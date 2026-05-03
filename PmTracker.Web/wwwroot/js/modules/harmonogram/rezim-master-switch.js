/**
 * FIX 2026-05-03 — JS handler pro master switch "Automatické vyplňování harmonogramu".
 *
 * Switch (gov-form-switch s data-record-rezim-switch) v tab strip řádku ovládá bulk
 * SkutecnostRezim všech DELAY řádků daného záznamu:
 *   ON  = Auto (auto-fill ze ServiceDesk vyjádření)
 *   OFF = Manual (uživatel vyplňuje datumy ručně, žádný auto sync)
 *
 * Pro saved záznam (ZaznamId > 0): POST /Harmonogram/BulkSetRezim → bulk update DELAY rows.
 * Pro Create flow (ZaznamId == 0): no server call, switch state se persistuje až s Save
 * (form data atribut data-record-rezim-pending = "Auto" / "Manual" čte SaveRecord).
 *
 * Side-effect import v bootstrap.js (memory: project_bundle_sync).
 */
(function (global) {
  'use strict';

  const SWITCH_SELECTOR = '[data-record-rezim-switch]';

  function getCsrfToken() {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function getAsUserParam() {
    try {
      const u = new URL(window.location.href);
      return u.searchParams.get('asUser') || '';
    } catch {
      return '';
    }
  }

  async function bulkSetRezim(zaznamId, rezim) {
    const token = getCsrfToken();
    const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
    if (token) headers['RequestVerificationToken'] = token;
    // Dev: forward asUser query do POST URL aby UserContextMiddleware mohl resolvnout principal.
    const asUser = getAsUserParam();
    const url = asUser
      ? `/Harmonogram/BulkSetRezim?asUser=${encodeURIComponent(asUser)}`
      : '/Harmonogram/BulkSetRezim';
    const resp = await fetch(url, {
      method: 'POST',
      headers: headers,
      credentials: 'same-origin',
      body: JSON.stringify({ ZaznamId: zaznamId, Rezim: rezim })
    });
    if (!resp.ok) {
      const body = await resp.text().catch(() => '');
      throw new Error(`HTTP ${resp.status}: ${body || resp.statusText}`);
    }
    return await resp.json();
  }

  function showStatus(switchEl, message, color) {
    let banner = switchEl.parentElement?.querySelector('[data-record-rezim-banner]');
    if (!banner) {
      banner = document.createElement('gov-message');
      banner.setAttribute('data-record-rezim-banner', '');
      switchEl.insertAdjacentElement('afterend', banner);
    }
    banner.setAttribute('color', color || 'info');
    banner.textContent = message;
    if (color !== 'danger') {
      setTimeout(() => banner?.remove(), 5000);
    }
  }

  async function handleChange(event) {
    const sw = event.target?.closest?.(SWITCH_SELECTOR);
    if (!sw) return;

    const isChecked = sw.hasAttribute('checked')
      || sw.getAttribute('aria-checked') === 'true';
    const rezim = isChecked ? 'Auto' : 'Manual';

    const zaznamIdRaw = sw.getAttribute('data-record-zaznam-id') || '0';
    const zaznamId = parseInt(zaznamIdRaw, 10);

    // Create flow (Id=0) — jen lokální state pro Save POST.
    if (!Number.isFinite(zaznamId) || zaznamId <= 0) {
      const form = sw.closest('form[data-record-editor-form]');
      if (form) form.setAttribute('data-record-rezim-pending', rezim);
      return;
    }

    // Saved záznam — bulk endpoint.
    sw.setAttribute('disabled', 'true');
    try {
      const result = await bulkSetRezim(zaznamId, rezim);
      const changed = result?.changed ?? 0;
      if (result?.syncFailed) {
        showStatus(sw, `Rezim přepnut, ale auto-sync selhal: ${result.syncFailReason || 'neznámá chyba'}`, 'warning');
      } else if (changed === 0 && result?.message) {
        showStatus(sw, result.message, 'info');
      } else {
        showStatus(sw, `Rezim přepnut na ${rezim} (${changed} řádek změněno).`, 'success');
      }
    } catch (err) {
      console.warn('BulkSetRezim selhal:', err);
      showStatus(sw, `Přepnutí selhalo: ${err.message}`, 'danger');
      // Revert switch state
      if (isChecked) sw.removeAttribute('checked');
      else sw.setAttribute('checked', '');
    } finally {
      sw.removeAttribute('disabled');
    }
  }

  function init() {
    // gov-form-switch emituje 'gov-change' i nativní 'change'.
    document.addEventListener('change', handleChange);
    document.addEventListener('gov-change', handleChange);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  global.pmRezimMasterSwitch = { bulkSetRezim };
})(window);
