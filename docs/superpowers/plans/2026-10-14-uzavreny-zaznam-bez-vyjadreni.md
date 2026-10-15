# Uzavřený záznam bez nových vyjádření — plán implementace

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: použij superpowers:executing-plans (inline, po úkolech). Kroky používají checkboxy (`- [ ]`). Commity dělej až při exekuci plánu (na výslovný pokyn uživatele zvolit exekuci), do lokálního `main`, bez pushe.

**Cíl:** Záznam ve stavu s příznakem `ciselnik_stavu_ukolu.is_final = 1` nepřijme nové vyjádření: server uložení odmítne srozumitelnou chybou a UI nezobrazí editor vyjádření ani výběr jednání k navázání.

**Architektura:** Jedno sdílené pravidlo `TaskStatusRules.IsClosedForComments(state)` (holé `IsFinal`, bez výjimky pro pozastavené). Server ho vynucuje na obou místech, kde vzniká nové vyjádření (`AddCommentAsync`, `SaveMeetingNotesBatchAsync` v `CommentService`). UI ho vyhodnocuje jednou při sestavení panelu (`ProjectService.BuildRecordCommentsPanelAsync` → `ZaznamCommentsPanelViewModel.IsRecordClosed`), kontroler podle něj vypne `CanAddComment` a partial místo formuláře ukáže poznámku. Zákaz úprav a mazání vyjádření u uzavřeného **jednání** už existuje (`EnsureMeetingAllowsCommentChangesAsync`, `IsMeetingReadOnly`); plán ho jen pinuje regresními testy.

**Tech Stack:** ASP.NET Core MVC + Razor, EF Core, SQL Server; xUnit + FluentAssertions; testy Unit / Integration (`SqlIntegrationFixture`) / Api (`ApiSqlFixture`, `WebApplicationFactory`).

**Spec:** žádný samostatný design dokument — zadání je v konverzaci 2026-10-14 (analýza + požadavky uživatele). Související, nezávislý: `docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md` (mění jen formulář změny stavu, na tento plán nemá vliv).

## Global Constraints

- „Uzavřený záznam" = `ciselnik_stavu_ukolu.is_final = 1`. **Jen** `IsFinal`. Pozastavené záznamy se dál dopisují (pozastavený stav má `is_final = 0`). Bez výjimky podle názvu stavu.
- Blokuje se jen **přidání** nového vyjádření. Čtení existujících vyjádření a jejich tisk/export se nemění.
- Zákaz úprav a mazání vyjádření vázaných na uzavřené jednání platí už dnes (`MeetingStatePolicy.IsReadOnly`: kód `CLOSED`, název obsahuje „uzav", nebo `UzamklOsobaId`). Nic se tu nemění, jen testy.
- Chybové hlášky musí být srozumitelné operátorovi bez placeholderů (`<id>` apod.) a říct, co udělat (znovu otevřít záznam změnou stavu).
- Konstruktor `CommentService` se nemění (DI zůstává).
- Statické UI testy kotvit na atributy / ASCII, ne na český text (Razor kóduje diakritiku na entity).
- Žádná změna DB schématu ani `db_upgrade_*.sql`.
- Testy Unit i Api jsou zelené (nové selhání = regrese). E2E: porovnávat jména proti baseline worktree (33 pre-existing selhání), ne počtem.

## Review Focus

1. **Dávkový zápis z jednání s jedním uzavřeným záznamem mezi několika** — očekává se all-or-nothing: nic se neuloží, chyba jmenuje uzavřený záznam (Task 2, test `SaveMeetingNotesBatch_ClosedRecordAmongOpen_SavesNothing`).
2. **Záznam s `StavUkoluId = NULL`** — nesmí se považovat za uzavřený ani spadnout (Task 1 test `IsClosedForComments_IsFalseForUnknownState`; Task 2 helper přijímá `int?`).
3. **Uživatel s právem, ale záznam uzavřen** — dostane nejdřív zprávu o oprávnění, pokud ho nemá; zprávu o uzavření až když oprávnění má (pořadí kontrol, Task 2 test `AddComment_ClosedRecord_UserWithoutPermissionGetsPermissionError`).
4. **Stránka záznamu (celostránkový render) i částečný render panelu** — oba musí skrýt editor; jdou přes stejný `BuildRecordCommentsPanelAsync` + `PrepareRecordCommentsPresentationAsync` (Task 3 grep + Api test).
5. **Existující vyjádření uzavřeného záznamu se dál zobrazují** a u otevřeného jednání jdou upravit; uzavřený záznam nesmí zablokovat úpravu (Task 3 test `ClosedRecord_ExistingCommentStillRenderedAndEditableInOpenMeeting`).

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Services/Common/TaskStatusRules.cs` | nové pravidlo `IsClosedForComments` |
| `PmTracker.Web/Services/CommentService.cs` | guard v `AddCommentAsync` a `SaveMeetingNotesBatchAsync`, privátní helper |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` | `ZaznamCommentsPanelViewModel.IsRecordClosed` |
| `PmTracker.Web/Services/ProjectService.LazyQueries.cs` | `BuildRecordCommentsPanelAsync` vyplní `IsRecordClosed` |
| `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` | `PrepareRecordCommentsPresentationAsync` vypne `CanAddComment` |
| `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml` | poznámka místo formuláře |
| `PmTracker.Tests.Unit/Common/TaskStatusRulesTests.cs` | testy pravidla |
| `PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestDataStore.cs` | wrapper `SaveMeetingNotesBatch` |
| `PmTracker.Tests.Integration/DataStore/ClosedRecordCommentsDataStoreTests.cs` | nový soubor, serverové testy |
| `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` | helpery `CloseRecordAsync`, `AddCommentAsync` |
| `PmTracker.Tests.Api/Controllers/ClosedRecordCommentsRenderTests.cs` | nový soubor, render testy |

**Mimo rozsah (vědomě):**
- Sekce vyjádření v `Views/Jednani/_TaskItemPartial.cshtml` (`JednaniController.TaskItemPartial`): na detailu jednání se nerenderuje od 2026-07-08. Serverový guard v `SaveMeetingNotesBatchAsync` (Task 2) tu cestu pokrývá, i kdyby se sekce vrátila.
- Uzamčení polí samotného záznamu ve stavu `IsFinal` (editace záznamu zůstává, jak je — uživatel řešil jen vyjádření).
- Změna číselníku stavů, `TaskStatusRules.IsCompleted` (podbarvení tisku) — beze změny.

---

### Task 1: Pravidlo `IsClosedForComments`

**Files:**
- Modify: `PmTracker.Web/Services/Common/TaskStatusRules.cs`
- Test: `PmTracker.Tests.Unit/Common/TaskStatusRulesTests.cs`

**Interfaces:**
- Produces: `public static bool TaskStatusRules.IsClosedForComments(CiselnikStavuUkoluEntity? state)` — `true` právě když `state is not null && state.IsFinal`. Používají Task 2 (server) a Task 3 (UI).

- [ ] **Step 1: Napsat padající testy**

Do `TaskStatusRulesTests` (za existující `IsCompleted_IsFalseForUnknownState`) přidat:

```csharp
    [Fact]
    public void IsClosedForComments_IsTrueForFinalState()
        => TaskStatusRules
            .IsClosedForComments(new CiselnikStavuUkoluEntity { Nazev = "Ukončeno", IsFinal = true })
            .Should().BeTrue();

    [Fact]
    public void IsClosedForComments_IsFalseForNonFinalState()
        => TaskStatusRules
            .IsClosedForComments(new CiselnikStavuUkoluEntity { Nazev = "Rozpracováno", IsFinal = false })
            .Should().BeFalse();

    [Fact]
    public void IsClosedForComments_IsFalseForPausedNonFinalState()
        => TaskStatusRules
            .IsClosedForComments(new CiselnikStavuUkoluEntity { Nazev = "Pozastaveno", IsFinal = false })
            .Should().BeFalse("pozastavené záznamy se dál dopisují (rozhodnutí uživatele 2026-10-14)");

    [Fact]
    public void IsClosedForComments_FollowsIsFinalOnlyEvenForPausedName()
        => TaskStatusRules
            .IsClosedForComments(new CiselnikStavuUkoluEntity { Nazev = "Pozastaveno", IsFinal = true })
            .Should().BeTrue("pro vyjádření platí holé is_final, bez výjimky podle názvu (na rozdíl od IsCompleted)");

    [Fact]
    public void IsClosedForComments_IsFalseForUnknownState()
        => TaskStatusRules.IsClosedForComments(null).Should().BeFalse();
```

- [ ] **Step 2: Ověřit, že selhávají**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TaskStatusRulesTests.IsClosedForComments" --nologo`
Expected: build FAIL — `TaskStatusRules` neobsahuje `IsClosedForComments`.

- [ ] **Step 3: Implementace**

Do `TaskStatusRules` (na konec třídy) přidat:

```csharp
    /// <summary>
    /// Záznam v koncovém stavu (is_final) nepřijímá nová vyjádření — transparentní historie,
    /// k doplnění se musí znovu otevřít. Na rozdíl od <see cref="IsCompleted"/> platí holé
    /// is_final: pozastavení se tu neřeší (rozhodnutí 2026-10-14, pozastavené se dopisují).
    /// </summary>
    public static bool IsClosedForComments(CiselnikStavuUkoluEntity? state)
        => state is { IsFinal: true };
```

- [ ] **Step 4: Ověřit, že procházejí**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TaskStatusRulesTests" --nologo`
Expected: PASS (všechny testy třídy, nové i staré).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Common/TaskStatusRules.cs PmTracker.Tests.Unit/Common/TaskStatusRulesTests.cs
git commit -m "feat(vyjadreni): pravidlo IsClosedForComments (holé is_final)"
```

---

### Task 2: Server odmítne vyjádření k uzavřenému záznamu

**Files:**
- Modify: `PmTracker.Web/Services/CommentService.cs` (`AddCommentAsync` ~ř. 21–73, `SaveMeetingNotesBatchAsync` ~ř. 185–260, nový privátní helper u `NormalizeCommentText`)
- Modify: `PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestDataStore.cs` (wrapper za `AddComment`, ř. ~133)
- Create: `PmTracker.Tests.Integration/DataStore/ClosedRecordCommentsDataStoreTests.cs`

**Interfaces:**
- Consumes: `TaskStatusRules.IsClosedForComments(CiselnikStavuUkoluEntity?)` z Task 1.
- Produces: `InvalidOperationException` se zprávami
  - `AddCommentAsync`: `Záznam {ZaznamId} je uzavřený (stav „{název}“), vyjádření nelze přidat. Nejprve ho znovu otevřete změnou stavu.`
  - `SaveMeetingNotesBatchAsync`: `Záznam {ZaznamId} je uzavřený (stav „{název}“), poznámku nelze zapsat. Nejprve ho znovu otevřete změnou stavu.`
  - Kontrolery tyto výjimky už zobrazují (existující zpracování `InvalidOperationException` v `ZaznamyController.AddComment` a `JednaniController` — nic dalšího se neupravuje).
- Produces (test infra): `IntegrationTestDataStore.SaveMeetingNotesBatch(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser)`.

- [ ] **Step 1: Wrapper pro dávkový zápis v test infrastruktuře**

V `IntegrationTestDataStore.cs` za metodu `AddComment` přidat:

```csharp
    public void SaveMeetingNotesBatch(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser)
        => services.GetRequiredService<IMeetingService>().SaveMeetingNotesBatchAsync(meetingId, rows, currentUser).GetAwaiter().GetResult();
```

(`IMeetingService` je ve stejném namespace jako `IRecordService`, který soubor už používá — pokud se nezkompiluje, doplnit chybějící `using PmTracker.Web.Services;`.)

- [ ] **Step 2: Napsat padající integrační testy**

Vytvořit `PmTracker.Tests.Integration/DataStore/ClosedRecordCommentsDataStoreTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Záznam ve stavu is_final nepřijímá nová vyjádření (2026-10-14). Pozastavené (is_final = 0)
/// se dál dopisují. Úpravy a mazání vyjádření u uzavřeného jednání zůstávají zakázané.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ClosedRecordCommentsDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ClosedRecordCommentsDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int OwnerId, int SubsystemId, int OpenRecordId, int ClosedRecordId, int MeetingId, CurrentUserContextViewModel Pm);

    private static async Task<Scenario> SeedAsync(PmTracker.Web.Data.PmTrackerDbContext dbContext, string marker, int meetingNumber)
    {
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Owner");
        var pmId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Pm");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker.ToUpperInvariant());
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker}S", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        var openRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", $"{marker}Open");
        var closedRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", $"{marker}Closed");
        await SetRecordStateAsync(dbContext, closedRecordId, isFinal: true);
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber);

        var pm = IntegrationTestHelper.BuildUser(
            pmId,
            isSuperAdmin: false,
            grants: new[]
            {
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.CommentsAdd, projectId),
                IntegrationTestHelper.AllowProjectPermission(PermissionKeys.MeetingsNotesEdit, projectId)
            });
        return new Scenario(projectId, ownerId, subsystemId, openRecordId, closedRecordId, meetingId, pm);
    }

    private static async Task SetRecordStateAsync(PmTracker.Web.Data.PmTrackerDbContext dbContext, int recordId, bool isFinal)
    {
        var stateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal == isFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
        record.StavUkoluId = stateId;
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task AddComment_OpenRecord_Passes()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_open_ok");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsOk", 9601);

        store.AddComment(new AddCommentCommand { ZaznamId = s.OpenRecordId, JednaniId = s.MeetingId, Text = "Do otevřeného." }, s.Pm);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.ZaznamId == s.OpenRecordId)).Should().BeTrue();
    }

    [Fact]
    public async Task AddComment_ClosedRecord_IsRejectedWithClearMessage()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_rejected");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsRej", 9611);

        var act = () => store.AddComment(new AddCommentCommand { ZaznamId = s.ClosedRecordId, JednaniId = s.MeetingId, Text = "Do uzavřeného." }, s.Pm);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*uzavřený*vyjádření nelze přidat*znovu otevřete*");
        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.ZaznamId == s.ClosedRecordId)).Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_ReopenedRecord_Passes()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_reopen");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsRe", 9621);
        await SetRecordStateAsync(dbContext, s.ClosedRecordId, isFinal: false);

        store.AddComment(new AddCommentCommand { ZaznamId = s.ClosedRecordId, JednaniId = s.MeetingId, Text = "Po znovuotevření." }, s.Pm);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.ZaznamId == s.ClosedRecordId)).Should().BeTrue();
    }

    [Fact]
    public async Task AddComment_ClosedRecord_UserWithoutPermissionGetsPermissionError()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_noperm");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsNp", 9631);
        var outsiderId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ClsNpOutsider");
        var outsider = IntegrationTestHelper.BuildUser(outsiderId, isSuperAdmin: false, grants: Array.Empty<PermissionGrantViewModel>());

        var act = () => store.AddComment(new AddCommentCommand { ZaznamId = s.ClosedRecordId, JednaniId = s.MeetingId, Text = "Cizí." }, outsider);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Nemáte oprávnění*");
    }

    [Fact]
    public async Task SaveMeetingNotesBatch_ClosedRecordAmongOpen_SavesNothing()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_batch");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsBt", 9641);

        var act = () => store.SaveMeetingNotesBatch(
            s.MeetingId,
            new[] { (s.OpenRecordId, "Poznámka A"), (s.ClosedRecordId, "Poznámka B") },
            s.Pm);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*Záznam {s.ClosedRecordId} je uzavřený*poznámku nelze zapsat*");
        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x => x.JednaniId == s.MeetingId))
            .Should().BeFalse("dávka je all-or-nothing");
    }

    [Fact]
    public async Task UpdateAndDeleteComment_InClosedMeeting_StayForbidden()
    {
        var db = await _fixture.CreateDatabaseAsync("closed_cmt_meeting_lock");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var s = await SeedAsync(dbContext, "ClsMl", 9651);

        store.AddComment(new AddCommentCommand { ZaznamId = s.OpenRecordId, JednaniId = s.MeetingId, Text = "Před uzavřením jednání." }, s.Pm);
        var comment = await dbContext.Vyjadreni.AsNoTracking().FirstAsync(x => x.ZaznamId == s.OpenRecordId);

        var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == s.MeetingId);
        meeting.StavJednaniId = await dbContext.CiselnikStavuJednani.Where(x => x.Kod == "CLOSED").Select(x => x.Id).FirstAsync();
        await dbContext.SaveChangesAsync();

        var update = () => store.UpdateComment(new UpdateCommentCommand { Id = comment.Id, Text = "Přepis historie." }, s.Pm);
        var delete = () => store.DeleteComment(new DeleteCommentCommand { Id = comment.Id }, s.Pm);

        update.Should().Throw<InvalidOperationException>().WithMessage("*uzavřeného jednání*nelze upravovat ani mazat*");
        delete.Should().Throw<InvalidOperationException>().WithMessage("*uzavřeného jednání*nelze upravovat ani mazat*");
        (await dbContext.Vyjadreni.AsNoTracking().FirstAsync(x => x.Id == comment.Id)).TextVyjadreni
            .Should().Contain("Před uzavřením jednání.");
    }
}
```

Poznámky pro exekutora: (a) pokud `EnsureRecordAsync` v integračním helperu vybere `!IsFinal` stav, `SetRecordStateAsync` ho přepíše explicitně, takže test nezávisí na pořadí číselníku; (b) pokud `AddComment_OpenRecord_Passes` selže na oprávnění, je chyba v sestavení grantů v `SeedAsync` (ne v produkčním kódu) — oprav granty podle `CommentCreateAuthorizationDataStoreTests`, ne test.

- [ ] **Step 3: Ověřit, že relevantní testy selhávají správně**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ClosedRecordCommentsDataStoreTests" --nologo`
Expected: `AddComment_OpenRecord_Passes`, `AddComment_ReopenedRecord_Passes`, `AddComment_ClosedRecord_UserWithoutPermissionGetsPermissionError`, `UpdateAndDeleteComment_InClosedMeeting_StayForbidden` PASS (chování už existuje); `AddComment_ClosedRecord_IsRejectedWithClearMessage` a `SaveMeetingNotesBatch_ClosedRecordAmongOpen_SavesNothing` FAIL („Expected a <InvalidOperationException> to be thrown, but no exception was thrown").

- [ ] **Step 4: Implementace guardu**

V `CommentService.cs`:

a) V `AddCommentAsync` hned za blok `if (!canAdd) { throw ... }` a před `var note = new VyjadreniEntity` vložit:

```csharp
        var closedStates = await LoadClosedTaskStatesAsync([record.StavUkoluId], ct);
        if (record.StavUkoluId is { } stateId && closedStates.TryGetValue(stateId, out var closedState))
        {
            throw new InvalidOperationException(
                $"Záznam {record.Id} je uzavřený (stav „{closedState.Nazev}“), vyjádření nelze přidat. Nejprve ho znovu otevřete změnou stavu.");
        }
```

b) V `SaveMeetingNotesBatchAsync` za načtení `leadEquivalentOsobaIdsBySubsystem` (před `var localNow`) vložit:

```csharp
        var closedStates = await LoadClosedTaskStatesAsync(
            recordsById.Values.Select(x => x.StavUkoluId).ToList(), ct);
```

a v cyklu `foreach (var row in normalizedRows)` hned za blok `if (!commentAuthorizationPolicy.CanSaveMeetingNote(...)) { throw ... }` (před `notes.Add`) vložit:

```csharp
            if (record.StavUkoluId is { } recordStateId && closedStates.TryGetValue(recordStateId, out var closedState))
            {
                throw new InvalidOperationException(
                    $"Záznam {record.Id} je uzavřený (stav „{closedState.Nazev}“), poznámku nelze zapsat. Nejprve ho znovu otevřete změnou stavu.");
            }
```

Výjimka se vyhodí před `AddRange`/`SaveChangesAsync`, takže dávka je all-or-nothing.

c) Za metodu `NormalizeCommentText` přidat helper:

```csharp
    private async Task<Dictionary<int, CiselnikStavuUkoluEntity>> LoadClosedTaskStatesAsync(
        IReadOnlyCollection<int?> stateIds, CancellationToken ct)
    {
        var ids = stateIds.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var states = await dbContext.CiselnikStavuUkolu
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
        return states
            .Where(TaskStatusRules.IsClosedForComments)
            .ToDictionary(x => x.Id);
    }
```

(Volání `LoadClosedTaskStatesAsync([record.StavUkoluId], ct)` předává `int?` kolekci; `using PmTracker.Web.Services.Common;` v souboru už je.)

- [ ] **Step 5: Ověřit, že procházejí**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ClosedRecordCommentsDataStoreTests|FullyQualifiedName~CommentCreateAuthorizationDataStoreTests|FullyQualifiedName~MeetingAndCommentDataStoreTests" --nologo`
Expected: PASS (nové i existující komentářové testy — testovací záznamy mají nefinální stav, guard je nerozbije).

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/CommentService.cs PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestDataStore.cs PmTracker.Tests.Integration/DataStore/ClosedRecordCommentsDataStoreTests.cs
git commit -m "feat(vyjadreni): server odmítne vyjádření k záznamu ve stavu is_final"
```

---

### Task 3: UI neukáže editor vyjádření u uzavřeného záznamu

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` (`ZaznamCommentsPanelViewModel`, ~ř. 112–130)
- Modify: `PmTracker.Web/Services/ProjectService.LazyQueries.cs` (`BuildRecordCommentsPanelAsync`, ~ř. 288–365)
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` (`PrepareRecordCommentsPresentationAsync`, ř. 156–175)
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml` (ř. ~146–170)
- Modify: `PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs` (helpery za `CreateMeetingAsync`, ř. ~556–575)
- Create: `PmTracker.Tests.Api/Controllers/ClosedRecordCommentsRenderTests.cs`

**Interfaces:**
- Consumes: `TaskStatusRules.IsClosedForComments` z Task 1.
- Produces: `ZaznamCommentsPanelViewModel.IsRecordClosed` (`bool { get; init; }`); partial renderuje při `IsRecordClosed` element `<p class="comment-closed-note" data-record-closed-note="true">…</p>` místo formuláře `AddComment`.
- Produces (test infra): `ApiSqlFixture.CloseRecordAsync(int recordId)`, `ApiSqlFixture.AddCommentAsync(int recordId, int meetingId, int authorOsobaId, string text)`.

- [ ] **Step 1: Ověřit, že panel vzniká jen na jednom místě**

Run: `grep -rn "new ZaznamCommentsPanelViewModel" --include='*.cs' --include='*.cshtml' . | grep -v "/bin/\|/obj/"`
Expected: jediný výskyt `PmTracker.Web/Services/ProjectService.LazyQueries.cs` (ř. ~333). Pokud přibyl další, doplnit i tam `IsRecordClosed`.

- [ ] **Step 2: Helpery v Api fixture**

V `ApiSqlFixture.cs` za metodu `CreateMeetingAsync` přidat:

```csharp
    public async Task CloseRecordAsync(int recordId)
    {
        await using var dbContext = CreateDbContext();

        var finalStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
        record.StavUkoluId = finalStateId;
        await dbContext.SaveChangesAsync();
    }

    public async Task AddCommentAsync(int recordId, int meetingId, int authorOsobaId, string text)
    {
        await using var dbContext = CreateDbContext();

        dbContext.Vyjadreni.Add(new VyjadreniEntity
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            AutorOsobaId = authorOsobaId,
            TextVyjadreni = text,
            DatumVyjadreni = DateTime.Now
        });
        await dbContext.SaveChangesAsync();
    }
```

- [ ] **Step 3: Napsat padající Api render testy**

Vytvořit `PmTracker.Tests.Api/Controllers/ClosedRecordCommentsRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záznam ve stavu is_final (2026-10-14): panel vyjádření nenabídne editor ani výběr jednání,
/// jen poznámku. Existující vyjádření se dál zobrazují; u uzavřeného jednání nejdou upravit.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ClosedRecordCommentsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ClosedRecordCommentsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int PmId, int OwnerId, int SubsystemId, int OpenRecordId, int ClosedRecordId, int OpenMeetingId);

    private async Task<Scenario> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, "PROJ_MAN");
        var openRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} otevreny");
        var closedRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} uzavreny");
        await _fixture.CloseRecordAsync(closedRecordId);
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        return new Scenario(projectId, pmId, ownerId, subsystemId, openRecordId, closedRecordId, openMeetingId);
    }

    private async Task<string> CommentsHtmlAsync(int projectId, int recordId, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task OpenRecord_PmSeesForm_AndNoClosedNote()
    {
        var s = await SeedAsync("ApiCls1", 9981);

        var html = await CommentsHtmlAsync(s.ProjectId, s.OpenRecordId, s.PmId);

        html.Should().Contain("action=\"/Zaznamy/AddComment\"");
        html.Should().Contain($"<option value=\"{s.OpenMeetingId}\"");
        html.Should().NotContain("data-record-closed-note");
    }

    [Fact]
    public async Task ClosedRecord_PmSeesNoFormAndNoMeetingSelect_ButClosedNote()
    {
        var s = await SeedAsync("ApiCls2", 9983);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ClosedRecordId, s.PmId);

        html.Should().NotContain("action=\"/Zaznamy/AddComment\"");
        html.Should().NotContain("name=\"JednaniId\"");
        html.Should().Contain("data-record-closed-note=\"true\"");
    }

    [Fact]
    public async Task ClosedRecord_ExistingCommentStillRenderedAndEditableInOpenMeeting()
    {
        var s = await SeedAsync("ApiCls3", 9985);
        await _fixture.AddCommentAsync(s.ClosedRecordId, s.OpenMeetingId, s.PmId, "ApiCls3 historicke vyjadreni");

        var html = await CommentsHtmlAsync(s.ProjectId, s.ClosedRecordId, s.PmId);

        html.Should().Contain("ApiCls3 historicke vyjadreni");
        html.Should().Contain("data-comment-edit-toggle", "uzavřený záznam nezamyká úpravy; ty řídí stav jednání");
    }

    [Fact]
    public async Task CommentInClosedMeeting_HasNoEditOrDeleteControls()
    {
        var s = await SeedAsync("ApiCls4", 9987);
        await _fixture.AddCommentAsync(s.OpenRecordId, s.OpenMeetingId, s.PmId, "ApiCls4 vyjadreni");
        var closedMeetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9988);
        await _fixture.AddCommentAsync(s.OpenRecordId, closedMeetingId, s.PmId, "ApiCls4 uzavrene jednani");

        var html = await CommentsHtmlAsync(s.ProjectId, s.OpenRecordId, s.PmId);

        html.Should().Contain("ApiCls4 uzavrene jednani");
        CountOccurrences(html, "data-comment-edit-toggle").Should().Be(1, "jen vyjádření u otevřeného jednání jde upravit");
        CountOccurrences(html, "action=\"/Zaznamy/DeleteComment\"").Should().Be(1);
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
```

Poznámka: PM role `PROJ_MAN` má `comments.add` a `records.edit`. Pokud by `CommentsHtmlAsync` vracelo u PM v `OpenRecord_PmSeesForm...` formulář bez `<option>` (jednání se filtrují přes `GetMeetingIdsAllowingNewCommentAsync`), ověř, že `PROJ_MAN` v `RecordCommentsFormAuthzRenderTests.ProjektovyManazer_CiziSubsystem_NabidneObeJednani` dostává OPEN jednání — dostává, takže test je v souladu.

- [ ] **Step 4: Ověřit, že selhávají správně**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ClosedRecordCommentsRenderTests" --nologo`
Expected: `ClosedRecord_PmSeesNoFormAndNoMeetingSelect_ButClosedNote` FAIL (formulář je vidět, poznámka chybí). Ostatní tři PASS (dokumentují stávající chování, které se nesmí rozbít).

- [ ] **Step 5: Implementace — view model**

V `ZaznamCommentsPanelViewModel` přidat za `IsFullyLoaded`:

```csharp
    public bool IsRecordClosed { get; init; }
```

- [ ] **Step 6: Implementace — builder panelu**

V `ProjectService.LazyQueries.cs`, `BuildRecordCommentsPanelAsync(int projectId, int recordId, int? limit, bool loadAll, ...)`:

a) Rozšířit select záznamu:

```csharp
            .Select(x => new
            {
                x.Id,
                x.VlastnikId,
                x.StavUkoluId
            })
```

b) Za `var meetingOptions = await BuildOpenMeetingOptionsForProjectAsync(projectId, ct);` přidat:

```csharp
        var taskStates = await lookupCache.GetTaskStatesAsync(ct);
        var isRecordClosed = record.StavUkoluId is { } recordStateId
            && taskStates.TryGetValue(recordStateId, out var recordState)
            && TaskStatusRules.IsClosedForComments(recordState);
```

c) V inicializátoru `new ZaznamCommentsPanelViewModel` za `IsFullyLoaded = isFullyLoaded,` přidat `IsRecordClosed = isRecordClosed,`.

(`TaskStatusRules` je v `PmTracker.Web.Services.Common` — pokud `ProjectService.LazyQueries.cs` nemá odpovídající `using`, doplnit.)

- [ ] **Step 7: Implementace — kontroler**

V `ZaznamyController.Partials.cs`, `PrepareRecordCommentsPresentationAsync` nahradit blok od `var allowedMeetingIds` po konec metody:

```csharp
        if (model.IsRecordClosed)
        {
            // Uzavřený záznam (is_final) nepřijímá nová vyjádření: žádná nabídka jednání, žádný editor.
            model.OtevrenaJednani = Array.Empty<JednaniOptionViewModel>();
        }
        else
        {
            // Nabídka jednání = přesně ta, kde by AddComment prošel (sdílená CommentAuthorizationPolicy,
            // 2026-10-13). Dřív se tu ptalo jen na comments.add v projektu — subsystémový grant ho dává
            // vedoucímu/zástupci pro celý projekt, takže u cizího subsystému viděl editor, který server odmítl.
            var allowedMeetingIds = await _recordService.GetMeetingIdsAllowingNewCommentAsync(
                summary.Id, model.OtevrenaJednani, CurrentUserContext, ct);
            model.OtevrenaJednani = model.OtevrenaJednani
                .Where(item => allowedMeetingIds.Contains(item.Id))
                .ToList();
        }

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanEditRecord = canEditRecord;
        model.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        model.CanAddComment = !model.IsRecordClosed && model.OtevrenaJednani.Count > 0;
```

- [ ] **Step 8: Implementace — view**

V `_ZaznamCommentsPartial.cshtml` za uzavírací `}` bloku `@if (Model.CanAddComment) { ... }` (těsně před závěrečným `</div>` souboru) přidat:

```cshtml
    else if (Model.IsRecordClosed)
    {
        <p class="comment-closed-note" data-record-closed-note="true">
            Záznam je uzavřený, nové vyjádření nelze přidat. Pro doplnění ho nejprve znovu otevřete změnou stavu.
        </p>
    }
```

Tedy ze stávajícího `@if (Model.CanAddComment) { ... }` se stane `@if (...) { ... } else if (...) { ... }` — `else if` musí navazovat přímo za uzavírací `}` (Razor to vyžaduje bez prázdného textu mezi bloky; komentáře `@* *@` mezi ně nevkládat).

Styl: přidat do `PmTracker.Web/wwwroot/css/` do souboru, který definuje `.comment-form` (ověř `grep -rn "\.comment-form" PmTracker.Web/wwwroot/css`; tam, kde je to deklarováno, **ne** do jiného souboru kvůli pořadí načítání), pravidlo s tokeny:

```css
.comment-closed-note {
    margin: 0;
    padding: var(--gov-spacing-xs, 0.5rem) 0;
    color: var(--pm-text-muted);
    font-size: var(--gov-font-size-s, 0.875rem);
}
```

Nesahat na `:has` / `max-content` (Edge na i15). Před přidáním ověř grepem, že selektor `.comment-closed-note` nikde neexistuje (duplicitní pravidla přebíjejí).

- [ ] **Step 9: Ověřit, že procházejí**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ClosedRecordCommentsRenderTests|FullyQualifiedName~RecordCommentsFormAuthzRenderTests" --nologo`
Expected: PASS (nové i existující render testy formuláře).

- [ ] **Step 10: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs PmTracker.Web/Services/ProjectService.LazyQueries.cs PmTracker.Web/Controllers/ZaznamyController.Partials.cs PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml PmTracker.Web/wwwroot/css PmTracker.Tests.Api/TestInfrastructure/ApiSqlFixture.cs PmTracker.Tests.Api/Controllers/ClosedRecordCommentsRenderTests.cs
git status --short
git commit -m "feat(vyjadreni): UI neukáže editor vyjádření u záznamu ve stavu is_final"
```

(`git status --short` před commitem: zkontrolovat, že v indexu je jen výše uvedené a ne rozpracovaný spec `docs/superpowers/specs/2026-10-13-zmena-stavu-k-jednani-design.md`.)

---

### Task 4: Regrese, ověření v prohlížeči, dokončení

**Files:** žádné nové (jen ověření; případné nálezy opravit v příslušném úkolu).

- [ ] **Step 1: Celé Unit a Api sady**

Run: `dotnet test PmTracker.Tests.Unit --nologo` a `dotnet test PmTracker.Tests.Api --nologo`
Expected: obojí zelené (baseline od 2026-10-08 je zelená; jakékoli selhání = regrese způsobená touto změnou).

- [ ] **Step 2: Integrační sada**

Run: `dotnet test PmTracker.Tests.Integration --nologo`
Expected: zelená. Selhání v existujících testech, které vytvářejí záznam ve finálním stavu a pak k němu přidávají vyjádření, je skutečný dopad nového pravidla — opravit test tak, aby použil nefinální stav (nebo záznam znovu otevřel), ne guard.

- [ ] **Step 3: E2E porovnání proti baseline**

Spustit `dotnet test PmTracker.Tests.E2E --nologo` a porovnat **jména** selhání proti baseline worktree (33 pre-existing selhání; jména normalizovat Pythonem). Očekávání: žádné nové jméno. Výstup shrnout česky.

- [ ] **Step 4: Ruční ověření v reálném prohlížeči (Playwright, lokální app)**

Spustit lokální aplikaci s env connection stringem lokální dev DB. V dev DB mít záznam ve stavu `is_final = 1` (např. Ukončeno) a záznam v běžném stavu, oba v projektu s otevřeným jednáním. Projít jako administrátor projektu:
1. Karta uzavřeného záznamu → panel vyjádření: **není** textarea ani select „Vazba na jednání", je poznámka „Záznam je uzavřený…"; existující vyjádření vidět.
2. Totéž na celostránkovém detailu záznamu (`/Zaznamy/Detail/{id}`).
3. Záznam v běžném stavu: editor i výběr jednání jsou, uložení vyjádření funguje.
4. Pozastavený záznam (`is_final = 0`): editor je a dá se dopsat.
5. Změnit uzavřený záznam zpět na rozpracovaný → po znovunačtení panelu je editor zpět.
6. Vyjádření u uzavřeného jednání: tlačítka Upravit/Smazat chybí.
Pořídit snímky; v dark mode zkontrolovat čitelnost poznámky (barva přes `--pm-text-muted`).

- [ ] **Step 5: Obejít UI a zkusit server přímo (negativní test ručně)**

V dev toolu / `curl` s `asUser` odeslat `POST /Zaznamy/AddComment` na uzavřený záznam. Expected: odpověď s chybou „Záznam … je uzavřený …", ve `Vyjadreni` žádný nový řádek.

- [ ] **Step 6: Dokončení**

`git status` (čistý strom kromě cizího untracked specu), `git log --oneline -4` ukáže 3 commity z Tasků 1–3. Hotové a otestované je v lokálním `main`; **bez pushe** (push jen na pokyn).

---

## Self-review

- **Pokrytí zadání:** server odmítne uložení s chybou → Task 2; UI neukáže editor ani výběr jednání → Task 3; jen `is_final`, pozastavené se dopisují → pravidlo + testy v Task 1, Review Focus; zákaz úprav/mazání u uzavřeného jednání → už implementováno, pinováno testy v Task 2 (server) a Task 3 (UI).
- **Placeholdery:** žádné; všechny kroky mají kód a příkazy. Jediná podmíněná místa (using, grant oprava) jsou verifikační poznámky s pokynem, co dělat.
- **Konzistence typů:** `IsClosedForComments(CiselnikStavuUkoluEntity?)` (Task 1) použito v Task 2 (`Where(TaskStatusRules.IsClosedForComments)`) i Task 3; `IsRecordClosed` definováno v Task 3 Step 5 a použito v Step 6–8 i v testech přes atribut `data-record-closed-note`; `ApiSqlFixture.CloseRecordAsync/AddCommentAsync` definováno v Step 2 a použito v Step 3; `StavUkoluId` je `int?` — helper `LoadClosedTaskStatesAsync(IReadOnlyCollection<int?>)` i guardy to respektují.
- **Review Focus:** každý bod má test (1: batch test; 2: unit test null; 3: no-permission test; 4: grep v Task 3 Step 1 + Api test přes `RecordCommentsPartial` — celostránkový render používá stejný builder, ověřeno ručně v Task 4 Step 4.2; 5: render test historického vyjádření).
