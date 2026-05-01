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

## Loadování v prohlížeči

`_Layout.cshtml` načítá `~/js/site.js` jako **ES module** (`<script type="module">`).
Žádný build / bundling — browser sám stáhne `site.js` a všechny imported moduly.

```
site.js
  └─ bootstrap.js                    (top-level entry)
       ├─ side-effect imports        (eventBus, externiOdkaz, vyjadreni, ...)
       └─ named imports              (navigation, modals, schedule, ...)
```

`site.bundle.js` v repu zůstává jako historický artefakt z předchozí éry — je
**netracking** v Layoutu a **netracking** ve sw cache. Nemažeme ho jen kvůli
git history a pro případnou reverzi, ale aktivně se NEpoužívá.

## Side-effect importy v bootstrap.js (KRITICKÉ)

Některé moduly registrují globální listenery / `window.pm*` / custom elementy
v top-level kódu (legacy IIFE pattern nebo `customElements.define` na top of
file). **Bez explicitního importu jejich kód nikdy neběží.**

Při přechodu z `site.bundle.js` (kde tyto moduly byly inline-bundlovány) na
modular `site.js` (ES imports) je nutné každý takový modul registrovat jako
**side-effect import** v bootstrap.js:

```js
// PmTracker.Web/wwwroot/js/modules/bootstrap.js (head of file)
import "./eventBus.js";                          // gov-click adapter
import "./externiOdkaz/sync.js";                  // pmExterniOdkazSync
import "./vyjadreni/chatModal.js";                // pmChatModal
import "./vyjadreni/chatModalDragDrop.js";
import "./vyjadreni/chatModalReharvest.js";
import "./harmonogram/manualKroky.js";            // pmManualKroky
import "./schedule-feature-c/toggle-rezim.js";    // pmScheduleFeatureC
import "./vyzvy/index.js";                        // pmVyzvy.bootstrap
import "./vyzvy/panelController.js";
import "./vyzvy/switchController.js";
import "../components/pm-chat-stepper/pm-chat-stepper.js"; // <pm-chat-stepper> CE
```

**Anti-vzor** který v dubnu 2026 položil celý UI: side-effect modul existuje
v `/wwwroot/js/modules/`, ale nikdo ho neimportuje. Při migraci z bundle.js
zapomenuto. Symptom: tlačítka na stránce nereagují, žádný JS error v konzoli,
žádný hint kde je problém.

**Pravidlo:** každý nový modul co dělá `customElements.define`, `document.addEventListener`,
nebo `window.pmX = ...` v top-level kódu **MUSÍ být přidán jako side-effect import
do bootstrap.js**. Reviewer by měl zkontrolovat při review nového modulu.

**Detekce:** test [playwright-comprehensive-smoke.js](/tmp/playwright-comprehensive-smoke.js)
ověří v Stage 5 že všechny očekávané `window.pm*` jsou definovány. Pokud
nějaký nový modul přidáš, doplň ho do testu.

## Event bus

`eventBus.js` poskytuje:

- `installGovClickAdapter()` — auto-instaluje listener `gov-click` co re-dispatchuje
  nativní `click` na stejném targetu
- `appEventBus.on(selector, event, handler)` — delegation helper (ESM export, ne
  window namespace; modul musí explicit import)

**Proč adapter existuje:** `gov-design-system 4.x` u `<gov-button>` interně volá
`event.stopPropagation()` na nativním `click` eventu a místo toho emituje vlastní
`gov-click` custom event. Bez adapteru by žádný `document.addEventListener('click', ...)`
nezachytil kliky na gov-button. Adapter překládá gov-click → native click → existující
delegated handlery fungují bez změny.

## init() volání v runInitializers

Některé legacy moduly mají dvoufázový lifecycle:

1. **Top-level kód** (registruje `window.pmX`, custom elementy, atd.)
2. **`pmX.init()`** volaný po DOMContentLoaded (našel selektory v DOM, navázal handlery)

Po side-effect importu fáze 1 proběhla, ale fáze 2 musí explicitně volat bootstrap.
V `runInitializers` v bootstrap.js:

```js
() => window.pmExterniOdkazSync?.init?.(),
() => window.pmChatModal?.init?.(),
() => window.pmManualKroky?.init?.(),
() => window.pmScheduleFeatureC?.init?.(),
```

Optional chaining — modul se může v testovém prostředí nenahrát. Pokud `pmX`
neexistuje, init se přeskočí bez chyby.

## Testování

- Čistě funkční helpery (`utils.js`, `snippets.js`) — unit test přes node
- DOM/fetch — Playwright E2E
- Event handling — Playwright (spuštěný live)
- **Bootstrap chain integrita** — `playwright-comprehensive-smoke.js` Stage 5
  ověří že všechny `window.pm*` moduly jsou loaded
