(function (global) {
  'use strict';

  // Komponenta je <gov-form-switch> (gov-design-system 4.x web component).
  // Emituje 'gov-change' event s detail.checked. Property 'checked' je reflectovaná
  // na atribut → setAttribute/removeAttribute funguje pro programovou změnu stavu.

  async function handleSwitch(switchEl, isChecked) {
    const externiOdkazId = switchEl.dataset.externiOdkazId;
    const wrap = switchEl.closest('[data-external-vyzvy-switch-wrap]');
    const statusEl = wrap ? wrap.querySelector('[data-vyzvy-switch-status]') : null;
    const hiddenState = wrap ? wrap.querySelector('[data-external-vyzvy-switch-state]') : null;

    // Nová vazba ještě nemá Id (spec 2026-09-10 R0.3), set-zaradid nemá co volat.
    // Stav nese skryté pole formuláře a PNF se do bufferu zařadí při uložení záznamu.
    if (!externiOdkazId || externiOdkazId === '0') {
      if (hiddenState) hiddenState.value = isChecked ? 'true' : 'false';
      if (statusEl) statusEl.textContent = isChecked ? 'Po uložení půjde do bufferu' : '';
      return;
    }

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

  // Delegace z document (spec 2026-09-10 R0.3): nový řádek vazby vzniká klonem šablony
  // (recordEditor/form.js) až po načtení stránky a posluchač navázaný na jednotlivé
  // přepínače by ho minul. gov-change ze Stencil komponenty probublává — stejný vzor
  // používá recordScheduleView.js.
  document.addEventListener('gov-change', function (e) {
    const target = e.target;
    if (!(target instanceof Element)) return;
    const sw = target.closest('gov-form-switch[data-vyzvy-switch]');
    if (!sw) return;
    const checked = e.detail ? !!e.detail.checked : !!sw.checked;
    handleSwitch(sw, checked);
  });
})(window);
