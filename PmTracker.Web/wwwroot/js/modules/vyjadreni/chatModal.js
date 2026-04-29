/**
 * Plán C — chat modal shell (Task 15).
 * Fetchuje partial z /Vyjadreni/Modal?externiOdkazId=X&zaznamId=Y, vloží do gov-dialog,
 * inicializuje drag-drop (Task 16), autosave (Task 17), reharvest (Task 18).
 */
(function (global) {
  'use strict';

  let dialogEl = null;
  let currentCtx = null; // { externiOdkazId, zaznamId }

  function ensureDialog() {
    if (dialogEl) return dialogEl;
    dialogEl = document.createElement('gov-dialog');
    dialogEl.setAttribute('size', 'l');
    // Fix Bug 2026-04-29: bez data-modal-variant gov-dialog používal default ~832px
    // → vnitřní obsah .pm-chat-modal s max-width 1800px přetékal a vznikal horizontální
    // scroll. data-modal-variant="chat-modal" napíchne CSS rule v site.css která
    // nastaví --max-width: min(95vw, 1800px) na gov-dialog wrapper, tj. okno
    // se skutečně rozšíří, ne jen vnitřní obsah.
    dialogEl.setAttribute('data-modal-container', '');
    dialogEl.setAttribute('data-modal-variant', 'chat-modal');
    dialogEl.innerHTML = `
      <div slot="label">Vyjádření a termíny</div>
      <div class="pm-chat-modal__content" data-chat-modal-content></div>
      <div slot="footer" style="display:flex; justify-content:flex-end">
        <pm-button variant="Primary" data-chat-modal-close>Zavřít</pm-button>
      </div>
    `;
    document.body.appendChild(dialogEl);
    dialogEl.addEventListener('click', (event) => {
      if (event.target.closest('[data-chat-modal-close]')) close();
    });
    return dialogEl;
  }

  async function open(externiOdkazId, zaznamId) {
    currentCtx = { externiOdkazId: String(externiOdkazId), zaznamId: String(zaznamId) };
    const el = ensureDialog();
    const container = el.querySelector('[data-chat-modal-content]');
    container.innerHTML = '<p class="pm-chat-modal__loading">Načítám…</p>';
    show(el);
    await loadInto(container);
  }

  /**
   * Spec 2026-04-29: scroll preservation — před refresh modalu (binding create/delete)
   * najdi bublinu v centru viewportu a uchovej její vyjadreni-id. Po načtení nového
   * partialu scroll na tuto bublinu. Bez toho user pokaždé skočí na začátek po každém
   * vybrání kroku z dropdown.
   */
  function captureScrollAnchor(container) {
    const body = container.querySelector('.pm-chat-modal__body');
    if (!body) return null;
    const bodyRect = body.getBoundingClientRect();
    const targetY = bodyRect.top + bodyRect.height / 2;
    const bubbles = container.querySelectorAll('[data-bubble][data-vyjadreni-id]');
    let bestId = null;
    let bestDist = Infinity;
    bubbles.forEach((b) => {
      const r = b.getBoundingClientRect();
      const center = r.top + r.height / 2;
      const dist = Math.abs(center - targetY);
      if (dist < bestDist) {
        bestDist = dist;
        bestId = b.getAttribute('data-vyjadreni-id');
      }
    });
    return bestId;
  }

  function restoreScrollAnchor(container, anchorVyjadreniId) {
    if (!anchorVyjadreniId) return;
    // Wait for layout — gov komponenty hydratují asynchronně, počkat 1 rAF.
    requestAnimationFrame(() => {
      const target = container.querySelector(
        '[data-bubble][data-vyjadreni-id="' + anchorVyjadreniId + '"]'
      );
      if (target) {
        target.scrollIntoView({ behavior: 'auto', block: 'center' });
      }
    });
  }

  async function loadInto(container, options) {
    if (!currentCtx) return;
    const opts = options || {};
    const preserveAnchor = opts.preserveAnchor === true;
    const anchorId = preserveAnchor ? captureScrollAnchor(container) : null;
    try {
      const url = `/Vyjadreni/Modal?externiOdkazId=${encodeURIComponent(currentCtx.externiOdkazId)}&zaznamId=${encodeURIComponent(currentCtx.zaznamId)}`;
      const resp = await fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'text/html' } });
      if (!resp.ok) {
        container.innerHTML = `<gov-alert variant="error">Nepodařilo se načíst vyjádření (HTTP ${resp.status}).</gov-alert>`;
        return;
      }
      const html = await resp.text();
      container.innerHTML = html;
      const root = container.querySelector('[data-chat-modal-root]');
      if (root) {
        if (global.pmChatModalDragDrop && typeof global.pmChatModalDragDrop.attach === 'function') {
          global.pmChatModalDragDrop.attach(root);
        }
        if (global.pmChatModalReharvest && typeof global.pmChatModalReharvest.attach === 'function') {
          global.pmChatModalReharvest.attach(root);
        }
      }
      if (preserveAnchor) restoreScrollAnchor(container, anchorId);
    } catch (err) {
      container.innerHTML = `<gov-alert variant="error">Chyba při načítání: ${err.message || err}</gov-alert>`;
    }
  }

  /**
   * Review finding A-5: po úspěšné mutaci (create binding, delete binding) přenačti
   * partial a re-attach JS moduly. Volané z chatModalDragDrop po úspěšném Create/Delete.
   *
   * Spec 2026-04-29: refreshModal zachovává scroll position — po refresh se modal
   * scrollne na bublinu, která byla v centru viewportu před refreshem. User nemusí
   * po každém přiřazení kroku scrollovat zpět.
   */
  async function refreshModal() {
    if (!dialogEl) return;
    const container = dialogEl.querySelector('[data-chat-modal-content]');
    if (!container) return;
    await loadInto(container, { preserveAnchor: true });
  }

  function show(el) {
    if (typeof el.show === 'function') el.show();
    else el.setAttribute('open', '');
  }

  function close() {
    if (!dialogEl) return;
    if (typeof dialogEl.hide === 'function') dialogEl.hide();
    else dialogEl.removeAttribute('open');
  }

  function onClick(event) {
    const btn = event.target.closest('[data-external-chat-open]');
    if (!btn) return;
    if (btn.hasAttribute('disabled')) return;
    const externiOdkazId = btn.getAttribute('data-external-odkaz-id');
    const zaznamId = btn.getAttribute('data-external-zaznam-id');
    if (!externiOdkazId || !zaznamId) {
      console.warn('Chat open: chybí data-external-odkaz-id nebo data-external-zaznam-id na', btn);
      return;
    }
    event.preventDefault();
    open(externiOdkazId, zaznamId);
  }

  function init() {
    document.addEventListener('click', onClick);
  }

  global.pmChatModal = { init, open, close, refreshModal };
})(window);
