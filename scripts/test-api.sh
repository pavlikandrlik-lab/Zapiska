#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RESULTS_DIR="$ROOT_DIR/artifacts/test-results/api"

mkdir -p "$RESULTS_DIR"

dotnet test "$ROOT_DIR/PmTracker.Tests.Api/PmTracker.Tests.Api.csproj" \
  -c Release \
  --logger "trx;LogFileName=api.trx" \
  --results-directory "$RESULTS_DIR" \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "[OK] API tests finished."
