# Design — Ranní systémová oprava UX + bugů (inbox 2026-04-20)

**Branch:** `codex/senior-refactor-fase-1`
**Inbox origin:** `docs/superpowers/plans/2026-04-20-upravy-inbox.md`
**Scope této fáze:** úpravy #1, #2, #4, #5, #6, #7, #8, #9 (8 položek, společný systémový fix)
**Mimo scope:** #3 (authz audit — separátní analytická fáze), #10 (editor modal width — deferred na rozhodnutí app-wide full-width)

---

## Sekce A — Jednani year-grouping evolution

### A1 — Projektová záložka (`/Projekty/Detail/{id}?tab=jednani`) → historické roky defaultně `collapsed`

**View:** `Views/Projekty/_ProjectMeetingsTab.cshtml`

- **Před:** `data-meeting-history-default="open"`, `isPreviewYear ? "preview" : "open"`
- **Po:** `data-meeting-history-default="collapsed"`, `isPreviewYear ? "preview" : "collapsed"`
- JS `meetingOverview.js` **není dotčen** (toggle logika je symetrická).
- CSS **není dotčen**.

### A2 — Globální záložka (`/Jednani/Index`) → project-level history toggle

**View:** `Views/Jednani/Index.cshtml`

Struktura projekt-karty:

```html
<section class="card meeting-project-overview" data-project-card>
    <header class="meeting-header"
            role="button"
            data-project-history-toggle
            aria-expanded="false"
            aria-controls="project-history-@projekt.ProjektId"
            tabindex="0">
        <h2>@projekt.ProjektNazev</h2>
        @if (hasHistoricalYears)
        {
            <gov-icon class="meeting-project-chevron" name="chevron-down" type="components" aria-hidden="true"></gov-icon>
            <span class="meeting-project-history-count" aria-hidden="true">+@(historicalYearsCount) starších roků</span>
        }
    </header>

    @* Aktuální rok — VŽDY viditelný, mimo toggle scope *@
    <div class="meeting-year-stack" data-meeting-overview="year-grouped"
         data-meeting-history-default="collapsed"
         data-meeting-preview-year="@projekt.PreviewRok">
        <section class="meeting-year-group" data-meeting-year-state="preview" ...>
            ...preview year-group markup...
        </section>
    </div>

    @* Historické roky — uvnitř toggle-ovatelného wrapperu *@
    <div class="meeting-project-history-body"
         id="project-history-@projekt.ProjektId"
         data-project-history-body
         hidden>
        <div class="meeting-year-stack" ...>
            @foreach (var historicalRok in historicalYears)
            {
                <section class="meeting-year-group" data-meeting-year-state="collapsed" ...>
                    ...collapsed year-group markup (s vlastním year-chevron)...
                </section>
            }
        </div>
    </div>
</section>
```

**UX behavior:**
- Celý `<header class="meeting-header">` je klikatelný + fokusovatelný (`role="button"`, `tabindex="0"`, klávesy Space/Enter).
- Chevron vpravo nahoře (`chevron-down` default → `chevron-up` po expand) — swap `name` atributu (ne CSS rotate, viz #6acf1d1 artefakty).
- Když projekt nemá historické roky → header je NE-klikatelný (žádný `data-project-history-toggle`, žádný chevron, žádný count).
- Když projekt má historické roky → počet v hlavičce (`+N starších roků`).
- Při expandu → historické year-groups jsou `collapsed` (každý s vlastním year-chevron pro rozbalení jednání).

**JS rozšíření `meetingOverview.js`:**

```js
export function toggleProjectHistory(toggleEl) {
    const card = toggleEl.closest("[data-project-card]");
    if (!card) return;
    const body = card.querySelector("[data-project-history-body]");
    const chevron = toggleEl.querySelector(".meeting-project-chevron");
    if (!body) return;

    const isExpanded = toggleEl.getAttribute("aria-expanded") === "true";
    const nextState = !isExpanded;
    toggleEl.setAttribute("aria-expanded", nextState ? "true" : "false");
    body.hidden = !nextState;
    if (chevron) chevron.setAttribute("name", nextState ? "chevron-up" : "chevron-down");
}
```

**Bootstrap delegace:**

```js
// bootstrap.js — add to global click handler
const projectHistoryToggle = target.closest("[data-project-history-toggle]");
if (projectHistoryToggle) {
    event.preventDefault();
    toggleProjectHistory(projectHistoryToggle);
    return;
}
// Space / Enter keydown handler analogous
```

**Spec update:** `docs/specs/meetings-year-grouping.md` — rozšířit sekci "Aplikační záložka" o project-level toggle, "Projektová záložka" o collapsed default.

**Tests:**
- `MeetingsYearGroupingTests.ApplicationJednaniIndex_ShouldWrapHistoricalYearsInProjectHistoryBody` — kontroluje `[data-project-history-body][hidden]` wrapper kolem historických year-groups
- `..._ProjectHeaderShouldBeToggle` — kontroluje `role="button"` + `data-project-history-toggle` + `aria-expanded`
- `..._ProjectTabShouldDefaultHistoricalYearsToCollapsed` — (přejmenováno z `..._Open`) kontroluje `isPreviewYear ? "preview" : "collapsed"`

---

## Sekce B — Dashboard full-width + non-scrollable + enrichment + icons

### B1 — Layout: full-width + non-scrollable

**View:** `Views/Dashboard/Index.cshtml` (nebo ekvivalent)
**CSS:** nová sekce v `site.css` `/* dashboard-layout */`

```
┌─────────────── Header (~72px) ────────────────┐
├──────────────────────┬───────────────────────┤
│                      │   Jednání (1/3 × 1/2) │
│   Záznamy            ├───────────────────────┤
│   (2/3 × full)       │   News    (1/3 × 1/2) │
└──────────────────────┴───────────────────────┘
```

CSS:
```css
.dashboard-shell {
    height: 100vh;
    display: grid;
    grid-template-rows: auto 1fr;
    grid-template-columns: 100%;
    overflow: hidden; /* outer non-scrollable */
}

.dashboard-body {
    display: grid;
    grid-template-columns: 2fr 1fr;
    grid-template-rows: 1fr 1fr;
    gap: 12px;
    min-height: 0; /* klíč pro flex grid + overflow */
    padding: 12px;
}

.dashboard-section--focus   { grid-column: 1; grid-row: 1 / 3; }
.dashboard-section--meetings{ grid-column: 2; grid-row: 1; }
.dashboard-section--news    { grid-column: 2; grid-row: 2; }

.dashboard-section {
    display: flex;
    flex-direction: column;
    min-height: 0; /* viz výše */
    overflow: hidden; /* sekce sama má overflow hidden */
}

.dashboard-section-header { flex: 0 0 auto; }
.dashboard-section-body   { flex: 1 1 auto; overflow-y: auto; min-height: 0; }
.dashboard-section-footer { flex: 0 0 auto; } /* obsahuje "Zobrazit více" */

/* Responsive: pod 1024px stackovat do scrollable single-column */
@media (max-width: 1024px) {
    .dashboard-shell { height: auto; overflow: auto; }
    .dashboard-body {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto auto;
    }
    .dashboard-section--focus,
    .dashboard-section--meetings,
    .dashboard-section--news {
        grid-column: 1; grid-row: auto;
    }
}
```

**Full-width wrapper:** `_Layout.cshtml` — dashboard view bypass-ne centrální `.container` (nebo použije `.container-fluid`). Detail ověřit přes layout sekci.

### B2 — "Zobrazit více" → route na dedikované stránky (všechny 3 sekce)

- Routes: `/Dashboard/Focus` (zaznamy full list), `/Dashboard/Meetings` (meetings full list), `/Dashboard/News` (news full list, už existuje)
- Každý `dashboard-section-footer` má **jediné** tlačítko "Zobrazit více" (ne "Načíst více") s href na příslušnou route
- **Smazat všechna "Načíst více"** — kompletně odstranit lazy-load JS pagination z dashboardu (`data-load-more`, `loadMore`)

**Backend:** ověřit/vytvořit `DashboardController.Focus`, `.Meetings` actions. News už existuje (`/dashboard/news`).

### B3 — News enrichment (#5)

**ViewModel:** `HomeViewModels.cs` `DashboardNewsItemViewModel` — přidat:
```csharp
public string ActorName { get; init; } = string.Empty;
```

**Service:** `DashboardService.BuildNewsItemsAsync` — resolve `ActorOsobaId` na display jméno (existing `OsobaRepository` nebo join).

**Description template** (per event type):
- Záznam Create/Update: `$"{actorName} • {TruncateFirstLine(record.Cil, 120)}"` (fallback: `actorName` samotné)
- Vyjádření Create/Update: `$"{actorName} • {TruncateFirstLine(comment.Text, 120)}"`
- Jednání Create/Update: `$"{actorName} • {date:dd.MM.yyyy} v {time:HH:mm}"` (odstranit redundantní `BuildProjectLabel`, zůstane jen v `ProjectLabel` slotu)

Helper `TruncateFirstLine(string, int maxLen)` — první řádek, ellipsis na `maxLen`.

### B4 — Icon buttons (#6)

Textová tlačítka → `<gov-button variant="plain" size="sm">` s icon-only slotem + `title` + `aria-label`.

**Mapping:**
| Akce | Ikona (gov-icon name) | aria-label / title |
|---|---|---|
| Upravit (záznam, vyjádření) | `pencil` (fallback: `edit`) | "Upravit" |
| Navrhnout termín a harmonogram | `calendar-clock` (fallback: inline SVG) | "Navrhnout termín a harmonogram" |
| Smazat (vyjádření) | `trash` (fallback: `delete`) | "Smazat" |

**Tooltip:** `title` atribut (browser native tooltip). Nechávám prozatím nativní — gov-design-system nemá ověřený tooltip Web Component ve verzi 4.2.9.

**Views dotčené:**
- `_ZaznamCard.cshtml` (záznam card actions)
- `_EditZaznamFormActions.cshtml` / `_RecordEditorActions.cshtml` (editor actions)
- `_VyjadreniCard.cshtml` nebo `_CommentCard.cshtml` (comment edit/delete)

**Architecture test:** `DashboardActionButtonsTests.ShouldBeIconOnly` — grep že dotčené views neobsahují `>Upravit<` nebo `>Smazat<` textová tlačítka (jen jako `aria-label` / `title`).

---

## Sekce C — Modal systém (Fáze 2E dědictví fix)

### C1 — Floating portal stacking fix (#7) — dynamic re-parent

**Problem:** `#floating-panel-root` v `_Layout.cshtml` je v light DOM na úrovni stránky. Gov-dialog má vlastní shadow DOM stacking context → panel se vykresluje ZA modalem.

**Fix:**
- **`bootstrap.js`** — listen na modal-open event (custom event emitted když se modal aktivuje). Když modal otevřen, přesun `#floating-panel-root` jako první child aktivního `<gov-dialog>` (přes `dialog.prepend(rootEl)`). Když modal zavřen, vrátit do původní pozice (body, nebo kde byl v `_Layout`).
- **Lifecycle:**
  ```js
  // Helper
  let originalFloatingParent = null;
  let originalFloatingNextSibling = null;
  function reparentFloatingRootIntoModal(dialog) {
      const root = document.getElementById("floating-panel-root");
      if (!root || !dialog) return;
      if (!originalFloatingParent) {
          originalFloatingParent = root.parentElement;
          originalFloatingNextSibling = root.nextSibling;
      }
      dialog.prepend(root);
  }
  function restoreFloatingRoot() {
      const root = document.getElementById("floating-panel-root");
      if (!root || !originalFloatingParent) return;
      originalFloatingParent.insertBefore(root, originalFloatingNextSibling);
      originalFloatingParent = null;
      originalFloatingNextSibling = null;
  }
  ```
- **Volací místa:**
  - `openModal(dialog)` (nebo ekvivalent v `modals.js`) → `reparentFloatingRootIntoModal(dialog)`
  - `closeModal()` → `restoreFloatingRoot()` PŘED remove innerHTML modal-rootu
- **Edge cases:**
  - Už zavřený modal — no-op (root není v modalu)
  - Nested modal (unlikely, ale defensive) — vždy re-parent do nejvýš-levelového aktivního modalu
  - Picker otevřený když modal zavírá se — `closeModal` nejdřív `closeAllFloatingPanels()` (už existuje), až pak restore parent

### C2 — Gov-close handler universal fallback (#8 + #9 část 1)

**Problem:** `handleGovCloseEvent` v `bootstrap.js` řeší pouze record-editor dirty-check. Non-record-editor modaly (Přidat ručně, AD search, atd.) při kliknutí na X nic nezavírají.

**Fix:** rozšířit `handleGovCloseEvent`:

```js
function handleGovCloseEvent(event) {
    const dialog = event.target;
    if (!(dialog instanceof HTMLElement) || dialog.tagName !== "GOV-DIALOG") return;

    // Prioritně — record-editor dirty-check
    if (dialog.hasAttribute("data-record-editor-active") || hasDirtyRecordEditor(dialog)) {
        event.preventDefault(); // block default close
        requestRecordEditorModalClose();
        return;
    }

    // Default fallback — univerzální close pro non-record-editor modaly
    closeModal();
}
```

Current rozlišovací podmínka (record-editor vs. ostatní) ověřit při implementaci — může být `[data-modal-variant="record-editor"]` atribut.

### C3 — Modal overflow policy (#9 část 2)

**Problem:** AD search modal content přetéká, vytvoří internal scrollbar.
**User pravidlo:** "modaly by neměly být scrollovatelné bez opravdu závažných důvodů".

**Fix:**
1. **CSS default:** `gov-dialog[data-modal-container] .modal-content { overflow: hidden; }` (změna z current — ponechá gov-dialog build-in overflow handling, ne custom scroll)
2. **Opt-in flag** pro legitimní výjimky (record-editor form může být delší než viewport):
   ```html
   <gov-dialog data-modal-container data-modal-variant="record-editor" data-modal-scrollable="true" ...>
   ```
3. **CSS pro opt-in:** `gov-dialog[data-modal-container][data-modal-scrollable="true"] .modal-content { overflow-y: auto; }`
4. **AD search modal** (a další picker modaly) — content layout refactor: search input fixed nahoře, result list má vlastní `max-height: calc(100vh - 240px); overflow-y: auto;` v interní obálce. Modal sám NE-scrollable.

**Architecture test:** `ModalLayoutRulesTests.ShouldNotScrollByDefault` — všechny modal view partials (`_*Modal.cshtml`, `_EditZaznamForm.cshtml` atd.) buď:
- (a) NE-deklarují `data-modal-scrollable="true"`, NEBO
- (b) Deklarují + jsou v whitelistu (aktuálně: `_EditZaznamForm.cshtml` jediný opt-in)

**Spec:** nový `docs/specs/modal-layout-rules.md` — kdy scrollable OK (1 legitimní case: record-editor form).

---

## Side — #10 modal width CSS var — root cause zdokumentován

**Investigace 2026-04-20:**
V `PmTracker.Web/wwwroot/lib/gov-design-system/dist/` (verze 4.2.9) jsou dostupné custom properties pro gov-dialog:
- `--max-width` (fallback: `52.5rem`) — jediná CSS var pro šířku; používána jako `max-width: var(--max-width, 52.5rem)` v `.gov-dialog__dialog`
- Žádné `--gov-dialog-max-width` ani `--gov-dialog-max-height` v dist CSS **neexistují**
- `max-height` je hardcoded `75vh` — nemá CSS var slot; override vyžaduje `:part()` nebo wrapper element

Naše site.css používá `--gov-dialog-max-width`, správný název je `--max-width`.

**Fix (odložený — čeká na rozhodnutí #4 full-width app):**
1. Nahradit `--gov-dialog-max-width` za `--max-width` v gov-dialog variantách wide / record-editor
2. Pro max-height najít alternativu přes `:part()` nebo wrapper (nemá CSS var slot)
3. Ověřit přes Playwright harness po fixu

Scope: pure CSS change, deferred za ranní opravu.

---

## Implementation order + commit strategy

1. **Sekce A** (malé, low-risk): A1 commit → A2 commit → spec update commit
2. **Sekce C** (bug fixes, vysoká priorita): C1 (portal) → C2 (close handler) → C3 (overflow policy) → každý vlastní commit
3. **Sekce B** (UX, větší): B1 (layout) → B2 (routes + remove "Načíst více") → B3 (news enrichment) → B4 (icon buttons) — per commit
4. **Architecture tests** přidávány s každou změnou (TDD konzistentně s 3A–3D)
5. **Side #10** — dokumentační commit na konci (zjištění v komentáři v CSS)

**Risk:** Sekce B1 (CSS Grid layout) je nejvíc vizuálně invazivní — vyžaduje Playwright visual verification po každém kroku (dashboard current vs. new).

---

## Testing

**Unit / architecture tests (automated):**
- A: `MeetingsYearGroupingTests` rozšířeny (3 nové testy)
- B: `DashboardLayoutTests.DashboardShouldBeFullWidth`, `DashboardShouldNotScroll`, `NewsItemViewModelShouldHaveActorName`, `DashboardActionButtonsTests.ShouldBeIconOnly`
- C: `ModalLayoutRulesTests.ShouldNotScrollByDefault`, `GovDialogCloseHandlerTests.UniversalFallback` (spíše smoke test že bootstrap.js má fallback)

**Manual verification (Citrix SQL):**
- A: kliknout header projekt-karty, ověřit rozpad; per-year chevrons po expand
- B: dashboard fit-on-screen, "Zobrazit více" navigace, news items s actor + context, icon buttons + tooltips
- C: AD search výsledky nad modalem, "Přidat ručně" X zavírá, AD modal NE-scrollable

**Playwright (sandbox, SQL-free):**
- `pm-modal-harness.html` rozšířit o simulaci person-picker inside modal — verify portal reparenting
- Icon button render verify

---

## Exit criteria

- 8 úprav implementovány bez regresí
- Unit tests 523 → ~540+ (nové architecture tests)
- Build 0 warnings / 0 errors
- Visual verification přes Playwright harness pro modaly + Citrix smoke pro full app
- Zero URL/route breakage (#4 navigace na `/Dashboard/Focus` je nová route, ne přejmenování existující)
- Commits bisectable (per-úprava, ne megacommit)
