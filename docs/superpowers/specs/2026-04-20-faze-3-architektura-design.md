# Fáze 3 — Architektonický refactor PM Tracker (design)

**Datum:** 2026-04-20
**Autor:** Claude (autonomous), pro review uživatelem
**Branch:** `codex/senior-refactor-fase-1` (po dokončení Fáze 2E)
**Scope:** Rozbití god-files, sjednocení architektury, čitelná struktura v celé aplikaci

## Cíl

Po Fázích 1 + 2 (tag-helper primitivy + views migrace + modální systém) je codebase technicky v pořádku, ale **strukturálně nedůvěryhodný** pro další rozvoj:

- 5 backend souborů > 800 LOC s více než jednou odpovědností v jednom souboru
- 5 JS modulů > 900 LOC mixujících navigaci, stav, persistenci, UI
- 1 controller (`ProjektyController`) drží 5 feature-oblastí a 31 akcí
- `site.css` je 6 016 LOC v jednom souboru, styly záznamu a modálu scattered

Cíl Fáze 3: **každý soubor má jednu jasně pojmenovanou odpovědnost**, soubory mají jasná API s předvídatelnými spotřebiteli, architektura je navigabilní bez full-text grepu. Model k následování: `PmTracker.Web/Services/Settings/` — 11 souborů s Commands/Queries/Factory disciplínou, celkem 1 383 LOC.

## Non-goals (explicitně)

- **Funkční změny:** žádná změna chování. Všechny splits jsou mechanické + API-preserving.
- **Greenfield rewrite:** ne. Držíme existující domain model, ORM, auth, routing.
- **Nové feature:** ne. Fáze 3 je čistě architektonická.
- **Přepis testů:** testy se upraví jen tam, kde se změnil soubor, který testují. Asserce zachováváme.
- **Optimalizace výkonu:** pokud narazíme, poznamenáme si a odsuneme. Fáze 3 není performance work.

## Principy

1. **Jeden soubor = jedna odpovědnost.** Velikost není primární metrika, ale pokud soubor > 500 LOC v C# / > 300 LOC v JS a zároveň drží více nekoherentních odpovědností, split.
2. **Partial class pattern pro velké service** (`ProjectService.*`, `RecordService.*`, `MeetingService.*`) — osvědčený pattern v projektu, applikujeme konzistentně.
3. **Command/Query rozdělení na folder-level** pro domény s oběma operacemi (`Settings/` je vzor — `Commands.cs` + `Queries.cs`).
4. **JS moduly = feature boundaries, ne technické vrstvy.** `recordEditor.navigation.js`, ne `recordEditor.utils.js`.
5. **CSS = per-feature stylesheet + global reset/components.** `site.css` se drží pouze pro globální reset + variables + gov-* overrides. Feature styly do samostatných souborů importovaných přes `@import` nebo link tagy.
6. **DRY ne na úkor coupling.** Raději duplikace 3 řádků než falešná abstrakce.
7. **API preserving:** public consumers (Controllers, Views) se NESMÍ dotknout, pokud to není explicitní cíl toho konkrétního sub-phase.
8. **TDD:** každý split má pre-existing testy nebo si přidáme markup/presence test dokazující strukturu. Build + testy musí být vždy zelené po každém commitu.

## Decomposition strategy

Fáze 3 se rozloží na **5 sub-phases**, podle rizika od nejnižšího:

### Fáze 3A — Low-risk mechanical splits
- `Services/Dashboard/DashboardPriorityServices.cs` (892 LOC → 5 souborů po jedné odpovědnosti)
- `Services/Export/ExportTemplateQueries.cs` (1 281 LOC → 3 soubory — queries + builders + models)
- `Services/Export/OpenXmlWordExportService.cs` (1 113 LOC → partials podle sekcí dokumentu)
- `Views/Export/PdfTemplate.cshtml` (766 LOC → extrakce inline CSS + partial rozpad)

**Risk: Low.** Jeden consumer na soubor, žádné API změny, mechanické přesuny.

### Fáze 3B — JS module splits
- `recordEditor.js` (1 919 LOC) → 4 moduly (navigation, form, richtext, draft) + orchestrator
- `schedule.js` (1 697 LOC) → 4 moduly (filters, gantt, timeline, block) + planner
- `pickers.js` (1 530 LOC) → 4 moduly (date, time, person, adPerson) + barrel
- `filters.js` (1 105 LOC) → 3 moduly (projectFilter, recordDisplay, printFilter) + orchestrator
- `ui.js` (949 LOC) → 2 moduly (print, floating) + rainbow render

**Risk: Medium.** Import grafs se rozpadají — `bootstrap.js` musí aktualizovat namespacing. `site.bundle.js` se musí synchronizovat (nebo pattern barrel re-export). Playwright verify po každém splitu.

### Fáze 3C — Backend service decomposition
- `Services/RecordService.WriteCommands.cs` (1 707 LOC) → 3 partials (SaveRecord, DeleteRecord, MeetingIdentifier) s extrahovanými private helpers
- `Services/RecordProposalService.cs` (1 142 LOC → partial class pattern — Queries, SubmitCommands, DecisionCommands)
- `Services/Data/HarmonogramService.cs` (858 LOC) → split scheule domain od dictionary catalog concern (interface + implementation split)

**Risk: Medium.** Konzumenti (Controllers + DI registrace) se musí aktualizovat. Testy pokrývající tyto services musíme verifikovat.

### Fáze 3D — Controller + ViewModels split
- `Controllers/ProjektyController.cs` (882 LOC, 31 akcí) → 3 controllery podle feature-area (Projekty, ProjektMeetings, ProjektTeam)
- `Models/ViewModels/ProjektyViewModels.cs` (677 LOC) → 4 soubory (List, Detail, Tab, Record)
- `Models/ViewModels/CommandViewModels.cs` (532 LOC) → 3 soubory podle domény (RecordCommands, MeetingCommands, TeamCommands)

**Risk: Medium-High.** Route změny pro controllery vyžadují Views aktualizaci (`asp-controller=` hodnoty v Razor templates) + možné URL regrese. E2E smoke nutný.

### Fáze 3E — CSS architectural cleanup
- Extrakce `pdf-export.css`, `schedule.css`, `project-dashboard.css`, `gov-overrides.css` z monolith `site.css` (6 016 → ~3 500 LOC)
- Consolidate scattered gov-component overrides
- Identifikace duplicate rules mezi schedule-modal + schedule-tab blocks

**Risk: Low-Medium.** CSS porušení = vizuální regrese. Playwright screenshot verifikace před/po klíčových views.

## Tech stack invariants

- ASP.NET Core 8 MVC Razor
- EF Core 8 (SQL Server)
- gov-design-system 4.2.9 Web Components + custom `pm-*` TagHelpers
- xUnit + FluentAssertions pro unit
- Playwright pro E2E + harness
- Partial class pattern zavedený v `ProjectService.*` / `MeetingService.*` / `PeopleService.*` / `DictionaryService.*`

## File structure targets (po Fázi 3)

```
PmTracker.Web/
├── Controllers/
│   ├── ProjektyController.cs            (~200 LOC, projects CRUD + tabs)
│   ├── ProjektMeetingsController.cs     (~200 LOC, meeting operations) ★ nový
│   ├── ProjektTeamController.cs         (~300 LOC, team + roles + subsystems) ★ nový
│   └── ... (stávající controllery < 400 LOC)
│
├── Services/
│   ├── Dashboard/
│   │   ├── DashboardPriorityModels.cs              ★
│   │   ├── DashboardPriorityScoringService.cs      ★
│   │   ├── DashboardPriorityQuery.cs               ★
│   │   ├── PriorityMatrixRebuildService.cs         ★
│   │   ├── PriorityMatrixHostedServices.cs         ★
│   │   └── DashboardService.cs
│   │
│   ├── Export/
│   │   ├── ExportTemplateQueries.cs                (orchestration only)
│   │   ├── ExportProjectionBuilders.cs             ★
│   │   ├── ExportProjectionModels.cs               ★
│   │   ├── OpenXmlWordExportService.cs             (core)
│   │   ├── OpenXmlWordExportService.Records.cs     ★ partial
│   │   ├── OpenXmlWordExportService.Metadata.cs    ★ partial
│   │   └── OpenXmlWordElements.cs                  ★
│   │
│   ├── Data/
│   │   ├── HarmonogramService.cs                   (schedule schema + compute only)
│   │   └── HarmonogramCatalogService.cs            ★ (dictionary catalog concern)
│   │
│   ├── RecordService.cs                            (root partial)
│   ├── RecordService.SaveRecord.cs                 ★
│   ├── RecordService.DeleteRecord.cs               ★
│   ├── RecordService.MeetingIdentifier.cs          ★
│   │
│   ├── RecordProposalService.cs                    (root partial)
│   ├── RecordProposalService.Queries.cs            ★
│   ├── RecordProposalService.SubmitCommands.cs     ★
│   ├── RecordProposalService.DecisionCommands.cs   ★
│   │
│   └── ... (ostatní services beze změny)
│
├── Models/ViewModels/
│   ├── ProjektListViewModels.cs                    ★
│   ├── ProjektDetailViewModels.cs                  ★
│   ├── ProjektTabViewModels.cs                     ★
│   ├── ProjektRecordViewModels.cs                  ★
│   ├── RecordCommandViewModels.cs                  ★
│   ├── MeetingCommandViewModels.cs                 ★
│   └── TeamCommandViewModels.cs                    ★
│
└── wwwroot/
    ├── css/
    │   ├── site.css                                (~3 500 LOC, global reset + gov overrides + shared components)
    │   ├── pdf-export.css                          ★
    │   ├── schedule.css                            ★
    │   └── project-dashboard.css                   ★
    │
    └── js/modules/
        ├── recordEditor.js                         (~150 LOC orchestrator)
        ├── recordEditor.navigation.js              ★
        ├── recordEditor.form.js                    ★
        ├── recordEditor.richtext.js                ★
        ├── recordEditor.draft.js                   ★
        │
        ├── schedule.js                             (~150 LOC orchestrator)
        ├── schedule.filters.js                     ★
        ├── schedule.gantt.js                       ★
        ├── schedule.timeline.js                    ★
        ├── schedule.block.js                       ★
        │
        ├── pickers.js                              (~10 LOC barrel)
        ├── pickers.date.js                         ★
        ├── pickers.time.js                         ★
        ├── pickers.person.js                       ★
        ├── pickers.adPerson.js                     ★
        │
        ├── filters.js                              (~200 LOC orchestrator)
        ├── filters.projectFilter.js                ★
        ├── filters.recordDisplay.js                ★
        ├── filters.printFilter.js                  ★
        │
        ├── ui.js                                   (~100 LOC + rainbow)
        ├── ui.print.js                             ★
        └── ui.floating.js                          ★
```

★ = nové soubory v Fázi 3. Celkem cca 35-40 nových souborů, všechny < 500 LOC.

## Sequencing

1. **Fáze 3A první** (DashboardPriority, ExportTemplate, OpenXmlWord, PdfTemplate) — zkouška splitovací disciplíny na low-risk materiálu. Ověření, že testy chytí problémy.
2. **Fáze 3B druhá** (JS moduly) — zavedení bundle-sync disciplíny. `recordEditor.js` první, protože má nejvyšší dopad.
3. **Fáze 3C třetí** (backend services) — po zkušenostech z 3A. Konzumenti stále stejní (controllers).
4. **Fáze 3D čtvrtá** (controllery + VMs) — vyšší risk kvůli routing a Razor asp-controller změnám.
5. **Fáze 3E poslední** (CSS) — po JS stabilizaci. Playwright before/after screenshots.

Každá sub-phase má vlastní implementační plán v `docs/superpowers/plans/2026-04-20-faze-3<X>-*.md`.

## Testing strategy

- Per-commit: `dotnet build` + `dotnet test PmTracker.Tests.Unit` vždy zelené
- Per sub-phase end: Playwright harness verify (rozšířit z Fáze 2E)
- Per sub-phase end: manual review check (user schválení / komentáře)
- Rollback: každý split je 1 commit per file / per logical boundary → easy `git revert` pokud něco zhavaruje

## Exit criteria pro Fázi 3

- ☐ Žádný source file > 800 LOC v `PmTracker.Web/Services/` (kromě EF generovaných)
- ☐ Žádný JS module > 500 LOC v `PmTracker.Web/wwwroot/js/modules/`
- ☐ `ProjektyController` rozdělen podle feature-area
- ☐ `site.css` < 4 000 LOC, feature styly v samostatných souborech
- ☐ Nový developer může identifikovat vhodný soubor podle názvu bez grepu
- ☐ Všechny unit testy zelené (~346+)
- ☐ Všechny Playwright harness scénáře zelené
- ☐ Worklog + updated architecture docs

## Open questions pro uživatele

Tyto nejsou blockery, ale chtěl bych potvrdit v ranním review:

1. **`pm-dialog` wrapper (Fáze 2C)**: používá se jen ve StyleGuide? Jestli ano, v Fázi 3E cleanup odstranit.
2. **CSS extraction via `@import` vs `<link>` tags?** Preferuji `<link>` pro jasné HTTP caching separation. Confirm.
3. **`ProjektyController` split — zachovat `Projekty` route prefix pro všechny 3 controllery?** (`/Projekty/SaveTeamMember` beze změny) nebo změnit na `/ProjektyTeam/Save...`? Preferuji zachování routes = zero URL regression.
4. **`ScheduleBlockRenderer` (850 LOC JS class)** — split dovnitř (method groups jako separate files via `export` from within class)? Nebo zachovat class + jen noty v komentáři? Preferuji druhé — class je cohesive unit.
5. **CSS variables konvence** — pokračujeme s `--pm-*` a `--gov-*` prefixy, konsolidace variant (Fáze 1 rollout není kompletní) — ano / separátní phase?

## Další kroky

Po schválení této specifikace:

1. Implementační plán pro **Fázi 3A** (`docs/superpowers/plans/2026-04-20-faze-3a-mechanical-splits.md`) — 4-5 tasků, ihned executable
2. Exekuce 3A přes `subagent-driven-development` (Sonnet agents)
3. Post-3A review + případná úprava plánu 3B
4. Pokračování 3B → 3C → 3D → 3E

Autonomní pokračování: vzhledem k jasnému scope (audit + mechanical splits + partial patterns), začínám Fázi 3A Task 1 (DashboardPriority split — lowest-risk) paralelně s review této specifikace uživatelem. Task 1 je plně reverzibilní přes `git revert`.
