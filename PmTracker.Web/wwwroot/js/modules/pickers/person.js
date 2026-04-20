/**
 * pickers/person.js — single person picker, collab multi-select picker a formatting.
 * Obsahuje: formatPersonEntryLabel, initSinglePersonPickers, initCollabPickers.
 * Sdílené helpery (formatPersonEntryLabel) jsou re-exportovány pro adPerson.js.
 */
import {
    debounce,
    normalizeSearchText,
    parseJsonPayload,
    reportClientDiagnostic,
    scoreSearchCandidate
} from "../utils.js";
import {
    isInteractionInsideFloatingControl,
    mountFloatingPanel,
    unmountFloatingPanel
} from "../ui.js";

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
