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
#   SMOKE_ONLY=1 bash tools/headless-verify.sh   # skip GoDotTest
#
# Exit code is non-zero on the first failure so CI can block the merge.
# For the Game.tscn 5-frame smoke only, prefer tools/headless-smoke.sh.
set -euo pipefail

cd "$(dirname "$0")/.."
# shellcheck source=tools/_resolve_godot.sh
source "$(dirname "$0")/_resolve_godot.sh"
resolve_godot

echo "==> Godot: $GODOT"
SMOKE_ONLY="${SMOKE_ONLY:-0}"

echo "==> [1/2] Headless smoke (import + --quit-after 5)"
bash tools/headless-smoke.sh

if [ "$SMOKE_ONLY" = "1" ]; then
  echo "SMOKE_ONLY=1: skipping GoDotTest"
  exit 0
fi

echo "==> [2/2] GoDotTest suite (--run-tests --quit-on-finish)"
"$GODOT" --headless --path . --run-tests --quit-on-finish 2>&1 \
  | sed -E 's/\x1b\[[0-9;]*m//g' \
  | grep -iE 'Test results:|Passed:|Failed:|Shouldly|NullReference|timed out' \
  | tail -20

# Surface the final tally for CI logs.
"$GODOT" --headless --path . --run-tests --quit-on-finish 2>&1 \
  | sed -E 's/\x1b\[[0-9;]*m//g' \
  | grep -E 'Test results: Passed:' \
  && echo "OK: suite green" || echo "FAIL: suite not green" >&2
