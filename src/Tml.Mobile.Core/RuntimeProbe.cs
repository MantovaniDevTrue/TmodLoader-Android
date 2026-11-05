using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Tml.Mobile;

public interface IRuntimeProbe { int Run(); }

public readonly record struct RuntimeReport(
    string Framework, string Architecture, bool DynamicCodeSupported,
    bool DynamicCodeCompiled, bool AssemblyLoaded, bool EmitSucceeded, string Details);

public static class RuntimeProbe
{
    public static RuntimeReport Run(string? assemblyPath)
    {
        bool loaded = false, emit = false;
        var details = new List<string>();
        if (assemblyPath is not null)
        {
            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
                var probe = (IRuntimeProbe?)Activator.CreateInstance(assembly.GetType("Tml.RuntimeProbe.Probe", throwOnError: true)!);
                loaded = probe?.Run() == 42;
                details.Add(loaded ? "DLL externa: OK" : "DLL externa: resultado inesperado");
            }
            catch (Exception error) { details.Add($"DLL externa: {error.GetType().Name}: {error.Message}"); }
        }
        else details.Add("DLL de teste não fornecida");

        try
        {
            var method = new DynamicMethod("MobileEmitProbe", typeof(int), Type.EmptyTypes);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4, 42);
            il.Emit(OpCodes.Ret);
            emit = method.CreateDelegate<Func<int>>()() == 42;
            details.Add(emit ? "Reflection.Emit: OK" : "Reflection.Emit: resultado inesperado");
        }
        catch (Exception error) { details.Add($"Reflection.Emit: {error.GetType().Name}: {error.Message}"); }

        return new(RuntimeInformation.FrameworkDescription, RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeFeature.IsDynamicCodeSupported, RuntimeFeature.IsDynamicCodeCompiled,
            loaded, emit, string.Join("\n", details));
    }
}
