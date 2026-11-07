using Microsoft.Xna.Framework;
using FnaGame = Microsoft.Xna.Framework.Game;

namespace Tml.Mobile.Android;

internal sealed class GameRunScope : IDisposable
{
    private readonly Type expectedType;
    private readonly Action initialize;
    private bool initialized;

    internal GameRunScope(Type expectedType, Action initialize)
    {
        this.expectedType = expectedType;
        this.initialize = initialize;
        MobileGameLifecycleEXT.BeforeRun += BeforeRun;
    }

    private void BeforeRun(FnaGame game)
    {
        if (initialized || game.GetType() != expectedType) return;
        initialize();
        initialized = true;
    }

    public void Dispose() => MobileGameLifecycleEXT.BeforeRun -= BeforeRun;
}
