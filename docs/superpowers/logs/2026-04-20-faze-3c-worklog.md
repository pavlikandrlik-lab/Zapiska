# Fáze 3C worklog — Backend service decomposition

**Datum dokončení:** 2026-04-20
**Branch:** `codex/senior-refactor-fase-1`

## Commits v 3C

| SHA | Task | Popis |
|---|---|---|
| `7dfecb3` | plan | docs(3c): plan — backend service decomposition |
| `45b5cdf` | T1 | RecordService.WriteCommands.cs (1707 LOC) → 3 partials (SaveRecord 1514, DeleteRecord 147, MeetingIdentifier 83) |
| `b8c7cc5` | T2 | RecordProposalService.cs (1142 LOC) → root (103) + 3 partials (Queries 571, SubmitCommands 258, DecisionCommands 252) |
| `2c7bd45` | T3 | HarmonogramService (858 LOC) → 2 services (HarmonogramService 557 schedule + HarmonogramCatalogService 546 catalog) s oddělenými interfaces |

## Metriky

- **Unit testy:** 454 → 473 (+19 new architecture tests: 8 T1 + 10 T2 + 1 T3-adjusted)
- **God-services rozbito:** 3 (RecordService.WriteCommands, RecordProposalService, HarmonogramService)
- **Nové soubory:** 11 (3 partials T1 + 3 partials T2 + 2 services + 2 interfaces T3 + 3 arch test files)
- **Smazané soubory:** 1 (RecordService.WriteCommands.cs 1707 LOC)
- **API-breaking changes:** 1 (IHarmonogramService interface zúžený — consumers DictionaryService aktualizováni)
- **Consumer files changed:** 2 (DictionaryService.cs + DictionariesServiceTests.cs)

## Architektonické vzory zavedené

### Partial class pattern (T1 + T2)

Applikovaný stabilní vzor z `ProjectService.*` / `MeetingService.*`. Každý split má **root file** držící třídu + constructor + nested records + shared private helpers, a **N partial files** podle responsibility boundary.

**RecordService.\*** (T1) — 3 partials po responsibility:
```
PmTracker.Web/Services/
├── RecordService.cs                   ← root (konstruktor + shared)
├── RecordService.SaveRecord.cs        ← SaveRecordAsync + persistence helpers
├── RecordService.DeleteRecord.cs      ← DeleteRecordAsync + cascade logic
└── RecordService.MeetingIdentifier.cs ← AssignMeetingIdentifierAsync
```

**RecordProposalService.\*** (T2) — root + 3 partials po CQRS-like split:
```
PmTracker.Web/Services/
├── RecordProposalService.cs                  ← root (konstruktor + shared helpers: LoadProposalAsync, DeserializePayload, ResolveScheduleTypeDefinitionsAsync)
├── RecordProposalService.Queries.cs          ← 7 read methods + editor configuration helpers
├── RecordProposalService.SubmitCommands.cs   ← 2 submit commands + validation helpers
└── RecordProposalService.DecisionCommands.cs ← 4 decision commands + Apply*Proposal helpers
```

**Placement rule pro private helpers:** primary-caller principle. Helper volaný **výlučně** z jednoho partialu jde tam. Helper volaný **z více partialů** zůstává v root. Tato disciplína zachovává nízkou coupling mezi partials a dělá intent čitelný.

### Interface split pattern (T3)

HarmonogramService držel dva bounded contexts: schedule compute + dictionary catalog CRUD. Konzumenti byli disjoint (4 schedule-side × 1 catalog-side) — ideální kandidát pro **separate service pair**:

```
PmTracker.Web/Services/Data/
├── IHarmonogramService.cs         ← schedule doménový interface (7 methods)
├── HarmonogramService.cs           ← schedule compute implementation (557 LOC)
├── IHarmonogramCatalogService.cs  ← catalog doménový interface (5 methods, participates in IDictionariesQueriesComposition/CommandsComposition)
└── HarmonogramCatalogService.cs    ← catalog CRUD implementation (546 LOC)
```

**Klíčová design decision:** `IHarmonogramCatalogService` (ne `IHarmonogramService`) teď participuje v kompozici `IDictionariesQueriesComposition`/`IDictionariesCommandsComposition`. Tento posun je významný — dictionary composition framework integrace byla domain concern pouze pro catalog, ne pro schedule.

**Trade-off T3:** `HarmonogramCatalogService` duplikuje 4 schema-loading helpers (`LoadActiveHarmonogramSchemaAsync`, `LoadHarmonogramTypyAsync`, `BuildFallbackSchemaDefinition`, `IsMissingHarmonogramCatalogSchema`). Agent (architecture tests) explicitně zakázal injektovat `IHarmonogramService` do catalog service, aby zůstaly fully decoupled. **Follow-up candidate:** extrahovat tyto helpers do interního `HarmonogramSchemaLoader` / `HarmonogramSchemaQueries` shared component pokud duplikace bude problém — pro teď je to ~40 LOC duplikace akceptovatelná pro fully decoupled services.

## Testing discipline

- **TDD pattern:** arch tests first → failing → split → tests green. Konzistentně přes všechny 3 tasky.
- **Byte-exact method bodies** při přesunu (no "while I'm here" cleanup). Zachovává behavior bit-for-bit.
- **Consumer impact minimization:** T1/T2 beze změny consumer (API identický). T3 pouze 2 consumer files aktualizovány (DictionaryService + testy).

## Bundle / API stability

- T1 + T2: `IRecordService` + `IRecordProposalService` interfaces **beze změny**. Všichni consumers (ZaznamyController, ProjektyController, atd.) netknuté.
- T3: `IHarmonogramService` **zúžený** (catalog methods vyňaty). DictionaryService aktualizován na `IHarmonogramCatalogService`. DataStoreServiceCollectionExtensions přidalo registrace catalog service.

## Risks addressed / open

**Addressed:**
- Partial class ambiguity (root identical constructor se musí zmínit pouze jednou — verified via build)
- Private helper dvojitá definice (architecture tests ensure no helper is defined in 2 partials)
- Circular dependency (partial class inherently šetří od circular — same class)
- Consumer churn (T3 minimalizovaný na 2 files)

**Open / deferred:**
- `HarmonogramCatalogService` duplikuje 4 schema helpers (40 LOC) — acceptable pro decoupling; extract pokud duplikace poroste
- `ResolvePlannedTypeIdsAsync` v RecordProposalService root je dead code (defined, nikdy voláno) — T2 agent ponechal per "preserve behavior exactly" constraint; cleanup kdykoli
- Runtime verification proti SQL stále vyžaduje Citrix manual smoke — deferred jako u 3A/3B

## Lessons learned

1. **Partial class pattern je zlatý standard** pro splits bez API break. Zachovává konstruktor + shared state + umožňuje responsibility-based file organization.
2. **Interface split je vyšší risk** než partial split — konzumenti musí být aktualizováni. Ale když bounded contexts jsou čisté (jako schedule vs. catalog), je to správné řešení.
3. **Architecture tests jsou guard rail proti regresím při refactor-lite budoucích změnách** — zachovávají intent "public method X je v partialu Y".
4. **Primary-caller placement rule pro helpers** (T1 + T2) je mechanical a deterministic — grep usage + decide. Žádný judgment call nutný.
5. **HarmonogramCatalogService duplikace je pragmatic trade-off** — plná decoupling > DRY když helpers jsou small (~40 LOC) a bounded contexts jsou nezávislé.

## Phase 3C summary

3 god-services (1707 + 1142 + 858 = 3707 LOC) rozbity do:
- **9 nových souborů** (6 partials + 2 services + 2 interfaces — minus 2 duplicate counted)
- **1 smazaný soubor** (RecordService.WriteCommands.cs)
- **19 new architecture tests** (z 454 → 473)
- **0 runtime regressions** očekávaných (partial class a interface split jsou compile-time transformations)

## Next — Fáze 3D

**Plán:** `docs/superpowers/plans/2026-04-20-faze-3d-controllers.md` (bude vytvořen)

Cíle:
- `ProjektyController.cs` (882 LOC, 31 actions) → 3 controllers podle responsibility (Projects, ProjectRecords, ProjectProposals)
- Velké ViewModels (ZaznamEditViewModel a další god-VMs) → split per bounded context
- Potenciálně Areas introduction (Admin/*, Projekty/*, Ciselniky/*) pokud controllers dají přirozené grouping

Risk: Medium — consumer jsou Views (Razor), URL routes (SEO/bookmarks), JS bootstrap. Musíme zachovat existing URL schéma přes `[Route]` attributes.
