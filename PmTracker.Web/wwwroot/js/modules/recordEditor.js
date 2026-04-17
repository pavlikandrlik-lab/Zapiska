import { parseJsonPayload, reportClientDiagnostic } from "./utils.js";
import { closeModal, isModalOpen, openUrlModal, modalState } from "./modals.js";
import { closeAllFloatingPanels, positionPrintChooser, queueRainbowSegmentRender } from "./ui.js";
import {
    initAdPersonPickers,
    initCollabPickers,
    initCustomDatePickers,
    initCustomTimePickers,
    initSinglePersonPickers,
    setAppDateFieldValue
} from "./pickers.js";
import { buildRecordUiState, restoreRecordUiState } from "./navigation.js";
import { initConfirmSubmitToggles } from "./ajax.js";
import { initRecordSchedulePlanner, queueRecordSchedulePlannerRecalc } from "./schedule.js";

const modalRoot = document.getElementById("modal-root");
const recordEditorPreferenceStorageKey = "pmtracker.recordEditor.preference";
const recordEditorReturnStateStoragePrefix = "pmtracker.recordEditor.returnState.project.";
const recordEditorDraftStoragePrefix = "pmtracker.recordEditor.draft.";
const recordEditorDraftTtlMs = 12 * 60 * 60 * 1000;

export const recordEditorState = {
    chooser: null,
    chooserTrigger: null,
    closeGuard: null,
    closeGuardTrigger: null
};

export function getRecordEditorPreferenceLabel(mode) {
    if (mode === "modal") {
        return "Otevřít v modalu";
    }

    if (mode === "page") {
        return "Otevřít na stránce";
    }

    return "není nastaveno";
}

export function getStoredRecordEditorPreference() {
    const value = localStorage.getItem(recordEditorPreferenceStorageKey);
    if (value === "modal" || value === "page") {
        return value;
    }

    return null;
}

export function setStoredRecordEditorPreference(mode) {
    if (mode !== "modal" && mode !== "page") {
        return;
    }

    localStorage.setItem(recordEditorPreferenceStorageKey, mode);
    refreshRecordEditorPreferenceUi();
}

export function clearStoredRecordEditorPreference() {
    localStorage.removeItem(recordEditorPreferenceStorageKey);
    refreshRecordEditorPreferenceUi();
}

export function refreshRecordEditorPreferenceUi() {
    const preferred = getStoredRecordEditorPreference();
    document.querySelectorAll("[data-record-editor-preference-current]").forEach((element) => {
        element.textContent = getRecordEditorPreferenceLabel(preferred);
    });

    document.querySelectorAll("[data-record-editor-preference-reset]").forEach((element) => {
        if (element instanceof HTMLButtonElement) {
            element.disabled = preferred === null;
        }
    });
}

export function getCurrentLocalUrl() {
    return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

export function getRecordEditorReturnStateKey(projectId) {
    return `${recordEditorReturnStateStoragePrefix}${projectId}`;
}

export function closeRecordEditorChooser(options) {
    const settings = options || {};
    const restoreFocus = Boolean(settings.restoreFocus);
    const trigger = recordEditorState.chooserTrigger;

    if (recordEditorState.chooser instanceof HTMLElement) {
        recordEditorState.chooser.remove();
    }

    recordEditorState.chooser = null;
    recordEditorState.chooserTrigger = null;

    if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
        trigger.focus({ preventScroll: true });
    }
}

export function buildRecordEditorUrl(trigger, mode) {
    if (!(trigger instanceof HTMLElement)) {
        return "";
    }

    const rawUrl = trigger.getAttribute("data-record-editor-url") || "";
    if (!rawUrl) {
        return "";
    }

    const editorUrl = new URL(rawUrl, window.location.origin);
    editorUrl.searchParams.set("presentation", mode === "page" ? "page" : "modal");
    editorUrl.searchParams.set("returnUrl", getCurrentLocalUrl());
    return `${editorUrl.pathname}${editorUrl.search}${editorUrl.hash}`;
}

export function captureRecordEditorReturnState(trigger) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    const projectId = Number.parseInt(trigger.getAttribute("data-record-editor-project-id") || "", 10);
    if (!Number.isInteger(projectId) || projectId <= 0) {
        return;
    }

    const scopeRoot = document.querySelector(`[data-project-detail-root][data-project-id="${CSS.escape(String(projectId))}"]`)
        || document.querySelector("[data-project-detail-root]");
    const baseState = buildRecordUiState(scopeRoot instanceof HTMLElement ? scopeRoot : document);
    const state = {
        projectId,
        returnUrl: getCurrentLocalUrl(),
        activeTab: baseState.activeTab,
        scrollY: baseState.scrollY,
        expandedRecordIds: Array.isArray(baseState.expandedRecordIds) ? baseState.expandedRecordIds : [],
        commentSortDirectionByRecordId: baseState.commentSortDirectionByRecordId || {},
        capturedAt: new Date().toISOString()
    };

    sessionStorage.setItem(getRecordEditorReturnStateKey(projectId), JSON.stringify(state));
}

export function navigateToRecordEditorPage(trigger) {
    const targetUrl = buildRecordEditorUrl(trigger, "page");
    if (!targetUrl) {
        return;
    }

    captureRecordEditorReturnState(trigger);
    window.location.assign(targetUrl);
}

export function handleRecordEditorChoice(trigger, mode, shouldSkipRemember) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    if (!shouldSkipRemember) {
        setStoredRecordEditorPreference(mode);
    }

    if (mode === "page") {
        navigateToRecordEditorPage(trigger);
        return;
    }

    openUrlModal(buildRecordEditorUrl(trigger, "modal"), trigger);
}

export function createRecordEditorChooser(trigger) {
    const label = trigger.getAttribute("data-record-editor-label") || "Editor záznamu";

    const popover = document.createElement("div");
    popover.className = "record-editor-popover";
    popover.setAttribute("role", "dialog");
    popover.setAttribute("aria-modal", "false");
    popover.setAttribute("data-record-editor-popover", "true");
    popover.setAttribute("tabindex", "-1");

    const title = document.createElement("h3");
    title.className = "record-editor-popover-title";
    title.textContent = "Vyberte způsob otevření";
    popover.appendChild(title);

    const subtitle = document.createElement("p");
    subtitle.className = "record-editor-popover-subtitle";
    subtitle.textContent = label;
    popover.appendChild(subtitle);

    const actions = document.createElement("div");
    actions.className = "record-editor-popover-actions";

    const modalButton = document.createElement("button");
    modalButton.type = "button";
    modalButton.className = "btn small";
    modalButton.textContent = "Otevřít v modalu";
    modalButton.setAttribute("data-record-editor-mode", "modal");
    actions.appendChild(modalButton);

    const pageButton = document.createElement("button");
    pageButton.type = "button";
    pageButton.className = "btn small";
    pageButton.textContent = "Otevřít na stránce";
    pageButton.setAttribute("data-record-editor-mode", "page");
    actions.appendChild(pageButton);

    popover.appendChild(actions);

    const rememberLabel = document.createElement("label");
    rememberLabel.className = "record-editor-popover-remember";
    const rememberCheckbox = document.createElement("input");
    rememberCheckbox.type = "checkbox";
    rememberCheckbox.setAttribute("data-record-editor-remember", "true");
    rememberLabel.appendChild(rememberCheckbox);
    rememberLabel.append(" Neukládat pro tentokrát jako výchozí volbu");
    popover.appendChild(rememberLabel);

    const note = document.createElement("p");
    note.className = "record-editor-popover-note";
    note.textContent = "Pokud volbu neuložíte, systém se při dalším otevření zeptá znovu.";
    popover.appendChild(note);

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "record-editor-popover-close";
    closeButton.setAttribute("aria-label", "Zavřít výběr způsobu otevření editoru");
    closeButton.textContent = "×";
    popover.appendChild(closeButton);

    popover.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (target.closest(".record-editor-popover-close")) {
            event.preventDefault();
            closeRecordEditorChooser({ restoreFocus: true });
            return;
        }

        const choice = target.closest("[data-record-editor-mode]");
        if (!choice) {
            return;
        }

        event.preventDefault();
        const mode = choice.getAttribute("data-record-editor-mode");
        if (mode !== "modal" && mode !== "page") {
            return;
        }

        const skipRemember = rememberCheckbox.checked;
        closeRecordEditorChooser({ restoreFocus: false });
        handleRecordEditorChoice(trigger, mode, skipRemember);
    });

    return popover;
}

export function showRecordEditorChooser(trigger) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    closeRecordEditorChooser({ restoreFocus: false });

    const popover = createRecordEditorChooser(trigger);
    document.body.appendChild(popover);
    positionPrintChooser(popover, trigger);
    recordEditorState.chooser = popover;
    recordEditorState.chooserTrigger = trigger;

    const firstAction = popover.querySelector("[data-record-editor-mode]");
    if (firstAction instanceof HTMLElement) {
        firstAction.focus({ preventScroll: true });
    } else {
        popover.focus({ preventScroll: true });
    }
}

export function openRecordEditor(trigger, forcedMode) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    const mode = forcedMode || getStoredRecordEditorPreference();
    if (mode === "modal") {
        openUrlModal(buildRecordEditorUrl(trigger, "modal"), trigger);
        return;
    }

    if (mode === "page") {
        navigateToRecordEditorPage(trigger);
        return;
    }

    showRecordEditorChooser(trigger);
}

export function restoreRecordEditorReturnStateFromUrl() {
    const projectRoot = document.querySelector("[data-project-detail-root]");
    if (!(projectRoot instanceof HTMLElement)) {
        return;
    }

    const currentUrl = new URL(window.location.href);
    if (currentUrl.searchParams.get("restoreRecordEditorState") !== "1") {
        return;
    }

    const cleanupUrl = () => {
        currentUrl.searchParams.delete("restoreRecordEditorState");
        history.replaceState(history.state || {}, "", `${currentUrl.pathname}${currentUrl.search}${currentUrl.hash}`);
    };

    const projectId = Number.parseInt(projectRoot.dataset.projectId || "", 10);
    if (!Number.isInteger(projectId) || projectId <= 0) {
        cleanupUrl();
        return;
    }

    const storageKey = getRecordEditorReturnStateKey(projectId);
    const rawState = sessionStorage.getItem(storageKey);
    if (!rawState) {
        cleanupUrl();
        return;
    }

    try {
        const state = JSON.parse(rawState);
        restoreRecordUiState(state);
    } catch {
        reportClientDiagnostic("record-editor-return-state-invalid", { projectId });
    } finally {
        sessionStorage.removeItem(storageKey);
        cleanupUrl();
    }
}

export function initPermissionMetadataBindings(scope) {
    const root = scope instanceof Element ? scope : document;
    const forms = root.querySelectorAll("form");
    forms.forEach((form) => {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const keySelect = form.querySelector("[data-authz-permission-key]");
        const categorySelect = form.querySelector("[data-authz-permission-category]");
        const scopeSelect = form.querySelector("[data-authz-permission-scope]");
        const help = form.querySelector("[data-authz-permission-help]");

        if (!(keySelect instanceof HTMLSelectElement) ||
            !(categorySelect instanceof HTMLSelectElement) ||
            !(scopeSelect instanceof HTMLSelectElement)) {
            return;
        }

        if (keySelect.dataset.authzPermissionBound === "true") {
            return;
        }
        keySelect.dataset.authzPermissionBound = "true";

        const applyCatalogMetadata = () => {
            const selectedOption = keySelect.selectedOptions.length > 0
                ? keySelect.selectedOptions[0]
                : null;
            if (!(selectedOption instanceof HTMLOptionElement)) {
                return;
            }

            const categoryId = (selectedOption.dataset.categoryId || "").trim();
            const categoryKod = (selectedOption.dataset.categoryKod || "").trim();
            const scopeLevel = (selectedOption.dataset.scopeLevel || "").trim().toUpperCase();
            const description = (selectedOption.dataset.description || "").trim();

            if (categoryId) {
                categorySelect.value = categoryId;
            }
            if (scopeLevel === "GLOBAL" || scopeLevel === "PROJECT") {
                scopeSelect.value = scopeLevel;
            }

            if (help instanceof HTMLElement) {
                if (description || categoryKod || scopeLevel) {
                    const fragments = [];
                    if (description) {
                        fragments.push(description);
                    }
                    if (categoryKod) {
                        fragments.push(`Kategorie: ${categoryKod}`);
                    }
                    if (scopeLevel) {
                        fragments.push(`Rozsah: ${scopeLevel}`);
                    }
                    help.textContent = fragments.join(" | ");
                } else {
                    help.textContent = "";
                }
            }
        };

        keySelect.addEventListener("change", applyCatalogMetadata);
        categorySelect.addEventListener("change", applyCatalogMetadata);
        scopeSelect.addEventListener("change", applyCatalogMetadata);
        applyCatalogMetadata();
    });
}

export function updateTaskTypeVisibility(categorySelect) {
    if (!(categorySelect instanceof HTMLSelectElement)) {
        return;
    }

    const form = categorySelect.closest("form");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const topRow = form.querySelector("[data-record-row-top]");
    const typeRow = form.querySelector("[data-typ-ukolu-row]");
    const selectedLabel = categorySelect.selectedIndex >= 0
        ? (categorySelect.options[categorySelect.selectedIndex]?.textContent || "")
        : "";
    const normalized = `${categorySelect.value || ""} ${selectedLabel}`.toLowerCase();
    const isTask = normalized.includes("úkol") || normalized.includes("ukol");
    const scheduleTab = form.querySelector("[data-record-schedule-tab]");
    const schedulePanel = form.querySelector("[data-record-schedule-panel]");
    const scheduleNote = form.querySelector("[data-record-schedule-note]");
    // V proposal editoru (návrh úpravy harmonogramu) je základní metadata
    // server-side zamčená (AllowBasicMetadataEdit=false). JS NESMÍ toto
    // serverové zámek odemknout, i když je záznam kategorie Úkol.
    // Viz docs/specs/record-proposal-editor.md.
    const metadataLocked = form.dataset.metadataLocked === "true";

    if (topRow instanceof HTMLElement) {
        topRow.dataset.hasType = isTask ? "true" : "false";
    }

    if (!(typeRow instanceof HTMLElement)) {
        return;
    }

    const typeSelect = typeRow.querySelector("select");
    typeRow.hidden = !isTask;
    if (typeSelect instanceof HTMLSelectElement) {
        typeSelect.disabled = !isTask || metadataLocked;
        if (!isTask) {
            typeSelect.value = "";
        }
    }

    if (scheduleTab instanceof HTMLElement) {
        scheduleTab.hidden = !isTask;
    }

    if (scheduleNote instanceof HTMLElement) {
        scheduleNote.hidden = isTask;
    }

    if (schedulePanel instanceof HTMLElement) {
        const schedulePermissionMode = (form.dataset.schedulePermissionMode || "full").toLowerCase();
        const canScheduleEditFull = schedulePermissionMode === "full";
        const canScheduleAddOnly = schedulePermissionMode === "add";
        const canScheduleAny = canScheduleEditFull || canScheduleAddOnly;
        const canEditDurationInput = (input) => {
            if (!(input instanceof HTMLInputElement)) {
                return false;
            }

            if (input.dataset.scheduleStaticDisabled === "true") {
                return false;
            }

            if (canScheduleEditFull) {
                return true;
            }

            if (!canScheduleAddOnly) {
                return false;
            }

            const originalDuration = Number.parseInt((input.dataset.scheduleOriginalDuration || "").trim(), 10);
            return !Number.isFinite(originalDuration) || originalDuration <= 0;
        };
        const syncStepperButtons = (buttonSelector, inputSelector) => {
            form.querySelectorAll(buttonSelector)
                .forEach((button) => {
                    if (!(button instanceof HTMLButtonElement)) {
                        return;
                    }

                    const row = button.closest("[data-schedule-step-row]");
                    const input = row?.querySelector(inputSelector);
                    button.disabled = !(input instanceof HTMLInputElement) || input.disabled;
                });
        };

        const setScheduleDateFieldState = () => {
            form.querySelectorAll(".schedule-date-field[data-app-date-field]").forEach((dateField) => {
                if (!(dateField instanceof HTMLElement)) {
                    return;
                }

                const valueInput = dateField.querySelector("[data-schedule-date], [data-schedule-delay-date]");
                const staticDisabled = valueInput instanceof HTMLInputElement
                    && valueInput.dataset.scheduleStaticDisabled === "true";
                const isDelayDate = valueInput instanceof HTMLInputElement
                    && valueInput.hasAttribute("data-schedule-delay-date");
                const row = dateField.closest("[data-schedule-step-row]");
                const linkedInput = row?.querySelector(isDelayDate ? "[data-schedule-delay]" : "[data-schedule-duration]");
                const linkedLocked = linkedInput instanceof HTMLInputElement ? linkedInput.disabled : true;
                const shouldDisable = !canScheduleAny || staticDisabled || linkedLocked;
                dateField.dataset.appDateLocked = shouldDisable ? "true" : "false";

                if (valueInput instanceof HTMLInputElement) {
                    valueInput.disabled = shouldDisable;
                }

                const trigger = dateField.querySelector("[data-app-date-open]");
                if (trigger instanceof HTMLButtonElement) {
                    trigger.disabled = shouldDisable;
                }
            });
        };

        if (!isTask) {
            schedulePanel.hidden = true;
            schedulePanel.setAttribute("data-schedule-disabled", "true");
            form.querySelectorAll("[data-schedule-duration], [data-schedule-delay]")
                .forEach((input) => {
                    if (input instanceof HTMLInputElement) {
                        input.disabled = true;
                    }
                });
            form.querySelectorAll("[data-schedule-duration-inc], [data-schedule-duration-dec], [data-schedule-delay-inc], [data-schedule-delay-dec]")
                .forEach((button) => {
                    if (button instanceof HTMLButtonElement) {
                        button.disabled = true;
                    }
                });
            setScheduleDateFieldState();
            setRecordFormTab(form, "basic");
        } else {
            schedulePanel.removeAttribute("data-schedule-disabled");
            form.querySelectorAll("[data-schedule-duration]")
                .forEach((input) => {
                    if (input instanceof HTMLInputElement) {
                        input.disabled = !canEditDurationInput(input);
                    }
                });
            form.querySelectorAll("[data-schedule-delay]")
                .forEach((input) => {
                    if (input instanceof HTMLInputElement) {
                        const staticDisabled = input.dataset.scheduleStaticDisabled === "true";
                        input.disabled = !canScheduleAny || staticDisabled;
                    }
                });
            syncStepperButtons("[data-schedule-duration-inc], [data-schedule-duration-dec]", "[data-schedule-duration]");
            syncStepperButtons("[data-schedule-delay-inc], [data-schedule-delay-dec]", "[data-schedule-delay]");
            setScheduleDateFieldState();

            if (!form._recordSchedulePlanner && canScheduleAny) {
                initRecordSchedulePlanner(form);
            }
        }
    }

    if (isTask && form._recordSchedulePlanner && typeof form._recordSchedulePlanner.recalcAll === "function") {
        form._recordSchedulePlanner.recalcAll();
    }
}

export function setRecordFormTab(form, tabKey) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const tabs = form.querySelectorAll("[data-record-modal-tab]");
    const panels = form.querySelectorAll("[data-record-modal-panel]");
    if (tabs.length === 0 || panels.length === 0) {
        return;
    }

    const requestedTab = typeof tabKey === "string" ? tabKey : "basic";
    const requestedButton = form.querySelector(`[data-record-modal-tab="${requestedTab}"]`);
    const normalizedTab = requestedButton instanceof HTMLElement && !requestedButton.hidden
        ? requestedTab
        : "basic";

    tabs.forEach((tab) => {
        if (!(tab instanceof HTMLElement)) {
            return;
        }

        tab.classList.toggle("active", tab.dataset.recordModalTab === normalizedTab);
    });

    panels.forEach((panel) => {
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const active = panel.dataset.recordModalPanel === normalizedTab;
        panel.hidden = !active;
        panel.classList.toggle("active", active);
    });

    const activeTabInput = form.querySelector("[data-record-active-tab-input]");
    if (activeTabInput instanceof HTMLInputElement) {
        activeTabInput.value = normalizedTab;
    }

    if (normalizedTab === "schedule") {
        if (!form._recordSchedulePlanner) {
            initRecordSchedulePlanner(form);
        }

        if (form._recordSchedulePlanner && typeof form._recordSchedulePlanner.recalcAll === "function") {
            form._recordSchedulePlanner.recalcAll();
        }

        queueRecordSchedulePlannerRecalc(form, 0);

        const harmonogramPanel = form.querySelector('[data-record-modal-panel="schedule"]');
        if (harmonogramPanel instanceof HTMLElement) {
            queueRainbowSegmentRender(harmonogramPanel);
        }
    }
}

export function initExternalLinksEditors(scope) {
    scope.querySelectorAll("[data-external-links-editor]").forEach((editor) => {
        if (!(editor instanceof HTMLElement) || editor.dataset.externalLinksReady === "true") {
            return;
        }

        const rowsContainer = editor.querySelector("[data-external-links]");
        const addButton = editor.querySelector("[data-external-add]");
        const template = editor.querySelector("template[data-external-template]");
        if (!(rowsContainer instanceof HTMLElement)
            || !(addButton instanceof HTMLButtonElement)
            || !(template instanceof HTMLTemplateElement)) {
            return;
        }

        editor.dataset.externalLinksReady = "true";
        const rowNamePattern = /ExterniVazby\[\d+\]\./g;
        const estimatedPriceTypes = new Set(["PMP", "PNF"]);

        const syncEstimatedPriceField = (row) => {
            if (!(row instanceof HTMLElement)) {
                return;
            }

            const typeSelect = row.querySelector("[data-external-type-select]");
            const priceField = row.querySelector("[data-external-price-field]");
            const priceInput = row.querySelector("[data-external-price-input]");
            if (!(typeSelect instanceof HTMLSelectElement)
                || !(priceField instanceof HTMLElement)
                || !(priceInput instanceof HTMLInputElement)) {
                return;
            }

            const shouldShow = estimatedPriceTypes.has(String(typeSelect.value || "").trim().toUpperCase());
            priceField.hidden = !shouldShow;
            priceInput.disabled = !shouldShow;
            if (!shouldShow) {
                priceInput.value = "";
            }
        };

        const reindexRows = () => {
            const rows = Array.from(rowsContainer.querySelectorAll("[data-external-row]"))
                .filter((item) => item instanceof HTMLElement);
            rows.forEach((row, index) => {
                row.querySelectorAll("[name]").forEach((field) => {
                    if (!(field instanceof HTMLElement)) {
                        return;
                    }

                    const name = field.getAttribute("name");
                    if (!name) {
                        return;
                    }

                    field.setAttribute("name", name.replace(rowNamePattern, `ExterniVazby[${index}].`));
                });
            });
        };

        const buildRowFromTemplate = (index) => {
            const html = template.innerHTML.replace(/__index__/g, String(index)).trim();
            if (!html) {
                return null;
            }

            const wrapper = document.createElement("div");
            wrapper.innerHTML = html;
            const row = wrapper.firstElementChild;
            return row instanceof HTMLElement ? row : null;
        };

        addButton.addEventListener("click", () => {
            const index = rowsContainer.querySelectorAll("[data-external-row]").length;
            const row = buildRowFromTemplate(index);
            if (!(row instanceof HTMLElement)) {
                return;
            }

            rowsContainer.appendChild(row);
            initCustomDatePickers(row);
            syncEstimatedPriceField(row);
            reindexRows();
        });

        rowsContainer.querySelectorAll("[data-external-row]").forEach((row) => {
            if (row instanceof HTMLElement) {
                syncEstimatedPriceField(row);
            }
        });

        rowsContainer.addEventListener("change", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const typeSelect = target.closest("[data-external-type-select]");
            if (!(typeSelect instanceof HTMLSelectElement)) {
                return;
            }

            syncEstimatedPriceField(typeSelect.closest("[data-external-row]"));
        });

        rowsContainer.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const removeButton = target.closest("[data-external-remove]");
            if (!(removeButton instanceof HTMLButtonElement)) {
                return;
            }

            const row = removeButton.closest("[data-external-row]");
            if (!(row instanceof HTMLElement)) {
                return;
            }

            row.remove();
            reindexRows();
        });

        reindexRows();
    });
}

export function initRecordOwnerAutofill(scope) {
    scope.querySelectorAll('form[data-record-owner-autofill="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.recordOwnerAutofillReady === "true") {
            return;
        }

        const subsystemSelect = form.querySelector("[data-record-subsystem-select]");
        const ownerPicker = form.querySelector("[data-record-owner-picker]");
        const ownerInput = ownerPicker?.querySelector("[data-person-picker-input]");
        const ownerHiddenInput = ownerPicker?.querySelector("[data-person-picker-hidden]");
        const ownerSource = ownerPicker?.querySelector("[data-person-picker-source]");
        if (!(subsystemSelect instanceof HTMLSelectElement)
            || !(ownerPicker instanceof HTMLElement)) {
            return;
        }

        form.dataset.recordOwnerAutofillReady = "true";

        const clearOwnerPicker = () => {
            if (ownerHiddenInput instanceof HTMLInputElement) {
                ownerHiddenInput.value = "";
            }

            if (ownerInput instanceof HTMLInputElement) {
                ownerInput.value = "";
                ownerInput.setCustomValidity("");
            }
        };

        const resolveSelectedOwnerId = () => {
            const selectedOption = subsystemSelect.selectedOptions[0];
            if (!(selectedOption instanceof HTMLOptionElement)) {
                return "";
            }

            const rawOwnerId = (selectedOption.dataset.ownerId || "").trim();
            const parsedOwnerId = Number.parseInt(rawOwnerId, 10);
            if (!Number.isInteger(parsedOwnerId) || parsedOwnerId <= 0) {
                return "";
            }

            return String(parsedOwnerId);
        };

        const findOwnerItem = (ownerId) => {
            if (!ownerId || !(ownerSource instanceof HTMLElement)) {
                return null;
            }

            return Array.from(ownerSource.querySelectorAll("[data-id]"))
                .find((item) => item instanceof HTMLElement && (item.dataset.id || "").trim() === ownerId);
        };

        const applyOwnerFromSubsystem = () => {
            const ownerId = resolveSelectedOwnerId();
            if (!ownerId) {
                clearOwnerPicker();
                return;
            }

            const ownerItem = findOwnerItem(ownerId);
            if (!(ownerItem instanceof HTMLElement)) {
                clearOwnerPicker();
                return;
            }

            ownerPicker.dispatchEvent(new CustomEvent("person-picker:select-id", {
                bubbles: true,
                detail: {
                    id: ownerId,
                    source: "auto"
                }
            }));

            if (ownerHiddenInput instanceof HTMLInputElement) {
                ownerHiddenInput.value = ownerId;
            }

            if (ownerInput instanceof HTMLInputElement) {
                const label = (ownerItem.dataset.label || "").trim();
                const email = (ownerItem.dataset.email || "").trim();
                ownerInput.value = email ? `${label} <${email}>` : label;
                ownerInput.setCustomValidity("");
            }
        };

        subsystemSelect.addEventListener("change", () => {
            applyOwnerFromSubsystem();
        });

        form.addEventListener("submit", () => {
            if (ownerHiddenInput instanceof HTMLInputElement
                && !ownerHiddenInput.value.trim()) {
                applyOwnerFromSubsystem();
            }
        });

        if (ownerHiddenInput instanceof HTMLInputElement
            && !ownerHiddenInput.value.trim()) {
            applyOwnerFromSubsystem();
        }
    });
}

export function initMeetingNumberValidation(scope) {
    scope.querySelectorAll('form[data-meeting-number-unique="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.meetingNumberValidationReady === "true") {
            return;
        }

        const input = form.querySelector("[data-meeting-number-input]");
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const warning = form.querySelector("[data-meeting-number-warning]");
        const submitButton = form.querySelector('button[type="submit"]');
        const existingNumbers = new Set(
            (input.dataset.existingMeetingNumbers || "")
                .split(",")
                .map((value) => Number.parseInt(value.trim(), 10))
                .filter((value) => Number.isInteger(value) && value > 0)
        );
        const currentMeetingNumber = Number.parseInt((input.dataset.currentMeetingNumber || "").trim(), 10);
        if (Number.isInteger(currentMeetingNumber) && currentMeetingNumber > 0) {
            existingNumbers.delete(currentMeetingNumber);
        }

        form.dataset.meetingNumberValidationReady = "true";

        const setWarningState = (isDuplicate) => {
            if (warning instanceof HTMLElement) {
                warning.hidden = !isDuplicate;
            }

            if (isDuplicate) {
                input.classList.add("field-invalid");
            } else {
                input.classList.remove("field-invalid");
            }

            if (submitButton instanceof HTMLButtonElement) {
                submitButton.disabled = isDuplicate;
            }
        };

        const validate = () => {
            const parsed = Number.parseInt((input.value || "").trim(), 10);
            const isDuplicate = Number.isInteger(parsed) && existingNumbers.has(parsed);
            if (isDuplicate) {
                input.setCustomValidity("Jednání s tímto číslem už v projektu existuje.");
            } else {
                input.setCustomValidity("");
            }
            setWarningState(isDuplicate);
        };

        input.addEventListener("input", validate);
        input.addEventListener("change", validate);
        form.addEventListener("submit", validate);
        validate();
    });
}

export function initRecordFormTabs(scope) {
    scope.querySelectorAll('form[data-record-form-tabs="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.recordFormTabsReady === "true") {
            return;
        }

        form.dataset.recordFormTabsReady = "true";
        const activeTabInput = form.querySelector("[data-record-active-tab-input]");
        const initialTab = activeTabInput instanceof HTMLInputElement ? activeTabInput.value : "basic";
        setRecordFormTab(form, initialTab);

        form.querySelectorAll("[data-record-modal-tab]").forEach((tabButton) => {
            if (!(tabButton instanceof HTMLButtonElement)) {
                return;
            }

            tabButton.addEventListener("click", () => {
                const tabKey = tabButton.dataset.recordModalTab || "basic";
                setRecordFormTab(form, tabKey);
            });
        });
    });
}

export function initRecordGoalAutoGrow(scope) {
    scope.querySelectorAll("textarea[data-record-goal-autogrow='true']").forEach((textarea) => {
        if (!(textarea instanceof HTMLTextAreaElement) || textarea.dataset.recordGoalAutogrowReady === "true") {
            return;
        }

        textarea.dataset.recordGoalAutogrowReady = "true";
        const resize = () => {
            const minHeight = Number.parseFloat(window.getComputedStyle(textarea).minHeight || "0");
            textarea.style.height = "auto";
            const nextHeight = Math.max(textarea.scrollHeight, Number.isFinite(minHeight) ? minHeight : 0);
            textarea.style.height = `${Math.round(nextHeight)}px`;
        };

        textarea.addEventListener("input", resize);
        textarea.addEventListener("change", resize);
        window.requestAnimationFrame(resize);
    });
}

export function initRecordMeetingDateSync(scope) {
    scope.querySelectorAll('form[data-record-editor-form="true"][data-is-create="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.recordMeetingDateSyncReady === "true") {
            return;
        }

        const meetingSelect = form.querySelector("[data-record-meeting-number-select]");
        const startDateInput = form.querySelector('[data-app-date-value][name="DatumZalozeni"]');
        if (!(meetingSelect instanceof HTMLSelectElement) || !(startDateInput instanceof HTMLInputElement)) {
            return;
        }

        form.dataset.recordMeetingDateSyncReady = "true";
        const syncToSelectedMeeting = () => {
            const selectedOption = meetingSelect.options[meetingSelect.selectedIndex];
            const meetingDateIso = selectedOption?.dataset.recordMeetingDate || "";
            if (!meetingDateIso) {
                return;
            }

            setAppDateFieldValue(startDateInput, meetingDateIso);
        };

        meetingSelect.addEventListener("change", syncToSelectedMeeting);
        syncToSelectedMeeting();
    });
}

export function looksLikeHtml(value) {
    return /<\s*\/?\s*[a-z][^>]*>/i.test(value || "");
}

export function getOrCreateRichTextSourceContainer(form) {
    if (!(form instanceof HTMLFormElement)) {
        return null;
    }

    const existing = form.querySelector("[data-rich-text-source-container]");
    if (existing instanceof HTMLElement) {
        return existing;
    }

    const container = document.createElement("div");
    container.className = "richtext-source-container";
    container.setAttribute("data-rich-text-source-container", "true");
    container.setAttribute("aria-hidden", "true");
    form.appendChild(container);
    return container;
}

export function initRichTextEditors(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    if (typeof window.Quill !== "function") {
        return;
    }

    const quillCtor = window.Quill;
    scope.querySelectorAll("textarea[data-rich-text='true']").forEach((textarea) => {
        if (!(textarea instanceof HTMLTextAreaElement)
            || textarea.disabled
            || textarea.dataset.richTextReady === "true") {
            return;
        }

        const host = document.createElement("div");
        host.className = "richtext-host";
        textarea.insertAdjacentElement("beforebegin", host);
        const form = textarea.closest("form");
        const labelParent = textarea.closest("label");
        if (labelParent instanceof HTMLLabelElement && form instanceof HTMLFormElement) {
            const sourceContainer = getOrCreateRichTextSourceContainer(form);
            if (sourceContainer instanceof HTMLElement) {
                sourceContainer.appendChild(textarea);
            } else {
                host.appendChild(textarea);
            }
        } else {
            host.appendChild(textarea);
        }
        textarea.classList.add("richtext-source-hidden");
        textarea.setAttribute("aria-hidden", "true");
        textarea.setAttribute("tabindex", "-1");

        const editorShell = document.createElement("div");
        editorShell.className = "richtext-editor-shell";
        host.appendChild(editorShell);

        const placeholder = (textarea.getAttribute("placeholder") || "").trim();
        const quill = new quillCtor(editorShell, {
            theme: "snow",
            placeholder,
            modules: {
                toolbar: [
                    ["bold", "italic", "underline"],
                    ["link"],
                    [{ list: "ordered" }, { list: "bullet" }],
                    [{ indent: "-1" }, { indent: "+1" }]
                ]
            }
        });

        textarea.dataset.richTextReady = "true";
        textarea._richTextEditor = quill;

        const editorNode = editorShell.querySelector(".ql-editor");
        const configuredMinHeight = Number.parseFloat((textarea.dataset.richTextMinHeight || "").trim());
        const minHeight = Number.isFinite(configuredMinHeight)
            ? configuredMinHeight
            : Math.max(88, Number.parseInt(textarea.getAttribute("rows") || "4", 10) * 22);
        if (editorNode instanceof HTMLElement) {
            editorNode.style.minHeight = `${Math.round(minHeight)}px`;
        }

        const syncTextarea = () => {
            const text = (quill.getText() || "").replace(/\u00a0/g, " ").trim();
            if (!text) {
                textarea.value = "";
                return;
            }

            const html = (quill.root?.innerHTML || "").trim();
            textarea.value = html && html !== "<p><br></p>" ? html : "";
        };

        const resize = () => {
            if (!(editorNode instanceof HTMLElement)) {
                return;
            }

            editorNode.style.height = "auto";
            const nextHeight = Math.max(editorNode.scrollHeight, minHeight);
            editorNode.style.height = `${Math.round(nextHeight)}px`;
        };

        const initialValue = textarea.value || "";
        if (initialValue.trim().length > 0) {
            if (looksLikeHtml(initialValue)) {
                quill.clipboard.dangerouslyPasteHTML(initialValue);
            } else {
                quill.setText(initialValue);
            }
        } else {
            quill.setText("");
        }

        if (form instanceof HTMLFormElement) {
            form.addEventListener("submit", syncTextarea);
        }

        quill.on("text-change", () => {
            syncTextarea();
            resize();
        });

        quill.on("editor-change", () => {
            resize();
        });

        syncTextarea();
        window.requestAnimationFrame(resize);
    });
}

export function initRecordFormEnhancements(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    initCustomDatePickers(scope);
    initCustomTimePickers(scope);

    scope.querySelectorAll("[data-kategorie-select]").forEach((element) => {
        if (element instanceof HTMLSelectElement) {
            updateTaskTypeVisibility(element);
        }
    });

    initSinglePersonPickers(scope);
    initRecordOwnerAutofill(scope);
    initAdPersonPickers(scope);
    initExternalLinksEditors(scope);
    initCollabPickers(scope);
    initMeetingNumberValidation(scope);
    initConfirmSubmitToggles(scope);
    initRecordFormTabs(scope);
    initRecordGoalAutoGrow(scope);
    initRecordMeetingDateSync(scope);
    initRichTextEditors(scope);
    initRecordSchedulePlanner(scope);
    initRecordEditorDirtyTracking(scope);
}

export function shouldIgnoreRecordEditorField(name) {
    if (!name) {
        return true;
    }

    const normalized = String(name).trim().toLowerCase();
    if (!normalized) {
        return true;
    }

    return normalized === "__requestverificationtoken"
        || normalized === "presentation"
        || normalized === "returnurl"
        || normalized === "editortab";
}

export function buildRecordEditorFormSnapshot(form) {
    if (!(form instanceof HTMLFormElement)) {
        return "";
    }

    const entries = [];
    const formData = new FormData(form);
    formData.forEach((value, key) => {
        if (shouldIgnoreRecordEditorField(key)) {
            return;
        }

        const normalizedValue = value instanceof File
            ? value.name
            : String(value ?? "");
        entries.push(`${key}=${normalizedValue}`);
    });

    entries.sort();
    return entries.join("&");
}

export function getRecordEditorDraftStorageKey(form) {
    if (!(form instanceof HTMLFormElement)) {
        return "";
    }

    const projectId = Number.parseInt(form.dataset.recordEditorProjectId || "", 10);
    if (!Number.isInteger(projectId) || projectId <= 0) {
        return "";
    }

    const idInput = form.querySelector('input[name="Id"]');
    const rawRecordId = idInput instanceof HTMLInputElement
        ? idInput.value.trim()
        : "";
    const recordId = rawRecordId || "new";
    const presentation = (form.dataset.recordEditorPresentation || "modal").trim().toLowerCase();
    return `${recordEditorDraftStoragePrefix}${projectId}.${recordId}.${presentation}`;
}

export function buildRecordEditorDraftValues(form) {
    const values = {};
    if (!(form instanceof HTMLFormElement)) {
        return values;
    }

    const formData = new FormData(form);
    formData.forEach((value, key) => {
        if (shouldIgnoreRecordEditorField(key) || value instanceof File) {
            return;
        }

        const normalized = String(value ?? "");
        if (!Array.isArray(values[key])) {
            values[key] = [];
        }
        values[key].push(normalized);
    });

    return values;
}

export function buildRecordEditorDraftSnapshotFromValues(values) {
    if (!values || typeof values !== "object") {
        return "";
    }

    const entries = [];
    Object.entries(values).forEach(([key, list]) => {
        if (shouldIgnoreRecordEditorField(key) || !Array.isArray(list)) {
            return;
        }

        list.forEach((value) => {
            entries.push(`${key}=${String(value ?? "")}`);
        });
    });

    entries.sort();
    return entries.join("&");
}

export function normalizeRecordEditorDraftValues(rawValues) {
    if (!rawValues || typeof rawValues !== "object") {
        return {};
    }

    const normalized = {};
    Object.entries(rawValues).forEach(([key, list]) => {
        if (shouldIgnoreRecordEditorField(key)) {
            return;
        }

        if (Array.isArray(list)) {
            const values = list.map((item) => String(item ?? ""));
            normalized[key] = values;
            return;
        }

        normalized[key] = [String(list ?? "")];
    });

    return normalized;
}

export function clearRecordEditorDraftSaveTimer(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const timerId = Number.parseInt(form.dataset.recordEditorDraftTimerId || "", 10);
    if (Number.isFinite(timerId) && timerId > 0) {
        window.clearTimeout(timerId);
    }
    delete form.dataset.recordEditorDraftTimerId;
}

export function clearRecordEditorDraft(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    clearRecordEditorDraftSaveTimer(form);
    const storageKey = getRecordEditorDraftStorageKey(form);
    if (!storageKey) {
        return;
    }

    sessionStorage.removeItem(storageKey);
}

export function saveRecordEditorDraft(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    if (form.dataset.recordEditorNavigating === "true") {
        return;
    }

    const storageKey = getRecordEditorDraftStorageKey(form);
    if (!storageKey) {
        return;
    }

    if (!isRecordEditorFormDirty(form)) {
        sessionStorage.removeItem(storageKey);
        return;
    }

    const values = buildRecordEditorDraftValues(form);
    const snapshot = buildRecordEditorDraftSnapshotFromValues(values);
    if (!snapshot) {
        sessionStorage.removeItem(storageKey);
        return;
    }

    const payload = {
        version: 1,
        savedAtUtc: new Date().toISOString(),
        values
    };
    sessionStorage.setItem(storageKey, JSON.stringify(payload));
}

export function scheduleRecordEditorDraftSave(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    clearRecordEditorDraftSaveTimer(form);
    const timerId = window.setTimeout(() => {
        delete form.dataset.recordEditorDraftTimerId;
        saveRecordEditorDraft(form);
    }, 1500);
    form.dataset.recordEditorDraftTimerId = String(timerId);
}

export function readRecordEditorDraft(form) {
    if (!(form instanceof HTMLFormElement)) {
        return null;
    }

    const storageKey = getRecordEditorDraftStorageKey(form);
    if (!storageKey) {
        return null;
    }

    const raw = sessionStorage.getItem(storageKey);
    if (!raw) {
        return null;
    }

    try {
        const parsed = JSON.parse(raw);
        const savedAtUtc = typeof parsed.savedAtUtc === "string" ? parsed.savedAtUtc : "";
        const savedAtMs = savedAtUtc ? Date.parse(savedAtUtc) : NaN;
        if (!Number.isFinite(savedAtMs) || (Date.now() - savedAtMs) > recordEditorDraftTtlMs) {
            sessionStorage.removeItem(storageKey);
            return null;
        }

        const values = normalizeRecordEditorDraftValues(parsed.values);
        const snapshot = buildRecordEditorDraftSnapshotFromValues(values);
        if (!snapshot) {
            sessionStorage.removeItem(storageKey);
            return null;
        }

        return {
            key: storageKey,
            values,
            snapshot
        };
    } catch (error) {
        sessionStorage.removeItem(storageKey);
        return null;
    }
}

export function setRecordEditorRichTextValue(textarea, nextValue) {
    if (!(textarea instanceof HTMLTextAreaElement)) {
        return;
    }

    const normalized = String(nextValue ?? "");
    textarea.value = normalized;
    const editor = textarea._richTextEditor;
    if (!editor) {
        return;
    }

    if (!normalized.trim()) {
        if (typeof editor.setText === "function") {
            editor.setText("");
        }
        return;
    }

    if (looksLikeHtml(normalized)
        && editor.clipboard
        && typeof editor.clipboard.dangerouslyPasteHTML === "function") {
        editor.clipboard.dangerouslyPasteHTML(normalized);
        return;
    }

    if (typeof editor.setText === "function") {
        editor.setText(normalized);
    }
}

export function applyRecordEditorDraft(form, values) {
    if (!(form instanceof HTMLFormElement) || !values || typeof values !== "object") {
        return false;
    }

    const controls = Array.from(form.querySelectorAll("[name]"))
        .filter((control) =>
            control instanceof HTMLInputElement
            || control instanceof HTMLTextAreaElement
            || control instanceof HTMLSelectElement);
    if (controls.length === 0) {
        return false;
    }

    const groupedControls = new Map();
    controls.forEach((control) => {
        const name = control.getAttribute("name") || "";
        if (!name || shouldIgnoreRecordEditorField(name)) {
            return;
        }

        if (!groupedControls.has(name)) {
            groupedControls.set(name, []);
        }
        groupedControls.get(name).push(control);
    });

    groupedControls.forEach((group, name) => {
        const incoming = Array.isArray(values[name])
            ? values[name].map((item) => String(item ?? ""))
            : [];
        if (group.length === 0) {
            return;
        }

        const first = group[0];
        if (first instanceof HTMLInputElement && first.type === "radio") {
            group.forEach((radio) => {
                if (radio instanceof HTMLInputElement) {
                    radio.checked = incoming.includes(radio.value);
                }
            });
            return;
        }

        if (first instanceof HTMLInputElement && first.type === "checkbox") {
            group.forEach((checkbox) => {
                if (checkbox instanceof HTMLInputElement) {
                    checkbox.checked = incoming.includes(checkbox.value);
                }
            });
            return;
        }

        if (first instanceof HTMLSelectElement && first.multiple) {
            const selected = new Set(incoming);
            group.forEach((selectControl) => {
                if (!(selectControl instanceof HTMLSelectElement)) {
                    return;
                }

                Array.from(selectControl.options).forEach((option) => {
                    option.selected = selected.has(option.value);
                });
            });
            return;
        }

        const nextValue = incoming.length > 0 ? incoming[0] : "";
        group.forEach((control) => {
            if (control instanceof HTMLTextAreaElement && control.dataset.richText === "true") {
                setRecordEditorRichTextValue(control, nextValue);
                return;
            }

            if (control instanceof HTMLInputElement
                || control instanceof HTMLTextAreaElement
                || control instanceof HTMLSelectElement) {
                control.value = nextValue;
            }
        });
    });

    controls.forEach((control) => {
        if (!(control instanceof HTMLElement)) {
            return;
        }

        control.dispatchEvent(new Event("input", { bubbles: true }));
        control.dispatchEvent(new Event("change", { bubbles: true }));
    });

    return true;
}

export function maybeRestoreRecordEditorDraft(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const draft = readRecordEditorDraft(form);
    if (!draft) {
        return;
    }

    const initialSnapshot = form.dataset.recordEditorSnapshot || "";
    if (!draft.snapshot || draft.snapshot === initialSnapshot) {
        clearRecordEditorDraft(form);
        return;
    }

    const shouldRestore = window.confirm("Byla nalezena rozpracovaná verze záznamu. Chcete ji obnovit?");
    if (!shouldRestore) {
        clearRecordEditorDraft(form);
        return;
    }

    applyRecordEditorDraft(form, draft.values);
}

export function markRecordEditorFormClean(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
}

export function isRecordEditorFormDirty(form) {
    if (!(form instanceof HTMLFormElement)) {
        return false;
    }

    return buildRecordEditorFormSnapshot(form) !== (form.dataset.recordEditorSnapshot || "");
}

export function prepareRecordEditorFormNavigation(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    form.dataset.recordEditorNavigating = "true";
    clearRecordEditorDraft(form);
    markRecordEditorFormClean(form);
}

export function closeRecordEditorCloseGuard(options) {
    const settings = options || {};
    const restoreFocus = Boolean(settings.restoreFocus);
    const trigger = recordEditorState.closeGuardTrigger;

    if (recordEditorState.closeGuard instanceof HTMLElement) {
        recordEditorState.closeGuard.remove();
    }

    recordEditorState.closeGuard = null;
    recordEditorState.closeGuardTrigger = null;

    if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
        trigger.focus({ preventScroll: true });
    }
}

export function promptRecordEditorDiscard(form, trigger) {
    if (!(form instanceof HTMLFormElement) || !isRecordEditorFormDirty(form)) {
        return Promise.resolve(true);
    }

    closeRecordEditorCloseGuard({ restoreFocus: false });

    return new Promise((resolve) => {
        const isModalForm = modalRoot instanceof HTMLElement && modalRoot.contains(form);
        const host = isModalForm ? getActiveModalContainer() : document.body;
        if (!(host instanceof HTMLElement)) {
            resolve(window.confirm("Máte neuložené změny. Chcete je zahodit?"));
            return;
        }

        const overlay = document.createElement("div");
        overlay.className = `record-editor-close-guard${isModalForm ? " record-editor-close-guard-modal" : ""}`;
        overlay.setAttribute("data-record-editor-close-guard", "true");

        const dialog = document.createElement("div");
        dialog.className = "record-editor-close-guard-dialog";
        dialog.setAttribute("role", "alertdialog");
        dialog.setAttribute("aria-modal", "true");
        dialog.setAttribute("tabindex", "-1");

        const title = document.createElement("h3");
        title.className = "record-editor-close-guard-title";
        title.textContent = "Máte neuložené změny.";
        dialog.appendChild(title);

        const text = document.createElement("p");
        text.className = "record-editor-close-guard-text";
        text.textContent = "Chcete pokračovat v úpravách, nebo změny zahodit?";
        dialog.appendChild(text);

        const actions = document.createElement("div");
        actions.className = "record-editor-close-guard-actions";

        const keepEditingButton = document.createElement("button");
        keepEditingButton.type = "button";
        keepEditingButton.className = "btn";
        keepEditingButton.textContent = "Pokračovat v úpravách";
        actions.appendChild(keepEditingButton);

        const discardButton = document.createElement("button");
        discardButton.type = "button";
        discardButton.className = "btn danger";
        discardButton.textContent = "Zahodit změny";
        actions.appendChild(discardButton);

        dialog.appendChild(actions);
        overlay.appendChild(dialog);

        const finish = (shouldDiscard, restoreFocus) => {
            closeRecordEditorCloseGuard({ restoreFocus });
            if (shouldDiscard) {
                prepareRecordEditorFormNavigation(form);
            }
            resolve(shouldDiscard);
        };

        overlay.addEventListener("click", (event) => {
            if (event.target === overlay) {
                finish(false, true);
            }
        });

        keepEditingButton.addEventListener("click", () => finish(false, true));
        discardButton.addEventListener("click", () => finish(true, false));

        host.appendChild(overlay);
        recordEditorState.closeGuard = overlay;
        recordEditorState.closeGuardTrigger = trigger instanceof HTMLElement ? trigger : null;

        window.requestAnimationFrame(() => {
            keepEditingButton.focus({ preventScroll: true });
        });
    });
}

export async function requestRecordEditorModalClose(trigger) {
    const editorForm = modalRoot?.querySelector('form[data-record-editor-form="true"]');
    if (!(editorForm instanceof HTMLFormElement)) {
        closeModal();
        return;
    }

    const canClose = await promptRecordEditorDiscard(editorForm, trigger);
    if (canClose) {
        closeModal();
    }
}

export async function requestRecordEditorPageCancel(trigger) {
    const editorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
    if (!(editorForm instanceof HTMLFormElement)) {
        const fallbackUrl = trigger instanceof HTMLElement
            ? trigger.getAttribute("data-record-editor-back-url") || window.location.href
            : window.location.href;
        window.location.assign(fallbackUrl);
        return;
    }

    const canClose = await promptRecordEditorDiscard(editorForm, trigger);
    if (!canClose) {
        return;
    }

    const targetUrl = editorForm.dataset.recordEditorBackUrl
        || (trigger instanceof HTMLElement ? trigger.getAttribute("data-record-editor-back-url") : "")
        || window.location.href;
    window.location.assign(targetUrl);
}

export function initRecordEditorDirtyTracking(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    scope.querySelectorAll('form[data-record-editor-form="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.recordEditorDirtyReady === "true") {
            return;
        }

        form.dataset.recordEditorDirtyReady = "true";
        form.dataset.recordEditorNavigating = "false";

        form.addEventListener("submit", () => {
            form.dataset.recordEditorNavigating = "true";
            clearRecordEditorDraftSaveTimer(form);
        });
        form.addEventListener("input", () => {
            scheduleRecordEditorDraftSave(form);
        });
        form.addEventListener("change", () => {
            scheduleRecordEditorDraftSave(form);
        });

        window.requestAnimationFrame(() => {
            if (form.isConnected) {
                form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
                maybeRestoreRecordEditorDraft(form);
                form.dataset.recordEditorNavigating = "false";
            }
        });
    });
}

export function resolveRecordEditorTabForFieldKey(rawKey) {
    const normalizedKey = normalizeServerFieldKey(rawKey).toLowerCase();
    if (!normalizedKey) {
        return "";
    }

    if (normalizedKey.startsWith("externivazby[")) {
        return "external";
    }

    if (normalizedKey.startsWith("vybranispolupracovniciids")) {
        return "collaboration";
    }

    if (normalizedKey.startsWith("harmonogramhodnoty[")
        || normalizedKey.startsWith("uiharmonogramdatumy[")
        || normalizedKey.startsWith("uiharmonogramposunutedatumy[")) {
        return "schedule";
    }

    const basicPrefixes = [
        "kategorie",
        "typukolu",
        "stav",
        "nazev",
        "cil",
        "popis",
        "vlastnikid",
        "datumzalozeni",
        "terminukonceni",
        "subsystem",
        "jednaniidprocislo"
    ];

    return basicPrefixes.some((prefix) => normalizedKey.startsWith(prefix)) ? "basic" : "";
}

export function resolveRecordEditorTabLabel(tabKey) {
    switch ((tabKey || "").toLowerCase()) {
        case "basic":
            return "Základní údaje";
        case "external":
            return "Externí vazby";
        case "collaboration":
            return "Spolupráce";
        case "schedule":
            return "Harmonogram";
        default:
            return "";
    }
}

export function resolveRecordEditorFieldLabel(rawKey) {
    const normalizedKey = normalizeServerFieldKey(rawKey);
    if (!normalizedKey) {
        return "";
    }

    const lower = normalizedKey.toLowerCase();
    const direct = {
        "kategorie": "Kategorie záznamu",
        "typukolu": "Typ úkolu",
        "stav": "Stav úkolu",
        "nazev": "Název",
        "cil": "Cíl",
        "popis": "Popis",
        "vlastnikid": "Vlastník",
        "datumzalozeni": "Datum založení",
        "terminukonceni": "Termín ukončení",
        "subsystem": "Subsystém",
        "jednaniidprocislo": "Jednání pro identifikátor",
        "vybranispolupracovniciids": "Spolupráce",
        "harmonogramhodnoty": "Harmonogram"
    };
    if (direct[lower]) {
        return direct[lower];
    }

    const externalMatch = /^externivazby\[(\d+)\]\.([a-z0-9_]+)$/i.exec(normalizedKey);
    if (externalMatch) {
        const row = Number.parseInt(externalMatch[1], 10) + 1;
        const fieldRaw = externalMatch[2].toLowerCase();
        const fieldLabelByKey = {
            "typ": "Typ odkazu",
            "cislo": "Číslo",
            "predpokladanacena": "Předpokládaná cena",
            "vyzva": "Výzva",
            "datumobjednani": "Datum objednání",
            "plandodani": "Plán dodání",
            "datumdodani": "Datum dodání",
            "datumprevzeti": "Datum převzetí"
        };
        const fieldLabel = fieldLabelByKey[fieldRaw] || externalMatch[2];
        return `Řádek ${row}: ${fieldLabel}`;
    }

    const scheduleMatch = /^harmonogramhodnoty\[(\d+)\]\.([a-z0-9_]+)$/i.exec(normalizedKey);
    if (scheduleMatch) {
        const row = Number.parseInt(scheduleMatch[1], 10) + 1;
        const fieldRaw = scheduleMatch[2].toLowerCase();
        const fieldLabelByKey = {
            "typid": "Typ kroku",
            "hodnota": "Hodnota"
        };
        const fieldLabel = fieldLabelByKey[fieldRaw] || scheduleMatch[2];
        return `Řádek ${row}: ${fieldLabel}`;
    }

    return normalizedKey;
}

export function buildContextualSummaryMessage(rawKey, message) {
    const trimmedMessage = String(message || "").trim();
    if (!trimmedMessage) {
        return "";
    }

    const tabKey = resolveRecordEditorTabForFieldKey(rawKey);
    const tabLabel = resolveRecordEditorTabLabel(tabKey);
    const fieldLabel = resolveRecordEditorFieldLabel(rawKey);
    const context = [tabLabel, fieldLabel].filter(Boolean).join(" / ");
    return context ? `[${context}] ${trimmedMessage}` : trimmedMessage;
}

export function normalizeServerFieldKey(rawKey) {
    if (!rawKey) {
        return "";
    }

    const key = String(rawKey).trim();
    const dotIndex = key.indexOf(".");
    if (dotIndex > 0) {
        const prefix = key.slice(0, dotIndex);
        if (/^[a-zA-Z][a-zA-Z0-9]*$/.test(prefix)
            && (prefix.toLowerCase() === "command" || prefix.toLowerCase().endsWith("command"))) {
            return key.slice(dotIndex + 1);
        }
    }

    return key;
}
