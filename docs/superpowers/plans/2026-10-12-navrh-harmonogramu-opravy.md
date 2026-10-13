# Návrh změny harmonogramu — dialog, důvod změny, režim vyplňování — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Klik na „Navrhnout změnu harmonogramu“ u záznamu s čekajícím návrhem ukáže dialog místo chybové stránky. Autor návrhu může napsat důvod změny, schvalovatel ho uvidí a po schválení se uloží do historie termínu. Přepínač Auto/Ručně se v návrhu uloží a po schválení provede, schvalovatel vidí stav z návrhu a infobar, když se liší od záznamu.

**Architecture:**
- Pravidlo „lze založit návrh“ je jedna metoda `GetScheduleProposalAvailabilityAsync`. Používá ji editor návrhu, nový JSON endpoint pro ověření po kliknutí a záložní cesta v controlleru (přímý odkaz, souběh), která místo chybové stránky vrátí zpět s hláškou.
- Důvod změny a režim vyplňování putují v `PayloadJson` návrhu (žádná migrace DB). Schválení zapisuje do `zaznam_historie_terminu` (dnes schválení historii termínu nezapisuje vůbec).
- Pravidlo „přepínač je zapnutý“ se vytáhne do `HarmonogramAutoFillRule`, ať editor záznamu, návrh i detail návrhu počítají stav stejně.

**Tech Stack:** .NET 8, ASP.NET Core MVC + Razor, EF Core (SQL Server), DS gov 4.7.0 web components (`gov-dialog`, `gov-infobar`, `gov-icon`, `gov-button`, `gov-form-switch`), vanilla ES moduly, xUnit + FluentAssertions, Testcontainers SQL (Api, Integration), Playwright .NET (E2E), `node --test` (JS).

**Spec:** `docs/known-issues/2026-10-08-navrh-harmonogramu-opravy.md` (rozhodnutí uživatele 2026-10-08, sekce „Rozhodnutí uživatele“ 1–3) + konverzace 2026-10-12 („Navrhnout změnu harmonogramu … zobrazuje se mi chybová stránka“, „kde je pole pro důvod“, „kde ho vidí schvalovatel“).

## Global Constraints

- **DS FIS pravidlo (bez výjimky):** AI nikdy needituje `PmTracker.Web/wwwroot/assets/gov/**` ani `assets/ds-fis/ds-fis.css` / `assets/ds-fis/ds-fis.js`.
- **DS gov 4.7.0, ověřeno v bundlu 2026-10-12:** `gov-dialog` props `open`, `block-close`, `block-backdrop-close`, `role`, `label-tag`, `accessible-close-label` …; sloty `icon`, `title`, `footer` (oprava 2026-10-13: slot `label` gov-dialog nemá — nadpis se nevykreslil; komponenta je bez shadow DOM). **Atribut „status“ neexistuje** — stav chyby = `gov-icon slot="icon" color="error"`. `gov-infobar` props `color`, `type`, `role`, `closable`; sloty `icon`, `headline`. `gov-icon` props `size`, `color`, `name`, `type`. `gov-button` barvy přes `color`/`type` (Primary = `primary`/`solid`, Secondary = `primary`/`outlined`).
- **Dialog:** nikdy `block-close` (X by byl mrtvý); vždy `block-backdrop-close="true"` (zavírá se jen křížkem / tlačítkem).
- **Infobar (rozhodnutí uživatele):** `color="error"`, `type="bold"`, ikona `components/exclamation-triangle-fill`, `closable="false"`.
- **Důvod změny je nepovinný** (uživatel: „uživateli umožnil napsat text“), max **1000** znaků, prostý text (ne rich text).
- **Bez migrace DB:** důvod i režim v `zaznam_navrhy.payload_json`; `zaznam_historie_terminu.duvod` je od 1_4_6 `NVARCHAR(MAX)`.
- **Text dialogu:** nadpis „Návrh nelze založit“, text „K záznamu už existuje aktivní návrh na změnu termínu a harmonogramu. Nový návrh půjde založit, až bude čekající návrh schválen nebo zamítnut.“
- **Výchozí důvod v historii při schválení bez textu:** `Schválený návrh #{id}`. Přímá úprava záznamu dál píše „Úprava záznamu“.
- **JS:** modul s vedlejším efektem musí být importovaný v `wwwroot/js/modules/bootstrap.js`; AJAX URL vždy přes `appendCurrentAsUser` (`navigationShared.js`).
- **Razor:** komentář `@* *@` nikdy uvnitř tagu s TagHelpery; `data-x="@null"` vypíše `data-x=""` (podmíněné atributy skládat ručně).
- **Api HTML testy:** Razor kóduje diakritiku na entity → v HTML se kotvit na atributy/ASCII, český text ověřovat na view modelu (Integration).
- **Git:** commity rovnou do `main`, trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, push jen na pokyn. Výstup testů je česky.
- **Baseline (2026-10-08, main 3ddbd77):** Unit 2045/2045, Api 405/405, JS 31/31, Integration `Search|RichText` 34/34, E2E `Search|Header` 18/20 (2 pre-existing: `ProjectTeamTab_ShouldApplySharedSearch_AfterLazyLoad`, `SearchIndex_RenderujeGovFormSearch_PoMigracinaPmSearch`).

## Rozhodnutí v plánu (k potvrzení uživatelem)

1. **Kdo je blokován:** každý čekající návrh změny harmonogramu záznamu blokuje nový — tak se dnes chová otevření editoru. `SubmitScheduleProposalAsync` sice umí vlastní/cizí čekající návrh nahradit (supersede), ale přes UI se k tomu nikdo nedostane; tato pojistka zůstává beze změny.
2. **Záložní cesta na serveru** (přímý odkaz, druhá karta, souběh) vrací zpět s `TempData["ErrorMessage"]` → `gov-message color="error"` nahoře na stránce. Dialog je hlavní cesta po kliknutí.
3. **Důvod v historii termínu** se zapíše jen tehdy, když návrh termín skutečně mění. Schvalovatel ho vidí vždy.
4. **Podbarvení přepínače** v editoru návrhu i na detailu používá existující `.proposal-field-changed` (stejné oranžové podbarvení jako ostatní změněná pole návrhu).

## Review Focus

1. **Klik s Ctrl/Cmd/Shift nebo prostředním tlačítkem** (otevření v nové kartě) se nezachytává — prohlížeč otevře odkaz, rozhodne server (Task 2 test).
2. **Selhání ověřovacího dotazu** (síť, 500, 401) → pokračuje se na editor jako dřív; server rozhodne a případně vrátí s hláškou (Task 2 test čisté funkce + Task 1 Api test záložní cesty).
3. **Návrh, který už čeká v produkci** (payload bez `DuvodZmeny` a `HarmonogramRezim`) se zobrazí i schválí jako dřív: `null` = beze změny režimu, historie s výchozím důvodem (Task 4 a 5 testy).
4. **Důvod s HTML značkami nebo na více řádků** se zobrazí zakódovaně a se zachovanými zalomeními (Task 4 test).
5. **Návrh „Auto“, který nese ruční data u kroků 1/3/4/6/7/10**, je po schválení neuplatní (shodně se `SaveRecord`) a naplánuje harvest (Task 5 test).

---

## File Structure

| Soubor | Změna | Odpovědnost |
|---|---|---|
| `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` | Modify | `ScheduleProposalAvailability`, payload `DuvodZmeny`, `HarmonogramRezim`, `ChangesHarmonogramRezim` |
| `PmTracker.Web/Services/IRecordProposalService.cs`, `RecordProposalService.Queries.cs` | Modify | pravidlo dostupnosti, detail návrhu (sdělení autora, režim z návrhu) |
| `PmTracker.Web/Controllers/NavrhyController.cs` | Modify | JSON endpoint `ScheduleProposalAvailability`, záložní cesta v `CreateScheduleProposal` |
| `PmTracker.Web/Controllers/ProjektyController.cs`, `ZaznamyController.cs`, `ZaznamyController.Partials.cs` | Modify | `ScheduleProposalCheckUrl` vedle `ScheduleProposalUrl` |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs`, `ZaznamDetailPageViewModel.cs`, `ZaznamEditViewModels.cs` | Modify | nové vlastnosti VM |
| `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml`, `ZaznamDetailPage.cshtml`, `_EditZaznamForm.cshtml` | Modify | data atributy, pole důvodu, sdělení autora, infobar, podbarvení přepínače |
| `PmTracker.Web/wwwroot/js/modules/navrhy/scheduleProposalGuard.js` | Create | ověření po kliknutí + dialog |
| `PmTracker.Web/wwwroot/js/modules/bootstrap.js` | Modify | import modulu |
| `PmTracker.Web/wwwroot/js/modules/harmonogram/rezim-master-switch.js` | Modify | podbarvení přepínače při rozdílu |
| `PmTracker.Web/wwwroot/css/site.css` | Modify | sdělení autora, přepínač s podbarvením |
| `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` | Modify | `SaveRecordCommand.DuvodZmeny` |
| `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs` | Modify | důvod + režim do payloadu |
| `PmTracker.Web/Services/Schedules/HarmonogramAutoFillRule.cs` | Create | jediné pravidlo stavu přepínače |
| `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` | Modify | použití pravidla |
| `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs`, `RecordProposalService.DecisionCommands.cs` | Modify | odeslání (režim, změna) a schválení (historie, režim, harvest) |
| `PmTracker.Web/Services/Records/PendingScheduleProposalLockEvaluator.cs` | Modify | změna režimu zamyká harmonogram |
| testy | Create/Modify | viz tasky |
| `docs/known-issues/2026-10-08-navrh-harmonogramu-opravy.md`, `docs/wiki/projekty/navrhy/navrhnout-zmenu-planu.md` | Modify | dokumentace |

---

### Task 0: Baseline

- [ ] **Step 1:** `git status --short` je čistý, větev `main`.
- [ ] **Step 2:** Spusť a zapiš výsledky:

```bash
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
dotnet test PmTracker.Tests.Api 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
npm test 2>&1 | grep -E "^ℹ (pass|fail)"
```

Expected: Unit 2045/2045, Api 405/405, JS 31/31.

---

### Task 1: Pravidlo dostupnosti, JSON endpoint a záložní cesta bez chybové stránky

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs`
- Modify: `PmTracker.Web/Services/IRecordProposalService.cs`, `PmTracker.Web/Services/RecordProposalService.Queries.cs`
- Modify: `PmTracker.Web/Controllers/NavrhyController.cs`
- Modify: `PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestDataStore.cs`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs`
- Test: `PmTracker.Tests.Integration/DataStore/ScheduleProposalAvailabilityTests.cs` (Create), `PmTracker.Tests.Api/Controllers/ScheduleProposalAvailabilityEndpointTests.cs` (Create)

**Interfaces:**
- Produces:
  - `public enum ScheduleProposalBlockReason { None, NoPermission, PendingProposal }`
  - `public sealed record ScheduleProposalAvailability(bool CanCreate, ScheduleProposalBlockReason Reason, string? Message, int? PendingProposalId)` s konstantami `PendingTitle`, `PendingMessage`.
  - `Task<ScheduleProposalAvailability> IRecordProposalService.GetScheduleProposalAvailabilityAsync(int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)`
  - GET `/Navrhy/ScheduleProposalAvailability?projektId=&zaznamId=` → JSON `{ canCreate, title, message, proposalUrl }`.
  - Api fixture: `Task<int> EnsurePendingScheduleProposalAsync(int projectId, int subsystemId, int recordId, int authorOsobaId, string? harmonogramRezim = null, string? duvodZmeny = null)`.

- [ ] **Step 1: Failing Integration test** `ScheduleProposalAvailabilityTests.cs` (vzor nastavení osob/rolí: `RecordProposalDataStoreTests.SaveRecord_WithPendingScheduleProposal_ShouldPreserveDeadlineAndPlannedValues`):

```csharp
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleProposalAvailabilityTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ScheduleProposalAvailabilityTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dostupnost_BezCekajicihoNavrhu_LzeZalozit_SCekajicimNe()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_availability");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailLead");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AvailManager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "AVAIL1");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "AVAIL1_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", "Avail");
        var proposer = IntegrationTestHelper.BuildUser(proposerId, visibleProjectIds: [projectId]);

        var volno = store.GetScheduleProposalAvailability(projectId, recordId, proposer);
        volno.CanCreate.Should().BeTrue();
        volno.Reason.Should().Be(ScheduleProposalBlockReason.None);

        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = BuildEditCommand(editor);
        command.TerminUkonceni = editor.TerminUkonceni!.Value.AddDays(7);
        store.SubmitScheduleProposal(command, proposer);
        var proposalId = await dbContext.ZaznamNavrhy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId).Select(x => x.Id).SingleAsync();

        var obsazeno = store.GetScheduleProposalAvailability(projectId, recordId, proposer);
        obsazeno.CanCreate.Should().BeFalse();
        obsazeno.Reason.Should().Be(ScheduleProposalBlockReason.PendingProposal);
        obsazeno.PendingProposalId.Should().Be(proposalId);
        obsazeno.Message.Should().Be(ScheduleProposalAvailability.PendingMessage);
    }

    [Fact]
    public async Task Dostupnost_BezOpravneniKSubsystemu_NelzeZalozit()
    {
        // osoba bez role v subsystému záznamu a bez proposals.edit.any
        // … stejné nastavení projektu jako výše, uživatel = BuildUser(jinaOsobaId, visibleProjectIds: [projectId])
        // Expected: CanCreate false, Reason NoPermission.
    }
}
```

  `BuildEditCommand` zkopíruj z `RecordProposalDataStoreTests` (privátní helper, řádky 454–480). Druhý test dopiš celý podle komentáře (osoba přes `EnsurePersonAsync(dbContext, "AvailCizi")`, bez přiřazení role).

- [ ] **Step 2:** `IntegrationTestDataStore` — přidej wrapper vedle `BuildScheduleProposalEditor`:

```csharp
public ScheduleProposalAvailability GetScheduleProposalAvailability(int projektId, int zaznamId, CurrentUserContextViewModel currentUser)
    => services.GetRequiredService<IRecordProposalService>().GetScheduleProposalAvailabilityAsync(projektId, zaznamId, currentUser).GetAwaiter().GetResult();
```

- [ ] **Step 3:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ScheduleProposalAvailabilityTests"`. Expected: build error (typ a metoda neexistují).

- [ ] **Step 4: Implementace typu** v `RecordProposalViewModels.cs`:

```csharp
public enum ScheduleProposalBlockReason
{
    None,
    NoPermission,
    PendingProposal
}

/// <summary>
/// Lze k záznamu založit návrh změny termínu a harmonogramu? Jediné pravidlo pro editor návrhu,
/// ověření po kliknutí (dialog) i záložní cestu v controlleru (uživatel 2026-10-08: dialog,
/// ne chybová stránka).
/// </summary>
public sealed record ScheduleProposalAvailability(
    bool CanCreate,
    ScheduleProposalBlockReason Reason,
    string? Message,
    int? PendingProposalId)
{
    public const string PendingTitle = "Návrh nelze založit";
    public const string PendingMessage =
        "K záznamu už existuje aktivní návrh na změnu termínu a harmonogramu. Nový návrh půjde založit, až bude čekající návrh schválen nebo zamítnut.";
    public const string NoPermissionMessage =
        "Návrh změny termínu a harmonogramu může vytvořit jen vedoucí relevantního subsystému nebo jeho zástupce.";

    public static readonly ScheduleProposalAvailability Available = new(true, ScheduleProposalBlockReason.None, null, null);
}
```

- [ ] **Step 5: Implementace služby.** Do `IRecordProposalService` přidej podpis z Interfaces. V `RecordProposalService.Queries.cs`:

```csharp
public async Task<ScheduleProposalAvailability> GetScheduleProposalAvailabilityAsync(
    int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
{
    var access = await _authorizationPolicy.EvaluateRecordAccessAsync(projectId, recordId, currentUser, ct);
    if (!access.CanCreateScheduleProposal)
    {
        return new ScheduleProposalAvailability(false, ScheduleProposalBlockReason.NoPermission,
            ScheduleProposalAvailability.NoPermissionMessage, null);
    }

    var pendingLock = await _pendingScheduleProposalLockEvaluator.EvaluateAsync(recordId, ct);
    return pendingLock.HasPendingProposal
        ? new ScheduleProposalAvailability(false, ScheduleProposalBlockReason.PendingProposal,
            ScheduleProposalAvailability.PendingMessage, pendingLock.ProposalId)
        : ScheduleProposalAvailability.Available;
}
```

  V `BuildScheduleProposalEditorAsync` nahraď obě kontroly (oprávnění i čekající návrh) voláním:

```csharp
var availability = await GetScheduleProposalAvailabilityAsync(projectId, recordId, currentUser, ct);
if (!availability.CanCreate)
{
    throw new InvalidOperationException(availability.Message);
}
```

- [ ] **Step 6:** Spusť test z kroku 3. Expected: PASS 2/2. Spusť i `--filter "FullyQualifiedName~RecordProposal"` (Integration). Expected: PASS (editor se chová stejně).

- [ ] **Step 7: Api fixture helper** `EnsurePendingScheduleProposalAsync` v `ApiSqlFixture.cs` (vzor `EnsurePendingCreateProposalAsync`, ř. 195). Payload:

```csharp
var payload = System.Text.Json.JsonSerializer.Serialize(new RecordProposalPayload
{
    ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
    SchedulePlan = new SchedulePlanProposalPayload
    {
        ProjektId = projectId,
        ZaznamId = recordId,
        TerminUkonceni = DateTime.Today.AddDays(45),
        ChangesTermDeadline = true,
        HarmonogramRezim = harmonogramRezim,
        ChangesHarmonogramRezim = harmonogramRezim is not null,
        DuvodZmeny = duvodZmeny
    }
});
```

  Entita: `ProjektId`, `ZaznamId = recordId`, `SubsystemId`, `TypNavrhu = "SCHEDULE_PLAN_CHANGE"`, `Stav = "PENDING"`, `PayloadJson`, `CreatedByOsobaId = authorOsobaId`, `CreatedAt = DateTime.UtcNow`. Vrací existující čekající návrh záznamu, pokud už je (filtrovaný unique index `UX_zaznam_navrhy_pending_schedule_per_record` druhý nepustí). Pozn.: `HarmonogramRezim`, `ChangesHarmonogramRezim` a `DuvodZmeny` vzniknou v Task 3 a 5 — do té doby helper přiřazuje jen existující vlastnosti a tyto tři řádky doplní Task 3/5 (stejný commit jako vlastnosti).

- [ ] **Step 8: Failing Api testy** `ScheduleProposalAvailabilityEndpointTests.cs` (vzor fixture: `ProposalDecisionReadonlyRenderTests`):

```csharp
[Fact]
public async Task Dostupnost_SCekajicimNavrhem_VraciJsonSDuvodem()
{
    // osoba = lead subsystému (EnsureSubsystemLeadAsync), záznam „U", čekající návrh přes EnsurePendingScheduleProposalAsync
    var response = await client.GetAsync($"/Navrhy/ScheduleProposalAvailability?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    json.GetProperty("canCreate").GetBoolean().Should().BeFalse();
    json.GetProperty("title").GetString().Should().Be(ScheduleProposalAvailability.PendingTitle);
    json.GetProperty("message").GetString().Should().Be(ScheduleProposalAvailability.PendingMessage);
    json.GetProperty("proposalUrl").GetString().Should().Contain($"proposalId={proposalId}");
}

[Fact]
public async Task Dostupnost_BezNavrhu_LzeZalozit()
{
    // jiný záznam bez návrhu → canCreate true, proposalUrl null
}

[Fact]
public async Task OtevreniEditoru_SCekajicimNavrhem_VratiZpetSHlaskou_NeChybovouStranku()
{
    using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
    var response = await client.GetAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

    response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    var location = response.Headers.Location!.ToString();
    location.Should().Contain($"/Projekty/Detail/{projectId}").And.Contain("tab=zaznamy").And.Contain($"recordId={recordId}");

    var page = await client.GetAsync(location.Contains("asUser=") ? location : $"{location}&asUser={_fixture.AdminOsobaId}");
    (await page.Content.ReadAsStringAsync()).Should().Contain("<gov-message color=\"error\">",
        "hláška z TempData se ukáže nahoře na stránce (text kóduje Razor — ověřuje se na modelu)");
}

[Fact]
public async Task OtevreniEditoru_SCekajicimNavrhem_ARetunUrl_VratiNaReturnUrl()
{
    // returnUrl=/Zaznamy/Detail/{recordId} → Location začíná /Zaznamy/Detail/{recordId}
}
```

  Dopiš celé testy (setup osob/projektu podle vzoru, asUser = admin — superadmin má `proposals.schedule.create`).

- [ ] **Step 9:** `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ScheduleProposalAvailabilityEndpointTests"`. Expected: FAIL (endpoint neexistuje → 404; editor → 500/výjimka).

- [ ] **Step 10: Implementace controlleru** v `NavrhyController`:

```csharp
[HttpGet]
[Authorize(Policy = "permission:proposals.schedule.create")]
public async Task<IActionResult> ScheduleProposalAvailability(int projektId, int zaznamId, CancellationToken ct = default)
{
    if (!CurrentUserContext.CanAccessProject(projektId))
    {
        return NotFound();
    }

    var availability = await _recordProposalService.GetScheduleProposalAvailabilityAsync(projektId, zaznamId, CurrentUserContext, ct);
    string? proposalUrl = null;
    if (availability.PendingProposalId is int pendingId
        && await _recordProposalService.CanViewProposalTabAsync(projektId, CurrentUserContext, ct))
    {
        proposalUrl = Url.Action(nameof(ProposalDetail), new { projektId, proposalId = pendingId });
    }

    return Json(new
    {
        canCreate = availability.CanCreate,
        title = availability.CanCreate ? null : ScheduleProposalAvailability.PendingTitle,
        message = availability.Message,
        proposalUrl
    });
}
```

  V `CreateScheduleProposal` před `BuildScheduleProposalEditorAsync`:

```csharp
// Uživatel 2026-10-08: čekající návrh = návrat s hláškou, ne chybová stránka. Hlavní cesta
// je dialog po kliknutí (scheduleProposalGuard.js); sem se dostane přímý odkaz nebo souběh.
var availability = await _recordProposalService.GetScheduleProposalAvailabilityAsync(projektId, zaznamId, CurrentUserContext, ct);
if (availability.Reason == ScheduleProposalBlockReason.PendingProposal)
{
    TempData["ErrorMessage"] = availability.Message;
    return Redirect(NormalizeLocalReturnUrl(returnUrl)
        ?? Url.Action("Detail", "Projekty", new { id = projektId, tab = "zaznamy", recordId = zaznamId })
        ?? $"/Projekty/Detail/{projektId}?tab=zaznamy&recordId={zaznamId}");
}
```

  Pozn.: `title` pro `NoPermission` použij `ScheduleProposalAvailability.PendingTitle` taky („Návrh nelze založit“ platí pro oba důvody) — tedy `title = availability.CanCreate ? null : ScheduleProposalAvailability.PendingTitle` beze změny.

- [ ] **Step 11:** Spusť testy z kroků 3 a 9. Expected: PASS.
- [ ] **Step 12: Commit:** `fix(navrhy): čekající návrh harmonogramu vrátí zpět s hláškou, ne chybovou stránku; endpoint pro ověření`.

---

### Task 2: Dialog po kliknutí na „Navrhnout změnu harmonogramu“

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/navrhy/scheduleProposalGuard.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (import vedle `./ui/anchoredMenu.js`)
- Modify: `ZaznamCardSummaryViewModel` (`ProjektZaznamyTabViewModels.cs` ř. 93), `ZaznamDetailPageViewModel.cs` — `public string? ScheduleProposalCheckUrl { get; set; }`
- Modify: `ProjektyController.cs` ř. 287, `ZaznamyController.Partials.cs` ř. 114, `ZaznamyController.cs` ř. 97 — nastav `ScheduleProposalCheckUrl = Url.Action("ScheduleProposalAvailability", "Navrhy", new { projektId, zaznamId })`
- Modify: `_ZaznamPartial.cshtml` ř. 136–140, `ZaznamDetailPage.cshtml` ř. 54–57
- Test: `tests/js/dom/scheduleProposalGuard.test.js` (Create), `PmTracker.Tests.Api/Controllers/ScheduleProposalAvailabilityEndpointTests.cs` (doplnit), `PmTracker.Tests.E2E/Scenarios/ScheduleProposalGuardScenariosTests.cs` (Create)

**Interfaces:**
- Consumes: endpoint z Task 1.
- Produces: `export function resolveScheduleProposalCheck(result)` → `{ action: "navigate" } | { action: "dialog", title, message, proposalUrl }`; `export function isPlainLeftClick(event)` → `boolean`; DOM marker `[data-schedule-proposal-blocked-dialog]`.

- [ ] **Step 1: Failing JS test** `tests/js/dom/scheduleProposalGuard.test.js`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";
import { resolveScheduleProposalCheck, isPlainLeftClick } from "../../../PmTracker.Web/wwwroot/js/modules/navrhy/scheduleProposalGuard.js";

test("čekající návrh → dialog s textem ze serveru", () => {
    assert.deepEqual(
        resolveScheduleProposalCheck({ canCreate: false, title: "Návrh nelze založit", message: "Text", proposalUrl: "/Navrhy/ProposalDetail?projektId=1&proposalId=5" }),
        { action: "dialog", title: "Návrh nelze založit", message: "Text", proposalUrl: "/Navrhy/ProposalDetail?projektId=1&proposalId=5" });
});

test("bez odkazu na návrh → dialog bez odkazu", () => {
    assert.equal(resolveScheduleProposalCheck({ canCreate: false, title: "T", message: "M", proposalUrl: null }).proposalUrl, null);
});

test("lze založit nebo neznámá odpověď → pokračovat na editor (rozhodne server)", () => {
    assert.deepEqual(resolveScheduleProposalCheck({ canCreate: true }), { action: "navigate" });
    assert.deepEqual(resolveScheduleProposalCheck(null), { action: "navigate" });
    assert.deepEqual(resolveScheduleProposalCheck({}), { action: "navigate" });
});

test("klik s modifikátorem nebo jiným tlačítkem se nezachytává (nová karta)", () => {
    assert.equal(isPlainLeftClick({ button: 0 }), true);
    assert.equal(isPlainLeftClick({ button: 1 }), false);
    assert.equal(isPlainLeftClick({ button: 0, ctrlKey: true }), false);
    assert.equal(isPlainLeftClick({ button: 0, metaKey: true }), false);
    assert.equal(isPlainLeftClick({ button: 0, shiftKey: true }), false);
});
```

  Přidej soubor do `package.json` skriptu `test` jen pokud ho glob `tests/js/dom/*.test.js` nechytí (chytí).

- [ ] **Step 2:** `node --test tests/js/dom/scheduleProposalGuard.test.js`. Expected: `Cannot find module …scheduleProposalGuard.js`.

- [ ] **Step 3: Implementace** `scheduleProposalGuard.js`:

```js
// Ověření „Navrhnout změnu harmonogramu" před přechodem na editor návrhu (uživatel 2026-10-08):
// když k záznamu už čeká návrh, ukáže dialog místo chybové stránky. Server kontrolu drží i sám
// (přímý odkaz, souběh) — vrátí zpět s hláškou (NavrhyController.CreateScheduleProposal).
import { appendCurrentAsUser } from "../navigationShared.js";

const LINK_SELECTOR = "[data-schedule-proposal-check-url]";
const CLOSE_DELAY_MS = 400; // gov-dialog close transition (stejně jako recordEditor/draft.js)
let checkInFlight = false;

/** Co udělat s odpovědí ověření. Neznámá odpověď = pokračovat, rozhodne server. */
export function resolveScheduleProposalCheck(result) {
    if (!result || result.canCreate !== false) {
        return { action: "navigate" };
    }
    return {
        action: "dialog",
        title: result.title || "Návrh nelze založit",
        message: result.message || "",
        proposalUrl: typeof result.proposalUrl === "string" && result.proposalUrl ? result.proposalUrl : null
    };
}

/** Klik do nové karty (Ctrl/Cmd/Shift, prostřední tlačítko) nechává prohlížeči. */
export function isPlainLeftClick(event) {
    return (event.button ?? 0) === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey;
}

function createButton(label, { color, type, href }) {
    const button = document.createElement("gov-button");
    button.setAttribute("color", color);
    button.setAttribute("type", type);
    button.setAttribute("size", "m");
    if (href) {
        button.setAttribute("href", href);
    }
    button.textContent = label;
    return button;
}

function openBlockedDialog({ title, message, proposalUrl }) {
    const dialog = document.createElement("gov-dialog");
    dialog.setAttribute("role", "alertdialog");
    dialog.setAttribute("label-tag", "h3");
    dialog.setAttribute("block-backdrop-close", "true");
    dialog.setAttribute("accessible-close-label", "Zavřít dialog");
    dialog.setAttribute("data-schedule-proposal-blocked-dialog", "");

    // Stav chyby podle DS: ikona ve slotu icon (gov-dialog 4.7.0 nemá atribut status).
    const icon = document.createElement("gov-icon");
    icon.setAttribute("slot", "icon");
    icon.setAttribute("type", "components");
    icon.setAttribute("name", "exclamation-triangle-fill");
    icon.setAttribute("color", "error");
    dialog.appendChild(icon);

    const label = document.createElement("span");
    label.setAttribute("slot", "label");
    label.textContent = title;
    dialog.appendChild(label);

    const text = document.createElement("p");
    text.textContent = message;
    dialog.appendChild(text);

    const footer = document.createElement("div");
    footer.setAttribute("slot", "footer");
    footer.className = "schedule-proposal-blocked-dialog-footer";
    const okButton = createButton("Rozumím", { color: "primary", type: "solid" });
    footer.appendChild(okButton);
    if (proposalUrl) {
        footer.appendChild(createButton("Zobrazit čekající návrh",
            { color: "primary", type: "outlined", href: appendCurrentAsUser(proposalUrl) }));
    }
    dialog.appendChild(footer);

    let closed = false;
    const close = () => {
        if (closed) return;
        closed = true;
        dialog.open = false;
        window.setTimeout(() => dialog.remove(), CLOSE_DELAY_MS);
    };
    okButton.addEventListener("click", close);
    dialog.addEventListener("gov-close", close);

    document.body.appendChild(dialog);
    window.requestAnimationFrame(() => {
        dialog.open = true;
        window.setTimeout(() => okButton.isConnected && okButton.focus({ preventScroll: true }), 80);
    });
    return dialog;
}

async function handleClick(event) {
    const link = event.target instanceof Element ? event.target.closest(LINK_SELECTOR) : null;
    if (!link || !isPlainLeftClick(event)) {
        return;
    }
    const href = link.getAttribute("href");
    const checkUrl = link.getAttribute("data-schedule-proposal-check-url");
    if (!href || !checkUrl) {
        return;
    }

    event.preventDefault();
    if (checkInFlight) {
        return;
    }
    checkInFlight = true;
    try {
        const response = await fetch(appendCurrentAsUser(checkUrl), {
            headers: { Accept: "application/json" },
            credentials: "same-origin"
        });
        const decision = response.ok
            ? resolveScheduleProposalCheck(await response.json())
            : { action: "navigate" };
        if (decision.action === "dialog") {
            openBlockedDialog(decision);
            return;
        }
    } catch {
        // Síť / neplatná odpověď — pokračuje se na editor, rozhodne server.
    } finally {
        checkInFlight = false;
    }
    window.location.assign(href);
}

// Capture fáze: menu karty má data-stop-propagation (bubling by se sem nedostal).
// stopPropagation se nevolá — menu se po kliknutí samo zavře.
if (typeof document !== "undefined") {
    document.addEventListener("click", handleClick, true);
}
```

  `bootstrap.js`: přidej `import "./navrhy/scheduleProposalGuard.js"; // 2026-10-12 — dialog místo chybové stránky u čekajícího návrhu.` za `./ui/anchoredMenu.js`.
  `site.css`: `.schedule-proposal-blocked-dialog-footer { display: flex; gap: 0.75rem; justify-content: flex-end; }`.

- [ ] **Step 4:** `node --test tests/js/dom/scheduleProposalGuard.test.js` a `npm test`. Expected: PASS.

- [ ] **Step 5: Markup.** `_ZaznamPartial.cshtml` (položka menu):

```cshtml
<a class="record-actions-menu-item" role="menuitem"
   href="@summary.ScheduleProposalUrl"
   data-schedule-proposal-check-url="@summary.ScheduleProposalCheckUrl">Navrhnout změnu harmonogramu</a>
```

  `ZaznamDetailPage.cshtml`:

```cshtml
<pm-button variant="Secondary" size="Small" href="@Model.ScheduleProposalUrl"
           data-schedule-proposal-check-url="@Model.ScheduleProposalCheckUrl">Navrhnout změnu harmonogramu</pm-button>
```

  Controllery: hned za každé přiřazení `ScheduleProposalUrl` (ProjektyController ř. 287 a 307 není renderovaný — jen 287; ZaznamyController.Partials ř. 114 jako `??=`; ZaznamyController ř. 97) přidej odpovídající `ScheduleProposalCheckUrl` s `projektId` a `zaznamId` (bez `returnUrl`).

- [ ] **Step 6: Api render test** (doplnit do `ScheduleProposalAvailabilityEndpointTests`): karta záznamu v `/Projekty/RecordsTabPartial/{projectId}` obsahuje `data-schedule-proposal-check-url="/Navrhy/ScheduleProposalAvailability?projektId={projectId}&amp;zaznamId={recordId}"`; stránka `/Zaznamy/Detail/{recordId}` taky. Expected před markupem FAIL, po markupu PASS.

- [ ] **Step 7: Failing E2E** `ScheduleProposalGuardScenariosTests.cs` (vzor seedu: `SearchResultTargetScenariosTests.CreateRecordWithCommentsAsync`; čekající návrh vlož SQL `INSERT INTO dbo.zaznam_navrhy (projekt_id, zaznam_id, subsystem_id, typ_navrhu, stav, payload_json, created_by_osoba_id, created_at) VALUES (…, 'SCHEDULE_PLAN_CHANGE', 'PENDING', @payload, @admin, SYSUTCDATETIME())` s payloadem `{"ProposalType":"SCHEDULE_PLAN_CHANGE","SchedulePlan":{"ProjektId":…,"ZaznamId":…,"TerminUkonceni":"…","ChangesTermDeadline":true}}`):

```csharp
[Fact]
public async Task NavrhSCekajicimNavrhem_UkazeDialog_ZustaneNaStrance()
{
    var recordId = await CreateTaskRecordAsync();
    await InsertPendingScheduleProposalAsync(recordId);
    var page = await _fixture.NewPageAsync();
    try
    {
        var url = $"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}";
        await page.GotoAsync(url);
        await page.Locator("[data-schedule-proposal-check-url]").DispatchEventAsync("click");

        var dialog = page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]");
        await Expect(dialog).ToHaveCountAsync(1);
        await Expect(dialog).ToContainTextAsync("Návrh nelze založit");
        await Expect(page).ToHaveURLAsync(url);
    }
    finally
    {
        await page.Context.CloseAsync();
        await DeleteRecordAsync(recordId);   // smaže i zaznam_navrhy záznamu
    }
}

[Fact]
public async Task NavrhBezCekajicihoNavrhu_OtevreEditor()
{
    // bez návrhu → po kliknutí URL obsahuje /Navrhy/CreateScheduleProposal, žádný dialog
}
```

  Pozn. (memory Playwright + gov komponenty): host `gov-button` je „not visible“ → klik přes `DispatchEventAsync("click")`; viditelnost dialogu přes `ToHaveCountAsync`, ne `ToBeVisible`.

- [ ] **Step 8:** `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ScheduleProposalGuardScenariosTests"`. Expected před krokem 3–5 FAIL (chybová stránka / žádný dialog), teď PASS 2/2.
- [ ] **Step 9: Commit:** `feat(navrhy): dialog „Návrh nelze založit" místo chybové stránky u čekajícího návrhu`.

---

### Task 3: Pole „Důvod změny“ v návrhu

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` (`SaveRecordCommand`)
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` (`SchedulePlanProposalPayload`)
- Modify: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs`
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (řádek `DuvodZmeny = duvodZmeny` v helperu z Task 1)
- Test: `PmTracker.Tests.Unit/Records/RecordProposalPayloadMapperScheduleTests.cs` (Create), `PmTracker.Tests.Api/Controllers/ScheduleProposalReasonRenderTests.cs` (Create)

**Interfaces:**
- Produces: `SaveRecordCommand.DuvodZmeny` (`string?`, `[StringLength(SaveRecordCommand.DuvodZmenyMaxLength)]`, `public const int DuvodZmenyMaxLength = 1000`), `SchedulePlanProposalPayload.DuvodZmeny` (`string?`, null = nevyplněno).

- [ ] **Step 1: Failing unit test** `RecordProposalPayloadMapperScheduleTests.cs`:

```csharp
public sealed class RecordProposalPayloadMapperScheduleTests
{
    private static SaveRecordCommand Command(string? duvod) => new()
    {
        Id = 5, ProjektId = 1, TerminUkonceni = new DateTime(2026, 11, 30), DuvodZmeny = duvod
    };

    [Theory]
    [InlineData("  Dodavatel posunul dodávku.  ", "Dodavatel posunul dodávku.")]
    [InlineData("Řádek 1\nŘádek 2", "Řádek 1\nŘádek 2")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void BuildSchedulePayload_PrenasiDuvodZmeny_OrezanyNeboNull(string? vstup, string? ocekavany)
    {
        var payload = new RecordProposalPayloadMapper().BuildSchedulePayload(Command(vstup), new DateTime(2026, 10, 31), currentAutoFill: true);
        payload.SchedulePlan!.DuvodZmeny.Should().Be(ocekavany);
    }
}
```

  Pozn.: parametr `currentAutoFill` přidává Task 5. Pokud Task 3 běží dřív, volej zatím `BuildSchedulePayload(Command(vstup), new DateTime(2026, 10, 31))` a Task 5 volání v tomto testu upraví.

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordProposalPayloadMapperScheduleTests"`. Expected: build error (`DuvodZmeny` neexistuje).

- [ ] **Step 3: Implementace.** `SaveRecordCommand`:

```csharp
public const int DuvodZmenyMaxLength = 1000;

/// <summary>
/// Důvod změny z návrhu změny termínu a harmonogramu (uživatel 2026-10-08) — nepovinné sdělení
/// autora schvalovateli; po schválení se uloží do zaznam_historie_terminu.duvod. Přímá úprava
/// záznamu ho nepoužívá.
/// </summary>
[StringLength(DuvodZmenyMaxLength)]
public string? DuvodZmeny { get; set; }
```

  `SchedulePlanProposalPayload`: `public string? DuvodZmeny { get; set; }` (komentář: „Sdělení autora návrhu (uživatel 2026-10-08). Null u návrhů bez důvodu i u starších návrhů.“).
  Mapper `BuildSchedulePayload`: `DuvodZmeny = string.IsNullOrWhiteSpace(command.DuvodZmeny) ? null : command.DuvodZmeny.Trim()`.

- [ ] **Step 4:** Spusť test z kroku 2. Expected: PASS 4/4.

- [ ] **Step 5: Failing Api render test** `ScheduleProposalReasonRenderTests.cs`:

```csharp
[Fact]
public async Task EditorNavrhu_MaPoleDuvodu_EditorZaznamuNe()
{
    // lead subsystému, záznam „U" bez návrhu
    var proposalHtml = await client.GetStringAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
    proposalHtml.Should().Contain("name=\"DuvodZmeny\"").And.Contain("maxlength=\"1000\"").And.Contain("data-record-proposal-reason");

    var editHtml = await client.GetStringAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
    editHtml.Should().NotContain("name=\"DuvodZmeny\"");
}
```

- [ ] **Step 6:** `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ScheduleProposalReasonRenderTests"`. Expected: FAIL.

- [ ] **Step 7: Markup** v `_EditZaznamForm.cshtml` mezi `</div>` řádku záložek (`record-editor-tabs-row`, ř. ~174) a `<div class="record-editor-actions">` — mimo záložky, aby pole bylo vidět na záložce Základní i Harmonogram:

```cshtml
@if (Model.IsSchedulePlanProposalEditor)
{
    <div class="record-form-row record-proposal-reason" data-record-proposal-reason>
        <label for="duvodZmeny">
            Důvod změny (volitelné)
            <textarea id="duvodZmeny"
                      name="DuvodZmeny"
                      rows="3"
                      maxlength="@SaveRecordCommand.DuvodZmenyMaxLength"></textarea>
        </label>
        <p class="muted record-editor-footnote">Uvidí ho schvalovatel návrhu. Po schválení se uloží do historie termínu.</p>
    </div>
}
```

  (Razor komentář nad `@if`, nikdy uvnitř tagu.)

- [ ] **Step 8:** Spusť test z kroku 6 a Integration `--filter "FullyQualifiedName~RecordProposal"`. Expected: PASS.
- [ ] **Step 9:** Doplň `DuvodZmeny = duvodZmeny` do helperu `EnsurePendingScheduleProposalAsync` (Task 1 krok 7).
- [ ] **Step 10: Commit:** `feat(navrhy): pole Důvod změny v návrhu změny termínu a harmonogramu`.

---

### Task 4: Sdělení autora při schvalování a důvod v historii termínu

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.Queries.cs` (`BuildProposalDetailAsync`, větev `SchedulePlanChange`)
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs` (`ApplyApprovedScheduleProposalAsync`)
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml`, `PmTracker.Web/wwwroot/css/site.css`
- Test: `PmTracker.Tests.Integration/DataStore/ScheduleProposalReasonTests.cs` (Create), `ScheduleProposalReasonRenderTests.cs` (doplnit)

**Interfaces:**
- Consumes: `SchedulePlanProposalPayload.DuvodZmeny` (Task 3).
- Produces: `ZaznamEditViewModel.ShowProposalAuthorMessage` (bool), `ProposalAuthorName` (string), `ProposalCreatedAtLocal` (DateTime?), `ProposalAuthorMessage` (string?).

- [ ] **Step 1: Failing Integration testy** `ScheduleProposalReasonTests.cs` (setup jako Task 1; schvalovatel `BuildUser(managerId, visibleProjectIds: [projectId], grants: [AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)])`):

```csharp
[Fact]
public async Task DetailNavrhu_UkazeSdeleniAutora()
{
    // submit s command.DuvodZmeny = "Dodavatel posunul dodávku.\nNový termín potvrzen." a změnou termínu
    var detail = store.BuildProposalDetail(projectId, proposalId, manager);
    detail.ShowProposalAuthorMessage.Should().BeTrue();
    detail.ProposalAuthorMessage.Should().Be("Dodavatel posunul dodávku.\nNový termín potvrzen.");
    detail.ProposalAuthorName.Should().Contain("AvailLead");   // jméno z EnsurePersonAsync
}

[Fact]
public async Task SchvaleniSeZmenouTerminu_ZapiseHistoriiSDuvodem()
{
    // original = record.DatumUkonceni; navržený = original + 14; DuvodZmeny = "Posun dodávky"
    store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposalId }, manager);
    var historie = await dbContext.ZaznamHistorieTerminu.AsNoTracking().SingleAsync(x => x.ZaznamId == recordId);
    historie.PuvodniDatum.Should().Be(original);
    historie.NoveDatum.Should().Be(original.AddDays(14));
    historie.Duvod.Should().Be("Posun dodávky");
}

[Fact]
public async Task SchvaleniBezDuvodu_ZapiseVychoziDuvodSCislemNavrhu()
{
    // stejné bez DuvodZmeny → Duvod == $"Schválený návrh #{proposalId}"
}

[Fact]
public async Task SchvaleniJenHarmonogramu_HistoriiTerminuNezapise()
{
    // TerminUkonceni beze změny, jen PlanDatum kroku 1 → žádný řádek zaznam_historie_terminu
}

[Fact]
public async Task StaryNavrhBezDuvodu_DetailUkazeChybejiciDuvod()
{
    // návrh vložený přímo do DB s payloadem bez DuvodZmeny (starý formát) →
    // ShowProposalAuthorMessage true, ProposalAuthorMessage null
}
```

  Dopiš celé testy podle vzoru `RecordProposalDataStoreTests` (submit přes `store.SubmitScheduleProposal`, id návrhu z `dbContext.ZaznamNavrhy`).

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ScheduleProposalReasonTests"`. Expected: build error (vlastnosti VM), po jejich přidání FAIL (prázdné hodnoty, chybějící historie).

- [ ] **Step 3: Implementace.**
  - `ZaznamEditViewModel`: čtyři vlastnosti z Interfaces s komentářem „Sdělení autora návrhu změny termínu a harmonogramu na stránce schvalování (uživatel 2026-10-08).“
  - `BuildProposalDetailAsync`, větev `SchedulePlanChange`, po `ConfigureProposalDetailEditor`:

```csharp
model.ShowProposalAuthorMessage = true;
model.ProposalAuthorMessage = schedulePayload.DuvodZmeny;
model.ProposalAuthorName = await _dbContext.Osoby.AsNoTracking()
    .Where(x => x.Id == proposal.CreatedByOsobaId)
    .Select(x => string.IsNullOrWhiteSpace(x.Titul) ? x.Jmeno + " " + x.Prijmeni : x.Titul + " " + x.Jmeno + " " + x.Prijmeni)
    .FirstOrDefaultAsync(ct) ?? $"Osoba #{proposal.CreatedByOsobaId}";
model.ProposalCreatedAtLocal = TimeZoneInfo.ConvertTimeFromUtc(
    DateTime.SpecifyKind(proposal.CreatedAt, DateTimeKind.Utc), _timeProvider.LocalTimeZone);
```

    (Zobrazení jména zkontroluj proti `peopleById` v `BuildProjectProposalsTabAsync`, ř. 63 — použij stejný tvar; pokud tam je helper, zavolej ho.)
  - `ApplyApprovedScheduleProposalAsync`: před přepisem termínu si ulož `var oldDeadline = record.DatumUkonceni.Date;` a za `record.DatumUkonceni = …` přidej:

```csharp
// Dřív schválení historii termínu nezapisovalo vůbec (zapisoval jen RecordService.SaveRecord).
if (record.DatumUkonceni.Date != oldDeadline)
{
    _dbContext.ZaznamHistorieTerminu.Add(new ZaznamHistorieTerminuEntity
    {
        ZaznamId = record.Id,
        PuvodniDatum = oldDeadline,
        NoveDatum = record.DatumUkonceni.Date,
        DatumZmeny = _timeProvider.GetLocalNow().LocalDateTime,
        Duvod = string.IsNullOrWhiteSpace(schedulePayload.DuvodZmeny)
            ? $"Schválený návrh #{proposal.Id}"
            : schedulePayload.DuvodZmeny.Trim()
    });
}
```

  - `_EditZaznamForm.cshtml`, hned za blok `HasPendingScheduleProposalLock` (nad řádkem záložek):

```cshtml
@if (Model.IsProposalDecisionDetail && Model.ShowProposalAuthorMessage)
{
    <section class="record-proposal-author-message" data-proposal-author-message aria-labelledby="proposal-author-message-title">
        <h3 id="proposal-author-message-title">Sdělení autora návrhu</h3>
        <p class="muted">@Model.ProposalAuthorName@(Model.ProposalCreatedAtLocal is DateTime vytvoreno ? $" · {vytvoreno:dd.MM.yyyy HH:mm}" : "")</p>
        @if (!string.IsNullOrWhiteSpace(Model.ProposalAuthorMessage))
        {
            <p class="record-proposal-author-message-text" data-proposal-author-message-text>@Model.ProposalAuthorMessage</p>
        }
        else
        {
            <p class="muted" data-proposal-author-message-empty>Autor důvod změny neuvedl.</p>
        }
    </section>
}
```

  - `site.css` (jen tokeny): `.record-proposal-author-message { border: 1px solid var(--pm-border); border-radius: var(--gov-radius-sm); padding: 12px 16px; background: var(--pm-surface); } .record-proposal-author-message h3 { margin: 0 0 4px; } .record-proposal-author-message-text { white-space: pre-line; margin: 0; }`.

- [ ] **Step 4:** Spusť test z kroku 2. Expected: PASS 5/5.

- [ ] **Step 5: Api render test** (doplnit do `ScheduleProposalReasonRenderTests`): návrh z `EnsurePendingScheduleProposalAsync(…, duvodZmeny: "<b>tučně</b>\nřádek")` → `/Navrhy/ProposalDetail?projektId=…&proposalId=…` obsahuje `data-proposal-author-message-text`, obsahuje `&lt;b&gt;` a neobsahuje `<b>tučně</b>` (zakódováno, Review Focus 4). Návrh bez důvodu → `data-proposal-author-message-empty`. Expected PASS.
- [ ] **Step 6:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RecordProposal|FullyQualifiedName~ScheduleProposal"`. Expected: PASS.
- [ ] **Step 7: Commit:** `feat(navrhy): schvalovatel vidí sdělení autora; schválení zapíše termín do historie s důvodem`.

---

### Task 5: Režim vyplňování se ukládá do návrhu a schválení ho provede

**Files:**
- Create: `PmTracker.Web/Services/Schedules/HarmonogramAutoFillRule.cs`
- Modify: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` (ř. ~271)
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs`
- Modify: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` (ř. ~142)
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs`
- Modify: `PmTracker.Web/Services/Records/PendingScheduleProposalLockEvaluator.cs`
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (řádky režimu v helperu z Task 1)
- Test: `PmTracker.Tests.Unit/Schedules/HarmonogramAutoFillRuleTests.cs` (Create), `RecordProposalPayloadMapperScheduleTests.cs` (doplnit), `PmTracker.Tests.Integration/DataStore/ScheduleProposalRezimTests.cs` (Create)

**Interfaces:**
- Produces:
  - `public static class HarmonogramAutoFillRule { public const string Auto = "Auto"; public const string Manual = "Manual"; public static bool IsAutoFill(IEnumerable<(int Poradi, byte SkutecnostRezim)> kroky); public static string? Normalize(string? rezim); }`
  - `SchedulePlanProposalPayload.HarmonogramRezim` (`string?`), `ChangesHarmonogramRezim` (`bool`).
  - `RecordProposalPayloadMapper.BuildSchedulePayload(SaveRecordCommand command, DateTime originalDeadline, bool currentAutoFill)`.
  - `ApplyApprovedScheduleProposalAsync` vrací `Task<bool>` = „naplánovat harvest“.

- [ ] **Step 1: Failing unit testy.** `HarmonogramAutoFillRuleTests.cs`:

```csharp
public sealed class HarmonogramAutoFillRuleTests
{
    private const byte Auto = (byte)SkutecnostRezimEnum.Auto;
    private const byte Manual = (byte)SkutecnostRezimEnum.Manual;

    [Fact]
    public void BezKroku_JeAuto() => HarmonogramAutoFillRule.IsAutoFill([]).Should().BeTrue();

    [Fact]
    public void RucniJenKroky2_5_8_9_PrepinacNevypnou()
        => HarmonogramAutoFillRule.IsAutoFill([(2, Manual), (5, Manual), (1, Auto)]).Should().BeTrue();

    [Fact]
    public void RucniAutoEligibleKrok_JeManual()
        => HarmonogramAutoFillRule.IsAutoFill([(3, Manual), (1, Auto)]).Should().BeFalse();

    [Theory]
    [InlineData("Auto", "Auto")]
    [InlineData("manual", "Manual")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("cokoli", null)]
    public void Normalize(string? vstup, string? ocekavany) => HarmonogramAutoFillRule.Normalize(vstup).Should().Be(ocekavany);
}
```

  Do `RecordProposalPayloadMapperScheduleTests` přidej:

```csharp
[Theory]
[InlineData("Manual", true, "Manual", true)]
[InlineData("Auto", true, "Auto", false)]
[InlineData("Auto", false, "Auto", true)]
[InlineData(null, true, null, false)]
public void BuildSchedulePayload_NeseRezimAZmenu(string? rezim, bool soucasneAuto, string? ocekavanyRezim, bool ocekavanaZmena)
{
    var command = Command(null);
    command.HarmonogramRezim = rezim;
    var plan = new RecordProposalPayloadMapper().BuildSchedulePayload(command, new DateTime(2026, 10, 31), soucasneAuto).SchedulePlan!;
    plan.HarmonogramRezim.Should().Be(ocekavanyRezim);
    plan.ChangesHarmonogramRezim.Should().Be(ocekavanaZmena);
}
```

  (a v testu z Task 3 doplň argument `currentAutoFill: true`, pokud chybí).

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~HarmonogramAutoFillRuleTests|FullyQualifiedName~RecordProposalPayloadMapperScheduleTests"`. Expected: build error.

- [ ] **Step 3: Implementace pravidla** `HarmonogramAutoFillRule.cs`:

```csharp
namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Přepínač „Automatické vyplňování harmonogramu“ (uživatel 2026-10-08: musí platit i v návrhu).
/// Zapnutý, když žádný krok napojený na ServiceDesk (1/3/4/6/7/10) není v ručním režimu —
/// kroky 2/5/8/9 jsou ruční vždy a přepínač neovlivňují. Jediné pravidlo pro editor záznamu,
/// editor návrhu i detail návrhu.
/// </summary>
public static class HarmonogramAutoFillRule
{
    public const string Auto = "Auto";
    public const string Manual = "Manual";

    public static bool IsAutoFill(IEnumerable<(int Poradi, byte SkutecnostRezim)> kroky)
        => !kroky.Any(k => !HarmonogramManualSteps.IsManual(k.Poradi)
                           && k.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual);

    public static string? Normalize(string? rezim)
        => string.Equals(rezim, Auto, StringComparison.OrdinalIgnoreCase) ? Auto
            : string.Equals(rezim, Manual, StringComparison.OrdinalIgnoreCase) ? Manual
            : null;
}
```

  `ProjectService.RecordEditorComposition.cs`: `HarmonogramAutoFillSwitchOn = HarmonogramAutoFillRule.IsAutoFill(krokRows.Select(k => ((int)k.Poradi, k.SkutecnostRezim)))` (zachovej stávající komentář nad řádkem).
  Payload: `public string? HarmonogramRezim { get; set; }` („Auto“/„Manual“ podle přepínače v návrhu; null u starších návrhů = beze změny) a `public bool ChangesHarmonogramRezim { get; set; }`.
  Mapper: nový parametr `bool currentAutoFill`;

```csharp
var rezim = HarmonogramAutoFillRule.Normalize(command.HarmonogramRezim);
var changesRezim = rezim is not null && (rezim == HarmonogramAutoFillRule.Auto) != currentAutoFill;
// … HarmonogramRezim = rezim, ChangesHarmonogramRezim = changesRezim
```

- [ ] **Step 4:** Spusť testy z kroku 2. Expected: PASS.

- [ ] **Step 5: Odeslání a zámek.** `SubmitScheduleProposalAsync` před mapperem:

```csharp
var currentKroky = await _dbContext.ZaznamHarmonogramKroky.AsNoTracking()
    .Where(k => k.ZaznamId == record.Id)
    .Select(k => new { k.Poradi, k.SkutecnostRezim })
    .ToListAsync(ct);
var currentAutoFill = HarmonogramAutoFillRule.IsAutoFill(currentKroky.Select(k => ((int)k.Poradi, k.SkutecnostRezim)));
var payload = _payloadMapper.BuildSchedulePayload(command, record.DatumUkonceni, currentAutoFill);
```

  Podmínka „Návrh neobsahuje žádnou změnu“ doplň o `&& !schedulePayload.ChangesHarmonogramRezim`.
  `PendingScheduleProposalLockEvaluator`: `locksSchedule` doplň o `|| (schedulePayload?.ChangesHarmonogramRezim ?? false)`.

- [ ] **Step 6: Failing Integration testy** `ScheduleProposalRezimTests.cs` (setup jako Task 1; krok řádky založ přímo přes `dbContext.ZaznamHarmonogramKroky.Add` jako v `RecordProposalDataStoreTests` ř. 133):

```csharp
[Fact]
public async Task NavrhAutoNaRucne_PoSchvaleni_AutoEligibleKrokyRucne_RucniDatumUlozeno()
{
    // záznam: kroky 1..10 Auto; návrh HarmonogramRezim="Manual", ManualActualKroky=[{Poradi=3, AbsolutniDatum=X}]
    // po ApproveProposal: kroky 1,3,4,6,7,10 SkutecnostRezim=Manual; krok 3 SkutecnostDatum=X
}

[Fact]
public async Task NavrhRucneNaAuto_PoSchvaleni_AutoEligibleKrokyAuto_RucniDataAutoKrokuNeuplatni()
{
    // záznam: kroky 1..10, auto-eligible Manual, krok 3 SkutecnostDatum=A;
    // návrh HarmonogramRezim="Auto" + ManualActualKroky=[{Poradi=3, AbsolutniDatum=B}] (Review Focus 5)
    // po schválení: kroky 1,3,4,6,7,10 SkutecnostRezim=Auto; krok 3 SkutecnostDatum zůstane A (B se neuplatní)
}

[Fact]
public async Task StaryNavrhBezRezimu_SchvaleniChovaJakoDriv()
{
    // payload bez HarmonogramRezim (vložit přímo do DB) + ManualActualKroky krok 3
    // → krok 3 Manual s datem, ostatní kroky beze změny režimu (Review Focus 3)
}

[Fact]
public async Task NavrhJenSeZmenouRezimu_LzeOdeslat()
{
    // command bez změny termínu a plánu, HarmonogramRezim="Manual" na záznamu v Auto
    // → SubmitScheduleProposal nevyhodí „Návrh neobsahuje žádnou změnu…"; payload.ChangesHarmonogramRezim == true
}
```

  Dopiš celé testy. Pozn.: `BuildEditCommand` posílá `HarmonogramHodnoty = []` → změna jen režimu musí projít díky `ChangesHarmonogramRezim`.

- [ ] **Step 7:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ScheduleProposalRezimTests"`. Expected: FAIL (režim se neuplatní).

- [ ] **Step 8: Implementace schválení** v `ApplyApprovedScheduleProposalAsync` (změň návratový typ na `Task<bool>`), po `GetOrCreateKrok`:

```csharp
// Uživatel 2026-10-08: přepínač v návrhu se po schválení opravdu provede (dřív se neukládal).
// Stejné pravidlo jako RecordService.SaveRecord: přepínač řídí jen kroky 1/3/4/6/7/10.
var targetRezim = HarmonogramAutoFillRule.Normalize(schedulePayload.HarmonogramRezim);
var targetAuto = targetRezim == HarmonogramAutoFillRule.Auto;
var switchedToAuto = false;
if (targetRezim is not null)
{
    var target = targetAuto ? SkutecnostRezimEnum.Auto : SkutecnostRezimEnum.Manual;
    foreach (var poradi in Enumerable.Range(1, 10).Where(p => !HarmonogramManualSteps.IsManual(p)))
    {
        var row = GetOrCreateKrok(poradi);
        if (row.SkutecnostRezim != (byte)target)
        {
            switchedToAuto |= targetAuto;
            row.SkutecnostRezim = (byte)target;
            row.UpdatedAt = nowUtc;
        }
    }
}
```

  V cyklu `ChangesScheduleActual` přeskoč auto-eligible kroky, když `targetAuto` (`if (targetAuto && !HarmonogramManualSteps.IsManual(av.Poradi)) continue;`). `ManualActualKrokApplier.Compute(schedulePayload.ManualActualKroky, acceptAutoEligibleKroky: !targetAuto)`. Na konci `return switchedToAuto;`.
  Volající v `ApproveProposalAsync`: `if (await ApplyApprovedScheduleProposalAsync(proposal, currentUser, ct)) { harvestRecordIdToEnqueue = proposal.ZaznamId; }` (harvest se naplánuje po commitu stávajícím mechanismem, ř. ~85).

- [ ] **Step 9:** Spusť test z kroku 7 a celé Integration `--filter "FullyQualifiedName~RecordProposal|FullyQualifiedName~ScheduleProposal"`. Expected: PASS.
- [ ] **Step 10:** Doplň do Api helperu `EnsurePendingScheduleProposalAsync` řádky `HarmonogramRezim = harmonogramRezim, ChangesHarmonogramRezim = harmonogramRezim is not null`.
- [ ] **Step 11:** `dotnet test PmTracker.Tests.Unit`. Expected: bez selhání.
- [ ] **Step 12: Commit:** `fix(navrhy): režim vyplňování harmonogramu se ukládá do návrhu a po schválení provede`.

---

### Task 6: Schvalovatel vidí režim z návrhu, infobar; podbarvení přepínače v editoru návrhu

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs`
- Modify: `PmTracker.Web/Services/RecordProposalService.Queries.cs` (`BuildProposalDetailAsync`)
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml` (přepínač ř. 163–173 + infobar)
- Modify: `PmTracker.Web/wwwroot/js/modules/harmonogram/rezim-master-switch.js` (`handleChange`)
- Modify: `PmTracker.Web/wwwroot/css/site.css`
- Test: `PmTracker.Tests.Integration/DataStore/ScheduleProposalRezimTests.cs` (doplnit), `PmTracker.Tests.Api/Controllers/ScheduleProposalRezimRenderTests.cs` (Create), `PmTracker.Tests.E2E/Scenarios/ScheduleProposalGuardScenariosTests.cs` (doplnit)

**Interfaces:**
- Consumes: `HarmonogramAutoFillRule` a payload z Task 5.
- Produces: `ZaznamEditViewModel.ProposalChangesHarmonogramRezim` (bool), `ProposalRezimChangeMessage` (string?); atribut `data-record-rezim-original` na přepínači v editoru návrhu.

- [ ] **Step 1: Failing Integration test** (doplnit do `ScheduleProposalRezimTests`):

```csharp
[Fact]
public async Task DetailNavrhu_UkazeRezimZNavrhu_AOznaciZmenu()
{
    // záznam Auto, návrh HarmonogramRezim="Manual"
    var detail = store.BuildProposalDetail(projectId, proposalId, manager);
    detail.HarmonogramAutoFillSwitchOn.Should().BeFalse("schvalovatel vidí stav z návrhu, ne ze záznamu");
    detail.ProposalChangesHarmonogramRezim.Should().BeTrue();
    detail.ProposalRezimChangeMessage.Should().Contain("ruční");
}

[Fact]
public async Task DetailNavrhu_StejnyRezim_BezOznaceni()
{
    // záznam Auto, návrh HarmonogramRezim="Auto" → ProposalChangesHarmonogramRezim false, message null
}
```

- [ ] **Step 2:** `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ScheduleProposalRezimTests"`. Expected: build error / FAIL.

- [ ] **Step 3: Implementace.** VM: `public bool ProposalChangesHarmonogramRezim { get; set; }`, `public string? ProposalRezimChangeMessage { get; set; }`. `BuildProposalDetailAsync`, větev `SchedulePlanChange`, **před** `ApplySchedulePayloadToModel` si ulož `var recordAutoFill = model.HarmonogramAutoFillSwitchOn;` a po `ConfigureProposalDetailEditor`:

```csharp
var proposedRezim = HarmonogramAutoFillRule.Normalize(schedulePayload.HarmonogramRezim);
if (proposedRezim is not null)
{
    model.HarmonogramAutoFillSwitchOn = proposedRezim == HarmonogramAutoFillRule.Auto;
    model.ProposalChangesHarmonogramRezim = model.HarmonogramAutoFillSwitchOn != recordAutoFill;
    model.ProposalRezimChangeMessage = !model.ProposalChangesHarmonogramRezim ? null
        : model.HarmonogramAutoFillSwitchOn
            ? "Návrh přepíná harmonogram na automatické vyplňování. Po schválení se kroky 1, 3, 4, 6, 7 a 10 budou znovu plnit ze ServiceDesku."
            : "Návrh přepíná harmonogram na ruční vyplňování. Po schválení se kroky 1, 3, 4, 6, 7 a 10 přestanou plnit ze ServiceDesku.";
}
```

- [ ] **Step 4:** Spusť test z kroku 2. Expected: PASS.

- [ ] **Step 5: Failing Api render testy** `ScheduleProposalRezimRenderTests.cs`:

```csharp
[Fact]
public async Task DetailNavrhuSeZmenouRezimu_MaInfobarAPodbarvenyPrepinac()
{
    // záznam bez krok řádků (= Auto), návrh EnsurePendingScheduleProposalAsync(…, harmonogramRezim: "Manual")
    var html = await client.GetStringAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
    var infobar = Regex.Match(html, "<gov-infobar[^>]*data-proposal-rezim-infobar[^>]*>[\\s\\S]*?</gov-infobar>").Value;
    infobar.Should().Contain("color=\"error\"").And.Contain("type=\"bold\"").And.Contain("closable=\"false\"")
        .And.Contain("name=\"exclamation-triangle-fill\"").And.Contain("slot=\"icon\"");
    Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>").Value
        .Should().Contain("proposal-field-changed").And.NotContain(" checked");
}

[Fact]
public async Task DetailNavrhuBezZmenyRezimu_BezInfobaru()
{
    // harmonogramRezim: null → html neobsahuje data-proposal-rezim-infobar
}

[Fact]
public async Task EditorNavrhu_PrepinacNesePuvodniStav()
{
    var html = await client.GetStringAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
    Regex.Match(html, "<gov-form-switch[^>]*data-record-rezim-switch[^>]*>").Value
        .Should().Contain("data-record-rezim-original=\"Auto\"");
}
```

- [ ] **Step 6:** `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ScheduleProposalRezimRenderTests"`. Expected: FAIL.

- [ ] **Step 7: Markup** `_EditZaznamForm.cshtml`.
  - Infobar hned pod blokem sdělení autora (Task 4):

```cshtml
@if (Model.IsProposalDecisionDetail && Model.ProposalChangesHarmonogramRezim)
{
    <gov-infobar color="error" type="bold" closable="false" data-proposal-rezim-infobar>
        <gov-icon slot="icon" type="components" name="exclamation-triangle-fill"></gov-icon>
        @Model.ProposalRezimChangeMessage
    </gov-infobar>
}
```

  - Přepínač: přidej třídu a atribut složené ručně (Razor nevynechá `data-*` s null):

```cshtml
@{
    var rezimSwitchClass = "record-editor-tabs-rezim-switch"
        + (Model.IsProposalDecisionDetail && Model.ProposalChangesHarmonogramRezim ? " proposal-field-changed" : "");
    var rezimOriginalAttr = Model.IsSchedulePlanProposalEditor
        ? $"data-record-rezim-original=\"{(Model.HarmonogramAutoFillSwitchOn ? "Auto" : "Manual")}\""
        : "";
}
<gov-form-switch size="m"
                 class="@rezimSwitchClass"
                 data-record-rezim-switch
                 @Html.Raw(rezimOriginalAttr)
                 …zbytek atributů beze změny…>
```

  - `rezim-master-switch.js`, v `handleChange` za `applyRezimToCells(form, rezim);`:

```js
// Editor návrhu (uživatel 2026-10-08): přepínač přepnutý opačně než na záznamu se podbarví
// stejně jako ostatní změněná pole návrhu.
const original = sw.getAttribute('data-record-rezim-original');
if (original) {
  sw.classList.toggle('proposal-field-changed', original !== rezim);
}
```

  - `site.css`: `.record-editor-tabs-rezim-switch.proposal-field-changed { display: inline-flex; }` (podbarvení a padding dodá `.proposal-field-changed`).

- [ ] **Step 8:** Spusť testy z kroku 6. Expected: PASS.

- [ ] **Step 9: E2E** (doplnit do `ScheduleProposalGuardScenariosTests`):

```csharp
[Fact]
public async Task EditorNavrhu_PrepnutiRezimu_PodbarviPrepinac_ZpetOdbarvi()
{
    var recordId = await CreateTaskRecordAsync();
    var page = await _fixture.NewPageAsync();
    try
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/Navrhy/CreateScheduleProposal?projektId={_fixture.ProjectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var sw = page.Locator("gov-form-switch[data-record-rezim-switch]");
        await Expect(sw).ToHaveAttributeAsync("data-record-rezim-original", "Auto");

        // gov-change s detail.checked je autoritativní vstup handleru (rezim-master-switch.js).
        await sw.DispatchEventAsync("gov-change", new { detail = new { @checked = false } });
        await Expect(sw).ToHaveClassAsync(new Regex("proposal-field-changed"));
        await sw.DispatchEventAsync("gov-change", new { detail = new { @checked = true } });
        await Expect(sw).Not.ToHaveClassAsync(new Regex("proposal-field-changed"));
    }
    finally
    {
        await page.Context.CloseAsync();
        await DeleteRecordAsync(recordId);
    }
}
```

  Pokud `DispatchEventAsync` s `detail` nevytvoří `CustomEvent` (Playwright .NET vytváří podle typu události), použij `page.EvaluateAsync("sel => document.querySelector(sel).dispatchEvent(new CustomEvent('gov-change', { bubbles: true, detail: { checked: false } }))", "gov-form-switch[data-record-rezim-switch]")` a zapiš to jako Ruling.

- [ ] **Step 10:** `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ScheduleProposalGuardScenariosTests"`. Expected: PASS 3/3.
- [ ] **Step 11: Commit:** `feat(navrhy): schvalovatel vidí režim vyplňování z návrhu a infobar při změně; přepínač v návrhu se podbarví`.

---

### Task 7: Dokumentace, ruční ověření, celé sady

- [ ] **Step 1: Dokumentace.**
  - `docs/known-issues/2026-10-08-navrh-harmonogramu-opravy.md`: nahoře „Stav: opraveno 2026-10-12 (commity …)“; u každého bodu jedna věta, jak je vyřešený; „Související vyjasnění“ doplň: důvod se nově zapisuje při schválení návrhu, dřív schválení historii termínu nezapisovalo vůbec.
  - `docs/wiki/projekty/navrhy/navrhnout-zmenu-planu.md`: odstavce „Důvod změny“ (nepovinné pole, kdo ho vidí, kam se uloží), „Když už návrh čeká“ (dialog, odkaz na čekající návrh), „Automatické vyplňování v návrhu“ (přepínač se uloží, po schválení provede, podbarvení, infobar u schvalovatele).
- [ ] **Step 2: Ruční ověření v aplikaci** (lokální DB, env proměnné z memory, `?asUser=1`): založit návrh s důvodem a přepnutým režimem → druhý pokus z menu karty i ze stránky záznamu ukáže dialog → detail návrhu ukáže sdělení autora a infobar → schválit → `SELECT TOP 1 * FROM dbo.zaznam_historie_terminu WHERE zaznam_id = … ORDER BY id DESC` má důvod, kroky 1/3/4/6/7/10 mají `skutecnost_rezim` podle návrhu. Testovací data po ověření smazat.
- [ ] **Step 3: Celé sady:**

```bash
dotnet test PmTracker.Tests.Unit 2>&1 | tail -1
npm test 2>&1 | grep -E "^ℹ (pass|fail)"
dotnet test PmTracker.Tests.Api 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RecordProposal|FullyQualifiedName~ScheduleProposal|FullyQualifiedName~Search|FullyQualifiedName~RichText" 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~Proposal|FullyQualifiedName~Search|FullyQualifiedName~Header" 2>&1 | grep -E "^\s+Neúspěšné |Úspěšné!|Neúspěšné!"
```

  Expected: Unit, JS, Api a Integration bez selhání; E2E jen známá pre-existing selhání (porovnat jména proti baseline).
- [ ] **Step 4: Commit dokumentace:** `docs(navrhy): dialog při čekajícím návrhu, důvod změny, režim vyplňování v návrhu`.
- [ ] **Step 5: Report uživateli** česky: co se změnilo (kde je pole důvodu, kde ho vidí schvalovatel, dialog, režim), čísla testů, že nasazení nepotřebuje SQL skript, `main` nepushnutý, co ručně vyzkoušet na i15 (Edge, Ctrl+F5 kvůli JS modulům).
