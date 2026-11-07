using Android.App;
using Android.Runtime;
using Android.Views;
using Java.Interop;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Reflection;
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
        overlay = new TouchControlsView(owner) { ConsumeFrames = false, ShowDiagnostics = false };
        overlay.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
        owner.AddContentView(overlay, new ViewGroup.LayoutParams(-1, -1));
        Log("SDL_ACTIVITY_ATTACHED");
    }

    [Export("suspendInput")]
    public static void SuspendInput() { overlay?.Suspend(); Log("SDL_INPUT_SUSPENDED"); }

    [Export("resumeInput")]
    public static void ResumeInput() { overlay?.Resume(); Log("SDL_INPUT_RESUMED"); }

    [Export("detach")]
    public static void Detach() { overlay = null; activity = null; }

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
            MobileInputEXT.Source = overlay!.Router;
            if (owner.Intent?.GetStringExtra("mode") == "game") RunTerraria(owner.FilesDir!.AbsolutePath);
            else { using var game = new RenderProbe(); game.Run(); }
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
            Type program = game.GetType("Terraria.Program", true)!;
            Log("TML_PROGRAM_TYPE_READY");
            MethodInfo entry = program.GetMethod("LaunchGame", [typeof(string[]), typeof(bool)])
                ?? throw new MissingMethodException("Terraria.Program.LaunchGame(string[], bool)");
            Log("TML_LAUNCH_INVOKE");
            entry.Invoke(null,
                [new[] { "-savedirectory", Path.Combine(files, "Saves"), "-tmlsavedirectory", Path.Combine(files, "Saves/tModLoader"),
                    "-nosteam" }, false]);
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
        public RenderProbe()
        {
            graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1280, PreferredBackBufferHeight = 720,
                IsFullScreen = true, GraphicsProfile = GraphicsProfile.HiDef };
            IsFixedTimeStep = true;
        }
        protected override void LoadContent()
        {
            batch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            Log("FNA_GRAPHICS_READY " + GraphicsDevice.Adapter.Description);
        }
        protected override void Update(GameTime time)
        {
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
