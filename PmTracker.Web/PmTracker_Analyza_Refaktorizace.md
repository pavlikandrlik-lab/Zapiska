# PmTracker.Web — Kompletní analýza stavu refaktorizace

> Dokument obsahuje: co bylo správně opraveno, co zůstalo rozbité,
> nové problémy nalezené při průzkumu a přesné Codex prompty ke zkopírování.

---

## ČÁST 1 — EXECUTIVE SUMMARY

Refaktorizace odstranila největší architektonické problémy (Modules/, God class,
Service Locator, middleware buffering). Výsledek je výrazně čistší než výchozí stav.

**Kde to ale selhalo:**

1. `UserContextResolver` načítá **celou tabulku projektů** při každém HTTP requestu
   (každý klik uživatele). Toto je závažnější problém než původní 10-vrstvý call chain.
2. `ProjektyController` a `ZaznamyController` stále over-fetchují pro modály/partial views.
3. V 11 View souborech stále existuje business logika (string.Equals, LINQ, Url.Action).
4. `ProjectService` stále implementuje 3 zbytečné composition interfaces.
5. `Program.cs` má mrtvý kód a dvojí routing.
6. Žádné bezpečnostní hlavičky (CSP, X-Frame-Options).
7. Dev credentials natvrdo v `appsettings.Development.json`.

Původní odhad „193 synchronních .ToList() volání" byl špatně — většina z nich jsou
správné in-memory LINQ operace na již načtených datech. Skutečný async stav je dobrý.

---

## ČÁST 2 — CO BYLO SPRÁVNĚ OPRAVENO

### ✅ Architektura — velké problémy odstraněny

| Problém | Stav | Důkaz |
|---------|------|-------|
| 10-vrstvý call chain (Modules/) | ✅ Odstraněn | `Modules/` adresář neexistuje |
| God class SqlServerDataStore | ✅ Odstraněn | Soubor neexistuje |
| Services konsolidovány | ✅ Hotovo | `MeetingService`, `ProjectService`, `RecordService` + partial soubory |
| DbContext IEntityTypeConfiguration | ✅ Hotovo | `Data/Configuration/` — 6 souborů |
| Export 46 souborů → 3 soubory | ✅ Hotovo | `Services/Export/`: `ExportTemplateQueries.cs`, `ExportTemplateUseCase.cs`, `OpenXmlWordExportService.cs` |

### ✅ Async/await — opraveno správně

Původní odhad „193 synchronních volání" byl nepřesný. Průzkum ukázal:

- `HarmonogramService.cs` — **0** DB-facing `.ToList()` volání, všechna jsou in-memory ✅
- `MeetingService.DetailQueries.cs` — 13 DB volání jsou `.ToListAsync()`, 19× `.ToList()` jsou in-memory ✅
- `RecordService.WriteCommands.cs` — správný vzor: async pro DB, sync pro in-memory ✅
- `ExportTemplateQueries.cs` — používá `.FirstOrDefaultAsync()` správně ✅
- `ProjectService.RecordComposition.cs` — 8 fází načítání dat, všechny DB-facing jsou async ✅
- `Controllers/` — 64 async metod napříč všemi controllery ✅
- `.SaveChanges()` → `.SaveChangesAsync()` — **0 synchronních save volání** ✅

Vzor je správný: `await dbContext.X.ToListAsync(ct)` → pak `.ToList()` na in-memory kolekci.

### ✅ N+1 opravena v JednaniController (Prompt 4)

Všechny tři opravy z Promptu 4 jsou **implementovány a ověřeny**:

```csharp
// JednaniController.SaveStatus (line 153):
var projektId = await _meetingService.GetMeetingProjectIdAsync(command.JednaniId, ct);
// ✅ Dříve: BuildJednaniDetailAsync() — načítalo vše

// JednaniController.AddMeetingParticipantModal (line 132):
var actualProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
// ✅ Dříve: BuildJednaniDetailAsync() — načítalo vše

// JednaniController.TaskItemPartial (line 80):
var ukol = await _meetingService.GetSingleTaskAsync(jednaniId, zaznamId, ct);
// ✅ Dříve: BuildJednaniDetailAsync() — načítalo vše
```

### ✅ Service Locator odstraněn z BaseController

```csharp
// BaseController konstruktor — správné DI:
protected BaseController(
    IUserContextResolver userContextResolver,
    TimeProvider timeProvider,       // ✅ konstruktor, ne HttpContext.RequestServices
    ILoggerFactory loggerFactory)    // ✅ konstruktor, ne HttpContext.RequestServices
```

`ExecuteValidatedCommandAsync` — plně async, 2 overloady (sync/async permission check).

### ✅ Views — velké opravy provedeny

| Problém | Stav |
|---------|------|
| ViewBag v Controllers | ✅ 0 výskytů |
| ViewBag v Views | ✅ 0 výskytů |
| BaseViewModel vytvořen | ✅ `Models/ViewModels/BaseViewModel.cs` |
| NavPermissionsViewModel v Layout | ✅ Správný vzor místo inline logiky |
| Gantt výpočty v Razor | ✅ Přesunuto do `ProjektHarmonogramUkolViewModel` (line 154-159) |
| PDF template LINQ | ✅ Odstraněn — šablona je čistý HTML template |

### ✅ Middleware a Filters

| Problém | Stav |
|---------|------|
| AjaxResponseContractGuardMiddleware buffering | ✅ Middleware kompletně odstraněn |
| AjaxAntiforgeryFilter reflection hack | ✅ Používá `IAntiforgeryValidationFailedResult` |
| X-Trace-Id header | ✅ Přesunuto do inline middleware v Program.cs |

### ✅ JavaScript

| Problém | Stav |
|---------|------|
| site.js 9 016 řádků | ✅ 3 řádky (jen import + init) |
| wwwroot/js/modules/ | ✅ 13 modulů (plan: 8, vzniklo navíc: ajax.js, bootstrap.js, navigation.js, pickers.js, ui.js, utils.js) |

### ✅ Infrastruktura

- **Error handling**: `HomeController` **MÁ** `Error()` action — `app.UseExceptionHandler("/Home/Error")` je správně ✅
- **Connection string**: Jedna, konzistentní přes prostředí, žádné duplicity ✅
- **DbContext**: Jedna registrace, SqlServer only ✅

---

## ČÁST 3 — CO NEBYLO OPRAVENO (ZBÝVAJÍCÍ PROBLÉMY)

### ❌ KRITICKÉ — UserContextResolver: SELECT * na každý request

**Soubor:** `Services/Security/UserContextResolver.cs`

Toto je **nově nalezený problém** který v původním dokumentu nebyl identifikován.
`UserContextResolver` se volá při každém HTTP requestu (každý klik uživatele).

```csharp
// Line 282 — načte VŠECHNY projekty bez filtru:
var allProjects = await dbContext.Projekty
    .ToListAsync(ct);
// Pak v paměti hledá smazané. 50 projektů? Načte 50.
// 500 projektů? Načte 500. Každý request. Každý uživatel.

// Line 345 — načte VŠECHNY uživatele (v dev "asUser" funkci):
var candidates = await dbContext.Osoby
    .Select(...)
    .ToListAsync(ct);
// Pak v paměti fuzzy-matchuje login.

// Line 116 — načte uživatele matchující login pattern (může být bulk):
var people = await dbContext.Osoby
    .Where(...)
    .ToListAsync(ct);
```

**Dopad**: Každý stisk klávesy v UI = SELECT * FROM Projekty + komplex JOIN queries pro permissions.
Při 200 projektech a 10 souběžných uživatelích = 10 zbytečných full-table scans paralelně.

### ❌ Over-fetching — ProjektyController

**Soubor:** `Controllers/ProjektyController.cs`

```csharp
// EditProjectModal line 113:
var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);
//                                   ^^^^^^^^^^^^^^^^^^^^^^^^^^
// Načte VŠECHNY projekty z DB, pak v C# paměti hledá jeden. Potřebuje jen
// Id, Nazev, Zkratka, Stav, StavKod, PouzivatIdentJednani.

// DeleteProjectModal line 150:
var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);
// Totéž. Potřebuje jen Id, Nazev, Zkratka, Stav.
```

`IProjectService` nemá metodu `GetProjectByIdAsync(int id)` — chybí.

### ❌ Over-fetching — ZaznamyController

**Soubor:** `Controllers/ZaznamyController.cs`

```csharp
// RecordCardPartial lines 208-209:
var model = await _recordService.BuildProjektDetailAsync(projektId, ct);
var record = model.Zaznamy.FirstOrDefault(x => x.Id == zaznamId);
```

`BuildProjektDetailAsync` → `BuildRecordCardsForProjectAsync` načítá data ve **8 fázích**:
1. Všechny záznamy projektu
2. Všechny lookup tabulky (kategorie, typy úkolů, stavy, subsystémy)
3. Historie vlastníků pro všechny záznamy
4. Historie termínů pro všechny záznamy
5. Historie subsystémů pro všechny záznamy
6. Komentáře pro všechny záznamy
7. Osoby referencované z komentářů
8. Jednání referencovaná z komentářů

Pro zobrazení **jedné kartičky záznamu** po uložení.

`IRecordService` nemá metodu `GetRecordByIdAsync(int projektId, int zaznamId)` — chybí.

Line 234 navíc čte `model.OtevrenaJednani` — otevřená jednání pro projekt.
Nová metoda musí vrátit i tuto kolekci (nebo mít samostatnou `GetOpenMeetingOptionsAsync`).

### ❌ Program.cs — 2 zbývající problémy

```csharp
// Problém 1 — line 51+53: dvojí route registrace
app.MapControllers();           // pro attribute routing ([Route(...)], [HttpGet("path")])
app.MapControllerRoute(         // pro conventional routing
    name: "default",
    pattern: "{controller=Projekty}/{action=Index}/{id?}");
// Controllery používají conventional routing → app.MapControllers() je zbytečné.

// Problém 2 — line 59: mrtvý kód
public partial class Program;
// Existuje pouze pro WebApplicationFactory v integračních testech.
// V projektu NEJSOU žádné testy.
```

*(Poznámka: `app.UseExceptionHandler("/Home/Error")` je SPRÁVNĚ — HomeController má Error action)*

### ❌ Composition interfaces — zbytky po refaktoru

**Soubory:** `Services/ProjectService.cs`, `Services/Data/DataStoreServiceCollectionExtensions.cs`

```csharp
// ProjectService.cs lines 9-13 — 3 rozhraní navíc:
public sealed partial class ProjectService :
    IProjectService,              // ✅ správně
    IProjectDetailComposition,    // ❌ zbytečné — mělo být smazáno v Promptu 2
    IRecordEditorQueriesComposition,  // ❌ zbytečné
    IRecordWriteCommandsComposition   // ❌ zbytečné

// DataStoreServiceCollectionExtensions.cs lines 69-71:
services.AddScoped<IProjectDetailComposition>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordEditorQueriesComposition>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordWriteCommandsComposition>(sp => sp.GetRequiredService<ProjectService>());
// Tyto 3 registrace jsou zbytečné pokud nikdo neinjectuje tyto interface přímo.
```

Stejná situace: `IDictionariesCommandsComposition` a `IDictionariesQueriesComposition`
v `Services/Data/` — ověřit zda jsou stále aktivně používány.

### ❌ Views — zbývající logika (neúplný Prompt 5)

Při průzkumu nalezeno v .cshtml souborech:

**`Views/Projekty/_EditZaznamForm.cshtml` — lines 4-35 (13 komplexních proměnných):**
```csharp
@{
    // Příklady logiky která patří do ViewModelu/Controlleru:
    var selectedTab = // .FirstOrDefault() s string.Equals porovnáním
    var isPagePresentation = // string.Equals podmínka
    var permissionMode = // ternární operátor s více podmínkami
    var normalizedEditorTab = // string.Equals s více větvemi
    var fallbackTab = // ternární fallback
    var returnUrl = // podmíněná URL stavba
    // + dalších 7 proměnných
}
```

**Soubory s `Url.Action()` voláními (38 celkem v 11 souborech):**

| Soubor | Výskytů |
|--------|---------|
| `Views/Nastaveni/_DetailPanel.cshtml` | 8× |
| `Views/Projekty/Detail.cshtml` | 7× |
| `Views/Jednani/Detail.cshtml` | 5× |
| `Views/Projekty/Index.cshtml` | 4× |
| `Views/Jednani/Index.cshtml` | 3× |
| `Views/Osoby/Index.cshtml` | 3× |
| `Views/Projekty/_ZaznamPartial.cshtml` | 3× |
| `Views/Projekty/_EditZaznamForm.cshtml` | 2× |
| ostatní | 3× |

**LINQ v 6 view souborech** (`FirstOrDefault`, `Where`, `OrderBy`, `Select`, `Any`):
- `Views/Projekty/Detail.cshtml`
- `Views/Nastaveni/_DetailPanel.cshtml`
- `Views/Jednani/Detail.cshtml`
- `Views/Nastaveni/RolePermissionModal.cshtml`
- `Views/Nastaveni/UserRolesModal.cshtml`

**`string.Equals` v 11 view souborech** — podmíněný rendering přes string porovnání místo bool property.

### ❌ Prompty 15 a 16 — nepuštěno

- **Číselníky v hlavní navigaci**: Stále v `_Layout.cshtml` line 81 — Prompt 15 nerealizován
- **Stránkování**: `BuildProjektyListAsync` ani `BuildProjektDetailAsync` nemají skip/take — Prompt 16 nerealizován
- **NastaveniController**: `RoleModal`, `PermissionModal`, `RolePermissionModal`, `ToggleRole`, `TogglePermission`, `DeleteRolePermission` stále existují — Prompt 15 (část o UI pro konfig permissions) nerealizován

### ❌ ZaznamyController — 13 URL helper metod (Prompt 2.24 nesplněn)

`Controllers/ZaznamyController.cs` stále obsahuje 13+ privátních pomocných metod:
- `BuildCommentAjaxSuccessResult` (line 289)
- `BuildSaveAjaxSuccessResult` (line 303) — 40 řádků
- `BuildDeleteRecordAjaxSuccessResult` (line 365)
- `ResolveCommentRedirect` (line 388)
- `BuildDeleteRecordReturnUrl` (line 503)
- `BuildRestoreReturnUrl` (line 514)
- `BuildMeetingDetailUrl` (line 522)
- `NormalizePresentation` (line 453)
- `NormalizeEditorTab` (line 468)
- `NormalizeProjectTab` (line 488)
- `NormalizeDeleteTab` (line 493)
- `NormalizeRecordEditorUiContext` (line 535)
- `NormalizeLocalReturnUrl` (line 547)

Poznámka: `NormalizeLocalReturnUrl` má bezpečnostní kontrolu `Url.IsLocalUrl()` — správně.
Celý controller má 556 řádků.

---

## ČÁST 4 — NOVÉ PROBLÉMY (NENALEZENY V PŮVODNÍM DOKUMENTU)

### 🔴 Bezpečnostní hlavičky chybí

`Program.cs` neobsahuje žádný security headers middleware kromě `X-Trace-Id`.
Chybí:
- **CSP** (Content-Security-Policy) — chrání před XSS
- **X-Frame-Options** nebo `frame-ancestors` v CSP — chrání před clickjacking
- **X-Content-Type-Options: nosniff** — chrání před MIME sniffing

Pro intranetovou aplikaci s Windows Auth je dopad nízký, ale best practice to vyžaduje.

### 🟡 Hardcoded dev credentials v appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "PmTrackerDb": "Server=localhost,1433;Database=PmTracker;User ID=sa;Password=PmTracker!2026;..."
  }
}
```

SA heslo je v plaintext v git repozitáři. Pokud je repo i na vzdáleném serveru nebo
sdílené, toto je bezpečnostní problém. Správně: user secrets nebo environment variables.

### 🟡 Žádné strukturované logování (Serilog)

Aplikace používá default `ILoggerFactory`. V produkci to znamená logy jdou do Windows
Event Log nebo IIS, bez strukturovaného formátu. Debugování produkčních problémů je obtížné.
Serilog s JSON sink by umožnil centralizovaný log management.

### 🟡 Export registrace — zbytečné v DI

`DataStoreServiceCollectionExtensions.cs` registruje tyto export helper třídy jednotlivě:
```csharp
services.AddScoped<ExportCommentProjectionBuilder>();
services.AddScoped<ExportRoleProjectionBuilder>();
services.AddScoped<ExportAttendanceProjectionBuilder>();
services.AddScoped<ExportRecordVisibilityEvaluator>();
services.AddScoped<ExportRecordProjectionBuilder>();
services.AddScoped<ExportTemplateSummaryBuilder>();
```
Po sloučení exportu na 3 soubory tyto builder třídy pravděpodobně stále existují jako
separátní injektované závislosti místo privátních metod v `OpenXmlWordExportService`.

### 🟡 Dvojitá navigace — Jednání

Jak bylo identifikováno v původním dokumentu (bod 2.29), problém stále existuje:
- `_Layout.cshtml` line 83: globální nav položka "Jednání" (→ `/Jednani/Index`)
- `Views/Projekty/Detail.cshtml` line 20: záložka projektu "Jednání" (→ tab=jednani)

Obě vypadají identicky, vedou na různá místa, bez vizuálního rozlišení.

---

## ČÁST 5 — CODEX PROMPTY

---

### PROMPT 1 — KRITICKÉ: Oprava UserContextResolver (SELECT * na každý request)

**Proč:** `UserContextResolver` se spouští při každém HTTP requestu.
Na řádku 282 načítá **celou tabulku Projekty** bez WHERE filtru, pak filtruje v paměti.
Každý klik uživatele = SELECT * FROM Projekty.

```
You are a senior C# developer refactoring a .NET 8 ASP.NET MVC application called PmTracker.Web.

THE PROBLEM:
Services/Security/UserContextResolver.cs runs on EVERY HTTP request (it resolves user context
for every controller action).

PROBLEM A — Line ~282 loads ALL projects without a WHERE filter:
  var allProjects = await dbContext.Projekty.ToListAsync(ct);
  // Then filters in memory to find non-deleted projects.
  // With 200 projects = 200 rows loaded per request, for every user, every click.

PROBLEM B — Line ~345 loads ALL users (in dev "asUser" functionality):
  var candidates = await dbContext.Osoby.Select(...).ToListAsync(ct);
  // Used to fuzzy-match a login name in development.

WHAT TO DO:

Fix A:
1. Find the code in UserContextResolver that loads projects for the purpose of building
   the user's "visible project IDs" or checking project existence.
2. If the query loads ALL projects and then filters in-memory for non-deleted/non-archived ones:
   Move the filter to the database query with a WHERE clause:
   - Replace: await dbContext.Projekty.ToListAsync(ct)
   - With: await dbContext.Projekty.Where(p => !p.JeSmazan).Select(p => p.Id).ToListAsync(ct)
   OR if you only need to check which projects a user can see:
   Load only the IDs needed (with appropriate WHERE), not full entity rows.
   Use Select projection — load only needed columns (Id, StavKod, JeSmazan etc.).

Fix B:
The "asUser" fuzzy matching (line 345+) is development-only functionality.
Wrap it with:
  if (!app.Environment.IsDevelopment()) — already done for some parts, ensure this
  specific bulk load only happens in development AND only when "asUser" query param is present.
If it IS triggered in dev, the bulk load is acceptable (dev only).
Verify it cannot be triggered in production.

GENERAL RULE for UserContextResolver:
Every dbContext query in this file MUST:
- Have a WHERE clause that limits results to needed rows
- Use Select projection to load only needed columns (avoid SELECT *)
- Never load entire tables

RULES:
- Do NOT change the user context resolution logic or the resulting CurrentUserContextViewModel
- Do NOT change permission checking behavior
- The resolved user context must be identical — only the DB queries change
- Do NOT change the method signatures
- All existing tests must still pass
```

---

### PROMPT 2 — Oprava over-fetchingu (ProjektyController + ZaznamyController)

**Proč:** Dva controllery načítají celé kolekce dat aby pak vybraly jeden záznam.
Každé otevření modálu = zbytečné načtení všech projektů / celého projektu.

**Konkrétní kód:**
```csharp
// ProjektyController.cs line 113 (EditProjectModal):
var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);

// ProjektyController.cs line 150 (DeleteProjectModal):
var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);

// ZaznamyController.cs line 208-209 (RecordCardPartial):
var model = await _recordService.BuildProjektDetailAsync(projektId, ct);
var record = model.Zaznamy.FirstOrDefault(x => x.Id == zaznamId);
// + line 234: ViewData["OtevrenaJednani"] = model.OtevrenaJednani;
```

```
You are a senior C# developer refactoring a .NET 8 ASP.NET MVC application called PmTracker.Web.

THE PROBLEM:

PROBLEM A — ProjektyController lines 113 and 150:
Both EditProjectModal and DeleteProjectModal call:
  (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id)
BuildProjektyListAsync() fetches ALL projects from the database.
Then FirstOrDefault() filters in C# memory.

For EditProjectModal, only these fields are needed: Id, Nazev, Zkratka, Stav, StavKod, PouzivatIdentJednani
For DeleteProjectModal, only: Id, Nazev, Zkratka, Stav

PROBLEM B — ZaznamyController lines 208-209, 234:
RecordCardPartial calls:
  var model = await _recordService.BuildProjektDetailAsync(projektId, ct);
  var record = model.Zaznamy.FirstOrDefault(x => x.Id == zaznamId);
  // ...
  ViewData["OtevrenaJednani"] = model.OtevrenaJednani;

BuildProjektDetailAsync() -> BuildRecordCardsForProjectAsync() loads data in 8 stages:
all records + all lookups + all history tables + all comments + all referenced people + meetings.
Only ONE record (by zaznamId) and OtevrenaJednani (open meetings list) are actually used.

WHAT TO DO:

Fix A — Add GetProjectByIdAsync to IProjectService:

1. Add to Services/IProjectService.cs:
   Task<ProjektListItemViewModel?> GetProjectByIdAsync(int id, CancellationToken ct = default);

2. Implement in ProjectService (find the appropriate partial file — ProjectService.ListQueries.cs
   or similar):
   public async Task<ProjektListItemViewModel?> GetProjectByIdAsync(int id, CancellationToken ct)
   {
       return await dbContext.Projekty
           .AsNoTracking()
           .Where(p => p.Id == id && !p.JeSmazan)
           .Select(p => new ProjektListItemViewModel
           {
               Id = p.Id,
               Nazev = p.Nazev,
               Zkratka = p.Zkratka,
               // Copy the exact field mappings from the existing BuildProjektyListAsync
               // implementation — match Stav, StavKod, PouzivatIdentJednani exactly
           })
           .FirstOrDefaultAsync(ct);
   }
   IMPORTANT: Look at the existing BuildProjektyListAsync() implementation to find the exact
   entity->ViewModel field mappings. Copy them precisely so the ViewModel has the same data
   as when loaded via the list method.

3. Update ProjektyController.EditProjectModal (line 113):
   Replace:
     var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);
   With:
     var project = await _projectService.GetProjectByIdAsync(id, ct);

4. Update ProjektyController.DeleteProjectModal (line 150): same replacement.

Fix B — Add targeted query methods to IRecordService:

1. Add to Services/IRecordService.cs:
   Task<ProjektZaznamCardViewModel?> GetRecordCardAsync(int projektId, int zaznamId, CancellationToken ct = default);
   Task<IReadOnlyList<LookupOptionViewModel>> GetOpenMeetingOptionsAsync(int projektId, CancellationToken ct = default);

2. Implement GetRecordCardAsync:
   - Study what BuildRecordCardsForProjectAsync() produces for a single record
   - Create a targeted query that loads ONLY the data for one specific record (zaznamId)
   - Use AsNoTracking() and Select projection
   - The method must return a fully populated ProjektZaznamCardViewModel (same type as
     model.Zaznamy[i] currently returns) — check what fields ZaznamyController uses after
     loading (lines 215-232): CanEditRecord, CanEditSchedule, JeUkol,
     AktualniSubsystemLeadEquivalentOsobaIds, etc.
   - For fields requiring related data (history, comments), include only what is needed
     for the specific record

3. Implement GetOpenMeetingOptionsAsync:
   - Load only meetings where ProjektId == projektId AND status is not closed
   - Return as IReadOnlyList<LookupOptionViewModel> (same format as model.OtevrenaJednani)
   - Look at how OtevrenaJednani is built in the existing BuildProjektDetailAsync to match format

4. Update ZaznamyController.RecordCardPartial (lines 208-209, 234):
   Replace:
     var model = await _recordService.BuildProjektDetailAsync(projektId, ct);
     var record = model.Zaznamy.FirstOrDefault(x => x.Id == zaznamId);
     // ...
     ViewData["OtevrenaJednani"] = model.OtevrenaJednani;
   With:
     var record = await _recordService.GetRecordCardAsync(projektId, zaznamId, ct);
     // ...
     ViewData["OtevrenaJednani"] = await _recordService.GetOpenMeetingOptionsAsync(projektId, ct);

RULES:
- Do NOT change the HTTP contract — same response, same PartialView, same ViewModel type
- Do NOT change permission checks (lines 215-223 of RecordCardPartial stay identical)
- Do NOT change the ViewModel property assignments (lines 225-232 stay identical)
- Do NOT change ViewData["ProjektId"] assignment (line 235)
- Return 404 if project/record not found — same behavior as now
- All new DB queries must use AsNoTracking() and Select projections
- The ProjektZaznamCardViewModel must be fully populated — check every property
  that is accessed in Views/Projekty/_ZaznamPartial.cshtml
```

---

### PROMPT 3 — Oprava Program.cs (2 zbývající problémy)

**Proč:** `partial class Program` je mrtvý kód bez testů.
`app.MapControllers()` je pravděpodobně zbytečné pro conventionally-routed controllery.

**Konkrétní kód v Program.cs:**
```csharp
// line 51+53 — dvojí route mapping:
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Projekty}/{action=Index}/{id?}");

// line 59 — mrtvý kód:
public partial class Program;
```

```
You are a senior C# developer refactoring a .NET 8 ASP.NET MVC application called PmTracker.Web.

THE PROBLEM: Program.cs has two minor issues.

PROBLEM A — Double route mapping (lines 51 and 53):
  app.MapControllers();           // for attribute routing
  app.MapControllerRoute(...);    // for conventional routing

app.MapControllers() is needed ONLY for controllers with [Route(...)], [HttpGet("path/here")]
or other explicit route attribute decorations.
app.MapControllerRoute() handles conventional {controller}/{action}/{id?} routing.

PROBLEM B — Dead code (line 59):
  public partial class Program;
This exists for WebApplicationFactory in integration tests.
Search the solution for any .Tests or .IntegrationTests projects.
If none exist, this is dead code.

WHAT TO DO:

Fix A:
1. Search ALL controller files in Controllers/ for attribute routing patterns:
   - [Route("path")] on controller class or action methods
   - [HttpGet("explicit/path")] — not empty [HttpGet], but with a path string argument
   - [HttpPost("explicit/path")] etc.
2a. IF no controllers use explicit path attribute routing:
    Remove line 51: app.MapControllers();
    Keep line 53: app.MapControllerRoute(...)
    Add a comment: // Conventional routing only — no attribute-routed controllers
2b. IF some controllers use attribute routing:
    Keep both lines. Add a comment identifying which controllers use attribute routing.

Fix B:
1. Search the entire solution (all .csproj files) for test projects.
2a. If NO test projects exist: Remove line 59 (public partial class Program;)
2b. If test projects exist: Keep it and add a comment explaining why.

RULES:
- Do NOT change middleware order
- Do NOT change the default route pattern
- Do NOT remove UseExceptionHandler — HomeController has Error action, this is correct
- If removing app.MapControllers() would break anything, keep it and document why
```

---

### PROMPT 4 — Odstranění composition interfaces (zbytky Promptu 2)

**Proč:** `ProjectService` stále implementuje 3 rozhraní co měla být smazána.
Jsou stále registrovány v DI i bez SqlServerDataStore.

**Konkrétní kód:**
```csharp
// Services/ProjectService.cs lines 9-13:
public sealed partial class ProjectService :
    IProjectService,                   // ✅ správně
    IProjectDetailComposition,         // ❌ zbytečné
    IRecordEditorQueriesComposition,   // ❌ zbytečné
    IRecordWriteCommandsComposition    // ❌ zbytečné

// Services/Data/DataStoreServiceCollectionExtensions.cs lines 69-71:
services.AddScoped<IProjectDetailComposition>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordEditorQueriesComposition>(sp => sp.GetRequiredService<ProjectService>());
services.AddScoped<IRecordWriteCommandsComposition>(sp => sp.GetRequiredService<ProjectService>());
```

```
You are a senior C# developer refactoring a .NET 8 ASP.NET MVC application called PmTracker.Web.

THE PROBLEM:
After removing SqlServerDataStore, three composition interfaces were left on ProjectService
instead of being deleted. They are still registered in DI unnecessarily.

Current state in Services/ProjectService.cs (lines 9-13):
  public sealed partial class ProjectService :
      IProjectService,
      IProjectDetailComposition,       // leftover from SqlServerDataStore refactor
      IRecordEditorQueriesComposition, // leftover
      IRecordWriteCommandsComposition  // leftover

These three are also registered in DataStoreServiceCollectionExtensions.cs lines 69-71.

WHAT TO DO:

Step 1 — Find all consumers:
Search the entire codebase (Controllers/, Services/) for constructor parameters or
field declarations of type:
  - IProjectDetailComposition
  - IRecordEditorQueriesComposition
  - IRecordWriteCommandsComposition
  - IDictionariesCommandsComposition  (also check this one)
  - IDictionariesQueriesComposition   (also check this one)

Step 2 — Replace each injection:
For every class that injects IProjectDetailComposition, IRecordEditorQueriesComposition,
or IRecordWriteCommandsComposition:
- Replace the injected interface type with IProjectService
  (since ProjectService implements IProjectService and contains all the same methods)
- Update the constructor parameter and field declaration
- Update all method call sites — the method names stay the same, just the variable type changes

Example:
  BEFORE: private readonly IProjectDetailComposition _projectDetail;
  AFTER:  private readonly IProjectService _projectService;
  // Then: _projectDetail.BuildProjektDetailAsync(id, ct) becomes _projectService.BuildProjektDetailAsync(id, ct)

Step 3 — Clean up ProjectService:
After all consumers are updated:
- Remove IProjectDetailComposition, IRecordEditorQueriesComposition,
  IRecordWriteCommandsComposition from ProjectService class declaration (lines 9-13)

Step 4 — Clean up DI registrations:
In DataStoreServiceCollectionExtensions.cs:
- Remove lines 69-71 (the three AddScoped registrations for composition interfaces)

Step 5 — Delete interface files:
Delete from Services/Data/:
  - IProjectDetailComposition.cs
  - IRecordEditorQueriesComposition.cs
  - IRecordWriteCommandsComposition.cs

Step 6 — Repeat for dictionary interfaces:
Apply the same analysis to IDictionariesCommandsComposition and IDictionariesQueriesComposition:
- Find their implementations (HarmonogramService? DictionaryService?)
- Find their consumers
- Replace injections with IHarmonogramService or IDictionaryService as appropriate
- Remove from class declarations, DI registrations, and delete files

RULES:
- Do NOT change any method implementations
- Do NOT change business logic
- Do NOT remove IProjectService, IMeetingService, IRecordService etc.
- The project must compile without errors after this change
- The DI container must correctly resolve all services
```

---

### PROMPT 5 — Přesun zbývající logiky z Views do ViewModels

**Proč:** Prompt 5 byl proveden neúplně. Views stále obsahují string porovnání,
LINQ operace a `Url.Action()` volání. Největší problém: `_EditZaznamForm.cshtml` lines 4-35.

**Konkrétní zbývající problémy:**

```csharp
// Views/Projekty/_EditZaznamForm.cshtml lines 4-35 — 13 komplexních proměnných:
var selectedTab = somelist.FirstOrDefault(x => string.Equals(x.Value, ...));
var isPagePresentation = string.Equals(presentation, "page", ...);
var permissionMode = condition ? "edit" : "view";
var normalizedEditorTab = string.Equals(tab, "external") ? "external" : ...;
// + dalších 9 proměnných s podmíněnou logikou

// Url.Action() v 11 view souborech (38 výskytů celkem):
// Detail.cshtml: 7×, _DetailPanel.cshtml: 8×, Jednani/Detail.cshtml: 5×, atd.

// LINQ v 6 view souborech:
// .FirstOrDefault(), .Where(), .OrderBy(), .Select(), .Any()
// v Detail.cshtml, _DetailPanel.cshtml, Jednani/Detail.cshtml atd.
```

```
You are a senior C# developer refactoring a .NET 8 ASP.NET MVC application called PmTracker.Web.

THE PROBLEM:
Views still contain logic that belongs in ViewModels and Controllers.
The previous refactoring (Prompt 5) was incomplete.

PRIORITY 1 — Views/Projekty/_EditZaznamForm.cshtml lines 4-35:
This partial has ~13 complex variable declarations in its @{ } block including:
- .FirstOrDefault() with string.Equals comparisons
- Multiple ternary operators for determining presentation mode, editor tab, permission mode
- Conditional URL construction

These variables must be moved to the ViewModel that _EditZaznamForm.cshtml receives.
Find which ViewModel (check the @model declaration at top of the file) and add properties:
  bool IsPagePresentation { get; init; }
  string NormalizedEditorTab { get; init; }  // "basic", "external", "collaboration", "schedule"
  string PermissionMode { get; init; }       // "edit" or "view"
  string FallbackTab { get; init; }
  string? ReturnUrl { get; init; }
  // etc. — one property per variable in the @{ } block

Compute these in the Controller action(s) that render this partial (check which controllers
call the action that renders _EditZaznamForm).

PRIORITY 2 — Url.Action() calls in views (38 occurrences across 11 files):
Every Url.Action() call in a view builds a URL that belongs in the ViewModel.

Process each file:
For every Url.Action("Action", "Controller", new { ... }) in a view:
1. Add a string property to the ViewModel: string ActionUrl { get; init; }
   (use a descriptive name: EditUrl, DeleteUrl, BackUrl, SaveUrl, etc.)
2. Compute the URL in the Controller using Url.Action(...)
3. Replace the inline call in the view with: href="@Model.EditUrl"

Files to process (highest priority first):
1. Views/Nastaveni/_DetailPanel.cshtml (8× Url.Action)
2. Views/Projekty/Detail.cshtml (7× Url.Action)
3. Views/Jednani/Detail.cshtml (5× Url.Action)
4. Views/Jednani/Index.cshtml (3× Url.Action)
5. Views/Projekty/Index.cshtml (4× Url.Action)
6. Views/Osoby/Index.cshtml (3× Url.Action)
7. Views/Projekty/_ZaznamPartial.cshtml (3× Url.Action)
8. Views/Projekty/_EditZaznamForm.cshtml (2× Url.Action)
9. remaining files (1× each)

PRIORITY 3 — LINQ in views:
In Views/Projekty/Detail.cshtml, Views/Nastaveni/_DetailPanel.cshtml,
Views/Jednani/Detail.cshtml, Views/Nastaveni/RolePermissionModal.cshtml,
Views/Nastaveni/UserRolesModal.cshtml:
- Find .FirstOrDefault(), .Where(), .OrderBy(), .Select(), .Any() calls
- Move them to ViewModel properties (pre-computed IReadOnlyList<T>, bool, etc.)
- Replace in view with @Model.PreparedProperty

PRIORITY 4 — string.Equals in views:
In any view file with string.Equals() or string.Compare() for conditional rendering:
- Replace with a bool ViewModel property
- Example: string.Equals(Model.Tab, "jednani") -> bool ShowJednaniTab { get; init; }

RULES:
- Views must contain ONLY: HTML tags, @foreach, @if (Model.BoolProperty), @Model.StringProperty
- No string comparisons in views
- No Url.Action() calls in views
- No LINQ in views
- No complex @{ } code blocks
- ViewModel properties are computed ONCE in the Controller, not re-evaluated per render
- Do NOT change the rendered HTML output — same UI, just moved computation
- Do NOT break any AJAX calls that depend on data-url attributes
```

---

### PROMPT 6 — Bezpečnostní hlavičky

**Proč:** Aplikace neodesílá standard bezpečnostní HTTP hlavičky.
Pro intranetovou aplikaci je dopad nízký, ale hlavičky jsou triviální přidat.

```
You are a senior C# developer adding security HTTP response headers to a .NET 8 ASP.NET MVC
application called PmTracker.Web. This is an intranet application with Windows Authentication.

THE PROBLEM:
Program.cs has no security headers middleware beyond X-Trace-Id.
Missing headers:
- X-Content-Type-Options: nosniff
- X-Frame-Options: SAMEORIGIN (or Content-Security-Policy: frame-ancestors 'self')
- Referrer-Policy: strict-origin-when-cross-origin

Note: Full CSP is complex for this app (uses inline scripts for Quill editor, SignalR etc.)
— do NOT add a restrictive Content-Security-Policy without testing.

WHAT TO DO:
In Program.cs, extend the existing inline middleware (the one adding X-Trace-Id) to also add
security headers:

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return Task.CompletedTask;
    });
    await next();
});

RULES:
- Do NOT add Content-Security-Policy — too risky without testing inline scripts
- Do NOT change existing middleware order
- Do NOT add X-XSS-Protection (deprecated in modern browsers)
- These headers apply to ALL responses including AJAX — that is correct
```

---

### PROMPT 7 — Dev credentials: přesun do user secrets

**Proč:** `appsettings.Development.json` obsahuje SA heslo v plaintext v git repozitáři.

**Konkrétní problém:**
```json
{
  "ConnectionStrings": {
    "PmTrackerDb": "Server=localhost,1433;Database=PmTracker;User ID=sa;Password=PmTracker!2026;..."
  }
}
```

```
You are a senior C# developer improving security in a .NET 8 ASP.NET MVC application.

THE PROBLEM:
appsettings.Development.json contains a hardcoded SQL Server SA password in plaintext.
This file is committed to the git repository.

WHAT TO DO:
1. Remove the connection string from appsettings.Development.json:
   Remove the "ConnectionStrings" section entirely from appsettings.Development.json.
   Keep only the Logging and other non-sensitive settings.

2. Initialize user secrets for the project:
   In the .csproj file, ensure there is a <UserSecretsId> element.
   If it doesn't exist, add:
   <UserSecretsId>pmtracker-web-dev-secrets</UserSecretsId>

3. Document in appsettings.Development.json with a placeholder key:
   {
     "_Note": "Connection string is stored in user secrets. Run: dotnet user-secrets set ConnectionStrings:PmTrackerDb 'your-connection-string'",
     "Logging": { ... }
   }

4. Add to README or a new DEVELOPMENT_SETUP.md file:
   Instructions for setting up the development connection string via:
   dotnet user-secrets set "ConnectionStrings:PmTrackerDb" "Server=localhost,1433;Database=PmTracker;User ID=sa;Password=YOUR_PASSWORD;..."

5. If appsettings.Development.json is NOT in .gitignore and contains the password in git history:
   Consider rotating the SA password if the repository is shared or accessible.

RULES:
- Do NOT change appsettings.json (production uses Windows Auth — no password there)
- Do NOT change appsettings.Production.json
- The app must still work in development — user secrets override appsettings files automatically
- Do NOT store credentials in environment-committed files
```

---

## ČÁST 6 — STAV VÝKONU PO REFAKTORIZACI

### Co zlepšilo výkon ✅

| Oblast | Původní stav | Nový stav |
|--------|-------------|-----------|
| Call chain pro 1 DB operaci | 10+ tříd | Controller → Service → DbContext (2 skoky) |
| SaveAttendance 20 uživatelů | 20 DB transakcí | 1 batch transakce |
| JednaniController.TaskItemPartial | Načetlo celé jednání | `GetSingleTaskAsync` — 1 cílený dotaz |
| JednaniController.SaveStatus | Načetlo celé jednání | `GetMeetingProjectIdAsync` — 1 dotaz |
| JS parse při načtení stránky | 9 016 řádků | 3 řádky + lazy ES moduly |
| Response buffering | Každý AJAX = full RAM | Middleware odstraněn |

### Co stále zatěžuje výkon ❌

| Oblast | Popis | Závažnost |
|--------|-------|-----------|
| UserContextResolver line 282 | SELECT * FROM Projekty na každý request | 🔴 Kritické |
| RecordCardPartial | Načte celý projekt (8 fází) pro 1 záznam | 🟠 Vysoké |
| EditProjectModal/DeleteProjectModal | Načte všechny projekty pro 1 projekt | 🟡 Střední |
| Číselníky v DB | Stavy/typy načítány z DB místo C# enum | 🟡 Střední |
| Žádné stránkování | Celý seznam projektů/záznamů vždy | 🟡 Střední |

### Co nevadí (bylo špatně odhadnuto)

| Oblast | Proč nevadí |
|--------|-------------|
| `.ToList()` v Services (193×) | Jsou in-memory operace na již načtených datech — správný vzor |
| Lookup tabulky (CiselnikStavuJednani atd.) | Malé tabulky, načteny async, v paměti transformovány |
| `HarmonogramService.cs` 15× `.ToList()` | Vše in-memory, žádné DB-facing synchronní volání |

---

## ČÁST 7 — SHRNUTÍ PROMPTŮ (POŘADÍ PODLE PRIORIT)

| Prompt | Problém | Priorita |
|--------|---------|----------|
| PROMPT 1 | UserContextResolver SELECT * na každý request | 🔴 Okamžitě |
| PROMPT 2 | Over-fetching v ProjektyController + ZaznamyController | 🟠 Brzy |
| PROMPT 4 | Composition interfaces odstranit | 🟠 Brzy |
| PROMPT 3 | Program.cs partial class + dvojí routing | 🟡 Kdykoli |
| PROMPT 5 | Views zbývající logika (Url.Action, LINQ, string.Equals) | 🟡 Kdykoli |
| PROMPT 6 | Bezpečnostní hlavičky | 🟡 Kdykoli |
| PROMPT 7 | Dev credentials do user secrets | 🟡 Kdykoli |
