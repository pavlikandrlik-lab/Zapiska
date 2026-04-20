/**
 * schedule/block.js — ScheduleBlockRenderer class.
 *
 * Fáze 3B Task 2: vyextrahováno ze schedule.js (1697 LOC).
 * Cohesive ~850 LOC unit — kept intact (no internal decomposition).
 */

import {
    addCalendarDays,
    diffCalendarDays,
    formatDisplayDate,
    formatIsoDate,
    msPerDay,
    parseIsoDate,
    toUtcDayStamp
} from "../utils.js";
import { queueRainbowSegmentRender } from "../ui.js";
import { renderTimelineAxis } from "./timeline.js";

// F-07: New async function that calls the server-side /Schedule/Recalc endpoint.
// Replaces the local buildSchedulePlanAndActual calculation for the recalcAll path.
async function fetchSchedulePreview(startDate, deadlineDate, steps, antiForgeryToken) {
    const payload = {
        recordId: 0,
        startDate: formatIsoDate(startDate),
        deadlineDate: formatIsoDate(deadlineDate),
        steps: steps.map(s => ({
            stepIndex: s.stepIndex,
            durationTypeId: s.durationTypeId || 0,
            delayTypeId: s.delayTypeId || 0,
            durationDays: s.duration,
            delayDays: s.delay
        }))
    };
    const response = await fetch('/Schedule/Recalc', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': antiForgeryToken
        },
        body: JSON.stringify(payload),
        credentials: 'same-origin'
    });
    if (!response.ok) {
        return null;
    }
    return await response.json();
}

// F-07: replaced by server-side /Schedule/Recalc endpoint — kept for use in
// recalcFromDate / recalcFromDelayDate which need local date arithmetic before recalcAll.
function buildSchedulePlanAndActual(state, startDate) {
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

function buildScheduleScale(startDate, deadlineDate, actualEndDate) {
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

function toSchedulePercent(valueDate, axisStart, totalDays) {
    const days = diffCalendarDays(valueDate, axisStart);
    return Math.max(0, Math.min(100, (days * 100) / totalDays));
}

function toScheduleWidthPercent(startDate, endDate, totalDays) {
    const days = Math.max(0, diffCalendarDays(endDate, startDate));
    return Math.max(0, Math.min(100, (days * 100) / totalDays));
}

function formatSchedulePercent(value) {
    return `${Number.isFinite(value) ? value.toFixed(4) : "0.0000"}%`;
}

function formatScheduleSegmentWidth(value) {
    if (!Number.isFinite(value) || value <= 0) {
        return "0%";
    }

    return `calc(${value.toFixed(4)}% + 1px)`;
}

function formatScheduleOffsetLabel(delay) {
    return delay > 0 ? `+${delay} dnů` : `${delay} dnů`;
}

export class ScheduleBlockRenderer {
    constructor(root, options = {}) {
        this.root = root;
        this.mode = String(root.dataset.scheduleMode || "project-readonly").trim() || "project-readonly";
        this.form = options.form instanceof HTMLFormElement
            ? options.form
            : root.closest('form[data-record-editor-form="true"]');
        this.startInput = this.form instanceof HTMLFormElement
            ? this.form.querySelector('input[name="DatumZalozeni"]')
            : null;
        this.deadlineInput = this.form instanceof HTMLFormElement
            ? this.form.querySelector('input[name="TerminUkonceni"]')
            : null;
        this.summaryDeadline = root.querySelector("[data-schedule-summary-deadline]");
        this.summaryBaseline = root.querySelector("[data-schedule-summary-baseline]");
        this.summaryShifted = root.querySelector("[data-schedule-summary-shifted]");
        this.summaryDuration = root.querySelector("[data-schedule-summary-duration]");
        this.summaryDelay = root.querySelector("[data-schedule-summary-delay]");
        this.summaryState = root.querySelector("[data-schedule-summary-state]");
        this.summaryOverrun = root.querySelector("[data-schedule-summary-overrun]");
        this.statusLine = root.querySelector(".schedule-status-line");
        this.overviewAxis = root.querySelector('[data-schedule-axis="overview"]');
        this.breakdownAxis = root.querySelector('[data-schedule-axis="breakdown"]');
        this.overviewTodayMarkers = Array.from(root.querySelectorAll('[data-schedule-marker="today"]'))
            .filter((node) => node instanceof HTMLElement);
        this.overviewDeadlineMarkers = Array.from(root.querySelectorAll('[data-schedule-marker="deadline"]'))
            .filter((node) => node instanceof HTMLElement);
        this.overviewPlannedSegments = this.collectSegmentMap('[data-schedule-segment-kind="planned"]');
        this.overviewActualSegments = this.collectSegmentMap('[data-schedule-segment-kind="actual"]');
        this.breakdownRows = Array.from(root.querySelectorAll("[data-schedule-breakdown-track]"))
            .map((track) => {
                if (!(track instanceof HTMLElement)) {
                    return null;
                }

                const row = track.closest("[data-schedule-step-row]");
                if (!(row instanceof HTMLElement)) {
                    return null;
                }

                const stepIndex = Number.parseInt(row.dataset.stepIndex || "", 10);
                if (!Number.isInteger(stepIndex)) {
                    return null;
                }

                return {
                    row,
                    stepIndex,
                    offset: row.querySelector("[data-schedule-offset]"),
                    track,
                    plannedSegment: track.querySelector('[data-schedule-breakdown-segment="planned"]'),
                    actualSegment: track.querySelector('[data-schedule-breakdown-segment="actual"]'),
                    todayMarker: track.querySelector("[data-schedule-breakdown-today]")
                };
            })
            .filter((entry) => entry && Number.isInteger(entry.stepIndex))
            .sort((left, right) => left.stepIndex - right.stepIndex);
        // TODO F-04: editorRows je pole řazené dle stepIndex (KrokIndex, 1-based).
        // Přístup přes this.editorRows[index] v recalcFromDate/recalcFromDelayDate
        // používá 0-based forEach index — to funguje správně, pokud KrokIndex hodnoty
        // nemají mezery. Pokud by mezery nastaly (smazaný krok), je potřeba přepsat
        // na Map<stepIndex, entry> a upravit všechny přístupy (readState, writeState,
        // renderEditorRows, bind). Aktuálně ponecháme pole; při výskytu mezery v KrokIndex
        // by bylo nutno tuto refaktorizaci dokončit.
        this.editorRows = Array.from(root.querySelectorAll("tr[data-schedule-step-row]"))
            .map((row) => {
                if (!(row instanceof HTMLTableRowElement)) {
                    return null;
                }

                const stepIndex = Number.parseInt(row.dataset.stepIndex || "", 10);
                if (!Number.isInteger(stepIndex)) {
                    return null;
                }

                return {
                    row,
                    stepIndex,
                    durationInput: row.querySelector("[data-schedule-duration]"),
                    delayInput: row.querySelector("[data-schedule-delay]"),
                    dateInput: row.querySelector("[data-schedule-date]"),
                    delayDateInput: row.querySelector("[data-schedule-delay-date]"),
                    durationInc: row.querySelector("[data-schedule-duration-inc]"),
                    durationDec: row.querySelector("[data-schedule-duration-dec]"),
                    delayInc: row.querySelector("[data-schedule-delay-inc]"),
                    delayDec: row.querySelector("[data-schedule-delay-dec]")
                };
            })
            .filter((entry) => entry && Number.isInteger(entry.stepIndex))
            .sort((left, right) => left.stepIndex - right.stepIndex);
        const inlineDelayColor = String(root.style.getPropertyValue("--record-schedule-delay-color") || "").trim();
        const computedDelayColor = window.getComputedStyle(root).getPropertyValue("--record-schedule-delay-color").trim();
        this.delayColor = inlineDelayColor || computedDelayColor || "var(--schedule-delay)";
    }

    collectSegmentMap(selector) {
        const segments = new Map();
        this.root.querySelectorAll(selector).forEach((node) => {
            if (!(node instanceof HTMLElement)) {
                return;
            }

            const stepIndex = Number.parseInt(node.dataset.stepIndex || "", 10);
            if (!Number.isInteger(stepIndex)) {
                return;
            }

            segments.set(stepIndex, node);
        });
        return segments;
    }

    isReady() {
        return this.root instanceof HTMLElement
            && (this.editorRows.length > 0 || this.breakdownRows.length > 0);
    }

    normalizeInt(input) {
        if (!(input instanceof HTMLInputElement)) {
            return 0;
        }

        const parsed = Number.parseInt((input.value || "").trim(), 10);
        return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
    }

    normalizeSignedInt(input) {
        if (!(input instanceof HTMLInputElement)) {
            return 0;
        }

        const parsed = Number.parseInt((input.value || "").trim(), 10);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    setDateInputValue(input, value) {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        input.value = formatIsoDate(value);
        const dateField = input.closest("[data-app-date-field]");
        if (!(dateField instanceof HTMLElement)) {
            return;
        }

        const display = dateField.querySelector("[data-app-date-display]");
        if (display instanceof HTMLInputElement) {
            display.value = formatDisplayDate(value);
        }
    }

    getStartDate() {
        if (this.startInput instanceof HTMLInputElement) {
            return parseIsoDate(this.startInput.value) || new Date();
        }

        return parseIsoDate(this.root.dataset.scheduleStart) || new Date();
    }

    getDeadlineDate(startDate) {
        if (this.deadlineInput instanceof HTMLInputElement) {
            return parseIsoDate(this.deadlineInput.value) || startDate;
        }

        return parseIsoDate(this.root.dataset.scheduleDeadline) || startDate;
    }

    readTypeId(input) {
        if (!(input instanceof HTMLInputElement)) {
            return 0;
        }

        const stacked = input.closest(".schedule-stacked-input");
        if (!(stacked instanceof HTMLElement)) {
            return 0;
        }

        const hidden = stacked.querySelector("input[type='hidden']");
        if (!(hidden instanceof HTMLInputElement)) {
            return 0;
        }

        return Number.parseInt(hidden.value || "0", 10) || 0;
    }

    readState() {
        if (this.editorRows.length > 0) {
            return this.editorRows.map((entry) => ({
                stepIndex: entry.stepIndex,
                name: String(entry.row.dataset.stepName || "").trim(),
                color: String(entry.row.dataset.stepColor || "").trim(),
                duration: this.normalizeInt(entry.durationInput),
                delay: this.normalizeSignedInt(entry.delayInput),
                durationTypeId: this.readTypeId(entry.durationInput),
                delayTypeId: this.readTypeId(entry.delayInput)
            }));
        }

        return this.breakdownRows.map((entry) => ({
            stepIndex: entry.stepIndex,
            name: String(entry.row.dataset.stepName || "").trim(),
            color: String(entry.row.dataset.stepColor || "").trim(),
            duration: Math.max(0, Number.parseInt(entry.row.dataset.stepDuration || "0", 10) || 0),
            delay: Number.parseInt(entry.row.dataset.stepDelay || "0", 10) || 0,
            durationTypeId: 0,
            delayTypeId: 0
        }));
    }

    writeState(state) {
        state.forEach((item, index) => {
            const entry = this.editorRows[index];
            if (!entry) {
                return;
            }

            if (entry.durationInput instanceof HTMLInputElement) {
                entry.durationInput.value = String(item.duration);
            }

            if (entry.delayInput instanceof HTMLInputElement) {
                entry.delayInput.value = String(item.delay);
            }

            entry.row.dataset.stepDuration = String(item.duration);
            entry.row.dataset.stepDelay = String(item.delay);
        });
    }

    recalcFromDuration() {
        this.recalcAll();
    }

    recalcFromDelay() {
        this.recalcAll();
    }

    recalcFromDate(stepIndex) {
        const entry = this.editorRows[stepIndex];
        if (!entry || !(entry.dateInput instanceof HTMLInputElement)) {
            this.recalcAll();
            return;
        }

        const state = this.readState();
        const startDate = this.getStartDate();
        const { plan } = buildSchedulePlanAndActual(state, startDate);
        const previousPlanEnd = stepIndex === 0
            ? startDate
            : plan[stepIndex - 1]?.end || startDate;
        const selectedDate = parseIsoDate(entry.dateInput.value) || previousPlanEnd;
        state[stepIndex] = { ...state[stepIndex], duration: Math.max(0, diffCalendarDays(selectedDate, previousPlanEnd)) };
        this.writeState(state);
        this.recalcAll();
    }

    recalcFromDelayDate(stepIndex) {
        const entry = this.editorRows[stepIndex];
        if (!entry || !(entry.delayDateInput instanceof HTMLInputElement)) {
            this.recalcAll();
            return;
        }

        const state = this.readState();
        const startDate = this.getStartDate();
        const { plan } = buildSchedulePlanAndActual(state, startDate);
        const planEnd = plan[stepIndex]?.end || startDate;
        const selectedDate = parseIsoDate(entry.delayDateInput.value) || planEnd;
        state[stepIndex] = { ...state[stepIndex], delay: diffCalendarDays(selectedDate, planEnd) };
        this.writeState(state);
        this.recalcAll();
    }

    setAxisRange(axis, startDate, endDate) {
        if (!(axis instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
            return;
        }

        axis.dataset.axisStart = formatIsoDate(startDate);
        axis.dataset.axisEnd = formatIsoDate(endDate);
        renderTimelineAxis(axis, startDate, endDate);
    }

    applySegmentLayout(segment, leftPercent, widthPercent, title) {
        if (!(segment instanceof HTMLElement)) {
            return;
        }

        segment.style.left = formatSchedulePercent(leftPercent);
        segment.style.width = formatScheduleSegmentWidth(widthPercent);
        segment.style.display = widthPercent > 0 ? "" : "none";
        if (title) {
            segment.title = title;
        }
    }

    resetSegmentLayout(segment) {
        if (!(segment instanceof HTMLElement)) {
            return;
        }

        segment.style.left = "0%";
        segment.style.width = "0%";
        segment.style.display = "none";
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
            this.summaryOverrun.textContent = this.mode === "record-editor"
                ? (stihame ? "" : `(+${overrunDays} dnů)`)
                : `${overrunDays} dnů`;
        }
        if (this.statusLine instanceof HTMLElement) {
            this.statusLine.classList.toggle("ok", stihame);
            this.statusLine.classList.toggle("late", !stihame);
        }
    }

    renderOverview(plan, actual, state, startDate, deadlineDate) {
        const actualEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
        const { totalDays, axisEndDate } = buildScheduleScale(startDate, deadlineDate, actualEnd);
        const today = new Date();
        const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        const todayPercent = toSchedulePercent(todayDate, startDate, totalDays);
        const deadlinePercent = toSchedulePercent(deadlineDate, startDate, totalDays);

        this.overviewTodayMarkers.forEach((marker) => {
            marker.style.left = formatSchedulePercent(todayPercent);
            marker.title = `Dnes: ${formatDisplayDate(todayDate)}`;
        });

        this.overviewDeadlineMarkers.forEach((marker) => {
            marker.style.left = formatSchedulePercent(deadlinePercent);
            marker.title = `Termín úkolu: ${formatDisplayDate(deadlineDate)}`;
        });

        this.setAxisRange(this.overviewAxis, startDate, axisEndDate);

        let previousPlanRight = 0;
        let previousActualRight = 0;
        let skippedCompactActualWidth = 0;

        state.forEach((item, index) => {
            const planItem = plan[index];
            const actualItem = actual[index];
            const plannedSegment = this.overviewPlannedSegments.get(item.stepIndex);
            const actualSegment = this.overviewActualSegments.get(item.stepIndex);
            if (!planItem || !actualItem) {
                this.resetSegmentLayout(plannedSegment);
                this.resetSegmentLayout(actualSegment);
                return;
            }

            const rawPlanLeft = toSchedulePercent(planItem.start, startDate, totalDays);
            const rawPlanRight = toSchedulePercent(planItem.end, startDate, totalDays);
            const rawActualLeft = toSchedulePercent(actualItem.start, startDate, totalDays);
            const rawActualRight = toSchedulePercent(actualItem.end, startDate, totalDays);

            if (item.duration > 0) {
                skippedCompactActualWidth = 0;  // Reset při každém viditelném kroku
                const planLeft = Math.max(previousPlanRight, rawPlanLeft);
                const planRight = Math.max(planLeft, rawPlanRight);
                const planWidth = Math.max(0, planRight - planLeft);
                this.applySegmentLayout(
                    plannedSegment,
                    planLeft,
                    planWidth,
                    `${item.name}: plán ${formatDisplayDate(planItem.start)} - ${formatDisplayDate(planItem.end)}`);
                previousPlanRight = planRight;

                const adjustedActualLeft = Math.max(0, rawActualLeft - skippedCompactActualWidth);
                const adjustedActualRight = Math.max(adjustedActualLeft, rawActualRight - skippedCompactActualWidth);
                const actualLeft = Math.max(previousActualRight, adjustedActualLeft);
                const actualRight = Math.max(actualLeft, adjustedActualRight);
                const actualWidth = Math.max(0, actualRight - actualLeft);
                if (actualWidth <= 0) {
                    this.resetSegmentLayout(actualSegment);
                } else {
                    this.applySegmentLayout(
                        actualSegment,
                        actualLeft,
                        actualWidth,
                        `${item.name}: skutečnost ${formatDisplayDate(actualItem.start)} - ${formatDisplayDate(actualItem.end)}`);
                }
                previousActualRight = actualRight;
            } else {
                // Akumuluj přeskočenou šířku jen pro bezprostředně sousední nulové kroky.
                // Reset se provede při dalším duration>0 kroku níže.
                skippedCompactActualWidth += Math.max(0, rawActualRight - rawActualLeft);
                this.resetSegmentLayout(plannedSegment);
                this.resetSegmentLayout(actualSegment);
            }
        });
    }

    resolveBreakdownAxis(plan, actual, startDate, deadlineDate) {
        const dates = [];
        plan.forEach((item) => {
            dates.push(item.start, item.end);
        });
        actual.forEach((item) => {
            dates.push(item.start, item.end);
        });

        if (dates.length === 0) {
            return {
                axisStart: startDate,
                axisEnd: deadlineDate
            };
        }

        const sorted = dates.slice().sort((left, right) => left.getTime() - right.getTime());
        const axisStart = sorted[0];
        const latest = sorted[sorted.length - 1];
        const axisEnd = latest.getTime() > deadlineDate.getTime() ? latest : deadlineDate;

        return {
            axisStart,
            axisEnd
        };
    }

    renderBreakdown(plan, actual, state, startDate, deadlineDate) {
        if (this.breakdownRows.length === 0) {
            return;
        }

        const { axisStart, axisEnd } = this.resolveBreakdownAxis(plan, actual, startDate, deadlineDate);
        const totalDays = Math.max(1, diffCalendarDays(axisEnd, axisStart));
        const today = new Date();
        const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        const todayPercent = toSchedulePercent(todayDate, axisStart, totalDays);

        this.setAxisRange(this.breakdownAxis, axisStart, axisEnd);

        this.breakdownRows.forEach((entry, index) => {
            const item = state[index];
            const planItem = plan[index];
            const actualItem = actual[index];
            if (!item || !planItem || !actualItem) {
                return;
            }

            if (entry.offset instanceof HTMLElement) {
                entry.offset.textContent = formatScheduleOffsetLabel(item.delay);
                entry.offset.classList.toggle("late", item.delay > 0);
                entry.offset.classList.toggle("ahead", item.delay < 0);
            }

            if (entry.todayMarker instanceof HTMLElement) {
                entry.todayMarker.style.left = formatSchedulePercent(todayPercent);
                entry.todayMarker.title = `Dnes: ${formatDisplayDate(todayDate)}`;
            }

            if (item.duration <= 0) {
                this.resetSegmentLayout(entry.plannedSegment);
                this.resetSegmentLayout(entry.actualSegment);
                return;
            }

            const plannedLeft = toSchedulePercent(planItem.start, axisStart, totalDays);
            const plannedWidth = toScheduleWidthPercent(planItem.start, planItem.end, totalDays);
            this.applySegmentLayout(
                entry.plannedSegment,
                plannedLeft,
                plannedWidth,
                `${item.name}: plán ${formatDisplayDate(planItem.start)} - ${formatDisplayDate(planItem.end)}`);

            const actualLeft = toSchedulePercent(actualItem.start, axisStart, totalDays);
            const actualWidth = toScheduleWidthPercent(actualItem.start, actualItem.end, totalDays);
            this.applySegmentLayout(
                entry.actualSegment,
                actualLeft,
                actualWidth,
                `${item.name}: skutečnost ${formatDisplayDate(actualItem.start)} - ${formatDisplayDate(actualItem.end)}`);
            if (entry.actualSegment instanceof HTMLElement) {
                entry.actualSegment.style.setProperty("--schedule-actual-color", this.delayColor);
            }
        });
    }

    renderEditorRows(plan, actual) {
        this.editorRows.forEach((entry, index) => {
            const planEnd = plan[index]?.end;
            const actualEnd = actual[index]?.end;

            if (entry.dateInput instanceof HTMLInputElement && planEnd instanceof Date) {
                this.setDateInputValue(entry.dateInput, planEnd);
            }

            if (entry.delayDateInput instanceof HTMLInputElement && actualEnd instanceof Date) {
                this.setDateInputValue(entry.delayDateInput, actualEnd);
            }

            entry.row.dataset.stepDuration = String(this.normalizeInt(entry.durationInput));
            entry.row.dataset.stepDelay = String(this.normalizeSignedInt(entry.delayInput));

            // F-15: Dynamicky nastavit min pro delay input, aby duration + delay >= 0
            if (entry.durationInput instanceof HTMLInputElement && entry.delayInput instanceof HTMLInputElement) {
                const currentDuration = parseInt(entry.durationInput.value, 10) || 0;
                entry.delayInput.min = -currentDuration;
            }
        });
    }

    getAntiForgeryToken() {
        // Try the enclosing form first (editor context)
        if (this.form instanceof HTMLFormElement) {
            const tokenInput = this.form.querySelector('input[name="__RequestVerificationToken"]');
            if (tokenInput instanceof HTMLInputElement && tokenInput.value) {
                return tokenInput.value;
            }
        }

        // Fall back to a dedicated data attribute placed by the view
        const dedicated = this.root.querySelector("[data-schedule-antiforgery]");
        if (dedicated instanceof HTMLInputElement && dedicated.value) {
            return dedicated.value;
        }

        // Last resort: scan the whole document
        const global = document.querySelector('input[name="__RequestVerificationToken"]');
        if (global instanceof HTMLInputElement && global.value) {
            return global.value;
        }

        return "";
    }

    // F-07: recalcAll is now async — calls /Schedule/Recalc server endpoint.
    // Falls back to local buildSchedulePlanAndActual when the fetch fails so the
    // UI stays functional even if the endpoint is temporarily unavailable.
    async recalcAll() {
        const state = this.readState();
        if (state.length === 0) {
            return;
        }

        const startDate = this.getStartDate();
        const deadlineDate = this.getDeadlineDate(startDate);

        // Debounce: cancel previous pending recalc and schedule a new one.
        if (this._recalcDebounceTimer !== undefined) {
            clearTimeout(this._recalcDebounceTimer);
        }

        await new Promise((resolve) => {
            this._recalcDebounceTimer = setTimeout(resolve, 150);
        });

        // Try server-side calculation first
        const token = this.getAntiForgeryToken();
        let usedServerData = false;

        if (token) {
            try {
                const serverResult = await fetchSchedulePreview(startDate, deadlineDate, state, token);
                if (serverResult && Array.isArray(serverResult.steps)) {
                    // Build plan/actual arrays from server response for the renderers
                    const plan = serverResult.steps.map((s) => ({
                        start: parseIsoDate(s.planStart) || startDate,
                        end: parseIsoDate(s.planEnd) || startDate
                    }));
                    const actual = serverResult.steps.map((s) => ({
                        start: parseIsoDate(s.actualStart) || startDate,
                        end: parseIsoDate(s.actualEnd) || startDate
                    }));

                    this.renderEditorRows(plan, actual);
                    this.renderSummary(plan, actual, state, startDate, deadlineDate);
                    this.renderOverview(plan, actual, state, startDate, deadlineDate);
                    this.renderBreakdown(plan, actual, state, startDate, deadlineDate);
                    queueRainbowSegmentRender(this.root);
                    usedServerData = true;
                }
            } catch (_err) {
                // Fall through to local calculation
            }
        }

        if (!usedServerData) {
            // Fallback: local calculation (F-07 kept as fallback)
            const { plan, actual } = buildSchedulePlanAndActual(state, startDate);
            this.renderEditorRows(plan, actual);
            this.renderSummary(plan, actual, state, startDate, deadlineDate);
            this.renderOverview(plan, actual, state, startDate, deadlineDate);
            this.renderBreakdown(plan, actual, state, startDate, deadlineDate);
            queueRainbowSegmentRender(this.root);
        }
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

        const startRepeat = () => {
            suppressClickOnce = true;
            stepOnce();
            stopRepeat();
            repeatDelayTimer = window.setTimeout(() => {
                repeatIntervalTimer = window.setInterval(() => {
                    stepOnce();
                }, repeatIntervalMs);
            }, repeatDelayMs);
        };

        button.addEventListener("mousedown", (event) => {
            if (!(event instanceof MouseEvent) || event.button !== 0) {
                return;
            }

            event.preventDefault();
            startRepeat();
        });

        button.addEventListener("touchstart", (event) => {
            event.preventDefault();
            startRepeat();
        }, { passive: false });

        button.addEventListener("click", () => {
            if (suppressClickOnce) {
                suppressClickOnce = false;
                return;
            }

            stepOnce();
        });

        button.addEventListener("mouseup", stopRepeat);
        button.addEventListener("mouseleave", stopRepeat);
        button.addEventListener("touchend", stopRepeat);
        button.addEventListener("touchcancel", stopRepeat);
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
        if (this.mode !== "record-editor") {
            return;
        }

        this.editorRows.forEach((entry, index) => {
            if (entry.durationInput instanceof HTMLInputElement) {
                let durationDebounceTimer = null;
                entry.durationInput.addEventListener("input", () => {
                    clearTimeout(durationDebounceTimer);
                    durationDebounceTimer = setTimeout(() => {
                        this.recalcFromDuration(index);
                    }, 150);
                });
                entry.durationInput.addEventListener("change", () => {
                    entry.durationInput.value = String(this.normalizeInt(entry.durationInput));
                    this.recalcFromDuration(index);
                });
            }

            if (entry.delayInput instanceof HTMLInputElement) {
                let delayDebounceTimer = null;
                entry.delayInput.addEventListener("input", () => {
                    clearTimeout(delayDebounceTimer);
                    delayDebounceTimer = setTimeout(() => {
                        this.recalcFromDelay(index);
                    }, 150);
                });
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
