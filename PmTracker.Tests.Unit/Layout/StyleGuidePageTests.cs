using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class StyleGuidePageTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!;
    }

    [Fact]
    public void Controller_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Controllers", "StyleGuideController.cs");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void IndexView_Existuje_A_ObsahujePmKomponenty()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "StyleGuide", "Index.cshtml");
        File.Exists(path).Should().BeTrue();
        var content = File.ReadAllText(path);
        content.Should().Contain("<pm-button");
        content.Should().Contain("<pm-alert");
        content.Should().Contain("<pm-badge");
        content.Should().Contain("<pm-field");
        content.Should().Contain("<pm-icon");
    }

    [Fact]
    public void IndexView_MaSekciProKazdouKomponentu()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "StyleGuide", "Index.cshtml");
        var content = File.ReadAllText(path);
        content.Should().Contain("Tlačítka");
        content.Should().Contain("Alerty");
        content.Should().Contain("Badge");
        content.Should().Contain("Pole");
        content.Should().Contain("Ikony");
    }
}
