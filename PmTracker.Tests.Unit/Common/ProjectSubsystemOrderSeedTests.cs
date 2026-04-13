using FluentAssertions;

namespace PmTracker.Tests.Unit.Common;

public sealed class ProjectSubsystemOrderSeedTests
{
    [Fact]
    public void ProductionBaselineSeed_ShouldCreateProjectSubsystemOrderColumn_AndUniqueIndex()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "PMTracker_insert_sql"));

        script.Should().Contain("CREATE TABLE dbo.projekt_subsystemy");
        script.Should().Contain("poradi int NOT NULL");
        script.Should().Contain("UX_projekt_subsystemy_projekt_poradi_aktivni");
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Nepodařilo se najít kořen repozitáře.");
    }
}
