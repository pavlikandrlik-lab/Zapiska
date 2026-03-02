using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Dictionaries;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class CiselnikyController : BaseController
{
    private readonly IDictionariesService _dictionariesService;

    public CiselnikyController(
        IPmTrackerDataStore dataStore,
        IUserContextResolver userContextResolver,
        IDictionariesService dictionariesService)
        : base(dataStore, userContextResolver)
    {
        _dictionariesService = dictionariesService;
    }

    public IActionResult Index(string? id)
    {
        var model = _dictionariesService.BuildCiselnikyDashboard(id);
        return View(model);
    }

    public IActionResult Detail(string id)
    {
        var model = _dictionariesService.BuildCiselnikyDashboard(id);
        return View("Index", model);
    }

    public IActionResult Panel(string id)
    {
        var detail = _dictionariesService.BuildCiselnikDetail(id);
        return PartialView("_CiselnikDetail", detail);
    }

    public IActionResult EditRow(string key, int id)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.CiselnikyEdit))
        {
            return Forbid();
        }

        if (string.Equals(key, "harmonogram-kroky", StringComparison.OrdinalIgnoreCase) && !CurrentUserContext.IsSuperAdmin)
        {
            return Forbid();
        }

        var detail = _dictionariesService.BuildCiselnikDetail(key);
        var row = detail.Polozky.FirstOrDefault(x => x.Id == id);
        if (row is null)
        {
            return NotFound();
        }

        if (row.IsLocked && !CurrentUserContext.IsSuperAdmin)
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
    public IActionResult SaveRow(SaveCiselnikRowCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () =>
            {
                if (!CurrentUserContext.HasPermission(PermissionKeys.CiselnikyEdit))
                {
                    return false;
                }

                if (string.Equals(command.Key, "harmonogram-kroky", StringComparison.OrdinalIgnoreCase)
                    && !CurrentUserContext.IsSuperAdmin)
                {
                    return false;
                }

                if (command.Id.HasValue && !CurrentUserContext.IsSuperAdmin)
                {
                    var detail = _dictionariesService.BuildCiselnikDetail(command.Key);
                    var row = detail.Polozky.FirstOrDefault(x => x.Id == command.Id.Value);
                    if (row is not null && row.IsLocked)
                    {
                        return false;
                    }
                }

                return true;
            },
            invalidAjaxMessage: "Položku číselníku nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { id = command.Key }),
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { id = command.Key }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "ciselniky-detail",
                refreshUrl: Url.Action(nameof(Panel), "Ciselniky", new { id = command.Key }),
                ciselnikKey: command.Key,
                message: "Položka číselníku byla uložena."),
            operation: () => _dictionariesService.SaveCiselnikRow(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteRow(DeleteCiselnikRowCommand command)
    {
        return ExecuteCommand(
            hasPermission: () =>
            {
                if (!CurrentUserContext.HasPermission(PermissionKeys.CiselnikyEdit))
                {
                    return false;
                }

                if (string.Equals(command.Key, "harmonogram-kroky", StringComparison.OrdinalIgnoreCase)
                    && !CurrentUserContext.IsSuperAdmin)
                {
                    return false;
                }

                if (!CurrentUserContext.IsSuperAdmin)
                {
                    var detail = _dictionariesService.BuildCiselnikDetail(command.Key);
                    var row = detail.Polozky.FirstOrDefault(x => x.Id == command.Id);
                    if (row is not null && row.IsLocked)
                    {
                        return false;
                    }
                }

                return true;
            },
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { id = command.Key }),
            onAjaxSuccess: null,
            operation: () => _dictionariesService.DeleteCiselnikRow(command, CurrentUserContext));
    }
}
