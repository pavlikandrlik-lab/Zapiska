using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Zábrana proti commitu skutečných credentials do appsettings.json (který je v gitu).
/// Secrets patří do User Secrets (~/.microsoft/usersecrets/<id>/secrets.json) — viz
/// docs/technical/14-local-dev-secrets.md.
///
/// Detekované patterny:
///   1. Password= s non-placeholder hodnotou (empty nebo typické placeholder hodnoty OK)
///   2. User Id= s non-placeholder hodnotou
///   3. Neprázdné ApiKey/Token pole v JSON
/// </summary>
public sealed class AppSettingsCredentialLeakGuardTests
{
    // appsettings.json je v .gitignore (obsahuje runtime credentials — lokálně a po publishi).
    // V gitu je jen appsettings.example.json jako schema template — ten je chráněný proti
    // leaku reálných credentials. Viz docs/technical/14-local-dev-secrets.md.
    private const string AppSettingsPath = "PmTracker.Web/appsettings.example.json";

    // Placeholder hodnoty, které jsou povolené (slouží jen jako schema template).
    private static readonly string[] AllowedPlaceholderUsers =
    {
        "sa",       // docker localhost dev default — nízké riziko, je to SQL Server default SA na lokálním docker SQL serveru
        "user",
        "USER",
        "placeholder",
        "PLACEHOLDER",
        "<READONLY_USER>", // SD integrace placeholder — viz docs/technical/13-servicedesk-connection-setup.md
    };

    private static readonly string[] AllowedPlaceholderPasswords =
    {
        "PmTracker!2026", // docker localhost dev default (well-known)
        "placeholder",
        "PLACEHOLDER",
        "YOUR_PASSWORD_HERE",
        "CHANGEME",
        "<PASSWORD>",      // SD integrace placeholder — viz docs/technical/13-servicedesk-connection-setup.md
    };

    [Fact]
    public void AppSettingsJson_ShouldNotContainRealPassword()
    {
        var path = ResolvePath(AppSettingsPath);
        var content = File.ReadAllText(path);

        // Match všechny Password=<value>; páry v ConnectionStrings
        var matches = Regex.Matches(content, @"Password\s*=\s*([^;""\\]+)", RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var value = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue; // Prázdné heslo je povolené (placeholder)
            }

            AllowedPlaceholderPasswords.Should().Contain(value,
                $"appsettings.json obsahuje non-placeholder heslo '{value}' — patří do User Secrets. " +
                $"Viz docs/technical/14-local-dev-secrets.md.");
        }
    }

    [Fact]
    public void AppSettingsJson_ShouldNotContainRealUserId()
    {
        var path = ResolvePath(AppSettingsPath);
        var content = File.ReadAllText(path);

        var matches = Regex.Matches(content, @"User\s+Id\s*=\s*([^;""\\]+)", RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var value = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            AllowedPlaceholderUsers.Should().Contain(value,
                $"appsettings.json obsahuje non-placeholder User Id '{value}' — patří do User Secrets.");
        }
    }

    [Fact]
    public void AppSettingsJson_ShouldNotContainNonEmptyApiKeys()
    {
        var path = ResolvePath(AppSettingsPath);
        var content = File.ReadAllText(path);

        // Matchuje "ApiKey": "xxx" / "Token": "xxx" / "Password": "xxx" (pole uvnitř JSON objektu).
        // Note: Password= v connection stringu je matchovaný předchozím testem.
        var matches = Regex.Matches(
            content,
            @"""(?:ApiKey|Token|Secret|Password)""\s*:\s*""([^""]+)""",
            RegexOptions.IgnoreCase);

        foreach (Match m in matches)
        {
            var value = m.Groups[1].Value.Trim();
            value.Should().BeEmpty(
                $"appsettings.json obsahuje non-empty secret field s hodnotou '{value}' — patří do User Secrets.");
        }
    }

    [Fact]
    public void AppSettingsJson_ConnectionStrings_ShouldStayAsSchemaTemplate()
    {
        var path = ResolvePath(AppSettingsPath);
        var content = File.ReadAllText(path);

        // Sanity — soubor musí existovat a mít ConnectionStrings sekci (jinak by celý test byl no-op).
        content.Should().Contain("\"ConnectionStrings\"",
            "appsettings.json musí mít ConnectionStrings sekci jako schema template — viz docs/technical/14-local-dev-secrets.md");
    }
}
