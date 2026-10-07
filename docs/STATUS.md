# Current status

## Active baseline

**Phase 2.22.98 — Post-Texture LoadContent Isolation**

Target:

- Android ARM64
- MonoVM runtime 8.0.23
- tModLoader 1.4.5-era desktop assembly stack
- FNA/FNA3D graphics path
- controlled first-frame bootstrap

## Confirmed working

The current bootstrap has already crossed these startup stages:

- native APK bootstrap and MonoVM initialization;
- managed assembly preload pipeline;
- tModLoader entry path;
- dedicated 32 MiB pthread for the game bootstrap;
- `NPCID.Sets` static initialization;
- full `ContentSamples.Initialize()`:
  - 1506 NPC SetDefaults passes;
  - 1022 projectile passes;
  - 5461 item passes;
- graphics device creation;
- display/backbuffer setup;
- render target allocation;
- `FNA3D_CreateTexture2D` bridge;
- `FNA3D_SetTextureData2D` bridge;
- real 1×1 texture allocation and 4-byte pixel upload.

## Current investigation

The next failure occurs after the successful dummy texture upload performed at the beginning of `Terraria.Main.LoadContent`.

Phase 2.22.98 traces:

- `Main.LoadContent`;
- `AssetInitializer.CreateAssetServices`;
- `GameServiceContainer`;
- `AssetRepository` and asset readers;
- `SoundEngine.Initialize`;
- `LegacyAudioSystem`;
- `AssetSourceController`;
- `ModLoader.PrepareAssets`;
- first-chance managed exceptions after the texture upload.

The goal is to identify the first exact post-texture blocker before applying another compatibility bridge.

## Rules for the current bootstrap

- Preserve the desktop tModLoader/FNA behavior as much as possible.
- Do not suppress managed exceptions just to advance startup.
- Fix the real compatibility boundary instead.
- Keep the 32 MiB game-thread stack until the startup path is proven stable.
- Keep ARM64 tagged-pointer handling in metadata writes.
- Change one verified blocker at a time.
