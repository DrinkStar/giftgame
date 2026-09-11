#!/usr/bin/env bash
# Headless Game.tscn smoke for SeaAnomaly (Godot 4.7.1 mono).
#
# Proves Main → Game.tscn loads for --quit-after N frames without missing
# imported assets. WaterMesh GPU init is expected to WARN on a DRM-less VM
# (contract: _gpuEnabled=false, GetWaveHeight → 0). Not a GoDotTest run.
#
# Usage:
#   bash tools/headless-smoke.sh
#   GODOT=/path/to/godot bash tools/headless-smoke.sh
#   FRAMES=5 USER_DATA_DIR=/tmp/seaanomaly-userdata bash tools/headless-smoke.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
# shellcheck source=tools/_resolve_godot.sh
source "$(dirname "$0")/_resolve_godot.sh"
resolve_godot

FRAMES="${FRAMES:-5}"
USER_DATA_DIR="${USER_DATA_DIR:-${TMPDIR:-/tmp}/seaanomaly-userdata}"
LOG_DIR="${LOG_DIR:-${TMPDIR:-/tmp}/seaanomaly-smoke}"
mkdir -p "$USER_DATA_DIR" "$LOG_DIR"

echo "==> Godot: $GODOT"
echo "==> [0/2] LFS assets are real files (not pointers)"
python3 - <<'PY'
from pathlib import Path

checks = [
    (Path("assets/icons/wood.png"), b"\x89PNG"),
    (Path("assets/audio/bgm/island.ogg"), b"OggS"),
]
for path, magic in checks:
    if not path.is_file():
        raise SystemExit(f"missing asset: {path}")
    data = path.read_bytes()[:64]
    if data.startswith(b"version https://git-lfs"):
        raise SystemExit(f"LFS pointer still checked out: {path}\nRun: git lfs pull")
    if not data.startswith(magic):
        raise SystemExit(f"unexpected header for {path} (want {magic!r}, got {data[:16]!r})")
print("ok")
PY

IMPORT_LOG="$LOG_DIR/import.log"
SMOKE_LOG="$LOG_DIR/smoke.log"

echo "==> [1/2] --headless --import"
"$GODOT" --headless --import --path . --user-data-dir "$USER_DATA_DIR" \
  >"$IMPORT_LOG" 2>&1 || {
  echo "import failed; see $IMPORT_LOG" >&2
  tail -40 "$IMPORT_LOG" >&2
  exit 1
}
if ! grep -q '\[ DONE \]' "$IMPORT_LOG"; then
  echo "import log missing DONE marker; see $IMPORT_LOG" >&2
  exit 1
fi
echo "ok"

echo "==> [2/2] --headless --quit-after ${FRAMES}"
set +e
"$GODOT" --headless --path . --quit-after "$FRAMES" --user-data-dir "$USER_DATA_DIR" \
  >"$SMOKE_LOG" 2>&1
SMOKE_EC=$?
set -e

echo "exit=$SMOKE_EC log=$SMOKE_LOG"
sed -E 's/\x1b\[[0-9;]*m//g' "$SMOKE_LOG" | grep -E \
  'DayNightService:|WaterMesh:|Failed loading resource|SCRIPT ERROR|LookGood|IndexOutOfRange|ERROR: 2 resources' \
  || true

fail() { echo "FAIL: $*" >&2; exit 1; }

[ "$SMOKE_EC" = "0" ] || fail "godot exit $SMOKE_EC"
grep -q 'DayNightService: Started' "$SMOKE_LOG" || fail "Game.tscn did not reach DayNightService"
if grep -q 'Failed loading resource:' "$SMOKE_LOG"; then
  echo "missing resources:" >&2
  grep 'Failed loading resource:' "$SMOKE_LOG" | sort -u | head -20 >&2
  fail "imported assets failed to load"
fi
if grep -qiE 'SCRIPT ERROR|IndexOutOfRange' "$SMOKE_LOG"; then
  fail "script / runtime error in smoke log"
fi
if grep -q '/sys/class/drm' "$SMOKE_LOG"; then
  fail "LookGood walked /sys/class/drm (headless guard missing)"
fi

echo "OK: headless smoke green (WaterMesh GPU WARN is expected without a GPU)"
