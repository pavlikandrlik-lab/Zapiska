using FluentAssertions;

namespace PmTracker.Tests.Unit.Common;

public sealed class SeedBaselineDocumentationTests
{
    [Fact]
    public void DocumentationStructure_ShouldUseTechnicalFolderWithoutLegacyPages()
    {
        var root = GetRepositoryRoot();
        var expectedTechnicalPages = new[]
        {
            "00-documentation-tree.md",
            "01-system-context.md",
            "02-architecture.md",
            "03-runtime-configuration.md",
            "04-installation-deployment-iis.md",
            "05-web-server-iis-config.md",
            "06-database-bootstrap-migrations.md",
            "07-security-authz.md",
            "08-operations-runbooks.md",
            "09-testing-quality.md",
            "10-troubleshooting-recovery.md"
        };

        foreach (var page in expectedTechnicalPages)
        {
            File.Exists(Path.Combine(root, "docs", "technical", page)).Should().BeTrue();
        }

        File.Exists(Path.Combine(root, "docs", "technical-guide.md")).Should().BeFalse();
        File.Exists(Path.Combine(root, "docs", "admin-guide.md")).Should().BeFalse();
        File.Exists(Path.Combine(root, "docs", "db-bootstrap.md")).Should().BeFalse();
    }

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
        script.Should().Contain("PROJ_MAN");
        script.Should().NotContain("VED_SUB");
        script.Should().NotContain("ANALYTIK");
        script.Should().NotContain("DEV");
    }

    [Fact]
    public void ProductionBaselineSeed_ShouldContainMinimalOperationalDefaults()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "PMTracker_insert_sql"));

        script.Should().Contain("N'U', N'Úkol'");
        script.Should().Contain("N'OPEN', N'Rozpracováno'");
        script.Should().Contain("N'DONE', N'Hotovo'");
        script.Should().Contain("N'DRAFT', N'Příprava'");
        script.Should().Contain("N'CLOSED', N'Uzavřeno'");
        script.Should().Contain("N'PRESENT', N'Přítomen'");
        script.Should().Contain("N'ONLINE', N'Online'");
        script.Should().Contain("N'EXCUSED', N'Omluven'");
        script.Should().Contain("N'ABSENT', N'Nepřítomen'");
        script.Should().Contain("N'mp', N'MiniProjekt'");
        script.Should().Contain("N'A', N'Akce'");
        script.Should().Contain("N'P', N'Projekt'");
        script.Should().Contain("N'RU', N'Hlavní úkol rozvoje'");
        script.Should().Contain("N'PMP', N'Požadavek metodické podpory'");
        script.Should().Contain("N'PNF', N'Požadavek nové funkcionality'");
        script.Should().Contain("N'NES', N'Nesrovnalost'");
        script.Should().Contain("N'HS01_DURATION'");
        script.Should().Contain("N'HS11_DELAY'");
    }

    [Fact]
    public void DatabaseBootstrapDocumentation_ShouldListAllPlannedBusinessTables()
    {
        var document = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "docs", "technical", "06-database-bootstrap-migrations.md"));

        document.Should().Contain("dbo.ciselnik_kategorii_zaznamu");
        document.Should().Contain("dbo.ciselnik_stavu_ukolu");
        document.Should().Contain("dbo.ciselnik_typu_ukolu");
        document.Should().Contain("dbo.subsystemy");
        document.Should().Contain("dbo.ciselnik_stavu_jednani");
        document.Should().Contain("dbo.ciselnik_stavu_ucasti");
        document.Should().Contain("dbo.harmonogram_sablony");
        document.Should().Contain("dbo.ciselnik_harmonogram_typu");
        document.Should().Contain("dbo.ciselnik_typu_externich_odkazu");
        document.Should().Contain("dbo.vyzvy");
        document.Should().Contain("dbo.vyzva_historie_stavu");
        document.Should().Contain("dbo.ciselnik_organizace");
        document.Should().Contain("dbo.ciselnik_organizacni_celky");
    }

    [Fact]
    public void DatabaseBootstrapDocumentation_ShouldDescribeHybridSeedModel()
    {
        var document = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "docs", "technical", "06-database-bootstrap-migrations.md"));

        document.Should().Contain("PMTracker_insert_sql");
        document.Should().Contain("db_seed_dev_admin.sql");
        document.Should().Contain("Produkční baseline", "dokument musí popisovat produkční bootstrap režim");
        document.Should().Contain("První superadmin", "dokument musí obsahovat onboarding první provozní osoby");
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
