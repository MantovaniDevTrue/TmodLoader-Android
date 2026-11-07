using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;

namespace Tml.GameIntegration;

internal sealed class TileDrawScratchPatch : IDisposable
{
    private readonly ILHook allocation;
    private readonly Hook scope;
    private delegate void DrawTile(TileDrawing self, Vector2 position, Vector2 offset, int x, int y);

    internal TileDrawScratchPatch(Action<string> log)
    {
        var method = typeof(TileDrawing).GetMethod("DrawSingleTile", BindingFlags.Instance | BindingFlags.NonPublic)!;
        allocation = new ILHook(method, Rewrite);
        try
        {
            scope = InstallScope(method);
        }
        catch { allocation.Dispose(); throw; }
        log("ANDROID_TILE_SCRATCH_READY per-thread+reentrant+reset-colors");
    }

    internal static Hook InstallScope(MethodBase method) =>
        new Hook(method, (Action<DrawTile, TileDrawing, Vector2, Vector2, int, int>)
                ((original, self, position, offset, x, y) =>
                {
                    TileDrawScratch.Enter();
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

    public void Dispose()
    {
        scope.Dispose();
        allocation.Dispose();
        TileDrawScratch.Clear();
    }
}

internal static class TileDrawScratch
{
    private const int Capacity = 8;
    [ThreadStatic] private static TileDrawInfo?[]? slots;
    [ThreadStatic] private static int depth;

    internal static void Enter() => depth++;

    internal static TileDrawInfo Rent()
    {
        if (depth <= 0) throw new InvalidOperationException("Tile scratch used outside draw scope");
        if (depth > Capacity) return new TileDrawInfo();
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
        depth--;
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

    internal static void Clear() { slots = null; depth = 0; }
}
