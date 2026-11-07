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
check allocation/frame, GC frequency, draw p95 and visible lighting/effects. Include
the first sword mod only after a no-mod baseline. Verify saving and re-entry.

The startup gap after music remains unresolved. Vulkan remains experimental and
failed on the physical Adreno phone in 0.2.14; its SIGKILL has no proven cause.
