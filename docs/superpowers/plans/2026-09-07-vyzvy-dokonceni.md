# Výzvy — dokončení tématu: implementační plán

> **Pro agentní pracovníky:** POVINNÝ SUB-SKILL: použij `superpowers:executing-plans` a implementuj plán blok po bloku. Kroky používají checkbox (`- [ ]`) syntaxi.

**Goal:** Přesunout Výzvy z dashboardu do projektového menu, postavit rail s výběrem roku v gov prvcích, nahradit automatické číslování ručním, umožnit přesuny PNF bez skoku obrazovky a povýšit aplikaci na verzi 0.9.

**Architecture:** Plán je rozdělený na **funkční bloky, ne na vrstvy**. Každý blok je svislý řez celou aplikací (služba → view model → view → JS → testy) a končí stavem, který jde spustit a ručně vyzkoušet. Aplikace je funkční po každém bloku, ne až na konci.

**Tech Stack:** ASP.NET Core MVC (net8.0), EF Core 8, Razor, xUnit + FluentAssertions + Moq, EF InMemory pro unit testy, ESM moduly ve `wwwroot/js/modules/`, gov-design-system 4.x.

**Spec:** [docs/superpowers/specs/2026-09-07-vyzvy-dokonceni-design.md](../specs/2026-09-07-vyzvy-dokonceni-design.md)

## Global Constraints

- **Commity jsou DRŽENÉ.** Nic necommituj — uživatel commituje sám po ruční verifikaci. Žádný krok tohoto plánu neobsahuje `git commit`.
- `appsettings.json` a `appsettings.Development.json` jsou gitignorovaná tajemství — nikdy needituj ani nevytvářej.
- Build musí skončit s **0 chybami a 0 varováními**.
- Rozsah čísla výzvy: **1–999**.
- Role s právem měnit: `VLASTNIK_PROJEKTU`, `GEST`, `PROJ_MAN`, `ADM_PROJ`; globální `SUPERADMIN` a `APP_ADMIN` si práva ponechávají.
- Zobrazení záložky **není** řízeno žádným oprávněním.
- **Po každém bloku spusť plnou sadu Unit testů** a nahlas výsledek. Blok není hotový, dokud sada neběží.

## Konvence gov prvků

Struktura gov design systému není pro rail s dlaždicemi ideální, proto **nosný layout je vlastní CSS grid**, ale **všechny ovládací a obsahové prvky uvnitř jsou gov komponenty**:

| Potřeba | Prvek | Poznámka |
|---|---|---|
| Tlačítka | `<pm-button variant="Primary\|Secondary\|Ghost">` | TagHelper, renderuje `gov-button`; dokumentace `docs/architecture/buttons.md` |
| Dlaždice v railu | vlastní `<button>` + `<gov-tag>` | `gov-tile` je odkaz s `href` — klik by navigoval místo přepnutí panelu; `gov-card` nemá klikací sémantiku. Dlaždice je proto tlačítko (klávesnice) se stavovým `gov-tag` uvnitř. |
| Stav výzvy | `<gov-tag>` | Barva podle stavu |
| Výběr roku | `<gov-form-select>` | |
| Číslo výzvy v modalu | `<gov-form-input>` v `<gov-form-group>` | |
| Modal | `<gov-dialog>` | **Nikdy `block-close`** — atribut disabluje křížek |
| Prázdné stavy | `<p class="muted">` | `gov-empty` v distribuci existuje, ale v aplikaci ho nikdo nepoužívá a není ověřené, zda nevyžaduje pojmenovaný slot — tiché zmizení hlášky by bylo horší než nekonzistence. |
| Chybové hlášky | `<gov-message>` | |
| Ikony | `<gov-icon type="components">` | Bootstrap Icons, ne gov-shipped |

**Známé pasti gov komponent — dodrž, jinak se chyby zopakují:**

- `instanceof HTMLButtonElement` **nematchuje** `gov-button`. Používej helpery `isButtonLike` / `setButtonDisabled` z `modules/utils.js`.
- Nastavení `.textContent` na hostu `gov-button` rozbije Stencil slot relocation a **zdvojí popisek**. Měň viditelnost vnořených `<span>` přes `hidden`.
- CSS `:checked` **nematchuje** custom elementy — pro `gov-form-switch` piš `[checked]`.
- Atributy `aria-*` a `hidden` na hostu `pm-button` se neuplatní. Stav promítej třídou na obyčejném předkovi.
- Zavírání plovoucích vrstev vyhodnocuj na `mousedown`, ne na `click` — tažení myší ven jinak retargetuje `click` na společného předka a vrstvu falešně zavře.
- Nosný layout drží letité flex/grid základy; `:has` a podobné novinky jen jako nepovinné vylepšení. Cílový prohlížeč je Edge na i15.

## Přehled bloků

| Blok | Co bude po něm fungovat |
|---|---|
| 1 | Výzvy jsou záložka projektového menu, vidí je každý člen projektu |
| 2 | Rail 20/80 s dlaždicemi a výběrem roku v gov prvcích |
| 3 | PNF v pravém panelu seskupené podle projektového záznamu |
| 4 | Zakládání výzvy s ručně zadaným číslem přes modal |
| 5 | Switch u PNF končí vždy v bufferu |
| 6 | Přesuny PNF tažením i kontextovým menu, bez skoku obrazovky |
| 7 | Tlačítko Tisk výzvy (zatím bez funkce) |
| 8 | Verze 0.9 a závěrečné ověření |

## Přenášené dluhy z rozpracovaných bloků

Zapisováno průběžně při exekuci, aby se nic neztratilo při zkrácení kontextu.
Každá položka je přiřazená k bloku, který ji má uzavřít.

| # | Dluh | Vznikl v | Uzavře blok | Stav |
|---|---|---|---|---|
| D1 | `handleZalozit` v `panelController.js` posílá jen `ProjektId` a akce `otevrit-novou` je dočasně napojená na staré auto-číslování. Nahradit modalem s ručně zadaným číslem. | 2 | **4** | ✅ uzavřeno v bloku 4 |
| D2 | `VyzvyController.ReassignModal` drží `_timeProvider.GetLocalNow().Year`, aby prošel překlad. Je to mrtvý kód (`reassignModal.js` není v `bootstrap.js`) — smazat celý. | 2 | **6** | ✅ uzavřeno v bloku 6 |
| D3 | **Regrese:** měnič stavu výzvy zmizel se smazanou `_VyzvyPanel.VyzvaCard.cshtml`. JS `handleZmenitStav` a `[data-vyzvy-stav-toggle]` žijí, ale chybí markup — stav výzvy nejde změnit. Plán pro `_VyzvyPane.cshtml` ho neobsahoval. | 2 | **3** | ✅ uzavřeno v bloku 3 |
| D4 | Panel výzvy nezobrazuje stav ani datum odeslání, ačkoli spec §6.4 je v hlavičce vyžaduje. | 2 | **3** | ✅ uzavřeno v bloku 3 |
| D5 | `ZalozitVyzvuAsync` odvozuje rok výzvy z UTC (`rok = now.Year`). V CZ (UTC+1/+2) by 1. ledna po půlnoci vznikla výzva ještě s loňským rokem. | 4 | **před 8** | ✅ controller předává `GetLocalNow()` jako všude jinde; zrušeno i duplicitní pole `TimeProvider` |
| D6 | `VyzvyControllerTests` netestují controller — kopírují si `switch` výraz do testu a asertují nad vlastní kopií. | 4 | **před 8** | ✅ přepsáno na volání reálné akce se záznamovou službou a pevnými hodinami; 7 testů |
| D7 | `vyzvy/pnfMenu.js` a `recordActionsMenu.js` měly shodnou kostru (otevřít/zavřít, mousedown-origin guard, Escape). | 6 | **před 8** | ✅ jeden `ui/anchoredMenu.js` s třídou `AnchoredMenu`; obě menu jsou konfigurace, oba staré soubory smazané |

---

## Blok 1: Výzvy se stěhují do projektového menu

**Cíl bloku:** Záložka Výzvy je v projektovém menu mezi Návrhy a Dashboard, otevře ji každý, kdo vidí projekt. Obsah panelu zůstává zatím starý — mění se jen umístění a přístup.

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`
- Create: `PmTracker.Web/Views/Projekty/_ProjectVyzvyTab.cshtml`
- Delete: `PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs`
- Modify: `PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs:123-165`
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModels.cs:11-18`
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs`
- Modify: `PmTracker.Web/Views/Projekty/Detail.cshtml`
- Modify: `PmTracker.Web/Controllers/ProjectDashboardController.cs:129-145`
- Modify: `PmTracker.Web/Views/ProjectDashboard/Index.cshtml`
- Modify: `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`
- Test: `PmTracker.Tests.Unit/Authorization/ProjectRolePermissionMatrixTests.cs`
- Test: `PmTracker.Tests.Unit/Authorization/PerActionKeyCoverageTests.cs`
- Test: `PmTracker.Tests.Unit/Authorization/ResolverSwapSafetyTests.cs`

**Interfaces:**
- Produces: `VyzvyPanelViewModel` (přejmenováno z `ProjectDashboardVyzvyPanelViewModel`) v namespace `PmTracker.Web.Models.ViewModels.Vyzvy`; `PmTracker.Web.Services.Vyzvy.VyzvyPanelBuilder` s `Task<VyzvyPanelViewModel> BuildAsync(int projektId, bool muzeEditovat, CancellationToken ct)`; akce `ProjektyController.VyzvyTabPartial(int id, CancellationToken ct)` na URL `/Projekty/VyzvyTabPartial/{id}`.

- [ ] **Krok 1: Napiš padající test na přítomnost záložky**

Vytvoř nový soubor `PmTracker.Tests.Api/Controllers/VyzvyTabPlacementTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Výzvy se 2026-09-07 stěhují z dashboardu do projektového menu. Hlídá umístění
/// záložky i to, že ji vidí každý, kdo vidí projekt (bez permission gate).
/// </summary>
public sealed class VyzvyTabPlacementTests : IClassFixture<PmTrackerApiFactory>
{
    private readonly PmTrackerApiFactory _factory;

    public VyzvyTabPlacementTests(PmTrackerApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ProjectDetail_RendersVyzvyTab_BetweenNavrhyAndDashboard()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/Projekty/Detail/1?tab=zaznamy");

        html.Should().Contain("data-tab=\"vyzvy\"", "záložka Výzvy patří do projektového menu");

        var navrhy = html.IndexOf("data-tab=\"navrhy\"", StringComparison.Ordinal);
        var vyzvy = html.IndexOf("data-tab=\"vyzvy\"", StringComparison.Ordinal);
        var dashboard = html.IndexOf("ProjectDashboard", StringComparison.Ordinal);

        vyzvy.Should().BeGreaterThan(navrhy, "Výzvy jsou až za Návrhy");
        vyzvy.Should().BeLessThan(dashboard, "Výzvy jsou před Dashboardem");
    }

    [Fact]
    public async Task VyzvyTabPartial_ReturnsPanel()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/Projekty/VyzvyTabPartial/1");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("data-vyzvy-panel");
    }

    [Fact]
    public async Task Dashboard_NoLongerOffersVyzvyTab()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/ProjectDashboard/Index?projektId=1");

        html.Should().NotContain("data-dashboard-tab=\"vyzvy\"",
            "Výzvy se z dashboardu odstěhovaly do projektového menu");
    }
}
```

**Před spuštěním ověř tvar fixture:** otevři jiný soubor v `PmTracker.Tests.Api/Controllers/` a zkontroluj skutečný název factory třídy a způsob autentizace testovacího klienta. Pokud se liší od `PmTrackerApiFactory`, uprav podle něj — jméno výše je odhad podle konvence, ne ověřený fakt.

- [ ] **Krok 2: Spusť test a ověř, že padá**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTabPlacementTests"`
Očekávej: FAIL — `data-tab="vyzvy"` v HTML není a `/Projekty/VyzvyTabPartial/1` vrací 404.

- [ ] **Krok 3: Přestěhuj view modely panelu**

Vytvoř `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs` a přesuň do něj třídy `ProjectDashboardVyzvyPanelViewModel`, `VyzvyPanelBufferViewModel`, `VyzvyPanelVyzvaViewModel`, `VyzvyPanelPolozkaViewModel` z `PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs:123-165`. V novém souboru:

- namespace `PmTracker.Web.Models.ViewModels.Vyzvy`
- `ProjectDashboardVyzvyPanelViewModel` přejmenuj na `VyzvyPanelViewModel`

V `ProjectDashboardViewModels.cs` původní třídy smaž.

- [ ] **Krok 4: Přestěhuj builder**

Přesuň `PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs` do `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`: změň namespace na `PmTracker.Web.Services.Vyzvy`, doplň `using PmTracker.Web.Models.ViewModels.Vyzvy;`, návratový typ `BuildAsync` změň na `VyzvyPanelViewModel`. Původní soubor smaž.

Oprav DI registraci — najdi ji a uprav namespace:

```bash
grep -rn "VyzvyPanelBuilder" --include="*.cs" PmTracker.Web | grep -v "/bin/\|/obj/"
```

- [ ] **Krok 5: Vytvoř partial záložky**

Vytvoř `PmTracker.Web/Views/Projekty/_ProjectVyzvyTab.cshtml`:

```razor
@using PmTracker.Web.Models.ViewModels.Vyzvy
@model VyzvyPanelViewModel
@{
    var isActive = ViewData["IsActive"] as bool? ?? false;
}

<section id="panel-vyzvy"
         class="tab-panel @(isActive ? "active" : null)"
         role="tabpanel"
         aria-labelledby="tab-vyzvy"
         data-tab-panel="vyzvy">
    @await Html.PartialAsync("_VyzvyPanelBody", Model)
</section>
```

Přesuň dosavadní tělo panelu: zkopíruj `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml` na `PmTracker.Web/Views/Projekty/_VyzvyPanelBody.cshtml`, v něm změň direktivu modelu na `@using PmTracker.Web.Models.ViewModels.Vyzvy` + `@model VyzvyPanelViewModel` a názvy partialů `_VyzvyPanel.BufferCard` / `_VyzvyPanel.VyzvaCard` ponech — jen tyto dva soubory zkopíruj z `Views/ProjectDashboard/` do `Views/Projekty/` se stejnými názvy. Původní tři soubory ve `Views/ProjectDashboard/` smaž. (V bloku 2 se `_VyzvyPanelBody` i obě karty přepíší na nový layout.)

- [ ] **Krok 6: Zaregistruj záložku v controlleru**

V `PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModels.cs` přidej vedle `NavrhyTab`:

```csharp
    public ProjektLazyTabShellViewModel VyzvyTab { get; init; } = new() { TabKey = "vyzvy", LoadingText = "Načítání výzev..." };
```

a vedle `LoadedNavrhyTab`:

```csharp
    public VyzvyPanelViewModel? LoadedVyzvyTab { get; set; }
```

(doplň `using PmTracker.Web.Models.ViewModels.Vyzvy;`)

V `PmTracker.Web/Controllers/ProjektyController.cs`:

- Vedle konstanty `ProposalsTab` přidej `private const string VyzvyTab = "vyzvy";`
- K řádku s `model.NavrhyTab.LoadUrl` přidej:

```csharp
        model.VyzvyTab.LoadUrl = Url.Action(nameof(VyzvyTabPartial), new { id = projectId }) ?? $"/Projekty/VyzvyTabPartial/{projectId}";
```

- Do větve, která pro aktivní záložku předpřipraví obsah (vedle `model.LoadedNavrhyTab = proposalsTab;`), přidej analogickou větev pro `VyzvyTab`, která zavolá builder a naplní `model.LoadedVyzvyTab`.
- Přidej akci (umísti ji vedle ostatních `*TabPartial` akcí):

```csharp
    /// <summary>
    /// Panel Výzev jako projektová záložka (2026-09-07 přesun z dashboardu).
    /// Zobrazení není gateované oprávněním — vidí ho každý, kdo vidí projekt;
    /// omezené jsou až měnící akce, které si klíče ověřují samy.
    /// </summary>
    [HttpGet("/Projekty/VyzvyTabPartial/{id:int}")]
    public async Task<IActionResult> VyzvyTabPartial(int id, CancellationToken ct)
    {
        if (!await CanAccessProjectAsync(id, ct)) return Forbid();

        var muzeEditovat = CurrentUserContext.HasPermission(PermissionKeys.VyzvyCreate, id);
        var model = await _vyzvyPanelBuilder.BuildAsync(id, muzeEditovat, ct);
        return PartialView("_ProjectVyzvyTab", model);
    }
```

**Pozor:** `CanAccessProjectAsync` je zástupný název. Najdi, jak ostatní `*TabPartial` akce ověřují přístup k projektu, a použij **stejný** mechanismus. Stejně tak `_vyzvyPanelBuilder` doplň do konstruktoru podle vzoru ostatních závislostí.

- [ ] **Krok 7: Přidej odkaz záložky do menu**

V `PmTracker.Web/Views/Projekty/Detail.cshtml` do bloku `@{ ... }` nahoře přidej:

```csharp
    var vyzvyTabUrl = Url.Action("Detail", "Projekty", new { id = Model.Projekt.Id, tab = "vyzvy", asUser }) ?? $"/Projekty/Detail/{Model.Projekt.Id}?tab=vyzvy";
```

Mezi blok `@if (Model.CanViewProposals) { ... }` a `@if (Model.CanViewDashboard) { ... }` vlož:

```razor
        @* Výzvy — bez permission gate: vidí je každý, kdo vidí projekt (spec 2026-09-07 §4.1). *@
        <a class="tab tab-secondary @(Model.ActiveTab == "vyzvy" ? "active" : null)"
           id="tab-vyzvy" href="@vyzvyTabUrl" role="tab"
           aria-selected="@(Model.ActiveTab == "vyzvy" ? "true" : "false")"
           aria-controls="panel-vyzvy" data-tab="vyzvy">Výzvy</a>
```

Do sekce s panely (vedle bloku pro `panel-navrhy`) přidej stejnou dvojici větví jako mají Návrhy — pokud je `Model.LoadedVyzvyTab` naplněný, renderuj partial `_ProjectVyzvyTab` s `["IsActive"] = Model.ActiveTab == "vyzvy"`, jinak lazy `<section>` s `data-project-tab-lazy-url="@Model.VyzvyTab.LoadUrl"` a `data-project-tab-loaded="false"`.

**Poznámka:** komentář `@* *@` nikdy nedávej dovnitř tagu mezi atributy — spolkne následující atribut. Komentáře patří nad tag.

- [ ] **Krok 8: Odpoj Výzvy od dashboardu**

- V `PmTracker.Web/Views/ProjectDashboard/Index.cshtml` smaž tlačítko `data-dashboard-tab="vyzvy"` i odpovídající `<section data-dashboard-tab-panel="vyzvy">`.
- V `PmTracker.Web/Controllers/ProjectDashboardController.cs` smaž akci `GetVyzvyPanel` (řádky kolem 129-145) i nepoužité `using`y.
- Grepni zbytky a odstraň je:

```bash
grep -rn "vyzvy-panel\|VyzvyPanel\|dashboard.vyzvy" --include="*.cs" --include="*.cshtml" --include="*.js" PmTracker.Web | grep -v "/bin/\|/obj/"
```

- [ ] **Krok 9: Uprav oprávnění**

V `PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs`:

- Smaž definici `new("dashboard.vyzvy.view", "Záložka Výzvy", "DASHBOARD", PermissionScopeLevel.Project),`
- Smaž všech **11** přiřazení `dashboard.vyzvy.view` (role `SUPERADMIN`, `APP_ADMIN`, `READ_ALL`, `VLASTNIK_PROJEKTU`, `ADM_PROJ`, `PROJ_MAN`, `GEST`, `HOST`, `VEDOUCI_SUBSYSTEMU`, `ZASTUPCE_VEDOUCIHO_SUBSYSTEMU`, `METODIK_SUBSYSTEMU`)
- K roli `GEST` přidej pět klíčů, které dnes nemá:

```csharp
        new("GEST", "vyzvy.create", ScopeMode.All, true),
        new("GEST", "vyzvy.state.change", ScopeMode.All, true),
        new("GEST", "vyzvy.pnf.assign", ScopeMode.All, true),
        new("GEST", "vyzvy.pnf.reassign", ScopeMode.All, true),
        new("GEST", "vyzvy.word.export", ScopeMode.All, true),
```

V `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` smaž konstantu pro `dashboard.vyzvy.view`, pokud existuje.

V testech `ProjectRolePermissionMatrixTests.cs`, `PerActionKeyCoverageTests.cs` a `ResolverSwapSafetyTests.cs` odstraň `dashboard.vyzvy.view` ze seznamů očekávaných klíčů a doplň `GEST` tam, kde se vyjmenovávají držitelé `vyzvy.*`.

- [ ] **Krok 10: Spusť testy bloku**

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj` — očekávej 0 chyb, 0 varování.
Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTabPlacementTests"` — očekávej zelené.
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — očekávej zelené.

Nahlas výsledek všech tří běhů, včetně počtu testů.

---

## Blok 2: Rail 20/80 s výběrem roku

**Cíl bloku:** Panel má vlevo rail s dlaždicemi (buffer první, pod ním výzvy zvoleného roku od nejnovější) a vpravo obsah vybrané dlaždice. Přepínání dlaždic je okamžité, přepnutí roku načte jinou sadu výzev.

**Files:**
- Modify: `PmTracker.Web/Services/Vyzvy/IVyzvaService.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`
- Modify: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (parametr `rok`)
- Rewrite: `PmTracker.Web/Views/Projekty/_VyzvyPanelBody.cshtml`
- Create: `PmTracker.Web/Views/Projekty/_VyzvyRail.cshtml`
- Create: `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`
- Delete: `PmTracker.Web/Views/Projekty/_VyzvyPanel.BufferCard.cshtml`, `_VyzvyPanel.VyzvaCard.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/components/vyzvy-panel.css`
- Modify: `PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceRokTests.cs` (nový)

**Interfaces:**
- Consumes: `VyzvyPanelViewModel` a `VyzvyPanelBuilder` z bloku 1.
- Produces: `Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, int rok, CancellationToken ct)`, `Task<IReadOnlyList<int>> GetRokyAsync(int projektId, CancellationToken ct)`; na `VyzvyPanelViewModel` vlastnosti `int VybranyRok`, `IReadOnlyList<int> DostupneRoky`.

- [ ] **Krok 1: Napiš padající testy filtru roku**

Vytvoř `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceRokTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceRokTests
{
    private static VyzvaEntity Vyzva(int id, int poradove, int rok, DateTime zalozeni)
        => new()
        {
            Id = id, ProjektId = 1, Kod = $"{poradove}/{rok}", PoradoveVRoce = poradove, Rok = rok,
            Stav = VyzvaStav.Priprava, DatumZalozeni = zalozeni, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        };

    [Fact]
    public async Task GetVyzvy_VraciJenVybranyRok_OdNejnovejsi()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            Vyzva(1, 1, 2026, new DateTime(2026, 1, 10)),
            Vyzva(2, 2, 2026, new DateTime(2026, 5, 20)),
            Vyzva(3, 7, 2025, new DateTime(2025, 3, 3)));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var vyzvy = await svc.GetVyzvyAsync(1, 2026, CancellationToken.None);

        vyzvy.Select(v => v.Kod).Should().ContainInOrder("2/2026", "1/2026");
        vyzvy.Should().HaveCount(2, "rok 2025 do výběru nepatří");
    }

    [Fact]
    public async Task GetRoky_VraciRokySVyzvami_Sestupne_BezDuplicit()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            Vyzva(1, 1, 2024, new DateTime(2024, 1, 10)),
            Vyzva(2, 2, 2026, new DateTime(2026, 5, 20)),
            Vyzva(3, 3, 2026, new DateTime(2026, 6, 20)));
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var roky = await svc.GetRokyAsync(1, CancellationToken.None);

        roky.Should().ContainInOrder(2026, 2024);
        roky.Should().HaveCount(2);
    }
}
```

- [ ] **Krok 2: Spusť a ověř selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaServiceRokTests"`
Očekávej: chyba překladu — `GetVyzvyAsync` nemá parametr `rok`, `GetRokyAsync` neexistuje.

- [ ] **Krok 3: Rozšiř službu o rok**

V `IVyzvaService.cs` nahraď `Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, CancellationToken ct);` za:

```csharp
    /// <summary>Výzvy projektu v daném roce, od nejnovější k nejstarší.</summary>
    Task<IReadOnlyList<VyzvaDetail>> GetVyzvyAsync(int projektId, int rok, CancellationToken ct);

    /// <summary>Roky, ve kterých projekt má aspoň jednu výzvu, sestupně.</summary>
    Task<IReadOnlyList<int>> GetRokyAsync(int projektId, CancellationToken ct);
```

Ve `VyzvaService.Queries.cs` změň hlavičku `GetVyzvyAsync` na `(int projektId, int rok, CancellationToken ct)` a její první dotaz na:

```csharp
        var vyzvy = await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId && v.Rok == rok)
            .OrderByDescending(v => v.DatumZalozeni)
            .ToListAsync(ct);
```

Do stejné třídy přidej:

```csharp
    public async Task<IReadOnlyList<int>> GetRokyAsync(int projektId, CancellationToken ct)
        => await _db.Vyzvy.AsNoTracking()
            .Where(v => v.ProjektId == projektId)
            .Select(v => v.Rok)
            .Distinct()
            .OrderByDescending(r => r)
            .ToListAsync(ct);
```

- [ ] **Krok 4: Ověř, že testy služby projdou**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaServiceRokTests"`
Očekávej: zelené. Builder zatím nepřekládá — spraví ho krok 5.

- [ ] **Krok 5: Rozšiř view model a builder o rok**

Do `VyzvyPanelViewModel` přidej:

```csharp
    /// <summary>Rok zvolený v railu. Buffer je na roku nezávislý.</summary>
    public int VybranyRok { get; init; }

    /// <summary>Nabídka roků: roky s výzvami, vždy včetně aktuálního roku, sestupně.</summary>
    public IReadOnlyList<int> DostupneRoky { get; init; } = Array.Empty<int>();
```

V `VyzvyPanelBuilder` změň signaturu na `BuildAsync(int projektId, bool muzeEditovat, int? rok, CancellationToken ct)` a začátek metody na:

```csharp
        var projekt = await _db.Projekty.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projektId, ct);

        var roky = await _vyzvaService.GetRokyAsync(projektId, ct);
        var aktualniRok = DateTime.Now.Year;

        // Aktuální rok je v nabídce vždy, i když v něm zatím žádná výzva není —
        // jinak by nešlo založit první výzvu roku.
        var dostupneRoky = roky.Contains(aktualniRok)
            ? roky
            : roky.Concat(new[] { aktualniRok }).OrderByDescending(r => r).ToList();

        var vybranyRok = rok is int r && dostupneRoky.Contains(r) ? r : aktualniRok;

        var bufferItems = await _vyzvaService.GetBufferAsync(projektId, ct);
        var vyzvy = await _vyzvaService.GetVyzvyAsync(projektId, vybranyRok, ct);
```

a do vraceného objektu doplň `VybranyRok = vybranyRok,` a `DostupneRoky = dostupneRoky,`.

V `ProjektyController.VyzvyTabPartial` přidej parametr `int? rok` a předej ho builderu.

- [ ] **Krok 6: Přepiš tělo panelu na grid 20/80**

Nahraď obsah `PmTracker.Web/Views/Projekty/_VyzvyPanelBody.cshtml`:

```razor
@using PmTracker.Web.Models.ViewModels.Vyzvy
@model VyzvyPanelViewModel

<div class="vyzvy-panel"
     data-vyzvy-panel
     data-project-id="@Model.ProjektId"
     data-vybrany-rok="@Model.VybranyRok"
     data-muze-editovat="@(Model.MuzeEditovat ? "true" : "false")">

    @if (!string.IsNullOrWhiteSpace(Model.ChybaProjektuMessage))
    {
        <gov-message type="warning" class="vyzvy-panel-warning">
            @Model.ChybaProjektuMessage
        </gov-message>
    }

    <div class="vyzvy-layout">
        @await Html.PartialAsync("_VyzvyRail", Model)

        <div class="vyzvy-content" data-vyzvy-content>
            @await Html.PartialAsync("_VyzvyPane", VyzvyPaneViewModel.ProBuffer(Model))
            @foreach (var v in Model.Vyzvy)
            {
                @await Html.PartialAsync("_VyzvyPane", VyzvyPaneViewModel.ProVyzvu(Model, v))
            }
        </div>
    </div>
</div>
```

Do `VyzvyPanelViewModels.cs` přidej pomocný model, aby partial panelu nemusel rozlišovat buffer a výzvu:

```csharp
/// <summary>
/// Jeden panel v pravém sloupci. Buffer i výzva sdílí stejný tvar, liší se jen tím,
/// že buffer nemá hlavičku ani součet — díky tomu je partial jen jeden.
/// </summary>
public sealed class VyzvyPaneViewModel
{
    public required string Klic { get; init; }              // "buffer" nebo "vyzva-{id}"
    public bool JeBuffer { get; init; }
    public bool MuzeEditovat { get; init; }
    public VyzvyPanelVyzvaViewModel? Vyzva { get; init; }
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();

    public static VyzvyPaneViewModel ProBuffer(VyzvyPanelViewModel panel) => new()
    {
        Klic = "buffer",
        JeBuffer = true,
        MuzeEditovat = panel.MuzeEditovat,
        Polozky = panel.Buffer.Polozky,
    };

    public static VyzvyPaneViewModel ProVyzvu(VyzvyPanelViewModel panel, VyzvyPanelVyzvaViewModel vyzva) => new()
    {
        Klic = $"vyzva-{vyzva.Id}",
        JeBuffer = false,
        MuzeEditovat = panel.MuzeEditovat,
        Vyzva = vyzva,
        Polozky = vyzva.Polozky,
    };
}
```

- [ ] **Krok 7: Vytvoř rail**

Vytvoř `PmTracker.Web/Views/Projekty/_VyzvyRail.cshtml`:

```razor
@using PmTracker.Web.Models.ViewModels.Vyzvy
@model VyzvyPanelViewModel

<nav class="vyzvy-rail" aria-label="Výzvy projektu">
    <div class="vyzvy-rail-rok">
        <gov-form-select size="s">
            <select aria-label="Rok výzev" data-vyzvy-rok>
                @foreach (var r in Model.DostupneRoky)
                {
                    <option value="@r" selected="@(r == Model.VybranyRok)">@r</option>
                }
            </select>
        </gov-form-select>
    </div>

    <ul class="vyzvy-rail-list">
        @* Buffer je první vždy, nezávisle na zvoleném roce (spec §6.3). *@
        <li>
            <button type="button" class="vyzvy-tile is-selected"
                    data-vyzvy-tile="buffer" data-vyzvy-drop-target="buffer"
                    aria-pressed="true">
                <span class="vyzvy-tile-title">Buffer</span>
                <span class="vyzvy-tile-count">@Model.Buffer.Polozky.Count PNF</span>
            </button>
        </li>

        @foreach (var v in Model.Vyzvy)
        {
            var jeZamcena = v.Stav != "Priprava";
            <li>
                <button type="button" class="vyzvy-tile"
                        data-vyzvy-tile="vyzva-@v.Id"
                        data-vyzva-id="@v.Id"
                        data-vyzvy-drop-target="@(jeZamcena ? "none" : "vyzva")"
                        aria-pressed="false">
                    <span class="vyzvy-tile-title">@v.Kod</span>
                    <gov-tag size="s" color="@(v.Stav switch { "Priprava" => "warning", "Odeslano" => "success", _ => "neutral" })">
                        @(v.Stav switch { "Priprava" => "Příprava", "Odeslano" => "Odesláno", _ => "Zrušeno" })
                    </gov-tag>
                    <span class="vyzvy-tile-count">@v.Polozky.Count PNF</span>
                </button>
            </li>
        }
    </ul>

    @if (Model.MuzeEditovat)
    {
        <div class="vyzvy-rail-actions">
            <pm-button variant="Primary" size="Small" data-vyzvy-action="otevrit-novou">Nová výzva</pm-button>
        </div>
    }
</nav>
```

**Pozor:** hodnoty atributů `size` a `color` u `gov-form-select` a `gov-tag` ověř proti reálné distribuci — otevři `PmTracker.Web/wwwroot/lib/gov-design-system/` a zkontroluj, jaké hodnoty komponenta přijímá. Pokud se liší, použij ty skutečné; nevymýšlej si je.

- [ ] **Krok 8: Vytvoř panel obsahu**

Vytvoř `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`. Zatím renderuje plochý seznam PNF — seskupení podle záznamu přidá blok 3:

```razor
@using System.Globalization
@using PmTracker.Web.Models.ViewModels.Vyzvy
@model VyzvyPaneViewModel
@{
    var css = CultureInfo.GetCultureInfo("cs-CZ");
}

<section class="vyzvy-pane" data-vyzvy-pane="@Model.Klic" hidden="@(Model.Klic != "buffer")">
    @if (!Model.JeBuffer && Model.Vyzva is not null)
    {
        <header class="vyzvy-pane-header">
            <h3 class="vyzvy-pane-title">Výzva @Model.Vyzva.Kod</h3>
            <span class="vyzvy-pane-meta">
                @Model.Vyzva.ZalozilJmeno · @Model.Vyzva.DatumZalozeni.ToString("d. M. yyyy", css)
            </span>
        </header>
    }

    @if (Model.Polozky.Count == 0)
    {
        <gov-empty>
            @(Model.JeBuffer
                ? "Buffer je prázdný — přepněte switch u PNF v externích vazbách."
                : "Výzva zatím neobsahuje žádné PNF.")
        </gov-empty>
    }
    else
    {
        <ul class="vyzvy-pnf-list">
            @foreach (var p in Model.Polozky)
            {
                <li class="vyzvy-pnf" data-externi-odkaz-id="@p.ExterniOdkazId">
                    <span class="vyzvy-pnf-cislo">@p.Cislo</span>
                    <span class="vyzvy-pnf-nazev">@(p.StrucneNazev ?? "(bez názvu z HOT)")</span>
                    <span class="vyzvy-pnf-cena">
                        @(p.PredpokladanaCena?.ToString("N2", css) ?? "—")
                    </span>
                </li>
            }
        </ul>
    }
</section>
```

Smaž `_VyzvyPanel.BufferCard.cshtml` a `_VyzvyPanel.VyzvaCard.cshtml` ve `Views/Projekty/`.

- [ ] **Krok 9: Styly railu**

Do `PmTracker.Web/wwwroot/css/components/vyzvy-panel.css` nahraď stávající pravidla panelu za layout 20/80. Nosné jsou letité vlastnosti, žádné `:has`:

```css
.vyzvy-layout { display: grid; grid-template-columns: 1fr 4fr; gap: 1rem; align-items: start; }
.vyzvy-rail { display: flex; flex-direction: column; gap: 0.75rem; }
.vyzvy-rail-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.5rem; }
.vyzvy-tile {
  width: 100%; text-align: left; cursor: pointer;
  display: flex; flex-direction: column; gap: 0.25rem;
  padding: 0.6rem 0.75rem; border: 1px solid var(--pm-border); border-radius: 6px;
  background: var(--pm-surface); color: inherit;
}
.vyzvy-tile.is-selected { border-color: var(--gov-color-primary-base, #2362a2); border-width: 2px; }
.vyzvy-tile.is-drag-over { border-style: dashed; border-width: 2px; }
.vyzvy-tile-title { font-weight: 600; }
.vyzvy-tile-count { font-size: 0.85em; color: var(--pm-text-muted); }
.vyzvy-pnf-list { list-style: none; margin: 0; padding: 0; }
.vyzvy-pnf { display: grid; grid-template-columns: auto 1fr auto auto; gap: 0.75rem; align-items: center;
             padding: 0.4rem 0.5rem; border-bottom: 1px solid var(--pm-border); }
.vyzvy-pnf-cena { text-align: right; font-variant-numeric: tabular-nums; }
```

**Před zápisem grepni duplicity** — `site.css` často přebíjí `components/` kvůli pořadí načítání:

```bash
grep -n "vyzvy-panel\|vyzvy-tile\|vyzvy-layout" PmTracker.Web/wwwroot/css/site.css
```

Pokud tam pravidla jsou, smaž je tam, ne tady.

- [ ] **Krok 10: JS pro výběr dlaždice a rok**

V `PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js` nahraď funkci `reloadPanel` a doplň výběr dlaždice. Klíčové části:

```js
  function selectTile(panelElement, klic) {
    panelElement.querySelectorAll('[data-vyzvy-tile]').forEach(function (tile) {
      const on = tile.dataset.vyzvyTile === klic;
      tile.classList.toggle('is-selected', on);
      tile.setAttribute('aria-pressed', on ? 'true' : 'false');
    });
    panelElement.querySelectorAll('[data-vyzvy-pane]').forEach(function (pane) {
      pane.hidden = pane.dataset.vyzvyPane !== klic;
    });
    panelElement.dataset.vybranaDlazdice = klic;
  }

  // Rok mění množinu výzev → nutný dotaz na server. Po překreslení se vybere buffer,
  // protože dlaždice předchozího roku už v railu nejsou.
  async function reloadPanel(panelElement, options) {
    const opts = options || {};
    const projectId = panelElement.dataset.projectId;
    const rok = opts.rok || panelElement.dataset.vybranyRok;
    const scrollEl = document.scrollingElement || document.documentElement;
    const scrollY = scrollEl.scrollTop;

    const url = '/Projekty/VyzvyTabPartial/' + encodeURIComponent(projectId) +
                '?rok=' + encodeURIComponent(rok);
    const resp = await fetch(url, { credentials: 'same-origin' });
    if (!resp.ok) { showToast('Načtení panelu selhalo.', true); return; }

    const tmp = document.createElement('div');
    tmp.innerHTML = await resp.text();
    const newEl = tmp.querySelector('[data-vyzvy-panel]');
    if (!newEl || !panelElement.parentNode) return;

    panelElement.parentNode.replaceChild(newEl, panelElement);
    if (global.pmVyzvy && global.pmVyzvy.bootstrap) global.pmVyzvy.bootstrap(newEl);

    // Zachovej vybranou dlaždici, pokud v novém railu existuje — jinak buffer.
    const chtena = opts.vybrat || panelElement.dataset.vybranaDlazdice || 'buffer';
    const existuje = newEl.querySelector('[data-vyzvy-tile="' + chtena + '"]');
    selectTile(newEl, existuje ? chtena : 'buffer');
    scrollEl.scrollTop = scrollY;
  }
```

Do `bindPanel` přidej obsluhu kliknutí na dlaždici a změny roku:

```js
      const tile = e.target.closest('[data-vyzvy-tile]');
      if (tile) { selectTile(panelElement, tile.dataset.vyzvyTile); return; }
```

```js
    panelElement.addEventListener('change', function (e) {
      const rokSelect = e.target.closest('[data-vyzvy-rok]');
      if (rokSelect) reloadPanel(panelElement, { rok: rokSelect.value, vybrat: 'buffer' });
    });
```

Smaž větev `action === 'otevrit-reassign'` — modal zaniká v bloku 6.

- [ ] **Krok 11: Ověř blok**

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj` — 0 chyb, 0 varování.
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — zelené.
Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTabPlacementTests"` — zelené.

Nahlas výsledky. Ruční kontrola uživatelem: rail vlevo, buffer první, přepnutí dlaždice i roku funguje.

---

## Blok 3: PNF seskupené podle projektového záznamu

**Cíl bloku:** Pravý panel neukazuje plochý seznam PNF, ale záznamy a pod každým jeho PNF. Jeden záznam se může objevit pod více výzvami, protože každé jeho PNF může být jinde.

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`
- Modify: `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`
- Modify: `PmTracker.Web/wwwroot/css/components/vyzvy-panel.css`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvyPanelGroupingTests.cs` (nový)

**Interfaces:**
- Consumes: `VyzvyPanelBuilder.BuildAsync(int projektId, bool muzeEditovat, int? rok, CancellationToken ct)` z bloku 2.
- Produces: `VyzvyPanelZaznamSkupinaViewModel` s vlastnostmi `int ZaznamId`, `string? CisloViditelne`, `string? Nazev`, `IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky`; na `VyzvyPaneViewModel` vlastnost `IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Skupiny` nahrazuje `Polozky`.

- [ ] **Krok 1: Napiš padající test seskupení**

Vytvoř `PmTracker.Tests.Unit/Vyzvy/VyzvyPanelGroupingTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Pravý panel seskupuje PNF podle projektového záznamu (spec §7.1). Jeden záznam
/// může mít víc PNF a každé může být v jiné výzvě, takže se záznam objeví vícekrát.
/// </summary>
public sealed class VyzvyPanelGroupingTests
{
    private static VyzvaEntity Vyzva(int id, int poradove)
        => new()
        {
            Id = id, ProjektId = 1, Kod = $"{poradove}/2026", PoradoveVRoce = poradove, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 4, poradove), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        };

    private static ZaznamExterniOdkazEntity Pnf(int id, int zaznamId, string cislo, int? vyzvaId)
        => new()
        {
            Id = id, ZaznamId = zaznamId, TypOdkazuId = 1, Cislo = cislo,
            ZaradidDoVyzvy = true, VyzvaId = vyzvaId,
        };

    [Fact]
    public async Task Build_SeskupujePnfPodleZaznamu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 200);

        db.Vyzvy.Add(Vyzva(10, 1));
        db.ZaznamExterniOdkazy.AddRange(
            Pnf(500, 100, "336865", 10),
            Pnf(501, 100, "341837", 10),
            Pnf(502, 200, "345763", 10));
        await db.SaveChangesAsync();

        var builder = new VyzvyPanelBuilder(VyzvaServiceTestHarness.CreateService(db), db);
        var model = await builder.BuildAsync(1, muzeEditovat: true, rok: 2026, CancellationToken.None);

        var vyzva = model.Vyzvy.Should().ContainSingle().Which;
        vyzva.Skupiny.Should().HaveCount(2, "PNF patří dvěma různým záznamům");
        vyzva.Skupiny.Single(s => s.ZaznamId == 100).Polozky.Should().HaveCount(2);
        vyzva.Skupiny.Single(s => s.ZaznamId == 200).Polozky.Should().HaveCount(1);
        vyzva.Skupiny.Single(s => s.ZaznamId == 100).Nazev.Should().Be("test");
    }

    [Fact]
    public async Task Build_JedenZaznamVeDvouVyzvach_JeVObou()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.Vyzvy.AddRange(Vyzva(10, 1), Vyzva(11, 2));
        db.ZaznamExterniOdkazy.AddRange(
            Pnf(500, 100, "336865", 10),
            Pnf(501, 100, "341837", 11));
        await db.SaveChangesAsync();

        var builder = new VyzvyPanelBuilder(VyzvaServiceTestHarness.CreateService(db), db);
        var model = await builder.BuildAsync(1, muzeEditovat: true, rok: 2026, CancellationToken.None);

        model.Vyzvy.Should().HaveCount(2);
        model.Vyzvy.Should().OnlyContain(v => v.Skupiny.Count == 1 && v.Skupiny[0].ZaznamId == 100,
            "PNF téhož záznamu mohou být v různých výzvách — záznam se objeví v obou");
    }

    [Fact]
    public async Task Build_BufferSeSkupinamiTake()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);
        db.ZaznamExterniOdkazy.Add(Pnf(500, 100, "336865", null));
        await db.SaveChangesAsync();

        var builder = new VyzvyPanelBuilder(VyzvaServiceTestHarness.CreateService(db), db);
        var model = await builder.BuildAsync(1, muzeEditovat: true, rok: 2026, CancellationToken.None);

        model.Buffer.Skupiny.Should().ContainSingle()
            .Which.Polozky.Should().ContainSingle().Which.Cislo.Should().Be("336865");
    }
}
```

- [ ] **Krok 2: Spusť a ověř selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvyPanelGroupingTests"`
Očekávej: chyba překladu — `Skupiny` na view modelu neexistuje.

- [ ] **Krok 3: Doplň model skupiny**

Do `VyzvyPanelViewModels.cs` přidej:

```csharp
/// <summary>
/// Jeden projektový záznam a jeho PNF v rámci jedné výzvy (nebo bufferu).
/// Záznam se může objevit pod více výzvami — každé jeho PNF může být jinde.
/// </summary>
public sealed class VyzvyPanelZaznamSkupinaViewModel
{
    public int ZaznamId { get; init; }
    public string? CisloViditelne { get; init; }
    public string? Nazev { get; init; }
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();
}
```

Na `VyzvyPanelBufferViewModel` a `VyzvyPanelVyzvaViewModel` přidej:

```csharp
    public IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Skupiny { get; init; } = Array.Empty<VyzvyPanelZaznamSkupinaViewModel>();
```

Na `VyzvyPaneViewModel` nahraď `Polozky` za `Skupiny` stejného typu a v obou továrních metodách plň `Skupiny = panel.Buffer.Skupiny` resp. `Skupiny = vyzva.Skupiny`.

- [ ] **Krok 4: Seskup a seřaď v builderu**

Ve `VyzvyPanelBuilder` nahraď načítání `CisloViditelne` za načtení celého řadicího kontextu a doplň seskupení:

```csharp
    /// <summary>Data záznamu potřebná pro popisek skupiny a její řazení.</summary>
    private sealed record ZaznamInfo(
        string? CisloViditelne, string Nazev, string? KategorieNazev,
        int CisloViditelneA, byte CisloViditelneTyp, int CisloViditelneB, int CisloZaznamu);

    private async Task<IReadOnlyDictionary<int, ZaznamInfo>> LoadZaznamInfoAsync(
        IEnumerable<int> zaznamIds, CancellationToken ct)
    {
        var ids = zaznamIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, ZaznamInfo>();

        return await (from z in _db.ProjektoveZaznamy.AsNoTracking()
                      join k in _db.CiselnikKategoriiZaznamu.AsNoTracking() on z.KategorieId equals k.Id into kj
                      from k in kj.DefaultIfEmpty()
                      where ids.Contains(z.Id)
                      select new
                      {
                          z.Id, z.CisloViditelne, z.Nazev, KategorieNazev = k != null ? k.Nazev : null,
                          z.CisloViditelneA, z.CisloViditelneTyp, z.CisloViditelneB, z.CisloZaznamu,
                      })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ZaznamInfo(x.CisloViditelne, x.Nazev, x.KategorieNazev,
                                    x.CisloViditelneA, x.CisloViditelneTyp, x.CisloViditelneB, x.CisloZaznamu),
                ct);
    }

    /// <summary>
    /// Skupiny se řadí stejně jako záznamy v záložce Záznamy a v tisku — sdílenou
    /// utilitou RecordDisplayOrdering, aby uživatel nepotkal tři různá pořadí.
    /// </summary>
    private static IReadOnlyList<VyzvyPanelZaznamSkupinaViewModel> Seskup(
        IEnumerable<VyzvyPanelPolozkaViewModel> polozky,
        IReadOnlyDictionary<int, ZaznamInfo> info)
        => polozky
            .GroupBy(p => p.ZaznamId)
            .Select(g =>
            {
                var i = info.GetValueOrDefault(g.Key);
                return new
                {
                    Skupina = new VyzvyPanelZaznamSkupinaViewModel
                    {
                        ZaznamId = g.Key,
                        CisloViditelne = i?.CisloViditelne,
                        Nazev = i?.Nazev,
                        Polozky = g.OrderBy(p => p.ExterniOdkazId).ToArray(),
                    },
                    Kategorie = RecordDisplayOrdering.CategoryOrder(i?.KategorieNazev),
                    PartA = RecordDisplayOrdering.VisibleNumberPartA(i?.CisloViditelneA ?? 0, i?.CisloZaznamu ?? 0),
                    PartB = RecordDisplayOrdering.VisibleNumberPartB(i?.CisloViditelneTyp ?? 0, i?.CisloViditelneB ?? 0),
                    Cislo = i?.CisloZaznamu ?? 0,
                };
            })
            .OrderBy(x => x.Kategorie).ThenBy(x => x.PartA).ThenBy(x => x.PartB).ThenBy(x => x.Cislo)
            .Select(x => x.Skupina)
            .ToArray();
```

Doplň `using PmTracker.Web.Services.Common;`. V `BuildAsync` nahraď volání `LoadCisloViditelneMapAsync` za `LoadZaznamInfoAsync` (nad sjednocenými ID bufferu i všech výzev), starou metodu smaž a při stavbě bufferu i každé výzvy naplň `Skupiny = Seskup(polozky, info)`. Vlastnost `Polozky` na obou modelech ponech — používá ji počet PNF na dlaždici.

**Pozor:** ověř skutečný název DbSetu číselníku kategorií (`grep -n "CiselnikKategoriiZaznamu" PmTracker.Web/Data/PmTrackerDbContext.cs`) a vlastnost s názvem kategorie; pokud se liší, uprav dotaz.

- [ ] **Krok 5: Spusť testy a ověř, že projdou**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvyPanelGroupingTests"`
Očekávej: zelené.

- [ ] **Krok 6: Vykresli skupiny v panelu**

V `_VyzvyPane.cshtml` nahraď blok `<ul class="vyzvy-pnf-list">` za:

```razor
        @foreach (var skupina in Model.Skupiny)
        {
            <section class="vyzvy-zaznam">
                <h4 class="vyzvy-zaznam-hlavicka">
                    <span class="vyzvy-zaznam-cislo">@(skupina.CisloViditelne ?? "—")</span>
                    <span class="vyzvy-zaznam-nazev">@skupina.Nazev</span>
                </h4>
                <ul class="vyzvy-pnf-list">
                    @foreach (var p in skupina.Polozky)
                    {
                        <li class="vyzvy-pnf" data-externi-odkaz-id="@p.ExterniOdkazId">
                            <span class="vyzvy-pnf-cislo">@p.Cislo</span>
                            <span class="vyzvy-pnf-nazev">@(p.StrucneNazev ?? "(bez názvu z HOT)")</span>
                            <span class="vyzvy-pnf-cena">
                                @(p.PredpokladanaCena?.ToString("N2", css) ?? "—")
                            </span>
                        </li>
                    }
                </ul>
            </section>
        }
```

a podmínku prázdnoty změň z `Model.Polozky.Count == 0` na `Model.Skupiny.Count == 0`.

Do CSS přidej:

```css
.vyzvy-zaznam { margin-bottom: 1rem; }
.vyzvy-zaznam-hlavicka { display: flex; gap: 0.5rem; align-items: baseline; margin: 0 0 0.35rem; font-size: 0.95rem; }
.vyzvy-zaznam-cislo { font-weight: 700; }
.vyzvy-zaznam-nazev { color: var(--pm-text-muted); }
```

- [ ] **Krok 7: Vrať hlavičku výzvy — stav a měnič stavu (dluhy D3, D4)**

Blok 2 smazal `_VyzvyPanel.VyzvaCard.cshtml`, se kterou zmizel i měnič stavu. JS obsluha
(`handleZmenitStav`, selektory `[data-vyzvy-stav-toggle]`, `[data-vyzvy-action="zmenit-stav"]`)
zůstala funkční, chybí jen markup. Spec §6.4 navíc v hlavičce vyžaduje stav a datum odeslání.

Do `<header class="vyzvy-pane-header">` v `_VyzvyPane.cshtml` doplň za `vyzvy-pane-meta`:

```razor
            <gov-tag size="s" type="subtle" color="@StavColor(Model.Vyzva.Stav)">@StavLabel(Model.Vyzva.Stav)</gov-tag>
            @if (Model.Vyzva.DatumOdeslani is DateTime odeslano)
            {
                <span class="vyzvy-pane-meta">odesláno @odeslano.ToString("d. M. yyyy", css)</span>
            }
            @if (Model.MuzeEditovat && Model.Vyzva.PovoleneStavy.Count > 0)
            {
                <span class="vyzvy-stav-menu" data-vyzvy-stav-menu>
                    <pm-button variant="Secondary" size="Small" data-vyzvy-stav-toggle>Změnit stav</pm-button>
                    <ul class="vyzvy-stav-menu-list">
                        @foreach (var stav in Model.Vyzva.PovoleneStavy)
                        {
                            <li>
                                <button type="button"
                                        data-vyzvy-action="zmenit-stav"
                                        data-vyzva-id="@Model.Vyzva.Id"
                                        data-novy-stav="@stav">@StavLabel(stav)</button>
                            </li>
                        }
                    </ul>
                </span>
            }
```

Pomocné funkce `StavColor` / `StavLabel` přesuň z `_VyzvyRail.cshtml` do statické třídy
`PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaStavPresentation.cs`, aby je oba partialy
sdílely a popisky se nerozešly:

```csharp
namespace PmTracker.Web.Models.ViewModels.Vyzvy;

/// <summary>Popisky a barvy stavů výzvy — sdílené railem i panelem, aby se nerozešly.</summary>
public static class VyzvaStavPresentation
{
    public static string Color(string stav) => stav switch
    {
        "Priprava" => "warning",
        "Odeslano" => "success",
        _ => "neutral"
    };

    public static string Label(string stav) => stav switch
    {
        "Priprava" => "Příprava",
        "Odeslano" => "Odesláno",
        _ => "Zrušeno"
    };
}
```

Styly `.vyzvy-stav-menu` a `.vyzvy-stav-menu-list` v `components/vyzvy-panel.css` už existují
(zůstaly z původního layoutu) — ověř grepem, že nebyly odstraněny, a případně je vrať.

- [ ] **Krok 8: Ověř blok**

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj` — 0 chyb, 0 varování.
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — zelené.
Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTabPlacementTests"` — zelené.
Nahlas počty.

---

## Blok 4: Zakládání výzvy s ručním číslem

**Cíl bloku:** Tlačítko „Nová výzva" otevře modal, kde zadáš číslo domluvené se SVA. Výzva vznikne prázdná, buffer zůstane nedotčený. Duplicitní číslo a číslo mimo rozsah aplikace odmítne.

**Files:**
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaErrors.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/IVyzvaService.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.Founding.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaCodeGenerator.cs`
- Modify: `PmTracker.Web/Controllers/VyzvyController.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`
- Modify: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Create: `PmTracker.Web/Views/Projekty/_VyzvyNovaModal.cshtml`
- Modify: `PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaCodeGeneratorTests.cs`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvyControllerTests.cs`

**Interfaces:**
- Produces: `Task<VyzvaResult<VyzvaDetail>> ZalozitVyzvuAsync(int projektId, int poradoveVRoce, int zalozilOsobaId, DateTime now, CancellationToken ct)`; `Task<IReadOnlyList<int>> GetObsazenaCislaAsync(int projektId, int rok, CancellationToken ct)`; kódy `VyzvaErrorCode.InvalidVyzvaNumber`, `VyzvaErrorCode.DuplicateVyzvaNumber`; na `VyzvyPanelViewModel` vlastnosti `IReadOnlyList<int> ObsazenaCisla` a `string? CisloRamcoveSmlouvy`.

- [x] **Krok 1: Přepiš testy zakládání**

Nahraď celý obsah `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceFoundingTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaServiceFoundingTests
{
    private static readonly DateTime Now2026 = new(2026, 4, 20);

    [Fact]
    public async Task ZalozitVyzvu_PrazdnyBuffer_Projde()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 3, 7, Now2026, CancellationToken.None);

        var ok = result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>().Which.Value;
        ok.Kod.Should().Be("3/2026");
        ok.Polozky.Should().BeEmpty("výzva vzniká prázdná, PNF se do ní přesouvají ručně");
    }

    [Fact]
    public async Task ZalozitVyzvu_NechavaBufferNedotceny()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 500, ZaznamId = 100, TypOdkazuId = 1, Cislo = "336865",
            ZaradidDoVyzvy = true, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        await svc.ZalozitVyzvuAsync(1, 1, 7, Now2026, CancellationToken.None);

        var ev = await db.ZaznamExterniOdkazy.FindAsync(500);
        ev!.VyzvaId.Should().BeNull("zakládání už buffer nevysává");
    }

    [Fact]
    public async Task ZalozitVyzvu_ZapiseSnapshotyAHistorii()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 5, 7, Now2026, CancellationToken.None);

        var ok = result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>().Which.Value;
        ok.PoradoveVRoce.Should().Be(5);
        ok.Rok.Should().Be(2026);
        ok.Stav.Should().Be(VyzvaStav.Priprava);
        ok.MistoPlneniSnapshot.Should().Be("FIS (EIS): VZ 8201");
        ok.CisloRamcoveSmlouvySnapshot.Should().Be("23106000271");
        db.VyzvaHistorieStavu.Should().ContainSingle(h => h.NovyStav == VyzvaStav.Priprava && h.PuvodniStav == null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task ZalozitVyzvu_CisloMimoRozsah_InvalidVyzvaNumber(int poradove)
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, poradove, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.InvalidVyzvaNumber);
    }

    [Fact]
    public async Task ZalozitVyzvu_ObsazeneCislo_DuplicateVyzvaNumber()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
            Stav = VyzvaStav.Odeslano, DatumZalozeni = Now2026, ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 2, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.DuplicateVyzvaNumber);
    }

    [Fact]
    public async Task ZalozitVyzvu_StejneCisloJinyRok_Projde()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 1, ProjektId = 1, Kod = "2/2025", PoradoveVRoce = 2, Rok = 2025,
            Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2025, 4, 1), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 2, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Ok>()
            .Which.Value.Kod.Should().Be("2/2026", "unikátnost je v rámci roku a smlouvy");
    }

    [Fact]
    public async Task ZalozitVyzvu_BezMistaPlneni_Error()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = null, CisloRamcoveSmlouvy = "A",
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.ZalozitVyzvuAsync(1, 1, 7, Now2026, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<VyzvaDetail>.Fail>()
            .Which.Error.Code.Should().Be(VyzvaErrorCode.ProjectMissingMistoPlneni);
    }

    [Fact]
    public async Task GetObsazenaCisla_VraciCislaRokuASmlouvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            new VyzvaEntity
            {
                Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = Now2026, ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
            },
            new VyzvaEntity
            {
                Id = 2, ProjektId = 1, Kod = "4/2026", PoradoveVRoce = 4, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = Now2026, ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
            },
            new VyzvaEntity
            {
                Id = 3, ProjektId = 1, Kod = "9/2025", PoradoveVRoce = 9, Rok = 2025,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2025, 1, 1), ZalozilOsobaId = 1,
                MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
            });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var obsazena = await svc.GetObsazenaCislaAsync(1, 2026, CancellationToken.None);

        obsazena.Should().BeEquivalentTo(new[] { 1, 4 });
    }
}
```

- [x] **Krok 2: Spusť a ověř selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaServiceFoundingTests"`
Očekávej: chyba překladu — `ZalozitVyzvuAsync`, `GetObsazenaCislaAsync` ani nové chybové kódy neexistují.

- [x] **Krok 3: Doplň chybové kódy**

Ve `VyzvaErrors.cs` smaž `BufferEmpty = 4,` a před uzavírací závorku enumu přidej:

```csharp
    InvalidVyzvaNumber = 12,
    DuplicateVyzvaNumber = 13,
```

- [x] **Krok 4: Uprav rozhraní**

V `IVyzvaService.cs` nahraď deklaraci `ZaloztVyzvuZBufferuAsync` za:

```csharp
    /// <summary>
    /// Založí prázdnou výzvu s ručně zadaným pořadovým číslem. Číslo se domlouvá externě
    /// (SVA), aplikace ho negeneruje — ověřuje jen rozsah 1–999 a duplicitu v rámci
    /// (číslo rámcové smlouvy, rok). PNF se do výzvy přesouvají samostatnou akcí.
    /// </summary>
    Task<VyzvaResult<VyzvaDetail>> ZalozitVyzvuAsync(
        int projektId, int poradoveVRoce, int zalozilOsobaId, DateTime now, CancellationToken ct);

    /// <summary>Pořadová čísla už obsazená v daném roce a rámcové smlouvě projektu — nápověda do formuláře.</summary>
    Task<IReadOnlyList<int>> GetObsazenaCislaAsync(int projektId, int rok, CancellationToken ct);
```

- [x] **Krok 5: Přepiš zakládání**

Nahraď celý obsah `PmTracker.Web/Services/Vyzvy/VyzvaService.Founding.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.Vyzvy;

public sealed partial class VyzvaService
{
    /// <summary>Povolený rozsah pořadového čísla výzvy (spec §5.1).</summary>
    private const int MinPoradoveVRoce = 1;
    private const int MaxPoradoveVRoce = 999;

    public async Task<VyzvaResult<VyzvaDetail>> ZalozitVyzvuAsync(
        int projektId, int poradoveVRoce, int zalozilOsobaId, DateTime now, CancellationToken ct)
    {
        var projekt = await _db.Projekty.FirstOrDefaultAsync(p => p.Id == projektId, ct);
        if (projekt == null)
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectNotFound, "Projekt nenalezen");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingMistoPlneni, "Projekt nemá místo plnění");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return Fail<VyzvaDetail>(VyzvaErrorCode.ProjectMissingCisloRamcoveSmlouvy, "Projekt nemá číslo rámcové smlouvy");

        if (poradoveVRoce < MinPoradoveVRoce || poradoveVRoce > MaxPoradoveVRoce)
            return Fail<VyzvaDetail>(VyzvaErrorCode.InvalidVyzvaNumber,
                $"Číslo výzvy musí být v rozsahu {MinPoradoveVRoce}–{MaxPoradoveVRoce}.");

        var rok = now.Year;
        var smlouva = projekt.CisloRamcoveSmlouvy!;
        if (await JeCisloObsazeneAsync(smlouva, rok, poradoveVRoce, ct))
            return Fail<VyzvaDetail>(VyzvaErrorCode.DuplicateVyzvaNumber,
                $"Výzva {poradoveVRoce}/{rok} už pro tuto rámcovou smlouvu existuje.");

        var vyzva = new VyzvaEntity
        {
            ProjektId = projektId,
            Kod = VyzvaCodeGenerator.Generuj(poradoveVRoce, rok),
            PoradoveVRoce = poradoveVRoce,
            Rok = rok,
            Stav = VyzvaStav.Priprava,
            DatumZalozeni = now,
            ZalozilOsobaId = zalozilOsobaId,
            MistoPlneniSnapshot = projekt.MistoPlneni!,
            CisloRamcoveSmlouvySnapshot = smlouva,
        };
        _db.Vyzvy.Add(vyzva);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (await JeCisloObsazeneAsync(smlouva, rok, poradoveVRoce, ct))
        {
            // Souběh dvou zakládajících: unique index ux_vyzvy_smlouva_rok_poradove porazí
            // druhého. Překládáme na srozumitelnou hlášku místo serverové chyby.
            return Fail<VyzvaDetail>(VyzvaErrorCode.DuplicateVyzvaNumber,
                $"Výzva {poradoveVRoce}/{rok} už pro tuto rámcovou smlouvu existuje.");
        }

        _db.VyzvaHistorieStavu.Add(new VyzvaHistorieStavuEntity
        {
            VyzvaId = vyzva.Id,
            PuvodniStav = null,
            NovyStav = VyzvaStav.Priprava,
            DatumZmeny = now,
            ZmenilOsobaId = zalozilOsobaId,
        });
        await _db.SaveChangesAsync(ct);

        var detail = await GetVyzvaAsync(vyzva.Id, ct);
        return new VyzvaResult<VyzvaDetail>.Ok(detail!);
    }

    private Task<bool> JeCisloObsazeneAsync(string cisloSmlouvy, int rok, int poradove, CancellationToken ct)
        => _db.Vyzvy.AsNoTracking().AnyAsync(
            v => v.CisloRamcoveSmlouvySnapshot == cisloSmlouvy && v.Rok == rok && v.PoradoveVRoce == poradove, ct);
}
```

- [x] **Krok 6: Doplň dotaz na obsazená čísla**

Do `VyzvaService.Queries.cs` přidej:

```csharp
    public async Task<IReadOnlyList<int>> GetObsazenaCislaAsync(int projektId, int rok, CancellationToken ct)
    {
        var cisloSmlouvy = await _db.Projekty.AsNoTracking()
            .Where(p => p.Id == projektId)
            .Select(p => p.CisloRamcoveSmlouvy)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(cisloSmlouvy)) return Array.Empty<int>();

        return await _db.Vyzvy.AsNoTracking()
            .Where(v => v.CisloRamcoveSmlouvySnapshot == cisloSmlouvy && v.Rok == rok)
            .Select(v => v.PoradoveVRoce)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }
```

- [x] **Krok 7: Odstraň automatické číslování**

Ve `VyzvaCodeGenerator.cs` smaž metodu `DalsiPoradoveVRoce`; `Generuj` ponech. Ve `VyzvaCodeGeneratorTests.cs` smaž testy, které ji volaly.

Grepni zbytky — výstup musí být prázdný:

```bash
grep -rn "ZaloztVyzvuZBufferuAsync\|DalsiPoradoveVRoce\|BufferEmpty" --include="*.cs" . | grep -v "/bin/\|/obj/"
```

- [x] **Krok 8: Uprav controller**

Ve `VyzvyController.cs` změň požadavek a akci:

```csharp
    public sealed class ZaloztRequest
    {
        public int ProjektId { get; set; }
        public int PoradoveVRoce { get; set; }
    }
```

V akci `Zalozit` nahraď volání služby za:

```csharp
        var result = await _vyzvaService.ZalozitVyzvuAsync(
            request.ProjektId, request.PoradoveVRoce, CurrentUserContext.OsobaId, now, ct);
```

- [x] **Krok 9: Spusť testy služby a controlleru**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaServiceFoundingTests|FullyQualifiedName~VyzvyControllerTests|FullyQualifiedName~VyzvaCodeGenerator"`
Očekávej: zelené. Testy controlleru, které posílaly požadavek bez čísla, doplň o `PoradoveVRoce = 1`.

- [x] **Krok 10: Doplň data pro modal do panelu**

Do `VyzvyPanelViewModel` přidej:

```csharp
    /// <summary>Obsazená pořadová čísla ve zvoleném roce — nápověda v modalu Nová výzva.</summary>
    public IReadOnlyList<int> ObsazenaCisla { get; init; } = Array.Empty<int>();

    /// <summary>Číslo rámcové smlouvy projektu — kontext pro uživatele v modalu.</summary>
    public string? CisloRamcoveSmlouvy { get; init; }
```

Ve `VyzvyPanelBuilder.BuildAsync` je naplň: `ObsazenaCisla = await _vyzvaService.GetObsazenaCislaAsync(projektId, vybranyRok, ct)` a `CisloRamcoveSmlouvy = projekt?.CisloRamcoveSmlouvy`.

Ve `BufferZalozitPodminky` smaž větev `if (buffer.Count == 0) return (false, "Buffer je prázdný.");` — prázdný buffer už zakládání neblokuje. Zbylé dvě větve (chybějící místo plnění a číslo smlouvy) ponech.

- [x] **Krok 11–12: Modal a jeho napojení — ODCHYLKA OD PLÁNU**

Plán počítal s vlastním `<gov-dialog>` vloženým do panelu a s ruční obsluhou v `panelController.js`.
Při ověření gov API (jak plán ukládal) se ukázalo, že aplikace už má vlastní zavedenou
infrastrukturu modalů, kterou by inline dialog obcházel:

`data-modal-url` na tlačítku → `openUrlModal` (bootstrap.js) → akce controlleru vrací partial
s `Layout = "_ModalLayout"` → formulář s `data-ajax-submit="true"` → `initModalAjaxSubmit`
(ajax.js) → `closeModal()` + `refreshPageScope(payload)`.

Použit tento vzor, protože zadarmo dává: vykreslení chyby u konkrétního pole (`fieldErrors`),
antiforgery filtr, focus trap, obsluhu křížku a diagnostický log. Inline dialog by to všechno
duplikoval a založil v aplikaci druhý systém modalů.

Co z toho plyne pro kód:
- `Views/Vyzvy/NovaVyzvaModal.cshtml` (ne `_VyzvyNovaModal.cshtml` v panelu), plain `<input>`
  v `.form-grid` + `_ModalFormActions` — stejně jako `AssignMeetingIdentifierModal.cshtml`.
- `VyzvyController.NovaVyzvaModal(projektId, rok)` staví `NovaVyzvaModalViewModel`; data se
  načtou při otevření modalu, takže nemohou zvětrat. Proto `ObsazenaCisla` ani
  `CisloRamcoveSmlouvy` NEJSOU na `VyzvyPanelViewModel`, jak plán navrhoval v kroku 10.
- `Zalozit` odpovídá kontraktem `ModalSubmitResultViewModel` (`AjaxSuccessResult` /
  `AjaxErrorResult`), ne vlastním `{success, reload}`. Ostatní akce panelu zůstávají
  na `postForm` kontraktu — komentář v controlleru to vysvětluje.
- Nový scope `vyzvy-panel` v `recordRefresh.js` volá `window.pmVyzvy.reloadCurrentPanel`.
  `uiContext` nese klíč dlaždice, `refreshUrl` rok — bez roku by panel po založení
  z prohlíženého staršího roku spadl na buffer.
- V railu se tlačítko renderuje jen při `Buffer.MuzeZaloztVyzvu`, jinak
  `<p data-vyzvy-zalozit-blokace>` s důvodem. Ten důvod builder počítal už dřív, ale
  žádná šablona ho nevypisovala.
- `IVyzvaService.GetCisloRamcoveSmlouvyAsync` přidán navíc oproti plánu — controller
  nemá sahat na DbContext.

**Pozor na testy:** Razor kóduje diakritiku na číselné entity (`M&#xED;sto pln&#x11B;n&#xED;`),
takže assertace na český text v HTML neprojde. Kotvi se na atributy nebo ASCII.

- [x] **Krok 13: Ověř blok**

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj` — 0 chyb, 0 varování.
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — zelené. Nahlas počty.

---

## Blok 5: Switch u PNF končí vždy v bufferu

**Cíl bloku:** Zapnutí switche „Zařadit do další výzvy" pošle PNF do bufferu, nikdy rovnou do rozpracované výzvy.

**Files:**
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvaService.Assignment.cs`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceAssignmentTests.cs`

**Interfaces:**
- Consumes: `NastavitZaradidAsync(int externiOdkazId, bool zaradit, CancellationToken ct)` — signatura se nemění, mění se chování.

- [x] **Krok 1: Napiš padající test**

Přidej do `PmTracker.Tests.Unit/Vyzvy/VyzvaServiceAssignmentTests.cs`:

```csharp
    [Fact]
    public async Task NastavitZaradid_ZapnutiPriExistujiciPripraveVyzve_KonciVBufferu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        await VyzvaServiceTestHarness.SeedZaznamAsync(db, 100);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = 10, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 4, 1), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "23106000271",
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 500, ZaznamId = 100, TypOdkazuId = 1, Cislo = "336865",
            ZaradidDoVyzvy = false, VyzvaId = null,
        });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var result = await svc.NastavitZaradidAsync(500, true, CancellationToken.None);

        result.Should().BeOfType<VyzvaResult<Unit>.Ok>();
        var ev = await db.ZaznamExterniOdkazy.FindAsync(500);
        ev!.ZaradidDoVyzvy.Should().BeTrue();
        ev.VyzvaId.Should().BeNull(
            "switch znamená jen čekání v bufferu — zařazení do výzvy je vždy vědomý přesun");
    }
```

- [x] **Krok 2: Spusť a ověř, že padá**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~NastavitZaradid_ZapnutiPriExistujiciPripraveVyzve_KonciVBufferu"`
Očekávej: FAIL — `ev.VyzvaId` je 10, protože se PNF automaticky přiřadilo do rozpracované výzvy.

- [x] **Krok 3: Odstraň automatické přiřazení**

Ve `VyzvaService.Assignment.cs` nahraď blok `if (zaradit && odkaz.VyzvaId == null) { ... } else if (!zaradit && odkaz.VyzvaId.HasValue) { ... }` za:

```csharp
        // Switch = jen „čeká v bufferu". Dřív se PNF automaticky přiřadilo do nejstarší
        // rozpracované výzvy, ale výzvy se nově zakládají prázdné a plní vědomým přesunem —
        // automatika by je plnila za zády uživatele a buffer by zůstal prázdný (spec §8.4).
        if (!zaradit && odkaz.VyzvaId.HasValue)
        {
            odkaz.VyzvaId = null;
        }
```

**Poznámka k exekuci:** Plán počítal s přidáním nového testu. Místo toho jsem přepsal
`ZaradidOn_PripravaVyzvaExistuje_Priradi` (nově `..._PrestoKonciVBufferu`, seedovaný rovnou
dvěma rozpracovanými výzvami) a smazal `ZaradidOn_DveVyzvyPriprava_PriradiNejnizsiPoradove` —
testoval výběr nejnižšího pořadového čísla, a žádný výběr už se nekoná. Přidat třetí skoro
identický test by sadu jen nafoukl.

Navíc `PmTracker.Tests.Api/Controllers/VyzvySwitchBufferTests.cs`: celá cesta
endpoint → služba → panel, ověřuje že PNF po zapnutí switche je v panelu bufferu
a rozpracovaná výzva zůstala prázdná. Červeno-zeleně ověřeno vrácením staré automatiky.

`_EditZaznamExternalPanel.cshtml` se měnit nemusel — `statusText` už pro PNF bez výzvy
psal „Čeká se (buffer projektu)"; při staré automatice to byla nepravda, teď to sedí.

- [x] **Krok 4: Ověř blok**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvaServiceAssignment"`
Očekávej: zelené. Pokud padne starší test očekávající automatické přiřazení, přepiš jeho očekávání na `VyzvaId == null` se stejným zdůvodněním.

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — zelené. Nahlas počty.

---

## Blok 6: Přesuny PNF tažením a kontextovým menu

**Cíl bloku:** PNF lze přesunout tažením na dlaždici i přes menu `⋯` u řádku. Po přesunu zůstává vybraná stejná dlaždice a pozice odscrollování — pohled se nikdy nepřepne na cílovou výzvu. Zamčené výzvy nepřijmou drop ani se nenabízejí jako cíl.

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/vyzvy/pnfMenu.js`
- Create: `PmTracker.Web/wwwroot/js/modules/vyzvy/dragDrop.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js`
- Modify: `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`
- Modify: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`
- Modify: `PmTracker.Web/wwwroot/css/components/vyzvy-panel.css`
- Modify: `PmTracker.Web/Controllers/VyzvyController.cs`
- Delete: `PmTracker.Web/Views/Vyzvy/ReassignModal.cshtml`
- Delete: `PmTracker.Web/Models/ViewModels/Vyzvy/ReassignModalViewModel.cs`
- Delete: `PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvyPanelGroupingTests.cs`

**Interfaces:**
- Consumes: `reloadPanel(panelElement, { rok?, vybrat? })` a `selectTile` z bloku 2; `POST /vyzvy/prerdit` s poli `ExterniOdkazId` a `CilovaVyzvaId`.
- Produces: na `VyzvyPanelViewModel` vlastnost `IReadOnlyList<VyzvyCilPresunuViewModel> CilePresunu` (id a kód nezamčených výzev + buffer), globální `window.pmVyzvy.presunPnf(panelElement, externiOdkazId, cilovaVyzvaId)`.

- [x] **Krok 1: Napiš padající test na nabídku cílů**

Přidej do `PmTracker.Tests.Unit/Vyzvy/VyzvyPanelGroupingTests.cs`:

```csharp
    [Fact]
    public async Task Build_CilePresunu_NeobsahujiZamceneVyzvy()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        var priprava = Vyzva(10, 1);
        var odeslana = Vyzva(11, 2);
        odeslana.Stav = VyzvaStav.Odeslano;
        var zrusena = Vyzva(12, 3);
        zrusena.Stav = VyzvaStav.Zruseno;
        db.Vyzvy.AddRange(priprava, odeslana, zrusena);
        await db.SaveChangesAsync();

        var builder = new VyzvyPanelBuilder(VyzvaServiceTestHarness.CreateService(db), db);
        var model = await builder.BuildAsync(1, muzeEditovat: true, rok: 2026, CancellationToken.None);

        model.CilePresunu.Select(c => c.VyzvaId).Should().BeEquivalentTo(new int?[] { null, 10 },
            "cílem smí být buffer a rozpracovaná výzva; odeslaná ani zrušená ne");
    }
```

- [x] **Krok 2: Spusť a ověř selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~Build_CilePresunu_NeobsahujiZamceneVyzvy"`
Očekávej: chyba překladu — `CilePresunu` neexistuje.

- [x] **Krok 3: Doplň nabídku cílů**

Do `VyzvyPanelViewModels.cs`:

```csharp
/// <summary>
/// Cíl přesunu PNF nabízený v kontextovém menu. VyzvaId == null znamená buffer.
/// Zamčené výzvy (Odesláno, Zrušeno) se do nabídky nedostanou — server by je odmítl.
/// </summary>
public sealed class VyzvyCilPresunuViewModel
{
    public int? VyzvaId { get; init; }
    public required string Popisek { get; init; }
}
```

Na `VyzvyPanelViewModel`:

```csharp
    public IReadOnlyList<VyzvyCilPresunuViewModel> CilePresunu { get; init; } = Array.Empty<VyzvyCilPresunuViewModel>();
```

Ve `VyzvyPanelBuilder.BuildAsync` po sestavení `vyzvy` doplň:

```csharp
        var cilePresunu = new List<VyzvyCilPresunuViewModel>
        {
            new() { VyzvaId = null, Popisek = "Buffer" },
        };
        cilePresunu.AddRange(vyzvy
            .Where(v => v.Stav == VyzvaStav.Priprava)
            .Select(v => new VyzvyCilPresunuViewModel { VyzvaId = v.Id, Popisek = $"Výzva {v.Kod}" }));
```

a do vraceného objektu `CilePresunu = cilePresunu,`.

- [x] **Krok 4: Spusť test a ověř, že projde**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~VyzvyPanelGroupingTests"`
Očekávej: zelené.

- [x] **Krok 5: Doplň menu a tažení do markupu**

V `_VyzvyPane.cshtml` změň `<li class="vyzvy-pnf" ...>` tak, aby řádek šel táhnout a nesl menu. Model panelu předej partialu — do `VyzvyPaneViewModel` přidej `public IReadOnlyList<VyzvyCilPresunuViewModel> CilePresunu { get; init; } = Array.Empty<VyzvyCilPresunuViewModel>();` a v obou továrních metodách ho naplň z `panel.CilePresunu`.

Řádek PNF (zamčená výzva menu ani tažení nedostane):

```razor
                    @{
                        var lzeHybat = Model.MuzeEditovat && (Model.JeBuffer || Model.Vyzva?.Stav == "Priprava");
                    }
                    <li class="vyzvy-pnf"
                        data-externi-odkaz-id="@p.ExterniOdkazId"
                        draggable="@(lzeHybat ? "true" : "false")">
                        <span class="vyzvy-pnf-cislo">@p.Cislo</span>
                        <span class="vyzvy-pnf-nazev">@(p.StrucneNazev ?? "(bez názvu z HOT)")</span>
                        <span class="vyzvy-pnf-cena">@(p.PredpokladanaCena?.ToString("N2", css) ?? "—")</span>
                        @if (lzeHybat)
                        {
                            <span class="vyzvy-pnf-menu">
                                <button type="button" class="vyzvy-pnf-dots"
                                        data-vyzvy-menu-trigger aria-label="Přesunout PNF @p.Cislo"
                                        aria-haspopup="true" aria-expanded="false">
                                    <gov-icon size="s" name="three-dots" type="components" aria-hidden="true"></gov-icon>
                                </button>
                                <div class="vyzvy-pnf-menu-panel" data-vyzvy-menu hidden>
                                    <ul>
                                        @foreach (var cil in Model.CilePresunu)
                                        {
                                            var jeAktualni = Model.JeBuffer
                                                ? cil.VyzvaId == null
                                                : cil.VyzvaId == Model.Vyzva!.Id;
                                            if (jeAktualni) { continue; }
                                            <li>
                                                <button type="button"
                                                        data-vyzvy-presun
                                                        data-externi-odkaz-id="@p.ExterniOdkazId"
                                                        data-cilova-vyzva-id="@(cil.VyzvaId?.ToString() ?? "")">
                                                    Přesunout do: @cil.Popisek
                                                </button>
                                            </li>
                                        }
                                    </ul>
                                </div>
                            </span>
                        }
                    </li>
```

**Pozor:** ověř, že ikona `three-dots` v sadě existuje (`ls PmTracker.Web/wwwroot/lib/gov-design-system/assets/icons/components/ | grep three`). Pokud ne, vyber existující a jméno oprav.

- [x] **Krok 6: Napiš modul kontextového menu**

Vytvoř `PmTracker.Web/wwwroot/js/modules/vyzvy/pnfMenu.js`. Menu jede na sdílené floating vrstvě, stejně jako menu na kartě záznamu:

```js
// pnfMenu.js — kontextové menu ⋯ u PNF řádku (přesun do bufferu / jiné výzvy).
//
// Jede na sdílené floating vrstvě (ui/floating.js): panel se mountuje do
// #floating-panel-root a pozicuje pod trigger. Outside-close vyhodnocuje PŮVOD gesta
// (mousedown), ne click — tažení z menu ven by jinak retargetovalo click na společného
// předka a menu falešně zavřelo (memory feedback_modal_close_x_only_no_backdrop).
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { closeAllFloatingPanels, mountFloatingPanel, unmountFloatingPanel } from "../ui/floating.js";

(function (global) {
  'use strict';

  function bindMenus(panelElement) {
    panelElement.addEventListener('click', function (e) {
      const trigger = e.target.closest('[data-vyzvy-menu-trigger]');
      if (trigger) {
        e.preventDefault();
        const panel = trigger.parentElement.querySelector('[data-vyzvy-menu]');
        if (!panel) return;
        const open = trigger.getAttribute('aria-expanded') === 'true';
        closeAllFloatingPanels(document);
        if (open) { unmountFloatingPanel(panel); trigger.setAttribute('aria-expanded', 'false'); return; }
        panel.hidden = false;
        mountFloatingPanel(panel, trigger);
        trigger.setAttribute('aria-expanded', 'true');
        return;
      }

      const presun = e.target.closest('[data-vyzvy-presun]');
      if (presun) {
        e.preventDefault();
        closeAllFloatingPanels(document);
        const cil = presun.dataset.cilovaVyzvaId;
        global.pmVyzvy.presunPnf(
          panelElement,
          presun.dataset.externiOdkazId,
          cil === '' ? null : cil);
      }
    });
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bindPnfMenu = bindMenus;
})(window);
```

**Pozor:** ověř skutečné signatury `mountFloatingPanel` / `unmountFloatingPanel` / `closeAllFloatingPanels` v `modules/ui/floating.js` a použij je přesně; vzor převezmi z `modules/recordActionsMenu.js`.

- [x] **Krok 7: Napiš modul tažení**

Vytvoř `PmTracker.Web/wwwroot/js/modules/vyzvy/dragDrop.js`:

```js
// dragDrop.js — tažení PNF řádku na dlaždici v railu. Native HTML5 DnD, bez knihovny.
// Drop přijímají jen dlaždice s data-vyzvy-drop-target != "none" (buffer a rozpracované výzvy).
(function (global) {
  'use strict';

  const DATA_TYPE = 'text/x-pmtracker-pnf';

  function bindDragDrop(panelElement) {
    panelElement.addEventListener('dragstart', function (e) {
      const row = e.target.closest('.vyzvy-pnf[draggable="true"]');
      if (!row) return;
      e.dataTransfer.setData(DATA_TYPE, row.dataset.externiOdkazId);
      e.dataTransfer.effectAllowed = 'move';
    });

    panelElement.addEventListener('dragover', function (e) {
      const tile = e.target.closest('[data-vyzvy-drop-target]');
      if (!tile || tile.dataset.vyzvyDropTarget === 'none') return;
      e.preventDefault();
      e.dataTransfer.dropEffect = 'move';
      tile.classList.add('is-drag-over');
    });

    panelElement.addEventListener('dragleave', function (e) {
      const tile = e.target.closest('[data-vyzvy-drop-target]');
      if (tile) tile.classList.remove('is-drag-over');
    });

    panelElement.addEventListener('drop', function (e) {
      const tile = e.target.closest('[data-vyzvy-drop-target]');
      if (!tile || tile.dataset.vyzvyDropTarget === 'none') return;
      e.preventDefault();
      tile.classList.remove('is-drag-over');
      const id = e.dataTransfer.getData(DATA_TYPE);
      if (!id) return;
      global.pmVyzvy.presunPnf(panelElement, id, tile.dataset.vyzvaId || null);
    });
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bindDragDrop = bindDragDrop;
})(window);
```

- [x] **Krok 8: Doplň přesun a zachování pohledu**

Do `panelController.js` přidej:

```js
  // Po přesunu se pohled NESMÍ přepnout na cílovou výzvu — uživatel zůstává tam,
  // kde je, jen se aktualizuje seznam a počty (spec §8.2).
  async function presunPnf(panelElement, externiOdkazId, cilovaVyzvaId) {
    const zustatNa = panelElement.dataset.vybranaDlazdice || 'buffer';
    const result = await postForm('/vyzvy/prerdit', {
      ExterniOdkazId: externiOdkazId,
      CilovaVyzvaId: cilovaVyzvaId,
    });
    if (!result.success) {
      showToast(result.message || 'Přesun PNF selhal.', true);
      return;
    }
    await reloadPanel(panelElement, { vybrat: zustatNa });
  }
```

a vystav ji: `global.pmVyzvy.presunPnf = presunPnf;`

V `index.js` do `bootstrap(panelElement)` doplň volání `global.pmVyzvy.bindPnfMenu(panelElement)` a `global.pmVyzvy.bindDragDrop(panelElement)`.

- [x] **Krok 9: Zaregistruj moduly v bootstrapu**

V `PmTracker.Web/wwwroot/js/modules/bootstrap.js` za řádek `import "./vyzvy/switchController.js";` přidej:

```js
import "./vyzvy/pnfMenu.js";                        // 2026-09-07 — ⋯ menu přesunu PNF (sdílená floating vrstva).
import "./vyzvy/dragDrop.js";                       // 2026-09-07 — tažení PNF na dlaždice railu.
```

Bez tohoto kroku se moduly nikdy nenačtou a menu ani tažení nebude fungovat.

- [x] **Krok 10: Smaž mrtvý modal přiřazení**

Modal „Upravit přiřazení" je dnes v prohlížeči **mrtvý kód** — `reassignModal.js` není importovaný z `bootstrap.js`, takže tlačítko nikdy nic neudělalo. Smaž:

- `PmTracker.Web/Views/Vyzvy/ReassignModal.cshtml`
- `PmTracker.Web/Models/ViewModels/Vyzvy/ReassignModalViewModel.cs`
- `PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js`
- akci `ReassignModal` ve `VyzvyController.cs` (endpoint `GET /vyzvy/reassign-modal`)
- testy této akce ve `VyzvyControllerTests.cs`

Ověř, že nic nezůstalo:

```bash
grep -rn "ReassignModal\|reassign-modal\|otevrit-reassign" --include="*.cs" --include="*.cshtml" --include="*.js" PmTracker.Web PmTracker.Tests.Unit | grep -v "/bin/\|/obj/"
```

- [x] **Krok 11: Styly menu**

Do `vyzvy-panel.css` přidej:

```css
.vyzvy-pnf-menu { position: relative; }
.vyzvy-pnf-dots { border: 0; background: transparent; cursor: pointer; padding: 0.2rem 0.4rem; line-height: 1; }
.vyzvy-pnf-menu-panel { min-width: 220px; background: var(--pm-surface); border: 1px solid var(--pm-border);
                        border-radius: 6px; box-shadow: 0 4px 14px rgba(0,0,0,.18); padding: 0.25rem; }
.vyzvy-pnf-menu-panel ul { list-style: none; margin: 0; padding: 0; }
.vyzvy-pnf-menu-panel button { display: block; width: 100%; text-align: left; border: 0;
                               background: transparent; padding: 0.4rem 0.6rem; cursor: pointer; }
.vyzvy-pnf-menu-panel button:hover { background: var(--pm-surface-hover, rgba(0,0,0,.06)); }
.vyzvy-pnf[draggable="true"] { cursor: grab; }
```

**Poznámky k exekuci:**

- **Ikona:** `three-dots` v sadě nebyla (plán to nechal ověřit). Stáhl jsem
  `three-dots-vertical.svg` z Bootstrap Icons 1.11.3, odkud pochází zbytek
  `wwwroot/assets/icons/components/` (memory feedback_gov_icons_are_bootstrap).
- **pnfMenu.js:** plán navrhoval per-panel IIFE bez mousedown guardu a bez Escape.
  Napsáno místo toho jako dokumentový ESM modul podle `recordActionsMenu.js` — spec §8.1
  chce guardy zdarma ze sdílené vrstvy, ne vlastní poloviční menu. Panel Výzev se bere
  z triggeru, ne z menu panelu: ten je namountovaný v `#floating-panel-root`.
- **dragDrop.js:** posluchače na dokumentu, ne na panelu — panel se po každé akci nahrazuje
  novým elementem. Navíc kontrola `dataTransfer.types`, aby se dlaždice nezvýrazňovala
  při tažení textu odjinud.
- **Testy:** `VyzvyPresunJsTests` (piny zdroje JS, vzor `RecordScheduleToggleJsTests`)
  a `VyzvyPresunTests` (Api markup: tažení a menu jen tam, kde je přesun proveditelný).
  Zamykací větve červeno-zeleně ověřeny.
- **Past ve fixture:** `ApiSqlFixture.EnsureRecordAsync` navzdory názvu zakládá pokaždé nový
  záznam a databáze je sdílená mezi testy → seed si existující záznam hledá sám, jinak
  padá unique index `ux_zaznam_externi_odkazy_cislo_in_vyzve`. Výřezy panelu z HTML jsou
  ve sdíleném `TestInfrastructure/VyzvyPanelHtml.cs`.

- [x] **Krok 12: Ověř blok**

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj` — 0 chyb, 0 varování.
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj` — zelené.
Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj` — zelené kromě 4 známých gantt selhání.

Ruční kontrola uživatelem: přetažení PNF na dlaždici i přesun přes `⋯` funguje, obrazovka neuskočí, zamčená výzva drop nepřijme.

---

## Blok 7: Tisk výzvy do Wordu a PDF

**Cíl bloku:** Tlačítko „Tisk výzvy" otevře standardní chooser formátu a vygeneruje výzvu
podle resortního formuláře — hlavička, tabulka požadavků, kapitola s kalkulací za každé
PNF, součty s DPH a podpisová doložka. Spec §9.

Rozsah je velký, proto rozdělený na čtyři samostatně testovatelné části. Po každé
proběhne build + testy, teprve pak se pokračuje.

**Pořadí se při exekuci obrátilo na 7B → 7C → 7D → 7A.** Tlačítko staví URL přes
`Url.Action("VyzvaTisk", "Export", …)`, což na neexistující akci vrátí `null` — 7A tedy
nejde dokončit ani ověřit dřív, než endpointy existují. Plán měl obrácenou závislost.

**Global:** commity držené; build musí končit 0 chyb / 0 varování; známá selhání
(4 Api gantt) se hlásí, neopravují.

---

**Poznámky k exekuci (dokončeno 2026-09-07):**

- Pořadí bylo 7B → 7C → 7D → 7A, viz důvod výše.
- **Test PDF nesmí číst tělo odpovědi.** `FakePdfRenderer` vrací podvržené PDF, ne HTML;
  obsah šablony se bere z `Factory.Services.GetRequiredService<FakePdfRenderer>().LastHtml`
  (vzor z `ExportControllerTests`). První verze testu na to najela.
- **Pojmenované argumenty:** `BuildAsync(..., rok: null, ct)` po přidání parametru přestane
  jít přeložit (CS8323) — poslední argument musí být taky pojmenovaný (`ct: ct`).
- Přidání parametru rozbilo i **dva stuby `IVyzvyPanelBuilder`** v testech controlleru
  (`ProjektyControllerBehaviorTests`, `ProjektyControllerTeamAuthzTests`) — plán počítal
  jen s voláními builderu.
- Anti-spoof guard ověřen červeno-zeleně: po odstranění řádku
  `projektId != 0 && projektId != model.ProjektId` spadly oba testy cizího projektu.
- CSS výzvy je v `pdf-export.css` jako blok `.vyzva-*`; Word používá vlastní OpenXML tabulky.

### 7A — Tlačítko a oprávnění

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Modify: `PmTracker.Web/Services/Vyzvy/VyzvyPanelBuilder.cs`
- Modify: `PmTracker.Web/Controllers/ProjektyController.TabPartials.cs`
- Modify: `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`
- Modify: `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs`
- Test: `PmTracker.Tests.Api/Controllers/VyzvyTiskTests.cs`

- [x] **Krok 1: Api test na tlačítko (RED)**

Vytvoř `PmTracker.Tests.Api/Controllers/VyzvyTiskTests.cs`. Výřezy panelu ber ze sdíleného
`VyzvyPanelHtml` (blok 6). Seed musí být idempotentní — fixture sdílí databázi mezi testy
a `ux_vyzvy_smlouva_rok_poradove` je globální.

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Tisk výzvy (spec 2026-09-07-vyzvy-dokonceni-design §9). Tlačítko jede na sdíleném
/// chooseru formátu, takže nese obě URL; buffer se netiskne.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyTiskTests
{
    private readonly ApiSqlFixture _fixture;

    public VyzvyTiskTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int VyzvaId)> SeedAsync()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiVyzvyTiskOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIVYZTISK");

        await using var dbContext = _fixture.CreateDbContext();
        var projekt = await dbContext.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = "APIVYZ-SML-TISK";
        await dbContext.SaveChangesAsync();

        var existing = await dbContext.Vyzvy
            .FirstOrDefaultAsync(v => v.ProjektId == projectId && v.Rok == 2026 && v.PoradoveVRoce == 701);
        if (existing is not null) return (projectId, existing.Id);

        var vyzva = new VyzvaEntity
        {
            ProjektId = projectId, Kod = "701/2026", PoradoveVRoce = 701, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 2, 1), ZalozilOsobaId = ownerId,
            MistoPlneniSnapshot = "FIS (EIS): VZ 8201", CisloRamcoveSmlouvySnapshot = "APIVYZ-SML-TISK",
        };
        dbContext.Vyzvy.Add(vyzva);
        await dbContext.SaveChangesAsync();
        return (projectId, vyzva.Id);
    }

    private async Task<string> LoadPanelAsync(int projectId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        return await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?rok=2026&asUser={_fixture.AdminOsobaId}");
    }

    [Fact]
    public async Task PanelVyzvy_NabidneTiskPresSdilenyChooser()
    {
        var (projectId, vyzvaId) = await SeedAsync();
        var pane = VyzvyPanelHtml.VyzvaPane(await LoadPanelAsync(projectId), vyzvaId);

        pane.Should().Contain("data-print-trigger", "tisk jede na sdíleném chooseru formátu");
        pane.Should().Contain($"/Export/Vyzva/{vyzvaId}/Tisk", "chooser potřebuje PDF URL");
        pane.Should().Contain($"/Export/Vyzva/{vyzvaId}/Word", "a Word URL");
        pane.Should().Contain($"projektId={projectId}",
            "bez projektId by project-scoped policy udělala tichý globální check");
    }

    [Fact]
    public async Task BufferNemaTisk()
    {
        var (projectId, _) = await SeedAsync();
        var buffer = VyzvyPanelHtml.BufferPane(await LoadPanelAsync(projectId));

        buffer.Should().NotContain("data-print-trigger", "buffer není dokument, nemá co tisknout");
    }
}
```

Ověřeno při přípravě plánu: tento test **selže** dokud tlačítko neexistuje, druhý projde
triviálně (negativní tvrzení).

- [x] **Krok 2: `MuzeTisknout` do modelu**

Na `VyzvyPanelViewModel` i `VyzvyPaneViewModel` přidej `bool MuzeTisknout`.
`BuildAsync` dostane parametr `bool muzeTisknout` **před `int? rok`**:

```csharp
    Task<VyzvyPanelViewModel> BuildAsync(
        int projektId, bool muzeEditovat, bool muzeTisknout, int? rok, CancellationToken ct);
```

**Rozbije to pět volání v testech** (`VyzvyPanelGroupingTests` × 5, `VyzvyPanelBuilderTests` × 6).
Uprav je na pojmenované argumenty a ověř grepem, že žádné volání se čtyřmi argumenty nezůstalo:

```bash
grep -rn "BuildAsync(1, muzeEditovat" --include="*.cs" PmTracker.Tests.Unit | grep -v muzeTisknout
```

- [x] **Krok 3: Controller předá oprávnění**

Ve `VyzvyTabPartial` přidej
`var muzeTisknout = CurrentUserContext.HasPermission(PermissionKeys.VyzvyWordExport, id);`
a předej do builderu.

- [x] **Krok 4: Tlačítko v hlavičce panelu**

V `_VyzvyPane.cshtml` do `<header>` (jen když `!Model.JeBuffer && Model.MuzeTisknout`):

```razor
<pm-button variant="Secondary" size="Small"
           data-print-trigger="true"
           data-print-pdf-url="@Url.Action("VyzvaTisk", "Export", new { vyzvaId = Model.Vyzva!.Id, projektId = Model.ProjektId })"
           data-print-word-url="@Url.Action("VyzvaWord", "Export", new { vyzvaId = Model.Vyzva!.Id, projektId = Model.ProjektId })"
           data-print-label="Tisk výzvy @Model.Vyzva!.Kod">Tisk výzvy</pm-button>
```

`VyzvyPaneViewModel` k tomu potřebuje `ProjektId` — doplň ho a plň z panelu.

- [x] **Krok 5: Popisek klíče**

V `SecurityViewModels.cs` uprav popis `VyzvyWordExport` — klíč nově kryje Word i PDF.

- [x] **Krok 6: Ověř**

Build 0/0, `dotnet test PmTracker.Tests.Unit` zelené, `VyzvyTiskTests` zelené.

---

### 7B — Projekce výzvy pro export

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaExportViewModels.cs`
- Create: `PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs`
- Modify: `PmTracker.ServiceDesk.Contracts/Contracts/HotZaznamDto.cs`
- Modify: `PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs`

**Produces:** `IVyzvaExportBuilder.BuildAsync(int vyzvaId, CancellationToken ct)`
→ `VyzvaExportViewModel?` (null = výzva neexistuje).

Model podle spec §9.3–9.5: hlavička, `IReadOnlyList<VyzvaExportPozadavekViewModel>`
(PoradoveOznaceni `a`/`b`/…, CisloUkoluVp, Nazev, CisloHtl, Popis, VazbaPmp,
`VyzvaExportKalkulaceViewModel?`), souhrny.

Kalkulace: řádky A–D napevno (Analýza / Programové úpravy / Testování / Implementace),
`cena_* ` z DB je bez DPH, DPH 21 % dopočítat. Sazba DPH jako konstanta na jednom místě.

- [x] **Krok 1: Testy projekce (RED)**

Vytvoř `PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs`. `FakeTicketing` je potřeba
vlastní — stub v `VyzvaServiceTestHarness` vrací jen prázdno a kalkulace se z něj nedají nastavit.

```csharp
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Projekce výzvy pro tisk (spec 2026-09-07 §9.3–9.5). Word i PDF staví z tohoto modelu,
/// takže co se ověří tady, platí pro oba formáty.
/// </summary>
public sealed class VyzvaExportBuilderTests
{
    private const int VyzvaId = 10;

    /// <summary>Ticketing, který vrací připravené HOT popisy a kalkulace podle čísla PNF.</summary>
    private sealed class FakeTicketing : ITicketingQueryService
    {
        public Dictionary<string, HotZaznamDto> Zaznamy { get; } = new();
        public Dictionary<string, HotKalkulaceDto> Kalkulace { get; } = new();

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult(Zaznamy.GetValueOrDefault(cislo));

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotZaznamDto>>(
                cisla.Where(Zaznamy.ContainsKey).ToDictionary(c => c, c => Zaznamy[c]));

        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string pid, CancellationToken ct)
            => Task.FromResult(Kalkulace.GetValueOrDefault(pid));

        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> pidy, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(
                pidy.Where(Kalkulace.ContainsKey).ToDictionary(p => p, p => Kalkulace[p]));
    }

    private static HotKalkulaceDto Kalkulace(string pid) => new(
        1, pid, Verze: 2,
        PracnostAnalyza: 2m, SazbaAnalyza: 100m, CenaAnalyza: 200m,
        PracnostProgramovani: 3m, SazbaProgramovani: 200m, CenaProgramovani: 600m,
        PracnostTestovani: 1m, SazbaTestovani: 50m, CenaTestovani: 50m,
        PracnostImplementace: 1m, SazbaImplementace: 150m, CenaImplementace: 150m,
        CenaCelkem: 1000m,
        PocetLicenci: null, SazbaLicence: null, CenaLicence: null, RozpadLicence: null,
        Termin: null, TextTermin: null);

    private static async Task<PmTracker.Web.Data.PmTrackerDbContext> SeedAsync(
        params (int ZaznamId, string Cislo)[] pnf)
    {
        var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);

        db.Vyzvy.Add(new VyzvaEntity
        {
            Id = VyzvaId, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
            Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 2, 6), ZalozilOsobaId = 1,
            MistoPlneniSnapshot = "FIS (EIS): VZ 8201", CisloRamcoveSmlouvySnapshot = "23106000271",
        });

        var id = 500;
        foreach (var (zaznamId, cislo) in pnf)
        {
            await VyzvaServiceTestHarness.SeedZaznamAsync(db, zaznamId);
            db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
            {
                Id = id++, ZaznamId = zaznamId, TypOdkazuId = 1, Cislo = cislo,
                ZaradidDoVyzvy = true, VyzvaId = VyzvaId,
            });
        }
        await db.SaveChangesAsync();
        return db;
    }

    private static VyzvaExportBuilder Builder(
        PmTracker.Web.Data.PmTrackerDbContext db, ITicketingQueryService ticketing)
        => new(db, ticketing);

    [Fact]
    public async Task Build_HlavickaZeSnapshotuVyzvy()
    {
        using var db = await SeedAsync((100, "336865"));

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model.Should().NotBeNull();
        model!.KodVyzvy.Should().Be("2/2026");
        model.CisloRamcoveSmlouvy.Should().Be("23106000271");
        model.MistoPlneni.Should().Be("FIS (EIS): VZ 8201");
        model.InformacniSystem.Should().Be("FIS", "nadpis nese zkratku IS z místa plnění");
    }

    [Fact]
    public async Task Build_PozadavkyMajiPoradovaPismenaAUdajeZeZaznamu()
    {
        using var db = await SeedAsync((100, "336865"), (200, "341837"));

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model!.Pozadavky.Select(p => p.PoradoveOznaceni).Should().Equal(new[] { "a", "b" });
        var prvni = model.Pozadavky[0];
        prvni.CisloUkoluVp.Should().Be("RU100", "Č. úkolu VP je viditelné číslo záznamu");
        prvni.Nazev.Should().Be("test");
        prvni.CisloHtl.Should().Be("336865");
    }

    [Fact]
    public async Task Build_KalkulaceMaCtyriRadkyAPocitaDph()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "strucne", "popis", Pid: "A490P00ECYL1");
        ticketing.Kalkulace["A490P00ECYL1"] = Kalkulace("A490P00ECYL1");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        var k = model!.Pozadavky[0].Kalkulace;
        k.Should().NotBeNull();
        k!.Radky.Select(r => r.Kod).Should().Equal(new[] { "A", "B", "C", "D" });
        k.Radky.Select(r => r.Nazev).Should().Equal(
            new[] { "Analýza", "Programové úpravy", "Testování", "Implementace" });
        k.Radky[1].Rozsah.Should().Be(3m);
        k.Radky[1].Sazba.Should().Be(200m);
        k.Radky[1].CenaBezDph.Should().Be(600m);

        k.CelkemBezDph.Should().Be(1000m, "součet cen A–D, v databázi jsou bez DPH");
        k.CelkemDph.Should().Be(210m, "DPH 21 %");
        k.CelkemSDph.Should().Be(1210m);
    }

    [Fact]
    public async Task Build_PnfBezKalkulace_MaPrazdneRadkyNeNull()
    {
        using var db = await SeedAsync((100, "336865"));

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        var k = model!.Pozadavky[0].Kalkulace;
        k.Should().NotBeNull("tabulka se tiskne i bez dat, jen prázdná");
        k!.Radky.Should().HaveCount(4);
        k.Radky.Should().OnlyContain(r => r.Rozsah == null && r.CenaBezDph == null);
        k.CelkemBezDph.Should().Be(0m);
    }

    [Fact]
    public async Task Build_PopisAVazbaNaPmpZeZaznamu()
    {
        using var db = await SeedAsync((100, "336865"));
        db.CiselnikTypuExternichOdkazu.Add(
            new CiselnikTypuExternichOdkazuEntity { Id = 2, Kod = "PMP", Nazev = "PMP" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 900, ZaznamId = 100, TypOdkazuId = 2, Cislo = "335730",
        });
        await db.SaveChangesAsync();

        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "strucne", "Popis pozadavku.");

        var model = await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None);

        model!.Pozadavky[0].Popis.Should().Be("Popis pozadavku.");
        model.Pozadavky[0].VazbaPmp.Should().Be("335730", "kapitola uvádí vazbu na PMP");
    }

    [Fact]
    public async Task Build_PrazdnaVyzva_SePorenderujeBezPozadavku()
    {
        using var db = await SeedAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(VyzvaId, CancellationToken.None);

        model.Should().NotBeNull("prázdná výzva se tiskne (spec §9.7)");
        model!.Pozadavky.Should().BeEmpty();
        model.CelkemBezDph.Should().Be(0m);
    }

    [Fact]
    public async Task Build_NeexistujiciVyzva_VraciNull()
    {
        using var db = await SeedAsync();

        var model = await Builder(db, new FakeTicketing()).BuildAsync(vyzvaId: 999, CancellationToken.None);

        model.Should().BeNull();
    }
}
```

Ověřeno při přípravě plánu: soubor **neprojde překladem**, dokud `VyzvaExportBuilder`
neexistuje — to je očekávaný RED.
- [x] **Krok 2: `Pid` na `HotZaznamDto`** + naplnění v `SqlTicketingQueryService`.
  Pozor: `HotZaznamDto` je poziční record — nový parametr přidej **na konec s default null**,
  ať se nerozbijí existující volání.
- [x] **Krok 3: Model** — `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaExportViewModels.cs`

Tvar je závazný, 7C i 7D z něj čtou:

```csharp
namespace PmTracker.Web.Models.ViewModels.Vyzvy;

/// <summary>
/// Výzva pro tisk (spec 2026-09-07 §9). Word i PDF staví z tohoto modelu, aby se obsah
/// obou formátů nemohl rozejít. Ceny jsou bez DPH tak, jak přicházejí z HOT_KALKULACE;
/// DPH dopočítává projekce sazbou DphSazba.
/// </summary>
public sealed class VyzvaExportViewModel
{
    public const decimal DphSazba = 0.21m;

    public int VyzvaId { get; init; }
    public int ProjektId { get; init; }
    public required string KodVyzvy { get; init; }
    public int PoradoveVRoce { get; init; }
    public int Rok { get; init; }
    public required string CisloRamcoveSmlouvy { get; init; }
    public required string MistoPlneni { get; init; }

    /// <summary>Zkratka IS z místa plnění — „FIS (EIS): VZ 8201" dá „FIS".</summary>
    public required string InformacniSystem { get; init; }

    public IReadOnlyList<VyzvaExportPozadavekViewModel> Pozadavky { get; init; }
        = Array.Empty<VyzvaExportPozadavekViewModel>();

    public decimal CelkemBezDph { get; init; }
    public decimal CelkemDph { get; init; }
    public decimal CelkemSDph { get; init; }
    public decimal LicenceBezDph { get; init; }
    public decimal LicenceDph { get; init; }
    public decimal LicenceSDph { get; init; }
}

public sealed class VyzvaExportPozadavekViewModel
{
    /// <summary>Poř. č. v tabulce požadavků: a, b, c, … (po „z" pokračuje aa, ab).</summary>
    public required string PoradoveOznaceni { get; init; }
    public int ZaznamId { get; init; }
    public string? CisloUkoluVp { get; init; }
    public string? Nazev { get; init; }
    public required string CisloHtl { get; init; }
    public string? Popis { get; init; }
    public string? VazbaPmp { get; init; }

    /// <summary>Nikdy null — bez dat se tiskne prázdná tabulka se čtyřmi řádky.</summary>
    public required VyzvaExportKalkulaceViewModel Kalkulace { get; init; }
}

public sealed class VyzvaExportKalkulaceViewModel
{
    public IReadOnlyList<VyzvaExportKalkulaceRadekViewModel> Radky { get; init; }
        = Array.Empty<VyzvaExportKalkulaceRadekViewModel>();
    public decimal CelkemBezDph { get; init; }
    public decimal CelkemDph { get; init; }
    public decimal CelkemSDph { get; init; }
    public int? PocetLicenci { get; init; }
    public decimal? SazbaLicence { get; init; }
    public decimal? CenaLicence { get; init; }
}

public sealed class VyzvaExportKalkulaceRadekViewModel
{
    public required string Kod { get; init; }      // A / B / C / D
    public required string Nazev { get; init; }    // Analýza / Programové úpravy / Testování / Implementace
    public decimal? Rozsah { get; init; }          // hodiny
    public decimal? Sazba { get; init; }           // Kč/hod
    public decimal? CenaBezDph { get; init; }
    public decimal? CenaDph { get; init; }
    public decimal? CenaSDph { get; init; }
}
```

- [x] **Krok 4: Builder** — `VyzvaExportBuilder(PmTrackerDbContext db, ITicketingQueryService ticketing)`
  s `IVyzvaExportBuilder`. Postup: načti výzvu (null → vrať null), její PNF vazby, záznamy,
  seřaď sdíleným `RecordDisplayOrdering`, přiděl pořadová písmena, dotáhni HOT popisy
  (`GetZaznamyAsync`), z nich `Pid` a přes něj kalkulace (`GetAkceptovaneKalkulaceAsync`),
  PMP vazby ze stejných záznamů. Registruj v `VyzvyServiceCollectionExtensions`.
- [x] **Krok 5: Ověř** — build 0/0, unit zelené.

---

### 7C — PDF

**Files:**
- Create: `PmTracker.Web/Views/Export/VyzvaTemplate.cshtml`
- Modify: `PmTracker.Web/Controllers/ExportController.cs`
- Modify: `PmTracker.Web/wwwroot/css/pdf-export.css`
- Test: `PmTracker.Tests.Api/Controllers/VyzvyTiskTests.cs`

- [x] **Krok 1: Api testy (RED)** — `/Export/Vyzva/{id}/Tisk?projektId=` vrací 200,
  obsahuje číslo PNF a kód výzvy; cizí `projektId` vrací `NotFound` (anti-spoof);
  neexistující výzva `NotFound`.
- [x] **Krok 2: Razor šablona** podle struktury spec §9.3, placeholdery `X` dle §9.6.
- [x] **Krok 3: Akce `VyzvaTisk`** — vzor `JednaniTisk` včetně anti-spoof guardu
  a `EnsureProjectReadableAsync`; policy `permission:vyzvy.word.export`.
- [x] **Krok 4: Ověř** — build 0/0, Api zelené kromě 4 známých.

---

### 7D — Word

**Files:**
- Create: `PmTracker.Web/Services/Export/OpenXmlVyzvaExportService.cs`
- Modify: `PmTracker.Web/Controllers/ExportController.cs`
- Test: `PmTracker.Tests.Api/Controllers/VyzvyTiskTests.cs`

- [x] **Krok 1: Api test (RED)** — `/Export/Vyzva/{id}/Word` vrací 200 s content-type
  `application/vnd.openxmlformats-officedocument.wordprocessingml.document`, tělo jde otevřít
  jako `WordprocessingDocument` a obsahuje číslo PNF i kód výzvy.
- [x] **Krok 2: `IVyzvaWordExportService.BuildDocument(VyzvaExportViewModel)`** —
  OpenXML, pomocné prvky z `OpenXmlWordElements`.
- [x] **Krok 3: Akce `VyzvaWord`** — vzor `JednaniWord`.
- [x] **Krok 4: Ověř** — build 0/0, Unit + Api zelené kromě 4 známých.

---

## Blok 8: Verze 0.9 a závěrečné ověření

**Cíl bloku:** Aplikace hlásí verzi 0.9 (pilotní před 1.0) a celá sada testů je ověřená.

**Files:**
- Modify: `PmTracker.Web/PmTracker.Web.csproj:6`

**Výsledek ověření 2026-09-07 (před bumpem):** Unit 1663/1663, Api 345/349
(4 známé gantt), Integration 85/86 (1 známé `proposals.accept`). Build 0 chyb / 0 varování.
Verze 0.9 ověřena v `PmTracker.Web.dll` (InformationalVersion).

- [x] **Krok 1: Ověř celek PŘED bumpem**

Verze 0.9 má označovat hotový a ověřený celek, takže se povyšuje až po zelené sadě.

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj`
Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj`
Spusť: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj` (vyžaduje běžící Docker/Colima; pokud neběží, nahlas to a nepředstírej výsledek)

Očekávej: Unit zelené, Api zelené kromě 4 známých gantt selhání, Integration zelené kromě 1 známého `proposals.accept`. **Jakékoli jiné selhání znamená, že blok není hotový** — oprav ho, nebump verzi.

- [x] **Krok 2: Povyš verzi**

V `PmTracker.Web/PmTracker.Web.csproj` na řádku 6 změň:

```xml
    <AppVersion>0.9</AppVersion>
```

`Version` i `InformationalVersion` se z `AppVersion` odvozují, takže jiné místo měnit netřeba.

- [x] **Krok 3: Ověř, že se verze projeví**

Spusť: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ApplicationVersionFormatterTests"`
Očekávej: zelené. Pokud některý test verzi pinuje na 0.8, uprav ho na 0.9.

Ruční kontrola uživatelem: patička aplikace hlásí „Verze 0.9".

- [x] **Krok 4: Předání**

Shrň uživateli, co je hotové po blocích, které sady testů běžely a s jakým výsledkem, a která dřívější selhání zůstala. **Commity zůstávají nezacommitované** — uživatel je udělá po ruční verifikaci.

Připomeň dvě věci, které zůstávají otevřené a nejsou součástí tohoto plánu:

1. **Formulář tisku výzvy** — upřesní se samostatně jako poslední krok tématu. Prázdná výzva je nově legální stav, takže generování bude muset rozhodnout, jestli ji odmítne, nebo vytiskne s prázdnou tabulkou požadavků.
2. **Wiki stránka Výzvy** (`docs/wiki/projekty/projektovy-dashboard/vyzvy/`) popisuje neexistující funkce a navíc leží v adresáři dashboardu, odkud se Výzvy odstěhovaly. Potřebuje přepsat a přesunout.
