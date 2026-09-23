# A8 — Aplikační dialog při šipce zpět + breadcrumb guard ověření — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** Šipka zpět prohlížeče na rozpracovaném stránkovém editoru vyvolá aplikační dialog „Zahodit změny?" (žádný browser dialog); návrat z breadcrumb lišty je guardovaný a pokrytý testem.

**Architecture:** pushState-trap v novém modulu `recordEditor/historyTrap.js`: na stránkovém editoru se při loadu pushne sentinel entry; `popstate` handler při dirty formu ukáže existující `promptRecordEditorDiscard`, při čistém formu propustí (`history.back()`). Browser dialog nehrozí — `beforeunload` je záměrně prázdný. Breadcrumb odkazy (`.app-breadcrumb-back/-close/-link` = obyčejné `<a href>`) kryje existující `maybeGuardOutboundNavigation` — přidává se pouze E2E test (fix jen pokud test odhalí díru).

**Upřesnění vůči spec:** trap se aktivuje **při loadu stránkového editoru** (ne až při prvním zašpinění) a popstate handler rozhoduje podle `isRecordEditorFormDirty` — stejné UX (čistý form projde bez dotazu), ale žádný race „první keystroke vs pushState" a žádný hook do dirty tracking.

**Tech Stack:** ES modules, Playwright E2E, xUnit source-assertion.

## Global Constraints
- ŽÁDNÝ nativní `beforeunload` dialog (handler zůstává prázdný).
- Dialog = existující `promptRecordEditorDiscard` (`[data-record-editor-close-guard]`, tlačítka „Pokračovat v úpravách" / „Zahodit změny") — žádný nový dialog.
- Memory `project_bundle_sync`: nový modul musí být importován z `bootstrap.js`, jinak tichý fail.
- Commity držené.

---

### Task 1: historyTrap modul + registrace

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (import + `runInitializers` položka)
- Test: `PmTracker.Tests.Unit/Layout/RecordEditorHistoryTrapTests.cs` (create)

**Interfaces:**
- Consumes: `isRecordEditorFormDirty(form)`, `promptRecordEditorDiscard(form, trigger)` (návrat `Promise<bool>` — true = odejít), `prepareRecordEditorFormNavigation` sémantika přes `form.dataset.recordEditorNavigating` (vše `./draft.js`).
- Produces: `export function initRecordEditorHistoryTrap()` — no-op mimo stránkový editor.

- [ ] **Step 1: Failing source-assertion test**

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A8 (2026-07-08): pushState-trap pro šipku zpět na stránkovém editoru.
/// Sentinel entry + popstate → aplikační dialog (promptRecordEditorDiscard);
/// beforeunload zůstává prázdný (žádný browser dialog).
/// </summary>
public sealed class RecordEditorHistoryTrapTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    [Fact]
    public void HistoryTrap_Exists_UsesDirtyCheck_AndDiscardPrompt()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js");
        src.Should().Contain("export function initRecordEditorHistoryTrap");
        src.Should().Contain("pmEditorTrap");
        src.Should().Contain("popstate");
        src.Should().Contain("isRecordEditorFormDirty");
        src.Should().Contain("promptRecordEditorDiscard");
        src.Should().NotContain("beforeunload", "nativní dialog je zakázaný");
    }

    [Fact]
    public void Bootstrap_WiresHistoryTrap()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("initRecordEditorHistoryTrap");
    }
}
```

- [ ] **Step 2: Run — FAIL** (soubor neexistuje).

- [ ] **Step 3: Modul**

```javascript
/**
 * historyTrap.js — A8 (2026-07-08): aplikační dialog při šipce zpět na STRÁNKOVÉM editoru.
 *
 * Mechanismus: při loadu editoru pushState sentinel ({pmEditorTrap}) → šipka zpět popne
 * sentinel = popstate VE STEJNÉM dokumentu (žádný browser dialog; beforeunload je záměrně
 * prázdný). Dirty form → promptRecordEditorDiscard: „Zůstat" = sentinel se obnoví,
 * „Zahodit změny" = history.back() na skutečnou předchozí stránku. Čistý form → projde rovnou.
 *
 * Limity (záměr): kryje 1 krok zpět (delší skok v historii trap obejde — best effort);
 * zavření tabu/okna nekryjeme (nativní dialog nechceme).
 */
import { isRecordEditorFormDirty, promptRecordEditorDiscard } from "./draft.js";

const trapState = { armed: false, prompting: false };

function findPageEditorForm() {
    const form = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
    return form instanceof HTMLFormElement ? form : null;
}

async function handlePopState() {
    const form = findPageEditorForm();
    if (!form || form.dataset.recordEditorNavigating === "true") {
        // Editor pryč / navigace už schválená — propustit dál.
        window.history.back();
        return;
    }

    if (!isRecordEditorFormDirty(form)) {
        window.history.back();
        return;
    }

    if (trapState.prompting) {
        // Reentrance (další back během otevřeného dialogu) — obnovit sentinel a nechat dialog být.
        window.history.pushState({ pmEditorTrap: true }, "", window.location.href);
        return;
    }

    trapState.prompting = true;
    try {
        // promptRecordEditorDiscard při „Zahodit změny" interně nastaví
        // recordEditorNavigating=true (prepareRecordEditorFormNavigation).
        const shouldLeave = await promptRecordEditorDiscard(form, null);
        if (shouldLeave) {
            window.history.back();
        } else {
            window.history.pushState({ pmEditorTrap: true }, "", window.location.href);
        }
    } finally {
        trapState.prompting = false;
    }
}

export function initRecordEditorHistoryTrap() {
    if (trapState.armed || !findPageEditorForm()) {
        return;
    }
    trapState.armed = true;
    window.history.pushState({ pmEditorTrap: true }, "", window.location.href);
    window.addEventListener("popstate", handlePopState);
}
```

- [ ] **Step 4: bootstrap.js** — import `import { initRecordEditorHistoryTrap } from "./recordEditor/historyTrap.js";` (k ostatním recordEditor importům) + do `runInitializers([...])` přidat `() => initRecordEditorHistoryTrap(),`.

- [ ] **Step 5: Run test — PASS** + build + restart app.

- [ ] **Step 6: Interakce s existujícím flow (ruční ověření v prohlížeči, Playwright skript):**
  - „Zrušit a vrátit se" tlačítko: po potvrzení odchodu jde přes `recordEditorNavigating=true` → náš popstate handler propouští ✓ (ověřit, že cílová stránka je správná — kvůli sentinelu může být potřeba `history.go(-2)`? NE: tlačítko naviguje přes `location.assign`, sentinel zůstane v historii jako duplicitní entry téže URL — následná šipka zpět z cílové stránky se vrátí na editor (čistý → OK). Změřit reálné chování a zapsat do reportu; pokud UX vadí (dvojitý back), doplnit v handleru `prepareRecordEditorFormNavigation` větev s `history.go(-2)` — jen po důkazu.)

### Task 2: E2E scénáře (šipka zpět + breadcrumb)

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/RecordEditorHistoryBackScenariosTests.cs` (create)

**Interfaces:**
- Consumes: E2E fixture; editor `/Zaznamy/Create?projektId={ProjectId}&asUser={AdminOsobaId}`; dirty = vyplnit input `name="Nazev"`; dialog `[data-record-editor-close-guard]`; tlačítka dle textu „Pokračovat v úpravách" / „Zahodit změny"; breadcrumb `.app-breadcrumb-back`.

- [ ] **Step 1: Testy**

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>A8 (2026-07-08): dirty stránkový editor — šipka zpět i breadcrumb ← vyvolají
/// aplikační dialog; čistý editor projde bez dotazu; žádný nativní browser dialog.</summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordEditorHistoryBackScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public RecordEditorHistoryBackScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private string EditorUrl() => $"{_fixture.BaseUrl}/Zaznamy/Create?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}";
    private string ProjectUrl() => $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}";
    private static ILocatorAssertions Expect(ILocator l) => Assertions.Expect(l);

    [Fact]
    public async Task DirtyEditor_BrowserBack_ShowsAppDialog_StayAndLeaveWork()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectUrl());          // reálná předchozí stránka v historii
        await page.GotoAsync(EditorUrl());
        await page.Locator("input[name='Nazev']").FillAsync("Dirty test A8");

        await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 5000 })
            .ContinueWith(_ => { });                 // GoBack nemusí navigovat (trap) — nečekat na load
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToBeVisibleAsync();

        // „Zůstat" → dialog pryč, pořád na editoru, trap obnoven.
        await page.GetByText("Pokračovat v úpravách").ClickAsync();
        page.Url.Should().Contain("/Zaznamy/Create");

        // Druhý pokus → „Zahodit změny" → odejde na předchozí stránku.
        await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 5000 })
            .ContinueWith(_ => { });
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToBeVisibleAsync();
        await page.GetByText("Zahodit změny").ClickAsync();
        await page.WaitForURLAsync("**/Projekty/Detail/**", new() { Timeout = 8000 });
    }

    [Fact]
    public async Task CleanEditor_BrowserBack_LeavesWithoutDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectUrl());
        await page.GotoAsync(EditorUrl());
        await page.GoBackAsync();
        await page.WaitForURLAsync("**/Projekty/Detail/**", new() { Timeout = 8000 });
        (await page.Locator("[data-record-editor-close-guard]").CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DirtyEditor_BreadcrumbBack_ShowsAppDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(EditorUrl());
        await page.Locator("input[name='Nazev']").FillAsync("Dirty breadcrumb A8");
        await page.Locator(".app-breadcrumb-back").ClickAsync();
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToBeVisibleAsync();
        page.Url.Should().Contain("/Zaznamy/Create", "guard musí zastavit navigaci");
        await page.GetByText("Pokračovat v úpravách").ClickAsync();
    }
}
```

- [ ] **Step 2: Run** `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~RecordEditorHistoryBackScenariosTests" -v q --nologo`.
  - `DirtyEditor_BreadcrumbBack` může projít UŽ PŘED Task 1 (existující guard) — to je OK (test = pojistka userova bodu). Pokud FAILne, je to reálná díra → systematic-debugging v `maybeGuardOutboundNavigation` (pravděpodobný kandidát: časnější `return` větev delegovaného handleru) a fix u zdroje.
  - GoBack testy: pokud Playwright `GoBackAsync` s trapem timeoutuje jinak, než plán čeká, upravit čekání (`WaitForTimeoutAsync(500)` + assert dialogu) — chování (dialog, URL) je akceptační, ne přesná Playwright sémantika.

- [ ] **Step 3: Regrese** — celý E2E projekt zelený (`MeetingModalPickerPositionScenariosTests`, `ProjectMenuOverflowScenariosTests`, `ModalCloseXScenariosTests` z A7, `ProjectFilterPreferencesScenariosTests`).

- [ ] **Step 4: Ruční scénář po uložení** (Playwright skript): vyplnit povinná pole → uložit → redirect na detail → šipka zpět → NESMÍ skončit na „mrtvém" sentinelu (přejde na editor/checkpoint dle historie; editor po loadu čistý → případný další back projde). Výsledek do reportu; úprava (`location.replace` po save) jen po důkazu problému.
