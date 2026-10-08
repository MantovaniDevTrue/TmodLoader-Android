# Current status

## Baselines

Validated baseline:

**Phase 2.22.99 — FAudio No-Device Fallback**

Current test baseline:

**Phase 2.23.01 — FNA3D SpriteBatch Buffer Bridge**

Target:

- Android ARM64
- MonoVM runtime 8.0.23
- tModLoader 1.4.5-era desktop assembly stack
- FNA/FNA3D graphics path
- controlled first-frame bootstrap

## Confirmed working

The bootstrap has crossed these startup stages:

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
- `AssetRepository` creation and main-thread registration;
- successful opening of the supplied `Content/PixelShader.xnb`.

## Phase 2.22.99 validation result

The no-audio compatibility path is proven.

Observed sequence:

1. `FAudioCreate` bridge executes.
2. `FAudio_GetDeviceCount` returns zero.
3. `FAudio_Release` executes.
4. FNA raises `NoAudioHardwareException`.
5. `Terraria.Audio.SoundEngine.TestAudioSupport()` handles it.
6. `Terraria.Audio.SoundEngine.Initialize()` returns normally.
7. `Main.LoadContent` continues into asset service initialization.

The earlier `libFAudio.so` issue is no longer the active blocker.

## Shader payload result

The required platform files were supplied externally from the matching tModLoader package:

- `Content/PixelShader.xnb`;
- `Content/TileShader.xnb`;
- `Content/ScreenShader.xnb`.

On the next run, the previous `PixelShader.xnb` `FileNotFoundException` disappears.

The new trace reaches:

`Terraria.ModLoader.Engine.TMLContentManager::OpenStream`

and records a normal `LOADCONTENT_LEAVE`.

That proves the file is found and opened successfully.

## Current boundary

The shader stream and native effect creation boundaries are now crossed. The current boundary is the managed exception raised after the third successful `FNA3D_CreateEffect` return.

Upstream FNA's shader load path is:

`TMLContentManager.Load -> ContentReader -> EffectReader.Read -> Effect::.ctor -> FNA3D_CreateEffect`

`EffectReader` reads the compiled shader bytes into a managed `byte[]`, and the `Effect` constructor passes that blob to `FNA3D_CreateEffect`.

This makes `FNA3D_CreateEffect` the next unmanaged boundary to test.

## Phase 2.23.00

The current test phase:

- preserves the validated FAudio and texture bridges;
- resolves the APK's real `FNA3D_CreateEffect`;
- resolves Mono's public `mono_array_length` and `mono_array_addr_with_size` embedding APIs;
- converts `FNA3D_Impl.FNA3D_CreateEffect` from P/Invoke to InternalCall before first JIT;
- forwards the real managed effect byte array to the native APK FNA3D implementation;
- adds focused tracing for `ContentReader`, `EffectReader.Read`, `Effect::.ctor` and `FNA3D_CreateEffect`.

Phase 2.23.00 has now validated the `FNA3D_CreateEffect` bridge on-device.

Observed native effect creations:

- 91,904-byte effect blob -> non-null `effect` and `effectData`;
- 34,052-byte effect blob -> non-null `effect` and `effectData`;
- 39,320-byte effect blob -> non-null `effect` and `effectData`.

This proves the real shader byte arrays are reaching the APK FNA3D implementation and three effects are being created successfully.

The exact next exception is now known:

`System.InvalidProgramException` in `FNA3D_Impl.FNA3D_GenVertexBuffer`.

The stack is:

`FNA3D_GenVertexBuffer -> VertexBuffer..ctor -> DynamicVertexBuffer..ctor -> SpriteBatch..ctor -> Terraria.Main.LoadContent`.

This proves the effect bridge is complete and the next boundary is the SpriteBatch buffer allocation path.

## Rules for the current bootstrap

- Preserve the desktop tModLoader/FNA behavior as much as possible.
- Prefer real upstream code paths over swallowing exceptions.
- Keep the 32 MiB game-thread stack until the startup path is proven stable.
- Keep ARM64 tagged-pointer handling in metadata writes.
- Preserve the validated FNA3D texture bridges.
- Keep proprietary/non-source-controlled game content out of the public repository.
- Bridge one verified unmanaged boundary at a time.

## Phase 2.23.01

The new test phase preserves all validated bridges and converts the exact SpriteBatch buffer path to InternalCalls before first JIT:

- `FNA3D_GenVertexBuffer`;
- `FNA3D_GenIndexBuffer`;
- `FNA3D_SetIndexBufferData`;
- `FNA3D_SupportsNoOverwrite`.

Each bridge forwards to the real symbol from the APK `libFNA3D.so`; no fake graphics buffers are created. The build also traces `SpriteBatch`, `VertexBuffer`, `DynamicVertexBuffer` and `IndexBuffer` constructors to expose the next boundary cleanly.
