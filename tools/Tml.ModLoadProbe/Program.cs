using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;

if(args.Length!=2)throw new ArgumentException("Usage: Tml.ModLoadProbe <private-game> <mod.tmod>");
string root=Path.GetFullPath(args[0]), modPath=Path.GetFullPath(args[1]);
string temporary=Path.Combine(Path.GetTempPath(),"tml-modload-"+Guid.NewGuid());
Environment.SetEnvironmentVariable("FNA_PLATFORM_BACKEND","NONE");
var paths=Directory.EnumerateFiles(root,"*.dll",SearchOption.AllDirectories)
    .Where(p=>!p.Contains("/runtimes/")&&!p.Contains("/Native/")&&!p.Contains("/Metadata/"))
    .GroupBy(Path.GetFileNameWithoutExtension,StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g=>g.Key!,g=>g.First(),StringComparer.OrdinalIgnoreCase);
AssemblyLoadContext.Default.Resolving+=(context,name)=>paths.TryGetValue(name.Name!,out var path)?
    name.Name is "FNA" or "Tml.Mobile.Core" ? context.LoadFromStream(new MemoryStream(File.ReadAllBytes(path))) : context.LoadFromAssemblyPath(path):null;
const BindingFlags instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;

foreach(string name in new[]{"Tml.Mobile.Core","FNA"})
{
    using var stream=File.OpenRead(Path.Combine(root,name+".dll"));
    var bundled=AssemblyLoadContext.Default.LoadFromStream(stream);
    if(bundled.Location.Length!=0)throw new Exception("Host assembly was not locationless");
    Console.WriteLine("Bundled host simulation: "+bundled.GetName().Name+" location=<empty>");
}
var game=Assembly.LoadFrom(Path.Combine(root,"tModLoader.dll"));
game.GetType("Terraria.Program",true)!.GetField("SavePath")!.SetValue(null,temporary);
RuntimeHelpers.RunClassConstructor(game.GetType("Terraria.Main",true)!.TypeHandle);
var contextType=game.GetType("Terraria.ModLoader.Core.AssemblyManager+ModLoadContext",true)!;
var packageType=game.GetType("Terraria.ModLoader.Core.TmodFile",true)!;
var package=packageType.GetConstructor(instance,null,[typeof(string),typeof(string),typeof(Version)],null)!.Invoke([modPath,null,null]);
using var open=(IDisposable)packageType.GetMethod("Open")!.Invoke(package,null)!;
var localType=game.GetType("Terraria.ModLoader.Core.LocalMod",true)!;
var locationType=game.GetType("Terraria.ModLoader.Core.ModLocation",true)!;
var local=Activator.CreateInstance(localType,instance,null,[Enum.Parse(locationType,"Local"),package],null)!;
object NewContext()=>Activator.CreateInstance(contextType,instance,null,[local],null)!;
object oldContext=NewContext();
var resolverType=game.GetType("Terraria.ModLoader.Core.AssemblyManager+ModLoadContext+MetadataResolver",true)!;
var oldResolver=Activator.CreateInstance(resolverType,instance,null,[oldContext],null)!;
var mlcType=Assembly.LoadFrom(paths["System.Reflection.MetadataLoadContext"]).GetType("System.Reflection.MetadataLoadContext",true)!;
using(var oldMetadata=(IDisposable)Activator.CreateInstance(mlcType,[oldResolver,"System.Private.CoreLib"])!)
{
    bool reproduced=false;
    try { resolverType.GetMethod("Resolve")!.Invoke(oldResolver,[oldMetadata,Assembly.Load("FNA").GetName()]); }
    catch(TargetInvocationException e) when(e.InnerException is InvalidCastException) { reproduced=true;Console.WriteLine("PASS actual original MetadataResolver reproduces locationless default-context assembly cast failure"); }
    if(!reproduced)throw new Exception("Expected locationless-host metadata regression was not reproduced");
}
((AssemblyLoadContext)oldContext).Unload();
var metadata=Path.Combine(temporary,"game/Metadata");Directory.CreateDirectory(metadata);
Directory.CreateDirectory(Path.Combine(temporary,"game/Libraries"));
foreach(string name in new[]{"FNA","Tml.Mobile.Core"})File.Copy(Path.Combine(root,name+".dll"),Path.Combine(metadata,name+".metadata.dll"));
var bridge=Assembly.LoadFrom(Path.Combine(root,"Tml.GameIntegration.dll"));
var patchType=bridge.GetType("Tml.GameIntegration.MetadataAssemblyPatch",true)!;
using var patch=(IDisposable)Activator.CreateInstance(patchType,instance,null,[temporary,(Action<string>)Console.WriteLine],null)!;
object fixedContext=NewContext();
try
{
    contextType.GetMethod("LoadAssemblies")!.Invoke(fixedContext,null);
    var types=(System.Collections.IDictionary)contextType.GetField("loadableTypes")!.GetValue(fixedContext)!;
    var loaded=types.Values.Cast<Type[]>().SelectMany(t=>t).ToArray();
    if(!loaded.Any(t=>t.FullName=="AndroidSwordTest.Content.Items.Weapons.ExampleSword"))throw new Exception("Sword was excluded from loadable types");
    Console.WriteLine("PASS actual ModLoadContext.LoadAssemblies and GetLoadableTypes with locationless host FNA/core; types="+loaded.Length);
}
finally { ((AssemblyLoadContext)fixedContext).Unload();Directory.Delete(temporary,true); }
Console.WriteLine("Headless CoreCLR regression; physical Android content/gameplay validation remains required.");
