// Phase 11 (DESIGN-9-A, 2026-05-01) — JS handler pro dropdown výběr preferred kandidáta v Auto kroku.
//
// Před fixem (phantom UI bug 3): tlačítka [data-feature-c-select-candidate]
// v _ScheduleBlockManualCell.cshtml existovala, ale žádný JS handler je neposlouchal —
// klik nedělal nic kromě otevření/zavření <details>. Server endpoint
// POST /Harmonogram/SelectCandidate fungoval, jen ho nikdo nevolal.
//
// Flow:
//   1. Klik na <button data-feature-c-select-candidate> uvnitř <details> dropdownu
//   2. POST /Harmonogram/SelectCandidate { HodnotaId, ExterniOdkazId, ZaznamId, KrokPoradi }
//   3. Po success refresh data-attributes buňky + close <details>
//
// Memory: project_manual_actual_kroky_phantom_ui (phantom UI bug 3 closed).

(function (global) {
    'use strict';

    const BUTTON_SELECTOR = '[data-feature-c-select-candidate]';
    const CELL_SELECTOR = '[data-schedule-actual-cell]';
    const DROPDOWN_SELECTOR = '[data-feature-c-dropdown]';

    function getAntiforgery() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : null;
    }

    async function postSelectCandidate(payload) {
        const token = getAntiforgery();
        const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;
        const resp = await fetch('/Harmonogram/SelectCandidate', {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify(payload)
        });
        if (!resp.ok) {
            const body = await resp.text().catch(() => '');
            throw new Error(`HTTP ${resp.status}: ${body || resp.statusText}`);
        }
        return await resp.json();
    }

    function refreshSelectionInUi(cell, selectedExterniOdkazId) {
        const items = cell.querySelectorAll('[data-feature-c-select-candidate]');
        items.forEach((btn) => {
            const id = parseInt(btn.getAttribute('data-externi-odkaz-id') || '0', 10);
            const isSelected = id === selectedExterniOdkazId;
            btn.setAttribute('data-is-selected', isSelected ? 'true' : 'false');
            btn.setAttribute('aria-current', isSelected ? 'true' : 'false');

            const li = btn.closest('li');
            if (li) li.classList.toggle('schedule-actual-cell__dropdown-item--selected', isSelected);

            const check = btn.querySelector('.schedule-actual-cell__dropdown-item-check');
            if (isSelected && !check) {
                const span = document.createElement('span');
                span.className = 'schedule-actual-cell__dropdown-item-check';
                span.setAttribute('aria-hidden', 'true');
                span.textContent = '✓';
                btn.appendChild(span);
            } else if (!isSelected && check) {
                check.remove();
            }
        });

        // Close <details> dropdown
        const details = cell.querySelector(DROPDOWN_SELECTOR);
        if (details && details.tagName === 'DETAILS') {
            details.removeAttribute('open');
        }
    }

    function showError(cell, message) {
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

    async function handleClick(event) {
        const btn = event.target?.closest(BUTTON_SELECTOR);
        if (!btn) return;
        event.preventDefault();

        const cell = btn.closest(CELL_SELECTOR);
        const hodnotaId = parseInt(btn.getAttribute('data-hodnota-id') || '0', 10);
        const externiOdkazId = parseInt(btn.getAttribute('data-externi-odkaz-id') || '0', 10);
        const zaznamId = parseInt(btn.getAttribute('data-zaznam-id') || '0', 10);
        const krokPoradi = parseInt(btn.getAttribute('data-krok-poradi') || '0', 10);

        if (!Number.isFinite(externiOdkazId) || externiOdkazId <= 0) {
            showError(cell, 'Chybí ID externího odkazu kandidáta.');
            return;
        }

        const payload = {
            HodnotaId: hodnotaId > 0 ? hodnotaId : 0,
            ExterniOdkazId: externiOdkazId,
            ZaznamId: zaznamId > 0 ? zaznamId : null,
            KrokPoradi: krokPoradi > 0 ? krokPoradi : null
        };

        btn.disabled = true;
        try {
            const result = await postSelectCandidate(payload);
            const newPreferred = (result && typeof result.preferredExterniOdkazId === 'number')
                ? result.preferredExterniOdkazId
                : externiOdkazId;
            refreshSelectionInUi(cell, newPreferred);
        } catch (err) {
            showError(cell, `Výběr kandidáta selhal: ${err.message}`);
        } finally {
            btn.disabled = false;
        }
    }

    function init() {
        document.addEventListener('click', handleClick);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    global.pmSelectCandidate = { postSelectCandidate };
})(window);
