using System;

namespace Microsoft.Xna.Framework
{
    public static class MobileGameLifecycleEXT
    {
        // Invoked by Game.Run after the derived game has been constructed.
        // Merely subscribing must not compile methods on the derived type.
        public static event Action<Game> BeforeRun;
        internal static void OnBeforeRun(Game game) => BeforeRun?.Invoke(game);
    }
}
