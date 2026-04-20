# Širokoúhlý layout Fáze 1+2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přepnout 6 data-heavy views na fluid outer container s clamp padding, aby full využili šířku FHD i 4K monitorů. Default 1280px ponechán pro text/form views.

**Architecture:** 2-tier layout policy přes `ViewData["BodyClass"]` + `_Layout.cshtml` switch. Fluid tier dostane `clamp(16px, 2vw, 48px)` padding (tablet → 4K škálování). Content-driven inner constraints řešíme case-by-case, ne upfront. Žádný mid-tier pixel cap (anti-pattern pro multi-monitor).

**Tech Stack:** ASP.NET Core 8 MVC Razor, CSS custom properties + clamp(), xUnit architecture tests.

**Spec:** [docs/superpowers/specs/2026-04-20-sirokouhly-layout-design.md](../specs/2026-04-20-sirokouhly-layout-design.md)

---

## File Structure

**Modified:**
- `PmTracker.Web/wwwroot/css/site.css` — update `.app-main--fluid` rule (clamp padding + margin reset)
- `PmTracker.Web/Views/Projekty/Detail.cshtml` — Task 2
- `PmTracker.Web/Views/Projekty/Index.cshtml` — Task 3
- `PmTracker.Web/Views/ProjectDashboard/Index.cshtml` — Task 4 (+ potenciální cleanup `.dashboard-fullwidth`)
- `PmTracker.Web/Views/Jednani/Detail.cshtml` — Task 5
- `PmTracker.Web/Views/Search/Index.cshtml` — Task 6
- `PmTracker.Web/Views/Dashboard/Focus.cshtml` — Task 7

**Created:**
- `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` — architecture tests Task 1

---

## Task 1: Infrastructure — clamp padding + architecture tests

**Files:**
- Create: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs`
- Modify: `PmTracker.Web/wwwroot/css/site.css:788-793` (`.app-main` default) + nové rules pro `.app-main--fluid`

- [ ] **Step 1.1: Write failing test**

Vytvořit `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Širokoúhlý layout Fáze 1+2 (2026-04-20): 2-tier width policy.
/// Default 1280px (reading/form) vs fluid (data-heavy) s clamp padding.
/// Content-driven inner constraints řešeno per-komponenta, ne globally.
/// Viz docs/superpowers/specs/2026-04-20-sirokouhly-layout-design.md.
/// </summary>
public sealed class LayoutWidthPolicyTests
{
    [Fact]
    public void SiteCss_DefaultAppMain_ShouldKeep1280pxMaxWidth()
    {
        // Regression guard — default tier (reading/form) zachová 1280px cap.
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main\s*\{[^}]*max-width\s*:\s*1280px",
            "default .app-main si drží 1280px max-width pro text/form views");
    }

    [Fact]
    public void SiteCss_AppMainFluid_ShouldUseClampPadding()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main--fluid\s*\{[\s\S]*?padding\s*:\s*0\s+clamp\(\s*16px\s*,\s*2vw\s*,\s*48px\s*\)",
            "fluid tier má clamp(16px, 2vw, 48px) horizontal padding (škálování FHD → 4K)");
    }

    [Fact]
    public void SiteCss_AppMainFluid_ShouldRemoveMaxWidthCap()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main--fluid\s*\{[\s\S]*?max-width\s*:\s*none",
            "fluid tier odstraňuje max-width cap pro 4K využití");
    }

    [Fact]
    public void SiteCss_AppMainFluid_ShouldResetDefaultMargin()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.app-main--fluid\s*\{[\s\S]*?margin\s*:\s*0",
            "fluid tier resetuje margin (default 24px auto 48px by zbytečně odsazoval)");
    }

    [Fact]
    public void Layout_BodyClassSwitch_ShouldSupportFluidTier()
    {
        var layout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("dashboard-page",
            "Layout switch rozpoznává BodyClass 'dashboard-page' → fluid tier");
        layout.Should().Contain("app-main--fluid",
            "switch přikládá třídu app-main--fluid k main elementu");
    }
}
```

- [ ] **Step 1.2: Run test — expect FAIL**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~LayoutWidthPolicyTests" 2>&1 | tail -8
```
Expected: `Failed: 2-3` (testy na `clamp`, `margin: 0`, case `max-width: none` nejsou splněné).

Pozn.: `SiteCss_DefaultAppMain_ShouldKeep1280pxMaxWidth` by měl projít už nyní (regression guard). `Layout_BodyClassSwitch_ShouldSupportFluidTier` projde taky (Úprava #4 už switch má).

- [ ] **Step 1.3: Update `.app-main--fluid` CSS rule**

V `PmTracker.Web/wwwroot/css/site.css` najít **existující** `.app-main--fluid` block (přidaný v Úpravě #4, commit `3c6961f`). Pokud existuje, nahradit za nový. Pokud existuje pouze jako one-liner `max-width: none; padding: 0;`, rozšířit.

Najít blok (pravděpodobně cca řádek 795-810, za `.app-main { ... }`):

```css
.app-main--fluid {
    max-width: none;
    padding: 0;
}
```

Nahradit:

```css
/* Širokoúhlý layout (2026-04-20): fluid tier pro data-heavy views.
   clamp padding: FHD 1920 → ~38px, 4K 3840 → 48px (cap),
   tablet <768 → 16px (minimum). Content uvnitř si řídí vlastní
   constraints (reading-block max-width: 72ch, form-panel 900px,
   tabulky 100%, card grids auto-fill). Viz spec:
   docs/superpowers/specs/2026-04-20-sirokouhly-layout-design.md */
.app-main--fluid {
    max-width: none;
    margin: 0;
    padding: 0 clamp(16px, 2vw, 48px);
}
```

Pokud `.app-main--fluid` ještě neexistuje (nečekaný stav), přidat za default `.app-main` block (cca za řádek 795).

- [ ] **Step 1.4: Run tests — expect PASS**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~LayoutWidthPolicyTests" 2>&1 | tail -5
```
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 1.5: Full test suite**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors. Tests 550 → **555** (5 nových tests).

- [ ] **Step 1.6: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): clamp padding pro .app-main--fluid (širokoúhlý Fáze 1 infra)

Fluid tier dostává horizontal padding clamp(16px, 2vw, 48px) pro rozumné
škálování napříč monitor rozlišeními:
- Tablet ~768px: 16px padding
- FHD 1920px: ~38px padding
- 4K 3840px: 48px cap (comfortable breathing room)

Margin reset 0 (default 24px auto 48px by zbytečně odsazoval při fluid).
max-width: none zachováno z Úpravy #4.

5 architecture tests LayoutWidthPolicyTests — regression guard pro
default tier + fluid tier pravidla.

Spec: docs/superpowers/specs/2026-04-20-sirokouhly-layout-design.md"
```

---

## Task 2: Projekty/Detail — BodyClass "dashboard-page"

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/Detail.cshtml:1-20` (Razor header block)
- Modify: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` (přidat per-view test)

- [ ] **Step 2.1: Write failing test**

Do `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` přidat:

```csharp
[Fact]
public void ProjektyDetail_ShouldUseFluidLayout()
{
    var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/Detail.cshtml"));
    view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Projekty/Detail je data-heavy (5 tabs, karty, grid) → fluid tier");
}
```

- [ ] **Step 2.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ProjektyDetail_ShouldUseFluidLayout" 2>&1 | tail -5
```

- [ ] **Step 2.3: Update view**

V `PmTracker.Web/Views/Projekty/Detail.cshtml` najít header Razor block (řádek 2-18, začíná `@{`). Přidat řádek **za** `ViewData["Title"] = ...`:

```razor
@model ProjektDetailViewModel
@{
    ViewData["Title"] = $"Projekt {Model.Projekt.Zkratka}";
    ViewData["BodyClass"] = "dashboard-page";
    var asUser = Context.Request.Query["asUser"].ToString();
    // ... zbytek existujícího kódu beze změny
```

- [ ] **Step 2.4: Run test — expect PASS**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ProjektyDetail" 2>&1 | tail -5
```

- [ ] **Step 2.5: Full suite + commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: 555 → 556.

```bash
git add PmTracker.Web/Views/Projekty/Detail.cshtml \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): Projekty/Detail na fluid tier (širokoúhlý Fáze 1)

Přidán ViewData['BodyClass'] = 'dashboard-page'. Detail s 5 tabs
(Záznamy, Harmonogram, Jednání, Tým, Návrhy) získá plnou šířku monitoru.
Tabs row + tab-panely roztáhnou přirozeně, Harmonogram/Gantt nejvíc."
```

---

## Task 3: Projekty/Index — BodyClass "dashboard-page"

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/Index.cshtml:1-8` (Razor header)
- Modify: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` (+1 test)

- [ ] **Step 3.1: Write failing test**

Do `LayoutWidthPolicyTests.cs`:

```csharp
[Fact]
public void ProjektyIndex_ShouldUseFluidLayout()
{
    var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/Index.cshtml"));
    view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Projekty/Index seznam karet → fluid tier (víc sloupců na 4K)");
}
```

- [ ] **Step 3.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ProjektyIndex_ShouldUseFluidLayout" 2>&1 | tail -5
```

- [ ] **Step 3.3: Update view**

V `PmTracker.Web/Views/Projekty/Index.cshtml` header (řádky 1-8):

```razor
@model ProjektyIndexViewModel
@{
    ViewData["Title"] = "Projekty";
    ViewData["BodyClass"] = "dashboard-page";
    var canHideDone = Model.StavyProjektu.Any(x => string.Equals(x.Value, "DONE", StringComparison.OrdinalIgnoreCase));
    var canHideDeleted = Model.StavyProjektu.Any(x => string.Equals(x.Value, "DELETED", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 3.4: Run test — expect PASS**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ProjektyIndex_ShouldUseFluidLayout" 2>&1 | tail -5
```

- [ ] **Step 3.5: Commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: 556 → 557.

```bash
git add PmTracker.Web/Views/Projekty/Index.cshtml \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): Projekty/Index na fluid tier (širokoúhlý Fáze 1)

Card grid .project-list-shell s repeat(auto-fill, minmax(...)) → na
širších monitorech přirozeně více sloupců (lepší scannable přehled)."
```

---

## Task 4: ProjectDashboard/Index — BodyClass + redundance audit

**Files:**
- Modify: `PmTracker.Web/Views/ProjectDashboard/Index.cshtml:1-10` (header)
- Modify: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` (+1 test)
- Potenciál: `PmTracker.Web/wwwroot/css/site.css` (pokud `.dashboard-fullwidth` rules konfliktují s novým paddingem)

- [ ] **Step 4.1: Write failing test**

Do `LayoutWidthPolicyTests.cs`:

```csharp
[Fact]
public void ProjectDashboardIndex_ShouldUseFluidLayout()
{
    var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/ProjectDashboard/Index.cshtml"));
    view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "ProjectDashboard panely (zaznamy, NES, výzvy, stats) → fluid tier");
}
```

- [ ] **Step 4.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ProjectDashboardIndex" 2>&1 | tail -5
```

- [ ] **Step 4.3: Audit existing `.dashboard-fullwidth` class**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -nE "\.dashboard-fullwidth" PmTracker.Web/wwwroot/css/site.css
```

Pokud class existuje a obsahuje `max-width: none` + padding rules, **zjistit zda duplikuje `.app-main--fluid`**. Pokud ano, ponechat beze změny (je to wrapper uvnitř hlavního containeru, neškodí). Pokud class nastavuje padding který kolyduje s clamp paddingem, poznamenat do commit message jako follow-up cleanup.

- [ ] **Step 4.4: Update view**

V `PmTracker.Web/Views/ProjectDashboard/Index.cshtml` header (řádky 1-10):

```razor
@model ProjectDashboardPageViewModel
@{
    ViewData["Title"] = $"Dashboard – {Model.Projekt.Zkratka}";
    ViewData["BodyClass"] = "dashboard-page";
}
```

Ponechat existující `<section class="dashboard-fullwidth" ...>` element beze změny (interní wrapper je OK).

- [ ] **Step 4.5: Run test — expect PASS + commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: 557 → 558.

```bash
git add PmTracker.Web/Views/ProjectDashboard/Index.cshtml \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): ProjectDashboard/Index na fluid tier (širokoúhlý Fáze 1)

Tab dashboard projektu (záznamy, NES, výzvy, stats). Existující
.dashboard-fullwidth wrapper uvnitř markup ponechán — interní
content constraint, neškodí novému .app-main--fluid outer paddingu."
```

---

## Task 5: Jednani/Detail — BodyClass

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/Detail.cshtml:1-30` (header)
- Modify: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` (+1 test)

- [ ] **Step 5.1: Write failing test**

```csharp
[Fact]
public void JednaniDetail_ShouldUseFluidLayout()
{
    var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Jednani/Detail.cshtml"));
    view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Jednani/Detail (zápis + účastníci + úkoly + attendance) → fluid tier");
}
```

- [ ] **Step 5.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~JednaniDetail_ShouldUseFluidLayout" 2>&1 | tail -5
```

- [ ] **Step 5.3: Update view**

V `PmTracker.Web/Views/Jednani/Detail.cshtml` header (cca řádek 1-30 `@{` block). Najít první řádek v @{ } bloku a přidat:

```razor
@model JednaniDetailViewModel
@{
    ViewData["Title"] = "Detail jednání";
    ViewData["BodyClass"] = "dashboard-page";
    var uzavrenyStav = Model.StavyJednani.FirstOrDefault(...);
    // ... zbytek existujícího kódu
```

- [ ] **Step 5.4: Run + commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: 558 → 559.

```bash
git add PmTracker.Web/Views/Jednani/Detail.cshtml \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): Jednani/Detail na fluid tier (širokoúhlý Fáze 2)

Zápis + účastníci + attendance summary + úkoly panel benefitnou
z větší šířky — na FHD+ monitorech mohou být potenciálně side-by-side
(dnes stackují, po Fáze 2 review rozhodnout zda přeuspořádat markup)."
```

---

## Task 6: Search/Index — BodyClass

**Files:**
- Modify: `PmTracker.Web/Views/Search/Index.cshtml:1-10` (header)
- Modify: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` (+1 test)

- [ ] **Step 6.1: Write failing test**

```csharp
[Fact]
public void SearchIndex_ShouldUseFluidLayout()
{
    var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Search/Index.cshtml"));
    view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Search/Index hit list napříč entitami → fluid tier");
}
```

- [ ] **Step 6.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~SearchIndex_ShouldUseFluidLayout" 2>&1 | tail -5
```

- [ ] **Step 6.3: Update view**

V `PmTracker.Web/Views/Search/Index.cshtml` header (cca řádek 1-10):

```razor
@model PmTracker.Web.Services.Search.GlobalSearchPageViewModel
@using PmTracker.Web.Services.Search
@{
    ViewData["Title"] = string.IsNullOrWhiteSpace(Model.Query) ? "Vyhledávání" : $"Vyhledávání: {Model.Query}";
    ViewData["BodyClass"] = "dashboard-page";

    string TypeLabel(string t) => t switch
    // ... zbytek existujícího kódu
```

- [ ] **Step 6.4: Run + commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: 559 → 560.

```bash
git add PmTracker.Web/Views/Search/Index.cshtml \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): Search/Index na fluid tier (širokoúhlý Fáze 2)

Hit list multi-type výsledků (projekty, záznamy, jednání, osoby,
subsystémy, vyjádření, návrhy) — širší tabulka → víc metadata
v každém řádku bez ellipsis."
```

---

## Task 7: Dashboard/Focus — BodyClass

**Files:**
- Modify: `PmTracker.Web/Views/Dashboard/Focus.cshtml:1-10` (header)
- Modify: `PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs` (+1 test)

- [ ] **Step 7.1: Write failing test**

```csharp
[Fact]
public void DashboardFocus_ShouldUseFluidLayout()
{
    var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/Focus.cshtml"));
    view.Should().Contain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Dashboard/Focus full list záznamů → fluid tier");
}

[Fact]
public void DashboardMeetingsAndNews_ShouldStayNarrow()
{
    // Úmyslně ponecháno narrow pro reading UX newsfeed / chronological feed.
    // YAGNI override — pokud později user bude chtít wide, změníme tehdy.
    var meetings = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/Meetings.cshtml"));
    var news = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/News.cshtml"));

    meetings.Should().NotContain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Dashboard/Meetings je chronological feed — reading UX, ne data grid");
    news.Should().NotContain("ViewData[\"BodyClass\"] = \"dashboard-page\"",
        "Dashboard/News je newsfeed — reading UX");
}
```

- [ ] **Step 7.2: Run tests — expect FAIL na Focus, PASS na Meetings/News guard**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~DashboardFocus|FullyQualifiedName~DashboardMeetingsAndNews" 2>&1 | tail -5
```

- [ ] **Step 7.3: Update Dashboard/Focus view**

V `PmTracker.Web/Views/Dashboard/Focus.cshtml` header (cca řádek 1-5):

```razor
@model DashboardFocusListPageViewModel
@{
    ViewData["Title"] = Model.PageTitle;
    ViewData["BodyClass"] = "dashboard-page";
}
```

- [ ] **Step 7.4: Run + commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: 560 → 562 (2 nové tests).

```bash
git add PmTracker.Web/Views/Dashboard/Focus.cshtml \
        PmTracker.Tests.Unit/Layout/LayoutWidthPolicyTests.cs
git commit -m "feat(layout): Dashboard/Focus na fluid tier (širokoúhlý Fáze 2)

Full list záznamů ze 'Zobrazit více' — výhoda z víc sloupců.
Dashboard/Meetings + News záměrně ponechány narrow (reading UX).

Architecture test guard: Meetings/News NESMĚJÍ na fluid přejít
bez explicitního rozhodnutí (regression guard)."
```

---

## Task 8: Final validation + publish refresh

**Files:** none (validation only)

- [ ] **Step 8.1: Full test suite**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors, Tests **562 passed**.

- [ ] **Step 8.2: Verify commits chain**

```bash
git log --oneline -10
```
Expected: 7 nových commits od Task 1 (infra) po Task 7 (Dashboard/Focus).

- [ ] **Step 8.3: Playwright static smoke (sandbox — SQL-unreachable)**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/wwwroot"
python3 -m http.server 8766 > /tmp/static.log 2>&1 &
sleep 2
```

Vytvořit `/tmp/playwright-layout-smoke.js`:

```javascript
const { chromium } = require('playwright');

const tests = [
  { name: 'FHD 1920x1080', viewport: { width: 1920, height: 1080 } },
  { name: '4K 3840x2160', viewport: { width: 3840, height: 2160 } },
  { name: 'Tablet 768x1024', viewport: { width: 768, height: 1024 } },
];

// Test static harness existuje a pm-modal-harness.html je zelený
// (verification že JS bundle + CSS se loadují bez errorů na různých viewportech)
(async () => {
  const browser = await chromium.launch({ headless: true });
  for (const t of tests) {
    const ctx = await browser.newContext({ viewport: t.viewport });
    const page = await ctx.newPage();
    await page.goto('http://127.0.0.1:8766/pm-modal-harness.html', { waitUntil: 'networkidle' });
    await page.screenshot({ path: `/tmp/layout-smoke-${t.name.replace(/[^a-z0-9]/gi,'-')}.png`, fullPage: false });
    console.log(`📸 ${t.name}: OK`);
    await ctx.close();
  }
  await browser.close();
})();
```

Run:
```bash
cd /Users/Pavel.Andrlik/.claude/plugins/cache/playwright-skill/playwright-skill/4.1.0/skills/playwright-skill
node run.js /tmp/playwright-layout-smoke.js 2>&1 | tail -10
```

Kill server:
```bash
pkill -f "http.server 8766"
```

- [ ] **Step 8.4: Publish refresh**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
rm -rf publish publish.zip
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o publish --nologo 2>&1 | tail -3
cd publish && zip -rq ../publish.zip . && cd ..
ls -la publish.zip | awk '{print $5, "bytes"}'
```

- [ ] **Step 8.5: Final summary commit (optional — pokud dává smysl sloučit validation do předchozího)**

Pokud všechno green, žádný další commit není potřeba (7 commits po Task 1–7 + 1 docs commit (spec) = 8 commitů celkem pro Fáze 1+2).

**Manual Citrix smoke checklist (user, mimo sandbox):**
- 1920×1080 — Projekty/Detail, Projekty/Index, ProjectDashboard, Jednani/Detail, Search, Dashboard/Focus
- 3840×2160 (4K) — stejné views. Ověřit: full screen využit, card grid má více sloupců, tabulky plně roztaženy.
- 1366×768 (HD) — responsive fallback drží layout (žádný horizontal scroll).

---

## Self-Review

**Spec coverage:**
- ✅ Task 1 — infrastructure (.app-main--fluid clamp padding + margin reset + max-width none)
- ✅ Task 2 — Projekty/Detail (Fáze 1)
- ✅ Task 3 — Projekty/Index (Fáze 1)
- ✅ Task 4 — ProjectDashboard/Index (Fáze 1)
- ✅ Task 5 — Jednani/Detail (Fáze 2)
- ✅ Task 6 — Search/Index (Fáze 2)
- ✅ Task 7 — Dashboard/Focus (Fáze 2) + guard test pro Meetings/News narrow
- ✅ Task 8 — Final validation + publish

**Placeholder scan:** žádné "TBD", "TODO", "similar to". Všechny tasks mají konkrétní code snippets + exact paths + commands.

**Type consistency:**
- `ViewData["BodyClass"] = "dashboard-page"` — identický string napříč 6 views
- `.app-main--fluid` class name konzistentně použita
- Test class `LayoutWidthPolicyTests` + namespace `PmTracker.Tests.Unit.Layout` konzistentně

**Exit criteria:**
- 6 views přepnuty na fluid ✅
- `.app-main--fluid` má clamp padding ✅
- Tests 550 → 562 (+12 architecture tests) ✅
- Build 0 errors ✅
- Žádné route/URL/API change ✅
- Publish refresh ✅

**Scope boundary:**
- Fáze 3+ (Osoby, Nastaveni, Ciselniky, EditZaznamPage) NEzahrnuto — budoucí iterace
- Harmonogram tab special-case NEzahrnuto — fixneme pokud po Fáze 1 review vynikne problém
- User-preference toggle NEzahrnuto — YAGNI
