# Implementation Plan — odstranění modalu pro úpravu/tvorbu projektového záznamu

> **For execution:** Inline execution v jednom session (user explicitně nechce agenty). Tasky postupně, po každém commit, na konci verifikace.

**Goal:** Odstranit modal flow pro úpravu/tvorbu projektových záznamů a návrhů. Po refaktoru jediná cesta = page edit `/Zaznamy/Edit/{id}` resp. `/Zaznamy/Create?...`.

**Architecture:** Big bang refactor. Backend přestane akceptovat `presentation` parametr. Razor triggery (`data-record-editor-url`) → `pm-button` s `href`. JS chooser modul + preference UI smazány. Sdílený `_ModalLayout.cshtml` zachován (jiné modaly).

**Tech Stack:** ASP.NET Core 8 MVC, Razor Views, ESM JavaScript, gov-design-system 4.x, xUnit + FluentAssertions.

**Spec:** [docs/superpowers/specs/2026-04-25-remove-record-editor-modal-design.md](../specs/2026-04-25-remove-record-editor-modal-design.md)

---

## Discovery (proveden 2026-04-25 před plánem)

- ✅ `EditZaznamPage.cshtml` existuje — page view funguje
- ✅ `EditZaznamModal.cshtml` existuje — ke smazání
- ✅ `updateTaskTypeVisibility` v `recordEditor/form.js:97` — POUŽÍVÁ page editor (bootstrap.js:611) → MUSÍ se zachovat
- ✅ `recordEditor.js` (root) je barrel re-exporter `export * from "./recordEditor/index.js"`
- ✅ `RecordEditorControllerTests.cs` — má `presentation=modal` reference
- ✅ Žádný `EditZaznamPage` v `_ModalLayout` — sdílený layout

---

## Task 1: Přesun `updateTaskTypeVisibility` mimo recordEditor/

`recordEditor/form.js` obsahuje JEN modal-flow logiku KROMĚ `updateTaskTypeVisibility`, který používá page editor. Vytvořím nový samostatný modul `taskTypeVisibility.js`.

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/taskTypeVisibility.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (změna importu)

- [ ] **Step 1.1: Najít stávající implementaci** — `Read recordEditor/form.js:90-130` pro extrakci `updateTaskTypeVisibility` + jejích závislostí
- [ ] **Step 1.2: Vytvořit `taskTypeVisibility.js`** s extrahovanou funkcí (zachovat 1:1 kód)
- [ ] **Step 1.3: Změnit import v bootstrap.js** z `./recordEditor.js` (nebo `./recordEditor/index.js`) na `./taskTypeVisibility.js`
- [ ] **Step 1.4: bun build verify** — `bun build PmTracker.Web/wwwroot/js/site.js --target browser --outfile /tmp/check.js` — žádné import errors
- [ ] **Step 1.5: Commit** — `feat(zaznam): move updateTaskTypeVisibility out of recordEditor/ before modal removal`

## Task 2: Backend — ZaznamyController odstranit `presentation`

**Files:**
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs:18-19,51,86,91,105,111,171-200` (odstranit konstanty, helpers, parametr)

- [ ] **Step 2.1: Odstranit konstanty**

```csharp
// SMAZAT řádky 18-19:
private const string PresentationModal = "modal";
private const string PresentationPage = "page";
```

- [ ] **Step 2.2: `Edit` action — odstranit `presentation` parametr**

```csharp
// PŘED:
public async Task<IActionResult> Edit(int id, string? presentation, string? returnUrl, CancellationToken ct = default)
// PO:
public async Task<IActionResult> Edit(int id, string? returnUrl, CancellationToken ct = default)
```
A v těle:
```csharp
// PŘED:
PrepareRecordEditorModel(model, presentation, returnUrl, canEditRecord, canManageSchedule);
return View(GetEditorViewPath(model.Presentation), model);
// PO:
PrepareRecordEditorModel(model, returnUrl, canEditRecord, canManageSchedule);
return View("~/Views/Projekty/EditZaznamPage.cshtml", model);
```

- [ ] **Step 2.3: `Create` action — odstranit `presentation` parametr** (analogicky jako Edit)
- [ ] **Step 2.4: `PrepareRecordEditorModel` — odstranit `presentation` parametr a propsání do modelu**
- [ ] **Step 2.5: Smazat `GetEditorViewPath`, `ResolvePresentation`, `NormalizePresentation`, `IsAjaxRequest` (pokud nepoužitý jinde)**
- [ ] **Step 2.6: Smazat `Presentation` property z RecordEditorViewModel** (nebo jinde kde existuje) — udělat to v Task 5

## Task 3: Backend — NavrhyController odstranit `presentation`

**Files:**
- Modify: `PmTracker.Web/Controllers/NavrhyController.cs:13,32,46,59,86,168-169,255-282`

- [ ] **Step 3.1: Smazat `PresentationModal` konstantu (řádek 13)**
- [ ] **Step 3.2: 4 actions — odstranit `presentation` parametr:** `CreateRecordProposal`, `CreateScheduleProposal`, `ProposalDetail`, `PrefillCreateProposal`
- [ ] **Step 3.3: `PrepareProposalEditorModel` — odstranit `presentation` parametr**
- [ ] **Step 3.4: Smazat `GetEditorViewPath`, `NormalizeRecordEditorPresentation`, `ResolvePresentation`** (whatever exists)
- [ ] **Step 3.5: V `prefillUrl` URL builderu (řádek 168) odstranit `presentation = PresentationPage`** z anonymního objektu

## Task 4: Backend — Smazat `EditZaznamModal.cshtml`

**Files:**
- Delete: `PmTracker.Web/Views/Projekty/EditZaznamModal.cshtml`

- [ ] **Step 4.1: `git rm PmTracker.Web/Views/Projekty/EditZaznamModal.cshtml`**

## Task 5: Modely — odstranit `Presentation` property

**Files:**
- Modify: ViewModel souboru (najít přes `grep -rn "Presentation" Models/`)

- [ ] **Step 5.1: Najít ViewModel se `Presentation` property** — `grep -rn "public string Presentation" PmTracker.Web/Models`
- [ ] **Step 5.2: Odstranit `Presentation` property + případné `IsModal` / `IsPage` helpers**
- [ ] **Step 5.3: Najít všechny reference v Razor views** — `grep -rn "Model.Presentation\|Model.IsModal\|Model.IsPage" PmTracker.Web/Views/` a smazat / upravit

## Task 6: Razor triggery — `data-record-editor-url` → `pm-button` s `href`

5 souborů, ~10 triggerů. Pattern conversion:

```razor
<!-- PŘED -->
<pm-button variant="Primary"
           data-record-editor-url="@Model.CreateRecordEditorUrl"
           data-record-editor-project-id="@Model.ProjektId"
           data-record-editor-label="Nový záznam">
    Nový záznam
</pm-button>

<!-- PO -->
<pm-button variant="Primary" href="@Model.CreateRecordEditorUrl">
    Nový záznam
</pm-button>
```

- [ ] **Step 6.1: `_ProjectRecordsTab.cshtml`** (2 triggery: Nový záznam, Nový návrh záznamu)
- [ ] **Step 6.2: `_ZaznamPartial.cshtml`** (2 triggery: Upravit záznam, Návrh úpravy harmonogramu)
- [ ] **Step 6.3: `_ProjectScheduleTab.cshtml`** (1 trigger: ScheduleEditUrl)
- [ ] **Step 6.4: `_ProjectProposalsTab.cshtml`** (4 triggery: Nový návrh, Detail, PrefillCreate, Detail)
- [ ] **Step 6.5: `Jednani/Detail.cshtml`** (1 trigger: createRecordInMeetingUrl)
- [ ] **Step 6.6: Ověřit `pm-button` s `href`** funguje (gov-button reflektuje na native nav) — projít konkrétní pm-button.razor / pm-button TagHelper
- [ ] **Step 6.7: Commit** — `refactor(zaznam): replace data-record-editor-url triggers with pm-button href (modal removal)`

## Task 7: Profil UI — odstranit sekci preference

**Files:**
- Modify: `PmTracker.Web/Views/Profil/Index.cshtml:51-58`

- [ ] **Step 7.1: Smazat `<section>` „Výchozí otevření editoru záznamu"** (řádky 51-58)

## Task 8: JS — vyčištění bootstrap.js od recordEditor importů

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (imports + references)

Imports k odstranění z bloku `from "./recordEditor.js"` (řádky 59-75):
- `clearStoredRecordEditorPreference`
- `closeRecordEditorChooser`
- `closeRecordEditorCloseGuard`
- `initPermissionMetadataBindings` — **OVĚŘIT, možná zachovat** (init perm metadata pro page editor?)
- `initRecordFormEnhancements` — **OVĚŘIT** (page editor může používat)
- `isRecordEditorFormDirty`
- `openRecordEditor`
- `prepareRecordEditorFormNavigation`
- `promptRecordEditorDiscard`
- `recordEditorState`
- `refreshRecordEditorPreferenceUi`
- `requestRecordEditorModalClose`
- `requestRecordEditorPageCancel`
- `restoreRecordEditorReturnStateFromUrl`
- `updateTaskTypeVisibility` — **PŘESUN** v Task 1, takže import změnit, ne smazat

- [ ] **Step 8.1: Najít všechny reference každého výše uvedeného symbolu** v bootstrap.js — `grep -n "openRecordEditor\|closeRecordEditorChooser\|...” bootstrap.js`
- [ ] **Step 8.2: Pro každý symbol který bootstrap.js volá, přesunout obsah volání do generic location nebo smazat — záleží na sémantice**
- [ ] **Step 8.3: Aktualizovat import z recordEditor.js na pouze opravdu potřebné funkce**
- [ ] **Step 8.4: bun build verify** — žádné errors

## Task 9: JS — smazat `recordEditor/` adresář a `recordEditor.js` barrel

**Files:**
- Delete: `PmTracker.Web/wwwroot/js/modules/recordEditor/index.js`
- Delete: `PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js`
- Delete: `PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js`
- Delete: `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js`
- Delete: `PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js`
- Delete: `PmTracker.Web/wwwroot/js/modules/recordEditor.js`

**ALE pozor:** Pokud `richtext.js` (nebo jiné) jsou používány page editorem, MUSÍM JE ZACHOVAT (přesunout do top-level modules/).

- [ ] **Step 9.1: Pro každý JS modul v recordEditor/ — `grep -rn "from.*recordEditor/<filename>"`** → pokud importuje cokoliv mimo recordEditor/, je sdílený
- [ ] **Step 9.2: Sdílené moduly přesunout** do `wwwroot/js/modules/` (top-level)
- [ ] **Step 9.3: `git rm` zbylé čistě modal-flow soubory + adresář**
- [ ] **Step 9.4: `git rm recordEditor.js` barrel**
- [ ] **Step 9.5: bun build verify** — žádné import errors

## Task 10: Tests — smazat / upravit

**Files:**
- Delete: `PmTracker.Tests.Unit/Architecture/RecordEditorJsSplitTests.cs`
- Modify: `PmTracker.Tests.Api/Controllers/RecordEditorControllerTests.cs` (případně smazat modal-only testy)
- Modify: `PmTracker.Tests.Unit/Modals/ModalLayoutMarkupTests.cs` (smazat EditZaznamModal-specific testy pokud existují)

- [ ] **Step 10.1: `git rm RecordEditorJsSplitTests.cs`** — testovala JS split do recordEditor/ adresáře, který nyní mažeme
- [ ] **Step 10.2: V `RecordEditorControllerTests.cs` — najít testy s `presentation=modal` nebo `presentation=page`** — přepsat tak aby neoperovaly s parametrem (vypustit), nebo smazat pokud testovaly jen modal cestu
- [ ] **Step 10.3: V `ModalLayoutMarkupTests.cs` — najít reference na `EditZaznamModal`** — smazat
- [ ] **Step 10.4: `dotnet test PmTracker.sln` verify** — všechno zelené

## Task 11: Final verification

- [ ] **Step 11.1: `dotnet build PmTracker.sln /nodeReuse:false`** — 0 warnings, 0 errors
- [ ] **Step 11.2: `dotnet test PmTracker.sln /nodeReuse:false`** — všechno zelené (1257+ tests)
- [ ] **Step 11.3: `bun build PmTracker.Web/wwwroot/js/site.js --target browser --outfile /tmp/check.js`** — bundle OK
- [ ] **Step 11.4: Manuální smoke test** (nebo Playwright/E2E pokud přijde po):
  - Klik „Nový záznam" → naviguje na page edit
  - Klik „Upravit záznam" → naviguje na page edit
  - Klik „Detail návrhu" → naviguje na page návrh
  - Profil — sekce „Výchozí otevření editoru záznamu" pryč
  - Žádný JS error v console
- [ ] **Step 11.5: Commit final** — pokud nejsou commitnuté všechny tasky, sjednotit do jednoho final commit `refactor(zaznam): remove record editor modal flow (page-only)`

---

## Plan self-review

**Spec coverage:** Všechny sekce spec doc mapped na tasky:
- Backend controllers → Task 2, 3
- Backend views → Task 4
- Backend modely → Task 5
- Razor triggery → Task 6
- Profile UI → Task 7
- JS moduly → Task 1, 8, 9
- Tests → Task 10
- Verifikace → Task 11

**Placeholder scan:** 4× „**OVĚŘIT**" v Task 8 — to NENÍ placeholder, je to explicit conditional logic se discovery commandem. Akceptováno.

**Type/symbol consistency:** `updateTaskTypeVisibility` použit v Task 1 (move) → Task 8 (import update) — konzistentní. `EditZaznamPage.cshtml` použit v Task 2 → Task 4 (zachován) — konzistentní.

**Scope:** Single-purpose refactor, focused.
