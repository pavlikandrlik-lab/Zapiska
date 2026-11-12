using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záložka NES v prodlení (spec 2026-10-15 §10, §13): tabulka ticketů u dodavatele, detail s časovou osou
/// a odůvodněním, sekce Hrozí / Nelze vyhodnotit, placeholder bez IS a chybová karta při výpadku.
/// Přehled dodává falešná <see cref="IProdleniService"/> (ServiceDesk je v Api testech vypnutý).
/// Kotvení na atributy a ASCII (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectDashboardNesPanelRenderTests
{
    private static readonly DateTime Predano = new(2026, 10, 1, 9, 12, 0);

    private readonly ApiSqlFixture _fixture;

    public ProjectDashboardNesPanelRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Tabulka_ma_data_typ_a_u_dodavatele_od()
    {
        var html = await Panel("APIDASHNES1", PrehledSDaty());

        html.Should().Contain("data-nes-row").And.Contain("data-typ=\"NES\"").And.Contain("data-typ=\"PMP\"");
        html.Should().Contain("01.10.2026 09:12", "sloupec U dodavatele od = datum poslední předávky");
        html.Should().Contain("data-nes-filter-typ=\"\"").And.Contain("data-nes-filter-typ=\"NES\"")
            .And.Contain("data-nes-filter-typ=\"PMP\"").And.Contain("data-nes-filter-typ=\"PNF\"");
        html.Should().Contain("https://servicedesk.fis.acr/Hotline/Ticket/Details/900101");
        html.Should().Contain("data-chart-key=\"nes-kpi\"")
            .And.Contain("data-chart-key=\"nes-dny-podle-dodavatele\"")
            .And.Contain("data-chart-key=\"nes-podle-typu\"");
        html.Should().Contain("/dashboard/nes-panel/export");
    }

    [Fact]
    public async Task Radky_jsou_serazene_podle_dnu_sestupne_a_vice_dodavatelu_ma_hvezdicku()
    {
        var html = await Panel("APIDASHNES2", PrehledSDaty());

        var prvni = html.IndexOf("data-nes-dny=\"12\"", StringComparison.Ordinal);
        var druhy = html.IndexOf("data-nes-dny=\"5\"", StringComparison.Ordinal);
        prvni.Should().BePositive();
        druhy.Should().BeGreaterThan(prvni);
        html.Should().Contain("data-nes-vice-dodavatelu", "ticket u více dodavatelů nese hvězdičku s vysvětlivkou T00");
    }

    [Fact]
    public async Task Detail_obsahuje_timeline_svg()
    {
        var html = await Panel("APIDASHNES3", PrehledSDaty());

        html.Should().Contain("data-dashboard-expandable-detail=\"nes-0\"");
        html.Should().Contain("<svg").And.Contain("data-prodleni-timeline");
        html.Should().Contain("data-timeline-obratka").And.Contain("data-timeline-dnes").And.Contain("data-timeline-prodlouzeni");
        html.Should().Contain("data-nes-duvod=\"T05\"").And.Contain("data-nes-duvod=\"T14\"");
    }

    [Fact]
    public async Task Nelze_vyhodnotit_sekce_s_poctem()
    {
        var html = await Panel("APIDASHNES4", PrehledSDaty());

        html.Should().Contain("data-nes-sekce=\"nelze\"").And.Contain("data-nes-sekce-pocet=\"2\"");
        html.Should().Contain("data-nes-chyba=\"SlaChybiProDulezitost\"").And.Contain("data-nes-chyba=\"ChybiIniciacniUdalost\"");
        html.Should().Contain("data-nes-sekce=\"hrozi\"").And.Contain("data-nes-sekce-pocet=\"1\"");
    }

    [Fact]
    public async Task Bez_prodleni_prazdny_stav_misto_tabulky()
    {
        var html = await Panel("APIDASHNES5",
            new ProdleniPrehled(ProdleniStavDat.Ok, 1, "FIS", [], [], [], [], []));

        html.Should().Contain("data-nes-empty");
        html.Should().NotContain("data-nes-row").And.NotContain("data-nes-filter-typ");
        html.Should().NotContain("/dashboard/nes-panel/export", "není co exportovat");
    }

    [Fact]
    public async Task Bez_is_placeholder_s_odkazem()
    {
        var html = await Panel("APIDASHNES6", ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, null, null));

        html.Should().Contain("data-nes-bez-napojeni").And.Contain("href=\"/Projekty\"");
        html.Should().NotContain("data-nes-row").And.NotContain("data-chart-key");
    }

    [Fact]
    public async Task Nedostupne_ma_retry()
    {
        var html = await Panel("APIDASHNES7", ProdleniPrehled.Prazdny(ProdleniStavDat.Nedostupne, 1, "FIS"));

        html.Should().Contain("data-dashboard-panel-retry").And.Contain("pd-state--error");
        html.Should().NotContain("data-nes-row").And.NotContain("data-chart-key");
    }

    [Fact]
    public async Task Export_bez_napojeni_404_a_s_daty_xlsx()
    {
        var bezNapojeni = await _fixture.EnsureProjectAsync("APIDASHNES8");
        using (var factory = Factory(ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, null, null)))
        using (var client = factory.CreateClient(new() { AllowAutoRedirect = false }))
        {
            var response = await client.GetAsync($"/projekty/{bezNapojeni}/dashboard/nes-panel/export?asUser={_fixture.AdminOsobaId}");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using (var factory = Factory(PrehledSDaty()))
        using (var client = factory.CreateClient(new() { AllowAutoRedirect = false }))
        {
            var response = await client.GetAsync($"/projekty/{bezNapojeni}/dashboard/nes-panel/export?asUser={_fixture.AdminOsobaId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should()
                .Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
    }

    private async Task<string> Panel(string marker, ProdleniPrehled prehled)
    {
        var projectId = await _fixture.EnsureProjectAsync(marker);
        using var factory = Factory(prehled);
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/projekty/{projectId}/dashboard/nes-panel?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private WebApplicationFactory<Program> Factory(ProdleniPrehled prehled)
        => _fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IProdleniService>();
            services.AddSingleton<IProdleniService>(new PevnyPrehled(prehled));
        }));

    private static ProdleniPrehled PrehledSDaty()
    {
        IReadOnlyList<WorkflowUdalost> udalosti =
        [
            new(1, Predano, 2, "ACME", null),
            new(2, Predano.AddDays(3), 11, "ACME", Predano.AddDays(6)),
        ];
        IReadOnlyList<PenaltyReason> duvody =
        [
            new(PenaltyReasonKod.T05, Predano, null, Predano.AddDays(1), null, null, 12, null, null),
            new(PenaltyReasonKod.T14, null, null, null, null, Predano.AddDays(6), null, null, null),
        ];

        return new ProdleniPrehled(ProdleniStavDat.Ok, 1, "FIS",
            VProdleni:
            [
                new(900101, "FIS-900101", "NES", "B", "Chyba vypoctu", "ACME", false, Predano, Predano.AddDays(1),
                    Predano.AddDays(6), 12, [Predano.Date.AddDays(7), Predano.Date.AddDays(8)], true, udalosti, duvody),
                new(900102, "FIS-900102", "PMP", null, "Uprava sestavy", "BETA", true, Predano, Predano.AddDays(2),
                    null, 5, [Predano.Date.AddDays(3)], false, udalosti, duvody),
            ],
            Hrozi: [new(900103, "PNF", "Hrozi", "ACME", Predano.AddDays(40), TimeSpan.FromDays(2))],
            NelzeVyhodnotit:
            [
                new(900104, "NES", "Bez SLA", "X", PenaltyChyba.SlaChybiProDulezitost),
                new(900105, "PMP", "Bez U1", null, PenaltyChyba.ChybiIniciacniUdalost),
            ],
            Otevrene: [], Penalizace: []);
    }

    private sealed class PevnyPrehled(ProdleniPrehled prehled) : IProdleniService
    {
        public Task<ProdleniPrehled> GetPrehledAsync(int projektId, CancellationToken ct) => Task.FromResult(prehled);

        public Task<ProdleniPrehled> GetPrehledProObdobiAsync(int projektId, DateTime obdobiOd, CancellationToken ct)
            => Task.FromResult(prehled);
    }
}
