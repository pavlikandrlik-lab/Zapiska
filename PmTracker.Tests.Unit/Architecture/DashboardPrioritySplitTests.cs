using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3A Task 1: DashboardPriorityServices.cs (892 LOC, 10 types)
/// rozdělen do 5 souborů podle layer/lifecycle.
/// </summary>
public sealed class DashboardPrioritySplitTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        return directory.FullName;
    }

    private static string ResolvePath(string relative) =>
        Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void OriginalGodFile_ShouldBeDeleted()
    {
        File.Exists(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs"))
            .Should().BeFalse("Fáze 3A Task 1 smazala god-file, obsah rozdělen do 5 souborů");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs")]
    public void NewFile_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3A Task 1");
        File.ReadAllText(full).Length.Should().BeGreaterThan(500, "každý split file má smysluplný obsah");
    }

    [Fact]
    public void ModelsFile_ShouldContainPublicInterfaces()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs"));
        content.Should().Contain("public interface IPriorityScoringService");
        content.Should().Contain("public interface IPriorityMatrixRebuildService");
        content.Should().Contain("public interface IDashboardPriorityQuery");
    }

    [Fact]
    public void ScoringServiceFile_ShouldContainOnlyScoringClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs"));
        content.Should().Contain("class PriorityScoringService");
        content.Should().NotContain("class PriorityMatrixRebuildService", "scoring service je izolovaný");
        content.Should().NotContain("class DashboardPriorityQuery", "scoring service je izolovaný");
    }

    [Fact]
    public void HostedServicesFile_ShouldContainThreeHostedServices()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs"));
        content.Should().Contain("class PriorityMatrixBootstrapHostedService");
        content.Should().Contain("class PriorityMatrixNightlyRebuildHostedService");
        content.Should().Contain("class PriorityMatrixQueuedRebuildHostedService");
    }

    [Fact]
    public void QueryFile_ShouldContainOnlyQueryClass()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs"));
        content.Should().Contain("class DashboardPriorityQuery", "query třída musí být v query souboru");
        content.Should().NotContain("class PriorityMatrixRebuildService", "query soubor je izolovaný od rebuild logiky");
        content.Should().NotContain("class PriorityScoringService", "query soubor je izolovaný od scoring logiky");
    }

    [Fact]
    public void RebuildServiceFile_ShouldContainRebuildTypes()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs"));
        content.Should().Contain("class PriorityMatrixRebuildService");
        content.Should().Contain("class PriorityMatrixRebuildQueue");
        content.Should().Contain("interface IPriorityMatrixRebuildQueue");
        content.Should().NotContain("class PriorityMatrixBootstrapHostedService", "hosted services jsou v samostatném souboru");
        content.Should().NotContain("class DashboardPriorityQuery", "query je v samostatném souboru");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs")]
    public void SplitFile_ShouldUseDashboardNamespace(string relativePath)
    {
        var content = File.ReadAllText(ResolvePath(relativePath));
        content.Should().Contain(
            "namespace PmTracker.Web.Services.Dashboard;",
            $"{relativePath} musí být ve správném namespace (file-scoped, konzistentní se stylem složky)");
    }

    [Fact]
    public void ModelsFile_ShouldNotImportEntityFramework()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs"));
        content.Should().NotContain(
            "using Microsoft.EntityFrameworkCore",
            "Models soubor nesmí importovat EF — musí být čistý leaf bez dependency na persistence vrstvy");
    }

    [Fact]
    public void ScoringServiceFile_ShouldNotImportEntityFramework()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs"));
        content.Should().NotContain(
            "using Microsoft.EntityFrameworkCore",
            "Scoring je pure compute — nesmí mít EF dependency");
    }
}
