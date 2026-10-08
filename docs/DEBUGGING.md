# Debugging

The bootstrap writes logs under:

`/storage/emulated/0/Android/media/com.mantovani.tmlmono8/`

## Phase 2.23.00 files

- `monovm_phase2_23_01.txt` — main bootstrap/runtime log.
- `monovm_phase2_23_01_state.txt` — last known high-level state.
- `monovm_phase2_23_01_loadcontent.txt` — focused shader/XNB `LoadContent` trace.
- `monovm_phase2_23_01_fna3d_binding.txt` — FNA3D metadata/binding audit, including `CreateEffect`.
- `monovm_phase2_23_01_audio.txt` — retained FAudio no-device audit.
- `monovm_phase2_23_01_stack.txt` — large-stack pthread diagnostics.

## Useful commands

FNA3D effect metadata audit:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_23_01_fna3d_binding.txt
```

Focused shader/content trace:

```bash
tail -n 350 /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_23_01_loadcontent.txt
```

Effect bridge and exception context:

```bash
grep -E "CreateEffect|EffectReader|PixelShader|TileShader|ScreenShader|LOADCONTENT_|FIRST_CHANCE_EXCEPTION|RUNONEFRAME_EXCEPTION|MANAGED EXCEPTION" \
/storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_23_01.txt | tail -n 450
```

Last state:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_23_01_state.txt
```

## Expected Phase 2.23.00 transition

The binding audit should show:

```text
CREATEEFFECT_METADATA_PATCH=APPLIED
CREATEEFFECT_METADATA_AFTER ... pinvoke=0 internalCall=1
CREATEEFFECT_LOOKUP_AFTER_PATCH=... match=1
```

The runtime should then show:

```text
TMLContentManager::OpenStream -> LEAVE
EffectReader::Read
Effect::.ctor
FNA3D_CreateEffect
ICALL_BRIDGE_ENTER=FNA3D_CreateEffect Android InternalCall bridge
ICALL_BRIDGE_EXIT=FNA3D_CreateEffect ... effect=... effectData=...
```

If the effect is accepted by FNA3D, the trace should continue into the remaining shader assets or the next `Main.LoadContent` subsystem.

## Phase 2.22.99 result

The FAudio no-device fallback is already validated. `NoAudioHardwareException` is expected during the probe and is handled by Terraria's own `TestAudioSupport` path.

The previous `PixelShader.xnb` file-not-found error is also resolved once the matching shader XNB payloads are supplied under `Content/`.

## Diagnostic rule

Always identify the **first** real exception/failure. A later exception can be a fallback-path side effect and may hide the original problem, as happened with `SaveSettings()` in Phase 2.22.92.

First-chance exceptions are diagnostic events, not automatically fatal exceptions. Follow the method-enter/leave sequence and final `RunOneFrame` outcome before deciding whether a first-chance exception itself is the blocker.
