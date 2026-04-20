(function (global) {
  'use strict';

  function getAntiForgeryToken() {
    const el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  async function postForm(url, data) {
    const form = new FormData();
    form.append('__RequestVerificationToken', getAntiForgeryToken());
    for (const [k, v] of Object.entries(data)) {
      if (v !== null && v !== undefined) form.append(k, String(v));
    }
    const resp = await fetch(url, { method: 'POST', body: form, credentials: 'same-origin' });
    if (!resp.ok) {
      return { success: false, errorCode: 'HttpError', message: 'HTTP ' + resp.status };
    }
    try { return await resp.json(); } catch (_) { return { success: true }; }
  }

  async function reloadPanel(panelElement) {
    const projectId = panelElement.dataset.projectId;
    const url = '/projekty/' + encodeURIComponent(projectId) + '/dashboard/vyzvy-panel';
    const resp = await fetch(url, { credentials: 'same-origin' });
    if (!resp.ok) { showToast('Načtení panelu selhalo.', true); return; }
    const html = await resp.text();
    const tmp = document.createElement('div');
    tmp.innerHTML = html;
    const newEl = tmp.querySelector('[data-vyzvy-panel]');
    if (newEl && panelElement.parentNode) {
      panelElement.parentNode.replaceChild(newEl, panelElement);
      if (global.pmVyzvy && global.pmVyzvy.bootstrap) global.pmVyzvy.bootstrap(newEl);
    }
  }

  function showToast(message, isError) {
    if (global.pmToast && typeof global.pmToast.show === 'function') {
      global.pmToast.show(message, { type: isError ? 'error' : 'info' });
    } else {
      // Fallback
      if (isError) { console.error(message); alert(message); }
      else { console.info(message); }
    }
  }

  async function handleZalozit(button, panelElement) {
    const projektId = button.dataset.projektId;
    button.disabled = true;
    try {
      const result = await postForm('/vyzvy/zalozit', { ProjektId: projektId });
      if (result.success) {
        showToast('Výzva založena.');
        await reloadPanel(panelElement);
      } else {
        showToast(result.message || 'Založení výzvy selhalo.', true);
      }
    } finally {
      button.disabled = false;
    }
  }

  async function handleZmenitStav(button, panelElement) {
    const vyzvaId = button.dataset.vyzvaId;
    const novyStav = button.dataset.novyStav;
    const result = await postForm('/vyzvy/zmenit-stav', { VyzvaId: vyzvaId, NovyStav: novyStav });
    if (result.success) {
      showToast('Stav výzvy změněn na ' + novyStav + '.');
      await reloadPanel(panelElement);
    } else {
      showToast(result.message || 'Změna stavu selhala.', true);
    }
  }

  function handleToggleCollapse(headerElement) {
    const card = headerElement.closest('[data-vyzvy-vyzva]');
    if (!card) return;
    card.classList.toggle('open');
    const btn = card.querySelector('.vyzvy-card-toggle-btn');
    if (btn) {
      const open = card.classList.contains('open');
      btn.setAttribute('aria-expanded', open ? 'true' : 'false');
      const icon = btn.querySelector('.vyzvy-card-toggle-icon');
      if (icon) icon.textContent = open ? '▼' : '▶';
    }
  }

  function handleStavMenuToggle(toggleBtn) {
    const menu = toggleBtn.closest('[data-vyzvy-stav-menu]');
    if (menu) menu.classList.toggle('open');
  }

  function bindPanel(panelElement) {
    panelElement.addEventListener('click', async function (e) {
      const stavToggle = e.target.closest('[data-vyzvy-stav-toggle]');
      if (stavToggle) {
        e.preventDefault();
        handleStavMenuToggle(stavToggle);
        return;
      }
      const actionBtn = e.target.closest('[data-vyzvy-action]');
      if (actionBtn) {
        const action = actionBtn.dataset.vyzvyAction;
        if (action === 'zalozit') { await handleZalozit(actionBtn, panelElement); return; }
        if (action === 'zmenit-stav') { await handleZmenitStav(actionBtn, panelElement); return; }
        if (action === 'otevrit-reassign') {
          if (global.pmVyzvy && global.pmVyzvy.openReassignModal) {
            global.pmVyzvy.openReassignModal(panelElement.dataset.projectId, function () { reloadPanel(panelElement); });
          }
          return;
        }
      }
      const toggleHeader = e.target.closest('[data-vyzvy-toggle]');
      if (toggleHeader && !e.target.closest('[data-vyzvy-action], [data-vyzvy-stav-toggle], .vyzvy-stav-menu-list')) {
        handleToggleCollapse(toggleHeader);
      }
    });
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bindPanel = bindPanel;
  global.pmVyzvy.reloadPanel = reloadPanel;
  global.pmVyzvy.postForm = postForm;
  global.pmVyzvy.showToast = showToast;
})(window);
