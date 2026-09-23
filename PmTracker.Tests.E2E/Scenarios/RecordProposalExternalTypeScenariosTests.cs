using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// V editoru NÁVRHU založení záznamu se externí vazba musí otypovat (Typ z tiketu) — sync už
/// není úplně vypnutý, jen odkládá harvest (4 datumy). Bez toho by schválení návrhu padlo na
/// „chybí typ externího záznamu". SD sync je stubovaný přes route interception.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordProposalExternalTypeScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordProposalExternalTypeScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateProposal_ExternalLink_ResolvesTypeFromTicket_AndDefersHarvest()
    {
        await EnsureAdminIsSubsystemLeadAsync();

        var page = await _fixture.NewPageAsync();

        // Stub SD sync: jakýkoli 6-ciferný ticket → typ PMP, bez harvest datumů.
        await page.RouteAsync("**/ExterniOdkaz/Sync", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"nalezeno\":true,\"cislo\":\"334565\",\"typ\":\"PMP\",\"strucne\":\"Stub\"," +
                       "\"datumObjednani\":null,\"planDodani\":null,\"datumDodani\":null,\"datumPrevzeti\":null,\"vyjadreni\":[]}"
            });
        });

        await page.GotoAsync($"{_fixture.BaseUrl}/Navrhy/CreateRecordProposal?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("form[data-is-proposal-editor='true']")).ToBeVisibleAsync();

        await page.Locator("[data-record-modal-tab='external']").First.ClickAsync();
        await page.Locator("[data-external-add]").First.ClickAsync();

        var row = page.Locator("[data-external-row]").Last;
        await Assertions.Expect(row).ToBeVisibleAsync();

        // Zadej číslo tiketu do gov-form-input hostu a odpal gov-input (jako reálný uživatel).
        await row.Locator("[data-external-cislo]").EvaluateAsync(
            @"(host) => {
                const inner = host.querySelector('input') || host;
                try { host.value = '334565'; } catch (e) {}
                if (inner && inner !== host) inner.value = '334565';
                if (inner) inner.dispatchEvent(new Event('input', { bubbles: true }));
                host.dispatchEvent(new CustomEvent('gov-input', { bubbles: true, detail: { value: '334565' } }));
            }");

        // Typ se doresolvuje z (stubovaného) SD — hidden pole musí nést PMP.
        var hidden = row.Locator("[data-external-type-hidden]");
        await Assertions.Expect(hidden).ToHaveValueAsync("PMP");

        // Harvest (4 datumy / chat) se v návrhu vůbec nerenderuje (HideExternalHarvestUi) — skutečnost
        // až po založení. Typ (metadata) se ale zachytil (viz výše).
        (await row.Locator("[data-external-dates]").CountAsync())
            .Should().Be(0, "harvest skutečnosti se v návrhu odkládá až po založení");

        await page.Context.CloseAsync();
    }

    private async Task EnsureAdminIsSubsystemLeadAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @ps int = (SELECT TOP 1 id FROM projekt_subsystemy
                               WHERE projekt_id = @projekt AND datum_odebrani IS NULL ORDER BY poradi, id);
            DECLARE @role int = (SELECT id FROM ciselnik_roli_subsystemu WHERE kod = 'VEDOUCI_SUBSYSTEMU');
            IF @ps IS NOT NULL AND @role IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM obsazeni_subsystemu_projektu
                WHERE projekt_subsystem_id = @ps AND osoba_id = @osoba
                    AND role_subsystemu_id = @role AND datum_odebrani IS NULL)
            INSERT INTO obsazeni_subsystemu_projektu (projekt_subsystem_id, osoba_id, role_subsystemu_id, datum_prirazeni)
            VALUES (@ps, @osoba, @role, SYSUTCDATETIME());
            """;
        command.Parameters.AddWithValue("@projekt", _fixture.ProjectId);
        command.Parameters.AddWithValue("@osoba", _fixture.AdminOsobaId);
        await command.ExecuteNonQueryAsync();
    }
}
