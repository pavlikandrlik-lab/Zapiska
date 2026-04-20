/**
 * schedule.js — backward-compat barrel re-export.
 *
 * Fáze 3B Task 2: původní monolitický soubor (1697 LOC, 24+ exports) byl rozdělen
 * do 4 submodulů + orchestrator pod schedule/.
 *
 * Nové kódy prosím importujte z "./schedule/index.js".
 * Tento barrel zůstává pro zpětnou kompatibilitu s existujícími importy.
 */
export * from "./schedule/index.js";
