(function (global) {
  'use strict';

  // Komponenta je <gov-form-switch> (gov-design-system 4.x web component).
  // Emituje 'gov-change' event s detail.checked. Property 'checked' je reflectovaná
  // na atribut → setAttribute/removeAttribute funguje pro programovou změnu stavu.

  async function handleSwitch(switchEl, isChecked) {
    const externiOdkazId = switchEl.dataset.externiOdkazId;
    if (!externiOdkazId) return;

    const wrap = switchEl.closest('[data-external-vyzvy-switch-wrap]');
    const statusEl = wrap ? wrap.querySelector('[data-vyzvy-switch-status]') : null;
    const hiddenState = wrap ? wrap.querySelector('[data-external-vyzvy-switch-state]') : null;

    switchEl.setAttribute('disabled', '');
    try {
      const result = await global.pmVyzvy.postForm('/vyzvy/set-zaradid', {
        ExterniOdkazId: externiOdkazId,
        Zaradit: isChecked,
      });
      if (result.success) {
        if (hiddenState) hiddenState.value = isChecked ? 'true' : 'false';
        if (statusEl) {
          statusEl.textContent = isChecked ? 'Čeká se (buffer projektu)' : '';
        }
      } else {
        // rollback komponenty zpět
        if (isChecked) {
          switchEl.removeAttribute('checked');
        } else {
          switchEl.setAttribute('checked', '');
        }
        global.pmVyzvy.showToast(result.message || 'Operace selhala.', true);
      }
    } finally {
      switchEl.removeAttribute('disabled');
    }
  }

  function bindSwitches(root) {
    const scope = root || document;
    scope.querySelectorAll('gov-form-switch[data-vyzvy-switch]').forEach(function (sw) {
      if (sw.dataset.vyzvyBound === 'true') return;
      sw.dataset.vyzvyBound = 'true';
      sw.addEventListener('gov-change', function (e) {
        const checked = e && e.detail ? !!e.detail.checked : !!sw.checked;
        handleSwitch(sw, checked);
      });
    });
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bindSwitches = bindSwitches;

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { bindSwitches(); });
  } else {
    bindSwitches();
  }
  document.addEventListener('pm:record-editor-loaded', function (e) { bindSwitches(e.target || document); });
})(window);
