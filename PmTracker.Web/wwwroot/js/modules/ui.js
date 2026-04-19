import { debounce, isButtonLike, measureTextWidth, parseColorChannels, getContrastTextColor, pickSegmentLabel, setButtonDisabled } from "./utils.js";
import { buildProjectPrintFilterQueryParams, buildProjectPrintFilterSnapshot } from "./filters.js";

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

export const floatingPanelRegistry = new Set();

let globalFloatingRoot = null;

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

export function renderRainbowSegmentLabel(segment) {
    if (!(segment instanceof HTMLElement)) {
        return;
    }

    const fullLabel = segment.dataset.rainbowSegmentLabelFull || "";
    const shortLabel = segment.dataset.rainbowSegmentLabelShort || "";
    if (!fullLabel && !shortLabel) {
        return;
    }

    const width = segment.getBoundingClientRect().width;
    const computed = window.getComputedStyle(segment);
    const fontSpec = `${computed.fontWeight} ${computed.fontSize} ${computed.fontFamily}`;
    const selected = pickSegmentLabel(fullLabel, shortLabel, width, fontSpec);

    segment.textContent = selected;
    if (!selected) {
        segment.style.removeProperty("color");
        return;
    }

    segment.style.color = getContrastTextColor(computed.backgroundColor);
}

export function renderAllRainbowSegmentLabels(scope) {
    const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
    root
        .querySelectorAll(
            ".schedule-overview-segment[data-rainbow-segment-label-short], "
            + ".schedule-layered-segment[data-rainbow-segment-label-short], "
            + ".schedule-mini-gantt-segment[data-rainbow-segment-label-short]")
        .forEach((segment) => {
            renderRainbowSegmentLabel(segment);
        });
}

export function queueRainbowSegmentRender(scope) {
    window.requestAnimationFrame(() => renderAllRainbowSegmentLabels(scope));
}

export function getGlobalFloatingLayerRoot() {
    if (globalFloatingRoot instanceof HTMLElement && globalFloatingRoot.isConnected) {
        return globalFloatingRoot;
    }

    const root = document.createElement("div");
    root.className = "app-floating-root";
    root.setAttribute("data-app-floating-root", "true");
    root.setAttribute("aria-hidden", "true");
    document.body.appendChild(root);
    globalFloatingRoot = root;
    return root;
}

export function getFloatingLayerRoot(container) {
    const overlay = container instanceof Element
        ? container.closest(".modal-overlay")
        : null;

    if (overlay instanceof HTMLElement) {
        const modalRoot = overlay.querySelector("[data-modal-floating-root]");
        if (modalRoot instanceof HTMLElement) {
            return modalRoot;
        }
    }

    return getGlobalFloatingLayerRoot();
}

export function getFloatingPanelAnchor(panel) {
    if (!(panel instanceof HTMLElement)) {
        return null;
    }

    const storedAnchor = panel._pmtrackerFloatingAnchor;
    if (storedAnchor instanceof HTMLElement && storedAnchor.isConnected) {
        return storedAnchor;
    }

    const fallbackAnchor = panel.closest("[data-floating-anchor]");
    return fallbackAnchor instanceof HTMLElement ? fallbackAnchor : null;
}

export function applyFloatingPanelKind(panel, kind) {
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    panel.classList.remove(
        "floating-panel--person-search",
        "floating-panel--ad-search",
        "floating-panel--date",
        "floating-panel--time");

    switch (kind) {
        case "person-search":
            panel.classList.add("floating-panel--person-search");
            break;
        case "ad-search":
            panel.classList.add("floating-panel--ad-search");
            break;
        case "date":
            panel.classList.add("floating-panel--date");
            break;
        case "time":
            panel.classList.add("floating-panel--time");
            break;
        default:
            break;
    }
}

export function mountFloatingPanel(panel, anchor, options = {}) {
    if (!(panel instanceof HTMLElement) || !(anchor instanceof HTMLElement)) {
        return;
    }

    const root = getFloatingLayerRoot(anchor);
    const existingMount = panel._pmtrackerFloatingMount;
    const kind = options.kind
        || panel.dataset.floatingKind
        || "";
    const matchWidth = options.matchWidth === true
        || panel.dataset.floatingMatchWidth === "true";

    if (!existingMount) {
        const placeholder = document.createElement("span");
        placeholder.hidden = true;
        placeholder.style.display = "none";
        panel.parentNode?.insertBefore(placeholder, panel);

        panel._pmtrackerFloatingMount = {
            placeholder,
            originParent: panel.parentElement
        };
    }

    if (panel.parentElement !== root) {
        root.appendChild(panel);
    }

    panel._pmtrackerFloatingAnchor = anchor;
    panel._pmtrackerFloatingOptions = {
        gap: Number.isFinite(options.gap) ? options.gap : 8,
        flipVertical: options.flipVertical !== false,
        kind,
        matchWidth,
        lockVerticalSide: options.lockVerticalSide === true
    };

    panel.classList.add("floating-panel");
    panel.classList.toggle("floating-panel--match-anchor", matchWidth);
    applyFloatingPanelKind(panel, kind);
    floatingPanelRegistry.add(panel);
    positionFloatingPanel(panel, anchor, panel._pmtrackerFloatingOptions);
}

export function unmountFloatingPanel(panel) {
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    const mount = panel._pmtrackerFloatingMount;
    if (mount?.placeholder instanceof HTMLElement && mount.placeholder.parentNode) {
        mount.placeholder.parentNode.insertBefore(panel, mount.placeholder);
        mount.placeholder.remove();
    }

    panel.classList.remove(
        "floating-panel",
        "floating-panel--match-anchor",
        "floating-panel--person-search",
        "floating-panel--ad-search",
        "floating-panel--date",
        "floating-panel--time");
    panel.style.removeProperty("position");
    panel.style.removeProperty("left");
    panel.style.removeProperty("right");
    panel.style.removeProperty("top");
    panel.style.removeProperty("bottom");
    panel.style.removeProperty("width");
    panel.style.removeProperty("max-width");
    panel.style.removeProperty("min-width");
    panel.style.removeProperty("max-height");
    panel.style.removeProperty("overflow-y");
    panel.style.removeProperty("visibility");

    delete panel._pmtrackerFloatingMount;
    delete panel._pmtrackerFloatingAnchor;
    delete panel._pmtrackerFloatingOptions;
    delete panel._pmtrackerVerticalSide;
    floatingPanelRegistry.delete(panel);
}

export function closeAllFloatingPanels(scope, exceptPanel) {
    floatingPanelRegistry.forEach((panel) => {
        if (!(panel instanceof HTMLElement) || panel === exceptPanel) {
            return;
        }

        const anchor = getFloatingPanelAnchor(panel);
        if (scope instanceof Element || scope instanceof Document) {
            const scopeContainsPanel = scope.contains(panel);
            const scopeContainsAnchor = anchor instanceof HTMLElement && scope.contains(anchor);
            if (!scopeContainsPanel && !scopeContainsAnchor) {
                return;
            }
        }

        panel.hidden = true;
        unmountFloatingPanel(panel);
    });
}

export function resolveRecordEditorFloatingBoundary(anchor, boundary, kind) {
    if (!(anchor instanceof HTMLElement) || !boundary) {
        return boundary;
    }

    if (kind !== "date" && kind !== "time" && kind !== "person-search") {
        return boundary;
    }

    const modalContainer = anchor.closest("[data-modal-container]");
    if (!(modalContainer instanceof HTMLElement)) {
        return boundary;
    }

    const recordEditorForm = anchor.closest('form[data-record-editor-form="true"]');
    if (!(recordEditorForm instanceof HTMLElement)) {
        return boundary;
    }

    const actionBar = recordEditorForm.querySelector(".record-editor-actions");
    if (!(actionBar instanceof HTMLElement)) {
        return boundary;
    }

    const actionBarRect = actionBar.getBoundingClientRect();
    if (actionBarRect.height <= 0) {
        return boundary;
    }

    const adjustedBottom = Math.min(boundary.bottom, actionBarRect.top - 8);
    if (adjustedBottom <= boundary.top + 72) {
        return boundary;
    }

    return {
        ...boundary,
        bottom: adjustedBottom
    };
}

export function positionFloatingPanel(panel, anchor, options = {}) {
    if (!(panel instanceof HTMLElement) || !(anchor instanceof HTMLElement) || panel.hidden) {
        return;
    }

    const gap = Number.isFinite(options.gap) ? options.gap : 8;
    const matchWidth = options.matchWidth === true || panel.dataset.floatingMatchWidth === "true";
    const kind = typeof options.kind === "string" ? options.kind : "";
    const lockVerticalSide = options.lockVerticalSide === true;
    const viewportBoundary = {
        left: 8,
        right: window.innerWidth - 8,
        top: 8,
        bottom: window.innerHeight - 8
    };
    const modalContainer = anchor.closest("[data-modal-container]");
    const baseBoundary = modalContainer instanceof HTMLElement
        ? (() => {
            const modalRect = modalContainer.getBoundingClientRect();
            return {
                left: Math.max(viewportBoundary.left, modalRect.left + 8),
                right: Math.min(viewportBoundary.right, modalRect.right - 8),
                top: Math.max(viewportBoundary.top, modalRect.top + 8),
                bottom: Math.min(viewportBoundary.bottom, modalRect.bottom - 8)
            };
        })()
        : viewportBoundary;
    const boundary = resolveRecordEditorFloatingBoundary(anchor, baseBoundary, kind);
    const anchorRect = anchor.getBoundingClientRect();
    if (anchorRect.width <= 0 || anchorRect.height <= 0) {
        return;
    }

    panel.style.position = "fixed";
    panel.style.visibility = "hidden";
    panel.style.left = "0px";
    panel.style.top = "0px";
    panel.style.right = "auto";
    panel.style.bottom = "auto";
    panel.style.maxHeight = "";
    panel.style.overflowY = "";
    panel.style.maxWidth = `${Math.max(boundary.right - boundary.left, 0)}px`;
    panel.style.width = matchWidth
        ? `${Math.min(Math.round(anchorRect.width), Math.max(boundary.right - boundary.left, 0))}px`
        : "";
    panel.style.minWidth = matchWidth ? `${Math.min(Math.round(anchorRect.width), Math.max(boundary.right - boundary.left, 0))}px` : "";

    let panelRect = panel.getBoundingClientRect();
    const availableBelow = Math.max(0, boundary.bottom - anchorRect.bottom - gap);
    const availableAbove = Math.max(0, anchorRect.top - boundary.top - gap);
    const fitsBelow = availableBelow >= panelRect.height;
    const fitsAbove = availableAbove >= panelRect.height;
    let shouldOpenAbove = false;
    const storedVerticalSide = panel._pmtrackerVerticalSide === "above" || panel._pmtrackerVerticalSide === "below"
        ? panel._pmtrackerVerticalSide
        : null;

    if (!lockVerticalSide) {
        delete panel._pmtrackerVerticalSide;
    }

    if (lockVerticalSide && storedVerticalSide) {
        shouldOpenAbove = storedVerticalSide === "above";
    } else {
        if (fitsBelow) {
            shouldOpenAbove = false;
        } else if (fitsAbove) {
            shouldOpenAbove = true;
        } else {
            shouldOpenAbove = availableAbove > availableBelow;
        }

        if (lockVerticalSide) {
            panel._pmtrackerVerticalSide = shouldOpenAbove ? "above" : "below";
        }
    }

    const availableOnSelectedSide = shouldOpenAbove ? availableAbove : availableBelow;
    if (availableOnSelectedSide > 0 && availableOnSelectedSide < panelRect.height) {
        const maxHeight = Math.floor(Math.max(availableOnSelectedSide - 4, 0));
        if (maxHeight > 0) {
            panel.style.maxHeight = `${maxHeight}px`;
            panel.style.overflowY = "auto";
            panelRect = panel.getBoundingClientRect();
        }
    }

    let left = anchorRect.left;
    if (left + panelRect.width > boundary.right) {
        left = boundary.right - panelRect.width;
    }
    if (left < boundary.left) {
        left = boundary.left;
    }

    let top = shouldOpenAbove
        ? anchorRect.top - panelRect.height - gap
        : anchorRect.bottom + gap;

    if (top + panelRect.height > boundary.bottom) {
        top = boundary.bottom - panelRect.height;
    }
    if (top < boundary.top) {
        top = boundary.top;
    }

    panel.style.left = `${Math.round(left)}px`;
    panel.style.top = `${Math.round(top)}px`;
    panel.style.visibility = "";
}

export function repositionFloatingPanels() {
    floatingPanelRegistry.forEach((panel) => {
        if (!(panel instanceof HTMLElement) || panel.hidden) {
            return;
        }

        const anchor = getFloatingPanelAnchor(panel);
        if (!(anchor instanceof HTMLElement) || !anchor.isConnected) {
            panel.hidden = true;
            unmountFloatingPanel(panel);
            return;
        }

        positionFloatingPanel(panel, anchor, panel._pmtrackerFloatingOptions || {});
    });
}

export const queueFloatingPanelReposition = debounce(() => {
    repositionFloatingPanels();
}, 16);

export function isInteractionInsideFloatingControl(target, anchor, panel) {
    if (!(target instanceof Element)) {
        return false;
    }

    return (anchor instanceof HTMLElement && anchor.contains(target))
        || (panel instanceof HTMLElement && panel.contains(target));
}
