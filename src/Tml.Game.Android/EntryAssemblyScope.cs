using System.Reflection;

namespace Tml.Mobile.Android;

// SDL enters .NET through JNI, so Mono may have no managed entry assembly.
// Scope the host/game identity to the corresponding managed invocation.
internal sealed class EntryAssemblyScope : IDisposable
{
    private readonly Assembly? previous = Assembly.GetEntryAssembly();
    private bool disposed;

    internal EntryAssemblyScope(Assembly assembly) => Assembly.SetEntryAssembly(assembly);

    public void Dispose()
    {
        if (disposed) return;
        Assembly.SetEntryAssembly(previous);
        disposed = true;
    }
}
