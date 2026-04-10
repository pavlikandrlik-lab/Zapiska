using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class PeopleIndexScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public PeopleIndexScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PeopleIndex_ShouldSupportClientSearch_AndColumnSorting()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Osoby?asUser={_fixture.AdminOsobaId}");

        var peopleCard = page.Locator("[data-osoby-table-card]");
        var searchInput = peopleCard.Locator("[data-table-tools-search-input]");
        await Expect(searchInput).ToBeVisibleAsync();

        var firstSurname = (await peopleCard.Locator("tbody tr[data-table-row] td").Nth(1).InnerTextAsync()).Trim();
        await searchInput.FillAsync(firstSurname);

        var visibleSurnames = await peopleCard.Locator("tbody tr[data-table-row]:not([hidden]) td:nth-child(2)").AllInnerTextsAsync();
        visibleSurnames.Should().NotBeEmpty();
        visibleSurnames.Should().OnlyContain(value => value.Contains(firstSurname, StringComparison.CurrentCultureIgnoreCase));

        await searchInput.FillAsync(string.Empty);

        var firstNameSortButton = peopleCard.Locator("[data-table-sort-button][data-table-sort-index='0']");
        await firstNameSortButton.ClickAsync();
        var ascendingNames = await peopleCard.Locator("tbody tr[data-table-row]:not([hidden]) td:first-child").AllInnerTextsAsync();
        ascendingNames.Should().Equal(ascendingNames.OrderBy(value => value, StringComparer.Create(new System.Globalization.CultureInfo("cs-CZ"), ignoreCase: true)));

        await firstNameSortButton.ClickAsync();
        var descendingNames = await peopleCard.Locator("tbody tr[data-table-row]:not([hidden]) td:first-child").AllInnerTextsAsync();
        descendingNames.Should().Equal(descendingNames.OrderByDescending(value => value, StringComparer.Create(new System.Globalization.CultureInfo("cs-CZ"), ignoreCase: true)));

        await searchInput.FillAsync("__no_match__");
        await Expect(peopleCard.Locator("[data-table-empty-row]")).ToBeVisibleAsync();

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
