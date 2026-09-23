# Podbarvení ukončených úkolů v tisku — implementační plán

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: `superpowers:executing-plans` (inline exekuce). Kroky používají `- [ ]` pro sledování postupu.

**Cíl:** Ukončený úkol je v tisku jemně modře podbarvený — v PDF i ve Wordu — a pozastavený stav nemůže nikdy platit za ukončený.

**Architektura:** Jedna sdílená funkce rozhoduje, co je pozastavení a co ukončený stav. Evaluátor viditelnosti dostane druhou metodu, která zpřístupní už počítaný stav k datu jednání. Model tisku dostane příznak `IsCompleted`, který čtou obě výstupní cesty — HTML šablona třídou, Word výplní buněk.

**Tech stack:** .NET 8, ASP.NET Core MVC, EF Core, OpenXML, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md`

## Globální omezení

- **Commity se NEDĚLAJÍ.** Uživatel commituje sám po ručním ověření. Místo kroku „commit" je vždy **checkpoint**.
- Barvy doslova: ukončený `#EFF6FF` (HTML) / `EFF6FF` (Word); pozastavený `#fdf4e8` / `FDF4E8` beze změny; vyjádření `#2563EB` beze změny.
- **Pozastavení má přednost před ukončením** (spec §4.3) — v CSS i ve Wordu.
- V tisku jednání se ukončenost bere **ke dni jednání**; v tisku projektu a záznamu z **dnešního** stavu (spec §4.2).
- Test na pozastavení zůstává doslova `Contains("pozastav", StringComparison.CurrentCultureIgnoreCase)` — chování se nesmí změnit.
- Sloupec „Stav" ani `IsPaused` se **nepřevádí** na stav k datu jednání (spec §9, rozhodnutí U6).
- Texty v kódu, komentářích i logu česky. Komentáře odkazují na spec datem `2026-09-05`.

## Struktura souborů

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Vytvořit | `PmTracker.Web/Services/Common/TaskStatusRules.cs` | jediná definice pozastavení a ukončeného stavu |
| Změnit | `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` | metoda evaluátoru, naplnění `IsCompleted`, `IsPaused` přes sdílenou funkci |
| Změnit | `PmTracker.Web/Models/ViewModels/PdfExportViewModels.cs` | `bool IsCompleted` |
| Změnit | `PmTracker.Web/Views/Export/_PdfRecordRow.cshtml` | třída `completed` na řádku |
| Změnit | `PmTracker.Web/wwwroot/css/pdf-export.css` | pravidlo podbarvení **před** pravidlem pozastavených |
| Změnit | `PmTracker.Web/Services/Export/OpenXmlWordExportService.cs` | konstanta `CompletedRecordFillHex` |
| Změnit | `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs` | volba výplně s předností pozastavení |
| Změnit | `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs` | varování při rozporném stavu |

---

### Task 1: Sdílená pravidla stavu úkolu

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Common/TaskStatusRules.cs`
- Změnit: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs:896-898`
- Test: `PmTracker.Tests.Unit/Common/TaskStatusRulesTests.cs`

**Rozhraní:**
- Poskytuje dál: `TaskStatusRules.IsPausedName(string? nazev) → bool`; `TaskStatusRules.IsCompleted(CiselnikStavuUkoluEntity? state) → bool`

- [ ] **Krok 1: Napsat padající test**

`PmTracker.Tests.Unit/Common/TaskStatusRulesTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>
/// Podbarvení ukončených úkolů (2026-09-05): číselník stavů úkolů se udržuje jen
/// v databázi, proto je rozhodování o pozastavení a ukončenosti na jednom místě.
/// </summary>
public sealed class TaskStatusRulesTests
{
    [Theory]
    [InlineData("Pozastaveno", true)]
    [InlineData("pozastaveno", true)]
    [InlineData("POZASTAVENO", true)]
    [InlineData("Dočasně pozastavený úkol", true)]
    [InlineData("Rozpracováno", false)]
    [InlineData("Ukončeno", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPausedName_DetectsPauseByName(string? nazev, bool expected)
        => TaskStatusRules.IsPausedName(nazev).Should().Be(expected);

    [Fact]
    public void IsCompleted_IsTrueForFinalState()
        => TaskStatusRules
            .IsCompleted(new CiselnikStavuUkoluEntity { Nazev = "Ukončeno", IsFinal = true })
            .Should().BeTrue();

    [Fact]
    public void IsCompleted_IsFalseForRunningState()
        => TaskStatusRules
            .IsCompleted(new CiselnikStavuUkoluEntity { Nazev = "Rozpracováno", IsFinal = false })
            .Should().BeFalse();

    [Fact]
    public void IsCompleted_IsFalseWhenPausedStateIsMarkedFinal()
        => TaskStatusRules
            .IsCompleted(new CiselnikStavuUkoluEntity { Nazev = "Pozastaveno", IsFinal = true })
            .Should().BeFalse(
                "rozhodnutí U1 — pozastavený stav nesmí platit za ukončený ani při rozporu v databázi");

    [Fact]
    public void IsCompleted_IsFalseForUnknownState()
        => TaskStatusRules.IsCompleted(null).Should().BeFalse();

    [Fact]
    public void Export_UsesSharedPauseRule_NotItsOwnLiteral()
    {
        var source = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/ExportProjectionBuilders.cs"));

        source.Should().Contain("TaskStatusRules.IsPausedName",
            "export musí používat sdílené pravidlo");
        source.Should().NotContain("\"pozastav\"",
            "definice pozastavení smí být jen v TaskStatusRules, jinak se obě verze časem rozejdou");
    }
}
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TaskStatusRulesTests"`
Očekávat: chyba překladu — `TaskStatusRules` neexistuje.

- [ ] **Krok 3: Vytvořit sdílená pravidla**

`PmTracker.Web/Services/Common/TaskStatusRules.cs`:

```csharp
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Common;

/// <summary>
/// Pravidla nad číselníkem stavů úkolů. Číselník se udržuje přímo v databázi
/// (z aplikace se needituje — nabídka číselníků ho neobsahuje), proto je tohle
/// jediné místo, kde se rozhoduje, co je pozastavení a co ukončený stav.
/// Spec 2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md, §7.1.
/// </summary>
public static class TaskStatusRules
{
    /// <summary>
    /// Pozastavení se pozná podle názvu — číselník nemá vlastní příznak.
    /// Test je doslova takový, jaký byl dosud v exportu, aby se chování nezměnilo.
    /// </summary>
    public static bool IsPausedName(string? nazev)
        => !string.IsNullOrWhiteSpace(nazev)
           && nazev.Contains("pozastav", StringComparison.CurrentCultureIgnoreCase);

    /// <summary>
    /// Ukončený = koncový a zároveň ne pozastavený. Druhá podmínka je hlídání kolize
    /// (rozhodnutí U1): pozastavený stav nesmí platit za ukončený ani tehdy, když ho
    /// někdo v databázi omylem označí jako koncový.
    /// </summary>
    public static bool IsCompleted(CiselnikStavuUkoluEntity? state)
        => state is not null && state.IsFinal && !IsPausedName(state.Nazev);
}
```

- [ ] **Krok 4: Převést export na sdílené pravidlo**

V `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` nahradit blok na řádcích 896-898:

```csharp
                IsPaused = record.StavUkoluId.HasValue
                    && (taskStates.GetValueOrDefault(record.StavUkoluId.Value)?.Nazev ?? string.Empty)
                        .Contains("pozastav", StringComparison.CurrentCultureIgnoreCase),
```

za:

```csharp
                IsPaused = record.StavUkoluId.HasValue
                    && TaskStatusRules.IsPausedName(taskStates.GetValueOrDefault(record.StavUkoluId.Value)?.Nazev),
```

Do hlavičky souboru doplnit `using PmTracker.Web.Services.Common;` (pokud tam ještě není — ověřit `grep -n "using PmTracker.Web.Services.Common;" PmTracker.Web/Services/Export/ExportProjectionBuilders.cs`).

- [ ] **Krok 5: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~TaskStatusRulesTests"`
Očekávat: 13/13 prošly (8 z Theory + 5 z Fact).

- [ ] **Krok 6: Ověřit, že se nezměnilo chování pozastavených**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~OpenXmlWordExportServiceTests"`
Očekávat: zelené, včetně `BuildDocument_ShouldShadeWholePausedRecordRow` — refaktor nesmí změnit, co se považuje za pozastavené.

- [ ] **Krok 7: Checkpoint**

Shrnout: pozastavení má jednu definici, export ji používá, chování beze změny. **Necommitovat.**

---

### Task 2: Ukončenost k datu jednání

**Soubory:**
- Změnit: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs:41-49` (rozhraní), `:178-236` (implementace)
- Test: `PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs`

**Rozhraní:**
- Používá z Task 1: `TaskStatusRules.IsCompleted(CiselnikStavuUkoluEntity?)`
- Poskytuje dál: `IExportRecordVisibilityEvaluator.IsCompletedForMeetingPrint(ProjektovyZaznamEntity, IReadOnlyDictionary<int, CiselnikStavuUkoluEntity>, IReadOnlyList<ZaznamHistorieStavuZaznamuEntity>, DateTime) → bool`

Pro tento evaluátor dosud **žádný unit test neexistoval** — zpětný přepočet stavu k datu se testuje poprvé.

- [ ] **Krok 1: Napsat padající test**

`PmTracker.Tests.Unit/Export/ExportRecordVisibilityEvaluatorTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Podbarvení ukončených úkolů (2026-09-05): v tisku jednání se ukončenost bere
/// ke dni toho jednání, ne z dneška (rozhodnutí U5). Testy pracují s entitami,
/// databázi nepotřebují.
/// </summary>
public sealed class ExportRecordVisibilityEvaluatorTests
{
    private const int RunningStateId = 1;
    private const int DoneStateId = 2;
    private const int PausedButFinalStateId = 3;

    private static readonly Dictionary<int, CiselnikStavuUkoluEntity> TaskStates = new()
    {
        [RunningStateId] = new CiselnikStavuUkoluEntity
            { Id = RunningStateId, Kod = "RUN", Nazev = "Rozpracováno", IsFinal = false },
        [DoneStateId] = new CiselnikStavuUkoluEntity
            { Id = DoneStateId, Kod = "DONE", Nazev = "Ukončeno", IsFinal = true },
        // Rozporný řádek, jaký může v databázi vzniknout ručním zásahem.
        [PausedButFinalStateId] = new CiselnikStavuUkoluEntity
            { Id = PausedButFinalStateId, Kod = "PAUSE", Nazev = "Pozastaveno", IsFinal = true }
    };

    private static ProjektovyZaznamEntity Record(int currentStateId) => new()
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

    /// <summary>Úkol byl 20.8. přepnut z Rozpracováno na Ukončeno.</summary>
    private static IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> ClosedOn20August() =>
    [
        new()
        {
            Id = 1,
            ZaznamId = 100,
            PuvodniStav = RunningStateId,
            NovyStav = DoneStateId,
            DatumZmeny = new DateTime(2026, 8, 20)
        }
    ];

    [Fact]
    public void IsCompletedForMeetingPrint_IsFalse_WhenTaskWasClosedAfterTheMeeting()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(DoneStateId), TaskStates, ClosedOn20August(), new DateTime(2026, 6, 10));

        result.Should().BeFalse(
            "na červnovém zápise úkol ještě běžel — dnešní stav se do historického tisku promítnout nesmí");
    }

    [Fact]
    public void IsCompletedForMeetingPrint_IsTrue_WhenTaskWasAlreadyClosedAtTheMeeting()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(DoneStateId), TaskStates, ClosedOn20August(), new DateTime(2026, 9, 1));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsCompletedForMeetingPrint_CountsChangeOnTheMeetingDayAsAlreadyApplied()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(DoneStateId), TaskStates, ClosedOn20August(), new DateTime(2026, 8, 20));

        result.Should().BeTrue("stav se bere ke konci dne jednání");
    }

    [Fact]
    public void IsCompletedForMeetingPrint_IsFalse_ForPausedStateMarkedFinal()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();

        var result = evaluator.IsCompletedForMeetingPrint(
            Record(PausedButFinalStateId), TaskStates, [], new DateTime(2026, 9, 1));

        result.Should().BeFalse("rozhodnutí U1 — pozastavení nikdy neplatí za ukončení");
    }

    [Fact]
    public void IsCompletedForMeetingPrint_IsFalse_WhenRecordHasNoTaskState()
    {
        var evaluator = new ExportRecordVisibilityEvaluator();
        var record = Record(RunningStateId);
        record.StavUkoluId = null;

        var result = evaluator.IsCompletedForMeetingPrint(
            record, TaskStates, [], new DateTime(2026, 9, 1));

        result.Should().BeFalse("informace a rozhodnutí nemají stav úkolu, nemohou být ukončené");
    }
}
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExportRecordVisibilityEvaluatorTests"`
Očekávat: chyba překladu — `IsCompletedForMeetingPrint` neexistuje.

- [ ] **Krok 3: Rozšířit rozhraní**

V `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` do `IExportRecordVisibilityEvaluator` za stávající `IsVisibleForMeetingPrint` doplnit:

```csharp
    /// <summary>
    /// Byl úkol ukončený ke dni jednání? Podbarvení ukončených (2026-09-05), rozhodnutí U5.
    /// </summary>
    bool IsCompletedForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate);
```

- [ ] **Krok 4: Doplnit implementaci**

Do třídy `ExportRecordVisibilityEvaluator` za metodu `IsVisibleForMeetingPrint` (končí na ř. 205) vložit:

```csharp
    public bool IsCompletedForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate)
    {
        var statusAtAnchorMeeting = ResolveTaskStatusAtDate(record.StavUkoluId, statusHistory, anchorMeetingDate);
        if (!statusAtAnchorMeeting.HasValue)
        {
            return false;
        }

        // Pozor: viditelnost dál používá holé is_final (IsFinalTaskStatus), zatímco
        // podbarvení jde přes TaskStatusRules, které navíc vylučuje pozastavení.
        // Rozporný stav v databázi tedy řádek skryje, ale nepodbarví — a startovní
        // varování na něj upozorní (spec §6).
        return TaskStatusRules.IsCompleted(taskStates.GetValueOrDefault(statusAtAnchorMeeting.Value));
    }
```

- [ ] **Krok 5: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ExportRecordVisibilityEvaluatorTests"`
Očekávat: 5/5 prošly.

- [ ] **Krok 6: Checkpoint**

Shrnout: stav k datu jednání je zpřístupněný a poprvé otestovaný. **Necommitovat.**

---

### Task 3: Příznak ukončenosti v modelu tisku

**Soubory:**
- Změnit: `PmTracker.Web/Models/ViewModels/PdfExportViewModels.cs:79` (za `IsPaused`)
- Změnit: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs:777-799` (blok snímkových pravidel) a `:881` (skládání modelu)
- Test: `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` (nový test do stávající třídy)

**Rozhraní:**
- Používá z Task 1: `TaskStatusRules.IsCompleted`
- Používá z Task 2: `IExportRecordVisibilityEvaluator.IsCompletedForMeetingPrint`
- Poskytuje dál: `PdfExportRecordViewModel.IsCompleted` (bool)

- [ ] **Krok 1: Napsat padající test**

Do `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` přidat:

```csharp
    /// <summary>
    /// Podbarvení ukončených (2026-09-05), rozhodnutí U5: úkol je dnes ukončený, ale
    /// zavřel se až mezi prvním a druhým jednáním. Na starším zápise proto ukončený
    /// být nesmí, na novějším ano. Kdyby se bral dnešní stav, byly by oba true.
    /// </summary>
    [Fact]
    public async Task BuildMeetingTemplate_MarksCompletionByMeetingDate_NotByTodaysState()
    {
        var db = await _fixture.CreateDatabaseAsync("export_completed_by_meeting_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, timeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpDoneAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpDoneOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPDONE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPDONE_SYS", adminId);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "ExpDoneRecord");
        var currentUser = IntegrationTestHelper.BuildUser(
            adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var runningStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var doneStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();

        // Úkol je DNES ukončený a vznikl dávno před oběma jednáními.
        var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
        record.StavUkoluId = doneStateId;
        record.DatumZalozeni = new DateTime(2026, 1, 5);

        var earlierMeetingId = await IntegrationTestHelper.CreateMeetingAsync(
            dbContext, projectId, "OPEN", meetingNumber: 9601);
        var laterMeetingId = await IntegrationTestHelper.CreateMeetingAsync(
            dbContext, projectId, "OPEN", meetingNumber: 9602);

        // CreateMeetingAsync dává vždy dnešek — data si test nastavuje sám.
        var earlierMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == earlierMeetingId);
        earlierMeeting.DatumPlanovane = new DateTime(2026, 3, 1);
        var laterMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == laterMeetingId);
        laterMeeting.DatumPlanovane = new DateTime(2026, 4, 1);

        // Zavřel se 20.3., tedy mezi oběma jednáními.
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = runningStateId,
            NovyStav = doneStateId,
            DatumZmeny = new DateTime(2026, 3, 20)
        });
        await dbContext.SaveChangesAsync();

        var earlierModel = await useCase.BuildMeetingTemplateAsync(earlierMeetingId, currentUser, autoPrint: false);
        var laterModel = await useCase.BuildMeetingTemplateAsync(laterMeetingId, currentUser, autoPrint: false);

        earlierModel.Zaznamy.Single(item => item.ZaznamId == recordId)
            .IsCompleted.Should().BeFalse("k 1.3. úkol ještě běžel");
        laterModel.Zaznamy.Single(item => item.ZaznamId == recordId)
            .IsCompleted.Should().BeTrue("k 1.4. už byl zavřený");
    }

    /// <summary>
    /// Podbarvení ukončených (2026-09-05): mimo tisk jednání se bere dnešní stav.
    /// </summary>
    [Fact]
    public async Task BuildProjectTemplate_MarksCompletionByCurrentState()
    {
        var db = await _fixture.CreateDatabaseAsync("export_completed_project_variant");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, timeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpProjDoneAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpProjDoneOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPPRJDONE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPPRJDONE_SYS", adminId);
        var doneRecordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "ExpProjDone");
        var runningRecordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "ExpProjRunning");
        var currentUser = IntegrationTestHelper.BuildUser(
            adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var doneStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var doneRecord = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == doneRecordId);
        doneRecord.StavUkoluId = doneStateId;
        await dbContext.SaveChangesAsync();

        var model = await useCase.BuildProjectTemplateAsync(
            projectId, currentUser, autoPrint: false, filters: null);

        model.Zaznamy.Single(item => item.ZaznamId == doneRecordId).IsCompleted.Should().BeTrue();
        model.Zaznamy.Single(item => item.ZaznamId == runningRecordId).IsCompleted.Should().BeFalse();
    }
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ExportTemplateUseCaseTests"`
Očekávat: chyba překladu — `PdfExportRecordViewModel.IsCompleted` neexistuje.

Podpisy použité v testu jsou ověřené proti kódu:
`BuildProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, bool autoPrint, ProjectExportRecordFilters? filters = null, CancellationToken ct = default)`
a DbSet historie stavů se jmenuje `dbContext.ZaznamHistorieStavuZaznamu`.

- [ ] **Krok 3: Doplnit příznak do modelu**

V `PmTracker.Web/Models/ViewModels/PdfExportViewModels.cs` za řádek `public bool IsPaused { get; init; }`:

```csharp
    /// <summary>
    /// Ukončený úkol — v tisku jednání ke dni jednání, jinde podle dnešního stavu.
    /// Podbarvení ukončených (2026-09-05), spec §4.2.
    /// </summary>
    public bool IsCompleted { get; init; }
```

- [ ] **Krok 4: Naplnit příznak v exportu**

V `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` před blok `if (applyMeetingSnapshotRules && anchorMeeting is not null)` (ř. 777) vložit deklaraci:

```csharp
        // Ukončenost k datu jednání (2026-09-05, rozhodnutí U5). Mimo tisk jednání
        // zůstává null a příznak se odvodí z dnešního stavu záznamu.
        HashSet<int>? completedRecordIds = null;
```

Uvnitř toho bloku, za přiřazení `records = records.Where(...).ToList();`, doplnit:

```csharp
            completedRecordIds = records
                .Where(record => exportRecordVisibilityEvaluator.IsCompletedForMeetingPrint(
                    record,
                    taskStates,
                    statusHistoryByRecord.TryGetValue(record.Id, out var completionHistory)
                        ? completionHistory
                        : Array.Empty<ZaznamHistorieStavuZaznamuEntity>(),
                    anchorMeetingDate))
                .Select(record => record.Id)
                .ToHashSet();
```

Do inicializátoru `PdfExportRecordViewModel` (ř. 881 a dál) za `IsPaused = ...` doplnit:

```csharp
                IsCompleted = completedRecordIds is not null
                    ? completedRecordIds.Contains(record.Id)
                    : TaskStatusRules.IsCompleted(record.StavUkoluId.HasValue
                        ? taskStates.GetValueOrDefault(record.StavUkoluId.Value)
                        : null),
```

- [ ] **Krok 5: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ExportTemplateUseCaseTests"`
Očekávat: všechny zelené včetně dvou nových.

- [ ] **Krok 6: Checkpoint**

Shrnout: model nese ukončenost, v tisku jednání podle data jednání, jinde podle dneška — ověřeno proti databázi. **Necommitovat.**

---

### Task 4: Podbarvení v PDF a HTML

**Soubory:**
- Změnit: `PmTracker.Web/Views/Export/_PdfRecordRow.cshtml:16-21`
- Změnit: `PmTracker.Web/wwwroot/css/pdf-export.css:189`
- Test: `PmTracker.Tests.Unit/Export/CompletedRecordHighlightCssTests.cs`
- Test: `PmTracker.Tests.Api/Controllers/ExportCompletedHighlightTests.cs`

**Rozhraní:**
- Používá z Task 3: `PdfExportRecordViewModel.IsCompleted`

- [ ] **Krok 1: Napsat padající testy**

`PmTracker.Tests.Unit/Export/CompletedRecordHighlightCssTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Podbarvení ukončených (2026-09-05): jemné modré pozadí řádku a pořadí pravidel,
/// aby při souběhu vyhrálo pozastavení (spec §4.3).
/// </summary>
public sealed class CompletedRecordHighlightCssTests
{
    private static string Css() => File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/pdf-export.css"));

    [Fact]
    public void Css_ShadesCompletedRowWithPaleBlue()
    {
        var css = Css();

        css.Should().Contain(".task-row.completed td");
        css.Should().Contain("#eff6ff", "jemná modrá odpovídá váze stávající krémové u pozastavených");
    }

    [Fact]
    public void Css_LetsPausedWinOverCompleted()
    {
        var css = Css();
        var completedIndex = css.IndexOf(".task-row.completed td", StringComparison.Ordinal);
        var pausedIndex = css.IndexOf(".task-row.paused td", StringComparison.Ordinal);

        completedIndex.Should().BeGreaterThan(-1);
        pausedIndex.Should().BeGreaterThan(-1);
        completedIndex.Should().BeLessThan(pausedIndex,
            "obě pravidla mají stejnou specificitu, takže rozhoduje pořadí — pozastavení musí vyhrát");
    }

    [Fact]
    public void Template_MarksCompletedRow()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Export/_PdfRecordRow.cshtml"));

        view.Should().Contain("Model.IsCompleted");
        view.Should().Contain("completed");
    }
}
```

`PmTracker.Tests.Api/Controllers/ExportCompletedHighlightTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Podbarvení ukončených (2026-09-05): příznak se propíše do vykreslené tiskové značky.
/// Tisk vrací PDF, proto se značka přebírá ze vstupu generátoru.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportCompletedHighlightTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportCompletedHighlightTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjektTisk_MarksCompletedRecordRow()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiDoneOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIDONE");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIDONESUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(
            projectId, ownerId, subsystemId, "U", "API completed record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var doneStateId = await dbContext.CiselnikStavuUkolu
                .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
            record.StavUkoluId = doneStateId;
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        await client.GetAsync($"/Export/Projekt/{projectId}/Tisk?asUser={_fixture.AdminOsobaId}");

        var html = _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>().LastHtml;
        html.Should().NotBeNullOrWhiteSpace();
        html.Should().Contain("task-row completed",
            "ukončený úkol musí mít v tisku třídu pro podbarvení");
    }
}
```

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CompletedRecordHighlightCssTests"`
Očekávat: selhání — `.task-row.completed td` v CSS není a šablona `Model.IsCompleted` nezná.

- [ ] **Krok 3: Označit řádek v šabloně**

V `PmTracker.Web/Views/Export/_PdfRecordRow.cshtml` do `@{ }` bloku na konec (za `var subsystemCode = ...`) přidat:

```csharp
    // Podbarvení ukončených (2026-09-05): pozastavení má přednost, proto se přidávají
    // obě třídy a rozhodne pořadí pravidel v pdf-export.css.
    var rowClasses = "task-row";
    if (Model.IsCompleted)
    {
        rowClasses += " completed";
    }
    if (Model.IsPaused)
    {
        rowClasses += " paused";
    }
```

a řádek 21 nahradit:

```razor
<tr class="@rowClasses">
```

- [ ] **Krok 4: Doplnit pravidlo do tiskového CSS**

V `PmTracker.Web/wwwroot/css/pdf-export.css` **před** stávající blok `.task-row.paused td` vložit:

```css
/* Podbarvení ukončených úkolů (2026-09-05). Stojí ZÁMĚRNĚ před pravidlem pro
   pozastavené — obě mají stejnou specificitu, takže rozhoduje pořadí a
   pozastavení musí vyhrát (spec §4.3). */
.task-row.completed td {
    background: #eff6ff;
}

```

- [ ] **Krok 5: Spustit testy a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CompletedRecordHighlightCssTests"`
Očekávat: 3/3 prošly.

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportCompletedHighlightTests"`
Očekávat: 1/1 prošel.

- [ ] **Krok 6: Ověřit, že se nerozbil zbytek tisku**

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Export"`
Očekávat: zelené.

- [ ] **Krok 7: Checkpoint**

Shrnout: PDF a HTML podbarvují ukončené, pozastavení má přednost. **Necommitovat.**

---

### Task 5: Podbarvení ve Wordu

**Soubory:**
- Změnit: `PmTracker.Web/Services/Export/OpenXmlWordExportService.cs:26`
- Změnit: `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs:101`
- Test: `PmTracker.Tests.Unit/Export/OpenXmlWordExportServiceTests.cs` (dva testy do stávající třídy)

**Rozhraní:**
- Používá z Task 3: `PdfExportRecordViewModel.IsCompleted`

- [ ] **Krok 1: Napsat padající testy**

Do `PmTracker.Tests.Unit/Export/OpenXmlWordExportServiceTests.cs` přidat:

```csharp
    /// <summary>Podbarvení ukončených (2026-09-05): Word podbarví celý řádek jako u pozastavených.</summary>
    [Fact]
    public void BuildDocument_ShouldShadeWholeCompletedRecordRow()
    {
        var payload = CreateSut().BuildDocument(
            CreateModelWithSingleRecord("Ukončený úkol", isPaused: false, isCompleted: true));

        AssertRowShading(payload, "Ukončený úkol", "EFF6FF");
    }

    /// <summary>
    /// Podbarvení ukončených (2026-09-05), spec §4.3: kdyby data přinesla obojí,
    /// vyhrává pozastavení.
    /// </summary>
    [Fact]
    public void BuildDocument_ShouldPreferPausedShading_OverCompleted()
    {
        var payload = CreateSut().BuildDocument(
            CreateModelWithSingleRecord("Sporný úkol", isPaused: true, isCompleted: true));

        AssertRowShading(payload, "Sporný úkol", "FDF4E8");
    }

    private static PdfExportTemplateViewModel CreateModelWithSingleRecord(
        string recordName, bool isPaused, bool isCompleted) => new()
    {
        ExportVariant = "meeting",
        AutoPrint = false,
        ProjektId = 10,
        ProjektZkratka = "EXP",
        ProjektNazev = "Export projekt",
        JednaniId = 20,
        JednaniCislo = 551,
        JednaniDatum = new DateTime(2026, 2, 17),
        JednaniMisto = "A1",
        JednaniStav = "Otevřeno",
        Vytvoril = "Tester",
        VytvorenoDne = new DateTime(2026, 2, 18, 12, 0, 0),
        SnapshotSummary = string.Empty,
        PreparationSummary = null,
        ProjektoveRole = [],
        AppliedRuleSummary = ["Automatický meeting výstup"],
        Legenda = [],
        Dochazka = [],
        Zaznamy =
        [
            new PdfExportRecordViewModel
            {
                ZaznamId = 30,
                CisloZaznamu = 1,
                CisloViditelne = "1",
                CisloViditelneA = 1,
                CisloViditelneB = 0,
                Nazev = recordName,
                Cil = "Cíl",
                Popis = "<p>Popis</p>",
                KategorieKod = "U",
                Kategorie = "Úkol",
                TypUkoluKod = null,
                TypUkolu = null,
                Stav = isPaused ? "Pozastaveno" : "Ukončeno",
                IsPaused = isPaused,
                IsCompleted = isCompleted,
                Vlastnik = "Ing. Test Autor",
                SubsystemKod = "SUB",
                Subsystem = "Subsystem",
                DatumZalozeni = new DateTime(2026, 2, 1),
                HistorieTerminu = [],
                Termin = new DateTime(2026, 3, 1),
                ExterniVazby = [],
                Spoluprace = [],
                Vyjadreni = []
            }
        ]
    };

    private static void AssertRowShading(byte[] payload, string recordName, string expectedFillHex)
    {
        using var stream = new MemoryStream(payload);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        body.Should().NotBeNull();

        var recordRow = body!.Descendants<TableRow>()
            .FirstOrDefault(row => row.InnerText.Contains(recordName, StringComparison.Ordinal));
        recordRow.Should().NotBeNull();

        var cells = recordRow!.Elements<TableCell>().ToList();
        cells.Should().HaveCount(3);
        foreach (var cell in cells)
        {
            cell.TableCellProperties.Should().NotBeNull();
            cell.TableCellProperties!.GetFirstChild<Shading>().Should().NotBeNull();
            cell.TableCellProperties.GetFirstChild<Shading>()!.Fill?.Value.Should().Be(expectedFillHex);
        }
    }
```

- [ ] **Krok 2: Spustit testy a ověřit, že padají**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~OpenXmlWordExportServiceTests"`
Očekávat: `BuildDocument_ShouldShadeWholeCompletedRecordRow` selže — buňky nemají žádné podbarvení.

- [ ] **Krok 3: Doplnit barvu**

V `PmTracker.Web/Services/Export/OpenXmlWordExportService.cs` za řádek s `PausedRecordFillHex`:

```csharp
    /// <summary>Ukončený úkol — jemná modrá, stejná jako v tiskovém CSS (2026-09-05).</summary>
    private const string CompletedRecordFillHex = "EFF6FF";
```

- [ ] **Krok 4: Rozšířit volbu výplně**

V `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs` nahradit řádek 101:

```csharp
        var recordFillColor = record.IsPaused ? PausedRecordFillHex : null;
```

za:

```csharp
        // Pozastavení má přednost před ukončením (2026-09-05, spec §4.3).
        var recordFillColor = record.IsPaused
            ? PausedRecordFillHex
            : record.IsCompleted ? CompletedRecordFillHex : null;
```

- [ ] **Krok 5: Spustit testy a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~OpenXmlWordExportServiceTests"`
Očekávat: zelené včetně obou nových a původního testu pozastavených.

- [ ] **Krok 6: Checkpoint**

Shrnout: Word podbarvuje ukončené a při souběhu vyhrává pozastavení, ověřeno na vygenerovaném dokumentu. **Necommitovat.**

---

### Task 6: Varování při startu a regrese

**Soubory:**
- Změnit: `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs:206`
- Test: `PmTracker.Tests.Unit/Export/CompletedRecordHighlightCssTests.cs` (pin na existenci varování)

**Rozhraní:**
- Používá z Task 1: `TaskStatusRules.IsPausedName`

- [ ] **Krok 1: Napsat padající test**

Do `PmTracker.Tests.Unit/Export/CompletedRecordHighlightCssTests.cs` přidat:

```csharp
    [Fact]
    public void StartupValidator_WarnsAboutContradictoryTaskStates()
    {
        var source = File.ReadAllText(
            ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        source.Should().Contain("TaskStatusRules.IsPausedName",
            "varování musí používat stejné pravidlo jako export, jinak se definice rozejdou");
        source.Should().Contain("CiselnikStavuUkolu",
            "kontroluje se číselník stavů úkolů");
        source.Should().Contain("LogWarning",
            "rozporný stav aplikaci nezastaví, jen se zaloguje");
    }
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CompletedRecordHighlightCssTests"`
Očekávat: selhání — validátor `TaskStatusRules` nezná.

- [ ] **Krok 3: Doplnit varování**

V `PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs` **před** řádek
`_logger.LogInformation("SQL startup validace proběhla úspěšně.");` vložit:

```csharp
        // Podbarvení ukončených (2026-09-05, spec §6): číselník stavů úkolů se udržuje
        // přímo v databázi, aplikace ho needituje. Rozporný řádek (pozastavení označené
        // jako koncový stav) by jinak nikdo neodhalil. Číselník je malý, načte se celý
        // a filtruje se stejnou funkcí jako v exportu.
        var taskStates = await dbContext.CiselnikStavuUkolu.AsNoTracking().ToListAsync(ct);
        var contradictoryTaskStates = taskStates
            .Where(state => state.IsFinal && TaskStatusRules.IsPausedName(state.Nazev))
            .Select(state => state.Nazev)
            .ToList();

        if (contradictoryTaskStates.Count > 0)
        {
            _logger.LogWarning(
                "Stavy úkolů {Stavy} jsou označené jako koncové (is_final) a zároveň vypadají jako " +
                "pozastavení. Očekávaná akce: zrušit is_final u těchto stavů přímo v databázi. " +
                "Aplikace funguje, tisk je ale nebude podbarvovat jako ukončené.",
                string.Join(", ", contradictoryTaskStates));
        }

```

Do hlavičky souboru doplnit `using PmTracker.Web.Services.Common;`.

- [ ] **Krok 4: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~CompletedRecordHighlightCssTests"`
Očekávat: 4/4 prošly.

- [ ] **Krok 5: Plná regrese**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.Api
dotnet test PmTracker.Tests.Integration
```

Očekávat: žádné nové selhání. Známé výpadky z minulé práce — **4 gantt testy v Api** (`schedule-layered-marker today`) a **1 integrační** (`ProposalRejectAndTakeOver...`, oprávnění `proposals.accept`) — se nezapočítávají, ale musí se **vyjmenovat**, ne odbýt. Cokoli dalšího je regrese.

- [ ] **Krok 6: Vizuální kontrola skutečného výstupu**

Ověřit na vygenerovaném PDF, že podbarvení opravdu tiskne a že syté modré písmo vyjádření je na něm čitelné. Bez běžící aplikace (chybí `appsettings.Development.json`) to jde přes zahazovací program se skutečným `pdf-export.css` a HTML s řádkem `<tr class="task-row completed">`, převedený na obrázek přes `qlmanage -t`.

Zkontrolovat: podbarvení je jemné, nekřičí, a modré písmo vyjádření na něm nezaniká.

- [ ] **Krok 7: Závěrečný checkpoint**

Shrnout uživateli: co je hotové, výsledky všech sad, a co ověřit ručně — tisk jednání s ukončeným úkolem v PDF i ve Wordu. **Necommitovat.**
