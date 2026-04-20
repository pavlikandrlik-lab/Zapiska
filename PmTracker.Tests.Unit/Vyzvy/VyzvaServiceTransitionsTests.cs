using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceTransitionsTests
{
    [Fact]
    public async Task PripravaNaOdeslano_ZapiseDatumOdeslaniAOsobu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Vyzvy.Add(NewVyzva(50, VyzvaStav.Priprava));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var now = new DateTime(2026, 4, 20);
        var result = await svc.ZmenitStavAsync(50, VyzvaStav.Odeslano, 7, now, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>();
        var v = await db.Vyzvy.FindAsync(50);
        v!.Stav.Should().Be(VyzvaStav.Odeslano);
        v.DatumOdeslani.Should().Be(now);
        v.OdeslalOsobaId.Should().Be(7);
    }

    [Fact]
    public async Task NepovolenyPrechod_Error()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Vyzvy.Add(NewVyzva(51, VyzvaStav.Zruseno));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZmenitStavAsync(51, VyzvaStav.Odeslano, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.InvalidStateTransition);
    }

    [Fact]
    public async Task Zruseno_VratiPnfDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 200);

        db.Vyzvy.Add(NewVyzva(52, VyzvaStav.Priprava));
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 900, ZaznamId = 200, TypOdkazuId = 1, Cislo = "111111",
            ZaradidDoVyzvy = true, VyzvaId = 52,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.ZmenitStavAsync(52, VyzvaStav.Zruseno, 7, DateTime.UtcNow, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(900);
        ev!.VyzvaId.Should().BeNull();
        ev.ZaradidDoVyzvy.Should().BeTrue();
    }

    private static VyzvaEntity NewVyzva(int id, VyzvaStav stav) => new()
    {
        Id = id, ProjektId = 1, Kod = $"{id}/2026", PoradoveVRoce = id, Rok = 2026,
        Stav = stav, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
        MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
    };
}
