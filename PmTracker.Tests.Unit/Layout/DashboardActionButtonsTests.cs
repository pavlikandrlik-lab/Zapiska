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
        view.Should().Contain("gov-icon name=\"pencil\"",
            "Upravit button má gov-icon pencil");
        view.Should().Contain("aria-label=\"@summary.EditButtonLabel\"",
            "aria-label zachovává původní text pro accessibility");
    }

    [Fact]
    public void ZaznamPartial_ProposeButton_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml"));
        view.Should().Contain("gov-icon name=\"calendar-clock\"",
            "Navrhnout termín má gov-icon calendar-clock");
        view.Should().Contain("aria-label=\"Navrhnout termín a harmonogram\"");
    }

    [Fact]
    public void CommentsPartial_EditDeleteButtons_ShouldBeIconOnly()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml"));
        view.Should().Contain("gov-icon name=\"pencil\"",
            "Upravit vyjádření = icon pencil");
        view.Should().Contain("gov-icon name=\"trash\"",
            "Smazat vyjádření = icon trash");
        view.Should().NotMatchRegex(
            @">Upravit<|>Smazat<",
            "žádný text label mimo aria-label (ikona + tooltip)");
    }
}
