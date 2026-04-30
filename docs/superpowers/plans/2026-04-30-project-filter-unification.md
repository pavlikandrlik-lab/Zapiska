# Project Filter Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sjednotit dva oddělené project filtry (Záznamy + Harmonogram) do jednoho shared state per projekt — jeden Razor partial, jedna JS config, jeden localStorage klíč.

**Architecture:** Sdílený `_ProjectFilterShell.cshtml` partial s 10-field markup mountovaný v obou tabech. JS state v jednom localStorage klíči `pmtracker.projectFilters.v1.project.<id>` (bez scope suffixu). Re-apply na `pm-tab-change` event z pm-tabs Web Component. Schedule data rozšířena o pole, která už má records.

**Tech Stack:** ASP.NET Core 8 MVC, Razor partials, vanilla ESM JS, custom Web Components (pm-tabs), xUnit + FluentAssertions pro architecture testy, vitest pro JS unit testy.

**Spec:** [docs/superpowers/specs/2026-04-30-project-filter-unification-design.md](../specs/2026-04-30-project-filter-unification-design.md)

---

## File Map

**New files:**
- `PmTracker.Web/Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs` — sdílený VM (T1)
- `PmTracker.Web/Services/Projekty/IProjectFilterShellViewModelBuilder.cs` + impl (T2)
- `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml` — sdílený partial (T6)
- `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs` — markup parity tests (T6, T7)
- `PmTracker.Tests.Unit/Services/Projekty/ProjectFilterShellViewModelBuilderTests.cs` (T2)

**Modified files:**
- `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` — rozšířit `ProjektHarmonogramUkolViewModel` (T3) + `ProjektHarmonogramTabViewModel` (T4)
- `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` — `ProjektZaznamyTabViewModel.FilterShell` (T4)
- `PmTracker.Web/Services/ProjectService.LazyQueries.cs` — oba tab buildery použijí builder (T4)
- `PmTracker.Web/Services/ProjectService.ScheduleComposition.cs` — mapovat nová pole z `ZaznamCardViewModel` (T5)
- `PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml` — partial call (T7)
- `PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml` — partial call + rozšířené `data-filter-*` na schedule cards (T7)
- `PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js` — sjednocený config + nový storage klíč + migrace (T8)
- `PmTracker.Web/wwwroot/js/modules/filters/index.js` — `pm-tab-change` listener (T9)
- `PmTracker.Web/wwwroot/js/modules/bootstrap.js` — smazat `data-schedule-filter-*` handlers (T10)

---

## Task 1: ProjectFilterShellViewModel (data class)

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs`

- [ ] **Step 1: Vytvoř ProjectFilterShellViewModel**

```csharp
namespace PmTracker.Web.Models.ViewModels;

/// <summary>
/// Sdílený VM pro <c>_ProjectFilterShell.cshtml</c> partial — drží 10 lookup options
/// + identifikátory potřebné pro JS filter init. Mountován v Records i Schedule
/// tabech, scope drží JS pro DOM disambiguation (apply na visible tab).
/// Spec: docs/superpowers/specs/2026-04-30-project-filter-unification-design.md
/// </summary>
public sealed class ProjectFilterShellViewModel
{
    public required int ProjektId { get; init; }
    public required int CurrentUserOsobaId { get; init; }

    /// <summary>"records" nebo "schedule" — používá se jako data-project-filter-scope.</summary>
    public required string Scope { get; init; }

    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> KategorieMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> TypyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> VlastniciMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyJednaniVyjadreni { get; init; } = Array.Empty<LookupOptionViewModel>();
}
```

- [ ] **Step 2: Build solution**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs
git commit -m "feat(project-filter): nový shared ProjectFilterShellViewModel"
```

---

## Task 2: ProjectFilterShellViewModelBuilder (service)

**Files:**
- Create: `PmTracker.Web/Services/Projekty/IProjectFilterShellViewModelBuilder.cs`
- Create: `PmTracker.Web/Services/Projekty/ProjectFilterShellViewModelBuilder.cs`
- Create: `PmTracker.Tests.Unit/Services/Projekty/ProjectFilterShellViewModelBuilderTests.cs`

- [ ] **Step 1: Najdi existing source ProjektFiltryViewModel** (ProjektZaznamyTabViewModel.Filtry build) v `ProjectService.LazyQueries.cs` — přepíšeme ho do builderu.

Run: `grep -n "SubsystemyMoznosti\|KategorieMoznosti\|StavyUkoluMoznosti\|TypyUkoluMoznosti" PmTracker.Web/Services/ProjectService.LazyQueries.cs | head -30`
Expected: výskyty v `BuildProjectRecordsTabAsync` ukazující jak se options sestavují.

- [ ] **Step 2: Napiš failing test**

```csharp
// PmTracker.Tests.Unit/Services/Projekty/ProjectFilterShellViewModelBuilderTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Projekty;
using Xunit;

namespace PmTracker.Tests.Unit.Services.Projekty;

public sealed class ProjectFilterShellViewModelBuilderTests
{
    [Fact]
    public async Task BuildAsync_ProducesAllSixLookupCollections_ForGivenProject()
    {
        await using var db = InMemoryDb();
        SeedMinimalProject(db, projektId: 1);

        var sut = new ProjectFilterShellViewModelBuilder(db);
        var result = await sut.BuildAsync(projektId: 1, currentUserOsobaId: 99, scope: "records", CancellationToken.None);

        result.ProjektId.Should().Be(1);
        result.CurrentUserOsobaId.Should().Be(99);
        result.Scope.Should().Be("records");
        result.SubsystemyMoznosti.Should().NotBeNull();
        result.KategorieMoznosti.Should().NotBeNull();
        result.StavyUkoluMoznosti.Should().NotBeNull();
        result.TypyUkoluMoznosti.Should().NotBeNull();
        result.VlastniciMoznosti.Should().NotBeNull();
        result.StavyJednaniVyjadreni.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildAsync_ScheduleScope_ProducesIdenticalOptionsAsRecordsScope()
    {
        await using var db = InMemoryDb();
        SeedMinimalProject(db, projektId: 1);

        var sut = new ProjectFilterShellViewModelBuilder(db);
        var records = await sut.BuildAsync(1, 99, "records", CancellationToken.None);
        var schedule = await sut.BuildAsync(1, 99, "schedule", CancellationToken.None);

        // DRY: oba scopes mají identický set lookup options (jen Scope se liší).
        schedule.SubsystemyMoznosti.Select(x => x.Value).Should().BeEquivalentTo(records.SubsystemyMoznosti.Select(x => x.Value));
        schedule.KategorieMoznosti.Select(x => x.Value).Should().BeEquivalentTo(records.KategorieMoznosti.Select(x => x.Value));
        schedule.Scope.Should().Be("schedule");
        records.Scope.Should().Be("records");
    }

    private static PmTrackerDbContext InMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    private static void SeedMinimalProject(PmTrackerDbContext db, int projektId)
    {
        db.HotProjekty.Add(new HotProjektEntity { Id = projektId, Nazev = "Test", Zkratka = "T" });
        // Minimal seed — builder musí umět prázdný project (žádné záznamy), vrátí prázdné kolekce.
        db.SaveChanges();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectFilterShellViewModelBuilderTests" -c Release --nologo`
Expected: FAIL — `IProjectFilterShellViewModelBuilder` not defined.

- [ ] **Step 4: Vytvoř interface**

```csharp
// PmTracker.Web/Services/Projekty/IProjectFilterShellViewModelBuilder.cs
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Projekty;

/// <summary>
/// Buduje sdílený <see cref="ProjectFilterShellViewModel"/> pro Project Detail tab partials.
/// Volaný z <c>ProjectService.BuildProjectRecordsTabAsync</c> i
/// <c>ProjectService.BuildProjectScheduleTabAsync</c> — DRY: lookup options jsou
/// identické pro oba taby, jen <see cref="ProjectFilterShellViewModel.Scope"/>
/// se liší pro JS DOM disambiguation.
/// </summary>
public interface IProjectFilterShellViewModelBuilder
{
    Task<ProjectFilterShellViewModel> BuildAsync(int projektId, int currentUserOsobaId, string scope, CancellationToken ct);
}
```

- [ ] **Step 5: Vytvoř impl** (extrahuj logiku z ProjektFiltryViewModel buildu v `ProjectService.LazyQueries.cs`)

```csharp
// PmTracker.Web/Services/Projekty/ProjectFilterShellViewModelBuilder.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Projekty;

public sealed class ProjectFilterShellViewModelBuilder(PmTrackerDbContext db) : IProjectFilterShellViewModelBuilder
{
    public async Task<ProjectFilterShellViewModel> BuildAsync(int projektId, int currentUserOsobaId, string scope, CancellationToken ct)
    {
        // Subsystémy: aktivní projektové subsystémy (kód + název).
        var subsystemy = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projektId && !x.DatumOdebrani.HasValue)
            .OrderBy(x => x.SubsystemKod).ThenBy(x => x.SubsystemNazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.SubsystemKod) ? x.SubsystemNazev : x.SubsystemKod,
                Label = string.IsNullOrWhiteSpace(x.SubsystemKod) ? x.SubsystemNazev : $"{x.SubsystemKod} - {x.SubsystemNazev}"
            })
            .ToListAsync(ct);

        // Kategorie / stavy / typy / stavy jednání-vyjádření: globální číselníky.
        var kategorie = await db.CiselnikKategorii.AsNoTracking()
            .OrderBy(x => x.Poradi).ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToListAsync(ct);
        var stavy = await db.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Poradi).ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToListAsync(ct);
        var typy = await db.CiselnikTypuUkolu.AsNoTracking()
            .OrderBy(x => x.Poradi).ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToListAsync(ct);
        var stavyJV = await db.CiselnikStavuJednaniVyjadreni.AsNoTracking()
            .OrderBy(x => x.Poradi).ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToListAsync(ct);

        // Vlastníci: distinct osoby, které jsou aktuálním vlastníkem nějakého záznamu projektu.
        var vlastnici = await db.HotZaznamy.AsNoTracking()
            .Where(z => z.ProjektId == projektId && !z.DatumSmazani.HasValue && z.AktualniVlastnikId != null)
            .Select(z => new { Id = z.AktualniVlastnikId!.Value, z.AktualniVlastnik!.Jmeno, z.AktualniVlastnik!.Prijmeni })
            .Distinct()
            .ToListAsync(ct);
        var vlastniciOptions = vlastnici
            .Select(o => new LookupOptionViewModel
            {
                Value = o.Id.ToString(),
                Label = $"{o.Jmeno} {o.Prijmeni}".Trim()
            })
            .OrderBy(x => x.Label)
            .ToList();

        return new ProjectFilterShellViewModel
        {
            ProjektId = projektId,
            CurrentUserOsobaId = currentUserOsobaId,
            Scope = scope,
            SubsystemyMoznosti = subsystemy,
            KategorieMoznosti = kategorie,
            StavyUkoluMoznosti = stavy,
            TypyUkoluMoznosti = typy,
            VlastniciMoznosti = vlastniciOptions,
            StavyJednaniVyjadreni = stavyJV
        };
    }
}
```

**POZNÁMKA pro engineera:** Přesné názvy DbSet entit (`HotProjekty`, `ProjektSubsystemy`, `CiselnikKategorii`, atd.) ověř proti reálnému contextu příkazem:

```bash
grep -n "public DbSet<" PmTracker.Web/Data/PmTrackerDbContext.cs
```

Pokud Entity Framework property names jsou jiné (např. `db.Subsystemy` místo `db.CiselnikSubsystemu`), použij ty skutečné. Logika zůstává stejná. Stejně tak entity property names — `record.AktualniVlastnik.Jmeno` může být `record.AktualniVlastnik.OsobaJmeno` apod. — ověř `Models/Entities/`.

- [ ] **Step 6: Registruj v DI**

Najdi `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` — najdi sekci kde se registrují project services, přidej:

```csharp
services.AddScoped<IProjectFilterShellViewModelBuilder, ProjectFilterShellViewModelBuilder>();
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectFilterShellViewModelBuilderTests" -c Release --nologo`
Expected: PASS 2/2

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Services/Projekty/IProjectFilterShellViewModelBuilder.cs PmTracker.Web/Services/Projekty/ProjectFilterShellViewModelBuilder.cs PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs PmTracker.Tests.Unit/Services/Projekty/ProjectFilterShellViewModelBuilderTests.cs
git commit -m "feat(project-filter): IProjectFilterShellViewModelBuilder + impl + tests"
```

---

## Task 3: ProjektHarmonogramUkolViewModel — rozšíření o filter fields

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs:15-33`

- [ ] **Step 1: Doplnit pole na ProjektHarmonogramUkolViewModel**

V souboru `ProjektHarmonogramTabViewModels.cs` najdi `public sealed class ProjektHarmonogramUkolViewModel` a přidej pole **za existující `Vlastnik`**:

```csharp
public sealed class ProjektHarmonogramUkolViewModel
{
    public int ZaznamId { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public string? TypUkolu { get; init; }
    public string? TypUkoluKod { get; init; }   // nové
    public required string Stav { get; init; }
    public string? StavKod { get; init; }       // nové
    public required string SubsystemKod { get; init; }
    public required string Subsystem { get; init; }
    public int SubsystemPoradi { get; init; }
    public bool SubsystemHasProjectOrder { get; init; }
    public required string Vlastnik { get; init; }
    public int VlastnikOsobaId { get; init; }   // nové
    public required string KategorieKod { get; init; }    // nové
    public required string Kategorie { get; init; }       // nové
    public bool JeAktivni { get; init; } = true;          // nové
    public IReadOnlyList<string> JednaniVyjadreniStavyKody { get; init; } = Array.Empty<string>();  // nové
    public bool Stihame { get; init; }
    public HarmonogramBlockViewModel HarmonogramBlok { get; init; } = new();
    public bool CanManageSchedule { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? ScheduleEditUrl { get; set; }
    public string? ScheduleProposalUrl { get; set; }
}
```

- [ ] **Step 2: Build to verify compile**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: errors v `ProjectService.ScheduleComposition.cs` (musí poskytnout nová required pole). To je očekávané — řešíme v Task 5.

- [ ] **Step 3: Commit**

Commit zatím rozbitý build je OK podle DRY/incremental commit pattern, ale lepší je počkat na fix v Task 5. **Skip commit zde** — pokračuj rovnou na Task 4.

---

## Task 4: ProjektHarmonogramTabViewModel + ProjektZaznamyTabViewModel — FilterShell field

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs:7-13`
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs:3-19`

- [ ] **Step 1: Přidej FilterShell na ProjektHarmonogramTabViewModel**

V `ProjektHarmonogramTabViewModels.cs` najdi `public sealed class ProjektHarmonogramTabViewModel` a uprav:

```csharp
public sealed class ProjektHarmonogramTabViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<ProjektHarmonogramUkolViewModel> HarmonogramUkoly { get; init; } = Array.Empty<ProjektHarmonogramUkolViewModel>();

    /// <summary>
    /// Sdílený filter shell — identický s <see cref="ProjektZaznamyTabViewModel.FilterShell"/>.
    /// Render přes <c>_ProjectFilterShell.cshtml</c>. Spec: 2026-04-30-project-filter-unification-design.
    /// </summary>
    public required ProjectFilterShellViewModel FilterShell { get; init; }
}
```

**POZNÁMKA:** `SubsystemyMoznosti` na ProjektHarmonogramTabViewModel **smazat** — duplikuje `FilterShell.SubsystemyMoznosti`. Razor consumer použije `Model.FilterShell.SubsystemyMoznosti`.

- [ ] **Step 2: Přidej FilterShell na ProjektZaznamyTabViewModel**

V `ProjektZaznamyTabViewModels.cs` uprav `public sealed class ProjektZaznamyTabViewModel`:

```csharp
public sealed class ProjektZaznamyTabViewModel
{
    public int ProjektId { get; init; }
    public bool DleSubsystemu { get; init; }
    public IReadOnlyList<ProjektZaznamGroupViewModel> SkupinyZaznamu { get; init; } = Array.Empty<ProjektZaznamGroupViewModel>();
    public IReadOnlyList<ProjektZaznamCardShellViewModel> Zaznamy { get; init; } = Array.Empty<ProjektZaznamCardShellViewModel>();
    public required ProjectFilterShellViewModel FilterShell { get; init; }   // nahrazuje Filtry
    public int CurrentUserOsobaId { get; set; }
    public bool CanManageRecords { get; set; }
    public bool CanCreateRecordProposal { get; set; }
    public string? CreateRecordEditorUrl { get; set; }
    public string? CreateRecordProposalUrl { get; set; }
    public string? RefreshUrl { get; set; }
    public string? MeetingCommentStatesUrl { get; set; }
    public string? ProjectPrintUrl { get; set; }
    public string? ProjectWordUrl { get; set; }
}
```

**POZNÁMKA:** `Filtry` (typ `ProjektFiltryViewModel`) **smazat** — nahrazeno `FilterShell` (typ `ProjectFilterShellViewModel`).

Po odstranění reference v ViewModelu zkontroluj, zda class `ProjektFiltryViewModel` ještě někdo používá:

```bash
grep -rn "ProjektFiltryViewModel" PmTracker.Web PmTracker.Tests.Unit PmTracker.Web.Tests --include="*.cs" --include="*.cshtml"
```

Pokud žádné výskyty — smaž celou class definici (může být v `ProjektZaznamyTabViewModels.cs` nebo separátním souboru). Pokud ještě někdo reference (např. test), pojdme rozhodnout: buď migrate ho na `ProjectFilterShellViewModel`, nebo nech `ProjektFiltryViewModel` jako orphan. Preference: smazat (DRY).

- [ ] **Step 3: Build — očekávaně failne**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: errors v `_ProjectRecordsTab.cshtml`, `_ProjectScheduleTab.cshtml`, `ProjectService.LazyQueries.cs` (chybějící required FilterShell + reference na deprecated `Filtry` / `SubsystemyMoznosti`). Řešíme v Task 5+6+7.

- [ ] **Step 4: Skip commit, pokračuj na Task 5.**

---

## Task 5: ProjectService — wire builder do obou tab buildů + Schedule mapování

**Files:**
- Modify: `PmTracker.Web/Services/ProjectService.LazyQueries.cs:32-146`
- Modify: `PmTracker.Web/Services/ProjectService.ScheduleComposition.cs:9-`
- Modify: `PmTracker.Web/Services/ProjectService.cs` (nebo wherever je primary constructor / DI)

- [ ] **Step 1: Inject IProjectFilterShellViewModelBuilder do ProjectService**

Najdi primary constructor `ProjectService`. V `ProjectService.cs` (hlavní partial) přidej parametr:

```csharp
public sealed partial class ProjectService(
    PmTrackerDbContext dbContext,
    IHarmonogramService harmonogramService,
    // ... existing params ...
    IProjectFilterShellViewModelBuilder filterShellBuilder)
    : IProjectService, IProjectDetailComposition
{
    // ...
}
```

**POZNÁMKA:** Pokud `ProjectService` nepoužívá primary constructor, přidej do existujícího ctoru parametr a private readonly field. Sleduj existing pattern.

- [ ] **Step 2: Použij builder v BuildProjectRecordsTabAsync**

V `ProjectService.LazyQueries.cs` najdi `BuildProjectRecordsTabAsync`. Místo manuálního buildu `Filtry = new ProjektFiltryViewModel { ... }` použij:

```csharp
var filterShell = await filterShellBuilder.BuildAsync(id, currentUserOsobaId, "records", ct);

return new ProjektZaznamyTabViewModel
{
    ProjektId = id,
    DleSubsystemu = ...,  // existing
    SkupinyZaznamu = ...,  // existing
    Zaznamy = ...,  // existing
    FilterShell = filterShell,  // nové
    CurrentUserOsobaId = currentUserOsobaId,
    // ... ostatní existing fields ...
};
```

**POZNÁMKA:** Pokud `BuildProjectRecordsTabAsync` aktuálně nepřijímá `currentUserOsobaId` jako parametr, najdi ho v existujícím komponovaném VM. Filter shell ho potřebuje pro JS data atribut.

- [ ] **Step 3: Použij builder v BuildProjectScheduleTabAsync**

V tom samém souboru, najdi `BuildProjectScheduleTabAsync` (line ~125). Smazat manuální `subsystemOptions` block a nahradit:

```csharp
public async Task<ProjektHarmonogramTabViewModel> BuildProjectScheduleTabAsync(int id, CancellationToken ct = default)
{
    var scheduleRecords = await BuildScheduleRecordCardsForProjectAsync(id, ct);
    var harmonogramUkoly = await BuildProjectScheduleRowsAsync(scheduleRecords, ct);
    var currentUserOsobaId = userContext.CurrentOsobaId;  // adjust podle ProjectService API
    var filterShell = await filterShellBuilder.BuildAsync(id, currentUserOsobaId, "schedule", ct);

    return new ProjektHarmonogramTabViewModel
    {
        ProjektId = id,
        CurrentUserOsobaId = currentUserOsobaId,
        HarmonogramUkoly = harmonogramUkoly,
        FilterShell = filterShell
    };
}
```

- [ ] **Step 4: V ProjectService.ScheduleComposition.cs map nová pole z ZaznamCardViewModel**

Najdi `BuildProjectScheduleRowsAsync` (řádek ~9). V LINQ `.Select(record => new ProjektHarmonogramUkolViewModel { ... })` přidej mapování nových polí:

```csharp
return new ProjektHarmonogramUkolViewModel
{
    ZaznamId = record.Id,
    CisloViditelne = record.CisloViditelne,
    Nazev = record.Nazev,
    TypUkolu = record.TypUkolu,
    TypUkoluKod = record.TypUkoluKod,                     // nové
    Stav = record.Stav,
    StavKod = record.StavKod,                             // nové
    SubsystemKod = record.AktualniSubsystemKod,
    Subsystem = record.AktualniSubsystem,
    SubsystemPoradi = record.AktualniSubsystemPoradi,
    SubsystemHasProjectOrder = record.AktualniSubsystemHasProjectOrder,
    Vlastnik = ownerDisplay,
    VlastnikOsobaId = record.AktualniVlastnikId,          // nové
    KategorieKod = record.KategorieKod,                   // nové
    Kategorie = record.KategorieNazev,                    // nové
    JeAktivni = record.IsAktivniStav,                     // nové
    JednaniVyjadreniStavyKody = record.VyjadreniJednaniStavyKody,  // nové
    Stihame = souhrn.Stihame,
    HarmonogramBlok = ...,  // existing
    // ... existing ...
};
```

- [ ] **Step 5: Build to verify compile**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: errors už jen v Razor souborech (`_ProjectRecordsTab.cshtml`, `_ProjectScheduleTab.cshtml`) referencujících `Model.Filtry` / `Model.SubsystemyMoznosti`. Řeším v Task 7.

- [ ] **Step 6: Skip commit (Razor failures), pokračuj na Task 6.**

---

## Task 6: _ProjectFilterShell.cshtml — sdílený partial

**Files:**
- Create: `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml`
- Create: `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs`

- [ ] **Step 1: Napiš failing architecture test**

```csharp
// PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec: 2026-04-30-project-filter-unification-design.md.
/// Architecture invariants pro shared filter shell:
///   1) Existuje jediný partial _ProjectFilterShell.cshtml (žádné inline duplicate filter markup v tabech).
///   2) Partial obsahuje všech 10 filter polí (data-filter-key atributů).
///   3) Oba taby (_ProjectRecordsTab + _ProjectScheduleTab) volají PartialAsync na _ProjectFilterShell.
/// </summary>
public sealed class ProjectFilterShellTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Fact]
    public void Partial_ContainsAllTenFilterKeys()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml");

        var requiredKeys = new[]
        {
            "groupBySubsystem", "subsystem", "sortBy", "kategorie", "stav",
            "typ", "vlastnik", "aktivni", "mine", "jednani-vyjadreni-stav"
        };
        foreach (var key in requiredKeys)
        {
            var pattern = $@"data-filter-key=""{Regex.Escape(key)}""";
            Regex.IsMatch(src, pattern).Should().BeTrue($"_ProjectFilterShell musí obsahovat data-filter-key=\"{key}\"");
        }
    }

    [Fact]
    public void RecordsTab_DelegatesFilterMarkupToSharedPartial()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml");
        Regex.IsMatch(src, @"PartialAsync\(""_ProjectFilterShell""")
            .Should().BeTrue("_ProjectRecordsTab musí volat PartialAsync na shared filter shell");
    }

    [Fact]
    public void ScheduleTab_DelegatesFilterMarkupToSharedPartial()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml");
        Regex.IsMatch(src, @"PartialAsync\(""_ProjectFilterShell""")
            .Should().BeTrue("_ProjectScheduleTab musí volat PartialAsync na shared filter shell");
    }

    [Fact]
    public void ScheduleTab_DoesNotUseLegacyDataScheduleFilterKey()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml");
        Regex.IsMatch(src, @"data-schedule-filter-key")
            .Should().BeFalse("_ProjectScheduleTab nesmí použít legacy data-schedule-filter-key (sjednoceno na data-filter-key)");
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectFilterShellTests" -c Release --nologo`
Expected: FAIL — partial neexistuje.

- [ ] **Step 3: Vytvoř _ProjectFilterShell.cshtml**

```cshtml
@* Sdílený filter shell pro Project Detail (Záznamy + Harmonogram).
   Mountován z _ProjectRecordsTab.cshtml i _ProjectScheduleTab.cshtml — DRY.
   State sdílený přes JS (projectFilter.js, jeden localStorage klíč per projekt).
   Spec: docs/superpowers/specs/2026-04-30-project-filter-unification-design.md *@
@model ProjectFilterShellViewModel

<div class="filter-shell"
     data-project-filter-scope="@Model.Scope"
     data-project-id="@Model.ProjektId"
     data-current-user-id="@Model.CurrentUserOsobaId">
    <button class="filter-toggle" type="button" data-filter-toggle>
        Filtry
        <span class="filter-icon" aria-hidden="true">
            <gov-icon size="s" name="funnel" type="components" aria-hidden="true"></gov-icon>
        </span>
    </button>
    <div class="active-filter-row" data-filter-chip-row="@Model.Scope" hidden="hidden"></div>
    <div class="filter-panel collapsed" data-filter-panel>
        <div class="filter-grid">
            <gov-form-switch class="filter-inline-wide" size="m" data-filter-key="groupBySubsystem" checked>
                <span slot="label"><label>Seskupit dle subsystému</label></span>
            </gov-form-switch>
            <label>
                Subsystém
                <select data-filter-key="subsystem">
                    <option value="">Vše</option>
                    @foreach (var item in Model.SubsystemyMoznosti)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
            <label>
                Řadit podle
                <select data-filter-key="sortBy">
                    <option value="project-asc" selected="selected">podle pořadí projektu vzestupně</option>
                    <option value="project-desc">podle pořadí projektu sestupně</option>
                    <option value="alpha-asc">abecedně vzestupně (A-Z)</option>
                    <option value="alpha-desc">abecedně sestupně (Z-A)</option>
                </select>
            </label>
            <label>
                Kategorie
                <select data-filter-key="kategorie">
                    <option value="">Vše</option>
                    @foreach (var item in Model.KategorieMoznosti)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
            <label>
                Stav úkolu
                <select data-filter-key="stav">
                    <option value="">Vše</option>
                    @foreach (var item in Model.StavyUkoluMoznosti)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
            <label>
                Typ úkolu
                <select data-filter-key="typ">
                    <option value="">Vše</option>
                    @foreach (var item in Model.TypyUkoluMoznosti)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
            <label>
                Vlastník
                <select data-filter-key="vlastnik">
                    <option value="">Všichni</option>
                    @foreach (var item in Model.VlastniciMoznosti)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
            <gov-form-switch size="m" data-filter-key="aktivni" checked>
                <span slot="label"><label>Pouze aktivní záznamy</label></span>
            </gov-form-switch>
            <gov-form-switch size="m" data-filter-key="mine">
                <span slot="label"><label>Jen mé záznamy</label></span>
            </gov-form-switch>
            <label>
                Jednání-vyjádření
                <select data-filter-key="jednani-vyjadreni-stav">
                    <option value="">Vše</option>
                    @foreach (var item in Model.StavyJednaniVyjadreni)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
        </div>
        <div class="filter-actions">
            <pm-button variant="Ghost" size="Small" data-filter-save-defaults="@Model.Scope">Uložit jako výchozí</pm-button>
            <span class="filter-save-status" data-filter-save-status="@Model.Scope" aria-live="polite"></span>
        </div>
    </div>
</div>
```

- [ ] **Step 4: Skip commit — testy pro tab usage budou failnout dokud Task 7 neopravi taby. Pokračuj na Task 7.**

---

## Task 7: _ProjectRecordsTab + _ProjectScheduleTab — partial call + rozšířené data atributy

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml:47-144`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml:11-49,75-80`

- [ ] **Step 1: V _ProjectRecordsTab.cshtml nahraď inline filter markup partial call**

Najdi sekci `<div class="filter-shell" ... data-project-filter-scope="records" ...>` až po její closing `</div>` (řádky 47-144). Nahraď celé:

```cshtml
@await Html.PartialAsync("_ProjectFilterShell", Model.FilterShell)
```

- [ ] **Step 2: V _ProjectScheduleTab.cshtml nahraď inline filter markup**

Najdi sekci `<div class="filter-shell" ... data-project-filter-scope="schedule" ...>` až po její closing `</div>` (řádky 11-49). Nahraď:

```cshtml
@await Html.PartialAsync("_ProjectFilterShell", Model.FilterShell)
```

- [ ] **Step 3: V _ProjectScheduleTab.cshtml přidej rozšířené data-filter-* atributy na schedule cards**

Najdi `<article class="schedule-card" data-schedule-item ...>` (řádek ~75). Změň atributy ze `data-schedule-filter-*` na `data-filter-*` a přidej nové:

```cshtml
<article class="schedule-card"
         data-schedule-item
         data-filter-subsystem="@item.Subsystem"
         data-filter-subsystem-kod="@item.SubsystemKod"
         data-filter-kategorie="@item.Kategorie"
         data-filter-kategorie-kod="@item.KategorieKod"
         data-filter-stav="@item.Stav"
         data-filter-stav-kod="@(item.StavKod ?? string.Empty)"
         data-filter-typ="@(item.TypUkolu ?? string.Empty)"
         data-filter-typ-kod="@(item.TypUkoluKod ?? string.Empty)"
         data-filter-vlastnik="@item.Vlastnik"
         data-filter-vlastnik-id="@item.VlastnikOsobaId"
         data-filter-aktivni="@item.JeAktivni.ToString().ToLowerInvariant()"
         data-filter-jednani-vyjadreni-stav="@string.Join(",", item.JednaniVyjadreniStavyKody)"
         data-schedule-subsystem-order="@item.SubsystemPoradi"
         data-schedule-subsystem-order-active="@item.SubsystemHasProjectOrder.ToString().ToLowerInvariant()">
```

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: 0 errors, 0 warnings.

- [ ] **Step 5: Run architecture tests — verify pass**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectFilterShellTests" -c Release --nologo`
Expected: PASS 4/4.

- [ ] **Step 6: Run all unit tests — žádné regrese**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --nologo`
Expected: PASS všechny.

- [ ] **Step 7: Commit (T3-T7 jako jeden bundled commit, vše rozbité do tohoto bodu)**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs \
        PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs \
        PmTracker.Web/Services/ProjectService.LazyQueries.cs \
        PmTracker.Web/Services/ProjectService.ScheduleComposition.cs \
        PmTracker.Web/Services/ProjectService.cs \
        PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml \
        PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml \
        PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml \
        PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs
git commit -m "feat(project-filter): sdílený _ProjectFilterShell partial + Schedule data parity

Refactor 2 oddělených filter UI do jednoho Razor partialu mountovaného v obou
tabech (Záznamy + Harmonogram). ProjektHarmonogramUkolViewModel rozšířen o
KategorieKod, Kategorie, JeAktivni, VlastnikOsobaId, JednaniVyjadreniStavyKody
+ TypUkoluKod, StavKod (parity s _ZaznamPartial). Schedule cards nyní používají
sjednocené data-filter-* atributy (smazáno legacy data-schedule-filter-*).

Spec: docs/superpowers/specs/2026-04-30-project-filter-unification-design.md
Plan task: 3-7"
```

---

## Task 8: projectFilter.js — sjednocený config + nový localStorage klíč + migrace

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js`

- [ ] **Step 1: Sjednoť projectFilterConfigs do jednoho config**

V `projectFilter.js` (řádky ~35-66) najdi `const projectFilterConfigs = { records: { ... }, schedule: { ... } };` a nahraď:

```javascript
/**
 * Single config — DRY refactor 2026-04-30.
 * Records i Schedule používají stejných 10 fields. JS apply pipeline najde
 * scope dynamicky přes data-project-filter-scope na DOM elementu.
 * Spec: 2026-04-30-project-filter-unification-design.md
 */
const projectFilterFields = [
    { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
    { inputKey: "sortBy", stateKey: "sortBy", type: "select", skipChip: true },
    { inputKey: "kategorie", stateKey: "kategorie", type: "select", chipLabel: "Kategorie" },
    { inputKey: "stav", stateKey: "stav", type: "select", chipLabel: "Stav úkolu" },
    { inputKey: "typ", stateKey: "typ", type: "select", chipLabel: "Typ úkolu" },
    { inputKey: "vlastnik", stateKey: "vlastnik", type: "select", chipLabel: "Vlastník" },
    { inputKey: "aktivni", stateKey: "aktivni", type: "checkbox", chipLabel: "Pouze aktivní úkoly" },
    { inputKey: "mine", stateKey: "mine", type: "checkbox", chipLabel: "Jen mé záznamy" },
    { inputKey: "jednani-vyjadreni-stav", stateKey: "jednaniVyjadreniStav", type: "select", chipLabel: "Jednání-vyjádření" },
    { inputKey: "groupBySubsystem", stateKey: "groupBySubsystem", type: "checkbox", skipChip: true }
];

function getProjectFilterConfig(scope) {
    return {
        rootSelector: `[data-project-filter-scope="${scope}"]`,
        inputSelector: "[data-filter-key]",          // sjednoceno (původně data-schedule-filter-key pro schedule)
        keyAttribute: "data-filter-key",
        chipRowSelector: `[data-filter-chip-row="${scope}"]`,
        statusSelector: `[data-filter-save-status="${scope}"]`,
        fields: projectFilterFields
    };
}
```

**POZNÁMKA:** Existující export `getProjectFilterConfig` původně přijímal scope a vracel `projectFilterConfigs[scope]`. API zůstává stejné (signature compatible) — jen interně teď delsi config.

- [ ] **Step 2: Storage klíč — sjednocený + migrace**

Najdi function `getProjectFilterStorageKey(scope, projektId)`. Změň na **bez scope suffixu**:

```javascript
function getProjectFilterStorageKey(projektId) {
    return `${projectFilterStoragePrefix}${projektId}`;
}
```

**POZNÁMKA — breaking signature change:** Pokud někde v `projectFilter.js` (nebo jiných modulech) je volán `getProjectFilterStorageKey(scope, projektId)`, tato volání se musí upravit na `getProjectFilterStorageKey(projektId)`. Ověř:

```bash
grep -rn "getProjectFilterStorageKey" PmTracker.Web/wwwroot/js
```

Updatuj všechny call-sites na novou signature.

A přidej **jednorázovou migraci** ze starých klíčů. Přidej helper:

```javascript
/**
 * Jednorázová migrace 2026-04-30: starý storage měl scope suffix
 * (.records / .schedule). Nový má jen <projektId>. Při prvním načtení
 * po deploy: pokud existuje starý records klíč, přesune ho pod nový
 * a smaže starý + schedule klíč (schedule měl jen 2 pole, records ho přebije).
 */
function migrateLegacyProjectFilterStorage(projektId) {
    if (!window.localStorage) return;
    const newKey = getProjectFilterStorageKey(projektId);
    if (window.localStorage.getItem(newKey) !== null) {
        return;  // už migrováno
    }
    const legacyRecordsKey = `${projectFilterStoragePrefix}${projektId}.records`;
    const legacyScheduleKey = `${projectFilterStoragePrefix}${projektId}.schedule`;
    const legacyValue = window.localStorage.getItem(legacyRecordsKey);
    if (legacyValue !== null) {
        window.localStorage.setItem(newKey, legacyValue);
    }
    window.localStorage.removeItem(legacyRecordsKey);
    window.localStorage.removeItem(legacyScheduleKey);
}
```

V `restoreProjectFilterScope(scope)` (existující exported funkce) zavolej `migrateLegacyProjectFilterStorage(projektId)` PŘED čtením storage.

- [ ] **Step 3: Smaž references na data-schedule-filter-* v projectFilter.js**

V souboru najdi všechny výskyty `data-schedule-filter` (CSS selectory, attribute lookups) a odstraň. Code by měl pracovat výhradně s `data-filter-*` atributy.

Run: `grep -n "data-schedule-filter\|schedule-filter-key" PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js`
Expected: 0 výskytů po editaci.

- [ ] **Step 4: Architecture test pro migration funkci**

Vytvoř / rozšiř `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs`:

```csharp
[Fact]
public void ProjectFilterJs_ContainsLegacyStorageMigration()
{
    var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
    src.Should().Contain("migrateLegacyProjectFilterStorage",
        "projectFilter.js musí obsahovat one-time migration helper pro legacy .records/.schedule storage keys");
}

[Fact]
public void ProjectFilterJs_DoesNotUseScopeSuffixedStorageKeys()
{
    var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
    // Po refactoru smí .records/.schedule string literál existovat JEN v migration funkci
    // (aby smazala legacy klíče). Production code path je bez suffixu.
    var occurrences = Regex.Matches(src, @"""\.records""|""\.schedule""").Count;
    occurrences.Should().BeLessThanOrEqualTo(2,
        "scope-suffixed storage keys smí existovat pouze v migration helperu (max 2 string literály)");
}

[Fact]
public void ProjectFilterJs_DoesNotReferenceDataScheduleFilterKey()
{
    var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
    src.Should().NotContain("data-schedule-filter",
        "projectFilter.js musí používat jen data-filter-* (sjednoceno 2026-04-30)");
}
```

- [ ] **Step 5: Build solution + run tests**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: 0 errors.

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectFilterShellTests|FullyQualifiedName~JsBundleImport" -c Release --nologo`
Expected: PASS všechny.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs
git commit -m "refactor(project-filter): sjednocený config + jeden localStorage klíč + migrace

Smazány .records/.schedule rozdělení v projectFilterConfigs (10 fields společných).
Storage klíč nyní jen pmtracker.projectFilters.v1.project.<id> (bez scope suffixu).
Jednorázová migrace ze starých scope-suffixed klíčů při prvním restore.
Smazány references na data-schedule-filter-* (sjednoceno na data-filter-*).

Plan task: 8"
```

---

## Task 9: filters/index.js — pm-tab-change listener

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/filters/index.js`

- [ ] **Step 1: Najdi entry point filters/index.js a doplň pm-tab-change listener**

Otevři `PmTracker.Web/wwwroot/js/modules/filters/index.js`. Najdi `init()` nebo equivalent function. Přidej:

```javascript
/**
 * Spec 2026-04-30: pm-tab-change event z pm-tabs Web Component → re-apply
 * sdíleného filter state na nově viditelný scope (records ↔ schedule).
 * Bez tohoto listeneru by user změna filtru v Záznamy nepropagovala na
 * Harmonogram po tab switch.
 */
function initProjectFilterTabSync() {
    document.addEventListener("pm-tab-change", (event) => {
        const detail = event.detail;
        if (!detail || typeof detail.key !== "string") return;
        // Map pm-tab key na project filter scope. Aktuálně používáme "zaznamy" → "records",
        // "harmonogram" → "schedule". Ostatní taby filter shell nemají, no-op.
        const scopeMap = { zaznamy: "records", harmonogram: "schedule" };
        const newScope = scopeMap[detail.key];
        if (!newScope) return;
        // Re-apply existing state na nově visible tab DOM.
        restoreProjectFilterScope(newScope);
    });
}
```

A v hlavní `init()` exportu zavolat `initProjectFilterTabSync()`.

**POZNÁMKA pro engineera:** Najdi přesný název hlavní init funkce v `index.js` (mohl by být `initProjectFilters`, `init`, atd.). Přidej `initProjectFilterTabSync()` na konec.

- [ ] **Step 2: Smoke check že restoreProjectFilterScope je importován v index.js**

Run: `grep -n "restoreProjectFilterScope\|projectFilter" PmTracker.Web/wwwroot/js/modules/filters/index.js`
Expected: import `restoreProjectFilterScope` z `./projectFilter.js`.

Pokud chybí, přidej:
```javascript
import { restoreProjectFilterScope } from "./projectFilter.js";
```

- [ ] **Step 3: Build + JS bundle test**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: 0 errors.

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~JsBundleImport" -c Release --nologo`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/filters/index.js
git commit -m "feat(project-filter): pm-tab-change listener pro cross-tab state propagaci

Sdílený filter state se re-applies při tab switch (zaznamy ↔ harmonogram)
přes pm-tab-change event z pm-tabs Web Component.

Plan task: 9"
```

---

## Task 10: bootstrap.js — cleanup data-schedule-filter-* handlers

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js:315-318,587-625`

- [ ] **Step 1: Najdi a smaž handlery pro data-schedule-filter-toggle / -panel / -key**

Najdi blok kolem řádku 315:

```javascript
const scheduleFilterToggle = target.closest("[data-schedule-filter-toggle]");
if (scheduleFilterToggle) {
    const filterPanel = document.querySelector("[data-schedule-filter-panel]");
    // ...
}
```

A blok kolem řádku 587-625:
```javascript
const scheduleFilterInput = target.closest("[data-schedule-filter-key]");
if (scheduleFilterInput instanceof ...) {
    persistScheduleFilterState(scheduleFilterInput);
}
```

**Smaž celé tyto bloky.** Sjednocený filter používá `data-filter-toggle` + `data-filter-key` (existující handlers v bootstrap.js).

- [ ] **Step 2: Smaž import `persistScheduleFilterState` (pokud existuje a není už nikde použit)**

Run: `grep -n "persistScheduleFilterState" PmTracker.Web/wwwroot/js/modules/`
Expected po cleanup: 0 výskytů (kromě možné definice v projectFilter.js — pokud existuje, smaž ji tam).

- [ ] **Step 3: Build + run all tests**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: 0 errors, 0 warnings.

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --nologo`
Expected: PASS všechny.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/bootstrap.js PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js
git commit -m "refactor(project-filter): smazat legacy data-schedule-filter-* handlers

Po sjednocení na data-filter-* (Task 7+8) jsou bootstrap.js bloky pro
data-schedule-filter-toggle / -key dead code. Smazáno + smazána exported
funkce persistScheduleFilterState (sjednocena s persistFilterState).

Plan task: 10"
```

---

## Task 11: Final verification + publish

**Files:**
- N/A (verification only)

- [ ] **Step 1: Full build (Release, 0 warnings)**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release -nologo`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 2: Full unit test suite**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj -c Release --nologo`
Expected: 100% PASS.

- [ ] **Step 3: Web tests**

Run: `dotnet test PmTracker.Web.Tests/PmTracker.Web.Tests.csproj -c Release --nologo`
Expected: 100% PASS.

- [ ] **Step 4: Publish + zip (per memory rule "publish vždy zabalit do zipu")**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
rm -rf publish
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o publish --nologo
rm -f publish.zip
(cd publish && zip -qr ../publish.zip .)
ls -lh publish.zip
```
Expected: `publish.zip` ~12 MB.

- [ ] **Step 5: Manual smoke test plan (pro deploy review)**

Doporučený manuální flow po deploy (nelze otestovat lokálně bez SQL):

1. Otevři `/projekty/<id>` → tab Záznamy.
2. V filter shell nastav: subsystem=X, kategorie=Y, stav=Z.
3. Klikni na tab Harmonogram. Ověř:
   - Filter shell tam je s identickým layoutem (10 fields).
   - Filtry jsou aktivní (subsystem=X, kategorie=Y, stav=Z předvyplněné).
   - Schedule cards jsou filtrovány podle subsystem + kategorie + stav.
4. Změň filtr v Harmonogram (např. typ=A). Klik na Záznamy → ověř že typ=A je aktivní i tam.
5. Reload stránky (F5) → filter restored.
6. DevTools → Application → localStorage → ověř že existuje pouze klíč `pmtracker.projectFilters.v1.project.<id>` (bez `.records` / `.schedule` suffixu).
7. Zatestuj migraci: před deploy ručně v devtools nastav `localStorage.setItem('pmtracker.projectFilters.v1.project.<id>.records', '{"subsystem":"X"}')`. Po deploy + reload → starý klíč pryč, nový obsahuje hodnoty.

- [ ] **Step 6: Commit publish.zip update (volitelné — zip není v gitu)**

publish.zip není v git tracked. Skip commit, jen ověř že je dostupný v repo root.

---

## Acceptance criteria (z spec, verify final)

1. ✅ Filter shell renderuje 10 fields v Záznamy I Harmonogram (markup identical, jeden zdrojový partial) — verify Task 6+7.
2. ✅ State sdílený: změna v Records → switch tab → Harmonogram má stejný stav, cards filtrované identicky — verify Task 9 + manual smoke.
3. ✅ localStorage: jeden klíč `pmtracker.projectFilters.v1.project.<id>`, žádné `.records` / `.schedule` suffixy v novém kódu — verify Task 8.
4. ✅ Migrace: existujícím userům s `<id>.records` se hodnoty přenesou do nového klíče při prvním návštěvě po deploy — verify Task 8 + manual smoke step 7.
5. ✅ Unit testy + architecture testy passed — verify Task 11 step 2-3.
6. ✅ Build 0 warnings + dotnet test full suite green — verify Task 11 step 1-3.
