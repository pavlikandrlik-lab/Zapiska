using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Controllers;

public sealed partial class ZaznamyController
{
    [HttpGet]
    public async Task<IActionResult> RecordCardPartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var record = await _recordService.BuildRecordCardShellAsync(projektId, zaznamId, ct);
        if (record is null)
        {
            return NotFound();
        }

        PrepareRecordCardShellPresentation(record, projektId);
        return PartialView("~/Views/Projekty/_ZaznamPartial.cshtml", record);
    }

    [HttpGet]
    public async Task<IActionResult> RecordDetailPartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordCardDetailAsync(projektId, zaznamId, ct);
        if (model is null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Projekty/_ZaznamDetailPartial.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> RecordSchedulePartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordScheduleBlockAsync(projektId, zaznamId, ct);
        if (model is null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Projekty/_ZaznamSchedulePartial.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> RecordCommentsPartial(int projektId, int zaznamId, int? limit, bool loadAll = false, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var record = await _recordService.BuildRecordCardShellAsync(projektId, zaznamId, ct);
        if (record is null)
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordCommentsPanelAsync(projektId, zaznamId, limit, loadAll, ct);
        if (model is null)
        {
            return NotFound();
        }

        PrepareRecordCommentsPresentation(model, record.Summary);
        return PartialView("~/Views/Projekty/_ZaznamCommentsPartial.cshtml", model);
    }

    /// <summary>
    /// Okno tiketu jen pro čtení (2026-10-13, spec 2026-04-21 §5.2): obsah tiketu ze ServiceDesku
    /// a přiřazení vyjádření ke krokům, bez možnosti je měnit. Guard = přístup k projektu
    /// záznamu, stejně jako stránka záznamu — žádný klíč. projektId se nebere od klienta, ale
    /// odvozuje z vazby, takže podvržené id vrátí 404. Bez harvestu: GET nic nezapisuje.
    /// Builder přes [FromServices], aby se neměnil konstruktor controlleru.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> VyjadreniNahled(
        int externiOdkazId,
        int zaznamId,
        [FromServices] IVyjadreniModalViewModelBuilder builder,
        CancellationToken ct = default)
    {
        if (externiOdkazId <= 0 || zaznamId <= 0)
        {
            return BadRequest();
        }

        var projektId = await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                               join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                               where eo.Id == externiOdkazId && eo.ZaznamId == zaznamId
                               select (int?)z.ProjektId)
                              .FirstOrDefaultAsync(ct);
        if (projektId is null || !CurrentUserContext.CanAccessProject(projektId.Value))
        {
            return NotFound();
        }

        var vm = await builder.BuildAsync(externiOdkazId, zaznamId, canEdit: false, canAddAddon: false, ct);
        if (vm is null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Vyjadreni/_ChatModal.cshtml", vm);
    }

    private void PrepareRecordCardShellPresentation(ProjektZaznamCardShellViewModel record, int projektId)
    {
        var summary = record.Summary;
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId);
        var canEditSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projektId);
        // F4 redesign 2026-04-23: records.schedule.add → proposals.schedule.create;
        // records.comment.subsystemlead → meetings.notes.subsystemlead.
        var canCreateScheduleProposal = summary.JeUkol
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, projektId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        var canAddGeneralComment = CurrentUserContext.HasPermission(PermissionKeys.CommentsAdd, projektId);

        summary.CanEditRecord = canEditRecord;
        summary.CanEditSchedule = canEditSchedule;
        // Tužka (→ Edit action) se řídí edit-právy, NE návrhem — sjednoceno s celostránkovou
        // cestou (ProjektyController.PrepareRecordCardShellPresentation). Dřív tu prosakoval
        // canCreateScheduleProposal → proposal-only user viděl tužku na AJAX-refreshnuté kartě,
        // ale klik = 403. Obě cesty musí renderovat identickou kartu (toggle spec 2026-07-13).
        summary.CanManageSchedule = canEditSchedule;
        summary.CanCreateScheduleProposal = canCreateScheduleProposal;
        summary.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        summary.CanAddComment = canEditRecord || canAddGeneralComment || canCommentAsSubsystemLead;
        summary.EditButtonLabel = canEditRecord ? "Upravit" : "GANTT";
        summary.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        record.DetailUrl ??= Url.Action(nameof(RecordDetailPartial), new { projektId, zaznamId = summary.Id });
        record.CommentsUrl ??= Url.Action(nameof(RecordCommentsPartial), new { projektId, zaznamId = summary.Id });
        record.ScheduleUrl ??= Url.Action(nameof(RecordSchedulePartial), new { projektId, zaznamId = summary.Id });
        record.RecordPageUrl ??= Url.Action(nameof(Detail), new { id = summary.Id, returnUrl = ProjektDetailTabUrl(projektId, "zaznamy") });
        summary.ScheduleProposalUrl ??= Url.Action("CreateScheduleProposal", "Navrhy", new { projektId, zaznamId = summary.Id });
        summary.ScheduleProposalCheckUrl ??= Url.Action("ScheduleProposalAvailability", "Navrhy", new { projektId, zaznamId = summary.Id });
    }

    private void PrepareRecordCommentsPresentation(ZaznamCommentsPanelViewModel model, ZaznamCardSummaryViewModel summary)
    {
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId);
        var canAddGeneralComment = CurrentUserContext.HasPermission(PermissionKeys.CommentsAdd, model.ProjektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, model.ProjektId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        // F4 redesign 2026-04-23: „subsystem lead bez records.edit" = v comment formuláři vidí
        // pouze DRAFT jednání (smí psát zápis jen tam). comments.add tento specifický invariant
        // NEPRELEPÍ — generický komentář se váže na stejný subsystem-lead workflow a restrikce
        // draft-only chrání proti zápisu v uzavřených jednáních.
        if (!canEditRecord && canCommentAsSubsystemLead)
        {
            model.OtevrenaJednani = model.OtevrenaJednani
                .Where(item => item.IsDraft)
                .ToList();
        }

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanEditRecord = canEditRecord;
        model.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        model.CanAddComment = model.OtevrenaJednani.Count > 0
            && (canEditRecord || canAddGeneralComment || canCommentAsSubsystemLead);
    }
}
