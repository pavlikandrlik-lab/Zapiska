# Pohled jako (superadmin vidí aplikaci očima jiného uživatele) — plán implementace

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: použij superpowers:executing-plans (inline, po úkolech, bez subagentů). Kroky používají checkboxy (`- [ ]`). Commity dělej až při exekuci plánu, do lokálního `main`, bez pushe.

**Cíl:** Superadmin si v menu účtu zvolí „Zobrazit jako…“, vybere osobu a prochází celou aplikaci s jejími právy a viditelností. Pod hlavičkou vidí lištu „Prohlížíte aplikaci jako X“ s tlačítkem „Zrušit pohled jako“. V režimu pohledu se nic neuloží.

**Architektura:** Identita přihlášeného (Windows Auth, v dev `?asUser=`) se nemění. `UserContextResolver` po nalezení skutečné osoby zeptá `IViewAsService`, jestli platí cookie `pmtracker.viewas` (formát `{skutečný}-{cíl}`). Když ano a skutečný uživatel je superadmin, sestaví celý `CurrentUserContextViewModel` pro cílovou osobu (role, práva, viditelné projekty, `HttpContext.Items[osobaId]`). Do kontextu přidá `ViewAs` (kdo se skutečně dívá) a aktivuje scoped `ViewAsRequestState`. Pouze ke čtení ve třech vrstvách:
1. `ViewAsReadOnlyMiddleware` odmítne každý ne-GET požadavek kromě akcí s `[AllowDuringViewAs]` (čtení přes POST) a `[ViewAsControl]` (zahájení a ukončení pohledu).
2. Známé zápisy při GET (zámek karty v editoru, heartbeat v KeepAlive, harvest trigger) se v pohledu přeskočí explicitně.
3. Pojistka `ViewAsWriteGuardInterceptor` (EF `DbCommandInterceptor`) v pohledu vyhodí `ViewAsWriteBlockedException` na každý INSERT/UPDATE/DELETE/MERGE/TRUNCATE z requestu, který nemá povolení. Middleware z ní udělá srozumitelnou odpověď.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, EF Core 8 (SQL Server), DS gov web komponenty; xUnit + FluentAssertions; testy Unit / Integration (`SqlIntegrationFixture`) / Api (`ApiSqlFixture`) / E2E (Playwright .NET).

**Spec:** žádný samostatný design dokument. Zadání je v konverzaci 2026-10-15/20 (analýza náročnosti, dotaz „podstrčí jen login?“, požadavek na detailní plán).

## Rozhodnutí (výchozí z analýzy, uživatel je může změnit před exekucí)

- **Jen pro čtení.** Žádný zápis za cílového uživatele, žádné autorství na jeho jméno.
- **Spustit smí jen superadmin** (`authz.superadmins`), ne projektový `ADM_PROJ`.
- **Cookie bez `Expires`** (session cookie): pohled skončí zavřením prohlížeče a platí ve všech záložkách.
- **Během aktivního pohledu se „Zobrazit jako…“ v menu neukazuje.** Pro přepnutí na jinou osobu se nejdřív zruší současný pohled.
- **Jako cíl jde i jiný superadmin.** Jen pro čtení je to neškodné, žádná zvláštní výjimka.

## Global Constraints

- Identita přihlášeného (`HttpContext.User`) se **nikdy** nemění. Mění se jen osoba, za kterou aplikace request vyhodnotí (`osoba_id` v kontextu a v `HttpContext.Items[CurrentUserAccessor.HttpContextItemKey]`).
- Superadmin se ověřuje **při každém requestu** podle skutečné osoby. Cookie bez platného superadmina, s cizím skutečným uživatelem, s neexistující cílovou osobou, s cílem = sám sebe nebo v chybném formátu se ignoruje a smaže.
- Název cookie: `pmtracker.viewas`. Hodnota: `{realOsobaId}-{targetOsobaId}` (obě kladná `int`, invariantní kultura). Volby: `HttpOnly`, `Secure = Request.IsHttps`, `SameSite=Lax`, `IsEssential`, `Path=/`, bez `Expires`.
- Kód chyby blokace: `VIEW_AS_READ_ONLY`. Kód chyby výběru osoby: `VIEW_AS_INVALID_TARGET`. Nesmí to být `HTTP_403` (to by v `ajax.js` spustilo session recovery a opakování submitu).
- Text blokace (jediný zdroj `ViewAsMessages.ReadOnly`): `V režimu „Zobrazit jako“ nelze nic ukládat ani měnit. Pro úpravy ukončete pohled tlačítkem „Zrušit pohled jako“ v liště pod hlavičkou.`
- Audit do `authz.audit_log`: `entity_type = "pohled_jako"`, `entity_id = cílová osoba_id`, `action = "view_as_start"` / `"view_as_stop"`, `actor_osoba_id = skutečná osoba`. **Bez auditu se pohled nezahájí.** Ukončení proběhne vždy, i když audit selže (jen se zaloguje chyba).
- Žádná změna DB schématu ani `db_upgrade_*.sql`: `audit_log` nemá CHECK na hodnoty.
- CSS jen přes gov tokeny (`AppCssColorTokenTests`), žádné `[data-theme="dark"]` pravidlo mimo tokens.css. Nosný layout lišty jen letitý flex + wrap (Edge na i15), žádné `:has` ani `max-content`.
- Razor: žádný `@* *@` uvnitř TagHelper tagu (spolkne další atribut). `data-*` atributy podmíněně neskládat přes `@null`.
- Api HTML testy kotví na atributy / ASCII jména, ne na český text (Razor kóduje diakritiku).
- Unit i Api sady zůstávají zelené (nové selhání = regrese). E2E se posuzuje porovnáním jmen proti baseline (33 pre-existing selhání), ne počtem.
- Zachovat `?asUser=` (dev): URL vygenerované novým kódem nesou `asUser` stejně jako dnešní kód (`WithAsUserRoute()`, `new { asUser }` v Razoru).

## Review Focus

1. **Stránka, která při GET potají zapisuje do DB** (neznámý vedlejší efekt mimo zámek, heartbeat a harvest). Čekané chování: stránka se v pohledu zobrazí, zápis se neprovede. Pokud zápis existuje, pojistka ho zablokuje a v logu bude `Pohled jako: zablokován zápis`. Task 7 má sweep test hlavních stránek a Task 9 ruční průchod s kontrolou logu.
2. **AJAX uložení v modalu nebo v editoru během pohledu.** Uživatel musí dostat čitelnou hlášku v modalu, ne „Relace vypršela“ a ne dvojí odeslání. Task 4 (unit middleware: JSON s `errorCode` `VIEW_AS_READ_ONLY`) a Task 7 (Api: POST `/Projekty/SaveProject` vrátí 403 JSON a projekt nevznikne).
3. **Cookie, které už neplatí**: superadmin mezitím přišel o roli, cílová osoba byla smazána, nebo jde o sdílený počítač s jiným Windows účtem. Čekané chování: aplikace se ukáže jako přihlášenému uživateli, cookie se smaže a nic nespadne. Task 2 (unit `ViewAsService`) a Task 6 (Api).
4. **Editor záznamu otevřený v pohledu.** Zámek karty cílového uživatele ani nikoho jiného se nesmí vytvořit, prodloužit ani smazat. Když záznam drží někdo třetí, admin vidí stejnou stránku „záznam upravuje X“ jako cílový uživatel. Task 5 (Integration `PeekAsync`) a Task 7 (Api Edit a KeepAlive).
5. **Klik na „Zrušit pohled jako“ ze stránky, kam cílový uživatel nesmí** (nebo kde cookie mezitím přestalo platit). Ukončení musí projít vždy a vrátit admina na stejnou stránku jako jeho samotného. Task 6 (Api `Ukoncit` bez aktivního pohledu → redirect, žádná chyba) a Task 8 (E2E).

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Services/Security/ViewAs/ViewAsCookie.cs` | nový — název, formát, parsování, volby, smazání cookie |
| `PmTracker.Web/Services/Security/ViewAs/ViewAsMessages.cs` | nový — texty hlášek |
| `PmTracker.Web/Services/Security/ViewAs/ViewAsRequestState.cs` | nový — scoped stav pohledu v requestu |
| `PmTracker.Web/Services/Security/ViewAs/ViewAsAttributes.cs` | nový — `[AllowDuringViewAs]`, `[ViewAsControl]` |
| `PmTracker.Web/Services/Security/ViewAs/LocalReferer.cs` | nový — bezpečný návrat na lokální Referer |
| `PmTracker.Web/Services/Security/ViewAs/ViewAsService.cs` | nový — vyhodnocení cookie, zahájení, ukončení, audit |
| `PmTracker.Web/Services/Security/ViewAs/SqlWriteCommandClassifier.cs` | nový — rozpozná zapisující SQL |
| `PmTracker.Web/Services/Security/ViewAs/ViewAsWriteGuardInterceptor.cs` | nový — EF pojistka + `ViewAsWriteBlockedException` |
| `PmTracker.Web/Middleware/ViewAsReadOnlyMiddleware.cs` | nový — blokace ne-GET + převod pojistky na odpověď |
| `PmTracker.Web/Controllers/PohledJakoController.cs` | nový — modal, hledání osob, Zahájit, Ukončit |
| `PmTracker.Web/Views/PohledJako/VyberModal.cshtml` | nový — modal s výběrem osoby |
| `PmTracker.Web/Views/Shared/_ViewAsBar.cshtml` | nový — lišta pod hlavičkou |
| `PmTracker.Web/Models/ViewModels/ViewAsViewModels.cs` | nový — `ViewAsInfo`, `ViewAsBarViewModel`, `ViewAsModalViewModel` |
| `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` | `CurrentUserContextViewModel.ViewAs` |
| `PmTracker.Web/Models/ViewModels/NavPermissionsViewModel.cs` | `CanStartViewAs`, `ViewAs` |
| `PmTracker.Web/Services/Data/RecordValidationContracts.cs` | `AjaxErrorCodes.ViewAsReadOnly`, `ViewAsInvalidTarget` |
| `PmTracker.Web/Services/Audit/AuditModels.cs` | `ViewAsStart`, `ViewAsStop`, `AuditEntityType.ViewAs` |
| `PmTracker.Web/Services/Security/UserContextResolver.cs` | cílová osoba místo skutečné + `ViewAs` + aktivace stavu |
| `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` | DI + interceptor do `AddDbContext` |
| `PmTracker.Web/Program.cs` | `UseMiddleware<ViewAsReadOnlyMiddleware>()` |
| `PmTracker.Web/Controllers/BaseController.cs` | `NavPermissions.CanStartViewAs` + `ViewAs` |
| `PmTracker.Web/Views/Shared/_Layout.cshtml` | položka menu + vložení lišty |
| `PmTracker.Web/wwwroot/css/site.css` | styl lišty |
| `PmTracker.Web/Services/Records/RecordEditLockService.cs` | `PeekAsync` (čtení zámku bez zápisu) |
| `PmTracker.Web/Controllers/ZaznamyController.cs` | v pohledu `PeekAsync` místo `TryAcquireAsync`, bez harvest triggeru |
| `PmTracker.Web/Controllers/AppController.cs` | v pohledu bez heartbeatu zámku |
| `PmTracker.Web/Controllers/ExportController.cs`, `HarmonogramController.cs`, `ExterniOdkazController.cs` | `[AllowDuringViewAs]` na čtecí POSTy |
| `PmTracker.Tests.Unit/Security/ViewAs/*.cs` | nové unit testy (cookie, klasifikátor, služba, middleware) |
| `PmTracker.Tests.Unit/Controllers/ZaznamyControllerEditAuthzTests.cs` | fake zámku dostane `PeekAsync` |
| `PmTracker.Tests.Unit/Architecture/AuthorizationPolicyEnforcementTests.cs` | allowlist `PohledJakoController.Zahajit/Ukoncit` |
| `PmTracker.Tests.Unit/Architecture/MiddlewareOrderingTests.cs` | pořadí nového middleware |
| `PmTracker.Tests.Integration/DataStore/ViewAsWriteGuardDataStoreTests.cs` | nový — pojistka proti reálné DB |
| `PmTracker.Tests.Integration/DataStore/RecordEditLockDataStoreTests.cs` | testy `PeekAsync` |
| `PmTracker.Tests.Api/Controllers/ViewAsFlowTests.cs` | nový — zahájení, ukončení, cookie, lišta, menu |
| `PmTracker.Tests.Api/Controllers/ViewAsReadOnlyTests.cs` | nový — blokace, sweep stránek, editor, KeepAlive |
| `PmTracker.Tests.E2E/Scenarios/ViewAsScenariosTests.cs` | nový — klikací scénář v prohlížeči |
| `docs/authorization.md` | sekce „Pohled jako“ |

**Mimo rozsah (vědomě):**
- Stránky mimo `BaseController` s plným layoutem (`SDConnectorController` diagnostika) lištu nemají, protože nemají ani `NavPermissions`. Blokace zápisu na nich platí, middleware je globální.
- Prázdná 403 po `Forbid()` (cílový uživatel nesmí na stránku) lištu nemá. Admin se vrátí zpět nebo na přehled, kde lišta je.
- Fetch volání mimo `ajax.js` (`chatModal.js`, `sync.js`, `scheduleProposalGuard.js`, `CreateVazba`) při blokaci ukážou svou obecnou chybu. Server zápis odmítne stejně, jen hláška není tak konkrétní.
- Harvest ServiceDesku spouštěný na pozadí (periodický job) běží v pohledu normálně: nemá HttpContext ani stav pohledu.

---

### Task 1: Základní stavební prvky (cookie, stav, atributy, hlášky, kódy, audit)

**Files:**
- Create: `PmTracker.Web/Services/Security/ViewAs/ViewAsCookie.cs`
- Create: `PmTracker.Web/Services/Security/ViewAs/ViewAsMessages.cs`
- Create: `PmTracker.Web/Services/Security/ViewAs/ViewAsRequestState.cs`
- Create: `PmTracker.Web/Services/Security/ViewAs/ViewAsAttributes.cs`
- Create: `PmTracker.Web/Services/Security/ViewAs/LocalReferer.cs`
- Modify: `PmTracker.Web/Services/Data/RecordValidationContracts.cs:3-15`
- Modify: `PmTracker.Web/Services/Audit/AuditModels.cs`
- Create: `PmTracker.Web/Models/ViewModels/ViewAsViewModels.cs`
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs:414-431` (`CurrentUserContextViewModel.ViewAs`)
- Modify: `PmTracker.Web/Models/ViewModels/NavPermissionsViewModel.cs`
- Test: `PmTracker.Tests.Unit/Security/ViewAs/ViewAsCookieTests.cs`
- Test: `PmTracker.Tests.Unit/Security/ViewAs/LocalRefererTests.cs`

**Interfaces:**
- Produces:
  - `static class ViewAsCookie { const string Name = "pmtracker.viewas"; static string Format(int realOsobaId, int targetOsobaId); static bool TryParse(string? raw, out int realOsobaId, out int targetOsobaId); static CookieOptions BuildOptions(HttpRequest request); static void Delete(HttpContext context); }`
  - `static class ViewAsMessages { const string ReadOnly; const string OnlySuperAdmin; const string AlreadyActive; const string SelfTarget; const string TargetNotFound; }`
  - `sealed class ViewAsRequestState { bool IsActive; int? RealOsobaId; int? TargetOsobaId; bool WritesPermitted; bool BlocksWrites; void Activate(int realOsobaId, int targetOsobaId); void PermitWrites(); }`
  - `sealed class AllowDuringViewAsAttribute : Attribute`, `sealed class ViewAsControlAttribute : Attribute`
  - `static class LocalReferer { static string? Resolve(HttpRequest request); }`
  - `AjaxErrorCodes.ViewAsReadOnly = "VIEW_AS_READ_ONLY"`, `AjaxErrorCodes.ViewAsInvalidTarget = "VIEW_AS_INVALID_TARGET"`
  - `AuditActionType.ViewAsStart` → `"view_as_start"`, `AuditActionType.ViewAsStop` → `"view_as_stop"`, `AuditEntityType.ViewAs` → `"pohled_jako"`
  - `sealed record ViewAsInfo(int RealOsobaId, string RealDisplayName)`; `CurrentUserContextViewModel.ViewAs : ViewAsInfo?` (null = žádný pohled)
  - `sealed record ViewAsBarViewModel(string TargetDisplayName, string RealDisplayName)`
  - `sealed class ViewAsModalViewModel { required string SearchUrl }`
  - `NavPermissionsViewModel.CanStartViewAs : bool`, `NavPermissionsViewModel.ViewAs : ViewAsBarViewModel?`

- [ ] **Step 1: Napiš padající testy cookie a Refereru**

`PmTracker.Tests.Unit/Security/ViewAs/ViewAsCookieTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class ViewAsCookieTests
{
    [Fact]
    public void Format_ThenTryParse_RoundTrips()
    {
        var raw = ViewAsCookie.Format(12, 345);

        raw.Should().Be("12-345");
        ViewAsCookie.TryParse(raw, out var real, out var target).Should().BeTrue();
        real.Should().Be(12);
        target.Should().Be(345);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12-")]
    [InlineData("-12")]
    [InlineData("a-b")]
    [InlineData("12-34-56")]
    [InlineData("0-5")]
    [InlineData("5-0")]
    [InlineData(" 12-34")]
    [InlineData("+12-34")]
    [InlineData("12:34")]
    public void TryParse_RejectsMalformedValues(string? raw)
    {
        ViewAsCookie.TryParse(raw, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void BuildOptions_IsHttpOnlyLaxSessionCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";

        var options = ViewAsCookie.BuildOptions(context.Request);

        options.HttpOnly.Should().BeTrue();
        options.Secure.Should().BeTrue();
        options.SameSite.Should().Be(SameSiteMode.Lax);
        options.IsEssential.Should().BeTrue();
        options.Path.Should().Be("/");
        options.Expires.Should().BeNull("pohled jako má skončit zavřením prohlížeče");
    }

    [Fact]
    public void Delete_EmitsExpiredSetCookie()
    {
        var context = new DefaultHttpContext();

        ViewAsCookie.Delete(context);

        context.Response.Headers.SetCookie.ToString()
            .Should().StartWith($"{ViewAsCookie.Name}=;")
            .And.Contain("1970");
    }
}
```

`PmTracker.Tests.Unit/Security/ViewAs/LocalRefererTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class LocalRefererTests
{
    private static HttpRequest Request(string? referer)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("pmtracker.local");
        if (referer is not null)
        {
            context.Request.Headers.Referer = referer;
        }
        return context.Request;
    }

    [Fact]
    public void Resolve_SameHost_ReturnsPathAndQuery()
        => LocalReferer.Resolve(Request("https://pmtracker.local/Projekty/Detail/5?tab=tym&asUser=3"))
            .Should().Be("/Projekty/Detail/5?tab=tym&asUser=3");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://evil.example/Projekty")]
    [InlineData("/Projekty")]
    public void Resolve_ForeignOrInvalid_ReturnsNull(string? referer)
        => LocalReferer.Resolve(Request(referer)).Should().BeNull();
}
```

- [ ] **Step 2: Spusť testy, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Security.ViewAs" --nologo`
Expected: FAIL — `The type or namespace name 'ViewAs' does not exist`.

- [ ] **Step 3: Implementuj prvky**

`PmTracker.Web/Services/Security/ViewAs/ViewAsCookie.cs`:

```csharp
using System.Globalization;

namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// Cookie režimu „Zobrazit jako“. Nepodepisuje se: platnost rozhoduje server při každém
/// requestu (skutečný přihlášený musí být superadmin a shodovat se s první částí hodnoty).
/// </summary>
public static class ViewAsCookie
{
    public const string Name = "pmtracker.viewas";

    public static string Format(int realOsobaId, int targetOsobaId)
        => string.Create(CultureInfo.InvariantCulture, $"{realOsobaId}-{targetOsobaId}");

    public static bool TryParse(string? raw, out int realOsobaId, out int targetOsobaId)
    {
        realOsobaId = 0;
        targetOsobaId = 0;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        var parts = raw.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var real)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var target)
            || real <= 0
            || target <= 0)
        {
            return false;
        }

        realOsobaId = real;
        targetOsobaId = target;
        return true;
    }

    public static CookieOptions BuildOptions(HttpRequest request) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        // Lax, ne Strict: odkaz z Outlooku/Teams by jinak otevřel aplikaci bez pohledu
        // a první další klik by pohled nečekaně „zapnul“.
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = "/"
    };

    public static void Delete(HttpContext context)
        => context.Response.Cookies.Delete(Name, BuildOptions(context.Request));
}
```

`PmTracker.Web/Services/Security/ViewAs/ViewAsMessages.cs`:

```csharp
namespace PmTracker.Web.Services.Security.ViewAs;

public static class ViewAsMessages
{
    public const string ReadOnly =
        "V režimu „Zobrazit jako“ nelze nic ukládat ani měnit. Pro úpravy ukončete pohled tlačítkem „Zrušit pohled jako“ v liště pod hlavičkou.";

    public const string OnlySuperAdmin = "Zobrazit aplikaci jako jiný uživatel může jen superadministrátor.";

    public const string AlreadyActive =
        "Pohled jako už běží. Nejdřív ho ukončete tlačítkem „Zrušit pohled jako“ v liště pod hlavičkou.";

    public const string SelfTarget = "Sám sebe zobrazit nelze — vyberte jiného uživatele.";

    public const string TargetNotFound = "Vybraná osoba v aplikaci neexistuje. Vyberte osobu ze seznamu.";
}
```

`PmTracker.Web/Services/Security/ViewAs/ViewAsRequestState.cs`:

```csharp
namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// Scoped stav pohledu pro jeden request. Čte ho middleware i EF pojistka, která nemá
/// přístup k HttpContextu; služby na pozadí mají vlastní scope, a tedy stav neaktivní.
/// </summary>
public sealed class ViewAsRequestState
{
    public bool IsActive { get; private set; }
    public int? RealOsobaId { get; private set; }
    public int? TargetOsobaId { get; private set; }
    public bool WritesPermitted { get; private set; }

    public bool BlocksWrites => IsActive && !WritesPermitted;

    public void Activate(int realOsobaId, int targetOsobaId)
    {
        IsActive = true;
        RealOsobaId = realOsobaId;
        TargetOsobaId = targetOsobaId;
    }

    public void PermitWrites() => WritesPermitted = true;
}
```

`PmTracker.Web/Services/Security/ViewAs/ViewAsAttributes.cs`:

```csharp
namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// POST akce, která jen čte (náhled, přesměrování na tisk). V pohledu projde,
/// zápis do DB ale dál hlídá <see cref="ViewAsWriteGuardInterceptor"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class AllowDuringViewAsAttribute : Attribute;

/// <summary>Zahájení / ukončení pohledu — v pohledu projde a smí zapsat audit.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ViewAsControlAttribute : Attribute;
```

`PmTracker.Web/Services/Security/ViewAs/LocalReferer.cs`:

```csharp
namespace PmTracker.Web.Services.Security.ViewAs;

public static class LocalReferer
{
    /// <summary>Path+query Refereru, jen když míří na stejný host; jinak null (open-redirect).</summary>
    public static string? Resolve(HttpRequest request)
    {
        var raw = request.Headers.Referer.ToString();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var referer))
        {
            return null;
        }

        return string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
            ? referer.PathAndQuery
            : null;
    }
}
```

`PmTracker.Web/Services/Data/RecordValidationContracts.cs` — do `AjaxErrorCodes` za `EmptyAjaxResponse` přidej:

```csharp
    /// <summary>Režim „Zobrazit jako“ je jen pro čtení. Nesmí být HTTP_403 — ajax.js by spustil session recovery.</summary>
    public const string ViewAsReadOnly = "VIEW_AS_READ_ONLY";
    public const string ViewAsInvalidTarget = "VIEW_AS_INVALID_TARGET";
```

`PmTracker.Web/Services/Audit/AuditModels.cs`:
- do `AuditActionType` za `Reorder` přidej `ViewAsStart, ViewAsStop`
- do `AuditEntityType` za `SdExterniOdkaz` přidej `ViewAs`
- do `ToDatabaseValue(AuditActionType)` přidej `AuditActionType.ViewAsStart => "view_as_start",` a `AuditActionType.ViewAsStop => "view_as_stop",`
- do `ToDatabaseValue(AuditEntityType)` přidej `AuditEntityType.ViewAs => "pohled_jako",`

`PmTracker.Web/Models/ViewModels/ViewAsViewModels.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels;

/// <summary>Kdo se v režimu „Zobrazit jako“ skutečně dívá. Null v kontextu = žádný pohled.</summary>
public sealed record ViewAsInfo(int RealOsobaId, string RealDisplayName);

public sealed record ViewAsBarViewModel(string TargetDisplayName, string RealDisplayName);

public sealed class ViewAsModalViewModel
{
    public required string SearchUrl { get; init; }
}
```

`SecurityViewModels.cs` — do `CurrentUserContextViewModel` za `Authorization` přidej:

```csharp
    /// <summary>
    /// Režim „Zobrazit jako“: kontext patří cílové osobě, tady je skutečně přihlášený superadmin.
    /// Null = běžný provoz.
    /// </summary>
    public ViewAsInfo? ViewAs { get; init; }
```

`NavPermissionsViewModel.cs` — přidej:

```csharp
    public bool CanStartViewAs { get; init; }
    public ViewAsBarViewModel? ViewAs { get; init; }
```

- [ ] **Step 4: Spusť testy, musí projít; ověř i audit testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Security.ViewAs|FullyQualifiedName~AuditWriteServiceTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Security/ViewAs PmTracker.Web/Services/Data/RecordValidationContracts.cs PmTracker.Web/Services/Audit/AuditModels.cs PmTracker.Web/Models/ViewModels/ViewAsViewModels.cs PmTracker.Web/Models/ViewModels/SecurityViewModels.cs PmTracker.Web/Models/ViewModels/NavPermissionsViewModel.cs PmTracker.Tests.Unit/Security/ViewAs
git commit -m "feat(pohled-jako): cookie, stav requestu, atributy, view modely a kódy chyb"
```

---

### Task 2: `ViewAsService` (vyhodnocení cookie, zahájení, ukončení, audit)

**Files:**
- Create: `PmTracker.Web/Services/Security/ViewAs/ViewAsService.cs`
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:70` (registrace)
- Test: `PmTracker.Tests.Unit/Security/ViewAs/ViewAsServiceTests.cs`

**Interfaces:**
- Consumes: `ViewAsCookie`, `AuditActionType.ViewAsStart/ViewAsStop`, `AuditEntityType.ViewAs`, `IAuditWriteService.WriteAsync(int?, AuditWriteEntry, CancellationToken)`, `IPersonIdentityMatcher.BuildPersonName(string?, string, string)`
- Produces:
  - `sealed record ViewAsDecision(int RealOsobaId, string RealDisplayName, int TargetOsobaId)`
  - `enum ViewAsStartResult { Started, SelfTarget, NotSuperAdmin, TargetNotFound }`
  - `interface IViewAsService { Task<ViewAsDecision?> EvaluateAsync(HttpContext httpContext, int realOsobaId, CancellationToken ct); Task<ViewAsStartResult> StartAsync(HttpContext httpContext, int realOsobaId, int targetOsobaId, CancellationToken ct); Task StopAsync(HttpContext httpContext, int realOsobaId, int? targetOsobaId, CancellationToken ct); }`

- [ ] **Step 1: Napiš padající testy**

`PmTracker.Tests.Unit/Security/ViewAs/ViewAsServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class ViewAsServiceTests
{
    private const int Admin = 10;
    private const int OtherAdmin = 11;
    private const int Plain = 20;
    private const int Target = 30;

    private sealed class RecordingAudit : IAuditWriteService
    {
        public List<(int? Actor, AuditWriteEntry Entry)> Written { get; } = [];
        public bool Throw { get; init; }
        public void Add(int? actorOsobaId, AuditWriteEntry entry) => Written.Add((actorOsobaId, entry));
        public Task WriteAsync(int? actorOsobaId, AuditWriteEntry entry, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("audit down");
            Written.Add((actorOsobaId, entry));
            return Task.CompletedTask;
        }
    }

    private static async Task<PmTrackerDbContext> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new PmTrackerDbContext(options);
        db.Osoby.AddRange(
            new OsobaEntity { Id = Admin, Titul = "Ing.", Jmeno = "Ada", Prijmeni = "Adminova" },
            new OsobaEntity { Id = OtherAdmin, Jmeno = "Olga", Prijmeni = "Ostatni" },
            new OsobaEntity { Id = Plain, Jmeno = "Petr", Prijmeni = "Prosty" },
            new OsobaEntity { Id = Target, Jmeno = "Tomas", Prijmeni = "Cil" });
        db.AuthzSuperadmins.AddRange(
            new AuthzSuperadminEntity { OsobaId = Admin },
            new AuthzSuperadminEntity { OsobaId = OtherAdmin });
        await db.SaveChangesAsync();
        return db;
    }

    private static ViewAsService Create(PmTrackerDbContext db, IAuditWriteService? audit = null)
        => new(db, audit ?? new RecordingAudit(), new PersonIdentityMatcher(new TextNormalizer()), NullLogger<ViewAsService>.Instance);

    private static DefaultHttpContext WithCookie(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
        {
            context.Request.Headers.Cookie = $"{ViewAsCookie.Name}={value}";
        }
        return context;
    }

    private static bool CookieDeleted(HttpContext context)
        => context.Response.Headers.SetCookie.ToString().StartsWith($"{ViewAsCookie.Name}=;", StringComparison.Ordinal);

    [Fact]
    public async Task Evaluate_NoCookie_ReturnsNull_AndTouchesNothing()
    {
        await using var db = await SeedAsync();
        var context = WithCookie(null);

        var decision = await Create(db).EvaluateAsync(context, Admin, CancellationToken.None);

        decision.Should().BeNull();
        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluate_ValidSuperadminCookie_ReturnsTargetAndRealName()
    {
        await using var db = await SeedAsync();
        var context = WithCookie($"{Admin}-{Target}");

        var decision = await Create(db).EvaluateAsync(context, Admin, CancellationToken.None);

        decision.Should().NotBeNull();
        decision!.TargetOsobaId.Should().Be(Target);
        decision.RealOsobaId.Should().Be(Admin);
        decision.RealDisplayName.Should().Contain("Adminova");
        CookieDeleted(context).Should().BeFalse();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("11-30")]   // cookie patří jinému skutečnému uživateli
    [InlineData("10-10")]   // cíl = sám sebe
    [InlineData("10-999")]  // cílová osoba neexistuje
    public async Task Evaluate_InvalidCookie_IsIgnoredAndDeleted(string cookie)
    {
        await using var db = await SeedAsync();
        var context = WithCookie(cookie);

        var decision = await Create(db).EvaluateAsync(context, Admin, CancellationToken.None);

        decision.Should().BeNull();
        CookieDeleted(context).Should().BeTrue();
    }

    [Fact]
    public async Task Evaluate_RealUserNotSuperadmin_IsIgnoredAndDeleted()
    {
        await using var db = await SeedAsync();
        var context = WithCookie($"{Plain}-{Target}");

        var decision = await Create(db).EvaluateAsync(context, Plain, CancellationToken.None);

        decision.Should().BeNull();
        CookieDeleted(context).Should().BeTrue();
    }

    [Fact]
    public async Task Start_Superadmin_WritesAuditThenCookie()
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        var result = await Create(db, audit).StartAsync(context, Admin, Target, CancellationToken.None);

        result.Should().Be(ViewAsStartResult.Started);
        audit.Written.Should().ContainSingle();
        audit.Written[0].Actor.Should().Be(Admin);
        audit.Written[0].Entry.ActionType.Should().Be(AuditActionType.ViewAsStart);
        audit.Written[0].Entry.EntityType.Should().Be(AuditEntityType.ViewAs);
        audit.Written[0].Entry.EntityId.Should().Be(Target.ToString());
        context.Response.Headers.SetCookie.ToString().Should().StartWith($"{ViewAsCookie.Name}={Admin}-{Target}");
    }

    [Theory]
    [InlineData(Plain, Target, ViewAsStartResult.NotSuperAdmin)]
    [InlineData(Admin, Admin, ViewAsStartResult.SelfTarget)]
    [InlineData(Admin, 999, ViewAsStartResult.TargetNotFound)]
    [InlineData(Admin, 0, ViewAsStartResult.TargetNotFound)]
    public async Task Start_Rejected_WritesNoCookieAndNoAudit(int real, int target, ViewAsStartResult expected)
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        var result = await Create(db, audit).StartAsync(context, real, target, CancellationToken.None);

        result.Should().Be(expected);
        audit.Written.Should().BeEmpty();
        context.Response.Headers.SetCookie.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_AuditFails_NoCookie()
    {
        await using var db = await SeedAsync();
        var context = new DefaultHttpContext();

        var act = () => Create(db, new RecordingAudit { Throw = true }).StartAsync(context, Admin, Target, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Response.Headers.SetCookie.Should().BeEmpty("bez auditu se pohled nesmí zahájit");
    }

    [Fact]
    public async Task Stop_DeletesCookie_AndAudits()
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        await Create(db, audit).StopAsync(context, Admin, Target, CancellationToken.None);

        CookieDeleted(context).Should().BeTrue();
        audit.Written.Should().ContainSingle(x => x.Entry.ActionType == AuditActionType.ViewAsStop);
    }

    [Fact]
    public async Task Stop_AuditFails_StillDeletesCookie_AndDoesNotThrow()
    {
        await using var db = await SeedAsync();
        var context = new DefaultHttpContext();

        await Create(db, new RecordingAudit { Throw = true }).StopAsync(context, Admin, Target, CancellationToken.None);

        CookieDeleted(context).Should().BeTrue("ukončení pohledu musí projít vždy");
    }

    [Fact]
    public async Task Stop_WithoutActiveTarget_DeletesCookie_WithoutAudit()
    {
        await using var db = await SeedAsync();
        var audit = new RecordingAudit();
        var context = new DefaultHttpContext();

        await Create(db, audit).StopAsync(context, Admin, null, CancellationToken.None);

        CookieDeleted(context).Should().BeTrue();
        audit.Written.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Spusť, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ViewAsServiceTests" --nologo`
Expected: FAIL — `ViewAsService` neexistuje.

- [ ] **Step 3: Implementuj službu**

`PmTracker.Web/Services/Security/ViewAs/ViewAsService.cs`:

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Security.ViewAs;

public sealed record ViewAsDecision(int RealOsobaId, string RealDisplayName, int TargetOsobaId);

public enum ViewAsStartResult
{
    Started,
    SelfTarget,
    NotSuperAdmin,
    TargetNotFound
}

public interface IViewAsService
{
    Task<ViewAsDecision?> EvaluateAsync(HttpContext httpContext, int realOsobaId, CancellationToken ct);
    Task<ViewAsStartResult> StartAsync(HttpContext httpContext, int realOsobaId, int targetOsobaId, CancellationToken ct);
    Task StopAsync(HttpContext httpContext, int realOsobaId, int? targetOsobaId, CancellationToken ct);
}

public sealed class ViewAsService(
    PmTrackerDbContext dbContext,
    IAuditWriteService auditWriteService,
    IPersonIdentityMatcher personIdentityMatcher,
    ILogger<ViewAsService> logger) : IViewAsService
{
    public async Task<ViewAsDecision?> EvaluateAsync(HttpContext httpContext, int realOsobaId, CancellationToken ct)
    {
        var raw = httpContext.Request.Cookies[ViewAsCookie.Name];
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (!ViewAsCookie.TryParse(raw, out var cookieRealOsobaId, out var targetOsobaId))
        {
            return Reject(httpContext, "neplatný formát cookie", realOsobaId, null);
        }

        if (cookieRealOsobaId != realOsobaId)
        {
            return Reject(httpContext, "cookie patří jinému přihlášenému uživateli", realOsobaId, targetOsobaId);
        }

        if (targetOsobaId == realOsobaId)
        {
            return Reject(httpContext, "cílem je sám přihlášený uživatel", realOsobaId, targetOsobaId);
        }

        if (!await IsSuperAdminAsync(realOsobaId, ct))
        {
            return Reject(httpContext, "přihlášený uživatel není superadmin", realOsobaId, targetOsobaId);
        }

        if (!await PersonExistsAsync(targetOsobaId, ct))
        {
            return Reject(httpContext, "cílová osoba neexistuje", realOsobaId, targetOsobaId);
        }

        var realDisplayName = await LoadDisplayNameAsync(realOsobaId, ct);
        return new ViewAsDecision(realOsobaId, realDisplayName, targetOsobaId);
    }

    public async Task<ViewAsStartResult> StartAsync(HttpContext httpContext, int realOsobaId, int targetOsobaId, CancellationToken ct)
    {
        if (targetOsobaId == realOsobaId)
        {
            return ViewAsStartResult.SelfTarget;
        }

        if (!await IsSuperAdminAsync(realOsobaId, ct))
        {
            return ViewAsStartResult.NotSuperAdmin;
        }

        if (targetOsobaId <= 0 || !await PersonExistsAsync(targetOsobaId, ct))
        {
            return ViewAsStartResult.TargetNotFound;
        }

        // Audit PŘED cookie: když se audit nepovede, pohled nezačne.
        await auditWriteService.WriteAsync(realOsobaId, BuildAuditEntry(AuditActionType.ViewAsStart, realOsobaId, targetOsobaId), ct);
        httpContext.Response.Cookies.Append(
            ViewAsCookie.Name,
            ViewAsCookie.Format(realOsobaId, targetOsobaId),
            ViewAsCookie.BuildOptions(httpContext.Request));

        logger.LogInformation(
            "Pohled jako zahájen. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
            realOsobaId,
            targetOsobaId);
        return ViewAsStartResult.Started;
    }

    public async Task StopAsync(HttpContext httpContext, int realOsobaId, int? targetOsobaId, CancellationToken ct)
    {
        ViewAsCookie.Delete(httpContext);
        if (targetOsobaId is null)
        {
            return;
        }

        // Ukončení musí projít vždy — selhání auditu jen zalogujeme.
        try
        {
            await auditWriteService.WriteAsync(realOsobaId, BuildAuditEntry(AuditActionType.ViewAsStop, realOsobaId, targetOsobaId.Value), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Audit ukončení pohledu jako selhal. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
                realOsobaId,
                targetOsobaId);
        }

        logger.LogInformation(
            "Pohled jako ukončen. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
            realOsobaId,
            targetOsobaId);
    }

    private ViewAsDecision? Reject(HttpContext httpContext, string reason, int realOsobaId, int? targetOsobaId)
    {
        logger.LogWarning(
            "Cookie pohledu jako zamítnuto ({Reason}) a smazáno. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
            reason,
            realOsobaId,
            targetOsobaId);
        ViewAsCookie.Delete(httpContext);
        return null;
    }

    private Task<bool> IsSuperAdminAsync(int osobaId, CancellationToken ct)
        => dbContext.AuthzSuperadmins.AsNoTracking().AnyAsync(x => x.OsobaId == osobaId, ct);

    private Task<bool> PersonExistsAsync(int osobaId, CancellationToken ct)
        => dbContext.Osoby.AsNoTracking().AnyAsync(x => x.Id == osobaId, ct);

    private async Task<string> LoadDisplayNameAsync(int osobaId, CancellationToken ct)
    {
        var person = await dbContext.Osoby.AsNoTracking()
            .Where(x => x.Id == osobaId)
            .Select(x => new { x.Titul, x.Jmeno, x.Prijmeni })
            .FirstOrDefaultAsync(ct);
        var name = person is null ? null : personIdentityMatcher.BuildPersonName(person.Titul, person.Jmeno, person.Prijmeni);
        return string.IsNullOrWhiteSpace(name) ? $"Uživatel #{osobaId}" : name;
    }

    private static AuditWriteEntry BuildAuditEntry(AuditActionType action, int realOsobaId, int targetOsobaId)
        => new(
            action,
            AuditEntityType.ViewAs,
            targetOsobaId.ToString(CultureInfo.InvariantCulture),
            BeforeState: null,
            AfterState: new { realOsobaId, targetOsobaId });
}
```

`PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` — hned pod `services.AddScoped<IUserContextResolver, UserContextResolver>();` přidej (a `using PmTracker.Web.Services.Security.ViewAs;`):

```csharp
        services.AddScoped<ViewAsRequestState>();
        services.AddScoped<IViewAsService, ViewAsService>();
```

- [ ] **Step 4: Spusť, musí projít**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ViewAsServiceTests" --nologo`
Expected: PASS (16 testů).

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/Security/ViewAs/ViewAsService.cs PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs PmTracker.Tests.Unit/Security/ViewAs/ViewAsServiceTests.cs
git commit -m "feat(pohled-jako): služba pro vyhodnocení cookie, zahájení a ukončení s auditem"
```

---

### Task 3: EF pojistka proti zápisu (`ViewAsWriteGuardInterceptor`)

**Files:**
- Create: `PmTracker.Web/Services/Security/ViewAs/SqlWriteCommandClassifier.cs`
- Create: `PmTracker.Web/Services/Security/ViewAs/ViewAsWriteGuardInterceptor.cs`
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:48-68` (`AddInterceptors`) a registrace
- Test: `PmTracker.Tests.Unit/Security/ViewAs/SqlWriteCommandClassifierTests.cs`
- Test: `PmTracker.Tests.Integration/DataStore/ViewAsWriteGuardDataStoreTests.cs`

**Interfaces:**
- Consumes: `ViewAsRequestState.BlocksWrites/RealOsobaId/TargetOsobaId/Activate/PermitWrites`, `ViewAsMessages.ReadOnly`
- Produces:
  - `static class SqlWriteCommandClassifier { static bool IsWrite(string? commandText); }`
  - `sealed class ViewAsWriteBlockedException : InvalidOperationException { static bool IsInChain(Exception? exception); }`
  - `sealed class ViewAsWriteGuardInterceptor(ViewAsRequestState state, ILogger<ViewAsWriteGuardInterceptor> logger) : DbCommandInterceptor`

- [ ] **Step 1: Padající testy klasifikátoru**

`PmTracker.Tests.Unit/Security/ViewAs/SqlWriteCommandClassifierTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class SqlWriteCommandClassifierTests
{
    [Theory]
    [InlineData("SET IMPLICIT_TRANSACTIONS OFF;\nSET NOCOUNT ON;\nINSERT INTO [osoby] ([jmeno]) OUTPUT INSERTED.[id] VALUES (@p0);")]
    [InlineData("SET NOCOUNT ON;\nUPDATE [projektove_zaznamy] SET [nazev] = @p0 WHERE [id] = @p1;\nSELECT @@ROWCOUNT;")]
    [InlineData("SET NOCOUNT ON;\nDELETE FROM [vyjadreni] WHERE [id] = @p0;")]
    [InlineData("SET NOCOUNT ON;\nMERGE [osoby] USING (VALUES (@p0, 0)) AS i ([jmeno], _Position) ON 1=0 WHEN NOT MATCHED THEN INSERT ([jmeno]) VALUES (i.[jmeno]);")]
    [InlineData("UPDATE [z]\nSET [z].[heartbeat_at] = @__nowUtc_0\nFROM [zaznam_edit_zamek] AS [z]\nWHERE [z].[zaznam_id] = @__zaznamId_1")]
    [InlineData("DELETE FROM [z]\nFROM [zaznam_edit_zamek] AS [z]\nWHERE [z].[zaznam_id] = @__zaznamId_0")]
    [InlineData("\nMERGE dbo.zaznam_edit_zamek WITH (HOLDLOCK) AS target\nUSING (SELECT @p0 AS zaznam_id) AS source")]
    [InlineData("-- tag\ninsert into x values (1)")]
    [InlineData("TRUNCATE TABLE dbo.x")]
    public void IsWrite_DetectsWriteStatements(string sql)
        => SqlWriteCommandClassifier.IsWrite(sql).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SELECT [o].[id], [o].[jmeno] FROM [osoby] AS [o] WHERE [o].[id] = @__id_0")]
    [InlineData("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE id = @p0")]
    [InlineData("SELECT [z].[datum_update], [z].[last_insert_note] FROM [z]")]
    [InlineData("WITH cte AS (SELECT 1 AS x) SELECT x FROM cte")]
    [InlineData("SELECT CASE WHEN EXISTS (SELECT 1 FROM [authz].[superadmins] AS [s] WHERE [s].[osoba_id] = @__osobaId_0) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END")]
    public void IsWrite_IgnoresReads(string? sql)
        => SqlWriteCommandClassifier.IsWrite(sql).Should().BeFalse();
}
```

- [ ] **Step 2: Spusť, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SqlWriteCommandClassifierTests" --nologo`
Expected: FAIL — typ neexistuje.

- [ ] **Step 3: Implementuj klasifikátor a interceptor**

`PmTracker.Web/Services/Security/ViewAs/SqlWriteCommandClassifier.cs`:

```csharp
using System.Text.RegularExpressions;

namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// Zapisující příkaz = řádek nebo příkaz za středníkem začíná INSERT/UPDATE/DELETE/MERGE/TRUNCATE.
/// Pokrývá EF dávky (SET NOCOUNT ON; INSERT …), ExecuteUpdate/Delete i raw MERGE zámku karty.
/// SELECT s WITH (UPDLOCK) ani sloupce typu datum_update nevyhoví (podtržítko není hranice slova).
/// </summary>
public static partial class SqlWriteCommandClassifier
{
    [GeneratedRegex(
        @"(?:^|;)\s*(?:INSERT|UPDATE|DELETE|MERGE|TRUNCATE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex WriteStatement();

    public static bool IsWrite(string? commandText)
        => !string.IsNullOrWhiteSpace(commandText) && WriteStatement().IsMatch(commandText);
}
```

`PmTracker.Web/Services/Security/ViewAs/ViewAsWriteGuardInterceptor.cs`:

```csharp
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PmTracker.Web.Services.Security.ViewAs;

public sealed class ViewAsWriteBlockedException() : InvalidOperationException(ViewAsMessages.ReadOnly)
{
    /// <summary>EF zabalí výjimku z dávky SaveChanges do DbUpdateException — hledáme v celém řetězci.</summary>
    public static bool IsInChain(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is ViewAsWriteBlockedException)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Pojistka „jen pro čtení“: v requestu s aktivním pohledem odmítne každý zapisující SQL příkaz,
/// i ten, o kterém middleware neví (zápis při GET). Scoped — sdílí <see cref="ViewAsRequestState"/>
/// s requestem; služby na pozadí mají vlastní scope s neaktivním stavem.
/// </summary>
public sealed class ViewAsWriteGuardInterceptor(
    ViewAsRequestState state,
    ILogger<ViewAsWriteGuardInterceptor> logger) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Guard(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Guard(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Guard(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Guard(command);
        return ValueTask.FromResult(result);
    }

    private void Guard(DbCommand command)
    {
        if (!state.BlocksWrites || !SqlWriteCommandClassifier.IsWrite(command.CommandText))
        {
            return;
        }

        var sql = command.CommandText;
        logger.LogError(
            "Pohled jako: zablokován zápis do databáze. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId} Sql={Sql}",
            state.RealOsobaId,
            state.TargetOsobaId,
            sql.Length > 300 ? sql[..300] : sql);
        throw new ViewAsWriteBlockedException();
    }
}
```

`PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs`:
- v lambdě `AddDbContext` za `optionsBuilder.UseSqlServer(...)` (za zavírací `});` volání `UseSqlServer`) přidej:

```csharp
            // Pohled jako: pojistka jen-pro-čtení. sp je scope requestu (DbContextOptions jsou scoped).
            optionsBuilder.AddInterceptors(sp.GetRequiredService<ViewAsWriteGuardInterceptor>());
```

- k registracím z Task 2 přidej `services.AddScoped<ViewAsWriteGuardInterceptor>();`

- [ ] **Step 4: Spusť unit testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~SqlWriteCommandClassifierTests" --nologo`
Expected: PASS.

- [ ] **Step 5: Padající integrační test pojistky proti reálné DB**

`PmTracker.Tests.Integration/DataStore/ViewAsWriteGuardDataStoreTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>Pojistka pohledu jako proti reálnému SQL Serveru (InMemory DbCommand nepouští).</summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ViewAsWriteGuardDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ViewAsWriteGuardDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static PmTrackerDbContext Create(string connectionString, ViewAsRequestState state)
        => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new ViewAsWriteGuardInterceptor(state, NullLogger<ViewAsWriteGuardInterceptor>.Instance))
            .Options);

    private static ViewAsRequestState Active()
    {
        var state = new ViewAsRequestState();
        state.Activate(1, 2);
        return state;
    }

    [Fact]
    public async Task ActiveViewAs_ReadsWork()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_reads");
        await using var dbContext = Create(db.ConnectionString, Active());

        var count = await dbContext.Osoby.CountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ActiveViewAs_SaveChangesInsert_IsBlocked()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_insert");
        await using var dbContext = Create(db.ConnectionString, Active());
        var orgId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync();
        dbContext.Osoby.Add(new OsobaEntity { Jmeno = "Blok", Prijmeni = "Zapisu", OrganizaceId = orgId });

        var act = () => dbContext.SaveChangesAsync();

        (await act.Should().ThrowAsync<Exception>())
            .Which.Should().Match<Exception>(ex => ViewAsWriteBlockedException.IsInChain(ex));
        await using var check = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        (await check.Osoby.AnyAsync(x => x.Prijmeni == "Zapisu")).Should().BeFalse();
    }

    [Fact]
    public async Task ActiveViewAs_ExecuteUpdate_IsBlocked()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_execupdate");
        await using var dbContext = Create(db.ConnectionString, Active());

        var act = () => dbContext.Osoby.Where(x => x.Id < 0).ExecuteUpdateAsync(s => s.SetProperty(x => x.Jmeno, "X"));

        await act.Should().ThrowAsync<ViewAsWriteBlockedException>();
    }

    [Fact]
    public async Task ActiveViewAs_RawMerge_IsBlocked()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_merge");
        await using var dbContext = Create(db.ConnectionString, Active());

        var act = () => dbContext.Database.ExecuteSqlRawAsync(
            "MERGE dbo.zaznam_edit_zamek AS t USING (SELECT -1 AS zaznam_id) AS s ON t.zaznam_id = s.zaznam_id WHEN MATCHED THEN DELETE;");

        await act.Should().ThrowAsync<ViewAsWriteBlockedException>();
    }

    [Fact]
    public async Task InactiveOrPermitted_WritesWork()
    {
        var db = await _fixture.CreateDatabaseAsync("viewas_permitted");
        var permitted = Active();
        permitted.PermitWrites();

        foreach (var state in new[] { new ViewAsRequestState(), permitted })
        {
            await using var dbContext = Create(db.ConnectionString, state);
            var orgId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync();
            dbContext.Osoby.Add(new OsobaEntity { Jmeno = "Smi", Prijmeni = "Zapsat", OrganizaceId = orgId });

            await dbContext.SaveChangesAsync();
        }

        await using var check = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        (await check.Osoby.CountAsync(x => x.Prijmeni == "Zapsat")).Should().Be(2);
    }
}
```

- [ ] **Step 6: Spusť integrační test (Colima + SQL kontejner musí běžet)**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ViewAsWriteGuardDataStoreTests" --nologo`
Expected: PASS (5 testů). Pokud `ActiveViewAs_SaveChangesInsert_IsBlocked` hlásí jinou výjimku bez `ViewAsWriteBlockedException` v řetězci, je špatně registrace interceptoru, ne test.

- [ ] **Step 7: Regrese — interceptor je teď v každém DbContextu aplikace**

Run: `dotnet test PmTracker.Tests.Api --nologo`
Expected: PASS (žádný request nemá aktivní pohled, pojistka nesmí nic měnit). V logu testů nesmí být varování EF `ManyServiceProvidersCreatedWarning`. Instance interceptoru na scope nemá měnit interní service provider EF. Kdyby se varování objevilo, přepni registraci na singleton interceptor, který stav čte přes `IHttpContextAccessor.HttpContext?.RequestServices.GetService<ViewAsRequestState>()`.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Services/Security/ViewAs PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs PmTracker.Tests.Unit/Security/ViewAs/SqlWriteCommandClassifierTests.cs PmTracker.Tests.Integration/DataStore/ViewAsWriteGuardDataStoreTests.cs
git commit -m "feat(pohled-jako): EF pojistka blokující zápis během pohledu"
```

---

### Task 4: `ViewAsReadOnlyMiddleware` a zařazení do pipeline

**Files:**
- Create: `PmTracker.Web/Middleware/ViewAsReadOnlyMiddleware.cs`
- Modify: `PmTracker.Web/Program.cs:147-148`
- Modify: `PmTracker.Tests.Unit/Architecture/MiddlewareOrderingTests.cs`
- Test: `PmTracker.Tests.Unit/Security/ViewAs/ViewAsReadOnlyMiddlewareTests.cs`

**Interfaces:**
- Consumes: `ViewAsRequestState`, `AllowDuringViewAsAttribute`, `ViewAsControlAttribute`, `ViewAsWriteBlockedException.IsInChain`, `LocalReferer.Resolve`, `ViewAsMessages.ReadOnly`, `AjaxErrorCodes.ViewAsReadOnly`, `ModalSubmitResultViewModel`
- Produces: `sealed class ViewAsReadOnlyMiddleware(RequestDelegate next, ILogger<ViewAsReadOnlyMiddleware> logger) { Task InvokeAsync(HttpContext context, ViewAsRequestState state); }`

- [ ] **Step 1: Padající testy middleware**

`PmTracker.Tests.Unit/Security/ViewAs/ViewAsReadOnlyMiddlewareTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Middleware;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Unit.Security.ViewAs;

public sealed class ViewAsReadOnlyMiddlewareTests
{
    private sealed class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object>? Saved { get; private set; }
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) => Saved = new Dictionary<string, object>(values);
    }

    private static (DefaultHttpContext Context, FakeTempDataProvider TempData) Build(string method, object? endpointMetadata = null, bool ajax = false, string? referer = null)
    {
        var tempData = new FakeTempDataProvider();
        var services = new ServiceCollection()
            .AddSingleton<ITempDataProvider>(tempData)
            .AddSingleton<ITempDataDictionaryFactory, TempDataDictionaryFactory>()
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = method;
        context.Request.Host = new HostString("pmtracker.local");
        context.Response.Body = new MemoryStream();
        if (ajax) context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        if (referer is not null) context.Request.Headers.Referer = referer;
        var metadata = endpointMetadata is null ? new EndpointMetadataCollection() : new EndpointMetadataCollection(endpointMetadata);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return (context, tempData);
    }

    private static ViewAsRequestState ActiveState()
    {
        var state = new ViewAsRequestState();
        state.Activate(1, 2);
        return state;
    }

    private static ViewAsReadOnlyMiddleware Middleware(RequestDelegate next)
        => new(next, NullLogger<ViewAsReadOnlyMiddleware>.Instance);

    private static async Task<JsonElement> ReadJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    [Fact]
    public async Task Inactive_PostPassesThrough()
    {
        var (context, _) = Build("POST", ajax: true);
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, new ViewAsRequestState());

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Active_GetPassesThrough_WritesStayBlocked()
    {
        var (context, _) = Build("GET");
        var state = ActiveState();
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, state);

        called.Should().BeTrue();
        state.BlocksWrites.Should().BeTrue();
    }

    [Fact]
    public async Task Active_AjaxPost_Returns403JsonWithViewAsCode_AndSkipsAction()
    {
        var (context, _) = Build("POST", ajax: true);
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, ActiveState());

        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var json = await ReadJsonAsync(context);
        json.GetProperty("ok").GetBoolean().Should().BeFalse();
        json.GetProperty("errorCode").GetString().Should().Be("VIEW_AS_READ_ONLY");
        json.GetProperty("message").GetString().Should().Be(ViewAsMessages.ReadOnly);
    }

    [Fact]
    public async Task Active_FormPost_RedirectsToLocalRefererWithTempDataMessage()
    {
        var (context, tempData) = Build("POST", referer: "https://pmtracker.local/Jednani/Detail/4?asUser=1");

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, ActiveState());

        context.Response.StatusCode.Should().Be(StatusCodes.Status303SeeOther);
        context.Response.Headers.Location.ToString().Should().Be("/Jednani/Detail/4?asUser=1");
        tempData.Saved.Should().ContainKey("ErrorMessage").WhoseValue.Should().Be(ViewAsMessages.ReadOnly);
    }

    [Fact]
    public async Task Active_FormPost_ForeignReferer_RedirectsToRoot()
    {
        var (context, _) = Build("POST", referer: "https://evil.example/x");

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, ActiveState());

        context.Response.Headers.Location.ToString().Should().Be("/");
    }

    [Fact]
    public async Task Active_AllowDuringViewAsPost_Passes_ButWritesStayBlocked()
    {
        var (context, _) = Build("POST", new AllowDuringViewAsAttribute(), ajax: true);
        var state = ActiveState();
        var called = false;

        await Middleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context, state);

        called.Should().BeTrue();
        state.BlocksWrites.Should().BeTrue("čtecí POST nesmí odemknout zápis");
    }

    [Fact]
    public async Task Active_ViewAsControlPost_Passes_AndPermitsWrites()
    {
        var (context, _) = Build("POST", new ViewAsControlAttribute());
        var state = ActiveState();

        await Middleware(_ => Task.CompletedTask).InvokeAsync(context, state);

        state.WritesPermitted.Should().BeTrue();
    }

    [Fact]
    public async Task Active_GetThatHitsWriteGuard_BecomesBlockedResponse()
    {
        var (context, _) = Build("GET", ajax: true);

        await Middleware(_ => throw new DbUpdateException("wrapped", new ViewAsWriteBlockedException()))
            .InvokeAsync(context, ActiveState());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await ReadJsonAsync(context)).GetProperty("errorCode").GetString().Should().Be("VIEW_AS_READ_ONLY");
    }

    [Fact]
    public async Task Active_UnrelatedException_IsRethrown()
    {
        var (context, _) = Build("GET");

        var act = () => Middleware(_ => throw new InvalidOperationException("jiná chyba")).InvokeAsync(context, ActiveState());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("jiná chyba");
    }
}
```

- [ ] **Step 2: Spusť, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ViewAsReadOnlyMiddlewareTests" --nologo`
Expected: FAIL — `ViewAsReadOnlyMiddleware` neexistuje.

- [ ] **Step 3: Implementuj middleware**

`PmTracker.Web/Middleware/ViewAsReadOnlyMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Web.Middleware;

/// <summary>
/// Režim „Zobrazit jako“ je jen pro čtení. Běží za UserContextMiddleware (ten stav aktivuje)
/// a před MVC, takže pokryje i controllery mimo BaseController. Vedle toho převádí výjimku
/// EF pojistky (zápis při GET) na stejnou srozumitelnou odpověď.
/// </summary>
public sealed class ViewAsReadOnlyMiddleware(RequestDelegate next, ILogger<ViewAsReadOnlyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ViewAsRequestState state)
    {
        if (!state.IsActive)
        {
            await next(context);
            return;
        }

        var metadata = context.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<ViewAsControlAttribute>() is not null)
        {
            state.PermitWrites();
            await next(context);
            return;
        }

        if (!IsSafeMethod(context.Request.Method) && metadata?.GetMetadata<AllowDuringViewAsAttribute>() is null)
        {
            logger.LogInformation(
                "Pohled jako: odmítnut {Method} {Path}. RealOsobaId={RealOsobaId} TargetOsobaId={TargetOsobaId}",
                context.Request.Method,
                context.Request.Path.Value,
                state.RealOsobaId,
                state.TargetOsobaId);
            await WriteBlockedAsync(context);
            return;
        }

        try
        {
            await next(context);
        }
        catch (Exception ex) when (ViewAsWriteBlockedException.IsInChain(ex) && !context.Response.HasStarted)
        {
            context.Response.Clear();
            await WriteBlockedAsync(context);
        }
    }

    private static bool IsSafeMethod(string method)
        => HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static async Task WriteBlockedAsync(HttpContext context)
    {
        var request = context.Request;
        if (WantsJson(request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ModalSubmitResultViewModel
            {
                Ok = false,
                Message = ViewAsMessages.ReadOnly,
                ErrorCode = AjaxErrorCodes.ViewAsReadOnly,
                TraceId = context.TraceIdentifier
            });
            return;
        }

        if (!IsSafeMethod(request.Method))
        {
            // Klasický formulář: zpět na stránku, hlášku ukáže layout (TempData["ErrorMessage"]).
            var tempData = context.RequestServices
                .GetRequiredService<ITempDataDictionaryFactory>()
                .GetTempData(context);
            tempData["ErrorMessage"] = ViewAsMessages.ReadOnly;
            tempData.Save();
            context.Response.StatusCode = StatusCodes.Status303SeeOther;
            context.Response.Headers.Location = LocalReferer.Resolve(request) ?? "/";
            return;
        }

        // Zápis při GET bez AJAXu: vědomě jen prostý text — jde o pojistku pro nečekaný případ.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync($"{ViewAsMessages.ReadOnly} ({AjaxErrorCodes.ViewAsReadOnly})");
    }

    private static bool WantsJson(HttpRequest request)
        => string.Equals(request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
           || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
           || (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false);
}
```

`PmTracker.Web/Program.cs` — hned pod `app.UseMiddleware<UserContextMiddleware>();` přidej:

```csharp
// Pohled jako: blokace zápisu. Za UserContextMiddleware (aktivuje stav), před MVC.
app.UseMiddleware<ViewAsReadOnlyMiddleware>();
```

- [ ] **Step 4: Rozšiř test pořadí middleware**

V `PmTracker.Tests.Unit/Architecture/MiddlewareOrderingTests.cs` přidej nový test:

```csharp
    [Fact]
    public void Program_ShouldPlaceViewAsMiddleware_AfterUserContext_AndBeforeAuthorization()
    {
        var programPath = Path.Combine(ArchitectureTestBase.RepoRoot(), "PmTracker.Web", "Program.cs");
        var lines = File.ReadAllLines(programPath);

        int? userCtxLine = null, viewAsLine = null, authzLine = null;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("UseMiddleware<UserContextMiddleware>")) userCtxLine = i;
            if (lines[i].Contains("UseMiddleware<ViewAsReadOnlyMiddleware>")) viewAsLine = i;
            if (lines[i].Contains("app.UseAuthorization()")) authzLine = i;
        }

        viewAsLine.Should().NotBeNull("blokace zápisu v pohledu jako musí být v pipeline");
        (userCtxLine < viewAsLine).Should().BeTrue("stav pohledu aktivuje až resolver v UserContextMiddleware");
        (viewAsLine < authzLine).Should().BeTrue("blokace má platit dřív, než se spustí MVC");
    }
```

- [ ] **Step 5: Spusť testy**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ViewAsReadOnlyMiddlewareTests|FullyQualifiedName~MiddlewareOrderingTests" --nologo`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Middleware/ViewAsReadOnlyMiddleware.cs PmTracker.Web/Program.cs PmTracker.Tests.Unit/Security/ViewAs/ViewAsReadOnlyMiddlewareTests.cs PmTracker.Tests.Unit/Architecture/MiddlewareOrderingTests.cs
git commit -m "feat(pohled-jako): middleware blokující zápis během pohledu"
```

---

### Task 5: Zámek karty, KeepAlive a harvest v pohledu bez zápisu

**Files:**
- Modify: `PmTracker.Web/Services/Records/RecordEditLockService.cs` (interface + `PeekAsync`)
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs:132` a `:147-150`
- Modify: `PmTracker.Web/Controllers/AppController.cs:64`
- Modify: `PmTracker.Tests.Unit/Controllers/ZaznamyControllerEditAuthzTests.cs:107-117` (fake)
- Test: `PmTracker.Tests.Integration/DataStore/RecordEditLockDataStoreTests.cs`

Pozn.: `CurrentUserContext.ViewAs` existuje od Task 1. Naplní ho až resolver v Task 6, do té doby je vždy null, takže změny v tomto tasku chování nemění. Ověří je Api testy v Task 7.

**Interfaces:**
- Consumes: `CurrentUserContextViewModel.ViewAs` (Task 1)
- Produces: `Task<RecordEditLockResult> IRecordEditLockService.PeekAsync(int zaznamId, int osobaId, CancellationToken ct = default)`: stejná sémantika jako `TryAcquireAsync`, ale **bez zápisu**. `Acquired = true`, když zámek neexistuje, patří `osobaId` nebo vypršel (`HeartbeatAt < now - Ttl`). Jinak `false` s držitelem.

- [ ] **Step 1: Padající integrační testy `PeekAsync`**

Do `RecordEditLockDataStoreTests` přidej:

```csharp
    [Fact]
    public async Task Peek_ShouldReportHolder_WithoutWriting()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_peek_holder");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKPEEK1");
        var viewer = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockPeekViewer");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);
        await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);
        var before = await dbContext.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == scenario.RecordId);

        var result = await service.PeekAsync(scenario.RecordId, viewer);

        result.Acquired.Should().BeFalse();
        result.HolderOsobaId.Should().Be(scenario.OsobaId);
        var after = await dbContext.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == scenario.RecordId);
        after.OsobaId.Should().Be(before.OsobaId);
        after.HeartbeatAt.Should().Be(before.HeartbeatAt, "nahlédnutí nesmí prodloužit cizí zámek");
    }

    [Fact]
    public async Task Peek_FreeRecord_ReportsAcquired_AndCreatesNoLock()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_peek_free");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKPEEK2");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        var result = await service.PeekAsync(scenario.RecordId, scenario.OsobaId);

        result.Acquired.Should().BeTrue();
        (await dbContext.ZaznamEditZamky.AnyAsync(x => x.ZaznamId == scenario.RecordId))
            .Should().BeFalse("nahlédnutí nesmí založit zámek");
    }

    [Fact]
    public async Task Peek_ExpiredForeignLock_ReportsAcquired()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_peek_expired");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKPEEK3");
        var viewer = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockPeekViewer3");
        var old = DateTime.UtcNow - RecordEditLockService.Ttl - TimeSpan.FromMinutes(1);
        dbContext.ZaznamEditZamky.Add(new PmTracker.Web.Models.Entities.ZaznamEditZamekEntity
        {
            ZaznamId = scenario.RecordId, OsobaId = scenario.OsobaId, ZiskanoAt = old, HeartbeatAt = old
        });
        await dbContext.SaveChangesAsync();
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        var result = await service.PeekAsync(scenario.RecordId, viewer);

        result.Acquired.Should().BeTrue("vypršelý zámek by si cílový uživatel převzal");
    }
```

- [ ] **Step 2: Spusť, musí selhat na kompilaci**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RecordEditLockDataStoreTests.Peek" --nologo`
Expected: FAIL — `PeekAsync` neexistuje.

- [ ] **Step 3: Implementuj `PeekAsync`**

V `RecordEditLockService.cs` do rozhraní `IRecordEditLockService` přidej:

```csharp
    /// <summary>Stejná odpověď jako <see cref="TryAcquireAsync"/>, ale bez zápisu (režim „Zobrazit jako“).</summary>
    Task<RecordEditLockResult> PeekAsync(int zaznamId, int osobaId, CancellationToken ct = default);
```

a do třídy za `TryAcquireAsync`:

```csharp
    public async Task<RecordEditLockResult> PeekAsync(int zaznamId, int osobaId, CancellationToken ct = default)
    {
        var expiryUtc = timeProvider.GetUtcNow().UtcDateTime - Ttl;
        var zamek = await dbContext.ZaznamEditZamky.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ZaznamId == zaznamId, ct);

        if (zamek is null || zamek.OsobaId == osobaId || zamek.HeartbeatAt < expiryUtc)
        {
            return new RecordEditLockResult(true, osobaId, zamek?.OsobaId == osobaId ? zamek.ZiskanoAt : null);
        }

        return new RecordEditLockResult(false, zamek.OsobaId, zamek.ZiskanoAt);
    }
```

Ve fake `AlwaysAcquiringEditLockService` (`ZaznamyControllerEditAuthzTests.cs`) přidej:

```csharp
        public Task<RecordEditLockResult> PeekAsync(int zaznamId, int osobaId, CancellationToken ct = default)
            => Task.FromResult(new RecordEditLockResult(true, osobaId, null));
```

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~RecordEditLockDataStoreTests" --nologo`
Expected: PASS (staré i nové testy).

- [ ] **Step 4: Editor a KeepAlive v pohledu**

`PmTracker.Web/Controllers/ZaznamyController.cs` — řádek se zámkem nahraď:

```csharp
        // Pohled jako je jen pro čtení: zámek se jen přečte (stejná stránka „záznam upravuje X“
        // jako u cílového uživatele), ale nezaloží ani neprodlouží.
        var lockResult = CurrentUserContext.ViewAs is null
            ? await _editLockService.TryAcquireAsync(id, CurrentUserContext.OsobaId, ct)
            : await _editLockService.PeekAsync(id, CurrentUserContext.OsobaId, ct);
```

a podmínku harvest triggeru změň na:

```csharp
        if (canEditRecord && CurrentUserContext.ViewAs is null)
```

`PmTracker.Web/Controllers/AppController.cs` — podmínku heartbeatu změň na:

```csharp
        // Pohled jako: heartbeat by prodloužil zámek cílového uživatele — vynechat.
        if (zaznamId is > 0 && resolution.UserContext.ViewAs is null)
```

- [ ] **Step 5: Build + dotčené unit testy**

Run: `dotnet build PmTracker.Web --nologo && dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~ZaznamyControllerEditAuthzTests|FullyQualifiedName~RecordEditLock" --nologo`
Expected: build OK, testy PASS. (Chování editoru a KeepAlive v pohledu ověří Api testy v Task 7.)

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/Records/RecordEditLockService.cs PmTracker.Web/Controllers/ZaznamyController.cs PmTracker.Web/Controllers/AppController.cs PmTracker.Tests.Unit/Controllers/ZaznamyControllerEditAuthzTests.cs PmTracker.Tests.Integration/DataStore/RecordEditLockDataStoreTests.cs
git commit -m "feat(pohled-jako): editor a keep-alive v pohledu zámek jen čtou"
```

---

### Task 6: Přepnutí identity v resolveru, controller, menu a lišta

**Files:**
- Modify: `PmTracker.Web/Services/Security/UserContextResolver.cs` (konstruktor, `ResolveAsync` za ř. 311, ř. 318, 333, 384-398)
- Modify: `PmTracker.Web/Controllers/BaseController.cs:125-137`
- Create: `PmTracker.Web/Controllers/PohledJakoController.cs`
- Create: `PmTracker.Web/Views/PohledJako/VyberModal.cshtml`
- Create: `PmTracker.Web/Views/Shared/_ViewAsBar.cshtml`
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml:171-175` (menu) a `:212-218` (lišta)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (za blok `.app-breadcrumb-*`)
- Modify: `PmTracker.Web/Controllers/ExportController.cs:226`, `HarmonogramController.cs:105`, `ExterniOdkazController.cs:49`
- Modify: `PmTracker.Tests.Unit/Architecture/AuthorizationPolicyEnforcementTests.cs` (allowlist)
- Test: `PmTracker.Tests.Api/Controllers/ViewAsFlowTests.cs`

**Interfaces:**
- Consumes: `IViewAsService`, `ViewAsDecision`, `ViewAsStartResult`, `ViewAsRequestState.Activate`, `ViewAsControlAttribute`, `AllowDuringViewAsAttribute`, `ViewAsMessages.*`, `AjaxErrorCodes.ViewAsInvalidTarget`, `LocalReferer.Resolve`, `ViewAsInfo`, `ViewAsBarViewModel`, `ViewAsModalViewModel`, `NavPermissionsViewModel.CanStartViewAs/ViewAs` (Task 1), `IProjectService.SearchProjectMemberCandidatesAsync(string, CancellationToken)`, `BaseController.WithAsUserRoute()`, `AjaxSuccessResult`, `AjaxErrorResult`, `AjaxForbiddenResult`
- Produces:
  - routy `GET /PohledJako/Vyber`, `GET /PohledJako/HledatOsoby?q=`, `POST /PohledJako/Zahajit` (form `OsobaId`), `POST /PohledJako/Ukoncit`
  - DOM kotvy: `data-view-as-start` (položka menu), `data-view-as-bar`, `data-view-as-target`, `data-view-as-stop`, `data-view-as-modal`

- [ ] **Step 1: Napiš padající Api testy průběhu**

`PmTracker.Tests.Api/Controllers/ViewAsFlowTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Pohled jako — zahájení, ukončení, platnost cookie a vykreslení menu a lišty.
/// Kotví na data-* atributy a ASCII jména (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ViewAsFlowTests
{
    private readonly ApiSqlFixture _fixture;

    public ViewAsFlowTests(ApiSqlFixture fixture) => _fixture = fixture;

    private HttpClient Client() => _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });

    private static HttpRequestMessage Request(HttpMethod method, string url, string? viewAsCookie = null, bool ajax = false, Dictionary<string, string>? form = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (viewAsCookie is not null) request.Headers.Add("Cookie", $"{ViewAsCookie.Name}={viewAsCookie}");
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        return request;
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static bool DeletesViewAsCookie(HttpResponseMessage response)
        => SetCookies(response).Any(v => v.StartsWith($"{ViewAsCookie.Name}=;", StringComparison.Ordinal) && v.Contains("1970"));

    private async Task<bool> AuditExistsAsync(string action, int actor, int target)
    {
        await using var db = _fixture.CreateDbContext();
        var entityId = target.ToString();
        return await db.AuthzAuditLog.AnyAsync(x =>
            x.EntityType == "pohled_jako" && x.Action == action && x.ActorOsobaId == actor && x.EntityId == entityId);
    }

    [Fact]
    public async Task Zahajit_Superadmin_SetsCookieAuditsAndRefreshesPage()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsStart");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Zahajit?asUser={_fixture.AdminOsobaId}",
            ajax: true, form: new() { ["OsobaId"] = target.ToString() }));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("refreshScope").GetString().Should().Be("page");
        SetCookies(response).Should().Contain(v => v.StartsWith($"{ViewAsCookie.Name}={_fixture.AdminOsobaId}-{target}", StringComparison.Ordinal));
        (await AuditExistsAsync("view_as_start", _fixture.AdminOsobaId, target)).Should().BeTrue();
    }

    [Fact]
    public async Task Zahajit_NonSuperadmin_Forbidden_NoCookie()
    {
        var plain = await _fixture.EnsurePersonAsync("ApiViewAsPlain");
        var target = await _fixture.EnsurePersonAsync("ApiViewAsPlainTarget");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Zahajit?asUser={plain}",
            ajax: true, form: new() { ["OsobaId"] = target.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        SetCookies(response).Should().NotContain(v => v.StartsWith($"{ViewAsCookie.Name}=", StringComparison.Ordinal) && !v.Contains("1970"));
    }

    [Theory]
    [InlineData("self")]
    [InlineData("missing")]
    public async Task Zahajit_InvalidTarget_ReturnsFieldError(string kind)
    {
        var osobaId = kind == "self" ? _fixture.AdminOsobaId : int.MaxValue;
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Zahajit?asUser={_fixture.AdminOsobaId}",
            ajax: true, form: new() { ["OsobaId"] = osobaId.ToString() }));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("VIEW_AS_INVALID_TARGET");
        json.RootElement.GetProperty("fieldErrors").TryGetProperty("OsobaId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ActiveViewAs_PageShowsBarAndTargetIdentity_HidesStartItemAndAdminNav()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsCil");
        using var client = Client();

        var baseline = await (await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}"))).Content.ReadAsStringAsync();
        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}"));
        var html = await response.Content.ReadAsStringAsync();

        baseline.Should().Contain("data-view-as-start", "superadmin bez pohledu má v menu Zobrazit jako");
        baseline.Should().Contain("data-modal-url=\"/PohledJako/Vyber\"", "atribut na gov-buttonu se nesmí ztratit (Razor gotcha)");
        baseline.Should().Contain("href=\"/Nastaveni\"");
        baseline.Should().NotContain("data-view-as-bar");

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-view-as-bar");
        html.Should().Contain("ApiViewAsCil", "hlavička i lišta ukazují cílového uživatele");
        html.Should().NotContain("data-view-as-start", "během pohledu se nový pohled nezahajuje");
        html.Should().NotContain("href=\"/Nastaveni\"", "cílový uživatel nemá práva nastavení");
    }

    [Fact]
    public async Task ActiveViewAs_ProjectVisibilityFollowsTarget()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsVis");
        var visibleProject = await _fixture.EnsureProjectAsync("APIVAVIS1");
        var hiddenProject = await _fixture.EnsureProjectAsync("APIVAVIS2");
        await _fixture.EnsureProjectRoleAssignmentAsync(visibleProject, target, "PROJ_MAN");
        using var client = Client();

        var html = await (await client.SendAsync(Request(HttpMethod.Get, $"/Projekty?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}")))
            .Content.ReadAsStringAsync();

        // Uzavírací uvozovka: id 12 nesmí matchnout /Projekty/Detail/123 (karta má data-href="…").
        html.Should().Contain($"/Projekty/Detail/{visibleProject}\"");
        html.Should().NotContain($"/Projekty/Detail/{hiddenProject}\"", "superadmin by ho viděl, cílový uživatel ne");
    }

    [Fact]
    public async Task ActiveViewAs_ProfileShowsTargetRoles()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsRoles");
        var project = await _fixture.EnsureProjectAsync("APIVAROLE");
        await _fixture.EnsureProjectRoleAssignmentAsync(project, target, "PROJ_MAN");
        using var client = Client();

        var html = await (await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}")))
            .Content.ReadAsStringAsync();

        html.Should().Contain("PROJ_MAN", "Moje práva v pohledu jsou práva cílového uživatele");
    }

    [Fact]
    public async Task Ukoncit_DeletesCookie_AuditsAndReturnsToReferer()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsStop");
        using var client = Client();
        var request = Request(HttpMethod.Post, $"/PohledJako/Ukoncit?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}",
            form: new());
        request.Headers.Referrer = new Uri($"http://localhost/Projekty?asUser={_fixture.AdminOsobaId}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be($"/Projekty?asUser={_fixture.AdminOsobaId}");
        DeletesViewAsCookie(response).Should().BeTrue();
        (await AuditExistsAsync("view_as_stop", _fixture.AdminOsobaId, target)).Should().BeTrue();
    }

    [Fact]
    public async Task Ukoncit_WithoutActiveViewAs_StillRedirects()
    {
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Ukoncit?asUser={_fixture.AdminOsobaId}", form: new()));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        DeletesViewAsCookie(response).Should().BeTrue();
    }

    [Fact]
    public async Task CookieFromNonSuperadmin_IsIgnoredAndDeleted()
    {
        var plain = await _fixture.EnsurePersonAsync("ApiViewAsPlain2");
        var target = await _fixture.EnsurePersonAsync("ApiViewAsPlain2Target");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={plain}", $"{plain}-{target}"));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-view-as-bar");
        html.Should().NotContain("ApiViewAsPlain2Target");
        DeletesViewAsCookie(response).Should().BeTrue();
    }

    [Fact]
    public async Task CookieOfAnotherRealUser_IsIgnoredAndDeleted()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsForeign");
        var otherAdmin = await _fixture.EnsurePersonAsync("ApiViewAsOtherAdmin");
        await using (var db = _fixture.CreateDbContext())
        {
            if (!await db.AuthzSuperadmins.AnyAsync(x => x.OsobaId == otherAdmin))
            {
                db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = otherAdmin, CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
        }
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}", $"{otherAdmin}-{target}"));
        var html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain("data-view-as-bar");
        DeletesViewAsCookie(response).Should().BeTrue();
    }

    [Fact]
    public async Task Vyber_Superadmin_RendersModalWithPicker_NonAdminForbidden()
    {
        var plain = await _fixture.EnsurePersonAsync("ApiViewAsModalPlain");
        using var client = Client();

        var ok = await client.SendAsync(Request(HttpMethod.Get, $"/PohledJako/Vyber?asUser={_fixture.AdminOsobaId}", ajax: true));
        var html = await ok.Content.ReadAsStringAsync();
        var forbidden = await client.SendAsync(Request(HttpMethod.Get, $"/PohledJako/Vyber?asUser={plain}", ajax: true));

        ok.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-view-as-modal");
        html.Should().Contain("data-person-picker=\"single\"");
        html.Should().Contain("action=\"/PohledJako/Zahajit");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HledatOsoby_ReturnsCandidates_WithoutCurrentUser()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsSearchable");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/PohledJako/HledatOsoby?q=ApiViewAsSearchable&asUser={_fixture.AdminOsobaId}", ajax: true));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var ids = json.RootElement.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain(target);
        ids.Should().NotContain(_fixture.AdminOsobaId);
    }
}
```

- [ ] **Step 2: Spusť, musí selhat (404 na `/PohledJako/*`, chybí lišta)**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ViewAsFlowTests" --nologo`
Expected: FAIL (většina testů: 404 nebo chybějící `data-view-as-*`).

- [ ] **Step 3: Resolver — cílová osoba místo skutečné**

`UserContextResolver.cs`:
- `using PmTracker.Web.Services.Security.ViewAs;`
- pole `private readonly IViewAsService _viewAsService;` a `private readonly ViewAsRequestState _viewAsState;`, parametry konstruktoru `IViewAsService viewAsService, ViewAsRequestState viewAsState` (na konec) a přiřazení
- hned za uzavírací `}` bloku `if (!osobaId.HasValue) { … }` (před `var osoba = await (`) vlož:

```csharp
        // Pohled jako: skutečná osoba je vyřešená, teď se rozhodne, za koho se request vyhodnotí.
        // Identita přihlášeného (HttpContext.User) se nemění — jen osoba_id kontextu.
        var realOsobaId = osobaId.Value;
        var viewAs = await _viewAsService.EvaluateAsync(httpContext, realOsobaId, ct);
        var effectiveOsobaId = viewAs?.TargetOsobaId ?? realOsobaId;
```

- v dotazu na osobu změň `where person.Id == osobaId.Value` na `where person.Id == effectiveOsobaId`
- v logu `ResolvedPersonMissingInDatabase` změň argument `osobaId` na `effectiveOsobaId`
- do inicializátoru `new CurrentUserContextViewModel { … }` přidej `ViewAs = viewAs is null ? null : new ViewAsInfo(viewAs.RealOsobaId, viewAs.RealDisplayName),`
- hned za inicializátor (před `_logger.LogInformation("User context resolved successfully…`) vlož:

```csharp
        if (viewAs is not null)
        {
            _viewAsState.Activate(viewAs.RealOsobaId, context.OsobaId);
        }
```

- do zprávy `User context resolved successfully` přidej `ViewAsRealOsobaId={ViewAsRealOsobaId}` s argumentem `viewAs?.RealOsobaId`

Kontrola: `grep -n "osobaId.Value" PmTracker.Web/Services/Security/UserContextResolver.cs` smí ukázat jen řádek `var realOsobaId = osobaId.Value;`.

- [ ] **Step 4: BaseController — navigace**

V `BaseController.OnActionExecutionAsync` do `new NavPermissionsViewModel { … }` přidej:

```csharp
            CanStartViewAs = CurrentUserContext.IsSuperAdmin && CurrentUserContext.ViewAs is null,
            ViewAs = CurrentUserContext.ViewAs is { } viewAs
                ? new ViewAsBarViewModel(CurrentUserContext.DisplayName, viewAs.RealDisplayName)
                : null,
```

- [ ] **Step 5: Controller**

`PmTracker.Web/Controllers/PohledJakoController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Režim „Zobrazit jako“: superadmin prochází aplikaci s právy jiné osoby, jen pro čtení.
/// Superadmin není permission klíč, proto body-check (allowlist v AuthorizationPolicyEnforcementTests).
/// </summary>
[Route("PohledJako")]
public sealed class PohledJakoController : BaseController
{
    private readonly IViewAsService _viewAsService;
    private readonly IProjectService _projectService;

    public PohledJakoController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IViewAsService viewAsService,
        IProjectService projectService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _viewAsService = viewAsService;
        _projectService = projectService;
    }

    private bool CanStartViewAs => CurrentUserContext.IsSuperAdmin && CurrentUserContext.ViewAs is null;

    [HttpGet("Vyber")]
    public IActionResult Vyber()
    {
        if (!CanStartViewAs)
        {
            return Forbid();
        }

        return View("VyberModal", new ViewAsModalViewModel
        {
            SearchUrl = Url.Action(nameof(HledatOsoby), WithAsUserRoute()) ?? "/PohledJako/HledatOsoby"
        });
    }

    [HttpGet("HledatOsoby")]
    public async Task<IActionResult> HledatOsoby([FromQuery(Name = "q")] string? query, CancellationToken ct)
    {
        if (!CanStartViewAs)
        {
            return Forbid();
        }

        var results = await _projectService.SearchProjectMemberCandidatesAsync(query ?? string.Empty, ct);
        return Json(new PersonPickerSearchResponseViewModel
        {
            Results = results.Where(x => x.Id != CurrentUserContext.OsobaId).ToList()
        });
    }

    [HttpPost("Zahajit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [ViewAsControl]
    public async Task<IActionResult> Zahajit([FromForm] int osobaId, CancellationToken ct)
    {
        if (CurrentUserContext.ViewAs is not null)
        {
            return AjaxErrorResult(ViewAsMessages.AlreadyActive, AjaxErrorCodes.ViewAsInvalidTarget);
        }

        if (!CurrentUserContext.IsSuperAdmin)
        {
            return AjaxForbiddenResult(ViewAsMessages.OnlySuperAdmin);
        }

        var result = await _viewAsService.StartAsync(HttpContext, CurrentUserContext.OsobaId, osobaId, ct);
        return result switch
        {
            ViewAsStartResult.Started => AjaxSuccessResult(
                "page",
                Url.Action("Index", "Dashboard", WithAsUserRoute()) ?? "/"),
            ViewAsStartResult.SelfTarget => TargetError(ViewAsMessages.SelfTarget),
            ViewAsStartResult.TargetNotFound => TargetError(ViewAsMessages.TargetNotFound),
            _ => AjaxForbiddenResult(ViewAsMessages.OnlySuperAdmin)
        };
    }

    [HttpPost("Ukoncit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [ViewAsControl]
    public async Task<IActionResult> Ukoncit(CancellationToken ct)
    {
        var viewAs = CurrentUserContext.ViewAs;
        await _viewAsService.StopAsync(
            HttpContext,
            viewAs?.RealOsobaId ?? CurrentUserContext.OsobaId,
            viewAs is null ? null : CurrentUserContext.OsobaId,
            ct);

        var back = LocalReferer.Resolve(Request);
        return back is not null
            ? LocalRedirect(back)
            : RedirectToAction("Index", "Dashboard", WithAsUserRoute());
    }

    private BadRequestObjectResult TargetError(string message)
        => AjaxErrorResult(
            message,
            AjaxErrorCodes.ViewAsInvalidTarget,
            fieldErrors: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["OsobaId"] = [message]
            });
}
```

Pozn.: `IProjectService` je v namespace `PmTracker.Web.Services` (`Services/IProjectService.cs`). Když kompilátor hlásí jiný namespace, oprav jen `using`.

- [ ] **Step 6: Allowlist v architektonickém testu**

V `AuthorizationPolicyEnforcementTests.Every_Mutating_Action_Should_Have_SpecificPolicy_OrBeExplicitlyAllowlisted` přidej na konec allowlistu:

```csharp
            // ---- PohledJakoController (režim „Zobrazit jako“) ----
            // Superadmin není permission klíč (authz.superadmins), proto body-check v akci.
            // Ukoncit musí projít i během pohledu, kdy kontext patří cílové osobě bez práv —
            // ukončení řídí skutečná identita (cookie + superadmin ověřené v ViewAsService).
            "PmTracker.Web.Controllers.PohledJakoController.Zahajit",
            "PmTracker.Web.Controllers.PohledJakoController.Ukoncit",
```

- [ ] **Step 7: Čtecí POSTy smí projít**

Přidej `[AllowDuringViewAs]` (a `using PmTracker.Web.Services.Security.ViewAs;`) nad:
- `ExportController.Pdf` (jen přesměrování na tisk),
- `HarmonogramController.PreviewSync` (ComputePlan bez zápisu, viz allowlist komentář „žádný DB write“),
- `ExterniOdkazController.Sync` (čte ServiceDesk, výsledek jde do klientského bufferu, nic neperzistuje).

Žádné jiné akce. Pokud některá z nich přece zapíše, zablokuje ji pojistka z Task 3.

- [ ] **Step 8: Modal výběru osoby**

`PmTracker.Web/Views/PohledJako/VyberModal.cshtml`:

```cshtml
@model ViewAsModalViewModel
@{
    Layout = "_ModalLayout";
    ViewData["ModalTitleId"] = "view-as-modal-title";
    ViewData["ModalOverflowVisible"] = true;
}

<h2 id="view-as-modal-title">Zobrazit aplikaci jako jiný uživatel</h2>

<p class="app-view-as-modal__note">
    Aplikace se zobrazí s právy a viditelností vybraného uživatele. V tomto režimu nelze nic ukládat;
    pohled ukončíte tlačítkem v liště pod hlavičkou.
</p>

<form class="form-grid"
      data-view-as-modal
      asp-controller="PohledJako"
      asp-action="Zahajit"
      method="post"
      data-ajax-submit="true">
    <div class="office-picker"
         data-person-picker="single"
         data-person-picker-search-url="@Model.SearchUrl"
         data-person-picker-placeholder="Vyhledejte osobu podle jména, e-mailu nebo loginu..."
         data-person-picker-empty="Nenalezeny žádné odpovídající osoby.">
        <input type="hidden" name="OsobaId" value="" data-person-picker-hidden required />
        <label class="office-search-label">Osoba</label>
        <div class="office-searchbox" data-floating-anchor>
            <input type="search" autocomplete="off" data-person-picker-input required />
            <div class="office-search-panel" data-person-picker-panel data-floating-panel data-floating-kind="person-search" data-floating-match-width="true" hidden>
                <div class="office-search-results" data-person-picker-results role="listbox" aria-label="Výsledky osob"></div>
            </div>
        </div>
    </div>

    @await Html.PartialAsync(
        "_ModalFormActions",
        new ModalFormActionsViewModel
        {
            SubmitLabel = "Zobrazit jako"
        })
</form>
```

- [ ] **Step 9: Lišta a menu v layoutu**

`PmTracker.Web/Views/Shared/_ViewAsBar.cshtml`:

```cshtml
@model PmTracker.Web.Models.ViewModels.ViewAsBarViewModel
@{
    // Zachová ?asUser (dev identita) stejně jako Projekty/Detail.cshtml.
    var asUser = Context.Request.Query["asUser"].ToString();
    var stopUrl = Url.Action("Ukoncit", "PohledJako", new { asUser }) ?? "/PohledJako/Ukoncit";
}
<div class="app-view-as-bar" role="status" data-view-as-bar>
    <gov-icon class="app-view-as-bar__icon" name="eye" type="components" aria-hidden="true"></gov-icon>
    <p class="app-view-as-bar__text">
        Prohlížíte aplikaci jako <strong data-view-as-target>@Model.TargetDisplayName</strong>, jen pro čtení.
        <span class="app-view-as-bar__real">Váš účet: @Model.RealDisplayName</span>
    </p>
    <form class="app-view-as-bar__form" method="post" action="@stopUrl">
        @Html.AntiForgeryToken()
        <gov-button native-type="submit" type="outlined" color="primary" size="s" data-view-as-stop>Zrušit pohled jako</gov-button>
    </form>
</div>
```

`_Layout.cshtml`:
- v menu účtu za `<li>` s „Moje práva“ (před `</ul>`) vlož:

```cshtml
                            @if (nav?.CanStartViewAs == true)
                            {
                                <li role="presentation">
                                    <gov-button type="base" color="primary" size="m" expanded role="menuitem"
                                                data-view-as-start
                                                data-modal-url="@Url.Action("Vyber", "PohledJako")">Zobrazit jako…</gov-button>
                                </li>
                            }
```

- mezi `</header>` a blok `@{ var breadcrumbTrail = … }` vlož:

```cshtml
    @if (nav?.ViewAs is { } viewAsBar)
    {
        <partial name="_ViewAsBar" model="viewAsBar" />
    }
```

- [ ] **Step 10: CSS lišty**

`site.css` — za poslední pravidlo `.app-breadcrumb-*` vlož:

```css
/* Lišta „Zobrazit jako“ (superadmin vidí aplikaci očima jiného uživatele, jen pro čtení).
   Nosný layout jen flex + wrap (Edge na i15); na úzkém displeji tlačítko spadne pod text. */
.app-view-as-bar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.5rem 1rem;
    padding: 0.5rem 1rem;
    background: var(--background-status-warning-subtle);
    border-bottom: 1px solid var(--border-warning);
    color: var(--text-primary);
    font-size: 0.875rem;
}
.app-view-as-bar__icon { flex: 0 0 auto; }
.app-view-as-bar__text { flex: 1 1 16rem; min-width: 0; margin: 0; }
.app-view-as-bar__real { color: var(--text-secondary); margin-left: 0.5rem; }
.app-view-as-bar__form { flex: 0 0 auto; margin: 0 0 0 auto; }
.app-view-as-modal__note { margin: 0 0 1rem; color: var(--text-secondary); }
```

- [ ] **Step 11: Spusť Api testy průběhu + architektonické a CSS testy**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ViewAsFlowTests" --nologo`
Expected: PASS (13 testů).

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~Architecture" --nologo`
Expected: PASS (`AuthorizationPolicyEnforcementTests`, `AppCssColorTokenTests`, `MiddlewareOrderingTests`).

- [ ] **Step 12: Commit**

```bash
git add PmTracker.Web PmTracker.Tests.Api/Controllers/ViewAsFlowTests.cs PmTracker.Tests.Unit/Architecture/AuthorizationPolicyEnforcementTests.cs
git status
git commit -m "feat(pohled-jako): přepnutí identity, menu Zobrazit jako a lišta s ukončením"
```

---

### Task 7: Api testy jen-pro-čtení (blokace, sweep stránek, editor, KeepAlive)

**Files:**
- Test: `PmTracker.Tests.Api/Controllers/ViewAsReadOnlyTests.cs`

**Interfaces:**
- Consumes: celý řetězec z Task 1–6; `ApiSqlFixture.EnsurePersonAsync/EnsureProjectAsync/EnsureSubsystemAsync/EnsureRecordAsync/EnsureProjectRoleAssignmentAsync/CreateDbContext`

- [ ] **Step 1: Napiš testy**

`PmTracker.Tests.Api/Controllers/ViewAsReadOnlyTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ViewAsReadOnlyTests
{
    private readonly ApiSqlFixture _fixture;

    public ViewAsReadOnlyTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int TargetId, int ProjectId, int RecordId, string Cookie);

    private async Task<Scenario> SeedAsync(string marker)
    {
        var targetId = await _fixture.EnsurePersonAsync($"{marker}Cil");
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, targetId, "PROJ_MAN");
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        return new Scenario(targetId, projectId, recordId, $"{_fixture.AdminOsobaId}-{targetId}");
    }

    private HttpClient Client() => _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });

    private static HttpRequestMessage Request(HttpMethod method, string url, string cookie, bool ajax = false, Dictionary<string, string>? form = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"{ViewAsCookie.Name}={cookie}");
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        return request;
    }

    [Fact]
    public async Task AjaxPost_IsBlockedWithReadableMessage_AndNothingIsWritten()
    {
        var s = await SeedAsync("ApiVaRo1");
        using var client = Client();
        const string marker = "APIVARO1NEW";

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/Projekty/SaveProject?asUser={_fixture.AdminOsobaId}", s.Cookie,
            ajax: true, form: new() { ["Zkratka"] = marker, ["CelyNazev"] = marker }));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("VIEW_AS_READ_ONLY");
        await using var db = _fixture.CreateDbContext();
        (await db.Projekty.AnyAsync(x => x.Zkratka == marker)).Should().BeFalse();
    }

    [Fact]
    public async Task FormPost_RedirectsBackWithTempData()
    {
        var s = await SeedAsync("ApiVaRo2");
        using var client = Client();
        var request = Request(HttpMethod.Post, $"/Jednani/SaveStatus?asUser={_fixture.AdminOsobaId}", s.Cookie, form: new());
        request.Headers.Referrer = new Uri($"http://localhost/Jednani?asUser={_fixture.AdminOsobaId}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.OriginalString.Should().Be($"/Jednani?asUser={_fixture.AdminOsobaId}");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith(".AspNetCore.Mvc.CookieTempDataProvider=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReleaseEditLockBeacon_IsBlocked_AndForeignLockSurvives()
    {
        var s = await SeedAsync("ApiVaRo3");
        await using (var db = _fixture.CreateDbContext())
        {
            db.ZaznamEditZamky.Add(new ZaznamEditZamekEntity
            {
                ZaznamId = s.RecordId, OsobaId = s.TargetId, ZiskanoAt = DateTime.UtcNow, HeartbeatAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var client = Client();
        var request = Request(HttpMethod.Post, $"/Zaznamy/ReleaseEditLock?asUser={_fixture.AdminOsobaId}", s.Cookie);
        request.Content = new StringContent($"{{\"zaznamId\":{s.RecordId}}}", System.Text.Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var check = _fixture.CreateDbContext();
        (await check.ZaznamEditZamky.AnyAsync(x => x.ZaznamId == s.RecordId && x.OsobaId == s.TargetId))
            .Should().BeTrue("admin nesmí cílovému uživateli uvolnit jeho zámek");
    }

    [Fact]
    public async Task MainPages_RenderForTarget_WithoutHittingWriteGuard()
    {
        var s = await SeedAsync("ApiVaRo4");
        using var client = Client();
        var admin = _fixture.AdminOsobaId;
        string[] urls =
        [
            $"/Dashboard?asUser={admin}",
            $"/Projekty?asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=zaznamy&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=harmonogram&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=jednani&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=tym&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=navrhy&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=vyzvy&asUser={admin}",
            $"/Zaznamy/Detail/{s.RecordId}?asUser={admin}",
            $"/Zaznamy/Edit?id={s.RecordId}&asUser={admin}",
            $"/Jednani?asUser={admin}",
            $"/Profil?asUser={admin}",
            $"/Search?q=ApiVaRo4&asUser={admin}"
        ];

        foreach (var url in urls)
        {
            var response = await client.SendAsync(Request(HttpMethod.Get, url, s.Cookie));
            var body = await response.Content.ReadAsStringAsync();

            body.Should().NotContain("VIEW_AS_READ_ONLY", $"{url} nesmí při čtení zapisovat do DB");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body[..Math.Min(body.Length, 400)]}");
        }
    }

    [Fact]
    public async Task Editor_InViewAs_OpensWithoutCreatingLock()
    {
        var s = await SeedAsync("ApiVaRo5");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}", s.Cookie));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-editor-form=\"true\"");
        await using var db = _fixture.CreateDbContext();
        (await db.ZaznamEditZamky.AnyAsync(x => x.ZaznamId == s.RecordId)).Should().BeFalse();
    }

    [Fact]
    public async Task Editor_InViewAs_ForeignLock_ShowsLockedPage_AndLockUntouched()
    {
        var s = await SeedAsync("ApiVaRo6");
        var holder = await _fixture.EnsurePersonAsync("ApiVaRo6Holder");
        var since = DateTime.UtcNow.AddMinutes(-2);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ZaznamEditZamky.Add(new ZaznamEditZamekEntity { ZaznamId = s.RecordId, OsobaId = holder, ZiskanoAt = since, HeartbeatAt = since });
            await db.SaveChangesAsync();
        }
        using var client = Client();

        var html = await (await client.SendAsync(Request(HttpMethod.Get, $"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}", s.Cookie)))
            .Content.ReadAsStringAsync();

        html.Should().Contain("data-record-edit-locked=\"true\"", "admin vidí totéž co cílový uživatel");
        await using var check = _fixture.CreateDbContext();
        var zamek = await check.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == s.RecordId);
        zamek.OsobaId.Should().Be(holder);
        zamek.HeartbeatAt.Should().BeCloseTo(since, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task KeepAlive_InViewAs_DoesNotExtendTargetLock()
    {
        var s = await SeedAsync("ApiVaRo7");
        var since = DateTime.UtcNow.AddMinutes(-3);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ZaznamEditZamky.Add(new ZaznamEditZamekEntity { ZaznamId = s.RecordId, OsobaId = s.TargetId, ZiskanoAt = since, HeartbeatAt = since });
            await db.SaveChangesAsync();
        }
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/App/KeepAlive?zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}", s.Cookie, ajax: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var check = _fixture.CreateDbContext();
        (await check.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == s.RecordId))
            .HeartbeatAt.Should().BeCloseTo(since, TimeSpan.FromSeconds(1));
    }
}
```

Pozn.: route detailu záznamu ověř: `ZaznamyController.Detail(int id, …)` → `/Zaznamy/Detail/{id}`. Pokud existující Api testy používají `?id=`, uprav URL ve sweepu podle nich (`grep -rn "Zaznamy/Detail" PmTracker.Tests.Api`).

- [ ] **Step 2: Spusť**

Run: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ViewAsReadOnlyTests" --nologo`
Expected: PASS (7 testů). Jak číst selhání:
- sweep hlásí `VIEW_AS_READ_ONLY` → stránka zapisuje při GET. Najdi SQL v logu (`Pohled jako: zablokován zápis`), zápis dej explicitně do podmínky `CurrentUserContext.ViewAs is null` (vzor Task 5) a přidej regresní řádek do sweepu,
- sweep hlásí 403/404 bez `VIEW_AS` → cílový uživatel stránku nesmí vidět, oprav seed (role), ne aplikaci.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.Api/Controllers/ViewAsReadOnlyTests.cs
git commit -m "test(pohled-jako): blokace zápisu, sweep stránek, zámek karty a keep-alive"
```

---

### Task 8: E2E scénář v prohlížeči

**Files:**
- Test: `PmTracker.Tests.E2E/Scenarios/ViewAsScenariosTests.cs`

**Interfaces:**
- Consumes: DOM kotvy `data-view-as-start`, `.modal-overlay`, `[data-person-picker-input]`, `[data-modal-floating-root] .office-search-item`, `pm-button[native-type='submit']`, `data-view-as-bar`, `data-view-as-target`, `data-view-as-stop`; `E2ETestFixture.BaseUrl/AdminOsobaId/Database.ConnectionString/NewPageAsync()`

- [ ] **Step 1: Napiš scénář**

`PmTracker.Tests.E2E/Scenarios/ViewAsScenariosTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Pohled jako: menu účtu → modal → výběr osoby → lišta pod hlavičkou → Zrušit pohled jako.
/// gov komponenty: host gov-buttonu je pro Playwright „not visible“, klik přes DispatchEvent
/// (položka menu) nebo vnitřní &lt;button&gt; (submit ve formuláři).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ViewAsScenariosTests
{
    private const string TargetJmeno = "E2EPohled";
    private const string TargetPrijmeni = "Cil";
    private readonly E2ETestFixture _fixture;

    public ViewAsScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private async Task EnsureTargetAsync()
    {
        await using var connection = new SqlConnection(_fixture.Database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM dbo.osoby WHERE email = N'e2e.pohled.cil@pmtracker.test')
                INSERT INTO dbo.osoby (jmeno, prijmeni, email, organizace_id)
                SELECT @jmeno, @prijmeni, N'e2e.pohled.cil@pmtracker.test', (SELECT TOP (1) id FROM dbo.ciselnik_organizace ORDER BY id);
            """;
        command.Parameters.AddWithValue("@jmeno", TargetJmeno);
        command.Parameters.AddWithValue("@prijmeni", TargetPrijmeni);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Superadmin_ZobraziJakoJinyUzivatel_AZrusiPohled()
    {
        await EnsureTargetAsync();
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1400, 900);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(0);

        await page.Locator("[data-view-as-start]").DispatchEventAsync("click");
        var modal = page.Locator(".modal-overlay");
        await Assertions.Expect(modal.Locator("[data-view-as-modal]")).ToHaveCountAsync(1);

        await modal.Locator("[data-person-picker-input]").FillAsync(TargetJmeno);
        var option = modal.Locator("[data-modal-floating-root] .office-search-item:not(.disabled)").First;
        await Assertions.Expect(option).ToBeVisibleAsync();
        await option.ClickAsync();
        await modal.Locator("form[data-view-as-modal]").EvaluateAsync("form => form.requestSubmit()");

        await page.WaitForURLAsync(url => url.Contains("/Dashboard", StringComparison.OrdinalIgnoreCase));
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(1);
        (await page.Locator("[data-view-as-target]").InnerTextAsync()).Should().Contain(TargetPrijmeni);

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("[data-view-as-start]")).ToHaveCountAsync(0);

        await page.Locator("[data-view-as-stop] button").ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/Projekty", StringComparison.OrdinalIgnoreCase));
        await Assertions.Expect(page.Locator("[data-view-as-bar]")).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("[data-view-as-start]")).ToHaveCountAsync(1);

        await page.Context.CloseAsync();
    }
}
```

Pozn.: `dbo.ciselnik_organizace` je tabulka `CiselnikOrganizaceEntity` (`LookupEntityConfiguration.cs:136`). Pokud `[data-view-as-stop] button` nenajde vnitřní tlačítko (jiná verze gov-buttonu), nahraď poslední klik `page.Locator("form.app-view-as-bar__form").EvaluateAsync("f => f.requestSubmit()")` a do poznámky v testu napiš proč.

- [ ] **Step 2: Spusť E2E scénář**

Run: `dotnet test PmTracker.Tests.E2E --filter "FullyQualifiedName~ViewAsScenariosTests" --nologo`
Expected: PASS.

- [ ] **Step 3: Celá E2E sada proti baseline**

Run: `dotnet test PmTracker.Tests.E2E --nologo --logger "trx;LogFileName=e2e-viewas.trx"` a porovnej jména selhání proti baseline worktree (Python normalizace jmen, viz paměť `project_e2e_baseline_failures`).
Expected: žádné nové jméno mimo 33 pre-existing.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Tests.E2E/Scenarios/ViewAsScenariosTests.cs
git commit -m "test(pohled-jako): E2E scénář zahájení a zrušení pohledu"
```

---

### Task 9: Dokumentace, plné sady testů, ruční průchod v prohlížeči

**Files:**
- Modify: `docs/authorization.md` (nová sekce před `## FAQ`)

- [ ] **Step 1: Dokumentace**

Do `docs/authorization.md` před `## FAQ` vlož:

```markdown
## Pohled jako (superadmin)

Superadmin (`authz.superadmins`) může v menu účtu zvolit **Zobrazit jako…** a procházet aplikaci
s právy a viditelností jiné osoby. Pod hlavičkou je lišta s tlačítkem **Zrušit pohled jako**.

- Identita přihlášeného (Windows Auth) se nemění. `UserContextResolver` po nalezení skutečné osoby
  vyhodnotí cookie `pmtracker.viewas` (`{skutečný}-{cíl}`) přes `IViewAsService` a sestaví kontext
  pro cílovou osobu. `CurrentUserContextViewModel.ViewAs` nese skutečnou osobu.
- Cookie platí jen pro superadmina, jehož `osoba_id` je v první části hodnoty. Jinak se ignoruje a smaže.
  Je to session cookie (končí zavřením prohlížeče).
- **Jen pro čtení**, ve třech vrstvách: `ViewAsReadOnlyMiddleware` odmítne ne-GET (výjimky:
  `[AllowDuringViewAs]` pro čtecí POSTy, `[ViewAsControl]` pro zahájení/ukončení). Editor a keep-alive
  zámek karty jen čtou (`IRecordEditLockService.PeekAsync`). `ViewAsWriteGuardInterceptor` zablokuje
  jakýkoli zapisující SQL z requestu v pohledu.
- Audit: `authz.audit_log`, `entity_type = 'pohled_jako'`, akce `view_as_start` / `view_as_stop`,
  `actor_osoba_id` = skutečný superadmin.
- Nová POST akce, která jen čte a má v pohledu fungovat, dostane `[AllowDuringViewAs]`. Zápis jí
  pojistka stejně nedovolí.
```

- [ ] **Step 2: Plné sady**

Run: `dotnet build --nologo && dotnet test PmTracker.Tests.Unit --nologo && dotnet test PmTracker.Tests.Integration --nologo && dotnet test PmTracker.Tests.Api --nologo`
Expected: vše PASS (Unit a Api od 2026-10-08 bez pre-existing selhání, nové selhání = regrese).

- [ ] **Step 3: Ruční průchod v reálném prohlížeči (Playwright skript proti lokální app)**

Spusť app lokálně (dev DB, env connection string, Colima SQL). Playwright z bundled driveru E2E projde a uloží snímky do scratchpadu:
1. `/Projekty?asUser=<admin>`: otevřít menu účtu (snímek s položkou „Zobrazit jako…“), modal, vybrat osobu s rolí v projektu.
2. Po přesměrování: snímek lišty ve **světlém i tmavém** motivu a na šířce **375 px** (tlačítko pod textem, žádný horizontální scroll).
3. Projít Přehled, Projekty, detail projektu (všechny záložky), záznam (detail + editor), Jednání, Profil (Moje práva = role cílové osoby), Hledání.
4. V editoru zkusit Uložit: modal ukáže hlášku `ViewAsMessages.ReadOnly`, žádné „Relace vypršela“ ani „Obnovit stránku“.
5. Zrušit pohled jako: návrat na stejnou stránku, bez lišty.
6. Zkontrolovat log aplikace: `grep -n "Pohled jako: zablokován zápis" <log>` je **prázdné**. Každý nález = zápis při GET, který se musí vyřešit jako v Task 7 Step 2.

- [ ] **Step 4: Commit + merge do main**

```bash
git add docs/authorization.md
git commit -m "docs(pohled-jako): popis režimu Zobrazit jako v autorizační dokumentaci"
git status
```

Práce jde do lokálního `main`, bez pushe. Uživateli předat seznam pro ruční ověření na i15 (Edge): lišta, menu, modal, tmavý motiv, Zrušit.
