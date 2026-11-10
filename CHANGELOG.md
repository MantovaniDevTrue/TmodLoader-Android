# Changelog

Versions are the Android `ApplicationDisplayVersion`. Detailed evidence for recent versions is in [docs/releases](docs/releases/).

## 0.2.18 — staged, not packaged

- Separate inventory input context: one-finger drag, two-finger context tap and hold (right click), two-finger vertical scroll (mouse wheel), with click-through suppression. [Notes](docs/releases/0.2.18.md)

## 0.2.17 — 2026-11-07

- Tile drawing scratch reuse also works with mods loaded, detaching objects only around `DrawEffects` callbacks.
- Reused LZX decoder state; memory reports add Android PSS categories, GC data and decoder reuse counts.
- Phone: world entered and saved with AndroidSwordTest; about 56.5 presents/s in the world and 48.3 with the inventory open. [Notes](docs/releases/0.2.17.md)

## 0.2.16

- Launcher with Home, Mods, Files, Settings and Diagnostics pages, in Portuguese, English and Spanish; mod toggles and TXT report export. [Notes](docs/releases/0.2.16.md)

## 0.2.15

- Reused per-tile drawing scratch object: 248 B per tile before, zero after warm-up. [Notes](docs/releases/0.2.15.md)

## 0.2.14

- Resolver for Android bundled-assembly metadata, so mod assemblies load. First local mod (AndroidSwordTest) loaded on the phone. [Notes](docs/releases/0.2.14.md)

## 0.2.13

- Local `.tmod` import without Workshop, themed launcher, optional Vulkan backend, touch-aim fixes. [Notes](docs/releases/0.2.13.md), [package](docs/releases/0.2.13-package.md)

## 0.2.12

- Native SDL touch on the game thread, in-game HUD and control editor, FAudio cue cleanup fix, bounded compressed-XNB buffers.

## 0.2.11

- Game display state follows FNA dimensions; SpriteBatch reuses non-overlapping buffer regions.

## 0.2.10

- GPU streaming and composition fixes; flicker stopped on Adreno.

## 0.2.9

- Matches the PC minimum resolution and ends the resize feedback loop.

## 0.2.8

- Fixes private calls inside MonoMod-copied originals.

## 0.2.7

- Game hooks wait until tModLoader startup has finished.

## 0.2.6

- Landscape lock, stable display size, Android link opening instead of `xdg-open`.

## 0.2.5

- Android adapter for MonoMod. First world entered and saved on the phone.

## 0.2.0 – 0.2.4

- SDL3 host with FNA3D and FAudio for ARM64, PC installation importer, startup crash capture, embedded entry assembly, native audio file paths. See [SDL_HOST.md](docs/SDL_HOST.md).

## 0.1.1

- Restored dynamic JNI registration so the diagnostic app opens. [Notes](docs/releases/0.1.1.md)

## 0.1.0

- .NET 10 ARM64 input/runtime diagnostic app and shared touch router. [Notes](docs/releases/0.1.0.md)
