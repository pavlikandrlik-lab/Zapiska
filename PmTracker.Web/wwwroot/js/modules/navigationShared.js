export function appendCurrentAsUser(url) {
    const currentUrl = new URL(window.location.href);
    const asUser = currentUrl.searchParams.get("asUser");
    if (!asUser) {
        return url;
    }

    const resolvedUrl = new URL(url, window.location.origin);
    if (resolvedUrl.origin !== window.location.origin || resolvedUrl.searchParams.has("asUser")) {
        return url;
    }

    resolvedUrl.searchParams.set("asUser", asUser);
    return `${resolvedUrl.pathname}${resolvedUrl.search}${resolvedUrl.hash}`;
}

export async function fetchHtmlDocument(url) {
    const response = await fetch(appendCurrentAsUser(url), {
        headers: { "X-Requested-With": "XMLHttpRequest" },
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    const html = await response.text();
    return new DOMParser().parseFromString(html, "text/html");
}

export async function fetchHtmlFragment(url) {
    const response = await fetch(appendCurrentAsUser(url), {
        headers: { "X-Requested-With": "XMLHttpRequest" },
        credentials: "same-origin",
        cache: "no-store"
    });

    if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
    }

    return response.text();
}

export function parseHtmlFragment(html) {
    return new DOMParser().parseFromString(html, "text/html");
}

export function resolveProjectTabPanel(tabNameOrPanel) {
    if (tabNameOrPanel instanceof HTMLElement && tabNameOrPanel.matches("[data-tab-panel]")) {
        return tabNameOrPanel;
    }

    if (typeof tabNameOrPanel !== "string" || !tabNameOrPanel) {
        return null;
    }

    const selector = `[data-tab-panel="${CSS.escape(tabNameOrPanel)}"]`;
    const panel = document.querySelector(selector);
    return panel instanceof HTMLElement ? panel : null;
}

export function resolveRecordCardElement(cardOrChild) {
    if (cardOrChild instanceof HTMLElement && cardOrChild.classList.contains("record-card")) {
        return cardOrChild;
    }

    if (!(cardOrChild instanceof Element)) {
        return null;
    }

    const card = cardOrChild.closest(".record-card[data-record-id]");
    return card instanceof HTMLElement ? card : null;
}

export function resolveOrCreateErrorContainer(container, attributeName) {
    let error = container.querySelector(`[${attributeName}]`);
    if (error instanceof HTMLElement) {
        return error;
    }

    error = document.createElement("div");
    error.className = "record-loading-error";
    error.setAttribute(attributeName, "");
    error.hidden = true;
    container.appendChild(error);
    return error;
}

export function renderLazyLoadError(errorContainer, message, retryAttributeName) {
    if (!(errorContainer instanceof HTMLElement)) {
        return;
    }

    errorContainer.innerHTML = "";
    const text = document.createElement("span");
    text.textContent = message;
    errorContainer.appendChild(text);

    const retryButton = document.createElement("button");
    retryButton.type = "button";
    retryButton.className = "btn small ghost";
    retryButton.setAttribute(retryAttributeName, "");
    retryButton.textContent = "Zkusit znovu";
    errorContainer.appendChild(retryButton);
    errorContainer.hidden = false;
}

export function setLazyLoadingState(container, placeholder, errorContainer, isLoading) {
    if (container instanceof HTMLElement) {
        if (isLoading) {
            container.setAttribute("aria-busy", "true");
        }
        else {
            container.removeAttribute("aria-busy");
        }
    }

    if (placeholder instanceof HTMLElement) {
        placeholder.hidden = !isLoading;
    }

    if (errorContainer instanceof HTMLElement && isLoading) {
        errorContainer.hidden = true;
        errorContainer.textContent = "";
    }
}

export function isElementInHiddenTree(element) {
    let current = element;
    while (current instanceof HTMLElement) {
        if (current.hidden) {
            return true;
        }

        current = current.parentElement;
    }

    return false;
}
