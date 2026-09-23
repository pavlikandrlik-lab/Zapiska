# A3 — Jednotné mezery: karty ↔ karty a poslední karta ↔ footer

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08)

## Problém a kontext okolí (změřeno)

Reference = záložka Záznamy projektu: **12 px mezi kartami, 20 px poslední karta → footer**. Odchylky:

| Místo | Naměřeno | Příčina |
|---|---|---|
| Dashboard subpage „Nejbližší jednání" (`/Dashboard/Meetings`) | **0 px mezi kartami** (slepené, screenshot) | `.dashboard-meeting-list { gap: var(--d-gap-item) }` — token `--d-gap-item` je definovaný **jen na `.dashboard-shell`** (Přehled). Subpages (`.dashboard-list-page(-narrow)` wrapper, žádný shell) → `var()` bez fallbacku → gap 0. Totéž `padding: 0 var(--d-pad-card) var(--d-gap-item)` → 0. Stejná třída vzoru jako dřívější `--pm-*` lekce (nedefinovaný token). |
| Dashboard subpages Focus/News | stejný mechanismus (listy sdílí selektor site.css:7536–7547) | dtto |
| Dashboard Přehled | shell → footer **0 px** | `.app-main--fluid:has(.dashboard-shell)` height=100dvh−header; footer nalepený hned pod fold |
| Projektová záložka Jednání | grid karet bez spodního odsazení k footeru | `.meeting-year-stack` nemá bottom margin/padding; `.app-main--fluid` má `margin: 0` |

## Řešení — jeden zdroj pravdy

1. **`--d-*` tokeny přesunout z `.dashboard-shell` na `:root`** (identické hodnoty; `.dashboard-shell` blok tokeny smaže — clamp výrazy jsou viewport-based, kontext nehraje roli). Tím listy na subpages dostanou správný `gap` i `padding` automaticky a zmizí celá třída „token mimo scope" chyb.
2. **Nový token `--app-content-bottom-gap: 20px`** (v `:root`, hodnota = naměřená reference záznamů) aplikovaný na:
   - `.app-main--fluid:has(.dashboard-shell)` → `padding-bottom: var(--app-content-bottom-gap)` (výška zůstává `100dvh − header`, obsah se o gap zmenší — panely mají interní scroll, nic se nerozbije),
   - `.dashboard-list-page`, `.dashboard-list-page-narrow` → `padding-bottom`,
   - `.meeting-year-stack` (projektová záložka Jednání) → `margin-bottom`.
3. Grep-audit dalších `var(--d-` výskytů mimo `.dashboard-shell` potomky — po přesunu na `:root` bezpředmětné, ale ověřit, že žádný jiný selektor nespoléhá na *ne*definování tokenu.

## Dotčené soubory
- `PmTracker.Web/wwwroot/css/site.css` — přesun tokenů, nový token, 3 aplikace.

## Akceptační kritéria (Playwright měření)
- `/Dashboard/Meetings|Focus|News`: mezera mezi kartami > 0 (≈ `--d-gap-item`, na 1440 ~10 px) a poslední karta → footer ≥ 20 px (při obsahu delším než viewport).
- Přehled: spodní hrana shellu → footer = 20 px.
- Projektová záložka Jednání: poslední řada karet → footer ≥ 20 px.
- Reference (Záznamy projektu) beze změny: 12 px / 20 px.
- Přehled se stále vejde na 1 obrazovku (1280×832) — padding nesmí vyvolat scroll stránky.

## Testy
- Unit (source-assertion): tokeny `--d-gap-item`/`--d-pad-card`/… definované v `:root` bloku; `--app-content-bottom-gap` existuje a je aplikován na 3 místech.
- Playwright ověřovací měření (čísla výše).

## Mimo scope
Změny rozměrů dlaždic (řeší A2); ne-dashboard stránky, které problém nemají.
