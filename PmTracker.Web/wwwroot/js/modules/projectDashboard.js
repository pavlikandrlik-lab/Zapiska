import { loadDashboardPanel } from "./dashboard.js";
import {
    fetchHtmlFragment,
    renderLazyLoadError,
    resolveOrCreateErrorContainer,
    setLazyLoadingState
} from "./navigationShared.js";

const projectDashboardTabStorageKeyPrefix = "pmtracker.projectDashboard.tab.";

// ---------------------------------------------------------------------------
// Shell resolution
// ---------------------------------------------------------------------------

function resolveProjectDashboardShell() {
    const shell = document.querySelector("[data-project-dashboard-shell]");
    return shell instanceof HTMLElement ? shell : null;
}

// ---------------------------------------------------------------------------
// Tab helpers
// ---------------------------------------------------------------------------

function resolveTabButtons(shell) {
    return Array.from(shell.querySelectorAll(".dashboard-tab[data-dashboard-tab]"));
}

function resolveTabPanels(shell) {
    return Array.from(shell.querySelectorAll(".dashboard-tab-panel[data-dashboard-tab-panel]"));
}

function resolveTabPanel(shell, tabKey) {
    if (!tabKey) {
        return null;
    }
    const panel = shell.querySelector(`.dashboard-tab-panel[data-dashboard-tab-panel="${CSS.escape(tabKey)}"]`);
    return panel instanceof HTMLElement ? panel : null;
}

function resolveActiveTabKey(shell) {
    const projectId = (shell.dataset.projectId || "").trim();
    if (!projectId) {
        return null;
    }
    return localStorage.getItem(`${projectDashboardTabStorageKeyPrefix}${projectId}`) || null;
}

function persistActiveTabKey(shell, tabKey) {
    const projectId = (shell.dataset.projectId || "").trim();
    if (!projectId || !tabKey) {
        return;
    }
    localStorage.setItem(`${projectDashboardTabStorageKeyPrefix}${projectId}`, tabKey);
}

// ---------------------------------------------------------------------------
// Tab switching
// ---------------------------------------------------------------------------

function setActiveProjectDashboardTab(shell, tabKey) {
    const tabs = resolveTabButtons(shell);
    const panels = resolveTabPanels(shell);

    tabs.forEach((tab) => {
        tab.classList.toggle("active", tab.getAttribute("data-dashboard-tab") === tabKey);
    });

    panels.forEach((panel) => {
        panel.classList.toggle("active", panel.getAttribute("data-dashboard-tab-panel") === tabKey);
    });

    persistActiveTabKey(shell, tabKey);
}

// ---------------------------------------------------------------------------
// Lazy-load a tab panel (delegates to dashboard.js loadDashboardPanel)
// Only loads the panel that has `data-dashboard-panel` on it; the tab panel
// element itself carries both `data-dashboard-tab-panel` and `data-dashboard-panel`.
// ---------------------------------------------------------------------------

async function ensureProjectDashboardTabPanelLoaded(shell, tabKey) {
    const tabPanel = resolveTabPanel(shell, tabKey);
    if (!(tabPanel instanceof HTMLElement)) {
        return false;
    }

    // Each tab-panel is also a dashboard-panel — delegate to the shared loader.
    if (!tabPanel.hasAttribute("data-dashboard-panel")) {
        return false;
    }

    // Skip if already loaded (data-dashboard-panel-loaded set after first fetch)
    if (tabPanel.dataset.dashboardPanelLoaded === "true") {
        return true;
    }

    const ok = await loadDashboardPanel(tabPanel);
    if (ok) {
        tabPanel.dataset.dashboardPanelLoaded = "true";
    }
    return ok;
}

// ---------------------------------------------------------------------------
// Category filter
// ---------------------------------------------------------------------------

function applyCategoryFilter(shell, category) {
    // Update active button
    shell.querySelectorAll("[data-dashboard-category-filter]").forEach((btn) => {
        if (!(btn instanceof HTMLElement)) {
            return;
        }
        const btnCategory = btn.getAttribute("data-dashboard-category-filter") ?? "";
        btn.classList.toggle("active", btnCategory === category);
    });

    // Filter table rows
    shell.querySelectorAll("[data-dashboard-expandable-row]").forEach((row) => {
        if (!(row instanceof HTMLElement)) {
            return;
        }
        const rowCategory = row.dataset.category || "";
        const visible = !category || rowCategory === category;
        row.hidden = !visible;

        // If we are hiding a row, also hide its detail row
        const recordId = row.dataset.recordId || "";
        if (recordId) {
            const detail = shell.querySelector(`[data-dashboard-expandable-detail="${CSS.escape(recordId)}"]`);
            if (detail instanceof HTMLElement && !visible) {
                detail.hidden = true;
                row.classList.remove("expanded");
            }
        }
    });
}

// ---------------------------------------------------------------------------
// Expandable rows
// ---------------------------------------------------------------------------

function toggleExpandableRow(row) {
    if (!(row instanceof HTMLElement)) {
        return;
    }

    const recordId = row.dataset.recordId || "";
    if (!recordId) {
        return;
    }

    const shell = row.closest("[data-project-dashboard-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const detail = shell.querySelector(`[data-dashboard-expandable-detail="${CSS.escape(recordId)}"]`);
    if (!(detail instanceof HTMLElement)) {
        return;
    }

    const isExpanded = !detail.hidden;
    detail.hidden = isExpanded;
    row.classList.toggle("expanded", !isExpanded);
}

// ---------------------------------------------------------------------------
// Year selector (statistics panel reload)
// ---------------------------------------------------------------------------

async function reloadStatisticsPanel(shell, year) {
    const panel = shell.querySelector('[data-dashboard-tab-panel="statistiky"]');
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const baseUrl = (panel.dataset.dashboardPanelUrl || "").trim();
    if (!baseUrl) {
        return;
    }

    const url = new URL(baseUrl, window.location.origin);
    url.searchParams.set("year", String(year));

    const content = panel.querySelector("[data-dashboard-panel-content]");
    const placeholder = panel.querySelector("[data-dashboard-panel-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(panel, "data-dashboard-panel-error");
    if (!(content instanceof HTMLElement)) {
        return;
    }

    setLazyLoadingState(panel, placeholder, errorContainer, true);

    try {
        content.innerHTML = await fetchHtmlFragment(url.href);
        setLazyLoadingState(panel, placeholder, errorContainer, false);
    }
    catch {
        setLazyLoadingState(panel, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst statistiky.", "data-dashboard-panel-retry");
    }
}

// ---------------------------------------------------------------------------
// Public: init
// ---------------------------------------------------------------------------

export function initProjectDashboardShell() {
    const shell = resolveProjectDashboardShell();
    if (!(shell instanceof HTMLElement) || shell.dataset.projectDashboardReady === "true") {
        return;
    }

    shell.dataset.projectDashboardReady = "true";

    const tabs = resolveTabButtons(shell);
    if (tabs.length === 0) {
        return;
    }

    // Determine which tab to activate: prefer persisted, fall back to first
    const firstTabKey = tabs[0].getAttribute("data-dashboard-tab") || "";
    const persistedTabKey = resolveActiveTabKey(shell);
    const availableKeys = new Set(tabs.map((t) => t.getAttribute("data-dashboard-tab")));

    const tabToActivate = persistedTabKey && availableKeys.has(persistedTabKey)
        ? persistedTabKey
        : firstTabKey;

    setActiveProjectDashboardTab(shell, tabToActivate);

    // Load only the active panel immediately; others are loaded on first activation
    void ensureProjectDashboardTabPanelLoaded(shell, tabToActivate);

    // Wire up tab click handlers
    tabs.forEach((tab) => {
        if (!(tab instanceof HTMLElement) || tab.dataset.projectDashboardTabReady === "true") {
            return;
        }
        tab.dataset.projectDashboardTabReady = "true";

        tab.addEventListener("click", () => {
            const key = tab.getAttribute("data-dashboard-tab") || "";
            if (!key) {
                return;
            }
            setActiveProjectDashboardTab(shell, key);
            void ensureProjectDashboardTabPanelLoaded(shell, key);
        });
    });
}

// ---------------------------------------------------------------------------
// Public: click handler (called from bootstrap handleDocumentClick)
// ---------------------------------------------------------------------------

export function handleProjectDashboardClick(target) {
    if (!(target instanceof Element)) {
        return false;
    }

    const shell = target.closest("[data-project-dashboard-shell]");
    if (!(shell instanceof HTMLElement)) {
        return false;
    }

    // Category filter button
    const categoryFilterBtn = target.closest("[data-dashboard-category-filter]");
    if (categoryFilterBtn instanceof HTMLElement) {
        const category = categoryFilterBtn.getAttribute("data-dashboard-category-filter") ?? "";
        applyCategoryFilter(shell, category);
        return true;
    }

    // Expandable row toggle
    const expandableRow = target.closest("[data-dashboard-expandable-row]");
    if (expandableRow instanceof HTMLElement) {
        // Don't expand when clicking a link or button inside the row
        if (target.closest("a, button")) {
            return false;
        }
        toggleExpandableRow(expandableRow);
        return true;
    }

    return false;
}

// ---------------------------------------------------------------------------
// Public: change handler (called from bootstrap handleDocumentChange)
// ---------------------------------------------------------------------------

export function handleProjectDashboardChange(target) {
    if (!(target instanceof Element)) {
        return false;
    }

    const shell = target.closest("[data-project-dashboard-shell]");
    if (!(shell instanceof HTMLElement)) {
        return false;
    }

    const yearSelect = target.closest("[data-dashboard-year-select]");
    if (yearSelect instanceof HTMLSelectElement) {
        const year = parseInt(yearSelect.value, 10);
        if (year > 0) {
            void reloadStatisticsPanel(shell, year);
        }
        return true;
    }

    return false;
}
