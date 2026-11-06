#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build tools/Tml.RuntimeProbe -c Release -o artifacts/probe
dotnet run --project tests/Tml.Mobile.Tests -c Release -- "$PWD/artifacts/probe/Tml.RuntimeProbe.dll"
dotnet build src/Tml.Mobile.Android -c Release -t:SignAndroidPackage "$@"
mkdir -p artifacts/mobile
cp src/Tml.Mobile.Android/bin/Release/net10.0-android/android-arm64/*-Signed.apk artifacts/mobile/tML_Android_0.1.1_Controles_NET10_ARM64.apk
python3 tools/verify_mobile_apk.py artifacts/mobile/tML_Android_0.1.1_Controles_NET10_ARM64.apk
python3 tools/verify_jni_startup.py src/Tml.Mobile.Android/obj/Release/net10.0-android/android-arm64/android/src artifacts/mobile/tML_Android_0.1.1_Controles_NET10_ARM64.apk
