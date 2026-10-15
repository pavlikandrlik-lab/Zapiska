# Design tokeny

**Barvy** jsou sémantické tokeny gov DS 4.7.0 (DS FIS v1.0.0), které gov sám přepíná podle
`data-theme` (světlý / tmavý motiv). Zdroj: `PmTracker.Web/wwwroot/assets/gov/styles/tokens.css`
(1:1 kopie kitu, needituje se). Aplikace nemá vlastní paletu ani tmavá pravidla (od 2026-10-15).

**Ostatní tokeny** (spacing, radius, typografie, z-index) jsou aliasy `--pm-*` nad gov tokeny
v `PmTracker.Web/wwwroot/css/tokens.css`.

## Pravidla

1. Barva = sémantický gov token (`--background-*`, `--text-*`, `--border-*`, `--icon-*`, `--status-*`). Žádný hex, `rgb()`, `color-mix()` ani záložní hodnota `var(--x, #hex)`.
2. Co gov nemá, je aplikační token `--app-*` definovaný jen v `css/tokens.css` (jediný soubor s `data-theme` a barevnými literály).
3. Žádné pravidlo `:root[data-theme="dark"] .x` — tmavý motiv přepínají tokeny.
4. Hlavička (`.gov-header`, logo, název, účet, pole hledání) se nestyluje — je zmražená.
5. Breakpointy v media queries jsou číselně shodné s gov DS; z-index vždy z `--pm-z-*` stacku.

## Kdy který token

| Co | Token |
|---|---|
| Pozadí stránky | `--background-page` |
| Karta, panel, input, dropdown | `--background-block-primary` |
| Zvýrazněná / vedlejší plocha, hover na ploše bloku | `--background-block-secondary` |
| Jemná šedá plocha (řádek, placeholder) | `--background-neutral-subtlest` / `--background-neutral-subtle` |
| Světle modrá plocha (odznak, vybraný řádek) | `--background-primary-subtlest` / `--background-primary-subtle` |
| Plná modrá (tlačítko, štítek TERMÍN) | `--background-primary` + text `--text-white-fixed` |
| Hlavní / vedlejší / tlumený text | `--text-primary` / `--text-secondary` / `--text-tertiary` |
| Odkaz, modrý text | `--text-primary-color` |
| Rámeček / jemný oddělovač / výrazný rámeček | `--border-subtle` / `--border-subtlest` / `--border-primary` |
| Chyba, úspěch, upozornění | `--text-status-*`, `--background-status-*(-subtlest)`, `--border-error/success/warning` |
| Fokus | `--status-focus` |
| Ikona | `--icon-default` / `--icon-neutral` |

### Aplikační tokeny (`css/tokens.css`)

| Token | Význam |
|---|---|
| `--app-shadow-soft`, `--app-shadow-raised` | Stíny (gov nemá tokeny stínů); v tmavém motivu silnější |
| `--app-category-decision(-subtle)` | Fialová kategorie **Rozhodnutí** (gov barvy jsou obsazené semaforem úkolů). Viz [Barvy karet záznamů](../wiki/projekty/zaznamy/barvy-karet.md). |
| `--app-schedule-step-1…11`, `--app-schedule-delay` | Duhová paleta kroků a skluz |
| `--app-schedule-stripe`, `-segment-inner`, `-segment-edge`, `-segment-outline` | Překryvy pruhů harmonogramu; pruhy mají barvy z Excelu (`HarmonogramKroky.cs`), stejné v obou motivech |

## Kontroly

- `AppCssColorTokenTests` (Unit): žádná stará proměnná, žádná nedefinovaná proměnná v barvě, žádný barevný literál mimo `tokens.css`, žádný `data-theme` mimo `tokens.css`, rohy záložek a hover vidět v obou motivech.
- `ThemePaletteAuditTests` (E2E): na 10 stránkách v obou motivech žádná barva mimo paletu gov + `--app-*` a žádný text pod WCAG kontrastem.

## Ostatní tokeny

### Spacing (4pt grid)

`--pm-spacing-3xs` (2px) až `--pm-spacing-3xl` (64px).

### Breakpointy

```css
@media (min-width: 576px) { /* --pm-bp-sm */ }
@media (min-width: 768px) { /* --pm-bp-md — tablety */ }
@media (min-width: 992px) { /* --pm-bp-lg — malé desktopy */ }
@media (min-width: 1200px) { /* --pm-bp-xl — standardní 1080p+ */ }
@media (min-width: 1400px) { /* --pm-bp-2xl — wide-screen 1440p+ */ }
```

### Z-index stack

| Token | Hodnota | Použití |
|---|---|---|
| `--pm-z-base` | 1 | default |
| `--pm-z-elevated` | 10 | karty, hover lift |
| `--pm-z-sticky` | 50 | sticky headery tabulky |
| `--pm-z-header` | 100 | app header |
| `--pm-z-dropdown` | 200 | dropdowny |
| `--pm-z-overlay` | 500 | overlay mimo modal |
| `--pm-z-modal` | 1000 | modaly |
| `--pm-z-toast` | 2000 | toasty nad vším |
