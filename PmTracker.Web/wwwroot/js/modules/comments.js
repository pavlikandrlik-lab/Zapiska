const commentSortDirectionStorageKey = "pmtracker.comments.sortDirection";

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
        const aId = Number(aNode.getAttribute("data-comment-id") || "0");
        const bId = Number(bNode.getAttribute("data-comment-id") || "0");
        if (normalizedDirection === "desc") {
            return (bMeeting - aMeeting) || (bId - aId);
        }

        return (aMeeting - bMeeting) || (aId - bId);
    });

    items.forEach((item) => list.appendChild(item));
    section.setAttribute("data-comment-sort-direction", normalizedDirection);
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

        const defaultDirection = normalizeCommentSortDirection(
            section.getAttribute("data-comment-sort-direction") || getStoredCommentSortDirection());
        applyCommentSort(section, defaultDirection);

        const toggle = section.querySelector("[data-comment-sort-toggle]");
        if (!(toggle instanceof HTMLButtonElement)) {
            return;
        }

        setCommentSortButtonLabel(toggle, defaultDirection);
        if (toggle.dataset.commentSortReady === "true") {
            return;
        }

        toggle.dataset.commentSortReady = "true";
        toggle.addEventListener("click", () => {
            const current = section.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";
            const next = current === "asc" ? "desc" : "asc";
            setStoredCommentSortDirection(next);
            applyCommentSortToAllSections(next, document);
        });
    });
}
