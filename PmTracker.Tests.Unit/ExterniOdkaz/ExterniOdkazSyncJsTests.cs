using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// JS piny pro předvyplnění textu požadavku (spec 2026-09-08 §5.4). Aplikace nemá JS
/// test runner, takže se ověřuje zdroj modulů — stejný vzor jako VyzvyPresunJsTests.
/// </summary>
public sealed class ExterniOdkazSyncJsTests
{
    private static string Js(string relativni)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
        return File.ReadAllText(Path.Combine(root, "PmTracker.Web/wwwroot/js/modules", relativni));
    }

    [Fact]
    public void RichText_MaMostNaWindow_ProStarsiModuly()
    {
        Js("recordEditor/richtext.js").Should().Contain("window.pmRichText",
            "sync.js je IIFE a nemůže importovat z ESM");
    }

    [Fact]
    public void Sync_PredvyplniTextPozadavkuJenUPnf()
    {
        var src = Js("externiOdkaz/sync.js");

        src.Should().Contain("data-external-pozadavek-input",
            "předvyplnění míří do editoru textu požadavku");
        src.Should().Contain("isPnf(", "u jiných typů vazby se nic nepředvyplňuje");
    }

    /// <summary>
    /// Rozepsaný text se nikdy nepřepíše — předvyplňuje se jen do prázdného pole.
    /// Jinak by pracovníkovi zmizelo, co už napsal, kdyby opravil číslo tiketu.
    /// </summary>
    [Fact]
    public void Sync_NeprepisujeJizNapsanyText()
    {
        var src = Js("externiOdkaz/sync.js");

        var start = src.IndexOf("function predvyplnPozadavek", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "předvyplnění má vlastní funkci");

        var konec = src.IndexOf("\n  function ", start + 1, StringComparison.Ordinal);
        var body = konec < 0 ? src[start..] : src[start..konec];

        body.Should().Contain("value", "rozhoduje se podle toho, jestli je pole prázdné");
    }
}
