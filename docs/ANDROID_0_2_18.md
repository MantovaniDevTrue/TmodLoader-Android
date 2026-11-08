# Android 0.2.18 — touch-first inventory candidate (not yet shipped)

## Scope

This is the first native-interaction stage for an Android-specific tModLoader
experience, preserving the underlying PC Terraria 1.4.5.8/.NET 10 mod API,
item transactions, recipes, mod hooks and world/game logic.

The source now includes an inventory-specific input context and:
- One-finger pointer selection and native left-button drag/drop.
- Two-finger secondary tap and hold, mapped to the real FNA right button.
- Two-finger vertical swipe mapped to the real FNA mouse wheel.
- Click-through suppression during secondary gestures and scrolling.
- Input reset across inventory/gameplay/options, pause and resizing.
- TouchRouter, FNA MouseState and private integration probe assertions.

This stage does **not** yet implement a fully responsive crafting panel,
custom touch-size ItemSlot layout, Android keyboard integration or the
full UI redesign. Such work is tracked in [NATIVE_MOBILE_UX.md](NATIVE_MOBILE_UX.md)
and [issue #1](https://github.com/MantovaniDevTrue/TmodLoader-Android/issues/1).

## Release status and honesty boundary

The Android `ApplicationDisplayVersion` is staged as `0.2.18` and the
payload refresh marker has been updated. This **does not mean** a playable
0.2.18 APK exists. The source host CI is public and cannot contain
the privately generated tModLoader game or the signing key. The private
0.2.17 APK is a useful input for comparison, not the new release.

Before describing 0.2.18 as built or sharing an APK:
1. The GitHub **.NET 10 Android foundation** workflow must pass the
   `mobile`, `fna` and `startup` jobs, including the new mobile/FNA
   inventory gesture regressions.
2. The **Android SDL game host** workflow must pass both graphics probes,
   launcher and native package checks. Keep the new workflow's source
   artifact out of the private game repository.
3. In the private build environment with the pinned upstream checkout,
   .NET 10 SDK, Android workload, NDK, SDK and signing certificate, build
   a matching `Tml.GameIntegration.dll` with this source revision.
4. Package the freshly built integration with the private tML .NET 10 DLLs;
   never reuse the older 0.2.17 integration binary when it lacks the
   new inventory context.
5. Produce `tML_Android_0.2.18_SDL3_NET10_ARM64.apk`, preserving
   `ApplicationId=com.mantovani.tmlmobile`, and sign with the **same**
   private signing key/certificate as the installed 0.2.17 build.
6. Validate the complete APK with signature v2/v3 verification,
   unique ZIP entries/CRC, AArch64 ABI, 16-KiB ELF/ZIP alignment and
   the correct embedded private `game-payload.zip` and integration DLL.
7. Install as an update, never uninstall first unless user saves and
   imported content are explicitly backed up.

### Existing private build procedure

From the private build machine, using its already prepared upstream sources:

```bash
python3 tools/prepare_android_game.py /private/tModLoader
python3 tools/build_game.py /private/tModLoader /private/android-candidate-0218 --dotnet /path/to/dotnet
python3 tools/package_game_payload.py /private/android-candidate-0218/game artifacts/game-payload-0218.zip

dotnet build src/Tml.Game.Android -c Release -m:1 -nr:false -t:SignAndroidPackage \
  -p:AndroidSdkDirectory=/android-sdk -p:JavaSdkDirectory=/jdk21 \
  -p:TmlMobileFnaProject=/private/android-candidate-0218/fna/FNA.Core.csproj \
  -p:TmlNativeDirectory="$PWD/artifacts/android-native" \
  -p:TmlGamePayload="$PWD/artifacts/game-payload-0218.zip"
```

The native Android libraries must be built from the same pinned sources as
the source host or recovered from the validated source-host workflow artifact.
The example above uses the project's normal private build path; keep
generated source, personal files, `game-payload-0218.zip` and keys **outside
the public repo**. Do not upload a playable APK containing proprietary game
binaries to public GitHub Actions.

In environments that lack the original signing key, **do not** silently
re-sign with a different development key: Android will reject updating the
existing app and uninstalling would jeopardize imported worlds and saves.

## Phone test gate

With OpenGL and only AndroidSwordTest enabled initially:
- Open inventory, tap a slot, drag an item to a second slot, verify stack
  identity and save/reentry.
- On a stack, use two-finger right-click and verify the original native item
  transaction count; swipe the recipe list with two fingers and check that no
  item accidentally moves or crafts.
- Test one-finger drag followed by a second gesture, cancel/resume and
  hardware mouse coexistence. Verify no phantom button hold/click.
- Use the same world to sample closed-inventory World versus open-inventory
  WorldUi FPS, CPU draw p95, allocation/tick and graphics PSS. Compare with
  0.2.17 warm runs and preserve all effects.
- Follow with modded recipes, chests and mod UI compatibility, then full
  responsive crafting/navigation/IME phases.

The 0.2.17 physical baseline was approximately 56.5 presents/s and
20.7 ms draw p95 in World, versus 48.3 presents/s and 23.9 ms draw p95
in WorldUi. These are historic samples, not 0.2.18 results.
