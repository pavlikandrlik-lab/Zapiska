using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>Spec 2026-10-14 — pojistky proti návratu druhého seznamu skutečnosti a mrtvého kódu.</summary>
public sealed class ScheduleActualSingleListTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void WebNepouzivaManualActualKroky_KromePrevoduStarehoJson()
    {
        var web = Path.Combine(Root(), "PmTracker.Web");
        var vyskyty = Directory.EnumerateFiles(web, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".cshtml") || f.EndsWith(".js"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}"))
            .Where(f => !f.EndsWith("RecordProposalPayloadJson.cs"))
            .Where(f => File.ReadAllText(f).Contains("ManualActualKrok"))
            .Select(f => Path.GetRelativePath(web, f))
            .ToList();

        vyskyty.Should().BeEmpty("druhý seznam s vlastním čítačem způsoboval díru v číslování (D1)");
    }

    /// <summary>Review Focus 2 — bootstrap.js z mezipaměti prohlížeče modul importuje.</summary>
    [Fact]
    public void ManualZdrojToggle_ZustavaPrazdnymModulem()
    {
        var path = Path.Combine(Root(), "PmTracker.Web/wwwroot/js/modules/harmonogram/manual-zdroj-toggle.js");
        File.Exists(path).Should().BeTrue("chybějící modul by shodil celý ESM graf u uživatelů se starým bootstrap.js");
        File.ReadAllText(path).Should().NotContain("addEventListener", "modul nesmí nic dělat (D5 mrtvý kód)");
        File.ReadAllText(Path.Combine(Root(), "PmTracker.Web/wwwroot/js/modules/bootstrap.js"))
            .Should().NotContain("manual-zdroj-toggle", "nová verze ho už neimportuje");
    }
}
