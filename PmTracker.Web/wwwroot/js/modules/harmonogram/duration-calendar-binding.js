/**
 * DESIGN-9-F (2026-05-02) — DURATION input je teď kalendář (datum konce kroku),
 * trvání ve dnech se počítá automaticky z (datum konce - datum začátku kroku).
 *
 * Architektura:
 *   - Hidden input data-schedule-duration-hidden (name="HarmonogramHodnoty[i].Hodnota")
 *     drží integer počet dní pro form POST. Server logice nezměněn.
 *   - Kalendář data-schedule-duration-calendar (= forward na hidden input
 *     uvnitř <pm-date-field> Custom Elementu) = primární editor.
 *   - Readonly text data-schedule-duration-readonly = vizuální zobrazení trvání.
 *
 * Datum začátku kroku N:
 *   - Krok 1: data-schedule-start na .schedule-block kořeni (= DatumZalozeni).
 *   - Krok N>1: BaselineDatum kroku N-1 (= datum konce předchozího kroku).
 *
 * Cumulative chain — když user změní datum kroku N, přepočítají se trvání
 * všech kroků N..konec, protože každý další krok navazuje na konec předchozího.
 *
 * Side-effect import v bootstrap.js (memory: project_bundle_sync).
 */
(function (global) {
  'use strict';

  const ROW_SELECTOR = '[data-schedule-step-row]';
  const CALENDAR_SELECTOR = '[data-schedule-duration-calendar]';
  const HIDDEN_SELECTOR = '[data-schedule-duration-hidden]';
  const READONLY_SELECTOR = '[data-schedule-duration-readonly]';
  const SCHEDULE_BLOCK_SELECTOR = '[data-schedule-start]';

  function parseIso(value) {
    if (typeof value !== 'string' || value.length < 10) return null;
    const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    if (!m) return null;
    const date = new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3])));
    return Number.isNaN(date.getTime()) ? null : date;
  }

  function formatIso(date) {
    const y = date.getUTCFullYear();
    const m = String(date.getUTCMonth() + 1).padStart(2, '0');
    const d = String(date.getUTCDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }

  function diffDays(startDate, endDate) {
    const ms = endDate.getTime() - startDate.getTime();
    return Math.round(ms / 86400000);
  }

  /**
   * Datum začátku kroku N = datum konce kroku N-1, resp. záznam.DatumZalozeni
   * pro krok 1. Iteruje přes řádky v sortu podle data-step-index.
   */
  function getStartDateForStepRow(allRows, stepIndex, recordStartDate) {
    if (stepIndex <= 1) return recordStartDate;
    const prevRow = allRows.find((r) => parseInt(r.getAttribute('data-step-index') || '0', 10) === stepIndex - 1);
    if (!prevRow) return recordStartDate;
    const prevCalendar = prevRow.querySelector(CALENDAR_SELECTOR);
    if (!prevCalendar) return recordStartDate;
    const prevHidden = prevCalendar.querySelector('input[type="hidden"][data-app-date-value]')
      || prevCalendar.querySelector('input[type="hidden"][name*="UiHarmonogramDatumy"]');
    const prevEnd = parseIso(prevHidden?.value);
    return prevEnd || recordStartDate;
  }

  function readCalendarIso(row) {
    const cal = row.querySelector(CALENDAR_SELECTOR);
    if (!cal) return null;
    const hidden = cal.querySelector('input[type="hidden"][data-app-date-value]')
      || cal.querySelector('input[type="hidden"][name*="UiHarmonogramDatumy"]');
    return hidden?.value || null;
  }

  function recalcRow(row, recordStartDate, allRows) {
    const stepIndex = parseInt(row.getAttribute('data-step-index') || '0', 10);
    const hidden = row.querySelector(HIDDEN_SELECTOR);
    const readonly = row.querySelector(READONLY_SELECTOR);
    if (!hidden || !readonly) return;

    const endIso = readCalendarIso(row);
    const endDate = parseIso(endIso);
    if (!endDate) {
      hidden.value = '0';
      readonly.textContent = '0 dnů';
      row.setAttribute('data-step-duration', '0');
      return;
    }

    const startDate = getStartDateForStepRow(allRows, stepIndex, recordStartDate);
    const days = Math.max(0, diffDays(startDate, endDate));
    hidden.value = String(days);
    readonly.textContent = `${days} ${days === 1 ? 'den' : (days >= 2 && days <= 4 ? 'dny' : 'dnů')}`;
    row.setAttribute('data-step-duration', String(days));
  }

  function recalcChainFrom(stepIndex, recordStartDate, allRows) {
    // Krok N a všechny následující — protože změna konce kroku N posune
    // začátek kroku N+1 (= konec kroku N), což ovlivňuje jeho délku.
    for (const row of allRows) {
      const idx = parseInt(row.getAttribute('data-step-index') || '0', 10);
      if (idx >= stepIndex) recalcRow(row, recordStartDate, allRows);
    }
  }

  function getRecordStartDate(scope) {
    const block = (scope || document).querySelector(SCHEDULE_BLOCK_SELECTOR);
    const iso = block?.getAttribute('data-schedule-start');
    return parseIso(iso) || new Date();
  }

  function getSortedRows(scope) {
    return Array.from((scope || document).querySelectorAll(ROW_SELECTOR))
      .sort((a, b) => parseInt(a.getAttribute('data-step-index') || '0', 10)
                    - parseInt(b.getAttribute('data-step-index') || '0', 10));
  }

  function init() {
    document.addEventListener('change', (event) => {
      const target = event.target;
      if (!(target instanceof HTMLInputElement)) return;
      const cal = target.closest(CALENDAR_SELECTOR);
      if (!cal) return;
      const row = cal.closest(ROW_SELECTOR);
      if (!row) return;
      const block = row.closest(SCHEDULE_BLOCK_SELECTOR) || document;
      const stepIndex = parseInt(row.getAttribute('data-step-index') || '0', 10);
      const recordStartDate = getRecordStartDate(block);
      const allRows = getSortedRows(block);
      recalcChainFrom(stepIndex, recordStartDate, allRows);
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  global.pmDurationCalendarBinding = { recalcChainFrom };
})(window);
