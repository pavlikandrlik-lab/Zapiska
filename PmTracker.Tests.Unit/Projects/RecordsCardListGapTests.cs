using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>
/// 2026-06-29: mezera mezi dlaždicemi v záložce ZÁZNAMY musí být stejně velká jako
/// mezi dlaždicemi v záložce HARMONOGRAM (harmonogram = cílový stav, nesahat na něj).
///
/// Efektivní svislá mezera = grid `gap` kontejneru + případný `margin-bottom` dlaždice
/// (v gridu se margin a gap SČÍTAJÍ, nekolabují). Harmonogram: .schedule-group-list gap
/// + .schedule-card (bez marginu). Záznamy: .card-list gap + .record-card margin-bottom.
/// Test porovnává efektivní mezeru, aby zachytil i regresi přes margin (ne jen gap).
/// </summary>
public sealed class RecordsCardListGapTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return File.ReadAllText(
            Path.Combine(dir!.FullName, "PmTracker.Web", "wwwroot", "css", "site.css"));
    }

    /// <summary>Vrátí hodnotu px vlastnosti uvnitř bloku selektoru, nebo 0 když chybí.</summary>
    private static int PxProp(string css, string selector, string prop)
    {
        var block = Regex.Match(css, Regex.Escape(selector) + @"\s*\{([^}]*)\}");
        block.Success.Should().BeTrue($"selektor {selector} musí existovat v site.css");
        var m = Regex.Match(block.Groups[1].Value, prop + @":\s*(\d+)px");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    [Fact]
    public void RecordTileSpacing_matches_harmonogram_tile_spacing()
    {
        var css = Css();

        var recordsSpacing = PxProp(css, ".card-list", "gap")
            + PxProp(css, ".record-card", "margin-bottom");
        var harmonogramSpacing = PxProp(css, ".schedule-group-list", "gap")
            + PxProp(css, ".schedule-card", "margin-bottom");

        recordsSpacing.Should().Be(harmonogramSpacing,
            "mezera mezi dlaždicemi v záložce záznamy musí být stejná jako v harmonogramu (cílový stav)");
    }
}
