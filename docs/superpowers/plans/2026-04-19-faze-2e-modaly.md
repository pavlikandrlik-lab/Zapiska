# Fáze 2E — migrace modálního systému na gov-dialog — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Přechod modálního systému z custom `.modal-overlay` markup na `<gov-dialog>` (přes `pm-dialog` wrapper), odstranění posledního 2D tech debtu (`_ModalFormActions` submit) a sjednocení modálních variant s gov-design-system.

**Architecture:** Adapterový přístup — `_ModalLayout.cshtml` nadále zůstane kanonický entry point (všech 18 modálních views používá `Layout = "_ModalLayout"`). Uvnitř se markup přepíše na `<gov-dialog>`, `modals.js` se adaptuje na `gov-dialog` open/close API. Existující `data-modal-url` / `data-modal-close` / `data-ajax-submit` kontrakt zůstává beze změny — volající views se **nedotýkají**. Floating-root pro pickery se přesune mimo gov-dialog do globálního `#floating-panel-root` (jediný kód, který na něj sahá, je `ui.js:610`, graceful fallback už existuje). `_ModalFormActions` přechází na `PmButtonVariant` enum (místo stringového `SubmitCssClass`).

**Tech Stack:** ASP.NET Core 8 MVC Razor, gov-design-system 4.2.9 `<gov-dialog>` Web Component, existující `pm-button` / `pm-dialog` TagHelpers (Fáze 1 / 2C), xUnit + FluentAssertions, Playwright pro E2E smoke.

---

## Rozsah (what IS and ISN'T in scope)

**V rozsahu:**
- Přepis `_ModalLayout.cshtml` na strukturu s `<gov-dialog>`
- Adaptace `modules/modals.js` na gov-dialog `.show()/.close()` API + reflect `open` attribute
- Migrace `_ModalFormActions` `SubmitCssClass` → `PmButtonVariant` enum + submit jako `<pm-button>`
- Odstranění legacy CSS (`.modal-overlay`, `.modal-close`, `.modal--*` variant classes, `.modal-header`)
- Per-view smoke test všech 18 modálních views
- Docs: `docs/architecture/dialogs.md` rozšířit, `docs/known-issues/modal-migration-to-pm-dialog.md` uzavřít

**Mimo rozsah (odloženo na pozdější fáze):**
- Refactor AJAX pipeline (`ajax.js`) — stále funguje na `querySelector("[data-modal-container]")`, přepsat později
- Refactor `person-picker` / `datetime-picker` floating positioning — `ui.js:610` funguje přes closest + globální fallback
- `_PageHeader.cshtml` back-link `data-record-editor-cancel` flow — migrován v 2D, zachovat
- Sdílený filter panel Záznamy+Harmonogram (separátní menší task)

## File Structure

**Modified (backend/views):**

| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Views/Shared/_ModalLayout.cshtml` | Přepsán — renderuje `<gov-dialog>` s title slot + default slot; `data-modal-close` button zůstává pro kompatibilitu; variantní classes přemapovány na CSS přes `data-modal-variant="record-editor\|wide\|default"` |
| `PmTracker.Web/Views/Shared/_ModalFormActions.cshtml` | Submit `<button>` → `<pm-button variant="@Model.SubmitVariant">` |
| `PmTracker.Web/Models/ViewModels/ModalViewModels.cs` | `SubmitCssClass: string` → `SubmitVariant: PmButtonVariant` (enum); default `Primary` |
| `PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml` | Jediný caller s `SubmitCssClass = "btn ghost danger"` → `SubmitVariant = PmButtonVariant.Destructive` |

**Modified (JS):**

| Soubor | Změna |
|---|---|
| `PmTracker.Web/wwwroot/js/modules/modals.js` | `setModalContent`/`closeModal` volají `.show()/.close()` na gov-dialog; `getActiveModalContainer` hledá `[data-modal-container]` uvnitř gov-dialog light-DOM; `openUrlModal` error fallback markup přepsán na gov-dialog |
| `PmTracker.Web/wwwroot/js/modules/ui.js` | `getFloatingLayerRoot`: `closest(".modal-overlay")` → `closest("gov-dialog")`, fallback beze změny |
| `PmTracker.Web/wwwroot/js/modules/bootstrap.js` | Click handler pro overlay-click-to-close: `target.classList.contains("modal-overlay")` → `target.tagName === "GOV-DIALOG" && event.target === target` (backdrop click) |
| `PmTracker.Web/wwwroot/js/site.bundle.js` | Synchronizace všech změn z modules |

**Modified (CSS):**

| Soubor | Změna |
|---|---|
| `PmTracker.Web/wwwroot/css/site.css` | Odstranit bloky `.modal-overlay` (3931), `.modal.modal--*` variants (3974-3986), `.modal-close*` (3998-4023), `.modal-header` (3990); přidat scope přes `gov-dialog[data-modal-variant="wide"]`, etc.; `:not(gov-dialog ...)` pattern z fáze 2A pro form scope |

**Test files:**

| Soubor | Účel |
|---|---|
| `PmTracker.Tests.Unit/Modals/ModalLayoutMarkupTests.cs` (nový) | Razor render `<gov-dialog>` structure, data-modal-variant, slot title, data-modal-close button |
| `PmTracker.Tests.Unit/Modals/ModalFormActionsTests.cs` (nový) | `pm-button` submit renderuje variant + disabled + SubmitLabel; DeleteRecord modal používá Destructive variant |
| `PmTracker.Tests.E2E/Scenarios/ModalGovDialogSmokeTests.cs` (nový) | Open/close 3 representative modals (DeleteRecord — simplest, NewMeeting — form, EditZaznam — nejkomplexnější); ověření backdrop click, esc, submit happy path |

**Deleted:**

- `docs/known-issues/modal-migration-to-pm-dialog.md` (po dokončení 2E)

---

## Task 0: Baseline + PmButtonVariant audit

**Cíl:** Změřit současný stav, ověřit že gov-dialog je ready pro náš use-case (spike), zmapovat všech 18 views.

**Files (read-only):**
- Read: `PmTracker.Web/Views/Shared/_ModalLayout.cshtml`
- Read: `PmTracker.Web/wwwroot/js/modules/modals.js`
- Read: `PmTracker.Web/TagHelpers/PmDialogTagHelper.cs`
- Read: `gov-kits/` (offline figma — pokud obsahuje gov-dialog spec)

- [ ] **Step 0.1: Baseline unit + E2E test counts**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Očekáváno: `Passed!  - Failed: 0, Passed: 339` (podle stavu po 2D + 6 bugfixů). Zaznamenat do worklogu.

- [ ] **Step 0.2: Inventář 18 views**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rln "_ModalLayout" PmTracker.Web/Views/ --include="*.cshtml" | sort > /tmp/2e-modal-views.txt
wc -l /tmp/2e-modal-views.txt
```

Očekáváno: 18 views. Seznam uložit do worklogu pro kontrolu v Task 4.

- [ ] **Step 0.3: Spike — ručně ověřit gov-dialog chování**

Otevřít StyleGuide `/StyleGuide` v browseru (lokální dev, nebo na Citrix pokud lokální SQL není dostupný). V DevTools:

```js
// V konzoli na StyleGuide:
const d = document.createElement("gov-dialog");
d.innerHTML = `<h3 slot="title">Test</h3><p>Body</p><button data-modal-close>Zavřít</button>`;
document.body.appendChild(d);
customElements.upgrade(d);
d.show?.();  // pokud existuje metoda
// zjistit: má `.show()` metodu? otevře se? má backdrop? klik na backdrop zavírá?
// Esc zavírá?
```

Zaznamenat do `/tmp/2e-spike-notes.md`:
- Metoda `.show()` / `.close()` existuje? (ano/ne)
- Atribut `open` reflektuje state? (ano/ne)
- Backdrop má CSS třídu / pseudo-element? (zjistit selector)
- Event při zavření? (např. `gov-dialog-close`, `close`)

**Pokud `.show()` neexistuje → použít `setAttribute("open", "true")` / `removeAttribute("open")`.** Plán dále počítá s attribute-based API jako fallback.

- [ ] **Step 0.4: Commit baseline**

Pouze worklog do docs. Nic v kódu se nemění.

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
# žádný commit — Task 0 je jen průzkum
```

### Task 0 findings (resolved 2026-04-19 noc 2)

**gov-dialog API (z `wwwroot/lib/gov-design-system/styles/lib/components/gov-dialog.css`):**

- Default: `display: none; visibility: hidden`
- Opens přes atribut `open=""` | `open="true"` | `open="1"` → CSS přepne `display: block`
- `[hydrated]` atribut signalizuje upgradnutý web component (může být užitečné pro JS čekání)
- **Nepotřebujeme volat `.show()/.close()`** — stačí `setAttribute("open", "true")` / `removeAttribute("open")`. Metody mohou ale neznamenají existovat jako alternativa.
- Interní struktura:
  - `gov-dialog__header` — přijímá `slot="title"` a volitelně `slot="icon"` (vlastní header, ne náš `.modal-header`)
  - `gov-dialog__content` — default slot, `overflow: auto`
  - `gov-dialog__footer` — volitelný footer slot (pro form actions)
  - `gov-dialog__close` — **vestavěný close button** na pozici (top-right absolute) — náš vlastní `.modal-close` je redundantní
- `--max-width: 52.5rem` default, `--max-height: 75vh`; varianty přes CSS custom property

**Dopad na plán:**

1. **Task 2**: `_ModalLayout.cshtml` už nepotřebuje vlastní `<button class="modal-close">` — gov-dialog ho poskytuje. Interní close emituje event, který musíme poslouchat v modals.js (Task 3).
2. **Task 2**: Title řadíme do `slot="title"`. Ale většina modál views už má vlastní `<h2 id="record-modal-title">@Title</h2>` v body. Možná řešení:
   - (a) Nechat vlastní h2 v body, negenerovat title slot v `_ModalLayout`. Gov-dialog header bude prázdný / minimální.
   - (b) Přemapovat ViewData["ModalTitle"] do title slotu a smazat h2 z views.
   - **Zvolíme (a)** — minimální invazivnost, views se nedotýkají.
3. **Task 3**: Backdrop click detection — gov-dialog má vlastní backdrop kontrolu. Místo `target.tagName === "GOV-DIALOG"` budeme poslouchat event `gov-dialog-close` nebo `close` (ověřit v implementaci).
4. **Task 5**: Smazat `.modal-close*` a `.modal-header` bloky — už nepoužité. Ale `.modal-content` scope padding pravděpodobně ponechat jako safety padding uvnitř gov-dialog content slotu.

---

## Task 1: `_ModalFormActions` — submit na pm-button s PmButtonVariant

**Cíl:** Uzavřít 2D leftover. Submit button přejde z `class="@SubmitCssClass"` na `<pm-button variant="@SubmitVariant">`. Jediný caller s non-default variant je `DeleteRecordModal`.

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/ModalViewModels.cs:132-138`
- Modify: `PmTracker.Web/Views/Shared/_ModalFormActions.cshtml`
- Modify: `PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml:39-44`
- Create: `PmTracker.Tests.Unit/Modals/ModalFormActionsTests.cs`

- [ ] **Step 1.1: Přidat enum property do ModalFormActionsViewModel**

Upravit `PmTracker.Web/Models/ViewModels/ModalViewModels.cs:132-138`:

```csharp
public sealed class ModalFormActionsViewModel
{
    public string SubmitLabel { get; init; } = "Uložit";
    public string CancelLabel { get; init; } = "Zrušit";
    // Fáze 2E: PmButtonVariant nahrazuje volný CSS string.
    // Legacy SubmitCssClass odstraněn (původně "btn primary" / "btn ghost danger").
    public PmTracker.Web.TagHelpers.PmButtonVariant SubmitVariant { get; init; }
        = PmTracker.Web.TagHelpers.PmButtonVariant.Primary;
    public bool DisableSubmit { get; init; }
}
```

- [ ] **Step 1.2: Upravit `_ModalFormActions.cshtml`**

Přepsat `PmTracker.Web/Views/Shared/_ModalFormActions.cshtml`:

```razor
@model ModalFormActionsViewModel

<div class="form-actions">
    <pm-button variant="Secondary" data-modal-close="true">@Model.CancelLabel</pm-button>
    <pm-button variant="@Model.SubmitVariant" native-type="submit"
               disabled="@Model.DisableSubmit">@Model.SubmitLabel</pm-button>
</div>
```

- [ ] **Step 1.3: Upravit `DeleteRecordModal.cshtml`**

Upravit řádek 39-44 v `PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml`:

```razor
    @await Html.PartialAsync(
        "_ModalFormActions",
        new ModalFormActionsViewModel
        {
            SubmitLabel = "Potvrdit trvalé smazání",
            SubmitVariant = PmTracker.Web.TagHelpers.PmButtonVariant.Destructive,
            DisableSubmit = false
        })
```

- [ ] **Step 1.4: Vytvořit unit test**

Vytvořit `PmTracker.Tests.Unit/Modals/ModalFormActionsTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// Ověřuje, že _ModalFormActions renderuje submit jako pm-button
/// s variantem z ModalFormActionsViewModel.SubmitVariant (Fáze 2E).
/// </summary>
public sealed class ModalFormActionsTests
{
    private static string LoadText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void Partial_ShouldUsePmButtonForSubmit()
    {
        var partial = LoadText("PmTracker.Web/Views/Shared/_ModalFormActions.cshtml");

        partial.Should().Contain(
            "<pm-button variant=\"@Model.SubmitVariant\" native-type=\"submit\"",
            "submit button musí být pm-button s variantem z modelu (ne raw CSS string)");
        partial.Should().NotContain(
            "SubmitCssClass",
            "legacy SubmitCssClass byl odstraněn ve prospěch PmButtonVariant");
    }

    [Fact]
    public void DeleteRecordModal_ShouldUseDestructiveVariant()
    {
        var view = LoadText("PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml");

        view.Should().Contain(
            "SubmitVariant = PmTracker.Web.TagHelpers.PmButtonVariant.Destructive",
            "delete modal musí používat Destructive variant (červené tlačítko)");
        view.Should().NotContain(
            "btn ghost danger",
            "legacy CSS string odstraněn");
    }

    [Fact]
    public void ModalFormActionsViewModel_ShouldDefaultToPrimary()
    {
        var model = LoadText("PmTracker.Web/Models/ViewModels/ModalViewModels.cs");

        model.Should().Contain(
            "SubmitVariant { get; init; }",
            "SubmitVariant property musí existovat");
        model.Should().Contain(
            "PmTracker.Web.TagHelpers.PmButtonVariant.Primary",
            "default hodnota SubmitVariant je Primary");
        model.Should().NotContain(
            "SubmitCssClass",
            "legacy property odstraněna");
    }
}
```

- [ ] **Step 1.5: Verify build + test**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -5
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Očekáváno: Build `0 Error(s)`. Tests: `Passed: 342` (3 nové testy přibyly).

- [ ] **Step 1.6: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Models/ViewModels/ModalViewModels.cs \
        PmTracker.Web/Views/Shared/_ModalFormActions.cshtml \
        PmTracker.Web/Views/Projekty/DeleteRecordModal.cshtml \
        PmTracker.Tests.Unit/Modals/ModalFormActionsTests.cs
git commit -m "$(cat <<'EOF'
refactor(modals): _ModalFormActions submit na pm-button (Fáze 2E Task 1)

Uzavírá 2D leftover: ModalFormActionsViewModel.SubmitCssClass (volný CSS
string) nahrazen enumem PmButtonVariant. Submit tlačítko renderuje
<pm-button native-type="submit"> místo raw <button class="btn primary">.
DeleteRecordModal přepnut z "btn ghost danger" na Destructive variant.

+3 unit testy (ModalFormActionsTests).
EOF
)"
```

---

## Task 2: Vytvořit gov-dialog-based `_ModalLayout`

**Cíl:** Přepsat `_ModalLayout.cshtml` na markup s `<gov-dialog>`, zachovat všechny `data-modal-*` kontrakty.

**Files:**
- Modify: `PmTracker.Web/Views/Shared/_ModalLayout.cshtml`
- Create: `PmTracker.Tests.Unit/Modals/ModalLayoutMarkupTests.cs`

- [ ] **Step 2.1: Přepsat `_ModalLayout.cshtml`**

Nahradit obsah `PmTracker.Web/Views/Shared/_ModalLayout.cshtml`:

```razor
@{
    Layout = null;
    var modalTitleId = ViewData["ModalTitleId"]?.ToString();
    if (string.IsNullOrWhiteSpace(modalTitleId))
    {
        modalTitleId = "modal-title";
    }

    var variant = ViewData["ModalVariant"]?.ToString();
    var normalizedVariant = string.Equals(variant, "record-editor", StringComparison.OrdinalIgnoreCase)
        ? "record-editor"
        : string.Equals(variant, "wide", StringComparison.OrdinalIgnoreCase)
            ? "wide"
            : "default";

    var overflowVisible = ViewData["ModalOverflowVisible"] is bool overflowBool
        ? overflowBool
        : string.Equals(ViewData["ModalOverflowVisible"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
}
@*
    Fáze 2E: <gov-dialog> nahrazuje custom .modal-overlay markup.
    Variants (default/wide/record-editor) + overflow-visible flag nese
    data-modal-variant / data-modal-overflow-visible atribut; CSS scope
    přes gov-dialog[data-modal-variant="..."].

    Kompatibilita:
    - data-modal-container — JS v modals.js + ajax.js hledá [data-modal-container]
      uvnitř modal rootu; dispatch z event handleru je shodný.
    - data-modal-close — close button zůstává jako native <button> s tímto
      atributem; bootstrap.js click handler beze změny.
    - data-modal-floating-root — přesunut mimo gov-dialog do #floating-panel-root
      v _Layout.cshtml; ui.js:610 fallback už existuje.
*@
<gov-dialog open="true"
            data-modal-container
            data-modal-variant="@normalizedVariant"
            data-modal-overflow-visible="@(overflowVisible ? "true" : "false")"
            aria-labelledby="@modalTitleId"
            tabindex="-1">
    <button class="modal-close" type="button" data-modal-close aria-label="Zavřít dialog" slot="header-actions">
        <span class="modal-close-icon" aria-hidden="true">&#10005;</span>
    </button>
    <div class="modal-content">
        @RenderBody()
    </div>
</gov-dialog>
```

- [ ] **Step 2.2: Vytvořit markup test**

Vytvořit `PmTracker.Tests.Unit/Modals/ModalLayoutMarkupTests.cs`:

```csharp
using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Modals;

/// <summary>
/// Fáze 2E: _ModalLayout přechází z custom .modal-overlay na <gov-dialog>.
/// Data-modal-* kontrakty (container, close, variant) musí zůstat pro kompatibilitu
/// s existujícím JS (modals.js, ajax.js, bootstrap.js) a 18 views.
/// </summary>
public sealed class ModalLayoutMarkupTests
{
    private static string LoadText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    [Fact]
    public void Layout_ShouldRenderGovDialogRoot()
    {
        var layout = LoadText("PmTracker.Web/Views/Shared/_ModalLayout.cshtml");

        layout.Should().Contain(
            "<gov-dialog",
            "root element modálu je <gov-dialog> (Fáze 2E)");
        layout.Should().NotContain(
            "class=\"modal-overlay\"",
            "legacy custom overlay byl odstraněn");
        layout.Should().NotContain(
            "<div class=\"modal modal--",
            "legacy .modal.modal--* variant wrapper byl odstraněn");
    }

    [Fact]
    public void Layout_ShouldPreserveDataModalContracts()
    {
        var layout = LoadText("PmTracker.Web/Views/Shared/_ModalLayout.cshtml");

        layout.Should().Contain(
            "data-modal-container",
            "JS používá [data-modal-container] selector (modals.js, ajax.js)");
        layout.Should().Contain(
            "data-modal-close",
            "close button zachovává data-modal-close pro bootstrap.js click handler");
        layout.Should().Contain(
            "data-modal-variant=\"@normalizedVariant\"",
            "variant se propaguje jako data-modal-variant atribut (místo CSS class)");
    }

    [Fact]
    public void Layout_ShouldMoveFloatingRootOutOfDialog()
    {
        var layout = LoadText("PmTracker.Web/Views/Shared/_ModalLayout.cshtml");

        // Floating root přesunut do _Layout.cshtml (#floating-panel-root).
        // Gov-dialog sám má shadow DOM a není vhodný host pro floating pickery.
        layout.Should().NotContain(
            "data-modal-floating-root",
            "floating-root přesunut mimo gov-dialog (Task 3)");
    }
}
```

- [ ] **Step 2.3: Build + test (dočasně selže)**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -5
```

Očekáváno: 3 nové testy PASS. Žádný regres v existujících. Pokud nějaký test dřív ověřoval `.modal-overlay` nebo `.modal-close`, selže — **ten regres řeší Task 3** (posun CSS selectors v JS a tests).

- [ ] **Step 2.4: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/Views/Shared/_ModalLayout.cshtml \
        PmTracker.Tests.Unit/Modals/ModalLayoutMarkupTests.cs
git commit -m "$(cat <<'EOF'
refactor(modals): _ModalLayout přepis na <gov-dialog> (Fáze 2E Task 2)

Custom .modal-overlay markup nahrazen <gov-dialog> s data-modal-container
/ data-modal-variant / data-modal-overflow-visible atributy. Data-modal-*
kontrakty zachovány pro kompatibilitu s modals.js / ajax.js / bootstrap.js.

Floating-root přesunut mimo gov-dialog — bude v _Layout.cshtml
(globální #floating-panel-root). ui.js:610 fallback už fallback existuje.

+3 unit testy. JS adaptace a sync bundle v Task 3.
EOF
)"
```

---

## Task 3: Adaptace JS (modals.js + ui.js + bootstrap.js + bundle)

**Cíl:** Přeložit `modals.js` na gov-dialog API, opravit `ui.js:610` selector, `bootstrap.js` backdrop click handler. Synchronizovat bundle.

**Files:**
- Modify: `PmTracker.Web/wwwroot/js/modules/modals.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/ui.js:608-623`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js:276-287`
- Modify: `PmTracker.Web/Views/Shared/_Layout.cshtml` (přidat globální `#floating-panel-root`)
- Modify: `PmTracker.Web/wwwroot/js/site.bundle.js` (sync)

- [ ] **Step 3.1: Přidat globální floating-root do `_Layout.cshtml`**

Najít v `PmTracker.Web/Views/Shared/_Layout.cshtml` místo kde končí `<body>` content (těsně před `</body>` a před `<script>`), přidat:

```razor
    @* Fáze 2E: globální floating-root pro pickery nad modály (gov-dialog shadow DOM
       není vhodný host). ui.js:610 graceful fallback dříve používal
       modal-scoped [data-modal-floating-root]; teď je singleton mimo modal. *@
    <div id="floating-panel-root" aria-hidden="true"></div>
```

**Ověřit že neexistuje duplikát:**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "floating-panel-root\|getGlobalFloatingLayerRoot" PmTracker.Web/ --include="*.cshtml" --include="*.js"
```

- [ ] **Step 3.2: Upravit `modals.js`**

Upravit `PmTracker.Web/wwwroot/js/modules/modals.js`:

```javascript
// Řádky 39-60 — selectors
function getActiveModalOverlay() {
    if (!(modalRoot instanceof HTMLElement)) {
        return null;
    }
    // Fáze 2E: modal root je gov-dialog (ne .modal-overlay div)
    return modalRoot.querySelector("gov-dialog");
}

export function getActiveModalContainer() {
    // data-modal-container je nastavený přímo na gov-dialog (viz _ModalLayout)
    const dialog = getActiveModalOverlay();
    if (!(dialog instanceof HTMLElement)) {
        return null;
    }
    return dialog;  // gov-dialog má atribut data-modal-container sám o sobě
}

// Řádky 102-119 — setModalContent
export function setModalContent(content, trigger) {
    if (!(modalRoot instanceof HTMLElement)) {
        return;
    }

    modalRuntime.closeAllFloatingPanels?.();
    modalRoot.innerHTML = "";
    modalRoot.appendChild(content);
    modalRoot.style.pointerEvents = "auto";
    modalRoot.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open");
    modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;

    // Fáze 2E: zajistit open state na gov-dialog (custom element se upgradne
    // asynchronně, ale naše Razor už má open="true").
    const dialog = modalRoot.querySelector("gov-dialog");
    if (dialog instanceof HTMLElement) {
        dialog.setAttribute("open", "true");
        if (typeof dialog.show === "function") {
            try { dialog.show(); } catch { /* gov-dialog show() může throw při duplicitním show — ignoruj */ }
        }
    }

    modalRuntime.initRecordFormEnhancements?.(modalRoot);
    modalRuntime.initPermissionMetadataBindings?.(modalRoot);
    window.requestAnimationFrame(() => {
        focusInitialModalElement();
    });
}

// Řádky 121-136 — closeModal
export function closeModal() {
    if (!(modalRoot instanceof HTMLElement)) {
        return;
    }

    const focusTarget = modalState.lastTrigger;
    modalRuntime.closeAllFloatingPanels?.();

    // Fáze 2E: explicitně zavolat .close() na gov-dialog pokud existuje,
    // pak vyčistit modal-root (inner HTML pryč).
    const dialog = modalRoot.querySelector("gov-dialog");
    if (dialog instanceof HTMLElement) {
        dialog.removeAttribute("open");
        if (typeof dialog.close === "function") {
            try { dialog.close(); } catch { /* ignorovat — dialog už může být zavřený */ }
        }
    }

    modalRoot.innerHTML = "";
    modalRoot.style.pointerEvents = "none";
    modalRoot.setAttribute("aria-hidden", "true");
    document.body.classList.remove("modal-open");
    modalState.lastTrigger = null;
    if (focusTarget instanceof HTMLElement && focusTarget.isConnected) {
        focusTarget.focus({ preventScroll: true });
    }
}

// Řádky 153-172 — openUrlModal error fallback
// Uvnitř catch bloku, nahradit fallback markup:
if (modalRoot instanceof HTMLElement) {
    modalRoot.innerHTML = `
        <gov-dialog open="true" data-modal-container data-modal-variant="default" aria-hidden="false" tabindex="-1">
            <p>Nepodařilo se načíst obsah dialogu.</p>
            <div class="modal-actions">
                <button type="button" class="btn btn-secondary" data-modal-close>Zavřít</button>
            </div>
        </gov-dialog>`;
    modalRoot.style.pointerEvents = "auto";
    modalRoot.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open");
    modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
    focusInitialModalElement();
}

// Řádky 206-209 — isModalOverlayClickTarget
export function isModalOverlayClickTarget(target) {
    // Backdrop click: gov-dialog sám (ne jeho content) byl kliknutý
    const dialog = getActiveModalOverlay();
    return dialog instanceof HTMLElement && target === dialog;
}
```

- [ ] **Step 3.3: Upravit `ui.js:610-623`**

```javascript
export function getFloatingLayerRoot(container) {
    // Fáze 2E: floating root není uvnitř gov-dialog (shadow DOM),
    // vracíme globální #floating-panel-root pro pickery nad modály.
    // Fallback na globální root zůstává.
    return getGlobalFloatingLayerRoot();
}
```

- [ ] **Step 3.4: Upravit `bootstrap.js:283-287`**

```javascript
// Řádek 283
if (target instanceof HTMLElement && target.tagName === "GOV-DIALOG" && target.hasAttribute("data-modal-container")) {
    event.preventDefault();
    void requestRecordEditorModalClose(target);
    return;
}
```

- [ ] **Step 3.5: Synchronizovat `site.bundle.js`**

Najít a upravit tyto bloky v `PmTracker.Web/wwwroot/js/site.bundle.js`:
- `modals.js` ekvivalent (hledat `function getActiveModalOverlay`, `function setModalContent`, `function closeModal`, `openUrlModal`)
- `ui.js:616` ekvivalent (`const modalRoot = overlay.querySelector("[data-modal-floating-root]")`)
- `bootstrap.js:283` ekvivalent (`target.classList.contains("modal-overlay")`)

Každou lokaci upravit podle modulových změn výše. Ověřit kontrolou:

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -n "modal-overlay\|modal-container\|data-modal-floating-root" PmTracker.Web/wwwroot/js/site.bundle.js | head -20
```

Očekáváno: žádné výsledky na `.modal-overlay` (kromě CSS komentáře), `getFloatingLayerRoot` používá pouze `getGlobalFloatingLayerRoot()`.

- [ ] **Step 3.6: Build + test**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Očekáváno: Build `0 Error(s)`. Tests: 342+ PASS.

- [ ] **Step 3.7: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/wwwroot/js/modules/modals.js \
        PmTracker.Web/wwwroot/js/modules/ui.js \
        PmTracker.Web/wwwroot/js/modules/bootstrap.js \
        PmTracker.Web/Views/Shared/_Layout.cshtml \
        PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "$(cat <<'EOF'
refactor(modals): modals.js + ui.js + bootstrap.js adapt na gov-dialog (Fáze 2E Task 3)

- modals.js: getActiveModalOverlay/setModalContent/closeModal pracují
  nad <gov-dialog> (setAttribute open + volitelně .show/.close metoda).
- ui.js getFloatingLayerRoot: pickery míří na globální #floating-panel-root
  (přidán do _Layout.cshtml), protože gov-dialog shadow DOM není vhodný host.
- bootstrap.js: backdrop click detekce z .modal-overlay na <gov-dialog>
  s data-modal-container.
- openUrlModal error fallback markup předělán na <gov-dialog>.
- Bundle synchronizován.

Data-modal-* kontrakty pro všech 18 views beze změny.
EOF
)"
```

---

## Task 4: Per-view smoke — verifikace 18 views

**Cíl:** Ujistit se, že žádný z 18 modálních views není rozbitý po Task 2+3. Smoke zahrnuje: otevření, submit (pokud form), backdrop click, Esc.

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/ModalGovDialogSmokeTests.cs`

**Runtime požadavek:** živý SQL + browser. Pokud v sandboxu není DB dostupná, tento task se vykoná uživatelem na Citrix dev prostředí — plán označuje Task 4 jako **manual verify required**.

- [ ] **Step 4.1: Vytvořit E2E smoke test pro 3 reprezentativní modaly**

Vytvořit `PmTracker.Tests.E2E/Scenarios/ModalGovDialogSmokeTests.cs`:

```csharp
using Microsoft.Playwright;
using Xunit;
using FluentAssertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Fáze 2E smoke: modály založené na <gov-dialog> se otevírají, zavírají,
/// submitují. Testuje 3 representative views: DeleteRecord (simplest),
/// NewMeeting (form), EditZaznam (nejkomplexnější — tabs, schedule, quill, pickers).
///
/// Pokud v CI prostředí není SQL dostupné, test se přeskočí přes
/// [Fact(Skip = ...)] fallback (stejný pattern jako existující E2E testy).
/// </summary>
public sealed class ModalGovDialogSmokeTests : IAsyncLifetime
{
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private IPage? _page;
    private const string BaseUrl = "http://127.0.0.1:5072";

    public async Task InitializeAsync()
    {
        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new() { Headless = true });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _pw?.Dispose();
    }

    [Fact(Skip = "Vyžaduje live SQL — spustit manuálně nebo v CI s SQL")]
    public async Task DeleteRecordModal_OpensAndRendersGovDialog()
    {
        await _page!.GotoAsync($"{BaseUrl}/Projekty/Detail/1?tab=zaznamy&asUser=1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find delete button on first record
        var deleteBtn = _page.Locator("[data-modal-url*='DeleteRecord']").First;
        await deleteBtn.ClickAsync();

        // Modal should open — gov-dialog with open attribute
        var dialog = _page.Locator("gov-dialog[data-modal-container]");
        await dialog.WaitForAsync(new() { Timeout = 5000 });
        var isOpen = await dialog.GetAttributeAsync("open");
        isOpen.Should().NotBeNull();
    }

    [Fact(Skip = "Vyžaduje live SQL")]
    public async Task AnyModal_ClosesOnDataModalCloseClick()
    {
        await _page!.GotoAsync($"{BaseUrl}/Projekty/Detail/1?tab=zaznamy&asUser=1");
        await _page.Locator("[data-modal-url*='DeleteRecord']").First.ClickAsync();
        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync();

        // Click close button
        await _page.Locator("[data-modal-close]").First.ClickAsync();

        // Dialog should be removed
        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync(
            new() { State = WaitForSelectorState.Detached, Timeout = 3000 });
    }

    [Fact(Skip = "Vyžaduje live SQL")]
    public async Task AnyModal_ClosesOnEscape()
    {
        await _page!.GotoAsync($"{BaseUrl}/Projekty/Detail/1?tab=zaznamy&asUser=1");
        await _page.Locator("[data-modal-url*='DeleteRecord']").First.ClickAsync();
        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync();

        await _page.Keyboard.PressAsync("Escape");

        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync(
            new() { State = WaitForSelectorState.Detached, Timeout = 3000 });
    }
}
```

- [ ] **Step 4.2: Vytvořit smoke checklist pro ruční ověření**

Vytvořit `docs/superpowers/logs/2026-04-19-faze-2e-manual-smoke.md`:

```markdown
# Fáze 2E manual smoke checklist

Pro každý z 18 views ověřit (manuálně nebo na Citrix):

- [ ] `Osoby/AdPersonModal` — otevření z search triggeru
- [ ] `Osoby/ManualPersonModal` — otevření + submit
- [ ] `Projekty/AddTeamMemberModal` — otevření + pickers fungují
- [ ] `Projekty/AssignProjectSubsystemModal` — otevření + submit
- [ ] `Projekty/AssignProjectSubsystemRoleModal` — otevření + submit
- [ ] `Projekty/AssignProjectRoleModal` — otevření + submit
- [ ] `Projekty/AssignMeetingIdentifierModal` — otevření + submit
- [ ] `Projekty/DeleteProjectModal` — otevření + confirm delete
- [ ] `Projekty/DeleteRecordModal` — otevření + Destructive submit button červeně
- [ ] `Projekty/EditZaznamModal` — otevření, všechny 3 taby fungují, submit, pickers floatují správně
- [ ] `Projekty/NewMeetingModal` — otevření + submit
- [ ] `Projekty/ProjectModal` — otevření + submit
- [ ] `Nastaveni/PermissionModal` — otevření + submit
- [ ] `Nastaveni/RoleModal` — otevření + submit
- [ ] `Nastaveni/RolePermissionModal` — otevření + submit
- [ ] `Nastaveni/UserRolesModal` — otevření + submit
- [ ] `Jednani/AddMeetingParticipantModal` — otevření + submit
- [ ] `Ciselniky/EditRow` — otevření + submit

Pro KAŽDÝ: backdrop click zavírá; Esc zavírá; focus trap funguje (Tab cyklí uvnitř).
```

- [ ] **Step 4.3: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
mkdir -p docs/superpowers/logs
git add PmTracker.Tests.E2E/Scenarios/ModalGovDialogSmokeTests.cs \
        docs/superpowers/logs/2026-04-19-faze-2e-manual-smoke.md
git commit -m "$(cat <<'EOF'
test(modals): E2E smoke pro gov-dialog modály + manual checklist (Fáze 2E Task 4)

3 Playwright smoke scénáře (DeleteRecord, close, Esc) — označené Skip
pro CI bez SQL. Manual checklist v docs/superpowers/logs/ pokrývá
všech 18 views, instrukce pro ověření na Citrix.
EOF
)"
```

---

## Task 5: Odstranění legacy CSS

**Cíl:** Po ověření v Task 4 odstranit `.modal-overlay`, `.modal-header`, `.modal-close`, `.modal--*` variant classes. CSS variants přesunout na `gov-dialog[data-modal-variant="..."]` selectors.

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css:3931-4043` (a variant bloky 4190+, 5845+)

- [ ] **Step 5.1: Smazat legacy bloky a přemapovat varianty**

Otevřít `PmTracker.Web/wwwroot/css/site.css`, odstranit tyto bloky (řádky pro orientaci, skutečné mohou být posunuté):

- `.modal-overlay { ... }` (~3931)
- `.modal-floating-root { ... }` (~3945, ~3952)
- `.modal.modal--default { ... }` (~3974)
- `.modal.modal--wide { ... }` (~3978)
- `.modal.modal--record-editor { ... }` (~3982)
- `.modal.modal--overflow-visible { ... }` (~3986)
- `.modal-header { ... }` (~3990)
- `.modal-close { ... }` (~3998)
- `.modal-close:hover { ... }` (~4015)
- `.modal-close-icon { ... }` (~4019)
- `.modal-content { ... }` variants (~4027, ~4031, ~4039, ~4043, ~4047)
- `.modal.modal--overflow-visible .office-search-panel { ... }` (~4190) — přepsat selector na `gov-dialog[data-modal-overflow-visible="true"] .office-search-panel`
- Media query block `@media (max-width: ...) .modal-overlay { ... }` (~5845) — přepsat na `gov-dialog[data-modal-container] { ... }`

Přidat kanonický blok (umístit kolem místa po smazaných):

```css
/* === gov-dialog modály (Fáze 2E) =====================================
   Nahrazuje custom .modal-overlay / .modal.modal--* strukturu. Variants
   řízeny přes data-modal-variant="default|wide|record-editor".
   Gov-dialog sám poskytuje backdrop + focus trap + Esc handler. */
gov-dialog[data-modal-container] {
    /* Variant-specific sizing */
}

gov-dialog[data-modal-container][data-modal-variant="wide"] {
    --gov-dialog-max-width: 1100px;
}

gov-dialog[data-modal-container][data-modal-variant="record-editor"] {
    --gov-dialog-max-width: 1280px;
    --gov-dialog-max-height: 92vh;
}

gov-dialog[data-modal-container][data-modal-overflow-visible="true"] .modal-content {
    overflow: visible;
}

/* Close button uvnitř gov-dialog — legacy třídy ponechány, jen scope. */
gov-dialog[data-modal-container] .modal-close {
    background: transparent;
    border: none;
    cursor: pointer;
    font-size: 18px;
    padding: 4px 8px;
    color: var(--gov-color-muted);
}

gov-dialog[data-modal-container] .modal-close:hover {
    color: var(--app-text-primary);
}

gov-dialog[data-modal-container] .modal-content {
    padding: 16px;
}
```

- [ ] **Step 5.2: Ověřit že žádný view nepoužívá smazané classes**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "class=\"modal-overlay\|class=\"modal--wide\|class=\"modal--record-editor\|class=\"modal-header\b" PmTracker.Web/Views/ --include="*.cshtml"
# Očekáváno: žádný výsledek.
grep -rn "\.modal-overlay\|\.modal-container\|\.modal-header" PmTracker.Web/wwwroot/css/site.css
# Očekáváno: jen v kanonickém komentáři nebo nikde.
```

- [ ] **Step 5.3: Build + test**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
dotnet build PmTracker.sln --nologo 2>&1 | tail -3
dotnet test PmTracker.Tests.Unit --nologo --logger "console;verbosity=minimal" 2>&1 | tail -3
```

Očekáváno: Build OK, tests 342+ PASS. Pokud nějaký existující test ověřoval `.modal-overlay` CSS class, upravit ho na `gov-dialog[data-modal-container]`.

- [ ] **Step 5.4: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add PmTracker.Web/wwwroot/css/site.css
git commit -m "$(cat <<'EOF'
style(modals): odstranění legacy .modal-overlay CSS (Fáze 2E Task 5)

Smazány bloky .modal-overlay, .modal.modal--*, .modal-close, .modal-header,
.modal-content — nahrazeny scope na gov-dialog[data-modal-container]
s data-modal-variant / data-modal-overflow-visible atributy.

Velikosti (wide=1100px, record-editor=1280px) řízeny přes CSS custom
properties --gov-dialog-max-width. Office-search-panel overflow selektor
a mobile media query přepsány.
EOF
)"
```

---

## Task 6: Docs + uzavření 2E

**Cíl:** Aktualizovat architekturní docs, smazat known-issue, připojit worklog.

**Files:**
- Modify: `docs/architecture/dialogs.md`
- Delete: `docs/known-issues/modal-migration-to-pm-dialog.md`
- Create: `docs/superpowers/logs/2026-04-19-faze-2e-worklog.md`

- [ ] **Step 6.1: Rozšířit `docs/architecture/dialogs.md`**

Doplnit sekci "Modální systém (2E)":

```markdown
## Modální systém (2E)

Kanonický entry point: `_ModalLayout.cshtml` → `<gov-dialog>`. Všech 18
modálních views používá `Layout = "_ModalLayout"`.

### Kontrakt

- `ViewData["ModalTitleId"]` — ID pro aria-labelledby
- `ViewData["ModalVariant"]` — `"default"` | `"wide"` | `"record-editor"`
- `ViewData["ModalOverflowVisible"]` — `"true"` | bool

### JS lifecycle (modals.js)

- `openUrlModal(url, trigger)` — fetch HTML, vložit do `#modal-root`,
  gov-dialog se sám upgradne, `setAttribute("open")` zajistí open state
- `closeModal()` — volá `.close()` na gov-dialog, vyprázdní `#modal-root`
- Backdrop click / Esc — obsluhuje gov-dialog nativně, bootstrap.js
  delegace zachytává i `data-modal-close` atribut na close buttonech
  uvnitř

### Floating pickery

Pickery (person, datetime) mountují do globálního `#floating-panel-root`
v `_Layout.cshtml`, NE uvnitř gov-dialog (shadow DOM kolize).
`ui.js:getFloatingLayerRoot` vrací globální root vždy.
```

- [ ] **Step 6.2: Smazat known-issue**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git rm docs/known-issues/modal-migration-to-pm-dialog.md
```

- [ ] **Step 6.3: Worklog**

Vytvořit `docs/superpowers/logs/2026-04-19-faze-2e-worklog.md` s diffem před/po:
- baseline: 339 unit testů, 18 views, ~200 LOC custom modal CSS
- po: 342+ testů, 18 views beze změny, ~80 LOC scoped gov-dialog CSS
- legacy CSS odstraněn: X řádků
- commits: 5

- [ ] **Step 6.4: Commit**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
git add docs/architecture/dialogs.md \
        docs/superpowers/logs/2026-04-19-faze-2e-worklog.md \
        docs/known-issues/modal-migration-to-pm-dialog.md
git commit -m "$(cat <<'EOF'
docs(2e): uzavření Fáze 2E — gov-dialog modály

Uzavřeno 5 tasky (_ModalFormActions submit na pm-button, _ModalLayout
přepis na gov-dialog, JS adaptace, per-view smoke, legacy CSS cleanup).

dialogs.md dokumentuje nový kontrakt, floating pickery a JS lifecycle.
Known-issue modal-migration-to-pm-dialog.md smazán (scope dokončen).

Další fáze: 2F (shared filter Records+Harmonogram) nebo 3 (rozbití
god-files).
EOF
)"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement (z known-issues/modal-migration-to-pm-dialog.md) | Task |
|---|---|
| `pm-modal` adapter s `data-modal-*` kompatibilitou | Task 2 (`_ModalLayout` adaptér přes atributy) |
| Refaktor `modals.js` na gov-dialog API | Task 3.2 |
| Migrace 33 views (reálně 18) | Task 4 (smoke), views se nedotýkají — `Layout = "_ModalLayout"` stačí |
| Odstranění legacy CSS | Task 5 |
| `_ModalFormActions` submit | Task 1 |
| `modal-floating-root` reorganizace | Task 3.1 + 3.3 |
| Varianty přes data-atributy | Task 2.1 + 5.1 |

**Placeholder scan:** Všechny code bloky obsahují reálný kód. Žádný TODO/TBD/similar-to-X. `Task 4` jasně označuje "Skip pro CI bez SQL" + manual checklist — ne placeholder, ale explicit deferral s instrukcemi.

**Type consistency:**
- `PmButtonVariant` použit konzistentně v Task 1 (`SubmitVariant`), referencuje `PmTracker.Web.TagHelpers.PmButtonVariant`
- `data-modal-variant` string hodnoty `"default" | "wide" | "record-editor"` konzistentní napříč Task 2 (Razor) + Task 5 (CSS selectors)
- `data-modal-container` atribut konzistentní — v Task 2 (Razor), Task 3 (JS selectors), Task 5 (CSS scope)

## Rizika & mitigace

1. **gov-dialog API neznámé** — Task 0.3 spike ověří. Plan má fallback přes `setAttribute("open")`.
2. **Shadow DOM blokuje AJAX querySelector** — data-modal-container je nastavený přímo na gov-dialog (light DOM), querySelector z venku ho najde.
3. **Floating pickery ztratí pozici** — Task 3.1 přesouvá globální root do `_Layout`; `ui.js:610` už má graceful fallback.
4. **18 views mlčky rozbito** — Task 4 E2E + manual checklist pokrývá. Rollback je `git revert` commit 2 + 3.
5. **DB není dostupná v sandboxu** — Task 4 explicitně manual verify; user testuje na Citrix, pak povolí Task 5 merge.

## Execution handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-19-faze-2e-modaly.md`. Dva způsoby exekuce:

**1. Subagent-Driven (recommended)** — dispatch fresh subagent per task, review mezi tasky, fast iteration.

**2. Inline Execution** — spouštím tasky v této session přes executing-plans, batch execution s checkpointy.

Jakou cestu zvolit?
