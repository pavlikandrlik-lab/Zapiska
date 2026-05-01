# ServiceDesk integrace — Sprint A (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rozšíření `PmTracker.ServiceDesk.Sql` o podporu IS hierarchie (HOT_IS/HOT_MODULY/HOT_SUBSYSTEM) a dashboardových dotazů nad aktivními tickety, aby mohl být ve Sprintu B aktivován panel "NES v prodlení" v projektovém dashboardu bez dalších změn v backendu.

**Architecture:** Doplňuje stávající read-only `TicketingReadOnlyDbContext` o tři nové entity (HOT_IS, HOT_MODULY, HOT_SUBSYSTEM) a přidává nový query service `IInformacniSystemQueryService` se třemi operacemi: seznam aktivních IS pro UI dropdown, seznam ticketů v prodlení per IS (dle `typ_zaznamu` používá `sla_deadline` nebo `dat_res_t`), a rozpočtová metrika IS. Pattern následuje existující `ITicketingQueryService` (Contracts → Sql implementace → Disabled fallback → Caching dekorátor → DI extension).

**Tech Stack:** .NET 8, EF Core 8 (SqlServer + InMemory pro testy), xUnit 2.9, FluentAssertions 8, Moq 4 (kde potřeba). Žádné nové NuGet balíčky.

**Design spec (source of truth):** [docs/superpowers/specs/2026-04-16-projektovy-dashboard-design.md](../specs/2026-04-16-projektovy-dashboard-design.md) — sekce "Záložka 2: NES v prodlení" (§98–129) a "Databázové změny" (§217–244).

---

## Kontext a rozhodnutí z brainstormu 2026-04-23

Brainstorm uzavřel následující rozhodnutí, která **nejsou** v originálním design specu, ale jsou závazná pro tento Sprint A:

1. **Definice prodlení per `typ_zaznamu`:**
   - `NES`: `sla_deadline < @reference AND stav != 'archiv'` (NES jako jediné mají `sla_deadline` vyplněné)
   - `PMP` / `PNF`: `dat_res_t < @reference AND stav != 'archiv'` (PMP/PNF mají vždy `sla_deadline = NULL`)
   - `term_pl` je placeholder (vždy někde v budoucnosti) — **nepoužívá se**.

2. **Prodlení = `DATEDIFF(DAY, @termin, @reference)`**. Žádná penalizační logika U1–U11, žádný parser textu vyjádření.

3. **Rozpočet per IS = přímo `HOT_IS.limit` a `HOT_IS.cerpani`**. Žádné sčítání přes subsystem-level tabulky.

4. **Bug #1 (Splneno type):** `HotZaznamEntity.Splneno` je v kódu `int?`, ale v DB je to `smalldatetime`. Oprava je v scope Sprintu A (sloupec zatím nikdo nečte, ale časovaná bomba).

5. **Kalkulace filter = jen `"Akceptováno"`:** Byznys rozhodnutí uživatele 2026-04-23. Fakturované stavy (`Fakturováno`/`Fakturovat`/`Vyfakturováno`) a další se NEzahrnují. Existující test [`SqlTicketingQueryServiceTests.GetAkceptovanouKalkulaci_FiltrujeAkceptovano_VraciNejvyssiVerzi`](../../../../PmTracker.Tests.Unit/ServiceDesk/SqlTicketingQueryServiceTests.cs) odráží tento design správně. **Žádná změna v `SqlTicketingQueryService` ve Sprintu A.**

6. **File storage pro přílohy, URL base, výzvy, penalizace, HTML parser** = out of scope pro Sprint A (řešeno v Sprintu B/C nebo trvale OUT).

7. **Design reality:** v DB neexistují formální FK mezi `HOT_*` tabulkami — EF mappingy používají navigation properties přes `HasPrincipalKey`/`HasForeignKey`, ale v DB konstrukty neexistují.

8. **Vztah ke stávající sync infrastruktuře (revize 2026-04-24):** Aplikace už má hotovou generickou sync infra (`PmTracker.Web/Services/Sync/SyncHostedServiceBase<TSettings>`, `ISyncJobSettings`, admin karta `/Nastaveni?section=synchronizace`) a dva běžící SD joby — `SdActivePeriodicSyncHostedService` a `SdArchivePeriodicSyncHostedService`. Ty **harvestují `HOT_VYJADRENI`** do lokální PmTracker DB (chat modal, binding rebalance). Sprint A je **datově disjunktní** — nový `IInformacniSystemQueryService` je on-demand read nad `HOT_IS/HOT_MODULY/HOT_SUBSYSTEM` z `intranetNEW.dbo`, bez lokální cache a bez vlastního hosted service. Sdílí jen `TicketingReadOnlyDbContext`.

9. **Žádná cache vrstva ve Sprintu A (revize 2026-04-24):** V produkci je 3 IS (ověřeno v discovery výstupu), query triviální. `AddScoped` + in-memory fields by fakticky fungovaly jen v rámci jednoho requestu — nefunkční dekorátor. Pokud by dashboard v budoucnu vyžadoval cache, koncepčně čisté řešení je nový sync job `sd.infosystems` přes stávající `SyncHostedServiceBase` s lokálním snapshot tabulkou. To je mimo scope Sprintu A.

10. **Žádné `.HasIndex(...)` v mappingu (revize 2026-04-24):** `TicketingReadOnlyDbContext` je read-only (SaveChanges throw-uje), nikdy nevolá `Migrate/EnsureCreated`. EF index metadata jsou tedy no-op. Nemáme ani DDL přístup k `intranetNEW` (cizí DB, read-only guard). Indexy musí zajistit DBA ServiceDesku; my je nedeklarujeme v modelu, abychom nepředstírali funkčnost, kterou nemáme.

11. **Id handling (memory feedback 2026-04-23 + 2026-04-24):** `HOT_ZAZNAMY.id` je v DB `int NULL`. Pravidlo: **ticket bez `id` je mimo scope PM Trackeru** (user zadává ticket výhradně přes 6-ciferné id). Query vrstva tedy **vždy** filtruje `z.Id != null && z.Id != ""` a používá `int.Parse(z.Id!)` pro mapování na DTO. NIKDY `TryParse` s fallbackem `0` — to by tiše zobrazilo „ghost" ticket v UI.

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `PmTracker.ServiceDesk.Sql/Entities/HotIsEntity.cs` | EF mapping tabulky `HOT_IS` (informační systémy) |
| `PmTracker.ServiceDesk.Sql/Entities/HotModulyEntity.cs` | EF mapping tabulky `HOT_MODULY` |
| `PmTracker.ServiceDesk.Sql/Entities/HotSubsystemEntity.cs` | EF mapping tabulky `HOT_SUBSYSTEM` |
| `PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs` | SQL implementace — queries pro seznam IS, prodlené tickety, rozpočet |
| `PmTracker.ServiceDesk.Sql/DisabledInformacniSystemQueryService.cs` | Fallback když `Ticketing:Enabled = false` |
| `PmTracker.ServiceDesk.Contracts/IInformacniSystemQueryService.cs` | Contract |
| `PmTracker.ServiceDesk.Contracts/Contracts/InformacniSystemDto.cs` | DTO pro seznam IS (id, název, zkratka, aktivita, rozpočet) |
| `PmTracker.ServiceDesk.Contracts/Contracts/ProdlenyTicketDto.cs` | DTO pro tickety v prodlení (id ticketu, pid, název, dní prodlení, ...) |
| `PmTracker.ServiceDesk.Contracts/Contracts/IsRozpocetDto.cs` | DTO pro rozpočet (limit, čerpání, procento) |
| `PmTracker.Tests.Unit/ServiceDesk/HotIsEntityShapeTests.cs` | Reflection test — sloupce/PK |
| `PmTracker.Tests.Unit/ServiceDesk/HotModulyEntityShapeTests.cs` | Reflection test |
| `PmTracker.Tests.Unit/ServiceDesk/HotSubsystemEntityShapeTests.cs` | Reflection test |
| `PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs` | In-memory testy |
| `PmTracker.Tests.Unit/ServiceDesk/DisabledInformacniSystemQueryServiceTests.cs` | Fallback testy |
| `PmTracker.Tests.Unit/ServiceDesk/TicketingReadOnlyDbContextShapeTests.cs` | Reflection test — DbContext mapuje všech 6 entit |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs` | Fix Bug #1 (`Splneno` → `DateTime?`) + rozšíření o sloupce potřebné pro filtraci/zobrazení v prodlení |
| `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` | Registrace 3 nových entit + rozšíření `HotZaznamEntity` mapping |
| `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs` | DI registrace `IInformacniSystemQueryService` (Sql + Caching nebo Disabled) |
| `PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs` | Update shape testu pro nové sloupce + `Splneno` typ |

---

## Tasks

### Task 1: Fix Bug #1 a rozšíření HotZaznamEntity

**Files:**
- Modify: `PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` (řádky 34-48)
- Modify: `PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs`

- [ ] **Step 1: Přidat failing shape test pro rozšířené property**

V `PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs` rozšířit existující shape test o nové vlastnosti. Přečti si nejdřív stávající obsah. Pak přidej:

```csharp
[Fact]
public void HotZaznamEntity_MaVsechnySloupceProNesDashboard()
{
    var t = typeof(PmTracker.ServiceDesk.Sql.Entities.HotZaznamEntity);
    var required = new[]
    {
        // fix Bug #1 + existing
        "Radek", "Id", "Pid", "TypZaznamu", "Strucne", "Popis",
        "Stav", "Splneno", "SlaDeadline", "Datum",
        // nové pro NES dashboard
        "Modul", "Subsystem",
        "TermPl", "DatResT", "DatDod",
        "Dulezitost", "Zavaznost",
        "Dodavatel", "ResTym",
        "Zpracoval", "Uzivatel", "Email", "ZalHfu",
        "PriznakZamceni", "PriznakGdpr", "Schvaleno",
    };
    foreach (var name in required)
        t.GetProperty(name).Should().NotBeNull($"property {name}");
}

[Fact]
public void HotZaznamEntity_SplnenoJeDateTimeNullable()
{
    var prop = typeof(PmTracker.ServiceDesk.Sql.Entities.HotZaznamEntity)
        .GetProperty("Splneno")!;
    prop.PropertyType.Should().Be(typeof(DateTime?),
        "Bug #1: sloupec HOT_ZAZNAMY.splneno je smalldatetime, ne int");
}
```

- [ ] **Step 2: Spustit test — ověřit FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotZaznamEntityShape" --no-restore`
Expected: FAIL s chybějícími properties a `Splneno` jako `int?`.

- [ ] **Step 3: Rozšířit `HotZaznamEntity.cs`**

Nahraď obsah souboru:

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
    public string? Stav { get; set; }

    // Bug #1 fix: DB typ je smalldatetime, nikoli int
    public DateTime? Splneno { get; set; }

    public DateTime? SlaDeadline { get; set; }
    public DateTime? Datum { get; set; }

    // NOVÉ — pro filtraci a zobrazení "v prodlení" dashboardu
    public string? Modul { get; set; }
    public string? Subsystem { get; set; }

    public DateTime? TermPl { get; set; }
    public DateTime? DatResT { get; set; }
    public DateTime? DatDod { get; set; }

    public string? Dulezitost { get; set; }
    public string? Zavaznost { get; set; }

    public string? Dodavatel { get; set; }
    public string? ResTym { get; set; }

    public string? Zpracoval { get; set; }
    public string? Uzivatel { get; set; }
    public string? Email { get; set; }
    public string? ZalHfu { get; set; }

    public byte PriznakZamceni { get; set; }
    public bool? PriznakGdpr { get; set; }
    public bool Schvaleno { get; set; }
}
```

- [ ] **Step 4: Rozšířit EF mapping v `TicketingReadOnlyDbContext.cs`**

V metodě `OnModelCreating`, najdi sekci `mb.Entity<HotZaznamEntity>(e =>` (řádek ~34). Nahraď celou konfiguraci tímto:

```csharp
mb.Entity<HotZaznamEntity>(e =>
{
    e.ToTable("HOT_ZAZNAMY", "dbo");
    e.HasKey(x => x.Radek);

    e.Property(x => x.Radek).HasColumnName("radek");
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.TypZaznamu).HasColumnName("typ_zaznamu").HasMaxLength(5);
    e.Property(x => x.Strucne).HasColumnName("strucne").HasMaxLength(250);
    e.Property(x => x.Popis).HasColumnName("popis");
    e.Property(x => x.Pid).HasColumnName("pid").HasMaxLength(50);
    e.Property(x => x.Stav).HasColumnName("stav").HasMaxLength(50);
    e.Property(x => x.Splneno).HasColumnName("splneno").HasColumnType("smalldatetime");
    e.Property(x => x.SlaDeadline).HasColumnName("sla_deadline").HasColumnType("datetime");
    e.Property(x => x.Datum).HasColumnName("datum").HasColumnType("smalldatetime");

    // nová pole
    e.Property(x => x.Modul).HasColumnName("modul").HasMaxLength(50);
    e.Property(x => x.Subsystem).HasColumnName("subsystem").HasMaxLength(5);
    e.Property(x => x.TermPl).HasColumnName("term_pl").HasColumnType("smalldatetime");
    e.Property(x => x.DatResT).HasColumnName("dat_res_t").HasColumnType("smalldatetime");
    e.Property(x => x.DatDod).HasColumnName("dat_dod").HasColumnType("smalldatetime");
    e.Property(x => x.Dulezitost).HasColumnName("dulezitost").HasMaxLength(10);
    e.Property(x => x.Zavaznost).HasColumnName("zavaznost").HasMaxLength(50);
    e.Property(x => x.Dodavatel).HasColumnName("dodavatel").HasMaxLength(50);
    e.Property(x => x.ResTym).HasColumnName("res_tym").HasMaxLength(50);
    e.Property(x => x.Zpracoval).HasColumnName("zpracoval").HasMaxLength(50);
    e.Property(x => x.Uzivatel).HasColumnName("uzivatel").HasMaxLength(50);
    e.Property(x => x.Email).HasColumnName("email").HasMaxLength(50);
    e.Property(x => x.ZalHfu).HasColumnName("zal_HFU").HasMaxLength(50);
    e.Property(x => x.PriznakZamceni).HasColumnName("priznak_zamceni");
    e.Property(x => x.PriznakGdpr).HasColumnName("priznak_gdpr");
    e.Property(x => x.Schvaleno).HasColumnName("schvaleno");
});
```

Poznámka: viz Kontext §10 — žádné `.HasIndex(...)` v read-only kontextu (no-op, matoucí). Výkon cizí DB je zodpovědnost DBA ServiceDesku.

- [ ] **Step 5: Build a spustit shape test — PASS**

Run: `dotnet build PmTracker.ServiceDesk.Sql --no-restore`
Expected: PASS.

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotZaznamEntityShape" --no-restore`
Expected: PASS — všechny properties existují, `Splneno` je `DateTime?`.

- [ ] **Step 6: Spustit ostatní existující SD testy — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "FullyQualifiedName~ServiceDesk" --no-restore`
Expected: PASS (žádný stávající test by se nemněl pokazit — všechny nové properties jsou přidané, nic neodebráno).

- [ ] **Step 7: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/HotZaznamEntity.cs \
        PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/HotZaznamEntityShapeTests.cs
git commit -m "fix(sd): oprava typu Splneno + rozšíření HotZaznamEntity pro dashboard

Bug #1: HOT_ZAZNAMY.splneno je smalldatetime, v entitě bylo int (časovaná bomba
při prvním .Select(x => x.Splneno)). Opraveno na DateTime?.

Rozšíření o sloupce potřebné pro filtraci NES/PMP/PNF v prodlení per IS:
modul, subsystem, term_pl, dat_res_t, dat_dod, dulezitost, zavaznost,
dodavatel, res_tym, zpracoval, uzivatel, email, zal_HFU, priznak_zamceni,
priznak_gdpr, schvaleno."
```

---

### Task 2: HotSubsystemEntity

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotSubsystemEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/HotSubsystemEntityShapeTests.cs`

- [ ] **Step 1: Napsat shape test**

Vytvořit `PmTracker.Tests.Unit/ServiceDesk/HotSubsystemEntityShapeTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotSubsystemEntityShapeTests
{
    [Fact]
    public void HotSubsystemEntity_MaPozadovaneProperty()
    {
        var t = typeof(HotSubsystemEntity);
        var required = new[] { "Id", "Nazev", "Zkratka", "Aktivita", "Dodavatel", "PriznakGdprSub" };
        foreach (var n in required)
            t.GetProperty(n).Should().NotBeNull($"property {n}");
    }

    [Fact]
    public void HotSubsystemEntity_ZkratkaJeNonNullableString()
    {
        // HOT_SUBSYSTEM.zkratka je nvarchar(5) NOT NULL a reálné PK
        var prop = typeof(HotSubsystemEntity).GetProperty("Zkratka")!;
        prop.PropertyType.Should().Be(typeof(string));
    }
}
```

- [ ] **Step 2: Spustit — FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotSubsystemEntityShape" --no-restore`
Expected: FAIL (class neexistuje).

- [ ] **Step 3: Vytvořit entitu**

Soubor `PmTracker.ServiceDesk.Sql/Entities/HotSubsystemEntity.cs`:

```csharp
namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_SUBSYSTEM — subsystémy informačních systémů.
/// Reálný PK je <c>zkratka</c> (nvarchar(5) NOT NULL).
/// </summary>
internal sealed class HotSubsystemEntity
{
    public int Id { get; set; }
    public string? Nazev { get; set; }
    public string Zkratka { get; set; } = string.Empty;
    public string? Aktivita { get; set; }
    public string? Dodavatel { get; set; }
    public bool? PriznakGdprSub { get; set; }
}
```

- [ ] **Step 4: Registrovat v DbContextu**

V `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`:

Najdi sekci `internal DbSet<...>` (řádek 15-17) a pod `HotVyjadreni` přidej:

```csharp
    internal DbSet<HotSubsystemEntity> HotSubsystemy => Set<HotSubsystemEntity>();
```

V `OnModelCreating` za stávající `HotVyjadreniEntity` mapping přidej:

```csharp
mb.Entity<HotSubsystemEntity>(e =>
{
    e.ToTable("HOT_SUBSYSTEM", "dbo");
    // reálné PK je zkratka (viz discovery SQL / hotline.txt)
    e.HasKey(x => x.Zkratka);
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.Nazev).HasColumnName("nazev").HasMaxLength(50);
    e.Property(x => x.Zkratka).HasColumnName("zkratka").HasMaxLength(5).IsRequired();
    e.Property(x => x.Aktivita).HasColumnName("aktivita").HasMaxLength(10);
    e.Property(x => x.Dodavatel).HasColumnName("dodavatel").HasMaxLength(10);
    e.Property(x => x.PriznakGdprSub).HasColumnName("priznak_gdpr_sub");
});
```

- [ ] **Step 5: Build + test — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotSubsystemEntityShape" --no-restore`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/HotSubsystemEntity.cs \
        PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/HotSubsystemEntityShapeTests.cs
git commit -m "feat(sd): přidat HotSubsystemEntity (HOT_SUBSYSTEM) s PK přes zkratka"
```

---

### Task 3: HotModulyEntity

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotModulyEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/HotModulyEntityShapeTests.cs`

- [ ] **Step 1: Shape test**

Soubor `PmTracker.Tests.Unit/ServiceDesk/HotModulyEntityShapeTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotModulyEntityShapeTests
{
    [Fact]
    public void HotModulyEntity_MaPozadovaneProperty()
    {
        var t = typeof(HotModulyEntity);
        var required = new[] { "Id", "Modul", "Zkratka", "Subsystem", "IdIS", "Faze", "Aktivita", "Dodavatel" };
        foreach (var n in required)
            t.GetProperty(n).Should().NotBeNull($"property {n}");
    }
}
```

- [ ] **Step 2: Spustit — FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotModulyEntityShape" --no-restore`
Expected: FAIL.

- [ ] **Step 3: Vytvořit entitu**

Soubor `PmTracker.ServiceDesk.Sql/Entities/HotModulyEntity.cs`:

```csharp
namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_MODULY — moduly pod subsystémy.
/// Join na HOT_ZAZNAMY je přes <c>zkratka</c> (99.7% match dle discovery).
/// FK na HOT_IS přes <c>id_IS</c>.
/// </summary>
internal sealed class HotModulyEntity
{
    public int Id { get; set; }
    public string? Modul { get; set; }
    public string Zkratka { get; set; } = string.Empty;
    public string? Subsystem { get; set; }
    public int? IdIS { get; set; }
    public string? Faze { get; set; }
    public string? Aktivita { get; set; }
    public string? Dodavatel { get; set; }
}
```

- [ ] **Step 4: Registrovat v DbContextu**

V `OnModelCreating` za `HotSubsystemEntity` mapping přidej:

```csharp
mb.Entity<HotModulyEntity>(e =>
{
    e.ToTable("HOT_MODULY", "dbo");
    e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("id");
    e.Property(x => x.Modul).HasColumnName("modul").HasMaxLength(50);
    e.Property(x => x.Zkratka).HasColumnName("zkratka").HasMaxLength(10).IsRequired();
    e.Property(x => x.Subsystem).HasColumnName("subsystem").HasMaxLength(5);
    e.Property(x => x.IdIS).HasColumnName("id_IS");
    e.Property(x => x.Faze).HasColumnName("faze").HasMaxLength(8);
    e.Property(x => x.Aktivita).HasColumnName("aktivita").HasMaxLength(10);
    e.Property(x => x.Dodavatel).HasColumnName("dodavatel").HasMaxLength(50);
});
```

(viz Kontext §10 — indexy v read-only modelu záměrně ne)

Přidej DbSet v `TicketingReadOnlyDbContext.cs`:

```csharp
    internal DbSet<HotModulyEntity> HotModuly => Set<HotModulyEntity>();
```

- [ ] **Step 5: Build + test — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotModulyEntityShape" --no-restore`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/HotModulyEntity.cs \
        PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/HotModulyEntityShapeTests.cs
git commit -m "feat(sd): přidat HotModulyEntity (HOT_MODULY) s FK id_IS → HOT_IS.ID"
```

---

### Task 4: HotIsEntity

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/Entities/HotIsEntity.cs`
- Modify: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/HotIsEntityShapeTests.cs`

- [ ] **Step 1: Shape test**

Soubor `PmTracker.Tests.Unit/ServiceDesk/HotIsEntityShapeTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HotIsEntityShapeTests
{
    [Fact]
    public void HotIsEntity_MaPozadovaneProperty()
    {
        var t = typeof(HotIsEntity);
        var required = new[] { "Id", "Nazev", "Zkratka", "Aktivita", "Limit", "Cerpani", "Semafor" };
        foreach (var n in required)
            t.GetProperty(n).Should().NotBeNull($"property {n}");
    }

    [Fact]
    public void HotIsEntity_LimitACerpaniJsouDecimal()
    {
        var t = typeof(HotIsEntity);
        t.GetProperty("Limit")!.PropertyType.Should().Be(typeof(decimal?));
        t.GetProperty("Cerpani")!.PropertyType.Should().Be(typeof(decimal?));
    }
}
```

- [ ] **Step 2: Spustit — FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotIsEntityShape" --no-restore`
Expected: FAIL.

- [ ] **Step 3: Vytvořit entitu**

Soubor `PmTracker.ServiceDesk.Sql/Entities/HotIsEntity.cs`:

```csharp
namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_IS — informační systémy (FIS, ISSP, X_FIS).
/// Aktivita je char(10), má trailing spaces — použít LIKE 'Aktivní%' nebo LTRIM/RTRIM.
/// </summary>
internal sealed class HotIsEntity
{
    public int Id { get; set; }
    public string? Nazev { get; set; }
    public string? Zkratka { get; set; }
    public string? Aktivita { get; set; }
    public decimal? Limit { get; set; }
    public decimal? Cerpani { get; set; }
    public string? Semafor { get; set; }
}
```

- [ ] **Step 4: Registrovat v DbContextu**

V `OnModelCreating` za `HotModulyEntity` mapping:

```csharp
mb.Entity<HotIsEntity>(e =>
{
    e.ToTable("HOT_IS", "dbo");
    e.HasKey(x => x.Id);
    e.Property(x => x.Id).HasColumnName("ID");
    e.Property(x => x.Nazev).HasColumnName("nazev").HasMaxLength(50);
    e.Property(x => x.Zkratka).HasColumnName("zkratka").HasMaxLength(10);
    e.Property(x => x.Aktivita).HasColumnName("aktivita").HasMaxLength(10).IsFixedLength();
    e.Property(x => x.Limit).HasColumnName("limit").HasColumnType("numeric(18,2)");
    e.Property(x => x.Cerpani).HasColumnName("cerpani").HasColumnType("numeric(18,2)");
    e.Property(x => x.Semafor).HasColumnName("semafor").HasMaxLength(5);
});
```

Přidej DbSet:

```csharp
    internal DbSet<HotIsEntity> HotIs => Set<HotIsEntity>();
```

- [ ] **Step 5: Build + test — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "HotIsEntityShape" --no-restore`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/Entities/HotIsEntity.cs \
        PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs \
        PmTracker.Tests.Unit/ServiceDesk/HotIsEntityShapeTests.cs
git commit -m "feat(sd): přidat HotIsEntity (HOT_IS) — informační systémy"
```

---

### Task 5: DTOs (InformacniSystemDto, ProdlenyTicketDto, IsRozpocetDto)

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/Contracts/InformacniSystemDto.cs`
- Create: `PmTracker.ServiceDesk.Contracts/Contracts/ProdlenyTicketDto.cs`
- Create: `PmTracker.ServiceDesk.Contracts/Contracts/IsRozpocetDto.cs`

- [ ] **Step 1: Vytvořit `InformacniSystemDto.cs`**

```csharp
namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Informační systém ze ServiceDesku. Používá se pro dropdown
/// nastavení IS na projektu a pro lookup hierarchie.
/// </summary>
public sealed record InformacniSystemDto(
    int Id,
    string Nazev,
    string Zkratka,
    bool JeAktivni,
    decimal? Limit,
    decimal? Cerpani);
```

- [ ] **Step 2: Vytvořit `ProdlenyTicketDto.cs`**

```csharp
namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Ticket v prodlení pro panel "NES v prodlení" v projektovém dashboardu.
/// Společný tvar pro NES/PMP/PNF — zdroj termínu (SLA nebo plánovaný)
/// je typově rozlišen přes <see cref="TypZaznamu"/>.
/// </summary>
public sealed record ProdlenyTicketDto(
    int Id,
    string Pid,
    string TypZaznamu,
    string? Strucne,
    string? Dulezitost,
    string? Zavaznost,
    string? Modul,
    string? Dodavatel,
    string? Stav,
    DateTime Termin,
    int DniProdleni);
```

- [ ] **Step 3: Vytvořit `IsRozpocetDto.cs`**

```csharp
namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Rozpočtová metrika IS — čerpání vůči limitu.
/// Hodnoty se berou přímo z <c>HOT_IS.limit</c>/<c>cerpani</c>, bez součtu
/// přes subsystem-level tabulky.
/// </summary>
public sealed record IsRozpocetDto(
    int IsId,
    string IsZkratka,
    decimal? Limit,
    decimal? Cerpani)
{
    /// <summary>Procento čerpání 0–100 (nebo null pokud limit není známý).</summary>
    public decimal? ProcentoCerpani
        => Limit is > 0 && Cerpani is not null
            ? Math.Round(Cerpani.Value / Limit.Value * 100m, 1, MidpointRounding.AwayFromZero)
            : null;
}
```

- [ ] **Step 4: Build — PASS**

Run: `dotnet build PmTracker.ServiceDesk.Contracts --no-restore`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.ServiceDesk.Contracts/Contracts/InformacniSystemDto.cs \
        PmTracker.ServiceDesk.Contracts/Contracts/ProdlenyTicketDto.cs \
        PmTracker.ServiceDesk.Contracts/Contracts/IsRozpocetDto.cs
git commit -m "feat(sd): DTO pro informační systém, prodlený ticket a rozpočet"
```

---

### Task 6: `IInformacniSystemQueryService` contract

**Files:**
- Create: `PmTracker.ServiceDesk.Contracts/IInformacniSystemQueryService.cs`

- [ ] **Step 1: Vytvořit interface**

Soubor `PmTracker.ServiceDesk.Contracts/IInformacniSystemQueryService.cs`:

```csharp
namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Read-only dotazy nad informačními systémy v ServiceDesku.
/// Pokud je <c>Ticketing:Enabled = false</c>, DI poskytuje
/// <c>DisabledInformacniSystemQueryService</c>, který vrací prázdné výsledky.
/// </summary>
public interface IInformacniSystemQueryService
{
    /// <summary>
    /// Seznam aktivních informačních systémů pro dropdown nastavení IS
    /// na projektu. Filtruje se podle <c>aktivita</c> (trimmed).
    /// </summary>
    Task<IReadOnlyList<InformacniSystemDto>> GetAktivniIsAsync(CancellationToken ct);

    /// <summary>
    /// Vrátí tickety v prodlení spadající pod daný informační systém.
    /// Definice prodlení per typ:
    /// <list type="bullet">
    ///   <item><c>NES</c>: <c>sla_deadline &lt; reference</c></item>
    ///   <item><c>PMP</c>/<c>PNF</c>: <c>dat_res_t &lt; reference</c></item>
    /// </list>
    /// Vždy filtruje <c>stav != 'archiv'</c>. Řazeno od nejstarších (tj. nejvíce
    /// v prodlení) nahoru.
    /// </summary>
    Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
        int isId,
        DateTime reference,
        CancellationToken ct);

    /// <summary>
    /// Rozpočtová metrika IS — limit, čerpání, procento.
    /// </summary>
    Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct);
}
```

- [ ] **Step 2: Build — PASS**

Run: `dotnet build PmTracker.ServiceDesk.Contracts --no-restore`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.ServiceDesk.Contracts/IInformacniSystemQueryService.cs
git commit -m "feat(sd): IInformacniSystemQueryService contract"
```

---

### Task 7: `SqlInformacniSystemQueryService` — `GetAktivniIsAsync`

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs`

- [ ] **Step 1: Napsat failing test pro `GetAktivniIsAsync`**

Soubor `PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SqlInformacniSystemQueryServiceTests
{
    private static TicketingReadOnlyDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    [Fact]
    public async Task GetAktivniIsAsync_VraciJenAktivni_IgnorujeTrailingSpaces()
    {
        using var db = CreateDb();
        db.HotIs.AddRange(
            new HotIsEntity { Id = 1, Nazev = "Finanční IS",    Zkratka = "FIS",   Aktivita = "Aktivní   ", Limit = 100, Cerpani = 50 },
            new HotIsEntity { Id = 2, Nazev = "Personální IS",  Zkratka = "ISSP",  Aktivita = "Aktivní   ", Limit = 200, Cerpani = 20 },
            new HotIsEntity { Id = 3, Nazev = "Vyřazený",        Zkratka = "OLD",   Aktivita = "Neaktivní", Limit = null, Cerpani = null });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetAktivniIsAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Zkratka).Should().BeEquivalentTo(new[] { "FIS", "ISSP" });
        result.All(x => x.JeAktivni).Should().BeTrue();
    }

    [Fact]
    public async Task GetAktivniIsAsync_PrazdnaDb_VraciPrazdnyList()
    {
        using var db = CreateDb();
        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetAktivniIsAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Spustit — FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SqlInformacniSystemQueryService" --no-restore`
Expected: FAIL (class neexistuje).

- [ ] **Step 3: Vytvořit service s implementací**

Soubor `PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql.Entities;

namespace PmTracker.ServiceDesk.Sql;

public sealed class SqlInformacniSystemQueryService : IInformacniSystemQueryService
{
    private const string AktivniPrefix = "Aktivní";
    private const string ArchivStav = "archiv";

    private readonly TicketingReadOnlyDbContext _db;

    public SqlInformacniSystemQueryService(TicketingReadOnlyDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<InformacniSystemDto>> GetAktivniIsAsync(CancellationToken ct)
    {
        // aktivita je char(10) → LIKE 'Aktivní%' spolehlivě matchne s trailing space
        var raw = await _db.HotIs
            .Where(i => i.Aktivita != null && EF.Functions.Like(i.Aktivita, AktivniPrefix + "%"))
            .OrderBy(i => i.Id)
            .ToListAsync(ct);

        return raw
            .Select(i => new InformacniSystemDto(
                Id: i.Id,
                Nazev: i.Nazev ?? string.Empty,
                Zkratka: (i.Zkratka ?? string.Empty).Trim(),
                JeAktivni: true,
                Limit: i.Limit,
                Cerpani: i.Cerpani))
            .ToList();
    }

    public Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
        int isId, DateTime reference, CancellationToken ct)
        => throw new NotImplementedException("Task 8");

    public Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct)
        => throw new NotImplementedException("Task 9");
}
```

- [ ] **Step 4: Spustit — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SqlInformacniSystemQueryServiceTests.GetAktivniIsAsync" --no-restore`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs \
        PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs
git commit -m "feat(sd): SqlInformacniSystemQueryService.GetAktivniIsAsync"
```

---

### Task 8: `SqlInformacniSystemQueryService.GetProdleneAsync`

**Files:**
- Modify: `PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs`
- Modify: `PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs`

- [ ] **Step 1: Napsat failing testy pro tři typy + archiv filter**

Přidej do `SqlInformacniSystemQueryServiceTests.cs`:

```csharp
private static void SeedIsHierarchy(TicketingReadOnlyDbContext db)
{
    db.HotIs.Add(new HotIsEntity { Id = 1, Zkratka = "FIS", Aktivita = "Aktivní   " });
    db.HotModuly.AddRange(
        new HotModulyEntity { Id = 100, Zkratka = "R_EIS", Subsystem = "DVEIS", IdIS = 1, Aktivita = "Aktivní" },
        new HotModulyEntity { Id = 101, Zkratka = "TP_RZA", Subsystem = "TPFIS", IdIS = 1, Aktivita = "Aktivní" },
        new HotModulyEntity { Id = 200, Zkratka = "JINY_IS", Subsystem = "XXX", IdIS = 99, Aktivita = "Aktivní" }); // mimo náš IS
}

[Fact]
public async Task GetProdleneAsync_NES_UzivaSlaDeadline_FiltrujeStav()
{
    var reference = new DateTime(2026, 4, 23, 12, 0, 0);

    using var db = CreateDb();
    SeedIsHierarchy(db);
    db.HotZaznamy.AddRange(
        // v prodlení u dodavatele — MUSÍ SE VRÁTIT
        new HotZaznamEntity { Radek = 1, Id = "111111", Pid = "P1", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "dodavatel", SlaDeadline = reference.AddDays(-5) },
        // v prodlení ale archiv — IGNOROVAT
        new HotZaznamEntity { Radek = 2, Id = "222222", Pid = "P2", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "archiv", SlaDeadline = reference.AddDays(-10) },
        // NES bez SLA — IGNOROVAT (NES musí mít sla_deadline)
        new HotZaznamEntity { Radek = 3, Id = "333333", Pid = "P3", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "otevřeno", SlaDeadline = null },
        // SLA v budoucnu — IGNOROVAT
        new HotZaznamEntity { Radek = 4, Id = "444444", Pid = "P4", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "otevřeno", SlaDeadline = reference.AddDays(5) },
        // modul mimo náš IS — IGNOROVAT
        new HotZaznamEntity { Radek = 5, Id = "555555", Pid = "P5", TypZaznamu = "NES", Modul = "JINY_IS",
                              Stav = "dodavatel", SlaDeadline = reference.AddDays(-1) });
    db.SaveChangesForTests();

    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

    result.Should().HaveCount(1);
    result[0].Id.Should().Be(111111);
    result[0].TypZaznamu.Should().Be("NES");
    result[0].DniProdleni.Should().Be(5);
    result[0].Termin.Should().Be(reference.AddDays(-5));
}

[Fact]
public async Task GetProdleneAsync_PMP_UzivaDatResT_IgnorujeSlaDeadline()
{
    var reference = new DateTime(2026, 4, 23, 12, 0, 0);

    using var db = CreateDb();
    SeedIsHierarchy(db);
    db.HotZaznamy.AddRange(
        // PMP s dat_res_t v prodlení — VRÁTIT
        new HotZaznamEntity { Radek = 1, Id = "100001", Pid = "P1", TypZaznamu = "PMP", Modul = "TP_RZA",
                              Stav = "dodavatel", DatResT = reference.AddDays(-3), SlaDeadline = null },
        // PMP s dat_res_t v budoucnu — IGNOROVAT
        new HotZaznamEntity { Radek = 2, Id = "100002", Pid = "P2", TypZaznamu = "PMP", Modul = "TP_RZA",
                              Stav = "otevřeno", DatResT = reference.AddDays(5), SlaDeadline = null },
        // PMP bez dat_res_t — IGNOROVAT
        new HotZaznamEntity { Radek = 3, Id = "100003", Pid = "P3", TypZaznamu = "PMP", Modul = "TP_RZA",
                              Stav = "otevřeno", DatResT = null });
    db.SaveChangesForTests();

    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

    result.Should().HaveCount(1);
    result[0].Id.Should().Be(100001);
    result[0].TypZaznamu.Should().Be("PMP");
    result[0].DniProdleni.Should().Be(3);
}

[Fact]
public async Task GetProdleneAsync_PNF_UzivaDatResT_StejneJakoPmp()
{
    var reference = new DateTime(2026, 4, 23, 12, 0, 0);

    using var db = CreateDb();
    SeedIsHierarchy(db);
    db.HotZaznamy.Add(
        new HotZaznamEntity { Radek = 1, Id = "200001", Pid = "P1", TypZaznamu = "PNF", Modul = "TP_RZA",
                              Stav = "dodavatel", DatResT = reference.AddDays(-7), SlaDeadline = null });
    db.SaveChangesForTests();

    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

    result.Should().HaveCount(1);
    result[0].TypZaznamu.Should().Be("PNF");
    result[0].DniProdleni.Should().Be(7);
}

[Fact]
public async Task GetProdleneAsync_RadiOdNejstarsichVProdleni()
{
    var reference = new DateTime(2026, 4, 23, 12, 0, 0);

    using var db = CreateDb();
    SeedIsHierarchy(db);
    db.HotZaznamy.AddRange(
        new HotZaznamEntity { Radek = 1, Id = "111111", Pid = "P1", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "dodavatel", SlaDeadline = reference.AddDays(-2) },
        new HotZaznamEntity { Radek = 2, Id = "222222", Pid = "P2", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "dodavatel", SlaDeadline = reference.AddDays(-10) },
        new HotZaznamEntity { Radek = 3, Id = "333333", Pid = "P3", TypZaznamu = "NES", Modul = "R_EIS",
                              Stav = "dodavatel", SlaDeadline = reference.AddDays(-5) });
    db.SaveChangesForTests();

    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

    // nejvíce v prodlení první
    result.Select(r => r.Id).Should().Equal(222222, 333333, 111111);
    result.Select(r => r.DniProdleni).Should().Equal(10, 5, 2);
}

[Fact]
public async Task GetProdleneAsync_NeznamyIs_VraciPrazdny()
{
    using var db = CreateDb();
    SeedIsHierarchy(db);
    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetProdleneAsync(isId: 999, DateTime.UtcNow, CancellationToken.None);
    result.Should().BeEmpty();
}
```

- [ ] **Step 2: Spustit — FAIL (NotImplementedException)**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SqlInformacniSystemQueryServiceTests.GetProdleneAsync" --no-restore`
Expected: FAIL s NotImplementedException.

- [ ] **Step 3: Implementovat `GetProdleneAsync`**

V `SqlInformacniSystemQueryService.cs` nahraď metodu `GetProdleneAsync`:

```csharp
public async Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
    int isId, DateTime reference, CancellationToken ct)
{
    // Nejdřív zjistit všechny modul zkratky pod daným IS.
    var modulyProIs = await _db.HotModuly
        .Where(m => m.IdIS == isId)
        .Select(m => m.Zkratka)
        .ToListAsync(ct);

    if (modulyProIs.Count == 0)
        return Array.Empty<ProdlenyTicketDto>();

    // Dva samostatné dotazy — jeden pro NES (sla_deadline), druhý pro PMP+PNF (dat_res_t).
    // InMemory provider nemá DATEDIFF, proto počítáme dny v C#.

    // Memory feedback (Kontext §11): ticket bez z.Id je mimo scope PM Trackeru.
    var nesRaw = await _db.HotZaznamy
        .Where(z => z.TypZaznamu == "NES"
                 && z.Stav != ArchivStav
                 && z.Id != null && z.Id != ""
                 && z.Modul != null
                 && modulyProIs.Contains(z.Modul)
                 && z.SlaDeadline != null
                 && z.SlaDeadline < reference)
        .OrderBy(z => z.SlaDeadline)
        .Select(z => new
        {
            z.Id, z.Pid, z.TypZaznamu, z.Strucne, z.Dulezitost, z.Zavaznost,
            z.Modul, z.Dodavatel, z.Stav, Termin = z.SlaDeadline!.Value
        })
        .ToListAsync(ct);

    var pmpPnfRaw = await _db.HotZaznamy
        .Where(z => (z.TypZaznamu == "PMP" || z.TypZaznamu == "PNF")
                 && z.Stav != ArchivStav
                 && z.Id != null && z.Id != ""
                 && z.Modul != null
                 && modulyProIs.Contains(z.Modul)
                 && z.DatResT != null
                 && z.DatResT < reference)
        .OrderBy(z => z.DatResT)
        .Select(z => new
        {
            z.Id, z.Pid, z.TypZaznamu, z.Strucne, z.Dulezitost, z.Zavaznost,
            z.Modul, z.Dodavatel, z.Stav, Termin = z.DatResT!.Value
        })
        .ToListAsync(ct);

    var all = nesRaw.Concat(pmpPnfRaw)
        .OrderBy(x => x.Termin)
        .Select(x => new ProdlenyTicketDto(
            // z.Id != null && != "" garantuje filtr výše; int.Parse záměrně bez fallbacku —
            // non-numeric id v DB by byl strukturální nesoulad, ať rupne hlasitě.
            Id: int.Parse(x.Id!),
            Pid: x.Pid ?? string.Empty,
            TypZaznamu: x.TypZaznamu ?? string.Empty,
            Strucne: x.Strucne,
            Dulezitost: x.Dulezitost,
            Zavaznost: x.Zavaznost,
            Modul: x.Modul,
            Dodavatel: x.Dodavatel,
            Stav: x.Stav,
            Termin: x.Termin,
            DniProdleni: (int)Math.Floor((reference.Date - x.Termin.Date).TotalDays)))
        .ToList();

    return all;
}
```

- [ ] **Step 4: Spustit — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SqlInformacniSystemQueryServiceTests.GetProdleneAsync" --no-restore`
Expected: PASS všech 5 testů.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs \
        PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs
git commit -m "feat(sd): GetProdleneAsync — NES přes sla_deadline, PMP/PNF přes dat_res_t"
```

---

### Task 9: `SqlInformacniSystemQueryService.GetRozpocetAsync`

**Files:**
- Modify: `PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs`
- Modify: `PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs`

- [ ] **Step 1: Napsat failing testy**

Přidej do `SqlInformacniSystemQueryServiceTests.cs`:

```csharp
[Fact]
public async Task GetRozpocetAsync_VraciLimitACerpani()
{
    using var db = CreateDb();
    db.HotIs.Add(new HotIsEntity { Id = 1, Zkratka = "FIS", Aktivita = "Aktivní",
                                    Limit = 1000m, Cerpani = 750m });
    db.SaveChangesForTests();

    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetRozpocetAsync(1, CancellationToken.None);

    result.Should().NotBeNull();
    result!.IsId.Should().Be(1);
    result.IsZkratka.Should().Be("FIS");
    result.Limit.Should().Be(1000m);
    result.Cerpani.Should().Be(750m);
    result.ProcentoCerpani.Should().Be(75.0m);
}

[Fact]
public async Task GetRozpocetAsync_BezLimitu_ProcentoNull()
{
    using var db = CreateDb();
    db.HotIs.Add(new HotIsEntity { Id = 1, Zkratka = "FIS", Aktivita = "Aktivní",
                                    Limit = null, Cerpani = 500m });
    db.SaveChangesForTests();

    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetRozpocetAsync(1, CancellationToken.None);

    result!.ProcentoCerpani.Should().BeNull();
}

[Fact]
public async Task GetRozpocetAsync_NeznamyIs_VraciNull()
{
    using var db = CreateDb();
    var svc = new SqlInformacniSystemQueryService(db);
    var result = await svc.GetRozpocetAsync(999, CancellationToken.None);
    result.Should().BeNull();
}
```

- [ ] **Step 2: Spustit — FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SqlInformacniSystemQueryServiceTests.GetRozpocetAsync" --no-restore`
Expected: FAIL.

- [ ] **Step 3: Implementovat**

Nahraď `GetRozpocetAsync` v `SqlInformacniSystemQueryService.cs`:

```csharp
public async Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct)
{
    var i = await _db.HotIs.FirstOrDefaultAsync(x => x.Id == isId, ct);
    if (i is null) return null;

    return new IsRozpocetDto(
        IsId: i.Id,
        IsZkratka: (i.Zkratka ?? string.Empty).Trim(),
        Limit: i.Limit,
        Cerpani: i.Cerpani);
}
```

- [ ] **Step 4: Spustit — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SqlInformacniSystemQueryServiceTests.GetRozpocetAsync" --no-restore`
Expected: PASS všech 3 testů.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/SqlInformacniSystemQueryService.cs \
        PmTracker.Tests.Unit/ServiceDesk/SqlInformacniSystemQueryServiceTests.cs
git commit -m "feat(sd): GetRozpocetAsync — limit/čerpání per IS"
```

---

### Task 10: `DisabledInformacniSystemQueryService`

**Files:**
- Create: `PmTracker.ServiceDesk.Sql/DisabledInformacniSystemQueryService.cs`
- Create: `PmTracker.Tests.Unit/ServiceDesk/DisabledInformacniSystemQueryServiceTests.cs`

- [ ] **Step 1: Napsat failing testy**

Soubor `PmTracker.Tests.Unit/ServiceDesk/DisabledInformacniSystemQueryServiceTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class DisabledInformacniSystemQueryServiceTests
{
    private static readonly DisabledInformacniSystemQueryService _svc = new();

    [Fact]
    public async Task GetAktivniIsAsync_VraciPrazdny()
    {
        var r = await _svc.GetAktivniIsAsync(CancellationToken.None);
        r.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProdleneAsync_VraciPrazdny()
    {
        var r = await _svc.GetProdleneAsync(1, DateTime.UtcNow, CancellationToken.None);
        r.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRozpocetAsync_VraciNull()
    {
        var r = await _svc.GetRozpocetAsync(1, CancellationToken.None);
        r.Should().BeNull();
    }
}
```

- [ ] **Step 2: Spustit — FAIL**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "DisabledInformacniSystemQueryService" --no-restore`
Expected: FAIL (class neexistuje).

- [ ] **Step 3: Vytvořit service**

Soubor `PmTracker.ServiceDesk.Sql/DisabledInformacniSystemQueryService.cs`:

```csharp
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.ServiceDesk.Sql;

/// <summary>
/// Fallback implementace vrácená DI když <c>Ticketing:Enabled = false</c>.
/// Symetricky k <see cref="DisabledTicketingQueryService"/>.
/// </summary>
public sealed class DisabledInformacniSystemQueryService : IInformacniSystemQueryService
{
    public Task<IReadOnlyList<InformacniSystemDto>> GetAktivniIsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InformacniSystemDto>>(Array.Empty<InformacniSystemDto>());

    public Task<IReadOnlyList<ProdlenyTicketDto>> GetProdleneAsync(
        int isId, DateTime reference, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ProdlenyTicketDto>>(Array.Empty<ProdlenyTicketDto>());

    public Task<IsRozpocetDto?> GetRozpocetAsync(int isId, CancellationToken ct)
        => Task.FromResult<IsRozpocetDto?>(null);
}
```

- [ ] **Step 4: Spustit — PASS**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "DisabledInformacniSystemQueryService" --no-restore`
Expected: PASS všech 3 testů.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/DisabledInformacniSystemQueryService.cs \
        PmTracker.Tests.Unit/ServiceDesk/DisabledInformacniSystemQueryServiceTests.cs
git commit -m "feat(sd): DisabledInformacniSystemQueryService fallback"
```

---

### Task 11: DI registrace

**Files:**
- Modify: `PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs`

Kontext: Žádný Caching dekorátor (viz Kontext §9). V produkci je 3 IS, query triviální. `IInformacniSystemQueryService` se registruje přímo jako `SqlInformacniSystemQueryService` v enabled větvi, jako `DisabledInformacniSystemQueryService` v disabled větvi. Symetricky k `ITicketingQueryService` a `IVyjadreniQueryService`.

- [ ] **Step 1: Přidat registraci do existujícího extension**

V `ServiceDeskServiceCollectionExtensions.cs`:

**Do disabled větve** (`if (!options.Enabled)`) přidej:

```csharp
services.AddScoped<IInformacniSystemQueryService, DisabledInformacniSystemQueryService>();
```

(tj. vedle `DisabledTicketingQueryService` a `DisabledVyjadreniQueryService`)

**Do enabled větve** (za `services.AddScoped<IVyjadreniQueryService, SqlVyjadreniQueryService>();`) přidej:

```csharp
services.AddScoped<IInformacniSystemQueryService, SqlInformacniSystemQueryService>();
```

- [ ] **Step 2: Build — PASS**

Run: `dotnet build PmTracker.ServiceDesk.Sql --no-restore`
Expected: PASS.

- [ ] **Step 3: Smoke test celé SD suity**

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "FullyQualifiedName~ServiceDesk" --no-restore`
Expected: PASS všech testů.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs
git commit -m "feat(sd): DI registrace IInformacniSystemQueryService (Sql / Disabled)"
```

---

### Task 12: Finální validace

- [ ] **Step 1: Full build solution**

Run: `dotnet build --no-restore`
Expected: PASS, 0 warnings v novém kódu.

- [ ] **Step 2: Full test suite**

Run: `dotnet test -c Release --no-restore`
Expected: PASS všech testů (existující + nové).

- [ ] **Step 3: Analyzer warnings check**

Run: `dotnet build -c Release --no-restore /p:TreatWarningsAsErrors=false 2>&1 | grep -E "warning|CA|IDE" | head -50`
Expected: žádné nové warningy ze změněných souborů.

- [ ] **Step 4: Reflection shape test — DbContext mapuje 6 entit**

Vytvoř `PmTracker.Tests.Unit/ServiceDesk/TicketingReadOnlyDbContextShapeTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class TicketingReadOnlyDbContextShapeTests
{
    [Fact]
    public void DbContext_MapujeVsech6HotEntit()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new TicketingReadOnlyDbContext(opts);

        var mappedTypes = db.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .ToHashSet();

        mappedTypes.Should().Contain(new[]
        {
            typeof(HotZaznamEntity),
            typeof(HotKalkulaceEntity),
            typeof(HotVyjadreniEntity),
            typeof(HotSubsystemEntity),
            typeof(HotModulyEntity),
            typeof(HotIsEntity),
        }, "TicketingReadOnlyDbContext musí mapovat všech 6 HOT_* entit");
    }
}
```

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "TicketingReadOnlyDbContextShape" --no-restore`
Expected: PASS.

Poznámka: záměrně přes reflection nad `db.Model.GetEntityTypes()`, ne přes `grep` — test zůstane platný i po refactoru (např. přesunu mappingů do extension metody).

- [ ] **Step 5: Final commit (pokud nějaký refactor drobnost)**

Pokud během final validace vznikla nějaká drobná úprava:

```bash
git add -A
git commit -m "chore(sd): final tweaks after full validation"
```

Pokud ne, Sprint A je hotový.

---

## Deliverable

Po dokončení Sprintu A codebase obsahuje:

- ✅ Bug #1 fix (`HotZaznamEntity.Splneno` je `DateTime?`)
- ✅ Rozšířený `HotZaznamEntity` (+ 15+ sloupců pro dashboard queries)
- ✅ Nové entity `HotIsEntity`, `HotModulyEntity`, `HotSubsystemEntity` plně mapované
- ✅ Nový contract `IInformacniSystemQueryService` s 3 operacemi
- ✅ `SqlInformacniSystemQueryService` — reálná implementace nad TicketingReadOnlyDbContext
- ✅ `DisabledInformacniSystemQueryService` — fallback pro `Ticketing:Enabled = false`
- ✅ DI registrace (symetrická k existujícímu `ITicketingQueryService`, bez cache vrstvy — viz Kontext §9)
- ✅ Kompletní unit test pokrytí (shape + query + fallback + DbContext reflection shape)

**Co není ve Sprintu A** (bude ve Sprintu B/C):

- ❌ Sloupec `ServiceDeskInfoSystemId` na tabulce `projekty` (DB migrace — Sprint B)
- ❌ UI dropdown pro nastavení IS na projektu — Sprint B
- ❌ Authz klíč `projects.edit.servicedesk` — Sprint B
- ❌ Aktivace NES panelu v projektovém dashboardu (zatím placeholder zůstává) — Sprint B
- ❌ `Ticketing:ServicedeskBaseUrl` appsetting + helper pro zpětné linky — Sprint B
- ❌ Výzvy (rozšíření `zaznam_externi_odkazy`) — Sprint C

---

## Otevřené otázky (pro rozhodnutí před Sprintem B)

1. **Migrace existujících projektů na IS:** heuristika (dominantní subsystem → IS), manuál revize, nebo kombinace? (Sprint B.)
2. **`Ticketing:ServicedeskBaseUrl`:** přesný formát URL (host + protokol + path); fragment `servicedesk.fis.acr.*` z brainstormu je neúplný.
