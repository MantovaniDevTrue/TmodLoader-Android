#!/usr/bin/env python3
"""Package managed game dependencies privately; Android FNA/core are supplied by the host."""
import argparse
from pathlib import Path
import zipfile


def package(source, destination):
    if not (source / "tModLoader.dll").is_file():
        raise SystemExit("Build the private Android managed candidate first.")
    if destination.exists():
        raise SystemExit("Payload destination already exists.")
    destination.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(source.rglob("*")):
            if not path.is_file():
                continue
            relative = path.relative_to(source).as_posix()
            if relative.startswith(("Libraries/Native/", "Libraries/tModCodeAssist/", "BuildHost-")):
                continue
            if path.name in {"FNA.dll", "Tml.Mobile.Core.dll", "FNA.dll.config"}:
                continue
            if path.suffix not in {".dll", ".json", ".config"}:
                continue
            archive.write(path, "game/" + relative)
    print(f"Private managed payload: {destination} ({destination.stat().st_size} bytes)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=lambda p: Path(p).resolve())
    parser.add_argument("destination", type=lambda p: Path(p).resolve())
    args = parser.parse_args()
    package(args.source, args.destination)
