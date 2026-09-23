# Drobečková lišta v aplikačním framu — implementační plán (fáze 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pevná drobečková lišta pod hlavním menu, která nahradí per-page hlavičky projektové větve (Projekty ▸ projekt ▸ jednání ▸ editor záznamu) sjednocenou navigací zanoření se šipkou zpět a křížky (✕ = jdi na rodiče).

**Architecture:** Server vykreslí trail z `ViewData["Breadcrumbs"]`. Stránka (controller) přispěje svou cestu přes helper na `BaseController`; `_Layout` vykreslí partial `_BreadcrumbBar.cshtml` pod `app-nav`. Drobečky jsou obyčejné `<a href>` (GET) — rodič = předchozí drobeček, takže ✕ i ← jsou jen odkazy. Žádný nový JS (dirty-check editoru chytí odkazy stávajícím `maybeGuardOutboundNavigation`).

**Tech Stack:** ASP.NET Core MVC (Razor), gov-design-system web components (`gov-icon` = Bootstrap Icons), vanilla CSS v `site.css`. Testy: xUnit + FluentAssertions (Unit, Api render přes `ApiSqlFixture`, E2E přes Playwright.NET `E2ETestFixture`).

## Global Constraints

- Branch: `codex/senior-refactor-fase-1`. Ve stromě je 6 nesouvisejících necommitnutých oprav — **této práce se netýkají**; necommituj je. Nové commity dělej jen k breadcrumb souborům.
- Spec: `docs/superpowers/specs/2026-07-04-breadcrumb-frame-bar-design.md` (zdroj pravdy pro rozhodnutí).
- Ikony vždy `<gov-icon name="…" type="components">` (Bootstrap Icons; `arrow-left`, `x`).
- Kořen sekce má `IsClosable=false` (bez ✕). Aktuální (poslední) drobeček má `Url=null` → `aria-current="page"`, není odkaz.
- Rodič = předchozí drobeček: ✕ na drobečku `i` → `Items[i-1].Url`; ← → `Items[Count-2].Url`.
- Rozsah = jen projektová větev (opt-in). Sekce bez `SetBreadcrumbs(...)` zůstávají beze změny.
- Testy běží: Unit `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj`; Api `PmTracker.Tests.Api/…` (potřebuje SQL/Colima); E2E `PmTracker.Tests.E2E/…` (SQL + `dotnet run`).

---

### Task 1: Breadcrumb model + BaseController helper

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Breadcrumbs.cs`
- Create: `PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs`
- Test: `PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs`

**Interfaces:**
- Produces:
  - `record Breadcrumb(string Text, string? Url, string? MutedSuffix, bool IsClosable)`
  - `record BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items)` s `string? ParentUrl` (= `Items[^2].Url` nebo null).
  - `BaseController.SetBreadcrumbs(params Breadcrumb[] items)`
  - `BaseController.SetProjectBreadcrumbs(int projektId, string projektNazev, string projektZkratka, (int Id, string Label)? meeting = null, string? currentText = null)`
  - `BaseController.SetSectionRootBreadcrumb(string text)`

- [ ] **Step 1: Write the failing test**

`PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs`:
```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Unit.Layout;

public sealed class BreadcrumbTrailTests
{
    [Fact]
    public void ParentUrl_IsSecondToLastItemUrl()
    {
        var trail = new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Projekty", "/Projekty", null, false),
            new Breadcrumb("Alfa", "/Projekty/Detail/7", "ALF", true),
            new Breadcrumb("Jednání 12", null, null, true),
        });

        trail.ParentUrl.Should().Be("/Projekty/Detail/7");
    }

    [Fact]
    public void ParentUrl_IsNull_ForSingleRoot()
    {
        var trail = new BreadcrumbTrail(new[] { new Breadcrumb("Projekty", null, null, false) });
        trail.ParentUrl.Should().BeNull();
    }

    [Fact]
    public void Current_IsLastItem_WithNullUrl()
    {
        var current = new Breadcrumb("Jednání 12", null, null, true);
        var trail = new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Projekty", "/Projekty", null, false),
            current,
        });

        trail.Items[^1].Should().Be(current);
        trail.Items[^1].Url.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~BreadcrumbTrailTests" --nologo`
Expected: FAIL — `Breadcrumb`/`BreadcrumbTrail` neexistují (compile error).

- [ ] **Step 3: Create the model**

`PmTracker.Web/Models/ViewModels/Breadcrumbs.cs`:
```csharp
namespace PmTracker.Web.Models.ViewModels;

/// <summary>Jeden drobeček. Url=null → aktuální (aria-current). IsClosable=false → kořen sekce (bez ✕).</summary>
public sealed record Breadcrumb(string Text, string? Url, string? MutedSuffix, bool IsClosable);

/// <summary>Uspořádaná drobečková cesta. Rodič libovolného drobečku = předchozí drobeček.</summary>
public sealed record BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items)
{
    /// <summary>Cíl šipky ← = URL předposledního drobečku (rodič aktuálního); null když je jen kořen.</summary>
    public string? ParentUrl => Items.Count >= 2 ? Items[Items.Count - 2].Url : null;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~BreadcrumbTrailTests" --nologo`
Expected: PASS (3/3).

- [ ] **Step 5: Add the BaseController helper**

`PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs`:
```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    protected void SetBreadcrumbs(params Breadcrumb[] items)
        => ViewData["Breadcrumbs"] = new BreadcrumbTrail(items);

    /// <summary>Top-level seznam sekce (např. „Projekty") — jediný kořenový drobeček, bez ✕/←.</summary>
    protected void SetSectionRootBreadcrumb(string text)
        => SetBreadcrumbs(new Breadcrumb(text, null, null, false));

    /// <summary>
    /// Kanonická projektová větev: Projekty ▸ projekt ▸ [jednání ▸] [aktuální].
    /// Poslední přítomný prvek je „aktuální" (Url=null). Zachovává ?asUser (dev impersonace).
    /// </summary>
    protected void SetProjectBreadcrumbs(
        int projektId,
        string projektNazev,
        string projektZkratka,
        (int Id, string Label)? meeting = null,
        string? currentText = null)
    {
        var items = new List<Breadcrumb>
        {
            new("Projekty", ProjektyUrl(), null, false),
        };

        var projectIsCurrent = meeting is null && currentText is null;
        items.Add(new(projektNazev, projectIsCurrent ? null : ProjektDetailUrl(projektId), projektZkratka, true));

        if (meeting is { } m)
        {
            var meetingIsCurrent = currentText is null;
            items.Add(new(m.Label, meetingIsCurrent ? null : JednaniDetailUrl(m.Id), null, true));
        }

        if (currentText is not null)
        {
            items.Add(new(currentText, null, null, true));
        }

        SetBreadcrumbs(items.ToArray());
    }

    private string ProjektyUrl() => Url.Action("Index", "Projekty", WithAsUser(null)) ?? "/Projekty";
    private string ProjektDetailUrl(int id) => Url.Action("Detail", "Projekty", WithAsUser(new { id })) ?? $"/Projekty/Detail/{id}";
    private string JednaniDetailUrl(int id) => Url.Action("Detail", "Jednani", WithAsUser(new { id })) ?? $"/Jednani/Detail/{id}";

    private RouteValueDictionary WithAsUser(object? routeValues)
    {
        var rvd = routeValues is null ? new RouteValueDictionary() : new RouteValueDictionary(routeValues);
        var asUser = Request.Query["asUser"].ToString();
        if (!string.IsNullOrEmpty(asUser))
        {
            rvd["asUser"] = asUser;
        }
        return rvd;
    }
}
```

- [ ] **Step 6: Build to verify compilation**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Breadcrumbs.cs PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs
git commit -m "feat(breadcrumbs): model + BaseController helper for project-branch trail"
```

---

### Task 2: `_BreadcrumbBar` partial + `_Layout` wiring + CSS

**Files:**
- Create: `PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml`
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml:125-126` (po `</nav>` uvnitř `<header>`)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (přidat blok za `.app-nav` pravidla, ~ř. 604)
- Test: `PmTracker.Tests.Unit/Layout/BreadcrumbBarMarkupTests.cs`

**Interfaces:**
- Consumes: `BreadcrumbTrail` (Task 1), `ViewData["Breadcrumbs"]`.
- Produces: DOM `nav.app-breadcrumb-bar > ol.app-breadcrumb-list > li.app-breadcrumb-item` s třídami `app-breadcrumb-back`, `app-breadcrumb-link`, `app-breadcrumb-current`, `app-breadcrumb-suffix`, `app-breadcrumb-close`.

- [ ] **Step 1: Write the failing markup test**

`PmTracker.Tests.Unit/Layout/BreadcrumbBarMarkupTests.cs`:
```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

public sealed class BreadcrumbBarMarkupTests
{
    [Fact]
    public void Partial_RendersNavList_WithBackCloseAndCurrentHooks()
    {
        var cshtml = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml"));
        cshtml.Should().Contain("app-breadcrumb-bar");
        cshtml.Should().Contain("aria-label=\"Drobečková navigace\"");
        cshtml.Should().Contain("<ol");
        cshtml.Should().Contain("app-breadcrumb-back");
        cshtml.Should().Contain("app-breadcrumb-close");
        cshtml.Should().Contain("aria-current=\"page\"");
        cshtml.Should().Contain("name=\"arrow-left\"");
        cshtml.Should().Contain("name=\"x\"");
    }

    [Fact]
    public void Layout_RendersBreadcrumbBar_WhenTrailPresent()
    {
        var layout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("ViewData[\"Breadcrumbs\"]");
        layout.Should().Contain("_BreadcrumbBar");
    }

    [Fact]
    public void SiteCss_DefinesStickyBreadcrumbBar()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().Contain(".app-breadcrumb-bar");
        css.Should().MatchRegex(@"\.app-breadcrumb-bar[^}]*position:\s*sticky");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~BreadcrumbBarMarkupTests" --nologo`
Expected: FAIL — soubory/pravidla neexistují.

- [ ] **Step 3: Create the partial**

`PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml`:
```razor
@model PmTracker.Web.Models.ViewModels.BreadcrumbTrail
@if (Model is { Items.Count: > 0 })
{
    <nav class="app-breadcrumb-bar" aria-label="Drobečková navigace">
        @if (Model.ParentUrl is not null)
        {
            <a class="app-breadcrumb-back" href="@Model.ParentUrl" aria-label="Zpět o úroveň výš">
                <gov-icon name="arrow-left" type="components"></gov-icon>
            </a>
        }
        <ol class="app-breadcrumb-list">
            @for (var i = 0; i < Model.Items.Count; i++)
            {
                var item = Model.Items[i];
                var isCurrent = i == Model.Items.Count - 1;
                var parentOfThis = i > 0 ? Model.Items[i - 1].Url : null;
                <li class="app-breadcrumb-item">
                    @if (item.Url is not null && !isCurrent)
                    {
                        <a class="app-breadcrumb-link" href="@item.Url">
                            <span class="app-breadcrumb-text">@item.Text</span>@if (item.MutedSuffix is not null)
                            {<span class="app-breadcrumb-suffix"> | @item.MutedSuffix</span>}
                        </a>
                    }
                    else
                    {
                        <span class="app-breadcrumb-current" aria-current="page">
                            <span class="app-breadcrumb-text">@item.Text</span>@if (item.MutedSuffix is not null)
                            {<span class="app-breadcrumb-suffix"> | @item.MutedSuffix</span>}
                        </span>
                    }
                    @if (item.IsClosable && parentOfThis is not null)
                    {
                        <a class="app-breadcrumb-close" href="@parentOfThis" aria-label="Zavřít @item.Text">
                            <gov-icon name="x" type="components"></gov-icon>
                        </a>
                    }
                </li>
            }
        </ol>
    </nav>
}
```

- [ ] **Step 4: Wire into `_Layout.cshtml`**

Modify `PmTracker.Web/Views/Shared/_Layout.cshtml` — najdi konec `<nav class="app-nav">` (ř. 125) a hned za `</nav>` (před `</header>` na ř. 126) vlož:
```razor
        </nav>

        @{
            var breadcrumbTrail = ViewData["Breadcrumbs"] as PmTracker.Web.Models.ViewModels.BreadcrumbTrail;
        }
        @if (breadcrumbTrail is not null)
        {
            <partial name="_BreadcrumbBar" model="breadcrumbTrail" />
        }
    </header>
```
(Pozn.: `</nav>` a `</header>` už v souboru jsou — vkládá se blok mezi ně.)

- [ ] **Step 5: Add CSS**

Do `PmTracker.Web/wwwroot/css/site.css` za pravidlo `.app-nav-link.active { … }` (~ř. 604) přidej:
```css
.app-breadcrumb-bar {
    position: sticky;
    top: 0;
    z-index: 40;
    display: flex;
    align-items: center;
    gap: 0.5rem;
    min-height: 2.25rem;
    padding: 0.25rem 1rem;
    background: var(--pm-surface);
    border-top: 1px solid var(--pm-border);
    border-bottom: 1px solid var(--pm-border);
    font-size: 0.875rem;
}
.app-breadcrumb-back {
    display: inline-flex;
    align-items: center;
    color: var(--pm-text-muted);
    padding: 0.125rem;
    border-radius: 0.25rem;
}
.app-breadcrumb-back:hover { color: var(--pm-text); background: var(--pm-border); }
.app-breadcrumb-list {
    display: flex;
    align-items: center;
    gap: 0.25rem;
    list-style: none;
    margin: 0;
    padding: 0;
    min-width: 0;
    overflow: hidden;
}
.app-breadcrumb-item {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    min-width: 0;
}
.app-breadcrumb-item:not(:first-child)::before {
    content: "›";
    color: var(--pm-text-muted);
    padding: 0 0.25rem;
}
.app-breadcrumb-link,
.app-breadcrumb-current {
    display: inline-flex;
    align-items: baseline;
    max-width: 22rem;
    min-width: 0;
    white-space: nowrap;
    overflow: hidden;
}
.app-breadcrumb-text { overflow: hidden; text-overflow: ellipsis; }
.app-breadcrumb-link { color: var(--pm-text); }
.app-breadcrumb-link:hover .app-breadcrumb-text { text-decoration: underline; }
.app-breadcrumb-current { color: var(--pm-text-muted); font-weight: 600; }
.app-breadcrumb-suffix { color: var(--pm-text-muted); font-weight: 400; }
.app-breadcrumb-close {
    display: inline-flex;
    align-items: center;
    color: var(--pm-text-muted);
    padding: 0.0625rem;
    border-radius: 0.25rem;
}
.app-breadcrumb-close:hover { color: var(--pm-text); background: var(--pm-border); }

/* Úzká šířka: schovej všechny drobečky kromě aktuálního, nech ← + aktuální. */
@media (max-width: 640px) {
    .app-breadcrumb-item:not(:last-child) { display: none; }
    .app-breadcrumb-item:not(:first-child)::before { content: none; }
}
```

- [ ] **Step 6: Run markup tests to verify they pass**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~BreadcrumbBarMarkupTests" --nologo`
Expected: PASS (3/3).

- [ ] **Step 7: Build web to verify Razor compiles**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml PmTracker.Web/Views/Shared/_Layout.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Unit/Layout/BreadcrumbBarMarkupTests.cs
git commit -m "feat(breadcrumbs): _BreadcrumbBar partial, _Layout wiring, sticky bar CSS"
```

---

### Task 3: Projekty/Index + Projekty/Detail (odstranění page-headeru, přesun stavu)

**Files:**
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs:44-71` (Index) a `:74-93` (Detail)
- Modify: `PmTracker.Web/Views/Projekty/Detail.cshtml:20-34` (smazat page-header) + `:36` (přesun stavu do řádku záložek)
- Modify: `PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs:20-38` (aktualizovat rozbitý test)
- Test: `PmTracker.Tests.Api/Controllers/BreadcrumbRenderTests.cs` (nový)

**Interfaces:**
- Consumes: `SetSectionRootBreadcrumb`, `SetProjectBreadcrumbs` (Task 1); `ProjektDetailViewModel.Projekt` = `ProjektHeaderViewModel { Nazev, Zkratka, Stav }`.

- [ ] **Step 1: Write the failing Api render test**

`PmTracker.Tests.Api/Controllers/BreadcrumbRenderTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public BreadcrumbRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjectDetail_RendersBreadcrumbBar_WithProjectCrumbAndCloseToList()
    {
        var ownerId = await _fixture.EnsurePersonAsync("BcProjOwner");
        var projectId = await _fixture.EnsureProjectAsync("BCPROJ");
        var subsystemId = await _fixture.EnsureSubsystemAsync("BCPROJSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar", html);
        html.Should().Contain("aria-current=\"page\"");                 // projekt = aktuální
        html.Should().Contain("app-breadcrumb-suffix");                 // " | ZKR"
        html.Should().Contain($"/Projekty?asUser={_fixture.AdminOsobaId}"); // ✕/← cíl = seznam
        // starý page-header už NE:
        html.Should().NotContain("project-title-inline");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbRenderTests" --nologo`
Expected: FAIL — `app-breadcrumb-bar` chybí, `project-title-inline` stále přítomen.

- [ ] **Step 3: Set breadcrumbs in the controller**

V `ProjektyController.cs`, akce `Index` (před `return View(model);` ~ř. 70):
```csharp
        SetSectionRootBreadcrumb("Projekty");
        return View(model);
```
Akce `Detail` (před `return View(model);` ~ř. 92):
```csharp
        SetProjectBreadcrumbs(model.Projekt.Id, model.Projekt.Nazev, model.Projekt.Zkratka);
        return View(model);
```
(Pozn.: pokud `ProjektHeaderViewModel` nemá `Id`, použij `id` z parametru akce: `SetProjectBreadcrumbs(id, model.Projekt.Nazev, model.Projekt.Zkratka);`.)

- [ ] **Step 4: Remove the page-header, move status into the tab strip**

V `PmTracker.Web/Views/Projekty/Detail.cshtml` smaž celou sekci ř. 20-34 (`<section class="page-header page-header-compact">…</section>`).
Do `<div class="tabs" role="tablist">` (ř. 36) přidej hned za otevírací tag stav projektu (vlevo od záložek):
```razor
<div class="tabs" role="tablist">
    @if (!string.IsNullOrWhiteSpace(Model.Projekt.Stav))
    {
        <span class="badge project-status-inline tabs-status">@Model.Projekt.Stav</span>
    }
```
A do `site.css` (za blok z Tasku 2) přidej:
```css
.tabs-status { align-self: center; margin-right: 0.5rem; }
```

- [ ] **Step 5: Update the now-broken existing test**

V `PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs` nahraď tělo testu `Detail_ShouldRenderInlineProjectHeader_LikeMainLayout` (ř. 20-38) — projektová hlavička se přesunula do drobečkové lišty:
```csharp
    [Fact]
    public async Task Detail_ShouldRenderProjectIdentityInBreadcrumbBar()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectHeaderOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMHDR");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBHDR", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API project header record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("app-breadcrumb-suffix");        // " | APIHARMHDR"
        html.Should().Contain("APIHARMHDR");
        html.Should().NotContain("project-title-inline");
    }
```

- [ ] **Step 6: Run both Api test classes to verify they pass**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbRenderTests|FullyQualifiedName~ProjectHarmonogramRenderTests" --nologo`
Expected: PASS (nové + upravený zelené).

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Controllers/ProjektyController.cs PmTracker.Web/Views/Projekty/Detail.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Api/Controllers/BreadcrumbRenderTests.cs PmTracker.Tests.Api/Controllers/ProjectHarmonogramRenderTests.cs
git commit -m "feat(breadcrumbs): project list + detail trail; drop project page-header, move status to tab strip"
```

---

### Task 4: Jednani/Detail (mezidrobeček jednání pod projektem)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/JednaniViewModels.cs` (přidat `ProjektZkratka` do `JednaniDetailViewModel`)
- Modify: služba `MeetingService` build metoda `BuildJednaniDetailAsync` (naplnit `ProjektZkratka`)
- Modify: `PmTracker.Web/Controllers/JednaniController.cs:59-90` (Detail — `SetProjectBreadcrumbs` s meeting)
- Modify: `PmTracker.Web/Views/Jednani/Detail.cshtml` (odstranit `_PageHeader`)
- Test: přidat do `PmTracker.Tests.Api/Controllers/BreadcrumbRenderTests.cs`

**Interfaces:**
- Consumes: `JednaniDetailViewModel { ProjektId, ProjektNazev, ProjektZkratka, PageTitle }` (PageTitle = popisek jednání).

- [ ] **Step 1: Write the failing Api render test**

Přidej do `BreadcrumbRenderTests.cs`:
```csharp
    [Fact]
    public async Task MeetingDetail_RendersProjectThenMeetingCrumbs()
    {
        var ownerId = await _fixture.EnsurePersonAsync("BcMeetOwner");
        var projectId = await _fixture.EnsureProjectAsync("BCMEET");
        var subsystemId = await _fixture.EnsureSubsystemAsync("BCMEETSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var meetingId = await _fixture.EnsureMeetingAsync(projectId, ownerId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}"); // projekt crumb + ✕ jednání cíl
        html.Should().Contain("app-breadcrumb-close");
    }
```
(Pozn.: pokud `ApiSqlFixture` nemá `EnsureMeetingAsync`, přidej helper vedle `EnsureRecordAsync` v `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs`, který vloží řádek do `jednani` s `projekt_id` a vrátí `id`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbRenderTests.MeetingDetail" --nologo`
Expected: FAIL — chybí drobečky (a případně `EnsureMeetingAsync`).

- [ ] **Step 3: Add `ProjektZkratka` to the VM + populate it**

V `JednaniViewModels.cs` do `JednaniDetailViewModel` přidej vedle `ProjektNazev`:
```csharp
    public required string ProjektZkratka { get; init; }
```
V `MeetingService.BuildJednaniDetailAsync` nastav `ProjektZkratka = <projekt>.Zkratka` (projekt už metoda načítá kvůli `ProjektNazev`).

- [ ] **Step 4: Set breadcrumbs in the controller**

V `JednaniController.cs`, akce `Detail`, před `return View(model);`:
```csharp
        SetProjectBreadcrumbs(
            model.ProjektId,
            model.ProjektNazev,
            model.ProjektZkratka,
            meeting: (model.Jednani.Id, model.PageTitle));
```

- [ ] **Step 5: Remove `_PageHeader` from the meeting view**

V `PmTracker.Web/Views/Jednani/Detail.cshtml` odstraň volání `<partial name="_PageHeader" … />` (resp. `@await Html.PartialAsync("_PageHeader", …)`). Titulek jednání teď nese aktuální drobeček.

- [ ] **Step 6: Run tests + build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo && dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbRenderTests" --nologo`
Expected: Build 0 errors; PASS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/JednaniViewModels.cs PmTracker.Web/Services PmTracker.Web/Controllers/JednaniController.cs PmTracker.Web/Views/Jednani/Detail.cshtml PmTracker.Tests.Api/Controllers/BreadcrumbRenderTests.cs PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs
git commit -m "feat(breadcrumbs): meeting detail nests under project; drop meeting page-header"
```

---

### Task 5: Zaznamy Edit/Create (aktuální drobeček záznamu, volitelně pod jednáním)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs` (přidat `ProjektNazev`, `ProjektZkratka`, `MeetingLabel`)
- Modify: `RecordService` build metody (`BuildZaznamEditAsync`/`BuildZaznamCreateAsync`) — naplnit nová pole
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs` (`Edit` ~ř. 86, `Create` ~ř. 103) — `SetProjectBreadcrumbs`
- Modify: `PmTracker.Web/Views/Projekty/EditZaznamPage.cshtml` (odstranit „Zpět" page-header)
- Test: přidat do `BreadcrumbRenderTests.cs`

**Interfaces:**
- Consumes: `ZaznamEditViewModel { ProjektId, ProjektNazev, ProjektZkratka, MeetingLabel, IsCreate, Id, UiContext, MeetingId }`.

- [ ] **Step 1: Write the failing Api render test**

Přidej do `BreadcrumbRenderTests.cs`:
```csharp
    [Fact]
    public async Task RecordEditor_RendersRecordAsCurrentCrumb_UnderProject()
    {
        var ownerId = await _fixture.EnsurePersonAsync("BcRecOwner");
        var projectId = await _fixture.EnsureProjectAsync("BCREC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("BCRECSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Bc editor record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain($"Záznam #{recordId}");
        html.Should().Contain($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}"); // ✕ záznamu → projekt
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbRenderTests.RecordEditor" --nologo`
Expected: FAIL — drobečky chybí.

- [ ] **Step 3: Add fields to the VM + populate**

V `ZaznamEditViewModels.cs` přidej do `ZaznamEditViewModel`:
```csharp
    public string ProjektNazev { get; set; } = string.Empty;
    public string ProjektZkratka { get; set; } = string.Empty;
    public string? MeetingLabel { get; set; }
```
V `RecordService` build metodách nastav `ProjektNazev`/`ProjektZkratka` z načteného projektu; `MeetingLabel` nastav jen když je editor v meeting kontextu (`contextMeetingId`/`UiContext=="meeting"`) — z popisku daného jednání.

- [ ] **Step 4: Set breadcrumbs in the controller**

V `ZaznamyController.cs` v `Edit` (za `PrepareRecordEditorModel(...)`, před `return View(...)`, ~ř. 86) a v `Create` (~ř. 109):
```csharp
        var currentCrumb = model.IsCreate ? "Nový záznam" : $"Záznam #{model.Id}";
        (int Id, string Label)? meetingCrumb =
            string.Equals(model.UiContext, "meeting", StringComparison.OrdinalIgnoreCase)
                && model.MeetingId is > 0
                && model.MeetingLabel is not null
                ? (model.MeetingId.Value, model.MeetingLabel)
                : null;
        SetProjectBreadcrumbs(model.ProjektId, model.ProjektNazev, model.ProjektZkratka, meetingCrumb, currentCrumb);
```

- [ ] **Step 5: Remove the editor page-header back button**

V `PmTracker.Web/Views/Projekty/EditZaznamPage.cshtml` odstraň `_PageHeader` partial (tlačítko „Zpět") — navigaci zpět teď zajišťuje drobečková lišta (a je chráněná dirty-check guardem). Akční lišta editoru (Uložit/Zrušit) zůstává beze změny.

- [ ] **Step 6: Run tests + build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo && dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbRenderTests" --nologo`
Expected: Build 0 errors; PASS (všechny 4 drobečkové render testy).

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs PmTracker.Web/Services PmTracker.Web/Controllers/ZaznamyController.cs PmTracker.Web/Views/Projekty/EditZaznamPage.cshtml PmTracker.Tests.Api/Controllers/BreadcrumbRenderTests.cs
git commit -m "feat(breadcrumbs): record editor trail (record current crumb, optional meeting parent); drop editor page-header"
```

---

### Task 6: E2E chování (✕, ←, dirty-guard)

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/BreadcrumbBarScenariosTests.cs`

**Interfaces:**
- Consumes: `E2ETestFixture` (`BaseUrl`, `ProjectId`, `AdminOsobaId`, `NewPageAsync`).

- [ ] **Step 1: Write the E2E test**

`PmTracker.Tests.E2E/Scenarios/BreadcrumbBarScenariosTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class BreadcrumbBarScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public BreadcrumbBarScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjectCrumbClose_NavigatesToProjectList()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");

        var bar = page.Locator(".app-breadcrumb-bar");
        await Assertions.Expect(bar).ToBeVisibleAsync();

        // ✕ na projektovém (aktuálním) drobečku → seznam Projekty
        await bar.Locator(".app-breadcrumb-close").First.ClickAsync();
        await page.WaitForURLAsync("**/Projekty**");
        page.Url.Should().Contain("/Projekty");
        page.Url.Should().NotContain("/Detail/");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task BackArrow_OnProjectDetail_GoesToProjectList()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");

        await page.Locator(".app-breadcrumb-back").ClickAsync();
        await page.WaitForURLAsync("**/Projekty**");
        page.Url.Should().NotContain("/Detail/");

        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 2: Build the E2E project to verify it compiles**

Run: `dotnet build PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --nologo`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run E2E (dev/SQL prostředí)**

Run: `dotnet test PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --filter "FullyQualifiedName~BreadcrumbBarScenariosTests" --nologo`
Expected: PASS (2/2). *(Vyžaduje SQL/Colima. Pokud DB není, test se spustí na dev/Citrix.)*

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/BreadcrumbBarScenariosTests.cs
git commit -m "test(breadcrumbs): E2E — crumb close + back-arrow navigate to parent"
```

---

## Self-Review

**Spec coverage:**
- Datový model + rodič=předchozí → Task 1. ✓
- Lišta pod menu + gov styl + sticky + a11y + přetečení → Task 2. ✓
- Kanonické vlastnictví / kořeny sekcí → Task 1 helper + Task 3/4/5 volání. ✓
- Chování ←/✕/text jako `<a href>`, dirty-check zdarma → Task 2 (odkazy) + Task 5 (editor, guard existuje). ✓
- Rozsah fáze 1 (Index, Detail, Jednani/Detail, editor) + odstranění page-headerů + přesun stavu → Task 3/4/5. ✓
- Testy Unit/Api-render/E2E → Task 1/2 (Unit), 3/4/5 (Api), 6 (E2E). ✓

**Placeholder scan:** žádné TBD; kód je konkrétní. Dvě „pokud VM/fixture nemá X, doplň" poznámky (Id na ProjektHeaderViewModel; `EnsureMeetingAsync`) jsou explicitní podmíněné kroky s postupem, ne placeholdery.

**Type consistency:** `Breadcrumb(Text, Url, MutedSuffix, IsClosable)` a `BreadcrumbTrail.Items/ParentUrl` konzistentní napříč Task 1–6; `SetProjectBreadcrumbs(projektId, nazev, zkratka, meeting?, currentText?)` volané stejně v Task 3/4/5.

## Poznámka k commitům

Kroky „Commit" jsou pro exekuci. Repo má pravidlo „commit až uživatel ověří" — při exekuci se drž tohoto: uživatel ověří ručně a dá pokyn, jinak commity drž. 6 nesouvisejících oprav ve stromě necommituj.
