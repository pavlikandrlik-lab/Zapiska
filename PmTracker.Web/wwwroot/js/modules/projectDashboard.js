import { loadDashboardPanel } from "./dashboard.js";
import { fetchHtmlFragment } from "./navigationShared.js";
import { resolveSouhrnCil } from "./dashboard/souhrnCil.js";

// Projektový dashboard (spec 2026-10-15 §8): systémové pm-tabs (persist="storage" řeší
// zapamatovanou záložku), lazy-load panelů na pm-tab-change, souhrnný pás z endpointu souhrn.

// ---------------------------------------------------------------------------
// Shell, záložky, panely
// ---------------------------------------------------------------------------

function resolveProjectDashboardShell() {
    const shell = document.querySelector("[data-project-dashboard-shell]");
    return shell instanceof HTMLElement ? shell : null;
}

function resolveTabs(shell) {
    const tabs = shell.querySelector("pm-tabs[data-dashboard-tabs]");
    return tabs instanceof HTMLElement ? tabs : null;
}

function resolveTabPanel(shell, tabKey) {
    if (!tabKey) {
        return null;
    }
    const panel = shell.querySelector(`pm-tab-panel[key="${CSS.escape(tabKey)}"][data-dashboard-panel]`);
    return panel instanceof HTMLElement ? panel : null;
}

const panelLoads = new WeakMap();

// Jeden fetch na panel: klik na dlaždici i pm-tab-change můžou žádat o tentýž panel současně.
function ensureProjectDashboardTabPanelLoaded(shell, tabKey) {
    const panel = resolveTabPanel(shell, tabKey);
    if (!(panel instanceof HTMLElement)) {
        return Promise.resolve(false);
    }
    if (panel.dataset.dashboardPanelLoaded === "true") {
        return Promise.resolve(true);
    }
    const running = panelLoads.get(panel);
    if (running) {
        return running;
    }

    const load = loadDashboardPanel(panel).then((ok) => {
        if (ok) {
            panel.dataset.dashboardPanelLoaded = "true";
        }
        panelLoads.delete(panel);
        return ok;
    });
    panelLoads.set(panel, load);
    return load;
}

async function initTabs(shell) {
    const tabs = resolveTabs(shell);
    if (!(tabs instanceof HTMLElement)) {
        return;
    }

    await customElements.whenDefined("pm-tabs");
    // pm-tabs se inicializuje v mikroúloze po připojení — počkat, než zná aktivní záložku.
    await new Promise((resolve) => queueMicrotask(resolve));

    // ?dashTab= přebije záložku uloženou v localStorage (pm-tabs dává uloženému stavu přednost před initial).
    const forced = (shell.dataset.dashboardForceTab || "").trim();
    if (forced && typeof tabs.setActive === "function") {
        tabs.setActive(forced, { silent: true });
    }

    tabs.addEventListener("pm-tab-change", (event) => {
        const key = event.detail && event.detail.key;
        if (key) {
            void ensureProjectDashboardTabPanelLoaded(shell, key);
        }
    });

    const active = tabs.activeKey;
    if (active) {
        void ensureProjectDashboardTabPanelLoaded(shell, active);
    }
}

// ---------------------------------------------------------------------------
// Souhrnný pás
// ---------------------------------------------------------------------------

async function loadSouhrn(shell) {
    const host = shell.querySelector("[data-dashboard-souhrn]");
    if (!(host instanceof HTMLElement)) {
        return;
    }
    const url = (host.dataset.url || "").trim();
    if (!url) {
        host.replaceChildren();
        return;
    }
    try {
        host.innerHTML = await fetchHtmlFragment(url);
    }
    catch {
        // Pás je doplněk: bez něj záložky fungují dál, prázdný kontejner se skryje (:empty).
        host.replaceChildren();
    }
}

async function activateSouhrnTarget(shell, cil) {
    const tabs = resolveTabs(shell);
    if (!(tabs instanceof HTMLElement) || typeof tabs.setActive !== "function") {
        return;
    }
    tabs.setActive(cil.tab);
    const ok = await ensureProjectDashboardTabPanelLoaded(shell, cil.tab);
    if (!ok) {
        return;
    }
    if (cil.tab === "zaznamy" && cil.filter) {
        // Prázdná kategorie má chip disabled — filtr by ukázal prázdnou tabulku, zůstane „Vše".
        const chip = shell.querySelector(`[data-dashboard-category-filter="${CSS.escape(cil.filter)}"]`);
        if (chip instanceof HTMLButtonElement && !chip.disabled) {
            applyCategoryFilter(shell, cil.filter);
        }
    }
    shell.dispatchEvent(new CustomEvent("pm:dashboard-filter", {
        bubbles: true,
        detail: { panel: cil.tab, filter: cil.filter }
    }));
}

// ---------------------------------------------------------------------------
// Filtr kategorií v záložce Záznamy
// ---------------------------------------------------------------------------

function applyCategoryFilter(shell, category) {
    shell.querySelectorAll("[data-dashboard-category-filter]").forEach((btn) => {
        if (!(btn instanceof HTMLElement)) {
            return;
        }
        const btnCategory = btn.getAttribute("data-dashboard-category-filter") ?? "";
        const active = btnCategory === category;
        btn.classList.toggle("active", active);
        btn.setAttribute("aria-pressed", active ? "true" : "false");
    });

    shell.querySelectorAll("[data-dashboard-expandable-row]").forEach((row) => {
        if (!(row instanceof HTMLElement)) {
            return;
        }
        const rowCategory = row.dataset.category || "";
        const visible = !category || rowCategory === category;
        row.hidden = !visible;

        const recordId = row.dataset.recordId || "";
        if (recordId) {
            const detail = shell.querySelector(`[data-dashboard-expandable-detail="${CSS.escape(recordId)}"]`);
            if (detail instanceof HTMLElement && !visible) {
                detail.hidden = true;
                row.classList.remove("expanded");
                row.setAttribute("aria-expanded", "false");
            }
        }
    });
}

// ---------------------------------------------------------------------------
// Rozbalovací řádky
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
    row.setAttribute("aria-expanded", isExpanded ? "false" : "true");
}

// Řádek je fokusovatelný (tabindex=0) — Enter/mezerník ho rozbalí stejně jako klik.
function handleShellKeydown(event) {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }
    const target = event.target;
    if (!(target instanceof HTMLElement) || !target.matches("[data-dashboard-expandable-row]")) {
        return;
    }
    event.preventDefault();
    toggleExpandableRow(target);
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
    shell.addEventListener("keydown", handleShellKeydown);
    void initTabs(shell);
    void loadSouhrn(shell);
}

// ---------------------------------------------------------------------------
// Public: click handler (volá bootstrap handleDocumentClick)
// ---------------------------------------------------------------------------

export function handleProjectDashboardClick(target) {
    if (!(target instanceof Element)) {
        return false;
    }

    const shell = target.closest("[data-project-dashboard-shell]");
    if (!(shell instanceof HTMLElement)) {
        return false;
    }

    const tile = target.closest("[data-souhrn-tab]");
    if (tile instanceof HTMLElement) {
        const cil = resolveSouhrnCil(tile.dataset);
        if (cil) {
            void activateSouhrnTarget(shell, cil);
        }
        return true;
    }

    const categoryFilterBtn = target.closest("[data-dashboard-category-filter]");
    if (categoryFilterBtn instanceof HTMLElement) {
        const category = categoryFilterBtn.getAttribute("data-dashboard-category-filter") ?? "";
        applyCategoryFilter(shell, category);
        return true;
    }

    const expandableRow = target.closest("[data-dashboard-expandable-row]");
    if (expandableRow instanceof HTMLElement) {
        // Klik na odkaz nebo tlačítko v řádku řádek nerozbaluje.
        if (target.closest("a, button, pm-button")) {
            return false;
        }
        toggleExpandableRow(expandableRow);
        return true;
    }

    return false;
}
