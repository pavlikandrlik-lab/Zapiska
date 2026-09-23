# B3+B4 — Detail návrhu: read-only formulář + žádné dirty dialogy — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** Na detailu návrhu (schvalování) nejde nic editovat (vč. záložek Externí záznamy a Spolupráce) a back/breadcrumb/Zrušit nikdy neukáže dirty dialog; návrhové EDITORY (create/prefill) zůstávají plně funkční.

**Architecture:** Existující stav `Model.IsProposalDecisionDetail` konzumují: (a) `_EditZaznamForm` → atribut `data-record-editor-guard="off"` (komentář NAD tag!), (b) panely Collaboration + External → disabled/skryté akce, (c) JS: `historyTrap` skip arming + `promptRecordEditorDiscard` early-true. Decision stránka nesubmituje pole (rozhodovací commandy) → disabled bezpečné.

**Tech Stack:** Razor, ES modules, Api render testy, E2E.

## Global Constraints
- Razor komentář NIKDY uvnitř TagHelper tagu (memory: polyká atribut) — vždy nad tag.
- Návrhové EDITORY (`CreateRecordProposal`, `PrefillCreateProposal`) guard-off NEMAJÍ (edituje se v nich).
- Codes: `TypNavrhu="CREATE_RECORD"`, `Stav="PENDING"` (RecordProposalViewModels).
- Commity držené.

---

### Task 1: Api fixture — seed pending návrhu + failing render testy (RED)

**Files:**
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (přidat `EnsurePendingCreateProposalAsync`)
- Create: `PmTracker.Tests.Api/Controllers/ProposalDecisionReadonlyRenderTests.cs`

**Interfaces:**
- Consumes: fixture `EnsurePersonAsync/EnsureProjectAsync/EnsureSubsystemAsync/EnsureProjectTeamMemberAsync/EnsureSubsystemLeadAsync`, `CreateDbContext()`; entity `ZaznamNavrhEntity { ProjektId, SubsystemId, TypNavrhu, Stav, PayloadJson, CreatedByOsobaId, CreatedAt }`.
- Produces: `Task<int> EnsurePendingCreateProposalAsync(int projectId, int subsystemId, int authorOsobaId)`.

- [ ] **Step 1: Fixture helper**

```csharp
    /// <summary>B3/B4 (2026-07-09): pending návrh založení záznamu pro render testy detailu.</summary>
    public async Task<int> EnsurePendingCreateProposalAsync(int projectId, int subsystemId, int authorOsobaId)
    {
        await using var dbContext = CreateDbContext();
        var existing = await dbContext.ZaznamNavrhy
            .Where(x => x.ProjektId == projectId && x.Stav == "PENDING" && x.TypNavrhu == "CREATE_RECORD")
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (existing.HasValue)
        {
            return existing.Value;
        }

        var subsystemKod = await dbContext.Subsystemy
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .FirstAsync();
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            ProposalType = "CREATE_RECORD",
            CreateRecord = new
            {
                ProjektId = projectId,
                Kategorie = "U",
                Stav = "OPEN",
                Nazev = "Api pending návrh",
                VlastnikId = authorOsobaId,
                DatumZalozeni = DateTime.Today,
                TerminUkonceni = DateTime.Today.AddDays(30),
                Subsystem = subsystemKod,
                VybraniSpolupracovniciIds = Array.Empty<int>(),
                ExterniVazby = Array.Empty<object>(),
                HarmonogramHodnoty = Array.Empty<object>()
            }
        });

        var proposal = new ZaznamNavrhEntity
        {
            ProjektId = projectId,
            SubsystemId = subsystemId,
            TypNavrhu = "CREATE_RECORD",
            Stav = "PENDING",
            PayloadJson = payload,
            CreatedByOsobaId = authorOsobaId,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ZaznamNavrhy.Add(proposal);
        await dbContext.SaveChangesAsync();
        return proposal.Id;
    }
```
(DbSet název `ZaznamNavrhy` ověř v PmTrackerDbContext — grep `ZaznamNavrh`; JSON deserializace payloadu je case-insensitive? Ověř `DeserializePayload` options — pokud case-sensitive, anonymní objekt už PascalCase odpovídá `RecordProposalPayload` ✓.)

- [ ] **Step 2: Failing testy**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>B3+B4 (2026-07-09): detail návrhu = read-only + guard-off; návrhový EDITOR guardy drží.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProposalDecisionReadonlyRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ProposalDecisionReadonlyRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int proposalId, int leadId)> SeedAsync()
    {
        var leadId = await _fixture.EnsurePersonAsync("ApiPropDecLead");
        var projectId = await _fixture.EnsureProjectAsync("APIPROPDEC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIPROPDEC_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var proposalId = await _fixture.EnsurePendingCreateProposalAsync(projectId, subsystemId, leadId);
        return (projectId, proposalId, leadId);
    }

    [Fact]
    public async Task ProposalDetail_IsReadonly_WithGuardOff()
    {
        var (projectId, proposalId, _) = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        var html = System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().Contain("data-record-editor-guard=\"off\"", "B3: rozhodovací stránka nemá dirty guard");
        // B4 — spolupráce: checkboxy disabled, search disabled.
        html.Should().NotContain("name=\"VybraniSpolupracovniciIds\" value", "kontrolní kotva — viz asserty níže");
        html.Should().MatchRegex(@"name=""VybraniSpolupracovniciIds""[^>]*disabled");
        html.Should().MatchRegex(@"data-collab-search[^>]*disabled");
        // B4 — externí: bez add/remove akcí.
        html.Should().NotContain("data-external-add");
        html.Should().NotContain("data-external-remove");
    }

    [Fact]
    public async Task CreateProposalEditor_KeepsGuardOn()
    {
        var (projectId, _, leadId) = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/CreateRecordProposal?projektId={projectId}&asUser={leadId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-record-editor-guard=\"off\"", "v editoru návrhu se edituje — guard musí zůstat");
        html.Should().Contain("data-external-add", "editor má add akci");
    }
}
```
Pozn.: assert `NotContain(...value` + MatchRegex — checkbox checked varianta má `value=".." checked` PŘED disabled; regex kotva je name+disabled v jednom tagu. Pokud pořadí atributů nevyhoví, uprav regex na `<input[^>]*VybraniSpolupracovniciIds[^>]*disabled` (tag-scoped).

- [ ] **Step 3: Run — FAIL** (`ProposalDetail_IsReadonly` — guard-off neexistuje, disabled chybí, add tlačítko přítomné). `CreateProposalEditor_KeepsGuardOn` může projít už teď.
`dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ProposalDecisionReadonlyRenderTests" --nologo -v q`
Pokud ProposalDetail vrátí 500 na payload mapping (`ApplyCreatePayload` Stav/Kategorie očekává NÁZEV místo kódu), uprav payload v helperu na názvy z fixture lookups („Úkol", „Běží"?) dle chybové zprávy — cíl je 200; STOP pokud padá jinde.

### Task 2: View změny (guard atribut + readonly panely)

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml` (atribut na form)
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamCollaborationPanel.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml`

- [ ] **Step 1: _EditZaznamForm** — komentář NAD form (rozšířit stávající A8 komentář) + atribut:

```razor
@* … stávající A8 komentář …
   B3 (2026-07-09): data-record-editor-guard="off" na detailu návrhu (IsProposalDecisionDetail)
   — rozhodovací stránka nemá koncept rozpracovanosti; historyTrap se nearmuje a
   promptRecordEditorDiscard propouští bez dialogu. *@
```
a do form tagu (za `data-record-editor-presentation="page"`):
```razor
      data-record-editor-guard="@(Model.IsProposalDecisionDetail ? "off" : "on")"
```

- [ ] **Step 2: Collaboration panel** — na začátek souboru:
```razor
@{
    // B4 (2026-07-09): detail návrhu = čistě rozhodovací — výběr spolupracovníků je read-only.
    var collabDisabled = Model.IsProposalDecisionDetail;
}
```
search input (ř. ~8): `<input type="search" placeholder="Jméno, organizace..." data-collab-search disabled="@(collabDisabled ? "disabled" : null)" />`
oba checkbox renderery (checked i unchecked větev): přidat `disabled="@(collabDisabled ? "disabled" : null)"`.

- [ ] **Step 3: External panel** — na začátek:
```razor
@{
    // B4 (2026-07-09): detail návrhu — externí vazby read-only, žádné add/remove/chat akce.
    var externalReadonly = Model.IsProposalDecisionDetail;
}
```
- add tlačítko (ř. ~274): obalit `@if (!externalReadonly) { … }`
- remove tlačítka (`data-external-remove`, ř. ~165): obalit `@if (!externalReadonly)`
- chat tlačítka (`data-external-chat-open`, ř. ~87, ~224): obalit `@if (!externalReadonly)` (chat je akce, na detailu návrhu nepatří)
- všechny `<gov-form-input>` řádků vazeb: doplnit `disabled="@(externalReadonly ? "true" : null)"` tam, kde disabled atribut chybí (ř. ~41–48, ~58–66, ~182–189; už-disabled ponechat). Template `<template data-external-template>` netřeba (add tlačítko zmizí).
- selecty typu (pokud v souboru — grep `<select`): stejné disabled.

- [ ] **Step 4: Audit ostatních panelů** — `grep -n "disabled\|CanEdit\|AllowBasic\|IsProposalDecisionDetail" PmTracker.Web/Views/Projekty/_EditZaznam*Panel.cshtml PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml | grep -v Collaboration | grep -v External` a projít: každý interaktivní prvek musí být gatovaný (`AllowBasicMetadataEdit`/`CanEditSchedule*`/`metadataLocked`). Díry doplnit stejným vzorem (`Model.IsProposalDecisionDetail`). Výstup auditu (seznam OK/fixnuto) do reportu.

- [ ] **Step 5: Build + Api testy — `ProposalDetail_IsReadonly` PASS.**

### Task 3: JS guard-off + E2E

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js` (`promptRecordEditorDiscard`)
- Test: `PmTracker.Tests.Unit/Layout/RecordEditorHistoryTrapTests.cs` (+1 test), `PmTracker.Tests.E2E/Scenarios/ProposalDecisionScenariosTests.cs` (create)

- [ ] **Step 1: historyTrap** — v `findPageEditorForm` respektovat guard:

```javascript
function findPageEditorForm() {
    const form = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
    if (!(form instanceof HTMLFormElement)) {
        return null;
    }
    // B3 (2026-07-09): rozhodovací stránka návrhu (guard="off") nemá koncept rozpracovanosti.
    if (form.dataset.recordEditorGuard === "off") {
        return null;
    }
    return form;
}
```
(Trap se nearmuje; popstate handler přes tentýž helper propouští.)

- [ ] **Step 2: promptRecordEditorDiscard** (draft.js:441) — hned po existujícím clean-check:

```javascript
export function promptRecordEditorDiscard(form, trigger) {
    if (!(form instanceof HTMLFormElement) || !isRecordEditorFormDirty(form)) {
        return Promise.resolve(true);
    }
    // B3 (2026-07-09): guard="off" (detail návrhu) — žádný dialog, rovnou propustit.
    if (form.dataset.recordEditorGuard === "off") {
        return Promise.resolve(true);
    }
```
Tím jsou pokryté Zrušit (requestRecordEditorPageCancel) i outbound guard (maybeGuardOutboundNavigation volá tentýž prompt).

- [ ] **Step 3: Unit pin** (do RecordEditorHistoryTrapTests):

```csharp
    [Fact]
    public void GuardOff_IsRespected_ByTrapAndDiscardPrompt()
    {
        Read("PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js")
            .Should().Contain("recordEditorGuard === \"off\"");
        Read("PmTracker.Web/wwwroot/js/modules/recordEditor/draft.js")
            .Should().Contain("recordEditorGuard === \"off\"");
    }
```

- [ ] **Step 4: E2E** — `ProposalDecisionScenariosTests` (E2E fixture nemá proposal seed → vytvořit přes DB v testu je mimo E2E vzor; místo toho E2E scénář na CREATE proposal editoru ověří, že guard TAM funguje, a decision stránku kryje Api render + živý Playwright skript):

```csharp
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>B3 (2026-07-09): guard-off jen na detailu návrhu — editor návrhu guardy drží.</summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ProposalDecisionScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ProposalDecisionScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateProposalEditor_DirtyBack_ShowsDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var url = $"{_fixture.BaseUrl}/Navrhy/CreateRecordProposal?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}";
        var response = await page.GotoAsync(url);
        if (response is null || response.Status != 200)
        {
            return; // admin nemusí být lead v E2E seedu — scénář kryje Api test CreateProposalEditor_KeepsGuardOn
        }
        await page.Locator("input[name='Nazev']").FillAsync("Dirty návrh");
        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { }
        await Assertions.Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1);
        await page.GetByRole(AriaRole.Button, new() { Name = "Pokračovat v úpravách" }).ClickAsync();
        await page.Context.CloseAsync();
    }
}
```

- [ ] **Step 5: Živý decision scénář (Playwright skript, dev app):** seed pending návrhu v dev DB (SQL insert dle fixture helperu) → otevřít ProposalDetail → breadcrumb ← ⇒ URL se změní BEZ dialogu; browser back z čerstvě otevřené stránky ⇒ odchod bez dialogu. Výsledek + screenshot do reportu.

- [ ] **Step 6: Regrese** — Unit celé, Api `ProposalDecisionReadonlyRenderTests` + `BreadcrumbCoverageTests`, E2E `RecordEditorHistoryBackScenariosTests` (guard-on cesty nedotčené).
