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

    /// <summary>
    /// gov-dialog 4.7.0 vykreslí nadpis jen ze slotu "title". Text v nepoužitém slotu v DOM je
    /// (ToContainTextAsync projde), ale vidět není. gov-dialog nemá shadow DOM — Stencil slotovaný
    /// prvek fyzicky přesune do .gov-dialog__title, takže se ověřuje tohle umístění a výška.
    /// </summary>
    private static async Task ExpectDialogTitleRenderedAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            """
            () => {
                const dialog = document.querySelector("gov-dialog[data-schedule-proposal-blocked-dialog]");
                const title = dialog && [...dialog.querySelectorAll(".gov-dialog__title *")]
                    .find(el => el.textContent.trim() === "Návrh nelze založit");
                return !!title && title.getBoundingClientRect().height > 0;
            }
            """,
            null,
            new() { Timeout = 5000 });
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
            // Review fix I1 (2026-10-12): trusted klik, ne DispatchEventAsync — ten obchází
            // gov-button vnitřní <a> a tím i gov-click adaptér (eventBus.js), takže by neprokázal
            // skutečné chování. gov-button host je pro Playwright "not visible" (Stencil) → Force.
            await page.Locator("[data-schedule-proposal-check-url]").ClickAsync(new() { Force = true });

            var dialog = page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]");
            await Expect(dialog).ToHaveCountAsync(1);
            await ExpectDialogTitleRenderedAsync(page);
            await Expect(page).ToHaveURLAsync(url);
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    /// <summary>
    /// Review fix I1 (2026-10-12): Ctrl/Cmd-click na tlačítko na stránce záznamu dřív otevřel
    /// novou kartu (správně — nativní akce odkazu) A ZÁROVEŇ naviguoval i aktuální kartu, protože
    /// gov-click adaptér (eventBus.js) re-dispatchoval click bez modifikátorů a guard ho vyhodnotil
    /// jako "plain left click". Po fixu (isAdapterClick) guard syntetický click ignoruje — aktuální
    /// karta musí zůstat na /Zaznamy/Detail a dialog se nesmí objevit; nová karta (pokud ji
    /// Chromium v této konfiguraci otevře) se jen zavře.
    /// </summary>
    [Fact]
    public async Task NavrhSCekajicimNavrhem_CtrlKlikNaDetailu_NenavigujeAniNeukazeDialog()
    {
        var recordId = await CreateTaskRecordAsync();
        await InsertPendingScheduleProposalAsync(recordId);
        var page = await _fixture.NewPageAsync();
        try
        {
            var url = $"{_fixture.BaseUrl}/Zaznamy/Detail/{recordId}?asUser={_fixture.AdminOsobaId}";
            await page.GotoAsync(url);

            IPage? popup = null;
            void OnPage(object? sender, IPage opened) => popup = opened;
            page.Context.Page += OnPage;
            try
            {
                await page.Locator("[data-schedule-proposal-check-url]").ClickAsync(new()
                {
                    Force = true,
                    Modifiers = new[] { KeyboardModifier.ControlOrMeta }
                });
                // Dost času na to, aby se guard (pokud by chyboval) stihl dotázat a navigovat.
                await page.WaitForTimeoutAsync(500);
            }
            finally
            {
                page.Context.Page -= OnPage;
            }

            if (popup is not null)
            {
                await popup.CloseAsync();
            }

            await Expect(page).ToHaveURLAsync(url);
            await Expect(page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]")).ToHaveCountAsync(0);
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
            // Review fix I1 (2026-10-12): trusted klik (viz vysvětlení v testu výše).
            await page.Locator("[data-schedule-proposal-check-url]").ClickAsync(new() { Force = true });

            await Expect(page).ToHaveURLAsync(new Regex(@"/Navrhy/CreateScheduleProposal(\?|$)"));
            await Expect(page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]")).ToHaveCountAsync(0);
            // Editor se opravdu vykreslil (ne chybová stránka) — navigace skriptem musí nést ?asUser.
            await Expect(page.Locator("[data-record-proposal-reason]")).ToHaveCountAsync(1);
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

            // Panel menu se mountuje mimo kartu, do #floating-panel-root. V plném běhu sady jsou
            // v projektu i karty z jiných testů → položku vybrat podle záznamu (konec URL ověření).
            var menuItem = page.Locator($"[data-record-menu] [data-schedule-proposal-check-url$='zaznamId={recordId}']");
            await Expect(menuItem).ToBeVisibleAsync();

            // Reálný trusted klik na plain <a> — ne gov-button, takže by měl být actionable.
            await menuItem.ClickAsync();

            var dialog = page.Locator("gov-dialog[data-schedule-proposal-blocked-dialog]");
            await Expect(dialog).ToHaveCountAsync(1);
            await ExpectDialogTitleRenderedAsync(page);
            await Expect(page).ToHaveURLAsync(listUrl);
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }

    /// <summary>
    /// Task 6 (2026-10-12): v editoru návrhu se přepínač „Automatické vyplňování harmonogramu"
    /// podbarví (.proposal-field-changed) stejně jako ostatní změněná pole návrhu, když se liší
    /// od původního stavu na záznamu (data-record-rezim-original), a odbarví se po přepnutí zpět.
    /// </summary>
    [Fact]
    public async Task EditorNavrhu_PrepnutiRezimu_PodbarviPrepinac_ZpetOdbarvi()
    {
        var recordId = await CreateTaskRecordAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Navrhy/CreateScheduleProposal?projektId={_fixture.ProjectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
            var sw = page.Locator("gov-form-switch[data-record-rezim-switch]");
            await Expect(sw).ToHaveAttributeAsync("data-record-rezim-original", "Auto");

            // Ruling (2026-10-12): DispatchEventAsync("gov-change", new { detail = ... }) nevytváří
            // CustomEvent s detail — Playwright .NET volí typ události podle jména, ne podle
            // dodaných dat, takže event.detail v handleru zůstává undefined. Fallback z briefu:
            // window.dispatchEvent přes page.EvaluateAsync, které vytvoří skutečný CustomEvent.
            const string dispatchGovChange =
                "([sel, checked]) => document.querySelector(sel).dispatchEvent(new CustomEvent('gov-change', { bubbles: true, detail: { checked } }))";
            const string selector = "gov-form-switch[data-record-rezim-switch]";

            // gov-change s detail.checked je autoritativní vstup handleru (rezim-master-switch.js).
            await page.EvaluateAsync(dispatchGovChange, new object[] { selector, false });
            await Expect(sw).ToHaveClassAsync(new Regex("proposal-field-changed"));
            await page.EvaluateAsync(dispatchGovChange, new object[] { selector, true });
            await Expect(sw).Not.ToHaveClassAsync(new Regex("proposal-field-changed"));
        }
        finally
        {
            await page.Context.CloseAsync();
            await DeleteRecordAsync(recordId);
        }
    }
}
