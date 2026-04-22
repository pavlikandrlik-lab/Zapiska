/**
 * Dočasný otvírač pro tlačítko 💬 (Vyjádření a termíny).
 * Ukazuje stub modal se zprávou, že chat modal je v přípravě (dělá se v Plánu C).
 */
(function (global) {
  'use strict';

  let dialogEl = null;

  function ensureDialog() {
    if (dialogEl) return dialogEl;
    dialogEl = document.createElement('gov-dialog');
    dialogEl.setAttribute('size', 'm');
    dialogEl.innerHTML = `
      <div slot="label">Vyjádření a termíny</div>
      <p>Chat modal s vyjádřeními a drag &amp; drop přiřazením ke krokům harmonogramu se připravuje.
         V aktuální verzi lze pracovat s ručními sloupci Plán / Skutečnost v záložce Harmonogram.</p>
      <div slot="footer" style="display:flex; justify-content:flex-end">
        <pm-button variant="Primary" data-chat-stub-close>OK</pm-button>
      </div>
    `;
    document.body.appendChild(dialogEl);
    dialogEl.addEventListener('click', (event) => {
      const btn = event.target.closest('[data-chat-stub-close]');
      if (btn) close();
    });
    return dialogEl;
  }

  function open() {
    const el = ensureDialog();
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
    event.preventDefault();
    open();
  }

  function init() {
    document.addEventListener('click', onClick);
  }

  global.pmExterniOdkazChatStub = { init };
})(window);
