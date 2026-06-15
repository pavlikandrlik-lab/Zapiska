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
 * Žádný server call. Persistence rezimu + datumů proběhne až při form submit (Save tlačítko)
 * v jednom POST /Zaznamy/Save (transactional). Server (RecordService.SaveRecord
 * + ApplyHarmonogramRezimAsync) aplikuje rezim na auto-eligible krok řádky
 * a v Auto rezimu spustí re-fill ze ServiceDesk vyjádření.
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
    // Manual→Auto: vyčistit hidden ISO + display value v _AppDateField partial
    // i (defensivně) hidden input KrokKey v auto-eligible variant (ten zůstává — server
    // ho potřebuje pro form binding, ale AbsolutniDatum=empty znamená "krok nenastal").
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
