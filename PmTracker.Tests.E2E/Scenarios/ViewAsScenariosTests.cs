using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Pohled jako: menu účtu → modal → výběr osoby → lišta pod hlavičkou → Zrušit pohled jako.
/// gov komponenty: host gov-buttonu je pro Playwright „not visible“, klik přes DispatchEvent
/// (položka menu) nebo vnitřní &lt;button&gt; (submit ve formuláři).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ViewAsScenariosTests
{
    private const string TargetJmeno = "E2EPohled";
    private const string TargetPrijmeni = "Cil";
    private readonly E2ETestFixture _fixture;

    public ViewAsScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private async Task EnsureTargetAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM dbo.osoby WHERE email = N'e2e.pohled.cil@pmtracker.test')
                INSERT INTO dbo.osoby (jmeno, prijmeni, email, organizace_id)
                SELECT @jmeno, @prijmeni, N'e2e.pohled.cil@pmtracker.test', (SELECT TOP (1) id FROM dbo.ciselnik_organizace ORDER BY id);
            """;
        command.Parameters.AddWithValue("@jmeno", TargetJmeno);
        command.Parameters.AddWithValue("@prijmeni", TargetPrijmeni);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Superadmin_ZobraziJakoJinyUzivatel_AZrusiPohled()
    {
        await EnsureTargetAsync();
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1400, 900);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(0);

        var userMenu = page.Locator("gov-dropdown.app-user-menu");
        await userMenu.Locator("gov-button button").First.ClickAsync(new() { Force = true });
        (await userMenu.EvaluateAsync<bool>("d => d.getState()")).Should().BeTrue();
        await page.Locator("[data-view-as-start] button").ClickAsync(new() { Force = true });
        // Modal je gov-dialog[data-modal-container]; panel výsledků pickeru se montuje jako plovoucí
        // (mimo formulář), proto ho hledáme na stránce.
        var modal = page.Locator("gov-dialog[data-modal-container]");
        await Assertions.Expect(modal.Locator("[data-view-as-modal]")).ToHaveCountAsync(1);
        (await userMenu.EvaluateAsync<bool>("d => d.getState()"))
            .Should().BeFalse("menu účtu se po otevření modalu z jeho položky musí zavřít, jinak visí nad dialogem");

        await modal.Locator("[data-person-picker-input]").FillAsync(TargetJmeno);
        var option = page.Locator("[data-person-picker-results] .office-search-item:not(.disabled)").First;
        await Assertions.Expect(option).ToBeVisibleAsync();
        await option.ClickAsync();
        await modal.Locator("form[data-view-as-modal]").EvaluateAsync("form => form.requestSubmit()");

        await page.WaitForURLAsync(url => url.Contains("/Dashboard", StringComparison.OrdinalIgnoreCase));
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(1);
        (await page.Locator("[data-view-as-target]").InnerTextAsync()).Should().Contain(TargetPrijmeni);

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-view-as-start]")).ToHaveCountAsync(0);

        await page.Locator("[data-view-as-stop] button").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/Projekty", StringComparison.OrdinalIgnoreCase));
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-view-as-start]")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }
}
