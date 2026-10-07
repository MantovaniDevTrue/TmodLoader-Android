# Debugging

The bootstrap writes logs under:

`/storage/emulated/0/Android/media/com.mantovani.tmlmono8/`

## Phase 2.22.98 files

- `monovm_phase2_22_98.txt` — main bootstrap/runtime log.
- `monovm_phase2_22_98_state.txt` — last known high-level state.
- `monovm_phase2_22_98_loadcontent.txt` — focused post-texture `LoadContent` trace.
- `monovm_phase2_22_98_fna3d_binding.txt` — FNA3D method metadata/binding audit.
- `monovm_phase2_22_98_stack.txt` — large-stack pthread diagnostics.
- additional focused files may exist for earlier diagnostic subsystems.

## Useful commands

Focused LoadContent trace:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_98_loadcontent.txt
```

Last state:

```bash
cat /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_98_state.txt
```

Tail of the main log:

```bash
tail -n 250 /storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_98.txt
```

Managed exception context:

```bash
grep -A100 -B120 "RUNONEFRAME_EXCEPTION_BEGIN" \
/storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_98.txt
```

FNA/PInvoke activity:

```bash
grep -E "PINVOKE_OVERRIDE_REQUEST=FNA3D:|PINVOKE_OVERRIDE_RESULT=FNA3D:|ICALL_BRIDGE.*FNA3D|FIRST_CHANCE_EXCEPTION|MANAGED_EXCEPTION_TOSTRING" \
/storage/emulated/0/Android/media/com.mantovani.tmlmono8/monovm_phase2_22_98.txt | tail -n 400
```

## Diagnostic rule

Always identify the **first** real exception/failure. A later exception can be a fallback-path side effect and may hide the original problem, as happened with `SaveSettings()` in Phase 2.22.92.
