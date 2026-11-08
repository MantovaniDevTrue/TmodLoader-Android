# tModLoader Android

An unofficial Android ARM64 port of [tModLoader](https://github.com/tModLoader/tModLoader), built on .NET 10, SDL3 and FNA. It runs the PC tModLoader game and C# mod API on the phone, with a mobile platform layer for touch, display, lifecycle and performance.

> **Status:** experimental. Version **0.2.17** enters, plays and saves a world on a physical ARM64 phone with a local mod loaded. **0.2.18** (touch-first inventory) is staged in source and not yet packaged. See [current status](docs/STATUS.md).

This is not an official tModLoader or Re-Logic release. The repository contains no Terraria code or assets: players supply their own Terraria PC 1.4.5.8 installation, which the launcher imports.

## Highlights

- **Same game, same mods.** The pinned upstream tModLoader compiles for .NET 10 with Mono JIT, so dynamic assembly loading and `Reflection.Emit` keep working and `.tmod` packages load unchanged.
- **Native touch input.** SDL touch events feed a shared router that writes directly into FNA's `KeyboardState` and `MouseState`, so the game sees normal input. An in-game editor moves, resizes and hides controls.
- **Launcher.** Imports the PC installation and local `.tmod` files through Android's document picker, toggles mods, and exports diagnostic reports. Available in Portuguese, English and Spanish.
- **Measured performance.** Optional FNA timing, memory and allocation reports, plus allocation-free tile drawing and content-decoding paths that keep every visual effect.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/Tml.Game.Android` | SDL3 game host, launcher, Android hooks and diagnostics |
| `src/Tml.GameIntegration` | Mobile integration compiled against the private game build |
| `src/Tml.Mobile.Core` | Shared multi-touch routing and content/runtime preflight |
| `src/Tml.Mobile.Android` | Standalone input/runtime diagnostic app |
| `integration/fna` | Mobile extensions patched into the pinned FNA source |
| `integration/native` | SDL3, FNA3D and FAudio native build and entry stub |
| `integration/game` | MSBuild targets for the game build |
| `tests/` | Touch, FNA, host and performance regression tests |
| `tools/` | Build, packaging, verification and probe tools |
| `examples/AndroidSwordTest` | Minimal mod used for on-device testing |
| `upstream.lock.json` | Pinned tModLoader, FNA and SDL revisions |

## Building

Requirements: .NET SDK 10.0.100 with the Android workload, JDK 21, Android SDK platform 36 and build-tools 36.0.0, NDK for native libraries.

```bash
dotnet workload install android --version 10.0.100
bash tools/build_mobile.sh -p:AndroidSdkDirectory=/path/to/android-sdk -p:JavaSdkDirectory=/path/to/jdk21
```

This builds the input/runtime diagnostic APK. The SDL3 game host is built by `.github/workflows/sdl-host.yml` without game files. A playable APK also needs the privately built game payload; see [build](docs/BUILD.md), [SDL3 host](docs/SDL_HOST.md) and [private game build](docs/GAME_BUILD.md).

## Documentation

- [Status](docs/STATUS.md): what works and what is next
- [Changelog](CHANGELOG.md), with detailed [release notes](docs/releases/)
- [Port architecture and delivery gates](docs/ANDROID_PORT.md)
- [Roadmap](docs/ROADMAP.md)
- [SDL3 host](docs/SDL_HOST.md), [touch controls](docs/MOBILE_TOUCH.md) and [mobile UI direction](docs/NATIVE_MOBILE_UX.md)
- [Performance capture](docs/PERFORMANCE.md)

## Credits and security

Upstream projects and licenses are listed in [THIRD_PARTY.md](THIRD_PARTY.md). Signing keys, game binaries, APKs and saves never go into Git; see [SECURITY.md](SECURITY.md).
