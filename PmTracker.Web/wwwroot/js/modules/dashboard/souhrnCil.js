// Cíl dlaždice souhrnného pásu dashboardu projektu (spec 2026-10-15 §8).
// Čistá funkce bez DOM — testovaná v tests/js/dom/dashboardSouhrnCil.test.js.

const ZNAME_ZALOZKY = new Set(["zaznamy", "nes", "statistiky"]);

/**
 * Z dataset dlaždice (`data-souhrn-tab`, `data-souhrn-filter`) určí, kam klik vede.
 * @param {{ souhrnTab?: string, souhrnFilter?: string } | undefined} dataset
 * @returns {{ tab: string, filter: string } | null} null = neznámý cíl, klik nic nedělá
 */
export function resolveSouhrnCil(dataset) {
    const tab = (dataset?.souhrnTab ?? "").trim();
    if (!ZNAME_ZALOZKY.has(tab)) {
        return null;
    }
    return { tab, filter: (dataset?.souhrnFilter ?? "").trim() };
}
