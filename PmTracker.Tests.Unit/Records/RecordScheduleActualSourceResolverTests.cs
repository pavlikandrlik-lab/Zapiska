using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordScheduleActualSourceResolverTests
{
    private static readonly Guid K1 = Guid.Parse("33333333-3333-3333-3333-000000000001");
    private static readonly Guid K3 = Guid.Parse("33333333-3333-3333-3333-000000000003");
    private static readonly Guid K5 = Guid.Parse("33333333-3333-3333-3333-000000000005");

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("zdroj-" + Guid.NewGuid())
            .Options);

    private static async Task SeedSchemaAsync(PmTrackerDbContext db)
    {
        var rows = new[]
        {
            (id: 1, p: 1, k: K1, zpozdeni: false, idDelay: 101),
            (id: 2, p: 3, k: K3, zpozdeni: false, idDelay: 103),
            (id: 3, p: 5, k: K5, zpozdeni: false, idDelay: 105)
        };
        foreach (var r in rows)
        {
            db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Id = r.id, Kod = $"HS{r.p:D2}_DURATION", Nazev = $"K{r.p}", Hodnota = 5,
                SablonaVerze = 1, KrokKey = r.k, KrokPoradi = r.p, JeZpozdeni = r.zpozdeni,
                BarvaHex = "#EF4444"
            });
            db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Id = r.idDelay, Kod = $"HS{r.p:D2}_DELAY", Nazev = $"K{r.p} delay", Hodnota = 0,
                SablonaVerze = 1, KrokKey = r.k, KrokPoradi = r.p, JeZpozdeni = true,
                BarvaHex = "#DC2626"
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Resolve_NoBindingNoDelay_ShouldReportNone()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);

        var sut = new RecordScheduleActualSourceResolver(db);
        var result = await sut.ResolveForRecordAsync(42, 1);

        result.Should().ContainKey(K1);
        result[K1].Zdroj.Should().Be(ZdrojSkutecnosti.None);
    }

    [Fact]
    public async Task Resolve_ActiveBinding_ShouldReportFromVyjadreni()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = 1,
            ZaznamId = 42,
            KrokKey = K3,
            ExterniOdkazId = 501,
            HotVyjadreniId = 999,
            DatumVyjadreni = new DateTime(2026, 3, 14, 10, 0, 0),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new RecordScheduleActualSourceResolver(db);
        var result = await sut.ResolveForRecordAsync(42, 1);

        result[K3].Zdroj.Should().Be(ZdrojSkutecnosti.FromVyjadreni);
        result[K3].SourceVyjadreniId.Should().Be(999);
        result[K3].SourceExterniOdkazId.Should().Be(501);
    }

    [Fact]
    public async Task Resolve_ManualDelayOnly_ShouldReportManual()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            ZaznamId = 42, TypId = 105, HodnotaInt = 7, UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new RecordScheduleActualSourceResolver(db);
        var result = await sut.ResolveForRecordAsync(42, 1);

        result[K5].Zdroj.Should().Be(ZdrojSkutecnosti.Manual);
        result[K5].SourceVyjadreniId.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_VazbaTakesPrecedenceOverManualDelay()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            ZaznamId = 42, TypId = 103, HodnotaInt = 3, UpdatedAt = DateTime.UtcNow
        });
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = 1,
            ZaznamId = 42,
            KrokKey = K3,
            ExterniOdkazId = 501,
            HotVyjadreniId = 999,
            DatumVyjadreni = new DateTime(2026, 3, 14),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new RecordScheduleActualSourceResolver(db);
        var result = await sut.ResolveForRecordAsync(42, 1);

        result[K3].Zdroj.Should().Be(ZdrojSkutecnosti.FromVyjadreni);
    }

    [Fact]
    public async Task Resolve_SupersededBindings_AreIgnored()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = 1,
            ZaznamId = 42,
            KrokKey = K3,
            ExterniOdkazId = 501,
            HotVyjadreniId = 999,
            DatumVyjadreni = new DateTime(2026, 3, 14),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Superseded,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new RecordScheduleActualSourceResolver(db);
        var result = await sut.ResolveForRecordAsync(42, 1);

        result[K3].Zdroj.Should().Be(ZdrojSkutecnosti.None);
    }
}
