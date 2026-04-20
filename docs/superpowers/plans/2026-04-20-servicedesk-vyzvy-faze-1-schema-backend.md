# ServiceDesk Výzvy — Fáze 1: Schéma a backend core

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zavést databázové schéma a backend služby pro automatizované sestavování výzev z PNF externích vazeb, bez UI a bez Word exportu.

**Architecture:** EF Core code-first migrace + upgrade SQL skripty (vzor `db_upgrade_*.sql`). Přejmenovat `CiselnikVyzva` → `Vyzva` s rozšířenými atributy a stavovým strojem. Přidat virtuální buffer (filtr nad `ZaznamExterniOdkaz`). Druhý DbContext `TicketingReadOnlyDbContext` pro čtení z `intranetNEW.dbo.HOT_*` (read-only, bez cache). Service layer `VyzvaService` pro založení, přechody stavu, přeřazování. Testy xUnit v `PmTracker.Tests.Unit`.

**Tech Stack:** .NET 8, EF Core (SqlServer), xUnit + FluentAssertions, DocumentFormat.OpenXml (fáze 3), Hangfire (mimo scope této fáze).

**Spec:** [docs/superpowers/specs/2026-04-20-servicedesk-integrace-vyzvy-design.md](../specs/2026-04-20-servicedesk-integrace-vyzvy-design.md)

**Rozsah plánu:** sekce 3 (datový model), 4 (buffer), 5 (life-cycle), 8 (ServiceDesk integrace — read-only DbContext bez UI), 9 (audit). **Mimo scope:** sekce 6 (UI) → fáze 2, sekce 7 (Word) → fáze 3, závěrečná akceptace → fáze 4. Dostupné jako samostatné plány později.

---

## File Structure

### Nové soubory

| Soubor | Zodpovědnost |
|---|---|
| `PmTracker.Web/Models/Entities/VyzvaEntity.cs` | Entita `VyzvaEntity` (přesun z `PmTrackerEntities.cs`) |
| `PmTracker.Web/Models/Entities/VyzvaStav.cs` | Enum `VyzvaStav` (Priprava=1, Odeslano=2, Zruseno=3) |
| `PmTracker.Web/Models/Entities/VyzvaHistorieStavuEntity.cs` | Audit log přechodů stavu |
| `PmTracker.Web/Data/Configuration/VyzvaEntityConfiguration.cs` | EF mapping (tabulka `vyzvy`) |
| `PmTracker.Web/Services/Vyzvy/IVyzvaService.cs` | Service interface (založení, přechody, přeřazení) |
| `PmTracker.Web/Services/Vyzvy/VyzvaService.cs` | Implementace |
| `PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs` | Typed error codes pro service |
| `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaBufferItem.cs` | DTO jednoho PNF v bufferu |
| `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaDetail.cs` | DTO výzvy + jejích PNF |
| `PmTracker.Web/Services/Ticketing/TicketingReadOnlyDbContext.cs` | Druhý DbContext pro `intranetNEW.HOT_*` |
| `PmTracker.Web/Services/Ticketing/Entities/HotZaznamEntity.cs` | Mapování `HOT_ZAZNAMY` |
| `PmTracker.Web/Services/Ticketing/Entities/HotKalkulaceEntity.cs` | Mapování `HOT_KALKULACE` |
| `PmTracker.Web/Services/Ticketing/Entities/HotPidEntity.cs` | Mapování `HOT_PID` |
| `PmTracker.Web/Services/Ticketing/ITicketingQueryService.cs` | Query interface |
| `PmTracker.Web/Services/Ticketing/TicketingQueryService.cs` | Implementace read-through dotazů |
| `PmTracker.Web/Services/Ticketing/Contracts/HotKalkulaceDto.cs` | DTO akceptované kalkulace (APTI + licence) |
| `PmTracker.Web/Services/Ticketing/Contracts/HotZaznamDto.cs` | DTO ticketu (strucne, popis, typ) |
| `PmTracker.Web/Migrations/{timestamp}_VyzvyFaze1.cs` | EF migrace |
| `db_upgrade_1_1_8_vyzvy.sql` | Ruční upgrade SQL skript pro produkční DB |
| `PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs` | Unit test auto-číslování |
| `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceStateTransitionsTests.cs` | Unit test přechodů stavu |
| `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceBufferTests.cs` | Unit test buffer + zařazování switch ON/OFF |
| `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs` | Unit test založení výzvy (Založit z bufferu) |
| `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceReassignmentTests.cs` | Unit test přeřazování mezi výzvami |
| `PmTracker.Tests.Unit/Ticketing/TicketingQueryServiceTests.cs` | Test filtru akceptace + mapování DTO (InMemory provider + seeded data) |

### Modifikované soubory

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` | Smazat `CiselnikVyzvaEntity`; rozšířit `ProjektEntity` o `MistoPlneni`, `CisloRamcoveSmlouvy`; rozšířit `ZaznamExterniOdkazEntity` o `ZaradidDoVyzvy` + rename `Vyzva` → `VyzvaId` |
| `PmTracker.Web/Data/PmTrackerDbContext.cs` | Rename `CiselnikVyzvy` → `Vyzvy`, typ `VyzvaEntity`; přidat `VyzvaHistorieStavu`; registrovat konfiguraci |
| `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs` | Smazat `ChallengeLookupEntityConfiguration` (starý mapping `CiselnikVyzvaEntity` — linie 144+) |
| `PmTracker.Web/Data/Configuration/ProjectEntityConfiguration.cs` | Přidat mapování `MistoPlneni`, `CisloRamcoveSmlouvy` |
| `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` | V konfiguraci `ZaznamExterniOdkazEntity` přidat `ZaradidDoVyzvy`, přejmenovat sloupec `vyzva` → `vyzva_id`, přidat FK na `Vyzva`, přidat filtered unique index na `cislo` WHERE `vyzva_id IS NOT NULL` |
| `PmTracker.Web/Services/ProjectService.LazyQueries.cs:301-307` | Nahradit `CiselnikVyzvaEntity` → `VyzvaEntity`, `CiselnikVyzvy` → `Vyzvy` |
| `PmTracker.Web/Services/Dictionaries/DictionaryService.Commands.cs:432` | Upravit zakládání výzvy v číselníku na nový typ (fallback cesta pro admina, zůstává funkční, ale přidat povinná nová pole) |
| `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:99` | Změnit `ciselnik_vyzvy` → `vyzvy` |
| `PmTracker.Web/Program.cs` (nebo ekvivalent DI registrace) | Zaregistrovat `TicketingReadOnlyDbContext`, `IVyzvaService`, `ITicketingQueryService` |
| `PmTracker.Web/appsettings.json` + `appsettings.Development.json` | Přidat prázdný `ConnectionStrings:TicketingReadOnly` |

---

## Task 1: Založit enum VyzvaStav

**Files:**
- Create: `PmTracker.Web/Models/Entities/VyzvaStav.cs`

- [ ] **Step 1: Vytvořit enum soubor**

```csharp
namespace PmTracker.Web.Models.Entities;

public enum VyzvaStav : byte
{
    Priprava = 1,
    Odeslano = 2,
    Zruseno = 3,
}
```

- [ ] **Step 2: Ověřit build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Models/Entities/VyzvaStav.cs
git commit -m "feat(vyzvy): add VyzvaStav enum (Priprava/Odeslano/Zruseno)"
```

---

## Task 2: Vytvořit VyzvaEntity a VyzvaHistorieStavuEntity

**Files:**
- Create: `PmTracker.Web/Models/Entities/VyzvaEntity.cs`
- Create: `PmTracker.Web/Models/Entities/VyzvaHistorieStavuEntity.cs`

- [ ] **Step 1: Vytvořit VyzvaEntity**

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

- [ ] **Step 2: Vytvořit VyzvaHistorieStavuEntity**

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
git commit -m "feat(vyzvy): add VyzvaEntity and VyzvaHistorieStavuEntity"
```

---

## Task 3: Odstranit starou CiselnikVyzvaEntity a opravit kompilaci

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:85-92` (odstranit)
- Modify: `PmTracker.Web/Services/ProjectService.LazyQueries.cs:301-307`
- Modify: `PmTracker.Web/Services/Dictionaries/DictionaryService.Commands.cs:432` a okolí

- [ ] **Step 1: Odstranit CiselnikVyzvaEntity z PmTrackerEntities.cs**

V souboru `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` najdi blok:

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

A **celý odstraň**.

- [ ] **Step 2: Opravit ProjectService.LazyQueries.cs**

V souboru `PmTracker.Web/Services/ProjectService.LazyQueries.cs` najdi blok kolem řádku 301-307:

```csharp
var vyzvaById = vyzvaIds.Length == 0
    ? new Dictionary<int, CiselnikVyzvaEntity>()
    : await dbContext.CiselnikVyzvy.AsNoTracking()
        .Where(x => vyzvaIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, ct);
```

Nahraď za:

```csharp
var vyzvaById = vyzvaIds.Length == 0
    ? new Dictionary<int, VyzvaEntity>()
    : await dbContext.Vyzvy.AsNoTracking()
        .Where(x => vyzvaIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, ct);
```

- [ ] **Step 3: Najít a opravit zbývající reference na CiselnikVyzva**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | grep -i "CiselnikVyzva" | head -20`

Každou chybu oprav. Pravděpodobné lokace:
- `PmTracker.Web/Services/Dictionaries/DictionaryService.Commands.cs` (nahradit typ a názvy DbSet, přidat snapshot pole zatím jako prázdné řetězce — proper naplnění přijde v Task 11)
- Kdekoli jinde — nahraď `CiselnikVyzvaEntity` → `VyzvaEntity`, `CiselnikVyzvy` → `Vyzvy`.

Pokud kód odkazuje na `Nazev` nebo `IsLocked` (pole zrušená ze specu): dočasně dosaď `$"Výzva {x.Kod}"` za Nazev a `x.Stav == VyzvaStav.Odeslano` za IsLocked, aby kompilace prošla; plné dočištění je součástí fáze 2 (UI).

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded, 0 Error(s). Warnings OK.

- [ ] **Step 5: Commit**

```bash
git add -A PmTracker.Web/Models/Entities/PmTrackerEntities.cs PmTracker.Web/Services/
git commit -m "refactor(vyzvy): odstranit CiselnikVyzvaEntity, nahradit VyzvaEntity v services"
```

---

## Task 4: Rozšířit ProjektEntity o MistoPlneni a CisloRamcoveSmlouvy

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:146-153`
- Modify: `PmTracker.Web/Data/Configuration/ProjectEntityConfiguration.cs`

- [ ] **Step 1: Rozšířit ProjektEntity**

V `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` najdi:

```csharp
public sealed class ProjektEntity
{
    public int Id { get; set; }
    public string Zkratka { get; set; } = string.Empty;
    public string CelyNazev { get; set; } = string.Empty;
    public int StavId { get; set; }
    public bool PouzivatIdentJednani { get; set; }
}
```

Přidej na konec třídy:

```csharp
    public string? MistoPlneni { get; set; }
    public string? CisloRamcoveSmlouvy { get; set; }
```

- [ ] **Step 2: Najít ProjectEntityConfiguration a přidat mapping**

Open `PmTracker.Web/Data/Configuration/ProjectEntityConfiguration.cs`. Najdi konfiguraci `ProjektEntity` (bloku `builder.ToTable("projekty")`). Přidej za existující `builder.Property(...)` volání:

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
git commit -m "feat(projekt): add MistoPlneni and CisloRamcoveSmlouvy to ProjektEntity"
```

---

## Task 5: Rozšířit ZaznamExterniOdkazEntity

**Files:**
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs:302-314`
- Modify: `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` (konfigurace `ZaznamExterniOdkazEntity`)

- [ ] **Step 1: Rename property Vyzva → VyzvaId a přidat ZaradidDoVyzvy**

V `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` najdi:

```csharp
public sealed class ZaznamExterniOdkazEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int TypOdkazuId { get; set; }
    public string Cislo { get; set; } = string.Empty;
    public decimal? PredpokladanaCena { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
    public int? Vyzva { get; set; }
}
```

Přejmenuj poslední pole `Vyzva` na `VyzvaId` a přidej `ZaradidDoVyzvy`:

```csharp
public sealed class ZaznamExterniOdkazEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int TypOdkazuId { get; set; }
    public string Cislo { get; set; } = string.Empty;
    public decimal? PredpokladanaCena { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
    public int? VyzvaId { get; set; }
    public bool ZaradidDoVyzvy { get; set; }
}
```

- [ ] **Step 2: Opravit reference na starý název `Vyzva`**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | grep "error CS" | head -30`

Pro každou chybu "does not contain a definition for 'Vyzva'" nahraď `.Vyzva` → `.VyzvaId` v kódu (typicky `ProjectService.LazyQueries.cs`, mappers, view models). **Nepřejmenovávej** pole `Vyzva` v ViewModelech, jen entitní přístup.

- [ ] **Step 3: Přidat mapping v RecordEntityConfiguration**

Open `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs`. Najdi konfigurační třídu pro `ZaznamExterniOdkazEntity` (bloku `builder.ToTable("zaznam_externi_odkazy")` nebo podobné). Uvnitř najdi starý mapping `builder.Property(x => x.Vyzva).HasColumnName("vyzva");` a **nahraď** za:

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
git commit -m "feat(vyzvy): rename ZaznamExterniOdkaz.Vyzva → VyzvaId, add ZaradidDoVyzvy switch"
```

---

## Task 6: Vytvořit VyzvaEntityConfiguration

**Files:**
- Create: `PmTracker.Web/Data/Configuration/VyzvaEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs` (smazat starou `ChallengeLookupEntityConfiguration`)

- [ ] **Step 1: Smazat starou ChallengeLookupEntityConfiguration**

V souboru `PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs` najdi třídu `ChallengeLookupEntityConfiguration` (okolo řádku 144, viz grep výše) a **celou ji smaž** (od `internal sealed class ChallengeLookupEntityConfiguration` až po uzavírající `}`).

- [ ] **Step 2: Vytvořit nový konfigurační soubor**

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

        builder.HasIndex(x => new { x.CisloRamcoveSmlouvySnapshot, x.Rok, x.PoradoveVRoce })
            .IsUnique()
            .HasDatabaseName("ux_vyzvy_smlouva_rok_poradove");
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
git add PmTracker.Web/Data/Configuration/VyzvaEntityConfiguration.cs PmTracker.Web/Data/Configuration/LookupEntityConfiguration.cs
git commit -m "feat(vyzvy): add VyzvaEntityConfiguration, remove legacy ChallengeLookupEntityConfiguration"
```

---

## Task 7: Registrovat DbSet pro Vyzvy a VyzvaHistorieStavu

**Files:**
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs:23` a konec třídy

- [ ] **Step 1: Upravit DbSet**

V `PmTracker.Web/Data/PmTrackerDbContext.cs` najdi řádek:

```csharp
    public DbSet<CiselnikVyzvaEntity> CiselnikVyzvy => Set<CiselnikVyzvaEntity>();
```

Nahraď za:

```csharp
    public DbSet<VyzvaEntity> Vyzvy => Set<VyzvaEntity>();
    public DbSet<VyzvaHistorieStavuEntity> VyzvaHistorieStavu => Set<VyzvaHistorieStavuEntity>();
```

- [ ] **Step 2: Ověřit, že `OnModelCreating` načítá konfigurace z assembly**

V stejném souboru (`PmTrackerDbContext.cs`) najdi metodu `OnModelCreating`. Pokud obsahuje `modelBuilder.ApplyConfigurationsFromAssembly(typeof(PmTrackerDbContext).Assembly);` — ok, nové konfigurace se načtou automaticky. Pokud tam je explicit registrace jednotlivých konfigurací, přidej:

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
git commit -m "feat(vyzvy): register Vyzvy and VyzvaHistorieStavu DbSets"
```

---

## Task 8: Vygenerovat a ověřit EF migraci

**Files:**
- Create: `PmTracker.Web/Migrations/{timestamp}_VyzvyFaze1.cs` (auto-generovaný)
- Modify: `PmTracker.Web/Migrations/PmTrackerDbContextModelSnapshot.cs` (auto-aktualizace)

- [ ] **Step 1: Vygenerovat migraci**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet ef migrations add VyzvyFaze1 \
  --project PmTracker.Web/PmTracker.Web.csproj \
  --startup-project PmTracker.Web/PmTracker.Web.csproj \
  --output-dir Migrations
```

Expected: `Build started... Build succeeded. Done.` Vytvoří se soubor `Migrations/{timestamp}_VyzvyFaze1.cs` a aktualizuje se `PmTrackerDbContextModelSnapshot.cs`.

- [ ] **Step 2: Prohlédnout generovanou migraci a ověřit, že obsahuje:**

Open `PmTracker.Web/Migrations/{timestamp}_VyzvyFaze1.cs` a zkontroluj:
- `CreateTable("vyzvy", …)` s 12 sloupci
- `CreateTable("vyzva_historie_stavu", …)` s 6 sloupci
- `AddColumn("misto_plneni", table: "projekty", …)`
- `AddColumn("cislo_ramcove_smlouvy", table: "projekty", …)`
- `RenameColumn(name: "vyzva", table: "zaznam_externi_odkazy", newName: "vyzva_id")`
- `AddColumn("zaradid_do_vyzvy", table: "zaznam_externi_odkazy", …)`
- `DropTable("ciselnik_vyzvy")` (nebo rename — EF se rozhodne; pokud to je DropTable, doplníme data-migration v Tasku 9 níže)
- `CreateIndex("ux_vyzvy_smlouva_rok_poradove", …, unique: true)`
- `CreateIndex("ux_zaznam_externi_odkazy_cislo_in_vyzve", …, unique: true, filter: "[vyzva_id] IS NOT NULL")`

Pokud něco chybí — vrať se do předchozích úkolů, doplň a migraci přegeneruj (`dotnet ef migrations remove` + znovu `add`).

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Migrations/
git commit -m "feat(vyzvy): EF migration VyzvyFaze1 (schema + rename + indexes)"
```

---

## Task 9: Napsat upgrade SQL skript

**Files:**
- Create: `db_upgrade_1_1_8_vyzvy.sql`

- [ ] **Step 1: Vytvořit skript podle vzoru existujících**

Prostuduj existující upgrade skript:

Run: `head -50 "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_1_7_new_task_status.sql"`

Vytvoř nový skript ve stejném stylu:

```sql
-- db_upgrade_1_1_8_vyzvy.sql
-- ServiceDesk Výzvy — Fáze 1: schéma
-- Spec: docs/superpowers/specs/2026-04-20-servicedesk-integrace-vyzvy-design.md

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Projekt: nová pole
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projekty') AND name = 'misto_plneni')
BEGIN
    ALTER TABLE dbo.projekty ADD misto_plneni NVARCHAR(500) NULL;
    ALTER TABLE dbo.projekty ADD cislo_ramcove_smlouvy NVARCHAR(100) NULL;
END;

-- 2. Drop starého ciselnik_vyzvy (je prázdný - ověřit)
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ciselnik_vyzvy')
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.ciselnik_vyzvy)
        THROW 50000, 'ciselnik_vyzvy contains data - manual migration required before running this script', 1;

    -- Drop FK z zaznam_externi_odkazy.vyzva -> ciselnik_vyzvy
    DECLARE @fkName SYSNAME = (
        SELECT name FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
          AND referenced_object_id = OBJECT_ID('dbo.ciselnik_vyzvy')
    );
    IF @fkName IS NOT NULL
        EXEC('ALTER TABLE dbo.zaznam_externi_odkazy DROP CONSTRAINT ' + @fkName);

    DROP TABLE dbo.ciselnik_vyzvy;
END;

-- 3. Rename zaznam_externi_odkazy.vyzva → vyzva_id + nový sloupec zaradid_do_vyzvy
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'vyzva')
    EXEC sp_rename 'dbo.zaznam_externi_odkazy.vyzva', 'vyzva_id', 'COLUMN';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy') AND name = 'zaradid_do_vyzvy')
    ALTER TABLE dbo.zaznam_externi_odkazy ADD zaradid_do_vyzvy BIT NOT NULL CONSTRAINT df_zaznam_externi_odkazy_zaradid DEFAULT 0;

-- 4. Create vyzvy
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
END;

-- 5. FK z zaznam_externi_odkazy.vyzva_id → vyzvy.id (po vytvoření vyzvy)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_zaznam_externi_odkazy_vyzva')
    ALTER TABLE dbo.zaznam_externi_odkazy
        ADD CONSTRAINT fk_zaznam_externi_odkazy_vyzva
        FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE SET NULL;

-- 6. Filtered unique index: PNF nemůže být ve dvou výzvách napříč projekty
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ux_zaznam_externi_odkazy_cislo_in_vyzve')
    CREATE UNIQUE INDEX ux_zaznam_externi_odkazy_cislo_in_vyzve
        ON dbo.zaznam_externi_odkazy (cislo)
        WHERE vyzva_id IS NOT NULL;

-- 7. Audit tabulka vyzva_historie_stavu
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'vyzva_historie_stavu')
BEGIN
    CREATE TABLE dbo.vyzva_historie_stavu (
        id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        vyzva_id INT NOT NULL,
        puvodni_stav TINYINT NULL,
        novy_stav TINYINT NOT NULL,
        datum_zmeny DATETIME2 NOT NULL,
        zmenil_osoba_id INT NOT NULL,
        CONSTRAINT fk_vyzva_historie_stavu_vyzva FOREIGN KEY (vyzva_id) REFERENCES dbo.vyzvy(id) ON DELETE CASCADE
    );

    CREATE INDEX ix_vyzva_historie_stavu_vyzva_id ON dbo.vyzva_historie_stavu (vyzva_id);
END;

COMMIT TRANSACTION;
```

- [ ] **Step 2: Commit**

```bash
git add db_upgrade_1_1_8_vyzvy.sql
git commit -m "feat(vyzvy): db_upgrade_1_1_8 SQL script for Vyzvy schema"
```

---

## Task 10: Aktualizovat SeedBaselineDocumentationTests

**Files:**
- Modify: `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:99`

- [ ] **Step 1: Nahradit ciselnik_vyzvy → vyzvy**

V `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs` na řádku 99 najdi:

```csharp
        document.Should().Contain("dbo.ciselnik_vyzvy");
```

Nahraď za:

```csharp
        document.Should().Contain("dbo.vyzvy");
        document.Should().Contain("dbo.vyzva_historie_stavu");
```

- [ ] **Step 2: Zkontrolovat, zda existuje baseline dokumentace (co test ověřuje)**

Run: `grep -l "ciselnik_vyzvy\|dbo.vyzvy" docs/ -r 2>/dev/null || true`

Pokud baseline doc existuje (pravděpodobně markdown v `docs/`), aktualizuj jej podle co test vyžaduje — nahraď `dbo.ciselnik_vyzvy` → `dbo.vyzvy` a doplň `dbo.vyzva_historie_stavu`.

- [ ] **Step 3: Run test**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SeedBaselineDocumentation" --no-restore`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs docs/
git commit -m "test(vyzvy): update baseline documentation expectation (vyzvy + vyzva_historie_stavu)"
```

---

## Task 11: Vytvořit IVyzvaService interface a DTO kontrakty

**Files:**
- Create: `PmTracker.Web/Services/Vyzvy/IVyzvaService.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs`
- Create: `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaBufferItem.cs`
- Create: `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaDetail.cs`

- [ ] **Step 1: VyzvaErrors**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs
namespace PmTracker.Web.Services.Vyzvy;

public enum VyzvaErrorCode
{
    None = 0,
    ProjectMissingMistoPlneni = 1,
    ProjectMissingCisloRamcoveSmlouvy = 2,
    BufferEmpty = 3,
    InvalidStateTransition = 4,
    VyzvaNotFound = 5,
    ExternalLinkNotFound = 6,
    ExternalLinkNotPnf = 7,
    VyzvaIsLocked = 8,
    PnfAlreadyInAnotherVyzva = 9,
}

public sealed record VyzvaError(VyzvaErrorCode Code, string Message);
```

- [ ] **Step 2: Contract DTOs**

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

- [ ] **Step 3: IVyzvaService interface**

```csharp
// PmTracker.Web/Services/Vyzvy/IVyzvaService.cs
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public interface IVyzvaService
{
    Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct);

    Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct);

    Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct);

    Task<Result<VyzvaDetail, VyzvaError>> ZaloztVyzvuZBufferuAsync(int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct);

    Task<Result<VyzvaDetail, VyzvaError>> ZmenitStavAsync(int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct);

    Task<Result<Unit, VyzvaError>> NastavitZaradidAsync(int externiOdkazId, bool zaradit, CancellationToken ct);

    Task<Result<Unit, VyzvaError>> PrerditPnfAsync(int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct);
}

public readonly record struct Unit;

public abstract record Result<TValue, TError>
{
    public sealed record Ok(TValue Value) : Result<TValue, TError>;
    public sealed record Fail(TError Error) : Result<TValue, TError>;
}
```

*(Poznámka: Pokud v projektu už existuje helper Result type, použij ho místo tohoto — zkontroluj `grep -r "public.*record.*Result" PmTracker.Web/Services/` před commitem. Default: viz výše.)*

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/
git commit -m "feat(vyzvy): add IVyzvaService interface, DTOs, and error codes"
```

---

## Task 12: Test-first — generátor kódu výzvy

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs`

- [ ] **Step 1: Napsat failing test pro generátor**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs
using FluentAssertions;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public class VyzvaCodeGeneratorTests
{
    [Fact]
    public void GenerujKod_FormatujeJakoPoradoveLomitkoRok()
    {
        var kod = VyzvaCodeGenerator.Generuj(poradoveVRoce: 2, rok: 2026);
        kod.Should().Be("2/2026");
    }

    [Fact]
    public void GenerujDalsiPoradove_PrazdnySeznam_Vraci1()
    {
        var dalsi = VyzvaCodeGenerator.DalsiPoradoveVRoce(existujiciPoradove: Array.Empty<int>());
        dalsi.Should().Be(1);
    }

    [Fact]
    public void GenerujDalsiPoradove_NejvyssiPlus1()
    {
        var dalsi = VyzvaCodeGenerator.DalsiPoradoveVRoce(existujiciPoradove: new[] { 1, 2, 5 });
        dalsi.Should().Be(6);
    }
}
```

- [ ] **Step 2: Run test — musí selhat (třída neexistuje)**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaCodeGenerator" --no-restore`
Expected: BUILD FAIL "The name 'VyzvaCodeGenerator' does not exist"

- [ ] **Step 3: Implementovat VyzvaCodeGenerator**

Create `PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs`:

```csharp
namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvaCodeGenerator
{
    public static string Generuj(int poradoveVRoce, int rok)
        => $"{poradoveVRoce}/{rok}";

    public static int DalsiPoradoveVRoce(IReadOnlyCollection<int> existujiciPoradove)
        => existujiciPoradove.Count == 0 ? 1 : existujiciPoradove.Max() + 1;
}
```

- [ ] **Step 4: Run test — musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaCodeGenerator" --no-restore`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs
git commit -m "feat(vyzvy): VyzvaCodeGenerator (formát N/YYYY, další pořadové)"
```

---

## Task 13: Test-first — validátor stavových přechodů

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceStateTransitionsTests.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaStateMachine.cs`

- [ ] **Step 1: Napsat failing test**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceStateTransitionsTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public class VyzvaServiceStateTransitionsTests
{
    [Theory]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Odeslano, true)]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Zruseno, true)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Priprava, true)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Zruseno, true)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Priprava, true)]
    public void JePovolenyPrechod_PovoleneKombinace_True(VyzvaStav z, VyzvaStav na, bool ocekavano)
    {
        VyzvaStateMachine.JePovolenyPrechod(z, na).Should().Be(ocekavano);
    }

    [Theory]
    [InlineData(VyzvaStav.Priprava, VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Odeslano)]
    [InlineData(VyzvaStav.Zruseno, VyzvaStav.Zruseno)]
    public void JePovolenyPrechod_NepovoleneKombinace_False(VyzvaStav z, VyzvaStav na)
    {
        VyzvaStateMachine.JePovolenyPrechod(z, na).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceStateTransitions" --no-restore`
Expected: BUILD FAIL "VyzvaStateMachine does not exist"

- [ ] **Step 3: Implementovat**

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

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceStateTransitions" --no-restore`
Expected: 9 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaStateMachine.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceStateTransitionsTests.cs
git commit -m "feat(vyzvy): VyzvaStateMachine + tests for allowed/forbidden transitions"
```

---

## Task 14: VyzvaService — skelet s DbContext, DI registrace

**Files:**
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`
- Modify: `PmTracker.Web/Program.cs` (přidat DI registraci)

- [ ] **Step 1: Skelet služby**

```csharp
// PmTracker.Web/Services/Vyzvy/VyzvaService.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed class VyzvaService : IVyzvaService
{
    private readonly PmTrackerDbContext _db;

    public VyzvaService(PmTrackerDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
    {
        var pnfTypId = await _db.CiselnikTypuExternichOdkazu
            .Where(t => t.Kod == "PNF")
            .Select(t => t.Id)
            .FirstAsync(ct);

        return await (
            from ev in _db.ZaznamExterniOdkazy.AsNoTracking()
            join z in _db.ProjektoveZaznamy.AsNoTracking() on ev.ZaznamId equals z.Id
            where z.ProjektId == projektId
                && ev.TypOdkazuId == pnfTypId
                && ev.ZaradidDoVyzvy
                && ev.VyzvaId == null
            select new VyzvaBufferItem(ev.Id, ev.ZaznamId, ev.Cislo, null, ev.PredpokladanaCena)
        ).ToListAsync(ct);
    }

    public Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
        => throw new NotImplementedException("Task 15");

    public Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
        => throw new NotImplementedException("Task 15");

    public Task<Result<VyzvaDetail, VyzvaError>> ZaloztVyzvuZBufferuAsync(int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct)
        => throw new NotImplementedException("Task 16");

    public Task<Result<VyzvaDetail, VyzvaError>> ZmenitStavAsync(int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
        => throw new NotImplementedException("Task 17");

    public Task<Result<Unit, VyzvaError>> NastavitZaradidAsync(int externiOdkazId, bool zaradit, CancellationToken ct)
        => throw new NotImplementedException("Task 18");

    public Task<Result<Unit, VyzvaError>> PrerditPnfAsync(int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
        => throw new NotImplementedException("Task 19");
}
```

*(Název `ProjektoveZaznamy` — ověř si přesný název DbSet v `PmTrackerDbContext.cs`. Pokud je jiný, použij ten.)*

- [ ] **Step 2: DI registrace**

Otevři `PmTracker.Web/Program.cs`. Najdi místo, kde se registrují ostatní services (pravděpodobně blok `builder.Services.AddScoped<IProjectService, ProjectService>();` nebo podobný). Přidej:

```csharp
builder.Services.AddScoped<IVyzvaService, VyzvaService>();
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Web/Program.cs
git commit -m "feat(vyzvy): VyzvaService skeleton with GetBufferAsync + DI registration"
```

---

## Task 15: Test + implementace GetVyzvyAsync a GetVyzvaAsync

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceQueryTests.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`
- Create (helper): `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTestHarness.cs`

- [ ] **Step 1: Test harness (in-memory DbContext)**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTestHarness.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;

namespace PmTracker.Tests.Unit.Vyzvy;

internal static class VyzvaServiceTestHarness
{
    public static PmTrackerDbContext CreateDb(string name = "")
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(string.IsNullOrEmpty(name) ? Guid.NewGuid().ToString() : name)
            .Options;
        return new PmTrackerDbContext(opts);
    }

    public static VyzvaService CreateService(PmTrackerDbContext db) => new(db);

    public static async Task SeedProjektAsync(PmTrackerDbContext db, int projektId = 1)
    {
        if (!await db.Projekty.AnyAsync(p => p.Id == projektId))
        {
            db.Projekty.Add(new ProjektEntity
            {
                Id = projektId,
                Zkratka = "P1",
                CelyNazev = "Projekt 1",
                StavId = 1,
                MistoPlneni = "FIS (EIS): VZ 8201",
                CisloRamcoveSmlouvy = "23106000271",
            });
            await db.SaveChangesAsync();
        }
    }

    public static async Task SeedPnfTypAsync(PmTrackerDbContext db)
    {
        if (!await db.CiselnikTypuExternichOdkazu.AnyAsync(t => t.Kod == "PNF"))
        {
            db.CiselnikTypuExternichOdkazu.Add(new CiselnikTypuExternichOdkazuEntity
            {
                Id = 1, Kod = "PNF", Nazev = "PNF",
            });
            await db.SaveChangesAsync();
        }
    }
}
```

*(Názvy DbSet `Projekty`, `CiselnikTypuExternichOdkazu` — ověř proti skutečnému `PmTrackerDbContext`.)*

- [ ] **Step 2: Napsat failing test**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceQueryTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public class VyzvaServiceQueryTests
{
    [Fact]
    public async Task GetVyzvyAsync_VraciVyzvyProjektu_SetridenoDatumZalozeniDesc()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);

        db.Vyzvy.AddRange(
            new VyzvaEntity
            {
                Id = 10, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 1, 5),
                ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "FIS", CisloRamcoveSmlouvySnapshot = "23106000271",
            },
            new VyzvaEntity
            {
                Id = 11, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2026, 1, 10),
                ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "FIS", CisloRamcoveSmlouvySnapshot = "23106000271",
            });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);

        var result = await svc.GetVyzvyAsync(1, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Kod.Should().Be("2/2026"); // novější první
        result[1].Kod.Should().Be("1/2026");
    }

    [Fact]
    public async Task GetVyzvaAsync_NeexistujiciId_VraciNull()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        var svc = VyzvaServiceTestHarness.CreateService(db);

        var result = await svc.GetVyzvaAsync(999, CancellationToken.None);

        result.Should().BeNull();
    }
}
```

- [ ] **Step 3: Run — fail (NotImplementedException)**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceQueryTests" --no-restore`
Expected: FAIL "The method or operation is not implemented"

- [ ] **Step 4: Implementovat GetVyzvyAsync a GetVyzvaAsync**

V `PmTracker.Web/Services/Vyzvy/VyzvaService.cs` nahraď `GetVyzvyAsync` a `GetVyzvaAsync`:

```csharp
    public async Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
    {
        var vyzvy = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId)
            .OrderByDescending(v => v.DatumZalozeni)
            .ToListAsync(ct);

        var vyzvaIds = vyzvy.Select(v => v.Id).ToList();
        var polozkyMap = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId.HasValue && vyzvaIds.Contains(ev.VyzvaId.Value))
            .OrderBy(ev => ev.Id)
            .GroupBy(ev => ev.VyzvaId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct);

        return vyzvy.Select(v => MapToDetail(v, polozkyMap.GetValueOrDefault(v.Id) ?? new())).ToList();
    }

    public async Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null) return null;

        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId == vyzvaId)
            .OrderBy(ev => ev.Id)
            .ToListAsync(ct);

        return MapToDetail(vyzva, polozky);
    }

    private static VyzvaDetail MapToDetail(VyzvaEntity v, List<ZaznamExterniOdkazEntity> polozky)
        => new(
            v.Id, v.ProjektId, v.Kod, v.Rok, v.PoradoveVRoce, v.Stav,
            v.DatumZalozeni, v.ZalozilOsobaId, v.DatumOdeslani, v.OdeslalOsobaId,
            v.MistoPlneniSnapshot, v.CisloRamcoveSmlouvySnapshot,
            polozky.Select(ev => new VyzvaDetailItem(
                ev.Id, ev.ZaznamId, ev.Cislo, null, ev.PredpokladanaCena)).ToList());
```

- [ ] **Step 5: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceQueryTests" --no-restore`
Expected: 2 passed

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/
git commit -m "feat(vyzvy): GetVyzvyAsync + GetVyzvaAsync with DTO mapping"
```

---

## Task 16: Test + implementace ZaloztVyzvuZBufferuAsync

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`

- [ ] **Step 1: Failing test**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public class VyzvaServiceFoundingTests
{
    [Fact]
    public async Task ZaloztVyzvuZBufferu_PrazdnyBuffer_VraciBufferEmpty()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, zalozilOsobaId: 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<Result<VyzvaDetail, VyzvaError>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.BufferEmpty);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_ProjektBezMistaPlneni_VraciError()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity { Id = 1, Zkratka = "P1", CelyNazev = "P1", StavId = 1, MistoPlneni = null, CisloRamcoveSmlouvy = "23106000271" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<Result<VyzvaDetail, VyzvaError>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ProjectMissingMistoPlneni);
    }

    [Fact]
    public async Task ZaloztVyzvuZBufferu_ValidniBuffer_VytvoriVyzvu_PriradiPolozky_VraciOk()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 100, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1,
            CisloViditelne = "RU100-1", Nazev = "z", VlastnikId = 1,
            DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1,
            HarmonogramSablonaVerze = 1,
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 500, ZaznamId = 100, TypOdkazuId = 1, Cislo = "336865",
            ZaradidDoVyzvy = true, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var now = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);

        var result = await svc.ZaloztVyzvuZBufferuAsync(1, 7, now, CancellationToken.None);

        var ok = result.Should().BeOfType<Result<VyzvaDetail, VyzvaError>.Ok>().Which.Value;
        ok.Kod.Should().Be("1/2026");
        ok.PoradoveVRoce.Should().Be(1);
        ok.Rok.Should().Be(2026);
        ok.Stav.Should().Be(VyzvaStav.Priprava);
        ok.ZalozilOsobaId.Should().Be(7);
        ok.MistoPlneniSnapshot.Should().Be("FIS (EIS): VZ 8201");
        ok.CisloRamcoveSmlouvySnapshot.Should().Be("23106000271");
        ok.Polozky.Should().HaveCount(1);

        var odkaz = await db.ZaznamExterniOdkazy.FindAsync(500);
        odkaz!.VyzvaId.Should().Be(ok.Id);
        odkaz.ZaradidDoVyzvy.Should().BeTrue(); // zařazeno do výzvy, switch zůstává true

        var historie = db.VyzvaHistorieStavu.ToList();
        historie.Should().ContainSingle(h => h.VyzvaId == ok.Id && h.PuvodniStav == null && h.NovyStav == VyzvaStav.Priprava);
    }
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceFoundingTests" --no-restore`
Expected: FAIL (NotImplementedException)

- [ ] **Step 3: Implementovat ZaloztVyzvuZBufferuAsync**

Nahraď metodu v `VyzvaService.cs`:

```csharp
    public async Task<Result<VyzvaDetail, VyzvaError>> ZaloztVyzvuZBufferuAsync(
        int projektId, int zalozilOsobaId, DateTime now, CancellationToken ct)
    {
        var projekt = await _db.Projekty.FirstOrDefaultAsync(p => p.Id == projektId, ct);
        if (projekt == null)
            return new Result<VyzvaDetail, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.VyzvaNotFound, "Projekt nenalezen"));
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return new Result<VyzvaDetail, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.ProjectMissingMistoPlneni, "Projekt nemá vyplněné místo plnění"));
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return new Result<VyzvaDetail, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.ProjectMissingCisloRamcoveSmlouvy, "Projekt nemá vyplněné číslo rámcové smlouvy"));

        var buffer = await GetBufferAsync(projektId, ct);
        if (buffer.Count == 0)
            return new Result<VyzvaDetail, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.BufferEmpty, "Buffer projektu je prázdný"));

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

        var bufferIds = buffer.Select(b => b.ExterniOdkazId).ToList();
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
        return new Result<VyzvaDetail, VyzvaError>.Ok(detail!);
    }
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceFoundingTests" --no-restore`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs
git commit -m "feat(vyzvy): ZaloztVyzvuZBufferuAsync + audit log, TDD"
```

---

## Task 17: Test + implementace ZmenitStavAsync

**Files:**
- Modify: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceStateTransitionsTests.cs` (přidat integrační testy)
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`

- [ ] **Step 1: Failing testy na chování služby**

Přidej do `VyzvaServiceStateTransitionsTests.cs`:

```csharp
    [Fact]
    public async Task ZmenitStavAsync_PripravaNaOdeslano_ZapiseDatumAOdeslalAHistorii()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 50, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 4, 1),
            ZalozilOsobaId = 1, MistoPlneniSnapshot = "FIS", CisloRamcoveSmlouvySnapshot = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var now = new DateTime(2026, 4, 20);
        var result = await svc.ZmenitStavAsync(50, VyzvaStav.Odeslano, zmenilOsobaId: 7, now, CancellationToken.None);

        result.Should().BeOfType<Result<VyzvaDetail, VyzvaError>.Ok>();
        var v = await db.Vyzvy.FindAsync(50);
        v!.Stav.Should().Be(VyzvaStav.Odeslano);
        v.DatumOdeslani.Should().Be(now);
        v.OdeslalOsobaId.Should().Be(7);
        db.VyzvaHistorieStavu.Should().ContainSingle(h => h.VyzvaId == 50 && h.NovyStav == VyzvaStav.Odeslano);
    }

    [Fact]
    public async Task ZmenitStavAsync_NepovolenyPrechod_VraciError()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 51, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Zruseno, DatumZalozeni = DateTime.UtcNow,
            ZalozilOsobaId = 1, MistoPlneniSnapshot = "FIS", CisloRamcoveSmlouvySnapshot = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZmenitStavAsync(51, VyzvaStav.Odeslano, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<Result<VyzvaDetail, VyzvaError>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.InvalidStateTransition);
    }

    [Fact]
    public async Task ZmenitStavAsync_ZPripravaDoZruseno_ZasePNFDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 52, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow,
            ZalozilOsobaId = 1, MistoPlneniSnapshot = "FIS", CisloRamcoveSmlouvySnapshot = "A",
        });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 200, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x",
            Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow,
            SubsystemId = 1, HarmonogramSablonaVerze = 1,
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 900, ZaznamId = 200, TypOdkazuId = 1, Cislo = "111111",
            ZaradidDoVyzvy = true, VyzvaId = 52,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZmenitStavAsync(52, VyzvaStav.Zruseno, 7, DateTime.UtcNow, CancellationToken.None);

        result.Should().BeOfType<Result<VyzvaDetail, VyzvaError>.Ok>();
        var odkaz = await db.ZaznamExterniOdkazy.FindAsync(900);
        odkaz!.VyzvaId.Should().BeNull();
        odkaz.ZaradidDoVyzvy.Should().BeTrue(); // vrací se do bufferu, switch zůstává
    }
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceStateTransitionsTests" --no-restore`
Expected: FAIL (NotImplementedException u asynchronních testů)

- [ ] **Step 3: Implementovat ZmenitStavAsync**

V `VyzvaService.cs` nahraď:

```csharp
    public async Task<Result<VyzvaDetail, VyzvaError>> ZmenitStavAsync(
        int vyzvaId, VyzvaStav novyStav, int zmenilOsobaId, DateTime now, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null)
            return new Result<VyzvaDetail, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.VyzvaNotFound, "Výzva nenalezena"));

        var puvodni = vyzva.Stav;
        if (!VyzvaStateMachine.JePovolenyPrechod(puvodni, novyStav))
            return new Result<VyzvaDetail, VyzvaError>.Fail(
                new VyzvaError(VyzvaErrorCode.InvalidStateTransition, $"Přechod {puvodni} → {novyStav} není povolen"));

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
        return new Result<VyzvaDetail, VyzvaError>.Ok(detail!);
    }
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceStateTransitionsTests" --no-restore`
Expected: 12 passed (9 theory + 3 nové async)

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceStateTransitionsTests.cs
git commit -m "feat(vyzvy): ZmenitStavAsync + audit + PNF vrací do bufferu při Zruseno"
```

---

## Task 18: Test + implementace NastavitZaradidAsync

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceBufferTests.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`

- [ ] **Step 1: Failing tests**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceBufferTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public class VyzvaServiceBufferTests
{
    [Fact]
    public async Task NastavitZaradid_NaPnfBezPripravaVyzvy_DaDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 1, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 10, ZaznamId = 1, TypOdkazuId = 1, Cislo = "222222", ZaradidDoVyzvy = false });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(10, true, CancellationToken.None);

        result.Should().BeOfType<Result<Unit, VyzvaError>.Ok>();
        var odkaz = await db.ZaznamExterniOdkazy.FindAsync(10);
        odkaz!.ZaradidDoVyzvy.Should().BeTrue();
        odkaz.VyzvaId.Should().BeNull();
    }

    [Fact]
    public async Task NastavitZaradid_NaPnfSPripravaVyzvou_ZariadiDoTeVyzvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity { Id = 30, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 2, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 11, ZaznamId = 2, TypOdkazuId = 1, Cislo = "333333", ZaradidDoVyzvy = false });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(11, true, CancellationToken.None);

        var odkaz = await db.ZaznamExterniOdkazy.FindAsync(11);
        odkaz!.ZaradidDoVyzvy.Should().BeTrue();
        odkaz.VyzvaId.Should().Be(30);
    }

    [Fact]
    public async Task NastavitZaradid_NaPnfSDvemaPripravaVyzvami_DaDoNejnizsihoPoradove()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.AddRange(
            new VyzvaEntity { Id = 40, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" },
            new VyzvaEntity { Id = 41, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 3, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 12, ZaznamId = 3, TypOdkazuId = 1, Cislo = "444444", ZaradidDoVyzvy = false });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(12, true, CancellationToken.None);

        var odkaz = await db.ZaznamExterniOdkazy.FindAsync(12);
        odkaz!.VyzvaId.Should().Be(41); // nejnižší PoradoveVRoce
    }

    [Fact]
    public async Task NastavitZaradid_OffNaPnfVePripravaVyzve_OdebereZVyzvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity { Id = 60, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 5, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 13, ZaznamId = 5, TypOdkazuId = 1, Cislo = "555555", ZaradidDoVyzvy = true, VyzvaId = 60 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.NastavitZaradidAsync(13, false, CancellationToken.None);

        var odkaz = await db.ZaznamExterniOdkazy.FindAsync(13);
        odkaz!.ZaradidDoVyzvy.Should().BeFalse();
        odkaz.VyzvaId.Should().BeNull();
    }

    [Fact]
    public async Task NastavitZaradid_OffNaPnfVeOdeslanoVyzve_VraciLocked()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity { Id = 70, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 6, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 14, ZaznamId = 6, TypOdkazuId = 1, Cislo = "666666", ZaradidDoVyzvy = true, VyzvaId = 70 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(14, false, CancellationToken.None);

        result.Should().BeOfType<Result<Unit, VyzvaError>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.VyzvaIsLocked);
    }

    [Fact]
    public async Task NastavitZaradid_NaNonPnfTyp_VraciError()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.CiselnikTypuExternichOdkazu.AddRange(
            new CiselnikTypuExternichOdkazuEntity { Id = 1, Kod = "PNF", Nazev = "PNF" },
            new CiselnikTypuExternichOdkazuEntity { Id = 2, Kod = "NES", Nazev = "NES" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 7, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 15, ZaznamId = 7, TypOdkazuId = 2, Cislo = "777777" });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(15, true, CancellationToken.None);

        result.Should().BeOfType<Result<Unit, VyzvaError>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ExternalLinkNotPnf);
    }
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceBufferTests" --no-restore`
Expected: FAIL

- [ ] **Step 3: Implementovat NastavitZaradidAsync**

V `VyzvaService.cs` nahraď:

```csharp
    public async Task<Result<Unit, VyzvaError>> NastavitZaradidAsync(int externiOdkazId, bool zaradit, CancellationToken ct)
    {
        var odkaz = await _db.ZaznamExterniOdkazy
            .FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena"));

        var pnfTypId = await _db.CiselnikTypuExternichOdkazu
            .Where(t => t.Kod == "PNF").Select(t => t.Id).FirstAsync(ct);
        if (odkaz.TypOdkazuId != pnfTypId)
            return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.ExternalLinkNotPnf, "Switch lze použít jen pro PNF"));

        if (!zaradit && odkaz.VyzvaId.HasValue)
        {
            var vyzva = await _db.Vyzvy.FindAsync(new object[] { odkaz.VyzvaId.Value }, ct);
            if (vyzva != null && vyzva.Stav == VyzvaStav.Odeslano)
                return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.VyzvaIsLocked, "Výzva je odeslaná"));
        }

        odkaz.ZaradidDoVyzvy = zaradit;

        if (zaradit && odkaz.VyzvaId == null)
        {
            // najdi první Priprava výzvu projektu
            var projektId = await _db.ProjektoveZaznamy
                .Where(z => z.Id == odkaz.ZaznamId)
                .Select(z => z.ProjektId)
                .FirstAsync(ct);

            var cilovaVyzva = await _db.Vyzvy.AsNoTracking()
                .Where(v => v.ProjektId == projektId && v.Stav == VyzvaStav.Priprava)
                .OrderBy(v => v.PoradoveVRoce)
                .Select(v => v.Id)
                .FirstOrDefaultAsync(ct);

            if (cilovaVyzva > 0) odkaz.VyzvaId = cilovaVyzva;
        }
        else if (!zaradit && odkaz.VyzvaId.HasValue)
        {
            odkaz.VyzvaId = null;
        }

        await _db.SaveChangesAsync(ct);
        return new Result<Unit, VyzvaError>.Ok(default);
    }
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceBufferTests" --no-restore`
Expected: 6 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceBufferTests.cs
git commit -m "feat(vyzvy): NastavitZaradidAsync — buffer, auto-assign to Priprava, lock guard"
```

---

## Task 19: Test + implementace PrerditPnfAsync (pro drag & drop)

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceReassignmentTests.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`

- [ ] **Step 1: Failing tests**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceReassignmentTests.cs
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public class VyzvaServiceReassignmentTests
{
    [Fact]
    public async Task PrerditPnf_ZBufferuDoPripravaVyzvy_Ok()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity { Id = 80, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 9, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 20, ZaznamId = 9, TypOdkazuId = 1, Cislo = "888888", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.PrerditPnfAsync(20, cilovaVyzvaId: 80, CancellationToken.None);

        result.Should().BeOfType<Result<Unit, VyzvaError>.Ok>();
        (await db.ZaznamExterniOdkazy.FindAsync(20))!.VyzvaId.Should().Be(80);
    }

    [Fact]
    public async Task PrerditPnf_DoNull_VratiDoBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity { Id = 90, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Priprava, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 10, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 21, ZaznamId = 10, TypOdkazuId = 1, Cislo = "999999", ZaradidDoVyzvy = true, VyzvaId = 90 });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.PrerditPnfAsync(21, null, CancellationToken.None);

        var o = await db.ZaznamExterniOdkazy.FindAsync(21);
        o!.VyzvaId.Should().BeNull();
        o.ZaradidDoVyzvy.Should().BeTrue();
    }

    [Fact]
    public async Task PrerditPnf_DoOdeslaneVyzvy_VraciLocked()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity { Id = 100, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026, Stav = VyzvaStav.Odeslano, DatumZalozeni = DateTime.UtcNow, ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A" });
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 11, ProjektId = 1, KategorieId = 1, CisloZaznamu = 1, CisloViditelne = "x", Nazev = "z", VlastnikId = 1, DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow, SubsystemId = 1, HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 22, ZaznamId = 11, TypOdkazuId = 1, Cislo = "101010", ZaradidDoVyzvy = true, VyzvaId = null });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.PrerditPnfAsync(22, 100, CancellationToken.None);

        result.Should().BeOfType<Result<Unit, VyzvaError>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.VyzvaIsLocked);
    }
}
```

- [ ] **Step 2: Run — fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceReassignmentTests" --no-restore`
Expected: FAIL

- [ ] **Step 3: Implementovat**

V `VyzvaService.cs` nahraď:

```csharp
    public async Task<Result<Unit, VyzvaError>> PrerditPnfAsync(int externiOdkazId, int? cilovaVyzvaId, CancellationToken ct)
    {
        var odkaz = await _db.ZaznamExterniOdkazy.FirstOrDefaultAsync(ev => ev.Id == externiOdkazId, ct);
        if (odkaz == null)
            return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.ExternalLinkNotFound, "Externí vazba nenalezena"));

        var pnfTypId = await _db.CiselnikTypuExternichOdkazu.Where(t => t.Kod == "PNF").Select(t => t.Id).FirstAsync(ct);
        if (odkaz.TypOdkazuId != pnfTypId)
            return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.ExternalLinkNotPnf, "Jen PNF"));

        if (odkaz.VyzvaId.HasValue)
        {
            var source = await _db.Vyzvy.FindAsync(new object[] { odkaz.VyzvaId.Value }, ct);
            if (source != null && source.Stav == VyzvaStav.Odeslano)
                return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.VyzvaIsLocked, "Zdrojová výzva je odeslaná"));
        }

        if (cilovaVyzvaId.HasValue)
        {
            var cil = await _db.Vyzvy.FindAsync(new object[] { cilovaVyzvaId.Value }, ct);
            if (cil == null)
                return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.VyzvaNotFound, "Cílová výzva neexistuje"));
            if (cil.Stav != VyzvaStav.Priprava)
                return new Result<Unit, VyzvaError>.Fail(new VyzvaError(VyzvaErrorCode.VyzvaIsLocked, "Cílová výzva není v Priprava"));
        }

        odkaz.VyzvaId = cilovaVyzvaId;
        if (cilovaVyzvaId.HasValue || odkaz.ZaradidDoVyzvy == false)
        {
            // když vracíme do bufferu, switch zůstává ON (toto je explicitní přeřazení)
            odkaz.ZaradidDoVyzvy = true;
        }

        await _db.SaveChangesAsync(ct);
        return new Result<Unit, VyzvaError>.Ok(default);
    }
```

- [ ] **Step 4: Run — pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvaServiceReassignmentTests" --no-restore`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceReassignmentTests.cs
git commit -m "feat(vyzvy): PrerditPnfAsync pro DnD přeřazení mezi bufferem a Priprava výzvami"
```

---

## Task 20: TicketingReadOnlyDbContext + HOT entity

**Files:**
- Create: `PmTracker.Web/Services/Ticketing/Entities/HotZaznamEntity.cs`
- Create: `PmTracker.Web/Services/Ticketing/Entities/HotKalkulaceEntity.cs`
- Create: `PmTracker.Web/Services/Ticketing/Entities/HotPidEntity.cs`
- Create: `PmTracker.Web/Services/Ticketing/TicketingReadOnlyDbContext.cs`
- Modify: `PmTracker.Web/appsettings.json`, `appsettings.Development.json`
- Modify: `PmTracker.Web/Program.cs`

- [ ] **Step 1: HOT entity**

```csharp
// PmTracker.Web/Services/Ticketing/Entities/HotZaznamEntity.cs
namespace PmTracker.Web.Services.Ticketing.Entities;

public sealed class HotZaznamEntity
{
    public long Radek { get; set; }
    public string Id { get; set; } = string.Empty;  // 6-místné
    public string? TypZaznamu { get; set; }         // PMP/PNF/NES
    public string? Strucne { get; set; }
    public string? Popis { get; set; }
    public int? Stav { get; set; }
    public int? Splneno { get; set; }
    public DateTime? SlaDeadline { get; set; }
}
```

```csharp
// PmTracker.Web/Services/Ticketing/Entities/HotKalkulaceEntity.cs
namespace PmTracker.Web.Services.Ticketing.Entities;

public sealed class HotKalkulaceEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }              // vazba na HOT_ZAZNAMY.id (nebo HOT_PID — podle reálu)
    public int? IdKalk { get; set; }
    public int? Verze { get; set; }
    public string? Akceptace { get; set; }        // 'Akceptováno' filtr
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

```csharp
// PmTracker.Web/Services/Ticketing/Entities/HotPidEntity.cs
namespace PmTracker.Web.Services.Ticketing.Entities;

public sealed class HotPidEntity
{
    public long Id { get; set; }
    public string? Pid { get; set; }
    public int? IdxPou { get; set; }
}
```

- [ ] **Step 2: Read-only DbContext**

```csharp
// PmTracker.Web/Services/Ticketing/TicketingReadOnlyDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PmTracker.Web.Services.Ticketing.Entities;

namespace PmTracker.Web.Services.Ticketing;

public sealed class TicketingReadOnlyDbContext : DbContext
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
        => throw new InvalidOperationException("TicketingReadOnlyDbContext is read-only");

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        => throw new InvalidOperationException("TicketingReadOnlyDbContext is read-only");

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

- [ ] **Step 3: appsettings**

Do `PmTracker.Web/appsettings.json` do bloku `ConnectionStrings` přidej:

```json
"TicketingReadOnly": ""
```

Do `PmTracker.Web/appsettings.Development.json` (pokud existuje) přidej placeholder se zakomentovanou hodnotou — pro dev tests necháme prázdný:

```json
"TicketingReadOnly": ""
```

Pokud `TicketingReadOnly` je prázdný string, DbContext se zaregistruje, ale jakýkoli dotaz skončí runtime chybou — to je OK pro lokální dev, protože feature je za feature flagem / podmíněna existujícím connection stringem. (Tato logika se řeší v Programu.cs.)

- [ ] **Step 4: DI registrace**

V `Program.cs` přidej vedle jiných `AddDbContext`:

```csharp
var ticketingConn = builder.Configuration.GetConnectionString("TicketingReadOnly");
builder.Services.AddDbContext<TicketingReadOnlyDbContext>(opts =>
{
    if (!string.IsNullOrWhiteSpace(ticketingConn))
        opts.UseSqlServer(ticketingConn);
    else
        opts.UseInMemoryDatabase("ticketing-disabled");
});
```

*(InMemory fallback je pragmatický, aby aplikace nastartovala bez connection stringu; skutečné dotazy přes Ticketing službu ověří prázdnost stringu a vrátí chybu „feature disabled". Tato logika přijde v Tasku 21.)*

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Ticketing/ PmTracker.Web/Program.cs PmTracker.Web/appsettings*.json
git commit -m "feat(ticketing): TicketingReadOnlyDbContext + HOT entity mapping"
```

---

## Task 21: ITicketingQueryService — akceptované kalkulace per PNF

**Files:**
- Create: `PmTracker.Web/Services/Ticketing/Contracts/HotKalkulaceDto.cs`
- Create: `PmTracker.Web/Services/Ticketing/Contracts/HotZaznamDto.cs`
- Create: `PmTracker.Web/Services/Ticketing/ITicketingQueryService.cs`
- Create: `PmTracker.Web/Services/Ticketing/TicketingQueryService.cs`
- Create: `PmTracker.Tests.Unit/Ticketing/TicketingQueryServiceTests.cs`
- Modify: `PmTracker.Web/Program.cs` (DI registrace)

- [ ] **Step 1: DTOs**

```csharp
// PmTracker.Web/Services/Ticketing/Contracts/HotZaznamDto.cs
namespace PmTracker.Web.Services.Ticketing.Contracts;

public sealed record HotZaznamDto(string Id, string? TypZaznamu, string? Strucne, string? Popis);
```

```csharp
// PmTracker.Web/Services/Ticketing/Contracts/HotKalkulaceDto.cs
namespace PmTracker.Web.Services.Ticketing.Contracts;

public sealed record HotKalkulaceDto(
    long Id,
    string Pid,
    int? Verze,
    decimal? PracnostA, decimal? SazbaA, decimal? CenaA,
    decimal? PracnostP, decimal? SazbaP, decimal? CenaP,
    decimal? PracnostT, decimal? SazbaT, decimal? CenaT,
    decimal? PracnostI, decimal? SazbaI, decimal? CenaI,
    decimal? Cena,
    int? PocetL, decimal? SazbaL, decimal? CenaL, string? RozpadLicence,
    DateTime? Termin, string? TextTermin);
```

- [ ] **Step 2: Interface + impl**

```csharp
// PmTracker.Web/Services/Ticketing/ITicketingQueryService.cs
using PmTracker.Web.Services.Ticketing.Contracts;

namespace PmTracker.Web.Services.Ticketing;

public interface ITicketingQueryService
{
    Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct);
    Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct);
    Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(IReadOnlyList<string> cislaPnf, CancellationToken ct);
}
```

```csharp
// PmTracker.Web/Services/Ticketing/TicketingQueryService.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Services.Ticketing.Contracts;

namespace PmTracker.Web.Services.Ticketing;

public sealed class TicketingQueryService : ITicketingQueryService
{
    private const string AkceptaceStav = "Akceptováno";
    private readonly TicketingReadOnlyDbContext _db;

    public TicketingQueryService(TicketingReadOnlyDbContext db) { _db = db; }

    public async Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
    {
        var z = await _db.HotZaznamy.AsNoTracking().FirstOrDefaultAsync(r => r.Id == cislo, ct);
        return z == null ? null : new HotZaznamDto(z.Id, z.TypZaznamu, z.Strucne, z.Popis);
    }

    public async Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
    {
        var k = await _db.HotKalkulace.AsNoTracking()
            .Where(x => x.Pid == cislo && x.Akceptace == AkceptaceStav)
            .OrderByDescending(x => x.Verze)
            .FirstOrDefaultAsync(ct);
        return k == null ? null : MapKalk(k);
    }

    public async Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
        IReadOnlyList<string> cislaPnf, CancellationToken ct)
    {
        if (cislaPnf.Count == 0) return new Dictionary<string, HotKalkulaceDto>();

        var raw = await _db.HotKalkulace.AsNoTracking()
            .Where(x => cislaPnf.Contains(x.Pid!) && x.Akceptace == AkceptaceStav)
            .ToListAsync(ct);

        return raw
            .GroupBy(x => x.Pid!)
            .ToDictionary(g => g.Key, g => MapKalk(g.OrderByDescending(k => k.Verze).First()));
    }

    private static HotKalkulaceDto MapKalk(Entities.HotKalkulaceEntity k)
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

- [ ] **Step 3: DI registrace**

V `Program.cs`:

```csharp
builder.Services.AddScoped<ITicketingQueryService, TicketingQueryService>();
```

- [ ] **Step 4: Test**

```csharp
// PmTracker.Tests.Unit/Ticketing/TicketingQueryServiceTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Services.Ticketing;
using PmTracker.Web.Services.Ticketing.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.Ticketing;

public class TicketingQueryServiceTests
{
    private static TicketingReadOnlyDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_FiltrujeNaAkceptovano_NejvyssiVerze()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "336865", Verze = 1, Akceptace = "Nabídka", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "336865", Verze = 2, Akceptace = "Akceptováno", Cena = 200 },
            new HotKalkulaceEntity { Id = 3, Pid = "336865", Verze = 3, Akceptace = "Akceptováno", Cena = 300 });
        await db.SaveChangesAsync();

        var svc = new TicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(3);
        result.Cena.Should().Be(300);
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_ZadnaAkceptovana_VraciNull()
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity { Id = 10, Pid = "111111", Verze = 1, Akceptace = "Nabídka", Cena = 100 });
        await db.SaveChangesAsync();

        var svc = new TicketingQueryService(db);
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
        await db.SaveChangesAsync();

        var svc = new TicketingQueryService(db);
        var result = await svc.GetAkceptovaneKalkulaceAsync(new[] { "A", "B", "C" }, CancellationToken.None);

        result.Should().HaveCount(2);
        result["A"].Cena.Should().Be(10);
        result["B"].Cena.Should().Be(20);
        result.ContainsKey("C").Should().BeFalse();
    }
}
```

- [ ] **Step 5: Run**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TicketingQueryServiceTests" --no-restore`
Expected: 3 passed

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Ticketing/ PmTracker.Web/Program.cs PmTracker.Tests.Unit/Ticketing/
git commit -m "feat(ticketing): TicketingQueryService — akceptované kalkulace + HOT záznam read-through"
```

---

## Task 22: Obohatit buffer a výzvy o HOT data (strucne název)

**Files:**
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.cs`

- [ ] **Step 1: Injektovat ITicketingQueryService**

Upravit konstruktor `VyzvaService` a fieldy:

```csharp
    private readonly PmTrackerDbContext _db;
    private readonly Ticketing.ITicketingQueryService _ticketing;

    public VyzvaService(PmTrackerDbContext db, Ticketing.ITicketingQueryService ticketing)
    {
        _db = db;
        _ticketing = ticketing;
    }
```

- [ ] **Step 2: GetBufferAsync — doplnit StrucneNazev**

Nahraď metodu `GetBufferAsync`:

```csharp
    public async Task<IReadOnlyList<VyzvaBufferItem>> GetBufferAsync(int projektId, CancellationToken ct)
    {
        var pnfTypId = await _db.CiselnikTypuExternichOdkazu
            .Where(t => t.Kod == "PNF").Select(t => t.Id).FirstAsync(ct);

        var raw = await (
            from ev in _db.ZaznamExterniOdkazy.AsNoTracking()
            join z in _db.ProjektoveZaznamy.AsNoTracking() on ev.ZaznamId equals z.Id
            where z.ProjektId == projektId
                && ev.TypOdkazuId == pnfTypId
                && ev.ZaradidDoVyzvy
                && ev.VyzvaId == null
            select new { ev.Id, ev.ZaznamId, ev.Cislo, ev.PredpokladanaCena }
        ).ToListAsync(ct);

        if (raw.Count == 0) return Array.Empty<VyzvaBufferItem>();

        var cisla = raw.Select(r => r.Cislo).Distinct().ToList();
        var hot = await GetHotZaznamyAsync(cisla, ct);

        return raw.Select(r => new VyzvaBufferItem(
            r.Id, r.ZaznamId, r.Cislo,
            hot.GetValueOrDefault(r.Cislo)?.Strucne,
            r.PredpokladanaCena)).ToList();
    }

    private async Task<IReadOnlyDictionary<string, Ticketing.Contracts.HotZaznamDto>> GetHotZaznamyAsync(
        IReadOnlyList<string> cisla, CancellationToken ct)
    {
        var result = new Dictionary<string, Ticketing.Contracts.HotZaznamDto>(cisla.Count);
        foreach (var c in cisla)
        {
            var z = await _ticketing.GetZaznamAsync(c, ct);
            if (z != null) result[c] = z;
        }
        return result;
    }
```

*(V budoucnu můžeme přidat batch `ITicketingQueryService.GetZaznamyAsync(List<string>)` — zatím YAGNI, 1× dotaz per PNF stačí pro desítky PNF per buffer.)*

- [ ] **Step 3: MapToDetail — doplnit StrucneNazev**

Nahraď `MapToDetail` a `GetVyzvaAsync` tak, aby se před mapováním vytáhly HOT DTO:

```csharp
    public async Task<VyzvaDetail?> GetVyzvaAsync(int vyzvaId, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null) return null;

        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId == vyzvaId)
            .OrderBy(ev => ev.Id)
            .ToListAsync(ct);

        var hot = await GetHotZaznamyAsync(polozky.Select(p => p.Cislo).Distinct().ToList(), ct);
        return MapToDetail(vyzva, polozky, hot);
    }

    public async Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct)
    {
        var vyzvy = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId)
            .OrderByDescending(v => v.DatumZalozeni)
            .ToListAsync(ct);
        if (vyzvy.Count == 0) return Array.Empty<VyzvaDetail>();

        var vyzvaIds = vyzvy.Select(v => v.Id).ToList();
        var polozkyMap = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId.HasValue && vyzvaIds.Contains(ev.VyzvaId.Value))
            .OrderBy(ev => ev.Id)
            .GroupBy(ev => ev.VyzvaId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct);

        var allCisla = polozkyMap.Values.SelectMany(list => list).Select(p => p.Cislo).Distinct().ToList();
        var hot = await GetHotZaznamyAsync(allCisla, ct);

        return vyzvy.Select(v => MapToDetail(v, polozkyMap.GetValueOrDefault(v.Id) ?? new(), hot)).ToList();
    }

    private static VyzvaDetail MapToDetail(VyzvaEntity v, List<ZaznamExterniOdkazEntity> polozky,
        IReadOnlyDictionary<string, Ticketing.Contracts.HotZaznamDto> hot)
        => new(
            v.Id, v.ProjektId, v.Kod, v.Rok, v.PoradoveVRoce, v.Stav,
            v.DatumZalozeni, v.ZalozilOsobaId, v.DatumOdeslani, v.OdeslalOsobaId,
            v.MistoPlneniSnapshot, v.CisloRamcoveSmlouvySnapshot,
            polozky.Select(ev => new VyzvaDetailItem(
                ev.Id, ev.ZaznamId, ev.Cislo,
                hot.GetValueOrDefault(ev.Cislo)?.Strucne,
                ev.PredpokladanaCena)).ToList());
```

- [ ] **Step 4: Opravit existující testy, kde se konstruuje VyzvaService ručně**

Test harness `VyzvaServiceTestHarness.CreateService` potřebuje druhý argument. Uprav:

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTestHarness.cs
    public static VyzvaService CreateService(PmTrackerDbContext db)
    {
        var ticketing = new StubTicketingQueryService();
        return new VyzvaService(db, ticketing);
    }

    private sealed class StubTicketingQueryService : PmTracker.Web.Services.Ticketing.ITicketingQueryService
    {
        public Task<PmTracker.Web.Services.Ticketing.Contracts.HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult<PmTracker.Web.Services.Ticketing.Contracts.HotZaznamDto?>(null);

        public Task<PmTracker.Web.Services.Ticketing.Contracts.HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
            => Task.FromResult<PmTracker.Web.Services.Ticketing.Contracts.HotKalkulaceDto?>(null);

        public Task<IReadOnlyDictionary<string, PmTracker.Web.Services.Ticketing.Contracts.HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(IReadOnlyList<string> cislaPnf, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, PmTracker.Web.Services.Ticketing.Contracts.HotKalkulaceDto>>(
                new Dictionary<string, PmTracker.Web.Services.Ticketing.Contracts.HotKalkulaceDto>());
    }
```

- [ ] **Step 5: Run všechny Vyzvy testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Vyzvy" --no-restore`
Expected: všechny passed (14 testů z Task 12-19)

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Vyzvy/VyzvaService.cs PmTracker.Tests.Unit/Vyzvy/VyzvaServiceTestHarness.cs
git commit -m "feat(vyzvy): obohacení DTO o strucne z HOT_ZAZNAMY přes ITicketingQueryService"
```

---

## Task 23: Full run all tests + finální commit

- [ ] **Step 1: Run all tests**

Run: `dotnet test PmTracker.Tests.Unit --no-restore`
Expected: všechny testy PASS (nově přidané + existující — typicky desítky testů)

- [ ] **Step 2: Pokud nějaký existující test selhal**

Opravi ho — pravděpodobné příčiny:
- referuje na starý typ `CiselnikVyzvaEntity` nebo `CiselnikVyzvy` → nahraď `VyzvaEntity` / `Vyzvy`
- referuje na pole `ZaznamExterniOdkaz.Vyzva` → nahraď `VyzvaId`
- baseline documentation → už opraveno v Tasku 10; pokud dosud ne, doplň

- [ ] **Step 3: Build celé solution**

Run: `dotnet build --no-restore`
Expected: Build succeeded, 0 Error(s)

- [ ] **Step 4: Verify gitu je čistý**

Run: `git status`
Expected: nothing to commit, working tree clean

- [ ] **Step 5: Vytvořit merge commit nebo tag na vrchol fáze 1**

```bash
git log --oneline -n 25 | cat
```

Očekávaný poslední commit: "feat(vyzvy): obohacení DTO o strucne …" (z Tasku 22).

Pokud není, opakuj chybějící úkoly.

---

## Hotovo — Fáze 1

Po dokončení tohoto plánu máš:
- ✅ Schéma DB (migrace + upgrade SQL skript)
- ✅ Entity, DbContext, konfigurace
- ✅ VyzvaService s kompletní business logikou (buffer, založení, stavové přechody, přeřazení)
- ✅ TicketingReadOnlyDbContext + TicketingQueryService (read-through do intranetNEW.HOT_*)
- ✅ Jednotkové testy (xUnit + FluentAssertions + InMemory provider)
- ✅ Audit log přechodů stavů

**Co chybí pro produkční deployment (samostatné plány):**
- Fáze 2: UI (záložka Výzvy, switch na externí vazbě, DnD modal, editace projektu)
- Fáze 3: Word export (OpenXML, šablona, vodoznak NÁVRH)
- Fáze 4: Secret manager / KeyVault connection string, integrační testy (Testcontainers s reálným SQL Serverem), manuální akceptace se vzorovou výzvou
