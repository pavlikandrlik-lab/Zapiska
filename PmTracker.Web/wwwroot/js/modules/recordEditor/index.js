/**
 * recordEditor/index.js
 *
 * Entry point + re-export barrel pro feature modul recordEditor.
 * Orchestruje init napříč všemi submoduly + pickery.
 *
 * Fáze 3B Task 1: původní 1919 LOC modul rozdělen do recordEditor/
 * folderu (navigation, form, richtext, draft, index).
 */

// Re-export public API ze všech submodulů pro backward-compat
export * from "./form.js";
export * from "./richtext.js";
export * from "./draft.js";

import {
    initCustomDatePickers,
    initCustomTimePickers,
    initSinglePersonPickers,
    initAdPersonPickers,
    initCollabPickers
} from "../pickers.js";
import {
    initRecordOwnerAutofill,
    initExternalLinksEditors,
    initMeetingNumberValidation,
    initRecordFormTabs,
    initRecordGoalAutoGrow,
    initRecordMeetingDateSync,
    updateTaskTypeVisibility
} from "./form.js";
import { initRichTextEditors } from "./richtext.js";
import { initRecordEditorDirtyTracking } from "./draft.js";
import { initRecordSchedulePlanner } from "../schedule.js";
import { initConfirmSubmitToggles } from "../ajax.js";

export function initRecordFormEnhancements(scope) {
    if (!(scope instanceof HTMLElement || scope instanceof Document)) {
        return;
    }

    initCustomDatePickers(scope);
    initCustomTimePickers(scope);

    scope.querySelectorAll("[data-kategorie-select]").forEach((element) => {
        if (element instanceof HTMLSelectElement) {
            updateTaskTypeVisibility(element);
        }
    });

    initSinglePersonPickers(scope);
    initRecordOwnerAutofill(scope);
    initAdPersonPickers(scope);
    initExternalLinksEditors(scope);
    initCollabPickers(scope);
    initMeetingNumberValidation(scope);
    initConfirmSubmitToggles(scope);
    initRecordFormTabs(scope);
    initRecordGoalAutoGrow(scope);
    initRecordMeetingDateSync(scope);
    initRichTextEditors(scope);
    initRecordSchedulePlanner(scope);
    initRecordEditorDirtyTracking(scope);
}
