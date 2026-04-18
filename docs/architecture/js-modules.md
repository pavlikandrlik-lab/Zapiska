# JS moduly — pravidla

## Limit 300 řádků / modul

Každý soubor v `PmTracker.Web/wwwroot/js/modules/*.js` má měkký limit 300 řádků
(skipBlankLines, skipComments). ESLint vypisuje warning, ne error.

Kontrola:
- Lokálně: ESLint s `PmTracker.Web/wwwroot/.eslintrc.json`
- CI: `scripts/check-file-sizes.sh`

## Struktura

Každý modul:

1. Jedna zodpovědnost — název souboru popisuje co dělá
2. Explicit exports — named exports, ne default
3. Pure tam, kde možné — helpery bez DOM/fetch isolovat
4. Side-effectful moduly jsou inicializované z `bootstrap.js`

## Bundle

Aplikace v prohlížeči načítá **jen `site.bundle.js`**. Moduly v `/modules/` jsou
pro vývoj / testování. Při změně v modulu synchronizuj do bundlu — viz
[synchronizace bundlu](../specs/modal-close-guard.md).

## Event bus

`eventBus.js` poskytuje:

- `appEventBus.on(selector, event, handler)` — delegation helper
- Automatický adaptér: gov-click → nativní click

Existující `addEventListener("click", ...)` posluchače fungují s `<gov-button>`
beze změny díky adaptéru.

## Testování

- Čistě funkční helpery (`utils.js`, `snippets.js`) — unit test přes node
- DOM/fetch — Playwright E2E
- Event handling — Playwright (spuštěný live)
