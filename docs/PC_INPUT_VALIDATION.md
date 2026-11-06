# Private Terraria PC input validation

Validated on 2026-11-06 from the installation supplied by the user. Proprietary executable/content bytes, Steam authentication data and private download links are excluded from this repository.

## Results

| Check | Observed result |
| --- | --- |
| Archive transport | Four ordered parts: 209,715,200 + 209,715,200 + 209,715,200 + 11,831,039 bytes |
| Reconstructed ZIP | 640,976,639 bytes; all entry CRC checks passed |
| Archive entries | 16,113, under a single Terraria-PC root |
| Expanded archive | 805,920,902 bytes |
| Client version | Terraria.exe version strings and changelog identify 1.4.5.8 |
| Client executable | 26,597,888 bytes; SHA-256 below |
| Vanilla Content | 15,997 files, 739,703,531 bytes |
| XNB structural checks | All 15,994 XNB files have a ten-byte header, XNB signature and declared length matching actual file length |
| Audio banks | One XSB, one XWB and one XGS present |
| Known startup assets | Splash_1, Item_1, Tiles_0 and all three shader XNB files present |

The installation ZIP was reconstructed successfully from the supplied parts. Its size differs from the earlier unsplit upload; no byte-for-byte equality with that earlier upload is claimed. The reconstructed archive itself passed CRC validation.

Terraria.exe SHA-256:
`960a03bff6050cf7be16dfc1a7b19e10fc2c4f8f835a6a3b135a50dd9e6ba2f3`

## Known startup assets

Paths are relative to the private vanilla Content root.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| Images/SplashScreens/Splash_1.xnb | 145792 | d553a343f75fb595d42e1228e9b5d5570936b690ca529650aa327d698266cd4c |
| Images/Item_1.xnb | 406 | c0a15e421a47b2e2276b511fde28166bd5695c98f06d6823a7bfd4038c10bf39 |
| Images/Tiles_0.xnb | 5048 | 8a1cb661c61b916bae057dda8ac2e061fd35ea69e0030dfd1af878a054fce1e6 |
| PixelShader.xnb | 13808 | 5332a581222f78e9e498efbe044162ccd82f5df286c859d5f522b4536edbc51b |
| TileShader.xnb | 2376 | 7f8bbee3b688b80f661143de65ac38ae2114060e4c0fa44af39f4ec32a7bf602 |
| ScreenShader.xnb | 7528 | ac4b2f25697444f01b089f6a18453c2d93da829a1e5e83182537701ca3ff4e64 |

## What this establishes

The matching vanilla executable and a structurally intact supplied Content tree are available for the selected upstream setup. The particular splash asset missing in the historical Android run is present in this input.

These checks do not certify the installation against an authoritative Steam manifest, decode every asset, execute the game, establish Android shader compatibility or measure FPS. Windows shader files remain vanilla input; FNA/platform overrides must be supplied and validated separately.

## Next integration boundary

1. Use the vanilla 1.4.5.8 executable as the selected upstream setup/decompilation input and build the corresponding tModLoader managed payload. The vanilla executable alone is not tModLoader.
2. Establish the Android SDL/FNA surface and dependencies in the SDK-managed .NET 10 process.
3. Configure the private vanilla Content root and separate tModLoader/platform override root.
4. Reach the real menu, validate lifecycle/audio/graphics, then test a small mod and world save/reentry.

No new playable APK was built during input validation.
