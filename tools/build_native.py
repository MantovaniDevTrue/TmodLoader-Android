#!/usr/bin/env python3
"""Build pinned SDL3/FNA3D/FAudio and the SDK-runtime entry bridge for Android."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def prepare_gles(fna):
    """Do not mistake Android EGL's non-null unsupported stubs for GL features."""
    source = fna / "lib/FNA3D/src/FNA3D_Driver_OpenGL.c"
    text = source.read_text()
    anchor = "\t/* Some stuff is okay for ES3, not for desktop. */"
    patch = (ROOT / "integration/native/gles_capabilities.inc").read_text()
    old = "return renderer->supports_ARB_map_buffer_range;"
    new = "return renderer->supports_ARB_map_buffer_range || renderer->useES3;"
    if new not in text:
        if text.count(old) != 1:
            raise SystemExit("FNA3D NoOverwrite capability context changed")
        text = text.replace(old, new)
    if patch in text:
        source.write_text(text)
        return
    if text.count(anchor) != 1:
        raise SystemExit("FNA3D GLES capability context changed")
    source.write_text(text.replace(anchor, patch + "\n" + anchor))


def prepare_audio(fna):
    source = fna / "lib/FAudio/src/FACT.c"
    text = source.read_text()
    patch = (ROOT / "integration/native/fact_cue_cleanup.inc").read_text()
    anchor = "\t/* Remove this Cue from the SoundBank list */"
    if patch not in text:
        if text.count(anchor) != 1:
            raise SystemExit("FAudio cue cleanup context changed")
        source.write_text(text.replace(anchor, patch + "\n" + anchor))


def revision(path, expected):
    actual = subprocess.check_output(["git", "-C", str(path), "rev-parse", "HEAD"], text=True).strip()
    if actual != expected:
        raise SystemExit(f"Unexpected source revision at {path}: {actual}")


def main(args):
    lock = json.loads((ROOT / "upstream.lock.json").read_text())
    revision(args.fna, lock["fna"]["commit"])
    revision(args.sdl, lock["sdl"]["commit"])
    prepare_gles(args.fna)
    prepare_audio(args.fna)
    cmake = args.sdk / "cmake/3.22.1/bin/cmake"
    ninja = cmake.parent / "ninja"
    ndk = args.sdk / "ndk/28.2.13676358"
    output = args.output / args.abi
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run([str(cmake), "-S", str(ROOT / "integration/native"), "-B", str(output), "-G", "Ninja",
                    f"-DCMAKE_MAKE_PROGRAM={ninja}", f"-DCMAKE_TOOLCHAIN_FILE={ndk / 'build/cmake/android.toolchain.cmake'}",
                    f"-DANDROID_ABI={args.abi}", "-DANDROID_PLATFORM=android-26", "-DCMAKE_BUILD_TYPE=Release",
                    f"-DTML_SDL_SOURCE={args.sdl}", f"-DTML_FNA_SOURCE={args.fna}"], check=True)
    subprocess.run([str(cmake), "--build", str(output), "--parallel", "4", "--target",
                    "SDL3-shared", "FNA3D", "FAudio-shared", "tmlhost"], check=True)
    destination = args.output / "jniLibs" / args.abi
    destination.mkdir(parents=True, exist_ok=True)
    for name in ["libSDL3.so", "libFNA3D.so", "libFAudio.so", "libtmlhost.so"]:
        matches = [p for p in output.rglob(name) if p.is_file()]
        if len(matches) != 1:
            raise SystemExit(f"Expected exactly one {name}, found {matches}")
        shutil.copyfile(matches[0], destination / name)
    java = args.output / "java/org/libsdl/app"
    java.mkdir(parents=True, exist_ok=True)
    for source in (args.sdl / "android-project/app/src/main/java/org/libsdl/app").glob("*.java"):
        text = source.read_text()
        if source.name == "SDLActivity.java":
            before = 'new Thread(new SDLMain(), "SDLThread")'
            if text.count(before) != 1:
                raise SystemExit("SDL thread creation context changed")
            text = text.replace(before, 'new Thread(null, new SDLMain(), "SDLThread", 32L * 1024 * 1024)')
        (java / source.name).write_text(text)
    shutil.copyfile(args.sdl / "LICENSE.txt", args.output / "SDL-LICENSE.txt")
    print(f"Native Android dependencies: {destination}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sdl", type=lambda p: Path(p).resolve())
    parser.add_argument("fna", type=lambda p: Path(p).resolve())
    parser.add_argument("sdk", type=lambda p: Path(p).resolve())
    parser.add_argument("output", type=lambda p: Path(p).resolve())
    parser.add_argument("--abi", choices=["arm64-v8a", "x86_64"], default="arm64-v8a")
    main(parser.parse_args())
