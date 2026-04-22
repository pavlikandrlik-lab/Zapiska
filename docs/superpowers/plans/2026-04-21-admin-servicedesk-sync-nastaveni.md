# Admin nastavení synchronizace ServiceDesku Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přidat admin rozhraní v `/Nastaveni?section=servicedesk-sync` pro řízení periodického harvestu (Hangfire interval), proaktivního harvestu při otevření editoru a master killswitche. Uložit konfiguraci do nové tabulky `servicedesk_sync_settings` (key-value).

**Architecture:** Nová klíč-hodnota tabulka + `IServiceDeskSyncSettings` cached reader (IMemoryCache). Nová sekce v existujícím `NastaveniController` (`/Nastaveni/ServiceDeskSync`). UI karta s gov komponentami (gov-select, gov-form-input, gov-switch). ACL: jen `app_admin` a `SuperAdmin`. Hangfire job čte interval z `IServiceDeskSyncSettings` při každém startu cyklu; při změně intervalu se cron re-registruje přes event.

**Tech Stack:** .NET 8 ASP.NET Core MVC, EF Core 8, IMemoryCache, Hangfire 1.8+, Gov Design System 4.2.9, xUnit + FluentAssertions + Moq, Playwright.

**Předpoklad:** Plán C (chat modal + Hangfire harvest implementace) **nemusí být hotov** — Plán E dodá jen konfiguraci + admin UI. Plán C pak použije `IServiceDeskSyncSettings` místo hardcoded hodnot.

> **AKTUALIZACE 2026-04-22 — Plán E je z velké části superseded:**
> Spec `2026-04-22-sync-infra-and-ad-design.md` zavádí **obecnou** sync admin kartu (`/Nastaveni?section=synchronizace`) s třemi slotty: AD, SD-active, SD-archive. Každý slot je řízen entitou implementující `ISyncJobSettings` (per-job row v singleton tabulce: `is_enabled`, `period_minutes`, `anchor_at`, `last_run_at`, `last_trigger_kind`, `last_result_json`, `is_running`, ...). Admin UI je společná komponenta `_SyncJobSettingsCard.cshtml` + handler per job (`ISyncJobAdminHandler`).
>
> **Co z tohoto plánu E platí:**
> - Motivace + business požadavky (kill-switch, interval, grace window, freshness, timeout, max-parallelism, archive grace).
> - Principy ACL (`app_admin` / `SuperAdmin`).
>
> **Co se v Plánu E NEIMPLEMENTUJE (dělá sync-infra plán):**
> - Tabulka `servicedesk_sync_settings` jako key-value store — **ZRUŠENO**. Místo toho dvě paralelní singleton-row tabulky `sd_active_sync_settings` + `sd_archive_sync_settings` dle vzoru `ad_sync_settings`.
> - `IServiceDeskSyncSettings` interface — nahrazeno generickou `ISyncJobSettings` + konkrétními entitami.
> - `NastaveniServiceDeskSyncController` — nahrazeno generickým `NastaveniSyncController` + keyed `ISyncJobAdminHandler`.
> - Razor view — nahrazeno shared `_SyncJobSettingsCard.cshtml`.
>
> **Doporučení:** Plán E **zahodit jako samostatný dokument** (nebo přepsat na tenké „mapping note" pro SD-specific pole). Místo něj:
> 1. Nejdřív implementovat `2026-04-22-sync-infra-and-ad.md` (hotový, 19 tasků).
> 2. Potom napsat `2026-04-22-sd-sync-revise.md` (AD bude template) — dodá SD konzumenty + admin karty + fingerprint sloupce na `zaznam_externi_odkazy`.
>
> **Authz:** sync admin karta má ACL `PermissionKeys.SettingsManage` (konzistentně s §6 spec sync-infra, nikoli speciální „SuperAdmin" check).

---

## File Structure

### Nové soubory
- `db_upgrade_1_2_1_servicedesk_sync_settings.sql` — tabulka + seed 6 klíčů.
- `PmTracker.Web/Models/Entities/ServiceDeskSyncSettingsEntity.cs` — row entity.
- `PmTracker.Web/Data/Configuration/ServiceDeskSyncSettingsEntityConfiguration.cs` — EF mapping.
- `PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncOptions.cs` — POCO s hodnotami (parsed).
- `PmTracker.Web/Services/ServiceDesk/IServiceDeskSyncSettings.cs` — reader/writer interface.
- `PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncSettings.cs` — cached implementace.
- `PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs` — nový controller (pro oddělení, ne přetížení `NastaveniController`).
- `PmTracker.Web/Models/ViewModels/ServiceDeskSync/ServiceDeskSyncViewModel.cs` — VM pro form.
- `PmTracker.Web/Views/Nastaveni/_ServiceDeskSyncCard.cshtml` — UI karta.
- `PmTracker.Web/wwwroot/css/components/servicedesk-sync-card.css` — lehký styling (gap, padding).
- `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsTests.cs` — unit testy reader/writer.
- `PmTracker.Tests.Unit/ServiceDesk/NastaveniServiceDeskSyncControllerTests.cs` — ACL + save flow.

### Modifikované soubory
- `PmTracker.Web/Data/PmTrackerDbContext.cs` — přidat `DbSet<ServiceDeskSyncSettingsEntity> ServiceDeskSyncSettings`.
- `PmTracker.Web/Program.cs` — DI registrace `IServiceDeskSyncSettings` + IMemoryCache.
- `PmTracker.Web/Views/Nastaveni/Index.cshtml` (nebo partial se sekcemi) — přidat položku „Synchronizace ServiceDesku" do navigace, viditelnou jen pro admin role.
- `PmTracker.Web/wwwroot/css/site.css` — `@import "components/servicedesk-sync-card.css"`.

### Soubory, které se záměrně NEMĚNÍ
- `NastaveniController.cs` — přeplněný AuthZ logikou, nový subsystem dostane vlastní controller.
- Samotný Hangfire job — **není součást tohoto plánu**, implementace je v Plánu C. Plán E jen dodá konfiguraci, kterou si Plán C přečte.

---

## Pořadí úkolů

1. **Task 1** — DB tabulka `servicedesk_sync_settings` + seed 6 klíčů.
2. **Task 2** — Entity + DbContext mapping.
3. **Task 3** — `ServiceDeskSyncOptions` POCO + parse helpers + unit test.
4. **Task 4** — `IServiceDeskSyncSettings` + cached implementace + unit test.
5. **Task 5** — ViewModel + Controller (GET + POST Save).
6. **Task 6** — Razor view `_ServiceDeskSyncCard.cshtml` + CSS.
7. **Task 7** — Navigace v `/Nastaveni` — přidat sekci, ACL check.
8. **Task 8** — Endpoint `/Nastaveni/ServiceDeskSync/RunNow` (force trigger).
9. **Task 9** — Endpoint `/Nastaveni/ServiceDeskSync/Log` (poslední Hangfire runs).
10. **Task 10** — Playwright smoke test (otevření stránky, změna hodnoty, save, reload — hodnota přetrvá).
11. **Task 11** — Full build + test + git clean.

---

## Task 1: DB tabulka + seed

**Files:**
- Create: `db_upgrade_1_2_1_servicedesk_sync_settings.sql`

- [ ] **Step 1: Napsat skript**

Vytvoř `db_upgrade_1_2_1_servicedesk_sync_settings.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_2_1_servicedesk_sync_settings.sql
-- Key-value store pro konfiguraci synchronizace ServiceDesku (Hangfire interval,
-- freshness threshold, killswitch, ...).
-- Spec: docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §8.6
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'servicedesk_sync_settings')
BEGIN
    CREATE TABLE dbo.servicedesk_sync_settings
    (
        klic                NVARCHAR(200) NOT NULL CONSTRAINT PK_servicedesk_sync_settings PRIMARY KEY,
        hodnota             NVARCHAR(MAX) NOT NULL,
        updated_at          DATETIME2 NOT NULL CONSTRAINT DF_servicedesk_sync_settings_updated_at DEFAULT SYSUTCDATETIME(),
        updated_by_osoba_id INT NULL
            CONSTRAINT FK_servicedesk_sync_settings_osoba REFERENCES dbo.osoby(id)
    );

    PRINT N'Tabulka servicedesk_sync_settings vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka servicedesk_sync_settings už existuje, přeskakuji CREATE.';
END;
GO

-- Seed výchozích hodnot (idempotentní MERGE)
MERGE dbo.servicedesk_sync_settings AS tgt
USING (VALUES
    (N'servicedesk.sync.enabled', N'true'),
    (N'servicedesk.sync.hangfire.interval', N'1h'),
    (N'servicedesk.sync.hangfire.maxParallelism', N'4'),
    (N'servicedesk.sync.hangfire.archiveGraceDays', N'7'),
    (N'servicedesk.sync.editor.freshnessMinutes', N'5'),
    (N'servicedesk.sync.timeout.seconds', N'30')
) AS src(klic, hodnota) ON tgt.klic = src.klic
WHEN NOT MATCHED THEN
    INSERT (klic, hodnota) VALUES (src.klic, src.hodnota);

PRINT N'Seed výchozích hodnot dokončen.';
GO
```

- [ ] **Step 2: Spustit skript proti Dev DB**

```bash
cd /tmp/run-sql && dotnet run "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_1_servicedesk_sync_settings.sql"
```

Expected: dvě `PRINT` zprávy (tabulka + seed). Opakované spuštění: obě zprávy přeskočí (idempotentní).

- [ ] **Step 3: Ověřit obsah**

Vytvoř `/tmp/verify-sd-sync.sql`:

```sql
SELECT klic, hodnota FROM dbo.servicedesk_sync_settings ORDER BY klic;
```

Spusť:

```bash
cd /tmp/run-sql && dotnet run /tmp/verify-sd-sync.sql
```

Expected: 6 řádků s výchozími hodnotami.

- [ ] **Step 4: Commit**

```bash
git add db_upgrade_1_2_1_servicedesk_sync_settings.sql
git commit -m "feat(db): db_upgrade_1_2_1 — servicedesk_sync_settings tabulka + seed 6 klíčů"
```

---

## Task 2: Entity + DbContext mapping

**Files:**
- Create: `PmTracker.Web/Models/Entities/ServiceDeskSyncSettingsEntity.cs`
- Create: `PmTracker.Web/Data/Configuration/ServiceDeskSyncSettingsEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsEntityTests.cs`

- [ ] **Step 1: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsEntityTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class ServiceDeskSyncSettingsEntityTests
{
    [Fact]
    public void Entity_ShouldHoldKlicAndHodnota()
    {
        var e = new ServiceDeskSyncSettingsEntity
        {
            Klic = "servicedesk.sync.enabled",
            Hodnota = "true",
            UpdatedAt = new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc),
            UpdatedByOsobaId = 42
        };
        e.Klic.Should().Be("servicedesk.sync.enabled");
        e.Hodnota.Should().Be("true");
        e.UpdatedByOsobaId.Should().Be(42);
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ServiceDeskSyncSettingsEntityTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Vytvořit entity**

Vytvoř `PmTracker.Web/Models/Entities/ServiceDeskSyncSettingsEntity.cs`:

```csharp
namespace PmTracker.Web.Models.Entities;

public sealed class ServiceDeskSyncSettingsEntity
{
    public string Klic { get; set; } = string.Empty;
    public string Hodnota { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByOsobaId { get; set; }
}
```

- [ ] **Step 4: Vytvořit EF configuration**

Vytvoř `PmTracker.Web/Data/Configuration/ServiceDeskSyncSettingsEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class ServiceDeskSyncSettingsEntityConfiguration
    : IEntityTypeConfiguration<ServiceDeskSyncSettingsEntity>
{
    public void Configure(EntityTypeBuilder<ServiceDeskSyncSettingsEntity> builder)
    {
        builder.ToTable("servicedesk_sync_settings");
        builder.HasKey(x => x.Klic);
        builder.Property(x => x.Klic).HasColumnName("klic").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Hodnota).HasColumnName("hodnota").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedByOsobaId).HasColumnName("updated_by_osoba_id").IsRequired(false);
    }
}
```

- [ ] **Step 5: Přidat DbSet do DbContext**

Otevři `PmTracker.Web/Data/PmTrackerDbContext.cs` a najdi blok s ostatními `DbSet<...>` property (např. kolem `HarmonogramSablony`).

Přidej:

```csharp
public DbSet<ServiceDeskSyncSettingsEntity> ServiceDeskSyncSettings => Set<ServiceDeskSyncSettingsEntity>();
```

V `OnModelCreating` najdi volání `ApplyConfigurationsFromAssembly` — pokud existuje, nová konfigurace se najde sama. Pokud ne, přidej:

```csharp
modelBuilder.ApplyConfiguration(new ServiceDeskSyncSettingsEntityConfiguration());
```

- [ ] **Step 6: Build + test**

Run: `dotnet build PmTracker.Web --no-restore && dotnet test PmTracker.Tests.Unit --filter "ServiceDeskSyncSettingsEntityTests" --no-restore`

Expected: 0 errors, 1 test passed.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/Entities/ServiceDeskSyncSettingsEntity.cs \
        PmTracker.Web/Data/Configuration/ServiceDeskSyncSettingsEntityConfiguration.cs \
        PmTracker.Web/Data/PmTrackerDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsEntityTests.cs
git commit -m "feat(servicedesk-sync): entity + DbContext mapping pro sync_settings"
```

---

## Task 3: `ServiceDeskSyncOptions` POCO + parse helpers

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncOptions.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncOptionsTests.cs`

- [ ] **Step 1: Napsat failující testy**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncOptionsTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class ServiceDeskSyncOptionsTests
{
    [Fact]
    public void Default_ReturnsProductionDefaults()
    {
        var opts = ServiceDeskSyncOptions.Default();
        opts.Enabled.Should().BeTrue();
        opts.HangfireInterval.Should().Be(TimeSpan.FromHours(1));
        opts.HangfireMaxParallelism.Should().Be(4);
        opts.ArchiveGraceDays.Should().Be(7);
        opts.EditorFreshness.Should().Be(TimeSpan.FromMinutes(5));
        opts.TimeoutPerTicket.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData("disabled", false)]
    [InlineData("15min", true)]
    [InlineData("1h", true)]
    [InlineData("12h", true)]
    public void ParseHangfireInterval_ValidValue_ReturnsParsedEnumOrDisabled(string value, bool expectedEnabled)
    {
        var result = ServiceDeskSyncOptions.ParseHangfireInterval(value);
        result.Enabled.Should().Be(expectedEnabled);
    }

    [Theory]
    [InlineData("15min", 15)]
    [InlineData("30min", 30)]
    [InlineData("1h", 60)]
    [InlineData("3h", 180)]
    [InlineData("6h", 360)]
    [InlineData("12h", 720)]
    public void ParseHangfireInterval_Valid_ReturnsCorrectDuration(string value, int expectedMinutes)
    {
        var result = ServiceDeskSyncOptions.ParseHangfireInterval(value);
        result.Interval.Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void ParseHangfireInterval_Unknown_ThrowsFormatException()
    {
        var act = () => ServiceDeskSyncOptions.ParseHangfireInterval("neplatný");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void SerializeHangfireInterval_RoundTrip()
    {
        ServiceDeskSyncOptions.SerializeHangfireInterval(TimeSpan.FromHours(1), enabled: true)
            .Should().Be("1h");
        ServiceDeskSyncOptions.SerializeHangfireInterval(TimeSpan.FromHours(1), enabled: false)
            .Should().Be("disabled");
        ServiceDeskSyncOptions.SerializeHangfireInterval(TimeSpan.FromMinutes(15), enabled: true)
            .Should().Be("15min");
    }
}
```

- [ ] **Step 2: Spustit testy (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ServiceDeskSyncOptionsTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Vytvořit POCO + helpers**

Vytvoř `PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncOptions.cs`:

```csharp
namespace PmTracker.Web.Services.ServiceDesk;

public sealed record ServiceDeskSyncOptions(
    bool Enabled,
    bool HangfireEnabled,
    TimeSpan HangfireInterval,
    int HangfireMaxParallelism,
    int ArchiveGraceDays,
    TimeSpan EditorFreshness,
    TimeSpan TimeoutPerTicket)
{
    public static ServiceDeskSyncOptions Default() => new(
        Enabled: true,
        HangfireEnabled: true,
        HangfireInterval: TimeSpan.FromHours(1),
        HangfireMaxParallelism: 4,
        ArchiveGraceDays: 7,
        EditorFreshness: TimeSpan.FromMinutes(5),
        TimeoutPerTicket: TimeSpan.FromSeconds(30));

    public static (bool Enabled, TimeSpan Interval) ParseHangfireInterval(string value)
    {
        return value switch
        {
            "disabled" => (false, TimeSpan.Zero),
            "15min" => (true, TimeSpan.FromMinutes(15)),
            "30min" => (true, TimeSpan.FromMinutes(30)),
            "1h" => (true, TimeSpan.FromHours(1)),
            "3h" => (true, TimeSpan.FromHours(3)),
            "6h" => (true, TimeSpan.FromHours(6)),
            "12h" => (true, TimeSpan.FromHours(12)),
            _ => throw new FormatException($"Neznámý interval: '{value}'.")
        };
    }

    public static string SerializeHangfireInterval(TimeSpan interval, bool enabled)
    {
        if (!enabled) return "disabled";
        return interval switch
        {
            { TotalMinutes: 15 } => "15min",
            { TotalMinutes: 30 } => "30min",
            { TotalMinutes: 60 } => "1h",
            { TotalMinutes: 180 } => "3h",
            { TotalMinutes: 360 } => "6h",
            { TotalMinutes: 720 } => "12h",
            _ => throw new FormatException($"Nepodporovaný interval: {interval}.")
        };
    }
}
```

- [ ] **Step 4: Build + test**

Run: `dotnet build PmTracker.Web --no-restore && dotnet test PmTracker.Tests.Unit --filter "ServiceDeskSyncOptionsTests" --no-restore`

Expected: 0 errors, 6/6 passed.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncOptions.cs \
        PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncOptionsTests.cs
git commit -m "feat(servicedesk-sync): ServiceDeskSyncOptions POCO + parse/serialize helpers"
```

---

## Task 4: `IServiceDeskSyncSettings` cached reader/writer

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/IServiceDeskSyncSettings.cs`
- Create: `PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncSettings.cs`
- Modify: `PmTracker.Web/Program.cs` — DI
- Test: `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsTests.cs`

- [ ] **Step 1: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class ServiceDeskSyncSettingsTests
{
    private static PmTrackerDbContext NewInMemory()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("sync-settings-" + Guid.NewGuid())
            .Options;
        return new PmTrackerDbContext(options);
    }

    [Fact]
    public async Task GetAsync_EmptyTable_ReturnsDefaultOptions()
    {
        await using var db = NewInMemory();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ServiceDeskSyncSettingsImpl(db, cache);

        var result = await sut.GetAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(ServiceDeskSyncOptions.Default());
    }

    [Fact]
    public async Task GetAsync_WithSeededValues_ReturnsParsedOptions()
    {
        await using var db = NewInMemory();
        db.ServiceDeskSyncSettings.AddRange(
            new ServiceDeskSyncSettingsEntity { Klic = "servicedesk.sync.enabled", Hodnota = "false" },
            new ServiceDeskSyncSettingsEntity { Klic = "servicedesk.sync.hangfire.interval", Hodnota = "3h" },
            new ServiceDeskSyncSettingsEntity { Klic = "servicedesk.sync.hangfire.maxParallelism", Hodnota = "8" },
            new ServiceDeskSyncSettingsEntity { Klic = "servicedesk.sync.editor.freshnessMinutes", Hodnota = "10" });
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ServiceDeskSyncSettingsImpl(db, cache);

        var result = await sut.GetAsync(CancellationToken.None);

        result.Enabled.Should().BeFalse();
        result.HangfireInterval.Should().Be(TimeSpan.FromHours(3));
        result.HangfireMaxParallelism.Should().Be(8);
        result.EditorFreshness.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task SaveAsync_InsertsOrUpdatesValuesAndInvalidatesCache()
    {
        await using var db = NewInMemory();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new ServiceDeskSyncSettingsImpl(db, cache);

        var originalGet = await sut.GetAsync(CancellationToken.None);
        originalGet.HangfireInterval.Should().Be(TimeSpan.FromHours(1));

        var newOpts = originalGet with { HangfireInterval = TimeSpan.FromHours(6) };
        await sut.SaveAsync(newOpts, editorOsobaId: 7, CancellationToken.None);

        var afterSave = await sut.GetAsync(CancellationToken.None);
        afterSave.HangfireInterval.Should().Be(TimeSpan.FromHours(6));

        (await db.ServiceDeskSyncSettings
                .Where(x => x.Klic == "servicedesk.sync.hangfire.interval")
                .Select(x => x.UpdatedByOsobaId)
                .FirstAsync())
            .Should().Be(7);
    }
}
```

- [ ] **Step 2: Spustit testy (musí failnout — třídy neexistují)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ServiceDeskSyncSettingsTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Vytvořit interface**

Vytvoř `PmTracker.Web/Services/ServiceDesk/IServiceDeskSyncSettings.cs`:

```csharp
namespace PmTracker.Web.Services.ServiceDesk;

public interface IServiceDeskSyncSettings
{
    Task<ServiceDeskSyncOptions> GetAsync(CancellationToken ct = default);
    Task SaveAsync(ServiceDeskSyncOptions opts, int? editorOsobaId, CancellationToken ct = default);
}
```

- [ ] **Step 4: Vytvořit implementaci**

Vytvoř `PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncSettings.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.ServiceDesk;

public sealed class ServiceDeskSyncSettingsImpl(
    PmTrackerDbContext db,
    IMemoryCache cache) : IServiceDeskSyncSettings
{
    private const string CacheKey = "servicedesk.sync.options";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<ServiceDeskSyncOptions> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue<ServiceDeskSyncOptions>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var rows = await db.ServiceDeskSyncSettings.AsNoTracking().ToListAsync(ct);
        var map = rows.ToDictionary(x => x.Klic, x => x.Hodnota, StringComparer.Ordinal);

        var defaults = ServiceDeskSyncOptions.Default();
        var (hfEnabled, hfInterval) = map.TryGetValue("servicedesk.sync.hangfire.interval", out var hfRaw)
            ? ServiceDeskSyncOptions.ParseHangfireInterval(hfRaw)
            : (defaults.HangfireEnabled, defaults.HangfireInterval);

        var result = new ServiceDeskSyncOptions(
            Enabled: map.TryGetValue("servicedesk.sync.enabled", out var enabledRaw)
                ? string.Equals(enabledRaw, "true", StringComparison.OrdinalIgnoreCase)
                : defaults.Enabled,
            HangfireEnabled: hfEnabled,
            HangfireInterval: hfInterval,
            HangfireMaxParallelism: map.TryGetValue("servicedesk.sync.hangfire.maxParallelism", out var mp) && int.TryParse(mp, out var mpInt)
                ? mpInt : defaults.HangfireMaxParallelism,
            ArchiveGraceDays: map.TryGetValue("servicedesk.sync.hangfire.archiveGraceDays", out var ag) && int.TryParse(ag, out var agInt)
                ? agInt : defaults.ArchiveGraceDays,
            EditorFreshness: map.TryGetValue("servicedesk.sync.editor.freshnessMinutes", out var fm) && int.TryParse(fm, out var fmInt)
                ? TimeSpan.FromMinutes(fmInt) : defaults.EditorFreshness,
            TimeoutPerTicket: map.TryGetValue("servicedesk.sync.timeout.seconds", out var to) && int.TryParse(to, out var toInt)
                ? TimeSpan.FromSeconds(toInt) : defaults.TimeoutPerTicket);

        cache.Set(CacheKey, result, CacheTtl);
        return result;
    }

    public async Task SaveAsync(ServiceDeskSyncOptions opts, int? editorOsobaId, CancellationToken ct = default)
    {
        var values = new Dictionary<string, string>
        {
            ["servicedesk.sync.enabled"] = opts.Enabled ? "true" : "false",
            ["servicedesk.sync.hangfire.interval"] = ServiceDeskSyncOptions.SerializeHangfireInterval(opts.HangfireInterval, opts.HangfireEnabled),
            ["servicedesk.sync.hangfire.maxParallelism"] = opts.HangfireMaxParallelism.ToString(),
            ["servicedesk.sync.hangfire.archiveGraceDays"] = opts.ArchiveGraceDays.ToString(),
            ["servicedesk.sync.editor.freshnessMinutes"] = ((int)opts.EditorFreshness.TotalMinutes).ToString(),
            ["servicedesk.sync.timeout.seconds"] = ((int)opts.TimeoutPerTicket.TotalSeconds).ToString(),
        };

        var existing = await db.ServiceDeskSyncSettings.ToDictionaryAsync(x => x.Klic, ct);
        var now = DateTime.UtcNow;

        foreach (var kv in values)
        {
            if (existing.TryGetValue(kv.Key, out var row))
            {
                row.Hodnota = kv.Value;
                row.UpdatedAt = now;
                row.UpdatedByOsobaId = editorOsobaId;
            }
            else
            {
                db.ServiceDeskSyncSettings.Add(new ServiceDeskSyncSettingsEntity
                {
                    Klic = kv.Key,
                    Hodnota = kv.Value,
                    UpdatedAt = now,
                    UpdatedByOsobaId = editorOsobaId
                });
            }
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);
    }
}

// alias pro testy — zkrácený název
public sealed class ServiceDeskSyncSettingsWithCache : ServiceDeskSyncSettingsImpl
{
    public ServiceDeskSyncSettingsWithCache(PmTrackerDbContext db, IMemoryCache cache)
        : base(db, cache) { }
}
```

**POZOR:** Test používá `ServiceDeskSyncSettingsImpl` přímo. Pokud má být třída `sealed` a interní, uprav podle návyků projektu. Pokud repo používá `internal sealed`, změň viditelnost + přidej `InternalsVisibleTo` pro test projekt.

- [ ] **Step 5: Registrovat v DI**

V `PmTracker.Web/Program.cs` najdi existující DI registrace (např. kolem `IHarvestScheduler`):

Přidej:

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IServiceDeskSyncSettings, ServiceDeskSyncSettingsImpl>();
```

`AddMemoryCache()` už možná volané je — grep pro jistotu.

- [ ] **Step 6: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "ServiceDeskSyncSettingsTests" --no-restore`

Expected: 0 errors, 3/3 passed.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/IServiceDeskSyncSettings.cs \
        PmTracker.Web/Services/ServiceDesk/ServiceDeskSyncSettings.cs \
        PmTracker.Web/Program.cs \
        PmTracker.Tests.Unit/ServiceDesk/ServiceDeskSyncSettingsTests.cs
git commit -m "feat(servicedesk-sync): IServiceDeskSyncSettings cached reader/writer + DI"
```

---

## Task 5: ViewModel + Controller GET/POST Save

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/ServiceDeskSync/ServiceDeskSyncViewModel.cs`
- Create: `PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/NastaveniServiceDeskSyncControllerTests.cs`

- [ ] **Step 1: Napsat failující testy**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/NastaveniServiceDeskSyncControllerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels.ServiceDeskSync;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class NastaveniServiceDeskSyncControllerTests
{
    [Fact]
    public async Task Index_ReturnsCurrentOptionsAsViewModel()
    {
        var settings = new Mock<IServiceDeskSyncSettings>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDeskSyncOptions.Default());
        var sut = new NastaveniServiceDeskSyncController(settings.Object);

        var result = await sut.Index(CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<ServiceDeskSyncViewModel>().Subject;
        vm.Enabled.Should().BeTrue();
        vm.HangfireInterval.Should().Be("1h");
        vm.HangfireMaxParallelism.Should().Be(4);
    }

    [Fact]
    public async Task Save_ValidViewModel_CallsSettingsSaveAndRedirects()
    {
        var settings = new Mock<IServiceDeskSyncSettings>();
        settings.Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceDeskSyncOptions.Default());
        var sut = new NastaveniServiceDeskSyncController(settings.Object);

        var vm = new ServiceDeskSyncViewModel
        {
            Enabled = true,
            HangfireInterval = "3h",
            HangfireMaxParallelism = 8,
            ArchiveGraceDays = 14,
            EditorFreshnessMinutes = 10,
            TimeoutSeconds = 60
        };

        var result = await sut.Save(vm, CancellationToken.None);

        settings.Verify(x => x.SaveAsync(
            It.Is<ServiceDeskSyncOptions>(o =>
                o.HangfireInterval == TimeSpan.FromHours(3)
                && o.HangfireMaxParallelism == 8
                && o.ArchiveGraceDays == 14
                && o.EditorFreshness == TimeSpan.FromMinutes(10)
                && o.TimeoutPerTicket == TimeSpan.FromSeconds(60)),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        result.Should().BeOfType<RedirectToActionResult>();
    }
}
```

- [ ] **Step 2: Spustit testy (COMPILATION ERROR)**

Run: `dotnet test PmTracker.Tests.Unit --filter "NastaveniServiceDeskSyncControllerTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Vytvořit ViewModel**

Vytvoř `PmTracker.Web/Models/ViewModels/ServiceDeskSync/ServiceDeskSyncViewModel.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels.ServiceDeskSync;

public sealed class ServiceDeskSyncViewModel
{
    public bool Enabled { get; set; } = true;

    [Required]
    public string HangfireInterval { get; set; } = "1h";

    [Range(1, 16)]
    public int HangfireMaxParallelism { get; set; } = 4;

    [Range(0, 30)]
    public int ArchiveGraceDays { get; set; } = 7;

    [Range(0, 60)]
    public int EditorFreshnessMinutes { get; set; } = 5;

    [Range(5, 120)]
    public int TimeoutSeconds { get; set; } = 30;

    public IReadOnlyList<string> IntervalOptions => new[]
    {
        "disabled", "15min", "30min", "1h", "3h", "6h", "12h"
    };
}
```

- [ ] **Step 4: Vytvořit Controller**

Vytvoř `PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels.ServiceDeskSync;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Controllers;

[Authorize]
[Route("Nastaveni/ServiceDeskSync")]
public sealed class NastaveniServiceDeskSyncController(
    IServiceDeskSyncSettings settings) : Controller
{
    // TODO: ACL guard — přidat [RequireAppAdmin] attribute nebo podobnou filter politiku
    //       ze stávajícího authz modulu; dělá se v Task 7.

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var opts = await settings.GetAsync(ct);
        var vm = new ServiceDeskSyncViewModel
        {
            Enabled = opts.Enabled,
            HangfireInterval = ServiceDeskSyncOptions.SerializeHangfireInterval(
                opts.HangfireInterval, opts.HangfireEnabled),
            HangfireMaxParallelism = opts.HangfireMaxParallelism,
            ArchiveGraceDays = opts.ArchiveGraceDays,
            EditorFreshnessMinutes = (int)opts.EditorFreshness.TotalMinutes,
            TimeoutSeconds = (int)opts.TimeoutPerTicket.TotalSeconds
        };
        return View("~/Views/Nastaveni/_ServiceDeskSyncCard.cshtml", vm);
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ServiceDeskSyncViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Nastaveni/_ServiceDeskSyncCard.cshtml", vm);
        }

        var (hfEnabled, hfInterval) = ServiceDeskSyncOptions.ParseHangfireInterval(vm.HangfireInterval);
        var opts = new ServiceDeskSyncOptions(
            Enabled: vm.Enabled,
            HangfireEnabled: hfEnabled,
            HangfireInterval: hfInterval,
            HangfireMaxParallelism: vm.HangfireMaxParallelism,
            ArchiveGraceDays: vm.ArchiveGraceDays,
            EditorFreshness: TimeSpan.FromMinutes(vm.EditorFreshnessMinutes),
            TimeoutPerTicket: TimeSpan.FromSeconds(vm.TimeoutSeconds));

        // TODO: resolve current user Id → editorOsobaId z CurrentUserContextViewModel
        await settings.SaveAsync(opts, editorOsobaId: null, ct);

        return RedirectToAction(nameof(Index));
    }
}
```

**TODO řešíme v Task 7** (ACL + current user Id). Jsou to jediné placeholdery v plánu a jsou navázané na následující task; akceptuji je sem.

- [ ] **Step 5: Install Moq pokud chybí**

Run: `grep "Moq" PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj`

Pokud prázdné: `dotnet add PmTracker.Tests.Unit package Moq`.

- [ ] **Step 6: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "NastaveniServiceDeskSyncControllerTests" --no-restore`

Expected: 0 build errors, 2/2 passed.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/ServiceDeskSync/ServiceDeskSyncViewModel.cs \
        PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs \
        PmTracker.Tests.Unit/ServiceDesk/NastaveniServiceDeskSyncControllerTests.cs
git commit -m "feat(servicedesk-sync): VM + controller Index/Save (ACL doplní další task)"
```

---

## Task 6: Razor view `_ServiceDeskSyncCard.cshtml` + CSS

**Files:**
- Create: `PmTracker.Web/Views/Nastaveni/_ServiceDeskSyncCard.cshtml`
- Create: `PmTracker.Web/wwwroot/css/components/servicedesk-sync-card.css`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (`@import`)

- [ ] **Step 1: Vytvořit CSS**

Vytvoř `PmTracker.Web/wwwroot/css/components/servicedesk-sync-card.css`:

```css
.sd-sync-card {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  max-width: 720px;
  padding: 1.5rem;
  background: var(--pm-surface, #ffffff);
  border: 1px solid var(--pm-border, #e2e8f0);
  border-radius: 8px;
}

.sd-sync-card h2 { margin: 0; font-size: 1.25rem; }

.sd-sync-card .sd-sync-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding-top: 1rem;
  border-top: 1px solid var(--pm-border, #e2e8f0);
}

.sd-sync-card .sd-sync-section:first-of-type { padding-top: 0; border-top: none; }

.sd-sync-card label {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  font-size: 0.9375rem;
}

.sd-sync-card label .hint {
  font-size: 0.8125rem;
  color: var(--pm-text-muted, #64748b);
}

.sd-sync-card .sd-sync-actions {
  display: flex;
  gap: 0.5rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--pm-border, #e2e8f0);
}
```

- [ ] **Step 2: Přidat import do site.css**

Použij Edit:

```
old_string: @import "components/externi-odkaz-card.css";
new_string: @import "components/externi-odkaz-card.css";
@import "components/servicedesk-sync-card.css";
```

(Předpokládá se, že `externi-odkaz-card.css` je z Plánu B hotový.)

- [ ] **Step 3: Vytvořit Razor view**

Vytvoř `PmTracker.Web/Views/Nastaveni/_ServiceDeskSyncCard.cshtml`:

```razor
@model PmTracker.Web.Models.ViewModels.ServiceDeskSync.ServiceDeskSyncViewModel

<div class="sd-sync-card" data-sd-sync-card>
    <h2>Synchronizace ServiceDesku</h2>

    <form asp-action="Save" method="post" data-sd-sync-form>
        @Html.AntiForgeryToken()

        <section class="sd-sync-section">
            <label>
                <span>Master kill-switch</span>
                <span class="hint">Když vypnuto, žádná synchronizace ani harvest neběží. Lookup při psaní Čísla zůstává.</span>
                <gov-switch>
                    <input type="checkbox" asp-for="Enabled" />
                </gov-switch>
            </label>
        </section>

        <section class="sd-sync-section">
            <h3>Periodický harvest (Hangfire)</h3>

            <label>
                <span>Interval</span>
                <span class="hint">Jak často backend prochází tickety a hledá nové vyjádření. Doporučení pro produkci: 1–3 hodiny.</span>
                <select asp-for="HangfireInterval" class="pm-select">
                    @foreach (var opt in Model.IntervalOptions)
                    {
                        <option value="@opt" selected="@(opt == Model.HangfireInterval)">
                            @(opt switch {
                                "disabled" => "Vypnuto",
                                "15min" => "Každých 15 minut",
                                "30min" => "Každých 30 minut",
                                "1h" => "Každou hodinu",
                                "3h" => "Každé 3 hodiny",
                                "6h" => "Každých 6 hodin",
                                "12h" => "Každých 12 hodin",
                                _ => opt
                            })
                        </option>
                    }
                </select>
            </label>

            <label>
                <span>Max paralelních requestů do ServiceDesku</span>
                <span class="hint">1–16. Vyšší hodnota = rychlejší průchod, ale větší zátěž ServiceDesku.</span>
                <input type="number" asp-for="HangfireMaxParallelism" min="1" max="16" />
            </label>

            <label>
                <span>Grace window po archivaci (dny)</span>
                <span class="hint">Po archivaci tiketu ho harvest ještě N dní kontroluje kvůli pozdním vyjádřením. Starší archivy se přeskočí.</span>
                <input type="number" asp-for="ArchiveGraceDays" min="0" max="30" />
            </label>
        </section>

        <section class="sd-sync-section">
            <h3>Proaktivní harvest (při otevření editoru)</h3>

            <label>
                <span>Čerstvost tiketu (minuty)</span>
                <span class="hint">Když je tiket harvestnutý do N minut, proaktivní harvest se přeskočí. 0 = vždy harvestuj.</span>
                <input type="number" asp-for="EditorFreshnessMinutes" min="0" max="60" />
            </label>
        </section>

        <section class="sd-sync-section">
            <h3>Timeout</h3>

            <label>
                <span>Timeout per tiket (sekundy)</span>
                <span class="hint">5–120. Harvest jednoho tiketu se po této době přeruší.</span>
                <input type="number" asp-for="TimeoutSeconds" min="5" max="120" />
            </label>
        </section>

        <div class="sd-sync-actions">
            <button type="submit" class="pm-button primary">Uložit změny</button>
            <a href="@Url.Action("Index", "NastaveniServiceDeskSync")" class="pm-button ghost">Zrušit</a>
        </div>
    </form>
</div>
```

- [ ] **Step 4: Ověřit, že view se spustí (build projde)**

Run: `dotnet build PmTracker.Web --no-restore`

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Nastaveni/_ServiceDeskSyncCard.cshtml \
        PmTracker.Web/wwwroot/css/components/servicedesk-sync-card.css \
        PmTracker.Web/wwwroot/css/site.css
git commit -m "feat(servicedesk-sync): Razor view + CSS pro admin kartu"
```

---

## Task 7: ACL guard + navigace v `/Nastaveni`

**Files:**
- Modify: `PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs` (přidat ACL filter)
- Modify: `PmTracker.Web/Views/Nastaveni/Index.cshtml` nebo příslušný navigation partial (přidat položku menu)

- [ ] **Step 1: Najít existující ACL pattern**

Run: `grep -n "IAuthorizationService\|RequirePermission\|HasPermission\|app_admin\|SuperAdmin" PmTracker.Web/Controllers/NastaveniController.cs | head -15`

Expected: vidíš pattern, jak NastaveniController ověřuje admin role.

- [ ] **Step 2: Aplikovat stejný ACL na `NastaveniServiceDeskSyncController`**

V `NastaveniServiceDeskSyncController`:
- Injectnout `CurrentUserContextResolver` nebo ekvivalentní službu, kterou používá `NastaveniController`.
- Na začátku každé metody ověřit, že uživatel je `app_admin` nebo `SuperAdmin`. Pokud ne → `Forbid()`.

Konkrétní kód je závislý na existujícím vzoru — opíráme se o `NastaveniController` jako referenci.

Příklad (uprav podle reálného pattern):

```csharp
private async Task<IActionResult?> EnforceAdminAsync(CancellationToken ct)
{
    var user = await _userContext.GetCurrentAsync(ct);
    if (!user.IsAppAdmin && !user.IsSuperAdmin)
    {
        return Forbid();
    }
    return null;
}

public async Task<IActionResult> Index(CancellationToken ct)
{
    var guard = await EnforceAdminAsync(ct);
    if (guard is not null) return guard;
    // ... původní kód
}
```

- [ ] **Step 3: Doplnit `editorOsobaId` z current usera**

Místo `editorOsobaId: null` ve volání `settings.SaveAsync` předat aktuální `user.OsobaId`.

- [ ] **Step 4: Přidat položku do navigace**

Najít soubor s navigací v `/Nastaveni` (menu sekcí):

Run: `grep -rn "section=\"authz\"\|data-nastaveni-section\|section=authz" PmTracker.Web/Views/Nastaveni/ 2>/dev/null | head -10`

V navigačním partialu přidej mezi existující položky:

```razor
@if (Model.CurrentUser.IsAppAdmin || Model.CurrentUser.IsSuperAdmin)
{
    <a asp-controller="NastaveniServiceDeskSync" asp-action="Index"
       class="nastaveni-nav-item">
        Synchronizace ServiceDesku
    </a>
}
```

(Konkrétní klassy + model property podle stávajícího pattern.)

- [ ] **Step 5: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "NastaveniServiceDeskSync" --no-restore`

Expected: 0 errors, existing tests passed.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs \
        PmTracker.Web/Views/Nastaveni/
git commit -m "feat(servicedesk-sync): ACL guard + navigace v /Nastaveni menu"
```

---

## Task 8: Endpoint `RunNow` (force Hangfire trigger)

**Files:**
- Modify: `PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs`

- [ ] **Step 1: Přidat endpoint**

V `NastaveniServiceDeskSyncController`:

```csharp
[HttpPost("RunNow")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RunNow(CancellationToken ct)
{
    var guard = await EnforceAdminAsync(ct);
    if (guard is not null) return guard;

    // Force Hangfire trigger (přes IHarvestScheduler — stub z Plánu B vrátí Task.CompletedTask,
    // reálná Plán C implementace spustí batch immediate).
    // TODO: Plán C přidá IHarvestScheduler.ForceBatchAsync()
    //       Pro Plán E stačí: redirect s flash message "V Plánu B je to stub — funkční v Plánu C".

    TempData["SyncRunNowStatus"] = "Force trigger byl předán do Hangfire (stub v Plánu B; reálný trigger v Plánu C).";
    return RedirectToAction(nameof(Index));
}
```

- [ ] **Step 2: Zobrazit flash message ve view**

Upravit `_ServiceDeskSyncCard.cshtml` na začátek (za `<h2>`):

```razor
@if (TempData["SyncRunNowStatus"] is string flashMsg)
{
    <div class="pm-flash pm-flash--info">@flashMsg</div>
}
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs \
        PmTracker.Web/Views/Nastaveni/_ServiceDeskSyncCard.cshtml
git commit -m "feat(servicedesk-sync): endpoint RunNow + flash message (stub do Plánu C)"
```

---

## Task 9: Endpoint `Log` (poslední Hangfire runs)

**Files:**
- Modify: `PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs`

Hangfire má svůj dashboard `/hangfire` který ukazuje historii jobů. V Plánu E nebudeme duplikovat — jen vrátíme **prázdný** placeholder JSON endpoint, který Plán C naplní.

- [ ] **Step 1: Přidat placeholder endpoint**

```csharp
[HttpGet("Log")]
public async Task<IActionResult> Log(int limit = 50, CancellationToken ct = default)
{
    var guard = await EnforceAdminAsync(ct);
    if (guard is not null) return guard;

    // Placeholder — Plán C naplní skutečnými daty z Hangfire job history.
    var placeholder = new[]
    {
        new
        {
            Datum = DateTime.UtcNow,
            ZpracovaneTickety = 0,
            Chyby = 0,
            PrumernaDobaMs = 0,
            Status = "Plán C ještě neběží; log bude naplněn po Plánu C."
        }
    };
    return Ok(placeholder);
}
```

- [ ] **Step 2: Commit**

```bash
git add PmTracker.Web/Controllers/NastaveniServiceDeskSyncController.cs
git commit -m "feat(servicedesk-sync): Log endpoint placeholder (naplní Plán C)"
```

---

## Task 10: Playwright smoke test

**Files:**
- Create: `/tmp/playwright-sd-sync-admin.js`

- [ ] **Step 1: Spustit dev server**

```bash
pkill -f "PmTracker.Web/bin" 2>/dev/null; sleep 1
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web" && ASPNETCORE_ENVIRONMENT=Development dotnet run > /tmp/pmtracker-run.log 2>&1 &
for i in 1 2 3 4 5 6 7 8 9 10; do
  if curl -sSf -o /dev/null http://localhost:5071/ 2>/dev/null; then echo READY; break; fi
  sleep 2
done
```

- [ ] **Step 2: Vytvořit Playwright skript**

Vytvoř `/tmp/playwright-sd-sync-admin.js`:

```js
const { chromium } = require('playwright');
const TARGET_URL = 'http://localhost:5071';
const AS_USER = 'pavel.admin@pmtracker.local';

(async () => {
  const browser = await chromium.launch({ headless: false, slowMo: 100 });
  const ctx = await browser.newContext({ viewport: { width: 1600, height: 1000 }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

  try {
    await page.goto(`${TARGET_URL}/Nastaveni/ServiceDeskSync?asUser=${encodeURIComponent(AS_USER)}`,
      { waitUntil: 'networkidle' });
    await page.waitForTimeout(800);
    await page.screenshot({ path: '/tmp/sd-sync-01-default.png', fullPage: true });

    // Změň interval na 3h
    const select = page.locator('select[name="HangfireInterval"]');
    await select.selectOption('3h');

    // Změň freshness na 10
    await page.fill('input[name="EditorFreshnessMinutes"]', '10');

    // Save
    await page.click('button[type="submit"]');
    await page.waitForLoadState('networkidle');
    await page.screenshot({ path: '/tmp/sd-sync-02-after-save.png', fullPage: true });

    // Reload a ověř, že hodnoty přetrvaly
    await page.reload({ waitUntil: 'networkidle' });
    const intervalValue = await page.locator('select[name="HangfireInterval"]').inputValue();
    const freshnessValue = await page.locator('input[name="EditorFreshnessMinutes"]').inputValue();
    console.log('Interval po reload:', intervalValue);
    console.log('Freshness po reload:', freshnessValue);

    console.log('\nConsole errors:', errors.length);
    errors.slice(0, 10).forEach(e => console.log(' ❌ ' + e.slice(0, 200)));
  } finally {
    await browser.close();
  }
})();
```

- [ ] **Step 3: Spustit**

```bash
cd ~/.claude/plugins/cache/playwright-skill/playwright-skill/*/skills/playwright-skill && \
  node run.js /tmp/playwright-sd-sync-admin.js 2>&1 | tail -20
```

Expected:
- `Interval po reload: 3h`
- `Freshness po reload: 10`
- Žádné console errors (favicon 404 OK).

- [ ] **Step 4: Reset hodnot zpět (ať Dev DB zůstane na defaults)**

Otevři stránku znovu, změň zpět na `1h` a `5`, ulož. Nebo přímo:

```bash
cd /tmp/run-sql && dotnet run /dev/stdin <<'SQL'
UPDATE dbo.servicedesk_sync_settings SET hodnota = '1h' WHERE klic = 'servicedesk.sync.hangfire.interval';
UPDATE dbo.servicedesk_sync_settings SET hodnota = '5' WHERE klic = 'servicedesk.sync.editor.freshnessMinutes';
SELECT klic, hodnota FROM dbo.servicedesk_sync_settings ORDER BY klic;
SQL
```

- [ ] **Step 5: Zastavit dev server**

```bash
pkill -f "PmTracker.Web/bin" 2>/dev/null
```

- [ ] **Step 6: Žádný commit** (Playwright skript v `/tmp`).

---

## Task 11: Full build + test + git clean

- [ ] **Step 1: Full build**

Run: `dotnet build --no-restore -c Debug`

Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Full test**

Run: `dotnet test PmTracker.Tests.Unit --no-restore --no-build`

Expected: all passed. Plán A + B + E přidaly ~15 testů.

- [ ] **Step 3: Git status clean + log**

Run: `git status && git log --oneline | head -20`

Expected: čistý strom, ~9 commitů z Plánu E + předchozí z A + B.

---

## Hotovo — Plán E

Po dokončení máš:
- ✅ Tabulka `servicedesk_sync_settings` + seed 6 klíčů.
- ✅ `IServiceDeskSyncSettings` cached reader/writer.
- ✅ Admin karta v `/Nastaveni/ServiceDeskSync` — gov komponenty, 6 polí + save.
- ✅ ACL: jen `app_admin` / `SuperAdmin`.
- ✅ Placeholder endpointy `RunNow` + `Log` — funkční napojení dodá Plán C.
- ✅ Playwright smoke ověřil persistence přes reload.

### Mimo scope (Plán C)
- Reálná Hangfire-based implementace `IHarvestScheduler` (Plán B dodal jen stub).
- Reálný `RunNow` (trigger batch jobu).
- Reálný `Log` (Hangfire job history view).
- `servicedesk.sync.enabled = false` master killswitch skutečně blokuje harvest — až po Plánu C.
