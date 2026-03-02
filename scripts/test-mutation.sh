#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
TOOLS_DIR="$ROOT_DIR/.tools"
OUTPUT_DIR="$ROOT_DIR/artifacts/mutation"

mkdir -p "$TOOLS_DIR" "$OUTPUT_DIR"

if [[ ! -x "$TOOLS_DIR/dotnet-stryker" ]]; then
  dotnet tool install --tool-path "$TOOLS_DIR" dotnet-stryker >/dev/null
fi

DEFAULT_MUTATE_TARGETS=(
  "!**/*"
  "Controllers/ProjektyController.cs"
  "Controllers/ZaznamyController.cs"
  "Controllers/NastaveniController.cs"
  "Services/Security/UserContextResolver.cs"
  "Models/ViewModels/SecurityViewModels.cs"
)

MUTATE_ARGS=()
if [[ -n "${MUTATION_MUTATE:-}" ]]; then
  MUTATE_ARGS+=(--mutate "$MUTATION_MUTATE")
else
  for target in "${DEFAULT_MUTATE_TARGETS[@]}"; do
    MUTATE_ARGS+=(--mutate "$target")
  done
fi

set +e
"$TOOLS_DIR/dotnet-stryker" \
  --solution "$ROOT_DIR/PmTracker.sln" \
  --project "$ROOT_DIR/PmTracker.Web/PmTracker.Web.csproj" \
  --test-project "$ROOT_DIR/PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj" \
  "${MUTATE_ARGS[@]}" \
  --output "$OUTPUT_DIR" \
  --reporter "Html" \
  --reporter "Json" \
  --threshold-high 100 \
  --threshold-low 0 \
  --break-at 0 \
  --skip-version-check
exit_code=$?
set -e

if [[ $exit_code -ne 0 ]]; then
  echo "[WARN] Mutation run finished with non-zero exit code ($exit_code). Report-only mode keeps pipeline green."
fi

echo "[OK] Mutation report generated (report-only)."
exit 0
