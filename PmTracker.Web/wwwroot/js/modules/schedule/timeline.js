/**
 * schedule/timeline.js — timeline axis rendering utilities.
 *
 * Fáze 3B Task 2: vyextrahováno ze schedule.js (1697 LOC).
 * Obsahuje: buildTimelineAxisTicks, renderTimelineAxis,
 *           renderStaticTimelineAxes, resolveTimelineAxisTickTargetCount,
 *           queueTimelineAxisRetry.
 */

import {
    addCalendarDays,
    diffCalendarDays,
    formatAxisDayMonth,
    formatAxisMonthYear,
    measureTextWidth,
    parseIsoDate,
    toUtcDayStamp
} from "../utils.js";

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
    const edgeInsetPx = Math.max(2, Math.min(4, Math.round(containerWidth * 0.006)));
    const usableAxisWidth = Math.max(1, containerWidth - (edgeInsetPx * 2));
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
    const resolveLabelPlacement = (index) => {
        if (tickNodes.length === 1 || index === 0) {
            return "start";
        }

        if (index === lastIndex) {
            return "end";
        }

        return "center";
    };
    const clampAbsoluteLeft = (value, width) => Math.max(0, Math.min(value, Math.max(0, containerWidth - width)));
    const resolveLabelLayout = (tickNode, labelNode, index) => {
        const placement = resolveLabelPlacement(index);
        const tickLeftPx = resolveTickLeftPx(tickNode);
        const maxWidthByPlacement = placement === "start"
            ? Math.max(1, containerWidth - tickLeftPx)
            : placement === "end"
                ? Math.max(1, tickLeftPx)
                : Math.max(1, containerWidth);

        labelNode.style.maxWidth = `${Math.max(1, Math.floor(maxWidthByPlacement))}px`;
        const labelWidthRaw = resolveLabelWidth(labelNode);
        const labelWidth = Math.max(1, Math.min(maxWidthByPlacement, labelWidthRaw > 0 ? labelWidthRaw : 1));
        const desiredLeft = placement === "start"
            ? tickLeftPx
            : placement === "end"
                ? tickLeftPx - labelWidth
                : tickLeftPx - (labelWidth / 2);
        const absoluteLeft = clampAbsoluteLeft(desiredLeft, labelWidth);

        return {
            tickNode,
            labelNode,
            tickLeftPx,
            labelWidth,
            absoluteLeft,
            absoluteRight: absoluteLeft + labelWidth
        };
    };
    const applyLabelLayout = (layout, hidden) => {
        if (!layout || !(layout.labelNode instanceof HTMLElement)) {
            return;
        }

        layout.labelNode.hidden = hidden;
        if (hidden) {
            return;
        }

        layout.labelNode.style.left = `${Math.round(layout.absoluteLeft - layout.tickLeftPx)}px`;
    };

    const labelLayouts = tickNodes
        .map((tickNode, index) => {
            if (!(tickNode instanceof HTMLElement)) {
                return null;
            }

            const labelNode = tickNode.querySelector(".timeline-axis-label");
            if (!(labelNode instanceof HTMLElement)) {
                return null;
            }

            labelNode.hidden = false;
            labelNode.style.left = "0px";
            return resolveLabelLayout(tickNode, labelNode, index);
        })
        .filter((layout) => layout && layout.labelNode instanceof HTMLElement);

    if (labelLayouts.length === 0) {
        return;
    }

    if (labelLayouts.length === 1) {
        applyLabelLayout(labelLayouts[0], false);
        return;
    }

    const firstLayout = labelLayouts[0];
    const lastLayout = labelLayouts[labelLayouts.length - 1];
    applyLabelLayout(firstLayout, false);
    applyLabelLayout(lastLayout, false);

    let previousLabelRight = firstLayout.absoluteRight;
    const reservedLastLeft = lastLayout.absoluteLeft;
    for (let index = 1; index < labelLayouts.length - 1; index += 1) {
        const currentLayout = labelLayouts[index];
        const overlapsPrevious = currentLayout.absoluteLeft < previousLabelRight + minLabelGap;
        const overlapsLast = currentLayout.absoluteRight > reservedLastLeft - minLabelGap;
        const shouldHide = overlapsPrevious || overlapsLast;
        applyLabelLayout(currentLayout, shouldHide);
        if (!shouldHide) {
            previousLabelRight = currentLayout.absoluteRight;
        }
    }

}

/**
 * Parsuje server-serializované měsíční ticky z data-schedule-ticks (JSON `{left,label}[]`).
 * Kanonická osa (scheduleAxis.js / ScheduleBarLayoutCalculator) je počítá; JS je jen kreslí.
 */
export function buildTicksFromServer(json) {
    if (!json) {
        return [];
    }
    try {
        const parsed = JSON.parse(json);
        return Array.isArray(parsed)
            ? parsed
                .filter((t) => t && Number.isFinite(t.left))
                .map((t) => ({ left: t.left, label: String(t.label ?? "") }))
            : [];
    } catch {
        return [];
    }
}

/**
 * Rozmístí popisky tiků v px (left + maxWidth) relativně k px pozici gridline.
 * KRITICKÉ: `.timeline-axis-label` je position:absolute uvnitř 1px-širokého tiku, takže
 * bez tohoto JS by se opíral o nesmyslné CSS `left:4px; max-width:calc(100% − 4px)` (100% = 1px).
 * Sdílí stejné chování s renderTimelineAxis (gantt). Popisek umístění: první start, poslední end,
 * ostatní vycentrované na gridline; překryvy se skryjí (gridline zůstává).
 */
function layoutAxisLabels(container, containerWidth) {
    const minLabelGap = 6;
    const tickNodes = Array.from(container.querySelectorAll(".timeline-axis-tick"))
        .filter((tickNode) => tickNode instanceof HTMLElement);
    const lastIndex = tickNodes.length - 1;
    const resolveLabelWidth = (labelNode) => {
        const measuredLabelWidth = labelNode.offsetWidth;
        const computedStyle = window.getComputedStyle(labelNode);
        const fallbackFontSpec = `${computedStyle.fontWeight} ${computedStyle.fontSize} ${computedStyle.fontFamily}`;
        const fallbackLabelWidth = Math.ceil(measureTextWidth(labelNode.textContent || "", fallbackFontSpec));
        return measuredLabelWidth > 0 ? measuredLabelWidth : fallbackLabelWidth;
    };
    const resolveTickLeftPx = (tickNode) => {
        const serializedPx = Number.parseFloat(tickNode.dataset.axisLeftPx || "");
        if (Number.isFinite(serializedPx)) {
            return serializedPx;
        }
        const measuredLeft = Number.parseFloat(tickNode.style.left || "0");
        return Number.isFinite(measuredLeft) ? measuredLeft : 0;
    };
    const resolveLabelPlacement = (index) =>
        (tickNodes.length === 1 || index === 0) ? "start" : index === lastIndex ? "end" : "center";
    const clampAbsoluteLeft = (value, width) => Math.max(0, Math.min(value, Math.max(0, containerWidth - width)));
    const resolveLabelLayout = (tickNode, labelNode, index) => {
        const placement = resolveLabelPlacement(index);
        const tickLeftPx = resolveTickLeftPx(tickNode);
        const maxWidthByPlacement = placement === "start"
            ? Math.max(1, containerWidth - tickLeftPx)
            : placement === "end" ? Math.max(1, tickLeftPx) : Math.max(1, containerWidth);
        labelNode.style.maxWidth = `${Math.max(1, Math.floor(maxWidthByPlacement))}px`;
        const labelWidthRaw = resolveLabelWidth(labelNode);
        const labelWidth = Math.max(1, Math.min(maxWidthByPlacement, labelWidthRaw > 0 ? labelWidthRaw : 1));
        const desiredLeft = placement === "start"
            ? tickLeftPx
            : placement === "end" ? tickLeftPx - labelWidth : tickLeftPx - (labelWidth / 2);
        const absoluteLeft = clampAbsoluteLeft(desiredLeft, labelWidth);
        return { labelNode, tickLeftPx, labelWidth, absoluteLeft, absoluteRight: absoluteLeft + labelWidth };
    };
    const applyLabelLayout = (layout, hidden) => {
        if (!layout) {
            return;
        }
        layout.labelNode.hidden = hidden;
        if (!hidden) {
            layout.labelNode.style.left = `${Math.round(layout.absoluteLeft - layout.tickLeftPx)}px`;
        }
    };

    const labelLayouts = tickNodes
        .map((tickNode, index) => {
            const labelNode = tickNode.querySelector(".timeline-axis-label");
            if (!(labelNode instanceof HTMLElement)) {
                return null;
            }
            labelNode.hidden = false;
            labelNode.style.left = "0px";
            return resolveLabelLayout(tickNode, labelNode, index);
        })
        .filter((layout) => layout !== null);

    if (labelLayouts.length === 1) {
        applyLabelLayout(labelLayouts[0], false);
    } else if (labelLayouts.length >= 2) {
        const firstLayout = labelLayouts[0];
        const lastLayout = labelLayouts[labelLayouts.length - 1];
        applyLabelLayout(firstLayout, false);
        applyLabelLayout(lastLayout, false);
        let previousLabelRight = firstLayout.absoluteRight;
        const reservedLastLeft = lastLayout.absoluteLeft;
        for (let index = 1; index < labelLayouts.length - 1; index += 1) {
            const currentLayout = labelLayouts[index];
            const overlaps = currentLayout.absoluteLeft < previousLabelRight + minLabelGap
                || currentLayout.absoluteRight > reservedLastLeft - minLabelGap;
            applyLabelLayout(currentLayout, overlaps);
            if (!overlaps) {
                previousLabelRight = currentLayout.absoluteRight;
            }
        }
    }

    // „DNES"/„TERMÍN" popisky na ose (priorita nad měsíčními popisky): vykreslí se z data-axis-today-pct
    // / data-axis-deadline-pct a měsíční popisek, který by je překryl, se schová. Vždy poslední, aby vyhrály.
    layoutAxisEventMarkers(container, containerWidth, labelLayouts, minLabelGap);
}

/**
 * Vykreslí „DNES" a „TERMÍN" ukazatele na ose (gridline + popisek) na pozici z data-axis-today-pct
 * a data-axis-deadline-pct (% osy). Prázdné/NaN → datum mimo osu → marker se nevykreslí. Měsíční popisek
 * kolidující s těmito popisky se schová (mají prioritu). Markery jsou samostatné uzly (.timeline-axis-event),
 * ne .timeline-axis-tick, takže se nepletou do rozmístění měsíčních popisků.
 */
function layoutAxisEventMarkers(container, containerWidth, monthLabelLayouts, minLabelGap) {
    container.querySelectorAll(".timeline-axis-event").forEach((node) => node.remove());

    const renderMarker = (pctRaw, modifierClass, text) => {
        const pct = Number.parseFloat(pctRaw || "");
        if (!Number.isFinite(pct)) {
            return;
        }
        const leftPx = (Math.max(0, Math.min(100, pct)) / 100) * containerWidth;

        const markerNode = document.createElement("span");
        markerNode.className = `timeline-axis-event ${modifierClass}`;
        markerNode.dataset.axisLeftPx = leftPx.toFixed(4);
        markerNode.style.left = `${leftPx.toFixed(4)}px`;

        const labelNode = document.createElement("span");
        labelNode.className = "timeline-axis-event-label";
        labelNode.textContent = text;
        markerNode.appendChild(labelNode);
        container.appendChild(markerNode);

        // Popisek vycentrovat na gridline, ořezat na šířku kontejneru.
        const computedStyle = window.getComputedStyle(labelNode);
        const fontSpec = `${computedStyle.fontWeight} ${computedStyle.fontSize} ${computedStyle.fontFamily}`;
        const labelWidth = Math.max(1, labelNode.offsetWidth || Math.ceil(measureTextWidth(text, fontSpec)));
        const desiredLeft = leftPx - (labelWidth / 2);
        const absoluteLeft = Math.max(0, Math.min(desiredLeft, Math.max(0, containerWidth - labelWidth)));
        const absoluteRight = absoluteLeft + labelWidth;
        labelNode.style.left = `${Math.round(absoluteLeft - leftPx)}px`;

        // Událostní popisek vyhrává: schovej každý měsíční popisek, který by se s ním (s mezerou) překrýval.
        monthLabelLayouts.forEach((layout) => {
            if (!layout || layout.labelNode.hidden) {
                return;
            }
            const overlaps = layout.absoluteLeft < absoluteRight + minLabelGap
                && layout.absoluteRight > absoluteLeft - minLabelGap;
            if (overlaps) {
                layout.labelNode.hidden = true;
            }
        });
    };

    // TERMÍN první, DNES druhý → při vzájemné kolizi je DNES nakreslen navrch (vyšší priorita).
    renderMarker(container.dataset.axisDeadlinePct, "deadline", "TERMÍN");
    renderMarker(container.dataset.axisTodayPct, "today", "DNES");
}

/**
 * Vykreslí měsíční ticky ze seznamu (gridline left v %, plná šířka — bez edge-insetu, sjednoceno
 * se segmenty/markery). Popisky pozicuje layoutAxisLabels v px (jinak je CSS opře o 1px tik).
 * Pozn.: gantt board používá vlastní renderTimelineAxis (vzorkování) — tato cesta je jen pro
 * statickou kartu / editor s kanonickou osou.
 */
export function renderTicksFromList(container, ticks, attempt = 0) {
    if (!(container instanceof HTMLElement) || !Array.isArray(ticks)) {
        return;
    }
    const containerWidth = Math.max(0, container.clientWidth);
    if (containerWidth <= 32 && attempt < 10) {
        // lazy tab / skrytý panel → šířka 0; zkusit po dalším frame.
        window.requestAnimationFrame(() => renderTicksFromList(container, ticks, attempt + 1));
        return;
    }

    container.replaceChildren();
    if (ticks.length === 0) {
        return;
    }

    ticks.forEach((tick, index) => {
        const leftPct = Math.max(0, Math.min(100, tick.left));
        const tickNode = document.createElement("span");
        tickNode.className = "timeline-axis-tick";
        if (index === 0) {
            tickNode.classList.add("edge");
        } else if (index === ticks.length - 1) {
            // Pravý krajní tick: gridline na 100 % by se kreslil za okrajem osy (overflow:hidden ho ořízne).
            // edge-end ho přes CSS posune o vlastní šířku dovnitř → zrcadlí levý první tick a je vidět.
            tickNode.classList.add("edge", "edge-end");
        }
        tickNode.style.left = `${leftPct}%`;
        // px pozice gridline (BEZ edge-insetu → zarovnáno s pruhy) pro layout popisků.
        tickNode.dataset.axisLeftPx = ((leftPct / 100) * containerWidth).toFixed(4);

        const labelNode = document.createElement("span");
        labelNode.className = "timeline-axis-label";
        labelNode.textContent = tick.label;
        tickNode.appendChild(labelNode);
        container.appendChild(tickNode);
    });

    layoutAxisLabels(container, containerWidth);
}

export function renderStaticTimelineAxes(scope) {
    const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
    root.querySelectorAll("[data-timeline-axis][data-axis-start][data-axis-end]").forEach((container) => {
        if (!(container instanceof HTMLElement)) {
            return;
        }

        // Kanonická osa: server dodal měsíční ticky → kreslíme je přímo (žádné vzorkování).
        const serverTicks = container.dataset.scheduleTicks;
        if (typeof serverTicks === "string" && serverTicks.length > 0) {
            renderTicksFromList(container, buildTicksFromServer(serverTicks));
            return;
        }

        // Fallback (např. breakdown osa bez serverových ticků) → původní vzorkovací cesta.
        const startDate = parseIsoDate(container.dataset.axisStart);
        const endDate = parseIsoDate(container.dataset.axisEnd);
        if (!(startDate instanceof Date) || !(endDate instanceof Date)) {
            return;
        }

        renderTimelineAxis(container, startDate, endDate);
    });
}
