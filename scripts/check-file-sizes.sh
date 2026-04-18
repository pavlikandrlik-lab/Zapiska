#!/usr/bin/env bash
# PM Tracker — file size policy warning
# Popisuje: docs/architecture/backend-layering.md, docs/architecture/js-modules.md
set -euo pipefail

LIMIT_CS=500
LIMIT_JS=300
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
THRESHOLD_EXCEEDED=0

echo "== PM Tracker file-size policy =="
echo "   C# limit: $LIMIT_CS řádků, JS limit: $LIMIT_JS řádků"
echo ""

while IFS= read -r -d '' file; do
    lines=$(wc -l < "$file")
    if [ "$lines" -gt "$LIMIT_CS" ]; then
        echo "::warning file=${file#$ROOT/},line=1::C# soubor má $lines řádků (limit $LIMIT_CS) — zvažte rozdělení"
        THRESHOLD_EXCEEDED=$((THRESHOLD_EXCEEDED + 1))
    fi
done < <(find "$ROOT/PmTracker.Web" "$ROOT/PmTracker.Data" -name "*.cs" -not -path "*/bin/*" -not -path "*/obj/*" -print0 2>/dev/null)

while IFS= read -r -d '' file; do
    lines=$(wc -l < "$file")
    if [ "$lines" -gt "$LIMIT_JS" ]; then
        echo "::warning file=${file#$ROOT/},line=1::JS modul má $lines řádků (limit $LIMIT_JS) — zvažte rozdělení"
        THRESHOLD_EXCEEDED=$((THRESHOLD_EXCEEDED + 1))
    fi
done < <(find "$ROOT/PmTracker.Web/wwwroot/js/modules" -name "*.js" -print0 2>/dev/null)

if [ "$THRESHOLD_EXCEEDED" -gt 0 ]; then
    echo ""
    echo "Nalezeno $THRESHOLD_EXCEEDED souborů nad limit. Script nevrací chybový kód (warning only)."
else
    echo "Všechny soubory v limitu."
fi
exit 0
