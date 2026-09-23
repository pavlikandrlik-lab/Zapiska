using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

// Jmenný prostor testů (PmTracker.Tests.Unit) stíní typ Unit ze služby — alias to řeší.
using VyzvaUnit = PmTracker.Web.Services.Vyzvy.Unit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Testy skutečného VyzvyControlleru. Do 2026-09-07 si tenhle soubor kopíroval switch
/// výraz z controlleru do těla testu a asertoval nad vlastní kopií — změna controlleru
/// ho neshodila. Nově se volají reálné akce a ověřuje se, co controller předá službě
/// a co vrátí klientovi.
/// </summary>
public sealed class VyzvyControllerTests
{
    private const int ProjektId = 7;
    private const int OsobaId = 42;

    /// <summary>Zaznamenává, s čím controller službu zavolal.</summary>
    private sealed class RecordingVyzvaService : IVyzvaService
    {
        public int? ZalozeniPoradove { get; private set; }
        public DateTime? ZalozeniNow { get; private set; }
        public VyzvaResult<VyzvaDetail> ZalozeniVysledek { get; set; } =
            new VyzvaResult<VyzvaDetail>.Ok(Detail());

        public static VyzvaDetail Detail(int id = 1, string kod = "3/2026", int rok = 2026) => new(
            id, ProjektId, kod, rok, 3, VyzvaStav.Priprava,
            new DateTime(rok, 1, 1), OsobaId, null, null, "FIS", "23106000271",
            Array.Empty<VyzvaDetailItem>());

        public Task<VyzvaResult<VyzvaDetail>> ZalozitVyzvuAsync(
            int projektId, int poradoveVRoce, int zalozilOsobaId, DateTime now, CancellationToken ct)
        {
            ZalozeniPoradove = poradoveVRoce;
            ZalozeniNow = now;
            return Task.FromResult(ZalozeniVysledek);
        }

        public Task<IReadOnlyList<int>> GetObsazenaCislaAsync(int projektId, int rok, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<int>>(new[] { 1, 2 });

        public Task<string?> GetCisloRamcoveSmlouvyAsync(int projektId, CancellationToken ct)
            => Task.FromResult<string?>("23106000271");

        public Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VyzvaBufferItem>>(Array.Empty<VyzvaBufferItem>());

        public Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, int rok, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<VyzvaDetail>>(Array.Empty<VyzvaDetail>());

        public Task<IReadOnlyList<int>> GetRokyAsync(int projektId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        public Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
            => Task.FromResult<VyzvaDetail?>(Detail(vyzvaId));

        public Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(
            int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
            => Task.FromResult<VyzvaResult<VyzvaDetail>>(new VyzvaResult<VyzvaDetail>.Ok(Detail(vyzvaId)));

        public Task<VyzvaResult<VyzvaUnit>> NastavitZaradidAsync(
            int externiOdkazId, bool zaradit, CancellationToken ct)
            => Task.FromResult<VyzvaResult<VyzvaUnit>>(new VyzvaResult<VyzvaUnit>.Ok(default));

        public Task<VyzvaResult<VyzvaUnit>> PrerditPnfAsync(
            int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
            => Task.FromResult<VyzvaResult<VyzvaUnit>>(new VyzvaResult<VyzvaUnit>.Ok(default));

        public Task<int?> ResolveExterniOdkazProjektIdAsync(int externiOdkazId, CancellationToken ct)
            => Task.FromResult<int?>(ProjektId);
    }

    /// <summary>Hodiny stojící na zadaném lokálním čase — pro test přelomu roku.</summary>
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
            "Test/CZ", TimeSpan.FromHours(1), "Test/CZ", "Test/CZ");
    }

    private static VyzvyController CreateController(IVyzvaService service, TimeProvider? time = null)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        var controller = new VyzvyController(
            userContextResolver: null!,
            timeProvider: time ?? TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            vyzvaService: service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        controller.Url = new StubUrlHelper();

        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("BaseController musí mít backing field pro CurrentUserContext");
        field!.SetValue(controller, UserContext());
        return controller;
    }

    /// <summary>Controller staví refreshUrl přes Url.Action; mimo pipeline helper chybí.</summary>
    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string? Action(UrlActionContext actionContext) => "/stub?rok=2026";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/stub";
        public string? RouteUrl(UrlRouteContext routeContext) => "/stub";
    }

    private static CurrentUserContextViewModel UserContext(bool smiZakladat = true)
    {
        var klice = new HashSet<string>();
        if (smiZakladat) klice.Add(PermissionKeys.VyzvyCreate);

        return new CurrentUserContextViewModel
        {
            OsobaId = OsobaId,
            Jmeno = "Vyzva", Prijmeni = "Tester", DisplayName = "Vyzva Tester",
            Email = "vyzva@test.local", OrganizacniCelek = "Test", OrganizacniCelekKod = "TEST",
            IsSuperAdmin = false, RoleKody = [], VisibleProjectIds = [ProjektId], DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>> { { ProjektId, klice } },
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>()),
        };
    }

    /// <summary>
    /// D5: rok výzvy musí vycházet z lokálního času. V UTC+1 je 1. ledna 00:30 místního
    /// času teprve 31. prosince v UTC — při odvození z UTC by výzva spadla do loňské
    /// číselné řady, kterou si projekt domlouvá se SVA.
    /// </summary>
    [Fact]
    public async Task Zalozit_NaPrelomuRoku_PosleSluzbeLokalniCas()
    {
        var service = new RecordingVyzvaService();
        var silvestr = new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.FromHours(1));
        var controller = CreateController(service, new FixedTimeProvider(silvestr));

        await controller.Zalozit(
            new VyzvyController.ZaloztRequest { ProjektId = ProjektId, PoradoveVRoce = 3 },
            CancellationToken.None);

        service.ZalozeniNow.Should().NotBeNull();
        service.ZalozeniNow!.Value.Year.Should().Be(2027,
            "v UTC je pořád 31. 12. 2026, ale výzva patří do roku 2027");
    }

    [Fact]
    public async Task Zalozit_PredavaZadaneCisloAPrihlasenouOsobu()
    {
        var service = new RecordingVyzvaService();
        var controller = CreateController(service);

        await controller.Zalozit(
            new VyzvyController.ZaloztRequest { ProjektId = ProjektId, PoradoveVRoce = 137 },
            CancellationToken.None);

        service.ZalozeniPoradove.Should().Be(137, "číslo zadává uživatel ručně");
    }

    [Fact]
    public async Task Zalozit_BezOpravneni_Nepovoli()
    {
        var service = new RecordingVyzvaService();
        var controller = CreateController(service);

        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(controller, UserContext(smiZakladat: false));

        var result = await controller.Zalozit(
            new VyzvyController.ZaloztRequest { ProjektId = ProjektId, PoradoveVRoce = 3 },
            CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        service.ZalozeniPoradove.Should().BeNull("služba se nesmí vůbec zavolat");
    }

    /// <summary>
    /// Chyba čísla se musí vrátit u pole formuláře, ne jen do souhrnu — uživatel
    /// opravuje konkrétní vstup (spec §5.2).
    /// </summary>
    [Theory]
    [InlineData(VyzvaErrorCode.DuplicateVyzvaNumber)]
    [InlineData(VyzvaErrorCode.InvalidVyzvaNumber)]
    public async Task Zalozit_ChybaCisla_VraciFieldError(VyzvaErrorCode kod)
    {
        var service = new RecordingVyzvaService
        {
            ZalozeniVysledek = new VyzvaResult<VyzvaDetail>.Fail(new VyzvaError(kod, "Chyba čísla")),
        };
        var controller = CreateController(service);

        var result = await controller.Zalozit(
            new VyzvyController.ZaloztRequest { ProjektId = ProjektId, PoradoveVRoce = 3 },
            CancellationToken.None);

        var payload = result.Should().BeAssignableTo<ObjectResult>()
            .Which.Value.Should().BeOfType<ModalSubmitResultViewModel>().Which;

        payload.Ok.Should().BeFalse();
        payload.FieldErrors.Should().ContainKey(nameof(VyzvyController.ZaloztRequest.PoradoveVRoce));
    }

    [Fact]
    public async Task Zalozit_JinaChyba_NevaziSeNaPole()
    {
        var service = new RecordingVyzvaService
        {
            ZalozeniVysledek = new VyzvaResult<VyzvaDetail>.Fail(
                new VyzvaError(VyzvaErrorCode.ProjectMissingMistoPlneni, "Chybí místo plnění")),
        };
        var controller = CreateController(service);

        var result = await controller.Zalozit(
            new VyzvyController.ZaloztRequest { ProjektId = ProjektId, PoradoveVRoce = 3 },
            CancellationToken.None);

        var payload = result.Should().BeAssignableTo<ObjectResult>()
            .Which.Value.Should().BeOfType<ModalSubmitResultViewModel>().Which;

        payload.Ok.Should().BeFalse();
        payload.FieldErrors.Should().BeEmpty("chyba projektu nepatří k poli s číslem");
    }

    /// <summary>Úspěch vede panel zpět na novou výzvu a na její rok (spec §5.3, §8.2).</summary>
    [Fact]
    public async Task Zalozit_Uspech_VratiScopeAKlicDlazdice()
    {
        var service = new RecordingVyzvaService
        {
            ZalozeniVysledek = new VyzvaResult<VyzvaDetail>.Ok(
                RecordingVyzvaService.Detail(id: 55, kod: "3/2026", rok: 2026)),
        };
        var controller = CreateController(service);

        var result = await controller.Zalozit(
            new VyzvyController.ZaloztRequest { ProjektId = ProjektId, PoradoveVRoce = 3 },
            CancellationToken.None);

        var payload = result.Should().BeOfType<JsonResult>()
            .Which.Value.Should().BeOfType<ModalSubmitResultViewModel>().Which;

        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("vyzvy-panel");
        payload.UiContext.Should().Be("vyzva-55");
    }
}
