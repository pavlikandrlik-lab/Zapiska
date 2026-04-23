using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Cílová matice subsystémových rolí (per-action redesign 2026-04-23).
/// Zdroj pravdy: docs/known-issues/authz-target-matrix.xlsx.
/// F7: records.comment.subsystemlead smazán; subsystem lead nyní drží jen
/// meetings.notes.subsystemlead (pro zápis jednání) + proposals.record.create.
/// </summary>
public sealed class SubsystemRolePermissionMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    // VEDOUCI + ZASTUPCE mají identickou sadu (14 cílových klíčů).
    private static readonly string[] SubsystemLeadTargetKeys =
    [
        "comments.add", "comments.delete.own", "comments.edit.own",
        "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
        "dashboard.view", "dashboard.vyzvy.view",
        "meetings.notes.subsystemlead",
        "proposals.edit.own", "proposals.record.create", "proposals.schedule.create",
        "schedule.preview",
        "search.index"
    ];

    [Fact]
    public void VEDOUCI_SUBSYSTEMU_ShouldHaveSubsystemLeadAndCommentsPermissions()
    {
        PermissionsFor("VEDOUCI_SUBSYSTEMU")
            .Should().BeEquivalentTo(SubsystemLeadTargetKeys.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void ZASTUPCE_VEDOUCIHO_SUBSYSTEMU_ShouldMatchVedouci()
    {
        PermissionsFor("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU")
            .Should().BeEquivalentTo(PermissionsFor("VEDOUCI_SUBSYSTEMU"));
    }

    [Fact]
    public void METODIK_SUBSYSTEMU_ShouldHaveComments()
    {
        PermissionsFor("METODIK_SUBSYSTEMU").Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.delete.own", "comments.edit.own",
            "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
            "dashboard.view", "dashboard.vyzvy.view",
            "search.index"
        });
    }
}
