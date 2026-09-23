# B7 — /Jednani: akční tlačítka sidebar drží u obsahu (neujíždí dolů)

**Datum:** 2026-07-09 · **Stav:** schváleno uživatelem (analýza 2026-07-09)

## Problém a kontext okolí (root cause)
`.meeting-project-overview__actions { margin-top: auto }` (site.css:5209) — sidebar karty projektu je flex column natažený gridem na výšku karty; při rozbalení více let/jednání pravý sloupec kartu natáhne a auto-margin odsune „Detail projektu / Nové jednání" na úplné dno (mimo dohled).

## Řešení
`margin-top: auto` → `margin-top: var(--pm-spacing-s)` — tlačítka zůstanou hned pod statistikami nezávisle na výšce karty. Gradient pozadí sidebaru dál vyplňuje celou výšku (beze změny). Mobilní breakpoint (~5292) beze změny.

## Dotčené soubory
- `PmTracker.Web/wwwroot/css/site.css` (jeden řádek + komentář proč)

## Akceptační kritéria (Playwright)
- /Jednani, rozbalit aktuální rok + historii (více let): `actions.top − stats.bottom ≤ ~24 px` (tlačítka pod statistikami), ne u dna karty.
- Sbalený stav vypadá jako dřív (tlačítka pod statistikami — u krátké karty se pozice fakticky nemění).

## Testy
- Unit source-assertion: `.meeting-project-overview__actions` blok NEobsahuje `margin-top: auto`.
- Playwright měření rozbaleného stavu + screenshot.

## Mimo scope
Sticky chování sidebaru při scrollu (nebylo žádáno).
