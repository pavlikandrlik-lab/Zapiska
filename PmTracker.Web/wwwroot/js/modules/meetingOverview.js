const FIRST_ROW_TOLERANCE_PX = 2;

function getMeetingYearGroups(scope) {
    return Array.from(scope.querySelectorAll("[data-meeting-year-group]"))
        .filter((group) => group instanceof HTMLElement);
}

function getMeetingCardSlots(scope) {
    return Array.from(scope.querySelectorAll("[data-meeting-card-wrap]"))
        .filter((slot) => slot instanceof HTMLElement);
}

function getMeetingId(slot) {
    if (!(slot instanceof HTMLElement)) {
        return "";
    }

    return slot.dataset.meetingId || "";
}

function getFirstVisualRowSlots(grid) {
    const slots = getMeetingCardSlots(grid).filter((slot) => !slot.hidden);
    if (slots.length === 0) {
        return [];
    }

    const firstTop = Math.min(...slots.map((slot) => slot.offsetTop));
    return slots.filter((slot) => Math.abs(slot.offsetTop - firstTop) <= FIRST_ROW_TOLERANCE_PX);
}

function formatMeetingCount(count) {
    return `${count} jednání`;
}

function updateMeetingYearCount(group, visibleCount) {
    const count = group.querySelector("[data-meeting-year-count]");
    if (!(count instanceof HTMLElement)) {
        return;
    }

    count.textContent = formatMeetingCount(visibleCount);
}

function clearPreviewHiddenSlots(scope) {
    getMeetingCardSlots(scope).forEach((slot) => {
        if (slot.dataset.meetingPreviewHidden !== "true") {
            return;
        }

        slot.hidden = false;
        delete slot.dataset.meetingPreviewHidden;
    });
}

function countMeetingSlots(group) {
    return getMeetingCardSlots(group).length;
}

function applyMeetingYearState(group) {
    if (!(group instanceof HTMLElement)) {
        return;
    }

    const body = group.querySelector("[data-meeting-year-body]");
    const grid = group.querySelector("[data-meeting-year-grid]");
    const toggle = group.querySelector("[data-meeting-year-toggle]");
    if (!(body instanceof HTMLElement) || !(grid instanceof HTMLElement) || !(toggle instanceof HTMLElement)) {
        return;
    }

    const state = group.dataset.meetingYearState || "collapsed";
    clearPreviewHiddenSlots(group);

    if (state === "collapsed") {
        body.hidden = true;
        body.classList.remove("is-preview");
        body.style.removeProperty("max-height");
        toggle.setAttribute("aria-expanded", "false");
        return;
    }

    body.hidden = false;
    toggle.setAttribute("aria-expanded", "true");
    if (state !== "preview") {
        body.classList.remove("is-preview");
        body.style.removeProperty("max-height");
        return;
    }

    body.classList.add("is-preview");
    body.style.removeProperty("max-height");
    const firstRowSlots = getFirstVisualRowSlots(grid);
    const firstRowIds = new Set(firstRowSlots.map(getMeetingId).filter(Boolean));
    getMeetingCardSlots(grid).forEach((slot) => {
        const meetingId = getMeetingId(slot);
        if (!meetingId || firstRowIds.has(meetingId)) {
            return;
        }

        slot.hidden = true;
        slot.dataset.meetingPreviewHidden = "true";
    });
}

function syncYearGroupedMeetingOverview(root) {
    getMeetingYearGroups(root).forEach((group) => {
        updateMeetingYearCount(group, countMeetingSlots(group));
        applyMeetingYearState(group);
    });
}

export function initMeetingOverview(scope = document) {
    const roots = Array.from(scope.querySelectorAll("[data-meeting-overview]"))
        .filter((root) => root instanceof HTMLElement);

    roots.forEach((root) => {
        syncYearGroupedMeetingOverview(root);
    });
}

export function toggleMeetingYearGroup(toggle) {
    const group = toggle instanceof HTMLElement
        ? toggle.closest("[data-meeting-year-group]")
        : null;
    if (!(group instanceof HTMLElement)) {
        return;
    }

    const currentState = group.dataset.meetingYearState || "collapsed";
    group.dataset.meetingYearState = currentState === "preview"
        ? "open"
        : currentState === "open"
            ? "collapsed"
            : "open";

    applyMeetingYearState(group);
}
