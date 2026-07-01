/**
 * filters/index.js — state persistence helpers + initProjectRecordsUi orchestrator
 * + re-export barrel pro celý filters feature modul.
 *
 * Fáze 3B Task 4: orchestrátor z filters.js (1105 LOC → submodul ~150 LOC).
 *
 * Exports (vlastní):
 *   restoreFilterState, persistFilterState, setFilterPanelOpen,
 *   handleProjectFilterInputChange, initProjectRecordsUi, setProjectFilterSaveStatus
 *
 * Re-exports vše z podmodulů (backward-compat API).
 */

export * from "./projectFilter.js";
export * from "./recordDisplay.js";
export * from "./printFilter.js";

import {
    restoreProjectFilterScope,
    persistProjectFilterSessionState,
    renderProjectFilterChips,
    setProjectFilterSaveStatus,
    buildProjectFilterStateFromInputs,
    getProjectFilterInput
} from "./projectFilter.js";

import {
    applyRecordsView,
    applyProjectRecordFilters,
    initSubsystemScrollIndicator
} from "./recordDisplay.js";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const projectFilterPanelStorageKey = "pmtracker.filters.open";

// ---------------------------------------------------------------------------
// Public exports — filter panel open/close
// ---------------------------------------------------------------------------

/**
 * Open/close filter shell pro daný scope. Hledá panel uvnitř scope-specific shell
 * (data-project-filter-scope="<scope>"), aby v případě 2 mountovaných shellů
 * (records + schedule) nezamnul panely ze sebe. Spec 2026-04-30.
 *
 * Backwards compat: pokud scope chybí (volání starým 1-arg signature `setFilterPanelOpen(open)`),
 * fallback na `data-project-filter-scope="records"` (původní chování pre-refactor).
 */
export function setFilterPanelOpen(scopeOrOpen, openIfScopeProvided) {
    const hasScope = typeof scopeOrOpen === "string";
    const scope = hasScope ? scopeOrOpen : "records";
    const open = hasScope ? Boolean(openIfScopeProvided) : Boolean(scopeOrOpen);

    const shell = document.querySelector(`[data-project-filter-scope="${scope}"]`);
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const filterPanel = shell.querySelector("[data-filter-panel]");
    const filterToggle = shell.querySelector("[data-filter-toggle]");
    if (!(filterPanel instanceof HTMLElement)) {
        return;
    }

    filterPanel.classList.toggle("collapsed", !open);
    if (filterToggle instanceof HTMLElement) {
        filterToggle.setAttribute("aria-expanded", String(open));
    }

    // Storage je per-projekt sdílený — open/close stav je společný pro records i schedule
    // (filter shell je sdílený DOM jen renderovaný 2× pro každý tab).
    localStorage.setItem(projectFilterPanelStorageKey, String(open));
}

// ---------------------------------------------------------------------------
// Public exports — filter input change handler
// (needs both projectFilter + recordDisplay → lives in orchestrator)
// ---------------------------------------------------------------------------

function applyProjectFilterScope(scope, options = {}) {
    if (scope === "records") {
        const state = buildProjectFilterStateFromInputs(scope);
        applyRecordsView(Boolean(state.groupBySubsystem) ? "subsystem" : "flat");
        return;
    }

    // Proposals apply žije ve stejném modulu (žádná cross-module závislost jako schedule),
    // takže ho řešíme přímo — funguje i pro persistFilterState cestu (dropdown change),
    // která volá tento handler s prázdnými options (bez applyScope callbacku).
    if (scope === "proposals") {
        applyProposalFilters();
        return;
    }

    if (typeof options.applyScope === "function") {
        options.applyScope(scope, buildProjectFilterStateFromInputs(scope));
    }
}

export function handleProjectFilterInputChange(scope, options = {}) {
    // Proposals nemá session persistenci (PENDING default každé načtení) a storage klíč je
    // sdílený napříč scopy (neobsahuje scope) — zápis proposals state by přepsal records/schedule
    // filtr. Proto pro proposals session-state NEpersistujeme; stav žije jen v DOM inputech.
    if (scope !== "proposals") {
        persistProjectFilterSessionState(scope);
    }
    renderProjectFilterChips(scope);
    setProjectFilterSaveStatus(scope, "");
    applyProjectFilterScope(scope, options);
}

// ---------------------------------------------------------------------------
// Public exports — state persistence helpers (convenience wrappers)
// ---------------------------------------------------------------------------

export function restoreFilterState() {
    return restoreProjectFilterScope("records");
}

/**
 * Spec 2026-04-30: scope se detekuje z DOM (input's closest data-project-filter-scope).
 * Records i schedule scope sdílejí jediný state, ale apply pipeline je scope-specific
 * (records re-renderuje record cards, schedule re-applies na schedule list).
 */
export function persistFilterState(input, options = {}) {
    const shell = input instanceof Element
        ? input.closest("[data-project-filter-scope]")
        : null;
    const scope = shell instanceof HTMLElement
        ? (shell.getAttribute("data-project-filter-scope") || "records")
        : "records";
    handleProjectFilterInputChange(scope, options);
}

/**
 * Spec 2026-04-30: pm-tabs Web Component emituje "pm-tab-change" když user přepne
 * mezi taby Záznamy ↔ Harmonogram (i pro ostatní taby). Sdílený filter state se
 * znovu aplikuje na nově viditelný scope, aby filter aplikovaný v Records byl
 * okamžitě platný i v Schedule cards (a naopak).
 */
export function initProjectFilterTabSync() {
    const scopeMap = { zaznamy: "records", harmonogram: "schedule" };
    document.addEventListener("pm-tab-change", (event) => {
        const detail = event && event.detail;
        const tabKey = detail && typeof detail.key === "string" ? detail.key : "";
        const scope = scopeMap[tabKey];
        if (!scope) {
            return; // ostatní taby (jednání, tým, návrhy) nemají filter shell — no-op
        }
        // Znovu aplikuj uložený state na nově viditelný shell — restoreProjectFilterScope
        // vyčte stejný localStorage klíč (sjednocený per projekt) a syncne inputy + chips.
        restoreProjectFilterScope(scope);
    });
}

// ---------------------------------------------------------------------------
// Public exports — main UI init orchestrator
// ---------------------------------------------------------------------------

export function initProjectRecordsUi(options = {}) {
    setFilterPanelOpen("records", localStorage.getItem(projectFilterPanelStorageKey) === "true");
    restoreFilterState();
    const recordsPanel = document.querySelector('[data-tab-panel="zaznamy"]');
    // groupBySubsystem: <gov-form-switch> (Web Component) i HTMLInputElement obojí mají `.checked`.
    const groupBySubsystemInput = getProjectFilterInput("records", "groupBySubsystem");
    const showGroupedView = groupBySubsystemInput instanceof HTMLElement
        ? !!groupBySubsystemInput.checked
        : true;
    const hasServerRenderedGroups = recordsPanel instanceof HTMLElement
        && recordsPanel.querySelector("[data-record-grouped-list] [data-subsystem-group]") instanceof HTMLElement;
    const setStatus = typeof options.setProjectFilterSaveStatus === "function"
        ? options.setProjectFilterSaveStatus
        : setProjectFilterSaveStatus;
    setStatus("records", "");
    if (showGroupedView && hasServerRenderedGroups && recordsPanel instanceof HTMLElement) {
        const groupedShell = recordsPanel.querySelector('[data-records-view="subsystem"]');
        const flatShell = recordsPanel.querySelector('[data-records-view="flat"]');
        if (groupedShell instanceof HTMLElement) {
            groupedShell.hidden = false;
        }

        if (flatShell instanceof HTMLElement) {
            flatShell.hidden = true;
        }

        applyProjectRecordFilters();
    }
    else {
        applyRecordsView(showGroupedView ? "subsystem" : "flat");
    }
    initSubsystemScrollIndicator();
}

// ---------------------------------------------------------------------------
// Public exports — proposals filter
// ---------------------------------------------------------------------------

function normalizeProposalFilterToken(value) {
    if (value === null || value === undefined) {
        return "";
    }
    return String(value).trim().toUpperCase();
}

export function applyProposalFilters() {
    const root = document.querySelector('[data-project-filter-scope="proposals"]');
    if (!(root instanceof HTMLElement)) {
        return;
    }

    const state = buildProjectFilterStateFromInputs("proposals");
    const filters = {
        stavNavrhu: normalizeProposalFilterToken(state.stavNavrhu),
        typNavrhu: normalizeProposalFilterToken(state.typNavrhu),
        subsystem: normalizeProposalFilterToken(state.subsystem),
        autor: normalizeProposalFilterToken(state.autor),
        rozhodl: normalizeProposalFilterToken(state.rozhodl)
    };

    const panel = root.closest("[data-tab-panel]") || document;

    panel.querySelectorAll(".proposal-card").forEach((card) => {
        const matches =
            (!filters.stavNavrhu || normalizeProposalFilterToken(card.dataset.filterStavNavrhu) === filters.stavNavrhu)
            && (!filters.typNavrhu || normalizeProposalFilterToken(card.dataset.filterTypNavrhu) === filters.typNavrhu)
            && (!filters.subsystem || normalizeProposalFilterToken(card.dataset.filterSubsystem) === filters.subsystem)
            && (!filters.autor || normalizeProposalFilterToken(card.dataset.filterAutor) === filters.autor)
            && (!filters.rozhodl || normalizeProposalFilterToken(card.dataset.filterRozhodl) === filters.rozhodl);
        card.hidden = !matches;
    });

    // Skryj celou sekci (.card = nadpis + list), pokud po filtraci nezbyla žádná viditelná
    // karta — jinak by zůstal osamocený nadpis bez obsahu. Sekce bez návrhů (server Count==0,
    // jen .muted zpráva, žádný .proposal-list) necháme být — reprezentují trvalý prázdný stav.
    panel.querySelectorAll(".proposal-section-grid > .card").forEach((section) => {
        const list = section.querySelector(".proposal-list");
        if (!(list instanceof HTMLElement)) {
            return;
        }
        const hasVisible = list.querySelectorAll(".proposal-card:not([hidden])").length > 0;
        section.hidden = !hasVisible;
    });
}

export function initProposalFilterUi() {
    const root = document.querySelector('[data-project-filter-scope="proposals"]');
    if (!(root instanceof HTMLElement)) {
        return;
    }
    // Panel startuje collapsed z markupu (.filter-panel.collapsed) — nevoláme setFilterPanelOpen,
    // protože to zapisuje do sdíleného localStorage klíče a přebilo by records/schedule preferenci.
    restoreProjectFilterScope("proposals");
    applyProposalFilters();
}
