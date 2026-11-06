# Dedicated tModLoader Android port

## Product and architecture

Maintain a Terraria-specific Android variant of tModLoader, preserving the original C# gameplay/mod API while adapting its platform layer and interface. Desktop mod compatibility is a goal for the selected upstream version, not a promise that every `.tmod` from every release will work.

- .NET 10 / ARM64, with game, loader and mod assemblies sharing the managed runtime and actual game objects.
- Preserve JIT and dynamic assembly loading. Keep the validated dynamic JNI callback registration for the Android Activity/View boundary.
- SDL/FNA own the game surface and graphics lifecycle. Android-specific startup, file access, lifecycle and touch integration belong to the platform layer.
- Adapt in-game menus/inventory/crafting using game UI and input APIs. Native Android views handle the surrounding application and system file/text interactions where appropriate.
- Preserve gameplay, networking and mod hook semantics while optimizing. Changes to simulation rates, AI, damage or authoritative updates require compatibility analysis; they are not default performance shortcuts.
- Prefer a working native Android graphics backend; measure before adding another renderer or moving code across a native-language boundary. Rust/C++ is optional for a demonstrated native requirement, not the gameplay/API rewrite strategy.

## Delivery gates

| Gate | Deliverable | Required evidence |
| --- | --- | --- |
| Platform foundation | .NET 10 ARM64, dynamic DLLs, JNI callbacks and touch router | 0.1.1 opened on the user's phone; emulator startup/touch/resume checks passed |
| Matching PC inputs | Vanilla Terraria PC 1.4.5.8 executable and Content received privately; tModLoader managed candidate now builds in .NET 10 | Archive CRC and basic XNB checks passed; see [PC_INPUT_VALIDATION.md](PC_INPUT_VALIDATION.md). Separate vanilla/platform content roots still required |
| Game host | Real SDL/FNA Android surface and game startup in the SDK-managed runtime | Reach menu without masking initialization exceptions; validate audio and graphics recovery |
| First playable baseline | Load a small supported mod, enter a world, save and reenter | Repeated device runs, correct hooks/assets, stable lifecycle and persistence |
| Mobile interface | Touch-oriented menus, inventory/crafting drag/drop, text, scrolling, zoom and mod hotkeys | Device interaction checks; hardware input coexistence and cutout/scale handling |
| Measured optimization | Identify and improve CPU/render/GC bottlenecks | Matched baseline comparison and sustained device runs with documented settings |

Profiling infrastructure is being prepared now so it is available at the first playable baseline. Gate order does not imply that optimization is postponed until all UI is complete.

## Performance acceptance

Target 60 FPS where the selected device/mod workload permits it: a 60 Hz frame interval is approximately 16.67 ms. This is a target, not a measured current result. Loading, world generation, graphics-driver waits and unusually heavy mods require separate analysis.

Compare the same game/mod versions, world, scene, resolution, quality settings and frame cap. Record device/OS, renderer, runtime, mod list, warm-up and capture duration. Repeat runs after thermal warm-up. Inspect presentation cadence, p95/p99 stalls, per-phase wall time, allocations and collections; average FPS alone is insufficient.

The new optional FNA timing hooks and bounded capture window are described in [PERFORMANCE.md](PERFORMANCE.md). They do not establish a performance advantage over any other launcher.

## Current boundary

The runnable Android app is still the controls/runtime diagnostic. It has not constructed the SDL game surface or launched Terraria. The dedicated-port decision does not change that status.

The matching vanilla Terraria PC 1.4.5.8 executable and Content tree are now available privately and passed archive/basic asset validation. They remain outside the repository. The private tModLoader managed candidate now builds from the selected upstream against mobile FNA and passes API/type loading checks; see [GAME_BUILD.md](GAME_BUILD.md). It still needs the SDL/FNA Android host and ARM64 native dependencies before the next game-startup gate. The legacy Mono 8 bootstrap remains historical reference; its internal metadata offsets must not be transplanted into Mono 10.
