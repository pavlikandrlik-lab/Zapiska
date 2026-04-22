using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniVazbaCardRenderingTests
{
    private static string ReadView()
    {
        var path = Path.Combine(
            FindRepoRoot(), "PmTracker.Web", "Views", "Projekty", "_EditZaznamExternalPanel.cshtml");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void ExternalPanel_ShouldUseNewCardGridClasses()
    {
        var view = ReadView();
        view.Should().Contain("external-field-cislo");
        view.Should().Contain("external-field-typ");
        view.Should().Contain("external-field-cena");
        view.Should().Contain("external-field-vyzva");
        view.Should().Contain("external-actions");
        view.Should().Contain("external-dates");
    }

    [Fact]
    public void ExternalPanel_ShouldHaveTypReadonly()
    {
        var view = ReadView();
        view.Should().Contain("data-external-type-display");
        view.Should().NotContain("data-external-type-select");
    }

    [Fact]
    public void ExternalPanel_ShouldHaveCislo6DigitPattern()
    {
        var view = ReadView();
        view.Should().Contain("pattern=\"[0-9]{6}\"");
        view.Should().Contain("maxlength=\"6\"");
    }

    [Fact]
    public void ExternalPanel_ShouldUseIconRemoveButton()
    {
        var view = ReadView();
        view.Should().Contain("data-external-remove");
        view.Should().Contain("gov-icon");
        view.Should().Contain("trash");
    }

    [Fact]
    public void ExternalPanel_ShouldUseChatIconButton()
    {
        var view = ReadView();
        view.Should().Contain("data-external-chat-open");
        // Používáme chat-dots ikonu (Bootstrap Icons) — v gov-kit/wwwroot nejsou
        // standalone "comment" ani "message-square" ikony, přidali jsme chat-dots.svg.
        view.Should().Contain("chat-dots");
    }

    [Fact]
    public void ExternalPanel_ShouldLabelSwitchAsVyzva()
    {
        var view = ReadView();
        view.Should().Contain(">Výzva<");
        view.Should().NotContain("Zařadit do další výzvy");
    }
}
