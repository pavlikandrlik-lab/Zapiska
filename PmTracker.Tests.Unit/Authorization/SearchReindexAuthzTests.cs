using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SearchReindexAuthzTests
{
    [Fact]
    public void SearchController_Reindex_ShouldUseAuthorizePolicyAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/SearchController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:search.reindex\")]",
            "SearchController musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }

    [Fact]
    public void SearchController_ShouldNotUseIsSuperAdminAsOnlyGate()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/SearchController.cs"));

        // Bypass pattern "if (!IsSuperAdmin) return Forbid()" bez permission checku
        code.Should().NotMatchRegex(
            @"if\s*\(\s*!\s*CurrentUserContext\.IsSuperAdmin\s*\)",
            "IsSuperAdmin-only bypass must be replaced with [Authorize(Policy)] attribute");
    }
}
