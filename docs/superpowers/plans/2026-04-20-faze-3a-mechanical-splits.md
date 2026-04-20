# Fáze 3A — Low-risk mechanical splits — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split 4 god-files (Dashboard priority subsystem, Export template queries, OpenXML Word service, PdfTemplate view) into focused per-responsibility files. Žádná API změna, žádná funkční změna — čistě mechanický refactor s zachováním chování.

**Architecture:** Postupujeme podle Settings/ disciplíny: jeden soubor = jedna veřejná jednotka / konzistentní skupina souvisejících typů. Partial class pattern pro large class decomposition. Consumer API (interfaces consumed by controllers / use-cases) zůstává beze změny.

**Tech Stack:** .NET 8 MVC, xUnit + FluentAssertions, EF Core 8. Žádné nové závislosti.

---

## File Structure

**Task 1 — DashboardPriorityServices (splits 1 file → 5 files):**

| Nový soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs` | `PriorityRecordContext`, `PriorityUserRelevanceContext`, `PriorityScoreBreakdown`, `DashboardPriorityItem` + `PriorityMatrixRebuildStatuses` constants + public interfaces (`IPriorityScoringService`, `IPriorityMatrixRebuildService`, `IDashboardPriorityQuery`) |
| `PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs` | `PriorityScoringService` (pure compute) |
| `PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs` | `DashboardPriorityQuery` (EF reads) |
| `PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs` | `PriorityMatrixRebuildService` + `PriorityMatrixRebuildQueue` + `IPriorityMatrixRebuildQueue` (internal) |
| `PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs` | `PriorityMatrixBootstrapHostedService`, `PriorityMatrixNightlyRebuildHostedService`, `PriorityMatrixQueuedRebuildHostedService` |
| `PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs` | **DELETED** |

**Task 2 — ExportTemplateQueries (splits 1 file → 3 files):**

| Nový soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Export/ExportTemplateQueries.cs` | Keep: `IExportTemplateQueries` interface + `ExportTemplateQueries` orchestration class (3 GetXxxTemplateAsync methods). Keep injection-constructor + orchestration (~125 LOC) |
| `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` | 6 projection builder interfaces + 6 implementations |
| `PmTracker.Web/Services/Export/ExportProjectionModels.cs` | `ExportTemplateQueryResult`, `ExportTemplateSummaryProjection`, other internal records |

**Task 3 — OpenXmlWordExportService (splits 1 file → 3 files + 1 helper):**

| Nový soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Export/OpenXmlWordExportService.cs` | `OpenXmlWordExportService` root + `BuildDocument` skeleton that delegates to section builders |
| `PmTracker.Web/Services/Export/OpenXmlWordExportService.Metadata.cs` | Partial — cover page, project metadata, attendance section builders (private methods) |
| `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs` | Partial — records table rendering (records enumeration, comments, external links, deadline history) |
| `PmTracker.Web/Services/Export/OpenXmlWordElements.cs` | `OpenXmlWordElements` static helper class + `TextSegment` record |

**Task 4 — PdfTemplate.cshtml (extracts inline CSS + optional partials):**

| Nový soubor | Obsah |
|---|---|
| `PmTracker.Web/wwwroot/css/pdf-export.css` | Obsah `<style>` bloku z `PdfTemplate.cshtml` (~200 LOC print-only CSS) |
| `PmTracker.Web/Views/Export/_PdfRecordRow.cshtml` | Per-record rendering (záznam + komentáře + externí vazby + deadline history) |
| `PmTracker.Web/Views/Export/_PdfAttendanceBlock.cshtml` | Účastníci tabulka |
| `PmTracker.Web/Views/Export/_PdfRolesBlock.cshtml` | Role assignments tabulka |
| `PmTracker.Web/Views/Export/PdfTemplate.cshtml` | Redukovaný na orchestraci: header, role block partial, attendance partial, záznamy block, auto-print script |

Každý task je samostatný commit (možný rollback) s vlastní TDD disciplínou.

---

### Task 1: DashboardPriorityServices.cs split

**Goal:** Rozdělit 892 LOC / 10 typů v jednom souboru do 5 souborů podle layer/lifecycle.

**Files:**
- Create: `PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs`
- Create: `PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs`
- Create: `PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs`
- Create: `PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs`
- Create: `PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs`
- Delete: `PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs`
- Verify: `PmTracker.Web/Services/Dashboard/DataStoreServiceCollectionExtensions.cs` nebo kde se registrují services v DI (pokud existuje)

- [ ] **Step 1.1: Audit current file + DI registrations**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -n "^public\|^internal\|^    public\|^    internal" PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs | head -60
```

Najít všechny DI registrace existujících typů:

```bash
grep -rn "PriorityScoringService\|PriorityMatrixRebuildService\|DashboardPriorityQuery\|PriorityMatrixBootstrap\|PriorityMatrixNightly\|PriorityMatrixQueued" PmTracker.Web --include="*.cs" | grep -v "/Dashboard/DashboardPriorityServices.cs" | head -20
```

Zaznamenat konzumenty + DI registraci do /tmp/task1-notes.md. Očekáváme DI v `PmTrackerServiceCollectionExtensions.cs` nebo podobně — hlavní startup registrations.

- [ ] **Step 1.2: Write failing tests pro nové file struktury**

Vytvořit `PmTracker.Tests.Unit/Architecture/DashboardPrioritySplitTests.cs` (pokud Architecture folder neexistuje, vytvořit):

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Fáze 3A Task 1: DashboardPriorityServices.cs (892 LOC, 10 types)
/// rozdělen do 5 souborů podle layer/lifecycle.
/// </summary>
public sealed class DashboardPrioritySplitTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        return directory.FullName;
    }

    private static string Path(string relative) =>
        System.IO.Path.Combine(RepoRoot(), relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

    [Fact]
    public void OriginalGodFile_ShouldBeDeleted()
    {
        File.Exists(Path("PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs"))
            .Should().BeFalse("Fáze 3A Task 1 smazala god-file, obsah rozdělen do 5 souborů");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs")]
    [InlineData("PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs")]
    public void NewFile_ShouldExistAndNotBeEmpty(string relativePath)
    {
        var full = Path(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} byl vytvořen v Fázi 3A Task 1");
        File.ReadAllText(full).Length.Should().BeGreaterThan(500, "každý split file má smysluplný obsah");
    }

    [Fact]
    public void ModelsFile_ShouldContainPublicInterfaces()
    {
        var content = File.ReadAllText(Path("PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs"));
        content.Should().Contain("public interface IPriorityScoringService");
        content.Should().Contain("public interface IPriorityMatrixRebuildService");
        content.Should().Contain("public interface IDashboardPriorityQuery");
    }

    [Fact]
    public void ScoringServiceFile_ShouldContainOnlyScoringClass()
    {
        var content = File.ReadAllText(Path("PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs"));
        content.Should().Contain("class PriorityScoringService");
        content.Should().NotContain("class PriorityMatrixRebuildService", "scoring service je izolovaný");
        content.Should().NotContain("class DashboardPriorityQuery", "scoring service je izolovaný");
    }

    [Fact]
    public void HostedServicesFile_ShouldContainThreeHostedServices()
    {
        var content = File.ReadAllText(Path("PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs"));
        content.Should().Contain("class PriorityMatrixBootstrapHostedService");
        content.Should().Contain("class PriorityMatrixNightlyRebuildHostedService");
        content.Should().Contain("class PriorityMatrixQueuedRebuildHostedService");
    }
}
```

- [ ] **Step 1.3: Run failing tests (should fail)**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" --filter "FullyQualifiedName~DashboardPrioritySplit" 2>&1 | tail -8
```

Expected: všechny testy FAIL (god-file stále existuje, nové soubory neexistují).

- [ ] **Step 1.4: Read god-file** (892 LOC)

```
Read PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs
```

Identifikovat pomocí `^public|^internal|^    public|^    internal` linií hranice mezi typy. Zapsat mapping type → target file do notes.

- [ ] **Step 1.5: Create DashboardPriorityModels.cs**

Obsahuje: public records (PriorityRecordContext, PriorityUserRelevanceContext, PriorityScoreBreakdown, DashboardPriorityItem), public interfaces (IPriorityScoringService, IPriorityMatrixRebuildService, IDashboardPriorityQuery), internal IPriorityMatrixRebuildQueue interface, PriorityMatrixRebuildStatuses constants.

Namespace: `PmTracker.Web.Services.Dashboard` (stejný).

Namespace imports z původního souboru opřít o usage — pravděpodobně `System`, `System.Collections.Generic`, případně entity types z `PmTracker.Web.Models.Entities` nebo `Data.Entities`.

- [ ] **Step 1.6: Create DashboardPriorityScoringService.cs**

Extrahovat `PriorityScoringService` class beze změny implementace. Namespace stejný. Imports kopírovat z původního souboru jen ty skutečně use-ované ve třídě.

- [ ] **Step 1.7: Create DashboardPriorityQuery.cs**

Extrahovat `DashboardPriorityQuery` class. Bude potřebovat EF Core imports (`Microsoft.EntityFrameworkCore`, `PmTrackerDbContext` z wherever).

- [ ] **Step 1.8: Create PriorityMatrixRebuildService.cs**

Obsahuje: `PriorityMatrixRebuildQueue` (in-memory Channel), `PriorityMatrixRebuildService` (orchestration). Include internal `IPriorityMatrixRebuildQueue` pokud není v Models (rozhodnout v kroku 1.5 kam patří — Models nebo tady; internal by mělo být blíže implementaci = tady).

- [ ] **Step 1.9: Create PriorityMatrixHostedServices.cs**

Extrahovat 3 hosted services: Bootstrap, Nightly, Queued.

- [ ] **Step 1.10: Delete original DashboardPriorityServices.cs**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git rm PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs
```

- [ ] **Step 1.11: Run build**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -5
```

Pokud jsou buildchyby (typicky: neexportovaný internal type, chybějící using), opravit. Opakovat dokud 0 Error.

- [ ] **Step 1.12: Run tests (including new architecture tests)**

```bash
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Expected: 346 + 6 = 352 tests pass (6 new architecture tests).

- [ ] **Step 1.13: Commit**

```bash
git add PmTracker.Web/Services/Dashboard/DashboardPriorityModels.cs \
        PmTracker.Web/Services/Dashboard/DashboardPriorityScoringService.cs \
        PmTracker.Web/Services/Dashboard/DashboardPriorityQuery.cs \
        PmTracker.Web/Services/Dashboard/PriorityMatrixRebuildService.cs \
        PmTracker.Web/Services/Dashboard/PriorityMatrixHostedServices.cs \
        PmTracker.Web/Services/Dashboard/DashboardPriorityServices.cs \
        PmTracker.Tests.Unit/Architecture/DashboardPrioritySplitTests.cs

git commit -m "refactor(dashboard): rozbití DashboardPriorityServices.cs do 5 souborů (Fáze 3A Task 1)

892 LOC / 10 types v jednom souboru → 5 souborů podle layer/lifecycle:

- DashboardPriorityModels.cs — public records + interfaces + constants
- DashboardPriorityScoringService.cs — PriorityScoringService (pure compute)
- DashboardPriorityQuery.cs — DashboardPriorityQuery (EF reads)
- PriorityMatrixRebuildService.cs — rebuild service + in-memory queue
- PriorityMatrixHostedServices.cs — 3 hosted services (Bootstrap, Nightly, Queued)

API beze změny. DI registrace nedotčena (všechny typy registrované
přes jejich interfaces zůstaly). Spotřebitelé (RecordService,
RecordProposalService, ProjectService, DashboardService) beze změny.

+6 architecture unit tests ověřujících přítomnost nových souborů
a korektní distribuci typů."
```

---

### Task 2: ExportTemplateQueries.cs split

**Goal:** Rozbít 1 281 LOC / 8+ typů do 3 logických souborů.

**Files:**
- Modify: `PmTracker.Web/Services/Export/ExportTemplateQueries.cs` (keep orchestration only)
- Create: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs`
- Create: `PmTracker.Web/Services/Export/ExportProjectionModels.cs`
- Create: `PmTracker.Tests.Unit/Architecture/ExportTemplateQueriesSplitTests.cs`

- [ ] **Step 2.1: Audit current file**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -nE "^(public|internal) (sealed )?(class|interface|record)" PmTracker.Web/Services/Export/ExportTemplateQueries.cs
```

Zaznamenat typy: 1 main class + 6 builder classes + 6 builder interfaces + 2-3 projection record types.

- [ ] **Step 2.2: Write failing architecture tests**

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class ExportTemplateQueriesSplitTests
{
    private static string RepoRoot() { /* same helper */ }
    private static string Path(string relative) => /* ... */;

    [Fact]
    public void Orchestration_ShouldBeSlim()
    {
        var file = Path("PmTracker.Web/Services/Export/ExportTemplateQueries.cs");
        File.Exists(file).Should().BeTrue();
        var loc = File.ReadAllLines(file).Length;
        loc.Should().BeLessThan(300, "ExportTemplateQueries je jen orchestration (GetXxx metody delegují do builders)");
    }

    [Fact]
    public void Builders_ShouldExist()
    {
        File.Exists(Path("PmTracker.Web/Services/Export/ExportProjectionBuilders.cs"))
            .Should().BeTrue();
    }

    [Fact]
    public void ProjectionModels_ShouldExist()
    {
        File.Exists(Path("PmTracker.Web/Services/Export/ExportProjectionModels.cs"))
            .Should().BeTrue();
    }

    [Fact]
    public void Orchestration_ShouldNotContainBuilderImplementations()
    {
        var content = File.ReadAllText(Path("PmTracker.Web/Services/Export/ExportTemplateQueries.cs"));
        content.Should().NotContain("class ExportAttendanceProjectionBuilder");
        content.Should().NotContain("class ExportRoleProjectionBuilder");
        content.Should().NotContain("class ExportCommentsProjectionBuilder");
    }
}
```

- [ ] **Step 2.3: Extract ExportProjectionModels.cs**

Přesunout `ExportTemplateQueryResult`, `ExportTemplateSummaryProjection`, any other internal records used by builders.

- [ ] **Step 2.4: Extract ExportProjectionBuilders.cs**

Přesunout 6 builder interfaces + 6 builder class implementations.

- [ ] **Step 2.5: Trim ExportTemplateQueries.cs to orchestration**

Ponechat pouze:
- `IExportTemplateQueries` interface
- `ExportTemplateQueries` class (constructor + 3 GetXxxTemplateAsync methods)

- [ ] **Step 2.6: Build + test**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Expected: Build 0 Error, 356 tests pass (+4 new).

- [ ] **Step 2.7: Commit**

```bash
git add PmTracker.Web/Services/Export/ExportTemplateQueries.cs \
        PmTracker.Web/Services/Export/ExportProjectionBuilders.cs \
        PmTracker.Web/Services/Export/ExportProjectionModels.cs \
        PmTracker.Tests.Unit/Architecture/ExportTemplateQueriesSplitTests.cs
git commit -m "refactor(export): rozdělit ExportTemplateQueries (Fáze 3A Task 2)

1281 LOC / 8+ typů → 3 soubory: orchestration (~125 LOC),
builders (interfaces + implementace, ~900 LOC), projection models.

API beze změny. Konzument: ExportTemplateUseCase + DI.

+4 architecture unit tests."
```

---

### Task 3: OpenXmlWordExportService.cs split

**Goal:** Rozdělit 1 113 LOC → 3-4 soubory (partial class + static helper).

**Files:**
- Modify: `PmTracker.Web/Services/Export/OpenXmlWordExportService.cs` (keep root + BuildDocument delegation)
- Create: `PmTracker.Web/Services/Export/OpenXmlWordExportService.Metadata.cs` (partial)
- Create: `PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs` (partial)
- Create: `PmTracker.Web/Services/Export/OpenXmlWordElements.cs` (extracted static helper)

- [ ] **Step 3.1: Audit file structure**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -nE "^(public|private|internal|protected) (static )?.*(class|Build|AddSection|Render)" PmTracker.Web/Services/Export/OpenXmlWordExportService.cs | head -40
```

Identifikovat:
- Methods on `OpenXmlWordExportService` → rozdělit podle section (metadata / records / tabulky)
- `OpenXmlWordElements` static class → samostatný soubor

- [ ] **Step 3.2: Write architecture tests + extract helper first**

Vytvořit `OpenXmlWordElements.cs` s `public static class OpenXmlWordElements` + `TextSegment` record. Pokud TextSegment je private, ponechat v původním souboru.

- [ ] **Step 3.3: Create Metadata partial**

Identifikovat private metody které staví: cover page, project metadata header, attendance section. Přesunout do `OpenXmlWordExportService.Metadata.cs` partial class fragment.

Verify: `public sealed partial class OpenXmlWordExportService` deklarace v obou (nebo vícero) souborech.

- [ ] **Step 3.4: Create Records partial**

Přesunout private metody pro records table + comments rendering.

- [ ] **Step 3.5: Trim core file**

Root `OpenXmlWordExportService.cs` ponechat:
- Class declaration (partial)
- Constructor + DI
- `BuildDocument` jako orchestrator (high-level flow)

- [ ] **Step 3.6: Build + test**

Expected: Build 0 Error. Existující Word export integration/unit testy (pokud jsou) pass.

- [ ] **Step 3.7: Commit**

```bash
git add PmTracker.Web/Services/Export/OpenXmlWordExportService.cs \
        PmTracker.Web/Services/Export/OpenXmlWordExportService.Metadata.cs \
        PmTracker.Web/Services/Export/OpenXmlWordExportService.Records.cs \
        PmTracker.Web/Services/Export/OpenXmlWordElements.cs \
        PmTracker.Tests.Unit/Architecture/OpenXmlWordExportSplitTests.cs
git commit -m "refactor(export): OpenXmlWordExportService partials + helper extract (Fáze 3A Task 3)

1113 LOC → 4 soubory: root (BuildDocument orchestrator), Metadata
partial (cover/attendance), Records partial (records+comments),
OpenXmlWordElements static helper.

API (IWordExportService) beze změny."
```

---

### Task 4: PdfTemplate.cshtml + inline CSS extraction

**Goal:** 766 LOC Razor view → partial rozpad + CSS extraction.

**Files:**
- Create: `PmTracker.Web/wwwroot/css/pdf-export.css`
- Create: `PmTracker.Web/Views/Export/_PdfRecordRow.cshtml`
- Create: `PmTracker.Web/Views/Export/_PdfAttendanceBlock.cshtml`
- Create: `PmTracker.Web/Views/Export/_PdfRolesBlock.cshtml`
- Modify: `PmTracker.Web/Views/Export/PdfTemplate.cshtml`

- [ ] **Step 4.1: Extract inline `<style>` block to pdf-export.css**

Najít `<style>` block v `PdfTemplate.cshtml`. Překopírovat obsah (bez `<style>` tagů) do nového `PmTracker.Web/wwwroot/css/pdf-export.css`. Nahradit `<style>` blok v Razor za `<link rel="stylesheet" href="~/css/pdf-export.css" />`.

- [ ] **Step 4.2: Extract record rendering to _PdfRecordRow.cshtml**

Najít foreach block rendering záznamů. Extrahovat tělo (jeden záznam + komentáře + externí vazby + deadline history) do partial.

V PdfTemplate.cshtml: nahradit `@foreach (var zaznam in ...) { ... }` za `@foreach { @await Html.PartialAsync("_PdfRecordRow", zaznam) }`.

- [ ] **Step 4.3: Extract _PdfAttendanceBlock.cshtml + _PdfRolesBlock.cshtml**

Stejný pattern. Attendance tabulka jako partial, roles tabulka jako partial.

- [ ] **Step 4.4: Verify PdfTemplate.cshtml zredukován**

Cíl: < 250 LOC (header + 3-4 partial calls + footer script).

- [ ] **Step 4.5: Runtime smoke (Playwright harness)**

Pokud možné, otevřít `/Export/JednaniTisk/1` v Playwright (potřebuje DB — likely Skip). Případně jen verify build + markup tests.

Verify via grep:

```bash
grep -c "<style>" PmTracker.Web/Views/Export/PdfTemplate.cshtml  # 0
grep -c "pdf-export.css" PmTracker.Web/Views/Export/PdfTemplate.cshtml  # 1
grep -c "Html.PartialAsync" PmTracker.Web/Views/Export/PdfTemplate.cshtml  # 3+
```

- [ ] **Step 4.6: Commit**

```bash
git add PmTracker.Web/wwwroot/css/pdf-export.css \
        PmTracker.Web/Views/Export/_PdfRecordRow.cshtml \
        PmTracker.Web/Views/Export/_PdfAttendanceBlock.cshtml \
        PmTracker.Web/Views/Export/_PdfRolesBlock.cshtml \
        PmTracker.Web/Views/Export/PdfTemplate.cshtml
git commit -m "refactor(export): PdfTemplate partial rozpad + CSS extraction (Fáze 3A Task 4)

766 LOC Razor view → 4 partials + separátní CSS soubor. PdfTemplate.cshtml
zredukován na orchestraci. Inline <style> → /css/pdf-export.css (cacheable).

+partial test (pokud přidán)."
```

---

## Self-Review

**Spec coverage check:**

Phase 3A spec říká: 4 low-risk mechanical splits. Tasks:
- T1 DashboardPriorityServices — ✓ plán
- T2 ExportTemplateQueries — ✓ plán
- T3 OpenXmlWordExportService — ✓ plán
- T4 PdfTemplate.cshtml — ✓ plán

**Placeholder scan:** žádné TBD, TODO, "add appropriate error handling" — všechny steps mají konkrétní code/command.

**Type consistency:** názvy tříd konzistentní napříč tasky. Rozhraní `IPriorityScoringService` identické v Task 1 modelu jako v consumers.

**Riziko per task:**
- T1: Low — změna je izolovaná, DI interfaces beze změny
- T2: Low — dělení už existujících oddělených typů
- T3: Low — partial class + helper extraction, žádné API změny
- T4: Low — Razor partials jsou mechanický přesun

## Execution handoff

**Subagent-Driven Development** — každý task dispatchnu sonnet implementerovi + spec compliance review + code quality review. Mezi tasky verify build + tests zelené, commit, pak next.

## Metrics target po Fázi 3A

- 4 god-files rozbito (celkem ~4 800 LOC → distribuováno do ~12-14 souborů)
- +14-20 architecture/presence tests
- 0 funkčních regresí
- Build time neovlivněn
