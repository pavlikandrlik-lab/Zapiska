#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
RESULTS_DIR="$ROOT_DIR/artifacts/test-results/unit"

mkdir -p "$RESULTS_DIR"

dotnet test "$ROOT_DIR/PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj" \
  -c Release \
  --logger "trx;LogFileName=unit.trx" \
  --results-directory "$RESULTS_DIR" \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "[OK] Unit tests finished."
