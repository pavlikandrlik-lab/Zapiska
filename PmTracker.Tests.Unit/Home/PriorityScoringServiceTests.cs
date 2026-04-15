using FluentAssertions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Unit.Home;

public sealed class PriorityScoringServiceTests
{
    private static readonly DateOnly Today = new(2026, 4, 14);

    [Fact]
    public void ComputeScore_ShouldReturnNull_WhenRecordIsNotRunning()
    {
        var service = CreateService();

        var result = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: false, Deadline: Today.AddDays(5), NearestFuturePlannedMilestoneDate: Today.AddDays(3)),
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeScore_ShouldReturnNull_WhenUserIsNotRelevant()
    {
        var service = CreateService();

        var result = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(5), NearestFuturePlannedMilestoneDate: Today.AddDays(3)),
            new PriorityUserRelevanceContext(10, IsOwner: false, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeScore_ShouldPreferOwnerWeightOverOtherRoles()
    {
        var service = CreateService();
        var record = new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(2), NearestFuturePlannedMilestoneDate: Today.AddDays(1));

        var ownerScore = service.ComputeScore(
            record,
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: true, IsCollaborator: true),
            Today);
        var collaboratorScore = service.ComputeScore(
            record,
            new PriorityUserRelevanceContext(10, IsOwner: false, IsSubsystemLeadOrDeputy: false, IsCollaborator: true),
            Today);

        ownerScore.Should().NotBeNull();
        collaboratorScore.Should().NotBeNull();
        ownerScore!.RoleWeight.Should().Be(100);
        collaboratorScore!.RoleWeight.Should().Be(40);
        ownerScore.Score.Should().BeGreaterThan(collaboratorScore.Score);
    }

    [Fact]
    public void ComputeScore_ShouldFavorOverdueDeadlineOverEquallyDistantFutureDeadline()
    {
        var service = CreateService();

        var overdue = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(-3), NearestFuturePlannedMilestoneDate: null),
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);
        var future = service.ComputeScore(
            new PriorityRecordContext(2, IsTask: true, IsRunning: true, Deadline: Today.AddDays(3), NearestFuturePlannedMilestoneDate: null),
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);

        overdue.Should().NotBeNull();
        future.Should().NotBeNull();
        overdue!.DeadlineSignal.Should().BeGreaterThan(future!.DeadlineSignal);
        overdue.Score.Should().BeGreaterThan(future.Score);
    }

    [Fact]
    public void ComputeScore_ShouldIgnorePastMilestone()
    {
        var service = CreateService();

        var withoutMilestone = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(10), NearestFuturePlannedMilestoneDate: null),
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);
        var withPastMilestone = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(10), NearestFuturePlannedMilestoneDate: Today.AddDays(-1)),
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);

        withPastMilestone.Should().NotBeNull();
        withoutMilestone.Should().NotBeNull();
        withPastMilestone!.MilestoneSignal.Should().Be(0);
        withPastMilestone.Score.Should().Be(withoutMilestone!.Score);
    }

    [Fact]
    public void ComputeScore_ShouldIncreaseScore_ForNearestFutureMilestone()
    {
        var service = CreateService();

        var withoutMilestone = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(10), NearestFuturePlannedMilestoneDate: null),
            new PriorityUserRelevanceContext(10, IsOwner: false, IsSubsystemLeadOrDeputy: true, IsCollaborator: false),
            Today);
        var withMilestone = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(10), NearestFuturePlannedMilestoneDate: Today.AddDays(2)),
            new PriorityUserRelevanceContext(10, IsOwner: false, IsSubsystemLeadOrDeputy: true, IsCollaborator: false),
            Today);

        withMilestone.Should().NotBeNull();
        withoutMilestone.Should().NotBeNull();
        withMilestone!.MilestoneSignal.Should().BeGreaterThan(0);
        withMilestone.Score.Should().BeGreaterThan(withoutMilestone!.Score);
    }

    [Fact]
    public void ComputeScore_ShouldApplyOverdueCap()
    {
        var service = CreateService(overdueCapDays: 5);

        var result = service.ComputeScore(
            new PriorityRecordContext(1, IsTask: true, IsRunning: true, Deadline: Today.AddDays(-20), NearestFuturePlannedMilestoneDate: null),
            new PriorityUserRelevanceContext(10, IsOwner: true, IsSubsystemLeadOrDeputy: false, IsCollaborator: false),
            Today);

        result.Should().NotBeNull();
        result!.DeadlineSignal.Should().Be(35);
    }

    private static PriorityScoringService CreateService(int horizonDays = 30, int overdueCapDays = 30)
        => new(Options.Create(new DashboardPriorityOptions
        {
            PriorityHorizonDays = horizonDays,
            PriorityOverdueCapDays = overdueCapDays,
            PriorityNightlyRebuildTime = "02:00",
            BootstrapFullRebuildOnStartup = true
        }));
}
