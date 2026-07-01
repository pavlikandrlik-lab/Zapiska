using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProposalFilterViewModelTests
{
    [Fact]
    public void ProposalsTab_ShouldRenderFilterShellPartial()
    {
        var view = Load("PmTracker.Web/Views/Projekty/_ProjectProposalsTab.cshtml");
        view.Should().Contain("_ProposalFilterShell",
            "tab Návrhy musí obsahovat filter shell partial");
    }

    [Fact]
    public void ProposalFilterShell_ShouldExist()
    {
        var path = Path.Combine(LocateRoot(), "PmTracker.Web", "Views", "Projekty", "_ProposalFilterShell.cshtml");
        File.Exists(path).Should().BeTrue("filter shell partial musí existovat");
    }

    [Fact]
    public void ProposalFilterShell_ShouldHaveProposalsScope()
    {
        var view = Load("PmTracker.Web/Views/Projekty/_ProposalFilterShell.cshtml");
        view.Should().Contain("data-project-filter-scope=\"proposals\"",
            "filter shell musí mít scope proposals");
    }

    [Fact]
    public void ProposalFilterShell_ShouldHaveFiveFilterKeys()
    {
        var view = Load("PmTracker.Web/Views/Projekty/_ProposalFilterShell.cshtml");
        view.Should().Contain("data-filter-key=\"stavNavrhu\"");
        view.Should().Contain("data-filter-key=\"typNavrhu\"");
        view.Should().Contain("data-filter-key=\"subsystem\"");
        view.Should().Contain("data-filter-key=\"autor\"");
        view.Should().Contain("data-filter-key=\"rozhodl\"");
    }

    private static string Load(string relativePath)
    {
        var full = Path.Combine(LocateRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(full);
    }

    private static string LocateRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
