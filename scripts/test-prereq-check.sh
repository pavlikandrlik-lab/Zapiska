#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

required_cmds=(dotnet docker)
for cmd in "${required_cmds[@]}"; do
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "[ERROR] Missing required command: $cmd"
    exit 1
  fi
done

if ! docker info >/dev/null 2>&1; then
  echo "[ERROR] Docker daemon is not available. Start Docker/Colima first."
  exit 1
fi

mkdir -p "$ROOT_DIR/artifacts/test-results" "$ROOT_DIR/artifacts/coverage" "$ROOT_DIR/artifacts/mutation"

echo "[OK] Prerequisites are ready."
