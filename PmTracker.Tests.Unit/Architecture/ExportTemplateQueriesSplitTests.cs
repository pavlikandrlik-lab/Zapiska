using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3A Task 2: ExportTemplateQueries.cs (1281 LOC, 8+ types)
/// rozdělen do 3 souborů: orchestration, projection builders, projection models.
/// </summary>
public sealed class ExportTemplateQueriesSplitTests
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
    public void Orchestration_ShouldBeSlim()
    {
        var file = ResolvePath("PmTracker.Web/Services/Export/ExportTemplateQueries.cs");
        File.Exists(file).Should().BeTrue();
        var loc = File.ReadAllLines(file).Length;
        loc.Should().BeLessThan(300, "ExportTemplateQueries je jen orchestration (GetXxx metody delegují do builders)");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/Export/ExportTemplateQueries.cs")]
    [InlineData("PmTracker.Web/Services/Export/ExportProjectionBuilders.cs")]
    [InlineData("PmTracker.Web/Services/Export/ExportProjectionModels.cs")]
    public void SplitFile_ShouldExistAndUseExportNamespace(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3A Task 2");
        var content = File.ReadAllText(full);
        content.Should().Contain("namespace PmTracker.Web.Services.Export;", "file-scoped namespace konzistentní s projektem");
    }

    [Fact]
    public void Orchestration_ShouldNotContainBuilderImplementations()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/ExportTemplateQueries.cs"));
        // All 6 builder classes moved out — orchestration keeps only interface + main class
        content.Should().Contain("class ExportTemplateQueries");
        content.Should().Contain("interface IExportTemplateQueries");
        content.Should().NotContain("class ExportAttendanceProjectionBuilder", "builders přesunuty");
        content.Should().NotContain("class ExportRoleProjectionBuilder", "builders přesunuty");
        content.Should().NotContain("class ExportCommentProjectionBuilder", "builders přesunuty");
        content.Should().NotContain("class ExportRecordProjectionBuilder", "builders přesunuty");
    }

    [Fact]
    public void Builders_ShouldContainProjectionBuilders()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/ExportProjectionBuilders.cs"));
        content.Should().Contain("class ExportAttendanceProjectionBuilder");
        content.Should().Contain("class ExportRoleProjectionBuilder");
        content.Should().Contain("class ExportCommentProjectionBuilder");
        content.Should().Contain("class ExportRecordProjectionBuilder");
        content.Should().Contain("class ExportRecordVisibilityEvaluator");
        content.Should().Contain("class ExportTemplateSummaryBuilder");
    }

    [Fact]
    public void Models_ShouldContainResultRecord()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/ExportProjectionModels.cs"));
        content.Should().Contain("ExportTemplateQueryResult");
        content.Should().Contain("ExportTemplateSummaryProjection");
    }

    [Fact]
    public void Builders_ShouldContainBuilderInterfaces()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/ExportProjectionBuilders.cs"));
        content.Should().Contain("interface IExportAttendanceProjectionBuilder");
        content.Should().Contain("interface IExportRoleProjectionBuilder");
        content.Should().Contain("interface IExportRecordProjectionBuilder");
        content.Should().Contain("interface IExportTemplateSummaryBuilder");
        content.Should().Contain("interface IExportCommentProjectionBuilder");
        content.Should().Contain("interface IExportRecordVisibilityEvaluator");
    }
}
