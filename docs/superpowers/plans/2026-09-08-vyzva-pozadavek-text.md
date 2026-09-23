# Výzva: jen Word s náhledem a vlastní text požadavku — implementační plán

> **Pro exekutora:** POVINNÁ SUB-DOVEDNOST: `superpowers:executing-plans`. Kroky mají
> checkboxy (`- [ ]`) pro sledování postupu. Uživatel si přeje **inline exekuci**
> v hlavní session, ne subagenty.

**Cíl:** Tisk výzvy zjednodušit na Word plus Náhled a nahradit popis z tiketu textem,
který si pracovník napíše sám u konkrétní PNF vazby.

**Architektura:** Nový nullable sloupec `zaznam_externi_odkazy.pozadavek` nese sanitizované
HTML z Quillu. Editor jede na existující rich-text infrastruktuře, převod do Wordu na
komponentě vytažené ze stávajícího exportu záznamu. Předřazený blok 1 zviditelní chyby,
aby se nepracovalo naslepo.

**Technologie:** ASP.NET Core MVC (net8.0), EF Core 8, Razor, Quill, DocumentFormat.OpenXml 3.2.0,
xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-08-vyzva-pozadavek-text-design.md`

## Globální omezení

- **Commity drží uživatel.** Žádný krok nesmí spustit `git commit`. Místo kroku „Commit"
  je vždy krok „Ověření".
- **Build musí skončit 0 chyb, 0 upozornění.**
- **Známá selhání, která se nikdy neopravují ani nevydávají za regresi:** 4 Api testy
  (`RecordEditorControllerTests.Edit_ShouldRenderScheduleMiniGantt_WithAlignedAxis_WithoutPerStepDuplicateBars`
  a tři `ProjectHarmonogramRenderTests.*`) a 1 Integration test
  (`ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`).
  Výchozí stav před tímto plánem: Unit 1680/1680, Api 355/359, Integration 85/86.
- **`appsettings.json` a `appsettings.Development.json` jsou gitignorované soubory se
  secrety** — nikdy je needituj ani nevymýšlej jejich obsah. Konfiguraci dokumentuj
  do `PmTracker.Web/appsettings.example.json`.
- **Schéma databáze se mění ručním skriptem** `db_upgrade_*.sql`, složka EF Migrations je
  záměrně prázdná (nasazení je offline intranet). Skripty musí být idempotentní.
- **Assertace na český text v Api HTML testech nikdy neprojdou** — Razor kóduje diakritiku
  na číselné entity (`Místo` → `M&#xED;sto`). Kotvi se na atributy, CSS třídy nebo ASCII.
- **`pm-button` strhává atributy z hostu** — `hidden`, `aria-*` na něm neúčinkují. Stav
  řiď třídou nebo atributem na obyčejném předkovi.
- **UPSERT, ne replace** — externí vazby mají FK z audit tabulek (`vyjadreni_vazby`),
  takže `RemoveRange + Add` skončí porušením cizího klíče.
- **Neopravovat naslepo.** Blok 1 je předřazený schválně: dokud není vidět příčina chyby
  500 při tisku výzvy, nesahej na exportní cestu v blocích 7 a 8.

## Mapa souborů

| Soubor | Odpovědnost | Blok |
|---|---|---|
| `PmTracker.ServiceDesk.Sql/Entities/HotKalkulaceEntity.cs` | CLR typy sedící se skutečnou databází | 0 |
| `PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs` | Výběr nejnovější akceptované kalkulace | 0 |
| `PmTracker.ServiceDesk.Contracts/Contracts/HotKalkulaceDto.cs` | Typy v kontraktu | 0 |
| `PmTracker.Web/Services/Diagnostics/DiagnosticLogBuilder.cs` (nový) | Jediný formát diagnostického výpisu pro AJAX i chybovou stránku | 1 |
| `PmTracker.Web/Controllers/BaseController.Ajax.cs` | Deleguje na sdílený builder | 1 |
| `PmTracker.Web/Controllers/HomeController.cs` | Chybová akce sestaví diagnostiku z původního požadavku | 1 |
| `PmTracker.Web/Models/ViewModels/ErrorViewModel.cs` | Nese zprávu, kód a diagnostiku | 1 |
| `PmTracker.Web/Views/Shared/Error.cshtml` | Vykreslí panel s výpisem | 1 |
| `PmTracker.Web/wwwroot/js/modules/ui/errorPageCopy.js` (nový) | Tlačítka Kopírovat a Uložit na chybové stránce | 1 |
| `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml` | Patička: Náhled, pak Tisk výzvy | 2 |
| `PmTracker.Web/Models/Entities/…ZaznamExterniOdkazEntity` | Vlastnost `Pozadavek` | 3 |
| `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` | Mapování sloupce | 3 |
| `db_upgrade_1_4_2_externi_odkaz_pozadavek.sql` (nový) | Přidání sloupce | 3 |
| `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` | `SaveRecordExterniVazbaCommand.Pozadavek` | 4 |
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | UPSERT ukládá text, harvest se ho nedotkne | 4 |
| `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml` | Editor pod datumy, jen u PNF | 5 |
| `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css` | Times New Roman 12 v editoru | 5 |
| `PmTracker.Web/Controllers/ExterniOdkazController.cs` | Odpověď náhledu nese `Popis` | 6 |
| `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js` | Předvyplnění nové vazby | 6 |
| `PmTracker.Web/Services/Export/RichTextWordWriter.cs` (nový) | Sdílený převod HTML → OpenXML | 7 |
| `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs` | Deleguje na sdílený zapisovač | 7 |
| `PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs` | Čte `pozadavek` místo popisu tiketu | 8 |
| `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaExportViewModels.cs` | `PozadavekHtml` místo `Popis` | 8 |
| `PmTracker.Web/Views/Export/VyzvaTemplate.cshtml` | Vykreslí HTML, TNR 12 | 8 |
| `PmTracker.Web/Services/Export/OpenXmlVyzvaExportService.cs` | Word vysází rich text v TNR 12 | 8 |

---
## Blok 0: Oprava mapování kalkulací — příčina chyby 500

**Stav: implementováno 2026-09-08, čeká na ověření naostro (krok 7).**

**Zjištěno 2026-09-08 z produkčního logu.** Obě cesty tisku (`/Export/Vyzva/{id}/Tisk`
i `/Export/Vyzva/{id}/Word`) padají na téže výjimce:

```
System.InvalidCastException: Unable to cast object of type 'System.String' to type 'System.Int32'.
   at Microsoft.Data.SqlClient.SqlDataReader.GetInt32(Int32 i)
   ... context type 'PmTracker.ServiceDesk.Sql.TicketingReadOnlyDbContext'
```

Sloupec namapovaný jako číslo je v reálné databázi text: `HOT_KALKULACE.verze` obsahuje
`"TZFIS2026"`, ale entita ho má jako `int?`.

**Proč to nechytily testy:** `GetAkceptovaneKalkulaceAsync` má tisk výzvy jako svého
**prvního konzumenta** a Api testy běží s vypnutým ServiceDeskem
(`DisabledTicketingQueryService`), takže se SQL cesta na `HOT_KALKULACE` nikdy nespustí.
Sada je proto zelená, zatímco produkce padá.

**Skutečné schéma (`sys.columns`, dodal uživatel 2026-09-08)** odhalilo čtyři rozpory,
ne jeden. Bez toho výstupu by se opravovalo postupně a tisk by padal pokaždé na jiném
sloupci:

| Vlastnost | Sloupec a jeho skutečný typ | Mapování dnes | Kdo ji čte | Řešení |
|---|---|---|---|---|
| `Verze` | `verze` — `nvarchar(100)` | `int?` | jen řazení, které se stejně mění | **smazat** |
| `IdKalk` | `id_kalk` — `nvarchar(30)` | `int?` | nikdo | **smazat** |
| `Termin` | `termin` — **`int`** | `DateTime?` | nikdo | **smazat** |
| `PocetL` | `pocet_l` — `numeric(18,2)` | `int?` | teče do view modelu, ale netiskne se | `decimal?` |

`termin` je nejzajímavější nález: je to celé číslo, ale entita ho čte jako datum. Spadlo
by to hned po opravě `verze` a `id_kalk`, jen s jinou hláškou. Lidsky čitelný termín nese
vedlejší sloupec `text_termin` (`nvarchar(500)`), který je namapovaný správně.

Smazáním tří nepoužitých vlastností přestane EF ty sloupce vůbec vybírat, takže je jejich
typ nezajímavý. Zbylé mapování schématu odpovídá: ceny, sazby a pracnosti jsou
`numeric(18,2)` proti `decimal?`, `pid`/`akceptace`/`text_termin` jsou `nvarchar`
proti `string?`, `popis` a `rozpad_licence` jsou `ntext`/`text` proti `string?`
a `id` je `int` proti `long` s `HasConversion<int>()`.

**Řazení podle rozhodnutí uživatele (2026-09-08):** filtr zůstává
`akceptace = 'Akceptováno'`; když je akceptovaných kalkulací pro jedno PNF víc, vyhrává
ta s **vyšším `id`**.

**Soubory:**
- Upravit: `PmTracker.ServiceDesk.Sql/Entities/HotKalkulaceEntity.cs`
- Upravit: `PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs` (mapování, řádky 87–121)
- Upravit: `PmTracker.ServiceDesk.Contracts/Contracts/HotKalkulaceDto.cs`
- Upravit: `PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs` (řádky 38–39, 51–56, 64)
- Upravit: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaExportViewModels.cs` (řádek 56)
- Test: `PmTracker.Tests.Unit/ServiceDesk/SqlTicketingQueryServiceTests.cs` (existující)
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs` (řádek 41 — konstrukce DTO)

- [x] **Krok 1: Napsat padající test na výběr kalkulace**

Do `PmTracker.Tests.Unit/ServiceDesk/SqlTicketingQueryServiceTests.cs`. Harness nad
`TicketingReadOnlyDbContext` v souboru už je — použij ho:

```csharp
    /// <summary>
    /// Když má PNF víc akceptovaných kalkulací, vyhrává ta s vyšším id
    /// (rozhodnutí uživatele 2026-09-08). Dřív se řadilo podle `verze`, jenže ta nese
    /// označení technického zadání ("TZFIS2026") a bývá u všech kalkulací stejná —
    /// „nejnovější" tak vycházela náhodně a do výzvy mohly jít ceny ze špatné kalkulace.
    /// </summary>
    [Fact]
    public async Task GetAkceptovaneKalkulace_PriViceAkceptovanych_VybereVyssiId()
    {
        // Dvě akceptované kalkulace téhož pid + jedna neakceptovaná s nejvyšším id.
        // Očekávání: vrátí se ta s vyšším id z AKCEPTOVANÝCH, neakceptovaná se ignoruje.
    }
```

Tělo napiš podle vzoru testu na řádku 61 téhož souboru, který
`GetAkceptovaneKalkulaceAsync` už volá — seed entit i sestavení služby okopíruj odtud.

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~GetAkceptovaneKalkulace_PriViceAkceptovanych"`
Očekávej: selhání — dnes se řadí podle `Verze`.

- [x] **Krok 3: Vyhodit nepoužité vlastnosti a přetypovat počet licencí**

V `HotKalkulaceEntity` **smaž** tři vlastnosti a jednu přetypuj:

```csharp
    // SMAZAT: public int? IdKalk { get; set; }      // id_kalk je nvarchar(30)
    // SMAZAT: public int? Verze { get; set; }       // verze je nvarchar(100)
    // SMAZAT: public DateTime? Termin { get; set; } // termin je int, ne datum
    public decimal? PocetL { get; set; }             // bylo int?, pocet_l je numeric(18,2)
```

V `TicketingReadOnlyDbContext` smaž odpovídající tři řádky `HasColumnName` pro
`id_kalk`, `verze` a `termin`. Mapování `pocet_l` zůstává, mění se jen CLR typ.

Doplň nad entitu komentář, ať to někdo nevrátí zpátky:

```csharp
// 2026-09-08: sloupce id_kalk, verze a termin se schválně nemapují — jejich CLR typy
// v entitě neodpovídaly databázi a čtení celé tabulky na nich padalo
// (InvalidCastException v SqlDataReader). Podle sys.columns je verze nvarchar(100),
// id_kalk nvarchar(30) a termin int (ne datum). Nikdo je nečte; lidsky čitelný termín
// nese text_termin. Kdyby je někdo potřeboval, nejdřív si ověř typ v sys.columns.
```

- [x] **Krok 4: Srovnat kontrakt a view model**

V `HotKalkulaceDto` smaž parametry `int? Verze` a `DateTime? Termin` a přetypuj
`int? PocetLicenci` na `decimal? PocetLicenci`. `TextTermin` zůstává — je namapovaný
správně a termín ve výzvě by se bral z něj. V `VyzvaExportViewModels.cs` (řádek 56) srovnej typ také:

```csharp
    public decimal? PocetLicenci { get; init; }
```

V `VyzvaExportBuilderTests.cs` na řádku 41 uprav konstrukci `HotKalkulaceDto` — zmizí
z ní `Verze: 2`.

- [x] **Krok 5: Opravit výběr kalkulace**

V `SqlTicketingQueryService` nahraď obě řazení. Jednotlivá kalkulace (řádek 39):

```csharp
            .OrderByDescending(x => x.Id)
```

Dávková varianta (řádek 56):

```csharp
            .ToDictionary(g => g.Key, g => MapKalkulace(g.OrderByDescending(k => k.Id).First()));
```

A v `MapKalkulace` (řádky 64–71) vypusť `k.Verze` i `k.Termin` z volání konstruktoru DTO.

- [x] **Krok 6: Spustit a ověřit průchod**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~SqlTicketingQueryServiceTests"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaExportBuilderTests"
dotnet build PmTracker.sln
```

Očekávej: testy zelené, build 0 chyb a 0 upozornění.

- [ ] **Krok 7: Ověřit naostro** — čeká na nasazení uživatelem

Tohle je jediný krok, který opravu potvrdí doopravdy: nasadit a zkusit tisk výzvy.
Testy sem nedosáhnou, protože ServiceDesk je v nich vypnutý.

Zbytkové riziko po dodání schématu žádné nezbývá: všechny čtyři rozpory jsou potvrzené
proti `sys.columns` a zbytek mapování schématu odpovídá.

---

## Blok 1: Chyba na obrazovce s detaily a kopírováním

Předřazený blok. Chyba 500 při tisku výzvy zatím nemá zjištěnou příčinu a bez viditelného
výpisu se na exportní cestě nedá bezpečně pracovat.

Dnes se diagnostický výpis staví v `BaseController` a dostane ho **jen AJAX cesta**
(odesílání formulářů v modalech). Obyčejný GET, který spadne, skončí na holé stránce
s jediným Request ID. Formát výpisu se vytáhne do sdílené komponenty, aby ho obě cesty
měly stejný, a chybová stránka ho vykreslí.

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Diagnostics/DiagnosticLogBuilder.cs`
- Vytvořit: `PmTracker.Web/wwwroot/js/modules/ui/errorPageCopy.js`
- Upravit: `PmTracker.Web/Controllers/BaseController.Ajax.cs` (řádky 94, 121–161, 172, 271)
- Upravit: `PmTracker.Web/Controllers/HomeController.cs`
- Upravit: `PmTracker.Web/Models/ViewModels/ErrorViewModel.cs`
- Upravit: `PmTracker.Web/Views/Shared/Error.cshtml`
- Upravit: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`
- Test: `PmTracker.Tests.Unit/Diagnostics/DiagnosticLogBuilderTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/Diagnostics/ErrorPageDiagnosticsTests.cs` (nový)

**Rozhraní:**
- Produkuje: `PmTracker.Web.Services.Diagnostics.DiagnosticLogBuilder.Build(DiagnosticLogRequest) → string`
  a záznam `DiagnosticLogRequest(DateTime TimestampUtc, string ErrorCode, string TraceId,
  string RequestLine, string Message, IReadOnlyDictionary<string, string[]>? FieldErrors = null,
  string? Details = null, Exception? Exception = null)`.
- Produkuje: `ErrorViewModel { RequestId, Message, ErrorCode, DiagnosticLog, ShowRequestId, ShowDiagnostics }`.

- [x] **Krok 1: Napsat padající test na sdílený builder**

`PmTracker.Tests.Unit/Diagnostics/DiagnosticLogBuilderTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Diagnostics;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// Formát diagnostického výpisu je jeden pro AJAX i pro chybovou stránku. Kdyby se
/// rozešly, uživatel by posílal dvě různé podoby téhož a hledalo by se v tom hůř.
/// </summary>
public sealed class DiagnosticLogBuilderTests
{
    [Fact]
    public void Build_ObsahujeKontextIVyjimku()
    {
        var text = DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
            TimestampUtc: new DateTime(2026, 9, 8, 10, 30, 0, DateTimeKind.Utc),
            ErrorCode: "UNHANDLED_EXCEPTION",
            TraceId: "trace-abc",
            RequestLine: "GET /Export/Vyzva/5/Tisk?projektId=2",
            Message: "Tisk selhal.",
            Exception: new InvalidOperationException("boom")));

        text.Should().Contain("2026-09-08");
        text.Should().Contain("UNHANDLED_EXCEPTION");
        text.Should().Contain("trace-abc");
        text.Should().Contain("GET /Export/Vyzva/5/Tisk?projektId=2",
            "bez cesty požadavku není poznat, co spadlo");
        text.Should().Contain("Tisk selhal.");
        text.Should().Contain("InvalidOperationException").And.Contain("boom");
    }

    /// <summary>
    /// QW-7 (2026-04-22) odstranil z výpisu hodnoty formuláře kvůli úniku osobních údajů.
    /// Přesun do sdílené komponenty to nesmí vrátit zpátky.
    /// </summary>
    [Fact]
    public void Build_NeobsahujeSekciSHodnotamiFormulare()
    {
        var text = DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
            TimestampUtc: DateTime.UtcNow,
            ErrorCode: "OPERATION_FAILED",
            TraceId: "t",
            RequestLine: "POST /Projekty/SaveProject",
            Message: "chyba"));

        text.Should().NotContain("FormValues", "hodnoty formuláře nesou osobní údaje");
    }
}
```

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~DiagnosticLogBuilderTests"`
Očekávej: chybu překladu `CS0246` — typ `DiagnosticLogBuilder` neexistuje.

- [x] **Krok 3: Vytvořit sdílenou komponentu**

`PmTracker.Web/Services/Diagnostics/DiagnosticLogBuilder.cs`:

```csharp
using System.Text;

namespace PmTracker.Web.Services.Diagnostics;

/// <summary>Vstup pro sestavení diagnostického výpisu.</summary>
public sealed record DiagnosticLogRequest(
    DateTime TimestampUtc,
    string ErrorCode,
    string TraceId,
    string RequestLine,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Details = null,
    Exception? Exception = null);

/// <summary>
/// Skládá diagnostický výpis, který uživatel vidí u chyby a může ho zkopírovat.
/// Vytaženo 2026-09-08 z BaseController, kde ho měla jen AJAX cesta — obyčejný GET,
/// který spadl, končil na holé stránce bez jediného detailu.
///
/// Hodnoty formuláře se do výpisu záměrně nedávají: QW-7 (2026-04-22) je odstranil
/// kvůli úniku osobních údajů a strážní test to hlídá.
/// </summary>
public static class DiagnosticLogBuilder
{
    public static string Build(DiagnosticLogRequest request)
    {
        var builder = new StringBuilder(2048);
        builder.Append("TimestampUtc: ").AppendLine(request.TimestampUtc.ToString("O"));
        builder.Append("ErrorCode: ").AppendLine(request.ErrorCode);
        builder.Append("TraceId: ").AppendLine(request.TraceId);
        builder.Append("Request: ").AppendLine(request.RequestLine);
        builder.Append("Message: ").AppendLine(request.Message);

        if (!string.IsNullOrWhiteSpace(request.Details))
        {
            builder.AppendLine("Details:");
            builder.AppendLine(request.Details.Trim());
        }

        if (request.FieldErrors is not null)
        {
            AppendFieldErrorSection(builder, request.FieldErrors);
        }

        if (request.Exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(request.Exception.ToString());
            AppendSqlAndEfDetails(builder, request.Exception);
        }

        return builder.ToString().TrimEnd();
    }
}
```

Do téže třídy **beze změny těla** přesuň dvě privátní statické metody z
`BaseController.Ajax.cs` a udělej je `private static` v novém souboru:

- `AppendFieldErrorSection(StringBuilder builder, IReadOnlyDictionary<string, string[]> fieldErrors)` — dnes řádek 94
- `AppendSqlAndEfDetails(StringBuilder builder, Exception rootException)` — dnes řádek 172

Obě se z `BaseController.Ajax.cs` smažou.

- [x] **Krok 4: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~DiagnosticLogBuilderTests"`
Očekávej: 2 úspěšné.

- [x] **Krok 5: Přepojit BaseController na sdílený builder**

V `BaseController.Ajax.cs` nahraď tělo `BuildDiagnosticLog` delegací. Metoda si ponechá
svou signaturu, aby volání na řádku 271 zůstalo beze změny:

```csharp
private string BuildDiagnosticLog(
    string errorCode,
    string traceId,
    string message,
    IReadOnlyDictionary<string, string[]> fieldErrors,
    string? details,
    Exception? exception)
    => DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
        TimestampUtc: _timeProvider.GetUtcNow().UtcDateTime,
        ErrorCode: errorCode,
        TraceId: traceId,
        RequestLine: $"{HttpContext.Request.Method} {HttpContext.Request.Path}{HttpContext.Request.QueryString}",
        Message: message,
        FieldErrors: fieldErrors,
        Details: details,
        Exception: exception));
```

- [x] **Krok 6: Ověřit, že se AJAX cesta nezměnila**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~BaseControllerAjaxPrivacyTests"`
Očekávej: 2 úspěšné. Tyto testy hlídají, že klient výpis dostává a nejsou v něm osobní údaje.

- [x] **Krok 7: Napsat padající test na chybovou akci**

`PmTracker.Tests.Unit/Diagnostics/ErrorPageDiagnosticsTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using Xunit;

namespace PmTracker.Tests.Unit.Diagnostics;

/// <summary>
/// Po pádu už akce běží na /Home/Error, takže naivně sestavená diagnostika by hlásila
/// cestu /Home/Error a byla by k ničemu. Původní cesta se musí brát
/// z IExceptionHandlerPathFeature, kterou naplní UseExceptionHandler.
/// </summary>
public sealed class ErrorPageDiagnosticsTests
{
    private static HomeController Controller(HttpContext context)
    {
        var resolver = new Mock<IUserContextResolver>();
        var controller = new HomeController(
            resolver.Object, TimeProvider.System, NullLoggerFactory.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
        return controller;
    }

    [Fact]
    public void Error_NeseDiagnostikuSPuvodniCestouAVyjimkou()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-xyz" };
        context.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Path = "/Export/Vyzva/5/Word",
            Error = new InvalidOperationException("boom"),
        });
        context.Request.Method = "GET";

        var result = Controller(context).Error().Should().BeOfType<ViewResult>().Subject;
        var vm = result.Model.Should().BeOfType<ErrorViewModel>().Subject;

        vm.RequestId.Should().Be("trace-xyz");
        vm.ShowDiagnostics.Should().BeTrue();
        vm.DiagnosticLog.Should().Contain("/Export/Vyzva/5/Word",
            "diagnostika musí ukázat cestu, která spadla, ne /Home/Error");
        vm.DiagnosticLog.Should().Contain("InvalidOperationException").And.Contain("boom");
    }

    /// <summary>
    /// Bez výjimky (uživatel si stránku otevřel přímo) se panel nenabízí — prázdný
    /// výpis by jen mátl.
    /// </summary>
    [Fact]
    public void Error_BezVyjimky_Nediagnostikuje()
    {
        var vm = Controller(new DefaultHttpContext { TraceIdentifier = "t" })
            .Error().Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<ErrorViewModel>().Subject;

        vm.ShowDiagnostics.Should().BeFalse();
    }
}
```

- [x] **Krok 8: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ErrorPageDiagnosticsTests"`
Očekávej: chybu překladu — `ErrorViewModel` nemá `ShowDiagnostics` ani `DiagnosticLog`.

- [x] **Krok 9: Rozšířit view model**

`PmTracker.Web/Models/ViewModels/ErrorViewModel.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels;

public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }

    /// <summary>Kopírovatelný výpis ve stejném formátu, jaký ukazují chyby v modalech.</summary>
    public string? DiagnosticLog { get; init; }

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
    public bool ShowDiagnostics => !string.IsNullOrWhiteSpace(DiagnosticLog);
}
```

- [x] **Krok 10: Sestavit diagnostiku v chybové akci**

V `HomeController` nahraď akci `Error`:

```csharp
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public IActionResult Error()
{
    // Po pádu běží akce už na /Home/Error. Původní cestu i výjimku drží feature,
    // kterou naplnil UseExceptionHandler — bez ní by diagnostika hlásila /Home/Error.
    var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
    var traceId = HttpContext.TraceIdentifier;

    if (feature?.Error is null)
    {
        return View(new ErrorViewModel { RequestId = traceId });
    }

    return View(new ErrorViewModel
    {
        RequestId = traceId,
        ErrorCode = "UNHANDLED_EXCEPTION",
        Message = "Požadavek se nepodařilo zpracovat.",
        DiagnosticLog = DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
            TimestampUtc: GetUtcNow(),
            ErrorCode: "UNHANDLED_EXCEPTION",
            TraceId: traceId,
            RequestLine: $"{HttpContext.Request.Method} {feature.Path}",
            Message: "Neošetřená výjimka při zpracování požadavku.",
            Exception: feature.Error)),
    });
}
```

Doplň `using Microsoft.AspNetCore.Diagnostics;` a `using PmTracker.Web.Services.Diagnostics;`.

`BaseController` dnes nabízí jen `GetLocalNow()` a pole `TimeProvider` má privátní, takže
`HomeController` se k UTC času nedostane. Přidej proto do `PmTracker.Web/Controllers/BaseController.cs`
hned pod `GetLocalNow()` (řádek 31) souseda:

```csharp
protected DateTime GetUtcNow()
{
    return _timeProvider.GetUtcNow().UtcDateTime;
}
```

Čas se v celé aplikaci bere z `TimeProvider`, nikdy z `DateTime.UtcNow` — testy si ho
podvrhují a přímé volání by je obešlo.

- [x] **Krok 11: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ErrorPageDiagnosticsTests"`
Očekávej: 2 úspěšné.

- [x] **Krok 12: Vykreslit panel na chybové stránce**

`PmTracker.Web/Views/Shared/Error.cshtml` — třídy jsou schválně stejné jako u panelu
v modalech (`modal-submit-diagnostics*`), aby se vzhled i CSS sdílely:

```cshtml
@model ErrorViewModel
@{
    ViewData["Title"] = "Chyba";
}

<section class="card">
    <h1>Došlo k chybě</h1>
    <p>@(Model.Message ?? "Omlouváme se, požadavek se nepodařilo zpracovat.")</p>

    @if (Model.ShowRequestId)
    {
        <p class="muted">Request ID: <span>@Model.RequestId</span></p>
    }

    @if (Model.ShowDiagnostics)
    {
        <details class="modal-submit-diagnostics" data-error-diagnostics>
            <summary>Diagnostický log</summary>
            <div class="modal-submit-diagnostics-actions">
                <button type="button" class="modal-submit-action-btn" data-error-copy>Kopírovat log</button>
                <button type="button" class="modal-submit-action-btn" data-error-save>Uložit log chyby</button>
            </div>
            <pre class="modal-submit-diagnostics-log" data-error-log>@Model.DiagnosticLog</pre>
        </details>
    }
</section>
```

- [x] **Krok 13: Napojit tlačítka**

`PmTracker.Web/wwwroot/js/modules/ui/errorPageCopy.js`:

```javascript
// Kopírování diagnostiky na chybové stránce. Panel v modalech si staví ajax.js sám
// z JSON odpovědi; tady je HTML vyrenderované serverem, takže stačí obsluha tlačítek.
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { copyTextToClipboard } from "../utils.js";

function log() {
    const el = document.querySelector("[data-error-log]");
    return el instanceof HTMLElement ? el.textContent || "" : "";
}

document.addEventListener("click", async (event) => {
    const target = event.target instanceof Element ? event.target : null;

    const copy = target?.closest("[data-error-copy]");
    if (copy instanceof HTMLElement) {
        const copied = await copyTextToClipboard(log());
        copy.textContent = copied ? "Zkopírováno" : "Kopírování selhalo";
        window.setTimeout(() => { copy.textContent = "Kopírovat log"; }, 1800);
        return;
    }

    const save = target?.closest("[data-error-save]");
    if (save instanceof HTMLElement) {
        const trace = document.querySelector("[data-error-diagnostics]")
            ? (document.title || "chyba") : "chyba";
        // BOM, ať Notepad otevře UTF-8 a nehádá ANSI — stejně jako ajax.js.
        const blob = new Blob(["﻿" + log()], { type: "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `${trace.replace(/[\\/:*?"<>|\s]+/g, "_")}.txt`;
        a.click();
        URL.revokeObjectURL(url);
    }
});
```

Do `bootstrap.js` přidej k ostatním side-effect importům:

```javascript
import "./ui/errorPageCopy.js";                     // 2026-09-08 — kopírování diagnostiky na chybové stránce.
```

- [x] **Krok 14: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
node --check PmTracker.Web/wwwroot/js/modules/ui/errorPageCopy.js
```

Očekávej: build 0 chyb a 0 upozornění, Unit sada zelená (1680 + 4 nové = 1684),
`node --check` bez výstupu. Aplikace nemá JS test runner, takže překlep by se jinak
projevil až tichým selháním v prohlížeči.

**Po tomto bloku:** zopakuj tisk výzvy, otevři chybovou stránku a zkopíruj výpis.
Teprve se známou příčinou pokračuj — a pokud ukáže vadu v exportní cestě, oprav ji
dřív, než se sáhne na bloky 7 a 8.

---
## Blok 2: Tisk jen do Wordu, vedle něj Náhled

Spec §4. Výběr formátu u výzvy mizí; zůstávají dvě tlačítka, zleva **Náhled**, pak
**Tisk výzvy**. Sdílený chooser (`ui/print.js`) se nemění — používá ho dalších pět míst
(tisk projektu, záznamu, úkolu, jednání).

**Soubory:**
- Upravit: `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml` (patička, dnes řádky ~130–145)
- Test: `PmTracker.Tests.Api/Controllers/VyzvyPanelLayoutTests.cs` (existující, přidat testy)

**Rozhraní:**
- Konzumuje: endpointy `Export.VyzvaTisk` a `Export.VyzvaWord` beze změny.
- Produkuje: hooky `data-vyzva-nahled` a `data-vyzva-word` pro Api testy.

- [x] **Krok 1: Napsat padající testy**

Do `PmTracker.Tests.Api/Controllers/VyzvyPanelLayoutTests.cs` přidej:

```csharp
    /// <summary>
    /// Výběr formátu u výzvy zrušen 2026-09-08 — pracovník vždycky potřebuje Word.
    /// Náhled zůstává a otevírá tiskovou podobu bez dialogu tisku.
    /// </summary>
    [Fact]
    public async Task Paticka_MaNahledPredTiskem_ABezVyberuFormatu()
    {
        var s = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadAsync(s.ProjectId), s.PripravaId);

        var nahled = pane.IndexOf("data-vyzva-nahled", StringComparison.Ordinal);
        var word = pane.IndexOf("data-vyzva-word", StringComparison.Ordinal);

        nahled.Should().BeGreaterThan(-1, "náhled musí být vyrenderovaný");
        word.Should().BeGreaterThan(nahled, "Náhled je hned vlevo vedle Tisku");

        pane.Should().NotContain("data-print-trigger",
            "výběr PDF/Word u výzvy zrušen — zbyl jen Word");
    }

    [Fact]
    public async Task Buffer_NemaAniNahledAniTisk()
    {
        var s = await SeedAsync();
        var buffer = VyzvyPanelHtml.BufferPane(await LoadAsync(s.ProjectId));

        buffer.Should().NotContain("data-vyzva-nahled");
        buffer.Should().NotContain("data-vyzva-word", "buffer není dokument");
    }
```

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyPanelLayoutTests"`
Očekávej: dva nové testy selžou — hooky zatím neexistují a `data-print-trigger` v patičce je.

- [x] **Krok 3: Přepsat patičku**

V `_VyzvyPane.cshtml` nahraď blok `@if (Model.MuzeTisknout) { … }` uvnitř
`.vyzvy-pane-footer` tímto:

```cshtml
            @if (Model.MuzeTisknout)
            {
                @* Výběr formátu zrušen 2026-09-08 — do spisové služby jde vždy Word.
                   Náhled otevře tiskovou podobu v nové kartě; endpoint vrací PDF jako
                   inline, takže se zobrazí v prohlížeči a dialog tisku nevyskočí. *@
                <a class="pm-btn pm-btn--secondary pm-btn--small"
                   data-vyzva-nahled
                   target="_blank"
                   rel="noopener"
                   href="@Url.Action("VyzvaTisk", "Export", new { vyzvaId = Model.Vyzva.Id, projektId = Model.ProjektId })">Náhled</a>

                <a class="pm-btn pm-btn--primary pm-btn--small"
                   data-vyzva-word
                   href="@Url.Action("VyzvaWord", "Export", new { vyzvaId = Model.Vyzva.Id, projektId = Model.ProjektId })">Tisk výzvy</a>
            }
```

Použij prostý `<a>`, ne `pm-button`: ten strhává atributy z hostu, takže `target="_blank"`
by se neuplatnil a náhled by se otevřel přes stávající stránku.

- [x] **Krok 4: Doplnit vzhled odkazů jako tlačítek**

Pokud třídy `.pm-btn`, `.pm-btn--primary`, `.pm-btn--secondary`, `.pm-btn--small`
v `PmTracker.Web/wwwroot/css/` neexistují, ověř grepem:

```bash
grep -rn "\.pm-btn" PmTracker.Web/wwwroot/css/ | head
```

Když nejsou, přidej do `components/vyzvy-panel.css` minimální vzhled na gov tokenech —
nezaváděj novou obecnou sadu tříd, drž se jmen `.vyzvy-pane-footer a`:

```css
/* Náhled a Tisk jsou odkazy, ne pm-button: ten strhává atributy z hostu, takže by
   se neuplatnil target="_blank" a náhled by přebil stávající stránku. */
.vyzvy-pane-footer a {
  display: inline-flex;
  align-items: center;
  padding: 0.35rem 0.75rem;
  border: 1px solid var(--gov-color-border);
  border-radius: var(--gov-radius-sm);
  background: var(--gov-color-surface);
  color: inherit;
  font-size: 0.875rem;
  font-weight: 600;
  cursor: pointer;
}

.vyzvy-pane-footer a:hover { border-color: var(--gov-color-primary); }

.vyzvy-pane-footer a[data-vyzva-word] {
  border-color: var(--gov-color-primary);
  box-shadow: inset 0 0 0 1px var(--gov-color-primary);
}
```

V takovém případě z kroku 3 vypusť třídy `pm-btn*` a nech jen `data-*` atributy.

- [x] **Krok 5: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyPanelLayoutTests"`
Očekávej: 10 úspěšných (8 stávajících + 2 nové).

- [x] **Krok 6: Ověřit, že chooser nikde jinde nezmizel**

```bash
grep -rn "data-print-trigger" PmTracker.Web/Views --include="*.cshtml"
```

Očekávej přesně pět výskytů: `ZaznamDetailPage.cshtml`, `Detail.cshtml`,
`_ZaznamPartial.cshtml`, `_MeetingCard.cshtml`, `Jednani/Detail.cshtml`.
Výzva mezi nimi **být nesmí**.

- [x] **Krok 7: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
```

Očekávej: build čistý, Api 357/361 (jen 4 známá selhání).

---

## Blok 3: Sloupec pro text požadavku

Spec §5.1 a §5.2. Samotné schéma, bez chování — díky tomu jde blok ověřit samostatně.

**Soubory:**
- Upravit: `PmTracker.Web/Models/Entities/…` (třída `ZaznamExterniOdkazEntity`)
- Upravit: `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` (kolem řádku 249)
- Vytvořit: `db_upgrade_1_4_2_externi_odkaz_pozadavek.sql`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazPozadavekMappingTests.cs` (nový)

**Rozhraní:**
- Produkuje: `ZaznamExterniOdkazEntity.Pozadavek` typu `string?`, sloupec `pozadavek`.

- [x] **Krok 1: Napsat padající test na mapování**

`PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazPozadavekMappingTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// Text požadavku patří k vazbě, ne k záznamu — jeden záznam může mít víc PNF
/// a každé svůj požadavek (spec 2026-09-08 §5.1).
/// </summary>
public sealed class ExterniOdkazPozadavekMappingTests
{
    private static PmTrackerDbContext CreateDb()
        => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Pozadavek_SeMapujeNaSloupecPozadavek()
    {
        using var db = CreateDb();

        var property = db.Model
            .FindEntityType(typeof(ZaznamExterniOdkazEntity))!
            .FindProperty(nameof(ZaznamExterniOdkazEntity.Pozadavek));

        property.Should().NotBeNull("bez vlastnosti se text nemá kam uložit");
        property!.GetColumnName().Should().Be("pozadavek");
        property.IsNullable.Should().BeTrue(
            "stávající vazby zůstávají prázdné, žádná migrace dat se nedělá");
    }
}
```

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ExterniOdkazPozadavekMappingTests"`
Očekávej: chybu překladu — `ZaznamExterniOdkazEntity` nemá `Pozadavek`.

- [x] **Krok 3: Doplnit vlastnost a mapování**

Do třídy `ZaznamExterniOdkazEntity` za `public bool ZaradidDoVyzvy { get; set; }`:

```csharp
    /// <summary>
    /// Text požadavku do výzvy, sanitizované HTML z rich text editoru (spec 2026-09-08).
    /// NULL u vazeb založených před 2026-09-08 — nedoplňuje se, pracovník si je vyplní
    /// podle potřeby. Prázdno i NULL znamenají ve výzvě žádný text; popis tiketu se
    /// jako náhrada nepoužívá, celý smysl pole je brát text odjinud než z tiketu.
    /// Harvest ze ServiceDesku se tohoto pole nikdy nedotkne.
    /// </summary>
    public string? Pozadavek { get; set; }
```

Do `RecordEntityConfiguration.cs` za řádek se `zaradid_do_vyzvy`:

```csharp
        builder.Property(x => x.Pozadavek).HasColumnName("pozadavek");
```

- [x] **Krok 4: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ExterniOdkazPozadavekMappingTests"`
Očekávej: 1 úspěšný.

- [x] **Krok 5: Napsat migrační skript**

`db_upgrade_1_4_2_externi_odkaz_pozadavek.sql` v kořeni repa:

```sql
-- 1.4.2 — text požadavku u externí vazby (spec 2026-09-08-vyzva-pozadavek-text-design §5.2)
--
-- Text, který se u PNF dostane do výzvy. Píše ho pracovník v kartě vazby, ukládá se
-- jako sanitizované HTML z rich text editoru.
--
-- Stávající řádky zůstávají NULL zcela záměrně: popis tiketu se nikam nedoplňuje
-- a pracovník si vazby vyplní podle potřeby (zadání uživatele 2026-09-08).
--
-- Skript je idempotentní — nasazení je offline a spouští ho ručně administrátor.

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.zaznam_externi_odkazy')
                 AND name = 'pozadavek')
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy ADD pozadavek NVARCHAR(MAX) NULL;
    PRINT 'zaznam_externi_odkazy.pozadavek pridan';
END
ELSE
BEGIN
    PRINT 'zaznam_externi_odkazy.pozadavek uz existuje - preskoceno';
END
```

- [x] **Krok 6: Ověřit idempotenci a zapsání do dokumentace**

Zkontroluj, zda repo vede seznam aplikovaných skriptů:

```bash
ls db_check_applied_upgrades.sql 2>/dev/null && grep -n "1_4_1" db_check_applied_upgrades.sql
grep -rn "1_4_1" docs/ --include="*.md" | head
```

Kdekoliv je 1.4.1 vyjmenovaná, doplň stejným způsobem 1.4.2. Bez toho by
administrátor při nasazení nevěděl, že má nový skript pustit.

- [x] **Krok 7: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
```

Očekávej: build čistý, Unit zelená.

---
## Blok 4: Ukládání textu a ochrana před harvestem

Spec §5.5 a §5.6. Text se ukládá spolu se záznamem, prochází sanitizací a harvest ze
ServiceDesku se ho nikdy nedotkne.

**Soubory:**
- Upravit: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` (řádky 99–112)
- Upravit: `PmTracker.Web/Services/RecordService.SaveRecord.cs` (UPSERT, řádky 1157–1195)
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs` (existující, zdrojové piny)
- Test: `PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs` (existující, chování nad DB)

**Pozor na povahu obou souborů:** `RecordServiceExternalLinkUpsertTests` **nemá databázový
harness** — jsou to piny nad zdrojovým textem služby (`LoadServiceSource()`). Chování
uložení se ověřuje v Integration sadě, kde je `store.SaveRecord(new SaveRecordCommand { … })`.

**Rozhraní:**
- Konzumuje: `ZaznamExterniOdkazEntity.Pozadavek` z bloku 3.
- Konzumuje: `richTextContentService` — privátní pole `RecordService` typu
  `IRichTextContentService` s metodami `NormalizeForStorage`, `ToSafeHtml`,
  `ToPlainText`, `HasVisibleText`.
- Produkuje: `SaveRecordExterniVazbaCommand.Pozadavek` typu `string?`.

- [x] **Krok 1a: Napsat padající zdrojový pin**

Do `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs` přidej
ve stylu ostatních testů toho souboru:

```csharp
    /// <summary>
    /// Text požadavku je pole formuláře, takže ho UPSERT musí propsat v OBOU větvích —
    /// při úpravě existující vazby i při založení nové. Vynechání jedné z nich by se
    /// projevilo jako „text se občas neuloží" (spec 2026-09-08 §5.5).
    /// </summary>
    [Fact]
    public void ReplaceRecordExternalLinksAsync_UkladaPozadavekVObouVetvich()
    {
        var source = LoadServiceSource();
        var methodIndex = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        methodIndex.Should().BeGreaterThan(0);

        var methodSlice = source[methodIndex..Math.Min(methodIndex + 6000, source.Length)];

        methodSlice.Should().Contain("existingEntity.Pozadavek = pozadavek",
            "úprava existující vazby musí text přepsat");
        methodSlice.Should().Contain("Pozadavek = pozadavek,",
            "nová vazba musí text uložit rovnou při založení");
        methodSlice.Should().Contain("HasVisibleText",
            "prázdný odstavec z Quillu (<p><br></p>) se nesmí uložit jako text");
    }
```

- [x] **Krok 1b: Napsat padající test chování nad databází**

Do `PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs` přidej. Pomocné
metody a tvar `SaveRecordCommand` okopíruj z prvního testu v tomtéž souboru
(`SaveRecord_ShouldPersistFiveHundredCharacterGoal_WithPreservedLineBreak`), ať se seed
projektu, kategorie a subsystému dělá stejně:

```csharp
    /// <summary>
    /// Text požadavku se ukládá k vazbě a přežije opětovné uložení záznamu. UPSERT drží
    /// Id, takže FK z vyjadreni_vazby zůstanou platné (memory feedback_replace_upsert_for_audit_fk).
    /// </summary>
    [Fact]
    public async Task SaveRecord_UlozitAZachovatTextPozadavkuUPnfVazby()
    {
        // Seed projektu a záznamu stejně jako v prvním testu souboru.
        // Ulož záznam s jednou PNF vazbou:
        //     ExterniVazby = { new SaveRecordExterniVazbaCommand {
        //         Typ = "PNF", Cislo = "336865",
        //         Pozadavek = "<p>Chceme sestavu.</p>" } }
        // Načti vazbu z DB a ověř:
        //     vazba.Pozadavek.Should().Contain("Chceme sestavu.");
        // Ulož znovu s Id té vazby a Pozadavek = "<p>Nove zadani.</p>":
        //     Id vazby se nesmí změnit a Pozadavek musí být "Nove zadani.".
    }
```

Tenhle jeden test plán záměrně nechává v této podobě: soubor seeduje projekt, kategorii,
subsystém a osobu vlastní dlouhou přípravou, kterou nemá smysl v plánu opisovat — okopíruj
ji z prvního testu a doplň jen tři řádky navíc. Zbytek plánu obsahuje testy celé.

- [x] **Krok 2: Spustit a ověřit selhání**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordServiceExternalLinkUpsertTests"
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~SaveRecord_UlozitAZachovatTextPozadavkuUPnfVazby"
```

Očekávej: zdrojový pin selže (v UPSERT nic o `Pozadavek` není) a Integration test se
nepřeloží, protože `SaveRecordExterniVazbaCommand` nemá `Pozadavek`.

- [x] **Krok 3: Doplnit pole do příkazu**

V `RecordCommands.cs` do `SaveRecordExterniVazbaCommand` za `ZaradidDoVyzvy`:

```csharp
    /// <summary>
    /// Text požadavku do výzvy (spec 2026-09-08). Rich text HTML z Quillu; před uložením
    /// projde sanitizací. Renderuje se jen u PNF, u ostatních typů zůstane null.
    /// </summary>
    public string? Pozadavek { get; set; }
```

- [x] **Krok 4: Uložit text v UPSERT větvi**

V `RecordService.SaveRecord.cs` v metodě `ReplaceRecordExternalLinksAsync` doplň nad
smyčkové větve normalizaci a v obou větvích přiřazení.

Za řádek `var cislo = link.Cislo!.Trim();` přidej:

```csharp
            // Rich text projde sanitizací stejně jako popis záznamu. HasVisibleText
            // odfiltruje prázdný odstavec z Quillu (<p><br></p>), který by se do výzvy
            // vytiskl jako prázdné místo.
            var pozadavek = richTextContentService.HasVisibleText(link.Pozadavek)
                ? richTextContentService.NormalizeForStorage(link.Pozadavek)
                : null;
```

Do větve `if (link.Id > 0 && existingById.TryGetValue(link.Id, out var existingEntity))`
za `existingEntity.VyzvaId = vyzvaId;`:

```csharp
                existingEntity.Pozadavek = pozadavek;
```

Do inicializátoru nové entity za `VyzvaId = vyzvaId`:

```csharp
                    Pozadavek = pozadavek,
```

- [x] **Krok 5: Spustit a ověřit průchod**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordServiceExternalLinkUpsertTests"
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~SaveRecord_UlozitAZachovatTextPozadavkuUPnfVazby"
```

Očekávej: obojí zelené.

- [x] **Krok 6: Napsat test, že harvest text nepřepíše**

`PmTracker.Tests.Unit/ExterniOdkaz/HarvestNeprepisujePozadavekTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// Po prvním vyplnění je jediným zdrojem pravdy pracovník, ne tiket (spec §5.6, R8).
/// Harvest ze ServiceDesku píše čtyři datumy a fingerprint — pole `pozadavek` mezi nimi
/// být nesmí. Tudy by se přepis vloudil nejsnáz, proto pin přímo na zdroj.
/// </summary>
public sealed class HarvestNeprepisujePozadavekTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void HarvestSluzby_NesahajiNaPozadavek()
    {
        var sluzby = Directory.GetFiles(
            Path.Combine(RepoRoot(), "PmTracker.Web/Services"),
            "*Harvest*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(
                Path.Combine(RepoRoot(), "PmTracker.Web/Services"),
                "PerTicketMetadata*.cs", SearchOption.AllDirectories))
            .ToArray();

        sluzby.Should().NotBeEmpty("bez nalezených služeb by test nic nehlídal");

        foreach (var soubor in sluzby)
        {
            File.ReadAllText(soubor).Should().NotContain(".Pozadavek",
                $"{Path.GetFileName(soubor)} nesmí přepsat text, který napsal pracovník");
        }
    }
}
```

- [x] **Krok 7: Spustit a ověřit**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~HarvestNeprepisujePozadavekTests"`
Očekávej: 1 úspěšný. Test má projít hned — jde o pin, ne o změnu chování. Ověř ho tím,
že do libovolné harvest služby dočasně napíšeš `entity.Pozadavek = null;`, spustíš test
(musí selhat) a řádek zase smažeš.

- [x] **Krok 8: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj
```

Očekávej: build čistý, Unit zelená, Integration 85/86 (jen známé selhání).
Integration sada je tu podstatná — hlídá, že uložení nerozbije cizí klíče z audit tabulek.

---

## Blok 5: Editor v kartě vazby

Spec §5.3. Rich text pole pod čtyřmi datumy, jen u PNF, na existující Quill infrastruktuře.

**Soubory:**
- Upravit: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs` (třída `ExterniOdkazEditViewModel`, řádky 120–134)
- Upravit: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs`
- Upravit: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs`
- Upravit: `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml`
- Upravit: `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css`
- Test: `PmTracker.Tests.Api/Controllers/ExterniOdkazPozadavekRenderTests.cs` (nový)

**Rozhraní:**
- Konzumuje: `textarea[data-rich-text="true"]`, kterou povyšuje
  `wwwroot/js/modules/recordEditor/richtext.js` na Quill. Hodnota se synchronizuje
  zpět do textarey jako HTML, takže se odešle běžným model bindingem.
- Produkuje: `ExterniOdkazEditViewModel.Pozadavek`, pole formuláře
  `ExterniVazby[i].Pozadavek`, hook `data-external-pozadavek-field`.

- [x] **Krok 1: Napsat padající Api testy**

`PmTracker.Tests.Api/Controllers/ExterniOdkazPozadavekRenderTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Rich text pole s textem požadavku se renderuje pod datumy a jen u PNF
/// (spec 2026-09-08 §5.3). Assertace se kotví na atributy — Razor kóduje diakritiku
/// na číselné entity, takže na český text se spolehnout nedá.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExterniOdkazPozadavekRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ExterniOdkazPozadavekRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Typ vazby je u nově zadávaného tiketu ještě neznámý, takže se pole renderuje vždy
    /// a schovává atributem — odhalí ho sync.js po dohledání tiketu. Stejný postup,
    /// jakým se řídí pole ceny a přepínač výzvy.
    /// </summary>
    [Fact]
    public async Task Editor_RenderujePolePozadavku_SHookemARichTextem()
    {
        var html = await NactiEditorZaznamuSPnfVazbouAsync();

        html.Should().Contain("data-external-pozadavek-field",
            "pole musí mít hook, přes který ho sync.js odhalí u PNF");
        html.Should().Contain("data-rich-text=\"true\"",
            "editor jede na existující Quill infrastruktuře, ne na vlastním setupu");
        html.Should().Contain("ExterniVazby[0].Pozadavek",
            "bez správného jména se hodnota neodešle model binderem");
    }

    [Fact]
    public async Task Editor_PolePozadavkuJePodDatumy()
    {
        var html = await NactiEditorZaznamuSPnfVazbouAsync();

        var datumy = html.IndexOf("external-dates", StringComparison.Ordinal);
        var pozadavek = html.IndexOf("data-external-pozadavek-field", StringComparison.Ordinal);

        datumy.Should().BeGreaterThan(-1);
        pozadavek.Should().BeGreaterThan(datumy, "pole patří pod stávající údaje karty");
    }

    /// <summary>
    /// U vazby, která PNF není, je pole schované. Nerenderovat ho vůbec nejde: typ je
    /// u nově zadávaného tiketu ještě neznámý a odhaluje ho až sync.js.
    /// </summary>
    [Fact]
    public async Task Editor_UNesVazby_MaPoleSchovane()
    {
        var html = await NactiEditorZaznamuSNesVazbouAsync();

        var index = html.IndexOf("data-external-pozadavek-field", StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, "pole se renderuje vždy, jen se schovává");

        var tag = html[..html.IndexOf('>', index)];
        tag[tag.LastIndexOf('<')..].Should().Contain("hidden",
            "u jiného typu než PNF se text požadavku nenabízí");
    }
}
```

Kromě `NactiEditorZaznamuSPnfVazbouAsync` napiš i `NactiEditorZaznamuSNesVazbouAsync` —
totéž, jen s typem vazby NES a jiným markerem v názvu záznamu, aby si seedy dvou testů
nešláply na jednu databázi.

Pomocnou metodu `NactiEditorZaznamuSPnfVazbouAsync` napiš podle vzoru
`PmTracker.Tests.Api/Controllers/RecordEditorControllerTests.cs` — seeduj projekt,
záznam a PNF vazbu a stáhni HTML editoru záznamu. Seed musí být idempotentní: Api testy
sdílí jednu databázi, takže záznam nejdřív dohledej podle markeru v názvu a teprve
když není, založ ho (`ApiSqlFixture.EnsureRecordAsync` zakládá nový vždy, navzdory jménu).

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExterniOdkazPozadavekRenderTests"`
Očekávej: oba testy selžou — hook v HTML není.

- [x] **Krok 3: Doplnit pole do view modelu a obou míst, která ho plní**

Do `ExterniOdkazEditViewModel` za `LastHarvestedAt`:

```csharp
    /// <summary>Text požadavku do výzvy — sanitizované HTML, jen u PNF (spec 2026-09-08).</summary>
    public string? Pozadavek { get; set; }
```

Ten view model plní **dvě** místa a obě je nutné doplnit:
`ProjectService.RecordEditorComposition.cs` a `Records/RecordProposalPayloadMapper.cs`.
Najdi je grepem a přidej `Pozadavek = …` k ostatním vlastnostem:

```bash
grep -rn "new ExterniOdkazEditViewModel" PmTracker.Web/Services/
```

Vynechání druhého místa je zavedená past tohoto repa: unit test nad builderem projde,
ale editor vyrenderuje prázdno (memory `feedback_harmonogram_krok_vm_remap`). Proto jsou
kontrolou Api render testy z kroku 1, ne unit test.

- [x] **Krok 4: Vykreslit editor v kartě**

V `_EditZaznamExternalPanel.cshtml` **za** blok `<div class="external-dates …">` a před
uzavírací `</div>` řádku vazby:

```cshtml
                    @* Text požadavku do výzvy (spec 2026-09-08 §5.3). Renderuje se vždy
                       a u ne-PNF se schová atributem — typ je u nově zadávaného tiketu
                       ještě neznámý a odhalí ho sync.js, stejně jako pole ceny. *@
                    <label class="external-field-pozadavek"
                           data-external-pozadavek-field
                           @(isPnf ? null : "hidden=\"hidden\"")>
                        <span class="external-field-label">Text požadavku do výzvy</span>
                        <textarea name="ExterniVazby[@i].Pozadavek"
                                  rows="5"
                                  data-rich-text="true"
                                  data-rich-text-min-height="120"
                                  data-external-pozadavek-input
                                  placeholder="Co je u tohoto PNF požadováno"
                                  @(externalReadonly ? "disabled" : null)>@vazba.Pozadavek</textarea>
                    </label>
```

Pozor na dvě věci. `@vazba.Pozadavek` patří dovnitř `<textarea>` bez mezer okolo, jinak
se do hodnoty dostane bílé místo. A `richtext.js` přeskakuje `textarea` s `disabled`,
takže v detailu návrhu (`externalReadonly`) zůstane prostý textový výpis — což je správně,
tam se needituje.

- [x] **Krok 5: Nastavit písmo editoru**

Do `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css` na konec:

```css
/* Text požadavku do výzvy (2026-09-08). Pole je přes celou šířku karty pod datumy. */
.external-row > .external-field-pozadavek {
  grid-column: 1 / -1;
  grid-row: 3;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  min-width: 0;
}

.external-row > .external-field-pozadavek[hidden] { display: none; }

/* Times New Roman 12 jen tady, ne globálně — ostatní editory v aplikaci si drží
   aplikační písmo. Co pracovník vidí, to dostane ve výzvě (spec R6). */
.external-row .external-field-pozadavek .ql-editor {
  font-family: "Times New Roman", Times, serif;
  font-size: 12pt;
  line-height: 1.4;
}
```

Ověř, že `grid-row: 3` nekoliduje s existujícím rozvržením karty — `.external-row` je
grid se dvěma řádky a `.external-dates` sedí na řádku 2. Pokud po přidání pole karta
poskočí, uprav čísla řádků, ne strukturu.

- [x] **Krok 6: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExterniOdkazPozadavekRenderTests"`
Očekávej: 2 úspěšné.

- [x] **Krok 7: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
```

Očekávej: build čistý, Api jen se 4 známými selháními.

---
## Blok 6: Předvyplnění textu u nově zadávané vazby

Spec §5.4 a rozhodnutí R5. Když pracovník zadá číslo tiketu a vazba je PNF, editor se
naplní popisem z tiketu — ale jen když je prázdný, aby se rozepsaný text nepřepsal.
Stávající vazby se nepředvyplňují (R2).

**Soubory:**
- Upravit: `PmTracker.Web/Controllers/ExterniOdkazController.cs` (`ExterniOdkazSyncResponse`, obě `return Ok(...)` větve)
- Upravit: `PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js` (most na `window`)
- Upravit: `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js`
- Test: `PmTracker.Tests.Api/Controllers/ExterniOdkazSyncPopisTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncJsTests.cs` (nový)

**Rozhraní:**
- Konzumuje: `HotZaznamDto.Popis` — už existuje, `GetZaznamAsync` ho vrací.
- Konzumuje: `setRecordEditorRichTextValue(textarea, value)` z `recordEditor/richtext.js`.
- Produkuje: `ExterniOdkazSyncResponse.Popis`, globální most `window.pmRichText.setValue`.

- [x] **Krok 1: Napsat padající Api test**

`PmTracker.Tests.Api/Controllers/ExterniOdkazSyncPopisTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Odpověď náhledu tiketu nese i popis, ze kterého se předvyplní text požadavku
/// (spec 2026-09-08 §5.4). Do 2026-09-08 vracela jen Strucne, takže klient popis neměl
/// odkud vzít.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExterniOdkazSyncPopisTests
{
    private readonly ApiSqlFixture _fixture;

    public ExterniOdkazSyncPopisTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Sync_OdpovedNesePolePopis()
    {
        var projectId = await _fixture.EnsureProjectAsync("APISYNCPOPIS");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/ExterniOdkaz/Sync?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("cislo", "999999"),
                ("projektId", projectId.ToString()))));

        var json = await response.Content.ReadAsStringAsync();

        // ServiceDesk je v testech vypnutý (DisabledTicketingQueryService), takže tiket
        // nebude nalezen — ověřujeme tvar kontraktu, ne obsah. Bez pole popis by klient
        // neměl co do editoru vložit.
        json.Should().Contain("popis", "kontrakt odpovědi musí nést popis tiketu");
    }
}
```

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExterniOdkazSyncPopisTests"`
Očekávej: selhání — v JSON pole `popis` není.

- [x] **Krok 3: Rozšířit odpověď o popis**

V `ExterniOdkazController.cs` najdi definici `ExterniOdkazSyncResponse` a přidej `Popis`
jako **poziční parametr na konci s výchozí hodnotou**:

```csharp
    string? Popis = null);
```

Stejný postup, jakým se do `HotZaznamDto` přidávalo `Pid` — nový parametr uprostřed by
rozbil existující poziční volání.

V úspěšné větvi `return Ok(new ExterniOdkazSyncResponse(...))` doplň na konec:

```csharp
            Popis: dto.Popis));
```

V nenalezené větvi zůstává `Popis` na výchozí `null` — nic se nedoplňuje.

- [x] **Krok 4: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~ExterniOdkazSyncPopisTests"`
Očekávej: 1 úspěšný.

- [x] **Krok 5: Vystavit setter rich textu na window**

`sync.js` je starší IIFE modul (`(function (global) { … })(window)`), takže nemůže
importovat z ESM `richtext.js`. Most vede přes `window`, stejně jako u ostatních
legacy modulů (`window.pmExterniOdkazSync`, `window.pmChatModal`).

Na konec `PmTracker.Web/wwwroot/js/modules/recordEditor/richtext.js` přidej:

```javascript
// Most pro starší IIFE moduly, které nemohou importovat z ESM (sync.js předvyplňuje
// text požadavku po dohledání tiketu). Stejný vzor jako window.pmExterniOdkazSync.
window.pmRichText = window.pmRichText || {};
window.pmRichText.setValue = setRecordEditorRichTextValue;
```

- [x] **Krok 6: Napsat padající JS piny**

`PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncJsTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// JS piny pro předvyplnění textu požadavku (spec 2026-09-08 §5.4). Aplikace nemá JS
/// test runner, takže se ověřuje zdroj modulů — stejný vzor jako VyzvyPresunJsTests.
/// </summary>
public sealed class ExterniOdkazSyncJsTests
{
    private static string Js(string relativni)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        var root = dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
        return File.ReadAllText(Path.Combine(root, "PmTracker.Web/wwwroot/js/modules", relativni));
    }

    [Fact]
    public void RichText_MaMostNaWindow_ProStarsiModuly()
    {
        Js("recordEditor/richtext.js").Should().Contain("window.pmRichText",
            "sync.js je IIFE a nemůže importovat z ESM");
    }

    [Fact]
    public void Sync_PredvyplniTextPozadavkuJenUPnf()
    {
        var src = Js("externiOdkaz/sync.js");

        src.Should().Contain("data-external-pozadavek-input",
            "předvyplnění míří do editoru textu požadavku");
        src.Should().Contain("isPnf(", "u jiných typů vazby se nic nepředvyplňuje");
    }

    /// <summary>
    /// Rozepsaný text se nikdy nepřepíše — předvyplňuje se jen do prázdného pole.
    /// Jinak by pracovníkovi zmizelo, co už napsal, kdyby opravil číslo tiketu.
    /// </summary>
    [Fact]
    public void Sync_NeprepisujeJizNapsanyText()
    {
        var src = Js("externiOdkaz/sync.js");

        var start = src.IndexOf("function predvyplnPozadavek", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1, "předvyplnění má vlastní funkci");

        var konec = src.IndexOf("\n  function ", start + 1, StringComparison.Ordinal);
        var body = konec < 0 ? src[start..] : src[start..konec];

        body.Should().Contain("value", "rozhoduje se podle toho, jestli je pole prázdné");
    }
}
```

- [x] **Krok 7: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ExterniOdkazSyncJsTests"`
Očekávej: `Sync_PredvyplniTextPozadavkuJenUPnf` a `Sync_NeprepisujeJizNapsanyText` selžou.

- [x] **Krok 8: Doplnit předvyplnění do sync.js**

K ostatním pomocným funkcím (vedle `setVyzvaVisible`) přidej:

```javascript
  // Předvyplnění textu požadavku popisem tiketu (spec 2026-09-08 §5.4). Jen u PNF
  // a jen do prázdného pole — rozepsaný text se nikdy nepřepíše, jinak by pracovníkovi
  // zmizelo, co už napsal, kdyby jen opravil číslo tiketu.
  //
  // Stávající vazby se nepředvyplňují: mají text uložený (i prázdný) a tahle větev
  // se u nich neuplatní, protože pole není prázdné nebo si ho pracovník smazal záměrně.
  function predvyplnPozadavek(row, popis) {
    const textarea = row.querySelector('[data-external-pozadavek-input]');
    if (!textarea || !popis) return;
    if ((textarea.value || '').trim()) return;

    if (global.pmRichText && typeof global.pmRichText.setValue === 'function') {
      global.pmRichText.setValue(textarea, popis);
    } else {
      textarea.value = popis;
    }
  }

  function setPozadavekVisible(row, visible) {
    const pole = row.querySelector('[data-external-pozadavek-field]');
    if (!pole) return;
    if (visible) {
      pole.removeAttribute('hidden');
    } else {
      pole.setAttribute('hidden', 'hidden');
    }
  }
```

V úspěšné větvi (`if (data.nalezeno)`) za `setVyzvaVisible(row, isPnf(data.typ));`:

```javascript
        setPozadavekVisible(row, isPnf(data.typ));
        if (isPnf(data.typ)) {
          predvyplnPozadavek(row, data.popis);
        }
```

V nenalezené větvi za `setVyzvaVisible(row, false);`:

```javascript
        setPozadavekVisible(row, false);
```

- [x] **Krok 9: Spustit a ověřit průchod**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ExterniOdkazSyncJsTests"
node --check PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js
```

Očekávej: 3 úspěšné a `node --check` bez výstupu.

- [x] **Krok 10: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
```

Očekávej: build čistý, Unit zelená, Api jen se 4 známými selháními.

---

## Blok 7: Sdílený parser rich textu pro Word

Spec §6, rozhodnutí R7. Výzva potřebuje totéž, co umí tisk záznamu. Refaktor do funkčního
kódu, takže musí být chováním neutrální.

**Pozor na hranici, kterou spec popisuje obecněji, než jak vypadá kód:** `AppendHtmlParagraphs`
zapisuje do `TableCell` a je svázaný s tabulkovým rozvržením tisku záznamu. Výzva sází do
`Body`. Sdílet se proto dá **parser** HTML, ne skládání odstavců. Je to i čistší hranice —
parsování je společné, rozvržení patří dokumentu.

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Export/RichTextHtmlParser.cs`
- Upravit: `PmTracker.Web/Services/Export/OpenXmlWordExportService.cs` (structy, řádky 50–68)
- Upravit: `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs` (řádky 450, 525, 601, 671)
- Test: `PmTracker.Tests.Unit/Export/RichTextHtmlParserTests.cs` (nový)

**Rozhraní:**
- Produkuje: `RichTextHtmlParser.Parse(string safeHtml) → IReadOnlyList<RichTextParagraph>`
- Produkuje: `RichTextParagraph(int IndentLevel, IReadOnlyList<RichTextToken> Tokens)`
- Produkuje: `RichTextToken(string Text, bool Bold, bool Italic, bool Underline, string? LinkHref, bool IsLineBreak)`

- [x] **Krok 1: Napsat padající test na parser**

`PmTracker.Tests.Unit/Export/RichTextHtmlParserTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Parser rich textu vytažený 2026-09-08 z exportu záznamu, aby ho mohla použít i výzva.
/// Chování musí zůstat stejné — proto tyhle testy popisují dnešní stav, ne nový.
/// </summary>
public sealed class RichTextHtmlParserTests
{
    [Fact]
    public void Parse_OdstavceATucnyText()
    {
        var odstavce = RichTextHtmlParser.Parse("<p>Ahoj <strong>svete</strong></p>");

        var odstavec = odstavce.Should().ContainSingle().Which;
        odstavec.Tokens.Should().HaveCount(2);
        odstavec.Tokens[0].Text.Should().Be("Ahoj ");
        odstavec.Tokens[0].Bold.Should().BeFalse();
        odstavec.Tokens[1].Text.Should().Be("svete");
        odstavec.Tokens[1].Bold.Should().BeTrue();
    }

    [Fact]
    public void Parse_SeznamDaOdsazeni()
    {
        var odstavce = RichTextHtmlParser.Parse("<ul><li>prvni</li><li>druhy</li></ul>");

        odstavce.Should().HaveCount(2);
        odstavce.Should().OnlyContain(p => p.IndentLevel > 0, "položky seznamu jsou odsazené");
    }

    [Fact]
    public void Parse_Odkaz_SiNeseCil()
    {
        var odstavce = RichTextHtmlParser.Parse("<p><a href=\"https://example.org\">web</a></p>");

        odstavce.Should().ContainSingle()
            .Which.Tokens.Should().ContainSingle()
            .Which.LinkHref.Should().Be("https://example.org");
    }

    [Fact]
    public void Parse_PrazdnyVstup_VraciPrazdno()
    {
        RichTextHtmlParser.Parse("").Should().BeEmpty();
        RichTextHtmlParser.Parse("<p><br></p>").Should()
            .OnlyContain(p => p.Tokens.All(t => t.IsLineBreak || string.IsNullOrEmpty(t.Text)),
                "prázdný odstavec z Quillu nesmí vyrobit viditelný text");
    }
}
```

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RichTextHtmlParserTests"`
Očekávej: chybu překladu — `RichTextHtmlParser` neexistuje.

- [x] **Krok 3: Vytvořit parser přesunem existujícího kódu**

Vytvoř `PmTracker.Web/Services/Export/RichTextHtmlParser.cs` a **přesuň do něj beze změny
těla** tyto členy z `OpenXmlWordExportService`:

| Odkud | Co | Nové jméno |
|---|---|---|
| `OpenXmlWordExportService.cs:52` | `HtmlInlineToken` | `public readonly record struct RichTextToken` |
| `OpenXmlWordExportService.cs:60` | `HtmlParagraphModel` | `public readonly record struct RichTextParagraph` |
| `OpenXmlWordExportService.cs:64` | `HtmlStyleState` | `private readonly record struct RichTextStyleState` |
| `…Records.cs:450` | `ParseRichHtml` | `public static Parse` |
| `…Records.cs:525` | `AppendListParagraphs` | `private static AppendListParagraphs` |
| `…Records.cs:601` | `AppendInlineTokens` | `private static AppendInlineTokens` |
| `…Records.cs:671` | `NormalizeHtmlForXml` | `private static NormalizeHtmlForXml` |

Pole struktur zůstávají stejná, jen se mění název typu. Hlavička souboru:

```csharp
using System.Xml.Linq;

namespace PmTracker.Web.Services.Export;

/// <summary>Jeden běh textu s formátováním.</summary>
public readonly record struct RichTextToken(
    string Text,
    bool Bold,
    bool Italic,
    bool Underline,
    string? LinkHref,
    bool IsLineBreak);

/// <summary>Jeden odstavec: úroveň odsazení a běhy textu.</summary>
public readonly record struct RichTextParagraph(
    int IndentLevel,
    IReadOnlyList<RichTextToken> Tokens);

/// <summary>
/// Převádí sanitizované HTML z rich text editoru na odstavce a běhy textu.
///
/// Vytaženo 2026-09-08 z OpenXmlWordExportService, kde bylo privátní — výzva potřebuje
/// totéž. Sdílí se schválně jen parsování: skládání odstavců zůstává u každého exportu
/// zvlášť, protože tisk záznamu sází do buňky tabulky a výzva do těla dokumentu.
/// </summary>
public static class RichTextHtmlParser
{
    // sem přijdou přesunuté metody
}
```

Pokud některá z přesunutých metod používá source-generated regex z partial třídy
(`[GeneratedRegex]`, viz `OpenXmlWordExportService.cs:70` a dál), ponech regex tam, kde je,
a předej výsledek parametrem — atribut vyžaduje partial třídu a přesun by nešel přeložit.

- [x] **Krok 4: Přepojit export záznamu na parser**

V `OpenXmlWordExportService.Records.cs` nahraď volání `ParseRichHtml(safeHtml)` za
`RichTextHtmlParser.Parse(safeHtml)` a typy `HtmlParagraphModel` / `HtmlInlineToken`
za `RichTextParagraph` / `RichTextToken`. Metody `AppendHtmlParagraphs` a
`CreateParagraphShell` **zůstávají** v exportu záznamu — jsou svázané s buňkou tabulky.

- [x] **Krok 5: Spustit a ověřit průchod i neutralitu refaktoru**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RichTextHtmlParserTests"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlWordExportServiceTests"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlWordExportSplitTests"
```

Očekávej: nové testy zelené a **obě existující sady zelené beze změny**. Kdyby bylo nutné
upravit existující test, refaktor přestal být chováním neutrální — vrať se a zjisti proč.

- [x] **Krok 6: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
```

Očekávej: build 0 chyb a 0 upozornění, Unit zelená.

---
## Blok 8: Výzva používá text požadavku

Spec §5.7. Poslední blok — teprve tady se změní obsah dokumentu.

**Zjištění, které mění rozsah R6:** `OpenXmlWordElements.CreateRunProperties`
(řádek 144) už dnes sází **Times New Roman napevno** v celém dokumentu. Z požadavku
„Times New Roman 12 i v dokumentu" tedy zbývá jen velikost. Zároveň z toho plyne, že
zbytek výzvy běží na 10 bodech (`BaseFontHalfPoints = 20`), takže text požadavku
ve 12 bodech bude proti okolí větší. Viz otevřený bod na konci plánu.

**Soubory:**
- Upravit: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaExportViewModels.cs` (řádek 42)
- Upravit: `PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs` (řádky 48–51, 86)
- Upravit: `PmTracker.Web/Views/Export/VyzvaTemplate.cshtml` (řádky 86–89)
- Upravit: `PmTracker.Web/Services/Export/OpenXmlVyzvaExportService.cs` (řádek 145)
- Upravit: `PmTracker.Web/wwwroot/css/pdf-export.css`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs` (existující)
- Test: `PmTracker.Tests.Unit/Export/OpenXmlVyzvaExportPozadavekTests.cs` (nový)

**Rozhraní:**
- Konzumuje: `ZaznamExterniOdkazEntity.Pozadavek` (blok 3),
  `RichTextHtmlParser.Parse` (blok 7), `IRichTextContentService` (už registrované v DI).
- Produkuje: `VyzvaExportPozadavekViewModel.PozadavekHtml` místo dosavadního `Popis`.

- [x] **Krok 1: Napsat padající testy builderu**

Do `PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs`. Soubor už má pomocné metody
`SeedAsync(params (int ZaznamId, string Cislo)[] pnf)`, `Builder(db, ticketing)`
a dvojníka `FakeTicketing` — používej je:

```csharp
    /// <summary>
    /// Text do výzvy jde z externí vazby, ne z popisu tiketu. Celý smysl změny 2026-09-08
    /// je, aby v dokumentu byl jen text, který pracovník napsal a viděl.
    /// </summary>
    [Fact]
    public async Task Build_BereTextZVazby_NeZTiketu()
    {
        using var db = await SeedAsync((100, "336865"));
        db.ZaznamExterniOdkazy.Single(x => x.Cislo == "336865").Pozadavek =
            "<p>Chceme sestavu.</p>";
        await db.SaveChangesAsync();

        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto(
            "336865", "PNF", "strucne", "Popis z HOT");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        var pozadavek = model!.Pozadavky.Should().ContainSingle().Which;
        pozadavek.PozadavekHtml.Should().Contain("Chceme sestavu.");
        pozadavek.PozadavekHtml.Should().NotContain("Popis z HOT",
            "popis tiketu se do výzvy už nedostane");
    }

    /// <summary>
    /// Vazby založené před 2026-09-08 mají NULL a popis tiketu se jako náhrada nepoužívá
    /// (spec R2 + R3). U požadavku se tedy nevytiskne nic, dokud pracovník text nevyplní.
    /// </summary>
    [Fact]
    public async Task Build_PrazdnyPozadavek_NevraciPopisTiketu()
    {
        using var db = await SeedAsync((100, "336865"));

        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto(
            "336865", "PNF", "strucne", "Popis z HOT");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        var pozadavek = model!.Pozadavky.Should().ContainSingle().Which;
        pozadavek.PozadavekHtml.Should().BeNullOrWhiteSpace(
            "prázdná hodnota znamená ve výzvě žádný text, ne náhradu z tiketu");
    }

    /// <summary>
    /// Dokument je výstup ven, takže sanitizace nesmí záviset jen na tom, že do databáze
    /// nikdy nic nepřišlo jinudy než přes uložení záznamu (spec §8, řádek „Unit — sanitizace").
    /// </summary>
    [Fact]
    public async Task Build_SkriptSeDoDokumentuNedostane()
    {
        using var db = await SeedAsync((100, "336865"));
        db.ZaznamExterniOdkazy.Single(x => x.Cislo == "336865").Pozadavek =
            "<p>Text</p><script>alert(1)</script>";
        await db.SaveChangesAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        var html = model!.Pozadavky.Single().PozadavekHtml;
        html.Should().Contain("Text");
        html.Should().NotContain("<script", "sanitizace běží i při stavbě dokumentu");
        html.Should().NotContain("alert(1)");
    }
```

Volání `Builder(db, ticketing)` po kroku 4 přestane překládat — pomocnou metodu uprav na:

```csharp
    private static VyzvaExportBuilder Builder(
        PmTracker.Web.Data.PmTrackerDbContext db, ITicketingQueryService ticketing)
        => new(db, ticketing, new PmTracker.Web.Services.Common.RichTextContentService());
```

`RichTextContentService` má bezparametrický konstruktor — testy exportu záznamu ho
instancují stejně (`OpenXmlWordExportServiceTests.cs:306`). Používej skutečnou službu,
ne dvojníka: sanitizace je právě to, co se tu ověřuje.

- [x] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaExportBuilderTests"`
Očekávej: chybu překladu — view model nemá `PozadavekHtml`.

- [x] **Krok 3: Přejmenovat pole ve view modelu**

V `VyzvaExportViewModels.cs` nahraď v `VyzvaExportPozadavekViewModel` řádek
`public string? Popis { get; init; }` za:

```csharp
    /// <summary>
    /// Text požadavku ze vstupu pracovníka — sanitizované HTML (spec 2026-09-08 §5.7).
    /// Do 2026-09-08 se sem dával popis tiketu z HOT_ZAZNAMY; ten se už nepoužívá,
    /// ani jako náhrada za prázdnou hodnotu.
    /// </summary>
    public string? PozadavekHtml { get; init; }
```

- [x] **Krok 4: Číst text z vazby**

V `VyzvaExportBuilder.cs` rozšiř projekci položek (dnes řádky 48–51) o nový sloupec:

```csharp
            .Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.Pozadavek })
```

Konstruktor doplň o `IRichTextContentService` a ulož ho do pole. Pak nahraď
`Popis = h?.Popis,` (řádek 86) za:

```csharp
                    // Sanitizace i tady, ne jen při uložení: dokument je výstup ven
                    // a nesmí záviset na tom, že do DB nikdy nic nepřišlo jinudy.
                    PozadavekHtml = _richText.HasVisibleText(x.Pozadavek)
                        ? _richText.ToSafeHtml(x.Pozadavek)
                        : null,
```

Proměnná `h` (záznam z HOT) zůstává — používá se dál pro `Pid` a kalkulace.
Zkontroluj, že se `h?.Popis` v souboru už nikde jinde nepoužívá:

```bash
grep -n "Popis" PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs
```

- [x] **Krok 5: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaExportBuilderTests"`
Očekávej: všechny testy třídy zelené.

- [x] **Krok 6: Vykreslit rich text v náhledu**

V `Views/Export/VyzvaTemplate.cshtml` nahraď blok (dnes řádky 86–89):

```cshtml
        @if (!string.IsNullOrWhiteSpace(p.PozadavekHtml))
        {
            @* Html.Raw je tu bezpečné: hodnotu sanitizoval builder přes ToSafeHtml.
               Kódování by naopak vypsalo syrové značky místo formátovaného textu. *@
            <div class="vyzva-pozadavek">@Html.Raw(p.PozadavekHtml)</div>
        }
```

Do `PmTracker.Web/wwwroot/css/pdf-export.css` k ostatním `.vyzva-*` pravidlům:

```css
/* Text požadavku od pracovníka. Times New Roman 12 shodně s Wordem, ať náhled
   neukazuje něco jiného než stažený dokument (spec R6). */
.vyzva-pozadavek {
  font-family: "Times New Roman", Times, serif;
  font-size: 12pt;
  line-height: 1.4;
}

.vyzva-pozadavek p { margin: 0 0 0.4em; }
.vyzva-pozadavek ul,
.vyzva-pozadavek ol { margin: 0 0 0.4em 1.2em; }
```

- [x] **Krok 7: Napsat padající test na Word**

`PmTracker.Tests.Unit/Export/OpenXmlVyzvaExportPozadavekTests.cs`:

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Word vysází text požadavku jako formátovaný rich text, ne jako syrové HTML
/// (spec 2026-09-08 §5.7). Velikost 12 bodů = 24 půlbodů; písmo Times New Roman
/// nastavuje OpenXmlWordElements.CreateRunProperties pro celý dokument.
/// </summary>
public sealed class OpenXmlVyzvaExportPozadavekTests
{
    private static WordprocessingDocument Otevrit(byte[] payload)
        => WordprocessingDocument.Open(new MemoryStream(payload, writable: false), false);

    [Fact]
    public void Build_TucnyTextZustaneTucny_ANeniVDokumentuHtml()
    {
        var model = Model("<p>Chceme <strong>sestavu</strong>.</p>");

        using var doc = Otevrit(new OpenXmlVyzvaExportService().BuildDocument(model));
        var body = doc.MainDocumentPart!.Document.Body!;
        var text = body.InnerText;

        text.Should().Contain("Chceme").And.Contain("sestavu");
        text.Should().NotContain("<strong>", "HTML značky se do dokumentu nesmí dostat");

        body.Descendants<Run>()
            .Where(r => r.InnerText.Contains("sestavu"))
            .Should().Contain(r => r.RunProperties!.Bold is not null,
                "tučné z editoru musí zůstat tučné i ve Wordu");
    }

    [Fact]
    public void Build_TextPozadavkuMa12Bodu()
    {
        var model = Model("<p>Chceme sestavu.</p>");

        using var doc = Otevrit(new OpenXmlVyzvaExportService().BuildDocument(model));

        doc.MainDocumentPart!.Document.Body!.Descendants<Run>()
            .Where(r => r.InnerText.Contains("Chceme"))
            .Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "24",
                "12 bodů = 24 půlbodů");
    }

    [Fact]
    public void Build_PrazdnyPozadavek_NicNevysazi()
    {
        var model = Model(null);

        using var doc = Otevrit(new OpenXmlVyzvaExportService().BuildDocument(model));

        doc.MainDocumentPart!.Document.Body!.InnerText
            .Should().NotContain("Chceme", "prázdná hodnota nemá co tisknout");
    }
```

Pomocnou metodu `Model(string? pozadavekHtml)` napiš podle toho, jak model staví
`PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs` — musí vrátit
`VyzvaExportViewModel` s jedním požadavkem, jehož `PozadavekHtml` je zadaná hodnota.
Uzavři třídu složenou závorkou.

- [x] **Krok 8: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlVyzvaExportPozadavekTests"`
Očekávej: selhání — dnes se text sází jako holý řetězec včetně HTML značek.

- [x] **Krok 9: Vysázet rich text ve Wordu**

V `OpenXmlVyzvaExportService.cs` nahraď řádek 145
`if (!string.IsNullOrWhiteSpace(p.Popis)) Odstavec(body, p.Popis!);` za:

```csharp
            if (!string.IsNullOrWhiteSpace(p.PozadavekHtml))
            {
                PozadavekOdstavce(body, p.PozadavekHtml!);
            }
```

A přidej k ostatním pomocným metodám (vedle `Odstavec` na řádku 54):

```csharp
    /// <summary>
    /// Vysází text požadavku jako formátovaný rich text. Písmo Times New Roman nastavuje
    /// CreateRunProperties pro celý dokument, tady se řídí jen velikost: 12 bodů
    /// = 24 půlbodů (spec 2026-09-08 R6).
    /// </summary>
    private static void PozadavekOdstavce(Body body, string safeHtml)
    {
        const int Velikost12Bodu = 24;

        foreach (var odstavec in RichTextHtmlParser.Parse(safeHtml))
        {
            var paragraph = new Paragraph(new ParagraphProperties(
                new SpacingBetweenLines { After = "120" },
                new Indentation { Left = (odstavec.IndentLevel * 360).ToString(CultureInfo.InvariantCulture) }));

            foreach (var token in odstavec.Tokens)
            {
                if (token.IsLineBreak)
                {
                    paragraph.Append(new Run(new Break()));
                    continue;
                }

                if (string.IsNullOrEmpty(token.Text)) { continue; }

                paragraph.Append(new Run(
                    OpenXmlWordElements.CreateRunProperties(
                        sizeHalfPoints: Velikost12Bodu,
                        bold: token.Bold,
                        italic: token.Italic,
                        strike: false,
                        underline: token.Underline),
                    new Text(token.Text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            body.Append(paragraph);
        }
    }
```

Doplň `using System.Globalization;`, pokud v souboru chybí. `CreateRunProperties` je
`internal`, takže je ze stejného sestavení dostupné.

Odkazy (`token.LinkHref`) se ve výzvě sázejí jako prostý text — dokument jde do spisové
služby a tiskne se, takže klikatelnost nemá komu posloužit. Kdyby ji uživatel chtěl,
je to samostatný požadavek.

- [x] **Krok 10: Spustit a ověřit průchod**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlVyzvaExportPozadavekTests"`
Očekávej: 3 úspěšné.

- [x] **Krok 11: Doplnit Api test na náhled**

Do `PmTracker.Tests.Api/Controllers/VyzvyTiskTests.cs` přidej test, že se sanitizovaný
text objeví v HTML náhledu a že se do něj nedostane skript. Obsah PDF se ověřuje přes
`Factory.Services.GetRequiredService<FakePdfRenderer>().LastHtml` — samotná odpověď je
falešný PDF payload, ne HTML. Kotvi se na ASCII, ne na český text.

- [x] **Krok 12: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj
```

Očekávej: build 0 chyb a 0 upozornění, Unit zelená, Api jen se 4 známými selháními,
Integration 85/86.

---

## Závěrečné ověření celého plánu

- [x] **Krok 1: Plná sada**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj
```

- [ ] **Krok 2: Ruční průchod, který testy nepokryjí** — čeká na uživatele

1. Otevři editor záznamu, zadej šestimístné číslo PNF tiketu — pole s textem požadavku
   se odhalí a předvyplní popisem tiketu.
2. Text uprav, ulož záznam, znovu otevři — text tam je.
3. Ve výzvě klikni na **Náhled** — otevře se v nové kartě, bez dialogu tisku.
4. Klikni na **Tisk výzvy** — stáhne se Word s textem v Times New Roman 12.
5. U vazby založené dřív zůstává pole prázdné a ve výzvě u ní není žádný text.

- [x] **Krok 3: Changelog**

Podklad `docs/changelog/releases/<verze>.md` doplň o řádky do sekcí Přidáno a Změněno.
Kořenový `CHANGELOG.md` **needituj ručně** — vygeneruj ho:

```bash
bash scripts/generate-changelog.sh
```

- [ ] **Krok 4: Předání**

Commity drží uživatel. Nahlas mu výsledky sad, co jsi ověřil ručně a co ne, a nech
verifikaci a commit na něm.

## Otevřené body

1. **Chyba 500 při tisku výzvy** — příčina zjištěna 2026-09-08 z produkčního logu
   a řeší ji blok 0: `HOT_KALKULACE.verze` je v databázi text, entita ho měla jako číslo.
   Blok 0 patří na začátek, protože bez něj tisk nefunguje vůbec a bloky 7 a 8 by se
   ověřovaly na rozbité cestě.
2. **Velikost písma proti okolí.** Dokument dnes běží na Times New Roman 10
   (`BaseFontHalfPoints = 20`); text požadavku bude podle R6 dvanáctibodový, takže bude
   proti okolnímu textu větší. Buď je to záměr, nebo se má sjednotit celý dokument
   na 12 bodů — rozhodnutí patří uživateli, ne exekutorovi plánu.

   **Vyřešeno 2026-09-10 finálním vzorem výzvy:** tělo vzoru je Times New Roman 12
   (styl Normal = 24 půlbodů), tabulky 10 bodů. Text požadavku ve 12 bodech je tedy
   správně a chybu má dnešní export, který sází tělo v 10 bodech. Řeší spec
   `2026-09-10-pnf-skutecna-cena-design.md` část B, bod B1.
3. **Blok 0 už nemá otevřenou neznámou** — schéma `HOT_KALKULACE` je ověřené proti
   `sys.columns` a výběr kalkulace je rozhodnutý (filtr `Akceptováno`, při shodě vyšší
   `id`). Zbývá jen ověření naostro po nasazení, kam testy nedosáhnou.

4. **Souborový log** zůstává zapnutý — vyřešeno 2026-09-08 před blokem 1. Zapisují se
   jen chyby (`MinimumLevel = Error`) a soubory se drží týden; maže se podle data
   v názvu, a to i za běhu při přelomu dne, ne jen při startu aplikace.
   Zbývá jedna drobnost, kterou jsem vědomě neřešil: `LogAjaxFailure` posílá do logu
   stack trace dvakrát — jednou uvnitř diagnostického výpisu a jednou jako výjimku.
   Je to chování staršího kódu a jeho úprava by sahala na testy soukromí, takže patří
   do samostatného zadání.
