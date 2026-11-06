using Android.App;
using Android.Runtime;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Tml.Mobile.Android;

internal static class HostDiagnostics
{
    private static bool installed;
    private static int firstChanceCount;
    [ThreadStatic] private static bool recording;

    [DllImport("tmlhost", EntryPoint = "tml_host_capture_output")]
    private static extern int CaptureNativeOutput([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    internal static void Start(Activity owner)
    {
        firstChanceCount = 0;
        string root = owner.FilesDir!.AbsolutePath;
        int capture = CaptureNativeOutput(Path.Combine(root, "native-console.log"));
        GameEntry.Log("NATIVE_CONSOLE_CAPTURE status=" + capture);
        Console.SetOut(new HostConsoleWriter("STDOUT"));
        Console.SetError(new HostConsoleWriter("STDERR"));
        if (installed) return;
        installed = true;
        AppDomain.CurrentDomain.FirstChanceException += FirstChance;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Record("MANAGED_UNHANDLED", e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => Record("TASK_UNOBSERVED", e.Exception);
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => Record("ANDROID_MANAGED_UNHANDLED", e.Exception);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Record("PROCESS_EXIT", Environment.ExitCode);
    }

    private static void FirstChance(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (recording || Volatile.Read(ref firstChanceCount) >= 64) return;
        if (Interlocked.Increment(ref firstChanceCount) <= 64)
            Record("STARTUP_EXCEPTION (may be handled)", e.Exception);
    }

    private static void Record(string kind, object? error)
    {
        if (recording) return;
        recording = true;
        try { GameEntry.Log(kind + " " + error); }
        catch { /* An error handler must not replace the original failure. */ }
        finally { recording = false; }
    }

    internal static string ReadReport(Activity owner)
    {
        string root = owner.FilesDir!.AbsolutePath;
        var report = new StringBuilder("tML Android " + owner.PackageManager!.GetPackageInfo(owner.PackageName!, 0)!.VersionName + "\n");
        AppendFile(report, Path.Combine(root, "host-session.log"), "Sessão", 20000);
        AppendFile(report, Path.Combine(root, "native-console.log"), "Saída nativa", 16000);
        string logs = Path.Combine(root, "game/tModLoader-Logs");
        if (Directory.Exists(logs))
        {
            string? latest = Directory.EnumerateFiles(logs, "client*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (latest != null) AppendFile(report, latest, "tModLoader", 20000);
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            try
            {
                var manager = (ActivityManager)owner.GetSystemService(Activity.ActivityService)!;
                bool nativeTraceRead = false;
                foreach (var exit in manager.GetHistoricalProcessExitReasons(owner.PackageName, 0, 4) ?? [])
                {
                    report.AppendLine($"\nAndroid: {DateTimeOffset.FromUnixTimeMilliseconds(exit.Timestamp):O}");
                    report.AppendLine($"process={exit.ProcessName} reason={exit.Reason} status={exit.Status}");
                    report.AppendLine($"{exit.Description} RSS={exit.Rss}KiB PSS={exit.Pss}KiB");
                    if (!nativeTraceRead && OperatingSystem.IsAndroidVersionAtLeast(31) &&
                        exit.Reason == ApplicationExitInfo.ReasonCrashNative)
                    {
                        nativeTraceRead = true;
                        try
                        {
                            using var trace = exit.TraceInputStream;
                            if (trace != null) report.AppendLine(NativeCrashTrace.Read(trace));
                            else report.AppendLine("Android não disponibilizou o stack nativo.");
                        }
                        catch (Exception e) { report.AppendLine("Stack nativo indisponível: " + e.Message); }
                    }
                }
            }
            catch (Exception e) { report.AppendLine("Histórico indisponível: " + e.Message); }
        }
        return report.ToString();
    }

    private static void AppendFile(StringBuilder output, string path, string label, int limit)
    {
        if (!File.Exists(path)) return;
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        bool truncated = file.Length > limit;
        if (truncated) file.Seek(-limit, SeekOrigin.End);
        using var reader = new StreamReader(file, Encoding.UTF8);
        output.AppendLine("\n--- " + label + (truncated ? " (final)" : "") + " ---");
        output.AppendLine(reader.ReadToEnd());
    }

    private sealed class HostConsoleWriter(string label) : TextWriter
    {
        private readonly StringBuilder line = new();
        private readonly object gate = new();
        public override Encoding Encoding => Encoding.UTF8;
        public override void Write(char value)
        {
            lock (gate)
            {
                if (value == '\r') return;
                if (value != '\n') line.Append(value);
                if (value == '\n' || line.Length >= 4096) Flush();
            }
        }
        public override void Write(string? value)
        {
            if (value == null) return;
            lock (gate) foreach (char c in value) Write(c);
        }
        public override void WriteLine(string? value) { lock (gate) { Write(value); Write('\n'); } }
        public override void Flush()
        {
            lock (gate)
            {
                if (line.Length == 0) return;
                Record(label, line.ToString());
                line.Clear();
            }
        }
    }
}
