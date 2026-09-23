using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Rail Výzev filtruje výzvy podle zvoleného roku (spec 2026-09-07 §6.2).
/// Buffer je na roku nezávislý, proto ho tyto testy neřeší.
/// </summary>
public sealed class VyzvaServiceRokTests
{
    private static VyzvaEntity Vyzva(int id, int poradove, int rok, DateTime zalozeni)
        => new()
        {
            Id = id, ProjektId = 1, Kod = $"{poradove}/{rok}", PoradoveVRoce = poradove, Rok = rok,
            Stav = VyzvaStav.Priprava, DatumZalozeni = zalozeni, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        };

    [Fact]
    public async Task GetVyzvy_VraciJenVybranyRok_OdNejnovejsi()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            Vyzva(1, 1, 2026, new DateTime(2026, 1, 10)),
            Vyzva(2, 2, 2026, new DateTime(2026, 5, 20)),
            Vyzva(3, 7, 2025, new DateTime(2025, 3, 3)));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var vyzvy = await svc.GetVyzvyAsync(1, 2026, CancellationToken.None);

        vyzvy.Select(v => v.Kod).Should().ContainInOrder("2/2026", "1/2026");
        vyzvy.Should().HaveCount(2, "rok 2025 do výběru nepatří");
    }

    [Fact]
    public async Task GetRoky_VraciRokySVyzvami_Sestupne_BezDuplicit()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            Vyzva(1, 1, 2024, new DateTime(2024, 1, 10)),
            Vyzva(2, 2, 2026, new DateTime(2026, 5, 20)),
            Vyzva(3, 3, 2026, new DateTime(2026, 6, 20)));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var roky = await svc.GetRokyAsync(1, CancellationToken.None);

        roky.Should().ContainInOrder(2026, 2024);
        roky.Should().HaveCount(2, "roky se nesmí opakovat");
    }

    [Fact]
    public async Task GetRoky_IgnorujeJinyProjekt()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        var cizi = Vyzva(9, 1, 2023, new DateTime(2023, 2, 2));
        cizi.ProjektId = 2;
        db.Vyzvy.AddRange(Vyzva(1, 1, 2026, new DateTime(2026, 5, 20)), cizi);
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var roky = await svc.GetRokyAsync(1, CancellationToken.None);

        roky.Should().Equal(new[] { 2026 }, "rok cizího projektu se do nabídky nesmí dostat");
    }
}
