using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Task 2 (2026-10-12): klik na „Navrhnout změnu harmonogramu" nejdřív ověří dostupnost
/// (scheduleProposalGuard.js, endpoint Navrhy/ScheduleProposalAvailability z Task 1). Čekající
/// návrh ukáže dialog „Návrh nelze založit" místo přechodu na editor; bez čekajícího návrhu
/// klik normálně naviguje na CreateScheduleProposal.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ScheduleProposalGuardScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ScheduleProposalGuardScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    private async Task<int> CreateTaskRecordAsync()
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var taskStateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var nextNumber = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;

        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId,
            KategorieId = categoryId,
            StavUkoluId = taskStateId,
            CisloZaznamu = nextNumber,
            Nazev = "E2E navrh harmonogramu guard",
            VlastnikId = _fixture.AdminOsobaId,
            DatumZalozeni = DateTime.Today,
            DatumUkonceni = DateTime.Today.AddDays(30),
            SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        await db.SaveChangesAsync();
        return record.Id;
    }

    private async Task InsertPendingScheduleProposalAsync(int recordId)
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektoveZaznamy.Where(x => x.Id == recordId).Select(x => x.SubsystemId).FirstAsync();
        var payload = JsonSerializer.Serialize(new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = _fixture.ProjectId,
                ZaznamId = recordId,
                TerminUkonceni = DateTime.Today.AddDays(45),
                ChangesTermDeadline = true
            }
        });

        db.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            ProjektId = _fixture.ProjectId,
            ZaznamId = recordId,
            SubsystemId = subsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = "PENDING",
            PayloadJson = payload,
            CreatedByOsobaId = _fixture.AdminOsobaId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task DeleteRecordAsync(int recordId)
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // zaznam_navrhy.zaznam_id je ON DELETE CASCADE (db_upgrade_1_3_12) — smaže i čekající návrh.
        command.CommandText = "DELETE FROM dbo.projektove_zaznamy WHERE id = @id;";
        command.Parameters.AddWithValue("@id", recordId);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task NavrhSCekajicimNavrhem_UkazeDialog_ZustaneNaStrance()
    {
        var recordId = await CreateTaskRecordAsync();
        await InsertPendingScheduleProposalAsync(recordId);
        var page = await _fixture.NewPageAsync();
        try
        {
            var url = $"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}";
            await page.GotoAsync(url);
            await page.Locator("[data-schedule-proposal-check-url]").DispatchEventAsync("click");

            var dialog = page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]");
            await Expect(dialog).ToHaveCountAsync(1);
            await Expect(dialog).ToContainTextAsync("Návrh nelze založit");
            await Expect(page).ToHaveURLAsync(url);
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    [Fact]
    public async Task NavrhBezCekajicihoNavrhu_OtevreEditor()
    {
        var recordId = await CreateTaskRecordAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}");
            await page.Locator("[data-schedule-proposal-check-url]").DispatchEventAsync("click");

            await Expect(page).ToHaveURLAsync(new Regex(@"/Navrhy/CreateScheduleProposal(\?|$)"));
            await Expect(page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]")).ToHaveCountAsync(0);
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    /// <summary>
    /// Review fix 1 (2026-10-12): primární hlášený scénář je menu karty záznamu v záložce
    /// Záznamy (ne stránka záznamu) — položka "Navrhnout změnu harmonogramu" je uvnitř panelu
    /// [data-record-menu], který nese data-stop-propagation a je ovládaný ui/anchoredMenu.js
    /// (panel se mountuje do #floating-panel-root). Reálný trusted klik (ClickAsync) na kotvě
    /// ověří, že capture-phase listener scheduleProposalGuard.js vyhraje PŘED nativní navigací
    /// odkazu — DispatchEventAsync by to neprokázal (untrusted event nespustí default akci
    /// odkazu vůbec, viz RED evidence v task-2-report.md).
    /// </summary>
    [Fact]
    public async Task NavrhSCekajicimNavrhem_ZMenuKartyZaznamu_UkazeDialog_ZustaneNaStrance()
    {
        var recordId = await CreateTaskRecordAsync();
        await InsertPendingScheduleProposalAsync(recordId);
        var page = await _fixture.NewPageAsync();
        try
        {
            var listUrl = $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}";
            await page.GotoAsync(listUrl);

            var card = page.Locator($".record-card[data-record-id='{recordId}']");
            await Expect(card).ToHaveCountAsync(1);

            // gov-button host (menu trigger) je pro Playwright „not visible" (Stencil) —
            // DispatchEvent obchází actionability, stejný vzor jako RecordScheduleToggleScenariosTests.
            await card.Locator("[data-record-menu-trigger]").DispatchEventAsync("click");

            // Panel menu se mountuje mimo kartu, do #floating-panel-root.
            var menuItem = page.Locator("[data-record-menu] [data-schedule-proposal-check-url]");
            await Expect(menuItem).ToBeVisibleAsync();

            // Reálný trusted klik na plain <a> — ne gov-button, takže by měl být actionable.
            await menuItem.ClickAsync();

            var dialog = page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]");
            await Expect(dialog).ToHaveCountAsync(1);
            await Expect(dialog).ToContainTextAsync("Návrh nelze založit");
            await Expect(page).ToHaveURLAsync(listUrl);
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }
}
