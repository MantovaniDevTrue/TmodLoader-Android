# Source snapshots

This directory stores sanitized source-only snapshots of important Android bootstrap baselines.

A snapshot may contain:

- native bootstrap C source;
- FNA/FNA3D bridge include files;
- Android manifest;
- build/verification scripts;
- tiny C stub sources;
- phase notes.

A snapshot must not contain:

- private signing keys;
- generated APKs;
- extracted proprietary game/runtime payloads;
- personal save data or logs.

The latest snapshot corresponds to the current baseline documented in `docs/STATUS.md`.
