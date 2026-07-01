# Harmonogram — oprava osy a zarovnání pruhů (plán/skutečnost) + ukazatel „Dnes"

**Datum:** 2026-06-23
**Stav:** Návrh k odsouhlasení
**Souvislost:** navazuje na analýzu chování os v konverzaci (projektová záložka Harmonogram). Tento dokument je závazná specifikace opravy, ze které se následně píše implementační plán.

---

## 1. Cíl

Pruhy **Plán** a **Skutečnost** a ukazatel **Dnes** na projektové záložce Harmonogram musí graficky odpovídat kalendáři — datum kroku má sedět na správném místě vůči popiskům měsíců. Dnes tomu tak není (např. 20.3 padne do první třetiny března).

Sekundárně: odstranit strukturální dluh, kvůli kterému se zobrazení historicky opakovaně rozjíždělo — **čtyři nezávislé výpočty osy** a **dva zdroje „dnes"** (UTC vs lokální čas).

## 2. Co je dnes špatně (shrnutí analýzy)

1. **Hlavní vizuální chyba:** popisky osy nejsou kalendářní hranice měsíců, ale **rovnoměrně rozmístěné vzorky** olabelované měsícem (`buildTimelineAxisTicks` v `timeline.js`). Popisek „březen" tak nesedí na 1.3; pruh končící 20.3 je sice lineárně správně, ale vůči zavádějícímu popisku vypadá posunutě.
2. **Čtyři různé definice osy** pro tatáž data:
   - Overview (statická karta) — server `ScheduleBarLayoutCalculator`: `axisEnd = max(termín, dnes, VŠECHNY konce plánu, VŠECHNY konce skutečnosti)`; „dnes" = **UTC**.
   - Overview (editor live) — JS `buildScheduleScale`: `axisEnd = max(termín, POSLEDNÍ konec skutečnosti, dnes)`; „dnes" = **lokální**. Ignoruje konce plánu za termínem.
   - Rozpad (editor i karta) — JS `resolveBreakdownAxis`: `axisStart = nejstarší start`, `axisEnd = max(nejzazší konec, termín)`, **bez „dnes"**.
   - Popisky osy — JS `renderTimelineAxis` nad libovolně předaným rozsahem.
   → editor ≠ uložený stav; overview ≠ rozpad ve stejné kartě.
3. **„Dnes" má dva zdroje:** server `GetUtcNow().UtcDateTime.Date` (UTC) vs JS `new Date()` (lokální). V noci o den vedle; mezi editorem a kartou se marker posune.
4. **Souřadnicové nesoulady i po opravě měsíců:** popisky osy mají edge-inset 2–4 px, pruhy ne; segmenty mají `width:calc(% + 1px)`; marker je bez `translateX(-50%)` a kraj se ořezává `overflow:hidden`.
5. **Žádné JS testy.** Buggy část (`timeline.js`, `block.js`) není pokrytá; opravy serveru ji nikdy nechránily → odtud „historicky se to sralo".

## 3. Kompatibilita s editorem harmonogramu (výsledek prověření)

**Architektura dnes:**
- Statická karta (projektová záložka) — pozice pruhů/markerů počítá **server** (`ScheduleBarLayoutCalculator`), `block.js` se pro ni **hned ukončí** (`index.js` early-return pro ne-editor). Popisky osy kreslí `timeline.js`.
- Editor — `block.js` (`ScheduleBlockRenderer`) počítá **čistě v prohlížeči** na každý úhoz (žádný server round-trip; endpoint `/Schedule/Recalc` byl v Fázi 3b zrušen — v `Controllers/` žádný `ScheduleController` není). Je to **ručně psaný mirror** serverového `ScheduleDateCalculator` + `ScheduleBarLayoutCalculator`.

**Závěr:**
- **Není žádné sdílené programové jádro** mezi serverem (C#) a editorem (JS) — jsou to dvě dvojče-implementace téhož algoritmu.
- **Jediná opravdu sdílená komponenta** je `timeline.js` (vykreslení popisků osy) — používá ji statická karta i editor. Změna `timeline.js` tedy ovlivní obě cesty a musí se ověřit v obou.
- Oprava jen statické karty (Varianta A) **nepoteče automaticky do editoru** — ten si ponechá vlastní `buildScheduleScale`/`resolveBreakdownAxis` a začne se od (opravené) karty lišit ještě víc.

**Důsledek pro rozsah:** spec pokrývá **obě** cesty, ale jako **dvě fáze se sdíleným kanonickým algoritmem a parity testy**. Fáze 2 (editor) je oddělitelná a může se udělat následně, aniž by blokovala Fázi 1.

## 4. Návrh řešení

### 4.1 Kanonický algoritmus osy (jeden zdroj pravdy)

Definujeme **jeden** algoritmus „layout osy", implementovaný dvojmo (C# autoritativní + JS dvojče) a zamčený golden-vector parity testy. Vstupy: `start` (DatumZalozeni), `deadline` (TerminUkonceni), `today` (lokální pražské datum), `steps[]` s `planEnd` a `actualEnd` (u aktuálního běžícího kroku `actualEnd = today`, jak už počítá `ScheduleDateCalculator`).

**Rozsah osy (přichycení na měsíce):**
- `contentEnd = max(deadline, max(planEnd), max(actualEnd))` — u běžícího úkolu po termínu je `today` zahrnut přirozeně přes `actualEnd` aktuálního kroku.
- `axisStart = první den měsíce(start)`.
- `axisEnd = poslední den měsíce(contentEnd)`; **edge case**: pokud `contentEnd == poslední den svého měsíce`, pak `axisEnd = poslední den měsíce(contentEnd + 1 den)` — tj. osa se protáhne o celý další měsíc, aby rozhodující datum (typicky „dnes") nikdy nesedělo na pravém kraji a nevypadalo to, že něco chybí.
- `totalDays = max(1, (axisEnd − axisStart) ve dnech)`.

**Měsíční ticky (kalendářní mřížka):**
- Tick na **1. dne každého měsíce** v intervalu `[axisStart, axisEnd]` (axisStart je 1. svého měsíce → první tick na 0 %).
- `left% = (tickDatum − axisStart) / totalDays × 100`.
- Popisek `MM/RRRR` (měsíc + rok; přesný den nepodstatný — manažerský přehled).
- **Prořídnutí popisků:** gridlines zůstávají vždy; když se popisky nevejdou (úzká karta), překrývající se skryjí, první a poslední vždy viditelné.

**Ukazatel „Dnes":**
- `todayPct = (today − axisStart) / totalDays × 100`, pouze pokud `axisStart ≤ today ≤ axisEnd`; jinak se marker **nevykreslí** (úkol v budoucnu nebo už proběhlý).

**Ukazatel „Termín":** stejně jako dnes — `deadlinePct` pokud spadá do intervalu, jinak skrytý.

**Pozice pruhů (segmentů):** `left% = (segStart − axisStart)/totalDays×100`, `width% = (segEnd − segStart)/totalDays×100`, nad **stejným** `[axisStart, axisEnd]`.

**„Dnes" = lokální pražské datum** všude (server i JS). Na serveru přes `TimeProvider.GetLocalNow()`; do JS se předá stejné datum jako `data-schedule-today` atribut, aby se UTC/local nikdy nerozešly (JS si nebere `new Date()` nezávisle).

### 4.2 Sjednocení souřadnic

Pruhy, markery i ticky musí používat **identické** mapování `0 % = levý kraj stopy`, `100 % = pravý kraj`:
- Zrušit edge-inset v ose (`timeline.js`) — ticky kreslit přes plnou šířku stopy jako pruhy.
- Zrušit `width:calc(% + 1px)` u segmentů; případné hairline mezery řešit konzistentním zaokrouhlením / vizuálně (box-shadow), ne nafouknutím šířky (které posouvá pravý okraj).
- Markeru přidat `transform: translateX(-50%)` a vyřešit ořez na krajích — markery přesunout do **nepřekrývané overlay vrstvy** nad stopou (segmenty si ponechají pill-clipping přes `overflow:hidden`/border-radius, markery už nebudou clipnuté).

### 4.3 Kde algoritmus žije (Varianta A)

- **C# (autoritativní):** nový čistý kalkulátor `ScheduleAxisLayout` (rozšíření/náhrada `ScheduleBarLayoutCalculator`) vracející `AxisStart, AxisEnd, TotalDays, TodayPct?, DeadlinePct?, MonthTicks[] (left%, label), Segments[]`. Statická karta jej použije a do markupu vyrenderuje inline `left/width` segmentů + **seznam ticků** (pozice + popisek) jako data, takže JS osu **už nepočítá, jen vykreslí**.
- **JS dvojče (`scheduleAxis.js`):** čistý modul implementující týž algoritmus pro **editor live-preview**. `block.js` jej použije místo `buildScheduleScale` a `resolveBreakdownAxis` (ty se zruší).
- **`timeline.js`** se zredukuje na „vykresli ticky ze seznamu" (žádné `buildTimelineAxisTicks` vzorkování): statická cesta dostane seznam ze serveru, editor z `scheduleAxis.js`. Toto je sdílená render-komponenta pro obě cesty.

### 4.4 Potvrzená chování (rozhodnuto v brainstormingu)

- Osa per-karta (měsíce dané karty), ne sdílená napříč kartami.
- U úkolu po termínu, který běží: osa do konce měsíce „dneška" (resp. dle edge-case pravidla), **skluz/přesah viditelný** jako pruh skutečnosti za termínem.
- Rozpad („Rozpad") použije **tutéž osu** jako overview, aby seděly svisle.
- Datumy u kroků v rozpadu: zatím neřešeno; nechat jak jsou, doladí se vizuálně podle výsledku (mimo rozsah této opravy).

## 5. Komponenty a tok dat

**Fáze 1 — statická karta (server-authoritative):**
1. `ScheduleAxisLayout.Compute(start, deadline, today=GetLocalNow, steps)` → layout.
2. `HarmonogramDateBlokBuilder.BuildBarLayout` volá nový kalkulátor; VM nese ticky + segmenty + today/deadline pct.
3. `_ScheduleBlock.cshtml` renderuje inline pozice segmentů, overlay markery (translateX), a `data-schedule-ticks` (seznam) + `data-schedule-today`.
4. `timeline.js` (zjednodušený) vykreslí ticky ze seznamu; žádné vzorkování, žádný edge-inset.

**Fáze 2 — editor live-preview (JS dvojče):**
1. `scheduleAxis.js` = JS port `ScheduleAxisLayout` (identický algoritmus).
2. `block.js` `renderOverview`/`renderBreakdown` použijí `scheduleAxis.js`; `buildScheduleScale` a `resolveBreakdownAxis` smazány.
3. „Dnes" v editoru bere `data-schedule-today` (lokální datum z serveru), ne `new Date()`.
4. Overlay markery + sjednocené souřadnice stejně jako Fáze 1.

## 6. Strategie testů

- **C# unit** (`ScheduleAxisLayoutTests`): přichycení na měsíce, edge-case měsíčního konce (+1 měsíc), today uvnitř/vně intervalu (skrytí), pozice ticků = kalendářní 1. dne, pozice segmentů, DST přelom (konec března), úkol budoucí/hotový/po termínu.
- **Golden-vector fixtures** (JSON, sdílené): vstup → očekávaný layout. C# test je ověří; identické fixtures ověří i JS dvojče → **zamčená parita**.
- **JS testy (NOVÉ — infra dnes neexistuje):** zavést **minimální node runner** (`node:test`, bez těžkých závislostí) a otestovat `scheduleAxis.js` proti týmž golden-vector fixtures + render `timeline.js` (počet ticků, pozice, prořídnutí popisků). [rozhodnuto 2026-06-23]
- **Api render test** (`ProjectHarmonogramRenderTests`): rozšířit o kontrolu inline pozic a ticků ve vyrenderovaném HTML.
- **Regrese:** stávající `ScheduleDateCalculatorTests`, `ScheduleBarLayoutCalculatorTests`, `HarmonogramDateBlokBuilder*Tests`, E2E `HarmonogramUnifiedScenariosTests` musí zůstat zelené (nebo se vědomě upravit dle nového rozsahu osy).

## 7. Rizika a co nerozbít

- **Sdílený seam `timeline.js`** — změna ovlivní statickou kartu i editor; ověřit v obou (i ve Fázi 1).
- **Lazy tab / šířka 0** — záložka je lazy, panely se přepínají `display`; osa se kreslí z post-layout šířky. Zpevnit render (ideálně `ResizeObserver` na stopu místo rAF-retry hříčky), aby se popisky dorovnaly při zobrazení/filtru/rozbalení.
- **Chronologie plánu v editoru** (`applyPlanChronologyBounds`) a **rainbow segmenty** (`queueRainbowSegmentRender`) — nezávislé na ose, ale běží ve stejné `recalcAll`; nesmí se rozbít.
- **Dark mode** a responsivní breakpointy (88px label sloupec, šířka stopy).
- **Ostatní konzumenti `ScheduleDateCalculator`** (PriorityMatrix, Dashboard) — používají vlastní „today" (DateOnly/referenceDate). Tato oprava **nemění** `ScheduleDateCalculator`, jen vrstvu osy; sjednocení „dnes" napříč těmito konzumenty je mimo rozsah (poznamenat jako navazující).

## 8. Rozhodnutí (potvrzeno 2026-06-23)

1. **JS test infra:** zavést **minimální node runner** (`node:test`) a `scheduleAxis.js` ověřit proti týmž golden-vector fixtures jako C# → pravá parita, opakovatelné v CI.
2. **Fázování:** Fáze 1 (statická karta) i Fáze 2 (editor) se dělají **společně** — jeden kanonický algoritmus a parita hned, aby nevznikl stav editor≠uložené a algoritmus se nepsal dvakrát.

## 9. Mimo rozsah (navazující)

- Společná kalendářní osa napříč kartami (srovnatelnost úkolů mezi sebou) — samostatné téma.
- Datumy u kroků v rozpadu — doladit vizuálně po této opravě.
- Sjednocení zdroje „dnes" v PriorityMatrix/Dashboard.

## 10. Kritéria úspěchu

- Datum 20.3 sedí ve druhé třetině března vůči mřížce měsíce; obecně pozice pruhů odpovídá kalendáři.
- „Dnes" je na správném místě, nikdy ne uříznuté/na kraji; skryté u budoucího/hotového úkolu.
- Editor live-preview a uložená karta zobrazují **identickou** osu pro stejná data (parity test).
- Overview a rozpad ve stejné kartě sdílí měřítko.
- Nové C# i JS testy zelené; stávající sada zelená.
