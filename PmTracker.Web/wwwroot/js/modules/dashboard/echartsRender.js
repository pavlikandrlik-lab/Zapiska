// dashboard/echartsRender.js — vykreslí ChartData kontejnery základního reportu přes
// Apache ECharts (SVG renderer = vektor pro tisk/PDF). Knihovna je self-hostovaná offline
// (wwwroot/lib/echarts), žádné CDN. Server posílá ChartData v data-chart-json (camelCase,
// kind jako string) — viz ChartJson.cs / _Chart.cshtml.
import * as echarts from "../../../lib/echarts/echarts.esm.min.js";

function optionFor(d) {
    const cats = d.categories || [];
    const series = d.series || [];
    switch (d.kind) {
        case "Bar":
            return {
                tooltip: {}, xAxis: { type: "category", data: cats }, yAxis: { type: "value" },
                series: series.map(s => ({ name: s.label, type: "bar", data: s.values }))
            };
        case "StackedBar":
            return {
                tooltip: {}, legend: {}, xAxis: { type: "category", data: cats }, yAxis: { type: "value" },
                series: series.map(s => ({ name: s.label, type: "bar", stack: "total", data: s.values }))
            };
        case "Line":
            return {
                tooltip: {}, xAxis: { type: "category", data: cats }, yAxis: { type: "value" },
                series: series.map(s => ({ name: s.label, type: "line", data: s.values }))
            };
        case "Pie": {
            const s0 = series[0] || { values: [] };
            return {
                tooltip: { trigger: "item" }, legend: {},
                series: [{ type: "pie", radius: "65%", data: cats.map((c, i) => ({ name: c, value: s0.values[i] ?? 0 })) }]
            };
        }
        default:
            return null;
    }
}

function renderOne(el) {
    if (el.dataset.echartReady === "true") return;
    let data;
    try { data = JSON.parse(el.dataset.chartJson || "{}"); } catch { return; }
    const option = optionFor(data);
    if (!option) return;
    // Lazy/skrytý panel (display:none nebo neaktivní tab) → kontejner má nulovou šířku.
    // ECharts by se vykreslil do 0×0; počkáme na další frame, až panel dostane rozměr.
    if (el.clientWidth === 0) { window.requestAnimationFrame(() => renderOne(el)); return; }
    const chart = echarts.init(el, null, { renderer: "svg" });
    chart.setOption(option);
    el.dataset.echartReady = "true";
    el._echart = chart;
}

export function initEchartsReport(scope) {
    const root = scope instanceof Element ? scope : document;
    root.querySelectorAll("[data-echart]").forEach(renderOne);
}

let resizeBound = false;
export function bindEchartsResize() {
    if (resizeBound) return;
    resizeBound = true;
    window.addEventListener("resize", () => {
        document.querySelectorAll("[data-echart]").forEach(el => el._echart && el._echart.resize());
    });
}
