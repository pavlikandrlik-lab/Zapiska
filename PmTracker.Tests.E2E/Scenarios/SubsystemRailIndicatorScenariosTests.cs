using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class SubsystemRailIndicatorScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public SubsystemRailIndicatorScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SubsystemIndicator_ShouldBeHidden_AtTopOfPage()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");

        var indicator = page.Locator("[data-subsystem-scroll-indicator]");
        await Expect(page.Locator("[data-subsystem-scroll-indicator-rail]")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-subsystem-scroll-indicator-bubble]")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-subsystem-scroll-indicator-label]")).ToHaveCountAsync(1);
        await Expect(indicator).ToBeHiddenAsync();

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
