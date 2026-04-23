using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Cílová matice projektových rolí (per-action redesign 2026-04-23).
/// Zdroj pravdy: docs/known-issues/authz-target-matrix.xlsx (sheet "Role × Permission (target)").
/// Testy asertují, že seed přesně odpovídá schválené matici.
///
/// Deprecated klíče (records.schedule.add, records.comment.subsystemlead, team.manage,
/// export.pdf, export.word) jsou zatím v seedu pro kompatibilitu během Fází 1–6
/// (smažou se v Fázi 7 + DB migraci). Testy je zohledňují přes <see cref="AllWithDeprecated"/>.
/// </summary>
public sealed class ProjectRolePermissionMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> AllWithDeprecated(
        IEnumerable<string> targetKeys,
        params string[] deprecated) =>
        targetKeys.Concat(deprecated).OrderBy(x => x, StringComparer.Ordinal);

    // -------------------------------------------------------------------------
    // VLASTNIK_PROJEKTU = ADM_PROJ = PROJ_MAN (59 cílových + deprecated)
    // -------------------------------------------------------------------------
    private static readonly string[] ProjectExecutiveTargetKeys =
    [
        "comments.add", "comments.delete.any", "comments.delete.own",
        "comments.edit.any", "comments.edit.own",
        "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
        "dashboard.view", "dashboard.vyzvy.view",
        "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
        "export.word.jednani", "export.word.projekt", "export.word.ukol",
        "externiodkazy.sync",
        "meetings.attendance.edit", "meetings.create", "meetings.delete", "meetings.edit",
        "meetings.notes.edit", "meetings.notes.subsystemlead", "meetings.participant.add",
        "meetings.status.change",
        "proposals.accept", "proposals.edit.any", "proposals.edit.own",
        "proposals.record.create", "proposals.reject", "proposals.schedule.create",
        "proposals.takeover",
        "records.assign.meeting", "records.create", "records.delete", "records.edit",
        "records.schedule.edit",
        "schedule.preview",
        "search.index",
        "team.candidates.search", "team.member.add", "team.member.remove",
        "team.role.assign", "team.role.deactivate",
        "team.subsystem.create", "team.subsystem.deactivate", "team.subsystem.reorder",
        "team.subsystem.role.assign", "team.subsystem.role.deactivate",
        "vyjadreni.modal.open", "vyjadreni.refresh", "vyjadreni.reharvest",
        "vyjadreni.vazba.create", "vyjadreni.vazba.delete",
        "vyzvy.create", "vyzvy.pnf.assign", "vyzvy.pnf.reassign",
        "vyzvy.state.change", "vyzvy.word.export"
    ];

    // Deprecated klíče, které VP/ADM_PROJ/PROJ_MAN mají zachovány pro kompat (viz seed).
    private static readonly string[] ProjectExecutiveDeprecatedKeys =
    [
        "records.schedule.add",
        "records.comment.subsystemlead",
        "team.manage",
        "export.pdf",
        "export.word"
    ];

    [Fact]
    public void VLASTNIK_PROJEKTU_ShouldHaveFullProjectExecutiveMatrix()
    {
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");
        perms.Should().BeEquivalentTo(
            AllWithDeprecated(ProjectExecutiveTargetKeys, ProjectExecutiveDeprecatedKeys));
    }

    [Fact]
    public void ADM_PROJ_ShouldHaveFullProjectExecutiveMatrix()
    {
        var perms = PermissionsFor("ADM_PROJ");
        perms.Should().BeEquivalentTo(
            AllWithDeprecated(ProjectExecutiveTargetKeys, ProjectExecutiveDeprecatedKeys));
    }

    [Fact]
    public void PROJ_MAN_ShouldMatchAdmProj()
    {
        PermissionsFor("PROJ_MAN").Should().BeEquivalentTo(PermissionsFor("ADM_PROJ"));
    }

    [Fact]
    public void HOST_ShouldHaveReadOnlyPermissions()
    {
        // HOST = read-only pozorovatel: dashboard + export + search. Deprecated export.pdf/word
        // ponechány během F1–F6.
        PermissionsFor("HOST").Should().BeEquivalentTo(new[]
        {
            "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
            "dashboard.view", "dashboard.vyzvy.view",
            "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
            "export.word.jednani", "export.word.projekt", "export.word.ukol",
            "search.index",
            // Deprecated (kompat F1–F6):
            "export.pdf", "export.word"
        });
    }

    [Fact]
    public void GEST_ShouldHaveCommentsAndReadOnly()
    {
        // GEST = komentátor + read-only. Deprecated: records.comment.subsystemlead + export.pdf/word.
        PermissionsFor("GEST").Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.delete.own", "comments.edit.own",
            "dashboard.nes.view", "dashboard.records.view", "dashboard.statistics.view",
            "dashboard.view", "dashboard.vyzvy.view",
            "export.pdf.jednani", "export.pdf.projekt", "export.pdf.ukol",
            "export.word.jednani", "export.word.projekt", "export.word.ukol",
            "search.index",
            // Deprecated (kompat F1–F6):
            "records.comment.subsystemlead", "export.pdf", "export.word"
        });
    }
}
