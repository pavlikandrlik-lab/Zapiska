/**
 * chatModalReharvest.js — „Znovu vytěžit" tlačítko v chat modalu.
 *
 * Spec 2026-04-29:
 *   - Po úspěšném re-harvestu (Throttled=false) automaticky volá
 *     window.pmChatModal.refreshModal() — user nemusí modal zavřít a otevřít.
 *   - Pokud server vrátil Throttled=true (cooldown 60s), zobrazí gov-infobar
 *     s informací "Re-harvest je dostupný jednou za minutu" a refresh NEVOLÁ.
 *
 * Formulář [data-reharvest-form] posílá POST /Vyjadreni/ReHarvest, response je JSON
 * s shape { fetched, created, superseded, skipped, message, throttled, retryAfterSeconds }.
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

  function ensureInfobarSlot(root) {
    let slot = root.querySelector('[data-reharvest-infobar-slot]');
    if (slot) return slot;
    // Vytvořit slot v headeru pod meta line
    slot = document.createElement('div');
    slot.setAttribute('data-reharvest-infobar-slot', '');
    slot.style.marginTop = '0.5rem';
    const header = root.querySelector('.pm-chat-modal__header');
    if (header && header.parentNode) {
      header.parentNode.insertBefore(slot, header.nextSibling);
    } else {
      root.insertBefore(slot, root.firstChild);
    }
    return slot;
  }

  function showThrottledInfobar(root, retryAfterSeconds) {
    const slot = ensureInfobarSlot(root);
    slot.innerHTML = '';
    const bar = document.createElement('gov-infobar');
    bar.setAttribute('color', 'warning');
    bar.setAttribute('type', 'solid');
    bar.setAttribute('closable', '');
    bar.setAttribute('accessible-close-label', 'Zavřít upozornění');
    bar.textContent = 'Re-harvest je dostupný jednou za minutu. Zkuste znovu za ' + retryAfterSeconds + ' s.';
    slot.appendChild(bar);
    // Auto-zmiznout po (retryAfter+2)s — v té chvíli už user může zkusit znovu.
    const ttl = (retryAfterSeconds + 2) * 1000;
    setTimeout(function () {
      if (slot.contains(bar)) slot.removeChild(bar);
    }, ttl);
  }

  function attach(root) {
    const form = root.querySelector('[data-reharvest-form]');
    if (!form) return;
    const btn = form.querySelector('[data-reharvest-btn]');

    form.addEventListener('submit', async (ev) => {
      ev.preventDefault();
      const fd = new FormData(form);
      if (btn) btn.setAttribute('disabled', '');
      setStatus(root, 'Vytěžování běží…', null);
      try {
        const resp = await fetch(form.action, {
          method: 'POST',
          credentials: 'same-origin',
          body: fd,
          headers: { 'Accept': 'application/json' }
        });
        if (!resp.ok) {
          setStatus(root, 'Vytěžování selhalo (HTTP ' + resp.status + ').', 'error');
          return;
        }
        const result = await resp.json();
        // Spec 2026-04-29: pokud server vrátil Throttled, zobrazit infobar a NEvolat refresh.
        if (result && (result.throttled === true || result.Throttled === true)) {
          const retryAfter = Number(result.retryAfterSeconds || result.RetryAfterSeconds || 60);
          showThrottledInfobar(root, retryAfter);
          setStatus(root, '', null);
          return;
        }
        const msg = result
          ? `Vytěžování dokončeno: načteno ${result.fetched ?? result.Fetched ?? 0}, vytvořeno ${result.created ?? result.Created ?? 0}, preskočeno ${result.skipped ?? result.Skipped ?? 0}.`
          : 'Vytěžování dokončeno.';
        setStatus(root, msg, 'ok');
        // Auto-refresh modalu po úspěšném re-harvestu (user nemusí zavřít/otevřít).
        if (global.pmChatModal && typeof global.pmChatModal.refreshModal === 'function') {
          await global.pmChatModal.refreshModal();
        }
      } catch (err) {
        setStatus(root, 'Vytěžování selhalo: ' + (err.message || err), 'error');
      } finally {
        if (btn) btn.removeAttribute('disabled');
      }
    });
  }

  global.pmChatModalReharvest = { attach };
})(window);
