# B3+B4 — Detail návrhu (schvalování): žádný dirty dialog + plně read-only formulář

**Datum:** 2026-07-09 · **Stav:** schváleno uživatelem (analýza 2026-07-09; B3 a B4 spolu — jedna stránka, jeden stav)

## Problém a kontext okolí
Detail návrhu (`NavrhyController.ProposalDetail` → EditZaznamPage) je čistě rozhodovací: schválit / zamítnout / zamítnout+převzít. `ConfigureProposalDetailEditor` (RecordProposalService.Queries.cs:470) nastavuje `model.IsProposalDecisionDetail = true` a vypíná `AllowBasicMetadataEdit/CanEdit*` — ale:
- **B4:** panely `_EditZaznamExternalPanel` a `_EditZaznamCollaborationPanel` flag NEkonzumují → externí vazby a spolupráce jsou editovatelné (checkboxy, inputy, add tlačítka živé).
- **B3:** JS guardy (historyTrap, `maybeGuardOutboundNavigation`, Zrušit prompt) flag neznají → editace z B4 udělá „dirty" a dialog vyskočí tam, kde nemá.

**Hodnocení „stavového formuláře" (dotaz usera):** `IsProposalDecisionDetail` UŽ JE stavový přepínač formuláře — plný stavový refactor by nepřinesl nic navíc. Zůstáváme u stávající cesty: flag konzumují všechny panely + JS. (Uživatelem výslovně ponecháno na mém zhodnocení.)

## Řešení

### B4 — read-only render všech panelů v decision módu
Do view kontextu (`_EditZaznamForm` → ViewData nebo přímo `Model.IsProposalDecisionDetail`, panely Model sdílí):
- `_EditZaznamCollaborationPanel`: checkboxy `disabled`, search input `disabled`; vizuálně tlumené (existující disabled styl).
- `_EditZaznamExternalPanel`: všechny inputy/selecty `disabled`, tlačítka „přidat/odebrat" se nerenderují.
- Kontrola ostatních panelů (schedule, term…): decision mód už kryjí `CanEditSchedule*`/`AllowTermDeadlineEdit` false — plán ověří grepem, že žádný interaktivní prvek nezůstal mimo gating; nalezené díry se zafixují stejně (flag).
- Decision stránka žádná pole nesubmituje (rozhodovací tlačítka postují commandy) → `disabled` je bezpečné (hidden-carrier problém z memory se netýká).

### B3 — vypnout guardy
`_EditZaznamForm`: při `Model.IsProposalDecisionDetail` render `data-record-editor-guard="off"` (komentář NAD tag — Razor komentář uvnitř TagHelper tagu polyká atributy, memory lekce). Konzumace:
- `historyTrap.initRecordEditorHistoryTrap`: form s `guard="off"` → nearmovat.
- `bootstrap.maybeGuardOutboundNavigation`: guard="off" → return false (propustit).
- Zrušit tlačítko (`requestRecordEditorPageCancel`/promptRecordEditorDiscard cesta): guard="off" → rovnou navigace bez promptu.
Po B4 by dirty stejně nevznikal, ale guard-off je sémanticky správně (rozhodovací stránka nemá koncept rozpracovanosti) a chrání i proti budoucím panelům.

## Dotčené soubory
- `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml` (atribut)
- `PmTracker.Web/Views/Projekty/_EditZaznamCollaborationPanel.cshtml`, `_EditZaznamExternalPanel.cshtml` (+ případné díry z auditu panelů)
- `PmTracker.Web/wwwroot/js/modules/recordEditor/historyTrap.js`, `bootstrap.js`, `recordEditor/draft.js` (guard-off větve)

## Akceptační kritéria
- Detail návrhu (create i schedule typ): žádný input/checkbox/select editovatelný, žádná add/remove tlačítka na externí/spolupráci; rozhodovací tlačítka fungují.
- Browser back / breadcrumb / Zrušit z detailu návrhu ⇒ **nikdy dialog**, přímý odchod.
- Editor záznamu (Create/Edit) a návrhové EDITORY (CreateRecordProposal/Prefill — tam se edituje!) ⇒ guardy fungují dál (regrese A8/B2).

## Testy
- Api render: ProposalDetail HTML — `data-record-editor-guard="off"`, collab checkboxy `disabled`, external panel bez add tlačítka; CreateRecordProposal HTML guard-off NEMÁ.
- E2E: otevřít detail návrhu → breadcrumb ← ⇒ bez dialogu; (fixture: seed návrhu přes SubmitCreateProposal command — Integration datastore vzor existuje).
- Unit source-assertion: JS soubory obsahují guard-off větve.

## Mimo scope
Workflow rozhodování (Approve/Reject commandy); vizuální redesign decision stránky.
