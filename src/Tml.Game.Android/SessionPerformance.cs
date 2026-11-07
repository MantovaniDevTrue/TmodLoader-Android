using Microsoft.Xna.Framework;
using Tml.Mobile;
using System.Globalization;

namespace Tml.Mobile.Android;

internal sealed class SessionPerformance : IDisposable
{
    private readonly FrameTimingWindow window = new(600);
    private readonly Timer timer;
    private readonly string path;
    private readonly Queue<string> recent = new();
    private int collecting;
    internal SessionPerformance(string root)
    {
        path = Path.Combine(root, "frame-performance.log");
        File.WriteAllText(path, "Frame timings (presentation calls, not measured GPU scanout)\n");
        MobilePerformanceEXT.Destination = window;
        timer = new Timer(_ => Save(), null, 5000, 5000);
    }
    private void Save()
    {
        if (Interlocked.Exchange(ref collecting, 1) != 0) return;
        try
        {
            var s = window.Snapshot();
            if (s.PresentedFrames < 2) return;
            string text = string.Create(CultureInfo.InvariantCulture,
                $"{DateTimeOffset.UtcNow:O} render={MobileGraphicsEXT.Size.Width}x{MobileGraphicsEXT.Size.Height} presents/s={s.PresentationCallsPerSecond:F1} frame-p95={s.PresentationInterval.P95Milliseconds:F2}ms update-p95={s.Update.P95Milliseconds:F2}ms draw-p95={s.Draw.P95Milliseconds:F2}ms present-p95={s.Present.P95Milliseconds:F2}ms alloc={s.AllocatedBytes}B gc={s.Gen0Collections}/{s.Gen1Collections}/{s.Gen2Collections} managed={GC.GetTotalMemory(false)/1048576}MiB alloc/frame={s.AllocatedBytes/Math.Max(1,s.Samples.Length)}B window={s.Samples.Length}ticks native-heap={global::Android.OS.Debug.NativeHeapAllocatedSize/1048576}MiB pss={ReadPss()}MiB resets-avoided={MobileGraphicsEXT.ResetsAvoided}");
            lock (recent) { recent.Enqueue(text); while (recent.Count > 12) recent.Dequeue(); File.WriteAllLines(path, recent); }
        }
        catch (Exception e) { GameEntry.Log("PERFORMANCE_REPORT_ERROR " + e.Message); }
        finally { Volatile.Write(ref collecting, 0); }
    }
    private static long ReadPss()
    {
        using var info = new global::Android.OS.Debug.MemoryInfo();
        global::Android.OS.Debug.GetMemoryInfo(info);
        return info.TotalPss / 1024;
    }
    public void Dispose() { timer.Dispose(); Save(); MobilePerformanceEXT.Destination = null; }
}
