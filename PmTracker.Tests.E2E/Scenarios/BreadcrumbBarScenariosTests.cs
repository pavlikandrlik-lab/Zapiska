using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Drobečková lišta (frame bar pod menu): ✕ na entitním drobečku i šipka ← navigují na rodiče.
/// Na projektovém detailu je projekt aktuální drobeček, jeho rodič = seznam Projekty.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class BreadcrumbBarScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public BreadcrumbBarScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjectCrumbClose_NavigatesToProjectList()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");

        var bar = page.Locator(".app-breadcrumb-bar");
        await Assertions.Expect(bar).ToBeVisibleAsync();

        // ✕ na projektovém (aktuálním) drobečku → seznam Projekty
        await bar.Locator(".app-breadcrumb-close").First.ClickAsync();
        await page.WaitForURLAsync("**/Projekty**");
        page.Url.Should().Contain("/Projekty");
        page.Url.Should().NotContain("/Detail/");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task BackArrow_OnProjectDetail_GoesToProjectList()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}");

        await page.Locator(".app-breadcrumb-back").ClickAsync();
        await page.WaitForURLAsync("**/Projekty**");
        page.Url.Should().NotContain("/Detail/");

        await page.Context.CloseAsync();
    }
}
