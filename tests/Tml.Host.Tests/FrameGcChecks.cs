using MonoMod.Cil;
using MonoMod.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;
using Tml.GameIntegration;

internal static class FrameGcChecks
{
    internal static void Run(Action<bool, string> check)
    {
        using var dmd = new DynamicMethodDefinition(typeof(FrameGcChecks).GetMethod(nameof(Frame), BindingFlags.Static | BindingFlags.NonPublic)!);
        int skipped = 0;
        using var context = new ILContext(dmd.Definition);
        FrameGcPatch.Rewrite(context, _ => skipped++);
        var frame = dmd.Generate().CreateDelegate<Action>();
        for (int i = 0; i < 600; i++) frame();
        check(skipped == 1800 && Terraria.Utilities.NewRuntimeMethods.Calls == 0, "600 rewritten frames skip all forced GC sites");
        Terraria.Utilities.NewRuntimeMethods.GC_Collect(2, GCCollectionMode.Forced, true);
        check(Terraria.Utilities.NewRuntimeMethods.Calls == 1, "non-frame GC call remains available");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Frame()
    {
        Terraria.Utilities.NewRuntimeMethods.GC_Collect(0, GCCollectionMode.Forced, false);
        Terraria.Utilities.NewRuntimeMethods.GC_Collect(2, GCCollectionMode.Optimized, false);
        Terraria.Utilities.NewRuntimeMethods.GC_Collect(2, GCCollectionMode.Optimized, false);
    }
}
namespace Terraria.Utilities
{
    public static class NewRuntimeMethods
    {
        public static int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void GC_Collect(int generation, GCCollectionMode mode, bool blocking) => Calls++;
    }
}
