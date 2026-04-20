/**
 * pickers.js — backward-compat barrel re-export.
 * Nové kódy importujte z "./pickers/index.js" nebo konkrétního submodulu.
 *
 * Fáze 3B Task 3: původní 1530 LOC rozdělen do pickers/ submodulů:
 *   - pickers/date.js     — setAppDateFieldValue, closeAllDatePanels, initCustomDatePickers
 *   - pickers/time.js     — closeAllTimePanels, initCustomTimePickers
 *   - pickers/person.js   — formatPersonEntryLabel, initSinglePersonPickers, initCollabPickers
 *   - pickers/adPerson.js — initAdPersonPickers
 */
export * from "./pickers/index.js";
