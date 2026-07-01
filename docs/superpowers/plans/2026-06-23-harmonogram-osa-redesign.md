# Harmonogram — oprava osy a zarovnání pruhů — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pruhy Plán/Skutečnost a ukazatel „Dnes" na projektové záložce Harmonogram (i v editoru) sedí na kalendářní ose s měsíčními hranicemi, počítané jedním kanonickým algoritmem (C# autoritativní + JS dvojče) zamčeným parity testy.

**Architecture:** Server (`ScheduleBarLayoutCalculator`) je jediný zdroj pravdy pro statickou kartu — spočítá osu přichycenou na měsíce + měsíční ticky + pozice. JS `timeline.js` osu už jen vykreslí z dat. Editor live-preview (`block.js`) používá JS dvojče `scheduleAxis.js` implementující identický algoritmus; parita s C# je zamčená sdílenými golden-vector fixtures (C# xUnit + JS `node:test`).

**Tech Stack:** .NET 8 / C#, xUnit + FluentAssertions, ASP.NET Razor (.cshtml), vanilla ESM JavaScript, `node:test` (nově), CSS (site.css).

**Spec:** `docs/superpowers/specs/2026-06-23-harmonogram-osa-redesign-design.md`
**Impact analýza (závazné úpravy zapracovány níže):** `docs/superpowers/plans/2026-06-23-harmonogram-osa-redesign-impact.md`

> **AMENDMENTY z impact analýzy (P1–P3, závazné):**
> - **Gantt seam:** `timeline.js` funkce `buildTimelineAxisTicks` a `renderTimelineAxis(axis,start,end)` se **NEMAŽOU** — používá je živý `gantt.js` a vyžaduje je `ScheduleJsSplitTests`. Nová cesta = **přidaná** funkce `renderTicksFromList`. Gantt board zůstává mimo rozsah.
> - **Breakdown markery:** nullable `TodayPct` se musí podmínit i v breakdown markeru (`_ScheduleBlock.cshtml:241`), ne jen overview.
> - **JS testy/fixtures mimo `wwwroot`:** leží v `tests/js/schedule/` (jinak by se publikovaly/servírovaly). `package.json` test glob a cesty v C# golden testu tomu odpovídají.

## Global Constraints

- **„Dnes" = lokální pražské datum** všude. Server: `TimeProvider.GetLocalNow().Date`. JS: čte `data-schedule-today` (ISO `yyyy-MM-dd`), NIKDY `new Date()` pro „dnes".
- **Osa per-karta** (měsíce dané karty), ne sdílená napříč kartami.
- **Jeden algoritmus, dvě implementace** (C# + JS) musí dávat bit-identické výsledky na golden-vector fixtures.
- **DRY/YAGNI/TDD**, časté commity. Žádné nové NuGet/NPM závislosti mimo vestavěný `node:test`.
- **Datumy se počítají v celých dnech** (`.Date`), DST-bezpečně (C# `DateTime.Date` rozdíly; JS `diffCalendarDays` přes `round`).
- Commit message footer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## Kanonický algoritmus osy (závazná reference pro C# i JS)

Vstup: `start` (datum), `deadline` (datum), `today` (lokální datum), `steps[]` = výsledek `ScheduleDateCalculator.Compute` (každý má `PlanStart, PlanEnd, MaSkutecnost, SkutecnostStart, SkutecnostEnd`).

```
firstDayOfMonth(d)  = nový DateTime(d.Year, d.Month, 1)
lastDayOfMonth(d)   = firstDayOfMonth(d).AddMonths(1).AddDays(-1)
isLastDayOfMonth(d) = d == lastDayOfMonth(d)

contentEnd = max(deadline,
                 max(PlanEnd přes steps),
                 max(SkutecnostEnd přes steps kde MaSkutecnost))     // dnes je zahrnut přes aktuální krok

axisStart = firstDayOfMonth(start)
axisEnd   = isLastDayOfMonth(contentEnd)
            ? lastDayOfMonth(contentEnd.AddDays(1))    // edge case: protáhnout o měsíc
            : lastDayOfMonth(contentEnd)
totalDays = max(1, (axisEnd - axisStart).Days)

pct(d)    = clamp((d - axisStart).Days * 100 / totalDays, 0, 100)

// měsíční ticky: 1. dne každého měsíce v [axisStart, axisEnd]
monthTicks = []
for m = axisStart; m <= axisEnd; m = m.AddMonths(1):
    monthTicks.add({ leftPct: (m - axisStart).Days * 100 / totalDays, label: $"{m.Month:00}/{m.Year}" })

// markery: null = nevykreslit (mimo interval)
todayPct    = (axisStart <= today    <= axisEnd) ? pct(today)    : null
deadlinePct = (axisStart <= deadline <= axisEnd) ? pct(deadline) : null

// segmenty: pro každý krok
planLeftPct/planWidthPct       z (PlanStart, PlanEnd)
actualLeftPct/actualWidthPct   z (SkutecnostStart, SkutecnostEnd) kde MaSkutecnost
```

Pozn.: `axisEnd` se počítá z `contentEnd`, ne přímo z `today`. U běžícího úkolu po termínu už `ScheduleDateCalculator` nastaví `SkutecnostEnd` aktuálního kroku na `today`, takže `contentEnd ≥ today` a edge-case pravidlo zajistí, že „dnes" nesedí na 100 %.

---

# FÁZE 1 — Statická karta (server autoritativní)

### Task 1: Rozšířit `ScheduleBarLayout` o měsíční ticky a nullable markery

**Files:**
- Modify: `PmTracker.Web/Services/Schedules/ScheduleBarLayoutCalculator.cs`
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleBarLayoutCalculatorTests.cs`

**Interfaces:**
- Produces: `record ScheduleMonthTick(double LeftPct, string Label)`. `ScheduleBarLayout(DateTime AxisStart, DateTime AxisEnd, int TotalDays, double? TodayPct, double? DeadlinePct, IReadOnlyList<ScheduleBarSegment> Segments, IReadOnlyList<ScheduleMonthTick> MonthTicks)`. `ScheduleBarLayoutCalculator.Compute(DateTime start, DateTime deadline, DateTime today, IReadOnlyList<ScheduleDateStepResult> steps) -> ScheduleBarLayout` (signatura beze změny, mění se tělo + návratový tvar).

- [ ] **Step 1: Upravit existující testy na nový tvar osy (měsíční přichycení)**

V `ScheduleBarLayoutCalculatorTests.cs` přepiš první test tak, aby očekával měsíčně přichycenou osu. Nahraď tělo testu `Compute_TwoSteps_PositionsPlanAndActualSegmentsAsPercentOfAxis`:

```csharp
[Fact]
public void Compute_TwoSteps_AxisSnapsToWholeMonths()
{
    // start 1.1., deadline 1.2. → contentEnd = 1.2. (není konec měsíce) → axisEnd = 28.2.2026.
    var steps = new[]
    {
        new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: true,  Start, new DateTime(2026, 1, 13), HarmonogramKrokStav.Splneno, JeAktualniKrok: false),
        new ScheduleDateStepResult(2, new DateTime(2026, 1, 11), new DateTime(2026, 1, 21), MaSkutecnost: false, new DateTime(2026, 1, 13), new DateTime(2026, 1, 13), HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
    };

    var layout = ScheduleBarLayoutCalculator.Compute(
        Start, deadline: new DateTime(2026, 2, 1), today: new DateTime(2026, 1, 15), steps);

    layout.AxisStart.Should().Be(new DateTime(2026, 1, 1));
    layout.AxisEnd.Should().Be(new DateTime(2026, 2, 28)); // poslední den měsíce contentEnd (1.2.)
    layout.TotalDays.Should().Be(58); // 1.1. → 28.2. = 58 dní

    layout.TodayPct.Should().NotBeNull();
    layout.TodayPct!.Value.Should().BeApproximately(14 * 100.0 / 58, 0.01);
    layout.DeadlinePct!.Value.Should().BeApproximately(31 * 100.0 / 58, 0.01);

    var s1 = layout.Segments.Single(s => s.Poradi == 1);
    s1.PlanLeftPct.Should().BeApproximately(0.0, 0.01);
    s1.PlanWidthPct.Should().BeApproximately(10 * 100.0 / 58, 0.01);
    s1.ActualWidthPct.Should().BeApproximately(12 * 100.0 / 58, 0.01);
}
```

- [ ] **Step 2: Přidat testy pro nové chování (edge case, skrytí markeru, ticky)**

Přidej do téhož souboru:

```csharp
[Fact]
public void Compute_ContentEndIsLastDayOfMonth_ExtendsAxisByOneMonth()
{
    // contentEnd = 31.3. (poslední den měsíce) → axisEnd = 30.4.
    var steps = new[]
    {
        new ScheduleDateStepResult(1, Start, new DateTime(2026, 3, 31), MaSkutecnost: false, Start, Start, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
    };
    var layout = ScheduleBarLayoutCalculator.Compute(
        Start, deadline: new DateTime(2026, 3, 31), today: new DateTime(2026, 2, 1), steps);

    layout.AxisEnd.Should().Be(new DateTime(2026, 4, 30));
}

[Fact]
public void Compute_TodayOutsideAxis_TodayPctIsNull()
{
    // úkol v budoucnu: start a plán v dubnu, dnes v lednu → dnes před osou.
    var aprilStart = new DateTime(2026, 4, 1);
    var steps = new[]
    {
        new ScheduleDateStepResult(1, aprilStart, new DateTime(2026, 4, 10), MaSkutecnost: false, aprilStart, aprilStart, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
    };
    var layout = ScheduleBarLayoutCalculator.Compute(
        aprilStart, deadline: new DateTime(2026, 4, 10), today: new DateTime(2026, 1, 15), steps);

    layout.TodayPct.Should().BeNull();
}

[Fact]
public void Compute_MonthTicks_AreFirstDayOfEachMonth()
{
    // osa 1.1. → 28.2. → ticky 1.1.(0%) a 1.2.
    var steps = new[]
    {
        new ScheduleDateStepResult(1, Start, new DateTime(2026, 1, 11), MaSkutecnost: false, Start, Start, HarmonogramKrokStav.Ceka, JeAktualniKrok: false),
    };
    var layout = ScheduleBarLayoutCalculator.Compute(
        Start, deadline: new DateTime(2026, 2, 1), today: new DateTime(2026, 1, 15), steps);

    layout.MonthTicks.Should().HaveCount(2);
    layout.MonthTicks[0].LeftPct.Should().BeApproximately(0.0, 0.01);
    layout.MonthTicks[0].Label.Should().Be("01/2026");
    layout.MonthTicks[1].Label.Should().Be("02/2026");
    layout.MonthTicks[1].LeftPct.Should().BeApproximately(31 * 100.0 / 58, 0.01);
}
```

- [ ] **Step 3: Spustit testy — musí selhat (kompilace/aserce)**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleBarLayoutCalculatorTests`
Expected: FAIL (chybí `MonthTicks`, `TodayPct` není nullable, axisEnd jiný).

- [ ] **Step 4: Implementovat nový kalkulátor**

Přepiš `ScheduleBarLayoutCalculator.cs` na:

```csharp
namespace PmTracker.Web.Services.Schedules;

public sealed record ScheduleBarSegment(
    int Poradi, double PlanLeftPct, double PlanWidthPct,
    bool HasActual, double ActualLeftPct, double ActualWidthPct);

/// <summary>Měsíční tick osy — pozice (%) a popisek „MM/RRRR".</summary>
public sealed record ScheduleMonthTick(double LeftPct, string Label);

/// <summary>Kompletní rozložení baru — osa přichycená na měsíce + markery + segmenty + měsíční ticky.</summary>
public sealed record ScheduleBarLayout(
    DateTime AxisStart, DateTime AxisEnd, int TotalDays,
    double? TodayPct, double? DeadlinePct,
    IReadOnlyList<ScheduleBarSegment> Segments,
    IReadOnlyList<ScheduleMonthTick> MonthTicks);

public static class ScheduleBarLayoutCalculator
{
    private static DateTime FirstDayOfMonth(DateTime d) => new(d.Year, d.Month, 1);
    private static DateTime LastDayOfMonth(DateTime d) => FirstDayOfMonth(d).AddMonths(1).AddDays(-1);
    private static bool IsLastDayOfMonth(DateTime d) => d.Date == LastDayOfMonth(d).Date;

    public static ScheduleBarLayout Compute(
        DateTime start, DateTime deadline, DateTime today, IReadOnlyList<ScheduleDateStepResult> steps)
    {
        var s = start.Date;
        var contentEnd = deadline.Date;
        foreach (var step in steps)
        {
            if (step.PlanEnd.Date > contentEnd) contentEnd = step.PlanEnd.Date;
            if (step.MaSkutecnost && step.SkutecnostEnd.Date > contentEnd) contentEnd = step.SkutecnostEnd.Date;
        }

        var axisStart = FirstDayOfMonth(s);
        var axisEnd = IsLastDayOfMonth(contentEnd)
            ? LastDayOfMonth(contentEnd.AddDays(1))
            : LastDayOfMonth(contentEnd);
        var totalDays = Math.Max(1, (axisEnd - axisStart).Days);

        double Pct(DateTime d) => Math.Clamp((d.Date - axisStart).Days * 100.0 / totalDays, 0.0, 100.0);
        double Width(DateTime from, DateTime to) =>
            Math.Clamp(Math.Max(0, (to.Date - from.Date).Days) * 100.0 / totalDays, 0.0, 100.0);
        bool InAxis(DateTime d) => d.Date >= axisStart && d.Date <= axisEnd;

        var segments = steps
            .OrderBy(x => x.Poradi)
            .Select(x => new ScheduleBarSegment(
                x.Poradi,
                Pct(x.PlanStart), Width(x.PlanStart, x.PlanEnd),
                x.MaSkutecnost,
                x.MaSkutecnost ? Pct(x.SkutecnostStart) : 0.0,
                x.MaSkutecnost ? Width(x.SkutecnostStart, x.SkutecnostEnd) : 0.0))
            .ToList();

        var ticks = new List<ScheduleMonthTick>();
        for (var m = axisStart; m <= axisEnd; m = m.AddMonths(1))
            ticks.Add(new ScheduleMonthTick((m - axisStart).Days * 100.0 / totalDays, $"{m.Month:00}/{m.Year}"));

        return new ScheduleBarLayout(
            axisStart, axisEnd, totalDays,
            InAxis(today) ? Pct(today) : null,
            InAxis(deadline) ? Pct(deadline) : null,
            segments, ticks);
    }
}
```

- [ ] **Step 5: Spustit testy — zelené**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleBarLayoutCalculatorTests`
Expected: PASS. (Pozn.: zbylé existující testy v souboru, které asertovaly starý axisEnd, uprav stejným principem na měsíčně přichycenou osu — projdi je jeden po druhém.)

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Schedules/ScheduleBarLayoutCalculator.cs PmTracker.Tests.Unit/Schedule/ScheduleBarLayoutCalculatorTests.cs
git commit -m "feat(harmonogram): osa přichycená na měsíce + měsíční ticky + nullable markery

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 2: „Dnes" = lokální datum (server) + propsat do VM

**Files:**
- Modify: `PmTracker.Web/Services/ProjectService.ScheduleComposition.cs:45`
- Modify: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs:124`
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` (HarmonogramBlockViewModel — přidat `DateTime Today`)
- Modify: `PmTracker.Web/Services/ProjectService.ScheduleBlockComposition.cs` (BuildScheduleBlockViewModel — naplnit Today)
- Test: `PmTracker.Tests.Unit/Schedule/HarmonogramDateBlokBuilderBarLayoutTests.cs`

**Interfaces:**
- Consumes: `ScheduleBarLayoutCalculator.Compute` (Task 1).
- Produces: `HarmonogramBlockViewModel.Today` (DateTime, lokální datum) — pro render `data-schedule-today`.

- [ ] **Step 1: Test — VM nese lokální dnešek**

Přidej test ověřující, že `BuildScheduleBlockViewModel` (přes `HarmonogramDateBlokBuilder`/composition) nastaví `Today`. Pokud composition vyžaduje DB, otestuj jednodušší kontrakt: že `HarmonogramBlockViewModel` má vlastnost `Today` typu `DateTime` (smoke), a hlavní pokrytí lokálního data je v Api testu (Task 6). Minimální unit:

```csharp
[Fact]
public void HarmonogramBlockViewModel_HasTodayProperty()
{
    var vm = new PmTracker.Web.Models.ViewModels.HarmonogramBlockViewModel { Today = new DateTime(2026, 6, 23) };
    vm.Today.Should().Be(new DateTime(2026, 6, 23));
}
```

- [ ] **Step 2: Spustit — selže (chybí `Today`)**

Run: `dotnet build PmTracker.Web`
Expected: FAIL — `HarmonogramBlockViewModel` nemá `Today`.

- [ ] **Step 3: Přidat vlastnost + naplnit lokálním datem**

V `HarmonogramBlockViewModel` přidej:

```csharp
/// <summary>Lokální (pražské) dnešní datum — pro marker „Dnes" v UI. Sjednocený zdroj s osou.</summary>
public DateTime Today { get; init; }
```

V `ProjectService.ScheduleBlockComposition.cs` `BuildScheduleBlockViewModel` přidej parametr `DateTime today` a do new VM `Today = today.Date`.

V `ProjectService.ScheduleComposition.cs:45` změň:

```csharp
var todayDate = timeProvider.GetLocalNow().Date;
```

a předej `todayDate` i do `BuildScheduleBlockViewModel(..., today: todayDate)`.

V `ProjectService.RecordEditorComposition.cs:124` stejně: `var todayDate = timeProvider.GetLocalNow().Date;` a předej do composition.

- [ ] **Step 4: Spustit testy + build**

Run: `dotnet build PmTracker.Web && dotnet test PmTracker.Tests.Unit --filter HarmonogramDateBlokBuilder`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ProjectService.ScheduleComposition.cs PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs PmTracker.Web/Services/ProjectService.ScheduleBlockComposition.cs PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs PmTracker.Tests.Unit/Schedule/HarmonogramDateBlokBuilderBarLayoutTests.cs
git commit -m "feat(harmonogram): dnes = lokální datum (GetLocalNow), propsáno do VM

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 3: Render — emit ticky + dnes, overlay markery, bez +1px

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml`
- Test: `PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs`

**Interfaces:**
- Consumes: `Model.OverviewLayout` (`ScheduleBarLayout` s `MonthTicks`, nullable `TodayPct/DeadlinePct`), `Model.Today`.
- Produces: HTML s `data-schedule-ticks` (JSON pole `{leftPct,label}`), `data-schedule-today` (ISO), markery v `.schedule-overview-markers` overlay; segmenty bez `+1px`.

- [ ] **Step 1: Api test — vykreslené HTML obsahuje ticky a dnes**

V `ProjectHarmonogramRenderTests.cs` přidej test, který načte harmonogram tab projektu se záznamem a ověří přítomnost `data-schedule-ticks` a `data-schedule-today` a že segment plánu nemá `+ 1px`:

```csharp
[Fact]
public async Task HarmonogramTab_RendersMonthTicksAndTodayData()
{
    var html = await GetHarmonogramTabHtmlAsync(); // existující helper v tomto souboru
    html.Should().Contain("data-schedule-ticks");
    html.Should().Contain("data-schedule-today");
    html.Should().NotContain("+ 1px"); // segmenty už šířku nenafukují
}
```

(Pokud helper `GetHarmonogramTabHtmlAsync` neexistuje, použij stávající vzor v souboru pro načtení `/Projekty/Detail/{id}?tab=gant` přes `WebApplicationFactory`.)

- [ ] **Step 2: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Api --filter ProjectHarmonogramRenderTests`
Expected: FAIL — atributy a overlay zatím nejsou.

- [ ] **Step 3: Upravit markup**

V `_ScheduleBlock.cshtml`:

1. Do kořene bloku přidej `data-schedule-today="@Model.Today.ToString("yyyy-MM-dd")"`.
2. V sekci overview (po `@foreach (var row ...)`) nahraď dnešní/termínový marker uvnitř tracku za **overlay vrstvu** + serializované ticky. Konkrétně nahraď dvojici `<div class="@overviewMarkerCssClass today" ...>` a `... deadline ...` blokem:

```cshtml
@if (ovl is not null)
{
    <div class="schedule-overview-markers" aria-hidden="true"
         data-schedule-ticks="@System.Text.Json.JsonSerializer.Serialize(ovl.MonthTicks.Select(t => new { left = t.LeftPct, label = t.Label }))">
        @if (ovl.TodayPct.HasValue)
        {
            <div class="schedule-overview-marker today" data-schedule-marker="today"
                 style="left:@Pct(ovl.TodayPct.Value)" title="Dnes @Model.Today.ToString("dd.MM.yyyy")"></div>
        }
        @if (ovl.DeadlinePct.HasValue)
        {
            <div class="schedule-overview-marker deadline" data-schedule-marker="deadline"
                 style="left:@Pct(ovl.DeadlinePct.Value)" title="Termín"></div>
        }
    </div>
}
```

3. V segmentu změň `width:calc({Pct(width)} + 1px)` na `width:{Pct(width)}` (řádek ~131): `positionStyle = hide ? "display:none;" : $"left:{Pct(left)};width:{Pct(width)};";`

(Pozn.: overlay `.schedule-overview-markers` se vykreslí jednou na track-řádek; markery v něm nejsou clipnuté — viz CSS Task 4.)

- [ ] **Step 4: Spustit — zelené**

Run: `dotnet test PmTracker.Tests.Api --filter ProjectHarmonogramRenderTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs
git commit -m "feat(harmonogram): render měsíčních ticků + overlay markerů, segmenty bez +1px

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 4: CSS — overlay markery (translateX, bez ořezu) + měsíční gridlines

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css` (kolem `.schedule-overview-marker` ~3121-3139)
- Test: `PmTracker.Tests.Unit/Architecture/` — přidej jednoduchý file-text test (volitelně) nebo ověření vizuálně. Zde stačí ruční vizuální kontrola + architektura test níže.

**Interfaces:**
- Consumes: `.schedule-overview-markers` (overlay), `.schedule-overview-marker.today/.deadline` z Task 3.

- [ ] **Step 1: Architektura test — marker má translateX a overlay není clipnutá**

Přidej `PmTracker.Tests.Unit/Architecture/ScheduleMarkerCssTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class ScheduleMarkerCssTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "PmTracker.Web", "wwwroot", "css", "site.css"));
    }

    [Fact]
    public void OverviewMarker_UsesTranslateXCenter()
    {
        var css = Css();
        css.Should().Contain(".schedule-overview-markers");
        css.Should().Contain("transform: translateX(-50%)");
    }
}
```

- [ ] **Step 2: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleMarkerCssTests`
Expected: FAIL.

- [ ] **Step 3: Přidat CSS**

Do `site.css` přidej (a uprav stávající `.schedule-overview-marker`):

```css
/* Overlay vrstva markerů NAD stopou — není clipnutá overflow:hidden stopy,
   takže marker „Dnes"/„Termín" se nikdy neořízne na kraji. */
.schedule-overview-markers {
    position: absolute;
    inset: 0;
    pointer-events: none;
    z-index: 6;
}

.schedule-overview-marker {
    position: absolute;
    top: -3px;
    bottom: -3px;
    width: 2px;
    transform: translateX(-50%);   /* střed čáry na přesné pozici data */
    opacity: 0.92;
}

/* měsíční gridline (slabá svislá linka na 1. dne měsíce) */
.schedule-overview-gridline {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 1px;
    transform: translateX(-50%);
    background: rgba(15, 23, 42, 0.10);
    pointer-events: none;
}
```

Aby byla overlay relativní vůči stopě, ujisti se, že `.schedule-overview-track` má `position: relative` (má — řádek ~3067). Overlay `.schedule-overview-markers` musí být **přímý potomek tracku**, ne segmentu.

- [ ] **Step 4: Spustit — zelené + ruční vizuální kontrola**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleMarkerCssTests`
Expected: PASS. Poté ručně otevři projekt → záložka Harmonogram a zkontroluj, že „Dnes" sedí a není uříznuté.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Unit/Architecture/ScheduleMarkerCssTests.cs
git commit -m "feat(harmonogram): CSS overlay markery (translateX, bez ořezu) + měsíční gridline

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 5: `timeline.js` — vykreslit ticky ze serveru (konec vzorkování)

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/timeline.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/index.js` (statický render čte ticky z data atributu)
- Test: `PmTracker.Web/wwwroot/js/modules/schedule/__tests__/timeline.test.js` (Task 7 infra)

**Interfaces:**
- Consumes: na statické kartě `[data-schedule-ticks]` (JSON `{left,label}[]`) místo `data-axis-start/end` vzorkování.
- Produces: `renderTicksFromList(container, ticks)` — vykreslí předané ticky + prořídnutí popisků; `renderStaticTimelineAxes(scope)` přepnuto na čtení `data-schedule-ticks`.

- [ ] **Step 1: Test (node:test, vznikne v Task 7) — render z listu**

Zapiš očekávání do `__tests__/timeline.test.js`:

```javascript
import { test } from "node:test";
import assert from "node:assert/strict";
import { buildTicksFromServer } from "../timeline.js";

test("buildTicksFromServer parsuje data-schedule-ticks", () => {
    const ticks = buildTicksFromServer('[{"left":0,"label":"01/2026"},{"left":50,"label":"02/2026"}]');
    assert.equal(ticks.length, 2);
    assert.equal(ticks[0].left, 0);
    assert.equal(ticks[1].label, "02/2026");
});

test("buildTicksFromServer vrací [] pro prázdné", () => {
    assert.deepEqual(buildTicksFromServer(""), []);
    assert.deepEqual(buildTicksFromServer(null), []);
});
```

- [ ] **Step 2: Spustit — selže (funkce neexistuje)**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/timeline.test.js`
Expected: FAIL.

- [ ] **Step 3: Implementovat render z listu**

V `timeline.js` přidej:

```javascript
export function buildTicksFromServer(json) {
    if (!json) return [];
    try {
        const parsed = JSON.parse(json);
        return Array.isArray(parsed)
            ? parsed.filter((t) => t && Number.isFinite(t.left)).map((t) => ({ left: t.left, label: String(t.label ?? "") }))
            : [];
    } catch {
        return [];
    }
}
```

Refaktoruj `renderTimelineAxis` tak, aby místo `buildTimelineAxisTicks(axisStart, axisEnd, ...)` přijal ticky přímo (nová cesta), a `renderStaticTimelineAxes` aby je četl z `container.dataset.scheduleTicks ?? closestTrack`. Konkrétně: pro statickou osu volej novou variantu, která bere `ticks` z `buildTicksFromServer`. Vzorkovací `buildTimelineAxisTicks` ponech jen jako fallback pro editor do Task 9 (pak se odstraní). Zachovej label-thinning (overlap hiding) a edge-inset ODSTRAŇ — ticky kresli na `left%` přes plnou šířku (sjednocení s pruhy).

- [ ] **Step 4: Spustit — zelené**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/timeline.test.js`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/schedule/timeline.js PmTracker.Web/wwwroot/js/modules/schedule/index.js PmTracker.Web/wwwroot/js/modules/schedule/__tests__/timeline.test.js
git commit -m "feat(harmonogram): timeline.js vykresluje měsíční ticky ze serveru (konec vzorkování)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

# FÁZE 2 — Kanonický JS algoritmus + editor + parita

### Task 6: JS test infra (`node:test`) + golden-vector fixtures

**Files:**
- Create: `package.json` (root, minimální)
- Create: `PmTracker.Web/wwwroot/js/modules/schedule/__fixtures__/axis-cases.json`
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleAxisGoldenVectorTests.cs`

**Interfaces:**
- Produces: sdílené fixtures (`axis-cases.json`) — pole `{ name, input:{start,deadline,today,steps:[{poradi,planStart,planEnd,maSkutecnost,skutecnostStart,skutecnostEnd}]}, expected:{axisStart,axisEnd,totalDays,todayPct,deadlinePct,segments:[...],monthTicks:[...]} }`. Čtou C# i JS.

- [ ] **Step 1: Vytvořit fixtures**

Vytvoř `axis-cases.json` se 3 případy (datumy ISO, pct na 4 desetinná místa). Příklad jednoho:

```json
[
  {
    "name": "two-steps-month-snap",
    "input": {
      "start": "2026-01-01", "deadline": "2026-02-01", "today": "2026-01-15",
      "steps": [
        {"poradi":1,"planStart":"2026-01-01","planEnd":"2026-01-11","maSkutecnost":true,"skutecnostStart":"2026-01-01","skutecnostEnd":"2026-01-13"},
        {"poradi":2,"planStart":"2026-01-11","planEnd":"2026-01-21","maSkutecnost":false,"skutecnostStart":"2026-01-13","skutecnostEnd":"2026-01-13"}
      ]
    },
    "expected": {
      "axisStart":"2026-01-01","axisEnd":"2026-02-28","totalDays":58,
      "todayPct":24.1379,"deadlinePct":53.4483,
      "monthTicks":[{"left":0.0,"label":"01/2026"},{"left":53.4483,"label":"02/2026"}],
      "segments":[
        {"poradi":1,"planLeft":0.0,"planWidth":17.2414,"hasActual":true,"actualLeft":0.0,"actualWidth":20.6897},
        {"poradi":2,"planLeft":17.2414,"planWidth":17.2414,"hasActual":false,"actualLeft":0.0,"actualWidth":0.0}
      ]
    }
  }
]
```

(Doplň case `last-day-of-month-edge` se `today=2026-03-31` → axisEnd `2026-04-30`, a case `today-outside` → `todayPct:null`. Hodnoty dopočítej přesně dle algoritmu.)

- [ ] **Step 2: Minimální package.json**

```json
{
  "name": "pmtracker-js-tests",
  "private": true,
  "type": "module",
  "scripts": {
    "test": "node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/"
  }
}
```

- [ ] **Step 3: C# golden-vector test (čte tytéž fixtures)**

```csharp
using System.IO;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleAxisGoldenVectorTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void Compute_MatchesGoldenVectors()
    {
        var path = Path.Combine(RepoRoot(), "PmTracker.Web", "wwwroot", "js", "modules", "schedule", "__fixtures__", "axis-cases.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var c in doc.RootElement.EnumerateArray())
        {
            var inp = c.GetProperty("input");
            var steps = inp.GetProperty("steps").EnumerateArray().Select(s => new ScheduleDateStepResult(
                s.GetProperty("poradi").GetInt32(),
                DateTime.Parse(s.GetProperty("planStart").GetString()!),
                DateTime.Parse(s.GetProperty("planEnd").GetString()!),
                s.GetProperty("maSkutecnost").GetBoolean(),
                DateTime.Parse(s.GetProperty("skutecnostStart").GetString()!),
                DateTime.Parse(s.GetProperty("skutecnostEnd").GetString()!),
                HarmonogramKrokStav.Ceka, false)).ToList();

            var layout = ScheduleBarLayoutCalculator.Compute(
                DateTime.Parse(inp.GetProperty("start").GetString()!),
                DateTime.Parse(inp.GetProperty("deadline").GetString()!),
                DateTime.Parse(inp.GetProperty("today").GetString()!),
                steps);

            var exp = c.GetProperty("expected");
            layout.AxisEnd.Should().Be(DateTime.Parse(exp.GetProperty("axisEnd").GetString()!),
                because: c.GetProperty("name").GetString());
            layout.TotalDays.Should().Be(exp.GetProperty("totalDays").GetInt32());
            if (exp.GetProperty("todayPct").ValueKind == JsonValueKind.Null)
                layout.TodayPct.Should().BeNull();
            else
                layout.TodayPct!.Value.Should().BeApproximately(exp.GetProperty("todayPct").GetDouble(), 0.01);
        }
    }
}
```

- [ ] **Step 4: Spustit C# test — zelené**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleAxisGoldenVectorTests`
Expected: PASS (algoritmus z Task 1 sedí na fixtures; pokud ne, oprav fixtures hodnoty dle skutečného výpočtu).

- [ ] **Step 5: Commit**

```bash
git add package.json PmTracker.Web/wwwroot/js/modules/schedule/__fixtures__/axis-cases.json PmTracker.Tests.Unit/Schedule/ScheduleAxisGoldenVectorTests.cs
git commit -m "test(harmonogram): golden-vector fixtures + C# parity test + node:test infra

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 7: `scheduleAxis.js` — JS dvojče algoritmu + parity test

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/schedule/scheduleAxis.js`
- Create: `PmTracker.Web/wwwroot/js/modules/schedule/__tests__/scheduleAxis.test.js`

**Interfaces:**
- Consumes: `axis-cases.json` (Task 6), `diffCalendarDays`/`addCalendarDays` z `../utils.js`.
- Produces: `computeAxisLayout({ start, deadline, today, steps }) -> { axisStart, axisEnd, totalDays, todayPct|null, deadlinePct|null, segments[], monthTicks[] }` (Date objekty + čísla; pct na 4 desetinná).

- [ ] **Step 1: Parity test proti fixtures**

`__tests__/scheduleAxis.test.js`:

```javascript
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { computeAxisLayout } from "../scheduleAxis.js";

const cases = JSON.parse(readFileSync(fileURLToPath(new URL("../__fixtures__/axis-cases.json", import.meta.url))));
const d = (s) => { const [y, m, day] = s.split("-").map(Number); return new Date(y, m - 1, day); };
const iso = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
const approx = (a, b) => Math.abs(a - b) < 0.01;

for (const c of cases) {
    test(`parity: ${c.name}`, () => {
        const layout = computeAxisLayout({
            start: d(c.input.start), deadline: d(c.input.deadline), today: d(c.input.today),
            steps: c.input.steps.map((s) => ({
                poradi: s.poradi, planStart: d(s.planStart), planEnd: d(s.planEnd),
                maSkutecnost: s.maSkutecnost, skutecnostStart: d(s.skutecnostStart), skutecnostEnd: d(s.skutecnostEnd)
            }))
        });
        assert.equal(iso(layout.axisEnd), c.expected.axisEnd);
        assert.equal(layout.totalDays, c.expected.totalDays);
        if (c.expected.todayPct === null) assert.equal(layout.todayPct, null);
        else assert.ok(approx(layout.todayPct, c.expected.todayPct), `todayPct ${layout.todayPct} != ${c.expected.todayPct}`);
    });
}
```

- [ ] **Step 2: Spustit — selže (modul neexistuje)**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/scheduleAxis.test.js`
Expected: FAIL.

- [ ] **Step 3: Implementovat `scheduleAxis.js`**

```javascript
import { addCalendarDays, diffCalendarDays } from "../utils.js";

const firstDayOfMonth = (d) => new Date(d.getFullYear(), d.getMonth(), 1);
const lastDayOfMonth = (d) => new Date(d.getFullYear(), d.getMonth() + 1, 0);
const isLastDayOfMonth = (d) => d.getDate() === lastDayOfMonth(d).getDate();
const clamp = (v) => Math.max(0, Math.min(100, v));
const round4 = (v) => Math.round(v * 10000) / 10000;

export function computeAxisLayout({ start, deadline, today, steps }) {
    let contentEnd = deadline;
    for (const s of steps) {
        if (diffCalendarDays(s.planEnd, contentEnd) > 0) contentEnd = s.planEnd;
        if (s.maSkutecnost && diffCalendarDays(s.skutecnostEnd, contentEnd) > 0) contentEnd = s.skutecnostEnd;
    }

    const axisStart = firstDayOfMonth(start);
    const axisEnd = isLastDayOfMonth(contentEnd)
        ? lastDayOfMonth(addCalendarDays(contentEnd, 1))
        : lastDayOfMonth(contentEnd);
    const totalDays = Math.max(1, diffCalendarDays(axisEnd, axisStart));

    const pct = (d) => round4(clamp((diffCalendarDays(d, axisStart) * 100) / totalDays));
    const width = (a, b) => round4(clamp((Math.max(0, diffCalendarDays(b, a)) * 100) / totalDays));
    const inAxis = (d) => diffCalendarDays(d, axisStart) >= 0 && diffCalendarDays(axisEnd, d) >= 0;

    const segments = steps.slice().sort((a, b) => a.poradi - b.poradi).map((s) => ({
        poradi: s.poradi,
        planLeft: pct(s.planStart), planWidth: width(s.planStart, s.planEnd),
        hasActual: s.maSkutecnost,
        actualLeft: s.maSkutecnost ? pct(s.skutecnostStart) : 0,
        actualWidth: s.maSkutecnost ? width(s.skutecnostStart, s.skutecnostEnd) : 0
    }));

    const monthTicks = [];
    for (let m = new Date(axisStart); m <= axisEnd; m = new Date(m.getFullYear(), m.getMonth() + 1, 1)) {
        monthTicks.push({
            left: round4((diffCalendarDays(m, axisStart) * 100) / totalDays),
            label: `${String(m.getMonth() + 1).padStart(2, "0")}/${m.getFullYear()}`
        });
    }

    return {
        axisStart, axisEnd, totalDays,
        todayPct: inAxis(today) ? pct(today) : null,
        deadlinePct: inAxis(deadline) ? pct(deadline) : null,
        segments, monthTicks
    };
}
```

- [ ] **Step 4: Spustit — zelené (parita)**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/scheduleAxis.test.js`
Expected: PASS. Pokud se C# a JS liší, je to skutečný rozpor — sjednoť (typicky zaokrouhlení); fixtures jsou autorita.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/schedule/scheduleAxis.js PmTracker.Web/wwwroot/js/modules/schedule/__tests__/scheduleAxis.test.js
git commit -m "feat(harmonogram): scheduleAxis.js — JS dvojče algoritmu osy + parity test

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 8: Editor — `block.js` používá `scheduleAxis.js`, dnes z data atributu

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/block.js`
- Test: `PmTracker.Web/wwwroot/js/modules/schedule/__tests__/blockAxis.test.js`

**Interfaces:**
- Consumes: `computeAxisLayout` (Task 7); `root.dataset.scheduleToday`.
- Produces: editor overview/breakdown markery a segmenty pozicované z `computeAxisLayout`; `buildScheduleScale` a `resolveBreakdownAxis` odstraněny.

- [ ] **Step 1: Test — block čte dnes z data atributu, ne new Date()**

`__tests__/blockAxis.test.js` (jednotka nad pomocnou funkcí, kterou vyextrahujeme):

```javascript
import { test } from "node:test";
import assert from "node:assert/strict";
import { resolveToday } from "../block.js";

test("resolveToday čte data-schedule-today", () => {
    const el = { dataset: { scheduleToday: "2026-06-23" } };
    const t = resolveToday(el);
    assert.equal(t.getFullYear(), 2026);
    assert.equal(t.getMonth(), 5);
    assert.equal(t.getDate(), 23);
});
```

- [ ] **Step 2: Spustit — selže**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/blockAxis.test.js`
Expected: FAIL (`resolveToday` neexistuje).

- [ ] **Step 3: Refaktor block.js**

1. Importuj `computeAxisLayout` z `./scheduleAxis.js` a `parseIsoDate` (už je).
2. Přidej exportovanou pomocnou funkci:

```javascript
export function resolveToday(root) {
    const iso = root && root.dataset ? root.dataset.scheduleToday : null;
    return parseIsoDate(iso) || new Date();
}
```

3. V `renderOverview` nahraď `buildScheduleScale(...)` + lokální `new Date()` voláním `computeAxisLayout({ start: startDate, deadline: deadlineDate, today: resolveToday(this.root), steps: ... })` a markery/segmenty pozicуj z jeho výstupu (`layout.todayPct`, `layout.segments`). Marker při `todayPct === null` skryj (`display:none`).
4. Smaž funkce `buildScheduleScale` a metodu `resolveBreakdownAxis`; `renderBreakdown` přepoj na `computeAxisLayout` (stejná osa jako overview → rozpad sedí svisle).
5. Odstraň zbytek vzorkovacího `buildTimelineAxisTicks` použití pro editor (osu editoru vykresluj přes `timeline.js` z `layout.monthTicks`).

- [ ] **Step 4: Spustit — zelené**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/`
Expected: PASS (všechny JS testy).

- [ ] **Step 5: Render dnes do editoru**

V `_ScheduleBlock.cshtml` je `data-schedule-today` na kořeni (Task 3) — platí i pro editor (sdílená partial). Ověř, že editor kořen má `data-schedule-today` (má, je to stejný `<div data-schedule-block>`).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/schedule/block.js PmTracker.Web/wwwroot/js/modules/schedule/__tests__/blockAxis.test.js
git commit -m "feat(harmonogram): editor používá kanonický scheduleAxis.js + dnes z data atributu

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 9: Úklid `timeline.js` (smazat vzorkování) + zpevnění renderu osy

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/timeline.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule/index.js`, `filters.js`
- Test: existující JS testy (Task 5/7) musí zůstat zelené

**Interfaces:**
- Consumes: `monthTicks` (server na statice, `computeAxisLayout` v editoru).
- Produces: `timeline.js` bez `buildTimelineAxisTicks`/edge-inset; render osy spouštěn přes `ResizeObserver` na stopě.

- [ ] **Step 1: Odstranit vzorkovací funkci**

Smaž `buildTimelineAxisTicks` a `resolveTimelineAxisTickTargetCount` z `timeline.js` (už je nikdo nevolá po Task 5/8). Ponech label-thinning a `percentToAxisPx` BEZ edge-insetu (`edgeInsetPx = 0`).

- [ ] **Step 2: ResizeObserver místo rAF-retry**

Nahraď `queueTimelineAxisRetry` rAF smyčku jedním `ResizeObserver` na track containeru, který přerenderuje osu při změně šířky (a tím vyřeší lazy/šířka-0). Zachovej veřejné API `renderStaticTimelineAxes(scope)`.

- [ ] **Step 3: Spustit JS testy**

Run: `node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/`
Expected: PASS.

- [ ] **Step 4: Spustit JS-split architektura test (C#)**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleJsSplitTests`
Expected: PASS (pokud test odkazuje na smazané symboly, uprav ho na nový stav).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/schedule/timeline.js PmTracker.Web/wwwroot/js/modules/schedule/index.js PmTracker.Web/wwwroot/js/modules/schedule/filters.js PmTracker.Tests.Unit/Architecture/ScheduleJsSplitTests.cs
git commit -m "refactor(harmonogram): timeline.js bez vzorkování + ResizeObserver render osy

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

### Task 10: Plná verifikace + ruční vizuální kontrola + publish

**Files:** —

- [ ] **Step 1: Plné test sady**

Run: `dotnet test PmTracker.Tests.Unit && dotnet test PmTracker.Tests.Api && node --test PmTracker.Web/wwwroot/js/modules/schedule/__tests__/`
Expected: vše zelené. (Orthogonální pre-existing failures dle paměti `project_pre_existing_test_failures` — ověř, že nové selhání nepřibylo.)

- [ ] **Step 2: Ruční vizuální kontrola (kritéria úspěchu ze specu)**

Spusť app, projekt → Harmonogram:
- datum 20.3 sedí ve druhé třetině března vůči mřížce;
- „Dnes" na správném místě, neuříznuté; skryté u hotového/budoucího úkolu;
- otevři editor téhož záznamu — osa identická s kartou (plán/skutečnost/dnes);
- rozbal Rozpad — měřítko sedí s overview.

- [ ] **Step 3: Publish + zip (dle zvyklosti)**

Run: `dotnet publish PmTracker.Web -c Release` a vytvoř `publish.zip` v rootu (viz `feedback_publish_zip`).

- [ ] **Step 4: Commit (pokud něco doladěno)**

```bash
git add -A
git commit -m "chore(harmonogram): finální verifikace osy + publish

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Pozn. k DRY/rizikům (z analýzy)

- `timeline.js` je sdílený seam — Task 5/9 ho mění pro statiku i editor; ověřit v obou.
- `applyPlanChronologyBounds` a `queueRainbowSegmentRender` v `recalcAll` se nesmí rozbít (Task 8).
- Dark mode markery: ověřit `:root[data-theme="dark"] .schedule-overview-marker.*` (zůstávají barvy z `site.css` ~6649).
- `ScheduleDateCalculator` se NEMĚNÍ — měníme jen vrstvu osy. Sjednocení „dnes" v PriorityMatrix/Dashboard je mimo rozsah.
