using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// 2026-06-29: vzájemný překlik záznam ⇄ harmonogram.
/// - crossTabNav.js přepne záložku (setActiveTab + ensureProjectTabLoaded) a sjede na kartu.
/// - Na kartě harmonogramu tlačítko „přejít na záznam" (data-goto-record, ikona list).
/// 2026-07-13 (toggle spec): na kartě záznamu se ikony calendar-date/calendar-plus přesunuly
/// do svislého textového menu (data-goto-schedule / návrh změny harmonogramu jako menu položky);
/// hooky a gating na MaHarmonogramHodnotu zůstávají, jen bez ikon. Markup karty detailně
/// pinuje RecordCardScheduleToggleMarkupTests.
/// </summary>
public sealed class RecordScheduleCrossNavTests
{
    private static string LoadText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void ProposalIsMenuItem_LinkingToScheduleProposalUrl()
    {
        var src = LoadText("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml");

        // Toggle spec 2026-07-13: návrh změny harmonogramu je textová položka svislého menu
        // (ikona calendar-plus zrušena), gated na CanCreateScheduleProposal + ScheduleProposalUrl.
        src.Should().Contain("summary.CanCreateScheduleProposal",
            "položka Navrhnout změnu harmonogramu je gated na oprávnění");
        src.Should().MatchRegex(
            "record-actions-menu-item[\\s\\S]{0,200}summary\\.ScheduleProposalUrl",
            "návrh změny harmonogramu je menu položka odkazující na ScheduleProposalUrl");
    }

    [Fact]
    public void GotoScheduleIsMenuItem_WithRecordId()
    {
        var src = LoadText("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml");

        // Toggle spec 2026-07-13: překlik na záložku Harmonogram je textová položka menu
        // (ikona calendar-date zrušena), stále nese id záznamu v data-goto-schedule.
        src.Should().Contain("data-goto-schedule=\"@summary.Id\"",
            "menu položka překliku na harmonogram nese id záznamu v hodnotě hooku");
        src.Should().MatchRegex(
            "record-actions-menu-item[\\s\\S]{0,120}data-goto-schedule=\"@summary.Id\"",
            "překlik na harmonogram je textová položka svislého menu");
    }

    [Fact]
    public void GotoScheduleButton_IsGatedOnHavingHarmonogramValue()
    {
        var src = LoadText("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml");

        // Záznam bez jediné hodnoty harmonogramu (ani plán, ani skutečnost) nemá v harmonogramu
        // dlaždici → tlačítko překliku se nesmí zobrazit. Gate na summary.MaHarmonogramHodnotu.
        src.Should().MatchRegex(
            "@if \\(summary\\.MaHarmonogramHodnotu\\)[\\s\\S]{0,200}data-goto-schedule",
            "tlačítko překliku na harmonogram se zobrazí jen když má záznam vyplněnou hodnotu harmonogramu");
    }

    [Fact]
    public void ScheduleCard_HasGotoRecordButton_WithListIconAndScrollTarget()
    {
        var src = LoadText("PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml");

        src.Should().MatchRegex(
            "class=\"schedule-card\"[\\s\\S]{0,500}data-schedule-record-id=\"@item.ZaznamId\"",
            "karta harmonogramu nese id záznamu jako scroll cíl pro překlik");
        src.Should().Contain("data-goto-record=\"@item.ZaznamId\"",
            "karta harmonogramu má tlačítko překliku na záznam nesoucí id v hodnotě hooku");
        src.Should().MatchRegex(
            "data-goto-record=\"@item.ZaznamId\"[\\s\\S]{0,300}name=\"list\"",
            "tlačítko překliku na záznam má ikonu list");
        src.Should().MatchRegex(
            "data-goto-record=\"@item.ZaznamId\"[\\s\\S]{0,200}data-stop-propagation=\"true\"",
            "tlačítko nesmí spustit toggle rozpadu (stop-propagation)");
    }

    [Fact]
    public void CrossTabNavModule_SwitchesTabLoadsAndScrollsToCard()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/crossTabNav.js");

        js.Should().Contain("data-goto-schedule", "reaguje na tlačítko záznam→harmonogram");
        js.Should().Contain("data-goto-record", "reaguje na tlačítko harmonogram→záznam");
        js.Should().Contain("setActiveTab", "přepne aktivní záložku");
        js.Should().Contain("ensureProjectTabLoaded", "počká na lazy-load cílové záložky (harmonogram)");
        js.Should().Contain("scrollIntoView", "sjede na cílovou kartu");
        js.Should().Contain(".schedule-card[data-schedule-record-id", "scroll cíl v harmonogramu");
        js.Should().Contain(".record-card[data-record-id", "scroll cíl v záznamech");
    }

    [Fact]
    public void Bootstrap_WiresCrossTabNav()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        js.Should().Contain("initCrossTabNav", "bootstrap musí inicializovat překlik záznam⇄harmonogram");
    }

    // 2026-07-05: po založení / editaci / schválení návrhu appka sjede na záznam a VYSVÍTÍ ho,
    // aby uživatel hned viděl výsledek a nemusel ho hledat. Deep-link reuse (data-record-target-id).
    [Fact]
    public void RecordDeepLink_FlashesTargetCard_WithCrossNavHighlight()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/recordLazyLoading.js");

        // Tělo funkce až po první uzavírací závorku na začátku řádku (konec top-level funkce).
        var start = js.IndexOf("export function initProjectRecordDeepLink", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "deep-link obsluhuje initProjectRecordDeepLink");
        var end = js.IndexOf("\n}\n", start, StringComparison.Ordinal);
        var body = js[start..end];

        body.Should().Contain("cross-nav-highlight",
            "deep-link cílové karty musí přidat flash třídu cross-nav-highlight (vysvícení záznamu)");
    }

    // 2026-07-01: „RozpadRozpad" na cross-tab cestě — reprodukováno v prohlížeči. Zápis
    // textContent na gov-button HOST během initu (než se komponenta dohydratuje) zdvojil
    // slotovaný label. Fix: label se píše JEN na skutečný user toggle (updateLabel:true);
    // init label nepíše (server ho už renderuje správně, kroky jsou vždy sbalené).
    [Fact]
    public void SyncScheduleExpandButton_WritesLabel_OnlyWhenUpdateLabelFlagSet()
    {
        var js = LoadText("PmTracker.Web/wwwroot/js/modules/schedule/index.js");

        // textContent zápis je pod flagem updateLabel (ne bezpodmínečně).
        js.Should().MatchRegex(
            "if \\(updateLabel\\)\\s*\\{\\s*button\\.textContent",
            "label na gov-button host se píše jen pod updateLabel flagem (jinak závod s hydratací → RozpadRozpad)");
        // toggle label mění → posílá updateLabel:true.
        js.Should().MatchRegex(
            "details\\.hidden = !expanded;[\\s\\S]{0,160}updateLabel: true",
            "user toggle posílá updateLabel:true (tlačítko je dohydratované, zápis bezpečný)");
    }
}
