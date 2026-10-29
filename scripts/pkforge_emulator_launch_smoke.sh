#!/usr/bin/env bash
# Ephemeral diagnostic only: no APK upload, private saves, ROMs or write grants.
set -euo pipefail
root="${1:?Pinned PKForge checkout required}"
workspace="${GITHUB_WORKSPACE:?}"
temp="${RUNNER_TEMP:?}"
build="$root/src/PKForge.App/bin/Debug/net10.0-android"
provenance="$temp/pkforge-diagnostic-provenance.json"
snapshot="$temp/pkforge-source-freeze.json"
selection="$temp/pkforge-selected-apk.json"
package="org.pkforge.app.debug"
test -f "$provenance" && test -f "$snapshot" && test -f "$selection"
apksigner="$(find "${ANDROID_HOME:?}/build-tools" -mindepth 2 -maxdepth 2 -name apksigner -type f | sort -V | tail -n 1)"
test -n "$apksigner" && test -x "$apksigner"
python3 "$workspace/scripts/pkforge_emulator_apk_selection.py" \
  --provenance "$provenance" --build-dir "$build" \
  --source-snapshot "$snapshot" --expected-mobile-sha "${GITHUB_SHA:?}" \
  --verify-selection "$selection"
signature="$(python3 "$workspace/scripts/pkforge_apk_signature_gate.py" \
  --provenance "$provenance" --selection "$selection" \
  --build-dir "$build" --source-snapshot "$snapshot" \
  --expected-mobile-sha "$GITHUB_SHA" --apksigner "$apksigner")"
python3 - "$selection" "$signature" "$package" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    selected = json.load(f)
signed, package = json.loads(sys.argv[2]), sys.argv[3]
assert selected["sha256"] == signed["apk_sha256"]
assert selected["source_sha"] == signed["source_sha"]
assert selected["application_id"] == signed["application_id"] == package
assert signed["signer_certificate_sha256"] and signed["verified_signature_schemes"]
assert signed["save_writes_authorized"] is False
assert signed["distribution_authorized"] is False
print("Exact preinstall signer and provenance evidence match.")
PY
apk="$(python3 - "$selection" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    print(json.load(f)["path"])
PY
)"
expected="$(python3 - "$selection" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as f:
    print(json.load(f)["sha256"])
PY
)"
test -f "$apk" && test ! -L "$apk"
test "$(sha256sum "$apk" | cut -d' ' -f1)" = "$expected"
adb wait-for-device
adb shell getprop sys.boot_completed | tr -d '\r' | grep -qx 1
adb install -r "$apk"
test "$(sha256sum "$apk" | cut -d' ' -f1)" = "$expected"
adb shell pm path "$package" | grep -q '^package:'
adb logcat -c
adb shell am force-stop "$package"
adb shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 > "$temp/pkforge-emulator-launch.txt"
foreground() {
  adb shell dumpsys activity activities |
    python3 "$workspace/scripts/pkforge_emulator_foreground.py" --package "$package"
}
launched=false
for _ in $(seq 1 30); do
  if adb shell pidof "$package" >/dev/null 2>&1 && foreground; then
    launched=true
    break
  fi
  sleep 2
done
if [ "$launched" != true ]; then
  adb logcat -d -s AndroidRuntime:E | tail -n 80 >&2
  echo "Diagnostic app never reached resumed foreground Activity" >&2
  exit 1
fi
adb shell input keyevent KEYCODE_HOME
sleep 2
if ! adb shell pidof "$package" >/dev/null 2>&1; then
  echo "Cannot verify background-PID negative gate: process exited" >&2
  exit 1
fi
if foreground; then
  echo "Foreground gate accepted a live background process" >&2
  exit 1
fi
adb shell monkey -p "$package" -c android.intent.category.LAUNCHER 1 > "$temp/pkforge-emulator-relaunch.txt"
recovered=false
for _ in $(seq 1 15); do
  if foreground; then recovered=true; break; fi
  sleep 2
done
test "$recovered" = true
python3 "$workspace/scripts/pkforge_emulator_picker_cancel.py" --package "$package"
test "$(sha256sum "$apk" | cut -d' ' -f1)" = "$expected"
echo "Installed diagnostic APK SHA-256: $expected"
echo "Package: $package; foreground, background-negative and relaunch passed."
echo "No provider, QR, backup transfer, real-device or save-write certification."
