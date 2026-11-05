# Phase 3.0 validation — 2026-11-05

## Completed locally

| Check | Result |
| --- | --- |
| .NET SDK | 10.0.100; runtime 10.0.0 |
| Android workload | 10.0.100 set; Microsoft.Android 36.1.2 |
| Android target | SDK 36, minimum API 26, ARM64 only |
| Shared touch/runtime checks | 22 passed |
| Integration against actual pinned FNA | Build passed; 11 input checks passed |
| Warmed core allocation sample | 0 bytes across 10,000 cycles |
| Warmed FNA input merge sample | 0 bytes across 10,000 cycles |
| Android diagnostic APK | Release build and test signing completed |
| Packaged native libraries | 10 AArch64 ELF64 libraries |
| Native ELF alignment | Every LOAD segment supports 16 KiB pages |
| Uncompressed native ZIP entries | 16 KiB aligned |
| APK signing | apksigner verifies v2 and v3 signatures |
| Source checks | git diff --check, Python compilation, shell syntax and workflow YAML parsing passed |

The local JDK 21.0.13.0.2 emits Android tooling warning XA0033 because this older workload cannot parse its five-component version string. Compilation/signing succeeded. CI selects Temurin 21. The upstream FNA build has its existing compiler/analyzer warnings; no source changes were made to suppress them.

## Artifact

`tML_Android_0.1.0_Controles_NET10_ARM64.apk`

SHA-256: `f12e4901a85661d57de08da76275a9795e5b3c194443d57aa19e779f9ca614d8`

Size: 20,821,307 bytes.

## Not validated

- Installing/running this APK on an Android device (none attached).
- Dynamic DLL loading or Reflection.Emit on Android; these are tested at startup by the diagnostic app, but only desktop execution has been observed here.
- Real SDL Android game surface creation, MonoMod detours, Terraria startup/worlds/audio and `.tmod` loading under the new host.
- In-game touch controls, mobile inventory/menu adaptation, mod keybind UI, GPU context recovery or Android performance/FPS.
- GitHub Actions execution; the workflow is committed for subsequent runs, while the listed results come from the local build environment.

This APK contains the input/runtime diagnostic, not the game. The previous game baseline and partial source snapshots are preserved separately.
