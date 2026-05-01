// Phase 12 (DESIGN-9-C, 2026-05-01) — pre-fetch staging UX flow.
//
// Když user na kartě externí vazby zadá Cislo6 a opustí input (blur), nebo když
// editor harmonogram tab dostane focus, JS volá POST /Harmonogram/PreviewSync s ZaznamId.
// Server vrátí HarmonogramSyncPlan (ComputePlanAsync, žádný DB write). JS uloží plán
// do sessionStorage pod klíčem `pmtracker.harmonogramPreview.{zaznamId}`.
//
// Editor harmonogram tab může číst staging před render (custom event
// pm-harmonogram-preview-ready). Submit form → clear staging.

(function (global) {
    'use strict';

    const STORAGE_KEY_PREFIX = 'pmtracker.harmonogramPreview.';
    const STORAGE_TTL_MS = 30 * 60 * 1000; // 30 min

    function getAntiforgery() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    async function fetchPreview(zaznamId) {
        const token = getAntiforgery();
        const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;
        const resp = await fetch('/Harmonogram/PreviewSync', {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify({ ZaznamId: zaznamId })
        });
        if (!resp.ok) {
            throw new Error(`Preview fetch failed: ${resp.status}`);
        }
        return await resp.json();
    }

    function storeStaging(zaznamId, plan) {
        if (!zaznamId || !plan) return;
        const wrapped = { fetchedAt: Date.now(), plan: plan };
        try {
            sessionStorage.setItem(STORAGE_KEY_PREFIX + zaznamId, JSON.stringify(wrapped));
        } catch (e) {
            console.warn('preview-sync: sessionStorage write failed', e);
        }
    }

    function readStaging(zaznamId) {
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY_PREFIX + zaznamId);
            if (!raw) return null;
            const wrapped = JSON.parse(raw);
            if (Date.now() - (wrapped.fetchedAt || 0) > STORAGE_TTL_MS) {
                sessionStorage.removeItem(STORAGE_KEY_PREFIX + zaznamId);
                return null;
            }
            return wrapped.plan;
        } catch {
            return null;
        }
    }

    function clearStaging(zaznamId) {
        try { sessionStorage.removeItem(STORAGE_KEY_PREFIX + zaznamId); } catch {}
    }

    async function handleBlur(event) {
        // Listener delegate na input pole s data-pm-prefetch-zaznam-id atributem.
        // Toto markup atribut musí být vystavený UI v místech kde dává smysl pre-fetch
        // (typicky karta externí vazby — input pro Cislo6 nebo skrytý ZaznamId field).
        const input = event.target?.closest('[data-pm-prefetch-zaznam-id]');
        if (!input) return;
        const zaznamId = parseInt(input.value || input.getAttribute('data-zaznam-id') || '0', 10);
        if (!Number.isFinite(zaznamId) || zaznamId <= 0) return;

        try {
            const plan = await fetchPreview(zaznamId);
            storeStaging(zaznamId, plan);
            // Custom event pro editor harmonogram tab — může re-renderovat s preview daty.
            document.dispatchEvent(new CustomEvent('pm-harmonogram-preview-ready', {
                detail: { zaznamId, plan }
            }));
        } catch (e) {
            console.warn('preview-sync fetch failed', e);
        }
    }

    function handleFormSubmit(event) {
        // Při submit form clear staging (po commitu jsou irrelevantní).
        const form = event.target?.closest('form[data-record-editor-form]');
        if (!form) return;
        const zaznamIdField = form.querySelector('input[name="Id"]');
        const zaznamId = parseInt(zaznamIdField?.value || '0', 10);
        if (zaznamId > 0) clearStaging(zaznamId);
    }

    function init() {
        document.addEventListener('blur', handleBlur, /* capture */ true);
        document.addEventListener('submit', handleFormSubmit);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    global.pmPreviewSync = { readStaging, clearStaging, fetchPreview };
})(window);
