# Current status

## Latest packaged build: 0.2.17

Tested on a physical ARM64 phone with OpenGL:

- AndroidSwordTest 0.1.1 loads; the session enters a world, saves in about 2.8 s and exits normally.
- World: median 56.5 presents/s, draw p95 about 20.7 ms.
- Inventory open: median 48.3 presents/s, draw p95 about 23.9 ms.
- Update p95 about 3.1 ms; process PSS about 2.29 GiB, of which graphics about 1.52 GiB.

Details: [0.2.17 release notes](releases/0.2.17.md).

## In source: 0.2.18

Touch-first inventory input: one-finger drag, two-finger context tap and hold, and two-finger scrolling through the real FNA mouse state. No 0.2.18 APK exists yet. It must be built in the private environment with the same signing key as 0.2.17. See [0.2.18 release notes](releases/0.2.18.md).

## Known limits

- Vulkan failed at startup on the test Adreno device; OpenGL is the default.
- Only a simple local mod has been tested on the phone. General PC mod compatibility is not established.
- Memory use is high: graphics resources account for most of the process PSS.
- Responsive inventory and crafting panels, Android keyboard input and mod configuration UI are not implemented yet.

## Next

1. Package and test 0.2.18 on the phone.
2. Profile drawing sub-passes and GPU resource lifetime before changing rendering behaviour.
3. Mobile crafting panel, then menus, Android Back and keyboard input. See [mobile UI direction](NATIVE_MOBILE_UX.md).
4. Test larger content mods.
