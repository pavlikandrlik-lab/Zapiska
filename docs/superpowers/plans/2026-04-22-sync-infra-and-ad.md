# Sync infrastruktura + AD konzument — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Postavit sdílenou sync infrastrukturu (periodic tick + reactive queue) jako first-class abstrakci v PM Tracker, a jako prvního konzumenta implementovat periodic + reactive AD synchronizaci osob.

**Architecture:** Nový namespace `PmTracker.Web.Services.Sync` obsahující `SyncScheduleCalculator` (pure static aligned-schedule algorithm), `ISyncJobSettings` (interface per-job typed entity), `SyncHostedServiceBase<T>` (abstract generic BackgroundService loop s catch-up on startup + semafor guard), `ISyncJobRunLock<T>` (in-memory SemaphoreSlim per job), `IReactiveSyncQueue<TRequest>` (bounded `System.Threading.Channels.Channel<T>` wrapper), `ReactiveSyncConsumerBase<TRequest>` (abstract BackgroundService consumer). AD konzument přidává `AdSyncSettingsEntity` (singleton row), `IAdSyncService` (SyncAllPeopleAsync + SyncSinglePersonAsync), `AdPeriodicSyncHostedService`, `AdReactiveSyncConsumer`, rozšíření `IActiveDirectoryService.ListByGuidsAsync` a admin UI kartu v `/Nastaveni?section=synchronizace`.

**Tech Stack:** .NET 8 ASP.NET Core MVC, EF Core 8, `Microsoft.Extensions.Hosting.BackgroundService`, `System.Threading.Channels`, `TimeProvider`, `Microsoft.Extensions.TimeProvider.Testing` (nový test package), `System.DirectoryServices.AccountManagement` (existující), xUnit + FluentAssertions + Moq.

**Spec:** [docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md](docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md)

**Předpoklady:**
- Single-instance aplikace (žádný distributed locking)
- DB je SQL Server (PM Tracker DB connection)
- Dev DB přístup pro manuální spuštění upgrade skriptu (`/tmp/run-sql` helper)
- [Microsoft.Extensions.TimeProvider.Testing](https://www.nuget.org/packages/Microsoft.Extensions.TimeProvider.Testing) dostupný z nuget.org

---

## File Structure

### Nové soubory — shared sync infrastructure

- `PmTracker.Web/Services/Sync/SyncTriggerKind.cs` — enum Auto/Manual
- `PmTracker.Web/Services/Sync/SyncScheduleCalculator.cs` — pure static aligned-schedule
- `PmTracker.Web/Services/Sync/ISyncJobSettings.cs` — interface pro per-job settings entity
- `PmTracker.Web/Services/Sync/ISyncJobRunLock.cs` — per-job concurrency lock interface
- `PmTracker.Web/Services/Sync/SyncJobRunLock.cs` — SemaphoreSlim wrapper impl
- `PmTracker.Web/Services/Sync/IReactiveSyncQueue.cs` — Channel-wrapper interface
- `PmTracker.Web/Services/Sync/ReactiveSyncQueue.cs` — bounded Channel impl
- `PmTracker.Web/Services/Sync/ReactiveSyncConsumerBase.cs` — abstract queue consumer
- `PmTracker.Web/Services/Sync/SyncHostedServiceBase.cs` — abstract generic periodic loop

### Nové soubory — AD konzument

- `PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs` — singleton row entity
- `PmTracker.Web/Data/Configuration/AdSyncSettingsEntityConfiguration.cs` — EF mapping
- `PmTracker.Web/Services/ActiveDirectory/AdSyncModels.cs` — AdSyncResult, AdSyncErrorItem, AdReactiveSyncRequest, AdReactiveSource
- `PmTracker.Web/Services/ActiveDirectory/IAdSyncService.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdSyncService.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdPeriodicSyncHostedService.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdReactiveSyncConsumer.cs`

### Nové soubory — UI

- `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsCardViewModel.cs`
- `PmTracker.Web/Models/ViewModels/Sync/SyncJobResultSummary.cs`
- `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsInputModel.cs`
- `PmTracker.Web/Services/Sync/ISyncJobAdminHandler.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdSyncJobAdminHandler.cs`
- `PmTracker.Web/Controllers/NastaveniSyncController.cs`
- `PmTracker.Web/Views/Shared/_SyncJobSettingsCard.cshtml`
- `PmTracker.Web/Views/Nastaveni/_SyncPanel.cshtml` — kontejner pro jednu nebo více karet

### Nové soubory — DB

- `db_upgrade_1_3_0_ad_sync_settings.sql`

### Nové soubory — testy

- `PmTracker.Tests.Unit/Sync/SyncScheduleCalculatorTests.cs`
- `PmTracker.Tests.Unit/Sync/SyncJobRunLockTests.cs`
- `PmTracker.Tests.Unit/Sync/ReactiveSyncQueueTests.cs`
- `PmTracker.Tests.Unit/Sync/SyncHostedServiceBaseTests.cs`
- `PmTracker.Tests.Unit/Sync/ReactiveSyncConsumerBaseTests.cs`
- `PmTracker.Tests.Unit/ActiveDirectory/AdSyncServiceTests.cs`
- `PmTracker.Tests.Unit/ActiveDirectory/AdPeriodicSyncHostedServiceTests.cs`
- `PmTracker.Tests.Unit/ActiveDirectory/AdReactiveSyncConsumerTests.cs`

### Modifikované soubory

- `PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — přidat `Microsoft.Extensions.TimeProvider.Testing`
- `PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs` — přidat `ListByGuidsAsync`
- `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryService.cs` — impl `ListByGuidsAsync`
- `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryModels.cs` — přidat `ActiveDirectoryBatchResponse`
- `PmTracker.Web/Data/PmTrackerDbContext.cs` — přidat `DbSet<AdSyncSettingsEntity>`
- `PmTracker.Web/Program.cs` — DI registrace shared infra + AD konzument
- `PmTracker.Web/Views/Nastaveni/Index.cshtml` — nová navigation položka
- `PmTracker.Web/Services/Settings/SettingsService.cs` — registrovat sekci `synchronizace`
- `PmTracker.Web/Models/ViewModels/NastaveniViewModels.cs` — přidat section metadata
- `PmTracker.Web/Controllers/OsobyController.cs` (nebo wherever AD pick flow) — enqueue reactive request po insertu
- `PmTracker.Web/Views/Osoby/Detail.cshtml` (nebo ekvivalent) — tlačítko „Aktualizovat z AD"

---

## Pořadí úkolů

| Fáze | Task | Obsah |
|---|---|---|
| A | 1 | NuGet TimeProvider.Testing + namespace scaffold |
| A | 2 | `SyncScheduleCalculator` TDD (10 test cases) |
| A | 3 | `SyncTriggerKind` + `ISyncJobSettings` |
| A | 4 | `ISyncJobRunLock` + `SyncJobRunLock` + tests |
| A | 5 | `IReactiveSyncQueue` + `ReactiveSyncQueue` + tests |
| A | 6 | `ReactiveSyncConsumerBase` + tests |
| A | 7 | `SyncHostedServiceBase` + catch-up + tests |
| B | 8 | DB upgrade skript + aplikace na Dev DB |
| B | 9 | `AdSyncSettingsEntity` + EF config + DbContext |
| B | 10 | `IActiveDirectoryService.ListByGuidsAsync` + `ActiveDirectoryBatchResponse` + impl |
| B | 11 | AD sync models |
| B | 12 | `IAdSyncService` + `AdSyncService` + unit tests |
| C | 13 | `AdPeriodicSyncHostedService` + integration test |
| C | 14 | `AdReactiveSyncConsumer` + integration test |
| D | 15 | Shared view model + `_SyncJobSettingsCard.cshtml` partial |
| D | 16 | `ISyncJobAdminHandler` + `AdSyncJobAdminHandler` + `NastaveniSyncController` |
| D | 17 | Nastaveni navigace + sekce `synchronizace` |
| D | 18 | Wire reactive triggery (person pick + manual update button) |
| E | 19 | Full build + test + final commit hygiene |

---

## Task 1: NuGet `Microsoft.Extensions.TimeProvider.Testing` + namespace scaffold

**Files:**
- Modify: `PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj`
- Create: `PmTracker.Web/Services/Sync/` (adresář)

- [ ] **Step 1: Přidat NuGet package**

Run:
```bash
dotnet add PmTracker.Tests.Unit package Microsoft.Extensions.TimeProvider.Testing --version 8.11.0
```

Expected output: `PackageReference for package 'Microsoft.Extensions.TimeProvider.Testing' version '8.11.0' added`.

- [ ] **Step 2: Ověřit reference v csproj**

Run:
```bash
grep "TimeProvider.Testing" PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
```

Expected: `<PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="8.11.0" />`

- [ ] **Step 3: Vytvořit adresář pro shared infra**

```bash
mkdir -p PmTracker.Web/Services/Sync
mkdir -p PmTracker.Tests.Unit/Sync
```

- [ ] **Step 4: Build ověří, že package se restore-lo**

Run: `dotnet build PmTracker.Tests.Unit --no-restore`

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
git commit -m "chore(tests): přidat Microsoft.Extensions.TimeProvider.Testing pro hosted service testy"
```

---

## Task 2: `SyncScheduleCalculator` TDD

**Files:**
- Create: `PmTracker.Tests.Unit/Sync/SyncScheduleCalculatorTests.cs`
- Create: `PmTracker.Web/Services/Sync/SyncScheduleCalculator.cs`

- [ ] **Step 1: Napsat failující test (10 cases)**

Create `PmTracker.Tests.Unit/Sync/SyncScheduleCalculatorTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class SyncScheduleCalculatorTests
{
    private static DateTimeOffset Utc(int y, int mo, int d, int h = 0, int mi = 0)
        => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeNext_NowAfterAnchor_OneTickAhead()
    {
        var anchor = Utc(2026, 1, 1, 0, 0);
        var period = TimeSpan.FromHours(12);
        var now = Utc(2026, 1, 1, 8, 0);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 1, 12, 0));
    }

    [Fact]
    public void ComputeNext_NowBeforeAnchor_ReturnsAnchor()
    {
        var anchor = Utc(2026, 1, 1, 16, 0);
        var period = TimeSpan.FromHours(1);
        var now = Utc(2026, 1, 1, 14, 25);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(anchor);
    }

    [Fact]
    public void ComputeNext_NowAfterAnchor_AlignsToAnchorMinutes()
    {
        var anchor = Utc(2026, 1, 1, 14, 38);
        var period = TimeSpan.FromHours(1);
        var now = Utc(2026, 1, 1, 14, 30);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(anchor);
    }

    [Fact]
    public void ComputeNext_NowEqualsAnchor_ReturnsAnchor()
    {
        var anchor = Utc(2026, 1, 1, 12, 0);
        var period = TimeSpan.FromHours(1);

        var next = SyncScheduleCalculator.ComputeNext(anchor, anchor, period);

        next.Should().Be(anchor);
    }

    [Fact]
    public void ComputeNext_ZeroPeriod_ThrowsArgumentOutOfRange()
    {
        var anchor = Utc(2026, 1, 1);
        var now = Utc(2026, 1, 2);

        var act = () => SyncScheduleCalculator.ComputeNext(now, anchor, TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*period*");
    }

    [Fact]
    public void ComputeNext_NegativePeriod_ThrowsArgumentOutOfRange()
    {
        var anchor = Utc(2026, 1, 1);
        var now = Utc(2026, 1, 2);

        var act = () => SyncScheduleCalculator.ComputeNext(now, anchor, TimeSpan.FromHours(-1));

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*period*");
    }

    [Fact]
    public void ComputeNext_CrossesMidnight()
    {
        var anchor = Utc(2026, 1, 1, 23, 0);
        var period = TimeSpan.FromHours(2);
        var now = Utc(2026, 1, 2, 0, 30);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 2, 1, 0));
    }

    [Fact]
    public void ComputeNext_DstTransitionDay_UtcImmune()
    {
        var anchor = Utc(2026, 3, 28, 0, 0);
        var period = TimeSpan.FromHours(1);
        var now = Utc(2026, 3, 29, 2, 30);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 3, 29, 3, 0));
    }

    [Fact]
    public void ComputeNext_PeriodGreaterThan24h_WorksAcrossDays()
    {
        var anchor = Utc(2026, 1, 1, 0, 0);
        var period = TimeSpan.FromHours(48);
        var now = Utc(2026, 1, 2, 0, 0);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 3, 0, 0));
    }

    [Fact]
    public void ComputeNext_VeryLargePeriodSevenDays()
    {
        var anchor = Utc(2026, 1, 1, 0, 0);
        var period = TimeSpan.FromDays(7);
        var now = Utc(2026, 1, 5, 12, 0);

        var next = SyncScheduleCalculator.ComputeNext(now, anchor, period);

        next.Should().Be(Utc(2026, 1, 8, 0, 0));
    }
}
```

- [ ] **Step 2: Spustit testy (musí failnout — třída neexistuje)**

Run: `dotnet test PmTracker.Tests.Unit --filter "SyncScheduleCalculatorTests" --no-restore 2>&1 | tail -10`

Expected: `error CS0246: The type or namespace name 'SyncScheduleCalculator' could not be found`.

- [ ] **Step 3: Implementovat SyncScheduleCalculator**

Create `PmTracker.Web/Services/Sync/SyncScheduleCalculator.cs`:

```csharp
namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Pure static calculator: zarovnat další tick na anchor + celočíselný násobek periody.
/// </summary>
public static class SyncScheduleCalculator
{
    public static DateTimeOffset ComputeNext(DateTimeOffset now, DateTimeOffset anchorAt, TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "period must be > TimeSpan.Zero");
        }

        if (now <= anchorAt)
        {
            return anchorAt;
        }

        var elapsed = now - anchorAt;
        var ticks = (long)Math.Ceiling(elapsed.Ticks / (double)period.Ticks);
        return anchorAt + TimeSpan.FromTicks(period.Ticks * ticks);
    }
}
```

- [ ] **Step 4: Spustit testy (musí projít)**

Run: `dotnet test PmTracker.Tests.Unit --filter "SyncScheduleCalculatorTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 10, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Sync/SyncScheduleCalculator.cs \
        PmTracker.Tests.Unit/Sync/SyncScheduleCalculatorTests.cs
git commit -m "feat(sync): SyncScheduleCalculator — aligned schedule algoritmus + 10 TDD cases"
```

---

## Task 3: `SyncTriggerKind` enum + `ISyncJobSettings` interface

**Files:**
- Create: `PmTracker.Web/Services/Sync/SyncTriggerKind.cs`
- Create: `PmTracker.Web/Services/Sync/ISyncJobSettings.cs`

- [ ] **Step 1: Vytvořit SyncTriggerKind enum**

Create `PmTracker.Web/Services/Sync/SyncTriggerKind.cs`:

```csharp
namespace PmTracker.Web.Services.Sync;

public enum SyncTriggerKind
{
    Auto = 0,
    Manual = 1
}

public static class SyncTriggerKindExtensions
{
    public static string ToWireString(this SyncTriggerKind kind) => kind switch
    {
        SyncTriggerKind.Auto => "auto",
        SyncTriggerKind.Manual => "manual",
        _ => "auto"
    };

    public static SyncTriggerKind ParseWireString(string? value) => value switch
    {
        "manual" => SyncTriggerKind.Manual,
        _ => SyncTriggerKind.Auto
    };
}
```

- [ ] **Step 2: Vytvořit ISyncJobSettings interface**

Create `PmTracker.Web/Services/Sync/ISyncJobSettings.cs`:

```csharp
namespace PmTracker.Web.Services.Sync;

public interface ISyncJobSettings
{
    // Konfigurace
    bool IsEnabled { get; set; }
    int PeriodMinutes { get; set; }
    DateTimeOffset AnchorAt { get; set; }

    // Status posledního běhu
    DateTime? LastRunAt { get; set; }
    string? LastTriggerKind { get; set; }
    string? LastResultJson { get; set; }

    // Viditelnost běhu
    bool IsRunning { get; set; }
    DateTime? RunStartedAt { get; set; }

    // Audit
    DateTime UpdatedAt { get; set; }
    int? UpdatedByOsobaId { get; set; }
}
```

- [ ] **Step 3: Build ověří syntax**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/Sync/SyncTriggerKind.cs \
        PmTracker.Web/Services/Sync/ISyncJobSettings.cs
git commit -m "feat(sync): SyncTriggerKind enum + ISyncJobSettings interface"
```

---

## Task 4: `ISyncJobRunLock` + `SyncJobRunLock` + tests

**Files:**
- Create: `PmTracker.Web/Services/Sync/ISyncJobRunLock.cs`
- Create: `PmTracker.Web/Services/Sync/SyncJobRunLock.cs`
- Create: `PmTracker.Tests.Unit/Sync/SyncJobRunLockTests.cs`

- [ ] **Step 1: Napsat failující test**

Create `PmTracker.Tests.Unit/Sync/SyncJobRunLockTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class SyncJobRunLockTests
{
    private sealed class DummyJobA : ISyncJobSettings
    {
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

    [Fact]
    public void TryAcquire_FirstCall_ReturnsTrue()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        sut.TryAcquire().Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_SecondCallWithoutRelease_ReturnsFalse()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        sut.TryAcquire();
        sut.TryAcquire().Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_AfterRelease_ReturnsTrue()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        sut.TryAcquire();
        sut.Release();
        sut.TryAcquire().Should().BeTrue();
    }

    [Fact]
    public void Release_WithoutAcquire_DoesNotThrow()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        var act = () => sut.Release();

        act.Should().NotThrow();
    }
}
```

- [ ] **Step 2: Spustit test (fail — třídy neexistují)**

Run: `dotnet test PmTracker.Tests.Unit --filter "SyncJobRunLockTests" --no-restore 2>&1 | tail -10`

Expected: compilation error na `SyncJobRunLock`.

- [ ] **Step 3: Vytvořit interface + impl**

Create `PmTracker.Web/Services/Sync/ISyncJobRunLock.cs`:

```csharp
namespace PmTracker.Web.Services.Sync;

public interface ISyncJobRunLock<TSettings>
    where TSettings : class, ISyncJobSettings
{
    /// <summary>Pokus o zamčení bez čekání. True = získáno, False = už běží.</summary>
    bool TryAcquire();

    /// <summary>Uvolnění. Musí se volat v finally po úspěšném TryAcquire.</summary>
    void Release();
}
```

Create `PmTracker.Web/Services/Sync/SyncJobRunLock.cs`:

```csharp
namespace PmTracker.Web.Services.Sync;

public sealed class SyncJobRunLock<TSettings> : ISyncJobRunLock<TSettings>
    where TSettings : class, ISyncJobSettings
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool TryAcquire() => _semaphore.Wait(TimeSpan.Zero);

    public void Release()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Volání Release bez předchozího TryAcquire — silent no-op
        }
    }
}
```

- [ ] **Step 4: Spustit test (passes)**

Run: `dotnet test PmTracker.Tests.Unit --filter "SyncJobRunLockTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Sync/ISyncJobRunLock.cs \
        PmTracker.Web/Services/Sync/SyncJobRunLock.cs \
        PmTracker.Tests.Unit/Sync/SyncJobRunLockTests.cs
git commit -m "feat(sync): ISyncJobRunLock per-T SemaphoreSlim + 4 unit testy"
```

---

## Task 5: `IReactiveSyncQueue` + `ReactiveSyncQueue` + tests

**Files:**
- Create: `PmTracker.Web/Services/Sync/IReactiveSyncQueue.cs`
- Create: `PmTracker.Web/Services/Sync/ReactiveSyncQueue.cs`
- Create: `PmTracker.Tests.Unit/Sync/ReactiveSyncQueueTests.cs`

- [ ] **Step 1: Napsat failující test**

Create `PmTracker.Tests.Unit/Sync/ReactiveSyncQueueTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class ReactiveSyncQueueTests
{
    private sealed record FakeRequest(int Id);

    [Fact]
    public async Task EnqueueAsync_Then_ReadOneItem()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();

        await sut.EnqueueAsync(new FakeRequest(1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var item in sut.ReadAllAsync(cts.Token))
        {
            item.Id.Should().Be(1);
            break;
        }
    }

    [Fact]
    public async Task PendingCount_AfterEnqueue_ReportsCount()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();

        await sut.EnqueueAsync(new FakeRequest(1));
        await sut.EnqueueAsync(new FakeRequest(2));
        await sut.EnqueueAsync(new FakeRequest(3));

        sut.PendingCount.Should().Be(3);
    }

    [Fact]
    public async Task EnqueueAsync_PreservesFifoOrder()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();
        await sut.EnqueueAsync(new FakeRequest(1));
        await sut.EnqueueAsync(new FakeRequest(2));
        await sut.EnqueueAsync(new FakeRequest(3));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var collected = new List<int>();
        await foreach (var item in sut.ReadAllAsync(cts.Token))
        {
            collected.Add(item.Id);
            if (collected.Count == 3) break;
        }

        collected.Should().ContainInOrder(1, 2, 3);
    }
}
```

- [ ] **Step 2: Test fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "ReactiveSyncQueueTests" --no-restore 2>&1 | tail -10`

Expected: compilation error.

- [ ] **Step 3: Vytvořit interface + impl**

Create `PmTracker.Web/Services/Sync/IReactiveSyncQueue.cs`:

```csharp
namespace PmTracker.Web.Services.Sync;

public interface IReactiveSyncQueue<TRequest>
{
    ValueTask EnqueueAsync(TRequest request, CancellationToken ct = default);
    IAsyncEnumerable<TRequest> ReadAllAsync(CancellationToken ct = default);
    int PendingCount { get; }
}
```

Create `PmTracker.Web/Services/Sync/ReactiveSyncQueue.cs`:

```csharp
using System.Threading.Channels;

namespace PmTracker.Web.Services.Sync;

public sealed class ReactiveSyncQueue<TRequest> : IReactiveSyncQueue<TRequest>
{
    private const int DefaultCapacity = 1000;

    private readonly Channel<TRequest> _channel;

    public ReactiveSyncQueue()
    {
        _channel = Channel.CreateBounded<TRequest>(new BoundedChannelOptions(DefaultCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(TRequest request, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(request, ct);

    public IAsyncEnumerable<TRequest> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);

    public int PendingCount => _channel.Reader.Count;
}
```

- [ ] **Step 4: Test pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "ReactiveSyncQueueTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Sync/IReactiveSyncQueue.cs \
        PmTracker.Web/Services/Sync/ReactiveSyncQueue.cs \
        PmTracker.Tests.Unit/Sync/ReactiveSyncQueueTests.cs
git commit -m "feat(sync): IReactiveSyncQueue bounded Channel<T> FIFO + 3 unit testy"
```

---

## Task 6: `ReactiveSyncConsumerBase` abstract + tests

**Files:**
- Create: `PmTracker.Web/Services/Sync/ReactiveSyncConsumerBase.cs`
- Create: `PmTracker.Tests.Unit/Sync/ReactiveSyncConsumerBaseTests.cs`

- [ ] **Step 1: Napsat failující test**

Create `PmTracker.Tests.Unit/Sync/ReactiveSyncConsumerBaseTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class ReactiveSyncConsumerBaseTests
{
    private sealed record FakeRequest(int Id);

    private sealed class FakeConsumer : ReactiveSyncConsumerBase<FakeRequest>
    {
        public List<int> Handled { get; } = new();
        public TaskCompletionSource<bool> FirstHandled { get; } = new();

        public FakeConsumer(IReactiveSyncQueue<FakeRequest> q, IServiceScopeFactory sf)
            : base(q, sf, NullLogger<FakeConsumer>.Instance) { }

        protected override Task HandleAsync(IServiceScope scope, FakeRequest request, CancellationToken ct)
        {
            Handled.Add(request.Id);
            if (Handled.Count == 1) FirstHandled.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StartAsync_HandlesEnqueuedItems()
    {
        var queue = new ReactiveSyncQueue<FakeRequest>();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var consumer = new FakeConsumer(queue, scopeFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new FakeRequest(42), cts.Token);

        await consumer.FirstHandled.Task.WaitAsync(TimeSpan.FromSeconds(1), cts.Token);
        consumer.Handled.Should().ContainSingle().Which.Should().Be(42);

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ContinuesAfterHandlerException()
    {
        var queue = new ReactiveSyncQueue<FakeRequest>();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var consumer = new ThrowingConsumer(queue, scopeFactory);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.StartAsync(cts.Token);

        await queue.EnqueueAsync(new FakeRequest(1), cts.Token);  // throw
        await queue.EnqueueAsync(new FakeRequest(2), cts.Token);  // success

        await consumer.SecondHandled.Task.WaitAsync(TimeSpan.FromSeconds(1), cts.Token);
        consumer.Handled.Should().Equal(2);

        await consumer.StopAsync(CancellationToken.None);
    }

    private sealed class ThrowingConsumer : ReactiveSyncConsumerBase<FakeRequest>
    {
        public List<int> Handled { get; } = new();
        public TaskCompletionSource<bool> SecondHandled { get; } = new();

        public ThrowingConsumer(IReactiveSyncQueue<FakeRequest> q, IServiceScopeFactory sf)
            : base(q, sf, NullLogger<ThrowingConsumer>.Instance) { }

        protected override Task HandleAsync(IServiceScope scope, FakeRequest request, CancellationToken ct)
        {
            if (request.Id == 1) throw new InvalidOperationException("fail");
            Handled.Add(request.Id);
            if (Handled.Count == 1) SecondHandled.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Test fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "ReactiveSyncConsumerBaseTests" --no-restore 2>&1 | tail -10`

Expected: compile error.

- [ ] **Step 3: Implementovat ReactiveSyncConsumerBase**

Create `PmTracker.Web/Services/Sync/ReactiveSyncConsumerBase.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PmTracker.Web.Services.Sync;

public abstract class ReactiveSyncConsumerBase<TRequest> : BackgroundService
{
    private readonly IReactiveSyncQueue<TRequest> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected ReactiveSyncConsumerBase(
        IReactiveSyncQueue<TRequest> queue,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected abstract Task HandleAsync(IServiceScope scope, TRequest request, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _queue.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            using var scope = _scopeFactory.CreateScope();
            try
            {
                await HandleAsync(scope, request, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reactive sync consumer handler failed for request {Request}", request);
            }
        }
    }
}
```

- [ ] **Step 4: Test pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "ReactiveSyncConsumerBaseTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Sync/ReactiveSyncConsumerBase.cs \
        PmTracker.Tests.Unit/Sync/ReactiveSyncConsumerBaseTests.cs
git commit -m "feat(sync): ReactiveSyncConsumerBase abstract BackgroundService + 2 testy"
```

---

## Task 7: `SyncHostedServiceBase` + catch-up + tests

**Files:**
- Create: `PmTracker.Web/Services/Sync/SyncHostedServiceBase.cs`
- Create: `PmTracker.Tests.Unit/Sync/SyncHostedServiceBaseTests.cs`

- [ ] **Step 1: Napsat failující testy**

Create `PmTracker.Tests.Unit/Sync/SyncHostedServiceBaseTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PmTracker.Web.Data;
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
            TimeProvider timeProvider)
            : base(scopeFactory, runLock, timeProvider, NullLogger<FakeHostedService>.Instance)
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
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new FakeHostedService(
            settings, services.GetRequiredService<IServiceScopeFactory>(),
            new SyncJobRunLock<FakeSettings>(), time);

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
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new FakeHostedService(
            settings, services.GetRequiredService<IServiceScopeFactory>(),
            new SyncJobRunLock<FakeSettings>(), time);

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
        var services = new ServiceCollection().BuildServiceProvider();
        var sut = new FakeHostedService(
            settings, services.GetRequiredService<IServiceScopeFactory>(),
            new SyncJobRunLock<FakeSettings>(), time);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(200);    // čas na start
        sut.RunCount.Should().Be(0);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }
}
```

- [ ] **Step 2: Test fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "SyncHostedServiceBaseTests" --no-restore 2>&1 | tail -10`

Expected: compile error.

- [ ] **Step 3: Implementovat SyncHostedServiceBase**

Create `PmTracker.Web/Services/Sync/SyncHostedServiceBase.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PmTracker.Web.Services.Sync;

public abstract class SyncHostedServiceBase<TSettings> : BackgroundService
    where TSettings : class, ISyncJobSettings
{
    private static readonly TimeSpan DisabledCheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StaleRunGuard = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncJobRunLock<TSettings> _runLock;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    protected SyncHostedServiceBase(
        IServiceScopeFactory scopeFactory,
        ISyncJobRunLock<TSettings> runLock,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _runLock = runLock;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected abstract string JobKey { get; }
    protected abstract Task<TSettings> LoadSettingsAsync(IServiceScope scope, CancellationToken ct);
    protected abstract Task SaveSettingsAsync(IServiceScope scope, TSettings settings, CancellationToken ct);
    protected abstract Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool caughtUp = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            TSettings settings;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                settings = await LoadSettingsAsync(scope, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{JobKey}] načtení settings selhalo", JobKey);
                await SafeDelay(DisabledCheckInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (!settings.IsEnabled)
            {
                await SafeDelay(DisabledCheckInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (settings.PeriodMinutes <= 0)
            {
                _logger.LogWarning("[{JobKey}] PeriodMinutes <= 0, čekám 60s a zkusím znovu", JobKey);
                await SafeDelay(DisabledCheckInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var period = TimeSpan.FromMinutes(settings.PeriodMinutes);
            var now = _timeProvider.GetUtcNow();

            // Catch-up on startup (běží jen jednou, pak loop pokračuje normal)
            if (!caughtUp)
            {
                caughtUp = true;
                if (settings.LastRunAt is null ||
                    now.UtcDateTime - settings.LastRunAt.Value > period)
                {
                    await ExecuteTickAsync(SyncTriggerKind.Auto, stoppingToken).ConfigureAwait(false);
                    continue;  // reload settings po běhu
                }
            }

            var next = SyncScheduleCalculator.ComputeNext(now, settings.AnchorAt, period);
            var delay = next - now;
            if (delay <= TimeSpan.Zero)
            {
                await ExecuteTickAsync(SyncTriggerKind.Auto, stoppingToken).ConfigureAwait(false);
                continue;
            }

            await SafeDelay(delay, stoppingToken).ConfigureAwait(false);
            if (stoppingToken.IsCancellationRequested) break;
            await ExecuteTickAsync(SyncTriggerKind.Auto, stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Orchestrace jednoho běhu: semafor, IsRunning flag, RunOnceAsync, SaveSettings s LastResultJson.
    /// Pokud semafor je zabraný → skip (log info).
    /// </summary>
    public async Task<bool> ExecuteTickAsync(SyncTriggerKind trigger, CancellationToken ct)
    {
        if (!_runLock.TryAcquire())
        {
            _logger.LogInformation("[{JobKey}] tick {Trigger} skipped — už běží", JobKey, trigger);
            return false;
        }

        var startedAt = _timeProvider.GetUtcNow().UtcDateTime;
        using var scope = _scopeFactory.CreateScope();
        TSettings settings;
        try
        {
            settings = await LoadSettingsAsync(scope, ct).ConfigureAwait(false);

            // Stale IsRunning flag recovery: pokud app spadla uprostřed předchozího běhu
            if (settings.IsRunning && settings.RunStartedAt is not null &&
                startedAt - settings.RunStartedAt.Value > StaleRunGuard)
            {
                _logger.LogWarning("[{JobKey}] clearing stale IsRunning flag (>{Min}min)", JobKey, StaleRunGuard.TotalMinutes);
            }

            settings.IsRunning = true;
            settings.RunStartedAt = startedAt;
            settings.LastTriggerKind = trigger.ToWireString();
            settings.UpdatedAt = startedAt;
            await SaveSettingsAsync(scope, settings, ct).ConfigureAwait(false);

            object result;
            try
            {
                result = await RunOnceAsync(scope, trigger, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{JobKey}] RunOnceAsync vyhodil výjimku", JobKey);
                result = new { error = ex.GetType().Name, message = ex.Message };
            }

            var finishedAt = _timeProvider.GetUtcNow().UtcDateTime;
            settings.IsRunning = false;
            settings.RunStartedAt = null;
            settings.LastRunAt = finishedAt;
            settings.LastResultJson = JsonSerializer.Serialize(result);
            settings.UpdatedAt = finishedAt;
            await SaveSettingsAsync(scope, settings, ct).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task SafeDelay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* stopping */ }
    }
}
```

- [ ] **Step 4: Test pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "SyncHostedServiceBaseTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Sync/SyncHostedServiceBase.cs \
        PmTracker.Tests.Unit/Sync/SyncHostedServiceBaseTests.cs
git commit -m "feat(sync): SyncHostedServiceBase generic loop + catch-up + 3 integrační testy"
```

---

## Task 8: DB upgrade skript + aplikace na Dev DB

**Files:**
- Create: `db_upgrade_1_3_0_ad_sync_settings.sql`

- [ ] **Step 1: Napsat skript**

Create `db_upgrade_1_3_0_ad_sync_settings.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_3_0_ad_sync_settings.sql
-- Singleton settings row pro AD periodic sync job.
-- Spec: docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md §3.1
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ad_sync_settings')
BEGIN
    CREATE TABLE dbo.ad_sync_settings
    (
        id                   INT NOT NULL
            CONSTRAINT PK_ad_sync_settings PRIMARY KEY,
        is_enabled           BIT NOT NULL
            CONSTRAINT DF_ad_sync_settings_enabled DEFAULT (0),
        period_minutes       INT NOT NULL
            CONSTRAINT DF_ad_sync_settings_period DEFAULT (360),
        anchor_at            DATETIMEOFFSET NOT NULL,
        last_run_at          DATETIME2 NULL,
        last_trigger_kind    NVARCHAR(16) NULL,
        last_result_json     NVARCHAR(MAX) NULL,
        is_running           BIT NOT NULL
            CONSTRAINT DF_ad_sync_settings_running DEFAULT (0),
        run_started_at       DATETIME2 NULL,
        updated_at           DATETIME2 NOT NULL
            CONSTRAINT DF_ad_sync_settings_updated DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id  INT NULL
            CONSTRAINT FK_ad_sync_settings_osoba REFERENCES dbo.osoby(id)
    );

    PRINT N'Tabulka ad_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka ad_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

-- Seed singleton row (idempotentní)
IF NOT EXISTS (SELECT 1 FROM dbo.ad_sync_settings WHERE id = 1)
BEGIN
    INSERT INTO dbo.ad_sync_settings (id, is_enabled, period_minutes, anchor_at)
    VALUES (1, 0, 360, SYSUTCDATETIMEOFFSET());
    PRINT N'Seed výchozího řádku ad_sync_settings id=1 vložen.';
END
ELSE
BEGIN
    PRINT N'Seed řádek ad_sync_settings id=1 už existuje, přeskakuji INSERT.';
END;
GO
```

- [ ] **Step 2: Spustit skript proti Dev DB**

Run:
```bash
cd /tmp/run-sql && dotnet run "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_3_0_ad_sync_settings.sql"
```

Expected: dvě `PRINT` zprávy. Opakované spuštění → obě zprávy „už existuje, přeskakuji".

- [ ] **Step 3: Ověřit obsah**

Create `/tmp/verify-ad-sync.sql`:
```sql
SELECT id, is_enabled, period_minutes, anchor_at, last_run_at
FROM dbo.ad_sync_settings;
```

Run:
```bash
cd /tmp/run-sql && dotnet run /tmp/verify-ad-sync.sql
```

Expected: 1 řádek (id=1, is_enabled=0, period_minutes=360, anchor_at=now, last_run_at=NULL).

- [ ] **Step 4: Commit**

```bash
git add db_upgrade_1_3_0_ad_sync_settings.sql
git commit -m "feat(db): db_upgrade_1_3_0 — ad_sync_settings singleton tabulka + seed"
```

---

## Task 9: `AdSyncSettingsEntity` + EF config + DbContext

**Files:**
- Create: `PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs`
- Create: `PmTracker.Web/Data/Configuration/AdSyncSettingsEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`

- [ ] **Step 1: Napsat failující test**

Create `PmTracker.Tests.Unit/Sync/AdSyncSettingsEntityTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class AdSyncSettingsEntityTests
{
    [Fact]
    public void Entity_ImplementsISyncJobSettings()
    {
        var e = new AdSyncSettingsEntity
        {
            Id = 1,
            IsEnabled = true,
            PeriodMinutes = 360,
            AnchorAt = new DateTimeOffset(2026, 4, 22, 3, 0, 0, TimeSpan.Zero)
        };

        ISyncJobSettings iface = e;
        iface.IsEnabled.Should().BeTrue();
        iface.PeriodMinutes.Should().Be(360);
        iface.AnchorAt.Hour.Should().Be(3);
    }
}
```

- [ ] **Step 2: Test fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "AdSyncSettingsEntityTests" --no-restore 2>&1 | tail -10`

Expected: compile error.

- [ ] **Step 3: Vytvořit entity**

Create `PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs`:

```csharp
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Models.Entities;

public sealed class AdSyncSettingsEntity : ISyncJobSettings
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; } = 360;
    public DateTimeOffset AnchorAt { get; set; }

    public DateTime? LastRunAt { get; set; }
    public string? LastTriggerKind { get; set; }
    public string? LastResultJson { get; set; }

    public bool IsRunning { get; set; }
    public DateTime? RunStartedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByOsobaId { get; set; }
}
```

- [ ] **Step 4: Vytvořit EF config**

Create `PmTracker.Web/Data/Configuration/AdSyncSettingsEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class AdSyncSettingsEntityConfiguration
    : IEntityTypeConfiguration<AdSyncSettingsEntity>
{
    public void Configure(EntityTypeBuilder<AdSyncSettingsEntity> builder)
    {
        builder.ToTable("ad_sync_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled");
        builder.Property(x => x.PeriodMinutes).HasColumnName("period_minutes");
        builder.Property(x => x.AnchorAt).HasColumnName("anchor_at");
        builder.Property(x => x.LastRunAt).HasColumnName("last_run_at");
        builder.Property(x => x.LastTriggerKind).HasColumnName("last_trigger_kind").HasMaxLength(16);
        builder.Property(x => x.LastResultJson).HasColumnName("last_result_json");
        builder.Property(x => x.IsRunning).HasColumnName("is_running");
        builder.Property(x => x.RunStartedAt).HasColumnName("run_started_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedByOsobaId).HasColumnName("updated_by_osoba_id");
    }
}
```

- [ ] **Step 5: Přidat DbSet do DbContext**

Otevři [PmTracker.Web/Data/PmTrackerDbContext.cs](PmTracker.Web/Data/PmTrackerDbContext.cs) — najdi blok s `DbSet<...>` property a přidej:

```csharp
public DbSet<AdSyncSettingsEntity> AdSyncSettings => Set<AdSyncSettingsEntity>();
```

Pokud `OnModelCreating` neobsahuje `ApplyConfigurationsFromAssembly`, přidej explicitně:

```csharp
modelBuilder.ApplyConfiguration(new AdSyncSettingsEntityConfiguration());
```

- [ ] **Step 6: Build + test pass**

Run: `dotnet build PmTracker.Web --no-restore && dotnet test PmTracker.Tests.Unit --filter "AdSyncSettingsEntityTests" --no-restore 2>&1 | tail -5`

Expected: `Passed: 1`.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs \
        PmTracker.Web/Data/Configuration/AdSyncSettingsEntityConfiguration.cs \
        PmTracker.Web/Data/PmTrackerDbContext.cs \
        PmTracker.Tests.Unit/Sync/AdSyncSettingsEntityTests.cs
git commit -m "feat(ad-sync): AdSyncSettingsEntity + EF config + DbContext registrace"
```

---

## Task 10: `IActiveDirectoryService.ListByGuidsAsync` + `ActiveDirectoryBatchResponse`

**Files:**
- Modify: `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryModels.cs`
- Modify: `PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs`
- Modify: `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryService.cs`
- Modify: `PmTracker.Web/Services/ActiveDirectory/ADConnector.cs` (pokud potřeba pro GUID dotaz)

- [ ] **Step 1: Rozšířit ActiveDirectoryModels**

Append to `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryModels.cs`:

```csharp
public sealed class ActiveDirectoryBatchResponse
{
    public bool Available { get; init; }
    public string? Message { get; init; }
    public required IReadOnlyList<ActiveDirectoryPersonResult> Persons { get; init; }
    public required IReadOnlyList<Guid> NotFoundGuids { get; init; }
}
```

- [ ] **Step 2: Rozšířit interface**

Modify `PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs`:

```csharp
namespace PmTracker.Web.Services.ActiveDirectory;

public interface IActiveDirectoryService
{
    Task<ActiveDirectorySearchResponse> SearchUsersAsync(string? query, CancellationToken ct = default);

    /// <summary>
    /// Načte osoby z AD podle seznamu GuidAd. Interně rozděluje na batches po 100.
    /// </summary>
    Task<ActiveDirectoryBatchResponse> ListByGuidsAsync(
        IReadOnlyCollection<Guid> guids,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Implementace v ActiveDirectoryService**

Modify `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryService.cs` — přidat metodu na konec třídy před `private static` helpery:

```csharp
public async Task<ActiveDirectoryBatchResponse> ListByGuidsAsync(
    IReadOnlyCollection<Guid> guids,
    CancellationToken ct = default)
{
    if (guids is null || guids.Count == 0)
    {
        return new ActiveDirectoryBatchResponse
        {
            Available = true,
            Persons = Array.Empty<ActiveDirectoryPersonResult>(),
            NotFoundGuids = Array.Empty<Guid>()
        };
    }

    if (!OperatingSystem.IsWindows())
    {
        _logger.LogInformation("AD ListByGuidsAsync is not available on non-Windows platform.");
        return new ActiveDirectoryBatchResponse
        {
            Available = false,
            Message = UnavailableMessage(_options.Domain),
            Persons = Array.Empty<ActiveDirectoryPersonResult>(),
            NotFoundGuids = guids.ToArray()
        };
    }

    var timeoutSeconds = Math.Clamp(_options.QueryTimeoutSeconds, 2, 60);
    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

    var persons = new List<ActiveDirectoryPersonResult>();
    var found = new HashSet<Guid>();

    try
    {
        foreach (var chunk in Chunk(guids, 100))
        {
#pragma warning disable CA1416
            var chunkResults = await Task.Run(
                () => ADConnector.GetADUsersByGuids(chunk, _options.Domain ?? string.Empty),
                timeoutCts.Token).ConfigureAwait(false);
#pragma warning restore CA1416

            foreach (var ad in chunkResults)
            {
                var mapped = MapAdInfoToResult(ad);
                if (mapped is not null)
                {
                    persons.Add(mapped);
                    found.Add(ad.GuidAd);
                }
            }
        }

        var notFound = guids.Where(g => !found.Contains(g)).ToArray();
        return new ActiveDirectoryBatchResponse
        {
            Available = true,
            Persons = persons,
            NotFoundGuids = notFound
        };
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
    {
        _logger.LogWarning("AD ListByGuidsAsync timeout.");
        return new ActiveDirectoryBatchResponse
        {
            Available = false,
            Message = UnavailableMessage(_options.Domain),
            Persons = persons,
            NotFoundGuids = guids.Where(g => !found.Contains(g)).ToArray()
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "AD ListByGuidsAsync unexpected error.");
        return new ActiveDirectoryBatchResponse
        {
            Available = false,
            Message = UnavailableMessage(_options.Domain),
            Persons = persons,
            NotFoundGuids = guids.Where(g => !found.Contains(g)).ToArray()
        };
    }
}

private static IEnumerable<IReadOnlyList<Guid>> Chunk(IReadOnlyCollection<Guid> source, int size)
{
    var batch = new List<Guid>(size);
    foreach (var g in source)
    {
        batch.Add(g);
        if (batch.Count == size) { yield return batch; batch = new List<Guid>(size); }
    }
    if (batch.Count > 0) yield return batch;
}

private static ActiveDirectoryPersonResult? MapAdInfoToResult(ADInfo user)
{
    if (user.GuidAd == Guid.Empty) return null;
    var jmeno = user.FirstName?.Trim() ?? string.Empty;
    var prijmeni = user.Surname?.Trim() ?? string.Empty;
    var email = user.Mail?.Trim() ?? string.Empty;
    var displayName = !string.IsNullOrWhiteSpace(user.DisplayName)
        ? user.DisplayName.Trim()
        : string.Join(' ', new[] { jmeno, prijmeni }.Where(x => !string.IsNullOrWhiteSpace(x)));

    return new ActiveDirectoryPersonResult
    {
        GuidAd = user.GuidAd,
        AdLogin = string.IsNullOrWhiteSpace(user.Login) ? null : user.Login.Trim(),
        DisplayName = displayName,
        Jmeno = jmeno,
        Prijmeni = prijmeni,
        Titul = string.IsNullOrWhiteSpace(user.Titul) ? null : user.Titul.Trim(),
        Email = email,
        Company = string.IsNullOrWhiteSpace(user.Company) ? null : user.Company.Trim(),
        Department = string.IsNullOrWhiteSpace(user.Department) ? null : user.Department.Trim(),
        CanSelect = !string.IsNullOrWhiteSpace(email),
        DisabledReason = null
    };
}
```

- [ ] **Step 4: Doplnit ADConnector**

Open `PmTracker.Web/Services/ActiveDirectory/ADConnector.cs` a přidej metodu `GetADUsersByGuids` (struktura podobná existující `GetADUsers`, ale filter přes objectGUID). Exaktní tvar závisí na současné implementaci.

Pokud současná `GetADUsers` používá `PrincipalSearcher` s `UserPrincipal` template, přidej:

```csharp
#if WINDOWS
using System.DirectoryServices.AccountManagement;

public static IReadOnlyList<ADInfo> GetADUsersByGuids(
    IReadOnlyCollection<Guid> guids, string domain)
{
    if (guids.Count == 0) return Array.Empty<ADInfo>();

    using var ctx = string.IsNullOrWhiteSpace(domain)
        ? new PrincipalContext(ContextType.Domain)
        : new PrincipalContext(ContextType.Domain, domain);

    var results = new List<ADInfo>(guids.Count);
    foreach (var g in guids)
    {
        try
        {
            var user = UserPrincipal.FindByIdentity(ctx, IdentityType.Guid, g.ToString());
            if (user is null) continue;
            results.Add(MapUserPrincipalToADInfo(user));
        }
        catch (Exception)
        {
            // ignore jednotlivé selhání, next guid
        }
    }
    return results;
}
#endif
```

(Pokud existuje `MapUserPrincipalToADInfo`, reuse; jinak vytvoř helper dle existující `GetADUsers` logiky.)

- [ ] **Step 5: Build ověří**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ActiveDirectory/
git commit -m "feat(ad): IActiveDirectoryService.ListByGuidsAsync + ActiveDirectoryBatchResponse"
```

---

## Task 11: AD sync models

**Files:**
- Create: `PmTracker.Web/Services/ActiveDirectory/AdSyncModels.cs`

- [ ] **Step 1: Vytvořit model soubor**

Create `PmTracker.Web/Services/ActiveDirectory/AdSyncModels.cs`:

```csharp
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed record AdSyncResult(
    DateTime StartedAt,
    DateTime FinishedAt,
    long DurationMs,
    int OkCount,
    int ErrorCount,
    int SkippedNoGuidCount,
    int NotFoundInAdCount,
    IReadOnlyList<AdSyncErrorItem> Errors)
{
    public static AdSyncResult Empty(DateTime startedAt)
        => new(startedAt, startedAt, 0, 0, 0, 0, 0, Array.Empty<AdSyncErrorItem>());
}

public sealed record AdSyncErrorItem(int OsobaId, Guid? GuidAd, string Reason);

public sealed record AdSinglePersonSyncResult(
    bool Success,
    bool FoundInAd,
    string? FailureReason);

public sealed record AdReactiveSyncRequest(int OsobaId, AdReactiveSource Source);

public enum AdReactiveSource { PersonPicked = 0, ManualUpdate = 1 }
```

- [ ] **Step 2: Build ověří**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Services/ActiveDirectory/AdSyncModels.cs
git commit -m "feat(ad-sync): modely AdSyncResult, AdReactiveSyncRequest, AdReactiveSource"
```

---

## Task 12: `IAdSyncService` + `AdSyncService` + unit testy

**Files:**
- Create: `PmTracker.Web/Services/ActiveDirectory/IAdSyncService.cs`
- Create: `PmTracker.Web/Services/ActiveDirectory/AdSyncService.cs`
- Create: `PmTracker.Tests.Unit/ActiveDirectory/AdSyncServiceTests.cs`

- [ ] **Step 1: Napsat failující testy**

Create `PmTracker.Tests.Unit/ActiveDirectory/AdSyncServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ActiveDirectory;

public sealed class AdSyncServiceTests
{
    private static PmTrackerDbContext NewInMemory()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("ad-sync-" + Guid.NewGuid())
            .Options;
        return new PmTrackerDbContext(options);
    }

    private static OsobaEntity PersonWithGuid(int id, Guid guid, string email = "old@example.com")
        => new()
        {
            Id = id,
            Jmeno = "Old",
            Prijmeni = "Name",
            Email = email,
            GuidAd = guid,
            OrganizaceId = 1
        };

    [Fact]
    public async Task SyncAllPeopleAsync_UpdatesPeopleWithGuidAd()
    {
        await using var db = NewInMemory();
        var guid = Guid.NewGuid();
        db.Osoby.Add(PersonWithGuid(1, guid));
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = new[] { new ActiveDirectoryPersonResult
              {
                  GuidAd = guid,
                  DisplayName = "New Name",
                  Jmeno = "New",
                  Prijmeni = "Surname",
                  Email = "new@example.com",
                  Company = "Acme",
                  Department = "IT",
                  CanSelect = true
              } },
              NotFoundGuids = Array.Empty<Guid>()
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncAllPeopleAsync(SyncTriggerKind.Auto, CancellationToken.None);

        result.OkCount.Should().Be(1);
        result.ErrorCount.Should().Be(0);
        result.NotFoundInAdCount.Should().Be(0);

        var updated = await db.Osoby.FirstAsync(x => x.Id == 1);
        updated.Jmeno.Should().Be("New");
        updated.Prijmeni.Should().Be("Surname");
        updated.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task SyncAllPeopleAsync_SkipsPeopleWithoutGuidAd()
    {
        await using var db = NewInMemory();
        db.Osoby.Add(new OsobaEntity
        {
            Id = 2, Jmeno = "Skip", Prijmeni = "Me", OrganizaceId = 1, GuidAd = null
        });
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = Array.Empty<ActiveDirectoryPersonResult>(),
              NotFoundGuids = Array.Empty<Guid>()
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncAllPeopleAsync(SyncTriggerKind.Auto, CancellationToken.None);

        result.SkippedNoGuidCount.Should().Be(1);
        result.OkCount.Should().Be(0);
    }

    [Fact]
    public async Task SyncAllPeopleAsync_RecordsNotFoundInAdWithoutDeletingPerson()
    {
        await using var db = NewInMemory();
        var guid = Guid.NewGuid();
        db.Osoby.Add(PersonWithGuid(3, guid, "keep@example.com"));
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = Array.Empty<ActiveDirectoryPersonResult>(),
              NotFoundGuids = new[] { guid }
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncAllPeopleAsync(SyncTriggerKind.Auto, CancellationToken.None);

        result.NotFoundInAdCount.Should().Be(1);

        var osoba = await db.Osoby.FirstAsync(x => x.Id == 3);
        osoba.Email.Should().Be("keep@example.com");  // not mutated
    }

    [Fact]
    public async Task SyncSinglePersonAsync_ReturnsFailureWhenOsobaHasNoGuidAd()
    {
        await using var db = NewInMemory();
        db.Osoby.Add(new OsobaEntity { Id = 4, Jmeno = "X", Prijmeni = "Y", OrganizaceId = 1 });
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncSinglePersonAsync(4, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("GuidAd");
    }

    [Fact]
    public async Task SyncSinglePersonAsync_ReturnsNotFoundWhenAdLacksGuid()
    {
        await using var db = NewInMemory();
        var guid = Guid.NewGuid();
        db.Osoby.Add(PersonWithGuid(5, guid));
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = Array.Empty<ActiveDirectoryPersonResult>(),
              NotFoundGuids = new[] { guid }
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncSinglePersonAsync(5, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FoundInAd.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Spustit testy (fail)**

Run: `dotnet test PmTracker.Tests.Unit --filter "AdSyncServiceTests" --no-restore 2>&1 | tail -10`

Expected: compile error (AdSyncService neexistuje).

- [ ] **Step 3: Vytvořit interface**

Create `PmTracker.Web/Services/ActiveDirectory/IAdSyncService.cs`:

```csharp
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public interface IAdSyncService
{
    Task<AdSyncResult> SyncAllPeopleAsync(SyncTriggerKind trigger, CancellationToken ct = default);
    Task<AdSinglePersonSyncResult> SyncSinglePersonAsync(int osobaId, CancellationToken ct = default);
}
```

- [ ] **Step 4: Vytvořit implementaci**

Create `PmTracker.Web/Services/ActiveDirectory/AdSyncService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdSyncService : IAdSyncService
{
    private const int MaxErrorsStored = 10;

    private readonly PmTrackerDbContext _db;
    private readonly IActiveDirectoryService _ad;
    private readonly ILogger<AdSyncService> _logger;
    private readonly TimeProvider _time;

    public AdSyncService(
        PmTrackerDbContext db,
        IActiveDirectoryService ad,
        ILogger<AdSyncService> logger,
        TimeProvider time)
    {
        _db = db;
        _ad = ad;
        _logger = logger;
        _time = time;
    }

    public async Task<AdSyncResult> SyncAllPeopleAsync(SyncTriggerKind trigger, CancellationToken ct = default)
    {
        var startedAt = _time.GetUtcNow().UtcDateTime;

        var osoby = await _db.Osoby.ToListAsync(ct).ConfigureAwait(false);
        var skippedNoGuid = osoby.Count(o => o.GuidAd is null);
        var withGuids = osoby.Where(o => o.GuidAd is not null).ToList();

        var guids = withGuids.Select(o => o.GuidAd!.Value).Distinct().ToArray();
        if (guids.Length == 0)
        {
            var finishedEmpty = _time.GetUtcNow().UtcDateTime;
            return new AdSyncResult(
                startedAt, finishedEmpty, (long)(finishedEmpty - startedAt).TotalMilliseconds,
                0, 0, skippedNoGuid, 0, Array.Empty<AdSyncErrorItem>());
        }

        var errors = new List<AdSyncErrorItem>();
        int okCount = 0;
        int notFound = 0;

        try
        {
            var response = await _ad.ListByGuidsAsync(guids, ct).ConfigureAwait(false);
            if (!response.Available)
            {
                errors.Add(new AdSyncErrorItem(0, null, response.Message ?? "AD unavailable"));
            }
            else
            {
                var byGuid = response.Persons.ToDictionary(p => p.GuidAd);
                foreach (var osoba in withGuids)
                {
                    if (!byGuid.TryGetValue(osoba.GuidAd!.Value, out var ad)) continue;
                    try
                    {
                        UpdateOsobaFromAd(osoba, ad);
                        okCount++;
                    }
                    catch (Exception ex)
                    {
                        if (errors.Count < MaxErrorsStored)
                            errors.Add(new AdSyncErrorItem(osoba.Id, osoba.GuidAd, ex.Message));
                    }
                }

                notFound = response.NotFoundGuids.Count;
            }

            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AD sync SyncAllPeopleAsync fatal");
            if (errors.Count < MaxErrorsStored)
                errors.Add(new AdSyncErrorItem(0, null, ex.Message));
        }

        var finishedAt = _time.GetUtcNow().UtcDateTime;
        return new AdSyncResult(
            startedAt, finishedAt, (long)(finishedAt - startedAt).TotalMilliseconds,
            okCount, errors.Count, skippedNoGuid, notFound, errors);
    }

    public async Task<AdSinglePersonSyncResult> SyncSinglePersonAsync(int osobaId, CancellationToken ct = default)
    {
        var osoba = await _db.Osoby.FirstOrDefaultAsync(x => x.Id == osobaId, ct).ConfigureAwait(false);
        if (osoba is null)
            return new AdSinglePersonSyncResult(false, false, "Osoba neexistuje");
        if (osoba.GuidAd is null)
            return new AdSinglePersonSyncResult(false, false, "Osoba nemá GuidAd");

        var response = await _ad.ListByGuidsAsync(new[] { osoba.GuidAd.Value }, ct).ConfigureAwait(false);
        if (!response.Available)
            return new AdSinglePersonSyncResult(false, false, response.Message ?? "AD unavailable");

        var adPerson = response.Persons.FirstOrDefault();
        if (adPerson is null)
            return new AdSinglePersonSyncResult(false, false, "Osoba nenalezena v AD");

        UpdateOsobaFromAd(osoba, adPerson);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new AdSinglePersonSyncResult(true, true, null);
    }

    private static void UpdateOsobaFromAd(OsobaEntity osoba, ActiveDirectoryPersonResult ad)
    {
        osoba.Jmeno = ad.Jmeno;
        osoba.Prijmeni = ad.Prijmeni;
        osoba.Titul = ad.Titul;
        if (!string.IsNullOrWhiteSpace(ad.Email))
            osoba.Email = ad.Email;
        // Company / Department nejsou na OsobaEntity — přeskočit, dokud nebude sloupec
        // AdLogin se neaktualizuje z AD (je to původní identifikátor)
    }
}
```

- [ ] **Step 5: Test pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "AdSyncServiceTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ActiveDirectory/IAdSyncService.cs \
        PmTracker.Web/Services/ActiveDirectory/AdSyncService.cs \
        PmTracker.Tests.Unit/ActiveDirectory/AdSyncServiceTests.cs
git commit -m "feat(ad-sync): IAdSyncService + impl + 5 unit testů"
```

---

## Task 13: `AdPeriodicSyncHostedService` + DI + integration test

**Files:**
- Create: `PmTracker.Web/Services/ActiveDirectory/AdPeriodicSyncHostedService.cs`
- Modify: `PmTracker.Web/Program.cs`
- Create: `PmTracker.Tests.Unit/ActiveDirectory/AdPeriodicSyncHostedServiceTests.cs`

- [ ] **Step 1: Vytvořit hosted service**

Create `PmTracker.Web/Services/ActiveDirectory/AdPeriodicSyncHostedService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdPeriodicSyncHostedService : SyncHostedServiceBase<AdSyncSettingsEntity>
{
    public AdPeriodicSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ISyncJobRunLock<AdSyncSettingsEntity> runLock,
        TimeProvider timeProvider,
        ILogger<AdPeriodicSyncHostedService> logger)
        : base(scopeFactory, runLock, timeProvider, logger) { }

    protected override string JobKey => "ad.periodic";

    protected override async Task<AdSyncSettingsEntity> LoadSettingsAsync(IServiceScope scope, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        return await db.AdSyncSettings.FirstAsync(x => x.Id == 1, ct).ConfigureAwait(false);
    }

    protected override async Task SaveSettingsAsync(IServiceScope scope, AdSyncSettingsEntity settings, CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        db.Entry(settings).State = EntityState.Modified;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    protected override async Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IAdSyncService>();
        return await svc.SyncAllPeopleAsync(trigger, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Napsat integration test**

Create `PmTracker.Tests.Unit/ActiveDirectory/AdPeriodicSyncHostedServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ActiveDirectory;

public sealed class AdPeriodicSyncHostedServiceTests
{
    private static (IServiceProvider provider, PmTrackerDbContext db) BuildProvider(
        IActiveDirectoryService adStub, TimeProvider time)
    {
        var services = new ServiceCollection();
        services.AddSingleton(time);
        services.AddDbContext<PmTrackerDbContext>(o => o.UseInMemoryDatabase("ad-periodic-" + Guid.NewGuid()));
        services.AddScoped<IActiveDirectoryService>(_ => adStub);
        services.AddScoped<IAdSyncService, AdSyncService>();
        services.AddSingleton(typeof(ISyncJobRunLock<>), typeof(SyncJobRunLock<>));
        services.AddLogging();
        var sp = services.BuildServiceProvider();
        return (sp, sp.CreateScope().ServiceProvider.GetRequiredService<PmTrackerDbContext>());
    }

    [Fact]
    public async Task ExecuteTickAsync_WritesLastRunAtAndLastResultJson()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);

        var adMock = new Mock<IActiveDirectoryService>();
        adMock.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ActiveDirectoryBatchResponse
              {
                  Available = true,
                  Persons = Array.Empty<ActiveDirectoryPersonResult>(),
                  NotFoundGuids = Array.Empty<Guid>()
              });

        var (sp, db) = BuildProvider(adMock.Object, time);
        db.AdSyncSettings.Add(new AdSyncSettingsEntity
        {
            Id = 1,
            IsEnabled = true,
            PeriodMinutes = 60,
            AnchorAt = fakeNow.AddHours(-1),
            LastRunAt = null
        });
        await db.SaveChangesAsync();

        var sut = new AdPeriodicSyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ISyncJobRunLock<AdSyncSettingsEntity>>(),
            time,
            NullLogger<AdPeriodicSyncHostedService>.Instance);

        await sut.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None);

        var after = await db.AdSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1);
        after.LastRunAt.Should().NotBeNull();
        after.LastTriggerKind.Should().Be("manual");
        after.LastResultJson.Should().NotBeNullOrWhiteSpace();
        after.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteTickAsync_ReturnsFalseWhenAlreadyRunning()
    {
        var time = new FakeTimeProvider();
        var adMock = new Mock<IActiveDirectoryService>();
        var (sp, db) = BuildProvider(adMock.Object, time);
        db.AdSyncSettings.Add(new AdSyncSettingsEntity { Id = 1, IsEnabled = true, PeriodMinutes = 60, AnchorAt = time.GetUtcNow() });
        await db.SaveChangesAsync();

        var runLock = sp.GetRequiredService<ISyncJobRunLock<AdSyncSettingsEntity>>();
        runLock.TryAcquire().Should().BeTrue();
        try
        {
            var sut = new AdPeriodicSyncHostedService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                runLock,
                time,
                NullLogger<AdPeriodicSyncHostedService>.Instance);

            var outcome = await sut.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None);

            outcome.Should().BeFalse();
        }
        finally
        {
            runLock.Release();
        }
    }
}
```

- [ ] **Step 3: Test run (green)**

Run: `dotnet test PmTracker.Tests.Unit --filter "AdPeriodicSyncHostedServiceTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 2`.

- [ ] **Step 4: DI registrace v Program.cs**

Open `PmTracker.Web/Program.cs`. Najdi sekci registrace služeb (kolem existujícího `builder.Services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();`).

Přidej:

```csharp
// Shared sync infrastructure
builder.Services.AddSingleton(typeof(ISyncJobRunLock<>), typeof(SyncJobRunLock<>));
builder.Services.AddSingleton(typeof(IReactiveSyncQueue<>), typeof(ReactiveSyncQueue<>));

// AD sync
builder.Services.AddScoped<IAdSyncService, AdSyncService>();
builder.Services.AddHostedService<AdPeriodicSyncHostedService>();
```

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ActiveDirectory/AdPeriodicSyncHostedService.cs \
        PmTracker.Web/Program.cs \
        PmTracker.Tests.Unit/ActiveDirectory/AdPeriodicSyncHostedServiceTests.cs
git commit -m "feat(ad-sync): AdPeriodicSyncHostedService + DI + 2 integrační testy"
```

---

## Task 14: `AdReactiveSyncConsumer` + integration test

**Files:**
- Create: `PmTracker.Web/Services/ActiveDirectory/AdReactiveSyncConsumer.cs`
- Modify: `PmTracker.Web/Program.cs`
- Create: `PmTracker.Tests.Unit/ActiveDirectory/AdReactiveSyncConsumerTests.cs`

- [ ] **Step 1: Vytvořit consumer**

Create `PmTracker.Web/Services/ActiveDirectory/AdReactiveSyncConsumer.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdReactiveSyncConsumer : ReactiveSyncConsumerBase<AdReactiveSyncRequest>
{
    public AdReactiveSyncConsumer(
        IReactiveSyncQueue<AdReactiveSyncRequest> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AdReactiveSyncConsumer> logger)
        : base(queue, scopeFactory, logger) { }

    protected override async Task HandleAsync(IServiceScope scope, AdReactiveSyncRequest request, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IAdSyncService>();
        var result = await svc.SyncSinglePersonAsync(request.OsobaId, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<AdReactiveSyncConsumer>>();
            logger.LogInformation(
                "AD reactive sync osoba={OsobaId} source={Source} failed: {Reason}",
                request.OsobaId, request.Source, result.FailureReason);
        }
    }
}
```

- [ ] **Step 2: Napsat test**

Create `PmTracker.Tests.Unit/ActiveDirectory/AdReactiveSyncConsumerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ActiveDirectory;

public sealed class AdReactiveSyncConsumerTests
{
    [Fact]
    public async Task EnqueuedRequest_TriggersSyncSinglePersonAsync()
    {
        var guid = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<PmTrackerDbContext>(o =>
            o.UseInMemoryDatabase("ad-reactive-" + Guid.NewGuid()));
        services.AddSingleton(typeof(IReactiveSyncQueue<>), typeof(ReactiveSyncQueue<>));

        var adMock = new Mock<IActiveDirectoryService>();
        adMock.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ActiveDirectoryBatchResponse
              {
                  Available = true,
                  Persons = new[] { new ActiveDirectoryPersonResult
                  {
                      GuidAd = guid, DisplayName = "Updated",
                      Jmeno = "Upd", Prijmeni = "Ated", Email = "upd@example.com", CanSelect = true
                  } },
                  NotFoundGuids = Array.Empty<Guid>()
              });
        services.AddScoped<IActiveDirectoryService>(_ => adMock.Object);
        services.AddScoped<IAdSyncService, AdSyncService>();
        services.AddLogging();

        var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.Osoby.Add(new OsobaEntity
            {
                Id = 10, Jmeno = "Old", Prijmeni = "Name",
                Email = "old@example.com", GuidAd = guid, OrganizaceId = 1
            });
            await db.SaveChangesAsync();
        }

        var queue = sp.GetRequiredService<IReactiveSyncQueue<AdReactiveSyncRequest>>();
        var consumer = new AdReactiveSyncConsumer(
            queue,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AdReactiveSyncConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new AdReactiveSyncRequest(10, AdReactiveSource.ManualUpdate), cts.Token);

        // Poll až se osoba updatuje
        for (int i = 0; i < 30 && !cts.IsCancellationRequested; i++)
        {
            await Task.Delay(100, cts.Token);
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            var after = await db.Osoby.AsNoTracking().FirstAsync(x => x.Id == 10);
            if (after.Jmeno == "Upd") { after.Jmeno.Should().Be("Upd"); break; }
        }

        await consumer.StopAsync(CancellationToken.None);
    }
}
```

- [ ] **Step 3: Test pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "AdReactiveSyncConsumerTests" --no-restore 2>&1 | tail -10`

Expected: `Passed: 1`.

- [ ] **Step 4: DI v Program.cs**

Append v DI bloku z Task 13:

```csharp
builder.Services.AddHostedService<AdReactiveSyncConsumer>();
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build PmTracker.Web --no-restore
git add PmTracker.Web/Services/ActiveDirectory/AdReactiveSyncConsumer.cs \
        PmTracker.Web/Program.cs \
        PmTracker.Tests.Unit/ActiveDirectory/AdReactiveSyncConsumerTests.cs
git commit -m "feat(ad-sync): AdReactiveSyncConsumer + DI + integrační test"
```

---

## Task 15: Shared view model + `_SyncJobSettingsCard.cshtml` partial

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Sync/SyncJobResultSummary.cs`
- Create: `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsCardViewModel.cs`
- Create: `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsInputModel.cs`
- Create: `PmTracker.Web/Views/Shared/_SyncJobSettingsCard.cshtml`

- [ ] **Step 1: View model soubory**

Create `PmTracker.Web/Models/ViewModels/Sync/SyncJobResultSummary.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels.Sync;

public sealed class SyncJobResultSummary
{
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public long? DurationMs { get; init; }
    public int? OkCount { get; init; }
    public int? ErrorCount { get; init; }
    public string? ErrorSummary { get; init; }
}
```

Create `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsCardViewModel.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels.Sync;

public sealed class SyncJobSettingsCardViewModel
{
    public required string JobKey { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }

    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; }
    public DateTimeOffset AnchorAt { get; set; }

    public DateTime? LastRunAt { get; init; }
    public string? LastTriggerKind { get; init; }
    public SyncJobResultSummary? LastResult { get; init; }

    public bool IsRunning { get; init; }
    public DateTime? RunStartedAt { get; init; }

    public bool CanManage { get; init; }
}
```

Create `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsInputModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels.Sync;

public sealed class SyncJobSettingsInputModel
{
    [Required]
    public required string JobKey { get; init; }

    public bool IsEnabled { get; set; }

    [Range(5, 10080)]   // 5 minut až 7 dnů
    public int PeriodMinutes { get; set; }

    [Required]
    public DateTimeOffset AnchorAt { get; set; }
}
```

- [ ] **Step 2: Razor partial**

Create `PmTracker.Web/Views/Shared/_SyncJobSettingsCard.cshtml`:

```razor
@model PmTracker.Web.Models.ViewModels.Sync.SyncJobSettingsCardViewModel

<section class="sync-job-card" data-sync-job-card data-job-key="@Model.JobKey">
    <header class="sync-job-card__head">
        <h3>@Model.Title</h3>
        @if (!string.IsNullOrWhiteSpace(Model.Description))
        {
            <p class="sync-job-card__desc">@Model.Description</p>
        }
    </header>

    @if (Model.IsRunning)
    {
        <div class="sync-job-card__running">
            Právě běží @(Model.LastTriggerKind == "manual" ? "(manuální spuštění)" : "(automatický tick)")
            @if (Model.RunStartedAt is not null)
            {
                <span>od @Model.RunStartedAt.Value.ToLocalTime().ToString("HH:mm:ss")</span>
            }
        </div>
    }

    <form asp-controller="NastaveniSync" asp-action="Save" method="post" class="sync-job-card__form">
        @Html.AntiForgeryToken()
        <input type="hidden" name="JobKey" value="@Model.JobKey" />

        <label class="sync-job-card__row">
            <span>Zapnuto</span>
            <input type="checkbox" name="IsEnabled" value="true" @(Model.IsEnabled ? "checked" : "") @(Model.CanManage ? "" : "disabled") />
            <input type="hidden" name="IsEnabled" value="false" />
        </label>

        <label class="sync-job-card__row">
            <span>Perioda (minuty)</span>
            <input type="number" name="PeriodMinutes" value="@Model.PeriodMinutes" min="5" max="10080" @(Model.CanManage ? "" : "disabled") />
        </label>

        <label class="sync-job-card__row">
            <span>Anchor (UTC)</span>
            <input type="datetime-local" name="AnchorAt" value="@Model.AnchorAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm")" @(Model.CanManage ? "" : "disabled") />
        </label>

        <div class="sync-job-card__actions">
            <button type="submit" class="pm-button primary" @(Model.CanManage ? "" : "disabled")>Uložit</button>
        </div>
    </form>

    @if (Model.CanManage)
    {
        <form asp-controller="NastaveniSync" asp-action="RunNow" method="post" class="sync-job-card__run-now">
            @Html.AntiForgeryToken()
            <input type="hidden" name="JobKey" value="@Model.JobKey" />
            <button type="submit" class="pm-button ghost" @(Model.IsRunning ? "disabled" : "")>Spustit teď</button>
        </form>
    }

    <div class="sync-job-card__status">
        <h4>Status posledního běhu</h4>
        @if (Model.LastRunAt is null)
        {
            <p>Zatím nespuštěno.</p>
        }
        else
        {
            <dl>
                <dt>Čas</dt>
                <dd>@Model.LastRunAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")</dd>
                <dt>Spouštěč</dt>
                <dd>@(Model.LastTriggerKind == "manual" ? "Manuální" : "Automatický")</dd>
                @if (Model.LastResult is not null)
                {
                    <dt>OK</dt><dd>@Model.LastResult.OkCount</dd>
                    <dt>Chyby</dt><dd>@Model.LastResult.ErrorCount</dd>
                    @if (Model.LastResult.DurationMs is not null)
                    {
                        <dt>Trvání</dt><dd>@Model.LastResult.DurationMs ms</dd>
                    }
                    @if (!string.IsNullOrWhiteSpace(Model.LastResult.ErrorSummary))
                    {
                        <dt>Shrnutí chyb</dt><dd>@Model.LastResult.ErrorSummary</dd>
                    }
                }
            </dl>
        }
    </div>
</section>
```

- [ ] **Step 3: Build ověří Razor syntax**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Sync/ \
        PmTracker.Web/Views/Shared/_SyncJobSettingsCard.cshtml
git commit -m "feat(sync-ui): sdílený view model + _SyncJobSettingsCard.cshtml partial"
```

---

## Task 16: `ISyncJobAdminHandler` + `AdSyncJobAdminHandler` + `NastaveniSyncController`

**Files:**
- Create: `PmTracker.Web/Services/Sync/ISyncJobAdminHandler.cs`
- Create: `PmTracker.Web/Services/ActiveDirectory/AdSyncJobAdminHandler.cs`
- Create: `PmTracker.Web/Controllers/NastaveniSyncController.cs`
- Modify: `PmTracker.Web/Program.cs`

- [ ] **Step 1: Vytvořit interface**

Create `PmTracker.Web/Services/Sync/ISyncJobAdminHandler.cs`:

```csharp
using PmTracker.Web.Models.ViewModels.Sync;

namespace PmTracker.Web.Services.Sync;

public interface ISyncJobAdminHandler
{
    string JobKey { get; }
    Task<SyncJobSettingsCardViewModel> LoadCardAsync(bool canManage, CancellationToken ct);
    Task<bool> SaveAsync(SyncJobSettingsInputModel input, int? editorOsobaId, CancellationToken ct);
    Task<bool> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct);
}
```

- [ ] **Step 2: Vytvořit AD handler**

Create `PmTracker.Web/Services/ActiveDirectory/AdSyncJobAdminHandler.cs`:

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Sync;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdSyncJobAdminHandler : ISyncJobAdminHandler
{
    private readonly PmTrackerDbContext _db;
    private readonly AdPeriodicSyncHostedService _hosted;
    private readonly TimeProvider _time;

    public AdSyncJobAdminHandler(PmTrackerDbContext db, AdPeriodicSyncHostedService hosted, TimeProvider time)
    {
        _db = db;
        _hosted = hosted;
        _time = time;
    }

    public string JobKey => "ad.periodic";

    public async Task<SyncJobSettingsCardViewModel> LoadCardAsync(bool canManage, CancellationToken ct)
    {
        var s = await _db.AdSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1, ct);

        SyncJobResultSummary? summary = null;
        if (!string.IsNullOrWhiteSpace(s.LastResultJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(s.LastResultJson);
                var root = doc.RootElement;
                summary = new SyncJobResultSummary
                {
                    StartedAt = root.TryGetProperty("startedAt", out var sa) && sa.TryGetDateTime(out var saDt) ? saDt : null,
                    FinishedAt = root.TryGetProperty("finishedAt", out var fa) && fa.TryGetDateTime(out var faDt) ? faDt : null,
                    DurationMs = root.TryGetProperty("durationMs", out var dm) ? dm.GetInt64() : null,
                    OkCount = root.TryGetProperty("okCount", out var ok) ? ok.GetInt32() : null,
                    ErrorCount = root.TryGetProperty("errorCount", out var ec) ? ec.GetInt32() : null,
                    ErrorSummary = root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0
                        ? errs[0].TryGetProperty("reason", out var r) ? r.GetString() : null
                        : null
                };
            }
            catch (JsonException) { /* ignore malformed */ }
        }

        return new SyncJobSettingsCardViewModel
        {
            JobKey = JobKey,
            Title = "Synchronizace s AD",
            Description = "Pravidelná aktualizace osob s vyplněným GuidAd.",
            IsEnabled = s.IsEnabled,
            PeriodMinutes = s.PeriodMinutes,
            AnchorAt = s.AnchorAt,
            LastRunAt = s.LastRunAt,
            LastTriggerKind = s.LastTriggerKind,
            LastResult = summary,
            IsRunning = s.IsRunning,
            RunStartedAt = s.RunStartedAt,
            CanManage = canManage
        };
    }

    public async Task<bool> SaveAsync(SyncJobSettingsInputModel input, int? editorOsobaId, CancellationToken ct)
    {
        if (input.JobKey != JobKey) return false;
        var s = await _db.AdSyncSettings.FirstAsync(x => x.Id == 1, ct);
        s.IsEnabled = input.IsEnabled;
        s.PeriodMinutes = input.PeriodMinutes;
        s.AnchorAt = input.AnchorAt;
        s.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        s.UpdatedByOsobaId = editorOsobaId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct)
    {
        // Fire-and-forget běh přímo v request kontextu. Semafor v hosted service zabrání kolizi.
        _ = Task.Run(async () =>
        {
            try { await _hosted.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None); }
            catch { /* logováno uvnitř */ }
        }, ct);
        return true;
    }
}
```

- [ ] **Step 3: Vytvořit controller**

Create `PmTracker.Web/Controllers/NastaveniSyncController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.Sync;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Controllers;

[Route("Nastaveni/Sync")]
public sealed class NastaveniSyncController : BaseController
{
    private readonly IEnumerable<ISyncJobAdminHandler> _handlers;

    public NastaveniSyncController(IEnumerable<ISyncJobAdminHandler> handlers)
    {
        _handlers = handlers;
    }

    private ISyncJobAdminHandler? FindHandler(string jobKey)
        => _handlers.FirstOrDefault(h => h.JobKey == jobKey);

    [HttpPost("{jobKey}/Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string jobKey, SyncJobSettingsInputModel input, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
            return Forbid();

        var handler = FindHandler(jobKey);
        if (handler is null) return NotFound();

        if (!ModelState.IsValid)
        {
            TempData["SyncSaveError"] = "Neplatné hodnoty — zkontroluj periodu a anchor.";
            return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
        }

        await handler.SaveAsync(input, CurrentUserContext.OsobaId, ct);
        TempData["SyncSaveSuccess"] = $"Nastavení '{jobKey}' uloženo.";
        return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
    }

    [HttpPost("{jobKey}/RunNow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(string jobKey, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
            return Forbid();

        var handler = FindHandler(jobKey);
        if (handler is null) return NotFound();

        await handler.TriggerManualRunAsync(CurrentUserContext.OsobaId, ct);
        TempData["SyncRunNowStatus"] = $"Trigger '{jobKey}' spuštěn — výsledek se objeví po dokončení běhu.";
        return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
    }
}
```

**POZOR:** Kontrola `CurrentUserContext` je property z `BaseController` (předpoklad, že existuje v PM Tracker). Pokud NastaveniController používá jinou metodu, přizpůsob podle [NastaveniController.cs:28](PmTracker.Web/Controllers/NastaveniController.cs#L28):
```csharp
if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
```

Pokud metoda `CurrentUserContext.OsobaId` neexistuje přesně takhle, pouzij to, co používá stávající `NastaveniController` v metodě `Save` pro editorOsobaId.

- [ ] **Step 4: DI registrace**

V `Program.cs` přidej za existující AD sync bloky:

```csharp
builder.Services.AddScoped<ISyncJobAdminHandler, AdSyncJobAdminHandler>();
```

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Sync/ISyncJobAdminHandler.cs \
        PmTracker.Web/Services/ActiveDirectory/AdSyncJobAdminHandler.cs \
        PmTracker.Web/Controllers/NastaveniSyncController.cs \
        PmTracker.Web/Program.cs
git commit -m "feat(sync-ui): ISyncJobAdminHandler + AdSyncJobAdminHandler + NastaveniSyncController"
```

---

## Task 17: Nastaveni navigace + sekce `synchronizace`

**Files:**
- Modify: `PmTracker.Web/Services/Settings/SettingsService.cs`
- Modify: `PmTracker.Web/Controllers/NastaveniController.cs`
- Create: `PmTracker.Web/Views/Nastaveni/_SyncPanel.cshtml`
- Modify: `PmTracker.Web/Views/Nastaveni/_DetailPanel.cshtml` (pokud existuje switch section → partial)

- [ ] **Step 1: Registrovat sekci v SettingsService**

Open `PmTracker.Web/Services/Settings/SettingsService.cs`. Najdi blok, kde se definuje seznam sekcí (např. list of `SettingsSectionDescriptor` nebo podobné).

Přidej novou sekci:

```csharp
new SettingsSectionDescriptor
{
    Key = "synchronizace",
    Nazev = "Synchronizace",
    Popis = "Periodické a reaktivní synchronizace (AD, ServiceDesk)",
    RequiredPermission = PermissionKeys.SettingsView,
    Pocet = null
}
```

(Přesný tvar je závislý na existující struktuře `SettingsSectionDescriptor` — otevři soubor a reflektuj formát.)

- [ ] **Step 2: Handling sekce v NastaveniController**

Najdi v `NastaveniController.Index` switch/if blok, kde se podle `section` rozhoduje, co renderovat.

Přidej větev pro `synchronizace`:

```csharp
if (normalizedSection == "synchronizace")
{
    if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsView))
        return Forbid();

    var handlers = HttpContext.RequestServices.GetServices<ISyncJobAdminHandler>();
    var canManage = CurrentUserContext.HasPermission(PermissionKeys.SettingsManage);
    var cards = new List<SyncJobSettingsCardViewModel>();
    foreach (var h in handlers)
        cards.Add(await h.LoadCardAsync(canManage, HttpContext.RequestAborted));

    panel.PartialViewName = "_SyncPanel";
    panel.PartialViewModel = cards;
    return View("Index", dashboardViewModel);
}
```

(Skutečný tvar závislý na existujícím pattern panelu — sleduj, jak jiné sekce jako `"efektivni-prava"` routují.)

- [ ] **Step 3: Vytvořit `_SyncPanel.cshtml`**

Create `PmTracker.Web/Views/Nastaveni/_SyncPanel.cshtml`:

```razor
@model IReadOnlyList<PmTracker.Web.Models.ViewModels.Sync.SyncJobSettingsCardViewModel>

<div class="sync-panel">
    <h2>Synchronizace</h2>
    <p class="sync-panel__intro">
        Správa periodických a reaktivních synchronizací mezi PM Trackerem a externími systémy.
    </p>

    @if (TempData["SyncSaveSuccess"] is string ok)
    {
        <div class="pm-flash pm-flash--success">@ok</div>
    }
    @if (TempData["SyncSaveError"] is string err)
    {
        <div class="pm-flash pm-flash--error">@err</div>
    }
    @if (TempData["SyncRunNowStatus"] is string run)
    {
        <div class="pm-flash pm-flash--info">@run</div>
    }

    @if (Model.Count == 0)
    {
        <p>Žádné sync joby nejsou registrované.</p>
    }
    else
    {
        foreach (var card in Model)
        {
            @await Html.PartialAsync("_SyncJobSettingsCard", card)
        }
    }
</div>
```

- [ ] **Step 4: Build + smoke test**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Settings/SettingsService.cs \
        PmTracker.Web/Controllers/NastaveniController.cs \
        PmTracker.Web/Views/Nastaveni/_SyncPanel.cshtml
git commit -m "feat(sync-ui): sekce Nastaveni/synchronizace + _SyncPanel + handler routing"
```

---

## Task 18: Wire reactive triggery (person pick + manual update button)

**Files:**
- Modify: `PmTracker.Web/Controllers/OsobyController.cs` (nebo controller kde je person pick flow)
- Modify: `PmTracker.Web/Views/Osoby/Detail.cshtml` (nebo equivalent detail/edit view)

**Pre-step — najít kde je AD pick flow:**

Run:
```bash
grep -rn "ActiveDirectoryPersonResult\|SearchUsersAsync" PmTracker.Web/Controllers/ 2>/dev/null | head -10
```

Expected: seznam controllerů/míst kde se AD data zapisují do `OsobaEntity`. Typicky `OsobyController.SaveFromAd` nebo podobné.

- [ ] **Step 1: Injektovat `IReactiveSyncQueue<AdReactiveSyncRequest>` do controlleru**

V konstruktoru controlleru (identifikováno pre-stepem) přidej parametr:

```csharp
private readonly IReactiveSyncQueue<AdReactiveSyncRequest> _adReactiveQueue;

public OsobyController(
    // ... existující parametry ...
    IReactiveSyncQueue<AdReactiveSyncRequest> adReactiveQueue)
{
    // ... existující přiřazení ...
    _adReactiveQueue = adReactiveQueue;
}
```

- [ ] **Step 2: Enqueue po úspěšném person pick insertu**

V metodě, která vkládá novou `OsobaEntity` z AD výsledku (po `db.SaveChangesAsync`):

```csharp
await _db.SaveChangesAsync(ct);

// Reactive refresh AD dat — fire & forget
await _adReactiveQueue.EnqueueAsync(
    new AdReactiveSyncRequest(osoba.Id, AdReactiveSource.PersonPicked), ct);
```

- [ ] **Step 3: Manuální „Aktualizovat z AD" endpoint**

Přidat nový endpoint do stejného controlleru:

```csharp
[HttpPost("/Osoby/{id:int}/SyncFromAd")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SyncFromAd(int id, CancellationToken ct)
{
    if (!CurrentUserContext.HasPermission(PermissionKeys.PeopleManage))
        return Forbid();

    var osoba = await _db.Osoby.FirstOrDefaultAsync(x => x.Id == id, ct);
    if (osoba is null) return NotFound();

    await _adReactiveQueue.EnqueueAsync(
        new AdReactiveSyncRequest(id, AdReactiveSource.ManualUpdate), ct);

    TempData["AdSyncStatus"] = $"Aktualizace {osoba.Jmeno} {osoba.Prijmeni} spuštěna na pozadí.";
    return RedirectToAction("Detail", new { id });
}
```

- [ ] **Step 4: UI tlačítko v Detail.cshtml**

Open `PmTracker.Web/Views/Osoby/Detail.cshtml` (nebo ekvivalent). V action-panelu osoby (blízko existujícího „Upravit"):

```razor
@if (Model.CanManage && Model.Osoba.GuidAd is not null)
{
    <form asp-controller="Osoby" asp-action="SyncFromAd" asp-route-id="@Model.Osoba.Id" method="post" class="pm-inline">
        @Html.AntiForgeryToken()
        <button type="submit" class="pm-button ghost" title="Načíst aktuální data z AD">
            <gov-icon name="refresh" type="components" aria-hidden="true"></gov-icon>
            Aktualizovat z AD
        </button>
    </form>
}

@if (TempData["AdSyncStatus"] is string st)
{
    <div class="pm-flash pm-flash--info">@st</div>
}
```

**POZOR:** `Model.CanManage` předpokládá, že ViewModel má takovou property. Pokud ne, nahraď přes `@{ var canManage = ...HasPermission(PermissionKeys.PeopleManage); }` s odpovídajícím pattern z jiných views.

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web --no-restore 2>&1 | tail -5`

Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Controllers/OsobyController.cs \
        PmTracker.Web/Views/Osoby/Detail.cshtml
git commit -m "feat(ad-sync): wire reactive triggery — person pick + manual 'Aktualizovat z AD' button"
```

---

## Task 19: Full build + test + final commit hygiene

- [ ] **Step 1: Full solution build**

Run: `dotnet build --no-restore -c Debug 2>&1 | tail -10`

Expected: `Build succeeded. 0 Error(s)`, 0 warnings nebo jen už-existující (no-new).

- [ ] **Step 2: Full test**

Run: `dotnet test PmTracker.Tests.Unit --no-restore --no-build 2>&1 | tail -15`

Expected: všechny testy passed, nové testy z tohoto plánu (~25 nových):
- 10 × SyncScheduleCalculator
- 4 × SyncJobRunLock
- 3 × ReactiveSyncQueue
- 2 × ReactiveSyncConsumerBase
- 3 × SyncHostedServiceBase
- 1 × AdSyncSettingsEntity
- 5 × AdSyncService
- 2 × AdPeriodicSyncHostedService
- 1 × AdReactiveSyncConsumer

- [ ] **Step 3: Git status clean**

Run: `git status && git log --oneline | head -20`

Expected: čistý strom; posledních ~19 commitů odpovídající Taskům 1-18 (+ commit specu `8b03b82`).

- [ ] **Step 4: Manuální smoke — zapnout AD sync**

Pokud Dev DB je dostupná a máš Windows stroj:

1. Spusť Dev server.
2. Přihlaš se jako admin se `SettingsManage` + `PeopleManage`.
3. Jdi na `/Nastaveni?section=synchronizace` — měj vidět kartu „Synchronizace s AD".
4. Zapni switch `is_enabled`, nastav perioda = 60, anchor = nyní, ulož.
5. Klikni „Spustit teď". Refresh za 3-5 vteřin.
6. Status panel by měl zobrazit `LastRunAt`, `LastTriggerKind = Manuální`, OK count (pokud osoby s `GuidAd` existují).
7. V `/Osoby/Detail/<id>` (osoba s `GuidAd`) klikni „Aktualizovat z AD" — za pár vteřin by data měla být refreshnutá (nebo log info o failure, pokud nejsi na Windows / AD není dostupné).

- [ ] **Step 5: Spec cross-check**

Run:
```bash
grep -n "^##\|^###" docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md | head -40
```

Projdi sekcí 2 (shared infra), 3 (AD), 6 (permissions+UI), 7 (testování) a zkontroluj, že každá subsekce je pokryta Task 1-18.

Známé no-impl: §4 (SD náčrt) je mimo scope tohoto plánu, §5.4 (verifikace `HOT_ZAZNAMY.datum`) je mimo scope tohoto plánu — bude v revidovaném SD plánu.

- [ ] **Step 6: Final commit — summary**

Pokud během Tasků vznikly drobné merge / format věci, vytvoř finalizační commit (jinak přeskoč):

```bash
git status
# pokud clean:
echo "Plan complete, 19 tasks, 19 commits."
```

---

## Hotovo

Po průběhu všech tasků máš:

- ✅ Sdílenou sync infrastrukturu v `PmTracker.Web/Services/Sync/` (7 souborů)
- ✅ AD konzument: `AdSyncSettingsEntity`, `IAdSyncService`, `AdPeriodicSyncHostedService`, `AdReactiveSyncConsumer`, rozšíření `IActiveDirectoryService.ListByGuidsAsync`
- ✅ Admin UI karta v `/Nastaveni?section=synchronizace` (shared `_SyncJobSettingsCard.cshtml`)
- ✅ Wire reactive triggery: person pick + manual update button
- ✅ ~25 unit/integration testů
- ✅ DB upgrade skript `db_upgrade_1_3_0_ad_sync_settings.sql` aplikovaný na Dev DB

### Mimo scope (další plány)

- **SD konzument** — `docs/superpowers/plans/2026-04-22-sd-sync-revise.md` (bude vytvořen samostatně): revidovat SD spec §8.2+§8.6, přepsat Plán C Task 10/11 na shared infra, přepsat Plán E na typed entity
- **Verifikace `HOT_ZAZNAMY.datum` semantiky** — bude první úkol v SD revize plánu
- **Reactive queue status v Nastavení** — defer (logy stačí pro debug)
