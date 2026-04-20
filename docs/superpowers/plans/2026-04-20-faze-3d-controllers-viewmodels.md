# Fáze 3D — Controllers + ViewModels decomposition — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Rozbít 2 největší controllery (ProjektyController 882 LOC/31 actions, ZaznamyController 636 LOC) partial class patternem a rozdělit 2 největší ViewModel soubory (ProjektyViewModels 677 LOC, CommandViewModels 532 LOC) podle bounded context. **Zero URL/route changes, zero consumer changes** — partial splits zachovávají public API endpoints i DTO tvary.

**Architecture:** Applikujeme stejný partial class pattern jako u backend services v 3C (`RecordService.*`, `RecordProposalService.*`). Controller je `public sealed partial class`, endpoints distributed across partial files podle endpoint group (tab partials, modals, commands). ViewModels splits: jeden file per view model (nebo per logicky-příbuzná skupina malých VMs).

**Tech Stack:** .NET 8, ASP.NET Core 8 MVC, partial class pattern, xUnit + FluentAssertions, ArchitectureTestBase.

---

## File Structure

### Task 1 — ProjektyController (882 LOC / 31 actions → root + 4 partials)

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Controllers/ProjektyController.cs` | **KEEP:** Add `partial` keyword. Root: konstruktor, private tab konstanty (RecordsTab, ScheduleTab...), shared helpers (NormalizeProjectTab, BuildProjectStatusOptionsAsync, PrepareProjectDetailPresentationAsync, PrepareActiveProjectTabAsync), Index + Detail endpoints |
| `PmTracker.Web/Controllers/ProjektyController.TabPartials.cs` | `RecordsTabPartial`, `HarmonogramTabPartial`, `JednaniTabPartial`, `TymTabPartial`, `NavrhyTabPartial`, `RecordMeetingCommentStates`, `SearchProjectMemberCandidates` + `PrepareProjectRecordsTabPresentationAsync` + `PrepareRecordCardShellPresentation` helpers (7 actions) |
| `PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs` | `NewProjectModal`, `EditProjectModal`, `DeleteProjectModal`, `AddTeamMemberModal`, `AssignProjectRoleModal`, `AssignProjectSubsystemModal`, `AssignProjectSubsystemRoleModal` (7 modal endpoints) |
| `PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs` | `NewMeetingModal`, `EditMeetingModal` + `EnsureReadableMeetingTimeError` helper (2 modals) |
| `PmTracker.Web/Controllers/ProjektyController.Commands.cs` | `SaveProject`, `DeleteProject`, `SaveMeeting`, `DeleteMeeting`, `SaveTeamMember`, `RemoveTeamMember`, `AssignProjectRole`, `DeactivateProjectRole`, `AssignProjectSubsystem`, `ReorderProjectSubsystem`, `DeactivateProjectSubsystem`, `AssignProjectSubsystemRole`, `DeactivateProjectSubsystemRole` (13 POST commands) |

### Task 2 — ZaznamyController (636 LOC → root + 3 partials)

Audit první — pak rozdělit na (předběžně):

| Soubor | Obsah (target) |
|---|---|
| `PmTracker.Web/Controllers/ZaznamyController.cs` | **KEEP:** Add `partial`. Root: konstruktor + shared |
| `PmTracker.Web/Controllers/ZaznamyController.Editor.cs` | Editor GET endpoints (NewZaznamModal, EditZaznamModal atd.) |
| `PmTracker.Web/Controllers/ZaznamyController.Commands.cs` | POST commands (SaveZaznam, DeleteZaznam atd.) |
| `PmTracker.Web/Controllers/ZaznamyController.Partials.cs` | Partial view endpoints |

### Task 3 — ProjektyViewModels split (677 LOC → per-concern files)

Současný monolith drží ~7-10 view modelů. Rozdělit per view model concern:

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektListViewModels.cs` | ProjektListViewModel, ProjektListItemViewModel, ProjektListFilterViewModel |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModels.cs` | ProjektDetailViewModel + nested tab child VMs |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` | ProjektZaznamyTabViewModel, ProjektZaznamCardShellViewModel |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektHarmonogramTabViewModels.cs` | Harmonogram tab VMs |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektJednaniTabViewModels.cs` | Jednani tab VMs |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektTymTabViewModels.cs` | Tym tab VMs |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektNavrhyTabViewModels.cs` | Navrhy tab VMs |

Subfolder `Projekty/` is new; same namespace `PmTracker.Web.Models.ViewModels` zachován → consumers (Views, controllers) netknutí.

### Task 4 — CommandViewModels split (532 LOC → per-domain files)

| Soubor | Obsah |
|---|---|
| `PmTracker.Web/Models/ViewModels/Commands/ProjectCommands.cs` | SaveProjectCommand, SoftDeleteProjectCommand, team/role/subsystem commands |
| `PmTracker.Web/Models/ViewModels/Commands/MeetingCommands.cs` | SaveMeetingCommand, DeleteMeetingCommand |
| `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` | SaveRecordCommand + record-related |
| `PmTracker.Web/Models/ViewModels/Commands/ProposalCommands.cs` | SaveProposalCommand, ProposalDecisionCommand |
| `PmTracker.Web/Models/ViewModels/Commands/DictionaryCommands.cs` | SaveCiselnikRowCommand, DeleteCiselnikRowCommand |

Subfolder `Commands/`; same namespace zachován.

---

## Task 1: ProjektyController partial split

### Steps

- [ ] **Step 1.1: Audit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
wc -l PmTracker.Web/Controllers/ProjektyController.cs
grep -nE "^\s*(public|private|\[HttpGet|\[HttpPost)" PmTracker.Web/Controllers/ProjektyController.cs
```

Mapovat všech 31 actions + helpers do 5 souborů dle tabulky výše.

- [ ] **Step 1.2: Architecture tests FIRST**

Create `PmTracker.Tests.Unit/Architecture/ProjektyControllerSplitTests.cs` using `using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;`:

```csharp
[Fact]
public void RootFile_ShouldBePartialClass()
{
    var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.cs"));
    content.Should().Contain("public sealed partial class ProjektyController");
}

[Theory]
[InlineData("PmTracker.Web/Controllers/ProjektyController.TabPartials.cs")]
[InlineData("PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs")]
[InlineData("PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs")]
[InlineData("PmTracker.Web/Controllers/ProjektyController.Commands.cs")]
public void NewPartial_ShouldExistAndBePartial(string relativePath) { ... }

[Fact]
public void TabPartials_ShouldContainTabEndpointsOnly() { ... }

[Fact]
public void ProjectModals_ShouldContainModalEndpointsOnly() { ... }

[Fact]
public void MeetingModals_ShouldContainMeetingModalsOnly() { ... }

[Fact]
public void Commands_ShouldContainPostActionsOnly() { ... }

[Fact]
public void RootFile_ShouldContainIndexAndDetail()
{
    var content = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/ProjektyController.cs"));
    content.Should().Contain("Task<IActionResult> Index");
    content.Should().Contain("Task<IActionResult> Detail");
}
```

- [ ] **Step 1.3 – 1.6: Extract 4 partials**

Create each new partial file with:
```csharp
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
// ... only required usings

namespace PmTracker.Web.Controllers;

public sealed partial class ProjektyController
{
    // moved actions + their exclusive helpers
}
```

Root file: add `partial` keyword. Remove moved actions (keep only Index + Detail + shared helpers).

- [ ] **Step 1.7: Build + test + commit**

Expected: 473 → 482 tests. Build 0 errors. Same URL routes → zero consumer breakage.

```bash
git add PmTracker.Web/Controllers/ProjektyController.cs \
        PmTracker.Web/Controllers/ProjektyController.TabPartials.cs \
        PmTracker.Web/Controllers/ProjektyController.ProjectModals.cs \
        PmTracker.Web/Controllers/ProjektyController.MeetingModals.cs \
        PmTracker.Web/Controllers/ProjektyController.Commands.cs \
        PmTracker.Tests.Unit/Architecture/ProjektyControllerSplitTests.cs

git commit -m "refactor(controllers): rozbití ProjektyController.cs do 5 partials (Fáze 3D Task 1)

882 LOC / 31 actions → root + 4 partial files podle endpoint group:
- ProjektyController.cs — Index, Detail + shared helpers + partial keyword
- ProjektyController.TabPartials.cs — 7 tab endpoints (Records/Harmonogram/Jednani/Tym/Navrhy + aux)
- ProjektyController.ProjectModals.cs — 7 project/team/role/subsystem modal endpoints
- ProjektyController.MeetingModals.cs — 2 meeting modal endpoints
- ProjektyController.Commands.cs — 13 POST commands

Zero URL/route changes (same class name, same [HttpGet]/[HttpPost] routes).
Zero consumer changes (Views, JS, URLs všechny netknutí)."
```

---

## Task 2: ZaznamyController partial split

### Steps

- [ ] **Step 2.1: Audit**

```bash
grep -nE "^\s*(public|\[HttpGet|\[HttpPost)" PmTracker.Web/Controllers/ZaznamyController.cs
```

Finalize split plan based on actual endpoint distribution.

- [ ] **Step 2.2: Architecture tests FIRST**

- [ ] **Step 2.3: Extract partials**

- [ ] **Step 2.4: Build + test + commit**

Expected: +6 tests. Same pattern jako Task 1.

---

## Task 3: ProjektyViewModels split

### Steps

- [ ] **Step 3.1: Audit**

```bash
wc -l PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs
grep -nE "^public (sealed |static )?(partial )?(class|record|interface)" PmTracker.Web/Models/ViewModels/ProjektyViewModels.cs
```

Identifikovat všechny VM typy a jejich vazby.

- [ ] **Step 3.2: Architecture tests FIRST**

```csharp
[Fact]
public void OriginalViewModelsFile_ShouldBeDeleted() { ... }

[Theory]
[InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektListViewModels.cs")]
[InlineData("PmTracker.Web/Models/ViewModels/Projekty/ProjektDetailViewModels.cs")]
// ... 7 new files
public void NewSplitFile_ShouldExistWithCorrectNamespace(string relativePath) { ... }
```

- [ ] **Step 3.3 – 3.9: Create 7 new files + delete original**

Each file uses same namespace `PmTracker.Web.Models.ViewModels;` (zero consumer impact):

```csharp
namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektListViewModel { ... }
```

- [ ] **Step 3.10: Build + test + commit**

Expected: +8 tests. Build 0 errors (namespace zachován → konzumenti se nedotknou).

---

## Task 4: CommandViewModels split

Podobně jako Task 3 — 5 soborů per domain, subfolder `Commands/`, namespace zachován.

- [ ] **Step 4.1 – 4.7: Audit → tests → split → verify → commit**

---

## Self-Review

**Spec coverage:**
- T1 ProjektyController split ✓
- T2 ZaznamyController split ✓ (rough — audit potřebný)
- T3 ProjektyViewModels per-concern split ✓
- T4 CommandViewModels per-domain split ✓

**Risks:**
- **Low** overall — partial class pattern + namespace-preserving VM split jsou compile-time only, zero URL/consumer changes.
- Highest risk: missed helper in root vs. partial placement (compile error immediate).
- Namespace zachování ve VM splits musí být důsledné — subfolder nemění namespace pokud použijeme `file-scoped namespace PmTracker.Web.Models.ViewModels;` (bez reflecting folder).

**Exit criteria:**
- 2 god-controllers rozbiti partial classes (ProjektyController + ZaznamyController)
- 2 největší VM monoliths (ProjektyViewModels + CommandViewModels) rozbity per-concern
- ~25-30 new architecture tests (5-10 per task)
- API endpoints beze změny (URL routes identické)
- Consumers (Views, JS, controllers) beze změny (zero churn)
- Unit tests 473 → ~500+

---

## Next — Fáze 3E

CSS architectural cleanup (`site.css` 6016 LOC → feature-specific files). Separate plan `2026-04-20-faze-3e-css-architecture.md` bude vytvořen.
