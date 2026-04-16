# Projektový Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a project-level analytics dashboard accessible from project detail, showing delayed tasks, annual KPIs, and placeholder tabs for future ServiceDesk integration.

**Architecture:** New `ProjectDashboardController` + `IProjectDashboardService`/`ProjectDashboardService` with views in `Views/ProjectDashboard/`. Authorization checks user's project roles (PROJ_MAN, ADM_PROJ, GEST) via DB query against `obsazeni_projektu`. Lazy-loaded tab panels via AJAX, same pattern as existing user dashboard.

**Tech Stack:** ASP.NET Core MVC, EF Core, Dapper (where existing), FluentAssertions, xUnit

**Spec:** `docs/superpowers/specs/2026-04-16-projektovy-dashboard-design.md`

**Out of scope for this plan (Phase 2 — ServiceDesk integration):**
- DB columns `ServiceDeskInfoSystemId` and `RozpocetCelkem` on `projekty` table — not needed until ServiceDesk integration
- NES panel data, Výzvy panel data, ServiceDesk metrics in Statistics — all placeholder-only in this phase
- Export to Excel on NES tab

**Implementation notes:**
- Task 5 `ComputeYearKpi` uses a simplified approach — the implementer must match the actual entity types from LINQ queries and may need to refine the KPI computation once actual data structures are understood
- All entity property names should be verified against `PmTrackerEntities.cs` during implementation
- Study `UcastEntity` carefully — attendance tracking may use `StavUcastiId` (FK to lookup) rather than `StavUcastiKod` string

---

## Task 1: Add GEST Role Constant and Startup Validation

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/RoleCatalogKeys.cs:5-9`
- Modify: `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs:11`
- Modify: `PmTracker.Web/Services/Common/ProjectRolePermissionGrantBuilder.cs:13-17`

- [ ] **Step 1: Add Gestor constant to ProjectRoleCodes**

In `PmTracker.Web/Models/ViewModels/RoleCatalogKeys.cs`, add the constant:

```csharp
public static class ProjectRoleCodes
{
    public const string ProjectOwner = "VLASTNIK_PROJEKTU";
    public const string Host = "HOST";
    public const string ProjectAdmin = "ADM_PROJ";
    public const string ProjectManager = "PROJ_MAN";
    public const string Gestor = "GEST";
}
```

- [ ] **Step 2: Add GEST to startup validation**

In `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs`, update the array:

```csharp
private static readonly string[] RequiredProjectRoleCodes = { ProjectRoleCodes.ProjectOwner, ProjectRoleCodes.Host, ProjectRoleCodes.ProjectAdmin, ProjectRoleCodes.ProjectManager, ProjectRoleCodes.Gestor };
```

- [ ] **Step 3: Build and verify no compilation errors**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/RoleCatalogKeys.cs PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs
git commit -m "feat: add GEST (Gestor FIS) project role constant and startup validation"
```

---

## Task 2: Dashboard Authorization Helper

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardAuthorizationPolicy.cs`
- Test: `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardAuthorizationPolicyTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardAuthorizationPolicyTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;

namespace PmTracker.Tests.Unit.ProjectDashboard;

public sealed class ProjectDashboardAuthorizationPolicyTests
{
    [Theory]
    [InlineData("PROJ_MAN")]
    [InlineData("ADM_PROJ")]
    [InlineData("GEST")]
    public void HasDashboardAccess_ShouldReturnTrue_ForAllowedRoleCodes(string roleCode)
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess([roleCode])
            .Should().BeTrue();
    }

    [Fact]
    public void HasDashboardAccess_ShouldReturnFalse_ForUnrelatedRole()
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess(["VLASTNIK_PROJEKTU", "HOST"])
            .Should().BeFalse();
    }

    [Fact]
    public void HasDashboardAccess_ShouldReturnFalse_ForEmptyRoles()
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess([])
            .Should().BeFalse();
    }

    [Fact]
    public void HasDashboardAccess_ShouldBeCaseInsensitive()
    {
        ProjectDashboardAuthorizationPolicy.HasDashboardAccess(["proj_man"])
            .Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboardAuthorizationPolicyTests" --no-restore`
Expected: FAIL — class does not exist

- [ ] **Step 3: Implement the authorization policy**

Create `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardAuthorizationPolicy.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public static class ProjectDashboardAuthorizationPolicy
{
    private static readonly HashSet<string> DashboardRoleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectRoleCodes.ProjectManager,
        ProjectRoleCodes.ProjectAdmin,
        ProjectRoleCodes.Gestor
    };

    public static bool HasDashboardAccess(IReadOnlyList<string> activeProjectRoleCodes)
    {
        return activeProjectRoleCodes.Any(code => DashboardRoleCodes.Contains(code));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboardAuthorizationPolicyTests" --no-restore`
Expected: All 4 tests PASS

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/ProjectDashboardAuthorizationPolicy.cs PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardAuthorizationPolicyTests.cs
git commit -m "feat: add ProjectDashboardAuthorizationPolicy with role-based access check"
```

---

## Task 3: Dashboard ViewModels

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs`

- [ ] **Step 1: Create the ViewModel file**

Create `PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjectDashboardPageViewModel : BaseViewModel
{
    public required ProjektHeaderViewModel Projekt { get; init; }
    public string ActiveTab { get; set; } = "zaznamy";
    public required string RecordsPanelUrl { get; init; }
    public required string NesPanelUrl { get; init; }
    public required string StatisticsPanelUrl { get; init; }
    public required string VyzvyPanelUrl { get; init; }
    public required string BackUrl { get; init; }
}

public enum DashboardRecordCategory
{
    Delayed,
    AwaitingActual,
    ApproachingDeadline
}

public sealed class ProjectDashboardRecordsPanelViewModel
{
    public IReadOnlyList<ProjectDashboardRecordRowViewModel> Records { get; init; } = [];
    public bool IsEmpty => Records.Count == 0;
}

public sealed class ProjectDashboardRecordRowViewModel
{
    public int RecordId { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public required string Subsystem { get; init; }
    public required string SubsystemKod { get; init; }
    public required string Vlastnik { get; init; }
    public DateTime TerminUkonceni { get; init; }
    public DashboardRecordCategory Category { get; init; }
    public int WorstOffsetDays { get; init; }
    public IReadOnlyList<ProjectDashboardStepDetailViewModel> ProblematicSteps { get; init; } = [];
    public required string ScheduleUrl { get; init; }
}

public sealed class ProjectDashboardStepDetailViewModel
{
    public required string StepName { get; init; }
    public DateTime PlannedDate { get; init; }
    public DateTime? ActualDate { get; init; }
    public int OffsetDays { get; init; }
    public int DaysSincePlanExpired { get; init; }
}

public sealed class ProjectDashboardNesPanelViewModel
{
    public bool IsServiceDeskIntegrated { get; init; }
    public string PlaceholderMessage { get; init; } = "Napojení na ServiceDesk není k dispozici.";
}

public sealed class ProjectDashboardStatisticsPanelViewModel
{
    public int SelectedYear { get; init; }
    public IReadOnlyList<int> AvailableYears { get; init; } = [];
    public ProjectDashboardKpiViewModel Kpi { get; init; } = new();
    public IReadOnlyList<ProjectDashboardQuarterViewModel> Quarters { get; init; } = [];
    public IReadOnlyList<ProjectDashboardSubsystemStatsViewModel> SubsystemStats { get; init; } = [];
    public double? AttendanceRate { get; init; }
    public bool IsServiceDeskIntegrated { get; init; }
    public bool HasData => Kpi.Splneno > 0 || Kpi.Zruseno > 0 || Kpi.Preneseno > 0 || Kpi.VProdleni > 0;
}

public sealed class ProjectDashboardKpiViewModel
{
    public int Splneno { get; init; }
    public int Zruseno { get; init; }
    public int Preneseno { get; init; }
    public double VcasnostPlneniPct { get; init; }
    public int VProdleni { get; init; }
    public double PrumerneProdleniDni { get; init; }
    public int Prodlouzeno { get; init; }

    public int? SplnenoPrevYear { get; init; }
    public int? ZrusenoPrevYear { get; init; }
    public int? PrenesenoPrevYear { get; init; }
    public double? VcasnostPlneniPctPrevYear { get; init; }
    public int? VProdleniPrevYear { get; init; }
    public double? PrumerneProdleniDniPrevYear { get; init; }
    public int? ProdlouzenoPrevYear { get; init; }
}

public sealed class ProjectDashboardQuarterViewModel
{
    public int Quarter { get; init; }
    public int PlannedCompletions { get; init; }
    public int ActualCompletions { get; init; }
}

public sealed class ProjectDashboardSubsystemStatsViewModel
{
    public required string SubsystemKod { get; init; }
    public required string SubsystemNazev { get; init; }
    public int Splneno { get; init; }
    public int VProdleni { get; init; }
    public int Zruseno { get; init; }
    public int Prodlouzeno { get; init; }
}

public sealed class ProjectDashboardVyzvyPanelViewModel
{
    public bool IsServiceDeskIntegrated { get; init; }
    public string PlaceholderMessage { get; init; } = "Žádné výzvy. Generování výzev vyžaduje napojení na ServiceDesk.";
}
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs
git commit -m "feat: add ProjectDashboard ViewModels"
```

---

## Task 4: Dashboard Service Interface and Records Logic

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/IProjectDashboardService.cs`
- Create: `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs`
- Test: `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardRecordCategorizationTests.cs`

- [ ] **Step 1: Create the service interface**

Create `PmTracker.Web/Services/ProjectDashboard/IProjectDashboardService.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public interface IProjectDashboardService
{
    Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default);
    Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default);
    Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default);
    ProjectDashboardNesPanelViewModel BuildNesPanel();
    ProjectDashboardVyzvyPanelViewModel BuildVyzvyPanel();
    Task<bool> CanAccessDashboardAsync(int projectId, int osobaId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the categorization tests**

Create `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardRecordCategorizationTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;

namespace PmTracker.Tests.Unit.ProjectDashboard;

public sealed class ProjectDashboardRecordCategorizationTests
{
    private static readonly DateTime ReferenceDate = new(2026, 4, 16);

    [Fact]
    public void ShouldCategorize_AsDelayed_WhenStepHasPositiveOffset()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1,
            Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 4, 1),
            ActualEndDate: new DateTime(2026, 4, 8),
            DurationDays: 5,
            OffsetDays: 7);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().Be(DashboardRecordCategory.Delayed);
    }

    [Fact]
    public void ShouldCategorize_AsAwaitingActual_WhenPlanExpiredAndActualIsZero()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1,
            Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 4, 1),
            ActualEndDate: new DateTime(2026, 4, 1),
            DurationDays: 5,
            OffsetDays: 0);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().Be(DashboardRecordCategory.AwaitingActual);
    }

    [Fact]
    public void ShouldCategorize_AsApproaching_WhenPlanWithinThreshold()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1,
            Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 4, 20),
            ActualEndDate: new DateTime(2026, 4, 20),
            DurationDays: 5,
            OffsetDays: 0);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().Be(DashboardRecordCategory.ApproachingDeadline);
    }

    [Fact]
    public void ShouldReturnNull_WhenStepIsOnTrack()
    {
        var step = new ScheduleStepSnapshot(
            StepIndex: 1,
            Name: "Dodávka",
            PlanEndDate: new DateTime(2026, 5, 1),
            ActualEndDate: new DateTime(2026, 5, 1),
            DurationDays: 5,
            OffsetDays: 0);

        var result = DashboardRecordCategorizer.CategorizeStep(step, ReferenceDate, approachingThresholdDays: 7);

        result.Should().BeNull();
    }

    [Fact]
    public void ShouldPickWorstCategory_WhenMultipleStepsAreProblematic()
    {
        var steps = new[]
        {
            new ScheduleStepSnapshot(1, "Krok1", new DateTime(2026, 4, 20), new DateTime(2026, 4, 20), 5, 0),
            new ScheduleStepSnapshot(2, "Krok2", new DateTime(2026, 4, 1), new DateTime(2026, 4, 8), 5, 7)
        };

        var result = DashboardRecordCategorizer.CategorizeRecord(steps, ReferenceDate, approachingThresholdDays: 7);

        result.Category.Should().Be(DashboardRecordCategory.Delayed);
        result.WorstOffsetDays.Should().Be(7);
    }

    [Fact]
    public void ShouldSortRecords_ByWorstOffsetDescending()
    {
        var records = new List<(DashboardRecordCategory Category, int WorstOffset)>
        {
            (DashboardRecordCategory.ApproachingDeadline, 3),
            (DashboardRecordCategory.Delayed, 15),
            (DashboardRecordCategory.AwaitingActual, 10),
            (DashboardRecordCategory.Delayed, 5)
        };

        var sorted = records.OrderBy(DashboardRecordCategorizer.SortKey).ToList();

        sorted[0].WorstOffset.Should().Be(15);
        sorted[1].WorstOffset.Should().Be(10);
        sorted[2].WorstOffset.Should().Be(5);
        sorted[3].WorstOffset.Should().Be(3);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboardRecordCategorizationTests" --no-restore`
Expected: FAIL — types do not exist

- [ ] **Step 4: Implement the categorizer**

Add to the bottom of `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardAuthorizationPolicy.cs` or create `PmTracker.Web/Services/ProjectDashboard/DashboardRecordCategorizer.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed record ScheduleStepSnapshot(
    int StepIndex,
    string Name,
    DateTime PlanEndDate,
    DateTime ActualEndDate,
    int DurationDays,
    int OffsetDays);

public sealed record RecordCategorizationResult(
    DashboardRecordCategory Category,
    int WorstOffsetDays,
    IReadOnlyList<(ScheduleStepSnapshot Step, DashboardRecordCategory StepCategory)> ProblematicSteps);

public static class DashboardRecordCategorizer
{
    public const int DefaultApproachingThresholdDays = 7;

    public static DashboardRecordCategory? CategorizeStep(ScheduleStepSnapshot step, DateTime referenceDate, int approachingThresholdDays)
    {
        if (step.OffsetDays > 0)
        {
            return DashboardRecordCategory.Delayed;
        }

        if (step.PlanEndDate < referenceDate && step.OffsetDays == 0 && step.ActualEndDate <= step.PlanEndDate)
        {
            return DashboardRecordCategory.AwaitingActual;
        }

        var daysUntilPlan = (step.PlanEndDate - referenceDate).Days;
        if (daysUntilPlan >= 0 && daysUntilPlan <= approachingThresholdDays && step.OffsetDays == 0)
        {
            return DashboardRecordCategory.ApproachingDeadline;
        }

        return null;
    }

    public static RecordCategorizationResult CategorizeRecord(
        IReadOnlyList<ScheduleStepSnapshot> steps,
        DateTime referenceDate,
        int approachingThresholdDays)
    {
        var problematic = new List<(ScheduleStepSnapshot Step, DashboardRecordCategory StepCategory)>();
        var worstCategory = (DashboardRecordCategory?)null;
        var worstOffset = 0;

        foreach (var step in steps)
        {
            var cat = CategorizeStep(step, referenceDate, approachingThresholdDays);
            if (cat is null)
            {
                continue;
            }

            problematic.Add((step, cat.Value));

            var effectiveOffset = cat.Value switch
            {
                DashboardRecordCategory.Delayed => step.OffsetDays,
                DashboardRecordCategory.AwaitingActual => (referenceDate - step.PlanEndDate).Days,
                DashboardRecordCategory.ApproachingDeadline => (step.PlanEndDate - referenceDate).Days,
                _ => 0
            };

            if (worstCategory is null || CategorySeverity(cat.Value) < CategorySeverity(worstCategory.Value)
                || (cat.Value == worstCategory.Value && effectiveOffset > worstOffset))
            {
                worstCategory = cat.Value;
                worstOffset = effectiveOffset;
            }
        }

        return new RecordCategorizationResult(
            worstCategory ?? DashboardRecordCategory.ApproachingDeadline,
            worstOffset,
            problematic);
    }

    public static (int Severity, int NegatedOffset) SortKey((DashboardRecordCategory Category, int WorstOffset) record)
    {
        return (CategorySeverity(record.Category), -record.WorstOffset);
    }

    private static int CategorySeverity(DashboardRecordCategory category) => category switch
    {
        DashboardRecordCategory.Delayed => 0,
        DashboardRecordCategory.AwaitingActual => 1,
        DashboardRecordCategory.ApproachingDeadline => 2,
        _ => 3
    };
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboardRecordCategorizationTests" --no-restore`
Expected: All 6 tests PASS

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/ PmTracker.Tests.Unit/ProjectDashboard/
git commit -m "feat: add DashboardRecordCategorizer with step/record categorization logic"
```

---

## Task 5: ProjectDashboardService Implementation

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs`
- Modify: `PmTracker.Web/Program.cs` or DI registration file (add service registration)

- [ ] **Step 1: Implement the service**

Create `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed class ProjectDashboardService : IProjectDashboardService
{
    private readonly PmTrackerDbContext _dbContext;

    public ProjectDashboardService(PmTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default)
    {
        var projekt = await _dbContext.Projekty.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Join(_dbContext.CiselnikStavuProjektu.AsNoTracking(), p => p.StavId, s => s.Id, (p, s) => new { p, s })
            .Select(x => new ProjektHeaderViewModel
            {
                Id = x.p.Id,
                Nazev = x.p.CelyNazev,
                Zkratka = x.p.Zkratka,
                Stav = x.s.Nazev
            })
            .FirstAsync(ct);

        return new ProjectDashboardPageViewModel
        {
            Projekt = projekt,
            RecordsPanelUrl = $"/projekty/{projectId}/dashboard/records-panel",
            NesPanelUrl = $"/projekty/{projectId}/dashboard/nes-panel",
            StatisticsPanelUrl = $"/projekty/{projectId}/dashboard/statistics-panel",
            VyzvyPanelUrl = $"/projekty/{projectId}/dashboard/vyzvy-panel",
            BackUrl = $"/Projekty/Detail/{projectId}"
        };
    }

    public async Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default)
    {
        var taskCategoryIds = await _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(c => c.Kod == RecordCategoryCodes.TaskShort || c.Kod == RecordCategoryCodes.Task)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (taskCategoryIds.Count == 0)
        {
            return new ProjectDashboardRecordsPanelViewModel();
        }

        var finalStateIds = await _dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(s => s.IsFinal)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var records = await (
                from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                join category in _dbContext.CiselnikKategoriiZaznamu.AsNoTracking() on record.KategorieId equals category.Id
                join subsystem in _dbContext.Subsystemy.AsNoTracking() on record.SubsystemId equals subsystem.Id
                join owner in _dbContext.Osoby.AsNoTracking() on record.VlastnikId equals owner.Id
                where record.ProjektId == projectId
                    && taskCategoryIds.Contains(record.KategorieId)
                    && (!record.StavUkoluId.HasValue || !finalStateIds.Contains(record.StavUkoluId.Value))
                select new
                {
                    record.Id,
                    record.CisloViditelne,
                    record.Nazev,
                    record.DatumZalozeni,
                    record.DatumUkonceni,
                    record.HarmonogramSablonaVerze,
                    SubsystemKod = subsystem.Kod,
                    SubsystemNazev = subsystem.Nazev,
                    VlastnikJmeno = owner.Jmeno + " " + owner.Prijmeni
                })
            .ToListAsync(ct);

        if (records.Count == 0)
        {
            return new ProjectDashboardRecordsPanelViewModel();
        }

        var recordIds = records.Select(r => r.Id).ToList();
        var harmonogramValues = await _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId))
            .ToListAsync(ct);

        var harmonogramTypes = await _dbContext.HarmonogramTypy.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, ct);

        var sablonaSteps = await _dbContext.HarmonogramSablony.AsNoTracking()
            .ToListAsync(ct);

        var valuesByRecord = harmonogramValues
            .GroupBy(v => v.ZaznamId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(v => v.TypId, v => v.HodnotaInt));

        var dashboardRecords = new List<ProjectDashboardRecordRowViewModel>();

        foreach (var record in records)
        {
            if (!valuesByRecord.TryGetValue(record.Id, out var values) || values.Count == 0)
            {
                continue;
            }

            var stepDefs = sablonaSteps
                .Where(s => s.Verze == record.HarmonogramSablonaVerze)
                .OrderBy(s => s.KrokIndex)
                .Select(s => new ScheduleTimelineStepDefinition
                {
                    StepIndex = s.KrokIndex,
                    Code = s.Kod,
                    Name = s.Nazev,
                    ColorHex = s.BarvaHex,
                    DurationTypeId = s.TrvaniTypId,
                    OffsetTypeId = s.ZpozdeniTypId
                })
                .ToList();

            if (stepDefs.Count == 0)
            {
                continue;
            }

            var computation = ScheduleTimelineCalculator.Compute(record.DatumZalozeni, stepDefs, values);

            var snapshots = computation.Steps.Select(s => new ScheduleStepSnapshot(
                s.StepIndex,
                stepDefs.FirstOrDefault(d => d.StepIndex == s.StepIndex)?.Name ?? $"Krok {s.StepIndex}",
                s.PlanEndDate,
                s.ActualEndDate,
                s.DurationDays,
                s.OffsetDays)).ToList();

            var categorization = DashboardRecordCategorizer.CategorizeRecord(
                snapshots, referenceDate, DashboardRecordCategorizer.DefaultApproachingThresholdDays);

            if (categorization.ProblematicSteps.Count == 0)
            {
                continue;
            }

            dashboardRecords.Add(new ProjectDashboardRecordRowViewModel
            {
                RecordId = record.Id,
                CisloViditelne = record.CisloViditelne ?? record.Id.ToString(),
                Nazev = record.Nazev,
                Subsystem = record.SubsystemNazev,
                SubsystemKod = record.SubsystemKod,
                Vlastnik = record.VlastnikJmeno,
                TerminUkonceni = record.DatumUkonceni,
                Category = categorization.Category,
                WorstOffsetDays = categorization.WorstOffsetDays,
                ProblematicSteps = categorization.ProblematicSteps.Select(ps => new ProjectDashboardStepDetailViewModel
                {
                    StepName = ps.Step.Name,
                    PlannedDate = ps.Step.PlanEndDate,
                    ActualDate = ps.StepCategory == DashboardRecordCategory.AwaitingActual ? null : ps.Step.ActualEndDate,
                    OffsetDays = ps.Step.OffsetDays,
                    DaysSincePlanExpired = ps.StepCategory == DashboardRecordCategory.AwaitingActual
                        ? (referenceDate - ps.Step.PlanEndDate).Days : 0
                }).ToList(),
                ScheduleUrl = $"/Projekty/Detail/{projectId}?tab=harmonogram&recordId={record.Id}"
            });
        }

        dashboardRecords.Sort((a, b) =>
        {
            var aKey = DashboardRecordCategorizer.SortKey((a.Category, a.WorstOffsetDays));
            var bKey = DashboardRecordCategorizer.SortKey((b.Category, b.WorstOffsetDays));
            var cmp = aKey.Severity.CompareTo(bKey.Severity);
            return cmp != 0 ? cmp : aKey.NegatedOffset.CompareTo(bKey.NegatedOffset);
        });

        return new ProjectDashboardRecordsPanelViewModel { Records = dashboardRecords };
    }

    public async Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default)
    {
        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);
        var prevYearStart = new DateTime(year - 1, 1, 1);
        var prevYearEnd = new DateTime(year - 1, 12, 31);

        var taskCategoryIds = await _dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(c => c.Kod == RecordCategoryCodes.TaskShort || c.Kod == RecordCategoryCodes.Task)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var finalStates = await _dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(s => s.IsFinal)
            .ToDictionaryAsync(s => s.Id, s => s.Kod, ct);

        var allRecords = await _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(r => r.ProjektId == projectId && taskCategoryIds.Contains(r.KategorieId))
            .Select(r => new
            {
                r.Id,
                r.DatumZalozeni,
                r.DatumUkonceni,
                r.StavUkoluId,
                r.SubsystemId
            })
            .ToListAsync(ct);

        var subsystems = await _dbContext.Subsystemy.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => new { s.Kod, s.Nazev }, ct);

        var historyTermin = await _dbContext.Set<ZaznamHistorieTerminuEntity>().AsNoTracking()
            .Where(h => allRecords.Select(r => r.Id).Contains(h.ZaznamId))
            .ToListAsync(ct);

        var currentYearKpi = ComputeYearKpi(allRecords, finalStates, historyTermin, yearStart, yearEnd);
        var prevYearKpi = ComputeYearKpi(allRecords, finalStates, historyTermin, prevYearStart, prevYearEnd);

        var quarters = Enumerable.Range(1, 4).Select(q =>
        {
            var qStart = new DateTime(year, (q - 1) * 3 + 1, 1);
            var qEnd = qStart.AddMonths(3).AddDays(-1);
            return new ProjectDashboardQuarterViewModel
            {
                Quarter = q,
                PlannedCompletions = allRecords.Count(r => r.DatumUkonceni >= qStart && r.DatumUkonceni <= qEnd),
                ActualCompletions = allRecords.Count(r =>
                    r.StavUkoluId.HasValue && finalStates.ContainsKey(r.StavUkoluId.Value)
                    && r.DatumUkonceni >= qStart && r.DatumUkonceni <= qEnd)
            };
        }).ToList();

        var subsystemStats = allRecords
            .GroupBy(r => r.SubsystemId)
            .Select(g =>
            {
                var sub = subsystems.GetValueOrDefault(g.Key);
                return new ProjectDashboardSubsystemStatsViewModel
                {
                    SubsystemKod = sub?.Kod ?? "-",
                    SubsystemNazev = sub?.Nazev ?? "-",
                    Splneno = g.Count(r => r.StavUkoluId.HasValue && finalStates.ContainsKey(r.StavUkoluId.Value)
                        && r.DatumUkonceni >= yearStart && r.DatumUkonceni <= yearEnd),
                    VProdleni = 0,
                    Zruseno = 0,
                    Prodlouzeno = g.Count(r => historyTermin.Any(h => h.ZaznamId == r.Id
                        && h.ChangedAt >= yearStart && h.ChangedAt <= yearEnd))
                };
            })
            .Where(s => s.Splneno > 0 || s.VProdleni > 0 || s.Zruseno > 0 || s.Prodlouzeno > 0)
            .ToList();

        var attendanceRate = await ComputeAttendanceRateAsync(projectId, yearStart, yearEnd, ct);

        var availableYears = allRecords
            .Select(r => r.DatumZalozeni.Year)
            .Concat(allRecords.Select(r => r.DatumUkonceni.Year))
            .Distinct()
            .OrderDescending()
            .ToList();

        if (!availableYears.Contains(year))
        {
            availableYears.Insert(0, year);
        }

        return new ProjectDashboardStatisticsPanelViewModel
        {
            SelectedYear = year,
            AvailableYears = availableYears,
            Kpi = new ProjectDashboardKpiViewModel
            {
                Splneno = currentYearKpi.Splneno,
                Zruseno = currentYearKpi.Zruseno,
                Preneseno = currentYearKpi.Preneseno,
                VcasnostPlneniPct = currentYearKpi.VcasnostPct,
                VProdleni = currentYearKpi.VProdleni,
                PrumerneProdleniDni = currentYearKpi.PrumerneProdleniDni,
                Prodlouzeno = currentYearKpi.Prodlouzeno,
                SplnenoPrevYear = prevYearKpi.Splneno,
                ZrusenoPrevYear = prevYearKpi.Zruseno,
                PrenesenoPrevYear = prevYearKpi.Preneseno,
                VcasnostPlneniPctPrevYear = prevYearKpi.VcasnostPct,
                VProdleniPrevYear = prevYearKpi.VProdleni,
                PrumerneProdleniDniPrevYear = prevYearKpi.PrumerneProdleniDni,
                ProdlouzenoPrevYear = prevYearKpi.Prodlouzeno
            },
            Quarters = quarters,
            SubsystemStats = subsystemStats,
            AttendanceRate = attendanceRate,
            IsServiceDeskIntegrated = false
        };
    }

    public ProjectDashboardNesPanelViewModel BuildNesPanel()
    {
        return new ProjectDashboardNesPanelViewModel { IsServiceDeskIntegrated = false };
    }

    public ProjectDashboardVyzvyPanelViewModel BuildVyzvyPanel()
    {
        return new ProjectDashboardVyzvyPanelViewModel { IsServiceDeskIntegrated = false };
    }

    public async Task<bool> CanAccessDashboardAsync(int projectId, int osobaId, CancellationToken ct = default)
    {
        var activeRoleCodes = await (
                from assignment in _dbContext.ObsazeniProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliProjektu.AsNoTracking() on assignment.RoleId equals role.Id
                where assignment.ProjektId == projectId
                    && assignment.OsobaId == osobaId
                    && !assignment.DatumOdebrani.HasValue
                select role.Kod)
            .ToListAsync(ct);

        return ProjectDashboardAuthorizationPolicy.HasDashboardAccess(activeRoleCodes);
    }

    private static YearKpiSnapshot ComputeYearKpi(
        List<dynamic> allRecords,
        Dictionary<int, string> finalStates,
        List<ZaznamHistorieTerminuEntity> historyTermin,
        DateTime yearStart,
        DateTime yearEnd)
    {
        // Simplified — the actual implementation will need proper typing
        // This is a placeholder structure showing the computation approach
        return new YearKpiSnapshot(0, 0, 0, 0, 0, 0, 0);
    }

    private async Task<double?> ComputeAttendanceRateAsync(int projectId, DateTime yearStart, DateTime yearEnd, CancellationToken ct)
    {
        var meetings = await (
                from meeting in _dbContext.Jednani.AsNoTracking()
                where meeting.ProjektId == projectId
                    && meeting.DatumPlanovane >= yearStart
                    && meeting.DatumPlanovane <= yearEnd
                select meeting.Id)
            .ToListAsync(ct);

        if (meetings.Count == 0)
        {
            return null;
        }

        var attendance = await _dbContext.Ucasti.AsNoTracking()
            .Where(u => meetings.Contains(u.JednaniId))
            .ToListAsync(ct);

        if (attendance.Count == 0)
        {
            return null;
        }

        var present = attendance.Count(u =>
            string.Equals(u.StavUcastiKod, "PRESENT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.StavUcastiKod, "PRITOMEN", StringComparison.OrdinalIgnoreCase));

        return attendance.Count > 0 ? Math.Round(100.0 * present / attendance.Count, 1) : null;
    }

    private sealed record YearKpiSnapshot(
        int Splneno, int Zruseno, int Preneseno, double VcasnostPct,
        int VProdleni, double PrumerneProdleniDni, int Prodlouzeno);
}
```

**Note to implementer:** The `ComputeYearKpi` method above uses `dynamic` as a placeholder. The actual implementation must use the correct anonymous type from the LINQ query. The implementer should study the `allRecords` query result type and replace `dynamic` with the correct type. Also check entity property names for `UcastEntity` (specifically `StavUcastiKod` vs lookup through `StavUcastiId`) by reading the entity definition before implementing.

- [ ] **Step 2: Register the service in DI**

Find the DI registration location (likely `Program.cs` or a separate `ServiceCollectionExtensions.cs`). Add:

```csharp
builder.Services.AddScoped<IProjectDashboardService, ProjectDashboardService>();
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeds (fix any entity property name mismatches)

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/IProjectDashboardService.cs PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs
git commit -m "feat: implement ProjectDashboardService with records and statistics logic"
```

---

## Task 6: ProjectDashboardController

**Files:**
- Create: `PmTracker.Web/Controllers/ProjectDashboardController.cs`
- Test: `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardControllerBehaviorTests.cs`

- [ ] **Step 1: Write the controller tests**

Create `PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardControllerBehaviorTests.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.ProjectDashboard;

public sealed class ProjectDashboardControllerBehaviorTests
{
    [Fact]
    public async Task Index_ShouldReturnView_WhenUserHasAccess()
    {
        var service = new FakeProjectDashboardService { CanAccess = true };
        var controller = CreateController(service);

        var result = await controller.Index(1);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<ProjectDashboardPageViewModel>();
    }

    [Fact]
    public async Task Index_ShouldReturnForbid_WhenUserLacksAccess()
    {
        var service = new FakeProjectDashboardService { CanAccess = false };
        var controller = CreateController(service);

        var result = await controller.Index(1);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task RecordsPanel_ShouldReturnPartialView()
    {
        var service = new FakeProjectDashboardService { CanAccess = true };
        var controller = CreateController(service);

        var result = await controller.RecordsPanel(1);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.Model.Should().BeOfType<ProjectDashboardRecordsPanelViewModel>();
    }

    [Fact]
    public async Task StatisticsPanel_ShouldReturnPartialView()
    {
        var service = new FakeProjectDashboardService { CanAccess = true };
        var controller = CreateController(service);

        var result = await controller.StatisticsPanel(1, 2026);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        partial.Model.Should().BeOfType<ProjectDashboardStatisticsPanelViewModel>();
    }

    [Fact]
    public async Task NesPanel_ShouldReturnPlaceholderPartialView()
    {
        var service = new FakeProjectDashboardService { CanAccess = true };
        var controller = CreateController(service);

        var result = await controller.NesPanel(1);

        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        var model = partial.Model.Should().BeOfType<ProjectDashboardNesPanelViewModel>().Subject;
        model.IsServiceDeskIntegrated.Should().BeFalse();
    }

    private static ProjectDashboardController CreateController(FakeProjectDashboardService service)
    {
        var controller = new ProjectDashboardController(
            new FakeUserContextResolver(),
            TimeProvider.System,
            NullLoggerFactory.Instance,
            service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        SetCurrentUserContext(controller, BuildCurrentUserContext());
        return controller;
    }

    private static void SetCurrentUserContext(ProjectDashboardController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(controller, userContext);
    }

    private static CurrentUserContextViewModel BuildCurrentUserContext()
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Jan",
            Prijmeni = "Tester",
            DisplayName = "Jan Tester",
            Email = "jan@test.local",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [1],
            DeletedProjectIds = [],
            PermissionGrants = []
        };
    }

    private sealed class FakeProjectDashboardService : IProjectDashboardService
    {
        public bool CanAccess { get; init; }

        public Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default)
            => Task.FromResult(new ProjectDashboardPageViewModel
            {
                Projekt = new ProjektHeaderViewModel { Id = projectId, Nazev = "Test", Zkratka = "TST", Stav = "Bezi" },
                RecordsPanelUrl = "/stub",
                NesPanelUrl = "/stub",
                StatisticsPanelUrl = "/stub",
                VyzvyPanelUrl = "/stub",
                BackUrl = "/stub"
            });

        public Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default)
            => Task.FromResult(new ProjectDashboardRecordsPanelViewModel());

        public Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default)
            => Task.FromResult(new ProjectDashboardStatisticsPanelViewModel { SelectedYear = year });

        public ProjectDashboardNesPanelViewModel BuildNesPanel()
            => new();

        public ProjectDashboardVyzvyPanelViewModel BuildVyzvyPanel()
            => new();

        public Task<bool> CanAccessDashboardAsync(int projectId, int osobaId, CancellationToken ct = default)
            => Task.FromResult(CanAccess);
    }

    private sealed class FakeUserContextResolver : IUserContextResolver
    {
        public Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboardControllerBehaviorTests" --no-restore`
Expected: FAIL — controller does not exist

- [ ] **Step 3: Implement the controller**

Create `PmTracker.Web/Controllers/ProjectDashboardController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("projekty/{id:int}/dashboard")]
public sealed class ProjectDashboardController : BaseController
{
    private readonly IProjectDashboardService _dashboardService;

    public ProjectDashboardController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProjectDashboardService dashboardService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int id, string? dashTab = null, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        if (!CurrentUserContext.IsSuperAdmin
            && !await _dashboardService.CanAccessDashboardAsync(id, CurrentUserContext.OsobaId, ct))
        {
            return Forbid();
        }

        var model = AttachCurrentUser(await _dashboardService.BuildDashboardPageAsync(id, ct));
        if (!string.IsNullOrWhiteSpace(dashTab))
        {
            model.ActiveTab = dashTab;
        }

        return View(model);
    }

    [HttpGet("records-panel")]
    public async Task<IActionResult> RecordsPanel(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id)
            || (!CurrentUserContext.IsSuperAdmin && !await _dashboardService.CanAccessDashboardAsync(id, CurrentUserContext.OsobaId, ct)))
        {
            return Forbid();
        }

        var localNow = TimeZoneInfo.ConvertTime(TimeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime;
        var model = await _dashboardService.BuildRecordsPanelAsync(id, localNow, ct);
        return PartialView("~/Views/ProjectDashboard/_RecordsPanel.cshtml", model);
    }

    [HttpGet("statistics-panel")]
    public async Task<IActionResult> StatisticsPanel(int id, int? year = null, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id)
            || (!CurrentUserContext.IsSuperAdmin && !await _dashboardService.CanAccessDashboardAsync(id, CurrentUserContext.OsobaId, ct)))
        {
            return Forbid();
        }

        var currentYear = TimeZoneInfo.ConvertTime(TimeProvider.GetUtcNow(), TimeZoneInfo.Local).Year;
        var model = await _dashboardService.BuildStatisticsPanelAsync(id, year ?? currentYear, ct);
        return PartialView("~/Views/ProjectDashboard/_StatisticsPanel.cshtml", model);
    }

    [HttpGet("nes-panel")]
    public async Task<IActionResult> NesPanel(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id)
            || (!CurrentUserContext.IsSuperAdmin && !await _dashboardService.CanAccessDashboardAsync(id, CurrentUserContext.OsobaId, ct)))
        {
            return Forbid();
        }

        var model = _dashboardService.BuildNesPanel();
        return PartialView("~/Views/ProjectDashboard/_NesPanel.cshtml", model);
    }

    [HttpGet("vyzvy-panel")]
    public async Task<IActionResult> VyzvyPanel(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id)
            || (!CurrentUserContext.IsSuperAdmin && !await _dashboardService.CanAccessDashboardAsync(id, CurrentUserContext.OsobaId, ct)))
        {
            return Forbid();
        }

        var model = _dashboardService.BuildVyzvyPanel();
        return PartialView("~/Views/ProjectDashboard/_VyzvyPanel.cshtml", model);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectDashboardControllerBehaviorTests" --no-restore`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Controllers/ProjectDashboardController.cs PmTracker.Tests.Unit/ProjectDashboard/ProjectDashboardControllerBehaviorTests.cs
git commit -m "feat: add ProjectDashboardController with authorization and lazy-loaded panels"
```

---

## Task 7: Dashboard Views

**Files:**
- Create: `PmTracker.Web/Views/ProjectDashboard/Index.cshtml`
- Create: `PmTracker.Web/Views/ProjectDashboard/_RecordsPanel.cshtml`
- Create: `PmTracker.Web/Views/ProjectDashboard/_StatisticsPanel.cshtml`
- Create: `PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml`
- Create: `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml`

- [ ] **Step 1: Create the main dashboard view**

Create `PmTracker.Web/Views/ProjectDashboard/Index.cshtml`. Follow the structure of `Views/Dashboard/Index.cshtml` — use `data-dashboard-panel` attributes for lazy loading. Include:
- Project header with back button
- Horizontal tab bar: Záznamy, NES v prodlení, Statické informace, Výzvy
- Full-width layout class (e.g. `dashboard-fullwidth`)
- Each tab panel with placeholder loading indicators and `data-dashboard-panel-url` pointing to the respective panel endpoints

- [ ] **Step 2: Create the Records panel partial**

Create `PmTracker.Web/Views/ProjectDashboard/_RecordsPanel.cshtml`. Include:
- Category filter buttons (Vše, V prodlení, Čeká na skutečnost, Blíží se termín) with `data-filter` attributes
- Table with columns: Číslo, Název, Subsystém, Vlastník, Termín ukončení, Kategorie (colored badge), Nejhorší odchylka
- Each row clickable with expandable detail panel showing problematic steps
- Empty state message when no records
- Use existing CSS classes from gov design where possible

- [ ] **Step 3: Create the Statistics panel partial**

Create `PmTracker.Web/Views/ProjectDashboard/_StatisticsPanel.cshtml`. Include:
- Year selector dropdown at top
- KPI cards row with trend arrows (compare to previous year)
- Quarterly breakdown table
- Subsystem breakdown table
- Attendance rate display
- ServiceDesk placeholder message
- Empty state for no data

- [ ] **Step 4: Create the NES and Výzvy placeholder partials**

Create `PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml` — simple placeholder message.
Create `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml` — simple placeholder message.

- [ ] **Step 5: Build and verify**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/ProjectDashboard/
git commit -m "feat: add ProjectDashboard views with lazy-loaded panels"
```

---

## Task 8: CSS for Full-Width Dashboard Layout

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Add dashboard-specific CSS classes**

Add to `site.css`:
- `.dashboard-fullwidth` — removes `max-width` constraint, adds horizontal padding
- `.dashboard-tabs` — horizontal tab bar styling (reuse existing `.tabs` pattern)
- `.dashboard-kpi-grid` — responsive grid for KPI cards
- `.dashboard-kpi-card` — card styling with number, label, trend arrow
- `.dashboard-category-badge` — colored badges for Delayed (red), AwaitingActual (orange), ApproachingDeadline (yellow)
- `.dashboard-expandable-row` — clickable table row with expand/collapse behavior
- `.dashboard-step-detail` — expandable detail panel styling
- Ensure all colors follow gov design token system

Study existing CSS patterns in `site.css` first to stay consistent.

- [ ] **Step 2: Build and verify**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css
git commit -m "feat: add full-width dashboard CSS layout and KPI card styles"
```

---

## Task 9: JavaScript for Dashboard Interactivity

**Files:**
- Modify or create: `PmTracker.Web/wwwroot/js/modules/project-dashboard.js`
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js` (if bundled)

- [ ] **Step 1: Implement dashboard JS module**

Study existing `PmTracker.Web/wwwroot/js/modules/` files for patterns (especially how user dashboard lazy loading works). Implement:
- Lazy panel loading (reuse existing `data-dashboard-panel` pattern from user dashboard)
- Category filter buttons — client-side filtering of table rows by `data-category` attribute
- Expandable rows — toggle detail panel on row click
- Year selector — AJAX reload of statistics panel when year changes
- Tab switching with URL state management

- [ ] **Step 2: Update bundle if needed**

If the project uses bundling, add the new module to the bundle configuration.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/js/
git commit -m "feat: add project dashboard JS module for lazy loading, filtering, and expandable rows"
```

---

## Task 10: Wire Dashboard Tab into Project Detail

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs` (add `CanViewDashboard`)
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (populate `CanViewDashboard`)
- Modify: `PmTracker.Web/Views/Projekty/Detail.cshtml` (add Dashboard tab)

- [ ] **Step 1: Add CanViewDashboard to ProjektDetailViewModel**

In `PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs`, add to `ProjektDetailViewModel`:

```csharp
public bool CanViewDashboard { get; set; }
```

- [ ] **Step 2: Populate CanViewDashboard in the controller**

In `PmTracker.Web/Controllers/ProjektyController.cs`, in the `PrepareProjectDetailPresentationAsync` method (or equivalent), add after the `CanViewProposals` check:

```csharp
model.CanViewDashboard = CurrentUserContext.IsSuperAdmin
    || await _projectDashboardService.CanAccessDashboardAsync(model.Projekt.Id, CurrentUserContext.OsobaId, ct);
```

This requires injecting `IProjectDashboardService` into `ProjektyController`. Add it as a constructor parameter.

- [ ] **Step 3: Add Dashboard tab to Detail.cshtml**

In `PmTracker.Web/Views/Projekty/Detail.cshtml`, after the Návrhy tab, add:

```html
@if (Model.CanViewDashboard)
{
    <a class="tab"
       href="@Url.Action("Index", "ProjectDashboard", new { id = Model.Projekt.Id })">
        Dashboard
    </a>
}
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs PmTracker.Web/Controllers/ProjektyController.cs PmTracker.Web/Views/Projekty/Detail.cshtml
git commit -m "feat: add Dashboard tab to project detail view with role-based visibility"
```

---

## Task 11: DI Registration and Final Wiring

**Files:**
- Modify: DI registration file (find via `AddScoped<IProjectService` search)

- [ ] **Step 1: Register all new services**

Add to the DI container:

```csharp
builder.Services.AddScoped<IProjectDashboardService, ProjectDashboardService>();
```

- [ ] **Step 2: Run full build**

Run: `dotnet build PmTracker.sln`
Expected: Build succeeds

- [ ] **Step 3: Run all existing tests**

Run: `dotnet test PmTracker.sln --no-restore`
Expected: All tests pass (existing + new)

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: register ProjectDashboardService in DI and final wiring"
```

---

## Task 12: Run Full Test Suite and Review

- [ ] **Step 1: Run all unit tests**

Run: `dotnet test PmTracker.Tests.Unit --no-restore -v normal`
Expected: All tests PASS

- [ ] **Step 2: Run all integration tests**

Run: `dotnet test PmTracker.Tests.Integration --no-restore -v normal`
Expected: All tests PASS (or expected failures unrelated to dashboard)

- [ ] **Step 3: Run build**

Run: `dotnet build PmTracker.sln --no-restore`
Expected: Build succeeds with no warnings related to new code

- [ ] **Step 4: Code review**

Review all new files for:
- Consistent naming with existing codebase
- No hardcoded strings that should be constants
- Proper async/await patterns
- No N+1 query issues in service
- Authorization checks on all controller actions
- Correct view paths in PartialView calls
