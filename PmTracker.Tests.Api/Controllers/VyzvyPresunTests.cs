using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Přesuny PNF (spec 2026-09-07-vyzvy-dokonceni-design §8): tažení i kontextové menu
/// se nabízejí jen tam, kde je přesun proveditelný. Se zamčenou výzvou (Odesláno, Zrušeno)
/// se hýbat nedá, takže její řádky nejsou tažitelné, nemají menu a sama se nenabízí jako cíl.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyPresunTests
{
    private readonly ApiSqlFixture _fixture;

    public VyzvyPresunTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenar(int ProjectId, int PripravaId, int OdeslanoId);

    /// <summary>
    /// Idempotentní seed — fixture sdílí databázi mezi testy a unique index
    /// ux_vyzvy_smlouva_rok_poradove je globální, takže druhé vložení téže výzvy selže.
    /// Stejný „Ensure" vzor jako helpery na ApiSqlFixture.
    /// </summary>
    private async Task<Scenar> SeedAsync()
    {
        const string zaznamMarker = "Zaznam k presunu";

        var ownerId = await _fixture.EnsurePersonAsync("ApiVyzvyPresunOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIVYZPRESUN");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIVYZPRESUN_SUB", ownerId);

        // EnsureRecordAsync navzdory názvu zakládá pokaždé nový záznam, takže si existující
        // hledáme sami — jinak by druhý test chtěl vložit PNF s číslem, které už v DB je.
        int recordId;
        await using (var lookup = _fixture.CreateDbContext())
        {
            recordId = await lookup.ProjektoveZaznamy.AsNoTracking()
                .Where(z => z.ProjektId == projectId && z.Nazev == zaznamMarker)
                .Select(z => z.Id)
                .FirstOrDefaultAsync();
        }
        if (recordId == 0)
        {
            recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", zaznamMarker);
        }

        await using var dbContext = _fixture.CreateDbContext();

        var projekt = await dbContext.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = "APIVYZ-SML-PRESUN";
        await dbContext.SaveChangesAsync();

        var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();

        async Task<int> EnsureVyzvaAsync(int poradove, VyzvaStav stav)
        {
            var existing = await dbContext.Vyzvy
                .FirstOrDefaultAsync(v => v.ProjektId == projectId && v.Rok == 2026 && v.PoradoveVRoce == poradove);
            if (existing is not null) return existing.Id;

            var vyzva = new VyzvaEntity
            {
                ProjektId = projectId,
                Kod = $"{poradove}/2026",
                PoradoveVRoce = poradove,
                Rok = 2026,
                Stav = stav,
                DatumZalozeni = new DateTime(2026, 2, 1),
                ZalozilOsobaId = ownerId,
                MistoPlneniSnapshot = "FIS",
                CisloRamcoveSmlouvySnapshot = "APIVYZ-SML-PRESUN",
            };
            dbContext.Vyzvy.Add(vyzva);
            await dbContext.SaveChangesAsync();
            return vyzva.Id;
        }

        async Task EnsurePnfAsync(string cislo, int vyzvaId)
        {
            if (await dbContext.ZaznamExterniOdkazy.AnyAsync(ev => ev.ZaznamId == recordId && ev.Cislo == cislo))
                return;

            dbContext.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId, TypOdkazuId = pnfTypeId, Cislo = cislo,
                ZaradidDoVyzvy = true, VyzvaId = vyzvaId,
            });
            await dbContext.SaveChangesAsync();
        }

        var pripravaId = await EnsureVyzvaAsync(611, VyzvaStav.Priprava);
        var odeslanoId = await EnsureVyzvaAsync(612, VyzvaStav.Odeslano);
        await EnsurePnfAsync("771001", pripravaId);
        await EnsurePnfAsync("771002", odeslanoId);

        return new Scenar(projectId, pripravaId, odeslanoId);
    }

    private async Task<string> LoadPanelAsync(int projectId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        return await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?rok=2026&asUser={_fixture.AdminOsobaId}");
    }

    [Fact]
    public async Task RozpracovanaVyzva_MaTazitelneRadkyIMenu()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadPanelAsync(s.ProjectId), s.PripravaId);

        pane.Should().Contain("draggable=\"true\"", "řádek PNF jde táhnout na dlaždici");
        pane.Should().Contain("data-vyzvy-menu-trigger", "menu ⋯ je rovnocenná druhá cesta");
        pane.Should().Contain("data-cilova-vyzva-id=\"\"",
            "prázdné id je buffer — ten je cílem vždy");
    }

    [Fact]
    public async Task ZamcenaVyzva_NemaAniTazeniAniMenu()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadPanelAsync(s.ProjectId), s.OdeslanoId);

        pane.Should().Contain("771002", "obsah odeslané výzvy se pořád zobrazuje");
        pane.Should().Contain("draggable=\"false\"", "s obsahem odeslané výzvy se hýbat nedá");
        pane.Should().NotContain("data-vyzvy-menu-trigger", "a menu přesunu se vůbec nenabízí");
    }

    [Fact]
    public async Task ZamcenaVyzva_SeNenabiziJakoCilAniNeprijmeDrop()
    {
        var s = await SeedAsync();
        var html = await LoadPanelAsync(s.ProjectId);

        VyzvyPanelHtml.VyzvaPane(html, s.PripravaId)
            .Should().NotContain($"data-cilova-vyzva-id=\"{s.OdeslanoId}\"",
                "odeslaná výzva se nesmí nabídnout jako cíl — server by přesun odmítl");

        var rail = VyzvyPanelHtml.Rail(html);
        rail.Should().Contain("data-vyzvy-drop-target=\"none\"", "dlaždice odeslané výzvy drop nepřijme");
        rail.Should().Contain("data-vyzvy-drop-target=\"buffer\"", "buffer drop přijme");
    }

    [Fact]
    public async Task RozpracovanaVyzva_SeNenabiziSamaSobe()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadPanelAsync(s.ProjectId), s.PripravaId);

        pane.Should().NotContain($"data-cilova-vyzva-id=\"{s.PripravaId}\"",
            "přesun do vlastní výzvy není akce");
    }
}
