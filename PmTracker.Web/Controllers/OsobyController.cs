using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class OsobyController : BaseController
{
    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IPeopleService _peopleService;

    public OsobyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IActiveDirectoryService activeDirectoryService,
        IPeopleService peopleService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _activeDirectoryService = activeDirectoryService;
        _peopleService = peopleService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = AttachCurrentUser(await _peopleService.BuildOsobyAsync(ct));
        model.PageTitle = "Osoby";
        model.CanManagePeople = CurrentUserContext.HasPermission(PermissionKeys.PeopleManage);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> AdPersonModal(CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        {
            return Forbid();
        }

        var searchUrl = Url.Action(nameof(SearchAd), "Osoby") ?? "/Osoby/SearchAd";
        var osobyModel = await _peopleService.BuildOsobyAsync(ct);
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
    public async Task<IActionResult> ManualPersonModal(int? id, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        {
            return Forbid();
        }

        var osobyModel = await _peopleService.BuildOsobyAsync(ct);
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
    public async Task<IActionResult> SearchAd([FromQuery(Name = "q")] string? query, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        {
            return Forbid();
        }

        var response = await _activeDirectoryService.SearchUsersAsync(query, ct);

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
    public async Task<IActionResult> SaveManual(SaveManualPersonCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.PeopleManage),
            invalidAjaxMessage: "Osobu nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index))),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "osoby-index",
                refreshUrl: Url.Action(nameof(Index), "Osoby"),
                message: "Osoba byla uložena.")),
            operation: () => _peopleService.SaveManualPersonAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAd(SaveAdPersonCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.PeopleManage),
            invalidAjaxMessage: "AD osobu nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index))),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "osoby-index",
                refreshUrl: Url.Action(nameof(Index), "Osoby"),
                message: "AD osoba byla uložena.")),
            operation: () => _peopleService.SaveAdPersonAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeletePersonCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.PeopleManage),
            invalidAjaxMessage: "Osobu nelze odstranit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index))),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "osoby-index",
                refreshUrl: Url.Action(nameof(Index), "Osoby"),
                message: "Osoba byla odstraněna.")),
            operation: () => _peopleService.DeletePersonAsync(command, CurrentUserContext, ct));
    }
}
