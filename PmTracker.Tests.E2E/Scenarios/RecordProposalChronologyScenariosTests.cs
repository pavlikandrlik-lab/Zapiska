using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Návrh záložení záznamu nesmí projít s nesmyslnými (klesajícími) plán datumy harmonogramu.
/// Serverová validace (ScheduleChronologyValidator) běží i pro odeslání návrhu, ne jen pro přímé
/// uložení záznamu. Ověřuje se skrz reálný HTTP submit běžící aplikace (routing → anti-forgery →
/// model binding → služba → validace), nezávisle na klientském JS.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordProposalChronologyScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordProposalChronologyScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateProposal_ShouldRejectNonChronologicalPlanDates_ViaServerValidation()
    {
        await EnsureAdminIsSubsystemLeadAsync();

        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Navrhy/CreateRecordProposal?projektId={_fixture.ProjectId}&asUser={_fixture.AdminOsobaId}");

        var form = page.Locator("form[data-record-editor-form='true'][data-is-create='true']");
        await Assertions.Expect(form).ToBeVisibleAsync();
        (await page.Locator("input[name='HarmonogramHodnoty[0].PlanDatum']").CountAsync())
            .Should().BeGreaterThan(0, "formulář návrhu musí obsahovat plán datumy harmonogramu");

        // Odešleme reálný formulář přes server, ale s klesajícími plán datumy (plán kroku 1 pozdější
        // než plán kroku 2). VlastnikId vynutíme, aby validace došla až ke kontrole chronologie.
        var responseJson = await page.EvaluateAsync<string>(
            @"async (adminId) => {
                const form = document.querySelector(""form[data-record-editor-form='true']"");
                const poradiInputs = [...form.querySelectorAll('input[name^=\""HarmonogramHodnoty\""][name$=\"".Poradi\""]')];
                const byPoradi = {};
                for (const pi of poradiInputs) {
                    const idx = pi.name.match(/HarmonogramHodnoty\[(\d+)\]\.Poradi/)[1];
                    byPoradi[pi.value.trim()] = idx;
                }
                const fd = new FormData(form);
                fd.set('Nazev', 'E2E chronologie test');
                fd.set('VlastnikId', String(adminId));
                fd.set(`HarmonogramHodnoty[${byPoradi['1']}].PlanDatum`, '2026-12-31');
                fd.set(`HarmonogramHodnoty[${byPoradi['2']}].PlanDatum`, '2026-01-05');

                const action = form.getAttribute('action');
                const url = `${action}${action.includes('?') ? '&' : '?'}asUser=${adminId}`;
                const r = await fetch(url, {
                    method: 'POST',
                    body: fd,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    redirect: 'manual'
                });
                const text = await r.text();
                return JSON.stringify({ status: r.status, redirected: r.redirected, body: text });
            }",
            _fixture.AdminOsobaId);

        using var doc = JsonDocument.Parse(responseJson);
        var status = doc.RootElement.GetProperty("status").GetInt32();
        var redirected = doc.RootElement.GetProperty("redirected").GetBoolean();
        var body = doc.RootElement.GetProperty("body").GetString() ?? string.Empty;

        status.Should().Be(400, "neplatný návrh musí server odmítnout");
        redirected.Should().BeFalse("odmítnutý návrh nesmí projít / přesměrovat na úspěch");
        body.Should().Contain("RECORD_VALIDATION_FAILED");
        body.Should().Contain("nesmí být dříve", "validace musí hlásit chronologickou chybu plánu");

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
