# Android 0.2.14 — mod metadata resolution

The first AndroidSwordTest attempt failed during MetadataLoadContext core assembly
resolution, before mod content registration. The original metadata resolver treats
any runtime assembly with an empty Location as belonging to a ModLoadContext.
Android bundles framework/host assemblies with empty Location in the default
context too, so that cast is invalid. The screenshot lacks the exception header;
the exact branch is reproduced separately with an in-memory host assembly.

## Fix

A scoped hook on the game's private MetadataResolver.Resolve supplies metadata-only
PE images for framework and bundled host assemblies. The existing packaged
Basic.Reference.Assemblies.Net100 embedded PE resources supply framework metadata;
the game receives copies of its matching FNA/core PE images under game/Metadata.
Other packaged library images come from the existing private Libraries tree.
The original resolver still handles mod assemblies and inter-mod dependencies.
No new runtime core assembly is loaded or substituted, no mod types are skipped,
and no SDK/Roslyn compiler is required on the phone.

`tools/stage_metadata.py` stages host metadata images during the private game build.
The private API checker verifies they match the runtime build inputs. The normal
payload packager includes these files. The Android payload marker and app version
are bumped to 0.2.14 so updates refresh the private integration and images.

## Regression evidence

`tools/Tml.ModLoadProbe` loads FNA/core from streams into the default CoreCLR context,
asserts empty Location, and invokes the actual original game metadata resolver.
That reproduces InvalidCastException. It then installs the patch and calls the
actual ModLoadContext.LoadAssemblies/GetLoadableTypes path with AndroidSwordTest.
All five mod types are discovered, including ExampleSword. The real private touch
hook/editor probe and assembly contracts also pass with the new integration.

The diagnostic differs from full Android content/gameplay loading. Physical phone
validation is required after installation. Keep AndroidSwordTest 0.1.0 unchanged;
the correction is in the loader. Do not infer FPS improvements from this fix.

Reproduce privately after staging the matching game/FNA/core and integration:

```
python3 tools/stage_metadata.py /private/game
dotnet run --project tools/Tml.ModLoadProbe -c Release -- /private/game /private/AndroidSwordTest_0.1.0.tmod
```

## Packaged candidate verification

[Android SDL workflow 42410952417](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42410952417)
passed all steps for source commit `64889267380de00f668a360e728930d6c9d1d3e3`,
including ARM64 packaging and both OpenGL/Vulkan emulator pixel/touch fixtures.
Foundation workflow 42410952516 also passed all three jobs.

The private playable APK includes the new integration and both required host
metadata PE images. Its ZIP contents/CRCs, v2/v3 signatures, prior-certificate
continuity and AArch64/16 KiB compatibility checks passed.

- File: `tML_Android_0.2.14_SDL3_NET10_ARM64.apk`
- Size: 51,738,221 bytes
- APK SHA-256: `2afa6f205613883ab5bb3933ddd60359ebb10d3ca03eb54680d85bb556f7ea1d`
- Payload SHA-256: `50c8e191700b92e6c8d7aa70ab29984e4ddc2a139288e72874f2897ee401f7c6`
- Source-host artifact: 13589353023

Install over 0.2.13 and re-enable AndroidSwordTest 0.1.0 if the earlier failure
disabled it. Full mod content/gameplay on the physical phone remains pending.

## Physical ARM64 phone validation — 2026-11-07, 18:01–18:07 BRT

The user tested 0.2.14 with OpenGL on Adreno 642L and confirmed the sword appeared
in the world. AndroidSwordTest 0.1.1 was selected over 0.1.0; it was enabled,
isolated, JIT-compiled (39 ms), loaded, configured and finalized. Its Load log
was emitted and mod loading completed in 2,579 ms. The supplied screenshot shows
the localized item/tooltip and a Demonic prefix (58 displayed damage).
This validates the simple local precompiled .tmod path, metadata resolution,
client resources, item registration and recipe availability on the phone. It does
not establish that every hook/effect or larger PC mod is compatible.

The session entered a 4200×1200 world and saved it (SaveWorld 3,467 ms), then
returned through TML_LAUNCH_RETURNED / MANAGED_SDL_EXIT with no new crash in this
trace. The mod's 16.7 KB RAM figure is the loader's attribution, not its complete
process-memory overhead. The 28 ms immediate asset-loading warning was during
mod startup and is not evidence of a per-frame cost.

Only WorldUi samples are present in the supplied, truncated gameplay performance
section: about 46–51 presentation calls/s, draw p95 22–24 ms, update p95 3–4.4 ms,
and process PSS about 2.36 GiB. These cannot substitute for closed-inventory World
samples or isolate the sword's performance cost. Vulkan was not tested on-device.

Split 1.0.11 was discovered, but has a different Terraria/LTS build stream
(tML 2026.4.3.0). It was not loaded in the recorded mod-content sequence. This
report does not validate Split compatibility; do not bypass that version boundary.

## Physical Adreno Vulkan test failed — 2026-11-07, 18:23–18:24 BRT

The user selected Vulkan/SDLGPU on 0.2.14. Native logging confirms the actual
Vulkan device was Adreno 642L (Qualcomm driver build 4bbe300fc3, conformance 1.2.0.1).
The small GPU fixture passed, content initialization completed in 1,046 ms and
music initialization in 77 ms. After the ReLogic splash the user observed a black
screen and eventual process termination. No mod discovery/content load or world
entry is present in this session's console trace.

SDL reports unsupported opaque presentation repeatedly. In the pinned SDL
release-3.4.20 gpu/vulkan backend, this warning means the surface capability mask
lacks VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR; swapchain creation then chooses another
supported composite-alpha mode (prefer inherit when available). It identifies a
presentation/compositor compatibility lead, not a demonstrated cause of either
the black screen or process termination. Missing VK_EXT_debug_utils is also a
warning, not an established fatal error.

Android records Signaled(2), status=9 at 21:24:45 UTC: SIGKILL. There is no SIGSEGV,
managed fatal exception or device-lost error in the supplied record. The actor and
reason for the kill cannot be determined here; historical LowMemory/MIUI events
must not be substituted for the cause of this run.

The 1.72 GB allocation value is cumulative transient allocation within the startup
window, not resident RAM. Sampled managed live memory was 251–290 MiB and process
PSS about 1.57 GiB during the later recorded startup samples. Menu is a game-state
label that also covers startup, so the later 55 presentation calls/s does not prove
a visible/working menu. No Vulkan gameplay or performance improvement is validated.

Use OpenGL for the current phone baseline. Keep Vulkan experimental and investigate
swapchain alpha/composition plus full-game rendering beyond the small pixel fixture
before recommending another on-device comparison.
