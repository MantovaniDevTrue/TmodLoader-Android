using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MonoMod.Core.Platforms;
using MonoMod.Utils;

namespace Tml.Mobile.Android;

// Keep MonoMod's ABI, pinning and compilation logic, but normalize modern
// Mono DynamicMethods before its legacy mhandle field lookup can run.
internal sealed class AndroidMonoRuntime(IRuntime inner) : IRuntime
{
    private static readonly MethodInfo create = typeof(DynamicMethod).GetMethod("CreateDynMethod", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new PlatformNotSupportedException("Mono DynamicMethod.CreateDynMethod is unavailable");
    private static readonly MethodInfo getInfo = typeof(DynamicMethod).GetMethod("GetRuntimeMethodInfo", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new PlatformNotSupportedException("Mono DynamicMethod.GetRuntimeMethodInfo is unavailable");
    private readonly ConditionalWeakTable<MethodBase, DynamicMethod> owners = new();

    private MethodBase Normalize(MethodBase method)
    {
        if (method is not DynamicMethod dynamic) return method;
        create.Invoke(dynamic, null);
        var info = (MethodBase)getInfo.Invoke(dynamic, null)!;
        // MonoMod pins the returned RuntimeMethodInfo. Keep its DynamicMethod
        // owner alive for precisely that lifetime without a permanent root.
        owners.GetValue(info, _ => dynamic);
        return info;
    }

    public RuntimeKind Target => inner.Target;
    public MonoMod.Core.Platforms.RuntimeFeature Features => inner.Features;
    public Abi Abi => inner.Abi;
    public event OnMethodCompiledCallback? OnMethodCompiled
    {
        add => inner.OnMethodCompiled += value;
        remove => inner.OnMethodCompiled -= value;
    }
    public MethodBase GetIdentifiable(MethodBase method) => inner.GetIdentifiable(Normalize(method));
    public RuntimeMethodHandle GetMethodHandle(MethodBase method) => inner.GetMethodHandle(Normalize(method));
    public bool RequiresGenericContext(MethodBase method) => inner.RequiresGenericContext(Normalize(method));
    public void DisableInlining(MethodBase method) => inner.DisableInlining(Normalize(method));
    public IDisposable? PinMethodIfNeeded(MethodBase method) => inner.PinMethodIfNeeded(Normalize(method));
    public IntPtr GetMethodEntryPoint(MethodBase method) => inner.GetMethodEntryPoint(Normalize(method));
    public void Compile(MethodBase method) => inner.Compile(Normalize(method));
}
