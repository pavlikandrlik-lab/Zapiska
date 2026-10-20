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
