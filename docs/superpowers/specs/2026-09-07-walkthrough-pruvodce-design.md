# Průvodce aplikací (walkthrough) — design

**Datum:** 2026-09-07
**Stav:** zadání pro implementaci
**Typ:** nový sdílený subsystém (knihovna + prvek menu)

---

## 1. Cíl

Zavést **průvodce aplikací** — ztmavené pozadí, vysvícený jeden prvek stránky, bublina
s popisem, navigace Další / Zpět. Slouží novému uživateli k orientaci.

Průvodce má být **znovupoužitelný i pro další aplikace resortu**, aby uživatelé měli
jednotný průvodce napříč systémy, přestože obsah a data jsou v každé aplikaci jiná.

**Terminologie:** tomuto vzoru se říká *product tour* / *guided tour* / *coach marks*.
Není to *wizard* (vícekrokový formulář) — ten řeší jiný problém a není předmětem tohoto zadání.

---

## 2. Rozhodnutí

### Engine: **Shepherd.js**, verze 15.3.0, licence MIT

Vendorovat do `wwwroot/lib/shepherd/` stejným způsobem jako gov-design-system a Quill.
Potřebné soubory z npm balíčku `shepherd.js@15.3.0`:

| Soubor | Velikost | Účel |
|---|---|---|
| `dist/js/shepherd.mjs` | ~46 kB | ESM bundle |
| `dist/css/shepherd.css` | ~3,5 kB | minimální default styling |

**První krok implementace — ověřit soběstačnost bundlu:**
```
grep -n 'from "@floating-ui\|from "\(?!\.\)' shepherd.mjs
```
`shepherd.mjs` musí mít Floating UI **inlinovaný**, žádné bare importy. Bez toho ho
na offline intranetu nerozběhneš. Velikost 46 kB tomu odpovídá, ale ověř to explicitně
před vendorováním.

### Proč Shepherd a ne driver.js

Rozhodl o tom požadavek na **reakci na role**. Uživatel s jinou rolí vidí jiná tlačítka
a jiné položky projektového menu, takže průvodce musí kroky přeskakovat nebo měnit text.

| Požadavek | Shepherd.js | driver.js |
|---|---|---|
| Přeskočit krok podle podmínky | `showOn: () => boolean` — per-krok predikát | **nemá**; jen globální `skipMissingElement` |
| Jiný text podle kontextu | `text` smí být **funkce** vyhodnocená při stavbě kroku | text je statický; nutno přepisovat DOM v `onPopoverRender` |
| Bublina bez prvku (placeholder) | vynechat `attachTo` → bublina uprostřed | vynechat `element` → popover uprostřed |
| Prvek se dorenderuje později | `attachTo.element` jako funkce (lazy, before-show) + `beforeShowPromise` | `element` jako funkce + `waitForElement` (v ms — hádá se čas) |
| Pozicování bubliny | Floating UI (best-in-class) | vlastní implementace |
| ESC ukončí | `exitOnEsc: true` **defaultně** | nutno doprogramovat |
| Šipky ←/→ | `keyboardNavigation: true` **defaultně** | nutno doprogramovat |
| Křížek | `cancelIcon: { enabled: true }` | součást popoveru |

Rozhodující argument není pohodlí u jedné túry, ale to, že jde o **sdílenou knihovnu pro
víc aplikací**. U Shepherdu žije podmíněnost deklarativně uvnitř definice kroku, takže ji
sdílený formát unese. U driver.js by podmíněnost žila v imperativním kódu každé aplikace
zvlášť — knihovna by si `showOn` musela implementovat znovu.

### Zamítnuté varianty (neotevírat znovu)

| Varianta | Důvod zamítnutí |
|---|---|
| **driver.js** | Nemá per-krok podmíněnost. Jeho přednosti (5 kB, nula závislostí) jsou tady bezcenné — aplikace už vozí Quill i celý Stencil bundle. |
| **Intro.js** | Licence **AGPL-3.0** — copyleft, nasazení by vyžadovalo zveřejnit zdroják. Komerční licence 9,99–299,99 USD. Vyřazeno licenčně. |
| **Tailwind CSS** | Není tour knihovna. Navíc: v4 vyžaduje `@property` / `color-mix()` / cascade layers a cílí Chrome 111+ — na Edge i15 negraceful-degraduje. Vyžaduje build step, který `PmTracker.Web` nemá. Preflight by kolidoval s globálními styly v `site.css`. Zavedl by třetí sadu tokenů proti gov + `--pm-*`. Ověřeno, že ani Flowbite (400+ komponent), ani Preline (300+) tour komponentu nemají. |
| **`gov-wizard` / `gov-stepper`** | gov je shipuje, ale je to vizuální accordion/stepper bez orchestrace. `gov-wizard` doslova jen nastaví `size` dětem a vyrenderuje slot. Řeší jiný problém. |
| **Vlastní engine** | Pozicování bubliny u okraje viewportu, ve scrollovaném kontejneru a na zoomu 110 % je netriviální. Na Edge i15 se to už jednou vymstilo (`max-content`). Nepsat znovu. |

---

## 3. Ověřená fakta o prostředí

Tohle jsou zjištění z kódu, ne předpoklady. Implementace na nich stojí.

### 3.1 gov-design-system běží celý v light DOM — **žádný shadow DOM**

Stencil encapsulation flagy v `wwwroot/lib/gov-design-system/dist/core/core.esm.js`:

```
[256,"gov-backdrop"   → hasRenderFn
[260,"gov-button"     → hasRenderFn | hasSlotRelocation
```

Bit `1` (shadow DOM) nemá **ani jedna** z ~60 komponent.

**Důsledek:** `document.querySelector('gov-button')` funguje, spotlight na gov prvcích
funguje bez shadow-piercing hacků. Největší známý problém tour knihoven (driver.js
issue #180 — highlight nefunguje uvnitř shadowRoot) se nás netýká.

Flag `4` = `hasSlotRelocation` zároveň vysvětluje známé potíže s `gov-button` a `textContent`.

### 3.2 Statické soubory mají vynucený charset

`Program.cs:108-110`:
```csharp
staticContentTypeProvider.Mappings[".css"] = "text/css; charset=utf-8";
staticContentTypeProvider.Mappings[".js"]  = "text/javascript; charset=utf-8";
```

Nové vendorované soubory tímhle projdou automaticky. **Nepřidávat `.mjs` mapping bez
charsetu** — bez něj Edge i15 dekóduje podle CP1250 a rozsype diakritiku.
Pozor: `shepherd.mjs` má příponu `.mjs`, která v mapování **není** — buď ji doplnit
s charsetem, nebo soubor při vendorování přejmenovat na `.js`. **Doporučeno: přejmenovat
na `shepherd.js`**, ať se mapping nemusí rozšiřovat.

### 3.3 Žádný bundler v `PmTracker.Web`

Není `package.json` ani build step. Assety se vendorují jako hotové `dist/` do
`wwwroot/lib/`. Layout načítá ESM přímo:

```
_Layout.cshtml:183  <script type="module" src="~/lib/gov-design-system/dist/core/core.esm.min.js" asp-append-version="true">
_Layout.cshtml:188  <script type="module" src="~/js/site.js" asp-append-version="true">
```

### 3.4 ESM moduly se musí explicitně importovat v `bootstrap.js`

`site.js` je jednořádkový:
```js
import { bootstrapPmTrackerApp } from "./modules/bootstrap.js";
bootstrapPmTrackerApp();
```

Side-effect moduly musí být uvedené v `wwwroot/js/modules/bootstrap.js`. **Zapomenutý
import = tiché nefungování celé feature**, bez chyby v konzoli. Tahle chyba už v projektu
jednou nastala (viz komentář v hlavičce `bootstrap.js`).

`asp-append-version` navíc verzuje **jen entry `site.js`**, ne ESM sub-importy. Při vývoji
je v dev větvi `Program.cs` nastaven `no-cache` na statické soubory; v produkci se změna
JS projeví až po hard-refresh.

### 3.5 Cílové prostředí = Edge na Windows i15 přes Citrix

Verdikt o vzhledu i funkčnosti dává uživatel na i15, ne lokální Chromium. Nosnou layout
logiku stavět z letitých CSS základů. `:has()` a intrinsic keywords jen jako progressive
enhancement.

---

## 4. Specifikace UI

### 4.1 Spouštění — nová položka v profilovém menu

Profilové rozbalovací menu je v `Views/Shared/_Layout.cshtml`, panel `[data-user-menu-panel]`
začíná na ř. 95. Položky:

```
_Layout.cshtml:102   <a class="user-menu-link" ...>Můj profil</a>
_Layout.cshtml:103   <a class="user-menu-link" ... asp-fragment="moje-prava">Moje práva</a>
```

**Přidat za ř. 103 jako poslední položku:**

```html
<button class="user-menu-link user-menu-link-btn" type="button" data-tour-start>
    Průvodce stránkou
</button>
```

**Styling je z velké části hotový.** `site.css` už obsahuje připravený modifikátor,
který se zatím **nikde nepoužívá** — je to přesně tenhle případ:

```
site.css:401  .user-menu-link      { display:block; border-radius:3px; padding:8px 10px;
                                     font-size:14px; color:inherit }
site.css:409  .user-menu-link:hover{ background: var(--app-nav-hover) }
site.css:413  .user-menu-link-btn  { width:100%; border:none; background:transparent;
                                     text-align:left; cursor:pointer }
```

Použít **obě třídy zároveň**. Jediné, co v `.user-menu-link-btn` chybí, je
`font-family: inherit` — tlačítka nedědí rodinu písma po dokumentu, takže bez toho
bude položka vysázená systémovým fontem a bude z menu vyčnívat. Doplnit.

- Je to `<button>`, ne `<a>` — nenaviguje, spouští akci na aktuální stránce.
- Kliknutí: zavřít profilové menu, pak spustit průvodce.

**Rozhodnutí — stránka bez definované túry:** položka se renderuje **vždy**, ale je
`disabled` s `title="Pro tuto stránku není průvodce k dispozici"`. Zdůvodnění: položka,
která se objevuje a mizí, mate víc než položka trvale přítomná. Stav se nastavuje na
klientovi při inicializaci podle toho, zda je pro aktuální stránku registrovaná túra.

**Proč přes menu a ne automaticky při prvním přihlášení:** provoz jde přes Citrix a není
zaručeno, že se aplikace spustí pod windowsovým profilem uživatele. Automatické spouštění
„jen jednou pro nového uživatele" by proto nebylo spolehlivé. Ruční spuštění je vědomá
volba, ne kompromis.

### 4.2 Ovládání průvodce

| Akce | Mechanismus | Poznámka |
|---|---|---|
| Další / Zpět | tlačítka v bublině (`buttons` v `defaultStepOptions`) | popisky česky: „Zpět", „Další", na posledním kroku „Hotovo" |
| Ukončit křížkem | `cancelIcon: { enabled: true }` | Shepherd ho renderuje v bublině |
| Ukončit ESC | `exitOnEsc: true` | **defaultní chování Shepherdu**, nic se nedoprogramovává |
| Šipky ←/→ | `keyboardNavigation: true` | default, ponechat zapnuté |

Uživatel žádal křížek **nebo** ESC — Shepherd umí **obojí zároveň** a obojí zapneme.

**Klik do ztmaveného pozadí NESMÍ průvodce ukončit.** Shepherd to defaultně nedělá —
nepřidávat to. V projektu platí pravidlo, že modály se zavírají jen křížkem: drag-select
z inputu ven retargetuje `click` na společného předka a způsobí falešné zavření.

**Kolize ESC s profilovým menu:** `pageSwitchers.js:519-523` má document-level `keydown`
listener, který na ESC zavírá profilové menu. Průvodce se spouští až po zavření menu,
takže ke konfliktu nedojde — ale ověřit, že ESC během průvodce nezpůsobí obojí naráz.

### 4.3 Vzhled

Průvodce musí vypadat jako zbytek aplikace. `shepherd.css` má jen 3,5 kB záměrně
minimálního stylingu — přemapovat na gov tokeny v novém `wwwroot/css/components/tour.css`.

K dispozici jsou gov tokeny (`tokens.min.css`), mimo jiné:
`--background-block-primary`, `--text-primary`, `--text-secondary`, `--corner-radius-s`,
`--spacing-*`, `--font-size-body-*`, `--status-focus`, `--color-neutral-900`.

**Referenční hodnoty ze `gov-backdrop`** pro ztmavení, ať průvodce vypadá jako gov modály:
`background-color: var(--color-neutral-900)`, `opacity: 0.45`, `z-index: 100`.
Shepherd overlay musí být **nad** tímto z-indexem.

**Dark mode:** povinný. Před tvrzením „dark mode hotov" grepnout duplicitní selektory —
`site.css` často přebíjí `css/components/*` kvůli pořadí načítání (`_Layout.cshtml:19-24`,
`site.css` je poslední).

---

## 5. Architektura

### 5.1 Definice túr — v repu, při vývoji

Rozhodnuto: túry se **nepíšou do DB a needitují se z administrace**. Jsou to ES moduly
verzované společně s aplikací. Odpadá tím CRUD, výběr prvků na stránce, verzování túr
i další authz klíč „kdo smí editovat túry".

Navrhovaná struktura:

```
wwwroot/js/modules/tour/
  index.js          — veřejné API: registerTour(), startTourForCurrentPage(), hasTourForCurrentPage()
  engine.js         — obalení Shepherdu: konfigurace, tlačítka, i18n, výchozí showOn
  registry.js       — mapa pageId → definice túry
  tours/
    dashboard.js
    projekty.js
    projekt-detail.js
    ...
```

`engine.js` je jediné místo, které importuje Shepherd. Definice túr o Shepherdu nevědí —
díky tomu je engine vyměnitelný a definice přenositelné do dalších aplikací.

### 5.2 Identifikace stránky

Layout už rozlišuje stránky (`NavClass(...)`, `ViewData["BodyClass"]`). Doplnit na `<main>`
atribut `data-tour-page="<pageId>"` a podle něj vybírat túru z registry. Konkrétní způsob
naplnění (ViewData vs. controller) je na implementaci — jen ať je jednotný.

### 5.3 Reakce na role — klíčová část

**Server už roli vyfiltroval za nás.** Layout renderuje položky podmíněně:

```csharp
@if (nav?.CanViewPeople == true) { <a ...>Osoby</a> }
@if (nav?.CanViewSettings == true) { <a ...>Nastavení</a> }
```

Prvek, na který uživatel nemá právo, **v DOMu vůbec není**. Kontrola přítomnosti prvku
tedy není odhad oprávnění — je to čtení serverového rozhodnutí. To je přesné, ne přibližné.

**Výchozí chování enginu:** každý krok dostane automaticky `showOn`, které vrátí `true`
jen když cílový prvek existuje **a je viditelný** (`getClientRects().length > 0` — pozor,
`offsetParent` je nespolehlivý u `position: fixed`). Krok bez cíle se přeskočí.

Autor túry tedy nemusí o rolích vědět vůbec. Pro případy, kdy to nestačí, může `showOn`
explicitně přepsat vlastní funkcí.

**Vědomě odloženo (YAGNI):** klientský snapshot oprávnění (`AuthorizationSnapshot` →
`data-*` / JSON script tag) a deklarace `requiresPermission` na kroku. Zavést teprve
tehdy, až se ukáže, že kontrola přítomnosti v DOMu nestačí — např. u prvků skrytých
přes CSS místo neexistence, nebo u lazy-renderovaných částí. Do té doby je to zbytečná
vrstva. Toto je dokumentovaný rozšiřovací bod, ne opomenutí.

### 5.4 Placeholder krok

Uživatel chtěl možnost, aby v průvodci „nebylo prázdno". Podporováno: krok bez `attachTo`
se zobrazí uprostřed obrazovky. Vhodné tam, kde absence prvku vyvolává otázku
(„kde mám tlačítko Schválit?").

**Používat úsporně.** Krok „tohle tlačítko nevidíš, protože nemáš roli X" je pro nového
uživatele spíš matoucí — učí ho o něčem, co nikdy neuvidí. Většinu rolí obslouží to,
že se krok prostě přeskočí.

### 5.5 Formát definice túry

Cíl: definice nezávislá na Shepherdu, přenositelná do dalších aplikací.

```js
export default {
  id: "dashboard",
  steps: [
    {
      target: "[data-user-menu-toggle]",     // selektor, prvek, nebo funkce
      title: "Váš profil",
      text: "Tady najdete svá práva a nastavení.",
      placement: "bottom",                    // volitelné
      // showOn: () => ...                    // volitelné, jinak = cíl existuje a je vidět
    },
    {
      // bez target → bublina uprostřed (placeholder / úvod / závěr)
      title: "Vítejte",
      text: "Průvodce vás provede touto stránkou."
    }
  ]
};
```

`engine.js` tenhle formát přeloží na Shepherd `StepOptions`. Mapování `target` → `attachTo`,
`placement` → `attachTo.on`, doplnění tlačítek a `showOn`.

---

## 6. Soubory k zásahu

| Soubor | Zásah |
|---|---|
| `PmTracker.Web/wwwroot/lib/shepherd/` | **nový** — vendorovat `shepherd.js` (přejmenovaný z `.mjs`) + `shepherd.css` |
| `PmTracker.Web/wwwroot/js/modules/tour/` | **nový** — engine, registry, definice túr |
| `PmTracker.Web/wwwroot/js/modules/bootstrap.js` | přidat import tour modulu — **bez něj feature tiše nefunguje** |
| `PmTracker.Web/Views/Shared/_Layout.cshtml` | ~ř. 104: položka menu pod „Moje práva"; ~ř. 24: `<link>` na `tour.css`; `<main>`: `data-tour-page` |
| `PmTracker.Web/wwwroot/css/components/tour.css` | **nový** — přemapování Shepherd stylů na gov tokeny vč. dark mode |
| `PmTracker.Web/wwwroot/css/site.css` | ř. 413 `.user-menu-link-btn` — doplnit `font-family: inherit` (jinak systémový font) |

---

## 7. Pasti specifické pro tento projekt

1. **Zapomenutý import v `bootstrap.js`** → celá feature tiše nefunguje, bez chyby v konzoli.
2. **`.mjs` nemá charset mapping** v `Program.cs` → mojibake na Edge i15. Přejmenovat na `.js`.
3. **Klik do backdropu nesmí zavírat.** Shepherd to nedělá — nepřidávat.
4. **`instanceof HTMLButtonElement` nematchuje `gov-button`.** Pokud budou kroky cílit na
   gov tlačítka, používat existující `isButtonLike` / `setButtonDisabled` helpery.
5. **Nesahat na `.textContent` gov-button hostu** — rozbije Stencil slot relocation
   (flag `4` = `hasSlotRelocation`).
6. **Duplicitní CSS pravidla** — `site.css` se načítá poslední a přebíjí `css/components/*`.
7. **ESM cache-busting** — `asp-append-version` verzuje jen `site.js`, ne sub-importy.
8. **Vizuální verdikt dává uživatel na i15**, ne lokální Chromium ani Playwright.

---

## 8. Akceptační kritéria

1. Položka „Průvodce stránkou" je v profilovém menu jako **poslední, pod „Moje práva"**.
2. Kliknutí zavře profilové menu a spustí průvodce **na aktuální stránce** (bez navigace).
3. Průvodce ztmaví pozadí, vysvítí prvek a zobrazí bublinu s popisem.
4. Funguje „Další" a „Zpět"; na posledním kroku je „Hotovo".
5. Průvodce ukončí **křížek** i klávesa **ESC**.
6. Klik do ztmaveného pozadí průvodce **neukončí**.
7. Kroky cílící na prvek, který uživatel v roli nevidí, se **automaticky přeskočí**.
8. Krok bez cíle se zobrazí **uprostřed obrazovky**.
9. Na stránce bez túry je položka menu **disabled** s vysvětlujícím `title`.
10. Vzhled odpovídá gov-design-systemu ve **světlém i tmavém** módu.
11. Ověřeno uživatelem na **Edge / i15 / Citrix**, ne jen lokálně.

---

## 9. Otevřené otázky pro implementaci

1. **Které stránky dostanou túru jako první?** Doporučení: začít jednou (Přehled nebo
   Projekty), doladit engine a vzhled, teprve pak rozšiřovat. Nedělat deset túr najednou.
2. **Naplnění `data-tour-page`** — přes `ViewData` jako `BodyClass`, nebo z controlleru?
   Sjednotit s existujícím vzorem.
3. **Testy** — projekt má Unit / Integration / Api / E2E vrstvy. Pozor: gov komponenty
   jsou v Playwrightu „not visible" (host má nulovou výšku); klikat přes
   `DispatchEventAsync("click")`, viditelnost ověřovat přes `ToHaveCount` / `hasAttribute`,
   ne `ToBeVisible`.

---

## 10. Zdroje

- [Shepherd.js — StepOptions](https://docs.shepherdjs.dev/api/step/interfaces/stepoptions/)
- [Shepherd.js — TourOptions](https://docs.shepherdjs.dev/api/tour/interfaces/touroptions/)
- [Shepherd.js — Usage](https://docs.shepherdjs.dev/guides/usage/)
- [driver.js — Configuration](https://driverjs.com/docs/configuration)
- [driver.js #489 — Skip step for missing element](https://github.com/kamranahmedse/driver.js/issues/489)
- [Intro.js — licence](https://introjs.com/docs/getting-started/license)
- [Tailwind CSS v4 — browser support](https://tailwindcss.com/blog/tailwindcss-v4)

**Pozor při implementaci:** dokumentace OSS jádra je na `docs.shepherdjs.dev`. Existuje
i komerční **Shepherd Pro** s dokumentací na `docs.shepherdpro.com` a skoro totožnou
strukturou. Všechny funkce použité v tomto zadání (`showOn`, `text` jako funkce,
`beforeShowPromise`, `attachTo` s funkcí, `cancelIcon`, `exitOnEsc`, `keyboardNavigation`)
jsou v **MIT jádře**.
