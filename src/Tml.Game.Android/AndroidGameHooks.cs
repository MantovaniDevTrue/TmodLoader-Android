using Android.App;
using Android.Content;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace Tml.Mobile.Android;

internal sealed class AndroidGameHooks : IDisposable
{
    private readonly List<Hook> hooks = [];

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
            MethodInfo display = main.GetMethod("SetDisplayMode", [typeof(int), typeof(int), typeof(bool)])
                ?? throw new MissingMethodException("Terraria.Main.SetDisplayMode(int,int,bool)");
            hooks.Add(new Hook(display, (Action<Action<int, int, bool>, int, int, bool>)((original, w, h, full) =>
                original(MobileGraphicsEXT.Size.Width, MobileGraphicsEXT.Size.Height, true))));
            GameEntry.Log("ANDROID_GAME_HOOKS_READY url-intent+stable-display");
        }
        catch { Dispose(); throw; }
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

    public void Dispose() { for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose(); hooks.Clear(); }
}
