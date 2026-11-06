namespace Tml.Mobile;

public readonly record struct ContentIssue(string Root, string Asset, string Reason);

public static class ContentPreflight
{
    private static readonly string[] VanillaAssets = [
        "Images/SplashScreens/Splash_1.xnb",
        "Images/Item_1.xnb", "Images/Tiles_0.xnb"
    ];
    private static readonly string[] PlatformAssets = ["PixelShader.xnb", "TileShader.xnb", "ScreenShader.xnb"];

    // This catches known startup failures. Passing is not proof of a complete/matching content tree.
    public static IReadOnlyList<ContentIssue> Validate(string vanillaRoot, string platformRoot)
    {
        var issues = new List<ContentIssue>();
        Check(vanillaRoot, VanillaAssets, issues);
        foreach (string asset in PlatformAssets)
        {
            string root = File.Exists(Path.Combine(platformRoot, asset)) ? platformRoot : vanillaRoot;
            Check(root, [asset], issues);
        }
        return issues;
    }

    private static void Check(string root, string[] assets, List<ContentIssue> issues)
    {
        Span<byte> header = stackalloc byte[10];
        foreach (string asset in assets)
        {
            string path = Path.Combine(root, asset.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { issues.Add(new(root, asset, "Arquivo ausente")); continue; }
            try
            {
                using var stream = File.OpenRead(path);
                if (stream.Read(header) != 10 || header[0] != 'X' || header[1] != 'N' || header[2] != 'B')
                    issues.Add(new(root, asset, "Cabeçalho XNB inválido"));
            }
            catch (IOException error) { issues.Add(new(root, asset, error.Message)); }
            catch (UnauthorizedAccessException error) { issues.Add(new(root, asset, error.Message)); }
        }
    }
}
