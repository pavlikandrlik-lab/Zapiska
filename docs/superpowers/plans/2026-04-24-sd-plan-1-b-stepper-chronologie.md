# Feature B — `pm-chat-stepper` + chronologie + buffer implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nahradit custom `pm-chat-step` CSS komponenty za custom element `<pm-chat-stepper>` dědící styly z gov-stepper; implementovat / ověřit chronology constraint při drag&drop; přidat buffer 5 pevných slotů (3 chronologické kroky dle typu ticketu + 2 slot pro user-added nepovinné kroky).

**Architecture:** Web component `PmChatStepperElement extends HTMLElement` manuálně importuje gov-stepper styly přes CSS `@import` (nebo shadowed `<link>`). Chronologie check v JS před commit drop eventu. Buffer slots jsou prázdné DOM elementy na konci stepper listu, vizuálně označené „+ přidat nepovinný krok" pro user-with-permission.

**Tech Stack:** Vanilla JS web components, gov-design-system (gov-stepper [https://designsystem.gov.cz/komponenty/stepper.html](https://designsystem.gov.cz/komponenty/stepper.html)), stávající `pm-chat-modal`, existing binding service (`BindingRebalanceService.cs`).

**Spec source:**
- [2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §7.3-7.4](../specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md)
- Memory: `project_servicedesk_infosystem_binding.md` → sekce „Stepper chronologie rules"

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/pm-chat-stepper.js` | Custom element `<pm-chat-stepper>` — wrapper + lifecycle |
| `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/chronology.js` | Pure funkce `validateDrop(bubbleDatum, targetKrok, currentBindings)` + `detectCascade(...)` |
| `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/buffer.js` | Helpery pro N=5 buffer slots (3+2) |
| `PmTracker.Web/wwwroot/css/components/pm-chat-stepper.css` | Gov-stepper import + chat-modal override |
| `PmTracker.Tests.Unit/Services/ServiceDesk/StepperChronologyTests.cs` | Unit testy chronology logic (pure functions, JS-to-C# port NEBO pure JS test — zvolit dle existing JS test infra) |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml` | `<div class="pm-chat-modal__stepper">` → `<pm-chat-stepper>` element |
| `PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs` | Přidat computed field `StepperSlotsPocet = 5` (3 fixní + 2 add-on) |
| `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs` | Přidat `int StepperSlotsPocet { get; init; }` + `int FixniKrokyPocet { get; init; }` |
| `PmTracker.Web/wwwroot/js/site.bundle.js` | Synchronizovat pm-chat-stepper modul (memory pravidlo: bundle sync ručně) |

---

## Tasks

### Task 1: Gov-stepper integrace — import gov kit v4 komponenty

**Files:**
- Check: existuje-li `gov-stepper` v `PmTracker.Web/wwwroot/lib/gov-design-system/` (nebo kamkoli se gov kit v4 loaduje v `_Layout.cshtml`)
- Create: `PmTracker.Web/wwwroot/css/components/pm-chat-stepper.css`

- [ ] **Step 1: Verify gov-stepper presence**

```bash
grep -rn "gov-stepper" PmTracker.Web/wwwroot/lib/ PmTracker.Web/Views/Shared/_Layout.cshtml 2>&1 | head -10
```

Expected: alespoň jeden match v gov-design-system kit. Pokud žádný → gov-stepper je nutno dodat jako extra import do `_Layout.cshtml` (dle gov kit v4 dokumentace). Pokud scripts loadují celý bundle gov komponent, nic nedělat (bude auto-available).

- [ ] **Step 2: Create pm-chat-stepper.css s gov styly**

```css
/* PmTracker.Web/wwwroot/css/components/pm-chat-stepper.css */
/*
 * pm-chat-stepper — custom element dědící vizuál z gov-stepper.
 * Gov styly se dědí přes native web component inheritance;
 * tento stylesheet je pro chat-modal-specifické override.
 */

pm-chat-stepper {
    display: block;
    width: 100%;
}

/* Buffer slots — 2 add-on sloty na konci */
pm-chat-stepper .pm-chat-stepper__buffer-slot {
    opacity: 0.5;
    border: 1px dashed var(--gov-color-neutral-border, #d0d5dd);
    padding: 0.5rem;
    margin-top: 0.5rem;
    cursor: pointer;
    transition: opacity 0.15s;
}

pm-chat-stepper .pm-chat-stepper__buffer-slot:hover {
    opacity: 0.85;
    background: var(--gov-color-neutral-bg-hover, #f9fafb);
}

pm-chat-stepper .pm-chat-stepper__buffer-slot[data-can-add="false"] {
    cursor: not-allowed;
    opacity: 0.3;
}

/* Drop-target visual feedback */
pm-chat-stepper [data-krok-key].pm-chat-stepper__drop-valid {
    outline: 2px solid var(--gov-color-success, #16a34a);
    outline-offset: 2px;
}

pm-chat-stepper [data-krok-key].pm-chat-stepper__drop-invalid {
    outline: 2px solid var(--gov-color-danger, #dc2626);
    outline-offset: 2px;
    cursor: not-allowed;
}
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/css/components/pm-chat-stepper.css
git commit -m "feat(stepper): CSS scaffold pro pm-chat-stepper dědící z gov-stepper"
```

---

### Task 2: Chronology pure functions — TDD

**Files:**
- Create: `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/chronology.js`
- Create: `PmTracker.Tests.Unit/Services/ServiceDesk/StepperChronologyTests.cs` (pokud JS unit infra neexistuje, C# port pure logiky)

- [ ] **Step 1: Napsat pure funkci `validateDrop` v JS**

```javascript
// PmTracker.Web/wwwroot/js/components/pm-chat-stepper/chronology.js
(function(global){
  'use strict';

  /**
   * Validuje, zda bublina s datem `bubbleDatum` může být puštěna na krok `targetKrok`.
   * Pravidla (dle memory project_servicedesk_infosystem_binding.md "Stepper chronologie rules"):
   *  - Krok dříve v řadě (menší poradi) musí mít bublinu s ranějším datem nebo prázdný.
   *  - Krok později v řadě (větší poradi) může obsahovat bublinu s pozdějším datem (= může se "posunout dolů" pokud nové drop ho předběhne).
   *  - Spodní krok nemůže vytlačit horní; horní krok posouvá spodní.
   * @param {Date} bubbleDatum - datum bubliny, která se přesouvá
   * @param {{poradi: number, bindingDatum: Date|null}} targetKrok - cílový krok
   * @param {Array<{poradi: number, bindingDatum: Date|null}>} allKroky - všechny kroky seřazené dle poradi asc
   * @returns {{ok: boolean, reason?: string, cascade?: Array<number>}}
   */
  function validateDrop(bubbleDatum, targetKrok, allKroky) {
    // 1) Zkontroluj kroky s menším poradi (musí být ≤ bubbleDatum)
    for (const krok of allKroky) {
      if (krok.poradi >= targetKrok.poradi) continue;
      if (krok.bindingDatum && krok.bindingDatum > bubbleDatum) {
        return {
          ok: false,
          reason: `Krok #${krok.poradi} má binding z ${krok.bindingDatum.toISOString().slice(0,10)} — nelze vložit dříve datovanou bublinu.`
        };
      }
    }
    // 2) Zkontroluj kroky s vyšším poradi (musí být prázdné nebo ≥ bubbleDatum; pokud ne, cascade)
    const cascade = [];
    for (const krok of allKroky) {
      if (krok.poradi <= targetKrok.poradi) continue;
      if (krok.bindingDatum && krok.bindingDatum < bubbleDatum) {
        cascade.push(krok.poradi); // krok se musí "posunout dolů" (uvolnit binding)
      }
    }
    return { ok: true, cascade: cascade.length > 0 ? cascade : undefined };
  }

  global.pmChatStepperChronology = { validateDrop };
})(window);
```

- [ ] **Step 2: Psát unit testy (JS Jasmine/Mocha pokud infra, jinak manuální test)**

Nejdřív ověř existenci JS testovacího runneru:
```bash
find PmTracker.Web -name "*.test.js" -o -name "*.spec.js" 2>&1 | head -5
find . -name "karma.conf*" -o -name "jest.config*" 2>&1 | head -5
```

Pokud JS test infra **neexistuje**, port logiky do C#:

```csharp
// PmTracker.Tests.Unit/Services/ServiceDesk/StepperChronologyTests.cs
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Services.ServiceDesk;

public sealed class StepperChronologyTests
{
    private static readonly DateTime D1 = new DateTime(2026, 3, 1);
    private static readonly DateTime D2 = new DateTime(2026, 3, 10);
    private static readonly DateTime D3 = new DateTime(2026, 3, 20);

    public record KrokState(int Poradi, DateTime? BindingDatum);
    public record DropResult(bool Ok, string? Reason, int[]? Cascade);

    /// <summary>
    /// Port logiky z chronology.js — single source of truth jsou JS funkce,
    /// tato C# verze je pro automatizovaný regresní test (JS test infra není).
    /// Pokud se logika v JS změní, tento test musí být updatnut.
    /// </summary>
    private static DropResult ValidateDrop(DateTime bubble, KrokState target, KrokState[] allKroky)
    {
        foreach (var k in allKroky)
        {
            if (k.Poradi >= target.Poradi) continue;
            if (k.BindingDatum is DateTime bd && bd > bubble)
                return new(false, $"Krok #{k.Poradi} má binding z {bd:yyyy-MM-dd}", null);
        }
        var cascade = new List<int>();
        foreach (var k in allKroky)
        {
            if (k.Poradi <= target.Poradi) continue;
            if (k.BindingDatum is DateTime bd && bd < bubble)
                cascade.Add(k.Poradi);
        }
        return new(true, null, cascade.Count > 0 ? cascade.ToArray() : null);
    }

    [Fact]
    public void Drop_Target_IsFirstEmpty_NoConflict_Ok()
    {
        var kroky = new[] { new KrokState(1, null), new KrokState(2, null), new KrokState(3, null) };
        var r = ValidateDrop(D2, kroky[0], kroky);
        r.Ok.Should().BeTrue();
        r.Cascade.Should().BeNull();
    }

    [Fact]
    public void Drop_Target_HasEarlierKrok_WithLaterDatum_Blocks()
    {
        var kroky = new[] { new KrokState(1, D3), new KrokState(2, null), new KrokState(3, null) };
        var r = ValidateDrop(D2, kroky[1], kroky);
        r.Ok.Should().BeFalse();
        r.Reason.Should().Contain("Krok #1");
    }

    [Fact]
    public void Drop_Target_HasLaterKrok_WithEarlierDatum_CascadesDown()
    {
        var kroky = new[] { new KrokState(1, null), new KrokState(2, null), new KrokState(3, D1) };
        var r = ValidateDrop(D2, kroky[1], kroky);
        r.Ok.Should().BeTrue();
        r.Cascade.Should().Equal(3);
    }

    [Fact]
    public void Drop_Bubble_Equals_ExistingBinding_Ok()
    {
        var kroky = new[] { new KrokState(1, D1), new KrokState(2, null) };
        var r = ValidateDrop(D1, kroky[1], kroky);
        r.Ok.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run tests — PASS**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "StepperChronology" --no-restore
```
Expected: PASS 4/4.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/components/pm-chat-stepper/chronology.js \
        PmTracker.Tests.Unit/Services/ServiceDesk/StepperChronologyTests.cs
git commit -m "feat(stepper): chronology pure funkce + unit testy"
```

---

### Task 3: Buffer pro 5 pevných slotů

**Files:**
- Create: `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/buffer.js`

- [ ] **Step 1: Implementovat `computeStepperSlots` pure funkci**

```javascript
// PmTracker.Web/wwwroot/js/components/pm-chat-stepper/buffer.js
(function(global){
  'use strict';

  const FIXED_SLOTS = 3;
  const ADDON_SLOTS = 2;
  const TOTAL_SLOTS = FIXED_SLOTS + ADDON_SLOTS;

  /**
   * Vrátí renderable slots pro stepper: 3 fixní (dle typu ticketu) + 2 prázdné add-on sloty.
   * Pokud fixní je víc než 3, extend přirozeně; pokud méně, padding empty.
   * @param {Array<{poradi:number, label:string, bindingDatum:Date|null, fixni:boolean}>} kroky
   * @param {boolean} mozeAddonPridat - user má právo přidat nepovinný krok
   * @returns {Array} renderable slots
   */
  function computeStepperSlots(kroky, mozeAddonPridat) {
    const fixed = kroky.filter(k => k.fixni).sort((a,b) => a.poradi - b.poradi);
    const addon = kroky.filter(k => !k.fixni).sort((a,b) => a.poradi - b.poradi);
    const slots = [...fixed, ...addon];

    // Padding do TOTAL_SLOTS prázdnými add-on sloty
    while (slots.length < TOTAL_SLOTS) {
      slots.push({
        poradi: slots.length + 1,
        label: mozeAddonPridat ? '+ přidat nepovinný krok' : '—',
        bindingDatum: null,
        fixni: false,
        isBufferSlot: true,
        canAdd: mozeAddonPridat
      });
    }

    return slots;
  }

  global.pmChatStepperBuffer = { computeStepperSlots, FIXED_SLOTS, ADDON_SLOTS, TOTAL_SLOTS };
})(window);
```

- [ ] **Step 2: Commit**

```bash
git add PmTracker.Web/wwwroot/js/components/pm-chat-stepper/buffer.js
git commit -m "feat(stepper): buffer 5 slotů (3 fixní + 2 add-on)"
```

---

### Task 4: `<pm-chat-stepper>` custom element

**Files:**
- Create: `PmTracker.Web/wwwroot/js/components/pm-chat-stepper/pm-chat-stepper.js`

- [ ] **Step 1: Napsat custom element**

```javascript
// PmTracker.Web/wwwroot/js/components/pm-chat-stepper/pm-chat-stepper.js
(function(global){
  'use strict';

  class PmChatStepperElement extends HTMLElement {
    constructor() {
      super();
      this._kroky = [];
      this._canAddAddon = false;
    }

    connectedCallback() {
      this._canAddAddon = this.hasAttribute('can-add-addon');
      this._render();
      this._bindDrops();
    }

    /** Setter called from chat modal init — array of krok objects */
    setKroky(kroky) {
      this._kroky = kroky;
      if (this.isConnected) this._render();
    }

    _render() {
      const slots = global.pmChatStepperBuffer.computeStepperSlots(this._kroky, this._canAddAddon);
      this.innerHTML = slots.map(slot => `
        <div class="pm-chat-step${slot.isBufferSlot ? ' pm-chat-stepper__buffer-slot' : ''}"
             data-krok-key="${slot.poradi}"
             data-krok-poradi="${slot.poradi}"
             data-is-buffer="${!!slot.isBufferSlot}"
             data-can-add="${!!slot.canAdd}"
             aria-dropeffect="move">
          <span class="pm-chat-step__label">${slot.label}</span>
          ${slot.bindingDatum ? `<span class="pm-chat-step__datum">${slot.bindingDatum.toISOString().slice(0,10)}</span>` : ''}
        </div>
      `).join('');
    }

    _bindDrops() {
      this.addEventListener('dragover', this._onDragOver.bind(this));
      this.addEventListener('dragleave', this._onDragLeave.bind(this));
      this.addEventListener('drop', this._onDrop.bind(this));
    }

    _onDragOver(e) {
      const target = e.target.closest('[data-krok-key]');
      if (!target) return;
      e.preventDefault();

      const bubbleDatum = new Date(e.dataTransfer.types.includes('application/x-bubble-datum')
        ? e.dataTransfer.getData('application/x-bubble-datum')
        : 0);
      if (isNaN(bubbleDatum.valueOf())) return;

      const poradi = parseInt(target.dataset.kroPoradi ?? target.dataset.kroKey, 10);
      const targetKrok = this._kroky.find(k => k.poradi === poradi) || { poradi, bindingDatum: null };
      const v = global.pmChatStepperChronology.validateDrop(bubbleDatum, targetKrok, this._kroky);

      this.querySelectorAll('[data-krok-key]').forEach(el => {
        el.classList.remove('pm-chat-stepper__drop-valid', 'pm-chat-stepper__drop-invalid');
      });
      target.classList.add(v.ok ? 'pm-chat-stepper__drop-valid' : 'pm-chat-stepper__drop-invalid');

      if (!v.ok) {
        target.title = v.reason;
        e.dataTransfer.dropEffect = 'none';
      } else {
        target.title = v.cascade ? `Cascade: posunou se kroky ${v.cascade.join(', ')}` : '';
        e.dataTransfer.dropEffect = 'move';
      }
    }

    _onDragLeave(e) {
      const target = e.target.closest('[data-krok-key]');
      target?.classList.remove('pm-chat-stepper__drop-valid', 'pm-chat-stepper__drop-invalid');
    }

    _onDrop(e) {
      e.preventDefault();
      const target = e.target.closest('[data-krok-key]');
      if (!target) return;
      target.classList.remove('pm-chat-stepper__drop-valid', 'pm-chat-stepper__drop-invalid');

      const detail = {
        kroPoradi: parseInt(target.dataset.kroPoradi ?? target.dataset.kroKey, 10),
        bubbleId: e.dataTransfer.getData('application/x-bubble-id'),
        bubbleDatum: e.dataTransfer.getData('application/x-bubble-datum'),
        isBufferSlot: target.dataset.isBuffer === 'true',
        canAdd: target.dataset.canAdd === 'true'
      };
      this.dispatchEvent(new CustomEvent('pm-chat-stepper-drop', { detail, bubbles: true }));
    }
  }

  if (!customElements.get('pm-chat-stepper')) {
    customElements.define('pm-chat-stepper', PmChatStepperElement);
  }

  global.PmChatStepperElement = PmChatStepperElement;
})(window);
```

- [ ] **Step 2: Sync do site.bundle.js**

Memory pravidlo: `site.bundle.js` musí být ručně udržovaný.

```bash
# Najdi, kde se loadují ostatní komponenty
grep -n "pm-chat\|pm-chat-modal" PmTracker.Web/wwwroot/js/site.bundle.js | head -5
```

Přidej 3 importy (chronology, buffer, pm-chat-stepper) na správné místo v bundle.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/js/components/pm-chat-stepper/pm-chat-stepper.js \
        PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "feat(stepper): <pm-chat-stepper> custom element + site.bundle.js sync"
```

---

### Task 5: Integrace do `_ChatModal.cshtml` + ViewModel rozšíření

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs`
- Modify: `PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs`
- Modify: `PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml`

- [ ] **Step 1: Rozšíř VM**

```csharp
// Add to VyjadreniModalViewModel (pokud Bublina VM existuje, krok VM ho má mít)
public sealed class KrokViewModel
{
    public int Poradi { get; init; }
    public string Label { get; init; } = "";
    public DateTime? BindingDatum { get; init; }
    public bool Fixni { get; init; } = true;
    public string? PredikatKey { get; init; }
}

// V root VyjadreniModalViewModel:
public IReadOnlyList<KrokViewModel> Kroky { get; init; } = Array.Empty<KrokViewModel>();
public bool CanAddAddon { get; init; }
```

- [ ] **Step 2: Builder plní Kroky + CanAddAddon**

V `VyjadreniModalViewModelBuilder.cs` (najdi kde se buduje root VM) přidat load kroků z existující tabulky + determinate `CanAddAddon` přes permission check (klíč je `records.schedule.add` nebo ekvivalent — zkontroluj `PermissionKeys`).

- [ ] **Step 3: Upravit Razor view**

Najdi `_ChatModal.cshtml` sekci s `pm-chat-modal__stepper`. Nahraď:

```cshtml
<pm-chat-stepper @(Model.CanAddAddon ? "can-add-addon" : "")
                 data-pm-chat-stepper>
</pm-chat-stepper>

<script>
  (function(){
    const el = document.querySelector('[data-pm-chat-stepper]');
    if (!el) return;
    el.setKroky(@Json.Serialize(Model.Kroky.Select(k => new {
        poradi = k.Poradi,
        label = k.Label,
        bindingDatum = k.BindingDatum?.ToString("O"),
        fixni = k.Fixni,
        predikatKey = k.PredikatKey
    })));
  })();
</script>
```

- [ ] **Step 4: Build + test**

```bash
dotnet build PmTracker.Web/PmTracker.Web.csproj --no-restore
dotnet test PmTracker.Tests.Unit -c Release --filter "FullyQualifiedName~Vyjadreni" --no-restore
```
Expected: 0 errors, all Vyjadreni tests pass.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Vyjadreni/VyjadreniModalViewModel.cs \
        PmTracker.Web/Services/ServiceDesk/VyjadreniModalViewModelBuilder.cs \
        PmTracker.Web/Views/Vyjadreni/_ChatModal.cshtml
git commit -m "feat(stepper): integrace <pm-chat-stepper> do chat modalu"
```

---

### Task 6: E2E manuální smoke (volitelné ale doporučené)

- [ ] **Step 1:** Pusť dev server, otevři projektový záznam s napojeným SD ticketem, otevři chat modal.

Ověř vizuálně:
- Stepper má 5 slotů (3 s labelem K3/K6/K4/K7/K10 dle typu ticketu + 2 „+ přidat nepovinný krok" pokud user má permission, jinak „—")
- Drag bublinu na krok → zelený outline pokud OK, červený + cursor:not-allowed pokud chronology blokuje
- Drop → event `pm-chat-stepper-drop` se dispatchne, binding update flow proběhne
- Tooltip u red outline říká „Krok #X má binding z YYYY-MM-DD"

- [ ] **Step 2: Finální commit drobností**

```bash
git status --short
# pokud něco → git add + commit "chore(stepper): manuální smoke fixes"
```

---

## Deliverable

Po dokončení Feature B codebase obsahuje:
- ✅ `<pm-chat-stepper>` custom element dědící styly z gov-stepper
- ✅ Chronology pure funkce s 4 unit testy (JS + C# port)
- ✅ Buffer 5 pevných slotů (3 fixní + 2 add-on dle permission)
- ✅ Integrace do chat modalu přes Razor partial
- ✅ Drag&drop s vizuálním feedbackem (green/red outline + tooltip)
- ✅ Cascade detection (dropdown poradi se posune pokud bubble předbíhá)

## Co NENÍ ve Sprintu B

- ❌ Autoconfirm cascade (zatím jen detekce + event) — user musí potvrdit v confirm dialogu (budoucí úloha)
- ❌ Full ARIA a11y compliance pro screen readers (basic ARIA attributes nastavené, ale celý ARIA pattern je Plan D+)
- ❌ Tests přes Playwright — unit level stačí, E2E manual je v Task 6
