# Standardní gov hlavička a patička (upgrade gov 4.2.9 → 4.7.0) — specifikace

**Datum:** 2026-09-23
**Stav:** návrh k odsouhlasení

## 1. Cíl

Nahradit vlastní hlavičku (`app-header`) a patičku (`app-footer`) **standardními
komponentami Design systému gov.cz** (`gov-header` / `gov-navigation` /
`gov-footer` / `gov-skip-links`) v **bílém** provedení, jaké mají standardní
gov weby (referenčně dia.gov.cz). Vzhled a struktura zbytku aplikace se nemění.

## 2. Zásadní rozhodnutí (odsouhlaseno 2026-09-23)

| Bod | Rozhodnutí |
|---|---|
| **Rozsah** | Vizuálně **jen hlavička + patička**. Page-heading a breadcrumbs zůstávají beze změny. |
| **Verze DS** | **Plný vanilla gov 4.7.0** (jádro + templates + fonty). **Bez DS FIS nadstavby** (`ds-fis.css`/`ds-fis.js` se nenačítají) — jejím hlavním rysem je tmavě modrá hlavička (§1), kterou nechceme. |
| **Bílá hlavička** | Plyne z vypuštění DS FIS — vanilla gov hlavička je bílá. |
| **Získání assetů** | **Kopie předsestaveného** `assets/gov` z `DesignSystem-FIS-v1.0.0/` do `wwwroot/assets/gov`. Žádný Node v buildu ani runtime (sedí na offline nasazení). |
| **Vyhledávání** | Ponechat stávající **Task-7 dropdown** (`app-search-*`, authz-filtrovaný, záznamocentrický) vsazený do search slotu nové hlavičky. Nepoužívat `gov-form-autocomplete`. |
| **Motiv** | **Nativní** `gov-theme-switch` (gov 4.7.0). Náš dark-mode CSS i gov jedou oba na `[data-theme="dark"]`, takže je kompatibilní. |
| **Navigace** | Plochá, 6 položek gated oprávněními (jako dnes) namapovaných do `gov-navigation`. **Bez** mega-menu / submenu (YAGNI). |
| **Breadcrumbs** | Zůstává stávající `_BreadcrumbBar` + `BreadcrumbTrail`. **Mimo rozsah.** |

## 3. Klíčové zjištění, na kterém stojí rozsah

Aplikace má v 4.2.9 nabundlovanou **jen jádrovou vrstvu** gov komponent
(`dist/core` + `styles/lib`). **Vrstvu `templates`** (CSS `templates.css` +
`templates-tokens.css` + `templates/scripts.js`), která teprve dodává
`gov-header` / `gov-navigation` / `gov-footer` / `gov-page-heading`, **nikdy
neobsahovala**. Standardní gov hlavička proto v aplikaci není a nejde „jen
zapnout".

Templates vrstvu dodá až 4.7.0 (kit ji má předsestavenou). Protože se
nedoporučuje míchat verze (4.7.0 templates × 4.2.9 jádro), upgraduje se
**celé jádro na 4.7.0**. Důsledek: **všechny gov komponenty** v aplikaci
(gov-button, gov-form-*, gov-dialog, gov-icon, gov-tag, gov-message,
gov-form-switch, gov-stepper, gov-container…) se přerámují na 4.7.0. To je
cena rozhodnutí X a promítá se do rizik (§7) a vizuálního checklistu (§8).

## 4. Architektura

### 4.1 Assety ve `wwwroot`

Nově:

```
wwwroot/assets/gov/
  ├─ components/  (core.css, core.esm.js, p-*.entry.js — 4.7.0 jádro)
  ├─ styles/      (tokens, templates-tokens, styles, layout, components,
  │                templates, animations, content, skip-links, index — 10 CSS)
  ├─ fonts/       (roboto*.woff2 + roboto.css)
  ├─ icons/       (colored, complex, components, templates)
  ├─ logo_main_white.png, logo_main_dark.png (nebo vlastní logo Zápisky)
  └─ favicon.svg
```

Zdroj = `DesignSystem-FIS-v1.0.0/assets/gov` + kořenové logo/favicon.
`ds-fis/` složka se **nekopíruje**.

Stará `wwwroot/lib/gov-design-system/` (4.2.9) se po ověření odstraní; do té
doby zůstává, aby šlo srovnávat.

### 4.2 Pořadí načítání v `<head>` (dle MANUAL Část B, B5 — bez ds-fis)

```cshtml
<script>
  window.GOV_DS_CONFIG = { iconsPath: "/assets/gov/icons", canValidateWcagOnRender: false };
</script>

<link rel="stylesheet" href="~/assets/gov/styles/tokens.css" />
<link rel="stylesheet" href="~/assets/gov/styles/templates-tokens.css" />
<link rel="stylesheet" href="~/assets/gov/styles/styles.css" />
<link rel="stylesheet" href="~/assets/gov/styles/layout.css" />
<link rel="stylesheet" href="~/assets/gov/styles/components.css" />
<link rel="stylesheet" href="~/assets/gov/styles/templates.css" />
<link rel="stylesheet" href="~/assets/gov/styles/animations.css" />
<link rel="stylesheet" href="~/assets/gov/styles/content.css" />
<link rel="stylesheet" href="~/assets/gov/styles/skip-links.css" />
<link rel="stylesheet" href="~/assets/gov/styles/index.css" />
<link rel="stylesheet" href="~/assets/gov/fonts/roboto.css" />

@* naše aplikační CSS až PO gov (přebíjí jen app- třídy, ne gov tokeny) *@
<link rel="stylesheet" href="~/css/tokens.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/govcz.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />

<script type="module" src="~/assets/gov/components/core.esm.js"></script>
<script src="~/assets/gov/templates/scripts.js" defer></script>
<script>
  window.addEventListener("DOMContentLoaded", () => window.initTemplateScripts && window.initTemplateScripts());
</script>
```

`quill`, `site.js`, `global-search.js` zůstávají tak jako dnes.

### 4.3 Tělo layoutu

- `<body>` → `gov-skip-links` (`#main-navigation`, `#main`) → `gov-header`
  (logo Zápisky, search slot s naším dropdownem, `gov-theme-switch`, uživatelské
  `gov-dropdown`, hamburger) → `gov-navigation#main-navigation` (6 plochých
  položek gated oprávněními) → `RenderBody()` → `gov-footer`.
- Struktura `gov-header`/`gov-footer` se přebírá z `index.html` **bez** DS FIS
  specifik (žádné `app-header-left`/mega-menu; ponechat jen to, co je vanilla gov).
- `#main` a page-heading/breadcrumbs uvnitř `RenderBody` **zůstávají jako dnes**
  (nevkládáme `gov-container`/`gov-page-heading`, ať nezasáhneme mimo rozsah).

### 4.4 Program.cs

Do stávajícího `ContentTypeProvider` doplnit `.woff2` → `font/woff2` a
`.mjs` → `text/javascript`, pokud chybí. Pro `/assets/gov/**` a `.woff2`
nastavit `Cache-Control: public,max-age=31536000,immutable` v `OnPrepareResponse`
(neměnné assety). Zbytek `UseStaticFiles` beze změny.

## 5. Konkrétní změny (před → po)

| Prvek | Dnes | Po |
|---|---|---|
| Hlavička | `<header class="app-header">` + `app-topbar`, vlastní CSS | `<header class="gov-header">` (vanilla, bílá) |
| Navigace | `<nav class="app-nav">`, `app-` odkazy, `NavClass()` active | `<nav class="gov-navigation" id="main-navigation">`, aktivní stav gov třídou |
| Skip-links | nemá | `gov-skip-links` |
| Uživatel | vlastní `user-menu` (button+panel, JS) | `gov-dropdown` s ikonou osoby (+ **Odhlásit** jako app- odchylka, §7) |
| Motiv | `theme.js` 3-stav + cookie | nativní `gov-theme-switch` |
| Search | `app-search` form + Task-7 dropdown | tentýž dropdown v search slotu `gov-header` |
| Patička | `<footer class="app-footer">`, odkazy na Dokumentaci | `<footer class="gov-footer">`, sloupce + verze `DS gov.cz 4.7.0` |
| gov jádro | 4.2.9 (`lib/`) | 4.7.0 (`assets/gov/`) |

## 6. Co zůstává beze změny

- Page-heading, `_BreadcrumbBar`, `BreadcrumbTrail` (mimo rozsah).
- Vyhledávací dropdown a jeho JS/CSS (`global-search.js`, `app-search-*`).
- Aplikační komponentní CSS v `site.css` (karty, harmonogram, dashboard…) —
  **kód se nemění**, ale vizuál se ověřuje po upgradu jádra (§8).
- `theme.js` zůstane jen jako tenký most, pokud bude potřeba (viz §9 otevřený bod).

## 7. Rizika

- **Upgrade jádra 4.2.9 → 4.7.0 přerámuje všechny gov komponenty.** Napříč
  pamětí projektu je řada gov-verzních gotchas (gov-button textContent/slot,
  gov-dialog block-close se měnilo mezi verzemi, gov-icon typy). Po upgradu je
  nutné projít komponenty ručně (§8).
- **Logout není ve vzoru gov/DS FIS** (index.html menu účtu logout nemá). Přidání
  je lokální `app-` odchylka — poznamenat do `docs/known-issues/ds-fis-odchylky.md`.
- **Dvojí gov jádro po dobu přechodu** (`lib/` 4.2.9 + `assets/gov/` 4.7.0) — stará
  se smí smazat až po ověření, jinak konflikt definic komponent.

## 8. Vizuální kontrola po upgradu jádra (checklist do plánu)

Ruční průchod (dělá uživatel), reprezentativní obrazovky s gov komponentami:
- [ ] Přehled (dashboard) — gov-button, gov-tag, gov-message, ECharts kontejnery
- [ ] Detail projektu / záznamu — editor, gov-form-*, gov-form-switch, gov-stepper, harmonogram
- [ ] Modály — gov-dialog (zavírání křížkem, backdrop — viz paměť o block-close)
- [ ] Formuláře — gov-form-input/select/group/control, validace
- [ ] Ikony — gov-icon `type="components"` (Bootstrap Icons) se stále načítají (iconsPath)
- [ ] Dark mode na reprezentativních stránkách
- [ ] Edge/i15 (nosný layout, charset) — dle paměti

## 9. Otevřené body k rozhodnutí

1. **Persistence a first-paint motivu.** Náš `theme.js` dnes drží 3-stav
   (light/dark/auto) v cookie a nastaví `data-theme` ještě před vykreslením
   (bez probliknutí). Nativní gov-theme-switch může persistovat jinak
   (localStorage) a „auto" stav řešit jen přes `prefers-color-scheme`.
   **Rozhodnout:** čistě nativní (přijmout případné probliknutí a chování „auto"
   dle gov), nebo ponechat tenký cookie-most v `theme.js` jen pro server-side
   první vykreslení. *Doporučení: ponechat tenký most, pokud se objeví probliknutí.*
2. **Logo v hlavičce.** Kit má `logo_main_white.png`. Aplikace má
   `zapiska-logo.svg`. **Rozhodnout:** ponechat logo Zápisky (doporučeno), nebo
   gov generické logo.
3. **Obsah patičky.** Namapovat stávající odkazy (Uživatelská příručka,
   Technická dokumentace, Q&A, Changelog) do sloupců `gov-footer`. Text verze
   ponechat aplikační. **Rozhodnout jen, pokud chceš jiné rozdělení sloupců.**
4. **Šířka obsahu.** Vanilla gov omezuje obsah na ~1200 px; aplikace je dnes
   širší (fluid tier). Hlavička/patička budou gov-šířky. **Rozhodnout:** srovnat
   šířku obsahu s gov, nebo nechat obsah širší než chrome (může vypadat nesourodě).

## 10. Testy

- **Api render test** na `_Layout`: obsahuje `gov-header`, `gov-navigation`,
  `gov-footer`, `gov-skip-links`; neobsahuje staré `app-header`/`app-nav`/`app-footer`.
- **Unit (soubor) test**: `<head>` má gov CSS ve správném pořadí a **nenačítá**
  `ds-fis.css`/`ds-fis.js` ani starou `lib/gov-design-system`.
- **Offline asset test**: `/assets/gov/components/core.esm.js` a klíčové CSS
  existují ve `wwwroot` (žádné CDN).
- **Guard**: navigační položky zůstávají gated stejnými oprávněními (CanViewPeople…).
- Ruční: §8 checklist + skip-links + mobilní hamburger + přepnutí motivu.

## 11. Mimo rozsah

- Page-heading / breadcrumbs redesign, mega-menu, `gov-container` obalení obsahu.
- DS FIS nadstavba (`ds-fis.css`/`ds-fis.js`).
- Sjednocení `JednaniController` visibility s `ProjectVisibilityResolver`
  (nález z code review, samostatný follow-up).
- Přepis vyhledávacího dropdownu na `gov-form-autocomplete`.
