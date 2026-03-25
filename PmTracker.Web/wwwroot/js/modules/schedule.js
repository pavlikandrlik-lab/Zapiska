import {
    addCalendarDays,
    diffCalendarDays,
    formatAxisDayMonth,
    formatAxisMonthYear,
    formatDisplayDate,
    formatIsoDate,
    measureTextWidth,
    msPerDay,
    normalizeFilterToken,
    parseIsoDate,
    toUtcDayStamp
} from "./utils.js";
import {
    getProjectFilterCurrentUserId,
    getProjectFilterInput,
    handleProjectFilterInputChange,
    restoreProjectFilterScope,
    scheduleSubsystemIndicatorSync,
    setProjectFilterSaveStatus,
    setRecordFilterVisibility
} from "./filters.js";
import { queueRainbowSegmentRender } from "./ui.js";

export function initScheduleExpandUi(scope) {
    const root = scope instanceof Element ? scope : document;
    root.querySelectorAll("[data-schedule-expand-toggle]").forEach((button) => {
        if (!(button instanceof HTMLButtonElement) || button.dataset.scheduleExpandReady === "true") {
            return;
        }

        const card = button.closest("[data-schedule-item]");
        const details = card instanceof HTMLElement
            ? card.querySelector("[data-schedule-steps]")
            : null;
        if (details instanceof HTMLElement) {
            button.setAttribute("aria-expanded", String(!details.hidden));
        }

        button.dataset.scheduleExpandReady = "true";
        button.addEventListener("click", () => {
            const owningCard = button.closest("[data-schedule-item]");
            if (!(owningCard instanceof HTMLElement)) {
                return;
            }

            const details = owningCard.querySelector("[data-schedule-steps]");
            if (!(details instanceof HTMLElement)) {
                return;
            }

            const expanded = details.hidden;
            details.hidden = !expanded;
            button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
            button.setAttribute("aria-expanded", String(expanded));

            if (expanded) {
                renderStaticTimelineAxes(details);
                queueRainbowSegmentRender(details);
                window.requestAnimationFrame(() => {
                    renderStaticTimelineAxes(details);
                    queueRainbowSegmentRender(details);
                });
            }
        });
    });
}

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

export function getGanttFilterValue(key) {
    const input = getProjectFilterInput("gantt", key);
    if (input instanceof HTMLInputElement && input.type === "checkbox") {
        return input.checked;
    }
    if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
        return input.value;
    }
    return "";
}

export function restoreGanttFilterState() {
    return restoreProjectFilterScope("gantt");
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

export class ProjectGanttBoard {
    constructor(panel) {
        this.panel = panel;
        this.projectId = String(panel.dataset.projectId || "0");
        this.pickerItems = Array.from(panel.querySelectorAll("[data-gantt-picker-item]"))
            .filter((node) => node instanceof HTMLElement);
        this.boardItems = Array.from(panel.querySelectorAll("[data-gantt-item]"))
            .filter((node) => node instanceof HTMLElement);
        this.pinInputs = Array.from(panel.querySelectorAll("[data-gantt-pin-input]"))
            .filter((node) => node instanceof HTMLInputElement);
        this.pinnedKey = `pmtracker.gantt.pinned.${this.projectId}`;
        this.expandedKey = `pmtracker.gantt.expanded.${this.projectId}`;

        const fallbackPinned = this.pinInputs
            .map((input) => String(input.dataset.ganttRecordId || "").trim())
            .filter(Boolean);
        this.pinnedIds = this.readIdSet(this.pinnedKey, fallbackPinned);
        this.expandedIds = this.readIdSet(this.expandedKey, []);
    }

    readIdSet(storageKey, fallbackValues) {
        const raw = localStorage.getItem(storageKey);
        if (!raw) {
            return new Set(fallbackValues);
        }
        try {
            const parsed = JSON.parse(raw);
            if (!Array.isArray(parsed)) {
                return new Set(fallbackValues);
            }
            return new Set(parsed.map((value) => String(value || "").trim()).filter(Boolean));
        } catch (error) {
            return new Set(fallbackValues);
        }
    }

    writeIdSet(storageKey, set) {
        localStorage.setItem(storageKey, JSON.stringify(Array.from(set)));
    }

    getFilters() {
        const currentUserId = getProjectFilterCurrentUserId("gantt");
        const hasCurrentUser = currentUserId && currentUserId !== "0";
        return {
            subsystem: normalizeFilterToken(getGanttFilterValue("subsystem")),
            kategorie: normalizeFilterToken(getGanttFilterValue("kategorie")),
            stav: normalizeFilterToken(getGanttFilterValue("stav")),
            typ: normalizeFilterToken(getGanttFilterValue("typ")),
            vlastnik: normalizeFilterToken(getGanttFilterValue("vlastnik")),
            onlyActive: Boolean(getGanttFilterValue("aktivni")),
            mine: Boolean(getGanttFilterValue("mine")),
            currentUserId,
            hasCurrentUser,
            stihani: normalizeFilterToken(getGanttFilterValue("stihani"))
        };
    }

    matchesFilters(node, filters) {
        if (!(node instanceof HTMLElement)) {
            return false;
        }

        const subsystem = normalizeFilterToken(node.dataset.ganttFilterSubsystemKod || node.dataset.ganttFilterSubsystem);
        const kategorie = normalizeFilterToken(node.dataset.ganttFilterKategorieKod || node.dataset.ganttFilterKategorie);
        const stav = normalizeFilterToken(node.dataset.ganttFilterStavKod || node.dataset.ganttFilterStav);
        const typ = normalizeFilterToken(node.dataset.ganttFilterTypKod || node.dataset.ganttFilterTyp);
        const vlastnik = normalizeFilterToken(node.dataset.ganttFilterVlastnikId || node.dataset.ganttFilterVlastnik);
        const isActive = node.dataset.ganttFilterAktivni === "true";
        const stihani = normalizeFilterToken(node.dataset.ganttFilterStihani);
        const matchesMine = !filters.mine || (filters.hasCurrentUser && vlastnik === filters.currentUserId);

        return (!filters.subsystem || subsystem === filters.subsystem)
            && (!filters.kategorie || kategorie === filters.kategorie)
            && (!filters.stav || stav === filters.stav)
            && (!filters.typ || typ === filters.typ)
            && (!filters.vlastnik || vlastnik === filters.vlastnik)
            && (!filters.onlyActive || isActive)
            && matchesMine
            && (!filters.stihani || stihani === filters.stihani);
    }

    setExpanded(recordId, expanded) {
        const normalized = String(recordId || "").trim();
        if (!normalized) {
            return;
        }

        if (expanded) {
            this.expandedIds.add(normalized);
        } else {
            this.expandedIds.delete(normalized);
        }
        this.writeIdSet(this.expandedKey, this.expandedIds);
        this.syncExpandedState();
    }

    setPinned(recordId, pinned) {
        const normalized = String(recordId || "").trim();
        if (!normalized) {
            return;
        }

        if (pinned) {
            this.pinnedIds.add(normalized);
        } else {
            this.pinnedIds.delete(normalized);
            this.expandedIds.delete(normalized);
            this.writeIdSet(this.expandedKey, this.expandedIds);
        }

        this.writeIdSet(this.pinnedKey, this.pinnedIds);
        this.apply();
    }

    syncPinnedInputs() {
        this.pinInputs.forEach((input) => {
            const recordId = String(input.dataset.ganttRecordId || "").trim();
            input.checked = this.pinnedIds.has(recordId);
        });
    }

    syncExpandedState() {
        this.boardItems.forEach((item) => {
            if (!(item instanceof HTMLElement)) {
                return;
            }

            const recordId = String(item.dataset.ganttRecordId || "").trim();
            const expanded = this.expandedIds.has(recordId);
            const details = item.querySelector("[data-gantt-steps]");
            if (details instanceof HTMLElement) {
                details.hidden = !expanded;
            }
            const button = item.querySelector("[data-gantt-expand-toggle]");
            if (button instanceof HTMLButtonElement) {
                button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
                button.setAttribute("aria-expanded", String(expanded));
            }
        });
    }

    apply() {
        const filters = this.getFilters();

        this.pickerItems.forEach((item) => {
            const visible = this.matchesFilters(item, filters);
            setRecordFilterVisibility(item, visible);
        });

        this.boardItems.forEach((item) => {
            if (!(item instanceof HTMLElement)) {
                return;
            }
            const recordId = String(item.dataset.ganttRecordId || "").trim();
            const visibleByFilter = this.matchesFilters(item, filters);
            const visible = visibleByFilter && this.pinnedIds.has(recordId);
            setRecordFilterVisibility(item, visible);
            item.dataset.ganttPinned = visible ? "true" : "false";
        });

        this.syncPinnedInputs();
        this.syncExpandedState();
        updateProjectGanttAxis(this.panel);
        queueRainbowSegmentRender(this.panel);
    }

    bind() {
        this.pinInputs.forEach((input) => {
            input.addEventListener("change", () => {
                this.setPinned(input.dataset.ganttRecordId, input.checked);
            });
        });

        this.panel.querySelectorAll("[data-gantt-expand-toggle]").forEach((button) => {
            if (!(button instanceof HTMLButtonElement)) {
                return;
            }
            button.addEventListener("click", () => {
                const recordId = String(button.dataset.ganttRecordId || "").trim();
                const expanded = this.expandedIds.has(recordId);
                this.setExpanded(recordId, !expanded);
            });
        });
    }
}

export function applyProjectGanttFilters() {
    const panel = document.querySelector("[data-gantt-panel]");
    if (!(panel instanceof HTMLElement) || !(panel._ganttBoard instanceof ProjectGanttBoard)) {
        return;
    }
    panel._ganttBoard.apply();
}

export function updateProjectGanttAxis(panel) {
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const axis = panel.querySelector("[data-gantt-axis]");
    if (!(axis instanceof HTMLElement)) {
        return;
    }

    const visibleItems = Array.from(panel.querySelectorAll("[data-gantt-item]"))
        .filter((node) => node instanceof HTMLElement && !node.hidden);
    if (visibleItems.length === 0) {
        axis.hidden = true;
        axis.replaceChildren();
        return;
    }

    const dates = visibleItems
        .flatMap((item) => {
            const startDate = parseIsoDate(item.dataset.ganttAxisStart);
            const endDate = parseIsoDate(item.dataset.ganttAxisEnd);
            return [startDate, endDate];
        })
        .filter((value) => value instanceof Date);
    if (dates.length === 0) {
        axis.hidden = true;
        axis.replaceChildren();
        return;
    }

    const ordered = dates.slice().sort((a, b) => a.getTime() - b.getTime());
    axis.hidden = false;
    renderTimelineAxis(axis, ordered[0], ordered[ordered.length - 1]);
}

export function resolveTimelineAxisTickTargetCount(containerWidth) {
    if (!Number.isFinite(containerWidth) || containerWidth <= 0) {
        return 2;
    }

    if (containerWidth < 320) {
        return 2;
    }

    const estimated = Math.round(containerWidth / 120);
    return Math.max(5, Math.min(10, estimated));
}

export function queueTimelineAxisRetry(container, startDate, endDate, attempt) {
    if (!(container instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
        return;
    }

    const retryAttempt = Number.isFinite(attempt) ? Math.trunc(attempt) : 0;
    if (retryAttempt >= 10 || !container.isConnected) {
        return;
    }

    const pendingFrame = Number.parseInt(container.dataset.axisRetryFrame || "0", 10);
    if (Number.isInteger(pendingFrame) && pendingFrame > 0) {
        window.cancelAnimationFrame(pendingFrame);
    }

    const frameId = window.requestAnimationFrame(() => {
        container.dataset.axisRetryFrame = "0";
        renderTimelineAxis(container, startDate, endDate, {
            retryAttempt: retryAttempt + 1
        });
    });
    container.dataset.axisRetryFrame = String(frameId);
}

export function buildTimelineAxisTicks(startDate, endDate, desiredTickCount) {
    const start = new Date(startDate.getFullYear(), startDate.getMonth(), startDate.getDate());
    const end = new Date(endDate.getFullYear(), endDate.getMonth(), endDate.getDate());
    const totalDays = Math.max(1, diffCalendarDays(end, start));
    const maxDistinctTicks = totalDays + 1;
    const requestedTicks = Number.isFinite(desiredTickCount) ? Math.trunc(desiredTickCount) : 7;
    const tickCount = Math.max(2, Math.min(maxDistinctTicks, requestedTicks));
    const useMonthYearLabels = totalDays > 120;
    const formatTickLabel = useMonthYearLabels ? formatAxisMonthYear : formatAxisDayMonth;
    const selectedOffsets = new Set([0, totalDays]);

    for (let index = 1; index < tickCount - 1; index += 1) {
        const offset = Math.round((index * totalDays) / (tickCount - 1));
        selectedOffsets.add(Math.max(0, Math.min(totalDays, offset)));
    }

    for (let dayOffset = 1; selectedOffsets.size < tickCount && dayOffset < totalDays; dayOffset += 1) {
        selectedOffsets.add(dayOffset);
    }

    const orderedOffsets = Array.from(selectedOffsets)
        .map((value) => Number.parseInt(String(value), 10))
        .filter((value) => Number.isFinite(value))
        .sort((a, b) => a - b);

    return orderedOffsets.map((dayOffset) => {
        const date = addCalendarDays(start, dayOffset);
        return {
            date,
            label: formatTickLabel(date),
            left: (dayOffset * 100) / totalDays
        };
    });
}

export function renderTimelineAxis(container, startDate, endDate, options) {
    if (!(container instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
        return;
    }

    const settings = options && typeof options === "object" ? options : {};
    const retryAttempt = Number.isFinite(settings.retryAttempt)
        ? Math.max(0, Math.trunc(settings.retryAttempt))
        : 0;
    const containerWidth = Math.max(0, container.clientWidth);
    if (containerWidth <= 0 || (containerWidth <= 32 && retryAttempt < 10)) {
        queueTimelineAxisRetry(container, startDate, endDate, retryAttempt);
        return;
    }

    const startStamp = toUtcDayStamp(startDate);
    const endStamp = toUtcDayStamp(endDate);
    const axisStart = startStamp <= endStamp ? startDate : endDate;
    const axisEnd = startStamp <= endStamp ? endDate : startDate;
    const totalDays = Math.max(1, diffCalendarDays(axisEnd, axisStart));
    const edgeInsetPx = Math.max(2, Math.min(4, Math.round(containerWidth * 0.006)));
    const usableAxisWidth = Math.max(1, containerWidth - (edgeInsetPx * 2));
    const labelGlobalShiftLeftPx = 14;
    const percentToAxisPx = (percentValue) => {
        const normalized = Math.max(0, Math.min(100, Number.isFinite(percentValue) ? percentValue : 0));
        return edgeInsetPx + ((normalized / 100) * usableAxisWidth);
    };

    const pendingFrame = Number.parseInt(container.dataset.axisRetryFrame || "0", 10);
    if (Number.isInteger(pendingFrame) && pendingFrame > 0) {
        window.cancelAnimationFrame(pendingFrame);
    }
    container.dataset.axisRetryFrame = "0";
    container.replaceChildren();
    const desiredTickCount = resolveTimelineAxisTickTargetCount(containerWidth);
    const ticks = buildTimelineAxisTicks(axisStart, axisEnd, desiredTickCount);
    ticks.forEach((tick, index) => {
        const tickNode = document.createElement("span");
        tickNode.className = "timeline-axis-tick";
        if (index === 0 || index === ticks.length - 1) {
            tickNode.classList.add("edge");
        }
        const tickLeftPx = percentToAxisPx(tick.left);
        tickNode.dataset.axisLeftPx = tickLeftPx.toFixed(4);
        tickNode.style.left = `${tickLeftPx.toFixed(4)}px`;

        const labelNode = document.createElement("span");
        labelNode.className = "timeline-axis-label";
        labelNode.textContent = tick.label;
        tickNode.appendChild(labelNode);
        container.appendChild(tickNode);
    });

    if (ticks.length === 0) {
        return;
    }

    let previousLabelRight = -Infinity;
    const minLabelGap = 6;
    const tickNodes = Array.from(container.querySelectorAll(".timeline-axis-tick"))
        .filter((tickNode) => tickNode instanceof HTMLElement);
    const lastIndex = tickNodes.length - 1;
    const resolveLabelWidth = (labelNode) => {
        if (!(labelNode instanceof HTMLElement)) {
            return 0;
        }
        const measuredLabelWidth = labelNode.offsetWidth;
        const computedStyle = window.getComputedStyle(labelNode);
        const fallbackFontSpec = `${computedStyle.fontWeight} ${computedStyle.fontSize} ${computedStyle.fontFamily}`;
        const fallbackLabelWidth = Math.ceil(measureTextWidth(labelNode.textContent || "", fallbackFontSpec));
        return measuredLabelWidth > 0 ? measuredLabelWidth : fallbackLabelWidth;
    };
    const resolveTickLeftPx = (tickNode) => {
        if (!(tickNode instanceof HTMLElement)) {
            return 0;
        }
        const serializedPx = Number.parseFloat(tickNode.dataset.axisLeftPx || "");
        if (Number.isFinite(serializedPx)) {
            return serializedPx;
        }
        const measuredLeft = Number.parseFloat(tickNode.style.left || "0");
        return Number.isFinite(measuredLeft) ? measuredLeft : 0;
    };
    const placeLabel = (tickNode, labelNode, index, forceVisible) => {
        const tickLeftPx = resolveTickLeftPx(tickNode);
        const labelWidthRaw = resolveLabelWidth(labelNode);
        const labelWidth = Math.max(1, Math.min(containerWidth, labelWidthRaw > 0 ? labelWidthRaw : 1));
        labelNode.style.maxWidth = `${Math.max(1, containerWidth)}px`;

        const sidePadding = 12;
        let desiredLeft = tickLeftPx + sidePadding;
        if (index === lastIndex) {
            desiredLeft = tickLeftPx - labelWidth - sidePadding;
        } else if (index > 0) {
            desiredLeft = tickLeftPx - (labelWidth / 2);
        }

        desiredLeft -= labelGlobalShiftLeftPx;

        const clampedLeft = Math.max(0, Math.min(desiredLeft, Math.max(0, containerWidth - labelWidth)));
        if (!forceVisible && clampedLeft < previousLabelRight + minLabelGap) {
            labelNode.hidden = true;
            return;
        }

        labelNode.hidden = false;
        labelNode.style.left = `${Math.round(clampedLeft - tickLeftPx)}px`;
        previousLabelRight = Math.max(previousLabelRight, clampedLeft + labelWidth);
    };

    tickNodes.forEach((tickNode, index) => {
        if (!(tickNode instanceof HTMLElement)) {
            return;
        }

        const labelNode = tickNode.querySelector(".timeline-axis-label");
        if (!(labelNode instanceof HTMLElement)) {
            return;
        }

        labelNode.hidden = false;
        labelNode.style.left = "4px";
        const forceVisible = index === 0 || index === lastIndex;
        placeLabel(tickNode, labelNode, index, forceVisible);
    });

    const resolveTickLabelNode = (tickNode) => tickNode instanceof HTMLElement
        ? tickNode.querySelector(".timeline-axis-label")
        : null;
    const visibleLabelNodes = tickNodes
        .map(resolveTickLabelNode)
        .filter((labelNode) => labelNode instanceof HTMLElement && !labelNode.hidden && String(labelNode.textContent || "").trim());

    const forceLabelVisible = (tickNode, alignEnd) => {
        if (!(tickNode instanceof HTMLElement)) {
            return;
        }

        const labelNode = tickNode.querySelector(".timeline-axis-label");
        if (!(labelNode instanceof HTMLElement)) {
            return;
        }

        const tickLeftPx = resolveTickLeftPx(tickNode);
        const labelWidthRaw = resolveLabelWidth(labelNode);
        const labelWidth = Math.max(1, Math.min(containerWidth, labelWidthRaw > 0 ? labelWidthRaw : 1));
        labelNode.style.maxWidth = `${Math.max(1, containerWidth)}px`;
        const desiredLeft = alignEnd
            ? Math.max(0, containerWidth - labelWidth - edgeInsetPx)
            : edgeInsetPx;
        const shiftedDesiredLeft = desiredLeft - labelGlobalShiftLeftPx;
        const clampedLeft = Math.max(0, Math.min(shiftedDesiredLeft, Math.max(0, containerWidth - labelWidth)));
        labelNode.hidden = false;
        labelNode.style.left = `${Math.round(clampedLeft - tickLeftPx)}px`;
    };

    if (visibleLabelNodes.length < 2 && tickNodes.length >= 2) {
        forceLabelVisible(tickNodes[0], false);
        forceLabelVisible(tickNodes[lastIndex], true);
    } else if (visibleLabelNodes.length === 0 && tickNodes.length === 1) {
        forceLabelVisible(tickNodes[0], false);
    }

}

export function renderStaticTimelineAxes(scope) {
    const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
    root.querySelectorAll("[data-timeline-axis][data-axis-start][data-axis-end]").forEach((container) => {
        if (!(container instanceof HTMLElement)) {
            return;
        }

        const startDate = parseIsoDate(container.dataset.axisStart);
        const endDate = parseIsoDate(container.dataset.axisEnd);
        if (!(startDate instanceof Date) || !(endDate instanceof Date)) {
            return;
        }

        renderTimelineAxis(container, startDate, endDate);
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

export class ScheduleTimelineEngine {
    static computePlanAndActual(state, startDate) {
        const plan = [];
        const actual = [];
        let planCursor = new Date(startDate.getTime());
        let actualCursor = new Date(startDate.getTime());

        state.forEach((item) => {
            const planStart = new Date(planCursor.getTime());
            const planEnd = addCalendarDays(planStart, item.duration);
            plan.push({ start: planStart, end: planEnd });
            planCursor = new Date(planEnd.getTime());

            const actualStart = new Date(actualCursor.getTime());
            const actualEnd = addCalendarDays(actualStart, Math.max(0, item.duration + item.delay));
            actual.push({ start: actualStart, end: actualEnd });
            actualCursor = new Date(actualEnd.getTime());
        });

        return { plan, actual };
    }

    static buildScale(startDate, deadlineDate, actualEndDate) {
        const startStamp = toUtcDayStamp(startDate);
        const axisEndStamp = Math.max(
            startStamp,
            toUtcDayStamp(deadlineDate),
            toUtcDayStamp(actualEndDate),
            toUtcDayStamp(new Date()));
        const totalDays = Math.max(1, Math.round((axisEndStamp - startStamp) / msPerDay));
        return {
            totalDays,
            axisEndDate: addCalendarDays(startDate, totalDays)
        };
    }

    static toPercent(valueDate, axisStart, totalDays) {
        const days = diffCalendarDays(valueDate, axisStart);
        return Math.max(0, Math.min(100, (days * 100) / totalDays));
    }

    static toWidthPercent(startDate, endDate, totalDays) {
        const days = Math.max(0, diffCalendarDays(endDate, startDate));
        return Math.max(0, Math.min(100, (days * 100) / totalDays));
    }
}

export class RecordSchedulePlanner {
    constructor(form, editor) {
        this.form = form;
        this.editor = editor;
        this.startInput = form.querySelector('input[name="DatumZalozeni"]');
        this.deadlineInput = form.querySelector('input[name="TerminUkonceni"]');
        this.summaryDeadline = editor.querySelector("[data-schedule-summary-deadline]");
        this.summaryBaseline = editor.querySelector("[data-schedule-summary-baseline]");
        this.summaryShifted = editor.querySelector("[data-schedule-summary-shifted]");
        this.summaryDuration = editor.querySelector("[data-schedule-summary-duration]");
        this.summaryDelay = editor.querySelector("[data-schedule-summary-delay]");
        this.summaryState = editor.querySelector("[data-schedule-summary-state]");
        this.summaryOverrun = editor.querySelector("[data-schedule-summary-overrun]");
        this.statusLine = editor.querySelector(".schedule-status-line");
        this.timelineAxes = Array.from(editor.querySelectorAll("[data-schedule-axis]"))
            .filter((node) => node instanceof HTMLElement);
        this.ganttTodayMarkers = Array.from(editor.querySelectorAll("[data-schedule-gantt-today]"))
            .filter((node) => node instanceof HTMLElement);
        this.ganttDeadlineMarkers = Array.from(editor.querySelectorAll("[data-schedule-gantt-deadline]"))
            .filter((node) => node instanceof HTMLElement);
        this.ganttPlannedSegments = Array.from(editor.querySelectorAll("[data-schedule-gantt-step-planned]"))
            .filter((node) => node instanceof HTMLElement);
        this.ganttActualSegments = Array.from(editor.querySelectorAll("[data-schedule-gantt-step-actual]"))
            .filter((node) => node instanceof HTMLElement);
        this.rows = Array.from(editor.querySelectorAll("[data-schedule-step-row]"))
            .filter((row) => row instanceof HTMLTableRowElement)
            .map((row, index) => ({
                row,
                index,
                durationInput: row.querySelector("[data-schedule-duration]"),
                delayInput: row.querySelector("[data-schedule-delay]"),
                dateInput: row.querySelector("[data-schedule-date]"),
                delayDateInput: row.querySelector("[data-schedule-delay-date]"),
                baselineCell: row.querySelector("[data-schedule-baseline]"),
                shiftedCell: row.querySelector("[data-schedule-shifted]"),
                durationInc: row.querySelector("[data-schedule-duration-inc]"),
                durationDec: row.querySelector("[data-schedule-duration-dec]"),
                delayInc: row.querySelector("[data-schedule-delay-inc]"),
                delayDec: row.querySelector("[data-schedule-delay-dec]")
            }));
    }

    isReady() {
        return this.form instanceof HTMLFormElement
            && this.editor instanceof HTMLElement
            && this.startInput instanceof HTMLInputElement
            && this.rows.length > 0;
    }

    readState() {
        return this.rows.map((entry) => {
            const duration = this.normalizeInt(entry.durationInput);
            const delay = this.normalizeSignedInt(entry.delayInput);
            return { duration, delay };
        });
    }

    writeState(state) {
        state.forEach((item, index) => {
            const entry = this.rows[index];
            if (!entry) {
                return;
            }
            if (entry.durationInput instanceof HTMLInputElement) {
                entry.durationInput.value = String(item.duration);
            }
            if (entry.delayInput instanceof HTMLInputElement) {
                entry.delayInput.value = String(item.delay);
            }
        });
    }

    getStartDate() {
        if (!(this.startInput instanceof HTMLInputElement)) {
            return new Date();
        }

        return parseIsoDate(this.startInput.value) || new Date();
    }

    getDeadlineDate(startDate) {
        if (!(this.deadlineInput instanceof HTMLInputElement)) {
            return startDate;
        }

        return parseIsoDate(this.deadlineInput.value) || startDate;
    }

    normalizeInt(input) {
        return this.normalizeIntWithMinimum(input, 0);
    }

    normalizeSignedInt(input) {
        if (!(input instanceof HTMLInputElement)) {
            return 0;
        }
        const parsed = Number.parseInt((input.value || "").trim(), 10);
        if (!Number.isFinite(parsed)) {
            return 0;
        }
        return parsed;
    }

    normalizeIntWithMinimum(input, minimum) {
        if (!(input instanceof HTMLInputElement)) {
            return Math.max(minimum, 0);
        }
        const parsed = Number.parseInt((input.value || "").trim(), 10);
        if (!Number.isFinite(parsed)) {
            return Math.max(minimum, 0);
        }
        return Math.max(minimum, parsed);
    }

    setDateInputValue(input, value) {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const isoValue = formatIsoDate(value);
        input.value = isoValue;
        const dateField = input.closest("[data-app-date-field]");
        if (!(dateField instanceof HTMLElement)) {
            return;
        }

        const display = dateField.querySelector("[data-app-date-display]");
        if (display instanceof HTMLInputElement) {
            display.value = formatDisplayDate(value);
        }
    }

    computePlanAndActual(state, startDate) {
        return ScheduleTimelineEngine.computePlanAndActual(state, startDate);
    }

    recalcFromDuration(stepIndex) {
        if (!Number.isInteger(stepIndex) || stepIndex < 0) {
            this.recalcAll();
            return;
        }

        this.recalcAll();
    }

    recalcFromDelay(stepIndex) {
        if (!Number.isInteger(stepIndex) || stepIndex < 0) {
            this.recalcAll();
            return;
        }

        this.recalcAll();
    }

    recalcFromDate(stepIndex) {
        const entry = this.rows[stepIndex];
        if (!entry || !(entry.dateInput instanceof HTMLInputElement)) {
            this.recalcAll();
            return;
        }

        const state = this.readState();
        const startDate = this.getStartDate();
        const { plan } = this.computePlanAndActual(state, startDate);
        const previousPlanEnd = stepIndex === 0
            ? startDate
            : plan[stepIndex - 1]?.end || startDate;
        const selectedDate = parseIsoDate(entry.dateInput.value) || previousPlanEnd;
        const computedDuration = Math.max(0, diffCalendarDays(selectedDate, previousPlanEnd));
        state[stepIndex] = { ...state[stepIndex], duration: computedDuration };
        this.writeState(state);
        this.recalcAll();
    }

    recalcFromDelayDate(stepIndex) {
        const entry = this.rows[stepIndex];
        if (!entry || !(entry.delayDateInput instanceof HTMLInputElement)) {
            this.recalcAll();
            return;
        }

        const state = this.readState();
        const startDate = this.getStartDate();
        const { plan } = this.computePlanAndActual(state, startDate);
        const planEnd = plan[stepIndex]?.end || startDate;
        const selectedDate = parseIsoDate(entry.delayDateInput.value) || planEnd;
        const computedDelay = diffCalendarDays(selectedDate, planEnd);
        state[stepIndex] = { ...state[stepIndex], delay: computedDelay };
        this.writeState(state);
        this.recalcAll();
    }

    renderSummary(plan, actual, state, startDate, deadlineDate) {
        const baselineEnd = plan.length > 0 ? plan[plan.length - 1].end : startDate;
        const shiftedEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
        const totalDuration = state.reduce((sum, item) => sum + item.duration, 0);
        const totalDelay = state.reduce((sum, item) => sum + item.delay, 0);
        const stihame = toUtcDayStamp(shiftedEnd) <= toUtcDayStamp(deadlineDate);
        const overrunDays = stihame ? 0 : diffCalendarDays(shiftedEnd, deadlineDate);

        if (this.summaryDeadline instanceof HTMLElement) {
            this.summaryDeadline.textContent = formatDisplayDate(deadlineDate);
        }
        if (this.summaryBaseline instanceof HTMLElement) {
            this.summaryBaseline.textContent = formatDisplayDate(baselineEnd);
        }
        if (this.summaryShifted instanceof HTMLElement) {
            this.summaryShifted.textContent = formatDisplayDate(shiftedEnd);
        }
        if (this.summaryDuration instanceof HTMLElement) {
            this.summaryDuration.textContent = String(totalDuration);
        }
        if (this.summaryDelay instanceof HTMLElement) {
            this.summaryDelay.textContent = totalDelay > 0 ? `+${totalDelay}` : String(totalDelay);
        }
        if (this.summaryState instanceof HTMLElement) {
            this.summaryState.textContent = stihame ? "Stíháme" : "Nestíháme";
        }
        if (this.summaryOverrun instanceof HTMLElement) {
            this.summaryOverrun.textContent = stihame ? "" : `(+${overrunDays} dnů)`;
        }
        if (this.statusLine instanceof HTMLElement) {
            this.statusLine.classList.toggle("ok", stihame);
            this.statusLine.classList.toggle("late", !stihame);
        }

        this.renderMiniGantt(plan, actual, startDate, deadlineDate);
    }

    renderMiniGantt(plan, actual, startDate, deadlineDate) {
        const actualEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
        const { totalDays, axisEndDate } = ScheduleTimelineEngine.buildScale(startDate, deadlineDate, actualEnd);
        const today = new Date();
        const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        const todayPercent = ScheduleTimelineEngine.toPercent(todayDate, startDate, totalDays);
        const deadlinePercent = ScheduleTimelineEngine.toPercent(deadlineDate, startDate, totalDays);
        const formatPercent = (value) => `${Number.isFinite(value) ? value.toFixed(4) : "0.0000"}%`;
        const formatSegmentWidth = (value) => {
            if (!Number.isFinite(value) || value <= 0) {
                return "0%";
            }

            // Add a tiny overlap to avoid visible sub-pixel seams between adjacent segments.
            return `calc(${value.toFixed(4)}% + 1px)`;
        };
        const renderContinuousSegments = (segments, items) => {
            let previousRight = 0;
            segments.forEach((segment, index) => {
                const item = items[index];
                if (!item) {
                    segment.style.left = "0%";
                    segment.style.width = "0%";
                    return;
                }

                const rawLeft = ScheduleTimelineEngine.toPercent(item.start, startDate, totalDays);
                const rawRight = ScheduleTimelineEngine.toPercent(item.end, startDate, totalDays);
                const left = index === 0 ? rawLeft : Math.max(previousRight, rawLeft);
                const right = Math.max(left, rawRight);
                const width = Math.max(0, right - left);

                segment.style.left = formatPercent(left);
                segment.style.width = formatSegmentWidth(width);
                previousRight = right;
            });
        };

        this.ganttTodayMarkers.forEach((marker) => {
            marker.style.left = formatPercent(todayPercent);
            marker.title = `Dnes: ${formatDisplayDate(todayDate)}`;
        });

        this.ganttDeadlineMarkers.forEach((marker) => {
            marker.style.left = formatPercent(deadlinePercent);
            marker.title = `Termín úkolu: ${formatDisplayDate(deadlineDate)}`;
        });

        this.timelineAxes.forEach((axis) => {
            renderTimelineAxis(axis, startDate, axisEndDate);
        });

        renderContinuousSegments(this.ganttPlannedSegments, plan);
        renderContinuousSegments(this.ganttActualSegments, actual);

        queueRainbowSegmentRender(this.editor);
    }

    renderStepRows(plan, actual, state) {
        this.rows.forEach((entry, index) => {
            const planEnd = plan[index]?.end;
            const actualEnd = actual[index]?.end;
            if (entry.baselineCell instanceof HTMLElement && planEnd instanceof Date) {
                entry.baselineCell.textContent = formatDisplayDate(planEnd);
            }
            if (entry.shiftedCell instanceof HTMLElement && actualEnd instanceof Date) {
                entry.shiftedCell.textContent = formatDisplayDate(actualEnd);
            }
            if (entry.dateInput instanceof HTMLInputElement && planEnd instanceof Date) {
                this.setDateInputValue(entry.dateInput, planEnd);
            }
            if (entry.delayDateInput instanceof HTMLInputElement && actualEnd instanceof Date) {
                this.setDateInputValue(entry.delayDateInput, actualEnd);
            }

        });
    }

    recalcAll() {
        const state = this.readState();
        const startDate = this.getStartDate();
        const deadlineDate = this.getDeadlineDate(startDate);
        const { plan, actual } = this.computePlanAndActual(state, startDate);
        this.renderStepRows(plan, actual, state);
        this.renderSummary(plan, actual, state, startDate, deadlineDate);
    }

    bindNumericStepper(button, input, delta, onChange) {
        if (!(button instanceof HTMLButtonElement) || !(input instanceof HTMLInputElement)) {
            return;
        }

        let repeatDelayTimer = null;
        let repeatIntervalTimer = null;
        let suppressClickOnce = false;
        const repeatDelayMs = 350;
        const repeatIntervalMs = 70;

        const stopRepeat = () => {
            if (repeatDelayTimer !== null) {
                window.clearTimeout(repeatDelayTimer);
                repeatDelayTimer = null;
            }
            if (repeatIntervalTimer !== null) {
                window.clearInterval(repeatIntervalTimer);
                repeatIntervalTimer = null;
            }
        };

        const stepOnce = () => {
            if (button.disabled || input.disabled) {
                return;
            }

            const isDelayInput = input.hasAttribute("data-schedule-delay");
            const currentValue = isDelayInput
                ? this.normalizeSignedInt(input)
                : this.normalizeInt(input);
            const nextValue = isDelayInput
                ? currentValue + delta
                : Math.max(0, currentValue + delta);
            input.value = String(nextValue);

            if (nextValue !== currentValue) {
                onChange();
            }
        };

        button.addEventListener("mousedown", (event) => {
            if (!(event instanceof MouseEvent) || event.button !== 0) {
                return;
            }

            event.preventDefault();
            suppressClickOnce = true;
            stepOnce();
            stopRepeat();
            repeatDelayTimer = window.setTimeout(() => {
                repeatIntervalTimer = window.setInterval(() => {
                    stepOnce();
                }, repeatIntervalMs);
            }, repeatDelayMs);
        });

        button.addEventListener("click", (event) => {
            if (suppressClickOnce) {
                suppressClickOnce = false;
                return;
            }

            stepOnce();
        });

        button.addEventListener("mouseup", stopRepeat);
        button.addEventListener("mouseleave", stopRepeat);
        button.addEventListener("blur", () => {
            stopRepeat();
            suppressClickOnce = false;
        });
        window.addEventListener("mouseup", (event) => {
            stopRepeat();
            if (!(event.target instanceof Element) || !button.contains(event.target)) {
                suppressClickOnce = false;
            }
        });
    }

    bind() {
        this.rows.forEach((entry, index) => {
            if (entry.durationInput instanceof HTMLInputElement) {
                entry.durationInput.addEventListener("input", () => this.recalcFromDuration(index));
                entry.durationInput.addEventListener("change", () => {
                    entry.durationInput.value = String(this.normalizeInt(entry.durationInput));
                    this.recalcFromDuration(index);
                });
            }

            if (entry.delayInput instanceof HTMLInputElement) {
                entry.delayInput.addEventListener("input", () => this.recalcFromDelay(index));
                entry.delayInput.addEventListener("change", () => {
                    entry.delayInput.value = String(this.normalizeSignedInt(entry.delayInput));
                    this.recalcFromDelay(index);
                });
            }

            if (entry.dateInput instanceof HTMLInputElement) {
                entry.dateInput.addEventListener("change", () => this.recalcFromDate(index));
            }
            if (entry.delayDateInput instanceof HTMLInputElement) {
                entry.delayDateInput.addEventListener("change", () => this.recalcFromDelayDate(index));
            }

            this.bindNumericStepper(entry.durationInc, entry.durationInput, +1, () => this.recalcFromDuration(index));
            this.bindNumericStepper(entry.durationDec, entry.durationInput, -1, () => this.recalcFromDuration(index));
            this.bindNumericStepper(entry.delayInc, entry.delayInput, +1, () => this.recalcFromDelay(index));
            this.bindNumericStepper(entry.delayDec, entry.delayInput, -1, () => this.recalcFromDelay(index));
        });

        if (this.startInput instanceof HTMLInputElement) {
            this.startInput.addEventListener("change", () => this.recalcAll());
        }
        if (this.deadlineInput instanceof HTMLInputElement) {
            this.deadlineInput.addEventListener("change", () => this.recalcAll());
        }
    }
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
        if (!(form instanceof HTMLFormElement) || form.dataset.recordScheduleReady === "true") {
            return;
        }

        const editor = form.querySelector("[data-record-schedule-editor]");
        const schedulePanel = form.querySelector("[data-record-schedule-panel]");
        if (!(editor instanceof HTMLElement)
            || (schedulePanel instanceof HTMLElement && schedulePanel.dataset.scheduleDisabled === "true")) {
            return;
        }

        const planner = new RecordSchedulePlanner(form, editor);
        if (!planner.isReady()) {
            return;
        }

        planner.bind();
        planner.recalcAll();
        form._recordSchedulePlanner = planner;
        form.dataset.recordScheduleReady = "true";
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
    applyProjectScheduleFilters();
    renderStaticTimelineAxes(schedulePanel);
    queueRainbowSegmentRender(schedulePanel);
    initScheduleExpandUi(schedulePanel);
}
