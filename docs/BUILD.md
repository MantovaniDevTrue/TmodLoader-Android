# Build notes

## Diagnostic app and tests

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

The source-only workflow needs no Terraria payload. Running the actual game requires the SDL3 host ([SDL_HOST.md](SDL_HOST.md)) and the privately built game payload ([GAME_BUILD.md](GAME_BUILD.md)).

## Performance collector checks

```bash
dotnet run --project tests/Tml.Performance.Tests -c Release
```

These checks use synthetic timing samples to validate reports, not to measure Terraria FPS. The FNA test command above additionally checks the opt-in timing adapter compiled inside actual FNA. See [PERFORMANCE.md](PERFORMANCE.md).

## Private managed game candidate

The pinned upstream tModLoader now compiles in .NET 10 against the mobile FNA extension. Use `tools/build_game.py` after preparing private source with upstream setup; see [GAME_BUILD.md](GAME_BUILD.md) for the tested commands, Linux reference-assembly requirements and exact verification limits. This is not an Android APK build.
