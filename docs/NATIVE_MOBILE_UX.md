# Android native-feeling UI and input architecture

## Product direction

The Android build is a dedicated touch-first tModLoader runtime, not a desktop
launcher with an overlaid virtual mouse. Its game, items, recipes, content and
third-party mod hook order remain tModLoader's existing authoritative logic.
The Android layer supplies human-scale gestures, accessible layout, correct
system integration and frame pacing. Android framework views belong in the
launcher, file picker and text keyboard; in-game controls use the same SDL/FNA
frame and render thread as Terraria, without a second Android Canvas redraw
loop or a second gameplay simulation.

**Avoid regressing mod compatibility to mimic touch controls.** ItemSlot,
Recipe, PlayerInput and mod UI hooks must still see the input actions and
original game state transitions. Preserve hardware mouse, keyboard and
controller input alongside touch; touch must never silently claim device input
when inactive.

## First implementation: inventory pointer gestures

The input router now has a distinct `InputContext.Inventory`, enabled only
while the real `Main.playerInventory` is open. All gameplay movement and aim
buttons are removed in this context; the inventory/escape controls remain.

- **One finger:** use the real cursor and primary click, including drag/drop.
- **Second finger, stationary:** a quick two-finger tap produces a bounded
  secondary click; holding the second finger can maintain the secondary button
  for native tML stack operations.
- **Two-finger vertical swipe:** synthesize bounded mouse-wheel increments
  aimed at the primary cursor location, to support existing scrollable recipe
  and inventory UI regions. It does not draw a separate recipe list.
- **Input isolation:** an active secondary gesture suppresses the primary mouse
  button; releasing the second finger does not re-click the first. A scroll
  does not produce a final secondary click. Only the first free-screen finger
  controls pointer coordinates. HUD buttons remain independent of the pointer.
- **No duplicate Android mouse:** SDL's built-in touch-to-mouse conversion
  stays disabled, while the game receives the actual FNA `MouseState`.
  The gesture state is canceled when changing mode, resizing or pausing.

The first implementation is an **input compatibility layer**, not a finished
responsive inventory design. The existing desktop recipe list must still be
verified for actual wheel handling and position on ARM64, and the two-finger
gestures require usability tests with real mod UI. These changes do not certify
touch-friendly crafting for every mod.

## Interaction contract

| User action | Mobile behavior | Game contract |
| --- | --- | --- |
| Inventory tap | Primary click at touched slot | Original ItemSlot and mod hooks |
| Move finger while pressed | Cursor drag and held primary button | Original inventory drag/drop |
| Second-finger tap/hold | Context click/hold | Existing right-click stack logic |
| Two-finger vertical drag | Relative mouse-wheel steps | Existing focused scroll UI |
| Equipment and chest | Same slot semantics; no copies | Item identity and save/network ownership |
| Recipe selection/craft | Dedicated panel in next phase | Original Recipe availability and crafting hooks |
| Text input | Android IME in a later phase | Original chat/sign/search text consumers |
| Hardware input | Remains functional | No capture without actual touch gesture |

## Subsequent implementation phases

### A. Inventory and crafting as adaptive mobile panels

Build a responsive inventory layout around minimum comfortable tap targets,
safe areas, portrait/landscape constraints and explicit equipment/chest tabs.
Do not reimplement chest transfers, shops, NPC trades or mod slot effects.
Expose a visible crafting panel with recipe search, categories, favorites,
recipe details, material count, and create-one/create-many/hold actions.
It must call existing recipe and item transaction paths, including
`ModRecipe`/recipe groups/hooks, never invent output items directly.
Scrolling and hit-testing must be verified against the pinned game version.

### B. Full menu and navigation adaptation

Android Back should close the innermost visible panel before closing the
inventory or opening the pause menu. Text fields should invoke the Android
keyboard with proper focus, selection, enter, cancel and composition.
Make mod configuration, keybinds, housing, bestiary, map, reforging, buffs,
equipment, chest, shop and server browser panels touch-friendly. Avoid invisible
desktop hover targets; provide long-press tooltip and readable touch feedback.

### C. Combat and world controls

Retain synchronized aim and player facing before firing. Audit smart cursor,
grappling, whips, holdout weapons, minions, mounts, and contextual NPC actions.
Support custom mod keybinds through an editable in-game action palette; do
not hardcode individual mods or discard held/toggle semantics.

### D. Performance and graphics

Profile per-pass CPU drawing and render-target lifetimes before changing
buffers or caches. The 0.2.17 ARM64 log recorded about 56.5 world
presentation calls/s vs about 48.3 with inventory UI, with draw p95 around
20.7/23.9 ms. UI drawing itself is a major optimization target. Keep shader,
particle, lighting and gameplay effects. Do not simulate frame-rate gains by
removing visible effects, slowing authoritative updates or forcing GC per frame.

## Quality gates and release criteria

1. Touch actions on the live ARM64 game: tap, drag, split stack, crafted output,
   materials consumed, scrolling, inventory close and back navigation.
2. Repeat with chest, shop, equipped accessories, storage/recipe UIs from mods,
   local multiplayer and a hardware mouse attached.
3. Exercise modded recipes, inventory extensions and tile/item interactions
   without breaking loader callbacks or content ownership.
4. Verify world save/reentry after inventory transactions, pause/resume and
   orientation changes. Tests must catch stuck buttons and stale pointer IDs.
5. Compare warm world/UI frame p95, allocation/tick and Android PSS against the
   same world/camera/device/thermal baseline. Keep performance claims scoped
   to measured runs; track success separately from compatibility.

## What distinguishes this port

The project's goal is *native Android behavior throughout the actual tML game
runtime*, not merely an alternative launch screen. A launcher redesign is only
the application shell; inventory, recipes, map, mod UI, hardware lifecycle,
accessibility and performance are first-class parts of the mobile port.
