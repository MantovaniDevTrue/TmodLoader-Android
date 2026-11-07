#!/usr/bin/env python3
"""Apply the initial Android platform edits to generated, pinned tModLoader sources."""
import argparse
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def prepare(upstream):
    lock = json.loads((ROOT / "upstream.lock.json").read_text())
    commit = subprocess.check_output(["git", "-C", str(upstream), "rev-parse", "HEAD"], text=True).strip()
    if commit != lock["tmodloader"]["commit"]:
        raise SystemExit("Unexpected tModLoader source revision")
    edits = {
        "Terraria/ModLoader/Engine/InstallVerifier.cs": (
            "\tinternal static void Startup()\n\t{\n",
            """\tinternal static void Startup()
\t{
\t\tif (OperatingSystem.IsAndroid()) {
\t\t\tstring directory = Environment.GetEnvironmentVariable("TML_ANDROID_TERRARIA")
\t\t\t\t?? throw new InvalidOperationException("Android Terraria directory was not configured.");
\t\t\tvanillaExePath = Path.Combine(directory, "Terraria.exe");
\t\t\tusing var executable = File.OpenRead(vanillaExePath);
\t\t\tstring hash = Convert.ToHexString(SHA256.HashData(executable));
\t\t\tif (!hash.Equals("960a03bff6050cf7be16dfc1a7b19e10fc2c4f8f835a6a3b135a50dd9e6ba2f3", StringComparison.OrdinalIgnoreCase))
\t\t\t\tthrow new InvalidDataException("Import the matching owned Terraria PC 1.4.5.8 executable.");
\t\t\tDistributionPlatform = DistributionPlatform.Unknown;
\t\t\tLogging.tML.Info("Android local installation verified against the supplied PC executable.");
\t\t\treturn;
\t\t}
"""),
        "Terraria/MonoLaunch.cs": (
            'public static readonly string NativesDir = Path.Combine(Environment.CurrentDirectory, "Libraries", "Native", NativePlatformDir);',
            '''public static readonly string NativesDir = OperatingSystem.IsAndroid()
\t\t? Environment.GetEnvironmentVariable("TML_ANDROID_NATIVE_DIR") ?? throw new InvalidOperationException("Android native directory was not configured.")
\t\t: Path.Combine(Environment.CurrentDirectory, "Libraries", "Native", NativePlatformDir);'''),
        "Terraria/Social/Steam/SteamedWraps.cs": (
            "\t\tInitializeModTags();\n",
            "\t\tInitializeModTags();\n\n\t\tif (OperatingSystem.IsAndroid())\n\t\t\treturn;\n"),
        "Terraria/ModLoader/Engine/LoggingHooks.cs": (
            "\tinternal static void Init()\n\t{\n",
            "\tinternal static void Init()\n\t{\n\t\tif (OperatingSystem.IsAndroid())\n\t\t\treturn;\n"),
        "Terraria/Program.cs": (
            'MainThread.Name = "Main Thread";', 'MainThread.Name ??= "Main Thread";'),
        "Terraria/ModLoader/Logging.cs": (
            'Assembly.GetEntryAssembly().Location',
            '(Assembly.GetEntryAssembly() ?? typeof(Logging).Assembly).Location')
    }
    changes = []
    for name, (before, after) in edits.items():
        path = upstream / "src/tModLoader" / name
        text = path.read_text(encoding="utf-8-sig")
        if after in text:
            continue
        if text.count(before) != 1:
            raise SystemExit(f"Android patch context changed: {name}")
        changes.append((path, text.replace(before, after)))
    for path, text in changes:
        path.write_text(text)
    print(f"Applied {len(changes)} Android source edits; no game files were copied to the Android repository.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("upstream", type=lambda p: Path(p).resolve())
    prepare(parser.parse_args().upstream)
