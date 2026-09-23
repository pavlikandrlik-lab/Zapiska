(function (global) {
  'use strict';

  // Nejdřív token panelu, teprve pak sken dokumentu. Stránka projektu žádný formulář
  // nemá, takže samotný sken vracel prázdný řetězec a server odpověděl 400 dřív, než se
  // akce spustila (2026-09-08). Stejný postup jako schedule/block.js: vlastní hook napřed.
  function getAntiForgeryToken(scope) {
    const scoped = scope
      && scope.querySelector('[data-vyzvy-antiforgery] input[name="__RequestVerificationToken"]');
    if (scoped && scoped.value) return scoped.value;

    const el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
  }

  async function postForm(url, data, scope) {
    const form = new FormData();
    form.append('__RequestVerificationToken', getAntiForgeryToken(scope));
    for (const [k, v] of Object.entries(data)) {
      if (v !== null && v !== undefined) form.append(k, String(v));
    }
    const resp = await fetch(url, { method: 'POST', body: form, credentials: 'same-origin' });
    if (!resp.ok) {
      return { success: false, errorCode: 'HttpError', message: 'HTTP ' + resp.status };
    }
    try { return await resp.json(); } catch (_) { return { success: true }; }
  }

  // Přepnutí dlaždice je čistě klientské — všechny panely jsou vyrenderované,
  // mění se jen viditelnost. Žádný dotaz na server.
  function selectTile(panelElement, klic) {
    panelElement.querySelectorAll('[data-vyzvy-tile]').forEach(function (tile) {
      const on = tile.dataset.vyzvyTile === klic;
      tile.classList.toggle('is-selected', on);
      tile.setAttribute('aria-pressed', on ? 'true' : 'false');
    });
    panelElement.querySelectorAll('[data-vyzvy-pane]').forEach(function (pane) {
      pane.hidden = pane.dataset.vyzvyPane !== klic;
    });
    panelElement.dataset.vybranaDlazdice = klic;

    // Tlačítko „Uzavřít výzvu" sedí v liště, tedy mimo dlaždici i mimo panel vpravo.
    // Cíl akce proto přebírá z vybrané dlaždice; buffer a zamčená výzva ho schovají.
    // Atribut hidden nese obalový span — pm-button si atributy z hostu strhává.
    const vybrana = panelElement.querySelector('[data-vyzvy-tile="' + klic + '"]');
    const uzavrit = panelElement.querySelector('[data-vyzvy-uzavrit]');
    if (uzavrit) {
      const lze = !!vybrana && vybrana.dataset.vyzvyMuzeUzavrit === 'true';
      uzavrit.hidden = !lze;
      uzavrit.dataset.vyzvaId = lze ? (vybrana.dataset.vyzvaId || '') : '';
    }
  }

  // Reload drží vybranou dlaždici i pozici odscrollování — po akci nesmí obrazovka
  // uskočit (spec §8.2). Změna roku je výjimka: dlaždice jiného roku v railu nejsou,
  // takže volající požádá o buffer.
  async function reloadPanel(panelElement, options) {
    const opts = options || {};
    const projectId = panelElement.dataset.projectId;
    const rok = opts.rok || panelElement.dataset.vybranyRok;
    const chtena = opts.vybrat || panelElement.dataset.vybranaDlazdice || 'buffer';
    const scrollEl = document.scrollingElement || document.documentElement;
    const scrollY = scrollEl.scrollTop;

    // 2026-09-07: panel se přestěhoval z dashboardu do projektového menu.
    const url = '/Projekty/VyzvyTabPartial/' + encodeURIComponent(projectId) +
                '?rok=' + encodeURIComponent(rok);
    const resp = await fetch(url, { credentials: 'same-origin' });
    if (!resp.ok) { showToast('Načtení panelu selhalo.', true); return; }
    const html = await resp.text();
    const tmp = document.createElement('div');
    tmp.innerHTML = html;
    const newEl = tmp.querySelector('[data-vyzvy-panel]');
    if (newEl && panelElement.parentNode) {
      panelElement.parentNode.replaceChild(newEl, panelElement);
      if (global.pmVyzvy && global.pmVyzvy.bootstrap) global.pmVyzvy.bootstrap(newEl);

      const existuje = newEl.querySelector('[data-vyzvy-tile="' + chtena + '"]');
      selectTile(newEl, existuje ? chtena : 'buffer');
      scrollEl.scrollTop = scrollY;
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

  async function handleZmenitStav(button, panelElement) {
    const vyzvaId = button.dataset.vyzvaId;
    const novyStav = button.dataset.novyStav;
    const result = await postForm('/vyzvy/zmenit-stav', { VyzvaId: vyzvaId, NovyStav: novyStav }, panelElement);
    if (result.success) {
      showToast('Stav výzvy změněn na ' + novyStav + '.');
      await reloadPanel(panelElement);
    } else {
      showToast(result.message || 'Změna stavu selhala.', true);
    }
  }

  // Po přesunu se pohled NESMÍ přepnout na cílovou dlaždici — uživatel zůstává tam,
  // kde je, jen se aktualizuje seznam a počty (spec §8.2).
  async function presunPnf(panelElement, externiOdkazId, cilovaVyzvaId) {
    if (!panelElement) return;
    const zustatNa = panelElement.dataset.vybranaDlazdice || 'buffer';
    const result = await postForm('/vyzvy/prerdit', {
      ExterniOdkazId: externiOdkazId,
      CilovaVyzvaId: cilovaVyzvaId,
    }, panelElement);
    if (!result.success) {
      showToast(result.message || 'Přesun PNF selhal.', true);
      return;
    }
    await reloadPanel(panelElement, { vybrat: zustatNa });
  }

  // Uzavření = přechod Příprava → Odesláno. Jede přes existující /vyzvy/zmenit-stav,
  // aby stavový automat i oprávnění zůstaly na jednom místě na serveru.
  async function handleUzavrit(panelElement) {
    const uzavrit = panelElement.querySelector('[data-vyzvy-uzavrit]');
    const vyzvaId = uzavrit ? uzavrit.dataset.vyzvaId : '';
    if (!vyzvaId) return;

    const result = await postForm(
      '/vyzvy/zmenit-stav', { VyzvaId: vyzvaId, NovyStav: 'Odeslano' }, panelElement);
    if (result.success) {
      showToast('Výzva byla uzavřena.');
      await reloadPanel(panelElement);
    } else {
      showToast(result.message || 'Uzavření výzvy selhalo.', true);
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
        if (action === 'zmenit-stav') { await handleZmenitStav(actionBtn, panelElement); return; }
        if (action === 'uzavrit') { await handleUzavrit(panelElement); return; }
      }
      const tile = e.target.closest('[data-vyzvy-tile]');
      if (tile) { selectTile(panelElement, tile.dataset.vyzvyTile); }
    });

    // gov-form-select emituje gov-change (ev.detail.value), ne nativní change.
    panelElement.addEventListener('gov-change', function (e) {
      const rokSelect = e.target.closest('[data-vyzvy-rok]');
      if (!rokSelect) return;
      const rok = (e.detail && e.detail.value) || '';
      if (!rok) return;
      reloadPanel(panelElement, { rok: rok, vybrat: 'buffer' });
    });
  }

  // Vstupní bod pro refreshPageScope('vyzvy-panel') — modal Nová výzva jede
  // standardní ajax-submit cestou, která umí jen scope, ne přímé volání panelu.
  async function reloadCurrentPanel(options) {
    const panelElement = document.querySelector('[data-vyzvy-panel]');
    if (!panelElement) return;
    await reloadPanel(panelElement, options);
  }

  // Bootstrap bydlí tady, ne ve vlastním modulu. Do 2026-09-08 byl v index.js, který
  // bootstrap.js importoval DŘÍV než tenhle soubor — při prvním běhu tedy bindPanel ještě
  // neexistoval, panel se přesto označil za obsloužený a klikání na dlaždice zůstalo mrtvé,
  // dokud reloadPanel element nevyměnil za nový. Tady je bindPanel definovaný o kus výš,
  // takže pořadí načítání modulů nemá co rozbít.
  function bootstrap(panelElement) {
    if (!panelElement) return;
    if (panelElement.dataset.vyzvyBootstrapped === 'true') return;
    bindPanel(panelElement);
    // Značka až po navázání — kdyby padla dřív, neúspěšný pokus panel otráví natrvalo.
    panelElement.dataset.vyzvyBootstrapped = 'true';
  }

  function bootstrapAll(root) {
    const scope = root && root.querySelectorAll ? root : document;
    if (scope.matches && scope.matches('[data-vyzvy-panel]')) { bootstrap(scope); return; }
    scope.querySelectorAll('[data-vyzvy-panel]').forEach(bootstrap);
  }

  // site.js je type="module", takže se spouští při readyState "interactive" — tedy už
  // po parsování. Panel je v DOMu a čekat na DOMContentLoaded není potřeba.
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { bootstrapAll(document); });
  } else {
    bootstrapAll(document);
  }

  document.addEventListener('pm:panel-loaded', function (e) {
    if (e.target) bootstrapAll(e.target);
  });

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bootstrap = bootstrap;
  global.pmVyzvy.bindPanel = bindPanel;
  global.pmVyzvy.reloadCurrentPanel = reloadCurrentPanel;
  global.pmVyzvy.presunPnf = presunPnf;
  global.pmVyzvy.selectTile = selectTile;
  global.pmVyzvy.reloadPanel = reloadPanel;
  global.pmVyzvy.postForm = postForm;
  global.pmVyzvy.showToast = showToast;
})(window);
