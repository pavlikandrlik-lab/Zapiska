using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class PendingScheduleProposalLockEvaluatorManualKrokyTests
{
    private static readonly Guid K5 = Guid.Parse("55555555-5555-5555-5555-000000000005");
    private static readonly Guid K8 = Guid.Parse("55555555-5555-5555-5555-000000000008");

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("lock-" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task Evaluate_NoProposal_ShouldNotLock()
    {
        await using var db = NewDb();
        var sut = new PendingScheduleProposalLockEvaluator(db);

        var state = await sut.EvaluateAsync(42);

        state.HasPendingProposal.Should().BeFalse();
        state.LockedManualKrokKeys.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_PendingWithManualKroky_ShouldReturnKrokKeys()
    {
        await using var db = NewDb();
        var payload = new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = 1,
                ZaznamId = 42,
                TerminUkonceni = new DateTime(2026, 6, 1),
                ChangesScheduleActual = false,
                ChangesSchedulePlan = false,
                ChangesTermDeadline = false,
                ManualActualKroky =
                [
                    new ManualActualKrokDto { KrokKey = K5, AbsolutniDatum = new DateOnly(2026, 3, 1) },
                    new ManualActualKrokDto { KrokKey = K8, AbsolutniDatum = new DateOnly(2026, 4, 1) }
                ]
            }
        };
        db.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            Id = 1, ProjektId = 1, ZaznamId = 42, SubsystemId = 7,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
            CreatedByOsobaId = 10,
            CreatedAt = new DateTime(2026, 4, 20)
        });
        await db.SaveChangesAsync();

        var sut = new PendingScheduleProposalLockEvaluator(db);
        var state = await sut.EvaluateAsync(42);

        state.HasPendingProposal.Should().BeTrue();
        state.LockedManualKrokKeys.Should().NotBeNull();
        state.LockedManualKrokKeys!.Should().BeEquivalentTo(new[] { K5, K8 });
        state.LocksSchedule.Should().BeTrue(
            "manuální kroky patří do actual části a musí zamknout rozpis i bez explicitního ChangesScheduleActual");
    }

    [Fact]
    public async Task Evaluate_PendingWithoutManualKroky_ShouldHaveNullKrokKeys()
    {
        await using var db = NewDb();
        var payload = new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = 1,
                ZaznamId = 42,
                TerminUkonceni = new DateTime(2026, 6, 1),
                ChangesSchedulePlan = true
            }
        };
        db.ZaznamNavrhy.Add(new ZaznamNavrhEntity
        {
            Id = 2, ProjektId = 1, ZaznamId = 42, SubsystemId = 7,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
            CreatedByOsobaId = 10,
            CreatedAt = new DateTime(2026, 4, 20)
        });
        await db.SaveChangesAsync();

        var sut = new PendingScheduleProposalLockEvaluator(db);
        var state = await sut.EvaluateAsync(42);

        state.HasPendingProposal.Should().BeTrue();
        state.LockedManualKrokKeys.Should().BeNull();
    }
}
