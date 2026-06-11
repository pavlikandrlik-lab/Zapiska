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
| Kategorie | Token | Význam |
|---|---|---|
| Úkol (`U`/`UKOL`) | `--gov-color-warning` (jantarová) | akce / pozornost |
| Informace (`INFO`) | `--gov-color-primary` (modrá) | informační, klid |
| Rozhodnutí (`ROZH`/`ROZHODNUTI`) | `--gov-color-success` (zelená) | rozhodnuto / závěr |
| ostatní / custom (default) | `--gov-color-muted` (neutrální šedá) | bez sémantiky |

Výhody: konzistence s gov-message/gov-tag, automatický light/dark (řeší i
dark-mode čitelnost), neutrální fallback pro per-projekt custom kategorie.

## Dotčené soubory
- `PmTracker.Web/wwwroot/css/site.css`:
  - `.record-bar` (ř. ~3401) — přidat `border-radius` (left) + default background
    `--gov-color-accent` → `--gov-color-muted`.
  - `.category-* .record-bar` (ř. ~6679–6691) — hardcoded hex → gov tokeny.
- Žádná změna markupu (`_ZaznamPartial` generuje `category-{kód}` dál).

## Mimo rozsah
- Tónování pillu kategorie (zůstává neutrální pill) — jen proužek.
- Změna kódů kategorií / číselníku.

## Verifikace
- Playwright: proužek má zaoblené levé rohy (matchuje kartu); barvy se mění
  light↔dark (computed background flipuje).
