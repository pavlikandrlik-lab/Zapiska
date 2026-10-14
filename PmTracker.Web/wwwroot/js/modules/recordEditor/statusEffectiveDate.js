/**
 * recordEditor/statusEffectiveDate.js
 *
 * Spec 2026-10-13 §6 — volba „Změna stavu platí k“ je vidět jen tehdy, když se vybraný
 * stav úkolu liší od uloženého. Volá se z initRecordFormEnhancements při každém vložení
 * editoru do stránky; ready-příznak na formuláři brání dvojímu navázání.
 */

export function syncStatusEffectiveDateRow(row, selectedStav) {
    row.hidden = selectedStav === (row.dataset.originalStav ?? "");
}

export function initStatusEffectiveDate(scope) {
    scope.querySelectorAll('form[data-record-editor-form="true"]').forEach((form) => {
        if (!(form instanceof HTMLFormElement) || form.dataset.statusEffectiveDateReady === "true") {
            return;
        }

        const row = form.querySelector("[data-status-effective-date-row]");
        const statusSelect = form.querySelector('select[name="Stav"]');
        if (!(row instanceof HTMLElement) || !(statusSelect instanceof HTMLSelectElement)) {
            return;
        }

        form.dataset.statusEffectiveDateReady = "true";
        const sync = () => syncStatusEffectiveDateRow(row, statusSelect.value);
        statusSelect.addEventListener("change", sync);
        sync();
    });
}
