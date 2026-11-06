# Optional FNA performance capture

The mobile FNA integration now includes timing hooks in the actual pinned `Game.Tick()` implementation. Capture is disabled by default. No Terraria performance measurement has been made with these hooks yet.

## Measurements

| Field | Meaning |
| --- | --- |
| Tick | Wall time inside a completed FNA tick, including its intentional frame pacing |
| Update | Sum of all update passes in the tick, including mobile input sampling |
| Draw | Wall time in the game's Draw call |
| Present | Wall time in EndDraw, which can include driver/presentation waits |
| PresentationInterval | Time between successive completed EndDraw calls in the captured window |
| PresentationCallsPerSecond | Rate derived from those EndDraw timestamps, rather than update count |
| AllocatedBytes | Allocation counter delta on the game thread during a completed tick |
| Gen0/1/2Collections | Process-wide collection count deltas during those ticks |

These are CPU-side wall-clock measurements. EndDraw timestamps are not hardware display timestamps or GPU timer queries. Background-thread allocations are not included in AllocatedBytes; GC counts are not attribution to an individual mod. The interval calculation includes time spent between ticks in the Android host. Suppressed drawing is recorded as a tick with no presentation timestamp, and fixed-timestep catch-up passes do not inflate presentation counts.

## Use from the game thread

```csharp
var capture = new Tml.Mobile.FrameTimingWindow(capacity: 600);
Microsoft.Xna.Framework.MobilePerformanceEXT.Destination = capture;

// On pause/resume, world changes or a new comparison session:
Microsoft.Xna.Framework.MobilePerformanceEXT.Destination = null;
capture.Reset();
Microsoft.Xna.Framework.MobilePerformanceEXT.Destination = capture;

// Disable when the capture is finished:
Microsoft.Xna.Framework.MobilePerformanceEXT.Destination = null;
```

With no destination, hooks avoid clock and GC counter sampling. Enabled capture still has CPU overhead even when recording allocates no managed bytes; use the same capture settings in comparisons. Hooks do not catch or change exceptions from Update/Draw/EndDraw; a failed tick is not silently converted into a successful timing sample. Configure capture on the game thread; snapshots may be read from a reporting thread.

## Reports

```csharp
// Reporting thread, at low frequency and outside gameplay timing:
var snapshot = capture.Snapshot();
System.Text.Json.JsonSerializer.Serialize(reportStream, snapshot);
```

The ring is bounded to 600 samples by default (approximately ten seconds at 60 ticks/second). Record operations are allocation-free after warm-up in the desktop test. Snapshotting copies a bounded window under a lock, then sorts/computes mean, nearest-rank p50/p95/p99 and maximum after releasing that lock. Reporting allocates memory and must not run every frame.

Raw timing values use the exported `TimestampFrequency` ticks per second; precomputed distributions are already in milliseconds.

A snapshot describes only the retained window. `TotalRecorded - Samples.Length` identifies the first retained sample index for a session. Do not label the last ten seconds as whole-session statistics or average overlapping snapshots into a full-run p95. Long captures need a later reporting/streaming layer outside the update thread. Reset before a new session so pause gaps are not reported as gameplay stalls.

## Validation limits

Tests cover chronological wraparound, bounded memory, report isolation, percentiles, suppressed presentation, update aggregation, clock monotonicity, empty captures, JSON serialization, concurrent reporting and steady-state record allocations. Adapter checks run against the compiled actual FNA assembly. Compilation and adapter tests do not exercise a real Terraria update/render loop or measure Android overhead.

The next verification is a real game run, first without mods and then with one small supported mod, before comparing heavier workloads on the Xiaomi Mi 11 Lite 5G NE.

## Source milestone validation

Local checks passed: 16 collector/report checks, 17 actual-FNA input/timing-adapter checks and 22 shared input/runtime checks (55 total). The patched pinned FNA compiled successfully. Steady-state record and enabled-adapter allocation samples reported zero managed bytes across 10,000 cycles; these are desktop synthetic checks, not Android game benchmarks.

[GitHub Actions run 42226566650](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42226566650) passed `fna`, `mobile` and `startup` for code commit `f91a3ada38304181351ff7f525cf69119d660a3f`. The startup job exercises the diagnostic app on an Android emulator; it still does not launch Terraria.
