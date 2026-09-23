using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvyPanelBuilderTests
{
    [Fact]
    public async Task Build_ProjektBezMistaPlneni_MuzeZaloztVyzvuFalse()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = null, CisloRamcoveSmlouvy = "A",
        });
        await db.SaveChangesAsync();

        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        result.ProjektId.Should().Be(1);
        result.Buffer.MuzeZaloztVyzvu.Should().BeFalse();
        result.Buffer.DuvodBlokace.Should().Contain("Místo plnění");
    }

    [Fact]
    public async Task Build_ProjektBezCislaSmlouvy_MuzeZaloztVyzvuFalse()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = "Nějaké místo", CisloRamcoveSmlouvy = null,
        });
        await db.SaveChangesAsync();

        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        result.Buffer.MuzeZaloztVyzvu.Should().BeFalse();
        result.Buffer.DuvodBlokace.Should().Contain("Číslo rámcové smlouvy");
    }

    /// <summary>
    /// Od 2026-09-07 vzniká výzva prázdná (spec §5.2), takže prázdný buffer už
    /// zakládání nebrání — dřív to byla blokující podmínka.
    /// </summary>
    [Fact]
    public async Task Build_PrazdnyBuffer_ZalozeniNeblokuje()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        result.Buffer.Polozky.Should().BeEmpty();
        result.Buffer.MuzeZaloztVyzvu.Should().BeTrue();
        result.Buffer.DuvodBlokace.Should().BeNull();
    }

    [Fact]
    public async Task Build_KolapsVyzvyMimoPripravu_PripravaOtevrena()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        db.Vyzvy.AddRange(
            new VyzvaEntity
            {
                Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 1, 1),
                ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            },
            new VyzvaEntity
            {
                Id = 2, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2026, 2, 1),
                ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        result.Vyzvy.Should().HaveCount(2);
        var priprava = result.Vyzvy.First(v => v.Stav == "Priprava");
        var odeslana = result.Vyzvy.First(v => v.Stav == "Odeslano");

        priprava.Kolapsovano.Should().BeFalse();
        odeslana.Kolapsovano.Should().BeTrue();
    }

    [Fact]
    public async Task Build_PovoleneStavy_PripravaMaOdeslanoAZruseno()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow,
            ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);
        var v = result.Vyzvy.First();

        v.PovoleneStavy.Should().BeEquivalentTo(new[] { "Odeslano", "Zruseno" });
    }

    [Fact]
    public async Task Build_MuzeEditovatFalse_ZalozeniNedostupne()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: false, muzeTisknout: true, rok: 2026, ct: CancellationToken.None);

        // muzeEditovat: false → tlačítko nesmí být aktivní, i když projekt má vše vyplněné
        result.MuzeEditovat.Should().BeFalse();
        result.Buffer.MuzeZaloztVyzvu.Should().BeFalse();
    }
}
