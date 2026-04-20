# Ranní systémová oprava — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementovat 8 úprav ze specu `2026-04-20-ranni-fix-design.md` napříč 3 sekcemi (Jednani UX, Dashboard, Modal systém) bez regresí, zero URL/API breakage.

**Architecture:** Partial class patterny + architecture tests (konzistentní s 3A-3D). Sekce A jednoduché view tweaks. Sekce C cross-cutting JS infrastructure fix (modal lifecycle + portal reparenting). Sekce B kombinuje CSS Grid layout + ViewModel rozšíření + icon button migraci. Každá úprava svůj commit.

**Tech Stack:** .NET 8, ASP.NET Core MVC, Razor, ES modules, gov-design-system 4.2.9, xUnit + FluentAssertions.

**Spec ref:** [docs/superpowers/specs/2026-04-20-ranni-fix-design.md](../specs/2026-04-20-ranni-fix-design.md)

---

## File Structure

**New files:**
- `PmTracker.Tests.Unit/Layout/DashboardLayoutTests.cs` — architecture tests pro #4
- `PmTracker.Tests.Unit/Layout/ModalLayoutRulesTests.cs` — architecture tests pro #9/2 overflow policy
- `PmTracker.Tests.Unit/Layout/DashboardActionButtonsTests.cs` — architecture tests pro #6 icons
- `docs/specs/modal-layout-rules.md` — nová specifikace modal overflow policy

**Modified files:**
- Views: `_ProjectMeetingsTab.cshtml`, `Jednani/Index.cshtml`, `Dashboard/Index.cshtml`, `_DashboardNewsPanel.cshtml`, `_DashboardFocusPanel.cshtml`, `_DashboardMeetingsPanel.cshtml`, `_ZaznamPartial.cshtml`, `_ZaznamCommentsPartial.cshtml`, `_ModalLayout.cshtml` (případně — pouze pokud overflow policy vyžaduje)
- JS: `meetingOverview.js`, `bootstrap.js`, `modals.js`, `dashboard.js`, `site.bundle.js`
- CSS: `site.css` (sekce dashboard-home, meeting-project-*, modal overflow, icon buttons)
- C#: `DashboardNewsItemViewModel`, `DashboardService.BuildNewsItemsAsync`
- Docs: `docs/specs/meetings-year-grouping.md`
- Tests: `MeetingsYearGroupingTests.cs`

---

## Task 1: A1 — Projekt-tab historical years → collapsed

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml`
- Modify: `PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs`
- Modify: `docs/specs/meetings-year-grouping.md`

- [ ] **Step 1.1: Update existující test pro collapsed**

V souboru `PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs` najít `ProjectJednaniTab_ShouldDefaultHistoricalYearsToOpen` a přejmenovat + upravit assertions:

```csharp
[Fact]
public void ProjectJednaniTab_ShouldDefaultHistoricalYearsToCollapsed()
{
    var source = LoadViewSource("PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml");

    source.Should().Contain(
        "data-meeting-history-default=\"collapsed\"",
        "projektová záložka Jednání nyní sbalené historické roky (konzistentní s aplikační záložkou)");

    source.Should().Contain(
        "isPreviewYear ? \"preview\" : \"collapsed\"",
        "projektová záložka musí nastavit historické roky do stavu collapsed");
}
```

- [ ] **Step 1.2: Run test — expect FAIL**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ProjectJednaniTab_ShouldDefaultHistoricalYearsToCollapsed" 2>&1 | tail -5
```
Expected: `Failed: 1` (assertion na `"collapsed"` selže).

- [ ] **Step 1.3: Update view — open → collapsed**

V `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml` najít všechny tři occurrences (`data-meeting-history-default="open"` wrapper atribut, `isPreviewYear ? "preview" : "open"` state ternární výraz, případně komentář o "rozbalené") a nahradit `"open"` za `"collapsed"`. Konkrétně:

```razor
@*
    Projektová záložka Jednání: historické roky jsou SBALENÉ (collapsed),
    konzistentně s aplikační záložkou /Jednani/Index. Uživatel si historické
    roky může rozkliknout year-chevronem. Viz docs/specs/meetings-year-grouping.md.
*@
<div class="meeting-year-stack"
     data-meeting-overview="year-grouped"
     data-meeting-history-default="collapsed"
     data-meeting-preview-year="@(Model.PreviewRok?.ToString() ?? string.Empty)">
    @foreach (var rok in Model.RocniSkupiny)
    {
        var isPreviewYear = Model.PreviewRok.HasValue && rok.Rok == Model.PreviewRok.Value;
        <section class="meeting-year-group"
                 data-meeting-year-group
                 data-meeting-year="@rok.Rok"
                 data-meeting-year-state="@(isPreviewYear ? "preview" : "collapsed")">
```

- [ ] **Step 1.4: Run test — expect PASS**

Run same command as Step 1.2. Expected: `Passed: 1`.

- [ ] **Step 1.5: Update spec doc**

V `docs/specs/meetings-year-grouping.md` najít sekci "Projektová záložka" a přepsat tabulku default stavů + markup příklad:

```markdown
### Projektová záložka `/Projekty/Detail/{id}?tab=jednani`

| Rok | Výchozí stav | Zobrazení |
| --- | --- | --- |
| **Aktuální rok** (PreviewRok) | `preview` | Body viditelný, pouze první řádek karet (stejné jako `/Jednani/Index`). |
| **Historické roky** | `collapsed` | Body je `hidden`, karty nejsou v DOMu viditelné. Uživatel musí rok ručně rozkliknout year-chevronem. |

**Důvod**: konzistence s aplikační záložkou — user 2026-04-20 si vyžádal sjednocené chování, aby na historické jednání bylo vždy nutné explicitní kliknutí (proti přehlcení dlouhou historií).

Markup:

```html
<div class="meeting-year-stack"
     data-meeting-overview="year-grouped"
     data-meeting-history-default="collapsed"
     data-meeting-preview-year="2026">
    <!-- aktuální rok state="preview", ostatní state="collapsed" -->
</div>
```
```

- [ ] **Step 1.6: Full test suite + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors. Tests `Passed: 523+`.

```bash
git add PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml \
        PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs \
        docs/specs/meetings-year-grouping.md
git commit -m "fix(jednani): projekt-tab historické roky defaultně collapsed (úprava #1)

Stejné chování jako /Jednani/Index. Důvod: konzistence + user 2026-04-20
explicitně vyžádal (přehlcení historií).

Test ProjectJednaniTab_ShouldDefaultHistoricalYearsToOpen přejmenovan
na ...Collapsed + assertion swap open → collapsed."
```

---

## Task 2: A2 — Globální tab project-level history toggle

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/Index.cshtml`
- Modify: `PmTracker.Web/wwwroot/js/modules/meetingOverview.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js`
- Modify: `PmTracker.Web/wwwroot/css/site.css`
- Modify: `PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs`
- Modify: `docs/specs/meetings-year-grouping.md`

- [ ] **Step 2.1: Write failing architecture test**

V `PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs` přidat:

```csharp
[Fact]
public void ApplicationJednaniIndex_ShouldWrapHistoricalYearsInProjectHistoryBody()
{
    // Úprava #2: historické roky na /Jednani/Index jsou defaultně skryté za
    // project-level toggle v hlavičce projektu. Markup: [data-project-history-body][hidden]
    // kolem historických year-groups; aktuální rok zůstává mimo wrapper.
    var source = LoadViewSource("PmTracker.Web/Views/Jednani/Index.cshtml");

    source.Should().Contain("data-project-history-toggle",
        "hlavička projekt-karty je klikatelný toggle");
    source.Should().Contain("data-project-history-body",
        "wrapper kolem historických year-groups");
    source.Should().Contain("role=\"button\"",
        "hlavička je role button pro accessibility");
    source.Should().MatchRegex(
        @"data-project-history-body[^>]*hidden",
        "wrapper historických roků je defaultně hidden");
}

[Fact]
public void ProjectHistoryToggleJs_ShouldExportAndHandleToggle()
{
    var js = LoadViewSource("PmTracker.Web/wwwroot/js/modules/meetingOverview.js");

    js.Should().Contain("export function toggleProjectHistory",
        "meetingOverview.js musí exportovat toggleProjectHistory");
    js.Should().Contain("[data-project-card]",
        "JS používá [data-project-card] selector pro scope");
    js.Should().Contain("[data-project-history-body]",
        "JS musí toggle-ovat [data-project-history-body]");

    var bootstrap = LoadViewSource("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
    bootstrap.Should().Contain("toggleProjectHistory",
        "bootstrap.js importuje + volá toggleProjectHistory");
    bootstrap.Should().Contain("[data-project-history-toggle]",
        "bootstrap.js deleguje click na [data-project-history-toggle]");
}

[Fact]
public void ProjectHistoryCss_ShouldStyleHeaderAsToggle()
{
    var css = LoadViewSource("PmTracker.Web/wwwroot/css/site.css");

    css.Should().Contain(".meeting-header[data-project-history-toggle]",
        "CSS stylizuje toggle-able header (pointer, hover)");
    css.Should().Contain(".meeting-project-chevron",
        "chevron má vlastní class pro swap name attribut");
}
```

- [ ] **Step 2.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ApplicationJednaniIndex_ShouldWrapHistoricalYearsInProjectHistoryBody" 2>&1 | tail -5
```
Expected: 3 new tests fail (markup/JS/CSS not yet implemented).

- [ ] **Step 2.3: Rewrite Index.cshtml s project-level toggle**

Nahradit obsah `PmTracker.Web/Views/Jednani/Index.cshtml`:

```razor
@model JednaniIndexViewModel
@{
    ViewData["Title"] = "Jednání";
}

@await Html.PartialAsync("_PageHeader", new PageHeaderViewModel
{
    Title = Model.PageTitle,
    Subtitle = "Přehled porad podle projektu.",
    BackUrl = Model.BackUrl,
    BackLabel = Model.BackLabel
})

@foreach (var projekt in Model.Projekty)
{
    var previewYearGroups = projekt.RocniSkupiny
        .Where(rok => projekt.PreviewRok.HasValue && rok.Rok == projekt.PreviewRok.Value)
        .ToList();
    var historicalYearGroups = projekt.RocniSkupiny
        .Where(rok => !projekt.PreviewRok.HasValue || rok.Rok != projekt.PreviewRok.Value)
        .ToList();
    var hasHistory = historicalYearGroups.Count > 0;
    var historyBodyId = $"project-history-{projekt.ProjektId}";

    <section class="card meeting-project-overview" data-project-card>
        <header class="meeting-header"
                @(hasHistory ? "role=\"button\"" : "")
                @(hasHistory ? "data-project-history-toggle" : "")
                @(hasHistory ? $"aria-controls=\"{historyBodyId}\"" : "")
                @(hasHistory ? "aria-expanded=\"false\"" : "")
                @(hasHistory ? "tabindex=\"0\"" : "")>
            <h2>@projekt.ProjektNazev</h2>
            @if (hasHistory)
            {
                <span class="meeting-project-history-count" aria-hidden="true">
                    +@historicalYearGroups.Count starších @(historicalYearGroups.Count == 1 ? "rok" : historicalYearGroups.Count < 5 ? "roky" : "roků")
                </span>
                <gov-icon class="meeting-project-chevron" name="chevron-down" type="components" aria-hidden="true"></gov-icon>
            }
        </header>

        @if (projekt.Jednani.Count == 0)
        {
            <p class="muted">Projekt zatím nemá evidovaná jednání.</p>
        }
        else
        {
            @* Aktuální rok — vždy viditelný *@
            @if (previewYearGroups.Count > 0)
            {
                <div class="meeting-year-stack"
                     data-meeting-overview="year-grouped"
                     data-meeting-history-default="collapsed"
                     data-meeting-preview-year="@(projekt.PreviewRok?.ToString() ?? string.Empty)">
                    @foreach (var rok in previewYearGroups)
                    {
                        @await Html.PartialAsync("_MeetingYearGroup", new MeetingYearGroupPartialViewModel
                        {
                            Rok = rok,
                            IsPreviewYear = true,
                            ProjektId = projekt.ProjektId,
                            CanDeleteMeetings = projekt.CanDeleteMeetings
                        })
                    }
                </div>
            }

            @* Historické roky — uvnitř toggleable wrapperu *@
            @if (hasHistory)
            {
                <div class="meeting-project-history-body"
                     id="@historyBodyId"
                     data-project-history-body
                     hidden>
                    <div class="meeting-year-stack"
                         data-meeting-overview="year-grouped"
                         data-meeting-history-default="collapsed"
                         data-meeting-preview-year="">
                        @foreach (var rok in historicalYearGroups)
                        {
                            @await Html.PartialAsync("_MeetingYearGroup", new MeetingYearGroupPartialViewModel
                            {
                                Rok = rok,
                                IsPreviewYear = false,
                                ProjektId = projekt.ProjektId,
                                CanDeleteMeetings = projekt.CanDeleteMeetings
                            })
                        }
                    </div>
                </div>
            }
        }
    </section>
}
```

- [ ] **Step 2.4: Create _MeetingYearGroup partial view + VM**

Nový soubor `PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml`:

```razor
@model MeetingYearGroupPartialViewModel

<section class="meeting-year-group"
         data-meeting-year-group
         data-meeting-year="@Model.Rok.Rok"
         data-meeting-year-state="@(Model.IsPreviewYear ? "preview" : "collapsed")">
    <button class="meeting-year-toggle"
            type="button"
            data-meeting-year-toggle
            aria-expanded="@(Model.IsPreviewYear ? "true" : "false")">
        <span class="meeting-year-toggle-main">
            <span class="meeting-year-title">@Model.Rok.Rok</span>
            <span class="meeting-year-count" data-meeting-year-count>@Model.Rok.PocetJednani jednání</span>
        </span>
        <gov-icon class="meeting-year-chevron" name="chevron-down" type="components" aria-hidden="true"></gov-icon>
    </button>
    <div class="meeting-year-body@(Model.IsPreviewYear ? " is-preview" : null)"
         data-meeting-year-body
         @(Model.IsPreviewYear ? null : "hidden")>
        <div class="meeting-grid" data-meeting-year-grid>
            @foreach (var jednani in Model.Rok.Jednani)
            {
                var detailUrl = Url.Action("Detail", new { id = jednani.Id }) ?? $"/Jednani/Detail/{jednani.Id}";
                var meetingPdfUrl = Url.Action("JednaniTisk", "Export", new { jednaniId = jednani.Id, autoPrint = true }) ?? $"/Export/Jednani/{jednani.Id}/Tisk?autoPrint=true";
                var meetingWordUrl = Url.Action("JednaniWord", "Export", new { jednaniId = jednani.Id }) ?? $"/Export/Jednani/{jednani.Id}/Word";
                <div class="meeting-card-slot" data-meeting-card-wrap data-meeting-id="@jednani.Id">
                    @await Html.PartialAsync("_MeetingCard", new MeetingCardViewModel
                    {
                        ProjektId = Model.ProjektId,
                        CanDelete = Model.CanDeleteMeetings,
                        UseAjaxDelete = false,
                        ReturnUrl = $"{Context.Request.Path}{Context.Request.QueryString}",
                        DetailUrl = detailUrl,
                        PrintPdfUrl = meetingPdfUrl,
                        PrintWordUrl = meetingWordUrl,
                        Jednani = jednani
                    })
                </div>
            }
        </div>
    </div>
</section>
```

Přidat ViewModel do `PmTracker.Web/Models/ViewModels/JednaniViewModels.cs` (na konec souboru, namespace `PmTracker.Web.Models.ViewModels`):

```csharp
public sealed class MeetingYearGroupPartialViewModel
{
    public JednaniYearGroupViewModel Rok { get; init; } = null!;
    public bool IsPreviewYear { get; init; }
    public int ProjektId { get; init; }
    public bool CanDeleteMeetings { get; init; }
}
```

- [ ] **Step 2.5: Extend meetingOverview.js s toggleProjectHistory**

Do `PmTracker.Web/wwwroot/js/modules/meetingOverview.js` přidat na konec souboru (po existujícím `toggleMeetingYearGroup`):

```javascript
// Úprava #2 (2026-04-20): project-level history toggle v aplikační
// záložce /Jednani/Index. Klikání na hlavičku projekt-karty rozbalí / sbalí
// wrapper historických year-groups. Shodný vzorec jako year-toggle, ale
// o úroveň výše (project-level vs. year-level).
export function toggleProjectHistory(toggleEl) {
    if (!(toggleEl instanceof HTMLElement)) {
        return;
    }

    const card = toggleEl.closest("[data-project-card]");
    if (!(card instanceof HTMLElement)) {
        return;
    }

    const body = card.querySelector("[data-project-history-body]");
    if (!(body instanceof HTMLElement)) {
        return;
    }

    const chevron = toggleEl.querySelector(".meeting-project-chevron");
    const isExpanded = toggleEl.getAttribute("aria-expanded") === "true";
    const nextExpanded = !isExpanded;

    toggleEl.setAttribute("aria-expanded", nextExpanded ? "true" : "false");
    if (nextExpanded) {
        body.removeAttribute("hidden");
    } else {
        body.setAttribute("hidden", "");
    }

    if (chevron instanceof HTMLElement) {
        chevron.setAttribute("name", nextExpanded ? "chevron-up" : "chevron-down");
    }
}
```

- [ ] **Step 2.6: Wire up bootstrap.js click + keyboard delegation**

V `PmTracker.Web/wwwroot/js/modules/bootstrap.js`:

Import (řádek 20 area — přidat `toggleProjectHistory`):
```javascript
import { initMeetingOverview, toggleMeetingYearGroup, toggleProjectHistory } from "./meetingOverview.js";
```

V global click handler (hledat `const meetingYearToggle = target.closest("[data-meeting-year-toggle]");` — tam přidat nový blok PŘED ním):
```javascript
    const projectHistoryToggle = target.closest("[data-project-history-toggle]");
    if (projectHistoryToggle instanceof HTMLElement) {
        event.preventDefault();
        toggleProjectHistory(projectHistoryToggle);
        return;
    }

    const meetingYearToggle = target.closest("[data-meeting-year-toggle]");
    // ... (existing code pokračuje)
```

Přidat keyboard support (hledat `document.addEventListener("keydown"` v bootstrap.js — pokud existuje, přidat handler; pokud ne, přidat nový):
```javascript
document.addEventListener("keydown", (event) => {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
        return;
    }
    const projectHistoryToggle = target.closest("[data-project-history-toggle]");
    if (projectHistoryToggle instanceof HTMLElement && projectHistoryToggle === target) {
        event.preventDefault();
        toggleProjectHistory(projectHistoryToggle);
    }
});
```

- [ ] **Step 2.7: Sync site.bundle.js**

V `PmTracker.Web/wwwroot/js/site.bundle.js`:

1. Najít `function toggleMeetingYearGroup(toggle)` (cca řádek 2735) a PŘIDAT za něj `function toggleProjectHistory(toggleEl) { ... }` s identickým tělem jako v meetingOverview.js (bez `export` keyword, bundle je CJS/IIFE).

2. Najít global click handler (cca řádek 9260) s `const meetingYearToggle = target.closest("[data-meeting-year-toggle]");` a PŘIDAT před tím blok pro `projectHistoryToggle` (viz Step 2.6).

3. Na konec bundle keydown handler (pokud není, přidat nový blok per Step 2.6).

- [ ] **Step 2.8: Add CSS styles**

V `PmTracker.Web/wwwroot/css/site.css` najít block `.meeting-year-stack { ... }` (cca řádek 4448) a PŘED něj přidat:

```css
/* ==========================================================================
   Projekt-level history toggle (úprava #2, 2026-04-20)
   Klikatelná hlavička projekt-karty na /Jednani/Index rozbalí/sbalí
   wrapper historických year-groups. Konzistentní pattern s year-toggle.
   ========================================================================== */

.meeting-project-overview .meeting-header {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 0;
}

.meeting-project-overview .meeting-header > h2 {
    flex: 1 1 auto;
    margin: 0;
}

.meeting-project-overview .meeting-header[data-project-history-toggle] {
    cursor: pointer;
    user-select: none;
    padding: 8px 12px;
    border-radius: var(--gov-radius);
    transition: background-color 0.15s ease;
}

.meeting-project-overview .meeting-header[data-project-history-toggle]:hover {
    background: color-mix(in srgb, var(--gov-color-primary) 5%, transparent);
}

.meeting-project-overview .meeting-header[data-project-history-toggle]:focus-visible {
    outline: 2px solid var(--gov-color-primary);
    outline-offset: 2px;
}

.meeting-project-history-count {
    font-size: 13px;
    color: var(--gov-color-muted);
    font-weight: 400;
}

.meeting-project-chevron {
    flex: 0 0 auto;
    font-size: 16px;
    color: currentColor;
}

.meeting-project-history-body {
    margin-top: 10px;
}
```

- [ ] **Step 2.9: Update spec doc**

V `docs/specs/meetings-year-grouping.md` nahradit sekci "Aplikační záložka `/Jednani/Index`":

```markdown
### Aplikační záložka `/Jednani/Index`

#### Struktura projekt-karty

V každé projekt-kartě se zobrazuje:

1. **Hlavička projektu** (`<header class="meeting-header">`) — název + `+N starších roků` counter + chevron. Celá hlavička je **klikatelný toggle** pro rozpad/sbalení historických roků (`data-project-history-toggle`).
2. **Aktuální rok** (`PreviewRok`) — **vždy viditelný** year-group v `state="preview"` (první řádek karet).
3. **Historické roky** — schované ve wrapperu `[data-project-history-body][hidden]`. Po kliknutí na hlavičku projektu se zobrazí jako year-groups v `state="collapsed"` (každý rok má vlastní year-chevron pro rozbalení jednání).

#### Viditelnost

| Element | Default | Po kliknutí na header |
| --- | --- | --- |
| Hlavička projektu | viditelná, chevron-down | chevron-up |
| Aktuální rok year-group | `state="preview"` | nezměněno |
| Historické year-groups | `hidden` (skryté) | viditelné, každý `state="collapsed"` |

**Důvod**: projekt-karta drží jen aktuální dění; starší historie je tichá, ale jedno kliknutí ji ukáže. Each year-group je pak individuálně collapsed (další click-through pro jednání).

#### Markup

```html
<section class="card meeting-project-overview" data-project-card>
    <header class="meeting-header"
            role="button"
            data-project-history-toggle
            aria-controls="project-history-42"
            aria-expanded="false"
            tabindex="0">
        <h2>Název projektu</h2>
        <span class="meeting-project-history-count" aria-hidden="true">+3 starších roků</span>
        <gov-icon class="meeting-project-chevron" name="chevron-down" ...></gov-icon>
    </header>

    <!-- Aktuální rok vždy viditelný -->
    <div class="meeting-year-stack" data-meeting-overview="year-grouped"
         data-meeting-history-default="collapsed"
         data-meeting-preview-year="2026">
        <section class="meeting-year-group" data-meeting-year-state="preview">...</section>
    </div>

    <!-- Historické roky za toggle -->
    <div class="meeting-project-history-body"
         id="project-history-42"
         data-project-history-body
         hidden>
        <div class="meeting-year-stack" ...>
            <section class="meeting-year-group" data-meeting-year-state="collapsed">...</section>
            <!-- další historické year-groups -->
        </div>
    </div>
</section>
```
```

- [ ] **Step 2.10: Run all tests + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors. Tests 526+ passed (3 new).

```bash
git add PmTracker.Web/Views/Jednani/Index.cshtml \
        PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml \
        PmTracker.Web/Models/ViewModels/JednaniViewModels.cs \
        PmTracker.Web/wwwroot/js/modules/meetingOverview.js \
        PmTracker.Web/wwwroot/js/modules/bootstrap.js \
        PmTracker.Web/wwwroot/js/site.bundle.js \
        PmTracker.Web/wwwroot/css/site.css \
        PmTracker.Tests.Unit/Layout/MeetingsYearGroupingTests.cs \
        docs/specs/meetings-year-grouping.md
git commit -m "feat(jednani): project-level history toggle na /Jednani/Index (úprava #2)

Hlavička projekt-karty je nyní klikatelný toggle — defaultně viditelný
pouze aktuální rok, historické roky schované ve wrapperu za toggle.
Chevron swap name attribute (ne CSS rotate — gov-icon artefakty).

Markup:
- meeting-header[role=button][data-project-history-toggle]
- meeting-project-history-body[hidden] wrapper kolem historických year-groups
- _MeetingYearGroup partial extrahována (DRY per year-group render)

JS:
- meetingOverview.js exportuje toggleProjectHistory
- bootstrap.js deleguje click + keyboard (Space/Enter)
- site.bundle.js synchronizován

Spec meetings-year-grouping.md rozšířena."
```

---

## Task 3: C1 — Floating portal dynamic re-parent

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/modals.js`
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js`
- Create: `PmTracker.Tests.Unit/Layout/ModalPortalReparentTests.cs`

- [ ] **Step 3.1: Write failing test**

Vytvořit nový soubor `PmTracker.Tests.Unit/Layout/ModalPortalReparentTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #7 (2026-04-20): floating portal panel se při otevření gov-dialog
/// přemísťuje DOVNITŘ aktivního modalu, aby dědil stacking context shadow DOM
/// a byl vizuálně nad modalem (předtím se vykresloval ZA modalem).
/// </summary>
public sealed class ModalPortalReparentTests
{
    [Fact]
    public void ModalsJs_ShouldReparentFloatingRootOnOpen()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/modals.js"));
        js.Should().Contain("reparentFloatingRootIntoModal",
            "modals.js musí volat reparentFloatingRootIntoModal při otevření modalu");
        js.Should().Contain("floating-panel-root",
            "fix manipuluje s #floating-panel-root elementem");
    }

    [Fact]
    public void ModalsJs_ShouldRestoreFloatingRootOnClose()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/modals.js"));
        js.Should().Contain("restoreFloatingRoot",
            "modals.js musí volat restoreFloatingRoot před clear modalu");
    }

    [Fact]
    public void SiteBundle_ShouldContainReparentHelpers()
    {
        var bundle = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/site.bundle.js"));
        bundle.Should().Contain("reparentFloatingRootIntoModal",
            "site.bundle.js synchronizovan s modals.js");
        bundle.Should().Contain("restoreFloatingRoot");
    }
}
```

- [ ] **Step 3.2: Run test — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ModalPortalReparentTests" 2>&1 | tail -5
```
Expected: 3 tests fail.

- [ ] **Step 3.3: Implement reparent helpers v modals.js**

Do `PmTracker.Web/wwwroot/js/modules/modals.js` — najít funkci `closeModal` a PŘED ní (nebo na úplný začátek souboru po existujících definicích) přidat:

```javascript
// Úprava #7 (2026-04-20): Floating portal (#floating-panel-root) je light DOM
// element v _Layout.cshtml. Gov-dialog má vlastní shadow DOM stacking context →
// picker panely render-ují za modalem. Řešení: při openModal přesun root DO
// aktivního gov-dialogu, při closeModal vrátit zpět (původní parent + position).
let floatingRootOriginalParent = null;
let floatingRootOriginalNextSibling = null;

export function reparentFloatingRootIntoModal(dialog) {
    if (!(dialog instanceof HTMLElement)) {
        return;
    }
    const root = document.getElementById("floating-panel-root");
    if (!(root instanceof HTMLElement)) {
        return;
    }
    // Už uvnitř modalu? (idempotent guard)
    if (root.parentElement === dialog) {
        return;
    }
    if (floatingRootOriginalParent === null) {
        floatingRootOriginalParent = root.parentElement;
        floatingRootOriginalNextSibling = root.nextSibling;
    }
    dialog.prepend(root);
}

export function restoreFloatingRoot() {
    const root = document.getElementById("floating-panel-root");
    if (!(root instanceof HTMLElement) || floatingRootOriginalParent === null) {
        return;
    }
    if (floatingRootOriginalNextSibling && floatingRootOriginalNextSibling.parentNode === floatingRootOriginalParent) {
        floatingRootOriginalParent.insertBefore(root, floatingRootOriginalNextSibling);
    } else {
        floatingRootOriginalParent.appendChild(root);
    }
    floatingRootOriginalParent = null;
    floatingRootOriginalNextSibling = null;
}
```

Dále najít funkci která otevírá modal (pravděpodobně `setModalContent` nebo `openModal`) a po insertBody/querySelector pro `gov-dialog[data-modal-container]` přidat:

```javascript
const activeDialog = modalRoot.querySelector("gov-dialog[data-modal-container]");
if (activeDialog instanceof HTMLElement) {
    // Čekej tick na custom element upgrade
    requestAnimationFrame(() => reparentFloatingRootIntoModal(activeDialog));
}
```

V `closeModal` PŘED `modalRoot.innerHTML = ""` (nebo ekvivalent clear) volat:
```javascript
restoreFloatingRoot();
```

- [ ] **Step 3.4: Sync site.bundle.js**

V `PmTracker.Web/wwwroot/js/site.bundle.js` najít funkci která je inline verzí `closeModal` / `setModalContent`. Najít sekci s definicemi modal helpers (cca první ~1500 řádků). Přidat identické tělo `reparentFloatingRootIntoModal` + `restoreFloatingRoot` + top-level proměnné `floatingRootOriginalParent`, `floatingRootOriginalNextSibling` (bez `export` keyword).

Najít volací místa (open/close) a přidat volání per Step 3.3.

- [ ] **Step 3.5: Run tests + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors. Tests 529+ (3 new pass).

```bash
git add PmTracker.Web/wwwroot/js/modules/modals.js \
        PmTracker.Web/wwwroot/js/site.bundle.js \
        PmTracker.Tests.Unit/Layout/ModalPortalReparentTests.cs
git commit -m "fix(modals): floating portal dynamic re-parent do aktivního modalu (úprava #7)

Regrese z Fáze 2E (a3bba6b) — #floating-panel-root byl přesunut z nitra
gov-dialog kvůli shadow DOM kolizi, ale tím picker panely ztratily
stacking context modalu a vykreslovaly se ZA modalem.

Fix: při openModal JS přesune root jako first child aktivního gov-dialog
(dědí shadow DOM stacking), při closeModal vrátí zpět na původní pozici.
Idempotent guard + original-position memo.

Aplikuje se na všechny pickery (AD, person, date, time) sdílející root."
```

---

## Task 4: C2 — Universal gov-close handler

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js`
- Create: `PmTracker.Tests.Unit/Layout/GovCloseHandlerTests.cs`

- [ ] **Step 4.1: Write failing test**

Vytvořit `PmTracker.Tests.Unit/Layout/GovCloseHandlerTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úpravy #8 + #9/1 (2026-04-20): gov-close event musí korektně zavřít
/// všechny modaly. Record-editor si ponechává dirty-check flow (existing),
/// ostatní modaly fallback na přímý closeModal().
/// </summary>
public sealed class GovCloseHandlerTests
{
    [Fact]
    public void BootstrapJs_HandleGovCloseEvent_ShouldFallbackToCloseModal()
    {
        var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/bootstrap.js"));
        js.Should().Contain("handleGovCloseEvent",
            "bootstrap.js musí definovat handleGovCloseEvent");
        // Musí obsahovat fallback větev na closeModal
        js.Should().MatchRegex(
            @"handleGovCloseEvent[^}]*closeModal\(\)",
            "handler obsahuje fallback volání closeModal() pro non-record-editor modaly");
    }

    [Fact]
    public void SiteBundle_ShouldContainUniversalCloseFallback()
    {
        var bundle = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/site.bundle.js"));
        bundle.Should().Contain("function handleGovCloseEvent",
            "bundle synchronizovan");
        bundle.Should().MatchRegex(
            @"handleGovCloseEvent[^}]*closeModal\(\)",
            "bundle obsahuje fallback closeModal()");
    }
}
```

- [ ] **Step 4.2: Run — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~GovCloseHandlerTests" 2>&1 | tail -5
```

- [ ] **Step 4.3: Update bootstrap.js handleGovCloseEvent**

V `PmTracker.Web/wwwroot/js/modules/bootstrap.js` najít funkci `handleGovCloseEvent` (pravděpodobně cca řádek 200-260). Nahradit tělo:

```javascript
function handleGovCloseEvent(event) {
    const dialog = event.target;
    if (!(dialog instanceof HTMLElement) || dialog.tagName !== "GOV-DIALOG") {
        return;
    }

    // Record-editor má vlastní dirty-check flow — gov-close je žádost
    // o zavření, kterou musí schválit promptRecordEditorDiscard.
    if (dialog.matches('[data-modal-variant="record-editor"]') ||
        document.querySelector("[data-record-editor-form][data-dirty='true']")) {
        event.preventDefault();
        requestRecordEditorModalClose();
        return;
    }

    // Fallback pro všechny non-record-editor modaly (Přidat ručně, AD search,
    // Přidat projektovou roli, atd.) — gov-close je fire-and-close,
    // žádný dirty-check není potřeba.
    event.preventDefault();
    closeModal();
}
```

Pokud původní `handleGovCloseEvent` volal jinou logiku, zachovat stávající record-editor cestu a přidat fallback `closeModal()` větev za ní.

- [ ] **Step 4.4: Sync site.bundle.js**

V `PmTracker.Web/wwwroot/js/site.bundle.js` najít `function handleGovCloseEvent` a stejný patch (identický body).

- [ ] **Step 4.5: Run + commit**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Tests 531+ pass.

```bash
git add PmTracker.Web/wwwroot/js/modules/bootstrap.js \
        PmTracker.Web/wwwroot/js/site.bundle.js \
        PmTracker.Tests.Unit/Layout/GovCloseHandlerTests.cs
git commit -m "fix(modals): gov-close universal fallback pro non-record-editor modaly (úpravy #8 #9/1)

Regrese z Fáze 2E — handleGovCloseEvent řešil POUZE record-editor dirty-check.
Pro ostatní modaly (Přidat ručně, AD search, Přidat projektovou/subsystémovou
roli) křížek nic nedělal.

Fix: po record-editor větvi fallback na closeModal() pro všechny ostatní
modaly. Record-editor dirty-check flow nedotčen."
```

---

## Task 5: C3 — Modal overflow policy (default hidden + opt-in flag)

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml` (jediný legit opt-in)
- Create: `PmTracker.Tests.Unit/Layout/ModalLayoutRulesTests.cs`
- Create: `docs/specs/modal-layout-rules.md`

- [ ] **Step 5.1: Write failing test**

Vytvořit `PmTracker.Tests.Unit/Layout/ModalLayoutRulesTests.cs`:

```csharp
using System.IO;
using System.Linq;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #9/2 (2026-04-20): modaly defaultně NEscrollovatelné.
/// User: "modaly by neměly být scrollovatelné bez opravdu závažných důvodů".
/// Výjimka: record-editor form může být delší než viewport — opt-in flag
/// data-modal-scrollable="true".
/// </summary>
public sealed class ModalLayoutRulesTests
{
    [Fact]
    public void SiteCss_ShouldDefaultModalOverflowToHidden()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"gov-dialog\[data-modal-container\]\s+\.modal-content\s*\{[^}]*overflow\s*:\s*hidden",
            "CSS musí defaultně nastavit overflow hidden na modal-content");
    }

    [Fact]
    public void SiteCss_ShouldAllowOptInScrollable()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"data-modal-scrollable=""true""[^{]*\{[^}]*overflow-y\s*:\s*auto",
            "CSS umožňuje opt-in scroll přes data-modal-scrollable flag");
    }

    [Fact]
    public void RecordEditorModal_ShouldBeOnlyOptInScrollable()
    {
        var editForm = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml"));
        editForm.Should().Contain("data-modal-scrollable=\"true\"",
            "record-editor form (delší než viewport) je legitimní výjimka z scroll policy");
    }

    [Fact]
    public void SpecDocument_ShouldDocumentOverflowRules()
    {
        var spec = File.ReadAllText(ResolvePath("docs/specs/modal-layout-rules.md"));
        spec.Should().Contain("data-modal-scrollable",
            "spec dokumentuje opt-in flag");
        spec.Should().Contain("record-editor",
            "spec zmiňuje record-editor jako jedinou legitimní výjimku");
    }
}
```

- [ ] **Step 5.2: Run — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~ModalLayoutRulesTests" 2>&1 | tail -5
```

- [ ] **Step 5.3: Update CSS**

V `PmTracker.Web/wwwroot/css/site.css` najít block `gov-dialog[data-modal-container] .modal-content` (cca řádek 3966) a nahradit:

```css
/* Úprava #9/2 (2026-04-20): modaly defaultně NEscrollovatelné.
   Opt-in přes data-modal-scrollable="true" pro legitimní výjimky
   (aktuálně jen record-editor form delší než viewport). Viz spec
   docs/specs/modal-layout-rules.md. */
gov-dialog[data-modal-container] .modal-content {
    padding: 16px;
    overflow: hidden;
}

gov-dialog[data-modal-container][data-modal-scrollable="true"] .modal-content {
    overflow-y: auto;
    max-height: calc(92vh - 32px); /* interní scroll region uvnitř dialogu */
}
```

- [ ] **Step 5.4: Add opt-in flag to record-editor modal**

V `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml` najít root `<gov-dialog ...>` nebo wrapper který renderuje modalu. Pokud je ve view jen form (wrapper je `_ModalLayout.cshtml`), najít ViewData.ModalVariant="record-editor" a přidat:

Pokud `_ModalLayout.cshtml` generuje gov-dialog, přidat tam podmíněný atribut:
V `PmTracker.Web/Views/Shared/_ModalLayout.cshtml` řádek s `<gov-dialog open="true"`:

```razor
<gov-dialog open="true"
            block-close="true"
            block-backdrop-close="true"
            data-modal-container
            data-modal-variant="@normalizedVariant"
            @(string.Equals(normalizedVariant, "record-editor") ? "data-modal-scrollable=\"true\"" : "")
            data-modal-overflow-visible="@(overflowVisible ? "true" : "false")"
            aria-labelledby="@modalTitleId"
            tabindex="-1">
```

Tím se flag automaticky nastaví pro všechny modaly vykreslené jako variant "record-editor".

- [ ] **Step 5.5: Create spec doc**

Vytvořit `docs/specs/modal-layout-rules.md`:

```markdown
# Specifikace — modal layout rules (modal-overflow-policy)

**Datum:** 2026-04-20
**Autor:** Pavel Andrlík (user request 2026-04-20 ranní inbox, úprava #9/2)

## Pravidlo

**Modaly (`<gov-dialog data-modal-container>`) NEJSOU scrollovatelné v default stavu.** Content uvnitř modalu se musí vejít do viewportu.

Pokud je content delší, má dvě možnosti:
1. **Refactor layoutu** — některý interní region má vlastní `overflow-y: auto` (např. seznam výsledků uvnitř modalu s fixed search inputem).
2. **Opt-in flag** `data-modal-scrollable="true"` — modal jako celek scrollable. **Pouze pro legitimní výjimky.**

## Legitimní výjimky (whitelist)

- **Record-editor form** (`_EditZaznamForm.cshtml`, variant `record-editor`): form obsahuje metadata + harmonogram + vyjádření + externí vazby — může překročit výšku viewportu i na plném 1080p monitoru. Opt-in flag je nastavován automaticky přes `_ModalLayout.cshtml` když `ModalVariant == "record-editor"`.

## Zakázané vzory

- Přidávání `data-modal-scrollable="true"` na nové modaly bez diskuse / aktualizace tohoto whitelistu
- Použití `overflow-y: auto` na `.modal-content` v custom CSS uvnitř views
- Scroll jako quick-fix pro content overflow — místo toho refactor layoutu (sticky header/footer + internal scroll region)

## CSS implementace

```css
gov-dialog[data-modal-container] .modal-content {
    padding: 16px;
    overflow: hidden;                                   /* default */
}

gov-dialog[data-modal-container][data-modal-scrollable="true"] .modal-content {
    overflow-y: auto;
    max-height: calc(92vh - 32px);
}
```

## Tests

Architecture test `ModalLayoutRulesTests` (PmTracker.Tests.Unit/Layout):
- CSS default overflow:hidden
- CSS opt-in scroll rule existuje
- Record-editor je jediný opt-in případ
```

- [ ] **Step 5.6: Run + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors. Tests 535+ (4 new).

```bash
git add PmTracker.Web/wwwroot/css/site.css \
        PmTracker.Web/Views/Shared/_ModalLayout.cshtml \
        PmTracker.Tests.Unit/Layout/ModalLayoutRulesTests.cs \
        docs/specs/modal-layout-rules.md
git commit -m "fix(modals): overflow policy default hidden + opt-in scrollable flag (úprava #9/2)

User pravidlo: 'modaly by neměly být scrollovatelné bez opravdu závažných
důvodů'. CSS default .modal-content overflow: hidden. Opt-in přes
data-modal-scrollable=true, auto nastaveno pro variant='record-editor'
(jediná legitimní výjimka — form delší než viewport).

Nová spec docs/specs/modal-layout-rules.md dokumentuje pravidlo +
whitelist výjimek."
```

---

## Task 6: B1 — Dashboard full-width + non-scrollable layout

**Files:**
- Modify: `PmTracker.Web/Views/Dashboard/Index.cshtml`
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml` (jen pokud dashboard potřebuje bypass container)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (dashboard layout sekce)
- Create: `PmTracker.Tests.Unit/Layout/DashboardLayoutTests.cs`

- [ ] **Step 6.1: Write failing test**

Vytvořit `PmTracker.Tests.Unit/Layout/DashboardLayoutTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #4 (2026-04-20): user dashboard full-width + non-scrollable.
/// Layout 2/3 Záznamy vlevo, 1/3 pravý sloupec split vertikálně
/// (Jednání nahoře, News dole). Responsive fallback <1024px na stacked scrollable.
/// </summary>
public sealed class DashboardLayoutTests
{
    [Fact]
    public void DashboardIndex_ShouldUseFullWidthShell()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/Index.cshtml"));
        view.Should().Contain("dashboard-shell",
            "Dashboard view používá .dashboard-shell (full-viewport height) wrapper");
        view.Should().Contain("data-dashboard-shell",
            "data-dashboard-shell marker pro JS scope + architecture testing");
    }

    [Fact]
    public void DashboardCss_ShouldDefineFullHeightGrid()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.dashboard-shell\s*\{[^}]*height\s*:\s*100vh",
            "shell má 100vh height (non-scrollable outer)");
        css.Should().MatchRegex(
            @"\.dashboard-shell\s*\{[^}]*overflow\s*:\s*hidden",
            "shell overflow hidden (non-scrollable outer)");
        css.Should().MatchRegex(
            @"\.dashboard-body\s*\{[^}]*grid-template-columns\s*:\s*2fr\s+1fr",
            "body grid 2/3 + 1/3");
    }

    [Fact]
    public void DashboardCss_ShouldPlacePanelsIn2By3Layout()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().MatchRegex(
            @"\.dashboard-section--focus[^}]*grid-column\s*:\s*1[^}]*grid-row\s*:\s*1\s*/\s*3",
            "Focus panel span vlevo celá výška");
        css.Should().MatchRegex(
            @"\.dashboard-section--meetings[^}]*grid-column\s*:\s*2[^}]*grid-row\s*:\s*1",
            "Meetings vpravo nahoře");
        css.Should().MatchRegex(
            @"\.dashboard-section--news[^}]*grid-column\s*:\s*2[^}]*grid-row\s*:\s*2",
            "News vpravo dole");
    }

    [Fact]
    public void DashboardCss_ShouldProvideResponsiveFallback()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().Contain("@media (max-width: 1024px)",
            "responsive fallback stacking pod 1024px");
    }
}
```

- [ ] **Step 6.2: Run — expect FAIL**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~DashboardLayoutTests" 2>&1 | tail -5
```

- [ ] **Step 6.3: Rewrite Dashboard/Index.cshtml**

Nahradit obsah `PmTracker.Web/Views/Dashboard/Index.cshtml`:

```razor
@model DashboardPageViewModel
@{
    ViewData["Title"] = Model.PageTitle;
    ViewData["BodyClass"] = "dashboard-page"; // pro _Layout full-width bypass
}

<section class="dashboard-shell" data-dashboard-shell>
    <header class="dashboard-shell-header">
        <h1>@Model.PageTitle</h1>
        @if (!string.IsNullOrWhiteSpace(Model.Subtitle))
        {
            <p class="muted">@Model.Subtitle</p>
        }
    </header>
    <div class="dashboard-body">
        <section class="dashboard-panel dashboard-section--focus"
                 data-dashboard-panel="focus"
                 data-dashboard-panel-url="@Model.FocusPanelUrl">
            <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>
                <div class="record-loading-line"></div>
                <div class="record-loading-line short"></div>
                <div class="record-loading-line"></div>
                <div class="record-loading-line short"></div>
            </div>
            <div data-dashboard-panel-content></div>
        </section>

        <section class="dashboard-panel dashboard-section--meetings"
                 data-dashboard-panel="meetings"
                 data-dashboard-panel-url="@Model.MeetingsPanelUrl">
            <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>
                <div class="record-loading-line"></div>
                <div class="record-loading-line short"></div>
            </div>
            <div data-dashboard-panel-content></div>
        </section>

        <section class="dashboard-panel dashboard-section--news"
                 data-dashboard-panel="news"
                 data-dashboard-panel-url="@Model.NewsPanelUrl">
            <div class="dashboard-panel-placeholder" data-dashboard-panel-placeholder>
                <div class="record-loading-line"></div>
                <div class="record-loading-line short"></div>
            </div>
            <div data-dashboard-panel-content></div>
        </section>
    </div>
</section>
```

- [ ] **Step 6.4: Update _Layout.cshtml — body class conditional full-width**

V `PmTracker.Web/Views/Shared/_Layout.cshtml` najít `<main id="main" class="app-main">` a nahradit:

```razor
@{
    var bodyClass = ViewData["BodyClass"]?.ToString();
    var mainClass = bodyClass == "dashboard-page" ? "app-main app-main--fluid" : "app-main";
}
<main id="main" class="@mainClass">
```

- [ ] **Step 6.5: Add CSS**

V `PmTracker.Web/wwwroot/css/site.css` najít existující `.dashboard-home` sekci (pokud existuje) a nahradit / nebo přidat nový block na konec souboru:

```css
/* ==========================================================================
   Dashboard full-width + non-scrollable layout (úprava #4, 2026-04-20)
   2/3 Záznamy vlevo (span row 1+2), 1/3 pravý sloupec stacked
   (Jednání row 1, News row 2). Non-scrollable outer, per-section internal scroll.
   Responsive fallback <1024px stacked + scrollable.
   ========================================================================== */

.app-main--fluid {
    max-width: none;
    padding: 0;
}

.dashboard-shell {
    height: 100vh;
    display: grid;
    grid-template-rows: auto 1fr;
    grid-template-columns: 100%;
    overflow: hidden;
}

.dashboard-shell-header {
    padding: 16px 24px;
    border-bottom: 1px solid var(--gov-color-border);
    flex: 0 0 auto;
}

.dashboard-shell-header h1 {
    margin: 0 0 4px;
    font-size: 22px;
}

.dashboard-body {
    display: grid;
    grid-template-columns: 2fr 1fr;
    grid-template-rows: 1fr 1fr;
    gap: 12px;
    min-height: 0;
    padding: 12px 24px 24px;
}

.dashboard-panel {
    display: flex;
    flex-direction: column;
    min-height: 0;
    overflow: hidden;
    background: var(--gov-color-surface);
    border: 1px solid var(--gov-color-border);
    border-radius: var(--gov-radius-lg);
}

.dashboard-section--focus {
    grid-column: 1;
    grid-row: 1 / 3;
}

.dashboard-section--meetings {
    grid-column: 2;
    grid-row: 1;
}

.dashboard-section--news {
    grid-column: 2;
    grid-row: 2;
}

.dashboard-panel-body {
    display: flex;
    flex-direction: column;
    min-height: 0;
    height: 100%;
}

.dashboard-panel-header {
    flex: 0 0 auto;
    padding: 16px 16px 12px;
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    gap: 12px;
}

.dashboard-panel-footer {
    flex: 0 0 auto;
    padding: 10px 16px;
    border-top: 1px solid var(--gov-color-border);
    display: flex;
    justify-content: flex-end;
    gap: 8px;
}

.dashboard-focus-list,
.dashboard-meetings-list,
.dashboard-news-list {
    flex: 1 1 auto;
    min-height: 0;
    overflow-y: auto;
    padding: 0 16px 12px;
}

@media (max-width: 1024px) {
    .dashboard-shell {
        height: auto;
        overflow: auto;
    }
    .dashboard-body {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto auto;
    }
    .dashboard-section--focus,
    .dashboard-section--meetings,
    .dashboard-section--news {
        grid-column: 1;
        grid-row: auto;
    }
    .dashboard-panel {
        min-height: 280px;
    }
}
```

- [ ] **Step 6.6: Run + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

```bash
git add PmTracker.Web/Views/Dashboard/Index.cshtml \
        PmTracker.Web/Views/Shared/_Layout.cshtml \
        PmTracker.Web/wwwroot/css/site.css \
        PmTracker.Tests.Unit/Layout/DashboardLayoutTests.cs
git commit -m "feat(dashboard): full-width + non-scrollable layout 2/3+1/3 (úprava #4)

CSS Grid 2fr 1fr × 1fr 1fr. Focus (záznamy) span row 1+2 vlevo, Meetings
top-right, News bottom-right. Header ~72px nahoře. Outer 100vh overflow hidden,
per-section internal scroll. Responsive fallback <1024px na single-column scrollable.

_Layout.cshtml ViewData.BodyClass=dashboard-page flag → app-main--fluid bypass
default container max-width."
```

---

## Task 7: B2 — "Zobrazit více" unified + smazat "Načíst více"

**Files:**
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml`
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml`
- Modify: `PmTracker.Web/wwwroot/js/modules/dashboard.js`
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js`

- [ ] **Step 7.1: Write failing test**

Přidat do `PmTracker.Tests.Unit/Layout/DashboardLayoutTests.cs`:

```csharp
[Theory]
[InlineData("PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml")]
[InlineData("PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml")]
[InlineData("PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml")]
public void DashboardPanels_ShouldUseZobrazitViceOnly(string relativePath)
{
    var view = File.ReadAllText(ResolvePath(relativePath));
    view.Should().NotContain("Načíst více",
        $"úprava #4: 'Načíst více' smazáno napříč dashboard panely ({relativePath})");
    view.Should().Contain("Zobrazit více",
        "jediné unified CTA");
}

[Fact]
public void DashboardJs_ShouldNotReferenceLoadMore()
{
    var js = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/dashboard.js"));
    js.Should().NotContain("data-dashboard-news-load-more",
        "lazy-load pagination smazána — 'Zobrazit více' routuje na /Dashboard/News");
    js.Should().NotContain("loadMoreButton",
        "load-more JS handler smazán");
}
```

- [ ] **Step 7.2: Run — expect FAIL**

- [ ] **Step 7.3: Update _DashboardFocusPanel.cshtml**

V `PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml` najít `<pm-button variant="Ghost" ... >Zobrazit vše</pm-button>` a změnit text na `Zobrazit více`:

```razor
<pm-button variant="Ghost" size="Small" href="@Model.ListUrl">Zobrazit více</pm-button>
```

- [ ] **Step 7.4: Update _DashboardMeetingsPanel.cshtml**

Již obsahuje "Zobrazit více" — ověřit žádné "Načíst více":
```bash
grep -n "Načíst více" PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml || echo "clean"
```

- [ ] **Step 7.5: Update _DashboardNewsPanel.cshtml — smazat load-more**

V `PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml` SMAZAT celý `@if (Model.CanLoadMore ...)` blok:

Před:
```razor
<div class="dashboard-panel-footer">
    @if (Model.CanLoadMore && !string.IsNullOrWhiteSpace(Model.LoadMoreUrl))
    {
        <pm-button variant="Secondary" size="Small" data-dashboard-news-load-more="@Model.LoadMoreUrl">Načíst více</pm-button>
    }
    <pm-button variant="Ghost" size="Small" href="@Model.ListUrl">Zobrazit více</pm-button>
</div>
```

Po:
```razor
<div class="dashboard-panel-footer">
    <pm-button variant="Ghost" size="Small" href="@Model.ListUrl">Zobrazit více</pm-button>
</div>
```

- [ ] **Step 7.6: Clean dashboard.js — smazat load-more handler**

V `PmTracker.Web/wwwroot/js/modules/dashboard.js` najít blok (cca řádky 84-100) obsahující `data-dashboard-news-load-more` / `loadMoreButton`. Smazat celý handler:

```javascript
// SMAZAT (cca řádek 84-100):
const loadMoreButton = target.closest("[data-dashboard-news-load-more]");
if (isButtonLike(loadMoreButton)) {
    // ... celé tělo ...
}
```

Pokud funkce měla další odkazy na load-more (např. `handleLoadMoreClick`), smazat je celé včetně helpers.

- [ ] **Step 7.7: Sync site.bundle.js**

V `PmTracker.Web/wwwroot/js/site.bundle.js` smazat identické load-more bloky (grep `data-dashboard-news-load-more` a `loadMoreButton` — smazat handler těla, ale ponechat ostatní nezatížené kód).

- [ ] **Step 7.8: Run + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

```bash
git add PmTracker.Web/Views/Dashboard/_DashboardFocusPanel.cshtml \
        PmTracker.Web/Views/Dashboard/_DashboardMeetingsPanel.cshtml \
        PmTracker.Web/Views/Dashboard/_DashboardNewsPanel.cshtml \
        PmTracker.Web/wwwroot/js/modules/dashboard.js \
        PmTracker.Web/wwwroot/js/site.bundle.js \
        PmTracker.Tests.Unit/Layout/DashboardLayoutTests.cs
git commit -m "refactor(dashboard): smazat 'Načíst více' + unify na 'Zobrazit více' (úprava #4)

3 panely: Focus 'Zobrazit vše' → 'Zobrazit více', Meetings beze změny,
News smazaná Secondary 'Načíst více' (lazy-load pagination z dashboardu ven —
'Zobrazit více' routuje na /Dashboard/News plnou stránku).

dashboard.js smazán load-more handler + site.bundle.js synchronizován."
```

---

## Task 8: B3 — News enrichment (ActorName + description context)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/HomeViewModels.cs`
- Modify: `PmTracker.Web/Services/Dashboard/DashboardService.cs`
- Modify: `PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml`
- Create: `PmTracker.Tests.Unit/Layout/DashboardNewsEnrichmentTests.cs`

- [ ] **Step 8.1: Write failing test**

Vytvořit `PmTracker.Tests.Unit/Layout/DashboardNewsEnrichmentTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #5 (2026-04-20): News items obsahují ActorName + kontext změny
/// (co/kdo). Současná Description redundantně opakuje ProjectLabel → nahrazena
/// smysluplným kontextem per event type.
/// </summary>
public sealed class DashboardNewsEnrichmentTests
{
    [Fact]
    public void NewsItemViewModel_ShouldHaveActorNameField()
    {
        var vmSrc = File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/HomeViewModels.cs"));
        vmSrc.Should().Contain("public string ActorName",
            "DashboardNewsItemViewModel musí mít ActorName field");
    }

    [Fact]
    public void DashboardService_ShouldPopulateActorName()
    {
        var svcSrc = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardService.cs"));
        svcSrc.Should().Contain("ActorName = ",
            "BuildNewsItemsAsync musí populovat ActorName na každém item");
    }

    [Fact]
    public void DashboardService_ShouldNotUseRedundantProjectLabelInDescription()
    {
        var svcSrc = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardService.cs"));
        // Staré: Description = $"Záznam v projektu {BuildProjectLabel(...)}" — redundantní,
        // nové: Description obsahuje actor + kontext
        svcSrc.Should().NotContain("Description = $\"Záznam v projektu",
            "Description nesmí redundantně opakovat projekt (ProjectLabel je separate slot)");
    }

    [Fact]
    public void NewsListPartial_ShouldRenderActorInMeta()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml"));
        view.Should().Contain("@item.ActorName",
            "partial musí renderovat ActorName");
    }
}
```

- [ ] **Step 8.2: Run — expect FAIL**

- [ ] **Step 8.3: Add ActorName to ViewModel**

V `PmTracker.Web/Models/ViewModels/HomeViewModels.cs` najít `class DashboardNewsItemViewModel` a přidat field (za existujícím `EventLabel`):

```csharp
public sealed class DashboardNewsItemViewModel
{
    // ... existing fields ...
    public string EventLabel { get; init; } = string.Empty;
    public string ActorName { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ProjectLabel { get; init; } = string.Empty;
    // ... zbytek fields ...
}
```

- [ ] **Step 8.4: Add helper TruncateFirstLine**

V `PmTracker.Web/Services/Dashboard/DashboardService.cs` přidat na konec souboru uvnitř třídy (před closing brace):

```csharp
private static string TruncateFirstLine(string? source, int maxLen)
{
    if (string.IsNullOrWhiteSpace(source))
    {
        return string.Empty;
    }
    var firstLine = source
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n', 2)[0]
        .Trim();
    if (firstLine.Length <= maxLen)
    {
        return firstLine;
    }
    return firstLine.Substring(0, Math.Max(0, maxLen - 1)).TrimEnd() + "…";
}
```

- [ ] **Step 8.5: Resolve Actor names v BuildNewsItemsAsync**

V `PmTracker.Web/Services/Dashboard/DashboardService.cs` najít `BuildNewsItemsAsync` (cca řádek 270). Na začátek metody (po existujícím audit query, před foreach loopem) přidat resolve Actor jmen:

```csharp
// Úprava #5 (2026-04-20): ActorName resolve z OsobaId → celé jméno (pro popis
// "Pavel Andrlík • [kontext]"). Batch lookup podle ActorOsobaId v auditu.
var actorIds = auditRows
    .Select(r => r.ActorOsobaId)
    .Distinct()
    .ToArray();

var actorNameMap = await _dbContext.Osoby
    .Where(o => actorIds.Contains(o.Id))
    .Select(o => new { o.Id, FullName = (o.Jmeno + " " + o.Prijmeni).Trim() })
    .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

string ResolveActorName(int actorId) =>
    actorNameMap.TryGetValue(actorId, out var name) ? name : "Neznámý uživatel";
```

Pozn.: pokud `_dbContext.Osoby` nebo přesný název kontextu je jiný (ověřit při implementaci `PmTrackerDbContext`), upravit podle skutečného DbSet názvu.

- [ ] **Step 8.6: Update news item population**

V `BuildNewsItemsAsync` najít `items.Add(new DashboardNewsItemViewModel { ... EventLabel = ..., Title = ..., Description = ..., ProjectLabel = ... })` pro KOMENTÁŘ (vyjádření), ZÁZNAM, JEDNÁNÍ a upravit každý:

**Pro komentář** (cca řádek 400-415):
```csharp
items.Add(new DashboardNewsItemViewModel
{
    // ... existing fields ...
    EventLabel = string.Equals(row.Action, AuditActionType.Create.ToDatabaseValue(), StringComparison.OrdinalIgnoreCase) ? "Nové vyjádření" : "Upravené vyjádření",
    ActorName = ResolveActorName(row.ActorOsobaId),
    Title = $"{comment.RecordNumber} - {comment.RecordTitle}",
    Description = TruncateFirstLine(comment.Text, 120),
    ProjectLabel = BuildProjectLabel(comment.ProjectCode, comment.ProjectName),
    // ... zbytek ...
});
```

**Pro záznam** (cca řádek 455-475):
```csharp
items.Add(new DashboardNewsItemViewModel
{
    EventLabel = eventLabel,
    ActorName = ResolveActorName(row.ActorOsobaId),
    Title = $"{record.RecordNumber} - {record.RecordTitle}",
    Description = TruncateFirstLine(record.Cil, 120),
    ProjectLabel = BuildProjectLabel(record.ProjectCode, record.ProjectName),
    // ... zbytek ...
});
```

**Pro jednání** (cca řádek 488-503):
```csharp
items.Add(new DashboardNewsItemViewModel
{
    EventLabel = string.Equals(row.Action, AuditActionType.Create.ToDatabaseValue(), StringComparison.OrdinalIgnoreCase) ? "Nové jednání" : "Změna jednání",
    ActorName = ResolveActorName(row.ActorOsobaId),
    Title = $"Jednání č. {meeting.MeetingNumber}",
    Description = $"{meeting.Date:dd.MM.yyyy} v {meeting.StartTime:HH\\:mm}",
    ProjectLabel = BuildProjectLabel(meeting.ProjectCode, meeting.ProjectName),
    // ... zbytek ...
});
```

Pozor: pokud dotaz na komentář/záznam neobsahuje `.Text` / `.Cil` ve select projekci, doplnit je do projekce (existing `comment` / `record` anonymous type). Ověřit při implementaci.

- [ ] **Step 8.7: Update _DashboardNewsList.cshtml — render Actor**

V `PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml` upravit `dashboard-item-meta` sekci:

```razor
@foreach (var item in Model)
{
    <a class="dashboard-news-item" href="@item.DetailUrl">
        <div class="dashboard-news-item-top">
            <span class="dashboard-news-label">@item.EventLabel</span>
            <time datetime="@item.CreatedAt.ToString("O")">@item.CreatedAt.ToString("dd.MM.yyyy HH:mm")</time>
        </div>
        <div class="dashboard-news-title">@item.Title</div>
        @if (!string.IsNullOrWhiteSpace(item.Description))
        {
            <p class="dashboard-item-description">@item.Description</p>
        }
        <div class="dashboard-item-meta">
            @if (!string.IsNullOrWhiteSpace(item.ActorName))
            {
                <span class="dashboard-news-actor">@item.ActorName</span>
            }
            @if (!string.IsNullOrWhiteSpace(item.ProjectLabel))
            {
                <span class="dashboard-news-project">@item.ProjectLabel</span>
            }
        </div>
    </a>
}
```

- [ ] **Step 8.8: Run + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

```bash
git add PmTracker.Web/Models/ViewModels/HomeViewModels.cs \
        PmTracker.Web/Services/Dashboard/DashboardService.cs \
        PmTracker.Web/Views/Dashboard/_DashboardNewsList.cshtml \
        PmTracker.Tests.Unit/Layout/DashboardNewsEnrichmentTests.cs
git commit -m "feat(dashboard): news enrichment — ActorName + description context (úprava #5)

DashboardNewsItemViewModel.ActorName (new). BuildNewsItemsAsync batch
resolve jmen přes Osoby DbSet.

Description per event type:
- Komentář → prvních 120 znaků textu (místo redundantního 'Záznam v projektu X')
- Záznam → prvních 120 znaků Cíl
- Jednání → datum + čas (bez duplicity ProjectLabel)

Template _DashboardNewsList.cshtml renderuje ActorName + ProjectLabel
v meta řádku."
```

---

## Task 9: B4 — Icon buttons (Upravit / Navrhnout / Smazat)

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (icon button variants)
- Create: `PmTracker.Tests.Unit/Layout/DashboardActionButtonsTests.cs`

- [ ] **Step 9.1: Write failing test**

Vytvořit `PmTracker.Tests.Unit/Layout/DashboardActionButtonsTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #6 (2026-04-20): textová tlačítka "Upravit", "Smazat",
/// "Navrhnout termín a harmonogram" → icon-only (gov-icon) + aria-label + title.
/// </summary>
public sealed class DashboardActionButtonsTests
{
    [Fact]
    public void ZaznamPartial_EditButton_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));
        view.Should().Contain("gov-icon name=\"pencil\"",
            "Upravit button má gov-icon pencil");
        view.Should().Contain("aria-label=\"@summary.EditButtonLabel\"",
            "aria-label zachovává původní text pro accessibility");
    }

    [Fact]
    public void ZaznamPartial_ProposeButton_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));
        view.Should().Contain("gov-icon name=\"calendar-clock\"",
            "Navrhnout termín má gov-icon calendar-clock");
        view.Should().Contain("aria-label=\"Navrhnout termín a harmonogram\"");
    }

    [Fact]
    public void CommentsPartial_EditDeleteButtons_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml"));
        view.Should().Contain("gov-icon name=\"pencil\"",
            "Upravit vyjádření = icon pencil");
        view.Should().Contain("gov-icon name=\"trash\"",
            "Smazat vyjádření = icon trash");
        view.Should().NotMatchRegex(
            @">Upravit<|>Smazat<",
            "žádný text label mimo aria-label (ikona + tooltip)");
    }
}
```

- [ ] **Step 9.2: Run — expect FAIL**

- [ ] **Step 9.3: Update _ZaznamPartial.cshtml**

V `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml` najít blok s Upravit + Navrhnout buttons (cca řádky 75-95) a nahradit:

```razor
@if (summary.CanEdit && !string.IsNullOrWhiteSpace(editRecordUrl))
{
    <pm-button variant="Secondary"
               size="Small"
               data-record-editor-url="@editRecordUrl"
               data-record-editor-project-id="@projektId"
               data-record-editor-label="@($"{summary.EditButtonLabel} záznam #{summary.CisloViditelne}")"
               data-stop-propagation="true"
               aria-label="@summary.EditButtonLabel"
               title="@summary.EditButtonLabel">
        <gov-icon name="pencil" type="basic" aria-hidden="true"></gov-icon>
    </pm-button>
}
@if (summary.CanCreateScheduleProposal && !string.IsNullOrWhiteSpace(summary.ScheduleProposalUrl))
{
    <pm-button variant="Secondary"
               size="Small"
               data-record-editor-url="@summary.ScheduleProposalUrl"
               data-record-editor-project-id="@projektId"
               data-record-editor-label="@($"Navrhnout změnu termínu a harmonogramu záznamu #{summary.CisloViditelne}")"
               data-stop-propagation="true"
               aria-label="Navrhnout termín a harmonogram"
               title="Navrhnout termín a harmonogram">
        <gov-icon name="calendar-clock" type="basic" aria-hidden="true"></gov-icon>
    </pm-button>
}
```

- [ ] **Step 9.4: Update _ZaznamCommentsPartial.cshtml**

V `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml` najít řádek 74 + 80 (Upravit / Smazat vyjádření) a nahradit:

Před (ř. 74):
```razor
<pm-button variant="Secondary" size="Small" data-comment-edit-toggle="true" aria-label="Upravit vyjádření">Upravit</pm-button>
```

Po:
```razor
<pm-button variant="Secondary" size="Small" data-comment-edit-toggle="true" aria-label="Upravit vyjádření" title="Upravit vyjádření">
    <gov-icon name="pencil" type="basic" aria-hidden="true"></gov-icon>
</pm-button>
```

Před (ř. 80):
```razor
<pm-button variant="Destructive" size="Small" native-type="submit" aria-label="Smazat vyjádření">Smazat</pm-button>
```

Po:
```razor
<pm-button variant="Destructive" size="Small" native-type="submit" aria-label="Smazat vyjádření" title="Smazat vyjádření">
    <gov-icon name="trash" type="basic" aria-hidden="true"></gov-icon>
</pm-button>
```

- [ ] **Step 9.5: Add CSS for icon-only pm-button compact spacing**

V `PmTracker.Web/wwwroot/css/site.css` najít block definující `pm-button` spacing (nebo přidat na konec):

```css
/* Úprava #6 (2026-04-20): icon-only pm-button — kompaktní padding,
   ikona vycentrovaná. Accessibility via aria-label + title. */
pm-button[size="Small"]:has(gov-icon:only-child),
pm-button[size="Small"] gov-button:has(gov-icon:only-child) {
    --gov-button-padding-inline: 8px;
    --gov-button-padding-block: 6px;
}

pm-button gov-icon {
    font-size: 16px;
    line-height: 1;
}
```

Pokud `:has` selector není podporován v target prohlížeči (IE, starší), fallback přes explicitní class `pm-button--icon-only` + manual CSS.

- [ ] **Step 9.6: Run + commit**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

```bash
git add PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml \
        PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml \
        PmTracker.Web/wwwroot/css/site.css \
        PmTracker.Tests.Unit/Layout/DashboardActionButtonsTests.cs
git commit -m "feat(ux): icon-only buttony pro Upravit/Smazat/Navrhnout (úprava #6)

Textové buttony nahrazené gov-icon (pencil, calendar-clock, trash).
Accessibility: aria-label + title (native tooltip) na každém.

Dotčené views:
- _ZaznamPartial.cshtml: edit + propose-schedule
- _ZaznamCommentsPartial.cshtml: edit + delete vyjádření

CSS: pm-button[size=Small] s gov-icon only-child → kompaktní padding."
```

---

## Task 10: Side — #10 modal width CSS var root cause dokumentace

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css`
- Modify: `docs/superpowers/specs/2026-04-20-ranni-fix-design.md`

- [ ] **Step 10.1: Investigate actual gov-dialog CSS custom properties**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
find PmTracker.Web/wwwroot/lib/gov-design-system -name "*.css" -exec grep -l "dialog" {} \; 2>/dev/null | head -5
# Najít dist CSS pro dialog:
find PmTracker.Web/wwwroot/lib/gov-design-system -name "*.css" | xargs grep -l "gov-dialog\|dialog" 2>/dev/null | head -3
# Prohledat správnou custom property:
find PmTracker.Web/wwwroot/lib/gov-design-system -name "*.css" | xargs grep -hE "--[a-z-]*dialog[a-z-]*(width|max-width)" 2>/dev/null | sort -u
```

Zapsat výsledek (jaké `--*-dialog-*-width` CSS vars skutečně existují) jako komentář do CSS.

- [ ] **Step 10.2: Add documentation comment to site.css**

V `PmTracker.Web/wwwroot/css/site.css` najít block `gov-dialog[data-modal-container][data-modal-variant="wide"]` (cca řádek 3953) a nahradit s dokumentačním komentářem:

```css
/* Úprava #10 INVESTIGATION (2026-04-20): aktuální varianty wide / record-editor
   nastavují --gov-dialog-max-width, ale při Playwright verifikaci v harness
   (pm-modal-harness.html) se width NEAPLIKUJE — modal zůstává v defaultní
   ~52rem šířce. Hypotéza: aktuální verze gov-design-system (4.2.9) používá
   v shadow DOM jinou custom property nebo ji vůbec nečte.

   Deferred fix: po rozhodnutí o full-width app-wide layout (Úprava #4 +
   potenciální #4b pro celou app). Viz docs/superpowers/specs/2026-04-20-ranni-fix-design.md
   sekce "Side — #10".

   Zjištěné dostupné custom properties v dist CSS [vyplnit po Step 10.1]:
   --TODO: report z find + grep */
gov-dialog[data-modal-container][data-modal-variant="wide"] {
    --gov-dialog-max-width: 1100px;
}

gov-dialog[data-modal-container][data-modal-variant="record-editor"] {
    --gov-dialog-max-width: 1280px;
    --gov-dialog-max-height: 92vh;
}
```

Po Step 10.1 nahradit `--TODO: report z find + grep` aktuálním seznamem custom properties nalezených v dist CSS.

- [ ] **Step 10.3: Update design spec — vyplnit zjištění**

V `docs/superpowers/specs/2026-04-20-ranni-fix-design.md` najít sekci "Side — #10 modal width bonus" a doplnit výsledek investigace:

```markdown
## Side — #10 modal width CSS var — root cause zdokumentován

**Investigace 2026-04-20:**
V `PmTracker.Web/wwwroot/lib/gov-design-system/dist/core/**.css` jsou dostupné custom properties pro gov-dialog: [aktuální seznam]. Naše site.css používá `--gov-dialog-max-width`, ale správný název je `--[aktuální]` (zjištěno z dist CSS).

**Fix (odložený — čeká na rozhodnutí #4 full-width app):**
1. Nahradit `--gov-dialog-max-width` za `--[aktuální]` v gov-dialog variantách wide / record-editor
2. Ověřit přes Playwright harness po fixu

Scope: pure CSS change, deferred za ranní opravu.
```

- [ ] **Step 10.4: Commit documentation**

```bash
git add PmTracker.Web/wwwroot/css/site.css \
        docs/superpowers/specs/2026-04-20-ranni-fix-design.md
git commit -m "docs(modals): investigace #10 — gov-dialog CSS custom property root cause

Dokumentační commit. Aktuální --gov-dialog-max-width neaplikuje size
varianty (Playwright harness potvrdil). Zjištěno správné property name
[podle dist CSS grep]. Fix odložen na full-width app-wide rozhodnutí (#4)."
```

---

## Final validation

- [ ] **Step 11.1: Full test suite**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```
Expected: Build 0 errors, 0 warnings. Tests 523 (previous) + ~20 new = **543+ passed, 0 failed**.

- [ ] **Step 11.2: Publish refresh**

```bash
rm -rf publish publish.zip
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o publish --nologo 2>&1 | tail -5
cd publish && zip -rq ../publish.zip . && cd ..
ls -la publish.zip
```

- [ ] **Step 11.3: Playwright smoke (sandbox)**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/wwwroot"
python3 -m http.server 8766 > /tmp/static.log 2>&1 &
# V /tmp/playwright-ranni-smoke.js: ověřit modal harness (znovu) a screenshot
# check že gov-dialog attrs stále present po refactor
```

- [ ] **Step 11.4: Git log check**

```bash
git log --oneline -15
```
Expected: 9 nových commitů (Tasks 1-9) + 1 docs commit (Task 10) = 10 commits od začátku.

- [ ] **Step 11.5: Citrix manual smoke (mimo sandbox)**

User manual smoke test:
- /Jednani/Index — projekt-header click expanduje starší roky, chevron animation, ESC po expand sbalí?
- /Projekty/Detail?tab=jednani — historické roky collapsed, per-year chevron funguje
- /Dashboard — fit-on-screen, "Zobrazit více" routuje, news items mají actor
- Modal Přidat ručně + AD search — X zavírá, picker panely nad modalem, AD modal NEscrollovatelný
- Record editor modal — form scroll internal (opt-in data-modal-scrollable), X funguje přes dirty-check

---

## Exit criteria

- [x] 8 úprav (#1, #2, #4, #5, #6, #7, #8, #9) implementovány
- [x] #10 dokumentační fix, fix odložený
- [x] Build 0 errors / 0 warnings
- [x] Unit + architecture tests 543+ pass
- [x] Zero URL/route breakage
- [x] Zero consumer API breakage
- [x] 10 bisectable commitů (per úprava)
- [x] Publish refresh hotový

## Spec coverage check

| Úprava | Task | Status |
|---|---|---|
| #1 projekt-tab collapsed | Task 1 | ✅ |
| #2 project-level history toggle | Task 2 | ✅ |
| #4 dashboard full-width + non-scroll | Task 6 | ✅ |
| #4 "Zobrazit více" unify | Task 7 | ✅ |
| #5 news enrichment | Task 8 | ✅ |
| #6 icon buttons | Task 9 | ✅ |
| #7 floating portal stacking | Task 3 | ✅ |
| #8 Přidat ručně X close | Task 4 | ✅ |
| #9/1 AD search X close | Task 4 | ✅ |
| #9/2 modal overflow policy | Task 5 | ✅ |
| #10 side docs | Task 10 | ✅ |

**Out of scope:** #3 authz audit — separátní analytická fáze.
