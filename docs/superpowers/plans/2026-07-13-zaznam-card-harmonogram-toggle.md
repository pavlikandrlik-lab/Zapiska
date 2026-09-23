# Toggle Harmonogram na kartě záznamu — implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task (inline exekuce — user standard, žádní subagenti). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Toggle switch na kartě záznamu v záložce Záznamy přepne obsah karty mezi detailem (informace + vyjádření) a kompletním harmonogramem; akční tlačítka hlavičky se přeskládají (max 3 + svislé menu + switch).

**Architecture:** Nový lazy endpoint `RecordSchedulePartial` (třetí shell vedle detailu a vyjádření) vrací sdílený `_ScheduleBlock` v readonly módu s rozbaleným rozpadem; pohled karty řídí dataset+třída na `.record-card`; menu jede na existující floating-panel vrstvě. Persistence pohledu přes `recordRefresh.js` (výměna karty + preserve celého panelu).

**Tech Stack:** ASP.NET Core MVC (Razor partials), vanilla ES moduly, gov-design-system web components, xUnit + FluentAssertions, Playwright .NET.

**Spec:** `docs/superpowers/specs/2026-07-13-zaznam-card-harmonogram-toggle-design.md`

## Global Constraints

- **Commity se NEPROVÁDÍ** — session pravidlo „commity držím", uživatel verifikuje ručně. Místo commit kroků je na konci každého tasku verifikační checkpoint.
- **i15 CSS pravidla** (memory feedback_i15_edge_css_compat): žádné `:has()` v nosné logice, žádné `max-content`/`min-content`/`fit-content`, jen letité flex/grid základy.
- **gov komponenty:** `gov-form-switch` styluj přes `[checked]` (ne `:checked`); na gov-button hostu nikdy `.textContent` (Stencil slot relocation) — ikony/labely přepínat `hidden` na vnořených elementech; `instanceof HTMLButtonElement` nematchuje gov-button → `isButtonLike`.
- **Statická osa harmonogramu** vyžaduje serverové ticky (`data-schedule-ticks` + today/deadline pct) — bez nich JS spadne do rozjetého fallbacku.
- **Osy se kreslí až po zviditelnění** — měří šířku, ve skrytém prvku je nulová.
- **Side-effect JS moduly musí být explicitně importovány v `bootstrap.js`** (memory project_bundle_sync), jinak tichý fail.
- Texty UI česky: „Harmonogram", „Zobrazit na záložce Harmonogram", „Navrhnout změnu harmonogramu", „Nepodařilo se načíst harmonogram záznamu."
- Testovací příkazy: `dotnet test PmTracker.Tests.Unit --nologo`, `dotnet test PmTracker.Tests.Api --nologo`, E2E dle instrukcí Tasku 5. Známé pre-existing failure sady nezhoršovat (Api: 4× gantt).

---

### Task 1: Server — endpoint harmonogramu jednoho záznamu + BreakdownExpanded

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` (BreakdownExpanded na `HarmonogramBlockViewModel`)
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` (`ScheduleUrl` + nový `ZaznamScheduleBlockViewModel`)
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml` (řádek ~189, `hidden` podmíněně)
- Create: `PmTracker.Web/Services/ProjectService.RecordScheduleComposition.cs`
- Modify: `PmTracker.Web/Services/IProjectService.cs`, `PmTracker.Web/Services/IRecordService.cs`, `PmTracker.Web/Services/RecordService.cs` (delegace)
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` (endpoint + ScheduleUrl v Prepare)
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (~ř. 254, ScheduleUrl v Prepare)
- Create: `PmTracker.Web/Views/Projekty/_ZaznamSchedulePartial.cshtml`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (veřejný seed helper kroků)
- Test: `PmTracker.Tests.Api/Controllers/RecordScheduleTogglePartialTests.cs` (nový)

**Interfaces:**
- Produces: `Task<ZaznamScheduleBlockViewModel?> BuildRecordScheduleBlockAsync(int projectId, int recordId, CancellationToken ct = default)` na `IProjectService` i `IRecordService`; `ZaznamScheduleBlockViewModel { bool Stihame; HarmonogramBlockViewModel HarmonogramBlok; }`; GET `/Zaznamy/RecordSchedulePartial?projektId=&zaznamId=`; `ProjektZaznamCardShellViewModel.ScheduleUrl`; `HarmonogramBlockViewModel.BreakdownExpanded`; fixture `SeedDatumScheduleAsync(int recordId, DateTime startDate, IReadOnlySet<int> actualSteps)`.

- [ ] **Step 1: Fixture helper** — do `ApiSqlFixture.cs` přidej (vzor je privátní helper v `ProjectHarmonogramRenderTests.cs:323`; potřebné `using PmTracker.Web.Models.Entities;` už fixture pravděpodobně má — ověř):

```csharp
/// <summary>Datum-model seed: plán pro všech 10 kroků (týdenní rozestup), skutečnost jen pro
/// kroky v <paramref name="actualSteps"/>. Sdílený pro harmonogram render testy.</summary>
public async Task SeedDatumScheduleAsync(int recordId, DateTime startDate, IReadOnlySet<int> actualSteps)
{
    await using var dbContext = CreateDbContext();
    var now = DateTime.UtcNow;
    for (var poradi = 1; poradi <= 10; poradi++)
    {
        var plan = startDate.Date.AddDays(poradi * 7);
        DateTime? actual = actualSteps.Contains(poradi) ? plan.AddDays(2) : null;
        dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
        {
            ZaznamId = recordId,
            Poradi = (byte)poradi,
            PlanDatum = plan,
            SkutecnostDatum = actual,
            SkutecnostZdroj = actual.HasValue ? (byte)2 : (byte)0,
            SkutecnostRezim = 2,
            UpdatedAt = now
        });
    }
    await dbContext.SaveChangesAsync();
}
```

- [ ] **Step 2: Failing testy** — nový soubor `PmTracker.Tests.Api/Controllers/RecordScheduleTogglePartialTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Toggle Harmonogram na kartě záznamu (2026-07-13): endpoint harmonogramu
/// jednoho záznamu — gov-tag + serverové ticky + ROZBALENÝ rozpad; 404 bez hodnot;
/// regrese: záložka Harmonogram má rozpad dál skrytý.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordScheduleTogglePartialTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordScheduleTogglePartialTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RecordSchedulePartial_RendersTagTicks_AndExpandedBreakdown()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecSchedOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS1", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record schedule partial");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordSchedulePartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("gov-tag", "štítek Stíháme/Nestíháme");
        html.Should().Contain("data-schedule-ticks", "statická osa vyžaduje serverové ticky");
        html.Should().Contain("gantt-steps schedule-steps");
        Regex.IsMatch(html, "class=\"gantt-steps schedule-steps\"[^>]*hidden")
            .Should().BeFalse("na kartě je rozpad rovnou rozbalený (spec R2)");
    }

    [Fact]
    public async Task RecordSchedulePartial_ReturnsNotFound_WithoutScheduleValues()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecSchedEmptyOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS2", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record schedule empty");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordSchedulePartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "záznam bez vyplněné hodnoty kroku nemá harmonogram dlaždici");
    }

    [Fact]
    public async Task ScheduleTab_KeepsBreakdownHidden_Regression()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecSchedTabOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS3", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record schedule tab regression");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Regex.IsMatch(html, "class=\"gantt-steps schedule-steps\"[^>]*hidden")
            .Should().BeTrue("v záložce Harmonogram se rozpad dál rozbaluje tlačítkem Rozpad");
    }
}
```

- [ ] **Step 3: Ověř, že testy selžou** — `dotnet test PmTracker.Tests.Api --filter RecordScheduleTogglePartialTests --nologo`. Očekávání: první dva testy FAIL na 404 (endpoint neexistuje), třetí PASS (regrese pinuje současné chování — to je v pořádku, hlídá budoucnost).

- [ ] **Step 4: BreakdownExpanded flag** — `ProjektHarmonogramTabViewModels.cs`, do `HarmonogramBlockViewModel` (za `HideActual`):

```csharp
    /// <summary>
    /// Toggle na kartě záznamu (2026-07-13): true = readonly rozpad kroků se renderuje
    /// rovnou viditelný (bez tlačítka „Rozpad"). Výchozí false = dosavadní chování
    /// (záložka Harmonogram rozbaluje tlačítkem).
    /// </summary>
    public bool BreakdownExpanded { get; init; }
```

- [ ] **Step 5: _ScheduleBlock.cshtml** — na řádku ~189 nahraď:

```razor
<div class="gantt-steps schedule-steps" data-schedule-steps data-schedule-record-id="@Model.RecordId" hidden="hidden">
```

za:

```razor
<div class="gantt-steps schedule-steps" data-schedule-steps data-schedule-record-id="@Model.RecordId" hidden="@(Model.BreakdownExpanded ? null : "hidden")">
```

(Razor atribut s hodnotou `null` se celý vynechá.)

- [ ] **Step 6: VM** — `ProjektZaznamyTabViewModels.cs`: do `ProjektZaznamCardShellViewModel` (za `CommentsUrl`) přidej `public string? ScheduleUrl { get; set; }` a na konec souboru:

```csharp
/// <summary>Toggle na kartě záznamu (2026-07-13): payload pohledu „harmonogram" —
/// štítek Stíháme/Nestíháme + sdílený readonly blok s rozbaleným rozpadem.</summary>
public sealed class ZaznamScheduleBlockViewModel
{
    public bool Stihame { get; init; }
    public HarmonogramBlockViewModel HarmonogramBlok { get; init; } = new();
}
```

- [ ] **Step 7: Service** — nový `PmTracker.Web/Services/ProjectService.RecordScheduleComposition.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    /// <summary>
    /// Toggle na kartě záznamu (2026-07-13): harmonogram JEDNOHO záznamu pro pohled
    /// „harmonogram" na kartě v záložce Záznamy. Stejné stavební kameny jako
    /// BuildProjectScheduleRowsAsync (záložka Harmonogram) — jen pro jeden záznam,
    /// s BreakdownExpanded (rozpad rovnou viditelný). NULL = záznam neexistuje /
    /// není úkol / nemá vyplněnou hodnotu kroku (stejný predikát jako dlaždice v záložce).
    /// </summary>
    public async Task<ZaznamScheduleBlockViewModel?> BuildRecordScheduleBlockAsync(int projectId, int recordId, CancellationToken ct = default)
    {
        var shell = await BuildRecordCardShellAsync(projectId, recordId, ct);
        if (shell is null || !shell.Summary.JeUkol)
        {
            return null;
        }

        var kroky = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToListAsync(ct);
        if (!HarmonogramKrokPredicates.MaVyplnenouHodnotu(kroky))
        {
            return null;
        }

        var summary = shell.Summary;
        var todayDate = timeProvider.GetLocalNow().Date;
        var deadline = (summary.AktualniTermin ?? summary.DatumZalozeni).Date;
        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(summary.DatumZalozeni, kroky, deadline, todayDate);

        return new ZaznamScheduleBlockViewModel
        {
            Stihame = souhrn.Stihame,
            HarmonogramBlok = BuildScheduleBlockViewModel(
                recordId,
                "project-readonly",
                summary.DatumZalozeni,
                deadline,
                "#dc2626",
                souhrn,
                HarmonogramDateBlokBuilder.BuildKroky(summary.DatumZalozeni, kroky, todayDate),
                overviewLayout: HarmonogramDateBlokBuilder.BuildBarLayout(summary.DatumZalozeni, kroky, deadline, todayDate),
                today: todayDate) with { BreakdownExpanded = true }
        };
    }
}
```

Pozn.: `HarmonogramKrokPredicates` — ověř namespace (`PmTracker.Web.Services.Schedules` dle použití v `ProjectService.ScheduleComposition.cs`); `summary.DatumZalozeni`/`summary.AktualniTermin` existují (renderuje je `_ZaznamPartial`). Pokud je `DatumZalozeni` na summary jiného jména, převezmi přesné jméno z `ZaznamCardSummaryViewModel`.

- [ ] **Step 8: Interfaces + delegace** — `IProjectService.cs` (vedle `BuildRecordCardShellAsync`):

```csharp
    Task<ZaznamScheduleBlockViewModel?> BuildRecordScheduleBlockAsync(int projectId, int recordId, CancellationToken ct = default);
```

Totéž do `IRecordService.cs`; do `RecordService.cs` (vedle stávající delegace na ř. 61):

```csharp
    public Task<ZaznamScheduleBlockViewModel?> BuildRecordScheduleBlockAsync(int projectId, int recordId, CancellationToken ct = default)
        => projectService.BuildRecordScheduleBlockAsync(projectId, recordId, ct);
```

- [ ] **Step 9: Partial** — nový `PmTracker.Web/Views/Projekty/_ZaznamSchedulePartial.cshtml`:

```razor
@model ZaznamScheduleBlockViewModel
@* Toggle na kartě záznamu (2026-07-13): obsah pohledu „harmonogram" — štítek + sdílený
   readonly blok (stejné kreslení jako dlaždice v záložce Harmonogram, bez jejího obalu). *@
<div class="record-schedule-tag-row">
    <gov-tag color="@(Model.Stihame ? "success" : "error")" size="s">@(Model.Stihame ? "Stíháme" : "Nestíháme")</gov-tag>
</div>
@await Html.PartialAsync("_ScheduleBlock", Model.HarmonogramBlok)
```

- [ ] **Step 10: Endpoint** — `ZaznamyController.Partials.cs`, za `RecordDetailPartial`:

```csharp
    [HttpGet]
    public async Task<IActionResult> RecordSchedulePartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordScheduleBlockAsync(projektId, zaznamId, ct);
        if (model is null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Projekty/_ZaznamSchedulePartial.cshtml", model);
    }
```

A v `PrepareRecordCardShellPresentation` (tamtéž, ř. ~88) za `CommentsUrl ??=`:

```csharp
        record.ScheduleUrl ??= Url.Action(nameof(RecordSchedulePartial), new { projektId, zaznamId = summary.Id });
```

- [ ] **Step 11: ProjektyController** — v `PrepareRecordCardShellPresentation` (ř. ~255, za CommentsUrl):

```csharp
        record.ScheduleUrl = Url.Action("RecordSchedulePartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordSchedulePartial?projektId={projectId}&zaznamId={summary.Id}";
```

- [ ] **Step 12: Build + testy zelené** — `dotnet build PmTracker.sln --nologo -clp:ErrorsOnly` a `dotnet test PmTracker.Tests.Api --filter RecordScheduleTogglePartialTests --nologo`. Očekávání: 3/3 PASS.

- [ ] **Step 13: Checkpoint (commit držený)** — `git status` pro přehled; žádný commit.

---

### Task 2: Karta záznamu — markup + CSS (indikátor, řady akcí, menu, switch, třetí shell)

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (sekce record-* ~ř. 3705-3830)
- Test: `PmTracker.Tests.Unit/Projects/RecordCardScheduleToggleMarkupTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/Layout/RecordScheduleViewCssTests.cs` (nový)
- Test: `PmTracker.Tests.Api/Controllers/RecordScheduleTogglePartialTests.cs` (rozšíření o card render testy)

**Interfaces:**
- Consumes: `ProjektZaznamCardShellViewModel.ScheduleUrl` (Task 1).
- Produces: DOM hooky pro Task 3/4 — `[data-record-menu-trigger]`, `[data-record-menu]`, `[data-record-menu-icon="closed"|"open"]`, `[data-record-view-switch]`, `[data-record-schedule-shell]` + `data-record-schedule-url`, `[data-record-schedule-placeholder]`, `[data-record-schedule-error]`; CSS třídy `record-card--schedule-view`, `record-expand-indicator`, `record-actions-row`, `record-actions-menu(-item)`.

- [ ] **Step 1: Failing unit testy (markup)** — `PmTracker.Tests.Unit/Projects/RecordCardScheduleToggleMarkupTests.cs`:

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>Toggle Harmonogram na kartě (2026-07-13): markup piny _ZaznamPartial —
/// indikátor rozbalení vlevo (R7), akce max 3 tlačítka + menu + switch (R4-R6, R8),
/// třetí lazy shell pro harmonogram.</summary>
public sealed class RecordCardScheduleToggleMarkupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Partial => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));

    [Fact]
    public void Header_StartsWithExpandIndicator_OldRightIndicatorGone()
    {
        Regex.IsMatch(Partial, "<header class=\"record-header\"[^>]*>\\s*<span class=\"record-expand-indicator\"", RegexOptions.Singleline)
            .Should().BeTrue("indikátor rozbalení je první prvek hlavičky (R7)");
        Partial.Should().NotContain("record-toggle-indicator", "stará šipka vpravo je pryč");
    }

    [Fact]
    public void Actions_TwoRows_MenuTriggerWithChevrons_NoCalendarIconsInHeader()
    {
        Partial.Should().Contain("record-actions-row");
        Partial.Should().Contain("data-record-menu-trigger");
        Partial.Should().Contain("data-record-menu");
        Partial.Should().Contain("chevron-right");
        Partial.Should().Contain("chevron-down");
        Partial.Should().NotContain("calendar-date", "překlik na záložku se stěhuje do menu");
        Partial.Should().NotContain("calendar-plus", "návrh harmonogramu se stěhuje do menu");
    }

    [Fact]
    public void MenuItems_AndSwitch_AreGated()
    {
        Partial.Should().Contain("data-goto-schedule=\"@summary.Id\"", "menu položka drží cross-tab hook");
        Partial.Should().Contain("data-record-view-switch");
        Partial.Should().Contain("data-record-schedule-shell");
        Partial.Should().Contain("data-record-schedule-url");
        // Gating: switch i schedule shell jen s harmonogramem; návrh dle oprávnění.
        Regex.Matches(Partial, @"summary\.MaHarmonogramHodnotu").Count.Should().BeGreaterThanOrEqualTo(3,
            "menu položka + switch + shell jsou gated na MaHarmonogramHodnotu");
        Partial.Should().Contain("summary.CanCreateScheduleProposal");
    }
}
```

- [ ] **Step 2: Failing unit testy (CSS)** — `PmTracker.Tests.Unit/Layout/RecordScheduleViewCssTests.cs`:

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>Toggle Harmonogram na kartě (2026-07-13): CSS piny — pohled řídí třída na kartě
/// (žádné :has, i15), indikátor vlevo rotuje, akce ve dvou řadách.</summary>
public sealed class RecordScheduleViewCssTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    [Fact]
    public void ScheduleView_TogglesShells_ByCardClass()
    {
        Css.Should().Contain(".record-card--schedule-view .record-detail-shell");
        Css.Should().Contain(".record-card--schedule-view .record-comments-lazy");
        Css.Should().Contain(".record-card:not(.record-card--schedule-view) .record-schedule-shell");
    }

    [Fact]
    public void ExpandIndicator_MovedLeft_RotationPreserved()
    {
        Css.Should().Contain(".record-expand-indicator");
        Css.Should().Contain(".record-card.collapsed .record-expand-indicator");
        Css.Should().NotContain(".record-toggle-indicator", "stará třída zanikla s markup přesunem");
    }

    [Fact]
    public void RecordBlock_AvoidsFragileModernCss()
    {
        // Úsek record-card sekce: od .record-expand-indicator po .record-schedule-shell pravidla.
        var start = Css.IndexOf(".record-actions", StringComparison.Ordinal);
        var end = Css.IndexOf(".record-schedule-shell", start, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        end.Should().BeGreaterThan(start);
        var slice = Css[start..end];
        slice.Should().NotContain(":has(", "i15 pojistka");
        slice.Should().NotContain("max-content", "i15 pojistka");
    }
}
```

- [ ] **Step 3: Ověř failure** — `dotnet test PmTracker.Tests.Unit --filter "RecordCardScheduleToggleMarkupTests|RecordScheduleViewCssTests" --nologo`. Očekávání: FAIL (markup/CSS neexistuje). Zároveň `grep -rn "record-toggle-indicator" PmTracker.Tests.Unit PmTracker.Tests.Api` — pokud existují starší piny na tuto třídu, uprav je v tomto tasku na `record-expand-indicator` (pravidlo: oprav všechny sourozence).

- [ ] **Step 4: Markup _ZaznamPartial.cshtml** — tři úpravy:

**(a)** Do `@{ ... }` bloku přidej lokál (za `editRecordUrl`):

```razor
    var hasMenuItems = summary.MaHarmonogramHodnotu
        || (summary.CanCreateScheduleProposal && !string.IsNullOrWhiteSpace(summary.ScheduleProposalUrl));
```

**(b)** Hlavička — první prvek + přestavba akcí. `<header class="record-header" data-record-toggle aria-expanded="false">` dostane jako PRVNÍ dítě:

```razor
        @* R7 (2026-07-13): indikátor rozbalení vlevo mezi krajem karty a kategorií. *@
        <span class="record-expand-indicator" aria-hidden="true">›</span>
```

Celý `<div class="record-actions">…</div>` (ř. 78-129) nahraď:

```razor
            <div class="record-actions">
                <div class="record-actions-row">
                    <span class="badge">@summary.Stav</span>
                    <a class="icon-btn"
                       href="@taskPrintPdfUrl"
                       target="_blank"
                       rel="noopener"
                       aria-label="Tisk záznamu #@summary.CisloViditelne"
                       data-stop-propagation="true"
                       data-print-trigger="true"
                       data-print-pdf-url="@taskPrintPdfUrl"
                       data-print-word-url="@taskPrintWordUrl"
                       data-print-label="Tisk záznamu #@summary.CisloViditelne">
                        <gov-icon size="s" name="printer" type="components" aria-hidden="true"></gov-icon>
                    </a>
                    @if (summary.CanEditRecord || summary.CanManageSchedule)
                    {
                        <pm-button variant="Secondary"
                                   size="Small"
                                   href="@editRecordUrl"
                                   data-stop-propagation="true"
                                   aria-label="@summary.EditButtonLabel"
                                   title="@summary.EditButtonLabel">
                            <gov-icon size="s" name="pencil" type="components" aria-hidden="true"></gov-icon>
                        </pm-button>
                    }
                    @* R4/R5 (2026-07-13): méně časté akce ve svislém menu (šipka → / ↓).
                       Ikony se přepínají hidden atributem na vnořených gov-icon — nikdy
                       textContent na gov hostu (slot relocation). *@
                    @if (hasMenuItems)
                    {
                        <pm-button variant="Secondary"
                                   size="Small"
                                   data-record-menu-trigger="true"
                                   data-stop-propagation="true"
                                   aria-haspopup="menu"
                                   aria-expanded="false"
                                   aria-label="Další akce záznamu"
                                   title="Další akce">
                            <gov-icon size="s" name="chevron-right" type="components" aria-hidden="true" data-record-menu-icon="closed"></gov-icon>
                            <gov-icon size="s" name="chevron-down" type="components" aria-hidden="true" hidden data-record-menu-icon="open"></gov-icon>
                        </pm-button>
                        <div class="record-actions-menu" data-record-menu hidden role="menu" data-stop-propagation="true">
                            @if (summary.MaHarmonogramHodnotu)
                            {
                                <button type="button" class="record-actions-menu-item" role="menuitem"
                                        data-goto-schedule="@summary.Id">Zobrazit na záložce Harmonogram</button>
                            }
                            @if (summary.CanCreateScheduleProposal && !string.IsNullOrWhiteSpace(summary.ScheduleProposalUrl))
                            {
                                <a class="record-actions-menu-item" role="menuitem"
                                   href="@summary.ScheduleProposalUrl">Navrhnout změnu harmonogramu</a>
                            }
                        </div>
                    }
                </div>
                @* R6 (2026-07-13): toggle Záznam ⇄ Harmonogram — jen u záznamů s harmonogramem. *@
                @if (summary.MaHarmonogramHodnotu)
                {
                    <div class="record-actions-row record-actions-row--switch">
                        <gov-form-switch size="s"
                                         class="record-view-switch"
                                         data-record-view-switch
                                         data-stop-propagation="true">
                            <span slot="label"><label>Harmonogram</label></span>
                        </gov-form-switch>
                    </div>
                }
            </div>
```

**(c)** Do `.record-body` za `</div>` bloku `record-comments-lazy` přidej třetí shell:

```razor
            @* Toggle (2026-07-13): třetí lazy shell — harmonogram záznamu. Viditelnost řídí
               třída record-card--schedule-view na kartě (CSS), obsah plní recordScheduleView.js. *@
            @if (summary.MaHarmonogramHodnotu)
            {
                <div class="record-schedule-shell"
                     data-record-schedule-shell
                     data-record-schedule-url="@Model.ScheduleUrl">
                    <div class="record-loading-placeholder" data-record-schedule-placeholder hidden>
                        <div class="record-loading-line"></div>
                        <div class="record-loading-line short"></div>
                    </div>
                    <div class="record-loading-error" data-record-schedule-error hidden></div>
                </div>
            }
```

- [ ] **Step 5: CSS site.css** — v record sekci (~ř. 3705-3830):

**(a)** `.record-actions` (ř. 3725) nahraď + přidej řady:

```css
/* Toggle na kartě (2026-07-13): akce ve dvou řadách — řada 1 max 3 tlačítka + badge,
   řada 2 switch Harmonogram. Jen letité flexy (i15). */
.record-actions {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 8px;
}

.record-actions-row {
    display: flex;
    align-items: center;
    gap: 10px;
}

.record-actions [hidden] {
    display: none !important;
}
```

**(b)** Selektor badge (ř. 3734) `.record-actions > .badge` → `.record-actions .badge` (badge je teď v řadě, ne přímý potomek).

**(c)** `.record-toggle-indicator` blok (ř. 3817-3826) nahraď:

```css
/* R7 (2026-07-13): indikátor rozbalení přesunut vlevo (mezi kraj karty a kategorii). */
.record-expand-indicator {
    display: inline-block;
    margin-top: 2px;
    font-size: 18px;
    transform: rotate(90deg);
    transition: transform 0.2s ease;
}

.record-card.collapsed .record-expand-indicator {
    transform: rotate(0deg);
}
```

**(d)** Za `.record-card.collapsed .record-body { display: none; }` přidej:

```css
/* Toggle na kartě (2026-07-13): pohled řídí třída na kartě — žádné :has (i15).
   V pohledu harmonogram je skrytý detail i vyjádření (R3), jinak schedule shell. */
.record-card:not(.record-card--schedule-view) .record-schedule-shell {
    display: none;
}

.record-card--schedule-view .record-detail-shell,
.record-card--schedule-view .record-comments-lazy {
    display: none;
}

.record-schedule-tag-row {
    margin: 2px 0 10px;
}

/* Svislé menu akcí karty — obsah panelu; pozicování dodává floating vrstva (ui/floating.js). */
.record-actions-menu {
    display: flex;
    flex-direction: column;
    gap: 2px;
    min-width: 250px;
    padding: 6px;
    background: var(--pm-surface);
    border: 1px solid var(--pm-border);
    border-radius: 8px;
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.14);
}

.record-actions-menu[hidden] {
    display: none;
}

.record-actions-menu-item {
    display: block;
    width: 100%;
    padding: 8px 10px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: inherit;
    font: inherit;
    text-align: left;
    text-decoration: none;
    cursor: pointer;
}

.record-actions-menu-item:hover,
.record-actions-menu-item:focus-visible {
    background: rgba(37, 99, 235, 0.10);
}
```

- [ ] **Step 6: Unit testy zelené** — `dotnet test PmTracker.Tests.Unit --filter "RecordCardScheduleToggleMarkupTests|RecordScheduleViewCssTests" --nologo` → PASS. Poté CELÝ unit projekt `dotnet test PmTracker.Tests.Unit --nologo` — pokud spadly starší piny na `record-toggle-indicator`/`calendar-date` markup, oprav je se zachováním intentu (přejmenování hooků, ne mazání testů).

- [ ] **Step 7: Failing Api render testy** — do `RecordScheduleTogglePartialTests.cs` přidej:

```csharp
    [Fact]
    public async Task RecordCard_WithSchedule_RendersSwitchMenuAndShell()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecCardSwitchOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED4");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS4", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API card with switch");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-view-switch");
        html.Should().Contain("data-record-menu-trigger");
        html.Should().Contain($"data-goto-schedule=\"{recordId}\"");
        html.Should().Contain("Navrhnout změnu harmonogramu", "admin má proposals.schedule.create");
        html.Should().Contain("data-record-schedule-url");
        html.Should().Contain("record-expand-indicator");
        html.Should().NotContain("record-toggle-indicator");
    }

    [Fact]
    public async Task RecordCard_WithoutScheduleValues_HasNoSwitchNorGotoSchedule()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecCardNoSchedOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED5");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS5", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API card no schedule");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-record-view-switch", "bez harmonogramu není co přepínat (R6)");
        html.Should().NotContain("data-goto-schedule", "překlik by vedl na neexistující dlaždici");
        html.Should().NotContain("data-record-schedule-shell");
        // Úkol s právem návrhu má menu s jedinou položkou (návrh změny harmonogramu).
        html.Should().Contain("data-record-menu-trigger");
        html.Should().Contain("Navrhnout změnu harmonogramu");
    }
```

- [ ] **Step 8: Api testy zelené** — `dotnet test PmTracker.Tests.Api --filter RecordScheduleTogglePartialTests --nologo` → 5/5 PASS (markup z kroku 4 je splní; kdyby ne, oprav markup, ne test). Poté celý Api projekt — jediné povolené failury: 4 známé pre-existing gantt.

- [ ] **Step 9: Checkpoint (commit držený).**

---

### Task 3: JS — svislé menu akcí (recordActionsMenu.js)

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/recordActionsMenu.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (side-effect import)
- Test: `PmTracker.Tests.Unit/Projects/RecordScheduleToggleJsTests.cs` (nový — menu piny; Task 4 do něj přidá další)

**Interfaces:**
- Consumes: `[data-record-menu-trigger]`, `[data-record-menu]`, `[data-record-menu-icon]` (Task 2); `mountFloatingPanel/unmountFloatingPanel/closeAllFloatingPanels` z `ui/floating.js`.
- Produces: samostatný side-effect modul (document-level delegace, přežívá výměny karet).

- [ ] **Step 1: Failing test** — `PmTracker.Tests.Unit/Projects/RecordScheduleToggleJsTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>Toggle Harmonogram na kartě (2026-07-13): JS piny — menu s mousedown-origin
/// guardem na floating vrstvě; (Task 4 přidá piny view-switch modulu a persistence).</summary>
public sealed class RecordScheduleToggleJsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Js(string name) => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/wwwroot/js/modules", name));

    [Fact]
    public void Menu_UsesFloatingLayer_WithMousedownOriginGuard()
    {
        var src = Js("recordActionsMenu.js");
        src.Should().Contain("mountFloatingPanel", "menu jede na sdílené floating vrstvě");
        src.Should().Contain("closeAllFloatingPanels");
        src.Should().Contain("mousedown", "outside-close vyhodnocuje původ gesta, ne click (drag lekce z modalů)");
        src.Should().Contain("Escape");
    }

    [Fact]
    public void Menu_IsWiredInBootstrap()
    {
        Js("bootstrap.js").Should().Contain("recordActionsMenu.js",
            "side-effect modul musí být explicitně importován (memory project_bundle_sync)");
    }
}
```

- [ ] **Step 2: Ověř failure** — `dotnet test PmTracker.Tests.Unit --filter RecordScheduleToggleJsTests --nologo` → FAIL (soubor neexistuje).

- [ ] **Step 3: Modul** — `PmTracker.Web/wwwroot/js/modules/recordActionsMenu.js`:

```js
// recordActionsMenu.js — svislé rozbalovací menu akcí na kartě záznamu (šipka → / ↓).
//
// Trigger [data-record-menu-trigger] otevře sourozenecký panel [data-record-menu] přes
// sdílenou floating vrstvu (ui/floating.js): panel se mountuje do #floating-panel-root,
// pozicuje pod trigger (flip, reposition při scrollu řeší bootstrap).
//
// Zavírání: klik na položku, Escape, klik mimo. Outside-close vyhodnocuje PŮVOD gesta
// (mousedown), ne click — drag z menu ven by jinak retargetoval click na společného
// předka a menu falešně zavřel (lekce z modalů, memory feedback_modal_close_x_only_no_backdrop).
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { closeAllFloatingPanels, mountFloatingPanel, unmountFloatingPanel } from "./ui/floating.js";

let openMenu = null; // { trigger, panel }
let pointerDownTarget = null;

function setTriggerState(trigger, expanded) {
    trigger.setAttribute("aria-expanded", String(expanded));
    const closedIcon = trigger.querySelector('[data-record-menu-icon="closed"]');
    const openIcon = trigger.querySelector('[data-record-menu-icon="open"]');
    if (closedIcon instanceof HTMLElement) {
        closedIcon.hidden = expanded;
    }
    if (openIcon instanceof HTMLElement) {
        openIcon.hidden = !expanded;
    }
}

function closeRecordActionsMenu() {
    if (!openMenu) {
        return;
    }
    const { trigger, panel } = openMenu;
    openMenu = null;
    panel.hidden = true;
    unmountFloatingPanel(panel);
    if (trigger.isConnected) {
        setTriggerState(trigger, false);
    }
}

function openRecordActionsMenu(trigger) {
    const sibling = trigger.nextElementSibling;
    const panel = sibling instanceof HTMLElement && sibling.matches("[data-record-menu]") ? sibling : null;
    if (!panel) {
        return;
    }
    closeRecordActionsMenu();
    closeAllFloatingPanels(document, panel);
    panel.hidden = false;
    mountFloatingPanel(panel, trigger, { gap: 4 });
    setTriggerState(trigger, true);
    openMenu = { trigger, panel };
}

function handleDocumentClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    // Resync: refreshPageScope volá closeAllFloatingPanels() mimo nás — panel už může být
    // skrytý/odmountovaný, jen stavová proměnná přežila.
    if (openMenu && openMenu.panel.hidden) {
        openMenu = null;
    }

    const trigger = target.closest("[data-record-menu-trigger]");
    if (trigger instanceof HTMLElement) {
        event.preventDefault();
        if (openMenu && openMenu.trigger === trigger) {
            closeRecordActionsMenu();
        }
        else {
            openRecordActionsMenu(trigger);
        }
        return;
    }

    if (!openMenu) {
        return;
    }

    // Klik na položku → zavřít; akci položky vykoná její vlastní handler/odkaz
    // (data-goto-schedule odchytává crossTabNav.js, návrh je <a href>).
    if (openMenu.panel.contains(target)) {
        closeRecordActionsMenu();
        return;
    }

    // Outside-close jen když gesto ZAČALO mimo panel i trigger.
    const origin = pointerDownTarget;
    const originInside = origin instanceof Node
        && (openMenu.panel.contains(origin) || openMenu.trigger.contains(origin));
    if (!originInside) {
        closeRecordActionsMenu();
    }
}

function handleDocumentMousedown(event) {
    pointerDownTarget = event.target instanceof Node ? event.target : null;
}

function handleDocumentKeydown(event) {
    if (event.key === "Escape") {
        closeRecordActionsMenu();
    }
}

document.addEventListener("mousedown", handleDocumentMousedown, true);
document.addEventListener("click", handleDocumentClick);
document.addEventListener("keydown", handleDocumentKeydown);
```

- [ ] **Step 4: Bootstrap import** — do `bootstrap.js` k side-effect importům (za `import "./harmonogram/rezim-master-switch.js";`):

```js
import "./recordActionsMenu.js"; // 2026-07-13 — svislé menu akcí na kartě záznamu (toggle spec).
```

- [ ] **Step 5: Testy zelené** — `dotnet test PmTracker.Tests.Unit --filter RecordScheduleToggleJsTests --nologo` → PASS.

- [ ] **Step 6: Checkpoint (commit držený).**

---

### Task 4: JS — view switch, lazy-load harmonogramu a persistence (recordScheduleView.js)

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/recordScheduleView.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/recordRefresh.js` (3 místa: refreshRecordCard, buildRecordUiState, restoreRecordUiState)
- Modify: `PmTracker.Web/wwwroot/js/modules/crossTabNav.js` (reset pohledu při goto-record)
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (side-effect import)
- Test: `PmTracker.Tests.Unit/Projects/RecordScheduleToggleJsTests.cs` (rozšíření)

**Interfaces:**
- Consumes: `[data-record-view-switch]`, `[data-record-schedule-shell]` + `data-record-schedule-url` (Task 2); endpoint z Task 1; `toggleRecordCard` (recordLazyLoading.js), `renderStaticTimelineAxes` (schedule.js), `queueRainbowSegmentRender` (ui.js), helpery z navigationShared.js.
- Produces: exporty `applyRecordViewState(card)`, `loadRecordSchedule(card, options)`, `setRecordView(card, scheduleView)`, `resetRecordViewToRecord(recordId)`; dataset kontrakt `card.dataset.recordViewSchedule = "true"|"false"`, `card.dataset.recordScheduleLoaded`; stavové pole `scheduleViewRecordIds` v buildRecordUiState.

- [ ] **Step 1: Failing testy** — do `RecordScheduleToggleJsTests.cs` přidej:

```csharp
    [Fact]
    public void ScheduleView_RendersAxesAfterShow_AndListensGovChange()
    {
        var src = Js("recordScheduleView.js");
        src.Should().Contain("renderStaticTimelineAxes", "osy se kreslí až po zviditelnění (vzor Rozpad)");
        src.Should().Contain("queueRainbowSegmentRender");
        src.Should().Contain("recordViewSchedule", "dataset je jediný zdroj pravdy stavu");
        src.Should().Contain("gov-change", "gov-form-switch emituje gov-change i change");
        src.Should().Contain("record-card--schedule-view");
        src.Should().Contain("data-record-schedule-retry");
    }

    [Fact]
    public void Persistence_CoversCardRefresh_AndPanelRestore()
    {
        var refresh = Js("recordRefresh.js");
        refresh.Should().Contain("scheduleViewRecordIds", "preserve celého panelu (bfcache/pageshow) drží pohled");
        refresh.Should().Contain("loadRecordSchedule");
        refresh.Should().Contain("applyRecordViewState");

        var crossNav = Js("crossTabNav.js");
        crossNav.Should().Contain("resetRecordViewToRecord",
            "goto-record z harmonogramu vrací kartu do pohledu záznam (R9/4)");

        Js("bootstrap.js").Should().Contain("recordScheduleView.js");
    }
```

- [ ] **Step 2: Ověř failure** — `dotnet test PmTracker.Tests.Unit --filter RecordScheduleToggleJsTests --nologo` → nové 2 testy FAIL.

- [ ] **Step 3: Modul** — `PmTracker.Web/wwwroot/js/modules/recordScheduleView.js`:

```js
// recordScheduleView.js — toggle Záznam ⇄ Harmonogram na kartě záznamu (spec 2026-07-13).
//
// Stav drží dataset karty (data-record-view-schedule → card.dataset.recordViewSchedule) —
// JEDINÝ zdroj pravdy; třídu .record-card--schedule-view (CSS řídí viditelnost shellů)
// a checked switche od něj odvozuje applyRecordViewState. recordRefresh.js díky tomu
// umí stav přečíst ze staré karty a přenést na novou (R9/3).
//
// Harmonogram je třetí lazy shell (vedle detailu a vyjádření); osy se kreslí až po
// zviditelnění — měří šířku, ve skrytém prvku je nulová (ověřený vzor „Rozpad").
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import {
    fetchHtmlFragment,
    renderLazyLoadError,
    resolveOrCreateErrorContainer,
    resolveRecordCardElement,
    setLazyLoadingState
} from "./navigationShared.js";
import { toggleRecordCard } from "./recordLazyLoading.js";
import { renderStaticTimelineAxes } from "./schedule.js";
import { queueRainbowSegmentRender } from "./ui.js";

const VIEW_CLASS = "record-card--schedule-view";

export function isRecordScheduleView(card) {
    return card instanceof HTMLElement && card.dataset.recordViewSchedule === "true";
}

/** Odvodí třídu karty a checked switche z datasetu (jediný zdroj pravdy). */
export function applyRecordViewState(card) {
    if (!(card instanceof HTMLElement)) {
        return;
    }
    const scheduleView = isRecordScheduleView(card);
    card.classList.toggle(VIEW_CLASS, scheduleView);
    const viewSwitch = card.querySelector("[data-record-view-switch]");
    if (viewSwitch instanceof HTMLElement) {
        if (scheduleView) {
            viewSwitch.setAttribute("checked", "");
        }
        else {
            viewSwitch.removeAttribute("checked");
        }
    }
}

export async function loadRecordSchedule(cardOrChild, options = {}) {
    const card = resolveRecordCardElement(cardOrChild);
    if (!(card instanceof HTMLElement)) {
        return false;
    }

    const shell = card.querySelector("[data-record-schedule-shell]");
    if (!(shell instanceof HTMLElement)) {
        return false;
    }

    const scheduleUrl = (shell.dataset.recordScheduleUrl || "").trim();
    if (!scheduleUrl) {
        return false;
    }

    const forceReload = options.force === true;
    if (card.dataset.recordScheduleLoaded === "true" && !forceReload) {
        return true;
    }

    const placeholder = shell.querySelector("[data-record-schedule-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(shell, "data-record-schedule-error");
    setLazyLoadingState(shell, placeholder, errorContainer, true);

    try {
        shell.innerHTML = await fetchHtmlFragment(scheduleUrl);
        card.dataset.recordScheduleLoaded = "true";
        shell.removeAttribute("aria-busy");
        // Kreslit až tady — shell je v tuto chvíli viditelný (setRecordView napřed
        // aplikuje view třídu), takže osy mají nenulovou šířku.
        renderStaticTimelineAxes(shell);
        queueRainbowSegmentRender(shell);
        return true;
    }
    catch {
        card.dataset.recordScheduleLoaded = "false";
        setLazyLoadingState(shell, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst harmonogram záznamu.", "data-record-schedule-retry");
        return false;
    }
}

export async function setRecordView(card, scheduleView) {
    if (!(card instanceof HTMLElement)) {
        return;
    }

    card.dataset.recordViewSchedule = scheduleView ? "true" : "false";
    applyRecordViewState(card);

    if (scheduleView) {
        // R9/1: přepnutí sbalené karty ji rovnou rozbalí.
        if (card.classList.contains("collapsed")) {
            await toggleRecordCard(card, { expand: true });
        }
        await loadRecordSchedule(card);
    }
}

/** R9/4: cross-nav „Zobrazit záznam" (harmonogram → záznamy) vrací kartu do pohledu záznam. */
export function resetRecordViewToRecord(recordId) {
    const id = String(recordId || "").trim();
    if (!id) {
        return;
    }
    document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(id)}"]`).forEach((card) => {
        if (card instanceof HTMLElement) {
            card.dataset.recordViewSchedule = "false";
            applyRecordViewState(card);
        }
    });
}

function handleSwitchChange(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const viewSwitch = target.closest("[data-record-view-switch]");
    if (!(viewSwitch instanceof HTMLElement) || viewSwitch.hasAttribute("disabled")) {
        return;
    }
    const card = viewSwitch.closest(".record-card");
    if (!(card instanceof HTMLElement)) {
        return;
    }
    void setRecordView(card, viewSwitch.hasAttribute("checked"));
}

function handleRetryClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const retry = target.closest("[data-record-schedule-retry]");
    if (!(retry instanceof HTMLElement)) {
        return;
    }
    event.preventDefault();
    void loadRecordSchedule(retry, { force: true });
}

// gov-form-switch emituje `gov-change` (Stencil) i klasický `change` — poslouchat oba
// (stejně jako rezim-master-switch.js). Stav se čte z [checked] atributu hostu
// (memory feedback_css_checked_custom_elements — atribut se reflektuje).
document.addEventListener("change", handleSwitchChange);
document.addEventListener("gov-change", handleSwitchChange);
document.addEventListener("click", handleRetryClick);
```

Pozn. pro exekuci: PŘED dokončením ověř v `harmonogram/rezim-master-switch.js` (funkce `handleChange`), jak čte stav switche — pokud čte `.checked` property místo atributu, použij v `handleSwitchChange` totéž (`viewSwitch.checked === true || viewSwitch.hasAttribute("checked")`).

- [ ] **Step 4: recordRefresh.js — tři integrace.**

**(a)** Import (k ostatním importům):

```js
import { applyRecordViewState, loadRecordSchedule } from "./recordScheduleView.js";
```

**(b)** `refreshRecordCard` — za řádek `const commentsWereLoaded = …` přidej:

```js
    const scheduleViewActive = anchorCurrentCard.dataset.recordViewSchedule === "true";
```

V `refreshedCards.forEach((card) => { … })` za nastavení `aria-expanded` přidej:

```js
        // Toggle (2026-07-13, R9/3): poloha přepínače přežívá výměnu karty.
        if (scheduleViewActive) {
            card.dataset.recordViewSchedule = "true";
            applyRecordViewState(card);
        }
```

V `hydrateTasks` async callbacku za blok `if (commentsWereLoaded) { … }` přidej:

```js
            if (scheduleViewActive) {
                await loadRecordSchedule(card);
            }
```

**(c)** `buildRecordUiState` — za výpočet `loadedCommentRecordIds` přidej:

```js
    const scheduleViewRecordIds = Array.from(root.querySelectorAll(".record-card[data-record-id]"))
        .filter((card) => card instanceof HTMLElement
            && !isElementInHiddenTree(card)
            && card.dataset.recordViewSchedule === "true")
        .map((card) => card.getAttribute("data-record-id") || "")
        .filter(Boolean);
```

a do return objektu přidej `scheduleViewRecordIds,` (za `loadedCommentRecordIds`).

**(d)** `restoreRecordUiState` — za `const loadedCommentSet = …` přidej:

```js
    const scheduleViewSet = new Set(Array.isArray(state.scheduleViewRecordIds) ? state.scheduleViewRecordIds : []);
```

V prvním `document.querySelectorAll(".record-card…").forEach` (collapse smyčka) za nastavení `aria-expanded` přidej:

```js
        if (scheduleViewSet.has(recordId)) {
            card.dataset.recordViewSchedule = "true";
        }
        applyRecordViewState(card);
```

V `restoreTasks.push((async () => { … }))` za comments blok přidej:

```js
            if (scheduleViewSet.has(recordId)) {
                await loadRecordSchedule(card);
            }
```

- [ ] **Step 5: crossTabNav.js** — import + reset. Nahoru:

```js
import { resetRecordViewToRecord } from "./recordScheduleView.js";
```

V `toRecord` větvi před `navigateToTab(…)`:

```js
                // R9/4: návrat na záznam = pohled záznam (uživatel jde číst záznam,
                // ne harmonogram, který právě opustil).
                resetRecordViewToRecord(recordId);
```

- [ ] **Step 6: Bootstrap import** — za import z Task 3:

```js
import "./recordScheduleView.js"; // 2026-07-13 — toggle Záznam ⇄ Harmonogram na kartě.
```

- [ ] **Step 7: Testy zelené** — `dotnet test PmTracker.Tests.Unit --filter RecordScheduleToggleJsTests --nologo` → PASS; poté celý Unit projekt bez regresí.

- [ ] **Step 8: Checkpoint (commit držený).**

---

### Task 5: E2E + živá verifikace

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/RecordScheduleToggleScenariosTests.cs` (nový)
- Scratchpad: Playwright ověřovací skript + screenshoty (1470×956)

**Interfaces:**
- Consumes: vše z Tasků 1-4. E2E infra: vzor `BreadcrumbBackOriginScenariosTests.cs` (fixture, `?asUser`, raw SQL seed přes `Database.ConnectionString`; gov-dialog/host gotchas memory feedback_history_state_pmtabs_playwright_back).

- [ ] **Step 1: E2E testy (failing při chybě implementace, jinak rovnou zelené)** — `RecordScheduleToggleScenariosTests.cs`; převezmi třídní skeleton (fixture, konstruktor, helpery) z `BreadcrumbBackOriginScenariosTests.cs` a přidej SQL seed helper záznamu s kroky:

```csharp
    /// <summary>Seed úkolu s harmonogram kroky (plán 10 kroků, skutečnost 1-2) přes raw SQL.
    /// Před psaním SQL ověřeno sys.columns (memory feedback_legacy_columns_diacritics_raw_sql).</summary>
    private async Task<int> EnsureTaskWithScheduleAsync(int projektId, string marker)
    {
        // 1) záznam: reuse existující E2E insert vzor pro dbo.zaznamy (kategorie U) z tohoto souboru
        //    nebo z RecordRichTextScenariosTests (stejný sloupcový seznam).
        // 2) kroky: INSERT INTO dbo.zaznam_harmonogram_krok (zaznam_id, poradi, plan_datum,
        //    skutecnost_datum, skutecnost_zdroj, skutecnost_rezim, updated_at)
        //    VALUES (@id, 1..10, DATEADD(day, poradi*7, @start), <jen kroky 1-2>, 2/0, 2, SYSUTCDATETIME());
        // Přesné sloupce ověř před spuštěním: SELECT name FROM sys.columns
        //   WHERE object_id = OBJECT_ID('dbo.zaznam_harmonogram_krok');
    }
```

Scénáře (tři testy):

```csharp
    [Fact]
    public async Task Switch_OnCollapsedCard_ExpandsAndShowsFullSchedule()
    {
        // arrange: seed úkol s kroky; goto /Projekty/Detail/{id}?asUser=1
        // act: click gov-form-switch[data-record-view-switch] na kartě (přes label span)
        // assert:
        //   - karta má třídu record-card--schedule-view a NEMÁ collapsed
        //   - .schedule-steps uvnitř karty je viditelný (Expect(...).ToHaveCountAsync(1)
        //     + not [hidden]) a obsahuje gantt-step-row (rozpad kroků, R2)
        //   - gov-tag (Stíháme/Nestíháme) viditelný
        //   - [data-record-detail-shell] a .record-comments-lazy mají display none
        //     (Evaluate getComputedStyle)
    }

    [Fact]
    public async Task CollapseAndExpand_KeepsScheduleView()
    {
        // arrange: jako výše + přepnout switch
        // act: click na .record-header (sbalí), click znovu (rozbalí)
        // assert: karta má record-card--schedule-view, switch má [checked] (R9/2)
    }

    [Fact]
    public async Task Menu_GotoScheduleTab_AndGotoRecordResetsView()
    {
        // arrange: jako výše + přepnout switch (pohled harmonogram)
        // act 1: click [data-record-menu-trigger] → assert menu viditelné
        //   (panel je mountovaný do #floating-panel-root!) + trigger aria-expanded=true
        // act 2: click položky data-goto-schedule → assert záložka harmonogram aktivní
        //   (.tab-panel[data-tab-panel="harmonogram"].active) + .schedule-card[data-schedule-record-id]
        //   viditelná (cross-nav highlight)
        // act 3: click [data-goto-record] na schedule kartě → assert záložka zaznamy aktivní
        //   a karta NEMÁ record-card--schedule-view (R9/4)
    }
```

Kroky testů rozepiš plnými Playwright .NET voláními podle vzorového souboru (Locator + Expect, ne WaitForURL — memory gotchas). Selektor switche: gov-form-switch je host element — klikej na `Locator("gov-form-switch[data-record-view-switch]")`, při flaky chování na vnitřní input přes `.Locator("input")`.

- [ ] **Step 2: Spusť E2E** — příkaz dle repo konvence (stejný jako u BreadcrumbBackOrigin* — `dotnet test PmTracker.Tests.E2E --filter RecordScheduleToggleScenariosTests --nologo`, vyžaduje běžící SQL kontejner). Očekávání: 3/3 PASS; při failu debuguj implementaci (systematic-debugging), ne testy.

- [ ] **Step 3: Živá verifikace (Playwright skript, 1470×956)** — build + spusť dev app (příkaz v memory feedback_esm_module_cache_busting / dev setup), seed přes sqlcmd pokud dev DB nemá úkol s kroky. Skript ve scratchpadu ověří a odscreenshotuje:
  1. kartu sbalenou (indikátor vlevo, 3 tlačítka + switch, žádné zalomení hlavičky na 1470×956),
  2. otevřené menu (šipka ↓, dvě položky),
  3. pohled harmonogram (tag + pruhy + osa s ticky lícující + rozbalený rozpad),
  4. měřením: osy ticky vs. pruhy (stejný vzor jako dřívější schedule ověření), computed display detail/comments shellů.
  Screenshoty ulož do scratchpadu pro ruční i15 verifikaci uživatelem.

- [ ] **Step 4: Plná regrese** — `dotnet test PmTracker.Tests.Unit --nologo` (0 fail), `dotnet test PmTracker.Tests.Api --nologo` (jen 4 známé pre-existing gantt), `dotnet test PmTracker.Tests.E2E --filter RecordScheduleToggleScenariosTests --nologo`.

- [ ] **Step 5: Závěrečný report + commit držený** — shrnutí pro uživatele (výsledky, screenshoty, co ručně ověřit na i15), úklid dev seedu dle domluvy.

---

## Self-review (provedeno při psaní)

1. **Spec coverage:** R1-R9 → R2 (Task 1 BreakdownExpanded + testy), R3 (Task 2 CSS + E2E computed display), R4/R5/R8 (Task 2 markup + Task 3 menu), R6 (Task 2 gating + Api testy), R7 (Task 2 indikátor + testy), R9/1-3 (Task 4 setRecordView + recordRefresh), R9/4 (Task 4 crossTabNav + E2E). §5 endpoint (Task 1), §7 chyby (retry hook Task 2/4), §8 testy (všechny vrstvy), §9 mimo rozsah — bez tasku (správně).
2. **Placeholders:** E2E Step 1 obsahuje záměrně komentované assert osnovy s odkazem na konkrétní vzorový soubor — exekutor v této session vzor zná; SQL sloupce se ověřují za běhu proti sys.columns (memory pravidlo), proto nejsou zapsané naslepo.
3. **Type consistency:** `BuildRecordScheduleBlockAsync` (ProjectService/IProjectService/IRecordService/controller — shodné); `ZaznamScheduleBlockViewModel { Stihame, HarmonogramBlok }` (Task 1 = partial v Task 1); dataset klíče `recordViewSchedule`/`recordScheduleLoaded` (Task 4 = recordRefresh úpravy); hooky `data-record-*` (Task 2 = Task 3/4 = testy).
