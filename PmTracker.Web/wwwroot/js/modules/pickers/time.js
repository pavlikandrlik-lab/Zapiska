/**
 * pickers/time.js — custom time picker.
 * Obsahuje: closeAllTimePanels, initCustomTimePickers.
 */
import {
    formatTime,
    parseIsoDateTime,
    parseTimeValue
} from "../utils.js";
import {
    isInteractionInsideFloatingControl,
    mountFloatingPanel,
    positionFloatingPanel,
    unmountFloatingPanel
} from "../ui.js";
import { closeAllDatePanels } from "./date.js";

export function closeAllTimePanels(exceptField) {
    document.querySelectorAll("[data-app-time-field]").forEach((candidate) => {
        if (!(candidate instanceof HTMLElement)) {
            return;
        }
        if (exceptField && candidate === exceptField) {
            return;
        }

        const panel = candidate.querySelector("[data-app-time-panel]");
        if (panel instanceof HTMLElement) {
            panel.hidden = true;
            unmountFloatingPanel(panel);
        }
    });
}

export function initCustomTimePickers(scope) {
    scope.querySelectorAll("[data-app-time-field]").forEach((field) => {
        if (!(field instanceof HTMLElement) || field.dataset.appTimeReady === "true") {
            return;
        }

        const displayInput = field.querySelector("[data-app-time-display]");
        const valueInput = field.querySelector("[data-app-time-value]");
        const openButton = field.querySelector("[data-app-time-open]");
        const panel = field.querySelector("[data-app-time-panel]");
        const grid = field.querySelector("[data-app-time-grid]");
        if (!(displayInput instanceof HTMLInputElement)
            || !(valueInput instanceof HTMLInputElement)
            || !(openButton instanceof HTMLButtonElement)
            || !(panel instanceof HTMLElement)
            || !(grid instanceof HTMLElement)) {
            return;
        }

        field.dataset.appTimeReady = "true";
        const isLocked = field.dataset.appTimeLocked === "true" || openButton.disabled;
        const form = field.closest("form");

        const initialDateTime = parseIsoDateTime(valueInput.value);
        const initialTime = parseTimeValue(displayInput.value)
            || parseTimeValue(valueInput.value)
            || (initialDateTime ? { hours: initialDateTime.getHours(), minutes: initialDateTime.getMinutes() } : null);
        let selected = initialTime || { hours: new Date().getHours(), minutes: new Date().getMinutes() };

        const syncValue = () => {
            const normalizedTime = formatTime(selected.hours, selected.minutes);
            displayInput.value = normalizedTime;
            valueInput.value = normalizedTime;
        };

        const closePanel = () => {
            panel.hidden = true;
            unmountFloatingPanel(panel);
        };

        const render = () => {
            grid.innerHTML = "";

            for (let hour = 6; hour <= 22; hour += 1) {
                for (let minute = 0; minute < 60; minute += 15) {
                    const timeText = formatTime(hour, minute);
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "app-time-option";
                    button.textContent = timeText;
                    button.dataset.time = timeText;
                    button.setAttribute("role", "option");
                    button.setAttribute("aria-selected", String(selected.hours === hour && selected.minutes === minute));
                    if (selected.hours === hour && selected.minutes === minute) {
                        button.classList.add("selected");
                    }

                    button.addEventListener("click", () => {
                        selected = { hours: hour, minutes: minute };
                        syncValue();
                        closePanel();
                    });

                    grid.appendChild(button);
                }
            }

            if (!panel.hidden) {
                positionFloatingPanel(panel, field, panel._pmtrackerFloatingOptions || {
                    gap: 8,
                    flipVertical: true,
                    kind: "time"
                });
            }
        };

        const openPanel = () => {
            if (isLocked) {
                return;
            }
            closeAllDatePanels();
            closeAllTimePanels(field);
            render();
            panel.hidden = false;
            mountFloatingPanel(panel, field, { gap: 8, flipVertical: true, kind: "time" });
        };

        syncValue();

        openButton.addEventListener("click", () => {
            if (panel.hidden) {
                openPanel();
            } else {
                closePanel();
            }
        });

        displayInput.addEventListener("mousedown", (event) => {
            event.preventDefault();
            openPanel();
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
            } else if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                closePanel();
            }
        });

        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", () => {
                syncValue();
            });
        }

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
    });
}
