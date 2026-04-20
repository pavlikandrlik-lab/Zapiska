import { isButtonLike, setButtonDisabled } from "../utils.js";
import { buildProjectPrintFilterQueryParams, buildProjectPrintFilterSnapshot } from "../filters.js";

const printFormatStorageKey = "pmtracker.print.preferredFormat";

export const printState = {
    popover: null,
    trigger: null,
    hoverTimerId: 0
};

// Reposition hook — recordEditor chooser (popover kotvený nad tlačítkem
// přes positionPrintChooser) se musí po scroll/resize přepozicovat.
// Nelze importovat recordEditorState přímo (vzniká cyklická závislost
// ui.js ↔ recordEditor.js), proto runtime registrace: recordEditor.js
// sem při otevření chooseru zaregistruje svůj popover a trigger.
export const auxFloatingChoosers = new Set();

export function registerFloatingChooser(popover, trigger) {
    if (!(popover instanceof HTMLElement) || !(trigger instanceof HTMLElement)) {
        return;
    }
    auxFloatingChoosers.add({ popover, trigger });
}

export function unregisterFloatingChooser(popover) {
    if (!(popover instanceof HTMLElement)) {
        return;
    }
    for (const entry of auxFloatingChoosers) {
        if (entry.popover === popover) {
            auxFloatingChoosers.delete(entry);
        }
    }
}

export function getStoredPrintFormat() {
    const value = localStorage.getItem(printFormatStorageKey);
    if (value === "pdf" || value === "word") {
        return value;
    }
    return null;
}

export function setStoredPrintFormat(format) {
    if (format !== "pdf" && format !== "word") {
        return;
    }

    localStorage.setItem(printFormatStorageKey, format);
    refreshPrintPreferenceUi();
}

export function clearStoredPrintFormat() {
    localStorage.removeItem(printFormatStorageKey);
    refreshPrintPreferenceUi();
}

export function getPrintFormatLabel(format) {
    if (format === "pdf") {
        return "PDF";
    }

    if (format === "word") {
        return "WORD";
    }

    return "není nastaven";
}

export function refreshPrintPreferenceUi() {
    const preferred = getStoredPrintFormat();
    document.querySelectorAll("[data-print-preference-current]").forEach((element) => {
        element.textContent = getPrintFormatLabel(preferred);
    });

    document.querySelectorAll("[data-print-preference-reset]").forEach((element) => {
        if (isButtonLike(element)) {
            setButtonDisabled(element, preferred === null);
        }
    });
}

export function clearPrintHoverTimer() {
    if (printState.hoverTimerId) {
        window.clearTimeout(printState.hoverTimerId);
        printState.hoverTimerId = 0;
    }
}

export function closePrintChooser(options) {
    const settings = options || {};
    const restoreFocus = Boolean(settings.restoreFocus);
    const trigger = printState.trigger;

    if (printState.popover instanceof HTMLElement) {
        printState.popover.remove();
    }

    printState.popover = null;
    printState.trigger = null;
    clearPrintHoverTimer();

    if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
        trigger.focus({ preventScroll: true });
    }
}

export function resolvePrintUrl(trigger, format, options) {
    const settings = options || {};
    if (!(trigger instanceof Element)) {
        return "";
    }

    if (format === "word") {
        if (typeof settings.wordUrlOverride === "string" && settings.wordUrlOverride) {
            return settings.wordUrlOverride;
        }
        return trigger.getAttribute("data-print-word-url") || "";
    }

    if (typeof settings.pdfUrlOverride === "string" && settings.pdfUrlOverride) {
        return settings.pdfUrlOverride;
    }

    return trigger.getAttribute("data-print-pdf-url") || trigger.getAttribute("href") || "";
}

function isProjectPrintTrigger(trigger) {
    return trigger instanceof HTMLElement && trigger.dataset.projectPrintTrigger === "true";
}

function shouldPromptForProjectPrintFilters(trigger) {
    return isProjectPrintTrigger(trigger) && buildProjectPrintFilterSnapshot().hasRelevantFilters;
}

function buildProjectPrintUrl(trigger, format, useCurrentFilters) {
    const baseUrl = resolvePrintUrl(trigger, format);
    if (!baseUrl) {
        return "";
    }

    const url = new URL(baseUrl, window.location.origin);
    const { params } = buildProjectPrintFilterQueryParams(useCurrentFilters);
    params.forEach((value, key) => {
        url.searchParams.set(key, value);
    });

    return url.toString();
}

export function openPrintUrl(url) {
    if (!url) {
        return;
    }
    const link = document.createElement("a");
    link.href = url;
    link.target = "_blank";
    link.rel = "noopener";
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();
}

export function positionPrintChooser(popover, trigger) {
    if (!(popover instanceof HTMLElement) || !(trigger instanceof HTMLElement)) {
        return;
    }

    const rect = trigger.getBoundingClientRect();
    const width = popover.offsetWidth;
    const height = popover.offsetHeight;
    const gap = 8;

    let left = rect.right - width;
    let top = rect.bottom + gap;

    if (left < gap) {
        left = gap;
    }
    if (left + width > window.innerWidth - gap) {
        left = Math.max(gap, window.innerWidth - width - gap);
    }

    if (top + height > window.innerHeight - gap) {
        top = rect.top - height - gap;
    }
    if (top < gap) {
        top = gap;
    }

    popover.style.left = `${Math.round(left)}px`;
    popover.style.top = `${Math.round(top)}px`;
}

export function handlePrintChoice(trigger, format, shouldRemember, options) {
    const url = resolvePrintUrl(trigger, format, options);
    if (!url) {
        return;
    }

    if (shouldRemember) {
        setStoredPrintFormat(format);
    }

    openPrintUrl(url);
}

function handleProjectPrintScopeChoice(trigger, useCurrentFilters) {
    const preferredFormat = getStoredPrintFormat();
    if (preferredFormat) {
        const preferredUrl = buildProjectPrintUrl(trigger, preferredFormat, useCurrentFilters);
        if (preferredUrl) {
            closePrintChooser({ restoreFocus: false });
            openPrintUrl(preferredUrl);
            return;
        }
    }

    showPrintChooser(trigger, {
        quickMode: false,
        pdfUrlOverride: buildProjectPrintUrl(trigger, "pdf", useCurrentFilters),
        wordUrlOverride: buildProjectPrintUrl(trigger, "word", useCurrentFilters)
    });
}

function createProjectPrintScopeChooser(trigger) {
    const label = trigger.getAttribute("data-print-label") || "Tisk projektu";

    const popover = document.createElement("div");
    popover.className = "print-format-popover";
    popover.setAttribute("role", "dialog");
    popover.setAttribute("aria-modal", "false");
    popover.setAttribute("data-print-popover", "true");
    popover.setAttribute("tabindex", "-1");

    const title = document.createElement("h3");
    title.className = "print-format-title";
    title.textContent = "Použít aktuální filtry?";
    popover.appendChild(title);

    const subtitle = document.createElement("p");
    subtitle.className = "print-format-subtitle";
    subtitle.textContent = label;
    popover.appendChild(subtitle);

    const actions = document.createElement("div");
    actions.className = "print-format-actions";

    const filteredButton = document.createElement("button");
    filteredButton.type = "button";
    filteredButton.className = "btn small";
    filteredButton.textContent = "Použít aktuální filtry";
    filteredButton.setAttribute("data-print-filter-scope", "current");
    actions.appendChild(filteredButton);

    const fullProjectButton = document.createElement("button");
    fullProjectButton.type = "button";
    fullProjectButton.className = "btn small ghost";
    fullProjectButton.textContent = "Tisknout celý projekt";
    fullProjectButton.setAttribute("data-print-filter-scope", "all");
    actions.appendChild(fullProjectButton);

    popover.appendChild(actions);

    const note = document.createElement("p");
    note.className = "print-format-note";
    note.textContent = "Volba se použije jen pro tento tisk a neukládá se.";
    popover.appendChild(note);

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "print-format-close";
    closeButton.setAttribute("aria-label", "Zavřít výběr filtru pro tisk projektu");
    closeButton.textContent = "×";
    popover.appendChild(closeButton);

    popover.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (target.closest(".print-format-close")) {
            event.preventDefault();
            closePrintChooser({ restoreFocus: true });
            return;
        }

        const choice = target.closest("[data-print-filter-scope]");
        if (!choice) {
            return;
        }

        event.preventDefault();
        const scope = choice.getAttribute("data-print-filter-scope");
        handleProjectPrintScopeChoice(trigger, scope === "current");
    });

    return popover;
}

export function createPrintChooser(trigger, options) {
    const settings = options || {};
    const quickMode = Boolean(settings.quickMode);
    const label = trigger.getAttribute("data-print-label") || "Tisk";

    const popover = document.createElement("div");
    popover.className = "print-format-popover";
    popover.setAttribute("role", "dialog");
    popover.setAttribute("aria-modal", "false");
    popover.setAttribute("data-print-popover", "true");
    popover.setAttribute("tabindex", "-1");

    const title = document.createElement("h3");
    title.className = "print-format-title";
    title.textContent = quickMode ? "Jednorázová volba formátu" : "Vyberte formát tisku";
    popover.appendChild(title);

    const subtitle = document.createElement("p");
    subtitle.className = "print-format-subtitle";
    subtitle.textContent = label;
    popover.appendChild(subtitle);

    const actions = document.createElement("div");
    actions.className = "print-format-actions";

    const pdfButton = document.createElement("button");
    pdfButton.type = "button";
    pdfButton.className = "btn small";
    pdfButton.textContent = "PDF";
    pdfButton.setAttribute("data-print-choice", "pdf");
    actions.appendChild(pdfButton);

    const wordButton = document.createElement("button");
    wordButton.type = "button";
    wordButton.className = "btn small";
    wordButton.textContent = "WORD";
    wordButton.setAttribute("data-print-choice", "word");
    actions.appendChild(wordButton);

    popover.appendChild(actions);

    const rememberLabel = document.createElement("label");
    rememberLabel.className = "print-format-remember";
    const rememberCheckbox = document.createElement("input");
    rememberCheckbox.type = "checkbox";
    rememberCheckbox.setAttribute("data-print-remember", "true");
    rememberLabel.appendChild(rememberCheckbox);
    rememberLabel.append(quickMode
        ? " Nastavit jako novou preferenci pro tento počítač"
        : " Zapamatovat pro tento počítač");
    popover.appendChild(rememberLabel);

    const note = document.createElement("p");
    note.className = "print-format-note";
    note.textContent = quickMode
        ? "Ve výchozím stavu jde o jednorázovou volbu. Uloženou preferenci změníte jen zaškrtnutím volby výše."
        : "Volba se ukládá pouze pro tento počítač/prohlížeč.";
    popover.appendChild(note);

    const closeButton = document.createElement("button");
    closeButton.type = "button";
    closeButton.className = "print-format-close";
    closeButton.setAttribute("aria-label", "Zavřít výběr formátu tisku");
    closeButton.textContent = "×";
    popover.appendChild(closeButton);

    popover.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        if (target.closest(".print-format-close")) {
            event.preventDefault();
            closePrintChooser({ restoreFocus: true });
            return;
        }

        const choice = target.closest("[data-print-choice]");
        if (!choice) {
            return;
        }

        event.preventDefault();
        const format = choice.getAttribute("data-print-choice");
        if (format !== "pdf" && format !== "word") {
            return;
        }

        const remember = rememberCheckbox.checked;
        closePrintChooser({ restoreFocus: false });
        handlePrintChoice(trigger, format, remember, settings);
    });

    return popover;
}

export function showPrintChooser(trigger, options) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    closePrintChooser({ restoreFocus: false });

    const popover = createPrintChooser(trigger, options);
    document.body.appendChild(popover);
    positionPrintChooser(popover, trigger);
    printState.popover = popover;
    printState.trigger = trigger;

    const firstAction = popover.querySelector("[data-print-choice], [data-print-filter-scope]");
    if (firstAction instanceof HTMLElement) {
        firstAction.focus({ preventScroll: true });
    } else {
        popover.focus({ preventScroll: true });
    }
}

function showProjectPrintScopeChooser(trigger) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    closePrintChooser({ restoreFocus: false });

    const popover = createProjectPrintScopeChooser(trigger);
    document.body.appendChild(popover);
    positionPrintChooser(popover, trigger);
    printState.popover = popover;
    printState.trigger = trigger;

    const firstAction = popover.querySelector("[data-print-filter-scope]");
    if (firstAction instanceof HTMLElement) {
        firstAction.focus({ preventScroll: true });
    } else {
        popover.focus({ preventScroll: true });
    }
}

export function handlePrintTriggerClick(trigger) {
    if (!(trigger instanceof HTMLElement)) {
        return;
    }

    clearPrintHoverTimer();
    if (shouldPromptForProjectPrintFilters(trigger)) {
        showProjectPrintScopeChooser(trigger);
        return;
    }

    const preferredFormat = getStoredPrintFormat();
    if (preferredFormat) {
        const preferredUrl = resolvePrintUrl(trigger, preferredFormat);
        if (preferredUrl) {
            closePrintChooser({ restoreFocus: false });
            openPrintUrl(preferredUrl);
            return;
        }
    }

    showPrintChooser(trigger, { quickMode: false });
}

export function initPrintFormatChooser() {
    refreshPrintPreferenceUi();

    document.addEventListener("pointerover", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const trigger = target.closest("[data-print-trigger]");
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        if (event.relatedTarget instanceof Element && trigger.contains(event.relatedTarget)) {
            return;
        }

        if (event instanceof PointerEvent && event.pointerType !== "mouse") {
            return;
        }

        if (shouldPromptForProjectPrintFilters(trigger)) {
            return;
        }

        if (!getStoredPrintFormat()) {
            return;
        }

        clearPrintHoverTimer();
        printState.hoverTimerId = window.setTimeout(() => {
            showPrintChooser(trigger, { quickMode: true });
        }, 2000);
    });

    document.addEventListener("pointerout", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const trigger = target.closest("[data-print-trigger]");
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        if (event.relatedTarget instanceof Element && trigger.contains(event.relatedTarget)) {
            return;
        }

        clearPrintHoverTimer();
    });

    const repositionAuxChoosers = () => {
        for (const entry of auxFloatingChoosers) {
            if (entry.popover instanceof HTMLElement
                && entry.popover.isConnected
                && entry.trigger instanceof HTMLElement
                && entry.trigger.isConnected) {
                positionPrintChooser(entry.popover, entry.trigger);
            } else {
                auxFloatingChoosers.delete(entry);
            }
        }
    };

    window.addEventListener("scroll", () => {
        if (printState.popover instanceof HTMLElement && printState.trigger instanceof HTMLElement) {
            positionPrintChooser(printState.popover, printState.trigger);
        }

        repositionAuxChoosers();
    }, true);

    window.addEventListener("resize", () => {
        if (printState.popover instanceof HTMLElement && printState.trigger instanceof HTMLElement) {
            positionPrintChooser(printState.popover, printState.trigger);
        }

        repositionAuxChoosers();
    });
}
