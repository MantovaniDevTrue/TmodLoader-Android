using System.Threading;
using Microsoft.Xna.Framework.Graphics;
using Tml.Mobile;

namespace Microsoft.Xna.Framework
{
    public static class MobileGraphicsEXT
    {
        public static bool Enabled { get; private set; }
        public static RenderSize Size { get; private set; }
        private static int resetsAvoided;
        private static int suspended;
        public static bool CanDraw => Volatile.Read(ref suspended) == 0;
        public static void Suspend(bool value) => Volatile.Write(ref suspended, value ? 1 : 0);
        public static int ResetsAvoided => Volatile.Read(ref resetsAvoided);

        public static void Enable(int width, int height)
        {
            Size = MobileRenderPolicy.Select(width, height);
            Enabled = true;
            resetsAvoided = 0;
        }

        internal static void Configure(GraphicsDeviceInformation information)
        {
            if (!Enabled) return;
            var p = information.PresentationParameters;
            p.BackBufferWidth = Size.Width;
            p.BackBufferHeight = Size.Height;
            p.IsFullScreen = true;
            p.DisplayOrientation = DisplayOrientation.LandscapeLeft;
            p.MultiSampleCount = 0;
            p.PresentationInterval = PresentInterval.One;
        }

        internal static bool SkipUnchanged(GraphicsDevice device, GraphicsDeviceInformation desired)
        {
            if (!Enabled) return false;
            var a = device.PresentationParameters;
            var b = desired.PresentationParameters;
            bool same = device.Adapter == desired.Adapter && device.GraphicsProfile == desired.GraphicsProfile &&
                a.BackBufferWidth == b.BackBufferWidth && a.BackBufferHeight == b.BackBufferHeight &&
                a.IsFullScreen == b.IsFullScreen && a.BackBufferFormat == b.BackBufferFormat &&
                a.DepthStencilFormat == b.DepthStencilFormat && a.MultiSampleCount == b.MultiSampleCount &&
                a.PresentationInterval == b.PresentationInterval && a.DeviceWindowHandle == b.DeviceWindowHandle &&
                a.RenderTargetUsage == b.RenderTargetUsage && a.DisplayOrientation == b.DisplayOrientation;
            if (same) Interlocked.Increment(ref resetsAvoided);
            return same;
        }
    }
}
