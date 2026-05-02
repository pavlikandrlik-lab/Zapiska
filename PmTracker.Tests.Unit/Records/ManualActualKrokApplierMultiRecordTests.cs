using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Regression test pro FIX 2026-05-02 — ApplyAsync chyběl filter ZaznamId v query
/// načítající existing DURATION values. Bez něj načetla rows napříč všemi záznamy
/// se shodným TypId (TypId = sdílený PK ciselnik_harmonogram_typu, ne per-record),
/// ToDictionary failnula s duplicate key.
///
/// Reprodukce: 2 záznamy ve stejném schématu, oba mají DURATION řádky → query vrací
/// 2× stejný TypId → ToDictionary throw ArgumentException.
/// </summary>
public sealed class ManualActualKrokApplierMultiRecordTests
{
    private static readonly Guid K2Key = Guid.Parse("99999999-2222-2222-2222-000000000002");
    private const int SablonaVerze = 1;
    private const int K1DurationTypId = 1;
    private const int K2DurationTypId = 2;
    private const int K2DelayTypId = 102;

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("multi-record-" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task ApplyAsync_TwoRecordsSameSchema_DoesNotThrow_DuplicateKey()
    {
        // Arrange — 2 záznamy ve stejné schema verzi, oba mají DURATION rows pro stejné TypIds
        await using var db = NewDb();
        db.CiselnikHarmonogramTypu.AddRange(
            new HarmonogramTypEntity
            {
                Id = K1DurationTypId, Kod = "HS01_DURATION", Nazev = "K1", Hodnota = 5,
                SablonaVerze = SablonaVerze, KrokKey = Guid.NewGuid(), KrokPoradi = 1,
                JeZpozdeni = false, BarvaHex = "#111111"
            },
            new HarmonogramTypEntity
            {
                Id = K2DurationTypId, Kod = "HS02_DURATION", Nazev = "K2", Hodnota = 3,
                SablonaVerze = SablonaVerze, KrokKey = K2Key, KrokPoradi = 2,
                JeZpozdeni = false, BarvaHex = "#222222"
            },
            new HarmonogramTypEntity
            {
                Id = K2DelayTypId, Kod = "HS02_DELAY", Nazev = "K2 delay", Hodnota = 0,
                SablonaVerze = SablonaVerze, KrokKey = K2Key, KrokPoradi = 2,
                JeZpozdeni = true, BarvaHex = "#FF0000"
            });

        // Záznam 1 — má DURATION values pro K1 + K2
        db.ZaznamHarmonogramHodnoty.AddRange(
            new ZaznamHarmonogramHodnotaEntity
            {
                Id = 1, ZaznamId = 100, TypId = K1DurationTypId, HodnotaInt = 5,
                UpdatedAt = DateTime.UtcNow,
                SkutecnostRezim = SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
            },
            new ZaznamHarmonogramHodnotaEntity
            {
                Id = 2, ZaznamId = 100, TypId = K2DurationTypId, HodnotaInt = 3,
                UpdatedAt = DateTime.UtcNow,
                SkutecnostRezim = SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
            },
            // Záznam 2 — STEJNÉ TypIds (sdílené schema), JINÝ ZaznamId
            new ZaznamHarmonogramHodnotaEntity
            {
                Id = 3, ZaznamId = 200, TypId = K1DurationTypId, HodnotaInt = 7,
                UpdatedAt = DateTime.UtcNow,
                SkutecnostRezim = SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
            },
            new ZaznamHarmonogramHodnotaEntity
            {
                Id = 4, ZaznamId = 200, TypId = K2DurationTypId, HodnotaInt = 4,
                UpdatedAt = DateTime.UtcNow,
                SkutecnostRezim = SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
            });
        await db.SaveChangesAsync();

        var schema = new HarmonogramSchemaDefinition(
            SablonaVerze,
            "#FF0000",
            new List<HarmonogramTypPar>
            {
                new(1, "HS01_DURATION", "K1", "#111111", K1DurationTypId, 0),
                new(2, "HS02_DURATION", "K2", "#222222", K2DurationTypId, K2DelayTypId)
            });
        var plannedTypeIds = new HashSet<int> { K1DurationTypId, K2DurationTypId };

        var manualKroky = new List<ManualActualKrokDto>
        {
            new() { KrokKey = K2Key, AbsolutniDatum = new DateOnly(2026, 6, 10) }
        };

        var harmonogramService = new Mock<IHarmonogramService>();
        // Default empty timeline — pure pass-through pro test (Compute neseje na obsah).
        harmonogramService
            .Setup(x => x.BuildHarmonogramVypocetPublic(
                It.IsAny<DateTime>(),
                It.IsAny<IReadOnlyList<HarmonogramTypPar>>(),
                It.IsAny<IReadOnlyDictionary<int, int>?>()))
            .Returns(new List<HarmonogramVypocetKroku>
            {
                new(1, "HS01_DURATION", "K1", "#111111", K1DurationTypId, 0,
                    5, 0, new DateTime(2026, 1, 1), new DateTime(2026, 1, 6),
                    new DateTime(2026, 1, 1), new DateTime(2026, 1, 6)),
                new(2, "HS02_DURATION", "K2", "#222222", K2DurationTypId, K2DelayTypId,
                    3, 0, new DateTime(2026, 1, 6), new DateTime(2026, 1, 9),
                    new DateTime(2026, 1, 6), new DateTime(2026, 1, 9))
            });

        // Act — pre-fix: throws ArgumentException (duplicate K1/K2 TypId across records)
        // Post-fix: filtruje WHERE x.ZaznamId == 100 → unique TypIds → success.
        var act = async () => await ManualActualKrokApplier.ApplyAsync(
            zaznamId: 100,
            manualKroky: manualKroky,
            schema: schema,
            datumZalozeni: new DateTime(2026, 1, 1),
            plannedTypeIds: plannedTypeIds,
            submittedValues: Array.Empty<SaveRecordHarmonogramValueCommand>(),
            dbContext: db,
            harmonogramService: harmonogramService.Object,
            ct: CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().HaveCount(1);
        result.Subject[0].KrokPoradi.Should().Be(2);
        result.Subject[0].DelayTypId.Should().Be(K2DelayTypId);
    }
}
