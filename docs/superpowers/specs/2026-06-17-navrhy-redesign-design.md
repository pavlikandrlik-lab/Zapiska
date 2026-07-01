# Návrhy (record proposals) — redesign 7a–7e

**Datum:** 2026-06-17
**Kontext:** Projekty → záložka Návrhy + sdílený editor záznamu (`_EditZaznamForm`).

## Vůdčí princip (DRY)

Jeden editor (`_EditZaznamForm` + `_EditZaznamBasicPanel` + `recordEditor/form.js`) slouží klasickému create/edit i návrhům. Návrhy se liší **jen výstupním cílem** (`SubmitCreateProposal`/`SubmitScheduleProposal` vs `Save`) a **tím, která pole jsou zamčená**. Chyby v návrzích pramení z proposal-specifických výjimek, které zbytečně rozcházejí návrh od (funkčního) klasického editoru. Opravujeme tak, že **odebereme zbytečné rozdíly** a ponecháme jen ty pravé (návrh změny harmonogramu má zamčená basic pole).

Režimy editoru:
- **Klasik** (`Save`) — funguje, sahat minimálně.
- **Nový návrh založení** (`ConfigureCreateProposalEditor`) — kategorie volitelná, basic editable.
- **Návrh změny termínu/harmonogramu** (`ConfigureScheduleProposalEditor`) — existující úkol, basic pole zamčená (`AllowBasicMetadataEdit=false`, `CanEditRecord=false`), harmonogram otevřený. 7d/7b se ho netýká.
- **Detail návrhu (posouzení)** (`ConfigureProposalDetailEditor`) — read-only, rozhodovací tlačítka.

---

## 7a — Akce na panelu → jen „Detail návrhu", rozhodnutí v detailu

**Soubory:** `Views/Projekty/_ProjectProposalsTab.cshtml`, `Views/Projekty/_EditZaznamForm.cshtml`, `Services/RecordProposalService.Queries.cs` (`ConfigureProposalDetailEditor`), `Models/ViewModels/Projekty/ZaznamEditViewModels.cs`.

- Panel `_ProjectProposalsTab.cshtml`, obě sekce (NavrhyZalozeni, NavrhyHarmonogramu): v `.proposal-card-actions` ponechat **jen** `Detail návrhu`. Odstranit `<form>` bloky Schválit / Zamítnout / Zamítnout a převzít data / Zamítnout a upravit. „Předvyplnit formulář" (jen rozhodnuté návrhy, není rozhodovací tlačítko) ponechat na panelu.
- Detail (`_EditZaznamForm`, `IsProposalDecisionDetail`): doplnit pro návrhy **založení** tlačítko **„Zamítnout a převzít data"** (`RejectAndTakeOverCreateProposal`). Přidat na model flag `CanRejectAndTakeOverProposal` a nastavit ho v `ConfigureProposalDetailEditor` jen pro typ `CreateRecord` (a jen když `canDecide`). Footer detailu pak: Schválit / Zamítnout / (Zamítnout a převzít data — založení) / (Zamítnout a upravit — harmonogram).

## 7b — Harmonogram v create-proposalu: plán-only

**Soubory:** `_EditZaznamForm.cshtml`, `_EditZaznamSchedulePanel.cshtml` + schedule block partial(y), `RecordProposalService.Queries.cs`, `ZaznamEditViewModels.cs`/schedule perm set.

- Nový create-proposal harmonogram = **plán editovatelný, skutečnost skrytá** (ne disabled — skrytá). Realita (skutečnost) až po založení v klasickém režimu.
- Přidat příznak `ScheduleHideActual` (bool) na `ZaznamEditViewModel` (nebo do `ScheduleEditorPermissionSet`). `ConfigureCreateProposalEditor` ho nastaví `true` (+ zajistí plán editovatelný). Schedule blok při `ScheduleHideActual` nerenderuje skutečnostní inputy/sloupec.
- Detail návrhu (posouzení) harmonogram = read-only (jak dnes).
- Návrh změny harmonogramu beze změny.

## 7c — Externí vazby v create-proposalu: jen čísla, netěžit

**Soubory:** `Controllers/NavrhyController*` / `RecordProposalService` submit-create flow, `Services/Records/RecordProposalPayloadMapper.cs`, validační cesta.

- Submit návrhu založení: uložit **zadaná čísla** externích vazeb do payloadu (`CreateRecordProposalPayload.ExterniVazby`). Validovat **jen formát** (6 cifer), **NE** SD-existence (`external_sd_ticketing_disabled` / fingerprint lookup) a **NE** harvest/extrakci.
- Harvest + 4 datumy + bubliny: až po **schválení**, kdy vznikne reálný záznam klasickou cestou (ta už harvest spouští). Tj. proposal-submit cesta nesmí volat `ExterniOdkazValidator.ValidateCreateAsync` ani `harvestScheduler`.

## 7d — „Nový návrh záznamu": typ úkolu + harmonogram při kategorii úkol

**Root cause:** `recordEditor/form.js:141` `typeSelect.disabled = !isTask || metadataLocked || isProposalEditor` — `isProposalEditor` typ úkolu v návrhu vždy zamkne (staré rozhodnutí 2026-04-19). Harmonogram tab je server-side gated na `JeUkolKategorie` (default kategorie = „Informace" → false → tab není v DOM → JS ho nemůže odkrýt). Klasik i návrh mají stejnou server logiku; jediný proposal-specifický rozdíl je `isProposalEditor` u typu.

**Soubory:** `wwwroot/js/modules/recordEditor/form.js`, `_EditZaznamBasicPanel.cshtml`, `_EditZaznamForm.cshtml`.

- Odstranit `|| isProposalEditor` z type-disable v `form.js` i z odpovídající Razor `disabled` logiky v `_EditZaznamBasicPanel.cshtml` (typ řídí `!isTask || metadataLocked`). → návrh změny harmonogramu zůstává zamčený (má `metadataLocked=true`), create-proposal i klasik povolí typ při kategorii úkol.
- `_EditZaznamForm.cshtml`: harmonogram **tab + panel** (a master switch) renderovat **vždy**, ale **skryté** když `!JeUkolKategorie` (místo `@if (Model.JeUkolKategorie)`), aby je JS mohl odkrýt při výběru kategorie úkol. JS už `scheduleTab.hidden` přepíná podle `isTask`.
- **Klasik:** type-fix je no-op (klasik není `isProposalEditor`). Always-render harmonogramu ověřit, že nerozbije klasické create/edit ani existující testy (`ProjectHarmonogramRenderTests`, `ScheduleJsSplitTests`, atd.).
- Platí jen tam, kde se kategorie vybírá (create-proposal + klasik). Návrh změny harmonogramu neřešen.

## 7e — Footer: tlačítka u sebe, text jako poznámka pod čarou

**Soubor:** `_EditZaznamForm.cshtml` (`.record-editor-actions`).

- Pořadí: akční tlačítka **u sebe** — „Zrušit a vrátit se" + primární (`PrimaryActionLabel` / „Odeslat návrh a vrátit se do projektu") resp. rozhodovací tlačítka v detailu. `SecondaryNote` a `ProposalSummaryNote` přesunout **pod** tlačítka jako `<p class="muted record-editor-footnote">` (poznámka pod čarou), ne mezi tlačítka.

---

## Testování

- 7a/7e: render testy (Api) — panel karta má jen „Detail návrhu"; detail založení má „Zamítnout a převzít data"; footer note je za tlačítky.
- 7b: render — create-proposal harmonogram nemá skutečnostní inputy; detail read-only.
- 7c: submit návrhu s 6-cif. čísly projde bez SD/harvest; payload obsahuje čísla; neplatný formát odmítnut.
- 7d: unit (JS split / Razor render) — typ úkolu není v create-proposalu disabled; harmonogram tab je v DOM (skrytý) i pro ne-úkol; klasik beze změny.
- Vždy: `dotnet build` + cílené + plné suite, žádné regrese.
