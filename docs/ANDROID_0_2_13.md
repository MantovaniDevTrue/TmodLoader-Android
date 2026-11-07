# Android 0.2.13

## Input

Touch fingers keep ownership of a HUD control for their gesture. Pointer and aim
stick events carry monotonic revisions. Only a new free-screen gesture or a new
stick gesture changes aim ownership. Releasing the stick retains its last direction;
HUD buttons cannot restore a stale screen tap. Suspend suppresses synthetic input.
The native SDL touch path remains on the game thread, with no Android overlay mouse.
Player direction follows stick aim before the existing action trigger applies fire.

## Local mods

Use **Adicionar mods (.tmod)** in the launcher. Android's document picker supports
multiple packages, including files exposed by Downloads or another file provider.
Imports stream to a temporary file, require a TMOD header and minimum package size,
and enforce 1 GiB per package. Names are normalized; installation uses an atomic
same-directory move. Updates keep `<name>.tmod.previous`. Incomplete/invalid imports
leave the installed package intact. The full package format and version are then
validated by tModLoader itself.

Destination: `files/Saves/tModLoader/Mods`, matching `Main.SavePath/Mods` in the
existing loader. Activate imported packages in **Gerenciar mods**, then reload.
No Steam/Workshop dependency. A precompiled .tmod runs its IL in Mono; Roslyn is
for compiling source, not required to execute the packaged assembly. PC mods can
still depend on unavailable native libraries or desktop APIs; compatibility is
not guaranteed. Establish the no-mod baseline before testing large mods.

## Launcher and themes

Native Android launcher: three setup/play/local-mod cards, original tModLoader
icon plus a separate Android vector badge, reports and graphics test.
tModLoader/Calamity/Thorium are palette presets. No mod artwork is bundled.
Import one custom JSON palette using **Importar tema** (64 KiB maximum):

```json
{"Name":"Meu tema","Background":"#111725","Surface":"#202C43","Accent":"#9BC55D","Text":"#F1F4FA","Muted":"#B1BDD2"}
```

All six fields are required. Colors are #RRGGBB. Imported theme and renderer choice
persist across app restarts. Importing a new custom theme replaces the previous
custom palette; the built-in presets remain available.

## Rendering and performance evidence

OpenGL ES remains the default. Vulkan is an experimental alternative through
FNA3D SDLGPU / SDL Vulkan, enabled in the native build. Graphics tests use the
selected backend and the same multi-texture/render-target pixel fixture. GL error
polls run only with OpenGL. Do not infer that Vulkan improves a CPU-limited scene;
compare the same world, camera and effects on the phone before choosing a backend.

Explicit interpreter disabling preserves Mono JIT and dynamic IL loading.
`ANDROID_CPU_EXECUTION` looks up an already-executed method in Mono's JIT table;
a successful lookup reports architecture and machine code size, otherwise the
log says unconfirmed. Reflection InterpretedInvoke frames do not by themselves
show the execution mode of game update methods.

The 0.2.12 samples supplied were after leaving the world. They cannot establish
world FPS. New performance reports retain separate World/WorldUi/Menu/Saving/
Paused samples, reset windows on context transitions, and avoid extending a last
presentation timestamp through inactivity. Twelve recent records per context
remain available instead of menu records replacing the entire gameplay trace.
Memory values distinguish managed heap, native heap and Android PSS.

Deferred content/music iterator timings report wall time, actual MoveNext work,
worst step and step count, covering the unexplained startup gap outside LoadContent.
This instrumentation does not skip assets, effects or save validation.

## Validation

Local foundation/FNA/private hook checks and public Android CI are recorded with
the release. ARM64 phone validation is still required for touch behavior, world
FPS and the experimental Vulkan backend. No promise of sustained 60 FPS is made.


## SDL launcher CI regression — 2026-11-07

The Android emulator smoke test resolves **Renderizador** to the actual
`android.widget.Spinner`, not the neighboring text label. Dropdown items are
selected from visible, enabled UI nodes; the launcher is scrolled only while
discovering controls. Five standalone selector regression cases pass locally.

After the OpenGL/SDLGPU probe has reached its first rendered frame, the test
preserves the startup log, clears logcat, and injects a fresh tap on the captured
landscape surface. It then requires a new native SDL game-thread touch event
without generating extra launcher scroll gestures. This prevents old or unrelated
input events from satisfying the assertion.

Local Python selector tests pass. The modified GitHub Actions emulator run,
real ARM64 gameplay, Vulkan graphics and private APK contents/signature need
separate verification; a source commit or prior APK does not prove them.


## Emulator workflow #34 — failure triage

The 2026-11-07 Android SDL game host #34 run on commit `a6630a9`
was reported as **failed** for the `surface` job, with two artifacts.
The artifact count is consistent with the staged public source-host APK and
always-uploaded smoke diagnostics, but does not establish a passed Vulkan
test or a packaged private/playable game payload. The screenshot does not
include the failing step or log message, so its root cause is unknown.

The next source commit `96daf0e` improves smoke-test fault localization:
the console reports the active renderer and exact missing log marker, and
prints relevant SDL/touch diagnostics on failure. The Vulkan and OpenGL
checks retain their original acceptance requirements. A fresh CI outcome
and the failing run's `sdl-host-checks` files are still needed before
claiming that the emulator issue has been fixed.


## Workflow 42394775887 — exact failure and fix

[Run 42394775887](https://github.com/MantovaniDevTrue/TmodLoader-Android/actions/runs/42394775887)
checked commit `96daf0e`. The ARM64 package/signature checks, source-host artifact,
x64 emulator APK and OpenGL probe passed. OpenGL presented 5,496 of 8,192
expected pixels. Vulkan/SDLGPU also reached `SDL_FNA_FIRST_FRAME` and
`ANDROID_GPU_STRESS_OK`, but the smoke script failed on an expected
`FNA3D Driver: SDLGPU` log line, which was not emitted to Android logcat.
The log also showed a created Vulkan instance and device, but that alone
was insufficient to prove the active FNA3D renderer.

The fix instruments the pinned FNA3D SDLGPU creation path with
`SDL_GetGPUDeviceDriver(device)` and emits
`TML_SDL_GPU_ACTIVE_DRIVER=<actual-driver>` through SDL warning logging
after a GPU device is successfully created. Smoke tests now require the
specific `TML_SDL_GPU_ACTIVE_DRIVER=vulkan` marker, rather than the
unavailable banner or any unrelated mention of Vulkan. The source patch
is strict and idempotent, and a synthetic regression fixture is included
in the workflow.

The change has been committed, but **the next emulator run and a private,
playable ARM64 APK are not yet validated**. These checks cover the
host graphics probe, not full Terraria gameplay or mod loading.
