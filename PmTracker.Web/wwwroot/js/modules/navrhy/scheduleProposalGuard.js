// Ověření „Navrhnout změnu harmonogramu" před přechodem na editor návrhu (uživatel 2026-10-08):
// když k záznamu už čeká návrh, ukáže dialog místo chybové stránky. Server kontrolu drží i sám
// (přímý odkaz, souběh) — vrátí zpět s hláškou (NavrhyController.CreateScheduleProposal).
import { appendCurrentAsUser } from "../navigationShared.js";

const LINK_SELECTOR = "[data-schedule-proposal-check-url]";
const CLOSE_DELAY_MS = 400; // gov-dialog close transition (stejně jako recordEditor/draft.js)
let checkInFlight = false;

/** Co udělat s odpovědí ověření. Neznámá odpověď = pokračovat, rozhodne server. */
export function resolveScheduleProposalCheck(result) {
    if (!result || result.canCreate !== false) {
        return { action: "navigate" };
    }
    return {
        action: "dialog",
        title: result.title || "Návrh nelze založit",
        message: result.message || "",
        proposalUrl: typeof result.proposalUrl === "string" && result.proposalUrl ? result.proposalUrl : null
    };
}

/** Klik do nové karty (Ctrl/Cmd/Shift, prostřední tlačítko) nechává prohlížeči. */
export function isPlainLeftClick(event) {
    return (event.button ?? 0) === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey;
}

function createButton(label, { color, type, href }) {
    const button = document.createElement("gov-button");
    button.setAttribute("color", color);
    button.setAttribute("type", type);
    button.setAttribute("size", "m");
    if (href) {
        button.setAttribute("href", href);
    }
    button.textContent = label;
    return button;
}

function openBlockedDialog({ title, message, proposalUrl }) {
    const dialog = document.createElement("gov-dialog");
    dialog.setAttribute("role", "alertdialog");
    dialog.setAttribute("label-tag", "h3");
    dialog.setAttribute("block-backdrop-close", "true");
    dialog.setAttribute("accessible-close-label", "Zavřít dialog");
    dialog.setAttribute("data-schedule-proposal-blocked-dialog", "");

    // Stav chyby podle DS: ikona ve slotu icon (gov-dialog 4.7.0 nemá atribut status).
    const icon = document.createElement("gov-icon");
    icon.setAttribute("slot", "icon");
    icon.setAttribute("type", "components");
    icon.setAttribute("name", "exclamation-triangle-fill");
    icon.setAttribute("color", "error");
    dialog.appendChild(icon);

    const label = document.createElement("span");
    label.setAttribute("slot", "label");
    label.textContent = title;
    dialog.appendChild(label);

    const text = document.createElement("p");
    text.textContent = message;
    dialog.appendChild(text);

    const footer = document.createElement("div");
    footer.setAttribute("slot", "footer");
    footer.className = "schedule-proposal-blocked-dialog-footer";
    const okButton = createButton("Rozumím", { color: "primary", type: "solid" });
    footer.appendChild(okButton);
    if (proposalUrl) {
        footer.appendChild(createButton("Zobrazit čekající návrh",
            { color: "primary", type: "outlined", href: appendCurrentAsUser(proposalUrl) }));
    }
    dialog.appendChild(footer);

    let closed = false;
    const close = () => {
        if (closed) return;
        closed = true;
        dialog.open = false;
        window.setTimeout(() => dialog.remove(), CLOSE_DELAY_MS);
    };
    okButton.addEventListener("click", close);
    dialog.addEventListener("gov-close", close);

    document.body.appendChild(dialog);
    window.requestAnimationFrame(() => {
        dialog.open = true;
        window.setTimeout(() => okButton.isConnected && okButton.focus({ preventScroll: true }), 80);
    });
    return dialog;
}

async function handleClick(event) {
    const link = event.target instanceof Element ? event.target.closest(LINK_SELECTOR) : null;
    if (!link || !isPlainLeftClick(event)) {
        return;
    }
    const href = link.getAttribute("href");
    const checkUrl = link.getAttribute("data-schedule-proposal-check-url");
    if (!href || !checkUrl) {
        return;
    }

    event.preventDefault();
    if (checkInFlight) {
        return;
    }
    checkInFlight = true;
    try {
        const response = await fetch(appendCurrentAsUser(checkUrl), {
            headers: { Accept: "application/json" },
            credentials: "same-origin"
        });
        const decision = response.ok
            ? resolveScheduleProposalCheck(await response.json())
            : { action: "navigate" };
        if (decision.action === "dialog") {
            openBlockedDialog(decision);
            return;
        }
    } catch {
        // Síť / neplatná odpověď — pokračuje se na editor, rozhodne server.
    } finally {
        checkInFlight = false;
    }
    window.location.assign(href);
}

// Capture fáze: menu karty má data-stop-propagation (bubling by se sem nedostal).
// stopPropagation se nevolá — menu se po kliknutí samo zavře.
if (typeof document !== "undefined") {
    document.addEventListener("click", handleClick, true);
}
