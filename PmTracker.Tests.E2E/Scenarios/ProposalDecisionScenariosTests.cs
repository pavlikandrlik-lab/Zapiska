using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>B3 (2026-07-09): guard-off jen na detailu návrhu — editor návrhu guardy drží.
/// C3 (2026-07-10): schvalování je kompletně read-only — sweep pin.</summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ProposalDecisionScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ProposalDecisionScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    /// <summary>Pending CREATE_RECORD návrh přímým SQL (vzor ApiSqlFixture.EnsurePendingCreateProposalAsync).</summary>
    private async Task<int> EnsurePendingProposalAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();

        await using (var existing = connection.CreateCommand())
        {
            existing.CommandText = "SELECT TOP (1) id FROM dbo.zaznam_navrhy WHERE projekt_id = @p AND stav = 'PENDING' AND typ_navrhu = 'CREATE_RECORD'";
            existing.Parameters.AddWithValue("@p", _fixture.ProjectId);
            var found = await existing.ExecuteScalarAsync();
            if (found is not null && found != DBNull.Value)
            {
                return Convert.ToInt32(found);
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @subsystemId INT, @subsystemKod NVARCHAR(100);
            -- Pozn.: sloupec je [kód] s diakritikou (legacy schéma) — EF property Kod se mapuje sem.
            SELECT TOP (1) @subsystemId = ps.subsystem_id, @subsystemKod = s.[kód]
            FROM dbo.projekt_subsystemy ps
            JOIN dbo.subsystemy s ON s.id = ps.subsystem_id
            WHERE ps.projekt_id = @p AND ps.datum_odebrani IS NULL
            ORDER BY ps.subsystem_id;

            INSERT INTO dbo.zaznam_navrhy (projekt_id, subsystem_id, typ_navrhu, stav, payload_json, created_by_osoba_id, created_at)
            VALUES (@p, @subsystemId, 'CREATE_RECORD', 'PENDING',
                '{"ProposalType":"CREATE_RECORD","CreateRecord":{"ProjektId":' + CAST(@p AS NVARCHAR(12))
                + ',"Kategorie":"U","Stav":"OPEN","Nazev":"E2E pending návrh","VlastnikId":' + CAST(@author AS NVARCHAR(12))
                + ',"DatumZalozeni":"' + CONVERT(NVARCHAR(10), GETDATE(), 23)
                + '","TerminUkonceni":"' + CONVERT(NVARCHAR(10), DATEADD(DAY, 30, GETDATE()), 23)
                + '","Subsystem":"' + @subsystemKod
                + '","VybraniSpolupracovniciIds":[],"ExterniVazby":[],"HarmonogramHodnoty":[]}}',
                @author, SYSUTCDATETIME());
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        command.Parameters.AddWithValue("@p", _fixture.ProjectId);
        command.Parameters.AddWithValue("@author", _fixture.AdminOsobaId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ProposalDecisionPage_HasNoEditableControls_OutsideWhitelist()
    {
        var proposalId = await EnsurePendingProposalAsync();
        var page = await _fixture.NewPageAsync();
        var response = await page.GotoAsync(
            $"{_fixture.BaseUrl}/Navrhy/ProposalDetail?projektId={_fixture.ProjectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        response.Should().NotBeNull();
        response!.Status.Should().Be(200);

        // C3: rezim switch je disabled a klik nepřepne stav.
        var rezimSwitch = page.Locator("gov-form-switch[data-record-rezim-switch]");
        await Assertions.Expect(rezimSwitch).ToHaveCountAsync(1);
        await Assertions.Expect(rezimSwitch).ToHaveAttributeAsync("disabled", new System.Text.RegularExpressions.Regex(".*"));
        var checkedBefore = await rezimSwitch.GetAttributeAsync("checked");
        await rezimSwitch.ClickAsync(new() { Force = true });
        await page.WaitForTimeoutAsync(250);
        (await rezimSwitch.GetAttributeAsync("checked")).Should().Be(checkedBefore, "disabled switch se kliknutím nesmí přepnout");

        // Sweep: žádný editovatelný datový prvek mimo whitelist (q = globální hledání v headeru;
        // vnitřní checkbox disabled gov-form-switch hostu nese interakční blok na hostu).
        var offenders = await page.EvaluateAsync<string[]>(
            """
            () => Array.from(document.querySelectorAll(
                    'input:not([type=hidden]):not([disabled]):not([readonly]), select:not([disabled]), textarea:not([disabled])'))
                .filter(el => el.getBoundingClientRect().height > 0 && !el.closest('[hidden]'))
                .filter(el => el.name !== 'q')
                .filter(el => !el.closest('gov-form-switch[disabled]'))
                .map(el => el.name || el.id || el.className.toString())
            """);
        offenders.Should().BeEmpty("na schvalování návrhu nesmí být žádný editovatelný datový prvek");
        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task CreateProposalEditor_DirtyBack_ShowsDialog()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var url = $"{_fixture.BaseUrl}/Navrhy/CreateRecordProposal?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}";
        var response = await page.GotoAsync(url);
        if (response is null || response.Status != 200)
        {
            return; // admin nemusí být lead v E2E seedu — scénář kryje Api test CreateProposalEditor_KeepsGuardOn
        }
        await page.Locator("input[name='Nazev']").FillAsync("Dirty návrh");
        try { await page.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit, Timeout = 3000 }); } catch { }
        await Assertions.Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(1);
        await page.GetByRole(AriaRole.Button, new() { Name = "Pokračovat v úpravách" }).ClickAsync();
        await page.Context.CloseAsync();
    }
}
