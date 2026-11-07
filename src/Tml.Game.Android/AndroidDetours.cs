using System.Reflection;
using System.Runtime.CompilerServices;
using MonoMod.Core.Platforms;
using MonoMod.RuntimeDetour;

namespace Tml.Mobile.Android;

internal static class AndroidDetours
{
    private static bool initialized;
    internal static void Initialize()
    {
        if (initialized) return;
        var system = new AndroidDetourSystem();
        var arch = PlatformTriple.CreateCurrentArchitecture(system);
        var runtime = PlatformTriple.CreateCurrentRuntime(system, arch);
        PlatformTriple.SetPlatformTriple(new PlatformTriple(arch, system, runtime));
        GameEntry.Log($"ANDROID_DETOUR_PLATFORM_READY {arch.Target} {runtime.Target}");
        // Fail here on the SDL thread, with a report, before background game
        // static constructors can poison AssemblyRedirects and abort the process.
        MethodInfo source = typeof(AndroidDetours).GetMethod(nameof(Probe), BindingFlags.NonPublic | BindingFlags.Static)!;
        if (Probe(3) != 4) throw new InvalidOperationException("Detour baseline failed");
        using (var hook = new Hook(source, (Func<Func<int, int>, int, int>)((orig, value) => orig(value) * 7)))
            if (Probe(3) != 28) throw new InvalidOperationException("Android managed hook/original call failed");
        if (Probe(3) != 4) throw new InvalidOperationException("Android hook removal failed");
        using (var first = new Hook(source, (Func<Func<int, int>, int, int>)((orig, value) => orig(value) + 7)))
        {
            using (var second = new Hook(source, (Func<Func<int, int>, int, int>)((orig, value) => orig(value) + 5)))
                if (Probe(3) != 16) throw new InvalidOperationException("Android hook chain failed");
            if (Probe(3) != 11) throw new InvalidOperationException("Android nested hook removal failed");
        }
        for (int index = 1; index <= 8; index++)
        {
            int add = index;
            using (var hook = new Hook(source, (Func<Func<int, int>, int, int>)((orig, value) => orig(value) + add)))
                if (Probe(index) != index + 1 + add) throw new InvalidOperationException("Android repeated hook apply failed");
            if (Probe(index) != index + 1) throw new InvalidOperationException("Android repeated hook removal failed");
        }
        initialized = true;
        GameEntry.Log("ANDROID_DETOUR_PROBE_OK apply+original+chain+repeat+remove");
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int Probe(int value) => value + 1;
}
