# Modal Vyjádření a termíny — Dropdown Approach Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Nahradit drag&drop binding (krok ↔ bublina) za dropdown selector v každé bublině. Stepper vpravo se zachová jako informační dashboard se sticky alignment + click-to-scroll.

**Architecture:** Per-bubble `<gov-form-select>` s pre-computed disabled options (1:1 + chronologie validation server-side). Stepper bez drag handlers, jen sticky position + click-to-scroll. Po každé změně → POST + refreshModal.

**Tech Stack:** ASP.NET Core 8 Razor, gov-form-select + gov-tag (gov-design-system 4.x), vanilla JS ES module, xUnit + FluentAssertions.

**Spec source:** [docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md](../specs/2026-04-29-modal-vyjadreni-dropdown-design.md)

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Models/ViewModels/Vyjadreni/KrokOptionViewModel.cs` | Record pro dropdown option (KrokKey, KrokPoradi, Nazev, IsDisabled, DisabledReason) |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js` | Dropdown change + ✕ clear handlers + click-to-scroll na stepper items |
| `PmTracker.Tests.Unit/Vyjadreni/ChatModalDropdownTests.cs` | Architecture guards — Razor obsahuje dropdown, drag CSS pryč, drag JS soubory smazány |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs` (BubbleViewModel) | + AssignedKrokKey, AssignedKrokPoradi, AssignedKrokColor, AssignedKrokIsPinned, StepOptions |
| `PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs` | Per-bubble option computation s 1:1 + chronologie validation |
| `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml` | Dropdown/badge container do bublina meta + odstranit buffer slot + click-to-scroll na stepper |
| `PmTracker.Web/wwwroot/css/components/chat-modal.css` | Drag CSS pryč, dropdown/badge styling |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js` | Refactor: drop drag/buffer imports, integrace bubbleStepSelector + click-to-scroll. Backward-compat global API zachován. |
| `PmTracker.Tests.Unit/Vyjadreni/ChatModalRedesignTests.cs` | Aktualizace asserts — drag pryč, dropdown přibyl |

### Smazané soubory
| Soubor | Důvod |
|---|---|
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js` | Drag broken, dropdown nepoužívá |
| `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js` | Buffer odstraněn, kroky bez bindingu jsou implicit v dropdown options |

### Zachované (informational dashboard)
- `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperSticky.js` — sticky aligned kroky vedle bound bublin

---

## Tasks

### Task 1: KrokOptionViewModel record + extend BubbleViewModel

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/Vyjadreni/KrokOptionViewModel.cs`
- Modify: `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs` (BubbleViewModel)

- [ ] **Step 1: Vytvořit KrokOptionViewModel record**

```csharp
namespace PmTracker.Web.Models.ViewModels.Vyjadreni;

/// <summary>
/// Spec 2026-04-29-modal-vyjadreni-dropdown §3 — option v dropdown selectoru
/// per bublina. IsDisabled + DisabledReason řídí disabled stav v gov-form-select
/// (1:1 / chronologie validation, computed server-side).
/// </summary>
public sealed record KrokOptionViewModel(
    Guid KrokKey,
    int KrokPoradi,
    string Nazev,
    bool IsDisabled,
    string? DisabledReason);
```

- [ ] **Step 2: Najít BubbleViewModel definici**

```bash
grep -n "class BubbleViewModel\|record BubbleViewModel" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Models/ViewModels/Vyjadreni/" -r 2>/dev/null
```

- [ ] **Step 3: Rozšířit BubbleViewModel o 5 properties**

V definici BubbleViewModel přidat (před nebo za existing properties):

```csharp
/// <summary>
/// Spec 2026-04-29: krok přiřazený této bublině přes binding (NULL pokud žádný).
/// </summary>
public Guid? AssignedKrokKey { get; init; }
public int? AssignedKrokPoradi { get; init; }

/// <summary>
/// "success" pro auto-fill binding, "warning" pro manuál binding, NULL pokud bez bindingu.
/// </summary>
public string? AssignedKrokColor { get; init; }

/// <summary>
/// True pokud je krok auto-pinned (např. K10 PNF na archivní bublině) — UI nezobrazí
/// ✕ tlačítko pro odebrání.
/// </summary>
public bool AssignedKrokIsPinned { get; init; }

/// <summary>
/// Dropdown options pro tuto bublinu (jen pokud AssignedKrokKey je NULL).
/// Disabled options řídí 1:1 / chronologie validation (server-side computed).
/// </summary>
public IReadOnlyList<KrokOptionViewModel> StepOptions { get; init; } =
    Array.Empty<KrokOptionViewModel>();
```

- [ ] **Step 4: Build verify**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release --no-restore 2>&1 | tail -3
```

Expected: 0 Warnings, 0 Errors.

---

### Task 2: VyjadreniModalViewModelBuilder — per-bubble option computation

**Files:**
- Modify: `PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs`

- [ ] **Step 1: Najít kde se buduje `vm.Bubliny` v BuildAsync**

```bash
grep -n "Bubliny\s*=\|BubbleViewModel\|hot_vyjadreni" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs" | head -10
```

- [ ] **Step 2: Po načtení bublin přidat option computation**

Po existing `vm.Bubliny = ...` (kde se mapují bubliny) přidat:

```csharp
// Spec 2026-04-29-modal-vyjadreni-dropdown §3 — pre-compute step options per bublina.
// Validace: 1:1 (krok bound jiné bublině = disabled), chronologie (krok N nesmí být
// na bublině s datem mimo interval [krok N-1, krok N+1]).
//
// AssignedKrok info: krok K je bound bublině B pokud existuje binding
// s HotVyjadreniId == B.VyjadreniId AND KrokKey == K.KrokKey AND Stav == Active.

// Mapa: HotVyjadreniId → (KrokKey, Source). Pro každou bublinu existuje max 1 binding.
var bindingByHotVyjadreniId = activeBindings
    .GroupBy(b => b.HotVyjadreniId)
    .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.CreatedAt).First());

// Mapa pro chronologie: KrokPoradi → bublina datum (z bindings).
// Pomáhá zjistit, zda by binding krok N na bublinu B porušil chronologie:
// pokud existuje krok N-1 bound na bublinu B' s pozdějším datem než B → invalid.
var krokToBoundBubbleDatum = new Dictionary<int, DateTime>();
foreach (var b in activeBindings)
{
    var krok = vm.Kroky.FirstOrDefault(k => k.KrokKey == b.KrokKey);
    if (krok is null) continue;
    krokToBoundBubbleDatum[krok.KrokPoradi] = b.DatumVyjadreni;
}

// Special case K10 PNF: pokud existuje bublina s K10 frází (predikát "K10_NasazeniArchivace"),
// K10 je auto-pinned na ní → není v dropdown options jiných bublin.
var k10AutoPinnedHotVyjadreniId = vm.Bubliny
    .FirstOrDefault(b => string.Equals(b.Predikat, "K10_NasazeniArchivace", StringComparison.OrdinalIgnoreCase))
    ?.VyjadreniId;
var k10Krok = vm.Kroky.FirstOrDefault(k => k.KrokPoradi == 10);

// Dopočítat per-bubble Assigned* + StepOptions.
vm.Bubliny = vm.Bubliny.Select(bubble =>
{
    StepperKrokViewModel? assignedKrok = null;
    string? assignedColor = null;
    bool assignedIsPinned = false;

    if (bindingByHotVyjadreniId.TryGetValue(bubble.VyjadreniId, out var binding))
    {
        assignedKrok = vm.Kroky.FirstOrDefault(k => k.KrokKey == binding.KrokKey);
        if (assignedKrok is not null)
        {
            assignedColor = binding.Source switch
            {
                (byte)VazbaSource.Auto => "success",
                (byte)VazbaSource.Manual => "warning",
                _ => null
            };
            // K10 na archivní bublině je auto-pinned (nelze odpojit).
            assignedIsPinned = k10Krok is not null
                && assignedKrok.KrokKey == k10Krok.KrokKey
                && k10AutoPinnedHotVyjadreniId == bubble.VyjadreniId;
        }
    }

    // Build StepOptions jen pokud bublina nemá Assigned (jinak zobrazí badge).
    var options = assignedKrok is not null
        ? Array.Empty<KrokOptionViewModel>()
        : BuildStepOptionsForBubble(bubble, vm.Kroky, activeBindings, krokToBoundBubbleDatum, k10AutoPinnedHotVyjadreniId);

    return bubble with
    {
        AssignedKrokKey = assignedKrok?.KrokKey,
        AssignedKrokPoradi = assignedKrok?.KrokPoradi,
        AssignedKrokColor = assignedColor,
        AssignedKrokIsPinned = assignedIsPinned,
        StepOptions = options
    };
}).ToList();
```

- [ ] **Step 3: Implementovat `BuildStepOptionsForBubble` helper metodu**

V VyjadreniModalViewModelBuilder.cs přidat private static metodu:

```csharp
/// <summary>
/// Spec 2026-04-29-modal-vyjadreni-dropdown §3 — pre-compute disabled options
/// per bublinu pro krok dropdown.
/// </summary>
private static IReadOnlyList<KrokOptionViewModel> BuildStepOptionsForBubble(
    BubbleViewModel bubble,
    IReadOnlyList<StepperKrokViewModel> kroky,
    IReadOnlyList<ZaznamHarmonogramVyjadreniVazbaEntity> activeBindings,
    IReadOnlyDictionary<int, DateTime> krokToBoundBubbleDatum,
    long? k10AutoPinnedHotVyjadreniId)
{
    var options = new List<KrokOptionViewModel>();
    foreach (var krok in kroky.OrderBy(k => k.KrokPoradi))
    {
        // K10 auto-pinned (PNF archivní): NEzobrazujeme v dropdown options jiných bublin.
        if (krok.KrokPoradi == 10 && k10AutoPinnedHotVyjadreniId.HasValue
            && bubble.VyjadreniId != k10AutoPinnedHotVyjadreniId.Value)
        {
            continue;
        }

        bool isDisabled = false;
        string? reason = null;

        // 1:1 — krok bound jiné bublině?
        var existingBinding = activeBindings.FirstOrDefault(b => b.KrokKey == krok.KrokKey);
        if (existingBinding is not null && existingBinding.HotVyjadreniId != bubble.VyjadreniId)
        {
            isDisabled = true;
            reason = $"Přiřazen bublině z {existingBinding.DatumVyjadreni:dd.MM.yyyy}";
        }
        else
        {
            // Chronologie validation: krok N na bublině B je validní iff
            //   - žádný krok N-1 bound s datem > B.Datum
            //   - žádný krok N+1 bound s datem < B.Datum
            foreach (var (otherPoradi, otherDatum) in krokToBoundBubbleDatum)
            {
                if (otherPoradi == krok.KrokPoradi) continue;
                if (otherPoradi < krok.KrokPoradi && otherDatum > bubble.Datum)
                {
                    isDisabled = true;
                    reason = $"Porušila by se chronologie kroku {otherPoradi}";
                    break;
                }
                if (otherPoradi > krok.KrokPoradi && otherDatum < bubble.Datum)
                {
                    isDisabled = true;
                    reason = $"Porušila by se chronologie kroku {otherPoradi}";
                    break;
                }
            }
        }

        options.Add(new KrokOptionViewModel(
            krok.KrokKey, krok.KrokPoradi, krok.Nazev, isDisabled, reason));
    }
    return options;
}
```

- [ ] **Step 4: Build verify + run existing modal tests**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release --no-restore 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit -c Release --filter "ChatModalRedesign|StepperFilteringByType" --no-restore 2>&1 | tail -5
```

Expected: build OK, existing tests pass.

---

### Task 3: Razor partial — dropdown/badge v bublině + odstranit buffer + click-to-scroll na stepper

**Files:**
- Modify: `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml`

- [ ] **Step 1: Najít bublina meta sekci**

```bash
grep -n "pm-chat-bubble__meta\|pm-chat-bubble__pinned" "/Users/Pavel.Andrlik/Documents/PM Tracker/PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml" | head -5
```

- [ ] **Step 2: Nahradit pm-chat-bubble__pinned za nový dropdown/badge container**

V Razor souboru najít:
```cshtml
@if (isAssigned)
{
    <span class="pm-chat-bubble__pinned" title="Navázáno na krok @bubble.NavazanoNaKrokPoradi">
        Krok @bubble.NavazanoNaKrokPoradi
    </span>
}
```

Nahradit za:
```cshtml
<div class="pm-chat-bubble__krok">
    @if (bubble.AssignedKrokKey.HasValue)
    {
        <gov-tag color="@bubble.AssignedKrokColor" size="s">
            Krok @bubble.AssignedKrokPoradi
        </gov-tag>
        @if (canEdit && !bubble.AssignedKrokIsPinned)
        {
            <button type="button"
                    class="pm-chat-bubble__krok-clear"
                    data-clear-bubble-binding
                    data-vyjadreni-id="@bubble.VyjadreniId"
                    title="Odebrat krok">
                ×
            </button>
        }
    }
    else if (canEdit && !isNes && bubble.StepOptions.Count > 0)
    {
        <gov-form-select size="s"
                         data-bubble-step-selector
                         data-vyjadreni-id="@bubble.VyjadreniId"
                         data-vyjadreni-datum="@bubble.Datum.ToString("o")"
                         options="@System.Text.Json.JsonSerializer.Serialize(bubble.StepOptions.Select(o => new {
                             value = o.KrokKey.ToString(),
                             label = $"Krok {o.KrokPoradi}: {o.Nazev}",
                             disabled = o.IsDisabled,
                             title = o.DisabledReason
                         }).Prepend(new { value = "", label = "— vyber krok —", disabled = false, title = (string?)null }))">
        </gov-form-select>
    }
</div>
```

- [ ] **Step 3: Smazat buffer sekci v Razor**

Najít blok `@if (nepriarezeneKroky.Count > 0)` (Razor přidaný v 2026-04-28) a smazat celý blok (včetně `<div class="pm-chat-modal__buffer">`...`</div>`).

Související lokální proměnné `nepriarezeneKroky` smaž též — zachovat jen `prirazeneKroky` (= jen kroky bound bublinám).

- [ ] **Step 4: Stepper section — bez drag handlers + click-to-scroll**

Najít `<gov-stepper size="m" data-aligned-stepper>` blok s `<gov-stepper-item>` items. Atributy `data-step`, `data-vazba-id`, `data-current-vyjadreni-id`, `data-current-datum` zachovat (potřebné pro stepperSticky.js + click-to-scroll). Atributy `data-in-buffer`, `data-source` lze nechat (info), ale drag-related (`cursor: grab` přes CSS) bude pryč v Task 4.

V content slot zachovat „Odpojit" tlačítko (paralelní cesta):
```cshtml
<span slot="content">
    @krok.AktualniVyjadreniDatum?.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
    @if (canEdit)
    {
        <button type="button" class="pm-chat-step__clear"
                data-clear-binding
                title="Odpojit krok od bubliny">
            Odpojit
        </button>
    }
</span>
```

Beze změny.

---

### Task 4: CSS — odstranit drag CSS, přidat dropdown/badge styling

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/components/chat-modal.css`

- [ ] **Step 1: Smazat drag-related CSS pravidla**

Najít a smazat:
```css
gov-stepper-item[data-step] {
    cursor: grab;
    user-select: none;
    transition: transform 60ms ease-out, box-shadow 0.15s;
}
gov-stepper-item[data-step][data-dragging="true"] {
    cursor: grabbing;
    box-shadow: 0 8px 16px rgba(0,0,0,0.15);
    z-index: 10;
}
gov-stepper-item[data-step][data-pinned="true"] {
    cursor: not-allowed;
}
.pm-chat-bubble.drop-target {
    outline: 2px solid var(--gov-color-warning, #F59E0B);
    outline-offset: 2px;
}
```

A smazat buffer-related CSS:
```css
.pm-chat-modal__buffer { ... }
.pm-chat-modal__buffer-header { ... }
@media (max-width: 1099px) {
    .pm-chat-modal__buffer { position: static; }
}
```

- [ ] **Step 2: Přidat dropdown/badge styling**

```css
/* Bublina krok selector — vpravo nahoře v meta line */
.pm-chat-bubble__krok {
    margin-left: auto;
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    flex-shrink: 0;
}

.pm-chat-bubble__krok-clear {
    background: transparent;
    border: 1px solid var(--gov-color-error, #DC2626);
    color: var(--gov-color-error, #DC2626);
    border-radius: 50%;
    width: 1.25rem;
    height: 1.25rem;
    line-height: 1;
    font-size: 0.85rem;
    cursor: pointer;
    padding: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
}
.pm-chat-bubble__krok-clear:hover {
    background: var(--gov-color-error-bg, #FEF2F2);
}

/* Stepper items — clickable pro scroll-to-bubble (žádný drag cursor) */
gov-stepper-item[data-step] {
    cursor: pointer;
    transition: opacity 0.15s;
}
gov-stepper-item[data-step]:hover {
    opacity: 0.85;
}
```

- [ ] **Step 3: Build CSS sanity check**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release --no-restore 2>&1 | tail -3
```

---

### Task 5: JS bubbleStepSelector.js — onChange + clear handlers

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js`

- [ ] **Step 1: Vytvořit modul**

```javascript
/**
 * bubbleStepSelector.js — onChange handler pro <gov-form-select> v bublinách
 * + clear handler pro ✕ tlačítko + click-to-scroll na stepper items.
 *
 * Spec: docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md §4
 */

function getCsrfToken(root) {
    const input = root.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
}

function setStatus(root, text, state) {
    const el = root.querySelector('[data-chat-status]');
    if (!el) return;
    el.textContent = text || '';
    if (state) el.setAttribute('data-status-state', state);
    else el.removeAttribute('data-status-state');
}

async function refreshModalIfPossible() {
    if (window.pmChatModal && typeof window.pmChatModal.refreshModal === 'function') {
        await window.pmChatModal.refreshModal();
    }
}

async function createBinding(root, payload) {
    const token = getCsrfToken(root);
    const resp = await fetch('/Vyjadreni/HarmonogramVazba/Create', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify(payload)
    });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    return resp.json();
}

async function deleteBinding(root, payload) {
    const token = getCsrfToken(root);
    const resp = await fetch('/Vyjadreni/HarmonogramVazba/Delete', {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify(payload)
    });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    return resp.json();
}

export function attachBubbleStepSelectors(root) {
    if (!root) return;

    const externiOdkazId = Number(root.getAttribute('data-externi-odkaz-id'));
    const zaznamId = Number(root.getAttribute('data-zaznam-id'));
    const projektId = Number(root.getAttribute('data-projekt-id'));

    // Dropdown change → POST Create binding
    root.querySelectorAll('[data-bubble-step-selector]').forEach((select) => {
        select.addEventListener('gov-change', async (ev) => {
            const newKrokKey = (ev.detail && ev.detail.value) || '';
            if (!newKrokKey) return;  // user vybral "—" — no-op

            const vyjadreniId = select.getAttribute('data-vyjadreni-id');
            const vyjadreniDatum = select.getAttribute('data-vyjadreni-datum');
            const payload = {
                externiOdkazId: externiOdkazId,
                zaznamId: zaznamId,
                projektId: projektId,
                krokKey: newKrokKey,
                hotVyjadreniId: Number(vyjadreniId),
                datumVyjadreni: vyjadreniDatum
            };

            setStatus(root, 'Ukládám…', null);
            try {
                await createBinding(root, payload);
                setStatus(root, 'Krok přiřazen.', 'ok');
                await refreshModalIfPossible();
            } catch (err) {
                console.error('Bubble step bind failed', err);
                setStatus(root, 'Uložení selhalo: ' + (err.message || err), 'error');
            }
        });
    });

    // ✕ tlačítko na badge → POST Delete binding
    root.querySelectorAll('[data-clear-bubble-binding]').forEach((btn) => {
        btn.addEventListener('click', async () => {
            const vyjadreniId = btn.getAttribute('data-vyjadreni-id');
            // Najít vazbu ID — z stepperu (krok s data-current-vyjadreni-id == this vyjadreniId)
            const stepperItem = root.querySelector(
                'gov-stepper-item[data-step][data-current-vyjadreni-id="' + vyjadreniId + '"]'
            );
            const vazbaId = stepperItem ? Number(stepperItem.getAttribute('data-vazba-id')) : 0;
            if (!vazbaId) {
                setStatus(root, 'Nelze odebrat — chybí ID vazby.', 'error');
                return;
            }
            const payload = { vazbaId: vazbaId, projektId: projektId };
            setStatus(root, 'Odpojuji…', null);
            try {
                await deleteBinding(root, payload);
                setStatus(root, 'Krok odebrán.', 'ok');
                await refreshModalIfPossible();
            } catch (err) {
                console.error('Bubble clear binding failed', err);
                setStatus(root, 'Odebrání selhalo: ' + (err.message || err), 'error');
            }
        });
    });

    // Stepper item click → scroll na bound bublinu
    root.querySelectorAll('gov-stepper-item[data-step]').forEach((item) => {
        item.addEventListener('click', (ev) => {
            // Ignore klik na "Odpojit" tlačítko uvnitř — to má vlastní handler
            if (ev.target.closest('[data-clear-binding]')) return;
            const vyjadreniId = item.getAttribute('data-current-vyjadreni-id');
            if (!vyjadreniId) return;
            const bubble = root.querySelector('[data-bubble][data-vyjadreni-id="' + vyjadreniId + '"]');
            if (bubble) bubble.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
    });

    // Stepper „Odpojit" button → POST Delete (paralelní cesta k ✕ na badge)
    root.querySelectorAll('[data-clear-binding]').forEach((btn) => {
        btn.addEventListener('click', async (ev) => {
            ev.stopPropagation();
            const item = btn.closest('gov-stepper-item[data-step]');
            if (!item) return;
            const vazbaId = Number(item.getAttribute('data-vazba-id'));
            if (!vazbaId) {
                setStatus(root, 'Chybí data-vazba-id — nelze odpojit.', 'error');
                return;
            }
            const payload = { vazbaId: vazbaId, projektId: projektId };
            setStatus(root, 'Odpojuji…', null);
            try {
                await deleteBinding(root, payload);
                setStatus(root, 'Odpojeno.', 'ok');
                await refreshModalIfPossible();
            } catch (err) {
                setStatus(root, 'Odpojení selhalo: ' + (err.message || err), 'error');
            }
        });
    });
}
```

---

### Task 6: Refactor chatModalDragDrop.js — drop drag/buffer, integrate bubbleStepSelector

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js`

- [ ] **Step 1: Přepsat soubor**

```javascript
/**
 * chatModalDragDrop.js — orchestrace bindings v modalu Vyjádření a termíny.
 *
 * 2026-04-29 redesign (commit XXX): drag&drop nahrazen dropdown selectorem v každé
 * bublině. Tato modul zajistí init dropdown handlers (bubbleStepSelector) +
 * sticky alignment (stepperSticky) jako informational dashboard.
 *
 * Backward-compat API: window.pmChatModalDragDrop.attach(root) — voláno z chatModal.js.
 */

import { initStepperSticky } from './stepperSticky.js';
import { attachBubbleStepSelectors } from './bubbleStepSelector.js';

function attach(root) {
    if (!root) return null;
    if (root.getAttribute('data-chat-init') === 'true') return null;
    root.setAttribute('data-chat-init', 'true');

    // Sticky aligned kroky vedle bound bublin (informational dashboard).
    const sticky = initStepperSticky(root);

    // Dropdown handlers + clear + click-to-scroll.
    attachBubbleStepSelectors(root);

    return {
        destroy: function () {
            if (sticky) sticky.destroy();
        }
    };
}

if (typeof window !== 'undefined') {
    window.pmChatModalDragDrop = { attach: attach };
}

export { attach };
```

- [ ] **Step 2: Build verify**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.Web/PmTracker.Web.csproj -c Release --no-restore 2>&1 | tail -3
```

---

### Task 7: Smazat stepperDragSnap.js + stepperBuffer.js

**Files:**
- Delete: `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js`
- Delete: `PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js`

- [ ] **Step 1: Smazat soubory**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
rm PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js
rm PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js
```

---

### Task 8: Architecture guard testy

**Files:**
- Create: `PmTracker.Tests.Unit/Vyjadreni/ChatModalDropdownTests.cs`
- Modify: `PmTracker.Tests.Unit/Vyjadreni/ChatModalRedesignTests.cs`

- [ ] **Step 1: Nový test ChatModalDropdownTests**

```csharp
using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Architecture guard pro 2026-04-29 dropdown redesign.
/// Spec: docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md
/// </summary>
public sealed class ChatModalDropdownTests
{
    private static string LoadRepoText(string relativePath)
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }
        if (directory is null) throw new System.InvalidOperationException("Nepodařilo se najít kořen.");
        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat: {full}");
        return File.ReadAllText(full);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new System.InvalidOperationException("Nepodařilo se najít kořen.");
    }

    [Fact]
    public void RazorPartial_ObsahujeBubbleStepSelectorAndClearButton()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().Contain("data-bubble-step-selector",
            "Razor musí renderovat dropdown selector v bublinách.");
        html.Should().Contain("data-clear-bubble-binding",
            "Razor musí renderovat ✕ clear button na badge.");
        html.Should().Contain("<gov-tag",
            "Razor musí používat gov-tag pro badge přiřazeného kroku.");
    }

    [Fact]
    public void RazorPartial_ZadnyBufferProNeprirazeneKroky()
    {
        var html = LoadRepoText("PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml");
        html.Should().NotContain("data-stepper-buffer",
            "Buffer pro nepřiřazené kroky byl odstraněn (kroky bez bindingu jsou implicit v dropdown options).");
        html.Should().NotContain("Nepřiřazené kroky",
            "Buffer header text odstraněn.");
    }

    [Fact]
    public void Css_ZadneDragRules()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().NotContain("cursor: grab",
            "Drag cursor odstraněn — žádný drag&drop.");
        css.Should().NotContain("data-dragging",
            "Drag state CSS odstraněn.");
        css.Should().NotContain(".pm-chat-modal__buffer",
            "Buffer CSS odstraněn.");
    }

    [Fact]
    public void Css_ObsahujeKrokSelectorStyling()
    {
        var css = LoadRepoText("PmTracker.Web/wwwroot/css/components/chat-modal.css");
        css.Should().Contain(".pm-chat-bubble__krok",
            "Krok selector container styling musí existovat.");
        css.Should().Contain(".pm-chat-bubble__krok-clear",
            "✕ button styling musí existovat.");
    }

    [Fact]
    public void BubbleStepSelectorJs_Existuje()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js");
        js.Should().Contain("export function attachBubbleStepSelectors",
            "Modul musí exportovat attach funkci.");
        js.Should().Contain("data-bubble-step-selector",
            "Modul musí targetovat dropdown selectory.");
        js.Should().Contain("scrollIntoView",
            "Modul musí implementovat click-to-scroll na stepper items.");
    }

    [Fact]
    public void DragSnapAndBufferModules_BylySmazany()
    {
        var dragSnap = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "js", "modules", "vyjadreni", "stepperDragSnap.js");
        File.Exists(dragSnap).Should().BeFalse(
            "stepperDragSnap.js byl smazán (drag broken, dropdown nepoužívá).");

        var buffer = Path.Combine(FindRepoRoot(),
            "PmTracker.Web", "wwwroot", "js", "modules", "vyjadreni", "stepperBuffer.js");
        File.Exists(buffer).Should().BeFalse(
            "stepperBuffer.js byl smazán (buffer odstraněn).");
    }

    [Fact]
    public void ChatModalDragDropJs_ImportujeNoveModuly()
    {
        var js = LoadRepoText("PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js");
        js.Should().Contain("import { attachBubbleStepSelectors }",
            "chatModalDragDrop musí importovat bubbleStepSelector.");
        js.Should().Contain("import { initStepperSticky }",
            "chatModalDragDrop musí zachovat stepperSticky import.");
        js.Should().NotContain("stepperDragSnap",
            "chatModalDragDrop už NESMÍ importovat smazaný stepperDragSnap.");
        js.Should().NotContain("stepperBuffer",
            "chatModalDragDrop už NESMÍ importovat smazaný stepperBuffer.");
        js.Should().Contain("window.pmChatModalDragDrop",
            "Backward-compat global API zachován.");
    }

    [Fact]
    public void BubbleViewModel_MaPropertiesProDropdown()
    {
        var cs = LoadRepoText("PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs");
        cs.Should().Contain("AssignedKrokKey",
            "BubbleViewModel musí mít AssignedKrokKey property.");
        cs.Should().Contain("AssignedKrokColor",
            "BubbleViewModel musí mít AssignedKrokColor property.");
        cs.Should().Contain("StepOptions",
            "BubbleViewModel musí mít StepOptions property.");
    }

    [Fact]
    public void KrokOptionViewModel_Existuje()
    {
        var cs = LoadRepoText("PmTracker.Web/Models/ViewModels/Vyjadreni/KrokOptionViewModel.cs");
        cs.Should().Contain("public sealed record KrokOptionViewModel",
            "KrokOptionViewModel record musí existovat.");
        cs.Should().Contain("IsDisabled",
            "Record musí mít IsDisabled property pro disabled state validation.");
        cs.Should().Contain("DisabledReason",
            "Record musí mít DisabledReason property pro tooltip.");
    }
}
```

- [ ] **Step 2: Aktualizovat existující ChatModalRedesignTests — některé testy už neplatí**

V `ChatModalRedesignTests.cs` smazat tyto testy (drag-related, neplatné v dropdown approach):
- `RazorPartial_ObsahujeBufferProNeprirazeneKroky` (buffer odstraněn)
- `StepperDragSnapModule_Existuje` (modul smazán)
- `StepperBufferModule_Existuje` (modul smazán)
- `ChatModalDragDropModule_PouzivaNoveModuly` (asserts old imports — já mám aktualizovanou verzi v ChatModalDropdownTests)

Najít a smazat:

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
# Manuálně smazat 4 testy v ChatModalRedesignTests.cs (cca 30-50 řádků)
```

Konkrétně smazat testy:
- `[Fact] public void ChatModalCshtml_ObsahujeBufferProNeprirazeneKroky()` — část `data-stepper-buffer` + Nepřiřazené kroky asserts (block ~10 řádků)
- `[Fact] public void StepperDragSnapModule_Existuje()` — celý fact (block ~10 řádků)
- `[Fact] public void StepperBufferModule_Existuje()` — celý fact (block ~10 řádků)
- `[Fact] public void ChatModalDragDropModule_PouzivaNoveModuly()` — celý fact (block ~15 řádků)

- [ ] **Step 3: Run tests**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit -c Release --filter "ChatModalDropdown|ChatModalRedesign" --no-restore 2>&1 | tail -8
```

Expected: all pass.

---

### Task 9: Final build + run all tests + smoke

- [ ] **Step 1: Full build**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln -c Release --no-restore 2>&1 | tail -8
```

Expected: 0 Warning(s), 0 Error(s).

- [ ] **Step 2: Full unit suite**

```bash
dotnet test PmTracker.Tests.Unit -c Release --no-restore 2>&1 | tail -3
```

Expected: většina pass, 2 pre-existing fails (bun bundle quote — nesouvisí).

- [ ] **Step 3: Manuální smoke (post-deploy)**
  1. Otevřít modal pro PMP záznam — bubliny mají dropdown vpravo nahoře (pokud nejsou bound)
  2. Vybrat krok z dropdownu → krok se přiřadí, modal refresh, badge zobrazí
  3. Klik na ✕ na badge → krok odebrán, dropdown zase k dispozici
  4. Klik na krok ve stepperu vpravo → modal scrollne na jeho bound bublinu (smooth)
  5. Pokus přiřadit krok N na bublinu mimo chronologii → option je disabled v dropdownu, tooltip vysvětluje
  6. Pokus přiřadit krok N už bound jiné bublině → option disabled, tooltip s datem druhé bubliny

---

### Task 10: Commit + publish

- [ ] **Step 1: Stage files**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add \
  docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md \
  docs/superpowers/plans/2026-04-29-modal-vyjadreni-dropdown.md \
  PmTracker.Web/Models/ViewModels/Vyjadreni/KrokOptionViewModel.cs \
  PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs \
  PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs \
  PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml \
  PmTracker.Web/wwwroot/css/components/chat-modal.css \
  PmTracker.Web/wwwroot/js/modules/vyjadreni/chatModalDragDrop.js \
  PmTracker.Web/wwwroot/js/modules/vyjadreni/bubbleStepSelector.js \
  PmTracker.Tests.Unit/Vyjadreni/ChatModalDropdownTests.cs \
  PmTracker.Tests.Unit/Vyjadreni/ChatModalRedesignTests.cs

git rm PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperDragSnap.js \
       PmTracker.Web/wwwroot/js/modules/vyjadreni/stepperBuffer.js
```

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(modal-vyjadreni): dropdown approach (replace drag&drop)"
```

- [ ] **Step 3: Publish + zip (per memory rule)**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet publish PmTracker.Web/PmTracker.Web.csproj -c Release -o publish/
cd publish
zip -r ../publish.zip *
cd ..
ls -la publish.zip
```

---

## Self-Review

**1. Spec coverage:**
- §1 Layout (dropdown/badge v bublině) → Task 3 (Razor)
- §2 Stepper informační dashboard (sticky + click-to-scroll, no drag) → Task 3, Task 5 (click handler), Task 4 (no drag CSS)
- §3 Step options validation (1:1 + chronologie) → Task 2 (BuildStepOptionsForBubble)
- §4 Frontend handler → Task 5 (bubbleStepSelector.js)
- §5 Smazat drag → Task 7 (delete files), Task 4 (drag CSS), Task 6 (refactor imports)
- §6 ViewModel changes → Task 1 (KrokOptionViewModel + BubbleViewModel)
- Special K10 PNF auto-pinned → Task 2 (k10AutoPinnedHotVyjadreniId logic)

**2. Placeholder scan:** Žádné TBD/TODO. Code blocks ve všech krocích.

**3. Type consistency:** `KrokOptionViewModel`, `BubbleViewModel.AssignedKrokKey`, `attachBubbleStepSelectors`, `BuildStepOptionsForBubble` konzistentně použity napříč tasks 1, 2, 3, 5, 8.

Plan complete.
