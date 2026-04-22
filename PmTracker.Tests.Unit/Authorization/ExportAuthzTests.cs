using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ExportAuthzTests
{
    [Fact]
    public void ExportController_Pdf_ShouldUseAuthorizePolicyAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:export.pdf\")]",
            "ExportController PDF actions musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }

    [Fact]
    public void ExportController_Word_ShouldUseAuthorizePolicyAttribute()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:export.word\")]",
            "ExportController Word actions musí mít [Authorize(Policy)] atribut místo body HasPermission checku");
    }

    [Fact]
    public void ExportController_ShouldNotBypassAuthOnIsSuperAdminAlone()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        // Ryzí "if (!IsSuperAdmin) return Forbid()" bez permission checku je security hole
        code.Should().NotMatchRegex(
            @"if\s*\(\s*!\s*CurrentUserContext\.IsSuperAdmin\s*\)\s*(\r?\n\s*)?{?\s*(\r?\n\s*)?return\s+Forbid",
            "nesmí existovat IsSuperAdmin-only bypass pro export — používej permission key check");
    }
}
