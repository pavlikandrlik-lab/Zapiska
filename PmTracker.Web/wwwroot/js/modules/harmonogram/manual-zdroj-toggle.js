/**
 * FIX 2026-05-04 — chevron toggle pro kroky 2/5/8/9 ("Ručně" ⇄ "Z vyjádření").
 *
 * UI:
 *   - <details data-manual-zdroj-dropdown> obsahuje <summary> (chevron) + 2 <button> options
 *   - Klik na option → set hidden ManualActualKroky[i].PreferredZdroj = "Manual" | "Auto"
 *   - + update data-skutecnost-rezim na příslušném cell pro CSS visibility (Manual = input,
 *     Auto = bubble z vyjádření)
 *   - Při Auto: vyčistí hidden ManualActualKroky[i].AbsolutniDatum (= server skip stage,
 *     následně ResetManualKrokyToAutoAsync resetuje krok řádek na Auto rezim).
 *
 * Persistence: server-side v RecordService.SaveRecord (chevron toggle se ukládá až při Save,
 *              v paměti dokud user nestiskne "Uložit").
 *
 * Side-effect import v bootstrap.js (memory: project_bundle_sync).
 */
(function () {
  'use strict';

  const DROPDOWN_SELECTOR = '[data-manual-zdroj-dropdown]';
  const OPTION_SELECTOR = '[data-manual-zdroj-select]';

  function findCellAndKey(button) {
    const dropdown = button.closest(DROPDOWN_SELECTOR);
    if (!dropdown) return null;
    const krokKey = dropdown.getAttribute('data-krok-key');
    const cell = button.closest('[data-schedule-actual-cell]');
    return { dropdown, cell, krokKey };
  }

  function updateAriaCurrent(dropdown, selectedZdroj) {
    dropdown.querySelectorAll(OPTION_SELECTOR).forEach((btn) => {
      const z = btn.getAttribute('data-manual-zdroj-select');
      btn.setAttribute('aria-current', z === selectedZdroj ? 'true' : 'false');
    });
  }

  function updateHiddenInputs(krokKey, selectedZdroj) {
    // PreferredZdroj hidden input — vyhledat podle krokKey (nezapojený s closest cell, protože
    // cell může mít více hidden inputů z různých kroků v Razor render order).
    const hidden = document.querySelector(
      `input[data-manual-krok-preferred-zdroj][data-manual-krok-key="${krokKey}"]`
    );
    if (hidden) hidden.value = selectedZdroj;

    // Pokud Auto → vyčistit AbsolutniDatum (= server skip manual stage, ResetManualKrokyToAutoAsync
    // pak resetuje krok řádek a composition při reload vrátí FromVyjadreni datum).
    if (selectedZdroj === 'Auto') {
      const inputs = document.querySelectorAll(
        `pm-date-field[data-manual-krok-key="${krokKey}"]`
      );
      inputs.forEach((df) => {
        const isoH = df.querySelector('input[data-app-date-value]');
        const dispI = df.querySelector('input[data-app-date-display]');
        if (isoH) isoH.value = '';
        if (dispI) dispI.value = '';
        df.setAttribute('iso-value', '');
        df.setAttribute('display-value', '');
      });
    }
  }

  function handleClick(event) {
    const button = event.target?.closest?.(OPTION_SELECTOR);
    if (!button) return;
    event.preventDefault();
    const ctx = findCellAndKey(button);
    if (!ctx || !ctx.cell || !ctx.krokKey) return;

    const selectedZdroj = button.getAttribute('data-manual-zdroj-select');
    if (selectedZdroj !== 'Manual' && selectedZdroj !== 'Auto') return;

    // Cell data-skutecnost-rezim controluje CSS visibility (Auto/Manual).
    ctx.cell.setAttribute('data-skutecnost-rezim', selectedZdroj);
    updateAriaCurrent(ctx.dropdown, selectedZdroj);
    updateHiddenInputs(ctx.krokKey, selectedZdroj);

    // Zavřít dropdown po výběru.
    if (ctx.dropdown.tagName.toLowerCase() === 'details') {
      ctx.dropdown.removeAttribute('open');
    }
  }

  function init() {
    document.addEventListener('click', handleClick);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
