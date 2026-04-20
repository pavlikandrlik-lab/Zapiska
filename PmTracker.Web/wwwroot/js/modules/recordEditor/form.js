/**
 * recordEditor/form.js
 *
 * Tabs, metadata bindings, task-type visibility, owner autofill,
 * meeting validation, field-specific init, field label resolvers.
 * Exportuje:
 * - initPermissionMetadataBindings
 * - updateTaskTypeVisibility
 * - setRecordFormTab
 * - initRecordFormTabs
 * - initExternalLinksEditors
 * - initRecordOwnerAutofill
 * - initMeetingNumberValidation
 * - initRecordGoalAutoGrow
 * - initRecordMeetingDateSync
 * - resolveRecordEditorTabForFieldKey / resolveRecordEditorTabLabel
 * - resolveRecordEditorFieldLabel / buildContextualSummaryMessage
 * - normalizeServerFieldKey
 */

import { isButtonLike } from "../utils.js";
import { initCustomDatePickers, setAppDateFieldValue } from "../pickers.js";
import { initRecordSchedulePlanner, queueRecordSchedulePlannerRecalc } from "../schedule.js";
import { queueRainbowSegmentRender } from "../ui.js";
import { buildRecordEditorFormSnapshot } from "./draft.js";

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
    // V proposal editoru (návrh úpravy harmonogramu I návrh založení
    // záznamu) NESMÍ být TypUkolu měnitelný — typ úkolu není součástí
    // návrhového workflow. Razor to zajišťuje přes
    // `metadataLocked || IsProposalEditor`, ale JS musí mít stejnou
    // logiku, jinak na event změny kategorie typeSelect.disabled=false
    // re-enable typeSelect. User report 2026-04-19 noc: "F není to opraveno,
    // stále mohu rozkliknout a rozbalí se mi nabídka - Typ úkolu".
    // Root cause: ConfigureCreateProposalEditor ponechává
    // AllowBasicMetadataEdit=true (návrh založení má být editable kromě
    // typu), takže metadataLocked=false a samotný check nestačí.
    const metadataLocked = form.dataset.metadataLocked === "true";
    const isProposalEditor = form.dataset.isProposalEditor === "true";

    if (topRow instanceof HTMLElement) {
        topRow.dataset.hasType = isTask ? "true" : "false";
    }

    if (!(typeRow instanceof HTMLElement)) {
        return;
    }

    const typeSelect = typeRow.querySelector("select");
    typeRow.hidden = !isTask;
    if (typeSelect instanceof HTMLSelectElement) {
        typeSelect.disabled = !isTask || metadataLocked || isProposalEditor;
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

        // Schedule planner přepsal hodnoty UiHarmonogramDatumy[*] a normalizoval
        // duration/delay inputy. Toto není uživatelská změna — obnovíme snapshot,
        // aby se po přepnutí na schedule tab nespouštěl close guard a modal šel
        // zavřít. Viz docs/specs/modal-close-guard.md.
        window.requestAnimationFrame(() => {
            if (form.isConnected) {
                form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
            }
        });
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
            || !isButtonLike(addButton)
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
            if (!isButtonLike(removeButton)) {
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
