# Jak se kód ze Zápisky přebírá do aplikace s React frontendem

Odpověď na otázku vývojáře: *„Zápiska nemá React a React komunikuje přes rozhraní —
jak se tedy dá její kód použít?"*

**Krátce: naprostá většina kódu Zápisky o Razoru vůbec neví.**

---

## Námitka: „Zápiska je MVC, Číselníky budou kvůli Reactu jinak"

Tahle námitka **míchá dvě různé věci** a stojí za to je rozplést, protože se vrací.

### MVC v ASP.NET Core není „server vykresluje HTML"

MVC je v ASP.NET Core **způsob zpracování požadavku**: směrování, navázání dat z požadavku,
filtry, vkládání závislostí, autorizace. Rozhraní vracející JSON je **součástí téhož
zásobníku** — od ASP.NET Core 1.0 jsou Web API a MVC jeden celek.

Třída označená `[ApiController]` dědí z `ControllerBase`. Třída vracející Razor pohled
dědí z `Controller`, což je **potomek téhož `ControllerBase`**. Je to jedna hierarchie,
jedno směrování, jedny filtry.

> **Číselníky budou také MVC.** Jen nepoužijí to „V".

### Zápiska sama dělá obojí — změřeno

Není to teorie. V repozitáři Zápisky:

| Zjištění | Hodnota |
|---|---|
| Míst, která vracejí JSON (`Json(...)` / `Ok(...)`) | **24** |
| Controllerů, které to dělají | **9** |
| Vyhrazená část základního controlleru pro JSON | `Controllers/BaseController.Ajax.cs` |
| Testovací sada pro JSON vrstvu | `PmTracker.Tests.Api/Controllers/AjaxControllersTests.cs` |

`BaseController.Ajax.cs` není improvizace — je to celá část třídy s **chybovými kódy
a chybami po jednotlivých polích**:

```csharp
protected BadRequestObjectResult AjaxInvalidModelResult(string? message = null)
{
    var errorCode = AjaxErrorCodes.RequestValidationFailed;
    var fieldErrors = BuildModelStateFieldErrors();
    …
}
```

**Zápiska tedy už dnes provozuje obě podoby vedle sebe, ve stejné aplikaci, nad stejnými
službami.** Číselníky nedělají nic nového — jen zahazují tu polovinu, která vrací HTML.

### Co má námitka pravdu

Tři věci, a jsou to přesně ty, které měření řadí do zbylých 30 %:

| Co | Proč se to nepřenese |
|---|---|
| **Razor pohledy** | Nahradí je React. Zahazují se celé. |
| **View modely tvarované pro vykreslení** | Nesou hotové řetězce k zobrazení a příznaky typu „ukázat tohle tlačítko". Tvar pro JSON je jiný a **nekopíruje se 1:1**. |
| **Orchestrace pohledu v controlleru** | `TempData`, přesměrování, převod chyb do pohledu. Nahradí je stavové kódy a `ProblemDetails`. |

### Čemu ale nemá pravdu

**MVC není architektura Zápisky.** Je to vzor zpracování požadavku, který používá webový
rámec. Architektura Zápisky je **vrstvená**: controller → služby → data. A právě tu vrstvenost
přebíráme — je na volbě prezentační vrstvy nezávislá.

Kdyby námitka platila, znamenalo by to, že přechodem na React přijdeme o služby, autorizaci,
Active Directory, audit a datovou vrstvu. **Měření říká opak: 70 % kódu o prezentační
vrstvě vůbec neví.**

---

## Kolik kódu je vázané na Razor — změřeno

Měření nad repozitářem Zápisky:

| Vrstva | Řádků | Ví o Razoru? | Osud |
|---|---:|---|---|
| `Services/` | 32 428 | **ne** (1 soubor z 278) | **beze změny** |
| `Models/` | 4 730 | ne | **beze změny** |
| `Data/` | 1 062 | ne | beze změny (přepis dotazů na SQL Server) |
| `Middleware/` | 117 | ne | **beze změny** |
| `Filters/` | 96 | ne | beze změny |
| `Extensions/` | 25 | ne | **beze změny** |
| `Controllers/` | 6 335 | ano — vrací `View()` | **přepis na tenké, vracející JSON** |
| `TagHelpers/` | 1 340 | ano | **port na React komponenty** |
| `Views/` | 8 954 | ano | **zahodit, nahradit Reactem** |

**Zhruba 70 % kódu je vůči prezentační vrstvě netečné.** Jediný soubor ve `Services/`,
který sahá na MVC, je `Export/RazorViewRenderer.cs` — a ten je Razorem z definice
(viz níže).

Ověřitelné příkazem:

```bash
grep -rl "using Microsoft.AspNetCore.Mvc" PmTracker.Web/Services --include='*.cs'
# → jediný nález: Services/Export/RazorViewRenderer.cs
```

---

## Kde vede šev

Zápiska je vrstvená a Razor sedí **až úplně nahoře**. Výměna prezentační vrstvy se ho
dotkne, ale nic pod ním neví, že se něco stalo.

```
       ZÁPISKA                             ČÍSELNÍKY
  ┌──────────────────┐               ┌──────────────────┐
  │  Razor Views     │  ← zahodit →  │  React SPA       │
  ├──────────────────┤               ├──────────────────┤
  │  TagHelpery pm-* │  ← port    →  │  Komponenty pm-* │
  ├──────────────────┤               ├──────────────────┤
  │  Controllery     │  ← přepis  →  │  Controllery     │
  │  vrací View()    │               │  vrací JSON      │
  ├══════════════════┤               ├══════════════════┤
  │  Služby          │               │  Služby          │
  │  Autorizace      │  BEZE ZMĚNY   │  Autorizace      │
  │  AD, audit       │      ═══▶     │  AD, audit       │
  │  Middleware      │               │  Middleware      │
  │  Datová vrstva   │               │  Datová vrstva   │
  └──────────────────┘               └──────────────────┘
```

### Šev je v controlleru doslova jeden řádek

```csharp
// Zápiska
public async Task<IActionResult> Detail(int id, CancellationToken ct)
{
    var model = await _sluzba.NactiAsync(id, ct);
    return View(model);              // ← Razor vyrenderuje HTML
}

// Číselníky
public async Task<IActionResult> Detail(int id, CancellationToken ct)
{
    var model = await _sluzba.NactiAsync(id, ct);
    return Ok(model);                // ← totéž jako JSON
}
```

**Volání služby je totožné.** Mění se poslední řádek.

### Autorizace se nemění vůbec

```csharp
[Authorize(Policy = "opravneni:hodnoty.edit")]
public async Task<IActionResult> Uloz(string kod, ...)
```

Atribut se vyhodnotí **v průběhu zpracování požadavku, dřív než se akce spustí** —
a je mu jedno, jestli akce nakonec vrátí HTML, nebo JSON. Identitu naplní middleware,
oprávnění vyhodnotí handler nad projekcí práv. Ani jeden z nich o prezentační vrstvě neví.

---

## Co se mění při přechodu na React — vyčerpávající seznam

Tři věci. Ne víc.

| Co | V Zápisce | V Číselnících |
|---|---|---|
| **Prvky závislé na právech** | `@if (mám právo)` vyhodnocené serverem při renderu | `GET /internal/ja` vrátí práva, React podle nich skryje prvky |
| **Ochrana proti podvržení požadavku** | tag helper vložil token do formuláře sám | React si token vyžádá a posílá výslovně |
| **Odmítnutí bez práva** | přesměrování na přihlašovací stránku | `401`/`403` jako JSON, žádné přesměrování |

Třetí bod už Zápiska sama řeší pro svoje volání z JavaScriptu —
`Filters/AjaxAntiforgeryResultFilter.cs` je hotová předloha.

---

## Modul po modulu

| Modul | Jak se přebírá |
|---|---|
| **Active Directory** | **Beze změny.** `ADConnector` stojí na `System.DirectoryServices` a `System.Security.Principal` — čisté Windows API bez vazby na web. |
| **Přihlášení a identita** | **Beze změny.** Middleware nad `HttpContext`. V Číselnících ještě zjednodušené — neznámý uživatel se neodmítá. |
| **Autorizace (RBAC)** | **Beze změny v principu.** Projekce práv, policy handler, seed. Mění se doména klíčů a vypadá jeden pojem rozsahu. |
| **Auditní log** | **Beze změny.** Zápis do tabulky, žádná vazba na UI. |
| **Wiki a dokumentace** | Služba renderující markdown **beze změny**; obrazovka se přepíše do Reactu. |
| **Komponenty `pm-*`** | **Port.** Zápiskové TagHelpery jsou předloha mapování na gov komponenty — jaké atributy, jaké varianty. Přepisuje se obal, ne rozhodnutí. |
| **Testovací infrastruktura** | **Beze změny.** Továrna aplikace, podvržená identita, fixture. Testuje se HTTP vrstva, ne renderované HTML — proto to sedí dokonce líp. |

### Testy dokonce vyjdou lépe

Zápiska musí v testech obcházet to, že **šablonovací vrstva kóduje diakritiku na HTML
entity**, takže se nelze kotvit na český text a testy se drží atributů a ASCII.

U rozhraní vracejícího JSON tenhle problém **zaniká** — porovnává se hodnota, ne
vyrenderovaný dokument.

---

## Jediná věc, která se opravdu přebrat nedá

**Sazba PDF.** Zápiska ho dělá takto:

```
Razor šablona → HTML → Chromium (PuppeteerSharp) → PDF
     ↑                        ↑
 nepřenosné              přenosné beze změny
```

`ChromiumPdfRenderer` se přebírá **beze změny** — bere HTML a vrací PDF, o původu HTML
nic neví. Nepřenosný je jen `RazorViewRenderer`, tedy krok, který HTML vyrábí.

**Doporučení:** ponechat Razor **výhradně pro tiskové šablony** a vynutit to
architektonickým testem, který nepustí `.cshtml` mimo `Views/Tisk/`.

Důvod je konkrétní: **šablonovací vrstva escapuje HTML sama.** Ruční skládání HTML
z uživatelských dat by znamenalo hlídat escapování na každém místě, a jedno opomenutí
je bezpečnostní chyba. Tisková sestava je navíc neinteraktivní dokument — přesně to,
k čemu se šablony hodí a kvůli čemu se u interaktivních obrazovek neosvědčily.

Rozhoduje se v **P9**.
