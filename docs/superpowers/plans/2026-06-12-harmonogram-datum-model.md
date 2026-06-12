# Harmonogram — datum-model + 10 fixních kroků: Implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přepsat backend harmonogramu z offset-modelu na absolutní datumy a zrušit šablony/verzování ve prospěch 10 pevných kroků; frontend zůstává vizuálně.

**Architecture:** 10 kroků = konstanta v kódu (žádné DB šablony). Per záznam 10 řádků `zaznam_harmonogram_krok` s `plan_datum` + `skutecnost_datum` (NULL = nenastal). Kandidáti vytěžení zůstávají v `zaznam_harmonogram_vyjadreni_vazby` (vazba nově přes `poradi`); `skutecnost_datum` je cached výsledek (default MAX / K1-MIN / `preferred` override), udržuje harvest sync. Výpočet pracuje s absolutními daty (žádná offset aritmetika).

**Tech Stack:** .NET 8, EF Core (MS SQL), ruční SQL migrace (`db_upgrade_*.sql`), xUnit + FluentAssertions, vanilla ES modules (frontend).

**Spec:** `docs/superpowers/specs/2026-06-12-harmonogram-skutecnost-design.md`

---

## Strategie buildu (coexist → delete)

Přepis se dotýká sdílených typů napříč ~20 soubory. Aby byl build **co nejdéle zelený a review po fázích bezpečné**:
- **Fáze 1** přidá nový model **vedle** starého (build zelený).
- **Fáze 2–6** migrují konzumenty po jednom na nový model.
- **Fáze 7** smaže starý model + tabulky (build zelený, staré pryč).

Data jsou zahoditelná → DB migrace = DROP/CREATE bez backfillu; po nasazení znovu vytěžit.

---

## File Structure (co se vytvoří / mění / maže)

**Vytvořit:**
- `PmTracker.Web/Services/Schedules/HarmonogramKroky.cs` — konstanta 10 pevných kroků (poradi, kod, nazev, barva, manuální flag, harvest predikát).
- `PmTracker.Web/Models/Entities/ZaznamHarmonogramKrokEntity.cs` — nová entita řádku kroku.
- `PmTracker.Web/Data/Configuration/ScheduleKrokEntityConfiguration.cs` — EF mapping nové tabulky.
- `db_upgrade_1_4_0_harmonogram_datum_model.sql` — DROP starých tabulek/sloupce, CREATE `zaznam_harmonogram_krok`, ALTER vazby.

**Modifikovat (po fázích):**
- `ScheduleTimelineCalculator.cs`, `HarmonogramService.cs` — výpočet na datech.
- `ZaznamHarmonogramVyjadreniVazbaEntity.cs` (+ config) — `KrokKey`→`Poradi`.
- `PmTrackerEntities.cs` — odstranit `HarmonogramSablonaVerze` ze záznamu.
- `PmTrackerDbContext.cs` — DbSety.
- compose: `ProjectService.ScheduleComposition.cs`, `RecordEditorComposition.cs`, `ScheduleBlockComposition.cs`, `SchedulePreviewService.cs`.
- save/proposal: `RecordService.SaveRecord.cs`, `RecordProposalService.*`, `RecordProposalPayloadMapper.cs`.
- harvest: `HarmonogramSkutecnostSyncService.cs`, `HarmonogramSkutecnostResolver.cs`, `HarmonogramKrokDatumMapping.cs`, `VyjadreniHarvestService.cs`, `ManualActualKrokApplier.cs`.
- reporting: `ProjectDashboardService.cs`, `HomeDashboardService.cs`, `DashboardPriorityScoringService.cs`.
- frontend: `wwwroot/js/modules/schedule/block.js`.

**Smazat (Fáze 7):**
- `HarmonogramCatalogService.cs`, admin UI „harmonogram-kroky" (`CiselnikyController`/`DictionaryService` větev).
- entity `HarmonogramSablonaEntity`, `HarmonogramTypEntity`, `ZaznamHarmonogramHodnotaEntity` + jejich EF configy.

---

## FÁZE 1 — Základ: konstanta kroků + nová entita + DB (build zůstává zelený)

### Task 1.1: Konstanta 10 pevných kroků

**Files:**
- Create: `PmTracker.Web/Services/Schedules/HarmonogramKroky.cs`
- Test: `PmTracker.Tests.Unit/Schedule/HarmonogramKrokyTests.cs`

- [ ] **Step 1: Napsat failing test**

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramKrokyTests
{
    [Fact]
    public void Vsech_10_kroku_je_definovano_v_poradi_1_az_10()
    {
        HarmonogramKroky.Vse.Select(k => k.Poradi).Should().Equal(Enumerable.Range(1, 10));
    }

    [Fact]
    public void Manualni_kroky_jsou_2_5_8_9()
    {
        HarmonogramKroky.Vse.Where(k => k.JeManualni).Select(k => k.Poradi)
            .Should().Equal(2, 5, 8, 9);
    }

    [Theory]
    [InlineData(1, "K1")]
    [InlineData(3, "K3")]
    [InlineData(4, "K4_K7")]
    [InlineData(6, "K6")]
    [InlineData(7, "K4_K7")]
    [InlineData(10, "K10")]
    public void Harvestovane_kroky_maji_spravny_predikat(int poradi, string predikat)
    {
        HarmonogramKroky.Vse.Single(k => k.Poradi == poradi).HarvestPredikat.Should().Be(predikat);
    }
}
```

- [ ] **Step 2: Spustit test — má failnout (typ neexistuje)**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~HarmonogramKrokyTests"`
Expected: FAIL (compile error — `HarmonogramKroky` neexistuje)

- [ ] **Step 3: Implementovat konstantu**

```csharp
namespace PmTracker.Web.Services.Schedules;

public sealed record HarmonogramKrokDefinice(
    int Poradi,
    string Kod,
    string Nazev,
    string BarvaHex,
    bool JeManualni,
    string? HarvestPredikat);

public static class HarmonogramKroky
{
    public static readonly IReadOnlyList<HarmonogramKrokDefinice> Vse =
    [
        new(1,  "HS01", "1. priprava zadani dodavateli", "#EF4444", false, "K1"),
        new(2,  "HS02", "2. konzultace terminu s dodavatelem pred vytvorenim zadani", "#F97316", true,  null),
        new(3,  "HS03", "3. odeslani zadani dodavateli", "#F59E0B", false, "K3"),
        new(4,  "HS04", "4. dodani navrhu reseni", "#84CC16", false, "K4_K7"),
        new(5,  "HS05", "5. vyporadani pripominek", "#22C55E", true,  null),
        new(6,  "HS06", "6. odeslani pozadavku na vyrobu", "#14B8A6", false, "K6"),
        new(7,  "HS07", "7. dodani funkcionality dodavatelem", "#06B6D4", false, "K4_K7"),
        new(8,  "HS08", "8. pripominkovani", "#3B82F6", true,  null),
        new(9,  "HS09", "9. testovani", "#6366F1", true,  null),
        new(10, "HS10", "10. nasazeni do provozu", "#8B5CF6", false, "K10"),
    ];
}
```

- [ ] **Step 4: Spustit test — má projít**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~HarmonogramKrokyTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Schedules/HarmonogramKroky.cs PmTracker.Tests.Unit/Schedule/HarmonogramKrokyTests.cs
git commit -m "feat(harmonogram): konstanta 10 pevnych kroku (datum-model zaklad)"
```

### Task 1.2: Nová entita `ZaznamHarmonogramKrokEntity` + EF config + DbSet

**Files:**
- Create: `PmTracker.Web/Models/Entities/ZaznamHarmonogramKrokEntity.cs`
- Create: `PmTracker.Web/Data/Configuration/ScheduleKrokEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs` (přidat DbSet)

- [ ] **Step 1: Entita**

```csharp
namespace PmTracker.Web.Models.Entities;

public sealed class ZaznamHarmonogramKrokEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public byte Poradi { get; set; }                 // 1..10
    public DateTime? PlanDatum { get; set; }         // plánové datum konce kroku
    public DateTime? SkutecnostDatum { get; set; }   // NULL = krok nenastal; cached výsledek kandidáta
    public byte SkutecnostZdroj { get; set; }        // 0 Neznamo / 1 Automat / 2 Manual / 3 Historicka
    public byte SkutecnostRezim { get; set; }        // 0 Auto / 1 Manual
    public int? PreferredExterniOdkazId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 2: EF config** (následovat vzor `RecordEntityConfiguration.cs:290-318`)

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

public sealed class ScheduleKrokEntityConfiguration : IEntityTypeConfiguration<ZaznamHarmonogramKrokEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamHarmonogramKrokEntity> builder)
    {
        builder.ToTable("zaznam_harmonogram_krok");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.Poradi).HasColumnName("poradi");
        builder.Property(x => x.PlanDatum).HasColumnName("plan_datum").HasColumnType("date");
        builder.Property(x => x.SkutecnostDatum).HasColumnName("skutecnost_datum").HasColumnType("date");
        builder.Property(x => x.SkutecnostZdroj).HasColumnName("skutecnost_zdroj");
        builder.Property(x => x.SkutecnostRezim).HasColumnName("skutecnost_rezim");
        builder.Property(x => x.PreferredExterniOdkazId).HasColumnName("preferred_externi_odkaz_id");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(x => new { x.ZaznamId, x.Poradi })
            .IsUnique().HasDatabaseName("UQ_zaznam_harmonogram_krok_zaznam_poradi");
        builder.HasOne<ProjektovyZaznamEntity>().WithMany()
            .HasForeignKey(x => x.ZaznamId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3: DbSet** — v `PmTrackerDbContext.cs` přidat `public DbSet<ZaznamHarmonogramKrokEntity> ZaznamHarmonogramKroky => Set<ZaznamHarmonogramKrokEntity>();` a registraci config (dle stávajícího patternu ApplyConfiguration).

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: Build succeeded (nový typ koexistuje se starým)

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Models/Entities/ZaznamHarmonogramKrokEntity.cs PmTracker.Web/Data/Configuration/ScheduleKrokEntityConfiguration.cs PmTracker.Web/Data/PmTrackerDbContext.cs
git commit -m "feat(harmonogram): entita zaznam_harmonogram_krok + EF mapping"
```

### Task 1.3: SQL migrace (DROP staré + CREATE nové + ALTER vazby)

**Files:**
- Create: `db_upgrade_1_4_0_harmonogram_datum_model.sql`

- [ ] **Step 1: Napsat skript** (idempotentní, správné pořadí kvůli FK)

```sql
-- db_upgrade_1_4_0_harmonogram_datum_model.sql
-- Harmonogram: offset->datum model + 10 fixnich kroku. Data zahoditelna (DROP/CREATE).
SET XACT_ABORT ON;
BEGIN TRAN;

-- 1) Nova tabulka kroku
IF OBJECT_ID('dbo.zaznam_harmonogram_krok','U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_harmonogram_krok (
        id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_zaznam_harmonogram_krok PRIMARY KEY,
        zaznam_id INT NOT NULL,
        poradi TINYINT NOT NULL,
        plan_datum DATE NULL,
        skutecnost_datum DATE NULL,
        skutecnost_zdroj TINYINT NOT NULL CONSTRAINT DF_zhk_zdroj DEFAULT(0),
        skutecnost_rezim TINYINT NOT NULL CONSTRAINT DF_zhk_rezim DEFAULT(0),
        preferred_externi_odkaz_id INT NULL,
        updated_at DATETIME2 NOT NULL CONSTRAINT DF_zhk_updated DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT FK_zhk_zaznam FOREIGN KEY (zaznam_id)
            REFERENCES dbo.projektove_zaznamy(id) ON DELETE CASCADE,
        CONSTRAINT UQ_zaznam_harmonogram_krok_zaznam_poradi UNIQUE (zaznam_id, poradi)
    );
END

-- 2) Vazby: krok_key (uniqueidentifier) -> poradi (tinyint)
IF COL_LENGTH('dbo.zaznam_harmonogram_vyjadreni_vazby','poradi') IS NULL
    ALTER TABLE dbo.zaznam_harmonogram_vyjadreni_vazby ADD poradi TINYINT NULL;
-- (data zahoditelna: stare radky vazeb se nemigrujou, harvest je vytvori znovu)
DELETE FROM dbo.zaznam_harmonogram_vyjadreni_vazby;
IF COL_LENGTH('dbo.zaznam_harmonogram_vyjadreni_vazby','krok_key') IS NOT NULL
    ALTER TABLE dbo.zaznam_harmonogram_vyjadreni_vazby DROP COLUMN krok_key;

-- 3) Drop stary offset model + sloupec verze na zaznamu + FK
IF OBJECT_ID('dbo.zaznam_harmonogram_hodnoty','U') IS NOT NULL
    DROP TABLE dbo.zaznam_harmonogram_hodnoty;
-- FK projektove_zaznamy -> harmonogram_sablony nejdriv
DECLARE @fk SYSNAME = (SELECT name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('dbo.projektove_zaznamy') AND referenced_object_id = OBJECT_ID('dbo.harmonogram_sablony'));
IF @fk IS NOT NULL EXEC('ALTER TABLE dbo.projektove_zaznamy DROP CONSTRAINT ' + @fk);
IF COL_LENGTH('dbo.projektove_zaznamy','harmonogram_sablona_verze') IS NOT NULL
    ALTER TABLE dbo.projektove_zaznamy DROP COLUMN harmonogram_sablona_verze;
IF OBJECT_ID('dbo.ciselnik_harmonogram_typu','U') IS NOT NULL
    DROP TABLE dbo.ciselnik_harmonogram_typu;
IF OBJECT_ID('dbo.harmonogram_sablony','U') IS NOT NULL
    DROP TABLE dbo.harmonogram_sablony;

COMMIT;
```

- [ ] **Step 2: Ověřit syntaxi** proti dev DB (pokud dostupná) nebo manuální review pořadí DROP/FK.
- [ ] **Step 3: Commit**

```bash
git add db_upgrade_1_4_0_harmonogram_datum_model.sql
git commit -m "feat(harmonogram): SQL migrace datum-model (drop offset/sablony)"
```

> **Pozn.:** Skript dropuje sloupec/tabulky, na které se starý kód EF mapuje. Spustit ho až **po Fázi 7** (smazání starých entit), jinak EF startup hodí mismatch. V dev běhu lze do té doby pracovat na staré DB; ostré spuštění SQL = součást Fáze 7 acceptance.

---

## FÁZE 2 — Výpočet na datech (TDD)

**Cíl:** `ScheduleTimelineCalculator` + `HarmonogramService` počítají z absolutních dat dle spec sekce 2.
**Soubory:** `ScheduleTimelineCalculator.cs` (přepis), `HarmonogramService.cs` (build z dat, konstanta kroků), testy `ScheduleTimelineTests`/`HarmonogramSouhrnTests`.
**Klíčové testy (napsat první):**
- plán segment k = `[plan(k-1), plan(k)]`, plan(0)=start.
- skutečnost: jen vyplněné; krok k = `[skut(předchozí vyplněný), skut(k)]`; nevyplněné nulová šířka.
- skutečné dokončení = poslední vyplněný; krok10 nevyplněn → projekce na dnešek.
- překročení = `max(0, skut.dokončení − termín)`; stíháme = `≤ termín`.
- edge: `skut(k) < skut(předchozí)` → nezáporná šířka.
**Acceptance:** unit testy zelené; ostatní build může být dočasně červený (konzumenti migrují ve Fázi 3+).
**Detail kódu:** dopíše se těsně před exekucí proti aktuálnímu `ScheduleTimelineCalculator.cs`.

---

## FÁZE 3 — Čtení / UI compose

**Cíl:** karta + editor + block + preview čtou plan/skutecnost datumy z `zaznam_harmonogram_krok`.
**Soubory:** `ProjectService.ScheduleComposition.cs`, `RecordEditorComposition.cs`, `ScheduleBlockComposition.cs`, `SchedulePreviewService.cs`, dotčené ViewModels (`HarmonogramKrokEditViewModel`, `HarmonogramSouhrnViewModel`), šablona `_ScheduleBlock.cshtml` (per-krok datumy, hidden nevyplněné).
**Test:** komponentní/jednotkové testy compose (kde existují); jinak ověření v běžící appce (čára skutečnosti dle modelu).
**Acceptance:** karta i editor renderují stejně dle nového modelu; build zelený pro tyto soubory.
**Detail kódu:** JIT proti aktuálním souborům.

---

## FÁZE 4 — Zápis (save + proposal)

**Cíl:** uložení plánu i skutečnosti jako datumů; manual override + auto/manual switch; proposal/approve flow.
**Soubory:** `RecordService.SaveRecord.cs` (persist kroků), `RecordProposalService.DecisionCommands.cs`, `RecordProposalService.SubmitCommands.cs`, `RecordProposalPayloadMapper.cs`, `ManualActualKrokApplier.cs` (zruší konverzi datum→offset, ukládá datum).
**Test:** save-flow testy (manuální datum se uloží, auto reset, switch režimu).
**Acceptance:** uložení/načtení záznamu konzistentní; build zelený.
**Pozn.:** nejcitlivější fáze (concurrency stamp `updated_at`, manual/auto).
**Detail kódu:** JIT.

---

## FÁZE 5 — Harvest na datumy + vazby přes `poradi`

**Cíl:** vytěžení ukládá resolved datum přímo do `skutecnost_datum`; kandidáti ve vazbách přes `poradi`; default MAX / K1-MIN / preferred override.
**Soubory:** `HarmonogramSkutecnostSyncService.cs` (zrušit konverzi na offset, zapisovat datum), `HarmonogramSkutecnostResolver.cs` (Poradi místo KrokKey), `HarmonogramKrokDatumMapping.cs`, `VyjadreniHarvestService.cs` (synthetic K1 přes Poradi), `ZaznamHarmonogramVyjadreniVazbaEntity.cs` + config (KrokKey→Poradi).
**Test:** harvest unit testy (2 kandidáti → MAX vybrán; K1 → MIN; preferred override).
**Acceptance:** re-harvest naplní `skutecnost_datum` správně; build zelený; end-to-end ověření v appce.
**Detail kódu:** JIT.

---

## FÁZE 6 — Reporting

**Cíl:** dashboardy čtou datumy z nového modelu.
**Soubory:** `ProjectDashboardService.cs`, `HomeDashboardService.cs`, `DashboardPriorityScoringService.cs`.
**Test:** existující dashboard testy + ověření metrik.
**Acceptance:** metriky sedí; build zelený.
**Detail kódu:** JIT.

---

## FÁZE 7 — Úklid + ostrá DB migrace

**Cíl:** smazat starý model a admin UI; spustit SQL migraci.
**Soubory (smazat):** `HarmonogramCatalogService.cs`; entity `HarmonogramSablonaEntity`, `HarmonogramTypEntity`, `ZaznamHarmonogramHodnotaEntity` + jejich EF configy; DbSety; admin větev „harmonogram-kroky" v `CiselnikyController`/`DictionaryService` + partial; `ProjektovyZaznamEntity.HarmonogramSablonaVerze`.
**Krok DB:** spustit `db_upgrade_1_4_0_harmonogram_datum_model.sql` na dev DB.
**Acceptance:** `dotnet build` 0 chyb; **celá** unit sada zelená (kromě pre-existujících nesouvisejících); appka nastartuje proti migrované DB; re-harvest funguje.
**Detail kódu:** JIT.

---

## Self-Review (pokrytí spec)

- Spec 1.1 (konstanta kroků) → Task 1.1 ✓
- Spec 1.2 (tabulka kroku) → Task 1.2 + 1.3 ✓
- Spec 1.3/1.4 (vazby + multi-kandidát) → Fáze 5 ✓
- Spec 1.5 (DROP) → Task 1.3 + Fáze 7 ✓
- Spec 2 (výpočet) → Fáze 2 ✓
- Spec 3 (dotčený kód) → Fáze 3–6 ✓
- Spec 4 (fázování) → odpovídá ✓
- **Pozn. k placeholderům:** Fáze 2–7 záměrně nesou exaktní kód až do JIT detailingu před exekucí (přepis mutuje sdílené typy; předpis přesných editů teď by byl stale). Každá fáze se rozepíše na bite-sized TDD kroky bezprostředně před spuštěním.
