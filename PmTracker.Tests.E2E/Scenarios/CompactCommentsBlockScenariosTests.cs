using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22: vyjádření v jednom bloku. 5 jednořádkových ≤ 200 px; bublina s autorem na štítku
/// (myš i Tab); ikony Upravit / Smazat při najetí na řádek, u uzavřeného jednání nikdy; posledních 10
/// vyjádření a stránkování v hlavičce vlevo od řazení (R10, R11).
/// Jednání 9731–9736 (pod 9820), data v minulosti.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class CompactCommentsBlockScenariosTests
{
    private const int FirstMeetingNumber = 9731;
    private const string EditedText = "E2E upravene jednoradkove vyjadreni";
    private readonly E2ETestFixture _fixture;

    public CompactCommentsBlockScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    private sealed record Seed(int RecordId, IReadOnlyList<int> MeetingIds, IReadOnlyList<int> CommentIds, int ClosedCommentId);

    private sealed class Rect
    {
        public double Left { get; set; }
        public double Right { get; set; }
        public double CenterY { get; set; }
    }

    private async Task<Seed> SeedAsync(bool withClosedMeetingComment, int extraComments = 0)
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var taskStateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var openStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync();
        var closedStateId = await db.CiselnikStavuJednani.Where(x => x.Kod == "CLOSED").Select(x => x.Id).FirstAsync();
        var nextNumber = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId, KategorieId = categoryId, StavUkoluId = taskStateId,
            CisloZaznamu = nextNumber, Nazev = "E2E kompaktni vyjadreni", VlastnikId = _fixture.AdminOsobaId,
            DatumZalozeni = DateTime.Today.AddDays(-60), DatumUkonceni = DateTime.Today.AddDays(30), SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        var meetings = Enumerable.Range(0, 6).Select(i => new JednaniEntity
        {
            ProjektId = _fixture.ProjectId, CisloJednani = FirstMeetingNumber + i,
            DatumPlanovane = DateTime.Today.AddDays(-30 + i), CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E kompaktni", StavJednaniId = i == 5 ? closedStateId : openStateId
        }).ToList();
        db.Jednani.AddRange(meetings);
        await db.SaveChangesAsync();

        var comments = meetings.Take(5).Select((m, i) => new VyjadreniEntity
        {
            ZaznamId = record.Id, JednaniId = m.Id, AutorOsobaId = _fixture.AdminOsobaId,
            TextVyjadreni = $"<p>E2E jednoradkove vyjadreni {i + 1}</p>",
            TextVyjadreniProstyText = $"E2E jednoradkove vyjadreni {i + 1}",
            DatumVyjadreni = m.DatumPlanovane.AddHours(10)
        }).ToList();
        db.Vyjadreni.AddRange(comments);
        db.Vyjadreni.AddRange(Enumerable.Range(1, extraComments).Select(n => new VyjadreniEntity
        {
            ZaznamId = record.Id, JednaniId = meetings[0].Id, AutorOsobaId = _fixture.AdminOsobaId,
            TextVyjadreni = $"<p>E2E dalsi vyjadreni {n}</p>",
            TextVyjadreniProstyText = $"E2E dalsi vyjadreni {n}",
            DatumVyjadreni = meetings[0].DatumPlanovane.AddHours(10).AddMinutes(n)
        }));
        VyjadreniEntity? closedComment = null;
        if (withClosedMeetingComment)
        {
            closedComment = new VyjadreniEntity
            {
                ZaznamId = record.Id, JednaniId = meetings[5].Id, AutorOsobaId = _fixture.AdminOsobaId,
                TextVyjadreni = "<p>E2E k uzavrenemu jednani</p>", TextVyjadreniProstyText = "E2E k uzavrenemu jednani",
                DatumVyjadreni = meetings[5].DatumPlanovane.AddHours(10)
            };
            db.Vyjadreni.Add(closedComment);
        }
        await db.SaveChangesAsync();
        return new Seed(record.Id, meetings.Select(m => m.Id).ToList(), comments.Select(c => c.Id).ToList(), closedComment?.Id ?? 0);
    }

    private async Task CleanupAsync(Seed seed)
    {
        await using var db = CreateDbContext();
        await db.Vyjadreni.Where(x => x.ZaznamId == seed.RecordId).ExecuteDeleteAsync();
        await db.ProjektoveZaznamy.Where(x => x.Id == seed.RecordId).ExecuteDeleteAsync();
        await db.Jednani.Where(x => seed.MeetingIds.Contains(x.Id)).ExecuteDeleteAsync();
    }

    private async Task<ILocator> OpenPanelAsync(IPage page, Seed seed)
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{seed.RecordId}?asUser={_fixture.AdminOsobaId}");
        var list = page.Locator("[data-comment-list]");
        await Expect(list).ToHaveCountAsync(1);
        return list;
    }

    /// <summary>Obdélník tlačítka: host pm-buttonu, nebo vnitřní prvek, když host nemá vlastní box.</summary>
    private static Task<Rect> ButtonRectAsync(ILocator host) => host.EvaluateAsync<Rect>(
        @"el => {
            let r = el.getBoundingClientRect();
            if (r.width === 0) { const inner = el.querySelector('button, a, .element'); if (inner) r = inner.getBoundingClientRect(); }
            return { left: r.left, right: r.right, centerY: r.top + r.height / 2 };
        }");

    [Fact]
    public async Task FiveOneLineComments_FitIn200px_AndTooltipShowsAuthorOnHoverAndFocus()
    {
        var seed = await SeedAsync(withClosedMeetingComment: false);
        var page = await _fixture.NewPageAsync();
        try
        {
            var list = await OpenPanelAsync(page, seed);
            var box = await list.BoundingBoxAsync();
            box.Should().NotBeNull();
            box!.Height.Should().BeLessThanOrEqualTo(200, "spec 2026-10-22: 5 jednořádkových vyjádření ≤ 200 px (dřív 600)");

            var lastRow = page.Locator($"[data-comment-item][data-comment-id='{seed.CommentIds[^1]}']");
            var tooltip = lastRow.Locator("[data-comment-meta]");
            (await tooltip.EvaluateAsync<string>("t => getComputedStyle(t).opacity")).Should().Be("0");
            await lastRow.Locator(".comment-meeting-chip").HoverAsync();
            await Expect(tooltip).ToHaveCSSAsync("opacity", "1");
            await Expect(tooltip).ToContainTextAsync(DateTime.Today.AddDays(-26).ToString("dd.MM.yyyy"));
            var tipBox = await tooltip.BoundingBoxAsync();
            var viewportWidth = await page.EvaluateAsync<int>("() => document.documentElement.clientWidth");
            (tipBox!.X + tipBox.Width).Should().BeLessThanOrEqualTo(viewportWidth, "bublina nevyjede vpravo z okna");

            await page.Mouse.MoveAsync(5, 5);
            await Expect(tooltip).ToHaveCSSAsync("opacity", "0");
            await lastRow.Locator(".comment-meeting-chip").FocusAsync();
            await Expect(tooltip).ToHaveCSSAsync("opacity", "1");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }

    [Fact]
    public async Task RowHover_ShowsGreyBackgroundAndActions_EditSaves_ClosedMeetingHasNone()
    {
        var seed = await SeedAsync(withClosedMeetingComment: true);
        var page = await _fixture.NewPageAsync();
        try
        {
            await OpenPanelAsync(page, seed);
            var row = page.Locator($"[data-comment-item][data-comment-id='{seed.CommentIds[0]}']");
            var actions = row.Locator("[data-comment-row-actions]");
            await Expect(actions).ToHaveCSSAsync("opacity", "0");
            var restingBg = await row.EvaluateAsync<string>("r => getComputedStyle(r).backgroundColor");

            await row.Locator("[data-comment-text]").HoverAsync();
            await Expect(actions).ToHaveCSSAsync("opacity", "1");
            var hoverBg = await row.EvaluateAsync<string>("r => getComputedStyle(r).backgroundColor");
            var expectedBg = await page.EvaluateAsync<string>(
                "() => { const d = document.createElement('div'); d.style.background = 'var(--background-neutral-subtle)'; document.body.appendChild(d); const c = getComputedStyle(d).backgroundColor; d.remove(); return c; }");
            hoverBg.Should().Be(expectedBg).And.NotBe(restingBg, "řádek pod myší je jemně šedý");

            await actions.Locator("[data-comment-edit-toggle]").DispatchEventAsync("click");
            await Expect(row.Locator("[data-comment-edit-form]")).Not.ToHaveAttributeAsync("hidden", "");
            await Expect(row.Locator(".comment-side")).ToBeHiddenAsync();
            await row.Locator("[data-comment-edit-cancel]").DispatchEventAsync("click");
            await Expect(row.Locator("[data-comment-edit-form]")).ToHaveAttributeAsync("hidden", "");
            await Expect(row.Locator(".comment-side")).ToBeVisibleAsync();

            await row.Locator("[data-comment-text]").HoverAsync();
            await actions.Locator("[data-comment-edit-toggle]").DispatchEventAsync("click");
            var editor = row.Locator("[data-comment-edit-form] .ql-editor").First;
            await Expect(editor).ToBeVisibleAsync();
            await editor.FillAsync(EditedText);
            var saveResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && response.Url.Contains("/Zaznamy/UpdateComment", StringComparison.OrdinalIgnoreCase));
            await row.Locator("[data-comment-edit-form] button[type='submit']").ClickAsync();
            var saveResponse = await saveResponseTask;
            saveResponse.Ok.Should().BeTrue(await saveResponse.TextAsync());
            await Expect(page.Locator($"[data-comment-item][data-comment-id='{seed.CommentIds[0]}'] [data-comment-text]"))
                .ToContainTextAsync(EditedText);

            var closedRow = page.Locator($"[data-comment-item][data-comment-id='{seed.ClosedCommentId}']");
            await closedRow.Locator("[data-comment-text]").HoverAsync();
            await Expect(closedRow.Locator("[data-comment-row-actions]")).ToHaveCountAsync(0);
            await Expect(closedRow.Locator("[data-comment-edit-toggle]")).ToHaveCountAsync(0);
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }

    [Fact]
    public async Task SortToggle_ReversesRowsInsideTheBlock()
    {
        var seed = await SeedAsync(withClosedMeetingComment: false);
        var page = await _fixture.NewPageAsync();
        try
        {
            await OpenPanelAsync(page, seed);
            var ids = page.Locator("[data-comment-list] > [data-comment-item]");
            var before = await ids.EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.commentId)");
            await page.Locator("[data-comment-sort-toggle]").DispatchEventAsync("click");
            var after = await ids.EvaluateAllAsync<string[]>("els => els.map(e => e.dataset.commentId)");
            after.Should().Equal(before.Reverse(), "řazení přesouvá řádky uvnitř bloku");
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }

    [Fact]
    public async Task TwelveComments_ShowTen_PaginationInHeaderLeftOfSort_BothDirections()
    {
        var seed = await SeedAsync(withClosedMeetingComment: false, extraComments: 7);
        var page = await _fixture.NewPageAsync();
        try
        {
            await OpenPanelAsync(page, seed);
            var rows = page.Locator("[data-comment-list] > [data-comment-item]");
            await Expect(rows).ToHaveCountAsync(10);

            var header = page.Locator(".record-comments-header");
            var loadMore = header.Locator("[data-record-comments-load-more]");
            var loadAll = header.Locator("[data-record-comments-load-all]");
            var sortToggle = header.Locator("[data-comment-sort-toggle]");

            foreach (var direction in new[] { "asc", "desc" })
            {
                if (direction == "desc")
                {
                    await sortToggle.DispatchEventAsync("click");
                    await Expect(page.Locator("[data-comment-sort-section]")).ToHaveAttributeAsync("data-comment-sort-direction", "desc");
                }

                await Expect(loadMore).ToHaveCountAsync(1);
                await Expect(loadAll).ToHaveCountAsync(1);
                var more = await ButtonRectAsync(loadMore);
                var all = await ButtonRectAsync(loadAll);
                var sort = await ButtonRectAsync(sortToggle);
                more.Right.Should().BeLessThanOrEqualTo(all.Left + 1, $"{direction}: Další je vlevo od Zobrazit vše");
                all.Right.Should().BeLessThanOrEqualTo(sort.Left + 1, $"{direction}: stránkování je vlevo od řazení (R11)");
                Math.Abs(more.CenterY - sort.CenterY).Should().BeLessThan(20, $"{direction}: v jedné řadě s řazením");
            }

            await loadMore.DispatchEventAsync("click");
            await Expect(rows).ToHaveCountAsync(12);
            await Expect(page.Locator("[data-record-comments-load-more]")).ToHaveCountAsync(0, new() { Timeout = 5000 });
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(seed);
        }
    }
}
