# Architecture

## Overview

The APK is a small Android ARM64 host that launches the desktop tModLoader/FNA managed stack through MonoVM.

The native bootstrap is responsible for:

1. preparing Android storage/runtime paths;
2. loading the native runtime and required libraries;
3. configuring MonoVM;
4. resolving managed assemblies;
5. installing Android-specific InternalCall bridges where the original desktop interop wrapper cannot execute correctly;
6. constructing and driving Terraria/tModLoader through a controlled `Game.RunOneFrame()` loop;
7. writing detailed state and first-chance exception diagnostics.

## Why InternalCall bridges exist

Most desktop FNA interop is naturally expressed through P/Invoke. In the current Android MonoVM environment, specific generated managed-to-native wrappers have produced invalid `calli` IL.

For a confirmed failing method, the bootstrap can:

- resolve the real symbol from the APK native library;
- inspect the actual `MonoMethod`;
- convert that method metadata from P/Invoke to InternalCall before first JIT;
- register an ABI-compatible native bridge with Mono.

This is only done after a failure is proven in logs.

## ARM64 tagged pointers

Android ARM64 can expose pointers with a top-byte tag. A tagged pointer is still valid for the runtime, but direct lookup against `/proc/self/maps` requires the canonical address.

The bootstrap therefore:

- preserves the original tagged pointer for Mono object/method access;
- strips the top-byte tag only for page lookup/protection calculations.

This distinction was required for the successful FNA3D metadata patch.

## Game-thread stack

A normal Android pthread stack was insufficient for the very large desktop Terraria static initialization path used by `NPCID.Sets` and its bestiary helper data.

The bootstrap creates the game bootstrap worker with a **32 MiB stack**. This is currently a compatibility requirement, not an optimization.

## Source-of-truth policy

The project uses upstream tModLoader/FNA/FNA3D source to understand expected behavior and signatures. Android compatibility changes are kept in the bootstrap rather than changing gameplay/mod semantics unless a managed patch becomes strictly necessary.
