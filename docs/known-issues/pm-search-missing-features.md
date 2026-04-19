# pm-search — chybějící features pro úplnou migraci layoutu

**Status:** Tech debt, vznik 2026-04-19 ve Fázi 2D. Není blokující.

## Kontext

`pm-search` TagHelper (Fáze 2D, Task 1) zatím nepokrývá všechny use-casy gov-form-search. Layout globálního hledání [PmTracker.Web/Views/Shared/_Layout.cshtml](../../PmTracker.Web/Views/Shared/_Layout.cshtml) proto dál používá přímé gov tagy.

## Chybějící features

### 1. `button-erase` slot (clear query button)

Gov-form-search podporuje slot `button-erase` pro tlačítko "smazat dotaz". Layout ho používá:

```razor
<gov-button slot="button-erase" size="s" color="primary" type="base">
    <gov-icon slot="icon-start" name="x" type="components"></gov-icon>
</gov-button>
```

**Navrhované API v pm-search:**
- `Erasable` (bool, default false) — pokud true, emituje `<gov-button slot="button-erase">` s `<gov-icon name="x">`.

### 2. `identifier` atribut na vnořeném gov-form-input

Layout používá `identifier="app-global-search-input"`, což gov-form-input používá pro generování HTML `id` a `for` na vnořeném `<label>`. Pm-search tuto property neexponuje.

**Navrhované API:**
- `Identifier` (string?, default null) — pass-through na gov-form-input.

### 3. `autocomplete` atribut na vnořeném gov-form-input

Globální search vypíná autocomplete (`autocomplete="off"`), aby prohlížeč nenabízel minulé hodnoty. Pm-search to neexponuje.

**Navrhované API:**
- `Autocomplete` (string?, default null) — pass-through atribut.

## Další nemigrované search pattery v codebase

Pro úplnost — tyto use-cases **ne**potřebují další features pm-search, ale mají JS/DOM závislosti které migraci blokují:

### `data-person-picker-input` (7 modálů)

Floating autocomplete picker — JS čte `.value`, poslouchá `input` event, očekává native `<input type="search">`. Vyžaduje refaktor `PmTracker.Web/wwwroot/js/modules/pickers.js`.

### `data-table-tools-search-input` (Osoby/Index, Projekty/_ProjectTeamTab)

Client-side tabulkový filter — `tableTools.js:199` kontroluje `instanceof HTMLInputElement`. Custom element gov-form-input check nesplní. Vyžaduje refaktor tableTools.js.

## Prioritizace

Low priority. Současný stav (gov-form-search v layoutu) plně funguje. Migrace je kosmetická a má smysl jen jako součást širšího refaktoru (např. zavedení konzistentního search pattern v celé aplikaci, nebo migrace tableTools.js na custom element API).
