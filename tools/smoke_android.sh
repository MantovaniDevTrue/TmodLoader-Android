#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
apk_path="${1:?Pass the diagnostic APK path}"
package_name=com.mantovani.tmlmobile
adb install -r "$apk_path"
adb logcat -c
adb shell am start -W -n "$package_name/crc645f428b8dbfb46340.MainActivity"
mkdir -p artifacts/smoke
for attempt in $(seq 1 30); do
    adb logcat -d -v brief > artifacts/smoke/logcat.txt
    if grep -Eq 'FATAL EXCEPTION|Fatal signal|UnsatisfiedLinkError' artifacts/smoke/logcat.txt; then
        cat artifacts/smoke/logcat.txt
        exit 1
    fi
    if grep -q MOBILE_STARTUP_READY artifacts/smoke/logcat.txt &&
       grep -q MOBILE_FIRST_DRAW artifacts/smoke/logcat.txt &&
       grep -q MOBILE_RUNTIME_PROBE_OK artifacts/smoke/logcat.txt; then
        adb shell input tap 200 200
        sleep 2
        adb shell pidof "$package_name"
        adb shell input keyevent KEYCODE_HOME
        sleep 1
        adb shell am start -W -n "$package_name/crc645f428b8dbfb46340.MainActivity"
        sleep 2
        adb shell pidof "$package_name"
        adb logcat -d -v brief > artifacts/smoke/logcat.txt
        if grep -Eq 'FATAL EXCEPTION|Fatal signal|UnsatisfiedLinkError' artifacts/smoke/logcat.txt; then
            cat artifacts/smoke/logcat.txt
            exit 1
        fi
        grep -q MOBILE_TOUCH_RECEIVED artifacts/smoke/logcat.txt
        adb shell screencap -p /sdcard/tml-mobile.png
        adb pull /sdcard/tml-mobile.png artifacts/smoke/tml-mobile.png
        echo 'PASS: Activity started, first draw completed, touch callback ran, DLL/Emit probes passed'
        exit 0
    fi
    sleep 1
done
cat artifacts/smoke/logcat.txt
echo 'Startup markers did not arrive' >&2
exit 1
