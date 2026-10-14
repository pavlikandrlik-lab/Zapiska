# Změna stavu úkolu platná k jednání — implementační plán

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: `superpowers:executing-plans` (inline exekuce v hlavní session, žádní subagenti). Kroky používají `- [ ]` pro sledování postupu.

**Cíl:** Při změně stavu úkolu uživatel zvolí, ke kterému datu změna platí (dnes, nebo datum otevřeného či připravovaného jednání). Tisk jednání pak ukazuje celý řádek úkolu, tedy sloupec Stav, pozastavení i ukončení, ke dni jednání.

**Architektura:** Historie stavů dostane sloupec `plati_od`, jediné datum, které čte tisk i dashboard. Vedle něj přibude `jednani_id`, vazba, přes kterou se `plati_od` udržuje při přeplánování jednání. Nabídku dat počítá čistá statická politika `StatusChangeEffectiveDatePolicy`. Data pro ni načítá statický dotaz `StatusChangeEffectiveDateQuery` (vzor `RecordVersionQuery`). Editor ho používá pro zobrazení, uložení záznamu pro ověření. Evaluátor tisku vrací jeden stav ke dni jednání a z něj se odvodí text sloupce Stav i obě podbarvení.

**Tech stack:** .NET 8, ASP.NET Core MVC + Razor, EF Core (SQL Server, bez EF migrací — schéma řídí ruční `db_upgrade_*.sql`), xUnit + FluentAssertions, Testcontainers (Azure SQL Edge), node:test pro JS, Playwright (bundled driver z E2E buildu) pro ověření v prohlížeči.

**Spec:** `docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md`

## Globální omezení

- **Pracuje se ve worktree**, ne v hlavním adresáři. Jiná session souběžně commituje do `main` v `/Users/Pavel.Andrlik/Documents/PM Tracker`. Worktree: `.claude/worktrees/zmena-stavu-k-jednani`, větev `feat/zmena-stavu-k-jednani`. Všechny cesty a příkazy v plánu jsou relativní ke kořeni worktree.
- Commit po každém úkolu. Na konci merge do lokálního `main` (merge commit). **Push jen na výslovný pokyn**, force-push nikdy, `--no-verify` nikdy.
- Commit message končí řádkem `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Texty v kódu, komentářích, hláškách i testech česky. Komentáře odkazují na spec datem `2026-10-13` a číslem paragrafu.
- **`plati_od` je jediné datum, které se čte** (tisk, dashboard, kontrola pořadí). `jednani_id` se pro výpočty nečte nikdy (spec §4.5).
- **`datum_zmeny` zůstává okamžik uložení v UTC** (`timeProvider.GetUtcNow().UtcDateTime`), beze změny (spec D3).
- „Dnes" = **místní datum** `timeProvider.GetLocalNow().Date`, nikdy datum z UTC.
- Spodní mez = `MAX(plati_od poslední změny stavu záznamu, datum založení)`. Stejné datum je povolené (spec §4.4).
- Uzavřené jednání = `MeetingStatePolicy.IsReadOnly` (kód `CLOSED`, název obsahuje „uzav", nebo `UzamklOsobaId`). Nabídka: dnes + jednání podle `CisloJednani` sestupně. Uzavřené nabídku ukončí, `DRAFT`/`OPEN` se nabídne, neznámý stav se přeskočí (spec §4.2).
- Výchozí volba: nejnovější nabízené `OPEN` → dnes → první volba (spec §4.3).
- Popisky voleb doslova: `Dnes (13.10.2026)`, `Jednání č. 889 – 22.10.2026 (příprava)`, `Jednání č. 888 – 08.10.2026` (pomlčka je `–`, datum `dd.MM.yyyy` invariantně).
- Chybové hlášky doslova podle spec §7.
- Tisk projektu a tisk záznamu ukazují dál **dnešní** stav (spec §4.8).
- Databázový skript: `db_upgrade_1_4_10_zmena_stavu_plati_od.sql` (`1_4_9` je obsazené převodem historické skutečnosti). Vyžaduje SQL Server 2016+.
- Pravidla otevírání a uzavírání jednání se **nedělají** (spec §10, rozhodnutí uživatele 2026-10-14).
- Známá selhání testů (nepočítají se jako regrese, ale vyjmenovat je): Integration `ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`; E2E 33 pre-existing jmen (posuzovat porovnáním jmen proti baseline z `main`, ne počtem). Unit, Api a JS mají být zelené celé.
- Integration a Api testy potřebují běžící Docker (Colima): `colima status`, případně `colima start`.

## Na co se při revizi zaměřit

Pět vstupů, které spec předpokládá, ale žádný test úkolu je jinak nepokryje. Ke každému je test přidaný do úkolu, kterému kód patří:

1. **Stav se v editoru změní a vrátí na původní** → server dostane nezměněný stav s jakoukoli hodnotou `StavPlatiK` (i nesmyslnou). Uložení musí projít a nesmí vzniknout řádek historie. → test v úkolu 5.
2. **Formulář bez pole `StavPlatiK`** (stránka otevřená před nasazením, jiné volající cesty) → změna platí k dnešnímu místnímu datu. → test v úkolu 1, v úkolu 5 zůstává zelený.
3. **Podvržené id jednání z jiného projektu** → odmítnout hláškou „neexistuje", nepřijmout. → test v úkolu 5.
4. **Úprava jednání bez změny data** (jen místo nebo čas) → historie stavů se nesmí dotknout ani tehdy, když v ní je rozpor. → test v úkolu 7.
5. **V témže uložení se posune datum založení za zvolené jednání** → odmítnout spodní mezí. → test v úkolu 5.

---

### Úkol 0: Worktree a dokumenty

**Soubory:**
- Přesunout: `docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md` (v hlavním adresáři je netrackovaný)
- Přesunout: `docs/superpowers/plans/2026-10-14-zmena-stavu-k-jednani.md` (tento plán, netrackovaný)

- [ ] **Krok 1: Ověřit stav hlavního adresáře a založit worktree**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git status --short
git worktree add .claude/worktrees/zmena-stavu-k-jednani -b feat/zmena-stavu-k-jednani main
```

Očekávat: `git status` ukáže jen oba netrackované dokumenty (spec a plán). Pokud ukazuje i cizí rozpracované soubory, **nesahat na ně**. Worktree se zakládá z commitu `main`, takže se ho netýkají.

- [ ] **Krok 2: Přesunout spec a plán do worktree**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
mv docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md .claude/worktrees/zmena-stavu-k-jednani/docs/superpowers/specs/
mv docs/superpowers/plans/2026-10-14-zmena-stavu-k-jednani.md .claude/worktrees/zmena-stavu-k-jednani/docs/superpowers/plans/
```

- [ ] **Krok 3: Ověřit, že číslo skriptu je volné**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/.claude/worktrees/zmena-stavu-k-jednani"
ls db_upgrade_1_4_1*.sql
grep -n "1_4_10" db_check_applied_upgrades.sql docs/technical/06-database-bootstrap-migrations.md
```

Očekávat: `1_4_10` neexistuje a grep nic nenajde. Pokud ho mezitím obsadila jiná práce, použít další volné číslo (`1_4_11`) **ve všech výskytech v plánu i ve specu** a pokračovat.

- [ ] **Krok 4: Sestavit a ověřit výchozí stav testů**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build
npm test
```

Očekávat: build bez chyb, Unit a JS zelené. Pokud ne, zastavit se a ohlásit uživateli, že `main` není zelený. Neopravovat cizí selhání.

- [ ] **Krok 5: Commit**

```bash
git add docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md docs/superpowers/plans/2026-10-14-zmena-stavu-k-jednani.md
git commit -m "$(cat <<'EOF'
docs(stav-k-jednani): spec a implementační plán změny stavu platné k jednání

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 1: Sloupce `plati_od` a `jednani_id`

**Soubory:**
- Vytvořit: `db_upgrade_1_4_10_zmena_stavu_plati_od.sql`
- Změnit: `docs/technical/06-database-bootstrap-migrations.md` (§5.1 seznam skriptů)
- Změnit: `db_check_applied_upgrades.sql`
- Změnit: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (`ZaznamHistorieStavuZaznamuEntity`)
- Změnit: `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` (`RecordStateHistoryEntityConfiguration`)
- Změnit: `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs`
- Změnit: `PmTracker.Web/Services/RecordService.SaveRecord.cs` (zápis řádku historie stavů)
- Test: `PmTracker.Tests.Unit/Common/StatusEffectiveDateMigrationRegistrationTests.cs`
- Test: `PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs`

**Rozhraní:**
- Poskytuje dál: `ZaznamHistorieStavuZaznamuEntity.PlatiOd` (`DateTime`, sloupec `date`, NOT NULL), `ZaznamHistorieStavuZaznamuEntity.JednaniId` (`int?`); `SqlStartupValidatorHostedService.StatusEffectiveDateColumns`; v testech pomocníci `ArrangeTaskAsync`, `BuildEditCommandAsync`, `PragueTimeProvider`.

- [ ] **Krok 1: Napsat padající unit test registrace**

`PmTracker.Tests.Unit/Common/StatusEffectiveDateMigrationRegistrationTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>
/// db_upgrade_1_4_10 — datum, ke kterému změna stavu platí, a vazba na jednání
/// (spec 2026-10-13 §5).
/// </summary>
public sealed class StatusEffectiveDateMigrationRegistrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_10_zmena_stavu_plati_od.sql";

    [Fact]
    public void Migrace_JeVBootstrapSeznamu_AVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"))
            .Should().Contain(MigrationFile);
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_10_zmena_stavu_plati_od");
    }

    [Fact]
    public void Migrace_HlidaVerziServeru_APrevadiUtcNaMistniDatum_VTransakci()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("SET XACT_ABORT ON").And.Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
        sql.Should().Contain("ProductMajorVersion", "AT TIME ZONE vyžaduje SQL Server 2016+");
        sql.Should().Contain("AT TIME ZONE ''Central Europe Standard Time''",
            "staré řádky mají datum_zmeny v UTC, plati_od je místní datum");
        sql.Should().Contain("plati_od DATE NOT NULL");
        sql.Should().Contain("FK_zaznam_historie_stavu_zaznamu_jednani");
        sql.Should().Contain("IX_zaznam_historie_stavu_zaznamu_jednani_id");
    }

    [Fact]
    public void StartovaciKontrola_VyzadujeObaSloupce()
    {
        SqlStartupValidatorHostedService.StatusEffectiveDateColumns.Should().BeEquivalentTo(new[]
        {
            ("dbo.zaznam_historie_stavu_zaznamu", "plati_od"),
            ("dbo.zaznam_historie_stavu_zaznamu", "jednani_id"),
        });
    }

    [Theory]
    [InlineData(nameof(ZaznamHistorieStavuZaznamuEntity.PlatiOd), "plati_od")]
    [InlineData(nameof(ZaznamHistorieStavuZaznamuEntity.JednaniId), "jednani_id")]
    public void Entity_JeNamapovanaNaSloupec(string property, string column)
    {
        var prop = HistoryEntityType().FindProperty(property);

        prop.Should().NotBeNull();
        prop!.GetColumnName().Should().Be(column);
    }

    [Fact]
    public void PlatiOd_JeDatumBezCasu()
        => HistoryEntityType().FindProperty(nameof(ZaznamHistorieStavuZaznamuEntity.PlatiOd))!
            .GetColumnType().Should().Be("date");

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType HistoryEntityType()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new PmTrackerDbContext(opts);
        return db.Model.FindEntityType(typeof(ZaznamHistorieStavuZaznamuEntity))!;
    }
}
```

- [ ] **Krok 2: Napsat padající integrační test**

`PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Změna stavu platná k jednání (spec 2026-10-13): co uložení změny stavu zapíše do historie,
/// co nabídne editor a jak se historie chová při přeplánování a smazání jednání.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class StatusChangeEffectiveDateDataStoreTests
{
    // 13.10.2026 00:30 v Praze = 12.10. 22:30 UTC — místní a UTC datum se liší.
    private static readonly DateTimeOffset JustAfterMidnightInPrague = new(2026, 10, 12, 22, 30, 0, TimeSpan.Zero);
    private static readonly DateTime RecordFoundedOn = new(2026, 9, 1);

    private readonly SqlIntegrationFixture _fixture;

    public StatusChangeEffectiveDateDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    /// <summary>Review focus 2: formulář bez pole StavPlatiK — změna platí k dnešnímu místnímu datu.</summary>
    [Fact]
    public async Task SaveRecord_StatusChangeWithoutChoice_IsEffectiveOnLocalToday_AndKeepsUtcMomentOfSaving()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_today");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(JustAfterMidnightInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFTODAY");

        store.SaveRecord(await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode), arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == arranged.RecordId);
        change.DatumZmeny.Should().Be(new DateTime(2026, 10, 12, 22, 30, 0), "datum_zmeny zůstává okamžik uložení v UTC");
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 13), "„dnes“ je místní datum, ne datum v UTC");
        change.JednaniId.Should().BeNull();
    }

    private sealed record ArrangedTask(int ProjectId, int RecordId, CurrentUserContextViewModel User, string DoneStatusCode);

    private static async Task<ArrangedTask> ArrangeTaskAsync(PmTrackerDbContext dbContext, string marker)
    {
        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Admin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Owner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", marker);

        // Založení před všemi jednáními v testech, aby spodní mez (spec §4.4) nic nevyřadila.
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = RecordFoundedOn;
        await dbContext.SaveChangesAsync();

        var doneStatusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();

        return new ArrangedTask(
            projectId,
            recordId,
            IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true),
            doneStatusCode);
    }

    private static async Task<SaveRecordCommand> BuildEditCommandAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        string statusCode,
        DateTime? datumZalozeni = null)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).SingleAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).SingleAsync();

        return new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = datumZalozeni ?? record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu
        };
    }

    private sealed class PragueTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");
    }
}
```

- [ ] **Krok 3: Spustit testy a ověřit, že padají**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StatusEffectiveDateMigrationRegistrationTests"
```

Očekávat: chyba překladu, `PlatiOd`, `JednaniId` a `StatusEffectiveDateColumns` neexistují. Integrační test se zatím nepřeloží ze stejného důvodu.

- [ ] **Krok 4: Vytvořit databázový skript**

`db_upgrade_1_4_10_zmena_stavu_plati_od.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_4_10_zmena_stavu_plati_od.sql
--
-- Historie stavů úkolu: datum, ke kterému změna platí, a vazba na jednání.
--
-- KONTEXT (spec docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md):
--   Změna stavu dosud platila k okamžiku uložení. Zápis z jednání zpracovaný o pár dní
--   později pak v tisku toho jednání ukazoval stav, který k datu jednání ještě neplatil.
--   Uživatel nově volí, ke kterému datu změna platí: dnes, nebo datum jednání.
--
-- SÉMANTIKA:
--   * plati_od (DATE, NOT NULL) — místní datum, ke kterému změna platí. Jediné datum,
--     které čte tisk jednání i dashboard.
--   * jednani_id (INT, NULL) — jednání, ke kterému změna patří. Aplikace přes něj
--     při přeplánování jednání přepíše plati_od. Pro výpočty se nečte.
--   * datum_zmeny zůstává beze změny: okamžik uložení v UTC (audit).
--
-- ROZSAH MIGRACE:
--   * Přidá oba sloupce, cizí klíč na dbo.jednani (NO ACTION — vazbu ruší aplikace
--     při mazání jednání) a index pro přeplánování a mazání jednání.
--   * Existujícím řádkům doplní plati_od převodem datum_zmeny z UTC na místní datum
--     (Central Europe Standard Time). Opraví tím i posun o den u změn uložených
--     mezi 0:00 a 2:00 místního času.
--
-- POŽADAVEK: SQL Server 2016 nebo novější (AT TIME ZONE). Na starší verzi skript
--   skončí chybou a nic nezmění.
--
-- SPUŠTĚNÍ: pod účtem s db_owner na cílové databázi, PŘED startem nové verze aplikace
--   (bez sloupců aplikace nenastartuje — ohlásí název tohoto skriptu).
--   sqlcmd -S <SQL_HOST>\<INSTANCE> -E -d PM_Tracker -b -i db_upgrade_1_4_10_zmena_stavu_plati_od.sql
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.10] Změna stavu platná k datu — start';

IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) < 13
BEGIN
    RAISERROR(N'Skript vyžaduje SQL Server 2016 nebo novější (AT TIME ZONE). Databáze zůstala beze změny.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.zaznam_historie_stavu_zaznamu', N'plati_od') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ADD plati_od DATE NULL;
    PRINT N'[1.4.10] sloupec plati_od přidán';
END

-- Dynamicky: sloupec mohl vzniknout až v tomto batchi, statický příkaz by neprošel překladem.
EXEC sp_executesql N'
UPDATE dbo.zaznam_historie_stavu_zaznamu
SET plati_od = CAST((datum_zmeny AT TIME ZONE ''UTC'') AT TIME ZONE ''Central Europe Standard Time'' AS date)
WHERE plati_od IS NULL;';

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu')
             AND name = N'plati_od'
             AND is_nullable = 1)
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ALTER COLUMN plati_od DATE NOT NULL;';
    PRINT N'[1.4.10] plati_od doplněn u existujících řádků a nastaven jako NOT NULL';
END

IF COL_LENGTH(N'dbo.zaznam_historie_stavu_zaznamu', N'jednani_id') IS NULL
BEGIN
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ADD jednani_id INT NULL;
    PRINT N'[1.4.10] sloupec jednani_id přidán';
END

IF OBJECT_ID(N'dbo.FK_zaznam_historie_stavu_zaznamu_jednani', N'F') IS NULL
BEGIN
    EXEC sp_executesql N'
    ALTER TABLE dbo.zaznam_historie_stavu_zaznamu
        ADD CONSTRAINT FK_zaznam_historie_stavu_zaznamu_jednani
        FOREIGN KEY (jednani_id) REFERENCES dbo.jednani (id);';
    PRINT N'[1.4.10] cizí klíč na dbo.jednani přidán';
END

-- Obyčejný (ne filtrovaný) index: filtrovaný vyžaduje QUOTED_IDENTIFIER ON, který sqlcmd bez -I nezapíná.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'dbo.zaznam_historie_stavu_zaznamu')
                 AND name = N'IX_zaznam_historie_stavu_zaznamu_jednani_id')
BEGIN
    EXEC sp_executesql N'
    CREATE INDEX IX_zaznam_historie_stavu_zaznamu_jednani_id
        ON dbo.zaznam_historie_stavu_zaznamu (jednani_id);';
    PRINT N'[1.4.10] index jednani_id přidán';
END

COMMIT TRANSACTION;

PRINT N'[1.4.10] hotovo';
```

- [ ] **Krok 5: Zaregistrovat skript v bootstrap seznamu a v kontrolním skriptu**

V `docs/technical/06-database-bootstrap-migrations.md` (§5.1) nahradit řádek:

```markdown
38. `db_seed_dev_admin.sql` (pouze neprodukční prostředí)
```

za:

```markdown
38. `db_upgrade_1_4_10_zmena_stavu_plati_od.sql`
39. `db_seed_dev_admin.sql` (pouze neprodukční prostředí)
```

Pokud mezitím přibyly další položky, zařadit skript za poslední `db_upgrade_*` a seed přečíslovat. Testy čtou tento seznam a zakládají z něj testovací databázi (`tests/Common/RepositoryPaths.cs`).

V `db_check_applied_upgrades.sql` vložit **za** blok naplnění `@historicka` (končí řádkem `N'@c INT OUTPUT', @c = @historicka OUTPUT;`):

```sql

DECLARE @platiOd INT = CASE
    WHEN COL_LENGTH(N'dbo.zaznam_historie_stavu_zaznamu', N'plati_od') IS NOT NULL
     AND COL_LENGTH(N'dbo.zaznam_historie_stavu_zaznamu', N'jednani_id') IS NOT NULL
    THEN 1 ELSE 0 END;
    -- 1_4_10: datum, ke kterému změna stavu platí, a vazba na jednání
```

a **za** blok `INSERT @r VALUES (380, N'db_upgrade_1_4_9_historicka_skutecnost', …);`:

```sql

INSERT @r VALUES (390, N'db_upgrade_1_4_10_zmena_stavu_plati_od',
    CASE WHEN @platiOd = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupce zaznam_historie_stavu_zaznamu.plati_od a jednani_id (bez nich aplikace nenastartuje)');
```

Do výstupu „3) Poznámky k interpretaci" přidat za poslední položku `(6, …)` čárku a řádek:

```sql
    (7, N'db_upgrade_1_4_10 spusť PŘED startem nové verze aplikace (bez sloupců nenastartuje). Vyžaduje SQL Server 2016 nebo novější.')
```

- [ ] **Krok 6: Doplnit entitu a mapování**

V `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` nahradit třídu `ZaznamHistorieStavuZaznamuEntity`:

```csharp
public sealed class ZaznamHistorieStavuZaznamuEntity
{
    public int Id { get; set; }
    public int ZaznamId { get; set; }
    public int PuvodniStav { get; set; }
    public int NovyStav { get; set; }

    /// <summary>Okamžik uložení (UTC) — jen audit; stav k datu se počítá z <see cref="PlatiOd"/>.</summary>
    public DateTime DatumZmeny { get; set; }

    /// <summary>Místní datum, ke kterému změna platí — jediné datum pro tisk i dashboard (spec 2026-10-13 §4.5).</summary>
    public DateTime PlatiOd { get; set; }

    /// <summary>Jednání, ke kterému změna patří; jen udržuje PlatiOd při přeplánování (spec 2026-10-13 §4.6).</summary>
    public int? JednaniId { get; set; }
}
```

V `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` v `RecordStateHistoryEntityConfiguration.Configure` za řádek s `DatumZmeny` přidat:

```csharp
        builder.Property(x => x.PlatiOd).HasColumnName("plati_od").HasColumnType("date");
        builder.Property(x => x.JednaniId).HasColumnName("jednani_id");
```

- [ ] **Krok 7: Startovní kontrola sloupců**

V `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs` za pole `ProstyTextColumns` přidat:

```csharp
    /// <summary>
    /// Sloupce historie stavů (db_upgrade_1_4_10). Bez nich spadne každé uložení změny stavu
    /// i tisk jednání, proto je start musí ohlásit i s názvem skriptu.
    /// </summary>
    internal static readonly (string Table, string Column)[] StatusEffectiveDateColumns =
    {
        ("dbo.zaznam_historie_stavu_zaznamu", "plati_od"),
        ("dbo.zaznam_historie_stavu_zaznamu", "jednani_id"),
    };
```

a v metodě validace hned za smyčku přes `ProstyTextColumns`:

```csharp
        foreach (var (table, column) in StatusEffectiveDateColumns)
        {
            if (!await HasColumnAsync(dbContext, table, column, ct))
            {
                throw new InvalidOperationException(
                    $"V DB chybí sloupec {table}.{column}. Spusťte db_upgrade_1_4_10_zmena_stavu_plati_od.sql.");
            }
        }
```

- [ ] **Krok 8: Ukládat `PlatiOd` u nové změny stavu**

V `PmTracker.Web/Services/RecordService.SaveRecord.cs` v bloku `if (oldStatus != entity.StavUkoluId && oldStatus.HasValue && entity.StavUkoluId.HasValue)` nahradit inicializátor:

```csharp
                    dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
                    {
                        ZaznamId = entity.Id,
                        PuvodniStav = oldStatus.Value,
                        NovyStav = entity.StavUkoluId.Value,
                        DatumZmeny = timeProvider.GetUtcNow().UtcDateTime,
                        PlatiOd = GetLocalNow().Date
                    });
```

(Volbu jednání doplní úkol 5. Do té doby platí změna k dnešnímu místnímu datu, jako dosud, ale bez chyby s časovým pásmem.)

- [ ] **Krok 9: Spustit testy a ověřit, že prochází**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build --filter "FullyQualifiedName~StatusEffectiveDateMigrationRegistrationTests"
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~StatusChangeEffectiveDateDataStoreTests"
```

Očekávat: Unit 6/6, Integration 1/1. Integrační test zároveň ověří, že skript projde na Azure SQL Edge (testovací databáze se zakládá z bootstrap seznamu). **Kdyby Azure SQL Edge `AT TIME ZONE` nepodporoval, zastavit se a ohlásit to uživateli.** Neobcházet to, produkce běží na plném SQL Serveru.

- [ ] **Krok 10: Commit**

```bash
git add db_upgrade_1_4_10_zmena_stavu_plati_od.sql docs/technical/06-database-bootstrap-migrations.md db_check_applied_upgrades.sql \
  PmTracker.Web/Models/Entities/PmTrackerEntities.cs PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs \
  PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs PmTracker.Web/Services/RecordService.SaveRecord.cs \
  PmTracker.Tests.Unit/Common/StatusEffectiveDateMigrationRegistrationTests.cs \
  PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): historie stavů nese datum platnosti a vazbu na jednání (db_upgrade_1_4_10)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 2: Tisk jednání a dashboard čtou `plati_od`

**Soubory:**
- Změnit: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` (`ResolveTaskStatusAtDate`, dotaz na historii v `BuildExportRecordsAsync`)
- Změnit: `PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDatasetLoader.cs`
- Test (změna): `PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs`
- Test (změna): `PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs`
- Test (změna): `PmTracker.Tests.Integration/DataStore/ProjectMembershipDataStoreTests.cs`
- Test (změna): `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs`

**Rozhraní:**
- Používá z úkolu 1: `ZaznamHistorieStavuZaznamuEntity.PlatiOd`.
- Rozhraní evaluátoru se v tomto úkolu nemění.

- [ ] **Krok 1: Napsat padající testy**

V `PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs` doplnit do `ClosedOn20August()` řádek `PlatiOd = new DateTime(2026, 8, 20),` (za `DatumZmeny`) a přidat test:

```csharp
    /// <summary>
    /// Spec 2026-10-13 §4.7 — zápis z jednání 8.10 zpracovaný až 13.10: změna uložená 13.10
    /// platí k 8.10, takže v tisku jednání 8.10 je úkol ukončený.
    /// </summary>
    [Fact]
    public void IsCompletedForMeetingPrint_UsesPlatiOd_NotTheMomentOfSaving()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> savedLate =
        [
            new()
            {
                Id = 1,
                ZaznamId = 100,
                PuvodniStav = RunningStateId,
                NovyStav = DoneStateId,
                DatumZmeny = new DateTime(2026, 10, 13, 7, 0, 0),
                PlatiOd = new DateTime(2026, 10, 8)
            }
        ];

        evaluator.IsCompletedForMeetingPrint(Record(DoneStateId), TaskStates, savedLate, new DateTime(2026, 10, 8))
            .Should().BeTrue("rozhoduje datum platnosti, ne okamžik uložení");
    }
```

V `PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs` nahradit vložení řádku historie:

```csharp
        var dokonceniDatum = new DateTime(2025, 6, 15);
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = nonFinalStateId,
            NovyStav = finalStateId,
            // Uloženo o měsíc později, platí k 15.6. — dashboard bere datum platnosti (spec 2026-10-13 §4.9).
            DatumZmeny = new DateTime(2025, 7, 20, 8, 0, 0),
            PlatiOd = dokonceniDatum
        });
```

(Assert `r.DatumDokonceni == dokonceniDatum` zůstává.)

V `PmTracker.Tests.Integration/DataStore/ProjectMembershipDataStoreTests.cs` (test s jednáními 860/861) doplnit do obou řádků historie `PlatiOd` se stejným datem jako `DatumZmeny`: `PlatiOd = new DateTime(2025, 8, 30)` a `PlatiOd = new DateTime(2025, 8, 10)`.

V `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` (test `BuildMeetingTemplate_MarksCompletionByMeetingDate_NotByTodaysState`) doplnit do řádku historie `PlatiOd = new DateTime(2026, 3, 20)`.

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExportRecordVisibilityEvaluatorTests"
dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ZakladniDatasetLoaderTests"
```

Očekávat: `IsCompletedForMeetingPrint_UsesPlatiOd_NotTheMomentOfSaving` selže (dnešní kód bere 13.10). Dashboard test selže (vrátí 20.7.).

- [ ] **Krok 3: Přepočet stavu nad `plati_od`**

V `ExportProjectionBuilders.cs` v `ResolveTaskStatusAtDate` nahradit podmínku:

```csharp
            // Spec 2026-10-13 §4.7 — rozhoduje datum platnosti, ne okamžik uložení.
            if (change.PlatiOd.Date <= targetDate.Date)
```

V `BuildExportRecordsAsync` v dotazu na `ZaznamHistorieStavuZaznamu` nahradit řazení:

```csharp
                .OrderByDescending(x => x.PlatiOd)
                .ThenByDescending(x => x.Id)
```

- [ ] **Krok 4: Dashboard nad `plati_od`**

V `ZakladniDatasetLoader.cs` nahradit komentář a agregaci:

```csharp
        // Datum dokončení = datum platnosti posledního přechodu do koncového (final) stavu
        // (spec 2026-10-13 §4.9). Záznam, který do žádného final stavu nevstoupil, zůstává null.
```

```csharp
            .Select(g => new { ZaznamId = g.Key, Datum = g.Max(x => x.PlatiOd) })
```

- [ ] **Krok 5: Spustit testy a ověřit, že prochází**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build --filter "FullyQualifiedName~ExportRecordVisibilityEvaluatorTests"
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~ZakladniDatasetLoaderTests|FullyQualifiedName~ExportTemplateUseCaseTests|FullyQualifiedName~ProjectMembershipDataStoreTests"
```

Očekávat: vše zelené.

- [ ] **Krok 6: Commit**

```bash
git add PmTracker.Web/Services/Export/ExportProjectionBuilders.cs PmTracker.Web/Services/ProjectDashboard/Zakladni/ZakladniDatasetLoader.cs \
  PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs PmTracker.Tests.Integration/DataStore/ZakladniDatasetLoaderTests.cs \
  PmTracker.Tests.Integration/DataStore/ProjectMembershipDataStoreTests.cs PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): tisk jednání a dashboard počítají stav podle data platnosti změny

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 3: Celý řádek tisku jednání ke dni jednání (U6)

**Soubory:**
- Změnit: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` (rozhraní a implementace evaluátoru, `BuildExportRecordsAsync`)
- Test (přepsat): `PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs`
- Test (přidat): `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs`
- Dokumentace: `docs/wiki/export/tisk-jednani.md`, `docs/superpowers/specs/2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md`

**Rozhraní:**
- Používá z úkolu 2: přepočet nad `PlatiOd` a řazení `(PlatiOd desc, Id desc)`.
- Poskytuje dál: `IExportRecordVisibilityEvaluator.ResolveStatusForMeetingPrint(ProjektovyZaznamEntity record, IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory, DateTime anchorMeetingDate) → int?`. **Odstraněno:** `IsCompletedForMeetingPrint`.

- [ ] **Krok 1: Přepsat unit testy evaluátoru**

`PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs` celý nahradit:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Stav úkolu ke dni jednání (spec 2026-10-13 §4.7). Z něj se v tisku jednání odvodí
/// sloupec Stav, pozastavení i ukončení (§4.8). Testy pracují s entitami, databázi nepotřebují.
/// </summary>
public sealed class ExportRecordVisibilityEvaluatorTests
{
    private const int RunningStateId = 1;
    private const int DoneStateId = 2;

    private static ProjektovyZaznamEntity Record(int? currentStateId) => new()
    {
        Id = 100,
        ProjektId = 1,
        KategorieId = 1,
        SubsystemId = 1,
        VlastnikId = 1,
        CisloZaznamu = 1,
        Nazev = "Testovací úkol",
        StavUkoluId = currentStateId,
        DatumZalozeni = new DateTime(2026, 1, 5)
    };

    private static ZaznamHistorieStavuZaznamuEntity Change(int id, int from, int to, DateTime platiOd, DateTime? savedAt = null) => new()
    {
        Id = id,
        ZaznamId = 100,
        PuvodniStav = from,
        NovyStav = to,
        DatumZmeny = savedAt ?? platiOd,
        PlatiOd = platiOd
    };

    /// <summary>Stejné řazení, v jakém historii předává BuildExportRecordsAsync.</summary>
    private static IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> NewestFirst(params ZaznamHistorieStavuZaznamuEntity[] changes)
        => changes.OrderByDescending(x => x.PlatiOd).ThenByDescending(x => x.Id).ToList();

    [Fact]
    public void ResolveStatus_ReturnsPreviousState_WhenChangeTakesEffectAfterTheMeeting()
    {
        var history = NewestFirst(Change(1, RunningStateId, DoneStateId, new DateTime(2026, 8, 20)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 6, 10))
            .Should().Be(RunningStateId, "na červnovém zápise úkol ještě běžel");
    }

    [Fact]
    public void ResolveStatus_ReturnsNewState_WhenChangeTookEffectBeforeTheMeeting()
    {
        var history = NewestFirst(Change(1, RunningStateId, DoneStateId, new DateTime(2026, 8, 20)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 9, 1))
            .Should().Be(DoneStateId);
    }

    [Fact]
    public void ResolveStatus_CountsChangeEffectiveOnTheMeetingDayAsApplied()
    {
        var history = NewestFirst(Change(1, RunningStateId, DoneStateId, new DateTime(2026, 8, 20)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 8, 20))
            .Should().Be(DoneStateId, "změna platná v den jednání se počítá jako platná");
    }

    [Fact]
    public void ResolveStatus_UsesPlatiOd_NotTheMomentOfSaving()
    {
        var history = NewestFirst(Change(
            1, RunningStateId, DoneStateId, platiOd: new DateTime(2026, 10, 8), savedAt: new DateTime(2026, 10, 13, 7, 0, 0)));

        new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(DoneStateId), history, new DateTime(2026, 10, 8))
            .Should().Be(DoneStateId);
    }

    /// <summary>
    /// Spec 2026-10-13 §4.4 — omylem ukončeno k 889 a znovuotevřeno zase k 889: obě změny
    /// platí týž den, rozhoduje pořadí uložení a úkol nikde nevyjde jako ukončený.
    /// </summary>
    [Fact]
    public void ResolveStatus_SameDayCloseAndReopen_LastSavedWins()
    {
        var history = NewestFirst(
            Change(1, RunningStateId, DoneStateId, new DateTime(2026, 10, 22)),
            Change(2, DoneStateId, RunningStateId, new DateTime(2026, 10, 22)));
        var evaluator = new ExportRecordVisibilityEvaluator();

        evaluator.ResolveStatusForMeetingPrint(Record(RunningStateId), history, new DateTime(2026, 10, 22))
            .Should().Be(RunningStateId);
        evaluator.ResolveStatusForMeetingPrint(Record(RunningStateId), history, new DateTime(2026, 10, 8))
            .Should().Be(RunningStateId);
    }

    [Fact]
    public void ResolveStatus_ReturnsNull_WhenRecordHasNoTaskState()
        => new ExportRecordVisibilityEvaluator()
            .ResolveStatusForMeetingPrint(Record(null), [], new DateTime(2026, 9, 1))
            .Should().BeNull("informace a rozhodnutí nemají stav úkolu");
}
```

(Pravidlo U1, tedy že pozastavený stav označený jako koncový není ukončený, dál hlídá `TaskStatusRulesTests`. Ukončenost už neurčuje evaluátor, ale `TaskStatusRules.IsCompleted` nad stavem z této metody.)

- [ ] **Krok 2: Přidat integrační testy tisku**

Do `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` přidat (třída už má `FixedTimeProvider`; doplnit `using PmTracker.Web.Data;`, pokud chybí):

```csharp
    /// <summary>
    /// Spec 2026-10-13 §4.8 — tabulka z návrhu. Úkol ukončený 13.10. při zpracování zápisu
    /// s volbou „k jednání 888“, „dnes“ a „k jednání 889“. Sloupec Stav, podbarvení
    /// i viditelnost se v každém tisku řídí stavem ke dni toho jednání.
    /// </summary>
    [Fact]
    public async Task BuildMeetingTemplate_PrintsWholeRowAtMeetingDate_ForEachEffectiveDateChoice()
    {
        var db = await _fixture.CreateDatabaseAsync("export_row_at_meeting_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, timeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpRowAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpRowOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPROW");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPROW_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var running = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => !x.IsFinal).OrderBy(x => x.Id).FirstAsync();
        var done = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => x.IsFinal).OrderBy(x => x.Id).FirstAsync();

        var meeting887 = await CreateDatedMeetingAsync(dbContext, projectId, "OPEN", 887, new DateTime(2026, 10, 1));
        var meeting888 = await CreateDatedMeetingAsync(dbContext, projectId, "OPEN", 888, new DateTime(2026, 10, 8));
        var meeting889 = await CreateDatedMeetingAsync(dbContext, projectId, "DRAFT", 889, new DateTime(2026, 10, 22));

        var closedFor888 = await CreateTaskWithStatusChangeAsync(
            dbContext, projectId, ownerId, subsystemId, "ExpRowK888", running.Id, done.Id, new DateTime(2026, 10, 8), meeting888);
        var closedToday = await CreateTaskWithStatusChangeAsync(
            dbContext, projectId, ownerId, subsystemId, "ExpRowDnes", running.Id, done.Id, new DateTime(2026, 10, 13), null);
        var closedFor889 = await CreateTaskWithStatusChangeAsync(
            dbContext, projectId, ownerId, subsystemId, "ExpRowK889", running.Id, done.Id, new DateTime(2026, 10, 22), meeting889);

        var print887 = await useCase.BuildMeetingTemplateAsync(meeting887, currentUser, autoPrint: false);
        var print888 = await useCase.BuildMeetingTemplateAsync(meeting888, currentUser, autoPrint: false);
        var print889 = await useCase.BuildMeetingTemplateAsync(meeting889, currentUser, autoPrint: false);

        AssertRow(print887, closedFor888, running.Nazev, completed: false);
        AssertRow(print888, closedFor888, done.Nazev, completed: true);
        print889.Zaznamy.Should().NotContain(x => x.ZaznamId == closedFor888, "ukončený už k minulému jednání 888");

        AssertRow(print887, closedToday, running.Nazev, completed: false);
        AssertRow(print888, closedToday, running.Nazev, completed: false);
        AssertRow(print889, closedToday, done.Nazev, completed: true);

        AssertRow(print887, closedFor889, running.Nazev, completed: false);
        AssertRow(print888, closedFor889, running.Nazev, completed: false);
        AssertRow(print889, closedFor889, done.Nazev, completed: true);
    }

    /// <summary>
    /// Spec 2026-10-13 §4.8 — pozastavení se v tisku jednání bere ke dni jednání, stejně jako
    /// sloupec Stav. Úkol pozastavený až po jednání 888 není v jeho tisku krémový.
    /// </summary>
    [Fact]
    public async Task BuildMeetingTemplate_TaskPausedAfterTheMeeting_IsNotPausedInThatMeetingsPrint()
    {
        var db = await _fixture.CreateDatabaseAsync("export_paused_at_meeting_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, timeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpPauseAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpPauseOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPPAUSE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPPAUSE_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var running = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => !x.IsFinal).OrderBy(x => x.Id).FirstAsync();
        // Seed číselníku pozastavení nemá; v produkci se stav udržuje přímo v databázi.
        var paused = new CiselnikStavuUkoluEntity { Kod = "PAUSE_T", Nazev = "Pozastaveno", IsFinal = false };
        dbContext.CiselnikStavuUkolu.Add(paused);
        await dbContext.SaveChangesAsync();

        var meeting888 = await CreateDatedMeetingAsync(dbContext, projectId, "OPEN", 888, new DateTime(2026, 10, 8));
        var meeting889 = await CreateDatedMeetingAsync(dbContext, projectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var recordId = await CreateTaskWithStatusChangeAsync(
            dbContext, projectId, ownerId, subsystemId, "ExpPause", running.Id, paused.Id, new DateTime(2026, 10, 13), null);

        var print888 = await useCase.BuildMeetingTemplateAsync(meeting888, currentUser, autoPrint: false);
        var print889 = await useCase.BuildMeetingTemplateAsync(meeting889, currentUser, autoPrint: false);

        var rowAt888 = print888.Zaznamy.Should().ContainSingle(x => x.ZaznamId == recordId).Which;
        rowAt888.Stav.Should().Be(running.Nazev);
        rowAt888.IsPaused.Should().BeFalse();

        var rowAt889 = print889.Zaznamy.Should().ContainSingle(x => x.ZaznamId == recordId).Which;
        rowAt889.Stav.Should().Be("Pozastaveno");
        rowAt889.IsPaused.Should().BeTrue();
    }

    private static void AssertRow(PdfExportTemplateViewModel print, int recordId, string expectedStav, bool completed)
    {
        var row = print.Zaznamy.Should().ContainSingle(x => x.ZaznamId == recordId).Which;
        row.Stav.Should().Be(expectedStav);
        row.IsCompleted.Should().Be(completed);
        row.IsPaused.Should().BeFalse();
    }

    private static async Task<int> CreateDatedMeetingAsync(
        PmTrackerDbContext dbContext, int projectId, string stateCode, int number, DateTime date)
    {
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, stateCode, meetingNumber: number);
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = date;
        await dbContext.SaveChangesAsync();
        return meetingId;
    }

    /// <summary>Úkol založený 1.9.; změna stavu uložená 13.10. a platná k <paramref name="platiOd"/>.</summary>
    private static async Task<int> CreateTaskWithStatusChangeAsync(
        PmTrackerDbContext dbContext, int projectId, int ownerId, int subsystemId, string marker,
        int fromStateId, int toStateId, DateTime platiOd, int? jednaniId)
    {
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", marker);
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = new DateTime(2026, 9, 1);
        record.StavUkoluId = toStateId;
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = fromStateId,
            NovyStav = toStateId,
            DatumZmeny = new DateTime(2026, 10, 13, 7, 0, 0),
            PlatiOd = platiOd,
            JednaniId = jednaniId
        });
        await dbContext.SaveChangesAsync();
        return recordId;
    }
```

- [ ] **Krok 3: Spustit testy a ověřit, že padají**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExportRecordVisibilityEvaluatorTests"
```

Očekávat: chyba překladu, `ResolveStatusForMeetingPrint` neexistuje.

- [ ] **Krok 4: Změnit rozhraní a implementaci evaluátoru**

V `IExportRecordVisibilityEvaluator` nahradit deklaraci `IsCompletedForMeetingPrint` (včetně jejího `<summary>`):

```csharp
    /// <summary>
    /// Stav úkolu ke dni jednání (spec 2026-10-13 §4.7). Null = záznam stav úkolu nemá.
    /// Z něj se v tisku jednání odvodí sloupec Stav, pozastavení i ukončení (§4.8).
    /// </summary>
    int? ResolveStatusForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate);
```

V `ExportRecordVisibilityEvaluator` nahradit celou metodu `IsCompletedForMeetingPrint` za:

```csharp
    public int? ResolveStatusForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate)
        => ResolveTaskStatusAtDate(record.StavUkoluId, statusHistory, anchorMeetingDate);
```

- [ ] **Krok 5: Řádek tisku z jednoho stavu**

V `BuildExportRecordsAsync` nahradit komentář a deklaraci `completedRecordIds`:

```csharp
        // Stav ke dni jednání (spec 2026-10-13 §4.8, U6). Mimo tisk jednání zůstává null
        // a řádek se vykreslí z dnešního stavu záznamu.
        Dictionary<int, int?>? statusAtAnchorByRecord = null;
```

V bloku `if (applyMeetingSnapshotRules && anchorMeeting is not null)` nahradit přiřazení `completedRecordIds = records…ToHashSet();` za:

```csharp
            statusAtAnchorByRecord = records.ToDictionary(
                record => record.Id,
                record => exportRecordVisibilityEvaluator.ResolveStatusForMeetingPrint(
                    record,
                    statusHistoryByRecord.TryGetValue(record.Id, out var anchorHistory)
                        ? anchorHistory
                        : Array.Empty<ZaznamHistorieStavuZaznamuEntity>(),
                    anchorMeetingDate));
```

V projekci řádku (`records.Select(record => { … })`) za řádek `var hasProjectOrder = subsystemOrderById.TryGetValue(…);` vložit:

```csharp
            var printedStatusId = statusAtAnchorByRecord is not null
                ? statusAtAnchorByRecord[record.Id]
                : record.StavUkoluId;
            var printedStatus = printedStatusId.HasValue ? taskStates.GetValueOrDefault(printedStatusId.Value) : null;
```

a v inicializátoru `PdfExportRecordViewModel` nahradit tři vlastnosti `Stav`, `IsPaused`, `IsCompleted`:

```csharp
                Stav = printedStatusId.HasValue ? printedStatus?.Nazev ?? "-" : "-",
                IsPaused = TaskStatusRules.IsPausedName(printedStatus?.Nazev),
                IsCompleted = TaskStatusRules.IsCompleted(printedStatus),
```

- [ ] **Krok 6: Spustit testy a ověřit, že prochází**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build --filter "FullyQualifiedName~ExportRecordVisibilityEvaluatorTests|FullyQualifiedName~TaskStatusRulesTests|FullyQualifiedName~CompletedRecordHighlightCssTests|FullyQualifiedName~OpenXmlWordExportServiceTests"
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~ExportTemplateUseCaseTests|FullyQualifiedName~ProjectMembershipDataStoreTests"
dotnet test PmTracker.Tests.Api --no-build --filter "FullyQualifiedName~Export"
```

Očekávat: vše zelené, včetně obou nových integračních testů a `TaskStatusRulesTests.Export_UsesSharedPauseRule_NotItsOwnLiteral`.

- [ ] **Krok 7: Dokumentace**

V `docs/wiki/export/tisk-jednani.md` za sekci `### Vyjádření a komentáře` (před `### Příští jednání`) vložit:

```markdown
### Stav úkolu k datu jednání

Zápis ukazuje každý úkol tak, jak byl **ke dni jednání**, ne jak je dnes:

- sloupec **Stav** je stav platný k datu jednání,
- **modře podbarvený** je úkol, který byl k datu jednání ukončený,
- **krémově podbarvený** je úkol, který byl k datu jednání pozastavený,
- ukončený úkol se vytiskne jen na zápise, kde se poprvé hlásí jako hotový; v dalších zápisech už není.

K jakému datu změna stavu platí, volí uživatel při změně stavu v editoru záznamu
(viz [Upravit záznam](../projekty/zaznamy/upravit-zaznam.md#změna-stavu-úkolu)).
Tisk projektu a tisk záznamu ukazují dnešní stav.
```

V `docs/superpowers/specs/2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md`:
- v tabulce rozhodnutí na konec řádku U6 (před závěrečné ` |`, kotva `se v této změně **neřeší**.`) doplnit ` *(2026-10-13: vyřešeno, viz `2026-10-13-zmena-stavu-k-jednani-design.md`.)*`,
- hned pod nadpis `## 9. Mimo rozsah` vložit odstavec `> **2026-10-13:** obě omezení z této sekce řeší `2026-10-13-zmena-stavu-k-jednani-design.md`. Tisk jednání bere sloupec Stav i pozastavení ke dni jednání.`

- [ ] **Krok 8: Commit**

```bash
git add PmTracker.Web/Services/Export/ExportProjectionBuilders.cs PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs \
  PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs docs/wiki/export/tisk-jednani.md \
  docs/superpowers/specs/2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): tisk jednání bere sloupec Stav i pozastavení ke dni jednání (U6)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 4: Pravidla nabídky „Změna stavu platí k"

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Records/StatusChangeEffectiveDatePolicy.cs`
- Test: `PmTracker.Tests.Unit/Records/StatusChangeEffectiveDatePolicyTests.cs`

**Rozhraní:**
- Poskytuje dál (vše v `PmTracker.Web.Services.Records`):
  - `record StatusChangeMeetingCandidate(int Id, int CisloJednani, DateTime Datum, string? StavKod, bool IsClosed)`
  - `record StatusChangeEffectiveDateOption(string Value, int? JednaniId, DateTime Datum, string Label)`, kde `Value` je `""` pro dnešek, jinak id jednání
  - `record StatusChangeEffectiveDateChoice(IReadOnlyList<StatusChangeEffectiveDateOption> Options, string DefaultValue, DateTime LowerBound)`
  - `record StatusChangeEffectiveDateResolution(StatusChangeEffectiveDateOption? Option, string? Error)`
  - `StatusChangeEffectiveDatePolicy.TodayValue` (`""`)
  - `StatusChangeEffectiveDatePolicy.Build(IReadOnlyList<StatusChangeMeetingCandidate> meetings, DateTime today, DateTime? lastEffectiveDate, DateTime recordFoundedOn) → StatusChangeEffectiveDateChoice`
  - `StatusChangeEffectiveDatePolicy.Resolve(StatusChangeEffectiveDateChoice choice, IReadOnlyList<StatusChangeMeetingCandidate> meetings, string? requestedValue) → StatusChangeEffectiveDateResolution`

- [ ] **Krok 1: Napsat padající testy**

`PmTracker.Tests.Unit/Records/StatusChangeEffectiveDatePolicyTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Nabídka „Změna stavu platí k“ — spec 2026-10-13 §4.2–§4.4, hlášky §7.</summary>
public sealed class StatusChangeEffectiveDatePolicyTests
{
    private static readonly DateTime Today = new(2026, 10, 13);
    private static readonly DateTime FoundedLongAgo = new(2026, 1, 5);

    private static readonly StatusChangeMeetingCandidate M889Draft = new(889, 889, new DateTime(2026, 10, 22), "DRAFT", false);
    private static readonly StatusChangeMeetingCandidate M888Open = new(888, 888, new DateTime(2026, 10, 8), "OPEN", false);
    private static readonly StatusChangeMeetingCandidate M887Open = new(887, 887, new DateTime(2026, 10, 1), "OPEN", false);
    private static readonly StatusChangeMeetingCandidate M886Closed = new(886, 886, new DateTime(2026, 9, 24), "CLOSED", true);
    private static readonly StatusChangeMeetingCandidate M885Closed = new(885, 885, new DateTime(2026, 9, 17), "CLOSED", true);
    private static readonly StatusChangeMeetingCandidate M884Open = new(884, 884, new DateTime(2026, 9, 10), "OPEN", false);

    /// <summary>Příklad ze zadání (spec §4.2) — vstup záměrně neseřazený.</summary>
    private static readonly IReadOnlyList<StatusChangeMeetingCandidate> SpecExample =
        [M884Open, M887Open, M889Draft, M885Closed, M888Open, M886Closed];

    [Fact]
    public void Build_SpecExample_OffersTodayAnd889To887_StopsAtFirstClosed_DefaultsTo888()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        choice.Options.Select(x => x.Label).Should().Equal(
            "Dnes (13.10.2026)",
            "Jednání č. 889 – 22.10.2026 (příprava)",
            "Jednání č. 888 – 08.10.2026",
            "Jednání č. 887 – 01.10.2026");
        choice.Options.Select(x => x.Value).Should().Equal("", "889", "888", "887");
        choice.Options.Select(x => x.JednaniId).Should().Equal(null, 889, 888, 887);
        choice.DefaultValue.Should().Be("888", "výchozí je nejnovější jednání Otevřeno pro zápis (spec §4.3)");
    }

    [Fact]
    public void Build_LockedOpenMeeting_StopsTheOfferLikeClosedOne()
    {
        var locked888 = M888Open with { IsClosed = true };

        var choice = StatusChangeEffectiveDatePolicy.Build([M889Draft, locked888, M887Open], Today, null, FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("", "889");
    }

    [Fact]
    public void Build_UnknownMeetingState_IsSkipped_ButDoesNotStopTheOffer()
    {
        var unknown888 = M888Open with { StavKod = "JINY" };

        var choice = StatusChangeEffectiveDatePolicy.Build([M889Draft, unknown888, M887Open], Today, null, FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("", "889", "887");
    }

    [Fact]
    public void Build_LowerBoundFromLastChange_RemovesEarlierOptions_KeepsSameDate()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 8), FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("", "889", "888");
        choice.LowerBound.Should().Be(new DateTime(2026, 10, 8));
    }

    [Fact]
    public void Build_FoundingDate_IsALowerBoundToo()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, new DateTime(2026, 10, 5));

        choice.Options.Select(x => x.Value).Should().Equal("", "889", "888");
    }

    [Fact]
    public void Build_DefaultFallsBackToToday_WhenNoOpenMeetingIsOffered()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build([M889Draft, M886Closed], Today, null, FoundedLongAgo);

        choice.DefaultValue.Should().Be(StatusChangeEffectiveDatePolicy.TodayValue);
    }

    [Fact]
    public void Build_DefaultFallsBackToFirstOption_WhenTodayIsBelowLowerBound()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        choice.Options.Select(x => x.Value).Should().Equal("889");
        choice.DefaultValue.Should().Be("889");
    }

    [Fact]
    public void Build_NoOptions_WhenEverythingIsBelowLowerBound()
    {
        var closed889 = M889Draft with { StavKod = "CLOSED", IsClosed = true };

        var choice = StatusChangeEffectiveDatePolicy.Build([closed889, M888Open], Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        choice.Options.Should().BeEmpty();
        choice.DefaultValue.Should().Be(StatusChangeEffectiveDatePolicy.TodayValue);
    }

    [Fact]
    public void Resolve_ReturnsChosenMeeting()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        var resolution = StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, "887");

        resolution.Error.Should().BeNull();
        resolution.Option!.JednaniId.Should().Be(887);
        resolution.Option.Datum.Should().Be(new DateTime(2026, 10, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_MissingValue_MeansToday(string? requested)
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        var resolution = StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, requested);

        resolution.Option!.JednaniId.Should().BeNull();
        resolution.Option.Datum.Should().Be(Today);
    }

    [Theory]
    [InlineData("886")]
    [InlineData("884")]
    public void Resolve_ClosedMeetingOrMeetingBehindClosedOne_IsRejected(string requested)
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        var resolution = StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, requested);

        resolution.Option.Should().BeNull();
        resolution.Error.Should().Be(
            $"Jednání č. {requested} teď nejde zvolit — je uzavřené nebo leží za uzavřeným jednáním. Vyberte znovu, k čemu změna stavu platí.");
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("abc")]
    public void Resolve_UnknownMeeting_IsRejected(string requested)
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, null, FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, requested).Error.Should().Be(
            "Vybrané jednání neexistuje nebo bylo smazáno. Vyberte znovu, k čemu změna stavu platí.");
    }

    [Fact]
    public void Resolve_OptionBelowLowerBound_IsRejected()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 8), FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, "887").Error.Should().Be(
            "Změna stavu nemůže platit dříve než 08.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    [Fact]
    public void Resolve_TodayBelowLowerBound_IsRejected()
    {
        var choice = StatusChangeEffectiveDatePolicy.Build(SpecExample, Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, SpecExample, "").Error.Should().Be(
            "Změna stavu nemůže platit dříve než 22.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    [Fact]
    public void Resolve_NoOptions_ExplainsWhyStatusCannotChange()
    {
        var closed889 = M889Draft with { StavKod = "CLOSED", IsClosed = true };
        var meetings = new[] { closed889, M888Open };
        var choice = StatusChangeEffectiveDatePolicy.Build(meetings, Today, new DateTime(2026, 10, 22), FoundedLongAgo);

        StatusChangeEffectiveDatePolicy.Resolve(choice, meetings, "").Error.Should().Be(
            "Stav teď nejde změnit: změna nemůže platit dříve než 22.10.2026 (předchozí změna stavu nebo založení záznamu) a žádné nabízené datum není pozdější.");
    }
}
```

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StatusChangeEffectiveDatePolicyTests"
```

Očekávat: chyba překladu, typy neexistují.

- [ ] **Krok 3: Implementovat politiku**

`PmTracker.Web/Services/Records/StatusChangeEffectiveDatePolicy.cs`:

```csharp
using System.Globalization;

namespace PmTracker.Web.Services.Records;

/// <summary>Jednání projektu, jak ho potřebuje nabídka „Změna stavu platí k“.</summary>
public sealed record StatusChangeMeetingCandidate(int Id, int CisloJednani, DateTime Datum, string? StavKod, bool IsClosed);

/// <summary>Volba nabídky. <see cref="Value"/> je hodnota pole StavPlatiK: prázdná = dnes, jinak id jednání.</summary>
public sealed record StatusChangeEffectiveDateOption(string Value, int? JednaniId, DateTime Datum, string Label);

public sealed record StatusChangeEffectiveDateChoice(
    IReadOnlyList<StatusChangeEffectiveDateOption> Options,
    string DefaultValue,
    DateTime LowerBound);

public sealed record StatusChangeEffectiveDateResolution(StatusChangeEffectiveDateOption? Option, string? Error);

/// <summary>
/// K jakému datu platí změna stavu úkolu — spec 2026-10-13 §4.2 (nabídka), §4.3 (výchozí
/// volba), §4.4 (spodní mez) a §7 (hlášky). Čistá funkce; data načítá StatusChangeEffectiveDateQuery.
/// </summary>
public static class StatusChangeEffectiveDatePolicy
{
    public const string TodayValue = "";

    private const string DraftCode = "DRAFT";
    private const string OpenCode = "OPEN";
    private const string RetryHint = "Vyberte znovu, k čemu změna stavu platí.";

    public static StatusChangeEffectiveDateChoice Build(
        IReadOnlyList<StatusChangeMeetingCandidate> meetings,
        DateTime today,
        DateTime? lastEffectiveDate,
        DateTime recordFoundedOn)
    {
        var lowerBound = recordFoundedOn.Date;
        if (lastEffectiveDate.HasValue && lastEffectiveDate.Value.Date > lowerBound)
        {
            lowerBound = lastEffectiveDate.Value.Date;
        }

        var offered = new List<(StatusChangeEffectiveDateOption Option, bool IsOpenMeeting)>
        {
            (new StatusChangeEffectiveDateOption(
                TodayValue,
                null,
                today.Date,
                string.Create(CultureInfo.InvariantCulture, $"Dnes ({today:dd.MM.yyyy})")), false)
        };

        foreach (var meeting in meetings.OrderByDescending(x => x.CisloJednani))
        {
            if (meeting.IsClosed)
            {
                break;
            }

            var isDraft = string.Equals(meeting.StavKod, DraftCode, StringComparison.OrdinalIgnoreCase);
            var isOpen = string.Equals(meeting.StavKod, OpenCode, StringComparison.OrdinalIgnoreCase);
            if (!isDraft && !isOpen)
            {
                continue;
            }

            var label = string.Create(
                CultureInfo.InvariantCulture,
                $"Jednání č. {meeting.CisloJednani} – {meeting.Datum:dd.MM.yyyy}{(isDraft ? " (příprava)" : string.Empty)}");
            offered.Add((new StatusChangeEffectiveDateOption(
                meeting.Id.ToString(CultureInfo.InvariantCulture),
                meeting.Id,
                meeting.Datum.Date,
                label), isOpen));
        }

        var allowed = offered.Where(x => x.Option.Datum >= lowerBound).ToList();
        var defaultValue = allowed.Where(x => x.IsOpenMeeting).Select(x => x.Option.Value).FirstOrDefault()
            ?? allowed.Where(x => x.Option.Value == TodayValue).Select(x => x.Option.Value).FirstOrDefault()
            ?? allowed.Select(x => x.Option.Value).FirstOrDefault()
            ?? TodayValue;

        return new StatusChangeEffectiveDateChoice(allowed.Select(x => x.Option).ToList(), defaultValue, lowerBound);
    }

    public static StatusChangeEffectiveDateResolution Resolve(
        StatusChangeEffectiveDateChoice choice,
        IReadOnlyList<StatusChangeMeetingCandidate> meetings,
        string? requestedValue)
    {
        var value = string.IsNullOrWhiteSpace(requestedValue) ? TodayValue : requestedValue.Trim();
        var option = choice.Options.FirstOrDefault(x => x.Value == value);
        if (option is not null)
        {
            return new StatusChangeEffectiveDateResolution(option, null);
        }

        var lowerBound = choice.LowerBound.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        if (choice.Options.Count == 0)
        {
            return Rejected(
                $"Stav teď nejde změnit: změna nemůže platit dříve než {lowerBound} (předchozí změna stavu nebo založení záznamu) a žádné nabízené datum není pozdější.");
        }

        var belowLowerBound = $"Změna stavu nemůže platit dříve než {lowerBound} (předchozí změna stavu nebo založení záznamu).";
        if (value == TodayValue)
        {
            return Rejected(belowLowerBound);
        }

        var meeting = int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var meetingId)
            ? meetings.FirstOrDefault(x => x.Id == meetingId)
            : null;
        if (meeting is null)
        {
            return Rejected($"Vybrané jednání neexistuje nebo bylo smazáno. {RetryHint}");
        }

        return meeting.Datum.Date < choice.LowerBound
            ? Rejected(belowLowerBound)
            : Rejected($"Jednání č. {meeting.CisloJednani} teď nejde zvolit — je uzavřené nebo leží za uzavřeným jednáním. {RetryHint}");
    }

    private static StatusChangeEffectiveDateResolution Rejected(string error) => new(null, error);
}
```

Pozor na `Resolve_ClosedMeetingOrMeetingBehindClosedOne_IsRejected` pro 886: jednání 886 (24.9.) leží nad spodní mezí (5.1.), takže hláška je „teď nejde zvolit". Kdyby jeho datum bylo pod mezí, vyhrála by hláška o spodní mezi. To je v pořádku, i ta je pravdivá.

- [ ] **Krok 4: Spustit testy a ověřit, že prochází**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StatusChangeEffectiveDatePolicyTests"
```

Očekávat: vše zelené (19 testů včetně řádků Theory).

- [ ] **Krok 5: Commit**

```bash
git add PmTracker.Web/Services/Records/StatusChangeEffectiveDatePolicy.cs PmTracker.Tests.Unit/Records/StatusChangeEffectiveDatePolicyTests.cs
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): pravidla nabídky, výchozí volby a spodní meze data platnosti změny stavu

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 5: Uložení změny stavu s volbou data

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Records/StatusChangeEffectiveDateQuery.cs`
- Změnit: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` (`SaveRecordCommand.StavPlatiK`)
- Změnit: `PmTracker.Web/Services/RecordService.SaveRecord.cs` (validační kontext, `ValidateRecordSaveCommandAsync`, zápis historie)
- Test: `PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs` (další testy, úprava pomocníka)

**Rozhraní:**
- Používá z úkolu 4: `StatusChangeEffectiveDatePolicy.Build/Resolve` a záznamy z něj.
- Používá z úkolu 1: `PlatiOd`, `JednaniId`.
- Poskytuje dál:
  - `record StatusChangeEffectiveDateContext(IReadOnlyList<StatusChangeMeetingCandidate> Meetings, StatusChangeEffectiveDateChoice Choice)`
  - `StatusChangeEffectiveDateQuery.LoadAsync(PmTrackerDbContext dbContext, int projektId, int zaznamId, DateTime recordFoundedOn, DateTime today, CancellationToken ct) → Task<StatusChangeEffectiveDateContext>`
  - `SaveRecordCommand.StavPlatiK` (`string?`)

- [ ] **Krok 1: Rozšířit pomocníka testu a napsat padající testy**

V `StatusChangeEffectiveDateDataStoreTests` nahradit `BuildEditCommandAsync` verzí s parametrem `stavPlatiK`:

```csharp
    private static async Task<SaveRecordCommand> BuildEditCommandAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        string statusCode,
        string? stavPlatiK = null,
        DateTime? datumZalozeni = null)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).SingleAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).SingleAsync();

        return new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = datumZalozeni ?? record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu,
            StavPlatiK = stavPlatiK
        };
    }
```

Přidat do třídy pole, pomocníky a testy (doplnit `using PmTracker.Web.Models.Entities;`, `using PmTracker.Web.Services.Data;`, `using PmTracker.Web.Services.Records;`):

```csharp
    // 13.10.2026 09:00 v Praze.
    private static readonly DateTimeOffset MorningInPrague = new(2026, 10, 13, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveRecord_StatusChangeBoundToMeeting_StoresMeetingDateAndId()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_meeting");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFMEET");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));

        store.SaveRecord(
            await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode, stavPlatiK: meeting888.ToString()),
            arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.ZaznamId == arranged.RecordId);
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 8));
        change.JednaniId.Should().Be(meeting888);
        change.DatumZmeny.Should().Be(new DateTime(2026, 10, 13, 7, 0, 0), "okamžik uložení zůstává pro audit");
    }

    [Fact]
    public async Task SaveRecord_ClosedMeeting_IsRejected_AndNothingIsSaved()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_closed");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFCLOSED");
        var closed886 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "CLOSED", 886, new DateTime(2026, 9, 24));
        var statusBefore = await CurrentStatusAsync(dbContext, arranged.RecordId);

        var command = await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode, stavPlatiK: closed886.ToString());
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().StartWith("Jednání č. 886 teď nejde zvolit");
        (await CurrentStatusAsync(dbContext, arranged.RecordId)).Should().Be(statusBefore);
        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().AnyAsync(x => x.ZaznamId == arranged.RecordId)).Should().BeFalse();
    }

    /// <summary>Review focus 3: id jednání z jiného projektu se nesmí přijmout.</summary>
    [Fact]
    public async Task SaveRecord_MeetingFromAnotherProject_IsRejected()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_other_project");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFOTHER");
        var otherProjectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SEFOTHER2");
        var foreignMeeting = await CreateDatedMeetingAsync(dbContext, otherProjectId, "OPEN", 500, new DateTime(2026, 10, 8));

        var command = await BuildEditCommandAsync(dbContext, arranged.RecordId, arranged.DoneStatusCode, stavPlatiK: foreignMeeting.ToString());
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().Be("Vybrané jednání neexistuje nebo bylo smazáno. Vyberte znovu, k čemu změna stavu platí.");
    }

    [Fact]
    public async Task SaveRecord_EffectiveDateBeforePreviousChange_IsRejected()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_lower_bound");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFBOUND");
        var meeting887 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 887, new DateTime(2026, 10, 1));
        await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));
        // Předchozí změna stavu platí k 8.10.: úkol je teď hotový, uživatel ho znovu otevírá.
        await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 8), jednaniId: null);
        var runningCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();

        var command = await BuildEditCommandAsync(dbContext, arranged.RecordId, runningCode, stavPlatiK: meeting887.ToString());
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().Be("Změna stavu nemůže platit dříve než 08.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    /// <summary>Review focus 5: posunuté datum založení v témže uložení funguje jako spodní mez.</summary>
    [Fact]
    public async Task SaveRecord_FoundingDateMovedAfterChosenMeeting_IsRejected()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_founding");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFFOUND");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));

        var command = await BuildEditCommandAsync(
            dbContext, arranged.RecordId, arranged.DoneStatusCode,
            stavPlatiK: meeting888.ToString(), datumZalozeni: new DateTime(2026, 10, 10));
        var act = () => store.SaveRecord(command, arranged.User);

        act.Should().Throw<RecordValidationException>()
            .Which.FieldErrors["StavPlatiK"].Should().ContainSingle()
            .Which.Should().Be("Změna stavu nemůže platit dříve než 10.10.2026 (předchozí změna stavu nebo založení záznamu).");
    }

    /// <summary>Review focus 1: nezměněný stav — pole StavPlatiK se ignoruje, i když je nesmyslné.</summary>
    [Fact]
    public async Task SaveRecord_UnchangedStatus_IgnoresStavPlatiK_AndWritesNoHistory()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_unchanged");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFSAME");
        var currentStatusId = await CurrentStatusAsync(dbContext, arranged.RecordId);
        var currentCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == currentStatusId)
            .Select(x => x.Kod)
            .SingleAsync();

        store.SaveRecord(await BuildEditCommandAsync(dbContext, arranged.RecordId, currentCode, stavPlatiK: "999999"), arranged.User);

        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().AnyAsync(x => x.ZaznamId == arranged.RecordId)).Should().BeFalse();
    }

    [Fact]
    public async Task Query_LockedMeeting_StopsTheOfferLikeClosedOne()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_locked");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var arranged = await ArrangeTaskAsync(dbContext, "SEFLOCK");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var locked888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));
        await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 887, new DateTime(2026, 10, 1));
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == locked888);
        meeting.UzamklOsobaId = arranged.User.OsobaId;
        await dbContext.SaveChangesAsync();

        var context = await StatusChangeEffectiveDateQuery.LoadAsync(
            dbContext, arranged.ProjectId, arranged.RecordId, RecordFoundedOn, new DateTime(2026, 10, 13), CancellationToken.None);

        context.Choice.Options.Select(x => x.JednaniId).Should().Equal(null, meeting889);
    }

    private static async Task<int> CreateDatedMeetingAsync(
        PmTrackerDbContext dbContext, int projectId, string stateCode, int number, DateTime date)
    {
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, stateCode, meetingNumber: number);
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = date;
        await dbContext.SaveChangesAsync();
        return meetingId;
    }

    /// <summary>Přímý zápis změny stavu do historie (a do záznamu) — stav, jaký by zanechalo dřívější uložení.</summary>
    private static async Task<int> AddStatusChangeAsync(
        PmTrackerDbContext dbContext, int recordId, bool toFinal, DateTime platiOd, int? jednaniId)
    {
        var running = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var done = await dbContext.CiselnikStavuUkolu.AsNoTracking().Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.StavUkoluId = toFinal ? done : running;
        var change = new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = toFinal ? running : done,
            NovyStav = toFinal ? done : running,
            DatumZmeny = new DateTime(2026, 10, 13, 7, 0, 0),
            PlatiOd = platiOd,
            JednaniId = jednaniId
        };
        dbContext.ZaznamHistorieStavuZaznamu.Add(change);
        await dbContext.SaveChangesAsync();
        return change.Id;
    }

    private static Task<int?> CurrentStatusAsync(PmTrackerDbContext dbContext, int recordId)
        => dbContext.ProjektoveZaznamy.AsNoTracking().Where(x => x.Id == recordId).Select(x => x.StavUkoluId).SingleAsync();
```

Test z úkolu 1 volá pomocníka bez `stavPlatiK` a tak to zůstane. Jde o review focus 2 (formulář bez pole) a test musí zůstat zelený i po přidání validace.

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

```bash
dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~StatusChangeEffectiveDateDataStoreTests"
```

Očekávat: chyba překladu, `SaveRecordCommand.StavPlatiK` a `StatusChangeEffectiveDateQuery` neexistují.

- [ ] **Krok 3: Pole příkazu**

V `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` do `SaveRecordCommand` za `Stav` přidat:

```csharp
    /// <summary>
    /// Spec 2026-10-13 §4.5 — k čemu platí změna stavu: prázdné = dnes, jinak id jednání.
    /// Server ho použije jen při skutečné změně stavu.
    /// </summary>
    public string? StavPlatiK { get; set; }
```

- [ ] **Krok 4: Dotaz nad daty projektu**

`PmTracker.Web/Services/Records/StatusChangeEffectiveDateQuery.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

public sealed record StatusChangeEffectiveDateContext(
    IReadOnlyList<StatusChangeMeetingCandidate> Meetings,
    StatusChangeEffectiveDateChoice Choice);

/// <summary>
/// Nabídka „Změna stavu platí k“ nad daty projektu a záznamu (spec 2026-10-13 §4.2–§4.4).
/// Stejný výpočet používá editor pro zobrazení i uložení pro ověření, aby se nemohly rozejít.
/// </summary>
public static class StatusChangeEffectiveDateQuery
{
    public static async Task<StatusChangeEffectiveDateContext> LoadAsync(
        PmTrackerDbContext dbContext,
        int projektId,
        int zaznamId,
        DateTime recordFoundedOn,
        DateTime today,
        CancellationToken ct)
    {
        var meetings = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .ToListAsync(ct);
        var meetingStates = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);
        var candidates = meetings
            .Select(meeting =>
            {
                var state = meetingStates.GetValueOrDefault(meeting.StavJednaniId);
                return new StatusChangeMeetingCandidate(
                    meeting.Id,
                    meeting.CisloJednani,
                    meeting.DatumPlanovane.Date,
                    state?.Kod,
                    MeetingStatePolicy.IsReadOnly(meeting, state));
            })
            .ToList();
        var lastEffectiveDate = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .Where(x => x.ZaznamId == zaznamId)
            .MaxAsync(x => (DateTime?)x.PlatiOd, ct);

        return new StatusChangeEffectiveDateContext(
            candidates,
            StatusChangeEffectiveDatePolicy.Build(candidates, today, lastEffectiveDate, recordFoundedOn));
    }
}
```

- [ ] **Krok 5: Ověření ve validaci a zápis do historie**

V `RecordService.SaveRecord.cs`:

1. Do `SaveRecordValidationContext` přidat poslední parametr:

```csharp
        IReadOnlyList<int> NormalizedCollaborationIds,
        StatusChangeEffectiveDateOption? StatusChangeEffectiveDate);
```

2. Ve `ValidateRecordSaveCommandAsync` vložit hned **za** blok `if (project?.PouzivatIdentJednani == true && !command.Id.HasValue) { … }` (výběr jednání pro identifikátor) a **před** `var normalizedCollaborationIds = …`:

```csharp
        // Spec 2026-10-13 §4.5 — k čemu platí změna stavu. Jen když vznikne řádek historie
        // (stejná podmínka jako zápis v transakci); jinak se pole StavPlatiK ignoruje.
        StatusChangeEffectiveDateOption? statusChangeEffectiveDate = null;
        if (existingRecord is not null
            && existingRecord.ProjektId == command.ProjektId
            && existingRecord.StavUkoluId.HasValue
            && statusId.HasValue
            && existingRecord.StavUkoluId.Value != statusId.Value)
        {
            var effectiveDateContext = await StatusChangeEffectiveDateQuery.LoadAsync(
                dbContext, command.ProjektId, existingRecord.Id, command.DatumZalozeni.Date, GetLocalNow().Date, ct);
            var resolution = StatusChangeEffectiveDatePolicy.Resolve(
                effectiveDateContext.Choice, effectiveDateContext.Meetings, command.StavPlatiK);
            if (resolution.Option is null)
            {
                AddRecordValidationIssue(
                    issues,
                    "StavPlatiK",
                    resolution.Error!,
                    "basic",
                    "status_effective_date_invalid",
                    command.StavPlatiK);
            }
            else
            {
                statusChangeEffectiveDate = resolution.Option;
            }
        }
```

3. Do `return new SaveRecordValidationContext(…)` na konec doplnit argument:

```csharp
            normalizedCollaborationIds,
            statusChangeEffectiveDate);
```

4. V transakci (blok `if (oldStatus != entity.StavUkoluId && …)`) nahradit inicializátor řádku historie:

```csharp
                    dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
                    {
                        ZaznamId = entity.Id,
                        PuvodniStav = oldStatus.Value,
                        NovyStav = entity.StavUkoluId.Value,
                        DatumZmeny = timeProvider.GetUtcNow().UtcDateTime,
                        // Validace volbu naplní vždy, když se stav mění; prázdná zůstane jen při souběhu,
                        // kdy stav mezi validací a transakcí změnil někdo jiný — pak platí dnes.
                        PlatiOd = validation.StatusChangeEffectiveDate?.Datum ?? GetLocalNow().Date,
                        JednaniId = validation.StatusChangeEffectiveDate?.JednaniId
                    });
```

(`using PmTracker.Web.Services.Records;` už v souboru je.)

- [ ] **Krok 6: Spustit testy a ověřit, že prochází**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~StatusChangeEffectiveDateDataStoreTests|FullyQualifiedName~RecordSaveDataStoreTests|FullyQualifiedName~RecordConcurrencyDataStoreTests"
dotnet test PmTracker.Tests.Unit --no-build
```

Očekávat: `StatusChangeEffectiveDateDataStoreTests` 8/8 (1 z úkolu 1 + 7 nových), stávající uložení záznamů beze změny, Unit celé zelené (včetně `RecordServiceWriteSplitTests`).

- [ ] **Krok 7: Commit**

```bash
git add PmTracker.Web/Services/Records/StatusChangeEffectiveDateQuery.cs PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs \
  PmTracker.Web/Services/RecordService.SaveRecord.cs PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): uložení změny stavu ověří a zapíše zvolené datum platnosti nebo jednání

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 6: Volba v editoru záznamu

**Soubory:**
- Změnit: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs`
- Změnit: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` (`BuildZaznamEditForEntityAsync`)
- Změnit: `PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml`
- Změnit: `PmTracker.Web/wwwroot/css/site.css`
- Vytvořit: `PmTracker.Web/wwwroot/js/modules/recordEditor/statusEffectiveDate.js`
- Změnit: `PmTracker.Web/wwwroot/js/modules/recordEditor/index.js`
- Změnit: `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js` (popisek pole pro souhrn chyb)
- Test: `tests/js/dom/statusEffectiveDate.test.js`
- Test: `PmTracker.Tests.Api/Controllers/RecordEditorStatusEffectiveDateTests.cs`
- Test: `PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs` (2 testy)
- Test: `PmTracker.Tests.Unit/Records/StatusEffectiveDateEditorAssetsTests.cs`

**Rozhraní:**
- Používá z úkolu 5: `StatusChangeEffectiveDateQuery.LoadAsync`, `SaveRecordCommand.StavPlatiK`.
- Poskytuje dál: `ZaznamEditViewModel.StavPlatiK` (`StatusChangeEffectiveDateChoice?`, `null` = volba se nezobrazuje); JS export `syncStatusEffectiveDateRow(row, selectedStav)`, `initStatusEffectiveDate(scope)`.

- [ ] **Krok 1: Napsat padající testy**

`tests/js/dom/statusEffectiveDate.test.js`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";

import { syncStatusEffectiveDateRow } from "../../../PmTracker.Web/wwwroot/js/modules/recordEditor/statusEffectiveDate.js";

// Duck-typed řádek: jen hidden a dataset, víc funkce nečte.
function fakeRow(originalStav) {
    return { hidden: true, dataset: { originalStav } };
}

test("změněný stav volbu „Změna stavu platí k“ ukáže", () => {
    const row = fakeRow("Rozpracováno");
    syncStatusEffectiveDateRow(row, "Hotovo");
    assert.equal(row.hidden, false);
});

test("návrat na uložený stav volbu znovu skryje", () => {
    const row = fakeRow("Rozpracováno");
    syncStatusEffectiveDateRow(row, "Hotovo");
    syncStatusEffectiveDateRow(row, "Rozpracováno");
    assert.equal(row.hidden, true);
});

test("záznam bez uloženého stavu: jakýkoli vybraný stav je změna", () => {
    const row = fakeRow("");
    syncStatusEffectiveDateRow(row, "Rozpracováno");
    assert.equal(row.hidden, false);
});
```

`PmTracker.Tests.Unit/Records/StatusEffectiveDateEditorAssetsTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-13 §6 — napojení volby „Změna stavu platí k“ v editoru.</summary>
public sealed class StatusEffectiveDateEditorAssetsTests
{
    [Fact]
    public void Css_HiddenFormRow_IsReallyHidden()
        => File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"))
            .Should().Contain(".record-form-row[hidden]",
                ".record-form-row má display:grid, který atribut hidden jinak přebije");

    [Fact]
    public void EditorBootstrap_InitializesStatusEffectiveDate()
    {
        var index = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/js/modules/recordEditor/index.js"));

        index.Should().Contain("from \"./statusEffectiveDate.js\"");
        index.Should().Contain("initStatusEffectiveDate(scope);",
            "editor se vkládá i dodatečně (lazy load, refresh karty) a vždy přes initRecordFormEnhancements");
    }

    [Fact]
    public void BasicPanel_RendersChoiceOnlyWhenMetadataIsEditable()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml"));

        view.Should().Contain("Model.StavPlatiK is not null && !metadataLocked");
        view.Should().Contain("name=\"StavPlatiK\"");
        view.Should().Contain("data-original-stav=\"@Model.Stav\"");
    }
}
```

`PmTracker.Tests.Api/Controllers/RecordEditorStatusEffectiveDateTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-13 §6 — editor vykreslí volbu „Změna stavu platí k“ skrytou, s nabídkou
/// a předvybraným nejnovějším otevřeným jednáním. Kotví se na atributy: Razor kóduje diakritiku.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordEditorStatusEffectiveDateTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordEditorStatusEffectiveDateTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Edit_RendersHiddenChoice_WithNewestOpenMeetingPreselected()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiStavPlatiK");
        var projectId = await _fixture.EnsureProjectAsync("APISTAVPK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISTAVPKSUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API stav plati k");
        // Všechna jednání fixture dává na dnešek = datum založení záznamu, spodní mez nic nevyřadí.
        var olderOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9711);
        var newestOpenId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9712);
        var draftId = await _fixture.CreateMeetingAsync(projectId, "DRAFT", 9713);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Matches(html, "<div[^>]*data-status-effective-date-row[^>]*\\shidden").Should().BeTrue("volba je skrytá, dokud se stav nezmění");
        Matches(html, "<select[^>]*name=\"StavPlatiK\"").Should().BeTrue();
        Matches(html, "<option value=\"\"").Should().BeTrue("volba „dnes“ má prázdnou hodnotu");
        Matches(html, $"<option value=\"{newestOpenId}\" selected=\"selected\"").Should().BeTrue();
        Matches(html, $"<option value=\"{olderOpenId}\">").Should().BeTrue();
        Matches(html, $"<option value=\"{draftId}\">").Should().BeTrue();
    }

    private static bool Matches(string html, string pattern)
        => Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
```

Do `StatusChangeEffectiveDateDataStoreTests` přidat:

```csharp
    [Fact]
    public async Task BuildZaznamEdit_OffersChoice_ForExistingRecord()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_editor");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFEDIT");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));

        var editor = store.BuildZaznamEdit(arranged.RecordId);

        editor.StavPlatiK.Should().NotBeNull();
        editor.StavPlatiK!.Options.Select(x => x.JednaniId).Should().Equal(null, meeting888);
        editor.StavPlatiK.DefaultValue.Should().Be(meeting888.ToString());
        editor.StavPlatiK.Options[0].Label.Should().Be("Dnes (13.10.2026)");
    }

    [Fact]
    public async Task BuildZaznamCreate_HasNoChoice()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_create");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFCREATE");

        store.BuildZaznamCreate(arranged.ProjectId).StavPlatiK.Should().BeNull("založení nezapisuje řádek historie stavů (spec §4.1)");
    }
```

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

```bash
npm test
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~StatusEffectiveDateEditorAssetsTests"
```

Očekávat: JS test selže (modul neexistuje), Unit testy selžou (CSS pravidlo, import ani pole v šabloně nejsou).

- [ ] **Krok 3: View model a skládání editoru**

V `ZaznamEditViewModels.cs` doplnit `using PmTracker.Web.Services.Records;` a do `ZaznamEditViewModel` za `StavyUkolu`:

```csharp
    /// <summary>Spec 2026-10-13 §6 — volba „Změna stavu platí k“; null = nezobrazuje se (zakládání).</summary>
    public StatusChangeEffectiveDateChoice? StavPlatiK { get; set; }
```

V `ProjectService.RecordEditorComposition.cs` v `BuildZaznamEditForEntityAsync` hned za `var todayDate = timeProvider.GetLocalNow().Date;`:

```csharp
        // Spec 2026-10-13 §6 — při zakládání se volba nezobrazuje (založení nezapisuje historii stavů).
        var statusEffectiveDate = isCreate || record.Id <= 0
            ? null
            : (await StatusChangeEffectiveDateQuery.LoadAsync(
                dbContext, record.ProjektId, record.Id, record.DatumZalozeni, todayDate, ct)).Choice;
```

a do inicializátoru `ZaznamEditViewModel` za `StavyUkolu = …`:

```csharp
            StavPlatiK = statusEffectiveDate,
```

- [ ] **Krok 4: Šablona**

V `_EditZaznamBasicPanel.cshtml` vložit hned za uzavírací `</div>` horního řádku (`record-form-row-top`), tedy před `@if (Model.IsCreate && Model.PouzivatIdentJednani)`:

```razor
    @* Spec 2026-10-13 §6 — viditelné jen po změně stavu (recordEditor/statusEffectiveDate.js). *@
    @if (Model.StavPlatiK is not null && !metadataLocked)
    {
        <div class="record-form-row" data-status-effective-date-row data-original-stav="@Model.Stav" hidden>
            @if (Model.StavPlatiK.Options.Count > 0)
            {
                <label for="stav-plati-k">
                    Změna stavu platí k
                    <select id="stav-plati-k" name="StavPlatiK">
                        @foreach (var option in Model.StavPlatiK.Options)
                        {
                            <option value="@option.Value" selected="@(option.Value == Model.StavPlatiK.DefaultValue)">@option.Label</option>
                        }
                    </select>
                </label>
            }
            else
            {
                <gov-message color="warning">
                    Stav teď nejde změnit: změna nemůže platit dříve než @Model.StavPlatiK.LowerBound.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) (předchozí změna stavu nebo založení záznamu) a žádné nabízené datum není pozdější.
                </gov-message>
            }
        </div>
    }
```

Komentář stojí **nad** `@if`, ne uvnitř tagu: komentář mezi atributy TagHelperu tiše zahodí následující atribut.

- [ ] **Krok 5: CSS**

V `PmTracker.Web/wwwroot/css/site.css` hned za pravidlo `.record-form-row { display: grid; gap: 12px; }`:

```css
/* Spec 2026-10-13 §6 — display:grid by atribut hidden přebil. */
.record-form-row[hidden] {
    display: none;
}
```

- [ ] **Krok 6: JS modul a jeho zapojení**

`PmTracker.Web/wwwroot/js/modules/recordEditor/statusEffectiveDate.js`:

```js
/**
 * recordEditor/statusEffectiveDate.js
 *
 * Spec 2026-10-13 §6 — volba „Změna stavu platí k“ je vidět jen tehdy, když se vybraný
 * stav úkolu liší od uloženého. Volá se z initRecordFormEnhancements při každém vložení
 * editoru do stránky; ready-příznak na formuláři brání dvojímu navázání.
 */

export function syncStatusEffectiveDateRow(row, selectedStav) {
    row.hidden = selectedStav === (row.dataset.originalStav ?? "");
}

export function initStatusEffectiveDate(scope) {
    scope.querySelectorAll('form[data-record-editor-form="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.statusEffectiveDateReady === "true") {
            return;
        }

        const row = form.querySelector("[data-status-effective-date-row]");
        const statusSelect = form.querySelector('select[name="Stav"]');
        if (!(row instanceof HTMLElement) || !(statusSelect instanceof HTMLSelectElement)) {
            return;
        }

        form.dataset.statusEffectiveDateReady = "true";
        const sync = () => syncStatusEffectiveDateRow(row, statusSelect.value);
        statusSelect.addEventListener("change", sync);
        sync();
    });
}
```

V `recordEditor/index.js`:
- k ostatním importům přidat `import { initStatusEffectiveDate } from "./statusEffectiveDate.js";`,
- do re-export bloku přidat `export * from "./statusEffectiveDate.js";`,
- v `initRecordFormEnhancements` hned za `initRecordMeetingNumberPreview(scope);` přidat `initStatusEffectiveDate(scope);`. Musí být **před** `initRecordEditorDirtyTracking(scope);`: obnova konceptu posílá `change` a posluchač už musí existovat.

V `recordEditor/form.js` v `resolveRecordEditorFieldLabel` do mapy `direct` přidat `"stavplatik": "Změna stavu platí k",`. (`resolveRecordEditorTabForFieldKey` klíč přiřadí záložce „Základní údaje" přes prefix `stav`, nic dalšího netřeba.)

- [ ] **Krok 7: Spustit testy a ověřit, že prochází**

```bash
npm test
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build --filter "FullyQualifiedName~StatusEffectiveDateEditorAssetsTests"
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~StatusChangeEffectiveDateDataStoreTests"
dotnet test PmTracker.Tests.Api --no-build --filter "FullyQualifiedName~RecordEditorStatusEffectiveDateTests|FullyQualifiedName~RecordEditorControllerTests"
```

Očekávat: JS všechny zelené (včetně 3 nových), Unit 3/3, Integration 10/10, Api zelené.

- [ ] **Krok 8: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs \
  PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml PmTracker.Web/wwwroot/css/site.css \
  PmTracker.Web/wwwroot/js/modules/recordEditor/statusEffectiveDate.js PmTracker.Web/wwwroot/js/modules/recordEditor/index.js \
  PmTracker.Web/wwwroot/js/modules/recordEditor/form.js tests/js/dom/statusEffectiveDate.test.js \
  PmTracker.Tests.Unit/Records/StatusEffectiveDateEditorAssetsTests.cs PmTracker.Tests.Api/Controllers/RecordEditorStatusEffectiveDateTests.cs \
  PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): editor záznamu nabídne, ke kterému datu nebo jednání změna stavu platí

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 7: Přeplánování a smazání jednání

**Soubory:**
- Změnit: `PmTracker.Web/Services/MeetingService.WriteCommands.cs` (`SaveMeetingAsync`, `DeleteMeetingAsync`, nové privátní metody)
- Test: `PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs` (4 testy)

**Rozhraní:**
- Používá z úkolu 1: `PlatiOd`, `JednaniId`; z úkolu 5 pomocníky testu `CreateDatedMeetingAsync`, `AddStatusChangeAsync`.
- Používá existující `MeetingService.ResolveVisibleRecordNumber(ProjektovyZaznamEntity)` (private static v `MeetingService.DetailQueries.cs`, stejná partial třída).

- [ ] **Krok 1: Napsat padající testy**

Do `StatusChangeEffectiveDateDataStoreTests` přidat:

```csharp
    [Fact]
    public async Task SaveMeeting_Reschedule_MovesBoundStatusChanges()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_reschedule");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFMOVE");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var changeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 22), jednaniId: meeting889);

        store.SaveMeeting(MeetingCommand(meeting889, arranged.ProjectId, 889, new DateTime(2026, 10, 20), "DRAFT"), arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == changeId);
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 20), "změna navázaná na jednání jde s jeho datem (spec §4.6)");
        change.JednaniId.Should().Be(meeting889);
    }

    [Fact]
    public async Task SaveMeeting_RescheduleBreakingOrderOfChanges_IsRejected_AndNothingChanges()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_reschedule_order");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFORDER");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        // Ukončeno k jednání 889, pak 25.10. znovu otevřeno „dnes“.
        var closeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 22), jednaniId: meeting889);
        await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: false, platiOd: new DateTime(2026, 10, 25), jednaniId: null);
        var visibleNumber = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == arranged.RecordId)
            .Select(x => x.CisloViditelne ?? x.CisloZaznamu.ToString())
            .SingleAsync();

        var act = () => store.SaveMeeting(
            MeetingCommand(meeting889, arranged.ProjectId, 889, new DateTime(2026, 10, 29), "DRAFT"), arranged.User);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Jednání nelze přesunout na 29.10.2026: změna stavu záznamů č. {visibleNumber.Trim()} navázaná na toto jednání*");
        (await dbContext.Jednani.AsNoTracking().SingleAsync(x => x.Id == meeting889)).DatumPlanovane.Should().Be(new DateTime(2026, 10, 22));
        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == closeId)).PlatiOd.Should().Be(new DateTime(2026, 10, 22));
    }

    /// <summary>
    /// Review focus 4: úprava jednání bez změny data na historii nesahá. Navázaná změna má
    /// záměrně jiné datum než jednání — kdyby přepis proběhl, test to pozná.
    /// </summary>
    [Fact]
    public async Task SaveMeeting_WithoutDateChange_DoesNotTouchStatusHistory()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_meeting_same_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFPLACE");
        var meeting889 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "DRAFT", 889, new DateTime(2026, 10, 22));
        var changeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 21), jednaniId: meeting889);

        var command = MeetingCommand(meeting889, arranged.ProjectId, 889, new DateTime(2026, 10, 22), "DRAFT");
        command.Misto = "Jiná místnost";
        store.SaveMeeting(command, arranged.User);

        (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == changeId))
            .PlatiOd.Should().Be(new DateTime(2026, 10, 21));
    }

    [Fact]
    public async Task DeleteMeeting_KeepsPlatiOd_AndClearsBinding()
    {
        var db = await _fixture.CreateDatabaseAsync("status_eff_meeting_delete");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new PragueTimeProvider(MorningInPrague));
        var arranged = await ArrangeTaskAsync(dbContext, "SEFDELETE");
        var meeting888 = await CreateDatedMeetingAsync(dbContext, arranged.ProjectId, "OPEN", 888, new DateTime(2026, 10, 8));
        var changeId = await AddStatusChangeAsync(dbContext, arranged.RecordId, toFinal: true, platiOd: new DateTime(2026, 10, 8), jednaniId: meeting888);

        store.DeleteMeeting(new DeleteMeetingCommand { JednaniId = meeting888, ProjektId = arranged.ProjectId }, arranged.User);

        var change = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking().SingleAsync(x => x.Id == changeId);
        change.JednaniId.Should().BeNull();
        change.PlatiOd.Should().Be(new DateTime(2026, 10, 8), "změna dál platí k datu, které jednání mělo");
        (await dbContext.Jednani.AsNoTracking().AnyAsync(x => x.Id == meeting888)).Should().BeFalse();
    }

    private static SaveMeetingCommand MeetingCommand(int id, int projectId, int number, DateTime date, string stateCode) => new()
    {
        Id = id,
        ProjektId = projectId,
        CisloJednani = number,
        DatumPlanovane = date,
        CasZacatek = new TimeOnly(9, 0),
        Misto = "Test",
        StavJednani = stateCode
    };
```

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

```bash
dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~StatusChangeEffectiveDateDataStoreTests&FullyQualifiedName~Meeting"
```

Očekávat: `SaveMeeting_Reschedule_MovesBoundStatusChanges` a `…BreakingOrder…` selžou (datum se nepřepíše, výjimka nevznikne). `DeleteMeeting_…` selže na porušení cizího klíče. `SaveMeeting_WithoutDateChange_…` projde už teď a po změně musí zůstat zelený.

- [ ] **Krok 3: Přeplánování**

V `SaveMeetingAsync` ve větvi `if (command.Id.HasValue)` vložit hned **za** kontrolu `if (IsMeetingReadOnly(existing, existingStatus)) { throw … }` a **před** `var old = MeetingAuditSnapshot.FromEntity(existing);`:

```csharp
            if (existing.DatumPlanovane.Date != command.DatumPlanovane.Date)
            {
                await MoveBoundStatusChangesAsync(existing.Id, command.DatumPlanovane.Date, ct);
            }
```

Na konec třídy přidat:

```csharp
    /// <summary>
    /// Spec 2026-10-13 §4.6 — změny stavu navázané na jednání jdou s jeho datem. Změny každého
    /// záznamu musí podle pořadí uložení (id) mít neklesající plati_od, jinak by přepočet stavu
    /// k datu jednání dával nesmysly; takové přeplánování se odmítne a nic se neuloží.
    /// </summary>
    private async Task MoveBoundStatusChangesAsync(int meetingId, DateTime newDate, CancellationToken ct)
    {
        var bound = await dbContext.ZaznamHistorieStavuZaznamu
            .Where(x => x.JednaniId == meetingId)
            .ToListAsync(ct);
        if (bound.Count == 0)
        {
            return;
        }

        var recordIds = bound.Select(x => x.ZaznamId).Distinct().ToList();
        var history = await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);
        var brokenRecordIds = history
            .GroupBy(x => x.ZaznamId)
            .Where(group => !IsNonDecreasing(group
                .OrderBy(x => x.Id)
                .Select(x => x.JednaniId == meetingId ? newDate : x.PlatiOd.Date)))
            .Select(group => group.Key)
            .ToList();

        if (brokenRecordIds.Count > 0)
        {
            var numbers = (await dbContext.ProjektoveZaznamy.AsNoTracking()
                    .Where(x => brokenRecordIds.Contains(x.Id))
                    .ToListAsync(ct))
                .Select(ResolveVisibleRecordNumber)
                .OrderBy(x => x, StringComparer.CurrentCulture)
                .ToList();
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Jednání nelze přesunout na {newDate:dd.MM.yyyy}: změna stavu záznamů č. {string.Join(", ", numbers)} navázaná na toto jednání by platila mimo pořadí ostatních změn stavu těchto záznamů. Ponechte původní datum, nebo zvolte datum mezi sousedními změnami."));
        }

        foreach (var change in bound)
        {
            change.PlatiOd = newDate;
        }
    }

    private static bool IsNonDecreasing(IEnumerable<DateTime> dates)
    {
        DateTime? previous = null;
        foreach (var date in dates)
        {
            if (previous.HasValue && date < previous.Value)
            {
                return false;
            }

            previous = date;
        }

        return true;
    }
```

Změny řádků historie jsou sledované a uloží je stávající `SaveChangesAsync` spolu s jednáním, tedy v jednom zápisu.

- [ ] **Krok 4: Smazání jednání**

V `DeleteMeetingAsync` za načtení `commentRows` přidat:

```csharp
        // Spec 2026-10-13 §4.6 — změna stavu dál platí k datu, které jednání mělo; ruší se jen vazba.
        var boundStatusChanges = await dbContext.ZaznamHistorieStavuZaznamu
            .Where(x => x.JednaniId == command.JednaniId)
            .ToListAsync(ct);
        foreach (var change in boundStatusChanges)
        {
            change.JednaniId = null;
        }
```

a podmínku prvního uložení rozšířit, aby vazby zrušila **před** smazáním jednání (cizí klíč je `NO ACTION`):

```csharp
        if (attendanceRows.Count > 0 || commentRows.Count > 0 || boundStatusChanges.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
```

- [ ] **Krok 5: Spustit testy a ověřit, že prochází**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~StatusChangeEffectiveDateDataStoreTests|FullyQualifiedName~Meeting"
dotnet test PmTracker.Tests.Unit --no-build --filter "FullyQualifiedName~Meeting"
```

Očekávat: 14/14 v `StatusChangeEffectiveDateDataStoreTests`, ostatní testy jednání beze změny.

- [ ] **Krok 6: Commit**

```bash
git add PmTracker.Web/Services/MeetingService.WriteCommands.cs PmTracker.Tests.Integration/DataStore/StatusChangeEffectiveDateDataStoreTests.cs
git commit -m "$(cat <<'EOF'
feat(stav-k-jednani): přeplánování jednání posune navázané změny stavu, smazání zruší jen vazbu

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Úkol 8: Dokumentace, regrese, ověření v prohlížeči a merge

**Soubory:**
- Změnit: `docs/wiki/projekty/zaznamy/upravit-zaznam.md`
- Změnit nebo vytvořit: `docs/changelog/releases/0.11.md` nebo `0.12.md` (viz krok 2; `CHANGELOG.md` generuje `scripts/generate-changelog.sh`)

- [ ] **Krok 1: Uživatelská dokumentace**

V `docs/wiki/projekty/zaznamy/upravit-zaznam.md` za sekci `### Tab 1: Základní` (před `### Tab 2: Harmonogram`) vložit:

```markdown
#### Změna stavu úkolu

Když v editoru změníš **Stav úkolu**, objeví se pod ním pole **Změna stavu platí k**.
Určuje, ke kterému datu změna platí. Podle toho ji ukáže tisk jednání.

- Nabízí se **dnešní datum** a jednání projektu od nejnovějšího, která jsou
  v **přípravě** nebo **otevřená pro zápis**. První uzavřené jednání nabídku ukončí,
  starší jednání už se nenabídnou, ani když jsou otevřená.
- Předvybrané je nejnovější jednání **otevřené pro zápis**.
- Změna nemůže platit dříve než předchozí změna stavu ani před založením záznamu.
- Když jednání přeplánuješ, změny stavu navázané na něj se posunou s ním.

Příklad: zápis z jednání 8.10. zpracováváš až 13.10. a úkol ukončuješ, protože se
to na jednání řeklo. Zvol „Jednání č. 888 – 08.10.2026" a tisk jednání 888 ukáže
úkol jako ukončený.
```

- [ ] **Krok 2: Changelog**

Poslední podklad je `docs/changelog/releases/0.11.md` z 2026-10-14 (převod historické skutečnosti). **Zeptat se uživatele**, jestli už 0.11 nasadil. Pokud ne, doplnit body do `0.11.md` do stávajících sekcí. Pokud ano, založit `0.12.md` se stejnou strukturou (`## 0.12 - <datum>`, `### Přidáno`, `### Opraveno`, `### Poznámka k nasazení`). Obsah:
- **Přidáno:** volba „Změna stavu platí k" v editoru,
- **Opraveno:** tisk jednání ukazuje sloupec Stav i podbarvení ke dni jednání,
- **Poznámka k nasazení:** spustit `db_upgrade_1_4_10_zmena_stavu_plati_od.sql` **před** startem nové verze, SQL Server 2016+, stav ověří `db_check_applied_upgrades.sql`.

Pak spustit `scripts/generate-changelog.sh` a zkontrolovat diff `CHANGELOG.md`. Pokud skript selže nebo se struktura liší od očekávání, `CHANGELOG.md` ručně neupravovat a ohlásit to.

- [ ] **Krok 3: Plná regrese**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build
dotnet test PmTracker.Tests.Api --no-build
dotnet test PmTracker.Tests.Integration --no-build
npm test
```

Očekávat: Unit, Api a JS celé zelené. Integration zelená kromě známého `ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`, které je třeba vyjmenovat. Cokoli dalšího je regrese a před pokračováním se opraví.

E2E (~11 min, sama spouští aplikaci):

```bash
dotnet test PmTracker.Tests.E2E --no-build 2>&1 | tee "$TMPDIR/e2e-branch.txt"
```

Seznam jmen selhání porovnat s baseline z `main` v zahazovacím worktree podle paměti `project_e2e_baseline_failures`: jména normalizovat Pythonem a porovnat přes `comm`. Nové jméno = regrese.

- [ ] **Krok 4: Upgrade lokální dev databáze**

Lokální DB `PmTracker` v kontejneru `pmtracker-sql` upgraduji sám, se zálohou:

```bash
SQL="/opt/homebrew/bin/sqlcmd -S localhost,1433 -U sa -P PmTracker!2026 -Ndisable"
$SQL -d master -Q "BACKUP DATABASE [PmTracker] TO DISK = N'/var/opt/mssql/data/PmTracker-pred-1_4_10.bak' WITH INIT, COPY_ONLY"
$SQL -d PmTracker -i db_check_applied_upgrades.sql
```

Spustit chybějící skripty ve vzestupném pořadí (`$SQL -d PmTracker -b -i <skript>`), včetně `db_upgrade_1_4_10_zmena_stavu_plati_od.sql`. Pak znovu kontrolní skript: `1_4_10` musí mít stav `APLIKOVÁN`. Ověřit převod:

```bash
$SQL -d PmTracker -Q "SELECT TOP 5 id, datum_zmeny, plati_od, jednani_id FROM dbo.zaznam_historie_stavu_zaznamu ORDER BY id DESC"
```

- [ ] **Krok 5: Ověření v prohlížeči**

Spustit aplikaci z worktree:

```bash
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__PmTrackerDb='Server=localhost,1433;Database=PmTracker;User Id=sa;Password=PmTracker!2026;TrustServerCertificate=True;Encrypt=True' \
PmTracker__Data__Provider=SqlServer \
dotnet run --project PmTracker.Web --urls http://localhost:5071 --no-launch-profile
```

Připravit data v lokální DB: úkol v projektu, který má aspoň jedno jednání otevřené pro zápis a jedno v přípravě. Když takový projekt není, založit si přes UI testovací jednání a po ověření je smazat. Mazat **jen vlastní** testovací data, lokální DB může souběžně plnit jiná session. Zapsat si `RECORD_ID`, `PROJECT_ID`, `MEETING_ID` (otevřené jednání), uložený stav úkolu a název koncového stavu.

Skript `$SCRATCHPAD/stav-k-jednani.cjs` (cesty upravit na skutečné id):

```js
const path = require("path");
const fs = require("fs");
const { chromium } = require(path.resolve("PmTracker.Tests.E2E/bin/Debug/net8.0/.playwright/package"));

const BASE = "http://localhost:5071";
const RECORD_ID = 0;            // doplnit
const PROJECT_ID = 0;           // doplnit
const MEETING_ID = 0;           // doplnit — otevřené jednání, ke kterému se bude ukončovat
const DONE_LABEL = "Hotovo";    // doplnit — název koncového stavu
const OUT = process.argv[2];

(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage({ viewport: { width: 1400, height: 1000 } });
    const errors = [];
    page.on("pageerror", (e) => errors.push(`pageerror: ${e.message}`));
    page.on("console", (m) => { if (m.type() === "error") errors.push(`console: ${m.text()}`); });

    await page.goto(`${BASE}/Zaznamy/Edit?id=${RECORD_ID}&asUser=1`);
    await page.waitForSelector('select[name="Stav"]');
    const row = page.locator("[data-status-effective-date-row]");
    const original = await page.locator('select[name="Stav"]').inputValue();
    console.log("1) skryto po otevření:", await row.isHidden());
    await page.screenshot({ path: `${OUT}/01-otevreno.png` });

    await page.selectOption('select[name="Stav"]', { label: DONE_LABEL });
    console.log("2) vidět po změně stavu:", await row.isVisible());
    console.log("   volby:", await page.locator('select[name="StavPlatiK"] option').allTextContents());
    console.log("   předvybráno:", await page.locator('select[name="StavPlatiK"]').inputValue());
    await page.screenshot({ path: `${OUT}/02-volba.png` });

    await page.selectOption('select[name="Stav"]', original);
    console.log("3) skryto po návratu stavu:", await row.isHidden());

    await page.selectOption('select[name="Stav"]', { label: DONE_LABEL });
    await page.selectOption('select[name="StavPlatiK"]', String(MEETING_ID));
    await page.locator("gov-button", { hasText: "Uložit" }).first().click({ force: true });
    await page.waitForLoadState("networkidle");
    await page.screenshot({ path: `${OUT}/03-ulozeno.png` });

    const pdf = await page.request.get(`${BASE}/Export/Jednani/${MEETING_ID}/Tisk?projektId=${PROJECT_ID}&autoPrint=false&asUser=1`);
    fs.writeFileSync(`${OUT}/04-tisk-jednani.pdf`, await pdf.body());
    console.log("4) tisk jednání:", pdf.status(), pdf.headers()["content-type"]);

    console.log("chyby v konzoli:", errors);
    await browser.close();
})();
```

```bash
mkdir -p "$SCRATCHPAD/stav-k-jednani"
PmTracker.Tests.E2E/bin/Debug/net8.0/.playwright/node/darwin-arm64/node "$SCRATCHPAD/stav-k-jednani.cjs" "$SCRATCHPAD/stav-k-jednani"
```

Ověřit:
- výstup 1–3: `true`, `true` s nabídkou podle pravidel a předvybraným nejnovějším otevřeným jednáním, `true`,
- žádné chyby v konzoli,
- snímky 01–03 prohlédnout nástrojem Read. Pole sedí pod horním řádkem formuláře, nepřekrývá sousední pole a po uložení se editor normálně zavře nebo obnoví,
- PDF `04-tisk-jednani.pdf` přečíst nástrojem Read. Úkol má ve sloupci Stav koncový stav a je modře podbarvený.
- Na konci zkontrolovat v DB, že řádek historie má `jednani_id = MEETING_ID` a `plati_od` = datum jednání. Pak vlastní testovací data vrátit nebo smazat.

Najde-li průchod vadu, opravit ji (s testem, který před opravou padá), znovu pustit dotčené testy a průchod zopakovat.

- [ ] **Krok 6: Commit dokumentace**

```bash
git add docs/wiki/projekty/zaznamy/upravit-zaznam.md docs/changelog/releases CHANGELOG.md
git status --short
git commit -m "$(cat <<'EOF'
docs(stav-k-jednani): nápověda k volbě data platnosti změny stavu a changelog

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Krok 7: Merge do `main`**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git status --short
```

Pokud hlavní adresář obsahuje cizí necommitnuté změny v souborech, kterých se větev týká, **zastavit se a zeptat uživatele**. Jinak:

```bash
git merge --no-ff feat/zmena-stavu-k-jednani -m "Merge feat/zmena-stavu-k-jednani into main"
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit --no-build
dotnet test PmTracker.Tests.Api --no-build
dotnet test PmTracker.Tests.Integration --no-build --filter "FullyQualifiedName~StatusChangeEffectiveDate|FullyQualifiedName~ExportTemplateUseCase|FullyQualifiedName~ZakladniDatasetLoader|FullyQualifiedName~ProjectMembershipDataStore"
npm test
git worktree remove .claude/worktrees/zmena-stavu-k-jednani
git branch -d feat/zmena-stavu-k-jednani
```

Konflikty při merge (`main` se mezitím pohnul) řešit sloučením obou stran, nic nezahazovat. Zejména `db_check_applied_upgrades.sql`, bootstrap seznam a `CHANGELOG.md`: pokud mezitím někdo obsadil `1_4_10`, přečíslovat skript (soubor, testy, spec i dokumentaci) a merge zopakovat. Push jen na pokyn.

- [ ] **Krok 8: Závěrečné shrnutí uživateli**

Shrnout: co je hotové, výsledky všech sad (s vyjmenovanými známými selháními), co ukázal průchod v prohlížeči (snímky a PDF poslat přes SendUserFile). K nasazení připomenout: spustit `db_upgrade_1_4_10_zmena_stavu_plati_od.sql` **před** startem nové verze na obou instancích (pm-tracker a pm-tracker-vyvoj), vyžaduje SQL Server 2016+. Nabídnout jednorázový skript pro opravu už uložených změn (spec §10). Uživatelův ruční test na serveru je finální ověření.
