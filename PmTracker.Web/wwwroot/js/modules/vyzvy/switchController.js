(function (global) {
  'use strict';

  async function handleSwitch(checkbox) {
    const externiOdkazId = checkbox.dataset.externiOdkazId;
    if (!externiOdkazId) return;

    const wrap = checkbox.closest('[data-external-vyzvy-switch-wrap]');
    const statusEl = wrap ? wrap.querySelector('[data-vyzvy-switch-status]') : null;
    const hiddenState = wrap ? wrap.querySelector('[data-external-vyzvy-switch-state]') : null;
    const zaradit = checkbox.checked;

    checkbox.disabled = true;
    try {
      const result = await global.pmVyzvy.postForm('/vyzvy/set-zaradid', {
        ExterniOdkazId: externiOdkazId,
        Zaradit: zaradit,
      });
      if (result.success) {
        if (hiddenState) hiddenState.value = zaradit ? 'true' : 'false';
        if (statusEl) {
          statusEl.textContent = zaradit ? 'Čeká se (buffer projektu)' : '';
        }
      } else {
        checkbox.checked = !zaradit;
        global.pmVyzvy.showToast(result.message || 'Operace selhala.', true);
      }
    } finally {
      checkbox.disabled = false;
    }
  }

  function bindSwitches(root) {
    const scope = root || document;
    scope.querySelectorAll('[data-vyzvy-switch]').forEach(function (cb) {
      if (cb.dataset.vyzvyBound === 'true') return;
      cb.dataset.vyzvyBound = 'true';
      cb.addEventListener('change', function () { handleSwitch(cb); });
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
