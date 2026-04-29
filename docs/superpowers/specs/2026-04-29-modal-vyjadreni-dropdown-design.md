# Modal Vyjádření a termíny — dropdown approach (replace drag&drop)

**Stav:** schváleno k implementaci (2026-04-29)
**Závisí na:** [2026-04-28-modal-vyjadreni-redesign-design.md](2026-04-28-modal-vyjadreni-redesign-design.md) — drag&drop redesign který selhal v praxi (3 bugs reportované 2026-04-29)

## Goal

Nahradit drag&drop binding (krok ↔ bublina) za **dropdown selector v každé bublině**. Drag&drop má v praxi 2 nepřekonatelné problémy:
- `position: fixed` + viewport-relative coordinates → krok se „vyskočí z modalu" při scroll
- Shadow-DOM gov-stepper-item event retargeting → „Odpojit" tlačítko nereaguje

User volba (2026-04-29): dropdown je robustní, accessible, mobile-friendly.

## Architecture

Dropdown na každé bublině vyjadřeni s krok-options. Validace (1:1, chronologie) přes pre-computed `IsDisabled` na serverové straně. Po každé změně → POST + `refreshModal()` (re-fetch partial). Stepper vpravo zachován jako **informační dashboard** (sticky aligned + click-to-scroll na bound bublinu).

## §1 — Layout v bublině

```html
<li class="pm-chat-bubble" data-bubble data-vyjadreni-id="..." data-datum="...">
    <div class="pm-chat-bubble__meta">
        <span class="pm-chat-bubble__autor">...</span>
        <span class="pm-chat-bubble__tym">...</span>
        <time class="pm-chat-bubble__datum">...</time>
        <!-- NEW: krok selector vpravo nahoře -->
        <div class="pm-chat-bubble__krok">
            @if (assigned) {
                <gov-tag color="@assignedColor" size="s">Krok @poradi</gov-tag>
                @if (canEdit && !krok.IsPinned) {
                    <button class="pm-chat-bubble__krok-clear" data-clear-bubble-binding>×</button>
                }
            } else {
                <gov-form-select size="s" data-bubble-step-selector
                                 data-vyjadreni-id="@id"
                                 options="@Json.Serialize(stepOptions)">
                </gov-form-select>
            }
        </div>
    </div>
    <div class="pm-chat-bubble__popis">...</div>
</li>
```

**Color states pro badge** (=stepper barvy):
- `success` (zelená) — auto-fill binding (predikát fráze ze ServiceDesku)
- `warning` (oranžová) — manuál binding (user explicit choice)

## §2 — Stepper vpravo (informační dashboard)

Beze změny layout (sticky aligned column). Změny:

- **Bez drag handlers** — gov-stepper-item není draggable, žádný `cursor: grab`, žádné mousedown listenery
- **Click-to-scroll**: klik na krok → smooth scroll modalu k jeho bound bublině:
  ```javascript
  step.addEventListener('click', () => {
      const vyjadreniId = step.getAttribute('data-current-vyjadreni-id');
      if (!vyjadreniId) return;
      const bubble = root.querySelector(`[data-bubble][data-vyjadreni-id="${vyjadreniId}"]`);
      if (bubble) bubble.scrollIntoView({ behavior: 'smooth', block: 'center' });
  });
  ```
- **„Odpojit" tlačítko** přímo v krok content slot — funkční (paralelní cesta k ✕ na badge bubliny)
- **Buffer** pro nepřiřazené kroky **odstraněn** — kroky bez bindingu jsou implicitní v dropdown options. Jednodušší DOM.

## §3 — Step options validace (1:1 + chronologie)

Per bublina B se sestaví options pro každý krok K relevantní pro typ záznamu:

| Stav | Disabled? | Tooltip |
|---|---|---|
| K již bound této bublině B | n/a | option neexistuje (je zobrazen jako badge) |
| K bound jiné bublině B' | ano | „Přiřazen bublině z {B'.Datum:dd.MM.yyyy}" |
| K není bound nikomu, K bind do B by porušilo chronologii | ano | „Porušila by se chronologie kroku {N±1}" |
| K není bound, chronologie OK | ne | — |

**Chronologie**: krok N na bublině B je validní, právě když:
- Pokud existuje bound krok N-1 (nižší pořadí v aktivní sadě) na bublině B': `B.Datum >= B'.Datum`
- Pokud existuje bound krok N+1 (vyšší pořadí v aktivní sadě): `B.Datum <= B'.Datum`

**Special case krok 10 PNF (archiv)**: pokud existuje bublina s K10 frází, K10 je auto-pinned na ní → V dropdown options jiných bublin K10 NENÍ vůbec; v té archivní bublině badge bez ✕.

## §4 — Frontend handler

`bubbleStepSelector.js`:

```javascript
function attachBubbleStepSelectors(root) {
    root.querySelectorAll('[data-bubble-step-selector]').forEach((select) => {
        select.addEventListener('gov-change', async (ev) => {
            const newKrokKey = ev.detail.value;
            const vyjadreniId = select.getAttribute('data-vyjadreni-id');
            if (!newKrokKey) return;  // user vybral "—" — no-op (dropdown is for unbound bubbles)
            
            // POST Create binding
            const response = await fetch('/Vyjadreni/HarmonogramVazba/Create', { ... });
            if (response.ok) {
                await window.pmChatModal.refreshModal();
            }
        });
    });
    
    root.querySelectorAll('[data-clear-bubble-binding]').forEach((btn) => {
        btn.addEventListener('click', async () => {
            const bubble = btn.closest('[data-bubble]');
            const vyjadreniId = bubble.getAttribute('data-vyjadreni-id');
            // Find vazba ID via API or by ?
            ...
        });
    });
}
```

Po POST → `refreshModal()` (re-fetch partial), JS init znovu.

## §5 — Smazat drag&drop

| Soubor | Důvod |
|---|---|
| `stepperDragSnap.js` | Drag broken, žádný dropdown ho nepoužívá |
| `stepperBuffer.js` | Buffer odstraněn — kroky bez bindingu jsou implicit v dropdown options |
| Drag CSS (`cursor: grab`, `[data-dragging]`, drag-related rules v `chat-modal.css`) | Irelevant |

| Zachovat |
|---|
| `stepperSticky.js` — informační sticky alignment kroku vedle bound bubliny |
| Backend POST endpoints (Create/Update/Delete binding) |

## §6 — ViewModel změny

`BubbleViewModel` přidá:
```csharp
public Guid? AssignedKrokKey { get; init; }      // NULL pokud bublina nemá binding
public int? AssignedKrokPoradi { get; init; }    // pro UI label "Krok 3"
public string? AssignedKrokColor { get; init; }  // "success" / "warning" / null
public bool AssignedKrokIsPinned { get; init; }  // true pro K10 PNF auto-pinned
public IReadOnlyList<KrokOptionViewModel> StepOptions { get; init; } = [];
```

`KrokOptionViewModel` (nový):
```csharp
public sealed record KrokOptionViewModel(
    Guid KrokKey,
    int KrokPoradi,
    string Nazev,
    bool IsDisabled,
    string? DisabledReason);
```

`VyjadreniModalViewModelBuilder.BuildAsync` rozšíří per-bubble computation:
1. Načíst všechny aktivní bindings záznamu
2. Pro každou bublinu:
   - AssignedKrok = match z bindings podle hot_vyjadreni_id
   - StepOptions = pro každý relevant krok (per typ filter §3 z 2026-04-28 spec):
     - Skip pokud krok je bound této bublině (zobrazí se jako badge, ne v dropdown)
     - Skip K10 pokud bublina není archivní + K10 je auto-pinned jinde
     - IsDisabled + DisabledReason podle 1:1 / chronologie pravidel

## Tech Stack

ASP.NET Core 8 Razor, gov-form-select (gov-design-system 4.x — `<gov-form-select options="...">`), gov-tag, vanilla JS (ES module), CSS Grid (zachované).

## Implementační dopad

### Smazat
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js`
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js`

### Nové
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js`
- `PmTracker.Web/Models/ViewModels/Vyjadreni/KrokOptionViewModel.cs` (record)

### Modifikované
- `_ChatModal.cshtml` — přidat dropdown/badge container do bublina meta + odstranit buffer slot + click-to-scroll na stepper items
- `chat-modal.css` — drag CSS pryč, dropdown/badge styling
- `BubbleViewModel` (existující) — přidat 5 properties (Assigned* + StepOptions)
- `VyjadreniModalViewModelBuilder.cs` — per-bubble option computation
- `chatModalDragDrop.js` — refactor: smaže import drag/buffer modulů, integrace bubbleStepSelector + clear-bubble-binding handler. Sticky zachovaná. **Renaming**: ne (zachovat název pro backward-compat global API `pmChatModalDragDrop.attach`).

## Testing

- **Architecture guard** `ChatModalDropdownTests`:
  - Razor obsahuje `data-bubble-step-selector` + `data-clear-bubble-binding`
  - Razor obsahuje `<gov-tag` pro badge
  - CSS NEobsahuje `cursor: grab` ani `[data-dragging]`
  - JS modul `bubbleStepSelector.js` existuje, exportuje funkci
  - Smazané: `stepperDragSnap.js` a `stepperBuffer.js` neexistují
- **ViewModel** `BubbleStepOptionsTests`:
  - Builder vytváří StepOptions per bubble
  - 1:1 disabled correctly (krok přiřazen jinde)
  - Chronologie disabled correctly

## Otevřené otázky

Žádné. User schválil 4 body (2026-04-29).

## Risk

- **gov-form-select API kompatibilita** — pokud `options` prop neakceptuje JSON array s `disabled` a `disabledTitle`, fallback na `<option>` slot tags + manual disabled handling.
- **Performance** — pro tikety s 100+ vyjadřeními se generuje 100+ dropdown elementů. Server-side computation je rychlá (in-memory loop přes few items), DOM rendering moderní browser zvládne.
- **refreshModal po každé změně** — flicker. Akceptováno (jednodušší než client-side state management). Pokud bude problém, lze pozdě client-side patch DOM bez reload.
