using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

if (args.Length != 3) throw new ArgumentException("Usage: Tml.ModPackage <private-game> <mod-source> <output.tmod>");
string root = Path.GetFullPath(args[0]), source = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
string temporary = Path.Combine(Path.GetTempPath(), "tml-modpackage-" + Guid.NewGuid());
Environment.SetEnvironmentVariable("FNA_PLATFORM_BACKEND", "NONE");
var paths = Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
    .Where(p => !p.Contains("/runtimes/") && !p.Contains("/Native/"))
    .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g=>g.Key!,g=>g.First(),StringComparer.OrdinalIgnoreCase);
AssemblyLoadContext.Default.Resolving += (context,name)=>paths.TryGetValue(name.Name!,out var path)?context.LoadFromAssemblyPath(path):null;
var game = Assembly.LoadFrom(Path.Combine(root,"tModLoader.dll"));
const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
game.GetType("Terraria.Program",true)!.GetField("SavePath")!.SetValue(null,temporary);
RuntimeHelpers.RunClassConstructor(game.GetType("Terraria.Main",true)!.TypeHandle);
try
{
    string name = Path.GetFileName(source);
    var propertiesType = game.GetType("Terraria.ModLoader.Core.BuildProperties",true)!;
    object properties = propertiesType.GetMethod("ReadBuildFile",statics)!.Invoke(null,[source])!;
    var packageType = game.GetType("Terraria.ModLoader.Core.TmodFile",true)!;
    var ctor = packageType.GetConstructor(instance,null,[typeof(string),typeof(string),typeof(Version)],null)!;
    var version = (Version)propertiesType.GetField("version",instance)!.GetValue(properties)!;
    object package = ctor.Invoke([output,name,version]);
    var add = packageType.GetMethod("AddFile",instance)!;
    add.Invoke(package,["Info",propertiesType.GetMethod("ToBytes",instance)!.Invoke(properties,null)!]);
    string dll = Path.Combine(source,"bin/Release/net10.0",name+".dll");
    add.Invoke(package,[name+".dll",File.ReadAllBytes(dll)]);
    foreach (var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(source,file).Replace('\\','/');
        if(relative.StartsWith("bin/") || relative.StartsWith("obj/") || relative.EndsWith(".csproj"))continue;
        add.Invoke(package,[relative,File.ReadAllBytes(file)]);
    }
    packageType.GetMethod("Save",instance)!.Invoke(package,null);
    object reopened = ctor.Invoke([output,null,null]);
    using var open = (IDisposable)packageType.GetMethod("Open")!.Invoke(reopened,null)!;
    if (!(bool)packageType.GetMethod("VerifyHash",instance)!.Invoke(reopened,null)!)throw new InvalidDataException("Package hash failed");
    var readProperties = propertiesType.GetMethod("ReadModFile",statics)!.Invoke(null,[reopened])!;
    if(!version.Equals(propertiesType.GetField("version",instance)!.GetValue(readProperties)))throw new InvalidDataException("Package metadata mismatch");
    byte[] compiled = (byte[])packageType.GetMethod("GetBytes",[typeof(string)])!.Invoke(reopened,[name+".dll"])!;
    var assembly = Assembly.Load(compiled);
    foreach (var type in assembly.GetTypes()) Console.WriteLine("TYPE " + type.FullName);
    foreach (string asset in new[] { "Content/Items/Weapons/ExampleSword.png", "Content/Dusts/Sparkle.png", "Localization/pt-BR.hjson", "Localization/en-US.hjson", "LICENSE" })
        if (!(bool)packageType.GetMethod("HasFile")!.Invoke(reopened,[asset])!)throw new InvalidDataException("Missing asset: " + asset);
    var item = assembly.GetType(name+".Content.Items.Weapons.ExampleSword",true)!;
    if(!game.GetType("Terraria.ModLoader.ModItem",true)!.IsAssignableFrom(item))throw new InvalidDataException("Item must inherit actual ModItem");
    if(assembly.GetReferencedAssemblies().Any(a=>a.Name=="ExampleMod"))throw new InvalidDataException("Unexpected full ExampleMod dependency");
    Console.WriteLine($"PASS TMOD reopen/hash/Info/assembly/type checks: {name} {version}, tML {packageType.GetProperty("TModLoaderVersion")!.GetValue(reopened)}");
    Console.WriteLine("Desktop packaging check only; Android gameplay loading remains to be tested.");
}
finally {if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
