using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// C1 (2026-07-10): šipka ← v breadcrumbs vrací na místo původu — origin (returnUrl)
/// z přehledu /Jednani, jinak kanonická záložka entity (tab=jednani / tab=navrhy).
/// Klik na projekt-drobeček zůstává homepage projektu.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class BreadcrumbBackOriginScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public BreadcrumbBackOriginScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    /// <summary>Otevřené jednání přímým SQL (vzor ApiSqlFixture.EnsureMeetingAsync).</summary>
    private async Task<int> EnsureMeetingAsync(int meetingNumber = 9820)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @existing INT = (SELECT TOP (1) id FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = @n);
            IF @existing IS NOT NULL
            BEGIN
                SELECT @existing;
                RETURN;
            END
            DECLARE @stateId INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani WHERE kod = 'OPEN');
            INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, misto, stav_jednani_id)
            VALUES (@p, @n, CONVERT(DATE, GETDATE()), '09:00', 'E2E breadcrumb origin', @stateId);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        command.Parameters.AddWithValue("@p", _fixture.ProjectId);
        command.Parameters.AddWithValue("@n", meetingNumber);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task MeetingDetail_FromOverview_BackArrowReturnsToOverview()
    {
        var meetingId = await EnsureMeetingAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Jednani?asUser={_fixture.AdminOsobaId}");

        // Karta jednání je <article data-href> s JS navigací (ne <a href>).
        var card = page.Locator($"article.meeting-card[data-href*='/Jednani/Detail/{meetingId}']").First;
        await Assertions.Expect(card).ToHaveAttributeAsync("data-href", new System.Text.RegularExpressions.Regex("returnUrl="));
        await card.ClickAsync();
        await page.WaitForURLAsync($"**/Jednani/Detail/{meetingId}**");

        await page.Locator(".app-breadcrumb-back").ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"/Jednani(\?|$)(?!Detail)"));
        page.Url.Should().NotContain("/Detail/");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task MeetingDetail_FromProjectTab_BackArrowReturnsToMeetingsTab()
    {
        var meetingId = await EnsureMeetingAsync();
        var page = await _fixture.NewPageAsync();

        // Přímý vstup na detail (bez returnUrl) = kanonická záložka Jednání.
        await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        await page.Locator(".app-breadcrumb-back").ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("tab=jednani"));

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task MeetingDetail_ProjectCrumbClick_GoesToProjectHomepage()
    {
        var meetingId = await EnsureMeetingAsync();
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");

        // Projekt-drobeček (link obsahující /Projekty/Detail/) → homepage projektu bez tab.
        await page.Locator($".app-breadcrumb-link[href*='/Projekty/Detail/{_fixture.ProjectId}']").ClickAsync();
        await page.WaitForURLAsync($"**/Projekty/Detail/{_fixture.ProjectId}**");
        page.Url.Should().NotContain("tab=", "homepage projektu = default záložka Záznamy");

        await page.Context.CloseAsync();
    }
}
