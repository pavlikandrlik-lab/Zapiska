using FluentAssertions;
using Microsoft.Data.SqlClient;
using PmTracker.Tests.Common;
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

    /// <summary>
    /// Review I1 (2026-10-08): HTML změněné mimo EF (rollback na binárky <= 1.4.6, budoucí
    /// SQL migrace, ruční SSMS oprava) nechá čistý text zastaralý — backfill doplňuje jen
    /// NULL, takže ho nic neopraví. db_reset_prosty_text_hledani.sql čistý text vynuluje,
    /// a teprve pak ho backfill dopočte znovu ze skutečného (nového) HTML.
    /// </summary>
    [Fact]
    public async Task ResetSkript_VynulujeZastaralyCistyText_ABackfillHoDopocteZNovehoHtml()
    {
        var db = await _fixture.CreateDatabaseAsync("reset_prosty_text");
        var seed = await SearchSeed.CreateAsync(db.ConnectionString);
        var zaznamId = await seed.AddRecordAsync(seed.ProjektId, "Starý", popis: "<p>staré</p>");

        // Simulace: binárky <= 1.4.6 (nebo ruční SQL) upraví HTML, ale čistý text neumí
        // vynulovat — zůstává zastaralé.
        await using (var connection = new SqlConnection(db.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE dbo.projektove_zaznamy SET popis = N'<p>nové <b>slovo</b></p>' WHERE id = @id";
            command.Parameters.AddWithValue("@id", zaznamId);
            await command.ExecuteNonQueryAsync();
        }

        await using var ctxPredResetem = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        (await ctxPredResetem.ProjektoveZaznamy.FindAsync(zaznamId))!.PopisProstyText.Should().Be("staré",
            "HTML se změnilo mimo EF — čistý text bez resetu zůstává zastaralý");

        await SqlScriptRunner.ExecuteScriptsAsync(db.ConnectionString,
            [Path.Combine(RepositoryPaths.Root, "db_reset_prosty_text_hledani.sql")]);

        await using var ctxPoResetu = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        (await ctxPoResetu.ProjektoveZaznamy.FindAsync(zaznamId))!.PopisProstyText.Should().BeNull(
            "reset vynuluje čistý text, aby ho příští start (backfill) dopočetl znovu");

        await RichTextSearchTextBackfillHostedService.BackfillAsync(ctxPoResetu, default);

        (await ctxPoResetu.ProjektoveZaznamy.FindAsync(zaznamId))!.PopisProstyText.Should().Be("nové slovo");
    }
}
