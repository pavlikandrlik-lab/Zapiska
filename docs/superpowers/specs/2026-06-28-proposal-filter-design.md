# Filtr návrhů — rozšíření stávajícího filter systému

## Cíl

Přidat filtrování na tab Návrhy (stav, typ, subsystém, autor, rozhodl) s defaultem PENDING. Přidat `proposals` scope do stávajícího procedurálního filter systému (`projectFilter.js`) — stejný vzor jako records/schedule, žádný refactor fungujícího kódu.

## Architektura

### JS: rozšíření `projectFilter.js`

Přidat `proposalFields` pole a `proposals` scope do `projectFilterConfigs`:

```js
const proposalFields = [
    { inputKey: "stavNavrhu", stateKey: "stavNavrhu", type: "select", chipLabel: "Stav návrhu" },
    { inputKey: "typNavrhu", stateKey: "typNavrhu", type: "select", chipLabel: "Typ návrhu" },
    { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
    { inputKey: "autor", stateKey: "autor", type: "select", chipLabel: "Autor" },
    { inputKey: "rozhodl", stateKey: "rozhodl", type: "select", chipLabel: "Rozhodl" },
];

projectFilterConfigs.proposals = buildProjectFilterConfig("proposals");
// buildProjectFilterConfig se rozšíří o parametr fields (default = projectFilterFields)
```

### JS: `restoreProjectFilterScope` — proposals default

Proposals scope nemá localStorage persistence. Při `restore()` vždy aplikuje hardcoded default `{ stavNavrhu: "PENDING" }` nezávisle na session/localStorage. Realizace: v `restoreProjectFilterScope` detekce scope === "proposals" → skip storage read, použít defaultState.

### JS: `applyProposalFilters` — nová funkce ve `filters/index.js`

Client-side filtrování `.proposal-card` elementů podle `data-filter-*` atributů:
- Pro každý `.proposal-card`: porovnat `data-filter-stav-navrhu`, `data-filter-typ-navrhu`, `data-filter-subsystem`, `data-filter-autor`, `data-filter-rozhodl` se state
- Card `hidden` pokud neodpovídá filtru
- Sekce (`.card` s h3) `hidden` pokud všechny její `.proposal-card` jsou hidden
- Prázdná zpráva se ukáže pokud celá sekce je prázdná po filtraci

### JS: event delegation v `bootstrap.js`

Rozšíření stávajícího `handleProjectFilterInputChange` o proposals scope — stejný pattern jako records/schedule (click toggle, change/input na filter-key, chip remove).

### Server: ViewModel rozšíření

`ProjektNavrhyTabViewModel` získá nové property + `FilterShell`:

```csharp
public ProjectFilterShellViewModel FilterShell { get; set; }
```

`ProjectFilterShellViewModel` je STÁVAJÍCÍ třída — ale proposals potřebuje jiná pole než records. Dvě varianty:

**Varianta A (doporučená):** Nový `ProposalFilterShellViewModel` s 5 specifickými kolekcemi + nový `_ProposalFilterShell.cshtml` partial. DRY na CSS úrovni (stejné `.filter-shell` třídy), ale vlastní markup pro 5 filtrů.

**Varianta B:** Rozšířit `ProjectFilterShellViewModel` o proposals kolekce a `_ProjectFilterShell.cshtml` podmínit scopem. Vede k podmínkovému spaghetti v jedné partial.

→ **Varianta A** — čistější, oddělené zodpovědnosti.

### Nový ViewModel: `ProposalFilterShellViewModel`

```csharp
public sealed class ProposalFilterShellViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<LookupOptionViewModel> StavyNavrhuMoznosti { get; init; }
    public IReadOnlyList<LookupOptionViewModel> TypyNavrhuMoznosti { get; init; }
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; }
    public IReadOnlyList<LookupOptionViewModel> AutoriMoznosti { get; init; }
    public IReadOnlyList<LookupOptionViewModel> RozhodliMoznosti { get; init; }
}
```

Zdroj dat:
- `StavyNavrhuMoznosti` = hardcoded: Čeká na rozhodnutí (PENDING), Schváleno (APPROVED), Zamítnuto (REJECTED)
- `TypyNavrhuMoznosti` = hardcoded: Návrh záznamu (CREATE_RECORD), Návrh harmonogramu (SCHEDULE_PLAN_CHANGE)
- `SubsystemyMoznosti` = distinct subsystémy z návrhů v projektu (join na Subsystemy)
- `AutoriMoznosti` = distinct autoři z návrhů v projektu (join na Osoby, format "Příjmení Jméno")
- `RozhodliMoznosti` = distinct DecidedByOsobaId z návrhů v projektu (join na Osoby)

### View: `_ProposalFilterShell.cshtml` (nový)

Nová partial — stejná HTML struktura jako `_ProjectFilterShell.cshtml`, ale s 5 select poli:
- `data-project-filter-scope="proposals"`
- 5× `<select data-filter-key="stavNavrhu|typNavrhu|subsystem|autor|rozhodl">`
- Žádné checkboxy (groupBySubsystem, aktivni, mine)
- "Uložit jako výchozí" tlačítko SKRYTÉ (proposals nemá persistence)

### View: `_ProjectProposalsTab.cshtml` úpravy

1. Vložit `@await Html.PartialAsync("_ProposalFilterShell", Model.FilterShell)` na začátek
2. Každý `.proposal-card` dostane data atributy:
   - `data-filter-stav-navrhu="PENDING"` (SUPERSEDED → "REJECTED")
   - `data-filter-typ-navrhu="CREATE_RECORD"` nebo `"SCHEDULE_PLAN_CHANGE"`
   - `data-filter-subsystem="GESTOR"`
   - `data-filter-autor="14"` (OsobaId)
   - `data-filter-rozhodl="7"` (OsobaId, prázdné pokud nerozhodnuto)

### CSS

Žádné nové CSS — reuse `.filter-shell`, `.filter-panel`, `.filter-grid`, `.active-filter-chip` z records filtru.

## Soubory (přehled)

| Soubor | Akce |
|---|---|
| `js/modules/filters/projectFilter.js` | Přidat `proposalFields` + `proposals` scope config |
| `js/modules/filters/index.js` | Přidat `applyProposalFilters`, `initProposalFilterUi` |
| `js/modules/bootstrap.js` | Rozšířit event delegation o proposals scope |
| `Models/ViewModels/Projekty/ProposalFilterShellViewModel.cs` | Nový ViewModel |
| `Models/ViewModels/RecordProposalViewModels.cs` | `FilterShell` property na `ProjektNavrhyTabViewModel` |
| `Services/RecordProposalService.Queries.cs` | Plnění lookup kolekcí |
| `Controllers/ProjektyController.cs` | `PrepareProjectProposalsTabPresentation` → nastavení FilterShell |
| `Views/Projekty/_ProposalFilterShell.cshtml` | Nová partial |
| `Views/Projekty/_ProjectProposalsTab.cshtml` | Vložit filter shell + data atributy |

## Mimo scope

- OOP refactor filter systému (budoucí iterace při 4.+ scopu)
- Řazení návrhů (chronologické stačí)
- Seskupení dle subsystému (records-only)
- localStorage persistence pro proposals (záměrně vypnuto)
