# Osobní předvolby — správa po jednotlivých položkách (Implementační plán)

> **For agentic workers:** Inline execution (superpowers:executing-plans). Kroky mají `- [ ]`.
> **Commity DRŽENY** — uživatel commituje až po ručním ověření („commit až si ověřím"). Kroky „Commit" v plánu se při inline běhu vynechávají; místo nich průběžné testy.

**Goal:** Nahradit hromadné mazání osobních předvoleb spravovatelným, rozšiřitelným seznamem, kde každá uložená předvolba (formát tisku, filtry per-projekt) je řádek s vlastním „smazat".

**Architecture:** Klientský registr deskriptorů (`preferences/registry.js`) + render (`preferences/render.js`) na stránce Profil. Server vykreslí prázdný kontejner; obsah plní JS ze storage. Storage-key logika zůstává v `filters/projectFilter.js` (vlastník prefixu), print v `ui/print.js`.

**Tech Stack:** ASP.NET Core MVC (Razor), ESM moduly (bootstrap.js side-effect wiring), gov-design-system (gov-icon), localStorage/cookie, xUnit source-assertion (Unit) + Playwright (E2E).

Spec: `docs/superpowers/specs/2026-07-05-personal-preferences-management-design.md`

## Global Constraints

- Jen klientské úložiště (localStorage); žádná DB migrace; offline.
- Nový ESM kód musí být explicitně importován z `bootstrap.js` (jinak tichý fail — `project_bundle_sync`).
- gov-design-system: „smazat" = `<gov-icon size="s" name="trash" type="components" aria-hidden="true">`.
- České UI texty. Žádné „Smazat vše".
- Fix at source, enumeruj sourozence grepem, TDD (červený test první).

---

## File Structure

- **Create** `PmTracker.Web/wwwroot/js/modules/preferences/registry.js` — pole deskriptorů + oba deskriptory (printFormat, projectFilters).
- **Create** `PmTracker.Web/wwwroot/js/modules/preferences/render.js` — `initPersonalPreferences()`: render seznamu, delegované mazání, status, prázdný stav.
- **Modify** `PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js` — label companion v `saveProjectFilterDefaults`; nové `listSavedProjectFilterPreferences()`, `removeProjectFilterPreference(projectId)`; odstranit `clearProjectFilterPreferenceStorage`.
- **Modify** `PmTracker.Web/wwwroot/js/modules/bootstrap.js` — import+init `initPersonalPreferences`; odstranit import `clearProjectFilterPreferenceStorage` a dvě reset větve v `handleDocumentClick`.
- **Modify** `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml` — `data-project-zkratka`.
- **Modify** `PmTracker.Web/Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs` — `Zkratka`.
- **Modify** `PmTracker.Web/Services/ProjectService.FilterShell.cs` — naplnit `Zkratka`.
- **Modify** `PmTracker.Web/Views/Profil/Index.cshtml` — dvě karty → jedna sekce „Předvolby".
- **Modify** `PmTracker.Web/wwwroot/css/site.css` — `.preferences-list`, `.preference-row`, `.preference-row-remove`.
- **Create** `PmTracker.Tests.Unit/Profile/PersonalPreferencesTests.cs` — source-assertion.
- **Modify** `PmTracker.Tests.E2E/Scenarios/ProjectFilterPreferencesScenariosTests.cs` — nový scénář per-item mazání.

---

## Task 1: Storage API pro per-projekt předvolby + zkratka na shellu

**Files:** projectFilter.js, _ProjectFilterShell.cshtml, ProjectFilterShellViewModel.cs, ProjectService.FilterShell.cs, PersonalPreferencesTests.cs

**Produces:** `listSavedProjectFilterPreferences() -> [{ projectId: string, label: string }]`, `removeProjectFilterPreference(projectId: string): void`, `.label` companion klíč, `data-project-zkratka` na filter-shellu.

- [ ] **Step 1 — červený Unit test** (`PmTracker.Tests.Unit/Profile/PersonalPreferencesTests.cs`):

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Profile;

public sealed class PersonalPreferencesTests
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
    public void ProjectFilterJs_ExposesPerItemApi_AndDropsBulkClear()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
        src.Should().Contain("export function listSavedProjectFilterPreferences");
        src.Should().Contain("export function removeProjectFilterPreference");
        src.Should().NotContain("clearProjectFilterPreferenceStorage",
            "hromadné mazání nahrazeno per-item (Sub-projekt 1, 2026-07-05).");
        src.Should().Contain(".label", "saveProjectFilterDefaults ukládá companion label klíč.");
    }

    [Fact]
    public void FilterShell_RendersProjectZkratka()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml");
        src.Should().Contain("data-project-zkratka=\"@Model.Zkratka\"");
    }
}
```

- [ ] **Step 2 — spusť, ověř červenou:** `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~PersonalPreferencesTests" -v q` → FAIL.

- [ ] **Step 3 — VM `Zkratka`** (`ProjectFilterShellViewModel.cs`, za `ProjektId`):

```csharp
    public int ProjektId { get; init; }

    /// <summary>Zkratka projektu — pro čitelný label uložené filtrové předvolby (Profil ▸ Předvolby).</summary>
    public string Zkratka { get; init; } = string.Empty;
```

- [ ] **Step 4 — naplnit `Zkratka`** (`ProjectService.FilterShell.cs`): před `return new ProjectFilterShellViewModel` přidat lookup a do inicializátoru přidat `Zkratka`:

```csharp
        var projektZkratka = await dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == projektId)
            .Select(x => x.Zkratka)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return new ProjectFilterShellViewModel
        {
            ProjektId = projektId,
            Zkratka = projektZkratka,
            Scope = scope,
            // …stávající lookup kolekce beze změny…
```

- [ ] **Step 5 — `data-project-zkratka`** (`_ProjectFilterShell.cshtml`, řádek s `data-project-id`):

```html
<div class="filter-shell"
     data-project-filter-scope="@Model.Scope"
     data-project-id="@Model.ProjektId"
     data-project-zkratka="@Model.Zkratka">
```

- [ ] **Step 6 — projectFilter.js: label companion + per-item API + odstranit bulk.**
  Nahradit `saveProjectFilterDefaults` (respektuje `getProjectFilterRoot`/`getProjectFilterProjectId`, které v modulu existují):

```javascript
export function saveProjectFilterDefaults(scope) {
    const state = persistProjectFilterSessionState(scope);
    const key = getProjectFilterStorageKey(scope, "defaults");
    writeJsonStorage(localStorage, key, state);

    // Companion label klíč — čitelný název pro správu předvoleb (Profil ▸ Předvolby).
    const projectId = getProjectFilterProjectId(scope);
    const zkratka = (getProjectFilterRoot(scope)?.dataset.projectZkratka || "").trim();
    if (projectId && projectId !== "0" && zkratka) {
        try {
            localStorage.setItem(`${projectFilterStoragePrefix}${projectId}.label`, zkratka);
        } catch { /* storage disabled/full — label je volitelný */ }
    }

    setProjectFilterSaveStatus(scope, "Výchozí filtry uloženy v tomto prohlížeči.");
}
```

  Nahradit `clearProjectFilterPreferenceStorage` (celé tělo) těmito dvěma exporty:

```javascript
/**
 * Vyjmenuje uložené projektové filtrové předvolby (jedna položka na projekt) pro
 * správu na Profilu. Čte `${prefix}<id>.defaults` a companion `${prefix}<id>.label`.
 */
export function listSavedProjectFilterPreferences() {
    const items = [];
    try {
        for (let i = 0; i < localStorage.length; i += 1) {
            const key = localStorage.key(i);
            if (!key || !key.startsWith(projectFilterStoragePrefix) || !key.endsWith(".defaults")) {
                continue;
            }
            const projectId = key.slice(projectFilterStoragePrefix.length, -".defaults".length);
            if (!projectId) {
                continue;
            }
            const label = localStorage.getItem(`${projectFilterStoragePrefix}${projectId}.label`);
            items.push({
                projectId,
                label: label && label.trim() ? `Filtry projektu ${label.trim()}` : `Filtry projektu #${projectId}`
            });
        }
    } catch { /* storage disabled — vrať co je */ }
    return items.sort((a, b) => a.label.localeCompare(b.label, "cs"));
}

/** Smaže filtrovou předvolbu jednoho projektu (defaults + label z localStorage, state ze sessionStorage). */
export function removeProjectFilterPreference(projectId) {
    const id = String(projectId || "").trim();
    if (!id) {
        return;
    }
    try {
        localStorage.removeItem(`${projectFilterStoragePrefix}${id}.defaults`);
        localStorage.removeItem(`${projectFilterStoragePrefix}${id}.label`);
        sessionStorage.removeItem(`${projectFilterStoragePrefix}${id}.state`);
        // legacy scope-suffixed klíče téhož projektu (migrace 2026-04-30)
        ["records", "schedule"].forEach((seg) => {
            localStorage.removeItem(`${projectFilterStoragePrefix}${id}.${seg}.defaults`);
            sessionStorage.removeItem(`${projectFilterStoragePrefix}${id}.${seg}.state`);
        });
    } catch { /* storage disabled */ }
}
```

  V doc-headeru modulu (řádek ~11) nahradit `clearProjectFilterPreferenceStorage,` za `listSavedProjectFilterPreferences, removeProjectFilterPreference,`. `removeMatchingStorageKeys` zůstává (může být bez volajícího → ponechat, je to obecný helper; pokud lint hlásí unused, odstranit).

- [ ] **Step 7 — spusť Unit filtr:** oba testy z Tasku 1 PASS. `node --check` na projectFilter.js OK.

---

## Task 2: Registr deskriptorů (`preferences/registry.js`)

**Consumes:** `getStoredPrintFormat`, `clearStoredPrintFormat`, `getPrintFormatLabel` (z `../ui/print.js`); `listSavedProjectFilterPreferences`, `removeProjectFilterPreference` (z `../filters/projectFilter.js`).
**Produces:** `preferenceRegistry: Descriptor[]`, kde `Descriptor.list(): PreferenceItem[]`, `PreferenceItem = { descriptorId, itemKey, label, valueText?, remove() }`.

- [ ] **Step 1 — červený Unit test** (přidat do PersonalPreferencesTests.cs):

```csharp
    [Fact]
    public void Registry_DeclaresPrintAndProjectFilterDescriptors()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/registry.js");
        src.Should().Contain("id: \"printFormat\"");
        src.Should().Contain("id: \"projectFilters\"");
        src.Should().Contain("export const preferenceRegistry");
    }
```

- [ ] **Step 2 — ověř červenou** (soubor neexistuje) → FAIL.

- [ ] **Step 3 — vytvořit `registry.js`:**

```javascript
/**
 * preferences/registry.js — registr osobních předvoleb (klientské, localStorage).
 * Každý deskriptor umí vyjmenovat své uložené položky (0..n) a každou samostatně smazat.
 * Rozšíření = přidání deskriptoru (Sub-projekt 2 sem přidá zámek rozbaleného menu).
 */

import {
    getStoredPrintFormat,
    clearStoredPrintFormat,
    getPrintFormatLabel
} from "../ui/print.js";
import {
    listSavedProjectFilterPreferences,
    removeProjectFilterPreference
} from "../filters/projectFilter.js";

const printFormatDescriptor = {
    id: "printFormat",
    list() {
        const format = getStoredPrintFormat();
        if (!format) {
            return [];
        }
        return [{
            descriptorId: "printFormat",
            itemKey: "printFormat",
            label: "Preferovaný formát tisku",
            valueText: getPrintFormatLabel(format),
            remove: () => clearStoredPrintFormat()
        }];
    }
};

const projectFiltersDescriptor = {
    id: "projectFilters",
    list() {
        return listSavedProjectFilterPreferences().map((item) => ({
            descriptorId: "projectFilters",
            itemKey: item.projectId,
            label: item.label,
            valueText: null,
            remove: () => removeProjectFilterPreference(item.projectId)
        }));
    }
};

export const preferenceRegistry = [printFormatDescriptor, projectFiltersDescriptor];

/** Vrátí všechny aktuálně uložené položky napříč deskriptory (ploché pole). */
export function listAllPreferenceItems() {
    return preferenceRegistry.flatMap((descriptor) => descriptor.list());
}
```

- [ ] **Step 4 — Unit PASS + `node --check`.**

---

## Task 3: Render + bootstrap wiring (`preferences/render.js`, bootstrap.js)

**Consumes:** `listAllPreferenceItems`, `preferenceRegistry` (z `./registry.js`).
**Produces:** `initPersonalPreferences(): void` (idempotentní; no-op pokud `[data-preferences-list]` chybí).

- [ ] **Step 1 — červený Unit test** (přidat):

```csharp
    [Fact]
    public void Render_InitAndDelegatesRemove()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/render.js");
        src.Should().Contain("export function initPersonalPreferences");
        src.Should().Contain("data-preference-remove");
        src.Should().Contain("data-preferences-list");
    }

    [Fact]
    public void Bootstrap_WiresPreferences_AndDropsBulkResetBranches()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("initPersonalPreferences");
        src.Should().NotContain("data-project-filter-preferences-reset");
        src.Should().NotContain("data-print-preference-reset");
        src.Should().NotContain("clearProjectFilterPreferenceStorage");
    }
```

- [ ] **Step 2 — ověř červenou.**

- [ ] **Step 3 — vytvořit `render.js`:**

```javascript
/**
 * preferences/render.js — vykreslení a správa seznamu osobních předvoleb (Profil).
 * Server vykreslí prázdný [data-preferences-list]; obsah plní JS z registru.
 */

import { listAllPreferenceItems, preferenceRegistry } from "./registry.js";

let boundContainer = null;

function findItem(descriptorId, itemKey) {
    const descriptor = preferenceRegistry.find((d) => d.id === descriptorId);
    if (!descriptor) {
        return null;
    }
    return descriptor.list().find((item) => item.itemKey === itemKey) || null;
}

function setStatus(text) {
    const status = document.querySelector("[data-preferences-status]");
    if (status instanceof HTMLElement) {
        status.textContent = text || "";
    }
}

export function renderPersonalPreferences() {
    const list = document.querySelector("[data-preferences-list]");
    const empty = document.querySelector("[data-preferences-empty]");
    if (!(list instanceof HTMLElement)) {
        return;
    }

    const items = listAllPreferenceItems();
    list.textContent = "";

    if (empty instanceof HTMLElement) {
        empty.hidden = items.length > 0;
    }

    items.forEach((item) => {
        const row = document.createElement("li");
        row.className = "preference-row";

        const label = document.createElement("span");
        label.className = "preference-row-label";
        label.textContent = item.valueText ? `${item.label}: ${item.valueText}` : item.label;

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "preference-row-remove";
        remove.setAttribute("data-preference-remove", "");
        remove.setAttribute("data-preference-descriptor", item.descriptorId);
        remove.setAttribute("data-preference-item", item.itemKey);
        remove.setAttribute("aria-label", `Smazat předvolbu: ${item.label}`);
        remove.innerHTML = '<gov-icon size="s" name="trash" type="components" aria-hidden="true"></gov-icon>';

        row.append(label, remove);
        list.appendChild(row);
    });
}

export function initPersonalPreferences() {
    const list = document.querySelector("[data-preferences-list]");
    if (!(list instanceof HTMLElement)) {
        return;
    }

    renderPersonalPreferences();

    if (boundContainer === list) {
        return; // listener už navěšen (idempotence)
    }
    boundContainer = list;

    list.addEventListener("click", (event) => {
        const target = event.target instanceof Element
            ? event.target.closest("[data-preference-remove]")
            : null;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        event.preventDefault();
        const descriptorId = target.getAttribute("data-preference-descriptor") || "";
        const itemKey = target.getAttribute("data-preference-item") || "";
        const item = findItem(descriptorId, itemKey);
        if (item) {
            item.remove();
            renderPersonalPreferences();
            setStatus("Předvolba byla odstraněna.");
        }
    });
}
```

- [ ] **Step 4 — bootstrap.js wiring:**
  1. Odstranit `clearProjectFilterPreferenceStorage,` z import bloku (řádek ~55).
  2. Přidat import: `import { initPersonalPreferences } from "./preferences/render.js";` (k ostatním importům).
  3. Odstranit obě reset větve v `handleDocumentClick` (řádky ~169–185: `[data-print-preference-reset]` i `[data-project-filter-preferences-reset]`).
  4. Ověřit grepem, jestli `clearStoredPrintFormat` má v bootstrap.js jiného volajícího; pokud ne, odstranit i jeho import.
  5. Do init-listu v `bootstrapPmTrackerApp()` (kolem řádku 766) přidat: `() => initPersonalPreferences(),`.

- [ ] **Step 5 — Unit PASS (oba nové testy) + `node --check` na render.js a bootstrap.js.**

---

## Task 4: Profil view — sjednocená sekce „Předvolby"

**Files:** Profil/Index.cshtml, PersonalPreferencesTests.cs

- [ ] **Step 1 — červený Unit test** (přidat):

```csharp
    [Fact]
    public void Profil_RendersPreferencesList_AndDropsOldBulkCards()
    {
        var src = Read("PmTracker.Web/Views/Profil/Index.cshtml");
        src.Should().Contain("data-preferences-list");
        src.Should().Contain("data-preferences-empty");
        src.Should().NotContain("data-project-filter-preferences-reset");
        src.Should().NotContain("data-print-preference-reset");
    }
```

- [ ] **Step 2 — ověř červenou.**

- [ ] **Step 3 — nahradit dvě karty** (`profile-print-preference-card` + `profile-preferences-card` „Uložené filtry projektu") jednou:

```html
    <section class="card profile-preferences-card">
        <h2>Předvolby</h2>
        <p class="muted">Uloženo pouze v tomto prohlížeči na tomto počítači.</p>
        <ul class="preferences-list" data-preferences-list></ul>
        <p class="muted" data-preferences-empty hidden>Žádné uložené předvolby.</p>
        <p class="profile-preference-status" data-preferences-status aria-live="polite"></p>
    </section>
```

  (Karta „Vyhledávání (admin)" a ostatní sekce beze změny.)

- [ ] **Step 4 — Unit PASS.**

---

## Task 5: CSS + E2E + finální ověření

**Files:** site.css, ProjectFilterPreferencesScenariosTests.cs

- [ ] **Step 1 — CSS** (`site.css`, k profile stylům):

```css
.preferences-list { list-style: none; margin: 0.5rem 0 0; padding: 0; display: flex; flex-direction: column; gap: 0.25rem; }
.preference-row { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; padding: 0.35rem 0.5rem; border: 1px solid var(--pm-border); border-radius: 6px; }
.preference-row-label { min-width: 0; overflow-wrap: anywhere; }
.preference-row-remove { display: inline-flex; align-items: center; justify-content: center; padding: 0.2rem; border: none; background: transparent; color: var(--pm-text-muted); cursor: pointer; border-radius: 4px; }
.preference-row-remove:hover { color: var(--gov-color-error-500, #c52a3a); background: var(--pm-surface); }
```

- [ ] **Step 2 — E2E scénář** (nahradit tělo `Profile_ShouldClearStoredProjectFilterPreferences` — dnes ověřuje hromadné smazání, nově per-item):

```csharp
    [Fact]
    public async Task Profile_ShouldRemoveSingleProjectFilterPreference_LeavingOthers()
    {
        var page = await _fixture.NewPageAsync();

        // Ulož výchozí filtry pro aktuální projekt (jedna položka).
        await page.GotoAsync(ProjectDetailUrl("zaznamy"));
        var recordsShell = page.Locator("[data-project-filter-scope='records']");
        await OpenFiltersAsync(recordsShell);
        await ToggleSwitchAsync(recordsShell, "Pouze aktivní záznamy");
        await recordsShell.Locator("[data-filter-save-defaults='records']").ClickAsync();
        await Expect(recordsShell.Locator("[data-filter-save-status='records']")).ToContainTextAsync("Výchozí filtry uloženy");

        // Profil → v seznamu předvoleb je řádek s tlačítkem smazat.
        await page.GotoAsync($"{_fixture.BaseUrl}/Profil?asUser={_fixture.AdminOsobaId}");
        var list = page.Locator("[data-preferences-list]");
        await Expect(list.Locator("[data-preference-remove][data-preference-descriptor='projectFilters']")).ToHaveCountAsync(1);

        await list.Locator("[data-preference-remove][data-preference-descriptor='projectFilters']").First.ClickAsync();
        await Expect(page.Locator("[data-preferences-status]")).ToContainTextAsync("odstraněna");
        await Expect(list.Locator("[data-preference-remove][data-preference-descriptor='projectFilters']")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }
```

- [ ] **Step 3 — finální ověření:**
  - `dotnet build PmTracker.sln` → 0 errors.
  - `dotnet test PmTracker.Tests.Unit` → vše zelené (vč. nových).
  - `node --check` na všech nových/změněných JS.
  - Api render: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProjectHarmonogramRenderTests|FullyQualifiedName~ExportControllerTests"` (očekávaně 3 pre-existing gantt faily, jinak zelené).
  - Grep-sken: žádné zbylé `clearProjectFilterPreferenceStorage` / `data-*-preference-reset`.
  - Code review + oprava nálezů.

---

## Self-Review

**Spec coverage:** registr+deskriptory (T2), render+per-item mazání (T3), label per-projekt (T1), odstranění bulk (T1/T3/T4), Profil UI (T4), CSS+E2E (T5), testy v každém tasku. ✔
**Placeholders:** žádné — všechny kroky mají reálný kód/příkaz. ✔
**Type consistency:** `PreferenceItem` shape (`descriptorId/itemKey/label/valueText/remove`) shodné v registry.js (T2) i render.js (T3); `listSavedProjectFilterPreferences`/`removeProjectFilterPreference` shodné mezi T1 a T2. ✔
