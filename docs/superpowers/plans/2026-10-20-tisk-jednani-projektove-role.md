# Tisk jednání a „Znovu vytěžit“ pro projektové role — plán opravy

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: použij superpowers:executing-plans (inline, po úkolech, bez subagentů). Kroky používají checkboxy (`- [ ]`). Commity do větve `fix/tisk-jednani-projektove-role`, na konci merge do lokálního `main`, bez pushe.

**Cíl:** PM, vedoucí subsystému a další role bez globální role otevřou tisk a Word jednání ze seznamu Jednání i ze záložky projektu a v okně vyjádření použijí „Znovu vytěžit“ bez „Přístup odepřen“. Nová architekturní pojistka hlídá, aby se stejná chyba nevrátila jinde.

**Architektura:** Politika `[Authorize(Policy = "permission:…")]` čte `projektId` jen z route → query → form (`PermissionAuthorizationHandler`). Opravují se proto odkazy a formuláře, aby `projektId` nesly. Server se nemění, kromě kontroly „projekt patří k externímu odkazu“ u `ReHarvest` (vzor `ExportController.JednaniTisk`). Pojistkou je unit test: reflexí najde akce s projektovou politikou, které nemají `projektId` v route, a ve zdrojácích Razoru a controllerů ověří, že každé skládání URL a každý formulář na ně `projektId` nese.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, xUnit + FluentAssertions, Api testy (`ApiSqlFixture`, `ServiceDeskFactory` s `FakeServiceDesk`), unit architekturní testy (reflexe + regex nad zdrojáky), Playwright (ruční průchod).

**Spec:** samostatný spec není. Analýzu (níže) uživatel odsouhlasil v konverzaci 2026-10-20 („sepiš implementační plán jak to opravit“).

## Analýza (místo specu)

- **Symptom:** osoba, která je v projektu PM i vedoucí subsystému, dostane při tisku zápisu jednání „Přístup odepřen“.
- **Příčina:** tisk (`export.pdf.jednani`) a Word (`export.word.jednani`) mají projektové role (`VLASTNIK_PROJEKTU`, `ADM_PROJ`, `PROJ_MAN`, `GEST`, `HOST`) i subsystémové role (`VEDOUCI_SUBSYSTEMU`, `ZASTUPCE_…`, `METODIK_…`) jen na svůj projekt. Bez `projektId` v requestu handler ověřuje globální oprávnění, a to tyto role nemají, takže vrátí 403. Superadmin, správce aplikace a role „čtení všeho“ mají klíč globálně, proto to testy pod adminem nechytily.
- **Kombinace PM + vedoucí nehraje roli.** Reprodukováno lokálně:
  - Osoba 3 (PM + vedoucí v projektu 1): tisk jednání 7 bez `projektId` je odmítnut, s `projektId=1` vrátí 200. Word se chová stejně.
  - Osoba 3 jako PM v projektu 4 bez role vedoucího: stejný výsledek.
  - Superadmin: 200 i bez `projektId`.
- **Proč to přežilo opravu z 2026-07-14:** `58efa9f` přidal `projektId` jen do `Views/Jednani/Detail.cshtml`. Bez něj zůstaly:
  - seznam Jednání: `Views/Jednani/_MeetingYearGroup.cshtml:32-33`,
  - záložka Jednání v projektu: `Views/Projekty/_ProjectMeetingsTab.cshtml:53-54`,
  - Přehled: `Controllers/DashboardController.cs:109-112` (URL se zatím nikde nevykreslují).
- **Audit stejné třídy chyb** (všechny akce s projektovou politikou bez `{projektId}` v route):
  - **GET odkazy:** 37 míst. Chybí jen 6 výše uvedených. `EditProjectModal` a `DeleteProjectModal` (`id` místo `projektId`) postižené nejsou: `projects.edit` a `projects.delete` mají jen `SUPERADMIN` a `APP_ADMIN`.
  - **POST formuláře:** 14 formulářů. Chybí jen `Views/Vyjadreni/_ChatModal.cshtml:56` („Znovu vytěžit“, `vyjadreni.reharvest`, klíč mají `VLASTNIK_PROJEKTU`, `ADM_PROJ`, `PROJ_MAN`). `SDConnector/Index.cshtml` je stránka jen pro adminy (`settings.sd.view` je globální klíč).
  - **JS skládané URL:** žádné na tyto akce (jen `/Vyjadreni/Modal`, ta politiku nemá).

## Global Constraints

- `projektId` se doplňuje do route values (`Url.Action(..., new { …, projektId = … })`) i do záložního řetězce za `??` (vzor `Views/Jednani/Detail.cshtml:10-11`).
- Kontrola „projekt patří k entitě“ je mírná: když `projektId == 0`, kontrola se přeskočí (zpětná kompatibilita přímých URL, globální uživatel projde politikou tak jako tak). Když je `projektId` zadané a nesedí, akce vrátí `NotFound()`.
- Nenahrazovat politiku kontrolou v těle akce přes `CurrentUserContext.HasPermission(...)`. Zakazuje to `PolicyAttributeMigrationTests`.
- Api HTML testy kotví na atributy a URL, ne na český text (Razor kóduje diakritiku). Odkazy z HTML se před použitím dekódují `WebUtility.HtmlDecode` (`&amp;`).
- Testy oprávnění běží jako osoba **jen s projektovou nebo subsystémovou rolí**, nikdy jako `AdminOsobaId`.
- Unit a Api sady zelené. E2E se posuzuje porovnáním jmen selhání proti baseline `main`, ne počtem.
- Po dokončení nová sekce v `docs/k-otestovani.md` (sekce 3).

## Review Focus

1. **Uživatel s rolí ve více projektech.** Každá karta v seznamu Jednání musí nést projekt svého jednání, ne první projekt na stránce. Task 1 má test se dvěma projekty.
2. **Starší roky (sbalená historie).** Vykresluje je stejný partial, skryté v HTML. Test v Task 1 má jednání z loňska.
3. **Podvržený `projektId`.** PM projektu A pošle `ReHarvest` s externím odkazem projektu B a `projektId=A`, výsledek musí být 404. Task 2 test. U tisku hlídá stávající `JednaniTisk_ShouldReturnNotFound_WhenProjektIdDoesNotMatchMeeting`.
4. **Globální admin a staré URL bez `projektId`.** Musí fungovat dál. Task 2 má test pro admina bez `projektId`. Tisk bez `projektId` pro admina hlídají stávající `ExportControllerTests`.
5. **Režim „Zobrazit jako“.** Tisk (GET) v pohledu funguje s oprávněním zobrazené osoby, „Znovu vytěžit“ (POST) pohled blokuje jako každý zápis. Ověří ruční průchod v Task 4.

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml:32-33` | `projektId = Model.ProjektId` do odkazů tisku a Wordu |
| `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml:53-54` | totéž |
| `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml:58` | skryté pole `projektId` ve formuláři „Znovu vytěžit“ |
| `PmTracker.Web/Controllers/VyjadreniModalController.cs` (`ReHarvest`) | parametr `projektId` + kontrola, že odkaz patří k projektu |
| `PmTracker.Web/Controllers/DashboardController.cs:109-112` | `projektId = item.ProjectId` (Task 3, po RED pojistky) |
| `PmTracker.Tests.Api/Controllers/MeetingPrintLinksProjectRoleTests.cs` | nový |
| `PmTracker.Tests.Api/Controllers/VyjadreniReHarvestProjectRoleTests.cs` | nový |
| `PmTracker.Tests.Unit/Architecture/ProjectScopedPolicyRequestCarriesProjektIdTests.cs` | nový: pojistka |
| `docs/k-otestovani.md` | sekce 3 |

---

### Task 1: Odkazy tisku a Wordu v seznamu Jednání a v záložce projektu

**Files:**
- Modify: `PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml:32-33`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml:53-54`
- Test: `PmTracker.Tests.Api/Controllers/MeetingPrintLinksProjectRoleTests.cs`

**Interfaces:**
- Consumes: `ApiSqlFixture.EnsurePersonAsync/EnsureProjectAsync/EnsureProjectRoleAssignmentAsync/EnsureSubsystemAsync/EnsureSubsystemLeadAsync/EnsureMeetingAsync/CreateDbContext`, `ProjectRoleCodes.ProjectManager` (`PmTracker.Web.Models.ViewModels`).
- Produces: nic pro další úkoly.

- [ ] **Step 1: Padající Api testy**

`PmTracker.Tests.Api/Controllers/MeetingPrintLinksProjectRoleTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// 2026-10-20: PM / vedoucí subsystému bez globální role dostával u tisku jednání ze seznamu
/// Jednání a ze záložky projektu „Přístup odepřen“ — odkazy nenesly projektId, policy
/// export.pdf.jednani / export.word.jednani pak udělala globální kontrolu. Testy jdou cestou
/// uživatele: vykreslí stránku jako projektová role, vezmou odkaz z HTML a otevřou ho.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingPrintLinksProjectRoleTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingPrintLinksProjectRoleTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Osoba jen s projektovou / subsystémovou rolí (žádná globální), vlastní projekt a jednání.</summary>
    private async Task<(int OsobaId, int ProjectId, int MeetingId)> SeedAsync(string role)
    {
        var marker = $"APIPRN{role.ToUpperInvariant()}";
        var osobaId = await _fixture.EnsurePersonAsync($"ApiPrint{role}");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        if (role.Contains("pm", StringComparison.Ordinal))
        {
            await _fixture.EnsureProjectRoleAssignmentAsync(projectId, osobaId, ProjectRoleCodes.ProjectManager);
        }

        if (role.Contains("lead", StringComparison.Ordinal))
        {
            var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", osobaId);
            await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, osobaId);
        }

        var meetingId = await _fixture.EnsureMeetingAsync(projectId, 9601);
        return (osobaId, projectId, meetingId);
    }

    private static IReadOnlyList<string> PrintLinks(string html, int meetingId) =>
        Regex.Matches(WebUtility.HtmlDecode(html), $@"/Export/Jednani/{meetingId}/(?:Tisk|Word)[^""'\s<>]*")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

    private static int? ProjektIdOf(string link)
    {
        var match = Regex.Match(link, @"[?&]projektId=(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string WithAsUser(string url, int osobaId) =>
        url.Contains('?') ? $"{url}&asUser={osobaId}" : $"{url}?asUser={osobaId}";

    private static async Task<string> GetOkHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Theory]
    [InlineData("pm", "seznam")]
    [InlineData("lead", "seznam")]
    [InlineData("pmlead", "seznam")]
    [InlineData("pm", "zalozka")]
    [InlineData("lead", "zalozka")]
    [InlineData("pmlead", "zalozka")]
    public async Task PrintLinks_OpenForProjectRoleWithoutGlobalRole(string role, string page)
    {
        var (osobaId, projectId, meetingId) = await SeedAsync(role);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var pageUrl = page == "seznam"
            ? $"/Jednani?projektId={projectId}&asUser={osobaId}"
            : $"/Projekty/Detail/{projectId}?tab=jednani&asUser={osobaId}";

        var links = PrintLinks(await GetOkHtmlAsync(client, pageUrl), meetingId);

        links.Should().HaveCount(2, "karta jednání nabízí tisk (PDF) i Word");
        foreach (var link in links)
        {
            var response = await client.GetAsync(WithAsUser(link, osobaId));
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"role {role} otevírá {link} ze stránky {page}");
            ProjektIdOf(link).Should().Be(projectId, "policy export.*.jednani čte projekt jen z requestu");
        }
    }

    [Fact]
    public async Task PrintLinks_InMeetingList_CarryProjectOfEachMeeting_IncludingOlderYears()
    {
        var osobaId = await _fixture.EnsurePersonAsync("ApiPrintMulti");
        var projectA = await _fixture.EnsureProjectAsync("APIPRNMULTA");
        var projectB = await _fixture.EnsureProjectAsync("APIPRNMULTB");
        await _fixture.EnsureProjectRoleAssignmentAsync(projectA, osobaId, ProjectRoleCodes.ProjectManager);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectB, osobaId, ProjectRoleCodes.ProjectManager);
        var meetingA = await _fixture.EnsureMeetingAsync(projectA, 9611);
        var olderMeetingA = await _fixture.EnsureMeetingAsync(projectA, 9612);
        var meetingB = await _fixture.EnsureMeetingAsync(projectB, 9613);
        await using (var db = _fixture.CreateDbContext())
        {
            var older = await db.Jednani.FirstAsync(x => x.Id == olderMeetingA);
            older.DatumPlanovane = new DateTime(DateTime.Today.Year - 1, 11, 17);
            await db.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await GetOkHtmlAsync(client, $"/Jednani?asUser={osobaId}");

        foreach (var (meetingId, projectId) in new[] { (meetingA, projectA), (olderMeetingA, projectA), (meetingB, projectB) })
        {
            var links = PrintLinks(html, meetingId);
            links.Should().HaveCount(2, $"jednání {meetingId} má na kartě tisk i Word");
            links.Should().OnlyContain(link => ProjektIdOf(link) == projectId,
                $"odkazy jednání {meetingId} musí nést jeho vlastní projekt {projectId}");
        }
    }
}
```

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingPrintLinksProjectRoleTests" --nologo`
Expected: FAIL 7/7.
- 6× Theory: `Expected response.StatusCode to be OK … but found Forbidden` u prvního odkazu. Je to přesně symptom, který hlásil uživatel.
- Multi-project test: `ProjektIdOf(link)` je `<null>`, odkazy `projektId` nemají.

Pokud Theory padne už na `GetOkHtmlAsync` (stránka nevrátí 200, např. vedoucí subsystému seznam Jednání nevidí), je to jiná chyba. Zastav se a zapiš ji jako ruling, nezakrývej ji.

- [ ] **Step 3: Oprava odkazů**

`Views/Jednani/_MeetingYearGroup.cshtml:32-33`:

```cshtml
                var meetingPdfUrl = Url.Action("JednaniTisk", "Export", new { jednaniId = jednani.Id, projektId = Model.ProjektId, autoPrint = true }) ?? $"/Export/Jednani/{jednani.Id}/Tisk?projektId={Model.ProjektId}&autoPrint=true";
                var meetingWordUrl = Url.Action("JednaniWord", "Export", new { jednaniId = jednani.Id, projektId = Model.ProjektId }) ?? $"/Export/Jednani/{jednani.Id}/Word?projektId={Model.ProjektId}";
```

`Views/Projekty/_ProjectMeetingsTab.cshtml:53-54`:

```cshtml
                                var meetingPdfUrl = Url.Action("JednaniTisk", "Export", new { jednaniId = jednani.Id, projektId = Model.ProjektId, autoPrint = true }) ?? $"/Export/Jednani/{jednani.Id}/Tisk?projektId={Model.ProjektId}&autoPrint=true";
                                var meetingWordUrl = Url.Action("JednaniWord", "Export", new { jednaniId = jednani.Id, projektId = Model.ProjektId }) ?? $"/Export/Jednani/{jednani.Id}/Word?projektId={Model.ProjektId}";
```

(`MeetingYearGroupPartialViewModel.ProjektId` plní `Jednani/Index.cshtml` pro každou projektovou skupinu, `ProjektJednaniTabViewModel.ProjektId` je projekt záložky.)

- [ ] **Step 4: Spusť, musí projít + sousední testy**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~MeetingPrintLinksProjectRoleTests|FullyQualifiedName~ExportControllerTests|FullyQualifiedName~MeetingOverviewRenderTests" --nologo`
Expected: PASS (7 nových + stávající).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Jednani/_MeetingYearGroup.cshtml PmTracker.Web/Views/Projekty/_ProjectMeetingsTab.cshtml PmTracker.Tests.Api/Controllers/MeetingPrintLinksProjectRoleTests.cs
git commit -m "fix(export): tisk/Word jednání ze seznamu a záložky projektu nesou projektId (projektové role)"
```

---

### Task 2: „Znovu vytěžit“ v okně vyjádření

**Files:**
- Modify: `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml` (formulář `data-reharvest-form`, ř. 56–66)
- Modify: `PmTracker.Web/Controllers/VyjadreniModalController.cs` (`ReHarvest`, ř. ~266–270)
- Test: `PmTracker.Tests.Api/Controllers/VyjadreniReHarvestProjectRoleTests.cs`

**Interfaces:**
- Consumes: `ApiSqlFixture.ServiceDeskFactory`, `ServiceDesk` (`FakeServiceDesk.Tikety/Vyjadreni`), `EnsureRecordAsync`, `SeedDatumScheduleAsync`, `SeedTicketBindingAsync` (vrací id externího odkazu), `ApiTestHttpHelper.BuildForm(params (string Key, string Value)[])`.
- Produces: `ReHarvest([FromForm] int externiOdkazId, [FromForm] int projektId, CancellationToken ct)`.

- [ ] **Step 1: Padající Api testy**

`PmTracker.Tests.Api/Controllers/VyjadreniReHarvestProjectRoleTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// 2026-10-20: „Znovu vytěžit“ v okně vyjádření posílalo jen externiOdkazId → policy
/// vyjadreni.reharvest udělala globální kontrolu → 403 pro vlastníka / admina / PM projektu.
/// Formulář nese projektId a akce ověří, že externí odkaz do projektu patří.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyjadreniReHarvestProjectRoleTests
{
    private readonly ApiSqlFixture _fixture;

    public VyjadreniReHarvestProjectRoleTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int RecordId, int OdkazId, int PmId);

    /// <summary>Projekt s PM (jen projektová role), záznam s vytěženou vazbou na tiket a falešný ServiceDesk.</summary>
    private async Task<Scenario> SeedAsync(string marker, string cislo, long hotVyjadreniId)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });
        var odkazId = await _fixture.SeedTicketBindingAsync(recordId, cislo, "PMP", poradi: 3, hotVyjadreniId: hotVyjadreniId, datum: new DateTime(2026, 4, 1));

        _fixture.ServiceDesk.Tikety[cislo] = new HotZaznamDto(cislo, "PMP  ", "Strucne testu", "Popis tiketu", "uzivatel");
        _fixture.ServiceDesk.Vyjadreni[cislo] =
        [
            new HotVyjadreniDto(hotVyjadreniId, "D", $"PID-{cislo}", new DateTime(2026, 4, 1, 10, 0, 0), "dodavatel", "Zaznam byl predan dodavateli k reseni.", null, 1)
        ];
        return new Scenario(projectId, recordId, odkazId, pmId);
    }

    /// <summary>Pole formuláře „Znovu vytěžit“ tak, jak je prohlížeč odešle (FormData z formuláře).</summary>
    private static List<(string Key, string Value)> ReHarvestFormFields(string html)
    {
        var form = Regex.Match(html, @"<form[^>]*data-reharvest-form[^>]*>(?<body>.*?)</form>", RegexOptions.Singleline);
        form.Success.Should().BeTrue("PM s vyjadreni.modal.open vidí v okně tlačítko Znovu vytěžit");
        return Regex.Matches(form.Groups["body"].Value, @"<input\b[^>]*>")
            .Select(input => (
                Key: Regex.Match(input.Value, @"\bname=""(?<v>[^""]*)""").Groups["v"].Value,
                Value: WebUtility.HtmlDecode(Regex.Match(input.Value, @"\bvalue=""(?<v>[^""]*)""").Groups["v"].Value)))
            .Where(field => field.Key.Length > 0)
            .ToList();
    }

    [Fact]
    public async Task ReHarvest_FromChatModal_ProjectManagerWithoutGlobalRole_IsAllowed()
    {
        var s = await SeedAsync("ApiReh1", "814001", 7101);
        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });

        var modal = await client.GetAsync($"/Vyjadreni/Modal?externiOdkazId={s.OdkazId}&zaznamId={s.RecordId}&asUser={s.PmId}");
        var html = await modal.Content.ReadAsStringAsync();
        modal.StatusCode.Should().Be(HttpStatusCode.OK, html);
        var fields = ReHarvestFormFields(html);

        using var form = ApiTestHttpHelper.BuildForm(fields.ToArray());
        var response = await client.PostAsync($"/Vyjadreni/ReHarvest?asUser={s.PmId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "PM má vyjadreni.reharvest na svůj projekt — formulář musí projekt nést");
        fields.Should().Contain(("projektId", s.ProjectId.ToString()));
    }

    [Fact]
    public async Task ReHarvest_ProjektIdOfAnotherProject_ReturnsNotFound()
    {
        var own = await SeedAsync("ApiReh2", "814002", 7102);
        var foreign = await SeedAsync("ApiReh3", "814003", 7103);
        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        using var form = ApiTestHttpHelper.BuildForm(
            ("externiOdkazId", foreign.OdkazId.ToString()),
            ("projektId", own.ProjectId.ToString()));

        var response = await client.PostAsync($"/Vyjadreni/ReHarvest?asUser={own.PmId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "projektId musí patřit k externímu odkazu — jinak by PM projektu A vytěžil tiket projektu B");
    }

    [Fact]
    public async Task ReHarvest_GlobalAdminWithoutProjektId_StillAllowed()
    {
        var s = await SeedAsync("ApiReh4", "814004", 7104);
        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        using var form = ApiTestHttpHelper.BuildForm(("externiOdkazId", s.OdkazId.ToString()));

        var response = await client.PostAsync($"/Vyjadreni/ReHarvest?asUser={_fixture.AdminOsobaId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "globální uživatel projde policy i bez projektId (zpětná kompatibilita)");
    }
}
```

- [ ] **Step 2: Spusť, musí selhat**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~VyjadreniReHarvestProjectRoleTests" --nologo`
Expected:
- `…ProjectManagerWithoutGlobalRole_IsAllowed`: FAIL, `found Forbidden`, symptom pro PM.
- `…ProjektIdOfAnotherProject_ReturnsNotFound`: FAIL, `found OK` (nebo `InternalServerError`). Kontrola projektu dnes chybí.
- `…GlobalAdminWithoutProjektId_StillAllowed`: PASS už teď. Je to charakterizační test, hlídá zpětnou kompatibilitu.

Pokud „IsAllowed“ po opravě vrátí 500 místo 200, falešný ServiceDesk pro re-harvest nestačí. Zjisti příčinu z výstupu testu (log `ReHarvest selhal`), nepovoluj 500.

- [ ] **Step 3: Oprava formuláře**

`Views/Vyjadreni/_ChatModal.cshtml`, ve formuláři `data-reharvest-form` za `<input type="hidden" name="externiOdkazId" … />` přidej:

```cshtml
                    <input type="hidden" name="projektId" value="@Model.ProjektId" />
```

(`VyjadreniModalViewModel.ProjektId` plní `VyjadreniModalViewModelBuilder` z projektu záznamu. JS `chatModalReharvest.js` posílá `new FormData(form)`, takže se pole odešle bez změny JS.)

- [ ] **Step 4: Oprava akce**

`Controllers/VyjadreniModalController.cs`, `ReHarvest`: změň signaturu a na začátek přidej kontrolu projektu (vzor `Refresh` ve stejném controlleru a `ExportController.JednaniTisk`):

```csharp
    public async Task<IActionResult> ReHarvest([FromForm] int externiOdkazId, [FromForm] int projektId, CancellationToken ct)
    {
        if (externiOdkazId <= 0) return BadRequest();

        // projektId nese authz kontext pro policy vyjadreni.reharvest (projektové role ho mají jen
        // na svůj projekt). Je-li zadané, musí patřit k externímu odkazu — jinak by šlo autorizovat
        // proti spravovanému projektu a vytěžit tiket cizího projektu. Bez projektId projde policy
        // jen globální uživatel (zpětná kompatibilita, vzor ExportController.JednaniTisk).
        if (projektId != 0)
        {
            var ownerProjektId = await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                                        join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                                        where eo.Id == externiOdkazId
                                        select (int?)z.ProjektId)
                                       .FirstOrDefaultAsync(ct);
            if (ownerProjektId != projektId)
            {
                return NotFound();
            }
        }

        try
        {
```

Zbytek těla (`try { … ReHarvestTicketAsync … }`) se nemění.

- [ ] **Step 5: Spusť, musí projít + sousední testy**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~VyjadreniReHarvestProjectRoleTests|FullyQualifiedName~VyjadreniNahledEndpointTests|FullyQualifiedName~Vyjadreni" --nologo`
Expected: PASS.

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyjadreniModal|FullyQualifiedName~Architecture" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml PmTracker.Web/Controllers/VyjadreniModalController.cs PmTracker.Tests.Api/Controllers/VyjadreniReHarvestProjectRoleTests.cs
git commit -m "fix(vyjadreni): Znovu vytěžit nese projektId + kontrola projektu externího odkazu"
```

---

### Task 3: Architekturní pojistka + URL tisku na Přehledu

**Files:**
- Test: `PmTracker.Tests.Unit/Architecture/ProjectScopedPolicyRequestCarriesProjektIdTests.cs`
- Modify: `PmTracker.Web/Controllers/DashboardController.cs:109-112`

**Interfaces:**
- Consumes: `PermissionSeedConfiguration.Actions/Roles/RoleMappings`, `PermissionScopeLevel`, `RoleScope` (`PmTracker.Web.Services.Security`), `ArchitectureTestBase.ResolvePath`, `typeof(Program).Assembly`.

- [ ] **Step 1: Pojistka (unit)**

`PmTracker.Tests.Unit/Architecture/ProjectScopedPolicyRequestCarriesProjektIdTests.cs`:

```csharp
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// 2026-10-20 (tisk jednání: „Přístup odepřen“ pro PM / vedoucího subsystému):
/// PermissionAuthorizationHandler čte projektId jen z route → query → form. Odkaz nebo formulář
/// na akci s projektovou politikou bez projektId = globální kontrola → 403 pro projektové
/// a subsystémové role. Pojistka projde všechna místa v Razoru a controllerech, kde se na takovou
/// akci skládá URL (Url.Action / RedirectToAction / &lt;a asp-action&gt;) nebo míří formulář.
/// Akce, jejichž klíč mají jen globální role (např. projects.edit), se nekontrolují — tam
/// globální kontrola stačí. Heuristika: Url.Action bez controlleru se přiřadí podle složky
/// view / jména souboru controlleru; Views/Shared bez controlleru se přeskočí.
/// </summary>
public sealed class ProjectScopedPolicyRequestCarriesProjektIdTests
{
    private const string PolicyPrefix = "permission:";

    private sealed record PolicyAction(string Controller, string RoutePrefix, string Action, string ActionPath, string Verb, string Key);

    private sealed record SourceFile(string RelativePath, string? Controller, string Text);

    private static readonly Regex UrlGenerationCall = new(
        @"(?:Url\.Action|RedirectToAction)\(\s*(?:""(?<action>\w+)""|nameof\((?:\w+\.)?(?<action>\w+)\))\s*(?:,\s*""(?<controller>\w+)"")?\s*(?:,\s*new\s*\{(?<args>[^}]*)\})?\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex AnchorTagHelper = new(@"<a\b[^>]*\basp-action=""(?<action>\w+)""[^>]*>", RegexOptions.Compiled);
    private static readonly Regex FormOpenTag = new(@"<form\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex FormActionButton = new(@"formaction=""@Url\.Action\(""(?<action>\w+)"",\s*""(?<controller>\w+)""\)""", RegexOptions.Compiled);
    private static readonly Regex AspController = new(@"asp-controller=""(?<controller>\w+)""", RegexOptions.Compiled);
    private static readonly Regex AspAction = new(@"asp-action=""(?<action>\w+)""", RegexOptions.Compiled);
    private static readonly Regex LiteralFormAction = new(@"\baction=""/(?<prefix>\w+)/(?<path>\w+)""", RegexOptions.Compiled);
    private static readonly Regex ProjektIdRouteValue = new(@"\bprojektId\b", RegexOptions.Compiled);

    // Klíč formuláře musí být přesně „projektId“ (case-insensitive) — asp-for="Command.ProjektId"
    // by se odeslal jako „Command.ProjektId“ a handler by ho nenašel.
    private static readonly Regex ProjektIdFormField = new(@"name=""projektId""|asp-for=""ProjektId""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void LinksToProjectScopedGetActions_CarryProjektId()
    {
        var targets = ActionsNeedingProjektIdInRequest().Where(a => a.Verb == "GET").ToList();
        targets.Should().Contain(a => a.Controller == "Export" && a.Action == "JednaniTisk",
            "sanity: tisk jednání je akce s projektovou politikou bez projektId v route");

        var checkedSites = 0;
        var violations = new List<string>();
        foreach (var file in SourceFiles())
        {
            foreach (Match call in UrlGenerationCall.Matches(file.Text))
            {
                var controller = call.Groups["controller"].Success ? call.Groups["controller"].Value : file.Controller;
                var target = Find(targets, controller, call.Groups["action"].Value);
                if (target is null)
                {
                    continue;
                }

                checkedSites++;
                if (!ProjektIdRouteValue.IsMatch(call.Groups["args"].Value))
                {
                    violations.Add($"{file.RelativePath}:{LineOf(file.Text, call.Index)} → {target.Controller}.{target.Action} ({target.Key})");
                }
            }

            foreach (Match tag in AnchorTagHelper.Matches(file.Text))
            {
                var controllerMatch = AspController.Match(tag.Value);
                var target = Find(targets, controllerMatch.Success ? controllerMatch.Groups["controller"].Value : file.Controller, tag.Groups["action"].Value);
                if (target is null)
                {
                    continue;
                }

                checkedSites++;
                if (!tag.Value.Contains("asp-route-projektId", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{file.RelativePath}:{LineOf(file.Text, tag.Index)} → {target.Controller}.{target.Action} ({target.Key})");
                }
            }
        }

        checkedSites.Should().BeGreaterThanOrEqualTo(20,
            "sanity: kontrola musí najít existující odkazy (detail jednání, tisk úkolu, modaly týmu a jednání)");
        violations.Should().BeEmpty(
            "PermissionAuthorizationHandler čte projektId jen z route → query → form; odkaz bez něj = globální kontrola → " +
            "403 pro projektové a subsystémové role. Doplň projektId = … do route values. Chybí: " + string.Join("; ", violations));
    }

    [Fact]
    public void FormsPostingToProjectScopedActions_CarryProjektIdField()
    {
        var targets = ActionsNeedingProjektIdInRequest().Where(a => a.Verb == "POST").ToList();
        targets.Should().Contain(a => a.Controller == "VyjadreniModal" && a.Action == "ReHarvest",
            "sanity: Znovu vytěžit je POST s projektovou politikou bez projektId v route");

        var checkedForms = 0;
        var violations = new List<string>();
        foreach (var file in SourceFiles().Where(f => f.RelativePath.EndsWith(".cshtml", StringComparison.Ordinal)))
        {
            foreach (Match open in FormOpenTag.Matches(file.Text))
            {
                var bodyStart = open.Index + open.Length;
                var end = file.Text.IndexOf("</form>", bodyStart, StringComparison.Ordinal);
                var body = file.Text[bodyStart..(end < 0 ? file.Text.Length : end)];
                foreach (var target in FormTargets(open.Value, body, file.Controller, targets).Distinct())
                {
                    checkedForms++;
                    if (!ProjektIdFormField.IsMatch(body))
                    {
                        violations.Add($"{file.RelativePath}:{LineOf(file.Text, open.Index)} → {target.Controller}.{target.Action} ({target.Key})");
                    }
                }
            }
        }

        checkedForms.Should().BeGreaterThanOrEqualTo(10,
            "sanity: kontrola musí najít existující formuláře (mazání jednání, komentáře, návrhy, mazání projektu)");
        violations.Should().BeEmpty(
            "formulář na akci s projektovou politikou musí poslat pole projektId (hidden input name=\"projektId\"), " +
            "jinak handler udělá globální kontrolu → 403 pro projektové role. Chybí: " + string.Join("; ", violations));
    }

    /// <summary>
    /// Akce s [Authorize(Policy = "permission:k")], kde k je projektový klíč, který drží aspoň jedna
    /// projektová / subsystémová role, a route akce neobsahuje {projektId}. Controller s politikou,
    /// kterou projektové role nemají (např. SDConnector = settings.sd.view), se přeskočí.
    /// </summary>
    private static IReadOnlyList<PolicyAction> ActionsNeedingProjektIdInRequest()
    {
        var projectLevelKeys = PermissionSeedConfiguration.Actions
            .Where(a => a.ScopeLevel == PermissionScopeLevel.Project)
            .Select(a => a.Klic)
            .ToHashSet(StringComparer.Ordinal);
        var nonGlobalRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope != RoleScope.Global)
            .Select(r => r.Kod)
            .ToHashSet(StringComparer.Ordinal);
        var keysHeldByProjectRoles = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.IsAllowed && nonGlobalRoles.Contains(m.RoleKod))
            .Select(m => m.ActionKlic)
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<PolicyAction>();
        foreach (var controller in typeof(Program).Assembly.GetTypes().Where(IsController))
        {
            if (PolicyKeys(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)).Any(k => !keysHeldByProjectRoles.Contains(k)))
            {
                continue;
            }

            var name = controller.Name[..^"Controller".Length];
            var routePrefix = controller.GetCustomAttributes<RouteAttribute>().FirstOrDefault()?.Template ?? name;
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var verb = method.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault();
                if (verb is null)
                {
                    continue;
                }

                var actionPath = verb.Template ?? method.Name;
                if ($"{routePrefix}/{actionPath}".Contains("{projektId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var key in PolicyKeys(method.GetCustomAttributes<AuthorizeAttribute>()))
                {
                    if (projectLevelKeys.Contains(key) && keysHeldByProjectRoles.Contains(key))
                    {
                        result.Add(new PolicyAction(name, routePrefix, method.Name, actionPath, verb.HttpMethods.First(), key));
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<PolicyAction> FormTargets(string openTag, string body, string? fileController, IReadOnlyList<PolicyAction> targets)
    {
        var aspAction = AspAction.Match(openTag);
        if (aspAction.Success)
        {
            var aspController = AspController.Match(openTag);
            var target = Find(targets, aspController.Success ? aspController.Groups["controller"].Value : fileController, aspAction.Groups["action"].Value);
            if (target is not null)
            {
                yield return target;
            }
        }

        var literal = LiteralFormAction.Match(openTag);
        if (literal.Success)
        {
            var target = targets.FirstOrDefault(t =>
                string.Equals(t.RoutePrefix, literal.Groups["prefix"].Value, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.ActionPath, literal.Groups["path"].Value, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                yield return target;
            }
        }

        foreach (Match button in FormActionButton.Matches(body))
        {
            var target = Find(targets, button.Groups["controller"].Value, button.Groups["action"].Value);
            if (target is not null)
            {
                yield return target;
            }
        }
    }

    private static PolicyAction? Find(IReadOnlyList<PolicyAction> targets, string? controller, string action) =>
        controller is null
            ? null
            : targets.FirstOrDefault(t =>
                string.Equals(t.Controller, controller, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Action, action, StringComparison.Ordinal));

    private static IEnumerable<string> PolicyKeys(IEnumerable<AuthorizeAttribute> attributes) =>
        attributes
            .Select(a => a.Policy)
            .Where(p => p is not null && p.StartsWith(PolicyPrefix, StringComparison.Ordinal))
            .Select(p => p![PolicyPrefix.Length..]);

    private static bool IsController(Type type) =>
        !type.IsAbstract
        && typeof(ControllerBase).IsAssignableFrom(type)
        && type.Name.EndsWith("Controller", StringComparison.Ordinal);

    private static IEnumerable<SourceFile> SourceFiles()
    {
        var web = ArchitectureTestBase.ResolvePath("PmTracker.Web");
        foreach (var path in Directory.EnumerateFiles(Path.Combine(web, "Views"), "*.cshtml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(web, path).Replace('\\', '/');
            var folder = relative.Split('/')[1];
            yield return new SourceFile(relative, folder == "Shared" ? null : folder, File.ReadAllText(path));
        }

        foreach (var path in Directory.EnumerateFiles(Path.Combine(web, "Controllers"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(web, path).Replace('\\', '/');
            var match = Regex.Match(Path.GetFileName(path), @"^(?<name>\w+?)Controller\b");
            yield return new SourceFile(relative, match.Success ? match.Groups["name"].Value : null, File.ReadAllText(path));
        }
    }

    private static int LineOf(string text, int index) => text[..index].Count(c => c == '\n') + 1;
}
```

- [ ] **Step 2: Spusť, odkazy musí selhat na Přehledu**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ProjectScopedPolicyRequestCarriesProjektIdTests" --nologo`
Expected:
- `LinksToProjectScopedGetActions_CarryProjektId`: FAIL a seznam obsahuje přesně `Controllers/DashboardController.cs:109 → Export.JednaniTisk` a `:111 → Export.JednaniWord`. Odkazy z Task 1 jsou už opravené.
- `FormsPostingToProjectScopedActions_CarryProjektIdField`: PASS, formulář z Task 2 je opravený.

Jiný seznam, třeba falešný nález kvůli heuristice regexu, se řeší rulingem: oprav regex, ne kód aplikace. Zapiš to do ledgeru.

- [ ] **Step 3: Ověř, že formulářová pojistka umí selhat (mutace)**

Dočasně smaž v `Views/Vyjadreni/_ChatModal.cshtml` řádek `<input type="hidden" name="projektId" … />` a spusť stejný příkaz.
Expected: `FormsPostingToProjectScopedActions_CarryProjektIdField` FAIL s `Views/Vyjadreni/_ChatModal.cshtml:56 → VyjadreniModal.ReHarvest (vyjadreni.reharvest)`. Pak řádek vrať (`git diff` na `_ChatModal.cshtml` musí být prázdný).

- [ ] **Step 4: Oprava URL na Přehledu**

`Controllers/DashboardController.cs` v `PrepareMeetingsPresentation`:

```csharp
            item.PrintPdfUrl = Url.Action("JednaniTisk", "Export", new { jednaniId = item.MeetingId, projektId = item.ProjectId, autoPrint = true })
                ?? $"/Export/Jednani/{item.MeetingId}/Tisk?projektId={item.ProjectId}&autoPrint=true";
            item.PrintWordUrl = Url.Action("JednaniWord", "Export", new { jednaniId = item.MeetingId, projektId = item.ProjectId })
                ?? $"/Export/Jednani/{item.MeetingId}/Word?projektId={item.ProjectId}";
```

(`DashboardMeetingItemViewModel.ProjectId` je `int`. URL se dnes nikde nevykreslují, oprava je kvůli konzistenci a pojistce.)

- [ ] **Step 5: Spusť, musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Architecture" --nologo`
Expected: PASS (včetně `AuthorizationPolicyEnforcementTests`, `PolicyAttributeMigrationTests`).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Tests.Unit/Architecture/ProjectScopedPolicyRequestCarriesProjektIdTests.cs PmTracker.Web/Controllers/DashboardController.cs
git commit -m "test(authz): pojistka — odkazy a formuláře na projektové policy nesou projektId; Přehled tisk URL"
```

---

### Task 4: Ruční průchod za role, plné sady, k-otestovani, merge

**Files:**
- Modify: `docs/k-otestovani.md`

- [ ] **Step 1: Plné sady**

Run: `dotnet test PmTracker.Tests.Unit --nologo` a `dotnet test PmTracker.Tests.Api --nologo` (výstup do souboru, číst konec).
Expected: Unit 2223 + 2 nové = 2225 PASS. Api 480 + 10 nových = 490 PASS.

- [ ] **Step 2: E2E proti baseline**

Celá `dotnet test PmTracker.Tests.E2E` s TRX na větvi a v dočasném worktree `main` (cesty v uvozovkách, viz skript z plánu Přehled nadpisy), porovnání jmen selhání Pythonem.
Expected: žádné nové jméno selhání (baseline 33).

- [ ] **Step 3: Ruční průchod v prohlížeči (Playwright, lokální app 5071, lokální DB)**

Osoba 3 je v projektu 1 PM i vedoucí subsystému, bez globální role. Projekt 1 má jednání 5–7 a záznam 300 s externími odkazy 4 a 6. Jméno osoby 3 zjisti z `dbo.osoby`.

1. **Za osobu 3 přes „Zobrazit jako“** (superadmin `asUser=1` → menu účtu → Zobrazit jako… → osoba 3):
   - `/Jednani` → karta jednání projektu 1 → ikona tiskárny → v nabídce PDF → nová záložka se načte (200, ne „Přístup odepřen“). Totéž pro Word (stažení souboru).
   - Detail projektu 1 → záložka Jednání → totéž.
   - Detail jednání → totéž (regrese).
   - Snímky obrazovky.
2. **Za osobu 3 přímo (`asUser=3`, bez Pohledu jako, protože ten blokuje POST):** záznam 300 → okno vyjádření externího odkazu → „Znovu vytěžit“. Stavová hláška nesmí být `HTTP 403`. Lokálně bez ServiceDesku může být `HTTP 500` nebo „dokončeno“, zapiš, co vyšlo.
3. **Negativní kontrola:** osoba bez role v projektu 1 (např. 4) a `/Export/Jednani/7/Tisk?projektId=1` → odmítnuto. Oprava nesmí otevřít přístup.
4. Zachyť `pageerror` a console chyby. Testovací stopy (audit „Zobrazit jako“) v lokální DB po průchodu smaž, jen svoje.

- [ ] **Step 4: Zápis do `docs/k-otestovani.md`** (nová sekce 3)

```markdown
## 3. Tisk jednání a „Znovu vytěžit“ pro projektové role

**Hotovo 2026-10-20:** plán `docs/superpowers/plans/2026-10-20-tisk-jednani-projektove-role.md`.

**Co se opravilo:**
- PM, vedoucí subsystému a další role bez globální role (vlastník, admin projektu, gestor, host, zástupce, metodik) dostávali u tisku a Wordu jednání ze **seznamu Jednání** a ze **záložky Jednání v projektu** „Přístup odepřen“. Z detailu jednání to fungovalo už od července. Odkazy teď nesou projekt.
- Vlastník, admin projektu a PM dostávali „Přístup odepřen“ u **„Znovu vytěžit“** v okně vyjádření tiketu. Opraveno. Server navíc ověřuje, že tiket patří do projektu.
- Nový automatický test hlídá, aby žádný odkaz ani formulář na akci s projektovým oprávněním neztratil projekt.

**Otestovat (přihlášen jako PM nebo vedoucí subsystému bez admin role, nebo superadmin přes „Zobrazit jako“):**
- [ ] Jednání → karta jednání → tiskárna → PDF: otevře se tisk, ne „Přístup odepřen“.
- [ ] Totéž → Word: stáhne se dokument.
- [ ] Detail projektu → záložka Jednání → tisk PDF i Word.
- [ ] Detail jednání → tisk PDF i Word (fungovalo i dřív, nesmí se rozbít).
- [ ] Jednání ze starších let (rozbalit historii) → tisk funguje.
- [ ] Uživatel ve více projektech: tisk jednání z každého projektu funguje.
- [ ] Jako PM (ne přes „Zobrazit jako“, ten zápisy blokuje): záznam s externím odkazem → okno vyjádření → „Znovu vytěžit“ → proběhne, ne „HTTP 403“.
- [ ] Uživatel bez role v projektu tisk jednání toho projektu dál neotevře.

**Známá omezení:** staré záložky / přímé URL tisku bez `projektId` fungují jen globálním uživatelům (stejně jako dřív).
```

- [ ] **Step 5: Commit + merge**

```bash
git add docs/k-otestovani.md
git commit -m "docs(k-otestovani): tisk jednání a Znovu vytěžit pro projektové role"
git checkout main
git merge --no-ff fix/tisk-jednani-projektove-role -m "Merge fix/tisk-jednani-projektove-role into main"
git branch -d fix/tisk-jednani-projektove-role
```

Pak do sekce 3 doplnit hash merge (`**Hotovo 2026-10-20:** merge \`<hash>\`, plán …`) a commitnout na `main`.
