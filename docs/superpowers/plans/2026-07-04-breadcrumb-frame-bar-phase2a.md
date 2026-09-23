# Drobečková lišta — fáze 2a (rollout na jednoduché sekce)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Rozšířit drobečkovou lištu (infrastruktura hotová ve fázi 1) na zbývající sekce mimo Dokumentaci: Osoby, Nastavení, Profil, Jednání (seznam), Přehled + jeho podstránky, Číselníky.

**Architecture:** Znovupoužití hotové infry z fáze 1 — `BaseController.SetSectionRootBreadcrumb(text)` pro ploché sekce a `SetBreadcrumbs(params Breadcrumb[])` pro zanořené. Žádný nový model/partial/CSS. `_Layout` už lištu vykresluje, když je `ViewData["Breadcrumbs"]` nastaveno.

**Tech Stack:** ASP.NET Core MVC, xUnit + FluentAssertions (Api render přes `ApiSqlFixture`).

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-04-breadcrumb-frame-bar-design.md`; fáze 1 plán: `…/plans/2026-07-04-breadcrumb-frame-bar.md`.
- Kanonické vlastnictví: konkrétní jednání/záznam kořenují pod projektem (řešeno ve fázi 1). Seznam Jednání = vlastní kořen „Jednání".
- Ploché sekce (landing) = jeden kořenový drobeček, bez ←/✕ (`SetSectionRootBreadcrumb`).
- `git commit` drž do ověření uživatelem (repo pravidlo). 6 dřívějších oprav + fáze 1 jsou ve stromě — necommitovat je omylem.
- Dokumentace je mimo tento plán (fáze 2b).

---

### Task 1: Ploché kořenové sekce

Sekce bez zanoření a bez page-headeru — jen kořenový drobeček na landing akci.

**Files:**
- Modify: `PmTracker.Web/Controllers/OsobyController.cs` (Index, před `return View`)
- Modify: `PmTracker.Web/Controllers/NastaveniController.cs` (Index)
- Modify: `PmTracker.Web/Controllers/ProfilController.cs` (Index)
- Modify: `PmTracker.Web/Controllers/JednaniController.cs` (Index)
- Modify: `PmTracker.Web/Controllers/DashboardController.cs` (Index)
- Test: `PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs` (Create)

**Interfaces:**
- Consumes: `BaseController.SetSectionRootBreadcrumb(string)` (fáze 1).

- [ ] **Step 1: Write the failing test**

`PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbPhase2RenderTests
{
    private readonly ApiSqlFixture _fixture;

    public BreadcrumbPhase2RenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/Osoby", "Osoby")]
    [InlineData("/Nastaveni", "Nastavení")]
    [InlineData("/Profil", "Můj profil")]
    [InlineData("/Jednani", "Jednání")]
    [InlineData("/Dashboard", "Přehled")]
    public async Task SectionLanding_RendersRootBreadcrumb(string path, string label)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{path}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("aria-current=\"page\"");
        html.Should().Contain(label);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests" --nologo`
Expected: FAIL — `app-breadcrumb-bar` chybí na těchto stránkách.

- [ ] **Step 3: Add the one-liner to each landing action**

Do každé akce těsně před `return View(model);` (resp. `return View();`):
- `OsobyController.Index` → `SetSectionRootBreadcrumb("Osoby");`
- `NastaveniController.Index` → `SetSectionRootBreadcrumb("Nastavení");`
- `ProfilController.Index` → `SetSectionRootBreadcrumb("Můj profil");`
- `JednaniController.Index` → `SetSectionRootBreadcrumb("Jednání");`
- `DashboardController.Index` → `SetSectionRootBreadcrumb("Přehled");`

(Pozn.: `Osoby`, `Nastaveni`, `Profil`, `Dashboard`, `Jednani` dědí `BaseController`, takže helper je dostupný. Pokud některá `Index` nemá lokální `model`, zavolej helper před příslušným `return View(...)`.)

- [ ] **Step 4: Run test + build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo && dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests" --nologo`
Expected: Build 0 errors; PASS (5 InlineData).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Controllers/OsobyController.cs PmTracker.Web/Controllers/NastaveniController.cs PmTracker.Web/Controllers/ProfilController.cs PmTracker.Web/Controllers/JednaniController.cs PmTracker.Web/Controllers/DashboardController.cs PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs
git commit -m "feat(breadcrumbs): phase 2a — root crumb on flat section landings"
```

---

### Task 2: Číselníky (kořen + detail číselníku)

**Files:**
- Modify: `PmTracker.Web/Controllers/CiselnikyController.cs` (Index → root; Detail → root ▸ název číselníku)
- Test: přidat do `BreadcrumbPhase2RenderTests.cs`

**Interfaces:**
- Consumes: `SetSectionRootBreadcrumb`, `SetBreadcrumbs`; model detailu má `CiselnikNazev` (viz `CiselnikyController` build ~ř. 71).

- [ ] **Step 1: Write the failing test**

Přidej do `BreadcrumbPhase2RenderTests.cs`:
```csharp
    [Fact]
    public async Task CiselnikyIndex_RendersRootCrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("Číselníky");
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests.CiselnikyIndex" --nologo`
Expected: FAIL.

- [ ] **Step 3: Set breadcrumbs in Číselníky**

- `Index` akce (obě větve nastavující `PageTitle = "Číselníky"`): před `return View(...)` přidej `SetSectionRootBreadcrumb("Číselníky");`.
- `Detail` akce (build s `CiselnikNazev`): před `return View(model);` přidej:
```csharp
        SetBreadcrumbs(
            new Breadcrumb("Číselníky", Url.Action("Index", "Ciselniky", WithAsUserRoute()), null, false),
            new Breadcrumb(model.CiselnikNazev, null, null, true));
```
Kde `WithAsUserRoute()` je nový chráněný helper v `BaseController.Breadcrumbs.cs` (zpřístupní stávající privátní `WithAsUser(null)`):
```csharp
    protected Microsoft.AspNetCore.Routing.RouteValueDictionary WithAsUserRoute() => WithAsUser(null);
```
(Detail není závislý na projektu, takže nepoužívá `SetProjectBreadcrumbs`; kořen „Číselníky" bez ✕, aktuální = název číselníku.)

- [ ] **Step 4: Run + build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo && dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests" --nologo`
Expected: Build 0 errors; PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Controllers/CiselnikyController.cs PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs
git commit -m "feat(breadcrumbs): phase 2a — Ciselniky root + dictionary detail crumb"
```

---

### Task 3: Přehled — podstránky (Focus/News/Meetings) pod „Přehled"

Dashboard podstránky používají `_PageHeader` (jen back tlačítko, prázdný Title). Nahradit frame drobečky `Přehled ▸ <podstránka>` a odstranit `_PageHeader`.

**Files:**
- Modify: `PmTracker.Web/Controllers/DashboardController.cs` (Focus, News, Meetings)
- Modify: `PmTracker.Web/Views/Dashboard/Focus.cshtml` (odstranit `_PageHeader`, ř. 10-…)
- Modify: `PmTracker.Web/Views/Dashboard/News.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/Meetings.cshtml`
- Test: přidat do `BreadcrumbPhase2RenderTests.cs`

**Interfaces:**
- Consumes: `SetBreadcrumbs`, `WithAsUserRoute()` (Task 2).

- [ ] **Step 1: Write the failing test**

Přidej do `BreadcrumbPhase2RenderTests.cs`:
```csharp
    [Theory]
    [InlineData("/Dashboard/Focus", "Zaměření")]
    [InlineData("/Dashboard/News", "Novinky")]
    [InlineData("/Dashboard/Meetings", "Jednání")]
    public async Task DashboardSubpage_RendersUnderPrehled(string path, string label)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{path}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("app-breadcrumb-close");             // podstránka = zavíratelná entita
        html.Should().Contain($"/Dashboard?asUser={_fixture.AdminOsobaId}"); // ✕/← cíl = Přehled
        html.Should().Contain(label);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests.DashboardSubpage" --nologo`
Expected: FAIL.

- [ ] **Step 3: Set breadcrumbs in Dashboard subpages**

Helper v `BaseController.Breadcrumbs.cs`:
```csharp
    /// <summary>Podstránka sekce: Kořen(link) ▸ aktuální(bez odkazu). Kořen je zavíratelný (✕ → kořen).</summary>
    protected void SetSectionChildBreadcrumb(string rootText, string rootUrl, string currentText)
        => SetBreadcrumbs(
            new Breadcrumb(rootText, rootUrl, null, false),
            new Breadcrumb(currentText, null, null, true));
```
V `DashboardController` do akcí před `return View(...)`:
```csharp
        // Focus:
        SetSectionChildBreadcrumb("Přehled", Url.Action("Index", "Dashboard", WithAsUserRoute())!, "Zaměření");
        // News:
        SetSectionChildBreadcrumb("Přehled", Url.Action("Index", "Dashboard", WithAsUserRoute())!, "Novinky");
        // Meetings:
        SetSectionChildBreadcrumb("Přehled", Url.Action("Index", "Dashboard", WithAsUserRoute())!, "Jednání");
```

- [ ] **Step 4: Remove `_PageHeader` from the three views**

V `Focus.cshtml`, `News.cshtml`, `Meetings.cshtml` odstraň blok `@await Html.PartialAsync("_PageHeader", new PageHeaderViewModel { … })` (navigaci nese frame lišta).

- [ ] **Step 5: Run + build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --nologo && dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~BreadcrumbPhase2RenderTests" --nologo`
Expected: Build 0 errors; PASS (všechny phase-2a testy).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Controllers/DashboardController.cs PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs PmTracker.Web/Views/Dashboard/Focus.cshtml PmTracker.Web/Views/Dashboard/News.cshtml PmTracker.Web/Views/Dashboard/Meetings.cshtml PmTracker.Tests.Api/Controllers/BreadcrumbPhase2RenderTests.cs
git commit -m "feat(breadcrumbs): phase 2a — dashboard subpages under Přehled; drop _PageHeader"
```

---

## Self-Review

**Spec coverage (fáze 2 rollout mimo Dokumentaci):** ploché sekce → Task 1; Číselníky → Task 2; Přehled podstránky → Task 3. Jednání seznam = root (Task 1); konkrétní jednání/záznam už fáze 1. Dokumentace = fáze 2b (samostatný plán). ✓

**Placeholder scan:** žádné TBD; kód konkrétní. Podmíněná poznámka u Task 1 (umístění helperu podle přítomnosti `model`) je explicitní návod.

**Type consistency:** `SetSectionRootBreadcrumb(string)`, `SetBreadcrumbs(params Breadcrumb[])`, nové `WithAsUserRoute()` a `SetSectionChildBreadcrumb(string,string,string)` konzistentní napříč Task 1–3; `Breadcrumb(Text, Url, MutedSuffix, IsClosable)` dle fáze 1.

## Poznámka k ověření labelů

Labely „Zaměření/Novinky/Jednání" v Task 3 testu musí odpovídat skutečným nadpisům podstránek — při exekuci ověřit v příslušných akcích/ViewModelu a případně srovnat (test i trail používají stejný řetězec).
