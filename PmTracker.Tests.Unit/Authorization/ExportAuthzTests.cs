using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ExportAuthzTests
{
    [Fact]
    public void ExportController_Pdf_ShouldUsePerEntityPolicyAttribute()
    {
        // Per-action redesign 2026-04-23: export.pdf → per-entita (projekt / jednani / ukol).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:export.pdf.projekt\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:export.pdf.jednani\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:export.pdf.ukol\")]");
    }

    [Fact]
    public void ExportController_Word_ShouldUsePerEntityPolicyAttribute()
    {
        // Per-action redesign 2026-04-23: export.word → per-entita (projekt / jednani / ukol).
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("[Authorize(Policy = \"permission:export.word.projekt\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:export.word.jednani\")]");
        code.Should().Contain("[Authorize(Policy = \"permission:export.word.ukol\")]");
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
