# Sync infrastruktura + AD konzument — Design Spec

**Datum:** 2026-04-22
**Branch:** `codex/senior-refactor-fase-1`
**Status:** 🆕 nová feature, spec for review
**Související inbox:** `docs/superpowers/plans/2026-04-20-upravy-inbox.md` Úpravy #14 + #15
**Související specy (planned revision):** `docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md` §8.2 + §8.6
**Související plány (planned revision):** `docs/superpowers/plans/2026-04-21-chat-modal-harvest-core.md` Task 10/11 + `docs/superpowers/plans/2026-04-21-admin-servicedesk-sync-nastaveni.md`

---

## 1. Kontext a motivace

PM Tracker potřebuje **periodicky aktualizovat data osob** z Active Directory (jméno, email, organizace — dnes se zapíšou jen při první výběru osoby z AD pickeru a dál nestárnou). Druhý příbuzný use case je **periodický harvest ServiceDesk tiketů** (chat vyjádření, změny stavu). Oba typy úloh sdílí strukturu:
- scheduled loop s nastavitelnou periodou + anchor time
- reactive trigger z business událostí (výběr osoby, úprava záznamu)
- admin UI pro enable/disable, parametry a status

Místo duplikace dvou samostatných řešení (ručně psaný `BackgroundService` pro AD, Hangfire pro SD) navrhuje tento spec **sdílenou sync infrastrukturu** jako první-tř-rate abstrakci v projektu. AD sync bude první konzument; ServiceDesk harvest druhý. Navrhovaná infra pokrývá oba potřebné patterny — scheduled tick a reactive queue — bez externí frameworkové závislosti.

### 1.1 Prečo ne Hangfire

Hangfire je vynikající pro scénáře s persistentní retry queue, distribuovanými workery a ops dashboardem. Původní uživatelský požadavek v inboxu #14 explicitně tyhle features NE-uje:
> Perzistentní retry queue (Hangfire-like) — NE, nepotřebujeme.
> Distributed locking — NE, aplikace je single-instance.
> Dashboard UI pro ops — NE, status panel v Nastavení stačí.

Plus: user chce **anchor time + aligned schedule** (např. „každou hodinu od 14:38"), což Hangfire cron nativně neumí. Vlastní 500-LOC infra pokryje přesně požadované features, bez ~10 Hangfire system-tabulek v aplikační DB a bez nové NuGet dependency.

### 1.2 Scope tohoto specu

- ✅ **Sdílená sync infrastruktura** — full design (periodic + reactive patterns)
- ✅ **AD konzument** — full design (1 periodic + 2 reactive triggery)
- ✅ **SD konzument** — pouze **náčrt mapování** na sdílenou infra (2 periodic + 4 reactive + 2 direct sync). Revize SD spec §8.6 a Plánu C/E přijde po scheulingu tohoto specu/plánu; detaily SD zůstanou v revidovaném SD specu.
- ❌ Reálná implementace AD i SD — **jde po tomto plánu**, v samostatné PR.

---

## 2. Sdílená sync infrastruktura

Nový namespace `PmTracker.Web.Services.Sync`. Všechny komponenty jsou _pure-play .NET 8_, využívají `TimeProvider` a `IServiceScopeFactory` tak jak to dělá stávající [SearchReindexHostedService](PmTracker.Web/Services/Search/SearchReindexHostedService.cs).

### 2.1 `SyncScheduleCalculator` — aligned-schedule algoritmus

Pure static, zero I/O, unit-testable. Jedna metoda:

```csharp
namespace PmTracker.Web.Services.Sync;

public static class SyncScheduleCalculator
{
    /// <summary>
    /// Vrátí okamžik dalšího ticku zarovnaný na anchor + násobek periody.
    /// </summary>
    /// <param name="now">Aktuální čas (UTC)</param>
    /// <param name="anchorAt">Anchor čas (absolutní okamžik)</param>
    /// <param name="period">Perioda (musí být > TimeSpan.Zero)</param>
    /// <returns>Okamžik, kdy má proběhnout další tick</returns>
    public static DateTimeOffset ComputeNext(
        DateTimeOffset now,
        DateTimeOffset anchorAt,
        TimeSpan period);
}
```

**Algoritmus** (z inboxu #14):
```
if now <= anchorAt:
    next = anchorAt
else:
    elapsed = now - anchorAt
    ticks   = ceil(elapsed / period)
    next    = anchorAt + ticks * period
```

**Edge cases (všechny pokryty testy):**
- `period <= TimeSpan.Zero` → throw `ArgumentOutOfRangeException`
- `now == anchorAt` → return anchorAt (okamžitě spustit)
- `now < anchorAt` (anchor v budoucnosti) → return anchorAt
- Period > 24h (např. 48h) — funguje stejně, tick násobek periody od anchoru
- Přechod přes půlnoc / DST — `DateTimeOffset` operace jsou absolute, tj. bez DST artefaktů; algorithm pracuje v UTC

### 2.2 `ISyncJobSettings` — interface pro typed per-job entity

```csharp
public interface ISyncJobSettings
{
    // Konfigurace
    bool IsEnabled { get; set; }
    int PeriodMinutes { get; set; }              // min 5, validace na vstupu
    DateTimeOffset AnchorAt { get; set; }         // UTC

    // Status posledního běhu
    DateTime? LastRunAt { get; set; }             // UTC; == finishedAt běhu
    string? LastTriggerKind { get; set; }         // "auto" | "manual"
    string? LastResultJson { get; set; }          // per-job payload (viz 2.2.1)

    // Viditelnost právě běžícího ticku
    bool IsRunning { get; set; }
    DateTime? RunStartedAt { get; set; }          // UTC

    // Audit
    DateTime UpdatedAt { get; set; }
    int? UpdatedByOsobaId { get; set; }
}
```

Každý konzument definuje vlastní typed entity (AD má `AdSyncSettingsEntity`, SD má dvě: archive + non-archive). Rows jsou **singleton** per job — v tabulce je vždy právě 1 řádek, `Id = 1` (seedovaný).

#### 2.2.1 `LastResultJson` — per-job shape

JSON blob, shape je volný per konzument. Pro AD sync:
```json
{
  "startedAt": "2026-04-22T14:25:00Z",
  "finishedAt": "2026-04-22T14:25:07Z",
  "durationMs": 7412,
  "okCount": 132,
  "errorCount": 2,
  "skippedNoGuidCount": 5,
  "notFoundInAdCount": 1,
  "errors": [
    { "osobaId": 42, "guidAd": "abc…", "reason": "LDAP timeout" }
  ]
}
```

Shared UI partial zobrazuje jen generické fieldy (startedAt, durationMs, souhrn okCount/errorCount + první 3 errors). Per-job detail view si může přidat vlastní rendering.

### 2.3 `SyncHostedServiceBase<TSettings>` — abstract generic loop

```csharp
public abstract class SyncHostedServiceBase<TSettings> : BackgroundService
    where TSettings : class, ISyncJobSettings
{
    protected abstract string JobKey { get; }
    protected abstract Task<TSettings> LoadSettingsAsync(PmTrackerDbContext db, CancellationToken ct);
    protected abstract Task RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct);

    // Orchestrace:
    //  - catch-up on startup (if LastRunAt == null || now - LastRunAt > period)
    //  - aligned loop přes SyncScheduleCalculator.ComputeNext
    //  - try/finally: set IsRunning/RunStartedAt, clear after
    //  - ISyncJobRunLock semafor guard
    //  - konfig reload při každé iteraci loopu (admin změnil period/anchor → další tick už nový)
    //  - cancellation: OperationCanceledException → clean exit
}

public enum SyncTriggerKind { Auto, Manual }
```

**Orchestrace detailně:**

1. `ExecuteAsync` startuje:
   - Načti `TSettings` přes `LoadSettingsAsync`
   - Pokud `!IsEnabled` → čekat na change (simple polling každých 60s zda user neflipnul switch), pak restart loopu
   - **Catch-up on startup**: `IF LastRunAt == null OR now - LastRunAt > period THEN RunOnceAsync(trigger=Auto)` (bez čekání na anchor)
2. Normal loop:
   - `next = SyncScheduleCalculator.ComputeNext(now, AnchorAt, period)`
   - `await Task.Delay(next - now, ct)` (bez odečtu 1ms — přijatelná drift je ≪ periody)
   - Po probuzení: `RunOnceAsync(trigger=Auto)`
   - Reload settings (protože admin mohl změnit period/anchor/enabled)
3. `RunOnceAsync` kontrakt:
   - Acquire semafor přes `ISyncJobRunLock` (with `TryAcquireAsync` zero timeout → pokud zaběhnuté manuál, skipni tick a log)
   - Set `IsRunning = true, RunStartedAt = now, LastTriggerKind = trigger`, save
   - Spusť abstract body (konzument má v scope přístup k DbContext + služby)
   - V `finally`: `IsRunning = false, LastRunAt = now, LastResultJson = …`, save
   - Release semafor

### 2.4 `ISyncJobRunLock<TSettings>` — concurrency guard

```csharp
public interface ISyncJobRunLock<TSettings>
    where TSettings : class, ISyncJobSettings
{
    /// <summary>Pokusí se zabrat lock bez čekání. True = získáno, False = už běží.</summary>
    bool TryAcquire();
    void Release();
}

internal sealed class SyncJobRunLock<TSettings> : ISyncJobRunLock<TSettings>
    where TSettings : class, ISyncJobSettings
{
    private readonly SemaphoreSlim _sem = new(1, 1);
    public bool TryAcquire() => _sem.Wait(0);
    public void Release() => _sem.Release();
}
```

Registruje se jako **singleton** per generic T (DI: `AddSingleton(typeof(ISyncJobRunLock<>), typeof(SyncJobRunLock<>))`) → každá `TSettings` má vlastní semafor v aplikaci.

**Kde se používá:**
- `SyncHostedServiceBase` v `RunOnceAsync` — TryAcquire/Release
- Manual "Spustit teď" endpoint v controlleru — stejný vzor

Když manuál vyhraje semafor, tick čeká na další periodu. Když tick vyhraje, manuál dostane "už běží, zkus za chvíli" message.

### 2.5 `ReactiveSyncQueue<TRequest>` — FIFO pro fire-and-forget reactive triggery

Interface + impl nad `System.Threading.Channels.Channel<T>`:

```csharp
public interface IReactiveSyncQueue<TRequest>
{
    ValueTask EnqueueAsync(TRequest request, CancellationToken ct = default);
    IAsyncEnumerable<TRequest> ReadAllAsync(CancellationToken ct = default);
    int PendingCount { get; }  // pro status panel
}

internal sealed class ReactiveSyncQueue<TRequest> : IReactiveSyncQueue<TRequest>
{
    private readonly Channel<TRequest> _channel = Channel.CreateBounded<TRequest>(
        new BoundedChannelOptions(capacity: 1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    // ... EnqueueAsync / ReadAllAsync / PendingCount ...
}
```

**Bounded s DropOldest** — pokud backend zpomalí, queue se nepřetrefí; oldest request odpadne. Pro reactive sync je to přijatelné (další tick/akce si data stejně vyžádá).

Registrace: `AddSingleton(typeof(IReactiveSyncQueue<>), typeof(ReactiveSyncQueue<>))`.

### 2.6 `ReactiveSyncConsumerBase<TRequest>` — abstract BackgroundService čte z queue

```csharp
public abstract class ReactiveSyncConsumerBase<TRequest> : BackgroundService
{
    protected abstract Task HandleAsync(IServiceScope scope, TRequest request, CancellationToken ct);

    // ExecuteAsync: await foreach (var req in queue.ReadAllAsync(ct))
    // per request: create scope, call HandleAsync, catch exception (log + pokračuj)
}
```

Každý konzument (AD reactive, SD reactive) odvodí vlastní třídu a override `HandleAsync`.

### 2.7 `_SyncJobSettingsCard.cshtml` — sdílený Razor partial

Parametrizováno přes view model:

```csharp
public sealed class SyncJobSettingsCardViewModel
{
    public string Title { get; init; } = "";
    public string JobKey { get; init; } = "";             // pro form action routing
    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; }
    public DateTimeOffset AnchorAt { get; set; }
    public DateTime? LastRunAt { get; init; }
    public string? LastTriggerKind { get; init; }
    public SyncJobResultSummary? LastResult { get; init; }  // deserialized LastResultJson (common fields)
    public bool IsRunning { get; init; }
    public DateTime? RunStartedAt { get; init; }
    public string? StatusDescription { get; init; }       // např. "Právě běží od 14:25 (manual)"
}
```

Partial renderuje: enable switch, period input (number min 5), anchor time-picker, tlačítko „Spustit teď" (POST form), tlačítko „Uložit změny", status panel (LastRunAt, TriggerKind, OK/Error count, IsRunning indikátor).

Controller akce které partial volá (route convention):
- `POST /Nastaveni/Sync/{jobKey}/Save` — uložení config (period, anchor, isEnabled)
- `POST /Nastaveni/Sync/{jobKey}/RunNow` — manuální trigger

Partial je **read-only** pro per-job specific detaily (LastResultJson custom fields) — konzument si může pod partialem dorenderovat vlastní detail sekci.

---

## 3. Konzument AD (first consumer)

### 3.1 Data model

**Nová entita** [PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs](PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs):

```csharp
public sealed class AdSyncSettingsEntity : ISyncJobSettings
{
    public int Id { get; set; }                  // PK, singleton row Id=1
    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; } = 360;  // default 6h
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

**Nová tabulka** (DB upgrade skript `db_upgrade_1_3_0_ad_sync_settings.sql`):

```sql
CREATE TABLE dbo.ad_sync_settings (
    id                   INT NOT NULL CONSTRAINT PK_ad_sync_settings PRIMARY KEY,
    is_enabled           BIT NOT NULL CONSTRAINT DF_ad_sync_settings_enabled DEFAULT (0),
    period_minutes       INT NOT NULL CONSTRAINT DF_ad_sync_settings_period DEFAULT (360),
    anchor_at            DATETIMEOFFSET NOT NULL,
    last_run_at          DATETIME2 NULL,
    last_trigger_kind    NVARCHAR(16) NULL,
    last_result_json     NVARCHAR(MAX) NULL,
    is_running           BIT NOT NULL CONSTRAINT DF_ad_sync_settings_running DEFAULT (0),
    run_started_at       DATETIME2 NULL,
    updated_at           DATETIME2 NOT NULL CONSTRAINT DF_ad_sync_settings_updated DEFAULT SYSUTCDATETIME(),
    updated_by_osoba_id  INT NULL
        CONSTRAINT FK_ad_sync_settings_osoba REFERENCES dbo.osoby(id)
);

-- Seed singleton row
INSERT INTO dbo.ad_sync_settings (id, is_enabled, period_minutes, anchor_at)
VALUES (1, 0, 360, SYSUTCDATETIMEOFFSET());
```

### 3.2 Rozšíření `IActiveDirectoryService`

Přidat metodu [PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs](PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs):

```csharp
Task<ActiveDirectoryBatchResponse> ListByGuidsAsync(
    IReadOnlyCollection<Guid> guids,
    CancellationToken ct = default);
```

Implementace:
- Rozdělit guids na batches po 100 (LDAP filter string má limity)
- Pro každý batch volat LDAP s filtrem `(&(objectClass=user)(|(objectGUID=...)(objectGUID=...)))`
- Výsledek: `ActiveDirectoryBatchResponse { Available, Message?, Persons: IReadOnlyList<ActiveDirectoryPersonResult>, NotFoundGuids: IReadOnlyList<Guid> }`
- Platform unavailable (non-Windows) → `Available = false`, `NotFoundGuids = input.guids`

### 3.3 `IAdSyncService` — service layer

```csharp
public interface IAdSyncService
{
    /// <summary>
    /// Synchronizuje všechny osoby s GuidAd z AD. Volá se z periodic hosted service
    /// nebo z manual "Spustit teď" endpointu.
    /// </summary>
    Task<AdSyncResult> SyncAllPeopleAsync(SyncTriggerKind trigger, CancellationToken ct);

    /// <summary>
    /// Synchronizuje jednu osobu z AD. Volá se z reactive queue consumeru.
    /// </summary>
    Task<AdSinglePersonSyncResult> SyncSinglePersonAsync(int osobaId, CancellationToken ct);
}

public sealed record AdSyncResult(
    DateTime StartedAt,
    DateTime FinishedAt,
    int OkCount,
    int ErrorCount,
    int SkippedNoGuidCount,
    int NotFoundInAdCount,
    IReadOnlyList<AdSyncErrorItem> Errors);

public sealed record AdSinglePersonSyncResult(
    bool Success,
    string? FailureReason,
    bool FoundInAd);

public sealed record AdSyncErrorItem(int OsobaId, Guid? GuidAd, string Reason);
```

**Implementace `SyncAllPeopleAsync`:**

1. Load všech `OsobaEntity` s `GuidAd IS NOT NULL`
2. Skip ty bez GuidAd (`skippedNoGuidCount++`)
3. Batched volání `IActiveDirectoryService.ListByGuidsAsync(guidsChunk, ct)` — chunks po 100
4. Pro každý výsledek z AD:
   - Update OsobaEntity: Jmeno, Prijmeni, Titul, Email, Company, Department (= AdLogin se nemění)
   - `okCount++`
5. Pro každý guid v `NotFoundGuids`:
   - Nech OsobaEntity neporušenou (NEPROMAZAT — FK + historie)
   - Log warning, přičti do `notFoundInAdCount`
6. Při výjimce (LDAP timeout, DB write fail) per chunk/osoba:
   - Přidat do `Errors` (top 10 uloženo v LastResultJson)
   - `errorCount++`
7. Return `AdSyncResult`

**Idempotence:** každá iterace upsertuje stejná data — opakované běhy nesmí vytvořit duplicaty ani audit spam. Update audit log entry `{EntityType: "osoba", Action: "ad_sync_update", ChangedFields: [...]}` jen když se fields skutečně změnily (porovnání před-zápisem).

**`SyncSinglePersonAsync`:** volá `IActiveDirectoryService.ListByGuidsAsync` s jedním guidem; pokud osoba nemá GuidAd → return `Success=false, FailureReason="Osoba nemá GuidAd"`.

### 3.4 `AdPeriodicSyncHostedService`

```csharp
public sealed class AdPeriodicSyncHostedService
    : SyncHostedServiceBase<AdSyncSettingsEntity>
{
    protected override string JobKey => "ad.periodic";
    protected override async Task<AdSyncSettingsEntity> LoadSettingsAsync(
        PmTrackerDbContext db, CancellationToken ct)
        => await db.AdSyncSettings.FirstAsync(x => x.Id == 1, ct);

    protected override async Task RunOnceAsync(
        IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IAdSyncService>();
        var result = await svc.SyncAllPeopleAsync(trigger, ct);
        // Parent zapíše LastResultJson = JsonSerializer.Serialize(result)
    }
}
```

DI registrace: `AddHostedService<AdPeriodicSyncHostedService>()`.

### 3.5 AD reactive triggery

Request type:

```csharp
public sealed record AdReactiveSyncRequest(int OsobaId, AdReactiveSource Source);
public enum AdReactiveSource { PersonPicked, ManualUpdate }
```

Consumer:

```csharp
public sealed class AdReactiveSyncConsumer
    : ReactiveSyncConsumerBase<AdReactiveSyncRequest>
{
    protected override async Task HandleAsync(
        IServiceScope scope, AdReactiveSyncRequest req, CancellationToken ct)
    {
        var svc = scope.ServiceProvider.GetRequiredService<IAdSyncService>();
        await svc.SyncSinglePersonAsync(req.OsobaId, ct);
    }
}
```

**Napojení v aplikaci:**

- **Person pick z AD pickeru:** v [PmTracker.Web/Controllers/OsobyController.cs](PmTracker.Web/Controllers/OsobyController.cs) (nebo wherever `CreateFromAd` flow je) → po úspěšném insertu nového `OsobaEntity`:
  ```csharp
  await queue.EnqueueAsync(new AdReactiveSyncRequest(osoba.Id, AdReactiveSource.PersonPicked));
  ```
- **Manuální „Aktualizovat osobu z AD" tlačítko:** nové tlačítko v detailu osoby (jen pro admin s `PermissionKeys.PeopleManage`). Endpoint:
  ```csharp
  [HttpPost("/Osoby/{id:int}/SyncFromAd")]
  public async Task<IActionResult> SyncFromAd(int id, ...)
  {
      await queue.EnqueueAsync(new AdReactiveSyncRequest(id, AdReactiveSource.ManualUpdate));
      return ...;  // flash message "Aktualizace spuštěna na pozadí"
  }
  ```

DI: `AddHostedService<AdReactiveSyncConsumer>()`.

### 3.6 Edge cases AD

| Případ | Chování |
|---|---|
| Osoba bez `GuidAd` | Skip, `skippedNoGuidCount++` |
| Osoba s `GuidAd`, ale AD ji nevrátí | Log warning, `notFoundInAdCount++`, OsobaEntity nezměněná |
| AD server unavailable (non-Windows, timeout) | `AdSyncResult.ErrorCount = 0`, `LastResultJson.errors = [{reason: "AD unavailable"}]`, další tick normálně |
| LDAP timeout na batchi | Celý batch → error; další batch zkus normálně |
| DB write fail na jedné osobě | Per-row exception; ostatní osoby v chunku dojedou; error přidán do `Errors` |
| App restart uprostřed běhu | `IsRunning = true` zůstane stale v DB. Další tick catch-up logic: pokud `now - RunStartedAt > 2×period` → clear stale flag, proceed. Jinak čekej (ale teoreticky nemůže vzniknout, protože semafor se uvolní v finally). |
| Konfig změna za běhu (admin změní period) | Další iterace loopu reloaduje settings, next tick počítán z nové periody |

### 3.7 AD config defaults

Initial seed (při prvním deployi):
- `is_enabled = 0` (admin musí explicitně zapnout)
- `period_minutes = 360` (6h)
- `anchor_at = SYSUTCDATETIMEOFFSET()` (now)

Doporučené produkční hodnoty (v UI jako hint):
- Perioda: 6h (AD data se nemění rychle)
- Anchor: 03:00 UTC (mimo pracovní špičku)

---

## 4. Konzument SD (second consumer — náčrt)

**Detaily SD přijdou v revidovaném SD specu** (`docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md` §8.6 přepsat). Tady jen mapování na shared infra:

### 4.1 Dva periodic joby

| Job | Filter | Default period | Default anchor |
|---|---|---|---|
| `sd.active` | `HOT_ZAZNAMY.stav <> N'archiv'` | 60 min | 00:00 UTC |
| `sd.archive` | `HOT_ZAZNAMY.stav = N'archiv'` | 1440 min (24h) | 04:00 UTC |

Každý má vlastní typed entity (`SdActiveSyncSettingsEntity`, `SdArchiveSyncSettingsEntity`) a vlastní hosted service, oba odvozeny ze `SyncHostedServiceBase<T>`. Oba volají stejný `ISdHarvestService` s parametrem `HarvestScope.Active | HarvestScope.Archive`.

**Grace window ze specu §8.2.4 se DROPne** — user preference („jakmile je záznam v archivu, stačí synchronizovat podle archiv, ne častěji"). Stav-based partition je clean, un-archive případy (1-3% tiketů) řeší next non-archive tick, který uvidí stav změněný.

### 4.2 Fingerprint detekce pro periodic harvest

**Dvoustupňový fingerprint** (detail v §5):

1. **Primary — HOT_ZAZNAMY.datum** (jeden batched dotaz pro všechny kandidáty)
2. **Secondary — HOT_VYJADRENI max_id + count** (jen pro tickety s change na primary)

### 4.3 Reactive triggery (4×)

| Trigger | Source | Request payload |
|---|---|---|
| T2 | Save záznamu s novou/upravenou externí vazbou | `SdReactiveHarvestRequest(zaznamId, reason: "record_save")` |
| T5 | Otevření editoru projektového záznamu | `SdReactiveHarvestRequest(zaznamId, reason: "editor_open")` |
| T7 | Schválení návrhu `CREATE_RECORD` s ext. vazbami | `SdReactiveHarvestRequest(zaznamId, reason: "proposal_approve")` |
| T8 | Otevření Externí vazby / Harmonogram tabu (lazy fallback) | `SdReactiveHarvestRequest(zaznamId, reason: "tab_open")` |

Všechny enqueue do `IReactiveSyncQueue<SdReactiveHarvestRequest>`, consumer volá `ISdHarvestService.HarvestForRecordAsync(zaznamId, ct)`.

### 4.4 Direct sync-call triggery (2×)

T3 a T6 nejdou přes queue — user čeká, spinner běží. Controller volá **přímo** `await ISdHarvestService.HarvestSingleTicketAsync(ticketId, ct)` s `await`.

### 4.5 Revize existujících SD artefaktů (separátní commity, po approvu tohoto specu)

- `docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md` §8.2 + §8.6 — přepsat na typed entity + dva periodic joby + stav-based partition
- `docs/superpowers/plans/2026-04-21-chat-modal-harvest-core.md` Task 10 (Hangfire scheduler) → nahradit `ReactiveHarvestSchedulerAdapter` wrapper nad `IReactiveSyncQueue<SdReactiveHarvestRequest>`. Task 10 `dotnet add package Hangfire.*` odstranit.
- `docs/superpowers/plans/2026-04-21-chat-modal-harvest-core.md` Task 11 (Hangfire batch job) → nahradit dva hosted services odvozené ze `SyncHostedServiceBase<T>`.
- `docs/superpowers/plans/2026-04-21-admin-servicedesk-sync-nastaveni.md` — přepsat celý (11 tasků → ~7 tasků): drop K-V `servicedesk_sync_settings` tabulku, drop parse/serialize helpers, drop cached IMemoryCache reader; použít shared partial `_SyncJobSettingsCard.cshtml`.

Revize plány: `docs/superpowers/plans/2026-04-22-sd-sync-revise.md` (separátní plán pro amendments, vznikne po schválení tohoto specu).

---

## 5. Fingerprint strategie pro SD harvest

Platí pro oba SD periodic joby. Cílem: minimální queries proti HOT DB + korektní detekce změn i u edit-in-place draftů.

### 5.1 Ukládaná data per `zaznam_externi_odkazy` row

Rozšíření existující tabulky (EF migrace):

| Sloupec | Typ | Význam |
|---|---|---|
| `last_known_hot_zaznam_datum` | DATETIME2 NULL | Snapshot `HOT_ZAZNAMY.datum` při posledním syncu |
| `last_known_max_vyjadreni_id` | BIGINT NULL | `MAX(HOT_VYJADRENI.id)` při posledním syncu |
| `last_known_vyjadreni_count` | INT NULL | `COUNT(*) HOT_VYJADRENI` při posledním syncu |
| `last_harvested_at` | DATETIME2 NULL | Timestamp posledního úspěšného harvestu (už ve specu) |

### 5.2 Algoritmus (periodic tick)

**Krok 1 — batched primary check (1 dotaz):**

```sql
SELECT id, datum, stav
FROM dbo.HOT_ZAZNAMY
WHERE id IN (@t1, @t2, ...)   -- ids kandidátů podle filteru stavu
```

Pro každý ticket porovnat:
- `datum == last_known_hot_zaznam_datum` → **primary skip** (netřeba drill do HOT_VYJADRENI)
- `datum != last_known_hot_zaznam_datum` nebo `last_known_hot_zaznam_datum IS NULL` → **drill do sekundární kontroly**

**Krok 2 — sekundární fingerprint (jen pro tickety s change):**

Batched pro tickety s change:
```sql
SELECT hot_zaznam_id, MAX(id) AS max_id, COUNT(*) AS cnt
FROM dbo.HOT_VYJADRENI
WHERE hot_zaznam_id IN (@changed1, @changed2, ...)
GROUP BY hot_zaznam_id
```

Porovnat:
- `max_id == last_known_max_vyjadreni_id AND cnt == last_known_vyjadreni_count` → **secondary skip** (ticket `datum` se změnil, ale vyjádření ne — např. změna stavu bez nového komentáře; update jen `last_known_hot_zaznam_datum`)
- Jinak → **fetch**

**Krok 3 — fetch pro tickety s mismatch:**

```sql
SELECT *
FROM dbo.HOT_VYJADRENI
WHERE hot_zaznam_id = @ticket
  AND (@lastKnownId IS NULL OR id >= @lastKnownId)   -- inclusive kvůli draftu
ORDER BY id ASC;
```

**Krok 4 — upsert + harvest predikáty + update fingerprint:**

- Upsert fetched rows do lokální cache (idempotent, PK `(zaznam_externi_odkaz_id, hot_vyjadreni_id)`)
- Na (new + re-harvested last known) spustit existující harvest predikáty (Plán C Task 6 `HarvestPredicates`)
- Update fingerprint: `last_known_hot_zaznam_datum = hot.datum`, `last_known_max_vyjadreni_id = MAX(fetched.id)`, `last_known_vyjadreni_count = hot.cnt`, `last_harvested_at = now`

### 5.3 Ošetření edge cases

| Případ | Chování |
|---|---|
| Ticket v HOT neexistuje (smazaný) | Primary query ho nevrátí. Log warning `"Ticket #X neexistuje v HOT"`. `last_harvested_at` **neupdatovat** — zkusí se znovu. |
| První sync tiketu | `last_known_hot_zaznam_datum IS NULL` → vždy drill do fetch. Po prvním úspěšném syncu fingerprints zasnapshotované. |
| HOT_ZAZNAMY.datum se NEMĚNÍ při edit-in-place vyjádření | Secondary check zachytí přes `max_id/count`, ale pokud je to edit-in-place (stejné `id`, jen jiný text) → **miss**. Řešení: inclusive fetch `id >= last_known_max_id` zachytí poslední vyjádření (viz níže). |
| Draft edit (vyjádření už existuje, user ho ještě dopisuje) | Re-harvest posledního známého ID (inclusive fetch) ho pokaždé přebere. Cost: 1 extra row na sync per ticket. Idempotent upsert → no-op pokud text stejný. |

### 5.4 Verifikační úloha (součást plánu implementace)

**Před shipem ověřit**, že `HOT_ZAZNAMY.datum` se skutečně mění při:
1. Přidání nového vyjádření do tiketu
2. Změně stavu tiketu
3. Edit-in-place draftu existujícího vyjádření

Test metodou:
- Vyber existující ticket v testovací HOT DB
- Zaznamenej aktuální `datum`
- Proveď změnu (přes SD UI nebo přímo SQL update, pokud je povolené)
- Re-query, ověř, že `datum` se posunul

**Výsledky verifikace:**
- ✅ `datum` se mění při všech třech akcích → strategie (primary HOT_ZAZNAMY.datum) OK
- ⚠️ `datum` se mění jen při některých → strategie stále použitelná jako hint, ale **sekundární fingerprint je nepovinný** (vždy drill)
- ❌ `datum` se nemění konzistentně → **padnout zpět** na `HOT_VYJADRENI` max_id+count jako primary (bez HOT_ZAZNAMY.datum check)

Výsledek se zaznamená do plánu jako task „Ověření HOT_ZAZNAMY.datum semantiky" **před** finalizací harvest-cycle kódu.

---

## 6. Permission & admin UI

### 6.1 Permission

**Existující `PermissionKeys.SettingsManage`** ([PmTracker.Web/Models/ViewModels/SecurityViewModels.cs:24](PmTracker.Web/Models/ViewModels/SecurityViewModels.cs#L24)) — žádný nový permission key není potřeba. User rozhodl 2026-04-21.

Co chrání:
- Čtení `Nastaveni/Sync/*` endpointů — `PermissionKeys.SettingsView` (fallback na view) nebo `SettingsManage` (plný read+write)
- Save config (POST `/Nastaveni/Sync/{jobKey}/Save`) — `PermissionKeys.SettingsManage`
- RunNow (POST `/Nastaveni/Sync/{jobKey}/RunNow`) — `PermissionKeys.SettingsManage`
- Manuální „Aktualizovat osobu z AD" tlačítko — `PermissionKeys.PeopleManage` (admin správa osob, ne admin settings)

### 6.2 UI — Nastavení → Synchronizace

Nová sidebar položka v [PmTracker.Web/Views/Nastaveni/Index.cshtml](PmTracker.Web/Views/Nastaveni/Index.cshtml) — section key `synchronizace` (viditelná pouze při `SettingsView`, funkční akce vyžadují `SettingsManage`).

**Layout panelu:**

```
┌──────────────────────────────────────────────────┐
│ Synchronizace                                    │
├──────────────────────────────────────────────────┤
│ [Karta 1] Synchronizace s AD                     │
│   (shared _SyncJobSettingsCard s JobKey=ad.periodic)│
├──────────────────────────────────────────────────┤
│ [Karta 2] ServiceDesk — aktivní tickety          │
│   (JobKey=sd.active)                             │
├──────────────────────────────────────────────────┤
│ [Karta 3] ServiceDesk — archivní tickety         │
│   (JobKey=sd.archive)                            │
└──────────────────────────────────────────────────┘
```

V tomto specu implementujeme **pouze Kartu 1** (AD). Karty 2 a 3 přijdou s revizí SD (separátní plán).

**Controller**: nový `NastaveniSyncController` s routes:
- `GET /Nastaveni/Sync/{jobKey}` — detail jedné karty (pokud user jde na deep-link)
- `POST /Nastaveni/Sync/{jobKey}/Save` — uložit config
- `POST /Nastaveni/Sync/{jobKey}/RunNow` — manual trigger

`jobKey` routing přes dictionary — controller neví nic o konkrétním typu; rezolvuje handler per jobKey přes DI keyed-service pattern:

```csharp
public interface ISyncJobAdminHandler
{
    string JobKey { get; }
    Task<SyncJobSettingsCardViewModel> LoadAsync(CancellationToken ct);
    Task SaveAsync(SyncJobSettingsInputModel input, int? editorOsobaId, CancellationToken ct);
    Task<ManualRunOutcome> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct);
}

// Registrace keyed:
services.AddScoped<ISyncJobAdminHandler, AdSyncJobAdminHandler>();
// Controller resolve via IEnumerable<ISyncJobAdminHandler> + FirstOrDefault(h => h.JobKey == routeKey)
```

---

## 7. Testování

### 7.1 Unit testy

**`SyncScheduleCalculatorTests`** — 10 cases dle inboxu + edge:

| # | Case | Anchor | Period | Now | Expected Next |
|---|---|---|---|---|---|
| 1 | `now > anchor`, tick 1 | 2026-01-01 00:00 | 12h | 2026-01-01 08:00 | 2026-01-01 12:00 |
| 2 | `now < anchor` | 2026-01-01 16:00 | 1h | 2026-01-01 14:25 | 2026-01-01 16:00 |
| 3 | `now > anchor`, 2× tick | 2026-01-01 14:38 | 1h | 2026-01-01 14:30 | 2026-01-01 14:38 |
| 4 | `now == anchor` | 2026-01-01 12:00 | 1h | 2026-01-01 12:00 | 2026-01-01 12:00 |
| 5 | period = 0 | … | 0 | … | **throw** `ArgumentOutOfRangeException` |
| 6 | period negative | … | -1h | … | **throw** |
| 7 | Crossing midnight | 2026-01-01 23:00 | 2h | 2026-01-02 00:30 | 2026-01-02 01:00 |
| 8 | DST transition day (UTC immune, pouze smoke test) | 2026-03-28 00:00 UTC | 1h | 2026-03-29 02:30 UTC | 2026-03-29 03:00 UTC |
| 9 | Period > 24h | 2026-01-01 00:00 | 48h | 2026-01-02 00:00 | 2026-01-03 00:00 |
| 10 | Very large period (7 days) | 2026-01-01 00:00 | 7 days | 2026-01-05 12:00 | 2026-01-08 00:00 |

### 7.2 Integration testy

**`AdSyncServiceTests`** (fake IActiveDirectoryService):
- `SyncAllPeopleAsync_updates_people_with_guidAd`
- `SyncAllPeopleAsync_skips_people_without_guidAd_and_counts_them`
- `SyncAllPeopleAsync_handles_not_found_in_ad_without_deleting`
- `SyncAllPeopleAsync_batches_requests_per_100_guids`
- `SyncAllPeopleAsync_records_partial_errors_in_result`
- `SyncSinglePersonAsync_returns_failure_when_osoba_has_no_guidAd`
- `SyncSinglePersonAsync_returns_not_found_when_ad_lacks_guid`

**`AdPeriodicSyncHostedServiceTests`** (FakeTimeProvider):
- `ExecutesCatchUpRunOnStartup_whenLastRunAtIsNull`
- `ExecutesCatchUpRunOnStartup_whenLastRunAtOlderThanPeriod`
- `SkipsCatchUp_whenLastRunAtRecent`
- `ExecutesAlignedTicksOverTimeWindow` — posuň fake time o 3× period, ověř 3 volání `SyncAllPeopleAsync`
- `SkipsTick_whenAlreadyRunning` (semafor test)
- `ReloadsSettings_betweenIterations`

### 7.3 TDD postup

Per [superpowers:test-driven-development] — pro každou jednotku: red → green → refactor. Plán bude mít explicitní „napsat failující test" kroky před implementací.

### 7.4 Dependencies

Ověřeno: `Microsoft.Extensions.TimeProvider.Testing` **není** v current dependencies; integration projekt má vlastní `FixedTimeProvider`. **Plán přidá** balíček do `PmTracker.Tests.Unit` pro `FakeTimeProvider.Advance(TimeSpan)` support. Je to Microsoft-supplied, ~15 KB, bez tranzitivních závislostí.

---

## 8. Rollout + migrace

1. **DB migrace** — `db_upgrade_1_3_0_ad_sync_settings.sql` se spustí ručně (user má `/tmp/run-sql` helper pro aplikaci skriptů). Plán obsahuje Task pro aplikaci na Dev DB.
2. **Shared infra** — čistě nový kód, žádný impact na existující features. Žádná data migrace.
3. **AD feature flag** — při prvním deployi je `is_enabled = 0`. Admin v Nastavení → Synchronizace → AD karta explicitně zapne. Do té doby: žádný tick, žádné reactive trigery neběží (konzument si check před enqueue, nebo queue tiše drop-oldest).
4. **Testovací období** — po zapnutí admin sleduje 1-2 periodic ticky přes status panel. Pokud errors > 0, diagnostika z LastResultJson.
5. **SD konzument** — separátní PR po ship tohoto plánu, podle revidovaných SD plánů/specu.

---

## 9. Otevřené otázky

| # | Otázka | Resolution |
|---|---|---|
| 1 | `HOT_ZAZNAMY.datum` má last-modified semantiku? | Verifikovat v plánu jako první task (viz §5.4). Fallback strategy ready. |
| 2 | Revize SD specu/plánů — kdy? | **Po approvu** tohoto AD specu a plánu. Amendment commits, separátní plán `2026-04-22-sd-sync-revise.md`. |
| 3 | Manuální „Aktualizovat osobu z AD" tlačítko — UI lokace? | V detailu osoby ([PmTracker.Web/Views/Osoby/Detail.cshtml](PmTracker.Web/Views/Osoby/Detail.cshtml), pokud existuje; jinak v edit modalu). Decision při implementaci UI tasku. |
| 4 | Reactive queue status panel v Nastavení? | Ne v tomto specu — defer. Logy stačí pro debug. |
| 5 | Sdílená infra namespace `PmTracker.Web.Services.Sync` OK? | Navrhuju; review při commitu. |

---

## 10. Summary změn v codebase

### 10.1 Nové soubory (shared infra)

- `PmTracker.Web/Services/Sync/SyncScheduleCalculator.cs`
- `PmTracker.Web/Services/Sync/SyncTriggerKind.cs`
- `PmTracker.Web/Services/Sync/ISyncJobSettings.cs`
- `PmTracker.Web/Services/Sync/SyncHostedServiceBase.cs`
- `PmTracker.Web/Services/Sync/ISyncJobRunLock.cs`
- `PmTracker.Web/Services/Sync/SyncJobRunLock.cs`
- `PmTracker.Web/Services/Sync/IReactiveSyncQueue.cs`
- `PmTracker.Web/Services/Sync/ReactiveSyncQueue.cs`
- `PmTracker.Web/Services/Sync/ReactiveSyncConsumerBase.cs`
- `PmTracker.Web/Models/ViewModels/Sync/SyncJobSettingsCardViewModel.cs`
- `PmTracker.Web/Views/Shared/_SyncJobSettingsCard.cshtml`

### 10.2 Nové soubory (AD konzument)

- `PmTracker.Web/Models/Entities/AdSyncSettingsEntity.cs`
- `PmTracker.Web/Data/Configuration/AdSyncSettingsEntityConfiguration.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdSyncModels.cs` (AdSyncResult, AdSyncErrorItem, AdReactiveSyncRequest, …)
- `PmTracker.Web/Services/ActiveDirectory/IAdSyncService.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdSyncService.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdPeriodicSyncHostedService.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdReactiveSyncConsumer.cs`
- `PmTracker.Web/Services/ActiveDirectory/AdSyncJobAdminHandler.cs`
- `PmTracker.Web/Controllers/NastaveniSyncController.cs`
- `db_upgrade_1_3_0_ad_sync_settings.sql`

### 10.3 Modifikované soubory

- `PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs` — přidat `ListByGuidsAsync`
- `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryService.cs` — impl `ListByGuidsAsync`
- `PmTracker.Web/Services/ActiveDirectory/ActiveDirectoryModels.cs` — přidat `ActiveDirectoryBatchResponse`
- `PmTracker.Web/Data/PmTrackerDbContext.cs` — `DbSet<AdSyncSettingsEntity>`
- `PmTracker.Web/Program.cs` — DI registrace shared infra + AD konzument
- `PmTracker.Web/Views/Nastaveni/Index.cshtml` — nová sekce „Synchronizace" (section key `synchronizace`)
- `PmTracker.Web/Models/ViewModels/NastaveniViewModels.cs` — section metadata
- `PmTracker.Web/Services/Settings/SettingsService.cs` — registrovat section

### 10.4 Testy

- `PmTracker.Tests.Unit/Sync/SyncScheduleCalculatorTests.cs`
- `PmTracker.Tests.Unit/Sync/SyncHostedServiceBaseTests.cs`
- `PmTracker.Tests.Unit/Sync/ReactiveSyncQueueTests.cs`
- `PmTracker.Tests.Unit/ActiveDirectory/AdSyncServiceTests.cs`
- `PmTracker.Tests.Unit/ActiveDirectory/AdReactiveSyncConsumerTests.cs`
- `PmTracker.Tests.Integration/ActiveDirectory/AdPeriodicSyncHostedServiceTests.cs`

### 10.5 NuGet dependencies

- `PmTracker.Tests.Unit.csproj` — přidat `Microsoft.Extensions.TimeProvider.Testing` (pro `FakeTimeProvider`)

---

## 11. Časový odhad

- Shared infra (2.1-2.7): ~1 den
- AD konzument (3.1-3.7): ~1 den
- UI + controller: ~0.5 dne
- Testy (7.1-7.2): ~0.5 dne
- Integrace + debug + commit hygiena: ~0.5 dne

**Total: ~3.5 dne** solo práce. Plán rozbije na 12-15 tasků s TDD red/green/refactor cyklem per task.

---

## 13. Amendment 2026-04-22 — Rate limiting, manual wake-up, AD reactive debounce

Toto je **amendment** k sekcím 2.x, 3.x, 6.x po revizi s uživatelem při psaní navazujícího plánu [2026-04-22-sd-sync-revise.md](../plans/2026-04-22-sd-sync-revise.md). Platí **pro všechny konzumenty shared sync infra** (AD i SD). Implementace se přidá do Task 7 (SyncHostedServiceBase), Task 10 (AD ListByGuids), Task 14 (AD reactive consumer), Task 16 (NastaveniSyncController).

### 13.1 Dvouúrovňový rate limit

Uživatel potvrdil design: **soft debounce (15 minut)** pro automatické reaktivní triggery, **hard floor (1 minuta)** pro manuální akce. Pravidla:

| Typ spouštění | Pravidlo | Implementace |
|---|---|---|
| Reactive auto (AD: PersonPicked; SD: T2/T5/T7/T8) | 15 min per-osobaId (AD) / per-zaznamId (SD), **bypass pokud první harvest** (LastSync IS NULL) | `IReactiveSyncQueue` producer strana: před enqueue check fingerprint column (`osoby.LastAdSyncAt` nebo `zaznam_externi_odkazy.LastHarvestedAt`) |
| Manual (tlačítka „Aktualizovat z AD", „Obnovit", 🔄) | 1 min per-item, bypass 15-min | `IMemoryCache` klíč `{job}.manual.{itemId}` s 60s TTL; vrátit HTTP 429 + Retry-After header |
| Admin „Spustit teď" (periodic job) | 1 min per-job floor | Check `settings.LastRunAt`; pokud `now - LastRunAt < 60s`, vrátit zprávu „Proběhl před X sekundami" |
| Periodic tick (auto) | `PeriodMinutes >= 5` (existující) | Validace beze změn |

**Důvod first-time bypass:** Uživatel přidá novou osobu (PersonPicked) nebo novou externí vazbu (T2) → musí se alespoň jednou načíst, i kdyby byl debounce window. Bez bypass by nový záznam zůstal prázdný.

### 13.2 ManualResetEventSlim pro „Spustit teď"

Původní spec §2.3 nepopisuje, jak handler `ISyncJobAdminHandler.TriggerManualRunAsync` signaluje běžící `SyncHostedServiceBase` loopu. Amendment:

**Per-job `ManualTriggerSignal<TSettings>` v DI singletonem:**

```csharp
public sealed class ManualTriggerSignal<TSettings>
    where TSettings : class, ISyncJobSettings
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken ct) => _tcs.Task.WaitAsync(ct);

    public void Signal()
    {
        // Použít Interlocked aby se současné signály neztratily; reset je implicitní
        // v další iteraci (consumer si načte novou instanci před await).
        _tcs.TrySetResult();
    }
}

// DI:
services.AddSingleton(typeof(ManualTriggerSignal<>));
```

**`SyncHostedServiceBase<TSettings>` loop upraven:**

```csharp
// Místo:
await Task.Delay(next - now, ct);

// Použij:
using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
var delayTask = Task.Delay(next - now, cts.Token);
var signalTask = _manualSignal.WaitAsync(cts.Token);
var winner = await Task.WhenAny(delayTask, signalTask);
cts.Cancel();  // zruší druhý task
var trigger = (winner == signalTask) ? SyncTriggerKind.Manual : SyncTriggerKind.Auto;
await RunOnceAsync(scope, trigger, ct);
```

**Controller akce `POST /Nastaveni/Sync/{jobKey}/RunNow`:**

```csharp
// V keyed handleru (AdSyncJobAdminHandler, SdActiveSyncJobAdminHandler, ...)
public async Task<ManualRunOutcome> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct)
{
    // 1-min floor check
    var settings = await LoadSettingsInternalAsync(ct);
    if (settings.LastRunAt.HasValue)
    {
        var elapsed = _time.GetUtcNow().UtcDateTime - settings.LastRunAt.Value;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            var retryAfter = (int)Math.Ceiling((TimeSpan.FromMinutes(1) - elapsed).TotalSeconds);
            return new ManualRunOutcome(
                Accepted: false,
                Message: $"Sync proběhl před {(int)elapsed.TotalSeconds} s. Zkus za {retryAfter} s.");
        }
    }

    // Lock check pro případ, že běží teď
    if (!_runLock.TryAcquire())
    {
        return new ManualRunOutcome(
            Accepted: false,
            Message: "Sync právě běží. Počkej na dokončení.");
    }
    _runLock.Release();  // Jen jsme ověřili, že momentálně neběží.

    // Signal do hosted service — ten se okamžitě probudí a spustí RunOnceAsync(trigger=Manual)
    _manualSignal.Signal();

    return new ManualRunOutcome(
        Accepted: true,
        Message: "Manual sync naplánován.");
}
```

**Latence:** User klikne „Spustit teď" → do ~1 sekundy vidí status „Running from ...". Žádné DB polling, žádné blokování HTTP request threadu.

### 13.3 AD reactive debounce — nový sloupec `osoby.LastAdSyncAt`

Pro 15-min debounce v AD reactive (PersonPicked + ManualUpdate triggery) je potřeba znát „kdy byla osoba naposledy synchronizovaná z AD". Existující `OsobaEntity` **nemá** tento sloupec.

**Nový DB upgrade skript** `db_upgrade_1_3_1_osoba_last_ad_sync_at.sql`:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'dbo.osoby')
                 AND name = N'last_ad_sync_at')
BEGIN
    ALTER TABLE dbo.osoby ADD last_ad_sync_at DATETIME2 NULL;
    PRINT N'Sloupec osoby.last_ad_sync_at přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec osoby.last_ad_sync_at už existuje.';
END
GO
```

**Entity update:** přidat `DateTime? LastAdSyncAt { get; set; }` do `OsobaEntity`. V `AdSyncService.SyncSinglePersonAsync` a `SyncAllPeopleAsync` po úspěšném update AD dat nastavit `osoba.LastAdSyncAt = time.GetUtcNow().UtcDateTime`.

**Reactive debounce check** v `AdReactiveSyncConsumer.HandleAsync` nebo na producent straně v `OsobyController.CreateFromAd`:

```csharp
// Producent (před EnqueueAsync):
var osoba = await db.Osoby.FirstAsync(x => x.Id == osobaId, ct);
if (osoba.LastAdSyncAt.HasValue
    && (time.GetUtcNow().UtcDateTime - osoba.LastAdSyncAt.Value) < TimeSpan.FromMinutes(15))
{
    // Skip: recently synced
    return;
}
await queue.EnqueueAsync(new AdReactiveSyncRequest(osobaId, AdReactiveSource.PersonPicked), ct);
```

**Manual button „Aktualizovat z AD":** stejný vzor jako SdSyncController — `IMemoryCache` klíč `ad.manual.{osobaId}` s 60s TTL, vrátit 429 při spam-cliku.

### 13.4 Per-item concurrency lock

Pro operace, kde může souběžně běžet periodic tick + direct sync + reactive consumer nad stejnou entitou (osoba, externí vazba, ticket), se přidá per-item `ConcurrentDictionary<int, SemaphoreSlim>` uvnitř příslušné service třídy:

- `AdSyncService`: lock per-`osobaId`
- `VyjadreniHarvestService`: lock per-`externiOdkazId` (detailně v sd-sync-revise Task 6)

Acquire s 0s timeout, při nezískání skip (někdo jiný na tom pracuje). Kombinované s `DbUpdateConcurrencyException` retry (1 reload + 1 retry) jako poslední safety net.

### 13.5 Dopady na existující sekce spec

| Sekce | Dopad |
|---|---|
| §2.3 SyncHostedServiceBase | Loop upravit per §13.2 — `Task.WhenAny(delay, manualSignal)` |
| §2.4 ISyncJobRunLock | Zůstává (preserve concurrency guard), doplňuje se manual signal |
| §3.3 IAdSyncService | SyncAllPeopleAsync + SyncSinglePersonAsync nastavují `LastAdSyncAt` |
| §3.5 AD reactive triggery | Přidat debounce na producent straně před `queue.EnqueueAsync` |
| §6.1 Permission | Beze změn |
| §6.2 UI Nastavení | „Spustit teď" tlačítko signaluje přes `ManualTriggerSignal`; 1-min floor check v handleru |
| §10 Summary změn | Přidat `db_upgrade_1_3_1_osoba_last_ad_sync_at.sql`, `ManualTriggerSignal<T>` třídu |

### 13.6 Dopady na existující Task v plánu sync-infra-and-ad

| Task | Dopad |
|---|---|
| Task 7 (`SyncHostedServiceBase`) | Přidat `ManualTriggerSignal<TSettings>` do konstruktoru + `Task.WhenAny` loop |
| Task 9 (`AdSyncSettingsEntity`) | Beze změn |
| Task 10 (`ListByGuidsAsync`) | Beze změn |
| Task 12 (`AdSyncService`) | Nastavit `osoba.LastAdSyncAt` po každém úspěšném syncu |
| Task 13 (`AdPeriodicSyncHostedService`) | Beze změn (base class to pokryje) |
| Task 14 (`AdReactiveSyncConsumer`) | Přidat 15-min debounce check na producent straně (před enqueue, v `OsobyController.CreateFromAd`) |
| Task 16 (handler + `NastaveniSyncController`) | `TriggerManualRunAsync` použije `ManualTriggerSignal<T>.Signal()` + 1-min floor check. RunNow endpoint vrátí 429 při spam-cliku. |
| **Nový Task 9a** | DB upgrade `db_upgrade_1_3_1_osoba_last_ad_sync_at.sql` + entity update |

---

## 12. Reference

- Inbox #14: [docs/superpowers/plans/2026-04-20-upravy-inbox.md:266-329](docs/superpowers/plans/2026-04-20-upravy-inbox.md#L266-L329)
- Inbox #15: [docs/superpowers/plans/2026-04-20-upravy-inbox.md:333-341](docs/superpowers/plans/2026-04-20-upravy-inbox.md#L333-L341)
- SD spec (bude revidován): [docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md)
- Pattern BackgroundService: [PmTracker.Web/Services/Search/SearchReindexHostedService.cs](PmTracker.Web/Services/Search/SearchReindexHostedService.cs)
- OsobaEntity + GuidAd: [PmTracker.Web/Models/Entities/PmTrackerEntities.cs:123-135](PmTracker.Web/Models/Entities/PmTrackerEntities.cs#L123-L135)
- Existující AD service: [PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs](PmTracker.Web/Services/ActiveDirectory/IActiveDirectoryService.cs)
- PermissionKeys: [PmTracker.Web/Models/ViewModels/SecurityViewModels.cs](PmTracker.Web/Models/ViewModels/SecurityViewModels.cs)
