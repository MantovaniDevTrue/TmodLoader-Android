using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;

namespace Tml.GameIntegration;

internal sealed class MetadataAssemblyPatch : IDisposable
{
    private readonly Assembly referencePack;
    private readonly Dictionary<string, string> resources;
    private readonly Dictionary<string, string> images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Hook hook;

    internal MetadataAssemblyPatch(string root, Action<string> log)
    {
        string game = Path.Combine(root, "game");
        referencePack = Assembly.Load("Basic.Reference.Assemblies.Net100");
        resources = referencePack.GetManifestResourceNames()
            .Where(name => name.StartsWith("net100.", StringComparison.Ordinal))
            .ToDictionary(name => name[7..], name => name, StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(Path.Combine(game, "Libraries"), "*.dll", SearchOption.AllDirectories))
            if (!path.Contains("/runtimes/") && !path.Contains("/Native/"))
                images.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        foreach (string name in new[] { "FNA", "Tml.Mobile.Core" })
        {
            string path = Path.Combine(game, "Metadata", name + ".metadata.dll");
            if (!File.Exists(path)) throw new FileNotFoundException("Required host metadata image is missing", path);
            images[name] = path;
        }
        var resolver = typeof(Main).Assembly.GetType("Terraria.ModLoader.Core.AssemblyManager+ModLoadContext+MetadataResolver", true)!;
        var method = resolver.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Instance)!;
        hook = new Hook(method, (Func<Func<MetadataAssemblyResolver, MetadataLoadContext, AssemblyName, Assembly>, MetadataAssemblyResolver, MetadataLoadContext, AssemblyName, Assembly>)Resolve);
        log($"ANDROID_MOD_METADATA_READY framework={resources.Count} external-images={images.Count}");
    }

    private Assembly Resolve(Func<MetadataAssemblyResolver, MetadataLoadContext, AssemblyName, Assembly> original,
        MetadataAssemblyResolver resolver, MetadataLoadContext context, AssemblyName name)
    {
        if (name.Name != null && resources.TryGetValue(name.Name, out string? resource))
        {
            using var stream = referencePack.GetManifestResourceStream(resource)!;
            return context.LoadFromStream(stream);
        }
        if (name.Name != null && images.TryGetValue(name.Name, out string? path))
            return context.LoadFromAssemblyPath(path);
        return original(resolver, context, name);
    }

    public void Dispose() => hook.Dispose();
}
