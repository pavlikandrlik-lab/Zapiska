using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Controllers;

/// <summary>
/// H-1 IDOR: Ověřuje, že team-management akce v ProjektyController
/// provádějí per-project kontrolu oprávnění <c>team.manage</c> na správném
/// projektu z command body (ne jen globální policy check).
///
/// Kontext: <see cref="PermissionAuthorizationHandler"/> čte projektId
/// z RouteValues. Pro POST akce, kde projektId přichází z form body (command
/// parametr), handler nemůže provést per-project check a defaultuje na global.
/// Fix: per-project kontrola v body přes CurrentUserContext.HasPermission(...).
/// </summary>
public sealed class ProjektyControllerTeamAuthzTests
{
    [Fact]
    public async Task DeactivateProjectRole_WhenUserLacksTeamManageOnProjekt_ReturnsForbidPath()
    {
        // Arrange: user má team.manage pouze na projektu A; volá deactivate s projektId = B.
        const int projectA = 100;
        const int projectB = 200;
        var projectService = new RecordingProjectService();
        var controller = CreateController(
            projectService,
            userContext: BuildProjectUserContext(
                projectId: projectA,
                permissionKeys: new[] { PermissionKeys.TeamRoleAssign, PermissionKeys.TeamRoleDeactivate, PermissionKeys.TeamMemberAdd, PermissionKeys.TeamMemberRemove }));

        // Act
        var result = await controller.DeactivateProjectRole(
            new DeactivateProjectRoleCommand { ProjektRoleId = 1 },
            projektId: projectB);

        // Assert
        result.Should().BeAssignableTo<ForbidResult>(
            "projekt B není v PerProjectPermissions, per-project check musí selhat");
        projectService.DeactivateProjectRoleCallCount.Should().Be(0,
            "operation se nesmí zavolat když autorizace selhala");
    }

    [Fact]
    public async Task DeactivateProjectRole_WhenUserHasTeamManageOnProjekt_Succeeds()
    {
        // Arrange: user má team.manage na projektu A; volá deactivate s projektId = A.
        const int projectA = 100;
        var projectService = new RecordingProjectService();
        var controller = CreateController(
            projectService,
            userContext: BuildProjectUserContext(
                projectId: projectA,
                permissionKeys: new[] { PermissionKeys.TeamRoleAssign, PermissionKeys.TeamRoleDeactivate, PermissionKeys.TeamMemberAdd, PermissionKeys.TeamMemberRemove }));

        // Act
        var result = await controller.DeactivateProjectRole(
            new DeactivateProjectRoleCommand { ProjektRoleId = 1 },
            projektId: projectA);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>("úspěšný průběh redirectuje na team tab");
        projectService.DeactivateProjectRoleCallCount.Should().Be(1);
    }

    [Fact]
    public async Task AssignProjectRole_WhenUserLacksTeamManageOnProjekt_ReturnsForbidPath()
    {
        // Arrange: projektId je v command.ProjektId (form body), ne v route.
        const int projectA = 100;
        const int projectB = 200;
        var projectService = new RecordingProjectService();
        var controller = CreateController(
            projectService,
            userContext: BuildProjectUserContext(
                projectId: projectA,
                permissionKeys: new[] { PermissionKeys.TeamRoleAssign, PermissionKeys.TeamRoleDeactivate, PermissionKeys.TeamMemberAdd, PermissionKeys.TeamMemberRemove }));

        // Act: útok přes IDOR — uživatel má team.manage na A, ale pokusí se zasáhnout B.
        var result = await controller.AssignProjectRole(
            new AssignProjectRoleCommand
            {
                ProjektId = projectB,
                OsobaId = 5,
                RoleKod = "PROJ_MAN"
            });

        // Assert
        result.Should().BeAssignableTo<ForbidResult>();
        projectService.AssignProjectRoleCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AssignProjectRole_WhenUserHasTeamManageOnSameProjekt_Succeeds()
    {
        const int projectA = 100;
        var projectService = new RecordingProjectService();
        var controller = CreateController(
            projectService,
            userContext: BuildProjectUserContext(
                projectId: projectA,
                permissionKeys: new[] { PermissionKeys.TeamRoleAssign, PermissionKeys.TeamRoleDeactivate, PermissionKeys.TeamMemberAdd, PermissionKeys.TeamMemberRemove }));

        var result = await controller.AssignProjectRole(
            new AssignProjectRoleCommand
            {
                ProjektId = projectA,
                OsobaId = 5,
                RoleKod = "PROJ_MAN"
            });

        result.Should().BeOfType<RedirectToActionResult>();
        projectService.AssignProjectRoleCallCount.Should().Be(1);
    }

    private static ProjektyController CreateController(
        RecordingProjectService projectService,
        CurrentUserContextViewModel userContext)
    {
        var services = new ServiceCollection();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        var controller = new ProjektyController(
            userContextResolver: null!,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            projectService: projectService,
            meetingService: new StubMeetingService(),
            recordProposalService: new StubRecordProposalService(),
            projectDashboardService: new StubProjectDashboardService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        controller.Url = new StubUrlHelper();
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());
        SetCurrentUserContext(controller, userContext);
        return controller;
    }

    private static CurrentUserContextViewModel BuildProjectUserContext(int projectId, params string[] permissionKeys)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 2,
            Jmeno = "Project",
            Prijmeni = "Tester",
            DisplayName = "Project Tester",
            Email = "project@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [projectId],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
                {
                    {
                        projectId,
                        new HashSet<string>(permissionKeys, StringComparer.OrdinalIgnoreCase)
                    }
                },
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }

    private static void SetCurrentUserContext(ProjektyController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("BaseController musí mít backing field pro CurrentUserContext");
        field!.SetValue(controller, userContext);
    }

    private sealed class RecordingProjectService : IProjectService
    {
        public int DeactivateProjectRoleCallCount { get; private set; }
        public int AssignProjectRoleCallCount { get; private set; }

        public Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<ProjektListItemViewModel>> BuildProjektyListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProjektListItemViewModel>>([]);
        public Task<ProjektListItemViewModel?> GetProjectListItemAsync(int id, CancellationToken ct = default)
            => Task.FromResult<ProjektListItemViewModel?>(null);
        public Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ProjektZaznamyTabViewModel> BuildProjectRecordsTabAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> BuildRecordMeetingCommentStatesAsync(int projectId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ProjektHarmonogramTabViewModel> BuildProjectScheduleTabAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ProjektJednaniTabViewModel> BuildProjectMeetingsTabAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ProjektTymTabViewModel> BuildProjectTeamTabAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ProjectTeamModalOptionsViewModel> BuildProjectTeamModalOptionsAsync(int id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<PersonPickerEntryViewModel>> SearchProjectMemberCandidatesAsync(string query, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<ProjektZaznamCardShellViewModel?> BuildRecordCardShellAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ProjektZaznamCardShellViewModel?>(null);
        public Task<ZaznamCardDetailViewModel?> BuildRecordCardDetailAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ZaznamCardDetailViewModel?>(null);
        public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, CancellationToken ct = default)
            => Task.FromResult<ZaznamCommentsPanelViewModel?>(null);
        public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, int? limit, bool loadAll, CancellationToken ct = default)
            => Task.FromResult<ZaznamCommentsPanelViewModel?>(null);
        public Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LookupOptionViewModel>>([]);

        public Task SaveTeamMemberAsync(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveTeamMemberAsync(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> SaveProjectAsync(SaveProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.FromResult(0);
        public Task SoftDeleteProjectAsync(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;

        public Task AssignProjectRoleAsync(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        {
            AssignProjectRoleCallCount++;
            return Task.CompletedTask;
        }

        public Task DeactivateProjectRoleAsync(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        {
            DeactivateProjectRoleCallCount++;
            return Task.CompletedTask;
        }

        public Task AssignProjectSubsystemAsync(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateProjectSubsystemAsync(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReorderProjectSubsystemAsync(ReorderProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task AssignProjectSubsystemRoleAsync(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeactivateProjectSubsystemRoleAsync(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubMeetingService : IMeetingService
    {
        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(IReadOnlyCollection<int>? projectIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MeetingModalViewModel> BuildNewMeetingModalAsync(int projectId, DateTime localNow, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<MeetingModalViewModel?> BuildEditMeetingModalAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool?> IsMeetingEditableAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> SaveMeetingAsync(SaveMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteMeetingAsync(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingStatusAsync(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddMeetingParticipantAsync(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAttendanceBatchAsync(int meetingId, IEnumerable<(int OsobaId, string StavUcasti)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubRecordProposalService : IRecordProposalService
    {
        public Task<bool> CanViewProposalTabAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ProjektNavrhyTabViewModel> BuildProjectProposalsTabAsync(int projectId, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ZaznamEditViewModel> BuildCreateRecordProposalEditorAsync(int projectId, CurrentUserContextViewModel currentUser, int? meetingId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ZaznamEditViewModel> BuildScheduleProposalEditorAsync(int projectId, int recordId, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ZaznamEditViewModel> BuildProposalDetailAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ZaznamEditViewModel> BuildEditableRecordEditorFromProposalAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ZaznamEditViewModel> BuildPrefilledCreateRecordEditorFromProposalAsync(int projectId, int proposalId, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SubmitCreateRecordProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SubmitScheduleProposalAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int?> ApproveProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RejectProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RejectAndTakeOverCreateProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RejectAndEditProposalAsync(ProposalDecisionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubProjectDashboardService : IProjectDashboardService
    {
        public Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default) => throw new NotSupportedException();
        public ProjectDashboardNesPanelViewModel BuildNesPanel() => throw new NotSupportedException();
        public Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(int projektId, bool muzeEditovat, CancellationToken ct) => throw new NotSupportedException();
        // CanAccessDashboardAsync smazáno v redesignu 2026-04-23
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/stub";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/stub";
        public string? RouteUrl(UrlRouteContext routeContext) => "/stub";
    }
}
