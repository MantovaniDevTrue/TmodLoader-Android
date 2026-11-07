using MonoMod.Cil;

namespace Tml.GameIntegration;

internal static class FrameGcPatch
{
    internal static void Rewrite(ILContext il, Action<int> skipped)
    {
        var cursor = new ILCursor(il);
        int count = 0;
        while (cursor.TryGotoNext(i => i.MatchCall("Terraria.Utilities.NewRuntimeMethods", "GC_Collect")))
        {
            cursor.Remove();
            cursor.EmitDelegate<Action<int, GCCollectionMode, bool>>((generation, _, _) => skipped(generation));
            count++;
        }
        if (count != 3) throw new InvalidOperationException("Unexpected EndDraw GC sites: " + count);
    }
}
