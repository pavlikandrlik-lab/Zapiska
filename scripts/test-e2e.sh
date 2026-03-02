#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RESULTS_DIR="$ROOT_DIR/artifacts/test-results/e2e"

mkdir -p "$RESULTS_DIR"

# Ensure Playwright CLI scripts are generated.
dotnet build "$ROOT_DIR/PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj" -c Release >/dev/null

PLAYWRIGHT_SH="$ROOT_DIR/PmTracker.Tests.E2E/bin/Release/net8.0/playwright.sh"
PLAYWRIGHT_PS1="$ROOT_DIR/PmTracker.Tests.E2E/bin/Release/net8.0/playwright.ps1"
PLAYWRIGHT_CLI="$ROOT_DIR/PmTracker.Tests.E2E/bin/Release/net8.0/.playwright/package/cli.js"
PLAYWRIGHT_NODE="$(find "$ROOT_DIR/PmTracker.Tests.E2E/bin/Release/net8.0/.playwright/node" -type f -name node | head -n 1 || true)"

if [[ -x "$PLAYWRIGHT_SH" ]]; then
  "$PLAYWRIGHT_SH" install chromium >/dev/null
elif [[ -f "$PLAYWRIGHT_PS1" ]] && command -v pwsh >/dev/null 2>&1; then
  pwsh "$PLAYWRIGHT_PS1" install chromium >/dev/null
elif [[ -n "$PLAYWRIGHT_NODE" ]] && [[ -f "$PLAYWRIGHT_CLI" ]]; then
  "$PLAYWRIGHT_NODE" "$PLAYWRIGHT_CLI" install chromium >/dev/null
else
  echo "[WARN] Playwright installer was not found (checked playwright.sh, playwright.ps1+pwsh, node+cli.js)."
fi

dotnet test "$ROOT_DIR/PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj" \
  -c Release \
  --logger "trx;LogFileName=e2e.trx" \
  --results-directory "$RESULTS_DIR" \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "[OK] E2E tests finished."
