# Projektové menu — 3 + overflow se zamykatelným rozbalením (Implementační plán)

> **For agentic workers:** Inline execution (superpowers:executing-plans). Kroky mají `- [ ]`.
> **Commity DRŽENY** — uživatel commituje až po ručním ověření. Kroky „Commit" se při inline běhu vynechávají; místo nich průběžné testy.

**Goal:** Rozdělit projektovou lištu na 3 primární záložky (Záznamy·Harmonogram·Jednání) + overflow „+" (Osoby·Návrhy·Dashboard), s volitelným trvalým rozbalením přes zámeček-přepínač (cookie), odemykatelným i v Předvolbách.

**Architecture:** Server čte cookie `pmtracker.projectMenu.locked` a renderuje variantu A (3+„+" popover) nebo B (6 inline). Zámeček-přepínač ve spodní řadě je v obou stavech; klik = zápis/smaz cookie + reload (server = zdroj layoutu). Odemčení i z Předvoleb (SP1 registr) → cookie ze dvou zdrojů přes sdílený `preferences/cookie.js`.

**Tech Stack:** ASP.NET Core MVC (Razor), ESM (bootstrap.js side-effect), gov-icon (Bootstrap Icons), cookie, xUnit (Unit source-assertion + Api render s cookie headerem) + Playwright.

Spec: `docs/superpowers/specs/2026-07-05-project-menu-overflow-lock-design.md`

## Global Constraints

- Progressive enhancement: záložky zůstávají `<a class="tab" data-tab href>` (server-nav i bez JS).
- Cookie: klientský zápis (`document.cookie … Path=/; SameSite=Lax`), serverové čtení (`Request.Cookies`).
- Přepnutí zámku = cookie + `location.reload()` (server = zdroj layoutu, minimum client logiky).
- Nový ESM modul side-effect importovaný z `bootstrap.js` (`project_bundle_sync`).
- gov-icon = Bootstrap Icons self-hosted v `wwwroot/assets/icons/components/` (`feedback_gov_icons_are_bootstrap`) — `lock`/`unlock` je nutné doplnit.
- Práva Návrhy/Dashboard beze změny. České texty. TDD (červený test první).

---

## File Structure

- **Create** `wwwroot/assets/icons/components/lock.svg`, `unlock.svg` — Bootstrap Icons 1.11.3.
- **Create** `wwwroot/js/modules/preferences/cookie.js` — `readCookie/writeCookie/deleteCookie`.
- **Create** `wwwroot/js/modules/projectMenu.js` — `initProjectMenuOverflow()` (popover + lock toggle).
- **Modify** `Models/ViewModels/Projekty/ProjektDetailViewModels.cs` — `bool ProjectMenuLocked`.
- **Modify** `Controllers/ProjektyController.cs` — číst cookie v `PrepareProjectDetailPresentationAsync`.
- **Modify** `Views/Projekty/Detail.cshtml` — tab strip A/B + lockrow.
- **Modify** `wwwroot/js/modules/preferences/registry.js` — `menuLockDescriptor`.
- **Modify** `wwwroot/js/modules/bootstrap.js` — import+init `initProjectMenuOverflow`.
- **Modify** `wwwroot/css/site.css` — overflow/popover/lockrow/hover-swap.
- **Create** `PmTracker.Tests.Api/Controllers/ProjectMenuOverflowRenderTests.cs`.
- **Create** `PmTracker.Tests.Unit/Projects/ProjectMenuOverflowTests.cs`.
- **Modify** `PmTracker.Tests.E2E/Scenarios/` — nový scénář lock-flow (nový soubor).

---

## Task 1: Foundations — ikony + cookie helper

**Produces:** `lock.svg`, `unlock.svg`; `readCookie(name)`, `writeCookie(name,value,maxAgeSeconds)`, `deleteCookie(name)`.

- [ ] **Step 1 — červený Unit test** (`PmTracker.Tests.Unit/Projects/ProjectMenuOverflowTests.cs`):

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProjectMenuOverflowTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));
    private static bool Exists(string rel) => File.Exists(Path.Combine(RepoRoot(), rel));

    [Fact]
    public void LockIcons_Exist()
    {
        Exists("PmTracker.Web/wwwroot/assets/icons/components/lock.svg").Should().BeTrue();
        Exists("PmTracker.Web/wwwroot/assets/icons/components/unlock.svg").Should().BeTrue();
    }

    [Fact]
    public void CookieHelper_ExportsReadWriteDelete()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/cookie.js");
        src.Should().Contain("export function readCookie");
        src.Should().Contain("export function writeCookie");
        src.Should().Contain("export function deleteCookie");
        src.Should().Contain("SameSite=Lax");
    }
}
```

- [ ] **Step 2 — ověř červenou:** `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ProjectMenuOverflowTests" -v q` → FAIL.

- [ ] **Step 3 — `lock.svg`** (formát dle `plus.svg`, Bootstrap Icons 1.11.3):

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" class="bi bi-lock" viewBox="0 0 16 16">
  <path d="M8 1a2 2 0 0 1 2 2v4H6V3a2 2 0 0 1 2-2m3 6V3a3 3 0 0 0-6 0v4a2 2 0 0 0-2 2v5a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2M5 8h6a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1"/>
</svg>
```

- [ ] **Step 4 — `unlock.svg`:**

```svg
<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" class="bi bi-unlock" viewBox="0 0 16 16">
  <path d="M11 1a2 2 0 0 0-2 2v4a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2h5V3a3 3 0 0 1 6 0v4a.5.5 0 0 1-1 0V3a2 2 0 0 0-2-2M3 8a1 1 0 0 0-1 1v5a1 1 0 0 0 1 1h6a1 1 0 0 0 1-1V9a1 1 0 0 0-1-1z"/>
</svg>
```

- [ ] **Step 5 — `preferences/cookie.js`:**

```javascript
/** preferences/cookie.js — mini cookie helper (Path=/, SameSite=Lax). Sdílí projectMenu + preferences. */

export function readCookie(name) {
    const target = `${name}=`;
    for (const entry of document.cookie.split(";")) {
        const trimmed = entry.trim();
        if (trimmed.startsWith(target)) {
            return decodeURIComponent(trimmed.slice(target.length));
        }
    }
    return null;
}

export function writeCookie(name, value, maxAgeSeconds) {
    document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; Max-Age=${maxAgeSeconds}; SameSite=Lax`;
}

export function deleteCookie(name) {
    document.cookie = `${name}=; Path=/; Max-Age=0; SameSite=Lax`;
}
```

- [ ] **Step 6 — Unit PASS + `node --check preferences/cookie.js`.**

---

## Task 2: Serverem řízený render menu (A/B) — VM + controller + Detail.cshtml

**Consumes:** cookie `pmtracker.projectMenu.locked`.
**Produces:** `ProjektDetailViewModel.ProjectMenuLocked`; markup s `data-project-menu-toggle`, `data-project-menu-popover`, `data-project-menu-lock-toggle`.

- [ ] **Step 1 — červený Api render test** (`PmTracker.Tests.Api/Controllers/ProjectMenuOverflowRenderTests.cs`):

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectMenuOverflowRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ProjectMenuOverflowRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetDetailAsync(bool locked)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        if (locked)
        {
            client.DefaultRequestHeaders.Add("Cookie", "pmtracker.projectMenu.locked=1");
        }
        var response = await client.GetAsync($"/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Unlocked_RendersOverflowToggle_AndLockToggle()
    {
        var html = await GetDetailAsync(locked: false);
        html.Should().Contain("data-project-menu-toggle");
        html.Should().Contain("data-project-menu-popover");
        html.Should().Contain("data-project-menu-lock-toggle");
    }

    [Fact]
    public async Task Locked_RendersInlineSecondary_NoOverflowToggle_ButLockToggle()
    {
        var html = await GetDetailAsync(locked: true);
        html.Should().NotContain("data-project-menu-toggle");
        html.Should().NotContain("data-project-menu-popover");
        html.Should().Contain("data-tab=\"tym\"");
        html.Should().Contain("data-project-menu-lock-toggle");
    }
}
```

- [ ] **Step 2 — ověř červenou:** `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ProjectMenuOverflowRenderTests" -v q` → FAIL.

- [ ] **Step 3 — VM flag** (`ProjektDetailViewModels.cs`, za `CanViewDashboard`):

```csharp
    public bool CanViewDashboard { get; set; }

    /// <summary>Trvale rozbalené projektové menu (cookie pmtracker.projectMenu.locked). Řídí A/B render lišty.</summary>
    public bool ProjectMenuLocked { get; set; }
```

- [ ] **Step 4 — controller** (`ProjektyController.cs`, v `PrepareProjectDetailPresentationAsync` za řádek `model.CanViewDashboard = …`):

```csharp
        model.CanViewDashboard = CurrentUserContext.HasPermission(PermissionKeys.DashboardView, projectId);
        // Trvale rozbalené menu — cookie čtená serverově (render bez fliknutí), zapisovaná klientsky.
        model.ProjectMenuLocked = Request.Cookies["pmtracker.projectMenu.locked"] == "1";
```

- [ ] **Step 5 — Detail.cshtml.** Nahradit blok sekundárních záložek (Osoby/Návrhy/Dashboard, dnes za primárními) tímto A/B blokem a doplnit lockrow. Primární 3 (Záznamy/Harmonogram/Jednání) zůstávají beze změny. Nahraď od `<a class="tab … data-tab="tym">` … po konec `@if (Model.CanViewDashboard) { … Dashboard … }`:

```html
    @{
        var secondaryActive = Model.ActiveTab == "tym" || Model.ActiveTab == "navrhy";
    }
    @if (Model.ProjectMenuLocked)
    {
        <a class="tab @(Model.ActiveTab == "tym" ? "active" : null)" id="tab-tym" href="@teamTabUrl"
           role="tab" aria-selected="@(Model.ActiveTab == "tym" ? "true" : "false")" aria-controls="panel-tym" data-tab="tym">
            Osoby (tým)
        </a>
        @if (Model.CanViewProposals)
        {
            <a class="tab @(Model.ActiveTab == "navrhy" ? "active" : null)" id="tab-navrhy" href="@proposalsTabUrl"
               role="tab" aria-selected="@(Model.ActiveTab == "navrhy" ? "true" : "false")" aria-controls="panel-navrhy" data-tab="navrhy">
                Návrhy
            </a>
        }
        @if (Model.CanViewDashboard)
        {
            <a class="tab" role="tab" aria-selected="false"
               href="@Url.Action("Index", "ProjectDashboard", new { projektId = Model.Projekt.Id })">
                Dashboard
            </a>
        }
    }
    else
    {
        <div class="tab-overflow" data-project-menu-overflow>
            <button type="button" class="tab tab-overflow-toggle @(secondaryActive ? "active" : null)"
                    data-project-menu-toggle aria-haspopup="menu" aria-expanded="false" aria-label="Další záložky">
                <gov-icon size="s" name="plus" type="components" aria-hidden="true"></gov-icon>
            </button>
            <div class="tab-overflow-menu" role="menu" data-project-menu-popover hidden="hidden">
                <a class="tab @(Model.ActiveTab == "tym" ? "active" : null)" id="tab-tym" href="@teamTabUrl"
                   role="menuitem" aria-controls="panel-tym" data-tab="tym"
                   aria-current="@(Model.ActiveTab == "tym" ? "page" : null)">Osoby (tým)</a>
                @if (Model.CanViewProposals)
                {
                    <a class="tab @(Model.ActiveTab == "navrhy" ? "active" : null)" id="tab-navrhy" href="@proposalsTabUrl"
                       role="menuitem" aria-controls="panel-navrhy" data-tab="navrhy"
                       aria-current="@(Model.ActiveTab == "navrhy" ? "page" : null)">Návrhy</a>
                }
                @if (Model.CanViewDashboard)
                {
                    <a class="tab" role="menuitem"
                       href="@Url.Action("Index", "ProjectDashboard", new { projektId = Model.Projekt.Id })">Dashboard</a>
                }
            </div>
        </div>
    }
```

  Před uzavírací `</div>` tab stripu (za blok `<div class="tabs-actions">…</div>`) přidat lockrow:

```html
    <div class="tabs-lockrow">
        <button type="button"
                class="project-menu-lock @(Model.ProjectMenuLocked ? "is-locked" : null)"
                data-project-menu-lock-toggle
                data-locked="@(Model.ProjectMenuLocked ? "1" : "0")"
                title="@(Model.ProjectMenuLocked ? "Odemknout — sbalit menu (jde i v Nastavení ▸ Předvolby)" : "Zamknout rozbalené menu")"
                aria-label="@(Model.ProjectMenuLocked ? "Odemknout projektové menu" : "Zamknout rozbalené projektové menu")">
            <gov-icon class="project-menu-lock-current" size="s" name="@(Model.ProjectMenuLocked ? "lock" : "unlock")" type="components" aria-hidden="true"></gov-icon>
            <gov-icon class="project-menu-lock-hover" size="s" name="@(Model.ProjectMenuLocked ? "unlock" : "lock")" type="components" aria-hidden="true"></gov-icon>
        </button>
    </div>
```

- [ ] **Step 6 — ověř zeleně:** `dotnet build PmTracker.sln` → 0 errors; Api render test PASS (oba).

---

## Task 3: Klientské chování — projectMenu.js + bootstrap wiring

**Consumes:** `readCookie/writeCookie/deleteCookie` (`./preferences/cookie.js`).
**Produces:** `initProjectMenuOverflow()`.

- [ ] **Step 1 — červený Unit test** (přidat do ProjectMenuOverflowTests.cs):

```csharp
    [Fact]
    public void ProjectMenuJs_HasOverflowAndLockBehavior()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/projectMenu.js");
        src.Should().Contain("export function initProjectMenuOverflow");
        src.Should().Contain("data-project-menu-toggle");
        src.Should().Contain("data-project-menu-lock-toggle");
        src.Should().Contain("location.reload");
        src.Should().Contain("Escape");
    }

    [Fact]
    public void Bootstrap_WiresProjectMenuOverflow()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("initProjectMenuOverflow");
    }
```

- [ ] **Step 2 — ověř červenou.**

- [ ] **Step 3 — `projectMenu.js`:**

```javascript
/**
 * projectMenu.js — overflow „+" popover na projektovém detailu + zámeček-přepínač (lock/unlock).
 * Server renderuje variantu A/B dle cookie; klient jen otevírá popover a přepíná cookie (+reload).
 */

import { readCookie, writeCookie, deleteCookie } from "./preferences/cookie.js";

const menuLockCookie = "pmtracker.projectMenu.locked";
const oneYearSeconds = 60 * 60 * 24 * 365;

function initOverflowPopover() {
    const overflow = document.querySelector("[data-project-menu-overflow]");
    if (!(overflow instanceof HTMLElement)) {
        return;
    }
    const toggle = overflow.querySelector("[data-project-menu-toggle]");
    const popover = overflow.querySelector("[data-project-menu-popover]");
    if (!(toggle instanceof HTMLElement) || !(popover instanceof HTMLElement)) {
        return;
    }

    const close = () => {
        popover.hidden = true;
        toggle.setAttribute("aria-expanded", "false");
    };

    toggle.addEventListener("click", (event) => {
        event.preventDefault();
        const willOpen = popover.hidden;
        popover.hidden = !willOpen;
        toggle.setAttribute("aria-expanded", String(willOpen));
    });

    // Výběr sekundární záložky zavře popover (in-page přepnutí řeší projectTabs.js).
    popover.addEventListener("click", (event) => {
        if (event.target instanceof Element && event.target.closest("[data-tab], a")) {
            close();
        }
    });

    document.addEventListener("click", (event) => {
        if (event.target instanceof Node && !overflow.contains(event.target)) {
            close();
        }
    });
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            close();
        }
    });
}

function initLockToggle() {
    const lock = document.querySelector("[data-project-menu-lock-toggle]");
    if (!(lock instanceof HTMLElement)) {
        return;
    }
    lock.addEventListener("click", (event) => {
        event.preventDefault();
        if (readCookie(menuLockCookie) === "1") {
            deleteCookie(menuLockCookie);
        } else {
            writeCookie(menuLockCookie, "1", oneYearSeconds);
        }
        window.location.reload();
    });
}

export function initProjectMenuOverflow() {
    initOverflowPopover();
    initLockToggle();
}
```

- [ ] **Step 4 — bootstrap.js:** přidat import `import { initProjectMenuOverflow } from "./projectMenu.js";` (k ostatním importům) a do init-listu v `bootstrapPmTrackerApp()` (u `() => initProjectTabs()` apod.) přidat `() => initProjectMenuOverflow(),`.

- [ ] **Step 5 — Unit PASS + `node --check projectMenu.js bootstrap.js`.**

---

## Task 4: Předvolby hook — menuLockDescriptor

**Consumes:** `readCookie/deleteCookie` (`./cookie.js`).
**Produces:** třetí deskriptor v `preferenceRegistry`.

- [ ] **Step 1 — červený Unit test** (přidat do ProjectMenuOverflowTests.cs):

```csharp
    [Fact]
    public void Registry_DeclaresMenuLockDescriptor()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/registry.js");
        src.Should().Contain("id: \"menuLock\"");
        src.Should().Contain("Trvale rozbalené projektové menu");
    }
```

- [ ] **Step 2 — ověř červenou.**

- [ ] **Step 3 — `registry.js`:** přidat import a deskriptor, rozšířit pole. Nahoru přidat:

```javascript
import { readCookie, deleteCookie } from "./cookie.js";
```

  Před `export const preferenceRegistry` přidat:

```javascript
const menuLockCookie = "pmtracker.projectMenu.locked";

const menuLockDescriptor = {
    id: "menuLock",
    list() {
        if (readCookie(menuLockCookie) !== "1") {
            return [];
        }
        return [{
            descriptorId: "menuLock",
            itemKey: "menuLock",
            label: "Trvale rozbalené projektové menu",
            valueText: null,
            remove: () => deleteCookie(menuLockCookie)
        }];
    }
};
```

  A rozšířit registr:

```javascript
export const preferenceRegistry = [printFormatDescriptor, projectFiltersDescriptor, menuLockDescriptor];
```

- [ ] **Step 4 — Unit PASS + `node --check registry.js`.**

---

## Task 5: CSS + E2E + finální ověření

- [ ] **Step 1 — CSS** (`site.css`, k tab strip stylům; `.tabs` je flex kontejner — lockrow zalomíme na spodní řadu):

```css
.tabs { flex-wrap: wrap; }
.tabs-lockrow { flex-basis: 100%; display: flex; justify-content: flex-end; margin-top: 2px; }

.tab-overflow { position: relative; display: inline-flex; }
.tab-overflow-toggle { display: inline-flex; align-items: center; }
.tab-overflow-menu {
    position: absolute; top: 100%; left: 0; z-index: 50; min-width: 180px;
    display: flex; flex-direction: column; gap: 2px; padding: 4px;
    background: var(--pm-surface); border: 1px solid var(--pm-border); border-radius: 6px;
    box-shadow: 0 6px 20px rgba(0, 0, 0, 0.15);
}
.tab-overflow-menu[hidden] { display: none; }
.tab-overflow-menu .tab { white-space: nowrap; }

.project-menu-lock {
    display: inline-flex; align-items: center; justify-content: center; padding: 2px 4px;
    border: none; background: transparent; color: var(--pm-text-muted); cursor: pointer; border-radius: 4px;
}
.project-menu-lock:hover { color: var(--gov-color-primary-500, #2362a2); background: var(--pm-surface); }
.project-menu-lock-hover { display: none; }
.project-menu-lock:hover .project-menu-lock-current { display: none; }
.project-menu-lock:hover .project-menu-lock-hover { display: inline-flex; }
```

- [ ] **Step 2 — E2E scénář** (`PmTracker.Tests.E2E/Scenarios/ProjectMenuOverflowScenariosTests.cs`):

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class ProjectMenuOverflowScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ProjectMenuOverflowScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private string DetailUrl() => $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}";

    [Fact]
    public async Task Overflow_OpensPopover_AndLockSwitchesToInline()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(DetailUrl());

        // Odemčeno: „+" otevře popover s Osoby (tým).
        await page.Locator("[data-project-menu-toggle]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-project-menu-popover] [data-tab='tym']")).ToBeVisibleAsync();

        // Zámek → reload → 6 inline, „+" pryč.
        await page.Locator("[data-project-menu-lock-toggle]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-project-menu-toggle]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator(".tabs [data-tab='tym']")).ToBeVisibleAsync();

        // Zpět odemknout.
        await page.Locator("[data-project-menu-lock-toggle]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-project-menu-toggle]")).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 3 — finální ověření:**
  - `dotnet build PmTracker.sln` → 0 errors.
  - `dotnet test PmTracker.Tests.Unit` → vše zelené.
  - `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProjectMenuOverflowRenderTests"` → zelené; plus `ProjectHarmonogramRenderTests|ExportControllerTests` (očekávaně 3 pre-existing gantt faily).
  - `node --check` na všech nových/změněných JS.
  - Grep-sken: `data-project-menu-toggle`, `data-project-menu-lock-toggle`, `menuLock` konzistentní; žádné duplicitní init.
  - Code review + oprava nálezů.

---

## Self-Review

**Spec coverage:** A/B render dle cookie (T2), popover+lock toggle obousměrně (T3), spodní lockrow + hover-swap (T2 markup + T5 CSS), menuLockDescriptor 2. zdroj (T4), ikony+cookie helper (T1), práva zachována (T2 `@if`), testy v každém tasku. ✔
**Placeholders:** žádné — reálný kód/příkazy. ✔
**Type consistency:** cookie `pmtracker.projectMenu.locked` + `readCookie/writeCookie/deleteCookie` + data-atributy (`data-project-menu-toggle`/`-popover`/`-lock-toggle`) shodné napříč T1–T5; `menuLockDescriptor` shape (`descriptorId/itemKey/label/valueText/remove`) shodný se SP1. ✔
