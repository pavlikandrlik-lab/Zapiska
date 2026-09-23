# Standardní gov hlavička a patička (upgrade gov 4.2.9 → 4.7.0) — specifikace

**Datum:** 2026-09-23
**Stav:** rozhodnutí uzavřena; doplněno o zjištění z přípravy plánu (§12), která
mají přednost před starším textem

## 1. Cíl

Nahradit vlastní hlavičku (`app-header`) a patičku (`app-footer`) **standardními
komponentami Design systému gov.cz** (`gov-header` / `gov-navigation` /
`gov-footer` / `gov-skip-links`) v **bílém** provedení, jaké mají standardní
gov weby (referenčně dia.gov.cz). Vzhled a struktura zbytku aplikace se nemění.

## 2. Zásadní rozhodnutí (odsouhlaseno 2026-09-23)

| Bod | Rozhodnutí |
|---|---|
| **Rozsah** | Vizuálně **jen hlavička + patička**. Page-heading a breadcrumbs zůstávají beze změny. |
| **Verze DS** | **Vanilla gov 4.7.0** (jádro + templates + fonty), **bez `index.css`** (§12.1). **Bez DS FIS nadstavby** (`ds-fis.css`/`ds-fis.js` se nenačítají) — jejím hlavním rysem je tmavě modrá hlavička (§1), kterou nechceme. |
| **Bílá hlavička** | Plyne z vypuštění DS FIS — vanilla gov hlavička je bílá. |
| **Získání assetů** | **Kopie předsestaveného** `assets/gov` z `DesignSystem-FIS-v1.0.0/` do `wwwroot/assets/gov`. Žádný Node v buildu ani runtime (sedí na offline nasazení). |
| **Vyhledávání** | Ponechat stávající **Task-7 dropdown** (`app-search-*`, authz-filtrovaný, záznamocentrický) vsazený do search slotu nové hlavičky. Nepoužívat `gov-form-autocomplete`. |
| **Motiv** | **Nativní** `gov-theme-switch` (gov 4.7.0) + tenký cookie most pro první vykreslení (§9.1). Náš dark-mode CSS i gov jedou oba na `[data-theme="dark"]`. Bez uložené volby **podle systému** (§12.4). |
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
  ├─ components/  (core.css, core.esm.js, p-*.entry.js — 4.7.0 jádro;
  │                core.css se kopíruje, ale NENAČÍTÁ — §12.9)
  ├─ styles/      (tokens, templates-tokens, styles, layout, components,
  │                templates, animations, content, skip-links, index — 10 CSS;
  │                index.css se kopíruje, ale nenačítá — §12.1)
  ├─ fonts/       (roboto*.woff2 + roboto.css)
  ├─ templates/   (scripts.js)
  └─ (logo zůstává ~/images/zapiska-logo.svg — z kitu se logo nekopíruje, §9.2)
```

Zdroj = `DesignSystem-FIS-v1.0.0/assets/gov`, kopíruje se beze změn.
`ds-fis/` složka se **nekopíruje**. Složka `icons/` z kitu se **nekopíruje** —
ikony zůstávají v aplikačním `wwwroot/assets/icons/` (§12.2).

Stará `wwwroot/lib/gov-design-system/` (4.2.9) se po ověření odstraní; do té
doby zůstává, aby šlo srovnávat.

### 4.2 Pořadí načítání v `<head>` (dle MANUAL Část B, B5 — bez ds-fis a bez index.css)

> Aktuální podoba je v §12 (bez `index.css`, `iconsPath` na `/assets/icons`,
> inline skript motivu). Níže je původní výchozí návrh z MANUALu.

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
| Navigace | `<nav class="app-nav">`, `app-` odkazy, `NavClass()` active | `<nav class="gov-navigation" id="main-navigation">`, aktivní položka `aria-current="page"` + app- zvýraznění (§12.6) |
| Skip-links | jeden `.skip-link` na obsah | `gov-skip-links` (navigace + obsah) |
| Uživatel | vlastní `user-menu` (button+panel, JS) | `gov-dropdown` s ikonou osoby; položky Můj profil + Moje práva, **bez Odhlásit** (§12.3) |
| Motiv | `theme.js` 3-stav + cookie | nativní `gov-theme-switch` + tenký cookie most (§9.1) |
| Search | `app-search` form + Task-7 dropdown | tentýž dropdown v search slotu `gov-header` |
| Patička | `<footer class="app-footer">`, odkazy na Dokumentaci | `<footer class="gov-footer">`, sloupce + verze `DS gov.cz 4.7.0` |
| gov jádro | 4.2.9 (`lib/`) | 4.7.0 (`assets/gov/`) |

## 6. Co zůstává beze změny

- Page-heading, `_BreadcrumbBar`, `BreadcrumbTrail` (mimo rozsah).
- Vyhledávací dropdown a jeho JS/CSS (`global-search.js`, `app-search-*`).
- Aplikační komponentní CSS v `site.css` (karty, harmonogram, dashboard…) —
  **kód se nemění**, ale vizuál se ověřuje po upgradu jádra (§8).
- `theme.js` zůstává jen jako tenký cookie most (§9.1).

## 7. Rizika

- **Upgrade jádra 4.2.9 → 4.7.0 přerámuje všechny gov komponenty.** Napříč
  pamětí projektu je řada gov-verzních gotchas (gov-button textContent/slot,
  gov-dialog block-close se měnilo mezi verzemi, gov-icon typy). Po upgradu je
  nutné projít komponenty ručně (§8).
- ~~Logout jako app- odchylka~~ — zrušeno, logout v aplikaci neexistuje (§12.3).
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

## 9. Doplňující rozhodnutí (2026-09-23)

1. **Motiv — nativní + tenký cookie most.** Přepínač je nativní `gov-theme-switch`
   4.7.0. Z `theme.js` zůstane jen tenký most: při vykreslení serverem nastaví
   `data-theme` podle cookie `pmtracker.theme.mode` (žádné probliknutí světlého
   motivu při načtení v tmavém režimu) a při `gov-change` volbu do cookie uloží.
   Vlastní 3-stavová logika a UI nad přepínačem se ruší.
2. **Logo — Zápiska.** V hlavičce zůstává `zapiska-logo.svg` a název „Zápiska".
   Logo z kitu (`logo_main_*.png`) se nekopíruje.
3. **Obsah patičky — výchozí mapování.** Stávající odkazy (Uživatelská příručka,
   Technická dokumentace, Q&A, Changelog) jdou do sloupců `gov-footer`; řádek
   verze ve formátu `Verze … · DS gov.cz 4.7.0`.
4. **Šířka — nechat nesourodě.** Hlavička a patička mají standardní gov šířku
   obsahu (~1200 px), obsah stránek si ponechá dnešní šířku (fluid tier u datových
   stránek). Okraje nebudou lícovat — přijato jako vědomý kompromis, žádná
   `app-` odchylka šířky se nezavádí.

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

## 12. Zjištění při přípravě plánu a rozhodnutí (2026-09-23)

Průzkum kódu a kitu při psaní plánu ukázal místa, kde výchozí návrh nesedí.
Kde se text výše s touto sekcí rozchází, platí tato sekce.

1. **`index.css` se nenačítá (rozhodnutí uživatele).** 4.7.0 `styles.css` je
   prakticky totéž, co aplikace už dnes dostává z 4.2.9 `core.min.css` (velikosti
   nadpisů, barva odkazů, odrážky `●`, utility `gov-text--*`); nově přidává jen
   `h1–h6, p { margin: 0 }` a drobnosti kolem focusu. Všechna velká globální
   pravidla jsou v `index.css`: `main { display:flex; gap }`, `section > * { margin-top }`
   (79× `<section>` ve view), reset `fieldset`, `address`. Ty by přestylovaly
   celou aplikaci, proto se `index.css` vynechává. Načítá se 9 z 10 CSS v pořadí
   MANUALu. Odchylka od MANUAL B5 → zapsat do `docs/known-issues/ds-fis-odchylky.md`.
   Z `index.css` patička potřebuje jen `address { font-style: normal }` → jedno
   `app-` pravidlo.
2. **Ikony zůstávají v `/assets/icons`.** Aplikace používá 22 ikon, které kit
   nemá (pencil, plus, trash, save, printer, lock…). Do `assets/gov/**` se sahat
   nesmí (pravidlo 1), takže `iconsPath` míří na aplikační strom
   `wwwroot/assets/icons` (to je i výchozí hodnota gov). Ikony, které má kit a
   aplikace ne (21 kusů, mj. `person-fill` pro menu uživatele), se do aplikačního
   stromu doplní; společné ikony zůstávají v aplikační verzi. Odchylka od
   MANUAL B („`iconsPath` musí ukazovat na `…/gov/icons`") → zapsat.
3. **Odhlásit vypadává.** Logout v aplikaci neexistuje a autentizace je IIS
   Windows Auth, která odhlášení neumí. Menu uživatele nese jméno, e-mail,
   organizaci, role a odkazy Můj profil + Moje práva (jako dnes).
4. **Motiv bez uložené volby = podle systému (rozhodnutí uživatele).** gov 4.7.0
   v režimu auto (bez `data-theme`) přepne podle systému **jen gov tokeny**; naše
   `site.css` reaguje jen na `[data-theme="dark"]` (161×) → vznikla by napůl tmavá
   stránka. Tenký most proto: server vykreslí `data-theme` jen z cookie
   `pmtracker.theme.mode` s hodnotou `dark`/`light`; jinak inline skript v `<head>`
   dosadí `data-theme` podle `prefers-color-scheme` ještě před prvním vykreslením.
   `gov-theme-switch` si volbu sám drží v **session** cookie `data-theme`; most při
   `gov-change` (filtr `detail.component === "gov-theme-switch"`, událost bublá)
   uloží volbu i do roční cookie `pmtracker.theme.mode`. Atribut `data-theme-mode`,
   localStorage a sledování změny systému za běhu se ruší.
5. **Hlavička nepřilepená — vanilla (rozhodnutí uživatele).** Dnes je přilepená
   celá hlavička včetně menu a drobečků. Nově odjede se stránkou i drobečková lišta
   (`.app-breadcrumb-bar` ztratí `position: sticky`). Posun kotev
   (`scroll-padding-top`, `scroll-margin-top`) a `top` přilepených postranních
   panelů (`.docs-toc`, `.gantt-picker`, `.ciselniky-sidebar`, `.settings-sidebar`)
   přestanou počítat s výškou hlavičky → společná proměnná `--app-sticky-top`.
   Dashboard přehled dál vyplní obrazovku: `header-height.js` měří vzdálenost
   od vršku stránky k `#main`.
6. **Aktivní položka navigace (rozhodnutí uživatele).** Vanilla `gov-navigation`
   nemá styl aktuální stránky. `aria-current="page"` na aktivním odkazu (stejná
   logika jako dnešní `NavClass`, na dashboardu se nezvýrazňuje nic) + `app-`
   zvýraznění. Odchylka → zapsat.
7. **Patička** má dnes 3 sloupce (Dokumenty / Informace / Podpora), ne jen
   4 odkazy — přenesou se 1:1 do `gov-footer`. Copyright z `index.html` se
   nepřidává (nemáme ho dnes a nebudeme vymýšlet vlastníka).
8. **Mobil (< 48 em):** vanilla schová `.gov-header__action` (přepínač motivu +
   menu uživatele) a ukáže `.gov-header__mobile` s hamburgerem. Přijímáme jako
   vanilla chování (aplikace je desktopová).
9. **`components/core.css` 4.7.0** obsahuje `@font-face` s cestami
   `/playground/build/assets/fonts/…` (stejný problém, kvůli kterému se v 4.2.9
   upravoval soubor DS). MANUAL ho nenačítá, fonty jdou přes `fonts/roboto.css`.
   Kopíruje se beze změny a test hlídá, že ho layout nenačítá.
10. **Stránka výsledků hledání** používá `gov-page-heading` a `gov-card-grid`, které
    teprve 4.7.0 `templates.css`/`layout.css` stylují → vizuální kontrola (§8).
11. **Utility třídy** (`gov-text--*`, `gov-color--*`, `gov-list--plain`) zůstávají
    k dispozici (jsou ve `styles.css`, který se načítá).
