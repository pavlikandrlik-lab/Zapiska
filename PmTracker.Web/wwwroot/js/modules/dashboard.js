import {
    fetchHtmlFragment,
    renderLazyLoadError,
    resolveOrCreateErrorContainer,
    setLazyLoadingState
} from "./navigationShared.js";

function resolvePanelShell(panelOrKey) {
    if (panelOrKey instanceof HTMLElement && panelOrKey.matches("[data-dashboard-panel]")) {
        return panelOrKey;
    }

    if (typeof panelOrKey !== "string" || !panelOrKey) {
        return null;
    }

    const panel = document.querySelector(`[data-dashboard-panel="${CSS.escape(panelOrKey)}"]`);
    return panel instanceof HTMLElement ? panel : null;
}

export async function loadDashboardPanel(panelOrKey, options = {}) {
    const panel = resolvePanelShell(panelOrKey);
    if (!(panel instanceof HTMLElement)) {
        return false;
    }

    const loadUrl = typeof options.url === "string" && options.url
        ? options.url
        : (panel.dataset.dashboardPanelUrl || "").trim();
    if (!loadUrl) {
        return false;
    }

    const content = panel.querySelector("[data-dashboard-panel-content]");
    const placeholder = panel.querySelector("[data-dashboard-panel-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(panel, "data-dashboard-panel-error");
    if (!(content instanceof HTMLElement)) {
        return false;
    }

    setLazyLoadingState(panel, placeholder, errorContainer, true);

    try {
        content.innerHTML = await fetchHtmlFragment(loadUrl);
        panel.dataset.dashboardPanelUrl = loadUrl;
        setLazyLoadingState(panel, placeholder, errorContainer, false);
        if (content.dispatchEvent) {
            content.dispatchEvent(new CustomEvent("pm:panel-loaded", { bubbles: true, detail: { url: loadUrl } }));
        }
        return true;
    }
    catch {
        setLazyLoadingState(panel, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst obsah panelu.", "data-dashboard-panel-retry");
        return false;
    }
}

export function initDashboardShell() {
    const shell = document.querySelector("[data-dashboard-shell]");
    if (!(shell instanceof HTMLElement) || shell.dataset.dashboardReady === "true") {
        return;
    }

    shell.dataset.dashboardReady = "true";
    shell.querySelectorAll("[data-dashboard-panel]").forEach((panel) => {
        if (panel instanceof HTMLElement) {
            void loadDashboardPanel(panel);
        }
    });
}

export function handleDashboardClick(target) {
    if (!(target instanceof Element)) {
        return false;
    }

    const retryButton = target.closest("[data-dashboard-panel-retry]");
    if (retryButton instanceof HTMLButtonElement) {
        const panel = retryButton.closest("[data-dashboard-panel]");
        if (panel instanceof HTMLElement) {
            void loadDashboardPanel(panel);
        }

        return true;
    }

    return false;
}
