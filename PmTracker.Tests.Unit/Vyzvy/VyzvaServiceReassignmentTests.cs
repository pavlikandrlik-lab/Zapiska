using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceReassignmentTests
{
    [Fact]
    public async Task PrerditZBufferuDoPripravaVyzvy_Ok()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 9);
        db.Vyzvy.Add(new VyzvaEntity { Id = 80, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 20, ZaznamId = 9, TypOdkazuId = 1, Cislo = "888888", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.PrerditPnfAsync(20, 80, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Ok>();
        (await db.ZaznamExterniOdkazy.FindAsync(20))!.VyzvaId.Should().Be(80);
    }

    [Fact]
    public async Task PrerditDoNull_VratiDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 10);
        db.Vyzvy.Add(new VyzvaEntity { Id = 90, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 21, ZaznamId = 10, TypOdkazuId = 1, Cislo = "999999", ZaradidDoVyzvy = true, VyzvaId = 90 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.PrerditPnfAsync(21, null, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(21);
        ev!.VyzvaId.Should().BeNull();
        ev.ZaradidDoVyzvy.Should().BeTrue();
    }

    [Fact]
    public async Task PrerditDoOdeslaneVyzvy_Locked()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 11);
        db.Vyzvy.Add(new VyzvaEntity { Id = 100, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 22, ZaznamId = 11, TypOdkazuId = 1, Cislo = "101010", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.PrerditPnfAsync(22, 100, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.VyzvaIsLocked);
    }
}
