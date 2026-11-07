#!/usr/bin/env python3
"""Build a private .NET 10 managed game candidate from prepared upstream sources."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

from prepare_fna import prepare

ROOT = Path(__file__).resolve().parents[1]


def run(command, cwd, environment, log):
    print(f"Running {command[1]} (log: {log})", flush=True)
    with log.open("w") as output:
        result = subprocess.run(command, cwd=cwd, env=environment, stdout=output, stderr=subprocess.STDOUT)
    if result.returncode:
        print(log.read_text(errors="replace")[-12000:], file=sys.stderr)
        raise SystemExit(f"Command failed ({result.returncode}); see {log}")


def build(upstream, destination, dotnet):
    lock = json.loads((ROOT / "upstream.lock.json").read_text())
    actual = subprocess.check_output(["git", "-C", str(upstream), "rev-parse", "HEAD"], text=True).strip()
    if actual != lock["tmodloader"]["commit"]:
        raise SystemExit(f"tModLoader revision mismatch: {actual}")
    project = upstream / "src/tModLoader/Terraria/Terraria.csproj"
    if not project.is_file():
        raise SystemExit("Generate sources with the pinned upstream setup CLI first; see docs/GAME_BUILD.md.")
    if destination.exists():
        raise SystemExit("Destination already exists; choose a new private work directory.")
    if destination == upstream or upstream.is_relative_to(destination) or destination.is_relative_to(upstream):
        raise SystemExit("Keep the private output directory separate from the upstream checkout.")
    sdk = subprocess.check_output([str(dotnet), "--version"], text=True).strip()
    if not sdk.startswith("10."):
        raise SystemExit(f"A .NET 10 SDK is required, found {sdk}")

    destination.mkdir(parents=True)
    fna = destination / "fna"
    prepare(upstream / "FNA", fna)
    payload = destination / "game"
    environment = os.environ.copy()
    environment["PATH"] = str(dotnet.parent) + os.pathsep + environment.get("PATH", "")
    command = [str(dotnet), "build", str(project), "-c", "Release", "-m:1", "-nr:false",
               "-p:BuildTasksTaskFactory=", "-v", "minimal",
               f"-p:CustomAfterMicrosoftCommonTargets={ROOT / 'integration/game/MobileGame.targets'}",
               f"-p:TmlMobileFnaProject={fna / 'FNA.Core.csproj'}",
               f"-p:tModLoaderSteamPath={payload}"]
    run(command, upstream, environment, destination / "build.log")
    bridge = destination / "mobile-integration"
    run([str(dotnet), "build", str(ROOT / "src/Tml.GameIntegration"), "-c", "Release", "-o", str(bridge),
         f"-p:GameDirectory={payload}", f"-p:FnaAssembly={fna / 'bin/Release/net10.0/FNA.dll'}"],
        ROOT, environment, destination / "mobile-integration.log")
    shutil.copyfile(bridge / "Tml.GameIntegration.dll", payload / "Tml.GameIntegration.dll")
    run([str(dotnet), "run", "--project", str(ROOT / "tools/Tml.GameCheck"), "-c", "Release",
         "--", str(payload)], ROOT, environment, destination / "assembly-check.log")

    run([str(dotnet), "run", "--project", str(ROOT / "tools/Tml.TouchProbe"), "-c", "Release",
         "--", str(payload)], ROOT, environment, destination / "touch-probe.log")

    names = {"tModLoader.dll", "FNA.dll", "ReLogic.dll", "Tml.Mobile.Core.dll", "Tml.GameIntegration.dll"}
    inventory = []
    for path in sorted(payload.rglob("*.dll")):
        if path.name in names:
            inventory.append({"path": str(path.relative_to(payload)), "bytes": path.stat().st_size,
                              "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    if {Path(item["path"]).name for item in inventory} != names:
        raise SystemExit("Managed candidate is missing required game/FNA/core assemblies.")
    report = {"upstream": actual, "fna": lock["fna"]["commit"], "sdk": sdk,
              "scope": "managed .NET 10 build and assembly loading; no Android game startup",
              "assemblies": inventory}
    (destination / "build-report.json").write_text(json.dumps(report, indent=2) + "\n")
    print(f"Managed candidate checked: {payload}")
    print("This is not a playable Android APK. Keep the generated game payload private.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("upstream", type=Path)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet:
        parser.error("Pass --dotnet /absolute/path/to/dotnet, or add the SDK to PATH.")
    build(args.upstream.resolve(), args.destination.resolve(), Path(args.dotnet).resolve())
