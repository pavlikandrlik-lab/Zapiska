import { isPlainObject, parseJsonPayload } from "./utils.js";

export const sessionStaleErrorCode = "SESSION_STALE_CLIENT_BLOCK";

const keepAliveEndpointPath = "/App/KeepAlive";
const keepAliveIntervalMs = 5 * 60 * 1000;
const keepAliveTimeoutMs = 10 * 1000;
const keepAliveFailureThreshold = 2;

export const sessionState = {
    intervalId: 0,
    inFlightPromise: null,
    stale: false,
    consecutiveFailures: 0,
    lastSuccessUtc: "",
    lastTraceId: "",
    lastFailureReason: ""
};

export function hasAjaxSubmitFormsInDom() {
    return document.querySelector('form[data-ajax-submit="true"]') instanceof HTMLFormElement;
}

export function setSessionStaleState(stale, reason) {
    sessionState.stale = Boolean(stale);
    document.documentElement.dataset.sessionStale = sessionState.stale ? "true" : "false";

    if (sessionState.stale) {
        if (reason) {
            sessionState.lastFailureReason = String(reason);
        }
        return;
    }

    sessionState.consecutiveFailures = 0;
    sessionState.lastFailureReason = "";
}

export function updateRequestVerificationTokens(nextToken) {
    const token = String(nextToken || "").trim();
    if (!token) {
        return false;
    }

    let updated = 0;
    document.querySelectorAll('input[name="__RequestVerificationToken"]').forEach((input) => {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        input.value = token;
        updated += 1;
    });

    return updated > 0;
}

function buildKeepAliveFailureReason(response, payload, error) {
    if (isPlainObject(payload) && typeof payload.message === "string" && payload.message.trim()) {
        return payload.message.trim();
    }

    if (response instanceof Response) {
        const status = response.status || 0;
        const statusText = response.statusText || "";
        return status > 0
            ? `HTTP ${status}${statusText ? ` ${statusText}` : ""}`
            : "KeepAlive response failed.";
    }

    if (error instanceof Error && error.message) {
        return error.message;
    }

    return "KeepAlive request failed.";
}

async function performKeepAliveRequest(source, force) {
    if (!force && document.hidden) {
        return true;
    }

    if (!force && !hasAjaxSubmitFormsInDom()) {
        return true;
    }

    const abortController = typeof AbortController === "function"
        ? new AbortController()
        : null;
    const timeoutId = window.setTimeout(() => {
        if (abortController) {
            abortController.abort();
        }
    }, keepAliveTimeoutMs);

    try {
        const response = await fetch(keepAliveEndpointPath, {
            method: "GET",
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "Accept": "application/json"
            },
            credentials: "same-origin",
            cache: "no-store",
            signal: abortController ? abortController.signal : undefined
        });
        const rawBody = await response.text();
        const payload = parseJsonPayload(rawBody);
        if (isPlainObject(payload) && typeof payload.traceId === "string" && payload.traceId.trim()) {
            sessionState.lastTraceId = payload.traceId.trim();
        }

        const hasToken = isPlainObject(payload)
            && typeof payload.requestVerificationToken === "string"
            && payload.requestVerificationToken.trim().length > 0;
        if (response.ok && isPlainObject(payload) && payload.ok === true && hasToken) {
            updateRequestVerificationTokens(payload.requestVerificationToken);
            sessionState.lastSuccessUtc = new Date().toISOString();
            setSessionStaleState(false, "");
            return true;
        }

        sessionState.consecutiveFailures += 1;
        const reason = buildKeepAliveFailureReason(response, payload, null);
        sessionState.lastFailureReason = reason;
        if (sessionState.consecutiveFailures >= keepAliveFailureThreshold) {
            setSessionStaleState(true, `${source}: ${reason}`);
        }
        return false;
    } catch (error) {
        sessionState.consecutiveFailures += 1;
        const reason = buildKeepAliveFailureReason(null, null, error);
        sessionState.lastFailureReason = reason;
        if (sessionState.consecutiveFailures >= keepAliveFailureThreshold) {
            setSessionStaleState(true, `${source}: ${reason}`);
        }
        return false;
    } finally {
        window.clearTimeout(timeoutId);
    }
}

export async function ensureSessionKeepAlive(source, force) {
    if (sessionState.stale && !force) {
        return false;
    }

    if (sessionState.inFlightPromise && typeof sessionState.inFlightPromise.then === "function") {
        return sessionState.inFlightPromise;
    }

    sessionState.inFlightPromise = performKeepAliveRequest(source, Boolean(force))
        .finally(() => {
            sessionState.inFlightPromise = null;
        });
    return sessionState.inFlightPromise;
}

export function initSessionCoordinator() {
    if (!(document.body instanceof HTMLElement) || document.body.dataset.sessionCoordinatorReady === "true") {
        return;
    }

    const scheduleKeepAlive = () => {
        void ensureSessionKeepAlive("scheduled", false);
    };

    document.body.dataset.sessionCoordinatorReady = "true";
    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) {
            scheduleKeepAlive();
        }
    });
    window.addEventListener("focus", () => {
        scheduleKeepAlive();
    });

    sessionState.intervalId = window.setInterval(scheduleKeepAlive, keepAliveIntervalMs);
    window.setTimeout(scheduleKeepAlive, 3000);
}
