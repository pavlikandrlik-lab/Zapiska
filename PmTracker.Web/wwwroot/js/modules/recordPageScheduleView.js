// recordPageScheduleView.js — přepínač Graf ⇄ Tabulka na stránce záznamu (spec 2026-07-14).
//
// Obě podoby harmonogramu jsou v DOMu (server je vyrenderuje naráz); přepínač jen mění
// třídu na kontejneru [data-record-page-schedule] a CSS skryje neaktivní. Žádný server
// round-trip, žádný lazy-load.
//
// Osy grafické podoby se měří ze šířky — ve skrytém prvku mají nulu. Proto se kreslí
// až ve chvíli, kdy je graf viditelný (při načtení v grafickém režimu, nebo při prvním
// přepnutí zpět na graf).
//
// Stav UI drží třída kontejneru (ne atribut na custom elementu) — memory
// feedback_pm_button_strips_host_attributes.
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { renderStaticTimelineAxes } from "./schedule.js";
import { queueRainbowSegmentRender } from "./ui.js";

const STORAGE_KEY = "pmtracker.recordPage.scheduleView";
const TABLE_CLASS = "record-page-schedule--table";

function readStoredView() {
    try {
        return window.localStorage.getItem(STORAGE_KEY) === "table" ? "table" : "graph";
    }
    catch {
        return "graph";
    }
}

function storeView(view) {
    try {
        window.localStorage.setItem(STORAGE_KEY, view);
    }
    catch {
        // Soukromý režim / zakázané úložiště — poloha se prostě nezapamatuje.
    }
}

function renderGraphOnce(container) {
    if (container.dataset.scheduleAxesRendered === "true") {
        return;
    }
    const graph = container.querySelector("[data-record-page-schedule-graph]");
    if (!(graph instanceof HTMLElement)) {
        return;
    }
    container.dataset.scheduleAxesRendered = "true";
    renderStaticTimelineAxes(graph);
    queueRainbowSegmentRender(graph);
}

function applyView(container, view) {
    const isTable = view === "table";
    container.classList.toggle(TABLE_CLASS, isTable);
    container.querySelectorAll("[data-schedule-view-toggle]").forEach((button) => {
        if (button instanceof HTMLElement) {
            button.setAttribute("aria-pressed", String(button.dataset.scheduleViewToggle === view));
        }
    });
    if (!isTable) {
        renderGraphOnce(container);
    }
}

function initRecordPageScheduleView() {
    const container = document.querySelector("[data-record-page-schedule]");
    if (!(container instanceof HTMLElement)) {
        return;
    }
    applyView(container, readStoredView());
}

document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const button = target.closest("[data-schedule-view-toggle]");
    if (!(button instanceof HTMLElement)) {
        return;
    }
    const container = button.closest("[data-record-page-schedule]");
    if (!(container instanceof HTMLElement)) {
        return;
    }
    event.preventDefault();
    const view = button.dataset.scheduleViewToggle === "table" ? "table" : "graph";
    applyView(container, view);
    storeView(view);
});

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initRecordPageScheduleView);
}
else {
    initRecordPageScheduleView();
}
