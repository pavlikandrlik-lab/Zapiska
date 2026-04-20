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

const projectRecordFilterPanelStorageKey = "pmtracker.filters.open";

// ---------------------------------------------------------------------------
// Public exports — filter panel open/close
// ---------------------------------------------------------------------------

export function setFilterPanelOpen(open) {
    const filterPanel = document.querySelector("[data-filter-panel]");
    const filterToggle = document.querySelector("[data-filter-toggle]");
    if (!(filterPanel instanceof HTMLElement)) {
        return;
    }

    filterPanel.classList.toggle("collapsed", !open);
    if (filterToggle instanceof HTMLElement) {
        filterToggle.setAttribute("aria-expanded", String(open));
    }

    localStorage.setItem(projectRecordFilterPanelStorageKey, String(open));
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

    if (typeof options.applyScope === "function") {
        options.applyScope(scope, buildProjectFilterStateFromInputs(scope));
    }
}

export function handleProjectFilterInputChange(scope, options = {}) {
    persistProjectFilterSessionState(scope);
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

export function persistFilterState(input, options = {}) {
    void input;
    handleProjectFilterInputChange("records", options);
}

// ---------------------------------------------------------------------------
// Public exports — main UI init orchestrator
// ---------------------------------------------------------------------------

export function initProjectRecordsUi(options = {}) {
    setFilterPanelOpen(localStorage.getItem(projectRecordFilterPanelStorageKey) === "true");
    restoreFilterState();
    const recordsPanel = document.querySelector('[data-tab-panel="zaznamy"]');
    const groupBySubsystemInput = getProjectFilterInput("records", "groupBySubsystem");
    const showGroupedView = groupBySubsystemInput instanceof HTMLInputElement
        ? groupBySubsystemInput.checked
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
