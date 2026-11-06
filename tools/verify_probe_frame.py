#!/usr/bin/env python3
"""Require a visible SpriteBatch result, rather than only a managed draw marker."""
import sys
from PIL import Image

with Image.open(sys.argv[1]) as image:
    image = image.convert("RGB")
    # The centered SeaGreen probe rectangle does not overlap any touch controls.
    region = image.crop((image.width * 2 // 5, image.height * 2 // 5,
                         image.width * 3 // 5, image.height * 3 // 5))
    pixels = list(region.getdata())
    green = sum(abs(r - 46) <= 8 and abs(g - 139) <= 8 and abs(b - 87) <= 8
                for r, g, b in pixels)
    if green < len(pixels) * 0.4:
        raise SystemExit(f"FNA sprite absent from screenshot: {green}/{len(pixels)} green pixels")
    print(f"PASS: presented FNA SpriteBatch rectangle ({green}/{len(pixels)} pixels)")
