import { debounce } from "../utils.js";

export const floatingPanelRegistry = new Set();

let globalFloatingRoot = null;

export function getGlobalFloatingLayerRoot() {
    if (globalFloatingRoot instanceof HTMLElement && globalFloatingRoot.isConnected) {
        return globalFloatingRoot;
    }

    // Fáze 2E: prefer statický element z _Layout.cshtml (výkon + prediktabilita)
    const existing = document.getElementById("floating-panel-root");
    if (existing instanceof HTMLElement) {
        globalFloatingRoot = existing;
        return existing;
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
    // Fáze 2E: floating pickery (person, datetime) nejsou mountovány uvnitř
    // gov-dialog (shadow DOM vs. floating positioning kolize). Vždy vrátíme
    // globální root #floating-panel-root v _Layout.cshtml. Parametr `container`
    // je tu pro zpětnou kompatibilitu API, ale ignorovaný.
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
