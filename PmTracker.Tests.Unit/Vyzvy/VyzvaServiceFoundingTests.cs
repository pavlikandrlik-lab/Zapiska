using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceFoundingTests
{
    [Fact]
    public async Task ZaloztVyzvuZBufferu_PrazdnyBuffer_BufferEmpty()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.BufferEmpty);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_BezMistaPlneni_Error()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = null, CisloRamcoveSmlouvy = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ProjectMissingMistoPlneni);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_ValidniBuffer_VytvoriVyzvuPriradiPnf()
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
        var now = new DateTime(2026, 4, 20);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, now, CancellationToken.None);

        var ok = result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>().Which.Value;
        ok.Kod.Should().Be("1/2026");
        ok.PoradoveVRoce.Should().Be(1);
        ok.Stav.Should().Be(VyzvaStav.Priprava);
        ok.MistoPlneniSnapshot.Should().Be("FIS (EIS): VZ 8201");
        ok.Polozky.Should().HaveCount(1);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(500);
        ev!.VyzvaId.Should().Be(ok.Id);
        ev.ZaradidDoVyzvy.Should().BeTrue();

        db.VyzvaHistorieStavu.Should().ContainSingle(h => h.NovyStav == VyzvaStav.Priprava && h.PuvodniStav == null);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_DruheZalozeniStejnyRok_Vraci2LomitkoRok()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 200);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 501, ZaznamId = 200, TypOdkazuId = 1, Cislo = "X",
            ZaradidDoVyzvy = true, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, new DateTime(2026, 6, 1), CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>()
            .Which.Value.Kod.Should().Be("2/2026");
    }
}
