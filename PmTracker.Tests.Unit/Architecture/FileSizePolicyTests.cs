using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Policy testy — neselhávají na existující god-files (fáze 3 je rozbije),
/// ale ověří, že scripty pro kontrolu existují a ESLint config je na místě.
/// </summary>
public sealed class FileSizePolicyTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("repozitář s PmTracker.sln musí být dostupný");
        return dir!;
    }

    [Fact]
    public void CheckFileSizesShellScript_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "scripts", "check-file-sizes.sh");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void CheckFileSizesPowershellScript_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "scripts", "check-file-sizes.ps1");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void EslintConfig_ExistujeVeWwwroot_SMaxLines()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", ".eslintrc.json");
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("max-lines");
        content.Should().Contain("300");
    }

    [Fact]
    public void EslintConfig_MaLimitNaRadky()
    {
        var eslintPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", ".eslintrc.json");
        var eslintContent = File.ReadAllText(eslintPath);
        eslintContent.Should().Contain("\"max\": 300");
    }
}
