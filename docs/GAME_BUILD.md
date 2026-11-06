# Private .NET 10 managed game build

## Validated milestone — 2026-11-06

The user's Terraria PC 1.4.5.8 executable was accepted by the pinned upstream setup CLI. Decompilation completed, and all three source patch stages passed in EXACT mode with strict warning handling. The resulting tModLoader source compiled for .NET 10 against the mobile FNA extension and shared core.

The final managed build completed with zero errors and 927 upstream compiler/analyzer warnings. The build is not warning-free. No upstream gameplay code was changed for this milestone.

The private candidate passed assembly loading checks for Terraria.Main, Mod, ModPlayer, ModNPC and ModItem, FNA Game inheritance, the mobile input/timing APIs and a single shared mobile core. This does not run the game loop, exercise MonoMod detours, load a .tmod or validate Android native libraries. No playable APK or device FPS result is claimed.

## Prepare upstream sources

Use a separate, clean checkout of the tModLoader commit in `upstream.lock.json` and initialize its submodules recursively. Generated upstream `src/`, executables, Content, ownership keys and compiled payloads must remain private.

Build the official setup CLI with the .NET 10 SDK:

```bash
dotnet build setup/CLI/Setup.CLI.csproj -c Release -m:1 -nr:false
dotnet setup/CLI/bin/Release/net10.0/setup-cli.dll terraria-version /private/Terraria-PC/Terraria.exe
```

The version must be 1.4.5.8. On this Linux build host, decompilation also needed the official .NET Framework 4.8.1 reference assemblies. Obtain `Microsoft.NETFramework.ReferenceAssemblies.net481` 1.0.3 from NuGet; the tested package SHA-256 is `917848bb273021e5ad42d5cd38cf5e637c52d90760ba43d1b2192ebf7bd6cf44`.

Extract its `build/.NETFramework/v4.8.1/**/*.dll` tree into the upstream `setup/.NETFramework/v4.8.1/` directory. Place that package's actual `mscorlib.dll` beside a private working copy of the supplied executable. Upstream detects that file and adds its framework reference cache to the decompiler's search directories. This resolves the observed ILSpy `Version not supported: 4.0.0.0` failure without changing decompiler behavior or suppressing an error.

From the upstream root, run:

```bash
dotnet setup/CLI/bin/Release/net10.0/setup-cli.dll decompile \
  --terraria-steam-dir /private/Terraria-PC \
  --tml-dev-steam-dir /private/upstream-output \
  -f --max-parallelism 4 --plain-progress
dotnet setup/CLI/bin/Release/net10.0/setup-cli.dll regen-source \
  -f --patch-mode EXACT --strict --plain-progress
```

These setup operations replace generated source directories. Use a clean working checkout, not a workspace containing unpreserved source edits. The normal upstream ownership/decryption process remains in use; do not publish its derived key. Setup may download the matching official dedicated server executable.

## Build against mobile FNA

From this Android repository:

```bash
python3 tools/build_game.py /private/tModLoader /private/new-managed-candidate \
  --dotnet /path/to/dotnet
```

The destination must not exist and must be separate from upstream. The script checks the pinned revision and .NET SDK, prepares a new revision/hash-checked mobile FNA tree, builds the real generated Terraria project and checks the staged assemblies. It writes `build-report.json`, `build.log`, `assembly-check.log` and the private `game/` output. Keep this directory outside Git, or under the ignored `artifacts/` directory.

`integration/game/MobileGame.targets` redirects both Terraria and ReLogic FNA project references during this build; it does not edit upstream project files. The process uses a single MSBuild node and in-process upstream build tasks (`BuildTasksTaskFactory` empty): the default separate task host failed to connect in this environment. This affects build tooling, not runtime hooks or the game thread.

The output is a managed build candidate. Upstream also copies desktop native libraries and development utilities into that output. They are not Android dependencies and must not be packaged into the ARM64 APK.

## Content roots

The supplied vanilla PC Content stays separate. Known shader assets may resolve from vanilla when no platform override exists; the preflight now follows that fallback instead of requiring duplicate shader files in an otherwise optional override root. A selected malformed override is reported.

The real supplied Content passed the six-asset preflight with an absent override root. The stronger inventory/header checks are recorded in [PC_INPUT_VALIDATION.md](PC_INPUT_VALIDATION.md). Neither check decodes shaders or proves Android rendering compatibility.

## Validation and remaining boundary

Validation passed: 24 shared input/content/runtime checks, 17 checks against the actual compiled FNA adapter and 16 performance collector checks (57 total), plus the managed game assembly check. Synthetic allocation/timing checks are not device benchmarks.

Next: integrate SDL's Android activity/surface and ARM64 native dependencies into the existing SDK-managed Mono/.NET 10 process, configure both content roots, and invoke the game on the game thread. Revalidate graphics, audio, lifecycle, runtime detours and a small real mod on-device. No legacy Mono 8 metadata offsets are reused.
