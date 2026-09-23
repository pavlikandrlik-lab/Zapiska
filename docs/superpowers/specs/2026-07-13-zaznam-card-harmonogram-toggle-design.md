# Karta záznamu: toggle Harmonogram + přeskládání akcí — design

**Datum:** 2026-07-13
**Stav:** návrh ke schválení
**Navazuje:** cross-tab nav (crossTabNav.js), lazy-load karty (recordLazyLoading.js), sdílený harmonogram blok (_ScheduleBlock.cshtml)

## 1. Cíl

V záložce **Záznamy** umožnit zobrazit harmonogram záznamu přímo na jeho kartě — toggle switch přepne obsah rozbalené karty mezi dnešním detailem (informace + vyjádření) a kompletním harmonogramem (pruhy Plán/Skutečnost, souhrn, štítek Stíháme/Nestíháme, rozbalený rozpad kroků). Současně se přeskládají akční tlačítka hlavičky karty: zůstanou maximálně 3 (tisk, upravit, šipka menu) + toggle switch ve spodní řadě; méně časté akce se přesunou do svislého rozbalovacího menu.

**Záložka Harmonogram se nemění** — vzhled i překlik „Zobrazit záznam" zůstávají, jak jsou.

## 2. Rozhodnutí uživatele (zaznamenáno z diskuse)

| # | Rozhodnutí |
|---|---|
| R1 | Realizují se obě varianty: **1) toggle na kartě (teď)**, 2) samostatná stránka záznamu (později, viz §9). |
| R2 | Po přepnutí na harmonogram se ukáže **rovnou všechno** — pruhy, souhrn i kompletní rozpad kroků s datumy (žádné další klikání). Navíc štítek Stíháme/Nestíháme. |
| R3 | Harmonogram nahrazuje **informace záznamu i vyjádření** — z původního obsahu zůstává jen hlavička (název, kategorie, stav). |
| R4 | Horní řada akcí: **tisk záznamu, upravit záznam, šipka doprava** (dle oprávnění, max 3). Šipka otevírá svislé menu; po rozbalení se změní na šipku dolů, dalším klikem se menu sbalí a šipka vrátí. |
| R5 | Menu obsahuje: **Zobrazit na záložce Harmonogram** (dnešní ikona kalendáře), **Navrhnout změnu harmonogramu** (dnešní kalendář-plus), a později **Otevřít na nové kartě** (přidá se až s variantou 2 — do té doby položka neexistuje). |
| R6 | Spodní řada: **toggle switch „Harmonogram"** — jen u záznamů, které harmonogram mají (predikát `MaHarmonogramHodnotu`, stejně jako dnešní ikona kalendáře). |
| R7 | Stávající indikátor rozbalení karty (›) se přesune **vlevo mezi kraj karty a kategorii záznamu**; nic jiného se neposouvá, jen se vloží mezi. |
| R8 | Šipka menu vpravo je **klasické ohraničené tlačítko** (čtvereček) jako ostatní akce vpravo. |
| R9 | Chování přepínače: přepnutí sbalené karty ji rozbalí; sbalení karty stav přepínače nemění; poloha přežije obnovu karty; u záznamů bez harmonogramu přepínač není. |

## 3. UI — hlavička karty (`_ZaznamPartial.cshtml`)

### 3.1 Levá strana

- Nový `<span class="record-expand-indicator">›</span>` jako **první prvek** `.record-header`, mezi levým barevným proužkem (`record-bar`) a blokem `record-title-wrap` (kategorie pill je jeho první řádek). Vizuálně nenápadný znak bez rámečku (dnešní vzhled `record-toggle-indicator`), rotace při rozbalení zůstává.
- Dosavadní `record-toggle-indicator` na konci `.record-actions` se **odstraní**.
- Zbytek hlavičky se neposouvá — indikátor se jen „vloží mezi".

### 3.2 Pravá strana — `.record-actions` ve dvou řadách

```
┌───────────────────────────────┐
│ [stav badge] [🖨] [✏] [›/˅]   │   ← řada 1: max 3 tlačítka dle oprávnění
│         [ Harmonogram ⭘――● ]  │   ← řada 2: toggle switch (jen s harmonogramem)
└───────────────────────────────┘
```

- **Tisk** (`data-print-trigger`) — beze změny.
- **Upravit** (tužka → editor záznamu, `CanEditRecord || CanManageSchedule`) — beze změny; jediné „upravit" (editor obsahuje i harmonogram).
- **Šipka menu** — `pm-button` variant Secondary/Small (ohraničený čtvereček, R8) s `gov-icon chevron-right`; po otevření `chevron-down`. Ikony přepínat toggle-em `hidden` na dvou vnořených `gov-icon` (nikdy `.textContent` — slot relocation). Atributy: `data-record-menu-trigger`, `aria-haspopup="menu"`, `aria-expanded`, `data-stop-propagation`.
- **Ikony kalendáře a kalendáře-plus z hlavičky zmizí** — funkce se stěhují do menu.
- Řada 2: `<gov-form-switch>` s labelem „Harmonogram", `data-record-view-switch`, `data-stop-propagation`. Renderuje se jen při `summary.MaHarmonogramHodnotu`. CSS stylování přes `[checked]`, ne `:checked`.
- Layout řad: prostý flex sloupec se dvěma flex řádky zarovnanými doprava. Žádné `:has`, žádné intrinsic keywords (i15).

### 3.3 Svislé menu

- Markup v `_ZaznamPartial` hned za trigger tlačítkem: `<div class="record-actions-menu" data-record-menu hidden role="menu">` se svislými položkami (celo-šířková tlačítka/odkazy s textem):
  1. **Zobrazit na záložce Harmonogram** — element s `data-goto-schedule="@summary.Id"` (existující globální handler v crossTabNav.js ho odchytí beze změny). Renderuje se při `MaHarmonogramHodnotu`.
  2. **Navrhnout změnu harmonogramu** — odkaz na `summary.ScheduleProposalUrl`. Renderuje se při `CanCreateScheduleProposal && ScheduleProposalUrl`.
  3. *(fáze 2)* **Otevřít na nové kartě** — až s variantou 2, teď se negeneruje.
- Nemá-li uživatel právo na žádnou položku, **trigger se vůbec nerenderuje**.
- JS modul `recordActionsMenu.js`: otevření = `mountFloatingPanel(panel, trigger)` (ui/floating.js — globální root, pozicování, flip, reposition při scrollu); zavření = klik na položku, ESC, klik mimo. Outside-close **musí kontrolovat mousedown-origin** (drag z menu ven nesmí zavřít přes retargetovaný click — lekce z modalů). Při otevření jiného menu `closeAllFloatingPanels()`. Delegovaný listener na `document` (přežije výměny karet).

## 4. Toggle — chování a obsah

### 4.1 Stavová pravidla (R9)

1. Přepnutí na „harmonogram" u sbalené karty kartu **rozbalí** (`toggleRecordCard(card, { expand: true })`).
2. Sbalení karty **nemění** polohu přepínače; po rozbalení se ukáže obsah dle přepínače.
3. Stav se drží na kartě: třída `record-card--schedule-view` + `card.dataset.recordViewSchedule="true"` + `checked` na switchi. Jediný zdroj pravdy = dataset; třídu a switch od něj odvozuje aplikační funkce `applyRecordViewState(card)`.
4. Cross-nav „Zobrazit záznam" ze záložky Harmonogram (`data-goto-record`) přepne kartu **zpět na záznam** (uživatel jde číst záznam, ne harmonogram, který právě opustil).

### 4.2 Obsah harmonogram pohledu

- Do `.record-body` přibude třetí shell: `<div class="record-schedule-shell" data-record-schedule-shell data-record-schedule-url="..." hidden>` (sourozenec detail shellu a vyjádření), s placeholder/error strukturou jako ostatní dva.
- V poloze „harmonogram": schedule shell viditelný; detail shell + vyjádření + tlačítka vyjádření skryté. Řídí třída `record-card--schedule-view` + obyčejné descendant selektory (`.record-card--schedule-view [data-record-detail-shell] { display:none }` atd.) — žádné `:has`.
- Obsah shellu dodá endpoint (§5): štítek `gov-tag` Stíháme/Nestíháme + sdílený `_ScheduleBlock` (mode `project-readonly`) s **rozpadem rovnou viditelným** (bez atributu `hidden` na `.schedule-steps` — nový volitelný flag `BreakdownExpanded` na `HarmonogramBlockViewModel`, výchozí `false` = dnešní chování všude jinde).

### 4.3 Lazy-load a kreslení

- `recordLazyLoading.js` dostane `loadRecordSchedule(card, options)` — kopíruje vzor `loadRecordDetail` (dataset `recordScheduleLoaded`, placeholder, error + retry hook `data-record-schedule-retry`).
- Po vložení HTML **a až po zviditelnění** shellu zavolat `renderStaticTimelineAxes(shell)` + `queueRainbowSegmentRender(shell)` — osy se měří z šířky, ve skrytém prvku mají nulu (ověřený vzor z „Rozpadu"). Pořadí: nastavit view třídu → načíst/vložit → vykreslit osy.

### 4.4 Persistence napříč obnovami (kritické — 3 cesty)

| Cesta | Kde | Co doplnit |
|---|---|---|
| Výměna karty po uložení vyjádření | `recordRefresh.js` → `refreshRecordCard` | před výměnou přečíst `recordViewSchedule` + `recordScheduleLoaded` z kotevní karty; po výměně znovu aplikovat stav (`applyRecordViewState`) a případně `loadRecordSchedule(card, { force: bylo-li načteno })` |
| Výměna celého panelu Záznamy (preserve) + bfcache `pageshow` | `recordRefresh.js` → `buildRecordUiState` / `restoreRecordUiState` | do stavu přidat `scheduleViewRecordIds`; při restore aplikovat view + načíst harmonogram u rozbalených |
| Deep-link po založení/editaci (`initProjectRecordDeepLink`) | `recordLazyLoading.js` | beze změny — výchozí pohled je záznam |

## 5. Server

### 5.1 Endpoint

`ZaznamyController.Partials.cs` → `[HttpGet] RecordSchedulePartial(int projektId, int zaznamId)`:
- guard `CanAccessProject` (jako sourozenci),
- service vrátí model, nebo `NotFound` když záznam neexistuje / není úkol / nemá vyplněnou hodnotu kroku (`HarmonogramKrokPredicates.MaVyplnenouHodnotu`),
- render nového partialu `Views/Projekty/_ZaznamSchedulePartial.cshtml` (gov-tag + `_ScheduleBlock`).

URL na kartu: `record.ScheduleUrl` (nová property shell VM), plní se v `PrepareRecordCardShellPresentation` — stejně jako `DetailUrl`/`CommentsUrl`.

### 5.2 Service

Nová metoda `BuildRecordScheduleBlockAsync(projektId, zaznamId, ct)` — **stejný vzor jako `BuildRecordCardShellAsync`**: implementace v `ProjectService` (nový malý partial; má dbContext, TimeProvider i privátní mapping `BuildScheduleBlockViewModel`), `RecordService` deleguje, signatura v `IProjectService` i `IRecordService`. Stavební kameny shodné se záložkou Harmonogram (`HarmonogramDateBlokBuilder.BuildSouhrn/BuildKroky/BuildBarLayout`, deadline = `AktualniTermin ?? DatumZalozeni`, delay barva `#dc2626`, `today` z `TimeProvider`). Vrací malý VM: `{ Stihame, HarmonogramBlok }` s `BreakdownExpanded = true`. Server layout (osa, ticky, pozice pruhů) **musí** být vyplněný — statická osa bez serverových ticků nelícuje (známá lekce).

## 6. Oprávnění a gating (shrnutí)

| Prvek | Podmínka |
|---|---|
| Tisk | vždy (dnešní stav) |
| Upravit | `CanEditRecord \|\| CanManageSchedule` (dnešní stav) |
| Menu trigger | aspoň jedna položka menu viditelná |
| Menu: Zobrazit na záložce Harmonogram | `MaHarmonogramHodnotu` |
| Menu: Navrhnout změnu harmonogramu | `CanCreateScheduleProposal && ScheduleProposalUrl` |
| Toggle switch | `MaHarmonogramHodnotu` |
| Endpoint RecordSchedulePartial | `CanAccessProject` + záznam má harmonogram hodnotu, jinak 404 |

Harmonogram pohled je **read-only** — žádné inputy, žádné ukládání.

## 7. Omezení a chyby

- **i15 pravidla** (feedback_i15_edge_css_compat): žádné `:has` v nosné logice, žádné `max-content`/intrinsic keywords, layout jen letité flex/grid základy. Statické soubory už mají charset fix.
- Chyba načtení harmonogramu: inline error + „Zkusit znovu" (retry hook), stejně jako detail/vyjádření. Karta zůstává funkční v pohledu záznam.
- Odstranění všech hodnot harmonogramu mezi renderem a klikem: endpoint vrátí 404 → error hláška v shellu („Záznam nemá harmonogram."), switch zůstává (stav světa při renderu karty; obnoví se s další výměnou karty).

## 8. Testy

- **Unit (Layout/JS piny):** markup pravidla _ZaznamPartial (indikátor vlevo první v headeru, max 3 tlačítka, menu položky dle oprávnění, switch gating); CSS pin `record-card--schedule-view` bez `:has`/intrinsic; pin mousedown-origin guardu v recordActionsMenu.js; pin `BreakdownExpanded` větve v _ScheduleBlock.
- **Api render:** karta záznamu s harmonogramem (switch + menu trigger + obě položky), bez harmonogramu (bez switche, menu jen dle práv), bez oprávnění na návrh (položka chybí); `RecordSchedulePartial` vrací gov-tag + schedule block s `data-schedule-ticks` a **viditelným** rozpadem; 404 pro záznam bez hodnot; regrese: `_ScheduleBlock` v záložce Harmonogram má rozpad stále `hidden`.
- **E2E (Playwright):** přepnutí switche na sbalené kartě → karta se rozbalí a ukáže rozpad kroků; sbalení+rozbalení → harmonogram zůstává; uložení vyjádření v jiné kartě / stejné kartě (před přepnutím) → poloha přepínače přežije obnovu; menu: otevření (šipka →/↓), překlik „Zobrazit na záložce Harmonogram" funguje jako dnešní ikona; `data-goto-record` z Harmonogramu vrací kartu do pohledu záznam.
- **Živě:** Playwright na 1470×956 — hlavička karty se nezalamuje, osy lícují; screenshoty pro ruční i15 verifikaci uživatelem.

## 9. Mimo rozsah — fáze 2 (evidováno, detailní spec později)

**Samostatná stránka záznamu** („Otevřít na nové kartě"): trvalá URL na jeden záznam, vizuálně hybrid karta/editor (členění do sekcí jako editor), **vše read-only kromě přidání vyjádření**. Technicky: levnější cesta „karta na stránce" (obnovovací mechanismus vyjádření najde `.record-card` a funguje beze změny) + harmonogram blok z endpointu této fáze; drobečky + returnUrl vzor z C1. Po dokončení se do menu karty přidá položka „Otevřít na nové kartě" (R5). Dvousloupcový layout jen pokud „karta na stránce" nebude stačit.
