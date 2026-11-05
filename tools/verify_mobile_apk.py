#!/usr/bin/env python3
"""Verify packaged ABI and 16 KiB ELF/ZIP alignment; signature is checked with apksigner."""
import argparse
import hashlib
import json
import pathlib
import struct
import zipfile


def verify(path):
    libraries = []
    with zipfile.ZipFile(path) as apk, open(path, "rb") as raw:
        for item in apk.infolist():
            if not item.filename.startswith("lib/") or not item.filename.endswith(".so"):
                continue
            if not item.filename.startswith("lib/arm64-v8a/"):
                raise ValueError(f"Unexpected ABI: {item.filename}")
            data = apk.read(item)
            if data[:6] != b"\x7fELF\x02\x01" or struct.unpack_from("<H", data, 18)[0] != 183:
                raise ValueError(f"Not little-endian AArch64 ELF64: {item.filename}")
            phoff = struct.unpack_from("<Q", data, 32)[0]
            phsize, phcount = struct.unpack_from("<HH", data, 54)
            loads = 0
            for index in range(phcount):
                off = phoff + index * phsize
                if struct.unpack_from("<I", data, off)[0] != 1:
                    continue
                loads += 1
                file_offset, virtual_address = struct.unpack_from("<QQ", data, off + 8)
                alignment = struct.unpack_from("<Q", data, off + 48)[0]
                if alignment < 16384 or (virtual_address-file_offset) % 16384:
                    raise ValueError(f"ELF LOAD segment is not 16 KiB compatible: {item.filename}")
            if not loads:
                raise ValueError(f"No ELF LOAD segments: {item.filename}")
            if item.compress_type == zipfile.ZIP_STORED:
                raw.seek(item.header_offset + 26)
                name_size, extra_size = struct.unpack("<HH", raw.read(4))
                offset = item.header_offset + 30 + name_size + extra_size
                if offset % 16384:
                    raise ValueError(f"Uncompressed library is not ZIP-aligned: {item.filename}")
            libraries.append(item.filename)
        if not libraries:
            raise ValueError("No native ARM64 libraries packaged")
        if "assets/Tml.RuntimeProbe.dll" not in apk.namelist():
            raise ValueError("External assembly runtime probe is missing")
    return {"apk": pathlib.Path(path).name, "sha256": hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest(),
            "abi": "arm64-v8a", "elf_page_alignment": 16384, "native_libraries": libraries,
            "scope": "Input/runtime diagnostic APK; does not launch Terraria"}


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("apk", type=pathlib.Path)
    args = parser.parse_args()
    print(json.dumps(verify(args.apk), indent=2))
