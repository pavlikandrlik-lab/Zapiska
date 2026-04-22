/**
 * Plán C Task 18 — Re-harvest tlačítko uvnitř chat modalu.
 * Formulář [data-reharvest-form] posílá POST /Vyjadreni/ReHarvest, po úspěchu
 * přenastaví status a uživatel může modal zavřít a otevřít znovu.
 */
(function (global) {
  'use strict';

  function setStatus(root, text, state) {
    const el = root.querySelector('[data-chat-status]');
    if (!el) return;
    el.textContent = text || '';
    if (state) el.setAttribute('data-status-state', state);
    else el.removeAttribute('data-status-state');
  }

  function attach(root) {
    const form = root.querySelector('[data-reharvest-form]');
    if (!form) return;
    const btn = form.querySelector('[data-reharvest-btn]');

    form.addEventListener('submit', async (ev) => {
      ev.preventDefault();
      const fd = new FormData(form);
      if (btn) btn.setAttribute('disabled', '');
      setStatus(root, 'Re-harvest běží…', null);
      try {
        const resp = await fetch(form.action, {
          method: 'POST',
          credentials: 'same-origin',
          body: fd,
          headers: { 'Accept': 'application/json' }
        });
        if (!resp.ok) {
          setStatus(root, 'Re-harvest selhal (HTTP ' + resp.status + ').', 'error');
          return;
        }
        const result = await resp.json();
        const msg = result
          ? `Re-harvest dokončen: načteno ${result.fetched}, vytvořeno ${result.created}, preskočeno ${result.skipped}.`
          : 'Re-harvest dokončen.';
        setStatus(root, msg, 'ok');
      } catch (err) {
        setStatus(root, 'Re-harvest selhal: ' + (err.message || err), 'error');
      } finally {
        if (btn) btn.removeAttribute('disabled');
      }
    });
  }

  global.pmChatModalReharvest = { attach };
})(window);
