# Fáze 3B worklog — JavaScript module splits

**Datum dokončení:** 2026-04-20
**Branch:** `codex/senior-refactor-fase-1`

## Commits v 3B

| SHA | Task | Popis |
|---|---|---|
| `57804ff` | T1 | recordEditor.js (1919 LOC) → 5 souborů v recordEditor/ |
| `7179600` | T2 + 3A followup | schedule.js (1697 LOC) → 5 souborů + 3A review cleanup (ArchitectureTestBase, narrow partial models, rename duplicates, textSegment internal) |
| `1f83cb3` | T3 | pickers.js (1530 LOC) → 5 souborů v pickers/ |
| `4ac971e` | T4 | filters.js (1105 LOC) → 4 souborů v filters/ |
| `6fe4214` | T5 | ui.js (949 LOC) → 3 soubory v ui/ |
| `8f1bcf8` | T5 followup | JsBundleImportConsistencyTests update (ui.js → ui/print.js) |

## Metriky

- **Unit testy:** 390 → 446 (+56 new architecture tests)
- **God-moduly rozbito:** 5 (recordEditor, schedule, pickers, filters, ui)
- **Nové soubory:** 26 (4+5+5+4+3 submodulů + 5 barrel modifikací)
- **LOC redistribution:** ~7 200 LOC reorganizovaných (1919 + 1697 + 1530 + 1105 + 949)
- **API-breaking changes:** 0 (všechny root moduly jsou backward-compat barrel re-exports)
- **Consumer files changed:** 0 (bootstrap.js, recordRefresh.js, ajax.js nedotčeny)

## Architektonické vzory zavedené

### Folder-per-feature pattern

```
wwwroot/js/modules/
├── recordEditor/
│   ├── navigation.js     ← URL building, chooser UI
│   ├── form.js           ← tabs, metadata bindings, task-type
│   ├── richtext.js       ← Quill integration
│   ├── draft.js          ← storage, dirty-state, close-guard
│   └── index.js          ← initRecordFormEnhancements orchestrator + re-exports
├── recordEditor.js       ← barrel: export * from "./recordEditor/index.js"
├── schedule/
│   ├── filters.js        ← schedule + gantt filter panel state
│   ├── gantt.js          ← ProjectGanttBoard class
│   ├── timeline.js       ← axis tick + render utilities
│   ├── block.js          ← ScheduleBlockRenderer (intact ~850 LOC)
│   └── index.js          ← planner + scheduleUi orchestrator
├── schedule.js           ← barrel
├── pickers/
│   ├── date.js
│   ├── time.js
│   ├── person.js
│   ├── adPerson.js
│   └── index.js
├── pickers.js            ← barrel
├── filters/
│   ├── projectFilter.js
│   ├── recordDisplay.js
│   ├── printFilter.js
│   └── index.js          ← initProjectRecordsUi orchestrator + state persistence
├── filters.js            ← barrel
├── ui/
│   ├── print.js          ← print chooser, aux chooser registry
│   ├── floating.js       ← floating panel system
│   └── index.js          ← rainbow render + re-exports
└── ui.js                 ← barrel
```

### Barrel pattern — backward compat

Každý split feature má root modul (`recordEditor.js`, `schedule.js`, atd.) převedený na barrel:

```js
// e.g., recordEditor.js
export * from "./recordEditor/index.js";
```

Consumer files (`bootstrap.js`, `ajax.js`, `recordRefresh.js`) se nedotkly — jejich `import { X, Y } from "./recordEditor.js"` dále funguje. Dlouhodobě mohou migrate na direct `import from "./recordEditor/index.js"` (clean final state, Fáze 3E nebo samostatně).

### Circular dependency resolution

Ve všech případech kde by vznikla circular dependency mezi submoduly, byl orchestrator (`index.js`) použit jako arbitrátor:

- **filters/**: `handleProjectFilterInputChange` přesunut do `index.js` (jinak `projectFilter.js ↔ recordDisplay.js` cycle)
- **schedule/**: `persistScheduleFilterState` + `persistGanttFilterState` do `index.js` (jinak `filters.js → gantt.js → filters.js` cycle)
- **recordEditor/**: `requestRecordEditorModalClose` + `PageCancel` v `draft.js` (ne navigation.js) — avoid `navigation → draft → navigation` cycle

### Architecture tests — shared base

DRY cleanup v commitu `7179600` založil `PmTracker.Tests.Unit/Architecture/ArchitectureTestBase.cs` se sdílenými `RepoRoot()` + `ResolvePath()` helpers. Všechny test classes v `Architecture/` folderu ho nyní používají přes `using static` — ~50 LOC duplikace odstraněno.

## Testing discipline

- **TDD pattern konzistentně**: architecture tests first → failing → split → tests green
- **Bundle sync verification** v každém tasku — architecture tests ověřují key exports in `site.bundle.js`
- **Cross-submodule boundary tests** — verify každý submodul neobsahuje typy jiných submodulů

## Bundle strategy

`site.bundle.js` obsahuje flat concatenation existujícího kódu — po splitech submodulů se **runtime chování NEMĚNÍ** (bundle má kód inline). Klíčové je verifikovat, že bundle obsahuje klíčové exports (grep + unit tests). Minor drift mezi moduly a bundle (e.g., import ordering) je OK — jedná se o buildovaný artefakt.

Fáze 3E může zvážit introduction build scriptu pro auto-regeneraci bundle z moduly, což by zabránilo ruční sync chybám. Pro teď funguje ruční disciplína + architecture test guards.

## Risks addressed / open

**Addressed:**
- Circular dependencies (resolved přes orchestrator pattern)
- Consumer API breakage (barrel zachovává all exports)
- Bundle desync (architecture tests verify key exports present)
- Test coverage gaps (každý submodul má dedicated content test)

**Open / deferred:**
- Runtime verification vyžaduje live SQL (Citrix manual smoke) — defered
- `ScheduleBlockRenderer` ~850 LOC v block.js — zachován intact (cohesive class); potenciální decomposition v budoucí fázi pokud class roste
- Long-term migration consumers z barrel na direct submodule imports (cleanup v 3E)

## Lessons learned

1. **Parallel agents work well when files don't overlap** — T4 (filters) a T5 (ui) běžely paralelně bez konfliktu, protože každý pracoval s různými folders
2. **Test file update discipline**: test file updates po refactor (e.g., ProposalEditorLockedFieldsTests after recordEditor split, JsBundleImportConsistencyTests after ui split) — musí být součást task scope
3. **Barrel pattern je zlatý standard** pro backward-compat JS refactory — consumer files zero-change, migration je transparent
4. **Orchestrator pattern** (index.js) elegantně řeší circular deps — submoduly zůstávají izolované, index.js koordinuje

## Next — Fáze 3C

**Plán:** `docs/superpowers/plans/2026-04-20-faze-3c-backend-services.md` (bude vytvořen)

Cíle:
- `RecordService.WriteCommands.cs` (1707 LOC) → 3 partials (SaveRecord, DeleteRecord, MeetingIdentifier) s extrahovanými private helpers
- `RecordProposalService.cs` (1142 LOC) → partial class pattern (Queries, SubmitCommands, DecisionCommands)
- `Services/Data/HarmonogramService.cs` (858 LOC) → separate schedule domain od dictionary catalog

Risk: Medium — konzumenti (controllers) se nedotýkají, ale DI + unit tests mohou vyžadovat drobné updaty.
