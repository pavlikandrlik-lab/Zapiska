using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Úprava #6 (2026-04-20): textová tlačítka "Upravit", "Smazat",
/// "Navrhnout termín a harmonogram" → icon-only (gov-icon) + aria-label + title.
/// </summary>
public sealed class DashboardActionButtonsTests
{
    [Fact]
    public void ZaznamPartial_EditButton_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));
        // gov-icon může mít atributy v libovolném pořadí (size="s" name="pencil" / name="pencil" size="s")
        view.Should().MatchRegex(@"<gov-icon[^>]*\bname=""pencil""",
            "Upravit button má gov-icon pencil");
        view.Should().Contain("aria-label=\"@summary.EditButtonLabel\"",
            "aria-label zachovává původní text pro accessibility");
    }

    [Fact]
    public void ZaznamPartial_ProposeAction_IsMenuItem()
    {
        // Toggle spec 2026-07-13: návrh změny harmonogramu už není ikonové tlačítko v hlavičce
        // (calendar-date/calendar-plus zrušeny) — je to textová položka svislého menu akcí.
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));
        view.Should().MatchRegex(
            @"record-actions-menu-item[\s\S]{0,200}summary\.ScheduleProposalUrl",
            "návrh změny harmonogramu je menu položka odkazující na ScheduleProposalUrl");
        view.Should().NotContain("calendar-date", "ikona kalendáře v hlavičce karty zanikla (přesun do menu)");
    }

    [Fact]
    public void CommentsPartial_EditDeleteButtons_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml"));
        view.Should().MatchRegex(@"<gov-icon[^>]*\bname=""pencil""",
            "Upravit vyjádření = icon pencil");
        // 2026-04-27: koš sjednocen na trash3 napříč aplikací (Bootstrap Icons 1.11.3,
        // přidaná do wwwroot/assets/icons/components/). Stará "trash" má zaoblenější tvar.
        view.Should().MatchRegex(@"<gov-icon[^>]*\bname=""trash3""",
            "Smazat vyjádření = icon trash3 (sjednoceno 2026-04-27)");
        view.Should().NotMatchRegex(
            @">Upravit<|>Smazat<",
            "žádný text label mimo aria-label (ikona + tooltip)");
    }
}
