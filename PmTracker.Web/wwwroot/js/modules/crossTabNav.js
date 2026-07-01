// crossTabNav.js — vzájemný překlik záznam ⇄ harmonogram (in-page, bez reloadu).
//
// Tlačítko `data-goto-schedule="<recordId>"` (na kartě záznamu) přepne na záložku
// harmonogram a sjede na `.schedule-card` daného záznamu. Tlačítko
// `data-goto-record="<recordId>"` (na kartě harmonogramu) přepne na záložku záznamy
// a sjede na `.record-card`. Hodnota hooku = id záznamu.
//
// Reuse existující tab infrastruktury z projectTabs.js: setActiveTab přepne aktivní
// panel, ensureProjectTabLoaded případně lazy-loadne harmonogram (vrací Promise).
import { ensureProjectTabLoaded, setActiveTab, syncTabQuery } from "./projectTabs.js";

const HIGHLIGHT_CLASS = "cross-nav-highlight";
const HIGHLIGHT_MS = 1600;

function scrollToCard(selector, attempt = 0) {
    const card = document.querySelector(selector);
    if (!(card instanceof HTMLElement)) {
        // Cílová karta nemusí být hned v DOMu (harmonogram se lazy-loaduje) → zkus po dalším frame.
        if (attempt < 20) {
            window.requestAnimationFrame(() => scrollToCard(selector, attempt + 1));
        }
        return;
    }

    card.scrollIntoView({ behavior: "smooth", block: "start" });
    card.classList.add(HIGHLIGHT_CLASS);
    window.setTimeout(() => card.classList.remove(HIGHLIGHT_CLASS), HIGHLIGHT_MS);
}

async function navigateToTab(tabName, cardSelector) {
    setActiveTab(tabName);
    syncTabQuery(tabName);
    try {
        await ensureProjectTabLoaded(tabName);
    }
    catch {
        // I při chybě lazy-loadu zkusíme sjet na to, co v DOMu je.
    }

    scrollToCard(cardSelector);
}

export function initCrossTabNav() {
    // Jen na detailu projektu (kde jsou záložky). Listener je na document → přežije
    // výměny panelů (lazy-load) i refresh jednotlivých karet.
    if (!document.querySelector(".tab[data-tab]")) {
        return;
    }

    document.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const toSchedule = target.closest("[data-goto-schedule]");
        if (toSchedule instanceof HTMLElement) {
            const recordId = (toSchedule.dataset.gotoSchedule || "").trim();
            if (recordId) {
                event.preventDefault();
                navigateToTab("harmonogram", `.schedule-card[data-schedule-record-id="${CSS.escape(recordId)}"]`);
            }
            return;
        }

        const toRecord = target.closest("[data-goto-record]");
        if (toRecord instanceof HTMLElement) {
            const recordId = (toRecord.dataset.gotoRecord || "").trim();
            if (recordId) {
                event.preventDefault();
                navigateToTab("zaznamy", `.record-card[data-record-id="${CSS.escape(recordId)}"]`);
            }
        }
    });
}
