# Android 0.2.15 — tile rendering scratch reuse

The physical OpenGL baseline in 0.2.13/0.2.14 allocates roughly 360–380 KB per
recorded tick in a world. Draw p95 is approximately 20 ms with the inventory
closed and 22–24 ms with world UI. These are observations, not allocation traces.

Inspection of the supplied private game identified one allocation inside every
`TileDrawing.DrawSingleTile`: a `TileDrawInfo` object whose constructor also
allocates nine `Vector3` color samples. The local CoreCLR regression measures
248 bytes per call. The exact Android byte count can differ.

## Change

`TileDrawScratchPatch` replaces only that constructor site. The original tile
drawing method, texture selection, lighting, shaders, effects and mod callbacks
continue to run. The resolution remains 1600×720. This reduces managed work on the
OpenGL rendering path; it does not replace the OpenGL driver.

Reuse is enabled only while no external mods are loaded (the built-in ModLoader
entry is allowed). `TileLoader.DrawEffects` exposes the object by reference, so an
external mod could retain or replace it. Those sessions keep fresh instances and
their original lifetime. This includes AndroidSwordTest. A separate regression
checks that externally retained instances are neither reused nor reset later.

Scratch storage is per thread and per nested invocation, retaining at most eight
objects and their nine-element color arrays. Greater recursion allocates normally.
Every field and all nine color samples are reset to constructor defaults before
reuse. Texture references are cleared when the invocation exits. A scoped detour
balances depth in `finally`, including early returns and exceptions. The IL edit
requires exactly one matching allocation site and fails on a changed method body.
The existing metadata and input fixes remain installed.

## Verification

`tools/Tml.TileDrawProbe` uses the real private `TileDrawInfo` type and the same IL
rewrite and scope detour used by the game. After warming the entire measurement
path, 10,000 calls allocate zero bytes instead of 2,480,000 bytes. It also checks
all 19 field defaults against a fresh instance, nested isolation, bounded overflow,
scope guards and recovery after an exception through the full detour chain.
The headless private touch probe installs the hook on the actual game method and
continues to exercise the options editor and profile persistence.

Reproduce with the matching private game and prepared FNA:

```sh
dotnet build tools/Tml.TileDrawProbe -c Release -m:1 \
  -p:FnaAssembly=/private/fna/FNA.dll -p:GameDirectory=/private/game
dotnet tools/Tml.TileDrawProbe/bin/Release/net10.0/Tml.TileDrawProbe.dll /private/game
```

These checks do not establish an Android FPS gain or complete rendering fidelity.
Compare a physical OpenGL run in the same world with inventory closed, then open;
check allocation/frame, GC frequency, draw p95 and visible lighting/effects. Test
the sword mod separately for compatibility; it intentionally uses the original
allocation path. Verify saving and re-entry.

The startup gap after music remains unresolved. Vulkan remains experimental and
failed on the physical Adreno phone in 0.2.14; its SIGKILL has no proven cause.

## Candidate package

The signed private playable APK is `tML_Android_0.2.15_SDL3_NET10_ARM64.apk`,
51,738,221 bytes, SHA-256
`0dd4ba400c3cd3752f648a4154cffe5cd1b03d16d6d55c831fe3d818a2863a2c`.
Its embedded payload SHA-256 is
`ff24d7d2c9c6ff053cbd62d7031e6614f5c67767dd201443d2e1978b22891d6b`.
The integration comes from compatibility-guard commit `3b4950f`; the unchanged
host inputs come from `ed8abe3`, source-host artifact 13591846974 in workflow
42416890447. The native host does not embed the private integration assembly;
the verified private payload supplies it. Both commits use app version 0.2.15.

Local checks passed: 30 tile scratch assertions, 59 input/core assertions,
28 FNA adapter assertions, 16 performance recorder assertions, actual private
API contracts, touch editor/profile persistence and five-type mod metadata loading.
APK checks passed: version, exact payload/integration bytes, ZIP CRC/no duplicate
entries, signature with the existing development certificate, ARM64 native
libraries and 16 KiB alignment. Install over 0.2.14 without uninstalling.
Physical OpenGL gameplay with this optimization remains pending.

Host workflow 42416890447 passed its ARM64 build/package checks and emulator
OpenGL/SDL_GPU rendering fixtures, and foundation workflow 42416890511 passed
all three jobs. Emulator fixtures are not physical phone benchmarks and do not
supersede the failed Adreno Vulkan result. The guard only changes the separate
private integration; its full scoped-detour regression and private probes passed.
