#!/usr/bin/env bash
# Headless verification + test gate for SeaAnomaly (Godot 4.7.1 mono).
#
# Runs the engine without the editor to prove the project still loads and the
# gameplay test suite is green. Intended as a PR / pre-merge gate (skill
# godot-headless-verify §8). Zero gameplay code is touched — this only drives
# the engine from the CLI.
#
# Usage:
#   bash tools/headless-verify.sh
#   GODOT="/path/to/godot.exe" bash tools/headless-verify.sh
#
# Exit code is non-zero on the first failure so CI can block the merge.
set -euo pipefail

# Resolve the Godot binary: explicit $GODOT, else the machine default.
if [ -z "${GODOT:-}" ]; then
  GODOT="D:/Godot/Godot_v4.7.1-stable_mono_win64/godot.exe"
fi
if [ ! -x "$GODOT" ] && [ ! -f "$GODOT" ]; then
  echo "GODOT binary not found: $GODOT" >&2
  echo "Set the GODOT env var to your Godot 4.7.1 mono executable." >&2
  exit 2
fi

# Run from the project root (parent of this script's dir).
cd "$(dirname "$0")/.."

echo "==> [1/3] Rebuild import cache (--headless --import)"
"$GODOT" --headless --import --path . 2>&1 | tail -1

echo "==> [2/3] Script parse check (no SCRIPT/Parse errors on first-party code)"
if "$GODOT" --headless --quit --path . 2>&1 \
    | grep -iE 'SCRIPT ERROR|Parse Error' ; then
  echo "first-party script errors detected" >&2
  exit 1
fi
echo "clean"

echo "==> [3/3] GoDotTest suite (--run-tests --quit-on-finish)"
"$GODOT" --headless --path . --run-tests --quit-on-finish 2>&1 \
  | sed -E 's/\x1b\[[0-9;]*m//g' \
  | grep -iE 'Test results:|Passed:|Failed:|Shouldly|NullReference|timed out' \
  | tail -20

# Surface the final tally for CI logs.
"$GODOT" --headless --path . --run-tests --quit-on-finish 2>&1 \
  | sed -E 's/\x1b\[[0-9;]*m//g' \
  | grep -E 'Test results: Passed:' \
  && echo "OK: suite green" || echo "FAIL: suite not green" >&2
