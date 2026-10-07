# Build notes

The current build is intentionally lightweight and does not require Gradle for every native bootstrap iteration.

## Local requirements

- Python 3
- `cryptography` Python package
- Clang/LLD with an Android ARM64 target
- locally supplied APK base payload
- local signing key/certificate

## Expected local payload

The current build script expects an `apk_base/` directory containing:

- `AndroidManifest.xml`
- `classes.dex`
- `resources.arsc`
- `lib/arm64-v8a/libFNA3D.so`
- `lib/arm64-v8a/libSDL2.so`

These binary/runtime files are intentionally not committed.

## Native output

The bootstrap is compiled as `libmain.so` with:

- target: `aarch64-linux-android24`
- LLD
- 16 KiB maximum/common page size
- no dependency on Android system headers during the tiny bootstrap build

## APK requirements

Generated APKs are verified for:

- Android ARM64 native ELF;
- no text relocations;
- 16 KiB native-library ZIP alignment;
- APK Signature Scheme v2;
- content digest consistency.

## Signing

Keep the private signing key outside Git. The historical local build script used a disposable test key next to the script; the public repository must instead use a local path or environment-specific signing setup.

## Source snapshot

The exact public source snapshot for the active baseline is stored under `source-snapshots/`. It excludes:

- APK/game binaries;
- runtime payloads;
- signing private keys.
