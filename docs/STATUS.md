# Current status

## Baselines

Latest validated baseline:

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

The native graphics path has moved past shader creation and SpriteBatch buffer allocation.

The current failure is:

`ReLogic.Content.AssetLoadException: Asset could not be found: "Images/SplashScreens/Splash_1"`

with the stack:

`AssetRepository.Request<Texture2D> -> AssetInitializer.LoadAsset<Texture2D> -> AssetInitializer.LoadSplashAssets -> Terraria.Main.LoadContent`.

This is a content-root problem, not a new FNA3D or Mono JIT failure.

Upstream tModLoader intentionally uses **two content roots**:

1. the vanilla Terraria PC `Content` directory as the base content tree;
2. the local tModLoader `Content` directory as an optional override tree.

The Android bootstrap already has routing support for external content roots. The next requirement is to provide a complete matching Terraria PC vanilla Content tree instead of copying individual XNB files one-by-one.

The current bootstrap can inspect:

- `/storage/emulated/0/Android/media/com.mantovani.tmlmono8/Content`;
- `/storage/emulated/0/Android/media/com.mantovani.tmlmono8/Terraria/Content`;
- the app-private `Content` tree.

The preferred long-term layout is to keep the vanilla Terraria PC content separate under `Terraria/Content` and reserve the top-level `Content` directory for tModLoader/platform overrides.

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

## Phase 2.23.01 validation

The SpriteBatch buffer bridge is validated on-device.

Observed successful native calls include:

- repeated `FNA3D_GenVertexBuffer` allocations with non-null buffer handles;
- repeated `FNA3D_GenIndexBuffer` allocations with non-null buffer handles;
- repeated `FNA3D_SetIndexBufferData` uploads;
- repeated `FNA3D_CreateEffect` calls, including the internal SpriteBatch effect;
- repeated `FNA3D_SupportsNoOverwrite` calls;
- continued texture/render-target creation after SpriteBatch construction.

This proves the first SpriteBatch and subsequent SpriteBatch instances cross the previously failing native buffer path successfully.

The current run advances further into `Main.LoadContent`, creates additional 4x4 textures and 2048x2048 render targets, and then reaches the vanilla splash asset load. The next blocker is a missing `Images/SplashScreens/Splash_1` asset in the selected vanilla content root.
