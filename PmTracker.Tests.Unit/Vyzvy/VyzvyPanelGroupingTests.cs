using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Pravý panel seskupuje PNF podle projektového záznamu (spec 2026-09-07 §7.1).
/// Jeden záznam může mít víc PNF a každé může být v jiné výzvě, takže se záznam
/// objeví pod více výzvami současně.
/// </summary>
public sealed class VyzvyPanelGroupingTests
{
    private static VyzvaEntity Vyzva(int id, int poradove)
        => new()
        {
            Id = id, ProjektId = 1, Kod = $"{poradove}/2026", PoradoveVRoce = poradove, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 4, poradove), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        };

    private static ZaznamExterniOdkazEntity Pnf(int id, int zaznamId, string cislo, int? vyzvaId)
        => new()
        {
            Id = id, ZaznamId = zaznamId, TypOdkazuId = 1, Cislo = cislo,
            ZaradidDoVyzvy = true, VyzvaId = vyzvaId,
        };

    private static VyzvyPanelBuilder CreateBuilder(PmTracker.Web.Data.PmTrackerDbContext db)
        => new(VyzvaServiceTestHarness.CreateService(db), db);

    [Fact]
    public async Task Build_SeskupujePnfPodleZaznamu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 200);

        db.Vyzvy.Add(Vyzva(10, 1));
        db.ZaznamExterniOdkazy.AddRange(
            Pnf(500, 100, "336865", 10),
            Pnf(501, 100, "341837", 10),
            Pnf(502, 200, "345763", 10));
        await db.SaveChangesAsync();

        var model = await CreateBuilder(db).BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        var vyzva = model.Vyzvy.Should().ContainSingle().Which;
        vyzva.Skupiny.Should().HaveCount(2, "PNF patří dvěma různým záznamům");
        vyzva.Skupiny.Single(s => s.ZaznamId == 100).Polozky.Should().HaveCount(2);
        vyzva.Skupiny.Single(s => s.ZaznamId == 200).Polozky.Should().HaveCount(1);
        vyzva.Skupiny.Single(s => s.ZaznamId == 100).Nazev.Should().Be("test");
        vyzva.Skupiny.Single(s => s.ZaznamId == 100).CisloViditelne.Should().Be("RU100");
    }

    [Fact]
    public async Task Build_JedenZaznamVeDvouVyzvach_JeVObou()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.Vyzvy.AddRange(Vyzva(10, 1), Vyzva(11, 2));
        db.ZaznamExterniOdkazy.AddRange(
            Pnf(500, 100, "336865", 10),
            Pnf(501, 100, "341837", 11));
        await db.SaveChangesAsync();

        var model = await CreateBuilder(db).BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        model.Vyzvy.Should().HaveCount(2);
        model.Vyzvy.Should().OnlyContain(v => v.Skupiny.Count == 1 && v.Skupiny[0].ZaznamId == 100,
            "PNF téhož záznamu mohou být v různých výzvách — záznam se objeví v obou");
    }

    [Fact]
    public async Task Build_BufferSeSkupinamiTake()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);
        db.ZaznamExterniOdkazy.Add(Pnf(500, 100, "336865", null));
        await db.SaveChangesAsync();

        var model = await CreateBuilder(db).BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        model.Buffer.Skupiny.Should().ContainSingle()
            .Which.Polozky.Should().ContainSingle().Which.Cislo.Should().Be("336865");
    }

    /// <summary>
    /// Nabídka cílů přesunu (spec §8.3): zamčená výzva se nesmí nabídnout, server by
    /// přesun stejně odmítl — uživatel nemá dostat volbu, která skončí chybou.
    /// </summary>
    [Fact]
    public async Task Build_CilePresunu_NeobsahujiZamceneVyzvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var priprava = Vyzva(10, 1);
        var odeslana = Vyzva(11, 2);
        odeslana.Stav = VyzvaStav.Odeslano;
        var zrusena = Vyzva(12, 3);
        zrusena.Stav = VyzvaStav.Zruseno;
        db.Vyzvy.AddRange(priprava, odeslana, zrusena);
        await db.SaveChangesAsync();

        var builder = CreateBuilder(db);
        var model = await builder.BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        model.CilePresunu.Select(c => c.VyzvaId).Should().BeEquivalentTo(new int?[] { null, 10 },
            "cílem smí být buffer a rozpracovaná výzva; odeslaná ani zrušená ne");
    }

    [Fact]
    public async Task Build_PnfVeSkupineRadiPodlePoradiPridani()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.Vyzvy.Add(Vyzva(10, 1));
        // Zakládáme v opačném pořadí, než je Id — kdyby se řadilo dle vložení, test spadne.
        db.ZaznamExterniOdkazy.AddRange(
            Pnf(502, 100, "999999", 10),
            Pnf(500, 100, "336865", 10));
        await db.SaveChangesAsync();

        var model = await CreateBuilder(db).BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        model.Vyzvy.Single().Skupiny.Single().Polozky
            .Select(p => p.Cislo).Should().Equal(new[] { "336865", "999999" },
                "uvnitř skupiny je stabilní pořadí podle Id externí vazby");
    }
}
