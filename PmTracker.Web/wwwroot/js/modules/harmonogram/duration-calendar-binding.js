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
      markRowInvalid(row, false, '');
      return;
    }

    const startDate = getStartDateForStepRow(allRows, stepIndex, recordStartDate);
    const rawDays = diffDays(startDate, endDate);
    const days = Math.max(0, rawDays);
    hidden.value = String(days);
    readonly.textContent = `${days} ${days === 1 ? 'den' : (days >= 2 && days <= 4 ? 'dny' : 'dnů')}`;
    row.setAttribute('data-step-duration', String(days));

    // FIX 2026-05-05: chronologie guard. Pokud user zadal datum konce dříve než datum začátku
    // (= dříve než datum konce předchozího kroku), Trvání by bylo záporné. Současný clamp na 0
    // server-side OK uloží, ale user nedostal žádný feedback že jeho zadání nedává smysl.
    // UI marker `data-schedule-duration-invalid` triggeruje red border (CSS) + tooltip.
    // Form submit listener (initSubmitGuard) blokuje uložení pokud existují invalid kroky.
    if (rawDays < 0) {
      const startLabel = formatDisplayDate(startDate);
      markRowInvalid(row, true,
        `Datum konce kroku nesmí být dříve než ${startLabel} (datum začátku tohoto kroku).`);
    } else {
      markRowInvalid(row, false, '');
    }
  }

  function formatDisplayDate(date) {
    const d = String(date.getUTCDate()).padStart(2, '0');
    const m = String(date.getUTCMonth() + 1).padStart(2, '0');
    const y = date.getUTCFullYear();
    return `${d}.${m}.${y}`;
  }

  function markRowInvalid(row, invalid, message) {
    if (invalid) {
      row.setAttribute('data-schedule-duration-invalid', 'true');
    } else {
      row.removeAttribute('data-schedule-duration-invalid');
    }
    // Tooltip + aria — set na pm-date-field i na inner display input.
    const cal = row.querySelector(CALENDAR_SELECTOR);
    if (cal instanceof HTMLElement) {
      if (invalid) {
        cal.setAttribute('title', message);
        cal.setAttribute('aria-invalid', 'true');
      } else {
        cal.removeAttribute('title');
        cal.removeAttribute('aria-invalid');
      }
      const display = cal.querySelector('input[data-app-date-display]');
      if (display instanceof HTMLInputElement) {
        if (invalid) {
          display.setAttribute('title', message);
        } else {
          display.removeAttribute('title');
        }
      }
    }
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
    // FIX 2026-05-04: scope může být sám schedule-block element (= row.closest vrátí ten)
    // nebo document. querySelector hledá DESCENDANTS, takže když scope je sám block,
    // vrací null → fallback new Date() (today) → diff je záporný → days=0 (bug Plán
    // datum se neuloží). Fix: detekovat self-match přes matches() první.
    if (!scope) return new Date();
    let iso = null;
    if (scope instanceof HTMLElement && scope.matches(SCHEDULE_BLOCK_SELECTOR)) {
      iso = scope.getAttribute('data-schedule-start');
    } else {
      const block = scope.querySelector ? scope.querySelector(SCHEDULE_BLOCK_SELECTOR) : null;
      iso = block?.getAttribute('data-schedule-start') || null;
    }
    return parseIso(iso) || new Date();
  }

  function getSortedRows(scope) {
    return Array.from((scope || document).querySelectorAll(ROW_SELECTOR))
      .sort((a, b) => parseInt(a.getAttribute('data-step-index') || '0', 10)
                    - parseInt(b.getAttribute('data-step-index') || '0', 10));
  }

  function init() {
    // FIX 2026-05-02: capture phase + setTimeout 0 — block.js (legacy schedule preview)
    // poslouchá stejný change event a fetchne /Schedule/Recalc, který by přepsal naši
    // hodnotu hidden_duration. Capture phase nezaručí pořadí spolehlivě napříč prohlížeči,
    // tak setTimeout 0 odloží náš recalc na konec event loop tick — duration-calendar-binding
    // má poslední slovo o hidden_duration value pro form POST.
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
      // Defer recalc to end of microtask queue — runs po block.js synchronous handler
      // i případném server preview fetch resolution.
      setTimeout(() => recalcChainFrom(stepIndex, recordStartDate, allRows), 0);
    }, true);

    // FIX 2026-05-05: form submit guard — pokud existuje řádek s data-schedule-duration-invalid,
    // blokujeme Save a zobrazíme chybu. User chce real-time UI feedback "tohle neprojde",
    // tak to vynucuje i na Save buttonu (jinak by user kliknul Save a vidět nic — clamp by
    // tichý uložil 0 dnů, což ne odpovídá tomu co user zadal).
    document.addEventListener('submit', (event) => {
      const form = event.target;
      if (!(form instanceof HTMLFormElement)) return;
      const invalidRow = form.querySelector(`${ROW_SELECTOR}[data-schedule-duration-invalid="true"]`);
      if (!invalidRow) return;
      event.preventDefault();
      // Scroll do pole + focus pro UX. Display input je readonly, ale focus stačí pro screen-reader hint.
      const cal = invalidRow.querySelector(CALENDAR_SELECTOR);
      const display = cal?.querySelector('input[data-app-date-display]');
      if (display instanceof HTMLInputElement) {
        display.scrollIntoView({ behavior: 'smooth', block: 'center' });
        display.focus({ preventScroll: true });
      }
      // Globální alert toast pokud existuje. Fallback alert() pro definitivní viditelnost.
      const message = cal?.getAttribute('title')
        || 'Některý krok harmonogramu má datum konce dříve než datum začátku.';
      window.alert(message);
    }, true);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  global.pmDurationCalendarBinding = { recalcChainFrom };
})(window);
