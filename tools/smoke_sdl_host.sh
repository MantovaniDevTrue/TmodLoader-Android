#!/usr/bin/env bash
set -euo pipefail
apk="$1"
backend="${2:-OpenGL}"
mkdir -p artifacts/sdl-smoke
adb shell am force-stop com.mantovani.tmlmobile || true
trap 'adb logcat -d > artifacts/sdl-smoke/logcat.txt || true; adb exec-out screencap -p > artifacts/sdl-smoke/frame.png || true' EXIT
adb install -r "$apk"
adb logcat -c
adb shell input keyevent 224
adb shell wm dismiss-keyguard || true
adb shell monkey -p com.mantovani.tmlmobile -c android.intent.category.LAUNCHER 1
if [ "$backend" == SDLGPU ]; then
  for label in Renderizador 'Vulkan · experimental'; do
    found=0
    for attempt in $(seq 1 15); do
      timeout 20 adb shell uiautomator dump /sdcard/tml-window.xml >/dev/null 2>&1 || true
      adb shell cat /sdcard/tml-window.xml > artifacts/sdl-smoke/window.xml
      if python3 tools/find_android_control.py artifacts/sdl-smoke/window.xml "$label" > /tmp/tml-renderer-tap.txt; then
        read -r tap_x tap_y < /tmp/tml-renderer-tap.txt
        adb shell input tap "$tap_x" "$tap_y"
        found=1
        break
      fi
      sleep 1
    done
    if [ "$found" != 1 ]; then echo "Renderer control missing: $label"; exit 1; fi
  done
fi
found_button=0
for attempt in $(seq 1 12); do
  timeout 20 adb shell uiautomator dump /sdcard/tml-window.xml >/dev/null 2>&1 || true
  adb shell cat /sdcard/tml-window.xml > artifacts/sdl-smoke/window.xml
  if python3 - <<'PY' > /tmp/tml-sdl-tap.txt
import re, xml.etree.ElementTree as ET
try:
    tree = ET.parse('artifacts/sdl-smoke/window.xml')
except ET.ParseError:
    raise SystemExit(1)
for node in tree.iter('node'):
    if node.get('text', '').casefold() == 'testar gráficos e controles':
        a = list(map(int, re.findall(r'\d+', node.get('bounds'))))
        print((a[0]+a[2])//2, (a[1]+a[3])//2)
        break
else:
    raise SystemExit(1)
PY
  then
    read -r tap_x tap_y < /tmp/tml-sdl-tap.txt
    adb exec-out screencap -p > artifacts/sdl-smoke/launcher.png
    adb shell input tap "$tap_x" "$tap_y"
    found_button=1
    break
  fi
  sleep 2
done
if [ "$found_button" != 1 ]; then
  echo 'Graphics probe button not found in launcher'
  exit 1
fi
for attempt in $(seq 1 45); do
  adb logcat -d > artifacts/sdl-smoke/logcat.txt
  if grep -q 'SDL_FNA_FIRST_FRAME' artifacts/sdl-smoke/logcat.txt; then
    grep -q 'HOST_ENTRY_ASSEMBLY_READY Tml.Game.Android' artifacts/sdl-smoke/logcat.txt
    grep -q 'ANDROID_DEFERRED_BOOTSTRAP_OK' artifacts/sdl-smoke/logcat.txt
    grep -q 'ANDROID_PC_RESOLUTION_STABLE' artifacts/sdl-smoke/logcat.txt
    grep -q 'ANDROID_GPU_STRESS_OK' artifacts/sdl-smoke/logcat.txt
    if [ "$backend" == OpenGL ]; then
      grep -q 'ANDROID_GLES_STREAMING subdata+attribute-offsets+float-depth+ring-reuse' artifacts/sdl-smoke/logcat.txt
    else
      grep -q 'FNA3D Driver: SDLGPU' artifacts/sdl-smoke/logcat.txt
      grep -qi 'vulkan' artifacts/sdl-smoke/logcat.txt
    fi
    grep -q 'ANDROID_DISPLAY_STABLE' artifacts/sdl-smoke/logcat.txt
    grep -q 'ANDROID_DMD_BACKEND DynamicMethod' artifacts/sdl-smoke/logcat.txt
    grep -q 'ANDROID_PRIVATE_ACCESS_OK' artifacts/sdl-smoke/logcat.txt
    grep -q 'ANDROID_DETOUR_PROBE_OK' artifacts/sdl-smoke/logcat.txt
    grep -q 'NATIVE_RELATIVE_PATH_MISMATCH_REPRODUCED' artifacts/sdl-smoke/logcat.txt
    grep -q 'NATIVE_FILE_IO_OK' artifacts/sdl-smoke/logcat.txt
    adb exec-out screencap -p > artifacts/sdl-smoke/input-surface.png
    read -r screen_w screen_h < <(python3 - <<'PYSCREEN'
from PIL import Image
with Image.open('artifacts/sdl-smoke/input-surface.png') as image:
    print(image.width, image.height)
PYSCREEN
)
    adb shell input tap "$((screen_w / 2))" "$((screen_h / 2))"
    native_touch=0
    for touch_attempt in $(seq 1 15); do
      adb logcat -d > artifacts/sdl-smoke/logcat.txt
      if grep -q 'ANDROID_NATIVE_TOUCH_EVENT sdl-to-game-thread' artifacts/sdl-smoke/logcat.txt; then native_touch=1; break; fi
      sleep 1
    done
    if [ "$native_touch" != 1 ]; then echo 'Native SDL touch did not reach the game thread'; exit 1; fi
    adb exec-out screencap -p > artifacts/sdl-smoke/frame.png
    python3 tools/verify_probe_frame.py artifacts/sdl-smoke/frame.png
    echo 'PASS: SDK-managed .NET entry, SDL3 Android surface, FNA3D texture/SpriteBatch frame'
    exit 0
  fi
  if grep -Eq 'HOST_ERROR|FATAL EXCEPTION' artifacts/sdl-smoke/logcat.txt; then
    tail -n 100 artifacts/sdl-smoke/logcat.txt
    exit 1
  fi
  sleep 2
done
echo 'No FNA frame observed'
tail -n 100 artifacts/sdl-smoke/logcat.txt
exit 1
