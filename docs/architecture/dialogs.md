# Dialogy (gov-dialog / pm-dialog)

## Přehled

PM Tracker používá **gov-dialog** (gov-design-system 4.2.9 Web Component)
pro všechny modální dialogy. Thin wrapper `pm-dialog` (Fáze 2C) poskytuje
standalone použití s title slotem, ale pro aplikační modaly s AJAX obsahem
se používá **`_ModalLayout.cshtml`** (Fáze 2E).

## Modální systém (2E)

**Kanonický entry point:** `PmTracker.Web/Views/Shared/_ModalLayout.cshtml`.
Všech 18 modal views nastavuje `Layout = "_ModalLayout"` a používá ViewData
kontrakt + render body.

### Struktura

```
<div id="modal-root">              <!-- _Layout.cshtml -->
    <gov-dialog
        open="true"
        block-close="true"
        block-backdrop-close="true"
        data-modal-container
        data-modal-variant="default|wide|record-editor"
        data-modal-overflow-visible="true|false"
        aria-labelledby="..."
        tabindex="-1">
        <div class="modal-content">
            @RenderBody()  <!-- Obsah jednotlivého modal view -->
        </div>
    </gov-dialog>
</div>
<div id="floating-panel-root" aria-hidden="true">
    <!-- Person/datetime pickery floatují zde, NE uvnitř gov-dialog shadow DOM -->
</div>
```

### Kontrakt ViewData

| Klíč | Hodnota | Použití |
|---|---|---|
| `ModalTitleId` | `string` | `id` pro `<h2>` v body; `aria-labelledby` cíl |
| `ModalVariant` | `"default" \| "wide" \| "record-editor"` | CSS max-width: default ~52rem, wide 1100px, record-editor 1280px/92vh |
| `ModalOverflowVisible` | `bool \| "true"` | `overflow: visible` na `.modal-content` — pro office-search-panel a floating pickery přečnívající mimo |

Jednotlivé view **si renderují vlastní `<h2 id="@ModalTitleId">`** v body.
Gov-dialog title slot není v layoutu naplněný (záměr — minimální invazivnost
18 views).

### Close flow (Fáze 2E pattern)

`block-close="true"` + `block-backdrop-close="true"` zabraňují **gov-dialog
self-close** na Esc a backdrop click. Místo toho:

1. **Esc klávesa** → `bootstrap.js:handleDocumentOverlayKeydown` → `requestRecordEditorModalClose(activeElement)` → `promptRecordEditorDiscard(form)` (dirty-check) → if confirmed: `closeModal()`
2. **Backdrop click** → gov-dialog zachytí event v shadow DOM a díky `block-backdrop-close` nepropaguje close. (Budoucí: wire `gov-close` nebo specific event.)
3. **Vestavěný X button (gov-dialog top-right)** → emituje `gov-close` event → `bootstrap.js:handleGovCloseEvent` → `requestRecordEditorModalClose` (dirty-check flow)
4. **`[data-modal-close]` button** (v `_ModalFormActions.cshtml` Cancel + per-view close buttons) → `bootstrap.js` click delegation → `requestRecordEditorModalClose`
5. **`[data-record-editor-cancel]`** (page-level editor Back link) → `requestRecordEditorPageCancel` → dirty-check → `window.location.assign(backUrl)`
6. **Outbound link click** (menu, breadcrumbs) — pokud existuje dirty page-level editor: `bootstrap.js:maybeGuardOutboundNavigation` → dirty prompt → navigate-or-stay

**Žádný z těchto flow nepoužívá nativní browser `beforeunload` dialog.**
`handleWindowBeforeUnload` je záměrně no-op (user preference — app-level
dialog je jediný, který se zobrazuje).

### JS lifecycle (modals.js)

```
openUrlModal(url, trigger)
  ↓
fetch HTML → insert do #modal-root → activateInsertedGovDialog()
  ├── dialog.setAttribute("open", "true")    // explicit pro pre-hydration
  └── dialog.show() v try/catch              // po upgrade gov-dialog

closeModal()
  ↓
dialog.removeAttribute("open") + dialog.close() v try/catch
  → modalRoot.innerHTML = "" → focus na trigger
```

### AJAX form submit

`ajax.js:initModalAjaxSubmit` detekuje modal context přes
`target.closest("gov-dialog[data-modal-container]")`. Po úspěšném submitu
zavře modal (via `closeModal`) a refreshuje view partials.

### Floating pickery

Person-picker a datetime-picker panely mountují do **globálního
`#floating-panel-root`** v `_Layout.cshtml`, NE uvnitř gov-dialog shadow DOM
(shadow DOM vs. positioning kolize). `ui.js:getFloatingLayerRoot` vrací
`#floating-panel-root` vždy — fallback on-demand creation je pro edge cases.

### 18 modal views

- **Osoby**: AdPersonModal, ManualPersonModal
- **Projekty**: AddTeamMemberModal, AssignMeetingIdentifierModal, AssignProjectRoleModal, AssignProjectSubsystemModal, AssignProjectSubsystemRoleModal, DeleteProjectModal, DeleteRecordModal, EditZaznamModal, NewMeetingModal, ProjectModal
- **Nastaveni**: PermissionModal, RoleModal, RolePermissionModal, UserRolesModal
- **Jednani**: AddMeetingParticipantModal
- **Ciselniky**: EditRow

### Varianty pm-button v `_ModalFormActions`

`ModalFormActionsViewModel.SubmitVariant: PmButtonVariant` (enum):
- `Primary` (default) — `<pm-button variant="Primary">` — modrá solid
- `Destructive` (např. `DeleteRecordModal`) — `<pm-button variant="Destructive">` — červená solid

Cancel je vždy `<pm-button variant="Secondary" data-modal-close="true">`.

### Vztah k `pm-dialog` wrapperu (Fáze 2C)

`pm-dialog` (`PmDialogTagHelper.cs`) je standalone thin wrapper — renderuje
`<gov-dialog>` s volitelným title slot pro neapplikační použití (StyleGuide
ukázka, potvrzovací dialogy bez AJAX). Pro aplikační modaly se používá
`_ModalLayout` systém (tato sekce).

## `pm-dialog` wrapper (Fáze 2C)

Thin wrapper nad `<gov-dialog>`. Modální dialog s titulkem, tělem a volitelnými akcemi.

### Použití

```razor
<pm-button variant="Primary" onclick="document.getElementById('confirm-delete').show()">Smazat</pm-button>

<pm-dialog id="confirm-delete" title="Potvrdit smazání">
    <p>Opravdu chcete smazat tento záznam?</p>
    <pm-button variant="Destructive" onclick="document.getElementById('confirm-delete').close()">Smazat</pm-button>
    <pm-button variant="Secondary" onclick="document.getElementById('confirm-delete').close()">Zrušit</pm-button>
</pm-dialog>
```

### API

| Property | Typ | Popis |
|---|---|---|
| `Id` | `string` | HTML id (nutné pro otevření/zavření z jiných elementů). |
| `Title` | `string` | Titulek (renderuje se jako `<h3 slot="title">`). |
| `Open` | `bool` | Defaultně otevřený (přidá `open="true"`). |

Dětský obsah jde do default slotu (tělo dialogu).

### Otevření/zavření z JS

gov-dialog web komponenta má metody `.show()` a `.close()`:

```javascript
const dlg = document.getElementById('confirm-delete');
dlg.show();    // otevřít
dlg.close();   // zavřít
```

### Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `id` | HTML id |
| `title` | `<h3 slot="title">` |
| `open` | `open="true"` |
| child content | default slot |

## Související

- `docs/architecture/buttons.md` — `pm-button` varianty (Primary / Secondary / Destructive / Ghost)
- `docs/superpowers/plans/2026-04-19-faze-2e-modaly.md` — implementační plán
- `docs/superpowers/logs/2026-04-19-faze-2e-manual-smoke.md` — 18 views manual checklist
