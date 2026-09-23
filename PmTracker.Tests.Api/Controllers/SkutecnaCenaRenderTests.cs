using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Skutečná cena z kalkulace má přednost před předpokládanou (spec 2026-09-10 A3 R7).
/// Kotví se na data-cena-zdroj a ASCII — Razor kóduje diakritiku i nezlomitelnou mezeru.
/// Snímek se seeduje přímo do DB: Api sada běží s vypnutým ServiceDeskem.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class SkutecnaCenaRenderTests
{
    private const string Marker = "APICENA";
    private const string Smlouva = "APICENA-SML";

    private readonly ApiSqlFixture _fixture;

    public SkutecnaCenaRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int RecordId, int VyzvaId);

    /// <summary>Idempotentní — Api testy sdílí jednu databázi.</summary>
    private async Task<Seed> SeedAsync()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCenaOwner");
        var projectId = await _fixture.EnsureProjectAsync(Marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync(Marker + "_SUB", ownerId);

        await using var db = _fixture.CreateDbContext();

        var projekt = await db.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = Smlouva;
        await db.SaveChangesAsync();

        const string nazev = "API zaznam se skutecnou cenou";
        var recordId = await db.ProjektoveZaznamy.Where(z => z.Nazev == nazev)
            .Select(z => (int?)z.Id).FirstOrDefaultAsync()
            ?? await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", nazev);

        var vyzva = await db.Vyzvy.FirstOrDefaultAsync(v => v.ProjektId == projectId && v.Rok == 2026 && v.PoradoveVRoce == 901);
        if (vyzva is null)
        {
            vyzva = new VyzvaEntity
            {
                ProjektId = projectId, Kod = "901/2026", PoradoveVRoce = 901, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 9, 1), ZalozilOsobaId = ownerId,
                MistoPlneniSnapshot = "FIS (EIS): VZ 8201", CisloRamcoveSmlouvySnapshot = Smlouva,
            };
            db.Vyzvy.Add(vyzva);
            await db.SaveChangesAsync();
        }

        if (!await db.ZaznamExterniOdkazy.AnyAsync(x => x.Cislo == "941901"))
        {
            var pnf = await db.CiselnikTypuExternichOdkazu.Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
            db.ZaznamExterniOdkazy.AddRange(
                new ZaznamExterniOdkazEntity
                {
                    ZaznamId = recordId, TypOdkazuId = pnf, Cislo = "941901",
                    PredpokladanaCena = 5000m, KalkulaceCena = 1000m, KalkulaceId = 77,
                    ZaradidDoVyzvy = true, VyzvaId = vyzva.Id,
                },
                new ZaznamExterniOdkazEntity
                {
                    ZaznamId = recordId, TypOdkazuId = pnf, Cislo = "941902",
                    PredpokladanaCena = 200m,
                    ZaradidDoVyzvy = true, VyzvaId = vyzva.Id,
                });
            await db.SaveChangesAsync();
        }

        return new Seed(projectId, recordId, vyzva.Id);
    }

    [Fact]
    public async Task Chip_UkazeSkutecnouCenu_AJinakPredpokladanou()
    {
        var s = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Zaznamy/RecordDetailPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-cena-zdroj=\"kalkulace\"", "PNF se známou kalkulací ukáže skutečnou cenu");
        html.Should().Contain("data-cena-zdroj=\"predpokladana\"", "PNF bez kalkulace ukáže předpokládanou");
    }

    [Fact]
    public async Task VyzvyPanel_KartaASoucetBerouSkutecnouCenu()
    {
        var s = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{s.ProjectId}?rok=2026&asUser={_fixture.AdminOsobaId}");
        var pane = VyzvyPanelHtml.VyzvaPane(html, s.VyzvaId);

        pane.Should().Contain("data-cena-zdroj=\"kalkulace\"");
        pane.Should().Contain("data-cena-zdroj=\"predpokladana\"");
        pane.Should().Contain("data-vyzvy-celkem=\"1200.00\"",
            "součet = skutečná 1000 + předpokládaná 200, ne 5000 + 200");
        pane.Should().Contain("data-vyzvy-soucet-predpokladane=\"1\"",
            "jedno PNF v součtu nese jen předpokládanou cenu");
    }
}
