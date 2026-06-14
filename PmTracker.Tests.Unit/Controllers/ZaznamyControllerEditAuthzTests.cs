using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.Controllers;

/// <summary>
/// Review finding R2-N-2: regresní testy pro S-2 fix (Edit akce autorizace PŘED
/// DB loadem a harvest triggerem).
/// </summary>
public sealed class ZaznamyControllerEditAuthzTests
{
    private const int RecordId = 500;
    private const int ProjektId = 100;
    private const int UserId = 42;

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("edit-authz-" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task Edit_WithoutAnyPermission_ReturnsForbid()
    {
        // User nemá ani records.edit, ani records.schedule.* — musí dostat Forbid
        // ještě PŘED načtením editor view modelu (S-2 fix: žádný existence leak,
        // žádný harvest trigger).
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = RecordId, ProjektId = ProjektId, SubsystemId = 1, KategorieId = 1,
            Nazev = "target"
        });
        await db.SaveChangesAsync();

        var recordService = new Mock<IRecordService>(MockBehavior.Strict);
        var harvestScheduler = new Mock<IHarvestScheduler>(MockBehavior.Strict);
        var recordUiFlow = new Mock<IRecordUiFlowResolver>();

        var controller = BuildController(db, recordService.Object, harvestScheduler.Object, recordUiFlow.Object,
            BuildUserContext(globalKeys: Array.Empty<string>(), projectKeys: new Dictionary<int, IReadOnlySet<string>>()));

        var result = await controller.Edit(RecordId, returnUrl: null, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        recordService.Verify(x => x.BuildZaznamEditAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never,
            "při Forbid se nesmí volat BuildZaznamEditAsync (existence leak).");
        harvestScheduler.Verify(x => x.ScheduleHarvestForRecordAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SdReactiveSource>()), Times.Never,
            "při Forbid se nesmí volat ScheduleHarvestForRecordAsync (T5 trigger amplifier).");
    }

    [Fact]
    public async Task Edit_WithoutRecordsEdit_DoesNotTriggerHarvest()
    {
        // User má records.schedule.edit ale NE records.edit — smí otevřít editor
        // (schedule tab je pro něj editovatelný), ale NESMÍ se triggerovat harvest
        // (T5 je jen pro records.edit, schedule-only role nemá business need).
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = RecordId, ProjektId = ProjektId, SubsystemId = 1, KategorieId = 1,
            Nazev = "target"
        });
        await db.SaveChangesAsync();

        var recordService = new Mock<IRecordService>();
        recordService
            .Setup(x => x.BuildZaznamEditAsync(RecordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildMinimalZaznamEditViewModel());

        var harvestScheduler = new Mock<IHarvestScheduler>(MockBehavior.Strict);
        var recordUiFlow = new Mock<IRecordUiFlowResolver>();

        var projectKeys = new Dictionary<int, IReadOnlySet<string>>
        {
            [ProjektId] = new HashSet<string> { PermissionKeys.RecordsScheduleEdit }
        };
        var controller = BuildController(db, recordService.Object, harvestScheduler.Object, recordUiFlow.Object,
            BuildUserContext(globalKeys: Array.Empty<string>(), projectKeys: projectKeys));

        var result = await controller.Edit(RecordId, returnUrl: null, CancellationToken.None);

        result.Should().BeOfType<ViewResult>();
        harvestScheduler.Verify(x => x.ScheduleHarvestForRecordAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<SdReactiveSource>()), Times.Never,
            "uživatel bez records.edit (pouze schedule.edit) nesmí triggerovat T5 harvest.");
    }

    private static ZaznamyController BuildController(
        PmTrackerDbContext db,
        IRecordService recordService,
        IHarvestScheduler harvestScheduler,
        IRecordUiFlowResolver recordUiFlow,
        CurrentUserContextViewModel userContext)
    {
        var controller = new ZaznamyController(
            userContextResolver: new FakeUserContextResolver(),
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            recordService: recordService,
            projectEditQuery: new ProjectEditQuery(recordService),
            recordUiFlowResolver: recordUiFlow,
            harvestScheduler: harvestScheduler,
            db: db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            },
            Url = new FakeUrlHelper()
        };
        SetCurrentUserContext(controller, userContext);
        return controller;
    }

    private sealed class FakeUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new ActionContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()
        };
        public string? Action(UrlActionContext actionContext) => "/fake";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/fake";
        public string? RouteUrl(UrlRouteContext routeContext) => "/fake";
    }

    private static void SetCurrentUserContext(ZaznamyController controller, CurrentUserContextViewModel userContext)
    {
        var field = typeof(BaseController).GetField(
            "<CurrentUserContext>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(controller, userContext);
    }

    private static CurrentUserContextViewModel BuildUserContext(
        IReadOnlyCollection<string> globalKeys,
        IReadOnlyDictionary<int, IReadOnlySet<string>> projectKeys)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = UserId,
            Jmeno = "Jan",
            Prijmeni = "Tester",
            DisplayName = "Jan Tester",
            Email = "jan@test.local",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
            IsSuperAdmin = false,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = projectKeys.Keys.ToList(),
            DeletedProjectIds = Array.Empty<int>(),
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(globalKeys),
                PerProjectPermissions: projectKeys,
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }

    private static ZaznamEditViewModel BuildMinimalZaznamEditViewModel()
    {
        return new ZaznamEditViewModel
        {
            PageTitle = string.Empty,
            Id = RecordId,
            CisloZaznamu = 1,
            CisloViditelne = "TEST-1",
            ProjektId = ProjektId,
            IsCreate = false,
            Nazev = "n",
            Cil = "c",
            Kategorie = "U",
            Popis = "p",
            Stav = "Nový",
            KategorieZaznamu = new List<string>(),
            StavyUkolu = new List<string>(),
            TypyUkolu = new List<string>(),
            JednaniProCisloOptions = new List<JednaniOptionViewModel>(),
            Subsystemy = new List<SubsystemOptionViewModel>(),
            Subsystem = "SYS",
            Vlastnici = new List<LookupOptionViewModel>(),
            VlastnikId = 1,
            JeUkolKategorie = true,
            DostupniVlastnici = new List<SpolupracovnikOptionViewModel>(),
            DostupniSpolupracovnici = new List<SpolupracovnikOptionViewModel>(),
            VybraniSpolupracovniciIds = new List<int>(),
            ExterniVazby = new List<ExterniOdkazEditViewModel>(),
            TypyExternichOdkazu = new List<string>(),
            DatumZalozeni = new DateTime(2026, 1, 1),
            TerminUkonceni = new DateTime(2026, 6, 1)
        };
    }

    private sealed class FakeUserContextResolver : IUserContextResolver
    {
        public Task<UserContextResolutionResult> ResolveAsync(HttpContext httpContext, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
