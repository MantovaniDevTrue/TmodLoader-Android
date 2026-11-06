using System.Diagnostics;

namespace Tml.Mobile;

public readonly record struct FrameTimingSample(
    long TickTicks, long UpdateTicks, long DrawTicks, long PresentTicks,
    int UpdatePasses, long PresentationTimestamp, long AllocatedBytes,
    int Gen0Collections, int Gen1Collections, int Gen2Collections);

public sealed record TimingDistribution(
    double MeanMilliseconds, double P50Milliseconds, double P95Milliseconds,
    double P99Milliseconds, double MaximumMilliseconds);

public sealed record FrameTimingSnapshot(
    long TotalRecorded, long TimestampFrequency, FrameTimingSample[] Samples, TimingDistribution Tick,
    TimingDistribution Update, TimingDistribution Draw, TimingDistribution Present,
    TimingDistribution PresentationInterval, int PresentedFrames,
    double PresentationCallsPerSecond, long AllocatedBytes, long UpdatePasses,
    long Gen0Collections, long Gen1Collections, long Gen2Collections);

// Record on the game thread; take snapshots from a reporting thread at low frequency.
public sealed class FrameTimingWindow
{
    private readonly object gate = new();
    private readonly FrameTimingSample[] samples;
    private int next, count;
    private long total, lastPresentation;

    public FrameTimingWindow(int capacity = 600)
    {
        if (capacity is < 2 or > 36000) throw new ArgumentOutOfRangeException(nameof(capacity));
        samples = new FrameTimingSample[capacity];
    }

    public void Record(in FrameTimingSample sample)
    {
        if (sample.TickTicks <= 0 || sample.UpdateTicks < 0 || sample.DrawTicks < 0 ||
            sample.PresentTicks < 0 || sample.UpdatePasses < 0 || sample.PresentationTimestamp < 0 ||
            sample.AllocatedBytes < 0 || sample.Gen0Collections < 0 || sample.Gen1Collections < 0 ||
            sample.Gen2Collections < 0)
            throw new ArgumentOutOfRangeException(nameof(sample));
        lock (gate)
        {
            if (sample.PresentationTimestamp != 0 && sample.PresentationTimestamp < lastPresentation)
                throw new ArgumentException("Presentation timestamps must be monotonic; reset on a new session.", nameof(sample));
            if (sample.PresentationTimestamp != 0) lastPresentation = sample.PresentationTimestamp;
            samples[next] = sample;
            next = (next + 1) % samples.Length;
            count = Math.Min(count + 1, samples.Length);
            total++;
        }
    }

    public void Reset()
    {
        lock (gate) { next = count = 0; total = lastPresentation = 0; }
    }

    public FrameTimingSnapshot Snapshot()
    {
        FrameTimingSample[] copy;
        long recorded;
        lock (gate)
        {
            copy = new FrameTimingSample[count];
            int start = (next - count + samples.Length) % samples.Length;
            int first = Math.Min(count, samples.Length - start);
            Array.Copy(samples, start, copy, 0, first);
            Array.Copy(samples, 0, copy, first, count - first);
            recorded = total;
        }
        // Sorting/report allocations happen after releasing the writer's lock.
        var tick = new long[copy.Length]; var update = new long[copy.Length];
        var draw = new long[copy.Length]; var present = new long[copy.Length];
        var intervals = new List<long>();
        long allocations = 0, updates = 0, gen0 = 0, gen1 = 0, gen2 = 0;
        long firstStamp = 0, previousStamp = 0, finalStamp = 0;
        int presented = 0;
        for (int i = 0; i < copy.Length; i++)
        {
            var sample = copy[i];
            tick[i] = sample.TickTicks; update[i] = sample.UpdateTicks;
            draw[i] = sample.DrawTicks; present[i] = sample.PresentTicks;
            allocations += sample.AllocatedBytes; updates += sample.UpdatePasses;
            gen0 += sample.Gen0Collections; gen1 += sample.Gen1Collections; gen2 += sample.Gen2Collections;
            long stamp = sample.PresentationTimestamp;
            if (stamp == 0) continue;
            presented++;
            if (firstStamp == 0) firstStamp = stamp;
            if (previousStamp != 0) intervals.Add(stamp - previousStamp);
            previousStamp = finalStamp = stamp;
        }
        double rate = presented >= 2 && finalStamp > firstStamp
            ? (presented - 1) * (double)Stopwatch.Frequency / (finalStamp - firstStamp) : 0;
        return new(recorded, Stopwatch.Frequency, copy, Describe(tick), Describe(update), Describe(draw), Describe(present),
            Describe(intervals.ToArray()), presented, rate, allocations, updates, gen0, gen1, gen2);
    }

    private static TimingDistribution Describe(long[] values)
    {
        if (values.Length == 0) return new(0, 0, 0, 0, 0);
        Array.Sort(values);
        double sum = 0;
        foreach (long value in values) sum += value;
        double milliseconds = 1000d / Stopwatch.Frequency;
        double Percentile(double p) => values[Math.Max(0, (int)Math.Ceiling(values.Length * p) - 1)] * milliseconds;
        return new(sum / values.Length * milliseconds, Percentile(.50), Percentile(.95),
            Percentile(.99), values[^1] * milliseconds);
    }
}
