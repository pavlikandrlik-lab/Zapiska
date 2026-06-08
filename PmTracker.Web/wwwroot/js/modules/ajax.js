import { closeModal } from "./modals.js";
import { appendCurrentAsUser } from "./navigationShared.js";
import { ensureSessionKeepAlive, hasAjaxSubmitFormsInDom, sessionState, sessionStaleErrorCode, setSessionStaleState } from "./session.js";
import {
    buildFormDataSnapshot,
    buildResponseHeadersSnapshot,
    copyTextToClipboard,
    isButtonLike,
    isPlainObject,
    parseJsonPayload,
    resolveAjaxResponseTraceId,
    setButtonDisabled,
    SUBMIT_SELECTOR,
    truncateDiagnosticBody
} from "./utils.js";
import {
    buildContextualSummaryMessage,
    clearRecordEditorDraft,
    markRecordEditorFormClean,
    normalizeServerFieldKey,
    resolveRecordEditorFieldLabel,
    resolveRecordEditorTabForFieldKey,
    resolveRecordEditorTabLabel
} from "./recordEditor.js";
import { refreshPageScope } from "./navigation.js";

const sessionExpiredErrorCode = "SESSION_EXPIRED";

export function resolveErrorTarget(field, form) {
    if (!(field instanceof HTMLElement)) {
        return null;
    }

    if (field instanceof HTMLInputElement && field.type === "hidden") {
        if (field.matches("[data-person-picker-hidden]")) {
            const pickerInput = field.closest('[data-person-picker="single"]')?.querySelector("[data-person-picker-input]");
            if (pickerInput instanceof HTMLElement) {
                return pickerInput;
            }
        }

        if (field.matches("[data-ad-guid], [data-ad-query-hidden]")) {
            const adInput = form.querySelector("[data-ad-query-input]");
            if (adInput instanceof HTMLElement) {
                return adInput;
            }
        }

        if (field.matches("[data-app-date-value]")) {
            const displayInput = field.closest("[data-app-date-field]")?.querySelector("[data-app-date-display]");
            if (displayInput instanceof HTMLElement) {
                return displayInput;
            }
        }

        if (field.matches("[data-app-time-value]")) {
            const displayInput = field.closest("[data-app-time-field]")?.querySelector("[data-app-time-display]");
            if (displayInput instanceof HTMLElement) {
                return displayInput;
            }
        }
    }

    return field;
}

export function clearModalFormErrors(form) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    form.querySelectorAll(".modal-submit-summary").forEach((node) => node.remove());
    form.querySelectorAll(".field-error-message").forEach((node) => node.remove());
    form.querySelectorAll(".field-invalid").forEach((node) => {
        if (node instanceof HTMLElement) {
            node.classList.remove("field-invalid");
            node.removeAttribute("aria-invalid");
        }
    });
}

export function findFieldByName(form, rawKey) {
    if (!(form instanceof HTMLFormElement)) {
        return null;
    }

    const normalizedKey = normalizeServerFieldKey(rawKey);
    if (!normalizedKey) {
        return null;
    }

    const controls = Array.from(form.querySelectorAll("[name]"))
        .filter((candidate) => candidate instanceof HTMLInputElement
            || candidate instanceof HTMLSelectElement
            || candidate instanceof HTMLTextAreaElement);

    const normalizedLower = normalizedKey.toLowerCase();
    const exactMatch = controls.find((candidate) => {
        const fieldName = candidate.getAttribute("name");
        return fieldName && fieldName.toLowerCase() === normalizedLower;
    });
    if (exactMatch instanceof HTMLElement) {
        return exactMatch;
    }

    const suffixMatch = controls.find((candidate) => {
        const fieldName = (candidate.getAttribute("name") || "").toLowerCase();
        return fieldName.endsWith(`.${normalizedLower}`);
    });
    return suffixMatch instanceof HTMLElement ? suffixMatch : null;
}

export function renderModalFormErrors(form, payload) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    clearModalFormErrors(form);

    const fieldErrors = payload && typeof payload === "object" && payload.fieldErrors && typeof payload.fieldErrors === "object"
        ? payload.fieldErrors
        : {};

    const summaryMessages = new Set();
    const invalidTargets = [];
    let firstInvalidTab = "";

    Object.entries(fieldErrors).forEach(([rawKey, messages]) => {
        if (!Array.isArray(messages) || messages.length === 0) {
            return;
        }

        const normalizedMessages = messages
            .map((value) => String(value || "").trim())
            .filter((value) => value.length > 0);

        if (normalizedMessages.length === 0) {
            return;
        }

        const field = findFieldByName(form, rawKey);
        if (!(field instanceof HTMLElement)) {
            if (!firstInvalidTab) {
                firstInvalidTab = resolveRecordEditorTabForFieldKey(rawKey);
            }
            normalizedMessages.forEach((message) => {
                const contextual = buildContextualSummaryMessage(rawKey, message);
                if (contextual) {
                    summaryMessages.add(contextual);
                }
            });
            return;
        }

        if (!firstInvalidTab) {
            firstInvalidTab = resolveRecordEditorTabForFieldKey(rawKey);
        }

        const target = resolveErrorTarget(field, form) || field;
        target.classList.add("field-invalid");
        target.setAttribute("aria-invalid", "true");
        invalidTargets.push(target);

        const errorHost = target.closest("label")
            || target.closest(".office-picker")
            || target.parentElement
            || form;
        if (!(errorHost instanceof HTMLElement)) {
            normalizedMessages.forEach((message) => {
                const contextual = buildContextualSummaryMessage(rawKey, message);
                if (contextual) {
                    summaryMessages.add(contextual);
                }
            });
            return;
        }

        const errorLine = document.createElement("div");
        errorLine.className = "field-error-message";
        errorLine.textContent = normalizedMessages.join(" ");
        errorHost.appendChild(errorLine);

        normalizedMessages.forEach((message) => {
            const contextual = buildContextualSummaryMessage(rawKey, message);
            if (contextual) {
                summaryMessages.add(contextual);
            }
        });
    });

    const topMessage = payload && typeof payload.message === "string" ? payload.message.trim() : "";
    const errorCode = payload && typeof payload.errorCode === "string" ? payload.errorCode.trim() : "";
    const traceId = payload && typeof payload.traceId === "string" ? payload.traceId.trim() : "";
    const diagnosticLog = payload && typeof payload.diagnosticLog === "string" ? payload.diagnosticLog.trim() : "";
    if (topMessage || summaryMessages.size > 0 || errorCode || traceId || diagnosticLog) {
        const summary = document.createElement("gov-message");
        summary.setAttribute("color", "error");
        summary.className = "modal-submit-summary";
        summary.setAttribute("role", "alert");

        const merged = [];
        if (topMessage) {
            merged.push(topMessage);
        }
        merged.push(...Array.from(summaryMessages));
        const summaryText = merged.filter(Boolean).join(" | ");
        if (summaryText) {
            const messageLine = document.createElement("div");
            messageLine.className = "modal-submit-summary-text";
            messageLine.textContent = summaryText;
            summary.appendChild(messageLine);
        }

        if (errorCode || traceId) {
            const metaLine = document.createElement("div");
            metaLine.className = "modal-submit-meta";
            const metaParts = [];
            if (errorCode) {
                metaParts.push(`Kód chyby: ${errorCode}`);
            }
            if (traceId) {
                metaParts.push(`TraceId: ${traceId}`);
            }
            metaLine.textContent = metaParts.join(" | ");
            summary.appendChild(metaLine);
        }

        const normalizedErrorCode = (errorCode || "").toUpperCase();
        if (normalizedErrorCode === sessionStaleErrorCode
            || normalizedErrorCode === sessionExpiredErrorCode) {
            const recoveryActions = document.createElement("div");
            recoveryActions.className = "modal-submit-diagnostics-actions";
            const reloadButton = document.createElement("button");
            reloadButton.type = "button";
            reloadButton.className = "modal-submit-action-btn modal-submit-action-btn--primary";
            reloadButton.textContent = "Obnovit stránku";
            reloadButton.addEventListener("click", () => {
                window.location.reload();
            });
            recoveryActions.appendChild(reloadButton);
            summary.appendChild(recoveryActions);
        }

        if (diagnosticLog) {
            const diagnosticBlock = document.createElement("details");
            diagnosticBlock.className = "modal-submit-diagnostics";

            const diagnosticSummary = document.createElement("summary");
            diagnosticSummary.textContent = "Diagnostický log";
            diagnosticBlock.appendChild(diagnosticSummary);

            const actions = document.createElement("div");
            actions.className = "modal-submit-diagnostics-actions";
            const copyButton = document.createElement("button");
            copyButton.type = "button";
            copyButton.className = "modal-submit-action-btn";
            copyButton.textContent = "Kopírovat log";
            copyButton.addEventListener("click", async () => {
                const copied = await copyTextToClipboard(diagnosticLog);
                copyButton.textContent = copied ? "Zkopírováno" : "Kopírování selhalo";
                window.setTimeout(() => {
                    copyButton.textContent = "Kopírovat log";
                }, 1800);
            });
            actions.appendChild(copyButton);

            // FIX 2026-05-04: tlačítko "Uložit log chyby" — otevře native Save As dialog
            // (File System Access API showSaveFilePicker, Chromium 86+) a uloží UTF-8 .txt
            // s názvem podle traceId. Fallback pro Firefox/Safari: anchor download s blob URL
            // (browser zobrazí standardní Save dialog dle nastavení uživatele).
            const saveButton = document.createElement("button");
            saveButton.type = "button";
            saveButton.className = "modal-submit-action-btn";
            saveButton.textContent = "Uložit log chyby";
            saveButton.addEventListener("click", async () => {
                const safeTraceId = (traceId || "diagnostic-log").replace(/[\\/:*?"<>|\s]+/g, "_");
                const fileName = `${safeTraceId}.txt`;
                // BOM ﻿ aby Notepad otevřel UTF-8 bez "ANSI" misdetection.
                const fileBody = "﻿" + diagnosticLog;
                const blob = new Blob([fileBody], { type: "text/plain;charset=utf-8" });
                let saved = false;
                try {
                    if (typeof window.showSaveFilePicker === "function") {
                        const handle = await window.showSaveFilePicker({
                            suggestedName: fileName,
                            types: [{
                                description: "Textový soubor (UTF-8)",
                                accept: { "text/plain": [".txt"] }
                            }]
                        });
                        const writable = await handle.createWritable();
                        await writable.write(blob);
                        await writable.close();
                        saved = true;
                    } else {
                        const url = URL.createObjectURL(blob);
                        const a = document.createElement("a");
                        a.href = url;
                        a.download = fileName;
                        a.style.display = "none";
                        document.body.appendChild(a);
                        a.click();
                        document.body.removeChild(a);
                        window.setTimeout(() => URL.revokeObjectURL(url), 1500);
                        saved = true;
                    }
                } catch (err) {
                    if (err && err.name === "AbortError") {
                        // user zavřel Save dialog — žádné UI zobrazení chyby
                        return;
                    }
                    saveButton.textContent = "Uložení selhalo";
                    window.setTimeout(() => {
                        saveButton.textContent = "Uložit log chyby";
                    }, 1800);
                    return;
                }
                if (saved) {
                    saveButton.textContent = "Uloženo";
                    window.setTimeout(() => {
                        saveButton.textContent = "Uložit log chyby";
                    }, 1800);
                }
            });
            actions.appendChild(saveButton);

            diagnosticBlock.appendChild(actions);

            const logPre = document.createElement("pre");
            logPre.className = "modal-submit-diagnostics-log";
            logPre.textContent = diagnosticLog;
            diagnosticBlock.appendChild(logPre);

            summary.appendChild(diagnosticBlock);
        }

        form.insertBefore(summary, form.firstElementChild);
        summary.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    if (firstInvalidTab) {
        setRecordFormTab(form, firstInvalidTab);
    }

    if (invalidTargets.length > 0 && invalidTargets[0] instanceof HTMLElement) {
        invalidTargets[0].focus();
    }
}

export function syncSinglePersonPickerInForm(form, wrapper) {
    if (!(form instanceof HTMLFormElement) || !(wrapper instanceof HTMLElement)) {
        return;
    }

    const hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
    const queryInput = wrapper.querySelector("[data-person-picker-input]");
    const source = wrapper.querySelector("[data-person-picker-source]");
    if (!(hiddenInput instanceof HTMLInputElement)
        || !(queryInput instanceof HTMLInputElement)
        || !(source instanceof HTMLElement)) {
        return;
    }

    if (hiddenInput.value.trim()) {
        queryInput.setCustomValidity("");
        return;
    }

    const query = normalizeSearchText(queryInput.value || "");
    if (!query) {
        return;
    }

    const candidates = Array.from(source.querySelectorAll("[data-id]"))
        .filter((entry) => entry instanceof HTMLElement)
        .map((entry) => {
            const id = (entry.dataset.id || "").trim();
            const label = (entry.dataset.label || "").trim();
            const email = (entry.dataset.email || "").trim();
            if (!id || !label) {
                return null;
            }

            const display = email ? `${label} <${email}>` : label;
            return { id, label, email, display };
        })
        .filter((entry) => entry !== null);

    const matches = candidates.filter((entry) => {
        const normalizedLabel = normalizeSearchText(entry.label);
        const normalizedEmail = normalizeSearchText(entry.email);
        const normalizedDisplay = normalizeSearchText(entry.display);
        return normalizedLabel === query || normalizedEmail === query || normalizedDisplay === query;
    });

    if (matches.length === 1) {
        const match = matches[0];
        hiddenInput.value = match.id;
        queryInput.value = match.display;
        queryInput.setCustomValidity("");
    }
}

export function validateRequiredPersonPickers(form) {
    if (!(form instanceof HTMLFormElement)) {
        return true;
    }

    let firstInvalidInput = null;
    form.querySelectorAll('[data-person-picker="single"]').forEach((wrapper) => {
        if (!(wrapper instanceof HTMLElement)) {
            return;
        }

        syncSinglePersonPickerInForm(form, wrapper);

        const hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
        const queryInput = wrapper.querySelector("[data-person-picker-input]");
        if (!(hiddenInput instanceof HTMLInputElement) || !(queryInput instanceof HTMLInputElement)) {
            return;
        }

        const isRequired = hiddenInput.required || queryInput.required;
        if (!isRequired) {
            queryInput.setCustomValidity("");
            return;
        }

        if (!hiddenInput.value.trim()) {
            queryInput.setCustomValidity("Vyberte osobu ze seznamu výsledků.");
            if (!(firstInvalidInput instanceof HTMLInputElement)) {
                firstInvalidInput = queryInput;
            }
        } else {
            queryInput.setCustomValidity("");
        }
    });

    if (firstInvalidInput instanceof HTMLInputElement) {
        firstInvalidInput.reportValidity();
        firstInvalidInput.focus();
        return false;
    }

    return true;
}

export function initConfirmSubmitToggles(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    scope.querySelectorAll('form[data-confirm-submit-toggle="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.confirmSubmitReady === "true") {
            return;
        }

        const checkbox = form.querySelector("[data-confirm-submit-checkbox]");
        const submit = form.querySelector(`[data-confirm-submit-button], ${SUBMIT_SELECTOR}`);
        if (!(checkbox instanceof HTMLInputElement)
            || checkbox.type !== "checkbox"
            || !isButtonLike(submit)) {
            return;
        }

        const sync = () => {
            setButtonDisabled(submit, !checkbox.checked);
        };

        checkbox.addEventListener("change", sync);
        sync();
        form.dataset.confirmSubmitReady = "true";
    });
}

export function setFormSubmitting(form, submitting) {
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const confirmationCheckbox = form.querySelector("[data-confirm-submit-checkbox]");
    const hasConfirmationGate = form.dataset.confirmSubmitToggle === "true"
        && confirmationCheckbox instanceof HTMLInputElement
        && confirmationCheckbox.type === "checkbox";

    form.querySelectorAll(SUBMIT_SELECTOR).forEach((element) => {
        if (isButtonLike(element)) {
            if (submitting) {
                setButtonDisabled(element, true);
                return;
            }

            if (hasConfirmationGate && !confirmationCheckbox.checked) {
                setButtonDisabled(element, true);
                return;
            }

            setButtonDisabled(element, false);
        }
    });
}

export function buildSessionStalePayload(action, method, requestFormSnapshot, details, isRecordEditorForm) {
    const traceId = sessionState.lastTraceId || "";
    const lines = [
        `TimestampUtc: ${new Date().toISOString()}`,
        `ErrorCode: ${sessionStaleErrorCode}`,
        `TraceId: ${traceId || "-"}`,
        "ClientSource: site.js:SessionCoordinator",
        `Request: ${(method || "POST").toUpperCase()} ${action || window.location.href}`,
        `LastKeepAliveSuccessUtc: ${sessionState.lastSuccessUtc || "-"}`,
        `KeepAliveFailuresInRow: ${sessionState.consecutiveFailures}`,
        `KeepAliveLastFailure: ${sessionState.lastFailureReason || "-"}`,
        `RecordEditorForm: ${isRecordEditorForm ? "true" : "false"}`,
        "RequestFormData:",
        requestFormSnapshot || "<unavailable>"
    ];
    if (details) {
        lines.push("Details:", String(details));
    }

    const message = isRecordEditorForm
        ? "Relace vypršela během úprav. Uložení je zablokováno, obnovte stránku. Rozpracovaný návrh záznamu zůstává uložen."
        : "Relace vypršela během úprav. Uložení je zablokováno, obnovte stránku a akci opakujte.";

    return {
        ok: false,
        message,
        errorCode: sessionStaleErrorCode,
        traceId,
        diagnosticLog: lines.join("\n"),
        fieldErrors: {}
    };
}

export function shouldAttemptSessionRecovery(payload) {
    if (!isPlainObject(payload)) {
        return false;
    }

    const code = typeof payload.errorCode === "string" ? payload.errorCode.trim().toUpperCase() : "";
    if (!code) {
        return false;
    }

    return code === "REQUEST_VALIDATION_FAILED"
        || code === sessionExpiredErrorCode
        || code === "HTTP_401"
        || code === "HTTP_403"
        || code === "NON_JSON_RESPONSE"
        || code === "EMPTY_AJAX_RESPONSE";
}

export function buildAjaxDiagnosticLines(errorCode, traceId, action, method, response, contentType, rawBody, requestFormSnapshot, extraLines) {
    const status = response instanceof Response ? response.status : 0;
    const statusText = response instanceof Response ? (response.statusText || "") : "";
    const responseUrl = response instanceof Response ? (response.url || action || window.location.href) : (action || window.location.href);
    const body = truncateDiagnosticBody(rawBody, 12000);
    const redirected = response instanceof Response ? response.redirected : false;
    const headersSnapshot = buildResponseHeadersSnapshot(response, 80);
    const lines = [
        `TimestampUtc: ${new Date().toISOString()}`,
        `ErrorCode: ${errorCode}`,
        `TraceId: ${traceId || "-"}`,
        "ClientSource: site.js:initModalAjaxSubmit",
        "ExpectedContract: ModalSubmitResultViewModel { ok:boolean, message?, errorCode?, traceId?, diagnosticLog?, fieldErrors? }",
        `Request: ${(method || "POST").toUpperCase()} ${action || window.location.href}`,
        `ResponseUrl: ${responseUrl}`,
        `Status: ${status}${statusText ? ` ${statusText}` : ""}`,
        `Redirected: ${redirected ? "true" : "false"}`,
        `ContentType: ${contentType || "-"}`,
        "ResponseHeaders:",
        headersSnapshot,
        "RequestFormData:",
        requestFormSnapshot || "<unavailable>",
        "Body:",
        body || "<empty>"
    ];

    if (Array.isArray(extraLines) && extraLines.length > 0) {
        lines.push(...extraLines.filter((line) => String(line || "").trim().length > 0));
    }

    return lines;
}

export function buildNonJsonAjaxFailureMessage(response, rawBody) {
    const status = response instanceof Response ? response.status : 0;
    const body = String(rawBody || "").toLowerCase();
    if (status === 401 || status === 403) {
        return "Relace vypršela nebo nemáte oprávnění. Obnovte stránku a zkuste akci znovu.";
    }

    if (status === 400
        && (body.includes("antiforgery")
            || body.includes("requestverificationtoken")
            || body.includes("csrf"))) {
        return "Bezpečnostní token formuláře vypršel nebo je neplatný. Obnovte stránku a akci opakujte.";
    }

    if (status >= 500) {
        return "Server během zpracování požadavku selhal. Zkuste akci opakovat.";
    }

    return "Server vrátil neočekávanou odpověď.";
}

export function buildNonJsonAjaxErrorPayload(response, action, method, contentType, rawBody, requestFormSnapshot) {
    const traceId = resolveAjaxResponseTraceId(response);
    const isEmptyBody = String(rawBody || "").trim().length === 0;
    const errorCode = isEmptyBody ? "EMPTY_AJAX_RESPONSE" : "NON_JSON_RESPONSE";
    const message = buildNonJsonAjaxFailureMessage(response, rawBody);
    const diagnosticLines = buildAjaxDiagnosticLines(
        errorCode,
        traceId,
        action,
        method,
        response,
        contentType,
        rawBody,
        requestFormSnapshot,
        null);

    const payload = {
        ok: false,
        message,
        errorCode,
        traceId,
        diagnosticLog: diagnosticLines.join("\n"),
        fieldErrors: {}
    };

    if (message.includes("token formuláře")) {
        payload.fieldErrors = {
            "__RequestVerificationToken": [
                "Token formuláře není platný nebo vypršel."
            ]
        };
    }

    return payload;
}

export function buildUnexpectedJsonContractPayload(response, action, method, contentType, rawBody, parsedPayload, requestFormSnapshot) {
    const plainPayload = isPlainObject(parsedPayload) ? parsedPayload : {};
    const traceId = resolveAjaxResponseTraceId(response);
    const payloadKeys = Object.keys(plainPayload);
    const messageFromPayload = typeof plainPayload.message === "string"
        ? plainPayload.message.trim()
        : typeof plainPayload.error === "string"
            ? plainPayload.error.trim()
            : "";
    const message = messageFromPayload || "Server vrátil nečekaný JSON kontrakt.";
    const errorCode = "UNEXPECTED_JSON_CONTRACT";
    const diagnosticLines = buildAjaxDiagnosticLines(
        errorCode,
        traceId,
        action,
        method,
        response,
        contentType,
        rawBody,
        requestFormSnapshot,
        [
            `PayloadKeys: ${payloadKeys.length > 0 ? payloadKeys.join(", ") : "-"}`,
            `PayloadType: ${Array.isArray(parsedPayload) ? "array" : typeof parsedPayload}`
        ]);

    return {
        ok: false,
        message,
        errorCode,
        traceId,
        diagnosticLog: diagnosticLines.join("\n"),
        fieldErrors: {}
    };
}

export function ensureAjaxErrorPayloadDiagnostics(parsedPayload, response, action, method, contentType, rawBody, requestFormSnapshot) {
    if (!isPlainObject(parsedPayload)) {
        return buildNonJsonAjaxErrorPayload(response, action, method, contentType, rawBody, requestFormSnapshot);
    }

    if (typeof parsedPayload.ok !== "boolean") {
        return buildUnexpectedJsonContractPayload(response, action, method, contentType, rawBody, parsedPayload, requestFormSnapshot);
    }

    if (parsedPayload.ok === true) {
        return parsedPayload;
    }

    const normalized = { ...parsedPayload };
    if (!isPlainObject(normalized.fieldErrors)) {
        normalized.fieldErrors = {};
    }

    if (typeof normalized.errorCode !== "string" || !normalized.errorCode.trim()) {
        const status = response instanceof Response ? response.status : 0;
        normalized.errorCode = status > 0 ? `HTTP_${status}` : "AJAX_OPERATION_FAILED";
    }

    if (typeof normalized.traceId !== "string" || !normalized.traceId.trim()) {
        normalized.traceId = resolveAjaxResponseTraceId(response);
    }

    if (typeof normalized.diagnosticLog !== "string" || !normalized.diagnosticLog.trim()) {
        const diagnosticLines = buildAjaxDiagnosticLines(
            normalized.errorCode,
            normalized.traceId,
            action,
            method,
            response,
            contentType,
            rawBody,
            requestFormSnapshot,
            [
                "Details:",
                "Server error payload did not include diagnosticLog.",
                `PayloadKeys: ${Object.keys(parsedPayload).join(", ") || "-"}`
            ]);
        normalized.diagnosticLog = diagnosticLines.join("\n");
    }

    return normalized;
}

export function buildAjaxExceptionPayload(error, action, method, requestFormSnapshot) {
    const errorCode = "CLIENT_AJAX_EXCEPTION";
    const exceptionName = error instanceof Error ? error.name : "UnknownError";
    const exceptionMessage = error instanceof Error ? error.message : String(error || "Unknown error");
    const exceptionStack = error instanceof Error && typeof error.stack === "string"
        ? truncateDiagnosticBody(error.stack, 12000)
        : "";
    const diagnosticLines = [
        `TimestampUtc: ${new Date().toISOString()}`,
        `ErrorCode: ${errorCode}`,
        "TraceId: -",
        "ClientSource: site.js:initModalAjaxSubmit",
        "ExpectedContract: ModalSubmitResultViewModel { ok:boolean, message?, errorCode?, traceId?, diagnosticLog?, fieldErrors? }",
        `Request: ${(method || "POST").toUpperCase()} ${action || window.location.href}`,
        `NavigatorOnline: ${navigator.onLine ? "true" : "false"}`,
        "RequestFormData:",
        requestFormSnapshot || "<unavailable>",
        `ExceptionName: ${exceptionName}`,
        `ExceptionMessage: ${exceptionMessage}`
    ];
    if (exceptionStack) {
        diagnosticLines.push("ExceptionStack:", exceptionStack);
    }

    return {
        ok: false,
        message: "Požadavek se nepodařilo dokončit na klientu.",
        errorCode,
        traceId: "",
        diagnosticLog: diagnosticLines.join("\n"),
        fieldErrors: {}
    };
}

export function initModalAjaxSubmit() {
    if (!(document.body instanceof HTMLElement) || document.body.dataset.modalAjaxReady === "true") {
        return;
    }

    document.body.dataset.modalAjaxReady = "true";
    document.addEventListener("submit", async (event) => {
        const target = event.target;
        if (!(target instanceof HTMLFormElement) || target.dataset.ajaxSubmit !== "true") {
            return;
        }
        const isModalForm = target.closest("gov-dialog[data-modal-container]") instanceof HTMLElement;

        if (event.defaultPrevented) {
            return;
        }

        event.preventDefault();
        clearModalFormErrors(target);
        const isRecordEditorForm = target.matches('[data-record-editor-form="true"]');
        const submitter = event instanceof SubmitEvent
            ? event.submitter
            : null;

        if (!validateRequiredPersonPickers(target)) {
            return;
        }

        if (!target.checkValidity()) {
            target.reportValidity();
            return;
        }

        const submitterAction = isButtonLike(submitter)
            ? (submitter.getAttribute("formaction") || "")
            : "";
        const submitterMethod = isButtonLike(submitter)
            ? (submitter.getAttribute("formmethod") || "")
            : "";
        const action = appendCurrentAsUser(submitterAction || target.getAttribute("action") || window.location.href);
        const method = (submitterMethod || target.getAttribute("method") || "post").toUpperCase();
        const blockedSnapshot = buildFormDataSnapshot(new FormData(target, submitter instanceof HTMLElement ? submitter : undefined), 120);
        if (sessionState.stale) {
            target.dataset.recordEditorNavigating = "false";
            renderModalFormErrors(
                target,
                buildSessionStalePayload(
                    action,
                    method,
                    blockedSnapshot,
                    "Submit blocked because session is stale.",
                    isRecordEditorForm));
            return;
        }

        setFormSubmitting(target, true);

        try {
            const executeSubmitAttempt = async () => {
                const formData = new FormData(target, submitter instanceof HTMLElement ? submitter : undefined);
                const requestFormSnapshot = buildFormDataSnapshot(formData, 120);
                const response = await fetch(action, {
                    method,
                    body: formData,
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    credentials: "same-origin"
                });

                const contentType = (response.headers.get("content-type") || "").toLowerCase();
                const rawBody = await response.text();
                const parsedPayload = parseJsonPayload(rawBody);
                const payload = ensureAjaxErrorPayloadDiagnostics(
                    parsedPayload,
                    response,
                    action,
                    method,
                    contentType,
                    rawBody,
                    requestFormSnapshot);

                return {
                    response,
                    payload,
                    requestFormSnapshot
                };
            };

            let result = await executeSubmitAttempt();
            const firstAttemptFailed = !result.response.ok || !result.payload || result.payload.ok !== true;
            if (firstAttemptFailed && shouldAttemptSessionRecovery(result.payload)) {
                const recovered = await ensureSessionKeepAlive("submit-recovery", true);
                if (recovered) {
                    result = await executeSubmitAttempt();
                }
            }

            if (sessionState.stale) {
                target.dataset.recordEditorNavigating = "false";
                renderModalFormErrors(
                    target,
                    buildSessionStalePayload(
                        action,
                        method,
                        result.requestFormSnapshot,
                        "Submit blocked after repeated keepalive failures.",
                        isRecordEditorForm));
                return;
            }

            if (!result.response.ok || !result.payload || result.payload.ok !== true) {
                target.dataset.recordEditorNavigating = "false";
                renderModalFormErrors(target, result.payload || { message: "Uložení se nezdařilo." });
                return;
            }

            if (isRecordEditorForm) {
                clearRecordEditorDraft(target);
                markRecordEditorFormClean(target);
                // FIX 2026-05-02: clear externí vazba pre-Save buffer entries pro tento projekt.
                // Po úspěšném Save je DB autoritativní zdroj — buffer už není potřeba.
                const projektIdEl = target.querySelector('[data-record-editor-project-id]')
                    || target.closest('[data-record-editor-project-id]');
                const projektId = projektIdEl ? projektIdEl.getAttribute('data-record-editor-project-id') : null;
                if (projektId && window.pmExterniOdkazSync?.clearAllBuffersForProject) {
                    window.pmExterniOdkazSync.clearAllBuffersForProject(projektId);
                }
                target.dataset.recordEditorNavigating = "true";
            }

            if (isModalForm) {
                closeModal();
            }
            await refreshPageScope(result.payload);
        } catch (error) {
            target.dataset.recordEditorNavigating = "false";
            const fallbackSnapshot = buildFormDataSnapshot(new FormData(target, submitter instanceof HTMLElement ? submitter : undefined), 120);
            renderModalFormErrors(target, buildAjaxExceptionPayload(error, action, method, fallbackSnapshot));
        } finally {
            setFormSubmitting(target, false);
        }
    });
}

export { hasAjaxSubmitFormsInDom };
