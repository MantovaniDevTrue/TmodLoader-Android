using Microsoft.Xna.Framework;

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

    private void BeforeRun(Game game)
    {
        if (initialized || game.GetType() != expectedType) return;
        initialize();
        initialized = true;
    }

    public void Dispose() => MobileGameLifecycleEXT.BeforeRun -= BeforeRun;
}
