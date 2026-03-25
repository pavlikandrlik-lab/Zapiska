import {
    applyCommentSort,
    buildRecordCommentsRequestUrl,
    initCommentSortUi,
    setCommentSortButtonLabel
} from "./comments.js";
import { navigationRuntime } from "./navigationRuntime.js";
import {
    fetchHtmlFragment,
    renderLazyLoadError,
    resolveOrCreateErrorContainer,
    resolveRecordCardElement,
    setLazyLoadingState
} from "./navigationShared.js";
import { queueRainbowSegmentRender } from "./ui.js";

const cardInteractiveSelector = [
    "button",
    "a",
    "input",
    "select",
    "textarea",
    "label",
    "[data-stop-propagation]",
    "[contenteditable='true']"
].join(", ");

export function applyRecordCommentSortDirection(card, direction) {
    const normalizedDirection = direction === "desc" ? "desc" : "asc";
    const section = card.querySelector("[data-comment-sort-section]");
    if (!(section instanceof HTMLElement)) {
        return;
    }

    applyCommentSort(section, normalizedDirection);
    const toggle = section.querySelector("[data-comment-sort-toggle]");
    if (toggle instanceof HTMLButtonElement) {
        setCommentSortButtonLabel(toggle, normalizedDirection);
    }
}

export async function loadRecordDetail(cardOrChild, options = {}) {
    const card = resolveRecordCardElement(cardOrChild);
    if (!(card instanceof HTMLElement)) {
        return false;
    }

    const detailUrl = typeof options.url === "string" && options.url
        ? options.url
        : (card.dataset.recordDetailUrl || "").trim();
    if (!detailUrl) {
        return false;
    }

    const forceReload = options.force === true;
    if (card.dataset.recordDetailLoaded === "true" && !forceReload) {
        return true;
    }

    const detailShell = card.querySelector("[data-record-detail-shell]");
    if (!(detailShell instanceof HTMLElement)) {
        return false;
    }

    const placeholder = detailShell.querySelector("[data-record-detail-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(detailShell, "data-record-detail-error");
    setLazyLoadingState(detailShell, placeholder, errorContainer, true);

    try {
        detailShell.innerHTML = await fetchHtmlFragment(detailUrl);
        card.dataset.recordDetailLoaded = "true";
        navigationRuntime.initRecordFormEnhancements?.(detailShell);
        queueRainbowSegmentRender(detailShell);
        return true;
    }
    catch (error) {
        card.dataset.recordDetailLoaded = "false";
        setLazyLoadingState(detailShell, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst detail záznamu.", "data-record-detail-retry");
        return false;
    }
}

export async function loadRecordComments(cardOrChild, options = {}) {
    const card = resolveRecordCardElement(cardOrChild);
    if (!(card instanceof HTMLElement)) {
        return false;
    }

    const commentsShell = card.querySelector("[data-record-comments-shell]");
    if (!(commentsShell instanceof HTMLElement)) {
        return false;
    }

    const baseCommentsUrl = typeof options.url === "string" && options.url
        ? options.url
        : (commentsShell.dataset.recordCommentsBaseUrl
            || commentsShell.dataset.recordCommentsUrl
            || card.dataset.recordCommentsBaseUrl
            || card.dataset.recordCommentsUrl
            || "").trim();
    if (!baseCommentsUrl) {
        return false;
    }
    const requestUrl = buildRecordCommentsRequestUrl(baseCommentsUrl, {
        limit: options.limit,
        loadAll: options.loadAll === true
    }) || baseCommentsUrl;

    const forceReload = options.force === true;
    const alreadyLoaded = commentsShell.dataset.recordCommentsLoaded === "true"
        || card.dataset.recordCommentsLoaded === "true";
    if (alreadyLoaded && !forceReload) {
        return true;
    }

    const placeholder = commentsShell.querySelector("[data-record-comments-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(commentsShell, "data-record-comments-error");
    setLazyLoadingState(commentsShell, placeholder, errorContainer, true);

    try {
        commentsShell.innerHTML = await fetchHtmlFragment(requestUrl);
        commentsShell.dataset.recordCommentsLoaded = "true";
        commentsShell.dataset.recordCommentsUrl = requestUrl;
        commentsShell.dataset.recordCommentsBaseUrl = baseCommentsUrl;
        card.dataset.recordCommentsLoaded = "true";
        card.dataset.recordCommentsBaseUrl = baseCommentsUrl;
        navigationRuntime.initRecordFormEnhancements?.(commentsShell);
        initCommentSortUi(commentsShell);
        if (options.sortDirection === "asc" || options.sortDirection === "desc") {
            applyRecordCommentSortDirection(card, options.sortDirection);
        }
        return true;
    }
    catch (error) {
        commentsShell.dataset.recordCommentsLoaded = "false";
        card.dataset.recordCommentsLoaded = "false";
        setLazyLoadingState(commentsShell, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst vyjádření.", "data-record-comments-retry");
        return false;
    }
}

export async function toggleRecordCard(cardOrChild, options = {}) {
    const card = resolveRecordCardElement(cardOrChild);
    if (!(card instanceof HTMLElement)) {
        return;
    }

    const shouldExpand = options.expand === true
        ? true
        : options.collapse === true
            ? false
            : card.classList.contains("collapsed");
    card.classList.toggle("collapsed", !shouldExpand);
    const header = card.querySelector("[data-record-toggle]");
    if (header instanceof HTMLElement) {
        header.setAttribute("aria-expanded", String(shouldExpand));
    }

    if (shouldExpand) {
        await Promise.all([
            loadRecordDetail(card, { force: options.force === true }),
            loadRecordComments(card, { force: options.force === true })
        ]);
    }
}

export function handleNavigationCardClick(target) {
    if (!(target instanceof Element)) {
        return false;
    }

    const navCard = target.closest("[data-href]");
    if (!(navCard instanceof HTMLElement)) {
        return false;
    }

    if (target.closest(cardInteractiveSelector)) {
        return false;
    }

    const href = navCard.getAttribute("data-href");
    if (!href) {
        return false;
    }

    window.location.href = href;
    return true;
}

export function handleNavigationCardKeydown(event, target) {
    if (!(event instanceof KeyboardEvent) || !(target instanceof Element)) {
        return false;
    }

    if (event.key !== "Enter" && event.key !== " ") {
        return false;
    }

    if (!(target instanceof HTMLElement) || !target.matches("[data-href]")) {
        return false;
    }

    const href = target.getAttribute("data-href");
    if (!href) {
        return false;
    }

    event.preventDefault();
    window.location.href = href;
    return true;
}
