using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Bug 2026-07-04: vedoucí subsystému (má proposals.schedule.create, ne records.edit/schedule.edit)
/// viděl u cizích záznamů tužku i „Upravit" v harmonogramu; klik = 403. Afordance editoru se musí
/// řídit STEJNOU gate jako server (ZaznamyController.Edit) = records.edit || records.schedule.edit,
/// bez návrhového klíče. RecordEditorAffordancePolicy je jediný zdroj pravdy pro obě.
/// </summary>
public sealed class RecordEditorAffordancePolicyTests
{
    private const int Project = 100;

    private static CurrentUserContextViewModel User(int projectId, params string[] keys)
        => new()
        {
            OsobaId = 2,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "test@local",
            OrganizacniCelek = "Org",
            OrganizacniCelekKod = "ORG",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [projectId],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
                {
                    { projectId, new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase) }
                },
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };

    [Fact]
    public void CanOpenEditor_ProposalOnlyUser_ReturnsFalse()
    {
        var user = User(Project, PermissionKeys.ProposalsScheduleCreate);
        RecordEditorAffordancePolicy.CanOpenEditor(user, Project).Should().BeFalse(
            "návrhový klíč editor neotvírá — vedoucí subsystému tužku/Upravit vidět nemá");
    }

    [Theory]
    [InlineData(PermissionKeys.RecordsEdit)]
    [InlineData(PermissionKeys.RecordsScheduleEdit)]
    public void CanOpenEditor_WithEditKey_ReturnsTrue(string editKey)
    {
        RecordEditorAffordancePolicy.CanOpenEditor(User(Project, editKey), Project).Should().BeTrue();
    }

    [Fact]
    public void CanOpenEditor_NoRelevantKey_ReturnsFalse()
    {
        RecordEditorAffordancePolicy.CanOpenEditor(User(Project, PermissionKeys.CommentsAdd), Project)
            .Should().BeFalse();
    }

    [Fact]
    public void CanOpenEditor_EditKeyOnOtherProject_ReturnsFalse()
    {
        // Per-project: records.edit na projektu 100 neotvírá editor na projektu 999.
        var user = User(Project, PermissionKeys.RecordsEdit);
        RecordEditorAffordancePolicy.CanOpenEditor(user, 999).Should().BeFalse();
    }
}
