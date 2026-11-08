using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

internal static class LzxChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var assembly = typeof(Game).Assembly;
        var decoderType = assembly.GetType("Microsoft.Xna.Framework.Content.LzxDecoder", true)!;
        var poolType = assembly.GetType("Microsoft.Xna.Framework.Content.LzxDecoderPool", true)!;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var rent = Expression.Lambda<Func<object>>(Expression.Convert(Expression.Call(poolType.GetMethod("Rent", flags)!), typeof(object))).Compile();
        var obj = Expression.Parameter(typeof(object));
        var giveBack = Expression.Lambda<Action<object>>(Expression.Call(poolType.GetMethod("Return", flags)!, Expression.Convert(obj, decoderType)), obj).Compile();
        var create = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(decoderType.GetConstructor([typeof(int)])!, Expression.Constant(16)), typeof(object))).Compile();
        var input = Expression.Parameter(typeof(Stream)); var length = Expression.Parameter(typeof(int));
        var output = Expression.Parameter(typeof(Stream)); var size = Expression.Parameter(typeof(int));
        var decode = Expression.Lambda<Func<object, Stream, int, Stream, int, int>>(Expression.Call(Expression.Convert(obj, decoderType), decoderType.GetMethod("Decompress")!, input, length, output, size), obj, input, length, output, size).Compile();
        var reset = Expression.Lambda<Action<object>>(Expression.Call(Expression.Convert(obj, decoderType), decoderType.GetMethod("ResetEXT", BindingFlags.Instance | BindingFlags.NonPublic)!), obj).Compile();

        byte[] Decode(object decoder, byte[] encoded, int count, bool partial = false)
        {
            using var source = partial ? new ShortRead(encoded) : new MemoryStream(encoded);
            using var target = new MemoryStream();
            int result = decode(decoder, source, encoded.Length, target, count);
            if (result != 0) throw new Exception("LZX fixture did not decode: " + result);
            return target.ToArray();
        }
        foreach (int kind in new[] { 1, 2, 3 })
        {
            byte[] expected = Enumerable.Range(0, 128).Select(i => (byte)(i % 2 == 0 ? 65 : 66)).ToArray();
            byte[] encoded = Encode(kind, expected);
            check(Decode(create(), encoded, expected.Length).SequenceEqual(expected), "fresh LZX fixture decodes block type " + kind);
            for (int i = 0; i < 4; i++)
            {
                object decoder = rent();
                try { check(Decode(decoder, encoded, expected.Length, kind == 3).SequenceEqual(expected), "reused LZX preserves bytes and independent file state " + kind); }
                finally { giveBack(decoder); }
            }
        }
        object reused = rent();
        var state = decoderType.GetField("m_state", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object dirty = state.GetValue(reused)!;
        foreach (var field in dirty.GetType().GetFields())
        {
            if (field.GetValue(dirty) is Array array)
                for (int i = 0; i < array.Length; i++) array.SetValue(Convert.ChangeType(3, array.GetType().GetElementType()!), i);
            else field.SetValue(dirty, field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, 3) : Convert.ChangeType(3, field.FieldType));
        }
        state.SetValue(reused, dirty);
        reset(reused);
        object clean = state.GetValue(create())!, restored = state.GetValue(reused)!;
        foreach (var field in clean.GetType().GetFields())
        {
            object? a = field.GetValue(restored), b = field.GetValue(clean);
            check(a is Array aa && b is Array bb ? aa.Cast<object>().SequenceEqual(bb.Cast<object>()) : Equals(a, b), "LZX reset restores constructor state: " + field.Name);
        }
        byte[] large = Enumerable.Range(0, 98304).Select(i => (byte)i).ToArray();
        byte[] blocks = Encode(3, large);
        using (var source = new MemoryStream(blocks))
        using (var target = new MemoryStream())
        {
            check(decode(reused, source, 32784, target, 32768) == 0 && decode(reused, source, 32768, target, 32768) == 0 && decode(reused, source, 32768, target, 32768) == 0 && target.ToArray().SequenceEqual(large), "LZX retains state across frames and window wrap within a file");
        }
        giveBack(reused);
        var held = Enumerable.Range(0, 4).Select(_ => rent()).ToArray();
        check(held.Distinct(ReferenceEqualityComparer.Instance).Count() == held.Length, "concurrent LZX leases never alias");
        foreach (var value in held) giveBack(value);
        byte[] sample = Encode(1, Enumerable.Repeat((byte)65, 128).ToArray());
        Parallel.For(0, 32, _ => { var decoder = rent(); try { if (Decode(decoder, sample, 128).Any(b => b != 65)) throw new Exception("LZX parallel data corruption"); } finally { giveBack(decoder); } });
        check(true, "LZX parallel independent files decode correctly");
        using (var content = new FixtureContent())
        {
            check(content.Load<int>("first") == 42, "actual ContentManager reads compressed XNB with pooled decoder");
            bool rejected = false;
            try { content.Load<int>("broken"); } catch (ContentLoadException) { rejected = true; }
            check(rejected && content.Load<int>("second") == 42, "actual malformed XNB returns decoder and next file loads correctly");
        }
        long Measure(bool pooled)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) { var decoder = pooled ? rent() : create(); if (pooled) giveBack(decoder); else GC.KeepAlive(decoder); }
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
        Measure(false); long baseline = Measure(false); Measure(true); long optimized = Measure(true);
        check(optimized == 0, "1000 warmed LZX decoder leases allocate zero bytes");
        Console.WriteLine($"LZX decoder allocation: fresh={baseline / 1000} B/file pooled={optimized} B/1000 files; created={MobileContentEXT.LzxDecoderCreations} reused={MobileContentEXT.LzxDecoderReuses}");
    }

    private static byte[] Encode(int kind, byte[] data)
    {
        var bits = new List<int>();
        void Put(int value, int count) { for (int i = count - 1; i >= 0; i--) bits.Add(value >> i & 1); }
        Put(0, 1); Put(kind, 3); Put(data.Length, 24);
        if (kind != 3)
        {
            if (kind == 2) Put(0, 24);
            void Lengths(int first, int last)
            {
                for (int i = 0; i < 20; i++) Put(i is 0 or 16 ? 1 : 0, 4);
                for (int i = first; i < last; i++) Put(i is 65 or 66 && first == 0 && last == 256 ? 1 : 0, 1);
            }
            Lengths(0, 256); Lengths(256, 512);
            for (int i = 0; i < 20; i++) Put(i is 0 or 16 ? 1 : 0, 4);
            for (int i = 0; i < 249; i++) Put(0, 1);
            foreach (byte b in data) Put(b == 65 ? 0 : 1, 1);
        }
        while (bits.Count % 16 != 0) bits.Add(0);
        using var result = new MemoryStream();
        using var writer = new BinaryWriter(result);
        for (int i = 0; i < bits.Count; i += 16) { int word = 0; for (int j = 0; j < 16; j++) word = word << 1 | bits[i + j]; writer.Write((ushort)word); }
        if (kind == 3) { writer.Write(1); writer.Write(1); writer.Write(1); writer.Write(data); }
        else writer.Write((ushort)0);
        return result.ToArray();
    }

    private sealed class ShortRead(byte[] data) : MemoryStream(data)
    {
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(3, count));
    }

    private sealed class FixtureContent : ContentManager
    {
        internal FixtureContent() : base(new GameServiceContainer()) { }
        protected override Stream OpenStream(string assetName)
        {
            using var body = new MemoryStream();
            using var fields = new BinaryWriter(body);
            fields.Write7BitEncodedInt(1);
            fields.Write(typeof(Game).Assembly.GetType("Microsoft.Xna.Framework.Content.Int32Reader", true)!.AssemblyQualifiedName!);
            fields.Write(0); fields.Write7BitEncodedInt(0); fields.Write7BitEncodedInt(1); fields.Write(42); fields.Flush();
            byte[] raw = body.ToArray(), encoded = assetName == "broken" ? new byte[16] : Encode(3, raw);
            var file = new MemoryStream(); using var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, true);
            writer.Write("XNBw"u8); writer.Write((byte)5); writer.Write((byte)0x80);
            writer.Write(19 + encoded.Length); writer.Write(raw.Length);
            writer.Write((byte)0xff); writer.Write((byte)(raw.Length >> 8)); writer.Write((byte)raw.Length);
            writer.Write((byte)(encoded.Length >> 8)); writer.Write((byte)encoded.Length); writer.Write(encoded); writer.Flush();
            file.Position = 0; return file;
        }
    }
}
