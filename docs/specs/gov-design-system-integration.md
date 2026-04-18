# Gov Design System — integrace Web Components

Gov Design System je hostován lokálně v `PmTracker.Web/wwwroot/lib/gov-design-system/`.
Komponenty jsou registrovány přes `core.esm.min.js` (tagy `<gov-*>` jsou dostupné globálně).

## Skutecne nahrazene Web Components

| Komponenta | Umisteni | Puvodni markup | Novy markup |
|---|---|---|---|
| `gov-message` | `Views/Shared/_Layout.cshtml:149` | `<div class="alert alert-error">` | `<gov-message color="error">` |
| `gov-message` | `Views/Shared/_ScheduleBlock.cshtml:41,47` | `<div class="alert warning">` | `<gov-message color="warning">` |
| `gov-message` | `Views/Shared/_ScheduleBlock.cshtml:53` | `<div class="alert info">` | `<gov-message color="primary">` |
| `gov-message` | `Views/Projekty/_EditZaznamBasicPanel.cshtml:104` | `<div class="alert danger">` | `<gov-message color="error">` |
| `gov-message` | `Views/Projekty/_EditZaznamBasicPanel.cshtml:156` | `<div class="alert warning">` | `<gov-message color="warning">` |
| `gov-message` | `Views/Projekty/AssignMeetingIdentifierModal.cshtml:36` | `<div class="alert danger">` | `<gov-message color="error">` |
| `gov-message` | `Views/Search/Index.cshtml:46` | `<p class="alert alert-info">` | `<gov-message color="primary">` |
| `gov-tag` | `Views/ProjectDashboard/_RecordsPanel.cshtml:71` | `<span class="badge badge-danger/warning/caution">` | `<gov-tag color="error/warning/neutral" size="s">` |
| `gov-tag` | `Views/Projekty/_ProjectScheduleTab.cshtml:90` | `<span class="badge schedule-badge-ok/late">` | `<gov-tag color="success/error" size="s">` |

### gov-message — atributy

- `color`: PRIMARY, NEUTRAL, ERROR, SUCCESS, WARNING (lowercase v HTML atributu)
- `type`: subtle (vychozi), bold
- Renderuje role="status", obsah pres slot

### gov-tag — atributy

- `color`: PRIMARY, NEUTRAL, ERROR, SUCCESS, WARNING
- `size`: s, m (vychozi)

## Zamerne ponechane CSS aproximace

### gov-theme-switch — NEPOUZIT

Duvod: `gov-theme-switch` podporuje pouze prepinani light/dark (2 stavy). Nase implementace
(`wwwroot/js/modules/theme.js`) implementuje trojstavovy rezim (light/dark/auto) s:

- Persistenci do cookie `pmtracker.theme.mode`
- Responzi na `prefers-color-scheme` media query pri rezim "auto"
- Vlastnim cookie klicovym nazvem (odlisnym od `data-theme`, ktery pouziva gov-theme-switch)

Nahrazeni by ztratilo auto rezim a rozbilo cookie persistenci. Ponechana custom CSS aproximace.
Layout obsahuje komentár s odůvodněním (viz `_Layout.cshtml:83`).

### gov-form-switch — NEPOUZIT pro filtry

Duvod: JS vrstva (`filters.js`, `pageSwitchers.js`) pouziva:

```js
root.querySelector('[data-filter-key="aktivni"]')
// vraceny element je testovan: input instanceof HTMLInputElement
```

`gov-form-switch` renderuje interni `<input type="checkbox">` pres shadow DOM. Shadow DOM
neni pristupny pres `querySelector` z light DOM — selector by nasel `gov-form-switch` element
(ne `HTMLInputElement`), instanceof check by selhal a filtrace by prestala fungovat.

Dotcene soubory:
- `Views/Projekty/Index.cshtml` — skryt hotove, skryt smazane (JS: pageSwitchers.js)
- `Views/Projekty/ProjectModal.cshtml` — PouzivatIdentJednani (form submit, ne JS filter)
- `Views/Projekty/_ProjectRecordsTab.cshtml` — groupBySubsystem, aktivni, mine (JS: filters.js)

### Tlacitka (.btn, .icon-btn)

113 vyskytu. JS click handlery (filter/tabs/modals) jsou navazane na `.btn` tridu a `data-*`
atributy. Refaktoring by vyzadoval prepsani vsech handleru — mimo scope tohoto ukolu.

### Modal (.modal-overlay)

Vlastni orchestrace pres `modals.js`. `gov-dialog` ma jinou API a life-cycle hooks.

### Kalendar / date picker

Uzivatel explicitne pozadoval zachovani.

### Rich text editor Quill

Treti-straninna knihovna. Nema gov ekvivalent.

### Chip s tooltipem

Slozena komponenta s vlastni JS logikou. Nahrazeni by vyzadovalo kompletni refaktoring.

## Pravidlo pro nova view elementy

Pri pridavani novych UI prvku preferovat gov Web Components:

- Alert / notifikace: `<gov-message color="error|warning|success|primary">`
- Badge / stitek: `<gov-tag color="..." size="s">`
- Jednoduchy checkbox (bez JS filtrace): `<gov-form-switch>`
- Tlacitko (nove): zvaz `<gov-button>` misto `.btn` pokud nema existujici JS handler
- Ikona: `<gov-icon name="..." type="components">`
