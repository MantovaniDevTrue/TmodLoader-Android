# Android 0.2.17 — transient rendering and content memory

## Physical baseline

The user's 0.2.16 OpenGL report confirms AndroidSwordTest 0.1.1 was loaded.
World samples were mostly 57–60 presentation calls/s with draw p95 around
22–23 ms; longer WorldUi samples were around 45 calls/s and 27 ms draw p95.
Update p95 was roughly 3 ms. Managed usage was approximately 675–691 MiB and
PSS 2346–2377 MiB in the sampled gameplay period. The session saved in 2609 ms
and exited normally. Historical Android exits are older than this session.

Startup included a roughly 29-second gap after music and a rolling-window
allocation total of about 1.72 GB. That is cumulative allocation, not live memory;
overlapping windows must not be summed. The report does not attribute all these
bytes to a particular function. In 0.2.16, loading any external mod also disabled
tile scratch reuse, so this run did not measure the no-mod tile optimization.

## Tile scratch with normal mod callbacks

Reuse no longer depends on whether any external mod is loaded. Instead, two
guarded IL edits in TileLoader.DrawEffects detach the leased object immediately
before the actual ModTile.DrawEffects or registered GlobalTile delegate call.
With no callbacks, the allocation-free scratch path remains available even when
an item mod is loaded. A callback gets the original object and color array, which
are never reset or reused after exposure. By-ref replacement and exceptions keep
their original semantics. The original callbacks and drawing order remain intact.

The edit requires exactly the two expected callback sites in the supplied game.
This contract covers the normal DrawEffects API. Arbitrary third-party IL edits
that expose private scratch elsewhere require separate compatibility review.
No effects, resolution, simulation rate or content are removed.

Private regressions invoke the actual TileLoader.DrawEffects path with no
handlers, a retaining GlobalTile delegate, by-ref replacement, a throwing delegate
and a registered retaining ModTile subclass. Retained objects and color arrays
remain independent of later scratch leases. Existing constructor-default,
re-entrancy, exception and warmed allocation checks remain in place.

## LZX decoder reuse

FNA previously created a 64 KiB LZX window and eight Huffman arrays for every
compressed XNB. A bounded, synchronized pool retains at most two decoders. Every
lease restores constructor-equivalent scalar fields, window bytes and all tables.
The decoder is returned in finally after the complete file, including failures;
state is preserved between compressed frames within that file. The uncompressed
LZX block path reads exactly into its existing window instead of allocating and
copying another temporary byte array.

The local CoreCLR test measures 87,608 allocated bytes for each fresh decoder,
and zero bytes for 1000 warmed decoder rent/return operations. This excludes
streams, asset objects, per-frame bit readers and texture storage, and Android
object sizes may differ. Retained decoder storage is about 175 KiB plus pool
bookkeeping, independent of total asset count.

Generated format-valid LZX fixtures cover verbatim, aligned and uncompressed
blocks, short reads, independent files, a three-frame stream crossing the window
boundary, concurrent leases and constructor-state restoration. Actual
ContentManager.Load<int> reads a compressed XNB fixture, rejects a malformed
compressed file and successfully loads the next file through the same pool.
Pinned upstream source hashes guard all FNA edits.

## Measurement and limits

The five-second performance report adds LZX creation/reuse counts and Android
PSS categories for Java, native, graphics, code, private-other and system memory.
These categories must not be added to the existing managed/native-heap values,
which are different measurements and overlap. Unsupported category values are -1.
GC heap and committed numbers describe the runtime's most recent GC information;
some runtimes return zero or do not provide them. No forced frame GC is added.

Physical FPS, startup time and total RAM improvements remain unverified until
the next comparable phone run. Vulkan remains experimental; this work targets
the working OpenGL baseline. The launcher and in-world touch editor are retained.

## Candidate package and verification

Private playable APK: `tML_Android_0.2.17_SDL3_NET10_ARM64.apk`, 51,762,797 bytes.
SHA-256: `5d090c9c46b1296816008161d5e5b68c1705bd4ec252ba039a5ecc6d77f012f1`.
Payload SHA-256: `1d36fe7e4211b37b29dae451c701eb798e01e6c98c400f6804b06d6fca0f6546`.
Source: `9cb031e6b39975113f29a5707c182cae7b9f0a50`.

Host workflow 42432868466 passed, including ARM64 packaging and the existing
OpenGL/SDL_GPU rendering, input and launcher/TXT export fixtures. All three jobs
in foundation workflow 42432868433 passed. Host artifact 13598058565 was checked
against its published digest before assembly. The final package's signature,
ARM64 libraries, 16 KiB alignment, manifest, ZIP integrity, exact private
integration and matching metadata images were verified.

Local checks passed: 73 FNA/input/LZX assertions, 37 tile scratch/callback
assertions, 73 core/input/library/diagnostic assertions, 20 host assertions and
16 performance recorder assertions. The actual private hook/editor/cache probe,
API contracts and five-type sword mod metadata load also passed headlessly.
These checks do not establish physical phone frame rates or total RAM savings.

LZX counters are cumulative for the process lifetime; differences between samples
can be used for later game runs in the same process. Install over the existing
app without uninstalling. For a comparable first run use OpenGL, the same world
and only AndroidSwordTest, then capture inventory-closed and inventory-open
periods. A no-mod run provides a separate base-engine comparison.
