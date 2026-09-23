# Sjednocení řazení záznamů (Záznamy ⇄ tisk) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Záznamy tab i tisk (jednání + celý projekt) řadí záznamy stejně — jeden složený sort: kategorie (Info → Rozhodnutí → Úkol → ostatní) primárně, viditelné číslo dle jednání sekundárně.

**Architecture:** Jeden sdílený řadicí helper `RecordDisplayOrdering` (Services/Common) je jediným zdrojem pravdy pro `CategoryOrder` + rozklad viditelného čísla (`VisibleNumberPartA/PartB`). Záznamy tab (server order) přidá primární klíč kategorie; tisk (`BuildSubsystemGroups`) i tři dosavadní kopie `OrderRecordsByVisibleNumber` se přepnou na helper. Klient řadí jen skupiny subsystémů, uvnitř zachovává server order → stačí měnit server.

**Tech Stack:** .NET 8, ASP.NET Core MVC, EF Core, xUnit + FluentAssertions.

## Global Constraints

- **Commity DRŽET** — necommitovat, dokud uživatel neověří (pravidlo projektu). Kroky „Commit" níže spouštět jen po výslovném pokynu; jinak přeskočit.
- **Žádné jiné řazení záznamů** — pořadí je pevné (kategorie → číslo); žádná uživatelská volba.
- **Tisk úkolu** (`BuildTaskTemplateAsync`) — NEMĚNIT.
- **Subsystémové seskupení** a jeho pořadí (dle pořadí projektu) — NEMĚNIT.
- **Přesné klíče řazení záznamů:** `CategoryOrder → název kategorie (tie-break) → VisibleNumberPartA → VisibleNumberPartB → CisloZaznamu`.
- `CategoryOrder`: `Informace=1, Rozhodnutí=2, Úkol=3, ostatní=4` (match dle názvu, case-insensitive: obsahuje „info" / „rozh" / „ukol"/„úkol").
- `VisibleNumberPartA(a, cisloZaznamu) = a > 0 ? a : max(0, cisloZaznamu)`.
- `VisibleNumberPartB(typ, b) = typ == 1 ? max(1, b) : 0` (1 = číslo dle jednání).

---

## File Structure

- **Create:** `PmTracker.Web/Services/Common/RecordDisplayOrdering.cs` — sdílený řadicí helper (CategoryOrder + VisibleNumberPartA/PartB).
- **Modify:** `PmTracker.Web/Services/ProjectService.RecordCards.cs` — Záznamy tab: řazení kategorie → číslo.
- **Modify:** `PmTracker.Web/Services/ProjectService.RecordComposition.cs` — odstranit lokální kopii (Resolve* + OrderRecordsByVisibleNumber), pokud nezůstane jiný caller.
- **Modify:** `PmTracker.Web/Services/Export/ExportTemplateUseCase.cs` — `BuildSubsystemGroups` použije `RecordDisplayOrdering.CategoryOrder`; smazat lokální `CategoryOrder`.
- **Modify:** `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` — VM build + fetch order použijí helper; smazat lokální kopie.
- **Modify:** `PmTracker.Web/Services/MeetingService.DetailQueries.cs` — pure-number ordering přes helper; smazat lokální kopii.
- **Modify:** `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml` — label „Řadit podle" → „Řazení subsystémů".
- **Create tests:** `PmTracker.Tests.Unit/Common/RecordDisplayOrderingTests.cs`, `PmTracker.Tests.Api/Controllers/RecordOrderingRenderTests.cs`.
- **Modify tests:** `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` (+1 test), `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs` (+1 test).

---

## Task 1: Sdílený řadicí helper `RecordDisplayOrdering`

**Files:**
- Create: `PmTracker.Web/Services/Common/RecordDisplayOrdering.cs`
- Test: `PmTracker.Tests.Unit/Common/RecordDisplayOrderingTests.cs`

**Interfaces:**
- Produces:
  - `RecordDisplayOrdering.MeetingNumberType` (const byte = 1)
  - `int RecordDisplayOrdering.CategoryOrder(string? categoryName)`
  - `int RecordDisplayOrdering.VisibleNumberPartA(int cisloViditelneA, int cisloZaznamu)`
  - `int RecordDisplayOrdering.VisibleNumberPartB(byte cisloViditelneTyp, int cisloViditelneB)`

- [ ] **Step 1: Write the failing test**

Create `PmTracker.Tests.Unit/Common/RecordDisplayOrderingTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Common;
using Xunit;

namespace PmTracker.Tests.Unit.Common;

public sealed class RecordDisplayOrderingTests
{
    [Theory]
    [InlineData("Informace", 1)]
    [InlineData("Rozhodnutí", 2)]
    [InlineData("Úkol", 3)]
    [InlineData("Ukol", 3)]
    [InlineData("Něco jiného", 4)]
    [InlineData("", 4)]
    [InlineData(null, 4)]
    public void CategoryOrder_MapsCategoryToPrimaryRank(string? name, int expected)
        => RecordDisplayOrdering.CategoryOrder(name).Should().Be(expected);

    [Theory]
    [InlineData(873, 5, 873)] // má viditelné číslo → to
    [InlineData(0, 5, 5)]     // nemá → fallback CisloZaznamu
    [InlineData(0, 0, 0)]
    public void VisibleNumberPartA_PrefersVisibleNumber_ElseRecordNumber(int a, int cisloZaznamu, int expected)
        => RecordDisplayOrdering.VisibleNumberPartA(a, cisloZaznamu).Should().Be(expected);

    [Theory]
    [InlineData((byte)1, 2, 2)]  // meeting type → pozice
    [InlineData((byte)1, 0, 1)]  // meeting type, prázdné → min 1
    [InlineData((byte)0, 7, 0)]  // ne-meeting → 0
    public void VisibleNumberPartB_OnlyForMeetingType(byte typ, int b, int expected)
        => RecordDisplayOrdering.VisibleNumberPartB(typ, b).Should().Be(expected);

    [Fact]
    public void CompoundSort_CategoryPrimary_ThenVisibleNumber_873Before881_RegardlessOfId()
    {
        // (id, category, cisloViditelneA, cisloViditelneB, typ, cisloZaznamu)
        var rows = new[]
        {
            (Id: 2, Cat: "Úkol", A: 881, B: 2),
            (Id: 5, Cat: "Informace", A: 873, B: 1),
            (Id: 3, Cat: "Rozhodnutí", A: 873, B: 2),
            (Id: 1, Cat: "Úkol", A: 875, B: 1),
            (Id: 4, Cat: "Informace", A: 881, B: 1),
        };

        var ordered = rows
            .OrderBy(r => RecordDisplayOrdering.CategoryOrder(r.Cat))
            .ThenBy(r => r.Cat, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => RecordDisplayOrdering.VisibleNumberPartA(r.A, 0))
            .ThenBy(r => RecordDisplayOrdering.VisibleNumberPartB(1, r.B))
            .Select(r => r.Id)
            .ToArray();

        // Info(873,881) → Rozhodnutí(873) → Úkol(875,881)
        ordered.Should().Equal(5, 4, 3, 1, 2);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordDisplayOrderingTests"`
Expected: FAIL — `RecordDisplayOrdering` neexistuje (compile error).

- [ ] **Step 3: Create the helper**

Create `PmTracker.Web/Services/Common/RecordDisplayOrdering.cs`:

```csharp
namespace PmTracker.Web.Services.Common;

/// <summary>
/// Jediný zdroj pravdy pro řazení záznamů v zobrazení (Záznamy tab) i v tisku (jednání + projekt):
/// primárně kategorie (Informace → Rozhodnutí → Úkol → ostatní), sekundárně viditelné číslo dle
/// jednania. Klíče se aplikují společně jako jeden složený sort.
/// </summary>
public static class RecordDisplayOrdering
{
    /// <summary>CisloViditelneTyp pro číslování dle jednání (873-1).</summary>
    public const byte MeetingNumberType = 1;

    public static int CategoryOrder(string? categoryName)
    {
        var normalized = (categoryName ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Contains("info")) return 1;
        if (normalized.Contains("rozh")) return 2;
        if (normalized.Contains("ukol") || normalized.Contains("úkol")) return 3;
        return 4;
    }

    public static int VisibleNumberPartA(int cisloViditelneA, int cisloZaznamu)
        => cisloViditelneA > 0 ? cisloViditelneA : Math.Max(0, cisloZaznamu);

    public static int VisibleNumberPartB(byte cisloViditelneTyp, int cisloViditelneB)
        => cisloViditelneTyp == MeetingNumberType ? Math.Max(1, cisloViditelneB) : 0;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordDisplayOrderingTests"`
Expected: PASS (všechny).

- [ ] **Step 5: Commit** (jen na pokyn uživatele)

```bash
git add PmTracker.Web/Services/Common/RecordDisplayOrdering.cs PmTracker.Tests.Unit/Common/RecordDisplayOrderingTests.cs
git commit -m "feat(ordering): sdílený RecordDisplayOrdering helper (kategorie + viditelné číslo)"
```

---

## Task 2: Záznamy tab — řazení kategorie → číslo

**Files:**
- Modify: `PmTracker.Web/Services/ProjectService.RecordCards.cs:14-24`
- Test: `PmTracker.Tests.Api/Controllers/RecordOrderingRenderTests.cs`

**Interfaces:**
- Consumes: `RecordDisplayOrdering.CategoryOrder`, `VisibleNumberPartA`, `VisibleNumberPartB` (Task 1).

**Kontext:** Klient (recordDisplay.js) seskupuje karty dle subsystému a **uvnitř skupiny zachovává server pořadí** → mění se jen server order v `BuildRecordCardsForProjectAsync`. Server renderuje karty ploše v tomto pořadí; Api test ověří pořadí `data-record-id` v HTML (seed do jednoho subsystému → globální = within-subsystem).

- [ ] **Step 1: Write the failing test**

Create `PmTracker.Tests.Api/Controllers/RecordOrderingRenderTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záznamy tab řadí karty primárně dle kategorie (Informace → Rozhodnutí → Úkol), sekundárně dle
/// čísla. Seed do jednoho subsystému → globální server pořadí = within-subsystem pořadí.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordOrderingRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordOrderingRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RecordsTab_OrdersByCategoryThenNumber()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiOrderOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIORDER");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIORDER_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        // Kategorie: U=Úkol, INFO=Informace, ROZHODNUTI=Rozhodnutí (seed fixture).
        var ukol = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Ukol A");
        var info = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "INFO", "Info B");
        var rozh = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "ROZHODNUTI", "Rozhodnuti C");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var order = Regex.Matches(html, "data-record-id=\"(\\d+)\"")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(id => id == ukol || id == info || id == rozh)
            .Distinct()
            .ToArray();

        order.Should().Equal(info, rozh, ukol,
            "pořadí je kategorie primárně: Informace → Rozhodnutí → Úkol");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordOrderingRenderTests"`
Expected: FAIL — dnešní pořadí je dle čísla (pořadí vytvoření `ukol, info, rozh`), ne dle kategorie.

- [ ] **Step 3: Change RecordCards ordering**

In `PmTracker.Web/Services/ProjectService.RecordCards.cs`, current lines 14-24:

```csharp
        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        records = [.. OrderRecordsByVisibleNumber(records)];
        var recordIds = records.Select(record => record.Id).ToArray();

        // Perf: číselníky přes scoped cache ...
        var categories = await lookupCache.GetCategoriesAsync(ct);
```

Replace with (načíst `categories` PŘED řazením, řadit kategorie → číslo přes helper):

```csharp
        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);

        // Perf: číselníky přes scoped cache ...
        var categories = await lookupCache.GetCategoriesAsync(ct);

        // Pořadí = kategorie (Info → Rozhodnutí → Úkol → ostatní) primárně, viditelné číslo sekundárně.
        // Stejný složený sort jako tisk (RecordDisplayOrdering). Klient uvnitř subsystému zachovává toto pořadí.
        records = records
            .OrderBy(x => RecordDisplayOrdering.CategoryOrder(categories.GetValueOrDefault(x.KategorieId)?.Nazev))
            .ThenBy(x => categories.GetValueOrDefault(x.KategorieId)?.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => RecordDisplayOrdering.VisibleNumberPartA(x.CisloViditelneA, x.CisloZaznamu))
            .ThenBy(x => RecordDisplayOrdering.VisibleNumberPartB(x.CisloViditelneTyp, x.CisloViditelneB))
            .ThenBy(x => x.CisloZaznamu)
            .ToList();
        var recordIds = records.Select(record => record.Id).ToArray();
```

Add `using PmTracker.Web.Services.Common;` at the top of the file if missing.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordOrderingRenderTests"`
Expected: PASS.

- [ ] **Step 5: Remove now-unused local copy (if no other caller)**

Verify: `grep -n "OrderRecordsByVisibleNumber" PmTracker.Web/Services/ProjectService.RecordComposition.cs` — pokud už žádný caller v ProjectService, smazat metody `OrderRecordsByVisibleNumber` (ř. 152-156), `ResolveVisibleNumberPartA` (132-140), `ResolveVisibleNumberPartB` (142-150) z `ProjectService.RecordComposition.cs`. Ponechat `ResolveVisibleRecordNumber` (122-130), pokud ho volá něco jiného (`grep -rn "ResolveVisibleRecordNumber" PmTracker.Web/Services`). Build musí projít:

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Commit** (jen na pokyn uživatele)

```bash
git add PmTracker.Web/Services/ProjectService.RecordCards.cs PmTracker.Web/Services/ProjectService.RecordComposition.cs PmTracker.Tests.Api/Controllers/RecordOrderingRenderTests.cs
git commit -m "feat(zaznamy): řazení karet dle kategorie + viditelného čísla (sdílený helper)"
```

---

## Task 3: Tisk (jednání + projekt) — přepnout na sdílený helper

**Files:**
- Modify: `PmTracker.Web/Services/Export/ExportTemplateUseCase.cs:75-121`
- Modify: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs:886-887,956-979`
- Modify: `PmTracker.Web/Services/MeetingService.DetailQueries.cs:510-534`
- Test: `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` (+1 test)

**Interfaces:**
- Consumes: `RecordDisplayOrdering` (Task 1).

**Kontext:** Tisk už řadí kategorie → číslo (výstup se nemá změnit); jen se odstraní duplicitní logika. Export VM nese rozřešené `CisloViditelneA/B`, takže within-group řazení přes `record.CisloViditelneA/B` zůstává stejné.

- [ ] **Step 1: Write the failing test**

In `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs`, add (přesné helpery dle existujícího testu na ř. 20-37 — `CreateExportTemplateUseCase`, `CreateMeetingAsync`, `BuildUser(isSuperAdmin:true)`). Testuje se přes **project** template (spolehlivě obsahuje všechny kategorie; stejné `BuildSubsystemGroups` jako meeting):

```csharp
    [Fact]
    public async Task BuildProjectTemplate_OrdersRecordsWithinSubsystem_ByCategoryThenNumber()
    {
        var db = await _fixture.CreateDatabaseAsync("export_order_category");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var fixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, fixedTimeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpOrderAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpOrderOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPORDER");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPORDER_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var ukol = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Ukol A");
        var info = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "INFO", "Info B");
        var rozh = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "ROZHODNUTI", "Rozhodnuti C");

        var model = await useCase.BuildProjectTemplateAsync(projectId, currentUser, autoPrint: false);

        var group = model.SubsystemGroups.Single();
        group.Records.Select(r => r.ZaznamId)
            .Where(id => id == ukol || id == info || id == rozh)
            .Should().Equal(info, rozh, ukol,
                "tisk řadí uvnitř subsystému kategorie primárně (Informace → Rozhodnutí → Úkol)");
    }
```

- [ ] **Step 2: Run test to verify it passes (regresní zámek)**

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~BuildProjectTemplate_OrdersRecordsWithinSubsystem"`
Expected: PASS už teď (tisk už kategorie → číslo dělá). Tento test je **regresní zámek** — musí zůstat zelený i po refaktoru (Step 3-5). Pokud by neprošel (jiná sada kategorií v seedu než U/INFO/ROZHODNUTI), ověř kódy: `SELECT kod,nazev FROM ciselnik_kategorii_zaznamu` a uprav kódy v seedu testu.

- [ ] **Step 3: ExportTemplateUseCase — použít sdílený CategoryOrder, smazat lokální**

In `PmTracker.Web/Services/Export/ExportTemplateUseCase.cs`, `BuildSubsystemGroups` within-group ordering (ř. 92-96) je:

```csharp
                Records = group
                    .OrderBy(record => CategoryOrder(record.Kategorie))
                    .ThenBy(record => record.Kategorie, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(record => record.CisloViditelneA)
                    .ThenBy(record => record.CisloViditelneB)
                    .ThenBy(record => record.CisloZaznamu)
                    .ToList()
```

Změň `CategoryOrder(record.Kategorie)` → `RecordDisplayOrdering.CategoryOrder(record.Kategorie)`. Smaž lokální metodu `CategoryOrder` (ř. 102-121). Přidej `using PmTracker.Web.Services.Common;`.

- [ ] **Step 4: ExportProjectionBuilders + MeetingService.DetailQueries — sjednotit resolve/order**

In `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs`:
- VM build (ř. 886-887): `CisloViditelneA = ResolveVisibleNumberPartA(record)` → `CisloViditelneA = RecordDisplayOrdering.VisibleNumberPartA(record.CisloViditelneA, record.CisloZaznamu)`; obdobně `CisloViditelneB = RecordDisplayOrdering.VisibleNumberPartB(record.CisloViditelneTyp, record.CisloViditelneB)`.
- `OrderRecordsByVisibleNumber` (ř. 973-979) přepiš na:

```csharp
    private static IEnumerable<ProjektovyZaznamEntity> OrderRecordsByVisibleNumber(IEnumerable<ProjektovyZaznamEntity> rows)
        => rows
            .OrderBy(x => RecordDisplayOrdering.VisibleNumberPartA(x.CisloViditelneA, x.CisloZaznamu))
            .ThenBy(x => RecordDisplayOrdering.VisibleNumberPartB(x.CisloViditelneTyp, x.CisloViditelneB))
            .ThenBy(x => x.CisloZaznamu);
```

- Smaž lokální `ResolveVisibleNumberPartA` (956-964) a `ResolveVisibleNumberPartB` (966-971). Přidej `using PmTracker.Web.Services.Common;`.

In `PmTracker.Web/Services/MeetingService.DetailQueries.cs`:
- `OrderRecordsByVisibleNumber` (ř. 530-534) přepiš stejně (viz výše, přes `RecordDisplayOrdering`).
- Smaž lokální `ResolveVisibleNumberPartA` (510-518) a `ResolveVisibleNumberPartB` (520-528). Přidej `using PmTracker.Web.Services.Common;`.

- [ ] **Step 5: Build + run regression test**

Run: `dotnet build PmTracker.Web/PmTracker.Web.csproj`
Expected: `Build succeeded`, 0 errors (zkontroluj, že smazané metody nemají jiné callery — pokud ano, přesměruj je na helper).

Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~ExportTemplateUseCaseTests"`
Expected: PASS (nový test + existující — výstup tisku beze změny).

- [ ] **Step 6: Commit** (jen na pokyn uživatele)

```bash
git add PmTracker.Web/Services/Export/ExportTemplateUseCase.cs PmTracker.Web/Services/Export/ExportProjectionBuilders.cs PmTracker.Web/Services/MeetingService.DetailQueries.cs PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs
git commit -m "refactor(export): tisk řadí přes sdílený RecordDisplayOrdering (bez změny výstupu)"
```

---

## Task 4: Přejmenování labelu „Řadit podle" → „Řazení subsystémů"

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml:33-41`
- Test: `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs` (+1 test)

- [ ] **Step 1: Write the failing test**

In `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs` add:

```csharp
    [Fact]
    public void Partial_SortByLabel_IsSubsystemSorting_NotMisleadingRadit()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml");

        src.Should().Contain("Řazení subsystémů",
            "sortBy řadí jen subsystémy — label to musí říkat.");
        src.Should().NotContain("Řadit podle",
            "starý zavádějící label musí zmizet (řadil zdánlivě záznamy).");
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~Partial_SortByLabel_IsSubsystemSorting"`
Expected: FAIL — label je dnes „Řadit podle".

- [ ] **Step 3: Rename the label**

In `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml`, change:

```html
            <label>
                Řadit podle
                <select data-filter-key="sortBy">
```

to:

```html
            <label>
                Řazení subsystémů
                <select data-filter-key="sortBy">
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~Partial_SortByLabel_IsSubsystemSorting"`
Expected: PASS.

- [ ] **Step 5: Commit** (jen na pokyn uživatele)

```bash
git add PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs
git commit -m "ui(filtr): label 'Řadit podle' → 'Řazení subsystémů' (řadí jen subsystémy)"
```

---

## Task 5: Regrese + E2E ověření

**Files:** žádná změna kódu; ověření.

- [ ] **Step 1: Full relevant suites**

Run: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordDisplayOrderingTests|FullyQualifiedName~ProjectFilterShellTests"`
Run: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~RecordOrderingRenderTests"`
Run: `dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~ExportTemplateUseCaseTests"`
Expected: vše PASS.

- [ ] **Step 2: E2E/Playwright (živě proti dev DB)**

Spustit appku (`dotnet run --project PmTracker.Web --urls http://localhost:5071 --no-launch-profile`, Development, connection na `pmtracker-sql`). Playwright: otevřít `/Projekty/Detail/<projekt s promíchanými kategoriemi>?tab=zaznamy&asUser=1`, ověřit vizuální pořadí karet uvnitř subsystému = Info → Rozhodnutí → Úkol → číslo; a `/Export/Jednani/<id>/Tisk` — pořadí sedí. Ověřit label „Řazení subsystémů" ve filtru. Zastavit server.

- [ ] **Step 3:** Nahlásit výsledky uživateli; commity držet do jeho ověření.
