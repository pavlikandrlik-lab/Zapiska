# Migrace modálního systému na pm-dialog (2E scope)

**Status:** Odloženo na Fázi 2E (po 2D). Dokumentováno 2026-04-19.

## Kontext

PM Tracker používá vlastní AJAX-based modální systém, který byl záměrně ponechán beze změny během Fáze 2D (migrace Views na pm-* wrappery).

**Komponenty současného systému:**

- [PmTracker.Web/Views/Shared/_ModalLayout.cshtml](../../PmTracker.Web/Views/Shared/_ModalLayout.cshtml) — overlay + container + floating-root + close button
- [PmTracker.Web/Views/Shared/_ModalFormActions.cshtml](../../PmTracker.Web/Views/Shared/_ModalFormActions.cshtml) — dvojice cancel+submit tlačítek (cancel migrován, submit ponechán)
- [PmTracker.Web/wwwroot/js/modules/modals.js](../../PmTracker.Web/wwwroot/js/modules/modals.js) — event delegace, fetch modálního obsahu přes `data-modal-url`
- [PmTracker.Web/wwwroot/js/modules/bootstrap.js:245](../../PmTracker.Web/wwwroot/js/modules/bootstrap.js) — close event handler přes `[data-modal-close]`
- CSS třídy `modal-overlay`, `modal-close`, `modal-content`, `modal--wide`, `modal--record-editor`, `modal--overflow-visible` (v site.css)

**33 views** obsahuje `modal-` CSS třídy nebo `data-modal-*` atributy.

## Proč nebyla migrace provedena v 2D

`pm-dialog` wrapper (Fáze 2C) je **thin wrapper nad `<gov-dialog>`** s API:
- Instance metody `.show()` / `.close()` na custom elementu
- Slot `title` pro titulek
- Default slot pro body

Přechod na `pm-dialog` vyžaduje **refaktor celé AJAX pipeline**, ne jen záměnu tagů:

### Blockery

1. **Lifecycle**: `gov-dialog` je custom element s instance metodami. Stávající `modals.js` manipuluje s `.modal-overlay` CSS třídami (`.is-open`, `.is-closing`). Adapter nebo refaktor.
2. **AJAX fetch flow**: `data-modal-url` → `fetch(url)` → vložit HTML do floating container → animovat vstup. S gov-dialog je nutno vložit HTML do `slot` a zavolat `.show()`.
3. **`data-ajax-submit` uvnitř gov-dialog**: formulářová validace, submit, error handling aktuálně pracují na `.modal-container` root. Uvnitř shadow DOM gov-dialog by bylo nutno přehodnotit (pravděpodobně zůstane stejné, protože formulář je light-DOM slot content).
4. **`modal-floating-root`**: kontejner pro floating pickers (person-picker, datetime-picker) nad modálem. Gov-dialog má vlastní z-index/positioning, floating-root musí být přemapovaný nebo nahrazený.
5. **Varianty modálu** (`modal--wide`, `modal--record-editor`, `modal--overflow-visible`) — musí být převedeny na gov-dialog `size` / `variant` atributy nebo řešeny přes CSS scope.

## Doporučený plán pro 2E

### Task 1 — Vytvoření `pm-modal` adapteru

Ne `pm-dialog` (už existuje, je to thin wrapper bez AJAX). **`pm-modal`** bude druhá vrstva: TagHelper nebo Razor partial, který:
- Přijímá stejný kontrakt jako `_ModalLayout` (ViewData["ModalTitleId"], variant, overflow)
- Interně používá `<gov-dialog>` pro shadow-DOM strukturu
- Exponuje kompatibilní `data-modal-*` atributy pro existující JS

### Task 2 — Refaktor `modals.js`

Migrovat z `modal-overlay` manipulace na gov-dialog `.show()/.close()` API. Zachovat kontrakty:
- `data-modal-url` → fetch → render
- `data-modal-close` → close
- `data-ajax-submit` → form submit → close on success

### Task 3 — Migrace 33 views

Nahradit `Layout = "_ModalLayout"` na `Layout = "_PmModalLayout"` (adapter). Po jednom view, smoke test každého.

### Task 4 — Odstranění legacy CSS

Smazat `.modal-overlay`, `.modal-close`, `.modal-container` atd. z site.css po dokončení Task 3.

## Zasažené views (33)

- **Shared**: `_ModalLayout`, `_ModalFormActions`
- **Projekty** (8): `AssignMeetingIdentifierModal`, `DeleteProjectModal`, `DeleteRecordModal`, `EditZaznamModal`, `NewMeetingModal`, `ProjectModal`, `AssignProjectRoleModal`, `AssignProjectSubsystemModal`, `AssignProjectSubsystemRoleModal`, `AddTeamMemberModal`
- **Nastaveni** (4): `PermissionModal`, `RoleModal`, `RolePermissionModal`, `UserRolesModal`
- **Osoby** (2): `AdPersonModal`, `ManualPersonModal`
- **Ciselniky** (1): `EditRow`
- **Jednani** (1): `AddMeetingParticipantModal`
- Plus partials s `data-modal-close` atributy (řešeno v adaptéru)

## Související bug / tech debt

- `_ModalFormActions.cshtml` má ponechaný submit button (`@Model.SubmitCssClass` s dynamickým `.btn` string) — migruje se spolu s modaly
- `_PageHeader.cshtml` back link má `data-record-editor-cancel` atribut, který je **interakce s modálním systémem** (AJAX cancel confirmation) — migrován v 2D, ale logika cancel flow v `bootstrap.js:224` je stále modal-dependent

## Priorita

Střední. Stávající modální systém funguje, ale je inkonzistentní s gov-design-system 4.2.9 principy (custom overlay vs. web component dialog). Vyřešit jako další větší fázi po 2D.
