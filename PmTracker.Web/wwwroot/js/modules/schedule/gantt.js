/**
 * schedule/gantt.js — ProjectGanttBoard class + gantt filter application + axis update.
 *
 * Fáze 3B Task 2: vyextrahováno ze schedule.js (1697 LOC).
 * Obsahuje: ProjectGanttBoard class, applyProjectGanttFilters, updateProjectGanttAxis.
 */

import {
    normalizeFilterToken,
    parseIsoDate
} from "../utils.js";
import {
    getProjectFilterCurrentUserId,
    getProjectFilterInput,
    setRecordFilterVisibility
} from "../filters.js";
import { queueRainbowSegmentRender } from "../ui.js";
import { renderTimelineAxis } from "./timeline.js";

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
