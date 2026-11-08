# Android 0.2.13 playable package — 2026-11-07

The private playable APK was assembled from the validated public ARM64 host and
the managed game payload, including a freshly built `Tml.GameIntegration.dll`.
The user's imported Terraria PC content remains required. The public
`android-source-host` artifact by itself is still a probe/launcher package.

- File: `tML_Android_0.2.13_SDL3_NET10_ARM64.apk`
- Size: 51373677 bytes
- APK SHA-256: `7fcc528998f66254602cf9dfad7690c4d4fa4c04cbd7003461c529439a7011ca`
- Managed payload SHA-256: `54629d14f56a2a7bfd02f18a2ff4c1ac42d587a8f07165aa3b3c87f35b92af0c`
- Source snapshot: `f7a8db3ae265d18d32ef30a76b1ac8ae227ab944`
- Host code/native build: `d22f7f8e14a250268cf9fbfd5a97c86f3e6f29d9`
- Host artifact: `13583820623` from [workflow 42396149435](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42396149435).

The source snapshot differs from the host build only in documentation. The
managed integration was built against the pinned, patched FNA and the existing
.NET 10 private tModLoader assembly, without replacing the host's FNA/core in the
embedded payload. No Terraria PC assets or private signing material are published
in this repository.

## Completed checks

- Mobile core tests, including aim ownership and local mod import/update failure handling.
- Actual FNA input tests, including per-scene performance capture.
- Host regression suite, including 600 rewritten frames skipping forced GC calls.
- Private assembly/API checks with the actual game DLLs.
- Headless private integration hook installation, options touch interception,
  control dragging and persisted touch profile.
- APK v2/v3 signatures verified with the same certificate as the prior playable
  packages (SHA-256 `32b2a1c7810245f6ee788de4a1e9452f35b049665984f71fdea0d68f217823b4`).
- All packaged native libraries verified as AArch64 ELF with 16 KiB-compatible
  load segments; uncompressed library ZIP offsets checked.
- ZIP CRCs, duplicate entries, exact embedded payload and integration DLL checked.

## Device validation still needed

The private game loop was not run on an ARM64 phone during packaging. The earlier
public emulator OpenGL/Vulkan pixel/touch checks do not establish world FPS,
phone Vulkan stability, long-session memory behavior, or general mod compatibility.
Start with the no-mod baseline. The 0.2.13 reports retain World/WorldUi measurements
separately from Menu/Saving so menu FPS cannot substitute for gameplay evidence.
