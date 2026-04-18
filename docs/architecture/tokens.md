# Design tokens

Aplikační CSS proměnné (`--pm-*`) jsou aliasy nad gov-design-system tokeny
(`--color-*`, `--spacing-*`, atd. — gov DS NEpoužívá prefix `--gov-`).
Zdroj: `PmTracker.Web/wwwroot/css/tokens.css`.

## Pravidla

1. Žádné hardcoded hodnoty v CSS — barva, spacing, radius, font-size → vždy token
2. Používej `--pm-*`, ne přímé `--color-*` v aplikačním CSS — `--pm-*` izoluje aplikaci od gov upgrade
3. Breakpointy v media queries jsou číselně shodné s gov DS
4. Z-index vždy z `--pm-z-*` stacku, nikdy ad-hoc číslo

## Kategorie tokenů

### Barvy

| Token | Význam |
|---|---|
| `--pm-color-primary` | Hlavní akční barva |
| `--pm-color-primary-strong` | Hover state |
| `--pm-color-primary-soft` | Pozadí primárních prvků |
| `--pm-color-success` | Úspěch / OK stav |
| `--pm-color-warning` | Upozornění |
| `--pm-color-error` | Chyba / destruktivní |
| `--pm-color-text` | Hlavní text |
| `--pm-color-text-muted` | Sekundární text |
| `--pm-color-background` | Pozadí stránky |
| `--pm-color-surface` | Pozadí karty / panelu |
| `--pm-color-border` | Hraniční linie |

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
