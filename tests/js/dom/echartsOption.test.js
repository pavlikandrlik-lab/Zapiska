import { test } from "node:test";
import assert from "node:assert/strict";

import { formatHodnota, optionFor, vyskaGrafu } from "../../../PmTracker.Web/wwwroot/js/modules/dashboard/echartsOption.js";
import { sestavTema } from "../../../PmTracker.Web/wwwroot/js/modules/dashboard/echartsTheme.js";

const TOKENY = {
    "--app-chart-1": "#111111",
    "--app-chart-2": "#222222",
    "--app-chart-neutral": "#333333",
    "--app-chart-neutral-strong": "#444444",
    "--background-block-primary": "#ffffff",
    "--text-primary": "#000001",
    "--text-secondary": "#000002",
    "--border-subtle": "#000003",
    "--border-subtlest": "#000004",
    "--background-status-success": "#00aa00",
    "--background-status-error": "#aa0000"
};
const tema = sestavTema((name) => TOKENY[name] ?? "");

const graf = (kind, series, extra = {}) => ({
    kind,
    title: "T",
    categories: ["A", "B", "C"],
    series,
    ...extra
});
const jedna = [{ label: "Počet", values: [3, 0, 1] }];
const dve = [{ label: "Původní", values: [1, 2, 3] }, { label: "Poslední", values: [4, 5, 6] }];

test("Bar s jednou sérií: kategorie na ose X, bez legendy, zaoblený horní konec, první barva palety", () => {
    const o = optionFor(graf("Bar", jedna), tema);

    assert.equal(o.xAxis.type, "category");
    assert.deepEqual(o.xAxis.data, ["A", "B", "C"]);
    assert.equal(o.yAxis.type, "value");
    assert.equal(o.series.length, 1);
    assert.equal(o.series[0].type, "bar");
    assert.equal(o.series[0].itemStyle.color, "#111111");
    assert.deepEqual(o.series[0].itemStyle.borderRadius, [4, 4, 0, 0]);
    assert.equal(o.legend.show, false, "jedna série legendu nepotřebuje — pojmenuje ji nadpis");
});

test("Bar se dvěma sériemi je seskupený a má legendu; barvy v pevném pořadí", () => {
    const o = optionFor(graf("Bar", dve), tema);

    assert.equal(o.legend.show, true);
    assert.deepEqual(o.series.map((s) => s.itemStyle.color), ["#111111", "#222222"]);
    assert.ok(o.series.every((s) => s.stack === undefined), "seskupený, ne skládaný");
});

test("StackedBar skládá do jednoho sloupce, mezi segmenty je mezera v barvě plochy", () => {
    const o = optionFor(graf("StackedBar", dve), tema);

    assert.ok(o.series.every((s) => s.stack === "total"));
    assert.ok(o.series.every((s) => s.itemStyle.borderColor === "#ffffff" && s.itemStyle.borderWidth === 2));
    assert.equal(o.legend.show, true);
});

test("ColorToken série se vyřeší na barvu tokenu; neznámý token spadne na paletu", () => {
    const o = optionFor(graf("StackedBar", [
        { label: "Hotovo", values: [1, 1, 1], colorToken: "--background-status-success" },
        { label: "Jiné", values: [1, 1, 1], colorToken: "--neexistuje" }
    ]), tema);

    assert.equal(o.series[0].itemStyle.color, "#00aa00");
    assert.equal(o.series[1].itemStyle.color, "#222222", "barva podle pořadí série, ne podle toho, kolik sérií token mělo");
});

test("HorizontalBar: kategorie na ose Y shora dolů, hodnoty přímo u sloupců", () => {
    const o = optionFor(graf("HorizontalBar", jedna), tema);

    assert.equal(o.yAxis.type, "category");
    assert.equal(o.yAxis.inverse, true);
    assert.equal(o.xAxis.type, "value");
    assert.deepEqual(o.series[0].itemStyle.borderRadius, [0, 4, 4, 0]);
    assert.equal(o.series[0].label.show, true);
});

test("StackedHorizontalBar: skládaný s legendou, kategorie na ose Y", () => {
    const o = optionFor(graf("StackedHorizontalBar", dve), tema);

    assert.equal(o.yAxis.type, "category");
    assert.ok(o.series.every((s) => s.stack === "total"));
    assert.equal(o.legend.show, true);
});

test("Line: čára 2 px, značky 8 px, legenda od dvou sérií", () => {
    const o = optionFor(graf("Line", dve), tema);

    assert.ok(o.series.every((s) => s.type === "line" && s.lineStyle.width === 2 && s.symbolSize === 8));
    assert.equal(o.legend.show, true);
    assert.equal(optionFor(graf("Line", jedna), tema).legend.show, false);
});

test("Pie: nulové výseče se nekreslí, barva jde s kategorií (ne s pořadím po filtru)", () => {
    const o = optionFor(graf("Pie", jedna), tema);

    assert.equal(o.series[0].type, "pie");
    assert.deepEqual(o.series[0].data.map((d) => [d.name, d.value, d.itemStyle.color]), [["A", 3, "#111111"], ["C", 1, "#333333"]]);
});

test("Donut: prstenec se součtem uprostřed a barvami podle categoryColorTokens", () => {
    const o = optionFor(graf("Donut", jedna, { categoryColorTokens: ["--background-status-success", null, "--app-chart-neutral-strong"] }), tema);

    const radius = o.series[0].radius;
    assert.ok(Array.isArray(radius) && radius.length === 2 && parseFloat(radius[0]) < parseFloat(radius[1]));
    assert.deepEqual(o.series[0].data.map((d) => d.itemStyle.color), ["#00aa00", "#444444"]);
    assert.equal(o.title.text, "4", "uprostřed je součet hodnot");
    assert.equal(o.title.top, "middle");
    assert.equal(optionFor(graf("Pie", jedna), tema).title, undefined, "plný koláč součet uprostřed nemá");
});

test("Karty a neznámý druh ECharts nekreslí", () => {
    assert.equal(optionFor(graf("StatCards", []), tema), null);
    assert.equal(optionFor(graf("Kpi", []), tema), null);
    assert.equal(optionFor(graf("Nesmysl", jedna), tema), null);
    assert.equal(optionFor({}, tema), null);
});

test("Tooltip formátuje hodnotu česky a s jednotkou", () => {
    const o = optionFor(graf("Bar", jedna, { unit: "%" }), tema);

    assert.match(o.tooltip.valueFormatter(12.5), /^12,5\s%$/);
    assert.equal(o.yAxis.max, 100, "procenta mají osu do 100");
});

test("formatHodnota: tisíce, desetiny, jednotka, nečíslo", () => {
    assert.match(formatHodnota(1234567), /^1\s234\s567$/);
    assert.equal(formatHodnota(2.25), "2,3");
    assert.match(formatHodnota(1500, "Kč"), /^1\s500\sKč$/);
    assert.equal(formatHodnota(undefined), "—");
    assert.equal(formatHodnota(Number.NaN), "—");
});

test("Osa počtů nemá necelé dílky; osa s desetinnými hodnotami ano", () => {
    assert.equal(optionFor(graf("Bar", jedna), tema).yAxis.minInterval, 1);
    assert.equal(optionFor(graf("Bar", [{ label: "Ø", values: [1.5, 2, 3] }]), tema).yAxis.minInterval, undefined);
});

test("vyskaGrafu: vodorovné sloupce rostou s počtem kategorií, ostatní mají pevnou výšku", () => {
    const hodne = { kind: "HorizontalBar", categories: Array.from({ length: 12 }, (_, i) => `K${i}`), series: jedna };

    assert.equal(vyskaGrafu(graf("Bar", jedna)), 280);
    assert.equal(vyskaGrafu({ kind: "HorizontalBar", categories: ["A"], series: jedna }), 160, "spodní mez, ať graf s jedním řádkem nezmizí");
    assert.ok(vyskaGrafu(hodne) > 280);
});
