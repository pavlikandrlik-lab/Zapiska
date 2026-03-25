import { navigationRuntime } from "./navigationRuntime.js";
import { fetchHtmlFragment } from "./navigationShared.js";

const commentSortDirectionStorageKey = "pmtracker.comments.sortDirection";
const defaultRecordCommentsLoadStep = 5;

export function normalizeCommentSortDirection(direction) {
    return direction === "desc" ? "desc" : "asc";
}

export function getStoredCommentSortDirection() {
    const value = localStorage.getItem(commentSortDirectionStorageKey);
    return normalizeCommentSortDirection(value);
}

export function setStoredCommentSortDirection(direction) {
    localStorage.setItem(commentSortDirectionStorageKey, normalizeCommentSortDirection(direction));
}

export function setCommentSortButtonLabel(button, direction) {
    if (!(button instanceof HTMLButtonElement)) {
        return;
    }

    button.textContent = direction === "desc"
        ? "Řazení: jednání sestupně"
        : "Řazení: jednání vzestupně";
    button.setAttribute("aria-pressed", direction === "desc" ? "true" : "false");
}

export function getCommentSortDirection(scope) {
    const section = scope instanceof HTMLElement && scope.matches("[data-comment-sort-section]")
        ? scope
        : scope instanceof Element
            ? scope.closest("[data-comment-sort-section]")
            : null;

    if (!(section instanceof HTMLElement)) {
        return getStoredCommentSortDirection();
    }

    return normalizeCommentSortDirection(section.getAttribute("data-comment-sort-direction"));
}

export function applyCommentSort(section, direction) {
    if (!(section instanceof HTMLElement)) {
        return;
    }

    const normalizedDirection = normalizeCommentSortDirection(direction);
    const list = section.querySelector("[data-comment-list]");
    if (!(list instanceof HTMLElement)) {
        return;
    }

    const items = Array.from(list.querySelectorAll("[data-comment-item]"))
        .filter((item) => item instanceof HTMLElement);
    if (items.length <= 1) {
        return;
    }

    items.sort((aNode, bNode) => {
        const aMeeting = Number(aNode.getAttribute("data-comment-meeting") || "0");
        const bMeeting = Number(bNode.getAttribute("data-comment-meeting") || "0");
        const aDate = Date.parse(aNode.getAttribute("data-comment-date") || "");
        const bDate = Date.parse(bNode.getAttribute("data-comment-date") || "");
        const aId = Number(aNode.getAttribute("data-comment-id") || "0");
        const bId = Number(bNode.getAttribute("data-comment-id") || "0");
        if (normalizedDirection === "desc") {
            return (bMeeting - aMeeting)
                || ((Number.isFinite(bDate) ? bDate : 0) - (Number.isFinite(aDate) ? aDate : 0))
                || (bId - aId);
        }

        return (aMeeting - bMeeting)
            || ((Number.isFinite(aDate) ? aDate : 0) - (Number.isFinite(bDate) ? bDate : 0))
            || (aId - bId);
    });

    items.forEach((item) => list.appendChild(item));
    section.setAttribute("data-comment-sort-direction", normalizedDirection);
}

function resolveRecordCommentsShell(source) {
    if (source instanceof HTMLElement && source.matches("[data-record-comments-shell]")) {
        return source;
    }

    if (!(source instanceof Element)) {
        return null;
    }

    const shell = source.closest("[data-record-comments-shell]");
    return shell instanceof HTMLElement ? shell : null;
}

function normalizeCount(value) {
    const parsed = Number.parseInt(String(value || ""), 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}

function normalizeLoadStep(value) {
    const parsed = Number.parseInt(String(value || ""), 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : defaultRecordCommentsLoadStep;
}

export function readRecordCommentsPanelState(source) {
    const panel = source instanceof HTMLElement && source.matches("[data-record-comments-panel]")
        ? source
        : source instanceof Element
            ? source.closest("[data-record-comments-panel]")
            : null;
    if (!(panel instanceof HTMLElement)) {
        return null;
    }

    const loadedCount = normalizeCount(panel.dataset.recordCommentsLoadedCount);
    const totalCount = normalizeCount(panel.dataset.recordCommentsTotalCount);
    const loadStep = normalizeLoadStep(panel.dataset.recordCommentsLoadStep);
    const isFullyLoaded = panel.dataset.recordCommentsIsFullyLoaded === "true"
        || (totalCount > 0 && loadedCount >= totalCount);

    return {
        panel,
        loadedCount,
        totalCount,
        loadStep,
        isFullyLoaded
    };
}

export function buildRecordCommentsRequestUrl(baseUrl, options = {}) {
    if (typeof baseUrl !== "string" || !baseUrl.trim()) {
        return "";
    }

    const url = new URL(baseUrl, window.location.origin);
    if (options.loadAll === true) {
        url.searchParams.set("loadAll", "true");
        url.searchParams.delete("limit");
    }
    else {
        url.searchParams.delete("loadAll");
        const limit = Number(options.limit);
        if (Number.isFinite(limit) && limit > 0) {
            url.searchParams.set("limit", String(Math.trunc(limit)));
        }
        else {
            url.searchParams.delete("limit");
        }
    }

    return `${url.pathname}${url.search}${url.hash}`;
}

function resolveRecordCommentsBaseUrl(shell) {
    if (!(shell instanceof HTMLElement)) {
        return "";
    }

    const shellUrl = (shell.dataset.recordCommentsBaseUrl || shell.dataset.recordCommentsUrl || "").trim();
    if (shellUrl) {
        return shellUrl;
    }

    const card = shell.closest(".record-card[data-record-id]");
    if (!(card instanceof HTMLElement)) {
        return "";
    }

    return (card.dataset.recordCommentsBaseUrl || card.dataset.recordCommentsUrl || "").trim();
}

function resolveInlineCommentsError(shell) {
    const existing = shell.querySelector("[data-record-comments-inline-error]");
    if (existing instanceof HTMLElement) {
        return existing;
    }

    const error = document.createElement("div");
    error.className = "record-loading-error";
    error.setAttribute("data-record-comments-inline-error", "");
    error.hidden = true;
    shell.appendChild(error);
    return error;
}

function setInlineCommentsError(shell, message) {
    const error = resolveInlineCommentsError(shell);
    error.textContent = message;
    error.hidden = !message;
}

export async function reloadProjectRecordCommentsPanel(source, options = {}) {
    const shell = resolveRecordCommentsShell(source);
    if (!(shell instanceof HTMLElement)) {
        return false;
    }

    const baseUrl = resolveRecordCommentsBaseUrl(shell);
    if (!baseUrl) {
        return false;
    }

    const requestUrl = buildRecordCommentsRequestUrl(baseUrl, {
        limit: options.limit,
        loadAll: options.loadAll === true
    }) || baseUrl;
    const sortDirection = options.sortDirection === "desc" || options.sortDirection === "asc"
        ? options.sortDirection
        : getCommentSortDirection(source);
    const card = shell.closest(".record-card[data-record-id]");

    setInlineCommentsError(shell, "");
    shell.setAttribute("aria-busy", "true");

    try {
        shell.innerHTML = await fetchHtmlFragment(requestUrl);
        shell.dataset.recordCommentsLoaded = "true";
        shell.dataset.recordCommentsUrl = requestUrl;
        shell.dataset.recordCommentsBaseUrl = baseUrl;
        if (card instanceof HTMLElement) {
            card.dataset.recordCommentsLoaded = "true";
            card.dataset.recordCommentsBaseUrl = baseUrl;
        }

        navigationRuntime.initRecordFormEnhancements?.(shell);
        initCommentSortUi(shell);
        const section = shell.querySelector("[data-comment-sort-section]");
        if (section instanceof HTMLElement) {
            applyCommentSort(section, sortDirection);
            const toggle = section.querySelector("[data-comment-sort-toggle]");
            if (toggle instanceof HTMLButtonElement) {
                setCommentSortButtonLabel(toggle, sortDirection);
            }
        }

        return true;
    }
    catch {
        setInlineCommentsError(shell, "Nepodařilo se načíst další vyjádření.");
        return false;
    }
    finally {
        shell.removeAttribute("aria-busy");
    }
}

export function applyCommentSortToAllSections(direction, scope = document) {
    const root = scope instanceof Element ? scope : document;
    const normalizedDirection = normalizeCommentSortDirection(direction);
    root.querySelectorAll("[data-comment-sort-section]").forEach((section) => {
        if (!(section instanceof HTMLElement)) {
            return;
        }

        applyCommentSort(section, normalizedDirection);
        const toggle = section.querySelector("[data-comment-sort-toggle]");
        if (toggle instanceof HTMLButtonElement) {
            setCommentSortButtonLabel(toggle, normalizedDirection);
        }
    });
}

export function initCommentSortUi(scope = document) {
    const root = scope instanceof Element ? scope : document;
    const sections = root.querySelectorAll("[data-comment-sort-section]");
    if (sections.length === 0) {
        return;
    }

    sections.forEach((section) => {
        if (!(section instanceof HTMLElement)) {
            return;
        }

        const defaultDirection = getStoredCommentSortDirection();
        applyCommentSort(section, defaultDirection);

        const toggle = section.querySelector("[data-comment-sort-toggle]");
        if (!(toggle instanceof HTMLButtonElement)) {
            return;
        }

        setCommentSortButtonLabel(toggle, defaultDirection);
        if (toggle.dataset.commentSortReady === "true") {
            const loadMoreButtonReady = section.querySelector("[data-record-comments-load-more]");
            const loadAllButtonReady = section.querySelector("[data-record-comments-load-all]");
            if (loadMoreButtonReady instanceof HTMLButtonElement && loadMoreButtonReady.dataset.commentLoadMoreReady !== "true") {
                loadMoreButtonReady.dataset.commentLoadMoreReady = "true";
                loadMoreButtonReady.addEventListener("click", async () => {
                    const state = readRecordCommentsPanelState(section);
                    if (!state) {
                        return;
                    }

                    loadMoreButtonReady.disabled = true;
                    try {
                        await reloadProjectRecordCommentsPanel(loadMoreButtonReady, {
                            limit: state.loadedCount + state.loadStep,
                            sortDirection: getCommentSortDirection(section)
                        });
                    }
                    finally {
                        loadMoreButtonReady.disabled = false;
                    }
                });
            }

            if (loadAllButtonReady instanceof HTMLButtonElement && loadAllButtonReady.dataset.commentLoadAllReady !== "true") {
                loadAllButtonReady.dataset.commentLoadAllReady = "true";
                loadAllButtonReady.addEventListener("click", async () => {
                    loadAllButtonReady.disabled = true;
                    try {
                        await reloadProjectRecordCommentsPanel(loadAllButtonReady, {
                            loadAll: true,
                            sortDirection: getCommentSortDirection(section)
                        });
                    }
                    finally {
                        loadAllButtonReady.disabled = false;
                    }
                });
            }
            return;
        }

        toggle.dataset.commentSortReady = "true";
        toggle.addEventListener("click", () => {
            const current = section.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";
            const next = current === "asc" ? "desc" : "asc";
            setStoredCommentSortDirection(next);
            applyCommentSortToAllSections(next, document);
        });

        const loadMoreButton = section.querySelector("[data-record-comments-load-more]");
        if (loadMoreButton instanceof HTMLButtonElement && loadMoreButton.dataset.commentLoadMoreReady !== "true") {
            loadMoreButton.dataset.commentLoadMoreReady = "true";
            loadMoreButton.addEventListener("click", async () => {
                const state = readRecordCommentsPanelState(section);
                if (!state) {
                    return;
                }

                loadMoreButton.disabled = true;
                try {
                    await reloadProjectRecordCommentsPanel(loadMoreButton, {
                        limit: state.loadedCount + state.loadStep,
                        sortDirection: getCommentSortDirection(section)
                    });
                }
                finally {
                    loadMoreButton.disabled = false;
                }
            });
        }

        const loadAllButton = section.querySelector("[data-record-comments-load-all]");
        if (loadAllButton instanceof HTMLButtonElement && loadAllButton.dataset.commentLoadAllReady !== "true") {
            loadAllButton.dataset.commentLoadAllReady = "true";
            loadAllButton.addEventListener("click", async () => {
                loadAllButton.disabled = true;
                try {
                    await reloadProjectRecordCommentsPanel(loadAllButton, {
                        loadAll: true,
                        sortDirection: getCommentSortDirection(section)
                    });
                }
                finally {
                    loadAllButton.disabled = false;
                }
            });
        }
    });
}
