/**
 * filters/recordDisplay.js — record filter visibility, view switching,
 * subsystem scroll indicator, and tab helpers.
 *
 * Fáze 3B Task 4: extrahováno z filters.js (1105 LOC → submodul ~310 LOC).
 *
 * Exports:
 *   setRecordFilterVisibility, applyProjectRecordFilters,
 *   scheduleSubsystemIndicatorSync, initSubsystemScrollIndicator,
 *   applyRecordsView, setActiveTab, syncTabQuery
 */

import {
    getProjectFilterConfig,
    getProjectFilterInput,
    getProjectFilterCurrentUserId,
    getProjectFilterStorageKey,
    buildProjectFilterStateFromInputs,
    setProjectFilterSaveStatus,
    normalizeSubsystemSortMode,
    compareSubsystemSortMeta
} from "./projectFilter.js";

// ---------------------------------------------------------------------------
// Constants used in this module
// ---------------------------------------------------------------------------

const recordMeetingCommentStateLoadingMessage = "Načítání dat pro filtr jednání-vyjádření...";
const recordMeetingCommentStateErrorMessage = "Nepodařilo se načíst data pro filtr jednání-vyjádření.";
const recordMeetingCommentStateCache = new Map();
const recordMeetingCommentStateRequests = new Map();

// ---------------------------------------------------------------------------
// Private helpers — DOM queries
// ---------------------------------------------------------------------------

function getProjectDetailRoot() {
    const root = document.querySelector("[data-project-detail-root]");
    return root instanceof HTMLElement ? root : null;
}

function getProjectRecordsPanel() {
    const panel = document.querySelector('[data-tab-panel="zaznamy"]');
    return panel instanceof HTMLElement ? panel : null;
}

function getProjectFilterProjectId(scope) {
    const config = getProjectFilterConfig(scope);
    if (!config) {
        return "0";
    }

    const root = document.querySelector(config.rootSelector);
    if (!(root instanceof HTMLElement)) {
        return "0";
    }

    const projectId = (root.dataset.projectId || "").trim();
    return projectId || "0";
}

// ---------------------------------------------------------------------------
// Private helpers — meeting comment state cache
// ---------------------------------------------------------------------------

function normalizeFilterToken(value) {
    if (value === null || value === undefined) {
        return "";
    }

    return String(value).trim().toUpperCase();
}

function normalizeRecordMeetingCommentStatesPayload(payload) {
    if (!payload || typeof payload !== "object") {
        return {};
    }

    const rawStates = payload.statesByRecordId && typeof payload.statesByRecordId === "object"
        ? payload.statesByRecordId
        : payload;
    const normalized = {};

    Object.entries(rawStates).forEach(([recordId, values]) => {
        const normalizedRecordId = String(recordId || "").trim();
        if (!normalizedRecordId) {
            return;
        }

        const normalizedValues = Array.isArray(values)
            ? values
                .map((value) => normalizeFilterToken(value))
                .filter(Boolean)
            : [];

        normalized[normalizedRecordId] = Array.from(new Set(normalizedValues));
    });

    return normalized;
}

function applyCachedRecordMeetingCommentStates(projectId) {
    const normalizedProjectId = String(projectId || "").trim();
    if (!normalizedProjectId || !recordMeetingCommentStateCache.has(normalizedProjectId)) {
        return false;
    }

    const statesByRecordId = recordMeetingCommentStateCache.get(normalizedProjectId) || {};
    const recordsPanel = getProjectRecordsPanel();
    if (!(recordsPanel instanceof HTMLElement)) {
        return false;
    }

    recordsPanel.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const recordId = (card.dataset.recordId || "").trim();
        const values = Array.isArray(statesByRecordId[recordId]) ? statesByRecordId[recordId] : [];
        card.dataset.filterVyjadreniJednaniStavy = values.join("|");
    });

    return true;
}

async function ensureRecordMeetingCommentStatesLoaded() {
    const projectRoot = getProjectDetailRoot();
    const projectId = getProjectFilterProjectId("records");
    const loadUrl = (projectRoot?.dataset.recordMeetingCommentStatesUrl || "").trim();
    if (!projectId || projectId === "0" || !loadUrl) {
        return false;
    }

    if (applyCachedRecordMeetingCommentStates(projectId)) {
        return true;
    }

    const existingRequest = recordMeetingCommentStateRequests.get(projectId);
    if (existingRequest instanceof Promise) {
        return existingRequest;
    }

    setProjectFilterSaveStatus("records", recordMeetingCommentStateLoadingMessage);

    const request = (async () => {
        try {
            const response = await fetch(loadUrl, {
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const payload = await response.json();
            recordMeetingCommentStateCache.set(projectId, normalizeRecordMeetingCommentStatesPayload(payload));
            applyCachedRecordMeetingCommentStates(projectId);
            setProjectFilterSaveStatus("records", "");
            applyProjectRecordFilters();
            return true;
        } catch (error) {
            setProjectFilterSaveStatus("records", recordMeetingCommentStateErrorMessage);
            return false;
        } finally {
            recordMeetingCommentStateRequests.delete(projectId);
        }
    })();

    recordMeetingCommentStateRequests.set(projectId, request);
    return request;
}

// ---------------------------------------------------------------------------
// Public exports — invalidate cache (used by recordRefresh.js)
// ---------------------------------------------------------------------------

export function invalidateRecordMeetingCommentStates(projectId) {
    const normalizedProjectId = String(projectId || "").trim();
    if (!normalizedProjectId) {
        return;
    }

    recordMeetingCommentStateCache.delete(normalizedProjectId);
    recordMeetingCommentStateRequests.delete(normalizedProjectId);
}

// ---------------------------------------------------------------------------
// Public exports — filter visibility
// ---------------------------------------------------------------------------

export function setRecordFilterVisibility(element, isVisible) {
    if (!(element instanceof HTMLElement)) {
        return;
    }

    element.classList.toggle("is-filter-hidden", !isVisible);
    element.hidden = !isVisible;
}

export function applyProjectRecordFilters() {
    const recordsPanel = getProjectRecordsPanel();
    if (!(recordsPanel instanceof HTMLElement)) {
        return;
    }

    const cards = recordsPanel.querySelectorAll(".record-card[data-record-id]");
    if (cards.length === 0) {
        return;
    }

    const state = buildProjectFilterStateFromInputs("records");
    const currentUserId = getProjectFilterCurrentUserId("records");
    const hasCurrentUser = currentUserId && currentUserId !== "0";
    const filters = {
        subsystem: normalizeFilterToken(state.subsystem),
        kategorie: normalizeFilterToken(state.kategorie),
        stav: normalizeFilterToken(state.stav),
        typ: normalizeFilterToken(state.typ),
        vlastnik: normalizeFilterToken(state.vlastnik),
        onlyActive: Boolean(state.aktivni),
        mine: Boolean(state.mine),
        meetingCommentState: normalizeFilterToken(state.jednaniVyjadreniStav)
    };
    const projectId = getProjectFilterProjectId("records");
    const hasMeetingCommentStateCache = applyCachedRecordMeetingCommentStates(projectId);
    if (filters.meetingCommentState && !hasMeetingCommentStateCache) {
        void ensureRecordMeetingCommentStatesLoaded();
    }

    cards.forEach((item) => {
        if (!(item instanceof HTMLElement)) {
            return;
        }

        const subsystem = normalizeFilterToken(item.dataset.filterSubsystemKod || item.dataset.filterSubsystem);
        const kategorie = normalizeFilterToken(item.dataset.filterKategorieKod || item.dataset.filterKategorie);
        const stav = normalizeFilterToken(item.dataset.filterStavKod || item.dataset.filterStav);
        const typ = normalizeFilterToken(item.dataset.filterTypKod || item.dataset.filterTyp);
        const vlastnik = normalizeFilterToken(item.dataset.filterVlastnikId || item.dataset.filterVlastnik);
        const isActive = item.dataset.filterAktivni === "true";
        const isTask = item.dataset.filterJeUkol === "true";
        const commentMeetingStates = (item.dataset.filterVyjadreniJednaniStavy || "")
            .split(/[|,]/g)
            .map((value) => normalizeFilterToken(value))
            .filter(Boolean);
        const matchesMeetingCommentState = !filters.meetingCommentState
            || !hasMeetingCommentStateCache
            || (isTask && commentMeetingStates.includes(filters.meetingCommentState));
        const matchesMine = !filters.mine || (hasCurrentUser && vlastnik === currentUserId);

        const matches =
            (!filters.subsystem || subsystem === filters.subsystem) &&
            (!filters.kategorie || kategorie === filters.kategorie) &&
            (!filters.stav || stav === filters.stav) &&
            (!filters.typ || typ === filters.typ) &&
            (!filters.vlastnik || vlastnik === filters.vlastnik) &&
            (!filters.onlyActive || isActive) &&
            matchesMine &&
            matchesMeetingCommentState;

        setRecordFilterVisibility(item, matches);
    });

    recordsPanel.querySelectorAll(".subsystem-group").forEach((group) => {
        if (!(group instanceof HTMLElement)) {
            return;
        }

        const hasVisibleCards = Array.from(group.querySelectorAll(".record-card"))
            .some((card) => card instanceof HTMLElement && !card.hidden);
        setRecordFilterVisibility(group, hasVisibleCards);
    });

    scheduleSubsystemIndicatorSync();
}

// ---------------------------------------------------------------------------
// Private helpers — subsystem scroll indicator
// ---------------------------------------------------------------------------

function resolveCurrentSubsystemGroup(groups, anchorY) {
    if (!Array.isArray(groups) || groups.length === 0) {
        return null;
    }

    let current = groups[0];
    for (const group of groups) {
        if (!(group instanceof HTMLElement)) {
            continue;
        }

        const rect = group.getBoundingClientRect();
        if (rect.bottom <= anchorY) {
            current = group;
            continue;
        }

        if (rect.top <= anchorY) {
            current = group;
        }
        break;
    }

    return current;
}

function resolveActiveSubsystemIndicatorShell() {
    const activePanel = document.querySelector(".tab-panel.active");
    if (!(activePanel instanceof HTMLElement)) {
        return null;
    }

    const groupedShell = activePanel.querySelector("[data-subsystem-grouped-shell]");
    if (!(groupedShell instanceof HTMLElement) || groupedShell.hidden) {
        return null;
    }

    return groupedShell;
}

function updateSubsystemScrollIndicator() {
    const indicator = document.querySelector("[data-subsystem-scroll-indicator]");
    const bubble = document.querySelector("[data-subsystem-scroll-indicator-bubble]");
    const label = document.querySelector("[data-subsystem-scroll-indicator-label]");

    if (!(indicator instanceof HTMLElement) || !(bubble instanceof HTMLElement) || !(label instanceof HTMLElement)) {
        return;
    }

    if (window.scrollY <= 0) {
        indicator.hidden = true;
        return;
    }

    const groupedShell = resolveActiveSubsystemIndicatorShell();
    if (!(groupedShell instanceof HTMLElement)) {
        indicator.hidden = true;
        return;
    }

    const visibleGroups = Array.from(groupedShell.querySelectorAll("[data-subsystem-group]"))
        .filter((group) => group instanceof HTMLElement && !group.hidden);

    if (visibleGroups.length === 0) {
        indicator.hidden = true;
        return;
    }

    const shellRect = groupedShell.getBoundingClientRect();
    if (shellRect.bottom <= 120 || shellRect.top >= window.innerHeight) {
        indicator.hidden = true;
        return;
    }

    const anchorY = Math.max(132, Math.min(window.innerHeight * 0.35, 220));
    const currentGroup = resolveCurrentSubsystemGroup(visibleGroups, anchorY);
    const subsystemName = currentGroup instanceof HTMLElement
        ? (currentGroup.getAttribute("data-subsystem-name") || "").trim()
        : "";

    if (!subsystemName) {
        indicator.hidden = true;
        return;
    }

    const bubbleTravel = Math.max(0, indicator.clientHeight - bubble.offsetHeight);
    const currentRect = currentGroup.getBoundingClientRect();
    const currentCenter = currentRect.top + (currentRect.height / 2);
    const progress = Math.max(0, Math.min(1, (currentCenter - shellRect.top) / Math.max(shellRect.height, 1)));
    bubble.style.transform = `translateY(${Math.round(progress * bubbleTravel)}px)`;
    label.textContent = subsystemName;
    indicator.hidden = false;
}

// ---------------------------------------------------------------------------
// Public exports — subsystem scroll indicator
// ---------------------------------------------------------------------------

export function scheduleSubsystemIndicatorSync() {
    if (!(document.body instanceof HTMLElement)) {
        return;
    }

    const currentFrame = Number.parseInt(document.body.dataset.subsystemIndicatorFrame || "0", 10);
    if (Number.isInteger(currentFrame) && currentFrame > 0) {
        window.cancelAnimationFrame(currentFrame);
    }

    const nextFrame = window.requestAnimationFrame(() => {
        document.body.dataset.subsystemIndicatorFrame = "0";
        updateSubsystemScrollIndicator();
    });
    document.body.dataset.subsystemIndicatorFrame = String(nextFrame);
}

export function initSubsystemScrollIndicator() {
    const indicator = document.querySelector("[data-subsystem-scroll-indicator]");
    if (!(indicator instanceof HTMLElement) || !(document.body instanceof HTMLElement)) {
        return;
    }

    if (document.body.dataset.subsystemIndicatorReady !== "true") {
        document.body.dataset.subsystemIndicatorReady = "true";
        window.addEventListener("scroll", scheduleSubsystemIndicatorSync, { passive: true });
        window.addEventListener("resize", scheduleSubsystemIndicatorSync);
    }

    scheduleSubsystemIndicatorSync();
}

// ---------------------------------------------------------------------------
// Public exports — records view switching
// ---------------------------------------------------------------------------

export function applyRecordsView(view) {
    const recordsPanel = getProjectRecordsPanel();
    if (!(recordsPanel instanceof HTMLElement)) {
        return;
    }

    const groupBySubsystemInput = getProjectFilterInput("records", "groupBySubsystem");
    const resolvedView = groupBySubsystemInput instanceof HTMLInputElement
        ? (groupBySubsystemInput.checked ? "subsystem" : "flat")
        : view;

    const shells = recordsPanel.querySelectorAll("[data-records-view]");
    if (shells.length === 0) {
        return;
    }

    const groupedList = recordsPanel.querySelector("[data-record-grouped-list]");
    const flatList = recordsPanel.querySelector("[data-record-flat-list]");
    const cards = Array.from(recordsPanel.querySelectorAll(".record-card[data-record-id]"))
        .filter((card) => card instanceof HTMLElement);
    const sortMode = normalizeSubsystemSortMode(buildProjectFilterStateFromInputs("records").sortBy);

    if (groupedList instanceof HTMLElement && flatList instanceof HTMLElement && cards.length > 0) {
        const groupsByKey = new Map();
        const orderedGroups = [];

        cards.forEach((card) => {
            const subsystemName = (card.getAttribute("data-filter-subsystem") || "").trim() || "-";
            const subsystemCode = (card.getAttribute("data-filter-subsystem-kod") || "").trim();
            const subsystemOrder = Number.parseInt(card.getAttribute("data-filter-subsystem-order") || "0", 10) || 0;
            const subsystemHasProjectOrder = card.getAttribute("data-filter-subsystem-order-active") === "true";
            const groupKey = `${subsystemCode}\u0000${subsystemName}`;
            let group = groupsByKey.get(groupKey);

            if (!group) {
                group = {
                    meta: {
                        name: subsystemName,
                        code: subsystemCode,
                        order: subsystemOrder,
                        hasProjectOrder: subsystemHasProjectOrder
                    },
                    cards: []
                };
                groupsByKey.set(groupKey, group);
                orderedGroups.push(group);
            }

            group.cards.push(card);
        });

        orderedGroups.sort((left, right) => compareSubsystemSortMeta(left.meta, right.meta, sortMode));

        if (resolvedView === "subsystem") {
            groupedList.innerHTML = "";

            orderedGroups.forEach((group) => {
                const currentGroup = document.createElement("div");
                currentGroup.className = "subsystem-group";
                currentGroup.setAttribute("data-subsystem-group", "");
                currentGroup.setAttribute("data-subsystem-name", group.meta.name);
                currentGroup.setAttribute("data-subsystem-kod", group.meta.code);
                currentGroup.setAttribute("data-subsystem-order", String(group.meta.order));
                currentGroup.setAttribute("data-subsystem-order-active", String(group.meta.hasProjectOrder));

                const heading = document.createElement("h3");
                heading.textContent = group.meta.name;
                currentGroup.appendChild(heading);

                const currentGroupCards = document.createElement("div");
                currentGroupCards.className = "card-list";
                group.cards.forEach((card) => currentGroupCards.appendChild(card));
                currentGroup.appendChild(currentGroupCards);
                groupedList.appendChild(currentGroup);
            });
        }
        else {
            orderedGroups.forEach((group) => {
                group.cards.forEach((card) => flatList.appendChild(card));
            });
            groupedList.innerHTML = "";
        }
    }

    shells.forEach((shell) => {
        const mode = shell.getAttribute("data-records-view");
        shell.toggleAttribute("hidden", mode !== resolvedView);
    });

    applyProjectRecordFilters();
    scheduleSubsystemIndicatorSync();
}

// ---------------------------------------------------------------------------
// Public exports — tab helpers
// ---------------------------------------------------------------------------

export function setActiveTab(tabName) {
    document.querySelectorAll("[data-tab-panel]").forEach((panel) => {
        if (!(panel instanceof HTMLElement)) {
            return;
        }
        panel.classList.toggle("active", panel.dataset.tabPanel === tabName);
    });
}

export function syncTabQuery(tabName) {
    if (!tabName) {
        return;
    }
    const url = new URL(window.location.href);
    url.searchParams.set("tab", tabName);
    window.history.replaceState({}, "", url);
}
