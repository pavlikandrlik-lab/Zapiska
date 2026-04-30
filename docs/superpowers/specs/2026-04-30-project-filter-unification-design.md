# Sjednocení project filtru pro Záznamy + Harmonogram

> Status: Draft for review · 2026-04-30 · Branch: codex/senior-refactor-fase-1

## Goal

Project Detail má dnes **dva oddělené filtry** — jeden pro záložku Záznamy (10 polí), druhý pro Harmonogram (jen 2 pole, "ořezaný"). Cíl: **jeden filtr, jeden state**, propsaný do obou záložek se stejnou funkcionalitou. Záložka Harmonogram dostane všechna pole, která má dnes Záznamy.

State je **vlastnost projektu** (per-project), ne per-tab. Když user změní filtr v Záznamy a přepne na Harmonogram, filtr je tam aplikován identicky. Persistence v `localStorage` (jeden klíč per projekt).

## Non-Goals

- **Server-side filtering** — filtr zůstává frontend-only (skrývání cards JS-em). Backend dál vrací všechna data tabu.
- **URL persistence** — žádný `?filter=...` v URL. Jen `localStorage`.
- **Změna `_ZaznamPartial.cshtml`** — records partial má dnes všechna potřebná `data-*` atributy.
- **Gantt code v `wwwroot/js/modules/schedule/gantt.js` + `schedule/filters.js`** — dead code (Gantt tab byl odstraněn, integrován do Harmonogramu). Není importován v `bootstrap.js`. Mimo scope; mohl by se v separátní cleanup smazat.
- **Jiné filtry v aplikaci** (dashboard, sync settings).

## Architecture

### High-level

```
┌─────────────────────────────────────────────────────────────┐
│ Project Detail page (Detail.cshtml)                         │
│                                                              │
│  ┌─ pm-tabs ─────────────────────────────────────────────┐  │
│  │  [Záznamy] [Harmonogram] [Jednání] [Tým] [Návrhy]     │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─ Active tab panel ────────────────────────────────────┐  │
│  │                                                        │  │
│  │  ┌─ _ProjectFilterShell.cshtml (shared partial) ─┐    │  │
│  │  │  • viditelný JEN v Záznamy + Harmonogram      │    │  │
│  │  │  • 10 fields, identický markup                 │    │  │
│  │  │  • data-project-filter-scope="<tab>"          │    │  │
│  │  └────────────────────────────────────────────────┘    │  │
│  │                                                        │  │
│  │  Records list nebo Schedule list                       │  │
│  └────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

       ▼ shared via JS
┌────────────────────────────────────────────┐
│ ProjectFilterController (JS)               │
│  • single config (10 fields)               │
│  • single state object per project         │
│  • localStorage key: pmtracker.project     │
│      Filters.v1.project.<projektId>        │
│  • applies to currently visible tab DOM    │
│  • re-applies on pm-tab-change event       │
└────────────────────────────────────────────┘
```

### Component boundaries

**`_ProjectFilterShell.cshtml` (Razor partial)** — UI shell.

- **Vstup:** ViewModel s lookup options (subsystémy, kategorie, stavy, typy, vlastníci, jednání-vyjádření stavy) + `ProjektId` + `CurrentUserOsobaId` + `Scope` ("records" / "schedule").
- **Výstup:** filter-shell HTML s 10 fields a `data-project-filter-scope="<scope>"`.
- **Závisí na:** lookup data (cached). Markup je identický pro oba scopes (Scope se používá jen pro DOM disambiguation).

**`ProjectFilterShellViewModel`** — sdílený VM předaný do partialu z obou tab buildů.

- **Drží:** lookup options + identifikátory.
- **Vyrábí ho:** `IProjectFilterShellViewModelBuilder` (server-side service), volaný z obou `BuildProjectRecordsTabAsync` + `BuildProjectScheduleTabAsync`.

**`projectFilter.js` (JS module, functional pattern)** — state + apply.

Zachovává existující functional API style modulu (žádný refactor na class). Module-scoped state cache, exportované funkce.

- **Drží:** state object v `localStorage` (jeden klíč per projekt). Module-scoped getter pro current state.
- **Exportované funkce:** `restoreProjectFilterScope(scope)`, `handleProjectFilterInputChange(input)`, `saveProjectFilterDefaults(scope)`, `clearProjectFilterPreferenceStorage(projektId)`, **nová** `reapplyOnTabChange(newScope)`.
- **Komunikuje:** přes `pm-tab-change` event z `pm-tabs` Web Component (existuje od refactoru 2026-04-30).
- **Závisí na:** localStorage, `data-filter-key` atributy na inputech, `data-filter-*` atributy na records/schedule cards.

**`ProjektHarmonogramUkolViewModel` rozšíření** — server-side data.

- Doplnit pole, která dnes má `_ZaznamPartial`, ale Schedule item ne: `Kategorie`, `KategorieKod`, `JeAktivni`, `VlastnikOsobaId`, `JednaniVyjadreniStav`.
- Schedule render přidá na `<article data-schedule-item>` tyto `data-filter-*` atributy (sjednocené názvy s `_ZaznamPartial`):
  - `data-filter-subsystem`, `data-filter-subsystem-kod` (rename z `data-schedule-filter-subsystem*`)
  - `data-filter-kategorie`, `data-filter-kategorie-kod`
  - `data-filter-stav`, `data-filter-stav-kod`
  - `data-filter-typ`, `data-filter-typ-kod`
  - `data-filter-vlastnik-id`, `data-filter-vlastnik`
  - `data-filter-aktivni` (`"true"` / `"false"`)
  - `data-filter-jednani-vyjadreni-stav`

### Data flow

1. User otevře `/projekty/{id}` → server vyrábí oba tab VM (Records + Schedule) — *to už dělá lazy: jen aktivní tab*.
2. Razor render obou taby zahrnuje `_ProjectFilterShell.cshtml` partial (jeden zdrojový soubor, 2 instance v DOMu).
3. Po DOMContentLoaded → `ProjectFilter.init()`:
   - Najde `[data-project-filter-scope]` ve viditelném tabu.
   - Načte state z localStorage (klíč `pmtracker.projectFilters.v1.project.<id>`).
   - Pokud klíč chybí → fallback na legacy `<klíč>.records`, načte, **smaže legacy**, uloží do nového klíče.
   - Zapíše hodnoty do form inputs uvnitř filter shell.
   - Aplikuje state na currently visible tab DOM (skryje neodpovídající cards).
4. User mění filter input → JS update state → save do localStorage → re-apply na visible tab DOM → re-render chips.
5. `pm-tab-change` event (z pm-tabs) → JS re-apply state na nově viditelný tab DOM.

### State shape

```json
{
  "subsystem": "ABC",
  "kategorie": "ukol",
  "stav": "rozpracovany",
  "typ": "rizene",
  "vlastnik": "42",
  "aktivni": true,
  "mine": false,
  "jednaniVyjadreniStav": "vyjadrene",
  "sortBy": "project-asc",
  "groupBySubsystem": true
}
```

Klíč: `pmtracker.projectFilters.v1.project.<projektId>` — ne `.records` ani `.schedule`.

### localStorage migrace

**Existující klíče (k odstranění):**
- `pmtracker.projectFilters.v1.project.<id>.records`
- `pmtracker.projectFilters.v1.project.<id>.schedule`

**Migrace** (jednorázová, při init):

1. Pokud `pmtracker.projectFilters.v1.project.<id>` (nový) existuje → použij ho.
2. Pokud chybí ale `<id>.records` (legacy) existuje → načti, ulož pod nový klíč, smaž legacy.
3. Smaž legacy `.schedule` klíč bez čtení (schedule měl jen 2 pole, records ho přebije).
4. Pokud žádný neexistuje → defaults z markupu (initial select values, `aktivni=true`, `groupBySubsystem=true`).

## Components — file-level changes

### Server (C#)

| File | Change |
|---|---|
| `Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | Doplnit pole na `ProjektHarmonogramUkolViewModel`: `Kategorie`, `KategorieKod`, `JeAktivni`, `VlastnikOsobaId`, `JednaniVyjadreniStav`. |
| `Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | Doplnit lookup options na `ProjektHarmonogramTabViewModel` (KategorieMoznosti, StavyUkoluMoznosti, TypyUkoluMoznosti, VlastniciMoznosti, StavyJednaniVyjadreni). |
| `Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs` | **Nový** sdílený VM: drží 10 fields lookup + ProjektId + CurrentUserOsobaId + Scope. |
| `Services/IProjectFilterShellViewModelBuilder.cs` | **Nové** rozhraní + impl. Drží query pro lookup options shared mezi oběma taby. |
| `Services/ProjectService.LazyQueries.cs` | `BuildProjectScheduleTabAsync` rozšířit o load chybějících polí + lookup options. `BuildProjectRecordsTabAsync` zachovat (data už má). |

### Razor

| File | Change |
|---|---|
| `Views/Projekty/_ProjectFilterShell.cshtml` | **Nový** sdílený partial — 10-field markup. Vstup: `ProjectFilterShellViewModel`. |
| `Views/Projekty/_ProjectRecordsTab.cshtml` | Inline filter markup nahradit `@await Html.PartialAsync("_ProjectFilterShell", Model.FilterShell)`. |
| `Views/Projekty/_ProjectScheduleTab.cshtml` | Stejně + render schedule cards s rozšířenými `data-filter-*` atributy (kategorie, vlastnikId, atd.). |

### JS

| File | Change |
|---|---|
| `wwwroot/js/modules/filters/projectFilter.js` | Sjednotit `projectFilterConfigs` do **jednoho config object** (smazat `.records` × `.schedule` rozlišení). Smazat `data-schedule-filter-*` references. |
| `wwwroot/js/modules/filters/projectFilter.js` | localStorage: nový klíč bez `.records`/`.schedule` suffixu + migrace step. |
| `wwwroot/js/modules/filters/index.js` | Listener na `pm-tab-change` → re-apply state na nově viditelný scope. |
| `wwwroot/js/modules/filters/recordDisplay.js` | Beze změny — apply logika pro records list zůstává (consumuje sjednocený state). |
| `wwwroot/js/modules/bootstrap.js` | Smazat `[data-schedule-filter-toggle]` / `[data-schedule-filter-key]` handlers (sjednoceno na `[data-filter-toggle]` / `[data-filter-key]`). |

### CSS

Beze změn — `.filter-shell`, `.filter-grid`, `.filter-toggle`, `.filter-panel` třídy zůstávají stejné.

### Cleanup (mimo hlavní task, jen poznámka)

- `wwwroot/js/modules/schedule/gantt.js` + `wwwroot/js/modules/schedule/filters.js` — dead code, nikam neimportováno. Mohou se smazat v separátním cleanup commitu.

## Error handling

- **Malformed localStorage value** (např. po manual edit): JSON.parse fail → fallback na defaults z markupu, log warning. Žádný hard crash.
- **Missing scope element** (např. uživatel je na Jednání tabu kde filter není): `init()` no-op return.
- **Unknown filter key** v state objektu (např. legacy field neznámý configu): ignored, ne-aplikováno, nedělá se chyba.

## Testing

### Unit tests (JS)

- `projectFilter.unit.test.js` (může být extracted z existing): config pro 10 fields, normalize pipeline, chip render, state ↔ inputs sync.
- Migrace test: simulace existujícího `<id>.records` localStorage → ověření že po init je nový klíč naplněn a legacy smazán.
- State application: synthetic DOM (records cards + schedule cards) + state → ověření že match/non-match cards dostanou správný `hidden` atribut.

### Architecture tests (C#)

- `ProjectFilterShellMarkupTests` — `_ProjectFilterShell.cshtml` má všech 10 `data-filter-key` atributů.
- `ProjektHarmonogramUkolViewModelFieldsTests` — VM obsahuje rozšířená pole (`Kategorie`, `JeAktivni`, atd.).
- `ProjectFilterPartialUsageTests` — `_ProjectRecordsTab.cshtml` i `_ProjectScheduleTab.cshtml` volají `PartialAsync("_ProjectFilterShell", ...)` (= DRY: žádné duplikované filter markup).

### E2E (manual smoke + later automated)

- Records → Harmonogram tab switch: filtr se přenese, cards filtrované stejně.
- Změna filtru v Harmonogram → switch na Records → filtr aplikován.
- Reload stránky → filter restored z localStorage.
- Migrace: ručně vložit do localStorage starý klíč `<id>.records`, reload → nový klíč existuje, starý smazán.

## Acceptance criteria

1. ✅ Filter shell renderuje 10 fields v Záznamy I Harmonogram (markup identical, jeden zdrojový partial).
2. ✅ State sdílený: změna v Records → switch tab → Harmonogram má stejný stav, cards filtrované identicky.
3. ✅ localStorage: jeden klíč `pmtracker.projectFilters.v1.project.<id>`, žádné `.records` / `.schedule` suffixy v novém kódu.
4. ✅ Migrace: existujícím userům s `<id>.records` se hodnoty přenesou do nového klíče při prvním návštěvě po deploy.
5. ✅ Unit testy + architecture testy passed.
6. ✅ Build 0 warnings + dotnet test full suite green.
