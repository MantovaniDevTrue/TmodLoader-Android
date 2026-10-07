# Debugging

The bootstrap writes logs under:

`/storage/emulated/0/Android/media/com.mantovani.tmlmono8/`

## Phase 2.22.99 files

- `monovm_phase2_22_99.txt` — main bootstrap/runtime log.
- `monovm_phase2_22_99_state.txt` — last known high-level state.
- `monovm_phase2_22_99_loadcontent.txt` — focused post-texture `LoadContent` trace.
- `monovm_phase2_22_99_audio.txt` — focused FAudio metadata/fallback audit.
- `monovm_phase2_22_99_fna3d_binding.txt` — FNA3D method metadata/binding audit.
- `monovm_phase2_22_99_stack.txt` — large-stack pthread diagnostics.
- additional focused files may exist for earlier diagnostic subsystems.

## Useful commands

FAudio compatibility audit:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_99_audio.txt
```

Focused LoadContent trace:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_99_loadcontent.txt
```

Last state:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_99_state.txt
```

Audio/fallback and managed exception context:

```bash
grep -E "FAudio|DisabledAudioSystem|SoundEngine|LOADCONTENT_|FIRST_CHANCE_EXCEPTION|RUNONEFRAME_EXCEPTION|MANAGED EXCEPTION" \
/storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_99.txt | tail -n 350
```

Full exception context if a new managed failure occurs:

```bash
grep -A100 -B120 "RUNONEFRAME_EXCEPTION_BEGIN" \
/storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_99.txt
```

## Expected Phase 2.22.99 transition

The focused audit should show the three probe methods converted to InternalCall:

```text
FAUDIOCREATE_METADATA_PATCH=APPLIED
FAUDIO_GETDEVICECOUNT_METADATA_PATCH=APPLIED
FAUDIO_RELEASE_METADATA_PATCH=APPLIED
```

The runtime should then execute the compatibility bridge:

```text
ICALL_BRIDGE_ENTER=FAudioCreate no-device Android fallback
ICALL_BRIDGE_ENTER=FAudio_GetDeviceCount no-device Android fallback
... count=0 ...
```

If the upstream fallback behaves as expected, `DisabledAudioSystem::.ctor` should run and `SoundEngine.Initialize` should return before the trace advances to the next `Main.LoadContent` stage.

## Diagnostic rule

Always identify the **first** real exception/failure. A later exception can be a fallback-path side effect and may hide the original problem, as happened with `SaveSettings()` in Phase 2.22.92.

First-chance exceptions are diagnostic events, not automatically fatal exceptions. Follow the method-enter/leave sequence and final `RunOneFrame` outcome before deciding whether a first-chance exception itself is the blocker.
