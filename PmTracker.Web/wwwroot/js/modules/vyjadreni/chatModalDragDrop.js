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

    // Drop targets — legacy `<ol data-step>` flow (Plán C Task 16/17).
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
        await dispatchCreateBinding(root, payload);
      });
    });

    // Plán 4 Feature C gap #5 (2026-04-24): nový `<pm-chat-stepper>` custom element emituje
    // `pm-chat-stepper-drop` event místo standardního drop. Listener přebírá event detail
    // a volá stejný backend endpoint. Legacy `<ol hidden>` bude odstraněn až se potvrdí
    // stability tohoto flow.
    const stepperElements = root.querySelectorAll('pm-chat-stepper, [data-pm-chat-stepper]');
    stepperElements.forEach((stepper) => {
      stepper.addEventListener('pm-chat-stepper-drop', async (ev) => {
        const detail = ev.detail || {};
        if (!detail.bubbleId || !detail.krokKey) {
          setStatus(root, 'Drop: chybí ID kroku nebo bubliny.', 'error');
          return;
        }
        const payload = {
          externiOdkazId: externiOdkazId,
          zaznamId: zaznamId,
          projektId: projektId,
          krokKey: detail.krokKey,
          hotVyjadreniId: Number(detail.bubbleId),
          datumVyjadreni: detail.bubbleDatum || null
        };
        await dispatchCreateBinding(root, payload);
      });
    });

    async function dispatchCreateBinding(rootEl, payload) {
      persistPending(rootEl, { op: 'create', payload });
      setStatus(rootEl, 'Ukládám…', null);
      try {
        await createBinding(rootEl, payload);
        setStatus(rootEl, 'Uloženo.', 'ok');
        clearPending(rootEl);
        if (global.pmChatModal && typeof global.pmChatModal.refreshModal === 'function') {
          await global.pmChatModal.refreshModal();
        }
      } catch (err) {
        setStatus(rootEl, 'Ukládání selhalo: ' + (err.message || err), 'error');
      }
    }

    // A-5: „Odpojit" tlačítka — posílají POST Delete s data-vazba-id, pak refresh.
    root.querySelectorAll('[data-clear-binding]').forEach((btn) => {
      btn.addEventListener('click', async () => {
        const step = btn.closest('[data-step]');
        if (!step) return;
        const vazbaId = Number(step.getAttribute('data-vazba-id'));
        if (!vazbaId) {
          setStatus(root, 'Chybí data-vazba-id — nelze odpojit.', 'error');
          return;
        }
        const payload = { vazbaId: vazbaId, projektId: projektId };
        persistPending(root, { op: 'delete', payload });
        setStatus(root, 'Odpojuji…', null);
        try {
          await deleteBinding(root, payload);
          setStatus(root, 'Odpojeno.', 'ok');
          clearPending(root);
          if (global.pmChatModal && typeof global.pmChatModal.refreshModal === 'function') {
            await global.pmChatModal.refreshModal();
          }
        } catch (err) {
          setStatus(root, 'Odpojení selhalo: ' + (err.message || err), 'error');
        }
      });
    });
  }

  global.pmChatModalDragDrop = { attach };
})(window);
