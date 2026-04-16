# Analýza a plán oprav — chyby po posledním push

Datum: 2026-04-16

---

## Souhrn nalezených chyb

### BUG-1: AJAX formuláře posílají data na špatnou URL (KRITICKÉ)

**Příznaky:** Založení jednání, úprava jednání i smazání jednání (z meeting karty) vrací `NON_JSON_RESPONSE` — server vrátí celou HTML stránku místo JSON.

**Kořenová příčina:**
V souboru `PmTracker.Web/wwwroot/js/modules/ajax.js`, řádky 728-734. Při přidávání podpory pro `formaction` na submit tlačítkách byla zavedena chyba:

```javascript
const submitterAction = submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement
    ? (submitter.getAttribute("formaction") || submitter.formAction || "")
    : "";
```

IDL vlastnost `submitter.formAction` vrací **URL aktuální stránky** (document base URL) když tlačítko NEMÁ atribut `formaction`. To je standardní chování prohlížeče — stejně jako `form.action` vrací URL stránky když chybí atribut `action`.

Fallback chain `submitter.getAttribute("formaction") || submitter.formAction || ""`:
1. `getAttribute("formaction")` → `null` (tlačítko nemá formaction)
2. `submitter.formAction` → `"http://localhost:8084/Projekty/Detail/2?tab=jednani"` (truthy!)
3. `submitterAction` → URL stránky místo prázdného řetězce

Pak na řádku 734:
```javascript
const action = appendCurrentAsUser(submitterAction || target.getAttribute("action") || window.location.href);
```
`submitterAction` je truthy (URL stránky) → `target.getAttribute("action")` (správná `/Projekty/SaveMeeting`) se přeskočí → fetch pošle POST na URL aktuální stránky.

Detail action na ProjektyControlleru nemá `[HttpGet]` atribut, takže přijme POST a vrátí HTML.

**Stejný bug na řádku 731** pro `submitterMethod`:
```javascript
const submitterMethod = ... submitter.getAttribute("formmethod") || submitter.formMethod || ""
```
`submitter.formMethod` vrací `"get"` (výchozí) nebo `""` podle prohlížeče — ne tak kritické, ale nesprávné.

**Dotčené soubory:**
- [ajax.js:728-735](PmTracker.Web/wwwroot/js/modules/ajax.js#L728-L735)

**Oprava:**
```javascript
const submitterAction = submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement
    ? (submitter.getAttribute("formaction") || "")
    : "";
const submitterMethod = submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement
    ? (submitter.getAttribute("formmethod") || "")
    : "";
```

Odstranit `submitter.formAction` a `submitter.formMethod` z fallback řetězce. Používat POUZE `getAttribute()` který vrací `null` když atribut neexistuje.

**Stejná oprava v `site.bundle.js`** — bundlovaný soubor musí být synchronizován.

---

### BUG-2: Globální vyhledávání není viditelné v UI

**Příznaky:** V horní liště vedle loga chybí vyhledávací pole.

**Kořenová příčina — 2 problémy:**

1. **Feature flag vypnutý**: V `appsettings.json` je `"Search": { "Enabled": false }`. Layout v `_Layout.cshtml:46-52` renderuje search pole podmíněně:
   ```razor
   @if (SearchOptionsAccessor.Value.Enabled)
   ```

2. **Chybějící CSS styly**: Třídy `.app-search` a `.app-search-dropdown` nemají žádné CSS styly v `site.css` (5668 řádků prohledáno). HTML element existuje v DOM (pokud je enabled), ale je neviditelný bez stylů.

**Dotčené soubory:**
- [_Layout.cshtml:46-52](PmTracker.Web/Views/Shared/_Layout.cshtml#L46-L52) — HTML existuje, OK
- `PmTracker.Web/wwwroot/css/site.css` — chybí styly pro `.app-search`
- `appsettings.json` — feature flag

**Oprava:**
1. Přidat CSS styly pro `.app-search` a `.app-search-dropdown` do `site.css`
2. Zapnout feature flag pro dev prostředí v `appsettings.Development.json`

---

### BUG-3: Smazání jednání — chybový log na špatné pozici + data se neaktualizují

**Příznaky:**
- Po smazání z meeting karty na záložce Jednání se zobrazí chybový log POD kartami místo na vrcholu stránky
- Po úpravě jednání se neaktualizuje stránka/data

**Kořenová příčina:**

A) **Úprava jednání** — stejný BUG-1 (`submitter.formAction` bug). Formulář v modálu `NewMeetingModal.cshtml` má `asp-action="SaveMeeting"`, ale AJAX handler posílá data na URL aktuální stránky.

B) **Smazání z meeting karty** — `_MeetingCard.cshtml:29` má `data-ajax-submit="true"`, formulář má `asp-action="DeleteMeeting"`, ale opět BUG-1 způsobí odeslání na špatnou URL.

C) **Pozice chybového logu** — `renderModalFormErrors()` v ajax.js:272 vkládá error summary jako `form.insertBefore(summary, form.firstElementChild)`. Pokud formulář je inline (uvnitř meeting karty), chyba se zobrazí uvnitř karty, ne na vrcholu stránky. Pro non-modal formuláře by se chybová zpráva měla zobrazit nad formulářem nebo na viditelném místě.

D) **Smazání z Jednani Detail** — formulář na `Jednani/Detail.cshtml:67` NEMÁ `data-ajax-submit="true"`. Po smazání proběhne normální form submit s redirect na project detail. Toto by mělo fungovat, ale chybí `data-ajax-submit="true"` pro konzistentní UX.

**Dotčené soubory:**
- [ajax.js:728-735](PmTracker.Web/wwwroot/js/modules/ajax.js#L728-L735) — BUG-1 oprava vyřeší A a B
- [ajax.js:272](PmTracker.Web/wwwroot/js/modules/ajax.js#L272) — pozice error summary
- [Detail.cshtml:67](PmTracker.Web/Views/Jednani/Detail.cshtml#L67) — chybí `data-ajax-submit`

---

### BUG-4: Návrh úpravy harmonogramu — špatný rozsah editace

**Příznaky:**
- Uživatel může změnit Typ úkolu (má být zakázáno)
- Karty "Externí vazby" a "Spolupráce" jsou viditelné (nemají být)
- Má být vidět pouze karta "Základní údaje" (jen Termín ukončení editovatelný) a "Harmonogram"

**Kořenová příčina:**

A) **Viditelnost karet** — v `_EditZaznamForm.cshtml:7`:
```razor
var canUseBasicTabs = Model.CanEditRecord || Model.IsProposalEditor || Model.IsProposalDecisionDetail;
```
Pro schedule proposal je `IsProposalEditor = true`, takže VŠECHNY basic tabs jsou viditelné. Chybí rozlišení mezi typy návrhů.

B) **Typ úkolu** — `ConfigureScheduleProposalEditor` nastavuje `AllowBasicMetadataEdit = false`, což by mělo nastavit `metadataLocked = true` v `_EditZaznamBasicPanel.cshtml:8`. Select na řádku 43 by pak měl mít `disabled="disabled"`. Pokud přesto nejde editovat, `AllowBasicMetadataEdit` je správně false. Nicméně je třeba ověřit, že žádný JS toto disabled neodstraňuje, a přidat explicitní serverovou validaci v `SubmitScheduleProposal` akci.

**Dotčené soubory:**
- [_EditZaznamForm.cshtml:7, 62-88](PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml#L7)
- [_EditZaznamBasicPanel.cshtml:8, 43](PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml#L8)
- [RecordProposalService.cs:584-604](PmTracker.Web/Services/RecordProposalService.cs#L584-L604)

**Oprava:**

1. Přidat do `ZaznamEditViewModel` property `ShowExternalTab` a `ShowCollaborationTab` (defaultně `true`)
2. V `ConfigureScheduleProposalEditor` nastavit obě na `false`
3. V `_EditZaznamForm.cshtml` podmínit viditelnost tabů "Externí vazby" a "Spolupráce" podle těchto properties
4. Přidat serverovou validaci: v `SubmitScheduleProposal` akci ignorovat hodnoty polí která nemají být editovatelná (Kategorie, TypUkolu, Stav)

---

## Spojení s TODO-review.md

Existující nálezy z code review (`TODO-review.md`) zůstávají platné. Tyto nové bugy jsou prioritnější:

| Priorita | Zdroj | Nález | Soubor |
|----------|-------|-------|--------|
| P0 — BLOK | Nový | BUG-1: `submitter.formAction` bug | ajax.js |
| P0 — BLOK | Nový | BUG-3: úprava/smazání jednání (důsledek BUG-1) | ajax.js |
| P1 | Nový | BUG-4: rozsah editace v návrhu harmonogramu | _EditZaznamForm.cshtml, RecordProposalService.cs |
| P1 | TODO-review #1 | IsAddOnlyMode regrese v _ScheduleBlock.cshtml | _ScheduleBlock.cshtml |
| P1 | TODO-review #2 | ToDictionary crash při duplicitních klíčích | SchedulePreviewService.cs |
| P2 | Nový | BUG-2: vyhledávání — CSS + feature flag | site.css, appsettings |
| P2 | Nový | BUG-3C: pozice error logu pro inline formuláře | ajax.js |
| P2 | TODO-review #3 | double debounce + promise leak v recalcAll | schedule.js |
| P3 | TODO-review #4 | ScheduleVersion se nepřenáší v CloneScheduleBlock | RecordProposalService.cs |
| P3 | TODO-review #5 | ForActivePlanProposal mrtvý kód | ScheduleEditorPermissionSet.cs |
| P3 | TODO-review #7 | TODO komentáře bez implementace | HarmonogramService.cs |
| P3 | TODO-review #8 | DTOs smíchané se service třídou | SchedulePreviewService.cs |

---

## Implementační pořadí

### Krok 1: Oprava AJAX submitter bugu (řeší BUG-1 + BUG-3A/B)
- Soubor: `ajax.js` řádky 728-731
- Soubor: `site.bundle.js` — synchronizovat
- Odstranit `submitter.formAction` a `submitter.formMethod` z fallback chain
- **Ověření:** E2E test pro vytvoření jednání, úpravu jednání, smazání jednání z karty

### Krok 2: Oprava viditelnosti tabů v návrhu harmonogramu (BUG-4)
- Přidat properties `ShowExternalTab`, `ShowCollaborationTab` do `ZaznamEditViewModel`
- Nastavit na `false` v `ConfigureScheduleProposalEditor`
- Podmínit viditelnost v `_EditZaznamForm.cshtml`
- Přidat serverovou validaci v `NavrhyController.SubmitScheduleProposal`
- **Ověření:** E2E test pro vytvoření návrhu změny harmonogramu

### Krok 3: Opravy z TODO-review.md (P1)
- IsAddOnlyMode v `_ScheduleBlock.cshtml`
- ToDictionary crash v `SchedulePreviewService.cs`

### Krok 4: Vyhledávání (BUG-2)
- CSS styly pro `.app-search`, `.app-search-dropdown`
- Feature flag enable v dev konfiguraci
- **Ověření:** vizuální kontrola + E2E test

### Krok 5: Chybový log pozice (BUG-3C) + Detail delete AJAX (BUG-3D)
- Upravit `renderModalFormErrors` pro non-modal formuláře
- Přidat `data-ajax-submit="true"` na delete form v `Jednani/Detail.cshtml`

### Krok 6: Zbylé P2/P3 nálezy z TODO-review.md
- double debounce + promise leak
- ScheduleVersion v CloneScheduleBlock
- Mrtvý kód a TODO komentáře

---

## E2E testy které by měly tyto bugy zachytit

1. **Test založení jednání**: POST na `/Projekty/SaveMeeting` musí vrátit JSON s `ok: true`
2. **Test úpravy jednání**: modal edit → POST → JSON response → data refresh
3. **Test smazání jednání z karty**: inline delete → POST → JSON response → karta zmizí
4. **Test smazání jednání z detailu**: formulář → redirect → stránka se přesměruje
5. **Test návrhu harmonogramu — editovatelnost**: ověřit že Typ úkolu je disabled, Termín ukončení je enabled, karty "Externí vazby" a "Spolupráce" nejsou viditelné
6. **Test vyhledávání**: search pole je viditelné v headeru, reaguje na vstup
