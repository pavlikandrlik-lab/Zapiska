# C1 — Breadcrumb ← vrací na místo původu (implementační plán)

> **For agentic workers:** Inline exekuce (executing-plans) v hlavní session — bez subagentů (pravidlo uživatele). Kroky checkbox syntaxí.

**Goal:** Šipka ← v breadcrumbs vede na origin (`returnUrl`) nebo kanonickou záložku entity; klik na projekt-drobeček zůstává homepage projektu (Záznamy).

**Architecture:** `BreadcrumbTrail` dostane explicitní `BackUrl` (přednost před URL předposledního drobečku). `SetProjectBreadcrumbs` přijme raw `backUrl` kandidáta, znormalizuje (`Url.IsLocalUrl`) a doplní kanonický fallback `tab=jednani`, když je aktuální drobeček jednání. Editor/návrhové stránky už origin-aware `model.BackUrl` počítají (`PrepareRecordEditorModel` / `PrepareProposalEditorModel` — returnUrl ?? tab=zaznamy/navrhy/meeting-detail) — jen se propojí. Nová origin místa: přehled `/Jednani` a cross-nav z Harmonogramu.

**Tech Stack:** ASP.NET Core MVC, Razor, xUnit, Playwright (.NET E2E).

## Global Constraints

- Commity DRŽET — uživatel finálně ověřuje ručně.
- `returnUrl` výhradně přes `Url.IsLocalUrl` (open-redirect); nevalidní → kanonický fallback. Žádný referer.
- Klik na projekt-drobeček beze změny: `/Projekty/Detail/{id}` bez tab (homepage = Záznamy).
- Tab klíče: `zaznamy`, `harmonogram`, `jednani`, `tym`, `navrhy` (ProjektyController konstanty; `NormalizeProjectTab` fallback = zaznamy).
- Api testy s diakritikou: assertovat přes `WebUtility.HtmlDecode` nebo regex tolerantní k `&amp;`.

---

### Task 1: BreadcrumbTrail.BackUrl

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Breadcrumbs.cs`
- Test: `PmTracker.Tests.Unit/Layout/BreadcrumbTrailTests.cs`

**Interfaces:**
- Produces: `BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items, string? BackUrl = null)`; `ParentUrl => BackUrl ?? Items[^2].Url`.

- [ ] **Step 1: Failing testy**

```csharp
[Fact]
public void ParentUrl_PrefersExplicitBackUrl()
{
    var trail = new BreadcrumbTrail(
        new[] { new Breadcrumb("Projekty", "/Projekty", null, false), new Breadcrumb("Jednání č. 1", null, null, true) },
        "/Projekty/Detail/1?tab=jednani");
    trail.ParentUrl.Should().Be("/Projekty/Detail/1?tab=jednani");
}

[Fact]
public void ParentUrl_FallsBackToSecondToLastItem_WhenBackUrlNull()
{
    var trail = new BreadcrumbTrail(
        new[] { new Breadcrumb("Projekty", "/Projekty", null, false), new Breadcrumb("X", null, null, true) });
    trail.ParentUrl.Should().Be("/Projekty");
}
```

- [ ] **Step 2: Run → první FAIL (konstruktor bez BackUrl parametru = compile error → to je očekávaný RED)**

- [ ] **Step 3: Implementace**

```csharp
/// <summary>Uspořádaná drobečková cesta. Rodič libovolného drobečku = předchozí drobeček.</summary>
public sealed record BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items, string? BackUrl = null)
{
    /// <summary>Cíl šipky ←: explicitní BackUrl (origin/kanonická záložka entity),
    /// jinak URL předposledního drobečku; null když je jen kořen.</summary>
    public string? ParentUrl => BackUrl ?? (Items.Count >= 2 ? Items[Items.Count - 2].Url : null);
}
```

`_BreadcrumbBar.cshtml` beze změny (čte `ParentUrl`).

- [ ] **Step 4: Unit Layout testy zelené** (`--filter "FullyQualifiedName~BreadcrumbTrail"` + celé Layout)

### Task 2: BaseController — backUrl plumbing + DRY NormalizeLocalReturnUrl

**Files:**
- Modify: `PmTracker.Web/Controllers/BaseController.Breadcrumbs.cs`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Commands.cs`, `PmTracker.Web/Controllers/ZaznamyController.cs` (smazat lokální helper, používat zděděný)
- Modify: `PmTracker.Web/Controllers/NavrhyController.cs` (dtto)

**Interfaces:**
- Produces: `SetProjectBreadcrumbs(..., string? backUrl = null)`; `protected string? NormalizeLocalReturnUrl(string?)`; `protected string ProjektDetailTabUrl(int id, string tab)`.

- [ ] **Step 1: Přesun helperu do BaseController** (identická sémantika — ověřit diff obou lokálních kopií, pak smazat obě):

```csharp
/// <summary>Origin kandidát pro navigaci zpět: jen lokální URL (open-redirect ochrana).</summary>
protected string? NormalizeLocalReturnUrl(string? returnUrl)
    => !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
```

- [ ] **Step 2: SetProjectBreadcrumbs — backUrl parametr + kanonický jednani fallback**

```csharp
protected void SetProjectBreadcrumbs(
    int projektId,
    string projektNazev,
    string projektZkratka,
    (int Id, string Label)? meeting = null,
    string? currentText = null,
    string? backUrl = null)
{
    // ...stávající stavba items beze změny...

    // C1 (2026-07-10): ← = origin (validovaný returnUrl) > kanonická záložka entity
    // (jednání, když je meeting aktuální drobeček) > URL předposledního drobečku.
    // Projekt-drobeček zůstává bez tab (homepage = Záznamy).
    var resolvedBack = NormalizeLocalReturnUrl(backUrl)
        ?? (meeting is not null && currentText is null
            ? ProjektDetailTabUrl(projektId, "jednani")
            : null);

    SetBreadcrumbs(resolvedBack, items.ToArray());
}

protected string ProjektDetailTabUrl(int id, string tab)
    => Url.Action("Detail", "Projekty", WithAsUser(new { id, tab })) ?? $"/Projekty/Detail/{id}?tab={tab}";

protected void SetBreadcrumbs(params Breadcrumb[] items) => SetBreadcrumbs(null, items);

protected void SetBreadcrumbs(string? backUrl, params Breadcrumb[] items)
    => ViewData["Breadcrumbs"] = new BreadcrumbTrail(items, backUrl);
```

- [ ] **Step 3: `dotnet build` čistý; Unit + Api Breadcrumb* testy zelené (chování bez backUrl beze změny)**

### Task 3: JednaniController.Detail — origin + kanonická záložka, úklid reliktu

**Files:**
- Modify: `PmTracker.Web/Controllers/JednaniController.cs` (Detail, ~ř. 60–90)
- Modify: `PmTracker.Web/Models/ViewModels/JednaniViewModels.cs` (jen pokud grep potvrdí, že `BackUrl`/`BackLabel` nikdo nečte)
- Test: `PmTracker.Tests.Api/Controllers/BreadcrumbBackNavigationRenderTests.cs` (nový)

- [ ] **Step 1: Failing Api render testy (nová třída, ApiSqlFixture + EnsureMeetingAsync)**

```csharp
[Fact]
public async Task MeetingDetail_BackArrow_TargetsProjectMeetingsTab()
{
    var html = await GetAsync($"/Jednani/Detail/{MeetingId}?asUser={Fixture.AdminOsobaId}");
    var back = Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value;
    WebUtility.HtmlDecode(back).Should().Contain("tab=jednani");
}

[Fact]
public async Task MeetingDetail_BackArrow_HonorsLocalReturnUrl()
{
    var html = await GetAsync($"/Jednani/Detail/{MeetingId}?returnUrl=%2FJednani&asUser={Fixture.AdminOsobaId}");
    var back = Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value;
    WebUtility.HtmlDecode(back).Should().Be("/Jednani");
}

[Fact]
public async Task MeetingDetail_BackArrow_RejectsExternalReturnUrl()
{
    var html = await GetAsync($"/Jednani/Detail/{MeetingId}?returnUrl=https%3A%2F%2Fevil.example&asUser={Fixture.AdminOsobaId}");
    WebUtility.HtmlDecode(Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value)
        .Should().Contain("tab=jednani");
}

[Fact]
public async Task MeetingDetail_ProjectCrumbLink_StaysWithoutTab()
{
    var html = await GetAsync($"/Jednani/Detail/{MeetingId}?asUser={Fixture.AdminOsobaId}");
    var projectLink = Regex.Match(html, "app-breadcrumb-link\" href=\"([^\"]*Projekty%2FDetail[^\"]*|[^\"]*Projekty/Detail[^\"]*)\"").Groups[1].Value;
    WebUtility.HtmlDecode(projectLink).Should().NotContain("tab=");
}
```

- [ ] **Step 2: Run → FAIL (back href dnes bez tab)**

- [ ] **Step 3: Implementace v Detail**

```csharp
SetProjectBreadcrumbs(
    model.ProjektId,
    model.ProjektNazev,
    model.ProjektZkratka,
    meeting: (model.Jednani.Id, model.PageTitle),
    backUrl: returnUrl);
```

Smazat blok `fallbackUrl / isValidReturnUrl / model.BackUrl / model.BackLabel`
POUZE pokud `grep -rn "BackUrl\|BackLabel"` přes Views/Jednani + testy potvrdí,
že je pro jednání nikdo nečte (view Detail.cshtml je nerenderuje — ověřeno
2026-07-10). Pokud čte, ponechat a jen doplnit breadcrumb volání.

- [ ] **Step 4: Testy zelené (nová třída + existující Breadcrumb* Api testy)**

### Task 4: Zaznamy + Navrhy — propojit existující model.BackUrl

**Files:**
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs` (Edit ~ř. 87, Create ~ř. 113 — volání SetProjectBreadcrumbs až PO PrepareRecordEditorModel)
- Modify: `PmTracker.Web/Controllers/NavrhyController.cs` (4 akce: CreateRecordProposal, CreateScheduleProposal, ProposalDetail, PrefillCreateProposal)
- Test: rozšířit `BreadcrumbBackNavigationRenderTests`

- [ ] **Step 1: Failing testy**

```csharp
[Fact]
public async Task ProposalDetail_BackArrow_TargetsProposalsTab()
{
    var html = await GetAsync($"/Navrhy/ProposalDetail?projektId={Fixture.ProjectId}&proposalId={ProposalId}&asUser={Fixture.AdminOsobaId}");
    WebUtility.HtmlDecode(Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value)
        .Should().Contain("tab=navrhy");
}

[Fact]
public async Task RecordEdit_BackArrow_TargetsRecordsTab()
{
    var html = await GetAsync($"/Zaznamy/Edit/{RecordId}?projektId={Fixture.ProjectId}&asUser={Fixture.AdminOsobaId}");
    WebUtility.HtmlDecode(Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value)
        .Should().Contain("tab=zaznamy");
}

[Fact]
public async Task RecordEdit_BackArrow_HonorsHarmonogramReturnUrl()
{
    var ret = Uri.EscapeDataString($"/Projekty/Detail/{Fixture.ProjectId}?tab=harmonogram");
    var html = await GetAsync($"/Zaznamy/Edit/{RecordId}?projektId={Fixture.ProjectId}&returnUrl={ret}&asUser={Fixture.AdminOsobaId}");
    WebUtility.HtmlDecode(Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value)
        .Should().Contain("tab=harmonogram");
}
```

- [ ] **Step 2: Run → FAIL**

- [ ] **Step 3: Implementace — v každé z 6 akcí doplnit `backUrl: model.BackUrl`**

`model.BackUrl` je už origin-aware (returnUrl ?? kanonický fallback:
Zaznamy → `tab=zaznamy` / meeting-detail; Navrhy → `tab=navrhy`), a lokální
(normalizace uvnitř Prepare*). Příklad (ZaznamyController.Edit):

```csharp
SetProjectBreadcrumbs(
    model.ProjektId, model.ProjektNazev, model.ProjektZkratka,
    currentText: model.IsCreate ? "Nový záznam" : $"Záznam #{model.Id}",
    backUrl: model.BackUrl);
```

- [ ] **Step 4: Testy zelené**

### Task 5: Nová origin místa — /Jednani přehled a Harmonogram cross-nav

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml` (ř. 23; partial renderuje JEN Jednani/Index — ověřeno grep 2026-07-10)
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (ř. ~271, ScheduleEditUrl)

- [ ] **Step 1: _MeetingYearGroup — detail odkaz nese aktuální URL přehledu**

```razor
@{
    // C1 (2026-07-10): origin pro breadcrumb ← na detailu (přehled /Jednani vč. filtrů).
    var meetingOverviewReturnUrl = Context.Request.Path + Context.Request.QueryString;
}
var detailUrl = Url.Action("Detail", new { id = jednani.Id, returnUrl = meetingOverviewReturnUrl })
    ?? $"/Jednani/Detail/{jednani.Id}?returnUrl={Uri.EscapeDataString(meetingOverviewReturnUrl)}";
```

(Deklaraci proměnné umístit k ostatním `@{ }` na začátku partialu, ne do smyčky.)

- [ ] **Step 2: ScheduleEditUrl — editor záznamu z gantu se vrací na harmonogram**

```csharp
item.ScheduleEditUrl = Url.Action("Edit", "Zaznamy", new
{
    id = item.ZaznamId,
    projektId = model.ProjektId,
    // C1 (2026-07-10): cross-nav origin — ← v editoru vrací na záložku Harmonogram.
    returnUrl = Url.Action("Detail", "Projekty", new { id = model.ProjektId, tab = ScheduleTab })
}) ?? $"/Zaznamy/Edit/{item.ZaznamId}";
```

- [ ] **Step 3: `dotnet build` + curl ověření obou URL v renderu**

Run: `curl -s "http://localhost:5071/Jednani?asUser=1" | grep -o "Detail/[0-9]*?returnUrl=[^\"]*" | head -3`
Expected: returnUrl=%2FJednani…

### Task 6: E2E scénáře + živé ověření + regrese

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/BreadcrumbBackOriginScenariosTests.cs`

- [ ] **Step 1: E2E fakta** (vzor RecordEditorHistoryBackScenariosTests; URL asserty přes `Assertions.Expect(page).ToHaveURLAsync(new Regex(...))` — Playwright .NET gotchas)

```csharp
// (a) /Jednani → detail → ← → zpět na /Jednani
// (b) projekt tab=jednani → detail jednání → ← → URL obsahuje tab=jednani
// (c) projekt tab=navrhy → detail návrhu → ← → URL obsahuje tab=navrhy
```

- [ ] **Step 2: E2E třída zelená**

- [ ] **Step 3: Živý Playwright průchod všech cest (vč. news → detail jednání → ← → /dashboard/news) + screenshoty**

- [ ] **Step 4: Regrese: Unit Layout (Breadcrumb*), Api (celé Controllers), E2E breadcrumb+editor třídy. Známé pre-existing výjimky: 4 Api gantt, 1 E2E SkipFilterPrompt.**

### Task 7: Držený commit

- [ ] Commit message: `feat(breadcrumbs): C1 — ← vrací na origin/kanonickou záložku (returnUrl mechanismus)`
