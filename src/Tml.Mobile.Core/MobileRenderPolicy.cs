namespace Tml.Mobile;

public readonly record struct RenderSize(int Width, int Height);

public static class MobileRenderPolicy
{
    // Pinned Terraria Main.minScreenH is 720, even in fullscreen mode.
    public const int MinimumWidth = 800;
    public const int MinimumHeight = 720;
    public const int PixelBudget = 1600 * MinimumHeight;

    public static RenderSize Select(int displayWidth, int displayHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0) throw new ArgumentOutOfRangeException(nameof(displayWidth));
        double aspect = Math.Clamp((double)Math.Max(displayWidth, displayHeight) / Math.Min(displayWidth, displayHeight), 4d / 3, 2.56);
        int width = Math.Clamp((int)(MinimumHeight * aspect / 8) * 8, MinimumWidth, PixelBudget / MinimumHeight);
        return new(width, MinimumHeight);
    }
}
