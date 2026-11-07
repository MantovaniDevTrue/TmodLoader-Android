# Native touch controls and mobile runtime integration

The mobile integration is compiled against the exact privately built .NET 10 game DLL and the prepared mobile FNA. Its sources are public; the game DLL and compiled private payload remain outside Git. It is loaded after Terraria creates Main and initializes save paths, before FNA starts the game. This is host-owned Android functionality, not a user-installed mod.

## In-world controls

Open the on-screen **Menu** button, then **Controles touch** in the settings window header. The editor lets the player drag controls, select hidden controls with **Próximo botão**, resize or hide a button, change opacity, toggle firing with the aim stick, mirror the layout and restore defaults. **Salvar / voltar** writes `Saves/touch-controls.json` atomically. Invalid profiles are rejected and logged; positions are clamped inside the viewport after resolution changes. Inventory mode retains only inventory/menu buttons so gameplay controls do not block item taps.

SDL finger events feed the shared bounded pointer router directly on the game thread. The Android Canvas overlay is removed from the SDL host, including its continuous UI-thread redraw. The controller draws within Terraria's OnPostDraw using FNA, the game font and vanilla item icons. It adds semantic actions to the actual global TriggersPack before Update computes edge transitions, preserving physical inputs and mod trigger processing. Synthetic keyboard controls are disabled in this mode. The aim origin follows the transformed local-player position, and player direction follows horizontal aim before item processing. Backgrounding cancels captures; resuming restores the appropriate context.

These are a new mobile control implementation and editor. They are not imported proprietary mobile Terraria controls and do not yet reproduce every mobile inventory gesture or platform keyboard behavior.

## Frame pacing and GC

The exact supplied game's EndDraw contains three calls to NewRuntimeMethods.GC_Collect, including generation-2 collections during nominal frame slack. The 0.2.11 phone report shows hundreds of generation-2 collections and roughly 120ms presentation-section p95 while leaving a world. The presentation section includes Main.EndDraw and is not isolated GPU swap timing.

FrameGcPatch rewrites only those three verified call sites. Automatic runtime GC and explicit collections outside EndDraw remain available. When the old frame-slack branch has more than 2ms left within its normal budget, it sleeps for the whole-millisecond portion instead of filling it with GC or spinning through the whole interval. The original timing accumulator and frame-skip control flow remain in place. The patch rejects an unexpected site count.

The isolated IL fixture executes 600 rewritten frames, checks all 1,800 sites were intercepted, and confirms that a call outside the frame remains available. This does not establish phone FPS or Mono/ARM64 ILHook behavior for the full game.

## Loading and memory

Compressed XNB input/output buffers now use an ArrayPool with at most two retained arrays per bucket up to 1MiB (less than 4MiB total retained payload capacity). Larger rentals are not retained. Each stream keeps its exact logical length and exposes its buffer to the existing zero-copy texture reader. Input reads use ReadExactly; readers own decompressed streams and return buffers on disposal, including decompression failures. Asset decoding, textures, shaders, effects and the 1600×720 policy remain enabled.

Main.LoadContent and WorldFile.SaveWorld log elapsed time. Save format, validation, backups and mod save calls remain unchanged. The public build script builds and stages Tml.GameIntegration.dll before packaging. The launcher payload marker advances so existing installs receive the module without deleting saves or imported content.

The game memory UI now receives Android ActivityManager's actual installed physical memory instead of interpreting GC.GetGCMemoryInfo().TotalAvailableMemoryBytes as installed RAM. This corrects a diagnostic value; it does not reduce resident usage. Process PSS, managed bytes and native heap remain separately reported.

## Audio shutdown

Pinned FAudio FACTCue_Stop returns early for a stopped cue. FACTCue_Destroy previously freed that cue without unconditionally releasing remaining prepared simpleWave/playingSound resources. Later wave-bank destruction could encounter the freed parent cue. The patch destroys any remaining associated resources after Stop and before removing/freeing the cue. Normal cleanup clears those pointers, so the additional cleanup does not double-destroy them.

The C lifecycle fixture compiles the exact patched destruction body with isolated dependencies under AddressSanitizer/UBSan, checks stopped/prepared and active cases, and verifies exactly-once release. Removing the patch reproduces an unreleased resource in the stopped case. The fixture does not decode the user's wave bank or play audio. Actual Android audio shutdown remains a phone validation boundary.

## Verification and device checks

Local checks cover profile persistence/bounds/invalid writes, SDL tap retention, pooled stream reuse and live-reader separation, IL frame-GC policy, the C audio lifecycle and the private integration's hooked API contracts. The public emulator workflow additionally injects an Android touch into the SDL surface and requires it to reach the game thread before checking pixels. It contains no private game bytes.

On the phone, test menu selection, world entry, moving/aiming/jumping simultaneously, inventory taps, the in-world layout editor, save/re-entry, background/resume and complete exit. Collect the performance report and content/save phase markers. Startup/save duration, overall memory reduction, sustained gameplay FPS and the actual native shutdown fix are unmeasured until that run.
