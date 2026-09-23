using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceAssignmentTests
{
    [Fact]
    public async Task ZaradidOn_BezPripravaVyzvy_DaDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 1);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 10, ZaznamId = 1, TypOdkazuId = 1, Cislo = "222222" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(10, true, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(10);
        ev!.ZaradidDoVyzvy.Should().BeTrue();
        ev.VyzvaId.Should().BeNull();
    }

    /// <summary>
    /// Do 2026-09-07 se PNF při zapnutí switche automaticky přiřadilo do nejstarší
    /// rozpracované výzvy. Nově výzvy vznikají prázdné a plní se vědomým přesunem,
    /// takže automatika by je plnila za zády uživatele a buffer by zůstal prázdný
    /// (spec §8.4). Test na výběr nejnižšího pořadového čísla proto zanikl — žádný
    /// výběr už se nekoná.
    /// </summary>
    [Fact]
    public async Task ZaradidOn_PripravaVyzvaExistuje_PrestoKonciVBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 2);
        db.Vyzvy.AddRange(
            new VyzvaEntity { Id = 30, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" },
            new VyzvaEntity { Id = 31, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 11, ZaznamId = 2, TypOdkazuId = 1, Cislo = "333333" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(11, true, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Ok>();
        var ev = await db.ZaznamExterniOdkazy.FindAsync(11);
        ev!.ZaradidDoVyzvy.Should().BeTrue();
        ev.VyzvaId.Should().BeNull(
            "switch znamená jen čekání v bufferu — zařazení do výzvy je vždy vědomý přesun");
    }

    [Fact]
    public async Task ZaradidOff_PripravaVyzva_OdebereZVyzvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 5);
        db.Vyzvy.Add(new VyzvaEntity { Id = 60, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 13, ZaznamId = 5, TypOdkazuId = 1, Cislo = "555555", ZaradidDoVyzvy = true, VyzvaId = 60 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(13, false, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(13);
        ev!.ZaradidDoVyzvy.Should().BeFalse();
        ev.VyzvaId.Should().BeNull();
    }

    [Fact]
    public async Task ZaradidOff_OdeslanoVyzva_Locked()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 6);
        db.Vyzvy.Add(new VyzvaEntity { Id = 70, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 14, ZaznamId = 6, TypOdkazuId = 1, Cislo = "666666", ZaradidDoVyzvy = true, VyzvaId = 70 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(14, false, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.VyzvaIsLocked);
    }

    [Fact]
    public async Task NonPnfTyp_ExternalLinkNotPnf()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.CiselnikTypuExternichOdkazu.AddRange(
            new CiselnikTypuExternichOdkazuEntity { Id = 1, Kod = "PNF", Nazev = "PNF" },
            new CiselnikTypuExternichOdkazuEntity { Id = 2, Kod = "NES", Nazev = "NES" });
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 7);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 15, ZaznamId = 7, TypOdkazuId = 2, Cislo = "777777" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(15, true, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<PmTracker.Web.Services.Vyzvy.Unit>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ExternalLinkNotPnf);
    }
}
