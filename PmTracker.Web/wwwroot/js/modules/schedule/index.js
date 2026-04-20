/**
 * schedule/index.js — entry point for schedule feature module.
 *
 * Fáze 3B Task 2: orchestrator + re-exports všech submodulů.
 * Obsahuje: initRecordSchedulePlanner, initProjectScheduleUi,
 *           queueRecordSchedulePlannerRecalc, toggleScheduleBreakdown,
 *           initScheduleExpandUi, persistScheduleFilterState, persistGanttFilterState.
 *
 * persistScheduleFilterState + persistGanttFilterState jsou zde (nikoliv v filters.js)
 * protože volají applyProjectGanttFilters — přesun do filters.js by způsobil
 * cirkulární závislost filters.js → gantt.js → filters.js.
 */

import { isButtonLike } from "../utils.js";
import {
    handleProjectFilterInputChange,
    setProjectFilterSaveStatus
} from "../filters.js";
import { queueRainbowSegmentRender } from "../ui.js";
import {
    applyProjectScheduleFilters,
    restoreScheduleFilterState,
    setScheduleFilterPanelOpen
} from "./filters.js";
import { applyProjectGanttFilters } from "./gantt.js";
import { renderStaticTimelineAxes } from "./timeline.js";
import { ScheduleBlockRenderer } from "./block.js";

export * from "./filters.js";
export * from "./gantt.js";
export * from "./timeline.js";
export * from "./block.js";

export function persistScheduleFilterState() {
    handleProjectFilterInputChange("schedule", {
        applyScope: (resolvedScope) => {
            if (resolvedScope === "schedule") {
                applyProjectScheduleFilters();
            }
            else if (resolvedScope === "gantt") {
                applyProjectGanttFilters();
            }
        }
    });
}

export function persistGanttFilterState() {
    handleProjectFilterInputChange("gantt", {
        applyScope: (resolvedScope) => {
            if (resolvedScope === "schedule") {
                applyProjectScheduleFilters();
            }
            else if (resolvedScope === "gantt") {
                applyProjectGanttFilters();
            }
        }
    });
}

function syncScheduleExpandButton(button, details) {
    if (!isButtonLike(button) || !(details instanceof HTMLElement)) {
        return;
    }

    const expanded = !details.hidden;
    button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
    button.setAttribute("aria-expanded", String(expanded));
}

export function toggleScheduleBreakdown(toggleOrTarget) {
    const button = isButtonLike(toggleOrTarget)
        ? toggleOrTarget
        : toggleOrTarget instanceof Element
            ? toggleOrTarget.closest("[data-schedule-expand-toggle]")
            : null;
    if (!isButtonLike(button)) {
        return false;
    }

    const owningCard = button.closest("[data-schedule-item]");
    if (!(owningCard instanceof HTMLElement)) {
        return false;
    }

    const details = owningCard.querySelector("[data-schedule-steps]");
    if (!(details instanceof HTMLElement)) {
        return false;
    }

    const expanded = details.hidden;
    details.hidden = !expanded;
    syncScheduleExpandButton(button, details);

    if (expanded) {
        renderStaticTimelineAxes(details);
        queueRainbowSegmentRender(details);
        window.requestAnimationFrame(() => {
            renderStaticTimelineAxes(details);
            queueRainbowSegmentRender(details);
        });
    }

    return true;
}

export function initScheduleExpandUi(scope) {
    const root = scope instanceof Element ? scope : document;
    root.querySelectorAll("[data-schedule-expand-toggle]").forEach((button) => {
        if (!isButtonLike(button)) {
            return;
        }

        const card = button.closest("[data-schedule-item]");
        const details = card instanceof HTMLElement
            ? card.querySelector("[data-schedule-steps]")
            : null;
        if (details instanceof HTMLElement) {
            syncScheduleExpandButton(button, details);
        }
    });
}

export function queueRecordSchedulePlannerRecalc(form, attempt) {
    if (!(form instanceof HTMLFormElement) || !form.isConnected) {
        return;
    }

    const retryAttempt = Number.isFinite(attempt) ? Math.max(0, Math.trunc(attempt)) : 0;
    const schedulePanel = form.querySelector('[data-record-modal-panel="schedule"]');
    if (schedulePanel instanceof HTMLElement && schedulePanel.hidden) {
        return;
    }

    const planner = form._recordSchedulePlanner;
    if (planner && typeof planner.recalcAll === "function") {
        planner.recalcAll();
        return;
    }

    if (retryAttempt >= 6) {
        return;
    }

    window.requestAnimationFrame(() => {
        queueRecordSchedulePlannerRecalc(form, retryAttempt + 1);
    });
}

function initScheduleBlockRenderers(scope) {
    const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
    const blocks = [];

    if (scope instanceof HTMLElement && scope.matches("[data-schedule-block]")) {
        blocks.push(scope);
    }

    root.querySelectorAll("[data-schedule-block]").forEach((block) => {
        if (block instanceof HTMLElement) {
            blocks.push(block);
        }
    });

    blocks.forEach((block) => {
        if (!(block instanceof HTMLElement)) {
            return;
        }

        if (block._scheduleRenderer instanceof ScheduleBlockRenderer) {
            block._scheduleRenderer.recalcAll();
            return;
        }

        const form = block.closest('form[data-record-editor-form="true"]');
        const renderer = new ScheduleBlockRenderer(block, { form });
        if (!renderer.isReady()) {
            return;
        }

        renderer.bind();
        renderer.recalcAll();
        block._scheduleRenderer = renderer;

        if (form instanceof HTMLFormElement && renderer.mode === "record-editor") {
            form._recordSchedulePlanner = renderer;
            form.dataset.recordScheduleReady = "true";
        }
    });
}

export function initRecordSchedulePlanner(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    const forms = [];
    if (scope instanceof HTMLFormElement && scope.matches('form[data-record-schedule-form="true"]')) {
        forms.push(scope);
    }

    scope.querySelectorAll('form[data-record-schedule-form="true"]').forEach((form) => {
        if (form instanceof HTMLFormElement) {
            forms.push(form);
        }
    });

    forms.forEach((form) => {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const editor = form.querySelector('[data-schedule-block][data-schedule-mode="record-editor"]');
        const schedulePanel = form.querySelector("[data-record-schedule-panel]");
        if (!(editor instanceof HTMLElement)
            || (schedulePanel instanceof HTMLElement && schedulePanel.dataset.scheduleDisabled === "true")) {
            return;
        }

        initScheduleBlockRenderers(editor);
        queueRecordSchedulePlannerRecalc(form, 0);
    });
}

export function initProjectScheduleUi() {
    const schedulePanel = document.querySelector('[data-tab-panel="harmonogram"]');
    if (!(schedulePanel instanceof HTMLElement)) {
        return;
    }

    const filterPanel = document.querySelector("[data-schedule-filter-panel]");
    if (filterPanel instanceof HTMLElement) {
        const storedOpen = localStorage.getItem("pmtracker.schedule.filters.open");
        setScheduleFilterPanelOpen(storedOpen === "true");
    }

    restoreScheduleFilterState();
    setProjectFilterSaveStatus("schedule", "");
    initScheduleBlockRenderers(schedulePanel);
    applyProjectScheduleFilters();
    renderStaticTimelineAxes(schedulePanel);
    queueRainbowSegmentRender(schedulePanel);
    initScheduleExpandUi(schedulePanel);
}
