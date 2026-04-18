# PM Tracker — file size policy warning (Windows / CI)
param(
    [int]$LimitCs = 500,
    [int]$LimitJs = 300
)

$root = Resolve-Path "$PSScriptRoot/.."
$script:exceeded = 0

Write-Host "== PM Tracker file-size policy =="
Write-Host "   C# limit: $LimitCs řádků, JS limit: $LimitJs řádků"
Write-Host ""

Get-ChildItem -Path "$root/PmTracker.Web","$root/PmTracker.Data" -Recurse -Include *.cs `
    | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } `
    | ForEach-Object {
        $lines = (Get-Content $_.FullName).Count
        if ($lines -gt $LimitCs) {
            Write-Warning "$($_.FullName): $lines řádků (limit $LimitCs) — zvažte rozdělení"
            $script:exceeded++
        }
    }

Get-ChildItem -Path "$root/PmTracker.Web/wwwroot/js/modules" -Recurse -Include *.js `
    | ForEach-Object {
        $lines = (Get-Content $_.FullName).Count
        if ($lines -gt $LimitJs) {
            Write-Warning "$($_.FullName): $lines řádků (limit $LimitJs) — zvažte rozdělení"
            $script:exceeded++
        }
    }

if ($script:exceeded -gt 0) {
    Write-Host "`nNalezeno $script:exceeded souborů nad limit."
} else {
    Write-Host "`nVšechny soubory v limitu."
}
exit 0
