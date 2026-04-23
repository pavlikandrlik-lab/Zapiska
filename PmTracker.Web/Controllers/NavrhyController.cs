using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class NavrhyController : BaseController
{
    private const string PresentationModal = "modal";
    private const string PresentationPage = "page";
    private const string ProposalsTab = "navrhy";
    private const string ProposalsRefreshScope = "projekty-detail-navrhy";

    private readonly IRecordProposalService _recordProposalService;

    public NavrhyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IRecordProposalService recordProposalService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _recordProposalService = recordProposalService;
    }

    [HttpGet]
    [Authorize(Policy = "permission:proposals.record.create")]
    public async Task<IActionResult> CreateRecordProposal(int projektId, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordProposalService.BuildCreateRecordProposalEditorAsync(projektId, CurrentUserContext, ct: ct);
        PrepareProposalEditorModel(model, presentation, returnUrl);
        return View(GetEditorViewPath(model.Presentation), model);
    }

    [HttpGet]
    [Authorize(Policy = "permission:proposals.schedule.create")]
    public async Task<IActionResult> CreateScheduleProposal(int projektId, int zaznamId, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordProposalService.BuildScheduleProposalEditorAsync(projektId, zaznamId, CurrentUserContext, ct);
        PrepareProposalEditorModel(model, presentation, returnUrl);
        return View(GetEditorViewPath(model.Presentation), model);
    }

    [HttpGet]
    public async Task<IActionResult> ProposalDetail(int projektId, int proposalId, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        if (!await _recordProposalService.CanViewProposalTabAsync(projektId, CurrentUserContext, ct))
        {
            return Forbid();
        }

        var model = await _recordProposalService.BuildProposalDetailAsync(projektId, proposalId, CurrentUserContext, ct);
        PrepareProposalEditorModel(model, presentation, returnUrl);
        return View(GetEditorViewPath(model.Presentation), model);
    }

    // EditFromProposal (GET) SMAZÁN v redesignu 2026-04-23.
    // Důvod: bypass workflow — admin upravil záznam, ale návrh zůstal ve stavu „čeká
    // na rozhodnutí". Správný postup pro „souhlasím většinou, upravím zbytek":
    //   1. ApproveProposal (proposals.accept) — návrh explicitně schválen
    //   2. ZaznamyController.Edit (records.edit) — standardní editace záznamu
    // Pro „nesouhlasím, upravím jinak": RejectAndEditProposal (proposals.reject + records.edit).
    // Service metoda BuildEditableRecordEditorFromProposalAsync bude smazána ve Fázi 3.

    [HttpGet]
    [Authorize(Policy = "permission:proposals.edit.own")]
    public async Task<IActionResult> PrefillCreateProposal(int projektId, int proposalId, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        if (!await _recordProposalService.CanViewProposalTabAsync(projektId, CurrentUserContext, ct))
        {
            return Forbid();
        }

        var model = await _recordProposalService.BuildPrefilledCreateRecordEditorFromProposalAsync(projektId, proposalId, CurrentUserContext, ct);
        PrepareProposalEditorModel(model, presentation, returnUrl);
        return View(GetEditorViewPath(model.Presentation), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:proposals.record.create")]
    public Task<IActionResult> SubmitCreateProposal(SaveRecordCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.CanAccessProject(command.ProjektId),
            invalidAjaxMessage: "Návrh založení záznamu nelze odeslat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToProposalTab(command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToProposalTab(command.ProjektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildProposalSubmitAjaxSuccess(command, "Návrh založení záznamu byl odeslán.")),
            operation: () => _recordProposalService.SubmitCreateRecordProposalAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:proposals.schedule.create")]
    public Task<IActionResult> SubmitScheduleProposal(SaveRecordCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.CanAccessProject(command.ProjektId),
            invalidAjaxMessage: "Návrh změny termínu a harmonogramu nelze odeslat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToProposalTab(command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToProposalTab(command.ProjektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildProposalSubmitAjaxSuccess(command, "Návrh změny termínu a harmonogramu byl odeslán.")),
            operation: () => _recordProposalService.SubmitScheduleProposalAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:proposals.accept")]
    public Task<IActionResult> ApproveProposal(ProposalDecisionCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.CanAccessProject(command.ProjektId),
            invalidAjaxMessage: "Návrh nelze schválit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToProposalTab(command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToProposalTab(command.ProjektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildProposalTabAjaxSuccess(command.ProjektId, "Návrh byl schválen.")),
            operation: async () => await _recordProposalService.ApproveProposalAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:proposals.reject")]
    public Task<IActionResult> RejectProposal(ProposalDecisionCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.CanAccessProject(command.ProjektId),
            invalidAjaxMessage: "Návrh nelze zamítnout.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToProposalTab(command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToProposalTab(command.ProjektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildProposalTabAjaxSuccess(command.ProjektId, "Návrh byl zamítnut.")),
            operation: () => _recordProposalService.RejectProposalAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:proposals.takeover")]
    public Task<IActionResult> RejectAndTakeOverCreateProposal(ProposalDecisionCommand command, CancellationToken ct = default)
    {
        var prefillUrl = Url.Action(nameof(PrefillCreateProposal), new { projektId = command.ProjektId, proposalId = command.ProposalId, presentation = PresentationPage })
            ?? $"/Navrhy/PrefillCreateProposal?projektId={command.ProjektId}&proposalId={command.ProposalId}&presentation=page";

        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.CanAccessProject(command.ProjektId),
            invalidAjaxMessage: "Návrh nelze převzít do formuláře.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToProposalTab(command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(Redirect(prefillUrl)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: prefillUrl,
                projectId: command.ProjektId,
                tab: ProposalsTab,
                message: "Návrh byl zamítnut a data byla převzata do nového formuláře.")),
            operation: () => _recordProposalService.RejectAndTakeOverCreateProposalAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:proposals.reject")]
    public Task<IActionResult> RejectAndEditProposal(ProposalDecisionCommand command, CancellationToken ct = default)
    {
        // Policy = proposals.reject (zamítnutí). Následný redirect směřuje na existující
        // záznam (ZaznamyController.Edit) — service metoda RejectAndEditProposalAsync
        // zajistí vrácení ZaznamId (F3 refactor).
        // Službou vytvářený editor už nenakrmuje data z návrhu (EditFromProposal bypass zrušen);
        // admin návrh zamítne a standardní cestou upraví cílový záznam.
        var fallbackEditUrl = Url.Action("Detail", "Projekty", new { id = command.ProjektId, tab = ProposalsTab })
            ?? $"/Projekty/Detail/{command.ProjektId}?tab={ProposalsTab}";

        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId),
            invalidAjaxMessage: "Návrh nelze zamítnout a převzít do formuláře.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToProposalTab(command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(Redirect(fallbackEditUrl)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: fallbackEditUrl,
                projectId: command.ProjektId,
                tab: ProposalsTab,
                message: "Návrh byl zamítnut. Pokračujte standardní editací záznamu.")),
            operation: () => _recordProposalService.RejectAndEditProposalAsync(command, CurrentUserContext, ct));
    }

    private void PrepareProposalEditorModel(ZaznamEditViewModel model, string? requestedPresentation, string? requestedReturnUrl)
    {
        model.Presentation = ResolvePresentation(requestedPresentation);
        model.ReturnUrl = NormalizeLocalReturnUrl(requestedReturnUrl);
        model.UiContext = "project";
        model.MeetingId = null;
        model.BackUrl = NormalizeLocalReturnUrl(requestedReturnUrl)
            ?? (Url.Action("Detail", "Projekty", new { id = model.ProjektId, tab = ProposalsTab }) ?? $"/Projekty/Detail/{model.ProjektId}?tab={ProposalsTab}");
        model.BackLabel = "Zpět do návrhů";
        model.UseAjaxSubmit = true;
    }

    private JsonResult BuildProposalTabAjaxSuccess(int projektId, string message)
    {
        return AjaxSuccessResult(
            refreshScope: ProposalsRefreshScope,
            refreshUrl: Url.Action("NavrhyTabPartial", "Projekty", new { id = projektId }),
            projectId: projektId,
            tab: ProposalsTab,
            message: message);
    }

    private IActionResult BuildProposalSubmitAjaxSuccess(SaveRecordCommand command, string message)
    {
        if (string.Equals(command.Presentation, PresentationPage, StringComparison.OrdinalIgnoreCase))
        {
            var redirectUrl = Url.Action("Detail", "Projekty", new { id = command.ProjektId, tab = ProposalsTab }) ?? $"/Projekty/Detail/{command.ProjektId}?tab={ProposalsTab}";
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: redirectUrl,
                projectId: command.ProjektId,
                tab: ProposalsTab,
                message: message);
        }

        return BuildProposalTabAjaxSuccess(command.ProjektId, message);
    }

    private RedirectToActionResult RedirectToProposalTab(int projektId)
        => RedirectToAction("Detail", "Projekty", new { id = projektId, tab = ProposalsTab })!;

    private string GetEditorViewPath(string presentation)
        => string.Equals(presentation, PresentationPage, StringComparison.OrdinalIgnoreCase)
            ? "~/Views/Projekty/EditZaznamPage.cshtml"
            : "~/Views/Projekty/EditZaznamModal.cshtml";

    private string? NormalizeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    }

    private string ResolvePresentation(string? requestedPresentation)
    {
        if (string.Equals(requestedPresentation, PresentationPage, StringComparison.OrdinalIgnoreCase))
        {
            return PresentationPage;
        }

        if (string.Equals(requestedPresentation, PresentationModal, StringComparison.OrdinalIgnoreCase))
        {
            return PresentationModal;
        }

        return IsAjaxRequest() ? PresentationModal : PresentationPage;
    }
}
