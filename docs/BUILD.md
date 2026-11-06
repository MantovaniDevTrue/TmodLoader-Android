# Build notes

## .NET 10 mobile foundation (active track)

Requirements: .NET SDK 10.0.100 or a compatible stable .NET 10 feature band, .NET Android workload set 10.0.100 (Android SDK pack 36.1.2), JDK 21, Android SDK platform 36 and build-tools 36.0.0. Initial minimum Android version: 8.0/API 26. Only `android-arm64` is packaged.

```bash
dotnet workload install android --version 10.0.100
bash tools/build_mobile.sh -p:AndroidSdkDirectory=/path/to/android-sdk -p:JavaSdkDirectory=/path/to/jdk21
```

Output: `artifacts/mobile/tML_Android_0.1.1_Controles_NET10_ARM64.apk`. This is an input/runtime diagnostic app, not a playable game. The build script generates its separate test DLL, runs the shared input checks, signs the APK with the local SDK test identity and checks native ABI/page alignment. Keep signing keys outside Git. Run `apksigner verify --verbose` separately to validate signatures.

Core tests do not require Android:

```bash
dotnet build tools/Tml.RuntimeProbe -c Release -o artifacts/probe
dotnet run --project tests/Tml.Mobile.Tests -c Release -- "$PWD/artifacts/probe/Tml.RuntimeProbe.dll"
```

To compile/test the real FNA integration, clone the FNA revision in `upstream.lock.json`, initialize its `lib/SDL2-CS`, `lib/SDL3-CS`, `lib/FAudio` and `lib/Theorafile` submodules, then:

```bash
python3 tools/prepare_fna.py /path/to/clean-fna /path/to/new-fna-mobile
dotnet build /path/to/new-fna-mobile/FNA.Core.csproj -c Release
dotnet run --project tests/Tml.Fna.Tests -c Release -p:FnaAssembly=/path/to/new-fna-mobile/bin/Release/net10.0/FNA.dll
```

The source-only workflow needs no Terraria payload. Starting the actual game requires the separate host migration and privately supplied game/content dependencies described in [NET10_MOBILE.md](NET10_MOBILE.md).

## Historical MonoVM 8 bootstrap


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
