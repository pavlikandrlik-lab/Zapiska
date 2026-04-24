# Sprint B — projekt→IS vazba + NES dashboard aktivace implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Přidat na projekt vazbu na Informační systém (FIS/ISSP hardcoded číselník) + aktivovat NES panel v projektovém dashboardu, který zobrazí tickety v prodlení pro IS napojený na projekt.

**Architecture:**
1. **DB migrace** — `projekty.ServiceDeskInfoSystemId INT NULL`
2. **Hardcoded číselník IS** — `SdInfoSystemy` static class (FIS, ISSP) — žádný runtime SD dotaz, jen lokální enum-like katalog
3. **Edit projektu** — dropdown IS v modalu úpravy projektu (stejný permission `projects.edit` dle C-Q1/U2)
4. **Dashboard NES panel** — aktivuje se pokud projekt má `ServiceDeskInfoSystemId`, volá `IInformacniSystemQueryService.GetProdleneAsync(isId, DateTime.UtcNow, ct)` (Sprint A hotové)
5. **URL builder** — reuse `BuildServiceDeskUrl` z `ProjectService.RecordComposition.cs:243-251`

**Tech Stack:** .NET 8, EF Core 8, existing dashboard infra (`ProjectDashboardService.BuildNesPanel`), existing Sprint A query service.

**Spec source:**
- [2026-04-16-projektovy-dashboard-design.md §98-129, §217-244](../specs/2026-04-16-projektovy-dashboard-design.md)
- Decision brief: U5 (per záznam switch), finální rozhodnutí Sprint B jako 5. PR
- Memory: `project_servicedesk_infosystem_binding.md` → „Terminologie" + „IS jako hardkódovaný číselník"

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `db_upgrade_1_3_11_projekty_infosystem.sql` | DB migrace — sloupec `ServiceDeskInfoSystemId` |
| `PmTracker.Web/Services/ServiceDesk/SdInfoSystemy.cs` | Static katalog (FIS=1, ISSP=2 — reálné HOT_IS.ID hodnoty) |
| `PmTracker.Web/Services/ServiceDesk/SdInfoSystemyTests.cs` | Unit testy katalogu |
| `PmTracker.Web/Views/Projekty/_EditInfoSystemSelect.cshtml` | Partial — dropdown IS v edit modalu |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Models/Entities/ProjektEntity.cs` | `+ int? ServiceDeskInfoSystemId` |
| `PmTracker.Web/Data/PmTrackerDbContext.cs` | EF mapping nového sloupce |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektEditViewModel.cs` | `+ int? ServiceDeskInfoSystemId` property |
| `PmTracker.Web/Controllers/ProjektyController.cs` | Edit GET: load IS seznam; Edit POST: save ServiceDeskInfoSystemId |
| `PmTracker.Web/Views/Projekty/Edit.cshtml` | Render `_EditInfoSystemSelect.cshtml` partial |
| `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs` | `BuildNesPanel`: unhardcode `IsServiceDeskIntegrated=false`, volat query service |
| `PmTracker.Web/Models/ViewModels/ProjectDashboard/ProjectDashboardNesPanelViewModel.cs` | Rozšířit o `IReadOnlyList<NesPanelItemViewModel> Items` |
| `PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml` | Render tabulky ticketů v prodlení |

---

## Tasks

### Task 1: DB migrace + entity

**Files:**
- Create: `db_upgrade_1_3_11_projekty_infosystem.sql`
- Modify: `PmTracker.Web/Models/Entities/ProjektEntity.cs`
- Modify: `PmTracker.Web/Data/PmTrackerDbContext.cs`

- [ ] **Step 1: SQL migrace**

```sql
-- db_upgrade_1_3_11_projekty_infosystem.sql
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.projekty') AND name = 'ServiceDeskInfoSystemId')
BEGIN
    ALTER TABLE dbo.projekty
        ADD ServiceDeskInfoSystemId INT NULL;
    PRINT 'Added projekty.ServiceDeskInfoSystemId column.';
END
ELSE
    PRINT 'projekty.ServiceDeskInfoSystemId already exists, skipping.';
GO

-- Žádný FK na intranetNEW.dbo.HOT_IS — cizí DB, jen logická vazba.
-- Hodnota = HOT_IS.ID (1 = FIS, 2 = ISSP; ověřit v SdInfoSystemy konstantách).

-- Sanity
SELECT COUNT(*) AS total_projekty,
       SUM(CASE WHEN ServiceDeskInfoSystemId IS NULL THEN 1 ELSE 0 END) AS bez_IS_napojeni
FROM dbo.projekty;
GO
```

- [ ] **Step 2: Entity**

V `ProjektEntity.cs` přidat:

```csharp
public int? ServiceDeskInfoSystemId { get; set; }
```

V `PmTrackerDbContext.cs` v mapping projektu:

```csharp
entity.Property(x => x.ServiceDeskInfoSystemId)
    .HasColumnName("ServiceDeskInfoSystemId");
// Žádný FK — cizí DB (intranetNEW)
```

- [ ] **Step 3: Migrace lokálně + build**

```bash
sqlcmd -S localhost -d PM_Tracker_VYVOJ -i db_upgrade_1_3_11_projekty_infosystem.sql
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add db_upgrade_1_3_11_projekty_infosystem.sql \
        PmTracker.Web/Models/Entities/ProjektEntity.cs \
        PmTracker.Web/Data/PmTrackerDbContext.cs
git commit -m "feat(projekt): DB migrace ServiceDeskInfoSystemId INT NULL"
```

---

### Task 2: `SdInfoSystemy` hardcoded katalog

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/SdInfoSystemy.cs`
- Create: `PmTracker.Tests.Unit/Services/ServiceDesk/SdInfoSystemyTests.cs`

- [ ] **Step 1: Katalog**

```csharp
// PmTracker.Web/Services/ServiceDesk/SdInfoSystemy.cs
namespace PmTracker.Web.Services.ServiceDesk;

public sealed record SdInfoSystem(int Id, string Zkratka, string Nazev);

/// <summary>
/// Hardcoded katalog Informačních systémů v PM Trackeru (dle decision brief C-Q3).
/// 2026-04-24: SD produkce má 3 záznamy v HOT_IS (FIS, ISSP, X_FIS).
/// X_FIS je mimo scope PM Trackeru (user rozhodnutí).
///
/// Hodnoty Id odpovídají HOT_IS.ID v intranetNEW. **Před deployem ověř lokálně:**
///    SELECT ID, zkratka, nazev FROM intranetNEW.dbo.HOT_IS
///    WHERE zkratka IN ('FIS', 'ISSP') ORDER BY ID;
///
/// Pokud ID v SD jsou jiné než 1 a 2, uprav konstanty níže.
/// </summary>
public static class SdInfoSystemy
{
    public const int FisId = 1;
    public const int IsspId = 2;

    public static readonly IReadOnlyList<SdInfoSystem> Vychozi = new[]
    {
        new SdInfoSystem(FisId, "FIS", "Finanční informační systém"),
        new SdInfoSystem(IsspId, "ISSP", "Informační systém sociálních podmínek"),
    };

    public static SdInfoSystem? ById(int id) => Vychozi.FirstOrDefault(x => x.Id == id);
    public static bool IsSupported(int? id) => id is int n && Vychozi.Any(x => x.Id == n);
}
```

- [ ] **Step 2: Unit testy**

```csharp
using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.Services.ServiceDesk;

public sealed class SdInfoSystemyTests
{
    [Fact]
    public void Vychozi_Ma2Polozky()
    {
        SdInfoSystemy.Vychozi.Should().HaveCount(2);
        SdInfoSystemy.Vychozi.Select(x => x.Zkratka).Should().Equal("FIS", "ISSP");
    }

    [Fact]
    public void ById_ExistingId_ReturnsSystem()
    {
        var fis = SdInfoSystemy.ById(SdInfoSystemy.FisId);
        fis.Should().NotBeNull();
        fis!.Zkratka.Should().Be("FIS");
    }

    [Fact]
    public void ById_UnknownId_ReturnsNull()
    {
        SdInfoSystemy.ById(999).Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]  // X_FIS out of scope
    [InlineData(999, false)]
    public void IsSupported_ReturnsCorrectly(int? id, bool expected)
    {
        SdInfoSystemy.IsSupported(id).Should().Be(expected);
    }
}
```

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SdInfoSystemy" --no-restore`
Expected: PASS 7/7.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/SdInfoSystemy.cs \
        PmTracker.Tests.Unit/Services/ServiceDesk/SdInfoSystemyTests.cs
git commit -m "feat(sd): hardcoded katalog SdInfoSystemy (FIS, ISSP)"
```

---

### Task 3: Edit projektu — dropdown IS

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektEditViewModel.cs`
- Modify: `PmTracker.Web/Controllers/ProjektyController.cs` (GET Edit + POST Edit)
- Create: `PmTracker.Web/Views/Projekty/_EditInfoSystemSelect.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/Edit.cshtml`

- [ ] **Step 1: VM**

V `ProjektEditViewModel`:

```csharp
public int? ServiceDeskInfoSystemId { get; set; }
public IReadOnlyList<PmTracker.Web.Services.ServiceDesk.SdInfoSystem> InfoSystemy { get; set; }
    = Array.Empty<PmTracker.Web.Services.ServiceDesk.SdInfoSystem>();
```

- [ ] **Step 2: Controller GET Edit naplní InfoSystemy**

V `ProjektyController.Edit` (GET):

```csharp
vm.InfoSystemy = PmTracker.Web.Services.ServiceDesk.SdInfoSystemy.Vychozi;
vm.ServiceDeskInfoSystemId = projekt.ServiceDeskInfoSystemId;
```

- [ ] **Step 3: Controller POST Edit uloží + validuje**

```csharp
if (model.ServiceDeskInfoSystemId.HasValue
    && !PmTracker.Web.Services.ServiceDesk.SdInfoSystemy.IsSupported(model.ServiceDeskInfoSystemId))
{
    ModelState.AddModelError(nameof(model.ServiceDeskInfoSystemId),
        "Zvolený Informační systém není v katalogu.");
    return View(model);
}

projekt.ServiceDeskInfoSystemId = model.ServiceDeskInfoSystemId;
await _db.SaveChangesAsync(ct);
```

- [ ] **Step 4: Partial view**

```cshtml
@* PmTracker.Web/Views/Projekty/_EditInfoSystemSelect.cshtml *@
@model PmTracker.Web.Models.ViewModels.Projekty.ProjektEditViewModel

<div class="form-group">
    <label asp-for="ServiceDeskInfoSystemId">Informační systém (ServiceDesk)</label>
    <select asp-for="ServiceDeskInfoSystemId"
            class="form-control"
            aria-describedby="infosystem-help">
        <option value="">— bez napojení —</option>
        @foreach (var is_ in Model.InfoSystemy)
        {
            <option value="@is_.Id">@is_.Zkratka — @is_.Nazev</option>
        }
    </select>
    <small id="infosystem-help" class="form-text text-muted">
        Napojení projektu na IS v ServiceDesku. Určuje, které tickety (NES/PMP/PNF)
        se zobrazí v dashboardu jako v prodlení.
    </small>
    <span asp-validation-for="ServiceDeskInfoSystemId" class="text-danger"></span>
</div>
```

- [ ] **Step 5: Include partial do Edit.cshtml**

```cshtml
@* V Edit.cshtml na vhodném místě formuláře: *@
<partial name="_EditInfoSystemSelect" model="Model" />
```

- [ ] **Step 6: Build + test**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore
```

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ProjektEditViewModel.cs \
        PmTracker.Web/Controllers/ProjektyController.cs \
        PmTracker.Web/Views/Projekty/_EditInfoSystemSelect.cshtml \
        PmTracker.Web/Views/Projekty/Edit.cshtml
git commit -m "feat(projekt): dropdown IS v edit modalu + validace katalogu"
```

---

### Task 4: Dashboard NES panel aktivace

**Files:**
- Modify: `PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs`
- Modify: `PmTracker.Web/Models/ViewModels/ProjectDashboard/ProjectDashboardNesPanelViewModel.cs`
- Modify: `PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml`

- [ ] **Step 1: VM rozšíření**

```csharp
public sealed class ProjectDashboardNesPanelViewModel
{
    public bool IsServiceDeskIntegrated { get; init; }
    public int? ServiceDeskInfoSystemId { get; init; }
    public string? IsZkratka { get; init; }
    public IReadOnlyList<NesPanelItemViewModel> Items { get; init; } = Array.Empty<NesPanelItemViewModel>();
    public int PocetVProdleni { get; init; }
    public double PrumerneProdleniDni { get; init; }
}

public sealed class NesPanelItemViewModel
{
    public required int TicketId { get; init; }
    public required string Pid { get; init; }
    public required string TypZaznamu { get; init; }
    public string? Strucne { get; init; }
    public string? Dodavatel { get; init; }
    public required DateTime Termin { get; init; }
    public required int DniProdleni { get; init; }
    public string? Stav { get; init; }
    public string? ServiceDeskUrl { get; init; }  // přes ProjectService.RecordComposition.BuildServiceDeskUrl
}
```

- [ ] **Step 2: Service BuildNesPanel**

V `ProjectDashboardService`:

```csharp
private readonly IInformacniSystemQueryService _isQueryService;  // Sprint A hotové

public async Task<ProjectDashboardNesPanelViewModel> BuildNesPanel(int projektId, CancellationToken ct)
{
    var projekt = await _db.Projekty.AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == projektId, ct);

    if (projekt?.ServiceDeskInfoSystemId is null)
    {
        return new ProjectDashboardNesPanelViewModel
        {
            IsServiceDeskIntegrated = false
        };
    }

    var isId = projekt.ServiceDeskInfoSystemId.Value;
    var infoSystem = SdInfoSystemy.ById(isId);

    var prodlene = await _isQueryService.GetProdleneAsync(isId, DateTime.UtcNow, ct);

    var items = prodlene.Select(p => new NesPanelItemViewModel
    {
        TicketId = p.Id,
        Pid = p.Pid,
        TypZaznamu = p.TypZaznamu,
        Strucne = p.Strucne,
        Dodavatel = p.Dodavatel,
        Termin = p.Termin,
        DniProdleni = p.DniProdleni,
        Stav = p.Stav,
        ServiceDeskUrl = $"https://servicedesk.fis.acr/Hotline/Ticket/Details/{p.Id}"
        // memory: stejný formát jako ProjectService.RecordComposition.BuildServiceDeskUrl
    }).ToList();

    return new ProjectDashboardNesPanelViewModel
    {
        IsServiceDeskIntegrated = true,
        ServiceDeskInfoSystemId = isId,
        IsZkratka = infoSystem?.Zkratka,
        Items = items,
        PocetVProdleni = items.Count,
        PrumerneProdleniDni = items.Count > 0 ? items.Average(x => x.DniProdleni) : 0.0
    };
}
```

- [ ] **Step 3: View**

```cshtml
@* PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml *@
@model PmTracker.Web.Models.ViewModels.ProjectDashboard.ProjectDashboardNesPanelViewModel

@if (!Model.IsServiceDeskIntegrated)
{
    <section class="pm-nes-panel pm-nes-panel--disabled">
        <p class="muted">
            Projekt není napojen na žádný Informační systém v ServiceDesku.
            <a asp-controller="Projekty" asp-action="Edit">Nastav napojení</a> pro zobrazení NES v prodlení.
        </p>
    </section>
}
else
{
    <section class="pm-nes-panel">
        <header class="pm-nes-panel__header">
            <h2>NES v prodlení — @Model.IsZkratka</h2>
            <div class="pm-nes-panel__kpi">
                <span>V prodlení: <strong>@Model.PocetVProdleni</strong></span>
                <span>Průměr dnů: <strong>@Model.PrumerneProdleniDni.ToString("F1")</strong></span>
            </div>
        </header>

        @if (Model.Items.Count == 0)
        {
            <p class="muted">Žádné tickety v prodlení.</p>
        }
        else
        {
            <table class="pm-nes-panel__table">
                <thead>
                    <tr>
                        <th>Ticket</th>
                        <th>Typ</th>
                        <th>Stručně</th>
                        <th>Dodavatel</th>
                        <th>Termín</th>
                        <th>Prodlení</th>
                        <th>Stav</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var item in Model.Items)
                    {
                        <tr>
                            <td>
                                <a href="@item.ServiceDeskUrl" target="_blank" rel="noopener noreferrer">
                                    #@item.TicketId
                                </a>
                            </td>
                            <td>@item.TypZaznamu</td>
                            <td>@item.Strucne</td>
                            <td>@item.Dodavatel</td>
                            <td>@item.Termin.ToString("dd.MM.yyyy")</td>
                            <td><strong>@item.DniProdleni</strong> dní</td>
                            <td>@item.Stav</td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </section>
}
```

- [ ] **Step 4: Unit test `BuildNesPanel`**

Napsat test, který:
1. Projekt bez `ServiceDeskInfoSystemId` → `IsServiceDeskIntegrated = false`
2. Projekt s IS ID + query service vrací prázdný list → `IsServiceDeskIntegrated = true, PocetVProdleni = 0`
3. Projekt s IS ID + 3 prodlené → správné mapování + průměr dnů

Použít mock `IInformacniSystemQueryService`.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ProjectDashboard/ProjectDashboardService.cs \
        PmTracker.Web/Models/ViewModels/ProjectDashboard/ProjectDashboardNesPanelViewModel.cs \
        PmTracker.Web/Views/ProjectDashboard/_NesPanel.cshtml \
        PmTracker.Tests.Unit/Services/ProjectDashboard/ProjectDashboardServiceNesPanelTests.cs
git commit -m "feat(dashboard): aktivace NES panelu s daty z Sprint A query service"
```

---

### Task 5: Migrace existujících projektů na IS (manuální)

**Files:**
- Create: `docs/technical/14-infosystem-migration-manual.md` (postup pro admina)

Dle decision brief: Sprint B nezavádí automatickou heuristiku. Admin ručně přiřadí každému projektu IS.

- [ ] **Step 1: Dokumentovat manuální postup**

```markdown
# Manuální migrace projektů na IS — postup

Po nasazení Sprint B DB migrace `db_upgrade_1_3_11_projekty_infosystem.sql` mají všechny existující projekty `ServiceDeskInfoSystemId = NULL`.

## Postup

1. Admin (role SUPERADMIN nebo APP_ADMIN) otevře přehled projektů
2. Pro každý projekt otevře Edit modal → v dropdownu „Informační systém" vybere FIS nebo ISSP dle doménového kontextu
3. Uloží

## Heuristika (doporučení):
- Projekty týkající se finančních modulů → **FIS**
- Projekty týkající se personálních modulů → **ISSP**
- Interní projekty bez vazby na SD → ponechat **— bez napojení —**

## SQL pro batch migrace (volitelné, opatrné)

```sql
-- Ukázka — uprav WHERE dle skutečné heuristiky
UPDATE dbo.projekty
SET ServiceDeskInfoSystemId = 1  -- FIS
WHERE nazev LIKE '%FIS%' AND ServiceDeskInfoSystemId IS NULL;
```
```

- [ ] **Step 2: Commit**

```bash
git add docs/technical/14-infosystem-migration-manual.md
git commit -m "docs(sd): manuální postup migrace projektů na IS"
```

---

### Task 6: Final validace

- [ ] **Step 1: Full build + test**

```bash
dotnet build -c Release --no-restore
dotnet test PmTracker.Tests.Unit -c Release --no-restore
```

- [ ] **Step 2: Manuální smoke**

1. Otevři Edit modal projektu → zvol FIS/ISSP → ulož
2. Otevři `/ProjectDashboard/{projektId}` → NES panel viditelný s tickety v prodlení (nebo „žádné tickety v prodlení")
3. Klik na ticket ID → otevře se ServiceDesk v novém tabu
4. Projekt bez napojení → panel zobrazuje „napoj na IS" zprávu + odkaz

- [ ] **Step 3: Final commit**

---

## Deliverable

- ✅ DB migrace 1_3_11 — `projekty.ServiceDeskInfoSystemId`
- ✅ `SdInfoSystemy` hardcoded katalog (FIS + ISSP, X_FIS out)
- ✅ Edit projektu: dropdown IS + validace
- ✅ `BuildNesPanel` aktivován (unhardcoded) s daty z Sprint A query service
- ✅ NES panel UI: tabulka ticketů v prodlení + KPI (počet, průměr dnů)
- ✅ Hyperlink ticketů do reálného ServiceDesku
- ✅ Graceful state pro projekty bez napojení (banner + odkaz na Edit)
- ✅ Dokumentace pro manuální migraci existujících projektů
