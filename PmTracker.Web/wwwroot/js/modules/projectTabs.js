import { initCommentSortUi } from "./comments.js";
import {
    applyRecordsView,
    initSubsystemScrollIndicator,
    restoreFilterState,
    scheduleSubsystemIndicatorSync,
    setFilterPanelOpen,
    setProjectFilterSaveStatus
} from "./filters.js";
import { navigationRuntime } from "./navigationRuntime.js";
import {
    fetchHtmlFragment,
    parseHtmlFragment,
    renderLazyLoadError,
    resolveOrCreateErrorContainer,
    resolveProjectTabPanel,
    setLazyLoadingState
} from "./navigationShared.js";
import { initProjectScheduleUi, renderStaticTimelineAxes } from "./schedule.js";
import { queueRainbowSegmentRender } from "./ui.js";

const projectTabStorageKey = "pmtracker.tab.active";
const projectRecordFilterPanelStorageKey = "pmtracker.filters.open";

class ProjectNavigationController {
    constructor(options = {}) {
        this.options = options;
    }

    normalizeTabName(tabName) {
        return tabName === "gant" ? "harmonogram" : tabName;
    }

    setActiveTab(tabName) {
        const normalizedTabName = this.normalizeTabName(tabName);
        if (!normalizedTabName) {
            return;
        }

        const tabs = document.querySelectorAll(".tab");
        const panels = document.querySelectorAll(".tab-panel");
        let activePanel = null;
        tabs.forEach((item) => {
            item.classList.toggle("active", item.getAttribute("data-tab") === normalizedTabName);
        });
        panels.forEach((panel) => {
            const isActive = panel.getAttribute("data-tab-panel") === normalizedTabName;
            panel.classList.toggle("active", isActive);
            if (isActive) {
                activePanel = panel;
            }
        });

        localStorage.setItem(projectTabStorageKey, normalizedTabName);

        if (activePanel instanceof HTMLElement) {
            if (normalizedTabName === "harmonogram") {
                this.options.renderStaticTimelineAxes?.(activePanel);
            }

            this.options.queueRainbowSegmentRender?.(activePanel);
        }

        this.options.scheduleSubsystemIndicatorSync?.();
    }

    syncTabQuery(tabName) {
        const normalizedTabName = this.normalizeTabName(tabName);
        if (!normalizedTabName) {
            return;
        }

        const url = new URL(window.location.href);
        url.searchParams.set("tab", normalizedTabName);
        history.replaceState(history.state, "", `${url.pathname}${url.search}${url.hash}`);
    }

    async ensureTabLoaded(tabName, options = {}) {
        return ensureProjectTabLoaded(this.normalizeTabName(tabName), options);
    }

    initTabs() {
        const tabs = document.querySelectorAll(".tab");
        if (tabs.length === 0) {
            return;
        }

        const availableTabs = new Set(Array.from(tabs).map((tab) => tab.getAttribute("data-tab")));
        const storedTab = this.normalizeTabName(localStorage.getItem(projectTabStorageKey));
        const urlTab = this.normalizeTabName(new URL(window.location.href).searchParams.get("tab"));
        const defaultTab = tabs[0].getAttribute("data-tab");
        const tabToActivate = urlTab && availableTabs.has(urlTab)
            ? urlTab
            : storedTab && availableTabs.has(storedTab)
                ? storedTab
                : defaultTab;

        this.setActiveTab(tabToActivate);
        void this.ensureTabLoaded(tabToActivate);

        tabs.forEach((tab) => {
            if (!(tab instanceof HTMLElement) || tab.dataset.tabReady === "true") {
                return;
            }

            tab.dataset.tabReady = "true";
            tab.addEventListener("click", () => {
                const requestedTab = tab.getAttribute("data-tab");
                this.setActiveTab(requestedTab);
                this.syncTabQuery(requestedTab);
                void this.ensureTabLoaded(requestedTab);
            });
        });
    }

    initRecordsUi() {
        const filterPanel = document.querySelector("[data-filter-panel]");
        if (filterPanel instanceof HTMLElement) {
            this.options.setFilterPanelOpen?.(localStorage.getItem(projectRecordFilterPanelStorageKey) === "true");
        }

        const state = this.options.restoreFilterState?.() || {};
        this.options.setProjectFilterSaveStatus?.("records", "");
        this.options.applyRecordsView?.(Boolean(state.groupBySubsystem) ? "subsystem" : "flat");
        this.options.initSubsystemScrollIndicator?.();
    }
}

const projectNavigationController = new ProjectNavigationController({
    renderStaticTimelineAxes,
    queueRainbowSegmentRender,
    scheduleSubsystemIndicatorSync,
    setFilterPanelOpen,
    restoreFilterState,
    setProjectFilterSaveStatus,
    applyRecordsView,
    initSubsystemScrollIndicator
});

function replaceProjectTabPanelFromHtml(tabKey, html, loadUrl) {
    const nextDoc = parseHtmlFragment(html);
    const replacement = nextDoc.querySelector(`[data-tab-panel="${CSS.escape(tabKey)}"]`);
    const current = document.querySelector(`[data-tab-panel="${CSS.escape(tabKey)}"]`);
    if (!(replacement instanceof HTMLElement) || !(current instanceof HTMLElement)) {
        throw new Error(`Nepodařilo se načíst panel ${tabKey}.`);
    }

    if (current.classList.contains("active")) {
        replacement.classList.add("active");
    }

    replacement.dataset.projectTabLoaded = "true";
    if (loadUrl) {
        replacement.dataset.projectTabLazyUrl = loadUrl;
    }

    current.replaceWith(replacement);
    return document.querySelector(`[data-tab-panel="${CSS.escape(tabKey)}"]`);
}

export function setActiveTab(tabName) {
    projectNavigationController.setActiveTab(tabName);
}

export function syncTabQuery(tabName) {
    projectNavigationController.syncTabQuery(tabName);
}

export function initProjectTabs() {
    projectNavigationController.initTabs();
}

export function initProjectRecordsUi() {
    projectNavigationController.initRecordsUi();
}

export async function loadProjectTabPanel(tabNameOrPanel, options = {}) {
    const panel = resolveProjectTabPanel(tabNameOrPanel);
    if (!(panel instanceof HTMLElement)) {
        return false;
    }

    const tabKey = (panel.dataset.tabPanel || "").trim();
    const loadUrl = typeof options.url === "string" && options.url
        ? options.url
        : (panel.dataset.projectTabLazyUrl || "").trim();
    if (!tabKey || !loadUrl) {
        return false;
    }

    const forceReload = options.force === true;
    if (panel.dataset.projectTabLoaded === "true" && !forceReload) {
        return true;
    }

    const placeholder = panel.querySelector("[data-project-tab-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(panel, "data-project-tab-error");
    setLazyLoadingState(panel, placeholder, errorContainer, true);

    try {
        const html = await fetchHtmlFragment(loadUrl);
        const currentPanel = replaceProjectTabPanelFromHtml(tabKey, html, loadUrl);
        if (currentPanel instanceof HTMLElement) {
            navigationRuntime.initRecordFormEnhancements?.(currentPanel);
            initCommentSortUi(currentPanel);

            if (tabKey === "zaznamy") {
                initProjectRecordsUi();
            }
            else if (tabKey === "harmonogram") {
                initProjectScheduleUi();
                renderStaticTimelineAxes(currentPanel);
            }

            queueRainbowSegmentRender(currentPanel);
            scheduleSubsystemIndicatorSync();
        }

        return true;
    }
    catch (error) {
        setLazyLoadingState(panel, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst obsah záložky.", "data-project-tab-retry");
        return false;
    }
}

export async function ensureProjectTabLoaded(tabName, options = {}) {
    const normalizedTabName = tabName === "gant" ? "harmonogram" : tabName;
    if (!normalizedTabName || normalizedTabName === "zaznamy") {
        return true;
    }

    return loadProjectTabPanel(normalizedTabName, options);
}
