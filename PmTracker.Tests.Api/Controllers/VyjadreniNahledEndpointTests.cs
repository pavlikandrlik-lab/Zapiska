using System.Net;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Okno tiketu jen pro čtení (2026-10-13, spec 2026-04-21 §5.2): obsah tiketu u bubliny
/// skutečnosti vidí každý, kdo vidí záznam — bez klíče. Úpravy přiřazení kroků zůstávají
/// v editačním okně za vyjadreni.modal.open.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyjadreniNahledEndpointTests
{
    private readonly ApiSqlFixture _fixture;

    public VyjadreniNahledEndpointTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int RecordId, int OdkazId, int DeputyId, int OutsiderId, string Cislo);

    private async Task<Scenario> SeedAsync(string marker, string cislo)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var outsiderId = await _fixture.EnsurePersonAsync($"{marker}Outsider");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });
        var odkazId = await _fixture.SeedTicketBindingAsync(recordId, cislo, "PMP", poradi: 3, hotVyjadreniId: 7001, datum: new DateTime(2026, 4, 1));

        _fixture.ServiceDesk.Tikety[cislo] = new HotZaznamDto(cislo, "PMP  ", "Strucne testu", "Popis tiketu", "uzivatel");
        _fixture.ServiceDesk.Vyjadreni[cislo] =
        [
            new HotVyjadreniDto(7001, "D", $"PID-{cislo}", new DateTime(2026, 4, 1, 10, 0, 0), "dodavatel", "Zaznam byl predan dodavateli k reseni.", null, 1),
            new HotVyjadreniDto(7002, "D", $"PID-{cislo}", new DateTime(2026, 4, 2, 10, 0, 0), "dodavatel", "Doplnujici informace.", null, 1)
        ];
        return new Scenario(projectId, recordId, odkazId, deputyId, outsiderId, cislo);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetNahledAsync(int odkazId, int recordId, int asUser)
    {
        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/VyjadreniNahled?externiOdkazId={odkazId}&zaznamId={recordId}&asUser={asUser}");
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VyjadreniNahled_Zastupce_VidiObsahTiketuAPrirazeniBezUprav()
    {
        var s = await SeedAsync("ApiNahled1", "813001");

        var (status, html) = await GetNahledAsync(s.OdkazId, s.RecordId, s.DeputyId);

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-chat-modal-mode=\"read\"");
        html.Should().Contain("data-vyjadreni-id=\"7001\"").And.Contain("data-vyjadreni-id=\"7002\"");
        html.Should().Contain("Krok 3", "přiřazení vyjádření ke kroku je vidět jako štítek");
        html.Should().Contain("data-chat-modal-readonly-hint");
        html.Should().NotContain("data-bubble-step-selector", "výběr kroku je jen v editačním okně");
        html.Should().NotContain("data-reharvest-form");
        html.Should().NotContain("data-clear-bubble-binding");
        html.Should().NotContain("data-clear-binding");
    }

    [Fact]
    public async Task VyjadreniNahled_UzivatelBezPristupuKProjektu_Vraci404()
    {
        var s = await SeedAsync("ApiNahled2", "813002");

        var (status, _) = await GetNahledAsync(s.OdkazId, s.RecordId, s.OutsiderId);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VyjadreniNahled_VazbaJinehoZaznamu_Vraci404()
    {
        var a = await SeedAsync("ApiNahled3", "813003");
        var b = await SeedAsync("ApiNahled4", "813004");

        // Vazba záznamu A s id záznamu B (B uživatel vidí) — nesmí prozradit tiket A.
        var (status, _) = await GetNahledAsync(a.OdkazId, b.RecordId, b.DeputyId);

        status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task VyjadreniNahled_ServiceDeskNedostupny_UkazeVarovaniMisto500()
    {
        var s = await SeedAsync("ApiNahled5", "813005");
        _fixture.ServiceDesk.OfflineCisla.Add(s.Cislo);

        var (status, html) = await GetNahledAsync(s.OdkazId, s.RecordId, s.DeputyId);

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-chat-modal-sd-offline");
        html.Should().Contain("data-chat-modal-mode=\"read\"");
    }

    [Fact]
    public async Task EditacniOkno_ZastupceBezKlice_ZustavaZakazane()
    {
        var s = await SeedAsync("ApiNahled6", "813006");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Vyjadreni/Modal?externiOdkazId={s.OdkazId}&zaznamId={s.RecordId}&asUser={s.DeputyId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "úpravy přiřazení drží vyjadreni.modal.open");
    }
}
