# tModLoader Android

Dedicated Android ARM64 adaptation of tModLoader: preserve the C# mod ecosystem while adapting the game host, interface, input and performance to mobile. See [architecture and delivery gates](docs/ANDROID_PORT.md).

> **Active work:** Experimental SDL3 Android game host (0.2.0). Native ARM64 SDL3/FNA3D/FAudio, a real FNA graphics probe, transparent controls, PC content import and private game entry wiring now build. Reaching Terraria menu/world on-device remains unvalidated. [Host build and verification scope](docs/SDL_HOST.md).
>
> **Historical game baseline:** Phase 2.23.01 — FNA3D SpriteBatch Buffer Bridge. Its gameplay host still uses MonoVM 8. Matching vanilla PC input was received privately on 2026-11-06; the new .NET 10 host remains to be integrated.

## Dedicated Android direction

The selected direction is a Terraria-specific Android port. The original 0.1.1 APK remains the validated controls/runtime diagnostic; a separate 0.2.0 SDL3 host is now built ([details](docs/SDL_HOST.md)). the matching vanilla PC 1.4.5.8 input has now passed [archive/basic content validation](docs/PC_INPUT_VALIDATION.md). The [private managed game candidate](docs/GAME_BUILD.md) now builds in .NET 10 against mobile FNA and passes assembly/type checks. SDL3/FNA Android host wiring and native ARM64 builds are now implemented; game startup still needs runtime validation. Optional FNA phase timing and bounded reports are now prepared for that first playable baseline. See [PERFORMANCE.md](docs/PERFORMANCE.md).

## New .NET 10 mobile source

- `src/Tml.Mobile.Core/` — shared multi-touch state and content/runtime preflight.
- `src/Tml.Mobile.Android/` — original ARM64 input/runtime diagnostic.
- `src/Tml.Game.Android/` — experimental SDL3 Android game host and private PC content importer.
- `integration/native/` — pinned SDL3/FNA3D/FAudio build and managed entry stub.
- `integration/fna/` — direct FNA input integration, with no per-field JNI bridge.
- `tools/prepare_fna.py` — revision-checked patching of the pinned FNA source into a separate build directory.
- `tests/` — gesture, allocation and actual FNA input tests.
- `.github/workflows/mobile-net10.yml` — repeatable APK and FNA checks.

See [build instructions](docs/BUILD.md) and [remaining game integration](docs/NET10_MOBILE.md). Full in-game touch, mobile menus and desktop mod compatibility are objectives still requiring implementation and device tests.

## Historical game progress

The bootstrap already reaches the first controlled game frame and has passed several major startup blockers:

- MonoVM runtime initialization on `android-arm64`;
- tModLoader assembly bootstrap and dependency loading;
- dedicated 32 MiB game-thread stack for the large Terraria static initialization path;
- complete `ContentSamples.Initialize()`;
- FNA graphics device and backbuffer setup;
- render-target creation;
- `FNA3D_CreateTexture2D` through an Android-safe InternalCall bridge;
- `FNA3D_SetTextureData2D` through an Android-safe InternalCall bridge;
- real 1×1 dummy texture creation and 4-byte pixel upload;
- ARM64 tagged-pointer-safe Mono method metadata conversion;
- `SoundEngine.Initialize()` no-device fallback through the existing Terraria/tModLoader audio path;
- asset service creation and registration of PNG/XNB/rawimg/FXC/WAV/MP3/OGG readers;
- successful opening of the real `Content/PixelShader.xnb` payload after it is supplied externally.

Phase 2.22.99 is validated. The FAudio bridge reports zero devices, FNA raises the expected `NoAudioHardwareException`, Terraria catches it in `TestAudioSupport`, and `SoundEngine.Initialize()` returns normally.

The shader content payload is also no longer missing. With the correct XNB files in `Content/`, `TMLContentManager.OpenStream` returns successfully for `PixelShader`.

Phase 2.23.00 and 2.23.01 are validated. Real shader effects are created through the APK FNA3D, and SpriteBatch vertex/index buffers are now allocated and initialized successfully.

The last historical run reached `AssetInitializer.LoadSplashAssets` and failed because its selected content tree lacked `Images/SplashScreens/Splash_1.xnb`. The privately supplied PC Content now contains that file; the historical game has not yet been rerun with it.

The supplied **Terraria PC 1.4.5.8** vanilla `Content` tree must be integrated while keeping tModLoader/platform `Content` files as a separate override layer. This mirrors upstream tModLoader's content-manager design instead of mixing mobile and desktop assets.

## Latest source

The sanitized source snapshot for the current test baseline is:

`source-snapshots/phase2.23.00/tML_Phase2_23_00_FNA3DEffectMetadataBridge_Fontes.zip` (latest published public snapshot; Phase 2.23.01 is the validated runtime baseline)

Its SHA-256 and exact contents/exclusions are documented beside the archive in:

`source-snapshots/phase2.23.00/README.md`

## Repository layout

- `docs/STATUS.md` — exact current state and active blocker.
- `docs/PHASES.md` — history of the diagnostic/fix phases.
- `docs/ARCHITECTURE.md` — MonoVM/bootstrap/FNA bridge architecture.
- `docs/DEBUGGING.md` — current logs and diagnostic commands.
- `docs/BUILD.md` — local build requirements.
- `docs/CONTENT.md` — required Android layout for vanilla Terraria PC content and tModLoader overrides.
- `docs/ROADMAP.md` — milestones from first frame to menu/mod/world support.
- `source-snapshots/` — sanitized source-only snapshots for important baselines.
- `SECURITY.md` — signing-key and runtime-payload policy.

## Important

This repository intentionally does **not** contain signing private keys, proprietary game/runtime payloads, personal save data or generated APK binaries. Those stay outside Git.

The build expects locally supplied Android/FNA and game content payload files described in `docs/BUILD.md`.

## Upstream projects

This work is built around the tModLoader, FNA, FNA3D, FAudio and SDL2 ecosystems. Their upstream licenses and terms continue to apply to their respective source and binaries.

## Development model

`main` tracks the latest working/diagnostic baseline.

As the Android port advances, each new phase should update:

1. `docs/STATUS.md`;
2. `docs/PHASES.md`;
3. the latest sanitized source snapshot when source changes materially;
4. debugging/build notes when the workflow changes.

The objective is to keep the repository useful as both the current source-of-truth and a readable record of how the Android bootstrap reached each milestone.
