# Design — odstranění modalu pro úpravu/tvorbu projektového záznamu

> Date: 2026-04-25
> Status: Approved (user 2026-04-25)
> Sequel skill: writing-plans

## Motivace

Modal pro úpravu a tvorbu projektového záznamu způsobuje kolize s **chat modalem vyjádření** v rámci editace externí vazby. Modal-in-modal v gov-design-system 4.x (gov-dialog) působí UX problémy (focus trap, z-index, scroll, escape klávesa). Místo řešení modal-in-modal celou sekundární modal cestu odstraňujeme — všechny editace záznamů a návrhů budou výhradně přes plnou stránku.

## Cílový stav

- Klik na jakékoli tlačítko „Upravit záznam" / „Nový záznam" / „Detail návrhu" / „Předvyplnit z návrhu" otevře **přímo plnou stránku** `/Zaznamy/Edit/{id}` resp. `/Zaznamy/Create?...` resp. `/Navrhy/...`
- Žádný chooser dialog („Otevřít v modalu vs. Otevřít na stránce")
- Žádná localStorage preference
- Žádný `presentation=modal` query parametr
- Žádné modal view (`EditZaznamModal.cshtml`)
- Žádné modal-specific JS moduly (`recordEditor/`)
- Sdílený `_ModalLayout.cshtml` ZŮSTÁVÁ (používají ho jiné modaly: ProjectModal, AssignMeetingIdentifierModal, NewMeetingModal, chat vyjádření, …)

## Rozsah změn

### Backend — controllers

**[ZaznamyController.cs](../../../PmTracker.Web/Controllers/ZaznamyController.cs):**
- `Edit(int id, string? presentation, string? returnUrl)` → odstranit `presentation` parametr, vždy vrátit page view
- `Create(int projektId, int? jednaniId, string? uiContext, string? presentation, string? returnUrl)` → odstranit `presentation` parametr
- Smazat: `PresentationModal`, `PresentationPage` konstanty, `GetEditorViewPath()`, `NormalizeRecordEditorPresentation()`, `IsAjaxRequest()` helper (pokud nikde jinde nepoužitý)
- View path = vždy `~/Views/Projekty/EditZaznam.cshtml` (page view)

**[NavrhyController.cs](../../../PmTracker.Web/Controllers/NavrhyController.cs):**
- Odstranit `presentation` ze 4 actions: `CreateRecordProposal`, `CreateScheduleProposal`, `ProposalDetail`, `PrefillCreateProposal`
- Smazat `PresentationModal`, `GetEditorViewPath()`, `NormalizeRecordEditorPresentation()`
- Odstranit `presentation = PresentationPage` z `prefillUrl` URL builderu

### Backend — views

**Smazat:**
- `PmTracker.Web/Views/Projekty/EditZaznamModal.cshtml` (modal-only view)
- Případné navazující partials, které jsou jen pro modal (TBD při implementaci — `EditZaznam.cshtml` page view by měl obsahovat ty samé content partials)

**Zachovat:**
- `_ModalLayout.cshtml` (sdílený s ostatními modaly)
- Všechny `_EditZaznam*Panel.cshtml` content partials (používá je page view)

### Backend — modely

**[Models/ViewModels/Zaznamy/RecordEditorViewModel.cs] (nebo equivalent):**
- Odstranit property `Presentation` (pokud existuje)
- Odstranit `IsModal` / `IsPage` helpers (pokud existují)

### Frontend — Razor triggery (≥10 míst)

Všechny `data-record-editor-url="..."` triggery nahradit za **`pm-button` s `href` atributem** (gov-button reflektuje href na native navigaci, zachovává Ctrl+klik pro nový tab). Stejné variantami a labely.

| Soubor | Tlačítka |
|---|---|
| [_ProjectScheduleTab.cshtml](../../../PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml) | „Upravit záznam z harmonogramu" (1×) |
| [_ProjectProposalsTab.cshtml](../../../PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml) | „Nový návrh záznamu", „Detail návrhu", „Předvyplnit z návrhu" (4×) |
| [_ZaznamPartial.cshtml](../../../PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml) | „Upravit záznam", „Nový návrh úpravy harmonogramu" (2×) |
| [_ProjectRecordsTab.cshtml](../../../PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml) | „Nový záznam", „Nový návrh záznamu" (2×) |
| [Jednani/Detail.cshtml](../../../PmTracker.Web/Views/Jednani/Detail.cshtml) | „Vytvořit záznam k jednání" (1×) |

**Navíc** odstranit `data-record-editor-project-id`, `data-record-editor-label` atributy (pro chooser je nepotřebujeme).

### Frontend — Profil

**[Profil/Index.cshtml](../../../PmTracker.Web/Views/Profil/Index.cshtml) řádky 51-58:**

Smazat celou sekci „Výchozí otevření editoru záznamu":
```razor
<section class="card profile-preferences-card">
    <h2>Výchozí otevření editoru záznamu</h2>
    ...
</section>
```

**ProfilController:**
- Odstranit cokoliv co podporovalo tuto sekci (pravděpodobně nic — UI je čistě klient-side localStorage)

### Frontend — JS moduly

**Smazat celý adresář `PmTracker.Web/wwwroot/js/modules/recordEditor/`:**
- `index.js`
- `navigation.js` (chooser, openRecordEditor, preference, return state)
- `draft.js` (close-guard, modal close request)
- `form.js` (TBD — pokud používá page view, přesunout content do page-specific modulu)
- `richtext.js` (TBD — pokud sdílen s page view)

**ALE pozor:** `form.js` a `richtext.js` mohou obsahovat logiku používanou i page editorem. Při implementaci ověřit. Pokud ano, přejmenovat / přesunout do `wwwroot/js/modules/zaznam-editor/` nebo podobně.

**[bootstrap.js](../../../PmTracker.Web/wwwroot/js/modules/bootstrap.js):**

Odstranit imports + reference na:
- `clearStoredRecordEditorPreference`
- `closeRecordEditorChooser`
- `closeRecordEditorCloseGuard`
- `isRecordEditorFormDirty`
- `openRecordEditor`
- `prepareRecordEditorFormNavigation`
- `promptRecordEditorDiscard`
- `recordEditorState`
- `refreshRecordEditorPreferenceUi`
- `requestRecordEditorModalClose`
- `requestRecordEditorPageCancel`
- `restoreRecordEditorReturnStateFromUrl`
- `updateTaskTypeVisibility` (TBD — možná zachovat pro page editor)

Zachovat ty, které jsou nutné pro page edit flow.

### Tests — smazat / upravit

**Smazat:**
- `PmTracker.Tests.Unit/Architecture/RecordEditorJsSplitTests.cs` (test JS split do recordEditor/ adresáře)
- Veškeré testy pro `getRecordEditorPreferenceLabel`, `setStoredRecordEditorPreference` apod.

**Aktualizovat:**
- `PmTracker.Tests.Unit/Modals/ModalLayoutMarkupTests.cs` — pokud má testy specifické pro EditZaznamModal, smazat
- E2E/API testy které volaly `/Zaznamy/Edit?presentation=modal` — buď smazat (pokud kompletně modální scénář) nebo přepnout na page

**Vyhledat:**
```
grep -rn "presentation=modal\|presentation=page\|RecordEditorPreference\|recordEditorChooser" PmTracker.Tests.*/
```

## Migrace existujících uživatelů

- localStorage klíč `pmtracker.recordEditor.preference` zůstává v prohlížečích — žádný cleanup. Po refaktoru je ignorován (žádný kód ho nečte). Po měsících se vytratí.

## Verifikace po implementaci

1. `dotnet build PmTracker.sln` — 0 warnings, 0 errors
2. `dotnet test PmTracker.sln` — všechny zelené (po update testů)
3. `bun build PmTracker.Web/wwwroot/js/site.js` — bundle OK
4. **Manuální smoke test:**
   - a) Klik „Nový záznam" v projektu → naviguje na `/Zaznamy/Create?projektId=...` (plná stránka, žádný modal)
   - b) Klik „Upravit záznam" na kartě → `/Zaznamy/Edit/{id}` (plná stránka)
   - c) Klik „Detail návrhu" / „Předvyplnit z návrhu" → `/Navrhy/...` (plná stránka)
   - d) Po uložení page editoru → zpět na předchozí URL (returnUrl flow zachován)
   - e) Profil/Index — sekce „Výchozí otevření editoru záznamu" pryč
   - f) Žádný JS error v console při kliku na editovat tlačítka
   - g) **Externí vazba** v editaci záznamu — chat vyjádření modal funguje bez kolizí (root cause vyřešen)

## Pre-implementation discovery (krok 0 v writing-plans)

První krok implementačního plánu MUSÍ být discovery sweep — ověření těchto bodů s rozhodovacím kritériem:

1. **`form.js` a `richtext.js`** v `wwwroot/js/modules/recordEditor/`
   - **Discovery:** `grep -rn "from.*recordEditor/(form|richtext)" PmTracker.Web/wwwroot/js/`
   - **Pravidlo:** Pokud importuje SAMO modální cesta → smazat. Pokud importuje page-edit kód → přesunout do `wwwroot/js/modules/zaznam-editor/` a aktualizovat importy.

2. **`updateTaskTypeVisibility`** export v bootstrap.js
   - **Discovery:** `grep -rn "updateTaskTypeVisibility" PmTracker.Web/wwwroot/js/`
   - **Pravidlo:** Pokud používá page editor → zachovat (přesunout import zdroje pokud byl z `recordEditor/`). Pokud jen modal → smazat.

3. **Modal-specific obsahové partials** v `Views/Projekty/`
   - **Discovery:** `grep -rl "@model.*EditZaznamModal\|IsModal\|Presentation.*Modal" Views/`
   - **Pravidlo:** Pokud partial používá pouze `EditZaznamModal.cshtml` → smazat. Pokud sdílen s `EditZaznam.cshtml` (page view) → zachovat.

4. **API/E2E testy se závislostí na `presentation=modal`**
   - **Discovery:** `grep -rn "presentation=modal\|PresentationModal" PmTracker.Tests.*/`
   - **Pravidlo:** Smazat test (pokud ověřoval pouze modální flow) nebo přepsat na page flow.

## Rollout

Refaktor je BIG bang — žádný feature flag, žádná postupná migrace. Po commitu se modal flow přestane existovat.

Důvody:
- Není production-critical (interní nástroj, malý počet uživatelů)
- Modal flow je rozbitý (modal-in-modal), tj. nemá smysl ho zachovávat dočasně
- Postupný rollout by zdvojnásobil práci (dva flow side-by-side)

## Sequel

Po schválení tohoto designu → invoke `superpowers:writing-plans` skill → detailní implementation plan s číslovanými kroky a checkpointy.
