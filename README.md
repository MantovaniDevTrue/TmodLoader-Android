# tModLoader Android

Experimental Android ARM64 bootstrap for running the desktop tModLoader/FNA stack through MonoVM on Android.

> **Status:** work in progress. Latest validated baseline: **Phase 2.23.01 — FNA3D SpriteBatch Buffer Bridge**.

## Current progress

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

The active startup blocker is no longer native graphics. `Main.LoadContent` reaches `AssetInitializer.LoadSplashAssets` and fails because the selected vanilla content tree does not contain `Images/SplashScreens/Splash_1.xnb`.

The port therefore now needs the complete matching **Terraria PC** vanilla `Content` tree, while keeping the tModLoader/platform `Content` files as a separate override layer. This mirrors upstream tModLoader's content-manager design instead of mixing mobile and desktop assets.

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
