using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Review fix M2 (2026-10-12): test double pro <see cref="IHarvestScheduler"/>. Zaznamenává
/// volání, aby šlo testovat "schválení přepnutí na Auto naplánuje harvest" i "non-enqueue"
/// (návrh beze změny rezimu) bez reálné ServiceDesk/reactive-queue infrastruktury. Registruje se
/// přes <see cref="IntegrationTestHelper.CreateDataStore"/> seam (<c>configureServices</c>),
/// po registraci reálného <see cref="ReactiveHarvestSchedulerAdapter"/> v
/// <c>AddPmTrackerDataStore</c> — poslední registrace vyhrává.
/// </summary>
internal sealed class RecordingHarvestScheduler : IHarvestScheduler
{
    public List<int> RecordHarvestCalls { get; } = new();
    public List<int> LinkHarvestCalls { get; } = new();

    public Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default, SdReactiveSource source = SdReactiveSource.RecordSave)
    {
        LinkHarvestCalls.Add(externiOdkazId);
        return Task.CompletedTask;
    }

    public Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default, SdReactiveSource source = SdReactiveSource.EditorOpen)
    {
        RecordHarvestCalls.Add(zaznamId);
        return Task.CompletedTask;
    }
}
