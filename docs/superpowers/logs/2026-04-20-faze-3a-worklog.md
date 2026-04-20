# Fáze 3A worklog — Low-risk mechanical splits

**Datum dokončení:** 2026-04-20
**Branch:** `codex/senior-refactor-fase-1`

## Commits

| SHA | Task | Popis |
|---|---|---|
| `1653a04` | T1 | DashboardPriorityServices.cs (892 LOC) → 5 files |
| `147ecd3` | T1 follow-up | +9 architecture tests (namespace + dependency boundaries) |
| `bb3aa31` | T2 | ExportTemplateQueries.cs (1281 LOC, 2 namespaces) → 3 files flat namespace |
| `901c3cd` | T3 | OpenXmlWordExportService.cs (1113 LOC) → partial + helper (4 files) |
| `4015f2e` | T4 | PdfTemplate.cshtml (766 LOC) → orchestrator + 3 partials + pdf-export.css |

## Metriky

- **Unit testy:** 346 → 390 (+44 new architecture tests)
- **God-files eliminované:** 4 (Dashboard, Export queries, OpenXml Word, PdfTemplate)
- **Nové soubory:** 16 backend + Razor + CSS
- **LOC redistribution:** ~4 053 LOC reorganizovaných (892 + 1281 + 1113 + 767 = 4 053)
- **API-breaking changes:** 0
- **Consumer files změněné:** 3 v T2 (namespace flattening: `ExportCommentProjectionBuilderTests`, `ExportTemplateSummaryBuilderTests`, `DataStoreServiceCollectionExtensions`)

## Architektonické principy zaváděné

1. **File-scoped namespace** konzistentně napříč všemi novými C# soubory (`namespace X;`)
2. **Minimal using directives** per file — jen skutečně používané
3. **XML doc comment header** na každém novém souboru (1-2 věty purpose)
4. **Architecture tests pattern:**
   - Presence: soubor existuje a není prázdný
   - Namespace: file-scoped, správné jméno
   - Content distribution: správné typy ve správných souborech
   - Dependency boundaries: Models nesmí importovat EF, Scoring nesmí importovat Rebuild, atd.
5. **Partial class discipline** (T3): `partial class` keyword ve všech fragmentech; private fields/methods shared cross-partial uvnitř stejné assembly
6. **Razor partials over inline** (T4): extract repeating sections; narrow model types preferovány (`PdfExportRecordViewModel` pro record row)
7. **CSS extraction** (T4): inline `<style>` → samostatný soubor přes `<link rel="stylesheet">` (cacheable, separation of concerns)

## Testing discipline

- **TDD order:** architecture tests first (failing), extraction second, tests green = refactor done
- **Architecture tests remain permanent** — zajišťují, že budoucí změny neztratí strukturu
- **45 nových tests** v `PmTracker.Tests.Unit/Architecture/` folderu

## Pattern pro Fázi 3B-E (lessons learned)

- **Namespace flattening may be required** (T2 příklad): god-file mohl mít více namespaces; split vyžaduje unify + update importů všude
- **Extracted helpers with `@@` Razor escapes**: musí se odstrand. v CSS souboru (T4: `@@page` → `@page`)
- **Minimum LOC threshold v testu** (500 chars) je pragmatic — zabraňuje naplnění prázdným souborem
- **Document private nested types** (T3: `HtmlInlineToken` atd.) — zůstávají v core kde byly, ne v partials
- **Status constants placement** (T1 m-2): internal implementation details mají být u implementace, ne v public-facing Models. Sledovat v T2/T3 pro konzistenci

## Risks addressed / open

**Addressed:**
- No visibility widening (verifikováno v T1 review)
- Partial class cross-file shared state (T3)
- Razor partial model binding (T4 used specific `PdfExportRecordViewModel` where possible)

**Open:**
- `pdf-export.css` runtime smoke test deferred — vyžaduje live SQL (Export controller requires data). Documented in manual smoke log.
- JS module split (Fáze 3B) bundle sync discipline needs to be formalized — stávající ruční disciplína funguje, ale je náchylná k chybám. Zvážit build script v 3E.

## Next — Fáze 3B

Plán: `docs/superpowers/plans/2026-04-20-faze-3b-js-moduly.md`

5 tasků (recordEditor, schedule, pickers, filters, ui) → feature-bounded submodule folders + barrel pattern pro backward-compat. Odhad ~35-50 nových architecture tests, 0 API change (backward-compat barrel re-export).
