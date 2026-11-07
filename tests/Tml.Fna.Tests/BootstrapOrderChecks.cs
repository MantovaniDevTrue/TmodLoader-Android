using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Tml.Mobile.Android;

internal static class BootstrapOrderChecks
{
    internal static void Run(Action<bool, string> check)
    {
        int installed = 0;
        BootstrapState.Path = null;
        Type target = typeof(BootstrapGame);
        using var scope = new GameRunScope(target, () =>
        {
            RuntimeHelpers.RunClassConstructor(target.TypeHandle);
            installed++;
        });
        check(BootstrapState.Initialized == 0, "registering game hooks does not initialize a path-dependent game type");
        var dispatch = typeof(MobileGameLifecycleEXT).GetMethod("OnBeforeRun", BindingFlags.NonPublic | BindingFlags.Static)!;
        // No SDL window is needed to exercise the dispatch/lifetime contract.
        Game unrelated = (Game)RuntimeHelpers.GetUninitializedObject(typeof(UnrelatedGame));
        GC.SuppressFinalize(unrelated);
        dispatch.Invoke(null, [unrelated]);
        check(installed == 0, "unrelated FNA games cannot install private Terraria hooks");
        BootstrapState.Path = Path.GetTempPath();
        Game game = (Game)RuntimeHelpers.GetUninitializedObject(target);
        GC.SuppressFinalize(game);
        dispatch.Invoke(null, [game]);
        check(installed == 1 && BootstrapState.Initialized == 1, "path-dependent static initialization happens after bootstrap paths exist");
        dispatch.Invoke(null, [game]);
        check(installed == 1, "repeated Run dispatch cannot install duplicate game hooks");
        scope.Dispose();
        dispatch.Invoke(null, [game]);
        check(installed == 1, "unwound bootstrap scope removes its lifecycle subscription");
    }

    private static class BootstrapState { internal static string? Path; internal static int Initialized; }
    private sealed class UnrelatedGame : Game { }
    private sealed class BootstrapGame : Game
    {
        static BootstrapGame()
        {
            _ = System.IO.Path.Combine(BootstrapState.Path!, "Worlds");
            BootstrapState.Initialized++;
        }
    }
}
