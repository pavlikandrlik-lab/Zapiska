# Rozhodovací tlačítka návrhu posílají POST na `/` → 405 (formaction se ztrácí na gov-button)

**Nahlášeno:** 2026-06-23 (klient error EMPTY_AJAX_RESPONSE)
**Analýza:** 2026-06-25
**Závažnost:** vysoká — celý decision flow návrhů je nefunkční (Schválit i všechna Zamítnout).
**Status:** ✅ opraveno 2026-06-25 — viz „Provedená oprava" níže.

## Symptom

Klik na **Schválit** v detailu návrhu skončí klientskou chybou:

```
ErrorCode: EMPTY_AJAX_RESPONSE
ClientSource: site.js:initModalAjaxSubmit
Request: POST /
ResponseUrl: http://localhost:8084/
Status: 405 Method Not Allowed
allow: GET
Body: <empty>
```

Formulářová data byla správná (`ProposalId=2`, `ProjektId=2`, antiforgery token, …) — jen **cíl** requestu byl špatný: POST šel na root `/` (GET-only) místo na `/Navrhy/ApproveProposal`.

## Root cause

Formulář detailu návrhu **záměrně nemá vlastní akci** — je to jeden sdílený formulář a každé rozhodovací tlačítko nese vlastní `formaction`:

- `RecordProposalService.Queries.cs:435-436` → pro decision detail `FormAction = ""`, `FormController = ""`.
- `Views/Projekty/_EditZaznamForm.cshtml:171-208` → 4 tlačítka, každé s `formaction="@Url.Action(...)"` + `formmethod="post"`.

Tlačítko `<pm-button>` se ale renderuje jako **`gov-button`** (web komponenta gov-design-system), ne jako nativní `<button>`:

1. `TagHelpers/PmButtonTagHelper.cs:42` → `output.TagName = "gov-button"`. Neznámé atributy (`formaction`, `formmethod`) projdou pass-through **na host element `<gov-button>`**.
2. `gov-button` interně vykreslí nativní `<button>`, ale do něj kopíruje jen pevný seznam atributů: `type, disabled, id, href, target, download, name, tabindex` + `inheritedAttributes` (pouze `aria-*`). **`formaction`/`formmethod` nepřenáší.**
   - Render: `wwwroot/lib/gov-design-system/dist/core/p-9ff7e018.entry.js`, funkce `render()`.
   - `grep -rn formaction wwwroot/lib/gov-design-system` = **0 výskytů** → komponenta tento atribut vůbec nezná.
3. Při submitu je `event.submitter` ten vnitřní nativní `<button>` — **bez `formaction`**.
4. `wwwroot/js/modules/ajax.js:794-800`:
   ```js
   const submitterAction = isButtonLike(submitter)
       ? (submitter.getAttribute("formaction") || "")   // → "" (host má formaction, inner button ne)
       : "";
   const action = appendCurrentAsUser(
       submitterAction || target.getAttribute("action") || window.location.href);
   ```
   `submitterAction = ""` → fallback na `form action`, který je u decision detailu prázdný a routing ho vyhodnotí jako `/` → `action = "/"`.
5. `fetch("/", { method: "POST" })` → **405 Method Not Allowed** (root je GET-only) → prázdné tělo → `EMPTY_AJAX_RESPONSE`.

Sedí na všechny údaje z reportu: `POST /`, `Status 405`, `allow: GET`, prázdné tělo.

Souvislost: gov-button se obecně nechová jako nativní `<button>` (viz `instanceof HTMLButtonElement` quirk, helpery `isButtonLike`/`setButtonDisabled`).

## Rozsah — postižená tlačítka

Všechna 4 rozhodovací tlačítka v `Views/Projekty/_EditZaznamForm.cshtml` sdílí identickou příčinu. Jsou to **jediné** výskyty `formaction`/`formmethod` v celém `Views/`:

| Tlačítko | formaction → akce | Endpoint | Stav |
|---|---|---|---|
| Schválit | `ApproveProposal` | `NavrhyController.cs:134` `[HttpPost]` | ❌ POST `/` → 405 |
| Zamítnout | `RejectProposal` | `NavrhyController.cs:149` `[HttpPost]` | ❌ stejná chyba |
| Zamítnout a převzít data | `RejectAndTakeOverCreateProposal` | `NavrhyController.cs:164` `[HttpPost]` | ❌ stejná chyba |
| Zamítnout a upravit | `RejectAndEditProposal` | `NavrhyController.cs:187` `[HttpPost]` | ❌ stejná chyba |

Všechny cílové akce existují jako platné `[HttpPost]` endpointy a `Url.Action` jim generuje správné URL — to URL ale gov-button zahodí. Reportováno jen Schválit, protože to uživatel kliknul jako první.

### Tlačítka, která jsou v pořádku (pro kontrolu při opravě neměnit)

- **Zrušit a vrátit se** (`_EditZaznamForm.cshtml:165`) — `native-type="button"`, nesubmitje.
- **Detail návrhu / Předvyplnit formulář** (`_ProjectProposalsTab.cshtml`) — `data-modal-url`, otevírají modal, ne form submit.
- **Uložit / Založit** (`_EditZaznamForm.cshtml:214`) — formulář má vlastní `asp-action` (`Save` / `SubmitCreateProposal` / `SubmitScheduleProposal`), na `formaction` nespoléhá.

## Navrhovaná oprava

Jeden zásah opraví všechna 4 tlačítka najednou.

**Doporučeno — opravit v JS submit handleru** `wwwroot/js/modules/ajax.js` (kolem ř. 794):
Při čtení `formaction`/`formmethod` ze submitteru vystoupat z nativního inner `<button>` na nejbližší `gov-button` host a vzít atribut odtud. Pseudokód:

```js
const submitterHost = submitter instanceof Element
    ? (submitter.closest("gov-button") || submitter)
    : null;
const readAttr = (name) => {
    if (!(submitter instanceof Element)) return "";
    return submitter.getAttribute(name)
        || (submitterHost && submitterHost.getAttribute(name))
        || "";
};
const submitterAction = readAttr("formaction");
const submitterMethod = readAttr("formmethod");
```

(Pozor: `event.submitter` může být inner button uvnitř shadow/host gov-buttonu; ověřit, zda `closest("gov-button")` host najde — pokud gov-button renderuje do shadow DOM, `event.submitter` je host sám a `closest` vrátí ten host. Ověřit v prohlížeči, čím přesně je `event.submitter`.)

**Alternativy (horší):**
- Forwardovat `formaction` v `PmButtonTagHelper` na inner button — **nejde**, TagHelper nemá přístup do shadow DOM gov-buttonu.
- Rozdělit decision detail na 4 samostatné formuláře, každý s `asp-action` — větší zásah do view, duplikace hidden inputů.

## Provedená oprava (2026-06-25)

Zvolena doporučená varianta — oprava v JS submit handleru. Ověřeno proti aktuálnímu
bundlu gov-design-system:

- gov-button se renderuje **bez shadow DOM** (light DOM, scoped). Inner `<button class="element">`
  je tedy reálný `event.submitter` a `submitter.closest("gov-button")` host najde.
- Render inner buttonu (`p-9ff7e018.entry.js`) kopíruje jen `disabled, id, href, target,
  download, hreflang, rel, name, type, tabindex` + `aria-*` → `formaction`/`formmethod` chybí.
- `grep formaction` v celém `wwwroot/lib/gov-design-system/dist/` = 0 výskytů (potvrzeno znovu).

Implementace:
- `wwwroot/js/modules/utils.js` — nová čistá funkce `resolveFormSubmitterAttr(submitter, attrName, closestGovButton)`:
  vrátí atribut ze submitteru, jinak vystoupá na předaný gov-button host.
- `wwwroot/js/modules/ajax.js` (~ř. 794) — submit handler nově počítá
  `submitterHost = submitter.closest("gov-button")` a čte `formaction`/`formmethod`
  přes `resolveFormSubmitterAttr`. Jeden zásah opravil všechna 4 rozhodovací tlačítka.
- Regresní test: `tests/js/dom/formSubmitterAttr.test.js` (node:test, 5 případů vč.
  gov-button host fallbacku). Spouští se přes `npm test`.

## Reprodukce / test

- Repro: otevřít detail návrhu (stav „k rozhodnutí"), kliknout Schválit → 405 na `/`.
- Regresní test: ověřit, že submit přes gov-button s `formaction` pošle POST na danou akci (ne na `/`). Buď JS unit test nad handlerem (extrahovat resolveSubmitAction), nebo E2E/Playwright na decision flow.

## Dotčené soubory

- `PmTracker.Web/wwwroot/js/modules/ajax.js` (~ř. 794-801) — místo opravy
- `PmTracker.Web/TagHelpers/PmButtonTagHelper.cs` — kontext (render gov-button)
- `PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml:171-208` — postižená tlačítka
- `PmTracker.Web/Controllers/NavrhyController.cs:131-200` — cílové endpointy
- `PmTracker.Web/Services/RecordProposalService.Queries.cs:435-436` — prázdná FormAction u decision detailu
