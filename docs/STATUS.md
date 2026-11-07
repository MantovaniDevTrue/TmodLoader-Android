# Current status

## Android 0.2.13 — 2026-11-07

The source includes Android's system document picker for importing local `.tmod`
packages, without Steam Workshop, plus a themed launcher, original tModLoader
icon with a separate Android badge, a selectable experimental Vulkan backend,
and revised touch aim/input ownership. See [ANDROID_0_2_13.md](ANDROID_0_2_13.md).

The launcher regression update selects the real render Spinner instead of its
text label and isolates the SDL native tap event from earlier launcher gestures.
The [Android SDL workflow 42394775887](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42394775887)
confirmed that ARM64 packaging, APK checks, x64 emulator build and the OpenGL
probe passed. OpenGL presented 5,496/8,192 expected center pixels. The Vulkan
probe also reached its FNA first frame and GPU stress pass but failed because
the test demanded an unavailable FNA3D driver banner in Android logcat.
The new source instruments the actual SDL GPU device driver and requires
`TML_SDL_GPU_ACTIVE_DRIVER=vulkan` instead. The next CI result, the private
ARM64 package, in-world performance and phone Vulkan stability are pending.
The public source-host APK is not the private playable game package.

## Active direction — dedicated Android port

The 0.2.5 phone report confirms that the .NET 10 ARM64 host created, entered and saved a small world. It also reports severe flicker/black frames, poor performance and high memory use. Repeated portrait/landscape/fullscreen resets appear in the log; no measured gameplay FPS is available. The final managed crash was a desktop `xdg-open` launch from `Terraria.Utils.OpenToURL`; a native save-thread fault followed during shutdown.

The 0.2.9 phone session confirms stable 1600×720 settings without the old resolution flood and successful world entry. Visual corruption persists: black menu, misplaced/clipped scene regions and flickering sprites/UI. Captured presentation-call rates are roughly 51–58/s, draw p95 about 19–21ms, update p95 about 5–7ms, and only two avoided resets. The process was removed by SwipeUpClean; this report has no new native crash. The logged 222MB installed-system-memory value is invalid as a physical-RAM measure; the OS process PSS is about 2.6GiB and managed snapshots about 610–613MiB.

The 0.2.10 phone report confirms that flicker stopped and the startup GPU fixture passed on Adreno, but the UI remains clipped and black scene regions persist. Presentation calls fell to 16–30/s and Android classified the exit as LowMemory (PSS approximately 2.4 GiB). Managed live snapshots remain 626–629 MiB; allocation-window traffic is not total resident memory. The desktop SDK discovery errors are separate from rendering.

The 0.2.11 candidate repairs a game/FNA display-state convergence gap: the original game compares manager preferences, so preconfigured FNA dimensions can leave Main's logical dimensions and UI/input caches unchanged. The hook now makes the original synchronize those dependencies when logical dimensions differ. GLES retains subdata uploads, attribute offsets and float depth but advertises logical NoOverwrite, allowing SpriteBatch to reuse nonoverlapping buffer regions and discard only on wrap. Reports add native heap, process PSS and allocation per recorded tick. Android SDL workflow 42380332682 passed the ARM64 package checks and Android API 30 x86_64 graphics fixture; foundation workflow 42380332719 passed all jobs. Corrected phone visuals, gameplay FPS and memory improvements remain unverified.

The 0.2.11 phone confirms display-state convergence (`1152×864` → `1600×720`). It still reports poor gameplay, approximately 40 seconds before mod discovery, slow save/exit, roughly 2.8 GiB PSS, hundreds of full collections while leaving a world, and a native SIGSEGV at FACTCue_Destroy during FACTWaveBank_Destroy.

The 0.2.12 candidate introduces native SDL touch transport, an in-game HUD and persistent editor accessed from the world's options window, removes forced GC only from EndDraw, reuses bounded compressed-XNB buffers and repairs stopped-cue resource cleanup in pinned FAudio. It adds content/save duration markers and corrects installed-memory reporting with Android's physical-memory API. Effects, render resolution, world format/validation and backups are retained. Local regressions and private module compilation passed. Android SDL workflow 42387360997 and foundation workflow 42387361040 passed; the actual private hooks/options editor also passed a separate headless CoreCLR probe. Full Android phone behavior and performance gains remain pending. See [MOBILE_TOUCH.md](MOBILE_TOUCH.md) for design, scope and verification boundaries.

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
