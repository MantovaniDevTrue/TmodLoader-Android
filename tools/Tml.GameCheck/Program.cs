using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: Tml.GameCheck <private-game-output>");
    return 2;
}

string root = Path.GetFullPath(args[0]);
PayloadContext? context = null;
try
{
    context = new PayloadContext(root);
    Assembly game = context.LoadFromAssemblyPath(Path.Combine(root, "tModLoader.dll"));
    Require(game.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName == ".NETCoreApp,Version=v10.0",
        "tModLoader must target .NET 10.");
    foreach (string name in new[] { "Terraria.Main", "Terraria.ModLoader.Mod", "Terraria.ModLoader.ModPlayer",
        "Terraria.ModLoader.ModNPC", "Terraria.ModLoader.ModItem" })
        Require(game.GetType(name, throwOnError: true) != null, name);

    Type main = game.GetType("Terraria.Main", throwOnError: true)!;
    Assembly fna = main.BaseType!.Assembly;
    Require(main.BaseType.FullName == "Microsoft.Xna.Framework.Game", "Terraria.Main must inherit the actual FNA Game.");
    Require(fna.GetName().Name == "FNA", "Unexpected graphics assembly.");
    Require(AssemblyLoadContext.GetLoadContext(fna) == context, "FNA must be resolved from this payload.");
    Type input = fna.GetType("Microsoft.Xna.Framework.Input.MobileInputEXT", throwOnError: true)!;
    Type timing = fna.GetType("Microsoft.Xna.Framework.MobilePerformanceEXT", throwOnError: true)!;
    Type router = input.GetProperty("Source")!.PropertyType;
    Type window = timing.GetProperty("Destination")!.PropertyType;
    Require(router.Assembly == window.Assembly && router.Assembly.GetName().Name == "Tml.Mobile.Core",
        "Input and timing must share one mobile core assembly.");

    string bridgePath = Path.Combine(root, "Tml.GameIntegration.dll");
    if (File.Exists(bridgePath))
    {
        var bridge = context.LoadFromAssemblyPath(bridgePath);
        Require(bridge.GetType("Tml.GameIntegration.MobileGameIntegration", true)!.GetMethod("Initialize")!.ReturnType == typeof(IDisposable), "Mobile integration entry contract");
        Require(bridge.GetType("Tml.GameIntegration.MobileGameIntegration", true)!.GetMethod("Initialize")!.GetParameters().Length == 4, "Physical-memory integration argument contract");
        Require(game.GetType("Terraria.ModLoader.UI.UIMemoryBar", true)!.GetMethod("GetTotalMemory")!.ReturnType == typeof(long), "Installed-memory UI hook contract");
        Require(main.GetMethod("EndDraw", BindingFlags.NonPublic | BindingFlags.Instance) != null, "Frame-end hook contract");
        Require(main.GetMethod("LoadContent", BindingFlags.NonPublic | BindingFlags.Instance) != null, "Content timing hook contract");
        Require(game.GetType("Terraria.IO.WorldFile", true)!.GetMethod("SaveWorld", [typeof(bool), typeof(bool), typeof(bool)]) != null, "Save timing hook contract");
        Require(game.GetType("Terraria.IngameOptions", true)!.GetMethod("Draw")!.GetParameters().Length == 2, "In-world touch options hook contract");
        Require(input.GetProperty("NativeTouchEnabled") != null && input.GetProperty("DirectActions") != null && input.GetEvent("BeforeUpdate") != null, "Native touch transport contract");
        Console.WriteLine("PASS: mobile game integration assembly and exact hooked API contracts.");
    }

    Console.WriteLine("PASS: .NET 10 tModLoader API types, FNA Game inheritance, mobile input/timing and shared core resolution.");
    Console.WriteLine("Assembly loading only: no game loop, Android native calls, MonoMod detours or mod gameplay tested.");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}
finally { context?.Unload(); }

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}

sealed class PayloadContext : AssemblyLoadContext
{
    private readonly Dictionary<string, string> assemblies = new(StringComparer.OrdinalIgnoreCase);

    public PayloadContext(string root) : base(isCollectible: true)
    {
        string libraries = Path.Combine(root, "Libraries");
        var paths = Directory.EnumerateFiles(root, "*.dll").Concat(
            Directory.EnumerateFiles(libraries, "*.dll", SearchOption.AllDirectories));
        foreach (string path in paths)
        {
            // Build tools, reference packs and native DLLs are not runtime dependencies.
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (relative.StartsWith("Libraries/Native/", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("Libraries/tModCodeAssist/", StringComparison.OrdinalIgnoreCase)
                || relative.StartsWith("Libraries/Basic.Reference.Assemblies", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("/runtimes/", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)) continue;
            AssemblyName identity;
            try { identity = AssemblyName.GetAssemblyName(path); }
            catch (BadImageFormatException) { continue; }
            if (!assemblies.TryAdd(identity.Name!, path))
                throw new InvalidDataException($"Duplicate managed runtime assembly: {identity.Name}");
        }
    }

    protected override Assembly? Load(AssemblyName name) =>
        assemblies.TryGetValue(name.Name!, out string? path) ? LoadFromAssemblyPath(path) : null;
}
