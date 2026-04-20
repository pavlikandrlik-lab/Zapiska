# Fáze 3B — JS module splits — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rozdělit 5 největších JavaScript modulů (recordEditor 1919 LOC, schedule 1697 LOC, pickers 1530 LOC, filters 1105 LOC, ui 949 LOC) do focused per-feature souborů. Žádná funkční změna; architectural cleanup s zachováním importních API přes re-export barrel.

**Architecture:** JS feature-bounded split — každý nový modul má jedno téma (navigation, form, richtext, draft, atd.). Orchestrátorový modul re-exportuje veřejné API (backward compat pro `bootstrap.js` + `recordRefresh.js` + `ajax.js`). Bundle `site.bundle.js` synchronizován po každém split commitu (ověřená disciplína z Fází 2D-2E).

**Tech Stack:** ES modules (native `import`/`export`), no bundler for modules (bundle synced manually per commit), Playwright static harness pro visual verification.

---

## File Structure — Target po Fázi 3B

```
PmTracker.Web/wwwroot/js/modules/
├── recordEditor/
│   ├── navigation.js          ★ ~300 LOC — URL builder, chooser, openRecordEditor, captureReturnState
│   ├── form.js                ★ ~300 LOC — tabs, metadata, task-type visibility, meeting-number validation
│   ├── richtext.js            ★ ~200 LOC — Quill init, HTML detection, rich-text helpers
│   ├── draft.js               ★ ~600 LOC — storage, snapshot, dirty, close-guard, promptDiscard
│   └── index.js               ★ ~150 LOC — `initRecordFormEnhancements` orchestrator + re-exports
├── recordEditor.js             (backward-compat re-export barrel → recordEditor/index.js)
│
├── schedule/
│   ├── filters.js             ★ ~200 LOC — schedule + gantt filter panel open/close + state
│   ├── gantt.js               ★ ~250 LOC — ProjectGanttBoard class + gantt filter application
│   ├── timeline.js            ★ ~300 LOC — buildTimelineAxisTicks, renderTimelineAxis, renderStaticTimelineAxes
│   ├── block.js               ★ ~850 LOC — ScheduleBlockRenderer class (retained intact — cohesive unit)
│   └── index.js               ★ ~150 LOC — initRecordSchedulePlanner + initProjectScheduleUi + re-exports
├── schedule.js                 (backward-compat barrel)
│
├── pickers/
│   ├── date.js                ★ ~430 LOC — setAppDateFieldValue, close, initCustomDatePickers
│   ├── time.js                ★ ~145 LOC — initCustomTimePickers
│   ├── person.js              ★ ~520 LOC — formatPersonEntryLabel, initSinglePersonPickers + initCollabPickers
│   ├── adPerson.js            ★ ~545 LOC — initAdPersonPickers
│   └── index.js               ★ ~20 LOC — re-export barrel
├── pickers.js                  (backward-compat barrel)
│
├── filters/
│   ├── projectFilter.js       ★ ~400 LOC — config, normalize, chips, defaults
│   ├── recordDisplay.js       ★ ~300 LOC — visibility, view switching, subsystem indicator, tabs
│   ├── printFilter.js         ★ ~50 LOC — snapshot + query params
│   └── index.js               ★ ~200 LOC — state persistence + initProjectRecordsUi
├── filters.js                  (backward-compat barrel)
│
├── ui/
│   ├── print.js               ★ ~400 LOC — print chooser, format preference, URL resolution
│   ├── floating.js            ★ ~350 LOC — floating panel system, registry, positioning
│   └── index.js               ★ ~100 LOC — rainbow segment render + re-exports
└── ui.js                       (backward-compat barrel)
```

★ = nové soubory v Fázi 3B.

## Compat strategy — barrel pattern

Každý split modul má root barrel soubor (stejný název jako původní, e.g. `recordEditor.js`) který pouze re-exportuje z `recordEditor/index.js`:

```js
// recordEditor.js (backward-compat barrel)
export * from "./recordEditor/index.js";
```

Tím se `import { openRecordEditor } from "./recordEditor.js"` nelomí. Consumer files (`bootstrap.js`, `recordRefresh.js`, `ajax.js`) se nedotýkají. Dlouhodobě mohou migrovat imports na `./recordEditor/index.js` přímo (clean final state, Fáze 3 wrap nebo odděleně).

---

## Task 1: recordEditor.js split (1 919 LOC → 5 moduly)

**Risk: Medium.** 44 exported functions, 3 consumer files. Bundle sync critical.

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js`
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js`
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js`
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js`
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/index.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/recordEditor.js` → barrel
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js` (sync — the bundle now contains all submodules effectively merged; structure inside bundle can stay as before if bundle is a concatenation, but we must ensure no double-declaration)
- Create: `PmTracker.Tests.Unit/Architecture/RecordEditorJsSplitTests.cs`

- [ ] **Step 1.1: Audit current module**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"

# List exports + sections
grep -nE "^export (async )?function|^export (const|let|var|class)" PmTracker.Web/wwwroot/js/modules/recordEditor.js

# Find imports (what this module depends on)
grep -nE "^import " PmTracker.Web/wwwroot/js/modules/recordEditor.js

# Find consumer files
grep -rn "from \"\\./recordEditor\\.js\"\|from \"\\./recordEditor\"" PmTracker.Web/wwwroot/js --include="*.js"
```

Map each export to target submodule per plan. Write to `/tmp/p3b-t1-audit.md`.

- [ ] **Step 1.2: Architecture tests FIRST**

Vytvořit `PmTracker.Tests.Unit/Architecture/RecordEditorJsSplitTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3B Task 1: recordEditor.js (1919 LOC, 44 exportů) rozdělen
/// do 4 feature modulů + orchestrator + barrel re-export.
/// </summary>
public sealed class RecordEditorJsSplitTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        return directory.FullName;
    }

    private static string ResolvePath(string relative) =>
        Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    [Theory]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/index.js")]
    public void Submodule_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3B Task 1");
        File.ReadAllText(full).Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public void BarrelModule_ShouldReExportFromSubmodule()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor.js"));
        content.Should().Contain("export * from \"./recordEditor/index.js\"",
            "recordEditor.js zachovává backward-compat re-export barrel");
        // Barrel should be tiny
        File.ReadAllLines(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor.js")).Length
            .Should().BeLessThan(50, "barrel je jen re-export");
    }

    [Fact]
    public void NavigationModule_ShouldContainOpenRecordEditor()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js"));
        content.Should().Contain("export function openRecordEditor");
        content.Should().NotContain("function initRichTextEditors", "richtext patří do richtext.js");
        content.Should().NotContain("function saveRecordEditorDraft", "draft patří do draft.js");
    }

    [Fact]
    public void DraftModule_ShouldContainDraftFunctions()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js"));
        content.Should().Contain("saveRecordEditorDraft");
        content.Should().Contain("readRecordEditorDraft");
        content.Should().Contain("isRecordEditorFormDirty");
        content.Should().Contain("promptRecordEditorDiscard");
    }

    [Fact]
    public void RichtextModule_ShouldContainQuillInit()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js"));
        content.Should().Contain("initRichTextEditors");
    }

    [Fact]
    public void FormModule_ShouldContainTaskTypeVisibility()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js"));
        content.Should().Contain("updateTaskTypeVisibility");
    }

    [Fact]
    public void IndexModule_ShouldOrchestrate()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/index.js"));
        content.Should().Contain("initRecordFormEnhancements",
            "orchestrator exportuje inicializační entry point");
        content.Should().Contain("export", "index.js re-exportuje public API submodules");
    }
}
```

- [ ] **Step 1.3: Read + categorize exports**

```
Read PmTracker.Web/wwwroot/js/modules/recordEditor.js (1919 řádků — můžeš číst ve 4 chunks)
```

Pro každý exported function rozhodni target submodule:
- **navigation.js**: `buildRecordEditorUrl`, `navigateToRecordEditorPage`, `openRecordEditor`, `captureRecordEditorReturnState`, `restoreRecordEditorReturnStateFromUrl`, `createRecordEditorChooser`, `showRecordEditorChooser`, `handleRecordEditorChoice`, `closeRecordEditorChooser`, `refreshRecordEditorPreferenceUi`, `clearStoredRecordEditorPreference`, `requestRecordEditorModalClose`
- **form.js**: `initPermissionMetadataBindings`, `updateTaskTypeVisibility`, `setRecordFormTab`, `initRecordFormTabs`, `initExternalLinksEditors`, `initRecordOwnerAutofill`, `initMeetingNumberValidation`, `initRecordGoalAutoGrow`, `initRecordMeetingDateSync`, `initConfirmSubmitToggles`, `initAdPersonPickers`, `initCollabPickers`, `initCustomDatePickers`, `initCustomTimePickers`, `initSinglePersonPickers` (některé z těchto fungují přes picker.js — ověřit zda se importují nebo redefinují; pokud importují, zůstávají v form.js, ne v pickers)
- **richtext.js**: `initRichTextEditors`, `getOrCreateRichTextSourceContainer`, `looksLikeHtml`, `setRecordEditorRichTextValue`
- **draft.js**: `saveRecordEditorDraft`, `readRecordEditorDraft`, `applyRecordEditorDraft`, `scheduleRecordEditorDraftSave`, `clearRecordEditorDraft`, `clearRecordEditorDraftSaveTimer`, `getRecordEditorDraftStorageKey`, `buildRecordEditorFormSnapshot`, `shouldIgnoreRecordEditorField`, `isRecordEditorFormDirty`, `markRecordEditorFormClean`, `promptRecordEditorDiscard`, `closeRecordEditorCloseGuard`, `prepareRecordEditorFormNavigation`, `maybeRestoreRecordEditorDraft`, `initRecordEditorDirtyTracking`
- **index.js**: `initRecordFormEnhancements` + re-export * z navigation, form, richtext, draft. State objects (`recordEditorState`) zůstává v nejrelevantnějším modulu — pravděpodobně `navigation.js` (chooser state), či rozdělit.

- [ ] **Step 1.4: Create submodules one-by-one**

Začít od `draft.js` (nejvíc self-contained — no deps on other submodules). Pak `richtext.js`, `form.js`, `navigation.js`, nakonec `index.js` orchestrator.

Každý file:
- File-scoped doc comment na vrchu
- Minimal `import` directives
- `export` shodné jako v původním

Cross-submodule imports jsou OK (e.g., `navigation.js` potřebuje `isRecordEditorFormDirty` z `draft.js` pro dirty-check před close) — explicit import:

```js
import { isRecordEditorFormDirty, promptRecordEditorDiscard } from "./draft.js";
```

- [ ] **Step 1.5: Create index.js orchestrator**

```js
// recordEditor/index.js — entry point + re-export barrel
export * from "./navigation.js";
export * from "./form.js";
export * from "./richtext.js";
export * from "./draft.js";

// Explicit orchestrator that consumers expect
import { initCustomDatePickers, initCustomTimePickers } from "../pickers.js";
import { initSinglePersonPickers, initAdPersonPickers, initCollabPickers } from "../pickers.js";
import { initRecordOwnerAutofill, initExternalLinksEditors, initMeetingNumberValidation,
         initConfirmSubmitToggles, initRecordFormTabs, initRecordGoalAutoGrow,
         initRecordMeetingDateSync, updateTaskTypeVisibility } from "./form.js";
import { initPermissionMetadataBindings } from "./form.js";
import { initRichTextEditors } from "./richtext.js";
import { initRecordEditorDirtyTracking } from "./draft.js";

export function initRecordFormEnhancements(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    initCustomDatePickers(scope);
    initCustomTimePickers(scope);

    scope.querySelectorAll("[data-kategorie-select]").forEach((element) => {
        if (element instanceof HTMLSelectElement) {
            updateTaskTypeVisibility(element);
        }
    });

    initSinglePersonPickers(scope);
    initRecordOwnerAutofill(scope);
    initAdPersonPickers(scope);
    initExternalLinksEditors(scope);
    initCollabPickers(scope);
    initMeetingNumberValidation(scope);
    initConfirmSubmitToggles(scope);
    initRecordFormTabs(scope);
    initRecordGoalAutoGrow(scope);
    initRecordMeetingDateSync(scope);
    initRichTextEditors(scope);
    initRecordEditorDirtyTracking(scope);
    initPermissionMetadataBindings(scope);
}
```

- [ ] **Step 1.6: Convert recordEditor.js to barrel**

```js
// recordEditor.js — backward-compat re-export barrel.
// Nové kódy importujte z "./recordEditor/index.js" přímo.
export * from "./recordEditor/index.js";
```

- [ ] **Step 1.7: Build + test + bundle sync**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Expected: Build 0 error, tests 390 + 7 = 397.

**Bundle sync:** kritický krok. `site.bundle.js` je konkatenace existujících modulů. Po splitu musíme ověřit že bundle:
- Obsahuje všechny funkce z nových submodulů (ne duplikovaně)
- Žádný broken import cross-submodule

Preferovaný přístup: **regenerovat bundle z modulů**. Pokud neexistuje build script, ruční sync:
- Najít blok corresponding k původnímu recordEditor.js v bundle
- Nahradit jeho obsahem nového spojeného recordEditor submodulu (konkatenace navigation.js + form.js + richtext.js + draft.js + index.js bez duplikátů imports)

Alternativně: použít `cat` a vyseknout import lines:
```bash
cat PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js \
    PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js \
    PmTracker.Web/wwwroot/js/modules/recordEditor/form.js \
    PmTracker.Web/wwwroot/js/modules/recordEditor/navigation.js \
    PmTracker.Web/wwwroot/js/modules/recordEditor/index.js | grep -v "^import " > /tmp/recordEditor-concat.js
```

Pak nahradit odpovídající blok v `site.bundle.js`. Verify žádné duplikáty.

- [ ] **Step 1.8: Playwright harness smoke (optional)**

Pokud lze, spustit `/tmp/pm-modal-harness.html` (Fáze 2E) — ověřit, že nově rozdělená architektura neudělala JS konzoli errors.

- [ ] **Step 1.9: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/recordEditor/ \
        PmTracker.Web/wwwroot/js/modules/recordEditor.js \
        PmTracker.Web/wwwroot/js/site.bundle.js \
        PmTracker.Tests.Unit/Architecture/RecordEditorJsSplitTests.cs

git commit -m "refactor(js): rozbití recordEditor.js do 4 submodulů (Fáze 3B Task 1)

1919 LOC / 44 exports → 5 souborů pod recordEditor/:
- navigation.js — URL builder, chooser, openRecordEditor, return state
- form.js — tabs, task-type visibility, metadata bindings, validation
- richtext.js — Quill init, rich-text helpers
- draft.js — storage, snapshot, dirty, close-guard, promptDiscard
- index.js — initRecordFormEnhancements orchestrator + re-exports

recordEditor.js zachován jako backward-compat barrel
(\"export * from './recordEditor/index.js'\"). Consumers (bootstrap.js,
recordRefresh.js, ajax.js) beze změny.

Bundle (site.bundle.js) synchronizován.

+N architecture unit tests."
```

---

## Task 2-5 (schedule, pickers, filters, ui)

Stejný pattern jako Task 1:
1. Audit exports
2. Architecture tests FIRST
3. Create submodules
4. Create index.js orchestrator
5. Convert root to barrel
6. Build + test + bundle sync
7. Commit

Detailed plán každého tasku v tomto dokumentu (zkrácený — plný popis bude generován před exekucí každého task):

### Task 2: schedule.js (1697 LOC) → 5 submodulů (filters, gantt, timeline, block, index)

**Key considerations:**
- `ScheduleBlockRenderer` class (~850 LOC) zůstává intact — sam o sobě cohesive unit
- `ProjectGanttBoard` class (~180 LOC) do gantt.js
- Timeline utility funkce do timeline.js
- Filter state do filters.js (může sdílet s `filters/` folder — rozhodnout: ponechat v schedule/ scope, nebo extract do filters/ folder? **Doporučení:** ponechat v schedule/ pro cohesion)

### Task 3: pickers.js (1530 LOC) → 4 submodulů

- `pickers/date.js` — 3 date picker exports
- `pickers/time.js` — 2 time picker exports
- `pickers/person.js` — single + collab person pickers
- `pickers/adPerson.js` — AD person picker (extensive search integration)
- `pickers/index.js` — barrel re-export

### Task 4: filters.js (1105 LOC) → 4 submodulů

- `filters/projectFilter.js` — project filter config, normalization, chips, defaults
- `filters/recordDisplay.js` — visibility, view switching, subsystem indicator, tabs
- `filters/printFilter.js` — snapshot + query params helpers
- `filters/index.js` — state persistence + initProjectRecordsUi

### Task 5: ui.js (949 LOC) → 3 submodulů

- `ui/print.js` — print chooser, format preference, URL resolution
- `ui/floating.js` — floating panel system, registry, positioning
- `ui/index.js` — rainbow render + re-exports

---

## Bundle sync strategy

Po každém JS splitu je **kritické** synchronizovat `site.bundle.js` (existující disciplína z 2E/3A). Možnosti:

**A. Ruční sync (stávající praxe)**:
- Najít pattern v bundle matching původní modul
- Nahradit obsahem nových submodulů (bez imports)
- Verify: žádné duplikát function definitions

**B. Skript pro regeneraci bundle**:
Zvažuje se v Fázi 3 — nice to have, ne blocker. Stávající ruční disciplína funguje.

**C. Dočasně odstranit bundle referenci z HTML a místo ní linkovat moduly přímo**:
High-risk, diverguje od production bundle. **Nedělat v 3B.** Zvážit v 3E nebo samostatné fázi.

**Doporučení: A (ruční sync) s novým architecture testem ověřujícím, že bundle.js obsahuje klíčové exports po splitu**:

```csharp
[Fact]
public void Bundle_ShouldContainKeyExports()
{
    var bundle = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/site.bundle.js"));
    bundle.Should().Contain("function openRecordEditor",
        "bundle obsahuje klíčovou funkci z recordEditor/navigation");
    bundle.Should().Contain("function initRichTextEditors",
        "bundle obsahuje klíčovou funkci z recordEditor/richtext");
    bundle.Should().Contain("function isRecordEditorFormDirty",
        "bundle obsahuje klíčovou funkci z recordEditor/draft");
}
```

---

## Testing strategy

**Per-task:**
- Unit architecture tests (presence, content distribution, barrel pattern)
- Bundle contains-key-exports test
- Build + dotnet test green (target: +7-10 tests per task = +35-50 total for 3B)

**Per-phase end:**
- Playwright harness smoke (modal open/close, form dirty check — stejný harness jako 2E)
- Manual check: otevřít StyleGuide, zkontrolovat žádné JS konzole errors

## Rollback strategy

Každý task je 1 commit. Pokud něco rozbije:
```bash
git revert <task-sha>
```

Vrátí soubory, vrátí bundle, vrátí testy. Žádné DB ani runtime state nedotčený.

## Exit criteria pro Fázi 3B

- ☐ 5 god-modulů (recordEditor, schedule, pickers, filters, ui) rozbito
- ☐ Každý nový modul < 500 LOC (outlier: `schedule/block.js` ~850 LOC — cohesive class)
- ☐ Root moduly jsou barrel re-exporty (< 50 LOC)
- ☐ `site.bundle.js` synchronizován, no broken imports
- ☐ +35-50 architecture tests, všechny zelené
- ☐ Consumer files (`bootstrap.js`, `recordRefresh.js`, `ajax.js`) beze změny
- ☐ Playwright harness smoke pass
