using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleControllerTests
{
    private const int ProjektId = 42;

    private static ScheduleController CreateController(bool hasSchedulePreview = true)
    {
        var controller = new ScheduleController(
            userContextResolver: null!,
            timeProvider: TimeProvider.System,
            loggerFactory: NullLoggerFactory.Instance,
            previewService: new SchedulePreviewService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Inject CurrentUserContext s optional schedule.preview klíčem na projektu.
        var perProject = new Dictionary<int, IReadOnlySet<string>>();
        if (hasSchedulePreview)
        {
            perProject[ProjektId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                PermissionKeys.SchedulePreview
            };
        }

        var userContext = new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Unit",
            Prijmeni = "Tester",
            DisplayName = "Unit Tester",
            Email = "unit@test.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = false,
            RoleKody = [],
            VisibleProjectIds = [ProjektId],
            DeletedProjectIds = [],
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: perProject,
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
        var field = typeof(BaseController).GetField("<CurrentUserContext>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(controller, userContext);
        return controller;
    }

    private static SchedulePreviewStepInput MakeStep(int index) => new()
    {
        StepIndex = index,
        DurationTypeId = index * 10,
        DelayTypeId = index * 10 + 1,
        DurationDays = 1,
        DelayDays = 0
    };

    private static SchedulePreviewRequest MakeRequest(IEnumerable<SchedulePreviewStepInput> steps) => new()
    {
        ProjektId = ProjektId,
        StartDate = new DateTime(2026, 1, 1),
        DeadlineDate = new DateTime(2027, 1, 1),
        Steps = steps.ToList()
    };

    [Fact]
    public void Recalc_WhenStepsExceedsMax_ReturnsBadRequest()
    {
        var controller = CreateController();
        var request = MakeRequest(Enumerable.Range(1, 501).Select(MakeStep));

        var result = controller.Recalc(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Recalc_AtExactlyMax_Succeeds()
    {
        var controller = CreateController();
        var request = MakeRequest(Enumerable.Range(1, 500).Select(MakeStep));

        var result = controller.Recalc(request);

        result.Should().BeOfType<JsonResult>();
    }

    [Fact]
    public void Recalc_WithoutProjektId_ReturnsBadRequest()
    {
        // Per-action redesign 2026-04-23: ProjektId je povinný pro autorizaci preview.
        var controller = CreateController();
        var request = new SchedulePreviewRequest
        {
            ProjektId = 0,
            StartDate = new DateTime(2026, 1, 1),
            DeadlineDate = new DateTime(2027, 1, 1),
            Steps = [MakeStep(1)]
        };

        var result = controller.Recalc(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Recalc_WhenUserLacksSchedulePreview_ReturnsForbid()
    {
        var controller = CreateController(hasSchedulePreview: false);
        var request = MakeRequest([MakeStep(1)]);

        var result = controller.Recalc(request);

        result.Should().BeOfType<ForbidResult>();
    }
}
