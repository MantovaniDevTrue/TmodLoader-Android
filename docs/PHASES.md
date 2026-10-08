# Phase history

This file tracks the diagnostic/fix chain that led to the current Android bootstrap.

| Phase | Name | Result |
|---|---|---|
| 2.22.83 | First Frame NRE Isolation | Moved diagnostics into the first controlled frame. |
| 2.22.84 | ContentSamples State Isolation | Identified startup progress inside `ContentSamples.Initialize`. |
| 2.22.85 | NPC65 Deep Trace | Narrowed the first static-init failure around NPC net ID -65. |
| 2.22.86 | NPCID Static Init Audit | Confirmed the path through `NPCID.Sets`. |
| 2.22.87 | NPCID Static Init Progress | Added progress counters around the large static constructor. |
| 2.22.88 | NPCID JIT Boundary Probe | Verified JIT boundaries while the constructor was running. |
| 2.22.89 | NPC Bestiary Helper Isolation | Narrowed the long stack use into bestiary draw-offset helpers. |
| 2.22.90 | Large Stack Game Thread Fix | Moved game bootstrap to a 32 MiB pthread; `NPCID.Sets` and `ContentSamples` completed. |
| 2.22.91 | Post Content Stage Isolation | Moved tracing past `ContentSamples`. |
| 2.22.92 | SaveSettings Root Cause Isolation | Revealed that the visible `SaveSettings` NRE was masking an earlier FNA failure. |
| 2.22.93 | FNA3D Texture2D ICall Fix | First direct bridge attempt for `FNA3D_CreateTexture2D`. |
| 2.22.94 | FNA3D Binding Audit | Proved `CreateTexture2D` was still P/Invoke while known-good FNA methods were InternalCall. |
| 2.22.95 | FNA3D Texture2D Metadata Bridge | Added metadata conversion, but Android ARM64 tagged pointers prevented the write. |
| 2.22.96 | FNA3D Tagged Pointer Metadata Bridge | Canonicalized tagged pointers and successfully converted `CreateTexture2D` to InternalCall. |
| 2.22.97 | FNA3D Texture Upload Metadata Bridge | Converted `SetTextureData2D`; real 1×1 texture upload completed. |
| 2.22.98 | Post-Texture LoadContent Isolation | Identified `SoundEngine.Initialize` and missing `libFAudio.so` as the first post-texture compatibility boundary. |
| 2.22.99 | FAudio No-Device Fallback | Validated: the zero-device bridge reaches the native Terraria/tML no-audio fallback, `SoundEngine.Initialize` returns, asset services initialize, and shader loading is reached. |
| 2.23.00 | FNA3D Effect Metadata Bridge | Validated: three real shader effect blobs reached APK FNA3D and returned non-null effect/effectData handles. The next failure was isolated to `FNA3D_GenVertexBuffer` during `SpriteBatch` construction. |
| 2.23.01 | FNA3D SpriteBatch Buffer Bridge | Validated: vertex/index buffers allocate successfully, index data uploads, SpriteBatch effects are created, SupportsNoOverwrite returns, render targets are created, and the next blocker is missing vanilla PC content (`Images/SplashScreens/Splash_1`). |

## Major breakthroughs

### Phase 2.22.90

The static initialization crash was not a bad NPC definition. It was a stack-size problem. A dedicated game pthread with a 32 MiB stack allowed:

- `NPCID.Sets::.cctor` to complete;
- all NPC/projectile/item sample generation to complete;
- `ContentSamples.Initialize()` to return normally.

### Phase 2.22.92

The visible exception was:

`Terraria.Main.SaveSettings() -> NullReferenceException`

but first-chance tracing showed the actual earlier failure:

`Microsoft.Xna.Framework.Graphics.FNA3D_Impl:FNA3D_CreateTexture2D -> InvalidProgramException`

The failing wrapper contained an unsupported/invalid `calli` path on the Android MonoVM environment.

### Phase 2.22.96

The FNA method metadata was converted from:

`PInvokeImpl + unmanaged wrapper`

to:

`InternalCall + native bridge`

The Android ARM64 method pointer used top-byte tagging, so the bootstrap had to canonicalize the pointer only for memory-map/protection lookup while keeping the original Mono pointer for access.

### Phase 2.22.97

The same verified conversion pattern was applied to `FNA3D_SetTextureData2D`. The runtime then successfully allocated a normal 1×1 texture and uploaded four bytes of pixel data.

### Phase 2.22.98

The first stage after the successful dummy texture upload was confirmed as:

`Terraria.Audio.SoundEngine.Initialize()`

The first-chance trace immediately exposed:

`System.DllNotFoundException: libFAudio.so`

This moved the active investigation from graphics into the audio initialization boundary.

### Phase 2.22.99

Instead of bundling an unverified FAudio build or modifying managed game logic, the bootstrap now converts only `FAudioCreate`, `FAudio_GetDeviceCount` and `FAudio_Release` to InternalCalls and reports zero devices.

The fallback is now proven. `SoundEngine.Initialize()` returns normally, `AssetInitializer.CreateAssetServices()` completes, and the asset readers register. After the matching shader XNB files are supplied externally, `TMLContentManager.OpenStream` also returns normally for `PixelShader`. Real Android audio remains separate work.

### Phase 2.23.00

With the shader file now found, the focused trace ends immediately after `TMLContentManager.OpenStream` returns. Upstream FNA then reads the XNB effect blob through `EffectReader`, constructs `Effect`, and calls `FNA3D_CreateEffect`.

Phase 2.23.00 applies the same proven ARM64 metadata conversion pattern to `FNA3D_CreateEffect`, but additionally resolves the managed `byte[]` through Mono's public array embedding APIs before forwarding the shader bytes to the real APK FNA3D implementation.

The bridge is now validated on-device. Three effect blobs (91,904, 34,052 and 39,320 bytes) returned non-null `effect` and `effectData` handles. The next blocker is a managed exception occurring only after those successful native effect creations; its exact `Exception.ToString()` is being isolated before the next code change.

### Phase 2.23.01

The managed exception from Phase 2.23.00 was resolved to an invalid generated P/Invoke wrapper for `FNA3D_GenVertexBuffer`. Source inspection of FNA shows the first `SpriteBatch` constructor immediately uses `GenVertexBuffer`, `GenIndexBuffer`, `SetIndexBufferData`, the already-working `CreateEffect`, and `SupportsNoOverwrite`.

Phase 2.23.01 therefore bridges that exact constructor path rather than guessing at unrelated FNA3D methods. All four new bridges forward to the real `libFNA3D.so` functions.

### Phase 2.23.01 validation

The exact SpriteBatch constructor path is now proven on-device. Multiple vertex and index buffers returned non-null native handles, index data uploads completed, the internal SpriteBatch effect was created through the already-proven effect bridge, and `SupportsNoOverwrite` returned normally. The run then continued into additional texture and 2048x2048 render-target creation. The next exception is an `AssetLoadException` for `Images/SplashScreens/Splash_1`, confirming that startup has reached vanilla asset loading and now needs a complete matching Terraria PC Content tree.
