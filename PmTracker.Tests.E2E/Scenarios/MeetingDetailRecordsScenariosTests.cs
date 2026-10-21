using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Plán 2026-10-21: detail otevřeného jednání ukazuje záznamy; vyjádření na kartě má předvybrané
/// toto jednání, uloží se k němu a předvolba přežije obnovení panelu; uzavřené jednání ukáže zápis
/// v iframe. Jednání: číslo pod 9820, datum v minulosti (neodsunout jednání jiných scénářů).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class MeetingDetailRecordsScenariosTests
{
    private const int OpenMeetingNumber = 9711;
    private const int ClosedMeetingNumber = 9712;
    private const string RecordName = "E2E detail jednani zaznam";
    private const string CommentText = "E2E vyjadreni z detailu jednani";
    private readonly E2ETestFixture _fixture;

    public MeetingDetailRecordsScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    private sealed record Seed(int RecordId, int OpenMeetingId, int ClosedMeetingId);

    private async Task<Seed> SeedAsync()
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var taskStateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var nextNumber = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;
        var openStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync();
        var closedStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "CLOSED").Select(x => x.Id).FirstAsync();

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId,
            KategorieId = categoryId,
            StavUkoluId = taskStateId,
            CisloZaznamu = nextNumber,
            Nazev = RecordName,
            VlastnikId = _fixture.AdminOsobaId,
            DatumZalozeni = DateTime.Today.AddDays(-30),
            DatumUkonceni = DateTime.Today.AddDays(45),
            SubsystemId = subsystemId
        };
        var openMeeting = new JednaniEntity
        {
            ProjektId = _fixture.ProjectId,
            CisloJednani = OpenMeetingNumber,
            DatumPlanovane = DateTime.Today.AddDays(-1),
            CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E detail jednani",
            StavJednaniId = openStateId
        };
        var closedMeeting = new JednaniEntity
        {
            ProjektId = _fixture.ProjectId,
            CisloJednani = ClosedMeetingNumber,
            DatumPlanovane = DateTime.Today.AddDays(-2),
            CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E detail jednani",
            StavJednaniId = closedStateId
        };
        db.ProjektoveZaznamy.Add(record);
        db.Jednani.AddRange(openMeeting, closedMeeting);
        await db.SaveChangesAsync();
        return new Seed(record.Id, openMeeting.Id, closedMeeting.Id);
    }

    private async Task CleanupAsync(Seed seed)
    {
        await using var db = CreateDbContext();
        await db.Vyjadreni.Where(x => x.ZaznamId == seed.RecordId).ExecuteDeleteAsync();
        await db.ProjektoveZaznamy.Where(x => x.Id == seed.RecordId).ExecuteDeleteAsync();
        await db.Jednani.Where(x => x.Id == seed.OpenMeetingId || x.Id == seed.ClosedMeetingId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task OpenMeeting_CommentPreselectsMeeting_AndSavesToIt()
    {
        var seed = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{seed.OpenMeetingId}?asUser={_fixture.AdminOsobaId}");
            await Expect(page.Locator("[data-meeting-strip]")).ToHaveCountAsync(1);

            var card = page.Locator($".record-card[data-record-id='{seed.RecordId}']");
            await Expect(card).ToHaveCountAsync(1);
            await card.Locator("[data-record-toggle]").First.DispatchEventAsync("click");

            var select = card.Locator("form.comment-form select[name='JednaniId']");
            await Expect(select).ToHaveValueAsync(seed.OpenMeetingId.ToString());

            var editor = card.Locator("form.comment-form .ql-editor").First;
            await Expect(editor).ToBeVisibleAsync();
            await editor.FillAsync(CommentText);

            var saveResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && response.Url.Contains("/Zaznamy/AddComment", StringComparison.OrdinalIgnoreCase));
            await card.Locator("form.comment-form button[type='submit']").ClickAsync();
            var saveResponse = await saveResponseTask;
            saveResponse.Ok.Should().BeTrue(await saveResponse.TextAsync());

            await Expect(card.Locator(".comment-list .comment-text").First).ToContainTextAsync(CommentText);
            await Expect(select).ToHaveValueAsync(seed.OpenMeetingId.ToString(),
                new() { Timeout = 5000 });

            await using var db = CreateDbContext();
            var savedMeetingId = await db.Vyjadreni.AsNoTracking()
                .Where(x => x.ZaznamId == seed.RecordId)
                .Select(x => x.JednaniId)
                .SingleAsync();
            savedMeetingId.Should().Be(seed.OpenMeetingId, "vyjádření z detailu jednání patří k tomuto jednání");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }

    [Fact]
    public async Task ClosedMeeting_ShowsMinutes()
    {
        var seed = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{seed.ClosedMeetingId}?asUser={_fixture.AdminOsobaId}");

            await Expect(page.Locator("iframe[data-meeting-minutes-frame]")).ToHaveCountAsync(1);
            var frame = page.FrameLocator("iframe[data-meeting-minutes-frame]");
            await Expect(frame.Locator(".print-sheet")).ToHaveCountAsync(1);
            await Expect(page.Locator("[data-project-detail-root]")).ToHaveCountAsync(0);

            var frameHeight = await page.Locator("iframe[data-meeting-minutes-frame]")
                .EvaluateAsync<double>("f => f.getBoundingClientRect().height");
            var contentHeight = await page.Locator("iframe[data-meeting-minutes-frame]")
                .EvaluateAsync<double>("f => f.contentDocument.documentElement.scrollHeight");
            frameHeight.Should().BeGreaterThanOrEqualTo(contentHeight - 1, "rám má výšku podle zápisu — jeden posuvník");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }
}
