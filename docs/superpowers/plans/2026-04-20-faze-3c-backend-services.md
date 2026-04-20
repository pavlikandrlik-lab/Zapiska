# Fáze 3C — Backend service decomposition — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox syntax.

**Goal:** Rozdělit 3 největší backend services (RecordService.WriteCommands 1707 LOC, RecordProposalService 1142 LOC, HarmonogramService 858 LOC) do partials podle command/query/responsibility. API beze změny, consumers (controllers) beze změny.

**Architecture:** Applikujeme stabilní partial class pattern z `ProjectService.*` / `MeetingService.*` / `PeopleService.*`. Při rozdělení `HarmonogramService` interface-split (schedule domain vs. dictionary catalog concern — jinak pak by "service" držela dvě doménové vrstvy v jednom).

**Tech Stack:** .NET 8, EF Core 8, partial class pattern, xUnit + FluentAssertions, ArchitectureTestBase (z 3A/3B).

---

## File Structure

**Task 1 — RecordService.WriteCommands (1707 LOC → 3 partials):**

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | `SaveRecordAsync` + private helpers (validate, persist harmonogram, external links, collaborators) |
| `PmTracker.Web/Services/RecordService.DeleteRecord.cs` | `DeleteRecordAsync` + soft-delete policy + cascade audit |
| `PmTracker.Web/Services/RecordService.MeetingIdentifier.cs` | `AssignMeetingIdentifierAsync` + validation |
| `PmTracker.Web/Services/RecordService.WriteCommands.cs` | **DELETED** |

**Task 2 — RecordProposalService (1142 LOC → partial class pattern):**

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/RecordProposalService.cs` | **KEEP:** root class declaration + constructor + shared helpers |
| `PmTracker.Web/Services/RecordProposalService.Queries.cs` | `BuildProjectProposalsTabAsync`, `CanViewProposalTabAsync`, 4 editor composition methods (CreateProposal, ScheduleProposal, Detail, Editable) |
| `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` | `SubmitCreateRecordProposalAsync`, `SubmitScheduleProposalAsync` |
| `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs` | `ApproveProposalAsync`, `RejectProposalAsync`, `RejectAndTakeOverAsync`, `RejectAndEditAsync` |

**Task 3 — HarmonogramService + HarmonogramCatalogService (interface split):**

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Services/Data/HarmonogramService.cs` | **MODIFIED:** schedule schema queries + computation only |
| `PmTracker.Web/Services/Data/HarmonogramCatalogService.cs` | **NEW:** catalog view composition + save/delete step rows |
| `PmTracker.Web/Services/Data/IHarmonogramService.cs` | **MODIFIED:** reduced interface — schedule domain only |
| `PmTracker.Web/Services/Data/IHarmonogramCatalogService.cs` | **NEW:** catalog interface (dictionary concern) |

---

## Task 1: RecordService.WriteCommands.cs split

**Goal:** 1707 LOC / 3 public methods → 3 focused partial files.

### Steps

- [ ] **Step 1.1: Audit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
wc -l PmTracker.Web/Services/RecordService.WriteCommands.cs
grep -nE "^    (public|private|internal)" PmTracker.Web/Services/RecordService.WriteCommands.cs | head -40
grep -rn "SaveRecordAsync\|DeleteRecordAsync\|AssignMeetingIdentifierAsync" PmTracker.Web --include="*.cs" | grep -v "/Services/RecordService" | head -15
```

- [ ] **Step 1.2: Architecture tests FIRST**

Create `PmTracker.Tests.Unit/Architecture/RecordServiceWriteSplitTests.cs`:

```csharp
using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Architecture;

public sealed class RecordServiceWriteSplitTests
{
    [Fact]
    public void OriginalWriteCommands_ShouldBeDeleted()
    {
        File.Exists(ResolvePath("PmTracker.Web/Services/RecordService.WriteCommands.cs"))
            .Should().BeFalse("Fáze 3C Task 1 smazal monolitický soubor, obsah rozdělen do 3 partials");
    }

    [Theory]
    [InlineData("PmTracker.Web/Services/RecordService.SaveRecord.cs")]
    [InlineData("PmTracker.Web/Services/RecordService.DeleteRecord.cs")]
    [InlineData("PmTracker.Web/Services/RecordService.MeetingIdentifier.cs")]
    public void NewPartial_ShouldExistAndBePartial(string relativePath)
    {
        var full = ResolvePath(relativePath);
        File.Exists(full).Should().BeTrue($"{relativePath} existuje po Fázi 3C Task 1");
        File.ReadAllText(full).Should().Contain("partial class RecordService",
            "každý nový soubor deklaruje partial class RecordService");
    }

    [Fact]
    public void SaveRecordPartial_ShouldContainSaveRecordOnly()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordService.SaveRecord.cs"));
        content.Should().Contain("public async Task<int> SaveRecordAsync");
        content.Should().NotContain("public async Task DeleteRecordAsync", "DeleteRecord patří do DeleteRecord.cs");
        content.Should().NotContain("AssignMeetingIdentifierAsync", "MeetingIdentifier patří do MeetingIdentifier.cs");
    }

    [Fact]
    public void DeleteRecordPartial_ShouldContainDeleteRecordOnly()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordService.DeleteRecord.cs"));
        content.Should().Contain("DeleteRecordAsync");
        content.Should().NotContain("public async Task<int> SaveRecordAsync");
    }

    [Fact]
    public void MeetingIdentifierPartial_ShouldContainMeetingIdentifier()
    {
        var content = File.ReadAllText(ResolvePath("PmTracker.Web/Services/RecordService.MeetingIdentifier.cs"));
        content.Should().Contain("AssignMeetingIdentifierAsync");
    }

    [Fact]
    public void SaveRecordFile_ShouldBeReasonablySized()
    {
        var file = ResolvePath("PmTracker.Web/Services/RecordService.SaveRecord.cs");
        var loc = File.ReadAllLines(file).Length;
        loc.Should().BeLessThan(1300, "SaveRecord partial může být velký, ale ne větší než 1300 LOC");
    }
}
```

- [ ] **Step 1.3: Read god-file**

```
Read PmTracker.Web/Services/RecordService.WriteCommands.cs (multi-chunk)
```

- [ ] **Step 1.4: Create RecordService.SaveRecord.cs**

Extract `SaveRecordAsync` + private helpers it uses (validate, persist harmonogram, etc.). Private helpers that are called ONLY from SaveRecord go into this partial. Helpers called from Delete or MeetingIdentifier too → stay in root `RecordService.cs` or go to where they logically belong.

- [ ] **Step 1.5: Create RecordService.DeleteRecord.cs**

Extract `DeleteRecordAsync` + its private helpers (soft-delete validation, cascade audit).

- [ ] **Step 1.6: Create RecordService.MeetingIdentifier.cs**

Extract `AssignMeetingIdentifierAsync` + its private helpers.

- [ ] **Step 1.7: Delete original**

```bash
git rm PmTracker.Web/Services/RecordService.WriteCommands.cs
```

- [ ] **Step 1.8: Build + test**

```bash
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Expected: Build 0 error. Tests 446 + 6 = 452 pass.

- [ ] **Step 1.9: Commit**

```bash
git add PmTracker.Web/Services/RecordService.SaveRecord.cs \
        PmTracker.Web/Services/RecordService.DeleteRecord.cs \
        PmTracker.Web/Services/RecordService.MeetingIdentifier.cs \
        PmTracker.Web/Services/RecordService.WriteCommands.cs \
        PmTracker.Tests.Unit/Architecture/RecordServiceWriteSplitTests.cs

git commit -m "refactor(record): rozbití RecordService.WriteCommands.cs do 3 partials (Fáze 3C Task 1)

1707 LOC / 3 public methods → 3 partial files:
- RecordService.SaveRecord.cs — SaveRecordAsync + persistence helpers
- RecordService.DeleteRecord.cs — DeleteRecordAsync + cascade logic
- RecordService.MeetingIdentifier.cs — AssignMeetingIdentifierAsync

Partial class pattern konzistentní s ProjectService.*, MeetingService.*.
API (IRecordService) beze změny. Consumer (ZaznamyController) beze změny."
```

---

## Task 2: RecordProposalService.cs split

**Goal:** 1142 LOC / 13 public methods → partial class pattern (Queries / SubmitCommands / DecisionCommands).

### Steps

- [ ] **Step 2.1: Audit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -nE "^    public async" PmTracker.Web/Services/RecordProposalService.cs
```

Map 13 methods → 3 groups.

- [ ] **Step 2.2: Architecture tests FIRST**

`PmTracker.Tests.Unit/Architecture/RecordProposalServiceSplitTests.cs` with tests matching T1 pattern.

- [ ] **Step 2.3: Modify root RecordProposalService.cs**

Convert root class to `public sealed partial class` (add `partial` keyword). Keep constructor + shared private helpers.

- [ ] **Step 2.4: Create RecordProposalService.Queries.cs**

Move `CanViewProposalTabAsync`, `BuildProjectProposalsTabAsync`, editor composition methods (ConfigureCreateProposalEditor, ConfigureScheduleProposalEditor, ConfigureProposalDetailViewModel, etc.).

- [ ] **Step 2.5: Create RecordProposalService.SubmitCommands.cs**

Move `SubmitCreateRecordProposalAsync`, `SubmitScheduleProposalAsync`.

- [ ] **Step 2.6: Create RecordProposalService.DecisionCommands.cs**

Move `ApproveProposalAsync`, `RejectProposalAsync`, `RejectAndTakeOverAsync`, `RejectAndEditAsync`.

- [ ] **Step 2.7: Build + test**

Expected: +6 tests = 458 total.

- [ ] **Step 2.8: Commit**

---

## Task 3: HarmonogramService split (schedule + catalog interface separation)

**Goal:** Rozdělit HarmonogramService (858 LOC) na 2 cohesive services:
- `HarmonogramService` — schedule domain: schema queries, computation, version tracking
- `HarmonogramCatalogService` — dictionary domain: catalog views, step row save/delete

Tyto dva services řeší různé bounded contexts. Současný HarmonogramService drží oba protože historicky vznikly spolu, ale jejich konzumenti jsou disjoint (schedule-facing services vs. dictionary catalog controllers).

### Steps

- [ ] **Step 3.1: Audit + identify interface split**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -nE "^    public async Task" PmTracker.Web/Services/Data/HarmonogramService.cs
cat PmTracker.Web/Services/Data/IHarmonogramService.cs 2>/dev/null || grep -rn "interface IHarmonogramService" PmTracker.Web --include="*.cs"
grep -rn "IHarmonogramService\b" PmTracker.Web --include="*.cs" | grep -v "/Data/Harmonogram" | head -15
```

Identify consumers per method. Maps:
- Schedule domain consumers: RecordProposalService, ProjectService, ProjectDashboardService, DashboardPriorityServices
- Catalog consumers: DictionaryService, CiselnikyController

- [ ] **Step 3.2: Architecture tests + interface tests**

- [ ] **Step 3.3: Create IHarmonogramCatalogService.cs** (nový interface)

Extract catalog methods signatures:
- `BuildCiselnikDetailAsync`
- `BuildHarmonogramKrokyCiselnikDetailAsync`
- `CountHarmonogramCatalogRowsAsync`
- `SaveHarmonogramStepRowAsync`
- `DeleteHarmonogramStepRowAsync`

- [ ] **Step 3.4: Update IHarmonogramService.cs**

Remove catalog methods. Keep only schedule domain:
- `GetActiveHarmonogramSchemaAsync`
- `GetSchemaForRecordAsync`
- `BuildRecordScheduleTypeDefinitions`
- `BuildHarmonogramVypocetPublic`
- `BuildHarmonogramSouhrn`
- `EnsurePersistedActiveHarmonogramSchemaVersionAsync`

- [ ] **Step 3.5: Create HarmonogramCatalogService.cs**

New class implementing `IHarmonogramCatalogService`. Moves catalog methods + catalog-specific private helpers.

- [ ] **Step 3.6: Trim HarmonogramService.cs**

Ponechat jen schedule domain. Delete catalog methods (přesunuty).

- [ ] **Step 3.7: DI registration update**

Add `IHarmonogramCatalogService` registration. Keep `IHarmonogramService` registration (narrowed).

- [ ] **Step 3.8: Update consumers**

- Consumers of schedule methods: beze změny (interface je jen užší)
- Consumers of catalog methods: change `IHarmonogramService` → `IHarmonogramCatalogService` dependency. Likely `DictionaryService` + `CiselnikyController`. Count expected: 2-3 files.

- [ ] **Step 3.9: Build + test + commit**

---

## Self-Review

**Spec coverage:**
- T1 RecordService.WriteCommands split ✓
- T2 RecordProposalService partial pattern ✓
- T3 HarmonogramService interface split ✓

**Risks:**
- T3 is highest risk (interface split → consumer updates). Do last so T1/T2 stabilize first.
- T1 has largest partial (SaveRecord likely > 1200 LOC). Acceptable — cohesive business transaction.
- Private helper placement: place in partial that's the primary caller; if shared, stay in root.

**Exit criteria:**
- 3 god-services rozbito
- ~15-18 new architecture tests (5-6 per task)
- API interfaces beze změny (except T3 interface narrowing)
- Consumers updated pouze v T3
- Unit tests 446 → 464+
