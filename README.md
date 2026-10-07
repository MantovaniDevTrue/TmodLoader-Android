# tModLoader Android

Experimental Android ARM64 bootstrap for running the desktop tModLoader/FNA stack through MonoVM on Android.

> **Status:** work in progress. The current development baseline is **Phase 2.22.98 — Post-Texture LoadContent Isolation**.

## Current progress

The bootstrap currently reaches the first controlled game frame and has already passed several early startup blockers:

- MonoVM runtime initialization on `android-arm64`;
- tModLoader assembly bootstrap and dependency loading;
- 32 MiB dedicated game-thread stack to finish the large Terraria static initialization path;
- complete `ContentSamples.Initialize()`;
- FNA graphics device creation;
- render-target creation;
- `FNA3D_CreateTexture2D` through an Android-safe InternalCall bridge;
- `FNA3D_SetTextureData2D` through an Android-safe InternalCall bridge;
- ARM64 tagged-pointer-safe metadata conversion from P/Invoke to InternalCall.

The active investigation is now inside the post-texture `Main.LoadContent` path: asset services/readers, audio initialization, asset source setup and `ModLoader.PrepareAssets()`.

## Repository layout

- `src/bootstrap/` — native MonoVM bootstrap and FNA/FNA3D bridge code.
- `android/` — Android manifest and small native build stubs used by the bootstrap build.
- `scripts/` — build and verification scripts.
- `docs/` — architecture, phase history, current status, testing and debugging notes.

## Important

This repository intentionally does **not** contain signing private keys, local runtime payloads, proprietary game files or generated APK binaries. Those stay outside Git. APKs are build artifacts, not source.

The build expects locally supplied Android/FNA payload files described in `docs/BUILD.md`.

## Upstream projects

This work is based around the tModLoader, FNA, FNA3D and SDL2 ecosystems. Their own licenses and upstream project terms continue to apply to their respective code and binaries.

## Development model

`main` tracks the latest working/diagnostic baseline. Each new phase is documented in `docs/PHASES.md`, and the source is updated as the Android bootstrap advances.
