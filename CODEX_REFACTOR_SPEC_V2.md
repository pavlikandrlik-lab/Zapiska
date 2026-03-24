# PmTracker.Web — Refaktoringová specifikace v2

## INSTRUKCE PRO CODEX (čti před každým krokem)

Jsi senior C# / JavaScript developer pracující na .NET 8 ASP.NET Core MVC aplikaci `PmTracker.Web`.
Řídíš se governance pravidly z `CODEX_INSTRUCTIONS_V3.md`. Níže jsou projektová doplnění.

### Pevná pravidla pro tento projekt

```
[P1]  Po každém kroku: dotnet build — zelený build před pokračováním.
[P2]  Žádné DB migrace, žádné změny v PmTrackerDbContext DbSety.
[P3]  Žádné změny HTTP kontraktů — stejné URL, form fields, JSON response (ModalSubmitResultViewModel).
[P4]  Žádné přejmenování metod bez explicitního pokynu v tomto dokumentu.
[P5]  Žádné abstrakce mimo tento dokument.
[P6]  Při technické nejednoznačnosti: volím nejmenší bezpečnou a reverzibilní změnu sám.
      Zastavuji se a hlásím jen tehdy, když rozhodnutí mění kontrakt, architekturu,
      bezpečnost, data nebo business chování — a toto dokument neřeší.
[P7]  Než vytvořím nový soubor: ověřím, zda v projektu neexistuje co mohu rozšířit.
[P8]  Před smazáním nebo přesunem souboru: projdu závislosti. Nikdy nesmažu bez jasné cílové cesty.
[P9]  CancellationToken parametr se vždy jmenuje `ct`, je vždy jako poslední parametr.
[P10] public partial class Program v Program.cs PONECH — je potřeba pro integrační testy.
```

### Formát hlášky při zastavení

```
Umístění: [soubor:řádek]
Problém: [co spec říká] ≠ [co repo obsahuje]
Dopad: [konkrétní riziko]
Varianty: A) ... B) ...
Doporučení: Varianta X — [důvod]
Build stav: ✅ / ❌ [popis]
```

### Soubory které se nesmí měnit bez zastavení a dotazu

```
Services/ActiveDirectory/               — AD integrace
Models/Entities/PmTrackerEntities.cs    — entity třídy
Services/Security/UserContextResolver.cs
Services/Common/                        — sdílené utility
Services/Schedules/ScheduleTimelineCalculator.cs
Veškeré DB migrace
```

`.cshtml` Views: nesmí se měnit VYJMA kroků 6, 7, 13 které to explicitně nařizují.

---

## PŘEHLED — Co a proč

Aplikace vznikala po částech přes AI bez globálního kontextu. Výsledek:
- 10+ vrstev delegace pro jednu DB operaci
- `SqlServerDataStore` implementuje 6 interface, 20+ závislostí
- `Modules/` je z velké části duplikát `Services/Data/` — ale **ne celý** (viz Krok 1)
- Celá aplikace synchronní — blokuje ASP.NET thread pool
- `site.js` 9 016 řádků v jednom souboru
- Views obsahují business logiku, výpočty, permission checky

**Cílový stav:** Controller → Service (2–3 závislosti) → DbContext. Async/await. ES moduly.

---

## KROK 1 — Bezpečný přesun a smazání `Modules/`

### Proč

`Modules/` obsahuje hlavně 1–3řádkové proxy třídy. Příklad volání `BuildJednaniDetail`:
`JednaniController → IMeetingsQueries → MeetingsQueries → IBuildJednaniDetailQueryHandler → BuildJednaniDetailQueryHandler → IMeetingsDataStore → MeetingsDataStore → IPmTrackerDataStore → SqlServerDataStore → IMeetingDetailQueriesUseCase → MeetingDetailQueriesUseCase` (zde teprve pracuje).

**Důležité:** Modules/ není čistě proxy. Část souborů obsahuje vlastní logiku a musí být přesunuta, ne smazána.

---

### Akce 1.1 — Audit Modules/ — klasifikace A/B

Projdi každý soubor v `Modules/`. Zařaď ho:
- **Skupina A** = čistá proxy (1–3 řádky, žádná logika) → **smazat**
- **Skupina B** = vlastní logika, kontrakt nebo kompozice → **přesunout** na cílovou cestu

**Níže je úplný audit. Postupuj přesně podle něj.**

#### Skupina B — PŘESUNOUT (před mazáním cokoliv jiného)

| Soubor v Modules/ | Cílová cesta | Namespace |
|---|---|---|
| `Modules/Records/IRecordUiFlowResolver.cs` | `Services/Records/IRecordUiFlowResolver.cs` | `PmTracker.Web.Services.Records` |
| `Modules/Records/RecordUiFlowResolver.cs` | `Services/Records/RecordUiFlowResolver.cs` | `PmTracker.Web.Services.Records` |
| `Modules/Export/ExportTemplateQueries.cs` | `Services/Export/ExportTemplateQueries.cs` | `PmTracker.Web.Services.Export` |
| `Modules/Export/ExportTemplateUseCase.cs` | `Services/Export/ExportTemplateUseCase.cs` | `PmTracker.Web.Services.Export` |
| `Modules/Export/IExportTemplateQueries.cs` | `Services/Export/IExportTemplateQueries.cs` | `PmTracker.Web.Services.Export` |
| `Modules/Export/IExportTemplateUseCase.cs` | `Services/Export/IExportTemplateUseCase.cs` | `PmTracker.Web.Services.Export` |
| `Modules/Export/ExportTemplateQueryResult.cs` | `Services/Export/ExportTemplateQueryResult.cs` | `PmTracker.Web.Services.Export` |
| `Modules/Export/ExportTemplateSummaryProjection.cs` | `Services/Export/ExportTemplateSummaryProjection.cs` | `PmTracker.Web.Services.Export` |
| `Modules/Export/Queries/I*.cs` (všechny query interface) | `Services/Export/Queries/` | `PmTracker.Web.Services.Export.Queries` |
| `Modules/Export/Queries/Export*ProjectionBuilder.cs` | `Services/Export/Queries/` | `PmTracker.Web.Services.Export.Queries` |
| `Modules/Export/Queries/ExportRecordVisibilityEvaluator.cs` | `Services/Export/Queries/` | `PmTracker.Web.Services.Export.Queries` |
| `Modules/Export/Queries/ExportTemplateSummaryBuilder.cs` | `Services/Export/Queries/` | `PmTracker.Web.Services.Export.Queries` |
| `Modules/Settings/ISettingsAuthzQueries.cs` | `Services/Settings/ISettingsAuthzQueries.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/ISettingsAuthzCommands.cs` | `Services/Settings/ISettingsAuthzCommands.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/ISettingsModalModelFactory.cs` | `Services/Settings/ISettingsModalModelFactory.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/IUserAuthorizationSnapshotBuilder.cs` | `Services/Settings/IUserAuthorizationSnapshotBuilder.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/UserAuthorizationSnapshot.cs` | `Services/Settings/UserAuthorizationSnapshot.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/SettingsAuthzQueries.cs` | `Services/Settings/SettingsAuthzQueries.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/SettingsAuthzCommands.cs` | `Services/Settings/SettingsAuthzCommands.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/SettingsModalModelFactory.cs` | `Services/Settings/SettingsModalModelFactory.cs` | `PmTracker.Web.Services.Settings` |
| `Modules/Settings/UserAuthorizationSnapshotBuilder.cs` | `Services/Settings/UserAuthorizationSnapshotBuilder.cs` | `PmTracker.Web.Services.Settings` |

Po přesunu: aktualizuj `namespace` v každém souboru. Aktualizuj `using` v:
- `ZaznamyController.cs`
- `ExportController.cs`
- `NastaveniController.cs`
- `ProfilePageQueriesUseCase.cs`
- Kdekoliv jinde kde kompilátor nahlásí chybu po změně namespace.

#### Skupina A — SMAZAT

```
Modules/Meetings/          — celá složka (MeetingsQueries, MeetingsCommands, MeetingsDataStore, jejich interface, DI extension)
Modules/Projects/          — celá složka (ProjectsQueries, ProjectsCommands, ProjectsDataStore, jejich interface, DI extension)
Modules/Export/ExportDataStore.cs
Modules/Export/ExportQueries.cs
Modules/Export/Queries/*DataStore*.cs  (pokud existují — proxy)
Modules/Settings/SettingsDataStore.cs
Jakýkoliv *ModuleServiceCollectionExtensions.cs soubor (DI wiring pouze pro proxy)
```

**Postup mazání:** Nejdřív přesuň vše ze skupiny B. Pak projdi skupinu A — pro každý soubor ověř, že nemá logiku (zkontroluj obsah, nejen název). Teprve pak smaž.

---

### Akce 1.2 — Aktualizuj controllery

**`JednaniController`:**
```csharp
// ODSTRAŇ:
IMeetingsQueries _meetingsQueries
IMeetingsCommands _meetingsCommands

// PŘIDEJ:
IMeetingDetailQueriesUseCase _meetingDetailQueriesUseCase
IMeetingListQueriesUseCase _meetingListQueriesUseCase
IMeetingWriteCommandsUseCase _meetingWriteCommandsUseCase
```
Přepiš volání: `_meetingsQueries.BuildJednaniDetail(id)` → `_meetingDetailQueriesUseCase.BuildJednaniDetail(id)` atd.

**`ProjektyController`:**
```csharp
// ODSTRAŇ:
IProjectsQueries _projectsQueries
IProjectsCommands _projectsCommands

// PŘIDEJ:
IProjectDetailQueriesUseCase _projectDetailQueriesUseCase
IProjectListQueriesUseCase _projectListQueriesUseCase
IProjectCommandsUseCase _projectCommandsUseCase
IProjectAssignmentCommandsUseCase _projectAssignmentCommandsUseCase
IMeetingListQueriesUseCase _meetingListQueriesUseCase
IMeetingWriteCommandsUseCase _meetingWriteCommandsUseCase
```

**`ExportController`:**
```csharp
// ODSTRAŇ:
IExportQueries _exportQueries

// PŘIDEJ:
IExportTemplateUseCase _exportTemplateUseCase
IPmTrackerDataStore _dataStore   // dočasně — odstraníme v Kroku 2
```

**Ostatní controllery:** stejný vzor — identifikuj Modules interface, nahraď příslušným UseCase ze `Services/Data/`.

### Akce 1.3 — Odstraň registrace z DI

V `Program.cs` odstraň `AddPmTrackerModules()` nebo ekvivalentní volání.
V DI extension souborech odstraň registrace proxy interface z `Modules/` namespace.

### Akce 1.4 — Build

```
dotnet build
```

Oprav všechny chyby. Nejčastější: chybějící `using` po namespace změnách skupiny B.

---

## KROK 2 — Odstranění `SqlServerDataStore` a `IPmTrackerDataStore`

### Proč

`SqlServerDataStore` implementuje 6 interface a má 20+ závislostí. Skoro každá metoda je čistá delegace. Výjimka: **obsahuje harmonogramové helpery** a **metody s vlastní logikou** — ty přesuneme, ne smažeme.

### Akce 2.1 — Vytvoř `HarmonogramService`

Vytvoř soubory:
- `PmTracker.Web/Services/Data/IHarmonogramService.cs`
- `PmTracker.Web/Services/Data/HarmonogramService.cs`

**Přesuň ze `SqlServerDataStore.cs`** PŘESNĚ tyto metody:

```
GetActiveHarmonogramSchema()
EnsurePersistedActiveHarmonogramSchemaVersion()
LoadHarmonogramSchema(int schemaVersion)
LoadHarmonogramTypy(int schemaVersion)
BuildFallbackSchemaDefinition(int version = 0, string? delayBarvaHex = null)  [static]
BuildDefaultHarmonogramTypeRows(int schemaVersion)  [static]
ResolveDefaultStepColor(int stepIndex)  [static]
IsValidHexColor(string? value)  [static]
NormalizeHexColor(string? value, string fallback)  [static]
IsMissingHarmonogramCatalogSchema(Exception ex)  [static]
BuildHarmonogramVypocet(...)  [static]
BuildHarmonogramSouhrn(...)  [static]
BuildRecordScheduleTypeDefinitions(HarmonogramSchemaDefinition schema)  [static]
GetSchemaForRecord(ProjektovyZaznamEntity record, ...)
GetSchemaForRecord(int schemaVersion, ...)
BuildHarmonogramKrokyCiselnikDetail(string key, bool canChangeLockState)
CountHarmonogramCatalogRows()
SaveHarmonogramStepRow(SaveCiselnikRowCommand command)
DeleteHarmonogramStepRow(DeleteCiselnikRowCommand command)
CloneActiveHarmonogramSchema()
ActivateClonedHarmonogramSchema(HarmonogramSablonaEntity newSchema)
FinalizeClonedHarmonogramSchema(HarmonogramSablonaEntity newSchema, bool normalizeRows = true)
NormalizeHarmonogramSchemaRows(int schemaVersion)
BuildUniqueDelayCode(string baseCode, IEnumerable<string?> usedCodes)  [static]
```

Přesuň také tyto private record typy jako `internal` typy v souboru `HarmonogramService.cs`:
```csharp
internal sealed record HarmonogramTypPar(int KrokIndex, string Kod, string Nazev, string BarvaHex, int TrvaniTypId, int ZpozdeniTypId);
internal sealed record HarmonogramSchemaDefinition(int Verze, string DelayBarvaHex, IReadOnlyList<HarmonogramTypPar> Kroky);
internal sealed record HarmonogramSchemaCloneResult(HarmonogramSablonaEntity SourceSchema, HarmonogramSablonaEntity NewSchema, IReadOnlyDictionary<int, HarmonogramTypEntity> ClonedBySourceId, IReadOnlyList<HarmonogramTypEntity> ClonedRows);
internal sealed record HarmonogramVypocetKroku(int KrokIndex, string Kod, string Nazev, string BarvaHex, int TrvaniTypId, int ZpozdeniTypId, int TrvaniDni, int ZpozdeniDni, DateTime PlanStartDatum, DateTime BaselineDatum, DateTime RealStartDatum, DateTime PosunuteDatum);
```

Konstanty přesuň také:
```
DefaultHarmonogramKroky  (pole)
DefaultDelayBarvaHex = "#DC2626"
HarmonogramDelayColorPseudoRowId = 0
HarmonogramDelayColorPseudoKod = "DELAY_COLOR"
```

**Konstruktor:**
```csharp
internal sealed class HarmonogramService(PmTrackerDbContext dbContext, TimeProvider timeProvider)
```

**`IHarmonogramService` musí exportovat** (ostatní mohou být internal):
```csharp
public interface IHarmonogramService
{
    HarmonogramSchemaDefinition GetActiveHarmonogramSchema();
    HarmonogramSchemaDefinition GetSchemaForRecord(ProjektovyZaznamEntity record);
    HarmonogramSchemaDefinition GetSchemaForRecord(int schemaVersion);
    IReadOnlyList<RecordScheduleTypeDefinition> BuildRecordScheduleTypeDefinitions(HarmonogramSchemaDefinition schema);
    HarmonogramVypocetResult BuildHarmonogramVypocet(DateTime datumZalozeni, IReadOnlyList<HarmonogramTypPar> typy, IReadOnlyDictionary<int, int>? hodnoty);
    HarmonogramSouhrn BuildHarmonogramSouhrn(IReadOnlyList<HarmonogramVypocetKroku> kroky, DateTime terminUkolu);
    Task EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct = default);
    Task<HarmonogramKrokyCiselnikDetailViewModel> BuildHarmonogramKrokyCiselnikDetailAsync(string key, bool canChangeLockState, CancellationToken ct = default);
    Task<int> CountHarmonogramCatalogRowsAsync(CancellationToken ct = default);
    Task SaveHarmonogramStepRowAsync(SaveCiselnikRowCommand command, CancellationToken ct = default);
    Task DeleteHarmonogramStepRowAsync(DeleteCiselnikRowCommand command, CancellationToken ct = default);
    Task<HarmonogramSchemaCloneResult> CloneActiveHarmonogramSchemaAsync(CancellationToken ct = default);
}
```

### Akce 2.2 — Vytvoř `CommentService`

**Toto je nový standalone service — neodstraňuj ho dál v Kroku 11.**

Vytvoř `Services/CommentService.cs`:

```csharp
namespace PmTracker.Web.Services;

public interface ICommentService
{
    // Přesuň sem přesné signatury všech metod z IRecordCommentCommandsUseCase
    // (SaveComment, DeleteComment, SaveMeetingNote — cokoliv existuje)
}

public sealed class CommentService(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    ICommentAuthorizationPolicy commentAuthorizationPolicy,
    TimeProvider timeProvider) : ICommentService
{
    // Přesuň sem implementaci z RecordCommentCommandsUseCase
}
```

Zaregistruj v DI:
```csharp
services.AddScoped<CommentService>();
services.AddScoped<ICommentService>(sp => sp.GetRequiredService<CommentService>());
```

**Důvod pro standalone service:** ProjectService i MeetingService i RecordService potřebují comment operace. Pokud by CommentService byl součástí RecordService, vznikne circular dependency (RecordService → IProjectService → ProjectService → IRecordService → circular). Standalone CommentService tento kruh přeruší.

### Akce 2.3 — Vytvoř `ProjectDataService`

Vytvoř `PmTracker.Web/Services/Data/ProjectDataService.cs`.

Přesuň ze `SqlServerDataStore` metody s vlastní logikou:

```
BuildCurrentUserContext(string? asProfile)
BuildProjektDetail(int id)
BuildZaznamEditForEntity(...)
BuildZaznamEdit(int id)
BuildZaznamCreate(int projektId, ...)
BuildProjectScheduleRows(...)
BuildRecordCardsForProject(int projectId)
BuildActiveProjectSubsystems(int projectId)
BuildActiveProjectMembershipRows(int projectId)
BuildUnifiedActiveProjectRoleRows(int projectId)
BuildUnifiedProjectRoleHistoryRows(int projectId)
BuildActiveProjectRoleAssignments(int projectId)
BuildProjectRoleHistory(int projectId)
BuildProjectMemberCandidates()
BuildRecordOwnerCandidates(int projectId, int? selectedOwnerId)
BuildProjectSubsystemOptions(int projectId)
BuildLeadEquivalentOsobaIdsByProjectSubsystem(int projectId)
BuildDefaultOwnerOsobaIdsByProjectSubsystem(int projectId)
BuildOpenMeetingOptions(...)
ResolveSelectedMeetingIdForNumber(...)  [static]
GetNextCisloZaznamu(int projektId)
GetNextCisloZaznamuTransactional(int projektId)
AllocateMeetingOrderTransactional(int projektId, int cisloJednani)
BuildRecordEditorProjectSubsystems(int projectId, int currentSubsystemId)
BuildRecordEditorDefaultOwnerOsobaIds(int projectId)
ResolveLeadEquivalentOsobaIds(int projectId, int subsystemId)
IsMeetingOpenForRecordNumbering(JednaniListItemViewModel meeting)
SaveTeamMember(SaveTeamMemberCommand, CurrentUserContextViewModel)
RemoveTeamMember(RemoveTeamMemberCommand, CurrentUserContextViewModel)
ResolveAsProfileToOsobaId(string? asProfile)
```

Helper metody (přesuň také):
```
BuildDisplayName(...)
BuildDisplayNameFromOsoba(...)
BuildInlinePersonLabel(...)
BuildInlinePersonLabelFromOsoba(...)
BuildSubsystemRoleLabel(...)  [static]
IsTaskCategory(...)  [static]
IsMeetingReadOnly(...)  [static]
ResolveVisibleRecordNumber(...)  [static]
ResolveVisibleNumberPartA(...)  [static]
ResolveVisibleNumberPartB(...)  [static]
OrderRecordsByVisibleNumber(...)  [static]
ExtractServiceDeskTicketId(...)  [static]
BuildServiceDeskUrl(...)  [static]
NormalizeAdLogin(...)  [static]
BuildNormalizedLoginCandidates(...)  [static]
LoginEquals(...)  [static]
LoginContains(...)  [static]
```

Poznámka: `WriteAudit` — každá service třída si nechá svou private metodu lokálně.

**Konstruktor:**
```csharp
internal sealed class ProjectDataService(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer,
    IPersonIdentityMatcher personIdentityMatcher,
    IHarmonogramService harmonogramService,
    IProjectDetailQueriesUseCase projectDetailQueriesUseCase,
    IProjectListQueriesUseCase projectListQueriesUseCase,
    IProjectCommandsUseCase projectCommandsUseCase,
    IProjectAssignmentCommandsUseCase projectAssignmentCommandsUseCase,
    IRecordEditorQueriesUseCase recordEditorQueriesUseCase,
    IRecordWriteCommandsUseCase recordWriteCommandsUseCase,
    ICommentService commentService,
    TimeProvider timeProvider)
```

`ProjectDataService` implementuje:
- `IProjectDetailComposition`
- `IRecordEditorQueriesComposition`
- `IRecordWriteCommandsComposition`

### Akce 2.4 — Smaž `SqlServerDataStore.cs` a `IPmTrackerDataStore.cs`

Před smazáním ověř: každá metoda z `SqlServerDataStore` je buď přesunuta do `HarmonogramService`, `ProjectDataService`, nebo je čistá proxy (pak ji lze smazat).

### Akce 2.5 — Oprav všechna volání `IPmTrackerDataStore`

Vyhledej v projektu `IPmTrackerDataStore`. Pro každé místo:
- `ExportController` → nahraď `IExportTemplateUseCase`
- Jiná místa by neměla existovat (odstranil jsi je v Kroku 1)

Smaž `IPmTrackerDataStore.cs`.

### Akce 2.6 — Aktualizuj proxy Services

`Services/People/PeopleService.cs`, `Services/Dictionaries/DictionariesService.cs`, `Services/Profile/ProfileService.cs`, `Services/Records/RecordsService.cs`:

**`PeopleService`** → injektuj přímo `IPeoplePageQueriesUseCase` a `IPersonCommandsUseCase`.

**`DictionariesService`** → injektuj `IDictionariesQueriesUseCase`, `IDictionariesCommandsUseCase`, `IHarmonogramService`.

**`ProfileService`** → injektuj přímo `IProfilePageQueriesUseCase`.

**`RecordsService`** → injektuj `IRecordEditorQueriesUseCase`, `IRecordWriteCommandsUseCase`, `ICommentService`, `IRecordEditorQueriesComposition`, `IRecordWriteCommandsComposition`.

Smaž proxy DataStore soubory:
```
Services/People/IPeopleDataStore.cs + PeopleDataStore.cs
Services/Dictionaries/IDictionariesDataStore.cs + DictionariesDataStore.cs
Services/Profile/IProfileDataStore.cs + ProfileDataStore.cs
Services/Records/IRecordsDataStore.cs + RecordsDataStore.cs
Services/Records/Commands/    — celá složka (1řádkové proxy)
Services/Records/Queries/     — celá složka (1řádkové proxy)
```

### Akce 2.7 — Aktualizuj DI registrace

V `DataStoreServiceCollectionExtensions.cs`:

**Smaž:**
```csharp
services.AddScoped<SqlServerDataStore>();
services.AddScoped<IPmTrackerDataStore>(sp => sp.GetRequiredService<SqlServerDataStore>());
```

**Přidej:**
```csharp
services.AddScoped<HarmonogramService>();
services.AddScoped<IHarmonogramService>(sp => sp.GetRequiredService<HarmonogramService>());

services.AddScoped<CommentService>();
services.AddScoped<ICommentService>(sp => sp.GetRequiredService<CommentService>());

services.AddScoped<ProjectDataService>();
services.AddScoped<IProjectDetailComposition>(sp => sp.GetRequiredService<ProjectDataService>());
services.AddScoped<IRecordEditorQueriesComposition>(sp => sp.GetRequiredService<ProjectDataService>());
services.AddScoped<IRecordWriteCommandsComposition>(sp => sp.GetRequiredService<ProjectDataService>());
```

### Akce 2.8 — Build

```
dotnet build
```

Toto je největší krok. Chyb může být více — procházej systematicky, jedna po druhé.

---

## KROK 3 — Konverze na async/await

### Proč

Celá aplikace je synchronní. `.ToList()`, `.SaveChanges()` blokují OS thread po dobu čekání na SQL Server. Thread pool se vyčerpá při souběžné zátěži.

### Akce 3.1 — Přidej `CancellationToken ct = default` do všech service metod

Pro každou metodu v těchto třídách přidej `CancellationToken ct = default` jako **poslední** parametr:
- `MeetingDetailQueriesUseCase`, `MeetingListQueriesUseCase`, `MeetingWriteCommandsUseCase`
- `ProjectDetailQueriesUseCase`, `ProjectListQueriesUseCase`, `ProjectCommandsUseCase`, `ProjectAssignmentCommandsUseCase`
- `RecordEditorQueriesUseCase`, `RecordWriteCommandsUseCase`
- `CommentService` (z Kroku 2)
- `DictionariesQueriesUseCase`, `DictionariesCommandsUseCase`
- `PeoplePageQueriesUseCase`, `PersonCommandsUseCase`
- `ProfilePageQueriesUseCase`
- `HarmonogramService`
- `ProjectDataService`
- `PeopleService`, `DictionariesService`, `ProfileService`, `RecordsService`
- Všechny odpovídající interface

Parametr se vždy jmenuje `ct`. Nikdy `cancellationToken`.

### Akce 3.2 — Přejmenuj metody na Async konvenci

Každá service metoda dostane suffix `Async`:
```
BuildJednaniDetail → BuildJednaniDetailAsync
SaveMeeting → SaveMeetingAsync
BuildProjektyList → BuildProjektyListAsync
```
Platí pro všechny metody ve všech service třídách z Akce 3.1.

### Akce 3.3 — Nahraď synchronní EF Core volání

| Synchronní | Async náhrada |
|---|---|
| `.ToList()` | `.ToListAsync(ct)` |
| `.ToDictionary(...)` | `.ToListAsync(ct)` pak `.ToDictionary()` v paměti |
| `.FirstOrDefault(x => ...)` | `.FirstOrDefaultAsync(x => ..., ct)` |
| `.First(x => ...)` | `await ...FirstOrDefaultAsync(x => ..., ct) ?? throw new ...` |
| `.Any(x => ...)` | `.AnyAsync(x => ..., ct)` |
| `.Count(x => ...)` | `.CountAsync(x => ..., ct)` |
| `.SaveChanges()` | `await dbContext.SaveChangesAsync(ct)` |

Přidej `using Microsoft.EntityFrameworkCore;` všude kde chybí.

### Akce 3.4 — Přidej async do podpisu metod

```csharp
// PŘED:
public JednaniDetailViewModel BuildJednaniDetail(int id) { ... }

// PO:
public async Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default) { ... }
```

### Akce 3.5 — Aktualizuj Controller actions

```csharp
// PO:
public async Task<IActionResult> Index(CancellationToken ct)
{
    var model = await _service.BuildAsync(HttpContext.RequestAborted);
    return View(model);
}
```

CancellationToken v controllerech = vždy `HttpContext.RequestAborted`.

### Akce 3.6 — Aktualizuj `BaseController.ExecuteValidatedCommand`

```csharp
protected async Task<IActionResult> ExecuteValidatedCommandAsync(
    Func<bool> hasPermission,
    string invalidAjaxMessage,
    string invalidFallbackMessage,
    Func<IActionResult> onInvalidRedirect,
    Func<Task<IActionResult>> onSuccessRedirect,
    Func<Task<IActionResult>>? onAjaxSuccess,
    Func<Task> operation,
    Func<Exception, Task<IActionResult>>? onExceptionRedirect = null)
```

**Nikdy nepoužívej `.Result` nebo `.Wait()` — sync-over-async deadlock.**
**Nikdy nepoužívej `async void` — vždy `async Task`.**

### Akce 3.7 — Build

```
dotnet build
```

---

## KROK 4 — Oprava DB dotazů (over-fetching a N+1)

### Akce 4.1 — Oprav načítání všech osob

V `MeetingDetailQueriesUseCase.BuildMeetingTasksAsync()`:

```csharp
// PŘED:
var people = dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

// PO:
var requiredPersonIds = projectRecords
    .Select(r => r.VlastnikId)
    .Concat(commentsByRecord.Values.SelectMany(c => c).Select(c => c.AutorOsobaId))
    .Distinct()
    .ToList();
var peopleList = await dbContext.Osoby.AsNoTracking()
    .Where(x => requiredPersonIds.Contains(x.Id))
    .ToListAsync(ct);
var people = peopleList.ToDictionary(x => x.Id);
```

Stejný vzor všude kde vidíš `.ToDictionary(x => x.Id)` na celé tabulce bez WHERE.

### Akce 4.2 — Přidej lightweight metody pro `JednaniController`

Do `IMeetingDetailQueriesUseCase` přidej:
```csharp
Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default);
Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default);
```

Implementuj v `MeetingDetailQueriesUseCase` — minimální SELECT.

V `JednaniController`:
- `SaveStatus`, `AddMeetingParticipantModal` → volej `GetMeetingProjectIdAsync()` místo `BuildJednaniDetailAsync()`
- `TaskItemPartial` → volej `GetSingleTaskAsync()`

### Akce 4.3 — Oprav N+1 v `SaveAttendance`

```csharp
// PO — batch operace, JEDEN SaveChangesAsync:
public async Task SaveAttendanceBatchAsync(
    int meetingId,
    IEnumerable<(int OsobaId, string StavUcasti)> rows,
    CurrentUserContextViewModel currentUser,
    CancellationToken ct = default)
{
    var stateRows = await dbContext.CiselnikStavuUcasti.AsNoTracking().ToListAsync(ct);
    var existing = await dbContext.Ucast
        .Where(x => x.JednaniId == meetingId)
        .ToListAsync(ct);
    var existingByPerson = existing.ToDictionary(x => x.OsobaId);

    foreach (var (osobaId, stavKod) in rows.Where(r => r.OsobaId > 0 && !string.IsNullOrWhiteSpace(r.StavUcasti)))
    {
        var stateId = stateRows.First(x => x.Kod == stavKod).Id;
        if (existingByPerson.TryGetValue(osobaId, out var entity))
            entity.StavUcastiId = stateId;
        else
            dbContext.Ucast.Add(new UcastEntity { JednaniId = meetingId, OsobaId = osobaId, StavUcastiId = stateId });
    }

    await dbContext.SaveChangesAsync(ct); // JEDEN roundtrip
    WriteAudit(...);
}
```

### Akce 4.4 — Build

```
dotnet build
```

---

## KROK 5 — Oprava `BaseController`

### Akce 5.1 — Odstraň Service Locator

```csharp
// ODSTRAŇ:
var timeProvider = HttpContext.RequestServices.GetService<TimeProvider>();
var loggerFactory = HttpContext.RequestServices.GetService<ILoggerFactory>();

// PŘIDEJ do konstruktoru BaseController:
protected BaseController(
    IUserContextResolver userContextResolver,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
```

Aktualizuj všechny controllery dědící z `BaseController` — přidej `TimeProvider` a `ILoggerFactory` do konstruktoru a předej `base(...)`.

### Akce 5.2 — Smaž mrtvý kód v `ZaznamyController`

```csharp
// PŘED:
if (string.Equals(tab, "harmonogram", StringComparison.OrdinalIgnoreCase)
    || string.Equals(tab, "gant", StringComparison.OrdinalIgnoreCase))

// PO:
if (string.Equals(tab, "harmonogram", StringComparison.OrdinalIgnoreCase))
```

### Akce 5.3 — Build

```
dotnet build
```

---

## KROK 6 — Přesun logiky z Views do ViewModelů

### Proč

Views obsahují `@{ var x = ...; }` bloky s permission checky, výpočty, přetypováním ViewBag.
MVC princip: View je hloupá HTML šablona. Logika patří do ViewModelu nebo Service.

### Akce 6.1 — Vytvoř `BaseViewModel`

```csharp
// Models/ViewModels/BaseViewModel.cs
public abstract class BaseViewModel
{
    public required CurrentUserContextViewModel CurrentUserContext { get; init; }

    public bool CanViewPeopleTab =>
        CurrentUserContext.HasPermissionPrefix(PermissionKeys.PeoplePrefix);
    public bool CanViewCiselnikyTab =>
        CurrentUserContext.HasPermissionPrefix(PermissionKeys.CiselnikyPrefix);
    public bool CanViewSettingsTab =>
        CurrentUserContext.HasPermissionPrefix(PermissionKeys.SettingsPrefix);
}
```

### Akce 6.2 — Vytvoř `NavPermissionsViewModel`

```csharp
// Models/ViewModels/NavPermissionsViewModel.cs
public sealed class NavPermissionsViewModel
{
    public bool CanViewPeople { get; init; }
    public bool CanViewCiselniky { get; init; }
    public bool CanViewSettings { get; init; }
    public string? CurrentUserDisplayName { get; init; }
    public string? CurrentUserOrg { get; init; }
}
```

### Akce 6.3 — Aktualizuj `BaseController.OnActionExecutionAsync`

**Odstraň** všechna `ViewBag` přiřazení:
```csharp
// ODSTRAŇ:
ViewBag.CurrentUserContext = ...
ViewBag.CurrentUserName = ...
ViewBag.CurrentUserEmail = ...
ViewBag.CurrentUserOrg = ...
ViewBag.CurrentUserOrgCode = ...
ViewBag.CurrentUserRoles = ...
ViewBag.IsGlobalAdmin = ...
```

**Přidej:**
```csharp
ViewData["NavPermissions"] = new NavPermissionsViewModel
{
    CanViewPeople = CurrentUserContext.HasPermissionPrefix(PermissionKeys.PeoplePrefix),
    CanViewCiselniky = CurrentUserContext.HasPermissionPrefix(PermissionKeys.CiselnikyPrefix),
    CanViewSettings = CurrentUserContext.HasPermissionPrefix(PermissionKeys.SettingsPrefix),
    CurrentUserDisplayName = CurrentUserContext.DisplayName,
    CurrentUserOrg = CurrentUserContext.OrganizacniCelek
};
```

### Akce 6.4 — Přidej computed properties do `ProjektDetailViewModel`

```csharp
public bool CanCreateMeetings { get; init; }
public bool CanEditMeetings { get; init; }
public bool CanManageTeam { get; init; }
public bool CanManageRecords { get; init; }
public bool CanManageSchedules { get; init; }
public string? CreateRecordEditorUrl { get; init; }
public string? ReturnToProjectUrl { get; init; }
public string? ProjectPrintUrl { get; init; }
public string? ProjectWordUrl { get; init; }
```

V `ProjektyController.Detail()`:
```csharp
model = model with
{
    CanCreateMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, id),
    CanEditMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, id),
    CanManageTeam = CurrentUserContext.HasPermission(PermissionKeys.TeamManage, id),
    CanManageRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, id),
    CanManageSchedules = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, id)
        || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, id)
        || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, id),
    CreateRecordEditorUrl = Url.Action("Create", "Zaznamy", new { projektId = id }),
    ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = id }),
    ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = id })
};
```

### Akce 6.5 — Přesuň Gantt výpočty do `ProjektHarmonogramUkolViewModel`

V `Views/Projekty/Detail.cshtml` jsou pro každou položku harmonogramu výpočty:
```csharp
// Tyto @{ } bloky patří do ViewModelu:
var compactAxisDays = Math.Max(1, ...);
Func<DateTime, double> toCompactPercent = ...;
```

Přidej do `ProjektHarmonogramUkolViewModel`:
```csharp
public double CompactDeadlinePercent { get; init; }
public double CompactTodayPercent { get; init; }
public double BreakdownTodayPercent { get; init; }
public string FormatCompactDeadlinePercent { get; init; } = "";
public string FormatCompactTodayPercent { get; init; } = "";
// ... ostatní computed hodnoty ze View
```

Výpočty přesuň do `BuildProjectScheduleRowsAsync` v `ProjectDataService`.

### Akce 6.6 — Přesuň výpočty z `Views/Export/PdfTemplate.cshtml`

`.GroupBy()`, `.OrderBy()`, `.Select()` patří do `ExportTemplateUseCase` — šablona dostane hotová data.

### Akce 6.7 — Aktualizuj `_Layout.cshtml`

```csharp
// ODSTRAŇ veškerou logiku v @{ } bloku
// PŘIDEJ na začátek:
@{
    var nav = ViewData["NavPermissions"] as NavPermissionsViewModel;
}
// Používej:
@if (nav?.CanViewPeople == true) { ... }
@nav?.CurrentUserDisplayName
```

### Akce 6.8 — Build

```
dotnet build
```

---

## KROK 7 — Sjednocení page header partial

### Akce 7.1 — Vytvoř `PageHeaderViewModel`

```csharp
// Models/ViewModels/PageHeaderViewModel.cs
public sealed class PageHeaderViewModel
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? BackUrl { get; init; }
    public string? BackLabel { get; init; }
    public string? Badge { get; init; }
}
```

### Akce 7.2 — Vytvoř `Views/Shared/_PageHeader.cshtml`

```html
@model PageHeaderViewModel
<section class="page-header">
    @if (Model.BackUrl != null)
    {
        <a class="btn small ghost" href="@Model.BackUrl">@(Model.BackLabel ?? "Zpět")</a>
    }
    <div class="page-header-content">
        <h1>@Model.Title</h1>
        @if (Model.Subtitle != null) { <p class="muted">@Model.Subtitle</p> }
        @if (Model.Badge != null) { <span class="badge">@Model.Badge</span> }
    </div>
</section>
```

### Akce 7.3 — Přidej `PageTitle` a `BackUrl` do ViewModelů

Pro každou view s vlastním headerem přidej do příslušného ViewModelu:
```csharp
public string PageTitle { get; init; } = "";
public string? BackUrl { get; init; }
public string? BackLabel { get; init; }
```

Nastav v Controlleru.

### Akce 7.4 — Nahraď custom headery ve Views

Najdi a nahraď:
- `<section class="page-header">...</section>`
- `<section class="page-header page-header-compact">...</section>`
- `<section class="record-editor-page-shell">...</section>`

Za:
```html
@await Html.PartialAsync("_PageHeader", new PageHeaderViewModel { Title = Model.PageTitle, BackUrl = Model.BackUrl, BackLabel = Model.BackLabel })
```

### Akce 7.5 — Sjednoť CSS

V `site.css` ponech jen jednu definici `.page-header`. Odstraň `.page-header-compact`, `.project-header-row`, `.record-editor-page-shell` pokud nejsou použity jinde.

---

## KROK 8 — Sloučení Export services

### Proč

`Services/Export/` má 17 souborů. Každá sekce Word dokumentu má vlastní interface + implementaci. Výsledná ekvivalentní implementace: 3 soubory.

### Akce 8.1 — Identifikuj co sloučit

**Smaž** (logika přejde jako private metody do `OpenXmlWordExportService`):
```
IWordExportHeaderSectionWriter.cs + OpenXmlWordHeaderSectionWriter.cs
IWordExportRecordsSectionWriter.cs + OpenXmlWordRecordsSectionWriter.cs
IWordExportRecordHeaderWriter.cs + OpenXmlWordRecordHeaderWriter.cs
IWordExportRecordCommentsCellWriter.cs + OpenXmlWordRecordCommentsCellWriter.cs
IWordExportRecordDeadlinesCellWriter.cs + OpenXmlWordRecordDeadlinesCellWriter.cs
IWordExportRecordPeopleCellWriter.cs + OpenXmlWordRecordPeopleCellWriter.cs
IWordExportRichHtmlParagraphWriter.cs + OpenXmlWordRichHtmlParagraphWriter.cs
```

**Ponech:**
```
IWordExportService.cs        (public interface — beze změny)
OpenXmlWordExportService.cs  (sloučíš sem vše)
OpenXmlWordElements.cs       (static helper — beze změny)
```

### Akce 8.2 — Přesuň logiku do `OpenXmlWordExportService`

```csharp
public sealed class OpenXmlWordExportService(IRichTextContentService richTextContentService) : IWordExportService
{
    public byte[] BuildDocument(PdfExportTemplateViewModel model) { ... }  // existující

    private void AppendHeader(Body body, ...) { ... }
    private void AppendRecordsSection(Body body, MainDocumentPart mainPart, ...) { ... }
    private void AppendRecord(TableRow row, ...) { ... }
    private void AppendComments(TableCell cell, ...) { ... }
    private void AppendDeadlines(TableCell cell, ...) { ... }
    private void AppendPeople(TableCell cell, ...) { ... }
    private void AppendHtmlParagraphs(TableCell cell, ...) { ... }
}
```

### Akce 8.3 — Aktualizuj DI

```csharp
// ODSTRAŇ všechny section writer registrace
// PONECH:
services.AddScoped<IWordExportService, OpenXmlWordExportService>();
```

### Akce 8.4 — Build

```
dotnet build
```

---

## KROK 9 — Oprava Filters a Middleware

### Akce 9.1 — Oprav `AjaxAntiforgeryResultFilter`

```csharp
// PŘED — reflection hack:
var typeName = result.GetType().Name;
return typeName.Contains("Antiforgery", StringComparison.OrdinalIgnoreCase);

// PO — správné ASP.NET Core API:
return result is IAntiforgeryValidationFailedResult;
```

Přidej `using Microsoft.AspNetCore.Antiforgery;` pokud chybí.

### Akce 9.2 — Odstraň `AjaxResponseContractGuardMiddleware`

Tento middleware bufferuje celé AJAX responses v RAM (`context.Response.Body = new MemoryStream()`).

Smaž soubor `AjaxResponseContractGuardMiddleware.cs`.
Odstraň jeho registraci z `Program.cs`.

### Akce 9.3 — Build

```
dotnet build
```

---

## KROK 10 — Oprava `Program.cs`

### Akce 10.1 — Oprav exception handler

Zkontroluj zda `HomeController` má action `Error`. Pokud nemá:

```csharp
// PŘEPNI na:
app.UseExceptionHandler("/App/Error");
```

V `AppController` přidej:
```csharp
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public IActionResult Error()
{
    return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
    {
        RequestId = HttpContext.TraceIdentifier
    });
}
```

### Akce 10.2 — PONECH `public partial class Program`

Soubor `Program.cs` obsahuje nebo bude obsahovat:
```csharp
public partial class Program;
```

**Nesmazávej.** Tato deklarace je potřeba pro integrační testy (viz Krok 16).

### Akce 10.3 — Zkontroluj dvojí route mapping

Pokud controllery MIX-ují attribute routing a conventional routing — ponech obě registrace.
Pokud všechny controllery používají jen conventional routing — odstraň `app.MapControllers()`.

### Akce 10.4 — Build

```
dotnet build
```

---

## KROK 11 — Sloučení UseCase tříd do doménových Services

### Proč

Po Krocích 1–10 stále existují UseCase třídy v `Services/Data/`. Slučujeme je do 5 doménových service souborů.

### Dependency mapa — před implementací si přečti

```
HarmonogramService       — standalone, žádné cirkulární závislosti
CommentService           — standalone (nezávisí na ostatních services)
MeetingService           → ICommentService
ProjectService           → IHarmonogramService, ICommentService
RecordService            → IRecordEditorQueriesComposition, IRecordWriteCommandsComposition,
                           ICommentService  (NE IProjectService — viz níže)
PeopleService            — standalone
DictionaryService        → IHarmonogramService
```

**Proč RecordService nesmí injektovat IProjectService:**
`RecordService → IProjectService` a zároveň `ProjectService → ICommentService`. Pokud `CommentService` závisí na čemkoliv z RecordService, vznikne circular. Místo toho RecordService injektuje composition interfaces (`IRecordEditorQueriesComposition`, `IRecordWriteCommandsComposition`) — ty jsou implementovány ProjectService, ale RecordService to neví. Circular je přerušen.

### Akce 11.1 — Vytvoř `Services/MeetingService.cs`

Slouč:
- `MeetingDetailQueriesUseCase` + `IMeetingDetailQueriesUseCase`
- `MeetingListQueriesUseCase` + `IMeetingListQueriesUseCase`
- `MeetingWriteCommandsUseCase` + `IMeetingWriteCommandsUseCase`
- `MeetingStatePolicy` (ponech jako `internal static class` v souboru)

```csharp
namespace PmTracker.Web.Services;

public interface IMeetingService
{
    // Všechny metody ze tří UseCase interface s Async suffixem a CancellationToken ct
}

public sealed class MeetingService(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer,
    IPersonIdentityMatcher personIdentityMatcher,
    ICommentService commentService,
    TimeProvider timeProvider) : IMeetingService
{
    // Přesunutá logika ze tří UseCase tříd
}
```

Smaž původní UseCase soubory.

### Akce 11.2 — Vytvoř `Services/ProjectService.cs`

Slouč:
- `ProjectDetailQueriesUseCase` + interface
- `ProjectListQueriesUseCase` + interface
- `ProjectCommandsUseCase` + interface
- `ProjectAssignmentCommandsUseCase` + interface
- `ProjectDataService` (z Kroku 2)

```csharp
public interface IProjectService
{
    // Všechny metody
}

public sealed class ProjectService(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer,
    IPersonIdentityMatcher personIdentityMatcher,
    IHarmonogramService harmonogramService,
    ICommentService commentService,
    TimeProvider timeProvider) : IProjectService, IProjectDetailComposition, IRecordEditorQueriesComposition, IRecordWriteCommandsComposition
```

Smaž původní UseCase soubory + `ProjectDataService.cs`.

### Akce 11.3 — Vytvoř `Services/RecordService.cs`

Slouč:
- `RecordEditorQueriesUseCase` + interface
- `RecordWriteCommandsUseCase` + interface
- `RecordValidationContracts` (ponech jako třídy v souboru)

`ScheduleTimelineCalculator` — ponech v `Services/Schedules/` beze změny.

```csharp
public interface IRecordService
{
    // Všechny metody
}

public sealed class RecordService(
    PmTrackerDbContext dbContext,
    IRichTextContentService richTextContentService,
    ICommentService commentService,
    IRecordEditorQueriesComposition recordEditorQueriesComposition,
    IRecordWriteCommandsComposition recordWriteCommandsComposition,
    TimeProvider timeProvider) : IRecordService
```

**Poznámka:** `IRecordEditorQueriesComposition` a `IRecordWriteCommandsComposition` jsou v DI registrovány jako aliasy na `ProjectService`. RecordService to neví a nemusí — závisí jen na abstrakci.

Smaž původní UseCase soubory.

### Akce 11.4 — Vytvoř `Services/PeopleService.cs`

Slouč:
- `PeoplePageQueriesUseCase` + interface
- `PersonCommandsUseCase` + interface
- Existující `Services/People/PeopleService.cs`

```csharp
public interface IPeopleService
{
    Task<OsobyIndexViewModel> BuildOsobyAsync(CancellationToken ct = default);
    Task<int> SaveManualPersonAsync(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task<int> SaveAdPersonAsync(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeletePersonAsync(DeletePersonCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}

public sealed class PeopleService(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer) : IPeopleService
```

Smaž `Services/People/` složku a UseCase soubory.

### Akce 11.5 — Vytvoř `Services/DictionaryService.cs`

Slouč:
- `DictionariesQueriesUseCase` + interface
- `DictionariesCommandsUseCase` + interface
- Existující `Services/Dictionaries/DictionariesService.cs`
- `DictionarySecurityPolicy` (ponech jako `internal static class` v souboru)

```csharp
public interface IDictionaryService
{
    Task<CiselnikyDashboardViewModel> BuildCiselnikyDashboardAsync(string? id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task<CiselnikDetailViewModel> BuildCiselnikDetailAsync(string id, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveCiselnikRowAsync(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteCiselnikRowAsync(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}

public sealed class DictionaryService(
    PmTrackerDbContext dbContext,
    IHarmonogramService harmonogramService) : IDictionaryService
```

Smaž `Services/Dictionaries/` složku a UseCase soubory.

### Akce 11.6 — Aktualizuj controllery

```
JednaniController     → IMeetingService
ProjektyController    → IProjectService, IMeetingService
ZaznamyController     → IRecordService, IProjectService
OsobyController       → IPeopleService
CiselnikyController   → IDictionaryService
```

### Akce 11.7 — Aktualizuj DI registrace

V `DataStoreServiceCollectionExtensions.cs` odstraň všechny UseCase registrace a nahraď:

```csharp
// Registruj concrete typy
services.AddScoped<HarmonogramService>();
services.AddScoped<CommentService>();
services.AddScoped<MeetingService>();
services.AddScoped<ProjectService>();
services.AddScoped<RecordService>();
services.AddScoped<PeopleService>();
services.AddScoped<DictionaryService>();

// Interface aliases — přímý odkaz na concrete (bez as-cast)
services.AddScoped<IHarmonogramService>(sp => sp.GetRequiredService<HarmonogramService>());
services.AddScoped<ICommentService>(sp => sp.GetRequiredService<CommentService>());
services.AddScoped<IMeetingService>(sp => sp.GetRequiredService<MeetingService>());
services.AddScoped<IProjectService>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordService>(sp => sp.GetRequiredService<RecordService>());
services.AddScoped<IPeopleService>(sp => sp.GetRequiredService<PeopleService>());
services.AddScoped<IDictionaryService>(sp => sp.GetRequiredService<DictionaryService>());

// Composition interfaces → ProjectService (jedna instance per request, sdílená)
services.AddScoped<IProjectDetailComposition>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordEditorQueriesComposition>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordWriteCommandsComposition>(sp => sp.GetRequiredService<ProjectService>());
```

Ponech:
```csharp
services.AddScoped<IUserContextResolver, UserContextResolver>();
services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
services.AddScoped<ITextNormalizer, TextNormalizer>();
services.AddScoped<IPersonIdentityMatcher, PersonIdentityMatcher>();
services.AddScoped<IPermissionEvaluationService, PermissionEvaluationService>();
services.AddScoped<ICommentAuthorizationPolicy, CommentAuthorizationPolicy>();
services.AddScoped<IRichTextContentService, RichTextContentService>();
services.AddScoped<IWordExportService, OpenXmlWordExportService>();
services.AddHostedService<SqlStartupValidatorHostedService>();
// SettingsAuthz services ponech
```

### Akce 11.8 — Build

```
dotnet build
```

### Akce 11.9 — Integrační smoke testy

Po úspěšném buildu vytvoř projekt `PmTracker.Web.Tests` (pokud neexistuje) a přidej základní smoke testy:

```csharp
// Tests/IntegrationTests/ProjectServiceTests.cs
public class ProjectServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetProjektyList_ReturnsSuccessfully()
    {
        // Arrange: authenticated HTTP client
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/Projekty");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetJednaniList_ReturnsSuccessfully()
    {
        var response = await _client.GetAsync("/Jednani");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

Účel: zachytit regrese při dalších krocích. Není potřeba 100% pokrytí — stačí hlavní happy paths.

---

## KROK 12 — Reorganizace `DbContext` konfigurace

### Proč

`PmTrackerDbContext.OnModelCreating()` konfiguruje 44+ entit v jedné metodě.

### Akce 12.1 — Vytvoř `Data/Configuration/` s IEntityTypeConfiguration<T> třídami

| Soubor | Konfiguruje |
|---|---|
| `ProjectEntityConfiguration.cs` | ProjektEntity, ObsazeniProjektuEntity, ProjektSubsystemEntity, ObsazeniSubsystemuProjektuEntity |
| `MeetingEntityConfiguration.cs` | JednaniEntity, UcastEntity, VyjadreniEntity |
| `RecordEntityConfiguration.cs` | ProjektovyZaznamEntity, ZaznamHarmonogramHodnotaEntity, ZaznamHistorie* entity, ZaznamExterniOdkazEntity, ZaznamSpolupraceEntity |
| `LookupEntityConfiguration.cs` | Ciselnik* entity, SubsystemEntity, HarmonogramSablonaEntity, HarmonogramTypEntity |
| `AuthorizationEntityConfiguration.cs` | Authz* entity, AuthzAuditLogEntity |
| `PersonEntityConfiguration.cs` | OsobaEntity |

### Akce 12.2 — Aktualizuj `OnModelCreating`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(PmTrackerDbContext).Assembly);
}
```

Přesně zkopíruj konfiguraci každé entity — neměň table names, column names, FK konfigurace.

### Akce 12.3 — Build a ověř prázdnou migrací

```
dotnet build
dotnet ef migrations add TestConfig --no-build
```

Pokud migrace je prázdná — konfigurace je identická. Smaž ji:
```
dotnet ef migrations remove
```

Pokud migrace obsahuje změny — zastavit se a zkontrolovat co se liší.

---

## KROK 13 — Rozdělení `site.js` na ES moduly

### Proč

9 016 řádků v jednom IIFE souboru. Prohlížeč parsuje vše při každém načtení.

### Akce 13.1 — Aktualizuj `_Layout.cshtml`

```html
<!-- PŘED: -->
<script src="~/js/site.js" asp-append-version="true"></script>

<!-- PO: -->
<script type="module" src="~/js/site.js" asp-append-version="true"></script>
```

### Akce 13.2 — Vytvoř složku `wwwroot/js/modules/`

### Akce 13.3 — Vytvoř `modules/utils.js`

Přesuň čisté utility (žádný DOM, žádný state):

```javascript
export const msPerDay = 24 * 60 * 60 * 1000;
export const dateMonths = ['Leden', 'Únor', /* ... */];

export function debounce(callback, waitMs) { /* ... */ }
export function normalizeFilterText(value) { /* ... */ }
export function normalizeFilterToken(value) { /* ... */ }
export function normalizeSearchText(value) { /* ... */ }
export function scoreSearchCandidate(query, haystack) { /* ... */ }
export function containsWordPrefix(text, token) { /* ... */ }
export function parseIsoDate(value) { /* ... */ }
export function parseDisplayDate(value) { /* ... */ }
export function parseIsoDateTime(value) { /* ... */ }
export function parseTimeValue(value) { /* ... */ }
export function formatIsoDate(date) { /* ... */ }
export function formatDisplayDate(date) { /* ... */ }
export function formatTime(hours, minutes) { /* ... */ }
export function diffCalendarDays(a, b) { /* ... */ }
export function addCalendarDays(baseDate, dayCount) { /* ... */ }
export function isSameCalendarDate(a, b) { /* ... */ }
export function toUtcDayStamp(date) { /* ... */ }
export function parseColorChannels(value) { /* ... */ }
export function getContrastTextColor(backgroundColor) { /* ... */ }
export function measureTextWidth(text, fontSpec) { /* ... */ }
export function pickSegmentLabel(fullLabel, shortLabel, availableWidthPx, fontSpec) { /* ... */ }
export function parseJsonPayload(rawText) { /* ... */ }
export function isPlainObject(value) { /* ... */ }
export function truncateDiagnosticBody(value, maxLength) { /* ... */ }
```

### Akce 13.4 — Vytvoř `modules/theme.js`

```javascript
const themeStorageKey = 'pmtracker.theme.mode';
const mediaDark = window.matchMedia('(prefers-color-scheme: dark)');

export function initTheme() { /* tělo initTheme() ze site.js */ }
// interní: getStoredThemeMode, resolveEffectiveTheme, setTheme
```

### Akce 13.5 — Vytvoř `modules/session.js`

```javascript
export const sessionState = {
    intervalId: 0,
    inFlightPromise: null,
    stale: false,
    consecutiveFailures: 0,
    lastSuccessUtc: '',
    lastTraceId: '',
    lastFailureReason: ''
};

export function initSessionCoordinator() { /* ... */ }
export async function ensureSessionKeepAlive(source, force) { /* ... */ }
export function updateRequestVerificationTokens(nextToken) { /* ... */ }
export function setSessionStaleState(stale, reason) { /* ... */ }
export function hasAjaxSubmitFormsInDom() { /* ... */ }
```

### Akce 13.6 — Vytvoř `modules/modals.js`

```javascript
export const modalState = { lastTrigger: null };

export function setModalContent(content, trigger) { /* ... */ }
export function closeModal() { /* ... */ }
export async function openUrlModal(url, trigger) { /* ... */ }
export function isModalOpen() { /* ... */ }
export function focusInitialModalElement() { /* ... */ }
export function getActiveModalContainer() { /* ... */ }
export function getFocusableElementsWithinModal(container) { /* ... */ }
```

### Akce 13.7 — Vytvoř `modules/ui.js`

```javascript
import { measureTextWidth, parseColorChannels, getContrastTextColor, pickSegmentLabel, debounce } from './utils.js';

export const floatingPanelRegistry = new Set();

export function mountFloatingPanel(panel, anchor, options) { /* ... */ }
export function unmountFloatingPanel(panel) { /* ... */ }
export function closeAllFloatingPanels(scope, exceptPanel) { /* ... */ }
export function positionFloatingPanel(panel, anchor, options) { /* ... */ }
export function repositionFloatingPanels() { /* ... */ }
export function getFloatingLayerRoot(container) { /* ... */ }
export function renderRainbowSegmentLabel(segment) { /* ... */ }
export function renderAllRainbowSegmentLabels(scope) { /* ... */ }
export function queueRainbowSegmentRender(scope) { /* ... */ }
export function applyProjectIndexFilters(scope) { /* ... */ }
export function initProjectIndexStatusFilters(scope) { /* ... */ }
export function syncProjectListStatusFilterInputs(shell) { /* ... */ }
export function initUserMenu() { /* ... */ }

export const printState = { popover: null, trigger: null, hoverTimerId: 0 };
export function initPrintFormatChooser() { /* ... */ }
export function refreshRecordEditorPreferenceUi() { /* ... */ }
export function getStoredPrintFormat() { /* ... */ }
```

### Akce 13.8 — Vytvoř `modules/pickers.js`

```javascript
import { mountFloatingPanel, unmountFloatingPanel, closeAllFloatingPanels, positionFloatingPanel } from './ui.js';
import { parseIsoDate, formatIsoDate, formatDisplayDate, parseTimeValue, formatTime,
         isSameCalendarDate, dateMonths, normalizeSearchText, scoreSearchCandidate,
         debounce, isPlainObject } from './utils.js';

export function initCustomDatePickers(scope) { /* ... */ }
export function initCustomTimePickers(scope) { /* ... */ }
export function initSinglePersonPickers(scope) { /* ... */ }
export function initAdPersonPickers(scope) { /* ... */ }
export function initCollabPickers(scope) { /* ... */ }
export function closeAllDatePanels(exceptField) { /* ... */ }
export function closeAllTimePanels(exceptField) { /* ... */ }
export function setAppDateFieldValue(valueInput, isoValue) { /* ... */ }
```

### Akce 13.9 — Vytvoř `modules/filters.js`

```javascript
import { normalizeFilterToken, normalizeSearchText, scoreSearchCandidate, debounce } from './utils.js';

export function initProjectRecordsUi() { /* ... */ }
export function applyProjectRecordFilters() { /* ... */ }
export function restoreFilterState() { /* ... */ }
export function persistFilterState() { /* ... */ }
export function setFilterPanelOpen(open) { /* ... */ }
export function getProjectFilterConfig(scope) { /* ... */ }
export function buildProjectFilterStateFromInputs(scope) { /* ... */ }
export function renderProjectFilterChips(scope) { /* ... */ }
export function handleProjectFilterInputChange(scope) { /* ... */ }
export function restoreProjectFilterScope(scope) { /* ... */ }
export function saveProjectFilterDefaults(scope) { /* ... */ }
export function clearProjectFilterInput(scope, inputKey) { /* ... */ }
export function clearProjectFilterPreferenceStorage() { /* ... */ }
export function applyRecordsView(view) { /* ... */ }
export function scheduleSubsystemIndicatorSync() { /* ... */ }
export function initSubsystemScrollIndicator() { /* ... */ }
export function setActiveTab(tabName) { /* ... */ }
export function syncTabQuery(tabName) { /* ... */ }
```

### Akce 13.10 — Vytvoř `modules/schedule.js`

```javascript
import { parseIsoDate, formatIsoDate, formatDisplayDate, diffCalendarDays, addCalendarDays,
         toUtcDayStamp, msPerDay, debounce } from './utils.js';
import { mountFloatingPanel, queueRainbowSegmentRender } from './ui.js';
import { setAppDateFieldValue } from './pickers.js';

export class ScheduleTimelineEngine { /* ... */ }
export class RecordSchedulePlanner { /* ... */ }

export function initProjectScheduleUi() { /* ... */ }
export function applyProjectScheduleFilters() { /* ... */ }
export function restoreScheduleFilterState() { /* ... */ }
export function renderStaticTimelineAxes(scope) { /* ... */ }
export function renderTimelineAxis(container, startDate, endDate, options) { /* ... */ }
export function queueRecordSchedulePlannerRecalc(form, attempt) { /* ... */ }
export function initRecordSchedulePlanner(scope) { /* ... */ }
export function initScheduleExpandUi(scope) { /* ... */ }
```

### Akce 13.11 — Vytvoř `modules/comments.js`

```javascript
const commentSortDirectionStorageKey = 'pmtracker.comments.sortDirection';

export function initCommentSortUi(scope) { /* ... */ }
export function applyCommentSort(section, direction) { /* ... */ }
export function applyCommentSortToAllSections(direction, scope) { /* ... */ }
export function setCommentSortButtonLabel(button, direction) { /* ... */ }
export function getStoredCommentSortDirection() { /* ... */ }
export function setStoredCommentSortDirection(direction) { /* ... */ }
```

### Akce 13.12 — Vytvoř `modules/ajax.js`

```javascript
import { modalState, closeModal, getActiveModalContainer } from './modals.js';
import { sessionState, ensureSessionKeepAlive, setSessionStaleState, hasAjaxSubmitFormsInDom } from './session.js';
import { isPlainObject, parseJsonPayload, truncateDiagnosticBody } from './utils.js';

export function initModalAjaxSubmit() { /* ... */ }
export function renderModalFormErrors(form, payload) { /* ... */ }
export function clearModalFormErrors(form) { /* ... */ }
export function setFormSubmitting(form, submitting) { /* ... */ }
export function validateRequiredPersonPickers(form) { /* ... */ }
```

### Akce 13.13 — Vytvoř `modules/recordEditor.js`

```javascript
import { modalState, closeModal, isModalOpen, openUrlModal } from './modals.js';
import { parseJsonPayload, debounce, isPlainObject } from './utils.js';
import { initCustomDatePickers, initCustomTimePickers, setAppDateFieldValue } from './pickers.js';
import { initRecordSchedulePlanner, queueRecordSchedulePlannerRecalc } from './schedule.js';
import { renderModalFormErrors, clearModalFormErrors } from './ajax.js';
import { queueRainbowSegmentRender } from './ui.js';

export const recordEditorState = { chooser: null, chooserTrigger: null, closeGuard: null, closeGuardTrigger: null };

export function initRecordFormEnhancements(scope) { /* ... */ }
export function setRecordFormTab(form, tabKey) { /* ... */ }
export function updateTaskTypeVisibility(categorySelect) { /* ... */ }
export function initRichTextEditors(scope) { /* ... */ }
export function openRecordEditor(trigger, forcedMode) { /* ... */ }
export function getStoredRecordEditorPreference() { /* ... */ }
export function setStoredRecordEditorPreference(mode) { /* ... */ }
export function clearStoredRecordEditorPreference() { /* ... */ }
export function restoreRecordEditorReturnStateFromUrl() { /* ... */ }
export function initRecordEditorDirtyTracking(scope) { /* ... */ }
export function isRecordEditorFormDirty(form) { /* ... */ }
export function markRecordEditorFormClean(form) { /* ... */ }
export function prepareRecordEditorFormNavigation(form) { /* ... */ }
export async function promptRecordEditorDiscard(form, trigger) { /* ... */ }
export async function requestRecordEditorModalClose(trigger) { /* ... */ }
export async function requestRecordEditorPageCancel(trigger) { /* ... */ }
export function clearRecordEditorDraft(form) { /* ... */ }
export function saveRecordEditorDraft(form) { /* ... */ }
export function maybeRestoreRecordEditorDraft(form) { /* ... */ }
```

### Akce 13.14 — Vytvoř `modules/navigation.js`

```javascript
import { setActiveTab, syncTabQuery } from './filters.js';
import { applyCommentSortToAllSections } from './comments.js';
import { initRecordFormEnhancements } from './recordEditor.js';
import { renderStaticTimelineAxes, initRecordSchedulePlanner } from './schedule.js';
import { queueRainbowSegmentRender, closeAllFloatingPanels } from './ui.js';

export function initProjectTabs() { /* ... */ }
export function initCiselnikAjaxSwitch() { /* ... */ }
export function initSettingsAjaxSwitch() { /* ... */ }
export function initProfileRightsFilter() { /* ... */ }
export function initPermissionMetadataBindings(scope) { /* ... */ }
export async function refreshPageScope(payload) { /* ... */ }
export function buildRecordUiState(scopeRoot) { /* ... */ }
export function restoreRecordUiState(state) { /* ... */ }
export function initProjectRecordPageshowSync() { /* ... */ }
```

### Akce 13.15 — Přepiš `site.js` jako entry point (<80 řádků)

```javascript
import { initTheme } from './modules/theme.js';
import { initUserMenu, initPrintFormatChooser, refreshRecordEditorPreferenceUi,
         applyProjectIndexFilters, initProjectIndexStatusFilters,
         repositionFloatingPanels, renderAllRainbowSegmentLabels,
         closeAllFloatingPanels } from './modules/ui.js';
import { initSessionCoordinator } from './modules/session.js';
import { initModalAjaxSubmit } from './modules/ajax.js';
import { initProjectTabs, initCiselnikAjaxSwitch, initSettingsAjaxSwitch,
         initProfileRightsFilter, initPermissionMetadataBindings,
         restoreRecordEditorReturnStateFromUrl, initProjectRecordPageshowSync,
         refreshPageScope } from './modules/navigation.js';
import { initProjectRecordsUi, initSubsystemScrollIndicator,
         handleProjectFilterInputChange, persistFilterState } from './modules/filters.js';
import { initProjectScheduleUi } from './modules/schedule.js';
import { initCommentSortUi } from './modules/comments.js';
import { initRecordFormEnhancements, openRecordEditor, requestRecordEditorModalClose,
         requestRecordEditorPageCancel, isRecordEditorFormDirty,
         prepareRecordEditorFormNavigation } from './modules/recordEditor.js';
import { modalState, closeModal, isModalOpen } from './modules/modals.js';
import { debounce } from './modules/utils.js';

// ── Inicializace ──────────────────────────────────────
initTheme();
initUserMenu();
initPrintFormatChooser();
refreshRecordEditorPreferenceUi();
initProjectTabs();
initProjectRecordsUi();
initProjectScheduleUi();
initProjectRecordPageshowSync();
initCommentSortUi(document);
restoreRecordEditorReturnStateFromUrl();
initProfileRightsFilter();
initCiselnikAjaxSwitch();
initSettingsAjaxSwitch();
initRecordFormEnhancements(document);
initPermissionMetadataBindings(document);
initProjectIndexStatusFilters(document);
initSessionCoordinator();
initModalAjaxSubmit();

// ── Delegated event listeners ──────────────────────────
// Přesuň sem document.addEventListener('click', ...) ze starého site.js
// Přesuň sem document.addEventListener('keydown', ...)
// Každý listener VOLÁ importované funkce — neobsahuje logiku inline.

// ── Resize/scroll listeners ───────────────────────────
const rerenderRainbowLabelsOnResize = debounce(() => renderAllRainbowSegmentLabels(document), 120);
window.addEventListener('scroll', repositionFloatingPanels, { passive: true });
window.addEventListener('resize', repositionFloatingPanels);
window.addEventListener('resize', rerenderRainbowLabelsOnResize);
```

### Akce 13.16 — Ověř v prohlížeči

1. DevTools → Console → žádné `Uncaught SyntaxError`, `Cannot resolve module`
2. Network tab → každý `.js` soubor v `modules/` vrací 200
3. Otestuj: dark mode toggle, modal, filtrování záznamů, harmonogram expand

---

## KROK 14 — Číselníky: dynamické položky a navigace

### Akce 14.1 — Identifikuj statické vs dynamické číselníky

**Statické (neměnit v UI — kód je na ně napojený):**
- `stavy-projektu` — kód referenčuje `"PLAN"`, `"DELETED"`
- `stavy-ukolu` — kód referenčuje `IsFinal`
- `stavy-jednani` — kód referenčuje `"CLOSED"`, `"OPEN"`
- `stavy-ucasti` — kód referenčuje `"PRESENT"`
- `kategorie-zaznamu` — kód rozlišuje `"U"` / `"UKOL"`
- `typy-ukolu` — kód s nimi pracuje
- `harmonogram-kroky` — technická konfigurace

**Dynamické (smysl mít v DB a UI):**
- `organizace`, `organizacni-celky`, `subsystemy`
- `typy-externich-odkazu`
- `vyzvy` (každý rok přibývají)
- `role-projektu`, `role-subsystemu`

### Akce 14.2 — Ponech odkaz Číselníky v hlavní navigaci

V `_Layout.cshtml` ponech odkaz `Číselníky` v hlavní navigaci.
V `Views/Nastaveni/` nepřidávej shortcut link na `Číselníky` (není to sekce Nastavení).

V `DictionaryService` odfiltruj statické číselníky z `BuildCiselnikyDashboard` — zobrazuj jen dynamické.

---

## KROK 15 — Přesun konfigurace oprávnění z DB do seed

### Proč

UI Nastavení umožňuje editovat klíče akcí (`projects.edit`), role a mapování. Přejmenování klíče v UI = všichni uživatelé ztratí oprávnění bez chybové hlášky. Toto je konfigurace aplikace, ne uživatelská data.

### Akce 15.1 — Vytvoř `Services/Security/PermissionSeedConfiguration.cs`

```csharp
namespace PmTracker.Web.Services.Security;

public sealed record RoleSeedItem(string Kod, string Nazev, bool IsSystem);
public sealed record ActionSeedItem(string Klic, string Nazev, string ScopeLevel);
public sealed record RoleActionSeedItem(string RoleKod, string ActionKlic, string ScopeMode);

public static class PermissionSeedConfiguration
{
    public static readonly IReadOnlyList<RoleSeedItem> Roles = [ /* čti z DB tabulky AuthzRoles */ ];
    public static readonly IReadOnlyList<ActionSeedItem> Actions = [ /* čti z PermissionKeys.cs + AuthzAkce */ ];
    public static readonly IReadOnlyList<RoleActionSeedItem> RoleMappings = [ /* čti z AuthzRoleAkce */ ];
}
```

**Důležité:** Hodnoty čti přímo z DB tabulek. Musí být identické.

### Akce 15.2 — Vytvoř `Services/Security/PermissionSeeder.cs`

```csharp
public sealed class PermissionSeeder(PmTrackerDbContext db)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in PermissionSeedConfiguration.Roles)
        {
            var existing = await db.AuthzRoles.FirstOrDefaultAsync(x => x.Kod == role.Kod, ct);
            if (existing is null)
                db.AuthzRoles.Add(new AuthzRoleEntity { Kod = role.Kod, Nazev = role.Nazev, IsSystem = role.IsSystem, IsActive = true });
            else
            {
                existing.Nazev = role.Nazev;
                existing.IsSystem = role.IsSystem;
            }
        }

        foreach (var action in PermissionSeedConfiguration.Actions)
        {
            var existing = await db.AuthzPermissions.FirstOrDefaultAsync(x => x.Klic == action.Klic, ct);
            if (existing is null)
                db.AuthzPermissions.Add(new AuthzPermissionEntity { Klic = action.Klic, Nazev = action.Nazev, ScopeLevel = action.ScopeLevel, IsSystem = true, IsActive = true });
            else
                existing.Nazev = action.Nazev;
        }

        await db.SaveChangesAsync(ct);

        // Upsert RoleMappings — stejný vzor po uložení rolí a akcí
        await db.SaveChangesAsync(ct);
    }
}
```

Seeder je **idempotentní** — spuštění vícekrát dá stejný výsledek.

### Akce 15.3 — Spusť seeder při startu

```csharp
// Program.cs — po app.Build(), před app.Run():
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<PermissionSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}
```

Registruj:
```csharp
services.AddScoped<PermissionSeeder>();
```

### Akce 15.4 — Ponech admin sekce v Nastavení

V `NastaveniController` ponech aktivní sekce i endpointy pro:
- `role`
- `akce`
- `role-akce`
- `uzivatele-role`
- `efektivni-prava`

V `Views/Nastaveni/` ponech viditelné sekce:
- `role`
- `akce`
- `role-akce`
- `uzivatele-role`
- `efektivni-prava`

### Akce 15.5 — Build

```
dotnet build
```

---

## KROK 16 — Závěrečné ověření

### Akce 16.1 — Build check

```
dotnet build
```

Nesmí být žádné `error`. `CA1416` warnings z `ADConnector.cs` jsou akceptovatelné.

### Akce 16.2 — Spuštění aplikace

```
dotnet run
```

Aplikace nastartuje bez exception. `SqlStartupValidatorHostedService` proběhne úspěšně.

### Akce 16.3 — Funkční checklist

| Test | Očekávaný výsledek |
|---|---|
| `GET /Projekty` | HTTP 200, seznam projektů |
| `GET /Projekty/Detail/{id}` | HTTP 200, detail se záložkami |
| `GET /Jednani` | HTTP 200 |
| `GET /Zaznamy/Create?projektId={id}` | Formulář pro nový záznam |
| `POST /Zaznamy/Save` (AJAX) | `{ ok: true }` JSON |
| `GET /Export/Projekt/{id}/Word` | `.docx` soubor |
| `GET /App/KeepAlive` | `{ ok: true, requestVerificationToken: "..." }` |
| Otevření modálního okna | Modal se otevře, focus trap funguje |
| Dark mode toggle | Téma se přepne a uloží |
| Browser console | Žádné JS chyby |

### Akce 16.4 — Strukturální checklist

```
[ ] Složka Modules/ neexistuje
[ ] SqlServerDataStore.cs neexistuje
[ ] IPmTrackerDataStore.cs neexistuje
[ ] Services/Records/Commands/ neexistuje
[ ] Services/Records/Queries/ neexistuje
[ ] Services/People/PeopleDataStore.cs neexistuje
[ ] Services/Dictionaries/DictionariesDataStore.cs neexistuje
[ ] Services/Export/ má maximálně 3 soubory
[ ] wwwroot/js/modules/ existuje a obsahuje 13 .js souborů
[ ] wwwroot/js/site.js má méně než 80 řádků
[ ] _Layout.cshtml má <script type="module" src="~/js/site.js">
[ ] Žádný controller neinjektuje IPmTrackerDataStore
[ ] Všechny service metody mají Async suffix a CancellationToken ct parametr
[ ] Žádné .ToList() bez await v service třídách
[ ] AjaxResponseContractGuardMiddleware neexistuje
[ ] PermissionSeedConfiguration.cs existuje
[ ] PermissionSeeder se spouští při startu
[ ] public partial class Program v Program.cs EXISTUJE
[ ] PmTracker.Web.Tests projekt existuje s alespoň 2 integrační testy
[ ] Integrační testy jsou zelené
```

### Akce 16.5 — Spusť testy

```
dotnet test
```

Všechny testy zelené.
