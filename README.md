# tModLoader Android

Experimental Android ARM64 bootstrap for running the desktop tModLoader/FNA stack through MonoVM on Android.

> **Status:** work in progress. Current baseline: **Phase 2.22.98 — Post-Texture LoadContent Isolation**.

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
- ARM64 tagged-pointer-safe Mono method metadata conversion.

The current investigation starts immediately after the successful tModLoader/FNA 1×1 dummy texture upload, inside `Terraria.Main.LoadContent`.

## Latest source

The sanitized source snapshot for the active baseline is:

`source-snapshots/phase2.22.98/tML_Android_Phase2_22_98_public_source.zip`

Its SHA-256 and exact contents/exclusions are documented beside the archive in:

`source-snapshots/phase2.22.98/README.md`

## Repository layout

- `docs/STATUS.md` — exact current state and active blocker.
- `docs/PHASES.md` — history of the diagnostic/fix phases.
- `docs/ARCHITECTURE.md` — MonoVM/bootstrap/FNA bridge architecture.
- `docs/DEBUGGING.md` — current logs and diagnostic commands.
- `docs/BUILD.md` — local build requirements.
- `docs/ROADMAP.md` — milestones from first frame to menu/mod/world support.
- `source-snapshots/` — sanitized source-only snapshots for important baselines.
- `SECURITY.md` — signing-key and runtime-payload policy.

## Important

This repository intentionally does **not** contain signing private keys, proprietary game/runtime payloads, personal save data or generated APK binaries. Those stay outside Git.

The build expects locally supplied Android/FNA payload files described in `docs/BUILD.md`.

## Upstream projects

This work is built around the tModLoader, FNA, FNA3D and SDL2 ecosystems. Their upstream licenses and terms continue to apply to their respective source and binaries.

## Development model

`main` tracks the latest working/diagnostic baseline.

As the Android port advances, each new phase should update:

1. `docs/STATUS.md`;
2. `docs/PHASES.md`;
3. the latest sanitized source snapshot when source changes materially;
4. debugging/build notes when the workflow changes.

The objective is to keep the repository useful as both the current source-of-truth and a readable record of how the Android bootstrap reached each milestone.
