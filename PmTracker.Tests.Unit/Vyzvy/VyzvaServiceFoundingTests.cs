using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Zakládání výzvy po revizi 2026-09-07 (spec §5): číslo se domlouvá externě se SVA,
/// aplikace ho negeneruje. Výzva vzniká prázdná — buffer se do ní už nevysává.
/// </summary>
public sealed class VyzvaServiceFoundingTests
{
    private static readonly DateTime Now2026 = new(2026, 4, 20);

    [Fact]
    public async Task ZalozitVyzvu_PrazdnyBuffer_Projde()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 3, 7, Now2026, CancellationToken.None);

        var ok = result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>().Which.Value;
        ok.Kod.Should().Be("3/2026");
        ok.Polozky.Should().BeEmpty("výzva vzniká prázdná, PNF se do ní přesouvají ručně");
    }

    [Fact]
    public async Task ZalozitVyzvu_NechavaBufferNedotceny()
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
        await svc.ZalozitVyzvuAsync(1, 1, 7, Now2026, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(500);
        ev!.VyzvaId.Should().BeNull("zakládání už buffer nevysává");
    }

    [Fact]
    public async Task ZalozitVyzvu_ZapiseSnapshotyAHistorii()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 5, 7, Now2026, CancellationToken.None);

        var ok = result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>().Which.Value;
        ok.PoradoveVRoce.Should().Be(5);
        ok.Rok.Should().Be(2026);
        ok.Stav.Should().Be(VyzvaStav.Priprava);
        ok.MistoPlneniSnapshot.Should().Be("FIS (EIS): VZ 8201");
        ok.CisloRamcoveSmlouvySnapshot.Should().Be("23106000271");
        db.VyzvaHistorieStavu.Should().ContainSingle(h => h.NovyStav == VyzvaStav.Priprava && h.PuvodniStav == null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task ZalozitVyzvu_CisloMimoRozsah_InvalidVyzvaNumber(int poradove)
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, poradove, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.InvalidVyzvaNumber);
    }

    [Fact]
    public async Task ZalozitVyzvu_ObsazeneCislo_DuplicateVyzvaNumber()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
            Stav = VyzvaStav.Odeslano, DatumZalozeni = Now2026, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 2, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.DuplicateVyzvaNumber);
    }

    [Fact]
    public async Task ZalozitVyzvu_StejneCisloJinyRok_Projde()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "2/2025", PoradoveVRoce = 2, Rok = 2025,
            Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2025, 4, 1), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 2, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>()
            .Which.Value.Kod.Should().Be("2/2026", "unikátnost je v rámci roku a smlouvy");
    }

    [Fact]
    public async Task ZalozitVyzvu_BezMistaPlneni_Error()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = null, CisloRamcoveSmlouvy = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 1, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ProjectMissingMistoPlneni);
    }

    [Fact]
    public async Task GetObsazenaCisla_VraciCislaRokuASmlouvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            new VyzvaEntity
            {
                Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = Now2026, ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
            },
            new VyzvaEntity
            {
                Id = 2, ProjektId = 1, Kod = "4/2026", PoradoveVRoce = 4, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = Now2026, ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
            },
            new VyzvaEntity
            {
                Id = 3, ProjektId = 1, Kod = "9/2025", PoradoveVRoce = 9, Rok = 2025,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2025, 1, 1), ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
            });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var obsazena = await svc.GetObsazenaCislaAsync(1, 2026, CancellationToken.None);

        obsazena.Should().BeEquivalentTo(new[] { 1, 4 });
    }
}
