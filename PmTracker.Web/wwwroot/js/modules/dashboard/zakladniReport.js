// dashboard/zakladniReport.js — výběr období Statistik → znovunačtení panelu záložky.
// Jde přes loadDashboardPanel: ten doplní asUser, ukáže načítání i chybu se „Zkusit znovu"
// a pošle pm:panel-loaded, na který echartsRender.js inicializuje grafy.
import { loadDashboardPanel } from "../dashboard.js";

/** Čistá funkce: URL panelu pro zvolený rok a kvartál (prázdný kvartál = celý rok). */
export function obdobiUrl(baseUrl, rok, kvartal) {
    const qs = new URLSearchParams({ rok: String(rok) });
    if (kvartal) {
        qs.set("kvartal", String(kvartal));
    }
    return `${baseUrl}?${qs.toString()}`;
}

let bound = false;

export function initZakladniReport() {
    if (bound) {
        return;
    }
    bound = true;

    document.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        const shell = target.closest("[data-zakladni-obdobi]");
        if (!(shell instanceof HTMLElement)) {
            return;
        }

        const baseUrl = shell.getAttribute("data-url") || "";
        const rok = shell.querySelector("[data-zakladni-obdobi-rok]")?.value || "";
        const kvartal = shell.querySelector("[data-zakladni-obdobi-kvartal]")?.value || "";
        const panel = shell.closest("[data-dashboard-panel]");
        if (!baseUrl || !rok || !(panel instanceof HTMLElement)) {
            return;
        }

        void loadDashboardPanel(panel, { url: obdobiUrl(baseUrl, rok, kvartal) });
    });
}
