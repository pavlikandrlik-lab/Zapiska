using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Profile;

/// <summary>
/// Sub-projekt 1 (2026-07-05): osobní předvolby jako spravovatelný seznam po jednotlivých položkách.
/// Spec: docs/superpowers/specs/2026-07-05-personal-preferences-management-design.md.
/// Source-assertion invarianty (klientský registr + render + Profil markup).
/// </summary>
public sealed class PersonalPreferencesTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    [Fact]
    public void ProjectFilterJs_ExposesPerItemApi_AndDropsBulkClear()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js");
        src.Should().Contain("export function listSavedProjectFilterPreferences");
        src.Should().Contain("export function removeProjectFilterPreference");
        src.Should().NotContain("clearProjectFilterPreferenceStorage",
            "hromadné mazání nahrazeno per-item (Sub-projekt 1, 2026-07-05).");
        src.Should().Contain(".label", "saveProjectFilterDefaults ukládá companion label klíč.");
    }

    [Fact]
    public void FilterShell_RendersProjectZkratka()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml");
        src.Should().Contain("data-project-zkratka=\"@Model.Zkratka\"");
    }

    [Fact]
    public void Registry_DeclaresPrintAndProjectFilterDescriptors()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/registry.js");
        src.Should().Contain("id: \"printFormat\"");
        src.Should().Contain("id: \"projectFilters\"");
        src.Should().Contain("export const preferenceRegistry");
    }

    [Fact]
    public void Render_InitAndDelegatesRemove()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/preferences/render.js");
        src.Should().Contain("export function initPersonalPreferences");
        src.Should().Contain("data-preference-remove");
        src.Should().Contain("data-preferences-list");
    }

    [Fact]
    public void Bootstrap_WiresPreferences_AndDropsBulkResetBranches()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js");
        src.Should().Contain("initPersonalPreferences");
        src.Should().NotContain("data-project-filter-preferences-reset");
        src.Should().NotContain("data-print-preference-reset");
        src.Should().NotContain("clearProjectFilterPreferenceStorage");
    }

    [Fact]
    public void Profil_RendersPreferencesList_AndDropsOldBulkCards()
    {
        var src = Read("PmTracker.Web/Views/Profil/Index.cshtml");
        src.Should().Contain("data-preferences-list");
        src.Should().Contain("data-preferences-empty");
        src.Should().NotContain("data-project-filter-preferences-reset");
        src.Should().NotContain("data-print-preference-reset");
    }
}
