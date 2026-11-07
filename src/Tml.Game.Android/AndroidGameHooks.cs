using Android.App;
using Android.Content;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Reflection.Emit;

namespace Tml.Mobile.Android;

internal sealed class AndroidGameHooks : IDisposable
{
    private readonly List<Hook> hooks = [];
    private GraphicsDeviceManager? graphics;
    private EventHandler<EventArgs>? deviceReady;

    internal AndroidGameHooks(Assembly game, Activity owner)
    {
        try
        {
            MethodInfo url = game.GetType("Terraria.Utils", true)!.GetMethod("OpenToURL", [typeof(string)])
                ?? throw new MissingMethodException("Terraria.Utils.OpenToURL(string)");
            hooks.Add(new Hook(url, (Action<Action<string>, string>)((original, address) => Open(owner, address))));
            Type main = game.GetType("Terraria.Main", true)!;
            const BindingFlags fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            int minWidth = (int)main.GetField("minScreenW", fields)!.GetValue(null)!;
            int minHeight = (int)main.GetField("minScreenH", fields)!.GetValue(null)!;
            int maxWidth = (int)main.GetField("maxScreenW", fields)!.GetValue(null)!;
            int maxHeight = (int)main.GetField("maxScreenH", fields)!.GetValue(null)!;
            var size = MobileGraphicsEXT.Size;
            if (size.Width < minWidth || size.Height < minHeight || size.Width > maxWidth || size.Height > maxHeight)
                throw new InvalidOperationException($"Render size {size.Width}x{size.Height} violates Terraria limits {minWidth}x{minHeight}..{maxWidth}x{maxHeight}");
            GameEntry.Log($"ANDROID_GAME_DISPLAY_LIMITS min={minWidth}x{minHeight} render={size.Width}x{size.Height}");
            graphics = (GraphicsDeviceManager)main.GetField("graphics", fields)!.GetValue(null)!;
            deviceReady = (_, _) =>
            {
                graphics.DeviceCreated -= deviceReady;
                AndroidGraphicsProbe.Run(graphics.GraphicsDevice);
            };
            if (graphics.GraphicsDevice is not null) AndroidGraphicsProbe.Run(graphics.GraphicsDevice);
            else graphics.DeviceCreated += deviceReady;
            MethodInfo display = main.GetMethod("SetDisplayMode", [typeof(int), typeof(int), typeof(bool)])
                ?? throw new MissingMethodException("Terraria.Main.SetDisplayMode(int,int,bool)");
            var screenWidth = ReadInt(main.GetField("screenWidth", fields)!);
            var screenHeight = ReadInt(main.GetField("screenHeight", fields)!);
            int repairs = 0;
            hooks.Add(new Hook(display, (Action<Action<int, int, bool>, int, int, bool>)((original, w, h, full) =>
            {
                var desired = MobileGraphicsEXT.Size;
                int beforeWidth = screenWidth(), beforeHeight = screenHeight();
                bool repair = beforeWidth != desired.Width || beforeHeight != desired.Height;
                if (repair)
                {
                    graphics.PreferredBackBufferWidth = beforeWidth;
                    graphics.PreferredBackBufferHeight = beforeHeight;
                }
                original(desired.Width, desired.Height, true);
                if (repair && ++repairs <= 8)
                    GameEntry.Log($"ANDROID_GAME_DISPLAY_SYNC from={beforeWidth}x{beforeHeight} game={screenWidth()}x{screenHeight()} buffer={graphics.PreferredBackBufferWidth}x{graphics.PreferredBackBufferHeight}");
            })));

            GameEntry.Log("ANDROID_GAME_HOOKS_READY url-intent+stable-display");
        }
        catch { Dispose(); throw; }
    }

    private static Func<int> ReadInt(FieldInfo field)
    {
        var method = new DynamicMethod("Read_" + field.Name, typeof(int), Type.EmptyTypes, field.DeclaringType!, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, field);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<int>>();
    }

    private static void Open(Activity owner, string address)
    {
        // Opening a link must never throw through Terraria's drawing method.
        if (!System.Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http" && uri.Scheme != "mailto" && uri.Scheme != "steam"))
        { GameEntry.Log("ANDROID_URL_REJECTED unsupported or invalid URI"); return; }
        owner.RunOnUiThread(() =>
        {
            try { owner.StartActivity(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(address))); }
            catch (Exception e) { GameEntry.Log("ANDROID_URL_OPEN_FAILED " + e.Message); }
        });
    }

    public void Dispose() { if (graphics != null && deviceReady != null) graphics.DeviceCreated -= deviceReady; for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose(); hooks.Clear(); }
}
