# Design: Zaoblení barevné čáry karty záznamu + sémantika barev kategorií

- **Datum:** 2026-06-11
- **Oblast:** Projekty → Záznamy (karta záznamu `_ZaznamPartial`)
- **Skill flow:** brainstorming → (tento spec) → implementace (2 CSS pravidla, triviální)

## Problém (2 související úkoly)

1. **Hranaté rohy čáry:** Karta `.record-card` má `border-radius`, ale svislý 6px
   barevný proužek `.record-bar` (samostatný flex prvek vlevo) má hranaté rohy,
   které přečnívají přes zaoblení karty. Karta má `overflow: visible` (záměrně),
   takže proužek se neořízne.
2. **Barvy bez systému:** Barvy kategorií jsou hardcoded hex (Úkol `#dc2626`,
   Informace `#2563eb`, Rozhodnutí `#7c3aed`) — arbitrární a nereagují na dark mode.

## Rozhodnutí (odsouhlaseno uživatelem)

### Úkol 1 — zaoblení čáry
Zaoblit levé rohy proužku místo měnit `overflow` (v kartě není floating prvek,
ale `overflow:visible` je explicitní → bezpečnější nesahat na něj):
```css
.record-bar { border-radius: var(--gov-radius) 0 0 var(--gov-radius); }
```

### Úkol 2 — sémantika barev (gov tokeny, theme-aware)

**Produkční kódy kategorií:** `U`=Úkol, `I`=Informace, `D`=Rozhodnutí (dev seed má
navíc `UKOL`/`INFO`/`ROZHODNUTI`). Barva se proto určuje sémanticky podle kódu
**i názvu** (fallback) v `_ZaznamPartial` → třídy `record-cat--*`.

| Kategorie | Kódy | Token | Význam |
|---|---|---|---|
| Úkol | `U`, `UKOL` | `--gov-color-warning` (jantarová) | akce / pozornost |
| Informace | `I`, `INFO`, `INF` | `--gov-color-primary` (modrá) | informační, klid |
| Rozhodnutí | `D`, `ROZHODNUTI`, `ROZH` | `--record-cat-decision` (fialová, mimo gov paletu, theme-aware) | rozhodnuto / závěr |
| ostatní / custom (default) | — | `--gov-color-muted` (neutrální šedá) | bez sémantiky |

### Úkol 3 — stavová eskalace úkolů (semafor) — 2026-06-11

U kategorie **Úkol** proužek navíc reaguje na stav a termín (semafor):

| Stav úkolu | Token | Podmínka |
|---|---|---|
| Běží, v termínu | `--gov-color-warning` (jantarová) | aktivní, termín ≥ dnes |
| **Po termínu** | `--gov-color-accent` (červená) | aktivní `JeUkol` + `AktualniTermin.Date < dnes` |
| Hotový | `--gov-color-success` (zelená) | finální stav (`IsAktivniStav=false`) |

- Termín = **termín úkolu** (`AktualniTermin` = `DatumUkonceni`), ne dílčí harmonogram.
- Hranice `< dnes` (v den termínu ještě jantarová). Dnešek z app `TimeProvider`.
- Červená jen pro **aktivní** úkoly; hotový úkol je vždy zelený (overdue se počítá
  jen pro aktivní). Info/Rozhodnutí se eskalace netýká.

Výhody: konzistence s gov-message/gov-tag, automatický light/dark (řeší i
dark-mode čitelnost), neutrální fallback pro per-projekt custom kategorie.
Uživatelská dokumentace: `docs/wiki/projekty/zaznamy/barvy-karet.md`.

## Dotčené soubory
- `PmTracker.Web/wwwroot/css/site.css`:
  - `.record-bar` — `border-radius` (left) + default background `--gov-color-muted`.
  - `.record-cat--{task|info|decision}` — barvy přes gov tokeny.
  - `.record-cat--task[data-filter-aktivni="false"]` → zelená (hotovo).
  - `.record-cat--task[data-record-overdue="true"]` → červená (po termínu).
- `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml`:
  - `@inject TimeProvider` + výpočet `catColor` (record-cat--*) a `isOverdue`.
  - `<article>` dostal `record-cat--*` třídu + `data-record-overdue`.

## Mimo rozsah
- Tónování pillu kategorie (zůstává neutrální pill) — jen proužek.
- Změna kódů kategorií / číselníku.

## Verifikace
- Playwright: proužek má zaoblené levé rohy (matchuje kartu); barvy se mění
  light↔dark (computed background flipuje).
