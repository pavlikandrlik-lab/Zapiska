// dashboard/zakladniReport.js — selektor období → reload partial reportu
import { initEchartsReport } from "./echartsRender.js";

export function initZakladniReport() {
    document.addEventListener("change", async (event) => {
        const target = event.target;
        if (!(target instanceof Element)) return;
        const shell = target.closest("[data-zakladni-obdobi]");
        if (!(shell instanceof HTMLElement)) return;

        const url = shell.getAttribute("data-url") || "";
        const rok = shell.querySelector("[data-zakladni-obdobi-rok]")?.value || "";
        const kvartal = shell.querySelector("[data-zakladni-obdobi-kvartal]")?.value || "";
        if (!url || !rok) return;

        const qs = new URLSearchParams({ rok });
        if (kvartal) qs.set("kvartal", kvartal);

        const report = shell.closest("[data-zakladni-report]");
        if (!(report instanceof HTMLElement)) return;
        try {
            const html = await fetch(`${url}?${qs.toString()}`, { headers: { "X-Requested-With": "XMLHttpRequest" } })
                .then((r) => r.text());
            report.outerHTML = html;
            // outerHTML výměnou je původní `report` odpojený; nový report má čerstvé
            // [data-echart] kontejnery bez echartReady → re-init ECharts nad ním.
            initEchartsReport(document.querySelector("[data-zakladni-report]"));
        } catch {
            /* tichý fail — uživatel zkusí znovu */
        }
    });
}
