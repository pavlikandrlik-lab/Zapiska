using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Jednorázový dopočet čistého textu pro data uložená před db_upgrade_1_4_7 (uživatel
/// 2026-10-08). Test seeduje stará data přímým SQL bez čistého textu a ověřuje, že
/// dopočet doplní jen chybějící řádky a na prázdném HTML se nezacyklí.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RichTextSearchTextBackfillTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RichTextSearchTextBackfillTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Backfill_DoplniJenChybejici_ANezacykliSeNaPrazdnemHtml()
    {
        var db = await _fixture.CreateDatabaseAsync("backfill_prosty_text");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Starý", popis: "<p>pes a <b>kočka</b></p>", withPlainText: false);
        var prazdnyId = await seed.AddRecordAsync(seed.ProjektId, "Prázdný", popis: "<p></p>", withPlainText: false);
        var jednaniId = await seed.AddMeetingAsync(seed.ProjektId, cisloJednani: 1);
        var vyjadreniId = await seed.AddStatementAsync(zaznamId, jednaniId, "<p>A&nbsp;<i>B</i></p>", withPlainText: false);

        await using var ctx = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var doplneno = await RichTextSearchTextBackfillHostedService.BackfillAsync(ctx, default);

        doplneno.Should().Be(3);
        (await ctx.ProjektoveZaznamy.FindAsync(zaznamId))!.PopisProstyText.Should().Be("pes a kočka");
        (await ctx.ProjektoveZaznamy.FindAsync(prazdnyId))!.PopisProstyText.Should().Be(string.Empty);
        (await ctx.Vyjadreni.FindAsync(vyjadreniId))!.TextVyjadreniProstyText.Should().Be("A B");

        (await RichTextSearchTextBackfillHostedService.BackfillAsync(ctx, default))
            .Should().Be(0, "druhý běh nemá co dělat — prázdné HTML dalo \"\", ne NULL");
    }
}
