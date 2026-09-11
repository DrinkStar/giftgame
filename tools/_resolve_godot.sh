# Shared Godot 4.7.1 mono resolver. Source from other tools/*.sh.
# Prefer $GODOT, then the Cloud Agent Linux install, then PATH, then Windows default.
resolve_godot() {
  if [ -n "${GODOT:-}" ] && { [ -x "$GODOT" ] || [ -f "$GODOT" ]; }; then
    return 0
  fi

  local candidates=(
    "${HOME}/.local/opt/Godot_v4.7.1-stable_mono_linux_x86_64/Godot_v4.7.1-stable_mono_linux.x86_64"
    "/usr/local/bin/godot"
    "godot"
    "D:/Godot/Godot_v4.7.1-stable_mono_win64/godot.exe"
  )
  local c
  for c in "${candidates[@]}"; do
    if [ -x "$c" ] || [ -f "$c" ]; then
      GODOT="$c"
      return 0
    fi
    if command -v "$c" >/dev/null 2>&1; then
      GODOT="$(command -v "$c")"
      return 0
    fi
  done

  echo "GODOT binary not found. Set GODOT to your Godot 4.7.1 mono executable." >&2
  return 2
}
