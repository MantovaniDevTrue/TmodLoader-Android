#!/usr/bin/env python3
"""Stage private host PE images for metadata-only mod inspection on Android."""
import argparse
from pathlib import Path
import shutil


def stage(game):
    destination = game / "Metadata"
    destination.mkdir(exist_ok=True)
    for name in ("FNA", "Tml.Mobile.Core"):
        source = game / (name + ".dll")
        if source.read_bytes()[:2] != b"MZ":
            raise SystemExit(f"Invalid host PE image: {source}")
        target = destination / (name + ".metadata.dll")
        shutil.copyfile(source, target)
        print(f"Metadata-only host image: {target.name}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("game", type=lambda p: Path(p).resolve())
    stage(parser.parse_args().game)
