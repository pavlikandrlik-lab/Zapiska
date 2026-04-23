using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class CiselnikyController : BaseController
{
    private readonly IDictionaryService _dictionaryService;

    public CiselnikyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IDictionaryService dictionaryService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _dictionaryService = dictionaryService;
    }

    public async Task<IActionResult> Index(string? id, CancellationToken ct)
    {
        var model = await _dictionaryService.BuildCiselnikyDashboardAsync(id, CurrentUserContext, ct);
        model.PageTitle = "Číselníky";
        PrepareDictionaryDetailPresentation(model.VybranyCiselnik);
        return View(model);
    }

    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        var model = await _dictionaryService.BuildCiselnikyDashboardAsync(id, CurrentUserContext, ct);
        model.PageTitle = "Číselníky";
        PrepareDictionaryDetailPresentation(model.VybranyCiselnik);
        return View("Index", model);
    }

    public async Task<IActionResult> Panel(string id, CancellationToken ct)
    {
        var detail = await _dictionaryService.BuildCiselnikDetailAsync(id, CurrentUserContext, ct);
        PrepareDictionaryDetailPresentation(detail);
        return PartialView("_CiselnikDetail", detail);
    }

    [Authorize(Policy = "permission:ciselniky.row.edit")]
    public async Task<IActionResult> EditRow(string key, int id, CancellationToken ct)
    {
        if (!DictionarySecurityPolicy.CanAccessDictionary(key, CurrentUserContext.IsSuperAdmin))
        {
            return Forbid();
        }

        var detail = await _dictionaryService.BuildCiselnikDetailAsync(key, CurrentUserContext, ct);
        var row = detail.Polozky.FirstOrDefault(x => x.Id == id);
        if (row is null)
        {
            return NotFound();
        }

        if (!DictionarySecurityPolicy.CanModifyRow(key, row.IsLocked, CurrentUserContext.IsSuperAdmin))
        {
            return Forbid();
        }

        var model = new CiselnikRadekEditViewModel
        {
            Key = detail.Key,
            CiselnikNazev = detail.Nazev,
            Id = row.Id,
            Kod = row.Kod,
            Nazev = row.Nazev,
            IsLocked = row.IsLocked,
            CanChangeLockState = row.CanChangeLockState,
            SloupecNavic = detail.SloupceNavic.FirstOrDefault(),
            HodnotaNavic = row.HodnotyNavic.FirstOrDefault(),
            HodnotaNavicRaw = row.HodnotyNavicRaw.FirstOrDefault() ?? row.HodnotyNavic.FirstOrDefault(),
            HodnotaNavicVolby = detail.HodnotaNavicVolby,
            IsHodnotaNavicSelect = detail.IsHodnotaNavicSelect,
            IsHodnotaNavicRequired = detail.IsHodnotaNavicRequired
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:ciselniky.row.edit")]
    public async Task<IActionResult> SaveRow(SaveCiselnikRowCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: async () =>
            {
                if (!DictionarySecurityPolicy.CanAccessDictionary(command.Key, CurrentUserContext.IsSuperAdmin))
                {
                    return false;
                }

                if (command.Id.HasValue && !CurrentUserContext.IsSuperAdmin)
                {
                    var detail = await _dictionaryService.BuildCiselnikDetailAsync(command.Key, CurrentUserContext, ct);
                    var row = detail.Polozky.FirstOrDefault(x => x.Id == command.Id.Value);
                    if (row is not null && !DictionarySecurityPolicy.CanModifyRow(command.Key, row.IsLocked, CurrentUserContext.IsSuperAdmin))
                    {
                        return false;
                    }
                }

                return true;
            },
            invalidAjaxMessage: "Položku číselníku nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { id = command.Key }),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index), new { id = command.Key })),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "ciselniky-detail",
                refreshUrl: Url.Action(nameof(Panel), "Ciselniky", new { id = command.Key }),
                ciselnikKey: command.Key,
                message: "Položka číselníku byla uložena.")),
            operation: () => _dictionaryService.SaveCiselnikRowAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:ciselniky.row.delete")]
    public async Task<IActionResult> DeleteRow(DeleteCiselnikRowCommand command, CancellationToken ct = default)
    {
        return await ExecuteCommandAsync(
            hasPermission: async () =>
            {
                if (!DictionarySecurityPolicy.CanAccessDictionary(command.Key, CurrentUserContext.IsSuperAdmin))
                {
                    return false;
                }

                if (!CurrentUserContext.IsSuperAdmin)
                {
                    var detail = await _dictionaryService.BuildCiselnikDetailAsync(command.Key, CurrentUserContext, ct);
                    var row = detail.Polozky.FirstOrDefault(x => x.Id == command.Id);
                    if (row is not null && !DictionarySecurityPolicy.CanModifyRow(command.Key, row.IsLocked, CurrentUserContext.IsSuperAdmin))
                    {
                        return false;
                    }
                }

                return true;
            },
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index), new { id = command.Key })),
            onAjaxSuccess: null,
            operation: () => _dictionaryService.DeleteCiselnikRowAsync(command, CurrentUserContext, ct));
    }

    private void PrepareDictionaryDetailPresentation(CiselnikDetailViewModel detail)
    {
        AttachCurrentUser(detail);
        detail.PageTitle = detail.Nazev;
        // F7 fix 2026-04-23: "ciselniky.edit" smazán; UI-gate = user má alespoň jeden
        // ciselniky.row.* klíč (edit / delete) — uvidí tlačítko Edit, protože reálnou
        // autorizaci akce provádí [Authorize(Policy="permission:ciselniky.row.edit")].
        detail.CanEditCiselnik = CurrentUserContext.HasPermission(PermissionKeys.CiselnikyRowEdit)
            || CurrentUserContext.HasPermission(PermissionKeys.CiselnikyRowDelete);
        detail.IsArchitect = CurrentUserContext.IsSuperAdmin;
    }
}
