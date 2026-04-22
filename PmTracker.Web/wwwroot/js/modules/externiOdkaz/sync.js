/**
 * Externí odkaz sync — debouncovaný input handler pro 6místné číslo tiketu.
 * Po naplnění 6 cifer volá POST /ExterniOdkaz/Sync a vyplní Typ + řeší vzhled karty.
 * Chat tlačítko se povolí jen pokud je tiket nalezen.
 */
(function (global) {
  'use strict';

  const DEBOUNCE_MS = 400;
  const timers = new WeakMap();

  function getCsrfToken() {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function getProjektId(row) {
    // projektId je čteno z ancestor data atributu na form wrapperu.
    const form = row.closest('[data-record-editor-project-id]');
    if (!form) return '';
    return form.getAttribute('data-record-editor-project-id') || '';
  }

  async function syncCislo(inputEl) {
    const row = inputEl.closest('[data-external-row]');
    if (!row) return;
    const cislo = (inputEl.value || '').trim();
    if (!/^\d{6}$/.test(cislo)) {
      setTypDisplay(row, null);
      setChatEnabled(row, false);
      row.removeAttribute('data-not-found');
      return;
    }

    const projektId = getProjektId(row);
    if (!projektId) {
      console.warn('ExterniOdkaz.Sync: chybí projektId na form wrapperu.');
      return;
    }

    const form = new FormData();
    form.append('cislo', cislo);
    form.append('projektId', projektId);
    form.append('__RequestVerificationToken', getCsrfToken());

    try {
      const resp = await fetch('/ExterniOdkaz/Sync', {
        method: 'POST',
        body: form,
        credentials: 'same-origin',
      });
      if (!resp.ok) throw new Error('HTTP ' + resp.status);
      const data = await resp.json();
      if (data.nalezeno) {
        setTypDisplay(row, data.typ);
        setTypHidden(row, data.typ);
        setChatEnabled(row, true);
        row.removeAttribute('data-not-found');
      } else {
        setTypDisplay(row, null);
        setTypHidden(row, '');
        setChatEnabled(row, false);
        row.setAttribute('data-not-found', 'true');
      }
    } catch (err) {
      console.warn('ExterniOdkaz.Sync selhal:', err);
      row.setAttribute('data-not-found', 'true');
    }
  }

  function setTypDisplay(row, typ) {
    const span = row.querySelector('[data-external-type-display]');
    if (span) span.textContent = typ || '—';
  }

  function setTypHidden(row, typ) {
    const hidden = row.querySelector('[data-external-type-hidden]');
    if (hidden) hidden.value = typ || '';
  }

  function setChatEnabled(row, enabled) {
    const btn = row.querySelector('[data-external-chat-open]');
    if (!btn) return;
    if (enabled) {
      btn.removeAttribute('disabled');
    } else {
      btn.setAttribute('disabled', 'disabled');
    }
  }

  function onInput(event) {
    const target = event.target;
    if (!(target instanceof HTMLInputElement)) return;
    if (!target.hasAttribute('data-external-cislo')) return;

    const existing = timers.get(target);
    if (existing) clearTimeout(existing);
    const timer = setTimeout(() => syncCislo(target), DEBOUNCE_MS);
    timers.set(target, timer);
  }

  function init() {
    document.addEventListener('input', onInput);
  }

  global.pmExterniOdkazSync = { init };
})(window);
