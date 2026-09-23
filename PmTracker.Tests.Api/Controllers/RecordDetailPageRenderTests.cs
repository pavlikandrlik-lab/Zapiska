using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Stránka záznamu (2026-07-14): read-only detail na trvalé URL —
/// bohatá hlavička, vyjádření, obě podoby harmonogramu, guard přístupu.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordDetailPageRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordDetailPageRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int recordId)> SeedTaskWithScheduleAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}Sub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} record");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });
        return (projectId, recordId);
    }

    [Fact]
    public async Task DetailPage_RendersHeaderCommentsAndBothScheduleViews()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage1");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Hlavička (bohatá — detail partial s historií a chips).
        html.Should().Contain("record-page-header");
        html.Should().Contain("record-history", "hlavička nese historii vlastníka/termínu/subsystému");
        // Vyjádření ve wrapperu karty (kvůli AJAX obnově).
        html.Should().Contain("record-card--page-column");
        html.Should().Contain("data-record-comments-shell");
        // Obě podoby harmonogramu naráz.
        html.Should().Contain("data-schedule-ticks", "grafická podoba nese serverové ticky");
        html.Should().Contain("schedule-table-wrap", "tabulková podoba je vyrenderovaná taky");
    }

    [Fact]
    public async Task DetailPage_TableView_IsReadOnly()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage2");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Read-only drží zámek datumových polí: KAŽDÉ pm-date-field v tabulce má locked="true"
        // (živě ověřeno: readonly input + skrytý picker, který nejde otevřít). Pin počítá pole,
        // aby test nešel obejít přidáním odemčeného pole.
        var tableStart = html.IndexOf("schedule-table-wrap", StringComparison.Ordinal);
        tableStart.Should().BeGreaterThan(0);
        var tableEnd = html.IndexOf("</table>", tableStart, StringComparison.Ordinal);
        var tableHtml = html[tableStart..tableEnd];

        var dateFields = System.Text.RegularExpressions.Regex.Matches(tableHtml, "<pm-date-field\\b[^>]*>");
        dateFields.Count.Should().BeGreaterThan(0, "tabulka renderuje datumová pole kroků");
        dateFields.Count(m => m.Value.Contains("locked=\"true\"", StringComparison.Ordinal))
            .Should().Be(dateFields.Count, "v read-only tabulce musí být zamčená VŠECHNA datumová pole");

        // Žádné volné editovatelné vstupy (mimo zamčená pm-date-field a hidden nosiče hodnot).
        tableHtml.Should().NotContain("<textarea", "v read-only tabulce nejsou textarea");
        tableHtml.Should().NotContain("<button", "v read-only tabulce nejsou akční tlačítka");
    }

    [Fact]
    public async Task DetailPage_WithoutSchedule_HasNoScheduleColumn()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecPage3Owner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECPAGE3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiRecPage3Sub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "ApiRecPage3 record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-record-page-schedule", "bez harmonogramu se pravý sloupec nerenderuje");
        html.Should().NotContain("data-schedule-view-toggle", "bez harmonogramu není co přepínat");
        html.Should().Contain("data-record-comments-shell", "vyjádření zůstávají");
    }

    [Fact]
    public async Task DetailPage_Header_IsRich_AndActionsAreGated()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage4");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Bohatá hlavička: kategorie, název, historie, souhrnná meta.
        html.Should().Contain("record-page-header");
        html.Should().Contain("record-history");
        html.Should().Contain("record-summary-meta");
        // Přepínač a oba kontejnery.
        html.Should().Contain("data-schedule-view-toggle=\"graph\"");
        html.Should().Contain("data-schedule-view-toggle=\"table\"");
        html.Should().Contain("data-record-page-schedule-graph");
        html.Should().Contain("data-record-page-schedule-table");
        // Admin má edit právo → tlačítko Upravit vede do editoru s návratem na stránku.
        html.Should().Contain("Upravit");
        html.Should().Contain($"returnUrl=%2FZaznamy%2FDetail%2F{recordId}");
    }

    [Fact]
    public async Task DetailPage_CommentForm_PostsToCorrectRecord()
    {
        var (projectId, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage5");
        // Formulář pro přidání vyjádření se renderuje jen s otevřeným jednáním
        // (vyjádření se na jednání váže).
        await _fixture.EnsureMeetingAsync(projectId, meetingNumber: 9932);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Panel vyjádření musí být uvnitř wrapperu karty — na něj je navázaná AJAX obnova.
        var wrapperStart = html.IndexOf("record-card--page-column", StringComparison.Ordinal);
        wrapperStart.Should().BeGreaterThan(0, "vyjádření běží ve skrytém wrapperu karty (spec §7)");
        var shellStart = html.IndexOf("data-record-comments-shell", wrapperStart, StringComparison.Ordinal);
        shellStart.Should().BeGreaterThan(wrapperStart, "shell vyjádření je uvnitř wrapperu, ne mimo něj");

        // Formulář pro přidání vyjádření ukládá na TENTO záznam a projekt (jinak by
        // vyjádření skončilo jinde). Partial používá obě velikosti názvu pole
        // (zaznamId / ZaznamId), proto case-insensitive match.
        var addFormStart = html.IndexOf("comment-form", StringComparison.Ordinal);
        addFormStart.Should().BeGreaterThan(0, "stránka renderuje formulář pro přidání vyjádření");
        var addFormHtml = html[addFormStart..];
        System.Text.RegularExpressions.Regex.IsMatch(
            addFormHtml, $"name=\"[Zz]aznamId\" value=\"{recordId}\"")
            .Should().BeTrue("formulář posílá id tohoto záznamu");
        System.Text.RegularExpressions.Regex.IsMatch(
            addFormHtml, $"name=\"[Pp]rojektId\" value=\"{projectId}\"")
            .Should().BeTrue("formulář posílá id tohoto projektu");
    }

    [Fact]
    public async Task EditorBreadcrumb_UsesProjectVisibleNumber_LikeDetailPage()
    {
        // 2026-09-03: editor zobrazoval v drobečcích databázové Id („Záznam #1234"),
        // zatímco stránka záznamu projektové číslo („Záznam #901-1"). Stejný záznam
        // se tak v drobečcích tvářil pokaždé jinak — sjednoceno na projektové číslo.
        var (projectId, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage6");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var detail = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var detailHtml = await detail.Content.ReadAsStringAsync();
        detail.StatusCode.Should().Be(HttpStatusCode.OK, detailHtml);

        // ASP.NET kóduje diakritiku na HTML entity (Z&#xE1;znam) — porovnáváme dekódovaně.
        var detailText = System.Net.WebUtility.HtmlDecode(detailHtml);
        var visibleNumber = System.Text.RegularExpressions.Regex
            .Match(detailText, @"Záznam #(?<n>[^<\s""]+)").Groups["n"].Value;
        visibleNumber.Should().NotBeNullOrEmpty("stránka záznamu ukazuje projektové číslo");
        visibleNumber.Should().NotBe(recordId.ToString(), "projektové číslo není databázové Id");

        var edit = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var editHtml = await edit.Content.ReadAsStringAsync();
        edit.StatusCode.Should().Be(HttpStatusCode.OK, editHtml);
        var editText = System.Net.WebUtility.HtmlDecode(editHtml);

        editText.Should().Contain($"Záznam #{visibleNumber}",
            "editor musí v drobečcích použít stejné (projektové) číslo jako stránka záznamu");
        editText.Should().NotContain($"Záznam #{recordId}<",
            "v drobečcích nemá být databázové Id");
    }

    [Fact]
    public async Task DetailPage_WithoutSchedule_UsesSingleColumnGrid()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecPage7Owner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECPAGE7");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiRecPage7Sub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "ApiRecPage7 record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("record-page-grid--single",
            "bez harmonogramu se vyjádření roztáhnou na celou šířku");
    }

    [Fact]
    public async Task DetailPage_WithSchedule_KeepsTwoColumnGrid()
    {
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage8");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("record-page-grid--single",
            "s harmonogramem zůstávají dva sloupce");
    }

    [Fact]
    public async Task DetailPage_TableView_ShowsActualDate_NotHarvestWarning()
    {
        // 2026-09-03 (bug): tabulka psala „Automat zatím nenašel vhodné vyjádření", přestože
        // skutečnost z vyjádření existovala a editor ji ukazoval.
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage9");

        // Krok 1 = skutečnost vytěžená automatem (přesně scénář z hlášení).
        await using (var db = _fixture.CreateDbContext())
        {
            var krok = await db.ZaznamHarmonogramKroky
                .FirstAsync(x => x.ZaznamId == recordId && x.Poradi == 1);
            krok.SkutecnostZdroj = (byte)PmTracker.Web.Models.Entities.SkutecnostZdrojEnum.Automat;
            await db.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        // Jen řádek kroku 1 — u kroků bez skutečnosti je varování správně.
        var rowStart = html.IndexOf("data-step-index=\"1\"", StringComparison.Ordinal);
        rowStart.Should().BeGreaterThan(0, "tabulka renderuje řádek kroku 1");
        var rowHtml = System.Net.WebUtility.HtmlDecode(
            html[rowStart..html.IndexOf("</tr>", rowStart, StringComparison.Ordinal)]);

        rowHtml.Should().NotContain("Automat zatím nenašel vhodné vyjádření",
            "u kroku s vytěženou skutečností nemá být varování");
        System.Text.RegularExpressions.Regex.IsMatch(rowHtml, @"\d{2}\.\d{2}\.\d{4}")
            .Should().BeTrue("řádek ukazuje datum skutečnosti");
    }

    [Fact]
    public async Task DetailPage_TableView_HasNoSourceSwitchingControls()
    {
        // Read-only nesmí nabídnout přepnutí zdroje ani výběr jiného ticketu — přes ně
        // by se šlo dostat k akcím (re-harvest), které patří jen oprávněným.
        var (_, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage10");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var tableStart = html.IndexOf("schedule-table-wrap", StringComparison.Ordinal);
        var tableHtml = html[tableStart..html.IndexOf("</table>", tableStart, StringComparison.Ordinal)];

        tableHtml.Should().NotContain("data-feature-c-dropdown", "žádný výběr jiného zdrojového ticketu");
        tableHtml.Should().NotContain("data-schedule-rezim-toggle", "žádné přepínání Auto/Ručně");
    }

    [Fact]
    public async Task DetailPage_TableView_LinksToSourceVyjadreni()
    {
        // 2026-09-03: u skutečnosti vytěžené automatem má být vidět, z jakého vyjádření pochází —
        // ikona otevře vyjádření k nahlédnutí. Akce uvnitř (re-harvest, vazby) chrání server
        // vlastními oprávněními.
        var (projectId, recordId) = await SeedTaskWithScheduleAsync("ApiRecPage11");
        await _fixture.SeedHarvestedActualAsync(projectId, recordId, poradi: 1);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var tableStart = html.IndexOf("schedule-table-wrap", StringComparison.Ordinal);
        tableStart.Should().BeGreaterThan(0);
        var tableHtml = html[tableStart..html.IndexOf("</table>", tableStart, StringComparison.Ordinal)];

        tableHtml.Should().Contain("data-external-chat-open",
            "u vytěžené skutečnosti je odkaz na zdrojové vyjádření");
        tableHtml.Should().NotContain("data-feature-c-dropdown",
            "ale bez výběru jiného zdrojového ticketu");
    }

    [Fact]
    public async Task DetailPage_NonExistentRecord_ReturnsNotFound()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/999999?asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
