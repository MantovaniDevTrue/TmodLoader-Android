# Roadmap

The project is a dedicated Android variant of tModLoader. Architecture, acceptance criteria and delivery gates are recorded in [ANDROID_PORT.md](ANDROID_PORT.md).

## Validated foundation

- .NET 10 / ARM64 diagnostic opened on the user's phone.
- Dynamic DLL loading and Reflection.Emit succeeded on-device.
- JNI startup, drawing, touch and pause/resume are covered by Android emulator checks.
- Shared multi-touch input and its FNA integration are implemented.

## Current development

- Optional per-tick/update/draw/present timing in the pinned FNA source.
- Bounded performance capture, allocation/collection counters and report statistics.
- This is measurement infrastructure, not evidence of higher game FPS. See [PERFORMANCE.md](PERFORMANCE.md).

## Next: actual game startup

1. Obtain the matching desktop Terraria executable/managed payload and complete PC Content tree privately.
2. Prepare the selected upstream tModLoader build and maintain separate vanilla/platform content roots.
3. Connect SDL's Android surface/lifecycle to the SDK-managed .NET 10 game host.
4. Revalidate graphics, audio and interop and reach the menu without hiding exceptions.

## First playable baseline

- Load one small supported desktop mod.
- Enter a world and verify gameplay hooks, saving and reentry.
- Test pause/resume, surface recreation and audio recovery on-device.
- Record baseline frame timings before attempting performance changes.

## Mobile interface and optimization

- Mobile menus and settings.
- Inventory/crafting touch interactions and dragging.
- Native text entry, scrolling, pinch zoom and configurable mod hotkeys.
- User-editable layouts, controller/mouse coexistence and safe-area scaling.
- Matched, thermally warmed comparisons of rendering, memory, frame pacing and heavier mods.

The 0.1.1 APK remains a controls/runtime diagnostic. A new playable APK will only be described as such after the real game-startup and world gates pass. Historical bootstrap milestones remain in [PHASES.md](PHASES.md).
