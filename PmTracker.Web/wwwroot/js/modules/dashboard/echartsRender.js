// dashboard/echartsRender.js — vykreslí ChartData kontejnery ([data-echart]) přes Apache ECharts
// (SVG renderer = vektor pro tisk/PDF). Knihovna je self-hostovaná offline (wwwroot/lib/echarts),
// žádné CDN. Server posílá ChartData v data-chart-json (camelCase, kind jako string) — viz
// ChartJson.cs / _Chart.cshtml. Mapování na option je v echartsOption.js, barvy v echartsTheme.js.
import * as echarts from "../../../lib/echarts/echarts.esm.min.js";
import { optionFor, vyskaGrafu } from "./echartsOption.js";
import { readTheme } from "./echartsTheme.js";

// Sledované kontejnery. Po výměně HTML panelu (změna období) staré zmizí z DOM — uklízí je sweep().
const sledovane = new Set();

function parse(el) {
    try {
        return JSON.parse(el.dataset.chartJson || "{}");
    }
    catch {
        return null;
    }
}

function dispose(el) {
    if (el._echart) {
        el._echart.dispose();
        el._echart = null;
    }
    delete el.dataset.echartReady;
}

function render(el) {
    // Skrytý panel (neaktivní záložka) má nulovou šířku — ECharts by kreslil do 0×0.
    // Vykreslí se, až ResizeObserver ohlásí skutečný rozměr.
    if (el.dataset.echartReady === "true" || el.clientWidth === 0) {
        return;
    }
    const data = parse(el);
    const option = data ? optionFor(data, readTheme()) : null;
    if (!option) {
        return;
    }
    if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        option.animation = false;
    }
    el.style.height = `${vyskaGrafu(data)}px`;
    const chart = echarts.init(el, null, { renderer: "svg" });
    chart.setOption(option);
    el._echart = chart;
    el.dataset.echartReady = "true";
}

const pozorovatel = typeof ResizeObserver === "function"
    ? new ResizeObserver((entries) => {
        for (const entry of entries) {
            const el = entry.target;
            if (el.dataset.echartReady === "true") {
                if (el._echart && el.clientWidth > 0) {
                    el._echart.resize();
                }
            }
            else {
                render(el);
            }
        }
    })
    : null;

function sweep() {
    for (const el of sledovane) {
        if (!el.isConnected) {
            if (pozorovatel) {
                pozorovatel.unobserve(el);
            }
            dispose(el);
            sledovane.delete(el);
        }
    }
}

function sleduj(el) {
    if (!(el instanceof HTMLElement) || sledovane.has(el)) {
        return;
    }
    sledovane.add(el);
    if (pozorovatel) {
        pozorovatel.observe(el);
    }
    render(el);
}

export function initEchartsReport(scope) {
    sweep();
    const root = scope instanceof Element ? scope : document;
    root.querySelectorAll("[data-echart]").forEach(sleduj);
}

function prekresliVse() {
    for (const el of sledovane) {
        if (el.isConnected && el.dataset.echartReady === "true") {
            dispose(el);
            render(el);
        }
    }
}

let lifecycleBound = false;

/** Jednorázově: změna velikosti okna, přepnutí motivu (data-theme) a načtení lazy panelu. */
export function bindEchartsLifecycle() {
    if (lifecycleBound) {
        return;
    }
    lifecycleBound = true;

    // Záloha pro prohlížeč bez ResizeObserver.
    window.addEventListener("resize", () => {
        for (const el of sledovane) {
            if (el._echart) {
                el._echart.resize();
            }
            else {
                render(el);
            }
        }
    });

    // Barvy grafů jsou vyřešené hodnoty tokenů — po přepnutí motivu se musí přečíst znovu.
    new MutationObserver(prekresliVse)
        .observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });

    // Lazy panel (záložka dashboardu, změna období) přinese nové kontejnery.
    document.addEventListener("pm:panel-loaded", (event) => {
        initEchartsReport(event.target instanceof Element ? event.target : document);
    });
}
