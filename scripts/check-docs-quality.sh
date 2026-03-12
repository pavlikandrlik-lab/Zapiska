#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

cd "$repo_root"

errors=0

print_error() {
    echo "[docs-quality] ERROR: $1" >&2
    errors=$((errors + 1))
}

require_file() {
    local file="$1"
    if [[ ! -f "$file" ]]; then
        print_error "Missing file: $file"
    fi
}

# 1) Required technical files
technical_files=(
    "docs/technical/00-documentation-tree.md"
    "docs/technical/01-system-context.md"
    "docs/technical/02-architecture.md"
    "docs/technical/03-runtime-configuration.md"
    "docs/technical/04-installation-deployment-iis.md"
    "docs/technical/05-web-server-iis-config.md"
    "docs/technical/06-database-bootstrap-migrations.md"
    "docs/technical/07-security-authz.md"
    "docs/technical/08-operations-runbooks.md"
    "docs/technical/09-testing-quality.md"
    "docs/technical/10-troubleshooting-recovery.md"
)

for file in "${technical_files[@]}"; do
    require_file "$file"
done

# 2) Required SOP/ISO sections in each technical file
required_sections=(
    "## 1. Účel"
    "## 2. Publikum a role"
    "## 3. Závislosti a předpoklady"
    "## 4. Vstupy a výstupy"
    "## 5. Detailní postup"
    "## 6. Verifikace"
    "## 7. Rollback"
    "## 8. Troubleshooting"
    "## 9. Audit a traceability"
)

for file in "${technical_files[@]}"; do
    [[ -f "$file" ]] || continue
    for section in "${required_sections[@]}"; do
        if ! rg -q --fixed-strings "$section" "$file"; then
            print_error "$file does not contain required section: $section"
        fi
    done

done

# 3) Naming/language consistency baseline
named_files=("docs/README.md" "docs/user-guide.md" "docs/qa.md" "install.md")
for file in "${named_files[@]}"; do
    require_file "$file"
    if [[ -f "$file" ]] && ! head -n 1 "$file" | rg -q "^# PM Tracker"; then
        print_error "$file must start with heading '# PM Tracker ...'"
    fi

done

# 4) Dead-link check for local markdown links in docs + install.md
check_link_target() {
    local source_file="$1"
    local target="$2"

    # skip external URLs, mailto and pure anchors
    if [[ "$target" =~ ^https?:// ]] || [[ "$target" =~ ^mailto: ]] || [[ "$target" =~ ^# ]]; then
        return 0
    fi

    # ignore in-app route links
    if [[ "$target" =~ ^/Dokumentace/ ]]; then
        return 0
    fi

    local path_only="$target"
    if [[ "$path_only" == *"#"* ]]; then
        path_only="${path_only%%#*}"
    fi

    # empty path after removing anchor
    if [[ -z "$path_only" ]]; then
        return 0
    fi

    local resolved
    if [[ "$path_only" = /* ]]; then
        resolved="$path_only"
    else
        local base_dir
        base_dir="$(cd "$(dirname "$source_file")" && pwd)"
        resolved="$base_dir/$path_only"
    fi

    if [[ ! -e "$resolved" ]]; then
        print_error "Dead link in $source_file -> $target (resolved: $resolved)"
    fi
}

while IFS= read -r line; do
    file_path="${line%%:*}"
    remainder="${line#*:}"
    remainder="${remainder#*:}"

    target="${remainder#*\(}"
    target="${target%\)}"

    check_link_target "$file_path" "$target"
done < <(rg -n -o "\[[^]]+\]\([^)]+\)" docs install.md)

if (( errors > 0 )); then
    echo "[docs-quality] FAILED with $errors issue(s)." >&2
    exit 1
fi

echo "[docs-quality] OK"
