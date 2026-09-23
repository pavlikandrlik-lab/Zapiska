using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Invarianty oprav 2026-07-08:
/// 1) tlačítko „Zobrazit účast" (pm-button → gov-button) reaguje — guard přes isButtonLike,
///    ne instanceof HTMLButtonElement; label přes vnořený span (ne textContent na hostu).
/// 2) přepínač „Pouze aktivní záznamy" srovnaný se selecty (transform, gov ignoruje host padding).
/// 3) detail jednání nezobrazuje sekci „Zápis úkolů" (záznamy + vyjádření).
/// </summary>
public sealed class MeetingDetailAndFilterFixesTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    private static string ExtractFunction(string src, string signature)
    {
        var start = src.IndexOf(signature, StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, $"funkce {signature} má existovat");
        var brace = src.IndexOf('{', start);
        var depth = 0;
        for (var i = brace; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}')
            {
                depth--;
                if (depth == 0) return src.Substring(start, i - start + 1);
            }
        }
        throw new InvalidOperationException($"nepodařilo se ohraničit tělo {signature}");
    }

    [Fact] // 1 — guard
    public void AttendanceToggle_UsesButtonLikeGuard_NotInstanceofButton()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/pageSwitchers.js");
        var fn = ExtractFunction(js, "export function toggleMeetingAttendancePanel");
        fn.Should().Contain("isButtonLike(button)",
            "pm-button renderuje gov-button (ne HTMLButtonElement) → guard musí být isButtonLike");
        fn.Should().NotContain("button instanceof HTMLButtonElement",
            "instanceof HTMLButtonElement odmítne gov-button → tlačítko by nereagovalo");
        js.Should().Contain("import { isButtonLike }");
    }

    [Fact] // 1 — label bez rozbití gov-button slotu
    public void AttendanceToggle_SwapsLabelViaSpan_NotHostTextContent()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/pageSwitchers.js");
        var fn = ExtractFunction(js, "export function toggleMeetingAttendancePanel");
        fn.Should().Contain("data-meeting-attendance-label");
        fn.Should().NotContain("button.textContent =",
            "textContent na gov-button hostu rozbíjí Stencil slot (zdvojený label)");

        var detail = Read("PmTracker.Web/Views/Jednani/Detail.cshtml");
        detail.Should().Contain("data-meeting-attendance-label");
    }

    [Fact] // 2 — přepínač v rovině se selecty
    public void FilterSwitch_AlignedWithSelects_ViaTransform()
    {
        var css = Read("PmTracker.Web/wwwroot/css/site.css");
        var block = Regex.Match(
            css,
            @"\.filter-grid gov-form-switch\[size\]:not\(\.filter-inline-wide\)\s*\{[^}]*\}",
            RegexOptions.Singleline);
        block.Success.Should().BeTrue("přepínač Pouze aktivní záznamy musí mít vlastní zarovnávací pravidlo");
        block.Value.Should().Contain("translateY",
            "gov pozicuje knob ve shadow DOM přes --padding-ver → host padding nefunguje, nutný transform");
    }

    [Fact] // 3 — detail jednání bez záznamů/vyjádření
    public void MeetingDetail_DoesNotRenderRecordsSection()
    {
        var detail = Read("PmTracker.Web/Views/Jednani/Detail.cshtml");
        // Render markery odstraněné sekce (ne próza — komentář o odstranění je povolený).
        detail.Should().NotContain("_TaskItemPartial");
        detail.Should().NotContain("meeting-task-header");
        detail.Should().NotContain("task-list");
        detail.Should().NotContain("createRecordInMeetingUrl");
        detail.Should().NotContain("Model.Ukoly.Count");
        // Detaily jednání zůstávají.
        detail.Should().Contain("<h2>Účast</h2>");
        detail.Should().Contain("<h2>Stav jednání</h2>");
    }
}
