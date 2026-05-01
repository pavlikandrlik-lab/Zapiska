using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class SyncHostedServiceBaseTests
{
    private sealed class FakeSettings : ISyncJobSettings
    {
        public int Id { get; set; } = 1;
        public bool IsEnabled { get; set; }
        public int PeriodMinutes { get; set; }
        public DateTimeOffset AnchorAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public string? LastTriggerKind { get; set; }
        public string? LastResultJson { get; set; }
        public bool IsRunning { get; set; }
        public DateTime? RunStartedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByOsobaId { get; set; }
    }

    private sealed class FakeHostedService : SyncHostedServiceBase<FakeSettings>
    {
        public int RunCount;
        public List<SyncTriggerKind> TriggerLog { get; } = new();
        public TaskCompletionSource<int> FirstRunComplete { get; } = new();

        private readonly FakeSettings _settings;

        public FakeHostedService(
            FakeSettings settings,
            IServiceScopeFactory scopeFactory,
            ISyncJobRunLock<FakeSettings> runLock,
            ManualTriggerSignal<FakeSettings> manualSignal,
            TimeProvider timeProvider)
            : base(scopeFactory, runLock, manualSignal, timeProvider, NullLogger<FakeHostedService>.Instance)
        {
            _settings = settings;
        }

        protected override string JobKey => "test.job";

        protected override Task<FakeSettings> LoadSettingsAsync(IServiceScope scope, CancellationToken ct)
            => Task.FromResult(_settings);

        protected override Task SaveSettingsAsync(IServiceScope scope, FakeSettings settings, CancellationToken ct)
            => Task.CompletedTask;

        protected override Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct)
        {
            Interlocked.Increment(ref RunCount);
            TriggerLog.Add(trigger);
            if (RunCount == 1) FirstRunComplete.TrySetResult(RunCount);
            return Task.FromResult<object>(new { ok = true });
        }
    }

    private static FakeHostedService BuildSut(FakeSettings settings, TimeProvider time, ManualTriggerSignal<FakeSettings>? signal = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new FakeHostedService(
            settings,
            services.GetRequiredService<IServiceScopeFactory>(),
            new SyncJobRunLock<FakeSettings>(),
            signal ?? new ManualTriggerSignal<FakeSettings>(),
            time);
    }

    [Fact]
    public async Task CatchUpOnStartup_WhenLastRunAtIsNull()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);
        var settings = new FakeSettings
        {
            IsEnabled = true,
            PeriodMinutes = 60,
            AnchorAt = fakeNow.AddHours(-1),
            LastRunAt = null
        };
        var sut = BuildSut(settings, time);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await sut.FirstRunComplete.Task.WaitAsync(TimeSpan.FromSeconds(2));

        sut.RunCount.Should().BeGreaterThanOrEqualTo(1);
        sut.TriggerLog.First().Should().Be(SyncTriggerKind.Auto);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task CatchUpOnStartup_WhenLastRunAtStale()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);
        var settings = new FakeSettings
        {
            IsEnabled = true,
            PeriodMinutes = 60,
            AnchorAt = fakeNow.AddDays(-1),
            LastRunAt = fakeNow.AddHours(-3).UtcDateTime   // více než 1× period = stale
        };
        var sut = BuildSut(settings, time);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await sut.FirstRunComplete.Task.WaitAsync(TimeSpan.FromSeconds(2));

        sut.RunCount.Should().BeGreaterThanOrEqualTo(1);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SkipsWhenNotEnabled()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);
        var settings = new FakeSettings
        {
            IsEnabled = false,
            PeriodMinutes = 60,
            AnchorAt = fakeNow,
            LastRunAt = null
        };
        var sut = BuildSut(settings, time);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(200);    // čas na start
        sut.RunCount.Should().Be(0);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ManualSignal_WakesUpDisabledLoop_AndRunsTickWithManualTrigger()
    {
        // Regression: před fixem 2026-04-29 disabled-branch volala SafeDelay(60s)
        // a manual signal se spotřeboval pouze v WaitForNextTriggerAsync uvnitř
        // enabled větve. User klikl "Spustit teď" na vypnutém jobu → signal se
        // ztratil a job neběžel. Po fixu disabled-branch také poslouchá signal.
        var fakeNow = new DateTimeOffset(2026, 4, 29, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);
        var settings = new FakeSettings
        {
            IsEnabled = false,
            PeriodMinutes = 60,
            AnchorAt = fakeNow,
            LastRunAt = null
        };
        var signal = new ManualTriggerSignal<FakeSettings>();
        var sut = BuildSut(settings, time, signal);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        // Give the loop a tick to enter the disabled-branch wait.
        await Task.Delay(100);

        signal.Signal();

        await sut.FirstRunComplete.Task.WaitAsync(TimeSpan.FromSeconds(2));
        sut.RunCount.Should().Be(1);
        sut.TriggerLog.First().Should().Be(SyncTriggerKind.Manual);

        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ManualSignal_WakesUpLoop_AndMarksTriggerManual()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);
        var settings = new FakeSettings
        {
            IsEnabled = true,
            PeriodMinutes = 60,
            // Anchor 30 min in the future → next auto tick won't fire during the test
            // window. Catch-up bypassed because LastRunAt is fresh.
            AnchorAt = fakeNow.AddMinutes(30),
            LastRunAt = fakeNow.UtcDateTime
        };
        var signal = new ManualTriggerSignal<FakeSettings>();
        var sut = BuildSut(settings, time, signal);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        // Give the loop a tick to enter the Task.WhenAny delay.
        await Task.Delay(100);

        signal.Signal();

        await sut.FirstRunComplete.Task.WaitAsync(TimeSpan.FromSeconds(2));
        sut.TriggerLog.First().Should().Be(SyncTriggerKind.Manual);

        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }
}
