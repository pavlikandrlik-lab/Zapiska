using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Controllers;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleControllerTests
{
    private static ScheduleController CreateController() =>
        new(new SchedulePreviewService());

    private static SchedulePreviewStepInput MakeStep(int index) => new()
    {
        StepIndex = index,
        DurationTypeId = index * 10,
        DelayTypeId = index * 10 + 1,
        DurationDays = 1,
        DelayDays = 0
    };

    [Fact]
    public void Recalc_WhenStepsExceedsMax_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var steps = Enumerable.Range(1, 501).Select(MakeStep).ToList();
        var request = new SchedulePreviewRequest
        {
            StartDate = new DateTime(2026, 1, 1),
            DeadlineDate = new DateTime(2027, 1, 1),
            Steps = steps
        };

        // Act
        var result = controller.Recalc(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Recalc_AtExactlyMax_Succeeds()
    {
        // Arrange
        var controller = CreateController();
        var steps = Enumerable.Range(1, 500).Select(MakeStep).ToList();
        var request = new SchedulePreviewRequest
        {
            StartDate = new DateTime(2026, 1, 1),
            DeadlineDate = new DateTime(2027, 1, 1),
            Steps = steps
        };

        // Act
        var result = controller.Recalc(request);

        // Assert
        result.Should().BeOfType<JsonResult>();
    }
}
