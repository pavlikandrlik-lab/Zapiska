# Základní report — rámec grafů Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Postavit znovupoužitelný **rámec pro grafy** základního reportu projektu (kontrakt grafu → sdílený dataset → builder → generický SVG renderer → stránka + selektor období) — bez konkrétních grafů; ty přijdou v navazujících plánech.

**Architecture:** Každý graf bude vyrábět samostatný `IZakladniChartProvider` (single responsibility = podklady pro jeden graf) z **jednoho sdíleného snapshotu** (`ZakladniDataset` načtený jedním průchodem DB). `ZakladniReportBuilder` projede všechny v DI zaregistrované providery a poskládá `ChartData` do sekcí. Generický renderer mapuje `ChartData.Kind` → ručně psaná SVG/HTML komponenta. Tento plán dodá plumbing + prázdnou (zatím bezgrafovou) stránku reportu se selektorem období.

**Tech Stack:** .NET 8 / ASP.NET MVC (Razor), EF Core, vanilla ESM JS, ručně psané SVG, xUnit + FluentAssertions.

## Global Constraints

- **Jen rámec** — žádné konkrétní `IZakladniChartProvider` implementace (grafy) v tomto plánu. Builder s nula providery vyrobí prázdný report.
- **Sdílený snapshot** — providery NEsahají do DB; dostanou hotový `ZakladniDataset`. Dataset ohraničen projektem + obdobím (coarse: `DatumZalozeni <= Obdobi.End`).
- **Reuse authz** — nový report gateuje existující policy `permission:dashboard.statistics.view` (nový permission key = SQL seed migrace, mimo rozsah).
- **Testovací idiom projektu** — čistá C# logika: xUnit golden-vektor/behavior testy (vzor `ScheduleBarLayoutCalculatorTests`). Views/JS/DI/routing: file-text architektonické testy (vzor `NavrhyRedesignTests`).
- **Routing** — sub-route pod `[Route("projekty/{projektId:int}/dashboard")]` v `ProjectDashboardController`.
- **Soubory** — nová složka `PmTracker.Web/Services/ProjectDashboard/Zakladni/`, views `PmTracker.Web/Views/ProjectDashboard/Zakladni/`.
- **DI registrace** — `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` (vzor: `services.AddScoped<IProjectDashboardService, ProjectDashboardService>()`).
- Necommitovat automaticky pouze pokud to user zakáže — plán obsahuje commit kroky; v tomto repu commituje uživatel sám, takže commit krok = „připrav `git add` a nech na uživateli" (NEspouštět `git commit` bez výzvy).

---

### Task 1: Kontrakt grafu — `ChartData`

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ChartData.cs`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ChartDataTests.cs`

**Interfaces:**
- Consumes: nic.
- Produces: `enum ChartKind { Bar, StackedBar, Pie, Line, StatCards }`; `record ChartSeries(string Label, IReadOnlyList<double> Values, string? ColorToken = null)`; `record StatCard(string Label, string Value, string? Note = null)`; `sealed record ChartData` s `Key`, `Kind`, `Title`, `Insight?`, `Note?`, `Categories`, `Series`, `Stats` (pro `StatCards`). Statická factory `ChartData.ForStatCards(...)` a `ChartData.ForSeries(...)`.

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Unit/Dashboard/Zakladni/ChartDataTests.cs
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ChartDataTests
{
    [Fact]
    public void ForSeries_SetsKindCategoriesAndSeries()
    {
        var data = ChartData.ForSeries(
            key: "records-per-subsystem",
            kind: ChartKind.Bar,
            title: "Záznamy per subsystém",
            categories: new[] { "GESTOR", "INTEGRACE" },
            series: new[] { new ChartSeries("Počet", new double[] { 3, 5 }) },
            insight: "GESTOR nese nejvíc práce");

        data.Key.Should().Be("records-per-subsystem");
        data.Kind.Should().Be(ChartKind.Bar);
        data.Categories.Should().Equal("GESTOR", "INTEGRACE");
        data.Series.Should().ContainSingle();
        data.Series[0].Values.Should().Equal(3, 5);
        data.Insight.Should().Be("GESTOR nese nejvíc práce");
        data.Stats.Should().BeEmpty();
    }

    [Fact]
    public void ForStatCards_SetsKindAndStats()
    {
        var data = ChartData.ForStatCards(
            key: "vyjadreni-summary",
            title: "Vyjádření",
            stats: new[] { new StatCard("Celkem", "42"), new StatCard("Ø na záznam", "1,8") });

        data.Kind.Should().Be(ChartKind.StatCards);
        data.Stats.Should().HaveCount(2);
        data.Stats[0].Value.Should().Be("42");
        data.Series.Should().BeEmpty();
        data.Categories.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ChartDataTests"`
Expected: FAIL — `ChartData` / `ChartKind` neexistují (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/ChartData.cs
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public enum ChartKind
{
    Bar,
    StackedBar,
    Pie,
    Line,
    StatCards
}

public sealed record ChartSeries(string Label, IReadOnlyList<double> Values, string? ColorToken = null);

public sealed record StatCard(string Label, string Value, string? Note = null);

/// <summary>
/// Rendering-agnostický kontrakt: podklady pro JEDEN graf. Výstup jednoho
/// <see cref="IZakladniChartProvider"/>. Generický renderer mapuje <see cref="Kind"/>
/// na konkrétní SVG/HTML komponentu.
/// </summary>
public sealed record ChartData
{
    public required string Key { get; init; }
    public required ChartKind Kind { get; init; }
    public required string Title { get; init; }

    /// <summary>Hlavní sdělení „na první pohled". Volitelné.</summary>
    public string? Insight { get; init; }

    /// <summary>Metodická poznámka / caveat. Volitelné.</summary>
    public string? Note { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    public IReadOnlyList<StatCard> Stats { get; init; } = [];

    public static ChartData ForSeries(
        string key,
        ChartKind kind,
        string title,
        IReadOnlyList<string> categories,
        IReadOnlyList<ChartSeries> series,
        string? insight = null,
        string? note = null)
        => new()
        {
            Key = key,
            Kind = kind,
            Title = title,
            Categories = categories,
            Series = series,
            Insight = insight,
            Note = note
        };

    public static ChartData ForStatCards(
        string key,
        string title,
        IReadOnlyList<StatCard> stats,
        string? insight = null,
        string? note = null)
        => new()
        {
            Key = key,
            Kind = ChartKind.StatCards,
            Title = title,
            Stats = stats,
            Insight = insight,
            Note = note
        };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit --filter "ChartDataTests"`
Expected: PASS (2 testy).

- [ ] **Step 5: Připrav commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/Zakladni/ChartData.cs PmTracker.Tests.Unit/Dashboard/Zakladni/ChartDataTests.cs
# commit nechej na uživateli
```

---

### Task 2: Období — `Obdobi`

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/Obdobi.cs`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ObdobiTests.cs`

**Interfaces:**
- Consumes: nic.
- Produces: `sealed record Obdobi(DateTime Start, DateTime End, string Label)`; statické factory `Obdobi.Rok(int year)`, `Obdobi.Kvartal(int year, int quarter)`, `Obdobi.Rozsah(DateTime start, DateTime end)`. `Start`/`End` jsou inkluzivní (End = 23:59:59 posledního dne).

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Unit/Dashboard/Zakladni/ObdobiTests.cs
using System;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ObdobiTests
{
    [Fact]
    public void Rok_SpansWholeYearInclusive()
    {
        var o = Obdobi.Rok(2025);
        o.Start.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0));
        o.End.Should().Be(new DateTime(2025, 12, 31, 23, 59, 59));
        o.Label.Should().Be("2025");
    }

    [Fact]
    public void Kvartal_Q3_SpansJulToSep()
    {
        var o = Obdobi.Kvartal(2025, 3);
        o.Start.Should().Be(new DateTime(2025, 7, 1, 0, 0, 0));
        o.End.Should().Be(new DateTime(2025, 9, 30, 23, 59, 59));
        o.Label.Should().Be("Q3 2025");
    }

    [Fact]
    public void Rozsah_UsesGivenBoundsAndEndOfDay()
    {
        var o = Obdobi.Rozsah(new DateTime(2024, 2, 10), new DateTime(2024, 5, 20));
        o.Start.Should().Be(new DateTime(2024, 2, 10, 0, 0, 0));
        o.End.Should().Be(new DateTime(2024, 5, 20, 23, 59, 59));
        o.Label.Should().Be("10.02.2024 – 20.05.2024");
    }

    [Fact]
    public void Kvartal_InvalidQuarter_Throws()
    {
        var act = () => Obdobi.Kvartal(2025, 5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ObdobiTests"`
Expected: FAIL — `Obdobi` neexistuje.

- [ ] **Step 3: Write minimal implementation**

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/Obdobi.cs
using System.Globalization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Časové období reportu. Start/End jsou inkluzivní (End = konec dne 23:59:59).
/// Záznam „aktivní v období" = překrývá ⟨Start, End⟩ (přesný predikát řeší konkrétní grafy).
/// </summary>
public sealed record Obdobi(DateTime Start, DateTime End, string Label)
{
    private static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddSeconds(-1);

    public static Obdobi Rok(int year)
        => new(new DateTime(year, 1, 1), EndOfDay(new DateTime(year, 12, 31)), year.ToString(CultureInfo.InvariantCulture));

    public static Obdobi Kvartal(int year, int quarter)
    {
        if (quarter is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(quarter), quarter, "Kvartál musí být 1–4.");
        }

        var startMonth = (quarter - 1) * 3 + 1;
        var start = new DateTime(year, startMonth, 1);
        var end = EndOfDay(start.AddMonths(3).AddDays(-1));
        return new Obdobi(start, end, $"Q{quarter} {year}");
    }

    public static Obdobi Rozsah(DateTime start, DateTime end)
        => new(start.Date, EndOfDay(end), $"{start:dd.MM.yyyy} – {end:dd.MM.yyyy}");
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit --filter "ObdobiTests"`
Expected: PASS (4 testy).

- [ ] **Step 5: Připrav commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/Zakladni/Obdobi.cs PmTracker.Tests.Unit/Dashboard/Zakladni/ObdobiTests.cs
```

---

### Task 3: Provider kontrakt + dataset tvar

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDataset.cs`
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/IZakladniChartProvider.cs`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ProviderContractTests.cs`

**Interfaces:**
- Consumes: `ChartData` (Task 1), `Obdobi` (Task 2).
- Produces:
  - `sealed record ZakladniDataset` — sdílený snapshot: `Obdobi Obdobi`, `IReadOnlyList<DatasetRecord> Records`, `IReadOnlyList<DatasetSubsystem> Subsystemy`, `IReadOnlyList<DatasetState> Stavy`, `IReadOnlyList<DatasetVyjadreni> Vyjadreni`, `IReadOnlyList<DatasetTerminChange> TerminChanges`. Vnořené `record`y nesou jen pole potřebná grafy (anti-corruption vůči EF entitám).
  - `interface IZakladniChartProvider { string Key { get; } string SectionKey { get; } int Order { get; } ChartData Build(ZakladniDataset dataset); }`.

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Unit/Dashboard/Zakladni/ProviderContractTests.cs
using System.Linq;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ProviderContractTests
{
    private sealed class FakeProvider : IZakladniChartProvider
    {
        public string Key => "fake";
        public string SectionKey => "zakladni";
        public int Order => 10;
        public ChartData Build(ZakladniDataset dataset)
            => ChartData.ForStatCards("fake", "Fake", new[] { new StatCard("Záznamů", dataset.Records.Count.ToString()) });
    }

    [Fact]
    public void Provider_BuildsChartFromDataset()
    {
        var dataset = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2025),
            Records = new[]
            {
                new DatasetRecord(1, SubsystemId: 7, StavId: 1, DatumZalozeni: new System.DateTime(2025, 2, 1), DatumUkonceni: new System.DateTime(2025, 6, 1)),
                new DatasetRecord(2, SubsystemId: 7, StavId: 2, DatumZalozeni: new System.DateTime(2025, 3, 1), DatumUkonceni: new System.DateTime(2025, 7, 1))
            }
        };

        IZakladniChartProvider provider = new FakeProvider();
        var chart = provider.Build(dataset);

        chart.Key.Should().Be("fake");
        chart.Stats.Single().Value.Should().Be("2");
        provider.SectionKey.Should().Be("zakladni");
        provider.Order.Should().Be(10);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ProviderContractTests"`
Expected: FAIL — `ZakladniDataset`, `DatasetRecord`, `IZakladniChartProvider` neexistují.

- [ ] **Step 3: Write minimal implementation**

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDataset.cs
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

// Pozn.: ProjektovyZaznamEntity.DatumUkonceni je NON-nullable → DatumUkonceni zde DateTime.
public sealed record DatasetRecord(
    int Id,
    int SubsystemId,
    int? StavId,
    DateTime DatumZalozeni,
    DateTime DatumUkonceni);

public sealed record DatasetSubsystem(int Id, string Kod, string Nazev);

public sealed record DatasetState(int Id, string Kod, string Nazev, bool IsFinal);

public sealed record DatasetVyjadreni(int Id, int ZaznamId, DateTime DatumVyjadreni);

// Mapuje ZaznamHistorieTerminuEntity: DatumZmeny / PuvodniDatum / NoveDatum.
public sealed record DatasetTerminChange(int ZaznamId, DateTime DatumZmeny, DateTime PuvodniTermin, DateTime NovyTermin);

/// <summary>
/// Sdílený snapshot základního reportu — načte se jedním průchodem DB
/// (<see cref="IZakladniDatasetLoader"/>) a předá se všem providerům.
/// Providery z něj jen čtou; do DB nesahají.
/// </summary>
public sealed record ZakladniDataset
{
    public required Obdobi Obdobi { get; init; }
    public IReadOnlyList<DatasetRecord> Records { get; init; } = [];
    public IReadOnlyList<DatasetSubsystem> Subsystemy { get; init; } = [];
    public IReadOnlyList<DatasetState> Stavy { get; init; } = [];
    public IReadOnlyList<DatasetVyjadreni> Vyjadreni { get; init; } = [];
    public IReadOnlyList<DatasetTerminChange> TerminChanges { get; init; } = [];
}
```

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/IZakladniChartProvider.cs
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Vyrábí podklady pro JEDEN graf základního reportu. Single responsibility.
/// Implementace se registrují do DI; <see cref="ZakladniReportBuilder"/> je projede.
/// </summary>
public interface IZakladniChartProvider
{
    /// <summary>Stabilní identifikátor grafu (např. „records-per-subsystem").</summary>
    string Key { get; }

    /// <summary>Do které sekce reportu graf patří.</summary>
    string SectionKey { get; }

    /// <summary>Pořadí v rámci sekce (vzestupně).</summary>
    int Order { get; }

    ChartData Build(ZakladniDataset dataset);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit --filter "ProviderContractTests"`
Expected: PASS.

- [ ] **Step 5: Připrav commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDataset.cs PmTracker.Web/Services/ProjectDashboard/Zakladni/IZakladniChartProvider.cs PmTracker.Tests.Unit/Dashboard/Zakladni/ProviderContractTests.cs
```

---

### Task 4: Builder — `ZakladniReportBuilder`

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniReportViewModel.cs`
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/IZakladniDatasetLoader.cs`
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniReportBuilder.cs`
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ZakladniReportBuilderTests.cs`

**Interfaces:**
- Consumes: `IZakladniChartProvider`, `ZakladniDataset`, `Obdobi`, `ChartData`.
- Produces:
  - `sealed record ReportSection(string Key, IReadOnlyList<ChartData> Charts)`; `sealed record ZakladniReportViewModel(int ProjektId, Obdobi Obdobi, IReadOnlyList<ReportSection> Sections)`.
  - `interface IZakladniDatasetLoader { Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct); }`.
  - `sealed class ZakladniReportBuilder(IZakladniDatasetLoader loader, IEnumerable<IZakladniChartProvider> providers)` s `Task<ZakladniReportViewModel> BuildAsync(int projektId, Obdobi obdobi, CancellationToken ct)`. Sekce řazené dle min(Order) providerů v sekci; grafy v sekci dle Order; prázdné sekce vynechány. **Nula providerů → prázdný report (žádné sekce).**

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Unit/Dashboard/Zakladni/ZakladniReportBuilderTests.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ZakladniReportBuilderTests
{
    private sealed class StubLoader : IZakladniDatasetLoader
    {
        public Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct)
            => Task.FromResult(new ZakladniDataset { Obdobi = obdobi });
    }

    private sealed class StubProvider(string key, string section, int order) : IZakladniChartProvider
    {
        public string Key => key;
        public string SectionKey => section;
        public int Order => order;
        public ChartData Build(ZakladniDataset dataset)
            => ChartData.ForStatCards(key, key, new[] { new StatCard("x", "1") });
    }

    [Fact]
    public async Task BuildAsync_NoProviders_ProducesEmptyReport()
    {
        var builder = new ZakladniReportBuilder(new StubLoader(), Enumerable.Empty<IZakladniChartProvider>());
        var report = await builder.BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        report.ProjektId.Should().Be(1);
        report.Obdobi.Label.Should().Be("2025");
        report.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_GroupsBySectionAndOrdersByOrder()
    {
        var providers = new IZakladniChartProvider[]
        {
            new StubProvider("b", "zakladni", 20),
            new StubProvider("a", "zakladni", 10),
            new StubProvider("c", "termin", 5)
        };
        var builder = new ZakladniReportBuilder(new StubLoader(), providers);
        var report = await builder.BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        // sekce „termin" má min Order 5 → první; „zakladni" min Order 10 → druhá.
        report.Sections.Select(s => s.Key).Should().Equal("termin", "zakladni");
        // grafy v „zakladni" dle Order: a(10) před b(20).
        report.Sections.Last().Charts.Select(c => c.Key).Should().Equal("a", "b");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ZakladniReportBuilderTests"`
Expected: FAIL — `ZakladniReportBuilder`, `IZakladniDatasetLoader`, `ZakladniReportViewModel` neexistují.

- [ ] **Step 3: Write minimal implementation**

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniReportViewModel.cs
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public sealed record ReportSection(string Key, IReadOnlyList<ChartData> Charts);

public sealed record ZakladniReportViewModel(
    int ProjektId,
    Obdobi Obdobi,
    IReadOnlyList<ReportSection> Sections);
```

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/IZakladniDatasetLoader.cs
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public interface IZakladniDatasetLoader
{
    Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct);
}
```

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniReportBuilder.cs
namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Načte sdílený dataset jedním průchodem a projede všechny zaregistrované
/// chart-providery. Výsledné <see cref="ChartData"/> poskládá do sekcí.
/// Nula providerů → prázdný report.
/// </summary>
public sealed class ZakladniReportBuilder
{
    private readonly IZakladniDatasetLoader _loader;
    private readonly IReadOnlyList<IZakladniChartProvider> _providers;

    public ZakladniReportBuilder(IZakladniDatasetLoader loader, IEnumerable<IZakladniChartProvider> providers)
    {
        _loader = loader;
        _providers = providers.ToList();
    }

    public async Task<ZakladniReportViewModel> BuildAsync(int projektId, Obdobi obdobi, CancellationToken ct)
    {
        var dataset = await _loader.LoadAsync(projektId, obdobi, ct);

        var sections = _providers
            .GroupBy(p => p.SectionKey)
            .Select(g => new
            {
                Key = g.Key,
                MinOrder = g.Min(p => p.Order),
                Charts = g.OrderBy(p => p.Order)
                    .Select(p => p.Build(dataset))
                    .ToList()
            })
            .OrderBy(s => s.MinOrder)
            .Select(s => new ReportSection(s.Key, s.Charts))
            .ToList();

        return new ZakladniReportViewModel(projektId, obdobi, sections);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit --filter "ZakladniReportBuilderTests"`
Expected: PASS (2 testy).

- [ ] **Step 5: Připrav commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniReportViewModel.cs PmTracker.Web/Services/ProjectDashboard/Zakladni/IZakladniDatasetLoader.cs PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniReportBuilder.cs PmTracker.Tests.Unit/Dashboard/Zakladni/ZakladniReportBuilderTests.cs
```

---

### Task 5: Dataset loader — `ZakladniDatasetLoader`

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDatasetLoader.cs`
- Test: `PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs`

**Interfaces:**
- Consumes: `PmTrackerDbContext`, `Obdobi`, `ZakladniDataset` + vnořené recordy (Task 3).
- Produces: `sealed class ZakladniDatasetLoader(PmTrackerDbContext db) : IZakladniDatasetLoader` — jedním průchodem (sada `AsNoTracking` dotazů) načte záznamy projektu coarse-ohraničené `DatumZalozeni <= obdobi.End`, jejich subsystémy, katalog stavů, vyjádření (DatumVyjadreni v ⟨Start,End⟩) a změny termínů (ZaznamHistorieTerminu pro ty záznamy).

**Pozn. k testu:** Integrační test používá SQL testcontainer (vyžaduje Docker/Colima). Vzor + helpery: `PmTracker.Tests.Integration/DataStore/RecordProposalDataStoreTests.cs` a `IntegrationTestHelper`.

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ZakladniDatasetLoaderTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ZakladniDatasetLoaderTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task LoadAsync_LoadsProjectRecordsBoundedByPeriodEnd()
    {
        var db = await _fixture.CreateDatabaseAsync("zakladni_dataset_loader");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "DatasetOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "DATASET1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "DATASET1_SYS", personId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        // EnsureRecordAsync nastaví DatumZalozeni=Today; pro test filtru období je přepíšeme:
        // jeden záznam 2025-03 (v období), druhý 2026-01 (po konci období Obdobi.Rok(2025) → vyloučen).
        var inPeriodId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "InPeriod");
        var futureId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, personId, subsystemId, "U", "FutureCreated");
        var inPeriod = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == inPeriodId);
        inPeriod.DatumZalozeni = new DateTime(2025, 3, 1);
        var future = await dbContext.ProjektoveZaznamy.FirstAsync(r => r.Id == futureId);
        future.DatumZalozeni = new DateTime(2026, 1, 5);
        await dbContext.SaveChangesAsync();

        var loader = new ZakladniDatasetLoader(dbContext);
        var dataset = await loader.LoadAsync(projectId, Obdobi.Rok(2025), CancellationToken.None);

        dataset.Records.Should().ContainSingle(r => r.DatumZalozeni == new DateTime(2025, 3, 1));
        dataset.Records.Should().NotContain(r => r.DatumZalozeni.Year == 2026);
        dataset.Subsystemy.Should().Contain(s => s.Id == subsystemId);
        dataset.Stavy.Should().NotBeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `DOCKER_HOST=unix:///Users/$USER/.colima/default/docker.sock TESTCONTAINERS_RYUK_DISABLED=true dotnet test PmTracker.Tests.Integration --filter "ZakladniDatasetLoaderTests"`
Expected: FAIL — `ZakladniDatasetLoader` neexistuje (compile). *(Pokud Docker neběží: `colima start` — viz `docs/specs/running-tests-locally.md`.)*

- [ ] **Step 3: Write minimal implementation**

```csharp
// PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDatasetLoader.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Načte sdílený snapshot základního reportu jedním průchodem DB. Coarse-ohraničeno
/// projektem + obdobím (DatumZalozeni &lt;= End). Přesný „aktivní v období" predikát
/// (dle data dokončení z historie stavů) aplikují konkrétní grafy.
/// </summary>
public sealed class ZakladniDatasetLoader : IZakladniDatasetLoader
{
    private readonly PmTrackerDbContext _db;
    public ZakladniDatasetLoader(PmTrackerDbContext db) => _db = db;

    public async Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct)
    {
        var records = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(r => r.ProjektId == projektId && r.DatumZalozeni <= obdobi.End)
            .Select(r => new DatasetRecord(r.Id, r.SubsystemId, r.StavUkoluId, r.DatumZalozeni, r.DatumUkonceni))
            .ToListAsync(ct);

        var recordIds = records.Select(r => r.Id).ToList();

        var subsystemy = await _db.ProjektSubsystemy.AsNoTracking()
            .Where(ps => ps.ProjektId == projektId && !ps.DatumOdebrani.HasValue)
            .Join(_db.Subsystemy.AsNoTracking(), ps => ps.SubsystemId, s => s.Id,
                (ps, s) => new DatasetSubsystem(s.Id, s.Kod, s.Nazev))
            .ToListAsync(ct);

        var stavy = await _db.CiselnikStavuUkolu.AsNoTracking()
            .Select(s => new DatasetState(s.Id, s.Kod, s.Nazev, s.IsFinal))
            .ToListAsync(ct);

        var vyjadreni = await _db.Vyjadreni.AsNoTracking()
            .Where(v => recordIds.Contains(v.ZaznamId)
                && v.DatumVyjadreni >= obdobi.Start && v.DatumVyjadreni <= obdobi.End)
            .Select(v => new DatasetVyjadreni(v.Id, v.ZaznamId, v.DatumVyjadreni))
            .ToListAsync(ct);

        var terminChanges = await _db.ZaznamHistorieTerminu.AsNoTracking()
            .Where(h => recordIds.Contains(h.ZaznamId))
            .Select(h => new DatasetTerminChange(h.ZaznamId, h.DatumZmeny, h.PuvodniDatum, h.NoveDatum))
            .ToListAsync(ct);

        return new ZakladniDataset
        {
            Obdobi = obdobi,
            Records = records,
            Subsystemy = subsystemy,
            Stavy = stavy,
            Vyjadreni = vyjadreni,
            TerminChanges = terminChanges
        };
    }
}
```

> **Ověřeno proti entitám:** `ProjektovyZaznamEntity` (ProjektId/SubsystemId/StavUkoluId/DatumZalozeni/DatumUkonceni), `ZaznamHistorieTerminuEntity` (ZaznamId/DatumZmeny/PuvodniDatum/NoveDatum), `VyjadreniEntity` (ZaznamId/DatumVyjadreni), `ProjektSubsystemEntity` (DatumOdebrani), `SubsystemEntity` (Kod/Nazev), `CiselnikStavuUkoluEntity` (Kod/Nazev/IsFinal). DbSety: ProjektoveZaznamy/ProjektSubsystemy/Subsystemy/CiselnikStavuUkolu/Vyjadreni/ZaznamHistorieTerminu.

- [ ] **Step 4: Run test to verify it passes**

Run: stejný příkaz jako Step 2.
Expected: PASS.

- [ ] **Step 5: Připrav commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDatasetLoader.cs PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs
```

---

### Task 6: Generický SVG renderer (dispatch + Bar + StatCards)

**Files:**
- Create: `PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml` (dispatch dle `Kind`)
- Create: `PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml` (SVG sloupec)
- Create: `PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartStatCards.cshtml` (HTML karty)
- Create: `PmTracker.Web/wwwroot/css/components/zakladni-report.css`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (přidat `@import "components/zakladni-report.css";` na začátek mezi ostatní `@import "components/*"`)
- Modify: `PmTracker.Web/Views/_ViewImports.cshtml` (pokud chybí `@using PmTracker.Web.Services.ProjectDashboard.Zakladni`)
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ChartRendererTests.cs` (file-text architektura)

**Interfaces:**
- Consumes: `ChartData`, `ChartKind`.
- Produces: dispatch partial `_Chart.cshtml` přijímající `@model ChartData`, který dle `Model.Kind` includuje konkrétní partial. Nepokryté kindy (Pie/StackedBar/Line) zatím renderují fallback `<!-- chart kind not yet implemented -->` (přijdou s konkrétními grafy).

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Unit/Dashboard/Zakladni/ChartRendererTests.cs
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ChartRendererTests
{
    private static string Read(string rel)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void DispatchPartial_RoutesByKind()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml");
        v.Should().Contain("Model.Kind");
        v.Should().Contain("_ChartBar");
        v.Should().Contain("_ChartStatCards");
    }

    [Fact]
    public void BarPartial_RendersSvg()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml");
        v.Should().Contain("<svg");
        v.Should().Contain("Model.Series");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ChartRendererTests"`
Expected: FAIL — soubory neexistují.

- [ ] **Step 3: Write minimal implementation**

`_Chart.cshtml`:
```razor
@model PmTracker.Web.Services.ProjectDashboard.Zakladni.ChartData
@using PmTracker.Web.Services.ProjectDashboard.Zakladni
<figure class="zr-chart" data-chart-key="@Model.Key">
    <figcaption class="zr-chart-caption">
        <span class="zr-chart-title">@Model.Title</span>
        @if (!string.IsNullOrWhiteSpace(Model.Insight))
        {
            <span class="zr-chart-insight">@Model.Insight</span>
        }
    </figcaption>
    @switch (Model.Kind)
    {
        case ChartKind.Bar:
            <partial name="~/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml" model="Model" />
            break;
        case ChartKind.StatCards:
            <partial name="~/Views/ProjectDashboard/Zakladni/_ChartStatCards.cshtml" model="Model" />
            break;
        default:
            <!-- graf typu @Model.Kind zatím nemá renderer (přijde s konkrétním grafem) -->
            break;
    }
    @if (!string.IsNullOrWhiteSpace(Model.Note))
    {
        <figcaption class="zr-chart-note muted">@Model.Note</figcaption>
    }
</figure>
```

`_ChartBar.cshtml` (minimální, ale funkční SVG sloupec — jedna série):
```razor
@model PmTracker.Web.Services.ProjectDashboard.Zakladni.ChartData
@{
    var series = Model.Series.Count > 0 ? Model.Series[0] : null;
    var values = series?.Values ?? new System.Collections.Generic.List<double>();
    var max = values.Count > 0 ? System.Math.Max(1.0, System.Linq.Enumerable.Max(values)) : 1.0;
    const int barW = 48, gap = 16, top = 8, chartH = 160;
    var width = Model.Categories.Count * (barW + gap) + gap;
}
<svg class="zr-bar" viewBox="0 0 @width @(chartH + 28)" role="img" aria-label="@Model.Title">
    @for (var i = 0; i < Model.Categories.Count; i++)
    {
        var v = i < values.Count ? values[i] : 0;
        var h = (int)System.Math.Round(v / max * chartH);
        var x = gap + i * (barW + gap);
        var y = top + (chartH - h);
        <rect class="zr-bar-rect" x="@x" y="@y" width="@barW" height="@h" rx="3"></rect>
        <text class="zr-bar-value" x="@(x + barW / 2)" y="@(y - 4)" text-anchor="middle">@v</text>
        <text class="zr-bar-label" x="@(x + barW / 2)" y="@(chartH + top + 16)" text-anchor="middle">@Model.Categories[i]</text>
    }
</svg>
```

`_ChartStatCards.cshtml`:
```razor
@model PmTracker.Web.Services.ProjectDashboard.Zakladni.ChartData
<div class="zr-statcards">
    @foreach (var s in Model.Stats)
    {
        <div class="zr-statcard">
            <div class="zr-statcard-value">@s.Value</div>
            <div class="zr-statcard-label">@s.Label</div>
            @if (!string.IsNullOrWhiteSpace(s.Note))
            {
                <div class="zr-statcard-note muted">@s.Note</div>
            }
        </div>
    }
</div>
```

`zakladni-report.css` (minimální styl + tisk-friendly, gov tokeny):
```css
.zr-chart { margin: 0 0 20px; }
.zr-chart-caption { display: flex; flex-direction: column; gap: 2px; margin-bottom: 8px; }
.zr-chart-title { font-weight: 600; font-size: 14px; }
.zr-chart-insight { font-size: 12px; color: var(--gov-color-muted); }
.zr-bar-rect { fill: var(--gov-color-primary); }
.zr-bar-value { font-size: 11px; fill: var(--gov-color-text); }
.zr-bar-label { font-size: 11px; fill: var(--gov-color-muted); }
.zr-statcards { display: flex; flex-wrap: wrap; gap: 12px; }
.zr-statcard { border: 1px solid var(--gov-color-border); border-radius: var(--gov-radius); padding: 12px 16px; min-width: 120px; }
.zr-statcard-value { font-size: 22px; font-weight: 700; }
.zr-statcard-label { font-size: 12px; color: var(--gov-color-muted); }
@media print { .zr-chart { break-inside: avoid; } }
```

Pokud `_ViewImports.cshtml` neobsahuje `@using PmTracker.Web.Services.ProjectDashboard.Zakladni`, přidej ho. CSS se načítá přes `@import` v `site.css` (component CSS NEjsou v `_Layout` jako `<link>`) — přidej řádek `@import "components/zakladni-report.css";` na začátek `site.css` mezi ostatní `@import "components/*";`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit --filter "ChartRendererTests"`
Expected: PASS.

- [ ] **Step 5: Připrav commit**

```bash
git add PmTracker.Web/Views/ProjectDashboard/Zakladni/_Chart.cshtml PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartBar.cshtml PmTracker.Web/Views/ProjectDashboard/Zakladni/_ChartStatCards.cshtml PmTracker.Web/wwwroot/css/components/zakladni-report.css PmTracker.Web/wwwroot/css/site.css PmTracker.Web/Views/_ViewImports.cshtml PmTracker.Tests.Unit/Dashboard/Zakladni/ChartRendererTests.cs
```

---

### Task 7: Stránka reportu + selektor období + controller + DI

**Files:**
- Create: `PmTracker.Web/Views/ProjectDashboard/Zakladni/Report.cshtml`
- Create: `PmTracker.Web/Views/ProjectDashboard/Zakladni/_ObdobiSelector.cshtml`
- Create: `PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js`
- Modify: `PmTracker.Web/Controllers/ProjectDashboardController.cs` (přidat akci `ZakladniReport`)
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` (registrace `IZakladniDatasetLoader`, `ZakladniReportBuilder`)
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (init selektoru)
- Test: `PmTracker.Tests.Unit/Dashboard/Zakladni/ZakladniReportWiringTests.cs` (file-text architektura)

**Interfaces:**
- Consumes: `ZakladniReportBuilder`, `ZakladniReportViewModel`, `Obdobi`, dispatch partial `_Chart`.
- Produces: GET akce `ZakladniReport(int projektId, int? rok, int? kvartal, DateTime? od, DateTime? doDate)` → sestaví `Obdobi` (default rok = aktuální) → `ZakladniReportBuilder.BuildAsync` → `PartialView("~/Views/ProjectDashboard/Zakladni/Report.cshtml", model)`. Selektor mění období a reloaduje partial (fetch → replace innerHTML).

- [ ] **Step 1: Write the failing test**

```csharp
// PmTracker.Tests.Unit/Dashboard/Zakladni/ZakladniReportWiringTests.cs
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ZakladniReportWiringTests
{
    private static string Read(string rel)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Controller_HasZakladniReportAction()
    {
        var c = Read("PmTracker.Web/Controllers/ProjectDashboardController.cs");
        c.Should().Contain("zakladni-report");
        c.Should().Contain("ZakladniReport");
        c.Should().Contain("permission:dashboard.statistics.view");
    }

    [Fact]
    public void Di_RegistersFrameworkServices()
    {
        var di = Read("PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs");
        di.Should().Contain("IZakladniDatasetLoader");
        di.Should().Contain("ZakladniReportBuilder");
    }

    [Fact]
    public void ReportView_RendersSectionsAndChartPartial()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/Report.cshtml");
        v.Should().Contain("Model.Sections");
        v.Should().Contain("Zakladni/_Chart");
        v.Should().Contain("_ObdobiSelector");
    }

    [Fact]
    public void Selector_HasPresetsAndReloadHook()
    {
        var v = Read("PmTracker.Web/Views/ProjectDashboard/Zakladni/_ObdobiSelector.cshtml");
        v.Should().Contain("data-zakladni-obdobi");
        var js = Read("PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js");
        js.Should().Contain("data-zakladni-obdobi");
        js.Should().Contain("fetch");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit --filter "ZakladniReportWiringTests"`
Expected: FAIL — akce/soubory/registrace neexistují.

- [ ] **Step 3: Write minimal implementation**

V `ProjectDashboardController.cs` přidej akci (using `PmTracker.Web.Services.ProjectDashboard.Zakladni;` nahoře):

```csharp
[HttpGet("zakladni-report")]
[Authorize(Policy = "permission:dashboard.statistics.view")]
public async Task<IActionResult> ZakladniReport(
    int projektId, int? rok = null, int? kvartal = null,
    DateTime? od = null, DateTime? doDate = null, CancellationToken ct = default)
{
    if (!await EnsureDashboardAccessAsync(projektId, ct))
    {
        return Forbid();
    }

    var currentYear = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).Year;
    Obdobi obdobi = (od.HasValue && doDate.HasValue)
        ? Obdobi.Rozsah(od.Value, doDate.Value)
        : kvartal.HasValue
            ? Obdobi.Kvartal(rok ?? currentYear, kvartal.Value)
            : Obdobi.Rok(rok ?? currentYear);

    var model = await _reportBuilder.BuildAsync(projektId, obdobi, ct);
    return PartialView("~/Views/ProjectDashboard/Zakladni/Report.cshtml", model);
}
```

Rozšiř konstruktor controlleru o `ZakladniReportBuilder` (DI injektáž) + nové pole:

```csharp
// nové pole vedle ostatních:
private readonly ZakladniReportBuilder _reportBuilder;

// konstruktor — přidat parametr a přiřazení:
public ProjectDashboardController(
    IUserContextResolver userContextResolver,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory,
    IProjectDashboardService dashboardService,
    INesPanelExcelExportService nesExcelExport,
    ZakladniReportBuilder reportBuilder)        // <- nový
    : base(userContextResolver, timeProvider, loggerFactory)
{
    _dashboardService = dashboardService;
    _nesExcelExport = nesExcelExport;
    _timeProvider = timeProvider;
    _reportBuilder = reportBuilder;             // <- nový
}
```

V `DataStoreServiceCollectionExtensions.cs` (vedle řádku 151) přidej:
```csharp
services.AddScoped<PmTracker.Web.Services.ProjectDashboard.Zakladni.IZakladniDatasetLoader,
    PmTracker.Web.Services.ProjectDashboard.Zakladni.ZakladniDatasetLoader>();
services.AddScoped<PmTracker.Web.Services.ProjectDashboard.Zakladni.ZakladniReportBuilder>();
// chart-providery se budou registrovat zde v navazujících plánech:
// services.AddScoped<IZakladniChartProvider, RecordsPerSubsystemProvider>();
```

`Report.cshtml`:
```razor
@model PmTracker.Web.Services.ProjectDashboard.Zakladni.ZakladniReportViewModel
<section class="zr-report" data-zakladni-report data-projekt-id="@Model.ProjektId">
    <partial name="~/Views/ProjectDashboard/Zakladni/_ObdobiSelector.cshtml" model="Model" />
    @if (Model.Sections.Count == 0)
    {
        <p class="muted">Pro toto období zatím nejsou žádné grafy.</p>
    }
    else
    {
        foreach (var section in Model.Sections)
        {
            <div class="zr-section" data-section="@section.Key">
                @foreach (var chart in section.Charts)
                {
                    <partial name="~/Views/ProjectDashboard/Zakladni/_Chart.cshtml" model="chart" />
                }
            </div>
        }
    }
</section>
```

`_ObdobiSelector.cshtml` (presety rok + kvartál; vlastní rozsah jako rozšíření později, ale hook připraven):
```razor
@model PmTracker.Web.Services.ProjectDashboard.Zakladni.ZakladniReportViewModel
@{
    var currentYear = System.DateTime.Now.Year;
    var url = $"/projekty/{Model.ProjektId}/dashboard/zakladni-report";
}
<div class="zr-obdobi" data-zakladni-obdobi data-url="@url">
    <label>Rok
        <select data-zakladni-obdobi-rok>
            @for (var y = currentYear; y >= currentYear - 5; y--)
            {
                <option value="@y" selected="@(Model.Obdobi.Label == y.ToString() ? "selected" : null)">@y</option>
            }
        </select>
    </label>
    <label>Kvartál
        <select data-zakladni-obdobi-kvartal>
            <option value="">celý rok</option>
            @for (var q = 1; q <= 4; q++)
            {
                <option value="@q">Q@q</option>
            }
        </select>
    </label>
</div>
```

`zakladniReport.js`:
```javascript
// dashboard/zakladniReport.js — selektor období → reload partial reportu
export function initZakladniReport() {
    document.addEventListener("change", async (event) => {
        const target = event.target;
        if (!(target instanceof Element)) return;
        const shell = target.closest("[data-zakladni-obdobi]");
        if (!(shell instanceof HTMLElement)) return;

        const url = shell.getAttribute("data-url") || "";
        const rok = shell.querySelector("[data-zakladni-obdobi-rok]")?.value || "";
        const kvartal = shell.querySelector("[data-zakladni-obdobi-kvartal]")?.value || "";
        if (!url || !rok) return;

        const qs = new URLSearchParams({ rok });
        if (kvartal) qs.set("kvartal", kvartal);

        const report = shell.closest("[data-zakladni-report]");
        if (!(report instanceof HTMLElement)) return;
        try {
            const html = await fetch(`${url}?${qs.toString()}`, { headers: { "X-Requested-With": "XMLHttpRequest" } })
                .then((r) => r.text());
            report.outerHTML = html;
        } catch {
            /* tichý fail — uživatel zkusí znovu */
        }
    });
}
```

V `bootstrap.js`: import `initZakladniReport` z `./dashboard/zakladniReport.js` a zavolej v `runInitializers([... () => initZakladniReport(), ...])`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet build PmTracker.Web && dotnet test PmTracker.Tests.Unit --filter "ZakladniReportWiringTests" --no-build`
Expected: PASS (4 testy).

- [ ] **Step 5: Full build + suite + commit**

```bash
dotnet build PmTracker.Web && dotnet test PmTracker.Tests.Unit --no-build
git add PmTracker.Web/Views/ProjectDashboard/Zakladni/Report.cshtml PmTracker.Web/Views/ProjectDashboard/Zakladni/_ObdobiSelector.cshtml PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js PmTracker.Web/Controllers/ProjectDashboardController.cs PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs PmTracker.Web/wwwroot/js/modules/bootstrap.js PmTracker.Tests.Unit/Dashboard/Zakladni/ZakladniReportWiringTests.cs
```

- [ ] **Step 6: Smoke verify (Playwright, volitelně)**

Spusť app (`dotnet run`, DB přes Colima), otevři `/projekty/1/dashboard/zakladni-report?asUser=1` — měla by se zobrazit prázdná stránka reportu se selektorem období (žádné grafy), bez console chyb. Změna roku/kvartálu reloadne partial.

---

## Self-Review

**1. Spec coverage:** Architektura (dva reporty / provider / sdílený snapshot / generický renderer / selektor) → Tasky 1–7. Základní report obsah (5 jednotek) → mimo rozsah tohoto plánu (rámec), připraveno registrací providerů. Období (presety + vlastní) → Task 2 + 7 (vlastní rozsah: factory hotová, UI hook připraven, plné UI v navazujícím). Testování (golden-vektor + integrační loader) → Tasky 1–5. ✔

**2. Placeholder scan:** Žádné „TBD/později" bez kódu; každý krok má konkrétní kód a příkaz. Komentář „providery zde v navazujících plánech" je záměrný extension point, ne placeholder. ✔

**3. Type consistency:** `ChartData`/`ChartKind`/`ChartSeries`/`StatCard` (T1) konzistentně v T3/T4/T6. `Obdobi` factory (T2) v T5/T7. `IZakladniChartProvider` (Key/SectionKey/Order/Build) v T3/T4. `IZakladniDatasetLoader.LoadAsync` (T4) implementováno v T5. `ZakladniReportBuilder.BuildAsync` (T4) voláno v T7. ✔

**4. Verifikace proti reálnému kódu (provedeno při review):**
- Entity pole ověřena: `ProjektovyZaznamEntity.DatumUkonceni` je **non-nullable** (DatasetRecord opraven); `ZaznamHistorieTerminuEntity` = `DatumZmeny`/`PuvodniDatum`/`NoveDatum` (loader opraven z chybného `ZmenenoAt`/`StaryTermin`/`NovyTermin`).
- DbSety ověřeny: `ProjektoveZaznamy`/`ProjektSubsystemy`/`Subsystemy`/`CiselnikStavuUkolu`/`Vyjadreni`/`ZaznamHistorieTerminu`. ✔
- `IntegrationTestHelper.EnsureRecordAsync(db, projId, ownerId, subsystemId, categoryCode, marker)` **nemá** `datumZalozeni` param a nastaví `DatumZalozeni=Today` → test ho přepisuje load+update (opraveno; helper se nemodifikuje).
- Component CSS se načítá přes `@import` v `site.css` (ne `<link>` v `_Layout`) → Task 6 opraven.
- Policy `permission:dashboard.statistics.view` existuje (seed + controller) → reuse OK, žádná authz migrace.
- `<partial>` cesty převedeny na absolutní `~/Views/ProjectDashboard/Zakladni/*.cshtml` (eliminace resolution ambiguity).
- Razor: default case dispatch přepsán z problematického `@Html.Raw(...)` na markup komentář. ✔

## Mimo rozsah (navazující plány)

- Konkrétní grafy (providery): záznamy/subsystém, stavový rozpad, vyjádření, termínová disciplína — každý vlastní task (provider + golden-vektor + případně nový `ChartKind` SVG partial).
- Plné UI vlastního rozsahu období (date-range picker).
- Analytický report.
- PDF/tisk export.
