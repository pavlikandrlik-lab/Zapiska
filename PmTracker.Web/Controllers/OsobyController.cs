using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.People;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class OsobyController : BaseController
{
    private static readonly TimeSpan AdSyncFromAdFloor = TimeSpan.FromMinutes(1);

    private readonly IActiveDirectoryService _activeDirectoryService;
    private readonly IPeopleService _peopleService;
    private readonly IReactiveSyncQueue<AdReactiveSyncRequest> _adReactiveQueue;
    private readonly IMemoryCache _memoryCache;
    private readonly PmTrackerDbContext _db;

    public OsobyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IActiveDirectoryService activeDirectoryService,
        IPeopleService peopleService,
        IReactiveSyncQueue<AdReactiveSyncRequest> adReactiveQueue,
        IMemoryCache memoryCache,
        PmTrackerDbContext db)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _activeDirectoryService = activeDirectoryService;
        _peopleService = peopleService;
        _adReactiveQueue = adReactiveQueue;
        _memoryCache = memoryCache;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = AttachCurrentUser(await _peopleService.BuildOsobyAsync(ct));
        model.PageTitle = "Osoby";
        model.CanManagePeople = CurrentUserContext.HasPermissionPrefix(PermissionKeys.PeoplePrefix);
        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = "permission:people.ad.search")]
    public async Task<IActionResult> AdPersonModal(CancellationToken ct)
    {
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
    // Per-action redesign 2026-04-23: ManualPersonModal slouží jak pro create (id=null)
    // tak edit (id>0). Policy = people.edit (edit je silnější; admin s jen create klíčem
    // modal nechce — může volat jen SaveManual).
    [Authorize(Policy = "permission:people.edit")]
    public async Task<IActionResult> ManualPersonModal(int? id, CancellationToken ct)
    {
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
    [Authorize(Policy = "permission:people.ad.search")]
    public async Task<IActionResult> SearchAd([FromQuery(Name = "q")] string? query, CancellationToken ct)
    {
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
    // Per-action redesign 2026-04-23: SaveManual větev podle Id:
    //   command.Id == null → people.create, jinak → people.edit.
    // Nejnižší společný jmenovatel pro policy gate: people.edit (který admini mají).
    // Imperativní body check ve F3 refactoru service vrstvy rozliší create vs edit.
    [Authorize(Policy = "permission:people.edit")]
    public async Task<IActionResult> SaveManual(SaveManualPersonCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => true,
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
    [Authorize(Policy = "permission:people.create")]
    public async Task<IActionResult> SaveAd(SaveAdPersonCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => true,
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

    [HttpPost("/Osoby/{id:int}/SyncFromAd")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:people.ad.sync")]
    public async Task<IActionResult> SyncFromAd(int id, CancellationToken ct)
    {
        var osoba = await _db.Osoby.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (osoba is null)
        {
            return NotFound();
        }

        if (osoba.GuidAd is null)
        {
            TempData["AdSyncStatus"] = $"Osoba {osoba.Jmeno} {osoba.Prijmeni} nemá GuidAd, aktualizace z AD není možná.";
            return RedirectToAction(nameof(Index));
        }

        // 1-min hard floor per-osobaId (§13.3).
        var cacheKey = $"ad.manual.{id}";
        if (_memoryCache.TryGetValue<DateTime>(cacheKey, out var lastTriggeredAt))
        {
            var elapsed = DateTime.UtcNow - lastTriggeredAt;
            if (elapsed < AdSyncFromAdFloor)
            {
                var retryAfter = (int)Math.Ceiling((AdSyncFromAdFloor - elapsed).TotalSeconds);
                Response.StatusCode = StatusCodes.Status429TooManyRequests;
                Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
                TempData["AdSyncStatus"] =
                    $"Aktualizace {osoba.Jmeno} {osoba.Prijmeni} byla spuštěna před {(int)elapsed.TotalSeconds} s. Zkus to za {retryAfter} s.";
                return RedirectToAction(nameof(Index));
            }
        }

        await _adReactiveQueue.EnqueueAsync(
            new AdReactiveSyncRequest(id, AdReactiveSource.ManualUpdate), ct);

        _memoryCache.Set(cacheKey, DateTime.UtcNow, AdSyncFromAdFloor);

        TempData["AdSyncStatus"] =
            $"Aktualizace {osoba.Jmeno} {osoba.Prijmeni} z AD spuštěna na pozadí.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:people.delete")]
    public async Task<IActionResult> Delete(DeletePersonCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => true,
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
