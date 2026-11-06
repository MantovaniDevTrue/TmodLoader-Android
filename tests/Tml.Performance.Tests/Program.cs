using System.Diagnostics;
using System.Text.Json;
using Tml.Mobile;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
long Ticks(double ms) => (long)Math.Round(ms * Stopwatch.Frequency / 1000);
bool Near(double actual, double expected) => Math.Abs(actual - expected) < .0001;
FrameTimingSample Sample(int ms, long stamp = 0, int passes = 1, long allocations = 0) =>
    new(Ticks(ms), Ticks(2), Ticks(3), Ticks(1), passes, stamp, allocations, 0, 0, 0);

var ring = new FrameTimingWindow(3);
for (int i = 1; i <= 5; i++) ring.Record(Sample(i + 10, 100 + Ticks(i * 20)));
var snapshot = ring.Snapshot();
Check(snapshot.TotalRecorded == 5 && snapshot.Samples.Length == 3, "bounded window overwrites old samples without growing");
Check(Near(snapshot.Samples[0].TickTicks * 1000d / Stopwatch.Frequency, 13) &&
    Near(snapshot.Tick.MaximumMilliseconds, 15), "wrapped samples remain chronological");
snapshot.Samples[0] = default;
Check(ring.Snapshot().Samples[0].TickTicks > 0, "report readers cannot mutate live samples");

var percentiles = new FrameTimingWindow(100);
for (int i = 1; i <= 100; i++) percentiles.Record(Sample(i));
var stats = percentiles.Snapshot();
Check(Near(stats.Tick.MeanMilliseconds, 50.5) && Near(stats.Tick.P50Milliseconds, 50), "timing mean and nearest-rank median are correct");
Check(Near(stats.Tick.P95Milliseconds, 95) && Near(stats.Tick.P99Milliseconds, 99), "slow-frame percentiles are not replaced by average FPS");

var pacing = new FrameTimingWindow();
pacing.Record(Sample(10, 100, passes: 3, allocations: 10) with { Gen0Collections = 1 });
pacing.Record(Sample(10, 0, passes: 2, allocations: 20));
pacing.Record(Sample(10, 100 + Ticks(40), passes: 4, allocations: 30));
stats = pacing.Snapshot();
Check(stats.PresentedFrames == 2 && stats.UpdatePasses == 9, "catch-up updates and suppressed drawing do not inflate presentation count");
Check(Near(stats.PresentationCallsPerSecond, 25) && Near(stats.PresentationInterval.MeanMilliseconds, 40),
    "presentation pacing includes time outside the measured update/draw sections");
Check(stats.AllocatedBytes == 60 && stats.Gen0Collections == 1, "allocation and collection totals include non-presented ticks");
Check(Near(stats.Update.MeanMilliseconds, 2) && Near(stats.Draw.MeanMilliseconds, 3) &&
    Near(stats.Present.MeanMilliseconds, 1), "CPU phases are reported separately");
var json = JsonSerializer.Serialize(stats);
Check(json.Contains("PresentationCallsPerSecond") && json.Contains("AllocatedBytes") &&
    stats.TimestampFrequency == Stopwatch.Frequency, "exports include raw timestamp units without serializing live locks or ring state");

bool rejected = false;
try { pacing.Record(Sample(10, 1)); } catch (ArgumentException) { rejected = true; }
Check(rejected && pacing.Snapshot().TotalRecorded == 3, "non-monotonic sessions are rejected without corrupting the window");
pacing.Reset();
stats = pacing.Snapshot();
Check(stats.Samples.Length == 0 && stats.TotalRecorded == 0 && stats.PresentationCallsPerSecond == 0,
    "new sessions and resume can reset pacing history");
Check(double.IsFinite(stats.Tick.MeanMilliseconds) && stats.PresentationInterval.MaximumMilliseconds == 0,
    "empty capture produces finite zero statistics");
rejected = false;
try { pacing.Record(Sample(10) with { AllocatedBytes = -1 }); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "invalid counters cannot enter exported statistics");

var allocationWindow = new FrameTimingWindow();
var value = Sample(16);
void RecordBatch() { for (int i = 0; i < 10000; i++) allocationWindow.Record(value); }
RecordBatch();
long before = GC.GetAllocatedBytesForCurrentThread();
RecordBatch();
long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Check(allocated == 0, "10,000 warmed recording operations allocate zero managed bytes");

var concurrent = new FrameTimingWindow(64);
var writer = Task.Run(() => { for (int i = 1; i <= 1000; i++) concurrent.Record(Sample(16, i)); });
bool ordered = true;
while (!writer.IsCompleted)
{
    var report = concurrent.Snapshot();
    for (int i = 1; i < report.Samples.Length; i++)
        ordered &= report.Samples[i].PresentationTimestamp >= report.Samples[i-1].PresentationTimestamp;
}
await writer;
Check(ordered && concurrent.Snapshot().TotalRecorded == 1000, "reporting alongside recording preserves coherent chronological snapshots");
Console.WriteLine($"{passed} performance collector checks passed. Synthetic timings are not game benchmarks.");
