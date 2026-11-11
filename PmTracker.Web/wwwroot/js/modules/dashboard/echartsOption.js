// dashboard/echartsOption.js — mapování ChartData (JSON ze serveru) na ECharts option.
// Čisté funkce bez DOM a bez knihovny ECharts, testované v tests/js/dom/echartsOption.test.js.
// Pravidla vzhledu (spec 2026-10-15 §12): jedna osa hodnot, zaoblené konce sloupců 4 px,
// mezera 2 px mezi segmenty v barvě plochy, čáry 2 px, legenda od dvou sérií, přímé popisky
// jen u prstence/koláče a vodorovných sloupců, texty v barvě textu (ne v barvě série).

const cislo = new Intl.NumberFormat("cs-CZ", { maximumFractionDigits: 1 });
const kompaktni = new Intl.NumberFormat("cs-CZ", { notation: "compact", maximumFractionDigits: 1 });

const VYSKA_GRAFU = 280;
const VYSKA_RADKU = 32;
// Do tolika kategorií se na svislé ose ukážou všechny popisky (koše stáří, typy, subsystémy); víc = prořídnou.
const VSECHNY_POPISKY_DO = 6;

/** Hodnota česky (tisíce, nejvýš jedno desetinné místo) s jednotkou; nečíslo = pomlčka. */
export function formatHodnota(v, unit) {
    if (typeof v !== "number" || !Number.isFinite(v)) {
        return "—";
    }
    const text = cislo.format(v);
    return unit ? `${text} ${unit}` : text;
}

function formatOsa(v, unit) {
    const text = kompaktni.format(v);
    return unit === "%" ? `${text} %` : text;
}

/** Výška kontejneru grafu: vodorovné sloupce rostou s počtem kategorií, ostatní mají pevnou výšku. */
export function vyskaGrafu(d) {
    if (d && (d.kind === "HorizontalBar" || d.kind === "StackedHorizontalBar")) {
        const radku = (d.categories || []).length;
        const legenda = (d.series || []).length >= 2 ? 28 : 0;
        return Math.max(160, radku * VYSKA_RADKU + 56 + legenda);
    }
    return VYSKA_GRAFU;
}

function zaklad(tema, { legenda, tooltip, unit, vodorovnePopisky = false, praveMisto = 16 }) {
    return {
        color: tema.color,
        textStyle: { color: tema.textStyle.color, fontFamily: tema.textStyle.fontFamily },
        animationDuration: 300,
        legend: {
            show: legenda,
            top: 0,
            left: 0,
            icon: "roundRect",
            itemWidth: 12,
            itemHeight: 12,
            textStyle: { color: tema.axis.label }
        },
        grid: { left: 4, right: vodorovnePopisky ? 56 : praveMisto, top: legenda ? 36 : 12, bottom: 4, containLabel: true },
        tooltip: {
            ...tooltip,
            confine: true,
            backgroundColor: tema.tooltip.backgroundColor,
            borderColor: tema.tooltip.borderColor,
            borderWidth: 1,
            textStyle: { color: tema.tooltip.textColor },
            valueFormatter: (v) => formatHodnota(v, unit)
        }
    };
}

function osaHodnot(tema, unit, series) {
    const jenCela = series.every((s) => (s.values || []).every((v) => Number.isInteger(v)));
    return {
        type: "value",
        max: unit === "%" ? 100 : undefined,
        // Počty kusů nemají necelé dílky.
        minInterval: jenCela ? 1 : undefined,
        axisLabel: { color: tema.axis.label, formatter: (v) => formatOsa(v, unit) },
        axisLine: { show: false },
        axisTick: { show: false },
        splitLine: { lineStyle: { color: tema.axis.split } }
    };
}

function osaKategorii(tema, cats, vodorovne) {
    return {
        type: "category",
        data: cats,
        // Vodorovné sloupce se čtou shora dolů v pořadí dat (první kategorie nahoře).
        inverse: vodorovne ? true : undefined,
        axisLabel: vodorovne
            ? { color: tema.axis.label, width: 180, overflow: "truncate" }
            : cats.length <= VSECHNY_POPISKY_DO
                ? { color: tema.axis.label, interval: 0, width: 84, overflow: "break" }
                : { color: tema.axis.label, hideOverlap: true },
        axisLine: { lineStyle: { color: tema.axis.line } },
        axisTick: { show: false }
    };
}

function sloupce(d, tema, { skladany, vodorovne }) {
    const cats = d.categories || [];
    const series = d.series || [];
    const unit = d.unit || "";
    const vicSerii = series.length >= 2;
    const primePopisky = vodorovne && !skladany && !vicSerii;
    const kategorie = osaKategorii(tema, cats, vodorovne);
    const hodnoty = osaHodnot(tema, unit, series);

    return {
        ...zaklad(tema, {
            legenda: vicSerii,
            unit,
            vodorovnePopisky: primePopisky,
            tooltip: { trigger: "axis", axisPointer: { type: "shadow" } }
        }),
        xAxis: vodorovne ? hodnoty : kategorie,
        yAxis: vodorovne ? kategorie : hodnoty,
        series: series.map((s, i) => ({
            name: s.label,
            type: "bar",
            data: s.values,
            stack: skladany ? "total" : undefined,
            barMaxWidth: vodorovne ? 22 : 40,
            itemStyle: {
                color: tema.barva(s.colorToken) || tema.color[i % tema.color.length],
                // Skládané segmenty jsou samostatné bloky; samostatný sloupec má zaoblený jen datový konec.
                borderRadius: skladany ? 2 : (vodorovne ? [0, 4, 4, 0] : [4, 4, 0, 0]),
                borderColor: tema.surface,
                borderWidth: skladany || vicSerii ? 2 : 0
            },
            label: primePopisky
                ? { show: true, position: "right", color: tema.textStyle.color, formatter: (p) => formatHodnota(p.value, unit) }
                : { show: false }
        }))
    };
}

function cara(d, tema) {
    const cats = d.categories || [];
    const series = d.series || [];
    const unit = d.unit || "";

    return {
        ...zaklad(tema, {
            legenda: series.length >= 2,
            unit,
            // Bez boundaryGap stojí poslední popisek osy přímo na okraji — potřebuje místo vpravo.
            praveMisto: 32,
            tooltip: { trigger: "axis", axisPointer: { type: "line", lineStyle: { color: tema.axis.line } } }
        }),
        xAxis: { ...osaKategorii(tema, cats, false), boundaryGap: false },
        yAxis: osaHodnot(tema, unit, series),
        series: series.map((s, i) => {
            const barva = tema.barva(s.colorToken) || tema.color[i % tema.color.length];
            return {
                name: s.label,
                type: "line",
                data: s.values,
                symbol: "circle",
                symbolSize: 8,
                lineStyle: { width: 2, color: barva },
                // Prstenec v barvě plochy odděluje značky, které se překrývají.
                itemStyle: { color: barva, borderColor: tema.surface, borderWidth: 2 }
            };
        })
    };
}

function kolac(d, tema, { prstenec }) {
    const cats = d.categories || [];
    const s0 = (d.series || [])[0] || { values: [] };
    const unit = d.unit || "";
    const tokeny = d.categoryColorTokens || [];

    // Barva patří kategorii (index v původním pořadí), ne pořadí po vyřazení nulových výsečí.
    const data = cats
        .map((nazev, i) => ({
            name: nazev,
            value: s0.values[i] ?? 0,
            itemStyle: { color: tema.barva(tokeny[i]) || tema.color[i % tema.color.length] }
        }))
        .filter((x) => x.value > 0);
    const soucet = data.reduce((a, x) => a + x.value, 0);

    return {
        ...zaklad(tema, { legenda: false, unit, tooltip: { trigger: "item" } }),
        // Součet uprostřed prstence.
        title: prstenec
            ? {
                text: formatHodnota(soucet),
                subtext: "celkem",
                left: "center",
                top: "middle",
                itemGap: 2,
                textStyle: { color: tema.textStyle.color, fontSize: 24, fontWeight: 700 },
                subtextStyle: { color: tema.axis.label, fontSize: 12 }
            }
            : undefined,
        series: [{
            type: "pie",
            radius: prstenec ? ["50%", "70%"] : "62%",
            center: ["50%", "50%"],
            data,
            avoidLabelOverlap: true,
            itemStyle: { borderColor: tema.surface, borderWidth: 2 },
            label: { color: tema.textStyle.color, formatter: (p) => `${p.name}\n${formatHodnota(p.value, unit)}` },
            labelLine: { lineStyle: { color: tema.axis.line } }
        }]
    };
}

/**
 * @param {object} d ChartData (camelCase, kind jako text)
 * @param {object} tema výstup sestavTema()
 * @returns {object|null} ECharts option; null = tenhle druh ECharts nekreslí (karty, neznámý druh)
 */
export function optionFor(d, tema) {
    if (!d || typeof d.kind !== "string") {
        return null;
    }
    switch (d.kind) {
        case "Bar":
            return sloupce(d, tema, { skladany: false, vodorovne: false });
        case "StackedBar":
            return sloupce(d, tema, { skladany: true, vodorovne: false });
        case "HorizontalBar":
            return sloupce(d, tema, { skladany: false, vodorovne: true });
        case "StackedHorizontalBar":
            return sloupce(d, tema, { skladany: true, vodorovne: true });
        case "Line":
            return cara(d, tema);
        case "Pie":
            return kolac(d, tema, { prstenec: false });
        case "Donut":
            return kolac(d, tema, { prstenec: true });
        default:
            return null;
    }
}
