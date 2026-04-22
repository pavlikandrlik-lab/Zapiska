using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ExportAuthzTests
{
    [Fact]
    public void ExportController_Pdf_ShouldReferenceExportPdfPermissionKey()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("PermissionKeys.ExportPdf",
            "ExportController PDF action musí kontrolovat export.pdf permission key");
    }

    [Fact]
    public void ExportController_Word_ShouldReferenceExportWordPermissionKey()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ExportController.cs"));

        code.Should().Contain("PermissionKeys.ExportWord",
            "ExportController Word action musí kontrolovat export.word permission key");
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
