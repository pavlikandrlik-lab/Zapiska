using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// F6 redesign 2026-04-23: pokrytí per-action klíčů v seedu.
/// Invariant: všechny aktivní klíče mají alespoň jeden role-mapping
/// (bez něj by klíč byl mrtvý — autorizace by vždy vracela false).
/// APP_ADMIN a SUPERADMIN musí mít stejné per-action klíče (plný admin).
/// </summary>
public sealed class PerActionKeyCoverageTests
{
    [Fact]
    public void EveryActiveKey_ShouldHaveAtLeastOneRoleMapping()
    {
        var mappedKeys = PermissionSeedConfiguration.RoleMappings
            .Select(m => m.ActionKlic)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unmappedKeys = PermissionSeedConfiguration.Actions
            .Select(a => a.Klic)
            .Where(k => !mappedKeys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        unmappedKeys.Should().BeEmpty(
            "každý per-action klíč v PermissionSeedConfiguration.Actions musí mít " +
            "alespoň jednu RoleMapping — jinak je klíč mrtvý (žádná role jej nemá).");
    }

    [Fact]
    public void SuperAdmin_And_AppAdmin_ShouldHave_SameKeys()
    {
        // F1 redesign 2026-04-23: APP_ADMIN = SUPERADMIN (obě role mají celý katalog).
        var superKeys = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == "SUPERADMIN" && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var appKeys = PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == "APP_ADMIN" && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var superOnly = superKeys.Except(appKeys).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var appOnly = appKeys.Except(superKeys).OrderBy(x => x, StringComparer.Ordinal).ToList();

        superOnly.Should().BeEmpty(
            $"SUPERADMIN má klíče, které APP_ADMIN postrádá: {string.Join(", ", superOnly)}");
        appOnly.Should().BeEmpty(
            $"APP_ADMIN má klíče, které SUPERADMIN postrádá: {string.Join(", ", appOnly)}");
    }

    [Fact]
    public void AllDefinitions_Should_CorrespondToSeedActions_Bijection()
    {
        // Bijekce PermissionKeys.AllDefinitions ↔ PermissionSeedConfiguration.Actions
        // zaručuje, že C# konstanty a seed jsou ve shodě (F1 kontrakt). SeedSourceOfTruthTests
        // pokrývá inversi; zde držíme vlastní assertion pro čitelnost diagnostiky.
        var definitionKeys = PermissionKeys.AllDefinitions
            .Select(d => d.Key)
            .ToHashSet(StringComparer.Ordinal);

        var seedKeys = PermissionSeedConfiguration.Actions
            .Select(a => a.Klic)
            .ToHashSet(StringComparer.Ordinal);

        var missingInSeed = definitionKeys.Except(seedKeys).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var extraInSeed = seedKeys.Except(definitionKeys).OrderBy(x => x, StringComparer.Ordinal).ToList();

        missingInSeed.Should().BeEmpty(
            $"klíče v PermissionKeys.AllDefinitions chybí v seedu: {string.Join(", ", missingInSeed)}");
        extraInSeed.Should().BeEmpty(
            $"seed obsahuje klíče mimo PermissionKeys.AllDefinitions: {string.Join(", ", extraInSeed)}");
    }

    [Theory]
    [InlineData("records.edit")]
    [InlineData("meetings.edit")]
    [InlineData("comments.add")]
    [InlineData("proposals.accept")]
    [InlineData("vyzvy.create")]
    public void CoreProjectKeys_MustHave_ProjectScopeLevel(string key)
    {
        var action = PermissionSeedConfiguration.Actions.FirstOrDefault(a => a.Klic == key);
        action.Should().NotBeNull($"core klíč '{key}' musí být v seedu");
        action!.ScopeLevel.Should().Be(PermissionScopeLevel.Project,
            $"'{key}' je per-project action (nelze globálně granovat).");
    }

    [Theory]
    [InlineData("projects.create")]
    [InlineData("projects.read.all")]
    [InlineData("search.reindex")]
    [InlineData("people.ad.sync")]
    public void CoreGlobalKeys_MustHave_GlobalScopeLevel(string key)
    {
        var action = PermissionSeedConfiguration.Actions.FirstOrDefault(a => a.Klic == key);
        action.Should().NotBeNull($"core klíč '{key}' musí být v seedu");
        action!.ScopeLevel.Should().Be(PermissionScopeLevel.Global,
            $"'{key}' je globální action (projektový kontext není použit).");
    }
}
