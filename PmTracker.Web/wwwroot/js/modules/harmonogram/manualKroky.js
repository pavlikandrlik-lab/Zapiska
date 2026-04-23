/**
 * Plán D Task 9 — client-side validace ručního data skutečnosti kroku (2/5/8/9).
 *
 * Kontrakt s Razor:
 *  - Input má atribut data-manual-krok-input
 *  - Vedle něj je hidden s name="ManualActualKroky[i].KrokKey" a
 *    vlastním name="ManualActualKroky[i].AbsolutniDatum"
 *  - Form parent je standardní record editor form, tzn. POST /Zaznamy/Save
 *
 * Validační pravidla (zrcadlí server ManualProposalFieldValidator):
 *  - Budoucí datum → odmítnuto lokálně (server by vrátil 400, radši UX first)
 *  - Chronologie: pokud nové datum > datum dalšího kroku, zobrazí se warning
 *    přes gov-message (inline), ale submit se nezamyká — server si poradí
 *    přes ManualActualKrokApplier cascade logikou
 *
 * Na rozdíl od externiOdkaz/sync.js neposíláme POST sami — data jsou součástí
 * record editor form POST do /Zaznamy/Save. Tento modul pouze validuje.
 */
(function (global) {
  'use strict';

  const INPUT_SELECTOR = '[data-manual-krok-input]';
  const WARNING_CLASS = 'schedule-actual-manual-warning';

  function parseIsoDate(value) {
    if (!value) return null;
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
    if (!match) return null;
    const year = Number(match[1]);
    const month = Number(match[2]);
    const day = Number(match[3]);
    const date = new Date(Date.UTC(year, month - 1, day));
    if (Number.isNaN(date.getTime())) return null;
    return date;
  }

  function todayUtc() {
    const now = new Date();
    return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  }

  function clearMessages(input) {
    const wrap = input.closest('.schedule-actual-manual');
    if (!wrap) return;
    const existing = wrap.querySelector('.' + WARNING_CLASS);
    if (existing) existing.remove();
    input.removeAttribute('data-manual-krok-invalid');
  }

  function showError(input, message) {
    const wrap = input.closest('.schedule-actual-manual');
    if (!wrap) return;
    clearMessages(input);
    const el = document.createElement('gov-message');
    el.setAttribute('color', 'danger');
    el.className = WARNING_CLASS;
    el.textContent = message;
    wrap.appendChild(el);
    input.setAttribute('data-manual-krok-invalid', 'true');
  }

  function showWarning(input, message) {
    const wrap = input.closest('.schedule-actual-manual');
    if (!wrap) return;
    clearMessages(input);
    const el = document.createElement('gov-message');
    el.setAttribute('color', 'warning');
    el.className = WARNING_CLASS;
    el.textContent = message;
    wrap.appendChild(el);
  }

  function findSiblingManualInputs(input) {
    const form = input.closest('form');
    if (!form) return [];
    return Array.from(form.querySelectorAll(INPUT_SELECTOR));
  }

  function validateChronology(input) {
    const value = parseIsoDate(input.value);
    if (!value) return;
    const myIndex = Number(input.getAttribute('data-manual-krok-index') || '0');
    const siblings = findSiblingManualInputs(input);
    for (const sibling of siblings) {
      if (sibling === input) continue;
      const sibIndex = Number(sibling.getAttribute('data-manual-krok-index') || '0');
      const sibValue = parseIsoDate(sibling.value);
      if (!sibValue) continue;
      if (sibIndex > myIndex && value > sibValue) {
        showWarning(input,
          'Datum je pozdější než u následujícího kroku. Server při schválení návrhu datumy zřetězí.');
        return;
      }
    }
  }

  function handleChange(event) {
    const input = event.target;
    if (!input || !input.matches || !input.matches(INPUT_SELECTOR)) return;

    clearMessages(input);

    if (!input.value) {
      // Prázdné pole je validní — server buď vynechá krok, nebo smaže skutečnost.
      return;
    }

    const parsed = parseIsoDate(input.value);
    if (!parsed) {
      showError(input, 'Neplatný formát data — očekává se YYYY-MM-DD.');
      return;
    }

    if (parsed > todayUtc()) {
      showError(input, 'Datum skutečnosti nemůže být v budoucnu.');
      return;
    }

    validateChronology(input);
  }

  function init() {
    document.addEventListener('change', handleChange, true);
  }

  global.pmManualKroky = { init };
})(window);
