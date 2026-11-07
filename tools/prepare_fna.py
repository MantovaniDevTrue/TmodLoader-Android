#!/usr/bin/env python3
"""Apply the mobile input extension to a clean, pinned FNA working copy."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]


def prepare(source: pathlib.Path, destination: pathlib.Path):
    lock = json.loads((ROOT / "upstream.lock.json").read_text())
    actual = subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if actual != lock["fna"]["commit"]:
        raise SystemExit(f"FNA revision mismatch: {actual}; expected {lock['fna']['commit']}")
    if destination.exists():
        raise SystemExit("Destination already exists; choose an empty path.")
    replacements = {
        "src/FNAPlatform/SDL3_FNAPlatform.cs": [
            ("\t\t\tUIntPtr resultSize;\n\t\t\tIntPtr result = SDL.SDL_LoadFile(path, out resultSize);\n\t\t\tsize = (IntPtr) resultSize.ToPointer();\n\t\t\treturn result;",
             "\t\t\treturn MobileFileIOEXT.ReadFile(path, out size);", 1)],
        "src/Audio/WaveBank.cs": [
            ("bankData = FAudio.FAudio_fopen(safeName);", "bankData = MobileFileIOEXT.OpenStreamingFile(safeName);", 1),
            ("if (AudioEngine.ProgramExiting)", "if (engine == null || AudioEngine.ProgramExiting)", 1)],
        "src/Audio/SoundBank.cs": [
            ("if (AudioEngine.ProgramExiting)", "if (engine == null || AudioEngine.ProgramExiting)", 1)],
        "src/Input/KeyboardState.cs": [("\t\tvoid AddPressedKey(int key)", "\t\tinternal void AddPressedKey(int key)", 1)],
        "src/Input/Keyboard.cs": [("return new KeyboardState(keys);", "return MobileInputEXT.MergeKeyboard(new KeyboardState(keys));", 2)],
        "src/Input/Mouse.cs": [
            ("return new MouseState(", "return MobileInputEXT.MergeMouse(new MouseState(", 1),
            ("\t\t\t\tx2\n\t\t\t);", "\t\t\t\tx2\n\t\t\t));", 1)],
        "src/Game.cs": [
            ("\n\t\t\tAdvanceElapsedTime();", "\n\t\t\tMobilePerformanceEXT.BeginFrame();\n\t\t\tAdvanceElapsedTime();", 1),
            ("\n\t\t\t\t\tUpdate(gameTime);", "\n\t\t\t\t\tlong mobileUpdateStart = MobilePerformanceEXT.BeginSection();\n\t\t\t\t\tMobileInputEXT.BeginUpdate();\n\t\t\t\t\tUpdate(gameTime);\n\t\t\t\t\tMobilePerformanceEXT.EndUpdate(mobileUpdateStart);", 1),
            ("\n\t\t\t\tUpdate(gameTime);", "\n\t\t\t\tlong mobileUpdateStart = MobilePerformanceEXT.BeginSection();\n\t\t\t\tMobileInputEXT.BeginUpdate();\n\t\t\t\tUpdate(gameTime);\n\t\t\t\tMobilePerformanceEXT.EndUpdate(mobileUpdateStart);", 1),
            ("\t\t\t\t\tDraw(gameTime);\n\t\t\t\t\tEndDraw();", "\t\t\t\t\tlong mobileDrawStart = MobilePerformanceEXT.BeginSection();\n\t\t\t\t\tDraw(gameTime);\n\t\t\t\t\tMobilePerformanceEXT.EndDraw(mobileDrawStart);\n\t\t\t\t\tlong mobilePresentStart = MobilePerformanceEXT.BeginSection();\n\t\t\t\t\tEndDraw();\n\t\t\t\t\tMobilePerformanceEXT.EndPresent(mobilePresentStart);", 1),
            ("\t\t\t\t\tEndDraw();\n\t\t\t\t\tMobilePerformanceEXT.EndPresent(mobilePresentStart);\n\t\t\t\t}\n\t\t\t}\n\t\t}\n", "\t\t\t\t\tEndDraw();\n\t\t\t\t\tMobilePerformanceEXT.EndPresent(mobilePresentStart);\n\t\t\t\t}\n\t\t\t}\n\t\t\tMobilePerformanceEXT.EndFrame();\n\t\t}\n", 1)],
    }
    # Validate all edits before copying or writing anything.
    modified = {}
    for filename, edits in replacements.items():
        text = (source / filename).read_text()
        expected = lock["fna"]["files"][filename]
        if hashlib.sha256((source / filename).read_bytes()).hexdigest() != expected:
            raise SystemExit(f"Modified upstream input file: {filename}")
        for old, new, count in edits:
            if text.count(old) != count:
                raise SystemExit(f"Patch context mismatch in {filename}: {old!r}")
            text = text.replace(old, new)
        modified[filename] = text
    shutil.copytree(source, destination, ignore=shutil.ignore_patterns(".git", "bin", "obj"))
    for filename, text in modified.items():
        (destination / filename).write_text(text)
    shutil.copyfile(ROOT / "integration/fna/MobileInputEXT.cs", destination / "src/Input/MobileInputEXT.cs")
    shutil.copyfile(ROOT / "integration/fna/MobilePerformanceEXT.cs", destination / "src/MobilePerformanceEXT.cs")
    shutil.copyfile(ROOT / "integration/fna/MobileFileIOEXT.cs", destination / "src/MobileFileIOEXT.cs")
    project = destination / "FNA.Core.csproj"
    text = project.read_text()
    reference = ROOT / "src/Tml.Mobile.Core/Tml.Mobile.Core.csproj"
    # Keep upstream's explicit source list; add only the extension and its shared state assembly.
    from xml.sax.saxutils import escape
    text = text.replace("</Project>", f'''  <ItemGroup>
    <Compile Include="src/Input/MobileInputEXT.cs" />
    <Compile Include="src/MobilePerformanceEXT.cs" />
    <Compile Include="src/MobileFileIOEXT.cs" />
    <ProjectReference Include="{escape(str(reference))}" />
  </ItemGroup>
</Project>''')
    project.write_text(text)
    print(project)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=pathlib.Path)
    parser.add_argument("destination", type=pathlib.Path)
    args = parser.parse_args()
    prepare(args.source.resolve(), args.destination.resolve())
