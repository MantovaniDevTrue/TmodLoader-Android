using Microsoft.Xna.Framework;
using Tml.Mobile;
using System.Globalization;
using Microsoft.Xna.Framework.Content;
using AndroidDebug = global::Android.OS.Debug;

namespace Tml.Mobile.Android;

internal sealed class SessionPerformance : IDisposable
{
    private readonly FrameTimingWindow[] windows = Enumerable.Range(0,6).Select(_ => new FrameTimingWindow(600)).ToArray();
    private readonly Timer timer;
    private readonly string path;
    private readonly Queue<string>[] recent = Enumerable.Range(0,6).Select(_ => new Queue<string>()).ToArray();
    private readonly long[] lastStamp = new long[6];
    private int collecting;
    private static bool gcDetails = true;
    internal SessionPerformance(string root)
    {
        path = Path.Combine(root, "frame-performance.log");
        File.WriteAllText(path, "Frame timings (presentation calls, not measured GPU scanout)\n");
        MobilePerformanceEXT.Scene = CaptureScene.Startup;
        MobilePerformanceEXT.SceneDestinations = windows;
        MobilePerformanceEXT.Destination = windows[0];
        timer = new Timer(_ => Save(), null, 5000, 5000);
    }
    private void Save()
    {
        if (Interlocked.Exchange(ref collecting, 1) != 0) return;
        try
        {
            int scene = (int)MobilePerformanceEXT.Scene;
            var s = windows[scene].Snapshot();
            if (s.PresentedFrames < 2 || s.Samples[^1].PresentationTimestamp == lastStamp[scene]) return;
            lastStamp[scene] = s.Samples[^1].PresentationTimestamp;
            string text = string.Create(CultureInfo.InvariantCulture,
                $"{DateTimeOffset.UtcNow:O} scene={(CaptureScene)scene} render={MobileGraphicsEXT.Size.Width}x{MobileGraphicsEXT.Size.Height} presents/s={s.PresentationCallsPerSecond:F1} frame-p95={s.PresentationInterval.P95Milliseconds:F2}ms update-p95={s.Update.P95Milliseconds:F2}ms draw-p95={s.Draw.P95Milliseconds:F2}ms present-p95={s.Present.P95Milliseconds:F2}ms alloc={s.AllocatedBytes}B gc={s.Gen0Collections}/{s.Gen1Collections}/{s.Gen2Collections} managed={GC.GetTotalMemory(false)/1048576}MiB alloc/frame={s.AllocatedBytes/Math.Max(1,s.Samples.Length)}B window={s.Samples.Length}ticks native-heap={AndroidDebug.NativeHeapAllocatedSize/1048576}MiB {ReadMemory()} lzx-new={MobileContentEXT.LzxDecoderCreations} lzx-reuse={MobileContentEXT.LzxDecoderReuses} resets-avoided={MobileGraphicsEXT.ResetsAvoided}");
            lock (recent) { recent[scene].Enqueue(text); while (recent[scene].Count > 12) recent[scene].Dequeue(); File.WriteAllLines(path, recent.SelectMany(q => q).OrderBy(line => line, StringComparer.Ordinal)); }
        }
        catch (Exception e) { GameEntry.Log("PERFORMANCE_REPORT_ERROR " + e.Message); }
        finally { Volatile.Write(ref collecting, 0); }
    }
    private static string ReadMemory()
    {
        using var info = new AndroidDebug.MemoryInfo();
        AndroidDebug.GetMemoryInfo(info);
        long Stat(string name) => long.TryParse(info.GetMemoryStat("summary." + name), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value / 1024 : -1;
        long heap = -1, committed = -1;
        if (gcDetails)
        {
            try { var gc = GC.GetGCMemoryInfo(); heap = gc.HeapSizeBytes / 1048576; committed = gc.TotalCommittedBytes / 1048576; }
            catch (PlatformNotSupportedException) { gcDetails = false; }
        }
        return string.Create(CultureInfo.InvariantCulture, $"pss={info.TotalPss/1024}MiB pss-java={Stat("java-heap")}MiB pss-native={Stat("native-heap")}MiB pss-graphics={Stat("graphics")}MiB pss-code={Stat("code")}MiB pss-other={Stat("private-other")}MiB pss-system={Stat("system")}MiB gc-last-heap={heap}MiB gc-committed={committed}MiB");
    }
    public void Dispose() { timer.Dispose(); Save(); MobilePerformanceEXT.Destination = null; MobilePerformanceEXT.SceneDestinations = null; }
}
