using FluentAssertions;

namespace PmTracker.Tests.Unit.Common;

public sealed class SeedBaselineDocumentationTests
{
    [Fact]
    public void ProductionBaselineSeed_ShouldNotContainLocalAdminBootstrap()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "PMTracker_insert_sql"));

        script.Should().NotContain("Pavel Admin");
        script.Should().NotContain("INSERT INTO authz.superadmins");
        script.Should().NotContain("INSERT INTO authz.user_roles");
    }

    [Fact]
    public void ProductionBaselineSeed_ShouldContainOnlyFixedProjectRoleCodes()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "PMTracker_insert_sql"));

        script.Should().Contain("VLASTNIK_PROJEKTU");
        script.Should().Contain("HOST");
        script.Should().Contain("ADM_PROJ");
        script.Should().NotContain("VED_SUB");
        script.Should().NotContain("ANALYTIK");
        script.Should().NotContain("DEV");
    }

    [Fact]
    public void SeedReferenceDataMatrix_ShouldListAllPlannedBusinessTables()
    {
        var document = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "docs", "seed-reference-data-matrix.md"));

        document.Should().Contain("dbo.ciselnik_kategorii_zaznamu");
        document.Should().Contain("dbo.ciselnik_stavu_ukolu");
        document.Should().Contain("dbo.ciselnik_typu_ukolu");
        document.Should().Contain("dbo.subsystemy");
        document.Should().Contain("dbo.ciselnik_stavu_jednani");
        document.Should().Contain("dbo.ciselnik_stavu_ucasti");
        document.Should().Contain("dbo.harmonogram_sablony");
        document.Should().Contain("dbo.ciselnik_harmonogram_typu");
        document.Should().Contain("dbo.ciselnik_typu_externich_odkazu");
        document.Should().Contain("dbo.ciselnik_vyzvy");
        document.Should().Contain("dbo.ciselnik_organizace");
        document.Should().Contain("dbo.ciselnik_organizacni_celky");
    }

    [Fact]
    public void DbBootstrapDocumentation_ShouldDescribeHybridSeedModel()
    {
        var document = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "docs", "db-bootstrap.md"));

        document.Should().Contain("PMTracker_insert_sql");
        document.Should().Contain("db_seed_dev_admin.sql");
        document.Should().Contain("Produkční baseline");
        document.Should().Contain("nevytváří žádnou osobu");
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
