# SDK-managed SDL3 Android host

The new `src/Tml.Game.Android` project builds an experimental .NET 10 Android host. It is separate from the original input diagnostic and uses the same application ID with version 0.2.4. The Java SDL activity, native entry stub, C# game host, mobile core, FNA and private game payload share one process and one SDK-managed Mono runtime.

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
