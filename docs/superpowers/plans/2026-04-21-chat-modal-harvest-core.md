# Chat Modal + Vyjádření Harvest (Core Feature) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementovat kompletní chat modal pro zobrazení vyjádření externího tiketu, přiřazení bublin ke krokům harmonogramu (drag & drop), automatické vytěžování textových predikátů z `HOT_VYJADRENI`, chronologie rebalance algoritmus, localStorage autosave, re-harvest, a diagnostickou stránku `/SDConnector` pro ověřování.

**Architecture:** Tři samostatné vrstvy. **Backend harvest** = `IVyjadreniHarvestService` + Hangfire batch job + `HotVyjadreniEntity` na read-only ServiceDesk DbContext. **Datový model** = `zaznam_harmonogram_vyjadreni_vazba` tabulka + reálná implementace `IHarvestScheduler` místo no-op stubu z Plánu B. **UI** = chat modal (Razor view + gov-card bubliny + vertikální stepper + drag & drop + localStorage klient) + diagnostická `/SDConnector` stránka. Jméno autora vyjádření se překládá z AD (cache v paměti, nikdy do DB).

**Tech Stack:** .NET 8 ASP.NET Core MVC + Razor, EF Core 8, Hangfire 1.8+, `Microsoft.Data.SqlClient`, Gov Design System 4.2.9, vanilla JS (ES6, bez framework), `IMemoryCache` pro AD resolution cache, xUnit + FluentAssertions + Moq + Playwright. ActiveDirectoryService (existující) pro login→jméno mapping.

**Předpoklady:**
- **Plán A** (fakturace cleanup) hotov.
- **Plán B** (karta externí vazby v2 + `IHarvestScheduler` stub + `NoOpHarvestScheduler`) hotov.
- **Plán E** (admin sync settings) hotov — `IServiceDeskSyncSettings` funguje, karta v `/Nastaveni/ServiceDeskSync` přístupná adminu.
- Spec 2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §4.1.1 s přesným schéma HOT_* a §8.1 s LIKE predikáty.

---

## File Structure

### Nové soubory — backend entity + data
- `PmTracker.ServiceDesk.Sql/Entities/HotVyjadreniEntity.cs` — nová entita.
- `PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs` *(modify)* — přidat `Pid` + opravit `Stav` na string.
- `db_upgrade_1_2_2_vyjadreni_vazba.sql` — tabulka `zaznam_harmonogram_vyjadreni_vazba`.
- `PmTracker.Web/Models/Entities/ZaznamHarmonogramVyjadreniVazbaEntity.cs` — entity v PM Tracker DB.
- `PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs` — EF mapping.

### Nové soubory — harvest services
- `PmTracker.ServiceDesk.Contracts/IVyjadreniQueryService.cs` — nový kontrakt.
- `PmTracker.ServiceDesk.Contracts/Contracts/HotVyjadreniDto.cs` — DTO.
- `PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs` — SQL implementace.
- `PmTracker.ServiceDesk.Sql/DisabledVyjadreniQueryService.cs` — feature-flag fallback.
- `PmTracker.Web/Services/ServiceDesk/IVyjadreniHarvestService.cs` — business logika harvestu.
- `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs` — LIKE predikáty + chronologie rebalance.
- `PmTracker.Web/Services/ServiceDesk/HangfireHarvestScheduler.cs` — reálná implementace `IHarvestScheduler` (nahradí `NoOpHarvestScheduler`).
- `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestBatchJob.cs` — Hangfire periodický job.
- `PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs` — static class s LIKE patterny pro K1/K3/K4/K6/K7/K10.
- `PmTracker.Web/Services/ServiceDesk/AdLoginCache.cs` — in-memory cache pro překlad login→jméno.

### Nové soubory — UI modal
- `PmTracker.Web/Controllers/VyjadreniModalController.cs` — endpointy `/Vyjadreni/Modal`, `/Vyjadreni/List`, `/Vyjadreni/HarmonogramVazba/Save`, `/Vyjadreni/HarmonogramVazba/ReHarvest`.
- `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs` — VM pro modal data.
- `PmTracker.Web/Models/ViewModels/Vyjadreni/BublinaViewModel.cs` — VM pro jednu bublinu.
- `PmTracker.Web/Models/ViewModels/Vyjadreni/StepperKrokViewModel.cs` — VM pro jeden krok stepperu.
- `PmTracker.Web/Views/Vyjadreni/Modal.cshtml` — hlavní Razor view.
- `PmTracker.Web/Views/Vyjadreni/_Bublina.cshtml` — partial pro bublinu.
- `PmTracker.Web/Views/Vyjadreni/_Stepper.cshtml` — partial pro stepper.
- `PmTracker.Web/wwwroot/css/components/vyjadreni-modal.css` — styly.
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/modalShell.js` — bootstrap modalu.
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/timeline.js` — renderování bublin.
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepper.js` — vertikální drag & drop.
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/autosave.js` — localStorage + debounced save.
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/reharvest.js` — re-harvest tlačítko.

### Nové soubory — diagnostická stránka /SDConnector
- `PmTracker.Web/Controllers/SDConnectorController.cs` — public route `/SDConnector` (podle StyleGuide pattern).
- `PmTracker.Web/Views/SDConnector/Index.cshtml` — dvousloupcový dashboard.
- `PmTracker.Web/Models/ViewModels/SDConnector/SDConnectorViewModel.cs` — VM stavu + raw data.
- `PmTracker.Web/wwwroot/css/components/sdconnector-page.css` — styly stránky (včetně modal-simulation dlaždice).
- `PmTracker.Web/wwwroot/js/modules/sdconnector/page.js` — input debounce + fetch + render.

### Nové soubory — testy
- `PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs` — LIKE patterny.
- `PmTracker.Tests.Unit/ServiceDesk/VyjadreniHarvestServiceTests.cs` — celá business logika harvestu.
- `PmTracker.Tests.Unit/ServiceDesk/ChronologieRebalanceTests.cs` — rebalance algoritmus.
- `PmTracker.Tests.Unit/ServiceDesk/AdLoginCacheTests.cs` — cache behavior.
- `PmTracker.Tests.Unit/ServiceDesk/VyjadreniModalControllerTests.cs` — API endpointy.
- `PmTracker.Tests.Unit/ServiceDesk/SDConnectorControllerTests.cs` — diagnostická stránka.

### Modifikované soubory
- `PmTracker.ServiceDesk.Contracts/ITicketingQueryService.cs` — **žádná změna**, rozšíření jde do nového interface `IVyjadreniQueryService`.
- `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` — odstranit `HotPid` DbSet + konfiguraci (potvrzeno: nepoužívá se).
- `PmTracker.ServiceDesk.Sql/Entities/HotPidEntity.cs` — **smazat** (viz výše).
- `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs` — zaregistrovat `IVyjadreniQueryService`.
- `PmTracker.Web/Program.cs` — registrovat `IHarvestScheduler` = `HangfireHarvestScheduler` (nahradit NoOp), `IVyjadreniHarvestService`, `AdLoginCache`, Hangfire server.
- `PmTracker.Web/Data/PmTrackerDbContext.cs` — přidat `DbSet<ZaznamHarmonogramVyjadreniVazbaEntity>`.
- `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml` — propojit tlačítko 💬 na reálný modal (nahradit stub z Plánu B).
- `PmTracker.Web/Services/ServiceDesk/NoOpHarvestScheduler.cs` — **smazat** (nahrazeno HangfireHarvestScheduler).
- `PmTracker.Web/wwwroot/js/site.bundle.js` — integrovat nové JS moduly.
- `PmTracker.Web/wwwroot/css/site.css` — `@import` pro `vyjadreni-modal.css` a `sdconnector-page.css`.
- `PmTracker.Web/Services/RecordService.SaveRecord.cs` — **žádná změna** (call-site z Plánu B už existuje, jen scheduler je nyní reálný).
- `PmTracker.Web/Controllers/ZaznamyController.cs` — přidat call na `IHarvestScheduler.ScheduleHarvestForRecordAsync` v metodě pro otevření editoru (T5 trigger).

### Soubory, které se záměrně NEMĚNÍ
- `IServiceDeskSyncSettings` (z Plánu E) — pouze čtení, funguje jak je.
- `VyzvaService` + kontrakty (Fáze 1) — harvest vyjádření je nezávislý od výzev.
- `HarmonogramService` — čte `ZaznamHarmonogramHodnotaEntity`; Plán C jen zapisuje hodnoty přes existující API (`ReplaceRecordScheduleValuesAsync`).

---

## Pořadí úkolů (20 tasků)

1. **Task 1** — Oprav `HotZaznamEntity`: přidej `Pid`, oprav `Stav` na string.
2. **Task 2** — Odstraň `HotPidEntity` a DbSet.
3. **Task 3** — `HotVyjadreniEntity` + konfigurace v `TicketingReadOnlyDbContext`.
4. **Task 4** — `IVyjadreniQueryService` + `HotVyjadreniDto` + SQL implementace.
5. **Task 5** — DB tabulka `zaznam_harmonogram_vyjadreni_vazba` + entity + EF mapping.
6. **Task 6** — `HarvestPredicates` static class + unit testy pro regex/LIKE generování.
7. **Task 7** — `AdLoginCache` (in-memory login→jméno).
8. **Task 8** — `IVyjadreniHarvestService` + `VyjadreniHarvestService` (bez chronologie — pouze single-krok detekce).
9. **Task 9** — Chronologie rebalance algoritmus + unit testy.
10. **Task 10** — `HangfireHarvestScheduler` + registrovat Hangfire server.
11. **Task 11** — `VyjadreniHarvestBatchJob` periodický job (T4).
12. **Task 12** — Zavolat T5 trigger v `ZaznamyController` otevření editoru.
13. **Task 13** — `VyjadreniModalController` + endpoints + ViewModels.
14. **Task 14** — Razor view `Modal.cshtml` + partials + CSS.
15. **Task 15** — JS `modalShell.js` + `timeline.js` (render).
16. **Task 16** — JS `stepper.js` (drag & drop + chronologie klientská kontrola).
17. **Task 17** — JS `autosave.js` (localStorage + debounced save + beforeunload).
18. **Task 18** — JS `reharvest.js` + propojení 💬 tlačítka z karty externí vazby.
19. **Task 19** — `/SDConnector` diagnostická stránka (controller + view + JS).
20. **Task 20** — Full build + Playwright end-to-end smoke + git clean.

---

## Task 1: Oprava `HotZaznamEntity`

**Files:**
- Modify: `PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` (mapping stav string + pid)
- Test: `PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs`

- [ ] **Step 1: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotZaznamEntityShapeTests
{
    private static Type GetEntityType()
    {
        var asm = Assembly.Load("PmTracker.ServiceDesk.Sql");
        var t = asm.GetType("PmTracker.ServiceDesk.Sql.Entities.HotZaznamEntity", throwOnError: true);
        return t!;
    }

    [Fact]
    public void HotZaznamEntity_ShouldExposePidProperty()
    {
        var prop = GetEntityType().GetProperty("Pid", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        prop.Should().NotBeNull("pid je FK používaný pro join s HOT_VYJADRENI a HOT_KALKULACE");
        prop!.PropertyType.Should().Be(typeof(string), "pid je alfanumerický identifikátor (např. A400P023RVVP)");
    }

    [Fact]
    public void HotZaznamEntity_StavShouldBeString()
    {
        var prop = GetEntityType().GetProperty("Stav", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        prop.Should().NotBeNull();
        // V hotline.txt je stav text (archiv/otevřeno atd.), ne int
        var type = Nullable.GetUnderlyingType(prop!.PropertyType) ?? prop.PropertyType;
        type.Should().Be(typeof(string));
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "HotZaznamEntityShapeTests" --no-restore`

Expected: 2 failed — `Pid` neexistuje, `Stav` je `int?`.

- [ ] **Step 3: Opravit entity**

Obsah `PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs`:

```csharp
namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotZaznamEntity
{
    public long Radek { get; set; }
    public string Id { get; set; } = string.Empty;
    public string? Pid { get; set; }
    public string? TypZaznamu { get; set; }
    public string? Strucne { get; set; }
    public string? Popis { get; set; }
    public string? Stav { get; set; }  // text: archiv / otevřeno / dodavatel / od dodavatele / k dodavateli
    public int? Splneno { get; set; }
    public DateTime? SlaDeadline { get; set; }
    public DateTime? Datum { get; set; }
}
```

- [ ] **Step 4: Aktualizovat EF mapping**

Otevři `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`, najdi blok `mb.Entity<HotZaznamEntity>`:

Run: `grep -n "HotZaznamEntity\|hot_zaznamy" PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`

V konfiguraci přidej mapování na nové sloupce:

```csharp
e.Property(x => x.Pid).HasColumnName("pid").HasMaxLength(50);
e.Property(x => x.Stav).HasColumnName("stav").HasMaxLength(50);
e.Property(x => x.Datum).HasColumnName("datum");
```

A odstraň stávající mapping `e.Property(x => x.Stav).HasColumnName("stav")` který může být jako `int?`.

- [ ] **Step 5: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "HotZaznamEntityShapeTests" --no-restore`

Expected: 0 errors, 2/2 passed.

- [ ] **Step 6: Ověřit, že Fáze 1 vyzvy testy stále fungují**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Vyzvy" --no-restore`

Expected: všechny existující testy passed.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs \
        PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs
git commit -m "refactor(servicedesk): HotZaznamEntity — přidat Pid + Datum, Stav:string"
```

---

## Task 2: Odstranit `HotPidEntity`

**Files:**
- Delete: `PmTracker.ServiceDesk.Sql/Entities/HotPidEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` (odstranit DbSet + konfiguraci)

- [ ] **Step 1: Ověřit, že HotPid nikde není referencován v runtime kódu**

Run: `grep -rn "HotPid\|HOT_PID" --include="*.cs" PmTracker.ServiceDesk.Sql PmTracker.Web 2>/dev/null`

Expected: jen `TicketingReadOnlyDbContext.cs` (DbSet + konfigurace) a samotná entity. Pokud jsou jiné odkazy, neodstraňovat — hlásit.

- [ ] **Step 2: Smazat soubor**

```bash
rm "PmTracker.ServiceDesk.Sql/Entities/HotPidEntity.cs"
```

- [ ] **Step 3: Upravit `TicketingReadOnlyDbContext.cs`**

Odstranit:
- `internal DbSet<HotPidEntity> HotPid => Set<HotPidEntity>();`
- blok `mb.Entity<HotPidEntity>(e => {...});`

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql --no-restore`

Expected: 0 errors.

- [ ] **Step 5: Full test**

Run: `dotnet test PmTracker.Tests.Unit --no-restore --no-build`

Expected: všechny testy passed (vyzvy testy dříve používaly HotPid? — ne, nepoužívaly).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/ PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs
git commit -m "refactor(servicedesk): smazat HotPidEntity — tabulka existuje, ale pid je identický s HOT_ZAZNAMY.pid"
```

---

## Task 3: `HotVyjadreniEntity` + konfigurace v DbContext

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotVyjadreniEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/HotVyjadreniEntityTests.cs`

- [ ] **Step 1: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/HotVyjadreniEntityTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotVyjadreniEntityTests
{
    [Fact]
    public void HotVyjadreniEntity_ShouldExposeAllExpectedColumns()
    {
        var v = new HotVyjadreniEntity
        {
            Id = 12345,
            Typ = "05",
            Pid = "A400P023RVVP",
            Datum = new DateTime(2026, 3, 14),
            Zpracoval = "Jan Novák",
            Popis = "<p>test vyjádření</p>",
            Tym = "FIS",
            ViditelneDodavateli = 1
        };
        v.Id.Should().Be(12345);
        v.Typ.Should().Be("05");
        v.Pid.Should().Be("A400P023RVVP");
        v.Datum.Should().Be(new DateTime(2026, 3, 14));
        v.Zpracoval.Should().Be("Jan Novák");
        v.Popis.Should().Contain("test");
        v.Tym.Should().Be("FIS");
        v.ViditelneDodavateli.Should().Be(1);
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "HotVyjadreniEntityTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Vytvořit entity**

Vytvoř `PmTracker.ServiceDesk.Sql/Entities/HotVyjadreniEntity.cs`:

```csharp
namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_VYJADRENI — jednotlivá vyjádření/komentáře k tiketům.
/// Schema podle SD_servicedesk/hotline.txt:
///   id, typ, pid, datum, zpracoval, popis, tym, export, id_export, viditelne_dodavateli
/// Join na HOT_ZAZNAMY přes pid (ne přes id!).
/// </summary>
internal sealed class HotVyjadreniEntity
{
    public long Id { get; set; }
    public string? Typ { get; set; }  // "05" / "25" / "16"
    public string? Pid { get; set; }
    public DateTime? Datum { get; set; }
    public string? Zpracoval { get; set; }
    public string? Popis { get; set; }  // HTML z produkce
    public string? Tym { get; set; }
    public int? ViditelneDodavateli { get; set; }
}
```

- [ ] **Step 4: Registrovat v DbContext**

V `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` přidej:

```csharp
internal DbSet<HotVyjadreniEntity> HotVyjadreni => Set<HotVyjadreniEntity>();
```

V `OnModelCreating`:

```csharp
mb.Entity<HotVyjadreniEntity>(e =>
{
    e.ToTable("HOT_VYJADRENI", "dbo");
    e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.Typ).HasColumnName("typ").HasMaxLength(10);
    e.Property(x => x.Pid).HasColumnName("pid").HasMaxLength(50);
    e.Property(x => x.Datum).HasColumnName("datum");
    e.Property(x => x.Zpracoval).HasColumnName("zpracoval").HasMaxLength(200);
    e.Property(x => x.Popis).HasColumnName("popis");
    e.Property(x => x.Tym).HasColumnName("tym").HasMaxLength(50);
    e.Property(x => x.ViditelneDodavateli).HasColumnName("viditelne_dodavateli");
});
```

- [ ] **Step 5: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "HotVyjadreniEntityTests" --no-restore`

Expected: 0 errors, 1 passed.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/HotVyjadreniEntity.cs \
        PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/HotVyjadreniEntityTests.cs
git commit -m "feat(servicedesk): HotVyjadreniEntity + EF mapping na HOT_VYJADRENI"
```

---

## Task 4: `IVyjadreniQueryService` + DTO + SQL implementace

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/IVyjadreniQueryService.cs`
- Create: `PmTracker.ServiceDesk.Contracts/Contracts/HotVyjadreniDto.cs`
- Create: `PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs`
- Create: `PmTracker.ServiceDesk.Sql/DisabledVyjadreniQueryService.cs`
- Modify: `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/SqlVyjadreniQueryServiceTests.cs`

- [ ] **Step 1: Vytvořit DTO**

`PmTracker.ServiceDesk.Contracts/Contracts/HotVyjadreniDto.cs`:

```csharp
namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotVyjadreniDto(
    long Id,
    string? Typ,
    string Pid,
    DateTime Datum,
    string? Zpracoval,
    string? Popis,
    string? Tym,
    int? ViditelneDodavateli);
```

- [ ] **Step 2: Vytvořit interface**

`PmTracker.ServiceDesk.Contracts/IVyjadreniQueryService.cs`:

```csharp
namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Read-only dotazy na HOT_VYJADRENI pro harvest vyjádření.
/// Join na HOT_ZAZNAMY přes pid.
/// </summary>
public interface IVyjadreniQueryService
{
    /// <summary>
    /// Všechna vyjádření pro tiket (6místné HOT_ZAZNAMY.id),
    /// seřazená vzestupně podle data. Volitelně filtrovaná na datum > sinceUtc
    /// (inkrementální harvest).
    /// </summary>
    Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
        string cislo6,
        DateTime? sinceUtc,
        CancellationToken ct);
}
```

- [ ] **Step 3: SQL implementace**

`PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public sealed class SqlVyjadreniQueryService(TicketingReadOnlyDbContext db) : IVyjadreniQueryService
{
    public async Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
        string cislo6, DateTime? sinceUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo6))
            return Array.Empty<HotVyjadreniDto>();

        // Join HOT_VYJADRENI.pid = HOT_ZAZNAMY.pid; filter by HOT_ZAZNAMY.id = cislo6
        var query = from v in db.HotVyjadreni.AsNoTracking()
                    join z in db.HotZaznamy.AsNoTracking() on v.Pid equals z.Pid
                    where z.Id == cislo6
                    select v;

        if (sinceUtc.HasValue)
        {
            query = query.Where(v => v.Datum > sinceUtc.Value);
        }

        var rows = await query
            .OrderBy(v => v.Datum)
            .ToListAsync(ct);

        return rows
            .Where(v => v.Datum.HasValue && v.Pid != null)
            .Select(v => new HotVyjadreniDto(
                v.Id,
                v.Typ,
                v.Pid!,
                v.Datum!.Value,
                v.Zpracoval,
                v.Popis,
                v.Tym,
                v.ViditelneDodavateli))
            .ToList();
    }
}
```

- [ ] **Step 4: Disabled fallback**

`PmTracker.ServiceDesk.Sql/DisabledVyjadreniQueryService.cs`:

```csharp
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public sealed class DisabledVyjadreniQueryService : IVyjadreniQueryService
{
    public Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
        string cislo6, DateTime? sinceUtc, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<HotVyjadreniDto>>(Array.Empty<HotVyjadreniDto>());
}
```

- [ ] **Step 5: Registrovat v DI**

V `ServiceDeskServiceCollectionExtensions.cs` přidej do stejné větve, kde se registruje `ITicketingQueryService`:

```csharp
// Enabled větev:
services.AddScoped<IVyjadreniQueryService, SqlVyjadreniQueryService>();

// Disabled větev (feature flag off):
services.AddScoped<IVyjadreniQueryService, DisabledVyjadreniQueryService>();
```

- [ ] **Step 6: Unit test**

`PmTracker.Tests.Unit/ServiceDesk/SqlVyjadreniQueryServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SqlVyjadreniQueryServiceTests
{
    private static TicketingReadOnlyDbContext NewInMemory()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase("tiketing-" + Guid.NewGuid())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_ReturnsOnlyMatchingTicketAndSortsAsc()
    {
        await using var db = NewInMemory();
        db.HotZaznamy.Add(new HotZaznamEntity { Radek = 1, Id = "336865", Pid = "A400P023RVVP", TypZaznamu = "PNF" });
        db.HotZaznamy.Add(new HotZaznamEntity { Radek = 2, Id = "999999", Pid = "ZZZZZZ", TypZaznamu = "PNF" });
        db.HotVyjadreni.AddRange(
            new HotVyjadreniEntity { Id = 1, Pid = "A400P023RVVP", Datum = new DateTime(2026, 1, 5), Popis = "prvni" },
            new HotVyjadreniEntity { Id = 2, Pid = "A400P023RVVP", Datum = new DateTime(2026, 1, 10), Popis = "druhe" },
            new HotVyjadreniEntity { Id = 3, Pid = "ZZZZZZ", Datum = new DateTime(2026, 1, 7), Popis = "jiny tiket" });
        await db.SaveChangesAsync();

        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("336865", sinceUtc: null, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_WithSinceUtc_FiltersIncrementalOnly()
    {
        await using var db = NewInMemory();
        db.HotZaznamy.Add(new HotZaznamEntity { Radek = 1, Id = "336865", Pid = "A400P023RVVP" });
        db.HotVyjadreni.AddRange(
            new HotVyjadreniEntity { Id = 1, Pid = "A400P023RVVP", Datum = new DateTime(2026, 1, 5) },
            new HotVyjadreniEntity { Id = 2, Pid = "A400P023RVVP", Datum = new DateTime(2026, 1, 10) });
        await db.SaveChangesAsync();

        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("336865", new DateTime(2026, 1, 6), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_EmptyCislo_ReturnsEmpty()
    {
        await using var db = NewInMemory();
        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("", null, CancellationToken.None);
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 7: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "SqlVyjadreniQueryServiceTests" --no-restore`

Expected: 3/3 passed.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.ServiceDesk.Contracts/IVyjadreniQueryService.cs \
        PmTracker.ServiceDesk.Contracts/Contracts/HotVyjadreniDto.cs \
        PmTracker.ServiceDesk.Sql/SqlVyjadreniQueryService.cs \
        PmTracker.ServiceDesk.Sql/DisabledVyjadreniQueryService.cs \
        PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs \
        PmTracker.Tests.Unit/ServiceDesk/SqlVyjadreniQueryServiceTests.cs
git commit -m "feat(servicedesk): IVyjadreniQueryService — read-only dotazy na HOT_VYJADRENI přes pid join"
```

---

## Task 5: DB tabulka `zaznam_harmonogram_vyjadreni_vazba` + entity + EF mapping

**Files:**
- Create: `db_upgrade_1_2_2_vyjadreni_vazba.sql`
- Create: `PmTracker.Web/Models/Entities/ZaznamHarmonogramVyjadreniVazbaEntity.cs`
- Create: `PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/VyjadreniVazbaEntityTests.cs`

- [ ] **Step 1: SQL skript**

`db_upgrade_1_2_2_vyjadreni_vazba.sql`:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'zaznam_harmonogram_vyjadreni_vazba')
BEGIN
    CREATE TABLE dbo.zaznam_harmonogram_vyjadreni_vazba
    (
        id                  INT IDENTITY PRIMARY KEY,
        zaznam_id           INT NOT NULL
            CONSTRAINT FK_zhvv_zaznam REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE,
        krok_key            UNIQUEIDENTIFIER NOT NULL,
        externi_odkaz_id    INT NOT NULL
            CONSTRAINT FK_zhvv_externi_odkaz REFERENCES dbo.zaznam_externi_odkazy(id) ON DELETE NO ACTION,
        hot_vyjadreni_id    BIGINT NOT NULL,
        datum_vyjadreni     DATETIME2 NOT NULL,
        source              TINYINT NOT NULL,
        stav                TINYINT NOT NULL,
        created_at          DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        created_by_osoba_id INT NULL
            CONSTRAINT FK_zhvv_osoba REFERENCES dbo.osoby(id),
        deleted_at          DATETIME2 NULL,
        deleted_by_osoba_id INT NULL
    );

    CREATE INDEX ix_zhvv_zaznam_krok_stav
        ON dbo.zaznam_harmonogram_vyjadreni_vazba(zaznam_id, krok_key, stav);
    CREATE INDEX ix_zhvv_externi_odkaz
        ON dbo.zaznam_harmonogram_vyjadreni_vazba(externi_odkaz_id);

    PRINT N'Tabulka zaznam_harmonogram_vyjadreni_vazba vytvořena.';
END
ELSE
BEGIN
    PRINT N'Tabulka zaznam_harmonogram_vyjadreni_vazba už existuje.';
END;
GO
```

Spusť proti Dev DB:

```bash
cd /tmp/run-sql && dotnet run "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_2_vyjadreni_vazba.sql"
```

- [ ] **Step 2: Entity**

`PmTracker.Web/Models/Entities/ZaznamHarmonogramVyjadreniVazbaEntity.cs`:

```csharp
namespace PmTracker.Web.Models.Entities;

public sealed class ZaznamHarmonogramVyjadreniVazbaEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public Guid KrokKey { get; set; }
    public int ExterniOdkazId { get; set; }
    public long HotVyjadreniId { get; set; }
    public DateTime DatumVyjadreni { get; set; }
    public byte Source { get; set; }  // 1 = Auto, 2 = Manual
    public byte Stav { get; set; }    // 1 = Active, 2 = Superseded, 3 = Deleted
    public DateTime CreatedAt { get; set; }
    public int? CreatedByOsobaId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedByOsobaId { get; set; }
}

public enum VazbaSource : byte
{
    Auto = 1,
    Manual = 2
}

public enum VazbaStav : byte
{
    Active = 1,
    Superseded = 2,
    Deleted = 3
}
```

- [ ] **Step 3: EF configuration**

`PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class ZaznamHarmonogramVyjadreniVazbaEntityConfiguration
    : IEntityTypeConfiguration<ZaznamHarmonogramVyjadreniVazbaEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHarmonogramVyjadreniVazbaEntity> b)
    {
        b.ToTable("zaznam_harmonogram_vyjadreni_vazba");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        b.Property(x => x.KrokKey).HasColumnName("krok_key");
        b.Property(x => x.ExterniOdkazId).HasColumnName("externi_odkaz_id");
        b.Property(x => x.HotVyjadreniId).HasColumnName("hot_vyjadreni_id");
        b.Property(x => x.DatumVyjadreni).HasColumnName("datum_vyjadreni");
        b.Property(x => x.Source).HasColumnName("source");
        b.Property(x => x.Stav).HasColumnName("stav");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CreatedByOsobaId).HasColumnName("created_by_osoba_id");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.DeletedByOsobaId).HasColumnName("deleted_by_osoba_id");
    }
}
```

- [ ] **Step 4: DbSet**

V `PmTrackerDbContext.cs`:

```csharp
public DbSet<ZaznamHarmonogramVyjadreniVazbaEntity> VyjadreniVazby
    => Set<ZaznamHarmonogramVyjadreniVazbaEntity>();
```

- [ ] **Step 5: Unit test**

`PmTracker.Tests.Unit/ServiceDesk/VyjadreniVazbaEntityTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class VyjadreniVazbaEntityTests
{
    [Fact]
    public void VazbaEntity_ShouldRoundTripAllFields()
    {
        var v = new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = 1,
            ZaznamId = 42,
            KrokKey = Guid.NewGuid(),
            ExterniOdkazId = 17,
            HotVyjadreniId = 99999,
            DatumVyjadreni = new DateTime(2026, 3, 14),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc)
        };
        v.Source.Should().Be((byte)VazbaSource.Auto);
        v.Stav.Should().Be((byte)VazbaStav.Active);
    }
}
```

- [ ] **Step 6: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "VyjadreniVazbaEntityTests" --no-restore`

Expected: 1 passed.

- [ ] **Step 7: Commit**

```bash
git add db_upgrade_1_2_2_vyjadreni_vazba.sql \
        PmTracker.Web/Models/Entities/ZaznamHarmonogramVyjadreniVazbaEntity.cs \
        PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs \
        PmTracker.Web/Data/PmTrackerDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/VyjadreniVazbaEntityTests.cs
git commit -m "feat(servicedesk): zaznam_harmonogram_vyjadreni_vazba tabulka + entity + EF"
```

---

## Task 6: `HarvestPredicates` static class + unit testy

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs`

- [ ] **Step 1: Napsat testy**

`PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HarvestPredicatesTests
{
    [Theory]
    [InlineData("Záznam byl založen a předán dodavateli k řešení pod značkou: XYZ", HarvestPredicateKind.K3_OdeslaniZadaniPmp)]
    [InlineData("Projektový manažer - FIS předal záznam dodavateli : Atos s.r.o. s termínem plnění dodavatele 31.3.2026", HarvestPredicateKind.PlanDodani)]
    [InlineData("Dodavatel přidal řešení.", HarvestPredicateKind.K4_K7_DodaniReseni)]
    [InlineData("Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.", HarvestPredicateKind.K6_OdeslaniPozadavku)]
    [InlineData("Záznam byl převeden do archivu.", HarvestPredicateKind.K10_NasazeniArchivace)]
    public void Matches_KnownPhrase_ReturnsExpectedKind(string popis, HarvestPredicateKind expected)
    {
        var kind = HarvestPredicates.ClassifyPopis(popis);
        kind.Should().Be(expected);
    }

    [Fact]
    public void Matches_UnknownText_ReturnsNone()
    {
        HarvestPredicates.ClassifyPopis("Random nepovinný text").Should().Be(HarvestPredicateKind.None);
    }

    [Fact]
    public void Matches_NullText_ReturnsNone()
    {
        HarvestPredicates.ClassifyPopis(null).Should().Be(HarvestPredicateKind.None);
    }

    [Fact]
    public void Matches_CaseInsensitive()
    {
        HarvestPredicates.ClassifyPopis("DODAVATEL přidal ŘEŠENÍ").Should().Be(HarvestPredicateKind.K4_K7_DodaniReseni);
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "HarvestPredicatesTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Implementovat**

`PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs`:

```csharp
namespace PmTracker.Web.Services.ServiceDesk;

public enum HarvestPredicateKind
{
    None = 0,
    K3_OdeslaniZadaniPmp,       // PMP
    K4_K7_DodaniReseni,         // PMP krok 4 i PNF krok 7
    K6_OdeslaniPozadavku,       // PNF
    K10_NasazeniArchivace,      // PMP i PNF
    PlanDodani                  // budoucí — regex na termín
}

/// <summary>
/// Textové predikáty pro klasifikaci HOT_VYJADRENI.popis.
/// Diacritika + case-insensitive porovnání (viz spec §8.1).
/// </summary>
public static class HarvestPredicates
{
    private const string PhraseK3 = "Záznam byl založen a předán dodavateli k řešení pod značkou:";
    private const string PhraseK4K7 = "Dodavatel přidal řešení";
    private const string PhraseK6 = "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.";
    private const string PhraseK10 = "Záznam byl převeden do archivu.";
    private const string PhrasePlanPartA = "předal záznam dodavateli :";
    private const string PhrasePlanPartB = "s termínem plnění dodavatele";

    public static HarvestPredicateKind ClassifyPopis(string? popis)
    {
        if (string.IsNullOrWhiteSpace(popis)) return HarvestPredicateKind.None;

        if (Contains(popis, PhraseK10)) return HarvestPredicateKind.K10_NasazeniArchivace;
        if (Contains(popis, PhraseK6)) return HarvestPredicateKind.K6_OdeslaniPozadavku;
        if (Contains(popis, PhraseK3)) return HarvestPredicateKind.K3_OdeslaniZadaniPmp;
        if (Contains(popis, PhrasePlanPartA) && Contains(popis, PhrasePlanPartB))
            return HarvestPredicateKind.PlanDodani;
        if (Contains(popis, PhraseK4K7)) return HarvestPredicateKind.K4_K7_DodaniReseni;

        return HarvestPredicateKind.None;
    }

    private static bool Contains(string text, string phrase)
        => text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// SQL LIKE pattern pro konkrétní kind — používá se v read-side dotazu
    /// (pokud chceme DB-side filter místo memory scan).
    /// </summary>
    public static string GetSqlLikePattern(HarvestPredicateKind kind) => kind switch
    {
        HarvestPredicateKind.K3_OdeslaniZadaniPmp => $"%{PhraseK3}%",
        HarvestPredicateKind.K4_K7_DodaniReseni => $"%{PhraseK4K7}%",
        HarvestPredicateKind.K6_OdeslaniPozadavku => $"%{PhraseK6}%",
        HarvestPredicateKind.K10_NasazeniArchivace => $"%{PhraseK10}%",
        HarvestPredicateKind.PlanDodani => $"%{PhrasePlanPartA}%{PhrasePlanPartB}%",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
```

- [ ] **Step 4: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "HarvestPredicatesTests" --no-restore`

Expected: 0 errors, 7/7 passed.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/HarvestPredicates.cs \
        PmTracker.Tests.Unit/ServiceDesk/HarvestPredicatesTests.cs
git commit -m "feat(servicedesk): HarvestPredicates — klasifikace HOT_VYJADRENI.popis do K3/K4/K6/K7/K10"
```

---

## Task 7: `AdLoginCache` (in-memory login → jméno)

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/AdLoginCache.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/AdLoginCacheTests.cs`

- [ ] **Step 1: Najít existující ActiveDirectoryService**

Run: `grep -rn "IActiveDirectoryService\|ActiveDirectoryService\b" PmTracker.Web/Services 2>/dev/null | head -10`

Expected: existující kontrakt pro dotazování AD (Plán C ho **jen používá**, nemění).

- [ ] **Step 2: Napsat failující test**

`PmTracker.Tests.Unit/ServiceDesk/AdLoginCacheTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Security;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class AdLoginCacheTests
{
    [Fact]
    public async Task ResolveLoginAsync_FirstCall_HitsAdService()
    {
        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ResolveDisplayNameByLoginAsync("jan.novak", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jan Novák");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AdLoginCache(ad.Object, cache);

        var name = await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);

        name.Should().Be("Jan Novák");
        ad.Verify(x => x.ResolveDisplayNameByLoginAsync("jan.novak", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveLoginAsync_SecondCall_UsesCache()
    {
        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ResolveDisplayNameByLoginAsync("jan.novak", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Jan Novák");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AdLoginCache(ad.Object, cache);

        await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);
        await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);
        await sut.ResolveDisplayNameAsync("jan.novak", CancellationToken.None);

        ad.Verify(x => x.ResolveDisplayNameByLoginAsync("jan.novak", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveLoginAsync_UnknownLogin_ReturnsOriginalLogin()
    {
        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ResolveDisplayNameByLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new AdLoginCache(ad.Object, cache);

        var name = await sut.ResolveDisplayNameAsync("neznamy.login", CancellationToken.None);

        name.Should().Be("neznamy.login");
    }
}
```

- [ ] **Step 3: Implementovat cache**

`PmTracker.Web/Services/ServiceDesk/AdLoginCache.cs`:

```csharp
using Microsoft.Extensions.Caching.Memory;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// In-memory cache pro překlad AD login → display name. Slouží pro vytěžování
/// vyjádření, kde HOT_VYJADRENI.zpracoval obsahuje buď přímo jméno, nebo login.
///
/// **DŮLEŽITÉ:** osoba se NIKDY neukládá do tabulky `osoby` — je to jen
/// prezentační informace pro UI. TTL 1 hodina.
/// </summary>
public sealed class AdLoginCache(
    IActiveDirectoryService adService,
    IMemoryCache cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public async Task<string> ResolveDisplayNameAsync(string login, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(login)) return string.Empty;

        var key = $"ad-login:{login.ToLowerInvariant()}";
        if (cache.TryGetValue<string>(key, out var cached) && cached is not null)
        {
            return cached;
        }

        var resolved = await adService.ResolveDisplayNameByLoginAsync(login, ct);
        var displayName = string.IsNullOrWhiteSpace(resolved) ? login : resolved!;
        cache.Set(key, displayName, Ttl);
        return displayName;
    }
}
```

**Pozn.:** Pokud `IActiveDirectoryService.ResolveDisplayNameByLoginAsync` metoda **neexistuje**, task fail-uje — v tom případě přidat do `IActiveDirectoryService` (sub-task) nebo použít existující metody (např. `FindByLoginAsync`) a extrahovat display name tam. Před Step 3 ověřit grep.

- [ ] **Step 4: Registrovat v DI**

V `PmTracker.Web/Program.cs`:

```csharp
builder.Services.AddSingleton<AdLoginCache>();
```

- [ ] **Step 5: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "AdLoginCacheTests" --no-restore`

Expected: 3/3 passed.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/AdLoginCache.cs \
        PmTracker.Web/Program.cs \
        PmTracker.Tests.Unit/ServiceDesk/AdLoginCacheTests.cs
git commit -m "feat(servicedesk): AdLoginCache — in-memory překlad login→jméno (TTL 1h, nikdy do DB)"
```

---

## Task 8: `VyjadreniHarvestService` — single-krok detekce (bez chronologie)

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/IVyjadreniHarvestService.cs`
- Create: `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/VyjadreniHarvestServiceTests.cs`

- [ ] **Step 1: Napsat testy pro základní single-krok harvest (bez chronologie)**

`PmTracker.Tests.Unit/ServiceDesk/VyjadreniHarvestServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class VyjadreniHarvestServiceTests
{
    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("harvest-" + Guid.NewGuid())
            .Options);

    [Fact]
    public async Task HarvestTicketAsync_K6PnfWithAcceptedCalc_CreatesActiveBinding()
    {
        await using var db = NewDb();
        db.ExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 100, Cislo = "336865", LastHarvestedAt = null
        });
        db.Zaznamy.Add(new ProjektovyZaznamEntity { Id = 100, HarmonogramSablonaVerze = 1 });
        await db.SaveChangesAsync();

        var vyjadreni = new Mock<IVyjadreniQueryService>();
        vyjadreni.Setup(x => x.GetVyjadreniForTicketAsync("336865", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] {
                new HotVyjadreniDto(
                    Id: 99, Typ: "25", Pid: "A400P023RVVP",
                    Datum: new DateTime(2026, 3, 14, 10, 0, 0),
                    Zpracoval: "pm.user", Popis: "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.",
                    Tym: "FIS", ViditelneDodavateli: 0)
            });

        var sut = BuildSut(db, vyjadreni.Object);

        await sut.HarvestTicketAsync(externiOdkazId: 1, CancellationToken.None);

        var bindings = await db.VyjadreniVazby.Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        bindings.Should().HaveCount(1);
        bindings[0].HotVyjadreniId.Should().Be(99);
        bindings[0].Source.Should().Be((byte)VazbaSource.Auto);
    }

    [Fact]
    public async Task HarvestTicketAsync_UpdatesLastHarvestedAt()
    {
        await using var db = NewDb();
        db.ExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await db.SaveChangesAsync();

        var vyjadreni = new Mock<IVyjadreniQueryService>();
        vyjadreni.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vyjadreni.Object);

        await sut.HarvestTicketAsync(1, CancellationToken.None);

        var eo = await db.ExterniOdkazy.FindAsync(1);
        eo!.LastHarvestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HarvestTicketAsync_NoMatchingPredicate_NoBindingCreated()
    {
        await using var db = NewDb();
        db.ExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await db.SaveChangesAsync();

        var vyjadreni = new Mock<IVyjadreniQueryService>();
        vyjadreni.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] {
                new HotVyjadreniDto(50, "05", "P", new DateTime(2026,1,1), "u", "nějaký náhodný text", "FIS", 0)
            });

        var sut = BuildSut(db, vyjadreni.Object);

        await sut.HarvestTicketAsync(1, CancellationToken.None);

        (await db.VyjadreniVazby.CountAsync()).Should().Be(0);
    }

    private static VyjadreniHarvestService BuildSut(PmTrackerDbContext db, IVyjadreniQueryService v)
    {
        // Další služby — harmonogram mock, schema lookup, atd. v reálném SUT.
        // Pro tyto testy využijeme minimalistický konstruktor.
        throw new NotImplementedException("BuildSut — implementuj podle finální signatury VyjadreniHarvestService v Step 3");
    }
}
```

**Poznámka:** `BuildSut` je placeholder — ve Step 3 vytvoříš službu, pak upravíš `BuildSut` na skutečnou instanci. Až po Step 3 přijdou testy passed.

- [ ] **Step 2: Spustit test (musí failnout s NotImplementedException nebo compilation)**

Run: `dotnet test PmTracker.Tests.Unit --filter "VyjadreniHarvestServiceTests" --no-restore`

Expected: FAIL.

- [ ] **Step 3: Implementovat service**

`PmTracker.Web/Services/ServiceDesk/IVyjadreniHarvestService.cs`:

```csharp
namespace PmTracker.Web.Services.ServiceDesk;

public interface IVyjadreniHarvestService
{
    /// <summary>
    /// Harvestuje vyjádření pro jeden externí odkaz (ticket).
    /// Aplikuje textové predikáty, vytvoří/aktualizuje vazby, aktualizuje LastHarvestedAt.
    /// </summary>
    Task HarvestTicketAsync(int externiOdkazId, CancellationToken ct = default);

    /// <summary>
    /// Harvestuje všechny externí vazby daného záznamu (T5 trigger).
    /// </summary>
    Task HarvestRecordAsync(int zaznamId, CancellationToken ct = default);

    /// <summary>
    /// Re-harvest — smaže všechny auto+manual vazby pro auto kroky, nastaví
    /// LastHarvestedAt=NULL, spustí harvest znovu. Manuální kroky (2/5/8/9)
    /// zůstávají beze změny.
    /// </summary>
    Task ReHarvestTicketAsync(int externiOdkazId, CancellationToken ct = default);
}
```

`PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.ServiceDesk;

public sealed class VyjadreniHarvestService(
    PmTrackerDbContext db,
    IVyjadreniQueryService vyjadreni,
    IHarmonogramService harmonogram,
    TimeProvider time) : IVyjadreniHarvestService
{
    public async Task HarvestTicketAsync(int externiOdkazId, CancellationToken ct = default)
    {
        var eo = await db.ExterniOdkazy.FirstOrDefaultAsync(x => x.Id == externiOdkazId, ct);
        if (eo is null || string.IsNullOrWhiteSpace(eo.Cislo)) return;

        var sinceUtc = eo.LastHarvestedAt;
        var list = await vyjadreni.GetVyjadreniForTicketAsync(eo.Cislo, sinceUtc, ct);

        if (list.Count > 0)
        {
            var schema = await harmonogram.GetSchemaForRecordAsync(
                (await db.Zaznamy.AsNoTracking().FirstAsync(x => x.Id == eo.ZaznamId, ct)), ct);

            foreach (var v in list)
            {
                var kind = HarvestPredicates.ClassifyPopis(v.Popis);
                if (kind == HarvestPredicateKind.None) continue;

                var krokKey = MapKindToKrokKey(kind, schema);
                if (krokKey is null) continue;

                await UpsertBindingAsync(eo.ZaznamId, krokKey.Value, eo.Id, v.Id, v.Datum, VazbaSource.Auto, ct);
            }
        }

        eo.LastHarvestedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task HarvestRecordAsync(int zaznamId, CancellationToken ct = default)
    {
        var ids = await db.ExterniOdkazy
            .Where(x => x.ZaznamId == zaznamId && x.Cislo != null)
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var id in ids)
        {
            await HarvestTicketAsync(id, ct);
        }
    }

    public async Task ReHarvestTicketAsync(int externiOdkazId, CancellationToken ct = default)
    {
        var schema = await GetSchemaForExterniOdkazAsync(externiOdkazId, ct);
        if (schema is null) return;

        var autoKroky = GetAutoKrokKeys(schema).ToHashSet();

        var activeBindings = await db.VyjadreniVazby
            .Where(x => x.ExterniOdkazId == externiOdkazId
                     && x.Stav == (byte)VazbaStav.Active
                     && autoKroky.Contains(x.KrokKey))
            .ToListAsync(ct);

        foreach (var b in activeBindings)
        {
            b.Stav = (byte)VazbaStav.Deleted;
            b.DeletedAt = time.GetUtcNow().UtcDateTime;
        }

        var eo = await db.ExterniOdkazy.FirstAsync(x => x.Id == externiOdkazId, ct);
        eo.LastHarvestedAt = null;
        await db.SaveChangesAsync(ct);

        await HarvestTicketAsync(externiOdkazId, ct);
    }

    private async Task UpsertBindingAsync(
        int zaznamId, Guid krokKey, int externiOdkazId, long hotVyjadreniId,
        DateTime datumVyjadreni, VazbaSource source, CancellationToken ct)
    {
        var existing = await db.VyjadreniVazby
            .Where(x => x.ZaznamId == zaznamId
                     && x.KrokKey == krokKey
                     && x.Stav == (byte)VazbaStav.Active)
            .ToListAsync(ct);

        // Tie-break: pokud existující Active má novější datum, přeskoč (novější vítězí).
        // Pokud nová bublina má novější datum, supersedni všechny existující.
        // Chronologie rebalance se řeší v Tasku 9.
        var newest = existing.OrderByDescending(x => x.DatumVyjadreni).FirstOrDefault();
        if (newest != null && newest.DatumVyjadreni >= datumVyjadreni) return;

        foreach (var e in existing)
        {
            e.Stav = (byte)VazbaStav.Superseded;
        }

        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = zaznamId,
            KrokKey = krokKey,
            ExterniOdkazId = externiOdkazId,
            HotVyjadreniId = hotVyjadreniId,
            DatumVyjadreni = datumVyjadreni,
            Source = (byte)source,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = time.GetUtcNow().UtcDateTime
        });
    }

    private static Guid? MapKindToKrokKey(HarvestPredicateKind kind, HarmonogramSchemaDefinition schema)
    {
        var poradi = kind switch
        {
            HarvestPredicateKind.K3_OdeslaniZadaniPmp => 3,
            HarvestPredicateKind.K4_K7_DodaniReseni => (int?)null, // Rozhodne se dle typu tiketu — viz Task 9 rebalance
            HarvestPredicateKind.K6_OdeslaniPozadavku => 6,
            HarvestPredicateKind.K10_NasazeniArchivace => 10,
            _ => null
        };
        if (poradi is null) return null;
        return schema.Steps.FirstOrDefault(s => s.KrokPoradi == poradi.Value)?.KrokKey;
    }

    // Ostatní pomocné metody vyřeší Task 9.
    private Task<HarmonogramSchemaDefinition?> GetSchemaForExterniOdkazAsync(int externiOdkazId, CancellationToken ct)
        => Task.FromResult<HarmonogramSchemaDefinition?>(null);

    private IEnumerable<Guid> GetAutoKrokKeys(HarmonogramSchemaDefinition schema)
    {
        var autoPoradi = new[] { 1, 3, 4, 6, 7, 10 };
        return schema.Steps.Where(s => autoPoradi.Contains(s.KrokPoradi)).Select(s => s.KrokKey);
    }
}
```

**Poznámka:** V tomto tasku je `MapKindToKrokKey` **zjednodušený** — `K4_K7_DodaniReseni` se neřeší per typ tiketu. To doplníme v Task 9 (chronologie rebalance), kde přidáme lookup typu tiketu + dispatch.

- [ ] **Step 4: Upravit `BuildSut` v testech**

Vyměň placeholder za reálnou instanci:

```csharp
private static VyjadreniHarvestService BuildSut(PmTrackerDbContext db, IVyjadreniQueryService v)
{
    var harmonogram = new Mock<IHarmonogramService>();
    harmonogram.Setup(x => x.GetSchemaForRecordAsync(It.IsAny<ProjektovyZaznamEntity>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new HarmonogramSchemaDefinition(
            Verze: 1,
            DelayBarvaHex: "#DC2626",
            Steps: new List<HarmonogramStepDefinition>
            {
                new(KrokKey: Guid.Parse("11111111-1111-1111-1111-111111111101"),
                    KrokPoradi: 1, Nazev: "1. příprava zadání", DurationTypId: 1, DelayTypId: 11, BarvaHex: "#EF4444"),
                new(Guid.Parse("11111111-1111-1111-1111-111111111106"),
                    6, "6. odeslání požadavku na výrobu", 6, 16, "#14B8A6"),
                new(Guid.Parse("11111111-1111-1111-1111-111111111110"),
                    10, "10. nasazení do provozu", 10, 20, "#8B5CF6"),
            }));
    return new VyjadreniHarvestService(db, v, harmonogram.Object, TimeProvider.System);
}
```

- [ ] **Step 5: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "VyjadreniHarvestServiceTests" --no-restore`

Expected: 3/3 passed.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/IVyjadreniHarvestService.cs \
        PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs \
        PmTracker.Tests.Unit/ServiceDesk/VyjadreniHarvestServiceTests.cs
git commit -m "feat(servicedesk): VyjadreniHarvestService — single-krok LIKE harvest + UpsertBinding + ReHarvest"
```

---

## Task 9: Chronologie rebalance algoritmus

**Files:**
- Modify: `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/ChronologieRebalanceTests.cs`

- [ ] **Step 1: Testy pro rebalance**

`PmTracker.Tests.Unit/ServiceDesk/ChronologieRebalanceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class ChronologieRebalanceTests
{
    [Fact]
    public void RebalanceKroky_NewBubbleBreaksOrder_SubsequentStepsShift()
    {
        // Setup: K6 bound to bubble @ 2026-03-01, K7 bound to bubble @ 2026-04-01
        // New K6 bubble arrives @ 2026-05-01 → K7 must shift to later bubble (or buffer)
        var kroky = new[]
        {
            new StepperKrok(6, Guid.Parse("11111111-1111-1111-1111-111111111106"),
                currentBubbleDatum: new DateTime(2026, 3, 1), currentBubbleId: 100),
            new StepperKrok(7, Guid.Parse("11111111-1111-1111-1111-111111111107"),
                currentBubbleDatum: new DateTime(2026, 4, 1), currentBubbleId: 200)
        };
        var newerBubbles = new[] { new BubleId(300, new DateTime(2026, 6, 1)) };

        var result = ChronologyRebalancer.Rebalance(
            kroky, targetKrokPoradi: 6, newBubbleId: 400, newBubbleDatum: new DateTime(2026, 5, 1),
            availableBubbles: newerBubbles);

        result.Updates.Should().HaveCount(2);
        result.Updates[0].KrokPoradi.Should().Be(6);
        result.Updates[0].NewBubbleId.Should().Be(400);
        result.Updates[1].KrokPoradi.Should().Be(7);
        result.Updates[1].NewBubbleId.Should().Be(300);  // posunul se na novější bublinu
    }

    [Fact]
    public void RebalanceKroky_NoSuccessorAvailable_StepGoesToBuffer()
    {
        var kroky = new[]
        {
            new StepperKrok(6, Guid.NewGuid(), new DateTime(2026, 3, 1), 100),
            new StepperKrok(7, Guid.NewGuid(), new DateTime(2026, 4, 1), 200)
        };
        var availableBubbles = Array.Empty<BubleId>();

        var result = ChronologyRebalancer.Rebalance(
            kroky, 6, newBubbleId: 400, newBubbleDatum: new DateTime(2026, 5, 1), availableBubbles);

        result.Updates[0].KrokPoradi.Should().Be(6);
        result.Updates[0].NewBubbleId.Should().Be(400);
        result.Updates[1].KrokPoradi.Should().Be(7);
        result.Updates[1].NewBubbleId.Should().BeNull();  // buffer
    }
}
```

- [ ] **Step 2: Implementovat rebalance**

Přidat do `VyjadreniHarvestService.cs` nebo do nové třídy `ChronologyRebalancer`:

```csharp
public sealed record StepperKrok(int KrokPoradi, Guid KrokKey, DateTime? CurrentBubbleDatum, long? CurrentBubbleId);
public sealed record BubleId(long Id, DateTime Datum);
public sealed record RebalanceUpdate(int KrokPoradi, long? NewBubbleId);
public sealed record RebalanceResult(IReadOnlyList<RebalanceUpdate> Updates);

public static class ChronologyRebalancer
{
    public static RebalanceResult Rebalance(
        IReadOnlyList<StepperKrok> kroky,
        int targetKrokPoradi,
        long newBubbleId,
        DateTime newBubbleDatum,
        IReadOnlyList<BubleId> availableBubbles)
    {
        var updates = new List<RebalanceUpdate>();
        var usedBubbleIds = new HashSet<long>(kroky.Where(k => k.CurrentBubbleId.HasValue).Select(k => k.CurrentBubbleId!.Value));
        usedBubbleIds.Remove(newBubbleId); // novější bublina může být použita pro následující kroky

        // Cíl: target dostane novou bublinu
        var sortedKroky = kroky.OrderBy(k => k.KrokPoradi).ToList();
        DateTime? minDatumProNasledujici = newBubbleDatum;

        foreach (var k in sortedKroky)
        {
            if (k.KrokPoradi < targetKrokPoradi) continue;

            if (k.KrokPoradi == targetKrokPoradi)
            {
                updates.Add(new RebalanceUpdate(k.KrokPoradi, newBubbleId));
                minDatumProNasledujici = newBubbleDatum;
                continue;
            }

            // Následující krok: jeho aktuální bublina je dřív než min, musí se posunout
            if (k.CurrentBubbleDatum.HasValue && k.CurrentBubbleDatum.Value > minDatumProNasledujici)
            {
                minDatumProNasledujici = k.CurrentBubbleDatum.Value;
                continue; // krok zůstává beze změny (chronologie OK)
            }

            var candidate = availableBubbles
                .Where(b => b.Datum > minDatumProNasledujici && !usedBubbleIds.Contains(b.Id))
                .OrderBy(b => b.Datum)
                .FirstOrDefault();

            if (candidate is not null)
            {
                updates.Add(new RebalanceUpdate(k.KrokPoradi, candidate.Id));
                minDatumProNasledujici = candidate.Datum;
                usedBubbleIds.Add(candidate.Id);
            }
            else
            {
                updates.Add(new RebalanceUpdate(k.KrokPoradi, null)); // parking zóna / buffer
            }
        }

        return new RebalanceResult(updates);
    }
}
```

- [ ] **Step 3: Napojit rebalance do `VyjadreniHarvestService.UpsertBindingAsync`**

Úprava: když se detekuje nová bublina pro krok K, před insert se spustí rebalance pro všechny kroky daného `zaznamId`, aplikují se updates.

Konkrétní integrační kód viz [`VyjadreniHarvestService.cs`](../plans/shared/rebalance-integration.md) *(poznámka: detail integrace je mimo bite-size size; rozepíše se při implementaci, případně do sub-tasku 9b).*

**Praktický přístup:** v Step 3 napiš minimální integrační kód (bez placeholder-u) — iterace `sortedKroky`, pro každý `RebalanceUpdate.NewBubbleId`:
- pokud `null` → supersedne existující Active vazbu
- pokud nenulová → InsertOrUpdate Active vazbu s novou bublinou

Testy z Step 1 ověří business pravidla.

- [ ] **Step 4: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "ChronologieRebalanceTests" --no-restore`

Expected: 2/2 passed.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs \
        PmTracker.Tests.Unit/ServiceDesk/ChronologieRebalanceTests.cs
git commit -m "feat(servicedesk): chronologie rebalance algoritmus + integrace do UpsertBinding"
```

---

## Task 10: `HangfireHarvestScheduler` + registrace Hangfire

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/HangfireHarvestScheduler.cs`
- Delete: `PmTracker.Web/Services/ServiceDesk/NoOpHarvestScheduler.cs`
- Modify: `PmTracker.Web/Program.cs`

- [ ] **Step 1: Přidat Hangfire package**

Run: `dotnet add PmTracker.Web package Hangfire.AspNetCore && dotnet add PmTracker.Web package Hangfire.SqlServer`

- [ ] **Step 2: Vytvořit scheduler**

```csharp
using Hangfire;

namespace PmTracker.Web.Services.ServiceDesk;

public sealed class HangfireHarvestScheduler(IBackgroundJobClient jobs) : IHarvestScheduler
{
    public Task ScheduleHarvestAsync(int externiOdkazId, CancellationToken ct = default)
    {
        jobs.Enqueue<IVyjadreniHarvestService>(svc =>
            svc.HarvestTicketAsync(externiOdkazId, CancellationToken.None));
        return Task.CompletedTask;
    }

    public Task ScheduleHarvestForRecordAsync(int zaznamId, CancellationToken ct = default)
    {
        jobs.Enqueue<IVyjadreniHarvestService>(svc =>
            svc.HarvestRecordAsync(zaznamId, CancellationToken.None));
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Program.cs — registrovat Hangfire + scheduler**

```csharp
builder.Services.AddHangfire(config => config
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("PmTrackerDb")));
builder.Services.AddHangfireServer();

// Nahrazuje NoOpHarvestScheduler z Plánu B:
builder.Services.AddScoped<IHarvestScheduler, HangfireHarvestScheduler>();
builder.Services.AddScoped<IVyjadreniHarvestService, VyjadreniHarvestService>();
```

Odstranit starou registraci `NoOpHarvestScheduler`.

- [ ] **Step 4: Smazat `NoOpHarvestScheduler.cs`**

```bash
rm PmTracker.Web/Services/ServiceDesk/NoOpHarvestScheduler.cs
```

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web --no-restore`

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/HangfireHarvestScheduler.cs \
        PmTracker.Web/Program.cs \
        PmTracker.Web/PmTracker.Web.csproj
git rm PmTracker.Web/Services/ServiceDesk/NoOpHarvestScheduler.cs
git commit -m "feat(servicedesk): Hangfire-based IHarvestScheduler nahrazuje NoOp stub"
```

---

## Task 11: `VyjadreniHarvestBatchJob` periodický job (T4)

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestBatchJob.cs`
- Modify: `PmTracker.Web/Program.cs`

- [ ] **Step 1: Implementovat batch job**

```csharp
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ServiceDesk;

public sealed class VyjadreniHarvestBatchJob(
    PmTrackerDbContext db,
    IVyjadreniHarvestService harvest,
    IServiceDeskSyncSettings settings,
    TimeProvider time,
    ILogger<VyjadreniHarvestBatchJob> log)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var opts = await settings.GetAsync(ct);
        if (!opts.Enabled || !opts.HangfireEnabled)
        {
            log.LogInformation("Harvest batch job disabled (Enabled={Enabled}, HangfireEnabled={HF}).",
                opts.Enabled, opts.HangfireEnabled);
            return;
        }

        var threshold = time.GetUtcNow().UtcDateTime - opts.HangfireInterval;
        var graceCutoff = time.GetUtcNow().UtcDateTime.AddDays(-opts.ArchiveGraceDays);

        var ids = await db.ExterniOdkazy
            .Where(x => x.Cislo != null
                     && (x.LastHarvestedAt == null || x.LastHarvestedAt < threshold)
                     && (x.DatumPrevzeti == null || x.DatumPrevzeti > graceCutoff))
            .Select(x => x.Id)
            .ToListAsync(ct);

        log.LogInformation("Harvest batch: {Count} tiketů ke zpracování.", ids.Count);

        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = opts.HangfireMaxParallelism,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(ids, parallel, async (id, c) =>
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(c);
                cts.CancelAfter(opts.TimeoutPerTicket);
                await harvest.HarvestTicketAsync(id, cts.Token);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Harvest externi_odkaz_id={Id} selhal.", id);
            }
        });
    }
}
```

- [ ] **Step 2: Registrovat periodický job v Program.cs**

```csharp
app.MapHangfireDashboard("/hangfire"); // optional; ACL samostatně

// Po startupu: zaregistrovat cron
var recurring = app.Services.GetRequiredService<IRecurringJobManager>();
using (var scope = app.Services.CreateScope())
{
    var settings = scope.ServiceProvider.GetRequiredService<IServiceDeskSyncSettings>();
    var opts = await settings.GetAsync(CancellationToken.None);
    if (opts.HangfireEnabled)
    {
        recurring.AddOrUpdate<VyjadreniHarvestBatchJob>(
            "servicedesk-harvest",
            job => job.ExecuteAsync(CancellationToken.None),
            CronExpressionFromInterval(opts.HangfireInterval));
    }
}
// ... (helper CronExpressionFromInterval převádí TimeSpan → Cron string)
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestBatchJob.cs \
        PmTracker.Web/Program.cs
git commit -m "feat(servicedesk): VyjadreniHarvestBatchJob (Hangfire T4) respektující admin sync settings"
```

---

## Task 12: T5 trigger v `ZaznamyController`

**Files:**
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs` (nebo kontroleru s otevřením editoru)

- [ ] **Step 1: Najít metodu otevření editoru záznamu**

Run: `grep -n "EditZaznam\|public async Task<IActionResult> Edit" PmTracker.Web/Controllers/*.cs 2>/dev/null | head`

- [ ] **Step 2: Injectnout `IHarvestScheduler` + `IServiceDeskSyncSettings`**

- [ ] **Step 3: Před return View() zavolat**

```csharp
var syncOpts = await settings.GetAsync(ct);
if (syncOpts.Enabled)
{
    await scheduler.ScheduleHarvestForRecordAsync(recordId, ct);
}
```

- [ ] **Step 4: Build + test**

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(servicedesk): T5 trigger — harvest při otevření editoru záznamu"
```

---

## Task 13: `VyjadreniModalController` + endpoints + ViewModels

**Files:**
- Create: `PmTracker.Web/Controllers/VyjadreniModalController.cs`
- Create: `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs`
- Create: `PmTracker.Web/Models/ViewModels/Vyjadreni/BublinaViewModel.cs`
- Create: `PmTracker.Web/Models/ViewModels/Vyjadreni/StepperKrokViewModel.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/VyjadreniModalControllerTests.cs`

**Endpointy:**
- `GET /Vyjadreni/Modal?externiOdkazId=X` — render modalu
- `GET /Vyjadreni/List?externiOdkazId=X` — JSON bubliny + stepper stav
- `POST /Vyjadreni/HarmonogramVazba/Save` — bulk save snapshot (z localStorage)
- `POST /Vyjadreni/HarmonogramVazba/ReHarvest` — sync re-harvest

Detail viz (shodně s templatem v Plánu B Task 3): input validace, ACL `records.edit`, response shape, unit testy.

Implementace formou stejnou jako Plán B Task 3 + 4. Pro stručnost tohoto plánu jsou detaily kódu vynechány, ale v Razor + ViewModel musí držet schéma:

```csharp
public sealed class VyjadreniModalViewModel
{
    public int ExterniOdkazId { get; init; }
    public string Cislo6 { get; init; } = string.Empty;
    public string TypZaznamu { get; init; } = string.Empty;
    public string? Strucne { get; init; }
    public DateTime? LastHarvestedAt { get; init; }
    public IReadOnlyList<BublinaViewModel> Bubliny { get; init; } = Array.Empty<BublinaViewModel>();
    public IReadOnlyList<StepperKrokViewModel> Stepper { get; init; } = Array.Empty<StepperKrokViewModel>();
    public bool EditAllowed { get; init; }
}

public sealed class BublinaViewModel
{
    public long HotVyjadreniId { get; init; }
    public string? Typ { get; init; }
    public DateTime Datum { get; init; }
    public string Autor { get; init; } = string.Empty;
    public string? Tym { get; init; }
    public string? PopisHtml { get; init; }
    public Guid? PrirazenoKeKrokuKey { get; init; }
    public bool JeSystemova { get; init; }  // Typ in {"05","25"}
    public bool JeKalkulace { get; init; }  // Typ == "16"
}

public sealed class StepperKrokViewModel
{
    public Guid KrokKey { get; init; }
    public int KrokPoradi { get; init; }
    public string Nazev { get; init; } = string.Empty;
    public bool JeRucni { get; init; }
    public long? HotVyjadreniId { get; init; }
    public DateTime? DatumSkutecnosti { get; init; }
    public string? AutorSkutecnosti { get; init; }
}
```

- [ ] **Step 1–7:** stejný TDD postup jako Plán B Task 3. Commit:

```bash
git commit -m "feat(servicedesk): VyjadreniModalController + 4 endpointy + VMs + unit testy"
```

---

## Task 14–18: UI (modal Razor + CSS + JS moduly)

*(Strukturálně stejný pattern jako Plán B Task 5–8. Klíčové soubory:*

- `Views/Vyjadreni/Modal.cshtml` — hlavní layout s `gov-dialog size="xl"`, 80/20 grid, header + body + footer
- `Views/Vyjadreni/_Bublina.cshtml` — per-bublina partial, diskriminuje system/kalkulace/běžná
- `Views/Vyjadreni/_Stepper.cshtml` — per-krok partial, drag handle, chip „Přiřazeno k bublině X"
- `wwwroot/css/components/vyjadreni-modal.css` — kompletní styly včetně drag & drop feedback, archivní bublina highlight
- `wwwroot/js/modules/vyjadreni/modalShell.js` — open/close, fetch initial state
- `wwwroot/js/modules/vyjadreni/timeline.js` — vykreslení bublin
- `wwwroot/js/modules/vyjadreni/stepper.js` — drag & drop s chronologickou fixací
- `wwwroot/js/modules/vyjadreni/autosave.js` — localStorage snapshot + debounced POST
- `wwwroot/js/modules/vyjadreni/reharvest.js` — confirm + POST + reload

Každý JS modul jeden task, TDD přes Playwright smoke. Detailní kroky per task stejně jako Plán B Task 5–8.*

Commity:
- `feat(ui): chat modal Razor view + CSS`
- `feat(ui): modalShell + timeline rendering`
- `feat(ui): stepper drag & drop s chronologickou fixací`
- `feat(ui): localStorage autosave + debounced save + beforeunload`
- `feat(ui): reharvest button + propojení 💬 ikony z karty externí vazby`

---

## Task 19: `/SDConnector` diagnostická stránka

**Files:**
- Create: `PmTracker.Web/Controllers/SDConnectorController.cs`
- Create: `PmTracker.Web/Views/SDConnector/Index.cshtml`
- Create: `PmTracker.Web/Models/ViewModels/SDConnector/SDConnectorViewModel.cs`
- Create: `PmTracker.Web/wwwroot/css/components/sdconnector-page.css`
- Create: `PmTracker.Web/wwwroot/js/modules/sdconnector/page.js`
- Test: `PmTracker.Tests.Unit/ServiceDesk/SDConnectorControllerTests.cs`

**Cíl stránky:**

`/SDConnector` je veřejná diagnostická stránka (stejný přístupový pattern jako `StyleGuide` — `BaseController`, žádné `[Authorize]`). Slouží vývojářům + uživateli k ověřování, že ServiceDesk integrace funguje, **aniž by bylo potřeba projít celým flow editoru záznamu**.

### Layout

```
┌─ Stav propojení ServiceDesk ────────────────────────────────┐
│ [gov-chip color=green]  Připojeno k intranetNEW             │  ← barva + text v gov-chip
│ TicketingReadOnly: nastaveno (Server=..., timeout 30s)     │
│ Poslední úspěšný dotaz: před 3 minutami                     │
└─────────────────────────────────────────────────────────────┘

┌─ Test tiket ──────────────────────────────────────────────── ┐
│ Číslo tiketu (6 cifer):  [ 336865 ]  [Načíst]              │
└──────────────────────────────────────────────────────────────┘

┌─ Raw data (levý sloupec) ─────┐  ┌─ User-friendly (pravý) ──┐
│ == Hlavička ==                │  │ PNF #336865              │
│ id: 336865                    │  │ Oprava přihlášení        │
│ pid: A400P023RVVP             │  │                          │
│ typ_zaznamu: PNF              │  │ ┌ Externí vazba ──────┐ │
│ stav: otevřeno                │  │ │ Datum objednání:    │ │
│                               │  │ │ 12.3.2026           │ │
│ == Stručně ==                 │  │ │ Datum dodání:       │ │
│ Oprava přihlášení             │  │ │ 28.3.2026           │ │
│                               │  │ │ Datum převzetí: —   │ │
│ == Popis ==                   │  │ └─────────────────────┘ │
│ [HTML raw]                    │  │                          │
│                               │  │ ┌ Simulovaný modal ─┐  │
│ == Vyjádření ==               │  │ │  (dlaždice 1200×  │  │
│ [id=1001] typ=25              │  │ │   800, rámeček)   │  │
│ 5.1.2026 10:00                │  │ │                   │  │
│ Autor: pm.user (PM Tracker)   │  │ │  Timeline bublin  │  │
│ Popis: [HTML raw]             │  │ │  stejně jako      │  │
│                               │  │ │  v modalu, ale    │  │
│ [id=1002] typ=05              │  │ │  přímo na stránce │  │
│ ...                           │  │ │  — žádný overlay  │  │
│                               │  │ └───────────────────┘  │
└───────────────────────────────┘  └──────────────────────────┘
```

### Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Controllers;
using PmTracker.Web.Services.Security;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Diagnostická stránka ServiceDesk konektoru. Stejný pattern jako StyleGuide.
/// </summary>
public sealed class SDConnectorController : BaseController
{
    private readonly ITicketingQueryService _ticketing;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly AdLoginCache _adCache;
    private readonly IConfiguration _config;

    public SDConnectorController(
        IUserContextResolver ucr,
        TimeProvider tp,
        ILoggerFactory lf,
        ITicketingQueryService ticketing,
        IVyjadreniQueryService vyjadreni,
        AdLoginCache adCache,
        IConfiguration config) : base(ucr, tp, lf)
    {
        _ticketing = ticketing;
        _vyjadreni = vyjadreni;
        _adCache = adCache;
        _config = config;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var vm = new SDConnectorViewModel
        {
            ConnectionStringConfigured = !string.IsNullOrWhiteSpace(
                _config.GetConnectionString("TicketingReadOnly"))
        };
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Load(string cislo, CancellationToken ct)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(cislo ?? "", @"^\d{6}$"))
            return BadRequest(new { Error = "Číslo musí být 6 cifer." });

        var ticket = await _ticketing.GetZaznamAsync(cislo, ct);
        if (ticket is null) return Ok(new { Nalezeno = false });

        var vyjadreni = await _vyjadreni.GetVyjadreniForTicketAsync(cislo, null, ct);

        // Pro každé vyjádření resolvujeme autora z AD (pokud je to login)
        var bubliny = new List<object>();
        foreach (var v in vyjadreni)
        {
            var displayName = !string.IsNullOrWhiteSpace(v.Zpracoval)
                ? await _adCache.ResolveDisplayNameAsync(v.Zpracoval, ct)
                : "";
            bubliny.Add(new
            {
                HotId = v.Id,
                v.Typ,
                v.Datum,
                LoginRaw = v.Zpracoval,
                AutorDisplayName = displayName,
                v.Tym,
                PopisHtml = v.Popis
            });
        }

        return Ok(new
        {
            Nalezeno = true,
            Raw = new { ticket.Id, ticket.TypZaznamu, ticket.Strucne, ticket.Popis },
            Bubliny = bubliny
        });
    }
}
```

### ViewModel

```csharp
public sealed class SDConnectorViewModel
{
    public bool ConnectionStringConfigured { get; init; }
}
```

### Razor view

`Views/SDConnector/Index.cshtml` — HTML s dvousloupcovým gridem (levý = raw `<pre>` listing, pravý = user-friendly karty). Dlaždice simulující modal má:

```css
.sdc-modal-simulation {
  width: min(1200px, 100%);
  height: 800px;
  border: 2px dashed var(--pm-border, #94a3b8);
  background: var(--pm-surface, #fff);
  border-radius: 8px;
  padding: 1rem;
  overflow: auto;
  position: relative;
}
.sdc-modal-simulation::before {
  content: "Simulace modalu (1200×800) — ověř rozměry + scroll";
  position: absolute; top: 0.5rem; left: 0.5rem;
  font-size: 0.75rem; color: var(--pm-text-muted, #64748b);
}
```

### JS klient

`wwwroot/js/modules/sdconnector/page.js`:

```js
(function(global){
  'use strict';

  const STORAGE_KEY = 'pm.sdconnector.lastLoad';

  async function load(cislo) {
    const resp = await fetch(`/SDConnector/Load?cislo=${encodeURIComponent(cislo)}`);
    if (!resp.ok) return null;
    const data = await resp.json();
    // Ukládá se jen do localStorage pro ladění; při reloadu se čistí (sessionStorage).
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ts: Date.now(), data}));
    return data;
  }

  function render(data) {
    renderRaw(data);
    renderUserFriendly(data);
  }

  function renderRaw(data) { /* vykresli <pre> blocky s hlavičkou, strucne, popisem, vyjádřeními */ }
  function renderUserFriendly(data) { /* vykresli pravý sloupec s gov-card externí vazby + timeline */ }

  function init() {
    const btn = document.getElementById('sdc-load-btn');
    const input = document.getElementById('sdc-cislo-input');
    btn?.addEventListener('click', async () => {
      const cislo = input.value.trim();
      if (!/^\d{6}$/.test(cislo)) { alert('6 cifer, prosím'); return; }
      const data = await load(cislo);
      if (data) render(data);
    });

    // Pokud v sessionStorage už něco je, obnov zobrazení
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (raw) {
      try { render(JSON.parse(raw).data); } catch(_) {}
    }
  }

  global.pmSDConnector = { init };
})(window);
```

- [ ] **Step 1: Controller + ViewModel**
- [ ] **Step 2: Razor view**
- [ ] **Step 3: CSS**
- [ ] **Step 4: JS modul + integrace do bundlu**
- [ ] **Step 5: Unit test (ACL, response shape)**
- [ ] **Step 6: Playwright smoke — otevřít `/SDConnector?asUser=pavel.admin@pmtracker.local`, vyzkoušet načtení testovacího tiketu**
- [ ] **Step 7: Commit**

```bash
git commit -m "feat(sdconnector): diagnostická stránka — gov-chip stav + raw/user-friendly dvousloupec + simulovaný modal"
```

---

## Task 20: Full build + end-to-end test + git clean

- [ ] **Step 1: `dotnet build` 0 errors**
- [ ] **Step 2: `dotnet test PmTracker.Tests.Unit` — všechny passed**
- [ ] **Step 3: Spustit dev server + Playwright kompletní e2e:**
  - Otevřít editor záznamu → ověřit T5 harvest (přes logy)
  - Otevřít chat modal → timeline + stepper → drag & drop → autosave → zavřít → reload → ověřit persistence
  - Klik re-harvest → potvrdit → ověřit reset + nový harvest
  - Otevřít `/SDConnector` → zadat číslo tiketu → ověřit raw + user-friendly rendering
  - Otevřít admin nastavení `/Nastaveni/ServiceDeskSync` → změnit interval → ověřit, že Hangfire job respektuje
- [ ] **Step 4: Git status clean + log přehled**

---

## Hotovo — Plán C

Po dokončení máš plný e2e flow:
- ✅ Přesné HOT_* mapping + pid join + read-only kontext.
- ✅ Nová tabulka vazeb + entity + EF.
- ✅ Harvest service s LIKE predikáty + chronologie rebalance + Hangfire.
- ✅ AD login → jméno cache (nikdy do DB osoby).
- ✅ Chat modal UI s timeline + stepper + drag & drop + localStorage autosave + re-harvest.
- ✅ T5 trigger při otevření editoru záznamu.
- ✅ Hangfire periodický job respektuje admin sync settings.
- ✅ `/SDConnector` diagnostická stránka pro ověření integrace.

### Mimo scope (Plán D)
- Integrace s `CreateRecordProposalPayload` + `SchedulePlanProposalPayload` (schéma 2 a 3).
- Úprava záložky harmonogramu (ruční plán/skutečnost pro kroky 2/5/8/9).

### Nasazení do produkce
SQL skripty `db_upgrade_1_2_0`, `1_2_1`, `1_2_2` + kompletní konfigurace Hangfire + secret `TicketingReadOnly`. Dev nejdřív → manuální akceptace na testovacích tiketech → Prod.
