#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
TOOLS_DIR="$ROOT_DIR/.tools"
COVERAGE_DIR="$ROOT_DIR/artifacts/coverage"
SUMMARY_FILE="$COVERAGE_DIR/summary.txt"
TEST_RESULTS_DIR="$ROOT_DIR/artifacts/test-results"

"$ROOT_DIR/scripts/test-prereq-check.sh"

mkdir -p "$TOOLS_DIR" "$COVERAGE_DIR" "$TEST_RESULTS_DIR"
rm -rf "$COVERAGE_DIR/report" "$SUMMARY_FILE"
find "$TEST_RESULTS_DIR" -name '*.trx' -delete >/dev/null 2>&1 || true
find "$TEST_RESULTS_DIR" -name 'coverage.cobertura.xml' -delete >/dev/null 2>&1 || true

echo "[INFO] Restore + build"
dotnet restore "$ROOT_DIR/PmTracker.sln"
dotnet build "$ROOT_DIR/PmTracker.sln" -c Release --no-restore

echo "[INFO] Running Unit"
"$ROOT_DIR/scripts/test-unit.sh"

echo "[INFO] Running Integration"
"$ROOT_DIR/scripts/test-integration.sh"

echo "[INFO] Running API"
"$ROOT_DIR/scripts/test-api.sh"

echo "[INFO] Running E2E"
"$ROOT_DIR/scripts/test-e2e.sh"

if [[ ! -x "$TOOLS_DIR/reportgenerator" ]]; then
  dotnet tool install --tool-path "$TOOLS_DIR" dotnet-reportgenerator-globaltool >/dev/null
fi

coverage_reports="$(find "$ROOT_DIR/artifacts/test-results" -name 'coverage.cobertura.xml' -print | tr '\n' ';')"
if [[ -z "$coverage_reports" ]]; then
  echo "[ERROR] No coverage.cobertura.xml files found."
  exit 1
fi

"$TOOLS_DIR/reportgenerator" \
  "-reports:${coverage_reports%;}" \
  "-targetdir:$COVERAGE_DIR/report" \
  "-reporttypes:HtmlInline;TextSummary" >/dev/null

cp "$COVERAGE_DIR/report/Summary.txt" "$SUMMARY_FILE"

echo "[INFO] Coverage summary"
cat "$SUMMARY_FILE"

line_cov="$(awk -F': ' '/Line coverage/ {print $2}' "$SUMMARY_FILE" | head -n1 | awk '{print $1}' | tr -d '%')"
branch_cov="$(awk -F': ' '/Branch coverage/ {print $2}' "$SUMMARY_FILE" | head -n1 | awk '{print $1}' | tr -d '%')"

line_cov="${line_cov:-0}"
branch_cov="${branch_cov:-0}"

coverage_failed=0
if ! awk -v line="$line_cov" -v branch="$branch_cov" 'BEGIN { if (line < 75 || branch < 65) exit 1; }'; then
  coverage_failed=1
fi

echo "[INFO] Running mutation report (non-blocking)"
"$ROOT_DIR/scripts/test-mutation.sh" || true

if [[ "$coverage_failed" -eq 1 ]]; then
  echo "[ERROR] Coverage gate failed: line=${line_cov}% (min 75), branch=${branch_cov}% (min 65)."
  exit 1
fi

echo "[OK] Full local test pipeline passed."
