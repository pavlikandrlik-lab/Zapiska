# Harmonogram — oprava osy — Implementační analýza (dopady, rizika, problémy)

**Datum:** 2026-06-23
**Doprovodný dokument k:** `docs/superpowers/plans/2026-06-23-harmonogram-osa-redesign.md` a specu `…/specs/2026-06-23-harmonogram-osa-redesign-design.md`
**Účel:** domyslet implementaci, zmapovat blast-radius, vytáhnout konkrétní implementační problémy a z nich odvodit **závazné úpravy task-planu** dřív, než se začne kódit.

---

## 1. Blast-radius (co všechno se dotkne)

### 1.1 C# — změna recordu `ScheduleBarLayout`
Měníme positional record (přidání `MonthTicks`, nullable `TodayPct`/`DeadlinePct`). **Každý call-site a čtenář se musí upravit:**

| Místo | Co | Dopad |
|---|---|---|
| `ScheduleBarLayoutCalculator.cs:13-19` | definice recordu + `Compute` | přepis (Task 1) |
| `HarmonogramDateBlokBuilder.cs:80` | jediný `Compute(...)` caller | beze změny signatury (parametry stejné) |
| `ProjectService.ScheduleBlockComposition.cs:8,20,32` | `BuildScheduleBlockViewModel` nese `OverviewLayout` + nově `Today` | +param `today` |
| `ProjectService.ScheduleComposition.cs:87` | caller #1 (statická karta) | předat `todayDate` (lokální) |
| `ProjectService.RecordEditorComposition.cs:267` | caller #2 (editor) | předat `todayDate` (lokální) |
| `_ScheduleBlock.cshtml:102` | overview today marker `Pct(ovl.TodayPct)` | nullable → podmínit (Task 3) |
| `_ScheduleBlock.cshtml:107` | overview deadline marker | nullable → podmínit (Task 3) |
| **`_ScheduleBlock.cshtml:241`** | **breakdown (rozpad) today marker** `Pct(ovl.TodayPct)` | **nullable → podmínit — V PLÁNU CHYBĚLO** |
| `_ScheduleBlock.cshtml:161` | `data-axis-end ?? TerminUkonceni` | po Task 5 nahradit tickami (níže) |
| `ScheduleBarLayoutCalculatorTests.cs:37,38,83` | aserce `TodayPct`/`DeadlinePct` jako non-null `double` | nullable → `.Value` / `NotBeNull` (Task 1) |
| `HarmonogramDateBlokBuilderBarLayoutTests.cs` | osa = max(...) bez měsíčního přichycení | aserce os se mění (Task 1/2) |
| `ProjectHarmonogramRenderTests.cs` (Api) | render HTML | rozšířit o ticky/today (Task 3) |
| `HarmonogramUnifiedScenariosTests` (E2E) | scénáře nad osou | ověřit, případně upravit očekávání |

### 1.2 JS — sdílený seam `renderTimelineAxis` má **tři** konzumenty
| Konzument | Volání | Co s tím |
|---|---|---|
| `timeline.js:50,284` (statická osa) | `renderTimelineAxis(c, start, end)` | přepnout na ticky ze serveru |
| `block.js:318` (`setAxisRange`, editor) | `renderTimelineAxis(axis, start, end)` | přepnout na ticky z `computeAxisLayout` |
| **`gantt.js:258` (`updateProjectGanttAxis`)** | `renderTimelineAxis(axis, ordered[0], ordered[last])` | **gantt nemá serverový layout — musí si nechat vzorkovací cestu** |

### 1.3 JS — ostatní dotčené
- `index.js` (re-export `* from gantt.js`, `initProjectScheduleUi`), `filters.js` (volá `renderStaticTimelineAxes`).
- `bootstrap.js` — `scheduleAxis.js` je **pure modul** (bez side-efektů), importuje ho jen `block.js`; do bootstrapu se **nepřidává**.

---

## 2. Konkrétní implementační problémy (a jak je vyřešit)

### P1 — `gantt.js` sdílí `renderTimelineAxis`; nesmíme smazat `buildTimelineAxisTicks`
**Důkaz:** `gantt.js:258` volá `renderTimelineAxis(axis, start, end)` a je živý (`bootstrap.js:84` importuje `applyProjectGanttFilters`). Navíc `ScheduleJsSplitTests:73-74` **vyžaduje**, aby `timeline.js` obsahoval `buildTimelineAxisTicks` i `renderTimelineAxis`.
**Důsledek:** Task 9 v původním plánu („odstranit `buildTimelineAxisTicks`/`resolveTimelineAxisTickTargetCount`") **rozbije gantt board i architektonický test**.
**Řešení (závazná úprava plánu):**
- **NEodstraňovat** `buildTimelineAxisTicks` ani `renderTimelineAxis(axis,start,end)`. Gantt board zůstává na vzorkovací cestě (gantt nemá měsíční serverový layout — je stavěný klientsky z `[data-gantt-item]` atributů; jeho migrace na měsíční mřížku je **mimo rozsah**).
- **Přidat** novou funkci `renderTicksFromList(container, ticks, options)` v `timeline.js`, kterou používá statická karta (server ticky) i editor (`computeAxisLayout` ticky). Stará funkce zůstává pro gantt.
- Edge-inset (2–4 px) odstranit **jen v nové cestě** (`renderTicksFromList`); gantt necháváme beze změny (jeho zarovnání s pruhy neřešíme).
- Task 9 se mění z „smazat" na „přidat novou cestu + ResizeObserver pro statiku/editor; gantt beze změny".

### P2 — Breakdown (rozpad) markery a segmenty taky závisí na `ovl`
**Důkaz:** `_ScheduleBlock.cshtml:241` (breakdown today marker) i `:211` (`ScheduleBarSegment? bseg`) čtou `ovl`.
**Důsledek:** nullable `TodayPct` rozbije breakdown render; Task 3 řešil jen overview.
**Řešení:** Task 3 rozšířit — breakdown today marker stejně podmínit `ovl.TodayPct.HasValue` a přesunout do overlay vrstvy (`.schedule-layered-marker` má vlastní styl — zkontrolovat `:239-242`). Breakdown osa statická = tytéž ticky jako overview (rozpad sdílí osu — soulad se specem §4.4).

### P3 — JS testy a fixtures NEsmí ležet ve `wwwroot`
**Důkaz:** vše ve `wwwroot/` se publikuje (a servíruje); `node:test` import by v prohlížeči spadl, kdyby se soubor omylem zabundloval; navíc bloat v `publish.zip`.
**Řešení (úprava plánu):** JS testy + fixtures dát **mimo `wwwroot`**, např.:
- `tests/js/schedule/scheduleAxis.test.js`, `…/timeline.test.js`, `…/blockAxis.test.js`
- `tests/js/schedule/__fixtures__/axis-cases.json`

ESM import v testu cílí na modul přes relativní cestu (`../../../PmTracker.Web/wwwroot/js/modules/schedule/scheduleAxis.js`). Pozn.: `scheduleAxis.js` importuje `../utils.js` **relativně vůči sobě**, takže se vyřeší správně bez ohledu na umístění testu. `package.json` `test` skript: `node --test tests/js/schedule/`. C# golden test čte fixtures z `tests/js/schedule/__fixtures__/axis-cases.json`.

### P4 — Zaokrouhlení a serializace (parita C# ↔ JS ↔ HTML)
- C# `Pct` v Razoru formátuje `0.####` (4 desetinná). JS `round4`. Server **`MonthTicks` se serializují `System.Text.Json`em v plné `double` přesnosti** do `data-schedule-ticks` → JS je vykreslí, ale C# golden test a JS parity test musí porovnávat se **stejnou tolerancí** (`0.01`).
- **Řešení:** golden fixtures drží pct na 4 desetinných; oba testy s tolerancí `< 0.01`. Inline `left%` v markupu i ticks JSON nechat na plné přesnosti (CSS si poradí); vizuální rozdíl < 1 px.

### P5 — `ResizeObserver` lifecycle (náhrada rAF-retry)
- Více karet = více stop = více observerů; je třeba je **odpojit** při re-renderu tabu/filtru, jinak leak a duplicitní překreslení.
- Editor: `setAxisRange` se volá při každém `recalcAll` (každý úhoz) — observer nesmí re-trigger smyčku (pozor na rekurzi observer→render→layout→observer).
- **Řešení:** jeden observer na track, `unobserve`/`disconnect` při teardownu; render osy idempotentní (žádná změna velikosti stopy renderem popisků → bez smyčky). Pro statiku stačí observe při `initProjectScheduleUi`; pro editor držet observer mimo `recalcAll` (osa se překreslí z `monthTicks`, ne z měření).

### P6 — Editor: `today` z atributu vs live editace
- Editor čte `start`/`deadline` z **inputů** (live), ale `today` z `data-schedule-today` (fixní z page-loadu). To je správně — „dnes" se během editace nemění. Jen ověřit, že `data-schedule-today` je na **kořeni editor bloku** (je — sdílená partial, Task 8 Step 5).

### P7 — `data-axis-end ?? TerminUkonceni` fallback (cshtml:161)
- Po Task 5 statická osa nečte `data-axis-start/end`, ale `data-schedule-ticks`. Starý axis `<div data-timeline-axis data-axis-start/end>` se buď přepíše na nositele `data-schedule-ticks`, nebo se ticky dají na existující axis element. **Sjednotit, ať nezůstanou dvě cesty.** Fallback `?? TerminUkonceni` zmizí (ticky chodí ze serveru vždy, když je `OverviewLayout`).

### P8 — Architektura testy k aktualizaci
- `ScheduleJsSplitTests`: přidat `scheduleAxis.js` do `Submodule_ShouldExistAndNotBeEmpty` + assert `computeAxisLayout`. **Neodstraňovat** assert na `buildTimelineAxisTicks` (P1).
- `JsBundleImportConsistencyTests`: cílené (ne glob) — `scheduleAxis.js` import v `block.js` musí být konzistentní; přidat guard, že `block.js` importuje `computeAxisLayout` (volitelné).

### P9 — Dark mode + překryv overlay
- `:root[data-theme="dark"] .schedule-overview-marker.*` (site.css ~6649) — overlay markery musí dědit tytéž barvy. Overlay `pointer-events:none` (ať neblokuje tooltipy segmentů). z-index nad segmenty, pod interaktivními prvky.

### P10 — Existující testy, které spadnou „správně"
- `ScheduleBarLayoutCalculatorTests` (axisEnd se mění na měsíční), `HarmonogramDateBlokBuilderBarLayoutTests`, Api `ProjectHarmonogramRenderTests`, E2E `HarmonogramUnifiedScenariosTests`. Každý projít a vědomě upravit očekávání (ne „ohnout" kód k starým testům).
- Pozn. `project_pre_existing_test_failures` (paměť): 8 Unit/7 Api/5 Integration už dnes padá z jiných důvodů — odlišit od našich.

---

## 3. Závazné úpravy task-planu (z této analýzy)

1. **Task 3** rozšířit: podmínit a do overlay přesunout i **breakdown** today marker (`_ScheduleBlock.cshtml:241`), ne jen overview. (P2)
2. **Task 5/9** přepsat: **nepřidávat** „odstranění `buildTimelineAxisTicks`". Místo toho **přidat `renderTicksFromList`**; `buildTimelineAxisTicks` + `renderTimelineAxis(axis,start,end)` **zůstávají** pro `gantt.js`. ResizeObserver jen pro novou cestu. (P1, P5)
3. **Task 6/7/8** přesunout JS testy + fixtures **mimo `wwwroot`** (`tests/js/schedule/…`); upravit `package.json` test glob a cesty v C# golden testu. (P3)
4. **Task 9** přejmenovat z „úklid/smazání" na „ResizeObserver + sjednocení axis nositele (`data-schedule-ticks`)"; `ScheduleJsSplitTests` upravit jen aditivně (přidat `scheduleAxis.js`), assert na `buildTimelineAxisTicks` **ponechat**. (P1, P7, P8)
5. **Task 3** Step 3 také odstranit/sloučit `data-axis-end ?? TerminUkonceni` cestu, ať existuje jediný nositel ticků. (P7)

---

## 4. Riziková matice

| # | Riziko | Pravděpodobnost | Dopad | Mitigace |
|---|---|---|---|---|
| P1 | Rozbití gantt boardu / arch. testu smazáním sdílené funkce | **Vysoká** (bez úpravy) | Vysoký | Ponechat `buildTimelineAxisTicks`, přidat `renderTicksFromList` |
| P2 | Breakdown render spadne na nullable `TodayPct` | Vysoká | Střední | Podmínit i breakdown marker (Task 3+) |
| P3 | Test soubory ve `wwwroot` se publikují/servírují | Jistá | Nízký–střední | Testy mimo `wwwroot` |
| P4 | Sub-pixel rozdíl C#/JS pozic | Střední | Nízký | Tolerance 0.01 v testech, 4 desetinná |
| P5 | ResizeObserver smyčka/leak | Střední | Střední | Idempotentní render, disconnect při teardownu |
| P10 | Falešně „rozbité" existující testy | Jistá | Nízký | Vědomě přepsat očekávání |
| — | E2E `HarmonogramUnifiedScenarios` citlivé na osu | Střední | Střední | Spustit brzy, upravit asserce |

---

## 5. Doporučené pořadí a kontrolní body

1. **Nejdřív C# jádro + golden fixtures** (Task 1 → Task 6 C# část): osa je matematicky uzamčená dřív, než se na ni napojí UI/JS.
2. **Pak statická karta** (Task 2,3,4,5) — viditelný výsledek, ověřitelný Api testem a okem.
3. **Pak JS dvojče + parita** (Task 7) — `scheduleAxis.js` proti týmž fixtures.
4. **Pak editor** (Task 8) — až je dvojče zelené.
5. **Úklid + ResizeObserver + arch. testy** (Task 9), **gantt se nedotýká**.
6. **Verifikace** (Task 10): full C# sady + `node --test` + ruční vizuál + E2E.

**Kontrolní bod po kroku 1:** C# golden test zelený = osa je „pravda". **Kontrolní bod po kroku 3:** JS parity zelená = editor bude konzistentní ještě před napojením.

---

## 6. Co zůstává mimo rozsah (vědomě)
- **Gantt board** (`[data-gantt-item]`) — vlastní vzorkovaná osa zůstává; migrace na měsíční mřížku je samostatné téma.
- Sjednocení „dnes" v `PriorityMatrix`/`Dashboard` (jiný `today` typ) — `ScheduleDateCalculator` neměníme.
- Datumy u kroků v rozpadu (vizuální doladění po nasazení).
- Společná kalendářní osa napříč kartami.
