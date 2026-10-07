# Lokální odchylky od Design systému FIS

Evidence podle pravidla 4 v `DesignSystem-FIS-v1.0.0/README.md`: odchylka se
nedělá v souborech Design systému, ale v souborech aplikace s prefixem `app-`,
a poznamená se sem.

## 1. Dropdown vyhledávání (2026-09-17)

**Čeho se týká:** `gov-form-autocomplete` v hlavičce.

**Co DS předepisuje:** vyhledávací pole s komponentou `gov-form-autocomplete`,
která si seznam výsledků vykresluje sama. Plní se přes property `options`
plochými řetězci (`[{ name: "text" }]`).

**Proč se odchylujeme:** komponenta nemá API pro vlastní vykreslení položky.
Zadání vyžaduje dvouřádkovou položku se zkratkou subsystému a číslem jednání
zarovnanými vpravo a se zvýrazněnou shodou — to se do plochého řetězce nevejde.

**Jak je odchylka provedena:** vyhledávací **pole** zůstává gov
(`gov-form-search` + `gov-form-input`). **Seznam výsledků** kreslí aplikace
v `.app-search-dropdown` / `.app-search-item*` (soubory `wwwroot/js/global-search.js`
a `wwwroot/css/site.css`). Soubory `assets/gov/**` ani `assets/ds-fis/*` se needitují.

Napojení na gov pole (2026-10-06):

- `global-search.js` je ES modul. Na vnitřní `<input name="q">` se napojí až po
  hydrataci `gov-form-input` (`customElements.whenDefined` + `componentOnReady()`),
  protože ten input vzniká až při vykreslení komponenty.
- Viditelnost křížku řídí třída `app-search--has-query` na formuláři. Atribut `hidden`
  na slotovaném `gov-button` nevydrží, protože ho Stencil při vykreslení
  `gov-form-search` přepíše na `false`.
- `autocomplete` je u `gov-form-input` boolean, proto `autocomplete="false"`.
  Hodnota `"off"` by našeptávač prohlížeče zapnula.

**Podklad pro případnou centrální úpravu DS:** `gov-form-autocomplete` by
potřebovala slot nebo callback pro vykreslení položky (obdoba `renderOption`),
aby šlo zobrazit víceřádkovou položku s metadaty. Patřilo by to do DS gov
(komponenta), ne do nadstavby DS FIS.

## 2. `index.css` DS gov se nenačítá (2026-09-23)

**Čeho se týká:** pořadí CSS v `<head>` podle MANUAL, Část B, B5.

**Co DS předepisuje:** načíst všech 10 souborů `assets/gov/styles/*.css` včetně `index.css`.

**Proč se odchylujeme:** `index.css` nese pravidla šablony stránky pro obsah uvnitř
`<gov-container id="main">`: `main { display:flex; flex-direction:column; gap }`,
`section > *:not(gov-layout-column) { margin-top }`, `section h2 + p { margin-top }`,
`fieldset`, `address`, `picture`. Aplikace obsah do `gov-container` nebalí (odchylka č. 5)
a má vlastní rozložení; tato pravidla by přestylovala celou aplikaci (79× `<section>`
ve view). Ostatních 9 souborů se načítá v pořadí MANUALu.

**Jak je odchylka provedena:** `index.css` se kopíruje s kitem beze změny, jen ho
`_Layout.cshtml` nenačítá. Z jeho pravidel patička potřebuje jen `address { font-style: normal }`
→ třída `app-address` v `site.css`. `[hidden]` a skip-links pokrývají `components.css`
a `skip-links.css`. Soubory `assets/gov/**` se needitují.

**Podklad pro centrální úpravu DS:** v balíčku `@gov-design-system-ce/styles` oddělit
pravidla šablony stránky (`main`, `section`) od pravidel potřebných pro hlavičku a patičku,
aby šlo DS nasadit do existující aplikace po částech. Patří do DS gov.

## 3. `iconsPath` míří na aplikační ikony (2026-09-23)

**Čeho se týká:** `window.GOV_DS_CONFIG.iconsPath` (MANUAL B: „musí ukazovat na `…/gov/icons`").

**Proč se odchylujeme:** aplikace používá 22 ikon `type="components"`, které sada kitu
(57 ikon) nemá — pencil, plus, trash, save, printer, lock… Do `assets/gov/**` se nesmí
nic přidávat (pravidlo 1) a `gov-icon` bere ikony z jediného kořene.

**Jak je odchylka provedena:** `iconsPath` = `~/assets/icons` (výchozí hodnota gov),
tj. aplikační `wwwroot/assets/icons/components/`. Obsahuje dosavadní ikony (Bootstrap
Icons 1.11.3, stahujeme je sami) + 21 ikon kitu, které aplikace neměla (mj. `person-fill`).
Společné ikony zůstávají v aplikační verzi (stejné glyfy). Složka `icons/` kitu se do
`wwwroot/assets/gov` nekopíruje. `GovAssets470Tests` hlídá, že strom je nadmnožinou sady
kitu a že existuje každá ikona použitá ve view/JS.

**Podklad pro centrální úpravu DS:** rozšířit `@gov-design-system-ce/icons` o běžné
akční ikony (pencil, plus, trash, save, printer, lock, unlock, calendar…), nebo umožnit
v `gov-icon` více kořenů ikon.

## 4. Zvýraznění aktivní položky hlavní navigace (2026-09-23)

**Čeho se týká:** `gov-navigation` v hlavičce.

**Co DS předepisuje:** `templates.css` nemá styl aktuální stránky — navigace ji vizuálně neoznačuje.

**Proč se odchylujeme:** aplikace má šest sekcí a uživatelé byli zvyklí vidět, kde jsou.

**Jak je odchylka provedena:** `_Layout.cshtml` dává aktivnímu odkazu `aria-current="page"`
(přístupnost, v souladu s DS), a to i Přehledu. Vizuál řeší `.app-main-nav a[aria-current="page"]`
v `site.css` (třída `app-main-nav` na `<nav class="gov-navigation">`), jen tokeny DS: podbarvení
`--button-outlined-primary-active` (stisknuté tlačítko; světlý motiv `--color-primary-200`,
tmavý `--color-primary-900`), o stupeň sytější než hover, aby šla vybraná položka odlišit od
položky pod myší. Stejná barva platí i pro `:hover` vybrané položky. Podtržení bylo do
2026-10-07; uživatel ho nechce (zaoblené konce podle rohů odkazu), zvýraznění podbarvením ano.

**Podklad pro centrální úpravu DS:** doplnit do `templates.css` styl
`.gov-navigation a[aria-current="page"]`. Patří do DS gov.

## 5. Obsah stránek mimo `gov-container` a `gov-page-heading` (2026-09-23)

**Čeho se týká:** README kitu, pravidlo 2 („obsah se vkládá jen dovnitř `<gov-container id="main">`").

**Proč se odchylujeme:** rozsah přestavby je jen hlavička a patička (spec 2026-09-23 §2).
Stránky mají vlastní šířkové tiery (`app-main`, `app-main--fluid`) a drobečkovou lištu
`_BreadcrumbBar`; přestavba na `gov-container` + `gov-page-heading` by zasáhla všechny obrazovky.

**Jak je odchylka provedena:** obsah zůstává v `<main id="main" class="app-main">` (id `main`
je cíl skip-linku), drobečky v `_BreadcrumbBar` pod hlavičkou. Struktura hlavičky a patičky
odpovídá `index.html`.

## 6. Nižší hlavní navigace (2026-10-06)

**Čeho se týká:** `gov-navigation` v hlavičce. DS dává položkám výšku
`var(--height-component-l)` (3rem = 48 px).

**Proč se odchylujeme:** výslovné přání uživatele. Navigace s 48 px vysokými položkami
zabírá zbytečně moc místa, 25 px se mu líbí víc.

**Jak je odchylka provedena:** token `--height-component-l` sdílí všechny komponenty
velikosti L, proto se nemění globálně. `site.css` ho přepisuje jen na `.app-main-nav`
a jen od 48em. V rozbaleném mobilním menu zůstávají dotykové cíle 48 px.

## 7. Obsah hlavičky přes celou šířku obrazovky (2026-10-06)

**Čeho se týká:** `.gov-header__content` (logo, hledání, přepínač motivu, účet).
DS ho omezuje na `max-width: var(--templates-layout-page-limit-max)` (75rem).
Hledání má v kitu pevnou šířku `gov-search--fixed-width` (26.5rem).

**Proč se odchylujeme:** výslovné přání uživatele. Na širokém monitoru má horní řádek
využít celou šířku a hledání se má roztahovat jako před přechodem na gov hlavičku.

**Jak je odchylka provedena:**

- `.gov-header__content` má třídu `app-header-wide` a `site.css` jí ruší `max-width`.
- Formulář hledání nemá `gov-search--fixed-width`. Od 48em ho `.app-search` roztahuje
  mezi logem a akcemi (`flex: 1 1 auto`, nejvýš 560 px).
- Hlavní navigace (`.gov-header__navigation`) zůstává v šířce stránky DS.
