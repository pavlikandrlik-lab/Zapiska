import { reportClientDiagnostic } from "./utils.js";

const modalRoot = document.getElementById("modal-root");
const modalFocusableSelector = [
    "a[href]",
    "button:not([disabled])",
    "input:not([disabled]):not([type='hidden'])",
    "select:not([disabled])",
    "textarea:not([disabled])",
    "[contenteditable='true']",
    "[tabindex]:not([tabindex='-1'])"
].join(", ");

const modalRuntime = {
    closeAllFloatingPanels: null,
    initRecordFormEnhancements: null,
    initPermissionMetadataBindings: null
};

export const modalState = {
    lastTrigger: null
};

export function configureModalRuntime(runtime = {}) {
    if (typeof runtime.closeAllFloatingPanels === "function") {
        modalRuntime.closeAllFloatingPanels = runtime.closeAllFloatingPanels;
    }

    if (typeof runtime.initRecordFormEnhancements === "function") {
        modalRuntime.initRecordFormEnhancements = runtime.initRecordFormEnhancements;
    }

    if (typeof runtime.initPermissionMetadataBindings === "function") {
        modalRuntime.initPermissionMetadataBindings = runtime.initPermissionMetadataBindings;
    }
}

function getActiveModalOverlay() {
    if (!(modalRoot instanceof HTMLElement)) {
        return null;
    }

    return modalRoot.querySelector(".modal-overlay");
}

export function getActiveModalContainer() {
    const overlay = getActiveModalOverlay();
    if (!(overlay instanceof HTMLElement)) {
        return null;
    }

    return overlay.querySelector("[data-modal-container]");
}

export function isModalOpen() {
    return modalRoot instanceof HTMLElement
        && modalRoot.getAttribute("aria-hidden") !== "true"
        && modalRoot.childElementCount > 0;
}

export function getFocusableElementsWithinModal(container) {
    if (!(container instanceof HTMLElement)) {
        return [];
    }

    return Array.from(container.querySelectorAll(modalFocusableSelector))
        .filter((element) => element instanceof HTMLElement)
        .filter((element) => {
            if (element.hasAttribute("disabled")) {
                return false;
            }
            if (element.getAttribute("aria-hidden") === "true") {
                return false;
            }

            return element.getClientRects().length > 0;
        });
}

export function focusInitialModalElement() {
    const modal = getActiveModalContainer();
    if (!(modal instanceof HTMLElement)) {
        return;
    }

    const autofocusCandidate = modal.querySelector("[autofocus]");
    if (autofocusCandidate instanceof HTMLElement && !autofocusCandidate.hasAttribute("disabled")) {
        autofocusCandidate.focus({ preventScroll: true });
        return;
    }

    const focusable = getFocusableElementsWithinModal(modal);
    if (focusable.length > 0) {
        focusable[0].focus({ preventScroll: true });
        return;
    }

    modal.focus({ preventScroll: true });
}

export function setModalContent(content, trigger) {
    if (!(modalRoot instanceof HTMLElement)) {
        return;
    }

    modalRuntime.closeAllFloatingPanels?.();
    modalRoot.innerHTML = "";
    modalRoot.appendChild(content);
    modalRoot.style.pointerEvents = "auto";
    modalRoot.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open");
    modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
    modalRuntime.initRecordFormEnhancements?.(modalRoot);
    modalRuntime.initPermissionMetadataBindings?.(modalRoot);
    window.requestAnimationFrame(() => {
        focusInitialModalElement();
    });
}

export function closeModal() {
    if (!(modalRoot instanceof HTMLElement)) {
        return;
    }

    const focusTarget = modalState.lastTrigger;
    modalRuntime.closeAllFloatingPanels?.();
    modalRoot.innerHTML = "";
    modalRoot.style.pointerEvents = "none";
    modalRoot.setAttribute("aria-hidden", "true");
    document.body.classList.remove("modal-open");
    modalState.lastTrigger = null;
    if (focusTarget instanceof HTMLElement && focusTarget.isConnected) {
        focusTarget.focus({ preventScroll: true });
    }
}

export async function openUrlModal(url, trigger) {
    if (!url) {
        return;
    }

    try {
        const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        const html = await response.text();
        const wrapper = document.createElement("div");
        wrapper.innerHTML = html;
        setModalContent(wrapper, trigger);
    } catch (error) {
        reportClientDiagnostic("modal-load-failed", { url });
        if (modalRoot instanceof HTMLElement) {
            modalRoot.innerHTML = `
                <div class="modal-overlay" aria-hidden="false">
                    <div class="modal-container" role="dialog" aria-modal="true" tabindex="-1">
                        <p>Nepodařilo se načíst obsah dialogu.</p>
                        <div class="modal-actions">
                            <button type="button" class="btn btn-secondary" data-modal-close>Zavřít</button>
                        </div>
                    </div>
                </div>`;
            modalRoot.style.pointerEvents = "auto";
            modalRoot.setAttribute("aria-hidden", "false");
            document.body.classList.add("modal-open");
            modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
            focusInitialModalElement();
        }
    }
}

export function trapFocusInModal(event) {
    const modal = getActiveModalContainer();
    if (!(modal instanceof HTMLElement)) {
        return;
    }

    const focusable = getFocusableElementsWithinModal(modal);
    if (focusable.length === 0) {
        event.preventDefault();
        modal.focus({ preventScroll: true });
        return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const activeElement = document.activeElement;
    const activeInsideModal = activeElement instanceof Element && modal.contains(activeElement);

    if (event.shiftKey) {
        if (!activeInsideModal || activeElement === first) {
            event.preventDefault();
            last.focus({ preventScroll: true });
        }
        return;
    }

    if (!activeInsideModal || activeElement === last) {
        event.preventDefault();
        first.focus({ preventScroll: true });
    }
}

export function isModalOverlayClickTarget(target) {
    const overlay = getActiveModalOverlay();
    return overlay instanceof HTMLElement && target === overlay;
}
