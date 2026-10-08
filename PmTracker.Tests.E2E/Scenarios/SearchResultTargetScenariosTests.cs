using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Odkaz z vyhledávání (RecordSearchService.BuildDetailUrl) otevře na detailu projektu
/// vyjádření se shodou — i když není mezi prvními pěti načtenými — a hledaný text na
/// kartě 15 sekund podsvítí.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class SearchResultTargetScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public SearchResultTargetScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    private string RecordUrl(int recordId, string extra = "") =>
        $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&recordId={recordId}{extra}&asUser={_fixture.AdminOsobaId}";

    [Fact]
    public async Task OdkazZVyhledavani_NactePosuneAPodsvitiVyjadreniSeShodou()
    {
        var (recordId, targetCommentId) = await CreateRecordWithCommentsAsync();
        var page = await _fixture.NewPageAsync();

        try
        {
            // Předpoklad testu: cílové (nejstarší) vyjádření mezi prvními pěti načtenými není.
            await page.GotoAsync(RecordUrl(recordId));
            var card = page.Locator($".record-card[data-record-id='{recordId}']");
            await Expect(card.Locator("[data-comment-item]")).ToHaveCountAsync(5);
            await Expect(card.Locator($"[data-comment-id='{targetCommentId}']")).ToHaveCountAsync(0);

            await page.Clock.InstallAsync();
            // Spojka „a“ v hl se nepodsvítí (2026-10-08: jinak se rozsvítilo každé „a“ na kartě).
            await page.GotoAsync(RecordUrl(recordId, $"&vyjadreniId={targetCommentId}&hl={Uri.EscapeDataString("řešení a zálohy")}"));

            card = page.Locator($".record-card[data-record-id='{recordId}']");
            var target = card.Locator($"[data-comment-id='{targetCommentId}']");
            await Expect(target).ToBeVisibleAsync();
            await Expect(target).ToBeInViewportAsync();

            var marks = target.Locator("[data-comment-text] mark.app-search-flash");
            await Expect(marks).ToHaveTextAsync(new[] { "řešení", "zálohy" });
            // Jen dvě shody na celé kartě — nic ze skrytého formuláře úpravy (textarea s HTML).
            await Expect(card.Locator("mark.app-search-flash")).ToHaveCountAsync(2);

            await page.Clock.RunForAsync(16_000);
            await Expect(card.Locator("mark.app-search-flash")).ToHaveCountAsync(0);
            await Expect(target.Locator("[data-comment-text]")).ToHaveTextAsync("Rozhodnuto o řešení zálohy");
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    /// <summary>
    /// Uživatel 2026-10-08: fráze v uvozovkách se podsvítí jako jeden celek, i s krátkým slovem
    /// uvnitř („z“). Fráze leží v názvu záznamu.
    /// </summary>
    [Fact]
    public async Task OdkazSFrazi_PodsvitiFraziJakoCelek()
    {
        var (recordId, _) = await CreateRecordWithCommentsAsync();
        var page = await _fixture.NewPageAsync();

        try
        {
            await page.GotoAsync(RecordUrl(recordId, $"&hl={Uri.EscapeDataString("\"cíl z vyhledávání\"")}"));

            var card = page.Locator($".record-card[data-record-id='{recordId}']");
            var marks = card.Locator("mark.app-search-flash");
            await Expect(marks).ToHaveTextAsync(new[] { "cíl z vyhledávání" });
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    /// <summary>Uživatel 2026-10-08: fráze se podsvítí i přes tučné slovo.</summary>
    [Fact]
    public async Task OdkazSFraziPresFormatovani_PodsvitiCelouFrazi()
    {
        var (recordId, targetCommentId) = await CreateRecordWithCommentsAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync(RecordUrl(recordId, $"&vyjadreniId={targetCommentId}&hl={Uri.EscapeDataString("\"o řešení zálohy\"")}"));
            var target = page.Locator($".record-card[data-record-id='{recordId}'] [data-comment-id='{targetCommentId}']");
            var marks = target.Locator("[data-comment-text] mark.app-search-flash");
            // Tři úseky (před, uvnitř a za <strong>); Playwright text porovnává bez krajních mezer.
            await Expect(marks).ToHaveTextAsync(new[] { "o", "řešení", "zálohy" });
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    private async Task<(int RecordId, int TargetCommentId)> CreateRecordWithCommentsAsync()
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var taskStateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var openStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync();
        var nextNumber = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;
        var nextMeeting = (await db.Jednani.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloJednani).MaxAsync() ?? 0) + 1;

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId,
            KategorieId = categoryId,
            StavUkoluId = taskStateId,
            CisloZaznamu = nextNumber,
            Nazev = "E2E cíl z vyhledávání",
            VlastnikId = _fixture.AdminOsobaId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today.AddDays(30),
            SubsystemId = subsystemId
        };
        var meeting = new JednaniEntity
        {
            ProjektId = _fixture.ProjectId,
            CisloJednani = nextMeeting,
            DatumPlanovane = DateTime.Today,
            CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E",
            StavJednaniId = openStateId
        };
        db.ProjektoveZaznamy.Add(record);
        db.Jednani.Add(meeting);
        await db.SaveChangesAsync();

        // Nejstarší vyjádření nese shodu; panel nejdřív načte pět nejnovějších.
        var target = new VyjadreniEntity
        {
            ZaznamId = record.Id,
            JednaniId = meeting.Id,
            AutorOsobaId = _fixture.AdminOsobaId,
            TextVyjadreni = "<p>Rozhodnuto o <strong>řešení</strong> zálohy</p>",
            DatumVyjadreni = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        db.Vyjadreni.Add(target);
        for (var i = 1; i <= 7; i++)
        {
            db.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = record.Id,
                JednaniId = meeting.Id,
                AutorOsobaId = _fixture.AdminOsobaId,
                TextVyjadreni = $"<p>Běžné vyjádření {i}</p>",
                DatumVyjadreni = new DateTime(2026, 2, i, 9, 0, 0, DateTimeKind.Utc)
            });
        }

        await db.SaveChangesAsync();
        return (record.Id, target.Id);
    }

    private async Task DeleteRecordAsync(int recordId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM dbo.vyjadreni WHERE zaznam_id = @id;
            DELETE FROM dbo.zaznam_priority_uzivatelu WHERE zaznam_id = @id;
            DELETE FROM dbo.projektove_zaznamy WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@id", recordId);
        await command.ExecuteNonQueryAsync();
    }
}
