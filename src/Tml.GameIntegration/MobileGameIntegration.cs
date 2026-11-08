using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Tml.Mobile;
using MobileViewport = Tml.Mobile.Viewport;
using NVector2 = System.Numerics.Vector2;

namespace Tml.GameIntegration;

public sealed class MobileGameIntegration : IDisposable
{
    private readonly TouchRouter router;
    private readonly string profilePath;
    private readonly Action<string> log;
    private readonly List<IDisposable> hooks = new();
    private TouchProfile profile;
    private TouchControl[] controls = [], all = [];
    private SpriteBatch? batch;
    private Texture2D? circle, pixel;
    private int width, height, mode = -1, selected, dragId = -1;
    private bool editor, dirty, firstDraw = true;
    private Rectangle touchTab, panel;
    private readonly Rectangle[] commands = new Rectangle[10];
    private string[] commandText = [];
    private string selectedLabel = "";
    private long skippedCollections;
    private static int saving;
    private readonly Dictionary<MobileAction, Texture2D> icons = new();

    public static IDisposable Initialize(TouchRouter router, string root, Action<string> log, long installedMemory) => new MobileGameIntegration(router, root, log, installedMemory);

    private MobileGameIntegration(TouchRouter source, string root, Action<string> logger, long installedMemory)
    {
        router = source; log = logger;
        profilePath = Path.Combine(root, "Saves", "touch-controls.json");
        try { profile = TouchProfile.Load(profilePath); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { log("ANDROID_TOUCH_PROFILE_INVALID " + e.Message); profile = new(); }
        try
        {
            hooks.Add(new MetadataAssemblyPatch(root, log));
            var selection = typeof(Terraria.ModLoader.ModLoader).GetField("_enabledMods", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingFieldException("ModLoader._enabledMods");
            if (selection.FieldType != typeof(HashSet<string>)) throw new InvalidOperationException("Unexpected enabled-mod cache type");
            selection.SetValue(null, null);
            log("ANDROID_MOD_SELECTION_REFRESHED disk-on-next-read");
            hooks.Add(new TileDrawScratchPatch(log));
            if (installedMemory <= 0) throw new InvalidOperationException("Android physical memory unavailable");
            hooks.Add(new Hook(typeof(Main).Assembly.GetType("Terraria.ModLoader.UI.UIMemoryBar", true)!.GetMethod("GetTotalMemory")!,
                (Func<Func<long>, long>)(_ => installedMemory)));
            var endDraw = typeof(Main).GetMethod("EndDraw", BindingFlags.Instance | BindingFlags.NonPublic)!;
            hooks.Add(new ILHook(endDraw, il => FrameGcPatch.Rewrite(il, SkipFrameCollection)));
            var load = typeof(Main).GetMethod("LoadContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
            hooks.Add(new Hook(load, (Action<Action<Main>, Main>)((original, self) =>
            {
                long start = Stopwatch.GetTimestamp(); log("ANDROID_CONTENT_BEGIN");
                try { original(self); }
                finally { log("ANDROID_CONTENT_END ms=" + Stopwatch.GetElapsedTime(start).TotalMilliseconds.ToString("F0")); }
            })));
            foreach (string name in new[] { "LoadContent_Deferred", "LoadMusic_InSteps" })
            {
                string stage = name;
                var method = typeof(Main).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
                hooks.Add(new Hook(method, (Func<Func<Main, System.Collections.IEnumerator>, Main, System.Collections.IEnumerator>)
                    ((original, self) => MeasureLoading(original(self), stage))));
            }
            hooks.Add(new Hook(typeof(Terraria.IO.WorldFile).GetMethod("SaveWorld", [typeof(bool), typeof(bool), typeof(bool)])!,
                (Action<Action<bool, bool, bool>, bool, bool, bool>)((original, a, b, c) =>
                {
                    long start = Stopwatch.GetTimestamp(); Interlocked.Increment(ref saving); log("ANDROID_SAVE_WORLD_BEGIN");
                    try { original(a, b, c); }
                    finally { Interlocked.Decrement(ref saving); log("ANDROID_SAVE_WORLD_END ms=" + Stopwatch.GetElapsedTime(start).TotalMilliseconds.ToString("F0")); }
                })));
            hooks.Add(new Hook(typeof(TriggersPack).GetMethod("Update")!, (Action<Action<TriggersPack>, TriggersPack>)((original, self) => { if (ReferenceEquals(self, PlayerInput.Triggers)) ApplyActions(self.Current); original(self); })));
            hooks.Add(new Hook(typeof(IngameOptions).GetMethod("Draw")!, (Action<Action<Main, SpriteBatch>, Main, SpriteBatch>)((original, main, sb) => { if (!editor) original(main, sb); })));
            MobileInputEXT.BeforeUpdate += Update;
            MobileInputEXT.TouchInterceptor = Touch;
            Main.OnPostDraw += Draw;
            MobileInputEXT.NativeTouchEnabled = true;
            MobileInputEXT.DirectActions = true;
            Update();
            log("ANDROID_NATIVE_TOUCH_READY sdl-events+game-hud+options-editor");
            log("ANDROID_FRAME_GC_POLICY automatic; EndDraw forced collections removed=3");
        }
        catch { Dispose(); throw; }
    }

    private System.Collections.IEnumerator MeasureLoading(System.Collections.IEnumerator source, string stage)
    {
        long start = Stopwatch.GetTimestamp(), work = 0, worst = 0;
        int steps = 0;
        log("ANDROID_LOADING_STAGE_BEGIN " + stage);
        try
        {
            while (true)
            {
                long step = Stopwatch.GetTimestamp();
                bool more = source.MoveNext();
                long elapsed = Stopwatch.GetTimestamp() - step;
                work += elapsed; worst = Math.Max(worst, elapsed); steps++;
                if (!more) break;
                yield return source.Current;
            }
        }
        finally
        {
            (source as IDisposable)?.Dispose();
            log($"ANDROID_LOADING_STAGE_END {stage} wall-ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F0} work-ms={work * 1000d / Stopwatch.Frequency:F0} worst-step-ms={worst * 1000d / Stopwatch.Frequency:F0} steps={steps}");
        }
    }

    private void SkipFrameCollection(int generation)
    {
        skippedCollections++;
        if (generation != 2) return;
        double idleMilliseconds = (Main.TARGET_FRAME_TIME - Main.UpdateTimeAccumulator) * 1000;
        if (idleMilliseconds > 2 && idleMilliseconds < 20)
            Thread.Sleep((int)idleMilliseconds - 1);
    }

    private void ApplyActions(TriggersSet t)
    {
        if (editor || Main.gameMenu || Main.drawingPlayerChat || Main.editSign || Main.editChest) return;
        var f = MobileInputEXT.Frame;
        if ((f.Pressed & MobileAction.Escape) != 0)
        {
            if (Main.ingameOptionsWindow) IngameOptions.Close(); else IngameOptions.Open();
        }
        t.Inventory |= f.IsHeld(MobileAction.Inventory);
        if (Main.ingameOptionsWindow || Main.playerInventory) return;
        t.Left |= f.IsHeld(MobileAction.Left); t.Right |= f.IsHeld(MobileAction.Right);
        t.Up |= f.IsHeld(MobileAction.Up); t.Down |= f.IsHeld(MobileAction.Down);
        t.Jump |= f.IsHeld(MobileAction.Jump); t.Grapple |= f.IsHeld(MobileAction.Grapple);
        t.QuickHeal |= f.IsHeld(MobileAction.Heal); t.QuickMana |= f.IsHeld(MobileAction.Mana);
        t.QuickBuff |= f.IsHeld(MobileAction.Buff); t.QuickMount |= f.IsHeld(MobileAction.Mount);
        t.SmartCursor |= f.IsHeld(MobileAction.SmartCursor); t.MapFull |= f.IsHeld(MobileAction.Map);
        t.HotbarMinus |= f.IsHeld(MobileAction.HotbarPrevious); t.HotbarPlus |= f.IsHeld(MobileAction.HotbarNext);
        t.MouseLeft |= f.IsHeld(MobileAction.Attack) || profile.AttackWithAim && f.Aim.LengthSquared() > .04f;
        t.MouseRight |= f.IsHeld(MobileAction.Interact);
        if ((t.MouseLeft || f.Aim.LengthSquared() > .04f) && MathF.Abs(f.Aim.X) > .05f)
            Main.LocalPlayer.ChangeDir(f.Aim.X < 0 ? -1 : 1);
    }

    private void Update()
    {
        MobilePerformanceEXT.Scene = !MobileGraphicsEXT.CanDraw ? CaptureScene.Paused : Volatile.Read(ref saving) > 0 ? CaptureScene.Saving : Main.gameMenu ? CaptureScene.Menu : editor || Main.ingameOptionsWindow || Main.playerInventory ? CaptureScene.WorldUi : CaptureScene.World;
        if (!MobileGraphicsEXT.CanDraw) { router.SetContext(InputContext.Suspended); return; }
        int nextMode = Main.gameMenu ? 0 : editor ? 4 : Main.ingameOptionsWindow ? 3 : Main.playerInventory ? 2 : 1;
        var size = MobileGraphicsEXT.Size;
        if (width != size.Width || height != size.Height || mode != nextMode || dirty)
        {
            width = size.Width; height = size.Height; mode = nextMode; dirty = false;
            var area = new MobileViewport(18, 18, width - 36, height - 36);
            all = profile.Layout(area, true);
            controls = profile.Layout(area);
            if (mode == 2) controls = controls.Where(c => c.Action == MobileAction.Inventory || c.Action == MobileAction.Escape).ToArray();
            if (mode is 0 or 3 or 4) controls = [];
            router.Configure(new(0, 0, width, height), controls);
            touchTab = new Rectangle(width / 2 + 105, height / 2 - 238, 220, 36);
            if (touchTab.Right > width - 18) touchTab.X = width - 238;
            panel = new Rectangle(width / 2 - 310, 30, 620, 188);
            for (int i = 0; i < commands.Length; i++) commands[i] = new Rectangle(panel.X + 12 + i % 5 * 120, panel.Y + 72 + i / 5 * 50, 112, 42);
            RefreshText();
        }
        router.SetContext(mode is 0 or 3 or 4 ? InputContext.Menu : InputContext.Gameplay);
        if (!Main.gameMenu && Main.LocalPlayer != null)
        {
            var origin = Vector2.Transform(Main.LocalPlayer.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
            MobileInputEXT.AimOriginX = origin.X / width;
            MobileInputEXT.AimOriginY = origin.Y / height;
        }
        if (editor && Keyboard.GetState().IsKeyDown(Keys.Escape)) CloseEditor();
    }

    private bool Touch(TouchPoint p)
    {
        if (!editor)
        {
            if (!Main.gameMenu && Main.ingameOptionsWindow && touchTab.Contains((int)p.Position.X, (int)p.Position.Y))
            {
                if (p.Phase == TouchPhase.Down) { editor = true; dirty = true; router.CancelAll(); log("ANDROID_TOUCH_EDITOR_OPEN"); }
                return true;
            }
            return false;
        }
        if (p.Phase == TouchPhase.Cancel) { dragId = -1; return true; }
        if (p.Phase == TouchPhase.Down)
        {
            for (int i = 0; i < commands.Length; i++)
                if (commands[i].Contains((int)p.Position.X, (int)p.Position.Y)) { Command(i); return true; }
            if (panel.Contains((int)p.Position.X, (int)p.Position.Y)) return true;
            for (int i = all.Length - 1; i >= 0; i--)
                if (all[i].Contains(p.Position)) { selected = i; dragId = p.Id; RefreshText(); break; }
        }
        if (p.Id == dragId && p.Phase is TouchPhase.Move or TouchPhase.Up)
        {
            var c = all[selected];
            var old = Placement(c);
            profile.Buttons[c.Id] = old with { X = Math.Clamp((p.Position.X - 18) / (width - 36), 0, 1), Y = Math.Clamp((p.Position.Y - 18) / (height - 36), 0, 1) };
            dirty = true;
            if (p.Phase == TouchPhase.Up) dragId = -1;
        }
        return true;
    }

    private ButtonPlacement Placement(TouchControl c) => profile.Buttons.TryGetValue(c.Id, out var value) ? value : new((c.Center.X - 18) / (width - 36), (c.Center.Y - 18) / (height - 36));

    private void Command(int index)
    {
        var c = all[Math.Clamp(selected, 0, all.Length - 1)];
        var p = Placement(c);
        switch (index)
        {
            case 0: CloseEditor(); return;
            case 1: selected = (selected + 1) % all.Length; break;
            case 2: profile.Buttons[c.Id] = p with { Size = Math.Clamp(p.Size - .1f, .5f, 2) }; break;
            case 3: profile.Buttons[c.Id] = p with { Size = Math.Clamp(p.Size + .1f, .5f, 2) }; break;
            case 4: profile.Buttons[c.Id] = p with { Visible = !p.Visible }; break;
            case 5: profile.Opacity = Math.Clamp(profile.Opacity - .1f, .15f, 1); break;
            case 6: profile.Opacity = Math.Clamp(profile.Opacity + .1f, .15f, 1); break;
            case 7: profile.AttackWithAim = !profile.AttackWithAim; break;
            case 8:
                foreach (var button in all) { var b = Placement(button); profile.Buttons[button.Id] = b with { X = 1 - b.X }; }
                break;
            case 9: profile = new(); selected = 0; break;
        }
        dirty = true; RefreshText();
    }

    private void RefreshText()
    {
        selected = Math.Clamp(selected, 0, all.Length - 1);
        selectedLabel = "Selecionado: " + all[selected].Label + " · arraste os botões para mover";
        commandText = ["Salvar / voltar", "Próximo botão", "Tamanho −", "Tamanho +", "Mostrar / ocultar", "Opacidade −", "Opacidade +", profile.AttackWithAim ? "Mira atira: sim" : "Mira atira: não", "Espelhar", "Restaurar"];
    }

    private void CloseEditor()
    {
        try { profile.Save(profilePath); log("ANDROID_TOUCH_PROFILE_SAVED"); }
        catch (IOException e) { log("ANDROID_TOUCH_PROFILE_SAVE_FAILED " + e.Message); return; }
        editor = false; dragId = -1; dirty = true; router.CancelAll();
    }

    private void Draw(GameTime time)
    {
        if (Main.gameMenu || FontAssets.MouseText == null || !FontAssets.MouseText.IsLoaded) return;
        if (batch == null)
        {
            var device = Main.instance.GraphicsDevice;
            batch = new SpriteBatch(device);
            pixel = new Texture2D(device, 1, 1); pixel.SetData(new[] { Color.White });
            circle = new Texture2D(device, 64, 64);
            var colors = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new(x, y), new(31.5f));
                colors[y * 64 + x] = d < 29 ? new Color(27, 43, 61, 220) : d < 31 ? new Color(174, 207, 220, 220) : Color.Transparent;
            }
            circle.SetData(colors);
            foreach (var action in new[] { (MobileAction.Jump, ItemID.CloudinaBottle), (MobileAction.Attack, ItemID.WoodenSword),
                (MobileAction.Interact, ItemID.Wrench), (MobileAction.Grapple, ItemID.GrapplingHook),
                (MobileAction.Heal, ItemID.HealingPotion), (MobileAction.Mana, ItemID.ManaPotion),
                (MobileAction.Buff, ItemID.IronskinPotion), (MobileAction.Mount, ItemID.SlimySaddle),
                (MobileAction.Inventory, ItemID.PiggyBank), (MobileAction.Map, ItemID.Compass), (MobileAction.SmartCursor, ItemID.Ruler) })
            {
                Main.instance.LoadItem(action.Item2);
                icons[action.Item1] = TextureAssets.Item[action.Item2].Value;
            }
        }
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        if (editor)
        {
            batch.Draw(pixel, new Rectangle(0, 0, width, height), Color.Black * .35f);
            for (int i = 0; i < all.Length; i++) DrawControl(all[i], i == selected ? 1 : .5f, true);
            batch.Draw(pixel, panel, new Color(20, 28, 48, 245));
            Text("Controles touch", panel.X + 14, panel.Y + 6, .9f);
            Text(selectedLabel, panel.X + 14, panel.Y + 36, .68f);
            for (int i = 0; i < commands.Length; i++) Button(commands[i], commandText[i]);
        }
        else
        {
            foreach (var c in controls) DrawControl(c, profile.Opacity, false);
            if (Main.ingameOptionsWindow) Button(touchTab, "Controles touch");
        }
        batch.End();
        if (firstDraw) { firstDraw = false; log("ANDROID_NATIVE_HUD_DRAWN"); }
    }

    private void DrawControl(TouchControl c, float opacity, bool label)
    {
        var center = new Vector2(c.Center.X, c.Center.Y);
        var f = MobileInputEXT.Frame;
        float active = f.IsHeld(c.Action) ? 1 : opacity;
        batch!.Draw(circle, center, null, Color.White * active, 0, new Vector2(32), c.Radius / 32, SpriteEffects.None, 0);
        if (c.Kind != ControlKind.Button)
        {
            var stick = c.Kind == ControlKind.MovementStick ? f.Movement : f.Aim;
            var p = center + new Vector2(stick.X, stick.Y) * c.Radius * .6f;
            batch.Draw(circle, p, null, Color.LightBlue * active, 0, new Vector2(32), c.Radius / 80, SpriteEffects.None, 0);
        }
        else if (icons.TryGetValue(c.Action, out var icon))
        {
            float scale = c.Radius * 1.12f / Math.Max(icon.Width, icon.Height);
            batch.Draw(icon, center, null, Color.White * active, 0, new Vector2(icon.Width, icon.Height) / 2, scale, SpriteEffects.None, 0);
            if (label) Text(c.Label, center.X, center.Y + c.Radius + 2, .6f, true);
        }
        else Text(c.Label, center.X, center.Y - 7, MathF.Min(.65f, c.Radius / 48), true, active);
        if (label && c.Kind != ControlKind.Button) Text(c.Label, center.X, center.Y - c.Radius - 22, .65f, true);
    }

    private void Button(Rectangle area, string text)
    {
        batch!.Draw(pixel, area, new Color(51, 73, 106));
        Text(text, area.Center.X, area.Center.Y - 8, .62f, true);
    }

    private void Text(string text, float x, float y, float scale, bool centered = false, float opacity = 1) => Utils.DrawBorderString(batch!, text, new(x, y), Color.White * opacity, scale, centered ? .5f : 0);

    public void Dispose()
    {
        MobileInputEXT.BeforeUpdate -= Update;
        MobileInputEXT.TouchInterceptor = null;
        MobileInputEXT.NativeTouchEnabled = false;
        MobileInputEXT.DirectActions = false;
        Main.OnPostDraw -= Draw;
        for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
        hooks.Clear(); batch?.Dispose(); circle?.Dispose(); pixel?.Dispose();
        log("ANDROID_FRAME_GC_SKIPPED " + skippedCollections);
    }
}
