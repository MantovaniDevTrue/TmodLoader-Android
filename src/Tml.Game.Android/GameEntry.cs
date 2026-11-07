using Android.App;
using Android.Runtime;
using Android.Views;
using Java.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Color = Microsoft.Xna.Framework.Color;
using FnaGame = Microsoft.Xna.Framework.Game;

namespace Tml.Mobile.Android;

[Register("com/mantovani/tmlmobile/GameEntry")]
public sealed class GameEntry : Java.Lang.Object
{
    private static Activity? activity;
    private static TouchControlsView? overlay;
    private static Tml.Mobile.TouchRouter? nativeRouter;
    private static readonly object logLock = new();
    private static string logPath = "";
    private static bool resolverInstalled;
    private static int active;

    [Export("attach")]
    public static void Attach(Activity owner)
    {
        activity = owner;
        logPath = Path.Combine(owner.FilesDir!.AbsolutePath, "host-session.log");
        File.WriteAllText(logPath, $"Session {DateTimeOffset.UtcNow:O}\n");
        HostDiagnostics.Start(owner);
        var metrics = new global::Android.Util.DisplayMetrics();
#pragma warning disable CS0618
        owner.WindowManager!.DefaultDisplay!.GetRealMetrics(metrics);
#pragma warning restore CS0618
        MobileGraphicsEXT.Enable(metrics.WidthPixels, metrics.HeightPixels);
        MobileGraphicsEXT.Suspend(false);
        Log($"ANDROID_RENDER_POLICY {MobileGraphicsEXT.Size.Width}x{MobileGraphicsEXT.Size.Height} landscape+fullscreen");
        nativeRouter = new Tml.Mobile.TouchRouter();
        Log("SDL_ACTIVITY_ATTACHED");
    }

    [Export("suspendInput")]
    public static void SuspendInput() { MobileGraphicsEXT.Suspend(true); overlay?.Suspend(); nativeRouter?.SetContext(Tml.Mobile.InputContext.Suspended); Log("SDL_INPUT_SUSPENDED"); }

    [Export("resumeInput")]
    public static void ResumeInput() { MobileGraphicsEXT.Suspend(false); overlay?.Resume(); nativeRouter?.SetContext(Tml.Mobile.InputContext.Menu); Log("SDL_INPUT_RESUMED"); }

    [Export("detach")]
    public static void Detach() { overlay = null; nativeRouter = null; activity = null; }

    [Export("runGame")]
    public static int RunGame()
    {
        if (Interlocked.Exchange(ref active, 1) != 0) return 1;
        try
        {
            Activity owner = activity ?? throw new InvalidOperationException("Activity unavailable");
            using var hostEntry = new EntryAssemblyScope(typeof(GameEntry).Assembly);
            Log("HOST_ENTRY_ASSEMBLY_READY " + Assembly.GetEntryAssembly()!.GetName().Name);
            Log($"MANAGED_SDL_ENTRY {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
            Environment.SetEnvironmentVariable("FNA_PLATFORM_BACKEND", "SDL3");
            Environment.SetEnvironmentVariable("FNA3D_FORCE_DRIVER", "OpenGL");
            Environment.SetEnvironmentVariable("SDL_TOUCH_MOUSE_EVENTS", "0");
            if (!resolverInstalled)
            {
                string native = owner.ApplicationInfo!.NativeLibraryDir!;
                NativeLibrary.SetDllImportResolver(typeof(FnaGame).Assembly, (name, assembly, search) =>
                {
                    string? file = name switch { "SDL3" => "libSDL3.so", "FNA3D" => "libFNA3D.so",
                        "FAudio" => "libFAudio.so", "libtheorafile" => "libtheorafile.so", _ => null };
                    return file == null ? IntPtr.Zero : NativeLibrary.Load(Path.Combine(native, file));
                });
                resolverInstalled = true;
            }
            MobileInputEXT.Source = nativeRouter ?? overlay!.Router;
            AndroidDetours.Initialize();
            using var performance = new SessionPerformance(owner.FilesDir!.AbsolutePath);
            using var glMonitor = new AndroidGraphicsProbe.FrameMonitor();
            if (owner.Intent?.GetStringExtra("mode") == "game") RunTerraria(owner.FilesDir!.AbsolutePath);
            else
            {
                NativeFileProbe.Run(owner);
                using var ordering = AndroidDetours.VerifyDeferredBootstrap(typeof(RenderProbe));
                nativeRouter!.Configure(new(0, 0, MobileGraphicsEXT.Size.Width, MobileGraphicsEXT.Size.Height), []);
                nativeRouter.SetContext(Tml.Mobile.InputContext.Menu);
                MobileInputEXT.NativeTouchEnabled = true;
                using var game = new RenderProbe();
                game.Run();
            }
            Log("MANAGED_SDL_EXIT");
            return 0;
        }
        catch (Exception error)
        {
            Log("HOST_ERROR " + error);
            ShowFailure(error);
            return 1;
        }
        finally
        {
            MobileInputEXT.NativeTouchEnabled = false;
            MobileInputEXT.Source = null;
            Interlocked.Exchange(ref active, 0);
        }
    }

    private static void RunTerraria(string files)
    {
        string payload = Path.Combine(files, "game");
        string vanilla = Path.Combine(files, "Terraria");
        if (!File.Exists(Path.Combine(vanilla, "Terraria.exe"))) throw new FileNotFoundException("Importe seu Terraria primeiro.");
        var issues = ContentPreflight.Validate(Path.Combine(vanilla, "Content"), Path.Combine(payload, "Content"));
        if (issues.Count > 0) throw new InvalidDataException(issues[0].Asset + ": " + issues[0].Reason);
        Directory.CreateDirectory(Path.Combine(payload, "Content/Images"));
        var lookup = Directory.EnumerateFiles(Path.Combine(payload, "Libraries"), "*.dll", SearchOption.AllDirectories)
            .Where(p => !p.Contains("/runtimes/") && !p.EndsWith(".resources.dll") && !p.Contains("/Native/"))
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);
        Assembly? Resolve(AssemblyLoadContext context, AssemblyName name) =>
            lookup.TryGetValue(name.Name!, out string? path) ? context.LoadFromAssemblyPath(path) : null;
        AssemblyLoadContext.Default.Resolving += Resolve;
        string previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = payload;
            Environment.SetEnvironmentVariable("TML_ANDROID_TERRARIA", vanilla);
            Environment.SetEnvironmentVariable("TML_ANDROID_NATIVE_DIR", activity!.ApplicationInfo!.NativeLibraryDir!);
            Log("TML_ENTRY_BEGIN");
            Log("TML_ASSEMBLY_LOAD_BEGIN");
            Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(payload, "tModLoader.dll"));
            Log("TML_ASSEMBLY_LOADED");
            using var gameEntry = new EntryAssemblyScope(game);
            Log("TML_ENTRY_ASSEMBLY_READY " + Assembly.GetEntryAssembly()!.GetName().Name);
            Log("TML_ASSEMBLY_REDIRECTS_INIT_BEGIN");
            Type redirects = game.GetType("Terraria.ModLoader.Core.AssemblyManager+AssemblyRedirects", true)!;
            RuntimeHelpers.RunClassConstructor(redirects.TypeHandle);
            Log("TML_ASSEMBLY_REDIRECTS_READY");
            AndroidGameHooks? androidHooks = null;
            IDisposable? integration = null;
            using var ready = new GameRunScope(game.GetType("Terraria.Main", true)!, () =>
            {
                var savePath = game.GetType("Terraria.Program", true)!.GetField("SavePath")!.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(savePath))
                    throw new InvalidOperationException("Game.Run reached before Terraria prepared its save paths");
                Log("TML_GAME_RUN_READY save-path-initialized");
                androidHooks = new AndroidGameHooks(game, activity!);
                Assembly bridge = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(payload, "Tml.GameIntegration.dll"));
                var initialize = bridge.GetType("Tml.GameIntegration.MobileGameIntegration", true)!.GetMethod("Initialize")!;
                using var memory = new ActivityManager.MemoryInfo();
                ((ActivityManager)activity!.GetSystemService(Activity.ActivityService)!).GetMemoryInfo(memory);
                Log($"ANDROID_SYSTEM_MEMORY installed={memory.TotalMem / 1048576}MiB available={memory.AvailMem / 1048576}MiB");
                integration = (IDisposable)initialize.Invoke(null, [nativeRouter!, files, (Action<string>)Log, memory.TotalMem])!;
            });
            Type program = game.GetType("Terraria.Program", true)!;
            Log("TML_PROGRAM_TYPE_READY");
            MethodInfo entry = program.GetMethod("LaunchGame", [typeof(string[]), typeof(bool)])
                ?? throw new MissingMethodException("Terraria.Program.LaunchGame(string[], bool)");
            Log("TML_LAUNCH_INVOKE");
            try
            {
                Log("TML_GAME_HOOKS_DEFERRED until-game-run");
                entry.Invoke(null,
                    [new[] { "-savedirectory", Path.Combine(files, "Saves"), "-tmlsavedirectory", Path.Combine(files, "Saves/tModLoader"),
                        "-nosteam" }, false]);
            }
            finally { integration?.Dispose(); androidHooks?.Dispose(); }
            Log("TML_LAUNCH_RETURNED");
        }
        finally { Environment.CurrentDirectory = previous; AssemblyLoadContext.Default.Resolving -= Resolve; }
    }

    internal static void Log(string message)
    {
        global::Android.Util.Log.Info("TmlHost", message);
        lock (logLock) if (logPath.Length > 0) File.AppendAllText(logPath, message + "\n");
    }

    private static void ShowFailure(Exception error)
    {
        // Only a managed failure can return here. Native process deaths are read
        // through Android's exit history when the launcher opens again.
        Activity? owner = activity;
        owner?.RunOnUiThread(() => {
            if (owner.IsFinishing || owner.IsDestroyed) return;
            new AlertDialog.Builder(owner).SetTitle("Falha ao iniciar")!
                .SetMessage(error.GetBaseException().Message + "\n\nO relatório foi salvo. Volte e abra Ver último relatório.")!
                .SetPositiveButton("Voltar", (_, _) => owner.Finish())!.Show();
        });
    }

    private sealed class RenderProbe : FnaGame
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch? batch;
        private Texture2D? pixel;
        private bool firstFrame = true;
        private bool touched;
        public RenderProbe()
        {
            graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720,
                IsFullScreen = true, GraphicsProfile = GraphicsProfile.HiDef };
            IsFixedTimeStep = true;
        }
        protected override void LoadContent()
        {
            int resets = 0;
            graphics.DeviceReset += (_, _) => resets++;
            foreach (var request in new[] { (800, 1200, false), (1920, 864, true), (2400, 1080, false) })
            {
                graphics.PreferredBackBufferWidth = request.Item1;
                graphics.PreferredBackBufferHeight = request.Item2;
                graphics.IsFullScreen = request.Item3;
                graphics.ApplyChanges();
            }
            var p = GraphicsDevice.PresentationParameters;
            if (resets != 0 || p.BackBufferWidth != MobileGraphicsEXT.Size.Width || p.BackBufferHeight != MobileGraphicsEXT.Size.Height || !p.IsFullScreen)
                throw new InvalidOperationException("Android graphics resize/fullscreen policy was not stable");
            Log("ANDROID_DISPLAY_STABLE portrait+resize+fullscreen no-device-reset");
            for (int index = 0; index < 120; index++)
            {
                // Match the pinned game's minimum-size clamp, then require it
                // to agree with the real FNA backbuffer and manager settings.
                int pcWidth = Math.Max(800, MobileGraphicsEXT.Size.Width);
                int pcHeight = Math.Max(720, MobileGraphicsEXT.Size.Height);
                graphics.PreferredBackBufferWidth = pcWidth;
                graphics.PreferredBackBufferHeight = pcHeight;
                graphics.ApplyChanges();
                if (p.BackBufferWidth != pcWidth || p.BackBufferHeight != pcHeight ||
                    graphics.PreferredBackBufferWidth != pcWidth || graphics.PreferredBackBufferHeight != pcHeight || resets != 0)
                    throw new InvalidOperationException("PC minimum-size clamp conflicts with the FNA render policy");
            }
            Log("ANDROID_PC_RESOLUTION_STABLE iterations=120 no-device-reset");
            AndroidGraphicsProbe.Run(GraphicsDevice);
            batch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            Log("FNA_GRAPHICS_READY " + GraphicsDevice.Adapter.Description);
        }
        protected override void Update(GameTime time)
        {
            if (!touched && MobileInputEXT.Frame.PointerPressed) { touched = true; Log("ANDROID_NATIVE_TOUCH_EVENT sdl-to-game-thread"); }
            if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
            base.Update(time);
        }
        protected override void Draw(GameTime time)
        {
            GraphicsDevice.Clear(new Color(12, 24, 38));
            var viewport = GraphicsDevice.Viewport;
            batch!.Begin();
            batch.Draw(pixel!, new Microsoft.Xna.Framework.Rectangle(viewport.Width / 2 - 120, viewport.Height / 2 - 60, 240, 120), Color.SeaGreen);
            var mouse = Mouse.GetState();
            batch.Draw(pixel!, new Microsoft.Xna.Framework.Rectangle(mouse.X - 5, mouse.Y - 5, 10, 10), Color.Orange);
            batch.End();
            if (firstFrame) { firstFrame = false; Log("SDL_FNA_FIRST_FRAME"); }
            base.Draw(time);
        }
        protected override void UnloadContent() { batch?.Dispose(); pixel?.Dispose(); base.UnloadContent(); }
    }
}
