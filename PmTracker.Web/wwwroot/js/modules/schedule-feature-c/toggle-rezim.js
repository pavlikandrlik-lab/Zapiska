// Plán 4 Feature C Task 6 — UI glue pro switch SkutecnostRezim (Auto ⇄ Ručně).
//
// Delegace kliků na [data-feature-c-toggle] v celém dokumentu (schedule block
// se může re-renderovat partial HTML replace). POST /Harmonogram/ToggleRezim
// s antiforgery tokenem. Po úspěchu refresh badge + toggle label v DOM bez
// full page reload. Po selhání inline error zpráva s TraceId.

(function (global) {
    'use strict';

    const TOGGLE_SELECTOR = '[data-feature-c-toggle]';
    const CELL_SELECTOR = '[data-schedule-actual-cell]';
    const BADGE_SELECTOR = '[data-feature-c-badge]';

    const ZDROJ_META = {
        Neznamo:    { icon: '—',                 tooltip: 'Skutečnost nebyla vyplněna.' },
        Automat:    { icon: '🤖',      tooltip: 'Skutečnost vyplněná automatem ze ServiceDesk vyjádření.' },
        Manual:     { icon: '✍️',      tooltip: 'Skutečnost vyplněná ručně uživatelem.' },
        Historicka: { icon: '📜',      tooltip: 'Skutečnost migrovaná před zavedením auto-fill (historická data).' }
    };

    function getAntiforgery() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    async function postToggleRezim(hodnotaId, rezim) {
        const token = getAntiforgery();
        const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;
        const resp = await fetch('/Harmonogram/ToggleRezim', {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify({ HodnotaId: hodnotaId, Rezim: rezim })
        });
        if (!resp.ok) {
            const body = await resp.text().catch(() => '');
            throw new Error(`HTTP ${resp.status}: ${body || resp.statusText}`);
        }
        return await resp.json();
    }

    function refreshCellUi(cell, newRezim, newZdroj) {
        if (!cell) return;
        cell.setAttribute('data-skutecnost-rezim', newRezim);
        cell.setAttribute('data-skutecnost-zdroj', newZdroj);

        const toggle = cell.querySelector(TOGGLE_SELECTOR);
        if (toggle) {
            toggle.setAttribute('data-current-rezim', newRezim);
            const label = toggle.querySelector('.schedule-actual-cell__toggle-label');
            if (label) label.textContent = newRezim === 'Auto' ? 'Auto' : 'Ručně';
            const newAria = newRezim === 'Auto'
                ? 'Přepnout režim skutečnosti z Auto na Ručně'
                : 'Přepnout režim skutečnosti z Ručně na Auto';
            toggle.setAttribute('aria-label', newAria);
            toggle.setAttribute('title', newAria);
        }

        const badge = cell.querySelector(BADGE_SELECTOR);
        if (badge) {
            const meta = ZDROJ_META[newZdroj] || ZDROJ_META.Neznamo;
            badge.setAttribute('data-zdroj', newZdroj);
            badge.setAttribute('title', meta.tooltip);
            badge.setAttribute('aria-label', meta.tooltip);
            badge.textContent = meta.icon;
        }
    }

    function showToggleError(cell, message) {
        if (!cell) { alert(message); return; }
        let errBox = cell.querySelector('[data-feature-c-error]');
        if (!errBox) {
            errBox = document.createElement('div');
            errBox.setAttribute('data-feature-c-error', '');
            errBox.className = 'schedule-actual-cell__error';
            cell.appendChild(errBox);
        }
        errBox.textContent = message;
        setTimeout(() => { errBox?.remove(); }, 5000);
    }

    async function handleToggleClick(event) {
        const btn = event.target.closest(TOGGLE_SELECTOR);
        if (!btn) return;
        event.preventDefault();

        const hodnotaIdRaw = btn.getAttribute('data-hodnota-id');
        const currentRezim = btn.getAttribute('data-current-rezim') || 'Auto';
        const hodnotaId = parseInt(hodnotaIdRaw, 10);
        if (!Number.isFinite(hodnotaId) || hodnotaId <= 0) {
            showToggleError(btn.closest(CELL_SELECTOR), 'Chybí ID řádku skutečnosti.');
            return;
        }
        const newRezim = currentRezim === 'Auto' ? 'Manual' : 'Auto';
        const cell = btn.closest(CELL_SELECTOR);

        btn.disabled = true;
        const originalText = btn.querySelector('.schedule-actual-cell__toggle-label')?.textContent || '';
        try {
            const result = await postToggleRezim(hodnotaId, newRezim);
            if (result && result.changed === true) {
                refreshCellUi(cell, result.skutecnostRezim || newRezim, result.skutecnostZdroj || 'Neznamo');
            } else {
                // server vrátil changed=false (rezim se nezměnil) — UI zůstane
            }
        } catch (err) {
            showToggleError(cell, `Přepnutí selhalo: ${err.message}`);
        } finally {
            btn.disabled = false;
            const label = btn.querySelector('.schedule-actual-cell__toggle-label');
            if (label && !label.textContent) label.textContent = originalText;
        }
    }

    function init() {
        // Event delegation na document — schedule block se může partial-replace.
        document.addEventListener('click', handleToggleClick);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    global.pmScheduleFeatureC = { postToggleRezim, refreshCellUi };
})(window);
