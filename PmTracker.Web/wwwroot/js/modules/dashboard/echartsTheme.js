// dashboard/echartsTheme.js — téma grafů z design tokenů (spec 2026-10-15 §12).
// Čistá funkce bez DOM: dostane čtečku CSS proměnné, vrátí barvy pro echartsOption.js.
// Testováno v tests/js/dom/echartsTheme.test.js.

// Záložní barvy pro případ, že token chybí (CSS se nenačetlo) — graf nesmí dostat prázdnou barvu.
const ZALOHA = {
    "--app-chart-1": "#3771b8",
    "--app-chart-2": "#af7a00",
    "--app-chart-neutral": "#888888",
    "--app-chart-neutral-strong": "#4f4f4f",
    "--background-block-primary": "#ffffff",
    "--text-primary": "#262626",
    "--text-secondary": "#4f4f4f",
    "--border-subtle": "#d1d1d1",
    "--border-subtlest": "#e7e7e7"
};

const NAZEV_PROMENNE = /^--[a-z0-9-]+$/i;

/**
 * @param {(name: string) => string} ctiPromennou vrací vyřešenou hodnotu CSS proměnné (nebo "")
 */
export function sestavTema(ctiPromennou) {
    const hodnota = (name) => String(ctiPromennou(name) ?? "").trim();
    const cti = (name) => hodnota(name) || ZALOHA[name];

    return {
        // Pevné pořadí: modrá, jantarová, šedá, tmavá šedá. Barva jde se sérií, nikdy s jejím pořadím po filtru.
        color: [cti("--app-chart-1"), cti("--app-chart-2"), cti("--app-chart-neutral"), cti("--app-chart-neutral-strong")],
        // Plocha karty: mezery mezi segmenty a prstenec kolem značek.
        surface: cti("--background-block-primary"),
        textStyle: { color: cti("--text-primary"), fontFamily: hodnota("--font-family") || "Roboto, Arial, sans-serif" },
        axis: { label: cti("--text-secondary"), line: cti("--border-subtle"), split: cti("--border-subtlest") },
        tooltip: {
            backgroundColor: cti("--background-block-primary"),
            borderColor: cti("--border-subtle"),
            textColor: cti("--text-primary")
        },
        /** Barva tokenu série/kategorie (ChartSeries.ColorToken); null = token neznámý → barva podle pořadí. */
        barva(token) {
            if (typeof token !== "string" || !NAZEV_PROMENNE.test(token)) {
                return null;
            }
            return hodnota(token) || null;
        }
    };
}

/** Téma podle aktuálního motivu stránky (vyřešené hodnoty tokenů z :root). */
export function readTheme() {
    const styl = getComputedStyle(document.documentElement);
    return sestavTema((name) => styl.getPropertyValue(name));
}
