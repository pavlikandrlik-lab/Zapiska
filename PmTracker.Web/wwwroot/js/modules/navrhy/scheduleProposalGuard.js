// Ověření „Navrhnout změnu harmonogramu" před přechodem na editor návrhu (uživatel 2026-10-08):
// když k záznamu už čeká návrh, ukáže dialog místo chybové stránky. Server kontrolu drží i sám
// (přímý odkaz, souběh) — vrátí zpět s hláškou (NavrhyController.CreateScheduleProposal).
import { appendCurrentAsUser } from "../navigationShared.js";
import { isAdapterClick } from "../eventBus.js";

const LINK_SELECTOR = "[data-schedule-proposal-check-url]";
const CLOSE_DELAY_MS = 400; // gov-dialog close transition (stejně jako recordEditor/draft.js)
const AVAILABILITY_TIMEOUT_MS = 5000; // review fix M4: fetch bez timeoutu nechával tlačítko "mrtvé"
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

function openBlockedDialog({ title, message, proposalUrl }, trigger) {
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

    // gov-dialog 4.7.0 vykresluje nadpis jen ze slotu "title" (slot "label" tu neexistuje).
    const heading = document.createElement("span");
    heading.setAttribute("slot", "title");
    heading.textContent = title;
    dialog.appendChild(heading);

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
        window.setTimeout(() => {
            dialog.remove();
            // Review fix M5 (2026-10-12): fokus po zavření zpět na prvek, kterým se dialog otevřel
            // (stejný vzor jako closeRecordEditorCloseGuard({ restoreFocus }) v recordEditor/draft.js).
            if (trigger instanceof HTMLElement && trigger.isConnected) {
                trigger.focus({ preventScroll: true });
            }
        }, CLOSE_DELAY_MS);
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

/**
 * Review fix M4 (2026-10-12): dotaz na dostupnost s timeoutem. Vlastní AbortController +
 * setTimeout (ne AbortSignal.timeout — širší kompatibilita), timeoutPromise soupeří s fetchem přes
 * Promise.race, takže i fetchImpl, který signal nerespektuje (nikdy se nevyřeší), po timeoutMs
 * doběhne na "navigate" — "server rozhodne" (Review Focus 2). Beze zbytečného čekání na fetch.
 */
export function fetchScheduleProposalDecision(checkUrl, { fetchImpl = fetch, timeoutMs = AVAILABILITY_TIMEOUT_MS } = {}) {
    const controller = new AbortController();
    let timeoutId;
    const timeoutPromise = new Promise((resolve) => {
        timeoutId = setTimeout(() => {
            controller.abort();
            resolve({ action: "navigate" });
        }, timeoutMs);
    });

    const fetchPromise = (async () => {
        try {
            const response = await fetchImpl(checkUrl, {
                headers: { Accept: "application/json" },
                credentials: "same-origin",
                signal: controller.signal
            });
            if (!response.ok) {
                return { action: "navigate" };
            }
            return resolveScheduleProposalCheck(await response.json());
        } catch {
            // Síť / neplatná odpověď / abort — pokračuje se na editor, rozhodne server.
            return { action: "navigate" };
        }
    })();

    return Promise.race([fetchPromise, timeoutPromise]).finally(() => clearTimeout(timeoutId));
}

async function handleClick(event) {
    // Review fix I1 (2026-10-12): ignorovat syntetický click z gov-click adaptéru (eventBus.js).
    // Ten click nenese skutečné modifikátory (gov-button ho emituje stejně při Ctrl/Cmd/Shift
    // kliku), takže by se omylem vyhodnotil jako "plain left click" i při kliku do nové karty.
    // Rozhoduje jen původní trusted click (myš na vnitřním <a>, nebo klávesnice Enter).
    if (isAdapterClick(event)) {
        return;
    }
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
        const decision = await fetchScheduleProposalDecision(appendCurrentAsUser(checkUrl));
        if (decision.action === "dialog") {
            openBlockedDialog(decision, link);
            return;
        }
    } finally {
        checkInFlight = false;
    }
    // Navigaci teď dělá skript místo odkazu, proto i tady appendCurrentAsUser (jako u dotazu
    // výše) — jinak se v dev prostředí ztratí ?asUser a editor spadne na chybějící přihlášení.
    window.location.assign(appendCurrentAsUser(href));
}

// Capture fáze: menu karty má data-stop-propagation (bubling by se sem nedostal).
// stopPropagation se nevolá — menu se po kliknutí samo zavře.
if (typeof document !== "undefined") {
    document.addEventListener("click", handleClick, true);
}
