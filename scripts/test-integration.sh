#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RESULTS_DIR="$ROOT_DIR/artifacts/test-results/integration"

mkdir -p "$RESULTS_DIR"

dotnet test "$ROOT_DIR/PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj" \
  -c Release \
  --logger "trx;LogFileName=integration.trx" \
  --results-directory "$RESULTS_DIR" \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "[OK] Integration tests finished."
