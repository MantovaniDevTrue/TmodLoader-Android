# Content layout on Android

tModLoader does not treat its own `Content` directory as a replacement for Terraria's vanilla assets.

Upstream tModLoader uses two layers:

1. the **vanilla Terraria PC Content tree** as the base content source;
2. the local **tModLoader Content tree** as an optional override source.

The Android port should preserve that model.

## Preferred layout

```text
/storage/emulated/0/Android/media/com.mantovani.tmlmono8/
├── Terraria/
│   └── Content/
│       ├── Images/
│       ├── Fonts/
│       ├── Sounds/
│       └── ...
├── Content/
│   ├── PixelShader.xnb
│   ├── TileShader.xnb
│   ├── ScreenShader.xnb
│   └── other tModLoader/platform overrides
└── work/
```

The vanilla tree must come from a legitimate desktop Terraria installation matching the tModLoader generation being tested.

Do not substitute Terraria Mobile assets as the vanilla desktop content tree. Asset names, layouts and platform-specific compiled content are not guaranteed to match what desktop tModLoader/FNA expects.

## Current canaries

The bootstrap already checks for:

- `Images/Projectile_651.xnb`;
- `Images/Projectile_981.xnb`.

Phase 2.23.01 additionally proved that a tree can pass those two canaries and still be incomplete for desktop startup. The current missing asset is:

- `Images/SplashScreens/Splash_1.xnb`.

Future validation should verify a broader desktop-content signature before selecting a root.

## Why not copy individual files forever?

Once `Main.LoadContent` begins loading vanilla assets, many thousands of textures, fonts, data files and sounds become reachable. Supplying one missing XNB at a time only moves the failure forward.

The correct solution is to stage the complete matching vanilla Terraria PC `Content` directory once, then let the existing tModLoader/FNA asset system load from it normally.

## Public repository policy

The repository documents the expected layout but does not redistribute Terraria's proprietary Content files.
