using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;

namespace Tml.GameIntegration;

internal sealed class TileDrawScratchPatch : IDisposable
{
    private readonly ILHook allocation;
    private readonly Hook scope;
    private readonly ILHook callbacks;
    internal delegate void DrawInfoReference(ref TileDrawInfo value);
    private delegate void DrawTile(TileDrawing self, Vector2 position, Vector2 offset, int x, int y);

    internal TileDrawScratchPatch(Action<string> log)
    {
        var method = typeof(TileDrawing).GetMethod("DrawSingleTile", BindingFlags.Instance | BindingFlags.NonPublic)!;
        allocation = new ILHook(method, Rewrite);
        ILHook? escape = null;
        try
        {
            escape = new ILHook(typeof(Terraria.ModLoader.TileLoader).GetMethod("DrawEffects")!, RewriteCallbacks);
            scope = InstallScope(method, static () => true);
            callbacks = escape;
        }
        catch { escape?.Dispose(); allocation.Dispose(); throw; }
        log("ANDROID_TILE_SCRATCH_READY per-thread+reentrant+callback-escape");
    }

    internal static Hook InstallScope(MethodBase method, Func<bool> reusable) =>
        new Hook(method, (Action<DrawTile, TileDrawing, Vector2, Vector2, int, int>)
                ((original, self, position, offset, x, y) =>
                {
                    TileDrawScratch.Enter(reusable());
                    try { original(self, position, offset, x, y); }
                    finally { TileDrawScratch.Exit(); }
                }));

    internal static void Rewrite(ILContext il)
    {
        var cursor = new ILCursor(il);
        int count = 0;
        while (cursor.TryGotoNext(i => i.MatchNewobj<TileDrawInfo>()))
        {
            cursor.Remove();
            cursor.EmitDelegate<Func<TileDrawInfo>>(TileDrawScratch.Rent);
            count++;
        }
        if (count != 1) throw new InvalidOperationException("Unexpected tile draw allocation sites: " + count);
    }

    internal static void RewriteCallbacks(ILContext il)
    {
        var cursor = new ILCursor(il);
        int count = 0;
        while (cursor.TryGotoNext(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference m &&
            (m.DeclaringType.FullName == "Terraria.ModLoader.ModTile" && m.Name == "DrawEffects" ||
             m.DeclaringType.FullName == "Terraria.ModLoader.TileLoader/DelegateDrawEffects" && m.Name == "Invoke")))
        {
            cursor.Emit(OpCodes.Dup);
            cursor.EmitDelegate<DrawInfoReference>(TileDrawScratch.Escape);
            cursor.Index++;
            count++;
        }
        if (count != 2) throw new InvalidOperationException("Unexpected tile callback sites: " + count);
    }

    public void Dispose()
    {
        scope.Dispose();
        callbacks.Dispose();
        allocation.Dispose();
        TileDrawScratch.Clear();
    }
}

internal static class TileDrawScratch
{
    private const int Capacity = 8;
    [ThreadStatic] private static TileDrawInfo?[]? slots;
    [ThreadStatic] private static int depth;
    [ThreadStatic] private static int blockedDepth;

    internal static void Enter(bool reusable = true)
    {
        depth++;
        if (!reusable && blockedDepth == 0) blockedDepth = depth;
    }

    internal static TileDrawInfo Rent()
    {
        if (depth <= 0) throw new InvalidOperationException("Tile scratch used outside draw scope");
        if (blockedDepth != 0 || depth > Capacity) return new TileDrawInfo();
        slots ??= new TileDrawInfo?[Capacity];
        var value = slots[depth - 1] ??= new TileDrawInfo();
        Reset(value);
        return value;
    }

    internal static void Exit()
    {
        if (depth <= 0) throw new InvalidOperationException("Unbalanced tile draw scope");
        if (depth <= Capacity && slots?[depth - 1] is { } value)
        {
            value.drawTexture = null;
            value.glowTexture = null;
        }
        if (blockedDepth == depth) blockedDepth = 0;
        depth--;
    }

    internal static void Escape(ref TileDrawInfo value)
    {
        if (depth > 0 && depth <= Capacity && slots != null && ReferenceEquals(slots[depth - 1], value))
            slots[depth - 1] = null;
    }

    internal static void Reset(TileDrawInfo value)
    {
        value.tileCache = default;
        value.typeCache = 0;
        value.tileFrameX = value.tileFrameY = 0;
        value.drawTexture = value.glowTexture = null;
        value.tileLight = value.glowColor = value.finalColor = value.colorTint = default;
        value.tileTop = value.tileWidth = value.tileHeight = value.halfBrickHeight = 0;
        value.addFrX = value.addFrY = 0;
        value.tileSpriteEffect = default;
        value.glowSourceRect = default;
        Array.Clear(value.colorSlices);
    }

    internal static void Clear() { slots = null; depth = blockedDepth = 0; }
}
