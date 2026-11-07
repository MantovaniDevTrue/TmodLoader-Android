namespace Tml.Mobile;

public readonly record struct RenderSize(int Width, int Height);

public static class MobileRenderPolicy
{
    public const int PixelBudget = 1280 * 720;

    // Keep the PC UI's minimum height, a landscape aspect and a bounded GPU load.
    public static RenderSize Select(int displayWidth, int displayHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0) throw new ArgumentOutOfRangeException(nameof(displayWidth));
        double aspect = Math.Clamp((double)Math.Max(displayWidth, displayHeight) / Math.Min(displayWidth, displayHeight), 4d / 3, 2.56);
        int height = Math.Clamp((int)(Math.Sqrt(PixelBudget / aspect) / 8) * 8, 600, 720);
        int width = Math.Clamp((int)(height * aspect / 8) * 8, 800, PixelBudget / height / 8 * 8);
        return new(width, height);
    }
}
