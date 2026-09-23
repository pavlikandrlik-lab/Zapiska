# Předvyplnění účasti u nového jednání — implementační plán

> **Pro agentní workery:** REQUIRED SUB-SKILL: použij superpowers:executing-plans (uživatel zvolil inline exekuci v hlavní session). Kroky mají checkbox (`- [ ]`) syntaxi pro sledování postupu.

**Cíl:** Nově zakládané jednání dostane u každého člena týmu předvyplněný stav účasti podle jeho nejčastější účasti za posledních max 15 uzavřených jednání projektu; stav `ONLINE` se ve všech seedech jmenuje „Videokonference".

**Architektura:** Rozhodovací logika žije v samostatné scoped službě `IAttendancePredictor`, která pro celý tým najednou vrátí slovník `osoba → id stavu účasti`. `MeetingService.CreateMeetingAttendanceSnapshotAsync` ji zavolá při zakládání jednání a použije předpověď tam, kde existuje; jinak zůstává dnešní výchozí stav. Historie se čte dvěma dotazy (jednání + účasti), agregace probíhá v paměti.

**Tech stack:** net8.0, ASP.NET Core MVC, EF Core 8 (SQL Server v provozu, `Microsoft.EntityFrameworkCore.InMemory` v unit testech), xUnit + FluentAssertions, Testcontainers.MsSql pro Integration.

**Spec:** `docs/superpowers/specs/2026-09-05-predvyplneni-ucasti-design.md`

## Globální omezení

- **Commity se DRŽÍ.** Uživatel commituje sám po ruční verifikaci. Každý task končí *checkpointem* (build + testy zelené), ne `git commit`. Navržená commit message je u každého tasku uvedená pro pozdější použití.
- Do odhadu se počítají **jen uzavřená jednání** — stav s kódem `CLOSED` (spec U3).
- Počet zohledněných jednání je konstanta `MaxConsideredMeetings = 15` v kódu, **ne** v konfiguraci (spec §5.3).
- Pořadí „posledních 15" se určuje podle **`CisloJednani` sestupně**, ne podle data (spec §4.1).
- Shoda počtů → vyhrává stav z **nejnovějšího** jednání mezi shodnými (spec §4.2).
- Osoba bez historie **není ve výsledném slovníku**; volající pro ni použije dnešní výchozí stav (spec §4.3).
- Odhad se uplatní **jen na nově zakládané jednání**; existující jednání se nepřepočítávají (spec U5).
- Název stavu `ONLINE` je **`Videokonference`** v produkčním baseline i ve vývojovém seedu (spec U1).
- `appsettings.json` a `appsettings.Development.json` jsou gitignorované; nevytvářet ani nevyplňovat.

## Struktura souborů

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Vytvořit | `PmTracker.Web/Services/Meetings/IAttendancePredictor.cs` | kontrakt odhadu |
| Vytvořit | `PmTracker.Web/Services/Meetings/AttendancePredictor.cs` | dotaz do historie + volba nejčetnějšího stavu |
| Vytvořit | `PmTracker.Tests.Unit/Meetings/AttendancePredictorTests.cs` | rozhodovací pravidla bez databáze |
| Vytvořit | `PmTracker.Tests.Integration/DataStore/MeetingAttendancePredictionTests.cs` | průchod zakládáním jednání až do SQL |
| Změnit | `PmTracker.Web/Services/MeetingService.cs` | konstruktor + pole `attendancePredictor` |
| Změnit | `PmTracker.Web/Services/MeetingService.WriteCommands.cs:345-387` | použití odhadu při zakládání docházky |
| Změnit | `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:125` | registrace služby |
| Změnit | `PmTracker.Tests.Unit/Meetings/MeetingServiceSaveAttendanceBatchTests.cs:38-39` | doplnit nový parametr konstruktoru |
| Změnit | `PmTracker.Tests.Unit/Meetings/MeetingServiceDelegationTests.cs:15` | doplnit nový parametr konstruktoru |
| Změnit | `db_seed_dev_admin.sql:113` | doplnit `ONLINE` / Videokonference |
| Změnit | `PMTracker_insert_sql:772` | přejmenovat `Online` → `Videokonference` |
| Změnit | `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:71` | srovnat pin s novým názvem + nový test na vývojový seed |

**Pořadí tasků:** Task 1 (seed) musí být před Taskem 3 — Integration test v Tasku 3 potřebuje, aby stav `ONLINE` v testovací databázi existoval. Integration databáze se zakládá z `db_seed_dev_admin.sql` (`tests/Common/RepositoryPaths.cs:24`).

---

### Task 1: Videokonference v seedech

**Files:**
- Modify: `db_seed_dev_admin.sql:112-117`
- Modify: `PMTracker_insert_sql:772`
- Test: `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs:71` (změna pinu) + nový `[Fact]` ve stejné třídě

**Interfaces:**
- Consumes: nic z předchozích tasků.
- Produces: stav účasti s kódem `ONLINE` a názvem `Videokonference` v testovací databázi Integration testů — Task 3 na něm staví.

**Kontext:** Překlep „VIdeokonference" v repozitáři není; vznikl přejmenováním přímo v databázi a uživatel si ho už opravil. Skutečná vada je, že vývojový seed řádek `ONLINE` vůbec nezakládá, takže videokonferenční účast nikdy neprojde lokálně ani v testech. Produkční baseline ho zakládá pod názvem „Online".

- [ ] **Krok 1: Uprav existující pin na produkční baseline**

V `PmTracker.Tests.Unit/Common/SeedBaselineDocumentationTests.cs` na řádku 71 změň:

```csharp
        script.Should().Contain("N'ONLINE', N'Videokonference'");
```

(původně `script.Should().Contain("N'ONLINE', N'Online'");`)

- [ ] **Krok 2: Přidej test na vývojový seed**

Do stejné třídy `SeedBaselineDocumentationTests`, hned za metodu `ProductionBaselineSeed_ShouldContainMinimalOperationalDefaults` (končí na řádku 84), vlož:

```csharp
    [Fact]
    public void DevSeed_ShouldContainVideokonferenceAttendanceState()
    {
        var script = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "db_seed_dev_admin.sql"));

        script.Should().Contain("N'ONLINE', N'Videokonference'",
            "bez tohoto řádku se videokonferenční účast nikdy neprojde lokálně ani v testech");
    }
```

Metoda `GetRepositoryRoot()` je privátní statická ve stejné třídě (řádek 117) — netřeba nic přidávat.

- [ ] **Krok 3: Spusť testy a ověř, že selžou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SeedBaselineDocumentationTests"`

Expected: FAIL — 2 selhání.
- `ProductionBaselineSeed_ShouldContainMinimalOperationalDefaults`: „Expected script to contain "N'ONLINE', N'Videokonference'"" (baseline má ještě „Online")
- `DevSeed_ShouldContainVideokonferenceAttendanceState`: „Expected script to contain "N'ONLINE', N'Videokonference'"" (řádek ve vývojovém seedu úplně chybí)

- [ ] **Krok 4: Doplň řádek do vývojového seedu**

V `db_seed_dev_admin.sql` vlož za řádek 113 (za blok `PRESENT`, aby pořadí odpovídalo produkčnímu baseline PRESENT → ONLINE → EXCUSED → ABSENT):

```sql
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'ONLINE')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'ONLINE', N'Videokonference', 1);
```

Výsledný blok (řádky 112-119) pak vypadá takto:

```sql
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'PRESENT')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'PRESENT', N'Přítomen', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'ONLINE')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'ONLINE', N'Videokonference', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'EXCUSED')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'EXCUSED', N'Omluven', 1);
    IF NOT EXISTS (SELECT 1 FROM dbo.ciselnik_stavu_ucasti WHERE kod = N'ABSENT')
        INSERT INTO dbo.ciselnik_stavu_ucasti(kod, nazev, is_locked) VALUES (N'ABSENT', N'Nepřítomen', 1);
```

- [ ] **Krok 5: Přejmenuj stav v produkčním baseline**

V `PMTracker_insert_sql` na řádku 772 změň:

```sql
    (N'ONLINE', N'Videokonference', 1),
```

(původně `    (N'ONLINE', N'Online', 1),`)

Řádek je uvnitř `MERGE dbo.ciselnik_stavu_ucasti` (řádky 769-784), jehož větev `WHEN MATCHED THEN UPDATE SET target.nazev = source.nazev` existující řádek přejmenuje — opětovné spuštění baseline tedy název v databázi skutečně srovná.

- [ ] **Krok 6: Spusť testy a ověř, že projdou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SeedBaselineDocumentationTests"`

Expected: PASS — všechny testy třídy zelené.

- [ ] **Krok 7: Checkpoint (commit se DRŽÍ)**

Navržená message pro pozdější commit uživatelem:

```
fix(seed): stav účasti ONLINE se jmenuje Videokonference a je i ve vývojovém seedu
```

---

### Task 2: Služba AttendancePredictor

**Files:**
- Create: `PmTracker.Web/Services/Meetings/IAttendancePredictor.cs`
- Create: `PmTracker.Web/Services/Meetings/AttendancePredictor.cs`
- Test: `PmTracker.Tests.Unit/Meetings/AttendancePredictorTests.cs`

**Interfaces:**
- Consumes: `PmTrackerDbContext` (namespace `PmTracker.Web.Data`), entity `JednaniEntity` (`Id`, `ProjektId`, `CisloJednani`, `DatumPlanovane`, `CasZacatek`, `StavJednaniId`), `UcastEntity` (`JednaniId`, `OsobaId`, `StavUcastiId`), `CiselnikStavuJednaniEntity` (`Id`, `Kod`, `Nazev`) — vše z `PmTracker.Web.Models.Entities`. DbSety: `dbContext.Jednani`, `dbContext.Ucast`, `dbContext.CiselnikStavuJednani`.
- Produces: `PmTracker.Web.Services.Meetings.IAttendancePredictor` s jedinou metodou
  `Task<IReadOnlyDictionary<int, int>> PredictAsync(int projectId, IReadOnlyCollection<int> osobaIds, CancellationToken ct = default)`
  a implementaci `PmTracker.Web.Services.Meetings.AttendancePredictor` s konstruktorem `AttendancePredictor(PmTrackerDbContext dbContext)`. Task 3 obojí používá.

**Poznámka k testovací infrastruktuře:** Unit projekt má `Microsoft.EntityFrameworkCore.InMemory` 8.0.12; vzor pro `PmTrackerDbContext` nad InMemory je `PmTracker.Tests.Unit/Schedule/HarmonogramSkutecnostSyncServiceTests.cs:28-31`. Entita `UcastEntity` má složený klíč `(JednaniId, OsobaId)` (`PmTracker.Web/Data/Configuration/MeetingEntityConfiguration.cs:29`), takže dvě účasti téže osoby musí být u různých jednání.

#### Cyklus A — základ: nejčetnější stav, méně než 15 jednání, osoba bez historie, cizí projekt

- [ ] **Krok 1: Napiš čtyři padající testy**

Vytvoř `PmTracker.Tests.Unit/Meetings/AttendancePredictorTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Meetings;
using Xunit;

namespace PmTracker.Tests.Unit.Meetings;

/// <summary>
/// Odhad předvyplněné účasti u nového jednání — spec
/// docs/superpowers/specs/2026-09-05-predvyplneni-ucasti-design.md.
/// Testuje pravidla rozhodování bez databáze: nejčetnější stav, strop 15 jednání,
/// jen uzavřená jednání, rozhodnutí shody počtů, osoba bez historie, cizí projekt.
/// </summary>
public sealed class AttendancePredictorTests
{
    private const int ProjektId = 7;
    private const int JinyProjektId = 8;
    private const int MemberOsobaId = 100;
    private const int NewcomerOsobaId = 101;

    private const int PresentStateId = 1;
    private const int OnlineStateId = 2;
    private const int ExcusedStateId = 3;

    private const int ClosedMeetingStateId = 10;
    private const int DraftMeetingStateId = 11;
    private const int OpenMeetingStateId = 12;

    private static PmTrackerDbContext CreateDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("attendance-predictor-" + Guid.NewGuid())
            .Options);

    private static async Task SeedMeetingStatesAsync(PmTrackerDbContext db)
    {
        db.CiselnikStavuJednani.AddRange(
            new CiselnikStavuJednaniEntity { Id = ClosedMeetingStateId, Kod = "CLOSED", Nazev = "Uzavřeno" },
            new CiselnikStavuJednaniEntity { Id = DraftMeetingStateId, Kod = "DRAFT", Nazev = "Příprava" },
            new CiselnikStavuJednaniEntity { Id = OpenMeetingStateId, Kod = "OPEN", Nazev = "Otevřeno pro zápis" });
        await db.SaveChangesAsync();
    }

    /// <summary>Id jednání = jeho číslo; testy tak nemusí držet dvě číselné řady.</summary>
    private static void AddMeeting(
        PmTrackerDbContext db, int meetingNumber, int stateId, int projectId = ProjektId)
    {
        db.Jednani.Add(new JednaniEntity
        {
            Id = meetingNumber,
            ProjektId = projectId,
            CisloJednani = meetingNumber,
            DatumPlanovane = new DateTime(2026, 1, 1).AddDays(meetingNumber),
            CasZacatek = new TimeOnly(9, 0),
            StavJednaniId = stateId
        });
    }

    private static void AddAttendance(PmTrackerDbContext db, int meetingNumber, int osobaId, int stateId)
        => db.Ucast.Add(new UcastEntity
        {
            JednaniId = meetingNumber,
            OsobaId = osobaId,
            StavUcastiId = stateId
        });

    [Fact]
    public async Task PredictAsync_VybereNejcetnejsiStav()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        for (var meetingNumber = 1; meetingNumber <= 10; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId);
            AddAttendance(db, meetingNumber, MemberOsobaId,
                meetingNumber <= 7 ? OnlineStateId : PresentStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(OnlineStateId,
            "7 z 10 uzavřených jednání bylo přes videokonferenci");
    }

    [Fact]
    public async Task PredictAsync_PocitaZTohoCoJe_KdyzJednaniJeMeneNezStrop()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        AddMeeting(db, meetingNumber: 2, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 2, MemberOsobaId, ExcusedStateId);
        AddMeeting(db, meetingNumber: 3, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 3, MemberOsobaId, ExcusedStateId);
        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(ExcusedStateId,
            "tři dostupná jednání se počítají stejně jako plných patnáct (spec U2)");
    }

    [Fact]
    public async Task PredictAsync_OsobaBezHistorieVeVysledkuNeni()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db)
            .PredictAsync(ProjektId, [MemberOsobaId, NewcomerOsobaId]);

        result.Should().ContainKey(MemberOsobaId);
        result.Should().NotContainKey(NewcomerOsobaId,
            "pro osobu bez historie volající použije výchozí stav, odhad ji nevymýšlí");
    }

    [Fact]
    public async Task PredictAsync_IgnorujeJednaniJinehoProjektu()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        for (var meetingNumber = 2; meetingNumber <= 6; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId, JinyProjektId);
            AddAttendance(db, meetingNumber, MemberOsobaId, OnlineStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(PresentStateId,
            "pět videokonferencí v jiném projektu nesmí přebít jediné jednání v tomto");
    }
}
```

- [ ] **Krok 2: Spusť testy a ověř, že selžou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests"`

Expected: FAIL — chyba kompilace `CS0246: The type or namespace name 'AttendancePredictor' could not be found` (a `PmTracker.Web.Services.Meetings` neexistuje).

- [ ] **Krok 3: Napiš kontrakt**

Vytvoř `PmTracker.Web/Services/Meetings/IAttendancePredictor.cs`:

```csharp
namespace PmTracker.Web.Services.Meetings;

/// <summary>
/// Odhad předvyplněného stavu účasti pro nově zakládané jednání — spec
/// docs/superpowers/specs/2026-09-05-predvyplneni-ucasti-design.md.
/// </summary>
public interface IAttendancePredictor
{
    /// <summary>
    /// Pro každou osobu vrátí id nejčetnějšího stavu účasti z posledních uzavřených
    /// jednání projektu. Osoby bez historie ve výsledku nejsou — volající pro ně
    /// použije výchozí stav.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> PredictAsync(
        int projectId,
        IReadOnlyCollection<int> osobaIds,
        CancellationToken ct = default);
}
```

- [ ] **Krok 4: Napiš minimální implementaci**

Vytvoř `PmTracker.Web/Services/Meetings/AttendancePredictor.cs`. Zatím **bez** filtru na uzavřená jednání, **bez** stropu 15 a **bez** rozhodnutí shody — ty přijdou v dalších cyklech, aby jejich testy měly skutečný červený běh:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Meetings;

public sealed class AttendancePredictor(PmTrackerDbContext dbContext) : IAttendancePredictor
{
    public async Task<IReadOnlyDictionary<int, int>> PredictAsync(
        int projectId,
        IReadOnlyCollection<int> osobaIds,
        CancellationToken ct = default)
    {
        var empty = new Dictionary<int, int>();
        if (osobaIds.Count == 0)
        {
            return empty;
        }

        var meetingIds = await dbContext.Jednani.AsNoTracking()
            .Where(meeting => meeting.ProjektId == projectId)
            .Select(meeting => meeting.Id)
            .ToListAsync(ct);
        if (meetingIds.Count == 0)
        {
            return empty;
        }

        var personIds = osobaIds.ToList();
        var attendanceRows = await dbContext.Ucast.AsNoTracking()
            .Where(row => meetingIds.Contains(row.JednaniId) && personIds.Contains(row.OsobaId))
            .Select(row => new { row.JednaniId, row.OsobaId, row.StavUcastiId })
            .ToListAsync(ct);

        return attendanceRows
            .GroupBy(row => row.OsobaId)
            .ToDictionary(
                personRows => personRows.Key,
                personRows => personRows
                    .GroupBy(row => row.StavUcastiId)
                    .OrderByDescending(stateRows => stateRows.Count())
                    .First().Key);
    }
}
```

- [ ] **Krok 5: Spusť testy a ověř, že projdou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests"`

Expected: PASS — 4 testy zelené.

#### Cyklus B — počítají se jen uzavřená jednání

- [ ] **Krok 6: Napiš padající test**

Přidej do `AttendancePredictorTests`:

```csharp
    [Fact]
    public async Task PredictAsync_IgnorujeJednaniVPripraveAOtevrena()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);
        AddMeeting(db, meetingNumber: 1, ClosedMeetingStateId);
        AddAttendance(db, meetingNumber: 1, MemberOsobaId, PresentStateId);
        AddMeeting(db, meetingNumber: 2, DraftMeetingStateId);
        AddAttendance(db, meetingNumber: 2, MemberOsobaId, OnlineStateId);
        AddMeeting(db, meetingNumber: 3, OpenMeetingStateId);
        AddAttendance(db, meetingNumber: 3, MemberOsobaId, OnlineStateId);
        AddMeeting(db, meetingNumber: 4, OpenMeetingStateId);
        AddAttendance(db, meetingNumber: 4, MemberOsobaId, OnlineStateId);
        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(PresentStateId,
            "účast u neuzavřených jednání může být jen předvyplněná odhadem — jinak by si odhad potvrzoval sám sebe (spec U3)");
    }
```

- [ ] **Krok 7: Spusť test a ověř, že selže**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests.PredictAsync_IgnorujeJednaniVPripraveAOtevrena"`

Expected: FAIL — „Expected result[100] to be 1, but found 2" (tři videokonference u neuzavřených jednání přebily jediné uzavřené).

- [ ] **Krok 8: Doplň filtr na uzavřená jednání**

V `AttendancePredictor.cs` přidej konstantu nad metodu `PredictAsync`:

```csharp
    /// <summary>Kód stavu uzavřeného jednání — stejný identifikátor používá zbytek aplikace
    /// (např. ProjectService.LazyQueries.cs, RecordService.MeetingIdentifier.cs).</summary>
    private const string ClosedMeetingStateCode = "CLOSED";
```

a nahraď dotaz na `meetingIds` joinem přes číselník stavů:

```csharp
        var meetingIds = await (
            from meeting in dbContext.Jednani.AsNoTracking()
            join state in dbContext.CiselnikStavuJednani.AsNoTracking()
                on meeting.StavJednaniId equals state.Id
            where meeting.ProjektId == projectId && state.Kod == ClosedMeetingStateCode
            select meeting.Id)
            .ToListAsync(ct);
```

- [ ] **Krok 9: Spusť testy a ověř, že projdou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests"`

Expected: PASS — 5 testů zelených.

#### Cyklus C — strop 15 jednání

- [ ] **Krok 10: Napiš padající test**

Přidej do `AttendancePredictorTests`:

```csharp
    [Fact]
    public async Task PredictAsync_ZapocitaJenPoslednichPatnactJednani()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);

        // Jednání 1-12: Přítomen (12×), jednání 13-20: Videokonference (8×).
        // Bez stropu vyhraje Přítomen 12:8. V posledních patnácti (6-20) je to
        // 7× Přítomen proti 8× Videokonference → musí vyhrát Videokonference.
        for (var meetingNumber = 1; meetingNumber <= 20; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId);
            AddAttendance(db, meetingNumber, MemberOsobaId,
                meetingNumber <= 12 ? PresentStateId : OnlineStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(OnlineStateId,
            "starší jednání než posledních patnáct se nezapočítávají (spec U2)");
    }
```

- [ ] **Krok 11: Spusť test a ověř, že selže**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests.PredictAsync_ZapocitaJenPoslednichPatnactJednani"`

Expected: FAIL — „Expected result[100] to be 2, but found 1" (bez stropu vyhrálo 12× Přítomen).

- [ ] **Krok 12: Doplň strop a řazení podle čísla jednání**

V `AttendancePredictor.cs` přidej konstantu k `ClosedMeetingStateCode`:

```csharp
    /// <summary>Kolik posledních uzavřených jednání se do odhadu započítá (spec U2).</summary>
    private const int MaxConsideredMeetings = 15;
```

a doplň do dotazu řazení a `Take`:

```csharp
        // Pořadí podle čísla jednání sestupně — stejné pojetí „předchozího jednání",
        // jaké používá tisk (ExportProjectionBuilders.cs:798). Dvě různá pojetí by mátla.
        var meetingIds = await (
            from meeting in dbContext.Jednani.AsNoTracking()
            join state in dbContext.CiselnikStavuJednani.AsNoTracking()
                on meeting.StavJednaniId equals state.Id
            where meeting.ProjektId == projectId && state.Kod == ClosedMeetingStateCode
            orderby meeting.CisloJednani descending
            select meeting.Id)
            .Take(MaxConsideredMeetings)
            .ToListAsync(ct);
```

- [ ] **Krok 13: Spusť testy a ověř, že projdou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests"`

Expected: PASS — 6 testů zelených.

#### Cyklus D — rozhodnutí shody počtů

- [ ] **Krok 14: Napiš padající test**

Přidej do `AttendancePredictorTests`. Účasti se zakládají od nejstaršího jednání, takže implementace bez rozhodovacího pravidla narazí nejdřív na starší stav a vrátí ho:

```csharp
    [Fact]
    public async Task PredictAsync_PriShodePoctuVyhrajeStavZNejnovejsihoJednani()
    {
        await using var db = CreateDb();
        await SeedMeetingStatesAsync(db);

        // 2:2 — Omluven u jednání 1 a 2, Videokonference u 3 a 4.
        // Řádky se zakládají od nejstaršího, aby implementace bez rozhodnutí shody
        // sáhla po Omluven (skupina, na kterou narazí první) a test měl červený běh.
        for (var meetingNumber = 1; meetingNumber <= 4; meetingNumber++)
        {
            AddMeeting(db, meetingNumber, ClosedMeetingStateId);
            AddAttendance(db, meetingNumber, MemberOsobaId,
                meetingNumber <= 2 ? ExcusedStateId : OnlineStateId);
        }

        await db.SaveChangesAsync();

        var result = await new AttendancePredictor(db).PredictAsync(ProjektId, [MemberOsobaId]);

        result[MemberOsobaId].Should().Be(OnlineStateId,
            "při shodě počtů rozhoduje, co osoba dělala naposledy (spec §4.2)");
    }
```

- [ ] **Krok 15: Spusť test a ověř, že selže**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests.PredictAsync_PriShodePoctuVyhrajeStavZNejnovejsihoJednani"`

Expected: FAIL — „Expected result[100] to be 2, but found 3" (`OrderByDescending` je stabilní, takže při shodě zůstane skupina, na kterou se narazilo první — Omluven).

Pokud test projde hned, **nepokračuj** — znamená to, že pořadí řádků z InMemory provideru je jiné, než test předpokládá, a test by shodu nehlídal. V takovém případě otoč pořadí zakládání (nejnovější jednání první) a ověř červený běh znovu.

- [ ] **Krok 16: Doplň rozhodnutí shody podle stáří jednání**

V `AttendancePredictor.cs` vlož před `return` výpočet pořadí a doplň `ThenBy`:

```csharp
        // 0 = nejnovější jednání. Bez tohoto pravidla by při shodě počtů rozhodovalo
        // pořadí řádků z databáze a stejné zadání by mohlo dát pokaždé jiný výsledek.
        var recencyRankByMeetingId = meetingIds
            .Select((meetingId, index) => (meetingId, index))
            .ToDictionary(x => x.meetingId, x => x.index);

        return attendanceRows
            .GroupBy(row => row.OsobaId)
            .ToDictionary(
                personRows => personRows.Key,
                personRows => personRows
                    .GroupBy(row => row.StavUcastiId)
                    .OrderByDescending(stateRows => stateRows.Count())
                    .ThenBy(stateRows => stateRows.Min(row => recencyRankByMeetingId[row.JednaniId]))
                    .First().Key);
```

- [ ] **Krok 17: Spusť testy a ověř, že projdou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~AttendancePredictorTests"`

Expected: PASS — 7 testů zelených.

- [ ] **Krok 18: Checkpoint (commit se DRŽÍ)**

Run: `dotnet build PmTracker.sln`
Expected: Build succeeded, 0 warnings.

Navržená message pro pozdější commit uživatelem:

```
feat(jednani): služba odhadu účasti podle historie uzavřených jednání
```

---

### Task 3: Zapojení odhadu do zakládání jednání

**Files:**
- Modify: `PmTracker.Web/Services/MeetingService.cs` (konstruktor + pole)
- Modify: `PmTracker.Web/Services/MeetingService.WriteCommands.cs:345-387`
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:125`
- Modify: `PmTracker.Tests.Unit/Meetings/MeetingServiceSaveAttendanceBatchTests.cs:38-39`
- Modify: `PmTracker.Tests.Unit/Meetings/MeetingServiceDelegationTests.cs:15`
- Test: `PmTracker.Tests.Integration/DataStore/MeetingAttendancePredictionTests.cs`

**Interfaces:**
- Consumes: z Tasku 2 `PmTracker.Web.Services.Meetings.IAttendancePredictor.PredictAsync(int projectId, IReadOnlyCollection<int> osobaIds, CancellationToken ct = default)` vracející `Task<IReadOnlyDictionary<int, int>>` (klíč = `OsobaId`, hodnota = `StavUcastiId`), implementaci `AttendancePredictor(PmTrackerDbContext dbContext)`. Z Tasku 1 stav účasti `ONLINE` v testovací databázi.
- Produces: nic pro další tasky.

**Kontext:** `CreateMeetingAttendanceSnapshotAsync` (`MeetingService.WriteCommands.cs:345`) je volaná jen jednou, při zakládání jednání (`:69`), takže se odhad automaticky uplatní jen na nová jednání (spec U5). Integration testy skládají služby přes reálný DI kontejner (`IntegrationTestHelper.BuildServiceProvider` → `AddPmTrackerDataStore`), takže registrace v Kroku 3 stačí i pro ně.

- [ ] **Krok 1: Napiš padající Integration test**

Vytvoř `PmTracker.Tests.Integration/DataStore/MeetingAttendancePredictionTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Předvyplnění účasti u nového jednání (2026-09-05) — průchod od SaveMeeting až do SQL.
/// Rozhodovací pravidla samotná hlídá AttendancePredictorTests v Unit projektu.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingAttendancePredictionTests
{
    private readonly SqlIntegrationFixture _fixture;

    public MeetingAttendancePredictionTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveMeeting_ShouldPrefillAttendance_FromClosedMeetingHistory()
    {
        var db = await _fixture.CreateDatabaseAsync("attendance_prediction");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PredictAdmin");
        var veteranId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PredictVeteran");
        var newcomerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PredictNewcomer");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PREDICT1");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(
            dbContext, projectId, veteranId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(
            dbContext, projectId, newcomerId, ProjectRoleCodes.ProjectManager);

        var onlineStateId = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "ONLINE")
            .Select(x => x.Id)
            .SingleAsync();
        var presentStateId = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "PRESENT")
            .Select(x => x.Id)
            .SingleAsync();

        // Historie veterána: 2× videokonference, 1× přítomen — vše u uzavřených jednání.
        var historyStates = new[] { onlineStateId, onlineStateId, presentStateId };
        for (var index = 0; index < historyStates.Length; index++)
        {
            var meetingId = await IntegrationTestHelper.CreateMeetingAsync(
                dbContext, projectId, "CLOSED", meetingNumber: 9601 + index);
            dbContext.Ucast.Add(new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = veteranId,
                StavUcastiId = historyStates[index]
            });
        }

        await dbContext.SaveChangesAsync();

        var newMeetingId = store.SaveMeeting(new SaveMeetingCommand
        {
            ProjektId = projectId,
            CisloJednani = 9610,
            DatumPlanovane = new DateTime(2026, 9, 10),
            CasZacatek = new TimeOnly(9, 0),
            Misto = "Zasedačka",
            StavJednani = "OPEN"
        }, currentUser);

        var attendance = await dbContext.Ucast
            .AsNoTracking()
            .Where(x => x.JednaniId == newMeetingId)
            .ToDictionaryAsync(x => x.OsobaId, x => x.StavUcastiId);

        attendance[veteranId].Should().Be(onlineStateId,
            "veterán byl na dvou ze tří uzavřených jednání přes videokonferenci");
        attendance[newcomerId].Should().Be(presentStateId,
            "osoba bez historie dostane výchozí stav jako dosud");
    }
}
```

- [ ] **Krok 2: Spusť test a ověř, že selže**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~MeetingAttendancePredictionTests"`

Expected: FAIL — `attendance[veteranId]` je id stavu `PRESENT` místo `ONLINE` („Expected … to be X, but found Y"), protože zakládání jednání zatím dává všem stejný výchozí stav.

Integration testy potřebují běžící Docker (Colima) kvůli Testcontainers.MsSql. Když kontejner nejede, test selže na startu fixture — to **není** červený běh, který hledáme; nejdřív rozjeď Docker.

- [ ] **Krok 3: Zaregistruj službu do DI**

V `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` vlož za řádek 125 (`services.AddScoped<MeetingService>();`):

```csharp
        // Předvyplnění účasti u nového jednání (2026-09-05) — odhad z historie
        // uzavřených jednání, spec docs/superpowers/specs/2026-09-05-predvyplneni-ucasti-design.md.
        services.AddScoped<PmTracker.Web.Services.Meetings.IAttendancePredictor,
                           PmTracker.Web.Services.Meetings.AttendancePredictor>();
```

- [ ] **Krok 4: Přidej závislost do MeetingService**

V `PmTracker.Web/Services/MeetingService.cs` doplň using, pole a parametr konstruktoru (nový parametr jde **na konec**, aby se nezměnilo pořadí stávajících):

```csharp
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Meetings;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService : IMeetingService
{
    private readonly PmTrackerDbContext dbContext;
    private readonly ITextNormalizer textNormalizer;
    private readonly IPersonIdentityMatcher personIdentityMatcher;
    private readonly ICommentService commentService;
    private readonly IAuditWriteService auditWriteService;
    private readonly TimeProvider timeProvider;
    private readonly IAttendancePredictor attendancePredictor;

    public MeetingService(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher,
        ICommentService commentService,
        IAuditWriteService auditWriteService,
        TimeProvider timeProvider,
        IAttendancePredictor attendancePredictor)
    {
        this.dbContext = dbContext;
        this.textNormalizer = textNormalizer;
        this.personIdentityMatcher = personIdentityMatcher;
        this.commentService = commentService;
        this.auditWriteService = auditWriteService;
        this.timeProvider = timeProvider;
        this.attendancePredictor = attendancePredictor;
    }
}
```

- [ ] **Krok 5: Použij odhad při zakládání docházky**

V `PmTracker.Web/Services/MeetingService.WriteCommands.cs` nahraď v metodě `CreateMeetingAttendanceSnapshotAsync` (řádky 353-368) blok od `var defaultStateId = …` po `.ToList();` tímto:

```csharp
        var defaultStateId = await ResolveDefaultAttendanceStatusIdAsync(ct);
        // Předvyplnění podle historie (spec 2026-09-05): kdo na jednání běžně nechodí,
        // dostane rovnou svůj obvyklý stav a obsluha dopisuje jen výjimky.
        var predictedStateIdByOsobaId = await attendancePredictor.PredictAsync(projectId, participantIds, ct);
        var existingIds = (await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToListAsync(ct))
            .ToHashSet();

        var rows = participantIds
            .Where(personId => !existingIds.Contains(personId))
            .Select(personId => new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = personId,
                StavUcastiId = predictedStateIdByOsobaId.TryGetValue(personId, out var predictedStateId)
                    ? predictedStateId
                    : defaultStateId
            })
            .ToList();
```

- [ ] **Krok 6: Srovnej dvě existující volání konstruktoru**

V `PmTracker.Tests.Unit/Meetings/MeetingServiceSaveAttendanceBatchTests.cs` na řádcích 38-39 změň:

```csharp
    private static MeetingService CreateService(PmTrackerDbContext db)
        => new(db, null!, null!, new FakeCommentService(), new FakeAuditWriteService(), TimeProvider.System,
            new AttendancePredictor(db));
```

a doplň do usings téhož souboru:

```csharp
using PmTracker.Web.Services.Meetings;
```

V `PmTracker.Tests.Unit/Meetings/MeetingServiceDelegationTests.cs` na řádku 15 změň:

```csharp
        var sut = new MeetingService(null!, null!, null!, commentService, new FakeAuditWriteService(), TimeProvider.System, null!);
```

Test volá jen `SaveMeetingNotesBatchAsync`, která odhad nepoužívá — `null!` je tu stejná úmluva jako u ostatních nepoužitých závislostí na témže řádku.

- [ ] **Krok 7: Spusť Unit testy a ověř, že projdou**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Meetings"`

Expected: PASS — `AttendancePredictorTests`, `MeetingServiceSaveAttendanceBatchTests` i `MeetingServiceDelegationTests` zelené.

- [ ] **Krok 8: Spusť Integration test a ověř, že projde**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~MeetingAttendancePredictionTests"`

Expected: PASS — 1 test zelený.

- [ ] **Krok 9: Checkpoint (commit se DRŽÍ)**

Navržená message pro pozdější commit uživatelem:

```
feat(jednani): nové jednání předvyplní účast podle historie místo „všem přítomen"
```

---

### Task 4: Kontrolní průchod

**Files:** žádné změny — pokud něco selže, oprava patří do tasku, který to způsobil.

**Interfaces:**
- Consumes: hotové Tasky 1-3.
- Produces: podklad pro ruční ověření uživatelem.

- [ ] **Krok 1: Build celého řešení**

Run: `dotnet build PmTracker.sln`
Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Krok 2: Celá Unit sada**

Run: `dotnet test PmTracker.Tests.Unit`
Expected: PASS. Známá dřívější selhání mimo tento rozsah hlásit, neopravovat.

- [ ] **Krok 3: Celá Api sada**

Run: `dotnet test PmTracker.Tests.Api`
Expected: PASS až na známá 4 dřívější selhání gantt testů (`schedule-layered-marker today`) — ta jsou ortogonální a nesouvisejí s účastí.

- [ ] **Krok 4: Celá Integration sada**

Run: `dotnet test PmTracker.Tests.Integration`
Expected: PASS až na známé dřívější selhání `ProposalRejectAndTakeOverE2ETests` (oprávnění `proposals.accept`).

- [ ] **Krok 5: Kontrola diffu**

Run: `git status --short && git diff --stat`
Expected: změněné jsou právě soubory ze sekce „Struktura souborů" a nic víc; žádný `appsettings*.json`, žádný soubor v `bin/` nebo `obj/`.

- [ ] **Krok 6: Předání uživateli**

Shrň, co je hotové, které testy běžely a s jakým výsledkem, a která dřívější selhání zůstala. Commity zůstávají **nezacommitované** — uživatel je udělá po ruční verifikaci.

---

## Poznámky k nasazení

- Vývojový seed je vkládací (`IF NOT EXISTS`), takže existující databázi s řádkem `ONLINE` **nepřejmenuje**. Uživatel si název ve své databázi už opravil ručně.
- Produkční baseline používá `MERGE` s větví `WHEN MATCHED THEN UPDATE SET target.nazev = source.nazev`, takže jeho opětovné spuštění název srovná. Podle `project_production_iis_deploy` běží dvě instance nad dvěma databázemi (`pm-tracker`, `pm-tracker-vyvoj`) — název je potřeba srovnat v obou.
- Žádná změna schématu, tedy **žádný nový `db_upgrade_*.sql`**. Mění se jen obsah číselníkového řádku.
