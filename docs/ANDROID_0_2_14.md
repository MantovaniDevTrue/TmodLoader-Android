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
