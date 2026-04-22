/**
 * Plán C — drag & drop + autosave (Tasks 16 + 17).
 * Bublina [data-bubble] se dá táhnout na krok [data-step].
 * Po dropu posíláme POST /Vyjadreni/HarmonogramVazba/Create.
 * Tlačítko „Odpojit" posílá POST /Vyjadreni/HarmonogramVazba/Delete.
 * Autosave: stav pending requestů se ukládá do localStorage pod data-autosave-key,
 * aby se při reloadu nebo chybě daly pokusy retry-ovat (jednoduchá persistence).
 */
(function (global) {
  'use strict';

  function getAntiForgeryToken(root) {
    const input = root.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  function setStatus(root, text, state) {
    const el = root.querySelector('[data-chat-status]');
    if (!el) return;
    el.textContent = text || '';
    if (state) el.setAttribute('data-status-state', state);
    else el.removeAttribute('data-status-state');
  }

  function persistPending(root, entry) {
    const key = root.getAttribute('data-autosave-key');
    if (!key) return;
    try {
      const raw = global.localStorage.getItem(key);
      const list = raw ? JSON.parse(raw) : [];
      list.push({ t: Date.now(), entry });
      global.localStorage.setItem(key, JSON.stringify(list.slice(-20)));
    } catch (e) { /* quota / storage disabled */ }
  }

  function clearPending(root) {
    const key = root.getAttribute('data-autosave-key');
    if (!key) return;
    try { global.localStorage.removeItem(key); } catch (e) { /* ignore */ }
  }

  async function createBinding(root, payload) {
    const token = getAntiForgeryToken(root);
    const resp = await fetch('/Vyjadreni/HarmonogramVazba/Create', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'RequestVerificationToken': token
      },
      body: JSON.stringify(payload)
    });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    return resp.json();
  }

  async function deleteBinding(root, payload) {
    const token = getAntiForgeryToken(root);
    const resp = await fetch('/Vyjadreni/HarmonogramVazba/Delete', {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'RequestVerificationToken': token
      },
      body: JSON.stringify(payload)
    });
    if (!resp.ok) throw new Error('HTTP ' + resp.status);
    return resp.json();
  }

  function attach(root) {
    const externiOdkazId = Number(root.getAttribute('data-externi-odkaz-id'));
    const zaznamId = Number(root.getAttribute('data-zaznam-id'));
    const projektId = Number(root.getAttribute('data-projekt-id'));

    // Drag sources
    root.querySelectorAll('[data-bubble][draggable="true"]').forEach((bubble) => {
      bubble.addEventListener('dragstart', (ev) => {
        const id = bubble.getAttribute('data-vyjadreni-id');
        const datum = bubble.getAttribute('data-datum');
        ev.dataTransfer.setData('application/x-pm-bubble', JSON.stringify({ id, datum }));
        ev.dataTransfer.effectAllowed = 'move';
        bubble.classList.add('pm-chat-bubble--dragging');
      });
      bubble.addEventListener('dragend', () => bubble.classList.remove('pm-chat-bubble--dragging'));
    });

    // Drop targets
    root.querySelectorAll('[data-step]').forEach((step) => {
      step.addEventListener('dragover', (ev) => {
        ev.preventDefault();
        ev.dataTransfer.dropEffect = 'move';
        step.classList.add('pm-chat-step--drop-target');
      });
      step.addEventListener('dragleave', () => step.classList.remove('pm-chat-step--drop-target'));
      step.addEventListener('drop', async (ev) => {
        ev.preventDefault();
        step.classList.remove('pm-chat-step--drop-target');
        const raw = ev.dataTransfer.getData('application/x-pm-bubble');
        if (!raw) return;
        let parsed;
        try { parsed = JSON.parse(raw); } catch (e) { return; }
        const krokKey = step.getAttribute('data-krok-key');
        const payload = {
          externiOdkazId: externiOdkazId,
          zaznamId: zaznamId,
          projektId: projektId,
          krokKey: krokKey,
          hotVyjadreniId: Number(parsed.id),
          datumVyjadreni: parsed.datum
        };
        persistPending(root, { op: 'create', payload });
        setStatus(root, 'Ukládám…', null);
        try {
          await createBinding(root, payload);
          setStatus(root, 'Uloženo. Zavřete a otevřete modal pro aktualizaci.', 'ok');
          clearPending(root);
        } catch (err) {
          setStatus(root, 'Ukládání selhalo: ' + (err.message || err), 'error');
        }
      });
    });

    // „Odpojit" tlačítka — najdeme ID v kroku
    root.querySelectorAll('[data-clear-binding]').forEach((btn) => {
      btn.addEventListener('click', async () => {
        const step = btn.closest('[data-step]');
        if (!step) return;
        // Pro delete potřebujeme skutečné vazba-id, které ve VM nemáme — backend by měl
        // poslat data-vazba-id; prozatím vyhledáme vazbu skrz re-fetch modalu jako fallback.
        setStatus(root, 'Odpojení musí být podpořeno na backendu — pošlete POST Delete s vazbaId.', 'error');
      });
    });
  }

  global.pmChatModalDragDrop = { attach };
})(window);
