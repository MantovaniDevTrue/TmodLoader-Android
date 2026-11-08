#!/usr/bin/env bash
set -euo pipefail
apk="$1"
backend="${2:-OpenGL}"
mkdir -p artifacts/sdl-smoke
echo "SDL_SMOKE_BACKEND=$backend"
require_marker() {
  local marker="$1"
  if ! grep -Fq -- "$marker" artifacts/sdl-smoke/logcat.txt; then
    echo "SDL_SMOKE_MISSING_MARKER backend=$backend marker=$marker"
    grep -E 'HOST_|ANDROID_|SDL_FNA|FNA3D|FATAL EXCEPTION' artifacts/sdl-smoke/logcat.txt | tail -n 80 || true
    exit 1
  fi
}
adb shell am force-stop com.mantovani.tmlmobile || true
trap 'adb logcat -d > artifacts/sdl-smoke/logcat.txt || true; adb exec-out screencap -p > artifacts/sdl-smoke/frame.png || true' EXIT
adb install -r "$apk"
adb logcat -c
adb shell input keyevent 224
adb shell wm dismiss-keyguard || true
adb shell monkey -p com.mantovani.tmlmobile -c android.intent.category.LAUNCHER 1
scroll_launcher() {
  adb exec-out screencap -p > artifacts/sdl-smoke/navigation.png
  read -r view_w view_h < <(python3 - <<'PYVIEW'
from PIL import Image
with Image.open('artifacts/sdl-smoke/navigation.png') as image:
    print(image.width,image.height)
PYVIEW
)
  adb shell input swipe "$((view_w / 2))" "$((view_h * 85 / 100))" "$((view_w / 2))" "$((view_h * 30 / 100))" 250
}
tap_launcher() {
  local label="$1"
  for attempt in $(seq 1 12); do
    timeout 20 adb shell uiautomator dump /sdcard/tml-window.xml >/dev/null 2>&1 || true
    adb shell cat /sdcard/tml-window.xml > artifacts/sdl-smoke/window.xml
    if python3 tools/find_android_control.py artifacts/sdl-smoke/window.xml "$label" > /tmp/tml-nav-tap.txt; then
      read -r tap_x tap_y < /tmp/tml-nav-tap.txt
      adb shell input tap "$tap_x" "$tap_y"
      sleep 1
      return
    fi
    scroll_launcher
    sleep 1
  done
  echo "Launcher control missing: $label"; exit 1
}
tap_launcher launcher-home
adb exec-out screencap -p > artifacts/sdl-smoke/launcher-home.png
tap_launcher launcher-mods
adb exec-out screencap -p > artifacts/sdl-smoke/launcher-mods.png
tap_launcher launcher-disable-all
adb root
adb wait-for-device
adb shell cat /data/user/0/com.mantovani.tmlmobile/files/Saves/tModLoader/Mods/enabled.json > artifacts/sdl-smoke/enabled.json
python3 -c 'import json; assert json.load(open("artifacts/sdl-smoke/enabled.json")) == []'
tap_launcher launcher-files
adb exec-out screencap -p > artifacts/sdl-smoke/launcher-files.png
tap_launcher launcher-settings
adb exec-out screencap -p > artifacts/sdl-smoke/launcher-settings.png
if [ "$backend" == OpenGL ]; then
  tap_launcher launcher-language
  tap_launcher English
  timeout 20 adb shell uiautomator dump /sdcard/tml-window.xml >/dev/null 2>&1 || true
  adb shell cat /sdcard/tml-window.xml > artifacts/sdl-smoke/launcher-english.xml
  grep -q 'text="Settings"' artifacts/sdl-smoke/launcher-english.xml
  adb exec-out screencap -p > artifacts/sdl-smoke/launcher-english.png
  tap_launcher launcher-language
  tap_launcher 'Português (Brasil)'
fi
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
      scroll_launcher
      sleep 1
    done
    if [ "$found" != 1 ]; then echo "Renderer control missing: $label"; exit 1; fi
  done
fi
tap_launcher launcher-diagnostics
adb exec-out screencap -p > artifacts/sdl-smoke/launcher-diagnostics.png
if [ "$backend" == OpenGL ]; then
  tap_launcher launcher-export-report
  adb exec-out screencap -p > artifacts/sdl-smoke/export-picker.png
  saved=0
  for attempt in $(seq 1 6); do
    timeout 20 adb shell uiautomator dump /sdcard/tml-window.xml >/dev/null 2>&1 || true
    adb shell cat /sdcard/tml-window.xml > artifacts/sdl-smoke/export-window.xml
    for label in SAVE Save SALVAR Salvar; do
      if python3 tools/find_android_control.py artifacts/sdl-smoke/export-window.xml "$label" > /tmp/tml-save-tap.txt; then
        read -r tap_x tap_y < /tmp/tml-save-tap.txt
        adb shell input tap "$tap_x" "$tap_y"
        saved=1
        break
      fi
    done
    if [ "$saved" == 1 ]; then break; fi
    if python3 tools/find_android_control.py artifacts/sdl-smoke/export-window.xml Downloads > /tmp/tml-save-tap.txt; then
      read -r tap_x tap_y < /tmp/tml-save-tap.txt
      adb shell input tap "$tap_x" "$tap_y"
    fi
    sleep 1
  done
  if [ "$saved" != 1 ]; then echo 'TXT document picker save control missing'; exit 1; fi
  sleep 2
  adb shell find /sdcard/Download -maxdepth 1 -name 'tML-Android-diagnostico-*.txt' > /tmp/tml-export-path.txt
  document=$(tr -d '\r' < /tmp/tml-export-path.txt | tail -n 1)
  test -n "$document"
  adb pull "$document" artifacts/sdl-smoke/diagnostics-export.txt
  python3 -c 'from pathlib import Path; data=Path("artifacts/sdl-smoke/diagnostics-export.txt").read_text(encoding="utf-8"); assert data.startswith("tML Android 0.2.18")'
fi
found_button=0
for attempt in $(seq 1 12); do
  timeout 20 adb shell uiautomator dump /sdcard/tml-window.xml >/dev/null 2>&1 || true
  adb shell cat /sdcard/tml-window.xml > artifacts/sdl-smoke/window.xml
  if [ "$attempt" == 1 ]; then adb exec-out screencap -p > artifacts/sdl-smoke/launcher-top.png; fi
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
  scroll_launcher
  sleep 2
done
if [ "$found_button" != 1 ]; then
  echo 'Graphics probe button not found in launcher'
  exit 1
fi
for attempt in $(seq 1 45); do
  adb logcat -d > artifacts/sdl-smoke/logcat.txt
  if grep -q 'SDL_FNA_FIRST_FRAME' artifacts/sdl-smoke/logcat.txt; then
    require_marker 'HOST_ENTRY_ASSEMBLY_READY Tml.Game.Android'
    require_marker 'ANDROID_DEFERRED_BOOTSTRAP_OK'
    require_marker 'ANDROID_PC_RESOLUTION_STABLE'
    require_marker 'ANDROID_GPU_STRESS_OK'
    if [ "$backend" == OpenGL ]; then
      require_marker 'ANDROID_GLES_STREAMING subdata+attribute-offsets+float-depth+ring-reuse'
    else
      require_marker 'TML_SDL_GPU_ACTIVE_DRIVER=vulkan'
    fi
    require_marker 'ANDROID_DISPLAY_STABLE'
    require_marker 'ANDROID_DMD_BACKEND DynamicMethod'
    require_marker 'ANDROID_PRIVATE_ACCESS_OK'
    require_marker 'ANDROID_DETOUR_PROBE_OK'
    require_marker 'NATIVE_RELATIVE_PATH_MISMATCH_REPRODUCED'
    require_marker 'NATIVE_FILE_IO_OK'
    adb exec-out screencap -p > artifacts/sdl-smoke/input-surface.png
    read -r screen_w screen_h < <(python3 - <<'PYSCREEN'
from PIL import Image
with Image.open('artifacts/sdl-smoke/input-surface.png') as image:
    print(image.width, image.height)
PYSCREEN
)
    cp artifacts/sdl-smoke/logcat.txt artifacts/sdl-smoke/startup-logcat.txt
    adb logcat -c
    adb shell input tap "$((screen_w / 2))" "$((screen_h / 2))"
    native_touch=0
    for touch_attempt in $(seq 1 15); do
      adb logcat -d > artifacts/sdl-smoke/logcat.txt
      if grep -q 'ANDROID_NATIVE_TOUCH_EVENT sdl-to-game-thread' artifacts/sdl-smoke/logcat.txt; then native_touch=1; break; fi
      sleep 1
    done
    if [ "$native_touch" != 1 ]; then
      echo "SDL_SMOKE_NATIVE_TOUCH_MISSING backend=$backend"
      grep -E 'SDL|TOUCH|INPUT|FATAL EXCEPTION' artifacts/sdl-smoke/logcat.txt | tail -n 80 || true
      exit 1
    fi
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
echo "SDL_SMOKE_FRAME_TIMEOUT backend=$backend"
tail -n 100 artifacts/sdl-smoke/logcat.txt
exit 1
