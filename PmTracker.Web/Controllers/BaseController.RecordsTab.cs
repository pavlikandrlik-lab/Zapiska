using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    private const string RecordsTabKey = "zaznamy";

    /// <summary>
    /// Příprava záložky Záznamy (oprávnění, odkazy karet). Sdílí ji detail projektu
    /// a detail jednání (plán 2026-10-21-detail-jednani-zaznamy). Odkazy na akce
    /// ProjektyController uvádějí controller výslovně — z JednaniController by jinak
    /// Url.Action doplnil ambientní controller volajícího.
    /// </summary>
    protected async Task PrepareRecordsTabPresentationAsync(
        ProjektZaznamyTabViewModel model,
        IRecordProposalService recordProposalService,
        int? kontextJednaniId,
        CancellationToken ct)
    {
        var projectId = model.ProjektId;
        var canManageRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        // „Nový návrh záznamu" gateujeme stejnou autoritativní branou jako panel Návrhů
        // (CreatableSubsystemIds > 0), aby se tlačítko v tab stripu zobrazilo přesně těm
        // uživatelům, co návrh opravdu mohou založit. Dřívější heuristika
        // (!canManageRecords && ProposalsRecordCreate) se s panelem rozcházela (user 2026-06-22).
        var canCreateRecordProposal = await recordProposalService.CanCreateRecordProposalAsync(projectId, CurrentUserContext, ct);

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanManageRecords = canManageRecords;
        model.CanCreateRecordProposal = canCreateRecordProposal;
        model.KontextJednaniId = kontextJednaniId;
        model.CreateRecordEditorUrl = kontextJednaniId.HasValue
            ? Url.Action("Create", "Zaznamy", new { projektId = projectId, jednaniId = kontextJednaniId, uiContext = "meeting" })
                ?? $"/Zaznamy/Create?projektId={projectId}&jednaniId={kontextJednaniId}&uiContext=meeting"
            : Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.CreateRecordProposalUrl = Url.Action("CreateRecordProposal", "Navrhy", new { projektId = projectId }) ?? $"/Navrhy/CreateRecordProposal?projektId={projectId}";
        model.RefreshUrl = Url.Action("RecordsTabPartial", "Projekty", new { id = projectId, kontextJednaniId })
            ?? $"/Projekty/RecordsTabPartial/{projectId}";
        model.MeetingCommentStatesUrl = Url.Action("RecordMeetingCommentStates", "Projekty", new { id = projectId }) ?? $"/Projekty/RecordMeetingCommentStates/{projectId}";
        model.ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = projectId, autoPrint = true }) ?? $"/Export/Projekt/{projectId}/Tisk?autoPrint=true";
        model.ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = projectId }) ?? $"/Export/Projekt/{projectId}/Word";
        if (kontextJednaniId.HasValue)
        {
            // Filtry v jednání: vlastní klíč v relaci, žádné uložené výchozí (R6).
            model.FilterShell.StorageId = $"jednani-{kontextJednaniId.Value}";
            model.FilterShell.AllowSaveDefaults = false;
        }

        foreach (var record in model.Zaznamy)
        {
            PrepareRecordsTabCardShell(record, projectId, kontextJednaniId);
        }

        foreach (var group in model.SkupinyZaznamu)
        {
            foreach (var record in group.Zaznamy)
            {
                PrepareRecordsTabCardShell(record, projectId, kontextJednaniId);
            }
        }
    }

    private void PrepareRecordsTabCardShell(ProjektZaznamCardShellViewModel record, int projectId, int? kontextJednaniId)
    {
        var summary = record.Summary;
        summary.CanEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        summary.CanEditSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projectId);
        // F4 redesign 2026-04-23: CanAddSchedule VM flag + records.schedule.add klíč smazán;
        // schedule-only editace probíhá výhradně přes návrh (ProposalsScheduleCreate).
        var canCreateScheduleProposal = summary.JeUkol
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projectId);
        // Tužka (→ Edit action) se řídí edit-právy, NE návrhem. Návrh má vlastní tlačítko
        // (CanCreateScheduleProposal → ScheduleProposalUrl). Dřív sem prosakoval canCreateScheduleProposal
        // → vedoucí subsystému viděl tužku na cizích záznamech, ale klik = 403.
        summary.CanManageSchedule = summary.CanEditSchedule;
        // F4 redesign: UI flag pro „přidat komentář za vedoucího subsystému" = meetings.notes.subsystemlead
        // (per-action klíč pro zápis v jednání).
        summary.CanCommentAsSubsystemLeader = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, projectId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        summary.CanCreateScheduleProposal = canCreateScheduleProposal;
        summary.EditButtonLabel = summary.CanEditRecord ? "Upravit" : "GANTT";
        summary.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        record.DetailUrl = Url.Action("RecordDetailPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordDetailPartial?projektId={projectId}&zaznamId={summary.Id}";
        record.CommentsUrl = Url.Action("RecordCommentsPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id, kontextJednaniId })
            ?? $"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={summary.Id}";
        record.ScheduleUrl = Url.Action("RecordSchedulePartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordSchedulePartial?projektId={projectId}&zaznamId={summary.Id}";
        var recordPageReturnUrl = kontextJednaniId.HasValue
            ? Url.Action("Detail", "Jednani", new { id = kontextJednaniId.Value }) ?? $"/Jednani/Detail/{kontextJednaniId.Value}"
            : ProjektDetailTabUrl(projectId, RecordsTabKey);
        record.RecordPageUrl = Url.Action("Detail", "Zaznamy", new { id = summary.Id, returnUrl = recordPageReturnUrl }) ?? $"/Zaznamy/Detail/{summary.Id}";
        // Tužka z detailu jednání otevře editor v kontextu jednání (předvolba „Změna stavu platí k“
        // + návrat do jednání), stejně jako „Nový záznam“ z jednání.
        record.EditRecordUrl = Url.Action("Edit", "Zaznamy", new
        {
            id = summary.Id,
            projektId = projectId,
            jednaniId = kontextJednaniId,
            uiContext = kontextJednaniId.HasValue ? "meeting" : null
        }) ?? $"/Zaznamy/Edit/{summary.Id}";
        summary.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={summary.Id}";
        summary.ScheduleProposalCheckUrl = Url.Action("ScheduleProposalAvailability", "Navrhy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Navrhy/ScheduleProposalAvailability?projektId={projectId}&zaznamId={summary.Id}";
    }
}
