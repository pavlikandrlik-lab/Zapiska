/**
 * recordEditor/draft.js
 *
 * Draft persistence + dirty-state tracking + close-guard dialog.
 * Exportuje:
 * - saveRecordEditorDraft / readRecordEditorDraft / applyRecordEditorDraft
 * - scheduleRecordEditorDraftSave / clearRecordEditorDraft / clearRecordEditorDraftSaveTimer
 * - getRecordEditorDraftStorageKey
 * - buildRecordEditorFormSnapshot / buildRecordEditorDraftValues
 * - buildRecordEditorDraftSnapshotFromValues / normalizeRecordEditorDraftValues
 * - shouldIgnoreRecordEditorField
 * - isRecordEditorFormDirty / markRecordEditorFormClean
 * - promptRecordEditorDiscard (app-level "opravdu odejít" dialog)
 * - closeRecordEditorCloseGuard
 * - prepareRecordEditorFormNavigation
 * - maybeRestoreRecordEditorDraft
 * - initRecordEditorDirtyTracking (init per form)
 * - requestRecordEditorPageCancel
 * - recordEditorState (close-guard tracking)
 */

import { setRecordEditorRichTextValue } from "./richtext.js";

export const recordEditorState = {
    closeGuard: null,
    closeGuardTrigger: null
};

const recordEditorDraftStoragePrefix = "pmtracker.recordEditor.draft.";
const recordEditorDraftTtlMs = 12 * 60 * 60 * 1000;

export function shouldIgnoreRecordEditorField(name) {
    if (!name) {
        return true;
    }

    const normalized = String(name).trim().toLowerCase();
    if (!normalized) {
        return true;
    }

    // UiHarmonogramDatumy jsou VYPOČÍTANÁ datumy z duration + typeId,
    // přepisuje je JS při přepnutí na schedule tab (ScheduleRenderer.renderEditorRows →
    // setDateInputValue). Nejsou to přímý uživatelský vstup — skutečné hodnoty jsou
    // v HarmonogramHodnoty[N].Hodnota (duration dny). Vyloučit ze snapshotu jinak
    // by přepnutí na tab Harmonogram způsobilo falešně-dirty stav a blokovalo zavření.
    if (normalized.startsWith("uiharmonogramdatumy")) {
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

export function isRecordEditorFormDirty(form) {
    if (!(form instanceof HTMLFormElement)) {
        return false;
    }

    return buildRecordEditorFormSnapshot(form) !== (form.dataset.recordEditorSnapshot || "");
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
        const host = document.body;
        if (!(host instanceof HTMLElement)) {
            resolve(window.confirm("Máte neuložené změny. Chcete je zahodit?"));
            return;
        }

        const overlay = document.createElement("div");
        overlay.className = "record-editor-close-guard";
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

export async function requestRecordEditorPageCancel(trigger) {
    const editorForm = document.querySelector('form[data-record-editor-form="true"]');
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
