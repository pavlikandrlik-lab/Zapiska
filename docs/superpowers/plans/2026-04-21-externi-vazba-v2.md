# Karta externí vazby v2 + auto-sync ServiceDesk Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přepsat kartu externí vazby v editoru záznamu do kompaktního layoutu (Číslo → Typ read-only → Cena → Výzva switch → ikonová tlačítka 🗑 + 💬) s auto-sync se ServiceDeskem po zadání 6místného čísla. Tlačítko 💬 zatím vede na modal, který jen říká „v přípravě" (skutečný chat modal se dělá v Plánu C).

**Architecture:** View `_EditZaznamExternalPanel.cshtml` se přerenderuje do nové struktury (2 řádky per karta: horní = inputy, dolní = 3 read-only datumy). `ExterniOdkazEditViewModel` dostane nové pole `LastHarvestedAt`. Nový controller endpoint `POST /Zaznamy/ExterniOdkaz/Sync` volá existující `ITicketingQueryService.GetZaznamAsync` + aplikuje textové predikáty na `HOT_VYJADRENI` (stub — pro Plán B implementujeme jen lookup typu+strucne; samotné vytěžování datumů naplní Plán C). UI JS modul `externiOdkazSync.js` debouncuje input pro 6místné číslo a volá endpoint. Ikona 🗑 nahradí stávající „Odebrat vazbu" text button. Ikona 💬 zatím otevírá `gov-dialog` s textem „Chat modal bude dostupný v další fázi".

**Tech Stack:** .NET 8 ASP.NET Core MVC + Razor, Gov Design System 4.2.9 (`gov-form-input`, `gov-button`, `gov-icon`, `gov-switch`), manuální JS bundle (`site.bundle.js`), EF Core 8, xUnit + FluentAssertions + Playwright.

**Předpoklad:** Plán A (fakturace cleanup) již proběhl — harmonogram má 10 kroků.

---

## File Structure

### Nové soubory
- `db_upgrade_1_2_0_external_link_harvested_at.sql` — přidá sloupec `last_harvested_at datetime2 NULL` do `dbo.zaznam_externi_odkazy`.
- `PmTracker.Web/Controllers/ExterniOdkazController.cs` — nový controller pro `POST /ExterniOdkaz/Sync`.
- `PmTracker.Web/Models/ViewModels/ExterniOdkaz/ExterniOdkazSyncResponse.cs` — DTO pro sync response.
- `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js` — debounced input handler + AJAX call + DOM update.
- `PmTracker.Web/wwwroot/js/modules/externiOdkaz/chatModalStub.js` — dočasný otvírač „v přípravě" modalu.
- `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css` — nový grid layout karty (dva řádky).
- `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncControllerTests.cs` — unit testy endpointu.
- `PmTracker.Tests.Unit/ExterniOdkaz/ExterniVazbaCardRenderingTests.cs` — render snapshot testy cshtml.

### Modifikované soubory
- `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (`ZaznamExterniOdkazEntity` section) — přidat `LastHarvestedAt datetime2?`.
- `PmTracker.Web/Data/Configuration/ZaznamExterniOdkazEntityConfiguration.cs` — namapovat nový sloupec.
- `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs` (`ExterniOdkazEditViewModel`) — přidat `LastHarvestedAt` + zrušit zobrazení `PlanDodani` jako inputu (zůstane readonly datum).
- `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml` — kompletní re-layout (viz Task 5).
- `PmTracker.Web/wwwroot/js/site.bundle.js` — inline nové JS moduly + bootstrap hook.
- `PmTracker.Web/wwwroot/css/site.css` — `@import "components/externi-odkaz-card.css"`.

### Soubory, které se záměrně NEMĚNÍ
- `RecordService.SaveRecord.cs` — persistence externích vazeb zůstává stejná; nová data jdou stejnou cestou.
- `PmTracker.ServiceDesk.Contracts/ITicketingQueryService.cs` — už obsahuje `GetZaznamAsync`; nepřidáváme nic.
- Chat modal samotný — dělá se v Plánu C.

---

## Pořadí úkolů

1. **Task 1** — DB sloupec `LastHarvestedAt` + entity + konfigurace.
2. **Task 2** — Rozšířit `ExterniOdkazEditViewModel` a save mapping.
3. **Task 3** — Nový controller `ExterniOdkazController` + endpoint `/Sync`.
4. **Task 4** — CSS grid pro novou kartu (`externi-odkaz-card.css`).
5. **Task 5** — Re-render `_EditZaznamExternalPanel.cshtml` do nové struktury.
6. **Task 6** — JS modul `sync.js` (debounce 6 číslic → AJAX → DOM update).
7. **Task 7** — JS modul `chatModalStub.js` (otvírá stub modal).
8. **Task 8** — Integrovat moduly do `site.bundle.js` + bootstrap.
9. **Task 9** — Playwright visual smoke test nové karty.
10. **Task 10** — Full build + test + git clean.

---

## Task 1: DB sloupec `LastHarvestedAt` + entity

**Files:**
- Create: `db_upgrade_1_2_0_external_link_harvested_at.sql`
- Modify: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (`ZaznamExterniOdkazEntity`)
- Modify: `PmTracker.Web/Data/Configuration/ZaznamExterniOdkazEntityConfiguration.cs`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazLastHarvestedAtTests.cs`

- [ ] **Step 1: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazLastHarvestedAtTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniOdkazLastHarvestedAtTests
{
    [Fact]
    public void ZaznamExterniOdkazEntity_ShouldExposeLastHarvestedAt()
    {
        var entity = new ZaznamExterniOdkazEntity
        {
            LastHarvestedAt = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc)
        };
        entity.LastHarvestedAt.Should().Be(new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ExterniOdkazLastHarvestedAtTests" --no-restore`

Expected: FAIL — property neexistuje.

- [ ] **Step 3: Najít `ZaznamExterniOdkazEntity` v `PmTrackerEntities.cs`**

Run: `grep -n "public sealed class ZaznamExterniOdkazEntity" PmTracker.Web/Models/Entities/PmTrackerEntities.cs`

Expected: najdeš třídu (např. kolem řádku 295).

- [ ] **Step 4: Přidat property**

V `ZaznamExterniOdkazEntity` za poslední property přidej:

```csharp
public DateTime? LastHarvestedAt { get; set; }
```

- [ ] **Step 5: Přidat mapování v konfiguraci**

Otevři `PmTracker.Web/Data/Configuration/ZaznamExterniOdkazEntityConfiguration.cs`.

V `Configure(EntityTypeBuilder<ZaznamExterniOdkazEntity> builder)` přidej:

```csharp
builder.Property(x => x.LastHarvestedAt)
    .HasColumnName("last_harvested_at")
    .IsRequired(false);
```

- [ ] **Step 6: Napsat SQL upgrade skript**

Vytvoř `db_upgrade_1_2_0_external_link_harvested_at.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_2_0_external_link_harvested_at.sql
-- Přidá sloupec last_harvested_at do zaznam_externi_odkazy pro tracking
-- posledního auto-harvestu ze ServiceDesku.
-- Spec: docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §4.2
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.zaznam_externi_odkazy')
      AND name = N'last_harvested_at'
)
BEGIN
    ALTER TABLE dbo.zaznam_externi_odkazy
    ADD last_harvested_at DATETIME2 NULL;

    PRINT N'Sloupec last_harvested_at přidán.';
END
ELSE
BEGIN
    PRINT N'Sloupec last_harvested_at už existuje, přeskakuji.';
END;
GO
```

- [ ] **Step 7: Build + test**

Run: `dotnet build PmTracker.Web --no-restore && dotnet test PmTracker.Tests.Unit --filter "ExterniOdkazLastHarvestedAtTests" --no-restore`

Expected: 0 build errors, 1 test passed.

- [ ] **Step 8: Spustit upgrade skript proti Dev DB**

```bash
cd /tmp/run-sql && dotnet run "/Users/Pavel.Andrlik/Documents/PM Tracker/db_upgrade_1_2_0_external_link_harvested_at.sql"
```

Expected: `PRINT` výstup „Sloupec last_harvested_at přidán." (nebo „už existuje" při opakování).

- [ ] **Step 9: Commit**

```bash
git add db_upgrade_1_2_0_external_link_harvested_at.sql \
        PmTracker.Web/Models/Entities/PmTrackerEntities.cs \
        PmTracker.Web/Data/Configuration/ZaznamExterniOdkazEntityConfiguration.cs \
        PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazLastHarvestedAtTests.cs
git commit -m "feat(externi-odkaz): last_harvested_at sloupec + entity property"
```

---

## Task 2: Rozšířit `ExterniOdkazEditViewModel`

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs` (`ExterniOdkazEditViewModel`)
- Modify: `PmTracker.Web/Services/RecordService.*.cs` (kde se ExterniVazby mapují)
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazEditViewModelTests.cs`

- [ ] **Step 1: Najít `ExterniOdkazEditViewModel`**

Run: `grep -n "class ExterniOdkazEditViewModel\|public sealed class ExterniOdkazEdit" PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs`

Expected: najdeš definici třídy.

- [ ] **Step 2: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazEditViewModelTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels.Projekty;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniOdkazEditViewModelTests
{
    [Fact]
    public void ExterniOdkazEditViewModel_ShouldExposeLastHarvestedAt()
    {
        var vm = new ExterniOdkazEditViewModel
        {
            LastHarvestedAt = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc)
        };
        vm.LastHarvestedAt.Should().Be(new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
    }
}
```

- [ ] **Step 3: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ExterniOdkazEditViewModelTests" --no-restore`

Expected: FAIL.

- [ ] **Step 4: Přidat property do VM**

V `ExterniOdkazEditViewModel` za poslední property přidej:

```csharp
public DateTime? LastHarvestedAt { get; set; }
```

- [ ] **Step 5: Namapovat VM → entity (při save)**

Najít místo, kde se `ExterniOdkazEditViewModel` mapuje do `ZaznamExterniOdkazEntity`:

Run: `grep -rn "new ZaznamExterniOdkazEntity\|ZaznamExterniOdkazEntity {" PmTracker.Web/Services 2>/dev/null`

Expected: `RecordService.SaveRecord.cs` nebo podobně — blok s `new ZaznamExterniOdkazEntity { ... }`.

V tom bloku přidej:

```csharp
LastHarvestedAt = vazba.LastHarvestedAt,
```

(pokud je to obousměrné — VM → entity; zpětné mapování entity → VM také přidej, hledej `new ExterniOdkazEditViewModel` ve stejném souboru nebo v `RecordService.Queries.cs`).

- [ ] **Step 6: Build + test**

Run: `dotnet build PmTracker.Web --no-restore && dotnet test PmTracker.Tests.Unit --filter "ExterniOdkazEditViewModelTests" --no-restore`

Expected: 0 errors, 1 passed.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ZaznamEditViewModels.cs \
        PmTracker.Web/Services/RecordService.*.cs \
        PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazEditViewModelTests.cs
git commit -m "feat(externi-odkaz): LastHarvestedAt v ExterniOdkazEditViewModel + save mapping"
```

---

## Task 3: Nový controller `ExterniOdkazController` + endpoint `/Sync`

**Files:**
- Create: `PmTracker.Web/Controllers/ExterniOdkazController.cs`
- Create: `PmTracker.Web/Models/ViewModels/ExterniOdkaz/ExterniOdkazSyncResponse.cs`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncControllerTests.cs`

- [ ] **Step 1: Napsat failující test**

Vytvoř `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncControllerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Controllers;
using PmTracker.Web.Models.ViewModels.ExterniOdkaz;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniOdkazSyncControllerTests
{
    [Fact]
    public async Task Sync_WithInvalidCislo_ReturnsBadRequest()
    {
        var ticketing = new Mock<ITicketingQueryService>();
        var sut = new ExterniOdkazController(ticketing.Object);

        var result = await sut.Sync("abc", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Sync_WithSixDigitCisloNotFound_ReturnsNotFoundResponse()
    {
        var ticketing = new Mock<ITicketingQueryService>();
        ticketing.Setup(x => x.GetZaznamAsync("999999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((HotZaznamDto?)null);
        var sut = new ExterniOdkazController(ticketing.Object);

        var result = await sut.Sync("999999", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ExterniOdkazSyncResponse>().Subject;
        payload.Nalezeno.Should().BeFalse();
        payload.Typ.Should().BeNull();
    }

    [Fact]
    public async Task Sync_WithSixDigitCisloFound_ReturnsTypAndStrucne()
    {
        var ticketing = new Mock<ITicketingQueryService>();
        ticketing.Setup(x => x.GetZaznamAsync("336865", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HotZaznamDto("336865", "PNF", "Oprava přihlášení", null));
        var sut = new ExterniOdkazController(ticketing.Object);

        var result = await sut.Sync("336865", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ExterniOdkazSyncResponse>().Subject;
        payload.Nalezeno.Should().BeTrue();
        payload.Typ.Should().Be("PNF");
        payload.Strucne.Should().Be("Oprava přihlášení");
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout — třída a endpoint neexistují)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ExterniOdkazSyncControllerTests" --no-restore`

Expected: **COMPILATION ERROR** — `ExterniOdkazController` a `ExterniOdkazSyncResponse` neexistují.

- [ ] **Step 3: Vytvořit DTO**

Vytvoř `PmTracker.Web/Models/ViewModels/ExterniOdkaz/ExterniOdkazSyncResponse.cs`:

```csharp
namespace PmTracker.Web.Models.ViewModels.ExterniOdkaz;

public sealed record ExterniOdkazSyncResponse(
    bool Nalezeno,
    string Cislo,
    string? Typ,
    string? Strucne);
```

- [ ] **Step 4: Vytvořit controller**

Vytvoř `PmTracker.Web/Controllers/ExterniOdkazController.cs`:

```csharp
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.ViewModels.ExterniOdkaz;

namespace PmTracker.Web.Controllers;

[Authorize]
[Route("ExterniOdkaz")]
public sealed class ExterniOdkazController(ITicketingQueryService ticketing) : Controller
{
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);

    [HttpPost("Sync")]
    public async Task<IActionResult> Sync([FromForm] string cislo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo) || !SixDigits.IsMatch(cislo))
        {
            return BadRequest(new { Error = "Číslo tiketu musí být přesně 6 cifer." });
        }

        var dto = await ticketing.GetZaznamAsync(cislo, ct);
        if (dto is null)
        {
            return Ok(new ExterniOdkazSyncResponse(
                Nalezeno: false, Cislo: cislo, Typ: null, Strucne: null));
        }

        return Ok(new ExterniOdkazSyncResponse(
            Nalezeno: true,
            Cislo: cislo,
            Typ: dto.TypZaznamu,
            Strucne: dto.Strucne));
    }
}
```

- [ ] **Step 5: Upravit test — volat `Sync(cislo, ct)` místo volání s `string`**

Pokud test v Step 1 volá `sut.Sync("abc", CancellationToken.None)` a Sync teď bere `[FromForm] string cislo`, C# by měl fungovat (parameter binding je runtime věc, test používá přímo method invocation). Pokud testy fungují, pokračuj. Pokud ne, uprav test na správnou signaturu.

- [ ] **Step 6: Přidat `Moq` do test projektu, pokud chybí**

Run: `grep "Moq" PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj`

Pokud prázdné, přidej: `dotnet add PmTracker.Tests.Unit package Moq`.

- [ ] **Step 7: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "ExterniOdkazSyncControllerTests" --no-restore`

Expected: 3/3 passed.

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/Controllers/ExterniOdkazController.cs \
        PmTracker.Web/Models/ViewModels/ExterniOdkaz/ExterniOdkazSyncResponse.cs \
        PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazSyncControllerTests.cs \
        PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
git commit -m "feat(externi-odkaz): POST /ExterniOdkaz/Sync endpoint pro 6místné číslo"
```

---

## Task 4: CSS pro novou kartu externí vazby

**Files:**
- Create: `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (přidat `@import`)

- [ ] **Step 1: Vytvořit CSS soubor**

Vytvoř `PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css`:

```css
/* Karta externí vazby — nový layout v2.
   Horní řádek: Číslo | Typ (readonly) | Cena | Výzva switch | (pravý okraj) ikony 🗑 + 💬.
   Dolní řádek: 3 read-only datumy (Objednání, Dodání, Převzetí). */

.external-row {
  display: grid;
  grid-template-columns: 140px 120px 160px 1fr auto;
  grid-template-rows: auto auto;
  gap: 0.5rem 1rem;
  align-items: end;
  padding: 1rem 1.25rem;
  background: var(--pm-surface, #ffffff);
  border: 1px solid var(--pm-border, #e2e8f0);
  border-radius: 8px;
  margin-bottom: 0.75rem;
}

.external-row > .external-field-cislo { grid-column: 1; grid-row: 1; }
.external-row > .external-field-typ { grid-column: 2; grid-row: 1; }
.external-row > .external-field-cena { grid-column: 3; grid-row: 1; }
.external-row > .external-field-vyzva { grid-column: 4; grid-row: 1; }

.external-row > .external-actions {
  grid-column: 5;
  grid-row: 1 / span 2;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  align-self: center;
}

.external-row > .external-dates {
  grid-column: 1 / span 4;
  grid-row: 2;
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--pm-border, #e2e8f0);
  margin-top: 0.5rem;
  font-size: 0.875rem;
}

.external-row > .external-dates[hidden] { display: none; }

.external-date-item {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.external-date-item > .label {
  font-weight: 600;
  color: var(--pm-text-muted, #64748b);
}

.external-date-item > .value {
  font-variant-numeric: tabular-nums;
}

.external-date-item > .value.muted {
  color: var(--pm-text-muted, #64748b);
  font-style: italic;
}

.external-actions .pm-icon-button {
  width: 2rem;
  height: 2rem;
  padding: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.external-row[data-not-found] {
  background: var(--pm-warning-subtle, #fef3c7);
  border-color: var(--pm-warning, #f59e0b);
}

.external-row[data-not-found]::after {
  content: "Tiket se nepodařilo dohledat v ServiceDesku.";
  grid-column: 1 / -1;
  grid-row: 3;
  color: var(--pm-warning-strong, #92400e);
  font-size: 0.8125rem;
  padding-top: 0.5rem;
}
```

- [ ] **Step 2: Přidat import do `site.css`**

Najít řádek začátku `site.css`:

Run: `head -3 PmTracker.Web/wwwroot/css/site.css`

Expected: `@import "components/vyzvy-panel.css";` na první řádce.

Přidat další řádek hned pod:

Použij Edit:

```
old_string: @import "components/vyzvy-panel.css";
new_string: @import "components/vyzvy-panel.css";
@import "components/externi-odkaz-card.css";
```

- [ ] **Step 3: Verifikace**

Run: `head -3 PmTracker.Web/wwwroot/css/site.css`

Expected: oba importy vidíš.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/css/components/externi-odkaz-card.css \
        PmTracker.Web/wwwroot/css/site.css
git commit -m "feat(externi-odkaz): CSS nové karty — 2-řádkový grid s ikonami vpravo"
```

---

## Task 5: Re-render `_EditZaznamExternalPanel.cshtml` do nové struktury

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniVazbaCardRenderingTests.cs`

- [ ] **Step 1: Napsat failující snapshot test**

Vytvoř `PmTracker.Tests.Unit/ExterniOdkaz/ExterniVazbaCardRenderingTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniVazbaCardRenderingTests
{
    private static string ReadView()
    {
        var path = Path.Combine(
            FindRepoRoot(), "PmTracker.Web", "Views", "Projekty", "_EditZaznamExternalPanel.cshtml");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void ExternalPanel_ShouldUseNewCardGridClasses()
    {
        var view = ReadView();
        view.Should().Contain("external-field-cislo");
        view.Should().Contain("external-field-typ");
        view.Should().Contain("external-field-cena");
        view.Should().Contain("external-field-vyzva");
        view.Should().Contain("external-actions");
        view.Should().Contain("external-dates");
    }

    [Fact]
    public void ExternalPanel_ShouldHaveTypReadonly()
    {
        var view = ReadView();
        view.Should().Contain("data-external-type-display");
        view.Should().NotContain("data-external-type-select");
    }

    [Fact]
    public void ExternalPanel_ShouldHaveCislo6DigitPattern()
    {
        var view = ReadView();
        view.Should().Contain("pattern=\"[0-9]{6}\"");
        view.Should().Contain("maxlength=\"6\"");
    }

    [Fact]
    public void ExternalPanel_ShouldUseIconRemoveButton()
    {
        var view = ReadView();
        view.Should().Contain("data-external-remove");
        view.Should().Contain("gov-icon");
        view.Should().Contain("trash");
    }

    [Fact]
    public void ExternalPanel_ShouldUseChatIconButton()
    {
        var view = ReadView();
        view.Should().Contain("data-external-chat-open");
        view.Should().Contain("comment");
    }

    [Fact]
    public void ExternalPanel_ShouldLabelSwitchAsVyzva()
    {
        var view = ReadView();
        view.Should().Contain(">Výzva<");
        view.Should().NotContain("Zařadit do další výzvy");
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "ExterniVazbaCardRenderingTests" --no-restore`

Expected: 6 failed.

- [ ] **Step 3: Přepsat view**

Nahraď obsah `PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml`:

```razor
@using System.Globalization
@model ZaznamEditViewModel
@{
    var isActive = ViewData["IsActive"] as bool? ?? false;
}

<div class="record-modal-panel"
     data-record-modal-panel="external"
     @(isActive ? null : "hidden=\"hidden\"")>
    <div class="section-divider">Externí vazby</div>
    <div data-external-links-editor>
        <div class="external-links" data-external-links>
            @for (var i = 0; i < Model.ExterniVazby.Count; i++)
            {
                var vazba = Model.ExterniVazby[i];
                var isPnf = string.Equals(vazba.Typ, "PNF", StringComparison.OrdinalIgnoreCase);
                var showEstimatedPrice = isPnf
                    || string.Equals(vazba.Typ, "PMP", StringComparison.OrdinalIgnoreCase);
                var estimatedPriceValue = vazba.PredpokladanaCena?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
                var statusText = vazba.VyzvaKod is not null
                    ? $"Zařazeno do výzvy {vazba.VyzvaKod}"
                    : vazba.ZaradidDoVyzvy ? "Čeká se (buffer projektu)" : null;
                var datumObjednani = vazba.DatumObjednani?.ToString("dd.MM.yyyy") ?? "—";
                var datumDodani = vazba.DatumDodani?.ToString("dd.MM.yyyy") ?? "—";
                var datumPrevzeti = vazba.DatumPrevzeti?.ToString("dd.MM.yyyy");
                var hasAnyDate = vazba.DatumObjednani.HasValue || vazba.DatumDodani.HasValue || vazba.DatumPrevzeti.HasValue;
                var chatEnabled = !string.IsNullOrWhiteSpace(vazba.Typ);

                <div class="external-row" data-external-row>
                    <input type="hidden" name="ExterniVazby[@i].Id" value="@vazba.Id" />
                    <input type="hidden" name="ExterniVazby[@i].Typ" value="@vazba.Typ" data-external-type-hidden />
                    <input type="hidden" name="ExterniVazby[@i].DatumObjednani" value="@(vazba.DatumObjednani?.ToString("yyyy-MM-dd"))" data-external-datum-objednani />
                    <input type="hidden" name="ExterniVazby[@i].PlanDodani" value="@(vazba.PlanDodani?.ToString("yyyy-MM-dd"))" />
                    <input type="hidden" name="ExterniVazby[@i].DatumDodani" value="@(vazba.DatumDodani?.ToString("yyyy-MM-dd"))" data-external-datum-dodani />
                    <input type="hidden" name="ExterniVazby[@i].DatumPrevzeti" value="@(vazba.DatumPrevzeti?.ToString("yyyy-MM-dd"))" data-external-datum-prevzeti />

                    <label class="external-field-cislo">
                        Číslo
                        <input type="text"
                               name="ExterniVazby[@i].Cislo"
                               value="@vazba.Cislo"
                               maxlength="6"
                               pattern="[0-9]{6}"
                               inputmode="numeric"
                               data-external-cislo
                               data-external-odkaz-id="@vazba.Id" />
                    </label>

                    <label class="external-field-typ">
                        Typ
                        <span class="pm-readonly-text" data-external-type-display>@(vazba.Typ ?? "—")</span>
                    </label>

                    <label class="external-field-cena" data-external-price-field @(showEstimatedPrice ? null : "hidden=\"hidden\"")>
                        Předpokládaná cena
                        <input type="number"
                               name="ExterniVazby[@i].PredpokladanaCena"
                               value="@estimatedPriceValue"
                               step="0.01"
                               min="0"
                               inputmode="decimal"
                               data-external-price-input
                               disabled="@(showEstimatedPrice ? null : "disabled")" />
                    </label>

                    <div class="external-field-vyzva vyzvy-switch-wrap" data-external-vyzvy-switch-wrap
                         @(isPnf ? null : "hidden=\"hidden\"")>
                        <label class="pm-switch-label">
                            <input type="hidden" name="ExterniVazby[@i].VyzvaId" value="@vazba.VyzvaId" />
                            <input type="hidden" name="ExterniVazby[@i].ZaradidDoVyzvy" value="@(vazba.ZaradidDoVyzvy ? "true" : "false")" data-external-vyzvy-switch-state />
                            <input type="checkbox"
                                   class="pm-switch"
                                   data-vyzvy-switch
                                   data-externi-odkaz-id="@vazba.Id"
                                   @(vazba.ZaradidDoVyzvy ? "checked=\"checked\"" : null) />
                            <span>Výzva</span>
                        </label>
                        <span class="vyzvy-switch-status" data-vyzvy-switch-status>@statusText</span>
                    </div>

                    <div class="external-actions">
                        <pm-button variant="Secondary" size="Small" data-external-chat-open="true"
                                   data-external-odkaz-id="@vazba.Id"
                                   title="Vyjádření a termíny"
                                   aria-label="Vyjádření a termíny"
                                   disabled="@(chatEnabled ? null : "disabled")">
                            <gov-icon name="comment" type="components"></gov-icon>
                        </pm-button>
                        <pm-button variant="Secondary" size="Small" data-external-remove="true"
                                   title="Odebrat vazbu"
                                   aria-label="Odebrat vazbu">
                            <gov-icon name="trash" type="components"></gov-icon>
                        </pm-button>
                    </div>

                    <div class="external-dates" @(hasAnyDate ? null : "hidden=\"hidden\"")>
                        <div class="external-date-item">
                            <span class="label">Datum objednání</span>
                            <span class="value">@datumObjednani</span>
                        </div>
                        <div class="external-date-item">
                            <span class="label">Datum dodání</span>
                            <span class="value">@datumDodani</span>
                        </div>
                        <div class="external-date-item">
                            <span class="label">Datum převzetí</span>
                            @if (datumPrevzeti is not null)
                            {
                                <span class="value">@datumPrevzeti</span>
                            }
                            else
                            {
                                <span class="value muted">Záznam ještě nebyl převeden do archivu.</span>
                            }
                        </div>
                    </div>
                </div>
            }
        </div>

        <template data-external-template>
            <div class="external-row" data-external-row>
                <input type="hidden" name="ExterniVazby[__index__].Id" value="0" />
                <input type="hidden" name="ExterniVazby[__index__].Typ" value="" data-external-type-hidden />
                <input type="hidden" name="ExterniVazby[__index__].DatumObjednani" value="" data-external-datum-objednani />
                <input type="hidden" name="ExterniVazby[__index__].PlanDodani" value="" />
                <input type="hidden" name="ExterniVazby[__index__].DatumDodani" value="" data-external-datum-dodani />
                <input type="hidden" name="ExterniVazby[__index__].DatumPrevzeti" value="" data-external-datum-prevzeti />

                <label class="external-field-cislo">
                    Číslo
                    <input type="text"
                           name="ExterniVazby[__index__].Cislo"
                           value=""
                           maxlength="6"
                           pattern="[0-9]{6}"
                           inputmode="numeric"
                           data-external-cislo
                           data-external-odkaz-id="0" />
                </label>

                <label class="external-field-typ">
                    Typ
                    <span class="pm-readonly-text" data-external-type-display>—</span>
                </label>

                <label class="external-field-cena" data-external-price-field hidden="hidden">
                    Předpokládaná cena
                    <input type="number"
                           name="ExterniVazby[__index__].PredpokladanaCena"
                           value=""
                           step="0.01"
                           min="0"
                           inputmode="decimal"
                           data-external-price-input
                           disabled="disabled" />
                </label>

                <div class="external-field-vyzva vyzvy-switch-wrap" data-external-vyzvy-switch-wrap hidden="hidden">
                    <label class="pm-switch-label">
                        <input type="hidden" name="ExterniVazby[__index__].VyzvaId" value="" />
                        <input type="hidden" name="ExterniVazby[__index__].ZaradidDoVyzvy" value="false" data-external-vyzvy-switch-state />
                        <input type="checkbox" class="pm-switch" data-vyzvy-switch data-externi-odkaz-id="" />
                        <span>Výzva</span>
                    </label>
                    <span class="vyzvy-switch-status" data-vyzvy-switch-status></span>
                </div>

                <div class="external-actions">
                    <pm-button variant="Secondary" size="Small" data-external-chat-open="true"
                               data-external-odkaz-id="0"
                               title="Vyjádření a termíny"
                               aria-label="Vyjádření a termíny"
                               disabled="disabled">
                        <gov-icon name="comment" type="components"></gov-icon>
                    </pm-button>
                    <pm-button variant="Secondary" size="Small" data-external-remove="true"
                               title="Odebrat vazbu"
                               aria-label="Odebrat vazbu">
                        <gov-icon name="trash" type="components"></gov-icon>
                    </pm-button>
                </div>

                <div class="external-dates" hidden="hidden">
                    <div class="external-date-item"><span class="label">Datum objednání</span><span class="value">—</span></div>
                    <div class="external-date-item"><span class="label">Datum dodání</span><span class="value">—</span></div>
                    <div class="external-date-item"><span class="label">Datum převzetí</span><span class="value muted">Záznam ještě nebyl převeden do archivu.</span></div>
                </div>
            </div>
        </template>

        <pm-button variant="Secondary" data-external-add="true">Přidat externí vazbu</pm-button>
    </div>
</div>
```

- [ ] **Step 4: Ověřit, že gov-icon name=comment + name=trash existují**

Run: `ls PmTracker.Web/wwwroot/assets/icons/components/ | grep -E "(trash|comment)"`

Expected: `comment.svg` + `trash.svg` (nebo `trash-alt.svg`). Pokud některá chybí, změň `name="..."` na nejbližší ekvivalent (např. `delete`, `chat`).

- [ ] **Step 5: Build + test**

Run: `dotnet build PmTracker.Web --no-restore && dotnet test PmTracker.Tests.Unit --filter "ExterniVazbaCardRenderingTests" --no-restore`

Expected: 6/6 passed.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_EditZaznamExternalPanel.cshtml \
        PmTracker.Tests.Unit/ExterniOdkaz/ExterniVazbaCardRenderingTests.cs
git commit -m "feat(externi-odkaz): kompaktní karta v2 — Číslo/Typ/Cena/Výzva/🗑/💬 + datumy"
```

---

## Task 6: JS modul `sync.js` (debounce 6 číslic → AJAX → DOM update)

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js`

- [ ] **Step 1: Vytvořit modul**

Vytvoř `PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js`:

```js
/**
 * Externí odkaz sync — debouncovaný input handler pro 6místné číslo tiketu.
 * Po naplnění 6 cifer volá POST /ExterniOdkaz/Sync a vyplní Typ + řeší vzhled karty.
 * Chat tlačítko se povolí jen pokud je tiket nalezen.
 */
(function (global) {
  'use strict';

  const DEBOUNCE_MS = 400;
  const timers = new WeakMap();

  function getCsrfToken() {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  async function syncCislo(inputEl) {
    const row = inputEl.closest('[data-external-row]');
    if (!row) return;
    const cislo = (inputEl.value || '').trim();
    if (!/^\d{6}$/.test(cislo)) {
      setTypDisplay(row, null);
      setChatEnabled(row, false);
      row.removeAttribute('data-not-found');
      return;
    }

    const form = new FormData();
    form.append('cislo', cislo);
    form.append('__RequestVerificationToken', getCsrfToken());

    try {
      const resp = await fetch('/ExterniOdkaz/Sync', {
        method: 'POST',
        body: form,
        credentials: 'same-origin',
      });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      if (data.nalezeno) {
        setTypDisplay(row, data.typ);
        setTypHidden(row, data.typ);
        setChatEnabled(row, true);
        row.removeAttribute('data-not-found');
      } else {
        setTypDisplay(row, null);
        setTypHidden(row, '');
        setChatEnabled(row, false);
        row.setAttribute('data-not-found', 'true');
      }
    } catch (err) {
      console.warn('ExterniOdkaz.Sync selhal:', err);
      row.setAttribute('data-not-found', 'true');
    }
  }

  function setTypDisplay(row, typ) {
    const span = row.querySelector('[data-external-type-display]');
    if (span) span.textContent = typ || '—';
  }

  function setTypHidden(row, typ) {
    const hidden = row.querySelector('[data-external-type-hidden]');
    if (hidden) hidden.value = typ || '';
  }

  function setChatEnabled(row, enabled) {
    const btn = row.querySelector('[data-external-chat-open]');
    if (!btn) return;
    if (enabled) {
      btn.removeAttribute('disabled');
    } else {
      btn.setAttribute('disabled', 'disabled');
    }
  }

  function onInput(event) {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) return;
    if (!target.hasAttribute('data-external-cislo')) return;

    const existing = timers.get(target);
    if (existing) clearTimeout(existing);
    const timer = setTimeout(() => syncCislo(target), DEBOUNCE_MS);
    timers.set(target, timer);
  }

  function init() {
    document.addEventListener('input', onInput);
  }

  global.pmExterniOdkazSync = { init };
})(window);
```

- [ ] **Step 2: Integrovat do bundlu** (odložíme do Task 8 — nejdřív chatModalStub)

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js
git commit -m "feat(externi-odkaz): JS modul sync.js — debounce 6 cifer + AJAX call"
```

---

## Task 7: JS modul `chatModalStub.js`

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/externiOdkaz/chatModalStub.js`

- [ ] **Step 1: Vytvořit modul**

Vytvoř `PmTracker.Web/wwwroot/js/modules/externiOdkaz/chatModalStub.js`:

```js
/**
 * Dočasný otvírač pro tlačítko 💬 (Vyjádření a termíny).
 * Ukazuje stub modal se zprávou, že chat modal je v přípravě (dělá se v Plánu C).
 */
(function (global) {
  'use strict';

  let dialogEl = null;

  function ensureDialog() {
    if (dialogEl) return dialogEl;
    dialogEl = document.createElement('gov-dialog');
    dialogEl.setAttribute('size', 'm');
    dialogEl.innerHTML = `
      <div slot="label">Vyjádření a termíny</div>
      <p>Chat modal s vyjádřeními a drag &amp; drop přiřazením ke krokům harmonogramu se připravuje.
         V aktuální verzi lze pracovat s ručními sloupci Plán / Skutečnost v záložce Harmonogram.</p>
      <div slot="footer" style="display:flex; justify-content:flex-end">
        <pm-button variant="Primary" data-chat-stub-close>OK</pm-button>
      </div>
    `;
    document.body.appendChild(dialogEl);
    dialogEl.addEventListener('click', (event) => {
      const btn = event.target.closest('[data-chat-stub-close]');
      if (btn) close();
    });
    return dialogEl;
  }

  function open() {
    const el = ensureDialog();
    if (typeof el.show === 'function') el.show();
    else el.setAttribute('open', '');
  }

  function close() {
    if (!dialogEl) return;
    if (typeof dialogEl.hide === 'function') dialogEl.hide();
    else dialogEl.removeAttribute('open');
  }

  function onClick(event) {
    const btn = event.target.closest('[data-external-chat-open]');
    if (!btn) return;
    if (btn.hasAttribute('disabled')) return;
    event.preventDefault();
    open();
  }

  function init() {
    document.addEventListener('click', onClick);
  }

  global.pmExterniOdkazChatStub = { init };
})(window);
```

- [ ] **Step 2: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/externiOdkaz/chatModalStub.js
git commit -m "feat(externi-odkaz): chatModalStub.js — otvírač gov-dialog 'v přípravě' pro 💬"
```

---

## Task 8: Integrovat moduly do `site.bundle.js`

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js`

- [ ] **Step 1: Najít konec bundlu**

Run: `tail -30 PmTracker.Web/wwwroot/js/site.bundle.js`

Expected: vidíš závěrečné IIFE vyzvy modulů (`PmTracker.Web/wwwroot/js/modules/vyzvy/index.js` atd.).

- [ ] **Step 2: Append oba moduly + init volání**

Použij Edit na append na konec souboru (najdi poslední řádek — obvykle `})(window);` od vyzvy/index.js):

```
old_string: })(window);
new_string: })(window);

// PmTracker.Web/wwwroot/js/modules/externiOdkaz/sync.js
<celý obsah sync.js bez BOM, jen JS kód od "(function (global) {" po "})(window);">

// PmTracker.Web/wwwroot/js/modules/externiOdkaz/chatModalStub.js
<celý obsah chatModalStub.js stejně>

// Init externí odkaz moduly po DOMContentLoaded
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', function () {
    if (window.pmExterniOdkazSync) window.pmExterniOdkazSync.init();
    if (window.pmExterniOdkazChatStub) window.pmExterniOdkazChatStub.init();
  });
} else {
  if (window.pmExterniOdkazSync) window.pmExterniOdkazSync.init();
  if (window.pmExterniOdkazChatStub) window.pmExterniOdkazChatStub.init();
}
```

**POZOR:** `old_string` musí být jedinečný v souboru. Pokud tam je víc `})(window);` na konci řádků, použij delší kontext (např. posledních 20 znaků bundle souboru + `})(window);`).

- [ ] **Step 3: Grep verifikace**

Run: `grep -c "pmExterniOdkazSync\.init\|pmExterniOdkazChatStub\.init" PmTracker.Web/wwwroot/js/site.bundle.js`

Expected: `4` (2 init volání × 2 větve `if/else`).

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "chore(bundle): integrovat externiOdkaz moduly do site.bundle.js"
```

---

## Task 9: Playwright visual smoke test

**Files:**
- Create: `/tmp/playwright-externi-odkaz-v2.js` (dočasný soubor podle governance)

- [ ] **Step 1: Spustit dev server**

```bash
pkill -f "PmTracker.Web/bin" 2>/dev/null; sleep 1
cd "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web" && ASPNETCORE_ENVIRONMENT=Development dotnet run > /tmp/pmtracker-run.log 2>&1 &
```

- [ ] **Step 2: Čekat na ready**

```bash
for i in 1 2 3 4 5 6 7 8 9 10; do
  if curl -sSf -o /dev/null http://localhost:5071/ 2>/dev/null; then echo READY; break; fi
  sleep 2
done
```

Expected: `READY`.

- [ ] **Step 3: Vytvořit Playwright skript**

Vytvoř `/tmp/playwright-externi-odkaz-v2.js`:

```js
const { chromium } = require('playwright');
const TARGET_URL = 'http://localhost:5071';
const AS_USER = 'pavel.admin@pmtracker.local';

(async () => {
  const browser = await chromium.launch({ headless: false, slowMo: 100 });
  const ctx = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
    ignoreHTTPSErrors: true,
  });
  const page = await ctx.newPage();
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  page.on('pageerror', e => errors.push('PAGE: ' + e.message));

  try {
    // Otevři dashboard projektu 1 se záznamy
    await page.goto(`${TARGET_URL}/projekty/1/dashboard?asUser=${encodeURIComponent(AS_USER)}&dashTab=zaznamy`,
      { waitUntil: 'networkidle' });
    await page.waitForTimeout(1500);

    // Najdi první edit link na záznam
    const edit = page.locator('a[href*="Zaznamy/Edit"], [data-record-editor-trigger]').first();
    const exists = await edit.count();
    console.log('edit links:', exists);
    if (exists > 0) {
      await edit.click();
      await page.waitForTimeout(1500);

      // Přepni na tab Externí vazby
      const extTab = page.locator('[data-record-modal-panel-tab][data-tab="external"], button:has-text("Externí")').first();
      if (await extTab.count() > 0) {
        await extTab.click();
        await page.waitForTimeout(500);
      }

      await page.screenshot({ path: '/tmp/externi-odkaz-v2-panel.png', fullPage: true });

      // Ověř, že nová struktura existuje
      const cislo = await page.locator('[data-external-cislo]').count();
      const typDisplay = await page.locator('[data-external-type-display]').count();
      const chatBtn = await page.locator('[data-external-chat-open]').count();
      const removeBtn = await page.locator('[data-external-remove]').count();
      console.log('cislo:', cislo, 'typ-display:', typDisplay, 'chat-btn:', chatBtn, 'remove-btn:', removeBtn);
    }

    console.log('\nConsole errors:', errors.length);
    errors.slice(0, 10).forEach(e => console.log(' ❌ ' + e.slice(0, 200)));
  } finally {
    await browser.close();
  }
})();
```

- [ ] **Step 4: Spustit**

```bash
cd ~/.claude/plugins/cache/playwright-skill/playwright-skill/*/skills/playwright-skill && \
  node run.js /tmp/playwright-externi-odkaz-v2.js 2>&1 | tail -30
```

Expected:
- `cislo: ≥1`
- `typ-display: ≥1`
- `chat-btn: ≥1`
- `remove-btn: ≥1`
- Console errors: 1 (favicon 404 OK), žádné jiné.

Zkontrolovat screenshot `/tmp/externi-odkaz-v2-panel.png` okem — nová karta se širokou strukturou, ikony vpravo, datumy pod.

- [ ] **Step 5: Vypnout dev server**

```bash
pkill -f "PmTracker.Web/bin" 2>/dev/null
```

- [ ] **Step 6: Žádný commit** (Playwright skript je ad-hoc v `/tmp`, nepatří do repa).

---

## Task 10: Full build + test + git clean

**Files:**
- (verifikace)

- [ ] **Step 1: Full build**

Run: `dotnet build --no-restore -c Debug`

Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Full test**

Run: `dotnet test PmTracker.Tests.Unit --no-restore --no-build`

Expected: všechny testy passed (po Plánech A + B: ~620 testů).

- [ ] **Step 3: Git status + git log**

Run: `git status && git log --oneline | head -15`

Expected: pracovní strom čistý, ~9 commitů z tohoto plánu + 7 z Plánu A.

- [ ] **Step 4: Bez dalšího commitu — plán hotov**

---

## Hotovo — Plán B

Po dokončení máš:
- ✅ Sloupec `last_harvested_at` v `zaznam_externi_odkazy` + entity + VM.
- ✅ Endpoint `POST /ExterniOdkaz/Sync` pro 6místné číslo → vrací Typ + Strucne.
- ✅ Nová CSS grid karta s ikonami 🗑 + 💬 vpravo, 3 datumy pod.
- ✅ Razor view přepsaný do nové struktury + snapshot testy hlídají regressi.
- ✅ JS modul `sync.js` debounce 400 ms → AJAX call → DOM update.
- ✅ JS modul `chatModalStub.js` → otvírá gov-dialog „v přípravě".
- ✅ Oba moduly v `site.bundle.js`.
- ✅ Playwright visual smoke ověřil nové DOM atributy.

### Mimo scope (dělá se v Plánu C)
- Skutečný chat modal s timeline, stepperem, drag & drop a automatem.
- Automatické vyplňování datumů (DatumObjednani/Dodani/Prevzeti) z textových predikátů na HOT_VYJADRENI.
- Tabulka `zaznam_harmonogram_vyjadreni_vazba`.
- Re-harvest.
