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

The 0.2.15 tile scratch optimization and compatibility guard are unchanged.
Physical phone gameplay remains the final performance validation.
