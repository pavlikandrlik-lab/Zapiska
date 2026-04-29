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
      setDatesVisible(row, false);
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
        setCenaVisible(row, isCenaTyp(data.typ));
        setVyzvaVisible(row, isPnf(data.typ));
        setChatEnabled(row, true);
        setDatesVisible(row, true);
        row.removeAttribute('data-not-found');
      } else {
        setTypDisplay(row, null);
        setTypHidden(row, '');
        setCenaVisible(row, false);
        setVyzvaVisible(row, false);
        setChatEnabled(row, false);
        setDatesVisible(row, false);
        row.setAttribute('data-not-found', 'true');
      }
    } catch (err) {
      console.warn('ExterniOdkaz.Sync selhal:', err);
      setDatesVisible(row, false);
      row.setAttribute('data-not-found', 'true');
    }
  }

  function setTypDisplay(row, typ) {
    // <gov-tag data-external-type-display> — text content + color attribut.
    const tag = row.querySelector('[data-external-type-display]');
    if (!tag) return;
    tag.textContent = typ || '—';
    tag.setAttribute('color', typTagColor(typ));
  }

  function typTagColor(typ) {
    const t = (typ || '').toUpperCase();
    if (t === 'NES') return 'warning';
    if (t === 'PMP') return 'primary';
    if (t === 'PNF') return 'success';
    return 'neutral';
  }

  function isCenaTyp(typ) {
    const t = (typ || '').toUpperCase();
    return t === 'PMP' || t === 'PNF';
  }

  function isPnf(typ) {
    return (typ || '').toUpperCase() === 'PNF';
  }

  function setCenaVisible(row, visible) {
    const cena = row.querySelector('.external-field-cena');
    if (!cena) return;
    // 2026-04-29: input je <gov-form-input> custom element — instanceof HTMLInputElement
    // by nematchoval. setAttribute funguje univerzálně (gov-form-input má disabled
    // jako reflected property → atribut → property).
    const input = cena.querySelector('[data-external-price-input]');
    if (visible) {
      cena.removeAttribute('hidden');
      if (input) input.removeAttribute('disabled');
    } else {
      cena.setAttribute('hidden', 'hidden');
      if (input) input.setAttribute('disabled', '');
    }
  }

  function setVyzvaVisible(row, visible) {
    const vyzva = row.querySelector('[data-external-vyzvy-switch-wrap]');
    if (!vyzva) return;
    if (visible) {
      vyzva.removeAttribute('hidden');
    } else {
      vyzva.setAttribute('hidden', 'hidden');
    }
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

  function setDatesVisible(row, visible) {
    const dates = row.querySelector('[data-external-dates]');
    if (!dates) return;
    if (visible) {
      dates.removeAttribute('hidden');
    } else {
      dates.setAttribute('hidden', 'hidden');
    }
  }

  function onInput(event) {
    // 2026-04-29: <gov-form-input> emituje 'gov-input' (interní `input` má
    // stopPropagation), takže poslouchám obojí — native 'input' pro regulérní
    // <input> (pokud někde zbyl) + 'gov-input' pro gov-form-input.
    const target = event.target;
    if (!target || typeof target.hasAttribute !== 'function') return;
    if (!target.hasAttribute('data-external-cislo')) return;

    const existing = timers.get(target);
    if (existing) clearTimeout(existing);
    const timer = setTimeout(() => syncCislo(target), DEBOUNCE_MS);
    timers.set(target, timer);
  }

  function init() {
    document.addEventListener('input', onInput);
    document.addEventListener('gov-input', onInput);
  }

  global.pmExterniOdkazSync = { init };
})(window);
