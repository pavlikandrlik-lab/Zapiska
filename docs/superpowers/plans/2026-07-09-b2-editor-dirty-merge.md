# B2 — Editor: schedule tab nesmí zahodit rozpracovanost (merge baseline) — Implementation Plan

> **For agentic workers:** Exekuce INLINE v hlavní session (user pravidlo). **Commity DRŽET.**

**Goal:** Přepnutí na záložku Harmonogram absorbuje jen planner šum; user změny zůstanou dirty → dialogy (back/breadcrumb/Zrušit) fungují.

**Architecture:** `onScheduleTabActivated` místo plného rebuildu baseline provede MERGE: v baseline snapshotu (řazené `key=value` řádky) přepíše pouze schedule klíče (prefixy `UiHarmonogramDatumy`, `HarmonogramHodnoty`, `ScheduleVersion`) hodnotami z aktuálního stavu. Correctness definují E2E scénáře (dirty přežije / čistý zůstane čistý), ne výčet prefixů.

**Tech Stack:** ES modules (form.js/draft.js), Playwright E2E, xUnit source-assertion.

## Global Constraints
- Guard `recordEditorScheduleSnapshotRebuilt` (jen první aktivace) zůstává.
- Snapshot formát (draft.js `buildRecordEditorFormSnapshot`: sort + join) se NEMĚNÍ.
- Commity držené; odchylka od očekávání = STOP + systematic-debugging.

---

### Task 1: E2E failing testy (RED)

**Files:**
- Modify: `PmTracker.Tests.E2E/Scenarios/RecordEditorHistoryBackScenariosTests.cs` (přidat 2 testy)

**Interfaces:**
- Consumes: `WaitForTrapArmedAsync(page)`, `EditorUrl()`, `ProjectUrl()` (existují v třídě); schedule tab click = `page.Locator("pm-tabs [key='schedule']").First` (Lego tagy pm-tab-left/right sdílí `key` atribut); guard dialog `[data-record-editor-close-guard]`.

- [ ] **Step 1: Přidat testy**

```csharp
    /// <summary>B2 (2026-07-09): první aktivace schedule tabu dřív PŘESTAVĚLA dirty baseline
    /// → user změny „zmizely" a dialog nevyskočil. Merge fix: absorbovat jen planner šum.</summary>
    [Fact]
    public async Task DirtyEditor_SurvivesScheduleTabVisit_BackShowsDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectUrl());
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);

        await page.Locator("input[name='Nazev']").FillAsync("Dirty před schedule tabem");
        // Přepnout na Harmonogram a zpět na Základní (uživatelův scénář).
        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        await page.WaitForTimeoutAsync(400); // planner recalc + rAF snapshot merge
        await page.Locator("pm-tabs [key='basic']").First.ClickAsync();

        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap */ }
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1); // gov-dialog host h=0
        await page.GetByRole(AriaRole.Button, new() { Name = "Pokračovat v úpravách" }).ClickAsync();
        await page.Context.CloseAsync();
    }

    /// <summary>Regrese fixu 2026-05-05: samotná návštěva schedule tabu (bez user editu)
    /// nesmí vytvořit falešný dirty — back projde bez dialogu.</summary>
    [Fact]
    public async Task CleanEditor_ScheduleTabVisitOnly_BackLeavesWithoutDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(ProjectUrl());
        await page.GotoAsync(EditorUrl());
        await WaitForTrapArmedAsync(page);

        await page.Locator("pm-tabs [key='schedule']").First.ClickAsync();
        await page.WaitForTimeoutAsync(400);

        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { /* trap */ }
        await Assertions.Expect(page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/Projekty/Detail/.*"),
            new() { Timeout = 8000 });
        (await page.Locator("[data-record-editor-close-guard]").CountAsync()).Should().Be(0);
        await page.Context.CloseAsync();
    }
```

Pozn.: pokud selektor `pm-tabs [key='schedule']` nenajde tab (Create default kategorie není úkol?), EditorUrl vede na Create s kategorií úkol default — ověř v prohlížeči (`curl … | grep key=\"schedule\"`); kdyby tab chyběl, přidej do URL query pro úkolovou kategorii dle Create akce. STOP a nahlásit, pokud tab na Create stránce neexistuje.

- [ ] **Step 2: Run — očekávání:** `DirtyEditor_SurvivesScheduleTabVisit` **FAIL** (dialog se neukáže — baseline absorboval Nazev); `CleanEditor_ScheduleTabVisitOnly` **PASS** (dnešní chování je clean-friendly).
`dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~SurvivesScheduleTabVisit|FullyQualifiedName~ScheduleTabVisitOnly" --nologo -v q`

### Task 2: Merge implementace (GREEN)

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js` (`onScheduleTabActivated`, ~ř. 300–333)

**Interfaces:**
- Consumes: `buildRecordEditorFormSnapshot(form)` (import z `./draft.js` už ve form.js existuje — ověř; jinak přidat).
- Produces: privátní `mergeScheduleKeysIntoBaseline(baseline, current)` ve form.js.

- [ ] **Step 1: Implementace** — nahradit blok od `if (form.dataset.recordEditorScheduleSnapshotRebuilt === "true")` po konec funkce:

```javascript
    // B2 (2026-07-09): MERGE místo plného rebuildu. Plný rebuild absorboval i USER změny
    // udělané před prvním vstupem na schedule tab → form „clean" → dialogy (back/breadcrumb/
    // Zrušit) nevyskočily. Merge přepíše v baseline JEN schedule klíče (planner šum:
    // UiHarmonogramDatumy[*], HarmonogramHodnoty[*], ScheduleVersion) — ostatní user dirt přežije.
    if (form.dataset.recordEditorScheduleSnapshotRebuilt === "true") {
        return;
    }
    window.requestAnimationFrame(() => {
        if (!form.isConnected) {
            return;
        }
        const baseline = form.dataset.recordEditorSnapshot || "";
        const current = buildRecordEditorFormSnapshot(form);
        form.dataset.recordEditorSnapshot = mergeScheduleKeysIntoBaseline(baseline, current);
        form.dataset.recordEditorScheduleSnapshotRebuilt = "true";
    });
}

const scheduleSnapshotKeyPrefixes = ["UiHarmonogramDatumy", "HarmonogramHodnoty", "ScheduleVersion"];

function isScheduleSnapshotKey(line) {
    return scheduleSnapshotKeyPrefixes.some((prefix) => line.startsWith(prefix));
}

/**
 * B2: vrátí baseline, ve kterém jsou schedule řádky nahrazené schedule řádky z current.
 * Snapshot = řazené řádky `key=value` oddělené \n (buildRecordEditorFormSnapshot).
 */
function mergeScheduleKeysIntoBaseline(baseline, current) {
    const baselineLines = baseline ? baseline.split("\n") : [];
    const currentLines = current ? current.split("\n") : [];
    const merged = baselineLines.filter((line) => !isScheduleSnapshotKey(line))
        .concat(currentLines.filter((line) => isScheduleSnapshotKey(line)));
    merged.sort();
    return merged.join("\n");
}
```
Ověřit oddělovač snapshotu v draft.js (`entries.join(...)`) — pokud není `"\n"`, použít skutečný (plán předpokládá `\n`; při jiném oddělovači ho převzít 1:1).

- [ ] **Step 2: Run E2E oba testy — PASS** (dirty přežije, clean zůstane clean). Pak celá třída `RecordEditorHistoryBackScenariosTests` zelená (5 testů).

- [ ] **Step 3: Unit pin** — do `PmTracker.Tests.Unit/Layout/RecordEditorHistoryTrapTests.cs` přidat:

```csharp
    [Fact]
    public void ScheduleTabActivation_MergesBaseline_InsteadOfFullRebuild()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js");
        src.Should().Contain("mergeScheduleKeysIntoBaseline",
            "B2: plný rebuild baseline absorboval user změny → merge jen schedule klíčů");
        src.Should().Contain("scheduleSnapshotKeyPrefixes");
    }
```
Run: filter `RecordEditorHistoryTrapTests` → PASS (3 testy).

- [ ] **Step 4: Akceptační scénář 3 ze spec (ručně/skript):** dirty Nazev → schedule tab → změnit datum kroku → back ⇒ dialog (schedule USER změna vs merged baseline je dirty). Playwright skript, výsledek do reportu.

- [ ] **Step 5: Regrese** — celý E2E `RecordEditorHistoryBackScenariosTests` + build Web 0 chyb + restart app.
