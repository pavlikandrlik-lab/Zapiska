(function (global) {
  'use strict';

  let modalElement = null;
  let onCloseCallback = null;

  function ensureModalContainer() {
    let el = document.getElementById('pm-vyzvy-reassign-modal');
    if (!el) {
      el = document.createElement('div');
      el.id = 'pm-vyzvy-reassign-modal';
      el.className = 'pm-modal';
      el.hidden = true;
      document.body.appendChild(el);
    }
    modalElement = el;
    return el;
  }

  async function loadModal(projektId) {
    const el = ensureModalContainer();
    const resp = await fetch('/vyzvy/reassign-modal?projektId=' + encodeURIComponent(projektId), {
      credentials: 'same-origin',
    });
    if (!resp.ok) {
      global.pmVyzvy.showToast('Nepodařilo se načíst modal.', true);
      return;
    }
    const html = await resp.text();
    el.innerHTML = html;
    el.hidden = false;
    bindDnd(el);
    bindClose(el);
  }

  function bindClose(el) {
    el.querySelectorAll('[data-vyzvy-reassign-close]').forEach(function (btn) {
      btn.addEventListener('click', function () { close(); });
    });
    el.addEventListener('click', function (e) { if (e.target === el) close(); });
  }

  function close() {
    if (modalElement) {
      modalElement.hidden = true;
      modalElement.innerHTML = '';
    }
    if (onCloseCallback) {
      try { onCloseCallback(); } catch (_) {}
    }
    onCloseCallback = null;
  }

  function bindDnd(rootEl) {
    const items = rootEl.querySelectorAll('[data-vyzvy-reassign-item]');
    const targets = rootEl.querySelectorAll('[data-vyzvy-reassign-target]');

    items.forEach(function (item) {
      item.addEventListener('dragstart', function (e) {
        item.classList.add('dragging');
        e.dataTransfer.setData('text/plain', item.dataset.externiOdkazId);
        e.dataTransfer.effectAllowed = 'move';
      });
      item.addEventListener('dragend', function () { item.classList.remove('dragging'); });
    });

    targets.forEach(function (target) {
      target.addEventListener('dragover', function (e) {
        e.preventDefault();
        target.classList.add('drag-over');
      });
      target.addEventListener('dragleave', function () { target.classList.remove('drag-over'); });
      target.addEventListener('drop', async function (e) {
        e.preventDefault();
        target.classList.remove('drag-over');
        const externiOdkazId = e.dataTransfer.getData('text/plain');
        const cilovaVyzvaId = target.dataset.cilovaVyzvaId || '';
        const columnsEl = rootEl.querySelector('[data-vyzvy-reassign-columns]');
        const projektId = columnsEl ? columnsEl.dataset.projektId : '';

        const result = await global.pmVyzvy.postForm('/vyzvy/prerdit', {
          ExterniOdkazId: externiOdkazId,
          CilovaVyzvaId: cilovaVyzvaId,
        });
        if (result.success) {
          await loadModal(projektId);
        } else {
          global.pmVyzvy.showToast(result.message || 'Přeřazení selhalo.', true);
        }
      });
    });
  }

  async function openReassignModal(projektId, onClose) {
    onCloseCallback = onClose || null;
    await loadModal(projektId);
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.openReassignModal = openReassignModal;
})(window);
