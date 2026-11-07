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
