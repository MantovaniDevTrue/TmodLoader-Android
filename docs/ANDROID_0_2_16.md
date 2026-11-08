# Android 0.2.16 — launcher workspace

The launcher now has five independent pages with persistent navigation:

- Home: installation and selection status, normal play, and play without mods.
- Mods: import local packages, toggle each internal mod identity, disable all and
  restore the selection saved before disabling. Duplicate package versions share
  one toggle. Headers show package version and target tML; compatibility and
  dependencies remain the game's responsibility.
- Files: import/update the owned Terraria PC ZIP and explain data retention.
- Settings: Portuguese, English or Spanish launcher language; tML, Calamity,
  Thorium or imported JSON theme; renderer selection; keep-screen-awake preference.
- Diagnostics: current session summary, fatal errors, warnings, separate Android
  exit history, full selectable log, clipboard copy, TXT export and graphics probe.

The native layout uses a scrollable navigation rail on wider landscape displays
and top navigation on smaller widths. Content has its own vertical scrolling.
There are no placeholder settings. Touch controls are configured in world, as
before. Launcher language does not change Terraria's own language.

## Mod selection

`LocalModLibrary` reads only bounded current-format TMOD header strings and uses
the internal name rather than the external filename. It never loads a mod DLL.
It writes the game's existing `Mods/enabled.json` as an atomic JSON string array.
Disabling all saves nonempty selections to `launcher-selection.json`; repeated
disabling does not overwrite that backup with an empty selection. Restore is an
explicit action. Starting without mods persists the empty selection until the
user changes or restores it. Package files and save files are not removed.
Before each game run, integration invalidates tML's lazy enabled-mod cache after
the save paths are initialized. The next read uses the game's own JSON parser.
The private touch probe seeds an old nonempty cache and verifies the real
EnabledMods property rereads the new empty selection from disk.

Corrupt package headers are shown individually. A corrupt selection is reported
and is not silently overwritten. Header parsing is not complete asset, hash,
dependency or compatibility validation. Import retains the existing package
validation and replacement policy.

## Diagnostics and export

The summary separates warnings/first-chance exceptions from fatal signatures.
Historical Android SIGSEGV or SIGKILL entries are never interpreted as current
session failures. Status 9 alone has no inferred cause. The summary describes
the available excerpts, not a complete guarantee that no error occurred.

TXT export uses Android ACTION_CREATE_DOCUMENT with text/plain and a timestamped
filename. The document picker lets the user choose the location. The export is a
UTF-8 snapshot of the same full report available on screen; saving errors are
shown in the launcher. No broad storage permission is requested.

Core regressions cover internal identities and duplicate versions, actual sword
package identity, selection round-trip, repeated-disable backup preservation,
bounded malformed headers, corrupt selection preservation, and diagnostic
classification with old native crash history. Android smoke navigation checks
each page, language round-trip, and actual empty enabled.json creation before
continuing the existing OpenGL/SDL_GPU graphics tests.

The OpenGL emulator smoke also exercises ACTION_CREATE_DOCUMENT, pulls the saved
TXT from Downloads, decodes it as UTF-8 and checks the app version header. The
keep-screen-awake preference is passed through the SDL activity and honored at
FNA's initial and focus-related screensaver calls, rather than being overridden
by FNA after the activity's window flag is set.

The 0.2.15 tile scratch optimization and compatibility guard are unchanged.
Physical phone gameplay remains the final performance validation.

## Packaged candidate and validation

Playable private APK: `tML_Android_0.2.16_SDL3_NET10_ARM64.apk`, 51,754,605 bytes.
SHA-256: `c624c78a6730342d2e8df51d70f8cf0feec950bc621bc87e4c94b0c0d98cabb2`.
Payload SHA-256: `723c67ea2f79410a2c5519fbb62bc70bb9d11a0e9cc0bbd03799afd9df05f59c`.
Source: `2ec386a2922087ca5cbe5ef0779c58adc58b0060`.

Android host workflow 42429633823 and all three foundation jobs in workflow
42429633861 passed. Source-host artifact 13596169431 was verified against its
published ZIP digest before assembly. The final package's existing signing
certificate, ARM64 libraries, 16 KiB alignment, manifest version, ZIP CRC, duplicate
entries, exact embedded integration and matching host metadata images were checked.

The emulator navigated all five launcher pages, changed to English and back,
disabled all mods and verified the real enabled.json, saved a TXT through the
Android document picker, pulled it from Downloads and decoded its version header
as UTF-8. That initial report contained only the 19-byte app header because no
session had run yet. Synthetic regression cases separately cover current errors,
handled warnings and historical native crashes. Screenshots of the home, files,
mods, settings, English interface, diagnostics and document picker were reviewed.
OpenGL and SDL_GPU native graphics/input fixtures also passed; emulator Vulkan
success does not supersede the failed physical Adreno Vulkan test.

Local regressions passed: 73 core/input/library/diagnostic checks (including real
sword identity), 28 FNA adapter checks, 20 host checks, 16 performance recorder
checks and 30 tile scratch checks. The real private API contracts, touch editor,
selection-cache refresh and five-type mod metadata probe passed headlessly.
The emulator audit reads app-private files using its rooted test image; the
production APK remains nondebuggable and does not request root on the phone.

Install over the previous version without uninstalling. Physical phone UI and
in-world performance validation remain pending. Use OpenGL for that baseline.
