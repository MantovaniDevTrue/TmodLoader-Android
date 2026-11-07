# SDK-managed SDL3 Android host

The new `src/Tml.Game.Android` project builds an experimental .NET 10 Android host. It is separate from the original input diagnostic and uses the same application ID with version 0.2.10. The Java SDL activity, native entry stub, C# game host, mobile core, FNA and private game payload share one process and one SDK-managed Mono runtime.

## Implemented

- Pinned SDL 3.4.20 Android activity/surface, FNA3D and FAudio compiled with NDK 28.2.13676358 for ARM64. An x86_64 build exists for emulator checks.
- A tiny `SDL_main` calls the registered managed Java export. It does not load or initialize another Mono runtime. Dynamic JNI registration remains enabled; native marshal-method optimization remains disabled.
- SDL's Java game thread requests a 32 MiB stack. Pause/focus loss cancels touch input through the shared router; the existing SDL lifecycle manages the surface/audio state.
- The touch overlay is transparent and the real FNA update loop consumes its frames. Diagnostic headings are hidden over the game surface.
- A graphics probe creates an actual FNA GraphicsDevice, uploads a texture and draws through SpriteBatch. `SDL_FNA_FIRST_FRAME` records draw submission; the emulator test also checks the presented screenshot for the centered green rectangle. This is a rendering test, not Terraria gameplay.
- The launcher imports the complete owned PC installation ZIP through Android's document picker. Import streams through a cache file, checks extraction paths/size, verifies the supplied 1.4.5.8 executable hash and preflights known content. It replaces the active content directory after validation and preserves the separate saves directory.
- Private managed game dependencies can be embedded as an optional ZIP asset. The launcher verifies the imported executable before extracting those dependencies and calling the actual tModLoader entrypoint. A host/session report and tModLoader client log can be viewed/copied from the launcher.

## Native build

Install the Android SDK platform 36, build-tools 36.0.0, CMake 3.22.1 and NDK 28.2.13676358. Use the FNA revision in `upstream.lock.json`, with recursive submodules, and the pinned SDL checkout:

```bash
python3 tools/build_native.py /source/SDL /source/FNA /android-sdk artifacts/android-native
python3 tools/build_native.py /source/SDL /source/FNA /android-sdk artifacts/android-native --abi x86_64
```

The build checks source revisions and copies SDL's Java sources with their license. The context-checked Java edit requests the larger stack. A context-checked FNA3D edit gates GLES base-vertex drawing on actual ES 3.2 support or advertised OES functions; Android EGL may return non-null unsupported stubs. Earlier contexts use the existing vertex-attribute offset fallback. FNA3D is built with its OpenGL backend; its SDL GPU backend is excluded because the pinned source fails to compile against stable SDL 3.4 headers (`resourceSetLayoutInfos` outside its version guard). No Vulkan/SDL GPU result is claimed. The separate Theorafile video library is not packaged in this initial host.

## Prepare the private game

Generate pinned upstream sources as described in [GAME_BUILD.md](GAME_BUILD.md), then apply the Android edits and build a new private candidate:

```bash
python3 tools/prepare_android_game.py /private/tModLoader
python3 tools/build_game.py /private/tModLoader /private/android-candidate --dotnet /path/to/dotnet
python3 tools/package_game_payload.py /private/android-candidate/game artifacts/game-payload.zip
```

The Android edits operate only on generated upstream `src/tModLoader` files: validate the user's executable instead of requiring the desktop Steam client, use the actual Android native library directory, skip unavailable Steam Workshop initialization and desktop-specific diagnostic detours, and preserve an existing SDL thread name. Local mod hooks are not disabled. Regenerate upstream patches with setup's diff operation when maintaining changes in that source tree; do not commit proprietary generated source or game binaries here.

The private payload excludes desktop native libraries and duplicate FNA/mobile-core assemblies. It contains game code and must stay out of the public repository. Workshop browsing/downloading and desktop helper-process workflows are not implemented on Android.

## APK

```bash
dotnet build src/Tml.Game.Android -c Release -m:1 -nr:false -t:SignAndroidPackage \
  -p:AndroidSdkDirectory=/android-sdk -p:JavaSdkDirectory=/jdk21 \
  -p:TmlMobileFnaProject=/private/android-candidate/fna/FNA.Core.csproj \
  -p:TmlNativeDirectory="$PWD/artifacts/android-native" \
  -p:TmlGamePayload="$PWD/artifacts/game-payload.zip"
```

Omit `TmlGamePayload` for the public source-only graphics probe. The ARM64 host APK passed native ABI/16 KiB ELF and ZIP alignment checks, APK v2/v3 signature checks and dynamic JNI metadata checks for the launcher, overlay and exported managed entrypoint. These are package checks, not proof that a Terraria menu/world renders on a phone.

The `Android SDL game host` workflow builds both native ABIs without game files and tests the real graphics probe on an emulator. Actual game initialization, ARM64 device rendering/audio recovery, runtime detours, menu/touch adaptation and mod/world/save behavior require separate runtime validation. An API/type-loading check on desktop is insufficient for those claims.

## Runtime evidence — 2026-11-06

Source-only workflow run [42311139412](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42311139412) passed on Android API 30 x86_64: SDK-managed entry, GLES 3.0 FNA3D device, texture upload and SpriteBatch presentation. The screenshot contains the centered SeaGreen rectangle above the clear color with the transparent touch overlay; the pixel check found 6240/8192 matching center pixels. Before the GLES capability fix the same test produced only the clear color and controls, with unsupported GL calls. This validates the emulator rendering path, not ARM64 phone gameplay or a Terraria menu/world.

The private ARM64 0.2.1 APK builds with zero warnings/errors and passes ABI/16 KiB alignment, v2/v3 signature and dynamic JNI metadata checks. Its development signing certificate differs from the older candidate; back up imported files/saves before removing an installed app if Android rejects an update for a signature mismatch. The new development key is retained privately for subsequent builds.

## Startup crash capture — 0.2.2

An ARM64 device report reached `TML_ENTRY_BEGIN` and then ended without an exception. This is not enough evidence to identify the cause. Version 0.2.2 records assembly-load/type/invocation boundaries, managed stdout/stderr, the first 64 startup exceptions (which may be handled), unhandled exceptions and managed process exit. A native bridge redirects stdout/stderr to a private file without installing signal handlers.

The launcher report includes bounded tails of host/native/game logs and Android process exit history. On Android 12+ it decodes the latest available native tombstone: signal, abort message, causes and up to 24 frames of the crashing thread. Register/memory dumps and unrelated threads are omitted. The decoder follows AOSP Android 14 tombstone.proto, rejects malformed/truncated wire fields, and has 11 host-side checks. Updating preserves imported content, saves and the 0.2.1 signing key; the private game payload is unchanged.

After updating, first open the report before launching another session: Android may retain a useful native trace from the previous failure. If no useful trace is available, reproduce once and reopen the report. This update improves diagnosis; it does not claim the reported game startup crash is fixed.

The source-only 0.2.2 workflow [42313215054](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42313215054) passed: all 11 decoder checks, ARM64 packaging/JNI validation and x86_64 emulator graphics presentation. Its runtime log confirms `NATIVE_CONSOLE_CAPTURE status=0` before `MANAGED_SDL_ENTRY` and `SDL_FNA_FIRST_FRAME`; the screenshot passed with 6240/8192 green center pixels. The private ARM64 APK builds with zero warnings/errors, retains the 0.2.1 certificate and passes 16 KiB ABI/alignment and signing checks. The original phone game-startup failure still needs its recovered trace.

## Embedded entry assembly — 0.2.3

The phone trace identified a managed `NullReferenceException` in `Logging.LogStartup`, immediately after logging FrameworkDescription. The next statement dereferenced `Assembly.GetEntryAssembly().Location`; JNI-hosted Mono can have no entry assembly. The process then called `ErrorReporting.FatalExit` and `Environment.Exit(1)`. The subsequent shutdown mutex messages are not evidence of the initial cause.

The host now scopes the .NET 10 `Assembly.SetEntryAssembly` override to its managed entry and to the actual loaded tModLoader assembly during the game invocation, restoring the previous value on return/unwind. No binary modification or replacement of the private game payload is required. `TML_ENTRY_ASSEMBLY_READY tModLoader` marks the game identity before launch. The source preparation script also adds a null-safe assembly fallback for future managed game rebuilds.

Seven additional checks reproduce a null entry assembly and the original null dereference, verify the logger location expression after the fix, and exercise nested scopes and exception cleanup. The emulator smoke test requires the host identity marker as well as the presented FNA sprite. Full Terraria initialization after this point still requires the next phone test.

Workflow [42316353901](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42316353901) passed for 0.2.3: 18 host regression checks, ARM64 package/JNI checks and an Android API 30 x86_64 Mono/FNA run requiring `HOST_ENTRY_ASSEMBLY_READY Tml.Game.Android` plus a presented sprite pixel check. The private ARM64 APK builds without warnings/errors and retains the 0.2.2 signing certificate. This verifies the entry-assembly API on Android; progression beyond the actual phone game logger still awaits device confirmation.

## Android native audio file paths — 0.2.4

The ARM64 phone advanced through startup logging, PC verification, Adreno GLES 3.2 device creation and AAudio initialization, then faulted at `FACT_INTERNAL_ParseSoundBank+68`. Disassembly places that offset at the first read of the buffer argument; Android reported a null pointer dereference. SDL Android resolves relative native reads from app internal storage, whereas managed `File.Exists` resolves them against the process working directory. A relative path could therefore pass the managed check but produce a null `SDL_LoadFile` result, which FNA previously passed to FACT without validation.

The prepared FNA now resolves native file paths to absolute paths for both in-memory banks/settings and FAudio streaming banks. Failed SDL reads throw an IOException containing the full path and SDL error before entering the parser; missing streaming files throw FileNotFoundException. Partially constructed SoundBank/WaveBank finalizers skip native calls before their engine is assigned. The game payload, imported content and audio playback remain enabled.

The Android graphics probe now includes a native I/O regression fixture in a separate working directory: reproduce the original relative SDL failure, then verify relative/absolute reads with spaces and UTF-8, FAudio streaming bytes and managed exceptions for missing files. This probe uses synthetic bytes without invoking a sound-bank parser or distributing game assets. The emulator smoke check requires `NATIVE_FILE_IO_OK` as well as entry identity and a presented FNA sprite. Actual Terraria audio initialization remains a phone test boundary.

Workflow [42319755599](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42319755599) passed on Android API 30 x86_64. Runtime markers confirmed `NATIVE_RELATIVE_PATH_MISMATCH_REPRODUCED`, then `NATIVE_FILE_IO_OK relative+absolute+spaces+utf8+streaming+missing+denied`, followed by `SDL_FNA_FIRST_FRAME`. The screenshot pixel check passed with 6240/8192 center pixels. SDL Android GetBasePath returns `./`, and its relative IOFromFile prepends app internal storage, corroborating the root cause. This validates file transport and streaming, not decoding the private Terraria sound bank or playing a world on ARM64. The matching private 0.2.4 APK passed package/JNI checks and retains the development signing certificate.

## Android MonoMod system adapter — 0.2.5

The phone 0.2.4 session passed the previous audio-bank read failure and entered the FNA game loop, then its preload worker aborted while initializing AssemblyManager.AssemblyRedirects. Pinned MonoMod.Core 1.3.4 explicitly throws NotImplementedException for OSKind.Android. The subsequent mutex/presentation failures occurred during the unhandled-exception shutdown; no separate renderer fix is claimed.

The host supplies an Android ISystem through PlatformTriple.SetPlatformTriple before tModLoader code runs. It uses Bionic mmap/mprotect/munmap directly, the runtime page size, pooled MonoMod page allocations, bounded non-overwriting address hints, readable-range checks from /proc/self/maps, original protection restoration and an NDK instruction-cache flush. The pinned System V ARM64/x86_64 classifier and Mono runtime adapter are reused. This does not instantiate the glibc LinuxSystem or load its desktop exception helper. Native exception-helper bridging and CoreCLR JIT interception are not provided.

The host pins MonoMod.RuntimeDetour 25.3.4, Core 1.3.4 and Utils 25.0.12 in the managed app so the platform is registered before background static initialization. Startup performs actual managed hook apply/original-call/chain/repeated-removal checks. The private game path additionally initializes the known AssemblyRedirects constructor on the SDL thread before starting graphics or the game preload worker, so a remaining incompatibility can reach HOST_ERROR without an active rendering loop. The game payload and imported content are unchanged.

Workflow [42321846324](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42321846324) passed on Android API 30 x86_64 Mono: `ANDROID_DETOUR_PLATFORM_READY x86_64 Mono`, `ANDROID_DETOUR_PROBE_OK apply+original+chain+repeat+remove`, native file I/O checks and a presented FNA sprite (6240/8192 center pixels). The later SDL-thread private-game preflight addition builds locally; the private Terraria payload is not included in CI. ARM64 hook behavior, the actual game assembly redirect and broader mod/ILHook/struct-generic compatibility remain device validation boundaries. Upstream NuGet intentionally still emits its unsupported-Android RID warning: this is an experimental host adapter, not upstream Android support.


## Stable mobile display and Android links — 0.2.6

The 0.2.5 ARM64 report reached an actual world. It repeatedly changed between landscape and portrait dimensions, then crashed while opening a menu URL through the Linux desktop helper `xdg-open`. The new activity locks sensor-landscape and restores immersive system bars on focus/resume. The FNA preparation applies one landscape render policy after PreparingDeviceSettings, mirrors those dimensions into manager preferences, and avoids device resets when all resulting presentation parameters are unchanged. Terraria.Main.SetDisplayMode is hooked to the same size/fullscreen policy so its screen fields and render targets agree. Drawing/presentation are suppressed while activity input is suspended.

The 0.2.6 policy used at most 1280×720 pixels with an assumed 800×600 minimum (the pinned game's real 720px height minimum is corrected in 0.2.9) and the display's landscape aspect where possible. On the reported 2400×1080 screen this chooses 1416×640: about 45% fewer pixels than the logged 1920×864 mode. This is a workload reduction, not an FPS measurement. Portrait requests, repeated resolution changes and fullscreen toggles in the graphics probe must produce zero device resets before its first rendered frame (`ANDROID_DISPLAY_STABLE`).

Terraria.Utils.OpenToURL is hooked to an Android ACTION_VIEW intent for supported absolute URLs. Intent failures are logged without throwing through Draw. No desktop process is launched. Private game code is not uploaded to CI; its exact API signatures were checked locally. The on-device hook/game paths remain a validation boundary.

SessionPerformance attaches the existing allocation-free recording adapter on the game thread. Every five seconds a reporting timer writes the last twelve bounded snapshots to frame-performance.log, included in the launcher report. It records CPU update/draw/presentation p95, presentation-call rate, managed bytes, collection deltas and avoided resets. These are overlapping windows, not session averages, GPU timestamps or process PSS. Pause gaps can appear in pacing statistics until they leave the window.

The CI source-host artifact excludes private game assets and signing keys. Local packaging appends the previously validated private game-payload.zip to that APK, preserving existing native-entry offsets, then signs with the existing development certificate. The completed package must pass signature and 16 KiB alignment checks before private delivery.

Validation: [workflow 42359495282](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42359495282) built ARM64 and x86_64 packages, passed packaged JNI/native alignment checks, and required ANDROID_DISPLAY_STABLE before validating the presented FNA SpriteBatch rectangle (6384/8192 center pixels) on Android API 30. The foundation workflow 42359495226 also passed. Local checks passed: 36 shared mobile, 18 host, 16 collector and 18 actual-FNA adapter checks. These are not private Terraria gameplay or phone FPS benchmarks.

Private 0.2.6 delivery SHA-256: `27c3ed0907963b31baa426203d66aac129701af4c24ccf23596c31328937e1b8` (51,672,661 bytes). APK v2/v3 signatures verify against the existing development certificate, all native libraries remain ARM64 and 16 KiB aligned, and the private runtime payload is byte-identical to 0.2.5. Install over the existing app to retain imported assets and saves. Test an existing world, rotation, background/resume and menu links, then collect the report's new Desempenho section.


## Defer game hooks until startup has completed — 0.2.7

The ARM64 0.2.6 report failed before TML_LAUNCH_INVOKE. MonoMod's GetFunctionPointer on Main.SetDisplayMode ran Terraria.Main's static constructor. Main.SavePath returns Program.SavePath, which is assigned only by ProcessLaunchArgs/SetSavePath during Program.LaunchGame. The constructor called Path.Combine(null, "Worlds"), permanently poisoning Main for that process. Basic hook and graphics probes did not cover this private-game bootstrap ordering.

The host now registers a scoped BeforeRun callback in mobile FNA without binding either Terraria method. The patched Game.Run invokes it only after the derived game instance exists. At that point the private Program.RunGame has passed save-path initialization and constructed Main. The callback checks Program.SavePath, logs TML_GAME_RUN_READY, then installs the existing URL/display hooks. Callback subscriptions and hooks are released when the invocation unwinds. The render policy, imported game data and save paths are unchanged; the host does not assign replacement paths or swallow type-initialization failures.

The Android graphics probe also exercises this boundary: register while a synthetic bootstrap path is unavailable, mark paths ready, enter actual FNA Game.Run, and install/remove a real MonoMod hook on a class whose explicit static constructor requires that bootstrap state. The smoke test requires ANDROID_DEFERRED_BOOTSTRAP_OK in addition to stable graphics, native I/O and presented pixels. This checks the mechanism on Android Mono; actual private Terraria startup still requires another ARM64 phone test.

Local FNA integration validation now has 23 passing checks. Five new checks cover metadata-only registration before paths, unrelated-game isolation, path-dependent static initialization after bootstrap, single installation on repeated run dispatch, and subscription cleanup. The Android smoke fixture additionally exercises actual MonoMod apply/original/removal at the real Game.Run boundary.

Validation: [SDL host workflow 42360692480](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42360692480) passed for code edbdb1eea5756af73619f4111853d899e31fe280: ARM64 package/JNI checks, Android API 30 x86_64 Mono deferred-bootstrap hook check, stable display and native-file checks, and a presented sprite pixel check (6384/8192). [Foundation workflow 42360808959](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42360808959) passed with the 23-check FNA suite including bootstrap-order tests.

Private 0.2.7 APK: 51,676,757 bytes; SHA-256 `4511c27040d0ad23b5c246bf8f7ad221f40a7ebde3452ffa850c786e46b0232a`. ZIP CRC/unique-entry, ARM64/16 KiB alignment and APK v2/v3 signature checks passed. The signing certificate and private managed game payload are unchanged. This verifies the repair mechanism, not a new private Terraria phone run or gameplay performance.


## Private calls in copied originals — 0.2.8

The 0.2.7 ARM64 report confirms TML_GAME_RUN_READY, both Android hooks, and Adreno GLES initialization. It then fails inside the generated DMD<SetDisplayMode> copied original with MethodAccessException for private Terraria.Main.InitTargets during FullscreenStartup. No frame reaches the performance collector. Later process-exit/mutex messages are shutdown effects; the reported fault is in the copied method's private-member access.

Pinned MonoMod.Utils 25.0.12 defaults to Cecil generation when its old Mono reflection fields (il_info/ilgen) are absent. The host now sets the supported MonoMod DMDType switch to dynamicmethod before creating any hooks. The DynamicMethod generator associates the copied original with its declaring type and skip-visibility enabled. This avoids executing Terraria's copied original in a separate Cecil assembly whose access checks rejected InitTargets. The display/URL hooks and deferred bootstrap point remain enabled; no private game member is made public.

.NET 10 Mono also renamed the DynamicMethod handle field to _mhandle, whereas pinned MonoMod.Core 1.3.4 looks for mhandle and then falls back to the unsupported DynamicMethod.MethodHandle property. AndroidMonoRuntime wraps the pinned Mono runtime adapter: CreateDynMethod plus GetRuntimeMethodInfo normalize dynamic methods to the runtime's ordinary MethodInfo internally for handle, pinning and compilation operations. Identity lookups translate the runtime MethodInfo back to the original DynamicMethod, so PlatformTriple does not try to find a synthetic method in its owner type's metadata. A ConditionalWeakTable associates that MethodInfo with its DynamicMethod owner while the MethodInfo is live/pinned. ABI classification, native method flags and normal method handling remain delegated to the existing adapter. Missing runtime members fail explicitly before game startup.

The Android probe now requires the selected backend to actually return a DynamicMethod. Its target calls a private non-inlined helper, accesses a private field and calls a nonpublic generic helper. Original calls, chained hooks, removal and a forced collection with a live hook must all pass before ANDROID_PRIVATE_ACCESS_OK is logged. The graphics smoke test requires this marker, the backend marker, deferred bootstrap and stable display checks before verifying presented pixels. This is a compatibility check, not a gameplay FPS benchmark or a guarantee for every mod hook signature.

Implementation references: MonoMod's [DynamicMethod generator](https://github.com/MonoMod/MonoMod/blob/reorganize/src/MonoMod.Utils/DMDGenerators/DMDEmitDynamicMethodGenerator.cs), [switch API](https://github.com/MonoMod/MonoMod/blob/reorganize/src/MonoMod.Utils/_/Switches.cs), [Mono runtime adapter](https://github.com/MonoMod/MonoMod/blob/reorganize/src/MonoMod.Core/Platforms/Runtimes/MonoRuntime.cs), and [.NET 10 Mono DynamicMethod](https://github.com/dotnet/runtime/blob/v10.0.0/src/mono/System.Private.CoreLib/src/System/Reflection/Emit/DynamicMethod.Mono.cs). Pinned packaged DLL strings additionally confirm legacy mhandle/il_info/ilgen names.

Intermediate Android experiment [42362259682](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42362259682) confirmed that the selected DynamicMethod generated and executed the private-access copy, then failed at MonoRuntime.GetMethodHandle -> DynamicMethod.MethodHandle. This reproduced the separate legacy-handle incompatibility before adding AndroidMonoRuntime; that intermediate APK was not delivered.

The first normalization experiment [42362456068](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42362456068) advanced beyond native-handle lookup but exposed that identity boundary: PlatformTriple.GetIdentifiable asserted when a synthetic RuntimeMethodInfo's reflected and declaring types differed and no metadata member matched its generated name. Returning the original DynamicMethod for known dynamic identities avoids that lookup while native operations continue to use its normalized runtime handle. This intermediate package was also not delivered.

Validation: [SDL host workflow 42362940344](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42362940344) passed for code 1f183f0b7f13df0db7dc5ee7f16c61fb2faa2b2d. The Android API 30 x86_64 Mono smoke test requires ANDROID_DMD_BACKEND DynamicMethod and ANDROID_PRIVATE_ACCESS_OK after the private-call/field/generic/original/GC/removal checks, as well as deferred bootstrap, native I/O, stable display and a presented sprite (6384/8192 center pixels). [Foundation workflow 42362940471](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42362940471) passed all jobs. The earlier cc326e5 foundation diagnostic timeout also passed when its unchanged startup job was retried. Local bridge compilation against the pinned Core/Utils assemblies succeeded without warnings/errors.

Private 0.2.8 delivery: 51,676,757 bytes; SHA-256 `77396eddfa9397e64887559afcd4438d50ff4ed4c05eca6d068d6478c679ea6b`. ZIP CRC and unique-entry checks, ARM64/16 KiB native alignment and APK v2/v3 signature checks passed. It uses the existing certificate and byte-identical private game payload. Android ARM64 private Terraria startup, visual stability and gameplay FPS still require phone validation after installing this update.


## Match the actual PC minimum and stop resize feedback — 0.2.9

The phone confirmed 0.2.8 entered the game/world and improved performance, but both menu and world flicker. Its log tail is flooded with Resolution changed to: 1416x720. The host selected 1416×640. Inspection of the exact supplied game's Main IL confirms minScreenW=800 and minScreenH=720. SetDisplayMode first reads the manager's preferred height (640), clamps its requested height to the game minimum (720), compares those sizes, and enters its changed-resolution path. That path changes screenHeight, applies manager preferences, initializes lighting, invokes resolution events and remakes render targets. FNA's mobile policy changes the preference back to 640 even if it avoids a native device reset, so the next call repeats the game-side work. Avoiding FNA device resets alone did not guarantee game-side convergence.

MobileRenderPolicy now always selects a 720px height and a landscape width bounded by 1600×720 pixels. On the 2400×1080 phone that selects 1600×720 with the same aspect. It has more pixels than the invalid 1416×640 policy, but removes the known clamp conflict; it still has fewer pixels than the earlier 1920×864 mode. No FPS improvement is measured from that arithmetic. Before binding SetDisplayMode, the deferred game hook checks the actual private Main min/max fields and logs ANDROID_GAME_DISPLAY_LIMITS; a conflicting policy fails explicitly instead of entering a resize loop.

Seven new local shared-core checks reproduce the old 1416×640 -> 1416×720 divergence, confirm the phone's 1600×720 selection, and require selected dimensions to survive the pinned game's minimum clamp across display sizes/orientations. The shared suite now has 43 checks. The real FNA probe applies 120 consecutive requests after that 800×720 clamp and requires its backbuffer, manager preferences and game request to remain identical with zero device resets. The Android smoke test requires ANDROID_PC_RESOLUTION_STABLE in addition to prior hook, lifecycle, native I/O, display and presented-pixel checks. The private game still needs another ARM64 run to establish whether any independent visual defect remains.

Validation: [SDL host workflow 42364770081](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42364770081) passed for code 22dc7cbff66d71b056ce0fe844e2418dc89821f7. It built and checked ARM64/x86_64 hosts, required ANDROID_PC_RESOLUTION_STABLE after 120 requests, and passed the existing native-I/O/private-hook/GC/lifecycle/stable-display checks and Android API 30 presented sprite check (5512/8192 center pixels). [Foundation workflow 42364770140](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42364770140) passed all jobs. Locally, all 43 shared and 23 FNA integration checks passed.

Private 0.2.9 delivery: 51,676,757 bytes; SHA-256 `adb4166fbbad25c2dffe798d1760f045305deca85edb7bfc82db9eb82c60a159`. ZIP CRC/entry uniqueness, native ARM64/16 KiB alignment and APK v2/v3 signatures passed. The existing certificate and private managed game payload are preserved. Install over the existing app, test both menu and an existing world, and collect the report's performance section. Removal of all phone flicker and resulting gameplay FPS are still unverified on the actual device.


## GPU streaming and composition validation — 0.2.10

The 0.2.9 phone report confirms that the resize feedback is gone (1600×720, only two avoided resets), but the screenshot and user report still show black/clipped scene regions, displaced/flickering sprites and a black menu. The screenshot has the same landscape aspect as the selected backbuffer; the corrupted scene regions alone do not establish a window-aspect bug. Timings retain about 600 samples: presentation calls roughly 51–58/s, draw p95 around 19–21ms and update p95 around 5–7ms. The approximately 220MB allocated counter is per retained window, not per frame or proof of a leak. These CPU-side timings are not GPU timer queries.

Source inspection shows that mapped NoOverwrite uploads return without uploading data if glMapBufferRange returns null, and that base-vertex support was still allowed on GLES 3.2. No phone GL error or null-map result has yet confirmed either as the remaining fault. The candidate therefore uses FNA3D's existing conservative Android GLES paths: supports_ARB_map_buffer_range=false and supports_ARB_draw_elements_base_vertex=false. SpriteBatch observes NoOverwrite support as false and uses the established discard/subdata path; drawing uses attribute offsets. GLES also explicitly uses glClearDepthf/glDepthRangef instead of desktop double-precision depth entry points. No shader, texture, game asset or Terraria game binary is replaced. This may change upload/presentation cost and must be measured on the actual device.

The probe first changes the viewport depth range and clear depth to exercise those GLES depth calls and check GL errors. The new GPU probe uses three distinct one-pixel textures, 3072 sprites across 64 Begin/End cycles and frequent texture changes/nonzero draw offsets. It draws a changing grid to a 96×64 target, copies that to an inset region of a 128×96 target, reads the result and verifies 1536 color cells plus black borders. It restores the previous targets, viewport, scissor, render states, sampler and texture before disposing its resources. The private game runs the same fixture on its initial graphics-device creation before loading game content. The test runs once at startup, not per gameplay frame.

A bounded monitor samples glGetError after every 120 completed presentations on the SDL rendering thread and logs at most eight ANDROID_GL_FRAME_ERROR messages. It does not inspect GL from a timer/background thread. The Android CI smoke test requires ANDROID_GPU_STRESS_OK and ANDROID_GLES_STREAMING subdata+attribute-offsets, followed by the existing deferred-bootstrap, private-access/GC, PC-resolution-convergence and presented-frame checks. Compilation or an emulator pass cannot certify the Adreno-specific game scene or restore a 60 FPS target by itself.

Validation: [SDL host workflow 42366778607](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42366778607) passed for code 956dc9fb6a131cd7fffc3ad6b08c3cdca3e846c8. Android API 30 x86_64 executed the non-default depth-range/clear check, verified ANDROID_GPU_STRESS_OK after all 1536 cells, required the conservative GLES marker, passed existing detour/GC/bootstrap/resolution/native-file checks, and presented the final FNA sprite (5512/8192 center pixels). [Foundation workflow 42366778610](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42366778610) also passed. Local prepared FNA compilation and 23 FNA adapter checks passed; the host GPU fixture compiled separately against that FNA without warnings/errors.

Private 0.2.10 delivery: 51,680,853 bytes; SHA-256 `84964e91ab93250b6d1065666d52647b1629cd7c40f5d942ce6b6e5b93552763`. ZIP CRC/entry uniqueness, native ARM64/16 KiB alignment and APK v2/v3 signatures passed. It retains the development certificate and byte-identical private game payload. The actual Adreno black-menu/flicker fix and any performance change remain unverified until the next phone test; the new GPU probe/error markers are included in that report.


## Repair game display state and reuse sprite buffers — 0.2.11

The Adreno 0.2.10 report confirms that flicker stopped and the composition probe passed, but game UI remains clipped, black scene regions remain, presentation calls fall to 16–30/s and Android reports LowMemory with PSS 2,529,892 KiB. This is not a measured 4 GiB process. The retained 600-tick allocation counter is overlapping traffic rather than live memory; managed live bytes remain approximately 626–629 MiB.

Inspection of the supplied private Main IL exposes a missed convergence case: SetDisplayMode compares the requested size with GraphicsDeviceManager preferences, not Main.screenWidth/screenHeight. FNA already sets preferences to the mobile size before the original game method executes. If Main still holds its previous dimensions, the original skips screen fields, input dimension caches, FixUIScale, resolution events and target rebuilding. The hook now detects that mismatch with cached typed field readers and primes only the manager preferences with the old game dimensions before calling the original. Its existing changed-resolution path then synchronizes all dependent state. The FNA unchanged-device guard still suppresses a redundant native reset. A bounded ANDROID_GAME_DISPLAY_SYNC marker records before/after dimensions. No per-frame FieldInfo boxing or reflection invocation is added.

The conservative GLES path previously made SupportsNoOverwrite false, causing SpriteBatch.UpdateVertexBuffer to orphan its full dynamic buffer at every Begin/End flush. GLES now advertises logical NoOverwrite while map_buffer_range remains disabled: existing SetVertexBufferData/SetIndexBufferData use glBufferSubData at nonoverlapping offsets and orphan only on ring wrap. Base-vertex remains disabled and single-precision depth remains enabled, preserving the conservative rendering path. The startup multi-texture fixture spans ring capacity and must still verify its pixels; no effect or shader is removed. The marker adds ring-reuse. Improvement on the phone is unmeasured.

Performance reports add bytes per recorded tick, retained tick count, native heap and actual process PSS so allocation traffic is distinguished from resident memory. Android PSS includes shared-memory attribution and does not identify a GPU leak by itself. SDK discovery failures originate in the desktop Mod Sources SDK check and are separate from graphics corruption; an embedded runtime is not an installed dotnet CLI SDK. Neither mobile-native UI completion nor full gameplay compatibility is claimed.
