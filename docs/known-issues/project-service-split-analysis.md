# ProjectService split — analýza a deferral

**Status:** Attempt rolled back — scope je větší než single-sprint.
**Datum analýzy:** 2026-04-23 (R3 review L-1 follow-up)

## Motivace

`PmTracker.Web.Services.ProjectService` je partial class rozprostřená přes 13 souborů (~3350 LOC celkem) s konstruktorem o 12 závislostech. Code-quality review R3 L-1 flagoval change-magnet ctor jako tech debt.

## Attempted approach (abandoned)

Extrahovat `ProjectTeamService` (TeamComposition.cs 802 LOC + Assignments.cs 401 LOC = 1203 LOC) s vlastním konstruktorem (4 deps).

## Proč attempt selhal

Cross-partial coupling se ukázal rozsáhlejší než vypadalo na povrchu:

### 1. Sdílené helpery volané z obou stran
- `LoadPeopleByIdsAsync`, `LoadOrganizationsByPeopleAsync`, `LoadOrgUnitsByPeopleAsync` definované v `RecordCards.cs`, volané z:
  - TeamComposition (4× call sites)
  - LazyQueries (5× call sites)
  - RecordCards (3× call sites — home)
- `BuildActiveProjectSubsystemsAsync` definovaný v `TeamComposition.cs`, volaný z:
  - LazyQueries (2×)
  - RecordComposition (2× přes interface impl)
  - RecordService.EditorQueries (1× přes `IRecordEditorQueriesComposition`)
- `BuildDisplayName`, `BuildDisplayNameFromOsoba` — formátovací helpery volané napříč
- `ResolveNextProjectSubsystemOrderAsync` — pricing helper

### 2. Sdílené state fields
- `Ci` (StringComparer, static readonly) — používaný napříč
- `timeProvider`, `priorityMatrixRebuildService` — Assignments je potřebuje

### 3. Interface segregation je částečná
- `IProjectService` má **26 metod** (11 jsou team-related → 42% surface area)
- `IProjectDetailComposition`, `IRecordEditorQueriesComposition`, `IRecordWriteCommandsComposition` — všechny tři implementuje ta stejná partial class a obsahují metody volající neoddělitelně provázanou logiku

### 4. Razor views + controllers závislé na `IProjectService` širokém kontraktu
- `ProjektyController` deleguje 14+ metod různých kategorií
- Composition interfaces jsou injektované do dalších services (`RecordService.EditorQueries`)

## Požadovaná správná strategie (proper sprint)

Proper split potřebuje 1-2 dny dedicated work, ideálně s dedicated plan v `docs/superpowers/plans/`:

1. **Vytvořit sdílené `ProjectQueryHelpers` static class** pro common lookups (Load people/orgs/org units, BuildActiveSubsystems) — eliminates cross-partial coupling.
2. **Extrahovat domain helpers** (formátovací, `ResolveNext*`) do dedicated static/helper classes.
3. **ISP split interface** `IProjectService` na focus interfaces:
   - `IProjectCrudService` (5-6 metod)
   - `IProjectTeamService` (11 metod)
   - `IProjectDetailCompositionService` (5-6 metod)
   - `IRecordEditorCompositionService` (5-6 metod)
4. **Implementovat split services** s focused constructors (3-5 deps each).
5. **Update controllers** — migrate on per-controller basis, each PR small.
6. **Integration test covarage** — ensure no behavior regression during migration.

Expected outcome: **4 focused services s 3-5 deps each**, místo 1 god service s 12.

## Proč nedělat to teď

1. **User's current sprint priority** byl quick wins (#5 IIS docs, #6 error envelope) — split zachycen jako nepriorita.
2. **Risk budget** — 1081 unit + 255 API tests pass; rizko introduct regresi při větším refactoru je vyšší než benefit.
3. **Scope diversity** — hodina reverse-engineering cross-partial coupling ukázala, že "jedna zkratka" neexistuje. Správný split potřebuje plan + TDD discipline.

## Recommended tracking

1. Přidat do backlogu jako "senior refactor fáze 2" (dedicated plan).
2. Před začátkem nového rozsáhlého feature scope-u v ProjectService zvážit, jestli danou funkci přidat jako NEW service (zabránit dalšímu růstu god service).
3. Pokud god ctor začne reálně bolet (merge conflicts mezi týmy, nepochopitelné change sets), upřednostnit split před dalšími features.

## Relevantní lokace

- `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/ProjectService*.cs` (13 partials)
- `/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/IProjectService.cs` (26 metod)
- Composition interfaces: `IProjectDetailComposition`, `IRecordEditorQueriesComposition`, `IRecordWriteCommandsComposition`
- Controllers s 14+ project service calls: `ProjektyController.*.cs`
