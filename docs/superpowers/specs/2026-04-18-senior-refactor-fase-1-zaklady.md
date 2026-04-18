# Senior refactor — Fáze 1: Architektonické základy

**Datum:** 2026-04-18
**Větev:** `codex/senior-refactor-fase-1`
**Autor:** Ing. Pavel Andrlík + Claude

## Cíl

Postavit v PM Tracker aplikaci solidní architektonickou kostru podle standardů zralého .NET + Web Components týmu (úroveň "senior Silicon Valley"), tak aby:

- Následující fáze (tlačítka, responzivita, de-god-file) měly kam sednout bez improvizace
- Libovolná firma znalá gov design systému (https://designsystem.gov.cz) převzala projekt a rozuměla mu bez "heritage knowledge"
- Změna major verze gov DS (dnes 4.2.9 → zítra 5.0) byla úprava v ~5 souborech, ne ve 113 views
- Aplikace vizuálně zůstala **beze změn** (vrstva se jen přestěhuje, UI zůstává)

## Ne-cíl

- Žádný přepis views (to je fáze 2)
- Žádná responzivita (fáze 2)
- Žádné rozbíjení god-files v JS/backendu (fáze 3)
- Žádné gov layout patterny (`<gov-layout>`, `<gov-grid>`) — pro dashboard/project-management aplikaci kontraproduktivní

## Rozhodnutí (z brainstormingu)

| Téma | Rozhodnutí |
|---|---|
| Postup | Varianta α — fázovaně, první fáze jsou základy |
| Scope gov DS | Vrstva B — tokeny + komponenty (ne layout) |
| Tlačítka/UI | Varianta Y — Razor TagHelper wrapper (`pm-*` komponenty) |
| Breakpointy | Vlastní, ale číselně shodné s gov DS (576/768/992/1200/1400) |
| Branch workflow | Nová větev `codex/senior-refactor-fase-1`, ověření, pak merge |
| Integrační jobs | **Hangfire** (enterprise standard, dashboard, retry, DB persist) |
| Config runtime | DB-backed přes Hangfire + UI `/Nastaveni/Integrace` |
| Druhá DB | Read-only `TicketingReadOnlyDbContext` přes servisní účet |
| Domain typy ticketů | NES (nesrovnalost), PMP (požadavek metodické podpory), PNF (požadavek nové funkcionality) |

## Domain kontext — rozšíření z 2. kola brainstormingu

Fáze 1 zahrnuje **písemné specifikace** pro business domény, které budou implementovány v pozdějších fázích:

- `docs/specs/ticketing-integration.md` — čtení z ticketing DB (fáze 3)
- `docs/specs/automat-vytezovani-vyjadreni.md` — automat + manuální tagy (fáze 3)
- `docs/specs/harmonogram-plan-vs-skutecnost.md` — plán vs. skutečnost (fáze 4)
- `docs/specs/dashboard-prodleni.md` — NES/PMP/PNF prodlení (fáze 5)

Každý dokument obsahuje sekci **„Otevřené otázky"** — věci čekající na rozhodnutí vedení (finální fráze, redukce kroků harmonogramu, definice prodlení). Dokumenty jsou **živé** — doplňují se, jak business specifikuje.

## Architektura

### 1. Design tokens (CSS proměnné)

**Soubor:** `PmTracker.Web/wwwroot/css/tokens.css` (nový)

Tenká vrstva nad `tokens.min.css` z gov DS. Gov tokeny jsou už načteny v `_Layout.cshtml` (`<link href="~/lib/gov-design-system/styles/lib/tokens.min.css">`), takže v `tokens.css` na ně rovnou odkazujeme. Obsahuje:

- **Aplikační tokens (`--pm-*`)** — aliasy gov tokenů se semantickým jménem:
  - `--pm-color-primary: var(--gov-color-primary-600);`
  - `--pm-spacing-s/m/l/xl/2xl/3xl: var(--gov-spacing-*);`
  - `--pm-radius-s/m/l: var(--gov-radius-*);`
  - `--pm-font-family-body/display: var(--gov-font-family-sans);`
- **Breakpointy jako CSS custom media (pro budoucí PostCSS):**
  - `--pm-bp-sm: 576px; --pm-bp-md: 768px; --pm-bp-lg: 992px; --pm-bp-xl: 1200px; --pm-bp-2xl: 1400px;`
- **Z-index stack:** `--pm-z-header: 100; --pm-z-dropdown: 200; --pm-z-modal: 1000;` — zamezí ad-hoc z-indexům rozmístěným v souborech

**Pravidlo:** všechny barvy/spacing/typo v `site.css` musí postupně přecházet na `var(--pm-*)`. Fáze 1 založí, fáze 2+ konvertuje.

### 2. Razor TagHelper vrstva (`PmComponents`)

**Umístění:** `PmTracker.Web/TagHelpers/` — ne nový assembly projekt. `TagHelper`y potřebují stejný `wwwroot` prostor a stejný lifetime scope, takže jsou součástí `PmTracker.Web`. Registrace přes `@addTagHelper *, PmTracker.Web` v `Views/_ViewImports.cshtml` (už tam takový import pravděpodobně je — ověříme).

**Komponenty (fáze 1 implementuje kostry, fáze 2 je konverzně nasadí):**

| TagHelper | Tag v Razor | Renderuje |
|---|---|---|
| `PmButtonTagHelper` | `<pm-button>` | `<gov-button>` |
| `PmAlertTagHelper` | `<pm-alert>` | `<gov-message>` (už částečně) |
| `PmBadgeTagHelper` | `<pm-badge>` | `<gov-tag>` (už částečně) |
| `PmFieldTagHelper` | `<pm-field>` | `<gov-form-control>`+`<gov-form-label>`+`<gov-form-input>`+`<gov-form-message>` |
| `PmIconTagHelper` | `<pm-icon>` | `<gov-icon>` |

**API contract pro `PmButtonTagHelper`** (reprezentativní):

```csharp
[HtmlTargetElement("pm-button")]
public sealed class PmButtonTagHelper : TagHelper
{
    public PmButtonVariant Variant { get; set; } = PmButtonVariant.Secondary;
    public PmButtonSize Size { get; set; } = PmButtonSize.Medium;
    public string? Icon { get; set; }
    public string? IconPosition { get; set; } = "start"; // start|end
    public string? Type { get; set; } = "button"; // button|submit|reset
    public bool Disabled { get; set; }
    public string? Href { get; set; } // pokud je, renderuje <gov-button href="..."> (odkaz)

    public override void Process(TagHelperContext ctx, TagHelperOutput out) { ... }
}

public enum PmButtonVariant { Primary, Secondary, Destructive, Ghost }
public enum PmButtonSize { Small, Medium, Large }
```

**Mapování variant → gov atributy** (interně v TagHelperu, dokumentováno v `docs/architecture/buttons.md`):

| `pm-button variant` | gov-button `color` | gov-button `type` |
|---|---|---|
| Primary | `primary` | `solid` |
| Secondary | `primary` | `outlined` |
| Destructive | `error` | `solid` |
| Ghost | `neutral` | `base` |

Pokud se gov DS změní, upravíme jednu tabulku v jednom souboru.

**Unit testy** v `PmTracker.Tests.Unit/TagHelpers/`:
- `PmButtonTagHelperTests` — pro každou variantu ověřit vygenerovaný HTML
- Snapshot testy pro stabilitu výstupu

### 3. JS event delegation vrstva

**Soubor:** `PmTracker.Web/wwwroot/js/modules/eventBus.js` (nový) + synchronizace do `site.bundle.js`

Problem dneska: 113 `<button>` má data-atributy (`data-modal-url`, `data-filter-key`, `data-confirm`) a JS v různých modulech (`modals.js`, `filters.js`, `pageSwitchers.js`) nasazuje vlastní `document.addEventListener("click", ...)`. Po přechodu na `<gov-button>` (které emituje `gov-click`, ne nativní `click`) se tyto handlery rozbijí.

**Řešení — centrální event delegation:**

```js
// eventBus.js
export const appEventBus = {
    on(selector, event, handler) {
        document.addEventListener(event, (e) => {
            const target = e.target.closest(selector);
            if (target) handler(e, target);
        });
    }
};

// Adaptér: gov-click a nativní click oba hlásí jako "click"
document.addEventListener("gov-click", (e) => {
    // propagace na nativní click, pokud target ještě click nemá
    const native = new MouseEvent("click", { bubbles: true, cancelable: true });
    e.detail?.originalEvent?.preventDefault?.();
    e.target.dispatchEvent(native);
});
```

**Výsledek:** existující JS posluchače nativního `click` zůstávají beze změny; `pm-button` → `gov-button` → `gov-click` se transparentně převede.

**Unit test:** `EventBusTests.js` (nebo přes Playwright jsm. fixtures).

### 4. Struktura JS modulů (bez god-files)

Stanovíme **limit 300 řádků per modul**. Fáze 1 tento limit jen dokumentuje a vytvoří ESLint config, který ho kontroluje (warning). Rozbití existujících god-files je fáze 3.

**Soubor:** `PmTracker.Web/wwwroot/.eslintrc.json`

```json
{
  "rules": {
    "max-lines": ["warn", { "max": 300, "skipBlankLines": true, "skipComments": true }]
  }
}
```

**Pravidlo dokumentované v `docs/architecture/js-modules.md`**:

- Jeden modul = jedna zodpovědnost
- Testovatelný bez DOMu tam, kde to dává smysl (pure helpery v `utils.js`, `snippets.js`)
- Side-effectful moduly (DOM/AJAX) mají jasné export rozhraní a jsou inicializované z `bootstrap.js`

### 5. Backend — thin controller pravidlo

**Dokument:** `docs/architecture/backend-layering.md`

- Controller ≤ 200 řádků; jen routing + validation + view model mapping
- Service layer (existuje, rozšíříme) — business logika
- Repository layer — EF Core queries (už existují `ProjectyRepository` atd.)
- **Pravidlo Fáze 1:** žádný nový kód do existujících 500+ řádků controllerů. Fáze 3 je rozbije.

Fáze 1 pouze zavede skript `scripts/check-file-sizes.ps1` (nebo `.sh`), který v CI (a volitelně v `dotnet build` pre-step) vypíše warning pro soubory nad 500 řádků v `PmTracker.Web/`, `PmTracker.Data/`. Warning, ne error — existující god-files zůstávají platné, nové kódy se píší menší. Fáze 3 god-files rozbije.

### 6. Living style guide

**Stránka:** `/StyleGuide` (`PmTracker.Web/Controllers/StyleGuideController.cs` + view)

Fáze 1 založí kostru — prázdnou stránku s placeholder sekcemi. Fáze 2 ji naplní příklady použití `pm-button`, `pm-alert`, `pm-badge`, `pm-field`.

**Cíl:** každý dev (interní i externí) po otevření `/StyleGuide` vidí **funkční** ukázky všech dostupných `pm-*` komponent se zdrojovým kódem. Žádné "musíš se poptat Pavla".

### 7. Dokumentace

Nové soubory v `docs/architecture/`:

- `README.md` — index architektonické dokumentace
- `buttons.md` — `pm-button` API, variant mapping na gov, příklady
- `alerts.md` — `pm-alert` API
- `badges.md` — `pm-badge` API
- `fields.md` — `pm-field` API
- `tokens.md` — CSS tokens, breakpointy
- `js-modules.md` — pravidlo modulů, event bus
- `backend-layering.md` — thin controller, services, repositories
- `upgrade-gov-ds.md` — postup upgrade gov DS (dnes 4.2.9 → 5.0)

Updatuje se existující `docs/specs/gov-design-system-integration.md` s odkazem na `docs/architecture/`.

## Testovací strategie

- **Unit**: každý `PmXxxTagHelper` má ≥3 testy (happy-path, disabled, icon/link varianta)
- **Snapshot**: výstupní HTML gov tagů má snapshot test, aby regrese gov verze byla okamžitě viditelná
- **Integration**: 1 E2E test — `/StyleGuide` stránka se načte 200 a obsahuje `<gov-button` v renderu
- **Offline test** (již existuje `OfflineAssetsTests`) — zkontrolovat, že fáze 1 nezavedla externí URL

## Plán implementace (pro writing-plans skill)

Hrubě 8–10 kroků:

1. `tokens.css` + začlenění v `_Layout.cshtml`
2. `PmButtonTagHelper` + unit testy (+ enum `PmButtonVariant`, `PmButtonSize`)
3. `PmAlertTagHelper` + unit testy
4. `PmBadgeTagHelper` + unit testy
5. `PmFieldTagHelper` + unit testy (nejkomplexnější — label/input/error)
6. `PmIconTagHelper` + unit testy
7. `eventBus.js` + `gov-click` adaptér + synchronizace do `site.bundle.js`
8. `.eslintrc.json` limit řádků, `Directory.Build.props` warning pro .cs
9. `StyleGuideController` + view (kostra, bez obsahu)
10. `docs/architecture/` — všech 8 dokumentů
11. E2E test `/StyleGuide` + update `GovComponentsReplacementTests`

Každý krok je samostatný commit na `codex/senior-refactor-fase-1`.

## Akceptační kritéria fáze 1

- [ ] `tokens.css` existuje, import v `_Layout.cshtml`, nerozbil stávající vizuál
- [ ] `PmButtonTagHelper` renderuje 4 varianty, ≥12 unit testů prochází
- [ ] `PmAlertTagHelper`, `PmBadgeTagHelper`, `PmFieldTagHelper`, `PmIconTagHelper` hotové, testy procházejí
- [ ] `eventBus.js` adaptér funguje — manuální test: nativní click i gov-click vede na stejný handler
- [ ] `/StyleGuide` načte 200, zobrazí placeholder sekce (obsah dodá fáze 2)
- [ ] Všech 8 dokumentů v `docs/architecture/` existuje
- [ ] `dotnet build` 0 chyb, 0 warningů
- [ ] `dotnet test` ≥ 267/267 (existující) + nové testy 100%
- [ ] `OfflineAssetsTests` prochází (žádné nové CDN)
- [ ] Vizuál aplikace **beze změn** — stávající views nepoužívají `pm-*` tagy (fáze 2)
- [ ] Screenshot `/` před a po je pixel-identický (Playwright visual diff, pokud čas)

## Rizika

1. **Event bus adaptér** může v edge case (Escape handling, preventDefault uvnitř gov komponenty) propagovat dvakrát. Mitigace: jednotkový test pro každou cestu + zaznamenaný guard (např. `WeakSet` zpracovaných eventů).

2. **TagHelper discovery** v ASP.NET Core vyžaduje `@addTagHelper *, PmTracker.Web` v `_ViewImports.cshtml`. Musíme ověřit, že všechny existující Razor views používají správný import (obvykle ano, `_ViewImports.cshtml` je globální).

3. **Fáze 1 nemá vidět** — uživatel chce vidět progress. Mitigace: průběžný report po každém kroku + `/StyleGuide` stránka, kde i bez fáze 2 zobrazíme 4 demonstrativní tlačítka pro ověření, že TagHelper funguje.

## Merge kritéria

Po dokončení fáze 1:
1. Uživatel ověří `/StyleGuide` v prohlížeči
2. Uživatel ověří, že aplikace stále funguje jako dřív (stávající views se nezměnily)
3. Schválí merge → zmergujeme do `codex/refactor_sprint_0` (hlavní pracovní větev)
4. Spustíme brainstorming pro fázi 2 (tlačítka + responzivita)
