#!/usr/bin/env bash
# Idempotent CodeGraph CLI + index bootstrap for SeaAnomaly (Godot 4.7.1 C#).
#
# Installs colbymchenry/codegraph if missing, wires the Cursor MCP server,
# and builds or refreshes the local .codegraph/ index. Safe for Cloud Agent
# `install` (must terminate; does not leave a file-watcher daemon running).
#
# Usage:
#   bash tools/ensure-codegraph.sh
set -euo pipefail

export PATH="${HOME}/.local/bin:${PATH}"
export CODEGRAPH_TELEMETRY="${CODEGRAPH_TELEMETRY:-0}"
export CODEGRAPH_NO_DAEMON="${CODEGRAPH_NO_DAEMON:-1}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [ ! -x "${HOME}/.local/bin/codegraph" ]; then
  echo "==> Installing CodeGraph CLI"
  curl -fsSL https://raw.githubusercontent.com/colbymchenry/codegraph/main/install.sh | sh
  export PATH="${HOME}/.local/bin:${PATH}"
fi

if [ -f "${HOME}/.bashrc" ] && ! grep -qF '${HOME}/.local/bin' "${HOME}/.bashrc" \
  && ! grep -qF '$HOME/.local/bin' "${HOME}/.bashrc" \
  && ! grep -qF "${HOME}/.local/bin" "${HOME}/.bashrc"; then
  echo 'export PATH="$HOME/.local/bin:$PATH"' >> "${HOME}/.bashrc"
fi

echo "==> Wiring Cursor MCP (codegraph serve --mcp)"
codegraph install --target=cursor --location=global --yes

if [ -d .codegraph ]; then
  echo "==> Syncing existing CodeGraph index"
  codegraph sync --quiet || codegraph index --quiet
else
  echo "==> Building CodeGraph index (codegraph init)"
  codegraph init --yes
fi

echo "==> CodeGraph status"
codegraph --version
codegraph status
