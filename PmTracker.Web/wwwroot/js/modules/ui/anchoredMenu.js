// anchoredMenu.js — rozbalovací menu ukotvené pod tlačítkem.
//
// Jeden mechanismus pro všechna taková menu v aplikaci. Do 2026-09-07 existoval dvakrát:
// recordActionsMenu.js (karta záznamu) a vyzvy/pnfMenu.js (přesun PNF) měly shodnou kostru
// a lišily se jen selektory a tím, co dělá položka. Chyba opravená v jednom se do druhého
// nepropsala, proto je logika tady jednou a konkrétní menu jsou jen konfigurace.
//
// Panel se mountuje do #floating-panel-root a pozicuje pod trigger přes sdílenou
// floating vrstvu (ui/floating.js) — vlastní pozicování se nepíše.
//
// Zavírání: klik na položku, Escape, klik mimo. Outside-close vyhodnocuje PŮVOD gesta
// (mousedown), ne click — tažení z menu ven by jinak retargetovalo click na společného
// předka a menu falešně zavřelo (memory feedback_modal_close_x_only_no_backdrop).
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { closeAllFloatingPanels, mountFloatingPanel, unmountFloatingPanel } from "./floating.js";

/** Právě otevřené menu — otevřené smí být vždy jen jedno. */
let openEntry = null; // { menu, trigger, panel }
let pointerDownTarget = null;

const registered = [];

export class AnchoredMenu {
    /**
     * @param {object} options
     * @param {string} options.triggerSelector Selektor tlačítka, které menu otevírá.
     * @param {string} options.panelSelector   Selektor panelu — musí být sourozenec hned za triggerem.
     * @param {string|null} [options.itemSelector]     Položka, kterou menu vykoná samo přes onItemActivate.
     * @param {number} [options.gap]                   Mezera mezi triggerem a panelem v px.
     * @param {(trigger: HTMLElement, expanded: boolean) => void} [options.onToggle]
     *        Doplňkový stav navázaný na otevření (např. třída na kartě záznamu).
     * @param {(item: HTMLElement, trigger: HTMLElement) => void} [options.onItemActivate]
     *        Akce položky. Volá se až po zavření menu, aby byl panel odmountovaný zpět.
     */
    constructor({
        triggerSelector,
        panelSelector,
        itemSelector = null,
        gap = 4,
        onToggle = null,
        onItemActivate = null,
    }) {
        this.triggerSelector = triggerSelector;
        this.panelSelector = panelSelector;
        this.itemSelector = itemSelector;
        this.gap = gap;
        this.onToggle = onToggle;
        this.onItemActivate = onItemActivate;
        registered.push(this);
    }

    /** Panel je sourozenec hned za triggerem — jinak menu neotevřeme. */
    resolvePanel(trigger) {
        const sibling = trigger.nextElementSibling;
        return sibling instanceof HTMLElement && sibling.matches(this.panelSelector) ? sibling : null;
    }

    setTriggerState(trigger, expanded) {
        trigger.setAttribute("aria-expanded", String(expanded));
        this.onToggle?.(trigger, expanded);
    }

    open(trigger) {
        const panel = this.resolvePanel(trigger);
        if (!panel) {
            return;
        }
        closeOpenMenu();
        closeAllFloatingPanels(document, panel);
        panel.hidden = false;
        mountFloatingPanel(panel, trigger, { gap: this.gap });
        this.setTriggerState(trigger, true);
        openEntry = { menu: this, trigger, panel };
    }
}

function closeOpenMenu() {
    if (!openEntry) {
        return;
    }
    const { menu, trigger, panel } = openEntry;
    openEntry = null;
    panel.hidden = true;
    unmountFloatingPanel(panel);
    if (trigger.isConnected) {
        menu.setTriggerState(trigger, false);
    }
}

function findTrigger(target) {
    for (const menu of registered) {
        const trigger = target.closest(menu.triggerSelector);
        if (trigger instanceof HTMLElement) {
            return { menu, trigger };
        }
    }
    return null;
}

function handleDocumentClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    // Resync: reload panelu i refreshPageScope volají closeAllFloatingPanels() mimo nás —
    // panel už může být skrytý a odmountovaný, jen stavová proměnná přežila.
    if (openEntry && openEntry.panel.hidden) {
        openEntry = null;
    }

    const found = findTrigger(target);
    if (found) {
        event.preventDefault();
        if (openEntry && openEntry.trigger === found.trigger) {
            closeOpenMenu();
        } else {
            found.menu.open(found.trigger);
        }
        return;
    }

    if (!openEntry) {
        return;
    }

    const { menu, trigger, panel } = openEntry;

    // Položka s vlastní akcí: menu ji vykoná samo. Zavíráme napřed, aby byl panel
    // odmountovaný zpět do svého místa dřív, než akce překreslí okolní DOM.
    if (menu.itemSelector && menu.onItemActivate) {
        const item = target.closest(menu.itemSelector);
        if (item instanceof HTMLElement && panel.contains(item)) {
            event.preventDefault();
            closeOpenMenu();
            menu.onItemActivate(item, trigger);
            return;
        }
    }

    // Ostatní kliky uvnitř panelu jen zavřou — akci vykoná vlastní handler položky
    // nebo odkaz (např. data-goto-schedule na kartě záznamu).
    if (panel.contains(target)) {
        closeOpenMenu();
        return;
    }

    // Outside-close jen když gesto ZAČALO mimo panel i trigger.
    const origin = pointerDownTarget;
    const originInside = origin instanceof Node && (panel.contains(origin) || trigger.contains(origin));
    if (!originInside) {
        closeOpenMenu();
    }
}

document.addEventListener("mousedown", (event) => {
    pointerDownTarget = event.target instanceof Node ? event.target : null;
}, true);
document.addEventListener("click", handleDocumentClick);
document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
        closeOpenMenu();
    }
});

// ── Konkrétní menu aplikace ──────────────────────────────────────────────────

// Karta záznamu: svislé menu akcí (šipka → / ↓).
new AnchoredMenu({
    triggerSelector: "[data-record-menu-trigger]",
    panelSelector: "[data-record-menu]",
    onToggle: (trigger, expanded) => {
        // Rotaci šipky řídí třída na kartě, ne atribut na triggeru — pm-button strhává
        // atributy z hostu, takže CSS na [aria-expanded] by se nechytlo.
        const card = trigger.closest(".record-card");
        if (card instanceof HTMLElement) {
            card.classList.toggle("record-actions-menu-open", expanded);
        }
    },
});

// Panel Výzev: menu ⋯ u řádku PNF (přesun do bufferu / jiné výzvy).
new AnchoredMenu({
    triggerSelector: "[data-vyzvy-menu-trigger]",
    panelSelector: "[data-vyzvy-menu]",
    itemSelector: "[data-vyzvy-presun]",
    onItemActivate: (item, trigger) => {
        // Panel menu je namountovaný v #floating-panel-root, takže z něj nahoru
        // k [data-vyzvy-panel] nedojdeme — bereme ho z triggeru, ten zůstává na místě.
        const panelElement = trigger.closest("[data-vyzvy-panel]");
        const cil = item.dataset.cilovaVyzvaId;
        window.pmVyzvy?.presunPnf?.(
            panelElement,
            item.dataset.externiOdkazId,
            cil === "" ? null : cil);
    },
});
