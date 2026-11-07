using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Tml.Mobile.Android;

internal static class AndroidGraphicsProbe
{
    internal static bool UseOpenGL = true;
    [DllImport("libGLESv3.so", EntryPoint = "glGetError")]
    private static extern uint GetError();

    private static void CheckErrors(string phase)
    {
        uint error = UseOpenGL ? GetError() : 0;
        if (error != 0) throw new InvalidOperationException($"OpenGL error 0x{error:X} during {phase}");
    }

    internal static void Run(GraphicsDevice device)
    {
        var targets = device.GetRenderTargets();
        var viewport = device.Viewport;
        var scissor = device.ScissorRectangle;
        var blend = device.BlendState;
        var depth = device.DepthStencilState;
        var raster = device.RasterizerState;
        var sampler = device.SamplerStates[0];
        var texture = device.Textures[0];
        for (int i = 0; UseOpenGL && i < 16 && GetError() != 0; i++) { }
        using var a = new RenderTarget2D(device, 96, 64, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var b = new RenderTarget2D(device, 128, 96, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var red = new Texture2D(device, 1, 1);
        using var green = new Texture2D(device, 1, 1);
        using var blue = new Texture2D(device, 1, 1);
        red.SetData(new[] { Color.Red }); green.SetData(new[] { Color.Lime }); blue.SetData(new[] { Color.Blue });
        Texture2D[] pixels = [red, green, blue];
        Color[] palette = [Color.Red, Color.Lime, Color.Blue];
        using var batch = new SpriteBatch(device);
        try
        {
            device.SetRenderTarget(a);
            var depthViewport = device.Viewport;
            depthViewport.MinDepth = 0.25f; depthViewport.MaxDepth = 0.75f;
            device.Viewport = depthViewport;
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 0.5f, 0);
            CheckErrors("GLES depth range/clear");
            // 3072 sprites span the 2048-sprite dynamic buffer and 64 Begin/End
            // cycles. Texture changes force nonzero vertex offsets within batches.
            for (int pass = 0; pass < 64; pass++)
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                for (int col = 0; col < 48; col++)
                    batch.Draw(pixels[(pass + col) % 3], new Rectangle(col * 2, pass % 32 * 2, 2, 2), Color.White);
                batch.End();
            }
            CheckErrors("multi-texture sprite streaming");
            device.SetRenderTarget(b);
            device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            batch.Draw(a, new Rectangle(16, 16, 96, 64), Color.White);
            batch.End();
            device.SetRenderTarget(null);
            Color[] readback = new Color[128 * 96];
            b.GetData(readback);
            for (int row = 0; row < 32; row++)
                for (int col = 0; col < 48; col++)
                {
                    Color expected = palette[(row + 32 + col) % 3];
                    Color actual = readback[(17 + row * 2) * 128 + 17 + col * 2];
                    if (actual != expected)
                        throw new InvalidOperationException($"GPU composition mismatch at {col},{row}: expected {expected}, got {actual}");
                }
            if (readback[0] != Color.Black || readback[^1] != Color.Black)
                throw new InvalidOperationException("GPU viewport/scissor border mismatch");
            CheckErrors("render-target composition/readback");
            GameEntry.Log("ANDROID_GPU_STRESS_OK sprites=3072 textures=3 targets=2 verified-cells=1536");
        }
        finally
        {
            device.SetRenderTargets(targets);
            device.Viewport = viewport;
            device.ScissorRectangle = scissor;
            device.BlendState = blend; device.DepthStencilState = depth; device.RasterizerState = raster;
            device.SamplerStates[0] = sampler; device.Textures[0] = texture;
        }
    }

    internal sealed class FrameMonitor : IDisposable
    {
        private int frames, errors;
        internal FrameMonitor() => MobileGraphicsEXT.FramePresented += Sample;
        private void Sample()
        {
            if (++frames % 120 != 0 || errors >= 8) return;
            uint error = UseOpenGL ? GetError() : 0;
            if (error != 0) { errors++; GameEntry.Log($"ANDROID_GL_FRAME_ERROR frame={frames} code=0x{error:X}"); }
        }
        public void Dispose() => MobileGraphicsEXT.FramePresented -= Sample;
    }
}
