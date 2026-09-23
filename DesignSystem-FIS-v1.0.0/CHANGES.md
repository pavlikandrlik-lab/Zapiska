# DS FIS v1.0.0 — změny oproti Design systému gov.cz

Aplikační šablona **Obecná aplikace** staví na **Design systému gov.cz
v4.7.0** a přidává nad něj tenkou nadstavbu **DS FIS**.

- **`assets/ds-fis/ds-fis.css`** – všechny vizuální odchylky (jeden soubor)
- **`assets/ds-fis/ds-fis.js`** – doplňkové chování (jeden soubor)

Nadstavba **nemění žádný zdrojový soubor DS**. Pracuje výhradně přes tokeny
a veřejné selektory DS. Odebráním obou `<link>` / `<script>` řádků z
`index.html` se aplikace vrátí k čistému DS gov v4.

Verze DS: `@gov-design-system-ce/{styles,templates,components,fonts,icons}` **4.7.0**.

---

## Přehled odchylek

| # | Oblast | Co se mění | Kde (ds-fis.css / .js) |
|---|--------|-----------|------------------------|
| 1 | **Hlavička – barva** | Tmavě modrá hlavička i pruh navigace (`--color-primary-900`, `#0c3372`, shodné s patičkou). Ve v4 je hlavička bílá. | css §1 |
| 2 | **Šířka obsahu** | Nad 1600 px se obsah rozšíří na 1536 px, nad 2048 px na 90 % šířky okna (DS má napevno 1200 px). Nikdy „až do krajů". Na širokoúhlých monitorech je navíc hlavní navigace držena v užším středovém pásu (75 % / 60 %), aby položky nebyly příliš daleko od sebe. | css §2 |
| 3 | **Rozvržení hlavičky** | Vyhledávací pole je na střed, název stránky má přednost a hledání ho nikdy nepřekryje (obě strany rostou stejně, `flex: 1 1 0`). | css §3 |
| 4 | **Mobil / tablet (< 1200 px)** | Široká lišta hledání se schová, ovládá se lupou; ikony 🔍 uživatel ☰ v jedné řadě vpravo (bílé); hlavní navigace = rozbalovací panel; podmenu se rozbalují **pod** položkou jako plochý blok na celou šířku panelu (bez rámu, zaoblení a stínu – dle DS „main-navigation" v mobilním zobrazení). | css §3–4, js §2, §4 |
| 5 | **Našeptávač** | `gov-form-autocomplete` uvnitř `gov-form-search`; seznam výsledků má šířku celého pole, je 6 px pod ním, má neprůhledné pozadí a jemné modré orámování; položky mají stejnou výšku (48 px), bez odrážek a mezer z generického `<ul><li>` DS. | css §5, js §1 |
| 6 | **Rozbalovací boxy hlavičky (jen desktop)** | Sekce / Agendy: jemné modré orámování + měkký stín; horní hrana úzkého menu lícuje se spodní hranou hlavičky; celoširoké mega-menu roztaženo přesně na šířku obsahu (DS má napevno `width:100vw; max-width:1240px`). Pod zlomem hlavičky se neuplatní (viz §4). | css §6 |
| 7 | **Menu uživatele** | Ikona osoby před jménem; položky „Můj profil / Nastavení účtu" zarovnané doprava; oddělovač + řádek „Platnost hesla: …"; „Odhlásit se" odebráno (v `index.html`); vnitřní seznam skryt do hydratace (jinak probliknutí při refreshi). | css §7, `index.html` |
| 8 | **Patička** | Zmírněné odsazení (`padding-block` z `--spacing-3xl` na `--spacing-2xl`, mezera mezi bloky z `3xl` na `2xl`) – DS působí roztahaně. | css §8 |
| 9 | **Úvodní text pod nadpisem** | `.gov-text-content` bez `max-width` – využije celou šířku obsahu (DS ho omezuje na ~polovinu). | css §9 |
| 10 | **Přeteklá položka „Další"** | Skript DS na úzkém displeji přesune položky menu do rozbalovací položky „Další". DS FIS ji hned potlačí a položky vrátí do menu (`MutationObserver`). | css §10, js §3 |
| 11 | **Zavírání rozbalených menu** | Zavře se klikem **levým** tlačítkem kdekoli mimo právě otevřený panel; pravé tlačítko ani klik na odkaz uvnitř panelu nic nezavírá (kvůli „otevřít v nové kartě" a čekání na načtení stránky). | js §5 |
| 12 | **Přilepená patička** | `body` je `flex` sloupec s `min-height: 100dvh`, `#main` má `flex: 1 0 auto` – u krátké stránky drží patička spodní okraj okna, u dlouhé se chová normálně. | css §11 |

---

## Detailní popis

### 1) Tmavě modrá hlavička (ds-fis.css §1)

Inspirováno modrou hlavičkou gov.cz **v3**; ve v4 tento styl není. Barva
sjednocena s patičkou přes token DS `--color-primary-900`.

- `.gov-header` dostává `background-color` + přepisuje `--component-nav-background`
  a `--component-nav-separator` (tokeny, které hlavička DS používá).
- Bílý text/ikony: logo (PNG `logo_main_white.png`), název, podnázev, odkazy
  navigace, spouštěcí tlačítka `base` (přes proměnnou `--color` webové
  komponenty `gov-button`).
- **Jen přímé potomky** hlavičky – ne položky uvnitř rozbalených panelů,
  ty mají světlé pozadí a musí zůstat tmavé.

Návrat k bílé hlavičce: smazat celou sekci §1.

### 2) Širší obsahová plocha (ds-fis.css §2)

Přepisuje token `--templates-layout-page-limit-max` (a `--container-width`)
v `@media (min-width: 100rem)` (96rem) a `128rem` (90 %). Roztáhne se
konzistentně obsah, pruh navigace i patička.

Aby se na velmi širokých monitorech hlavní navigace neroztáhla přes celou
šířku obsahu (položky daleko od sebe), je `.gov-header__navigation.js-gov-header__navigation`
nad `100rem` omezena na `max-width: 75%` a nad `128rem` na `60%`
(`margin-inline: auto`). Platí jen pro desktop – pod zlomem hlavičky je
navigace plovoucí panel na celou šířku (§4).

### 3–4) Rozvržení hlavičky a mobilní zobrazení (ds-fis.css §3–4, ds-fis.js §2, §4)

**Nové prvky v `index.html`** (mimo šablonu DS):

| Selektor | Účel |
|----------|------|
| `.app-header-left` | levý blok hlavičky (logo + název) |
| `.app-header-title` / `.app-header-subtitle` | název / podnázev (kvůli barvě a ořezu) |
| `.gov-header__mobile` + `.js-ac-mobile-toggle` | tlačítko lupy (mobil/tablet) |
| `.app-hamburger` + `.js-gov-header__navigation-trigger` | tlačítko hamburgeru |
| `.app-logo` | `<img>` loga |

- **Desktop (≥ 75 em):** `.app-header-left` a `.gov-header__action` mají
  `flex: 1 1 0`, pole se šířkou stupňovanou dle okna (24 rem od 75 em,
  30 rem od 90 em, 38 rem od 106 em) → hledání na středu, v úzkém pásmu
  (~1280 px) nepřekrývá název aplikace. Celý řetězec obalů vlevo má
  `min-width: 0`, takže se název případně ořízne `…`.
- **Tablet/mobil (< 75 em):** `.gov-header__content` je `flex-direction: row`
  (DS má na mobilu `column`); pole hledání je `position: absolute` panel pod
  hlavičkou, přepínaný třídou `body.app-search-open`; hlavní navigace je
  `position: absolute` panel, přepínaný atributem `hidden`; každé `<li>` menu je
  `display: block` → podmenu se rozbalí **pod** spouštěčem jako plochý blok
  na celou šířku panelu (`position: static`, `background: --background-block-primary`,
  bez rámu / zaoblení / stínu) – shodně s DS „main-navigation" v mobilním
  zobrazení.

**`ds-fis.js`:**
- `toggleMobileSearch()` – lupa přepíná `body.app-search-open`, zavře navigaci.
- `toggleMobileNav()` – hamburger přepíná `hidden` na `.js-gov-header__navigation`,
  vlastní obsluha v capture fázi + `stopPropagation` (nepere se se skriptem DS),
  posluchač i na `gov-click`.
- `collapseNavOnMobile()` – po načtení / při resize drží navigaci sbalenou
  pod 75 em (skript DS ji po ~500 ms znovu odkrývá, proto se volá i se zpožděním).

### 5) Našeptávač (ds-fis.css §5, ds-fis.js §1)

**Markup v `index.html`:** `gov-form-search` (chrom pole – lupa vpravo jako
modré tlačítko `slot="button"`, křížek `slot="button-erase"`) s vnořenou
komponentou `gov-form-autocomplete` (`slot="input"`).

- `#search gov-form-autocomplete { position: static }` → seznam
  `.gov-form-autocomplete__list` se polohuje vůči `#search`, takže má šířku
  celého pole (ne jen vnitřní části).
- `top: calc(100% + 0.375rem)` – 6 px pod polem, nepřekrývá orámování.
- `background-color: var(--background-block-primary)` – neprůhledné.
- `z-index: 40` – nad pruhem navigace (ten má `z-index: 10`).
- Reset generického `<ul><li>` z `styles.css` DS: bez odrážek `::before`,
  bez `margin-bottom`, bez `padding-left`; `.gov-form-autocomplete__item` je
  `display: flex; align-items: center; min-height: 3rem` → stejně vysoké řádky.

**`ds-fis.js`:** `initAutocomplete()` naplní `ac.options` (pole `{name}`),
nastaví `max-options="6"`, obslouží tlačítko „vymazat" (`.js-ac-clear` →
`clearValue()`), zobrazuje ho jen když je v poli text.
Pole `SEARCH_ITEMS` je ukázkové – v ostrém provozu je nahraďte voláním API.

### 6) Rozbalovací boxy hlavičky (ds-fis.css §6)

Celá sekce je v `@media (min-width: 75em)` – platí jen pro desktopové
plovoucí menu. Pod zlomem hlavičky jsou podmenu ploché bloky v panelu
(§4), dle vzhledu DS v mobilním zobrazení.

- `.gov-subnavigation`, `.gov-mega-menu` v hlavičce: `border` +
  `box-shadow` v modré `color-mix(--color-primary-900 …)`.
- `.gov-mega-menu`: `left: 0; width: 100%; max-width: none` – DS ho polohuje
  napevno `width: 100vw; max-width: 1240px`, což při širším rozvržení nesedí.
- `.gov-subnavigation { top: calc(var(--height-component-l) + var(--templates-margin-s)) }`
  – výchozí `top` počítá jen s výškou tlačítka, ne s dolním odsazením pruhu
  navigace, takže menu začínalo nad spodní hranou hlavičky.

### 7) Menu uživatele (ds-fis.css §7 + `index.html`)

- `<gov-icon slot="icon-start" name="person-fill">` před jménem.
- `<span class="app-user-name">` kolem jména (na mobilu se skrývá).
- `.gov-dropdown__list gov-button .element { justify-content: flex-end }` –
  položky doprava.
- `<hr class="app-user-menu__sep">` + `<li class="app-user-menu__info">` s
  textem „Platnost hesla: …".
- Položka **„Odhlásit se" odebrána** z markupu.
- `gov-dropdown:not(.hydrated) [slot="list"] { display: none }` – zabrání
  probliknutí rozbaleného boxu při refreshi (než komponenta naběhne).

### 8) Patička (ds-fis.css §8)

`.gov-footer { padding-block: var(--spacing-2xl) }`. Mezera mezi bloky
patičky `.gov-footer__content` je `var(--spacing-2xl)` jen od 48 em; na
mobilu `var(--spacing-l)` (jinak zbytečně velký odskok). Copyright i verze
mají stejnou menší velikost `var(--font-size-body-xs)` (DS dává verzi
`body-m`). Obsah patičky v `index.html` je generický a zkrácený; kontaktní
sloupec je „Hotline / Helpdesk" na dvou řádcích (odkaz na podporu + telefon).

### 9) Úvodní text (ds-fis.css §9)

`.gov-text-content { max-width: none }` – DS ho omezuje na
`var(--templates-width-max-4xl)`.

### 10) Položka „Další" (ds-fis.css §10, ds-fis.js §3)

CSS ji skryje (`display: none`), `undeferNav()` v JS přesune její obsah zpět
do hlavního `<ul>` a kontejner smaže; `MutationObserver` to hlídá i při
každém dalším vytvoření (skript DS ho vytváří při resize).

### 11) Zavírání rozbalených menu (ds-fis.js §5)

Posluchač `pointerdown` v capture fázi:
- ignoruje jiné než levé tlačítko (`e.button !== 0`),
- pokud je klik **uvnitř** otevřeného panelu (`.gov-subnavigation`,
  `.gov-mega-menu`), nic nedělá,
- jinak zavře všechna otevřená menu (`aria-expanded=false`, `hidden`,
  ikona zpět na `chevron-down`).
`Escape` zavře vše. Menu účtu (`gov-dropdown`) si zavření mimo řeší samo.

### 12) Přilepená patička (ds-fis.css §11)

`body { display: flex; flex-direction: column; min-height: 100dvh }`
(`100vh` jako fallback), `body > #main { flex: 1 0 auto }`, hlavička a
patička `flex: 0 0 auto`. U krátké stránky obsah dorovná zbývající výšku
okna a patička sedí na jeho spodním okraji; u dlouhé stránky se stránka
normálně roluje.

---

## Nové třídy / atributy zavedené DS FIS

Prefix **`app-`** = strukturální/vzhledová třída DS FIS.
Prefix **`js-`** = háček pro `ds-fis.js` (nikdy se na něj nestyluje).

```
.app-header-left        .app-header-title      .app-header-subtitle
.app-logo               .app-hamburger         .app-user-name
.app-user-menu__sep     .app-user-menu__info
.app-search-open        (třída na <body>, přepíná mobilní hledání)

.js-ac-mobile-toggle    .js-ac-clear
.js-gov-header__navigation-trigger   (název očekává i skript DS)
```

## Ikony gov použité navíc oproti čistému markupu šablony

`person-fill`, `list`, `x`, `search`, `chevron-down`, `chevron-right`,
`briefcase`, `info-circle` (vše z `@gov-design-system-ce/icons`, adresář
`components`).
