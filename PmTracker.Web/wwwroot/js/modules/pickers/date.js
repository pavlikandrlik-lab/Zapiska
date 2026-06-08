/**
 * pickers/date.js — custom date picker.
 * Obsahuje: setAppDateFieldValue, closeAllDatePanels, initCustomDatePickers
 * a všechny interní helpery pro kalendářní panel, navigaci a synchronizaci hodnot.
 */
import {
    dateMonths,
    formatDisplayDate,
    formatIsoDate,
    isSameCalendarDate,
    parseDisplayDate,
    parseIsoDate
} from "../utils.js";
import {
    isInteractionInsideFloatingControl,
    mountFloatingPanel,
    positionFloatingPanel,
    unmountFloatingPanel
} from "../ui.js";
import { closeAllTimePanels } from "./time.js";

export function setAppDateFieldValue(valueInput, isoValue) {
    if (!(valueInput instanceof HTMLInputElement)) {
        return false;
    }

    const normalizedIso = typeof isoValue === "string" ? isoValue.trim() : "";
    const parsed = parseIsoDate(normalizedIso);
    if (!(parsed instanceof Date)) {
        return false;
    }

    const previous = valueInput.value || "";
    valueInput.value = normalizedIso;

    const dateField = valueInput.closest("[data-app-date-field]");
    const displayInput = dateField?.querySelector("[data-app-date-display]");
    if (displayInput instanceof HTMLInputElement) {
        displayInput.value = formatDisplayDate(parsed);
    }

    if (previous !== normalizedIso) {
        valueInput.dispatchEvent(new Event("change", { bubbles: true }));
    }

    return true;
}

export function closeAllDatePanels(exceptField) {
    document.querySelectorAll("[data-app-date-field]").forEach((candidate) => {
        if (!(candidate instanceof HTMLElement)) {
            return;
        }
        if (exceptField && candidate === exceptField) {
            return;
        }

        const panel = candidate.querySelector("[data-app-date-panel]");
        if (panel instanceof HTMLElement) {
            panel.hidden = true;
            unmountFloatingPanel(panel);
        }
    });
}

export function initCustomDatePickers(scope) {
    scope.querySelectorAll("[data-app-date-field]").forEach((field) => {
        if (!(field instanceof HTMLElement) || field.dataset.appDateReady === "true") {
            return;
        }

        const displayInput = field.querySelector("[data-app-date-display]");
        const valueInput = field.querySelector("[data-app-date-value]");
        const openButton = field.querySelector("[data-app-date-open]");
        const panel = field.querySelector("[data-app-date-panel]");
        const prevButton = field.querySelector("[data-app-date-prev]");
        const nextButton = field.querySelector("[data-app-date-next]");
        const monthSelect = field.querySelector("[data-app-date-month]");
        const yearSelect = field.querySelector("[data-app-date-year]");
        const grid = field.querySelector("[data-app-date-grid]");
        // FIX 2026-05-05: optional clear button (✕). Renderuje se jen když pm-date-field má clearable="true".
        const clearButton = field.querySelector("[data-app-date-clear]");

        if (!(displayInput instanceof HTMLInputElement)
            || !(valueInput instanceof HTMLInputElement)
            || !(openButton instanceof HTMLButtonElement)
            || !(panel instanceof HTMLElement)
            || !(prevButton instanceof HTMLButtonElement)
            || !(nextButton instanceof HTMLButtonElement)
            || !(monthSelect instanceof HTMLSelectElement)
            || !(yearSelect instanceof HTMLSelectElement)
            || !(grid instanceof HTMLElement)) {
            return;
        }

        field.dataset.appDateReady = "true";
        const isFieldLocked = () => field.dataset.appDateLocked === "true" || openButton.disabled;

        let selectedDate = parseIsoDate(valueInput.value) || parseDisplayDate(displayInput.value) || null;
        let viewDate = selectedDate ? new Date(selectedDate.getTime()) : new Date();

        // FIX 2026-05-05: udržuj data-app-date-has-value v sync s hidden input value, aby CSS
        // (a případně budoucí JS) mohlo reagovat (např. show/hide clear button).
        const updateHasValueFlag = () => {
            field.dataset.appDateHasValue = (valueInput.value && valueInput.value.length > 0) ? "true" : "false";
        };
        updateHasValueFlag();

        const syncValue = () => {
            const previous = valueInput.value;
            valueInput.value = selectedDate ? formatIsoDate(selectedDate) : "";
            displayInput.value = selectedDate ? formatDisplayDate(selectedDate) : "";
            updateHasValueFlag();
            if (previous !== valueInput.value) {
                valueInput.dispatchEvent(new Event("change", { bubbles: true }));
            }
        };

        // FIX 2026-05-05: clear button — vyprázdní hidden + display + dispatch change event,
        // takže form binder dostane "" → nullable DateOnly? = null. Server-side
        // RecordService.SaveRecord.ClearManualKrokyAsync to interpretuje jako explicit clear
        // pro manuální kroky 2/5/8/9 (PreferredZdroj=Manual + AbsolutniDatum=null).
        if (clearButton instanceof HTMLButtonElement) {
            clearButton.addEventListener("click", (event) => {
                event.preventDefault();
                if (isFieldLocked()) return;
                if (!valueInput.value && !displayInput.value) return;
                selectedDate = null;
                syncValue();
                // Zavři kalendářový panel pokud byl otevřený.
                if (!panel.hidden) {
                    panel.hidden = true;
                    unmountFloatingPanel(panel);
                }
            });
        }

        const ensureMonthOptions = () => {
            if (monthSelect.options.length > 0) {
                return;
            }

            dateMonths.forEach((month, index) => {
                const option = document.createElement("option");
                option.value = String(index);
                option.textContent = month;
                monthSelect.appendChild(option);
            });
        };

        const ensureYearOptions = (centerYear) => {
            const fromYear = centerYear - 20;
            const toYear = centerYear + 20;
            const currentFrom = Number.parseInt(yearSelect.dataset.fromYear || "", 10);
            const currentTo = Number.parseInt(yearSelect.dataset.toYear || "", 10);
            if (currentFrom === fromYear && currentTo === toYear) {
                return;
            }

            yearSelect.innerHTML = "";
            for (let year = fromYear; year <= toYear; year += 1) {
                const option = document.createElement("option");
                option.value = String(year);
                option.textContent = String(year);
                yearSelect.appendChild(option);
            }
            yearSelect.dataset.fromYear = String(fromYear);
            yearSelect.dataset.toYear = String(toYear);
        };

        const renderGrid = () => {
            ensureMonthOptions();
            ensureYearOptions(viewDate.getFullYear());

            monthSelect.value = String(viewDate.getMonth());
            yearSelect.value = String(viewDate.getFullYear());

            grid.innerHTML = "";
            const currentMonth = viewDate.getMonth();
            const currentYear = viewDate.getFullYear();
            const firstDayOfMonth = new Date(currentYear, currentMonth, 1);
            const mondayOffset = (firstDayOfMonth.getDay() + 6) % 7;
            const firstVisibleDate = new Date(currentYear, currentMonth, 1 - mondayOffset);
            const today = new Date();
            today.setHours(0, 0, 0, 0);

            for (let i = 0; i < 42; i += 1) {
                const dayDate = new Date(firstVisibleDate.getFullYear(), firstVisibleDate.getMonth(), firstVisibleDate.getDate() + i);
                const button = document.createElement("button");
                button.type = "button";
                button.className = "app-date-day";
                button.textContent = String(dayDate.getDate());
                button.dataset.iso = formatIsoDate(dayDate);
                button.setAttribute("role", "gridcell");

                if (dayDate.getMonth() !== currentMonth) {
                    button.classList.add("outside");
                }
                if (selectedDate && isSameCalendarDate(dayDate, selectedDate)) {
                    button.classList.add("selected");
                }
                if (isSameCalendarDate(dayDate, today)) {
                    button.title = "Dnes";
                }

                button.addEventListener("click", () => {
                    selectedDate = dayDate;
                    viewDate = new Date(dayDate.getFullYear(), dayDate.getMonth(), 1);
                    syncValue();
                    closePanel();
                });

                grid.appendChild(button);
            }

            if (!panel.hidden) {
                positionFloatingPanel(panel, field, panel._pmtrackerFloatingOptions || {
                    gap: 8,
                    flipVertical: true,
                    kind: "date"
                });
            }
        };

        const closePanel = () => {
            panel.hidden = true;
            unmountFloatingPanel(panel);
        };

        const openPanel = () => {
            if (isFieldLocked()) {
                return;
            }
            closeAllDatePanels(field);
            closeAllTimePanels();
            renderGrid();
            panel.hidden = false;
            mountFloatingPanel(panel, field, { gap: 8, flipVertical: true, kind: "date" });
        };

        syncValue();

        openButton.addEventListener("click", () => {
            if (panel.hidden) {
                openPanel();
            } else {
                closePanel();
            }
        });

        displayInput.addEventListener("click", () => {
            openPanel();
        });

        displayInput.addEventListener("focus", () => {
            openPanel();
        });

        displayInput.addEventListener("keydown", (event) => {
            if (event.key === "Enter" || event.key === "ArrowDown") {
                event.preventDefault();
                openPanel();
            }
        });

        prevButton.addEventListener("click", () => {
            viewDate = new Date(viewDate.getFullYear(), viewDate.getMonth() - 1, 1);
            renderGrid();
        });

        nextButton.addEventListener("click", () => {
            viewDate = new Date(viewDate.getFullYear(), viewDate.getMonth() + 1, 1);
            renderGrid();
        });

        monthSelect.addEventListener("change", () => {
            const month = Number.parseInt(monthSelect.value, 10);
            if (!Number.isFinite(month)) {
                return;
            }
            viewDate = new Date(viewDate.getFullYear(), month, 1);
            renderGrid();
        });

        yearSelect.addEventListener("change", () => {
            const year = Number.parseInt(yearSelect.value, 10);
            if (!Number.isFinite(year)) {
                return;
            }
            viewDate = new Date(year, viewDate.getMonth(), 1);
            renderGrid();
        });

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }
            if (!isInteractionInsideFloatingControl(target, field, panel)) {
                closePanel();
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                closePanel();
            }
        });

        displayInput.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                closePanel();
            }
        });
    });
}
