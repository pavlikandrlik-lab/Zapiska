using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Barvy aplikace = gov tokeny v obou motivech (2026-10-15). Hlavní stránky nesmí mít barvu mimo
/// paletu gov + --app-* a žádný text pod WCAG kontrastem.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ThemePaletteAuditTests
{
    private readonly E2ETestFixture _fixture;

    public ThemePaletteAuditTests(E2ETestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task HlavniStranky_BarvyZGovPalety_AKontrastVPoradku(string theme)
    {
        var page = await _fixture.NewPageAsync();
        await page.Context.AddCookiesAsync([new Cookie { Name = "pmtracker.theme.mode", Value = theme, Url = _fixture.BaseUrl }]);
        var asUser = $"asUser={_fixture.AdminOsobaId}";

        var urls = new List<string> { "/", "/Projekty", "/Nastaveni", "/Search?q=a" };
        var projectPath = $"/Projekty/Detail/{_fixture.ProjectId}";
        urls.Add($"{projectPath}?tab=zaznamy");
        urls.Add($"{projectPath}?tab=jednani");
        urls.Add($"{projectPath}?tab=harmonogram");
        var (recordId, meetingId) = await SeedRecordAndMeetingAsync();
        urls.Add($"/Zaznamy/Detail/{recordId}");
        urls.Add($"/Zaznamy/Edit?id={recordId}");
        urls.Add($"/Jednani/Detail/{meetingId}");

        urls.Should().HaveCountGreaterThanOrEqualTo(10, "audit musí pokrýt i detail a editaci záznamu a detail jednání");

        var findings = new List<string>();
        foreach (var url in urls.Distinct())
        {
            var separator = url.Contains('?') ? '&' : '?';
            await page.GotoAsync($"{_fixture.BaseUrl}{url}{separator}{asUser}", new() { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForTimeoutAsync(500);
            (await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')")).Should().Be(theme);
            var result = await page.EvaluateAsync<ColorPaletteAudit.Result>(ColorPaletteAudit.Script);
            result.PaletteSize.Should().BeGreaterThan(50, "paleta se musí načíst z gov tokens.css");
            findings.AddRange(result.Off.Select(x => $"{url} mimo paletu: {x}"));
            findings.AddRange(result.Low.Select(x => $"{url} kontrast: {x}"));
        }

        findings.Should().BeEmpty(
            $"motiv {theme}: barvy patří do gov tokenů a text musí být čitelný (stránky: {string.Join(", ", urls.Distinct())}). Všechny nálezy:{Environment.NewLine}{string.Join(Environment.NewLine, findings)}{Environment.NewLine}");
        await page.Context.CloseAsync();
    }

    /// <summary>Vlastní běžící úkol s harmonogramem a otevřené jednání (vzor RecordDetailPageScenariosTests).</summary>
    private async Task<(int RecordId, int MeetingId)> SeedRecordAndMeetingAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @kat INT = (SELECT TOP (1) id FROM dbo.ciselnik_kategorii_zaznamu
                WHERE kod = N'U' OR nazev LIKE N'%kol%' ORDER BY CASE WHEN kod = N'U' THEN 0 ELSE 1 END, id);
            DECLARE @stav INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_ukolu WHERE is_final = 0 ORDER BY id);
            DECLARE @sub INT = (SELECT TOP (1) id FROM dbo.subsystemy ORDER BY id);

            IF NOT EXISTS (SELECT 1 FROM dbo.projekt_subsystemy
                WHERE projekt_id = @p AND subsystem_id = @sub AND datum_odebrani IS NULL)
                INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id, datum_prirazeni)
                VALUES (@p, @sub, SYSUTCDATETIME());

            DECLARE @rid INT = (SELECT TOP (1) id FROM dbo.projektove_zaznamy
                WHERE projekt_id = @p AND nazev = @marker ORDER BY id);
            IF @rid IS NULL
            BEGIN
                DECLARE @num INT = (SELECT ISNULL(MAX(cislo_zaznamu), 0) + 1 FROM dbo.projektove_zaznamy WHERE projekt_id = @p);
                INSERT INTO dbo.projektove_zaznamy
                    (projekt_id, kategorie_id, stav_ukolu_id, cislo_zaznamu, nazev, cil, popis,
                     vlastnik_id, datum_zalozeni, datum_ukonceni, subsystem_id)
                VALUES
                    (@p, @kat, @stav, @num, @marker, @marker, @marker,
                     @o, DATEADD(day, -30, CONVERT(DATE, GETDATE())), DATEADD(day, 45, CONVERT(DATE, GETDATE())), @sub);
                SET @rid = CAST(SCOPE_IDENTITY() AS INT);

                DECLARE @start DATE = DATEADD(day, -30, CONVERT(DATE, GETDATE()));
                DECLARE @i INT = 1;
                WHILE @i <= 10
                BEGIN
                    INSERT INTO dbo.zaznam_harmonogram_krok
                        (zaznam_id, poradi, plan_datum, skutecnost_datum, skutecnost_zdroj, skutecnost_rezim, updated_at)
                    VALUES
                        (@rid, @i, DATEADD(day, @i * 7, @start),
                         CASE WHEN @i <= 2 THEN DATEADD(day, @i * 7 + 2, @start) ELSE NULL END,
                         CASE WHEN @i <= 2 THEN 2 ELSE 0 END, 2, SYSUTCDATETIME());
                    SET @i = @i + 1;
                END
            END

            DECLARE @mid INT = (SELECT TOP (1) id FROM dbo.jednani WHERE projekt_id = @p AND cislo_jednani = 9941);
            IF @mid IS NULL
            BEGIN
                DECLARE @stateId INT = (SELECT TOP (1) id FROM dbo.ciselnik_stavu_jednani WHERE kod = N'OPEN');
                INSERT INTO dbo.jednani (projekt_id, cislo_jednani, datum_planovane, cas_zacatek, misto, stav_jednani_id)
                VALUES (@p, 9941, CONVERT(DATE, GETDATE()), '09:00', @marker, @stateId);
                SET @mid = CAST(SCOPE_IDENTITY() AS INT);
            END

            SELECT @rid, @mid;
            """;
        command.Parameters.AddWithValue("@p", _fixture.ProjectId);
        command.Parameters.AddWithValue("@o", _fixture.AdminOsobaId);
        command.Parameters.AddWithValue("@marker", "E2E audit barev");
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue("seed musí vrátit id záznamu a jednání");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }
}
