import { appendCurrentAsUser } from "./navigationShared.js";
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
    // Fáze 2E: modal root je gov-dialog (ne .modal-overlay div)
    return modalRoot.querySelector("gov-dialog");
}

export function getActiveModalContainer() {
    // Fáze 2E: data-modal-container je na gov-dialog samotném (v _ModalLayout),
    // ne na vnitřním .modal-container divu.
    const dialog = getActiveModalOverlay();
    return dialog instanceof HTMLElement ? dialog : null;
}

export function isModalOpen() {
    return modalRoot instanceof HTMLElement
        && modalRoot.getAttribute("aria-hidden") !== "true"
        && modalRoot.childElementCount > 0;
}

function activateInsertedGovDialog() {
    if (!(modalRoot instanceof HTMLElement)) {
        return;
    }

    const dialog = modalRoot.querySelector("gov-dialog");
    if (!(dialog instanceof HTMLElement)) {
        return;
    }

    // Explicitní `open="true"` pro případ, že Razor renderoval atribut ale
    // gov-dialog se ještě neupgradoval (custom element se upgraduje asynchronně).
    dialog.setAttribute("open", "true");
    if (typeof dialog.show === "function") {
        try { dialog.show(); } catch { /* .show() může throw při duplicitním volání nebo pre-hydration */ }
    }
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
    // Vestigial z .modal-overlay éry — ponecháváme defensivně (neovlivňuje gov-dialog).
    modalRoot.style.pointerEvents = "auto";
    modalRoot.setAttribute("aria-hidden", "false");
    document.body.classList.add("modal-open");
    modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
    activateInsertedGovDialog();

    const activeDialog = modalRoot.querySelector("gov-dialog[data-modal-container]");
    if (activeDialog instanceof HTMLElement) {
        // Čekej tick na custom element upgrade
        requestAnimationFrame(() => reparentFloatingRootIntoModal(activeDialog));
    }

    modalRuntime.initRecordFormEnhancements?.(modalRoot);
    modalRuntime.initPermissionMetadataBindings?.(modalRoot);
    window.requestAnimationFrame(() => {
        focusInitialModalElement();
    });
}

// Úprava #7 (2026-04-20): Floating portal (#floating-panel-root) je light DOM
// element v _Layout.cshtml. Gov-dialog má vlastní shadow DOM stacking context →
// picker panely render-ují za modalem. Řešení: při openModal přesun root DO
// aktivního gov-dialogu, při closeModal vrátit zpět (původní parent + position).
let floatingRootOriginalParent = null;
let floatingRootOriginalNextSibling = null;

export function reparentFloatingRootIntoModal(dialog) {
    if (!(dialog instanceof HTMLElement)) {
        return;
    }
    const root = document.getElementById("floating-panel-root");
    if (!(root instanceof HTMLElement)) {
        return;
    }
    // Už uvnitř modalu? (idempotent guard)
    if (root.parentElement === dialog) {
        return;
    }
    if (floatingRootOriginalParent === null) {
        floatingRootOriginalParent = root.parentElement;
        floatingRootOriginalNextSibling = root.nextSibling;
    }
    dialog.prepend(root);
}

export function restoreFloatingRoot() {
    const root = document.getElementById("floating-panel-root");
    if (!(root instanceof HTMLElement) || floatingRootOriginalParent === null) {
        return;
    }
    if (floatingRootOriginalNextSibling && floatingRootOriginalNextSibling.parentNode === floatingRootOriginalParent) {
        floatingRootOriginalParent.insertBefore(root, floatingRootOriginalNextSibling);
    } else {
        floatingRootOriginalParent.appendChild(root);
    }
    floatingRootOriginalParent = null;
    floatingRootOriginalNextSibling = null;
}

export function closeModal() {
    if (!(modalRoot instanceof HTMLElement)) {
        return;
    }

    const focusTarget = modalState.lastTrigger;
    modalRuntime.closeAllFloatingPanels?.();

    // Fáze 2E: gov-dialog musí dostat removeAttribute("open") (a volitelně
    // .close()) před unmountem, aby proběhl její cleanup (focus restore,
    // backdrop teardown). Try/catch kolem .close() kvůli defensivnímu volání
    // před hydratací.
    const dialog = modalRoot.querySelector("gov-dialog");
    if (dialog instanceof HTMLElement) {
        dialog.removeAttribute("open");
        if (typeof dialog.close === "function") {
            try { dialog.close(); } catch { /* ignorovat — už může být zavřený */ }
        }
    }

    restoreFloatingRoot();
    modalRoot.innerHTML = "";
    // Vestigial z .modal-overlay éry — ponecháváme defensivně (neovlivňuje gov-dialog).
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
        const response = await fetch(appendCurrentAsUser(url), { headers: { "X-Requested-With": "XMLHttpRequest" } });
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
                <gov-dialog open="true" block-close="true" block-backdrop-close="true" data-modal-container data-modal-variant="default" aria-labelledby="modal-error-title" tabindex="-1">
                    <h2 id="modal-error-title" class="sr-only">Chyba načtení dialogu</h2>
                    <p>Nepodařilo se načíst obsah dialogu.</p>
                    <div class="modal-actions">
                        <button type="button" class="btn btn-secondary" data-modal-close>Zavřít</button>
                    </div>
                </gov-dialog>`;
            // Vestigial z .modal-overlay éry — ponecháváme defensivně (neovlivňuje gov-dialog).
            modalRoot.style.pointerEvents = "auto";
            modalRoot.setAttribute("aria-hidden", "false");
            document.body.classList.add("modal-open");
            modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
            activateInsertedGovDialog();
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
