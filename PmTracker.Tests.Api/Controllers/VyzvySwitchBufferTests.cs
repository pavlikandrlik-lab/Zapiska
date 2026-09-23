using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Switch „Zařadit do další výzvy" na externí vazbě posílá PNF do bufferu, nikdy rovnou
/// do rozpracované výzvy (spec 2026-09-07-vyzvy-dokonceni-design §8.4). Hlídá celou cestu
/// endpoint → služba → panel, ne jen chování služby.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvySwitchBufferTests
{
    private readonly ApiSqlFixture _fixture;

    public VyzvySwitchBufferTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SetZaradid_PriExistujiciPripraveVyzve_PnfSkonciVBufferuPanelu()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiVyzvySwitchOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIVYZSWITCH");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIVYZSWITCH_SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Zaznam se switchem");

        int odkazId;
        int vyzvaId;
        await using (var dbContext = _fixture.CreateDbContext())
        {
            var projekt = await dbContext.Projekty.FirstAsync(p => p.Id == projectId);
            projekt.MistoPlneni = "FIS (EIS): VZ 8201";
            projekt.CisloRamcoveSmlouvy = "APIVYZ-SML-SWITCH";

            var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
                .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();

            // Rozpracovaná výzva, do které by stará automatika PNF rovnou vsypala.
            var vyzva = new PmTracker.Web.Models.Entities.VyzvaEntity
            {
                ProjektId = projectId,
                Kod = "455/2026",
                PoradoveVRoce = 455,
                Rok = 2026,
                Stav = PmTracker.Web.Models.Entities.VyzvaStav.Priprava,
                DatumZalozeni = new DateTime(2026, 3, 1),
                ZalozilOsobaId = ownerId,
                MistoPlneniSnapshot = "FIS",
                CisloRamcoveSmlouvySnapshot = "APIVYZ-SML-SWITCH",
            };
            dbContext.Vyzvy.Add(vyzva);

            var odkaz = new PmTracker.Web.Models.Entities.ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = pnfTypeId,
                Cislo = "884411",
                ZaradidDoVyzvy = false,
                VyzvaId = null,
            };
            dbContext.ZaznamExterniOdkazy.Add(odkaz);
            await dbContext.SaveChangesAsync();

            odkazId = odkaz.Id;
            vyzvaId = vyzva.Id;
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/vyzvy/set-zaradid?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ExterniOdkazId", odkazId.ToString()),
                ("Zaradit", "true"))));

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"success\":true", json);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var odkaz = await dbContext.ZaznamExterniOdkazy.AsNoTracking().FirstAsync(x => x.Id == odkazId);
            odkaz.ZaradidDoVyzvy.Should().BeTrue();
            odkaz.VyzvaId.Should().BeNull(
                "zařazení do konkrétní výzvy je vždy vědomý přesun, ne vedlejší efekt switche");
        }

        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?rok=2026&asUser={_fixture.AdminOsobaId}");

        VyzvyPanelHtml.BufferPane(html).Should().Contain("884411", "PNF čeká v bufferu");
        VyzvyPanelHtml.VyzvaPane(html, vyzvaId).Should().NotContain("884411", "rozpracovaná výzva zůstala prázdná");
    }
}
