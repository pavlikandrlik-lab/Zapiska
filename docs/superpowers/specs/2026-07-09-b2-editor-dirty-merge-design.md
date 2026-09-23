# B2 — Editor záznamu: přepnutí na Harmonogram nesmí zahodit rozpracovanost

**Datum:** 2026-07-09 · **Stav:** schváleno uživatelem (analýza 2026-07-09)

## Problém a kontext okolí (root cause)
`onScheduleTabActivated` ([recordEditor/form.js:300-332](../../../PmTracker.Web/wwwroot/js/modules/recordEditor/form.js)) při **první** aktivaci schedule tabu **přestaví celý dirty baseline** (`form.dataset.recordEditorSnapshot`) na aktuální stav formu. Záměr (FIX 2026-05-05): absorbovat planner normalizaci — planner při aktivaci přepisuje `UiHarmonogramDatumy[*]` a normalizuje duration/delay hodnoty, což by jinak dělalo falešný dirty. Vedlejší efekt: **absorbuje i user změny udělané před kliknutím na tab** → form „clean" → dialog (browser back / breadcrumb / Zrušit) nevyskočí. Platí pro editaci i nový záznam.

Snapshot formát (draft.js:57): řazené řádky `key=value` z FormData (ignorované klíče přes `shouldIgnoreRecordEditorField`).

## Řešení — merge místo plného rebuildu
Při první aktivaci schedule tabu (stávající guard `recordEditorScheduleSnapshotRebuilt` zůstává):
1. Parse baseline snapshot → mapa klíč→[hodnoty]; parse aktuální snapshot → mapa.
2. V baseline **přepsat pouze schedule klíče** hodnotami z aktuální mapy. Schedule klíč = název začínající jedním z prefixů: `UiHarmonogramDatumy`, `HarmonogramHodnoty`, `ScheduleVersion` (přesný výčet finalizuje plán grepem všech polí, která planner zapisuje — kritérium correctness určují akceptační testy níže, ne výčet).
3. Serializovat zpět (řazené) → nový baseline.

Efekt: planner šum absorbován (schedule-only návštěva zůstane clean), user dirt mimo schedule přežije (dialog se ukáže).

## Dotčené soubory
- `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js` — `onScheduleTabActivated` (merge helper)
- příp. `draft.js` — export parse/serialize helperů (sdílené s buildRecordEditorFormSnapshot)

## Akceptační kritéria
1. Nový záznam: vyplnit Název → klik na tab Harmonogram → klik na tab Základní → browser back ⇒ **dialog se ukáže**; breadcrumb ← ⇒ dialog; Zrušit ⇒ dialog.
2. Otevřít editor → klik na Harmonogram (nic nevyplněno) → back ⇒ **bez dialogu** (planner šum nesmí dělat falešný dirty) — regrese původního fixu 2026-05-05.
3. Vyplnit Název → Harmonogram → ZMĚNIT datum kroku → zpět; dialog ⇒ ano (schedule user změna po merge zůstane dirty vůči novému baseline).
4. Platí pro Create i Edit.

## Testy
- E2E (rozšíření `RecordEditorHistoryBackScenariosTests`): scénář 1 (dirty přežije tab switch) + scénář 2 (čistý zůstane čistý po tab switch).
- Unit source-assertion: onScheduleTabActivated neobsahuje plný `buildRecordEditorFormSnapshot(form)` přiřazený do snapshotu bez merge (pin mechanismu).

## Mimo scope
Změny planner logiky; per-field dirty UI.
