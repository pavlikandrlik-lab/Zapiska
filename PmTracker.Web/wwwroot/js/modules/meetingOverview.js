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

// Explicitní swap chevron ikon — nepoužíváme CSS rotate, protože gov-icon
// custom element renderuje SVG s vlastním mask/transform a kompozitní transform
// tvoří artefakty (user 2026-04-19 noc: "šipky u jednání jsou pořád blbě"
// po prvním pokusu s rotate 180deg). Swap `name` atributu je robustní —
// gov-icon si sám správně vyrenderuje chevron-up / chevron-down SVG.
function setMeetingYearChevron(group, name) {
    const chevron = group.querySelector(".meeting-year-chevron");
    if (chevron instanceof HTMLElement && chevron.getAttribute("name") !== name) {
        chevron.setAttribute("name", name);
    }
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
    delete group.dataset.meetingYearHasHidden;

    if (state === "collapsed") {
        body.hidden = true;
        body.classList.remove("is-preview");
        body.style.removeProperty("max-height");
        toggle.setAttribute("aria-expanded", "false");
        setMeetingYearChevron(group, "chevron-down");
        return;
    }

    body.hidden = false;
    if (state !== "preview") {
        toggle.setAttribute("aria-expanded", "true");
        body.classList.remove("is-preview");
        body.style.removeProperty("max-height");
        setMeetingYearChevron(group, "chevron-up");
        return;
    }

    body.classList.add("is-preview");
    body.style.removeProperty("max-height");
    const firstRowSlots = getFirstVisualRowSlots(grid);
    const firstRowIds = new Set(firstRowSlots.map(getMeetingId).filter(Boolean));
    let hiddenCount = 0;
    getMeetingCardSlots(grid).forEach((slot) => {
        const meetingId = getMeetingId(slot);
        if (!meetingId || firstRowIds.has(meetingId)) {
            return;
        }

        slot.hidden = true;
        slot.dataset.meetingPreviewHidden = "true";
        hiddenCount += 1;
    });

    if (hiddenCount > 0) {
        group.dataset.meetingYearHasHidden = "true";
    }

    // aria-expanded: v preview reflektuje, zda je co dorozbalovat.
    // Skryté karty → uživatel může ještě expandovat → aria-expanded="false".
    // Nic neskryto (první řádek = celý rok) → aria-expanded="true" (kliknutí zavírá).
    const canExpandMore = hiddenCount > 0;
    toggle.setAttribute("aria-expanded", canExpandMore ? "false" : "true");
    // Preview s skrytými = chevron-down (uživatel může dorozbalit);
    // preview bez skrytých = chevron-up (kliknutí zabalí).
    setMeetingYearChevron(group, canExpandMore ? "chevron-down" : "chevron-up");
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

// Úprava #2 (2026-04-20): project-level history toggle v aplikační
// záložce /Jednani/Index. Klikání na hlavičku projekt-karty rozbalí / sbalí
// wrapper historických year-groups. Shodný vzorec jako year-toggle, ale
// o úroveň výše (project-level vs. year-level).
export function toggleProjectHistory(toggleEl) {
    if (!(toggleEl instanceof HTMLElement)) {
        return;
    }

    const card = toggleEl.closest("[data-project-card]");
    if (!(card instanceof HTMLElement)) {
        return;
    }

    const body = card.querySelector("[data-project-history-body]");
    if (!(body instanceof HTMLElement)) {
        return;
    }

    const chevron = toggleEl.querySelector(".meeting-project-chevron");
    const isExpanded = toggleEl.getAttribute("aria-expanded") === "true";
    const nextExpanded = !isExpanded;

    toggleEl.setAttribute("aria-expanded", nextExpanded ? "true" : "false");
    if (nextExpanded) {
        body.removeAttribute("hidden");
    } else {
        body.setAttribute("hidden", "");
    }

    if (chevron instanceof HTMLElement) {
        chevron.setAttribute("name", nextExpanded ? "chevron-up" : "chevron-down");
    }
}

export function toggleMeetingYearGroup(toggle) {
    const group = toggle instanceof HTMLElement
        ? toggle.closest("[data-meeting-year-group]")
        : null;
    if (!(group instanceof HTMLElement)) {
        return;
    }

    const currentState = group.dataset.meetingYearState || "collapsed";
    const hasHidden = group.dataset.meetingYearHasHidden === "true";

    if (currentState === "open") {
        group.dataset.meetingYearState = "collapsed";
    } else if (currentState === "preview") {
        // Když jsou v preview skryté další karty → rozbalíme na plný open.
        // Když v preview nic skryté není (vše se vešlo na první řádek) → zabalíme.
        group.dataset.meetingYearState = hasHidden ? "open" : "collapsed";
    } else {
        group.dataset.meetingYearState = "open";
    }

    applyMeetingYearState(group);
}
