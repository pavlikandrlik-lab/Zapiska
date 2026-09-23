# Samostatná stránka záznamu — implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline exekuce — user standard, žádní subagenti). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Read-only stránka jednoho záznamu na trvalé URL — bohatá hlavička na plnou šířku, vlevo vyjádření (jediná zapisující část), vpravo harmonogram s přepínačem Graf ⇄ Tabulka.

**Architecture:** Stránka skládá existující partialy (`_ZaznamDetailPartial`, `_ZaznamCommentsPartial`, `_ZaznamSchedulePartial`) a nově vyčleněnou tabulkovou část harmonogramu. Server vyrenderuje obě podoby harmonogramu naráz; přepínač je čistě klientský (třída na kontejneru + localStorage). Vyjádření běží v skrytém `.record-card` wrapperu, takže stávající AJAX obnova funguje beze změny.

**Tech Stack:** ASP.NET Core MVC (Razor partials), vanilla ES moduly, gov-design-system, xUnit + FluentAssertions, Playwright .NET.

**Spec:** `docs/superpowers/specs/2026-07-14-zaznam-detail-page-design.md`

## Global Constraints

- **Commity se NEPROVÁDÍ** — session pravidlo „commity držím". Místo commit kroků je na konci každého tasku verifikační checkpoint.
- **i15 CSS pravidla** (memory `feedback_i15_edge_css_compat`): žádné `:has()` v nosné logice, žádné `max-content`/`min-content`/`fit-content`, jen letité flex/grid základy. Stohování přes `@media (max-width: 1100px)`.
- **Gov/pm komponenty** (memory `feedback_pm_button_strips_host_attributes`): stav UI řiď třídou na plain předku, ne atributem na custom-element hostu; nikdy `.textContent` na gov hostu.
- **Statická osa harmonogramu** vyžaduje serverové ticky a **kreslí se až po zviditelnění** (ve skrytém prvku má nulovou šířku).
- **Side-effect JS moduly musí být explicitně importovány v `bootstrap.js`** (memory `project_bundle_sync`), jinak tichý fail.
- **Poměr sloupců** 40 % / 60 %; pod 1100 px pod sebe (spec U1). **Hlavička bohatá** (spec U2). **Tabulka = 3 sloupce Krok / Plán / Skutečnost, celá zamčená** (spec U3). **Přepínač v prostoru harmonogramu** (spec U4).
- Texty UI česky: „Harmonogram", „Graf", „Tabulka", „Otevřít na nové kartě", „Vyjádření".
- Testovací příkazy: `dotnet test PmTracker.Tests.Unit --nologo`, `dotnet test PmTracker.Tests.Api --nologo`, `dotnet test PmTracker.Tests.E2E --filter <trida> --nologo`. Známé pre-existing failure sady nezhoršovat (Api: 4× gantt — 3× `ProjectHarmonogramRenderTests`, 1× `RecordEditorControllerTests` MiniGantt).

---

### Task 1: Extrakce tabulky harmonogramu do `_ScheduleTable.cshtml`

Nejrizikovější krok (sahá do editoru), proto první a samostatný. Jde o **čistý přesun bez změny výstupu**.

**Files:**
- Create: `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml`
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml:281-480` (nahradit blok voláním partialu)
- Modify: `PmTracker.Tests.Unit/Harmonogram/ScheduleBlockPartialManualKrokRenderingTests.cs:16`
- Modify: `PmTracker.Tests.Unit/Projects/NavrhyRedesignTests.cs:66-72`
- Modify: `PmTracker.Tests.Unit/Schedule/ScheduleBlockMarkupTests.cs` (test `EditorMode_HighlightsProposalChangedSteps_FromEditorDiff`)
- Modify: `PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs:221-234` (test `ScheduleBlock_DelayDateField_RespektujeNullOdchylka`)
- Test: `PmTracker.Tests.Unit/Schedule/ScheduleTableExtractionTests.cs` (nový)

**Interfaces:**
- Produces: partial `~/Views/Shared/_ScheduleTable.cshtml` s modelem `HarmonogramBlockViewModel`, renderující `<div class="schedule-table-wrap">…</div>`. Konzumenti: `_ScheduleBlock` (editor mód) a stránka záznamu (Task 2).

- [ ] **Step 1: Napiš failing test na existenci a zapojení partialu**

Nový soubor `PmTracker.Tests.Unit/Schedule/ScheduleTableExtractionTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Stránka záznamu (2026-07-14): tabulková sekce harmonogramu je vyčleněná do
/// samostatného partialu _ScheduleTable.cshtml, aby ji mohl použít i read-only
/// tabulkový režim na stránce záznamu. Extrakce je ČISTÝ PŘESUN — editor musí
/// tabulku dál renderovat beze změny.
/// </summary>
public sealed class ScheduleTableExtractionTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Table => Read("PmTracker.Web/Views/Shared/_ScheduleTable.cshtml");
    private static string Block => Read("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml");

    [Fact]
    public void TablePartial_Exists_WithBlockViewModel()
    {
        Table.Should().Contain("@model HarmonogramBlockViewModel", "partial bere celý blok VM");
        Table.Should().Contain("schedule-table-wrap", "renderuje obal tabulky");
        Table.Should().Contain("<th>Krok</th>", "3sloupcová tabulka Krok / Plán / Skutečnost");
    }

    [Fact]
    public void Block_DelegatesTableToPartial_InEditorMode()
    {
        Block.Should().Contain("_ScheduleTable.cshtml", "editor mód deleguje tabulku na partial");
        Block.Should().NotContain("schedule-table-wrap",
            "tabulkový markup se přesunul do _ScheduleTable.cshtml (žádná duplikace)");
    }

    [Fact]
    public void TablePartial_KeepsEditorContract()
    {
        // Kontrakt form bindingu a stavové logiky se přesunem NESMÍ změnit.
        Table.Should().Contain("HarmonogramHodnoty[", "plánové datum drží form binding");
        Table.Should().Contain("var manualInputIndex = 0;", "čítač manual inputů se přesunul s tabulkou");
        Table.Should().Contain("Model.HideActual", "7b větev skrytí skutečnosti");
        Table.Should().Contain("EditorChangedTypeTooltips", "proposal-diff zvýraznění kroků");
        Table.Should().Contain("_ScheduleBlockManualCell", "manual/auto buňky renderuje původní sub-partial");
    }
}
```

- [ ] **Step 2: Ověř, že testy selžou**

Run: `dotnet test PmTracker.Tests.Unit --filter ScheduleTableExtractionTests --nologo`
Expected: FAIL — `_ScheduleTable.cshtml` neexistuje (test crashne na chybějícím souboru).

- [ ] **Step 3: Vytvoř partial přesunem tabulkové sekce**

Zkopíruj z `_ScheduleBlock.cshtml` **beze změny obsahu** blok od `<div class="schedule-table-wrap">` (ř. 283) po jeho uzavírací `</div>` (ř. 479) do nového souboru `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml`, s hlavičkou:

```razor
@model HarmonogramBlockViewModel
@{
    // Stránka záznamu (2026-07-14): tabulková část harmonogramu vyčleněná z _ScheduleBlock,
    // aby ji mohl použít editor (přes _ScheduleBlock) i read-only tabulkový režim na
    // stránce záznamu. ČISTÝ PŘESUN — výstup se nesmí lišit.
    // Plán D Task 8: pořadí manual kroků v rámci ManualActualKroky[] bindingu.
    var manualInputIndex = 0;
    var lockedManualKrokKeys = Model.LockedManualKrokKeys ?? new HashSet<int>();
}
<div class="schedule-table-wrap">
    … (přesunutý obsah beze změny) …
</div>
```

Praktický postup (ověř si výsledek diffem, ne od oka):

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
sed -n '283,479p' PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml > /tmp/_table_body.txt
head -3 /tmp/_table_body.txt && tail -3 /tmp/_table_body.txt
```

Pozor: `manualInputIndex` a `lockedManualKrokKeys` byly deklarované nahoře v `_ScheduleBlock` (ř. 24-25) — v partialu je deklaruj znovu (viz hlavička výše). V `_ScheduleBlock` je **ponech** (používá je i jiná část? ověř: `grep -n "manualInputIndex\|lockedManualKrokKeys" PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml` — pokud po přesunu nezůstane žádné použití, obě deklarace z `_ScheduleBlock` smaž, jinak by build hlásil nepoužitou proměnnou jen jako warning, ale mrtvý kód tam nechceme).

- [ ] **Step 4: Nahraď sekci v `_ScheduleBlock` voláním partialu**

V `_ScheduleBlock.cshtml` nahraď celý blok ř. 281-480:

```razor
    @if (isEditor)
    {
        <div class="schedule-table-wrap">
            … 200 řádků …
        </div>
    }
```

za:

```razor
    @if (isEditor)
    {
        @* Stránka záznamu (2026-07-14): tabulka vyčleněna do _ScheduleTable.cshtml —
           sdílí ji editor i read-only tabulkový režim stránky záznamu. *@
        @await Html.PartialAsync("~/Views/Shared/_ScheduleTable.cshtml", Model)
    }
```

- [ ] **Step 5: Přesměruj 4 existující piny na nový soubor**

Tyto testy pinují text, který se přesunul. Intent zůstává — mění se jen cesta ke zdroji.

**(a)** `PmTracker.Tests.Unit/Harmonogram/ScheduleBlockPartialManualKrokRenderingTests.cs:16` — celá třída pinuje tabulkové buňky:

```csharp
    private const string PartialPath = "PmTracker.Web/Views/Shared/_ScheduleTable.cshtml";
```

**(b)** `PmTracker.Tests.Unit/Projects/NavrhyRedesignTests.cs`, test `ScheduleBlock_ShouldHideActualWhenFlagSet` (ř. 68):

```csharp
        var block = Load("PmTracker.Web/Views/Shared/_ScheduleTable.cshtml");
```

**(c)** `PmTracker.Tests.Unit/Schedule/ScheduleBlockMarkupTests.cs`, test `EditorMode_HighlightsProposalChangedSteps_FromEditorDiff` — nahraď v tomto jednom testu `LoadScheduleBlockSource()` čtením tabulky. Přidej k němu pomocnou metodu vedle stávající:

```csharp
    private static string LoadScheduleTableSource()
    {
        var viewPath = Path.Combine(RepoRoot(), "PmTracker.Web", "Views", "Shared", "_ScheduleTable.cshtml");
        File.Exists(viewPath).Should().BeTrue($"_ScheduleTable.cshtml musí existovat na cestě {viewPath}");
        return File.ReadAllText(viewPath);
    }
```

(Pokud třída nemá `RepoRoot()`, použij stejný způsob hledání kořene, jaký už má `LoadScheduleBlockSource`.) V testu pak `var source = LoadScheduleTableSource();`.

**(d)** `PmTracker.Tests.Unit/Architecture/HarmonogramPhantomUiFixesTests.cs`, test `ScheduleBlock_DelayDateField_RespektujeNullOdchylka`:

```csharp
        var src = Read("PmTracker.Web/Views/Shared/_ScheduleTable.cshtml");
```

- [ ] **Step 6: Testy zelené + regrese editoru**

Run: `dotnet test PmTracker.Tests.Unit --nologo`
Expected: PASS, 0 failed (výchozí stav před taskem = 1570 passed; teď 1573 s novou třídou).

Run: `dotnet test PmTracker.Tests.Api --nologo`
Expected: 4 failed = známý gantt kvartet, nic víc. Ověř jmenovitě:
`dotnet test PmTracker.Tests.Api --nologo 2>&1 | grep -E "\[FAIL\]"`

- [ ] **Step 7: Vizuální regrese editoru (povinná — přesun se dělá „naslepo")**

Spusť dev app a curl-ověř, že editor pořád renderuje tabulku:

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web --nologo -clp:ErrorsOnly
# app spusť na pozadí dle dev setupu (memory feedback_esm_module_cache_busting)
curl -s "http://localhost:5071/Zaznamy/Edit/99009?asUser=1" | grep -c "schedule-table-wrap"
```
Expected: `1`

- [ ] **Step 8: Checkpoint (commit držený)** — `git status` pro přehled, žádný commit.

---

### Task 2: Server — route `/Zaznamy/Detail/{id}`, view model a kompozice

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamDetailPageViewModel.cs`
- Create: `PmTracker.Web/Services/ProjectService.RecordDetailPage.cs`
- Create: `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml` (kostra — plný layout dodá Task 3)
- Modify: `PmTracker.Web/Services/IProjectService.cs`, `PmTracker.Web/Services/IRecordService.cs`, `PmTracker.Web/Services/RecordService.cs`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs` (nová akce `Detail`)
- Modify: `PmTracker.Tests.Unit/Controllers/ProjektyControllerTeamAuthzTests.cs`, `PmTracker.Tests.Unit/Projects/ProjektyControllerBehaviorTests.cs` (fake `IProjectService` — nový člen)
- Test: `PmTracker.Tests.Api/Controllers/RecordDetailPageRenderTests.cs` (nový)

**Interfaces:**
- Consumes: `_ScheduleTable.cshtml` (Task 1); `BuildRecordScheduleBlockAsync` a `ZaznamScheduleBlockViewModel` (varianta 1).
- Produces: `Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)` na `IProjectService` i `IRecordService`; GET `/Zaznamy/Detail/{id}?returnUrl=`; třída `ZaznamDetailPageViewModel` s vlastnostmi dle Step 3.

- [ ] **Step 1: Napiš failing Api testy**

Nový soubor `PmTracker.Tests.Api/Controllers/RecordDetailPageRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Stránka záznamu (2026-07-14): read-only detail na trvalé URL —
/// bohatá hlavička, vyjádření, obě podoby harmonogramu, guard přístupu.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordDetailPageRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordDetailPageRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int recordId)> SeedTaskWithScheduleAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}Sub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} record");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });
        return (projectId, recordId);
    }

    [Fact]
    public async Task DetailPage_RendersHeaderCommentsAndBothScheduleViews()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage1");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Hlavička (bohatá — detail partial s historií a chips).
        html.Should().Contain("record-page-header");
        html.Should().Contain("record-history", "hlavička nese historii vlastníka/termínu/subsystému");
        // Vyjádření ve wrapperu karty (kvůli AJAX obnově).
        html.Should().Contain("record-card--page-column");
        html.Should().Contain("data-record-comments-shell");
        // Obě podoby harmonogramu naráz.
        html.Should().Contain("data-schedule-ticks", "grafická podoba nese serverové ticky");
        html.Should().Contain("schedule-table-wrap", "tabulková podoba je vyrenderovaná taky");
    }

    [Fact]
    public async Task DetailPage_TableView_IsReadOnly()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage2");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Datumová pole tabulky jsou zamčená → _AppDateField je renderuje jako text, ne input.
        var tableStart = html.IndexOf("schedule-table-wrap", StringComparison.Ordinal);
        tableStart.Should().BeGreaterThan(0);
        var tableEnd = html.IndexOf("</table>", tableStart, StringComparison.Ordinal);
        var tableHtml = html[tableStart..tableEnd];
        tableHtml.Should().NotContain("<input type=\"text\"", "zamčená pole se nerenderují jako editovatelný input");
        tableHtml.Should().NotContain("<select", "v read-only tabulce nejsou selecty");
    }

    [Fact]
    public async Task DetailPage_WithoutSchedule_HasNoScheduleColumn()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecPage3Owner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECPAGE3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiRecPage3Sub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "ApiRecPage3 record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-record-page-schedule", "bez harmonogramu se pravý sloupec nerenderuje");
        html.Should().NotContain("data-schedule-view-toggle", "bez harmonogramu není co přepínat");
        html.Should().Contain("data-record-comments-shell", "vyjádření zůstávají");
    }

    [Fact]
    public async Task DetailPage_NonExistentRecord_ReturnsNotFound()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/999999?asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Ověř, že testy selžou**

Run: `dotnet test PmTracker.Tests.Api --filter RecordDetailPageRenderTests --nologo`
Expected: FAIL — route neexistuje, vrací 404 (poslední test projde už teď, to je v pořádku).

- [ ] **Step 3: View model**

Nový `PmTracker.Web/Models/ViewModels/Projekty/ZaznamDetailPageViewModel.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels;

/// <summary>
/// Stránka záznamu (2026-07-14): read-only detail jednoho záznamu na trvalé URL.
/// Skládá existující bloky — hlavičku (summary + detail), vyjádření a obě podoby
/// harmonogramu (grafickou i tabulkovou). Editovatelné je jen přidání vyjádření.
/// </summary>
public sealed class ZaznamDetailPageViewModel
{
    public int ProjektId { get; init; }
    public required string ProjektZkratka { get; init; }
    public required string ProjektNazev { get; init; }

    public required ZaznamCardSummaryViewModel Summary { get; init; }
    public required ZaznamCardDetailViewModel Detail { get; init; }
    public required ZaznamCommentsPanelViewModel Comments { get; init; }

    /// <summary>Grafická podoba (pruhy + osa + rozbalený rozpad). NULL = záznam nemá harmonogram.</summary>
    public ZaznamScheduleBlockViewModel? Schedule { get; init; }

    /// <summary>Tabulková podoba (Krok / Plán / Skutečnost, zamčená). NULL = záznam nemá harmonogram.</summary>
    public HarmonogramBlockViewModel? ScheduleTable { get; init; }

    // Presentation — plní controller.
    public bool CanEditRecord { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? EditUrl { get; set; }
    public string? ScheduleProposalUrl { get; set; }
    public string? PrintPdfUrl { get; set; }
    public string? PrintWordUrl { get; set; }
    public string? BackUrl { get; set; }
}
```

- [ ] **Step 4: Kompozice v ProjectService**

Nový `PmTracker.Web/Services/ProjectService.RecordDetailPage.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    /// <summary>
    /// Stránka záznamu (2026-07-14): složí data pro read-only stránku jednoho záznamu.
    /// Reuse existujících builderů — žádná nová dotazovací logika. Tabulková podoba je
    /// tentýž blok jako grafická, jen v editor módu se zamčenými oprávněními (render
    /// datumů jako text). NULL = záznam neexistuje nebo na něj uživatel nemá přístup.
    /// Oprávnění a URL doplňuje controller (presentation vrstva).
    /// </summary>
    public async Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)
    {
        var shell = await BuildRecordCardShellAsync(projectId, recordId, ct);
        if (shell is null)
        {
            return null;
        }

        var detail = await BuildRecordCardDetailAsync(projectId, recordId, ct);
        var comments = await BuildRecordCommentsPanelAsync(projectId, recordId, ct);
        if (detail is null || comments is null)
        {
            return null;
        }

        var schedule = await BuildRecordScheduleBlockAsync(projectId, recordId, ct);
        // Tabulková podoba: stejný blok v editor módu, ale plně zamčený. ForReadOnly() má
        // IsTaskCategory = false, což v tabulce zamkne plánová i skutečnostní datumová pole
        // (_AppDateField je pak renderuje jako text, ne input).
        var scheduleTable = schedule is null
            ? null
            : schedule.HarmonogramBlok with
            {
                Mode = "record-editor",
                Permissions = ScheduleEditorPermissionSet.ForReadOnly(),
                CanEditManualActual = false
            };

        var projekt = await BuildProjektDetailAsync(projectId, ct);

        return new ZaznamDetailPageViewModel
        {
            ProjektId = projectId,
            ProjektZkratka = projekt.Zkratka,
            ProjektNazev = projekt.CelyNazev,
            Summary = shell.Summary,
            Detail = detail,
            Comments = comments,
            Schedule = schedule,
            ScheduleTable = scheduleTable
        };
    }
}
```

Pozn. pro exekuci: ověř přesná jména vlastností projektu (`grep -n "Zkratka\|CelyNazev" PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModel*.cs`) a uprav mapování, pokud se liší. Pokud `BuildProjektDetailAsync` dělá zbytečně těžký dotaz, načti zkratku/název přímo z `dbContext.Projekty` projekcí na dvě pole.

- [ ] **Step 5: Rozhraní + delegace**

Do `IProjectService.cs` vedle `BuildRecordScheduleBlockAsync`:

```csharp
    Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default);
```

Totéž do `IRecordService.cs`; do `RecordService.cs` vedle existující delegace:

```csharp
    public Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)
        => projectService.BuildRecordDetailPageAsync(projectId, recordId, ct);
```

Do obou test-fake implementací `IProjectService` (`ProjektyControllerTeamAuthzTests.cs`, `ProjektyControllerBehaviorTests.cs`) přidej vedle `BuildRecordScheduleBlockAsync`:

```csharp
        public Task<ZaznamDetailPageViewModel?> BuildRecordDetailPageAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ZaznamDetailPageViewModel?>(null);
```

- [ ] **Step 6: Akce Detail v ZaznamyController**

Do `PmTracker.Web/Controllers/ZaznamyController.cs` (nad akci `Edit`):

```csharp
    /// <summary>
    /// Stránka záznamu (2026-07-14): read-only detail na trvalé URL (sdílitelný odkaz).
    /// Guard = přístup k projektu; žádné edit právo se nevyžaduje — stránka ukazuje totéž,
    /// co uživatel vidí na kartě v záložce Záznamy. Autorizace PŘED těžkými dotazy.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detail(int id, string? returnUrl, CancellationToken ct = default)
    {
        var projektId = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();

        if (!CurrentUserContext.CanAccessProject(projektId.Value))
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordDetailPageAsync(projektId.Value, id, ct);
        if (model is null) return NotFound();

        PrepareRecordDetailPagePresentation(model, returnUrl);
        SetProjectBreadcrumbs(
            model.ProjektId, model.ProjektNazev, model.ProjektZkratka,
            currentText: $"Záznam #{model.Summary.CisloViditelne}",
            backUrl: model.BackUrl);
        return View("~/Views/Projekty/ZaznamDetailPage.cshtml", model);
    }

    private void PrepareRecordDetailPagePresentation(ZaznamDetailPageViewModel model, string? returnUrl)
    {
        var projektId = model.ProjektId;
        var recordId = model.Summary.Id;

        model.CanEditRecord = RecordEditorAffordancePolicy.CanOpenEditor(CurrentUserContext, projektId);
        model.CanCreateScheduleProposal = model.Summary.JeUkol
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projektId);

        var pageUrl = Url.Action(nameof(Detail), new { id = recordId }) ?? $"/Zaznamy/Detail/{recordId}";
        model.EditUrl = Url.Action(nameof(Edit), new { id = recordId, projektId, returnUrl = pageUrl });
        model.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy",
            new { projektId, zaznamId = recordId, returnUrl = pageUrl });
        model.PrintPdfUrl = Url.Action("UkolTisk", "Export", new { zaznamId = recordId, projektId, autoPrint = true });
        model.PrintWordUrl = Url.Action("UkolWord", "Export", new { zaznamId = recordId, projektId });
        model.BackUrl = NormalizeLocalReturnUrl(returnUrl) ?? ProjektDetailTabUrl(projektId, "zaznamy");

        // Vyjádření: stejná presentation jako na kartě (oprávnění, filtr draft jednání).
        PrepareRecordCommentsPresentation(model.Comments, model.Summary);
        model.Comments.CurrentUserOsobaId = CurrentUserContext.OsobaId;
    }
```

Pozn.: `PrepareRecordCommentsPresentation` je privátní v `ZaznamyController.Partials.cs` — stejná partial třída, takže je dostupná. `_db`, `_recordService`, `RecordEditorAffordancePolicy`, `PermissionKeys` už controller používá (vzor v akci `Edit`).

- [ ] **Step 7: Kostra view**

Nový `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml` (plný layout dodá Task 3 — teď jen aby test prošel):

```razor
@model ZaznamDetailPageViewModel
@{
    ViewData["Title"] = $"Záznam {Model.Summary.CisloViditelne}";
}

<section class="card record-page-header">
    <div class="record-title">#@Model.Summary.CisloViditelne - @Model.Summary.Nazev</div>
    @await Html.PartialAsync("~/Views/Projekty/_ZaznamDetailPartial.cshtml", Model.Detail)
</section>

<article class="record-card record-card--page-column" data-record-id="@Model.Summary.Id">
    <div class="record-body">
        @await Html.PartialAsync("~/Views/Projekty/_ZaznamCommentsPartial.cshtml", Model.Comments)
    </div>
</article>

@if (Model.Schedule is not null && Model.ScheduleTable is not null)
{
    <section class="card" data-record-page-schedule>
        @await Html.PartialAsync("~/Views/Projekty/_ZaznamSchedulePartial.cshtml", Model.Schedule)
        @await Html.PartialAsync("~/Views/Shared/_ScheduleTable.cshtml", Model.ScheduleTable)
    </section>
}
```

- [ ] **Step 8: Build + testy zelené**

Run: `dotnet build PmTracker.sln --nologo -clp:ErrorsOnly`
Expected: 0 errors.

Run: `dotnet test PmTracker.Tests.Api --filter RecordDetailPageRenderTests --nologo`
Expected: PASS 4/4.

Run: `dotnet test PmTracker.Tests.Unit --nologo`
Expected: 0 failed.

- [ ] **Step 9: Checkpoint (commit držený).**

---

### Task 3: Layout stránky — bohatá hlavička, dva sloupce, CSS

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml` (plný layout)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (nová sekce na konci `.record-page-*`)
- Test: `PmTracker.Tests.Unit/Layout/RecordDetailPageLayoutTests.cs` (nový)
- Test: `PmTracker.Tests.Api/Controllers/RecordDetailPageRenderTests.cs` (rozšíření)

**Interfaces:**
- Consumes: `ZaznamDetailPageViewModel` (Task 2).
- Produces: DOM hooky pro Task 4 — `[data-record-page-schedule]`, `.record-page-schedule--table` (třída režimu), `[data-schedule-view-toggle="graph"|"table"]`, `[data-record-page-schedule-graph]`, `[data-record-page-schedule-table]`.

- [ ] **Step 1: Napiš failing unit testy (layout + CSS)**

Nový `PmTracker.Tests.Unit/Layout/RecordDetailPageLayoutTests.cs`:

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Stránka záznamu (2026-07-14): hlavička na plnou šířku + dva sloupce 40/60,
/// pod 1100 px stohování. Jen letité CSS konstrukce (i15 pojistka).
/// </summary>
public sealed class RecordDetailPageLayoutTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Css => Read("PmTracker.Web/wwwroot/css/site.css");
    private static string View => Read("PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml");

    /// <summary>Úsek CSS patřící stránce záznamu.</summary>
    private static string PageSlice()
    {
        var css = Css;
        var start = css.IndexOf(".record-page-grid", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "sekce stránky záznamu musí v site.css existovat");
        var end = css.IndexOf("/* === konec stránky záznamu === */", start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, "sekce musí být ohraničená koncovou značkou");
        return css[start..end];
    }

    [Fact]
    public void Grid_HasTwoColumns_WithStackingBelow1100()
    {
        var slice = PageSlice();
        slice.Should().Contain("grid-template-columns: 40% 1fr",
            "vlevo vyjádření ~40 %, vpravo harmonogram zbytek (spec U1)");
        Regex.IsMatch(Css, @"@media \(max-width: 1100px\)\s*\{[^@]*\.record-page-grid", RegexOptions.Singleline)
            .Should().BeTrue("pod 1100 px se sloupce skládají pod sebe");
    }

    [Fact]
    public void PageBlock_AvoidsFragileModernCss()
    {
        var slice = PageSlice();
        slice.Should().NotContain(":has(", "i15 pojistka — nosná logika bez :has");
        slice.Should().NotContain("max-content", "i15 pojistka — bez intrinsic keywords");
        slice.Should().NotContain("min-content", "i15 pojistka — bez intrinsic keywords");
        slice.Should().NotContain("fit-content", "i15 pojistka — bez intrinsic keywords");
    }

    [Fact]
    public void ScheduleViewMode_IsDrivenByContainerClass()
    {
        var slice = PageSlice();
        slice.Should().Contain(".record-page-schedule--table [data-record-page-schedule-graph]",
            "v tabulkovém režimu se skryje graf");
        slice.Should().Contain(".record-page-schedule:not(.record-page-schedule--table) [data-record-page-schedule-table]",
            "ve výchozím (grafickém) režimu se skryje tabulka");
    }

    [Fact]
    public void CommentsWrapper_IsResetCard_WithoutCollapse()
    {
        var slice = PageSlice();
        slice.Should().Contain(".record-card--page-column",
            "wrapper karty kolem vyjádření má resetovaný vzhled");
        View.Should().NotContain("record-card--page-column collapsed",
            "wrapper nesmí být collapsed — skryl by tělo s vyjádřeními");
        View.Should().NotContain("data-record-toggle",
            "na stránce se nic nesbaluje, wrapper nemá toggle hlavičku");
    }

    [Fact]
    public void Toggle_IsButtonPair_NotGovSwitch()
    {
        View.Should().Contain("data-schedule-view-toggle=\"graph\"");
        View.Should().Contain("data-schedule-view-toggle=\"table\"");
        View.Should().Contain("aria-pressed", "segmentovaný přepínač hlásí stav přes aria-pressed");
        View.Should().NotContain("gov-form-switch",
            "přepínáme mezi dvěma pojmenovanými pohledy, ne zapnuto/vypnuto (spec §6)");
    }
}
```

- [ ] **Step 2: Ověř, že testy selžou**

Run: `dotnet test PmTracker.Tests.Unit --filter RecordDetailPageLayoutTests --nologo`
Expected: FAIL (CSS sekce ani layout markup neexistují).

- [ ] **Step 3: Plný layout view**

Přepiš `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml`:

```razor
@model ZaznamDetailPageViewModel
@{
    ViewData["Title"] = $"Záznam {Model.Summary.CisloViditelne}";
    var summary = Model.Summary;
    var hasSchedule = Model.Schedule is not null && Model.ScheduleTable is not null;
}

@* Stránka záznamu (2026-07-14): read-only detail na trvalé URL. Hlavička na plnou šířku,
   pod ní dva sloupce — vlevo vyjádření (jediná zapisující část), vpravo harmonogram
   s přepínačem Graf/Tabulka. Navigaci „zpět" nese drobečková lišta. *@
<section class="card record-page-header">
    <div class="record-page-header-top">
        <div>
            <div class="record-meta">
                <span class="pill">@summary.KategorieNazev</span>
                @if (!string.IsNullOrWhiteSpace(summary.TypUkolu))
                {
                    <span class="pill ghost">@summary.TypUkolu</span>
                }
            </div>
            <h1 class="record-title">#@summary.CisloViditelne - @summary.Nazev</h1>
            @if (!string.IsNullOrWhiteSpace(summary.Cil))
            {
                <div class="record-subtitle record-goal-subtitle">@summary.Cil</div>
            }
        </div>
        <div class="record-page-header-actions">
            <span class="badge">@summary.Stav</span>
            @if (!string.IsNullOrWhiteSpace(Model.PrintPdfUrl))
            {
                <a class="icon-btn"
                   href="@Model.PrintPdfUrl"
                   target="_blank"
                   rel="noopener"
                   aria-label="Tisk záznamu #@summary.CisloViditelne"
                   data-print-trigger="true"
                   data-print-pdf-url="@Model.PrintPdfUrl"
                   data-print-word-url="@Model.PrintWordUrl"
                   data-print-label="Tisk záznamu #@summary.CisloViditelne">
                    <gov-icon size="s" name="printer" type="components" aria-hidden="true"></gov-icon>
                </a>
            }
            @if (Model.CanEditRecord && !string.IsNullOrWhiteSpace(Model.EditUrl))
            {
                <pm-button variant="Secondary" size="Small" href="@Model.EditUrl">Upravit</pm-button>
            }
            @if (Model.CanCreateScheduleProposal && !string.IsNullOrWhiteSpace(Model.ScheduleProposalUrl))
            {
                <pm-button variant="Secondary" size="Small" href="@Model.ScheduleProposalUrl">Navrhnout změnu harmonogramu</pm-button>
            }
        </div>
    </div>
    <div class="record-summary-meta">
        <span><span class="label">Termín:</span> @(summary.AktualniTermin?.ToString("dd.MM.yyyy") ?? "-")</span>
        <span><span class="label">Založeno:</span> @summary.DatumZalozeni.ToString("dd.MM.yyyy")</span>
    </div>
    @* Bohatá část: historie vlastníka/termínu/subsystému, popis, chips externích odkazů, spolupráce. *@
    @await Html.PartialAsync("~/Views/Projekty/_ZaznamDetailPartial.cshtml", Model.Detail)
</section>

<div class="record-page-grid">
    @* Levý sloupec — vyjádření. Wrapper .record-card je NUTNÝ: AJAX obnova vyjádření
       hledá .record-card[data-record-id] a v ní [data-record-comments-shell]. Wrapper
       nesmí mít třídu collapsed (skryla by tělo) ani data-record-toggle hlavičku. *@
    <article class="record-card record-card--page-column"
             data-record-id="@summary.Id"
             data-record-comments-url="@Url.Action("RecordCommentsPartial", "Zaznamy", new { projektId = Model.ProjektId, zaznamId = summary.Id })"
             data-record-comments-base-url="@Url.Action("RecordCommentsPartial", "Zaznamy", new { projektId = Model.ProjektId, zaznamId = summary.Id })"
             data-record-comments-loaded="true">
        <div class="record-body">
            <div class="record-comments-lazy"
                 data-record-comments-shell
                 data-record-comments-scope="@summary.Id"
                 data-record-comments-loaded="true">
                @await Html.PartialAsync("~/Views/Projekty/_ZaznamCommentsPartial.cshtml", Model.Comments)
            </div>
        </div>
    </article>

    @if (hasSchedule)
    {
        <section class="card record-page-schedule" data-record-page-schedule>
            <div class="record-page-schedule-head">
                <h2>Harmonogram</h2>
                @* Přepínač Graf/Tabulka — dvě tlačítka, ne gov-form-switch: přepínáme mezi
                   dvěma pojmenovanými pohledy a vyhýbáme se gov-host omezením. *@
                <div class="schedule-view-switch" role="group" aria-label="Podoba harmonogramu">
                    <button type="button" class="schedule-view-switch-btn"
                            data-schedule-view-toggle="graph" aria-pressed="true">Graf</button>
                    <button type="button" class="schedule-view-switch-btn"
                            data-schedule-view-toggle="table" aria-pressed="false">Tabulka</button>
                </div>
            </div>
            <div data-record-page-schedule-graph>
                @await Html.PartialAsync("~/Views/Projekty/_ZaznamSchedulePartial.cshtml", Model.Schedule)
            </div>
            <div data-record-page-schedule-table>
                @await Html.PartialAsync("~/Views/Shared/_ScheduleTable.cshtml", Model.ScheduleTable)
            </div>
        </section>
    }
</div>
```

- [ ] **Step 4: CSS**

Na konec `PmTracker.Web/wwwroot/css/site.css` přidej:

```css
/* === stránka záznamu (2026-07-14) === */
/* Hlavička na plnou šířku + dva sloupce 40/60. Jen letité grid/flex základy (i15). */
.record-page-grid {
    display: grid;
    grid-template-columns: 40% 1fr;
    gap: 16px;
    align-items: start;
    margin-top: 16px;
}

.record-page-header-top {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 16px;
}

.record-page-header-actions {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-wrap: wrap;
}

.record-page-header .record-title {
    font-size: 20px;
    margin: 4px 0 0;
}

/* Wrapper karty kolem vyjádření — nese jen hooky pro AJAX obnovu, vzhled má sloupec sám. */
.record-card--page-column {
    display: block;
    border: 0;
    border-radius: 0;
    background: none;
    box-shadow: none;
    padding: 0;
}

.record-card--page-column .record-bar {
    display: none;
}

.record-page-schedule-head {
    display: flex;
    justify-content: space-between;
    align-items: center;
    gap: 12px;
    margin-bottom: 12px;
}

.record-page-schedule-head h2 {
    margin: 0;
}

/* Segmentovaný přepínač Graf/Tabulka. */
.schedule-view-switch {
    display: flex;
    border: 1px solid var(--pm-border);
    border-radius: 6px;
    overflow: hidden;
}

.schedule-view-switch-btn {
    padding: 6px 14px;
    border: 0;
    background: none;
    color: inherit;
    font: inherit;
    cursor: pointer;
}

.schedule-view-switch-btn[aria-pressed="true"] {
    background: rgba(37, 99, 235, 0.12);
    font-weight: 600;
}

/* Režim podoby řídí třída na kontejneru — žádné :has (i15). */
.record-page-schedule--table [data-record-page-schedule-graph] {
    display: none;
}

.record-page-schedule:not(.record-page-schedule--table) [data-record-page-schedule-table] {
    display: none;
}

@media (max-width: 1100px) {
    .record-page-grid {
        grid-template-columns: 1fr;
    }
}
/* === konec stránky záznamu === */
```

- [ ] **Step 5: Rozšiř Api test o hlavičku a akce**

Do `RecordDetailPageRenderTests.cs` přidej:

```csharp
    [Fact]
    public async Task DetailPage_Header_IsRich_AndActionsAreGated()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage4");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Bohatá hlavička: kategorie, název, historie, chips/spolupráce sekce z detail partialu.
        html.Should().Contain("record-page-header");
        html.Should().Contain("record-history");
        html.Should().Contain("record-summary-meta");
        // Přepínač a oba kontejnery.
        html.Should().Contain("data-schedule-view-toggle=\"graph\"");
        html.Should().Contain("data-schedule-view-toggle=\"table\"");
        html.Should().Contain("data-record-page-schedule-graph");
        html.Should().Contain("data-record-page-schedule-table");
        // Admin má edit právo → tlačítko Upravit vede do editoru s návratem na stránku.
        html.Should().Contain("Upravit");
        html.Should().Contain($"returnUrl=%2FZaznamy%2FDetail%2F{recordId}");
    }
```

- [ ] **Step 6: Testy zelené**

Run: `dotnet test PmTracker.Tests.Unit --filter RecordDetailPageLayoutTests --nologo`
Expected: PASS 5/5.

Run: `dotnet test PmTracker.Tests.Api --filter RecordDetailPageRenderTests --nologo`
Expected: PASS 5/5.

Run: `dotnet test PmTracker.Tests.Unit --nologo`
Expected: 0 failed (pokud spadl starší CSS pin, oprav ho se zachováním intentu).

- [ ] **Step 7: Checkpoint (commit držený).**

---

### Task 4: Přepínač Graf ⇄ Tabulka (JS modul)

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/recordPageScheduleView.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (side-effect import)
- Test: `PmTracker.Tests.Unit/Projects/RecordPageScheduleViewJsTests.cs` (nový)

**Interfaces:**
- Consumes: hooky z Task 3 (`[data-record-page-schedule]`, `[data-schedule-view-toggle]`, `[data-record-page-schedule-graph]`), `renderStaticTimelineAxes` ze `schedule.js`, `queueRainbowSegmentRender` z `ui.js`.
- Produces: samostatný side-effect modul; localStorage klíč `pmtracker.recordPage.scheduleView` s hodnotami `graph` | `table`.

- [ ] **Step 1: Napiš failing test**

Nový `PmTracker.Tests.Unit/Projects/RecordPageScheduleViewJsTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// Stránka záznamu (2026-07-14): přepínač Graf ⇄ Tabulka — klientský, stav v třídě
/// kontejneru + localStorage, osy se kreslí až po zviditelnění grafu.
/// </summary>
public sealed class RecordPageScheduleViewJsTests
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
    public void Module_TogglesByContainerClass_AndPersists()
    {
        var src = Js("recordPageScheduleView.js");
        src.Should().Contain("record-page-schedule--table", "režim řídí třída na kontejneru");
        src.Should().Contain("pmtracker.recordPage.scheduleView", "poloha se pamatuje v localStorage");
        src.Should().Contain("aria-pressed", "tlačítka hlásí stav");
        src.Should().NotContain("gov-form-switch", "přepínač jsou dvě tlačítka, ne gov switch");
    }

    [Fact]
    public void Module_RendersAxesWhenGraphBecomesVisible()
    {
        var src = Js("recordPageScheduleView.js");
        src.Should().Contain("renderStaticTimelineAxes",
            "osy se měří ze šířky — kreslí se až když je graf viditelný");
        src.Should().Contain("queueRainbowSegmentRender");
    }

    [Fact]
    public void Module_IsWiredInBootstrap()
    {
        Js("bootstrap.js").Should().Contain("recordPageScheduleView.js",
            "side-effect modul musí být explicitně importován (memory project_bundle_sync)");
    }
}
```

- [ ] **Step 2: Ověř, že testy selžou**

Run: `dotnet test PmTracker.Tests.Unit --filter RecordPageScheduleViewJsTests --nologo`
Expected: FAIL — soubor neexistuje.

- [ ] **Step 3: Modul**

Nový `PmTracker.Web/wwwroot/js/modules/recordPageScheduleView.js`:

```js
// recordPageScheduleView.js — přepínač Graf ⇄ Tabulka na stránce záznamu (spec 2026-07-14).
//
// Obě podoby harmonogramu jsou v DOMu (server je vyrenderuje naráz); přepínač jen mění
// třídu na kontejneru [data-record-page-schedule] a CSS skryje neaktivní. Žádný server
// round-trip, žádný lazy-load.
//
// Osy grafické podoby se měří ze šířky — ve skrytém prvku mají nulu. Proto se kreslí
// až ve chvíli, kdy je graf viditelný (při načtení v grafickém režimu, nebo při prvním
// přepnutí zpět na graf).
//
// Stav UI drží třída kontejneru (ne atribut na custom elementu) — memory
// feedback_pm_button_strips_host_attributes.
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { renderStaticTimelineAxes } from "./schedule.js";
import { queueRainbowSegmentRender } from "./ui.js";

const STORAGE_KEY = "pmtracker.recordPage.scheduleView";
const TABLE_CLASS = "record-page-schedule--table";

function readStoredView() {
    try {
        return window.localStorage.getItem(STORAGE_KEY) === "table" ? "table" : "graph";
    }
    catch {
        return "graph";
    }
}

function storeView(view) {
    try {
        window.localStorage.setItem(STORAGE_KEY, view);
    }
    catch {
        // Soukromý režim / zakázané úložiště — poloha se prostě nezapamatuje.
    }
}

function renderGraphOnce(container) {
    if (container.dataset.scheduleAxesRendered === "true") {
        return;
    }
    const graph = container.querySelector("[data-record-page-schedule-graph]");
    if (!(graph instanceof HTMLElement)) {
        return;
    }
    container.dataset.scheduleAxesRendered = "true";
    renderStaticTimelineAxes(graph);
    queueRainbowSegmentRender(graph);
}

function applyView(container, view) {
    const isTable = view === "table";
    container.classList.toggle(TABLE_CLASS, isTable);
    container.querySelectorAll("[data-schedule-view-toggle]").forEach((button) => {
        if (button instanceof HTMLElement) {
            button.setAttribute("aria-pressed", String(button.dataset.scheduleViewToggle === view));
        }
    });
    if (!isTable) {
        renderGraphOnce(container);
    }
}

function initRecordPageScheduleView() {
    const container = document.querySelector("[data-record-page-schedule]");
    if (!(container instanceof HTMLElement)) {
        return;
    }
    applyView(container, readStoredView());
}

document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const button = target.closest("[data-schedule-view-toggle]");
    if (!(button instanceof HTMLElement)) {
        return;
    }
    const container = button.closest("[data-record-page-schedule]");
    if (!(container instanceof HTMLElement)) {
        return;
    }
    event.preventDefault();
    const view = button.dataset.scheduleViewToggle === "table" ? "table" : "graph";
    applyView(container, view);
    storeView(view);
});

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initRecordPageScheduleView);
}
else {
    initRecordPageScheduleView();
}
```

- [ ] **Step 4: Bootstrap import**

Do `bootstrap.js` k side-effect importům (za `import "./recordScheduleView.js";`):

```js
import "./recordPageScheduleView.js";               // 2026-07-14 — přepínač Graf/Tabulka na stránce záznamu.
```

- [ ] **Step 5: Testy zelené + syntax check**

Run: `dotnet test PmTracker.Tests.Unit --filter RecordPageScheduleViewJsTests --nologo`
Expected: PASS 3/3.

Syntax check ES modulu (node bere `.js` jako CommonJS, proto přes `.mjs` kopii):

```bash
SCRATCH="/private/tmp/claude-501/-Users-Pavel-Andrlik-Documents-PM-Tracker/6fb75d21-d17b-4f25-891d-fc518840db34/scratchpad"
cp "PmTracker.Web/wwwroot/js/modules/recordPageScheduleView.js" "$SCRATCH/_syn.mjs"
node --check "$SCRATCH/_syn.mjs" && echo OK
rm -f "$SCRATCH/_syn.mjs"
```
Expected: `OK`

- [ ] **Step 6: Checkpoint (commit držený).**

---

### Task 5: Vstupní bod — položka „Otevřít na nové kartě" v menu karty

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` (`RecordPageUrl` na shell VM)
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (`PrepareRecordCardShellPresentation`)
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` (`PrepareRecordCardShellPresentation`)
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml` (menu položka + `hasMenuItems`)
- Test: `PmTracker.Tests.Unit/Projects/RecordCardScheduleToggleMarkupTests.cs` (rozšíření)
- Test: `PmTracker.Tests.Api/Controllers/RecordScheduleTogglePartialTests.cs` (rozšíření)

**Interfaces:**
- Consumes: route `/Zaznamy/Detail/{id}` (Task 2).
- Produces: `ProjektZaznamCardShellViewModel.RecordPageUrl`; menu položka s `target="_blank"`.

- [ ] **Step 1: Napiš failing testy**

Do `PmTracker.Tests.Unit/Projects/RecordCardScheduleToggleMarkupTests.cs` přidej:

```csharp
    [Fact]
    public void Menu_HasOpenInNewTabItem_LinkingToRecordPage()
    {
        // Stránka záznamu (2026-07-14): menu karty dostalo vstupní bod na samostatnou stránku.
        Partial.Should().Contain("Otevřít na nové kartě");
        Partial.Should().Contain("Model.RecordPageUrl");
        Regex.IsMatch(Partial, "record-actions-menu-item[\\s\\S]{0,240}target=\"_blank\"")
            .Should().BeTrue("položka otevírá stránku v nové kartě prohlížeče");
        Partial.Should().Contain("rel=\"noopener\"");
    }
```

Do `PmTracker.Tests.Api/Controllers/RecordScheduleTogglePartialTests.cs` přidej:

```csharp
    [Fact]
    public async Task RecordCard_Menu_LinksToRecordDetailPage()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecPageLinkOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECPAGELINK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiRecPageLinkSub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record page link");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("Otevřít na nové kartě");
        html.Should().Contain($"/Zaznamy/Detail/{recordId}");
    }
```

- [ ] **Step 2: Ověř, že testy selžou**

Run: `dotnet test PmTracker.Tests.Unit --filter RecordCardScheduleToggleMarkupTests --nologo`
Run: `dotnet test PmTracker.Tests.Api --filter RecordScheduleTogglePartialTests --nologo`
Expected: nové testy FAIL.

- [ ] **Step 3: VM + obě presentation cesty**

Do `ProjektZaznamCardShellViewModel` (vedle `ScheduleUrl`):

```csharp
    /// <summary>Stránka záznamu (2026-07-14): odkaz na samostatnou stránku (nová karta prohlížeče).</summary>
    public string? RecordPageUrl { get; set; }
```

Do `ProjektyController.PrepareRecordCardShellPresentation` (za `record.ScheduleUrl = …`):

```csharp
        record.RecordPageUrl = Url.Action("Detail", "Zaznamy", new { id = summary.Id, returnUrl = ProjektDetailTabUrl(projectId, RecordsTab) }) ?? $"/Zaznamy/Detail/{summary.Id}";
```

Do `ZaznamyController.Partials.cs` → `PrepareRecordCardShellPresentation` (za `record.ScheduleUrl ??= …`):

```csharp
        record.RecordPageUrl ??= Url.Action(nameof(Detail), new { id = summary.Id, returnUrl = ProjektDetailTabUrl(projektId, "zaznamy") });
```

(Obě cesty musí renderovat identickou kartu — stejné pravidlo jako u `ScheduleUrl`.)

- [ ] **Step 4: Menu položka**

V `_ZaznamPartial.cshtml` rozšiř `hasMenuItems` (položka je vidět vždy, když je URL):

```razor
    var hasMenuItems = summary.MaHarmonogramHodnotu
        || (summary.CanCreateScheduleProposal && !string.IsNullOrWhiteSpace(summary.ScheduleProposalUrl))
        || !string.IsNullOrWhiteSpace(Model.RecordPageUrl);
```

Do bloku `<div class="record-actions-menu" …>` jako **první** položku:

```razor
                            @if (!string.IsNullOrWhiteSpace(Model.RecordPageUrl))
                            {
                                <a class="record-actions-menu-item" role="menuitem"
                                   href="@Model.RecordPageUrl" target="_blank" rel="noopener">Otevřít na nové kartě</a>
                            }
```

- [ ] **Step 5: Testy zelené**

Run: `dotnet test PmTracker.Tests.Unit --nologo`
Expected: 0 failed.

Run: `dotnet test PmTracker.Tests.Api --nologo`
Expected: 4 failed = známý gantt kvartet.

- [ ] **Step 6: Checkpoint (commit držený).**

---

### Task 6: E2E + živá verifikace

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/RecordDetailPageScenariosTests.cs` (nový)
- Scratchpad: Playwright ověřovací skript + screenshoty (1470×956 a 2560×1440)

**Interfaces:**
- Consumes: vše z Tasků 1-5. E2E vzor: `RecordScheduleToggleScenariosTests.cs` (seed helper `EnsureTaskWithScheduleAsync`, `DispatchClickAsync` pro gov/vysoké prvky).

- [ ] **Step 1: Napiš E2E scénáře**

Nový `PmTracker.Tests.E2E/Scenarios/RecordDetailPageScenariosTests.cs`. Seed helper **zkopíruj beze změny** z `RecordScheduleToggleScenariosTests.EnsureTaskWithScheduleAsync` (E2E dev seed nevkládá záznamy — vytváří se přímým SQL, marker `"E2E toggle harmonogram"`; použij vlastní marker `"E2E stranka zaznamu"`).

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Stránka záznamu (2026-07-14): dvousloupcové read-only zobrazení, přepínač
/// Graf ⇄ Tabulka s pamětí polohy, ukládání vyjádření, návrat drobečkem.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordDetailPageScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public RecordDetailPageScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    // … zkopíruj EnsureTaskWithScheduleAsync z RecordScheduleToggleScenariosTests
    //    (marker "E2E stranka zaznamu"), vrací int recordId …

    // gov/vysoké prvky nejsou pro Playwright „visible/stable" → DispatchEvent click.
    private static async Task DispatchClickAsync(ILocator locator)
    {
        await locator.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await locator.First.DispatchEventAsync("click");
    }

    [Fact]
    public async Task Page_ShowsHeaderCommentsAndGraph_ByDefault()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1470, 956);
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");

        await Assertions.Expect(page.Locator(".record-page-header")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-record-comments-shell]")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-record-page-schedule]")).ToHaveCountAsync(1);

        // Výchozí = graf: tabulka skrytá, graf viditelný, osy vykreslené (mají popisky).
        var tableDisplay = await page.Locator("[data-record-page-schedule-table]")
            .EvaluateAsync<string>("el => getComputedStyle(el).display");
        tableDisplay.Should().Be("none");
        await Assertions.Expect(page.Locator(".gantt-step-row")).Not.ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-record-page-schedule-graph] .timeline-axis-label")).Not.ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Toggle_SwitchesToTable_AndSurvivesReload()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");

        await DispatchClickAsync(page.Locator("[data-schedule-view-toggle='table']"));
        await Assertions.Expect(page.Locator("[data-record-page-schedule]"))
            .ToHaveClassAsync(new Regex("record-page-schedule--table"));

        var graphDisplay = await page.Locator("[data-record-page-schedule-graph]")
            .EvaluateAsync<string>("el => getComputedStyle(el).display");
        graphDisplay.Should().Be("none");
        await Assertions.Expect(page.Locator(".schedule-table-wrap")).ToHaveCountAsync(1);

        // Poloha přežije reload (localStorage).
        await page.ReloadAsync();
        await Assertions.Expect(page.Locator("[data-record-page-schedule]"))
            .ToHaveClassAsync(new Regex("record-page-schedule--table"));

        // Zpět na graf → osy se dokreslí (ve skrytém stavu měly nulovou šířku).
        await DispatchClickAsync(page.Locator("[data-schedule-view-toggle='graph']"));
        await Assertions.Expect(page.Locator("[data-record-page-schedule]"))
            .Not.ToHaveClassAsync(new Regex("record-page-schedule--table"));
        await Assertions.Expect(page.Locator("[data-record-page-schedule-graph] .timeline-axis-label")).Not.ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task AddingComment_SavesAndRefreshesPanel()
    {
        var recordId = await EnsureTaskWithScheduleAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");

        var form = page.Locator("form.comment-form");
        if (await form.CountAsync() == 0)
        {
            // Bez otevřeného jednání se formulář nerenderuje — scénář nemá co ověřit.
            await page.Context.CloseAsync();
            return;
        }

        var before = await page.Locator("[data-comment-item]").CountAsync();
        await page.Locator("form.comment-form .ql-editor, form.comment-form textarea").First
            .FillAsync("E2E vyjadreni ze stranky zaznamu");
        await DispatchClickAsync(form.Locator("button[type='submit'], pm-button[native-type='submit']"));

        await Assertions.Expect(page.Locator("[data-comment-item]")).ToHaveCountAsync(before + 1);

        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 2: Spusť E2E**

Run: `dotnet test PmTracker.Tests.E2E --filter RecordDetailPageScenariosTests --nologo`
Expected: PASS 3/3. Při failu debuguj implementaci (systematic-debugging), ne testy. Pozor na známé pasti: `locator.ScreenshotAsync` na vysokém prvku timeoutuje (nepoužívej), gov hosty klikej `DispatchEventAsync`.

- [ ] **Step 3: Živá verifikace (Playwright, dvě rozlišení)**

Postav a spusť dev app (dev setup v memory `feedback_esm_module_cache_busting`), naseeduj kroky na aktivní task-record, pak skript ve scratchpadu ověří a odscreenshotuje:

1. **1470×956** — poměr sloupců (změř `getBoundingClientRect().width` obou sloupců; levý ≈ 40 %), hlavička na plnou šířku, graf s lícující osou.
2. **Přepnutí na Tabulku** — tabulka viditelná, žádný editovatelný input uvnitř `.schedule-table-wrap` (`querySelectorAll('input:not([type=hidden])').length === 0`).
3. **2560×1440** — sloupce vedle sebe, žádné vodorovné rolování.
4. **1000×900** — sloupce pod sebou (levý a pravý mají stejné `x`).

Screenshoty ulož do scratchpadu pro ruční i15 verifikaci uživatelem.

- [ ] **Step 4: Plná regrese**

Run: `dotnet test PmTracker.Tests.Unit --nologo` → 0 failed.
Run: `dotnet test PmTracker.Tests.Api --nologo` → jen 4 známé gantt failures.
Run: `dotnet test PmTracker.Tests.E2E --filter "RecordDetailPageScenariosTests|RecordScheduleToggleScenariosTests" --nologo` → PASS.

- [ ] **Step 5: Úklid + závěrečný report**

Smaž dev seed kroků, zastav dev app, shrň uživateli: co vzniklo, výsledky testů, screenshoty, co ověřit ručně na i15, a že commity zůstávají držené.

---

## Self-review (provedeno při psaní)

1. **Spec coverage:** §1 cíl → Tasky 2-4; U1 poměr/stohování → Task 3 (CSS + testy); U2 bohatá hlavička → Task 3 (Step 3 + Api test); U3 tabulka 3 sloupce zamčená → Task 1 (extrakce) + Task 2 (ForReadOnly) + Api test read-only; U4 přepínač v prostoru harmonogramu → Task 3 (markup v `.record-page-schedule-head`) + Task 4 (chování); §4 reuse → Tasky 2-3; §5.1 route/guard → Task 2 Step 6; §5.2 VM/kompozice → Task 2 Steps 3-5; §5.3 extrakce → Task 1; §6 přepínač → Task 4; §7 wrapper vyjádření → Task 3 Step 3 + test `CommentsWrapper_IsResetCard_WithoutCollapse` + E2E; §8 vstupní bod → Task 5; §9 omezení → Task 3 (i15 test), Task 2 (404), Task 3 (bez harmonogramu žádný sloupec); §10 testy → rozloženo do všech tasků.
2. **Placeholder scan:** žádné TBD/TODO. Dvě místa vědomě odkazují na existující kód místo doslovného opisu: Task 1 Step 3 (přesun 200 řádků — opisovat je by zvýšilo riziko chyby) a Task 6 Step 1 (seed helper kopírovaný z existujícího E2E souboru) — obojí s přesnou cestou a rozsahem řádků.
3. **Type consistency:** `BuildRecordDetailPageAsync` (ProjectService / IProjectService / IRecordService / RecordService / controller / fakes — shodná signatura); `ZaznamDetailPageViewModel` vlastnosti použité ve view odpovídají Step 3; `RecordPageUrl` (VM → obě Prepare metody → view → testy); hooky `data-record-page-schedule*`, `data-schedule-view-toggle`, třída `record-page-schedule--table` a klíč `pmtracker.recordPage.scheduleView` konzistentní mezi Task 3, 4 a 6.
