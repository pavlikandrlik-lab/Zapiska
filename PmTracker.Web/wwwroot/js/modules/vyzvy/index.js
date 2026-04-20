(function (global) {
  'use strict';

  function bootstrap(panelElement) {
    if (!panelElement) return;
    if (panelElement.dataset.vyzvyBootstrapped === 'true') return;
    panelElement.dataset.vyzvyBootstrapped = 'true';
    if (global.pmVyzvy && global.pmVyzvy.bindPanel) {
      global.pmVyzvy.bindPanel(panelElement);
    }
  }

  function initOnDomReady() {
    document.querySelectorAll('[data-vyzvy-panel]').forEach(bootstrap);
  }

  global.pmVyzvy = global.pmVyzvy || {};
  global.pmVyzvy.bootstrap = bootstrap;

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initOnDomReady);
  } else {
    initOnDomReady();
  }

  document.addEventListener('pm:panel-loaded', function (e) {
    const root = e.target;
    if (!root) return;
    if (root.matches && root.matches('[data-vyzvy-panel]')) { bootstrap(root); return; }
    if (root.querySelectorAll) {
      root.querySelectorAll('[data-vyzvy-panel]').forEach(bootstrap);
    }
  });
})(window);
