using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class PermissionSeedConfigurationGlossaryTests
{
    [Fact]
    public void PermissionSeedConfiguration_ShouldContainGlossaryHeader()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"));

        code.Should().Contain("GLOSSARY",
            "soubor musí obsahovat glossary vysvětlující rozdíl mezi RoleScope, ScopeMode, PermissionScopeLevel");
        code.Should().Contain("RoleScope");
        code.Should().Contain("ScopeMode");
        code.Should().Contain("PermissionScopeLevel");
        code.Should().Contain("ObsazeniProjektu",
            "glossary musí zmínit, kde se projektové role přiřazují");
        code.Should().Contain("Include",
            "glossary musí zmínit všechny používané ScopeMode hodnoty včetně Include");
    }

    [Fact]
    public void PermissionSeedConfiguration_ShouldDocumentSeedOnlyArchitecture()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"));

        code.Should().Contain("seed",
            "soubor musí dokumentovat seed-only architekturu");
        code.Should().Contain("PR",
            "glossary musí zmínit, že role se mění přes PR review (ne přes UI)");
    }
}
