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
- real 1×1 texture allocation and 4-byte pixel upload;
- FAudio no-device probe bridge;
- expected `NoAudioHardwareException` handled inside `SoundEngine.TestAudioSupport`;
- successful return from `SoundEngine.Initialize()`;
- `AssetInitializer.CreateAssetServices()`;
- registration of PNG, XNB, rawimg, FXC, WAV, MP3 and OGG asset readers;
- `AssetRepository` creation and main-thread registration.

## Phase 2.22.99 result

The no-audio compatibility path is proven.

Observed sequence:

1. `FAudioCreate` bridge executes.
2. `FAudio_GetDeviceCount` returns zero.
3. `FAudio_Release` executes.
4. FNA raises `NoAudioHardwareException`.
5. `Terraria.Audio.SoundEngine.TestAudioSupport()` handles it.
6. `Terraria.Audio.SoundEngine.Initialize()` returns normally.
7. `Main.LoadContent` continues into asset service initialization.

This confirms that the earlier `libFAudio.so` problem is no longer the active blocker.

## Current blocker

The next failure is:

`System.IO.FileNotFoundException: .../Content/PixelShader.xnb`

followed by:

`Microsoft.Xna.Framework.Content.ContentLoadException: Could not load asset PixelShader`

The failure occurs in:

`Terraria.ModLoader.Engine.TMLContentManager.Load -> OpenStream`

This is a content payload problem rather than another Mono/FNA ABI failure.

The platform content set also contains:

- `PixelShader.xnb`;
- `TileShader.xnb`;
- `ScreenShader.xnb`.

These files are intentionally not source-controlled upstream. The tModLoader repository's own legacy file manifest describes them as non-GitHub files and says those omitted files are not theirs to host. Our public Android repository therefore must not commit copies of them either.

## Next action

Supply the correct FNA/Linux-compatible shader content files from a legitimate tModLoader/Terraria installation into the Android content root, then retest the same 2.22.99 APK before changing bootstrap code.

Only if loading the real shader files exposes another runtime incompatibility should a new bootstrap phase be created.

## Rules for the current bootstrap

- Preserve the desktop tModLoader/FNA behavior as much as possible.
- Prefer the upstream fallback path over swallowing or hiding exceptions.
- Do not ship arbitrary or mismatched native/audio/content payloads just to move startup forward.
- Keep the 32 MiB game-thread stack until the startup path is proven stable.
- Keep ARM64 tagged-pointer handling in metadata writes.
- Preserve the validated FNA3D texture bridges.
- Keep proprietary/non-source-controlled game content out of the public repository.
- Change one verified blocker at a time.
