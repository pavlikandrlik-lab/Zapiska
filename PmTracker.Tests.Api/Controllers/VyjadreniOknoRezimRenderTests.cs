using System.Net;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Režim okna u bubliny skutečnosti určuje server (2026-10-13): „edit" jen v editoru záznamu
/// držiteli vyjadreni.modal.open, všude jinde (stránka záznamu, editor i detail návrhu)
/// „read". Návrhy ho mít musí — editační okno by měnilo vazby mimo schvalování (spec §5.2).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyjadreniOknoRezimRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public VyjadreniOknoRezimRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int RecordId, int DeputyId)> SeedAsync(string marker, string cislo)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });
        await _fixture.SeedTicketBindingAsync(recordId, cislo, "PMP", poradi: 3, hotVyjadreniId: 7101, datum: DateTime.UtcNow.Date.AddDays(-7));
        _fixture.ServiceDesk.Tikety[cislo] = new HotZaznamDto(cislo, "PMP", "Strucne", "Popis");
        return (projectId, recordId, deputyId);
    }

    private static string ScheduleTable(string html)
    {
        var start = html.IndexOf("schedule-table-wrap", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "stránka renderuje tabulku harmonogramu");
        return html[start..html.IndexOf("</table>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task StrankaZaznamu_Zastupce_VidiBublinuVRezimuCteni()
    {
        var (_, recordId, deputyId) = await SeedAsync("ApiRezim1", "814001");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={deputyId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        var table = ScheduleTable(html);
        table.Should().Contain("data-external-chat-open", "bublinu vidí každý, kdo vidí záznam");
        table.Should().Contain("data-external-chat-mode=\"read\"");
        table.Should().NotContain("data-external-chat-mode=\"edit\"");
    }

    [Fact]
    public async Task StrankaZaznamu_Admin_TakeJenCteni()
    {
        var (_, recordId, _) = await SeedAsync("ApiRezim2", "814002");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await (await client.GetAsync($"/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}")).Content.ReadAsStringAsync();

        ScheduleTable(html).Should().Contain("data-external-chat-mode=\"read\"", "stránka záznamu je celá jen pro čtení");
    }

    [Fact]
    public async Task NavrhHarmonogramu_Zastupce_BublinaVRezimuCteni()
    {
        var (projectId, recordId, deputyId) = await SeedAsync("ApiRezim3", "814003");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={deputyId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-external-chat-open", "kandidát z (falešného) SD dá kroku zdroj");
        html.Should().Contain("data-external-chat-mode=\"read\"");
        html.Should().NotContain("data-external-chat-mode=\"edit\"");
    }

    [Fact]
    public async Task EditorZaznamu_DrzitelKliceOkna_BublinaVRezimuUprav()
    {
        var (projectId, recordId, _) = await SeedAsync("ApiRezim4", "814004");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-external-chat-mode=\"edit\"");
    }
}
