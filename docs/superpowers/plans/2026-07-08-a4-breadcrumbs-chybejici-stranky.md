# A4 — Breadcrumbs na chybějící stránky — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** 7 stránek (4× Navrhy, Search, 2× SDConnector) renderuje drobečkovou lištu; návrhové stránky kanonicky `Projekty › [projekt | ZKRATKA] › [aktuální]`.

**Architecture:** Doplnění volání existujících `BaseController` helperů — vzor identický se `ZaznamyController.cs:87` (stejný `ZaznamEditViewModel` se `ProjektNazev/ProjektZkratka`, stejná view `EditZaznamPage.cshtml`). Search/SDConnector = sekční helpery.

**Tech Stack:** ASP.NET MVC controllery, xUnit + Api render testy.

## Global Constraints
- `?asUser` propagaci řeší helpery samy (`WithAsUser`) — nepřidávat ručně.
- Texty currentText: „Nový návrh záznamu", „Návrh změny harmonogramu", „Schválení návrhu", „Převzetí návrhu" (dle tlačítek UI).
- Commity držené.

---

### Task 1: NavrhyController (4 akce)

**Files:**
- Test: `PmTracker.Tests.Unit/Architecture/NavrhyBreadcrumbTests.cs` (create)
- Test: `PmTracker.Tests.Api/Controllers/BreadcrumbCoverageTests.cs` (create — Api část jen CreateRecordProposal; ProposalDetail/Prefill vyžadují seed návrhu → kryje Unit)
- Modify: `PmTracker.Web/Controllers/NavrhyController.cs` (akce na ř. 29, 43, 56, 82)

**Interfaces:**
- Consumes: `SetProjectBreadcrumbs(int projektId, string projektNazev, string projektZkratka, (int,string)? meeting = null, string? currentText = null)` (BaseController.Breadcrumbs.cs:20); `model.ProjektNazev`, `model.ProjektZkratka` (ZaznamEditViewModel).
- Produces: breadcrumb trail na všech 4 návrhových stránkách.

- [ ] **Step 1: Failing Unit test**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>A4 (2026-07-08): všechny 4 stránkové akce NavrhyController nastavují breadcrumbs.</summary>
public sealed class NavrhyBreadcrumbTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void AllFourPageActions_SetProjectBreadcrumbs()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Controllers/NavrhyController.cs"));
        Regex.Matches(src, @"SetProjectBreadcrumbs\(").Count.Should().Be(4,
            "CreateRecordProposal, CreateScheduleProposal, ProposalDetail, PrefillCreateProposal");
        src.Should().Contain("\"Nový návrh záznamu\"");
        src.Should().Contain("\"Návrh změny harmonogramu\"");
        src.Should().Contain("\"Schválení návrhu\"");
        src.Should().Contain("\"Převzetí návrhu\"");
    }
}
```

- [ ] **Step 2: Run — FAIL** (`--filter "FullyQualifiedName~NavrhyBreadcrumbTests"`)

- [ ] **Step 3: Implementace** — do každé akce za `PrepareProposalEditorModel(model, returnUrl);` vložit (text dle akce):

```csharp
        // A4 (2026-07-08): návrhové stránky = kanonická projektová větev (vzor ZaznamyController).
        SetProjectBreadcrumbs(projektId, model.ProjektNazev, model.ProjektZkratka,
            currentText: "Nový návrh záznamu");
```
- `CreateRecordProposal` → `"Nový návrh záznamu"`
- `CreateScheduleProposal` → `"Návrh změny harmonogramu"`
- `ProposalDetail` → `"Schválení návrhu"`
- `PrefillCreateProposal` → `"Převzetí návrhu"`

- [ ] **Step 4: Run Unit — PASS**; `dotnet build PmTracker.Web -v q` 0 chyb.

- [ ] **Step 5: Api render test (CreateRecordProposal + Search dohromady, viz Task 2 file)** — viz Task 2 Step 1 (jeden soubor `BreadcrumbCoverageTests`).

### Task 2: Search + SDConnector

**Files:**
- Modify: `PmTracker.Web/Controllers/SearchController.cs` (Index, ř. ~43–49)
- Modify: `PmTracker.Web/Controllers/SDConnectorController.cs` (Index ř. ~65, Inspect ř. ~193)
- Test: `PmTracker.Tests.Api/Controllers/BreadcrumbCoverageTests.cs` (create)

**Interfaces:**
- Consumes: `SetSectionRootBreadcrumb(string)`, `SetSectionChildBreadcrumb(string rootText, string rootUrl, string currentText)`, `WithAsUserRoute()` (BaseController).

- [ ] **Step 1: Failing Api test**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>A4 (2026-07-08): stránky, kde breadcrumbs chyběly, je renderují.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbCoverageTests
{
    private readonly ApiSqlFixture _fixture;
    public BreadcrumbCoverageTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetOkAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task SearchPage_RendersBreadcrumbBar()
    {
        var html = await GetOkAsync($"/Search?q=test&asUser={_fixture.AdminOsobaId}");
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("Hledání");
    }

    [Fact]
    public async Task CreateRecordProposalPage_RendersProjectBreadcrumbs()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiBcOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIBC");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        var html = await GetOkAsync($"/Navrhy/CreateRecordProposal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("Nový návrh záznamu");
    }
}
```

- [ ] **Step 2: Run — FAIL** (Search: chybí bar; Navrhy: chybí bar — Unit z Task 1 už může být zelený, Api ověřuje render)

- [ ] **Step 3: SearchController.Index** — před `return View(...)`:
```csharp
        SetSectionRootBreadcrumb("Hledání");
```

- [ ] **Step 4: SDConnectorController** — `Index`: před `return View(vm);`:
```csharp
        SetSectionRootBreadcrumb("SD konektor");
```
`Inspect`: před `return View(vm);`:
```csharp
        SetSectionChildBreadcrumb(
            "SD konektor",
            Url.Action("Index", "SDConnector", WithAsUserRoute()) ?? "/SDConnector",
            "Inspekce");
```
Pozn.: pokud `SDConnectorController` nedědí z `BaseController` (ověřit hlavičku třídy), STOP a nahlásit — helpery jsou na BaseControlleru.

- [ ] **Step 5: Run Api — PASS**; live smoke: `curl -s "http://localhost:5071/Search?q=test&asUser=1" | grep -c app-breadcrumb-bar` → 1; totéž SDConnector Index (pokud je v dev dostupný bez SD konfigurace; 500 kvůli SD dependency = mimo scope, stačí Unit-level ověření present kódu + build).

- [ ] **Step 6: Regrese** — celé `dotnet test PmTracker.Tests.Unit -v q` zelené (pozor: `BreadcrumbAndMenuLayoutTests` a `BreadcrumbRenderTests` existují — nesmí spadnout).
