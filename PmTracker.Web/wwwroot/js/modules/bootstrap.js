import { initCommentSortUi } from "./comments.js";
import {
    configureNavigationRuntime,
    handleNavigationCardClick,
    handleNavigationCardKeydown,
    initCiselnikAjaxSwitch,
    loadProjectTabPanel,
    loadRecordComments,
    initProfileRightsFilter,
    initProjectIndexStatusFilters,
    initProjectRecordPageshowSync,
    initProjectRecordsUi,
    initProjectTabs,
    initSettingsAjaxSwitch,
    initUserMenu,
    loadRecordDetail,
    toggleRecordCard,
    toggleMeetingAttendancePanel
} from "./navigation.js";
import { initMeetingOverview, toggleMeetingYearGroup } from "./meetingOverview.js";
import {
    clearProjectFilterInput,
    clearProjectFilterPreferenceStorage,
    handleProjectFilterInputChange as handleProjectFilterModuleInputChange,
    persistFilterState,
    saveProjectFilterDefaults,
    setFilterPanelOpen
} from "./filters.js";
import {
    configureModalRuntime,
    isModalOpen,
    openUrlModal,
    trapFocusInModal
} from "./modals.js";
import { initModalAjaxSubmit } from "./ajax.js";
import {
    clearStoredRecordEditorPreference,
    closeRecordEditorChooser,
    closeRecordEditorCloseGuard,
    initPermissionMetadataBindings,
    initRecordFormEnhancements,
    isRecordEditorFormDirty,
    openRecordEditor,
    prepareRecordEditorFormNavigation,
    promptRecordEditorDiscard,
    recordEditorState,
    refreshRecordEditorPreferenceUi,
    requestRecordEditorModalClose,
    requestRecordEditorPageCancel,
    restoreRecordEditorReturnStateFromUrl,
    updateTaskTypeVisibility
} from "./recordEditor.js";
import {
    applyProjectGanttFilters,
    applyProjectScheduleFilters,
    initProjectScheduleUi,
    initRecordSchedulePlanner,
    persistScheduleFilterState,
    queueRecordSchedulePlannerRecalc,
    renderStaticTimelineAxes,
    setScheduleFilterPanelOpen,
    toggleScheduleBreakdown
} from "./schedule.js";
import {
    clearStoredPrintFormat,
    closeAllFloatingPanels,
    closePrintChooser,
    handlePrintTriggerClick,
    initPrintFormatChooser,
    printState,
    queueFloatingPanelReposition,
    renderAllRainbowSegmentLabels
} from "./ui.js";
import { debounce, isButtonLike } from "./utils.js";
import { initSessionCoordinator } from "./session.js";
import { initTheme } from "./theme.js";
import { initTableTools } from "./tableTools.js";
import { handleDashboardClick, initDashboardShell } from "./dashboard.js";
import {
    handleProjectDashboardChange,
    handleProjectDashboardClick,
    initProjectDashboardShell
} from "./projectDashboard.js";

const projectIndexFilterOptions = {
    hideDoneStorageKey: "pmtracker.projects.hideDone",
    hideDeletedStorageKey: "pmtracker.projects.hideDeleted"
};

function initProjectIndexUi() {
    initProjectIndexStatusFilters(document, projectIndexFilterOptions);
}

function initPageSwitchers() {
    initProfileRightsFilter();
    initCiselnikAjaxSwitch({ initRecordFormEnhancements });
    initSettingsAjaxSwitch();
}

function handleProjectFilterInputChange(scope) {
    handleProjectFilterModuleInputChange(scope, {
        applyScope: (resolvedScope) => {
            if (resolvedScope === "schedule") {
                applyProjectScheduleFilters();
            }
            else if (resolvedScope === "gantt") {
                applyProjectGanttFilters();
            }
        }
    });
}

configureNavigationRuntime({
    initRecordFormEnhancements,
    prepareRecordEditorFormNavigation
});

configureModalRuntime({
    closeAllFloatingPanels,
    initRecordFormEnhancements,
    initPermissionMetadataBindings
});

function handleDocumentClick(event) {
    const target = event.target instanceof Element
        ? event.target
        : event.target instanceof Node
            ? event.target.parentElement
            : null;
    if (!(target instanceof Element)) {
        return;
    }

    const resetPrintPreference = target.closest("[data-print-preference-reset]");
    if (isButtonLike(resetPrintPreference)) {
        event.preventDefault();
        clearStoredPrintFormat();
        return;
    }

    const resetProjectFilterPreferences = target.closest("[data-project-filter-preferences-reset]");
    if (isButtonLike(resetProjectFilterPreferences)) {
        event.preventDefault();
        clearProjectFilterPreferenceStorage();
        const status = document.querySelector("[data-project-filter-preferences-status]");
        if (status instanceof HTMLElement) {
            status.textContent = "Uložené projektové filtry byly odstraněny.";
        }
        return;
    }

    const resetRecordEditorPreference = target.closest("[data-record-editor-preference-reset]");
    if (isButtonLike(resetRecordEditorPreference)) {
        event.preventDefault();
        clearStoredRecordEditorPreference();
        const status = document.querySelector("[data-record-editor-preference-status]");
        if (status instanceof HTMLElement) {
            status.textContent = "Uložená výchozí volba byla odstraněna.";
        }
        return;
    }

    const printTrigger = target.closest("[data-print-trigger]");
    if (printTrigger) {
        event.preventDefault();
        handlePrintTriggerClick(printTrigger);
        return;
    }

    // Comment edit toggle — nahrazuje původní <details>/<summary> mechanizmus
    // (který zavíral edit form do flex cellu s omezenou šířkou → Quill "jen v pravé polovině").
    const commentEditToggle = target.closest("[data-comment-edit-toggle]");
    if (isButtonLike(commentEditToggle)) {
        event.preventDefault();
        const comment = commentEditToggle.closest("[data-comment-item]");
        const form = comment?.querySelector("[data-comment-edit-form]");
        if (form instanceof HTMLFormElement) {
            form.hidden = false;
            if (comment instanceof HTMLElement) {
                comment.dataset.editing = "true";
            }
            form.querySelector("textarea")?.focus();
        }
        return;
    }

    const commentEditCancel = target.closest("[data-comment-edit-cancel]");
    if (isButtonLike(commentEditCancel)) {
        event.preventDefault();
        const comment = commentEditCancel.closest("[data-comment-item]");
        const form = comment?.querySelector("[data-comment-edit-form]");
        if (form instanceof HTMLFormElement) {
            form.hidden = true;
            if (comment instanceof HTMLElement) {
                comment.removeAttribute("data-editing");
            }
        }
        return;
    }

    const filterChipRemove = target.closest("[data-filter-chip-remove]");
    if (filterChipRemove instanceof HTMLButtonElement) {
        event.preventDefault();
        const scope = filterChipRemove.getAttribute("data-filter-chip-remove") || "";
        const inputKey = filterChipRemove.getAttribute("data-filter-chip-key") || "";
        if (scope && inputKey) {
            clearProjectFilterInput(scope, inputKey);
            handleProjectFilterInputChange(scope);
        }
        return;
    }

    const saveDefaultsButton = target.closest("[data-filter-save-defaults]");
    if (isButtonLike(saveDefaultsButton)) {
        event.preventDefault();
        const scope = saveDefaultsButton.getAttribute("data-filter-save-defaults") || "";
        if (scope) {
            saveProjectFilterDefaults(scope);
        }
        return;
    }

    const attendanceToggle = target.closest("[data-meeting-attendance-toggle]");
    if (attendanceToggle instanceof HTMLButtonElement) {
        event.preventDefault();
        toggleMeetingAttendancePanel(attendanceToggle);
        return;
    }

    const meetingYearToggle = target.closest("[data-meeting-year-toggle]");
    if (meetingYearToggle instanceof HTMLButtonElement) {
        event.preventDefault();
        toggleMeetingYearGroup(meetingYearToggle);
        return;
    }

    const scheduleExpandToggle = target.closest("[data-schedule-expand-toggle]");
    if (isButtonLike(scheduleExpandToggle)) {
        event.preventDefault();
        toggleScheduleBreakdown(scheduleExpandToggle);
        return;
    }

    if (printState.popover instanceof HTMLElement
        && !target.closest("[data-print-popover]")
        && !target.closest("[data-print-trigger]")) {
        closePrintChooser({ restoreFocus: false });
    }

    if (recordEditorState.chooser instanceof HTMLElement
        && !target.closest("[data-record-editor-popover]")
        && !target.closest("[data-record-editor-url]")) {
        closeRecordEditorChooser({ restoreFocus: false });
    }

    const recordEditorCancel = target.closest("[data-record-editor-cancel]");
    if (recordEditorCancel) {
        event.preventDefault();
        void requestRecordEditorPageCancel(recordEditorCancel instanceof HTMLElement ? recordEditorCancel : null);
        return;
    }

    const recordEditorTrigger = target.closest("[data-record-editor-url]");
    if (recordEditorTrigger) {
        event.preventDefault();
        openRecordEditor(recordEditorTrigger instanceof HTMLElement ? recordEditorTrigger : null);
        return;
    }

    const openUrl = target.closest("[data-modal-url]");
    if (openUrl) {
        event.preventDefault();
        openUrlModal(openUrl.getAttribute("data-modal-url"), openUrl);
        return;
    }

    if (target.matches("[data-modal-close]") || target.closest("[data-modal-close]")) {
        event.preventDefault();
        const closeTarget = target.closest("[data-modal-close]");
        void requestRecordEditorModalClose(closeTarget instanceof HTMLElement ? closeTarget : null);
        return;
    }

    // Fáze 2E: backdrop click — target je gov-dialog přímo (ne vnitřní element).
    if (target instanceof HTMLElement && target.tagName === "GOV-DIALOG" && target.hasAttribute("data-modal-container")) {
        event.preventDefault();
        void requestRecordEditorModalClose(target);
        return;
    }

    const filterToggle = target.closest("[data-filter-toggle]");
    if (filterToggle) {
        const filterPanel = document.querySelector("[data-filter-panel]");
        if (filterPanel) {
            const isCollapsed = filterPanel.classList.contains("collapsed");
            setFilterPanelOpen(isCollapsed);
        }
        return;
    }

    const scheduleFilterToggle = target.closest("[data-schedule-filter-toggle]");
    if (scheduleFilterToggle) {
        const filterPanel = document.querySelector("[data-schedule-filter-panel]");
        if (filterPanel instanceof HTMLElement) {
            const isCollapsed = filterPanel.classList.contains("collapsed");
            setScheduleFilterPanelOpen(isCollapsed);
        }
        return;
    }

    const projectTabRetry = target.closest("[data-project-tab-retry]");
    if (projectTabRetry instanceof HTMLButtonElement) {
        event.preventDefault();
        const panel = projectTabRetry.closest("[data-tab-panel]");
        if (panel instanceof HTMLElement) {
            void loadProjectTabPanel(panel, { force: true });
        }
        return;
    }

    if (handleDashboardClick(target)) {
        event.preventDefault();
        return;
    }

    if (handleProjectDashboardClick(target)) {
        event.preventDefault();
        return;
    }

    const recordCommentsRetry = target.closest("[data-record-comments-retry]");
    if (recordCommentsRetry instanceof HTMLButtonElement) {
        event.preventDefault();
        const card = recordCommentsRetry.closest(".record-card");
        if (card instanceof HTMLElement) {
            void loadRecordComments(card, { force: true });
        }
        return;
    }

    const recordDetailRetry = target.closest("[data-record-detail-retry]");
    if (recordDetailRetry instanceof HTMLButtonElement) {
        event.preventDefault();
        const card = recordDetailRetry.closest(".record-card");
        if (card instanceof HTMLElement) {
            void loadRecordDetail(card, { force: true });
        }
        return;
    }

    const recordToggle = target.closest("[data-record-toggle]");
    if (recordToggle) {
        if (target.closest("[data-stop-propagation]")) {
            return;
        }
        const card = recordToggle.closest(".record-card");
        if (card) {
            void toggleRecordCard(card);
        }
        return;
    }

    if (target.closest("[data-stop-propagation]")) {
        return;
    }

    // Outbound link gate (2026-04-19 noc 2): když je otevřený page-level
    // record editor s nezavřenými změnami, jakýkoliv <a href> (menu, breadcrumbs,
    // logo) musí projít přes app-level confirm dialog, ne mlčky navigovat.
    // User hlásil: "dialog přeskočí, musíš upravit vnitřní logiku aby se
    // počkalo na volbu v dialogu a potom se teprve odešlo nebo zůstalo".
    // Předchozí nativní beforeunload dialog byl záměrně odstraněn; tohle ho
    // nahrazuje app-level ekvivalentem.
    if (maybeGuardOutboundNavigation(target, event)) {
        return;
    }

    if (handleNavigationCardClick(target)) {
        event.preventDefault();
    }
}

function maybeGuardOutboundNavigation(target, event) {
    const anchor = target.closest("a[href]");
    if (!(anchor instanceof HTMLElement)) {
        return false;
    }

    const href = anchor.getAttribute("href") || "";
    if (!href || href.startsWith("#") || href.startsWith("javascript:") || href.startsWith("mailto:") || href.startsWith("tel:")) {
        return false;
    }

    if (anchor instanceof HTMLAnchorElement && (anchor.hasAttribute("download") || (anchor.target && anchor.target !== "" && anchor.target !== "_self"))) {
        return false;
    }

    // Pokud je pod anchor jeden z našich data-* hooks, příslušný handler
    // už výše event.preventDefault + vlastní dialog řešil a return.
    // Do tohoto bodu se ti tedy dostanou jen "obyčejné" odkazy.

    const pageEditorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
    if (!(pageEditorForm instanceof HTMLFormElement)) {
        return false;
    }

    if (pageEditorForm.dataset.recordEditorNavigating === "true") {
        return false;
    }

    if (!isRecordEditorFormDirty(pageEditorForm)) {
        return false;
    }

    event.preventDefault();
    const targetHref = anchor instanceof HTMLAnchorElement ? anchor.href : href;
    (async () => {
        // promptRecordEditorDiscard interně volá prepareRecordEditorFormNavigation
        // při zvolení "zahodit změny" — nastaví recordEditorNavigating=true,
        // takže window.location.assign už bez dalšího dialogu projde.
        const canLeave = await promptRecordEditorDiscard(pageEditorForm, anchor);
        if (canLeave) {
            window.location.assign(targetHref);
        }
    })();
    return true;
}

function handleDocumentOverlayKeydown(event) {
    if (event.key === "Escape" && recordEditorState.closeGuard instanceof HTMLElement) {
        event.preventDefault();
        closeRecordEditorCloseGuard({ restoreFocus: true });
        return;
    }

    if (event.key === "Escape" && printState.popover instanceof HTMLElement && !isModalOpen()) {
        event.preventDefault();
        closePrintChooser({ restoreFocus: true });
        return;
    }

    if (event.key === "Escape" && recordEditorState.chooser instanceof HTMLElement && !isModalOpen()) {
        event.preventDefault();
        closeRecordEditorChooser({ restoreFocus: true });
        return;
    }

    if (!isModalOpen()) {
        return;
    }

    if (event.key === "Escape") {
        event.preventDefault();
        void requestRecordEditorModalClose(document.activeElement instanceof HTMLElement ? document.activeElement : null);
        return;
    }

    if (event.key === "Tab") {
        trapFocusInModal(event);
    }
}

function handleDocumentChange(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const filterInput = target.closest("[data-filter-key]");
    if (filterInput instanceof HTMLInputElement || filterInput instanceof HTMLSelectElement) {
        persistFilterState(filterInput);
    }

    const scheduleFilterInput = target.closest("[data-schedule-filter-key]");
    if (scheduleFilterInput instanceof HTMLInputElement || scheduleFilterInput instanceof HTMLSelectElement) {
        persistScheduleFilterState(scheduleFilterInput);
    }

    const categorySelect = target.closest("[data-kategorie-select]");
    if (categorySelect instanceof HTMLSelectElement) {
        updateTaskTypeVisibility(categorySelect);
        const form = categorySelect.closest("form");
        if (form instanceof HTMLFormElement) {
            initRecordSchedulePlanner(form);
        }
    }

    if (handleProjectDashboardChange(target)) {
        return;
    }
}

function handleDocumentInput(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    const filterInput = target.closest("[data-filter-key]");
    if (filterInput instanceof HTMLInputElement || filterInput instanceof HTMLSelectElement) {
        if (filterInput instanceof HTMLInputElement && filterInput.type === "checkbox") {
            return;
        }
        persistFilterState(filterInput);
    }

    const scheduleFilterInput = target.closest("[data-schedule-filter-key]");
    if (scheduleFilterInput instanceof HTMLInputElement || scheduleFilterInput instanceof HTMLSelectElement) {
        if (scheduleFilterInput instanceof HTMLInputElement && scheduleFilterInput.type === "checkbox") {
            return;
        }
        persistScheduleFilterState(scheduleFilterInput);
    }
}

function handleDocumentCardKeydown(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    handleNavigationCardKeydown(event, target);
}

// Dřívější implementace triggrovala nativní browser "Opravdu odejít?" dialog
// (Edge/Chrome). User 2026-04-19 noc: "vyskočí windows edge dialogové okno,
// než ze stránky odejdu tak problikne i dialog aplikace, mě se více líbí
// dialog aplikace, ne windows edge okno, je to chybné chování".
// Řešení: no-op. App-level dialog přes promptRecordEditorDiscard se volá
// při in-app navigaci (Cancel, Back, close modal). Tab close / URL change
// beze dvojitého dialogu — tradeoff, který user explicitně akceptuje.
function handleWindowBeforeUnload(_event) {
    // záměrně prázdné — žádný nativní confirm dialog
}

function handleGovCloseEvent(event) {
    // gov-dialog emituje gov-close při kliknutí na vestavěný X button.
    // block-close="true" + block-backdrop-close="true" na dialogu zabraňují
    // self-close; event je čistě "žádost o zavření" kterou musí schválit
    // náš dirty-check flow (promptRecordEditorDiscard).
    const target = event.target;
    if (target instanceof HTMLElement && target.tagName === "GOV-DIALOG" && target.hasAttribute("data-modal-container")) {
        event.preventDefault();
        event.stopPropagation();
        void requestRecordEditorModalClose(target);
    }
}

const rerenderRainbowLabelsOnResize = debounce(() => {
    renderAllRainbowSegmentLabels(document);
}, 120);

const rerenderTimelineAxesOnResize = debounce(() => {
    renderStaticTimelineAxes(document.querySelector(".tab-panel.active"));
    document.querySelectorAll('form[data-record-schedule-form="true"]').forEach((form) => {
        if (form instanceof HTMLFormElement) {
            queueRecordSchedulePlannerRecalc(form, 0);
        }
    });
}, 140);

const reflowMeetingOverviewsOnResize = debounce(() => {
    initMeetingOverview(document);
}, 120);

function normalizeEventBindings(bindings) {
    return Array.isArray(bindings) ? bindings : [];
}

function bindEventGroup(target, bindings) {
    normalizeEventBindings(bindings).forEach((binding) => {
        if (!binding || typeof binding.type !== "string" || typeof binding.handler !== "function") {
            return;
        }

        target.addEventListener(binding.type, binding.handler, binding.options);
    });
}

function runInitializers(initializers) {
    normalizeEventBindings(initializers).forEach((initializer) => {
        if (typeof initializer === "function") {
            initializer();
        }
    });
}

export function bootstrapPmTrackerApp() {
    bindEventGroup(document, [
        { type: "click", handler: handleDocumentClick },
        { type: "keydown", handler: handleDocumentOverlayKeydown },
        { type: "change", handler: handleDocumentChange },
        { type: "input", handler: handleDocumentInput },
        { type: "keydown", handler: handleDocumentCardKeydown },
        { type: "gov-close", handler: handleGovCloseEvent }
    ]);

    bindEventGroup(window, [
        { type: "beforeunload", handler: handleWindowBeforeUnload },
        { type: "scroll", handler: queueFloatingPanelReposition, options: true },
        { type: "resize", handler: queueFloatingPanelReposition },
        { type: "resize", handler: rerenderRainbowLabelsOnResize },
        { type: "resize", handler: rerenderTimelineAxesOnResize },
        { type: "resize", handler: reflowMeetingOverviewsOnResize }
    ]);

    runInitializers([
        () => initProjectTabs(),
        () => initProjectRecordsUi({ preserveServerView: true }),
        () => initProjectScheduleUi(),
        () => initProjectRecordPageshowSync(),
        () => initCommentSortUi(document),
        () => restoreRecordEditorReturnStateFromUrl(),
        () => initTheme(),
        () => initUserMenu(),
        () => initPrintFormatChooser(),
        () => refreshRecordEditorPreferenceUi(),
        () => initPageSwitchers(),
        () => initRecordFormEnhancements(document),
        () => initPermissionMetadataBindings(document),
        () => initTableTools(document),
        () => initMeetingOverview(document),
        () => initProjectIndexUi(),
        () => initDashboardShell(),
        () => initProjectDashboardShell(),
        () => initSessionCoordinator(),
        () => initModalAjaxSubmit()
    ]);
}
