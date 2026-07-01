import {
    getCommentSortDirection,
    initCommentSortUi,
    readRecordCommentsPanelState
} from "./comments.js";
import { invalidateRecordMeetingCommentStates } from "./filters.js";
import { navigationRuntime } from "./navigationRuntime.js";
import {
    fetchHtmlDocument,
    fetchHtmlFragment,
    isElementInHiddenTree,
    parseHtmlFragment
} from "./navigationShared.js";
import {
    applyRecordCommentSortDirection,
    loadRecordComments,
    loadRecordDetail
} from "./recordLazyLoading.js";
import {
    initProjectRecordsUi,
    initProjectTabs,
    loadProjectTabPanel,
    setActiveTab,
    syncTabQuery
} from "./projectTabs.js";
import { initMeetingOverview } from "./meetingOverview.js";
import { initProjectScheduleUi } from "./schedule.js";
import { initProposalFilterUi } from "./filters/index.js";
import { closeAllFloatingPanels } from "./ui.js";

function invalidateRecordMeetingCommentStateCacheForPayload(payload) {
    const projectId = payload?.projectId != null ? String(payload.projectId) : "";
    if (!projectId) {
        return;
    }

    invalidateRecordMeetingCommentStates(projectId);
}

function readRecordCommentsReloadState(card) {
    if (!(card instanceof HTMLElement)) {
        return {
            loadedCount: 0,
            loadAll: false,
            sortDirection: "asc"
        };
    }

    const section = card.querySelector("[data-comment-sort-section]");
    const panelState = readRecordCommentsPanelState(section instanceof HTMLElement ? section : card);

    return {
        loadedCount: panelState?.loadedCount ?? 0,
        loadAll: panelState?.isFullyLoaded === true && (panelState?.totalCount ?? 0) > 0,
        sortDirection: getCommentSortDirection(section instanceof HTMLElement ? section : card)
    };
}

export function buildRecordUiState(scopeRoot) {
    const root = scopeRoot instanceof HTMLElement ? scopeRoot : document;
    const expandedRecordIds = Array.from(root.querySelectorAll(".record-card[data-record-id]"))
        .filter((card) => card instanceof HTMLElement && !isElementInHiddenTree(card) && !card.classList.contains("collapsed"))
        .map((card) => card.getAttribute("data-record-id") || "")
        .filter(Boolean);
    const loadedCommentRecordIds = Array.from(root.querySelectorAll(".record-card[data-record-id]"))
        .filter((card) => card instanceof HTMLElement
            && !isElementInHiddenTree(card)
            && (card.dataset.recordCommentsLoaded === "true"
                || card.querySelector('[data-record-comments-shell][data-record-comments-loaded="true"]') instanceof HTMLElement))
        .map((card) => card.getAttribute("data-record-id") || "")
        .filter(Boolean);

    const commentSortDirectionByRecordId = {};
    const commentLoadStateByRecordId = {};
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

        const commentsState = readRecordCommentsReloadState(card);
        commentSortDirectionByRecordId[recordId] = commentsState.sortDirection;
        commentLoadStateByRecordId[recordId] = {
            loadedCount: commentsState.loadedCount,
            loadAll: commentsState.loadAll
        };
    });

    return {
        activeTab: localStorage.getItem("pmtracker.tab.active") || "zaznamy",
        scrollY: window.scrollY,
        expandedRecordIds,
        loadedCommentRecordIds,
        commentSortDirectionByRecordId,
        commentLoadStateByRecordId
    };
}

export async function restoreRecordUiState(state) {
    if (!state || typeof state !== "object") {
        return;
    }

    if (typeof state.activeTab === "string" && state.activeTab) {
        setActiveTab(state.activeTab);
        syncTabQuery(state.activeTab);
    }

    const expandedSet = new Set(Array.isArray(state.expandedRecordIds) ? state.expandedRecordIds : []);
    const loadedCommentSet = new Set(Array.isArray(state.loadedCommentRecordIds) ? state.loadedCommentRecordIds : []);
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
    const loadStateMap = state.commentLoadStateByRecordId && typeof state.commentLoadStateByRecordId === "object"
        ? state.commentLoadStateByRecordId
        : {};
    const restoreTasks = [];

    document.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const recordId = card.getAttribute("data-record-id") || "";
        if (!recordId || !expandedSet.has(recordId)) {
            return;
        }

        restoreTasks.push((async () => {
            await loadRecordDetail(card);
            if (loadedCommentSet.has(recordId)) {
                const loadState = loadStateMap[recordId] || {};
                await loadRecordComments(card, {
                    sortDirection: directionMap[recordId],
                    limit: loadState.loadedCount,
                    loadAll: loadState.loadAll === true
                });
            }
            else if (directionMap[recordId] === "asc" || directionMap[recordId] === "desc") {
                applyRecordCommentSortDirection(card, directionMap[recordId]);
            }
        })());
    });

    await Promise.all(restoreTasks);

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
    const detailWasLoaded = anchorCurrentCard.dataset.recordDetailLoaded === "true";
    const commentsWereLoaded = anchorCurrentCard.dataset.recordCommentsLoaded === "true";
    const previousCommentState = readRecordCommentsReloadState(anchorCurrentCard);

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
        navigationRuntime.initRecordFormEnhancements?.(card);
    });

    const hydrateTasks = refreshedCards
        .filter((card) => card instanceof HTMLElement && !card.classList.contains("collapsed"))
        .map(async (card) => {
            await loadRecordDetail(card, { force: detailWasLoaded });
            if (commentsWereLoaded) {
                await loadRecordComments(card, {
                    sortDirection: previousCommentState.sortDirection,
                    limit: previousCommentState.loadedCount,
                    loadAll: previousCommentState.loadAll
                });
            }
        });
    await Promise.all(hydrateTasks);

    const anchorRefreshedCard = refreshedCards.find((card) => !isElementInHiddenTree(card)) ?? refreshedCards[0];
    if (anchorRefreshedCard instanceof HTMLElement) {
        const afterTop = anchorRefreshedCard.getBoundingClientRect().top;
        const delta = afterTop - beforeTop;
        if (Math.abs(delta) > 1) {
            window.scrollBy({ top: delta, behavior: "auto" });
        }
    }
}

export async function refreshRecordComments(payload) {
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

    await Promise.all(currentCards.map((card) => {
        const commentsState = readRecordCommentsReloadState(card);
        return loadRecordComments(card, {
            force: true,
            url: refreshUrl,
            sortDirection: commentsState.sortDirection,
            limit: commentsState.loadedCount,
            loadAll: commentsState.loadAll
        });
    }));
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
        applyRecordCommentSortDirection(refreshedTask, previousSortDirection);

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
    const panel = document.querySelector('[data-tab-panel="harmonogram"]');
    if (!(panel instanceof HTMLElement) || panel.dataset.projectTabLoaded !== "true") {
        return;
    }

    await loadProjectTabPanel(panel, { force: true });
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
        invalidateRecordMeetingCommentStateCacheForPayload(payload);
        await refreshRecordCard(payload);
        initProjectRecordsUi();
        initProjectScheduleUi();
        initCommentSortUi(document);
        return;
    }

    if (scope === "record-comments") {
        invalidateRecordMeetingCommentStateCacheForPayload(payload);
        await refreshRecordComments(payload);
        initCommentSortUi(document);
        return;
    }

    if (scope === "record-card-with-schedules") {
        invalidateRecordMeetingCommentStateCacheForPayload(payload);
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
            panel.innerHTML = await fetchHtmlFragment(`${endpointUrl.pathname}${endpointUrl.search}`);
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

        panel.innerHTML = await fetchHtmlFragment(refreshUrl);
        navigationRuntime.initRecordFormEnhancements?.(panel);
        return;
    }

    switch (scope) {
        case "projekty-index":
        {
            const nextDoc = await fetchHtmlDocument(refreshUrl);
            replaceSelectorFromDocument(nextDoc, "[data-project-list-shell]");
            navigationRuntime.refreshProjectIndexFilters?.();
            break;
        }
        case "osoby-index":
        {
            const nextDoc = await fetchHtmlDocument(refreshUrl);
            replaceSelectorFromDocument(nextDoc, "[data-osoby-table-card]");
            break;
        }
        case "projekty-detail-zaznamy":
        case "projekty-detail-jednani":
        case "projekty-detail-tym":
        case "projekty-detail-navrhy":
        case "projekty-detail-zaznamy-preserve": {
            const preserveRecordUi = scope === "projekty-detail-zaznamy-preserve";
            const recordUiState = preserveRecordUi ? buildRecordUiState(document) : null;
            const tab = typeof payload.tab === "string" && payload.tab
                ? payload.tab
                : (scope === "projekty-detail-zaznamy"
                    ? "zaznamy"
                    : scope === "projekty-detail-jednani"
                        ? "jednani"
                        : scope === "projekty-detail-navrhy"
                            ? "navrhy"
                            : "tym");
            const targetTabPanelKey = tab === "harmonogram" || tab === "gant"
                ? "harmonogram"
                : tab;

            const html = await fetchHtmlFragment(refreshUrl);
            const nextDoc = parseHtmlFragment(html);
            const selector = `[data-tab-panel="${targetTabPanelKey}"]`;
            const replacement = nextDoc.querySelector(selector);
            const current = document.querySelector(selector);
            if (!(replacement instanceof HTMLElement) || !(current instanceof HTMLElement)) {
                throw new Error("Nepodařilo se obnovit detail projektu.");
            }

            current.replaceWith(replacement);

            const refreshedPanel = document.querySelector(`[data-tab-panel="${targetTabPanelKey}"]`);
            if (refreshedPanel instanceof HTMLElement) {
                if (targetTabPanelKey !== "zaznamy") {
                    refreshedPanel.dataset.projectTabLoaded = "true";
                    if (typeof refreshUrl === "string" && refreshUrl) {
                        refreshedPanel.dataset.projectTabLazyUrl = refreshUrl;
                    }
                }
                navigationRuntime.initRecordFormEnhancements?.(refreshedPanel);
            }
            setActiveTab(tab);
            syncTabQuery(tab);
            initProjectTabs();
            initProjectRecordsUi();
            initProjectScheduleUi();
            initProposalFilterUi();
            initMeetingOverview(document);
            initCommentSortUi(document);
            if (preserveRecordUi) {
                await restoreRecordUiState(recordUiState);
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
            const recordsPanel = document.querySelector('[data-tab-panel="zaznamy"]');
            const refreshUrl = recordsPanel instanceof HTMLElement
                ? (recordsPanel.dataset.projectTabRefreshUrl || window.location.href)
                : window.location.href;
            await refreshPageScope({
                refreshScope: "projekty-detail-zaznamy-preserve",
                refreshUrl,
                tab: "zaznamy"
            });
        } catch {
            // Fallback na plný reload drží panel ve konzistentním stavu i po chybě AJAX refresh.
            window.location.reload();
        }
    });
}
