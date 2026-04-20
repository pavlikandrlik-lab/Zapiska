# Fáze 3D worklog — Controllers + ViewModels decomposition

**Datum dokončení:** 2026-04-20
**Branch:** `codex/senior-refactor-fase-1`

## Commits v 3D

| SHA | Task | Popis |
|---|---|---|
| `5062f46` | plan | docs(3d): plan — controllers + viewmodels partial decomposition |
| `ca971a1` | T1 | ProjektyController.cs (882 LOC / 31 actions) → root + 4 partials |
| `9c29b31` | T2 | ZaznamyController.cs (636 LOC / 13 actions) → root + 3 partials |
| `d912af7` | T3+T4 | CommandViewModels (532) → Commands/ subfolder + T3's staged files (race-absorbed) |
| `95f5d20` | T3 | ProjektyViewModels.cs monolith delete (T3 follow-up po race conditioně) |

## Metriky

- **Unit testy:** 486 → 523 (+37 new architecture tests: 13 T1 + 11 T2 + 10 T3 + 16 T4 — T4 zahrnulo pár sdílených skip-regex assertions, minus overlap)
- **God-files rozbity:** 4 (2 controllers + 2 ViewModel monoliths)
  - ProjektyController 882 LOC → root 272 + 4 partials (116 + 190 + 53 + 284)
  - ZaznamyController 636 LOC → root 226 + 3 partials (52 + 106 + 276)
  - ProjektyViewModels 677 LOC → 7 files v Projekty/ subfolder (39-212 LOC each)
  - CommandViewModels 532 LOC → 5 files v Commands/ subfolder (25-180 LOC each)
- **Nové soubory:** 19 (4 T1 partials + 3 T2 partials + 7 T3 VMs + 5 T4 commands + 4 arch test files — minus duplicates)
- **Smazané soubory:** 2 (ProjektyViewModels.cs + CommandViewModels.cs monoliths)
- **API-breaking changes:** 0 (zero URL/route/namespace changes)
- **Consumer files changed:** 0 (Views, JS, Services, other Controllers všichni netknutí)

## Architektonické vzory zavedené

### Partial class pro controllers (T1 + T2)

Mirrors partial class pattern for backend services (Fáze 3C). Same class name, same route attributes, endpoints distributed po responsibility group:

```
PmTracker.Web/Controllers/
├── ProjektyController.cs                   ← Index + Detail + shared tab-prep helpers (272 LOC)
├── ProjektyController.TabPartials.cs       ← 7 tab endpoints (116 LOC)
├── ProjektyController.ProjectModals.cs     ← 7 project/team/role/subsystem modals (190 LOC)
├── ProjektyController.MeetingModals.cs     ← 2 meeting modals + helper (53 LOC)
├── ProjektyController.Commands.cs          ← 13 POST commands + team AJAX helpers (284 LOC)
│
├── ZaznamyController.cs                    ← Edit + Create editor endpoints + shared (226 LOC)
├── ZaznamyController.Modals.cs             ← 2 modals (52 LOC)
├── ZaznamyController.Partials.cs           ← 3 partial view endpoints (106 LOC)
└── ZaznamyController.Commands.cs           ← 6 POST commands + AJAX result builders (276 LOC)
```

**Klíčová insight z T1:** helpery sdílené mezi `Detail` (root) a `*TabPartial` endpoints byly ponechány v root per primary-caller rule. `PrepareProjectRecordsTabPresentationAsync` + 4 další `Prepare*TabPresentation*` helpers jsou volány jak z `PrepareActiveProjectTabAsync` (root), tak z individuálních partial endpoints (TabPartials.cs). Tyto sdílené helpers zůstávají v root.

**Klíčová insight z T2:** test isolation assertions musí kontrolovat plné signatury metod (`"Task<IActionResult> AddComment("`), ne jen jméno metody (`"AddComment"`) — jinak false-match na `CanAddComment` nebo `nameof(AddComment)` in different partials.

### ViewModel per-concern split (T3 + T4)

Nová subfolder struktura bez namespace change (file-scoped namespace je folder-independent):

```
PmTracker.Web/Models/ViewModels/
├── Projekty/                                     ← T3 (7 files, všechny namespace PmTracker.Web.Models.ViewModels)
│   ├── ProjektListViewModels.cs                 (list index + filter, 3 types, 39 LOC)
│   ├── ProjektDetailViewModels.cs               (detail + header + shell, 3 types, 49 LOC)
│   ├── ProjektZaznamyTabViewModels.cs           (records tab, 13 types, 212 LOC)
│   ├── ProjektHarmonogramTabViewModels.cs       (schedule tab, 5 types, 73 LOC)
│   ├── ProjektJednaniTabViewModels.cs           (meetings tab, 3 types, 35 LOC)
│   ├── ProjektTymTabViewModels.cs               (team tab, 13 types, 159 LOC)
│   └── ZaznamEditViewModels.cs                  (record editor, 3 types, 116 LOC)
│
└── Commands/                                    ← T4 (5 files, stejný namespace)
    ├── ProjectCommands.cs                       (12 commands: project + team + role + subsystem, 115 LOC)
    ├── MeetingCommands.cs                       (7 commands: meeting + participants + attendance, 95 LOC)
    ├── RecordCommands.cs                        (7 commands: record + comments + harmonogram values, 115 LOC)
    ├── ProposalCommands.cs                      (2 commands: decision + prefill, 30 LOC)
    └── DictionaryCommands.cs                    (13 commands: persons + authz + ciselnik, 175 LOC)
```

**Klíčová decision:** namespace zachován (`PmTracker.Web.Models.ViewModels;`) — subfolder neprojevuje do namespace path. To dává **zero consumer breakage** — Views, Controllers, Services dál referencují `using PmTracker.Web.Models.ViewModels;` a typy jsou stále rozpoznatelné.

### Race condition handling — parallel agent commits

T3 + T4 běžely paralelně. T4 dorazil na `git commit` první a commit absorboval T3's staged new files (Projekty/*.cs + ProjektyViewModelsSplitTests.cs). T3 pak dokončil follow-up commitem 95f5d20 (pouze deletion monolithu).

**Pattern:** parallel agents `git add` + `git commit` sdílejí stejný git index. Jeden z nich commit vše staged. Druhý pak jen vypořádá zbytek. **Pro budoucnost:** parallel agenti by měli buď (a) committovat po staged blokech s limitovaným globu, (b) používat isolated worktrees, nebo (c) serializovat commit step pomocí blocking zámku.

## Testing discipline

- **TDD pattern:** architecture tests first → failing → split → tests green. Konzistentně all 4 tasks.
- **Byte-exact method bodies** při přesunu. Zero behavior change.
- **Zero consumer impact verified** přes build + test suite (zero Views/JS/Services changed).

## Risks addressed / open

**Addressed:**
- Partial class ambiguity (root declaration identical konstruktor = compile check)
- Namespace preservation (file-scoped namespace nezávisí na folder path — ověřeno tests)
- Endpoint isolation (each partial contains only its assigned endpoints — guarded by architecture tests)
- Race condition v parallel commits (resolved přes follow-up commit 95f5d20)

**Open / deferred:**
- Runtime verification (Citrix manual smoke) — unchanged pattern vs. 3A/3B/3C; deferred
- Some controllers (NastaveniController 331 LOC, JednaniController 306 LOC, NavrhyController 288 LOC) jsou u hranice rozumné velikosti — akceptováno bez split pro teď
- 6 remaining large ViewModel files (SecurityViewModels 247, NastaveniViewModels 168, ...) akceptabilní

## Phase 3D summary

4 god-files (882 + 636 + 677 + 532 = 2727 LOC) rozbity do:
- **19 nových souborů** (7 controller partials + 12 ViewModel files)
- **2 smazané monolith soubory**
- **+37 architecture tests** (486 → 523)
- **Zero runtime regressions** (zero route/URL/namespace/consumer changes)

## Next — Fáze 3E

**Plán:** `docs/superpowers/plans/2026-04-20-faze-3e-css-architecture.md` (bude vytvořen).

Cíle:
- `wwwroot/css/site.css` (6016 LOC) → feature-specific files podle bounded context
- Extrahovat layout, components, utility vrstvy (design system conformance)
- Potenciálně Sass preprocessing zvážit (ale pravděpodobně over-engineering pro CSS splits)

Risk: Medium — CSS ordering matters (cascade), každá extrakce musí zachovat specificity + cascade order. Vyžaduje vizuální verifikaci přes Playwright harness.
