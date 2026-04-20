# ServiceDesk Výzvy — Fáze 1: Schéma, konektor a backend core

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zavést databázové schéma, izolovaný konektor do ServiceDesku (intranetNEW) a backend služby pro automatizované sestavování výzev z PNF externích vazeb — bez UI a bez Word exportu. UI (fáze 2) a Word (fáze 3) budou mít vlastní plány.

**Architecture:**
- **3-projektová izolace konektoru:** `PmTracker.ServiceDesk.Contracts` (čisté DTO + interface, nula závislostí na EF), `PmTracker.ServiceDesk.Sql` (read-only EF implementace pro `intranetNEW.HOT_*`), `PmTracker.Web` (konzumuje jen `Contracts`). Webový projekt **nikdy nevidí** HOT entity ani EF mapping — budoucí výměna ServiceDesku = nový projekt implementující `Contracts`, žádná změna ve Webu.
- **Feature flag** `Ticketing:Enabled` (TicketingOptions) + fail-safe `DisabledTicketingQueryService` místo tichého InMemory fallbacku. Špatná konfigurace = fail-fast, ne zamlčená chyba.
- **Request-scoped cache dekorátor** pro HOT lookupy (1 batch dotaz místo N+1 round-tripů).
- **Partial class split** `VyzvaService` do 5 souborů (Queries, Founding, Transitions, Assignment + core) — každý ~50–80 řádků, žádný „všemocný" soubor. Vzor z existující kódové báze (`RecordService.*.cs`).
- **Static pure helpers** pro číslování (`VyzvaCodeGenerator`), stavový automat (`VyzvaStateMachine`), query deduplikaci (`VyzvaQueries` IQueryable extensions). Testovatelné bez DI.
- **`sealed` všude**, `AsNoTracking` na všech read-only dotazech, covering index pro list výzev, batch HOT dotazy s `Contains`.

**Tech Stack:** .NET 8, EF Core (SqlServer), xUnit 2.9 + FluentAssertions 8, Microsoft.Extensions.Options, DocumentFormat.OpenXml (fáze 3, mimo scope).

**Konvence projektu:** Solution je `PmTracker.sln` (legacy `.sln`), projekty leží v root directory jako sibling složky (ne `src/`), používá se `Directory.Build.props` bez CPM. **Plán dodržuje existující konvence, nezavádí CPM ani `.slnx` migraci** — to by bylo cross-cutting rozhodnutí mimo scope této fáze.

**Spec:** [docs/superpowers/specs/2026-04-20-servicedesk-integrace-vyzvy-design.md](../specs/2026-04-20-servicedesk-integrace-vyzvy-design.md)

**Rozsah plánu:** sekce 3 (datový model), 4 (buffer), 5 (life-cycle), 8 (ServiceDesk konektor), 9 (audit). **Mimo scope:** sekce 6 (UI) → fáze 2, sekce 7 (Word) → fáze 3, produkční nasazení s Key Vault → fáze 4.

**UI poznámka do budoucna (fáze 2):** Cílová záložka Výzvy je navržena pro **širokoúhlé zobrazení** (split layout levý postranník + široký detail s tabulkou APTI kalkulací). Bere se v úvahu při návrhu DTO kontraktů v této fázi — `VyzvaDetail.Polozky` je plochý list bez paginace (jedna výzva = desítky PNF, ne stovky).

---

## Struktura souborů

### Nové projekty (2 assembly)

```
PmTracker.ServiceDesk.Contracts/            ← tenký projekt: jen typy a interface
  PmTracker.ServiceDesk.Contracts.csproj
  ITicketingQueryService.cs
  Contracts/
    HotZaznamDto.cs
    HotKalkulaceDto.cs
  Options/
    TicketingOptions.cs

PmTracker.ServiceDesk.Sql/                  ← read-only EF implementace
  PmTracker.ServiceDesk.Sql.csproj
  TicketingReadOnlyDbContext.cs
  Entities/
    HotZaznamEntity.cs
    HotKalkulaceEntity.cs
    HotPidEntity.cs
  SqlTicketingQueryService.cs
  DisabledTicketingQueryService.cs
  CachingTicketingQueryService.cs            ← request-scoped dekorátor
  ServiceDeskServiceCollectionExtensions.cs  ← AddServiceDeskIntegration()
```

### Změny v `PmTracker.Web/`

**Nové soubory:**
```
PmTracker.Web/Models/Entities/
  VyzvaEntity.cs                  ← (přesun + rozšíření z PmTrackerEntities.cs)
  VyzvaStav.cs                    ← enum
  VyzvaHistorieStavuEntity.cs

PmTracker.Web/Data/Configuration/
  VyzvaEntityConfiguration.cs

PmTracker.Web/Services/Vyzvy/
  IVyzvaService.cs                ← public kontrakt
  VyzvaErrors.cs
  VyzvaCodeGenerator.cs           ← static pure
  VyzvaStateMachine.cs            ← static pure
  VyzvaQueries.cs                 ← static IQueryable extensions (buffer filter)
  VyzvaService.cs                 ← partial: konstruktor + helpery
  VyzvaService.Queries.cs         ← partial: GetBuffer/GetVyzvy/GetVyzva
  VyzvaService.Founding.cs        ← partial: ZaloztVyzvuZBufferu
  VyzvaService.Transitions.cs     ← partial: ZmenitStav
  VyzvaService.Assignment.cs      ← partial: NastavitZaradid + PrerditPnf
  Contracts/
    VyzvaBufferItem.cs
    VyzvaDetail.cs
  VyzvyServiceCollectionExtensions.cs  ← AddVyzvyServices()

PmTracker.Web/Migrations/
  {timestamp}_VyzvyFaze1.cs       ← auto-generovaná

db_upgrade_1_1_8_vyzvy.sql        ← produkční upgrade skript
```

**Modifikované soubory:**
| Soubor | Změna |
|---|---|
| `PmTracker.sln` | Přidat 2 nové projekty |
| `PmTracker.Web/PmTracker.Web.csproj` | ProjectReference na `Contracts` + `Sql` |
| `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` | Odstranit `CiselnikVyzvaEntity`; rozšířit `ProjektEntity` (`MistoPlneni`, `CisloRamcoveSmlouvy`); rozšířit `ZaznamExterniOdkazEntity` (`ZaradidDoVyzvy` + rename `Vyzva` → `VyzvaId`) |
| `PmTracker.Web/Data/PmTrackerDbContext.cs` | DbSet rename `CiselnikVyzvy` → `Vyzvy` + přidat `VyzvaHistorieStavu` |
| `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs` | Smazat `ChallengeLookupEntityConfiguration` |
| `PmTracker.Web/Data/Configuration/ProjectEntityConfiguration.cs` | Mapping 2 nových polí |
| `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` | `VyzvaId` + FK + `ZaradidDoVyzvy` + filtered unique index |
| `PmTracker.Web/Services/ProjectService.LazyQueries.cs:301-307` | Typ `CiselnikVyzvaEntity` → `VyzvaEntity` |
| `PmTracker.Web/Services/Dictionaries/DictionaryService.Commands.cs:432` | Adaptace na nový typ (fallback admin číselník) |
| `PmTracker.Web/Program.cs` | `builder.Services.AddServiceDeskIntegration(builder.Configuration).AddVyzvyServices()` |
| `PmTracker.Web/appsettings.json` + `appsettings.Development.json` | Sekce `Ticketing` + `ConnectionStrings:TicketingReadOnly` |
| `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:99` | `ciselnik_vyzvy` → `vyzvy` + `vyzva_historie_stavu` |
| `docs/` baseline doc | Aktualizace schémat |

### Testovací soubory

```
PmTracker.Tests.Unit/
  Vyzvy/
    VyzvaCodeGeneratorTests.cs
    VyzvaStateMachineTests.cs
    VyzvaServiceTestHarness.cs
    VyzvaServiceQueryTests.cs
    VyzvaServiceFoundingTests.cs
    VyzvaServiceTransitionsTests.cs
    VyzvaServiceAssignmentTests.cs
    VyzvaServiceReassignmentTests.cs
  ServiceDesk/
    SqlTicketingQueryServiceTests.cs
    DisabledTicketingQueryServiceTests.cs
    CachingTicketingQueryServiceTests.cs
```

---

## Task 1: Vytvořit projekt PmTracker.ServiceDesk.Contracts

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/PmTracker.ServiceDesk.Contracts.csproj`
- Modify: `PmTracker.sln`

- [ ] **Step 1: Vytvořit csproj**

```xml
<!-- PmTracker.ServiceDesk.Contracts/PmTracker.ServiceDesk.Contracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.2" />
  </ItemGroup>
</Project>
```

**Proč jen `Microsoft.Extensions.Options`:** `TicketingOptions` potřebuje `[Required]`/`[Range]` atributy z DataAnnotations (součást BCL) a třída musí jít přes `IOptions<T>`. Žádná vazba na EF Core, ani na ASP.NET Core — tím zachováváme tenký kontraktový projekt.

- [ ] **Step 2: Přidat do solution**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet sln PmTracker.sln add PmTracker.ServiceDesk.Contracts/PmTracker.ServiceDesk.Contracts.csproj
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.ServiceDesk.Contracts/PmTracker.ServiceDesk.Contracts.csproj`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: Commit**

```bash
git add PmTracker.sln PmTracker.ServiceDesk.Contracts/
git commit -m "feat(servicedesk): nový projekt PmTracker.ServiceDesk.Contracts (kontrakty bez EF závislostí)"
```

---

## Task 2: Kontraktové DTO a TicketingOptions

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/Contracts/HotZaznamDto.cs`
- Create: `PmTracker.ServiceDesk.Contracts/Contracts/HotKalkulaceDto.cs`
- Create: `PmTracker.ServiceDesk.Contracts/Options/TicketingOptions.cs`

- [ ] **Step 1: HotZaznamDto**

```csharp
// PmTracker.ServiceDesk.Contracts/Contracts/HotZaznamDto.cs
namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotZaznamDto(
    string Id,
    string? TypZaznamu,
    string? Strucne,
    string? Popis);
```

- [ ] **Step 2: HotKalkulaceDto (readonly record struct je zbytečný — pole desítek polí)**

```csharp
// PmTracker.ServiceDesk.Contracts/Contracts/HotKalkulaceDto.cs
namespace PmTracker.ServiceDesk.Contracts;

public sealed record HotKalkulaceDto(
    long Id,
    string Pid,
    int? Verze,
    decimal? PracnostAnalyza, decimal? SazbaAnalyza, decimal? CenaAnalyza,
    decimal? PracnostProgramovani, decimal? SazbaProgramovani, decimal? CenaProgramovani,
    decimal? PracnostTestovani, decimal? SazbaTestovani, decimal? CenaTestovani,
    decimal? PracnostImplementace, decimal? SazbaImplementace, decimal? CenaImplementace,
    decimal? CenaCelkem,
    int? PocetLicenci, decimal? SazbaLicence, decimal? CenaLicence, string? RozpadLicence,
    DateTime? Termin, string? TextTermin);
```

*(Používám rozepsaná jména místo `PracnostA/P/T/I`, aby byly DTO čitelné bez přístupu k business slovníku ServiceDesku.)*

- [ ] **Step 3: TicketingOptions**

```csharp
// PmTracker.ServiceDesk.Contracts/Options/TicketingOptions.cs
using System.ComponentModel.DataAnnotations;

namespace PmTracker.ServiceDesk.Contracts;

public sealed class TicketingOptions
{
    public const string SectionName = "Ticketing";

    public bool Enabled { get; init; }

    [Required]
    public string ConnectionStringName { get; init; } = "TicketingReadOnly";

    public int CommandTimeoutSeconds { get; init; } = 30;
}
```

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.ServiceDesk.Contracts/PmTracker.ServiceDesk.Contracts.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add PmTracker.ServiceDesk.Contracts/
git commit -m "feat(servicedesk): DTO (HotZaznam/HotKalkulace) + TicketingOptions"
```

---

## Task 3: ITicketingQueryService interface

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/ITicketingQueryService.cs`

- [ ] **Step 1: Interface s batch metodami (proti N+1)**

```csharp
// PmTracker.ServiceDesk.Contracts/ITicketingQueryService.cs
namespace PmTracker.ServiceDesk.Contracts;

public interface ITicketingQueryService
{
    Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct);

    Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct);

    Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct);

    Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct);
}
```

**Proč batch + single:** Batch metoda eliminuje N+1 (jeden dotaz `WHERE id IN (...)` místo N dotazů). Single varianty jsou pohodlí pro scénáře, kdy vím ID jednoho ticketu.

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.ServiceDesk.Contracts/PmTracker.ServiceDesk.Contracts.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.ServiceDesk.Contracts/ITicketingQueryService.cs
git commit -m "feat(servicedesk): ITicketingQueryService s batch metodami (anti-N+1)"
```

---

## Task 4: Vytvořit projekt PmTracker.ServiceDesk.Sql

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
- Modify: `PmTracker.sln`

- [ ] **Step 1: csproj s referencí na Contracts + EF Core**

```xml
<!-- PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\PmTracker.ServiceDesk.Contracts\PmTracker.ServiceDesk.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**Verze `8.0.*`:** Ověř v Web projektu, jakou přesnou verzi EF Core používá, a použij stejnou. Run: `grep "EntityFrameworkCore" PmTracker.Web/PmTracker.Web.csproj`. Nahraď `8.0.*` přesnou verzí pro konzistenci.

- [ ] **Step 2: Přidat do solution**

```bash
dotnet sln PmTracker.sln add PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.sln PmTracker.ServiceDesk.Sql/
git commit -m "feat(servicedesk): nový projekt PmTracker.ServiceDesk.Sql (read-only EF implementace)"
```

---

## Task 5: HOT entity + TicketingReadOnlyDbContext

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs`
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotKalkulaceEntity.cs`
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotPidEntity.cs`
- Create: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`

- [ ] **Step 1: HotZaznamEntity (internal — nikdo mimo Sql projekt je nevidí)**

```csharp
// PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs
namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotZaznamEntity
{
    public long Radek { get; set; }
    public string Id { get; set; } = string.Empty;
    public string? TypZaznamu { get; set; }
    public string? Strucne { get; set; }
    public string? Popis { get; set; }
    public int? Stav { get; set; }
    public int? Splneno { get; set; }
    public DateTime? SlaDeadline { get; set; }
}
```

*`internal` je klíč:* entity existují pouze uvnitř `Sql` projektu. Nikdo mimo ně nemůže natáhnout přímo HOT schéma — musí projít přes `ITicketingQueryService` a DTO.

- [ ] **Step 2: HotKalkulaceEntity**

```csharp
// PmTracker.ServiceDesk.Sql/Entities/HotKalkulaceEntity.cs
namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotKalkulaceEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }
    public int? IdKalk { get; set; }
    public int? Verze { get; set; }
    public string? Akceptace { get; set; }
    public DateTime? Datum { get; set; }
    public DateTime? Termin { get; set; }
    public decimal? PracnostA { get; set; }
    public decimal? PracnostP { get; set; }
    public decimal? PracnostT { get; set; }
    public decimal? PracnostI { get; set; }
    public decimal? SazbaA { get; set; }
    public decimal? SazbaP { get; set; }
    public decimal? SazbaT { get; set; }
    public decimal? SazbaI { get; set; }
    public decimal? CenaA { get; set; }
    public decimal? CenaP { get; set; }
    public decimal? CenaT { get; set; }
    public decimal? CenaI { get; set; }
    public decimal? Cena { get; set; }
    public decimal? SazbaL { get; set; }
    public int? PocetL { get; set; }
    public decimal? CenaL { get; set; }
    public string? RozpadLicence { get; set; }
    public string? Popis { get; set; }
    public string? VyjadreniKalk { get; set; }
    public string? TextTermin { get; set; }
}
```

- [ ] **Step 3: HotPidEntity**

```csharp
// PmTracker.ServiceDesk.Sql/Entities/HotPidEntity.cs
namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotPidEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }
    public int? IdxPou { get; set; }
}
```

- [ ] **Step 4: TicketingReadOnlyDbContext**

```csharp
// PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

internal sealed class TicketingReadOnlyDbContext : DbContext
{
    public TicketingReadOnlyDbContext(DbContextOptions<TicketingReadOnlyDbContext> opts) : base(opts)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    public DbSet<HotZaznamEntity> HotZaznamy => Set<HotZaznamEntity>();
    public DbSet<HotKalkulaceEntity> HotKalkulace => Set<HotKalkulaceEntity>();
    public DbSet<HotPidEntity> HotPid => Set<HotPidEntity>();

    public override int SaveChanges()
        => throw new InvalidOperationException("TicketingReadOnlyDbContext is strictly read-only.");

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        => throw new InvalidOperationException("TicketingReadOnlyDbContext is strictly read-only.");

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<HotZaznamEntity>(e =>
        {
            e.ToTable("HOT_ZAZNAMY", "dbo");
            e.HasKey(x => x.Radek);
            e.Property(x => x.Radek).HasColumnName("radek");
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TypZaznamu).HasColumnName("typ_zaznamu");
            e.Property(x => x.Strucne).HasColumnName("strucne");
            e.Property(x => x.Popis).HasColumnName("popis");
            e.Property(x => x.Stav).HasColumnName("stav");
            e.Property(x => x.Splneno).HasColumnName("splneno");
            e.Property(x => x.SlaDeadline).HasColumnName("sla_deadline");
        });

        mb.Entity<HotKalkulaceEntity>(e =>
        {
            e.ToTable("HOT_KALKULACE", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Pid).HasColumnName("pid");
            e.Property(x => x.IdKalk).HasColumnName("id_kalk");
            e.Property(x => x.Verze).HasColumnName("verze");
            e.Property(x => x.Akceptace).HasColumnName("akceptace");
            e.Property(x => x.Datum).HasColumnName("datum");
            e.Property(x => x.Termin).HasColumnName("termin");
            e.Property(x => x.PracnostA).HasColumnName("pracnost_a");
            e.Property(x => x.PracnostP).HasColumnName("pracnost_p");
            e.Property(x => x.PracnostT).HasColumnName("pracnost_t");
            e.Property(x => x.PracnostI).HasColumnName("pracnost_i");
            e.Property(x => x.SazbaA).HasColumnName("sazba_a");
            e.Property(x => x.SazbaP).HasColumnName("sazba_p");
            e.Property(x => x.SazbaT).HasColumnName("sazba_t");
            e.Property(x => x.SazbaI).HasColumnName("sazba_i");
            e.Property(x => x.CenaA).HasColumnName("cena_a");
            e.Property(x => x.CenaP).HasColumnName("cena_p");
            e.Property(x => x.CenaT).HasColumnName("cena_t");
            e.Property(x => x.CenaI).HasColumnName("cena_i");
            e.Property(x => x.Cena).HasColumnName("cena");
            e.Property(x => x.SazbaL).HasColumnName("sazba_l");
            e.Property(x => x.PocetL).HasColumnName("pocet_l");
            e.Property(x => x.CenaL).HasColumnName("cena_l");
            e.Property(x => x.RozpadLicence).HasColumnName("rozpad_licence");
            e.Property(x => x.Popis).HasColumnName("popis");
            e.Property(x => x.VyjadreniKalk).HasColumnName("vyjadreni_kalk");
            e.Property(x => x.TextTermin).HasColumnName("text_termin");

            e.HasIndex(x => new { x.Pid, x.Akceptace }).HasDatabaseName("ix_hot_kalkulace_pid_akceptace");
        });

        mb.Entity<HotPidEntity>(e =>
        {
            e.ToTable("HOT_PID", "dbo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Pid).HasColumnName("PID");
            e.Property(x => x.IdxPou).HasColumnName("idx_pou");
        });
    }
}
```

**Poznámka k indexu:** `HasIndex` v konfiguraci EF slouží jen pro čtecí dotazy (EF to nebude vytvářet — `intranetNEW` DB není pod naší správou). Zdokumentováno pro budoucí DB admina, aby ověřil, že tenhle index existuje (klíčový pro rychlost `GetAkceptovaneKalkulace`).

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/
git commit -m "feat(servicedesk): TicketingReadOnlyDbContext + internal HOT entity"
```

---

## Task 6: SqlTicketingQueryService (implementace přes EF)

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs`

- [ ] **Step 1: Implementace s batch dotazy**

```csharp
// PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

internal sealed class SqlTicketingQueryService : ITicketingQueryService
{
    private const string AkceptovanoStav = "Akceptováno";
    private readonly TicketingReadOnlyDbContext _db;

    public SqlTicketingQueryService(TicketingReadOnlyDbContext db)
    {
        _db = db;
    }

    public async Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        var z = await _db.HotZaznamy.FirstOrDefaultAsync(r => r.Id == cislo, ct);
        return z == null ? null : MapZaznam(z);
    }

    public async Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotZaznamDto>();

        var cislaArr = cisla.Distinct().ToArray();
        var raw = await _db.HotZaznamy
            .Where(r => cislaArr.Contains(r.Id))
            .ToListAsync(ct);
        return raw.ToDictionary(r => r.Id, MapZaznam);
    }

    public async Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        var k = await _db.HotKalkulace
            .Where(x => x.Pid == cislo && x.Akceptace == AkceptovanoStav)
            .OrderByDescending(x => x.Verze)
            .FirstOrDefaultAsync(ct);
        return k == null ? null : MapKalkulace(k);
    }

    public async Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotKalkulaceDto>();

        var cislaArr = cisla.Distinct().ToArray();
        var raw = await _db.HotKalkulace
            .Where(x => x.Pid != null && cislaArr.Contains(x.Pid) && x.Akceptace == AkceptovanoStav)
            .ToListAsync(ct);

        return raw
            .GroupBy(x => x.Pid!)
            .ToDictionary(g => g.Key, g => MapKalkulace(g.OrderByDescending(k => k.Verze).First()));
    }

    private static HotZaznamDto MapZaznam(HotZaznamEntity e)
        => new(e.Id, e.TypZaznamu, e.Strucne, e.Popis);

    private static HotKalkulaceDto MapKalkulace(HotKalkulaceEntity k)
        => new(
            k.Id, k.Pid ?? string.Empty, k.Verze,
            k.PracnostA, k.SazbaA, k.CenaA,
            k.PracnostP, k.SazbaP, k.CenaP,
            k.PracnostT, k.SazbaT, k.CenaT,
            k.PracnostI, k.SazbaI, k.CenaI,
            k.Cena,
            k.PocetL, k.SazbaL, k.CenaL, k.RozpadLicence,
            k.Termin, k.TextTermin);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs
git commit -m "feat(servicedesk): SqlTicketingQueryService (batch + single + akceptace filter)"
```

---

## Task 7: DisabledTicketingQueryService (fail-safe)

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/DisabledTicketingQueryService.cs`

- [ ] **Step 1: Implementace s logováním warningu**

```csharp
// PmTracker.ServiceDesk.Sql/DisabledTicketingQueryService.cs
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

internal sealed class DisabledTicketingQueryService : ITicketingQueryService
{
    private static readonly IReadOnlyDictionary<string, HotZaznamDto> EmptyZaznamy
        = new Dictionary<string, HotZaznamDto>();
    private static readonly IReadOnlyDictionary<string, HotKalkulaceDto> EmptyKalkulace
        = new Dictionary<string, HotKalkulaceDto>();

    private readonly ILogger<DisabledTicketingQueryService> _logger;

    public DisabledTicketingQueryService(ILogger<DisabledTicketingQueryService> logger)
    {
        _logger = logger;
    }

    public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        LogDisabled(nameof(GetZaznamAsync));
        return Task.FromResult<HotZaznamDto?>(null);
    }

    public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        LogDisabled(nameof(GetZaznamyAsync));
        return Task.FromResult(EmptyZaznamy);
    }

    public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        LogDisabled(nameof(GetAkceptovanouKalkulaciAsync));
        return Task.FromResult<HotKalkulaceDto?>(null);
    }

    public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        LogDisabled(nameof(GetAkceptovaneKalkulaceAsync));
        return Task.FromResult(EmptyKalkulace);
    }

    private void LogDisabled(string operation)
    {
        _logger.LogWarning(
            "ServiceDesk integrace je vypnutá (Ticketing:Enabled=false), operace {Operation} vrací prázdný výsledek.",
            operation);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/DisabledTicketingQueryService.cs
git commit -m "feat(servicedesk): DisabledTicketingQueryService (fail-safe při Ticketing:Enabled=false)"
```

---

## Task 8: CachingTicketingQueryService (request-scoped dekorátor)

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/CachingTicketingQueryService.cs`

- [ ] **Step 1: Dekorátor**

```csharp
// PmTracker.ServiceDesk.Sql/CachingTicketingQueryService.cs
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Request-scoped cache pro HOT lookupy. V rámci jednoho requestu (jednoho scope)
/// ručí, že se stejný ticket / kalkulace načte nejvýše jednou z DB. Scope = 1 HTTP request.
/// </summary>
internal sealed class CachingTicketingQueryService : ITicketingQueryService
{
    private readonly ITicketingQueryService _inner;
    private readonly Dictionary<string, HotZaznamDto?> _zaznamyCache = new();
    private readonly Dictionary<string, HotKalkulaceDto?> _kalkulaceCache = new();

    public CachingTicketingQueryService(ITicketingQueryService inner)
    {
        _inner = inner;
    }

    public async Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        if (_zaznamyCache.TryGetValue(cislo, out var cached)) return cached;
        var result = await _inner.GetZaznamAsync(cislo, ct);
        _zaznamyCache[cislo] = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        var missing = cisla.Where(c => !_zaznamyCache.ContainsKey(c)).ToArray();
        if (missing.Length > 0)
        {
            var fetched = await _inner.GetZaznamyAsync(missing, ct);
            foreach (var c in missing)
                _zaznamyCache[c] = fetched.GetValueOrDefault(c);
        }

        var result = new Dictionary<string, HotZaznamDto>(cisla.Count);
        foreach (var c in cisla)
        {
            if (_zaznamyCache.TryGetValue(c, out var dto) && dto != null)
                result[c] = dto;
        }
        return result;
    }

    public async Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        if (_kalkulaceCache.TryGetValue(cislo, out var cached)) return cached;
        var result = await _inner.GetAkceptovanouKalkulaciAsync(cislo, ct);
        _kalkulaceCache[cislo] = result;
        return result;
    }

    public async Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        var missing = cisla.Where(c => !_kalkulaceCache.ContainsKey(c)).ToArray();
        if (missing.Length > 0)
        {
            var fetched = await _inner.GetAkceptovaneKalkulaceAsync(missing, ct);
            foreach (var c in missing)
                _kalkulaceCache[c] = fetched.GetValueOrDefault(c);
        }

        var result = new Dictionary<string, HotKalkulaceDto>(cisla.Count);
        foreach (var c in cisla)
        {
            if (_kalkulaceCache.TryGetValue(c, out var dto) && dto != null)
                result[c] = dto;
        }
        return result;
    }
}
```

**Proč ručně, ne `IMemoryCache`:** `IMemoryCache` by byl nadbytečný — je navržený pro dlouhodobý cache s expiry. My chceme jen request-scoped deduplikaci (Scoped lifetime = new instance per request = new Dictionary per request). Zdarma, bez deps.

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/CachingTicketingQueryService.cs
git commit -m "feat(servicedesk): CachingTicketingQueryService (request-scoped deduplikace)"
```

---

## Task 9: ServiceDeskServiceCollectionExtensions (DI registrace)

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs`

- [ ] **Step 1: AddServiceDeskIntegration extension**

```csharp
// PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

public static class ServiceDeskServiceCollectionExtensions
{
    public static IServiceCollection AddServiceDeskIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TicketingOptions>()
            .Bind(configuration.GetSection(TicketingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = configuration.GetSection(TicketingOptions.SectionName).Get<TicketingOptions>()
            ?? new TicketingOptions();

        if (!options.Enabled)
        {
            services.AddScoped<ITicketingQueryService, DisabledTicketingQueryService>();
            return services;
        }

        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"ServiceDesk integrace je zapnutá (Ticketing:Enabled=true), ale ConnectionString '{options.ConnectionStringName}' je prázdný. " +
                $"Buď nastav connection string, nebo vypni Ticketing:Enabled.");
        }

        services.AddDbContext<TicketingReadOnlyDbContext>(opts =>
            opts.UseSqlServer(connectionString, sql =>
                sql.CommandTimeout(options.CommandTimeoutSeconds)));

        // Dekorátor: CachingTicketingQueryService obaluje SqlTicketingQueryService.
        // Registrace přes Scoped = 1 cache instance per request.
        services.AddScoped<SqlTicketingQueryService>();
        services.AddScoped<ITicketingQueryService>(sp =>
            new CachingTicketingQueryService(sp.GetRequiredService<SqlTicketingQueryService>()));

        return services;
    }
}
```

**Proč fail-fast při špatné konfiguraci:** Pokud admin zapne `Ticketing:Enabled=true` ale nepřidá connection string, aplikace **nestartuje** (InvalidOperationException v `Program.cs`). Lepší než tiché selhání za běhu, kdy uživatel uvidí prázdné výzvy a nevědel by, že ServiceDesk není nakonfigurovaný.

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs
git commit -m "feat(servicedesk): AddServiceDeskIntegration DI + fail-fast bez connection stringu"
```

---

## Task 10: Testy pro ServiceDesk konektor

**Files:**
- Create: `PmTracker.Tests.Unit/ServiceDesk/SqlTicketingQueryServiceTests.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/DisabledTicketingQueryServiceTests.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/CachingTicketingQueryServiceTests.cs`
- Modify: `PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` (přidat reference)

- [ ] **Step 1: Přidat ProjectReference v Tests.Unit.csproj**

V `PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` do bloku `<ItemGroup>` s `<ProjectReference>` přidej:

```xml
    <ProjectReference Include="..\PmTracker.ServiceDesk.Contracts\PmTracker.ServiceDesk.Contracts.csproj" />
    <ProjectReference Include="..\PmTracker.ServiceDesk.Sql\PmTracker.ServiceDesk.Sql.csproj" />
```

Do `<ItemGroup>` s `<PackageReference>` přidej:

```xml
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.*" />
```

*(Ověř verzi proti Web projektu; použij stejnou.)*

**Aby testy viděly `internal` třídy Sql projektu**, přidej do `PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="PmTracker.Tests.Unit" />
  </ItemGroup>
```

- [ ] **Step 2: Test pro SqlTicketingQueryService**

```csharp
// PmTracker.Tests.Unit/ServiceDesk/SqlTicketingQueryServiceTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SqlTicketingQueryServiceTests
{
    private static TicketingReadOnlyDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_FiltrujeAkceptovano_VraciNejvyssiVerzi()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "336865", Verze = 1, Akceptace = "Nabídka", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "336865", Verze = 2, Akceptace = "Akceptováno", Cena = 200 },
            new HotKalkulaceEntity { Id = 3, Pid = "336865", Verze = 3, Akceptace = "Akceptováno", Cena = 300 });
        db.SaveChanges();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(3);
        result.CenaCelkem.Should().Be(300);
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_ZadnaAkceptovana_VraciNull()
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity { Id = 10, Pid = "111111", Verze = 1, Akceptace = "Nabídka", Cena = 100 });
        db.SaveChanges();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("111111", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAkceptovaneKalkulace_Batch_VraciPerPid()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "A", Verze = 1, Akceptace = "Akceptováno", Cena = 10 },
            new HotKalkulaceEntity { Id = 2, Pid = "B", Verze = 1, Akceptace = "Akceptováno", Cena = 20 },
            new HotKalkulaceEntity { Id = 3, Pid = "C", Verze = 1, Akceptace = "Nabídka", Cena = 30 });
        db.SaveChanges();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovaneKalkulaceAsync(new[] { "A", "B", "C" }, CancellationToken.None);

        result.Should().HaveCount(2);
        result["A"].CenaCelkem.Should().Be(10);
        result["B"].CenaCelkem.Should().Be(20);
        result.ContainsKey("C").Should().BeFalse();
    }

    [Fact]
    public async Task GetZaznamy_Batch_VraciExistujiciIgnorujeNeznama()
    {
        using var db = CreateDb();
        db.HotZaznamy.AddRange(
            new HotZaznamEntity { Radek = 1, Id = "A", Strucne = "foo" },
            new HotZaznamEntity { Radek = 2, Id = "B", Strucne = "bar" });
        db.SaveChanges();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetZaznamyAsync(new[] { "A", "B", "C" }, CancellationToken.None);

        result.Should().HaveCount(2);
        result["A"].Strucne.Should().Be("foo");
        result.ContainsKey("C").Should().BeFalse();
    }

    [Fact]
    public void SaveChanges_Throws_ProtiZapisu()
    {
        using var db = CreateDb();
        var act = () => db.SaveChanges();
        act.Should().Throw<InvalidOperationException>().WithMessage("*read-only*");
    }
}
```

- [ ] **Step 3: Test pro DisabledTicketingQueryService**

```csharp
// PmTracker.Tests.Unit/ServiceDesk/DisabledTicketingQueryServiceTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class DisabledTicketingQueryServiceTests
{
    private static DisabledTicketingQueryService CreateService()
        => new(NullLogger<DisabledTicketingQueryService>.Instance);

    [Fact]
    public async Task VsechnyMetody_VraciPrazdny()
    {
        var svc = CreateService();

        (await svc.GetZaznamAsync("A", default)).Should().BeNull();
        (await svc.GetZaznamyAsync(new[] { "A", "B" }, default)).Should().BeEmpty();
        (await svc.GetAkceptovanouKalkulaciAsync("A", default)).Should().BeNull();
        (await svc.GetAkceptovaneKalkulaceAsync(new[] { "A", "B" }, default)).Should().BeEmpty();
    }
}
```

- [ ] **Step 4: Test pro CachingTicketingQueryService**

```csharp
// PmTracker.Tests.Unit/ServiceDesk/CachingTicketingQueryServiceTests.cs
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class CachingTicketingQueryServiceTests
{
    private sealed class CountingInner : ITicketingQueryService
    {
        public int GetZaznamCalls { get; private set; }
        public int GetZaznamyBatchCalls { get; private set; }
        public int GetAkceptovanouCalls { get; private set; }
        public int GetAkceptovaneBatchCalls { get; private set; }

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
        {
            GetZaznamCalls++;
            return Task.FromResult<HotZaznamDto?>(new HotZaznamDto(cislo, "PNF", "nazev", "popis"));
        }

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
        {
            GetZaznamyBatchCalls++;
            IReadOnlyDictionary<string, HotZaznamDto> dict =
                cisla.ToDictionary(c => c, c => new HotZaznamDto(c, "PNF", "n", "p"));
            return Task.FromResult(dict);
        }

        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
        {
            GetAkceptovanouCalls++;
            return Task.FromResult<HotKalkulaceDto?>(null);
        }

        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
        {
            GetAkceptovaneBatchCalls++;
            return Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(
                new Dictionary<string, HotKalkulaceDto>());
        }
    }

    [Fact]
    public async Task Single_DruheVolani_NespoustiInner()
    {
        var inner = new CountingInner();
        var cache = new CachingTicketingQueryService(inner);

        await cache.GetZaznamAsync("A", default);
        await cache.GetZaznamAsync("A", default);

        inner.GetZaznamCalls.Should().Be(1);
    }

    [Fact]
    public async Task Batch_DruheVolaniStejnychId_NespoustiInner()
    {
        var inner = new CountingInner();
        var cache = new CachingTicketingQueryService(inner);

        await cache.GetZaznamyAsync(new[] { "A", "B" }, default);
        await cache.GetZaznamyAsync(new[] { "A", "B" }, default);

        inner.GetZaznamyBatchCalls.Should().Be(1);
    }

    [Fact]
    public async Task Batch_ProlozeneIds_PoslePouzeChybejici()
    {
        var inner = new CountingInner();
        var cache = new CachingTicketingQueryService(inner);

        await cache.GetZaznamyAsync(new[] { "A" }, default);
        await cache.GetZaznamyAsync(new[] { "A", "B" }, default);

        inner.GetZaznamyBatchCalls.Should().Be(2); // druhý batch volán jen s "B"
    }
}
```

- [ ] **Step 5: Run testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ServiceDesk" --no-restore`
Expected: 10 passed

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Tests.Unit/ServiceDesk/ PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj PmTracker.ServiceDesk.Sql/PmTracker.ServiceDesk.Sql.csproj
git commit -m "test(servicedesk): Sql + Disabled + Caching query services"
```

---

## Task 11: Přidat ServiceDesk reference do PmTracker.Web

**Files:**
- Modify: `PmTracker.Web/PmTracker.Web.csproj`
- Modify: `PmTracker.Web/appsettings.json`
- Modify: `PmTracker.Web/appsettings.Development.json` (pokud existuje)

- [ ] **Step 1: ProjectReference ve Web csproj**

Do `<ItemGroup>` s `<ProjectReference>` v `PmTracker.Web/PmTracker.Web.csproj` přidej:

```xml
    <ProjectReference Include="..\PmTracker.ServiceDesk.Contracts\PmTracker.ServiceDesk.Contracts.csproj" />
    <ProjectReference Include="..\PmTracker.ServiceDesk.Sql\PmTracker.ServiceDesk.Sql.csproj" />
```

**Poznámka:** Web projekt konzumuje `Sql` jen pro `AddServiceDeskIntegration` extension v `Program.cs`. Všechen ostatní kód používá pouze `Contracts`. Teoreticky bychom mohli ještě víc izolovat (Web referuje jen `Contracts`, `Sql` referuje jen projekt hostitele), ale za cenu magie. YAGNI.

- [ ] **Step 2: appsettings.json — sekce Ticketing + prázdný connection string**

Do root `PmTracker.Web/appsettings.json` přidej (např. za existující `ConnectionStrings`):

```json
  "Ticketing": {
    "Enabled": false,
    "ConnectionStringName": "TicketingReadOnly",
    "CommandTimeoutSeconds": 30
  }
```

A do sekce `ConnectionStrings`:

```json
    "TicketingReadOnly": ""
```

- [ ] **Step 3: appsettings.Development.json**

Pokud existuje, zrcadlově doplnit (necháme `Enabled: false` a prázdný connection string — dev defaultně bez ServiceDesku).

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/PmTracker.Web.csproj PmTracker.Web/appsettings*.json
git commit -m "feat(servicedesk): Web referuje Contracts+Sql, appsettings má sekci Ticketing (Enabled=false default)"
```

---

## Task 12: Zaregistrovat ServiceDesk v Program.cs

**Files:**
- Modify: `PmTracker.Web/Program.cs`

- [ ] **Step 1: Najít místo pro registraci services**

Run: `grep -n "AddScoped\|AddDbContext" PmTracker.Web/Program.cs | head -10`

Přidej nad místo, kde končí registrace služeb (před `builder.Build()`):

```csharp
builder.Services.AddServiceDeskIntegration(builder.Configuration);
```

Nahoře `Program.cs` přidej using:

```csharp
using PmTracker.ServiceDesk.Sql;
```

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Ověř, že app startuje (s Ticketing:Enabled=false)**

Run: `dotnet run --project PmTracker.Web/PmTracker.Web.csproj --no-build --urls http://localhost:5999 2>&1 &  sleep 4; kill %1 2>/dev/null; wait 2>/dev/null`

*(Alternativně: spusť to v IDE, počkej, že nastartuje, a zavři.)*

Expected: aplikace nastartuje bez výjimky, žádné error logy kolem ServiceDesku.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Program.cs
git commit -m "feat(servicedesk): Program.cs volá AddServiceDeskIntegration"
```

---

## Task 13: Založit enum VyzvaStav

**Files:**
- Create: `PmTracker.Web/Models/Entities/VyzvaStav.cs`

- [ ] **Step 1: Enum**

```csharp
// PmTracker.Web/Models/Entities/VyzvaStav.cs
namespace PmTracker.Web.Models.Entities;

public enum VyzvaStav : byte
{
    Priprava = 1,
    Odeslano = 2,
    Zruseno = 3,
}
```

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Models/Entities/VyzvaStav.cs
git commit -m "feat(vyzvy): VyzvaStav enum (Priprava/Odeslano/Zruseno)"
```

---

## Task 14: VyzvaEntity + VyzvaHistorieStavuEntity

**Files:**
- Create: `PmTracker.Web/Models/Entities/VyzvaEntity.cs`
- Create: `PmTracker.Web/Models/Entities/VyzvaHistorieStavuEntity.cs`

- [ ] **Step 1: VyzvaEntity**

```csharp
// PmTracker.Web/Models/Entities/VyzvaEntity.cs
namespace PmTracker.Web.Models.Entities;

public sealed class VyzvaEntity
{
    public int Id { get; set; }
    public int ProjektId { get; set; }
    public string Kod { get; set; } = string.Empty;
    public int PoradoveVRoce { get; set; }
    public int Rok { get; set; }
    public VyzvaStav Stav { get; set; }
    public DateTime DatumZalozeni { get; set; }
    public int ZalozilOsobaId { get; set; }
    public DateTime? DatumOdeslani { get; set; }
    public int? OdeslalOsobaId { get; set; }
    public string MistoPlneniSnapshot { get; set; } = string.Empty;
    public string CisloRamcoveSmlouvySnapshot { get; set; } = string.Empty;
}
```

- [ ] **Step 2: VyzvaHistorieStavuEntity**

```csharp
// PmTracker.Web/Models/Entities/VyzvaHistorieStavuEntity.cs
namespace PmTracker.Web.Models.Entities;

public sealed class VyzvaHistorieStavuEntity
{
    public int Id { get; set; }
    public int VyzvaId { get; set; }
    public VyzvaStav? PuvodniStav { get; set; }
    public VyzvaStav NovyStav { get; set; }
    public DateTime DatumZmeny { get; set; }
    public int ZmenilOsobaId { get; set; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/Entities/VyzvaEntity.cs PmTracker.Web/Models/Entities/VyzvaHistorieStavuEntity.cs
git commit -m "feat(vyzvy): VyzvaEntity + VyzvaHistorieStavuEntity"
```

---

## Task 15: Odstranit CiselnikVyzvaEntity a opravit kompilaci

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:85-92`
- Modify: `PmTracker.Web/Services/ProjectService.LazyQueries.cs:301-307`
- Modify: `PmTracker.Web/Services/Dictionaries/DictionaryService.Commands.cs:~432`

- [ ] **Step 1: Smazat CiselnikVyzvaEntity**

V `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` najdi:

```csharp
public sealed class CiselnikVyzvaEntity
{
    public int Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Nazev { get; set; } = string.Empty;
    public DateTime Rok { get; set; }
    public bool IsLocked { get; set; }
}
```

Celé smaž.

- [ ] **Step 2: Opravit ProjectService.LazyQueries.cs**

V `PmTracker.Web/Services/ProjectService.LazyQueries.cs` na ~řádcích 301-307 najdi:

```csharp
var vyzvaById = vyzvaIds.Length == 0
    ? new Dictionary<int, CiselnikVyzvaEntity>()
    : await dbContext.CiselnikVyzvy.AsNoTracking()
        .Where(x => vyzvaIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, ct);
```

Nahraď:

```csharp
var vyzvaById = vyzvaIds.Length == 0
    ? new Dictionary<int, VyzvaEntity>()
    : await dbContext.Vyzvy.AsNoTracking()
        .Where(x => vyzvaIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, ct);
```

- [ ] **Step 3: Opravit další reference buildem**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | grep -E "CS[0-9]+.*CiselnikVyzva|CS[0-9]+.*\.Vyzva[^I]" | head -20`

Pro každé selhání:
- `CiselnikVyzvaEntity` → `VyzvaEntity`
- `CiselnikVyzvy` (DbSet) → `Vyzvy`
- Odkazy na `Nazev` → dočasně `$"Výzva {x.Kod}"` (pole zrušeno ze specu)
- Odkazy na `IsLocked` → dočasně `x.Stav == VyzvaStav.Odeslano`
- V `DictionaryService.Commands.cs` rozšířit zakládání o povinná nová pole (dočasně prázdné stringy / defaulty — plné doplnění v UI fázi)

Opakuj `dotnet build` dokud build nepojede.

- [ ] **Step 4: Commit**

```bash
git add -A PmTracker.Web/Models/Entities/PmTrackerEntities.cs PmTracker.Web/Services/
git commit -m "refactor(vyzvy): odstranit CiselnikVyzvaEntity, nahradit VyzvaEntity v services"
```

---

## Task 16: Rozšířit ProjektEntity + mapping

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (ProjektEntity)
- Modify: `PmTracker.Web/Data/Configuration/ProjectEntityConfiguration.cs`

- [ ] **Step 1: Doplnit pole do ProjektEntity**

V `PmTrackerEntities.cs` najdi `public sealed class ProjektEntity` a přidej na konec:

```csharp
    public string? MistoPlneni { get; set; }
    public string? CisloRamcoveSmlouvy { get; set; }
```

- [ ] **Step 2: Mapping v ProjectEntityConfiguration**

V `ProjectEntityConfiguration.cs` v konfiguraci `ProjektEntity` (tabulka `projekty`) přidej za existující `builder.Property(...)`:

```csharp
        builder.Property(x => x.MistoPlneni).HasColumnName("misto_plneni").HasMaxLength(500);
        builder.Property(x => x.CisloRamcoveSmlouvy).HasColumnName("cislo_ramcove_smlouvy").HasMaxLength(100);
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/Entities/PmTrackerEntities.cs PmTracker.Web/Data/Configuration/ProjectEntityConfiguration.cs
git commit -m "feat(projekt): MistoPlneni + CisloRamcoveSmlouvy"
```

---

## Task 17: Rozšířit ZaznamExterniOdkazEntity (rename Vyzva → VyzvaId + ZaradidDoVyzvy)

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (ZaznamExterniOdkazEntity)
- Modify: `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs`

- [ ] **Step 1: Rename + přidat property**

V `PmTrackerEntities.cs` najdi `ZaznamExterniOdkazEntity` a nahraď poslední pole + přidej:

Před:
```csharp
    public int? Vyzva { get; set; }
```

Po:
```csharp
    public int? VyzvaId { get; set; }
    public bool ZaradidDoVyzvy { get; set; }
```

- [ ] **Step 2: Opravit reference na `.Vyzva` → `.VyzvaId`**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | grep "error CS" | head -30`

Nahraď `.Vyzva` → `.VyzvaId` v entity přístupu (ne ve ViewModelech).

- [ ] **Step 3: Mapping v RecordEntityConfiguration**

V `RecordEntityConfiguration.cs` v konfiguraci `ZaznamExterniOdkazEntity` nahraď mapping `Vyzva` a doplň:

```csharp
        builder.Property(x => x.VyzvaId).HasColumnName("vyzva_id");
        builder.Property(x => x.ZaradidDoVyzvy).HasColumnName("zaradid_do_vyzvy").HasDefaultValue(false);

        builder.HasOne<VyzvaEntity>()
            .WithMany()
            .HasForeignKey(x => x.VyzvaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Cislo)
            .HasDatabaseName("ux_zaznam_externi_odkazy_cislo_in_vyzve")
            .HasFilter("[vyzva_id] IS NOT NULL")
            .IsUnique();
```

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add -A PmTracker.Web/Models/Entities/PmTrackerEntities.cs PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs PmTracker.Web/Services/
git commit -m "feat(vyzvy): ZaznamExterniOdkaz rename Vyzva→VyzvaId + ZaradidDoVyzvy switch + filtered unique index"
```

---

## Task 18: VyzvaEntityConfiguration + odstranit ChallengeLookupEntityConfiguration

**Files:**
- Create: `PmTracker.Web/Data/Configuration/VyzvaEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs`

- [ ] **Step 1: Smazat ChallengeLookupEntityConfiguration**

V `LookupEntityConfiguration.cs` najdi třídu `ChallengeLookupEntityConfiguration` (kolem řádku 144) a celou smaž (od `internal sealed class ChallengeLookupEntityConfiguration` po uzavírající `}`).

- [ ] **Step 2: Nová konfigurace + covering index**

```csharp
// PmTracker.Web/Data/Configuration/VyzvaEntityConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class VyzvaEntityConfiguration : IEntityTypeConfiguration<VyzvaEntity>
{
    public void Configure(EntityTypeBuilder<VyzvaEntity> builder)
    {
        builder.ToTable("vyzvy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ProjektId).HasColumnName("projekt_id");
        builder.Property(x => x.Kod).HasColumnName("kod").HasMaxLength(20).IsRequired();
        builder.Property(x => x.PoradoveVRoce).HasColumnName("poradove_v_roce");
        builder.Property(x => x.Rok).HasColumnName("rok");
        builder.Property(x => x.Stav).HasColumnName("stav").HasConversion<byte>();
        builder.Property(x => x.DatumZalozeni).HasColumnName("datum_zalozeni");
        builder.Property(x => x.ZalozilOsobaId).HasColumnName("zalozil_osoba_id");
        builder.Property(x => x.DatumOdeslani).HasColumnName("datum_odeslani");
        builder.Property(x => x.OdeslalOsobaId).HasColumnName("odeslal_osoba_id");
        builder.Property(x => x.MistoPlneniSnapshot).HasColumnName("misto_plneni_snapshot").HasMaxLength(500).IsRequired();
        builder.Property(x => x.CisloRamcoveSmlouvySnapshot).HasColumnName("cislo_ramcove_smlouvy_snapshot").HasMaxLength(100).IsRequired();

        builder.HasOne<ProjektEntity>()
            .WithMany()
            .HasForeignKey(x => x.ProjektId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unikátnost pořadového čísla v rámci (smlouva, rok)
        builder.HasIndex(x => new { x.CisloRamcoveSmlouvySnapshot, x.Rok, x.PoradoveVRoce })
            .IsUnique()
            .HasDatabaseName("ux_vyzvy_smlouva_rok_poradove");

        // Covering index pro list výzev projektu (hlavní use-case v UI záložce)
        builder.HasIndex(x => new { x.ProjektId, x.DatumZalozeni })
            .HasDatabaseName("ix_vyzvy_projekt_datum_desc");
    }
}

internal sealed class VyzvaHistorieStavuEntityConfiguration : IEntityTypeConfiguration<VyzvaHistorieStavuEntity>
{
    public void Configure(EntityTypeBuilder<VyzvaHistorieStavuEntity> builder)
    {
        builder.ToTable("vyzva_historie_stavu");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.VyzvaId).HasColumnName("vyzva_id");
        builder.Property(x => x.PuvodniStav).HasColumnName("puvodni_stav").HasConversion<byte?>();
        builder.Property(x => x.NovyStav).HasColumnName("novy_stav").HasConversion<byte>();
        builder.Property(x => x.DatumZmeny).HasColumnName("datum_zmeny");
        builder.Property(x => x.ZmenilOsobaId).HasColumnName("zmenil_osoba_id");

        builder.HasOne<VyzvaEntity>()
            .WithMany()
            .HasForeignKey(x => x.VyzvaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.VyzvaId).HasDatabaseName("ix_vyzva_historie_stavu_vyzva_id");
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Data/Configuration/
git commit -m "feat(vyzvy): VyzvaEntityConfiguration + covering index, remove legacy ChallengeLookup"
```

---

## Task 19: Registrace DbSet v PmTrackerDbContext

**Files:**
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`

- [ ] **Step 1: Rename CiselnikVyzvy → Vyzvy + nový DbSet**

V `PmTrackerDbContext.cs` najdi řádek:

```csharp
    public DbSet<CiselnikVyzvaEntity> CiselnikVyzvy => Set<CiselnikVyzvaEntity>();
```

Nahraď:

```csharp
    public DbSet<VyzvaEntity> Vyzvy => Set<VyzvaEntity>();
    public DbSet<VyzvaHistorieStavuEntity> VyzvaHistorieStavu => Set<VyzvaHistorieStavuEntity>();
```

- [ ] **Step 2: OnModelCreating — ověř, že Assembly scan běží**

V `OnModelCreating` najdi `ApplyConfigurationsFromAssembly` nebo explicit registrace. Pokud scan: automaticky pojme nové konfigurace. Pokud explicit, přidej:

```csharp
        modelBuilder.ApplyConfiguration(new VyzvaEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VyzvaHistorieStavuEntityConfiguration());
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Data/PmTrackerDbContext.cs
git commit -m "feat(vyzvy): DbSet Vyzvy + VyzvaHistorieStavu v PmTrackerDbContext"
```

---

## Task 20: EF migrace VyzvyFaze1

**Files:**
- Create: `PmTracker.Web/Migrations/{timestamp}_VyzvyFaze1.cs` (auto)
- Modify: `PmTracker.Web/Migrations/PmTrackerDbContextModelSnapshot.cs` (auto)

- [ ] **Step 1: Generovat migraci**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet ef migrations add VyzvyFaze1 \
  --project PmTracker.Web/PmTracker.Web.csproj \
  --startup-project PmTracker.Web/PmTracker.Web.csproj \
  --output-dir Migrations
```

Expected: `Done.` + nový soubor v `Migrations/`.

- [ ] **Step 2: Review migrace**

Otevři vygenerovaný `{timestamp}_VyzvyFaze1.cs` a ověř:
- `CreateTable("vyzvy", …)` s 12 sloupci
- `CreateTable("vyzva_historie_stavu", …)` s 6 sloupci
- `AddColumn("misto_plneni")`, `AddColumn("cislo_ramcove_smlouvy")` v `projekty`
- `RenameColumn("vyzva" → "vyzva_id")` v `zaznam_externi_odkazy`
- `AddColumn("zaradid_do_vyzvy")` v `zaznam_externi_odkazy`
- `DropTable("ciselnik_vyzvy")`
- 3 indexy (`ux_vyzvy_smlouva_rok_poradove`, `ix_vyzvy_projekt_datum_desc`, `ux_zaznam_externi_odkazy_cislo_in_vyzve`)
- `ix_vyzva_historie_stavu_vyzva_id`

Pokud některé chybí — vrať se do Task 14-18, doplň, pak `dotnet ef migrations remove` + znovu `add`.

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Migrations/
git commit -m "feat(vyzvy): EF migration VyzvyFaze1 (schéma + rename + indexy)"
```

---

## Task 21: db_upgrade_1_1_8 SQL skript

**Files:**
- Create: `db_upgrade_1_1_8_vyzvy.sql`

- [ ] **Step 1: Podívat se na vzor**

Run: `head -40 "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_1_7_new_task_status.sql"`

- [ ] **Step 2: Vytvořit skript**

```sql
-- db_upgrade_1_1_8_vyzvy.sql
-- ServiceDesk Výzvy — Fáze 1: schéma
-- Spec: docs/superpowers/specs/2026-04-20-servicedesk-integrace-vyzvy-design.md
-- Plan: docs/superpowers/plans/2026-04-20-servicedesk-vyzvy-faze-1-schema-backend.md

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Projekt: nová pole
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projekty') AND name = 'misto_plneni')
BEGIN
    ALTER TABLE dbo.projekty ADD misto_plneni NVARCHAR(500) NULL;
    ALTER TABLE dbo.projekty ADD cislo_ramcove_smlouvy NVARCHAR(100) NULL;
END;

-- 2. Drop starého ciselnik_vyzvy (ověř prázdnost, safety check)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ciselnik_vyzvy')
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy)
        THROW 50000, 'ciselnik_vyzvy contains data — manual migration required', 1;

    DECLARE @fkName SYSNAME = (
        SELECT name FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
          AND referenced_object_id = OBJECT_ID('dbo.ciselnik_vyzvy')
    );
    IF @fkName IS NOT NULL
        EXEC('ALTER TABLE dbo.zaznam_externi_odkazy DROP CONSTRAINT ' + @fkName);

    DROP TABLE dbo.ciselnik_vyzvy;
END;

-- 3. Rename zaznam_externi_odkazy.vyzva → vyzva_id + zaradid_do_vyzvy
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'vyzva')
    EXEC sp_rename 'dbo.zaznam_externi_odkazy.vyzva', 'vyzva_id', 'COLUMN';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'zaradid_do_vyzvy')
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD zaradid_do_vyzvy BIT NOT NULL CONSTRAINT df_zeo_zaradid DEFAULT 0;

-- 4. Vyzvy
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzvy')
BEGIN
    CREATE TABLE dbo.vyzvy (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        projekt_id INT NOT NULL,
        kod NVARCHAR(20) NOT NULL,
        poradove_v_roce INT NOT NULL,
        rok INT NOT NULL,
        stav TINYINT NOT NULL,
        datum_zalozeni DATETIME2 NOT NULL,
        zalozil_osoba_id INT NOT NULL,
        datum_odeslani DATETIME2 NULL,
        odeslal_osoba_id INT NULL,
        misto_plneni_snapshot NVARCHAR(500) NOT NULL,
        cislo_ramcove_smlouvy_snapshot NVARCHAR(100) NOT NULL,
        CONSTRAINT fk_vyzvy_projekt FOREIGN KEY (projekt_id) REFERENCES dbo.projekty(id)
    );

    CREATE UNIQUE INDEX ux_vyzvy_smlouva_rok_poradove
        ON dbo.vyzvy (cislo_ramcove_smlouvy_snapshot, rok, poradove_v_roce);

    CREATE INDEX ix_vyzvy_projekt_datum_desc
        ON dbo.vyzvy (projekt_id, datum_zalozeni);
END;

-- 5. FK + filtered unique index
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_zeo_vyzva')
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD CONSTRAINT fk_zeo_vyzva
        FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_zaznam_externi_odkazy_cislo_in_vyzve')
    CREATE UNIQUE INDEX ux_zaznam_externi_odkazy_cislo_in_vyzve
        ON dbo.zaznam_externi_odkazy (cislo)
        WHERE vyzva_id IS NOT NULL;

-- 6. Audit
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzva_historie_stavu')
BEGIN
    CREATE TABLE dbo.vyzva_historie_stavu (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        vyzva_id INT NOT NULL,
        puvodni_stav TINYINT NULL,
        novy_stav TINYINT NOT NULL,
        datum_zmeny DATETIME2 NOT NULL,
        zmenil_osoba_id INT NOT NULL,
        CONSTRAINT fk_vhs_vyzva FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE CASCADE
    );

    CREATE INDEX ix_vyzva_historie_stavu_vyzva_id ON dbo.vyzva_historie_stavu (vyzva_id);
END;

COMMIT TRANSACTION;
```

- [ ] **Step 3: Commit**

```bash
git add db_upgrade_1_1_8_vyzvy.sql
git commit -m "feat(vyzvy): db_upgrade_1_1_8 SQL skript pro produkční DB"
```

---

## Task 22: Aktualizovat SeedBaselineDocumentationTests + docs

**Files:**
- Modify: `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:99`
- Modify: `docs/` baseline docs

- [ ] **Step 1: Test expectation**

V `SeedBaselineDocumentationTests.cs:99` nahraď:

```csharp
        document.Should().Contain("dbo.ciselnik_vyzvy");
```

za:

```csharp
        document.Should().Contain("dbo.vyzvy");
        document.Should().Contain("dbo.vyzva_historie_stavu");
```

- [ ] **Step 2: Najít baseline doc a aktualizovat**

Run: `grep -l "ciselnik_vyzvy" docs/ -r 2>/dev/null`

V nalezeném souboru nahraď `dbo.ciselnik_vyzvy` → `dbo.vyzvy` a doplň `dbo.vyzva_historie_stavu` do seznamu schémat.

- [ ] **Step 3: Test**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SeedBaselineDocumentation" --no-restore`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs docs/
git commit -m "test(vyzvy): baseline doc očekává vyzvy + vyzva_historie_stavu"
```

---

## Task 23: VyzvaCodeGenerator (static pure)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaCodeGeneratorTests
{
    [Fact]
    public void Generuj_FormatujeJakoPoradoveLomitkoRok()
        => VyzvaCodeGenerator.Generuj(poradoveVRoce: 2, rok: 2026).Should().Be("2/2026");

    [Fact]
    public void DalsiPoradoveVRoce_Prazdny_Vraci1()
        => VyzvaCodeGenerator.DalsiPoradoveVRoce(Array.Empty<int>()).Should().Be(1);

    [Fact]
    public void DalsiPoradoveVRoce_NejvyssiPlus1()
        => VyzvaCodeGenerator.DalsiPoradoveVRoce(new[] { 1, 2, 5 }).Should().Be(6);
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaCodeGenerator" --no-restore`
Expected: BUILD FAIL

- [ ] **Step 3: Implementace**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs
namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvaCodeGenerator
{
    public static string Generuj(int poradoveVRoce, int rok)
        => $"{poradoveVRoce}/{rok}";

    public static int DalsiPoradoveVRoce(IReadOnlyCollection<int> existujiciPoradove)
        => existujiciPoradove.Count == 0 ? 1 : existujiciPoradove.Max() + 1;
}
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaCodeGenerator" --no-restore`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs
git commit -m "feat(vyzvy): VyzvaCodeGenerator (static pure, TDD)"
```

---

## Task 24: VyzvaStateMachine (static pure)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaStateMachineTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaStateMachine.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaStateMachineTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaStateMachineTests
{
    [Theory]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Zruseno)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Zruseno)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Priprava)]
    public void JePovoleny_PovoleneKombinace_True(VyzvaStav z, VyzvaStav na)
        => VyzvaStateMachine.JePovolenyPrechod(z, na).Should().BeTrue();

    [Theory]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Zruseno)]
    public void JePovoleny_NepovoleneKombinace_False(VyzvaStav z, VyzvaStav na)
        => VyzvaStateMachine.JePovolenyPrechod(z, na).Should().BeFalse();
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaStateMachine" --no-restore`
Expected: BUILD FAIL

- [ ] **Step 3: Implementace**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaStateMachine.cs
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvaStateMachine
{
    public static bool JePovolenyPrechod(VyzvaStav z, VyzvaStav na)
        => (z, na) switch
        {
            (VyzvaStav.Priprava, VyzvaStav.Odeslano) => true,
            (VyzvaStav.Priprava, VyzvaStav.Zruseno) => true,
            (VyzvaStav.Odeslano, VyzvaStav.Priprava) => true,
            (VyzvaStav.Odeslano, VyzvaStav.Zruseno) => true,
            (VyzvaStav.Zruseno, VyzvaStav.Priprava) => true,
            _ => false,
        };
}
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaStateMachine" --no-restore`
Expected: 9 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaStateMachine.cs PmTracker.Tests.Unit/Vyzvy/VyzvaStateMachineTests.cs
git commit -m "feat(vyzvy): VyzvaStateMachine (static pure, TDD)"
```

---

## Task 25: VyzvaQueries (static IQueryable extensions — deduplikace buffer filtru)

**Files:**
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaQueries.cs`

- [ ] **Step 1: Extension class**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaQueries.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

internal static class VyzvaQueries
{
    public const string PnfKod = "PNF";

    /// <summary>
    /// Filtr pro buffer projektu — PNF externí vazby s ZaradidDoVyzvy=true a bez výzvy.
    /// Joinuje přes ProjektovyZaznam pro získání ProjektId.
    /// </summary>
    public static IQueryable<ZaznamExterniOdkazEntity> WhereVBufferuProjektu(
        this IQueryable<ZaznamExterniOdkazEntity> source,
        PmTrackerDbContext db,
        int projektId,
        int pnfTypId)
        => from ev in source
           join z in db.ProjektoveZaznamy on ev.ZaznamId equals z.Id
           where z.ProjektId == projektId
             && ev.TypOdkazuId == pnfTypId
             && ev.ZaradidDoVyzvy
             && ev.VyzvaId == null
           select ev;

    public static Task<int> GetPnfTypIdAsync(PmTrackerDbContext db, CancellationToken ct)
        => db.CiselnikTypuExternichOdkazu
            .Where(t => t.Kod == PnfKod)
            .Select(t => t.Id)
            .FirstAsync(ct);
}
```

**Název DbSet `ProjektoveZaznamy`:** ověř přesný název v `PmTrackerDbContext.cs` a v případě rozdílu oprav.

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaQueries.cs
git commit -m "feat(vyzvy): VyzvaQueries — IQueryable extension pro buffer filter (deduplikace)"
```

---

## Task 26: IVyzvaService + Contract DTOs + Errors + Result type

**Files:**
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs`
- Create: `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaBufferItem.cs`
- Create: `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaDetail.cs`
- Create: `PmTracker.Web/Services/Vyzvy/IVyzvaService.cs`

- [ ] **Step 1: Zkontrolovat, zda už v projektu existuje Result type**

Run: `grep -rn "public.*record.*Result<" PmTracker.Web/Services/ | head -5`

Pokud existuje reusable Result type, použij ho. Pokud ne, použij níže navržený.

- [ ] **Step 2: VyzvaErrors**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs
namespace PmTracker.Web.Services.Vyzvy;

public enum VyzvaErrorCode
{
    None = 0,
    ProjectNotFound = 1,
    ProjectMissingMistoPlneni = 2,
    ProjectMissingCisloRamcoveSmlouvy = 3,
    BufferEmpty = 4,
    InvalidStateTransition = 5,
    VyzvaNotFound = 6,
    ExternalLinkNotFound = 7,
    ExternalLinkNotPnf = 8,
    VyzvaIsLocked = 9,
    PnfAlreadyInAnotherVyzva = 10,
}

public sealed record VyzvaError(VyzvaErrorCode Code, string Message);

public readonly record struct Unit;

public abstract record VyzvaResult<TValue>
{
    public sealed record Ok(TValue Value) : VyzvaResult<TValue>;
    public sealed record Fail(VyzvaError Error) : VyzvaResult<TValue>;
}
```

- [ ] **Step 3: Contract DTOs (records — plochý list pro širokoúhlé UI)**

```csharp
// PmTracker.Web/Services/Vyzvy/Contracts/VyzvaBufferItem.cs
namespace PmTracker.Web.Services.Vyzvy.Contracts;

public sealed record VyzvaBufferItem(
    int ExterniOdkazId,
    int ZaznamId,
    string Cislo,
    string? StrucneNazev,
    decimal? PredpokladanaCena);
```

```csharp
// PmTracker.Web/Services/Vyzvy/Contracts/VyzvaDetail.cs
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy.Contracts;

public sealed record VyzvaDetail(
    int Id,
    int ProjektId,
    string Kod,
    int Rok,
    int PoradoveVRoce,
    VyzvaStav Stav,
    DateTime DatumZalozeni,
    int ZalozilOsobaId,
    DateTime? DatumOdeslani,
    int? OdeslalOsobaId,
    string MistoPlneniSnapshot,
    string CisloRamcoveSmlouvySnapshot,
    IReadOnlyList<VyzvaDetailItem> Polozky);

public sealed record VyzvaDetailItem(
    int ExterniOdkazId,
    int ZaznamId,
    string Cislo,
    string? StrucneNazev,
    decimal? PredpokladanaCena);
```

- [ ] **Step 4: IVyzvaService**

```csharp
// PmTracker.Web/Services/Vyzvy/IVyzvaService.cs
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public interface IVyzvaService
{
    Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct);
    Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct);
    Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct);

    Task<VyzvaResult<VyzvaDetail>> ZaloztVyzvuZBufferuAsync(
        int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct);

    Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(
        int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct);

    Task<VyzvaResult<Unit>> NastavitZaradidAsync(
        int externiOdkazId, bool zaradit, CancellationToken ct);

    Task<VyzvaResult<Unit>> PrerditPnfAsync(
        int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct);
}
```

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/IVyzvaService.cs PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs PmTracker.Web/Services/Vyzvy/Contracts/
git commit -m "feat(vyzvy): IVyzvaService kontrakt + DTO + Result type"
```

---

## Task 27: VyzvaService core (konstruktor + DI registrace)

**Files:**
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvyServiceCollectionExtensions.cs`
- Modify: `PmTracker.Web/Program.cs`

- [ ] **Step 1: Partial class core**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaService.cs
using PmTracker.Web.Data;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService : IVyzvaService
{
    private readonly PmTrackerDbContext _db;
    private readonly ITicketingQueryService _ticketing;

    public VyzvaService(PmTrackerDbContext db, ITicketingQueryService ticketing)
    {
        _db = db;
        _ticketing = ticketing;
    }

    private Task<int> GetPnfTypIdAsync(CancellationToken ct)
        => VyzvaQueries.GetPnfTypIdAsync(_db, ct);

    private async Task<IReadOnlyDictionary<string, HotZaznamDto>> LoadHotZaznamyAsync(
        IReadOnlyCollection<string> cisla, CancellationToken ct)
    {
        if (cisla.Count == 0) return new Dictionary<string, HotZaznamDto>();
        return await _ticketing.GetZaznamyAsync(cisla, ct);
    }
}
```

- [ ] **Step 2: DI extension**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvyServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvyServiceCollectionExtensions
{
    public static IServiceCollection AddVyzvyServices(this IServiceCollection services)
    {
        services.AddScoped<IVyzvaService, VyzvaService>();
        return services;
    }
}
```

- [ ] **Step 3: Program.cs — přidat AddVyzvyServices()**

Za existující `AddServiceDeskIntegration`:

```csharp
builder.Services
    .AddServiceDeskIntegration(builder.Configuration)
    .AddVyzvyServices();
```

*(Čtecí kontrola: `AddServiceDeskIntegration` vrací `IServiceCollection`, takže chainování funguje.)*

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: BUILD FAIL — chybí implementace metod z IVyzvaService. To je očekávané — metody dodáme v partial class v dalších úkolech. Dočasně přidej **stub** do Task 27 Step 1 před SaveChanges:

```csharp
    // Dočasné stuby — nahradí se v Task 28-31.
    public Task<IReadOnlyList<Contracts.VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
        => throw new NotImplementedException("Task 28");
    public Task<IReadOnlyList<Contracts.VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
        => throw new NotImplementedException("Task 28");
    public Task<Contracts.VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
        => throw new NotImplementedException("Task 28");
    public Task<VyzvaResult<Contracts.VyzvaDetail>> ZaloztVyzvuZBufferuAsync(int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct)
        => throw new NotImplementedException("Task 29");
    public Task<VyzvaResult<Contracts.VyzvaDetail>> ZmenitStavAsync(int vyzvaId, PmTracker.Web.Models.Entities.VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
        => throw new NotImplementedException("Task 30");
    public Task<VyzvaResult<Unit>> NastavitZaradidAsync(int externiOdkazId, bool zaradit, CancellationToken ct)
        => throw new NotImplementedException("Task 31");
    public Task<VyzvaResult<Unit>> PrerditPnfAsync(int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
        => throw new NotImplementedException("Task 31");
```

Pak build:

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Web/Services/Vyzvy/VyzvyServiceCollectionExtensions.cs PmTracker.Web/Program.cs
git commit -m "feat(vyzvy): VyzvaService core (partial skelet) + DI"
```

---

## Task 28: VyzvaService.Queries.cs (partial — GetBuffer/GetVyzvy/GetVyzva)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTestHarness.cs`
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceQueryTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs` (odstranit stuby pro Get* metody)

- [ ] **Step 1: Test harness**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTestHarness.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;

namespace PmTracker.Tests.Unit.Vyzvy;

internal static class VyzvaServiceTestHarness
{
    public static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }

    public static VyzvaService CreateService(PmTrackerDbContext db, ITicketingQueryService? ticketing = null)
        => new(db, ticketing ?? new StubTicketingQueryService());

    public static async Task SeedProjektAsync(PmTrackerDbContext db, int projektId = 1)
    {
        if (await db.Projekty.AnyAsync(p => p.Id == projektId)) return;
        db.Projekty.Add(new ProjektEntity
        {
            Id = projektId,
            Zkratka = "P1", CelyNazev = "Projekt 1", StavId = 1,
            MistoPlneni = "FIS (EIS): VZ 8201",
            CisloRamcoveSmlouvy = "23106000271",
        });
        await db.SaveChangesAsync();
    }

    public static async Task SeedPnfTypAsync(PmTrackerDbContext db)
    {
        if (await db.CiselnikTypuExternichOdkazu.AnyAsync(t => t.Kod == "PNF")) return;
        db.CiselnikTypuExternichOdkazu.Add(new CiselnikTypuExternichOdkazuEntity
        {
            Id = 1, Kod = "PNF", Nazev = "PNF",
        });
        await db.SaveChangesAsync();
    }

    public static async Task SeedZaznamAsync(PmTrackerDbContext db, int zaznamId, int projektId = 1)
    {
        if (await db.ProjektoveZaznamy.AnyAsync(z => z.Id == zaznamId)) return;
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = zaznamId, ProjektId = projektId, KategorieId = 1, CisloZaznamu = zaznamId,
            CisloViditelne = $"RU{zaznamId}", Nazev = "test", VlastnikId = 1,
            DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow,
            SubsystemId = 1, HarmonogramSablonaVerze = 1,
        });
        await db.SaveChangesAsync();
    }

    private sealed class StubTicketingQueryService : ITicketingQueryService
    {
        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult<HotZaznamDto?>(null);
        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotZaznamDto>>(new Dictionary<string, HotZaznamDto>());
        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
            => Task.FromResult<HotKalkulaceDto?>(null);
        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(new Dictionary<string, HotKalkulaceDto>());
    }
}
```

**Ověření názvů DbSet:** `db.Projekty`, `db.CiselnikTypuExternichOdkazu`, `db.ProjektoveZaznamy` — ověř proti reálnému `PmTrackerDbContext.cs` a opravit případné rozdíly.

- [ ] **Step 2: Failing testy**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceQueryTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceQueryTests
{
    [Fact]
    public async Task GetBuffer_PnfSZaradidTrueBezVyzvy_Vraci()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 500, ZaznamId = 100, TypOdkazuId = 1, Cislo = "336865",
            ZaradidDoVyzvy = true, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.GetBufferAsync(1, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Cislo.Should().Be("336865");
    }

    [Fact]
    public async Task GetBuffer_PnfJizVeVyzve_Nevraci()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.Vyzvy.Add(new VyzvaEntity { Id = 10, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 501, ZaznamId = 100, TypOdkazuId = 1, Cislo = "X", ZaradidDoVyzvy = true, VyzvaId = 10 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.GetBufferAsync(1, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVyzvy_VraciSetridenoDatumZalozeniDesc()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            new VyzvaEntity { Id = 10, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 1, 5), ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" },
            new VyzvaEntity { Id = 11, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2026, 1, 10), ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.GetVyzvyAsync(1, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Kod.Should().Be("2/2026");
        result[1].Kod.Should().Be("1/2026");
    }

    [Fact]
    public async Task GetVyzva_Neexistuje_Null()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        var svc = VyzvaServiceTestHarness.CreateService(db);
        (await svc.GetVyzvaAsync(999, CancellationToken.None)).Should().BeNull();
    }
}
```

- [ ] **Step 3: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceQueryTests" --no-restore`
Expected: FAIL (NotImplementedException)

- [ ] **Step 4: Implementovat partial**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
    {
        var pnfTypId = await GetPnfTypIdAsync(ct);

        var raw = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .WhereVBufferuProjektu(_db, projektId, pnfTypId)
            .Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.PredpokladanaCena })
            .ToListAsync(ct);

        if (raw.Count == 0) return Array.Empty<VyzvaBufferItem>();

        var cisla = raw.Select(r => r.Cislo).Distinct().ToArray();
        var hot = await LoadHotZaznamyAsync(cisla, ct);

        return raw.Select(r => new VyzvaBufferItem(
            r.Id, r.ZaznamId, r.Cislo,
            hot.GetValueOrDefault(r.Cislo)?.Strucne,
            r.PredpokladanaCena)).ToList();
    }

    public async Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
    {
        var vyzvy = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId)
            .OrderByDescending(v => v.DatumZalozeni)
            .ToListAsync(ct);
        if (vyzvy.Count == 0) return Array.Empty<VyzvaDetail>();

        var vyzvaIds = vyzvy.Select(v => v.Id).ToArray();
        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId.HasValue && vyzvaIds.Contains(ev.VyzvaId.Value))
            .OrderBy(ev => ev.Id)
            .ToListAsync(ct);

        var polozkyMap = polozky.GroupBy(p => p.VyzvaId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var allCisla = polozky.Select(p => p.Cislo).Distinct().ToArray();
        var hot = await LoadHotZaznamyAsync(allCisla, ct);

        return vyzvy.Select(v => MapToDetail(v, polozkyMap.GetValueOrDefault(v.Id) ?? new(), hot)).ToList();
    }

    public async Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null) return null;

        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId == vyzvaId)
            .OrderBy(ev => ev.Id)
            .ToListAsync(ct);

        var hot = await LoadHotZaznamyAsync(polozky.Select(p => p.Cislo).Distinct().ToArray(), ct);
        return MapToDetail(vyzva, polozky, hot);
    }

    private static VyzvaDetail MapToDetail(
        VyzvaEntity v,
        IReadOnlyList<ZaznamExterniOdkazEntity> polozky,
        IReadOnlyDictionary<string, HotZaznamDto> hot)
        => new(
            v.Id, v.ProjektId, v.Kod, v.Rok, v.PoradoveVRoce, v.Stav,
            v.DatumZalozeni, v.ZalozilOsobaId, v.DatumOdeslani, v.OdeslalOsobaId,
            v.MistoPlneniSnapshot, v.CisloRamcoveSmlouvySnapshot,
            polozky.Select(ev => new VyzvaDetailItem(
                ev.Id, ev.ZaznamId, ev.Cislo,
                hot.GetValueOrDefault(ev.Cislo)?.Strucne,
                ev.PredpokladanaCena)).ToList());
}
```

- [ ] **Step 5: Odstranit stuby z VyzvaService.cs**

V `VyzvaService.cs` odstraň stuby pro `GetBufferAsync`, `GetVyzvyAsync`, `GetVyzvaAsync` (zůstávají jen v partial). Ostatní stuby ponech.

- [ ] **Step 6: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceQueryTests" --no-restore`
Expected: 4 passed

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/
git commit -m "feat(vyzvy): VyzvaService.Queries.cs — GetBuffer/GetVyzvy/GetVyzva (TDD, partial)"
```

---

## Task 29: VyzvaService.Founding.cs (partial — ZaloztVyzvuZBufferu)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaService.Founding.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs` (odstranit stub)

- [ ] **Step 1: Failing tests**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceFoundingTests
{
    [Fact]
    public async Task ZaloztVyzvuZBufferu_PrazdnyBuffer_BufferEmpty()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.BufferEmpty);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_BezMistaPlneni_Error()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = null, CisloRamcoveSmlouvy = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ProjectMissingMistoPlneni);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_ValidniBuffer_VytvoriVyzvuPriradiPnf()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 500, ZaznamId = 100, TypOdkazuId = 1, Cislo = "336865",
            ZaradidDoVyzvy = true, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var now = new DateTime(2026, 4, 20);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, now, CancellationToken.None);

        var ok = result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>().Which.Value;
        ok.Kod.Should().Be("1/2026");
        ok.PoradoveVRoce.Should().Be(1);
        ok.Stav.Should().Be(VyzvaStav.Priprava);
        ok.MistoPlneniSnapshot.Should().Be("FIS (EIS): VZ 8201");
        ok.Polozky.Should().HaveCount(1);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(500);
        ev!.VyzvaId.Should().Be(ok.Id);
        ev.ZaradidDoVyzvy.Should().BeTrue();

        db.VyzvaHistorieStavu.Should().ContainSingle(h => h.NovyStav == VyzvaStav.Priprava && h.PuvodniStav == null);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_DruheZalozeniStejnyRok_Vraci2LomitkoRok()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 200);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 501, ZaznamId = 200, TypOdkazuId = 1, Cislo = "X", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, new DateTime(2026, 6, 1), CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>()
            .Which.Value.Kod.Should().Be("2/2026");
    }
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceFoundingTests" --no-restore`
Expected: FAIL

- [ ] **Step 3: Implementace partial**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaService.Founding.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<VyzvaDetail>> ZaloztVyzvuZBufferuAsync(
        int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct)
    {
        var projekt = await _db.Projekty.FirstOrDefaultAsync(p => p.Id == projektId, ct);
        if (projekt == null)
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectNotFound, "Projekt nenalezen");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingMistoPlneni, "Projekt nemá místo plnění");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingCisloRamcoveSmlouvy, "Projekt nemá číslo rámcové smlouvy");

        var pnfTypId = await GetPnfTypIdAsync(ct);
        var bufferIds = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .WhereVBufferuProjektu(_db, projektId, pnfTypId)
            .Select(ev => ev.Id)
            .ToListAsync(ct);

        if (bufferIds.Count == 0)
            return Fail<VyzvaDetail>(VyzvaErrorCode.BufferEmpty, "Buffer je prázdný");

        var rok = now.Year;
        var existujiciPoradove = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.CisloRamcoveSmlouvySnapshot == projekt.CisloRamcoveSmlouvy && v.Rok == rok)
            .Select(v => v.PoradoveVRoce)
            .ToListAsync(ct);
        var dalsiPoradove = VyzvaCodeGenerator.DalsiPoradoveVRoce(existujiciPoradove);

        var vyzva = new VyzvaEntity
        {
            ProjektId = projektId,
            Kod = VyzvaCodeGenerator.Generuj(dalsiPoradove, rok),
            PoradoveVRoce = dalsiPoradove,
            Rok = rok,
            Stav = VyzvaStav.Priprava,
            DatumZalozeni = now,
            ZalozilOsobaId = zalozilOsobaId,
            MistoPlneniSnapshot = projekt.MistoPlneni!,
            CisloRamcoveSmlouvySnapshot = projekt.CisloRamcoveSmlouvy!,
        };
        _db.Vyzvy.Add(vyzva);
        await _db.SaveChangesAsync(ct);

        var polozky = await _db.ZaznamExterniOdkazy
            .Where(ev => bufferIds.Contains(ev.Id))
            .ToListAsync(ct);
        foreach (var p in polozky) p.VyzvaId = vyzva.Id;

        _db.VyzvaHistorieStavu.Add(new VyzvaHistorieStavuEntity
        {
            VyzvaId = vyzva.Id,
            PuvodniStav = null,
            NovyStav = VyzvaStav.Priprava,
            DatumZmeny = now,
            ZmenilOsobaId = zalozilOsobaId,
        });
        await _db.SaveChangesAsync(ct);

        var detail = await GetVyzvaAsync(vyzva.Id, ct);
        return new VyzvaResult<VyzvaDetail>.Ok(detail!);
    }

    private static VyzvaResult<T>.Fail Fail<T>(VyzvaErrorCode code, string msg)
        => new(new VyzvaError(code, msg));
}
```

- [ ] **Step 4: Odstranit stub z VyzvaService.cs**

Odstraň `ZaloztVyzvuZBufferuAsync` stub z `VyzvaService.cs`.

- [ ] **Step 5: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceFoundingTests" --no-restore`
Expected: 4 passed

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.Founding.cs PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs
git commit -m "feat(vyzvy): VyzvaService.Founding.cs (TDD, partial)"
```

---

## Task 30: VyzvaService.Transitions.cs (partial — ZmenitStav)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTransitionsTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaService.Transitions.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs` (odstranit stub)

- [ ] **Step 1: Failing tests**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTransitionsTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceTransitionsTests
{
    [Fact]
    public async Task PripravaNaOdeslano_ZapiseDatumOdeslaniAOsobu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Vyzvy.Add(NewVyzva(50, VyzvaStav.Priprava));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var now = new DateTime(2026, 4, 20);
        var result = await svc.ZmenitStavAsync(50, VyzvaStav.Odeslano, 7, now, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>();
        var v = await db.Vyzvy.FindAsync(50);
        v!.Stav.Should().Be(VyzvaStav.Odeslano);
        v.DatumOdeslani.Should().Be(now);
        v.OdeslalOsobaId.Should().Be(7);
    }

    [Fact]
    public async Task NepovolenyPrechod_Error()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Vyzvy.Add(NewVyzva(51, VyzvaStav.Zruseno));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZmenitStavAsync(51, VyzvaStav.Odeslano, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.InvalidStateTransition);
    }

    [Fact]
    public async Task Zruseno_VratiPnfDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 200);

        db.Vyzvy.Add(NewVyzva(52, VyzvaStav.Priprava));
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 900, ZaznamId = 200, TypOdkazuId = 1, Cislo = "111111",
            ZaradidDoVyzvy = true, VyzvaId = 52,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.ZmenitStavAsync(52, VyzvaStav.Zruseno, 7, DateTime.UtcNow, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(900);
        ev!.VyzvaId.Should().BeNull();
        ev.ZaradidDoVyzvy.Should().BeTrue();
    }

    private static VyzvaEntity NewVyzva(int id, VyzvaStav stav) => new()
    {
        Id = id, ProjektId = 1, Kod = $"{id}/2026", PoradoveVRoce = id, Rok = 2026,
        Stav = stav, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1,
        MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
    };
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceTransitionsTests" --no-restore`
Expected: FAIL

- [ ] **Step 3: Implementace**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaService.Transitions.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<VyzvaDetail>> ZmenitStavAsync(
        int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null)
            return Fail<VyzvaDetail>(VyzvaErrorCode.VyzvaNotFound, "Výzva nenalezena");

        var puvodni = vyzva.Stav;
        if (!VyzvaStateMachine.JePovolenyPrechod(puvodni, novyStav))
            return Fail<VyzvaDetail>(VyzvaErrorCode.InvalidStateTransition,
                $"Přechod {puvodni} → {novyStav} není povolen");

        vyzva.Stav = novyStav;

        if (novyStav == VyzvaStav.Odeslano)
        {
            vyzva.DatumOdeslani = now;
            vyzva.OdeslalOsobaId = zmenilOsobaId;
        }

        if (novyStav == VyzvaStav.Zruseno)
        {
            var polozky = await _db.ZaznamExterniOdkazy
                .Where(ev => ev.VyzvaId == vyzvaId)
                .ToListAsync(ct);
            foreach (var p in polozky) p.VyzvaId = null;
        }

        _db.VyzvaHistorieStavu.Add(new VyzvaHistorieStavuEntity
        {
            VyzvaId = vyzvaId,
            PuvodniStav = puvodni,
            NovyStav = novyStav,
            DatumZmeny = now,
            ZmenilOsobaId = zmenilOsobaId,
        });
        await _db.SaveChangesAsync(ct);

        var detail = await GetVyzvaAsync(vyzvaId, ct);
        return new VyzvaResult<VyzvaDetail>.Ok(detail!);
    }
}
```

- [ ] **Step 4: Odstranit stub**

V `VyzvaService.cs` odstraň `ZmenitStavAsync` stub.

- [ ] **Step 5: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceTransitionsTests" --no-restore`
Expected: 3 passed

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.Transitions.cs PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTransitionsTests.cs
git commit -m "feat(vyzvy): VyzvaService.Transitions.cs (TDD, partial)"
```

---

## Task 31: VyzvaService.Assignment.cs (partial — NastavitZaradid + PrerditPnf)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceAssignmentTests.cs`
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceReassignmentTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs` (odstranit stuby)

- [ ] **Step 1: Failing tests (Assignment — NastavitZaradid)**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceAssignmentTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceAssignmentTests
{
    [Fact]
    public async Task ZaradidOn_BezPripravaVyzvy_DaDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 1);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 10, ZaznamId = 1, TypOdkazuId = 1, Cislo = "222222" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(10, true, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(10);
        ev!.ZaradidDoVyzvy.Should().BeTrue();
        ev.VyzvaId.Should().BeNull();
    }

    [Fact]
    public async Task ZaradidOn_PripravaVyzvaExistuje_Priradi()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 2);
        db.Vyzvy.Add(new VyzvaEntity { Id = 30, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 11, ZaznamId = 2, TypOdkazuId = 1, Cislo = "333333" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(11, true, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.FindAsync(11))!.VyzvaId.Should().Be(30);
    }

    [Fact]
    public async Task ZaradidOn_DveVyzvyPriprava_PriradiNejnizsiPoradove()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 3);
        db.Vyzvy.AddRange(
            new VyzvaEntity { Id = 40, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" },
            new VyzvaEntity { Id = 41, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 12, ZaznamId = 3, TypOdkazuId = 1, Cislo = "444444" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(12, true, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.FindAsync(12))!.VyzvaId.Should().Be(41);
    }

    [Fact]
    public async Task ZaradidOff_PripravaVyzva_OdebereZVyzvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 5);
        db.Vyzvy.Add(new VyzvaEntity { Id = 60, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 13, ZaznamId = 5, TypOdkazuId = 1, Cislo = "555555", ZaradidDoVyzvy = true, VyzvaId = 60 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(13, false, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(13);
        ev!.ZaradidDoVyzvy.Should().BeFalse();
        ev.VyzvaId.Should().BeNull();
    }

    [Fact]
    public async Task ZaradidOff_OdeslanoVyzva_Locked()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 6);
        db.Vyzvy.Add(new VyzvaEntity { Id = 70, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 14, ZaznamId = 6, TypOdkazuId = 1, Cislo = "666666", ZaradidDoVyzvy = true, VyzvaId = 70 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(14, false, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<Unit>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.VyzvaIsLocked);
    }

    [Fact]
    public async Task NonPnfTyp_ExternalLinkNotPnf()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.CiselnikTypuExternichOdkazu.AddRange(
            new CiselnikTypuExternichOdkazuEntity { Id = 1, Kod = "PNF", Nazev = "PNF" },
            new CiselnikTypuExternichOdkazuEntity { Id = 2, Kod = "NES", Nazev = "NES" });
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 7);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 15, ZaznamId = 7, TypOdkazuId = 2, Cislo = "777777" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(15, true, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<Unit>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ExternalLinkNotPnf);
    }
}
```

- [ ] **Step 2: Failing tests (Reassignment — PrerditPnf)**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceReassignmentTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceReassignmentTests
{
    [Fact]
    public async Task PrerditZBufferuDoPripravaVyzvy_Ok()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 9);
        db.Vyzvy.Add(new VyzvaEntity { Id = 80, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 20, ZaznamId = 9, TypOdkazuId = 1, Cislo = "888888", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.PrerditPnfAsync(20, 80, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<Unit>.Ok>();
        (await db.ZaznamExterniOdkazy.FindAsync(20))!.VyzvaId.Should().Be(80);
    }

    [Fact]
    public async Task PrerditDoNull_VratiDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 10);
        db.Vyzvy.Add(new VyzvaEntity { Id = 90, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 21, ZaznamId = 10, TypOdkazuId = 1, Cislo = "999999", ZaradidDoVyzvy = true, VyzvaId = 90 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.PrerditPnfAsync(21, null, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(21);
        ev!.VyzvaId.Should().BeNull();
        ev.ZaradidDoVyzvy.Should().BeTrue();
    }

    [Fact]
    public async Task PrerditDoOdeslaneVyzvy_Locked()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 11);
        db.Vyzvy.Add(new VyzvaEntity { Id = 100, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 22, ZaznamId = 11, TypOdkazuId = 1, Cislo = "101010", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.PrerditPnfAsync(22, 100, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<Unit>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.VyzvaIsLocked);
    }
}
```

- [ ] **Step 3: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceAssignmentTests|FullyQualifiedName~VyzvaServiceReassignmentTests" --no-restore`
Expected: FAIL

- [ ] **Step 4: Implementace**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    public async Task<VyzvaResult<Unit>> NastavitZaradidAsync(
        int externiOdkazId, bool zaradit, CancellationToken ct)
    {
        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena");

        var pnfTypId = await GetPnfTypIdAsync(ct);
        if (odkaz.TypOdkazuId != pnfTypId)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotPnf, "Switch lze použít jen pro PNF");

        // Ochrana: nedá se změnit switch u PNF, který je v odeslané výzvě.
        if (odkaz.VyzvaId.HasValue)
        {
            var aktualniVyzva = await _db.Vyzvy.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == odkaz.VyzvaId!.Value, ct);
            if (aktualniVyzva is { Stav: VyzvaStav.Odeslano })
                return Fail<Unit>(VyzvaErrorCode.VyzvaIsLocked, "Výzva je odeslaná");
        }

        odkaz.ZaradidDoVyzvy = zaradit;

        if (zaradit && odkaz.VyzvaId == null)
        {
            var projektId = await _db.ProjektoveZaznamy
                .Where(z => z.Id == odkaz.ZaznamId)
                .Select(z => z.ProjektId)
                .FirstAsync(ct);

            var cilovaVyzva = await _db.Vyzvy.AsNoTracking()
                .Where(v => v.ProjektId == projektId && v.Stav == VyzvaStav.Priprava)
                .OrderBy(v => v.PoradoveVRoce)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync(ct);

            odkaz.VyzvaId = cilovaVyzva;
        }
        else if (!zaradit && odkaz.VyzvaId.HasValue)
        {
            odkaz.VyzvaId = null;
        }

        await _db.SaveChangesAsync(ct);
        return new VyzvaResult<Unit>.Ok(default);
    }

    public async Task<VyzvaResult<Unit>> PrerditPnfAsync(
        int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
    {
        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena");

        var pnfTypId = await GetPnfTypIdAsync(ct);
        if (odkaz.TypOdkazuId != pnfTypId)
            return Fail<Unit>(VyzvaErrorCode.ExternalLinkNotPnf, "Jen PNF");

        if (odkaz.VyzvaId.HasValue)
        {
            var src = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == odkaz.VyzvaId!.Value, ct);
            if (src is { Stav: VyzvaStav.Odeslano })
                return Fail<Unit>(VyzvaErrorCode.VyzvaIsLocked, "Zdrojová výzva je odeslaná");
        }

        if (cilovaVyzvaId.HasValue)
        {
            var cil = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == cilovaVyzvaId.Value, ct);
            if (cil == null)
                return Fail<Unit>(VyzvaErrorCode.VyzvaNotFound, "Cílová výzva neexistuje");
            if (cil.Stav != VyzvaStav.Priprava)
                return Fail<Unit>(VyzvaErrorCode.VyzvaIsLocked, "Cílová výzva není v Priprava");
        }

        odkaz.VyzvaId = cilovaVyzvaId;
        odkaz.ZaradidDoVyzvy = true; // explicitní přeřazení → switch ON

        await _db.SaveChangesAsync(ct);
        return new VyzvaResult<Unit>.Ok(default);
    }
}
```

- [ ] **Step 5: Odstranit stuby**

V `VyzvaService.cs` odstraň `NastavitZaradidAsync` a `PrerditPnfAsync` stuby.

- [ ] **Step 6: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceAssignmentTests|FullyQualifiedName~VyzvaServiceReassignmentTests" --no-restore`
Expected: 9 passed (6 Assignment + 3 Reassignment)

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/
git commit -m "feat(vyzvy): VyzvaService.Assignment.cs — NastavitZaradid + PrerditPnf (TDD, partial)"
```

---

## Task 32: Full run + finální verify

- [ ] **Step 1: Run celé Unit testy**

Run: `dotnet test PmTracker.Tests.Unit --no-restore`
Expected: všechny testy PASS (nové + existující)

- [ ] **Step 2: Opravit zbývající rozbité testy (pokud jsou)**

Pravděpodobné příčiny:
- reference na `CiselnikVyzvaEntity` → `VyzvaEntity`
- `.Vyzva` → `.VyzvaId`
- baseline doc ještě ne plně aktualizovaný

- [ ] **Step 3: Build celé solution**

Run: `dotnet build --no-restore`
Expected: Build succeeded, 0 Error(s) napříč všemi 7 projekty (Web + Contracts + Sql + 5 test projektů).

- [ ] **Step 4: Ověřit běh aplikace**

Run: `dotnet run --project PmTracker.Web/PmTracker.Web.csproj --no-build --urls http://localhost:5999 2>&1 &  sleep 5; kill %1 2>/dev/null; wait 2>/dev/null`
Expected: app startuje bez výjimky; v logu je info o `ServiceDesk integrace je vypnutá` (z DisabledTicketingQueryService) jen pokud volá business kód — jinak žádné errory.

- [ ] **Step 5: Git status**

Run: `git status && git log --oneline -n 35`
Expected: pracovní strom čistý, všechny commits zaregistrované.

---

## Hotovo — Fáze 1

Po dokončení plánu máš:

- ✅ **2 samostatné projekty ServiceDesk konektoru** (`Contracts` + `Sql`) — Web nevidí HOT entity
- ✅ **Feature flag + DisabledService** (fail-safe bez connection stringu) + fail-fast při chybné konfiguraci
- ✅ **Request-scoped cache dekorátor** (anti-N+1)
- ✅ **Schéma DB** (EF migrace + SQL upgrade) včetně covering indexu
- ✅ **`VyzvaService` rozložený do 5 partial souborů** + 3 static helper (`VyzvaCodeGenerator`, `VyzvaStateMachine`, `VyzvaQueries`)
- ✅ **Všechny typy `sealed`**, DTO immutable, read dotazy `AsNoTracking`
- ✅ **Audit log** (`VyzvaHistorieStavu`)
- ✅ **32 testů** (buffer / founding / transitions / assignment / reassignment / code gen / state machine / ServiceDesk sql/disabled/caching)
- ✅ **DI extensions** (`AddServiceDeskIntegration`, `AddVyzvyServices`) — čistý `Program.cs`

### Co chybí do produkce (samostatné plány)

- **Fáze 2:** UI — záložka Výzvy (postranník + detail s buffer/výzvou), switch na externí vazbě, DnD modal, editace projektu (`MistoPlneni`/`CisloRamcoveSmlouvy`), fallback číselník
- **Fáze 3:** Word export (OpenXML šablona, vodoznak NÁVRH, APTI tabulky z `HOT_KALKULACE`)
- **Fáze 4:** Secret manager / Key Vault pro connection string, Testcontainers integrační testy (skutečný SQL Server), akceptační test vs. vzorový `.docx`
