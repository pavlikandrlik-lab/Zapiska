using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// A7 (2026-07-08): X zavírá modal (block-close odstraněn), backdrop klik NEzavírá,
/// žádná JS chyba z konvergence gov self-close + closeModal().
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ModalCloseXScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ModalCloseXScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private static ILocatorAssertions Expect(ILocator l) => Assertions.Expect(l);

    [Fact]
    public async Task CloseX_ClosesModal_BackdropDoesNot()
    {
        var page = await _fixture.NewPageAsync();
        var jsErrors = new List<string>();
        page.PageError += (_, e) => jsErrors.Add(e);

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=tym&asUser={_fixture.AdminOsobaId}");
        await page.Locator("[data-modal-url*='AssignProjectRoleModal']").First.ClickAsync();
        await Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(1);

        // Backdrop klik (roh stránky mimo dialog) NEzavírá.
        await page.Mouse.ClickAsync(5, 5);
        await page.WaitForTimeoutAsync(300);
        await Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(1);

        // X zavírá.
        await page.Locator("gov-dialog .gov-dialog__close").ClickAsync();
        await Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(0);

        jsErrors.Should().BeEmpty("dvojitá close cesta (gov self-close + closeModal) nesmí házet");
        await page.Context.CloseAsync();
    }
}
