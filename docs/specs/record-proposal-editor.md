# Specifikace — editor návrhu úpravy záznamu (record-proposal-editor)

Dokumentuje chování formuláře **„Navrhnout změnu termínu a harmonogramu"** (proposal editor).

---

## Kontext

Proposal editor se otevírá z detailu záznamu (tlačítko „Navrhnout termín a harmonogram")
a slouží VS / zástupcům VS subsystému k podání návrhu na změnu termínu ukončení záznamu
a harmonogramu. Návrh se následně schvaluje PM/ADM.

- Endpoint: `GET /Navrhy/CreateScheduleProposal?projektId=…&zaznamId=…`
- Controller: [PmTracker.Web/Controllers/NavrhyController.cs](../../PmTracker.Web/Controllers/NavrhyController.cs) — `CreateScheduleProposal`
- Service: [PmTracker.Web/Services/RecordProposalService.cs](../../PmTracker.Web/Services/RecordProposalService.cs) — `ConfigureScheduleProposalEditor`
- View: standardní editor záznamu
  - `~/Views/Projekty/EditZaznamPage.cshtml` (page)
  - `~/Views/Projekty/EditZaznamModal.cshtml` (modal)
  - Tělo: [_EditZaznamForm.cshtml](../../PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml) → [_EditZaznamBasicPanel.cshtml](../../PmTracker.Web/Views/Projekty/_EditZaznamBasicPanel.cshtml)

---

## Co může uživatel v proposal editoru MĚNIT

| Pole | Editovatelné? | Poznámka |
| --- | --- | --- |
| **Termín ukončení** | **ANO** | Zvýrazněno světle zelenou (`.proposal-field-editable`). |
| Celý harmonogram (plán + skutečnost) | ANO | Záložka „Harmonogram" je odemčená. |
| Kategorie záznamu | **NE** | Zamčeno (`AllowBasicMetadataEdit=false`). |
| **Typ úkolu** | **NE** | Zamčeno. Nesmí se ani dynamicky odemknout při změně kategorie. |
| Stav úkolu | NE | Zamčeno. |
| Datum založení | NE | Zamčeno (i když by jinak bylo odemčené — viz `datumZalozeniLocked`). |
| Subsystém | NE | Zamčeno. |
| Vlastník | NE | Zamčeno. |
| Název, popis | NE | Zamčeno. |
| Externí vazby | NE | Panel skrytý (`ShowExternalTab=false`). |
| Spolupráce | NE | Panel skrytý (`ShowCollaborationTab=false`). |

Serverová logika nastavující tyto zámky je v `ConfigureScheduleProposalEditor`:

```csharp
model.AllowBasicMetadataEdit = false;   // zamkne kategorie/typ/stav/subsystém/…
model.AllowTermDeadlineEdit = true;     // povolí termín ukončení
model.CanEditRecord = false;            // zamkne zbytek editoru
model.CanEditScheduleFull = true;       // povolí celý harmonogram
model.ShowExternalTab = false;
model.ShowCollaborationTab = false;
```

---

## Klíčové atributy na `<form>`

Formulář nese tyto atributy, na které se JS spoléhá:

| Atribut | Hodnota | Význam |
| --- | --- | --- |
| `data-metadata-locked` | `"true"` v proposal editoru, jinak `"false"` | JS NESMÍ odemknout žádný select v `record-form-row-top`. |
| `data-is-proposal-editor` | `"true"` v proposal editoru | Informační příznak pro JS rozhodování (např. warning modaly). |
| `data-schedule-permission-mode` | `full` / `add-only` / `readonly` | Řídí editaci harmonogramu. V proposal je `full`. |

Nastavuje [_EditZaznamForm.cshtml](../../PmTracker.Web/Views/Projekty/_EditZaznamForm.cshtml).

---

## Pravidlo pro klientský JS

**JS NESMÍ odemykat pole, která server zamkl.**

Pokud formulář má `data-metadata-locked="true"`, žádná dynamická funkce (např.
`updateTaskTypeVisibility`) nesmí nastavit `select.disabled = false` pro pole
z řádku `[data-record-row-top]`. To platí i pokud se změní kategorie záznamu na
„Úkol" — v proposal editoru uživatel nemá změnit kategorii, ale kdyby se mu to
podařilo (bug), typ úkolu musí zůstat zamčený.

Implementováno v [recordEditor.js](../../PmTracker.Web/wwwroot/js/modules/recordEditor.js)
funkce `updateTaskTypeVisibility`:

```js
const metadataLocked = form.dataset.metadataLocked === "true";
typeSelect.disabled = !isTask || metadataLocked;
```

---

## Vizuální rozlišení

- `.proposal-field-editable` — světle zelené pozadí pro pole, která uživatel MŮŽE editovat v proposal módu (aktuálně Termín ukončení).
- `.proposal-field-changed` — světle oranžové pozadí pro pole, která byla v aktivním návrhu změněna (tooltip zobrazuje starou hodnotu).

Definice v [site.css](../../PmTracker.Web/wwwroot/css/site.css), sekce „proposal-field-*".

---

## Pokrytí testy

- [PmTracker.Tests.Unit/Projects/ProposalEditorLockedFieldsTests.cs](../../PmTracker.Tests.Unit/Projects/ProposalEditorLockedFieldsTests.cs)
  - Ověřuje, že `_EditZaznamForm.cshtml` nastavuje `data-metadata-locked` podle `AllowBasicMetadataEdit`.
  - Ověřuje, že `recordEditor.js` respektuje `data-metadata-locked` v `updateTaskTypeVisibility`.
  - Ověřuje, že `_EditZaznamBasicPanel.cshtml` používá `metadataLocked` pro disabled select.

---

## Pravidla pro úpravy

1. **Nikdy nepřidávej JS, který odemyká pole označená `data-metadata-locked="true"`.**
2. Pokud přidáváš nové pole do `record-form-row-top`, musí respektovat `metadataLocked`.
3. Pokud přidáváš nové pole, které má být editovatelné v proposal módu, přidej nový flag do `ZaznamEditViewModel` (např. `AllowXyzEdit`) a nikdy nepoužívej plošně `metadataLocked`.
4. Server i klient musí souhlasit — pokud server v proposal módu pole zamkne, klient to nesmí odemknout.
