using System.Text;

namespace Tml.Mobile.Android;

// Selected fields from AOSP Android 14 debuggerd/proto/tombstone.proto:
// https://android.googlesource.com/platform/system/core/+/android-14.0.0_r1/debuggerd/proto/tombstone.proto
// Read only the crashing thread; do not include register/memory dumps or other apps.
internal static class NativeCrashTrace
{
    internal static string Read(Stream stream)
    {
        using var bytes = new MemoryStream();
        byte[] buffer = new byte[16384];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            if (bytes.Length + count > 8 * 1024 * 1024)
                throw new InvalidDataException("Native trace exceeds 8 MiB");
            bytes.Write(buffer, 0, count);
        }
        var fields = Fields(bytes.ToArray());
        ulong tid = Number(fields, 6);
        var result = new StringBuilder();
        var signal = fields.FirstOrDefault(f => f.Id == 10).Data;
        if (signal != null)
        {
            var info = Fields(signal);
            result.AppendLine($"Signal: {Text(info, 2)} ({Number(info, 1)}) {Text(info, 4)}");
        }
        string abort = Text(fields, 14);
        if (abort.Length > 0) result.AppendLine("Abort: " + abort);
        foreach (var cause in fields.Where(f => f.Id == 15 && f.Data != null))
            result.AppendLine("Cause: " + Text(Fields(cause.Data!), 1));
        foreach (var entry in fields.Where(f => f.Id == 16 && f.Data != null))
        {
            var map = Fields(entry.Data!);
            if (Number(map, 1) != tid) continue;
            byte[]? value = map.FirstOrDefault(f => f.Id == 2).Data;
            if (value == null) continue;
            var thread = Fields(value);
            result.AppendLine($"Thread: {tid} {Text(thread, 2)}");
            int index = 0;
            foreach (var frame in thread.Where(f => f.Id == 4 && f.Data != null).Take(24))
            {
                var data = Fields(frame.Data!);
                result.AppendLine($"#{index++:00} pc {Number(data, 1):x} {Text(data, 6)} ({Text(data, 4)}+{Number(data, 5)})");
            }
            break;
        }
        return result.ToString();
    }

    private readonly record struct Field(int Id, ulong Number, byte[]? Data);
    private static string Text(List<Field> fields, int id) =>
        fields.FirstOrDefault(f => f.Id == id).Data is byte[] bytes ? Encoding.UTF8.GetString(bytes) : "";
    private static ulong Number(List<Field> fields, int id) => fields.FirstOrDefault(f => f.Id == id).Number;

    private static List<Field> Fields(byte[] bytes)
    {
        int position = 0;
        var fields = new List<Field>();
        while (position < bytes.Length)
        {
            ulong tag = Varint(bytes, ref position);
            int id = checked((int)(tag >> 3));
            if (id == 0) throw new InvalidDataException("Invalid protobuf field");
            switch (tag & 7)
            {
                case 0: fields.Add(new(id, Varint(bytes, ref position), null)); break;
                case 1: Skip(bytes, ref position, 8); break;
                case 2:
                    int length = checked((int)Varint(bytes, ref position));
                    int start = position;
                    Skip(bytes, ref position, length);
                    fields.Add(new(id, 0, bytes.AsSpan(start, length).ToArray()));
                    break;
                case 5: Skip(bytes, ref position, 4); break;
                default: throw new InvalidDataException("Unsupported protobuf wire type");
            }
        }
        return fields;
    }

    private static void Skip(byte[] bytes, ref int position, int count)
    {
        if (count < 0 || count > bytes.Length - position) throw new InvalidDataException("Truncated protobuf field");
        position += count;
    }

    private static ulong Varint(byte[] bytes, ref int position)
    {
        ulong result = 0;
        for (int shift = 0; shift < 64; shift += 7)
        {
            if (position >= bytes.Length) throw new InvalidDataException("Truncated protobuf integer");
            byte value = bytes[position++];
            if (shift == 63 && value > 1) throw new InvalidDataException("Oversized protobuf integer");
            result |= (ulong)(value & 127) << shift;
            if ((value & 128) == 0) return result;
        }
        throw new InvalidDataException("Oversized protobuf integer");
    }
}
