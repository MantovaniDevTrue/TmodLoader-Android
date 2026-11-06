# .NET 10 / ARM64 mobile foundation

This is the first source milestone of the new Android port. It is **not a playable tModLoader APK**.

## Implemented

- `Tml.Mobile.Core`: bounded, thread-safe multi-touch routing with stable finger IDs, independent movement/aim sticks, simultaneous buttons, dead zone, short-tap preservation and explicit menu/text/suspended contexts.
- `Tml.Mobile.Android`: buildable .NET 10 Android ARM64 input/runtime diagnostic app. Landscape orientation, cutout/system-bar safe area, cancellation on focus loss/pause/resize and on-screen controls.
- `Tml.RuntimeProbe`: a separate DLL, not referenced by the Android host, copied from assets into app storage and loaded at runtime. It shares the host's typed `IRuntimeProbe` interface. The diagnostic also exercises `Reflection.Emit`.
- FNA integration: small reproducible source edits merge touch input into actual FNA `KeyboardState`/`MouseState` values. Sampling happens once per game update in both fixed and variable timestep paths. Hardware keyboard keys and unrelated mouse buttons are retained.
- Default buttons: jump, use item, interact, grapple, inventory, menu, heal, mana, buffs, mount, smart cursor, map, previous/next hotbar item.
- Content preflight: identifies six known startup assets across separate Terraria PC and tModLoader content roots, including the missing splash asset from the old baseline. Shader lookup falls back to vanilla when no platform override is supplied. This is a basic presence/header check, not certification of a complete content installation.
- GitHub Actions builds the diagnostic APK and compiles/tests the patched FNA without proprietary payloads.

## Runtime decision

The host targets `net10.0-android`, `android-arm64`, with Mono JIT, IL retained and trimming disabled. The APK's runtime/native libraries are AArch64. Mod DLLs remain managed IL and can be JIT-compiled to ARM64 on the device. This is not a claim that arbitrary mods are statically compiled into the APK.

NativeAOT, full AOT without a JIT fallback and removal of mod-visible members are not the initial configuration: compatibility with dynamically loaded mods must be proven first. Profile-guided AOT can be evaluated later against real game benchmarks. MonoMod hooks still require separate on-device validation.

## Upstream source

`upstream.lock.json` records the inspected tModLoader `1.4.5` commit, the exact FNA submodule revision and hashes of the FNA files being edited. The inspected upstream FNA already targets .NET 10. Pinning it does **not** mean the old Android game bootstrap has been migrated.

Run `tools/prepare_fna.py SOURCE DESTINATION` against the pinned clean FNA checkout. The script verifies its revision and file hashes, writes to a new destination, adds the shared mobile core reference and preserves upstream licenses. It refuses an existing destination or changed patch context.

## Connect to a future SDL game host

The native SDL Android window/context and the managed game must run in the same process/runtime. Do not start a second Mono runtime inside the SDK-managed app.

1. Build/load the Android FNA native dependencies and establish SDL's Android activity/surface lifecycle before constructing the game.
2. Disable SDL's automatic touch-to-mouse emulation before SDL initialization (`SDL_TOUCH_MOUSE_EVENTS=0`) so one physical finger does not produce duplicate input through SDL and this overlay.
3. Put `TouchControlsView` above the SDL surface and set `ConsumeFrames = false`.
4. On the game thread, assign `MobileInputEXT.Source = overlay.Router`. FNA's patched update loop becomes the only consumer; the UI reads `PeekFrame()` under the same lock.
5. Keep the overlay viewport aligned to the actual SDL surface, including any letterboxing. Use `SetContext` for gameplay, menu, native text entry and suspension.
6. Match keyboard bindings to Terraria's selected input profile through `MobileInputEXT.Bind`. Update the normalized aim origin from the rendered player position, especially near world edges.
7. Pause/cancel input on surface destruction, focus loss, application pause, display resizing and mod reload. Recreate GPU resources through the normal FNA/SDL lifecycle.

These are integration requirements; the diagnostic activity does not create the SDL game surface or call Terraria's entrypoint yet.

## Verification scope

Automated checks exercise touch gestures and the actual patched FNA input implementation on desktop .NET 10. Allocation checks cover warmed core/input merge loops, not Android drawing, JNI, game rendering or arbitrary mods. They do not establish Android FPS.

The ARM64 diagnostic APK can test runtime loading and touch behavior on a real phone. Its screen explicitly says that it does not start Terraria. No Android device is attached to the build environment, so device results are pending.

## Remaining work

1. The private managed candidate now builds in .NET 10 against mobile FNA and passes assembly/type checks ([GAME_BUILD.md](GAME_BUILD.md)). It is not yet an Android game build; platform dependencies still require integration. The supplied executable and Content passed [archive/basic validation](PC_INPUT_VALIDATION.md).
2. Migrate the real SDL/FNA game host to the SDK-managed .NET 10 runtime. Revalidate interop rather than transplanting Mono 8 metadata offsets.
3. Reach menu, load a normal `.tmod`, enter a world and save/reenter.
4. Integrate the overlay with real gameplay, menus, inventory/crafting dragging, virtual keyboard, scrolling, pinch zoom and configurable mod hotkeys.
5. Add layout editing/persistence, mobile settings, controller coexistence, audio lifecycle and a user-facing content importer.
6. Measure frame time, memory, GC, shaders and thermals on the Xiaomi Mi 11 Lite 5G NE before making performance claims.

The old phase 2.23.01 APK was inspected: it contains SDL2/FNA3D/bootstrap native libraries and Java classes, but no desktop managed game/runtime/content payload. The source archives in the public repository are historical partial snapshots, not a complete buildable game source tree.
