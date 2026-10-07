# Phase 2.23.00 — FNA3D Effect Metadata Bridge

This is the current **test** phase after the validated Phase 2.22.99 audio fallback.

## Why this phase exists

After supplying the correct platform shader assets:

- `Content/PixelShader.xnb`
- `Content/TileShader.xnb`
- `Content/ScreenShader.xnb`

the next run no longer throws `FileNotFoundException` for `PixelShader.xnb`.

The new trace reaches:

`Terraria.ModLoader.Engine.TMLContentManager::OpenStream`

and returns from it normally.

The trace then stops immediately after the shader stream is opened. Upstream FNA's `EffectReader` reads the compiled effect byte array and constructs `Microsoft.Xna.Framework.Graphics.Effect`, whose constructor calls `FNA3D_CreateEffect`.

This is the next unmanaged boundary being tested.

## What changed

Phase 2.23.00 preserves every validated bridge from earlier phases and additionally:

- resolves the APK's real `FNA3D_CreateEffect` symbol;
- resolves Mono's public array embedding APIs:
  - `mono_array_length`
  - `mono_array_addr_with_size`
- converts `FNA3D_Impl.FNA3D_CreateEffect` from P/Invoke metadata to InternalCall before first JIT;
- forwards the managed `byte[]` effect blob to the real APK FNA3D implementation;
- traces:
  - `ContentReader`
  - `EffectReader.Read`
  - `Effect::.ctor`
  - `FNA3D_CreateEffect`.

## Snapshot

`tML_Phase2_23_00_FNA3DEffectMetadataBridge_Fontes.zip`

SHA-256:

`e760f478bdaeca8705d3846826045d745afb7aacd12e4ca33278a7fc6df6a077`

## Status

Not yet validated on-device.

The expected successful transition is:

`OpenStream -> EffectReader.Read -> Effect::.ctor -> FNA3D_CreateEffect bridge -> shader asset loaded`.

If that succeeds, the same run should continue into `TileShader`, `ScreenShader`, or the next real `Main.LoadContent` subsystem.

## Excluded intentionally

The public source snapshot does not contain:

- APK signing private keys;
- generated APKs;
- proprietary game/runtime payloads;
- the shader XNB payloads themselves;
- personal logs or save data.
