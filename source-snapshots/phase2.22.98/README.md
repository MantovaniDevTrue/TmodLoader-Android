# Phase 2.22.98 — Post-Texture LoadContent Isolation

This directory contains the sanitized source snapshot for the current Android bootstrap baseline.

## Snapshot

`tML_Android_Phase2_22_98_public_source.zip`

SHA-256:

`d284c3c29515d4c84e38c52e3da4f1795673b6119564b72b9e04e28ca572c867`

## Included

- `bootstrap.c`
- `graphics_binding.inc`
- `build_phase2_22_98.py`
- `verify_apk.py`
- `verify_native.py`
- phase notes
- Android binary manifest used by the local APK base
- tiny native stub source files used by the bootstrap build

## Excluded intentionally

- APK signing private key
- signing certificate used only by the local test build
- generated APK
- `classes.dex`
- `resources.arsc`
- prebuilt SDL2/FNA3D/runtime binaries
- Terraria/tModLoader game payloads
- local logs and save data

The excluded runtime/build payloads must be supplied locally. See `docs/BUILD.md`.

## What this phase is testing

The previous phase successfully:

1. created the tModLoader/FNA dummy `Texture2D(1, 1)`;
2. routed `FNA3D_CreateTexture2D` through the Android InternalCall bridge;
3. routed `FNA3D_SetTextureData2D` through the Android InternalCall bridge;
4. uploaded the four-byte transparent pixel successfully.

Phase 2.22.98 isolates the next blocker inside the post-texture `Terraria.Main.LoadContent` sequence.
