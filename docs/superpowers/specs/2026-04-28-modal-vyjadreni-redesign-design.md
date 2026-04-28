# Modal Vyjádření a termíny — redesign (širší modal, native gov-stepper, cursor-tracked drag)

**Stav:** schváleno k implementaci (2026-04-28)
**Závisí na:** [2026-04-21-servicedesk-vytezovani-vyjadreni-design.md](2026-04-21-servicedesk-vytezovani-vyjadreni-design.md) §7.3-7.4 (původní zadání chování stepperu — zde se finalizuje), [2026-04-28-nes-vyjadreni-a-4-datumy-design.md](2026-04-28-nes-vyjadreni-a-4-datumy-design.md) (návazný — NES skip, krok 1, matice).

## Goal

Přepracovat modal „Vyjádření a termíny" tak, aby:
1. Byl širší (≥ 1600px na velkých monitorech), responzivní.
2. Stepper byl **native gov-stepper** (oprava oval bug u multi-line textu).
3. Stepper zobrazoval pouze kroky relevantní pro typ záznamu.
4. Kroky a vyjádření tvořily jeden scrollovatelný container, opticky dvě sloupce.
5. Drag&drop byl invertován — uživatel táhne **krok** (ne bublinu), krok sleduje kurzor a magneticky snapuje na nejbližší vyjádření.

## Architecture

Layout je CSS Grid s `position: sticky` na kroky-column elementech. Drag&drop je vanilla JS s `requestAnimationFrame`-driven cursor tracking; magnetický snap je propočítaný `getBoundingClientRect` výpočet nejbližší bubliny vůči kurzoru. Stepper komponenta = native `<gov-stepper>` + `<gov-stepper-item>` z gov-design-system 4.x (žádný custom element wrapper).

## §1 — Layout: width, grid, scroll model

### Modal width

```css
.pm-chat-modal {
    width: 95vw;
    max-width: min(95vw, 1800px);  /* caps at 1800 na velkých monitorech */
}
```

Future-note: pravděpodobný pozdější přechod na 1700px breakpoint + 2200px max-width pro 4K obrazovky — držet code structure flexibilní, ne hardcodovat hodnoty na desítkách míst.

### Grid breakpointy

| Viewport | Grid | Komentář |
|---|---|---|
| ≥ 1600px | `70fr 30fr` | velké monitory — vyjadreni dominantní |
| 1100–1599px | `60fr 40fr` | menší monitory — kroky širší pro čitelnost |
| < 1100px | stack vertikálně (1fr) | mobile — vyjadreni nahoře, kroky pod nimi |

### Scroll model

- Body = jeden scrollovatelný container (`overflow-y: auto`)
- Vyjadreni column = standard flow content (sequence bublin)
- Kroky column = `position: sticky` s offsetem propočítaným podle Y-pozice bound bubliny
- **Single scrollbar** pro celý body (žádný separátní scroll na sloupci kroků)
- Když user scrolluje, body se posouvá; sticky kroky se vizuálně shodují s pozicí bublin díky offsetu

## §2 — Stepper komponenta: native gov-stepper

**Replace** `<pm-chat-stepper>` (custom element wrapper) za **native `<gov-stepper>` + `<gov-stepper-item>`**:

```html
<gov-stepper size="m">
  <gov-stepper-item color="success" identifier="krok-1">
    <span slot="prefix">1</span>
    <span slot="headline">Příprava zadání dodavateli</span>
    <span slot="content">15.04.2026</span>
  </gov-stepper-item>
  <gov-stepper-item color="warning" identifier="krok-3">
    <span slot="prefix">3</span>
    <span slot="headline">Odeslání zadání dodavateli</span>
    <span slot="content">17.04.2026 (manuál)</span>
  </gov-stepper-item>
  <!-- ... -->
</gov-stepper>
```

**Color states** (gov-stepper-item nativně validuje `["PRIMARY","NEUTRAL","ERROR","SUCCESS","WARNING"]`):
- `color="success"` (zelená) — krok plněn z **automatu** (auto-fill z harvestu)
- `color="warning"` (oranžová) — krok plněn **manuálně** uživatelem
- `color="error"` (červená) — krok **bez vyjádření** (zatím nepřiřazený, leží v bufferu nebo prázdný)

**Bez oval bugu**: native gov-stepper-item má kolečko s číslem v separátní `__prefix` slot s vlastním fixed sizing (component handluje wrap interně). Custom CSS `pm-chat-step__poradi` 1.5rem × 1.5rem `border-radius: 50%` (který se protahuje při wrap) **se odstraňuje**.

**Smaže se:**
- `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/` (celý folder)
- `PmTracker.Web/wwwroot/css/components/pm-chat-stepper.css`

## §3 — Step filtering per typ záznamu

Per matice z [2026-04-28-nes-vyjadreni-a-4-datumy-design.md §1](2026-04-28-nes-vyjadreni-a-4-datumy-design.md), rozšířená o ruční dropdown kroky:

| Typ | Auto-fill kroky | Ruční dropdown kroky | Celkem stepper items |
|---|---|---|---|
| **NES** | — | — | **0** (modal bez stepperu, hotovo z 2026-04-28) |
| **PMP** | 1, 3, 4 | 2, 5 | **5** (`{1, 2, 3, 4, 5}`) |
| **PNF** | 1, 6, 7, 10 | 8, 9 | **6** (`{1, 6, 7, 8, 9, 10}`) |

Vizuální odlišení per krok:
- Auto-fill krok navázaný na vyjádření → `color="success"`
- Ruční krok navázaný na vyjádření → `color="warning"`
- Krok bez vyjádření → `color="error"` + leží v **bufferu** (viz §6)

## §4 — Drag&drop: cursor-tracking + magnetic snap

### Inverze směru tahu

Současná implementace: user táhne **bublinu** na **krok** (drop target = krok). Aktualizace: user táhne **krok** na **bublinu** (drop target = bublina).

### Drag flow

1. **Drag start** (mouse-down na `<gov-stepper-item>`):
   - Kurzor: `grabbing`
   - Krok element dostane `data-dragging="true"`
   - Pozice se uloží jako original (pro revert při neplatném drop)

2. **Drag in progress** (mousemove):
   - Krok element se plynule pohybuje s kurzorem **po ose Y** (`transform: translateY(...)`)
   - Horizontální pohyb kurzoru ignorován (krok stays v pravém sloupci)
   - **Magnetic snap**: každý mousemove event spočítá nejbližší bublinu vzhledem ke `cursor.clientY` (přes `getBoundingClientRect` všech `[data-bubble]`); krok target Y = `bubble.top + bubble.height/2 - krok.height/2`
   - Smooth animation: `transform: translateY(targetY) ease-out 60ms`
   - Vizuální highlight bubliny u kurzoru: `[data-bubble].drop-target` (oranžový outline)
   - **Souběžné scrollování**: user může scrollovat body kolečkem myši — tracking pokračuje, krok zůstává nad kurzorem

3. **Drag end** (mouse-up):
   - Krok zůstane na poslední snap pozici (Y-center cílové bubliny)
   - Detach od kurzoru (`data-dragging="false"`)
   - **Validace** před commit:
     - **Chronologie**: krok N nemůže být na bublině s `Datum` < `bound bubliny kroku N-1` ani > `bound bubliny kroku N+1`. Při poručení → toast „Krok {N} nelze přiřadit zde — porušila by se chronologie kroku {konflikt}." + revert animation (krok zpět na original pozici)
     - **1:1**: pokud target bublina už má jiný krok → toast „Bublina již má přiřazený krok {K}. Odpojte ho nejdřív." + revert
   - Pokud validace projde → POST `/Vyjadreni/HarmonogramVazba/Create` (nebo Update pokud krok už měl binding) + permanent commit pozice

### Speciální případ: krok 10 PNF (archiv)

Pokud existuje vyjádření obsahující K10 frázi „Záznam byl převeden do archivu." → krok 10 je **automaticky pinned** na tu bublinu, drag start zablokován (cursor: not-allowed, no-op na mouse-down). Pokud archivní bublina chybí → krok 10 leží v bufferu jako každý jiný neprassoný krok.

## §5 — Visual linky (REMOVED)

Section 7.5 původního specu (SVG hover linky krok↔bublina) **se neimplementuje**. Sticky alignment kroku s jeho bound bublinou poskytuje dostatečnou vizuální korelaci bez nutnosti SVG draw.

## §6 — Buffer pro nepřiřazené kroky

Kroky bez bindingu (= žádná vazba na bublinu) leží v **bufferu**:

- Pozice: **vpravo nahoře v modalu**, nad sticky alignment area pravého sloupce
- Vizuálně: malý box `gov-stepper` s items color="error", header text „Nepřiřazené kroky"
- Kroky odsud táhne user dolů na bublinu (drag start funguje stejně jako z přiřazené pozice)
- Po drop a úspěšný binding → krok zmizí z bufferu, objeví se sticky-aligned vedle své bound bubliny
- Pokud user odpojí krok (clear binding tlačítkem) → krok se vrátí do bufferu

## §7 — Sticky alignment math (per krok)

Pro každý krok s bindingem propočítat:
- `bubble.boundingRect.top` (relativní k body containeru)
- `krok.style.position = "sticky"`
- `krok.style.top = bubble.top + bubble.height/2 - krok.height/2 - bodyContainer.scrollTop`

Recompute trigger:
- ResizeObserver na body container
- Scroll event listener (debounced)
- Mutation observer na bubliny list (když přibyde/zmizí bublina)

Implementačně: vanilla JS modul `stepperSticky.js` + CSS variables pro sticky offsety per krok element.

## Tech Stack

Vanilla JS, native gov-design-system 4.x (`gov-stepper`, `gov-stepper-item`, `gov-toast`), CSS Grid + position sticky, existing backend (POST /Vyjadreni/HarmonogramVazba/{Create|Update|Delete} beze změny).

## Implementační dopad

### Nové soubory
| Soubor | Účel |
|---|---|
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js` | Cursor-tracked drag s magnetic snap k vyjadreni |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperSticky.js` | Sticky position propočet per krok |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js` | Buffer area handling (nepřiřazené kroky) |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml` | Přepsat stepper section: `<pm-chat-stepper>` → `<gov-stepper>` + `<gov-stepper-item>`, nový buffer slot, conditional draggable na bublinkách (zrušeno) |
| `PmTracker.Web/wwwroot/css/components/chat-modal.css` | Width 1800px max, grid breakpointy 1600/1100, sticky alignment CSS |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js` | Refaktor — bubliny už nejsou draggable, drag drive z kroku |

### Smazané soubory
| Soubor | Důvod |
|---|---|
| `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/` (celý folder: `pm-chat-stepper.js`, `chronology.js`, `buffer.js`) | Custom element nahrazený nativním `<gov-stepper>` |
| `PmTracker.Web/wwwroot/css/components/pm-chat-stepper.css` | Custom stepper CSS — odpadá s native gov-stepper |

### ViewModel
- `VyjadreniModalViewModel` — přidat `IReadOnlyList<int> RelevantStepsForType` (filtrované per typ záznamu); `Kroky` zůstává s plnou sadou (server-side filter v builderu)
- `VyjadreniModalViewModelBuilder` — filtrovat `Kroky` per `TiketTyp` (PMP → {1,2,3,4,5}, PNF → {1,6,7,8,9,10})

## Testing

- **Unit** `StepperFilteringTests` — ViewModel builder filtruje kroky per typ záznamu
- **Architecture guard** `ChatModalUsesNativeGovStepperTests` — _ChatModal.cshtml obsahuje `<gov-stepper>` (ne `<pm-chat-stepper>`)
- **Architecture guard** `ChatModalLayoutBreakpointsTests` — CSS obsahuje 1600px / 1100px media queries + max-width 1800px
- **Manuální smoke** (po implementaci):
  - Otevřít modal pro PMP záznam — stepper má 5 items, kroky 2 a 5 jsou color="error" (unbound)
  - Drag krok 3 vertikálně — krok smooth follow kurzor + snap na bublinu
  - Scroll body během dragu — kroky scrollují s vyjádřeními (sticky), drag pokračuje
  - Mouse-up nad bublinou → binding committed, krok teď color="success" + sticky aligned vedle bubliny
  - Pokus o drop na bublinu s jiným krokem → toast + revert
  - Resize window → grid breakpoints fungují (1600/1100/stack)

## Otevřené otázky

Žádné. Brainstorming Q1-Q3 + odpovědi v této session zodpověděny.

## Zodpovědnost

- **Implementace**: Claude (postupně, bez agentů — per user pattern)
- **Validace**: Ing. Andrlík (po deploy + smoke)
- **Spec review**: Ing. Andrlík (po napsání plánu)

## Risk areas

- **Cursor-tracked drag s magnetic snap** je nestandard implementace — vanilla JS DOM manipulation + getBoundingClientRect math. Vyžaduje pečlivé testování na různých velikostech viewport.
- **Sticky alignment** je tricky — když bubliny mají různou výšku (multi-line popisy), kroky se musí přesnit aligned. Potenciální flicker na scroll edges.
- **Souběžné scroll + drag** vyžaduje `passive: false` na scroll event listener během dragu pro správné chování.

Tyto risk areas jsou v scope iterace 1 — pokud se ukáže problém, dolaďuje se po smoke test feedback.
