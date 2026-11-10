# Roadmap

Architecture and acceptance criteria are in [ANDROID_PORT.md](ANDROID_PORT.md).

## Done

- .NET 10 ARM64 runtime with JIT, dynamic DLL loading and `Reflection.Emit` on the phone.
- SDL3 host with FNA3D and FAudio, PC installation import and the real tModLoader entry point.
- Menu, world entry, saving and exit on a physical device.
- Local `.tmod` import; first simple mod loaded on the phone.
- Native touch controls with an in-game editor.
- Performance and memory reports; allocation-free tile drawing and content decoding.

## In progress

- Touch-first inventory input (0.2.18).
- Rendering and GPU memory profiling.

## Next

- Mobile crafting panel with search, categories and craft actions that use the original recipe code.
- Android Back navigation and keyboard input for chat, signs and search.
- Touch-friendly menus, mod configuration, map and other game panels.
- Editable action palette for mod key bindings.
- Compatibility tests with larger content mods, chests, shops and mod UIs.
- Vulkan on devices where it starts reliably.
