using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// JS piny pro lištu panelu Výzev (2026-09-08). Aplikace nemá JS test runner, takže se
/// ověřuje zdroj modulu — stejný vzor jako VyzvyPresunJsTests.
///
/// „Uzavřít výzvu" sedí v liště nad layoutem, ale platí pro dlaždici vybranou v railu.
/// Vazbu drží selectTile; kdyby se ztratila, tlačítko by nabízelo akci nad špatnou výzvou.
/// </summary>
public sealed class VyzvyToolbarJsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string PanelController() => File.ReadAllText(Path.Combine(
        RepoRoot(), "PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js"));

    [Fact]
    public void SelectTile_RidiViditelnostTlacitkaUzavrit()
    {
        var src = PanelController();

        var start = src.IndexOf("function selectTile(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1);

        var konec = src.IndexOf("\n  function ", start + 1, StringComparison.Ordinal);
        var body = konec < 0 ? src[start..] : src[start..konec];

        body.Should().Contain("data-vyzvy-uzavrit",
            "přepnutí dlaždice musí přenastavit i tlačítko v liště");
        body.Should().Contain("vyzvyMuzeUzavrit",
            "o tom, zda lze uzavřít, rozhoduje příznak na dlaždici ze stavového automatu");
    }

    [Fact]
    public void Uzavrit_PosilaPrechodDoOdeslano()
    {
        var src = PanelController();

        src.Should().Contain("'uzavrit'", "lišta má vlastní akci");
        src.Should().Contain("Odeslano",
            "uzavření je přechod Příprava → Odesláno přes existující /vyzvy/zmenit-stav");
    }

    /// <summary>
    /// Tlačítko je v liště, tedy MIMO panel dlaždice. Id výzvy proto musí přijít
    /// z vybrané dlaždice, ne z okolí tlačítka — jinak by akce neměla cíl.
    /// </summary>
    [Fact]
    public void Uzavrit_BereIdVyzvyZVybraneDlazdice()
    {
        var src = PanelController();

        var start = src.IndexOf("function selectTile(", StringComparison.Ordinal);
        var konec = src.IndexOf("\n  function ", start + 1, StringComparison.Ordinal);
        var body = konec < 0 ? src[start..] : src[start..konec];

        body.Should().Contain("vyzvaId",
            "selectTile propíše id vybrané výzvy na tlačítko");
    }
}
