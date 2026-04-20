using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceQueryTests
{
    [Fact]
    public async Task GetBuffer_PnfSZaradidTrueBezVyzvy_Vraci()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 500, ZaznamId = 100, TypOdkazuId = 1, Cislo = "336865",
            ZaradidDoVyzvy = true, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.GetBufferAsync(1, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Cislo.Should().Be("336865");
    }

    [Fact]
    public async Task GetBuffer_PnfJizVeVyzve_Nevraci()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 10, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 501, ZaznamId = 100, TypOdkazuId = 1, Cislo = "X",
            ZaradidDoVyzvy = true, VyzvaId = 10,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.GetBufferAsync(1, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVyzvy_VraciSetridenoDatumZalozeniDesc()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            new VyzvaEntity
            {
                Id = 10, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 1, 5),
                ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            },
            new VyzvaEntity
            {
                Id = 11, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2026, 1, 10),
                ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.GetVyzvyAsync(1, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Kod.Should().Be("2/2026");
        result[1].Kod.Should().Be("1/2026");
    }

    [Fact]
    public async Task GetVyzva_Neexistuje_Null()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        var svc = VyzvaServiceTestHarness.CreateService(db);
        (await svc.GetVyzvaAsync(999, CancellationToken.None)).Should().BeNull();
    }
}
