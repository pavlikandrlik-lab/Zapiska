# Souběžná editace záznamu — implementační plán

> **Pro agentní workery:** POVINNÝ SUB-SKILL: použij `superpowers:executing-plans` (inline exekuce — preferencí uživatele) a postupuj task po tasku. Kroky mají checkbox (`- [ ]`) syntaxi pro sledování postupu.

**Cíl:** Odstranit falešné konflikty při ukládání záznamu, zavést detekci cizího lidského zápisu se jménem autora a zabránit dvěma uživatelům otevřít stejnou kartu k úpravám.

**Architektura:** Dva pilíře. Fáze 1 maže skalární kontrolu `ScheduleVersion` (chránila před neexistující kolizí uživatel × automat) a nahrazuje ji record-level guardem nad `row_version`, který se posouvá jen při *uživatelských* zápisech. Fáze 2 přidává pesimistický zámek karty v tabulce `zaznam_edit_zamek` s heartbeatem přibaleným na existující keep-alive.

**Tech stack:** ASP.NET Core MVC (.NET), EF Core + SQL Server, Razor views, ESM moduly ve `wwwroot/js`, gov-design-system web komponenty, xUnit + FluentAssertions, Playwright (.NET).

**Spec:** `docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md`

## Globální omezení

- **Jazyk:** kód anglicky, komentáře / uživatelské hlášky / commit messages česky (viz historie commitů: `fix(authz): …` + česká věta).
- **Žádné EF Migrations.** Schéma se mění výhradně idempotentními `db_upgrade_*.sql` skripty; adresář `Migrations` zůstává záměrně prázdný. Skript na produkci spouští operátor ručně.
- **Razor kóduje diakritiku na HTML entity** — Api testy nikdy neassertují na český text v HTML, kotví se na atributy a ASCII.
- **gov komponenty v Playwrightu:** klikat `DispatchEventAsync("click")`, čekat na třídu `hydrated`, viditelnost ověřovat přes `ToHaveCount` / atribut, ne `ToBeVisible`.
- **Nový JS modul musí být side-effect importován v `bootstrap.js`** — jinak se tiše nenačte a spadne celé UI.
- **`row_version` posouvá jen uživatelský zápis, nikdy automat.** Toto je invariant celého návrhu; hlídá ho test v Tasku 3.
- **Commity:** kroky `git commit` jsou v plánu připravené, ale spouštějí se až na pokyn uživatele — finální ruční ověření dělá uživatel sám na cílovém Edge/i15.
- **Testovací příkazy:** `dotnet test <projekt>.csproj --filter "FullyQualifiedName~<TřídaNeboMetoda>"`.

## Revize po dokončení Fáze 1 (2026-09-17, 21:49)

**Fáze 1 je hotová a zelená.** Skutečný čas **1,05 h** proti odhadu 2,75 h (viz spec §8.5).

### Co se ve Fázi 1 odchýlilo od plánu

| Task | Stav | Odchylka |
|---|---|---|
| 1 — smazat `ScheduleVersion` | ✅ hotovo | Navíc: validace `ValidateScheduleValuesAsync` se stala synchronní `ValidateScheduleValues` (bez DB už nemá co awaitovat). Architekturní test je tvrdý textový grep — neobsahuj slovo `ScheduleVersion` ani ve vysvětlujících komentářích, jinak shodí sám sebe. |
| 2 — record guard | ✅ hotovo **jinak** | `projektove_zaznamy` **nemá** `row_version` (ten mapping patří návrhům). Verzí je **id posledního auditního zápisu** — `RecordVersionQuery.ResolveVersionTokenAsync`. Viz spec §5.2. |
| 3 — bump `row_version` | ⛔️ **zrušen** | Bezpředmětný: audit se zapisuje i při uložení, které mění jen harmonogram, takže se verze posune sama. Invariant „automat verzi neposouvá" hlídá `AutomatScheduleWrite_ShouldNotChangeRecordVersion`. |
| 4 — ruční režim | ✅ hotovo **jinak** | Místo `ManualActualKrokApplier.Compute(acceptAutoEligibleKroky: true)` zůstal lokální filtr `if (!isManualStep && !rezimManual) continue;`. Důvod: `Compute` zahazuje položky s prázdným datem, takže by přes něj **přestalo fungovat mazání** už zadané skutečnosti. |

### Co z toho plyne pro Fázi 2

1. **Formát jména musí být jeden.** Fáze 1 hlásí autora jako „Příjmení Jméno" (`RecordLastWriterQuery`). Zámek nesmí použít `BuildDisplayName` z `ProjectService.RecordComposition` (ten přidává titul) — jinak bude aplikace o téže osobě mluvit dvěma způsoby. Řeší nový krok v Tasku 7.
2. **`ResolvePersonDisplayNameAsync` neexistuje** a nebude se dodělávat na `ProjectService`; jméno držitele zámku se dotáhne novým `PersonDisplayNameQuery` vedle `RecordLastWriterQuery`.
3. **Testovací infrastruktura je hotová a známá.** Integrační testy použijí `RecordConcurrencyScenario.SeedAsync(dbContext, store, marker)` (vrací `.RecordId`, `.OsobaId`, `.CurrentUser`), Api testy `ApiTestHttpHelper.BuildAjaxPost` / `BuildForm` / `ReadModalResultAsync` a `_fixture.CreateDbContext()`.
4. **`ZaznamyController` má klasický konstruktor** (`ZaznamyController.cs:32-46`) — `IRecordEditLockService` se přidá jako další parametr; `PmTrackerDbContext _db` je tam už dnes.
5. **Číslo migrace `1.4.5` je stále volné** — Fáze 1 žádný SQL skript nevytvořila.
6. **Zámek je teď jištěný zezadu.** Record guard z Fáze 1 odmítne cizí zápis i tehdy, když zámek selže, vyprší nebo ho obejdou dva taby. Selhání release beaconu tedy není ztráta dat, jen zbytečně držený zámek do TTL.
7. **Odhad Fáze 2 po kalibraci: 1,7–2,0 h** místo 4,25 h (spec §8.5). Nekrátit u položek čekajících na externí běh — plná integrační sada trvá 4 min 13 s a E2E se dvěma identitami je neznámá.

---

## Mapa souborů

| Soubor | Odpovědnost | Task |
|---|---|---|
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | mazání `ScheduleVersion` checku, `RecordVersion` guard, bump, ruční režim | 1–4 |
| `PmTracker.Web/Services/Data/RecordValidationContracts.cs` | nový kód `RECORD_STALE` + výjimka `RecordStaleException` | 2 |
| `PmTracker.Web/Services/Records/RecordLastWriterQuery.cs` (nový) | dohledání posledního lidského autora zápisu z auditu | 2 |
| `PmTracker.Web/Services/Records/RecordEditLockService.cs` (nový) | acquire / heartbeat / release zámku | 6 |
| `PmTracker.Web/Models/Entities/ZaznamEditZamekEntity.cs` (nový) | entita zámku | 5 |
| `PmTracker.Web/Data/Configuration/RecordEditLockEntityConfiguration.cs` (nový) | mapování zámku | 5 |
| `db_upgrade_1_4_5_record_edit_lock.sql` (nový) | tabulka zámku | 5 |
| `PmTracker.Web/Views/Projekty/_RecordEditLocked.cshtml` (nový) | hláška „upravuje jiný uživatel" | 7 |
| `PmTracker.Web/wwwroot/js/modules/recordEditor/editLock.js` (nový) | release beacon + předání `zaznamId` keep-alive | 8 |

---

## FÁZE 1 — odstranění falešných konfliktů (bez SQL skriptu)

### Task 1: Smazat kontrolu `ScheduleVersion` — ✅ HOTOVO

**Files:**
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs:699-720`
- Modify: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs:216-227`
- Modify: `PmTracker.Web/Services/ProjectService.ScheduleBlockComposition.cs:37`
- Modify: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml:44-46`
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs:74`
- Modify: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` (vlastnost `ScheduleVersion`)
- Modify: `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js:337`
- Test: `PmTracker.Tests.Unit/Architecture/ScheduleVersionRemovalTests.cs` (nový)

**Interfaces:**
- Consumes: nic
- Produces: `SaveRecordCommand` bez vlastnosti `ScheduleVersion`; `HarmonogramBlockViewModel` bez `ScheduleVersion`. Task 2 staví na stejném commandu a přidává `RecordVersion`.

- [ ] **Krok 1: Napiš architekturní test, který dnes selže**

`PmTracker.Tests.Unit/Architecture/ScheduleVersionRemovalTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-09-17 §6.1 — skalární kontrola ScheduleVersion je smazaná.
/// Chránila před kolizí uživatel × automat, která je nedosažitelná (§1.3),
/// a v pilotu blokovala uložení záznamů, které harmonogram vůbec nemění.
/// </summary>
public sealed class ScheduleVersionRemovalTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relPath)
        => File.ReadAllText(Path.Combine(LocateRepoRoot(), relPath));

    [Theory]
    [InlineData("PmTracker.Web/Services/RecordService.SaveRecord.cs")]
    [InlineData("PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs")]
    [InlineData("PmTracker.Web/Services/ProjectService.ScheduleBlockComposition.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs")]
    [InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs")]
    [InlineData("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml")]
    [InlineData("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js")]
    public void ScheduleVersion_NesmiZustatVKodu(string relPath)
    {
        Read(relPath).Should().NotContain("ScheduleVersion",
            $"{relPath} — spec 2026-09-17 §6.1: mechanismus ScheduleVersion je zrušen celý");
    }

    [Fact]
    public void ScheduleStaleData_PravidloNeexistuje()
    {
        Read("PmTracker.Web/Services/RecordService.SaveRecord.cs")
            .Should().NotContain("schedule_stale_data",
                "validační pravidlo schedule_stale_data je zrušeno — generovalo jen falešné konflikty");
    }
}
```

- [ ] **Krok 2: Spusť test a ověř, že selže**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleVersionRemovalTests"`
Expected: FAIL — 8 selhání (7× nalezené `ScheduleVersion`, 1× `schedule_stale_data`).

- [ ] **Krok 3: Smaž blok kontroly v `RecordService.SaveRecord.cs`**

Smaž celý blok od komentáře `// F-11: Soft concurrency check pro harmonogram` po jeho uzavírací `}` (řádky 699-720), tedy tuto část metody `ValidateScheduleValuesAsync`:

```csharp
        // F-11: Soft concurrency check pro harmonogram
        if (!string.IsNullOrEmpty(command.ScheduleVersion) && existingRecord is not null)
        {
            var currentMaxUpdatedAt = await dbContext.ZaznamHarmonogramKroky
                .Where(x => x.ZaznamId == existingRecord.Id)
                .MaxAsync(x => (DateTime?)x.UpdatedAt, ct);

            var currentVersion = currentMaxUpdatedAt.HasValue
                ? currentMaxUpdatedAt.Value.Ticks.ToString("X16")
                : string.Empty;

            if (currentVersion != command.ScheduleVersion)
            {
                AddRecordValidationIssue(
                    issues,
                    "ScheduleVersion",
                    "Harmonogram byl mezitím upraven jiným uživatelem. Načtěte záznam znovu.",
                    "schedule",
                    "schedule_stale_data",
                    null);
                return;
            }
        }
```

Zbytek metody (kontrola pořadí 1–10, duplicity, `ScheduleChronologyValidator`) zůstává beze změny. Parametr `existingRecord` v signatuře metody tím osiří — smaž ho i z volání na řádku 490 (`await ValidateScheduleValuesAsync(command, isTaskCategory, issues, ct);`).

- [ ] **Krok 4: Smaž výpočet verze v kompozici editoru**

`ProjectService.RecordEditorComposition.cs` — smaž blok:

```csharp
        // F-11: Soft concurrency check — načti MAX(UpdatedAt) harmonogramových hodnot jako version stamp
        var scheduleVersion = string.Empty;
        if (!isCreate && isTaskCategory && record.Id > 0)
        {
            var maxUpdatedAt = await dbContext.ZaznamHarmonogramKroky
                .Where(x => x.ZaznamId == record.Id)
                .MaxAsync(x => (DateTime?)x.UpdatedAt, ct);
            if (maxUpdatedAt.HasValue)
            {
                scheduleVersion = maxUpdatedAt.Value.Ticks.ToString("X16");
            }
        }
```

a všechna místa, kde se `scheduleVersion` předává dál (včetně parametru v `ScheduleBlockComposition.cs` a přiřazení `ScheduleVersion = scheduleVersion` na řádku 37).

- [ ] **Krok 5: Smaž pole z view modelu, commandu, view a JS**

```csharp
// ProjektHarmonogramTabViewModels.cs — smaž:
public string ScheduleVersion { get; init; } = string.Empty;

// RecordCommands.cs — smaž:
public string? ScheduleVersion { get; set; }
```

```html
<!-- _ScheduleBlock.cshtml — smaž celý @if blok: -->
@if (!string.IsNullOrEmpty(Model.ScheduleVersion))
{
    <input type="hidden" name="ScheduleVersion" value="@Model.ScheduleVersion" />
}
```

```javascript
// form.js:337 — vypusť poslední prvek pole:
const scheduleSnapshotKeyPrefixes = ["UiHarmonogramDatumy", "HarmonogramHodnoty"];
```

- [ ] **Krok 6: Uprav komentář v klonu návrhu**

`PmTracker.Web/Services/RecordProposalService.Queries.cs:524` — v komentáři u `with` vypusť zmínku `ScheduleVersion` ze seznamu zachovávaných polí. Kód samotný se nemění (`with` kopíruje, co existuje).

- [ ] **Krok 7: Spusť test a ověř, že prochází**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ScheduleVersionRemovalTests"`
Expected: PASS (8 testů).

- [ ] **Krok 8: Ověř, že se nic jiného nerozbilo**

Run: `dotnet build PmTracker.sln`
Expected: 0 chyb. Případné chyby budou jen „`ScheduleVersion` neexistuje" v místech, která krok 4–5 minul — doplň je.

- [ ] **Krok 9: Vytvoř sdílený seed pro testy souběhu**

`PmTracker.Tests.Integration/TestInfrastructure/RecordConcurrencyScenario.cs` (nový) — používají ho Tasky 1, 3, 4 a 6, takže vzniká jen jednou:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Spec 2026-09-17 — sdílený seed pro testy souběžné editace: projekt, subsystém,
/// osoba a jeden záznam kategorie úkol s krok řádky 1–10.
/// </summary>
public sealed record RecordConcurrencyScenario(
    int RecordId,
    int ProjectId,
    int OsobaId,
    string CategoryCode,
    string StatusCode,
    string SubsystemCode,
    CurrentUserContextViewModel CurrentUser)
{
    public static async Task<RecordConcurrencyScenario> SeedAsync(
        PmTrackerDbContext dbContext,
        IntegrationTestDataStore store,
        string marker)
    {
        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Osoba");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}_SUB", osobaId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, osobaId, "HOST");

        // Kategorie úkol: kód se mezi produkcí (U) a dev seedem liší, proto se hledá
        // podle názvu — „Úkol". Harmonogram existuje jen u této kategorie.
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Nazev.Contains("kol"))
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Id).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == subsystemId).Select(x => x.Kod).SingleAsync();

        var currentUser = IntegrationTestHelper.BuildUser(osobaId, isSuperAdmin: true);
        var recordId = store.SaveRecord(new SaveRecordCommand
        {
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = $"{marker} záznam",
            VlastnikId = osobaId,
            DatumZalozeni = new DateTime(2026, 9, 1),
            TerminUkonceni = new DateTime(2026, 12, 31),
            Subsystem = subsystemCode,
            HarmonogramHodnoty = BuildPlanRows()
        }, currentUser);

        return new RecordConcurrencyScenario(
            recordId, projectId, osobaId, categoryCode, statusCode, subsystemCode, currentUser);
    }

    /// <summary>Plán pro kroky 1–10 — bez něj PersistScheduleKroky neudělá nic.</summary>
    public static List<SaveRecordHarmonogramValueCommand> BuildPlanRows()
        => Enumerable.Range(1, 10)
            .Select(i => new SaveRecordHarmonogramValueCommand
            {
                Poradi = i,
                PlanDatum = new DateTime(2026, 9, 1).AddDays(i * 7)
            })
            .ToList();

    /// <summary>Command, který mění jen název — harmonogram posílá beze změny.</summary>
    public SaveRecordCommand BuildRenameCommand(string novyNazev) => new()
    {
        Id = RecordId,
        ProjektId = ProjectId,
        Kategorie = CategoryCode,
        Stav = StatusCode,
        Nazev = novyNazev,
        VlastnikId = OsobaId,
        DatumZalozeni = new DateTime(2026, 9, 1),
        TerminUkonceni = new DateTime(2026, 12, 31),
        Subsystem = SubsystemCode,
        HarmonogramHodnoty = BuildPlanRows()
    };

    /// <summary>Command, který mění jediné plánové datum — název zůstává.</summary>
    public SaveRecordCommand BuildScheduleOnlyCommand(int poradi, DateTime planDatum)
    {
        var command = BuildRenameCommand($"{OsobaId} záznam");
        command.Nazev = $"{ProjectId} záznam";
        var row = command.HarmonogramHodnoty.Single(x => x.Poradi == poradi);
        row.PlanDatum = planDatum;
        return command;
    }

    /// <summary>Command s ručním datem skutečnosti pro zadaný krok a režim.</summary>
    public SaveRecordCommand BuildManualActualCommand(string rezim, int poradi, DateOnly datum)
    {
        var command = BuildRenameCommand($"{ProjectId} záznam");
        command.HarmonogramRezim = rezim;
        command.ManualActualKroky = new List<ManualActualKrokDto>
        {
            new() { Poradi = poradi, AbsolutniDatum = datum }
        };
        return command;
    }
}
```

Pozn.: `SaveRecordHarmonogramValueCommand` a `ManualActualKrokDto` ověř v `Models/ViewModels/Commands/RecordCommands.cs` — pokud mají jiné názvy vlastností, sjednoť je, nevymýšlej nové.

- [ ] **Krok 10: Přidej behaviorální regresní test do integrace**

`PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs` — přidej metodu:

```csharp
    [Fact]
    public async Task SaveRecord_ShouldSucceed_WhenAutomatTouchedScheduleAfterEditorLoaded()
    {
        var db = await _fixture.CreateDatabaseAsync("record_save_after_automat_touch");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AutomatTouchAdmin");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "AUTOTOUCH");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "AUTOTOUCH_SUB", adminId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, adminId, "HOST");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "AUTOTOUCH");

        // Automat sáhne do skutečnosti kroku 4 poté, co si uživatel otevřel editor.
        var krok = await dbContext.ZaznamHarmonogramKroky
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum = new DateTime(2026, 9, 1);
        krok.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;
        krok.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Uživatel mění jen název — save musí projít.
        var act = () => store.SaveRecord(scenario.BuildRenameCommand("Nový název"), scenario.CurrentUser);

        act.Should().NotThrow<RecordValidationException>();
        (await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == scenario.RecordId)).Nazev
            .Should().Be("Nový název");
    }
```

Seed i commandy staví na `RecordConcurrencyScenario` z kroku 9 — `var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "AUTOTOUCH");`, pak `store.SaveRecord(scenario.BuildRenameCommand("Nový název"), scenario.CurrentUser);`.

- [ ] **Krok 11: Spusť integrační test**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~SaveRecord_ShouldSucceed_WhenAutomatTouchedScheduleAfterEditorLoaded"`
Expected: PASS.

- [ ] **Krok 12: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Integration/TestInfrastructure/RecordConcurrencyScenario.cs PmTracker.Tests.Unit/Architecture/ScheduleVersionRemovalTests.cs PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs
git commit -m "fix(zaznamy): smazat ScheduleVersion check — blokoval uložení kvůli zásahu automatu

Kontrola chránila před kolizí uživatel x automat, která je nedosažitelná:
do skutečnosti auto-eligible kroků se uživatel nedostane ani v ručním režimu.
Spec: docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md §6.1

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Record guard — `RecordVersion` a hláška se jménem — ✅ HOTOVO (přes audit id, ne rowversion)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs`
- Modify: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` (naplnění `RecordVersion`)
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml:83` (mezi ostatní hidden pole)
- Modify: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs`
- Modify: `PmTracker.Web/Services/Data/RecordValidationContracts.cs`
- Create: `PmTracker.Web/Services/Records/RecordLastWriterQuery.cs`
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs:380` (u načtení `existingRecord`)
- Modify: `PmTracker.Web/Controllers/BaseController.Commands.cs:237-258`
- Modify: `PmTracker.Web/wwwroot/js/modules/ajax.js:232-246`
- Test: `PmTracker.Tests.Unit/Records/RecordStaleGuardTests.cs` (nový)

**Interfaces:**
- Consumes: `SaveRecordCommand` z Tasku 1.
- Produces:
  - `SaveRecordCommand.RecordVersion` (`string?`, base64 `rowversion`)
  - `AjaxErrorCodes.RecordStale` = `"RECORD_STALE"`
  - `RecordStaleException(string message)` s `ErrorCode => AjaxErrorCodes.RecordStale`
  - `RecordLastWriterQuery.ResolveAsync(PmTrackerDbContext, int recordId, CancellationToken) → Task<RecordLastWriter?>` kde `RecordLastWriter(string DisplayName, DateTime AtUtc)`
  - Task 3 staví na stejném místě v `SaveRecordAsync`.

- [ ] **Krok 1: Napiš failující unit test na sestavení hlášky**

`PmTracker.Tests.Unit/Records/RecordStaleGuardTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Spec 2026-09-17 §5.3 — hláška o cizím zápisu musí pojmenovat autora,
/// a bez dohledaného autora nesmí spadnout.
/// </summary>
public sealed class RecordStaleGuardTests
{
    [Fact]
    public void Message_ObsahujeJmenoACas_KdyzJeAutorZnamy()
    {
        var message = RecordStaleMessageBuilder.Build(
            new RecordLastWriter("Novák Jan", new DateTime(2026, 9, 17, 14, 12, 0, DateTimeKind.Utc)));

        message.Should().Contain("Novák Jan");
        message.Should().Contain("14:12");
    }

    [Fact]
    public void Message_JeSrozumitelna_IKdyzAutorNeniZnamy()
    {
        var message = RecordStaleMessageBuilder.Build(null);

        message.Should().NotBeNullOrWhiteSpace();
        message.Should().NotContain("null");
    }

    [Fact]
    public void ErrorCode_JeRecordStale()
    {
        new RecordStaleException("x").ErrorCode.Should().Be("RECORD_STALE");
    }
}
```

- [ ] **Krok 2: Spusť test a ověř, že neprojde kompilací**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordStaleGuardTests"`
Expected: FAIL — `RecordStaleMessageBuilder`, `RecordLastWriter` ani `RecordStaleException` neexistují.

- [ ] **Krok 3: Doplň kód chyby a výjimku**

`PmTracker.Web/Services/Data/RecordValidationContracts.cs` — do `AjaxErrorCodes` přidej konstantu a pod `RecordValidationException` přidej výjimku:

```csharp
    public const string RecordStale = "RECORD_STALE";
```

```csharp
/// <summary>
/// Spec 2026-09-17 §5 — záznam mezitím uložil jiný ČLOVĚK (ne automat).
/// Automat row_version neposouvá, takže sem jeho zápisy nikdy nevedou.
/// </summary>
public sealed class RecordStaleException(string message) : Exception(message)
{
    public string ErrorCode => AjaxErrorCodes.RecordStale;
}
```

- [ ] **Krok 4: Vytvoř dotaz na posledního autora a builder hlášky**

`PmTracker.Web/Services/Records/RecordLastWriterQuery.cs`:

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Records;

public sealed record RecordLastWriter(string DisplayName, DateTime AtUtc);

/// <summary>
/// Spec 2026-09-17 §5.3 — poslední lidský zápis do záznamu z auditní tabulky.
/// Audit je jediný zdroj: projektove_zaznamy nemá sloupec „kdo naposledy upravil".
/// </summary>
public static class RecordLastWriterQuery
{
    private const string RecordEntityType = "zaznam";

    public static async Task<RecordLastWriter?> ResolveAsync(
        PmTrackerDbContext dbContext,
        int recordId,
        CancellationToken ct)
    {
        var entityId = recordId.ToString(CultureInfo.InvariantCulture);
        var row = await (
            from audit in dbContext.AuthzAuditLog.AsNoTracking()
            where audit.EntityType == RecordEntityType
                && audit.EntityId == entityId
                && (audit.Action == "update" || audit.Action == "approve")
                && audit.ActorOsobaId != null
            orderby audit.CreatedAt descending
            join osoba in dbContext.Osoby.AsNoTracking() on audit.ActorOsobaId equals osoba.Id
            select new { osoba.Titul, osoba.Jmeno, osoba.Prijmeni, audit.CreatedAt })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var name = $"{row.Prijmeni} {row.Jmeno}".Trim();
        return new RecordLastWriter(string.IsNullOrWhiteSpace(name) ? "neznámý uživatel" : name, row.CreatedAt);
    }
}

public static class RecordStaleMessageBuilder
{
    public static string Build(RecordLastWriter? writer)
    {
        if (writer is null)
        {
            return "Záznam mezitím uložil jiný uživatel. Vaše změny nelze uložit přes cizí verzi — načtěte záznam znovu.";
        }

        var stamp = writer.AtUtc.ToLocalTime().ToString("d.M.yyyy HH:mm", CultureInfo.GetCultureInfo("cs-CZ"));
        return $"Záznam mezitím uložil jiný uživatel: {writer.DisplayName} ({stamp}). "
             + "Vaše změny nelze uložit přes cizí verzi — načtěte záznam znovu.";
    }
}
```

Pozn.: název `Osoby` u DbSetu ověř v `PmTrackerDbContext.cs` a případně sjednoť.

- [ ] **Krok 5: Spusť unit test**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordStaleGuardTests"`
Expected: PASS (3 testy).

- [ ] **Krok 6: Přenes `RowVersion` do formuláře**

`ZaznamEditViewModels.cs` — přidej vlastnost:

```csharp
    /// <summary>Base64 rowversion záznamu; prázdné u nového záznamu. Spec 2026-09-17 §5.2.</summary>
    public string RecordVersion { get; set; } = string.Empty;
```

`ProjectService.RecordEditorComposition.cs` — do inicializátoru `new ZaznamEditViewModel { … }` přidej:

```csharp
            RecordVersion = record.RowVersion is { Length: > 0 }
                ? Convert.ToBase64String(record.RowVersion)
                : string.Empty,
```

`_EditZaznamForm.cshtml` — mezi stávající hidden pole (za `<input type="hidden" name="CisloZaznamu" … />`):

```html
    <input type="hidden" name="RecordVersion" value="@Model.RecordVersion" />
```

`RecordCommands.cs` — do `SaveRecordCommand`:

```csharp
    /// <summary>Base64 rowversion načteného záznamu. Prázdné = kontrola se přeskočí (create). Spec 2026-09-17 §5.2.</summary>
    public string? RecordVersion { get; set; }
```

- [ ] **Krok 7: Zapoj kontrolu do `SaveRecordAsync`**

`RecordService.SaveRecord.cs` — hned za načtení `existingRecord` (řádek 380) vlož:

```csharp
        // Spec 2026-09-17 §5 — cizí LIDSKÝ zápis. Prázdná verze = create nebo starý
        // formulář z doby před nasazením; v obou případech se kontrola přeskočí.
        if (existingRecord is not null && !string.IsNullOrWhiteSpace(command.RecordVersion))
        {
            var submitted = TryParseRowVersion(command.RecordVersion);
            if (submitted is not null && !submitted.SequenceEqual(existingRecord.RowVersion))
            {
                var writer = await RecordLastWriterQuery.ResolveAsync(dbContext, existingRecord.Id, ct);
                throw new RecordStaleException(RecordStaleMessageBuilder.Build(writer));
            }
        }
```

a do stejného partialu privátní helper:

```csharp
    private static byte[]? TryParseRowVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // Poškozený vstup nesmí shodit save — kontrola se v takovém případě jen přeskočí.
        Span<byte> buffer = stackalloc byte[16];
        return Convert.TryFromBase64String(value, buffer, out var written)
            ? buffer[..written].ToArray()
            : null;
    }
```

- [ ] **Krok 8: Přelož výjimku na AJAX odpověď**

`BaseController.Commands.cs` — v `BuildAjaxExceptionResult` **před** větev s `InvalidOperationException`:

```csharp
        if (exception is RecordStaleException staleException)
        {
            return AjaxErrorResult(
                staleException.Message,
                staleException.ErrorCode,
                staleException);
        }
```

- [ ] **Krok 9: Přidej tlačítko „Obnovit stránku" pro nový kód**

`ajax.js` — rozšiř podmínku u recovery tlačítka:

```javascript
        const normalizedErrorCode = (errorCode || "").toUpperCase();
        if (normalizedErrorCode === sessionStaleErrorCode
            || normalizedErrorCode === sessionExpiredErrorCode
            || normalizedErrorCode === "RECORD_STALE") {
```

- [ ] **Krok 10: Přidej guard test na chování po uložení (spec §5.4)**

Spec tvrdí, že obnova `RecordVersion` po uložení není potřeba, protože klient dělá plnou navigaci. Kdyby se editor někdy překlopil na in-place refresh, druhé uložení bez reloadu by spadlo na vlastní předchozí zápis — tenhle test to zachytí.

`PmTracker.Tests.Api/Controllers/RecordEditorControllerTests.cs` — přidej:

```csharp
    [Fact]
    public async Task Save_ShouldReturnPageRefreshScope_SoRecordVersionIsAlwaysReloaded()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        var response = await PostSaveAsync(client); // helper z tohoto souboru
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.GetProperty("refreshScope").GetString().Should().Be("page",
            "spec 2026-09-17 §5.4 — editor se po uložení celý přenačte; jinak je nutné "
            + "vracet nové RowVersion a přepisovat hidden pole RecordVersion");
    }
```

Pokud v souboru helper pro POST Save ještě není, použij stejný postup jako sousední testy: `CreateClient(new() { AllowAutoRedirect = false })`, hlavička `X-Requested-With`, `FormUrlEncodedContent` s antiforgery tokenem staženým z GET editoru.

- [ ] **Krok 11: Ověř build a doběhnutí testů**

Run: `dotnet build PmTracker.sln && dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj`
Expected: build 0 chyb; unit testy bez nových selhání (pozor: repo má evidovaná předchozí selhání mimo tuto oblast — porovnávej proti stavu před taskem).

- [ ] **Krok 12: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Unit/Records/RecordStaleGuardTests.cs PmTracker.Tests.Api/Controllers/RecordEditorControllerTests.cs
git commit -m "feat(zaznamy): record guard — cizí zápis odmítnut se jménem autora

RecordVersion (rowversion) putuje formulářem, při neshodě RECORD_STALE
s dohledaným autorem z auditu a tlačítkem Obnovit stránku.
Spec: docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md §5

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: ~~Posun `row_version` při uživatelském zápisu do harmonogramu~~ — ⛔️ ZRUŠEN (audit posouvá verzi sám)

**Files:**
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs:250` (výsledek `PersistScheduleKrokyAsync`)
- Modify: `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs:195` (`ApplyApprovedScheduleProposalAsync`)
- Test: `PmTracker.Tests.Integration/DataStore/RecordRowVersionBumpTests.cs` (nový)

**Interfaces:**
- Consumes: `RecordVersion` guard z Tasku 2 (bez bumpu by guard neviděl změny, které se týkají jen kroků).
- Produces: invariant „uživatelský zápis do kroků posune `row_version` záznamu, automat nikdy".

- [ ] **Krok 1: Napiš failující integrační test**

`PmTracker.Tests.Integration/DataStore/RecordRowVersionBumpTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-09-17 §5.1 — row_version posune JEN uživatelský zápis.
/// Kdyby ho posouval automat, vrátily by se falešné konflikty z pilotu.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordRowVersionBumpTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordRowVersionBumpTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveRecord_ShouldBumpRowVersion_WhenOnlyScheduleChanged()
    {
        var db = await _fixture.CreateDatabaseAsync("rowversion_bump_schedule_only");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, marker);

        var before = (await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == scenario.RecordId)).RowVersion;

        // Uživatel mění POUZE plánové datum kroku 3.
        store.SaveRecord(scenario.BuildScheduleOnlyCommand(poradi: 3, planDatum: new DateTime(2026, 10, 5)), scenario.CurrentUser);

        var after = (await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == scenario.RecordId)).RowVersion;
        after.Should().NotEqual(before, "uživatelský zápis do kroků musí posunout row_version záznamu");
    }

    [Fact]
    public async Task AutomatSync_ShouldNotBumpRowVersion()
    {
        var db = await _fixture.CreateDatabaseAsync("rowversion_no_bump_automat");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, marker);

        var before = (await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == scenario.RecordId)).RowVersion;

        // Automat zapíše skutečnost kroku 4 přesně tak, jak to dělá ApplyPlanAsync.
        var krok = await dbContext.ZaznamHarmonogramKroky.SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum = new DateTime(2026, 9, 1);
        krok.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Automat;
        krok.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var after = (await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == scenario.RecordId)).RowVersion;
        after.Should().Equal(before, "automat nesmí posunout row_version — jinak se vrátí falešné konflikty");
    }
}
```

Seed i commandy staví na `RecordConcurrencyScenario` (Task 1, krok 9): `var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "ROWVER");` a `store.SaveRecord(scenario.BuildScheduleOnlyCommand(poradi: 3, planDatum: new DateTime(2026, 10, 5)), scenario.CurrentUser);`.

- [ ] **Krok 2: Spusť testy**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordRowVersionBumpTests"`
Expected: první test FAIL (`row_version` se neposunul), druhý PASS.

- [ ] **Krok 3: Doplň bump do `SaveRecordAsync`**

`RecordService.SaveRecord.cs` — nahraď zahozený výsledek `PersistScheduleKrokyAsync` (dnes `_ = await …`) tímto:

```csharp
            var scheduleChanged = await PersistScheduleKrokyAsync(
                entity, command, isTaskCategory, pendingScheduleProposalLock.LocksSchedule, innerCt);

            // Spec 2026-09-17 §5.2 — když se změnily JEN kroky, řádek záznamu by zůstal
            // netknutý a row_version by se neposunul. Vynutíme UPDATE ve stejné transakci.
            if (scheduleChanged && dbContext.Entry(entity).State == EntityState.Unchanged)
            {
                dbContext.Entry(entity).Property(x => x.Nazev).IsModified = true;
            }
```

- [ ] **Krok 4: Doplň bump do schválení návrhu harmonogramu**

`RecordProposalService.DecisionCommands.cs` — v `ApplyApprovedScheduleProposalAsync` na konci metody, před poslední `SaveChangesAsync`:

```csharp
        // Spec 2026-09-17 §5.2 — schválení návrhu je LIDSKÝ zápis mimo editor;
        // zámek karty na něj nedosáhne, takže ho musí zachytit record guard.
        if (_dbContext.Entry(record).State == EntityState.Unchanged)
        {
            _dbContext.Entry(record).Property(x => x.Nazev).IsModified = true;
        }
```

- [ ] **Krok 5: Spusť testy znovu**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordRowVersionBumpTests"`
Expected: PASS (oba testy).

- [ ] **Krok 6: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Integration/DataStore/RecordRowVersionBumpTests.cs
git commit -m "feat(zaznamy): row_version se posune při uživatelském zápisu do harmonogramu

Save kroků i schválení návrhu nově vynutí UPDATE řádku záznamu, automat ne.
Tím record guard zachytí i cizí zápis mimo editor.
Spec: docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md §5.1

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Ruční režim — uložit datum i u auto-eligible kroků — ✅ HOTOVO

**Files:**
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs:846-893` (`PersistScheduleKrokyAsync`)
- Test: `PmTracker.Tests.Integration/DataStore/ManualActualAutoEligibleTests.cs` (nový)

**Interfaces:**
- Consumes: `ManualActualKrokApplier.Compute(IReadOnlyList<ManualActualKrokDto>, bool acceptAutoEligibleKroky)` (existuje, `Services/Records/ManualActualKrokApplier.cs`).
- Produces: v režimu `HarmonogramRezim = "Manual"` se `ManualActualKroky` uloží i pro kroky 1/3/4/6/7/10 a nastaví `SkutecnostRezim = Manual`.

- [ ] **Krok 1: Napiš failující integrační test**

`PmTracker.Tests.Integration/DataStore/ManualActualAutoEligibleTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-09-17 §6.2 — v ručním režimu UI nabízí datum i u kroků 1/3/4/6/7/10,
/// ale server ho dosud tiše zahazoval (phantom UI). Po opravě se uloží
/// a krok se přepne do Manual, takže ho automat přestane přepisovat.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ManualActualAutoEligibleTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ManualActualAutoEligibleTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveRecord_ShouldPersistManualDate_ForAutoEligibleKrok_InManualRezim()
    {
        var db = await _fixture.CreateDatabaseAsync("manual_actual_auto_eligible");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, marker);

        store.SaveRecord(
            scenario.BuildManualActualCommand(rezim: "Manual", poradi: 4, datum: new DateOnly(2026, 9, 10)),
            scenario.CurrentUser);

        var krok = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum.Should().Be(new DateTime(2026, 9, 10));
        krok.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        krok.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
    }

    [Fact]
    public async Task SaveRecord_ShouldIgnoreManualDate_ForAutoEligibleKrok_InAutoRezim()
    {
        var db = await _fixture.CreateDatabaseAsync("manual_actual_auto_eligible_ignored");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, marker);

        var act = () => store.SaveRecord(
            scenario.BuildManualActualCommand(rezim: "Auto", poradi: 4, datum: new DateOnly(2026, 9, 10)),
            scenario.CurrentUser);

        act.Should().NotThrow("v Auto režimu se cizí data jen ignorují, nespadne to");
        var krok = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum.Should().BeNull("v Auto režimu krok patří automatu");
    }
}
```

- [ ] **Krok 2: Spusť testy**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~ManualActualAutoEligibleTests"`
Expected: první FAIL (`SkutecnostDatum` je null), druhý PASS.

- [ ] **Krok 3: Přepiš filtr v `PersistScheduleKrokyAsync`**

Nahraď dnešní druhou smyčku:

```csharp
        foreach (var mk in manualByPoradi.Values)
        {
            if (!PmTracker.Web.Models.ViewModels.HarmonogramManualSteps.IsManual(mk.Poradi))
            {
                continue;
            }
```

tímto (zbytek těla smyčky beze změny):

```csharp
        // Spec 2026-09-17 §6.2 — v ručním režimu patří i auto-eligible kroky uživateli.
        // Compute(acceptAutoEligibleKroky: true) sdílí validaci se schvalovací cestou;
        // filtr podle režimu je tady, aby stray data v Auto režimu jen propadla (ne výjimka).
        foreach (var applied in ManualActualKrokApplier.Compute(
                     command.ManualActualKroky, acceptAutoEligibleKroky: true))
        {
            var isManualStep = PmTracker.Web.Models.ViewModels.HarmonogramManualSteps.IsManual(applied.Poradi);
            if (!isManualStep && !rezimManual)
            {
                continue; // Auto režim: auto-eligible kroky řídí automat
            }
```

Uvnitř smyčky pak nahraď `mk` za `applied` a `mk.AbsolutniDatum?.ToDateTime(TimeOnly.MinValue)` za `applied.AbsolutniDatum.ToDateTime(TimeOnly.MinValue)` (Compute vrací jen kroky s vyplněným datem, takže `Zdroj` je vždy `Manual`).

Pozor: první smyčka nad `HarmonogramHodnoty` používá `manualByPoradi` pro override u kroků 2/5/8/9 — tu ponech, jen její zdroj přepiš na výsledek téhož `Compute` volání, ať se validace nedubluje.

- [ ] **Krok 4: Spusť testy**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~ManualActualAutoEligibleTests"`
Expected: PASS (oba).

- [ ] **Krok 5: Ověř, že se nerozbil harmonogram jinde**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~Harmonogram" && dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~Harmonogram"`
Expected: žádné nové selhání oproti stavu před taskem.

- [ ] **Krok 6: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Integration/DataStore/ManualActualAutoEligibleTests.cs
git commit -m "fix(harmonogram): ruční datum se uloží i u kroků 1/3/4/6/7/10

UI ten input v ručním režimu nabízelo, server ho zahazoval (phantom UI).
Nově se uloží a krok přejde do Manual, takže ho automat přestane přepisovat.
Spec: docs/superpowers/specs/2026-09-17-record-edit-concurrency-design.md §6.2

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Krok 7: Změř a zapiš skutečný čas fáze 1**

Do spec do §8.5 zapiš naměřený čas fáze 1 a přepočítej odhad fáze 2. Toto je kalibrační bod dohodnutý s uživatelem.

---

## FÁZE 2 — pesimistický zámek karty

### Task 5: Tabulka zámku, entita, mapování

**Files:**
- Create: `db_upgrade_1_4_5_record_edit_lock.sql`
- Create: `PmTracker.Web/Models/Entities/ZaznamEditZamekEntity.cs`
- Create: `PmTracker.Web/Data/Configuration/RecordEditLockEntityConfiguration.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`
- Modify: `db_check_applied_upgrades.sql`
- Test: `PmTracker.Tests.Unit/Architecture/RecordEditLockSchemaTests.cs` (nový)

**Interfaces:**
- Produces: `ZaznamEditZamekEntity { int ZaznamId; int OsobaId; DateTime ZiskanoAt; DateTime HeartbeatAt; }`, `PmTrackerDbContext.ZaznamEditZamky`. Task 6 na tom staví.

- [ ] **Krok 1: Napiš SQL migraci**

`db_upgrade_1_4_5_record_edit_lock.sql` — hlavička ve stylu `db_upgrade_1_4_4_search_index.sql` (blok komentáře s kontextem, rozsahem, příkazem `sqlcmd` a poznámkou o idempotenci), tělo:

```sql
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.5] zaznam_edit_zamek — start';

IF OBJECT_ID(N'dbo.zaznam_edit_zamek', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zaznam_edit_zamek
    (
        zaznam_id    INT           NOT NULL,
        osoba_id     INT           NOT NULL,
        ziskano_at   DATETIME2(3)  NOT NULL,
        heartbeat_at DATETIME2(3)  NOT NULL,
        CONSTRAINT PK_zaznam_edit_zamek PRIMARY KEY CLUSTERED (zaznam_id),
        CONSTRAINT FK_zaznam_edit_zamek_zaznam FOREIGN KEY (zaznam_id)
            REFERENCES dbo.projektove_zaznamy (id) ON DELETE CASCADE,
        CONSTRAINT FK_zaznam_edit_zamek_osoba FOREIGN KEY (osoba_id)
            REFERENCES dbo.osoby (id)
    );
    PRINT N'[1.4.5] tabulka zaznam_edit_zamek vytvořena';
END
ELSE
    PRINT N'[1.4.5] tabulka zaznam_edit_zamek už existuje — přeskočeno';

PRINT N'[1.4.5] hotovo';
```

- [ ] **Krok 2: Přidej otisk do diagnostiky**

`db_check_applied_upgrades.sql` — do seznamu skriptů přidej řádek pro `1.4.5` s otiskem `OBJECT_ID(N'dbo.zaznam_edit_zamek', N'U') IS NOT NULL`, ve stejném tvaru, jaký používají sousední skripty.

- [ ] **Krok 3: Napiš architekturní test na mapování**

`PmTracker.Tests.Unit/Architecture/RecordEditLockSchemaTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class RecordEditLockSchemaTests
{
    [Fact]
    public void ZaznamEditZamek_JeNamapovanNaSpravnouTabulku()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new PmTrackerDbContext(opts);

        var entityType = db.Model.FindEntityType(typeof(ZaznamEditZamekEntity));

        entityType.Should().NotBeNull();
        entityType!.GetTableName().Should().Be("zaznam_edit_zamek");
        entityType.FindPrimaryKey()!.Properties.Single().Name
            .Should().Be(nameof(ZaznamEditZamekEntity.ZaznamId));
    }
}
```

- [ ] **Krok 4: Spusť test — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordEditLockSchemaTests"`
Expected: FAIL — typ `ZaznamEditZamekEntity` neexistuje.

- [ ] **Krok 5: Vytvoř entitu a mapování**

`PmTracker.Web/Models/Entities/ZaznamEditZamekEntity.cs`:

```csharp
namespace PmTracker.Web.Models.Entities;

/// <summary>
/// Spec 2026-09-17 §4.1 — advisory zámek karty záznamu. Jeden řádek na záznam,
/// expiruje TTL bez heartbeatu (vyhodnocuje se při acquire, žádný úklidový job).
/// </summary>
public sealed class ZaznamEditZamekEntity
{
    public int ZaznamId { get; set; }
    public int OsobaId { get; set; }
    public DateTime ZiskanoAt { get; set; }
    public DateTime HeartbeatAt { get; set; }
}
```

`PmTracker.Web/Data/Configuration/RecordEditLockEntityConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Data.Configuration;

internal sealed class RecordEditLockEntityConfiguration : IEntityTypeConfiguration<ZaznamEditZamekEntity>
{
    public void Configure(EntityTypeBuilder<ZaznamEditZamekEntity> builder)
    {
        builder.ToTable("zaznam_edit_zamek");
        builder.HasKey(x => x.ZaznamId);
        builder.Property(x => x.ZaznamId).HasColumnName("zaznam_id");
        builder.Property(x => x.OsobaId).HasColumnName("osoba_id");
        builder.Property(x => x.ZiskanoAt).HasColumnName("ziskano_at");
        builder.Property(x => x.HeartbeatAt).HasColumnName("heartbeat_at");
    }
}
```

`PmTrackerDbContext.cs` — přidej DbSet vedle ostatních:

```csharp
    public DbSet<ZaznamEditZamekEntity> ZaznamEditZamky => Set<ZaznamEditZamekEntity>();
```

- [ ] **Krok 6: Spusť test a build**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordEditLockSchemaTests" && dotnet build PmTracker.sln`
Expected: PASS, build 0 chyb.

- [ ] **Krok 7: Commit**

```bash
git add db_upgrade_1_4_5_record_edit_lock.sql db_check_applied_upgrades.sql PmTracker.Web PmTracker.Tests.Unit/Architecture/RecordEditLockSchemaTests.cs
git commit -m "feat(zaznamy): schéma zámku karty (zaznam_edit_zamek)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: `RecordEditLockService`

**Files:**
- Create: `PmTracker.Web/Services/Records/RecordEditLockService.cs`
- Modify: `PmTracker.Web/Program.cs` (registrace do DI vedle ostatních služeb)
- Test: `PmTracker.Tests.Integration/DataStore/RecordEditLockDataStoreTests.cs` (nový)

**Interfaces:**
- Consumes: `ZaznamEditZamekEntity`, `PmTrackerDbContext.ZaznamEditZamky` (Task 5).
- Produces:
  - `IRecordEditLockService.TryAcquireAsync(int zaznamId, int osobaId, CancellationToken) → Task<RecordEditLockResult>`
  - `IRecordEditLockService.HeartbeatAsync(int zaznamId, int osobaId, CancellationToken) → Task`
  - `IRecordEditLockService.ReleaseAsync(int zaznamId, int osobaId, CancellationToken) → Task`
  - `RecordEditLockResult(bool Acquired, int? HolderOsobaId, DateTime? HolderSinceUtc)`
  - `RecordEditLockService.Ttl` = `TimeSpan.FromMinutes(15)`
  - Task 7 a 8 na tom staví.

- [ ] **Krok 1: Napiš failující integrační testy**

`PmTracker.Tests.Integration/DataStore/RecordEditLockDataStoreTests.cs` — tři fakty:

```csharp
    [Fact]
    public async Task TryAcquire_ShouldSucceed_ForFirstUser()
    {
        // arrange: záznam + dvě osoby
        var result = await service.TryAcquireAsync(recordId, userA, CancellationToken.None);
        result.Acquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquire_ShouldFail_ForSecondUser_AndReportHolder()
    {
        await service.TryAcquireAsync(recordId, userA, CancellationToken.None);

        var result = await service.TryAcquireAsync(recordId, userB, CancellationToken.None);

        result.Acquired.Should().BeFalse();
        result.HolderOsobaId.Should().Be(userA);
        result.HolderSinceUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task TryAcquire_ShouldTakeOver_WhenHeartbeatOlderThanTtl()
    {
        await service.TryAcquireAsync(recordId, userA, CancellationToken.None);
        var zamek = await dbContext.ZaznamEditZamky.SingleAsync(x => x.ZaznamId == recordId);
        zamek.HeartbeatAt = DateTime.UtcNow - RecordEditLockService.Ttl - TimeSpan.FromMinutes(1);
        await dbContext.SaveChangesAsync();

        var result = await service.TryAcquireAsync(recordId, userB, CancellationToken.None);

        result.Acquired.Should().BeTrue("po vypršení TTL zámek přebírá další uživatel");
    }

    [Fact]
    public async Task TryAcquire_ShouldBeReentrant_ForSameUser()
    {
        await service.TryAcquireAsync(recordId, userA, CancellationToken.None);

        var result = await service.TryAcquireAsync(recordId, userA, CancellationToken.None);

        result.Acquired.Should().BeTrue("druhý tab téhož uživatele nesmí zamknout sám sebe");
    }
```

Arrange (infrastruktura z Fáze 1, ověřená): `var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCK");` pro záznam, `var userB = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockUserB");` pro druhou osobu; `userA = scenario.OsobaId`, `recordId = scenario.RecordId`, `service = new RecordEditLockService(dbContext, TimeProvider.System)`. Pozor: `RecordConcurrencyScenario` je `internal` (typ `IntegrationTestDataStore` je internal).

- [ ] **Krok 2: Spusť testy — musí selhat**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordEditLockDataStoreTests"`
Expected: FAIL — `RecordEditLockService` neexistuje.

- [ ] **Krok 3: Implementuj službu**

`PmTracker.Web/Services/Records/RecordEditLockService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Records;

public sealed record RecordEditLockResult(bool Acquired, int? HolderOsobaId, DateTime? HolderSinceUtc);

public interface IRecordEditLockService
{
    Task<RecordEditLockResult> TryAcquireAsync(int zaznamId, int osobaId, CancellationToken ct = default);
    Task HeartbeatAsync(int zaznamId, int osobaId, CancellationToken ct = default);
    Task ReleaseAsync(int zaznamId, int osobaId, CancellationToken ct = default);
}

/// <summary>
/// Spec 2026-09-17 §4 — advisory zámek karty. Acquire je jediný atomický MERGE,
/// aby dva souběžné požadavky nemohly zámek získat oba.
/// </summary>
public sealed class RecordEditLockService(PmTrackerDbContext dbContext, TimeProvider timeProvider)
    : IRecordEditLockService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);

    public async Task<RecordEditLockResult> TryAcquireAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiryUtc = nowUtc - Ttl;

        // MERGE zapíše jen tehdy, když zámek nikdo nedrží, drží ho tentýž uživatel,
        // nebo vypršel. Jinak nic nezmění a my níž dotáhneme držitele.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
MERGE dbo.zaznam_edit_zamek WITH (HOLDLOCK) AS target
USING (SELECT {zaznamId} AS zaznam_id) AS source
    ON target.zaznam_id = source.zaznam_id
WHEN MATCHED AND (target.osoba_id = {osobaId} OR target.heartbeat_at < {expiryUtc})
    THEN UPDATE SET osoba_id = {osobaId},
                    ziskano_at = CASE WHEN target.osoba_id = {osobaId} THEN target.ziskano_at ELSE {nowUtc} END,
                    heartbeat_at = {nowUtc}
WHEN NOT MATCHED
    THEN INSERT (zaznam_id, osoba_id, ziskano_at, heartbeat_at)
         VALUES ({zaznamId}, {osobaId}, {nowUtc}, {nowUtc});", ct);

        var zamek = await dbContext.ZaznamEditZamky.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ZaznamId == zaznamId, ct);

        if (zamek is null || zamek.OsobaId == osobaId)
        {
            return new RecordEditLockResult(true, osobaId, zamek?.ZiskanoAt);
        }

        return new RecordEditLockResult(false, zamek.OsobaId, zamek.ZiskanoAt);
    }

    public async Task HeartbeatAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.ZaznamEditZamky
            .Where(x => x.ZaznamId == zaznamId && x.OsobaId == osobaId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeartbeatAt, nowUtc), ct);
    }

    public async Task ReleaseAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        await dbContext.ZaznamEditZamky
            .Where(x => x.ZaznamId == zaznamId && x.OsobaId == osobaId)
            .ExecuteDeleteAsync(ct);
    }
}
```

`Program.cs` — registrace vedle ostatních scoped služeb:

```csharp
builder.Services.AddScoped<
    PmTracker.Web.Services.Records.IRecordEditLockService,
    PmTracker.Web.Services.Records.RecordEditLockService>();
```

- [ ] **Krok 4: Spusť testy**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordEditLockDataStoreTests"`
Expected: PASS (4 testy).

- [ ] **Krok 5: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Integration/DataStore/RecordEditLockDataStoreTests.cs
git commit -m "feat(zaznamy): RecordEditLockService — atomický acquire, TTL 15 min, re-entrance

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Brána v `GET Edit` a stránka „upravuje jiný uživatel"

**Files:**
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs:106` (akce `Edit`)
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Commands.cs:13` (release po úspěšném Save)
- Create: `PmTracker.Web/Views/Projekty/_RecordEditLocked.cshtml`
- Test: `PmTracker.Tests.Api/Controllers/RecordEditLockControllerTests.cs` (nový)

**Interfaces:**
- Consumes: `IRecordEditLockService` (Task 6), `RecordLastWriterQuery` — pro jméno držitele použij `BuildDisplayName` z `ProjectService.RecordComposition.cs:182-195`, ne vlastní formátování.
- Produces: `GET /Zaznamy/Edit/{id}` vrací u cizího zámku HTTP 200 s partialem `_RecordEditLocked` (ne redirect — Api test tak ověří obsah přímo).

- [ ] **Krok 1: Napiš failující Api test**

```csharp
    [Fact]
    public async Task Edit_ShouldRenderLockedPage_WhenAnotherUserHoldsLock()
    {
        // arrange: uživatel A drží zámek (přímý zápis do zaznam_edit_zamek přes fixture)
        var response = await clientAsUserB.GetAsync($"/Zaznamy/Edit/{recordId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("data-record-edit-locked=\"true\"");
        html.Should().NotContain("data-record-editor-form=\"true\"", "editor se nesmí vůbec vyrenderovat");
    }
```

Assertuj **jen na atributy** — český text projde Razor encodingem na entity.

- [ ] **Krok 2: Spusť test — musí selhat**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordEditLockControllerTests"`
Expected: FAIL — vrátí se plný editor.

- [ ] **Krok 3: Vytvoř view**

`PmTracker.Web/Views/Projekty/_RecordEditLocked.cshtml`:

```html
@model PmTracker.Web.Models.ViewModels.Projekty.RecordEditLockedViewModel
<div class="record-edit-locked" data-record-edit-locked="true">
    <gov-message color="warning">
        <div class="record-edit-locked-title">Tento záznam upravuje jiný uživatel: @Model.HolderDisplayName</div>
        <div class="record-edit-locked-detail">Úpravy začaly v @Model.SinceLocal.ToString("HH:mm"). Zkuste to prosím později.</div>
    </gov-message>
    <a class="gov-link" href="@Model.RetryUrl">Zkusit znovu</a>
</div>
```

View model přidej do `Models/ViewModels/Projekty/ZaznamEditViewModels.cs` (`HolderDisplayName`, `SinceLocal`, `RetryUrl`, `BackUrl`).

- [ ] **Krok 4: Sjednoť formátování jména (revize po Fázi 1)**

Fáze 1 hlásí autora cizího zápisu jako „Příjmení Jméno". Zámek musí mluvit stejně, jinak bude aplikace o téže osobě psát dvěma způsoby. Vytvoř `PmTracker.Web/Services/Records/PersonDisplayName.cs`:

```csharp
namespace PmTracker.Web.Services.Records;

/// <summary>
/// Spec 2026-09-17 §4.3 / §5.3 — tvar jména v hláškách o souběhu: „Příjmení Jméno".
/// Záměrně BEZ titulu: hláška má identifikovat kolegu, ne ho titulovat.
/// </summary>
public static class PersonDisplayName
{
    public static string Format(string? prijmeni, string? jmeno)
    {
        var name = $"{prijmeni} {jmeno}".Trim();
        return string.IsNullOrWhiteSpace(name) ? "neznámý uživatel" : name;
    }
}

public static class PersonDisplayNameQuery
{
    public static async Task<string> ResolveAsync(
        PmTrackerDbContext dbContext, int osobaId, CancellationToken ct)
    {
        var osoba = await dbContext.Osoby.AsNoTracking()
            .Where(x => x.Id == osobaId)
            .Select(x => new { x.Jmeno, x.Prijmeni })
            .FirstOrDefaultAsync(ct);

        return osoba is null
            ? "neznámý uživatel"
            : PersonDisplayName.Format(osoba.Prijmeni, osoba.Jmeno);
    }
}
```

Potom v `RecordLastWriterQuery.ResolveAsync` nahraď vlastní skládání jména voláním `PersonDisplayName.Format(row.Prijmeni, row.Jmeno)` — jeden zdroj pravdy. Unit testy z Fáze 1 (`RecordStaleGuardTests`) musí zůstat zelené.

- [ ] **Krok 5: Zapoj bránu do akce `Edit`**

`ZaznamyController` má klasický konstruktor (`ZaznamyController.cs:32-46`) — přidej parametr `IRecordEditLockService editLockService` a pole `_editLockService`. Pak do `Edit` hned za `RecordEditorAffordancePolicy.CanOpenEditor` check (tedy po autorizaci, ale **před** `GetEditModelAsync` i před harvest trigger):

```csharp
        var lockResult = await _editLockService.TryAcquireAsync(id, CurrentUserContext.OsobaId, ct);
        if (!lockResult.Acquired)
        {
            var holderName = await PersonDisplayNameQuery.ResolveAsync(_db, lockResult.HolderOsobaId!.Value, ct);
            return View("~/Views/Projekty/_RecordEditLocked.cshtml", new RecordEditLockedViewModel
            {
                HolderDisplayName = holderName,
                SinceLocal = (lockResult.HolderSinceUtc ?? DateTime.UtcNow).ToLocalTime(),
                RetryUrl = Url.Action("Edit", "Zaznamy", new { id }) ?? $"/Zaznamy/Edit/{id}",
                BackUrl = Url.Action("Detail", "Zaznamy", new { id }) ?? $"/Zaznamy/Detail/{id}"
            });
        }
```

- [ ] **Krok 6: Uvolni zámek po úspěšném uložení**

`ZaznamyController.Commands.cs` v akci `Save` — do `operation` lambdy za `SaveRecordAsync`:

```csharp
                if (command.Id.HasValue)
                {
                    // Best-effort: selhání release nesmí shodit už uložený záznam.
                    try
                    {
                        await _editLockService.ReleaseAsync(command.Id.Value, CurrentUserContext.OsobaId, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Release zámku záznamu {ZaznamId} selhal.", command.Id.Value);
                    }
                }
```

- [ ] **Krok 7: Spusť Api test**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordEditLockControllerTests"`
Expected: PASS.

- [ ] **Krok 8: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Api/Controllers/RecordEditLockControllerTests.cs
git commit -m "feat(zaznamy): editor nepustí druhého uživatele, ukáže kdo záznam upravuje

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Heartbeat na keep-alive a release při odchodu

**Files:**
- Modify: `PmTracker.Web/Controllers/AppController.cs:27` (`KeepAlive` přijme `zaznamId`)
- Create: `PmTracker.Web/Controllers/ZaznamyController.EditLock.cs` (endpoint `Release`)
- Create: `PmTracker.Web/wwwroot/js/modules/recordEditor/editLock.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/session.js` (předání `zaznamId` do keep-alive URL)
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (side-effect import)
- Test: `PmTracker.Tests.Unit/Architecture/RecordEditLockClientTests.cs` (nový)

**Interfaces:**
- Consumes: `IRecordEditLockService.HeartbeatAsync` / `ReleaseAsync` (Task 6).
- Produces: `GET /App/KeepAlive?zaznamId=123` obnoví zámek; `POST /Zaznamy/EditLock/Release` (tělo `zaznamId`) ho uvolní.

- [ ] **Krok 1: Napiš architekturní test na zapojení modulu**

```csharp
    [Fact]
    public void Bootstrap_ImportujeEditLock()
    {
        Read("PmTracker.Web/wwwroot/js/modules/bootstrap.js")
            .Should().Contain("recordEditor/editLock",
                "bez side-effect importu se modul nenačte a zámek se nikdy neuvolní");
    }

    [Fact]
    public void EditLock_UvolnujeZamekBeaconem()
    {
        Read("PmTracker.Web/wwwroot/js/modules/recordEditor/editLock.js")
            .Should().Contain("sendBeacon",
                "release při zavření tabu musí jít beaconem — fetch se při pagehide nedoručí");
    }
```

- [ ] **Krok 2: Spusť test — musí selhat**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordEditLockClientTests"`
Expected: FAIL — soubor `editLock.js` neexistuje.

- [ ] **Krok 3: Rozšiř `KeepAlive` o zámek**

`AppController.KeepAlive` — za úspěšnou resoluci uživatele, před `GetAndStoreTokens`:

```csharp
        // Spec 2026-09-17 §4.2 — heartbeat zámku jede na existujícím keep-alive,
        // aby editor nepotřeboval vlastní časovač.
        if (zaznamId is > 0)
        {
            await _editLockService.HeartbeatAsync(zaznamId.Value, resolution.UserContext.OsobaId, ct);
        }
```

Signatura: `public async Task<IActionResult> KeepAlive(int? zaznamId, CancellationToken ct)`.

- [ ] **Krok 4: Přidej release endpoint**

`PmTracker.Web/Controllers/ZaznamyController.EditLock.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace PmTracker.Web.Controllers;

public sealed partial class ZaznamyController
{
    public sealed record ReleaseEditLockRequest(int ZaznamId);

    [HttpPost("Zaznamy/EditLock/Release")]
    [IgnoreAntiforgeryToken] // sendBeacon z pagehide token nepřipojí; operace jen maže VLASTNÍ zámek
    public async Task<IActionResult> ReleaseEditLock([FromBody] ReleaseEditLockRequest request, CancellationToken ct)
    {
        if (request.ZaznamId <= 0)
        {
            return BadRequest();
        }

        await _editLockService.ReleaseAsync(request.ZaznamId, CurrentUserContext.OsobaId, ct);
        return Ok();
    }
}
```

- [ ] **Krok 5: Napiš klientský modul**

`PmTracker.Web/wwwroot/js/modules/recordEditor/editLock.js`:

```javascript
/**
 * Spec 2026-09-17 §4.2 — uvolnění zámku karty při odchodu z editoru.
 * Heartbeat řeší keep-alive (session.js), tady jen release.
 */
const releaseEndpoint = "/Zaznamy/EditLock/Release";

function resolveRecordId() {
    const form = document.querySelector('form[data-record-editor-form="true"]');
    if (!(form instanceof HTMLFormElement)) {
        return 0;
    }
    const idInput = form.querySelector('input[name="Id"]');
    const value = idInput instanceof HTMLInputElement ? Number.parseInt(idInput.value, 10) : 0;
    return Number.isFinite(value) && value > 0 ? value : 0;
}

export function initRecordEditLock() {
    const zaznamId = resolveRecordId();
    if (!zaznamId || document.body.dataset.recordEditLockReady === "true") {
        return;
    }

    document.body.dataset.recordEditLockReady = "true";
    document.documentElement.dataset.recordEditLockId = String(zaznamId);

    window.addEventListener("pagehide", () => {
        const payload = JSON.stringify({ zaznamId });
        navigator.sendBeacon(releaseEndpoint, new Blob([payload], { type: "application/json" }));
    });
}

initRecordEditLock();
```

`session.js` — v `performKeepAliveRequest` doplň `zaznamId` do URL:

```javascript
        const lockId = document.documentElement.dataset.recordEditLockId || "";
        const url = lockId ? `${keepAliveEndpointPath}?zaznamId=${encodeURIComponent(lockId)}` : keepAliveEndpointPath;
        const response = await fetch(url, {
```

`bootstrap.js` — přidej side-effect import k ostatním:

```javascript
import "./recordEditor/editLock.js";
```

- [ ] **Krok 6: Spusť testy a build**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordEditLockClientTests" && dotnet build PmTracker.sln`
Expected: PASS, build 0 chyb.

- [ ] **Krok 7: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Unit/Architecture/RecordEditLockClientTests.cs
git commit -m "feat(zaznamy): heartbeat zámku na keep-alive + release beaconem

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: E2E dva uživatelé, dokumentace, balíček

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/RecordEditLockScenariosTests.cs`
- Modify: `docs/technical/` (stránka o souběžné editaci), `docs/user-guide.md`, `CHANGELOG.md`

- [ ] **Krok 1: Napiš E2E scénář**

```csharp
    [Fact]
    public async Task DruhyUzivatel_NedostaneSeDoEditoru()
    {
        await LoginAsAsync(UserA);
        await Page.GotoAsync($"/Zaznamy/Edit/{RecordId}");
        await Expect(Page.Locator("form[data-record-editor-form='true']")).ToHaveCountAsync(1);

        await LoginAsAsync(UserB);
        await Page.GotoAsync($"/Zaznamy/Edit/{RecordId}");

        await Expect(Page.Locator("[data-record-edit-locked='true']")).ToHaveCountAsync(1);
        await Expect(Page.Locator("form[data-record-editor-form='true']")).ToHaveCountAsync(0);
    }
```

Pravidla pro gov komponenty: viditelnost přes `ToHaveCountAsync`, klikání `DispatchEventAsync("click")`, čekání na třídu `hydrated`.

- [ ] **Krok 2: Spusť E2E**

Run: `dotnet test PmTracker.Tests.E2E/PmTracker.Tests.E2E.csproj --filter "FullyQualifiedName~RecordEditLockScenariosTests"`
Expected: PASS.

- [ ] **Krok 3: Dopiš dokumentaci a CHANGELOG**

Do `CHANGELOG.md` jeden odstavec: zrušení ScheduleVersion, record guard se jménem, oprava ručního režimu, zámek karty + **povinný krok operátora: spustit `db_upgrade_1_4_5_record_edit_lock.sql`**.

- [ ] **Krok 4: Postav balíček**

```bash
dotnet publish PmTracker.Web -c Release -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
```

- [ ] **Krok 5: Commit**

```bash
git add PmTracker.Tests.E2E CHANGELOG.md docs
git commit -m "docs(zaznamy): souběžná editace — dokumentace, changelog, E2E scénář

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Kontrola pokrytí spec

| Sekce spec | Task |
|---|---|
| §4.1 datový model zámku | 5 |
| §4.2 životní cyklus (acquire / heartbeat / release / TTL) | 6, 7, 8 |
| §4.3 hláška pro blokovaného uživatele | 7 |
| §4.4 rozsah (jen editační cesta) | 7 |
| §5.1 pravidlo o verzi záznamu | 2 (Task 3 zrušen — audit posouvá verzi sám) |
| §5.2 mechanismus guardu (audit id) | 2 |
| §5.3 hláška se jménem z auditu | 2 |
| §5.4 guard test na `refreshScope` | 2 (krok 10) |
| §6.1 smazání `ScheduleVersion` | 1 |
| §6.2 oprava ručního režimu | 4 |
| §7 testovací strategie | všechny tasky |
| §8.5 kalibrace odhadu | ✅ zapsáno 2026-09-17 (1,05 h proti odhadu 2,75 h) |
