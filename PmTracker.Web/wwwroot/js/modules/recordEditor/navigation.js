/**
 * recordEditor/navigation.js
 *
 * URL builder, chooser UI, openRecordEditor, returnState, preference management.
 * Exportuje:
 * - recordEditorState (shared state object pro chooser a close-guard)
 * - buildRecordEditorUrl / navigateToRecordEditorPage / openRecordEditor
 * - captureRecordEditorReturnState / restoreRecordEditorReturnStateFromUrl
 * - createRecordEditorChooser / showRecordEditorChooser / handleRecordEditorChoice / closeRecordEditorChooser
 * - getRecordEditorPreferenceLabel / getStoredRecordEditorPreference / setStoredRecordEditorPreference
 * - clearStoredRecordEditorPreference / refreshRecordEditorPreferenceUi
 * - getCurrentLocalUrl / getRecordEditorReturnStateKey
 *
 * Pozn.: requestRecordEditorModalClose a requestRecordEditorPageCancel jsou v draft.js,
 * protože závisí na promptRecordEditorDiscard (close-guard logika).
 */

import { isButtonLike, reportClientDiagnostic, setButtonDisabled } from "../utils.js";
import { openUrlModal } from "../modals.js";
import {
    positionPrintChooser,
    registerFloatingChooser,
    unregisterFloatingChooser
} from "../ui.js";
import { buildRecordUiState, restoreRecordUiState } from "../navigation.js";

const recordEditorPreferenceStorageKey = "pmtracker.recordEditor.preference";
const recordEditorReturnStateStoragePrefix = "pmtracker.recordEditor.returnState.project.";

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
        if (isButtonLike(element)) {
            setButtonDisabled(element, preferred === null);
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
        unregisterFloatingChooser(recordEditorState.chooser);
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
    registerFloatingChooser(popover, trigger);
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

