using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Harmonogram;

public sealed class FakturaceCleanupTests
{
    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("test must be executed from within repository tree");
        return dir!.FullName;
    }

    [Fact]
    public void HarmonogramService_DefaultHarmonogramKroky_ShouldNotContainFakturace()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(), "PmTracker.Web", "Services", "Data", "HarmonogramService.cs"));
        source.Should().NotContain("HS11_");
        source.Should().NotContain("fakturace");
    }

    [Fact]
    public void HarmonogramCatalogService_ShouldNotContainFakturace()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(), "PmTracker.Web", "Services", "Data", "HarmonogramCatalogService.cs"));
        source.Should().NotContain("HS11_");
        source.Should().NotContain("fakturace");
    }

    [Fact]
    public void DevSeedScript_ShouldNotContainFakturaceRows()
    {
        var seed = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db_seed_dev_admin.sql"));
        seed.Should().NotContain("HS11_DURATION");
        seed.Should().NotContain("HS11_DELAY");
        seed.Should().NotContain("fakturace");
    }

    [Fact]
    public void CleanupUpgradeScript_ShouldExistAndDeleteFakturaceRows()
    {
        var path = Path.Combine(GetRepositoryRoot(), "db_upgrade_1_3_3_fakturace_cleanup.sql");
        File.Exists(path).Should().BeTrue("upgrade script must exist");
        var script = File.ReadAllText(path);
        script.Should().Contain("DELETE FROM dbo.zaznam_harmonogram_hodnoty");
        script.Should().Contain("DELETE FROM dbo.ciselnik_harmonogram_typu");
        script.Should().Contain("HS11");
    }
}
