#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

project_file="$repo_root/PmTracker.Web/PmTracker.Web.csproj"
release_dir="$repo_root/docs/changelog/releases"
output_file="$repo_root/CHANGELOG.md"

normalize_version_part() {
    local part="${1:-0}"
    case "$part" in
        ''|*[!0-9]*)
            printf '00000'
            ;;
        *)
            printf '%05d' "$part"
            ;;
    esac
}

if [[ ! -f "$project_file" ]]; then
    echo "Project file was not found: $project_file" >&2
    exit 1
fi

app_version="$(sed -n 's:.*<AppVersion>\(.*\)</AppVersion>.*:\1:p' "$project_file" | head -n 1 | tr -d '[:space:]')"

if [[ -z "$app_version" ]]; then
    echo "AppVersion was not found in $project_file" >&2
    exit 1
fi

mkdir -p "$release_dir"

current_release_file="$release_dir/$app_version.md"
if [[ ! -f "$current_release_file" ]]; then
    current_date="$(date '+%Y-%m-%d')"
    cat > "$current_release_file" <<EOF
## $app_version - $current_date

### Změněno
- Doplnit změny pro verzi $app_version.
EOF
fi

release_files="$(
    find "$release_dir" -maxdepth 1 -type f -name '*.md' -print | while IFS= read -r file; do
        version="$(basename "$file" .md)"
        part1=0
        part2=0
        part3=0
        part4=0
        IFS='.' read -r part1 part2 part3 part4 <<EOF
$version
EOF
        printf '%s.%s.%s.%s\t%s\n' \
            "$(normalize_version_part "$part1")" \
            "$(normalize_version_part "$part2")" \
            "$(normalize_version_part "$part3")" \
            "$(normalize_version_part "$part4")" \
            "$file"
    done | sort -r | cut -f2-
)"

{
    printf '# Changelog\n\n'
    printf 'Tento soubor je generován skriptem `scripts/generate-changelog.sh` z verzovaných podkladů v `docs/changelog/releases/`.\n\n'

    while IFS= read -r file; do
        if [[ -z "$file" ]]; then
            continue
        fi

        cat "$file"
        printf '\n\n'
    done <<EOF
$release_files
EOF
} > "$output_file"

printf 'Generated %s for version %s\n' "$output_file" "$app_version"
