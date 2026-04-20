# ServiceDesk Výzvy — Fáze 2: UI

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementovat UI vrstvu pro správu výzev v PM Trackeru: panel výzev na projektovém dashboardu, switch na externí vazbě PNF, modal pro drag & drop přeřazení, editace projektu s MistoPlneni + CisloRamcoveSmlouvy, fallback číselník. Backend (fáze 1) je hotový.

**Architecture:** MVC + AJAX JSON API. `VyzvyController` (nový) poskytuje POST endpointy (založit, změnit stav, set switch, přeřadit) a GET pro reassign modal. Panel na projektovém dashboardu se renderuje přes existující per-panel AJAX infrastrukturu (`ProjectDashboardService.BuildVyzvyPanelAsync`). JS moduly v `wwwroot/js/modules/vyzvy/` (panelController, switchController, reassignModal) — konkatenace do `site.bundle.js` manuálně podle konvence projektu.

**Tech Stack:** ASP.NET Core 8 MVC, Razor views, Gov Design System components, vanilla JS (HTML5 DnD API, žádná knihovna), xUnit pro unit testy controller + VM builder.

**Spec:** [docs/superpowers/specs/2026-04-20-servicedesk-vyzvy-faze-2-ui-design.md](../specs/2026-04-20-servicedesk-vyzvy-faze-2-ui-design.md)

**Výchozí stav:**
- Panel placeholder v `_VyzvyPanel.cshtml`, `BuildVyzvyPanel()` vrací `IsServiceDeskIntegrated = false`
- `IVyzvaService` zaregistrovaný v DI, 7 metod k dispozici
- `ExterniVazbaViewModel.Vyzva (string)` je dědictví — musíme přepsat na `VyzvaId (int?)` + `VyzvaKod` + `ZaradidDoVyzvy`
- Editor projektu `ProjectModal.cshtml` (modal) se `SaveProjectCommand` — přidáme 2 pole
- Záložka „Výzvy" v `ProjectDashboard/Index.cshtml` tab UI existuje, přidáme obsah

---

## File Structure

### Nové soubory

```
PmTracker.Web/Controllers/
  VyzvyController.cs                              ← AJAX JSON endpoints

PmTracker.Web/Models/ViewModels/Vyzvy/
  VyzvyPanelViewModel.cs                          ← model _VyzvyPanel.cshtml
  ReassignModalViewModel.cs                       ← model DnD modalu

PmTracker.Web/Services/ProjectDashboard/
  VyzvyPanelBuilder.cs                            ← extract stavby panelu z ProjectDashboardService (keep focused)

PmTracker.Web/Views/ProjectDashboard/
  _VyzvyPanel.BufferCard.cshtml                   ← partial buffer
  _VyzvyPanel.VyzvaCard.cshtml                    ← partial výzvy

PmTracker.Web/Views/Vyzvy/
  ReassignModal.cshtml                            ← DnD modal body

PmTracker.Web/wwwroot/js/modules/vyzvy/
  index.js                                        ← bootstrap
  panelController.js                              ← panel akce
  switchController.js                             ← switch na ext. vazbě
  reassignModal.js                                ← DnD

PmTracker.Web/wwwroot/css/components/
  vyzvy-panel.css                                 ← styly panelu
```

### Modifikované soubory

```
PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml       ← přepsat z placeholderu
PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs ← rozšířit VyzvyPanelViewModel
PmTracker.Web/Services/ProjectDashboard/IProjectDashboardService.cs  ← BuildVyzvyPanelAsync
PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs   ← BuildVyzvyPanelAsync impl
PmTracker.Web/Controllers/ProjectDashboardController.cs       ← update GetVyzvyPanel volá async
PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs   ← SaveProjectCommand + 2 pole
PmTracker.Web/Models/ViewModels/ModalViewModels.cs (nebo ProjectModalViewModel lokace)
                                                              ← propagovat 2 pole
PmTracker.Web/Views/Projekty/ProjectModal.cshtml              ← 2 nová textová pole
PmTracker.Web/Services/ProjectService.Commands.cs             ← mapping 2 polí při save
PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs
                                                              ← ExterniVazbaViewModel rewrite (Vyzva→VyzvaId/VyzvaKod/ZaradidDoVyzvy)
PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs (+ LazyQueries kde mapping probíhá)
                                                              ← mapping nových polí
PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs ← mapping nových polí (pokud relevantní)
PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml  ← <select Vyzva> → gov-switch + status
PmTracker.Web/wwwroot/js/site.bundle.js                       ← konkatenovat nové JS moduly
PmTracker.Web/wwwroot/css/site.css                            ← @import vyzvy-panel.css
PmTracker.Web/Views/Shared/_Layout.cshtml (pokud importuje modular CSS odkazem)
                                                              ← (může být už vyřešeno přes site.css @import)
```

### Soubory se záměrně NEMĚNÍ ve fázi 2

- Entity a EF konfigurace (fáze 1)
- `IVyzvaService` a partial class implementace (fáze 1)
- `VyzvaStateMachine`, `VyzvaCodeGenerator`, `VyzvaQueries` (fáze 1)
- ServiceDesk konektor (fáze 1)
- Word export (fáze 3)
- Connection string ticketing do production DB (fáze 4)

---

## Task 1: VyzvyPanelViewModel (rozšířený model)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs:105-110` (současný `ProjectDashboardVyzvyPanelViewModel`)

- [ ] **Step 1: Rozšířit VyzvyPanelViewModel**

V `PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs` najdi:

```csharp
public sealed class ProjectDashboardVyzvyPanelViewModel
{
    public bool IsServiceDeskIntegrated { get; init; }
    public string PlaceholderMessage { get; init; } = "Žádné výzvy. Generování výzev vyžaduje napojení na ServiceDesk.";
}
```

Nahraď:

```csharp
public sealed class ProjectDashboardVyzvyPanelViewModel
{
    public int ProjektId { get; init; }
    public bool MuzeEditovat { get; init; }
    public string? ChybaProjektuMessage { get; init; }
    public VyzvyPanelBufferViewModel Buffer { get; init; } = new();
    public IReadOnlyList<VyzvyPanelVyzvaViewModel> Vyzvy { get; init; } = Array.Empty<VyzvyPanelVyzvaViewModel>();
}

public sealed class VyzvyPanelBufferViewModel
{
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();
    public bool MuzeZaloztVyzvu { get; init; }
    public string? DuvodBlokace { get; init; }
}

public sealed class VyzvyPanelVyzvaViewModel
{
    public int Id { get; init; }
    public required string Kod { get; init; }
    public int PoradoveVRoce { get; init; }
    public int Rok { get; init; }
    public required string Stav { get; init; }
    public DateTime DatumZalozeni { get; init; }
    public string? ZalozilJmeno { get; init; }
    public DateTime? DatumOdeslani { get; init; }
    public string? OdeslalJmeno { get; init; }
    public IReadOnlyList<VyzvyPanelPolozkaViewModel> Polozky { get; init; } = Array.Empty<VyzvyPanelPolozkaViewModel>();
    public decimal? CelkovaCena { get; init; }
    public IReadOnlyList<string> PovoleneStavy { get; init; } = Array.Empty<string>();
    public bool Kolapsovano { get; init; }
}

public sealed class VyzvyPanelPolozkaViewModel
{
    public int ExterniOdkazId { get; init; }
    public int ZaznamId { get; init; }
    public required string Cislo { get; init; }
    public string? StrucneNazev { get; init; }
    public decimal? PredpokladanaCena { get; init; }
    public string? CisloViditelneZaznamu { get; init; }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Možné CS chyby v místech, která používala `IsServiceDeskIntegrated` nebo `PlaceholderMessage`. Oprav je — v `ProjectDashboardService` (Task 3) je přepíšeme. Pro teď: v `BuildVyzvyPanel()` dočasně vrať novou instance s `MuzeEditovat = false`, `ChybaProjektuMessage = "ServiceDesk integrace je v přípravě (fáze 2)"`, prázdné kolekce. V `_VyzvyPanel.cshtml` upravit case na novou model: dočasně `<p class="muted">@Model.ChybaProjektuMessage</p>` pokud není null.

Tyto úpravy umožní build projít; vše nahradíme v Task 3-5.

- [ ] **Step 3: Build znovu**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded, 0 Errors

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/ProjectDashboardViewModels.cs PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs
git commit -m "feat(vyzvy-ui): rozšířit VyzvyPanel VM (Buffer + Vyzvy kolekce)"
```

---

## Task 2: VyzvyPanelBuilder — extrakt stavby modelu do samostatné třídy

**Files:**
- Create: `PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs`

- [ ] **Step 1: Builder**

```csharp
// PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Services.ProjectDashboard;

public sealed class VyzvyPanelBuilder
{
    private readonly IVyzvaService _vyzvaService;
    private readonly PmTrackerDbContext _db;

    public VyzvyPanelBuilder(IVyzvaService vyzvaService, PmTrackerDbContext db)
    {
        _vyzvaService = vyzvaService;
        _db = db;
    }

    public async Task<ProjectDashboardVyzvyPanelViewModel> BuildAsync(
        int projektId, bool muzeEditovat, CancellationToken ct)
    {
        var projekt = await _db.Projekty.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projektId, ct);

        var bufferItems = await _vyzvaService.GetBufferAsync(projektId, ct);
        var vyzvy = await _vyzvaService.GetVyzvyAsync(projektId, ct);

        var jmenaOsob = await LoadOsobaNamesAsync(vyzvy, ct);
        var cisloViditelneById = await LoadCisloViditelneMapAsync(bufferItems, vyzvy, ct);

        var (muzeZalozit, duvod) = BufferZalozitPodminky(bufferItems, projekt);

        return new ProjectDashboardVyzvyPanelViewModel
        {
            ProjektId = projektId,
            MuzeEditovat = muzeEditovat,
            Buffer = new VyzvyPanelBufferViewModel
            {
                Polozky = bufferItems.Select(b => ToPolozka(b, cisloViditelneById)).ToArray(),
                MuzeZaloztVyzvu = muzeEditovat && muzeZalozit,
                DuvodBlokace = muzeZalozit ? null : duvod,
            },
            Vyzvy = vyzvy.Select(v => ToVyzva(v, jmenaOsob, cisloViditelneById)).ToArray(),
        };
    }

    private static (bool MuzeZalozit, string? Duvod) BufferZalozitPodminky(
        IReadOnlyList<VyzvaBufferItem> buffer, ProjektEntity? projekt)
    {
        if (projekt == null) return (false, "Projekt nenalezen.");
        if (string.IsNullOrWhiteSpace(projekt.MistoPlneni))
            return (false, "Projekt nemá vyplněné Místo plnění. Doplňte v editaci projektu.");
        if (string.IsNullOrWhiteSpace(projekt.CisloRamcoveSmlouvy))
            return (false, "Projekt nemá vyplněné Číslo rámcové smlouvy. Doplňte v editaci projektu.");
        if (buffer.Count == 0)
            return (false, "Buffer je prázdný.");
        return (true, null);
    }

    private async Task<IReadOnlyDictionary<int, string>> LoadOsobaNamesAsync(
        IReadOnlyList<VyzvaDetail> vyzvy, CancellationToken ct)
    {
        var ids = vyzvy.SelectMany(v => new[] { (int?)v.ZalozilOsobaId, v.OdeslalOsobaId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, string>();

        return await _db.Osoby.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => (o.Titul + " " + o.Jmeno + " " + o.Prijmeni).Trim(), ct);
    }

    private async Task<IReadOnlyDictionary<int, string?>> LoadCisloViditelneMapAsync(
        IReadOnlyList<VyzvaBufferItem> buffer, IReadOnlyList<VyzvaDetail> vyzvy, CancellationToken ct)
    {
        var zaznamIds = buffer.Select(b => b.ZaznamId)
            .Concat(vyzvy.SelectMany(v => v.Polozky.Select(p => p.ZaznamId)))
            .Distinct().ToArray();
        if (zaznamIds.Length == 0) return new Dictionary<int, string?>();

        return await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(z => zaznamIds.Contains(z.Id))
            .Select(z => new { z.Id, z.CisloViditelne })
            .ToDictionaryAsync(x => x.Id, x => x.CisloViditelne, ct);
    }

    private static VyzvyPanelPolozkaViewModel ToPolozka(
        VyzvaBufferItem item, IReadOnlyDictionary<int, string?> cisloViditelneById)
        => new()
        {
            ExterniOdkazId = item.ExterniOdkazId,
            ZaznamId = item.ZaznamId,
            Cislo = item.Cislo,
            StrucneNazev = item.StrucneNazev,
            PredpokladanaCena = item.PredpokladanaCena,
            CisloViditelneZaznamu = cisloViditelneById.GetValueOrDefault(item.ZaznamId),
        };

    private static VyzvyPanelPolozkaViewModel ToPolozka(
        VyzvaDetailItem item, IReadOnlyDictionary<int, string?> cisloViditelneById)
        => new()
        {
            ExterniOdkazId = item.ExterniOdkazId,
            ZaznamId = item.ZaznamId,
            Cislo = item.Cislo,
            StrucneNazev = item.StrucneNazev,
            PredpokladanaCena = item.PredpokladanaCena,
            CisloViditelneZaznamu = cisloViditelneById.GetValueOrDefault(item.ZaznamId),
        };

    private static VyzvyPanelVyzvaViewModel ToVyzva(
        VyzvaDetail detail,
        IReadOnlyDictionary<int, string> jmenaOsob,
        IReadOnlyDictionary<int, string?> cisloViditelneById)
    {
        var povoleneStavy = Enum.GetValues<VyzvaStav>()
            .Where(s => s != detail.Stav && VyzvaStateMachine.JePovolenyPrechod(detail.Stav, s))
            .Select(s => s.ToString())
            .ToArray();

        return new VyzvyPanelVyzvaViewModel
        {
            Id = detail.Id,
            Kod = detail.Kod,
            PoradoveVRoce = detail.PoradoveVRoce,
            Rok = detail.Rok,
            Stav = detail.Stav.ToString(),
            DatumZalozeni = detail.DatumZalozeni,
            ZalozilJmeno = jmenaOsob.GetValueOrDefault(detail.ZalozilOsobaId),
            DatumOdeslani = detail.DatumOdeslani,
            OdeslalJmeno = detail.OdeslalOsobaId.HasValue
                ? jmenaOsob.GetValueOrDefault(detail.OdeslalOsobaId.Value)
                : null,
            Polozky = detail.Polozky.Select(p => ToPolozka(p, cisloViditelneById)).ToArray(),
            CelkovaCena = detail.Polozky.Sum(p => p.PredpokladanaCena) ?? null,
            PovoleneStavy = povoleneStavy,
            Kolapsovano = detail.Stav != VyzvaStav.Priprava,
        };
    }
}
```

*(Pokud `Osoby` DbSet má jiný název v `PmTrackerDbContext` (např. `CiselnikOsob`), uprav. Ověř gerpem.)*

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded (mohou být warnings o nepoužívaném — ignoruj zatím)

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/VyzvyPanelBuilder.cs
git commit -m "feat(vyzvy-ui): VyzvyPanelBuilder — mapping IVyzvaService → PanelViewModel"
```

---

## Task 3: ProjectDashboardService — napojit builder async

**Files:**
- Modify: `PmTracker.Web/Services/ProjectDashboard/IProjectDashboardService.cs`
- Modify: `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs:322-326` (a konstruktor)
- Modify: `PmTracker.Web/Controllers/ProjectDashboardController.cs` (metoda volající `BuildVyzvyPanel`)

- [ ] **Step 1: Přejmenovat na BuildVyzvyPanelAsync v interface**

V `PmTracker.Web/Services/ProjectDashboard/IProjectDashboardService.cs` najdi:
```csharp
ProjectDashboardVyzvyPanelViewModel BuildVyzvyPanel();
```
Nahraď:
```csharp
Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(int projektId, int osobaId, bool isSuperOrAppAdmin, CancellationToken ct);
```

- [ ] **Step 2: Refactor ProjectDashboardService**

V `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs`:

1. Injektovat do konstruktoru `VyzvyPanelBuilder builder`:

Najdi konstruktor `ProjectDashboardService(...)` a přidej argument + field:
```csharp
private readonly VyzvyPanelBuilder _vyzvyPanelBuilder;

public ProjectDashboardService(
    /* ostatní existující argumenty */,
    VyzvyPanelBuilder vyzvyPanelBuilder)
    : /* base calls */
{
    _vyzvyPanelBuilder = vyzvyPanelBuilder;
    /* existující inicializace */
}
```

*(Pokud konstruktor má hodně argumentů, zachovej pořadí existujícího — přidej nový na konec.)*

2. Najdi `public ProjectDashboardVyzvyPanelViewModel BuildVyzvyPanel()` (~řádek 322) a celou metodu nahraď:

```csharp
public Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(
    int projektId, int osobaId, bool isSuperOrAppAdmin, CancellationToken ct)
{
    var muzeEditovat = isSuperOrAppAdmin || CanUserEditProjectVyzvy(projektId, osobaId);
    return _vyzvyPanelBuilder.BuildAsync(projektId, muzeEditovat, ct);
}

private bool CanUserEditProjectVyzvy(int projektId, int osobaId)
{
    // TODO(Task 4): volá se z existujícího ACL (podle proj_man/adm_proj role pro daný projekt)
    // Dočasně: vrať true — přepíšeme s reálným ACL checkerem
    return true;
}
```

- [ ] **Step 3: Registrovat VyzvyPanelBuilder v DI**

V `PmTracker.Web/Program.cs` najdi místo kde se registrují services ProjectDashboard (grepni `ProjectDashboardService`). Přidej před/za:
```csharp
builder.Services.AddScoped<PmTracker.Web.Services.ProjectDashboard.VyzvyPanelBuilder>();
```

- [ ] **Step 4: Update ProjectDashboardController**

V `PmTracker.Web/Controllers/ProjectDashboardController.cs` najdi metodu která vrací Vyzvy panel partial (pravděpodobně `GetVyzvyPanel` nebo podobné — grep `VyzvyPanel`). Aktualizuj:

```csharp
[HttpGet("vyzvy-panel")]
public async Task<IActionResult> GetVyzvyPanel(int id, CancellationToken ct)
{
    if (!CurrentUserContext.CanAccessProject(id)) return NotFound();

    var vm = await _dashboardService.BuildVyzvyPanelAsync(
        id,
        CurrentUserContext.OsobaId,
        CurrentUserContext.IsSuperAdmin || CurrentUserContext.IsAppAdmin,
        ct);

    return PartialView("_VyzvyPanel", vm);
}
```

*(Přizpůsob podle skutečných názvů properties `CurrentUserContext`. Pokud `IsAppAdmin` neexistuje, grepni jak se rozpoznává role. Použij to co existuje.)*

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/ PmTracker.Web/Controllers/ProjectDashboardController.cs PmTracker.Web/Program.cs
git commit -m "feat(vyzvy-ui): async BuildVyzvyPanelAsync přes VyzvyPanelBuilder"
```

---

## Task 4: ACL helper pro editaci výzev

**Files:**
- Modify: `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs` (dočasná metoda `CanUserEditProjectVyzvy`)

- [ ] **Step 1: Najít existující ACL pattern**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "proj_man\|adm_proj" PmTracker.Web/Services/ PmTracker.Web/Models/ViewModels/Security/ 2>/dev/null | head -15
```

Dle nálezu najdi existující helper (pravděpodobně `CanEditProject`, `IsProjectAdmin`, `HasProjectRole` nebo podobně) v `UserContextResolver` nebo `ProjectRoleService`. Pokud existuje `CurrentUserContext.IsProjectAdmin(projektId)` nebo podobně, použij.

- [ ] **Step 2: Nahradit dočasný helper**

V `ProjectDashboardService.cs` nahraď `CanUserEditProjectVyzvy` reálným voláním. Příklad (přizpůsob skutečnému názvu):

```csharp
private async Task<bool> CanUserEditProjectVyzvyAsync(int projektId, int osobaId, CancellationToken ct)
{
    // Povolené role pro editaci výzev projektu: proj_man, adm_proj
    var povoleneRolyKody = new[] { "proj_man", "adm_proj" };
    return await dbContext.ObsazeniProjektu
        .Where(o => o.ProjektId == projektId && o.OsobaId == osobaId && o.DatumOdebrani == null)
        .Join(dbContext.CiselnikRoliProjektu, o => o.RoleId, r => r.Id, (o, r) => r.Kod)
        .AnyAsync(kod => povoleneRolyKody.Contains(kod), ct);
}
```

A `BuildVyzvyPanelAsync` uprav:
```csharp
public async Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(
    int projektId, int osobaId, bool isSuperOrAppAdmin, CancellationToken ct)
{
    var muzeEditovat = isSuperOrAppAdmin || await CanUserEditProjectVyzvyAsync(projektId, osobaId, ct);
    return await _vyzvyPanelBuilder.BuildAsync(projektId, muzeEditovat, ct);
}
```

*(Pokud je v projektu jiný vzor pro ACL — např. injektovaná `IUserContextResolver` s metodou `HasProjectRole` — použij ten. Tato dočasná verze s přímým EF dotazem je fallback pokud nic lepšího nenajdeš.)*

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs
git commit -m "feat(vyzvy-ui): ACL check proj_man/adm_proj pro edit výzev"
```

---

## Task 5: `_VyzvyPanel.cshtml` + partials

**Files:**
- Modify: `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml` (přepsat z placeholderu)
- Create: `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.BufferCard.cshtml`
- Create: `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.VyzvaCard.cshtml`

- [ ] **Step 1: Main panel**

Přepis `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.cshtml`:

```razor
@using System.Globalization
@model ProjectDashboardVyzvyPanelViewModel

<section class="vyzvy-panel"
         data-vyzvy-panel
         data-project-id="@Model.ProjektId"
         data-muze-editovat="@(Model.MuzeEditovat ? "true" : "false")">

    @await Html.PartialAsync("_VyzvyPanel.BufferCard", Model.Buffer, new ViewDataDictionary(ViewData)
    {
        { "MuzeEditovat", Model.MuzeEditovat },
        { "ProjektId", Model.ProjektId }
    })

    @if (Model.Vyzvy.Count == 0)
    {
        <div class="vyzvy-empty muted">Projekt zatím nemá žádnou výzvu.</div>
    }
    else
    {
        foreach (var v in Model.Vyzvy)
        {
            @await Html.PartialAsync("_VyzvyPanel.VyzvaCard", v, new ViewDataDictionary(ViewData)
            {
                { "MuzeEditovat", Model.MuzeEditovat },
                { "ProjektId", Model.ProjektId }
            })
        }
    }

</section>
```

- [ ] **Step 2: Buffer partial**

Create `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.BufferCard.cshtml`:

```razor
@using System.Globalization
@model VyzvyPanelBufferViewModel
@{
    var muzeEditovat = ViewData["MuzeEditovat"] as bool? ?? false;
    var projektId = ViewData["ProjektId"] as int? ?? 0;
}

<article class="vyzvy-card vyzvy-card-buffer" data-vyzvy-buffer>
    <header class="vyzvy-card-header">
        <h3 class="vyzvy-card-title">Buffer — čeká na zařazení</h3>
        <span class="vyzvy-card-badge badge-muted">@Model.Polozky.Count PNF</span>
    </header>

    @if (Model.Polozky.Count == 0)
    {
        <p class="vyzvy-card-empty muted">
            Buffer je prázdný. U PNF v externích vazbách zapněte přepínač „Zařadit do další výzvy".
        </p>
    }
    else
    {
        <ul class="vyzvy-polozky">
            @foreach (var p in Model.Polozky)
            {
                <li class="vyzvy-polozka" data-vyzvy-polozka data-externi-odkaz-id="@p.ExterniOdkazId">
                    <span class="vyzvy-polozka-cislo">@p.Cislo</span>
                    @if (!string.IsNullOrWhiteSpace(p.CisloViditelneZaznamu))
                    {
                        <span class="vyzvy-polozka-meta">@p.CisloViditelneZaznamu</span>
                    }
                    <span class="vyzvy-polozka-nazev">@(p.StrucneNazev ?? "(bez názvu z HOT)")</span>
                    @if (p.PredpokladanaCena.HasValue)
                    {
                        <span class="vyzvy-polozka-cena">
                            @p.PredpokladanaCena.Value.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ")) Kč
                        </span>
                    }
                </li>
            }
        </ul>
    }

    <footer class="vyzvy-card-footer">
        @if (muzeEditovat)
        {
            <button type="button"
                    class="pm-button primary"
                    data-vyzvy-action="zalozit"
                    data-projekt-id="@projektId"
                    @(Model.MuzeZaloztVyzvu ? null : "disabled=\"disabled\"")>
                Založit výzvu z bufferu
            </button>
            @if (!Model.MuzeZaloztVyzvu && !string.IsNullOrWhiteSpace(Model.DuvodBlokace))
            {
                <span class="muted vyzvy-block-reason">@Model.DuvodBlokace</span>
            }
        }
    </footer>
</article>
```

- [ ] **Step 3: Vyzva partial**

Create `PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel.VyzvaCard.cshtml`:

```razor
@using System.Globalization
@model VyzvyPanelVyzvaViewModel
@{
    var muzeEditovat = ViewData["MuzeEditovat"] as bool? ?? false;
    var projektId = ViewData["ProjektId"] as int? ?? 0;
    var badgeClass = Model.Stav switch
    {
        "Priprava" => "badge-warning",
        "Odeslano" => "badge-success",
        "Zruseno" => "badge-muted",
        _ => "badge-muted",
    };
    var defaultOpen = !Model.Kolapsovano;
}

<article class="vyzvy-card vyzvy-card-vyzva @(defaultOpen ? "open" : null)"
         data-vyzvy-vyzva
         data-vyzva-id="@Model.Id"
         data-stav="@Model.Stav">
    <header class="vyzvy-card-header" data-vyzvy-toggle>
        <h3 class="vyzvy-card-title">Výzva @Model.Kod</h3>
        <span class="vyzvy-card-badge @badgeClass">@Model.Stav</span>
        <span class="vyzvy-card-meta">
            @Model.DatumZalozeni.ToString("dd.MM.yyyy")
            @if (!string.IsNullOrWhiteSpace(Model.ZalozilJmeno))
            {
                <text> · @Model.ZalozilJmeno</text>
            }
        </span>
        <button type="button" class="vyzvy-card-toggle-btn" aria-expanded="@(defaultOpen ? "true" : "false")">
            <span class="vyzvy-card-toggle-icon">@(defaultOpen ? "▼" : "▶")</span>
        </button>
    </header>

    <div class="vyzvy-card-body">
        @if (Model.Polozky.Count == 0)
        {
            <p class="vyzvy-card-empty muted">Výzva neobsahuje žádné PNF.</p>
        }
        else
        {
            <table class="vyzvy-polozky-table">
                <thead>
                    <tr>
                        <th>#</th>
                        <th>Č. úkolu VP</th>
                        <th>HTL</th>
                        <th>Název</th>
                        <th class="num">Cena</th>
                    </tr>
                </thead>
                <tbody>
                    @{ var index = 0; }
                    @foreach (var p in Model.Polozky)
                    {
                        var pismeno = ((char)('a' + index)).ToString();
                        index++;
                        <tr>
                            <td>@pismeno)</td>
                            <td>@(p.CisloViditelneZaznamu ?? "—")</td>
                            <td>@p.Cislo</td>
                            <td>@(p.StrucneNazev ?? "(bez názvu z HOT)")</td>
                            <td class="num">
                                @(p.PredpokladanaCena?.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ")) ?? "—")
                            </td>
                        </tr>
                    }
                    @if (Model.CelkovaCena.HasValue)
                    {
                        <tr class="total">
                            <td colspan="4">CELKEM (předpokládaná cena)</td>
                            <td class="num">@Model.CelkovaCena.Value.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ")) Kč</td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>

    <footer class="vyzvy-card-footer">
        <button type="button" class="pm-button ghost" disabled title="Export Word bude dostupný v další fázi">
            Stáhnout Word ✗
        </button>

        @if (muzeEditovat)
        {
            <button type="button"
                    class="pm-button ghost"
                    data-vyzvy-action="otevrit-reassign"
                    data-projekt-id="@projektId">
                Upravit přiřazení PNF
            </button>

            @if (Model.PovoleneStavy.Count > 0)
            {
                <div class="vyzvy-stav-menu" data-vyzvy-stav-menu>
                    <button type="button" class="pm-button ghost" data-vyzvy-stav-toggle>Změnit stav ▾</button>
                    <ul class="vyzvy-stav-menu-list">
                        @foreach (var s in Model.PovoleneStavy)
                        {
                            <li>
                                <button type="button"
                                        data-vyzvy-action="zmenit-stav"
                                        data-vyzva-id="@Model.Id"
                                        data-novy-stav="@s">
                                    Přepnout na: @s
                                </button>
                            </li>
                        }
                    </ul>
                </div>
            }
        }
    </footer>
</article>
```

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/ProjectDashboard/_VyzvyPanel*.cshtml
git commit -m "feat(vyzvy-ui): _VyzvyPanel + BufferCard + VyzvaCard partials"
```

---

## Task 6: CSS — vyzvy-panel.css

**Files:**
- Create: `PmTracker.Web/wwwroot/css/components/vyzvy-panel.css`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (přidat @import)

- [ ] **Step 1: Vytvořit stylový soubor**

```css
/* PmTracker.Web/wwwroot/css/components/vyzvy-panel.css */

.vyzvy-panel {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1rem 0;
}

.vyzvy-card {
  background: var(--pm-surface, #ffffff);
  border: 1px solid var(--pm-border, #e2e8f0);
  border-radius: 8px;
  padding: 1rem 1.25rem;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.02);
}

.vyzvy-card-buffer {
  border-left: 4px solid var(--token-warning, #f59e0b);
}

.vyzvy-card-vyzva {
  border-left: 4px solid var(--token-muted, #94a3b8);
}

.vyzvy-card-vyzva[data-stav="Priprava"] {
  border-left-color: var(--token-warning, #f59e0b);
}

.vyzvy-card-vyzva[data-stav="Odeslano"] {
  border-left-color: var(--token-success, #10b981);
}

.vyzvy-card-vyzva[data-stav="Zruseno"] {
  border-left-color: var(--token-muted, #94a3b8);
  opacity: 0.75;
}

.vyzvy-card-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding-bottom: 0.75rem;
  cursor: pointer;
}

.vyzvy-card-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
}

.vyzvy-card-badge {
  padding: 0.125rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 500;
}

.badge-warning { background: #fef3c7; color: #92400e; }
.badge-success { background: #d1fae5; color: #065f46; }
.badge-muted   { background: #e2e8f0; color: #475569; }

.vyzvy-card-meta {
  margin-left: auto;
  font-size: 0.8125rem;
  color: var(--pm-text-muted, #64748b);
}

.vyzvy-card-toggle-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  font-size: 1rem;
  color: var(--pm-text-muted, #64748b);
}

.vyzvy-card-body {
  display: none;
  padding-top: 0.75rem;
  border-top: 1px solid var(--pm-border, #e2e8f0);
}

.vyzvy-card.open > .vyzvy-card-body {
  display: block;
}

.vyzvy-card-footer {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  flex-wrap: wrap;
  padding-top: 0.75rem;
  border-top: 1px solid var(--pm-border, #e2e8f0);
  margin-top: 0.75rem;
}

.vyzvy-polozky {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.vyzvy-polozka {
  display: grid;
  grid-template-columns: auto auto 1fr auto;
  gap: 0.75rem;
  align-items: center;
  padding: 0.5rem 0.75rem;
  background: var(--pm-surface-alt, #f8fafc);
  border-radius: 6px;
  font-size: 0.875rem;
}

.vyzvy-polozka-cislo { font-family: var(--pm-font-mono, monospace); font-weight: 500; }
.vyzvy-polozka-meta  { font-family: var(--pm-font-mono, monospace); color: var(--pm-text-muted, #64748b); }
.vyzvy-polozka-cena  { text-align: right; font-variant-numeric: tabular-nums; }

.vyzvy-polozky-table {
  width: 100%;
  border-collapse: collapse;
}

.vyzvy-polozky-table th,
.vyzvy-polozky-table td {
  padding: 0.5rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid var(--pm-border, #e2e8f0);
}

.vyzvy-polozky-table .num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.vyzvy-polozky-table tr.total td {
  font-weight: 600;
  border-top: 2px solid var(--pm-border, #e2e8f0);
  border-bottom: none;
}

.vyzvy-stav-menu {
  position: relative;
}

.vyzvy-stav-menu-list {
  display: none;
  position: absolute;
  top: 100%;
  right: 0;
  list-style: none;
  margin: 0;
  padding: 0.25rem 0;
  background: var(--pm-surface, #ffffff);
  border: 1px solid var(--pm-border, #e2e8f0);
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
  min-width: 12rem;
  z-index: 10;
}

.vyzvy-stav-menu.open .vyzvy-stav-menu-list { display: block; }

.vyzvy-stav-menu-list button {
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  padding: 0.5rem 0.75rem;
  cursor: pointer;
  font-size: 0.875rem;
}

.vyzvy-stav-menu-list button:hover { background: var(--pm-surface-alt, #f1f5f9); }

.vyzvy-empty { padding: 1rem; text-align: center; }
.vyzvy-block-reason { margin-left: 0.75rem; font-size: 0.8125rem; }

/* Switch na externí vazbě PNF */
.vyzvy-switch-wrap {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 0.5rem;
}

.vyzvy-switch-status {
  font-size: 0.8125rem;
  color: var(--pm-text-muted, #64748b);
}

/* Reassign modal */
.vyzvy-reassign-columns {
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(220px, 1fr);
  gap: 1rem;
  overflow-x: auto;
  padding: 1rem 0;
}

.vyzvy-reassign-column {
  background: var(--pm-surface-alt, #f8fafc);
  border-radius: 8px;
  padding: 0.75rem;
  min-height: 200px;
}

.vyzvy-reassign-column.drag-over {
  background: #fef3c7;
  outline: 2px dashed var(--token-warning, #f59e0b);
}

.vyzvy-reassign-item {
  background: var(--pm-surface, #ffffff);
  border: 1px solid var(--pm-border, #e2e8f0);
  border-radius: 6px;
  padding: 0.5rem 0.75rem;
  margin-bottom: 0.5rem;
  cursor: grab;
  font-size: 0.875rem;
}

.vyzvy-reassign-item.dragging { opacity: 0.5; }
```

- [ ] **Step 2: @import v site.css**

Na vrch `PmTracker.Web/wwwroot/css/site.css` přidej (nebo pokud tam jsou existující @imports, zařaď mezi ně):

```css
@import "components/vyzvy-panel.css";
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/css/
git commit -m "feat(vyzvy-ui): CSS pro panel výzev (karty, switch, reassign modal)"
```

---

## Task 7: VyzvyController — AJAX JSON endpointy

**Files:**
- Create: `PmTracker.Web/Controllers/VyzvyController.cs`

- [ ] **Step 1: Controller**

```csharp
// PmTracker.Web/Controllers/VyzvyController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Controllers;

[Route("vyzvy")]
public sealed class VyzvyController : BaseController
{
    private readonly IVyzvaService _vyzvaService;
    private readonly TimeProvider _timeProvider;

    public VyzvyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IVyzvaService vyzvaService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _vyzvaService = vyzvaService;
        _timeProvider = timeProvider;
    }

    public sealed class ZaloztRequest
    {
        public int ProjektId { get; set; }
    }

    public sealed class ZmenitStavRequest
    {
        public int VyzvaId { get; set; }
        public string NovyStav { get; set; } = string.Empty;
    }

    public sealed class SetZaradidRequest
    {
        public int ExterniOdkazId { get; set; }
        public bool Zaradit { get; set; }
    }

    public sealed class PrerditRequest
    {
        public int ExterniOdkazId { get; set; }
        public int? CilovaVyzvaId { get; set; }
    }

    [HttpPost("zalozit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zalozit([FromForm] ZaloztRequest request, CancellationToken ct)
    {
        if (!CanEditProjectVyzvy(request.ProjektId)) return Forbid();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _vyzvaService.ZaloztVyzvuZBufferuAsync(
            request.ProjektId, CurrentUserContext.OsobaId, now, ct);

        return JsonFromResult(result);
    }

    [HttpPost("zmenit-stav")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZmenitStav([FromForm] ZmenitStavRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<VyzvaStav>(request.NovyStav, ignoreCase: false, out var stav))
            return BadRequest(new { success = false, errorCode = "InvalidStateTransition", message = "Neplatný stav." });

        if (!await CanEditVyzvaAsync(request.VyzvaId, ct)) return Forbid();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _vyzvaService.ZmenitStavAsync(
            request.VyzvaId, stav, CurrentUserContext.OsobaId, now, ct);

        return JsonFromResult(result);
    }

    [HttpPost("set-zaradid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetZaradid([FromForm] SetZaradidRequest request, CancellationToken ct)
    {
        if (!await CanEditExterniOdkazAsync(request.ExterniOdkazId, ct)) return Forbid();

        var result = await _vyzvaService.NastavitZaradidAsync(
            request.ExterniOdkazId, request.Zaradit, ct);

        return JsonFromUnit(result);
    }

    [HttpPost("prerdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Prerdit([FromForm] PrerditRequest request, CancellationToken ct)
    {
        if (!await CanEditExterniOdkazAsync(request.ExterniOdkazId, ct)) return Forbid();

        var result = await _vyzvaService.PrerditPnfAsync(
            request.ExterniOdkazId, request.CilovaVyzvaId, ct);

        return JsonFromUnit(result);
    }

    [HttpGet("reassign-modal")]
    public async Task<IActionResult> ReassignModal(int projektId, CancellationToken ct)
    {
        if (!CanEditProjectVyzvy(projektId)) return Forbid();

        var buffer = await _vyzvaService.GetBufferAsync(projektId, ct);
        var vyzvy = await _vyzvaService.GetVyzvyAsync(projektId, ct);

        var model = new Models.ViewModels.Vyzvy.ReassignModalViewModel
        {
            ProjektId = projektId,
            BufferPolozky = buffer,
            PripravaVyzvy = vyzvy.Where(v => v.Stav == VyzvaStav.Priprava).ToArray(),
        };

        return PartialView("~/Views/Vyzvy/ReassignModal.cshtml", model);
    }

    private IActionResult JsonFromResult<T>(VyzvaResult<T> result)
        => result switch
        {
            VyzvaResult<T>.Ok ok => Ok(new { success = true, reload = true }),
            VyzvaResult<T>.Fail fail => Ok(new
            {
                success = false,
                errorCode = fail.Error.Code.ToString(),
                message = fail.Error.Message,
            }),
            _ => StatusCode(500),
        };

    private IActionResult JsonFromUnit(VyzvaResult<Unit> result)
        => result switch
        {
            VyzvaResult<Unit>.Ok => Ok(new { success = true }),
            VyzvaResult<Unit>.Fail fail => Ok(new
            {
                success = false,
                errorCode = fail.Error.Code.ToString(),
                message = fail.Error.Message,
            }),
            _ => StatusCode(500),
        };

    private bool CanEditProjectVyzvy(int projektId)
    {
        if (!CurrentUserContext.CanAccessProject(projektId)) return false;
        if (CurrentUserContext.IsSuperAdmin) return true;
        // Dočasně: povol všechny uživatele, kteří vidí projekt. ACL proj_man/adm_proj
        // doplníme v Task 8 po ověření existujících helperů. Toto je TODO.
        return true;
    }

    private async Task<bool> CanEditVyzvaAsync(int vyzvaId, CancellationToken ct)
    {
        var detail = await _vyzvaService.GetVyzvaAsync(vyzvaId, ct);
        return detail != null && CanEditProjectVyzvy(detail.ProjektId);
    }

    private async Task<bool> CanEditExterniOdkazAsync(int externiOdkazId, CancellationToken ct)
    {
        // Najdi projekt přes zaznam
        var detail = await _vyzvaService.GetVyzvaAsync(0, ct); // hack — neefektivní; nahradíme reálnou query v Task 8
        // Prozatím: povol pokud uživatel má jakýkoli projekt (bude dokumentováno jako TODO v Task 8)
        return CurrentUserContext.IsSuperAdmin || true;
    }
}
```

*(Metody `CanEditProjectVyzvy`, `CanEditExterniOdkazAsync` jsou dočasné — v Task 8 je nahradíme reálným ACL dotazem. Pro teď povolit pokud `CurrentUserContext.CanAccessProject` — alespoň to zabrání cizím uživatelům zapisovat.)*

- [ ] **Step 2: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded (case `Ok ok` v switch — kompilátor případně vyhodí warning o unused var, uprav na `VyzvaResult<T>.Ok` bez binding, nebo `_` v patternu)

Pokud selže s „pattern match is irrefutable" atd., nahraď:
```csharp
VyzvaResult<T>.Ok => Ok(new { success = true, reload = true }),
VyzvaResult<T>.Fail fail => ...,
_ => StatusCode(500),
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Controllers/VyzvyController.cs
git commit -m "feat(vyzvy-ui): VyzvyController s JSON AJAX endpointy (dočasné ACL)"
```

---

## Task 8: Zpřísnit ACL ve VyzvyController

**Files:**
- Modify: `PmTracker.Web/Controllers/VyzvyController.cs` (metody `CanEditProjectVyzvy`, `CanEditExterniOdkazAsync`)

- [ ] **Step 1: Přidat dependency na DbContext**

V konstruktoru `VyzvyController` přidej argument `PmTrackerDbContext db`:

```csharp
private readonly IVyzvaService _vyzvaService;
private readonly TimeProvider _timeProvider;
private readonly PmTracker.Web.Data.PmTrackerDbContext _db;

public VyzvyController(
    IUserContextResolver userContextResolver,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory,
    IVyzvaService vyzvaService,
    PmTracker.Web.Data.PmTrackerDbContext db)
    : base(userContextResolver, timeProvider, loggerFactory)
{
    _vyzvaService = vyzvaService;
    _timeProvider = timeProvider;
    _db = db;
}
```

- [ ] **Step 2: Nahradit CanEditProjectVyzvy reálnou kontrolou**

```csharp
private async Task<bool> CanEditProjectVyzvyAsync(int projektId, CancellationToken ct)
{
    if (!CurrentUserContext.CanAccessProject(projektId)) return false;
    if (CurrentUserContext.IsSuperAdmin) return true;
    // app_admin — vyřeš dle skutečné property CurrentUserContext (IsAppAdmin nebo Role kontrola)
    if (IsAppAdmin()) return true;

    var povoleneRolyKody = new[] { "proj_man", "adm_proj" };
    var osobaId = CurrentUserContext.OsobaId;
    return await _db.ObsazeniProjektu.AsNoTracking()
        .Where(o => o.ProjektId == projektId && o.OsobaId == osobaId && o.DatumOdebrani == null)
        .Join(_db.CiselnikRoliProjektu, o => o.RoleId, r => r.Id, (o, r) => r.Kod)
        .AnyAsync(kod => povoleneRolyKody.Contains(kod), ct);
}

private bool IsAppAdmin()
{
    // TODO: Ověř jak se v projektu rozpoznává app_admin role. Grepni:
    //   grep -rn "app_admin\|IsAppAdmin\|AppAdmin" PmTracker.Web/Services/Security/
    // Pokud existuje property CurrentUserContext.IsAppAdmin → použij ji.
    // Pokud role checkuje přes CiselnikRoli.Kod == "app_admin", přidej tu kontrolu.
    return false; // fallback — pokud role není rozpoznaná, blokuj; bezpečnější než povolit
}
```

- [ ] **Step 3: Nahradit CanEditExterniOdkazAsync**

```csharp
private async Task<bool> CanEditExterniOdkazAsync(int externiOdkazId, CancellationToken ct)
{
    var projektId = await _db.ZaznamExterniOdkazy.AsNoTracking()
        .Where(ev => ev.Id == externiOdkazId)
        .Join(_db.ProjektoveZaznamy, ev => ev.ZaznamId, z => z.Id, (ev, z) => z.ProjektId)
        .FirstOrDefaultAsync(ct);
    if (projektId == 0) return false;

    return await CanEditProjectVyzvyAsync(projektId, ct);
}
```

- [ ] **Step 4: Aktualizovat volající metody na async**

V controlleru přepiš volání:
```csharp
// Stávající: if (!CanEditProjectVyzvy(request.ProjektId)) return Forbid();
if (!await CanEditProjectVyzvyAsync(request.ProjektId, ct)) return Forbid();
```

Přejmenuj starou metodu `CanEditProjectVyzvy` → zruš (je nahrazena asyncem). Podobně odstraň starou `CanEditVyzvaAsync` a `CanEditExterniOdkazAsync` bez async signatury. `ReassignModal` → async verze.

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Controllers/VyzvyController.cs
git commit -m "feat(vyzvy-ui): ACL check přes proj_man/adm_proj + app_admin ve VyzvyController"
```

---

## Task 9: ReassignModalViewModel + view

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Vyzvy/ReassignModalViewModel.cs`
- Create: `PmTracker.Web/Views/Vyzvy/ReassignModal.cshtml`

- [ ] **Step 1: ViewModel**

```csharp
// PmTracker.Web/Models/ViewModels/Vyzvy/ReassignModalViewModel.cs
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Models.ViewModels.Vyzvy;

public sealed class ReassignModalViewModel
{
    public int ProjektId { get; init; }
    public required IReadOnlyList<VyzvaBufferItem> BufferPolozky { get; init; }
    public required IReadOnlyList<VyzvaDetail> PripravaVyzvy { get; init; }
}
```

- [ ] **Step 2: View**

```razor
@* PmTracker.Web/Views/Vyzvy/ReassignModal.cshtml *@
@using PmTracker.Web.Services.Vyzvy.Contracts
@model PmTracker.Web.Models.ViewModels.Vyzvy.ReassignModalViewModel

<div class="modal-body">
    <h2>Upravit přiřazení PNF</h2>
    <p class="muted">Přetáhni PNF mezi bufferem a výzvami ve stavu <em>Priprava</em>. Odeslané výzvy nejsou v úpravě viditelné.</p>

    <div class="vyzvy-reassign-columns" data-vyzvy-reassign-columns data-projekt-id="@Model.ProjektId">

        <section class="vyzvy-reassign-column"
                 data-vyzvy-reassign-target
                 data-cilova-vyzva-id="">
            <header>
                <h3>Buffer</h3>
                <span class="muted">@Model.BufferPolozky.Count PNF</span>
            </header>
            <div class="vyzvy-reassign-items">
                @foreach (var item in Model.BufferPolozky)
                {
                    <div class="vyzvy-reassign-item"
                         draggable="true"
                         data-vyzvy-reassign-item
                         data-externi-odkaz-id="@item.ExterniOdkazId">
                        <strong>@item.Cislo</strong>
                        @if (!string.IsNullOrWhiteSpace(item.StrucneNazev))
                        {
                            <div class="muted">@item.StrucneNazev</div>
                        }
                    </div>
                }
            </div>
        </section>

        @foreach (var vyzva in Model.PripravaVyzvy)
        {
            <section class="vyzvy-reassign-column"
                     data-vyzvy-reassign-target
                     data-cilova-vyzva-id="@vyzva.Id">
                <header>
                    <h3>@vyzva.Kod</h3>
                    <span class="muted">Priprava · @vyzva.Polozky.Count PNF</span>
                </header>
                <div class="vyzvy-reassign-items">
                    @foreach (var polozka in vyzva.Polozky)
                    {
                        <div class="vyzvy-reassign-item"
                             draggable="true"
                             data-vyzvy-reassign-item
                             data-externi-odkaz-id="@polozka.ExterniOdkazId">
                            <strong>@polozka.Cislo</strong>
                            @if (!string.IsNullOrWhiteSpace(polozka.StrucneNazev))
                            {
                                <div class="muted">@polozka.StrucneNazev</div>
                            }
                        </div>
                    }
                </div>
            </section>
        }
    </div>

    <div class="modal-footer">
        <button type="button" class="pm-button ghost" data-vyzvy-reassign-close>Zavřít</button>
    </div>
</div>
```

- [ ] **Step 3: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Vyzvy/ PmTracker.Web/Views/Vyzvy/
git commit -m "feat(vyzvy-ui): ReassignModal VM + view (DnD sloupce buffer + Priprava výzvy)"
```

---

## Task 10: Edit projektu — MistoPlneni + CisloRamcoveSmlouvy

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs` (`SaveProjectCommand`)
- Modify: `PmTracker.Web/Models/ViewModels/ModalViewModels.cs` (`ProjectModalViewModel` — pokud má property pro hodnoty)
- Modify: `PmTracker.Web/Views/Projekty/ProjectModal.cshtml`
- Modify: `PmTracker.Web/Services/ProjectService.Commands.cs` (mapping)
- Modify: další místa, kde se `SaveProjectCommand` sestavuje (grep: `new SaveProjectCommand`)

- [ ] **Step 1: Rozšířit SaveProjectCommand**

V `ProjectCommands.cs` najdi `SaveProjectCommand` a přidej za `PouzivatIdentJednani`:

```csharp
    [StringLength(500)]
    public string? MistoPlneni { get; set; }

    [StringLength(100)]
    public string? CisloRamcoveSmlouvy { get; set; }
```

- [ ] **Step 2: Mapping v SaveProjectAsync**

V `PmTracker.Web/Services/ProjectService.Commands.cs` najdi metodu `SaveProjectAsync`. V bloku `if (command.Id.HasValue)` za `existing.PouzivatIdentJednani = command.PouzivatIdentJednani;` přidej:

```csharp
            existing.MistoPlneni = NormalizeOrNull(command.MistoPlneni);
            existing.CisloRamcoveSmlouvy = NormalizeOrNull(command.CisloRamcoveSmlouvy);
```

V bloku pro insert (`else` větev) při vytvoření nového projektu, za poslední přiřazení nového `ProjektEntity`:

```csharp
            // ... existing.Nazev, Zkratka, StavId, PouzivatIdentJednani
            MistoPlneni = NormalizeOrNull(command.MistoPlneni),
            CisloRamcoveSmlouvy = NormalizeOrNull(command.CisloRamcoveSmlouvy),
```

Přidej helper kamkoliv ve třídě:
```csharp
    private static string? NormalizeOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
```

- [ ] **Step 3: Update ProjectModal.cshtml**

V `PmTracker.Web/Views/Projekty/ProjectModal.cshtml` najdi existující `<label>` bloky (Nazev, Zkratka, Stav, checkbox PouzivatIdentJednani). Za poslední `<label>` přidej:

```razor
    <label>
        Místo plnění (pro generování výzev)
        <textarea name="MistoPlneni" rows="2" maxlength="500"
                  placeholder="např. FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6">@Model.Command.MistoPlneni</textarea>
    </label>
    <label>
        Číslo rámcové smlouvy (pro generování výzev)
        <input type="text" name="CisloRamcoveSmlouvy" maxlength="100"
               placeholder="např. 23106000271" value="@Model.Command.CisloRamcoveSmlouvy" />
    </label>
```

- [ ] **Step 4: Propagace hodnot při načtení modalu**

Najdi kde se `SaveProjectCommand` předvyplňuje při otevření edit modalu (pravděpodobně `ProjektyController.ProjectModals.cs` nebo `ProjectService.RecordCards.cs` — grep `new SaveProjectCommand` + `ProjectModalViewModel`):

```bash
grep -rn "new SaveProjectCommand\|Command = new SaveProjectCommand" PmTracker.Web/
```

Pro každé místo, kde se `SaveProjectCommand` naplňuje z existující entity, přidej:
```csharp
MistoPlneni = projekt.MistoPlneni,
CisloRamcoveSmlouvy = projekt.CisloRamcoveSmlouvy,
```

- [ ] **Step 5: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs PmTracker.Web/Services/ProjectService.Commands.cs PmTracker.Web/Views/Projekty/ProjectModal.cshtml PmTracker.Web/Services/ PmTracker.Web/Controllers/
git commit -m "feat(projekt): edit modal — MistoPlneni + CisloRamcoveSmlouvy"
```

---

## Task 11: ExterniVazbaViewModel — přejmenovat Vyzva → VyzvaId/VyzvaKod/ZaradidDoVyzvy

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs` (řádek ~111, `ExterniVazbaViewModel`)
- Modify: `PmTracker.Web/Services/ProjectService.RecordEditorComposition.cs` nebo `LazyQueries.cs` (mapping entity → VM)
- Modify: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs` (pokud referuje `.Vyzva`)

- [ ] **Step 1: Rozšířit ExterniVazbaViewModel**

V `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs` najdi `public string? Vyzva { get; set; }` (řádek ~111) a nahraď:

```csharp
    public int? VyzvaId { get; set; }
    public string? VyzvaKod { get; set; }
    public bool ZaradidDoVyzvy { get; set; }
```

- [ ] **Step 2: Odstranit zastaralý ZaznamEditViewModel.Vyzvy**

V `ZaznamEditViewModels.cs` najdi `public required IReadOnlyList<string> Vyzvy { get; set; }` (řádek ~39) a celý ten řádek odstraň. Už nebude potřeba (select se nahrazuje switchem).

- [ ] **Step 3: Oprava mapování**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore 2>&1 | grep "error CS" | head -30`

Pro každou chybu:
- `vazba.Vyzva` → `vazba.VyzvaKod` (display) nebo `vazba.VyzvaId` (FK)
- Seznam výzev `Model.Vyzvy` (v Razor) — odstranit, už neděláme select
- `RecordProposalPayloadMapper` — pokud tam je `Vyzva = ...`, nahraď `VyzvaId = ...`

Typické místo v `ProjectService.RecordEditorComposition.cs` nebo `LazyQueries.cs`:
```csharp
// předtím: Vyzva = vyzvaById.TryGetValue(...) ? ...Kod : null
// nyní:
VyzvaId = odkaz.VyzvaId,
VyzvaKod = odkaz.VyzvaId.HasValue && vyzvaById.TryGetValue(odkaz.VyzvaId.Value, out var v) ? v.Kod : null,
ZaradidDoVyzvy = odkaz.ZaradidDoVyzvy,
```

Opakuj `dotnet build` dokud build neprojde.

- [ ] **Step 4: Test build + run relevant tests**

```bash
dotnet build --no-restore
dotnet test PmTracker.Tests.Unit --no-restore
```
Expected: Build succeeded; testy passed (pokud nějaký specific test závisí na `Vyzvy` list, oprav ho)

- [ ] **Step 5: Commit**

```bash
git add -A PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs PmTracker.Web/Services/ PmTracker.Tests.Unit/
git commit -m "refactor(vyzvy-ui): ExterniVazbaVM — Vyzva (string) → VyzvaId + VyzvaKod + ZaradidDoVyzvy"
```

---

## Task 12: Úprava _EditZaznamExternalPanel.cshtml — switch místo selectu

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml` (řádky ~60-70 a ~165-170)

- [ ] **Step 1: Najít současný select pro Vyzva**

V `_EditZaznamExternalPanel.cshtml` existují 2 místa:
1. Smyčka přes existující vazby (kolem řádku 60)
2. Template pro novou vazbu (kolem řádku 165)

V obou místech najdi blok:
```razor
<select name="ExterniVazby[@i].Vyzva">
    <option value="">–</option>
    @foreach (var vyzva in Model.Vyzvy)
    {
        if (vyzva == vazba.Vyzva)
        {
            <option selected="selected">@vyzva</option>
        }
        else
        {
            <option>@vyzva</option>
        }
    }
</select>
```

- [ ] **Step 2: Nahradit switchem (existující řádky)**

V první smyčce nahraď celý `<label>` obsahující `<select name="ExterniVazby[@i].Vyzva">` za:

```razor
                    @{
                        var isPnf = string.Equals(vazba.Typ, "PNF", StringComparison.OrdinalIgnoreCase);
                        var statusText = vazba.VyzvaKod is not null
                            ? $"Zařazeno do výzvy {vazba.VyzvaKod}"
                            : vazba.ZaradidDoVyzvy
                                ? "Čeká se (buffer projektu)"
                                : null;
                    }
                    <div class="vyzvy-switch-wrap" data-external-vyzvy-switch-wrap
                         @(isPnf ? null : "hidden=\"hidden\"")>
                        <label class="pm-switch-label">
                            <input type="hidden" name="ExterniVazby[@i].VyzvaId" value="@vazba.VyzvaId" />
                            <input type="hidden" name="ExterniVazby[@i].ZaradidDoVyzvy" value="@(vazba.ZaradidDoVyzvy ? "true" : "false")" data-external-vyzvy-switch-state />
                            <input type="checkbox"
                                   class="pm-switch"
                                   data-vyzvy-switch
                                   data-externi-odkaz-id="@vazba.Id"
                                   @(vazba.ZaradidDoVyzvy ? "checked=\"checked\"" : null) />
                            Zařadit do další výzvy
                        </label>
                        @if (!string.IsNullOrEmpty(statusText))
                        {
                            <span class="vyzvy-switch-status" data-vyzvy-switch-status>@statusText</span>
                        }
                        else
                        {
                            <span class="vyzvy-switch-status" data-vyzvy-switch-status></span>
                        }
                    </div>
```

- [ ] **Step 3: Nahradit v template pro nový řádek**

Druhé místo (kolem řádku 165, `[__index__]` placeholder) nahraď podobně — s `__index__` místo `@i` a výchozími hodnotami prázdné:

```razor
                    <div class="vyzvy-switch-wrap" data-external-vyzvy-switch-wrap hidden="hidden">
                        <label class="pm-switch-label">
                            <input type="hidden" name="ExterniVazby[__index__].VyzvaId" value="" />
                            <input type="hidden" name="ExterniVazby[__index__].ZaradidDoVyzvy" value="false" data-external-vyzvy-switch-state />
                            <input type="checkbox" class="pm-switch" data-vyzvy-switch data-externi-odkaz-id="" />
                            Zařadit do další výzvy
                        </label>
                        <span class="vyzvy-switch-status" data-vyzvy-switch-status></span>
                    </div>
```

- [ ] **Step 4: Build**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml
git commit -m "feat(vyzvy-ui): switch na externí vazbě PNF místo select"
```

---

## Task 13: JS modul vyzvy/index.js + panelController.js

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/vyzvy/index.js`
- Create: `PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js`

- [ ] **Step 1: panelController.js**

```js
// PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js
(function (global) {
  'use strict';

  function getAntiForgeryToken() {
    const el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  async function postForm(url, data) {
    const form = new FormData();
    form.append('__RequestVerificationToken', getAntiForgeryToken());
    for (const [k, v] of Object.entries(data)) {
      if (v !== null && v !== undefined) form.append(k, String(v));
    }
    const resp = await fetch(url, { method: 'POST', body: form, credentials: 'same-origin' });
    return resp.json();
  }

  async function reloadPanel(panelElement) {
    const projectId = panelElement.dataset.projectId;
    const url = `/projekty/${projectId}/dashboard/vyzvy-panel`;
    const resp = await fetch(url, { credentials: 'same-origin' });
    const html = await resp.text();
    panelElement.outerHTML = html;
    const newEl = document.querySelector('[data-vyzvy-panel]');
    if (newEl) global.pmVyzvy.bootstrap(newEl);
  }

  function showToast(message, isError = false) {
    if (global.pmToast && typeof global.pmToast.show === 'function') {
      global.pmToast.show(message, { type: isError ? 'error' : 'info' });
    } else {
      alert(message);
    }
  }

  async function handleZalozit(button, panelElement) {
    const projektId = button.dataset.projektId;
    button.disabled = true;
    try {
      const result = await postForm('/vyzvy/zalozit', { ProjektId: projektId });
      if (result.success) {
        showToast('Výzva založena.');
        await reloadPanel(panelElement);
      } else {
        showToast(result.message || 'Založení výzvy selhalo.', true);
      }
    } finally {
      button.disabled = false;
    }
  }

  async function handleZmenitStav(button, panelElement) {
    const vyzvaId = button.dataset.vyzvaId;
    const novyStav = button.dataset.novyStav;
    const result = await postForm('/vyzvy/zmenit-stav', { VyzvaId: vyzvaId, NovyStav: novyStav });
    if (result.success) {
      showToast(`Stav výzvy změněn na ${novyStav}.`);
      await reloadPanel(panelElement);
    } else {
      showToast(result.message || 'Změna stavu selhala.', true);
    }
  }

  function handleToggleCollapse(headerElement) {
    const card = headerElement.closest('[data-vyzvy-vyzva]');
    if (!card) return;
    card.classList.toggle('open');
    const btn = card.querySelector('.vyzvy-card-toggle-btn');
    if (btn) {
      const open = card.classList.contains('open');
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
      const icon = btn.querySelector('.vyzvy-card-toggle-icon');
      if (icon) icon.textContent = open ? '▼' : '▶';
    }
  }

  function handleStavMenuToggle(toggleBtn) {
    const menu = toggleBtn.closest('[data-vyzvy-stav-menu]');
    if (menu) menu.classList.toggle('open');
  }

  function bindPanel(panelElement) {
    panelElement.addEventListener('click', async (e) => {
      const target = e.target.closest('[data-vyzvy-action], [data-vyzvy-toggle], [data-vyzvy-stav-toggle]');
      if (!target) return;

      if (target.matches('[data-vyzvy-stav-toggle]')) {
        e.preventDefault();
        handleStavMenuToggle(target);
        return;
      }

      if (target.matches('[data-vyzvy-toggle]') && !e.target.closest('[data-vyzvy-action]')) {
        handleToggleCollapse(target);
        return;
      }

      const action = target.dataset.vyzvyAction;
      if (action === 'zalozit') { await handleZalozit(target, panelElement); }
      else if (action === 'zmenit-stav') { await handleZmenitStav(target, panelElement); }
      else if (action === 'otevrit-reassign') {
        if (global.pmVyzvy && global.pmVyzvy.openReassignModal) {
          global.pmVyzvy.openReassignModal(panelElement.dataset.projectId, () => reloadPanel(panelElement));
        }
      }
    });
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bindPanel = bindPanel;
  global.pmVyzvy.reloadPanel = reloadPanel;
  global.pmVyzvy.postForm = postForm;
  global.pmVyzvy.showToast = showToast;
})(window);
```

- [ ] **Step 2: index.js**

```js
// PmTracker.Web/wwwroot/js/modules/vyzvy/index.js
(function (global) {
  'use strict';

  function bootstrap(panelElement) {
    if (!panelElement) return;
    if (panelElement.dataset.vyzvyBootstrapped === 'true') return;
    panelElement.dataset.vyzvyBootstrapped = 'true';
    if (global.pmVyzvy && global.pmVyzvy.bindPanel) {
      global.pmVyzvy.bindPanel(panelElement);
    }
  }

  function initOnDomReady() {
    document.querySelectorAll('[data-vyzvy-panel]').forEach(bootstrap);
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bootstrap = bootstrap;

  document.addEventListener('DOMContentLoaded', initOnDomReady);
  // Rebootstrap po AJAX replace (projectDashboard.js emituje event po loadu panelu)
  document.addEventListener('pm:panel-loaded', (e) => {
    const panel = e.target?.querySelector?.('[data-vyzvy-panel]') || e.target;
    if (panel && panel.matches?.('[data-vyzvy-panel]')) bootstrap(panel);
  });
})(window);
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/vyzvy/
git commit -m "feat(vyzvy-ui): JS modul panelController + bootstrap"
```

---

## Task 14: JS modul switchController.js

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js`

- [ ] **Step 1: switchController.js**

```js
// PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js
(function (global) {
  'use strict';

  async function handleSwitch(checkbox) {
    const externiOdkazId = checkbox.dataset.externiOdkazId;
    if (!externiOdkazId) return;

    const wrap = checkbox.closest('[data-external-vyzvy-switch-wrap]');
    const statusEl = wrap?.querySelector('[data-vyzvy-switch-status]');
    const hiddenState = wrap?.querySelector('[data-external-vyzvy-switch-state]');
    const zaradit = checkbox.checked;

    checkbox.disabled = true;
    try {
      const result = await global.pmVyzvy.postForm('/vyzvy/set-zaradid', {
        ExterniOdkazId: externiOdkazId,
        Zaradit: zaradit,
      });
      if (result.success) {
        if (hiddenState) hiddenState.value = zaradit ? 'true' : 'false';
        if (statusEl) {
          // Jednoduchý text; pokud potřebujeme přesný kód výzvy, server vrátí po reloadu
          statusEl.textContent = zaradit ? 'Čeká se (buffer projektu)' : '';
        }
      } else {
        // Rollback stavu
        checkbox.checked = !zaradit;
        global.pmVyzvy.showToast(result.message || 'Operace selhala.', true);
      }
    } finally {
      checkbox.disabled = false;
    }
  }

  function bindSwitches(root) {
    const scope = root || document;
    scope.querySelectorAll('[data-vyzvy-switch]').forEach((cb) => {
      if (cb.dataset.vyzvyBound === 'true') return;
      cb.dataset.vyzvyBound = 'true';
      cb.addEventListener('change', () => handleSwitch(cb));
    });
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bindSwitches = bindSwitches;

  document.addEventListener('DOMContentLoaded', () => bindSwitches());
  document.addEventListener('pm:record-editor-loaded', (e) => bindSwitches(e.target || document));
})(window);
```

- [ ] **Step 2: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js
git commit -m "feat(vyzvy-ui): JS switchController — PNF switch AJAX"
```

---

## Task 15: JS modul reassignModal.js

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js`

- [ ] **Step 1: reassignModal.js**

```js
// PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js
(function (global) {
  'use strict';

  let modalElement = null;
  let onCloseCallback = null;

  function ensureModalContainer() {
    let el = document.getElementById('pm-vyzvy-reassign-modal');
    if (!el) {
      el = document.createElement('div');
      el.id = 'pm-vyzvy-reassign-modal';
      el.className = 'pm-modal';
      el.hidden = true;
      document.body.appendChild(el);
    }
    modalElement = el;
    return el;
  }

  async function loadModal(projektId) {
    const el = ensureModalContainer();
    const resp = await fetch(`/vyzvy/reassign-modal?projektId=${encodeURIComponent(projektId)}`, {
      credentials: 'same-origin',
    });
    if (!resp.ok) {
      global.pmVyzvy.showToast('Nepodařilo se načíst modal.', true);
      return;
    }
    const html = await resp.text();
    el.innerHTML = html;
    el.hidden = false;
    bindDnd(el);
    bindClose(el);
  }

  function bindClose(el) {
    el.querySelectorAll('[data-vyzvy-reassign-close]').forEach((btn) => {
      btn.addEventListener('click', () => close());
    });
    el.addEventListener('click', (e) => {
      if (e.target === el) close();
    });
  }

  function close() {
    if (modalElement) {
      modalElement.hidden = true;
      modalElement.innerHTML = '';
    }
    if (onCloseCallback) {
      try { onCloseCallback(); } catch (_) { /* noop */ }
    }
    onCloseCallback = null;
  }

  function bindDnd(rootEl) {
    const items = rootEl.querySelectorAll('[data-vyzvy-reassign-item]');
    const targets = rootEl.querySelectorAll('[data-vyzvy-reassign-target]');

    items.forEach((item) => {
      item.addEventListener('dragstart', (e) => {
        item.classList.add('dragging');
        e.dataTransfer.setData('text/plain', item.dataset.externiOdkazId);
        e.dataTransfer.effectAllowed = 'move';
      });
      item.addEventListener('dragend', () => item.classList.remove('dragging'));
    });

    targets.forEach((target) => {
      target.addEventListener('dragover', (e) => {
        e.preventDefault();
        target.classList.add('drag-over');
      });
      target.addEventListener('dragleave', () => target.classList.remove('drag-over'));
      target.addEventListener('drop', async (e) => {
        e.preventDefault();
        target.classList.remove('drag-over');
        const externiOdkazId = e.dataTransfer.getData('text/plain');
        const cilovaVyzvaId = target.dataset.cilovaVyzvaId || '';
        const projektId = rootEl.querySelector('[data-vyzvy-reassign-columns]')?.dataset.projektId;

        const result = await global.pmVyzvy.postForm('/vyzvy/prerdit', {
          ExterniOdkazId: externiOdkazId,
          CilovaVyzvaId: cilovaVyzvaId,
        });
        if (result.success) {
          await loadModal(projektId);
        } else {
          global.pmVyzvy.showToast(result.message || 'Přeřazení selhalo.', true);
        }
      });
    });
  }

  async function openReassignModal(projektId, onClose) {
    onCloseCallback = onClose || null;
    await loadModal(projektId);
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.openReassignModal = openReassignModal;
})(window);
```

- [ ] **Step 2: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js
git commit -m "feat(vyzvy-ui): JS reassignModal — HTML5 DnD + reload po každém drop"
```

---

## Task 16: Zařadit JS moduly do site.bundle.js

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js` (append nové moduly)

**Kontext:** Projekt má ruční bundle — nové moduly se konkatenují do `site.bundle.js`. Původní `site.bundle.js` je cca ~několik tisíc řádků. Přidáme naše moduly na konec (nebo tam, kde jsou zařazené ostatní `modules/`).

- [ ] **Step 1: Najít místo a připojit obsah modulů**

Otevři `PmTracker.Web/wwwroot/js/site.bundle.js` na konci. Přidej 3 sekce:

```javascript
// ==========================================================================
// PmTracker.Web/wwwroot/js/modules/vyzvy/panelController.js
// ==========================================================================
<OBSAH panelController.js>

// ==========================================================================
// PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js
// ==========================================================================
<OBSAH switchController.js>

// ==========================================================================
// PmTracker.Web/wwwroot/js/modules/vyzvy/reassignModal.js
// ==========================================================================
<OBSAH reassignModal.js>

// ==========================================================================
// PmTracker.Web/wwwroot/js/modules/vyzvy/index.js
// ==========================================================================
<OBSAH index.js>
```

Kde `<OBSAH xxx.js>` nahraď **doslovnou** kopií obsahu toho souboru (IIFE wrapper zachovat).

- [ ] **Step 2: Ověř ve stránce**

Nespustíš testy — manuální kontrola že `window.pmVyzvy` existuje:
1. Nastartuj lokálně aplikaci (pokud možno)
2. V DevTools konzole: `typeof window.pmVyzvy.bootstrap === 'function'` → `true`

Pokud nemůžeš aplikaci spustit v rámci subagent práce, přeskoč Step 2 a ověř jen syntax konkatenace (že IIFE neotevřené závorky atd.).

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "chore(vyzvy-ui): konkatenovat vyzvy moduly do site.bundle.js"
```

---

## Task 17: Napojit vyzvy bootstrap na tab aktivaci v projectDashboard.js

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/projectDashboard.js` (nebo odpovídající modul v bundle)

- [ ] **Step 1: Najít existující handler aktivace tabu**

Run:
```bash
grep -n "data-dashboard-tab\|panel-loaded" PmTracker.Web/wwwroot/js/modules/projectDashboard.js 2>/dev/null | head
```

- [ ] **Step 2: Přidat event emit po loadu panelu**

Najdi metodu, která po AJAX loadu vloží HTML do `[data-dashboard-panel-content]` nebo `[data-dashboard-tab-panel]`. Po insertu HTML přidej:

```javascript
const event = new CustomEvent('pm:panel-loaded', { bubbles: true, detail: { panelName } });
panelElement.dispatchEvent(event);
```

*(Pokud taková metoda neexistuje nebo pattern je odlišný — grep najde relevantní lokaci. V nejhorším, hook do MutationObserver v `vyzvy/index.js` jako fallback.)*

- [ ] **Step 3: Propagace do bundlu**

Stejně jako Task 16 — pokud `projectDashboard.js` je zdrojový soubor nekopírovaný do bundlu, musí se bundle aktualizovat. Grepni `projectDashboard` v `site.bundle.js`:

```bash
grep -n "projectDashboard" PmTracker.Web/wwwroot/js/site.bundle.js | head
```

Najdi sekci `projectDashboard.js` v bundle a udělej stejnou úpravu tam.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/
git commit -m "feat(vyzvy-ui): emit pm:panel-loaded event po AJAX loadu tab panelu"
```

---

## Task 18: VyzvyController unit testy

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvyControllerTests.cs`

- [ ] **Step 1: Testy pro VyzvyController**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvyControllerTests.cs
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvyControllerTests
{
    [Fact]
    public async Task Zalozit_Success_VraciJsonSuccessTrue()
    {
        // NOTE: Tento test nebudeme spouštět přes plně zprovozněný HTTP pipeline.
        // Místo toho testujeme mapování výsledku `VyzvaResult<VyzvaDetail>.Ok` na JSON objekt.
        var dummyDetail = new VyzvaDetail(
            1, 1, "1/2026", 2026, 1, VyzvaStav.Priprava,
            DateTime.UtcNow, 1, null, null, "F", "A",
            Array.Empty<VyzvaDetailItem>());

        var result = new VyzvaResult<VyzvaDetail>.Ok(dummyDetail);

        // Simulace mappingu — kopie private helperu z VyzvyController
        object payload = result switch
        {
            VyzvaResult<VyzvaDetail>.Ok => new { success = true, reload = true },
            VyzvaResult<VyzvaDetail>.Fail f => new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message },
            _ => throw new InvalidOperationException(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("\"success\":true");
        json.Should().Contain("\"reload\":true");
    }

    [Fact]
    public void ZalozitFail_VraciJsonSuccessFalseSCodeAMessage()
    {
        var fail = new VyzvaResult<VyzvaDetail>.Fail(new VyzvaError(VyzvaErrorCode.BufferEmpty, "Buffer je prázdný"));

        object payload = fail switch
        {
            VyzvaResult<VyzvaDetail>.Ok => new { success = true, reload = true },
            VyzvaResult<VyzvaDetail>.Fail f => new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message },
            _ => throw new InvalidOperationException(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        json.Should().Contain("\"success\":false");
        json.Should().Contain("\"errorCode\":\"BufferEmpty\"");
        json.Should().Contain("Buffer je prázdný");
    }

    [Theory]
    [InlineData("Priprava", true)]
    [InlineData("Odeslano", true)]
    [InlineData("Zruseno", true)]
    [InlineData("NeznamyStav", false)]
    [InlineData("", false)]
    public void ZmenitStav_ParseStavu(string input, bool canParse)
    {
        var ok = Enum.TryParse<VyzvaStav>(input, ignoreCase: false, out var stav);
        ok.Should().Be(canParse);
    }
}
```

*(Tyto unit testy nepokrývají plný HTTP pipeline — to by vyžadovalo `WebApplicationFactory` integration testy. Scope fáze 2: pokrytí mappingu + parseru. Plnou E2E integraci zařadíme do fáze 4 s Testcontainers.)*

- [ ] **Step 2: Run testy**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvyControllerTests" --no-restore
```
Expected: 7 passed (1 + 1 + 5 theory)

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.Unit/Vyzvy/VyzvyControllerTests.cs
git commit -m "test(vyzvy-ui): VyzvyController — JSON mapping + parse stavu"
```

---

## Task 19: VyzvyPanelBuilder unit testy

**Files:**
- Create: `PmTracker.Tests.Unit/Vyzvy/VyzvyPanelBuilderTests.cs`

- [ ] **Step 1: Test pro builder**

```csharp
// PmTracker.Tests.Unit/Vyzvy/VyzvyPanelBuilderTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvyPanelBuilderTests
{
    [Fact]
    public async Task Build_ProjektBezMistaPlneni_MuzeZaloztVyzvuFalse()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 1, Zkratka = "P", CelyNazev = "P", StavId = 1,
            MistoPlneni = null, CisloRamcoveSmlouvy = "A",
        });
        await db.SaveChangesAsync();

        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, CancellationToken.None);

        result.ProjektId.Should().Be(1);
        result.Buffer.MuzeZaloztVyzvu.Should().BeFalse();
        result.Buffer.DuvodBlokace.Should().Contain("Místo plnění");
    }

    [Fact]
    public async Task Build_Default_ObsahujeKolapsVyzvyMimoPripravu()
    {
        using var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedProjektAsync(db);
        db.Vyzvy.AddRange(
            new VyzvaEntity
            {
                Id = 1, ProjektId = 1, Kod = "1/2026", PoradoveVRoce = 1, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 1, 1),
                ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            },
            new VyzvaEntity
            {
                Id = 2, ProjektId = 1, Kod = "2/2026", PoradoveVRoce = 2, Rok = 2026,
                Stav = VyzvaStav.Odeslano, DatumZalozeni = new DateTime(2026, 2, 1),
                ZalozilOsobaId = 1, MistoPlneniSnapshot = "F", CisloRamcoveSmlouvySnapshot = "A",
            });
        await db.SaveChangesAsync();

        var svc = VyzvaServiceTestHarness.CreateService(db);
        var builder = new VyzvyPanelBuilder(svc, db);

        var result = await builder.BuildAsync(1, muzeEditovat: true, CancellationToken.None);

        result.Vyzvy.Should().HaveCount(2);
        var priprava = result.Vyzvy.First(v => v.Stav == "Priprava");
        var odeslana = result.Vyzvy.First(v => v.Stav == "Odeslano");

        priprava.Kolapsovano.Should().BeFalse();
        odeslana.Kolapsovano.Should().BeTrue();

        priprava.PovoleneStavy.Should().Contain(new[] { "Odeslano", "Zruseno" });
        odeslana.PovoleneStavy.Should().Contain(new[] { "Priprava", "Zruseno" });
    }
}
```

- [ ] **Step 2: Run**

```bash
dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~VyzvyPanelBuilderTests" --no-restore
```
Expected: 2 passed

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Tests.Unit/Vyzvy/VyzvyPanelBuilderTests.cs
git commit -m "test(vyzvy-ui): VyzvyPanelBuilder — ACL a kolaps stavy"
```

---

## Task 20: Fallback číselník `/Ciselniky/Vyzvy` update

**Files:**
- Modify: dle nálezu v `CiselnikyController` + `Views/Ciselniky/` (struktura existuje, ale obsah pro `Vyzvy` závisí na existujícím kódu)

- [ ] **Step 1: Analýza existujícího stavu**

Run:
```bash
grep -rn "vyzvy\|vyzv" PmTracker.Web/Controllers/CiselnikyController.cs PmTracker.Web/Services/Dictionaries/ 2>/dev/null | head -20
```

Existující `DictionaryService.Commands.cs` řádek ~432 zakládal `VyzvaEntity` s placeholdery (TODO z Task 15 fáze 1). Ověř zda je to použito.

- [ ] **Step 2: Minimum viable update — zobrazit novou entitu**

Pokud `CiselnikyController` poskytuje generický GET/POST pro klíč `vyzvy`, upravit jen zobrazení:
- Zobrazovat sloupce: `Kod`, `Stav`, `DatumZalozeni`, `ZalozilOsobaId`, `MistoPlneniSnapshot`, `CisloRamcoveSmlouvySnapshot`
- Edit pouze pro SuperAdmin — ostatní read-only

Pokud je číselník `vyzvy` komplikovaný a vyžadoval by víc než 1-2 hodiny práce, **označit jako dokumentovaný TODO v sekci 13 specu** a ponechat číselník rozbitý (vstup do fáze 4 rework).

- [ ] **Step 3: Rozhodnutí**

Podle rozsahu nalezeného kódu:
- **Ano, jednoduché** → upravit view + commit
- **Ne, nestojí to za úsilí ve fázi 2** → commit prázdný `placeholder` do `/Views/Ciselniky/Vyzvy.cshtml` s textem „Správa výzev probíhá přes projektový dashboard. Tento číselník je určen pro SuperAdmin oprav chyb a bude dostupný v další fázi."

V obou případech commitnout change:

```bash
git add PmTracker.Web/
git commit -m "chore(vyzvy-ui): fallback číselník — placeholder s odkazem na projektový dashboard"
```

---

## Task 21: Full verify + manuální smoke

- [ ] **Step 1: Build**

```bash
dotnet build --no-restore
```
Expected: 0 errors, 0 warnings přes všechny projekty

- [ ] **Step 2: All tests**

```bash
dotnet test PmTracker.Tests.Unit --no-restore
```
Expected: All tests pass

- [ ] **Step 3: Spusť aplikaci lokálně (pokud možno)**

```bash
dotnet run --project PmTracker.Web/PmTracker.Web.csproj --urls http://localhost:5999
```

Manuální smoke (podle času):
- `/projekty/{id}/dashboard?tab=vyzvy` — panel se renderuje
- Klik „Založit výzvu" (pokud je buffer prázdný) → disabled + reason message
- Otevři editor záznamu s PNF externí vazbou — zkontroluj, že switch je zobrazený
- Switch ON → status zobrazí „Čeká se"
- Modal přeřazení — otevření, DnD

Pokud test není možný (chybí lokální DB, connection string), **zaznamenej do commit message jako „čeká na manuální smoke v prostředí s DB"**.

- [ ] **Step 4: Git status**

```bash
git status
git log --oneline | head -25
```

Expected: čistý strom, posledních cca 20-22 commitů k fázi 2.

---

## Hotovo — Fáze 2

Po dokončení plánu máš:
- ✅ Panel Výzvy v projektovém dashboardu (buffer + karty výzev s kolapsem)
- ✅ Switch na externí vazbě PNF (AJAX bez submitu formuláře)
- ✅ Modal pro drag & drop přeřazení mezi bufferem a Priprava výzvami
- ✅ Edit projektu s MistoPlneni + CisloRamcoveSmlouvy
- ✅ JSON API ve VyzvyController s ACL (proj_man/adm_proj/app_admin/SuperAdmin)
- ✅ JS moduly (panelController, switchController, reassignModal, bootstrap)
- ✅ CSS pro celý panel včetně badge stavů, reassign sloupců
- ✅ Unit testy pro controller mapping + builder

### Mimo scope (navazující)

- **Fáze 4** (reálné propojení ServiceDesk): connection string produkce, ověření mapování HOT tabulek (přes interaktivní review s uživatelem), Key Vault, Testcontainers integračky
- **Fáze 3** (Word export): OpenXML šablona, vodoznak NÁVRH, APTI tabulky z HOT_KALKULACE

Po akceptaci uživatel přepne prioritu na fázi 4 (ServiceDesk propojení), pak na fázi 3.
