using FluentAssertions;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>
/// Řádky NES v prodlení sdílejí rozbalovací mechanismus Záznamů (data-dashboard-expandable-row).
/// Filtr kategorie Záznamů je proto musí míjet — jinak klik na chip nebo dlaždici „Úkoly po termínu"
/// skryl všechny řádky NES (review 2026-11-11, reprodukováno v prohlížeči: 6 → 0 řádků).
/// JS testy nemají DOM, pojistka hlídá rozsah ve zdrojáku.
/// </summary>
public sealed class ProjectDashboardJsScopeTests
{
    private static string Read(string rel)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, rel.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Fact]
    public void Filtr_kategorie_pracuje_jen_v_panelu_zaznamu()
    {
        var js = Read("PmTracker.Web/wwwroot/js/modules/projectDashboard.js");
        var start = js.IndexOf("function applyCategoryFilter", StringComparison.Ordinal);
        start.Should().BePositive();
        var telo = js[start..js.IndexOf("\n}\n", start, StringComparison.Ordinal)];

        js.Should().Contain("const RECORDS_PANEL = '[data-dashboard-panel=\"records\"]'");
        telo.Should().Contain("shell.querySelector(RECORDS_PANEL)");
        telo.Should().NotContain("shell.querySelectorAll(\"[data-dashboard-expandable-row]\")",
            "řádky NES nesou stejný atribut a filtr by je skryl");
        telo.Should().NotContain("shell.querySelectorAll(\"[data-dashboard-category-filter]\")");
    }
}
