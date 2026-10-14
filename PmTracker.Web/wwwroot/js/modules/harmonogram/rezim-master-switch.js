/**
 * FIX 2026-05-04 — JS handler pro master switch "Automatické vyplňování harmonogramu".
 *
 * Architektura: switch je čistě klient-side. Při toggle:
 *   1. Update hidden form input [data-record-rezim-form] na "Auto" / "Manual"
 *   2. Iterace přes všechny [data-schedule-actual-cell][data-krok-poradi ∈ {1,3,4,6,7,10}]
 *      → update atributu data-skutecnost-rezim
 *      → CSS pravidla v site.css/components/schedule-actual-cell-feature-c.css automaticky
 *        skryjí/zobrazí auto bubble vs manual editable input (data-rezim-show-on="Auto"|"Manual")
 *   3. Manual→Auto: clear values v manual input cells (user explicit požadavek
 *      "auto→manual+vyplň→auto = data se ztratí, žádné dotazy")
 *
 * Žádný server call. Režim i data jdou až při uložení v jednom POST /Zaznamy/Save.
 * Server (ScheduleActualApplier, spec 2026-10-14) při přepnutí na Auto smaže data
 * kroků 1/3/4/6/7/10, která nedodal automat, a automat je hned doplní ze ServiceDesku;
 * při přepnutí na ruční nechá hodnoty i zdroje beze změny (vstupy jsou předvyplněné
 * aktuální hodnotou).
 *
 * Side-effect import v bootstrap.js (memory: project_bundle_sync).
 */
(function (global) {
  'use strict';

  const SWITCH_SELECTOR = '[data-record-rezim-switch]';
  const REZIM_FORM_SELECTOR = '[data-record-rezim-form]';
  const ACTUAL_CELL_SELECTOR = '[data-schedule-actual-cell]';
  // Auto-eligible kroky (mimo manuální 2/5/8/9). Master switch řídí jen tyto.
  const AUTO_ELIGIBLE_KROKY = new Set([1, 3, 4, 6, 7, 10]);

  function findAutoEligibleCells(form) {
    const root = form || document;
    return Array.from(root.querySelectorAll(ACTUAL_CELL_SELECTOR))
      .filter((cell) => {
        const poradi = parseInt(cell.getAttribute('data-krok-poradi') || '0', 10);
        return AUTO_ELIGIBLE_KROKY.has(poradi);
      });
  }

  function clearManualInputsInCell(cell) {
    // Manual→Auto: vyčistit hidden ISO + display value v _AppDateField partial. Server změny
    // kroků 1/3/4/6/7/10 v Auto režimu ignoruje (spec 2026-10-14); vyčištění je jen vizuální
    // potvrzení, že data převezme automat.
    const dateFields = cell.querySelectorAll('pm-date-field[data-manual-krok-auto-eligible]');
    dateFields.forEach((df) => {
      const isoHidden = df.querySelector('input[data-app-date-value]');
      const displayInput = df.querySelector('input[data-app-date-display]');
      if (isoHidden) isoHidden.value = '';
      if (displayInput) displayInput.value = '';
      // pm-date-field má observed attribute iso-value/display-value — synchronizace vnitřního stavu
      df.setAttribute('iso-value', '');
      df.setAttribute('display-value', '');
    });
  }

  function applyRezimToCells(form, rezim) {
    const cells = findAutoEligibleCells(form);
    cells.forEach((cell) => {
      cell.setAttribute('data-skutecnost-rezim', rezim);
      if (rezim === 'Auto') {
        clearManualInputsInCell(cell);
      }
    });
  }

  function handleChange(event) {
    const sw = event.target?.closest?.(SWITCH_SELECTOR);
    if (!sw) return;

    // C3 (2026-07-10): read-only stránka (schvalování návrhu) renderuje switch
    // s disabled — event od gov komponenty se nesmí propsat do hidden inputu ani cells.
    if (sw.hasAttribute('disabled')) return;

    // gov-form-switch CustomEvent: event.detail.checked je authoritative.
    const isChecked = (event && event.detail && typeof event.detail.checked === 'boolean')
      ? event.detail.checked
      : !!sw.checked;
    const rezim = isChecked ? 'Auto' : 'Manual';

    const form = sw.closest('form[data-record-editor-form]');
    if (!form) return;

    // 1) Update hidden form field (Save POST ho pošle)
    const hidden = form.querySelector(REZIM_FORM_SELECTOR);
    if (hidden) hidden.value = rezim;

    // 2) Update všechny auto-eligible cells (data-skutecnost-rezim → CSS visibility)
    applyRezimToCells(form, rezim);

    // Editor návrhu (uživatel 2026-10-08): přepínač přepnutý opačně než na záznamu se podbarví
    // stejně jako ostatní změněná pole návrhu.
    const original = sw.getAttribute('data-record-rezim-original');
    if (original) {
      sw.classList.toggle('proposal-field-changed', original !== rezim);
    }
  }

  function init() {
    document.addEventListener('change', handleChange);
    document.addEventListener('gov-change', handleChange);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  global.pmRezimMasterSwitch = { applyRezimToCells, findAutoEligibleCells };
})(window);
