using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Fáze B Task B4: SuperAdmin status určuje pouze AuthzSuperadmins tabulka
/// (propagovaná přes osoba.IsSuperAdmin v ResolvedPersonRow).
/// Hardcoded fallback "roleCodes obsahuje SUPERADMIN" je odstraněn.
/// </summary>
public sealed class SuperAdminSourceTests
{
    [Fact]
    public void UserContextResolver_ShouldNotContainHardcodedSuperadminFallback()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/UserContextResolver.cs"));

        code.Should().NotMatchRegex(
            @"roleCodes\.Any\s*\(\s*code\s*=>\s*string\.Equals\s*\(\s*code\s*,\s*""SUPERADMIN""",
            "hardcoded 'SUPERADMIN' fallback musí být odstraněn; zdrojem pravdy je jen AuthzSuperadmins → osoba.IsSuperAdmin");
    }

    [Fact]
    public void UserContextResolver_ShouldStillSetIsSuperAdminFromOsoba()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/UserContextResolver.cs"));

        code.Should().Contain("var isSuperAdmin = osoba.IsSuperAdmin;",
            "osoba.IsSuperAdmin (napojené na AuthzSuperadmins) musí zůstat jediným zdrojem");
    }

    [Fact]
    public void UserAuthorizationSnapshotBuilder_ShouldNotContainHardcodedSuperadminFallback()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Settings/UserAuthorizationSnapshotBuilder.cs"));

        // Nesmí obsahovat žádný fallback porovnávající role kód "SUPERADMIN" — ani string.Equals, ani Ci.Equals / StringComparer styl
        code.Should().NotMatchRegex(
            @"""SUPERADMIN""",
            "hardcoded SUPERADMIN string fallback musí být odstraněn i z UserAuthorizationSnapshotBuilder");
    }
}
