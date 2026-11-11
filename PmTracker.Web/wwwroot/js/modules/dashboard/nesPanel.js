// Záložka NES v prodlení (spec 2026-10-15 §10): filtr podle typu záznamu a reakce na dlaždice
// souhrnného pásu. Rozbalení řádku řeší sdílený mechanismus v projectDashboard.js.
// Panel se načítá líně, proto je vše delegované z document.

// Čistá funkce (tests/js/dom/nesPanelFilter.test.js). Typ v datech může být CHAR(5) s mezerami.
export function jeRadekViditelny(typRadku, vybranyTyp) {
    const filtr = String(vybranyTyp ?? "").trim().toUpperCase();
    if (!filtr) {
        return true;
    }
    return String(typRadku ?? "").trim().toUpperCase() === filtr;
}

function applyTypFilter(panel, typ) {
    panel.querySelectorAll("[data-nes-filter-typ]").forEach((btn) => {
        const active = (btn.getAttribute("data-nes-filter-typ") ?? "") === typ;
        btn.classList.toggle("active", active);
        btn.setAttribute("aria-pressed", active ? "true" : "false");
    });

    panel.querySelectorAll("[data-nes-row]").forEach((row) => {
        if (!(row instanceof HTMLElement)) {
            return;
        }
        const visible = jeRadekViditelny(row.dataset.typ, typ);
        row.hidden = !visible;
        if (visible) {
            return;
        }
        // Skrytý řádek nesmí nechat viset rozbalený detail.
        const detail = row.nextElementSibling;
        if (detail instanceof HTMLElement && detail.matches("[data-nes-detail]")) {
            detail.hidden = true;
        }
        row.classList.remove("expanded");
        row.setAttribute("aria-expanded", "false");
    });
}

function handleClick(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }
    const btn = target.closest("[data-nes-filter-typ]");
    if (!(btn instanceof HTMLButtonElement) || btn.disabled) {
        return;
    }
    const panel = btn.closest("[data-nes-panel]");
    if (panel instanceof HTMLElement) {
        applyTypFilter(panel, btn.getAttribute("data-nes-filter-typ") ?? "");
    }
}

// Dlaždice souhrnu: „Tickety v prodlení" → tabulka bez filtru, „Hrozí prodlení" → rozbalená sekce.
function handleDashboardFilter(event) {
    const detail = event.detail || {};
    if (detail.panel !== "nes") {
        return;
    }
    const panel = document.querySelector("[data-nes-panel]");
    if (!(panel instanceof HTMLElement)) {
        return;
    }

    let cil = null;
    if (detail.filter === "hrozi") {
        cil = panel.querySelector('[data-nes-sekce="hrozi"]');
        if (cil instanceof HTMLDetailsElement) {
            cil.open = true;
        }
    }
    else if (detail.filter === "v-prodleni") {
        applyTypFilter(panel, "");
        cil = panel.querySelector("[data-nes-tabulka]");
    }

    if (cil instanceof HTMLElement) {
        const klidne = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        cil.scrollIntoView({ behavior: klidne ? "auto" : "smooth", block: "start" });
    }
}

let bound = false;

export function initNesPanel() {
    if (bound) {
        return;
    }
    bound = true;
    document.addEventListener("click", handleClick);
    document.addEventListener("pm:dashboard-filter", handleDashboardFilter);
}
