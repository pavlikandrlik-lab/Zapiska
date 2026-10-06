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
/// Rich text z Quillu se ukládá v Unicode a bez entit pro písmena (db_upgrade_1_4_6).
/// Celý okruh: Quill → uložení → databáze → zobrazení → Quill v úpravě → uložení beze změny.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RichTextUnicodeScenariosTests
{
    private const string QuillHtml = "<p>Řešení 😀 Жук → ≥ ✓ a &lt;tag&gt;</p>";
    private const string StoredHtml = "<p>Řešení 😀 Жук → ≥ ✓ a &lt;tag&gt;</p>";
    private const string VisibleText = "Řešení 😀 Жук → ≥ ✓ a <tag>";

    private readonly E2ETestFixture _fixture;

    public RichTextUnicodeScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    [Fact]
    public async Task Vyjadreni_ZQuillu_SeUloziBezEntit_AZobraziIOtevreVUpraveBezeZtraty()
    {
        var (recordId, meetingId) = await CreateRecordAsync("E2E Unicode vyjádření");
        var page = await _fixture.NewPageAsync();

        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&recordId={recordId}&asUser={_fixture.AdminOsobaId}");
            // ?recordId= kartu rozbalí sám (recordLazyLoading) — klik na přepínač by ji sbalil.
            var card = page.Locator($".record-card[data-record-id='{recordId}']");
            await Expect(card.Locator("form.comment-form .ql-editor").First).ToBeVisibleAsync();

            await card.EvaluateAsync(
                """
                (cardNode, html) => {
                    const textarea = cardNode.querySelector("form.comment-form textarea[name='Text'][data-rich-text='true']");
                    textarea._richTextEditor.clipboard.dangerouslyPasteHTML(html);
                }
                """, QuillHtml);
            await card.Locator("form.comment-form select[name='JednaniId']").SelectOptionAsync(meetingId.ToString());

            var addResponse = page.WaitForResponseAsync(r =>
                r.Request.Method == "POST" && r.Url.Contains("/Zaznamy/AddComment", StringComparison.OrdinalIgnoreCase));
            await card.Locator("form.comment-form button[type='submit']").ClickAsync();
            (await addResponse).Ok.Should().BeTrue();

            var commentId = await ReadSingleCommentIdAsync(recordId);
            (await ReadCommentTextAsync(commentId)).Should().Be(StoredHtml, "písmena napřímo, nebezpečné znaky jako entity");

            await page.ReloadAsync();
            card = page.Locator($".record-card[data-record-id='{recordId}']");
            var item = card.Locator("[data-comment-item]").First;
            await Expect(item.Locator("[data-comment-text]")).ToHaveTextAsync(VisibleText);

            await item.Locator("[data-comment-edit-toggle]").DispatchEventAsync("click");
            var editForm = item.Locator("form[data-comment-edit-form]");
            await Expect(editForm.Locator(".ql-editor")).ToHaveTextAsync(VisibleText);

            var updateResponse = page.WaitForResponseAsync(r =>
                r.Request.Method == "POST" && r.Url.Contains("/Zaznamy/UpdateComment", StringComparison.OrdinalIgnoreCase));
            await editForm.EvaluateAsync("form => form.requestSubmit()");
            (await updateResponse).Ok.Should().BeTrue();

            (await ReadCommentTextAsync(commentId)).Should().Be(StoredHtml, "úprava beze změny nesmí text posunout");
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    private async Task<(int RecordId, int MeetingId)> CreateRecordAsync(string nazev)
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
            Nazev = nazev,
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
        return (record.Id, meeting.Id);
    }

    private async Task<int> ReadSingleCommentIdAsync(int recordId)
    {
        await using var db = CreateDbContext();
        return await db.Vyjadreni.Where(x => x.ZaznamId == recordId).Select(x => x.Id).SingleAsync();
    }

    private async Task<string> ReadCommentTextAsync(int commentId)
    {
        await using var db = CreateDbContext();
        return await db.Vyjadreni.Where(x => x.Id == commentId).Select(x => x.TextVyjadreni).SingleAsync();
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
