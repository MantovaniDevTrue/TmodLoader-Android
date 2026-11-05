# Roadmap

Active work is the .NET 10 ARM64 mobile track in [NET10_MOBILE.md](NET10_MOBILE.md). The new input/runtime diagnostic is buildable; migrating the actual game host and supplying matching PC payload/content are the next gates. The historical game milestones below remain pending until verified under the new runtime.

The project advances by proving one compatibility boundary at a time rather than hiding exceptions.

## 1. Finish Main.LoadContent

Current target.

- identify the first blocker after the successful dummy texture upload;
- validate asset reader/service initialization;
- validate audio initialization;
- validate asset source setup;
- reach `ModLoader.PrepareAssets()`;
- complete the first `RunOneFrame()` without a managed exception.

## 2. Reach the tModLoader menu

- complete deferred content loading;
- verify shaders/effects;
- verify font and texture loading;
- verify input and Android window/display behavior;
- stabilize rendering and frame presentation.

## 3. Load desktop mods

- discover and load a normal `.tmod`;
- validate assembly resolution;
- validate mod assets;
- validate hooks/content registration;
- test Split as a real-world compatibility target.

## 4. Enter a world

- character selection;
- world selection/creation;
- world load;
- gameplay update/draw loop;
- save path and persistence.

## 5. Android integration

- native touch/control path;
- lifecycle pause/resume;
- orientation/display handling;
- storage/file picker behavior;
- audio lifecycle;
- crash recovery/log export.

## 6. Performance and compatibility

- remove diagnostic overhead that is no longer needed;
- profile JNI/native/managed transitions;
- reduce unnecessary compatibility bridges;
- test a wider set of desktop mods;
- document known incompatibilities.

## Current rule

A bridge or managed workaround is only added after the failing call is identified in logs and its expected upstream behavior/signature is verified.
