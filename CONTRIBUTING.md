# Contributing to tModLoader Android

Thanks for helping improve this **unofficial, experimental** Android ARM64 port.

## Before opening an issue

1. Check the [current status](docs/STATUS.md), [roadmap](docs/ROADMAP.md) and existing issues.
2. Reproduce on the latest **validated build** if available. A source-only version label is not evidence of a playable APK.
3. Provide the tML Android version, Android version, device/SoC, renderer (OpenGL or experimental Vulkan), installed mods and reproduction steps.
4. Include the smallest relevant, **sanitized** diagnostic excerpt. Remove account details, personal file paths, tokens, game ownership data and other private information.
5. When reporting performance, include scene/world, resolution, active mods, thermals if known, and separate frame-time percentiles from presentation rates. Prefer same-scene before/after comparisons.

The repository cannot diagnose every desktop-only mod. Start with the small [AndroidSwordTest](examples/AndroidSwordTest/) validation mod when possible.

## Pull requests

- Make focused changes; explain the Android failure they fix and any impact on tModLoader mod behavior.
- Preserve vanilla game logic, mod APIs, hook order, save ownership and third-party license notices.
- Do not silently disable visual effects, bypass mods, force full garbage collections per frame or remove input paths to obtain synthetic benchmarks.
- Add or update tests for changes to input, FNA integration, native libraries and build tools.
- Update the current status/release notes only with evidence from the corresponding build and device.
- Run the relevant checks in [GitHub Actions](.github/workflows/) and disclose what **was not** tested.

## Distribution and privacy

**Do not commit or attach** Terraria executables, proprietary assets, privately generated game payloads, APKs containing such payloads, signing certificates, private keys, save files or personal logs. The build instructions explain how developers supply their own files outside Git.

The third-party projects retain their respective rights and licenses; see [credits](THIRD_PARTY.md). Do not assume the original Terraria license permits redistribution of game content.

## Current priorities

See the [native mobile UI roadmap](docs/NATIVE_MOBILE_UX.md) and [performance measurements](docs/PERFORMANCE.md). Current high-impact work includes inventory/crafting touch behavior, rendering sub-pass profiling, GPU memory auditing, app lifecycle, and compatibility checks with increasingly complex mods.
