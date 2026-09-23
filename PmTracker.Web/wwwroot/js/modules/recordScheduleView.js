// recordScheduleView.js — toggle Záznam ⇄ Harmonogram na kartě záznamu (spec 2026-07-13).
//
// Stav drží dataset karty (data-record-view-schedule → card.dataset.recordViewSchedule) —
// JEDINÝ zdroj pravdy; třídu .record-card--schedule-view (CSS řídí viditelnost shellů)
// a checked switche od něj odvozuje applyRecordViewState. recordRefresh.js díky tomu
// umí stav přečíst ze staré karty a přenést na novou (R9/3).
//
// Harmonogram je třetí lazy shell (vedle detailu a vyjádření); osy se kreslí až po
// zviditelnění — měří šířku, ve skrytém prvku je nulová (ověřený vzor „Rozpad").
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import {
    fetchHtmlFragment,
    renderLazyLoadError,
    resolveOrCreateErrorContainer,
    resolveRecordCardElement,
    setLazyLoadingState
} from "./navigationShared.js";
import { toggleRecordCard } from "./recordLazyLoading.js";
import { renderStaticTimelineAxes } from "./schedule.js";
import { queueRainbowSegmentRender } from "./ui.js";

const VIEW_CLASS = "record-card--schedule-view";

export function isRecordScheduleView(card) {
    return card instanceof HTMLElement && card.dataset.recordViewSchedule === "true";
}

/** Odvodí třídu karty a checked switche z datasetu (jediný zdroj pravdy). */
export function applyRecordViewState(card) {
    if (!(card instanceof HTMLElement)) {
        return;
    }
    const scheduleView = isRecordScheduleView(card);
    card.classList.toggle(VIEW_CLASS, scheduleView);
    const viewSwitch = card.querySelector("[data-record-view-switch]");
    if (viewSwitch instanceof HTMLElement) {
        if (scheduleView) {
            viewSwitch.setAttribute("checked", "");
        }
        else {
            viewSwitch.removeAttribute("checked");
        }
    }
}

export async function loadRecordSchedule(cardOrChild, options = {}) {
    const card = resolveRecordCardElement(cardOrChild);
    if (!(card instanceof HTMLElement)) {
        return false;
    }

    const shell = card.querySelector("[data-record-schedule-shell]");
    if (!(shell instanceof HTMLElement)) {
        return false;
    }

    const scheduleUrl = (shell.dataset.recordScheduleUrl || "").trim();
    if (!scheduleUrl) {
        return false;
    }

    const forceReload = options.force === true;
    if (card.dataset.recordScheduleLoaded === "true" && !forceReload) {
        return true;
    }

    const placeholder = shell.querySelector("[data-record-schedule-placeholder]");
    const errorContainer = resolveOrCreateErrorContainer(shell, "data-record-schedule-error");
    setLazyLoadingState(shell, placeholder, errorContainer, true);

    try {
        shell.innerHTML = await fetchHtmlFragment(scheduleUrl);
        card.dataset.recordScheduleLoaded = "true";
        shell.removeAttribute("aria-busy");
        // Kreslit až tady — shell je v tuto chvíli viditelný (setRecordView napřed
        // aplikuje view třídu), takže osy mají nenulovou šířku.
        renderStaticTimelineAxes(shell);
        queueRainbowSegmentRender(shell);
        return true;
    }
    catch {
        card.dataset.recordScheduleLoaded = "false";
        setLazyLoadingState(shell, placeholder, errorContainer, false);
        renderLazyLoadError(errorContainer, "Nepodařilo se načíst harmonogram záznamu.", "data-record-schedule-retry");
        return false;
    }
}

export async function setRecordView(card, scheduleView) {
    if (!(card instanceof HTMLElement)) {
        return;
    }

    card.dataset.recordViewSchedule = scheduleView ? "true" : "false";
    applyRecordViewState(card);

    if (scheduleView) {
        // R9/1: přepnutí sbalené karty ji rovnou rozbalí.
        if (card.classList.contains("collapsed")) {
            await toggleRecordCard(card, { expand: true });
        }
        await loadRecordSchedule(card);
    }
}

/** R9/4: cross-nav „Zobrazit záznam" (harmonogram → záznamy) vrací kartu do pohledu záznam. */
export function resetRecordViewToRecord(recordId) {
    const id = String(recordId || "").trim();
    if (!id) {
        return;
    }
    document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(id)}"]`).forEach((card) => {
        if (card instanceof HTMLElement) {
            card.dataset.recordViewSchedule = "false";
            applyRecordViewState(card);
        }
    });
}

function readSwitchChecked(event, viewSwitch) {
    // gov-form-switch CustomEvent: event.detail.checked je authoritative (vzor rezim-master-switch.js).
    if (event && event.detail && typeof event.detail.checked === "boolean") {
        return event.detail.checked;
    }
    // Vnitřní input je zdroj pravdy — flipne se synchronně před bublinou change, takže je
    // spolehlivější než host.checked (gov sync hostu může běžet až po našem listeneru).
    const innerInput = viewSwitch.querySelector("input");
    if (innerInput instanceof HTMLInputElement) {
        return innerInput.checked;
    }
    if (typeof viewSwitch.checked === "boolean") {
        return viewSwitch.checked;
    }
    return viewSwitch.hasAttribute("checked");
}

function handleSwitchChange(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const viewSwitch = target.closest("[data-record-view-switch]");
    if (!(viewSwitch instanceof HTMLElement) || viewSwitch.hasAttribute("disabled")) {
        return;
    }
    const card = viewSwitch.closest(".record-card");
    if (!(card instanceof HTMLElement)) {
        return;
    }
    void setRecordView(card, readSwitchChecked(event, viewSwitch));
}

function handleRetryClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const retry = target.closest("[data-record-schedule-retry]");
    if (!(retry instanceof HTMLElement)) {
        return;
    }
    event.preventDefault();
    void loadRecordSchedule(retry, { force: true });
}

// gov-form-switch emituje `gov-change` (Stencil) i klasický `change` — poslouchat oba
// (stejně jako rezim-master-switch.js).
document.addEventListener("change", handleSwitchChange);
document.addEventListener("gov-change", handleSwitchChange);
document.addEventListener("click", handleRetryClick);
