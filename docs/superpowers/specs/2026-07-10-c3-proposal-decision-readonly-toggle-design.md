# C3 — Schvalování návrhu: toggle auto-vyplňování read-only + sibling audit (spec)

**Datum:** 2026-07-10 · **Stav:** schváleno uživatelem („ok") · **Náročnost:** malá

## Problém

Na detailu schvalování návrhu (`IsProposalDecisionDetail`) zůstal editovatelný
master switch „Automatické vyplňování harmonogramu"
(`_EditZaznamForm.cshtml:157`, `gov-form-switch[data-record-rezim-switch]`).
Sedí v řádku záložek **mimo panely**, proto unikl B4 auditu (ten pokrýval
panely Spolupráce a Externí). Pravidlo: **na schvalování je vše read-only,
žádný editovatelný prvek.**

## Návrh

1. **Switch:** `disabled` atribut při `Model.IsProposalDecisionDetail`
   (stav zůstane viditelný — schvalovatel má vidět, zda je záznam
   v auto režimu). Hidden input `HarmonogramRezim` beze změny (decision
   stránka nemá save submit; JS `rezim-master-switch` na disabled hostu
   nic nepřepne — ověřit, případně guard-off v JS).
2. **Sibling audit celé decision stránky** (fix-all-siblings): Playwright
   sweep — enumerace všech interaktivních prvků
   (`input, select, textarea, button, gov-form-switch, pm-button,
   [contenteditable]`) na stránce schvalování; vše mimo rozhodovací akce
   (Schválit/Zamítnout/Zpět, případně přepínání záložek) musí být
   disabled/readonly/absent. Zvláštní pozornost: schedule panel
   (`_EditZaznamSchedulePanel` řídí `data-schedule-disabled` jen podle
   kategorie, ne podle decision režimu — manuální datum buňky,
   `_ScheduleBlockManualCell`), date/time pickery, person picker.
   Každý nález opravit u zdroje (partial), ne per-stránka.

## Testy

- **Api render (rozšíření `ProposalDecisionReadonlyRenderTests`):**
  decision detail → rezim switch má `disabled`; editor návrhu (create flow)
  → switch editovatelný (negativní kontrola). Po sweep auditu: pin na
  všechny nalezené+opravené prvky.
- **E2E/Playwright:** decision stránka — sweep asserce „0 editovatelných
  prvků mimo whitelist rozhodovacích akcí"; regresně normální editor
  (guard-on) zůstává plně editovatelný.

## Mimo scope

Změny decision flow (schválit/zamítnout logika) — beze změny.
