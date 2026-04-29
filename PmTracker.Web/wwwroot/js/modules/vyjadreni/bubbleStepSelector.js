/**
 * bubbleStepSelector.js — onChange handler pro <gov-form-select> v bublinách
 * + clear handler pro ✕ tlačítko + click-to-scroll na stepper items.
 *
 * Spec: docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md §4
 *
 * Tato modul nahrazuje stepperDragSnap.js (drag broken). User vybírá krok přes
 * dropdown v každé bublině. Validace 1:1 a chronologie je pre-computed na serverové
 * straně (StepOptions[].IsDisabled). Po každé změně refreshModal() re-fetch partial.
 */

function getCsrfToken(root) {
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

async function refreshModalIfPossible() {
    if (window.pmChatModal && typeof window.pmChatModal.refreshModal === 'function') {
        await window.pmChatModal.refreshModal();
    }
}

async function createBinding(root, payload) {
    const token = getCsrfToken(root);
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
    const token = getCsrfToken(root);
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

export function attachBubbleStepSelectors(root) {
    if (!root) return;

    const externiOdkazId = Number(root.getAttribute('data-externi-odkaz-id'));
    const zaznamId = Number(root.getAttribute('data-zaznam-id'));
    const projektId = Number(root.getAttribute('data-projekt-id'));

    // Dropdown change → POST Create binding.
    root.querySelectorAll('[data-bubble-step-selector]').forEach(function (select) {
        select.addEventListener('gov-change', async function (ev) {
            const newKrokKey = (ev.detail && ev.detail.value) || '';
            if (!newKrokKey) return;  // user vybral "—" — no-op

            const vyjadreniId = select.getAttribute('data-vyjadreni-id');
            const vyjadreniDatum = select.getAttribute('data-vyjadreni-datum');
            const payload = {
                externiOdkazId: externiOdkazId,
                zaznamId: zaznamId,
                projektId: projektId,
                krokKey: newKrokKey,
                hotVyjadreniId: Number(vyjadreniId),
                datumVyjadreni: vyjadreniDatum
            };

            setStatus(root, 'Ukládám…', null);
            try {
                await createBinding(root, payload);
                setStatus(root, 'Krok přiřazen.', 'ok');
                await refreshModalIfPossible();
            } catch (err) {
                console.error('Bubble step bind failed', err);
                setStatus(root, 'Uložení selhalo: ' + (err.message || err), 'error');
            }
        });
    });

    // ✕ tlačítko na badge → POST Delete binding (najít vazba ID ze stepperu).
    root.querySelectorAll('[data-clear-bubble-binding]').forEach(function (btn) {
        btn.addEventListener('click', async function () {
            const vyjadreniId = btn.getAttribute('data-vyjadreni-id');
            const stepperItem = root.querySelector(
                'gov-stepper-item[data-step][data-current-vyjadreni-id="' + vyjadreniId + '"]'
            );
            const vazbaId = stepperItem ? Number(stepperItem.getAttribute('data-vazba-id')) : 0;
            if (!vazbaId) {
                setStatus(root, 'Nelze odebrat — chybí ID vazby.', 'error');
                return;
            }
            const payload = { vazbaId: vazbaId, projektId: projektId };
            setStatus(root, 'Odpojuji…', null);
            try {
                await deleteBinding(root, payload);
                setStatus(root, 'Krok odebrán.', 'ok');
                await refreshModalIfPossible();
            } catch (err) {
                console.error('Bubble clear binding failed', err);
                setStatus(root, 'Odebrání selhalo: ' + (err.message || err), 'error');
            }
        });
    });

    // Stepper item click → scroll na bound bublinu (smooth animation).
    root.querySelectorAll('gov-stepper-item[data-step]').forEach(function (item) {
        item.addEventListener('click', function (ev) {
            // Ignore klik na "Odpojit" tlačítko uvnitř — má vlastní handler.
            if (ev.target.closest('[data-clear-binding]')) return;
            const vyjadreniId = item.getAttribute('data-current-vyjadreni-id');
            if (!vyjadreniId) return;
            const bubble = root.querySelector(
                '[data-bubble][data-vyjadreni-id="' + vyjadreniId + '"]'
            );
            if (bubble) bubble.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
    });

    // Stepper „Odpojit" tlačítko → POST Delete (paralelní cesta k ✕ na badge).
    root.querySelectorAll('[data-clear-binding]').forEach(function (btn) {
        btn.addEventListener('click', async function (ev) {
            ev.stopPropagation();
            const item = btn.closest('gov-stepper-item[data-step]');
            if (!item) return;
            const vazbaId = Number(item.getAttribute('data-vazba-id'));
            if (!vazbaId) {
                setStatus(root, 'Chybí data-vazba-id — nelze odpojit.', 'error');
                return;
            }
            const payload = { vazbaId: vazbaId, projektId: projektId };
            setStatus(root, 'Odpojuji…', null);
            try {
                await deleteBinding(root, payload);
                setStatus(root, 'Odpojeno.', 'ok');
                await refreshModalIfPossible();
            } catch (err) {
                setStatus(root, 'Odpojení selhalo: ' + (err.message || err), 'error');
            }
        });
    });
}
