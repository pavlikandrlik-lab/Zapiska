// Side-effect imports: tyto moduly registrují globální listenery a/nebo
// window.pm* objekty. Při migraci ze site.bundle.js (legacy IIFE bundle) na
// modular site.js se import zapomněl — moduly se never načítaly a celé feature
// (chat modal, externí odkaz sync, manual kroky validace, schedule Auto/Ručně toggle,
// vyzvy panel) tiše nefungovaly.
//
// 2026-04-28 redesign: pm-chat-stepper custom element odstraněn, nahrazen native
// gov-stepper. Stepper drag&drop teď přes chatModalDragDrop.js (importuje
// stepperSticky.js / stepperDragSnap.js / stepperBuffer.js side-effect).
//
// eventBus.js MUSÍ být první: registruje gov-click → click adapter. Bez něj
// všechna <gov-button> tlačítka zůstávají hluchá (gov-design-system 4.x emituje
// 'gov-click' a stopuje nativní click).
import "../components/pmTabs.js";
import "./eventBus.js";
import "./externiOdkaz/sync.js";
import "./vyjadreni/chatModal.js";
import "./vyjadreni/chatModalDragDrop.js";
import "./vyjadreni/chatModalReharvest.js";
import "./harmonogram/manualKroky.js";
import "./schedule-feature-c/toggle-rezim.js";
import "./schedule-feature-c/select-candidate.js"; // Phase 11 (DESIGN-9-A) — phantom UI bug 3 fix
import "./schedule-feature-c/preview-sync.js";    // Phase 12 (DESIGN-9-C) — pre-fetch staging
import "./vyzvy/index.js";
import "./vyzvy/panelController.js";
import "./vyzvy/switchController.js";

import { initCommentSortUi } from "./comments.js";
import {
    configureNavigationRuntime,
    handleNavigationCardClick,
    handleNavigationCardKeydown,
    applyProjectIndexFilters,
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
import { initMeetingOverview, toggleMeetingYearGroup, toggleProjectHistory } from "./meetingOverview.js";
import {
    clearProjectFilterInput,
    clearProjectFilterPreferenceStorage,
    handleProjectFilterInputChange as handleProjectFilterModuleInputChange,
    initProjectFilterTabSync,
    persistFilterState,
    saveProjectFilterDefaults,
    setFilterPanelOpen
} from "./filters.js";


import {
    closeModal,
    configureModalRuntime,
    isModalOpen,
    openUrlModal,
    trapFocusInModal
} from "./modals.js";
import { initModalAjaxSubmit } from "./ajax.js";
import {
    closeRecordEditorCloseGuard,
    initPermissionMetadataBindings,
    initRecordFormEnhancements,
    isRecordEditorFormDirty,
    prepareRecordEditorFormNavigation,
    promptRecordEditorDiscard,
    recordEditorState,
    requestRecordEditorPageCancel,
    updateTaskTypeVisibility
} from "./recordEditor.js";
import {
    applyProjectGanttFilters,
    applyProjectScheduleFilters,
    initProjectScheduleUi,
    initRecordSchedulePlanner,
    queueRecordSchedulePlannerRecalc,
    renderStaticTimelineAxes,
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
    prepareRecordEditorFormNavigation,
    refreshProjectIndexFilters: () => applyProjectIndexFilters(document, projectIndexFilterOptions)
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

    // Inbox #16 — super-admin reindex vyhledávání (Profil/Index karta).
    const reindexTrigger = target.closest("[data-search-reindex-trigger]");
    if (isButtonLike(reindexTrigger)) {
        event.preventDefault();
        handleSearchReindexClick(reindexTrigger);
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
    if (isButtonLike(attendanceToggle)) {
        event.preventDefault();
        toggleMeetingAttendancePanel(attendanceToggle);
        return;
    }

    const projectHistoryToggle = target.closest("[data-project-history-toggle]");
    if (projectHistoryToggle instanceof HTMLElement) {
        event.preventDefault();
        toggleProjectHistory(projectHistoryToggle);
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

    const recordEditorCancel = target.closest("[data-record-editor-cancel]");
    if (recordEditorCancel) {
        event.preventDefault();
        void requestRecordEditorPageCancel(recordEditorCancel instanceof HTMLElement ? recordEditorCancel : null);
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
        closeModal();
        return;
    }

    // Fáze 2E: backdrop click — target je gov-dialog přímo (ne vnitřní element).
    if (target instanceof HTMLElement && target.tagName === "GOV-DIALOG" && target.hasAttribute("data-modal-container")) {
        event.preventDefault();
        closeModal();
        return;
    }

    // Sjednocený filter toggle handler 2026-04-30: scope se detekuje z DOM (closest shell),
    // panel se hledá uvnitř shell aby v případě dvou mountovaných shellů (records + schedule)
    // nedošlo k záměně. Spec project-filter-unification-design.
    const filterToggle = target.closest("[data-filter-toggle]");
    if (filterToggle) {
        const shell = filterToggle.closest("[data-project-filter-scope]");
        const scope = shell?.getAttribute("data-project-filter-scope");
        const filterPanel = shell?.querySelector("[data-filter-panel]");
        if (scope && filterPanel instanceof HTMLElement) {
            const isCollapsed = filterPanel.classList.contains("collapsed");
            setFilterPanelOpen(scope, isCollapsed);
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

async function handleSearchReindexClick(trigger) {
    const card = trigger.closest("[data-search-admin-card]");
    if (!(card instanceof HTMLElement)) {
        return;
    }
    const reindexUrl = card.dataset.reindexUrl;
    if (!reindexUrl) {
        return;
    }
    const status = card.querySelector("[data-search-reindex-status]");
    const token = card.querySelector('input[name="__RequestVerificationToken"]');
    if (!(token instanceof HTMLInputElement)) {
        if (status) status.textContent = "Chybí anti-forgery token.";
        return;
    }

    if (status) status.textContent = "Reindexuji…";
    trigger.setAttribute("disabled", "disabled");
    try {
        const response = await fetch(reindexUrl, {
            method: "POST",
            headers: { "RequestVerificationToken": token.value, "Accept": "application/json" },
            credentials: "same-origin"
        });
        if (!response.ok) {
            const text = await response.text();
            if (status) status.textContent = `Reindex selhal: HTTP ${response.status} ${text.slice(0, 120)}`;
            return;
        }
        const payload = await response.json().catch(() => ({}));
        if (status) status.textContent = `Reindex dokončen. Indexováno dokumentů: ${payload.indexed ?? "?"}`;
        // Refresh status panel
        loadSearchAdminStatus(card).catch(() => {});
    } catch (err) {
        if (status) status.textContent = `Reindex selhal: ${err && err.message ? err.message : err}`;
    } finally {
        trigger.removeAttribute("disabled");
    }
}

async function loadSearchAdminStatus(card) {
    const statusUrl = card.dataset.statusUrl;
    if (!statusUrl) return;
    try {
        const response = await fetch(statusUrl, {
            method: "GET",
            headers: { "Accept": "application/json" },
            credentials: "same-origin"
        });
        if (!response.ok) {
            return;
        }
        const data = await response.json();
        const provider = card.querySelector("[data-search-provider]");
        const enabled = card.querySelector("[data-search-enabled]");
        const fts = card.querySelector("[data-search-fts]");
        const count = card.querySelector("[data-search-count]");
        if (provider) provider.textContent = data.provider ?? "–";
        if (enabled) enabled.textContent = data.enabled ? "ano" : "ne";
        if (fts) fts.textContent = data.isSearchable ? "připraven" : "NENÍ nakonfigurovaný";
        if (count) count.textContent = typeof data.documentCount === "number" ? data.documentCount.toLocaleString("cs-CZ") : "–";
    } catch {
        // silent — status panel zůstane s pomlčkami
    }
}

function initSearchAdminCard() {
    document.querySelectorAll("[data-search-admin-card]").forEach(card => {
        if (card instanceof HTMLElement) {
            loadSearchAdminStatus(card).catch(() => {});
        }
    });
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

    if (!isModalOpen()) {
        return;
    }

    if (event.key === "Escape") {
        event.preventDefault();
        closeModal();
        return;
    }

    if (event.key === "Tab") {
        trapFocusInModal(event);
    }
}

function isGovFormSwitchEl(el) {
    return el instanceof HTMLElement && typeof el.tagName === "string" && el.tagName.toLowerCase() === "gov-form-switch";
}

function syncProjectPouzivatIdentJednaniHidden(target) {
    const sw = target.closest("gov-form-switch[data-project-pouzivat-ident-jednani]");
    if (!isGovFormSwitchEl(sw)) return;
    const wrap = sw.closest(".project-form-switch-wrap");
    if (!(wrap instanceof HTMLElement)) return;
    const hidden = wrap.querySelector('[data-project-pouzivat-ident-jednani-state]');
    if (hidden instanceof HTMLInputElement) {
        hidden.value = sw.checked ? "true" : "false";
    }
}

function syncSyncCardSwitchHidden(target) {
    const sw = target.closest("gov-form-switch[data-sync-card-switch]");
    if (!isGovFormSwitchEl(sw)) return;
    const row = sw.closest(".sync-job-card__row--switch");
    if (!(row instanceof HTMLElement)) return;
    const hidden = row.querySelector('[data-sync-card-switch-state]');
    if (hidden instanceof HTMLInputElement) {
        hidden.value = sw.checked ? "true" : "false";
    }
}

// (legacy handleSyncTabClick removed — sync settings nyní používá pm-tabs Web Component)

function handleDocumentChange(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    syncProjectPouzivatIdentJednaniHidden(target);
    syncSyncCardSwitchHidden(target);

    // Sjednocený filter input handler 2026-04-30: persistFilterState detekuje scope
    // z DOM (closest shell) — funguje pro records i schedule shell. Spec
    // project-filter-unification-design.
    const filterInput = target.closest("[data-filter-key]");
    if (filterInput instanceof HTMLInputElement || filterInput instanceof HTMLSelectElement || isGovFormSwitchEl(filterInput)) {
        persistFilterState(filterInput);
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

    // Sjednocený filter input change handler — sdílí persistFilterState s gov-change handler
    // (handleDocumentChange). Spec 2026-04-30 project-filter-unification-design.
    const filterInput = target.closest("[data-filter-key]");
    if (filterInput instanceof HTMLInputElement || filterInput instanceof HTMLSelectElement) {
        if (filterInput instanceof HTMLInputElement && filterInput.type === "checkbox") {
            return;
        }
        persistFilterState(filterInput);
    }
}

function handleDocumentCardKeydown(event) {
    const target = event.target;
    if (!(target instanceof Element)) {
        return;
    }

    handleNavigationCardKeydown(event, target);
}

function handleProjectHistoryKeydown(event) {
    if (event.key !== "Enter" && event.key !== " ") {
        return;
    }
    const target = event.target;
    if (!(target instanceof HTMLElement)) {
        return;
    }
    const projectHistoryToggle = target.closest("[data-project-history-toggle]");
    if (projectHistoryToggle instanceof HTMLElement && projectHistoryToggle === target) {
        event.preventDefault();
        toggleProjectHistory(projectHistoryToggle);
    }
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
    // náš flow (dirty-check nebo přímé zavření).
    const dialog = event.target;
    if (!(dialog instanceof HTMLElement) || dialog.tagName !== "GOV-DIALOG") {
        return;
    }

    // Fallback pro všechny non-record-editor modaly (Přidat ručně, AD search,
    // Přidat projektovou roli, atd.) — gov-close je fire-and-close,
    // žádný dirty-check není potřeba.
    event.preventDefault();
    closeModal();
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
        { type: "gov-change", handler: handleDocumentChange },
        { type: "input", handler: handleDocumentInput },
        { type: "keydown", handler: handleDocumentCardKeydown },
        { type: "keydown", handler: handleProjectHistoryKeydown },
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
        () => initProjectFilterTabSync(),
        () => initProjectRecordPageshowSync(),
        () => initCommentSortUi(document),
        () => initTheme(),
        () => initUserMenu(),
        () => initPrintFormatChooser(),
        () => initPageSwitchers(),
        () => initRecordFormEnhancements(document),
        () => initPermissionMetadataBindings(document),
        () => initTableTools(document),
        () => initMeetingOverview(document),
        () => initProjectIndexUi(),
        () => initDashboardShell(),
        () => initProjectDashboardShell(),
        () => initSessionCoordinator(),
        () => initModalAjaxSubmit(),
        () => initSearchAdminCard(),
        // Legacy IIFE moduly registrují window.pm* + nabízejí init() volaný
        // po DOMContentLoaded. Při importu side-effects už registrují globaly,
        // ale init() volání jsou explicitní (legacy bundle pattern). Voláme
        // optional chaining — modul se může v testovém prostředí nenahrát.
        () => window.pmExterniOdkazSync?.init?.(),
        () => window.pmChatModal?.init?.(),
        () => window.pmManualKroky?.init?.(),
        () => window.pmScheduleFeatureC?.init?.(),
    ]);
}
