/**
 * recordEditor.js — backward-compat barrel re-export.
 *
 * Fáze 3B Task 1: původní 1919 LOC modul rozdělen do recordEditor/
 * folderu (navigation, form, richtext, draft, index). Toto je
 * dočasný barrel pro zpětnou kompatibilitu s consumery, kteří
 * importují z "./recordEditor.js" přímo. Nové kódy prosím importujte
 * z "./recordEditor/index.js".
 */
export * from "./recordEditor/index.js";
