import {
    dateMonths,
    debounce,
    formatDisplayDate,
    formatIsoDate,
    formatTime,
    isButtonLike,
    isSameCalendarDate,
    normalizeSearchText,
    parseDisplayDate,
    parseIsoDate,
    parseIsoDateTime,
    parseJsonPayload,
    parseTimeValue,
    reportClientDiagnostic,
    scoreSearchCandidate,
    setButtonDisabled
} from "./utils.js";
import {
    isInteractionInsideFloatingControl,
    mountFloatingPanel,
    positionFloatingPanel,
    unmountFloatingPanel
} from "./ui.js";

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

        const syncValue = () => {
            const previous = valueInput.value;
            valueInput.value = selectedDate ? formatIsoDate(selectedDate) : "";
            displayInput.value = selectedDate ? formatDisplayDate(selectedDate) : "";
            if (previous !== valueInput.value) {
                valueInput.dispatchEvent(new Event("change", { bubbles: true }));
            }
        };

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

export function formatPersonEntryLabel(entry) {
    if (entry.email) {
        return `${entry.label} <${entry.email}>`;
    }
    return entry.label;
}

export function initSinglePersonPickers(scope) {
    scope.querySelectorAll('[data-person-picker="single"]').forEach((wrapper) => {
        if (!(wrapper instanceof HTMLElement) || wrapper.dataset.pickerReady === "true") {
            return;
        }

        const input = wrapper.querySelector("[data-person-picker-input]");
        const anchor = wrapper.querySelector("[data-floating-anchor]");
        let hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
        if (!(hiddenInput instanceof HTMLInputElement)) {
            const formScope = wrapper.closest("form");
            if (formScope instanceof HTMLFormElement) {
                const formHidden = formScope.querySelector("[data-person-picker-hidden]");
                if (formHidden instanceof HTMLInputElement) {
                    hiddenInput = formHidden;
                }
            }
        }
        const searchUrl = (wrapper.dataset.personPickerSearchUrl || "").trim();
        const hasRemoteSearch = searchUrl.length > 0;
        const source = wrapper.querySelector("[data-person-picker-source]");
        const panel = wrapper.querySelector("[data-person-picker-panel]");
        const results = wrapper.querySelector("[data-person-picker-results]");
        const message = wrapper.querySelector("[data-person-picker-message]");

        if (!(input instanceof HTMLInputElement)
            || !(anchor instanceof HTMLElement)
            || !(hiddenInput instanceof HTMLInputElement)
            || !(panel instanceof HTMLElement)
            || !(results instanceof HTMLElement)) {
            return;
        }
        if (!hasRemoteSearch && !(source instanceof HTMLElement)) {
            return;
        }

        wrapper.dataset.pickerReady = "true";
        input.placeholder = wrapper.dataset.personPickerPlaceholder || input.placeholder || "Vyhledejte osobu...";

        let entries = Array.from((source instanceof HTMLElement ? source.querySelectorAll("[data-id]") : []))
            .map((item) => {
                if (!(item instanceof HTMLElement)) {
                    return null;
                }

                return {
                    id: item.dataset.id || "",
                    label: (item.dataset.label || "").trim(),
                    email: (item.dataset.email || "").trim(),
                    org: (item.dataset.org || "").trim(),
                    unit: (item.dataset.unit || "").trim()
                };
            })
            .filter((entry) => entry && entry.id && entry.label);

        if (!hasRemoteSearch && entries.length === 0) {
            return;
        }

        let filtered = [];
        let activeIndex = -1;
        let remoteSearchVersion = 0;
        let activeRemoteSearchController = null;
        const minRemoteQueryLength = 2;
        const lockVerticalSide = anchor.closest("[data-modal-container]") instanceof HTMLElement
            && anchor.closest('form[data-record-editor-form="true"][data-record-editor-presentation="modal"]') instanceof HTMLElement;

        const closePanel = () => {
            panel.hidden = true;
            activeIndex = -1;
            unmountFloatingPanel(panel);
        };

        const openPanel = () => {
            panel.hidden = false;
            mountFloatingPanel(panel, anchor, {
                gap: 6,
                flipVertical: true,
                kind: "person-search",
                matchWidth: true,
                lockVerticalSide
            });
        };

        const setMessage = (text) => {
            if (!(message instanceof HTMLElement)) {
                return;
            }
            message.textContent = text;
        };

        const renderStatus = (text) => {
            results.innerHTML = "";
            const statusRow = document.createElement("button");
            statusRow.type = "button";
            statusRow.className = "office-search-item disabled";
            statusRow.disabled = true;
            statusRow.tabIndex = -1;

            const primary = document.createElement("span");
            primary.className = "office-search-primary";
            primary.textContent = text;
            statusRow.appendChild(primary);

            results.appendChild(statusRow);
            setMessage(text);
            openPanel();
        };

        const findEntryById = (id) => entries.find((entry) => entry.id === id) || null;
        const findEntryByInput = () => {
            const query = normalizeSearchText(input.value || "");
            if (!query) {
                return null;
            }

            const exactDisplay = entries.find((entry) =>
                normalizeSearchText(formatPersonEntryLabel(entry)) === query);
            if (exactDisplay) {
                return exactDisplay;
            }

            const exactLabel = entries.find((entry) => normalizeSearchText(entry.label) === query);
            if (exactLabel) {
                return exactLabel;
            }

            const exactEmail = entries.find((entry) => entry.email && normalizeSearchText(entry.email) === query);
            if (exactEmail) {
                return exactEmail;
            }

            return null;
        };

        const selectEntry = (entry, sourceKind = "user") => {
            hiddenInput.value = entry.id;
            input.value = formatPersonEntryLabel(entry);
            input.setCustomValidity("");
            setMessage(entry.email
                ? `Vybraná osoba: ${entry.label}, ${entry.email}`
                : `Vybraná osoba: ${entry.label}`);
            closePanel();

            wrapper.dispatchEvent(new CustomEvent("person-picker:selected", {
                bubbles: true,
                detail: {
                    source: sourceKind,
                    id: entry.id,
                    label: entry.label,
                    email: entry.email
                }
            }));
        };

        const render = () => {
            results.innerHTML = "";

            if (filtered.length === 0) {
                if (hasRemoteSearch) {
                    renderStatus(wrapper.dataset.personPickerEmpty || "Nenalezeny žádné odpovídající osoby.");
                } else {
                    setMessage(wrapper.dataset.personPickerEmpty || "Nenalezeny žádné odpovídající osoby.");
                    closePanel();
                }
                return;
            }

            filtered.forEach((entry, index) => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "office-search-item";
                button.setAttribute("role", "option");
                button.dataset.index = String(index);
                if (index === activeIndex) {
                    button.classList.add("active");
                }

                const primary = document.createElement("span");
                primary.className = "office-search-primary";
                primary.textContent = entry.label;
                button.appendChild(primary);

                const secondary = document.createElement("span");
                secondary.className = "office-search-secondary";
                const hasOrgOrUnit = Boolean(entry.org || entry.unit);
                if (entry.email && hasOrgOrUnit) {
                    const orgPart = `${entry.org || "-"} / ${entry.unit || "-"}`;
                    secondary.textContent = `${entry.email} | ${orgPart}`;
                    button.appendChild(secondary);
                } else if (entry.email) {
                    secondary.textContent = entry.email;
                    button.appendChild(secondary);
                } else if (hasOrgOrUnit) {
                    secondary.textContent = `${entry.org || "-"} / ${entry.unit || "-"}`;
                    button.appendChild(secondary);
                }

                results.appendChild(button);
            });

            openPanel();
        };

        const rank = (query) => {
            const normalized = normalizeSearchText(query);

            const ranked = entries
                .map((entry) => {
                    const searchable = `${entry.label} ${entry.email} ${entry.org} ${entry.unit}`;
                    const score = normalized ? scoreSearchCandidate(normalized, searchable) : 1;
                    return { entry, score };
                })
                .filter((row) => row.score > 0)
                .sort((a, b) => b.score - a.score || a.entry.label.localeCompare(b.entry.label, "cs"));

            return ranked.slice(0, 15).map((row) => row.entry);
        };

        const runSearch = () => {
            filtered = rank(input.value || "");
            activeIndex = filtered.length > 0 ? 0 : -1;
            hiddenInput.value = "";
            input.setCustomValidity("");
            render();
        };

        const normalizeRemoteEntries = (payload) => {
            const sourceEntries = Array.isArray(payload)
                ? payload
                : Array.isArray(payload?.results)
                    ? payload.results
                    : [];

            return sourceEntries
                .map((item) => {
                    if (!item || typeof item !== "object") {
                        return null;
                    }

                    const id = item.id ? String(item.id).trim() : "";
                    const label = typeof item.label === "string" ? item.label.trim() : "";
                    if (!id || !label) {
                        return null;
                    }

                    return {
                        id,
                        label,
                        email: typeof item.email === "string" ? item.email.trim() : "",
                        org: typeof item.organizace === "string" ? item.organizace.trim() : "",
                        unit: typeof item.organizacniCelek === "string" ? item.organizacniCelek.trim() : ""
                    };
                })
                .filter((entry) => entry && entry.id && entry.label);
        };

        const performRemoteSearch = async () => {
            const query = (input.value || "").trim();
            hiddenInput.value = "";
            input.setCustomValidity("");

            if (query.length < minRemoteQueryLength) {
                filtered = [];
                if (query.length === 0) {
                    results.innerHTML = "";
                    closePanel();
                } else {
                    renderStatus(`Zadejte alespoň ${minRemoteQueryLength} znaky.`);
                }
                return;
            }

            const requestVersion = ++remoteSearchVersion;
            activeRemoteSearchController?.abort();
            const controller = new AbortController();
            activeRemoteSearchController = controller;

            renderStatus("Vyhledávám...");

            try {
                const separator = searchUrl.includes("?") ? "&" : "?";
                const response = await fetch(`${searchUrl}${separator}q=${encodeURIComponent(query)}`, {
                    headers: {
                        "X-Requested-With": "XMLHttpRequest",
                        "Accept": "application/json"
                    },
                    credentials: "same-origin",
                    signal: controller.signal
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const payload = parseJsonPayload(await response.text());
                if (requestVersion !== remoteSearchVersion) {
                    return;
                }

                entries = normalizeRemoteEntries(payload);
                filtered = entries;
                activeIndex = filtered.length > 0 ? 0 : -1;
                render();
            } catch (error) {
                if (error instanceof DOMException && error.name === "AbortError") {
                    return;
                }

                if (requestVersion !== remoteSearchVersion) {
                    return;
                }

                reportClientDiagnostic("person-picker-search-failed", { searchUrl });
                renderStatus("Vyhledávání osob se nepodařilo.");
            }
        };

        const debouncedSearch = debounce(() => {
            if (hasRemoteSearch) {
                void performRemoteSearch();
                return;
            }

            runSearch();
        }, hasRemoteSearch ? 220 : 140);

        const initial = entries.find((entry) => entry.id === hiddenInput.value);
        if (initial && !input.value.trim()) {
            input.value = formatPersonEntryLabel(initial);
        }

        wrapper.addEventListener("person-picker:select-id", (event) => {
            if (!(event instanceof CustomEvent)) {
                return;
            }

            const requestedId = event.detail?.id ? String(event.detail.id) : "";
            if (!requestedId) {
                return;
            }

            const selected = findEntryById(requestedId);
            if (!selected) {
                return;
            }

            const sourceKind = typeof event.detail?.source === "string" ? event.detail.source : "auto";
            selectEntry(selected, sourceKind === "user" ? "user" : "auto");
        });

        input.addEventListener("input", () => {
            setMessage("Vyhledávám...");
            debouncedSearch();
        });

        input.addEventListener("focus", () => {
            if (hasRemoteSearch) {
                if (hiddenInput.value.trim() && input.value.trim()) {
                    return;
                }

                const query = (input.value || "").trim();
                if (query.length >= minRemoteQueryLength) {
                    void performRemoteSearch();
                } else if (!hiddenInput.value.trim()) {
                    renderStatus(`Zadejte alespoň ${minRemoteQueryLength} znaky.`);
                }
                return;
            }

            filtered = rank(input.value || "");
            activeIndex = filtered.length > 0 ? 0 : -1;
            render();
        });

        input.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                closePanel();
                return;
            }

            if (event.key === "ArrowDown") {
                event.preventDefault();
                if (filtered.length === 0) {
                    if (hasRemoteSearch) {
                        const query = (input.value || "").trim();
                        if (query.length >= minRemoteQueryLength) {
                            void performRemoteSearch();
                            return;
                        }
                    } else {
                        filtered = rank(input.value || "");
                    }
                }
                activeIndex = Math.min(activeIndex + 1, filtered.length - 1);
                render();
                return;
            }

            if (event.key === "ArrowUp") {
                event.preventDefault();
                activeIndex = Math.max(activeIndex - 1, 0);
                render();
                return;
            }

            if (event.key === "Enter" && activeIndex >= 0 && filtered[activeIndex]) {
                event.preventDefault();
                selectEntry(filtered[activeIndex]);
            }
        });

        results.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const button = target.closest("[data-index]");
            if (!(button instanceof HTMLElement)) {
                return;
            }

            const index = Number.parseInt(button.dataset.index || "-1", 10);
            if (!Number.isFinite(index) || index < 0 || index >= filtered.length) {
                return;
            }

            selectEntry(filtered[index]);
        });

        const form = wrapper.closest("form");
        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", (event) => {
                if (hiddenInput.value) {
                    input.setCustomValidity("");
                    return;
                }

                if (!hasRemoteSearch) {
                    const matchedEntry = findEntryByInput();
                    if (matchedEntry) {
                        selectEntry(matchedEntry, "auto");
                        return;
                    }
                }

                event.preventDefault();
                setMessage("Vyberte osobu ze seznamu výsledků.");
                input.setCustomValidity("Vyberte osobu ze seznamu výsledků.");
                input.reportValidity();
                input.focus();
            });
        }

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
                closePanel();
            }
        });
    });
}

export function initAdPersonPickers(scope) {
    scope.querySelectorAll("[data-ad-picker]").forEach((wrapper) => {
        if (!(wrapper instanceof HTMLElement) || wrapper.dataset.adPickerReady === "true") {
            return;
        }

        const searchUrl = wrapper.dataset.searchUrl || "";
        const form = wrapper.closest("form");
        const anchor = wrapper.querySelector("[data-floating-anchor]");
        const queryInput = wrapper.querySelector("[data-ad-query-input]");
        const queryHidden = form?.querySelector("[data-ad-query-hidden]");
        const guidInput = form?.querySelector("[data-ad-guid]");
        const adLoginInput = form?.querySelector("[data-ad-login]");
        const adCompanyInput = form?.querySelector("[data-ad-company]");
        const adDepartmentInput = form?.querySelector("[data-ad-department]");
        const jmenoInput = form?.querySelector("[data-ad-jmeno]");
        const prijmeniInput = form?.querySelector("[data-ad-prijmeni]");
        const titulInput = form?.querySelector("[data-ad-titul]");
        const emailInput = form?.querySelector("[data-ad-email]");
        const orgSelect = form?.querySelector("[data-ad-org-select]");
        const orgCreateHint = form?.querySelector("[data-ad-org-create-hint]");
        const orgUnitSelect = form?.querySelector("[data-ad-org-unit-select]");
        const orgUnitCreateHint = form?.querySelector("[data-ad-org-unit-create-hint]");
        const submitButton = form?.querySelector("[data-ad-submit]");
        const panel = wrapper.querySelector("[data-ad-search-panel]");
        const results = wrapper.querySelector("[data-ad-results]");

        if (!searchUrl
            || !(form instanceof HTMLFormElement)
            || !(anchor instanceof HTMLElement)
            || !(queryInput instanceof HTMLInputElement)
            || !(queryHidden instanceof HTMLInputElement)
            || !(guidInput instanceof HTMLInputElement)
            || !(adLoginInput instanceof HTMLInputElement)
            || !(adCompanyInput instanceof HTMLInputElement)
            || !(adDepartmentInput instanceof HTMLInputElement)
            || !(jmenoInput instanceof HTMLInputElement)
            || !(prijmeniInput instanceof HTMLInputElement)
            || !(titulInput instanceof HTMLInputElement)
            || !(emailInput instanceof HTMLInputElement)
            || !(orgSelect instanceof HTMLSelectElement)
            || !(orgCreateHint instanceof HTMLElement)
            || !(orgUnitSelect instanceof HTMLSelectElement)
            || !(orgUnitCreateHint instanceof HTMLElement)
            || !(panel instanceof HTMLElement)
            || !(results instanceof HTMLElement)
            || !isButtonLike(submitButton)) {
            return;
        }

        wrapper.dataset.adPickerReady = "true";
        let activeIndex = -1;
        let currentResults = [];
        let adAvailabilityKnown = false;
        let adIsUnavailable = false;
        const adUnavailableMessage = "Active Directory ACR není dostupné.";

        const closePanel = () => {
            panel.hidden = true;
            activeIndex = -1;
            unmountFloatingPanel(panel);
        };

        const clearSelection = () => {
            guidInput.value = "";
            adLoginInput.value = "";
            adCompanyInput.value = "";
            adDepartmentInput.value = "";
            jmenoInput.value = "";
            prijmeniInput.value = "";
            titulInput.value = "";
            emailInput.value = "";
            clearGeneratedOption(orgSelect, orgCreateHint);
            clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
            setButtonDisabled(submitButton, true);
        };

        const normalizeText = (value) => (value || "").toString().trim().toLowerCase();

        const clearGeneratedOption = (select, hint) => {
            Array.from(select.options)
                .filter((option) => option.dataset.generated === "true")
                .forEach((option) => option.remove());
            hint.hidden = true;
        };

        const ensureGeneratedOption = (select, hint, rawValue) => {
            clearGeneratedOption(select, hint);
            const normalized = (rawValue || "").toString().trim();
            if (!normalized) {
                return;
            }

            const option = document.createElement("option");
            option.value = `__new__:${normalized}`;
            option.textContent = `${normalized} (+ bude přidáno do číselníku)`;
            option.dataset.generated = "true";
            select.appendChild(option);
            select.value = option.value;
            hint.hidden = false;
        };

        const ensureGeneratedOrgUnitOption = (code, name) => {
            clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
            const normalizedCode = (code || "").toString().trim();
            const normalizedName = (name || "").toString().trim();
            if (!normalizedCode && !normalizedName) {
                return;
            }

            const option = document.createElement("option");
            const encoded = normalizedCode && normalizedName
                ? `${normalizedCode}|${normalizedName}`
                : normalizedCode || normalizedName;
            option.value = `__new__:${encoded}`;
            option.textContent = normalizedCode && normalizedName
                ? `${normalizedCode} - ${normalizedName} (+ bude přidáno do číselníku)`
                : `${encoded} (+ bude přidáno do číselníku)`;
            option.dataset.generated = "true";
            orgUnitSelect.appendChild(option);
            orgUnitSelect.value = option.value;
            orgUnitCreateHint.hidden = false;
        };

        const extractCodePrefix = (value) => {
            const text = (value || "").toString().trim();
            const hyphenIndex = text.indexOf("-");
            if (hyphenIndex < 2 || hyphenIndex > 6) {
                return null;
            }

            const prefix = text.slice(0, hyphenIndex).trim().toUpperCase();
            if (!/^[A-Z0-9]{2,8}$/.test(prefix)) {
                return null;
            }

            return prefix;
        };

        const findOptionByCodeOrText = (select, rawText, preferredCodes = []) => {
            const options = Array.from(select.options);
            const normalizedText = normalizeText(rawText);
            const codeFromText = extractCodePrefix(rawText);
            const normalizedCodes = preferredCodes
                .map((code) => normalizeText(code))
                .filter((code) => code);
            if (codeFromText) {
                normalizedCodes.unshift(normalizeText(codeFromText));
            }

            for (const normalizedCode of normalizedCodes) {
                const byCode = options.find((option) => normalizeText(option.value) === normalizedCode);
                if (byCode) {
                    return byCode.value;
                }
            }

            if (normalizedText) {
                const byText = options.find((option) => normalizeText(option.textContent).includes(normalizedText));
                if (byText) {
                    return byText.value;
                }
            }

            return null;
        };

        const parseCompanyLocation = (rawCompany) => {
            const company = (rawCompany || "").toString().trim();
            if (!company) {
                return {
                    organizationCode: null,
                    organizationName: null,
                    orgUnitCode: null,
                    orgUnitName: null,
                    source: ""
                };
            }

            const slashIndex = company.indexOf("/");
            const left = slashIndex >= 0 ? company.slice(0, slashIndex).trim() : company;
            const right = slashIndex >= 0 ? company.slice(slashIndex + 1).trim() : "";

            let organizationCode = null;
            let organizationName = left;
            const hyphenIndex = left.indexOf("-");
            if (hyphenIndex >= 2 && hyphenIndex <= 6) {
                const maybeCode = left.slice(0, hyphenIndex).trim().toUpperCase();
                if (/^[A-Z0-9]{2,8}$/.test(maybeCode)) {
                    organizationCode = maybeCode;
                    organizationName = left.slice(hyphenIndex + 1).trim();
                }
            }

            return {
                organizationCode,
                organizationName: organizationName || null,
                orgUnitCode: right || null,
                orgUnitName: organizationName || null,
                source: company
            };
        };

        const tryAutoSelectOrganization = (row) => {
            const parsed = parseCompanyLocation(row.company);
            const company = parsed.source;
            const normalizedCompany = normalizeText(company);
            const forcedCodes = [];
            if (parsed.organizationCode) {
                forcedCodes.push(parsed.organizationCode);
            }
            if (normalizedCompany.includes("ministerstvo obrany")
                || normalizedCompany.includes("armada ceske republiky")
                || normalizedCompany.includes("armáda české republiky")
                || normalizedCompany.includes("acr")) {
                forcedCodes.push("MO");
            } else if (normalizedCompany.includes("gordic")) {
                forcedCodes.push("DOD");
            }

            const selected = findOptionByCodeOrText(orgSelect, parsed.organizationName || company, forcedCodes);
            if (selected) {
                clearGeneratedOption(orgSelect, orgCreateHint);
                orgSelect.value = selected;
                return;
            }

            if (parsed.organizationName || company) {
                ensureGeneratedOption(orgSelect, orgCreateHint, parsed.organizationName || company);
            } else if (orgSelect.options.length > 0) {
                clearGeneratedOption(orgSelect, orgCreateHint);
                const fallback = findOptionByCodeOrText(orgSelect, "", ["MO"]);
                orgSelect.value = fallback || orgSelect.options[0].value;
            }
        };

        const tryAutoSelectOrgUnit = (row) => {
            const parsedCompany = parseCompanyLocation(row.company);
            const departmentRaw = (row.department || "").toString().trim();
            const preferredCodes = [];
            if (parsedCompany.orgUnitCode) {
                preferredCodes.push(parsedCompany.orgUnitCode);
            }
            if (departmentRaw && /^[0-9]+$/.test(departmentRaw)) {
                preferredCodes.push(departmentRaw);
            }

            const selected = findOptionByCodeOrText(
                orgUnitSelect,
                parsedCompany.orgUnitName || departmentRaw,
                preferredCodes);
            if (selected) {
                clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                orgUnitSelect.value = selected;
                return;
            }

            const generatedCode = parsedCompany.orgUnitCode || (/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
            const generatedName = parsedCompany.orgUnitName || (!/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
            if (generatedCode || generatedName) {
                ensureGeneratedOrgUnitOption(generatedCode, generatedName);
            } else {
                clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                orgUnitSelect.value = "";
            }
        };

        const renderStatusRow = (text, type = "info") => {
            currentResults = [];
            activeIndex = -1;
            results.innerHTML = "";

            const status = document.createElement("div");
            status.className = `office-search-info ${type}`.trim();
            status.textContent = text;
            status.setAttribute("role", "status");
            status.setAttribute("aria-live", "polite");
            results.appendChild(status);
            panel.hidden = false;
            mountFloatingPanel(panel, anchor, {
                gap: 6,
                flipVertical: true,
                kind: "ad-search",
                matchWidth: true
            });
        };

        const fillFromResult = (row) => {
            if (!row.canSelect) {
                renderStatusRow(row.disabledReason || "Tuto osobu nelze vybrat.", "error");
                return;
            }

            guidInput.value = row.guidAd || "";
            adLoginInput.value = row.adLogin || "";
            adCompanyInput.value = row.company || "";
            adDepartmentInput.value = row.department || "";
            jmenoInput.value = row.jmeno || "";
            prijmeniInput.value = row.prijmeni || "";
            titulInput.value = row.titul || "";
            emailInput.value = row.email || "";
            tryAutoSelectOrganization(row);
            tryAutoSelectOrgUnit(row);
            queryHidden.value = queryInput.value.trim();
            queryInput.value = row.email ? `${row.displayName} <${row.email}>` : row.displayName;
            setButtonDisabled(submitButton, false);
            closePanel();
        };

        const renderResults = () => {
            results.innerHTML = "";

            if (currentResults.length === 0) {
                closePanel();
                return;
            }

            currentResults.forEach((row, index) => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "office-search-item";
                button.dataset.index = String(index);
                button.setAttribute("role", "option");
                if (index === activeIndex) {
                    button.classList.add("active");
                }
                if (!row.canSelect) {
                    button.classList.add("disabled");
                    button.disabled = true;
                }

                const primary = document.createElement("span");
                primary.className = "office-search-primary";
                primary.textContent = row.displayName || `${row.jmeno || ""} ${row.prijmeni || ""}`.trim();
                button.appendChild(primary);

                const secondary = document.createElement("span");
                secondary.className = "office-search-secondary";
                const orgPart = `${row.company || "-"} / ${row.department || "-"}`;
                secondary.textContent = row.email ? `${row.email} | ${orgPart}` : orgPart;
                button.appendChild(secondary);

                if (!row.canSelect && row.disabledReason) {
                    const reason = document.createElement("span");
                    reason.className = "office-search-warning";
                    reason.textContent = row.disabledReason;
                    button.appendChild(reason);
                }

                results.appendChild(button);
            });

            panel.hidden = false;
            mountFloatingPanel(panel, anchor, {
                gap: 6,
                flipVertical: true,
                kind: "ad-search",
                matchWidth: true
            });
        };

        const performSearch = async () => {
            const query = queryInput.value.trim();
            queryHidden.value = query;
            clearSelection();

            if (query.length < 1) {
                if (adIsUnavailable) {
                    renderStatusRow(adUnavailableMessage, "error");
                } else {
                    results.innerHTML = "";
                    closePanel();
                }
                return;
            }

            renderStatusRow("Vyhledávám v Active Directory...");

            try {
                const response = await fetch(`${searchUrl}?q=${encodeURIComponent(query)}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const payload = parseJsonPayload(await response.text());
                if (!payload || typeof payload !== "object") {
                    throw new Error("INVALID_AD_PAYLOAD");
                }
                if (!payload.available) {
                    adAvailabilityKnown = true;
                    adIsUnavailable = true;
                    renderStatusRow(payload.message || adUnavailableMessage, "error");
                    return;
                }

                adAvailabilityKnown = true;
                adIsUnavailable = false;
                currentResults = Array.isArray(payload.results) ? payload.results.slice(0, 5) : [];
                activeIndex = currentResults.length > 0 ? 0 : -1;
                if (currentResults.length > 0) {
                    renderResults();
                } else {
                    renderStatusRow("Žádná shoda.", "empty");
                }
            } catch {
                adAvailabilityKnown = true;
                adIsUnavailable = true;
                renderStatusRow(adUnavailableMessage, "error");
                reportClientDiagnostic("ad-search-failed", { searchUrl });
            }
        };

        const debouncedAdSearch = debounce(performSearch, 220);

        const probeAvailability = async () => {
            if (adAvailabilityKnown) {
                if (adIsUnavailable) {
                    renderStatusRow(adUnavailableMessage, "error");
                }
                return;
            }

            try {
                const response = await fetch(`${searchUrl}?q=${encodeURIComponent("__pmtracker_probe__")}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const payload = parseJsonPayload(await response.text());
                if (!payload || typeof payload !== "object") {
                    throw new Error("INVALID_AD_PROBE_PAYLOAD");
                }
                adAvailabilityKnown = true;
                adIsUnavailable = !payload.available;
                if (adIsUnavailable) {
                    renderStatusRow(payload.message || adUnavailableMessage, "error");
                }
            } catch {
                adAvailabilityKnown = true;
                adIsUnavailable = true;
                renderStatusRow(adUnavailableMessage, "error");
                reportClientDiagnostic("ad-probe-failed", { searchUrl });
            }
        };

        queryInput.addEventListener("input", () => {
            debouncedAdSearch();
        });

        queryInput.addEventListener("focus", () => {
            probeAvailability();
            if (currentResults.length > 0) {
                panel.hidden = false;
                mountFloatingPanel(panel, anchor, {
                    gap: 6,
                    flipVertical: true,
                    kind: "ad-search",
                    matchWidth: true
                });
            }
        });

        queryInput.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                event.preventDefault();
                event.stopPropagation();
                closePanel();
                return;
            }

            if (event.key === "ArrowDown") {
                if (currentResults.length === 0) {
                    return;
                }
                event.preventDefault();
                activeIndex = Math.min(activeIndex + 1, currentResults.length - 1);
                renderResults();
                return;
            }

            if (event.key === "ArrowUp") {
                if (currentResults.length === 0) {
                    return;
                }
                event.preventDefault();
                activeIndex = Math.max(activeIndex - 1, 0);
                renderResults();
                return;
            }

            if (event.key === "Enter" && activeIndex >= 0 && currentResults[activeIndex]) {
                event.preventDefault();
                fillFromResult(currentResults[activeIndex]);
            }
        });

        results.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const button = target.closest("[data-index]");
            if (!(button instanceof HTMLElement)) {
                return;
            }

            const index = Number.parseInt(button.dataset.index || "-1", 10);
            if (!Number.isFinite(index) || index < 0 || index >= currentResults.length) {
                return;
            }

            fillFromResult(currentResults[index]);
        });

        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", (event) => {
                if (guidInput.value && !submitButton.disabled) {
                    return;
                }

                event.preventDefault();
                renderStatusRow("Nejprve vyberte osobu z AD výsledků.", "empty");
                queryInput.focus();
            });
        }

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
                closePanel();
            }
        });
    });
}

export function initCollabPickers(scope) {
    scope.querySelectorAll(".collab-picker").forEach((wrapper) => {
        if (!(wrapper instanceof HTMLElement) || wrapper.dataset.collabPickerReady === "true") {
            return;
        }

        const search = wrapper.querySelector("[data-collab-search]");
        const optionsContainer = wrapper.querySelector("[data-collab-options]");
        if (!(search instanceof HTMLInputElement) || !(optionsContainer instanceof HTMLElement)) {
            return;
        }

        wrapper.dataset.collabPickerReady = "true";
        const options = Array.from(optionsContainer.querySelectorAll("[data-collab-option]"))
            .filter((item) => item instanceof HTMLElement);

        options.forEach((item, index) => {
            item.dataset.collabOrder = String(index);
        });

        const applySearch = () => {
            const query = search.value || "";
            const normalizedQuery = normalizeSearchText(query);

            const scored = options
                .map((option) => {
                    const label = option.dataset.collabLabel || option.textContent || "";
                    const score = normalizedQuery ? scoreSearchCandidate(normalizedQuery, label) : 1;
                    const order = Number.parseInt(option.dataset.collabOrder || "0", 10);
                    return { option, score, order };
                })
                .sort((a, b) => {
                    if (!normalizedQuery) {
                        return a.order - b.order;
                    }
                    return b.score - a.score || a.order - b.order;
                });

            scored.forEach((row) => {
                row.option.hidden = normalizedQuery.length > 0 && row.score <= 0;
                optionsContainer.appendChild(row.option);
            });
        };

        const debouncedApply = debounce(applySearch, 120);
        search.addEventListener("input", () => {
            debouncedApply();
        });

        applySearch();
    });
}
