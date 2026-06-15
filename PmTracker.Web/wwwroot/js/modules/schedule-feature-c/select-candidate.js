// Phase 11 (DESIGN-9-A, 2026-05-01) — JS handler pro dropdown výběr preferred kandidáta v Auto kroku.
//
// Před fixem (phantom UI bug 3): tlačítka [data-feature-c-select-candidate]
// v _ScheduleBlockManualCell.cshtml existovala, ale žádný JS handler je neposlouchal —
// klik nedělal nic kromě otevření/zavření <details>. Server endpoint
// POST /Harmonogram/SelectCandidate fungoval, jen ho nikdo nevolal.
//
// Flow:
//   1. Klik na <button data-feature-c-select-candidate> uvnitř <details> dropdownu
//   2. POST /Harmonogram/SelectCandidate { ZaznamId, Poradi, ExterniOdkazId }
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

    async function postSelectCandidate(payload, signal) {
        const token = getAntiforgery();
        const headers = { 'Content-Type': 'application/json', 'Accept': 'application/json' };
        if (token) headers['RequestVerificationToken'] = token;
        const resp = await fetch('/Harmonogram/SelectCandidate', {
            method: 'POST',
            headers: headers,
            credentials: 'same-origin',
            body: JSON.stringify(payload),
            signal: signal
        });
        if (!resp.ok) {
            const body = await resp.text().catch(() => '');
            throw new Error(`HTTP ${resp.status}: ${body || resp.statusText}`);
        }
        return await resp.json();
    }

    // FIX 2026-05-01 (round 3 #21): race condition guard — multiple rapid clicks na různé
    // kandidáty by mohly aplikovat odpovědi out-of-order. Per cell tracker abort předchozí
    // request při novém kliku.
    const pendingByCell = new WeakMap(); // cell DOM node → AbortController

    function refreshSelectionInUi(cell, selectedExterniOdkazId) {
        let selectedButton = null;
        const items = cell.querySelectorAll('[data-feature-c-select-candidate]');
        items.forEach((btn) => {
            const id = parseInt(btn.getAttribute('data-externi-odkaz-id') || '0', 10);
            const isSelected = id === selectedExterniOdkazId;
            if (isSelected) selectedButton = btn;
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

        // FIX 2026-05-04: aktualizuj viditelný datum span. Před fixem `refreshSelectionInUi`
        // updatovala jen data-is-selected/aria-current/checkmark, ale nezměnila viditelný
        // <span class="schedule-actual-datum"> v cell.main. User klikl jiný kandidát ale
        // pole zobrazovalo stále původní datum (bez page reload). Datum vytáhneme z
        // dropdown item buttonu (.schedule-actual-cell__dropdown-item-datum) — render
        // už ho tam má v Czech formátu dd.MM.yyyy.
        if (selectedButton) {
            const datumNode = selectedButton.querySelector('.schedule-actual-cell__dropdown-item-datum');
            const newDatumText = datumNode ? datumNode.textContent.trim() : '';
            if (newDatumText) {
                const visibleDatumSpan = cell.querySelector(
                    '[data-schedule-actual-source="vyjadreni"] .schedule-actual-datum[data-manual-krok-readonly]'
                );
                if (visibleDatumSpan) {
                    visibleDatumSpan.textContent = newDatumText;
                }
                // Update tooltip na pm-button (ikonka chatu) tak, aby odpovídal novému datu.
                const chatButton = cell.querySelector(
                    '[data-schedule-actual-source="vyjadreni"] [data-schedule-actual-from-vyjadreni]'
                );
                if (chatButton) {
                    const tooltip = `Z vyjádření ${newDatumText}`;
                    chatButton.setAttribute('title', tooltip);
                    chatButton.setAttribute('aria-label', tooltip);
                }

                // FIX 2026-05-05: pokud je tato cell krok 10 (poslední krok = "Skutečné dokončení"
                // celého záznamu), aktualizuj globální souhrn pole `[data-schedule-summary-shifted]`.
                // Server-side render je ze ScheduleTimelineCalculator.Summarize → Steps[^1].ActualEndDate
                // (= krok 10), tedy bez page reload by user viděl stale hodnotu po chevron změně.
                // User explicit požadavek: "vždy tahané z hodnoty kroku 10 skutečnosti".
                const krokPoradi = parseInt(cell.getAttribute('data-krok-poradi') || '0', 10);
                if (krokPoradi === 10) {
                    const block = cell.closest('[data-schedule-block]')
                        || cell.closest('form')
                        || document;
                    const summarySpan = block.querySelector('[data-schedule-summary-shifted]');
                    if (summarySpan instanceof HTMLElement) {
                        summarySpan.textContent = newDatumText;
                    }
                }
            }
        }

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
        const externiOdkazId = parseInt(btn.getAttribute('data-externi-odkaz-id') || '0', 10);
        const zaznamId = parseInt(btn.getAttribute('data-zaznam-id') || '0', 10);
        const krokPoradi = parseInt(btn.getAttribute('data-krok-poradi') || '0', 10);

        if (!Number.isFinite(externiOdkazId) || externiOdkazId <= 0) {
            showError(cell, 'Chybí ID externího odkazu kandidáta.');
            return;
        }
        if (!Number.isFinite(zaznamId) || zaznamId <= 0 || !Number.isFinite(krokPoradi) || krokPoradi < 1 || krokPoradi > 10) {
            showError(cell, 'Chybí ZaznamId nebo Poradi kroku.');
            return;
        }

        // Datum-model: server SelectCandidateRequest = (ZaznamId, Poradi, ExterniOdkazId).
        // Krok řádek si server vytvoří create-if-missing (EnsureKrokRowAsync).
        const payload = {
            ZaznamId: zaznamId,
            Poradi: krokPoradi,
            ExterniOdkazId: externiOdkazId
        };

        // FIX 2026-05-01 (round 3 #21): abort předchozí pending request pro tutéž cell.
        const previousController = pendingByCell.get(cell);
        if (previousController) {
            try { previousController.abort(); } catch { /* ignore */ }
        }
        const controller = new AbortController();
        pendingByCell.set(cell, controller);

        btn.disabled = true;
        try {
            const result = await postSelectCandidate(payload, controller.signal);
            // Pokud byl tento request abortován novějším klikem, výsledek ignoruj.
            if (pendingByCell.get(cell) !== controller) return;

            const newPreferred = (result && typeof result.preferredExterniOdkazId === 'number')
                ? result.preferredExterniOdkazId
                : externiOdkazId;
            refreshSelectionInUi(cell, newPreferred);
            if (result && result.syncFailed) {
                showError(cell,
                    `Kandidát uložen, ale auto-sync selhal: ${result.syncFailReason || 'neznámá chyba'}`);
            }
        } catch (err) {
            // AbortError se může objevit při novém kliku — silently ignore.
            if (err?.name === 'AbortError') return;
            showError(cell, `Výběr kandidáta selhal: ${err.message}`);
        } finally {
            if (pendingByCell.get(cell) === controller) {
                pendingByCell.delete(cell);
            }
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
