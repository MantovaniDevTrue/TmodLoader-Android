using System.Reflection;
using System.Reflection.Emit;
using MonoMod.Utils;
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
        // Modern Android Mono lacks the legacy fields used by automatic DMD
        // detection. Cecil-generated assemblies fail private calls in originals.
        MonoMod.Switches.SetSwitchValue(MonoMod.Switches.DMDType, "dynamicmethod");
        var system = new AndroidDetourSystem();
        var arch = PlatformTriple.CreateCurrentArchitecture(system);
        var runtime = new AndroidMonoRuntime(PlatformTriple.CreateCurrentRuntime(system, arch));
        PlatformTriple.SetPlatformTriple(new PlatformTriple(arch, system, runtime));
        GameEntry.Log($"ANDROID_DETOUR_PLATFORM_READY {arch.Target} {runtime.Target}");
        // Fail here on the SDL thread, with a report, before background game
        // static constructors can poison AssemblyRedirects and abort the process.
        MethodInfo source = typeof(AndroidDetours).GetMethod(nameof(Probe), BindingFlags.NonPublic | BindingFlags.Static)!;
        using (var definition = new DynamicMethodDefinition(source))
        {
            var generated = definition.Generate();
            if (generated is not DynamicMethod || generated.CreateDelegate<Func<int, int>>()(3) != 4)
                throw new InvalidOperationException("Android DynamicMethod private-access copy failed");
            GameEntry.Log("ANDROID_DMD_BACKEND DynamicMethod skip-visibility");
        }
        if (Probe(3) != 4) throw new InvalidOperationException("Detour baseline failed");
        using (var hook = new Hook(source, (Func<Func<int, int>, int, int>)((orig, value) => orig(value) * 7)))
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Probe(3) != 28) throw new InvalidOperationException("Android managed hook/original call failed after GC");
        }
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
        GameEntry.Log("ANDROID_PRIVATE_ACCESS_OK private-call+field+generic+original+gc+remove");
        initialized = true;
        GameEntry.Log("ANDROID_DETOUR_PROBE_OK apply+original+chain+repeat+remove");
    }

    internal static GameRunScope VerifyDeferredBootstrap(Type gameType)
    {
        BootstrapState.PathsReady = false;
        var scope = new GameRunScope(gameType, () =>
        {
            if (!BootstrapState.PathsReady) throw new InvalidOperationException("Hook ran before bootstrap paths");
            var method = typeof(BootstrapProbe).GetMethod(nameof(BootstrapProbe.Target))!;
            using (var hook = new Hook(method, (Func<Func<int, int>, int, int>)((original, value) => original(value) * 2)))
                if (BootstrapProbe.Target(3) != 8) throw new InvalidOperationException("Deferred hook failed");
            if (BootstrapProbe.Target(3) != 4) throw new InvalidOperationException("Deferred hook removal failed");
            GameEntry.Log("ANDROID_DEFERRED_BOOTSTRAP_OK path-before-type-initializer+hook+remove");
        });
        // Registration above must not run BootstrapProbe's static constructor.
        BootstrapState.PathsReady = true;
        return scope;
    }

    private static class BootstrapState { internal static bool PathsReady; }
    private static class BootstrapProbe
    {
        static BootstrapProbe()
        {
            if (!BootstrapState.PathsReady) throw new InvalidOperationException("Static initializer ran before paths were ready");
        }
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static int Target(int value) => value + 1;
    }

    private static int probeOffset = 0;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int PrivateAdd(int value) => value + 1;
    private static class PrivateGeneric<T>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static T Echo(T value) => value;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int Probe(int value) => PrivateAdd(PrivateGeneric<int>.Echo(value)) + probeOffset;
}
