using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class OsobyController : BaseController
{
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IPeopleService _peopleService;

    public OsobyController(
        IPmTrackerDataStore dataStore,
        IUserContextResolver userContextResolver,
        IActiveDirectoryService activeDirectoryService,
        IPeopleService peopleService)
        : base(dataStore, userContextResolver)
    {
        _activeDirectoryService = activeDirectoryService;
        _peopleService = peopleService;
    }

    public IActionResult Index()
    {
        var model = _peopleService.BuildOsoby();
        return View(model);
    }

    [HttpGet]
    public IActionResult AdPersonModal()
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        {
            return Forbid();
        }

        var searchUrl = Url.Action(nameof(SearchAd), "Osoby") ?? "/Osoby/SearchAd";
        var osobyModel = _peopleService.BuildOsoby();
        var model = new AdPersonModalViewModel
        {
            Title = "Přidat osobu z AD",
            SearchUrl = searchUrl,
            Organizace = osobyModel.Organizace,
            OrganizacniCelky = osobyModel.OrganizacniCelky
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult ManualPersonModal(int? id)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        {
            return Forbid();
        }

        var osobyModel = _peopleService.BuildOsoby();
        OsobaListItemViewModel? osoba = null;

        if (id.HasValue)
        {
            osoba = osobyModel.Osoby.FirstOrDefault(item => item.Id == id.Value);
            if (osoba is null)
            {
                return NotFound();
            }
        }

        var command = new SaveManualPersonCommand
        {
            Id = osoba?.Id,
            Jmeno = osoba?.Jmeno ?? string.Empty,
            Prijmeni = osoba?.Prijmeni ?? string.Empty,
            Titul = osoba?.Titul,
            Email = osoba?.Email,
            Organizace = osoba?.OrganizaceKod,
            OrganizacniCelek = osoba?.OrganizacniCelekKod,
            LocationLocked = osoba?.LocationLocked ?? false
        };

        var model = new ManualPersonModalViewModel
        {
            Title = id.HasValue
                ? osoba?.JeAdUcet == true
                    ? "Upravit organizační přiřazení osoby z AD"
                    : "Upravit ručně přidanou osobu"
                : "Přidat osobu ručně",
            Command = command,
            Organizace = osobyModel.Organizace,
            OrganizacniCelky = osobyModel.OrganizacniCelky,
            IsAdAccount = osoba?.JeAdUcet == true
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> SearchAd([FromQuery(Name = "q")] string? query, CancellationToken cancellationToken)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        {
            return Forbid();
        }

        var response = await _activeDirectoryService.SearchUsersAsync(query, cancellationToken);

        return Json(new
        {
            available = response.Available,
            message = response.Message,
            results = response.Results.Select(x => new
            {
                guidAd = x.GuidAd,
                adLogin = x.AdLogin,
                displayName = x.DisplayName,
                jmeno = x.Jmeno,
                prijmeni = x.Prijmeni,
                titul = x.Titul,
                email = x.Email,
                company = x.Company,
                department = x.Department,
                canSelect = x.CanSelect,
                disabledReason = x.DisabledReason
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveManual(SaveManualPersonCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.PeopleManage),
            invalidAjaxMessage: "Osobu nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => RedirectToAction(nameof(Index)),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "osoby-index",
                refreshUrl: Url.Action(nameof(Index), "Osoby"),
                message: "Osoba byla uložena."),
            operation: () => _peopleService.SaveManualPerson(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveAd(SaveAdPersonCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.PeopleManage),
            invalidAjaxMessage: "AD osobu nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => RedirectToAction(nameof(Index)),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "osoby-index",
                refreshUrl: Url.Action(nameof(Index), "Osoby"),
                message: "AD osoba byla uložena."),
            operation: () => _peopleService.SaveAdPerson(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(DeletePersonCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.PeopleManage),
            invalidAjaxMessage: "Osobu nelze odstranit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => RedirectToAction(nameof(Index)),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "osoby-index",
                refreshUrl: Url.Action(nameof(Index), "Osoby"),
                message: "Osoba byla odstraněna."),
            operation: () => _peopleService.DeletePerson(command, CurrentUserContext));
    }
}
