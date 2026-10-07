# Current status

## Active baseline

**Phase 2.22.99 — FAudio No-Device Fallback**

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
- render-target allocation;
- `FNA3D_CreateTexture2D` InternalCall bridge;
- `FNA3D_SetTextureData2D` InternalCall bridge;
- real 1×1 texture allocation and 4-byte pixel upload.

## Phase 2.22.98 result

The focused post-texture trace reached:

`Terraria.Audio.SoundEngine.Initialize()`

and then recorded:

`System.DllNotFoundException: libFAudio.so`

as the first post-texture managed compatibility boundary.

This is important because the upstream Terraria/FNA audio path is designed to fall back to a disabled audio system if audio support cannot be created. The first-chance exception therefore identifies the missing native dependency, but by itself does not justify replacing the entire audio system or patching gameplay code.

## Current investigation

Phase 2.22.99 introduces a deliberately narrow startup compatibility path for the FAudio probe.

Before first JIT, it converts only:

- `FAudio.FAudioCreate`;
- `FAudio.FAudio_GetDeviceCount`;
- `FAudio.FAudio_Release`;

from P/Invoke to InternalCall, using the same ARM64 tagged-pointer-safe Mono metadata mechanism already proven by the FNA3D texture fixes.

The native bridge reports a valid probe context and zero audio devices. The expected managed behavior is then:

1. FNA sees zero audio devices;
2. audio support is reported unavailable;
3. Terraria/tModLoader creates `DisabledAudioSystem`;
4. `SoundEngine.Initialize()` returns;
5. `Main.LoadContent` continues to the next real startup boundary.

This is a temporary no-audio compatibility baseline. Real Android FAudio integration remains a later milestone.

## Rules for the current bootstrap

- Preserve the desktop tModLoader/FNA behavior as much as possible.
- Prefer the upstream fallback path over swallowing or hiding exceptions.
- Do not ship an arbitrary/mismatched native audio library just to move startup forward.
- Keep the 32 MiB game-thread stack until the startup path is proven stable.
- Keep ARM64 tagged-pointer handling in metadata writes.
- Preserve the validated FNA3D texture bridges.
- Change one verified blocker at a time.
