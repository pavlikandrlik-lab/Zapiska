using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class MeetingOverviewYearGroupingScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public MeetingOverviewYearGroupingScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GlobalMeetingsIndex_ShouldStartWithPreviewCurrentYear_AndKeepOlderYearsCollapsed()
    {
        var currentYear = DateTime.Today.Year;
        var projectId = await CreateProjectWithMeetingsAsync(
            currentYearMeetings: 7,
            previousYearMeetings: 2,
            currentYear: currentYear);
        var page = await _fixture.NewPageAsync();

        await page.SetViewportSizeAsync(760, 1200);
        await page.GotoAsync($"{_fixture.BaseUrl}/Jednani?projektId={projectId}&asUser={_fixture.AdminOsobaId}");

        var root = page.Locator("[data-meeting-overview='year-grouped']").First;
        var currentYearGroup = root.Locator($"[data-meeting-year='{currentYear}']");
        var currentYearBody = currentYearGroup.Locator("[data-meeting-year-body]");
        var previousYearGroup = root.Locator($"[data-meeting-year='{currentYear - 1}']");
        var previousYearBody = previousYearGroup.Locator("[data-meeting-year-body]");

        await Expect(currentYearBody).ToBeVisibleAsync();
        await Expect(previousYearBody).ToBeHiddenAsync();

        var previewVisibleIds = await ReadVisibleMeetingIdsAsync(currentYearBody);
        previewVisibleIds.Count.Should().BeGreaterThan(0);
        previewVisibleIds.Count.Should().BeLessThan(7);

        await currentYearGroup.Locator("[data-meeting-year-toggle]").ClickAsync();
        await Expect(currentYearBody).ToBeVisibleAsync();

        var fullyVisibleCurrentYearIds = await ReadVisibleMeetingIdsAsync(currentYearBody);
        fullyVisibleCurrentYearIds.Count.Should().Be(7);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjectMeetingsTab_ShouldStartWithPreviewRow_ThenOpenAndCollapseCurrentYear()
    {
        var currentYear = DateTime.Today.Year;
        var projectId = await CreateProjectWithMeetingsAsync(
            currentYearMeetings: 7,
            previousYearMeetings: 2,
            currentYear: currentYear);
        var page = await _fixture.NewPageAsync();

        await page.SetViewportSizeAsync(760, 1200);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{projectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");

        var currentYearGroup = page.Locator("[data-meeting-overview='year-grouped']").First.Locator($"[data-meeting-year='{currentYear}']");
        var currentYearBody = currentYearGroup.Locator("[data-meeting-year-body]");
        var previousYearGroup = page.Locator($"[data-meeting-year='{currentYear - 1}']").First;
        var previousYearBody = previousYearGroup.Locator("[data-meeting-year-body]");

        // Projektová záložka (docs/specs/meetings-year-grouping.md): historické roky
        // startují ve stavu OPEN = viditelné (user chce vidět plnou historii jednoho
        // projektu). Aktuální rok startuje v preview (jen první řádek karet).
        await Expect(currentYearBody).ToBeVisibleAsync();
        await Expect(previousYearBody).ToBeVisibleAsync();

        var previewVisibleCount = await currentYearBody.Locator("[data-meeting-card-wrap]:visible").CountAsync();
        previewVisibleCount.Should().BeGreaterThan(0);
        previewVisibleCount.Should().BeLessThan(7);

        // Historický rok ve stavu open má vidět všechna jednání (2).
        (await previousYearBody.Locator("[data-meeting-card-wrap]:visible").CountAsync()).Should().Be(2);

        // Klik na toggle aktuálního roku v preview režimu s hidden kartami → rozbalí vše
        await currentYearGroup.Locator("[data-meeting-year-toggle]").ClickAsync();
        var fullVisibleCount = await currentYearBody.Locator("[data-meeting-card-wrap]:visible").CountAsync();
        fullVisibleCount.Should().Be(7);

        // Druhý klik → collapsed
        await currentYearGroup.Locator("[data-meeting-year-toggle]").ClickAsync();
        await Expect(currentYearBody).ToBeHiddenAsync();

        // Historický rok (open) → klik jej zabalí
        await previousYearGroup.Locator("[data-meeting-year-toggle]").ClickAsync();
        await Expect(previousYearBody).ToBeHiddenAsync();

        await page.Context.CloseAsync();
    }

    private async Task<int> CreateProjectWithMeetingsAsync(int currentYearMeetings, int previousYearMeetings, int currentYear)
    {
        var dbOptions = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(_fixture.Database.ConnectionString)
            .Options;

        await using var dbContext = new PmTrackerDbContext(dbOptions);
        var projectStatusId = await dbContext.CiselnikStavuProjektu
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var meetingStatusId = await dbContext.CiselnikStavuJednani
            .Where(x => x.Kod == "OPEN")
            .Select(x => x.Id)
            .FirstAsync();

        var projectCode = $"E2EMTG{Guid.NewGuid():N}"[..12];
        var project = new ProjektEntity
        {
            Zkratka = projectCode,
            CelyNazev = $"E2E meeting overview {projectCode}",
            StavId = projectStatusId,
            PouzivatIdentJednani = false
        };

        dbContext.Projekty.Add(project);
        await dbContext.SaveChangesAsync();

        var nextMeetingNumber = 900;
        for (var index = 0; index < currentYearMeetings; index += 1)
        {
            dbContext.Jednani.Add(new JednaniEntity
            {
                ProjektId = project.Id,
                CisloJednani = nextMeetingNumber + index,
                DatumPlanovane = new DateTime(currentYear, 12 - index, Math.Max(1, 20 - index)),
                CasZacatek = new TimeOnly(9, 0),
                Misto = "E2E",
                StavJednaniId = meetingStatusId
            });
        }

        for (var index = 0; index < previousYearMeetings; index += 1)
        {
            dbContext.Jednani.Add(new JednaniEntity
            {
                ProjektId = project.Id,
                CisloJednani = 800 + index,
                DatumPlanovane = new DateTime(currentYear - 1, 11 - index, Math.Max(1, 18 - index)),
                CasZacatek = new TimeOnly(10, 0),
                Misto = "E2E",
                StavJednaniId = meetingStatusId
            });
        }

        await dbContext.SaveChangesAsync();
        return project.Id;
    }

    private static async Task<IReadOnlyList<string>> ReadVisibleMeetingIdsAsync(ILocator scope)
    {
        return await scope.Locator("[data-meeting-card-wrap]")
            .EvaluateAllAsync<string[]>(
                """
                nodes => nodes
                    .filter(node => {
                        const element = node instanceof HTMLElement ? node : null;
                        return !!element && !element.hidden && element.offsetParent !== null;
                    })
                    .map(node => node.getAttribute("data-meeting-id") || "")
                    .filter(Boolean)
                """);
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
