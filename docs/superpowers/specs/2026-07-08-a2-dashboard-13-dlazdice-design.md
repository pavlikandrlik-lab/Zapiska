# A2 — Dashboard na 13": dlaždice jednání se nevejde, nepoměr fontů

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08)

## Problém a kontext okolí (změřeno na 1280×832)

- Dlaždice jednání (`.dashboard-meeting-item`, [_DashboardMeetingsList](../../../PmTracker.Web/Views/Dashboard/_DashboardMeetingsList.cshtml)) má 3 řádky: `PMT + badge` / `Jednání č. N` / `datum · čas · projekt`. Naměřená výška **~171 px/kus**.
- Panel jednání na Přehledu má `min-height: var(--d-panel-min)` = `clamp(140px, 18vh, 240px)` → na 832 px výšky jen **150 px** → v natěsnaném layoutu (focus panel plný) se 3. řádek dlaždice ořízne a panel scrolluje (user report: „třetí řádek není vidět").
- Fonty: titulek `--d-fs-title` = **18,2 px**, meta `--d-fs-base` = **15,7 px**, badge 12 px — na dlaždici neúměrné. Stejný nepoměr ve focus dlaždicích: `.dashboard-focus-title` (`--d-fs-title`) vs vlastník v `.dashboard-focus-meta` (`--d-fs-label` 0,88 rem).
- Tokeny definované na `.dashboard-shell` (site.css ~7418–7428); dlaždicové CSS ~734–843.

## Řešení

Nezmenšovat globální tokeny (ovlivnily by celý dashboard) — **zmenšit typografii a spacing per dlaždice**:

1. `.dashboard-meeting-title`, `.dashboard-news-title`, `.dashboard-focus-title` → `font-size: var(--d-fs-base)` (≈15,7 px na 13") místo `--d-fs-title`; `margin: 0.15rem 0` místo `0.25rem 0`.
2. `.dashboard-item-meta` (+ focus meta už je) → `font-size: var(--d-fs-label)` (≈12,8 px na 13"); `margin-top: 0.2rem` místo `0.45rem`.
3. `.dashboard-meeting-item`/`.dashboard-news-item`/`.dashboard-focus-item` padding → menší vertikální složka (např. `calc(var(--d-pad-card) * 0.7) var(--d-pad-card)`).
4. Přesné hodnoty doladit **měřením** (Playwright, viewporty 1280×832 + 1440×900 + 1920×1080) proti akceptačním kritériím níže — ne od oka.

## Dotčené soubory
- `PmTracker.Web/wwwroot/css/site.css` — dlaždicová typografie/spacing (~734–843), případně jemné doladění `--d-panel-min`.

## Akceptační kritéria
- 1280×832: výška dlaždice jednání **≤ 120 px** → celá první dlaždice (vč. 3. řádku) viditelná v panelu s `--d-panel-min` bez scrollu.
- Poměr titulek/meta font ≤ 1,25 (dnes 18,2/15,7 u jednání, 18,2/12,8 u focus vlastníka).
- 1920×1080: vizuálně zkontrolovat, že dlaždice nejsou přehnaně malé (screenshot k ručnímu odsouhlasení).
- Žádná změna informačního obsahu dlaždic.

## Testy
- Unit (source-assertion): pin nových font-size vazeb (title→`--d-fs-base`, meta→`--d-fs-label`).
- Playwright měření výšky dlaždice na 1280×832 (durable E2E do `PmTracker.Tests.E2E` jen pokud stabilní — jinak ověřovací skript + screenshoty pro ruční kontrolu).

## Mimo scope
Přestavba layoutu panelů Přehledu; změny obsahu dlaždic; ReassignModal (čeká dle dashboard reworku).
