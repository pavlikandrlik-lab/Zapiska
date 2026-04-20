/**
 * filters.js — backward-compat barrel re-export.
 * Nové kódy importujte z "./filters/index.js".
 *
 * Fáze 3B Task 4: původní 1105 LOC rozdělen do filters/ submodulů:
 *   - filters/projectFilter.js  — config, normalize, build state, render chips, defaults
 *   - filters/recordDisplay.js  — record visibility, view switching, subsystem indicator, tabs
 *   - filters/printFilter.js    — print filter snapshot + query params
 *   - filters/index.js          — state persistence + initProjectRecordsUi orchestrator + re-exports
 */
export * from "./filters/index.js";
