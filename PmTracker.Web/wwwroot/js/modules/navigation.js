import {
    applyCommentSort,
    initCommentSortUi,
    setCommentSortButtonLabel
} from "./comments.js";
import {
    applyRecordsView,
    initSubsystemScrollIndicator,
    restoreFilterState,
    scheduleSubsystemIndicatorSync,
    setFilterPanelOpen,
    setProjectFilterSaveStatus
} from "./filters.js";
import { closeAllFloatingPanels, queueRainbowSegmentRender } from "./ui.js";
import { initProjectScheduleUi, renderStaticTimelineAxes } from "./schedule.js";

const projectTabStorageKey = "pmtracker.tab.active";
const projectRecordFilterPanelStorageKey = "pmtracker.filters.open";

function readBooleanStorageDefaultTrue(key) {
    const rawValue = window.localStorage.getItem(key);
    if (rawValue === null) {
        return true;
    }

    return rawValue === "true";
}

function writeBooleanStorage(key, value) {
    window.localStorage.setItem(key, value ? "true" : "false");
}

function resolveQueryRoot(scope) {
    return scope && typeof scope.querySelector === "function"
        ? scope
        : document;
}

function readProjectListStatusFilterState(doneKey, deletedKey) {
    return {
        hideDone: readBooleanStorageDefaultTrue(doneKey),
        hideDeleted: readBooleanStorageDefaultTrue(deletedKey)
    };
}

function syncProjectListStatusFilterInputs(root, doneKey, deletedKey) {
    const state = readProjectListStatusFilterState(doneKey, deletedKey);
    root.querySelectorAll("[data-project-status-hide]").forEach((input) => {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
        if (statusCode === "DONE") {
            input.checked = state.hideDone;
        }
        else if (statusCode === "DELETED") {
            input.checked = state.hideDeleted;
        }
    });
}

function isPrimaryNavigationClick(event) {
    return !(event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0);
}

function setActiveLink(container, selector, activeKey) {
    container.querySelectorAll(selector).forEach((link) => {
        if (!(link instanceof HTMLAnchorElement)) {
            return;
        }

        const isActive = link.dataset.key === activeKey;
        link.classList.toggle("active", isActive);
        if (isActive) {
            link.setAttribute("aria-current", "page");
        }
        else {
            link.removeAttribute("aria-current");
        }
    });
}

async function fetchPanelHtml(endpoint) {
    const response = await fetch(endpoint, {
        headers: { "X-Requested-With": "XMLHttpRequest" }
    });
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    return response.text();
}

const cardInteractiveSelector = [
    "button",
    "a",
    "input",
    "select",
    "textarea",
    "label",
    "[data-stop-propagation]",
    "[contenteditable='true']"
].join(", ");

const navigationRuntime = {
    initRecordFormEnhancements: null,
    prepareRecordEditorFormNavigation: null
};

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

        tabs.forEach((tab) => {
            if (!(tab instanceof HTMLElement) || tab.dataset.tabReady === "true") {
                return;
            }

            tab.dataset.tabReady = "true";
            tab.addEventListener("click", () => {
                const requestedTab = tab.getAttribute("data-tab");
                this.setActiveTab(requestedTab);
                this.syncTabQuery(requestedTab);
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

export function configureNavigationRuntime(runtime = {}) {
    if (typeof runtime.initRecordFormEnhancements === "function") {
        navigationRuntime.initRecordFormEnhancements = runtime.initRecordFormEnhancements;
    }
    if (typeof runtime.prepareRecordEditorFormNavigation === "function") {
        navigationRuntime.prepareRecordEditorFormNavigation = runtime.prepareRecordEditorFormNavigation;
    }
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

export function applyProjectIndexFilters(scope, options = {}) {
    const root = resolveQueryRoot(scope);
    const shell = root.querySelector("[data-project-list-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    syncProjectListStatusFilterInputs(root, options.hideDoneStorageKey, options.hideDeletedStorageKey);

    const hiddenStatusCodes = Array.from(root.querySelectorAll("[data-project-status-hide]"))
        .filter((input) => input instanceof HTMLInputElement && input.checked)
        .map((input) => (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase())
        .filter((value) => value.length > 0);

    let visibleCount = 0;
    shell.querySelectorAll("[data-project-status-code]").forEach((card) => {
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const statusCode = (card.getAttribute("data-project-status-code") || "").trim().toUpperCase();
        const shouldHide = hiddenStatusCodes.includes(statusCode);
        card.hidden = shouldHide;
        if (!shouldHide) {
            visibleCount += 1;
        }
    });

    const emptyState = shell.querySelector("[data-project-grid-empty]");
    if (emptyState instanceof HTMLElement) {
        emptyState.hidden = visibleCount > 0;
    }
}

export function toggleProjectStatusFilterPanel(button) {
    if (!(button instanceof HTMLElement)) {
        return;
    }

    const panel = document.querySelector("[data-project-status-filter-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const nextOpen = panel.hidden;
    panel.hidden = !nextOpen;
    button.setAttribute("aria-expanded", String(nextOpen));
}

export function handleProjectStatusFilterInput(input, options = {}) {
    if (!(input instanceof HTMLInputElement)) {
        return;
    }

    const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
    if (statusCode === "DONE") {
        writeBooleanStorage(options.hideDoneStorageKey, input.checked);
    }
    else if (statusCode === "DELETED") {
        writeBooleanStorage(options.hideDeletedStorageKey, input.checked);
    }

    applyProjectIndexFilters(document, options);
}

export function initProjectIndexStatusFilters(scope, options = {}) {
    const root = resolveQueryRoot(scope);
    const shell = root.querySelector("[data-project-list-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const toggleButton = document.querySelector("[data-project-status-filter-toggle]");
    if (toggleButton instanceof HTMLButtonElement && toggleButton.dataset.boundProjectStatusFilter !== "true") {
        toggleButton.dataset.boundProjectStatusFilter = "true";
        toggleButton.addEventListener("click", () => {
            toggleProjectStatusFilterPanel(toggleButton);
        });
    }

    document.querySelectorAll("[data-project-status-hide]").forEach((input) => {
        if (input instanceof HTMLInputElement && input.dataset.boundProjectStatusFilter !== "true") {
            input.dataset.boundProjectStatusFilter = "true";
            input.addEventListener("change", () => {
                handleProjectStatusFilterInput(input, options);
            });
        }
    });

    syncProjectListStatusFilterInputs(document, options.hideDoneStorageKey, options.hideDeletedStorageKey);
    applyProjectIndexFilters(root, options);
}

export function toggleMeetingAttendancePanel(button) {
    if (!(button instanceof HTMLButtonElement)) {
        return;
    }

    const card = button.closest("[data-meeting-attendance-card]");
    if (!(card instanceof HTMLElement)) {
        return;
    }

    const panel = card.querySelector("[data-meeting-attendance-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const shouldOpen = panel.hidden;
    panel.hidden = !shouldOpen;
    button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
    button.textContent = shouldOpen ? "Skrýt účast" : "Zobrazit účast";
}

export function initCiselnikAjaxSwitch(options = {}) {
    const shell = document.querySelector("[data-ciselnik-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const panel = shell.querySelector("[data-ciselnik-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const contentUrl = shell.getAttribute("data-content-url");
    if (!contentUrl) {
        return;
    }

    const initRecordFormEnhancements = typeof options.initRecordFormEnhancements === "function"
        ? options.initRecordFormEnhancements
        : () => {};

    const getCurrentKeyFromUrl = () => new URL(window.location.href).searchParams.get("id");

    const loadDetail = async (key, push, href) => {
        if (!key) {
            return;
        }

        panel.setAttribute("aria-busy", "true");

        try {
            const endpoint = `${contentUrl}?id=${encodeURIComponent(key)}`;
            const html = await fetchPanelHtml(endpoint);
            panel.innerHTML = html;
            initRecordFormEnhancements(panel);
            setActiveLink(shell, "[data-ciselnik-link]", key);

            const nextState = { ...(history.state || {}), ciselnikKey: key };
            if (push && href) {
                history.pushState(nextState, "", href);
            }
            else if (!push) {
                history.replaceState(nextState, "", window.location.href);
            }
        }
        catch (error) {
            if (href) {
                window.location.href = href;
            }
        }
        finally {
            panel.setAttribute("aria-busy", "false");
        }
    };

    const initialActiveLink = shell.querySelector("[data-ciselnik-link].active");
    const initialKey = initialActiveLink instanceof HTMLAnchorElement
        ? initialActiveLink.dataset.key
        : getCurrentKeyFromUrl();

    if (initialKey) {
        history.replaceState(
            { ...(history.state || {}), ciselnikKey: initialKey },
            "",
            window.location.href);
        setActiveLink(shell, "[data-ciselnik-link]", initialKey);
    }

    shell.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const link = target.closest("[data-ciselnik-link]");
        if (!(link instanceof HTMLAnchorElement) || !isPrimaryNavigationClick(event)) {
            return;
        }

        event.preventDefault();
        void loadDetail(link.dataset.key, true, link.href);
    });

    window.addEventListener("popstate", (event) => {
        const key = event.state?.ciselnikKey || getCurrentKeyFromUrl();
        const link = shell.querySelector(`[data-ciselnik-link][data-key="${key}"]`);
        const href = link instanceof HTMLAnchorElement ? link.href : null;
        if (key) {
            void loadDetail(key, false, href);
        }
    });
}

export function initSettingsAjaxSwitch() {
    const shell = document.querySelector("[data-settings-shell]");
    if (!(shell instanceof HTMLElement)) {
        return;
    }

    const panel = shell.querySelector("[data-settings-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const panelUrl = shell.getAttribute("data-panel-url");
    if (!panelUrl) {
        return;
    }

    const getCurrentStateFromUrl = () => {
        const url = new URL(window.location.href);
        return {
            section: url.searchParams.get("section") || "role",
            userId: url.searchParams.get("userId"),
            projektId: url.searchParams.get("projektId")
        };
    };

    const buildHref = (section, userId, projektId) => {
        const url = new URL(window.location.href);
        url.searchParams.set("section", section);

        if (userId) {
            url.searchParams.set("userId", userId);
        }
        else {
            url.searchParams.delete("userId");
        }

        if (projektId) {
            url.searchParams.set("projektId", projektId);
        }
        else {
            url.searchParams.delete("projektId");
        }

        return `${url.pathname}${url.search}${url.hash}`;
    };

    const loadSection = async (section, push, options = {}) => {
        if (!section) {
            return;
        }

        const userId = options.userId ?? null;
        const projektId = options.projektId ?? null;
        const fallbackHref = options.href ?? null;
        panel.setAttribute("aria-busy", "true");

        try {
            const endpointUrl = new URL(panelUrl, window.location.origin);
            endpointUrl.searchParams.set("section", section);
            if (userId) {
                endpointUrl.searchParams.set("userId", userId);
            }
            if (projektId) {
                endpointUrl.searchParams.set("projektId", projektId);
            }

            const html = await fetchPanelHtml(`${endpointUrl.pathname}${endpointUrl.search}`);
            panel.innerHTML = html;
            setActiveLink(shell, "[data-settings-link]", section);

            const nextHref = buildHref(section, userId, projektId);
            const nextState = {
                ...(history.state || {}),
                settingsSection: section,
                settingsUserId: userId,
                settingsProjektId: projektId
            };

            if (push) {
                history.pushState(nextState, "", nextHref);
            }
            else {
                history.replaceState(nextState, "", nextHref);
            }
        }
        catch (error) {
            if (fallbackHref) {
                window.location.href = fallbackHref;
            }
        }
        finally {
            panel.setAttribute("aria-busy", "false");
        }
    };

    const initialStateFromUrl = getCurrentStateFromUrl();
    const initialActiveLink = shell.querySelector("[data-settings-link].active");
    const initialSection = initialActiveLink instanceof HTMLAnchorElement
        ? initialActiveLink.dataset.key
        : initialStateFromUrl.section;

    history.replaceState(
        {
            ...(history.state || {}),
            settingsSection: initialSection || "role",
            settingsUserId: initialStateFromUrl.userId,
            settingsProjektId: initialStateFromUrl.projektId
        },
        "",
        window.location.href);

    shell.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const link = target.closest("[data-settings-link]");
        if (!(link instanceof HTMLAnchorElement) || !isPrimaryNavigationClick(event)) {
            return;
        }

        event.preventDefault();
        const linkUrl = new URL(link.href);
        void loadSection(link.dataset.key, true, {
            href: link.href,
            userId: linkUrl.searchParams.get("userId"),
            projektId: linkUrl.searchParams.get("projektId")
        });
    });

    shell.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof Element) || !target.matches("[data-settings-filter-user], [data-settings-filter-project]")) {
            return;
        }

        const form = target.closest("[data-settings-filter-form]");
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const sectionInput = form.querySelector('input[name="section"]');
        const userSelect = form.querySelector("[data-settings-filter-user]");
        const projectSelect = form.querySelector("[data-settings-filter-project]");

        void loadSection(
            sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava",
            true,
            {
                userId: userSelect instanceof HTMLSelectElement ? userSelect.value : null,
                projektId: projectSelect instanceof HTMLSelectElement ? projectSelect.value : null
            });
    });

    shell.addEventListener("submit", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLFormElement) || !target.matches("[data-settings-filter-form]")) {
            return;
        }

        event.preventDefault();
        const sectionInput = target.querySelector('input[name="section"]');
        const userSelect = target.querySelector("[data-settings-filter-user]");
        const projectSelect = target.querySelector("[data-settings-filter-project]");

        void loadSection(
            sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava",
            true,
            {
                userId: userSelect instanceof HTMLSelectElement ? userSelect.value : null,
                projektId: projectSelect instanceof HTMLSelectElement ? projectSelect.value : null
            });
    });

    window.addEventListener("popstate", (event) => {
        const urlState = getCurrentStateFromUrl();
        const section = event.state?.settingsSection || urlState.section || "role";
        const userId = event.state?.settingsUserId ?? urlState.userId;
        const projektId = event.state?.settingsProjektId ?? urlState.projektId;
        const link = shell.querySelector(`[data-settings-link][data-key="${section}"]`);
        const href = link instanceof HTMLAnchorElement ? link.href : null;
        void loadSection(section, false, { href, userId, projektId });
    });
}

export function initProfileRightsFilter() {
    const form = document.querySelector("[data-profile-rights-form]");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const projectSelect = form.querySelector("[data-profile-rights-project]");
    if (!(projectSelect instanceof HTMLSelectElement)) {
        return;
    }

    projectSelect.addEventListener("change", () => {
        const url = new URL(window.location.href);
        if (projectSelect.value) {
            url.searchParams.set("projektId", projectSelect.value);
        }
        else {
            url.searchParams.delete("projektId");
        }

        url.hash = "moje-prava";
        window.location.href = url.toString();
    });
}

export function initUserMenu() {
    const menu = document.querySelector("[data-user-menu]");
    if (!(menu instanceof HTMLElement)) {
        return;
    }

    const toggle = menu.querySelector("[data-user-menu-toggle]");
    const panel = menu.querySelector("[data-user-menu-panel]");
    if (!(toggle instanceof HTMLButtonElement) || !(panel instanceof HTMLElement)) {
        return;
    }

    const setOpen = (open) => {
        panel.hidden = !open;
        toggle.setAttribute("aria-expanded", String(open));
    };

    toggle.addEventListener("click", () => {
        setOpen(panel.hidden);
    });

    document.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (!menu.contains(target)) {
            setOpen(false);
        }
    });

    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") {
            setOpen(false);
        }
    });
}

export function toggleRecordCard(card) {
    if (!(card instanceof HTMLElement)) {
        return;
    }

    const isCollapsed = card.classList.contains("collapsed");
    card.classList.toggle("collapsed", !isCollapsed);
    const header = card.querySelector("[data-record-toggle]");
    if (header instanceof HTMLElement) {
        header.setAttribute("aria-expanded", String(isCollapsed));
    }
}

export function handleNavigationCardClick(target) {
    if (!(target instanceof Element)) {
        return false;
    }

    const navCard = target.closest("[data-href]");
    if (!(navCard instanceof HTMLElement)) {
        return false;
    }

    if (target.closest(cardInteractiveSelector)) {
        return false;
    }

    const href = navCard.getAttribute("data-href");
    if (!href) {
        return false;
    }

    window.location.href = href;
    return true;
}

export function handleNavigationCardKeydown(event, target) {
    if (!(event instanceof KeyboardEvent) || !(target instanceof Element)) {
        return false;
    }

    if (event.key !== "Enter" && event.key !== " ") {
        return false;
    }

    if (!(target instanceof HTMLElement) || !target.matches("[data-href]")) {
        return false;
    }

    const href = target.getAttribute("data-href");
    if (!href) {
        return false;
    }

    event.preventDefault();
    window.location.href = href;
    return true;
}


export async function fetchHtmlDocument(url) {
    const response = await fetch(url, {
        headers: { "X-Requested-With": "XMLHttpRequest" },
        credentials: "same-origin"
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    const html = await response.text();
    return new DOMParser().parseFromString(html, "text/html");
}

export async function fetchHtmlFragment(url) {
    const response = await fetch(url, {
        headers: { "X-Requested-With": "XMLHttpRequest" },
        credentials: "same-origin"
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    return response.text();
}

export function isElementInHiddenTree(element) {
    let current = element;
    while (current instanceof HTMLElement) {
        if (current.hidden) {
            return true;
        }

        current = current.parentElement;
    }

    return false;
}

export function buildRecordUiState(scopeRoot) {
    const root = scopeRoot instanceof HTMLElement ? scopeRoot : document;
    const expandedRecordIds = Array.from(root.querySelectorAll('.record-card[data-record-id]'))
        .filter((card) => card instanceof HTMLElement && !isElementInHiddenTree(card) && !card.classList.contains("collapsed"))
        .map((card) => card.getAttribute("data-record-id") || "")
        .filter(Boolean);

    const commentSortDirectionByRecordId = {};
    root.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
        if (!(card instanceof HTMLElement)) {
            return;
        }
        if (isElementInHiddenTree(card)) {
            return;
        }

        const recordId = card.getAttribute("data-record-id");
        if (!recordId) {
            return;
        }

        const section = card.querySelector("[data-comment-sort-section]");
        if (!(section instanceof HTMLElement)) {
            return;
        }

        commentSortDirectionByRecordId[recordId] = section.getAttribute("data-comment-sort-direction") === "desc"
            ? "desc"
            : "asc";
    });

    return {
        activeTab: localStorage.getItem("pmtracker.tab.active") || "zaznamy",
        scrollY: window.scrollY,
        expandedRecordIds,
        commentSortDirectionByRecordId
    };
}

export function restoreRecordUiState(state) {
    if (!state || typeof state !== "object") {
        return;
    }

    if (typeof state.activeTab === "string" && state.activeTab) {
        setActiveTab(state.activeTab);
        syncTabQuery(state.activeTab);
    }

    const expandedSet = new Set(Array.isArray(state.expandedRecordIds) ? state.expandedRecordIds : []);
    document.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const recordId = card.getAttribute("data-record-id") || "";
        const shouldExpand = expandedSet.has(recordId);
        card.classList.toggle("collapsed", !shouldExpand);
        const header = card.querySelector("[data-record-toggle]");
        if (header instanceof HTMLElement) {
            header.setAttribute("aria-expanded", String(shouldExpand));
        }
    });

    const directionMap = state.commentSortDirectionByRecordId && typeof state.commentSortDirectionByRecordId === "object"
        ? state.commentSortDirectionByRecordId
        : {};
    Object.entries(directionMap).forEach(([recordId, direction]) => {
        document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"] [data-comment-sort-section]`)
            .forEach((section) => {
                if (!(section instanceof HTMLElement)) {
                    return;
                }
                const normalizedDirection = direction === "desc" ? "desc" : "asc";
                applyCommentSort(section, normalizedDirection);
                const toggle = section.querySelector("[data-comment-sort-toggle]");
                if (toggle instanceof HTMLButtonElement) {
                    setCommentSortButtonLabel(toggle, normalizedDirection);
                }
            });
    });

    if (typeof state.scrollY === "number" && Number.isFinite(state.scrollY)) {
        window.scrollTo({ top: state.scrollY, behavior: "auto" });
    }
}

export async function refreshRecordCard(payload) {
    const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
    const recordId = payload.recordId != null ? String(payload.recordId) : "";
    if (!refreshUrl || !recordId) {
        return;
    }

    const currentCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`))
        .filter((card) => card instanceof HTMLElement);
    if (currentCards.length === 0) {
        return;
    }

    const anchorCurrentCard = currentCards.find((card) => !isElementInHiddenTree(card)) ?? currentCards[0];
    const beforeTop = anchorCurrentCard.getBoundingClientRect().top;
    const wasCollapsed = anchorCurrentCard.classList.contains("collapsed");
    const previousSortDirection = anchorCurrentCard.querySelector("[data-comment-sort-section]")?.getAttribute("data-comment-sort-direction") === "desc"
        ? "desc"
        : "asc";

    const html = await fetchHtmlFragment(refreshUrl);
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const replacementCard = parsed.querySelector(".record-card[data-record-id]");
    if (!(replacementCard instanceof HTMLElement)) {
        throw new Error("Nepodařilo se načíst aktualizovanou kartu záznamu.");
    }

    currentCards.forEach((card, index) => {
        const nextCard = index === 0 ? replacementCard : replacementCard.cloneNode(true);
        card.replaceWith(nextCard);
    });

    const refreshedCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`))
        .filter((card) => card instanceof HTMLElement);
    refreshedCards.forEach((card) => {
        card.classList.toggle("collapsed", wasCollapsed);
        const header = card.querySelector("[data-record-toggle]");
        if (header instanceof HTMLElement) {
            header.setAttribute("aria-expanded", String(!wasCollapsed));
        }
        const section = card.querySelector("[data-comment-sort-section]");
        if (section instanceof HTMLElement) {
            applyCommentSort(section, previousSortDirection);
            const toggle = section.querySelector("[data-comment-sort-toggle]");
            if (toggle instanceof HTMLButtonElement) {
                setCommentSortButtonLabel(toggle, previousSortDirection);
            }
        }
        navigationRuntime.initRecordFormEnhancements?.(card);
    });

    const anchorRefreshedCard = refreshedCards.find((card) => !isElementInHiddenTree(card)) ?? refreshedCards[0];
    if (anchorRefreshedCard instanceof HTMLElement) {
        const afterTop = anchorRefreshedCard.getBoundingClientRect().top;
        const delta = afterTop - beforeTop;
        if (Math.abs(delta) > 1) {
            window.scrollBy({ top: delta, behavior: "auto" });
        }
    }
}

export async function refreshMeetingTaskItem(payload) {
    const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
    const recordId = payload.recordId != null ? String(payload.recordId) : "";
    if (!refreshUrl || !recordId) {
        return;
    }

    const currentTask = document.querySelector(`.task-item[data-task-record-id="${CSS.escape(recordId)}"]`);
    if (!(currentTask instanceof HTMLElement)) {
        return;
    }

    const beforeTop = currentTask.getBoundingClientRect().top;
    const previousSortDirection = currentTask.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";

    const html = await fetchHtmlFragment(refreshUrl);
    const parsed = new DOMParser().parseFromString(html, "text/html");
    const replacementTask = parsed.querySelector(".task-item[data-task-record-id]");
    if (!(replacementTask instanceof HTMLElement)) {
        throw new Error("Nepodařilo se načíst aktualizovaný blok úkolu.");
    }

    currentTask.replaceWith(replacementTask);
    navigationRuntime.initRecordFormEnhancements?.(replacementTask);

    const refreshedTask = document.querySelector(`.task-item[data-task-record-id="${CSS.escape(recordId)}"]`);
    if (refreshedTask instanceof HTMLElement) {
        applyCommentSort(refreshedTask, previousSortDirection);
        const toggle = refreshedTask.querySelector("[data-comment-sort-toggle]");
        if (toggle instanceof HTMLButtonElement) {
            setCommentSortButtonLabel(toggle, previousSortDirection);
        }

        const afterTop = refreshedTask.getBoundingClientRect().top;
        const delta = afterTop - beforeTop;
        if (Math.abs(delta) > 1) {
            window.scrollBy({ top: delta, behavior: "auto" });
        }
    }
}

export function replaceSelectorFromDocument(nextDoc, selector) {
    const current = document.querySelector(selector);
    const replacement = nextDoc.querySelector(selector);
    if (!(current instanceof HTMLElement) || !(replacement instanceof HTMLElement)) {
        return false;
    }
    current.replaceWith(replacement);
    return true;
}

export async function refreshProjectSchedulePanels() {
    const hasSchedulePanel = document.querySelector('[data-tab-panel="harmonogram"]') instanceof HTMLElement;
    if (!hasSchedulePanel) {
        return;
    }

    const refreshUrl = new URL(window.location.href);
    refreshUrl.searchParams.set("tab", "harmonogram");
    const nextDoc = await fetchHtmlDocument(refreshUrl.toString());
    replaceSelectorFromDocument(nextDoc, '[data-tab-panel="harmonogram"]');
    queueRainbowSegmentRender(document);
}

export async function refreshPageScope(payload) {
    if (!payload || typeof payload !== "object") {
        return;
    }

    closeAllFloatingPanels();

    const scope = typeof payload.refreshScope === "string" ? payload.refreshScope : "";
    const refreshUrl = typeof payload.refreshUrl === "string" && payload.refreshUrl
        ? payload.refreshUrl
        : window.location.href;

    if (!scope) {
        return;
    }

    if (scope === "page") {
        const pageEditorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
        if (pageEditorForm instanceof HTMLFormElement) {
            navigationRuntime.prepareRecordEditorFormNavigation?.(pageEditorForm);
        }

        window.location.assign(refreshUrl);
        return;
    }

    if (scope === "record-card") {
        await refreshRecordCard(payload);
        initProjectRecordsUi();
        initProjectScheduleUi();
        initCommentSortUi(document);
        return;
    }

    if (scope === "record-card-with-schedules") {
        const activeTab = localStorage.getItem("pmtracker.tab.active") || "zaznamy";
        const scrollY = window.scrollY;

        await refreshRecordCard(payload);
        await refreshProjectSchedulePanels();

        initProjectTabs();
        initProjectRecordsUi();
        initProjectScheduleUi();
        initCommentSortUi(document);
        setActiveTab(activeTab);
        syncTabQuery(activeTab);
        window.scrollTo({ top: scrollY, behavior: "auto" });
        return;
    }

    if (scope === "meeting-task-item") {
        await refreshMeetingTaskItem(payload);
        initCommentSortUi(document);
        return;
    }

    if (scope === "nastaveni-panel") {
        const shell = document.querySelector("[data-settings-shell]");
        const panel = shell?.querySelector("[data-settings-panel]");
        if (!(shell instanceof HTMLElement) || !(panel instanceof HTMLElement)) {
            return;
        }

        const endpointUrl = new URL(refreshUrl, window.location.origin);
        const section = endpointUrl.searchParams.get("section") || "role";
        const userId = endpointUrl.searchParams.get("userId");
        const projektId = endpointUrl.searchParams.get("projektId");

        panel.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(`${endpointUrl.pathname}${endpointUrl.search}`, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            panel.innerHTML = await response.text();
            navigationRuntime.initRecordFormEnhancements?.(panel);

            shell.querySelectorAll("[data-settings-link]").forEach((link) => {
                if (!(link instanceof HTMLAnchorElement)) {
                    return;
                }

                const key = link.dataset.key || "role";
                const linkUrl = new URL(link.href, window.location.origin);
                linkUrl.searchParams.set("section", key);
                if (userId) {
                    linkUrl.searchParams.set("userId", userId);
                } else {
                    linkUrl.searchParams.delete("userId");
                }
                if (projektId) {
                    linkUrl.searchParams.set("projektId", projektId);
                } else {
                    linkUrl.searchParams.delete("projektId");
                }

                link.href = `${linkUrl.pathname}${linkUrl.search}${linkUrl.hash}`;
                const active = key === section;
                link.classList.toggle("active", active);
                if (active) {
                    link.setAttribute("aria-current", "page");
                } else {
                    link.removeAttribute("aria-current");
                }
            });

            const nextUrl = new URL(window.location.href);
            nextUrl.searchParams.set("section", section);
            if (userId) {
                nextUrl.searchParams.set("userId", userId);
            } else {
                nextUrl.searchParams.delete("userId");
            }
            if (projektId) {
                nextUrl.searchParams.set("projektId", projektId);
            } else {
                nextUrl.searchParams.delete("projektId");
            }

            history.replaceState(
                {
                    ...(history.state || {}),
                    settingsSection: section,
                    settingsUserId: userId,
                    settingsProjektId: projektId
                },
                "",
                `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`
            );
        } finally {
            panel.setAttribute("aria-busy", "false");
        }

        return;
    }

    if (scope === "ciselniky-detail") {
        const panel = document.querySelector("[data-ciselnik-panel]");
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const response = await fetch(refreshUrl, {
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        });
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        panel.innerHTML = await response.text();
        navigationRuntime.initRecordFormEnhancements?.(panel);
        return;
    }

    const nextDoc = await fetchHtmlDocument(refreshUrl);

    switch (scope) {
        case "projekty-index":
            replaceSelectorFromDocument(nextDoc, "[data-project-list-shell]");
            initProjectIndexUi();
            break;
        case "osoby-index":
            replaceSelectorFromDocument(nextDoc, "[data-osoby-table-card]");
            break;
        case "projekty-detail-zaznamy":
        case "projekty-detail-jednani":
        case "projekty-detail-tym":
        case "projekty-detail-zaznamy-preserve": {
            const preserveRecordUi = scope === "projekty-detail-zaznamy-preserve";
            const recordUiState = preserveRecordUi ? buildRecordUiState(document) : null;
            const tab = typeof payload.tab === "string" && payload.tab
                ? payload.tab
                    : (scope === "projekty-detail-zaznamy"
                        ? "zaznamy"
                        : scope === "projekty-detail-jednani"
                            ? "jednani"
                            : "tym");
            const targetTabPanelKey = tab === "harmonogram" || tab === "gant"
                ? "harmonogram"
                : tab;

            if (targetTabPanelKey === "harmonogram") {
                replaceSelectorFromDocument(nextDoc, '[data-tab-panel="harmonogram"]');
            } else {
                replaceSelectorFromDocument(nextDoc, `[data-tab-panel="${targetTabPanelKey}"]`);
            }

            const refreshedPanel = document.querySelector(`[data-tab-panel="${targetTabPanelKey}"]`);
            if (refreshedPanel instanceof HTMLElement) {
                navigationRuntime.initRecordFormEnhancements?.(refreshedPanel);
            }
            setActiveTab(tab);
            syncTabQuery(tab);
            initProjectTabs();
            initProjectRecordsUi();
            initProjectScheduleUi();
            initCommentSortUi(document);
            if (preserveRecordUi) {
                restoreRecordUiState(recordUiState);
            }
            break;
        }
        default:
            break;
    }
}

export function initProjectRecordPageshowSync() {
    if (!(document.body instanceof HTMLElement) || document.body.dataset.projectRecordPageshowSyncReady === "true") {
        return;
    }

    const hasProjectRecordsPanel = document.querySelector('[data-tab-panel="zaznamy"]') instanceof HTMLElement;
    if (!hasProjectRecordsPanel) {
        return;
    }

    document.body.dataset.projectRecordPageshowSyncReady = "true";
    window.addEventListener("pageshow", async (event) => {
        if (!event.persisted) {
            return;
        }

        try {
            const refreshUrl = new URL(window.location.href);
            refreshUrl.searchParams.set("tab", "zaznamy");
            await refreshPageScope({
                refreshScope: "projekty-detail-zaznamy-preserve",
                refreshUrl: refreshUrl.toString(),
                tab: "zaznamy"
            });
        } catch {
            // Fallback na plný reload drží panel ve konzistentním stavu i po chybě AJAX refresh.
            window.location.reload();
        }
    });
}
