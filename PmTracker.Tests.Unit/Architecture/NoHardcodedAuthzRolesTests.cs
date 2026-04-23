using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// F6 redesign 2026-04-23: autorizační rozhodnutí musí vždy procházet
/// per-action klíči (PermissionKeys + IAuthorizationService / HasPermission),
/// NIKDY porovnáním proti konkrétním ProjectRoleCodes / SubsystemRoleCodes /
/// string literálům jako "PROJ_MAN".
///
/// Data-level použití (např. vlastník projektu se vybírá podle role VP)
/// je OK — hranice je v souboru s "Authorization" / "Policy" v názvu:
/// tam nesmí být žádné porovnání proti role kódu.
/// </summary>
public sealed class NoHardcodedAuthzRolesTests
{
    private static readonly string[] AuthzRoleCodes =
    {
        "\"SUPERADMIN\"",
        "\"APP_ADMIN\"",
        "\"VLASTNIK_PROJEKTU\"",
        "\"ADM_PROJ\"",
        "\"PROJ_MAN\"",
        "\"GEST\"",
        "\"VEDOUCI_SUBSYSTEMU\"",
        "\"ZASTUPCE_VEDOUCIHO_SUBSYSTEMU\"",
        "\"METODIK_SUBSYSTEMU\"",
        "\"READ_ALL\"",
        "\"HOST\"",
        "ProjectRoleCodes.ProjectAdmin",
        "ProjectRoleCodes.ProjectManager",
        "ProjectRoleCodes.Gestor",
    };

    [Fact]
    public void AuthorizationPolicyFiles_MustNotReferenceHardcodedRoleCodes()
    {
        var root = ResolvePath("PmTracker.Web/Services");
        var violations = new List<string>();

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(path);
            // Dovolené soubory: seedery, linker katalogu rolí, boot validator,
            // a samotný katalog konstant.
            if (fileName is "PermissionSeedConfiguration.cs"
                or "PermissionSeeder.cs"
                or "RoleCatalogLinker.cs"
                or "SqlStartupValidatorHostedService.cs")
            {
                continue;
            }

            // Kontrolujeme jen soubory, jejichž úkol je AUTORIZAČNÍ ROZHODNUTÍ.
            if (!(fileName.Contains("Authorization", StringComparison.OrdinalIgnoreCase)
                  || fileName.Contains("Policy", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var text = File.ReadAllText(path);
            foreach (var token in AuthzRoleCodes)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(ResolvePath("."), path)} obsahuje '{token}'");
                }
            }
        }

        violations.Should().BeEmpty(
            "Authorization/Policy soubory musí rozhodovat výhradně per-action klíči " +
            "(PermissionKeys.* + HasPermission / IAuthorizationService). Hardcoded role " +
            "porovnání zpětně vytváří IDOR riziko a zamezuje konfigurovatelnosti rolí v seedu.");
    }
}
