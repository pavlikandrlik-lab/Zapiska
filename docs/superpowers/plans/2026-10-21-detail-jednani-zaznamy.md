# Detail jednání se záznamy — plán implementace

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: použij superpowers:executing-plans (inline, po úkolech, bez subagentů). Kroky používají checkboxy (`- [ ]`). Commity do větve `feat/detail-jednani-zaznamy`, na konci merge do lokálního `main`, bez pushe. Task 8 je **volitelný** a provádí se jen na pokyn uživatele.

**Cíl:**
- **Jednání v přípravě nebo otevřené:** v detailu se ukáže stránka Záznamy (filtry + karty). Vyjádření i změna stavu mají jako výchozí volbu toto jednání, takže PM nebo admin projektu dopisuje vyjádření přímo při jednání.
- **Uzavřené jednání:** jen úzký pruh se stavem a pod ním zápis jako v PDF tisku.

**Architektura:**
- **Jedna komponenta záložky Záznamy.** Detail jednání vykreslí stejný partial `_ProjectRecordsTab` jako detail projektu. Přípravu odkazů a oprávnění karet z `ProjektyController` přesune Task 1 do sdíleného `BaseController.RecordsTab.cs`.
- **Kontext jednání** (`kontextJednaniId`) je jen parametr přípravy. Doplní ho do odkazu tužky (`Edit?jednaniId=…&uiContext=meeting`), do adresy vyjádření karty (`RecordCommentsPartial?…&kontextJednaniId=…`), do „Nový záznam“ a do obnovení záložky.
- **Výchozí volby:**
  - Vyjádření předvybere jednání z kontextu, jen když je v nabídce povolených jednání.
  - Změna stavu předvybere jednání čistou funkcí `StatusChangeEffectiveDatePolicy.PreferMeeting`.
  - Pravidla nabídky a ověření na serveru se nemění.
- **Uzavřené jednání:** zápis je HTML šablona tisku (`PdfTemplate.cshtml`, `autoPrint=false`) z nové akce `Export/Jednani/{id}/Nahled` v `iframe`. Je to stejný obsah jako PDF, jen bez generování PDF.
- **Filtry v jednání** mají vlastní klíč v `sessionStorage` (`pmtracker.meetingFilters.v1.jednani-{id}.state`) a žádné uložené výchozí hodnoty.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, ES moduly (`wwwroot/js/modules`), xUnit + FluentAssertions, Api testy (`ApiSqlFixture`), JS testy `node --test`, E2E Playwright .NET.

**Spec:** samostatný spec není. Zadání a rozhodnutí z konverzace 2026-10-21 jsou v sekci níže. Rozšiřuje spec `docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md` (výchozí volba §4.3) o kontext jednání.

## Zadání a rozhodnutí (konverzace 2026-10-21)

| # | Rozhodnutí |
|---|---|
| R1 | Detail jednání v **přípravě nebo otevřeného** obsahuje „v podstatě kopii stránky Záznamy“: filtry + karty záznamů. |
| R2 | Karty Účast a Stav jednání zůstávají, ale **kompaktně nahoře**: úzký pruh se stavem a akcemi, účast rozbalitelná. |
| R3 | **Uzavřené jednání:** jen kompaktní pruh se stavem a pod ním **zápis jako v PDF tisku**. Je to zkratka, aby se nemuselo klikat na Tisk. Účast se nezobrazuje, je v zápisu. |
| R4 | **Vyjádření** na kartě: výběr „Vazba na jednání“ má jako výchozí jednání, ze kterého uživatel přišel. Ostatní volby zůstávají na kliknutí. |
| R5 | **Změna stavu** přes tužku z detailu jednání: „Změna stavu platí k“ má jako výchozí toto jednání. Ostatní volby zůstávají. |
| R6 | **Filtry** jsou jen lokální pro jednání, bez ukládání výchozích hodnot. Nanejvýš se drží pro dané jednání v relaci prohlížeče. |
| R7 | **Žádné** značky „má / nemá vyjádření k tomuto jednání“ ani takový filtr. Udělá se později jinak. |
| R8 | Uživatel chce v plánu prozkoumat **pevnou volbu** (jiné jednání nejde vybrat). Analýza je v Task 8, implementace jen na pokyn. |

Doplněno v návrhu (k potvrzení při revizi plánu):

| # | Doplnění | Proč |
|---|---|---|
| D1 | Kontext je **jen předvolba**. Serverová pravidla (`CommentAuthorizationPolicy`, nabídka a ověření `StatusChangeEffectiveDatePolicy`) se nemění. | Žádná nová cesta, jak obejít pravidla |
| D2 | Jednání z kontextu, které nepatří k projektu záznamu nebo není v nabídce, se **tiše ignoruje**: platí dnešní výchozí volba. | Podvržené nebo zastaralé id nesmí nic rozbít |
| D3 | Uzavřené jednání se pozná sdíleným `MeetingStatePolicy.IsReadOnly`: kód `CLOSED`, název „uzav…“ nebo zamčené jednání. | Stejné pravidlo jako nabídka vyjádření a změny stavu |
| D4 | V pruhu jednání je **„Nový záznam“**, který založí záznam k tomuto jednání (`Create?jednaniId=…&uiContext=meeting`, to už umí). „Nový návrh“ a „Tisk projektu“ z lišty projektu se nepřebírají. | Akce patřící k jednání |
| D5 | „Otevřít na nové kartě“ (stránka záznamu) vrací šipkou zpět do detailu jednání. | Konzistentní návrat |
| D6 | Zápis uzavřeného jednání vidí ten, kdo smí tisknout jednání (`export.pdf.jednani`, mají ho všechny projektové role). | Stejná pravidla jako tisk |

## Global Constraints

- Chování záložky Záznamy v **detailu projektu** se nemění. Kontext je volitelný parametr s výchozí hodnotou `null`.
- Kontext se předává jen parametry: `kontextJednaniId` (záložka, vyjádření, příkazy vyjádření) a `jednaniId` + `uiContext=meeting` (editor, stejně jako dnešní `Create`).
- Razor: `data-*="@null"` atribut nevynechá. Volitelné `data-*` skládat přes `Html.Raw` (memory). Žádné `@* *@` uvnitř tagu s TagHelpery.
- CSS: barvy jen z gov / `--app-*` tokenů (`AppCssColorTokenTests`). Žádné nové `:has`, `max-content` ani pravidla `[data-theme]` (Edge na i15).
- JS: nový modul s vedlejším efektem musí být explicitně importovaný v `wwwroot/js/modules/bootstrap.js` (memory „ESM site.js — side-effect imports“). Po změně JS ověřit v prohlížeči s vypnutou cache.
- Api HTML testy kotví na atributy a URL. Text přes `WebUtility.HtmlDecode`, protože Razor kóduje diakritiku.
- Testy oprávnění běží za osobu **jen s projektovou rolí** (PM), ne za admina.
- E2E: vlastní jednání s číslem **pod 9820** a datem **ne dnes** (memory: jinak odsune jednání jiného scénáře do sbalené historie).
- Unit, Api a JS sady zelené. E2E porovnáním jmen selhání proti baseline `main`.
- Po dokončení sekce 4 v `docs/k-otestovani.md` (stále ten jeden soubor).

## Review Focus

1. **Obnovení vyjádření po uložení.** Po „Uložit“ se panel vyjádření načte znovu ze serveru (`refreshUrl`). Bez kontextu v `refreshUrl` by se předvolba ztratila. Task 3 test čte `refreshUrl` z odpovědi AJAX.
2. **Jednání z kontextu u záznamu nepovolené.** Například zástupce vedoucího u cizího subsystému nebo otevřené jednání tam, kde smí jen přípravu: nic se nepředvybere. Task 3 test.
3. **Jednání z kontextu mimo nabídku změny stavu** (spodní mez, za uzavřeným jednáním): platí výchozí volba podle §4.3. Task 4 unit testy.
4. **Cizí jednání v kontextu** (id jednání jiného projektu): ignoruje se v záložce i v editoru. Task 2 a Task 4 testy.
5. **Oddělení filtrů:** filtry v jednání nepřepíšou filtry záložky projektu a naopak, a v jednání se neukládají výchozí hodnoty. Task 6 JS test a ruční průchod.

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Controllers/BaseController.RecordsTab.cs` | **nový:** příprava záložky Záznamy (přesun z `ProjektyController`) + kontext jednání |
| `PmTracker.Web/Controllers/ProjektyController.cs`, `ProjektyController.TabPartials.cs` | volání sdílené přípravy; `RecordsTabPartial(id, kontextJednaniId)` |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` | `KontextJednaniId`, `EditRecordUrl`, `ZaznamCommentsPanelViewModel.KontextJednaniId/PredvybraneJednaniId` |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs` | `StorageId`, `AllowSaveDefaults` |
| `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml`, `_ProjectFilterShell.cshtml`, `_ZaznamCommentsPartial.cshtml` | kontextové odkazy, atribut úložiště, předvolba |
| `PmTracker.Web/Controllers/ZaznamyController.Partials.cs`, `ZaznamyController.Commands.cs`, `ZaznamyController.cs` | kontext ve vyjádřeních a v editoru |
| `PmTracker.Web/Services/Records/IRecordUiFlowResolver.cs`, `RecordUiFlowResolver.cs` | `kontextJednaniId` v obnovení vyjádření |
| `PmTracker.Web/Services/Records/StatusChangeEffectiveDatePolicy.cs` | `PreferMeeting` |
| `PmTracker.Web/Services/MeetingService.DetailQueries.cs`, `Models/ViewModels/JednaniViewModels.cs` | `IsReadOnly`, `ZaznamyTab`, `ZapisNahledUrl` |
| `PmTracker.Web/Controllers/JednaniController.cs` | sestavení záložky / odkazu na zápis |
| `PmTracker.Web/Controllers/ExportController.cs` | `JednaniNahled` + sdílená kontrola jednání |
| `PmTracker.Web/Views/Jednani/Detail.cshtml` | kompaktní pruh, záznamy / zápis |
| `PmTracker.Web/wwwroot/css/site.css` | `.meeting-strip*`, `.meeting-minutes-frame` |
| `PmTracker.Web/wwwroot/js/modules/meetingMinutes.js` + import v `bootstrap.js` | výška `iframe` podle obsahu |
| `PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js` | `buildProjectFilterStorageKey` + klíč pro jednání |
| testy | viz jednotlivé tasky |
| `docs/k-otestovani.md` | sekce 4 |

---

### Task 1: Sdílená příprava záložky Záznamy (přesun bez změny chování)

**Files:**
- Create: `PmTracker.Web/Controllers/BaseController.RecordsTab.cs`
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (odstranit `PrepareProjectRecordsTabPresentationAsync` ř. ~226–258 a `PrepareRecordCardShellPresentation` ř. ~260–286, volání ř. ~159)
- Modify: `PmTracker.Web/Controllers/ProjektyController.TabPartials.cs:18`

**Interfaces:**
- Produces: `protected Task PrepareRecordsTabPresentationAsync(ProjektZaznamyTabViewModel model, IRecordProposalService recordProposalService, CancellationToken ct)` v `BaseController` (Task 2 přidá parametr `int? kontextJednaniId`).

Čistý přesun (refaktor): testy, které ho hlídají, už existují. RED tu není, kontrakt je „celá Api sada zelená před i po“.

- [ ] **Step 1: Baseline Api sady**

Run: `dotnet test PmTracker.Tests.Api --nologo > <workspace>/t1-before.log 2>&1; tail -1 <workspace>/t1-before.log`
Expected: `Passed! … 490`.

- [ ] **Step 2: Přesun**

`BaseController.RecordsTab.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    private const string RecordsTabKey = "zaznamy";

    /// <summary>
    /// Příprava záložky Záznamy (oprávnění, odkazy karet). Sdílí detail projektu
    /// a od 2026-10-21 detail jednání (plán 2026-10-21-detail-jednani-zaznamy).
    /// </summary>
    protected async Task PrepareRecordsTabPresentationAsync(
        ProjektZaznamyTabViewModel model,
        IRecordProposalService recordProposalService,
        CancellationToken ct)
    {
        // TĚLO = doslovná kopie ProjektyController.PrepareProjectRecordsTabPresentationAsync s těmito záměnami:
        //   _recordProposalService                      → recordProposalService
        //   Url.Action(nameof(RecordsTabPartial), …)     → Url.Action("RecordsTabPartial", "Projekty", …)
        //   Url.Action(nameof(RecordMeetingCommentStates), …) → Url.Action("RecordMeetingCommentStates", "Projekty", …)
    }

    private void PrepareRecordCardShellPresentation(ProjektZaznamCardShellViewModel record, int projectId)
    {
        // TĚLO = doslovná kopie ProjektyController.PrepareRecordCardShellPresentation;
        //   ProjektDetailTabUrl(projectId, RecordsTab) → ProjektDetailTabUrl(projectId, RecordsTabKey)
    }
}
```

(Komentáře „TĚLO = …“ do souboru nepatří. Jsou to instrukce pro přesun, tělo se zkopíruje doslova z `ProjektyController.cs:226-286`.) V `ProjektyController` obě metody smaž a obě volání nahraď `await PrepareRecordsTabPresentationAsync(<model>, _recordProposalService, ct);`. Pozor: v kontextu `BaseController` musí `Url.Action` na akce `ProjektyController` uvádět controller `"Projekty"` (jinak by se použil ambientní controller volajícího, tedy `Jednani`).

- [ ] **Step 3: Api sada po přesunu + architektura**

Run: `dotnet test PmTracker.Tests.Api --nologo > <workspace>/t1-after.log 2>&1; tail -1 <workspace>/t1-after.log` a `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Architecture" --nologo`
Expected: Api `Passed! … 490`, Unit Architecture PASS (včetně `ProjectScopedPolicyRequestCarriesProjektIdTests`).

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Controllers/BaseController.RecordsTab.cs PmTracker.Web/Controllers/ProjektyController.cs PmTracker.Web/Controllers/ProjektyController.TabPartials.cs
git commit -m "refactor(zaznamy): příprava záložky Záznamy sdílená v BaseController"
```

---

### Task 2: Kontext jednání v záložce Záznamy

**Files:**
- Modify: `BaseController.RecordsTab.cs`, `ProjektyController.TabPartials.cs` (`RecordsTabPartial`), `ProjektyController.cs` (volání s `null`)
- Modify: `Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs`, `Models/ViewModels/Projekty/ProjectFilterShellViewModel.cs`
- Modify: `Views/Projekty/_ZaznamPartial.cshtml:31`, `Views/Projekty/_ProjectFilterShell.cshtml:7-10, 97`
- Test: `PmTracker.Tests.Api/Controllers/MeetingContextRecordsTabRenderTests.cs`

**Interfaces:**
- Consumes: `PrepareRecordsTabPresentationAsync` (Task 1).
- Produces:
  - `PrepareRecordsTabPresentationAsync(ProjektZaznamyTabViewModel model, IRecordProposalService recordProposalService, int? kontextJednaniId, CancellationToken ct)`,
  - `ProjektZaznamyTabViewModel.KontextJednaniId` (`int?`),
  - `ProjektZaznamCardShellViewModel.EditRecordUrl` (`string?`),
  - `ProjectFilterShellViewModel.StorageId` (`string?`, set) a `AllowSaveDefaults` (`bool`, set, default `true`),
  - `GET /Projekty/RecordsTabPartial/{id}?kontextJednaniId=` (pro Task 5 a obnovení).

- [ ] **Step 1: Padající Api testy**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Plán 2026-10-21 (detail jednání se záznamy): záložka Záznamy s kontextem jednání —
/// tužka, vyjádření, Nový záznam a obnovení nesou jednání; filtry mají vlastní klíč bez výchozích.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingContextRecordsTabRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingContextRecordsTabRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int PmId, int ProjectId, int RecordId, int MeetingId)> SeedAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var meetingId = await _fixture.EnsureMeetingAsync(projectId, 9401);
        return (pmId, projectId, recordId, meetingId);
    }

    private async Task<string> TabHtmlAsync(int projectId, int asUser, int? kontextJednaniId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var context = kontextJednaniId.HasValue ? $"&kontextJednaniId={kontextJednaniId}" : string.Empty;
        var response = await client.GetAsync($"/Projekty/RecordsTabPartial/{projectId}?asUser={asUser}{context}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    [Fact]
    public async Task WithMeetingContext_CardAndTabLinksCarryMeeting()
    {
        var s = await SeedAsync("ApiMtgTab1");

        var html = await TabHtmlAsync(s.ProjectId, s.PmId, s.MeetingId);

        html.Should().Contain($"/Zaznamy/Edit/{s.RecordId}?projektId={s.ProjectId}&jednaniId={s.MeetingId}&uiContext=meeting",
            "tužka otevře editor v kontextu jednání");
        html.Should().Contain($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&kontextJednaniId={s.MeetingId}");
        html.Should().Contain($"data-project-tab-refresh-url=\"/Projekty/RecordsTabPartial/{s.ProjectId}?kontextJednaniId={s.MeetingId}\"");
        html.Should().Contain($"data-project-filter-storage-id=\"jednani-{s.MeetingId}\"");
        html.Should().NotContain("data-filter-save-defaults", "v jednání se výchozí filtry neukládají");
    }

    [Fact]
    public async Task WithoutContext_TabUnchanged()
    {
        var s = await SeedAsync("ApiMtgTab2");

        var html = await TabHtmlAsync(s.ProjectId, s.PmId, null);

        html.Should().NotContain("uiContext=meeting");
        html.Should().NotContain("kontextJednaniId");
        html.Should().NotContain("data-project-filter-storage-id");
        html.Should().Contain("data-filter-save-defaults");
    }

    [Fact]
    public async Task ContextOfForeignProject_IsIgnored()
    {
        var s = await SeedAsync("ApiMtgTab3");
        var other = await SeedAsync("ApiMtgTab4");

        var html = await TabHtmlAsync(s.ProjectId, s.PmId, other.MeetingId);

        html.Should().NotContain("kontextJednaniId", "jednání jiného projektu se jako kontext ignoruje");
        html.Should().NotContain("uiContext=meeting");
    }
}
```

(Pořadí parametrů v URL odpovídá pořadí v anonymním objektu `Url.Action` z kroku 3. Pokud by se lišilo, uprav kotvy testu, ne pořadí, a zapiš ruling.)

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingContextRecordsTabRenderTests" --nologo`
Expected:
- `WithMeetingContext_CardAndTabLinksCarryMeeting`: FAIL, odkaz tužky bez `jednaniId`.
- Zbylé dva: PASS už teď (kontext se dnes ignoruje). Hlídají, že se po změně nic nerozbije.

- [ ] **Step 3: Implementace**

View modely:

```csharp
// ProjektZaznamyTabViewModel
/// <summary>Jednání, ze kterého se záložka zobrazuje (detail jednání); null = detail projektu.</summary>
public int? KontextJednaniId { get; set; }

// ProjektZaznamCardShellViewModel
/// <summary>Cíl tužky. Null = výchozí odkaz ve view (záznam mimo záložku Záznamy).</summary>
public string? EditRecordUrl { get; set; }

// ProjectFilterShellViewModel
/// <summary>Vlastní klíč úložiště filtrů (detail jednání: „jednani-{id}“); null = klíč projektu.</summary>
public string? StorageId { get; set; }
/// <summary>False = bez tlačítka „Uložit jako výchozí“ (filtry jednání jsou jen v relaci).</summary>
public bool AllowSaveDefaults { get; set; } = true;
```

`BaseController.RecordsTab.cs`: přidej parametr `int? kontextJednaniId` (před `ct`) a:

```csharp
        model.KontextJednaniId = kontextJednaniId;
        model.CreateRecordEditorUrl = kontextJednaniId.HasValue
            ? Url.Action("Create", "Zaznamy", new { projektId = projectId, jednaniId = kontextJednaniId, uiContext = "meeting" })
                ?? $"/Zaznamy/Create?projektId={projectId}&jednaniId={kontextJednaniId}&uiContext=meeting"
            : Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.RefreshUrl = Url.Action("RecordsTabPartial", "Projekty", new { id = projectId, kontextJednaniId })
            ?? $"/Projekty/RecordsTabPartial/{projectId}";
        if (kontextJednaniId.HasValue)
        {
            model.FilterShell.StorageId = $"jednani-{kontextJednaniId.Value}";
            model.FilterShell.AllowSaveDefaults = false;
        }
```

(Nahrazuje dosavadní přiřazení `CreateRecordEditorUrl` a `RefreshUrl`.) `PrepareRecordCardShellPresentation(record, projectId, kontextJednaniId)`:

```csharp
        record.CommentsUrl = Url.Action("RecordCommentsPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id, kontextJednaniId })
            ?? $"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={summary.Id}";
        var recordPageReturnUrl = kontextJednaniId.HasValue
            ? Url.Action("Detail", "Jednani", new { id = kontextJednaniId.Value }) ?? $"/Jednani/Detail/{kontextJednaniId.Value}"
            : ProjektDetailTabUrl(projectId, RecordsTabKey);
        record.RecordPageUrl = Url.Action("Detail", "Zaznamy", new { id = summary.Id, returnUrl = recordPageReturnUrl }) ?? $"/Zaznamy/Detail/{summary.Id}";
        record.EditRecordUrl = Url.Action("Edit", "Zaznamy", new
        {
            id = summary.Id,
            projektId = projectId,
            jednaniId = kontextJednaniId,
            uiContext = kontextJednaniId.HasValue ? "meeting" : null
        }) ?? $"/Zaznamy/Edit/{summary.Id}";
```

`ProjektyController`: volání v `Detail` s `kontextJednaniId: null`. `RecordsTabPartial`:

```csharp
    public async Task<IActionResult> RecordsTabPartial(int id, int? kontextJednaniId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        // Kontext jen pro jednání tohoto projektu — cizí id se ignoruje (předvolba, ne oprávnění).
        if (kontextJednaniId.HasValue
            && await _meetingService.GetMeetingProjectIdAsync(kontextJednaniId.Value, ct) != id)
        {
            kontextJednaniId = null;
        }

        var model = await _projectService.BuildProjectRecordsTabAsync(id, ct);
        await PrepareRecordsTabPresentationAsync(model, _recordProposalService, kontextJednaniId, ct);
        return PartialView("~/Views/Projekty/_ProjectRecordsTab.cshtml", model);
    }
```

`_ZaznamPartial.cshtml:31`:

```cshtml
    var editRecordUrl = Model.EditRecordUrl ?? Url.Action("Edit", "Zaznamy", new { id = summary.Id, projektId }) ?? $"/Zaznamy/Edit/{summary.Id}";
```

`_ProjectFilterShell.cshtml`: do kořenového tagu (za `data-project-zkratka="@Model.Zkratka"`) přidej volitelný atribut a tlačítko výchozích obal podmínkou:

```cshtml
     data-project-zkratka="@Model.Zkratka"@Html.Raw(Model.StorageId is null ? string.Empty : $" data-project-filter-storage-id=\"{System.Net.WebUtility.HtmlEncode(Model.StorageId)}\"")>
```

```cshtml
            @if (Model.AllowSaveDefaults)
            {
                <pm-button variant="Ghost" size="Small" data-filter-save-defaults="@Model.Scope">Uložit jako výchozí</pm-button>
            }
```

(Kořenový tag `_ProjectFilterShell` je obyčejný HTML tag bez TagHelperů. Ověř, že je, jinak `Html.Raw` v tagu nefunguje. Pak zapiš ruling a použij podmíněné vykreslení celého otevíracího tagu.)

- [ ] **Step 4: Spusť, musí projít + sousední testy**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingContextRecordsTabRenderTests|FullyQualifiedName~Project|FullyQualifiedName~Record" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A PmTracker.Web PmTracker.Tests.Api/Controllers/MeetingContextRecordsTabRenderTests.cs
git commit -m "feat(jednani-zaznamy): kontext jednání v záložce Záznamy (tužka, vyjádření, Nový záznam, filtry)"
```

---

### Task 3: Vyjádření s předvoleným jednáním

**Files:**
- Modify: `Controllers/ZaznamyController.Partials.cs` (`RecordCommentsPartial`, `PrepareRecordCommentsPresentationAsync`)
- Modify: `Controllers/ZaznamyController.Commands.cs` (`AddComment`, `UpdateComment`, `DeleteComment`, `BuildCommentAjaxSuccessResult`)
- Modify: `Services/Records/IRecordUiFlowResolver.cs`, `Services/Records/RecordUiFlowResolver.cs`
- Modify: `Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` (`ZaznamCommentsPanelViewModel`)
- Modify: `Views/Projekty/_ZaznamCommentsPartial.cshtml` (formuláře přidání ř. ~148, úpravy ř. ~92, smazání ř. ~77)
- Test: `PmTracker.Tests.Unit/Records/RecordUiFlowResolverTests.cs`, `PmTracker.Tests.Api/Controllers/MeetingContextCommentsTests.cs`

**Interfaces:**
- Consumes: `CommentsUrl` s `kontextJednaniId` (Task 2).
- Produces:
  - `RecordCommentsPartial(int projektId, int zaznamId, int? limit, bool loadAll = false, int? kontextJednaniId = null, CancellationToken ct = default)`,
  - `IRecordUiFlowResolver.ResolveCommentAjaxFlow(string? uiContext, int projektId, int zaznamId, int? meetingId, int? kontextJednaniId = null)`,
  - `ZaznamCommentsPanelViewModel.KontextJednaniId` a `PredvybraneJednaniId` (`int?`, set).

- [ ] **Step 1: Padající unit test resolveru**

Do `RecordUiFlowResolverTests`:

```csharp
    [Fact]
    public void ResolveCommentAjaxFlow_ProjectRefresh_KeepsMeetingContext()
    {
        var result = _sut.ResolveCommentAjaxFlow("project", 17, 31, null, kontextJednaniId: 55);

        result.RefreshScope.Should().Be("record-comments");
        result.RouteValues["kontextJednaniId"].Should().Be(55, "obnovený panel vyjádření musí dál předvybírat jednání");
    }
```

- [ ] **Step 2: Padající Api testy**

`PmTracker.Tests.Api/Controllers/MeetingContextCommentsTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Plán 2026-10-21: vyjádření v detailu jednání předvybírá to jednání; jiné volby zůstávají.
/// Předvolba jen z povolené nabídky (CommentAuthorizationPolicy) a přežije obnovení po uložení.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingContextCommentsTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingContextCommentsTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int PmId, int DeputyId, int RecordId, int DraftId, int OpenId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", meetingBase);
        var openId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase + 1);
        return new Scenario(projectId, pmId, deputyId, recordId, draftId, openId);
    }

    private async Task<string> CommentsHtmlAsync(Scenario s, int asUser, int? kontextJednaniId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var context = kontextJednaniId.HasValue ? $"&kontextJednaniId={kontextJednaniId}" : string.Empty;
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={asUser}{context}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private static bool IsSelected(string html, int meetingId) =>
        Regex.IsMatch(html, $"<option value=\"{meetingId}\" selected=\"selected\"");

    [Fact]
    public async Task ProjectManager_ContextMeeting_IsPreselected_OthersStayOffered()
    {
        var s = await SeedAsync("ApiMtgCom1", 9411);

        var html = await CommentsHtmlAsync(s, s.PmId, s.OpenId);

        IsSelected(html, s.OpenId).Should().BeTrue();
        html.Should().Contain($"<option value=\"{s.DraftId}\">", "jiné jednání jde vybrat na kliknutí");
        html.Should().Contain($"name=\"kontextJednaniId\" value=\"{s.OpenId}\"");
    }

    [Fact]
    public async Task DraftMeetingContext_PreselectsPreparation()
    {
        var s = await SeedAsync("ApiMtgCom2", 9421);

        var html = await CommentsHtmlAsync(s, s.PmId, s.DraftId);

        IsSelected(html, s.DraftId).Should().BeTrue();
    }

    [Fact]
    public async Task ContextMeetingNotAllowedForUser_NothingPreselected()
    {
        // Zástupce vedoucího smí jen DRAFT (rozhodnutí 2026-06-16) → OPEN z kontextu se nenabízí ani nepředvybere.
        var s = await SeedAsync("ApiMtgCom3", 9431);

        var html = await CommentsHtmlAsync(s, s.DeputyId, s.OpenId);

        html.Should().NotContain("selected=\"selected\"");
        html.Should().NotContain($"<option value=\"{s.OpenId}\"");
    }

    [Fact]
    public async Task WithoutContext_NothingPreselected()
    {
        var s = await SeedAsync("ApiMtgCom4", 9441);

        var html = await CommentsHtmlAsync(s, s.PmId, null);

        html.Should().NotContain("selected=\"selected\"");
        html.Should().NotContain("kontextJednaniId");
    }

    [Fact]
    public async Task AddComment_WithContext_RefreshUrlKeepsContext()
    {
        var s = await SeedAsync("ApiMtgCom5", 9451);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AddComment?projektId={s.ProjectId}&asUser={s.PmId}",
            ApiTestHttpHelper.BuildForm(
                ("ZaznamId", s.RecordId.ToString()),
                ("JednaniId", s.OpenId.ToString()),
                ("Text", "API vyjadreni z jednani"),
                ("uiContext", "project"),
                ("kontextJednaniId", s.OpenId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("record-comments");
        payload.RefreshUrl.Should().Contain($"kontextJednaniId={s.OpenId}");
    }
}
```

(Ověř název vlastnosti `RefreshUrl` v návratovém typu `ApiTestHttpHelper.ReadModalResultAsync`. Pokud se jmenuje jinak, uprav test a zapiš ruling.)

- [ ] **Step 3: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordUiFlowResolverTests" --nologo` a `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingContextCommentsTests" --nologo`
Expected:
- Unit: FAIL na kompilaci (resolver nemá parametr `kontextJednaniId`).
- Api:
  - FAIL: `ProjectManager_ContextMeeting_IsPreselected…`, `DraftMeetingContext_…` a `AddComment_WithContext_…`.
  - PASS: `ContextMeetingNotAllowed…` a `WithoutContext_…`.

- [ ] **Step 4: Implementace**

Resolver: rozhraní i třída dostanou `int? kontextJednaniId = null`. V projektové větvi:

```csharp
            RouteValues = new RouteValueDictionary(new { projektId, zaznamId, kontextJednaniId }),
```

`ZaznamCommentsPanelViewModel`:

```csharp
    /// <summary>Jednání, ze kterého je panel otevřený (detail jednání); null = bez kontextu.</summary>
    public int? KontextJednaniId { get; set; }
    /// <summary>Předvolba výběru „Vazba na jednání“ — kontext, jen pokud je v povolené nabídce.</summary>
    public int? PredvybraneJednaniId { get; set; }
```

`RecordCommentsPartial`: nový parametr `int? kontextJednaniId = null` (za `loadAll`). Před `PrepareRecordCommentsPresentationAsync` nastav `model.KontextJednaniId = kontextJednaniId;`. Na konec `PrepareRecordCommentsPresentationAsync` přidej:

```csharp
        model.PredvybraneJednaniId = model.KontextJednaniId is int kontext && model.OtevrenaJednani.Any(x => x.Id == kontext)
            ? kontext
            : null;
```

`ZaznamyController.Commands.cs`: `AddComment`, `UpdateComment` a `DeleteComment` dostanou `int? kontextJednaniId` (za `meetingId`). `BuildCommentAjaxSuccessResult(…, string message, int? kontextJednaniId = null)` ho předá do `ResolveCommentAjaxFlow(uiContext, projektId, zaznamId, meetingId, kontextJednaniId)`. Všechna tři volání ho předávají.

`_ZaznamCommentsPartial.cshtml`:
- Ve formuláři přidání u `<option>`: `<option value="@jednani.Id" selected="@(jednani.Id == Model.PredvybraneJednaniId)">@jednani.Label</option>`.
- Do formulářů přidání, úpravy a smazání vyjádření (všechny s `asp-controller="Zaznamy"`) přidej:

```cshtml
            @if (Model.KontextJednaniId.HasValue)
            {
                <input type="hidden" name="kontextJednaniId" value="@Model.KontextJednaniId" />
            }
```

(Formuláře úpravy a smazání: ověř, že `Model` je v jejich scope `ZaznamCommentsPanelViewModel`. Pokud jsou v cyklu přes `Model.Vyjadreni`, `Model.KontextJednaniId` je dostupný.)

- [ ] **Step 5: Spusť, musí projít + sousední testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordUiFlowResolverTests|FullyQualifiedName~Architecture" --nologo` a `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingContextCommentsTests|FullyQualifiedName~RecordCommentsFormAuthzRenderTests|FullyQualifiedName~ClosedRecordCommentsRenderTests|FullyQualifiedName~AjaxControllersTests" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A PmTracker.Web PmTracker.Tests.Unit/Records/RecordUiFlowResolverTests.cs PmTracker.Tests.Api/Controllers/MeetingContextCommentsTests.cs
git commit -m "feat(jednani-zaznamy): vyjádření předvybírá jednání z kontextu, obnovení po uložení ho drží"
```

---

### Task 4: Změna stavu v editoru otevřeném z jednání

**Files:**
- Modify: `Services/Records/StatusChangeEffectiveDatePolicy.cs` (`PreferMeeting`)
- Modify: `Controllers/ZaznamyController.cs` (`Edit`)
- Test: `PmTracker.Tests.Unit/Records/StatusChangeEffectiveDatePolicyTests.cs` (existující soubor, nebo nový `…PreferMeetingTests.cs`, pokud existující neexistuje), `PmTracker.Tests.Api/Controllers/RecordEditorStatusEffectiveDateTests.cs`

**Interfaces:**
- Consumes: odkaz tužky `Edit/{id}?projektId=&jednaniId=&uiContext=meeting` (Task 2).
- Produces: `public static StatusChangeEffectiveDateChoice PreferMeeting(StatusChangeEffectiveDateChoice choice, int meetingId)`; `Edit(int id, string? returnUrl, int? jednaniId = null, string? uiContext = null, CancellationToken ct = default)`.

- [ ] **Step 1: Padající unit testy**

(Najdi soubor s testy `StatusChangeEffectiveDatePolicy.Build` přes `grep -rl "StatusChangeEffectiveDatePolicy" PmTracker.Tests.Unit` a přidej do něj. Volání `Build(meetings, today, lastEffectiveDate, recordFoundedOn)` odpovídá produkčnímu podpisu.)

```csharp
    [Fact]
    public void PreferMeeting_MeetingInOffer_BecomesDefault()
    {
        var today = new DateTime(2026, 10, 21);
        var meetings = new[]
        {
            new StatusChangeMeetingCandidate(1, 888, new DateTime(2026, 10, 8), "OPEN", false),
            new StatusChangeMeetingCandidate(2, 887, new DateTime(2026, 10, 1), "OPEN", false),
        };
        var choice = StatusChangeEffectiveDatePolicy.Build(meetings, today, null, new DateTime(2026, 1, 1));
        choice.DefaultValue.Should().Be("1", "bez kontextu platí §4.3: nejnovější otevřené");

        var preferred = StatusChangeEffectiveDatePolicy.PreferMeeting(choice, 2);

        preferred.DefaultValue.Should().Be("2");
        preferred.Options.Should().BeEquivalentTo(choice.Options, "nabídka se nemění, jen výchozí volba");
    }

    [Fact]
    public void PreferMeeting_MeetingOutsideOffer_KeepsDefault()
    {
        var today = new DateTime(2026, 10, 21);
        var meetings = new[]
        {
            new StatusChangeMeetingCandidate(1, 888, new DateTime(2026, 10, 8), "OPEN", false),
            new StatusChangeMeetingCandidate(3, 886, new DateTime(2026, 9, 24), "CLOSED", true),
            new StatusChangeMeetingCandidate(4, 885, new DateTime(2026, 9, 17), "OPEN", false),
        };
        var choice = StatusChangeEffectiveDatePolicy.Build(meetings, today, null, new DateTime(2026, 1, 1));

        StatusChangeEffectiveDatePolicy.PreferMeeting(choice, 4).DefaultValue.Should().Be("1", "885 leží za uzavřeným 886 → není v nabídce");
        StatusChangeEffectiveDatePolicy.PreferMeeting(choice, 999).DefaultValue.Should().Be("1", "neznámé / cizí jednání");
    }

    [Fact]
    public void PreferMeeting_MeetingBelowLowerBound_KeepsDefault()
    {
        var today = new DateTime(2026, 10, 21);
        var meetings = new[]
        {
            new StatusChangeMeetingCandidate(1, 888, new DateTime(2026, 10, 8), "OPEN", false),
            new StatusChangeMeetingCandidate(2, 887, new DateTime(2026, 10, 1), "OPEN", false),
        };
        var choice = StatusChangeEffectiveDatePolicy.Build(meetings, today, new DateTime(2026, 10, 5), new DateTime(2026, 1, 1));

        StatusChangeEffectiveDatePolicy.PreferMeeting(choice, 2).DefaultValue.Should().Be("1", "887 (1.10.) je pod spodní mezí 5.10.");
    }
```

- [ ] **Step 2: Padající Api test editoru** (do `RecordEditorStatusEffectiveDateTests`)

```csharp
    [Fact]
    public async Task Edit_FromMeeting_PreselectsThatMeeting_AndReturnsToIt()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiStavPlatiKMtg");
        var projectId = await _fixture.EnsureProjectAsync("APISTAVPKMTG");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISTAVPKMTGS", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API stav plati k z jednani");
        var olderOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9721);
        var newestOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9722);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&jednaniId={olderOpenId}&uiContext=meeting&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Matches(html, $"<option value=\"{olderOpenId}\" selected=\"selected\"").Should().BeTrue("editor z jednání předvybere to jednání");
        Matches(html, $"<option value=\"{newestOpenId}\">").Should().BeTrue("jiné jednání jde vybrat");
        Matches(html, $"name=\"MeetingId\" value=\"{olderOpenId}\"").Should().BeTrue("uložení se vrátí do detailu jednání");
    }

    [Fact]
    public async Task Edit_WithForeignMeetingContext_IgnoresIt()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiStavPlatiKCizi");
        var projectId = await _fixture.EnsureProjectAsync("APISTAVPKCIZI");
        var otherProjectId = await _fixture.EnsureProjectAsync("APISTAVPKCIZI2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISTAVPKCIZIS", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API stav plati k cizi");
        var ownOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9731);
        var foreignOpenId = await _fixture.CreateMeetingAsync(otherProjectId, "OPEN", 9732);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&jednaniId={foreignOpenId}&uiContext=meeting&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Matches(html, $"<option value=\"{ownOpenId}\" selected=\"selected\"").Should().BeTrue("cizí jednání se ignoruje, platí §4.3");
        Matches(html, $"name=\"MeetingId\" value=\"{foreignOpenId}\"").Should().BeFalse();
    }
```

- [ ] **Step 3: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StatusChangeEffectiveDate" --nologo` a `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~RecordEditorStatusEffectiveDateTests" --nologo`
Expected:
- Unit: FAIL na kompilaci (`PreferMeeting` neexistuje).
- Api: `Edit_FromMeeting_…` FAIL (předvybrané je nejnovější), `Edit_WithForeignMeetingContext_…` PASS (kontext se dnes ignoruje).

- [ ] **Step 4: Implementace**

`StatusChangeEffectiveDatePolicy`:

```csharp
    /// <summary>
    /// Editor otevřený z detailu jednání (plán 2026-10-21): předvybere toto jednání, je-li v nabídce.
    /// Jinak výchozí volba podle §4.3 beze změny — nabídku ani ověření při uložení to nemění.
    /// </summary>
    public static StatusChangeEffectiveDateChoice PreferMeeting(StatusChangeEffectiveDateChoice choice, int meetingId)
    {
        var option = choice.Options.FirstOrDefault(x => x.JednaniId == meetingId);
        return option is null ? choice : choice with { DefaultValue = option.Value };
    }
```

`ZaznamyController.Edit`: podpis `Edit(int id, string? returnUrl, int? jednaniId = null, string? uiContext = null, CancellationToken ct = default)`. Hned po autorizaci (kde je známé `projektId`) ověř kontext:

```csharp
        // Kontext jednání (plán 2026-10-21) jen pro jednání projektu záznamu — cizí id se ignoruje.
        if (jednaniId.HasValue
            && !await _db.Jednani.AsNoTracking().AnyAsync(x => x.Id == jednaniId.Value && x.ProjektId == projektId.Value, ct))
        {
            jednaniId = null;
        }
```

Volání `PrepareRecordEditorModel(model, returnUrl, canEditRecord, canManageSchedule)` nahraď:

```csharp
        PrepareRecordEditorModel(model, returnUrl, canEditRecord, canManageSchedule, uiContext, jednaniId);
        if (model.MeetingId.HasValue && model.StavPlatiK is not null)
        {
            model.StavPlatiK = StatusChangeEffectiveDatePolicy.PreferMeeting(model.StavPlatiK, model.MeetingId.Value);
        }
```

(`PrepareRecordEditorModel` už kontext normalizuje a nastaví `MeetingId`, `BackUrl` i „Zpět na jednání“. Uložení z formuláře posílá `UiContext` a `MeetingId` a vrací do detailu jednání, to už existuje.)

- [ ] **Step 5: Spusť, musí projít + sousední testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StatusChangeEffectiveDate|FullyQualifiedName~Architecture" --nologo` a `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~RecordEditor" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A PmTracker.Web PmTracker.Tests.Unit PmTracker.Tests.Api/Controllers/RecordEditorStatusEffectiveDateTests.cs
git commit -m "feat(jednani-zaznamy): změna stavu z jednání předvybírá toto jednání (PreferMeeting)"
```

---

### Task 5: Detail jednání — kompaktní pruh, záznamy, zápis uzavřeného jednání

**Files:**
- Modify: `Models/ViewModels/JednaniViewModels.cs` (`JednaniDetailViewModel`)
- Modify: `Services/MeetingService.DetailQueries.cs` (`BuildJednaniDetailAsync` → `IsReadOnly`)
- Modify: `Controllers/JednaniController.cs` (konstruktor + `Detail`)
- Modify: `Controllers/ExportController.cs` (`JednaniNahled` + sdílená kontrola jednání)
- Modify: `Views/Jednani/Detail.cshtml`
- Modify: `wwwroot/css/site.css`
- Create: `wwwroot/js/modules/meetingMinutes.js`, import v `wwwroot/js/modules/bootstrap.js`
- Test: `PmTracker.Tests.Api/Controllers/MeetingDetailRecordsRenderTests.cs`

**Interfaces:**
- Consumes: `PrepareRecordsTabPresentationAsync(…, kontextJednaniId, ct)` (Task 2), `_ProjectRecordsTab.cshtml`.
- Produces: `JednaniDetailViewModel.IsReadOnly` (`bool`, init), `ZaznamyTab` (`ProjektZaznamyTabViewModel?`, set), `ZapisNahledUrl` (`string?`, set); `GET /Export/Jednani/{jednaniId}/Nahled?projektId=` (HTML šablona tisku).

- [ ] **Step 1: Padající Api testy**

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Plán 2026-10-21: detail otevřeného / připravovaného jednání ukazuje záložku Záznamy
/// v kontextu jednání; uzavřené jednání jen pruh se stavem a zápis (HTML šablona tisku v iframe).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingDetailRecordsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingDetailRecordsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int PmId, int ProjectId, int RecordId)> SeedProjectAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        return (pmId, projectId, recordId);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        return (response.StatusCode, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("OPEN", 9501)]
    [InlineData("DRAFT", 9502)]
    public async Task OpenOrDraftMeeting_ShowsRecordsInMeetingContext(string state, int number)
    {
        var s = await SeedProjectAsync($"ApiMtgDet{state}");
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, state, number);

        var (status, html) = await GetAsync($"/Jednani/Detail/{meetingId}?asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-project-detail-root", "záložka Záznamy je v detailu jednání");
        html.Should().Contain($"jednaniId={meetingId}&uiContext=meeting", "tužka i Nový záznam nesou jednání");
        html.Should().Contain($"data-project-filter-storage-id=\"jednani-{meetingId}\"");
        html.Should().Contain("data-meeting-strip");
        html.Should().NotContain("data-meeting-minutes-frame");
    }

    [Fact]
    public async Task ClosedMeeting_ShowsMinutesFrame_WithoutRecordsAndAttendance()
    {
        var s = await SeedProjectAsync("ApiMtgDetClosed");
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9503);

        var (status, html) = await GetAsync($"/Jednani/Detail/{meetingId}?asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-meeting-strip");
        html.Should().Contain($"src=\"/Export/Jednani/{meetingId}/Nahled?projektId={s.ProjectId}\"");
        html.Should().NotContain("data-project-detail-root");
        html.Should().NotContain("data-meeting-attendance-card");
    }

    [Fact]
    public async Task MinutesPreview_ProjectManager_GetsPrintTemplateHtml()
    {
        var s = await SeedProjectAsync("ApiMtgNahled");
        var meetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9504);

        var (status, html) = await GetAsync($"/Export/Jednani/{meetingId}/Nahled?projektId={s.ProjectId}&asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("class=\"print-sheet\"");
        html.Should().NotContain("window.print()", "náhled se sám netiskne");
    }

    [Fact]
    public async Task MinutesPreview_ForeignProjectId_ReturnsNotFound()
    {
        var s = await SeedProjectAsync("ApiMtgNahledA");
        var other = await SeedProjectAsync("ApiMtgNahledB");
        await _fixture.EnsureProjectRoleAssignmentAsync(other.ProjectId, s.PmId, ProjectRoleCodes.ProjectManager);
        var meetingInA = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9505);

        var (status, _) = await GetAsync($"/Export/Jednani/{meetingInA}/Nahled?projektId={other.ProjectId}&asUser={s.PmId}");

        status.Should().Be(HttpStatusCode.NotFound);
    }
}
```

(`CreateMeetingAsync(projectId, "CLOSED", …)`: ověř, že číselník stavů v testovací DB má kód `CLOSED`. `RecordDetailPage`/`ExportControllerTests` ho používají; jinak použij `UzavrenyStavKod` z číselníku a zapiš ruling.)

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingDetailRecordsRenderTests" --nologo`
Expected: FAIL všech 5 (chybí `data-meeting-strip`, záložka i akce `Nahled`).

- [ ] **Step 3: View model + IsReadOnly**

`JednaniDetailViewModel`:

```csharp
    /// <summary>Uzavřené jednání (MeetingStatePolicy.IsReadOnly) — detail ukáže jen pruh se stavem a zápis.</summary>
    public bool IsReadOnly { get; init; }
    /// <summary>Záložka Záznamy v kontextu jednání (jen otevřené / příprava).</summary>
    public ProjektZaznamyTabViewModel? ZaznamyTab { get; set; }
    /// <summary>HTML zápis jednání (šablona tisku) pro iframe — jen uzavřené jednání a export.pdf.jednani.</summary>
    public string? ZapisNahledUrl { get; set; }
```

`MeetingService.BuildJednaniDetailAsync`: načti stav jednání a do inicializace modelu doplň `IsReadOnly`:

```csharp
        var meetingState = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
        // … v new JednaniDetailViewModel { … }:
        IsReadOnly = MeetingStatePolicy.IsReadOnly(meeting, meetingState),
```

- [ ] **Step 4: Akce Nahled + sdílená kontrola jednání v ExportController**

Tři akce (`JednaniTisk`, `JednaniWord`, nová `JednaniNahled`) sdílejí kontrolu, proto ji vytáhni:

```csharp
    /// <summary>
    /// Jednání pro export: musí existovat, zadané projektId musí odpovídat jeho projektu
    /// (anti-spoof, 0 = přímá URL globálního uživatele) a projekt musí jít číst.
    /// </summary>
    private async Task<(IActionResult? Failure, int ProjektId)> ResolveMeetingForExportAsync(int jednaniId, int projektId, CancellationToken ct)
    {
        var meetingProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (meetingProjectId is null || (projektId != 0 && projektId != meetingProjectId.Value))
        {
            return (NotFound(), 0);
        }

        var accessCheck = await EnsureProjectReadableAsync(meetingProjectId.Value, ct);
        return (accessCheck, meetingProjectId.Value);
    }

    [HttpGet("Jednani/{jednaniId:int}/Nahled")]
    [Authorize(Policy = "permission:export.pdf.jednani")]
    public async Task<IActionResult> JednaniNahled(int jednaniId, int projektId, CancellationToken ct = default)
    {
        var (failure, _) = await ResolveMeetingForExportAsync(jednaniId, projektId, ct);
        if (failure is not null)
        {
            return failure;
        }

        // Zápis do detailu uzavřeného jednání (plán 2026-10-21): stejná šablona a data jako PDF tisk, bez PDF a bez autoPrint.
        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint: false, ct);
        return View(PdfViewPath, model);
    }
```

`JednaniTisk` a `JednaniWord` přepiš na `ResolveMeetingForExportAsync` (chování beze změny, hlídají ho `ExportControllerTests` včetně anti-spoof testu).

- [ ] **Step 5: Controller detailu**

`JednaniController`: konstruktor dostane `IRecordProposalService recordProposalService` (pole `_recordProposalService`). V `Detail` po nastavení oprávnění:

```csharp
        if (!model.IsReadOnly)
        {
            var zaznamyTab = await _projectService.BuildProjectRecordsTabAsync(model.ProjektId, ct);
            await PrepareRecordsTabPresentationAsync(zaznamyTab, _recordProposalService, model.Jednani.Id, ct);
            model.ZaznamyTab = zaznamyTab;
        }
        else if (CurrentUserContext.HasPermission(PermissionKeys.ExportPdfJednani, model.ProjektId))
        {
            model.ZapisNahledUrl = Url.Action("JednaniNahled", "Export", new { jednaniId = model.Jednani.Id, projektId = model.ProjektId })
                ?? $"/Export/Jednani/{model.Jednani.Id}/Nahled?projektId={model.ProjektId}";
        }
```

(`IProjectService.BuildProjectRecordsTabAsync` už `JednaniController` má přes `_projectService`. Ověř, že je na rozhraní. Pokud je jen na implementaci, zapiš ruling a použij rozhraní, které ho vystavuje.)

- [ ] **Step 6: View**

`Views/Jednani/Detail.cshtml` přestav takto. Proměnné v `@{ }` bloku nahoře zůstávají.
1. **Kompaktní pruh** `<section class="meeting-strip" data-meeting-strip>` nahradí `meeting-detail-header` a kartu „Stav jednání“. Obsahuje:
   - `meeting-strip__info`: datum, čas, místo, štítek stavu `@Model.Jednani.Stav`, u zamčeného „Uzamkl …“,
   - `meeting-strip__status`: stávající formuláře `SaveStatus` (select + Uložit stav, Otevřít / Uzavřít jednání) beze změny polí,
   - `header-actions meeting-detail-actions`: Tisk, **Nový záznam** (jen `Model.ZaznamyTab?.CanManageRecords == true`, `href="@Model.ZaznamyTab.CreateRecordEditorUrl"`), Upravit jednání, Smazat. Stávající markup se přesune.
2. **Účast:** stávající karta `data-meeting-attendance-card` beze změny, jen obalená `@if (!Model.IsReadOnly)`.
3. **Obsah:**

```cshtml
@if (Model.ZaznamyTab is not null)
{
    <section class="meeting-records" aria-label="Záznamy jednání">
        @await Html.PartialAsync("~/Views/Projekty/_ProjectRecordsTab.cshtml", Model.ZaznamyTab)
    </section>
}
else if (Model.ZapisNahledUrl is not null)
{
    <iframe class="meeting-minutes-frame"
            src="@Model.ZapisNahledUrl"
            title="Zápis jednání č. @Model.Jednani.CisloJednani"
            data-meeting-minutes-frame></iframe>
}
```

Smaž komentář o odstranění sekce „Zápis úkolů“ (ř. 207–209) a nahraď ho odkazem na tento plán.

- [ ] **Step 7: CSS + JS**

`site.css` (za pravidla `.meeting-*`). Jen letité flex základy a tokeny:

```css
/* Detail jednání (plán 2026-10-21): kompaktní pruh — stav, akce a účast nahoře, záznamy / zápis pod ním. */
.meeting-strip {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem 1rem;
    padding: 0.5rem 0.75rem;
    margin-bottom: 0.75rem;
    background: var(--background-block-primary);
    border: 1px solid var(--border-subtle);
    border-radius: var(--gov-radius);
}

.meeting-strip__info,
.meeting-strip__status {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.25rem 0.75rem;
}

.meeting-strip .meeting-detail-actions {
    margin-left: auto;
}

.meeting-minutes-frame {
    display: block;
    width: 100%;
    min-height: 70vh;
    border: 1px solid var(--border-subtle);
    border-radius: var(--gov-radius);
    background: var(--background-block-primary);
}
```

`wwwroot/js/modules/meetingMinutes.js`:

```js
// Detail uzavřeného jednání (plán 2026-10-21): iframe se zápisem dostane výšku podle obsahu,
// aby stránka měla jeden posuvník. Stejný origin → contentDocument je čitelný; při chybě zůstane min-height.
function fitMinutesFrame(frame) {
    try {
        const doc = frame.contentDocument;
        if (!doc || !doc.documentElement) {
            return;
        }
        frame.style.height = `${doc.documentElement.scrollHeight}px`;
    } catch {
        // cizí origin / zablokovaný přístup — ponechat min-height z CSS
    }
}

export function initMeetingMinutesFrames(root = document) {
    root.querySelectorAll("iframe[data-meeting-minutes-frame]").forEach((frame) => {
        if (frame.dataset.meetingMinutesBound === "true") {
            return;
        }
        frame.dataset.meetingMinutesBound = "true";
        frame.addEventListener("load", () => fitMinutesFrame(frame));
        if (frame.contentDocument?.readyState === "complete") {
            fitMinutesFrame(frame);
        }
    });
}

initMeetingMinutesFrames();
```

V `bootstrap.js` přidej k ostatním vedlejším importům `import "./meetingMinutes.js";`.

- [ ] **Step 8: Spusť, musí projít + sousední**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingDetailRecordsRenderTests|FullyQualifiedName~ExportControllerTests|FullyQualifiedName~Meeting|FullyQualifiedName~Jednani" --nologo` a `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Architecture" --nologo`
Expected: PASS. `ProjectScopedPolicyRequestCarriesProjektIdTests` musí najít odkaz na `JednaniNahled` s `projektId`. `AppCssColorTokenTests` a `JsBundleImportConsistencyTests` musí projít.

- [ ] **Step 9: Commit**

```bash
git add -A PmTracker.Web PmTracker.Tests.Api/Controllers/MeetingDetailRecordsRenderTests.cs
git commit -m "feat(jednani-zaznamy): detail jednání — kompaktní pruh, záznamy v kontextu jednání, zápis uzavřeného jednání"
```

---

### Task 6: Filtry jednání jen v relaci

**Files:**
- Modify: `wwwroot/js/modules/filters/projectFilter.js` (`getProjectFilterStorageKey`, `saveProjectFilterDefaults`)
- Test: `tests/js/dom/projectFilterStorageKey.test.js`

**Interfaces:**
- Consumes: atribut `data-project-filter-storage-id` (Task 2).
- Produces: `export function buildProjectFilterStorageKey(dataset, kind)`.

- [ ] **Step 1: Padající JS test**

```js
import { test } from "node:test";
import assert from "node:assert/strict";

import { buildProjectFilterStorageKey } from "../../../PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js";

test("detail projektu: klíč podle projektu pro stav i výchozí hodnoty", () => {
    assert.equal(buildProjectFilterStorageKey({ projectId: "12" }, "state"), "pmtracker.projectFilters.v1.project.12.state");
    assert.equal(buildProjectFilterStorageKey({ projectId: "12" }, "defaults"), "pmtracker.projectFilters.v1.project.12.defaults");
});

test("detail jednání: vlastní klíč stavu, oddělený od projektu", () => {
    assert.equal(
        buildProjectFilterStorageKey({ projectId: "12", projectFilterStorageId: "jednani-7" }, "state"),
        "pmtracker.meetingFilters.v1.jednani-7.state");
});

test("detail jednání: výchozí hodnoty se nečtou ani neukládají", () => {
    assert.equal(buildProjectFilterStorageKey({ projectId: "12", projectFilterStorageId: "jednani-7" }, "defaults"), "");
});

test("bez projektu i kontextu: žádný klíč", () => {
    assert.equal(buildProjectFilterStorageKey({}, "state"), "");
    assert.equal(buildProjectFilterStorageKey(undefined, "state"), "");
});
```

- [ ] **Step 2: Spusť, musí selhat**

Run: `node --test tests/js/dom/projectFilterStorageKey.test.js`
Expected: FAIL: `buildProjectFilterStorageKey` není exportovaná.

- [ ] **Step 3: Implementace**

```js
const meetingFilterStoragePrefix = "pmtracker.meetingFilters.v1.";

/**
 * Klíč úložiště filtrů z datasetu kořene filter shellu.
 * Detail jednání (data-project-filter-storage-id, plán 2026-10-21): vlastní klíč jen pro stav
 * v sessionStorage; výchozí hodnoty se nečtou ani neukládají ("" = bez úložiště).
 */
export function buildProjectFilterStorageKey(dataset, kind) {
    const storageId = (dataset?.projectFilterStorageId || "").trim();
    if (storageId) {
        return kind === "state" ? `${meetingFilterStoragePrefix}${storageId}.state` : "";
    }

    const projectId = (dataset?.projectId || "").trim();
    if (!projectId || projectId === "0") {
        return "";
    }

    return `${projectFilterStoragePrefix}${projectId}.${kind}`;
}

export function getProjectFilterStorageKey(scope, kind) {
    return buildProjectFilterStorageKey(getProjectFilterRoot(scope)?.dataset, kind);
}
```

(Nahrazuje dosavadní tělo `getProjectFilterStorageKey`.) V `saveProjectFilterDefaults` hned za `const key = getProjectFilterStorageKey(scope, "defaults");` přidej `if (!key) { return; }`. V jednání tlačítko chybí (Task 2), tohle je jen pojistka, aby se nezapsal popisek projektu. `readJsonStorage` a `writeJsonStorage` prázdný klíč už ignorují.

- [ ] **Step 4: Spusť, musí projít + celá JS sada**

Run: `npm test`
Expected: PASS (dosavadních 41 + 4 nové).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js tests/js/dom/projectFilterStorageKey.test.js
git commit -m "feat(jednani-zaznamy): filtry v detailu jednání jen v relaci, bez výchozích hodnot"
```

---

### Task 7: E2E, ruční průchod, plné sady, k-otestovani, merge

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/MeetingDetailRecordsScenariosTests.cs`
- Modify: `docs/k-otestovani.md`

- [ ] **Step 1: E2E scénář** (vzor seed SQL z `RecordDetailPageScenariosTests`, klik přes `DispatchEventAsync` u gov hostů)

```csharp
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Plán 2026-10-21: detail otevřeného jednání ukazuje záznamy; vyjádření na kartě má předvybrané
/// toto jednání a uloží se k němu; uzavřené jednání ukáže zápis v iframe.
/// Jednání: číslo pod 9820, datum včera (memory: neodsunout jednání jiných scénářů).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class MeetingDetailRecordsScenariosTests
{
    private const int OpenMeetingNumber = 9711;
    private const int ClosedMeetingNumber = 9712;
    private readonly E2ETestFixture _fixture;

    public MeetingDetailRecordsScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private async Task<(int RecordId, int OpenMeetingId, int ClosedMeetingId)> SeedAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @kat INT = (SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu
                WHERE kod = N'U' OR nazev LIKE N'%kol%' ORDER BY CASE WHEN kod = N'U' THEN 0 ELSE 1 END, id);
            DECLARE @stav INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_ukolu ORDER BY id);
            DECLARE @sub INT = (SELECT TOP (1) id FROM dbo.subsystemy ORDER BY id);
            IF NOT EXISTS (SELECT 1 FROM dbo.projekt_subsystemy WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL)
                INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id, datum_prirazeni) VALUES (@p, @sub, SYSUTCDATETIME());
            DECLARE @rid INT = (SELECT TOP (1) id FROM dbo.projektove_zaznamy WHERE projekt_id = @p AND nazev = @marker ORDER BY id);
            IF @rid IS NULL
            BEGIN
                DECLARE @num INT = (SELECT ISNULL(MAX(cislo_zaznamu), 0) + 1 FROM dbo.projektove_zaznamy WHERE projekt_id = @p);
                INSERT INTO dbo.projektove_zaznamy (projekt_id, kategorie_id, stav_ukolu_id, cislo_zaznamu, nazev, cil, popis, vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
                VALUES (@p, @kat, @stav, @num, @marker, @marker, @marker, @o, DATEADD(day, -30, CONVERT(DATE, GETDATE())), DATEADD(day, 45, CONVERT(DATE, GETDATE())), @sub);
                SET @rid = CAST(SCOPE_IDENTITY() AS INT);
            END
            DELETE FROM dbo.vyjadreni WHERE zaznam_id = @rid;
            DECLARE @open INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani WHERE kod = N'OPEN');
            DECLARE @closed INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani WHERE kod = N'CLOSED');
            IF NOT EXISTS (SELECT 1 FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = @openNo)
                INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, misto, stav_jednani_id)
                VALUES (@p, @openNo, DATEADD(day, -1, CONVERT(DATE, GETDATE())), '09:00', N'E2E detail jednani', @open);
            IF NOT EXISTS (SELECT 1 FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = @closedNo)
                INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, misto, stav_jednani_id)
                VALUES (@p, @closedNo, DATEADD(day, -2, CONVERT(DATE, GETDATE())), '09:00', N'E2E detail jednani', @closed);
            SELECT @rid,
                   (SELECT id FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = @openNo),
                   (SELECT id FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = @closedNo);
            """;
        command.Parameters.AddWithValue("@p", _fixture.ProjectId);
        command.Parameters.AddWithValue("@o", _fixture.AdminOsobaId);
        command.Parameters.AddWithValue("@marker", "E2E detail jednani zaznam");
        command.Parameters.AddWithValue("@openNo", OpenMeetingNumber);
        command.Parameters.AddWithValue("@closedNo", ClosedMeetingNumber);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    [Fact]
    public async Task OpenMeeting_CommentPreselectsMeeting_AndSavesToIt()
    {
        var (recordId, openMeetingId, _) = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{openMeetingId}?asUser={_fixture.AdminOsobaId}");

        var card = page.Locator($".record-card[data-record-id='{recordId}']");
        await Assertions.Expect(card).ToHaveCountAsync(1);
        await card.Locator("[data-record-comments-toggle], [data-record-toggle]").First.DispatchEventAsync("click");
        var select = card.Locator("form.comment-form select[name='JednaniId']");
        await Assertions.Expect(select).ToHaveValueAsync(openMeetingId.ToString());

        await card.Locator("form.comment-form textarea[name='Text']").EvaluateAsync("t => { t.value = 'E2E vyjadreni z detailu jednani'; t.dispatchEvent(new Event('input', { bubbles: true })); }");
        await card.Locator("form.comment-form [native-type='submit'], form.comment-form button[type='submit']").First.DispatchEventAsync("click");
        await Assertions.Expect(card.Locator(".comment-item, [data-comment-id]").First).ToBeAttachedAsync();
        await Assertions.Expect(select).ToHaveValueAsync(openMeetingId.ToString());

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ClosedMeeting_ShowsMinutes()
    {
        var (_, _, closedMeetingId) = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{closedMeetingId}?asUser={_fixture.AdminOsobaId}");

        await Assertions.Expect(page.Locator("iframe[data-meeting-minutes-frame]")).ToHaveCountAsync(1);
        var frame = page.FrameLocator("iframe[data-meeting-minutes-frame]");
        await Assertions.Expect(frame.Locator(".print-sheet")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-project-detail-root]")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }
}
```

(Selektory rozbalení vyjádření, textarey s rich-textem a seznamu vyjádření ověř ve stávajícím `RecordDetailPageScenariosTests` scénáři „přidání vyjádření“ a převezmi je doslova. Rich-text editor může vyžadovat jiný způsob zápisu textu. Odchylku zapiš jako ruling. `src` iframu nemá `asUser`, takže v dev může `FrameLocator` dostat 500: přidej do `src` dev parametr stejně jako jiné odkazy (`appendCurrentAsUser` v JS se na `src` nevztahuje). Pokud to nastane, ruling: test otevře `iframe` přes `page.Route` s doplněným `asUser`, nikoli úprava produkčního kódu.)

- [ ] **Step 2: Spusť E2E nové testy**

Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~MeetingDetailRecordsScenariosTests" --nologo`
Expected: PASS (2).

- [ ] **Step 3: Plné sady**

Run: `dotnet test PmTracker.Tests.Unit --nologo`, `dotnet test PmTracker.Tests.Api --nologo`, `npm test`
Expected:
- Unit: 2225 + nové (resolver 1 + PreferMeeting 3) PASS.
- Api: 490 + nové PASS.
- JS: 41 + 4 PASS.

- [ ] **Step 4: E2E celá sada proti baseline** (dočasný worktree `main`, cesty v uvozovkách, porovnání jmen z TRX Pythonem)
Expected: žádné nové jméno selhání (baseline 33).

- [ ] **Step 5: Ruční průchod v prohlížeči** (lokální app 5071, Playwright, snímky)

1. **Přes „Zobrazit jako“ Ing. Jana Testerová** (osoba 3, PM + vedoucí subsystému v projektu 1):
   - otevřené jednání projektu 1: pruh nahoře, účast rozbalitelná, záložka Záznamy;
   - rozbalit vyjádření karty: „Vazba na jednání“ = toto jednání;
   - tužka: editor, změna stavu, „Změna stavu platí k“ = toto jednání;
   - Zpět: návrat do detailu jednání.
   - Uložení pohled blokuje, je jen pro čtení.
2. **Přímo `asUser=3`** (zápis): přidat vyjádření z detailu jednání, uloží se k tomuto jednání a výběr zůstane. Změnit stav přes tužku k tomuto jednání, uložit, vrátí do jednání. Po průchodu svoje testovací data smazat.
3. **Uzavřené jednání:** pruh se stavem + zápis v rámu, výška podle obsahu, žádný dvojitý posuvník.
4. **Filtry:** nastavit v jednání, přejít na záložku Záznamy projektu, tam filtry beze změny. Zpět do jednání: filtry jednání drží (relace). Nové okno prohlížeče: výchozí.
5. Tmavý motiv, 375 px šířka, chyby v konzoli.

- [ ] **Step 6: Zápis do `docs/k-otestovani.md`** (sekce 4, stejný soubor)

```markdown
## 4. Detail jednání se záznamy

**Hotovo 2026-10-21:** plán `docs/superpowers/plans/2026-10-21-detail-jednani-zaznamy.md`.

**Co to dělá:**
- Detail jednání **v přípravě nebo otevřeného**: nahoře úzký pruh (datum, místo, stav, Uložit stav / Uzavřít jednání, Tisk, Nový záznam, Upravit, Smazat), pod ním rozbalitelná účast a celá stránka Záznamy (filtry + karty).
- **Vyjádření** na kartě má jako „Vazba na jednání“ předvybrané toto jednání (u přípravy přípravné vyjádření). Jiné jednání jde vybrat. Výběr zůstane i po uložení.
- **Tužka** otevře editor „z jednání“: „Změna stavu platí k“ má předvybrané toto jednání (pokud ho pravidla nabízejí), jinak jako dřív. Uložení vrátí do jednání.
- **Nový záznam** z pruhu založí záznam k tomuto jednání.
- **Filtry** v jednání jsou jen pro toto jednání a jen do zavření prohlížeče. Neukládají se a záložku Záznamy projektu neovlivní.
- **Uzavřené jednání:** jen pruh se stavem a pod ním zápis, stejný jako PDF tisk.

**Otestovat (PM nebo admin projektu, ne superadmin):**
- [ ] Otevřené jednání: pruh nahoře, účast rozbalitelná, záznamy pod ním. Totéž u jednání v přípravě.
- [ ] Vyjádření na kartě: předvybrané toto jednání; uložení ho zapíše k tomuto jednání; výběr zůstane.
- [ ] Jiné jednání jde ve výběru zvolit.
- [ ] Tužka → změnit stav → „Změna stavu platí k“ = toto jednání → Uložit → zpět v detailu jednání; v tisku jednání má záznam nový stav.
- [ ] Nový záznam z pruhu → založí se k tomuto jednání (číslo podle jednání, pokud projekt čísluje podle jednání).
- [ ] Filtry v jednání neovlivní záložku Záznamy projektu a naopak; po zavření prohlížeče jsou výchozí.
- [ ] Uzavřené jednání: jen pruh a zápis; zápis odpovídá PDF tisku; jedna posuvná lišta.
- [ ] Záložka Záznamy v detailu projektu funguje jako dřív (tužka, vyjádření bez předvolby, uložení filtrů jako výchozích).
- [ ] i15 / Edge: světlý i tmavý motiv, úzké okno.

**Známá omezení (vědomě):** žádné značky „má / nemá vyjádření k tomuto jednání“ (rozhodnutí R7). Zápis uzavřeného jednání je vzhled tisku (bílý papír i v tmavém motivu).
```

- [ ] **Step 7: Commit + merge**

```bash
git add PmTracker.Tests.E2E/Scenarios/MeetingDetailRecordsScenariosTests.cs docs/k-otestovani.md
git commit -m "test(jednani-zaznamy): E2E detail jednání; k-otestovani"
git checkout main
git merge --no-ff feat/detail-jednani-zaznamy -m "Merge feat/detail-jednani-zaznamy into main"
git branch -d feat/detail-jednani-zaznamy
```

Pak doplnit hash merge do sekce 4 a commitnout na `main`.

---

### Task 8 (VOLITELNÝ — jen na pokyn uživatele): pevná volba jednání

**Analýza (R8): jak složité je nedovolit jinou volbu než jednání, ze kterého uživatel přichází.**

| Část | Co se změní | Hraniční případ | Odhad (Opus inline) |
|---|---|---|---|
| Vyjádření | V kontextu jednání nabídka = jen toto jednání (`OtevrenaJednani` zúžit na `PredvybraneJednaniId`). Výběr se nahradí textem „Vyjádření k jednání č. X“ a skrytým `JednaniId`. Server: `AddComment` s `kontextJednaniId` odmítne jiné `JednaniId` (validační chyba, ne 403). | Jednání pro záznam nepovolené (zástupce vedoucího u otevřeného jednání, uzavřený záznam): formulář se neukáže a místo něj hláška „K tomuto jednání nelze u tohoto záznamu přidat vyjádření“. | 0,75 h |
| Změna stavu | `StatusChangeEffectiveDatePolicy.RestrictToMeeting(choice, meetingId)` → nabídka jen toto jednání. Uložení: `RecordService.SaveRecord` při `UiContext=meeting` ověří proti zúžené nabídce, takže podvržené „dnes“ neprojde. | Jednání není v nabídce (pod spodní mezí, za uzavřeným jednáním): stav **nejde v jednání změnit**. Editor ukáže hlášku „Změnu stavu k jednání č. X zapsat nelze (…). Změňte stav mimo jednání.“ Uživatel musí otevřít editor z projektu. | 1–1,25 h |
| Testy | Unit `RestrictToMeeting` (v nabídce / mimo), Api: vyjádření s jiným `JednaniId` v kontextu odmítnuto; editor s kontextem nabídne jen jednání; Save s „dnes“ v kontextu odmítnut. | — | v odhadech |
| **Celkem** | | | **≈ 2 h + ruční ověření** |

**Riziko pro uživatele:** změna zapsaná „dnes“ nebo k jinému jednání v detailu jednání nepůjde. Opravit omyl jde jen z detailu projektu. U jednání, které je pod spodní mezí, nepůjde v jednání stav změnit vůbec.

**Doporučení:** výchozí volba s možností jiné (Task 3–4) pokrývá cíl „vyjádření k jednání, ve kterém jsem“ bez rizika zablokování. Pevnou volbu doplnit, až se v provozu ukáže, že uživatelé omylem volí jiné jednání. Pokud ji uživatel chce rovnou, rozepíše se tento task do kroků RED → GREEN ve stylu Task 3–4 a provede se po Task 6.

---

## Odhad celkem (Opus 5 inline)

| Task | Odhad |
|---|---|
| 1 Sdílená příprava záložky | 0,5 h |
| 2 Kontext v záložce | 1 h |
| 3 Vyjádření | 1 h |
| 4 Změna stavu | 0,75 h |
| 5 Detail jednání + zápis | 2 h |
| 6 Filtry | 0,5 h |
| 7 E2E, průchod, sady, merge | 2 h |
| **Celkem** | **≈ 7,75 h** (+ volitelný Task 8 ≈ 2 h) |

Ruční ověření uživatelem je navíc (checklist v `docs/k-otestovani.md`).
