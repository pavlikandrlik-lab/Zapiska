/**
 * chatModalDragDrop.js — orchestrace drag&drop v modalu Vyjádření a termíny.
 *
 * 2026-04-28 redesign: bublinky už nejsou draggable. Drag drive z kroku přes
 * stepperDragSnap.js + sticky alignment přes stepperSticky.js + buffer
 * management přes stepperBuffer.js.
 *
 * Spec: docs/superpowers/specs/2026-04-28-modal-vyjadreni-redesign-design.md
 *
 * Backward-compat API: window.pmChatModalDragDrop.attach(root) — voláno z chatModal.js.
 */

import { initStepperSticky } from './stepperSticky.js';
import { initStepperDragSnap } from './stepperDragSnap.js';
import { moveStepFromBufferToAligned, moveStepFromAlignedToBuffer } from './stepperBuffer.js';

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
    if (!root) return null;
    if (root.getAttribute('data-chat-init') === 'true') return null;
    root.setAttribute('data-chat-init', 'true');

    const externiOdkazId = Number(root.getAttribute('data-externi-odkaz-id'));
    const zaznamId = Number(root.getAttribute('data-zaznam-id'));
    const projektId = Number(root.getAttribute('data-projekt-id'));

    // Sticky alignment kroku vedle bound bubliny.
    const sticky = initStepperSticky(root);

    // Cursor-tracked drag s magnetic snap.
    const dragSnap = initStepperDragSnap(root, {
        onCommit: async function (payload) {
            const apiPayload = {
                externiOdkazId: externiOdkazId,
                zaznamId: zaznamId,
                projektId: projektId,
                krokKey: payload.krokKey,
                hotVyjadreniId: Number(payload.vyjadreniId),
                datumVyjadreni: payload.datum
            };
            setStatus(root, 'Ukládám…', null);
            const result = await createBinding(root, apiPayload);
            // Po úspěšném commit přesun z bufferu do aligned (pokud byl)
            moveStepFromBufferToAligned(root, payload.krokKey);
            if (sticky && sticky.recompute) sticky.recompute();
            setStatus(root, 'Uloženo.', 'ok');
            return result;
        }
    });

    // „Odpojit" tlačítka — POST Delete + přesun zpět do bufferu.
    root.querySelectorAll('[data-clear-binding]').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const item = btn.closest('gov-stepper-item[data-step]');
            if (!item) return;
            const krokKey = item.getAttribute('data-krok-key');
            const vazbaId = Number(item.getAttribute('data-vazba-id'));
            if (!vazbaId) {
                setStatus(root, 'Chybí data-vazba-id — nelze odpojit.', 'error');
                return;
            }
            const payload = { vazbaId: vazbaId, projektId: projektId };
            setStatus(root, 'Odpojuji…', null);
            try {
                await deleteBinding(root, payload);
                moveStepFromAlignedToBuffer(root, krokKey);
                if (sticky && sticky.recompute) sticky.recompute();
                setStatus(root, 'Odpojeno.', 'ok');
            } catch (err) {
                setStatus(root, 'Odpojení selhalo: ' + (err.message || err), 'error');
            }
        });
    });

    return {
        destroy: function () {
            if (sticky) sticky.destroy();
            if (dragSnap) dragSnap.destroy();
        }
    };
}

// Backward-compat: chatModal.js volá window.pmChatModalDragDrop.attach(root).
if (typeof window !== 'undefined') {
    window.pmChatModalDragDrop = { attach: attach };
}

export { attach };
