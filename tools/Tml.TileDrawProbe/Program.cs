using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;
using Tml.GameIntegration;

if (args.Length != 1) throw new ArgumentException("Usage: Tml.TileDrawProbe <private-game>");
string root = Path.GetFullPath(args[0]);
var paths = Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
    .Where(p => !p.Contains("/runtimes/") && !p.Contains("/Metadata/"))
    .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);
AssemblyLoadContext.Default.Resolving += (context, name) => paths.TryGetValue(name.Name!, out var path) ? context.LoadFromAssemblyPath(path) : null;
Probe.Run();

static class Probe
{
    private static TileDrawInfo? drawn;
    private static TileDrawInfo? callbackRetained;
    private static bool replaceCallback, throwCallback;
    private static void Capture(int x, int y, int type, SpriteBatch batch, ref TileDrawInfo value)
    {
        callbackRetained = value;
        if (replaceCallback) value = new TileDrawInfo { typeCache = 99 };
        if (throwCallback) throw new IOException("callback fixture");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DrawFixture(TileDrawing self, Vector2 position, Vector2 offset, int x, int y)
    {
        var value = new TileDrawInfo();
        value.typeCache = (ushort)x;
        drawn = value;
        if (x < 0) throw new IOException("draw fixture");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static TileDrawInfo Construct() => new TileDrawInfo();

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        const int iterations = 10000;
        for (int i = 0; i < iterations; i++) GC.KeepAlive(Construct());
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) GC.KeepAlive(Construct());
        long baseline = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(baseline > 0, "baseline constructor really allocates");
        using var patch = new ILHook(typeof(Probe).GetMethod(nameof(Construct), BindingFlags.Static | BindingFlags.NonPublic)!, TileDrawScratchPatch.Rewrite);
        void Batch()
        {
            for (int i = 0; i < iterations; i++)
            {
                TileDrawScratch.Enter();
                try { GC.KeepAlive(Construct()); }
                finally { TileDrawScratch.Exit(); }
            }
        }
        long MeasureBatch()
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            Batch();
            return GC.GetAllocatedBytesForCurrentThread() - start;
        }
        MeasureBatch();
        long reused = MeasureBatch();
        Check(reused == 0, "patched constructor and scoped reuse allocate zero after warmup");
        Console.WriteLine($"MEASURE original={baseline / iterations}B/tile reused={reused}B/{iterations}tiles");

        TileDrawScratch.Enter();
        var outer = Construct();
        outer.typeCache = 98; outer.colorSlices[8] = Vector3.One;
        TileDrawScratch.Enter();
        var inner = Construct();
        Check(!ReferenceEquals(outer, inner), "nested render gets separate scratch");
        inner.typeCache = 25;
        TileDrawScratch.Exit();
        Check(outer.typeCache == 98 && outer.colorSlices[8] == Vector3.One, "nested render preserves outer values");
        TileDrawScratch.Exit();

        foreach (var field in typeof(TileDrawInfo).GetFields())
        {
            object? dirty = field.FieldType == typeof(Vector3[]) ? Enumerable.Repeat(Vector3.One, 9).ToArray() :
                field.FieldType == typeof(Texture2D) ? RuntimeHelpers.GetUninitializedObject(typeof(Texture2D)) :
                field.FieldType == typeof(Color) ? Color.White :
                field.FieldType == typeof(Rectangle) ? new Rectangle(1, 2, 3, 4) :
                field.FieldType.IsEnum ? Enum.ToObject(field.FieldType, 1) :
                field.FieldType.IsPrimitive ? Convert.ChangeType(1, field.FieldType) : null;
            if (dirty != null) field.SetValue(outer, dirty);
        }
        TileDrawScratch.Reset(outer);
        var fresh = new TileDrawInfo();
        foreach (var field in typeof(TileDrawInfo).GetFields())
        {
            var actual = field.GetValue(outer); var expected = field.GetValue(fresh);
            Check(actual is Vector3[] colors ? colors.Length == 9 && colors.All(c => c == Vector3.Zero) : Equals(actual, expected), "constructor defaults restored: " + field.Name);
        }
        var retained = outer.colorSlices;
        TileDrawScratch.Enter();
        try { throw new IOException("fixture"); }
        catch (IOException) { }
        finally { TileDrawScratch.Exit(); }
        TileDrawScratch.Enter();
        Check(ReferenceEquals(Construct(), outer) && ReferenceEquals(outer.colorSlices, retained), "exception leaves reusable scope and color storage");
        TileDrawScratch.Exit();
        for (int i = 0; i < 10; i++) { TileDrawScratch.Enter(); GC.KeepAlive(Construct()); }
        for (int i = 0; i < 10; i++) TileDrawScratch.Exit();
        TileDrawScratch.Clear();
        CheckThrows();

        var fixture = typeof(Probe).GetMethod(nameof(DrawFixture), BindingFlags.Static | BindingFlags.NonPublic)!;
        using var fixtureAllocation = new ILHook(fixture, TileDrawScratchPatch.Rewrite);
        bool reusable = true;
        using var fixtureScope = TileDrawScratchPatch.InstallScope(fixture, () => reusable);
        var renderer = (TileDrawing)RuntimeHelpers.GetUninitializedObject(typeof(TileDrawing));
        for (int i = 0; i < iterations; i++) DrawFixture(renderer, default, default, 1, 0);
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++) DrawFixture(renderer, default, default, 2, 0);
        Check(GC.GetAllocatedBytesForCurrentThread() - before == 0 && drawn!.typeCache == 2, "full allocation plus scope detour preserves draw and allocates zero");
        try { DrawFixture(renderer, default, default, -1, 0); }
        catch (IOException) { }
        CheckThrows();
        DrawFixture(renderer, default, default, 3, 0);
        Check(drawn!.typeCache == 3, "full draw detour recovers after exception");
        reusable = false;
        DrawFixture(renderer, default, default, 4, 0);
        var escaped = drawn;
        DrawFixture(renderer, default, default, 5, 0);
        Check(!ReferenceEquals(escaped, drawn) && escaped!.typeCache == 4, "external mod policy preserves distinct retained objects");
        reusable = true;
        DrawFixture(renderer, default, default, 6, 0);
        Check(!ReferenceEquals(escaped, drawn) && escaped!.typeCache == 4, "returning to reuse preserves external references");
        TileDrawScratch.Clear();
        VerifyCallbacks();
    }

    private static void VerifyCallbacks()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var hooks = typeof(TileLoader).GetField("HookDrawEffects", flags)!;
        var previous = hooks.GetValue(null);
        var delegateType = hooks.FieldType.GetElementType()!;
        using var patch = new ILHook(typeof(TileLoader).GetMethod("DrawEffects")!, TileDrawScratchPatch.RewriteCallbacks);
        try
        {
            hooks.SetValue(null, Array.CreateInstance(delegateType, 0));
            TileDrawScratch.Enter();
            var empty = Construct();
            TileLoader.DrawEffects(0, 0, 0, null!, ref empty);
            TileDrawScratch.Exit();
            TileDrawScratch.Enter();
            var again = Construct();
            Check(ReferenceEquals(empty, again), "actual TileLoader without callbacks keeps scratch reusable even with item mods");
            TileDrawScratch.Exit();
            var callbacks = Array.CreateInstance(delegateType, 1);
            callbacks.SetValue(Delegate.CreateDelegate(delegateType, typeof(Probe).GetMethod(nameof(Capture), flags)!), 0);
            hooks.SetValue(null, callbacks);
            foreach (bool replace in new[] { false, true })
            {
                replaceCallback = replace;
                TileDrawScratch.Enter();
                var value = Construct(); value.typeCache = 37; value.colorSlices[0] = Vector3.One;
                var escaped = value;
                TileLoader.DrawEffects(0, 0, 0, null!, ref value);
                TileDrawScratch.Exit();
                TileDrawScratch.Enter();
                Check(!ReferenceEquals(Construct(), escaped) && callbackRetained!.typeCache == 37 && callbackRetained.colorSlices[0] == Vector3.One, "actual global callback retains independent object and color storage");
                Check(!replace || value.typeCache == 99, "actual by-ref replacement preserves callback result");
                TileDrawScratch.Exit();
            }
            throwCallback = true; replaceCallback = false;
            TileDrawScratch.Enter();
            var failing = Construct(); failing.typeCache = 48;
            try { TileLoader.DrawEffects(0, 0, 0, null!, ref failing); }
            catch (IOException) { }
            finally { TileDrawScratch.Exit(); }
            TileDrawScratch.Enter();
            Check(!ReferenceEquals(Construct(), callbackRetained) && callbackRetained!.typeCache == 48, "throwing callback cannot leave an escaped object in the pool");
            TileDrawScratch.Exit();
            var tiles = (System.Collections.IList)typeof(TileLoader).GetField("tiles", flags)!.GetValue(null)!;
            var nextTile = typeof(TileLoader).GetField("nextTile", flags)!;
            object? savedNext = nextTile.GetValue(null);
            int index = tiles.Count;
            try
            {
                hooks.SetValue(null, Array.CreateInstance(delegateType, 0));
                throwCallback = replaceCallback = false;
                tiles.Add(new RetainingTile());
                nextTile.SetValue(null, Terraria.ID.TileID.Count + tiles.Count);
                TileDrawScratch.Enter();
                var modTile = Construct(); modTile.typeCache = 53;
                TileLoader.DrawEffects(0, 0, Terraria.ID.TileID.Count + index, null!, ref modTile);
                TileDrawScratch.Exit();
                TileDrawScratch.Enter();
                Check(ReferenceEquals(callbackRetained, modTile) && !ReferenceEquals(Construct(), modTile) && modTile.typeCache == 53, "actual ModTile virtual callback receives independent retained scratch");
                TileDrawScratch.Exit();
            }
            finally { if (tiles.Count > index) tiles.RemoveAt(index); nextTile.SetValue(null, savedNext); }
        }
        finally { hooks.SetValue(null, previous); TileDrawScratch.Clear(); }
    }

    private sealed class RetainingTile : ModTile
    {
        public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) => Capture(i, j, 0, spriteBatch, ref drawData);
    }

    private static void CheckThrows()
    {
        try { TileDrawScratch.Rent(); throw new Exception("scope guard missing"); }
        catch (InvalidOperationException) { Console.WriteLine("PASS unmatched scope rejected"); }
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
}
