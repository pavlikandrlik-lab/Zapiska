/**
 * Spec 2026-10-22 R10: procházení záznamů klávesami ← a → na stránce záznamu.
 *
 * Cíle čte z [data-record-sequence-nav] (data-prev-url / data-next-url). Jsou to stejné adresy
 * jako u tlačítek Předchozí / Další; prázdné = kraj seznamu, nepřetáčí se. Klávesy nereagují:
 *   - při psaní (pole, výběr, editor vyjádření);
 *   - v pruhu záložek;
 *   - v otevřeném okně;
 *   - s modifikátory (Alt+← je v prohlížeči „zpět“).
 * Side-effect import v bootstrap.js; při importu nesahá na DOM (testy bez DOM).
 */
import { appendCurrentAsUser } from "./navigationShared.js";

const NAV_SELECTOR = "[data-record-sequence-nav]";
const EDITABLE_SELECTOR = [
    "input", "textarea", "select", "[contenteditable='']", "[contenteditable='true']", "[role='tablist']",
    "gov-form-input", "gov-form-select", "gov-form-textarea", "gov-form-autocomplete"
].join(", ");

/** Kam klávesa vede: "prev", "next" nebo null. Čistá funkce. */
export function resolveSequenceKey(event, { editable, dialogOpen }) {
    if (!event || event.defaultPrevented || event.isComposing) {
        return null;
    }
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
        return null;
    }
    if (editable || dialogOpen) {
        return null;
    }
    if (event.key === "ArrowLeft") {
        return "prev";
    }
    if (event.key === "ArrowRight") {
        return "next";
    }
    return null;
}

/** Fokus je v prvku, kterému šipky patří (psaní, výběr, editor Quill, záložky). */
export function isEditableTarget(target) {
    if (!target || typeof target.closest !== "function") {
        return false;
    }
    if (target.isContentEditable === true) {
        return true;
    }
    return target.closest(EDITABLE_SELECTOR) !== null;
}

/** Otevřené okno: modal aplikace (#modal-root s obsahem) nebo otevřený gov-dialog. */
export function isDialogOpen(doc) {
    const modalRoot = doc.getElementById("modal-root");
    if (modalRoot && modalRoot.getAttribute("aria-hidden") !== "true" && modalRoot.childElementCount > 0) {
        return true;
    }
    return Array.from(doc.querySelectorAll("gov-dialog"))
        .some((dialog) => dialog.open === true || dialog.getAttribute("open") === "true");
}

function initRecordSequenceKeys() {
    const nav = document.querySelector(NAV_SELECTOR);
    if (!(nav instanceof HTMLElement)) {
        return;
    }

    document.addEventListener("keydown", (event) => {
        // composedPath: fokus uvnitř shadow DOM (gov komponenty) se jinak hlásí jako host.
        const path = typeof event.composedPath === "function" ? event.composedPath() : [];
        const target = path.length > 0 ? path[0] : event.target;
        const direction = resolveSequenceKey(event, {
            editable: isEditableTarget(target),
            dialogOpen: isDialogOpen(document)
        });
        if (!direction) {
            return;
        }
        const url = direction === "prev" ? nav.dataset.prevUrl : nav.dataset.nextUrl;
        if (!url) {
            return;
        }
        event.preventDefault();
        window.location.assign(appendCurrentAsUser(url));
    });
    // Klávesy fungují až po načtení modulu — značka pro testy (vzor data-app-date-ready).
    nav.dataset.recordSequenceKeysReady = "true";
}

if (typeof document !== "undefined") {
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initRecordSequenceKeys);
    } else {
        initRecordSequenceKeys();
    }
}
