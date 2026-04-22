using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SearchReindexAuthzTests
{
    [Fact]
    public void SearchController_Reindex_ShouldReferenceSearchReindexPermissionKey()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/SearchController.cs"));

        code.Should().Contain("PermissionKeys.SearchReindex",
            "SearchController musí kontrolovat search.reindex permission key místo IsSuperAdmin");
    }

    [Fact]
    public void SearchController_ShouldNotUseIsSuperAdminAsOnlyGate()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/SearchController.cs"));

        // Bypass pattern "if (!IsSuperAdmin) return Forbid()" bez permission checku
        code.Should().NotMatchRegex(
            @"if\s*\(\s*!\s*CurrentUserContext\.IsSuperAdmin\s*\)",
            "IsSuperAdmin-only bypass must be replaced with HasPermission(SearchReindex)");
    }
}
