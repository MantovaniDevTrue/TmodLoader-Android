#!/usr/bin/env python3
"""Check dynamic JNI registration in generated Java and packaged DEX metadata."""
import argparse
import pathlib
import zipfile


def verify(java_root, apk_path, game_host=False):
    callbacks = {
        "MainActivity": ["n_onCreate", "n_onPause", "n_onResume", "n_onWindowFocusChanged"],
        "TouchControlsView": ["n_onDraw", "n_onTouchEvent", "n_onSizeChanged"],
    }
    if game_host:
        callbacks["MainActivity"] = ["n_onCreate", "n_onActivityResult"]
        callbacks["GameEntry"] = ["n_Attach", "n_RunGame", "n_SuspendInput", "n_ResumeInput", "n_Detach"]
    with zipfile.ZipFile(apk_path) as apk:
        dex = b"".join(apk.read(name) for name in apk.namelist() if name.endswith(".dex"))
    if not dex:
        raise ValueError("No DEX payload")
    for name, methods in callbacks.items():
        matches = list(pathlib.Path(java_root).rglob(name + ".java"))
        if len(matches) != 1:
            raise ValueError(f"Expected exactly one generated wrapper for {name}")
        java = matches[0].read_text()
        if "mono.android.Runtime.register" not in java or "__md_methods" not in java:
            raise ValueError(f"Dynamic JNI registration missing for {name}")
        for method in methods:
            # A native method declaration alone does not register its managed implementation.
            if method + ":" not in java:
                raise ValueError(f"Missing JNI registration descriptor: {name}.{method}")
            if (method + ":").encode() not in dex:
                raise ValueError(f"Registration descriptor missing from APK DEX: {name}.{method}")
        print(f"PASS: {name}: generated and packaged dynamic JNI registration")
    print("JNI metadata checks passed; device launch validation is separate.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("java_root", type=pathlib.Path)
    parser.add_argument("apk", type=pathlib.Path)
    parser.add_argument("--game-host", action="store_true")
    args = parser.parse_args()
    verify(args.java_root, args.apk, game_host=args.game_host)
