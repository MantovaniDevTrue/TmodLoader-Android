# Phase 2.22.99 — FAudio No-Device Fallback

This directory contains the sanitized source snapshot for the Phase 2.22.99 Android bootstrap baseline.

## Snapshot

`tML_Phase2_22_99_FAudioNoDeviceFallback_Fontes.zip`

SHA-256:

`8f564af658d5f5af4a8fcb1c55dc70531376c26344f2ff411b7026012f0dccd8`

## Discovery from Phase 2.22.98

After the verified 1×1 texture allocation/upload, the next managed stage is:

`Terraria.Audio.SoundEngine.Initialize()`

The focused trace records first-chance exceptions:

`System.DllNotFoundException: libFAudio.so`

The upstream Terraria/FNA audio path is designed to fall back to a disabled audio system when audio support cannot be created. Phase 2.22.99 therefore tests that intended path without shipping a random or mismatched FAudio binary.

## Compatibility change

Before first JIT, the bootstrap converts only these FAudio probe methods from P/Invoke to InternalCall:

- `FAudioCreate`
- `FAudio_GetDeviceCount`
- `FAudio_Release`

The Android compatibility bridge reports a valid synthetic probe context with **zero audio devices**. That should make the existing Terraria/tModLoader logic choose `DisabledAudioSystem` and continue `Main.LoadContent`.

This is deliberately a temporary startup compatibility path. It does not implement real Android audio.

## Preserved fixes

- 32 MiB game-thread stack;
- ARM64 tagged-pointer-safe Mono metadata writes;
- `FNA3D_CreateTexture2D` InternalCall bridge;
- `FNA3D_SetTextureData2D` InternalCall bridge;
- previous SDL/FNA3D Android bridges.

## Included

- `bootstrap.c`
- `graphics_binding.inc`
- `build_phase2_22_99.py`
- `verify_apk.py`
- `verify_native.py`
- `LEIA-ME.txt`
- Android manifest
- tiny native stub source files

## Excluded intentionally

- signing private key/certificate;
- generated APK;
- `classes.dex`;
- `resources.arsc`;
- prebuilt SDL/FNA3D/runtime binaries;
- Terraria/tModLoader game payloads;
- personal logs and save data.
