# A9 — Filtr: prohodit pozice „Pouze aktivní záznamy" ↔ „Jednání-vyjádření"

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08)

## Problém a kontext okolí

[_ProjectFilterShell.cshtml](../../../PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml) renderuje `.filter-grid` v pořadí: … Vlastník (select) → **Pouze aktivní záznamy** (`gov-form-switch[data-filter-key="aktivni"]`) → **Jednání-vyjádření** (select `data-filter-key="jednani-vyjadreni-stav"`). User chce prohodit — select před switch (switch jako poslední prvek gridu).

Souvislosti:
- Filtr JS (`filters/projectFilter.js`, `recordDisplay.js`) váže přes `data-filter-key`, **ne přes pozici** → prohození je bezpečné.
- Zarovnávací pravidlo z 2026-07-08 (`.filter-grid gov-form-switch[size]:not(.filter-inline-wide) { transform: translateY(26px) }`) je vázané na třídu/typ elementu, ne na pozici → zůstává funkční.
- Shell je sdílený pro scope `records` i `schedule` (mount z _ProjectRecordsTab i _ProjectScheduleTab) — změna se projeví v obou, což je žádoucí (konzistence).

## Řešení
Prohodit dva sousední bloky markupu (label Jednání-vyjádření ↑, gov-form-switch aktivni ↓). Nic jiného.

## Akceptační kritéria
- Pořadí v UI: … Vlastník → Jednání-vyjádření → Pouze aktivní záznamy.
- Filtry fungují beze změny (persistence, chipy, uložení výchozích).
- Přepínač zůstává svisle zarovnaný se sousedním selectem.

## Testy
- Unit source-assertion: v _ProjectFilterShell.cshtml je `jednani-vyjadreni-stav` před `data-filter-key="aktivni"`.
