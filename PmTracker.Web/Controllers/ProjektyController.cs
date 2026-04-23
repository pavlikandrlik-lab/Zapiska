using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;
using System.Globalization;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed partial class ProjektyController : BaseController
{
    private const string RecordsTab = "zaznamy";
    private const string ScheduleTab = "harmonogram";
    private const string MeetingsTab = "jednani";
    private const string TeamTab = "tym";
    private const string ProposalsTab = "navrhy";
    private const string TeamRefreshScope = "projekty-detail-tym";
    private const string ProposalsRefreshScope = "projekty-detail-navrhy";

    private readonly IProjectService _projectService;
    private readonly IMeetingService _meetingService;
    private readonly IRecordProposalService _recordProposalService;
    private readonly IProjectDashboardService _projectDashboardService;

    public ProjektyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProjectService projectService,
        IMeetingService meetingService,
        IRecordProposalService recordProposalService,
        IProjectDashboardService projectDashboardService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _projectService = projectService;
        _meetingService = meetingService;
        _recordProposalService = recordProposalService;
        _projectDashboardService = projectDashboardService;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var projekty = (await _projectService.BuildProjektyListAsync(ct))
            .Where(project => CurrentUserContext.CanAccessProject(project.Id))
            .ToList();
        var projectStatusOptions = await BuildProjectStatusOptionsAsync(ct);

        var model = new ProjektyIndexViewModel
        {
            PageTitle = "Projekty",
            IsAdmin = CurrentUserContext.IsSuperAdmin,
            CanCreate = CurrentUserContext.HasPermission(PermissionKeys.ProjectsCreate),
            StavyProjektu = projectStatusOptions,
            Projekty = projekty
                .Select(p => new ProjektListItemViewModel
                {
                    Id = p.Id,
                    Zkratka = p.Zkratka,
                    Nazev = p.Nazev,
                    StavKod = p.StavKod,
                    Stav = p.Stav,
                    CanEdit = p.CanEdit && CurrentUserContext.HasPermission(PermissionKeys.ProjectsEdit, p.Id),
                    CanDelete = p.CanDelete && CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete, p.Id)
                })
                .ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> Detail(int id, string? tab, int? recordId, bool openComments = false, CancellationToken ct = default)
    {
        if (!await _projectService.ProjektExistsAsync(id, ct))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var requestedTab = NormalizeProjectTab(tab);
        var model = await _projectService.BuildProjektDetailAsync(id, ct);
        model.ActiveTab = requestedTab;
        model.TargetRecordId = requestedTab == RecordsTab ? recordId : null;
        model.TargetRecordOpenComments = requestedTab == RecordsTab && openComments;
        await PrepareProjectDetailPresentationAsync(model, ct);
        await PrepareActiveProjectTabAsync(model, requestedTab, ct);
        return View(model);
    }

    private Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CancellationToken ct = default)
        => _projectService.BuildProjectStatusOptionsAsync(CurrentUserContext, ct);

    private async Task PrepareProjectDetailPresentationAsync(ProjektDetailViewModel model, CancellationToken ct)
    {
        AttachCurrentUser(model);

        var projectId = model.Projekt.Id;
        var canManageRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        // F4 redesign 2026-04-23: records.schedule.add nahrazen proposals.schedule.create
        // — uživatel bez schedule.edit musí přes návrhový workflow.
        var canManageSchedules = canManageRecords
            || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projectId)
            || CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projectId);

        model.CanCreateMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, projectId);
        model.CanEditMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projectId);
        // F4 redesign 2026-04-23: team.manage rozdělen na per-action klíče; UI-gate pro tab
        // managementu = má uživatel alespoň jeden z team.member.add / role.assign / subsystem.create.
        model.CanManageTeam = CurrentUserContext.HasPermission(PermissionKeys.TeamMemberAdd, projectId)
            || CurrentUserContext.HasPermission(PermissionKeys.TeamRoleAssign, projectId)
            || CurrentUserContext.HasPermission(PermissionKeys.TeamSubsystemCreate, projectId);
        model.CanManageRecords = canManageRecords;
        model.CanManageSchedules = canManageSchedules;
        model.CanViewProposals = await _recordProposalService.CanViewProposalTabAsync(projectId, CurrentUserContext, ct);
        // Per-action redesign 2026-04-23: dashboard.view klíč řídí viditelnost tlačítka
        // (hardkódovaný whitelist rolí ProjectDashboardAuthorizationPolicy smazán).
        model.CanViewDashboard = CurrentUserContext.HasPermission(PermissionKeys.DashboardView, projectId);
        model.PageTitle = model.Projekt.Nazev;
        model.BackUrl = Url.Action("Index", "Projekty") ?? "/Projekty";
        model.BackLabel = "Zpět na přehled";
        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CreateRecordEditorUrl = Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.ReturnToProjectUrl = Url.Action("Detail", "Projekty", new { id = projectId, tab = "jednani" }) ?? $"/Projekty/Detail/{projectId}?tab=jednani";
        model.ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = projectId, autoPrint = true }) ?? $"/Export/Projekt/{projectId}/Tisk?autoPrint=true";
        model.ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = projectId }) ?? $"/Export/Projekt/{projectId}/Word";
        model.HarmonogramTab.LoadUrl = Url.Action(nameof(HarmonogramTabPartial), new { id = projectId }) ?? $"/Projekty/HarmonogramTabPartial/{projectId}";
        model.JednaniTab.LoadUrl = Url.Action(nameof(JednaniTabPartial), new { id = projectId }) ?? $"/Projekty/JednaniTabPartial/{projectId}";
        model.TymTab.LoadUrl = Url.Action(nameof(TymTabPartial), new { id = projectId }) ?? $"/Projekty/TymTabPartial/{projectId}";
        model.NavrhyTab.LoadUrl = Url.Action(nameof(NavrhyTabPartial), new { id = projectId }) ?? $"/Projekty/NavrhyTabPartial/{projectId}";

        await PrepareProjectRecordsTabPresentationAsync(model.ZaznamyTab, ct);
    }

    private async Task PrepareActiveProjectTabAsync(ProjektDetailViewModel model, string requestedTab, CancellationToken ct)
    {
        if (string.Equals(requestedTab, ScheduleTab, StringComparison.OrdinalIgnoreCase))
        {
            var scheduleTab = await _projectService.BuildProjectScheduleTabAsync(model.Projekt.Id, ct);
            PrepareProjectScheduleTabPresentation(scheduleTab);
            model.LoadedHarmonogramTab = scheduleTab;
            return;
        }

        if (string.Equals(requestedTab, MeetingsTab, StringComparison.OrdinalIgnoreCase))
        {
            var meetingsTab = await _projectService.BuildProjectMeetingsTabAsync(model.Projekt.Id, ct);
            PrepareProjectMeetingsTabPresentation(meetingsTab);
            model.LoadedJednaniTab = meetingsTab;
            return;
        }

        if (string.Equals(requestedTab, TeamTab, StringComparison.OrdinalIgnoreCase))
        {
            var teamTab = await _projectService.BuildProjectTeamTabAsync(model.Projekt.Id, ct);
            PrepareProjectTeamTabPresentation(teamTab);
            model.LoadedTymTab = teamTab;
            return;
        }

        if (string.Equals(requestedTab, ProposalsTab, StringComparison.OrdinalIgnoreCase)
            && await _recordProposalService.CanViewProposalTabAsync(model.Projekt.Id, CurrentUserContext, ct))
        {
            var proposalsTab = await _recordProposalService.BuildProjectProposalsTabAsync(model.Projekt.Id, CurrentUserContext, ct);
            PrepareProjectProposalsTabPresentation(proposalsTab);
            model.LoadedNavrhyTab = proposalsTab;
        }
    }

    private static string NormalizeProjectTab(string? tab)
    {
        if (string.Equals(tab, "gant", StringComparison.OrdinalIgnoreCase))
        {
            return ScheduleTab;
        }

        if (string.Equals(tab, ScheduleTab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tab, MeetingsTab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tab, TeamTab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tab, ProposalsTab, StringComparison.OrdinalIgnoreCase))
        {
            return tab!.ToLowerInvariant();
        }

        return RecordsTab;
    }

    private async Task PrepareProjectRecordsTabPresentationAsync(ProjektZaznamyTabViewModel model, CancellationToken ct)
    {
        var projectId = model.ProjektId;
        var canManageRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        // F4 redesign 2026-04-23: records.comment.subsystemlead nahrazen proposals.record.create
        // — samostatný per-action klíč pro tvorbu návrhů na záznam (nezávisle na komentářové doméně).
        var canCreateRecordProposal = !canManageRecords
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsRecordCreate, projectId);

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanManageRecords = canManageRecords;
        model.CanCreateRecordProposal = canCreateRecordProposal;
        model.CreateRecordEditorUrl = Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.CreateRecordProposalUrl = Url.Action("CreateRecordProposal", "Navrhy", new { projektId = projectId }) ?? $"/Navrhy/CreateRecordProposal?projektId={projectId}";
        model.RefreshUrl = Url.Action(nameof(RecordsTabPartial), new { id = projectId }) ?? $"/Projekty/RecordsTabPartial/{projectId}";
        model.MeetingCommentStatesUrl = Url.Action(nameof(RecordMeetingCommentStates), new { id = projectId }) ?? $"/Projekty/RecordMeetingCommentStates/{projectId}";
        model.ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = projectId, autoPrint = true }) ?? $"/Export/Projekt/{projectId}/Tisk?autoPrint=true";
        model.ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = projectId }) ?? $"/Export/Projekt/{projectId}/Word";

        foreach (var record in model.Zaznamy)
        {
            PrepareRecordCardShellPresentation(record, projectId);
        }

        foreach (var group in model.SkupinyZaznamu)
        {
            foreach (var record in group.Zaznamy)
            {
                PrepareRecordCardShellPresentation(record, projectId);
            }
        }
    }

    private void PrepareRecordCardShellPresentation(ProjektZaznamCardShellViewModel record, int projectId)
    {
        var summary = record.Summary;
        summary.CanEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        summary.CanEditSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projectId);
        // F4 redesign 2026-04-23: CanAddSchedule VM flag + records.schedule.add klíč smazán;
        // schedule-only editace probíhá výhradně přes návrh (ProposalsScheduleCreate).
        var canCreateScheduleProposal = summary.JeUkol
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projectId);
        summary.CanManageSchedule = summary.CanEditSchedule || canCreateScheduleProposal;
        // F4 redesign: UI flag pro „přidat komentář za vedoucího subsystému" = meetings.notes.subsystemlead
        // (per-action klíč pro zápis v jednání).
        summary.CanCommentAsSubsystemLeader = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, projectId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        summary.CanAddComment = summary.CanEditRecord
            || CurrentUserContext.HasPermission(PermissionKeys.CommentsAdd, projectId)
            || summary.CanCommentAsSubsystemLeader;
        summary.CanCreateScheduleProposal = canCreateScheduleProposal;
        summary.EditButtonLabel = summary.CanEditRecord ? "Upravit" : "GANTT";
        summary.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        record.DetailUrl = Url.Action("RecordDetailPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordDetailPartial?projektId={projectId}&zaznamId={summary.Id}";
        record.CommentsUrl = Url.Action("RecordCommentsPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={summary.Id}";
        summary.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={summary.Id}";
    }

    private void PrepareProjectScheduleTabPresentation(ProjektHarmonogramTabViewModel model)
    {
        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;

        // F4 redesign 2026-04-23: records.schedule.add → proposals.schedule.create.
        var canManageSchedules = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId)
            || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, model.ProjektId)
            || CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, model.ProjektId);

        foreach (var item in model.HarmonogramUkoly)
        {
            item.CanManageSchedule = canManageSchedules;
            item.ScheduleEditUrl = Url.Action("Edit", "Zaznamy", new { id = item.ZaznamId, projektId = model.ProjektId }) ?? $"/Zaznamy/Edit/{item.ZaznamId}";
            item.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy", new { projektId = model.ProjektId, zaznamId = item.ZaznamId }) ?? $"/Navrhy/CreateScheduleProposal?projektId={model.ProjektId}&zaznamId={item.ZaznamId}";
        }
    }

    private void PrepareProjectMeetingsTabPresentation(ProjektJednaniTabViewModel model)
    {
        model.CanCreateMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, model.ProjektId);
        model.CanEditMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, model.ProjektId);
        model.ReturnToProjectUrl = Url.Action(nameof(Detail), new { id = model.ProjektId, tab = "jednani" }) ?? $"/Projekty/Detail/{model.ProjektId}?tab=jednani";
        model.PreviewRok = MeetingYearGroupBuilder.ResolvePreviewYear(model.RocniSkupiny, GetLocalNow().Year);
    }

    private void PrepareProjectTeamTabPresentation(ProjektTymTabViewModel model)
    {
        // F4 redesign 2026-04-23: team.manage rozdělen; UI-gate = alespoň jeden z team write keys.
        model.CanManageTeam = CurrentUserContext.HasPermission(PermissionKeys.TeamMemberAdd, model.ProjektId)
            || CurrentUserContext.HasPermission(PermissionKeys.TeamRoleAssign, model.ProjektId)
            || CurrentUserContext.HasPermission(PermissionKeys.TeamSubsystemCreate, model.ProjektId);
    }

    private void PrepareProjectProposalsTabPresentation(ProjektNavrhyTabViewModel model)
    {
        model.CreateRecordProposalUrl = Url.Action("CreateRecordProposal", "Navrhy", new { projektId = model.ProjektId }) ?? $"/Navrhy/CreateRecordProposal?projektId={model.ProjektId}";

        foreach (var item in model.NavrhyZalozeni.Concat(model.NavrhyHarmonogramu))
        {
            item.DetailUrl = Url.Action("ProposalDetail", "Navrhy", new { projektId = model.ProjektId, proposalId = item.Id }) ?? $"/Navrhy/ProposalDetail?projektId={model.ProjektId}&proposalId={item.Id}";
            item.PrefillCreateFormUrl = Url.Action("PrefillCreateProposal", "Navrhy", new { projektId = model.ProjektId, proposalId = item.Id }) ?? $"/Navrhy/PrefillCreateProposal?projektId={model.ProjektId}&proposalId={item.Id}";
        }
    }
}
