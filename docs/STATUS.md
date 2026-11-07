# Current status

## Active direction — dedicated Android port

The 0.2.5 phone report confirms that the .NET 10 ARM64 host created, entered and saved a small world. It also reports severe flicker/black frames, poor performance and high memory use. Repeated portrait/landscape/fullscreen resets appear in the log; no measured gameplay FPS is available. The final managed crash was a desktop `xdg-open` launch from `Terraria.Utils.OpenToURL`; a native save-thread fault followed during shutdown.

The 0.2.8 candidate retains the 0.2.6 landscape orientation changes and fixes its startup regression. The 0.2.6 phone failed before Program.LaunchGame: compiling the Main.SetDisplayMode hook initialized Terraria.Main while Program.SavePath was null. Game-specific hooks now wait for FNA Game.Run, after the derived game has been constructed and save paths prepared. The 0.2.7 ARM64 report confirmed deferred startup and both hooks, then failed in SetDisplayMode's copied original when calling private Main.InitTargets. The host now explicitly selects DynamicMethod generation with skip-visibility and adapts .NET 10 Mono dynamic-method handles instead of relying on the legacy mhandle lookup. A private-call/field/generic/original-call regression probe also exercises garbage collection while the hook is live. The render policy fixes landscape orientation in the Android activity, enforces a bounded internal render size (1416×640 on the reported 2400×1080 display), coalesces unchanged FNA device settings, skips drawing while the activity is suspended, and routes game links to Android intents. A bounded frame-timing report is now collected automatically. These changes still need phone validation; stable visuals, a 60 FPS target, low memory use and broad mod compatibility are not yet certified.

Historical foundation: The project will adapt tModLoader specifically for Android, preserving the selected upstream C# API and mod behavior. The user confirmed that the 0.1.1 diagnostic opens and its on-screen DLL/Emit probes succeed on ARM64. Optional phase timing and a bounded reporting collector are now implemented in the pinned FNA source; real game timing and performance advantages remain unmeasured.

See [ANDROID_PORT.md](ANDROID_PORT.md) for delivery gates and [PERFORMANCE.md](PERFORMANCE.md) for measurement scope. The user's private Terraria PC 1.4.5.8 installation was received and passed archive and basic content validation on 2026-11-06. See [PC_INPUT_VALIDATION.md](PC_INPUT_VALIDATION.md). The matching tModLoader managed candidate now compiles for .NET 10 against mobile FNA and passes assembly-loading checks; see [GAME_BUILD.md](GAME_BUILD.md). The SDL3 surface/managed entry, native ARM64 dependencies, transparent overlay, content importer and private game entry wiring now build in the 0.2.0 host. Runtime graphics/game validation is the next boundary; see [SDL_HOST.md](SDL_HOST.md).


## Historical JNI startup failure

The user reported that diagnostic APK 0.1.0 failed before showing its UI: `UnsatisfiedLinkError` for `MainActivity.n_onCreate`. Version 0.1.1 explicitly disables native marshal-method optimization and restores dynamic JNI registration. A packaged-registration check and an Android emulator startup job were added. See [STARTUP_FIX_0_1_1.md](STARTUP_FIX_0_1_1.md) for evidence and remaining validation.


## Historical foundation — Phase 3.0 (.NET 10 / ARM64 mobile foundation)

The new source compiles a standalone ARM64 Android input/runtime diagnostic APK. Shared touch routing and actual FNA input integration are implemented; the desktop game entrypoint/SDL host has **not** been migrated to this app yet. No playable world, full mobile UI or general `.tmod` compatibility is claimed.

Completed local validation: .NET 10 shared input checks, separate-assembly/Reflection.Emit probes on desktop, and compilation/input checks against the exact upstream FNA revision. APK build/signature/ABI checks are recorded in [NET10_VALIDATION.md](NET10_VALIDATION.md). Device validation is pending.

The historical phase 2.23.01 APK was inspected and has no managed game/runtime/content payload. The matching vanilla PC executable and Content tree are now available privately and have passed the checks in [PC_INPUT_VALIDATION.md](PC_INPUT_VALIDATION.md). The .NET 10 managed payload now builds against mobile FNA and passes API/type resolution checks. Advancing to a playable port still requires SDL/native-host integration and on-device MonoMod validation. [Scope and integration requirements](NET10_MOBILE.md).

## SDL3 Android host — 2026-11-06

Built SDL3 3.4.20, pinned FNA3D and FAudio for Android ARM64 and x86_64. The 0.2.0 app includes native-to-managed entry in the SDK runtime, a real FNA graphics probe, transparent touch controls, streamed PC ZIP import and optional private tModLoader dependencies. ARM64 ABI/page alignment, v2/v3 signing and JNI metadata checks passed. Source-only emulator verification is configured separately. No menu/world/FPS/device claim is made. See [SDL_HOST.md](SDL_HOST.md).

## Managed game build — 2026-11-06

Pinned upstream sources were generated and patched in strict EXACT mode. tModLoader, ReLogic, mobile FNA and shared core compiled for .NET 10; the API/type loading check passed. The final build has zero errors and 927 upstream warnings. The 57 shared/FNA/performance checks passed. This remains a private managed candidate with desktop development dependencies, not an Android game build. See [GAME_BUILD.md](GAME_BUILD.md).

## Private PC input validation — 2026-11-06

Terraria PC 1.4.5.8 was received in four private archive parts. The reconstructed ZIP passed CRC checks; its Content tree has 15,997 files, including the previously missing splash asset. All 15,994 XNB files passed signature and declared-size checks. This validates input availability, not Android asset decoding, shader compatibility or game startup. No proprietary bytes or private download links are committed.

## Historical game track


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

### GLES rendering verified (0.2.1)

Android SDL host workflow [42311139412](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42311139412) passed on API 30 x86_64, including a screenshot pixel check for the actual SpriteBatch rectangle. Fixed GLES base-vertex detection so unsupported EGL stubs are not treated as implemented drawing functions. The private ARM64 0.2.1 package builds cleanly and passes ABI/alignment/signature/JNI checks. ARM64 phone launch and Terraria menu/world validation remain pending.
