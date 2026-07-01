# Filtr návrhů Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přidat filtrování návrhů (stav, typ, subsystém, autor, rozhodl) na tab Návrhy s defaultem PENDING při každém načtení stránky. Rozšířit stávající filter systém o `proposals` scope bez dopadu na records/schedule.

**Architecture:** Nový `proposals` scope v `projectFilter.js` s vlastním polem 5 filtrů (ne sdílených 10). Nový `_ProposalFilterShell.cshtml` (stejné CSS, jiné inputy). Nový `ProposalFilterShellViewModel`. Client-side filtrování přes `data-filter-*` atributy na `.proposal-card`. Žádný localStorage — PENDING default na každém page load.

**Tech Stack:** ASP.NET MVC (Razor), Vanilla ESM JS, CSS (stávající `.filter-shell` třídy), xUnit + FluentAssertions (guard testy)

## Global Constraints

- Nesmí se změnit chování records/schedule filtrů — žádná regrese
- `buildProjectFilterConfig` rozšířit o volitelný `fields` parametr — stávající volání bez parametru vrací původních 10 polí
- Proposals scope nečte/nezapisuje sessionStorage/localStorage — default PENDING na každém page load
- Proposals scope se NEPŘIDÁVÁ do tab sync mappingu (`initProjectFilterTabSync`) — přepnutí tabu neresetuje filtr
- CSS: reuse `.filter-shell`, `.filter-panel`, `.filter-grid`, `.active-filter-chip` — žádné nové CSS třídy
- SUPERSEDED stav se mapuje na `"REJECTED"` v data atributu

## Analýza dopadů

### Bezpečné body (auto-detect scope z DOM):
- Event delegation: chip remove, save defaults, toggle, input change — všechny čtou scope z DOM atributů, fungují pro libovolný scope automaticky
- `persistFilterState()` auto-detekuje scope přes `closest("[data-project-filter-scope]")`
- Filter shell CSS je scope-agnostické

### Místa vyžadující explicitní rozšíření:
| Soubor | Řádek | Změna | Dopad na records/schedule |
|--------|-------|-------|--------------------------|
| `projectFilter.js:56` | `buildProjectFilterConfig` | Přidat volitelný `fields` param | Žádný — default = stávající `projectFilterFields` |
| `projectFilter.js:67-70` | `projectFilterConfigs` | Přidat `proposals` entry | Žádný — přidání nového klíče nemění existující |
| `projectFilter.js:568` | `restoreProjectFilterScope` | Přidat proposals branch (skip storage, apply default) | Žádný — branch jen pro `scope === "proposals"` |
| `bootstrap.js:128-136` | `handleProjectFilterInputChange` wrapper | Přidat `"proposals"` case | Žádný — nový `else if`, existující `if/else` beze změny |
| `filters/index.js` | Nová `applyProposalFilters` | Nová funkce | Žádný — přidání, ne modifikace |

### Místa kde se NESMÍ nic měnit:
- `projectFilterFields` (řádek 43-54) — hardcoded 10 polí pro records/schedule
- `projectFilterConfigs.records` / `.schedule` — existující scope configs
- `initProjectFilterTabSync` tab mapping — proposals se nepřidává
- `getProjectFilterStorageKey` — proposals nesdílí storage s records/schedule
- `applyRecordsView` / `applyProjectRecordFilters` / `applyProjectScheduleFilters`

---

### Task 1: Server — ViewModel + lookup kolekce

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Projekty/ProposalFilterShellViewModel.cs`
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` — přidat `FilterShell` property
- Modify: `PmTracker.Web/Services/RecordProposalService.Queries.cs` — plnění lookup kolekcí
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` — `PrepareProjectProposalsTabPresentation` nastavení `CurrentUserOsobaId`
- Test: `PmTracker.Tests.Unit/Projects/ProposalFilterViewModelTests.cs`

**Interfaces:**
- Consumes: `LookupOptionViewModel` (existující třída), `RecordProposalStateCodes`, `RecordProposalTypeCodes`
- Produces: `ProposalFilterShellViewModel` s 5 lookup kolekcemi, `ProjektNavrhyTabViewModel.FilterShell`

- [ ] **Step 1: Write failing test**

```csharp
// PmTracker.Tests.Unit/Projects/ProposalFilterViewModelTests.cs
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProposalFilterViewModelTests
{
    [Fact]
    public void ProposalsTab_ShouldRenderFilterShellPartial()
    {
        var view = Load("PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml");
        view.Should().Contain("_ProposalFilterShell",
            "tab Návrhy musí obsahovat filter shell partial");
    }

    [Fact]
    public void ProposalFilterShell_ShouldExist()
    {
        var path = Path.Combine(LocateRoot(), "PmTracker.Web", "Views", "Projekty", "_ProposalFilterShell.cshtml");
        File.Exists(path).Should().BeTrue("filter shell partial musí existovat");
    }

    [Fact]
    public void ProposalFilterShell_ShouldHaveProposalsScope()
    {
        var view = Load("PmTracker.Web/Views/Projekty/_ProposalFilterShell.cshtml");
        view.Should().Contain("data-project-filter-scope=\"proposals\"",
            "filter shell musí mít scope proposals");
    }

    [Fact]
    public void ProposalFilterShell_ShouldHaveFiveFilterKeys()
    {
        var view = Load("PmTracker.Web/Views/Projekty/_ProposalFilterShell.cshtml");
        view.Should().Contain("data-filter-key=\"stavNavrhu\"");
        view.Should().Contain("data-filter-key=\"typNavrhu\"");
        view.Should().Contain("data-filter-key=\"subsystem\"");
        view.Should().Contain("data-filter-key=\"autor\"");
        view.Should().Contain("data-filter-key=\"rozhodl\"");
    }

    private static string Load(string relativePath)
    {
        var full = Path.Combine(LocateRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(full);
    }

    private static string LocateRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
```

- [ ] **Step 2: Run test — verify fails**

Run: `dotnet build PmTracker.Tests.Unit && dotnet test PmTracker.Tests.Unit --filter "ProposalFilterViewModelTests" --no-build`
Expected: FAIL (partial neexistuje, view neobsahuje filter shell)

- [ ] **Step 3: Create `ProposalFilterShellViewModel`**

```csharp
// PmTracker.Web/Models/ViewModels/Projekty/ProposalFilterShellViewModel.cs
namespace PmTracker.Web.Models.ViewModels.Projekty;

public sealed class ProposalFilterShellViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<LookupOptionViewModel> StavyNavrhuMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> TypyNavrhuMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> AutoriMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> RozhodliMoznosti { get; init; } = [];
}
```

- [ ] **Step 4: Add `FilterShell` to `ProjektNavrhyTabViewModel`**

V `RecordProposalViewModels.cs`, do `ProjektNavrhyTabViewModel` přidat:

```csharp
public ProposalFilterShellViewModel FilterShell { get; set; } = new();
```

Přidat `using PmTracker.Web.Models.ViewModels.Projekty;` pokud chybí.

- [ ] **Step 5: Populate lookup kolekce v `BuildProjectProposalsTabAsync`**

V `RecordProposalService.Queries.cs`, za sestavení `NavrhyZalozeni` a `NavrhyHarmonogramu` listů, před return:

```csharp
var allProposalItems = result.NavrhyZalozeni.Concat(result.NavrhyHarmonogramu).ToList();

result.FilterShell = new ProposalFilterShellViewModel
{
    ProjektId = projectId,
    StavyNavrhuMoznosti = new List<LookupOptionViewModel>
    {
        new() { Value = "PENDING", Label = "Čeká na rozhodnutí" },
        new() { Value = "APPROVED", Label = "Schváleno" },
        new() { Value = "REJECTED", Label = "Zamítnuto" },
    },
    TypyNavrhuMoznosti = new List<LookupOptionViewModel>
    {
        new() { Value = RecordProposalTypeCodes.CreateRecord, Label = "Návrh záznamu" },
        new() { Value = RecordProposalTypeCodes.SchedulePlanChange, Label = "Návrh harmonogramu" },
    },
    SubsystemyMoznosti = allProposalItems
        .Where(p => !string.IsNullOrWhiteSpace(p.Subsystem))
        .Select(p => p.Subsystem)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(s => s, StringComparer.Create(new CultureInfo("cs-CZ"), false))
        .Select(s => new LookupOptionViewModel { Value = s, Label = s })
        .ToList(),
    AutoriMoznosti = allProposalItems
        .Where(p => !string.IsNullOrWhiteSpace(p.Autor))
        .Select(p => new { p.CreatedByOsobaId, p.Autor })
        .DistinctBy(x => x.CreatedByOsobaId)
        .OrderBy(x => x.Autor, StringComparer.Create(new CultureInfo("cs-CZ"), false))
        .Select(x => new LookupOptionViewModel { Value = x.CreatedByOsobaId.ToString(), Label = x.Autor })
        .ToList(),
    RozhodliMoznosti = allProposalItems
        .Where(p => !string.IsNullOrWhiteSpace(p.RozhodlUzivatel))
        .Select(p => new { p.DecidedByOsobaId, p.RozhodlUzivatel })
        .Where(x => x.DecidedByOsobaId.HasValue)
        .DistinctBy(x => x.DecidedByOsobaId)
        .OrderBy(x => x.RozhodlUzivatel, StringComparer.Create(new CultureInfo("cs-CZ"), false))
        .Select(x => new LookupOptionViewModel { Value = x.DecidedByOsobaId!.Value.ToString(), Label = x.RozhodlUzivatel! })
        .ToList(),
};
```

Pozn.: `CreatedByOsobaId` a `DecidedByOsobaId` potřebují být na `RecordProposalListItemViewModel` — ověřit, zda existují. Pokud ne, přidat je do query a VM.

- [ ] **Step 6: Set `CurrentUserOsobaId` v controlleru**

V `ProjektyController.cs`, v `PrepareProjectProposalsTabPresentation`:

```csharp
model.FilterShell.CurrentUserOsobaId = CurrentUserContext.OsobaId;
```

- [ ] **Step 7: Create `_ProposalFilterShell.cshtml`**

```razor
@model PmTracker.Web.Models.ViewModels.Projekty.ProposalFilterShellViewModel

<div class="filter-shell"
     data-project-filter-scope="proposals"
     data-project-id="@Model.ProjektId"
     data-current-user-id="@Model.CurrentUserOsobaId">

    <button class="filter-toggle" type="button" data-filter-toggle>
        Filtry
        <span class="filter-icon" aria-hidden="true">
            <gov-icon size="s" name="funnel" type="components"></gov-icon>
        </span>
    </button>
    <div class="active-filter-row" data-filter-chip-row="proposals" hidden="hidden"></div>

    <div class="filter-panel collapsed" data-filter-panel>
        <div class="filter-grid">
            <div>
                <label class="form-label">Stav návrhu</label>
                <select class="form-select" data-filter-key="stavNavrhu">
                    <option value="">Vše</option>
                    @foreach (var option in Model.StavyNavrhuMoznosti)
                    {
                        <option value="@option.Value"
                                selected="@(string.Equals(option.Value, "PENDING", StringComparison.OrdinalIgnoreCase) ? "selected" : null)">
                            @option.Label
                        </option>
                    }
                </select>
            </div>
            <div>
                <label class="form-label">Typ návrhu</label>
                <select class="form-select" data-filter-key="typNavrhu">
                    <option value="">Vše</option>
                    @foreach (var option in Model.TypyNavrhuMoznosti)
                    {
                        <option value="@option.Value">@option.Label</option>
                    }
                </select>
            </div>
            <div>
                <label class="form-label">Subsystém</label>
                <select class="form-select" data-filter-key="subsystem">
                    <option value="">Vše</option>
                    @foreach (var option in Model.SubsystemyMoznosti)
                    {
                        <option value="@option.Value">@option.Label</option>
                    }
                </select>
            </div>
            <div>
                <label class="form-label">Autor</label>
                <select class="form-select" data-filter-key="autor">
                    <option value="">Vše</option>
                    @foreach (var option in Model.AutoriMoznosti)
                    {
                        <option value="@option.Value">@option.Label</option>
                    }
                </select>
            </div>
            <div>
                <label class="form-label">Rozhodl</label>
                <select class="form-select" data-filter-key="rozhodl">
                    <option value="">Vše</option>
                    @foreach (var option in Model.RozhodliMoznosti)
                    {
                        <option value="@option.Value">@option.Label</option>
                    }
                </select>
            </div>
        </div>
    </div>
</div>
```

Pozn.: `selected="selected"` na PENDING option zajistí server-side default. JS `restoreProjectFilterScope("proposals")` poté čte tuto DOM hodnotu jako fallback.

- [ ] **Step 8: Include filter shell v `_ProjectProposalsTab.cshtml`**

Na začátek sekce (po `<section id="panel-navrhy" ...>`), před `<div class="proposal-section-grid">`:

```razor
@await Html.PartialAsync("_ProposalFilterShell", Model.FilterShell)
```

- [ ] **Step 9: Add `data-filter-*` atributy na `.proposal-card`**

Na oba `<article class="proposal-card">` elementy (řádky ~28 a ~93) přidat:

```razor
<article class="proposal-card"
         data-filter-stav-navrhu="@(string.Equals(item.Stav, "SUPERSEDED", StringComparison.OrdinalIgnoreCase) ? "REJECTED" : item.Stav)"
         data-filter-typ-navrhu="@item.TypNavrhu"
         data-filter-subsystem="@item.Subsystem"
         data-filter-autor="@item.CreatedByOsobaId"
         data-filter-rozhodl="@(item.DecidedByOsobaId?.ToString() ?? "")">
```

Pozn.: `TypNavrhu` a `CreatedByOsobaId`/`DecidedByOsobaId` musí být na `RecordProposalListItemViewModel` — pokud chybí, přidat do query a VM.

- [ ] **Step 10: Verify `RecordProposalListItemViewModel` has needed properties**

Zkontrolovat, že `TypNavrhu`, `CreatedByOsobaId`, `DecidedByOsobaId` existují na VM. Pokud ne, přidat je do VM a query v `BuildProjectProposalsTabAsync`.

- [ ] **Step 11: Build + run tests**

Run: `dotnet build PmTracker.Tests.Unit && dotnet test PmTracker.Tests.Unit --filter "ProposalFilterViewModelTests" --no-build`
Expected: PASS

---

### Task 2: JS — proposals scope v projectFilter.js

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js:56-70,568-582`
- Test: `tests/js/dom/proposalFilterConfig.test.js` (nový, node:test)

**Interfaces:**
- Consumes: Stávající `buildProjectFilterConfig`, `projectFilterConfigs`, `restoreProjectFilterScope`
- Produces: `proposals` scope config s 5 poli, `restoreProjectFilterScope("proposals")` s PENDING defaultem

**Kontext rizik:**
- `buildProjectFilterConfig` se volá jen na řádcích 68-69 (records, schedule). Přidání volitelného parametru nemá vedlejší efekt.
- `projectFilterConfigs` se čte přes `getProjectFilterConfig(scope)` (řádek 190) — přidání nového klíče nemění existující lookup.
- `restoreProjectFilterScope` se volá z: `filters/index.js:restoreFilterState()` (jen records), `initProjectFilterTabSync` (jen records/schedule mapping), a `bootstrap.js` (přes DOM auto-detect). Nová `if (scope === "proposals")` branch nemá vliv na existující flow.

- [ ] **Step 1: Modify `buildProjectFilterConfig` — add optional `fields` parameter**

Řádek 56:
```javascript
// BEFORE:
function buildProjectFilterConfig(scope) {
    return {
        ...
        fields: projectFilterFields
    };
}

// AFTER:
function buildProjectFilterConfig(scope, fields) {
    return {
        rootSelector: `[data-project-filter-scope="${scope}"]`,
        inputSelector: "[data-filter-key]",
        keyAttribute: "data-filter-key",
        chipRowSelector: `[data-filter-chip-row="${scope}"]`,
        statusSelector: `[data-filter-save-status="${scope}"]`,
        fields: fields || projectFilterFields
    };
}
```

- [ ] **Step 2: Add `proposalFields` + `proposals` config**

Po `projectFilterFields` (řádek 54), před `buildProjectFilterConfig`:

```javascript
const proposalFilterFields = [
    { inputKey: "stavNavrhu", stateKey: "stavNavrhu", type: "select", chipLabel: "Stav návrhu" },
    { inputKey: "typNavrhu", stateKey: "typNavrhu", type: "select", chipLabel: "Typ návrhu" },
    { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
    { inputKey: "autor", stateKey: "autor", type: "select", chipLabel: "Autor" },
    { inputKey: "rozhodl", stateKey: "rozhodl", type: "select", chipLabel: "Rozhodl" },
];
```

Do `projectFilterConfigs` (řádek 67-70):
```javascript
const projectFilterConfigs = {
    records: buildProjectFilterConfig("records"),
    schedule: buildProjectFilterConfig("schedule"),
    proposals: buildProjectFilterConfig("proposals", proposalFilterFields)
};
```

- [ ] **Step 3: Modify `restoreProjectFilterScope` — proposals branch**

Řádek 568, přidat proposals branch PŘED stávající logiku:

```javascript
export function restoreProjectFilterScope(scope) {
    if (scope === "proposals") {
        const fallbackState = buildProjectFilterStateFromInputs(scope);
        applyProjectFilterStateToInputs(scope, fallbackState);
        renderProjectFilterChips(scope);
        return fallbackState;
    }

    migrateLegacyProjectFilterStorageKeys(getProjectFilterProjectId(scope));
    // ... zbytek stávající logiky beze změny
```

Proč: `fallbackState` čte aktuální DOM hodnoty — a server renderuje PENDING jako `selected` na select elementu. Takže fallback = `{ stavNavrhu: "PENDING", ... rest empty }`. Žádný storage read/write.

- [ ] **Step 4: Run full unit test suite**

Run: `dotnet test PmTracker.Tests.Unit --no-build`
Expected: 1374+ PASS, 0 FAIL

---

### Task 3: JS — apply logika + event wiring

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/filters/index.js` — přidat `applyProposalFilters`, exportovat
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js:128-136` — přidat proposals case
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` — init proposals filter při `initRecordFormEnhancements` nebo `bootstrapPmTrackerApp`

**Interfaces:**
- Consumes: `buildProjectFilterStateFromInputs("proposals")`, `normalizeFilterToken`
- Produces: `applyProposalFilters()` — show/hide `.proposal-card` podle filter state

**Kontext rizik:**
- `bootstrap.js` handler wrapper (řádek 128-136): přidání `else if (resolvedScope === "proposals")` za existující `if ("schedule") / else if ("gantt")`. Stávající branches beze změny.
- Event delegation (change/input na `[data-filter-key]`): funguje automaticky přes `persistFilterState()` auto-detect — žádná změna potřeba.
- Init: `restoreProjectFilterScope("proposals")` + `applyProposalFilters()` se musí volat PO renderování proposals tabu. Lazy-load tab pattern: proposals tab může být lazy-loaded — init se musí volat po loaded.

- [ ] **Step 1: Create `applyProposalFilters` v `filters/index.js`**

```javascript
import { normalizeFilterToken } from "./projectFilter.js";

export function applyProposalFilters() {
    const state = buildProjectFilterStateFromInputs("proposals");
    const filters = {
        stavNavrhu: normalizeFilterToken(state.stavNavrhu),
        typNavrhu: normalizeFilterToken(state.typNavrhu),
        subsystem: normalizeFilterToken(state.subsystem),
        autor: normalizeFilterToken(state.autor),
        rozhodl: normalizeFilterToken(state.rozhodl),
    };

    const root = document.querySelector('[data-project-filter-scope="proposals"]');
    if (!root) return;

    const panel = root.closest("[data-tab-panel]") || document;
    const cards = panel.querySelectorAll(".proposal-card");

    cards.forEach(function (card) {
        const matchStav = !filters.stavNavrhu
            || normalizeFilterToken(card.dataset.filterStavNavrhu) === filters.stavNavrhu;
        const matchTyp = !filters.typNavrhu
            || normalizeFilterToken(card.dataset.filterTypNavrhu) === filters.typNavrhu;
        const matchSubsystem = !filters.subsystem
            || normalizeFilterToken(card.dataset.filterSubsystem) === filters.subsystem;
        const matchAutor = !filters.autor
            || normalizeFilterToken(card.dataset.filterAutor) === filters.autor;
        const matchRozhodl = !filters.rozhodl
            || normalizeFilterToken(card.dataset.filterRozhodl) === filters.rozhodl;

        card.hidden = !(matchStav && matchTyp && matchSubsystem && matchAutor && matchRozhodl);
    });

    // Skrýt prázdné sekce
    panel.querySelectorAll(".proposal-section-grid > .card").forEach(function (section) {
        const visibleCards = section.querySelectorAll(".proposal-card:not([hidden])");
        const emptyMsg = section.querySelector(".muted");
        const list = section.querySelector(".proposal-list");
        if (list) {
            list.hidden = visibleCards.length === 0;
        }
        if (emptyMsg) {
            emptyMsg.hidden = visibleCards.length > 0;
        }
    });
}
```

- [ ] **Step 2: Wire `applyProposalFilters` do `bootstrap.js` handler wrapperu**

Řádek 128-136, přidat `"proposals"` case:

```javascript
// BEFORE:
function handleProjectFilterInputChange(scope) {
    handleProjectFilterModuleInputChange(scope, {
        applyScope: (resolvedScope) => {
            if (resolvedScope === "schedule") {
                applyProjectScheduleFilters();
            }
            else if (resolvedScope === "gantt") {
                applyProjectGanttFilters();
            }
        }
    });
}

// AFTER:
function handleProjectFilterInputChange(scope) {
    handleProjectFilterModuleInputChange(scope, {
        applyScope: (resolvedScope) => {
            if (resolvedScope === "schedule") {
                applyProjectScheduleFilters();
            }
            else if (resolvedScope === "gantt") {
                applyProjectGanttFilters();
            }
            else if (resolvedScope === "proposals") {
                applyProposalFilters();
            }
        }
    });
}
```

Přidat import nahoře v bootstrap.js:
```javascript
import { applyProposalFilters } from "./filters/index.js";
```

- [ ] **Step 3: Init proposals filter**

V `bootstrap.js`, najít kde se inicializuje lazy-loaded proposals tab (po AJAX refresh `"projekty-detail-navrhy"` scope v `recordRefresh.js`, nebo po `initRecordFormEnhancements`). Přidat:

```javascript
export function initProposalFilterUi() {
    const root = document.querySelector('[data-project-filter-scope="proposals"]');
    if (!root) return;
    restoreProjectFilterScope("proposals");
    applyProposalFilters();
}
```

Exportovat z `filters/index.js` a volat v:
1. `bootstrapPmTrackerApp` (po `initProjectRecordsUi`) — pro případ že proposals tab je aktivní při prvním load
2. Po AJAX refreshi proposals tabu (`recordRefresh.js`, scope `"projekty-detail-navrhy"`)

- [ ] **Step 4: Build + verify**

Run: `dotnet build PmTracker.Web && dotnet test PmTracker.Tests.Unit --no-build`
Expected: Build OK, 1374+ PASS

- [ ] **Step 5: Visual verification**

Spustit `dotnet run --project PmTracker.Web`, otevřít projekt s návrhy, hard-refresh:
1. Default = jen PENDING návrhy viditelné
2. Rozbalit filtry → přepnout stav na "Schváleno" → jen schválené viditelné
3. Přepnout zpět na "Čeká na rozhodnutí" → jen pending
4. Přepnout na jiný tab (Záznamy) a zpět na Návrhy → filtr zachován (DOM stále v paměti)
5. F5 reload → filtr resetován na PENDING
6. Ověřit že records/schedule filtry fungují nezměněně
7. Dark mode: filter shell vypadá konzistentně
