using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleBlockMarkupTests
{
    private static string LoadScheduleBlockSource()
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

        var viewPath = Path.Combine(
            directory.FullName,
            "PmTracker.Web",
            "Views",
            "Shared",
            "_ScheduleBlock.cshtml");

        File.Exists(viewPath).Should().BeTrue($"_ScheduleBlock.cshtml musí existovat na cestě {viewPath}");
        return File.ReadAllText(viewPath);
    }

    [Fact]
    public void EditorMode_ShouldRenderAllSteps_IncludingZeroDuration()
    {
        var source = LoadScheduleBlockSource();

        // Phase 9 (DESIGN-9-D, 2026-05-01): žádné skrývání kroků z žádného důvodu.
        // visibleCompactSteps používá kompletní Model.Kroky bez filteru
        // (memory project_harmonogram_visibility_rules: žádné skrývání kroků
        // podle typu napojeného ticketu nebo nulového trvání).
        source.Should().NotContain(
            "Model.Kroky.Where(krok => krok.TrvaniDni > 0)",
            "compact rainbow strip nesmí filtrovat zero-duration (DESIGN-9-D)");
        source.Should().Contain(
            "var visibleCompactSteps = Model.Kroky",
            "compact rainbow musí použít všechny kroky bez Where filtru");
    }

    [Fact]
    public void ZeroDurationSegments_ShouldBeHiddenViaInlineStyle()
    {
        var source = LoadScheduleBlockSource();

        // Server-rendered zero-duration segmenty v editoru mají display:none jako inline style.
        // JS je pak zobrazí přes applySegmentLayout, jakmile uživatel zadá kladné trvání.
        source.Should().Contain(
            "display:none;",
            "zero-duration / nevyplněné segmenty musí být skryty přes inline display:none");
        source.Should().Contain(
            "isPlannedHidden",
            "view musí rozlišovat skrytí plánového segmentu (TrvaniDni == 0)");
    }

    /// <summary>
    /// Regrese 2026-06-29: rozpadová (layered/breakdown) osa nelícovala s pruhy, zatímco
    /// overview osa ano. Overview osa nese serverové kanonické ticky (data-schedule-ticks)
    /// + DNES/TERMÍN pct → renderStaticTimelineAxes kreslí renderTicksFromList (full-width %,
    /// měsíční předěly, bez edge-insetu) = lícuje se server-pozicovanými segmenty. Breakdown
    /// osa měla jen data-axis-start/end → fallback renderTimelineAxis (edge-inset + syrový start
    /// + vzorkování dnů) → ticky neseděly. Fix: breakdown osa musí nést stejná kanonická data
    /// jako overview, aby šla stejnou (kanonickou) větví.
    /// </summary>
    [Fact]
    public void BreakdownAxis_ShouldCarryServerCanonicalTicks_LikeOverviewAxis()
    {
        var source = LoadScheduleBlockSource();

        source.Should().MatchRegex(
            "data-schedule-axis=\"breakdown\"[\\s\\S]{0,600}data-schedule-ticks",
            "breakdown osa musí nést serverové měsíční ticky (jinak JS fallback nelícuje s pruhy)");
        source.Should().MatchRegex(
            "data-schedule-axis=\"breakdown\"[\\s\\S]{0,600}data-axis-today-pct",
            "breakdown osa musí nést DNES pct ze serveru (kanonická pozice)");
        source.Should().MatchRegex(
            "data-schedule-axis=\"breakdown\"[\\s\\S]{0,600}data-axis-deadline-pct",
            "breakdown osa musí nést TERMÍN pct ze serveru (kanonická pozice)");
    }

    /// <summary>
    /// 2026-06-29: hover info parity. Overview segment ukazuje informace přes nativní
    /// atribut title="@segmentTitle". Layered (breakdown) segmenty měly místo toho
    /// data-segment-tooltip, který NIKDO nečte (mrtvý atribut — žádné JS/CSS/test) →
    /// při najetí myší se nic neukázalo. Fix: nativní title (stejný mechanismus jako
    /// overview) na obou breakdown segmentech + odstranění mrtvého data-segment-tooltip.
    /// </summary>
    [Fact]
    public void LayeredSegments_ShouldExposeNativeTitleTooltip_LikeOverview()
    {
        var source = LoadScheduleBlockSource();

        source.Should().MatchRegex(
            "schedule-layered-segment planned\"[\\s\\S]{0,400}title=\"",
            "plánový breakdown segment musí mít nativní title (hover info parity s overview)");
        source.Should().MatchRegex(
            "schedule-layered-segment actual\"[\\s\\S]{0,400}title=\"",
            "skutečnostní breakdown segment musí mít nativní title (hover info parity s overview)");
        source.Should().NotContain(
            "data-segment-tooltip",
            "mrtvý atribut data-segment-tooltip (nikdo ho nečte) musí být nahrazen nativním title");
    }

    /// <summary>
    /// 2026-06-29: při schvalování návrhu harmonogramu se změněné kroky nezvýrazňovaly —
    /// durationChanged/delayChanged byly po datum-model migraci natvrdo false. Diff data ale
    /// existují (HarmonogramBlockViewModel.EditorChangedTypeTooltips, per-krok klíč). Minimální
    /// oprava: odvodit zvýraznění + tooltip z tohoto slovníku, ne z hardcoded false/null.
    /// </summary>
    [Fact]
    public void EditorMode_HighlightsProposalChangedSteps_FromEditorDiff()
    {
        var source = LoadScheduleBlockSource();

        source.Should().NotContain("var durationChanged = false;",
            "zvýraznění změněných kroků návrhu nesmí být natvrdo vypnuté");
        source.Should().NotContain("var delayChanged = false;",
            "zvýraznění změněných kroků návrhu nesmí být natvrdo vypnuté");
        source.Should().Contain("EditorChangedTypeTooltips",
            "durationChanged/delayChanged + tooltipy se odvozují z per-krok diffu EditorChangedTypeTooltips");
    }

    [Fact]
    public void BreakdownSteps_ShouldRenderAllSteps_NoFilter()
    {
        var source = LoadScheduleBlockSource();

        // Phase 9 (DESIGN-9-D, 2026-05-01): breakdown nesmí filtrovat. Render všech
        // kroků včetně zero-duration / NULL OdchylkaDni — řízení viditelnosti přes
        // CSS (width:0 / visibility:hidden pro zero plan, žádný actual segment pro NULL).
        source.Should().NotContain(
            "Model.Kroky.Where(k => k.TrvaniDni > 0)",
            "breakdown nesmí filtrovat zero-duration řádky (DESIGN-9-D — žádné skrývání)");
        source.Should().Contain(
            "data-step-has-plan",
            "breakdown vystavuje data-step-has-plan attribute pro JS layout");
        source.Should().Contain(
            "data-step-has-actual",
            "breakdown vystavuje data-step-has-actual attribute (NULL OdchylkaDni → false)");
    }
}
