/**
 * schedule/filters.js — schedule + gantt filter panel open/close + state.
 *
 * Fáze 3B Task 2: vyextrahováno ze schedule.js (1697 LOC).
 * Obsahuje: setScheduleFilterPanelOpen, restoreScheduleFilterState,
 *           applyProjectScheduleFilters, getScheduleFilterValue,
 *           setGanttFilterPanelOpen, restoreGanttFilterState, getGanttFilterValue.
 *
 * Poznámka: persistScheduleFilterState + persistGanttFilterState jsou v index.js,
 * protože volají applyProjectGanttFilters (z gantt.js) — přesun sem by způsobil
 * cirkulární závislost filters.js → gantt.js → filters.js.
 */

import {
    normalizeFilterToken
} from "../utils.js";
import {
    buildProjectFilterStateFromInputs,
    getProjectFilterInput,
    normalizeSubsystemSortMode,
    restoreProjectFilterScope,
    scheduleSubsystemIndicatorSync,
    setRecordFilterVisibility,
    sortSubsystemGroupsInContainer
} from "../filters.js";
import { queueRainbowSegmentRender } from "../ui.js";
import { renderStaticTimelineAxes } from "./timeline.js";

export function setScheduleFilterPanelOpen(open) {
    const panel = document.querySelector("[data-schedule-filter-panel]");
    const toggle = document.querySelector("[data-schedule-filter-toggle]");
    const key = "pmtracker.schedule.filters.open";
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    panel.classList.toggle("collapsed", !open);
    if (toggle instanceof HTMLElement) {
        toggle.setAttribute("aria-expanded", String(open));
    }

    localStorage.setItem(key, String(open));
}

export function getScheduleFilterValue(key) {
    const input = getProjectFilterInput("schedule", key);
    if (input instanceof HTMLInputElement && input.type === "checkbox") {
        return input.checked;
    }
    if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
        return input.value;
    }
    return "";
}

export function restoreScheduleFilterState() {
    return restoreProjectFilterScope("schedule");
}

export function applyProjectScheduleFilters() {
    const cards = document.querySelectorAll("[data-schedule-item]");
    if (cards.length === 0) {
        return;
    }

    const state = buildProjectFilterStateFromInputs("schedule");
    const filters = {
        subsystem: normalizeFilterToken(state.subsystem)
    };

    cards.forEach((item) => {
        if (!(item instanceof HTMLElement)) {
            return;
        }

        const subsystem = normalizeFilterToken(item.dataset.scheduleFilterSubsystemKod || item.dataset.scheduleFilterSubsystem);
        const matches = !filters.subsystem || subsystem === filters.subsystem;

        setRecordFilterVisibility(item, matches);
    });

    document.querySelectorAll("[data-project-schedule-list] [data-subsystem-group]").forEach((group) => {
        if (!(group instanceof HTMLElement)) {
            return;
        }

        const hasVisibleItems = Array.from(group.querySelectorAll("[data-schedule-item]"))
            .some((item) => item instanceof HTMLElement && !item.hidden);
        group.hidden = !hasVisibleItems;
    });

    const scheduleList = document.querySelector("[data-project-schedule-list]");
    if (scheduleList instanceof HTMLElement) {
        const sortMode = normalizeSubsystemSortMode(state.sortBy);
        sortSubsystemGroupsInContainer(scheduleList, sortMode);
    }

    renderStaticTimelineAxes(document.querySelector('[data-tab-panel="harmonogram"]'));
    queueRainbowSegmentRender(document.querySelector('[data-tab-panel="harmonogram"]'));
    scheduleSubsystemIndicatorSync();
}

export function setGanttFilterPanelOpen(open) {
    const panel = document.querySelector("[data-gantt-filter-panel]");
    const toggle = document.querySelector("[data-gantt-filter-toggle]");
    const key = "pmtracker.gantt.filters.open";
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    panel.classList.toggle("collapsed", !open);
    if (toggle instanceof HTMLElement) {
        toggle.setAttribute("aria-expanded", String(open));
    }

    localStorage.setItem(key, String(open));
}

export function restoreGanttFilterState() {
    return restoreProjectFilterScope("gantt");
}
