(function () {
    const modalRoot = document.getElementById("modal-root");
    const themeStorageKey = "pmtracker.theme.mode";
    const printFormatStorageKey = "pmtracker.print.preferredFormat";
    const recordEditorPreferenceStorageKey = "pmtracker.recordEditor.preference";
    const projectListHideDoneStorageKey = "pmtracker.projects.hideDone";
    const projectListHideDeletedStorageKey = "pmtracker.projects.hideDeleted";
    const recordEditorReturnStateStoragePrefix = "pmtracker.recordEditor.returnState.project.";
    const mediaDark = window.matchMedia("(prefers-color-scheme: dark)");
    const modalState = {
        lastTrigger: null
    };
    const printState = {
        popover: null,
        trigger: null,
        hoverTimerId: 0
    };
    const recordEditorState = {
        chooser: null,
        chooserTrigger: null,
        closeGuard: null,
        closeGuardTrigger: null
    };
    const floatingPanelRegistry = new Set();
    let rainbowMeasureCanvas = null;
    let globalFloatingRoot = null;
    const projectFilterStoragePrefix = "pmtracker.projectFilters.v1.project.";
    const legacyProjectFilterPrefixes = [
        "pmtracker.filter.",
        "pmtracker.schedule.filter.",
        "pmtracker.gantt.filter."
    ];
    const legacyProjectFilterKeys = [
        "pmtracker.records.view",
        "pmtracker.gantt.filters.open"
    ];
    const legacyGanttStoragePrefixes = [
        "pmtracker.gantt.pinned.",
        "pmtracker.gantt.expanded."
    ];
    const projectFilterConfigs = {
        records: {
            rootSelector: '[data-project-filter-scope="records"]',
            inputSelector: "[data-filter-key]",
            keyAttribute: "data-filter-key",
            chipRowSelector: '[data-filter-chip-row="records"]',
            statusSelector: '[data-filter-save-status="records"]',
            fields: [
                { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
                { inputKey: "kategorie", stateKey: "kategorie", type: "select", chipLabel: "Kategorie" },
                { inputKey: "stav", stateKey: "stav", type: "select", chipLabel: "Stav úkolu" },
                { inputKey: "typ", stateKey: "typ", type: "select", chipLabel: "Typ úkolu" },
                { inputKey: "vlastnik", stateKey: "vlastnik", type: "select", chipLabel: "Vlastník" },
                { inputKey: "aktivni", stateKey: "aktivni", type: "checkbox", chipLabel: "Pouze aktivní úkoly" },
                { inputKey: "mine", stateKey: "mine", type: "checkbox", chipLabel: "Jen mé záznamy" },
                { inputKey: "jednani-vyjadreni-stav", stateKey: "jednaniVyjadreniStav", type: "select", chipLabel: "Jednání-vyjádření" },
                { inputKey: "groupBySubsystem", stateKey: "groupBySubsystem", type: "checkbox", skipChip: true }
            ]
        },
        schedule: {
            rootSelector: '[data-project-filter-scope="schedule"]',
            inputSelector: "[data-schedule-filter-key]",
            keyAttribute: "data-schedule-filter-key",
            chipRowSelector: '[data-filter-chip-row="schedule"]',
            statusSelector: '[data-filter-save-status="schedule"]',
            fields: [
                { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" }
            ]
        }
    };
    const modalFocusableSelector = [
        "a[href]",
        "button:not([disabled])",
        "input:not([disabled]):not([type='hidden'])",
        "select:not([disabled])",
        "textarea:not([disabled])",
        "[tabindex]:not([tabindex='-1'])"
    ].join(", ");

    function getStoredThemeMode() {
        return localStorage.getItem(themeStorageKey);
    }

    function resolveEffectiveTheme(mode) {
        if (mode === "dark" || mode === "light") {
            return mode;
        }
        return mediaDark.matches ? "dark" : "light";
    }

    function setTheme(mode, persist) {
        const normalized = mode === "dark" || mode === "light" || mode === "auto" ? mode : "auto";
        const effective = resolveEffectiveTheme(normalized);
        document.documentElement.setAttribute("data-theme", effective);
        document.documentElement.setAttribute("data-theme-mode", normalized);

        if (persist) {
            localStorage.setItem(themeStorageKey, normalized);
        }

        document.querySelectorAll("[data-theme-switch]").forEach((element) => {
            if (element instanceof HTMLInputElement) {
                element.checked = effective === "dark";
            }
        });
    }

    function initTheme() {
        const stored = getStoredThemeMode();
        setTheme(stored || "auto", false);

        const handleMediaChange = () => {
            const mode = getStoredThemeMode() || "auto";
            if (mode === "auto") {
                setTheme("auto", false);
            }
        };

        if (typeof mediaDark.addEventListener === "function") {
            mediaDark.addEventListener("change", handleMediaChange);
        } else if (typeof mediaDark.addListener === "function") {
            mediaDark.addListener(handleMediaChange);
        }

        document.querySelectorAll("[data-theme-switch]").forEach((element) => {
            if (!(element instanceof HTMLInputElement)) {
                return;
            }
            element.addEventListener("change", () => {
                setTheme(element.checked ? "dark" : "light", true);
            });
        });
    }

    function initUserMenu() {
        const menu = document.querySelector("[data-user-menu]");
        if (!(menu instanceof HTMLElement)) {
            return;
        }

        const toggle = menu.querySelector("[data-user-menu-toggle]");
        const panel = menu.querySelector("[data-user-menu-panel]");
        if (!(toggle instanceof HTMLButtonElement) || !(panel instanceof HTMLElement)) {
            return;
        }

        const setOpen = (open) => {
            panel.hidden = !open;
            toggle.setAttribute("aria-expanded", String(open));
        };

        toggle.addEventListener("click", () => {
            setOpen(panel.hidden);
        });

        document.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }
            if (!menu.contains(target)) {
                setOpen(false);
            }
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") {
                setOpen(false);
            }
        });

    }

    function getStoredPrintFormat() {
        const value = localStorage.getItem(printFormatStorageKey);
        if (value === "pdf" || value === "word") {
            return value;
        }
        return null;
    }

    function setStoredPrintFormat(format) {
        if (format !== "pdf" && format !== "word") {
            return;
        }

        localStorage.setItem(printFormatStorageKey, format);
        refreshPrintPreferenceUi();
    }

    function clearStoredPrintFormat() {
        localStorage.removeItem(printFormatStorageKey);
        refreshPrintPreferenceUi();
    }

    function getRecordEditorPreferenceLabel(mode) {
        if (mode === "modal") {
            return "Otevřít v modalu";
        }

        if (mode === "page") {
            return "Otevřít na stránce";
        }

        return "není nastaveno";
    }

    function getPrintFormatLabel(format) {
        if (format === "pdf") {
            return "PDF";
        }

        if (format === "word") {
            return "WORD";
        }

        return "není nastaven";
    }

    function refreshPrintPreferenceUi() {
        const preferred = getStoredPrintFormat();
        document.querySelectorAll("[data-print-preference-current]").forEach((element) => {
            element.textContent = getPrintFormatLabel(preferred);
        });

        document.querySelectorAll("[data-print-preference-reset]").forEach((element) => {
            if (element instanceof HTMLButtonElement) {
                element.disabled = preferred === null;
            }
        });
    }

    function refreshRecordEditorPreferenceUi() {
        const preferred = getStoredRecordEditorPreference();
        document.querySelectorAll("[data-record-editor-preference-current]").forEach((element) => {
            element.textContent = getRecordEditorPreferenceLabel(preferred);
        });

        document.querySelectorAll("[data-record-editor-preference-reset]").forEach((element) => {
            if (element instanceof HTMLButtonElement) {
                element.disabled = preferred === null;
            }
        });
    }

    function readBooleanStorageDefaultTrue(key) {
        const rawValue = window.localStorage.getItem(key);
        if (rawValue === null) {
            return true;
        }

        return rawValue === "true";
    }

    function writeProjectListStatusFilterState(key, value) {
        window.localStorage.setItem(key, value ? "true" : "false");
    }

    function readProjectListStatusFilterState() {
        return {
            hideDone: readBooleanStorageDefaultTrue(projectListHideDoneStorageKey),
            hideDeleted: readBooleanStorageDefaultTrue(projectListHideDeletedStorageKey)
        };
    }

    function resolveQueryRoot(scope) {
        return scope && typeof scope.querySelector === "function"
            ? scope
            : document;
    }

    function syncProjectListStatusFilterInputs(shell) {
        const root = resolveQueryRoot(shell);
        const state = readProjectListStatusFilterState();
        root.querySelectorAll("[data-project-status-hide]").forEach((input) => {
            if (!(input instanceof HTMLInputElement)) {
                return;
            }

            const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
            if (statusCode === "DONE") {
                input.checked = state.hideDone;
                return;
            }

            if (statusCode === "DELETED") {
                input.checked = state.hideDeleted;
            }
        });
    }

    function clearPrintHoverTimer() {
        if (printState.hoverTimerId) {
            window.clearTimeout(printState.hoverTimerId);
            printState.hoverTimerId = 0;
        }
    }

    function closePrintChooser(options) {
        const settings = options || {};
        const restoreFocus = Boolean(settings.restoreFocus);
        const trigger = printState.trigger;

        if (printState.popover instanceof HTMLElement) {
            printState.popover.remove();
        }

        printState.popover = null;
        printState.trigger = null;
        clearPrintHoverTimer();

        if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
            trigger.focus({ preventScroll: true });
        }
    }

    function resolvePrintUrl(trigger, format) {
        if (!(trigger instanceof Element)) {
            return "";
        }

        if (format === "word") {
            return trigger.getAttribute("data-print-word-url") || "";
        }

        return trigger.getAttribute("data-print-pdf-url") || trigger.getAttribute("href") || "";
    }

    function openPrintUrl(url) {
        if (!url) {
            return;
        }
        const link = document.createElement("a");
        link.href = url;
        link.target = "_blank";
        link.rel = "noopener";
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        link.remove();
    }

    function positionPrintChooser(popover, trigger) {
        if (!(popover instanceof HTMLElement) || !(trigger instanceof HTMLElement)) {
            return;
        }

        const rect = trigger.getBoundingClientRect();
        const width = popover.offsetWidth;
        const height = popover.offsetHeight;
        const gap = 8;

        let left = rect.right - width;
        let top = rect.bottom + gap;

        if (left < gap) {
            left = gap;
        }
        if (left + width > window.innerWidth - gap) {
            left = Math.max(gap, window.innerWidth - width - gap);
        }

        if (top + height > window.innerHeight - gap) {
            top = rect.top - height - gap;
        }
        if (top < gap) {
            top = gap;
        }

        popover.style.left = `${Math.round(left)}px`;
        popover.style.top = `${Math.round(top)}px`;
    }

    function handlePrintChoice(trigger, format, shouldRemember) {
        const url = resolvePrintUrl(trigger, format);
        if (!url) {
            return;
        }

        if (shouldRemember) {
            setStoredPrintFormat(format);
        }

        openPrintUrl(url);
    }

    function createPrintChooser(trigger, options) {
        const settings = options || {};
        const quickMode = Boolean(settings.quickMode);
        const label = trigger.getAttribute("data-print-label") || "Tisk";

        const popover = document.createElement("div");
        popover.className = "print-format-popover";
        popover.setAttribute("role", "dialog");
        popover.setAttribute("aria-modal", "false");
        popover.setAttribute("data-print-popover", "true");
        popover.setAttribute("tabindex", "-1");

        const title = document.createElement("h3");
        title.className = "print-format-title";
        title.textContent = quickMode ? "Jednorázová volba formátu" : "Vyberte formát tisku";
        popover.appendChild(title);

        const subtitle = document.createElement("p");
        subtitle.className = "print-format-subtitle";
        subtitle.textContent = label;
        popover.appendChild(subtitle);

        const actions = document.createElement("div");
        actions.className = "print-format-actions";

        const pdfButton = document.createElement("button");
        pdfButton.type = "button";
        pdfButton.className = "btn small";
        pdfButton.textContent = "PDF";
        pdfButton.setAttribute("data-print-choice", "pdf");
        actions.appendChild(pdfButton);

        const wordButton = document.createElement("button");
        wordButton.type = "button";
        wordButton.className = "btn small";
        wordButton.textContent = "WORD";
        wordButton.setAttribute("data-print-choice", "word");
        actions.appendChild(wordButton);

        popover.appendChild(actions);

        const rememberLabel = document.createElement("label");
        rememberLabel.className = "print-format-remember";
        const rememberCheckbox = document.createElement("input");
        rememberCheckbox.type = "checkbox";
        rememberCheckbox.setAttribute("data-print-remember", "true");
        rememberLabel.appendChild(rememberCheckbox);
        rememberLabel.append(quickMode
            ? " Nastavit jako novou preferenci pro tento počítač"
            : " Zapamatovat pro tento počítač");
        popover.appendChild(rememberLabel);

        const note = document.createElement("p");
        note.className = "print-format-note";
        note.textContent = quickMode
            ? "Ve výchozím stavu jde o jednorázovou volbu. Uloženou preferenci změníte jen zaškrtnutím volby výše."
            : "Volba se ukládá pouze pro tento počítač/prohlížeč.";
        popover.appendChild(note);

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "print-format-close";
        closeButton.setAttribute("aria-label", "Zavřít výběr formátu tisku");
        closeButton.textContent = "×";
        popover.appendChild(closeButton);

        popover.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            if (target.closest(".print-format-close")) {
                event.preventDefault();
                closePrintChooser({ restoreFocus: true });
                return;
            }

            const choice = target.closest("[data-print-choice]");
            if (!choice) {
                return;
            }

            event.preventDefault();
            const format = choice.getAttribute("data-print-choice");
            if (format !== "pdf" && format !== "word") {
                return;
            }

            const remember = rememberCheckbox.checked;
            closePrintChooser({ restoreFocus: false });
            handlePrintChoice(trigger, format, remember);
        });

        return popover;
    }

    function showPrintChooser(trigger, options) {
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        closePrintChooser({ restoreFocus: false });

        const popover = createPrintChooser(trigger, options);
        document.body.appendChild(popover);
        positionPrintChooser(popover, trigger);
        printState.popover = popover;
        printState.trigger = trigger;

        const firstAction = popover.querySelector("[data-print-choice]");
        if (firstAction instanceof HTMLElement) {
            firstAction.focus({ preventScroll: true });
        } else {
            popover.focus({ preventScroll: true });
        }
    }

    function handlePrintTriggerClick(trigger) {
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        clearPrintHoverTimer();
        const preferredFormat = getStoredPrintFormat();
        if (preferredFormat) {
            const preferredUrl = resolvePrintUrl(trigger, preferredFormat);
            if (preferredUrl) {
                closePrintChooser({ restoreFocus: false });
                openPrintUrl(preferredUrl);
                return;
            }
        }

        showPrintChooser(trigger, { quickMode: false });
    }

    function initPrintFormatChooser() {
        refreshPrintPreferenceUi();

        document.addEventListener("pointerover", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const trigger = target.closest("[data-print-trigger]");
            if (!(trigger instanceof HTMLElement)) {
                return;
            }

            if (event.relatedTarget instanceof Element && trigger.contains(event.relatedTarget)) {
                return;
            }

            if (event instanceof PointerEvent && event.pointerType !== "mouse") {
                return;
            }

            if (!getStoredPrintFormat()) {
                return;
            }

            clearPrintHoverTimer();
            printState.hoverTimerId = window.setTimeout(() => {
                showPrintChooser(trigger, { quickMode: true });
            }, 2000);
        });

        document.addEventListener("pointerout", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const trigger = target.closest("[data-print-trigger]");
            if (!(trigger instanceof HTMLElement)) {
                return;
            }

            if (event.relatedTarget instanceof Element && trigger.contains(event.relatedTarget)) {
                return;
            }

            clearPrintHoverTimer();
        });

        window.addEventListener("scroll", () => {
            if (printState.popover instanceof HTMLElement && printState.trigger instanceof HTMLElement) {
                positionPrintChooser(printState.popover, printState.trigger);
            }

            if (recordEditorState.chooser instanceof HTMLElement && recordEditorState.chooserTrigger instanceof HTMLElement) {
                positionPrintChooser(recordEditorState.chooser, recordEditorState.chooserTrigger);
            }
        }, true);

        window.addEventListener("resize", () => {
            if (printState.popover instanceof HTMLElement && printState.trigger instanceof HTMLElement) {
                positionPrintChooser(printState.popover, printState.trigger);
            }

            if (recordEditorState.chooser instanceof HTMLElement && recordEditorState.chooserTrigger instanceof HTMLElement) {
                positionPrintChooser(recordEditorState.chooser, recordEditorState.chooserTrigger);
            }
        });
    }

    function getStoredRecordEditorPreference() {
        const value = localStorage.getItem(recordEditorPreferenceStorageKey);
        if (value === "modal" || value === "page") {
            return value;
        }

        return null;
    }

    function setStoredRecordEditorPreference(mode) {
        if (mode !== "modal" && mode !== "page") {
            return;
        }

        localStorage.setItem(recordEditorPreferenceStorageKey, mode);
        refreshRecordEditorPreferenceUi();
    }

    function clearStoredRecordEditorPreference() {
        localStorage.removeItem(recordEditorPreferenceStorageKey);
        refreshRecordEditorPreferenceUi();
    }

    function getCurrentLocalUrl() {
        return `${window.location.pathname}${window.location.search}${window.location.hash}`;
    }

    function getRecordEditorReturnStateKey(projectId) {
        return `${recordEditorReturnStateStoragePrefix}${projectId}`;
    }

    function closeRecordEditorChooser(options) {
        const settings = options || {};
        const restoreFocus = Boolean(settings.restoreFocus);
        const trigger = recordEditorState.chooserTrigger;

        if (recordEditorState.chooser instanceof HTMLElement) {
            recordEditorState.chooser.remove();
        }

        recordEditorState.chooser = null;
        recordEditorState.chooserTrigger = null;

        if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
            trigger.focus({ preventScroll: true });
        }
    }

    function buildRecordEditorUrl(trigger, mode) {
        if (!(trigger instanceof HTMLElement)) {
            return "";
        }

        const rawUrl = trigger.getAttribute("data-record-editor-url") || "";
        if (!rawUrl) {
            return "";
        }

        const editorUrl = new URL(rawUrl, window.location.origin);
        editorUrl.searchParams.set("presentation", mode === "page" ? "page" : "modal");
        editorUrl.searchParams.set("returnUrl", getCurrentLocalUrl());
        return `${editorUrl.pathname}${editorUrl.search}${editorUrl.hash}`;
    }

    function captureRecordEditorReturnState(trigger) {
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        const projectId = Number.parseInt(trigger.getAttribute("data-record-editor-project-id") || "", 10);
        if (!Number.isInteger(projectId) || projectId <= 0) {
            return;
        }

        const scopeRoot = document.querySelector(`[data-project-detail-root][data-project-id="${CSS.escape(String(projectId))}"]`)
            || document.querySelector("[data-project-detail-root]");
        const baseState = buildRecordUiState(scopeRoot instanceof HTMLElement ? scopeRoot : document);
        const state = {
            projectId,
            returnUrl: getCurrentLocalUrl(),
            activeTab: baseState.activeTab,
            scrollY: baseState.scrollY,
            expandedRecordIds: Array.isArray(baseState.expandedRecordIds) ? baseState.expandedRecordIds : [],
            commentSortDirectionByRecordId: baseState.commentSortDirectionByRecordId || {},
            capturedAt: new Date().toISOString()
        };

        sessionStorage.setItem(getRecordEditorReturnStateKey(projectId), JSON.stringify(state));
    }

    function navigateToRecordEditorPage(trigger) {
        const targetUrl = buildRecordEditorUrl(trigger, "page");
        if (!targetUrl) {
            return;
        }

        captureRecordEditorReturnState(trigger);
        window.location.assign(targetUrl);
    }

    function handleRecordEditorChoice(trigger, mode, shouldSkipRemember) {
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        if (!shouldSkipRemember) {
            setStoredRecordEditorPreference(mode);
        }

        if (mode === "page") {
            navigateToRecordEditorPage(trigger);
            return;
        }

        openUrlModal(buildRecordEditorUrl(trigger, "modal"), trigger);
    }

    function createRecordEditorChooser(trigger) {
        const label = trigger.getAttribute("data-record-editor-label") || "Editor záznamu";

        const popover = document.createElement("div");
        popover.className = "record-editor-popover";
        popover.setAttribute("role", "dialog");
        popover.setAttribute("aria-modal", "false");
        popover.setAttribute("data-record-editor-popover", "true");
        popover.setAttribute("tabindex", "-1");

        const title = document.createElement("h3");
        title.className = "record-editor-popover-title";
        title.textContent = "Vyberte způsob otevření";
        popover.appendChild(title);

        const subtitle = document.createElement("p");
        subtitle.className = "record-editor-popover-subtitle";
        subtitle.textContent = label;
        popover.appendChild(subtitle);

        const actions = document.createElement("div");
        actions.className = "record-editor-popover-actions";

        const modalButton = document.createElement("button");
        modalButton.type = "button";
        modalButton.className = "btn small";
        modalButton.textContent = "Otevřít v modalu";
        modalButton.setAttribute("data-record-editor-mode", "modal");
        actions.appendChild(modalButton);

        const pageButton = document.createElement("button");
        pageButton.type = "button";
        pageButton.className = "btn small";
        pageButton.textContent = "Otevřít na stránce";
        pageButton.setAttribute("data-record-editor-mode", "page");
        actions.appendChild(pageButton);

        popover.appendChild(actions);

        const rememberLabel = document.createElement("label");
        rememberLabel.className = "record-editor-popover-remember";
        const rememberCheckbox = document.createElement("input");
        rememberCheckbox.type = "checkbox";
        rememberCheckbox.setAttribute("data-record-editor-remember", "true");
        rememberLabel.appendChild(rememberCheckbox);
        rememberLabel.append(" Neukládat pro tentokrát jako výchozí volbu");
        popover.appendChild(rememberLabel);

        const note = document.createElement("p");
        note.className = "record-editor-popover-note";
        note.textContent = "Pokud volbu neuložíte, systém se při dalším otevření zeptá znovu.";
        popover.appendChild(note);

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "record-editor-popover-close";
        closeButton.setAttribute("aria-label", "Zavřít výběr způsobu otevření editoru");
        closeButton.textContent = "×";
        popover.appendChild(closeButton);

        popover.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            if (target.closest(".record-editor-popover-close")) {
                event.preventDefault();
                closeRecordEditorChooser({ restoreFocus: true });
                return;
            }

            const choice = target.closest("[data-record-editor-mode]");
            if (!choice) {
                return;
            }

            event.preventDefault();
            const mode = choice.getAttribute("data-record-editor-mode");
            if (mode !== "modal" && mode !== "page") {
                return;
            }

            const skipRemember = rememberCheckbox.checked;
            closeRecordEditorChooser({ restoreFocus: false });
            handleRecordEditorChoice(trigger, mode, skipRemember);
        });

        return popover;
    }

    function showRecordEditorChooser(trigger) {
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        closeRecordEditorChooser({ restoreFocus: false });

        const popover = createRecordEditorChooser(trigger);
        document.body.appendChild(popover);
        positionPrintChooser(popover, trigger);
        recordEditorState.chooser = popover;
        recordEditorState.chooserTrigger = trigger;

        const firstAction = popover.querySelector("[data-record-editor-mode]");
        if (firstAction instanceof HTMLElement) {
            firstAction.focus({ preventScroll: true });
        } else {
            popover.focus({ preventScroll: true });
        }
    }

    function openRecordEditor(trigger, forcedMode) {
        if (!(trigger instanceof HTMLElement)) {
            return;
        }

        const mode = forcedMode || getStoredRecordEditorPreference();
        if (mode === "modal") {
            openUrlModal(buildRecordEditorUrl(trigger, "modal"), trigger);
            return;
        }

        if (mode === "page") {
            navigateToRecordEditorPage(trigger);
            return;
        }

        showRecordEditorChooser(trigger);
    }

    function restoreRecordEditorReturnStateFromUrl() {
        const projectRoot = document.querySelector("[data-project-detail-root]");
        if (!(projectRoot instanceof HTMLElement)) {
            return;
        }

        const currentUrl = new URL(window.location.href);
        if (currentUrl.searchParams.get("restoreRecordEditorState") !== "1") {
            return;
        }

        const cleanupUrl = () => {
            currentUrl.searchParams.delete("restoreRecordEditorState");
            history.replaceState(history.state || {}, "", `${currentUrl.pathname}${currentUrl.search}${currentUrl.hash}`);
        };

        const projectId = Number.parseInt(projectRoot.dataset.projectId || "", 10);
        if (!Number.isInteger(projectId) || projectId <= 0) {
            cleanupUrl();
            return;
        }

        const storageKey = getRecordEditorReturnStateKey(projectId);
        const rawState = sessionStorage.getItem(storageKey);
        if (!rawState) {
            cleanupUrl();
            return;
        }

        try {
            const state = JSON.parse(rawState);
            restoreRecordUiState(state);
        } catch (error) {
            console.warn("Nepodařilo se obnovit návratový stav editoru záznamu.", error);
        } finally {
            sessionStorage.removeItem(storageKey);
            cleanupUrl();
        }
    }

    function getActiveModalOverlay() {
        if (!(modalRoot instanceof HTMLElement)) {
            return null;
        }

        return modalRoot.querySelector(".modal-overlay");
    }

    function getActiveModalContainer() {
        const overlay = getActiveModalOverlay();
        if (!(overlay instanceof HTMLElement)) {
            return null;
        }

        return overlay.querySelector("[data-modal-container]");
    }

    function isModalOpen() {
        return modalRoot instanceof HTMLElement
            && modalRoot.getAttribute("aria-hidden") !== "true"
            && modalRoot.childElementCount > 0;
    }

    function getFocusableElementsWithinModal(container) {
        if (!(container instanceof HTMLElement)) {
            return [];
        }

        return Array.from(container.querySelectorAll(modalFocusableSelector))
            .filter((element) => element instanceof HTMLElement)
            .filter((element) => {
                if (element.hasAttribute("disabled")) {
                    return false;
                }
                if (element.getAttribute("aria-hidden") === "true") {
                    return false;
                }

                return element.getClientRects().length > 0;
            });
    }

    function focusInitialModalElement() {
        const modal = getActiveModalContainer();
        if (!(modal instanceof HTMLElement)) {
            return;
        }

        const autofocusCandidate = modal.querySelector("[autofocus]");
        if (autofocusCandidate instanceof HTMLElement && !autofocusCandidate.hasAttribute("disabled")) {
            autofocusCandidate.focus({ preventScroll: true });
            return;
        }

        const focusable = getFocusableElementsWithinModal(modal);
        if (focusable.length > 0) {
            focusable[0].focus({ preventScroll: true });
            return;
        }

        modal.focus({ preventScroll: true });
    }

    function setModalContent(content, trigger) {
        if (!modalRoot) {
            return;
        }
        closeAllFloatingPanels();
        modalRoot.innerHTML = "";
        modalRoot.appendChild(content);
        modalRoot.style.pointerEvents = "auto";
        modalRoot.setAttribute("aria-hidden", "false");
        document.body.classList.add("modal-open");
        modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
        initRecordFormEnhancements(modalRoot);
        initPermissionMetadataBindings(modalRoot);
        window.requestAnimationFrame(() => {
            focusInitialModalElement();
        });
    }

    function closeModal() {
        if (!modalRoot) {
            return;
        }

        const focusTarget = modalState.lastTrigger;
        closeAllFloatingPanels();
        modalRoot.innerHTML = "";
        modalRoot.style.pointerEvents = "none";
        modalRoot.setAttribute("aria-hidden", "true");
        document.body.classList.remove("modal-open");
        modalState.lastTrigger = null;
        if (focusTarget instanceof HTMLElement && focusTarget.isConnected) {
            focusTarget.focus({ preventScroll: true });
        }
    }

    async function openUrlModal(url, trigger) {
        if (!url) {
            return;
        }

        try {
            const response = await fetch(url, { headers: { "X-Requested-With": "XMLHttpRequest" } });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            const html = await response.text();
            const wrapper = document.createElement("div");
            wrapper.innerHTML = html;
            setModalContent(wrapper, trigger);
        } catch (err) {
            console.error("Modal load failed", err);
        }
    }

    function trapFocusInModal(event) {
        const modal = getActiveModalContainer();
        if (!(modal instanceof HTMLElement)) {
            return;
        }

        const focusable = getFocusableElementsWithinModal(modal);
        if (focusable.length === 0) {
            event.preventDefault();
            modal.focus({ preventScroll: true });
            return;
        }

        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        const activeElement = document.activeElement;
        const activeInsideModal = activeElement instanceof Element && modal.contains(activeElement);

        if (event.shiftKey) {
            if (!activeInsideModal || activeElement === first) {
                event.preventDefault();
                last.focus({ preventScroll: true });
            }
            return;
        }

        if (!activeInsideModal || activeElement === last) {
            event.preventDefault();
            first.focus({ preventScroll: true });
        }
    }

    function getProjectFilterConfig(scope) {
        return projectFilterConfigs[scope] || null;
    }

    function getProjectFilterRoot(scope) {
        const config = getProjectFilterConfig(scope);
        if (!config) {
            return null;
        }

        const root = document.querySelector(config.rootSelector);
        return root instanceof HTMLElement ? root : null;
    }

    function getProjectFilterInput(scope, inputKey) {
        const config = getProjectFilterConfig(scope);
        const root = getProjectFilterRoot(scope);
        if (!config || !(root instanceof HTMLElement)) {
            return null;
        }

        const input = root.querySelector(`${config.inputSelector}[${config.keyAttribute}="${inputKey}"]`);
        return input instanceof HTMLInputElement || input instanceof HTMLSelectElement ? input : null;
    }

    function getProjectFilterProjectId(scope) {
        const root = getProjectFilterRoot(scope);
        const projectId = (root?.dataset.projectId || "").trim();
        return projectId || "0";
    }

    function getProjectFilterCurrentUserId(scope) {
        return normalizeFilterToken(getProjectFilterRoot(scope)?.dataset.currentUserId || "");
    }

    function getProjectFilterStorageKey(scope, kind) {
        const projectId = getProjectFilterProjectId(scope);
        if (!projectId || projectId === "0") {
            return "";
        }

        return `${projectFilterStoragePrefix}${projectId}.${scope}.${kind}`;
    }

    function readJsonStorage(storage, key) {
        if (!key) {
            return null;
        }

        try {
            const raw = storage.getItem(key);
            if (!raw) {
                return null;
            }

            const parsed = JSON.parse(raw);
            return parsed && typeof parsed === "object" ? parsed : null;
        } catch (error) {
            return null;
        }
    }

    function writeJsonStorage(storage, key, value) {
        if (!key) {
            return;
        }

        storage.setItem(key, JSON.stringify(value));
    }

    function hasSelectOptionValue(input, value) {
        if (!(input instanceof HTMLSelectElement)) {
            return false;
        }

        return Array.from(input.options).some((option) => option.value === value);
    }

    function readProjectFilterInputValue(input, field) {
        if (input instanceof HTMLInputElement && field.type === "checkbox") {
            return input.checked;
        }

        if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
            return input.value;
        }

        return field.type === "checkbox" ? false : "";
    }

    function normalizeProjectFilterState(scope, rawState, fallbackState) {
        const config = getProjectFilterConfig(scope);
        if (!config) {
            return {};
        }

        const normalized = {};
        const source = rawState && typeof rawState === "object" ? rawState : {};
        const fallback = fallbackState && typeof fallbackState === "object" ? fallbackState : {};

        config.fields.forEach((field) => {
            const input = getProjectFilterInput(scope, field.inputKey);
            const fallbackValue = fallback[field.stateKey];
            const sourceValue = source[field.stateKey];

            if (field.type === "checkbox") {
                if (typeof sourceValue === "boolean") {
                    normalized[field.stateKey] = sourceValue;
                    return;
                }

                if (sourceValue === "true" || sourceValue === "false") {
                    normalized[field.stateKey] = sourceValue === "true";
                    return;
                }

                normalized[field.stateKey] = Boolean(fallbackValue);
                return;
            }

            const fallbackText = typeof fallbackValue === "string" ? fallbackValue : "";
            const candidate = typeof sourceValue === "string" ? sourceValue : fallbackText;

            if (candidate && input instanceof HTMLSelectElement && !hasSelectOptionValue(input, candidate)) {
                normalized[field.stateKey] = fallbackText && hasSelectOptionValue(input, fallbackText)
                    ? fallbackText
                    : "";
                return;
            }

            normalized[field.stateKey] = candidate;
        });

        return normalized;
    }

    function buildProjectFilterStateFromInputs(scope) {
        const config = getProjectFilterConfig(scope);
        if (!config) {
            return {};
        }

        const state = {};
        config.fields.forEach((field) => {
            const input = getProjectFilterInput(scope, field.inputKey);
            state[field.stateKey] = readProjectFilterInputValue(input, field);
        });

        return state;
    }

    function applyProjectFilterStateToInputs(scope, state) {
        const config = getProjectFilterConfig(scope);
        if (!config) {
            return;
        }

        config.fields.forEach((field) => {
            const input = getProjectFilterInput(scope, field.inputKey);
            if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
                return;
            }

            const value = state[field.stateKey];
            if (field.type === "checkbox" && input instanceof HTMLInputElement) {
                input.checked = Boolean(value);
                return;
            }

            input.value = typeof value === "string" ? value : "";
        });
    }

    function readStoredProjectFilterState(scope, kind, fallbackState) {
        const storage = kind === "state" ? sessionStorage : localStorage;
        const key = getProjectFilterStorageKey(scope, kind);
        const rawState = readJsonStorage(storage, key);
        if (!rawState) {
            return null;
        }

        return normalizeProjectFilterState(scope, rawState, fallbackState);
    }

    function persistProjectFilterSessionState(scope) {
        const currentState = buildProjectFilterStateFromInputs(scope);
        const normalizedState = normalizeProjectFilterState(scope, currentState, currentState);
        const key = getProjectFilterStorageKey(scope, "state");
        writeJsonStorage(sessionStorage, key, normalizedState);
        return normalizedState;
    }

    function setProjectFilterSaveStatus(scope, message) {
        const config = getProjectFilterConfig(scope);
        const root = getProjectFilterRoot(scope);
        if (!config || !(root instanceof HTMLElement)) {
            return;
        }

        const status = root.querySelector(config.statusSelector);
        if (status instanceof HTMLElement) {
            status.textContent = message || "";
        }
    }

    function buildProjectFilterChipLabel(field, input) {
        if (field.type === "checkbox") {
            return field.chipLabel || "";
        }

        if (!(input instanceof HTMLSelectElement)) {
            return "";
        }

        const option = input.selectedOptions[0];
        const optionText = option?.textContent?.trim() || "";
        if (!optionText) {
            return "";
        }

        return `${field.chipLabel}: ${optionText}`;
    }

    function renderProjectFilterChips(scope) {
        const config = getProjectFilterConfig(scope);
        const root = getProjectFilterRoot(scope);
        if (!config || !(root instanceof HTMLElement)) {
            return;
        }

        const chipRow = root.querySelector(config.chipRowSelector);
        if (!(chipRow instanceof HTMLElement)) {
            return;
        }

        chipRow.innerHTML = "";
        const state = buildProjectFilterStateFromInputs(scope);
        const chips = [];

        config.fields.forEach((field) => {
            if (field.skipChip) {
                return;
            }

            const value = state[field.stateKey];
            const isActive = field.type === "checkbox" ? Boolean(value) : Boolean(value);
            if (!isActive) {
                return;
            }

            const input = getProjectFilterInput(scope, field.inputKey);
            const label = buildProjectFilterChipLabel(field, input);
            if (!label) {
                return;
            }

            const chip = document.createElement("span");
            chip.className = "active-filter-chip";

            const text = document.createElement("span");
            text.className = "active-filter-chip-label";
            text.textContent = label;

            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "active-filter-chip-remove";
            remove.setAttribute("data-filter-chip-remove", scope);
            remove.setAttribute("data-filter-chip-key", field.inputKey);
            remove.setAttribute("aria-label", `Odebrat filtr ${label}`);
            remove.textContent = "×";

            chip.append(text, remove);
            chips.push(chip);
        });

        chipRow.hidden = chips.length === 0;
        chips.forEach((chip) => chipRow.appendChild(chip));
    }

    function applyProjectFilterScope(scope) {
        if (scope === "records") {
            const state = buildProjectFilterStateFromInputs(scope);
            applyRecordsView(Boolean(state.groupBySubsystem) ? "subsystem" : "flat");
            return;
        }

        if (scope === "schedule") {
            applyProjectScheduleFilters();
        }
    }

    function handleProjectFilterInputChange(scope) {
        persistProjectFilterSessionState(scope);
        renderProjectFilterChips(scope);
        setProjectFilterSaveStatus(scope, "");
        applyProjectFilterScope(scope);
    }

    function restoreProjectFilterScope(scope) {
        const fallbackState = buildProjectFilterStateFromInputs(scope);
        const restoredState = readStoredProjectFilterState(scope, "state", fallbackState)
            || readStoredProjectFilterState(scope, "defaults", fallbackState)
            || fallbackState;

        applyProjectFilterStateToInputs(scope, restoredState);
        persistProjectFilterSessionState(scope);
        renderProjectFilterChips(scope);
        return restoredState;
    }

    function saveProjectFilterDefaults(scope) {
        const state = persistProjectFilterSessionState(scope);
        const key = getProjectFilterStorageKey(scope, "defaults");
        writeJsonStorage(localStorage, key, state);
        setProjectFilterSaveStatus(scope, "Výchozí filtry uloženy v tomto prohlížeči.");
    }

    function clearProjectFilterInput(scope, inputKey) {
        const input = getProjectFilterInput(scope, inputKey);
        if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
            return;
        }

        if (input instanceof HTMLInputElement && input.type === "checkbox") {
            input.checked = false;
        } else {
            input.value = "";
        }
    }

    function removeMatchingStorageKeys(storage, predicate) {
        const keys = [];
        for (let i = 0; i < storage.length; i += 1) {
            const key = storage.key(i);
            if (key && predicate(key)) {
                keys.push(key);
            }
        }

        keys.forEach((key) => storage.removeItem(key));
    }

    function clearProjectFilterPreferenceStorage() {
        removeMatchingStorageKeys(localStorage, (key) =>
            key.startsWith(projectFilterStoragePrefix)
            || legacyProjectFilterKeys.includes(key)
            || legacyProjectFilterPrefixes.some((prefix) => key.startsWith(prefix))
            || legacyGanttStoragePrefixes.some((prefix) => key.startsWith(prefix)));

        removeMatchingStorageKeys(sessionStorage, (key) =>
            key.startsWith(projectFilterStoragePrefix)
            || legacyProjectFilterKeys.includes(key)
            || legacyProjectFilterPrefixes.some((prefix) => key.startsWith(prefix))
            || legacyGanttStoragePrefixes.some((prefix) => key.startsWith(prefix)));
    }

    function setFilterPanelOpen(open) {
        const filterPanel = document.querySelector("[data-filter-panel]");
        const filterToggle = document.querySelector("[data-filter-toggle]");
        const key = "pmtracker.filters.open";
        if (!filterPanel) {
            return;
        }
        filterPanel.classList.toggle("collapsed", !open);
        if (filterToggle) {
            filterToggle.setAttribute("aria-expanded", String(open));
        }
        localStorage.setItem(key, String(open));
    }

    function applyRecordsView(view) {
        const shells = document.querySelectorAll("[data-records-view]");
        if (shells.length === 0) {
            return;
        }
        shells.forEach((shell) => {
            const mode = shell.getAttribute("data-records-view");
            shell.toggleAttribute("hidden", mode !== view);
        });
        applyProjectRecordFilters();
        scheduleSubsystemIndicatorSync();
    }

    function restoreFilterState() {
        return restoreProjectFilterScope("records");
    }

    function persistFilterState(input) {
        handleProjectFilterInputChange("records");
    }

    function normalizeFilterText(value) {
        if (!value) {
            return "";
        }

        return value
            .toString()
            .trim()
            .toLowerCase()
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "");
    }

    function normalizeFilterToken(value) {
        if (value === null || value === undefined) {
            return "";
        }

        return String(value).trim().toUpperCase();
    }

    function normalizeSearchText(value) {
        return normalizeFilterText(value);
    }

    function containsWordPrefix(text, token) {
        if (!text || !token) {
            return false;
        }

        const parts = text.split(/[\s@._,;:/\\-]+/g).filter(Boolean);
        return parts.some((part) => part.startsWith(token));
    }

    function scoreSearchCandidate(query, haystack) {
        const normalizedQuery = normalizeSearchText(query);
        const normalizedHaystack = normalizeSearchText(haystack);

        if (!normalizedQuery) {
            return 1;
        }
        if (!normalizedHaystack) {
            return 0;
        }

        const tokens = normalizedQuery.split(/\s+/g).filter(Boolean);
        let score = 0;

        if (normalizedHaystack === normalizedQuery) {
            score += 1600;
        }
        if (normalizedHaystack.startsWith(normalizedQuery)) {
            score += 1100;
        }
        if (normalizedHaystack.includes(normalizedQuery)) {
            score += 700;
        }

        tokens.forEach((token) => {
            if (containsWordPrefix(normalizedHaystack, token)) {
                score += 180;
            } else if (normalizedHaystack.includes(token)) {
                score += 85;
            }
        });

        return score;
    }

    function debounce(callback, waitMs) {
        let timeoutId = 0;
        return (...args) => {
            window.clearTimeout(timeoutId);
            timeoutId = window.setTimeout(() => callback(...args), waitMs);
        };
    }

    function measureTextWidth(text, fontSpec) {
        const normalized = String(text || "").trim();
        if (!normalized) {
            return 0;
        }

        if (!(rainbowMeasureCanvas instanceof HTMLCanvasElement)) {
            rainbowMeasureCanvas = document.createElement("canvas");
        }

        const context = rainbowMeasureCanvas.getContext("2d");
        if (!context) {
            return normalized.length * 7;
        }

        context.font = fontSpec || "600 11px sans-serif";
        return context.measureText(normalized).width;
    }

    function parseColorChannels(value) {
        const normalized = String(value || "").trim();
        if (!normalized) {
            return null;
        }

        if (normalized.startsWith("#")) {
            const hex = normalized.slice(1);
            if (hex.length === 6) {
                const r = Number.parseInt(hex.slice(0, 2), 16);
                const g = Number.parseInt(hex.slice(2, 4), 16);
                const b = Number.parseInt(hex.slice(4, 6), 16);
                if (Number.isFinite(r) && Number.isFinite(g) && Number.isFinite(b)) {
                    return { r, g, b };
                }
            }
        }

        const match = normalized.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/i);
        if (!match) {
            return null;
        }

        const r = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[1]))));
        const g = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[2]))));
        const b = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[3]))));
        if (!Number.isFinite(r) || !Number.isFinite(g) || !Number.isFinite(b)) {
            return null;
        }

        return { r, g, b };
    }

    function getContrastTextColor(backgroundColor) {
        const channels = parseColorChannels(backgroundColor);
        if (!channels) {
            return "#0F172A";
        }

        const toLinear = (channel) => {
            const normalized = channel / 255;
            if (normalized <= 0.03928) {
                return normalized / 12.92;
            }
            return ((normalized + 0.055) / 1.055) ** 2.4;
        };

        const luminance = (0.2126 * toLinear(channels.r))
            + (0.7152 * toLinear(channels.g))
            + (0.0722 * toLinear(channels.b));

        return luminance >= 0.45 ? "#0F172A" : "#F8FAFC";
    }

    function pickSegmentLabel(fullLabel, shortLabel, availableWidthPx, fontSpec) {
        const available = Math.max(0, Number(availableWidthPx) || 0);
        if (available < 14) {
            return "";
        }

        const normalizedFull = String(fullLabel || "").trim();
        const normalizedShort = String(shortLabel || "").trim();
        const candidates = [normalizedFull, normalizedShort].filter(Boolean);
        const horizontalPaddingPx = 8;

        for (const candidate of candidates) {
            if (measureTextWidth(candidate, fontSpec) + horizontalPaddingPx <= available) {
                return candidate;
            }
        }

        return "";
    }

    function renderRainbowSegmentLabel(segment) {
        if (!(segment instanceof HTMLElement)) {
            return;
        }

        const fullLabel = segment.dataset.rainbowSegmentLabelFull || "";
        const shortLabel = segment.dataset.rainbowSegmentLabelShort || "";
        if (!fullLabel && !shortLabel) {
            return;
        }

        const width = segment.getBoundingClientRect().width;
        const computed = window.getComputedStyle(segment);
        const fontSpec = `${computed.fontWeight} ${computed.fontSize} ${computed.fontFamily}`;
        const selected = pickSegmentLabel(fullLabel, shortLabel, width, fontSpec);

        segment.textContent = selected;
        if (!selected) {
            segment.style.removeProperty("color");
            return;
        }

        segment.style.color = getContrastTextColor(computed.backgroundColor);
    }

    function renderAllRainbowSegmentLabels(scope) {
        const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
        root
            .querySelectorAll(
                ".schedule-overview-segment[data-rainbow-segment-label-short], "
                + ".schedule-layered-segment[data-rainbow-segment-label-short], "
                + ".schedule-mini-gantt-segment[data-rainbow-segment-label-short]")
            .forEach((segment) => {
                renderRainbowSegmentLabel(segment);
            });
    }

    function queueRainbowSegmentRender(scope) {
        window.requestAnimationFrame(() => renderAllRainbowSegmentLabels(scope));
    }

    function getFilterValue(key) {
        const input = getProjectFilterInput("records", key);
        if (input instanceof HTMLInputElement && input.type === "checkbox") {
            return input.checked;
        }
        if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
            return input.value;
        }
        return "";
    }

    function setRecordFilterVisibility(element, isVisible) {
        if (!(element instanceof HTMLElement)) {
            return;
        }

        element.classList.toggle("is-filter-hidden", !isVisible);
        element.hidden = !isVisible;
    }

    function applyProjectRecordFilters() {
        const cards = document.querySelectorAll(".record-card[data-record-id]");
        if (cards.length === 0) {
            return;
        }

        const state = buildProjectFilterStateFromInputs("records");
        const currentUserId = getProjectFilterCurrentUserId("records");
        const hasCurrentUser = currentUserId && currentUserId !== "0";
        const filters = {
            subsystem: normalizeFilterToken(state.subsystem),
            kategorie: normalizeFilterToken(state.kategorie),
            stav: normalizeFilterToken(state.stav),
            typ: normalizeFilterToken(state.typ),
            vlastnik: normalizeFilterToken(state.vlastnik),
            onlyActive: Boolean(state.aktivni),
            mine: Boolean(state.mine),
            meetingCommentState: normalizeFilterToken(state.jednaniVyjadreniStav)
        };

        cards.forEach((item) => {
            if (!(item instanceof HTMLElement)) {
                return;
            }

            const subsystem = normalizeFilterToken(item.dataset.filterSubsystemKod || item.dataset.filterSubsystem);
            const kategorie = normalizeFilterToken(item.dataset.filterKategorieKod || item.dataset.filterKategorie);
            const stav = normalizeFilterToken(item.dataset.filterStavKod || item.dataset.filterStav);
            const typ = normalizeFilterToken(item.dataset.filterTypKod || item.dataset.filterTyp);
            const vlastnik = normalizeFilterToken(item.dataset.filterVlastnikId || item.dataset.filterVlastnik);
            const isActive = item.dataset.filterAktivni === "true";
            const isTask = item.dataset.filterJeUkol === "true";
            const commentMeetingStates = (item.dataset.filterVyjadreniJednaniStavy || "")
                .split(/[|,]/g)
                .map((value) => normalizeFilterToken(value))
                .filter(Boolean);
            const matchesMeetingCommentState = !filters.meetingCommentState
                || (isTask && commentMeetingStates.includes(filters.meetingCommentState));
            const matchesMine = !filters.mine || (hasCurrentUser && vlastnik === currentUserId);

            const matches =
                (!filters.subsystem || subsystem === filters.subsystem) &&
                (!filters.kategorie || kategorie === filters.kategorie) &&
                (!filters.stav || stav === filters.stav) &&
                (!filters.typ || typ === filters.typ) &&
                (!filters.vlastnik || vlastnik === filters.vlastnik) &&
                (!filters.onlyActive || isActive) &&
                matchesMine &&
                matchesMeetingCommentState;

            setRecordFilterVisibility(item, matches);
        });

        document.querySelectorAll(".subsystem-group").forEach((group) => {
            if (!(group instanceof HTMLElement)) {
                return;
            }

            const hasVisibleCards = Array.from(group.querySelectorAll(".record-card"))
                .some((card) => card instanceof HTMLElement && !card.hidden);
            setRecordFilterVisibility(group, hasVisibleCards);
        });

        scheduleSubsystemIndicatorSync();
    }

    function resolveCurrentSubsystemGroup(groups, anchorY) {
        if (!Array.isArray(groups) || groups.length === 0) {
            return null;
        }

        let current = groups[0];
        for (const group of groups) {
            if (!(group instanceof HTMLElement)) {
                continue;
            }

            const rect = group.getBoundingClientRect();
            if (rect.bottom <= anchorY) {
                current = group;
                continue;
            }

            if (rect.top <= anchorY) {
                current = group;
            }
            break;
        }

        return current;
    }

    function resolveActiveSubsystemIndicatorShell() {
        const activePanel = document.querySelector(".tab-panel.active");
        if (!(activePanel instanceof HTMLElement)) {
            return null;
        }

        const groupedShell = activePanel.querySelector("[data-subsystem-grouped-shell]");
        if (!(groupedShell instanceof HTMLElement) || groupedShell.hidden) {
            return null;
        }

        return groupedShell;
    }

    function updateSubsystemScrollIndicator() {
        const indicator = document.querySelector("[data-subsystem-scroll-indicator]");
        const bubble = document.querySelector("[data-subsystem-scroll-indicator-bubble]");
        const label = document.querySelector("[data-subsystem-scroll-indicator-label]");

        if (!(indicator instanceof HTMLElement) || !(bubble instanceof HTMLElement) || !(label instanceof HTMLElement)) {
            return;
        }

        if (window.scrollY <= 0) {
            indicator.hidden = true;
            return;
        }

        const groupedShell = resolveActiveSubsystemIndicatorShell();
        if (!(groupedShell instanceof HTMLElement)) {
            indicator.hidden = true;
            return;
        }

        const visibleGroups = Array.from(groupedShell.querySelectorAll("[data-subsystem-group]"))
            .filter((group) => group instanceof HTMLElement && !group.hidden);

        if (visibleGroups.length === 0) {
            indicator.hidden = true;
            return;
        }

        const shellRect = groupedShell.getBoundingClientRect();
        if (shellRect.bottom <= 120 || shellRect.top >= window.innerHeight) {
            indicator.hidden = true;
            return;
        }

        const anchorY = Math.max(132, Math.min(window.innerHeight * 0.35, 220));
        const currentGroup = resolveCurrentSubsystemGroup(visibleGroups, anchorY);
        const subsystemName = currentGroup instanceof HTMLElement
            ? (currentGroup.getAttribute("data-subsystem-name") || "").trim()
            : "";

        if (!subsystemName) {
            indicator.hidden = true;
            return;
        }

        const bubbleTravel = Math.max(0, indicator.clientHeight - bubble.offsetHeight);
        const currentRect = currentGroup.getBoundingClientRect();
        const currentCenter = currentRect.top + (currentRect.height / 2);
        const progress = Math.max(0, Math.min(1, (currentCenter - shellRect.top) / Math.max(shellRect.height, 1)));
        bubble.style.transform = `translateY(${Math.round(progress * bubbleTravel)}px)`;
        label.textContent = subsystemName;
        indicator.hidden = false;
    }

    function scheduleSubsystemIndicatorSync() {
        if (!(document.body instanceof HTMLElement)) {
            return;
        }

        const currentFrame = Number.parseInt(document.body.dataset.subsystemIndicatorFrame || "0", 10);
        if (Number.isInteger(currentFrame) && currentFrame > 0) {
            window.cancelAnimationFrame(currentFrame);
        }

        const nextFrame = window.requestAnimationFrame(() => {
            document.body.dataset.subsystemIndicatorFrame = "0";
            updateSubsystemScrollIndicator();
        });
        document.body.dataset.subsystemIndicatorFrame = String(nextFrame);
    }

    function initSubsystemScrollIndicator() {
        const indicator = document.querySelector("[data-subsystem-scroll-indicator]");
        if (!(indicator instanceof HTMLElement) || !(document.body instanceof HTMLElement)) {
            return;
        }

        if (document.body.dataset.subsystemIndicatorReady !== "true") {
            document.body.dataset.subsystemIndicatorReady = "true";
            window.addEventListener("scroll", scheduleSubsystemIndicatorSync, { passive: true });
            window.addEventListener("resize", scheduleSubsystemIndicatorSync);
        }

        scheduleSubsystemIndicatorSync();
    }

    function initScheduleExpandUi(scope) {
        const root = scope instanceof Element ? scope : document;
        root.querySelectorAll("[data-schedule-expand-toggle]").forEach((button) => {
            if (!(button instanceof HTMLButtonElement) || button.dataset.scheduleExpandReady === "true") {
                return;
            }

            button.dataset.scheduleExpandReady = "true";
            button.addEventListener("click", () => {
                const recordId = String(button.dataset.scheduleRecordId || "").trim();
                if (!recordId) {
                    return;
                }

                const details = root.querySelector(`[data-schedule-steps][data-schedule-record-id="${CSS.escape(recordId)}"]`);
                if (!(details instanceof HTMLElement)) {
                    return;
                }

                const expanded = details.hidden;
                details.hidden = !expanded;
                button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
                button.setAttribute("aria-expanded", String(expanded));

                if (expanded) {
                    renderStaticTimelineAxes(details);
                    window.requestAnimationFrame(() => {
                        renderStaticTimelineAxes(details);
                    });
                }
            });
        });
    }

    function setScheduleFilterPanelOpen(open) {
        const panel = document.querySelector("[data-schedule-filter-panel]");
        const toggle = document.querySelector("[data-schedule-filter-toggle]");
        const key = "pmtracker.schedule.filters.open";
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        panel.classList.toggle("collapsed", !open);
        if (toggle instanceof HTMLElement) {
            toggle.setAttribute("aria-expanded", String(open));
        }

        localStorage.setItem(key, String(open));
    }

    function getScheduleFilterValue(key) {
        const input = getProjectFilterInput("schedule", key);
        if (input instanceof HTMLInputElement && input.type === "checkbox") {
            return input.checked;
        }
        if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
            return input.value;
        }
        return "";
    }

    function restoreScheduleFilterState() {
        return restoreProjectFilterScope("schedule");
    }

    function persistScheduleFilterState(input) {
        handleProjectFilterInputChange("schedule");
    }

    function applyProjectScheduleFilters() {
        const cards = document.querySelectorAll("[data-schedule-item]");
        if (cards.length === 0) {
            return;
        }

        const state = buildProjectFilterStateFromInputs("schedule");
        const filters = {
            subsystem: normalizeFilterToken(state.subsystem)
        };

        cards.forEach((item) => {
            if (!(item instanceof HTMLElement)) {
                return;
            }

            const subsystem = normalizeFilterToken(item.dataset.scheduleFilterSubsystemKod || item.dataset.scheduleFilterSubsystem);
            const matches = !filters.subsystem || subsystem === filters.subsystem;

            setRecordFilterVisibility(item, matches);
        });

        document.querySelectorAll("[data-project-schedule-list] [data-subsystem-group]").forEach((group) => {
            if (!(group instanceof HTMLElement)) {
                return;
            }

            const hasVisibleItems = Array.from(group.querySelectorAll("[data-schedule-item]"))
                .some((item) => item instanceof HTMLElement && !item.hidden);
            group.hidden = !hasVisibleItems;
        });

        renderStaticTimelineAxes(document.querySelector('[data-tab-panel="harmonogram"]'));
        queueRainbowSegmentRender(document.querySelector('[data-tab-panel="harmonogram"]'));
        scheduleSubsystemIndicatorSync();
    }

    function setGanttFilterPanelOpen(open) {
        const panel = document.querySelector("[data-gantt-filter-panel]");
        const toggle = document.querySelector("[data-gantt-filter-toggle]");
        const key = "pmtracker.gantt.filters.open";
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        panel.classList.toggle("collapsed", !open);
        if (toggle instanceof HTMLElement) {
            toggle.setAttribute("aria-expanded", String(open));
        }

        localStorage.setItem(key, String(open));
    }

    function getGanttFilterValue(key) {
        const input = getProjectFilterInput("gantt", key);
        if (input instanceof HTMLInputElement && input.type === "checkbox") {
            return input.checked;
        }
        if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
            return input.value;
        }
        return "";
    }

    function restoreGanttFilterState() {
        return restoreProjectFilterScope("gantt");
    }

    function persistGanttFilterState(input) {
        handleProjectFilterInputChange("gantt");
    }

    class ProjectGanttBoard {
        constructor(panel) {
            this.panel = panel;
            this.projectId = String(panel.dataset.projectId || "0");
            this.pickerItems = Array.from(panel.querySelectorAll("[data-gantt-picker-item]"))
                .filter((node) => node instanceof HTMLElement);
            this.boardItems = Array.from(panel.querySelectorAll("[data-gantt-item]"))
                .filter((node) => node instanceof HTMLElement);
            this.pinInputs = Array.from(panel.querySelectorAll("[data-gantt-pin-input]"))
                .filter((node) => node instanceof HTMLInputElement);
            this.pinnedKey = `pmtracker.gantt.pinned.${this.projectId}`;
            this.expandedKey = `pmtracker.gantt.expanded.${this.projectId}`;

            const fallbackPinned = this.pinInputs
                .map((input) => String(input.dataset.ganttRecordId || "").trim())
                .filter(Boolean);
            this.pinnedIds = this.readIdSet(this.pinnedKey, fallbackPinned);
            this.expandedIds = this.readIdSet(this.expandedKey, []);
        }

        readIdSet(storageKey, fallbackValues) {
            const raw = localStorage.getItem(storageKey);
            if (!raw) {
                return new Set(fallbackValues);
            }
            try {
                const parsed = JSON.parse(raw);
                if (!Array.isArray(parsed)) {
                    return new Set(fallbackValues);
                }
                return new Set(parsed.map((value) => String(value || "").trim()).filter(Boolean));
            } catch (error) {
                return new Set(fallbackValues);
            }
        }

        writeIdSet(storageKey, set) {
            localStorage.setItem(storageKey, JSON.stringify(Array.from(set)));
        }

        getFilters() {
            const currentUserId = getProjectFilterCurrentUserId("gantt");
            const hasCurrentUser = currentUserId && currentUserId !== "0";
            return {
                subsystem: normalizeFilterToken(getGanttFilterValue("subsystem")),
                kategorie: normalizeFilterToken(getGanttFilterValue("kategorie")),
                stav: normalizeFilterToken(getGanttFilterValue("stav")),
                typ: normalizeFilterToken(getGanttFilterValue("typ")),
                vlastnik: normalizeFilterToken(getGanttFilterValue("vlastnik")),
                onlyActive: Boolean(getGanttFilterValue("aktivni")),
                mine: Boolean(getGanttFilterValue("mine")),
                currentUserId,
                hasCurrentUser,
                stihani: normalizeFilterToken(getGanttFilterValue("stihani"))
            };
        }

        matchesFilters(node, filters) {
            if (!(node instanceof HTMLElement)) {
                return false;
            }

            const subsystem = normalizeFilterToken(node.dataset.ganttFilterSubsystemKod || node.dataset.ganttFilterSubsystem);
            const kategorie = normalizeFilterToken(node.dataset.ganttFilterKategorieKod || node.dataset.ganttFilterKategorie);
            const stav = normalizeFilterToken(node.dataset.ganttFilterStavKod || node.dataset.ganttFilterStav);
            const typ = normalizeFilterToken(node.dataset.ganttFilterTypKod || node.dataset.ganttFilterTyp);
            const vlastnik = normalizeFilterToken(node.dataset.ganttFilterVlastnikId || node.dataset.ganttFilterVlastnik);
            const isActive = node.dataset.ganttFilterAktivni === "true";
            const stihani = normalizeFilterToken(node.dataset.ganttFilterStihani);
            const matchesMine = !filters.mine || (filters.hasCurrentUser && vlastnik === filters.currentUserId);

            return (!filters.subsystem || subsystem === filters.subsystem)
                && (!filters.kategorie || kategorie === filters.kategorie)
                && (!filters.stav || stav === filters.stav)
                && (!filters.typ || typ === filters.typ)
                && (!filters.vlastnik || vlastnik === filters.vlastnik)
                && (!filters.onlyActive || isActive)
                && matchesMine
                && (!filters.stihani || stihani === filters.stihani);
        }

        setExpanded(recordId, expanded) {
            const normalized = String(recordId || "").trim();
            if (!normalized) {
                return;
            }

            if (expanded) {
                this.expandedIds.add(normalized);
            } else {
                this.expandedIds.delete(normalized);
            }
            this.writeIdSet(this.expandedKey, this.expandedIds);
            this.syncExpandedState();
        }

        setPinned(recordId, pinned) {
            const normalized = String(recordId || "").trim();
            if (!normalized) {
                return;
            }

            if (pinned) {
                this.pinnedIds.add(normalized);
            } else {
                this.pinnedIds.delete(normalized);
                this.expandedIds.delete(normalized);
                this.writeIdSet(this.expandedKey, this.expandedIds);
            }

            this.writeIdSet(this.pinnedKey, this.pinnedIds);
            this.apply();
        }

        syncPinnedInputs() {
            this.pinInputs.forEach((input) => {
                const recordId = String(input.dataset.ganttRecordId || "").trim();
                input.checked = this.pinnedIds.has(recordId);
            });
        }

        syncExpandedState() {
            this.boardItems.forEach((item) => {
                if (!(item instanceof HTMLElement)) {
                    return;
                }

                const recordId = String(item.dataset.ganttRecordId || "").trim();
                const expanded = this.expandedIds.has(recordId);
                const details = item.querySelector("[data-gantt-steps]");
                if (details instanceof HTMLElement) {
                    details.hidden = !expanded;
                }
                const button = item.querySelector("[data-gantt-expand-toggle]");
                if (button instanceof HTMLButtonElement) {
                    button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
                    button.setAttribute("aria-expanded", String(expanded));
                }
            });
        }

        apply() {
            const filters = this.getFilters();

            this.pickerItems.forEach((item) => {
                const visible = this.matchesFilters(item, filters);
                setRecordFilterVisibility(item, visible);
            });

            this.boardItems.forEach((item) => {
                if (!(item instanceof HTMLElement)) {
                    return;
                }
                const recordId = String(item.dataset.ganttRecordId || "").trim();
                const visibleByFilter = this.matchesFilters(item, filters);
                const visible = visibleByFilter && this.pinnedIds.has(recordId);
                setRecordFilterVisibility(item, visible);
                item.dataset.ganttPinned = visible ? "true" : "false";
            });

            this.syncPinnedInputs();
            this.syncExpandedState();
            updateProjectGanttAxis(this.panel);
            queueRainbowSegmentRender(this.panel);
        }

        bind() {
            this.pinInputs.forEach((input) => {
                input.addEventListener("change", () => {
                    this.setPinned(input.dataset.ganttRecordId, input.checked);
                });
            });

            this.panel.querySelectorAll("[data-gantt-expand-toggle]").forEach((button) => {
                if (!(button instanceof HTMLButtonElement)) {
                    return;
                }
                button.addEventListener("click", () => {
                    const recordId = String(button.dataset.ganttRecordId || "").trim();
                    const expanded = this.expandedIds.has(recordId);
                    this.setExpanded(recordId, !expanded);
                });
            });
        }
    }

    function applyProjectGanttFilters() {
        const panel = document.querySelector("[data-gantt-panel]");
        if (!(panel instanceof HTMLElement) || !(panel._ganttBoard instanceof ProjectGanttBoard)) {
            return;
        }
        panel._ganttBoard.apply();
    }

    function updateProjectGanttAxis(panel) {
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const axis = panel.querySelector("[data-gantt-axis]");
        if (!(axis instanceof HTMLElement)) {
            return;
        }

        const visibleItems = Array.from(panel.querySelectorAll("[data-gantt-item]"))
            .filter((node) => node instanceof HTMLElement && !node.hidden);
        if (visibleItems.length === 0) {
            axis.hidden = true;
            axis.replaceChildren();
            return;
        }

        const dates = visibleItems
            .flatMap((item) => {
                const startDate = parseIsoDate(item.dataset.ganttAxisStart);
                const endDate = parseIsoDate(item.dataset.ganttAxisEnd);
                return [startDate, endDate];
            })
            .filter((value) => value instanceof Date);
        if (dates.length === 0) {
            axis.hidden = true;
            axis.replaceChildren();
            return;
        }

        const ordered = dates.slice().sort((a, b) => a.getTime() - b.getTime());
        axis.hidden = false;
        renderTimelineAxis(axis, ordered[0], ordered[ordered.length - 1]);
    }

    function initProjectScheduleUi() {
        const schedulePanel = document.querySelector('[data-tab-panel="harmonogram"]');
        if (!(schedulePanel instanceof HTMLElement)) {
            return;
        }

        const filterPanel = document.querySelector("[data-schedule-filter-panel]");
        if (filterPanel instanceof HTMLElement) {
            const storedOpen = localStorage.getItem("pmtracker.schedule.filters.open");
            setScheduleFilterPanelOpen(storedOpen === "true");
        }

        restoreScheduleFilterState();
        setProjectFilterSaveStatus("schedule", "");
        applyProjectScheduleFilters();
        renderStaticTimelineAxes(schedulePanel);
        queueRainbowSegmentRender(schedulePanel);
        initScheduleExpandUi(schedulePanel);
    }

    function setActiveTab(tabName) {
        if (!tabName) {
            return;
        }
        if (tabName === "gant") {
            tabName = "harmonogram";
        }
        const tabs = document.querySelectorAll(".tab");
        const panels = document.querySelectorAll(".tab-panel");
        let activePanel = null;
        tabs.forEach((item) => {
            item.classList.toggle("active", item.getAttribute("data-tab") === tabName);
        });
        panels.forEach((panel) => {
            const isActive = panel.getAttribute("data-tab-panel") === tabName;
            panel.classList.toggle("active", isActive);
            if (isActive) {
                activePanel = panel;
            }
        });
        localStorage.setItem("pmtracker.tab.active", tabName);
        if (activePanel instanceof HTMLElement) {
            if (tabName === "harmonogram") {
                renderStaticTimelineAxes(activePanel);
            }
            queueRainbowSegmentRender(activePanel);
        }
        scheduleSubsystemIndicatorSync();
    }

    function syncTabQuery(tabName) {
        if (!tabName) {
            return;
        }
        if (tabName === "gant") {
            tabName = "harmonogram";
        }

        const url = new URL(window.location.href);
        url.searchParams.set("tab", tabName);
        history.replaceState(history.state, "", `${url.pathname}${url.search}${url.hash}`);
    }

    function initCiselnikAjaxSwitch() {
        const shell = document.querySelector("[data-ciselnik-shell]");
        if (!(shell instanceof HTMLElement)) {
            return;
        }

        const panel = shell.querySelector("[data-ciselnik-panel]");
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const contentUrl = shell.getAttribute("data-content-url");
        if (!contentUrl) {
            return;
        }

        const getCurrentKeyFromUrl = () => {
            const url = new URL(window.location.href);
            return url.searchParams.get("id");
        };

        const setActiveLink = (key) => {
            shell.querySelectorAll("[data-ciselnik-link]").forEach((link) => {
                if (!(link instanceof HTMLAnchorElement)) {
                    return;
                }
                const isActive = link.dataset.key === key;
                link.classList.toggle("active", isActive);
                if (isActive) {
                    link.setAttribute("aria-current", "page");
                } else {
                    link.removeAttribute("aria-current");
                }
            });
        };

        const loadDetail = async (key, push, href) => {
            if (!key) {
                return;
            }

            panel.setAttribute("aria-busy", "true");

            try {
                const endpoint = `${contentUrl}?id=${encodeURIComponent(key)}`;
                const response = await fetch(endpoint, {
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const html = await response.text();
                panel.innerHTML = html;
                initRecordFormEnhancements(panel);
                setActiveLink(key);

                if (push && href) {
                    history.pushState(
                        { ...(history.state || {}), ciselnikKey: key },
                        "",
                        href
                    );
                } else if (!push) {
                    history.replaceState(
                        { ...(history.state || {}), ciselnikKey: key },
                        "",
                        window.location.href
                    );
                }
            } catch (error) {
                if (href) {
                    window.location.href = href;
                }
            } finally {
                panel.setAttribute("aria-busy", "false");
            }
        };

        const initialKey =
            shell.querySelector("[data-ciselnik-link].active") instanceof HTMLAnchorElement
                ? shell.querySelector("[data-ciselnik-link].active").dataset.key
                : getCurrentKeyFromUrl();

        if (initialKey) {
            history.replaceState(
                { ...(history.state || {}), ciselnikKey: initialKey },
                "",
                window.location.href
            );
            setActiveLink(initialKey);
        }

        shell.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const link = target.closest("[data-ciselnik-link]");
            if (!(link instanceof HTMLAnchorElement)) {
                return;
            }

            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) {
                return;
            }

            event.preventDefault();
            const key = link.dataset.key;
            loadDetail(key, true, link.href);
        });

        window.addEventListener("popstate", (event) => {
            const key = event.state?.ciselnikKey || getCurrentKeyFromUrl();
            const href =
                shell.querySelector(`[data-ciselnik-link][data-key="${key}"]`) instanceof HTMLAnchorElement
                    ? shell.querySelector(`[data-ciselnik-link][data-key="${key}"]`).href
                    : null;
            if (key) {
                loadDetail(key, false, href);
            }
        });
    }

    function initSettingsAjaxSwitch() {
        const shell = document.querySelector("[data-settings-shell]");
        if (!(shell instanceof HTMLElement)) {
            return;
        }

        const panel = shell.querySelector("[data-settings-panel]");
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const panelUrl = shell.getAttribute("data-panel-url");
        if (!panelUrl) {
            return;
        }

        const getCurrentStateFromUrl = () => {
            const url = new URL(window.location.href);
            return {
                section: url.searchParams.get("section") || "role",
                userId: url.searchParams.get("userId"),
                projektId: url.searchParams.get("projektId")
            };
        };

        const setActiveLink = (section) => {
            shell.querySelectorAll("[data-settings-link]").forEach((link) => {
                if (!(link instanceof HTMLAnchorElement)) {
                    return;
                }
                const isActive = link.dataset.key === section;
                link.classList.toggle("active", isActive);
                if (isActive) {
                    link.setAttribute("aria-current", "page");
                } else {
                    link.removeAttribute("aria-current");
                }
            });
        };

        const buildHref = (section, userId, projektId) => {
            const url = new URL(window.location.href);
            url.searchParams.set("section", section);

            if (userId) {
                url.searchParams.set("userId", userId);
            } else {
                url.searchParams.delete("userId");
            }

            if (projektId) {
                url.searchParams.set("projektId", projektId);
            } else {
                url.searchParams.delete("projektId");
            }

            return `${url.pathname}${url.search}${url.hash}`;
        };

        const loadSection = async (section, push, options) => {
            if (!section) {
                return;
            }
            const userId = options?.userId ?? null;
            const projektId = options?.projektId ?? null;
            const fallbackHref = options?.href ?? null;

            panel.setAttribute("aria-busy", "true");

            try {
                const endpointUrl = new URL(panelUrl, window.location.origin);
                endpointUrl.searchParams.set("section", section);
                if (userId) {
                    endpointUrl.searchParams.set("userId", userId);
                }
                if (projektId) {
                    endpointUrl.searchParams.set("projektId", projektId);
                }

                const endpoint = `${endpointUrl.pathname}${endpointUrl.search}`;
                const response = await fetch(endpoint, { headers: { "X-Requested-With": "XMLHttpRequest" } });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }
                const html = await response.text();
                panel.innerHTML = html;
                setActiveLink(section);

                const nextHref = buildHref(section, userId, projektId);
                const nextState = {
                    ...(history.state || {}),
                    settingsSection: section,
                    settingsUserId: userId,
                    settingsProjektId: projektId
                };

                if (push) {
                    history.pushState(nextState, "", nextHref);
                } else if (!push) {
                    history.replaceState(nextState, "", nextHref);
                }
            } catch (error) {
                if (fallbackHref) {
                    window.location.href = fallbackHref;
                }
            } finally {
                panel.setAttribute("aria-busy", "false");
            }
        };

        const initialSection =
            shell.querySelector("[data-settings-link].active") instanceof HTMLAnchorElement
                ? shell.querySelector("[data-settings-link].active").dataset.key
                : getCurrentStateFromUrl().section;

        const initialStateFromUrl = getCurrentStateFromUrl();
        history.replaceState(
            {
                ...(history.state || {}),
                settingsSection: initialSection || "role",
                settingsUserId: initialStateFromUrl.userId,
                settingsProjektId: initialStateFromUrl.projektId
            },
            "",
            window.location.href
        );

        shell.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            const link = target.closest("[data-settings-link]");
            if (!(link instanceof HTMLAnchorElement)) {
                return;
            }
            if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) {
                return;
            }

            event.preventDefault();
            const section = link.dataset.key;
            const linkUrl = new URL(link.href);
            const userId = linkUrl.searchParams.get("userId");
            const projektId = linkUrl.searchParams.get("projektId");
            loadSection(section, true, { href: link.href, userId, projektId });
        });

        shell.addEventListener("change", (event) => {
            const target = event.target;
            if (!(target instanceof Element)) {
                return;
            }

            if (!target.matches("[data-settings-filter-user], [data-settings-filter-project]")) {
                return;
            }

            const form = target.closest("[data-settings-filter-form]");
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            const sectionInput = form.querySelector('input[name="section"]');
            const userSelect = form.querySelector('[data-settings-filter-user]');
            const projectSelect = form.querySelector('[data-settings-filter-project]');

            const section = sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava";
            const userId = userSelect instanceof HTMLSelectElement ? userSelect.value : null;
            const projektId = projectSelect instanceof HTMLSelectElement ? projectSelect.value : null;

            loadSection(section, true, { userId, projektId });
        });

        shell.addEventListener("submit", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLFormElement) || !target.matches("[data-settings-filter-form]")) {
                return;
            }

            event.preventDefault();
            const sectionInput = target.querySelector('input[name="section"]');
            const userSelect = target.querySelector('[data-settings-filter-user]');
            const projectSelect = target.querySelector('[data-settings-filter-project]');
            const section = sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava";
            const userId = userSelect instanceof HTMLSelectElement ? userSelect.value : null;
            const projektId = projectSelect instanceof HTMLSelectElement ? projectSelect.value : null;
            loadSection(section, true, { userId, projektId });
        });

        window.addEventListener("popstate", (event) => {
            const stateFromUrl = getCurrentStateFromUrl();
            const section = event.state?.settingsSection || stateFromUrl.section || "role";
            const userId = event.state?.settingsUserId ?? stateFromUrl.userId;
            const projektId = event.state?.settingsProjektId ?? stateFromUrl.projektId;
            const href =
                shell.querySelector(`[data-settings-link][data-key="${section}"]`) instanceof HTMLAnchorElement
                    ? shell.querySelector(`[data-settings-link][data-key="${section}"]`).href
                    : null;
            loadSection(section, false, { href, userId, projektId });
        });
    }

    function initProfileRightsFilter() {
        const form = document.querySelector("[data-profile-rights-form]");
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const projectSelect = form.querySelector("[data-profile-rights-project]");
        if (!(projectSelect instanceof HTMLSelectElement)) {
            return;
        }

        projectSelect.addEventListener("change", () => {
            const url = new URL(window.location.href);
            if (projectSelect.value) {
                url.searchParams.set("projektId", projectSelect.value);
            } else {
                url.searchParams.delete("projektId");
            }
            url.hash = "moje-prava";
            window.location.href = url.toString();
        });
    }

    function initPermissionMetadataBindings(scope) {
        const root = scope instanceof Element ? scope : document;
        const forms = root.querySelectorAll("form");
        forms.forEach((form) => {
            if (!(form instanceof HTMLFormElement)) {
                return;
            }

            const keySelect = form.querySelector("[data-authz-permission-key]");
            const categorySelect = form.querySelector("[data-authz-permission-category]");
            const scopeSelect = form.querySelector("[data-authz-permission-scope]");
            const help = form.querySelector("[data-authz-permission-help]");

            if (!(keySelect instanceof HTMLSelectElement) ||
                !(categorySelect instanceof HTMLSelectElement) ||
                !(scopeSelect instanceof HTMLSelectElement)) {
                return;
            }

            if (keySelect.dataset.authzPermissionBound === "true") {
                return;
            }
            keySelect.dataset.authzPermissionBound = "true";

            const applyCatalogMetadata = () => {
                const selectedOption = keySelect.selectedOptions.length > 0
                    ? keySelect.selectedOptions[0]
                    : null;
                if (!(selectedOption instanceof HTMLOptionElement)) {
                    return;
                }

                const categoryId = (selectedOption.dataset.categoryId || "").trim();
                const categoryKod = (selectedOption.dataset.categoryKod || "").trim();
                const scopeLevel = (selectedOption.dataset.scopeLevel || "").trim().toUpperCase();
                const description = (selectedOption.dataset.description || "").trim();

                if (categoryId) {
                    categorySelect.value = categoryId;
                }
                if (scopeLevel === "GLOBAL" || scopeLevel === "PROJECT") {
                    scopeSelect.value = scopeLevel;
                }

                if (help instanceof HTMLElement) {
                    if (description || categoryKod || scopeLevel) {
                        const fragments = [];
                        if (description) {
                            fragments.push(description);
                        }
                        if (categoryKod) {
                            fragments.push(`Kategorie: ${categoryKod}`);
                        }
                        if (scopeLevel) {
                            fragments.push(`Rozsah: ${scopeLevel}`);
                        }
                        help.textContent = fragments.join(" | ");
                    } else {
                        help.textContent = "";
                    }
                }
            };

            keySelect.addEventListener("change", applyCatalogMetadata);
            categorySelect.addEventListener("change", applyCatalogMetadata);
            scopeSelect.addEventListener("change", applyCatalogMetadata);
            applyCatalogMetadata();
        });
    }

    function updateTaskTypeVisibility(categorySelect) {
        if (!(categorySelect instanceof HTMLSelectElement)) {
            return;
        }

        const form = categorySelect.closest("form");
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const topRow = form.querySelector("[data-record-row-top]");
        const typeRow = form.querySelector("[data-typ-ukolu-row]");
        const selectedLabel = categorySelect.selectedIndex >= 0
            ? (categorySelect.options[categorySelect.selectedIndex]?.textContent || "")
            : "";
        const normalized = `${categorySelect.value || ""} ${selectedLabel}`.toLowerCase();
        const isTask = normalized.includes("úkol") || normalized.includes("ukol");
        const scheduleTab = form.querySelector("[data-record-schedule-tab]");
        const schedulePanel = form.querySelector("[data-record-schedule-panel]");
        const scheduleNote = form.querySelector("[data-record-schedule-note]");

        if (topRow instanceof HTMLElement) {
            topRow.dataset.hasType = isTask ? "true" : "false";
        }

        if (!(typeRow instanceof HTMLElement)) {
            return;
        }

        const typeSelect = typeRow.querySelector("select");
        typeRow.hidden = !isTask;
        if (typeSelect instanceof HTMLSelectElement) {
            typeSelect.disabled = !isTask;
            if (!isTask) {
                typeSelect.value = "";
            }
        }

        if (scheduleTab instanceof HTMLElement) {
            scheduleTab.hidden = !isTask;
        }

        if (scheduleNote instanceof HTMLElement) {
            scheduleNote.hidden = isTask;
        }

        if (schedulePanel instanceof HTMLElement) {
            const schedulePermissionMode = (form.dataset.schedulePermissionMode || "full").toLowerCase();
            const canScheduleEditFull = schedulePermissionMode === "full";
            const canScheduleAddOnly = schedulePermissionMode === "add";
            const canScheduleAny = canScheduleEditFull || canScheduleAddOnly;
            const canEditDurationInput = (input) => {
                if (!(input instanceof HTMLInputElement)) {
                    return false;
                }

                if (input.dataset.scheduleStaticDisabled === "true") {
                    return false;
                }

                if (canScheduleEditFull) {
                    return true;
                }

                if (!canScheduleAddOnly) {
                    return false;
                }

                const originalDuration = Number.parseInt((input.dataset.scheduleOriginalDuration || "").trim(), 10);
                return !Number.isFinite(originalDuration) || originalDuration <= 0;
            };
            const syncStepperButtons = (buttonSelector, inputSelector) => {
                form.querySelectorAll(buttonSelector)
                    .forEach((button) => {
                        if (!(button instanceof HTMLButtonElement)) {
                            return;
                        }

                        const row = button.closest("[data-schedule-step-row]");
                        const input = row?.querySelector(inputSelector);
                        button.disabled = !(input instanceof HTMLInputElement) || input.disabled;
                    });
            };

            const setScheduleDateFieldState = () => {
                form.querySelectorAll(".schedule-date-field[data-app-date-field]").forEach((dateField) => {
                    if (!(dateField instanceof HTMLElement)) {
                        return;
                    }

                    const valueInput = dateField.querySelector("[data-schedule-date], [data-schedule-delay-date]");
                    const staticDisabled = valueInput instanceof HTMLInputElement
                        && valueInput.dataset.scheduleStaticDisabled === "true";
                    const isDelayDate = valueInput instanceof HTMLInputElement
                        && valueInput.hasAttribute("data-schedule-delay-date");
                    const row = dateField.closest("[data-schedule-step-row]");
                    const linkedInput = row?.querySelector(isDelayDate ? "[data-schedule-delay]" : "[data-schedule-duration]");
                    const linkedLocked = linkedInput instanceof HTMLInputElement ? linkedInput.disabled : true;
                    const shouldDisable = !canScheduleAny || staticDisabled || linkedLocked;
                    dateField.dataset.appDateLocked = shouldDisable ? "true" : "false";

                    if (valueInput instanceof HTMLInputElement) {
                        valueInput.disabled = shouldDisable;
                    }

                    const trigger = dateField.querySelector("[data-app-date-open]");
                    if (trigger instanceof HTMLButtonElement) {
                        trigger.disabled = shouldDisable;
                    }
                });
            };

            if (!isTask) {
                schedulePanel.hidden = true;
                schedulePanel.setAttribute("data-schedule-disabled", "true");
                form.querySelectorAll("[data-schedule-duration], [data-schedule-delay]")
                    .forEach((input) => {
                        if (input instanceof HTMLInputElement) {
                            input.disabled = true;
                        }
                    });
                form.querySelectorAll("[data-schedule-duration-inc], [data-schedule-duration-dec], [data-schedule-delay-inc], [data-schedule-delay-dec]")
                    .forEach((button) => {
                        if (button instanceof HTMLButtonElement) {
                            button.disabled = true;
                        }
                    });
                setScheduleDateFieldState();
                setRecordFormTab(form, "basic");
            } else {
                schedulePanel.removeAttribute("data-schedule-disabled");
                form.querySelectorAll("[data-schedule-duration]")
                    .forEach((input) => {
                        if (input instanceof HTMLInputElement) {
                            input.disabled = !canEditDurationInput(input);
                        }
                    });
                form.querySelectorAll("[data-schedule-delay]")
                    .forEach((input) => {
                        if (input instanceof HTMLInputElement) {
                            const staticDisabled = input.dataset.scheduleStaticDisabled === "true";
                            input.disabled = !canScheduleAny || staticDisabled;
                        }
                    });
                syncStepperButtons("[data-schedule-duration-inc], [data-schedule-duration-dec]", "[data-schedule-duration]");
                syncStepperButtons("[data-schedule-delay-inc], [data-schedule-delay-dec]", "[data-schedule-delay]");
                setScheduleDateFieldState();

                if (!form._recordSchedulePlanner && canScheduleAny) {
                    initRecordSchedulePlanner(form);
                }
            }
        }

        if (isTask && form._recordSchedulePlanner && typeof form._recordSchedulePlanner.recalcAll === "function") {
            form._recordSchedulePlanner.recalcAll();
        }
    }

    function setRecordFormTab(form, tabKey) {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const tabs = form.querySelectorAll("[data-record-modal-tab]");
        const panels = form.querySelectorAll("[data-record-modal-panel]");
        if (tabs.length === 0 || panels.length === 0) {
            return;
        }

        const requestedTab = typeof tabKey === "string" ? tabKey : "basic";
        const requestedButton = form.querySelector(`[data-record-modal-tab="${requestedTab}"]`);
        const normalizedTab = requestedButton instanceof HTMLElement && !requestedButton.hidden
            ? requestedTab
            : "basic";

        tabs.forEach((tab) => {
            if (!(tab instanceof HTMLElement)) {
                return;
            }

            tab.classList.toggle("active", tab.dataset.recordModalTab === normalizedTab);
        });

        panels.forEach((panel) => {
            if (!(panel instanceof HTMLElement)) {
                return;
            }

            const active = panel.dataset.recordModalPanel === normalizedTab;
            panel.hidden = !active;
            panel.classList.toggle("active", active);
        });

        const activeTabInput = form.querySelector("[data-record-active-tab-input]");
        if (activeTabInput instanceof HTMLInputElement) {
            activeTabInput.value = normalizedTab;
        }

        if (normalizedTab === "schedule") {
            if (!form._recordSchedulePlanner) {
                initRecordSchedulePlanner(form);
            }

            if (form._recordSchedulePlanner && typeof form._recordSchedulePlanner.recalcAll === "function") {
                form._recordSchedulePlanner.recalcAll();
            }

            queueRecordSchedulePlannerRecalc(form, 0);

            const harmonogramPanel = form.querySelector('[data-record-modal-panel="schedule"]');
            if (harmonogramPanel instanceof HTMLElement) {
                queueRainbowSegmentRender(harmonogramPanel);
            }
        }
    }

    const dateMonths = [
        "Leden", "Únor", "Březen", "Duben", "Květen", "Červen",
        "Červenec", "Srpen", "Září", "Říjen", "Listopad", "Prosinec"
    ];

    function parseIsoDate(value) {
        if (!value) {
            return null;
        }

        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value.trim());
        if (!match) {
            return null;
        }

        const year = Number.parseInt(match[1], 10);
        const month = Number.parseInt(match[2], 10);
        const day = Number.parseInt(match[3], 10);
        if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
            return null;
        }

        const date = new Date(year, month - 1, day);
        if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
            return null;
        }

        return date;
    }

    function parseDisplayDate(value) {
        if (!value) {
            return null;
        }

        const match = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(value.trim());
        if (!match) {
            return null;
        }

        const day = Number.parseInt(match[1], 10);
        const month = Number.parseInt(match[2], 10);
        const year = Number.parseInt(match[3], 10);
        if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
            return null;
        }

        const date = new Date(year, month - 1, day);
        if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
            return null;
        }

        return date;
    }

    function formatIsoDate(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const day = String(date.getDate()).padStart(2, "0");
        return `${year}-${month}-${day}`;
    }

    function formatDisplayDate(date) {
        const day = String(date.getDate()).padStart(2, "0");
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const year = date.getFullYear();
        return `${day}.${month}.${year}`;
    }

    const msPerDay = 24 * 60 * 60 * 1000;

    function toUtcDayStamp(date) {
        return Date.UTC(date.getFullYear(), date.getMonth(), date.getDate());
    }

    function diffCalendarDays(a, b) {
        return Math.round((toUtcDayStamp(a) - toUtcDayStamp(b)) / msPerDay);
    }

    function addCalendarDays(baseDate, dayCount) {
        const days = Number.isFinite(dayCount) ? Math.trunc(dayCount) : 0;
        const next = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate());
        next.setDate(next.getDate() + days);
        return next;
    }

    function formatAxisDayMonth(date) {
        const day = String(date.getDate()).padStart(2, "0");
        const month = String(date.getMonth() + 1).padStart(2, "0");
        return `${day}.${month}.`;
    }

    function formatAxisMonthYear(date) {
        return new Intl.DateTimeFormat("cs-CZ", { month: "short", year: "numeric" }).format(date);
    }

    function resolveTimelineAxisTickTargetCount(containerWidth) {
        if (!Number.isFinite(containerWidth) || containerWidth <= 0) {
            return 2;
        }

        if (containerWidth < 320) {
            return 2;
        }

        const estimated = Math.round(containerWidth / 120);
        return Math.max(5, Math.min(10, estimated));
    }

    function queueTimelineAxisRetry(container, startDate, endDate, attempt) {
        if (!(container instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
            return;
        }

        const retryAttempt = Number.isFinite(attempt) ? Math.trunc(attempt) : 0;
        if (retryAttempt >= 10 || !container.isConnected) {
            return;
        }

        const pendingFrame = Number.parseInt(container.dataset.axisRetryFrame || "0", 10);
        if (Number.isInteger(pendingFrame) && pendingFrame > 0) {
            window.cancelAnimationFrame(pendingFrame);
        }

        const frameId = window.requestAnimationFrame(() => {
            container.dataset.axisRetryFrame = "0";
            renderTimelineAxis(container, startDate, endDate, {
                retryAttempt: retryAttempt + 1
            });
        });
        container.dataset.axisRetryFrame = String(frameId);
    }

    function buildTimelineAxisTicks(startDate, endDate, desiredTickCount) {
        const start = new Date(startDate.getFullYear(), startDate.getMonth(), startDate.getDate());
        const end = new Date(endDate.getFullYear(), endDate.getMonth(), endDate.getDate());
        const totalDays = Math.max(1, diffCalendarDays(end, start));
        const maxDistinctTicks = totalDays + 1;
        const requestedTicks = Number.isFinite(desiredTickCount) ? Math.trunc(desiredTickCount) : 7;
        const tickCount = Math.max(2, Math.min(maxDistinctTicks, requestedTicks));
        const useMonthYearLabels = totalDays > 120;
        const formatTickLabel = useMonthYearLabels ? formatAxisMonthYear : formatAxisDayMonth;
        const selectedOffsets = new Set([0, totalDays]);

        for (let index = 1; index < tickCount - 1; index += 1) {
            const offset = Math.round((index * totalDays) / (tickCount - 1));
            selectedOffsets.add(Math.max(0, Math.min(totalDays, offset)));
        }

        for (let dayOffset = 1; selectedOffsets.size < tickCount && dayOffset < totalDays; dayOffset += 1) {
            selectedOffsets.add(dayOffset);
        }

        const orderedOffsets = Array.from(selectedOffsets)
            .map((value) => Number.parseInt(String(value), 10))
            .filter((value) => Number.isFinite(value))
            .sort((a, b) => a - b);

        return orderedOffsets.map((dayOffset) => {
            const date = addCalendarDays(start, dayOffset);
            return {
                date,
                label: formatTickLabel(date),
                left: (dayOffset * 100) / totalDays
            };
        });
    }

    function renderTimelineAxis(container, startDate, endDate, options) {
        if (!(container instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
            return;
        }

        const settings = options && typeof options === "object" ? options : {};
        const retryAttempt = Number.isFinite(settings.retryAttempt)
            ? Math.max(0, Math.trunc(settings.retryAttempt))
            : 0;
        const containerWidth = Math.max(0, container.clientWidth);
        if (containerWidth <= 0 || (containerWidth <= 32 && retryAttempt < 10)) {
            queueTimelineAxisRetry(container, startDate, endDate, retryAttempt);
            return;
        }

        const startStamp = toUtcDayStamp(startDate);
        const endStamp = toUtcDayStamp(endDate);
        const axisStart = startStamp <= endStamp ? startDate : endDate;
        const axisEnd = startStamp <= endStamp ? endDate : startDate;
        const totalDays = Math.max(1, diffCalendarDays(axisEnd, axisStart));
        const edgeInsetPx = Math.max(2, Math.min(4, Math.round(containerWidth * 0.006)));
        const usableAxisWidth = Math.max(1, containerWidth - (edgeInsetPx * 2));
        const labelGlobalShiftLeftPx = 14;
        const percentToAxisPx = (percentValue) => {
            const normalized = Math.max(0, Math.min(100, Number.isFinite(percentValue) ? percentValue : 0));
            return edgeInsetPx + ((normalized / 100) * usableAxisWidth);
        };

        const pendingFrame = Number.parseInt(container.dataset.axisRetryFrame || "0", 10);
        if (Number.isInteger(pendingFrame) && pendingFrame > 0) {
            window.cancelAnimationFrame(pendingFrame);
        }
        container.dataset.axisRetryFrame = "0";
        container.replaceChildren();
        const desiredTickCount = resolveTimelineAxisTickTargetCount(containerWidth);
        const ticks = buildTimelineAxisTicks(axisStart, axisEnd, desiredTickCount);
        ticks.forEach((tick, index) => {
            const tickNode = document.createElement("span");
            tickNode.className = "timeline-axis-tick";
            if (index === 0 || index === ticks.length - 1) {
                tickNode.classList.add("edge");
            }
            const tickLeftPx = percentToAxisPx(tick.left);
            tickNode.dataset.axisLeftPx = tickLeftPx.toFixed(4);
            tickNode.style.left = `${tickLeftPx.toFixed(4)}px`;

            const labelNode = document.createElement("span");
            labelNode.className = "timeline-axis-label";
            labelNode.textContent = tick.label;
            tickNode.appendChild(labelNode);
            container.appendChild(tickNode);
        });

        if (ticks.length === 0) {
            return;
        }

        let previousLabelRight = -Infinity;
        const minLabelGap = 6;
        const tickNodes = Array.from(container.querySelectorAll(".timeline-axis-tick"))
            .filter((tickNode) => tickNode instanceof HTMLElement);
        const lastIndex = tickNodes.length - 1;
        const resolveLabelWidth = (labelNode) => {
            if (!(labelNode instanceof HTMLElement)) {
                return 0;
            }
            const measuredLabelWidth = labelNode.offsetWidth;
            const computedStyle = window.getComputedStyle(labelNode);
            const fallbackFontSpec = `${computedStyle.fontWeight} ${computedStyle.fontSize} ${computedStyle.fontFamily}`;
            const fallbackLabelWidth = Math.ceil(measureTextWidth(labelNode.textContent || "", fallbackFontSpec));
            return measuredLabelWidth > 0 ? measuredLabelWidth : fallbackLabelWidth;
        };
        const resolveTickLeftPx = (tickNode) => {
            if (!(tickNode instanceof HTMLElement)) {
                return 0;
            }
            const serializedPx = Number.parseFloat(tickNode.dataset.axisLeftPx || "");
            if (Number.isFinite(serializedPx)) {
                return serializedPx;
            }
            const measuredLeft = Number.parseFloat(tickNode.style.left || "0");
            return Number.isFinite(measuredLeft) ? measuredLeft : 0;
        };
        const placeLabel = (tickNode, labelNode, index, forceVisible) => {
            const tickLeftPx = resolveTickLeftPx(tickNode);
            const labelWidthRaw = resolveLabelWidth(labelNode);
            const labelWidth = Math.max(1, Math.min(containerWidth, labelWidthRaw > 0 ? labelWidthRaw : 1));
            labelNode.style.maxWidth = `${Math.max(1, containerWidth)}px`;

            const sidePadding = 12;
            let desiredLeft = tickLeftPx + sidePadding;
            if (index === lastIndex) {
                desiredLeft = tickLeftPx - labelWidth - sidePadding;
            } else if (index > 0) {
                desiredLeft = tickLeftPx - (labelWidth / 2);
            }

            desiredLeft -= labelGlobalShiftLeftPx;

            const clampedLeft = Math.max(0, Math.min(desiredLeft, Math.max(0, containerWidth - labelWidth)));
            if (!forceVisible && clampedLeft < previousLabelRight + minLabelGap) {
                labelNode.hidden = true;
                return;
            }

            labelNode.hidden = false;
            labelNode.style.left = `${Math.round(clampedLeft - tickLeftPx)}px`;
            previousLabelRight = Math.max(previousLabelRight, clampedLeft + labelWidth);
        };

        tickNodes.forEach((tickNode, index) => {
            if (!(tickNode instanceof HTMLElement)) {
                return;
            }

            const labelNode = tickNode.querySelector(".timeline-axis-label");
            if (!(labelNode instanceof HTMLElement)) {
                return;
            }

            labelNode.hidden = false;
            labelNode.style.left = "4px";
            const forceVisible = index === 0 || index === lastIndex;
            placeLabel(tickNode, labelNode, index, forceVisible);
        });

        const resolveTickLabelNode = (tickNode) => tickNode instanceof HTMLElement
            ? tickNode.querySelector(".timeline-axis-label")
            : null;
        const visibleLabelNodes = tickNodes
            .map(resolveTickLabelNode)
            .filter((labelNode) => labelNode instanceof HTMLElement && !labelNode.hidden && String(labelNode.textContent || "").trim());

        const forceLabelVisible = (tickNode, alignEnd) => {
            if (!(tickNode instanceof HTMLElement)) {
                return;
            }

            const labelNode = tickNode.querySelector(".timeline-axis-label");
            if (!(labelNode instanceof HTMLElement)) {
                return;
            }

            const tickLeftPx = resolveTickLeftPx(tickNode);
            const labelWidthRaw = resolveLabelWidth(labelNode);
            const labelWidth = Math.max(1, Math.min(containerWidth, labelWidthRaw > 0 ? labelWidthRaw : 1));
            labelNode.style.maxWidth = `${Math.max(1, containerWidth)}px`;
            const desiredLeft = alignEnd
                ? Math.max(0, containerWidth - labelWidth - edgeInsetPx)
                : edgeInsetPx;
            const shiftedDesiredLeft = desiredLeft - labelGlobalShiftLeftPx;
            const clampedLeft = Math.max(0, Math.min(shiftedDesiredLeft, Math.max(0, containerWidth - labelWidth)));
            labelNode.hidden = false;
            labelNode.style.left = `${Math.round(clampedLeft - tickLeftPx)}px`;
        };

        if (visibleLabelNodes.length < 2 && tickNodes.length >= 2) {
            forceLabelVisible(tickNodes[0], false);
            forceLabelVisible(tickNodes[lastIndex], true);
        } else if (visibleLabelNodes.length === 0 && tickNodes.length === 1) {
            forceLabelVisible(tickNodes[0], false);
        }

    }

    function renderStaticTimelineAxes(scope) {
        const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
        root.querySelectorAll("[data-timeline-axis][data-axis-start][data-axis-end]").forEach((container) => {
            if (!(container instanceof HTMLElement)) {
                return;
            }

            const startDate = parseIsoDate(container.dataset.axisStart);
            const endDate = parseIsoDate(container.dataset.axisEnd);
            if (!(startDate instanceof Date) || !(endDate instanceof Date)) {
                return;
            }

            renderTimelineAxis(container, startDate, endDate);
        });
    }

    function queueRecordSchedulePlannerRecalc(form, attempt) {
        if (!(form instanceof HTMLFormElement) || !form.isConnected) {
            return;
        }

        const retryAttempt = Number.isFinite(attempt) ? Math.max(0, Math.trunc(attempt)) : 0;
        const schedulePanel = form.querySelector('[data-record-modal-panel="schedule"]');
        if (schedulePanel instanceof HTMLElement && schedulePanel.hidden) {
            return;
        }

        const planner = form._recordSchedulePlanner;
        if (planner && typeof planner.recalcAll === "function") {
            planner.recalcAll();
            return;
        }

        if (retryAttempt >= 6) {
            return;
        }

        window.requestAnimationFrame(() => {
            queueRecordSchedulePlannerRecalc(form, retryAttempt + 1);
        });
    }

    function parseIsoDateTime(value) {
        if (!value) {
            return null;
        }

        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::\d{2})?$/.exec(value.trim());
        if (!match) {
            return null;
        }

        const year = Number.parseInt(match[1], 10);
        const month = Number.parseInt(match[2], 10);
        const day = Number.parseInt(match[3], 10);
        const hours = Number.parseInt(match[4], 10);
        const minutes = Number.parseInt(match[5], 10);
        if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)
            || !Number.isFinite(hours) || !Number.isFinite(minutes)
            || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
            return null;
        }

        const date = new Date(year, month - 1, day, hours, minutes, 0, 0);
        if (date.getFullYear() !== year
            || date.getMonth() !== month - 1
            || date.getDate() !== day
            || date.getHours() !== hours
            || date.getMinutes() !== minutes) {
            return null;
        }

        return date;
    }

    function parseTimeValue(value) {
        if (!value) {
            return null;
        }

        const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
        if (!match) {
            return null;
        }

        const hours = Number.parseInt(match[1], 10);
        const minutes = Number.parseInt(match[2], 10);
        if (!Number.isFinite(hours) || !Number.isFinite(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
            return null;
        }

        return { hours, minutes };
    }

    function formatTime(hours, minutes) {
        return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
    }

    function isSameCalendarDate(a, b) {
        return a.getFullYear() === b.getFullYear()
            && a.getMonth() === b.getMonth()
            && a.getDate() === b.getDate();
    }

    function getGlobalFloatingLayerRoot() {
        if (globalFloatingRoot instanceof HTMLElement && globalFloatingRoot.isConnected) {
            return globalFloatingRoot;
        }

        const root = document.createElement("div");
        root.className = "app-floating-root";
        root.setAttribute("data-app-floating-root", "true");
        root.setAttribute("aria-hidden", "true");
        document.body.appendChild(root);
        globalFloatingRoot = root;
        return root;
    }

    function getFloatingLayerRoot(container) {
        const overlay = container instanceof Element
            ? container.closest(".modal-overlay")
            : null;

        if (overlay instanceof HTMLElement) {
            const modalRoot = overlay.querySelector("[data-modal-floating-root]");
            if (modalRoot instanceof HTMLElement) {
                return modalRoot;
            }
        }

        return getGlobalFloatingLayerRoot();
    }

    function getFloatingPanelAnchor(panel) {
        if (!(panel instanceof HTMLElement)) {
            return null;
        }

        const storedAnchor = panel._pmtrackerFloatingAnchor;
        if (storedAnchor instanceof HTMLElement && storedAnchor.isConnected) {
            return storedAnchor;
        }

        const fallbackAnchor = panel.closest("[data-floating-anchor]");
        return fallbackAnchor instanceof HTMLElement ? fallbackAnchor : null;
    }

    function applyFloatingPanelKind(panel, kind) {
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        panel.classList.remove(
            "floating-panel--person-search",
            "floating-panel--ad-search",
            "floating-panel--date",
            "floating-panel--time");

        switch (kind) {
            case "person-search":
                panel.classList.add("floating-panel--person-search");
                break;
            case "ad-search":
                panel.classList.add("floating-panel--ad-search");
                break;
            case "date":
                panel.classList.add("floating-panel--date");
                break;
            case "time":
                panel.classList.add("floating-panel--time");
                break;
            default:
                break;
        }
    }

    function mountFloatingPanel(panel, anchor, options = {}) {
        if (!(panel instanceof HTMLElement) || !(anchor instanceof HTMLElement)) {
            return;
        }

        const root = getFloatingLayerRoot(anchor);
        const existingMount = panel._pmtrackerFloatingMount;
        const kind = options.kind
            || panel.dataset.floatingKind
            || "";
        const matchWidth = options.matchWidth === true
            || panel.dataset.floatingMatchWidth === "true";

        if (!existingMount) {
            const placeholder = document.createElement("span");
            placeholder.hidden = true;
            placeholder.style.display = "none";
            panel.parentNode?.insertBefore(placeholder, panel);

            panel._pmtrackerFloatingMount = {
                placeholder,
                originParent: panel.parentElement
            };
        }

        if (panel.parentElement !== root) {
            root.appendChild(panel);
        }

        panel._pmtrackerFloatingAnchor = anchor;
        panel._pmtrackerFloatingOptions = {
            gap: Number.isFinite(options.gap) ? options.gap : 8,
            flipVertical: options.flipVertical !== false,
            kind,
            matchWidth,
            lockVerticalSide: options.lockVerticalSide === true
        };

        panel.classList.add("floating-panel");
        panel.classList.toggle("floating-panel--match-anchor", matchWidth);
        applyFloatingPanelKind(panel, kind);
        floatingPanelRegistry.add(panel);
        positionFloatingPanel(panel, anchor, panel._pmtrackerFloatingOptions);
    }

    function unmountFloatingPanel(panel) {
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const mount = panel._pmtrackerFloatingMount;
        if (mount?.placeholder instanceof HTMLElement && mount.placeholder.parentNode) {
            mount.placeholder.parentNode.insertBefore(panel, mount.placeholder);
            mount.placeholder.remove();
        }

        panel.classList.remove(
            "floating-panel",
            "floating-panel--match-anchor",
            "floating-panel--person-search",
            "floating-panel--ad-search",
            "floating-panel--date",
            "floating-panel--time");
        panel.style.removeProperty("position");
        panel.style.removeProperty("left");
        panel.style.removeProperty("right");
        panel.style.removeProperty("top");
        panel.style.removeProperty("bottom");
        panel.style.removeProperty("width");
        panel.style.removeProperty("max-width");
        panel.style.removeProperty("min-width");
        panel.style.removeProperty("max-height");
        panel.style.removeProperty("overflow-y");
        panel.style.removeProperty("visibility");

        delete panel._pmtrackerFloatingMount;
        delete panel._pmtrackerFloatingAnchor;
        delete panel._pmtrackerFloatingOptions;
        delete panel._pmtrackerVerticalSide;
        floatingPanelRegistry.delete(panel);
    }

    function closeAllFloatingPanels(scope, exceptPanel) {
        floatingPanelRegistry.forEach((panel) => {
            if (!(panel instanceof HTMLElement) || panel === exceptPanel) {
                return;
            }

            const anchor = getFloatingPanelAnchor(panel);
            if (scope instanceof Element || scope instanceof Document) {
                const scopeContainsPanel = scope.contains(panel);
                const scopeContainsAnchor = anchor instanceof HTMLElement && scope.contains(anchor);
                if (!scopeContainsPanel && !scopeContainsAnchor) {
                    return;
                }
            }

            panel.hidden = true;
            unmountFloatingPanel(panel);
        });
    }

    function resolveRecordEditorFloatingBoundary(anchor, boundary, kind) {
        if (!(anchor instanceof HTMLElement) || !boundary) {
            return boundary;
        }

        if (kind !== "date" && kind !== "time" && kind !== "person-search") {
            return boundary;
        }

        const modalContainer = anchor.closest("[data-modal-container]");
        if (!(modalContainer instanceof HTMLElement)) {
            return boundary;
        }

        const recordEditorForm = anchor.closest('form[data-record-editor-form="true"]');
        if (!(recordEditorForm instanceof HTMLElement)) {
            return boundary;
        }

        const actionBar = recordEditorForm.querySelector(".record-editor-actions");
        if (!(actionBar instanceof HTMLElement)) {
            return boundary;
        }

        const actionBarRect = actionBar.getBoundingClientRect();
        if (actionBarRect.height <= 0) {
            return boundary;
        }

        const adjustedBottom = Math.min(boundary.bottom, actionBarRect.top - 8);
        if (adjustedBottom <= boundary.top + 72) {
            return boundary;
        }

        return {
            ...boundary,
            bottom: adjustedBottom
        };
    }

    function positionFloatingPanel(panel, anchor, options = {}) {
        if (!(panel instanceof HTMLElement) || !(anchor instanceof HTMLElement) || panel.hidden) {
            return;
        }

        const gap = Number.isFinite(options.gap) ? options.gap : 8;
        const matchWidth = options.matchWidth === true || panel.dataset.floatingMatchWidth === "true";
        const kind = typeof options.kind === "string" ? options.kind : "";
        const lockVerticalSide = options.lockVerticalSide === true;
        const viewportBoundary = {
            left: 8,
            right: window.innerWidth - 8,
            top: 8,
            bottom: window.innerHeight - 8
        };
        const modalContainer = anchor.closest("[data-modal-container]");
        const baseBoundary = modalContainer instanceof HTMLElement
            ? (() => {
                const modalRect = modalContainer.getBoundingClientRect();
                return {
                    left: Math.max(viewportBoundary.left, modalRect.left + 8),
                    right: Math.min(viewportBoundary.right, modalRect.right - 8),
                    top: Math.max(viewportBoundary.top, modalRect.top + 8),
                    bottom: Math.min(viewportBoundary.bottom, modalRect.bottom - 8)
                };
            })()
            : viewportBoundary;
        const boundary = resolveRecordEditorFloatingBoundary(anchor, baseBoundary, kind);
        const anchorRect = anchor.getBoundingClientRect();
        if (anchorRect.width <= 0 || anchorRect.height <= 0) {
            return;
        }

        panel.style.position = "fixed";
        panel.style.visibility = "hidden";
        panel.style.left = "0px";
        panel.style.top = "0px";
        panel.style.right = "auto";
        panel.style.bottom = "auto";
        panel.style.maxHeight = "";
        panel.style.overflowY = "";
        panel.style.maxWidth = `${Math.max(boundary.right - boundary.left, 0)}px`;
        panel.style.width = matchWidth
            ? `${Math.min(Math.round(anchorRect.width), Math.max(boundary.right - boundary.left, 0))}px`
            : "";
        panel.style.minWidth = matchWidth ? `${Math.min(Math.round(anchorRect.width), Math.max(boundary.right - boundary.left, 0))}px` : "";

        let panelRect = panel.getBoundingClientRect();
        const availableBelow = Math.max(0, boundary.bottom - anchorRect.bottom - gap);
        const availableAbove = Math.max(0, anchorRect.top - boundary.top - gap);
        const fitsBelow = availableBelow >= panelRect.height;
        const fitsAbove = availableAbove >= panelRect.height;
        let shouldOpenAbove = false;
        const storedVerticalSide = panel._pmtrackerVerticalSide === "above" || panel._pmtrackerVerticalSide === "below"
            ? panel._pmtrackerVerticalSide
            : null;

        if (!lockVerticalSide) {
            delete panel._pmtrackerVerticalSide;
        }

        if (lockVerticalSide && storedVerticalSide) {
            shouldOpenAbove = storedVerticalSide === "above";
        } else {
            if (fitsBelow) {
                shouldOpenAbove = false;
            } else if (fitsAbove) {
                shouldOpenAbove = true;
            } else {
                shouldOpenAbove = availableAbove > availableBelow;
            }

            if (lockVerticalSide) {
                panel._pmtrackerVerticalSide = shouldOpenAbove ? "above" : "below";
            }
        }

        const availableOnSelectedSide = shouldOpenAbove ? availableAbove : availableBelow;
        if (availableOnSelectedSide > 0 && availableOnSelectedSide < panelRect.height) {
            const maxHeight = Math.floor(Math.max(availableOnSelectedSide - 4, 0));
            if (maxHeight > 0) {
                panel.style.maxHeight = `${maxHeight}px`;
                panel.style.overflowY = "auto";
                panelRect = panel.getBoundingClientRect();
            }
        }

        let left = anchorRect.left;
        if (left + panelRect.width > boundary.right) {
            left = boundary.right - panelRect.width;
        }
        if (left < boundary.left) {
            left = boundary.left;
        }

        let top = shouldOpenAbove
            ? anchorRect.top - panelRect.height - gap
            : anchorRect.bottom + gap;

        if (top + panelRect.height > boundary.bottom) {
            top = boundary.bottom - panelRect.height;
        }
        if (top < boundary.top) {
            top = boundary.top;
        }

        panel.style.left = `${Math.round(left)}px`;
        panel.style.top = `${Math.round(top)}px`;
        panel.style.visibility = "";
    }

    function applyProjectIndexFilters(scope) {
        const root = resolveQueryRoot(scope);
        const shell = root.querySelector("[data-project-list-shell]");
        if (!(shell instanceof HTMLElement)) {
            return;
        }

        syncProjectListStatusFilterInputs(root);

        const hiddenStatusCodes = Array.from(root.querySelectorAll("[data-project-status-hide]"))
            .filter((input) => input instanceof HTMLInputElement && input.checked)
            .map((input) => (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase())
            .filter((value) => value.length > 0);

        let visibleCount = 0;
        shell.querySelectorAll("[data-project-status-code]").forEach((card) => {
            if (!(card instanceof HTMLElement)) {
                return;
            }

            const statusCode = (card.getAttribute("data-project-status-code") || "").trim().toUpperCase();
            const shouldHide = hiddenStatusCodes.includes(statusCode);
            card.hidden = shouldHide;
            if (!shouldHide) {
                visibleCount += 1;
            }
        });

        const emptyState = shell.querySelector("[data-project-grid-empty]");
        if (emptyState instanceof HTMLElement) {
            emptyState.hidden = visibleCount > 0;
        }
    }

    function initProjectIndexStatusFilters(scope) {
        const root = resolveQueryRoot(scope);
        const shell = root.querySelector("[data-project-list-shell]");
        if (!(shell instanceof HTMLElement)) {
            return;
        }

        const toggleButton = document.querySelector("[data-project-status-filter-toggle]");
        if (toggleButton instanceof HTMLButtonElement && toggleButton.dataset.boundProjectStatusFilter !== "true") {
            toggleButton.dataset.boundProjectStatusFilter = "true";
            toggleButton.addEventListener("click", () => {
                toggleProjectStatusFilterPanel(toggleButton);
            });
        }

        document.querySelectorAll("[data-project-status-hide]").forEach((input) => {
            if (input instanceof HTMLInputElement && input.dataset.boundProjectStatusFilter !== "true") {
                input.dataset.boundProjectStatusFilter = "true";
                input.addEventListener("change", () => {
                    handleProjectStatusFilterInput(input);
                });
            }
        });

        syncProjectListStatusFilterInputs(document);
        applyProjectIndexFilters(root);
    }

    function toggleProjectStatusFilterPanel(button) {
        if (!(button instanceof HTMLElement)) {
            return;
        }

        const panel = document.querySelector("[data-project-status-filter-panel]");
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const isOpen = !panel.hidden;
        const nextOpen = !isOpen;
        panel.hidden = !nextOpen;
        button.setAttribute("aria-expanded", String(nextOpen));
    }

    function handleProjectStatusFilterInput(input) {
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
        if (statusCode === "DONE") {
            writeProjectListStatusFilterState(projectListHideDoneStorageKey, input.checked);
        }
        else if (statusCode === "DELETED") {
            writeProjectListStatusFilterState(projectListHideDeletedStorageKey, input.checked);
        }

        applyProjectIndexFilters(document);
    }

    function toggleMeetingAttendancePanel(button) {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        const card = button.closest("[data-meeting-attendance-card]");
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const panel = card.querySelector("[data-meeting-attendance-panel]");
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        const shouldOpen = panel.hidden;
        panel.hidden = !shouldOpen;
        button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
        button.textContent = shouldOpen ? "Skrýt účast" : "Zobrazit účast";
    }

    function repositionFloatingPanels() {
        floatingPanelRegistry.forEach((panel) => {
            if (!(panel instanceof HTMLElement) || panel.hidden) {
                return;
            }

            const anchor = getFloatingPanelAnchor(panel);
            if (!(anchor instanceof HTMLElement) || !anchor.isConnected) {
                panel.hidden = true;
                unmountFloatingPanel(panel);
                return;
            }

            positionFloatingPanel(panel, anchor, panel._pmtrackerFloatingOptions || {});
        });
    }

    const queueFloatingPanelReposition = debounce(() => {
        repositionFloatingPanels();
    }, 16);

    function isInteractionInsideFloatingControl(target, anchor, panel) {
        if (!(target instanceof Element)) {
            return false;
        }

        return (anchor instanceof HTMLElement && anchor.contains(target))
            || (panel instanceof HTMLElement && panel.contains(target));
    }

    function closeAllDatePanels(exceptField) {
        document.querySelectorAll("[data-app-date-field]").forEach((candidate) => {
            if (!(candidate instanceof HTMLElement)) {
                return;
            }
            if (exceptField && candidate === exceptField) {
                return;
            }

            const panel = candidate.querySelector("[data-app-date-panel]");
            if (panel instanceof HTMLElement) {
                panel.hidden = true;
                unmountFloatingPanel(panel);
            }
        });
    }

    function closeAllTimePanels(exceptField) {
        document.querySelectorAll("[data-app-time-field]").forEach((candidate) => {
            if (!(candidate instanceof HTMLElement)) {
                return;
            }
            if (exceptField && candidate === exceptField) {
                return;
            }

            const panel = candidate.querySelector("[data-app-time-panel]");
            if (panel instanceof HTMLElement) {
                panel.hidden = true;
                unmountFloatingPanel(panel);
            }
        });
    }

    function initCustomDatePickers(scope) {
        scope.querySelectorAll("[data-app-date-field]").forEach((field) => {
            if (!(field instanceof HTMLElement) || field.dataset.appDateReady === "true") {
                return;
            }

            const displayInput = field.querySelector("[data-app-date-display]");
            const valueInput = field.querySelector("[data-app-date-value]");
            const openButton = field.querySelector("[data-app-date-open]");
            const panel = field.querySelector("[data-app-date-panel]");
            const prevButton = field.querySelector("[data-app-date-prev]");
            const nextButton = field.querySelector("[data-app-date-next]");
            const monthSelect = field.querySelector("[data-app-date-month]");
            const yearSelect = field.querySelector("[data-app-date-year]");
            const grid = field.querySelector("[data-app-date-grid]");

            if (!(displayInput instanceof HTMLInputElement)
                || !(valueInput instanceof HTMLInputElement)
                || !(openButton instanceof HTMLButtonElement)
                || !(panel instanceof HTMLElement)
                || !(prevButton instanceof HTMLButtonElement)
                || !(nextButton instanceof HTMLButtonElement)
                || !(monthSelect instanceof HTMLSelectElement)
                || !(yearSelect instanceof HTMLSelectElement)
                || !(grid instanceof HTMLElement)) {
                return;
            }

            field.dataset.appDateReady = "true";
            const isFieldLocked = () => field.dataset.appDateLocked === "true" || openButton.disabled;

            let selectedDate = parseIsoDate(valueInput.value) || parseDisplayDate(displayInput.value) || null;
            let viewDate = selectedDate ? new Date(selectedDate.getTime()) : new Date();

            const syncValue = () => {
                const previous = valueInput.value;
                valueInput.value = selectedDate ? formatIsoDate(selectedDate) : "";
                displayInput.value = selectedDate ? formatDisplayDate(selectedDate) : "";
                if (previous !== valueInput.value) {
                    valueInput.dispatchEvent(new Event("change", { bubbles: true }));
                }
            };

            const ensureMonthOptions = () => {
                if (monthSelect.options.length > 0) {
                    return;
                }

                dateMonths.forEach((month, index) => {
                    const option = document.createElement("option");
                    option.value = String(index);
                    option.textContent = month;
                    monthSelect.appendChild(option);
                });
            };

            const ensureYearOptions = (centerYear) => {
                const fromYear = centerYear - 20;
                const toYear = centerYear + 20;
                const currentFrom = Number.parseInt(yearSelect.dataset.fromYear || "", 10);
                const currentTo = Number.parseInt(yearSelect.dataset.toYear || "", 10);
                if (currentFrom === fromYear && currentTo === toYear) {
                    return;
                }

                yearSelect.innerHTML = "";
                for (let year = fromYear; year <= toYear; year += 1) {
                    const option = document.createElement("option");
                    option.value = String(year);
                    option.textContent = String(year);
                    yearSelect.appendChild(option);
                }
                yearSelect.dataset.fromYear = String(fromYear);
                yearSelect.dataset.toYear = String(toYear);
            };

            const renderGrid = () => {
                ensureMonthOptions();
                ensureYearOptions(viewDate.getFullYear());

                monthSelect.value = String(viewDate.getMonth());
                yearSelect.value = String(viewDate.getFullYear());

                grid.innerHTML = "";
                const currentMonth = viewDate.getMonth();
                const currentYear = viewDate.getFullYear();
                const firstDayOfMonth = new Date(currentYear, currentMonth, 1);
                const mondayOffset = (firstDayOfMonth.getDay() + 6) % 7;
                const firstVisibleDate = new Date(currentYear, currentMonth, 1 - mondayOffset);
                const today = new Date();
                today.setHours(0, 0, 0, 0);

                for (let i = 0; i < 42; i += 1) {
                    const dayDate = new Date(firstVisibleDate.getFullYear(), firstVisibleDate.getMonth(), firstVisibleDate.getDate() + i);
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "app-date-day";
                    button.textContent = String(dayDate.getDate());
                    button.dataset.iso = formatIsoDate(dayDate);
                    button.setAttribute("role", "gridcell");

                    if (dayDate.getMonth() !== currentMonth) {
                        button.classList.add("outside");
                    }
                    if (selectedDate && isSameCalendarDate(dayDate, selectedDate)) {
                        button.classList.add("selected");
                    }
                    if (isSameCalendarDate(dayDate, today)) {
                        button.title = "Dnes";
                    }

                    button.addEventListener("click", () => {
                        selectedDate = dayDate;
                        viewDate = new Date(dayDate.getFullYear(), dayDate.getMonth(), 1);
                        syncValue();
                        closePanel();
                    });

                    grid.appendChild(button);
                }

                if (!panel.hidden) {
                    positionFloatingPanel(panel, field, panel._pmtrackerFloatingOptions || {
                        gap: 8,
                        flipVertical: true,
                        kind: "date"
                    });
                }
            };

            const closePanel = () => {
                panel.hidden = true;
                unmountFloatingPanel(panel);
            };

            const openPanel = () => {
                if (isFieldLocked()) {
                    return;
                }
                closeAllDatePanels(field);
                closeAllTimePanels();
                renderGrid();
                panel.hidden = false;
                mountFloatingPanel(panel, field, { gap: 8, flipVertical: true, kind: "date" });
            };

            syncValue();

            openButton.addEventListener("click", () => {
                if (panel.hidden) {
                    openPanel();
                } else {
                    closePanel();
                }
            });

            displayInput.addEventListener("click", () => {
                openPanel();
            });

            displayInput.addEventListener("focus", () => {
                openPanel();
            });

            displayInput.addEventListener("keydown", (event) => {
                if (event.key === "Enter" || event.key === "ArrowDown") {
                    event.preventDefault();
                    openPanel();
                }
            });

            prevButton.addEventListener("click", () => {
                viewDate = new Date(viewDate.getFullYear(), viewDate.getMonth() - 1, 1);
                renderGrid();
            });

            nextButton.addEventListener("click", () => {
                viewDate = new Date(viewDate.getFullYear(), viewDate.getMonth() + 1, 1);
                renderGrid();
            });

            monthSelect.addEventListener("change", () => {
                const month = Number.parseInt(monthSelect.value, 10);
                if (!Number.isFinite(month)) {
                    return;
                }
                viewDate = new Date(viewDate.getFullYear(), month, 1);
                renderGrid();
            });

            yearSelect.addEventListener("change", () => {
                const year = Number.parseInt(yearSelect.value, 10);
                if (!Number.isFinite(year)) {
                    return;
                }
                viewDate = new Date(year, viewDate.getMonth(), 1);
                renderGrid();
            });

            document.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }
                if (!isInteractionInsideFloatingControl(target, field, panel)) {
                    closePanel();
                }
            });

            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    closePanel();
                }
            });

            displayInput.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    event.preventDefault();
                    event.stopPropagation();
                    closePanel();
                }
            });
        });
    }

    function initCustomTimePickers(scope) {
        scope.querySelectorAll("[data-app-time-field]").forEach((field) => {
            if (!(field instanceof HTMLElement) || field.dataset.appTimeReady === "true") {
                return;
            }

            const displayInput = field.querySelector("[data-app-time-display]");
            const valueInput = field.querySelector("[data-app-time-value]");
            const openButton = field.querySelector("[data-app-time-open]");
            const panel = field.querySelector("[data-app-time-panel]");
            const grid = field.querySelector("[data-app-time-grid]");
            if (!(displayInput instanceof HTMLInputElement)
                || !(valueInput instanceof HTMLInputElement)
                || !(openButton instanceof HTMLButtonElement)
                || !(panel instanceof HTMLElement)
                || !(grid instanceof HTMLElement)) {
                return;
            }

            field.dataset.appTimeReady = "true";
            const isLocked = field.dataset.appTimeLocked === "true" || openButton.disabled;
            const form = field.closest("form");

            const initialDateTime = parseIsoDateTime(valueInput.value);
            const initialTime = parseTimeValue(displayInput.value)
                || parseTimeValue(valueInput.value)
                || (initialDateTime ? { hours: initialDateTime.getHours(), minutes: initialDateTime.getMinutes() } : null);
            let selected = initialTime || { hours: new Date().getHours(), minutes: new Date().getMinutes() };

            const syncValue = () => {
                const normalizedTime = formatTime(selected.hours, selected.minutes);
                displayInput.value = normalizedTime;
                valueInput.value = normalizedTime;
            };

            const closePanel = () => {
                panel.hidden = true;
                unmountFloatingPanel(panel);
            };

            const render = () => {
                grid.innerHTML = "";

                for (let hour = 0; hour < 24; hour += 1) {
                    for (let minute = 0; minute < 60; minute += 15) {
                        const timeText = formatTime(hour, minute);
                        const button = document.createElement("button");
                        button.type = "button";
                        button.className = "app-time-option";
                        button.textContent = timeText;
                        button.dataset.time = timeText;
                        button.setAttribute("role", "option");
                        button.setAttribute("aria-selected", String(selected.hours === hour && selected.minutes === minute));
                        if (selected.hours === hour && selected.minutes === minute) {
                            button.classList.add("selected");
                        }

                        button.addEventListener("click", () => {
                            selected = { hours: hour, minutes: minute };
                            syncValue();
                            closePanel();
                        });

                        grid.appendChild(button);
                    }
                }

                if (!panel.hidden) {
                    positionFloatingPanel(panel, field, panel._pmtrackerFloatingOptions || {
                        gap: 8,
                        flipVertical: true,
                        kind: "time"
                    });
                }
            };

            const openPanel = () => {
                if (isLocked) {
                    return;
                }
                closeAllDatePanels();
                closeAllTimePanels(field);
                render();
                panel.hidden = false;
                mountFloatingPanel(panel, field, { gap: 8, flipVertical: true, kind: "time" });
            };

            syncValue();

            openButton.addEventListener("click", () => {
                if (panel.hidden) {
                    openPanel();
                } else {
                    closePanel();
                }
            });

            displayInput.addEventListener("mousedown", (event) => {
                event.preventDefault();
                openPanel();
            });

            displayInput.addEventListener("click", () => {
                openPanel();
            });

            displayInput.addEventListener("focus", () => {
                openPanel();
            });

            displayInput.addEventListener("keydown", (event) => {
                if (event.key === "Enter" || event.key === "ArrowDown") {
                    event.preventDefault();
                    openPanel();
                } else if (event.key === "Escape") {
                    event.preventDefault();
                    event.stopPropagation();
                    closePanel();
                }
            });

            if (form instanceof HTMLFormElement) {
                form.addEventListener("submit", () => {
                    syncValue();
                });
            }

            document.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }
                if (!isInteractionInsideFloatingControl(target, field, panel)) {
                    closePanel();
                }
            });

            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    closePanel();
                }
            });
        });
    }

    function formatPersonEntryLabel(entry) {
        if (entry.email) {
            return `${entry.label} <${entry.email}>`;
        }
        return entry.label;
    }

    function initSinglePersonPickers(scope) {
        scope.querySelectorAll('[data-person-picker="single"]').forEach((wrapper) => {
            if (!(wrapper instanceof HTMLElement) || wrapper.dataset.pickerReady === "true") {
                return;
            }

            const input = wrapper.querySelector("[data-person-picker-input]");
            const anchor = wrapper.querySelector("[data-floating-anchor]");
            let hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
            if (!(hiddenInput instanceof HTMLInputElement)) {
                const formScope = wrapper.closest("form");
                if (formScope instanceof HTMLFormElement) {
                    const formHidden = formScope.querySelector("[data-person-picker-hidden]");
                    if (formHidden instanceof HTMLInputElement) {
                        hiddenInput = formHidden;
                    }
                }
            }
            const source = wrapper.querySelector("[data-person-picker-source]");
            const panel = wrapper.querySelector("[data-person-picker-panel]");
            const results = wrapper.querySelector("[data-person-picker-results]");
            const message = wrapper.querySelector("[data-person-picker-message]");

            if (!(input instanceof HTMLInputElement)
                || !(anchor instanceof HTMLElement)
                || !(hiddenInput instanceof HTMLInputElement)
                || !(source instanceof HTMLElement)
                || !(panel instanceof HTMLElement)
                || !(results instanceof HTMLElement)) {
                return;
            }

            wrapper.dataset.pickerReady = "true";
            input.placeholder = wrapper.dataset.personPickerPlaceholder || input.placeholder || "Vyhledejte osobu...";

            const entries = Array.from(source.querySelectorAll("[data-id]"))
                .map((item) => {
                    if (!(item instanceof HTMLElement)) {
                        return null;
                    }

                    return {
                        id: item.dataset.id || "",
                        label: (item.dataset.label || "").trim(),
                        email: (item.dataset.email || "").trim(),
                        org: (item.dataset.org || "").trim(),
                        unit: (item.dataset.unit || "").trim()
                    };
                })
                .filter((entry) => entry && entry.id && entry.label);

            if (entries.length === 0) {
                return;
            }

            let filtered = [];
            let activeIndex = -1;
            const lockVerticalSide = anchor.closest("[data-modal-container]") instanceof HTMLElement
                && anchor.closest('form[data-record-editor-form="true"][data-record-editor-presentation="modal"]') instanceof HTMLElement;

            const closePanel = () => {
                panel.hidden = true;
                activeIndex = -1;
                unmountFloatingPanel(panel);
            };

            const openPanel = () => {
                panel.hidden = false;
                mountFloatingPanel(panel, anchor, {
                    gap: 6,
                    flipVertical: true,
                    kind: "person-search",
                    matchWidth: true,
                    lockVerticalSide
                });
            };

            const setMessage = (text) => {
                if (!(message instanceof HTMLElement)) {
                    return;
                }
                message.textContent = text;
            };

            const findEntryById = (id) => entries.find((entry) => entry.id === id) || null;
            const findEntryByInput = () => {
                const query = normalizeSearchText(input.value || "");
                if (!query) {
                    return null;
                }

                const exactDisplay = entries.find((entry) =>
                    normalizeSearchText(formatPersonEntryLabel(entry)) === query);
                if (exactDisplay) {
                    return exactDisplay;
                }

                const exactLabel = entries.find((entry) => normalizeSearchText(entry.label) === query);
                if (exactLabel) {
                    return exactLabel;
                }

                const exactEmail = entries.find((entry) => entry.email && normalizeSearchText(entry.email) === query);
                if (exactEmail) {
                    return exactEmail;
                }

                return null;
            };

            const selectEntry = (entry, sourceKind = "user") => {
                hiddenInput.value = entry.id;
                input.value = formatPersonEntryLabel(entry);
                input.setCustomValidity("");
                setMessage(entry.email
                    ? `Vybraná osoba: ${entry.label}, ${entry.email}`
                    : `Vybraná osoba: ${entry.label}`);
                closePanel();

                wrapper.dispatchEvent(new CustomEvent("person-picker:selected", {
                    bubbles: true,
                    detail: {
                        source: sourceKind,
                        id: entry.id,
                        label: entry.label,
                        email: entry.email
                    }
                }));
            };

            const render = () => {
                results.innerHTML = "";

                if (filtered.length === 0) {
                    setMessage(wrapper.dataset.personPickerEmpty || "Nenalezeny žádné odpovídající osoby.");
                    closePanel();
                    return;
                }

                filtered.forEach((entry, index) => {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "office-search-item";
                    button.setAttribute("role", "option");
                    button.dataset.index = String(index);
                    if (index === activeIndex) {
                        button.classList.add("active");
                    }

                    const primary = document.createElement("span");
                    primary.className = "office-search-primary";
                    primary.textContent = entry.label;
                    button.appendChild(primary);

                    const secondary = document.createElement("span");
                    secondary.className = "office-search-secondary";
                    const hasOrgOrUnit = Boolean(entry.org || entry.unit);
                    if (entry.email && hasOrgOrUnit) {
                        const orgPart = `${entry.org || "-"} / ${entry.unit || "-"}`;
                        secondary.textContent = `${entry.email} | ${orgPart}`;
                        button.appendChild(secondary);
                    } else if (entry.email) {
                        secondary.textContent = entry.email;
                        button.appendChild(secondary);
                    } else if (hasOrgOrUnit) {
                        secondary.textContent = `${entry.org || "-"} / ${entry.unit || "-"}`;
                        button.appendChild(secondary);
                    }

                    results.appendChild(button);
                });

                openPanel();
            };

            const rank = (query) => {
                const normalized = normalizeSearchText(query);

                const ranked = entries
                    .map((entry) => {
                        const searchable = `${entry.label} ${entry.email} ${entry.org} ${entry.unit}`;
                        const score = normalized ? scoreSearchCandidate(normalized, searchable) : 1;
                        return { entry, score };
                    })
                    .filter((row) => row.score > 0)
                    .sort((a, b) => b.score - a.score || a.entry.label.localeCompare(b.entry.label, "cs"));

                return ranked.slice(0, 15).map((row) => row.entry);
            };

            const runSearch = () => {
                filtered = rank(input.value || "");
                activeIndex = filtered.length > 0 ? 0 : -1;
                hiddenInput.value = "";
                input.setCustomValidity("");
                render();
            };

            const debouncedSearch = debounce(runSearch, 140);

            const initial = entries.find((entry) => entry.id === hiddenInput.value);
            if (initial && !input.value.trim()) {
                input.value = formatPersonEntryLabel(initial);
            }

            wrapper.addEventListener("person-picker:select-id", (event) => {
                if (!(event instanceof CustomEvent)) {
                    return;
                }

                const requestedId = event.detail?.id ? String(event.detail.id) : "";
                if (!requestedId) {
                    return;
                }

                const selected = findEntryById(requestedId);
                if (!selected) {
                    return;
                }

                const sourceKind = typeof event.detail?.source === "string" ? event.detail.source : "auto";
                selectEntry(selected, sourceKind === "user" ? "user" : "auto");
            });

            input.addEventListener("input", () => {
                setMessage("Vyhledávám...");
                debouncedSearch();
            });

            input.addEventListener("focus", () => {
                filtered = rank(input.value || "");
                activeIndex = filtered.length > 0 ? 0 : -1;
                render();
            });

            input.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    event.preventDefault();
                    event.stopPropagation();
                    closePanel();
                    return;
                }

                if (event.key === "ArrowDown") {
                    event.preventDefault();
                    if (filtered.length === 0) {
                        filtered = rank(input.value || "");
                    }
                    activeIndex = Math.min(activeIndex + 1, filtered.length - 1);
                    render();
                    return;
                }

                if (event.key === "ArrowUp") {
                    event.preventDefault();
                    activeIndex = Math.max(activeIndex - 1, 0);
                    render();
                    return;
                }

                if (event.key === "Enter" && activeIndex >= 0 && filtered[activeIndex]) {
                    event.preventDefault();
                    selectEntry(filtered[activeIndex]);
                }
            });

            results.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                const button = target.closest("[data-index]");
                if (!(button instanceof HTMLElement)) {
                    return;
                }

                const index = Number.parseInt(button.dataset.index || "-1", 10);
                if (!Number.isFinite(index) || index < 0 || index >= filtered.length) {
                    return;
                }

                selectEntry(filtered[index]);
            });

            const form = wrapper.closest("form");
            if (form instanceof HTMLFormElement) {
                form.addEventListener("submit", (event) => {
                    if (hiddenInput.value) {
                        input.setCustomValidity("");
                        return;
                    }

                    const matchedEntry = findEntryByInput();
                    if (matchedEntry) {
                        selectEntry(matchedEntry, "auto");
                        return;
                    }

                    event.preventDefault();
                    setMessage("Vyberte osobu ze seznamu výsledků.");
                    input.setCustomValidity("Vyberte osobu ze seznamu výsledků.");
                    input.reportValidity();
                    input.focus();
                });
            }

            document.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
                    closePanel();
                }
            });
        });
    }

    function initAdPersonPickers(scope) {
        scope.querySelectorAll("[data-ad-picker]").forEach((wrapper) => {
            if (!(wrapper instanceof HTMLElement) || wrapper.dataset.adPickerReady === "true") {
                return;
            }

            const searchUrl = wrapper.dataset.searchUrl || "";
            const form = wrapper.closest("form");
            const anchor = wrapper.querySelector("[data-floating-anchor]");
            const queryInput = wrapper.querySelector("[data-ad-query-input]");
            const queryHidden = form?.querySelector("[data-ad-query-hidden]");
            const guidInput = form?.querySelector("[data-ad-guid]");
            const adLoginInput = form?.querySelector("[data-ad-login]");
            const adCompanyInput = form?.querySelector("[data-ad-company]");
            const adDepartmentInput = form?.querySelector("[data-ad-department]");
            const jmenoInput = form?.querySelector("[data-ad-jmeno]");
            const prijmeniInput = form?.querySelector("[data-ad-prijmeni]");
            const titulInput = form?.querySelector("[data-ad-titul]");
            const emailInput = form?.querySelector("[data-ad-email]");
            const orgSelect = form?.querySelector("[data-ad-org-select]");
            const orgCreateHint = form?.querySelector("[data-ad-org-create-hint]");
            const orgUnitSelect = form?.querySelector("[data-ad-org-unit-select]");
            const orgUnitCreateHint = form?.querySelector("[data-ad-org-unit-create-hint]");
            const submitButton = form?.querySelector("[data-ad-submit]");
            const panel = wrapper.querySelector("[data-ad-search-panel]");
            const results = wrapper.querySelector("[data-ad-results]");

            if (!searchUrl
                || !(form instanceof HTMLFormElement)
                || !(anchor instanceof HTMLElement)
                || !(queryInput instanceof HTMLInputElement)
                || !(queryHidden instanceof HTMLInputElement)
                || !(guidInput instanceof HTMLInputElement)
                || !(adLoginInput instanceof HTMLInputElement)
                || !(adCompanyInput instanceof HTMLInputElement)
                || !(adDepartmentInput instanceof HTMLInputElement)
                || !(jmenoInput instanceof HTMLInputElement)
                || !(prijmeniInput instanceof HTMLInputElement)
                || !(titulInput instanceof HTMLInputElement)
                || !(emailInput instanceof HTMLInputElement)
                || !(orgSelect instanceof HTMLSelectElement)
                || !(orgCreateHint instanceof HTMLElement)
                || !(orgUnitSelect instanceof HTMLSelectElement)
                || !(orgUnitCreateHint instanceof HTMLElement)
                || !(panel instanceof HTMLElement)
                || !(results instanceof HTMLElement)
                || !(submitButton instanceof HTMLButtonElement)) {
                return;
            }

            wrapper.dataset.adPickerReady = "true";
            let activeIndex = -1;
            let currentResults = [];
            let adAvailabilityKnown = false;
            let adIsUnavailable = false;
            const adUnavailableMessage = "Active Directory ACR není dostupné.";

            const closePanel = () => {
                panel.hidden = true;
                activeIndex = -1;
                unmountFloatingPanel(panel);
            };

            const clearSelection = () => {
                guidInput.value = "";
                adLoginInput.value = "";
                adCompanyInput.value = "";
                adDepartmentInput.value = "";
                jmenoInput.value = "";
                prijmeniInput.value = "";
                titulInput.value = "";
                emailInput.value = "";
                clearGeneratedOption(orgSelect, orgCreateHint);
                clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                submitButton.disabled = true;
            };

            const normalizeText = (value) => (value || "").toString().trim().toLowerCase();

            const clearGeneratedOption = (select, hint) => {
                Array.from(select.options)
                    .filter((option) => option.dataset.generated === "true")
                    .forEach((option) => option.remove());
                hint.hidden = true;
            };

            const ensureGeneratedOption = (select, hint, rawValue) => {
                clearGeneratedOption(select, hint);
                const normalized = (rawValue || "").toString().trim();
                if (!normalized) {
                    return;
                }

                const option = document.createElement("option");
                option.value = `__new__:${normalized}`;
                option.textContent = `${normalized} (+ bude přidáno do číselníku)`;
                option.dataset.generated = "true";
                select.appendChild(option);
                select.value = option.value;
                hint.hidden = false;
            };

            const ensureGeneratedOrgUnitOption = (code, name) => {
                clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                const normalizedCode = (code || "").toString().trim();
                const normalizedName = (name || "").toString().trim();
                if (!normalizedCode && !normalizedName) {
                    return;
                }

                const option = document.createElement("option");
                const encoded = normalizedCode && normalizedName
                    ? `${normalizedCode}|${normalizedName}`
                    : normalizedCode || normalizedName;
                option.value = `__new__:${encoded}`;
                option.textContent = normalizedCode && normalizedName
                    ? `${normalizedCode} - ${normalizedName} (+ bude přidáno do číselníku)`
                    : `${encoded} (+ bude přidáno do číselníku)`;
                option.dataset.generated = "true";
                orgUnitSelect.appendChild(option);
                orgUnitSelect.value = option.value;
                orgUnitCreateHint.hidden = false;
            };

            const extractCodePrefix = (value) => {
                const text = (value || "").toString().trim();
                const hyphenIndex = text.indexOf("-");
                if (hyphenIndex < 2 || hyphenIndex > 6) {
                    return null;
                }

                const prefix = text.slice(0, hyphenIndex).trim().toUpperCase();
                if (!/^[A-Z0-9]{2,8}$/.test(prefix)) {
                    return null;
                }

                return prefix;
            };

            const findOptionByCodeOrText = (select, rawText, preferredCodes = []) => {
                const options = Array.from(select.options);
                const normalizedText = normalizeText(rawText);
                const codeFromText = extractCodePrefix(rawText);
                const normalizedCodes = preferredCodes
                    .map((code) => normalizeText(code))
                    .filter((code) => code);
                if (codeFromText) {
                    normalizedCodes.unshift(normalizeText(codeFromText));
                }

                for (const normalizedCode of normalizedCodes) {
                    const byCode = options.find((option) => normalizeText(option.value) === normalizedCode);
                    if (byCode) {
                        return byCode.value;
                    }
                }

                if (normalizedText) {
                    const byText = options.find((option) => normalizeText(option.textContent).includes(normalizedText));
                    if (byText) {
                        return byText.value;
                    }
                }

                return null;
            };

            const parseCompanyLocation = (rawCompany) => {
                const company = (rawCompany || "").toString().trim();
                if (!company) {
                    return {
                        organizationCode: null,
                        organizationName: null,
                        orgUnitCode: null,
                        orgUnitName: null,
                        source: ""
                    };
                }

                const slashIndex = company.indexOf("/");
                const left = slashIndex >= 0 ? company.slice(0, slashIndex).trim() : company;
                const right = slashIndex >= 0 ? company.slice(slashIndex + 1).trim() : "";

                let organizationCode = null;
                let organizationName = left;
                const hyphenIndex = left.indexOf("-");
                if (hyphenIndex >= 2 && hyphenIndex <= 6) {
                    const maybeCode = left.slice(0, hyphenIndex).trim().toUpperCase();
                    if (/^[A-Z0-9]{2,8}$/.test(maybeCode)) {
                        organizationCode = maybeCode;
                        organizationName = left.slice(hyphenIndex + 1).trim();
                    }
                }

                return {
                    organizationCode,
                    organizationName: organizationName || null,
                    orgUnitCode: right || null,
                    orgUnitName: organizationName || null,
                    source: company
                };
            };

            const tryAutoSelectOrganization = (row) => {
                const parsed = parseCompanyLocation(row.company);
                const company = parsed.source;
                const normalizedCompany = normalizeText(company);
                const forcedCodes = [];
                if (parsed.organizationCode) {
                    forcedCodes.push(parsed.organizationCode);
                }
                if (normalizedCompany.includes("ministerstvo obrany")
                    || normalizedCompany.includes("armada ceske republiky")
                    || normalizedCompany.includes("armáda české republiky")
                    || normalizedCompany.includes("acr")) {
                    forcedCodes.push("MO");
                } else if (normalizedCompany.includes("gordic")) {
                    forcedCodes.push("DOD");
                }

                const selected = findOptionByCodeOrText(orgSelect, parsed.organizationName || company, forcedCodes);
                if (selected) {
                    clearGeneratedOption(orgSelect, orgCreateHint);
                    orgSelect.value = selected;
                    return;
                }

                if (parsed.organizationName || company) {
                    ensureGeneratedOption(orgSelect, orgCreateHint, parsed.organizationName || company);
                } else if (orgSelect.options.length > 0) {
                    clearGeneratedOption(orgSelect, orgCreateHint);
                    const fallback = findOptionByCodeOrText(orgSelect, "", ["MO"]);
                    orgSelect.value = fallback || orgSelect.options[0].value;
                }
            };

            const tryAutoSelectOrgUnit = (row) => {
                const parsedCompany = parseCompanyLocation(row.company);
                const departmentRaw = (row.department || "").toString().trim();
                const preferredCodes = [];
                if (parsedCompany.orgUnitCode) {
                    preferredCodes.push(parsedCompany.orgUnitCode);
                }
                if (departmentRaw && /^[0-9]+$/.test(departmentRaw)) {
                    preferredCodes.push(departmentRaw);
                }

                const selected = findOptionByCodeOrText(
                    orgUnitSelect,
                    parsedCompany.orgUnitName || departmentRaw,
                    preferredCodes);
                if (selected) {
                    clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                    orgUnitSelect.value = selected;
                    return;
                }

                const generatedCode = parsedCompany.orgUnitCode || (/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
                const generatedName = parsedCompany.orgUnitName || (!/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
                if (generatedCode || generatedName) {
                    ensureGeneratedOrgUnitOption(generatedCode, generatedName);
                } else {
                    clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
                    orgUnitSelect.value = "";
                }
            };

            const renderStatusRow = (text, type = "info") => {
                currentResults = [];
                activeIndex = -1;
                results.innerHTML = "";

                const status = document.createElement("div");
                status.className = `office-search-info ${type}`.trim();
                status.textContent = text;
                status.setAttribute("role", "status");
                status.setAttribute("aria-live", "polite");
                results.appendChild(status);
                panel.hidden = false;
                mountFloatingPanel(panel, anchor, {
                    gap: 6,
                    flipVertical: true,
                    kind: "ad-search",
                    matchWidth: true
                });
            };

            const fillFromResult = (row) => {
                if (!row.canSelect) {
                    renderStatusRow(row.disabledReason || "Tuto osobu nelze vybrat.", "error");
                    return;
                }

                guidInput.value = row.guidAd || "";
                adLoginInput.value = row.adLogin || "";
                adCompanyInput.value = row.company || "";
                adDepartmentInput.value = row.department || "";
                jmenoInput.value = row.jmeno || "";
                prijmeniInput.value = row.prijmeni || "";
                titulInput.value = row.titul || "";
                emailInput.value = row.email || "";
                tryAutoSelectOrganization(row);
                tryAutoSelectOrgUnit(row);
                queryHidden.value = queryInput.value.trim();
                queryInput.value = row.email ? `${row.displayName} <${row.email}>` : row.displayName;
                submitButton.disabled = false;
                closePanel();
            };

            const renderResults = () => {
                results.innerHTML = "";

                if (currentResults.length === 0) {
                    closePanel();
                    return;
                }

                currentResults.forEach((row, index) => {
                    const button = document.createElement("button");
                    button.type = "button";
                    button.className = "office-search-item";
                    button.dataset.index = String(index);
                    button.setAttribute("role", "option");
                    if (index === activeIndex) {
                        button.classList.add("active");
                    }
                    if (!row.canSelect) {
                        button.classList.add("disabled");
                        button.disabled = true;
                    }

                    const primary = document.createElement("span");
                    primary.className = "office-search-primary";
                    primary.textContent = row.displayName || `${row.jmeno || ""} ${row.prijmeni || ""}`.trim();
                    button.appendChild(primary);

                    const secondary = document.createElement("span");
                    secondary.className = "office-search-secondary";
                    const orgPart = `${row.company || "-"} / ${row.department || "-"}`;
                    secondary.textContent = row.email ? `${row.email} | ${orgPart}` : orgPart;
                    button.appendChild(secondary);

                    if (!row.canSelect && row.disabledReason) {
                        const reason = document.createElement("span");
                        reason.className = "office-search-warning";
                        reason.textContent = row.disabledReason;
                        button.appendChild(reason);
                    }

                    results.appendChild(button);
                });

                panel.hidden = false;
                mountFloatingPanel(panel, anchor, {
                    gap: 6,
                    flipVertical: true,
                    kind: "ad-search",
                    matchWidth: true
                });
            };

            const performSearch = async () => {
                const query = queryInput.value.trim();
                queryHidden.value = query;
                clearSelection();

                if (query.length < 1) {
                    if (adIsUnavailable) {
                        renderStatusRow(adUnavailableMessage, "error");
                    } else {
                        results.innerHTML = "";
                        closePanel();
                    }
                    return;
                }

                renderStatusRow("Vyhledávám v Active Directory...");

                try {
                    const response = await fetch(`${searchUrl}?q=${encodeURIComponent(query)}`, {
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });

                    if (!response.ok) {
                        throw new Error(`HTTP ${response.status}`);
                    }

                    const payload = await response.json();
                    if (!payload.available) {
                        adAvailabilityKnown = true;
                        adIsUnavailable = true;
                        renderStatusRow(payload.message || adUnavailableMessage, "error");
                        return;
                    }

                    adAvailabilityKnown = true;
                    adIsUnavailable = false;
                    currentResults = Array.isArray(payload.results) ? payload.results.slice(0, 5) : [];
                    activeIndex = currentResults.length > 0 ? 0 : -1;
                    if (currentResults.length > 0) {
                        renderResults();
                    } else {
                        renderStatusRow("Žádná shoda.", "empty");
                    }
                } catch (error) {
                    adAvailabilityKnown = true;
                    adIsUnavailable = true;
                    renderStatusRow(adUnavailableMessage, "error");
                    console.error("AD search failed", error);
                }
            };

            const debouncedAdSearch = debounce(performSearch, 220);

            const probeAvailability = async () => {
                if (adAvailabilityKnown) {
                    if (adIsUnavailable) {
                        renderStatusRow(adUnavailableMessage, "error");
                    }
                    return;
                }

                try {
                    const response = await fetch(`${searchUrl}?q=${encodeURIComponent("__pmtracker_probe__")}`, {
                        headers: { "X-Requested-With": "XMLHttpRequest" }
                    });
                    if (!response.ok) {
                        throw new Error(`HTTP ${response.status}`);
                    }

                    const payload = await response.json();
                    adAvailabilityKnown = true;
                    adIsUnavailable = !payload.available;
                    if (adIsUnavailable) {
                        renderStatusRow(payload.message || adUnavailableMessage, "error");
                    }
                } catch (error) {
                    adAvailabilityKnown = true;
                    adIsUnavailable = true;
                    renderStatusRow(adUnavailableMessage, "error");
                    console.error("AD availability probe failed", error);
                }
            };

            queryInput.addEventListener("input", () => {
                debouncedAdSearch();
            });

            queryInput.addEventListener("focus", () => {
                probeAvailability();
                if (currentResults.length > 0) {
                    panel.hidden = false;
                    mountFloatingPanel(panel, anchor, {
                        gap: 6,
                        flipVertical: true,
                        kind: "ad-search",
                        matchWidth: true
                    });
                }
            });

            queryInput.addEventListener("keydown", (event) => {
                if (event.key === "Escape") {
                    event.preventDefault();
                    event.stopPropagation();
                    closePanel();
                    return;
                }

                if (event.key === "ArrowDown") {
                    if (currentResults.length === 0) {
                        return;
                    }
                    event.preventDefault();
                    activeIndex = Math.min(activeIndex + 1, currentResults.length - 1);
                    renderResults();
                    return;
                }

                if (event.key === "ArrowUp") {
                    if (currentResults.length === 0) {
                        return;
                    }
                    event.preventDefault();
                    activeIndex = Math.max(activeIndex - 1, 0);
                    renderResults();
                    return;
                }

                if (event.key === "Enter" && activeIndex >= 0 && currentResults[activeIndex]) {
                    event.preventDefault();
                    fillFromResult(currentResults[activeIndex]);
                }
            });

            results.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                const button = target.closest("[data-index]");
                if (!(button instanceof HTMLElement)) {
                    return;
                }

                const index = Number.parseInt(button.dataset.index || "-1", 10);
                if (!Number.isFinite(index) || index < 0 || index >= currentResults.length) {
                    return;
                }

                fillFromResult(currentResults[index]);
            });

            if (form instanceof HTMLFormElement) {
                form.addEventListener("submit", (event) => {
                    if (guidInput.value && !submitButton.disabled) {
                        return;
                    }

                    event.preventDefault();
                    renderStatusRow("Nejprve vyberte osobu z AD výsledků.", "empty");
                    queryInput.focus();
                });
            }

            document.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
                    closePanel();
                }
            });
        });
    }

    function initExternalLinksEditors(scope) {
        scope.querySelectorAll("[data-external-links-editor]").forEach((editor) => {
            if (!(editor instanceof HTMLElement) || editor.dataset.externalLinksReady === "true") {
                return;
            }

            const rowsContainer = editor.querySelector("[data-external-links]");
            const addButton = editor.querySelector("[data-external-add]");
            const template = editor.querySelector("template[data-external-template]");
            if (!(rowsContainer instanceof HTMLElement)
                || !(addButton instanceof HTMLButtonElement)
                || !(template instanceof HTMLTemplateElement)) {
                return;
            }

            editor.dataset.externalLinksReady = "true";
            const rowNamePattern = /ExterniVazby\[\d+\]\./g;
            const estimatedPriceTypes = new Set(["PMP", "PNF"]);

            const syncEstimatedPriceField = (row) => {
                if (!(row instanceof HTMLElement)) {
                    return;
                }

                const typeSelect = row.querySelector("[data-external-type-select]");
                const priceField = row.querySelector("[data-external-price-field]");
                const priceInput = row.querySelector("[data-external-price-input]");
                if (!(typeSelect instanceof HTMLSelectElement)
                    || !(priceField instanceof HTMLElement)
                    || !(priceInput instanceof HTMLInputElement)) {
                    return;
                }

                const shouldShow = estimatedPriceTypes.has(String(typeSelect.value || "").trim().toUpperCase());
                priceField.hidden = !shouldShow;
                priceInput.disabled = !shouldShow;
                if (!shouldShow) {
                    priceInput.value = "";
                }
            };

            const reindexRows = () => {
                const rows = Array.from(rowsContainer.querySelectorAll("[data-external-row]"))
                    .filter((item) => item instanceof HTMLElement);
                rows.forEach((row, index) => {
                    row.querySelectorAll("[name]").forEach((field) => {
                        if (!(field instanceof HTMLElement)) {
                            return;
                        }

                        const name = field.getAttribute("name");
                        if (!name) {
                            return;
                        }

                        field.setAttribute("name", name.replace(rowNamePattern, `ExterniVazby[${index}].`));
                    });
                });
            };

            const buildRowFromTemplate = (index) => {
                const html = template.innerHTML.replace(/__index__/g, String(index)).trim();
                if (!html) {
                    return null;
                }

                const wrapper = document.createElement("div");
                wrapper.innerHTML = html;
                const row = wrapper.firstElementChild;
                return row instanceof HTMLElement ? row : null;
            };

            addButton.addEventListener("click", () => {
                const index = rowsContainer.querySelectorAll("[data-external-row]").length;
                const row = buildRowFromTemplate(index);
                if (!(row instanceof HTMLElement)) {
                    return;
                }

                rowsContainer.appendChild(row);
                initCustomDatePickers(row);
                syncEstimatedPriceField(row);
                reindexRows();
            });

            rowsContainer.querySelectorAll("[data-external-row]").forEach((row) => {
                if (row instanceof HTMLElement) {
                    syncEstimatedPriceField(row);
                }
            });

            rowsContainer.addEventListener("change", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                const typeSelect = target.closest("[data-external-type-select]");
                if (!(typeSelect instanceof HTMLSelectElement)) {
                    return;
                }

                syncEstimatedPriceField(typeSelect.closest("[data-external-row]"));
            });

            rowsContainer.addEventListener("click", (event) => {
                const target = event.target;
                if (!(target instanceof Element)) {
                    return;
                }

                const removeButton = target.closest("[data-external-remove]");
                if (!(removeButton instanceof HTMLButtonElement)) {
                    return;
                }

                const row = removeButton.closest("[data-external-row]");
                if (!(row instanceof HTMLElement)) {
                    return;
                }

                row.remove();
                reindexRows();
            });

            reindexRows();
        });
    }

    function initCollabPickers(scope) {
        scope.querySelectorAll(".collab-picker").forEach((wrapper) => {
            if (!(wrapper instanceof HTMLElement) || wrapper.dataset.collabPickerReady === "true") {
                return;
            }

            const search = wrapper.querySelector("[data-collab-search]");
            const optionsContainer = wrapper.querySelector("[data-collab-options]");
            if (!(search instanceof HTMLInputElement) || !(optionsContainer instanceof HTMLElement)) {
                return;
            }

            wrapper.dataset.collabPickerReady = "true";
            const options = Array.from(optionsContainer.querySelectorAll("[data-collab-option]"))
                .filter((item) => item instanceof HTMLElement);

            options.forEach((item, index) => {
                item.dataset.collabOrder = String(index);
            });

            const applySearch = () => {
                const query = search.value || "";
                const normalizedQuery = normalizeSearchText(query);

                const scored = options
                    .map((option) => {
                        const label = option.dataset.collabLabel || option.textContent || "";
                        const score = normalizedQuery ? scoreSearchCandidate(normalizedQuery, label) : 1;
                        const order = Number.parseInt(option.dataset.collabOrder || "0", 10);
                        return { option, score, order };
                    })
                    .sort((a, b) => {
                        if (!normalizedQuery) {
                            return a.order - b.order;
                        }
                        return b.score - a.score || a.order - b.order;
                    });

                scored.forEach((row) => {
                    row.option.hidden = normalizedQuery.length > 0 && row.score <= 0;
                    optionsContainer.appendChild(row.option);
                });
            };

            const debouncedApply = debounce(applySearch, 120);
            search.addEventListener("input", () => {
                debouncedApply();
            });

            applySearch();
        });
    }

    function initRecordOwnerAutofill(scope) {
        scope.querySelectorAll('form[data-record-owner-autofill="true"]').forEach((form) => {
            if (!(form instanceof HTMLFormElement) || form.dataset.recordOwnerAutofillReady === "true") {
                return;
            }

            const subsystemSelect = form.querySelector("[data-record-subsystem-select]");
            const ownerPicker = form.querySelector("[data-record-owner-picker]");
            const ownerInput = ownerPicker?.querySelector("[data-person-picker-input]");
            const ownerHiddenInput = ownerPicker?.querySelector("[data-person-picker-hidden]");
            const ownerSource = ownerPicker?.querySelector("[data-person-picker-source]");
            if (!(subsystemSelect instanceof HTMLSelectElement)
                || !(ownerPicker instanceof HTMLElement)) {
                return;
            }

            form.dataset.recordOwnerAutofillReady = "true";

            const clearOwnerPicker = () => {
                if (ownerHiddenInput instanceof HTMLInputElement) {
                    ownerHiddenInput.value = "";
                }

                if (ownerInput instanceof HTMLInputElement) {
                    ownerInput.value = "";
                    ownerInput.setCustomValidity("");
                }
            };

            const resolveSelectedOwnerId = () => {
                const selectedOption = subsystemSelect.selectedOptions[0];
                if (!(selectedOption instanceof HTMLOptionElement)) {
                    return "";
                }

                const rawOwnerId = (selectedOption.dataset.ownerId || "").trim();
                const parsedOwnerId = Number.parseInt(rawOwnerId, 10);
                if (!Number.isInteger(parsedOwnerId) || parsedOwnerId <= 0) {
                    return "";
                }

                return String(parsedOwnerId);
            };

            const findOwnerItem = (ownerId) => {
                if (!ownerId || !(ownerSource instanceof HTMLElement)) {
                    return null;
                }

                return Array.from(ownerSource.querySelectorAll("[data-id]"))
                    .find((item) => item instanceof HTMLElement && (item.dataset.id || "").trim() === ownerId);
            };

            const applyOwnerFromSubsystem = () => {
                const ownerId = resolveSelectedOwnerId();
                if (!ownerId) {
                    clearOwnerPicker();
                    return;
                }

                const ownerItem = findOwnerItem(ownerId);
                if (!(ownerItem instanceof HTMLElement)) {
                    clearOwnerPicker();
                    return;
                }

                ownerPicker.dispatchEvent(new CustomEvent("person-picker:select-id", {
                    bubbles: true,
                    detail: {
                        id: ownerId,
                        source: "auto"
                    }
                }));

                if (ownerHiddenInput instanceof HTMLInputElement) {
                    ownerHiddenInput.value = ownerId;
                }

                if (ownerInput instanceof HTMLInputElement) {
                    const label = (ownerItem.dataset.label || "").trim();
                    const email = (ownerItem.dataset.email || "").trim();
                    ownerInput.value = email ? `${label} <${email}>` : label;
                    ownerInput.setCustomValidity("");
                }
            };

            subsystemSelect.addEventListener("change", () => {
                applyOwnerFromSubsystem();
            });

            form.addEventListener("submit", () => {
                if (ownerHiddenInput instanceof HTMLInputElement
                    && !ownerHiddenInput.value.trim()) {
                    applyOwnerFromSubsystem();
                }
            });

            if (ownerHiddenInput instanceof HTMLInputElement
                && !ownerHiddenInput.value.trim()) {
                applyOwnerFromSubsystem();
            }
        });
    }

    function initMeetingNumberValidation(scope) {
        scope.querySelectorAll('form[data-meeting-number-unique="true"]').forEach((form) => {
            if (!(form instanceof HTMLFormElement) || form.dataset.meetingNumberValidationReady === "true") {
                return;
            }

            const input = form.querySelector("[data-meeting-number-input]");
            if (!(input instanceof HTMLInputElement)) {
                return;
            }

            const warning = form.querySelector("[data-meeting-number-warning]");
            const submitButton = form.querySelector('button[type="submit"]');
            const existingNumbers = new Set(
                (input.dataset.existingMeetingNumbers || "")
                    .split(",")
                    .map((value) => Number.parseInt(value.trim(), 10))
                    .filter((value) => Number.isInteger(value) && value > 0)
            );
            const currentMeetingNumber = Number.parseInt((input.dataset.currentMeetingNumber || "").trim(), 10);
            if (Number.isInteger(currentMeetingNumber) && currentMeetingNumber > 0) {
                existingNumbers.delete(currentMeetingNumber);
            }

            form.dataset.meetingNumberValidationReady = "true";

            const setWarningState = (isDuplicate) => {
                if (warning instanceof HTMLElement) {
                    warning.hidden = !isDuplicate;
                }

                if (isDuplicate) {
                    input.classList.add("field-invalid");
                } else {
                    input.classList.remove("field-invalid");
                }

                if (submitButton instanceof HTMLButtonElement) {
                    submitButton.disabled = isDuplicate;
                }
            };

            const validate = () => {
                const parsed = Number.parseInt((input.value || "").trim(), 10);
                const isDuplicate = Number.isInteger(parsed) && existingNumbers.has(parsed);
                if (isDuplicate) {
                    input.setCustomValidity("Jednání s tímto číslem už v projektu existuje.");
                } else {
                    input.setCustomValidity("");
                }
                setWarningState(isDuplicate);
            };

            input.addEventListener("input", validate);
            input.addEventListener("change", validate);
            form.addEventListener("submit", validate);
            validate();
        });
    }

    function initRecordFormTabs(scope) {
        scope.querySelectorAll('form[data-record-form-tabs="true"]').forEach((form) => {
            if (!(form instanceof HTMLFormElement) || form.dataset.recordFormTabsReady === "true") {
                return;
            }

            form.dataset.recordFormTabsReady = "true";
            const activeTabInput = form.querySelector("[data-record-active-tab-input]");
            const initialTab = activeTabInput instanceof HTMLInputElement ? activeTabInput.value : "basic";
            setRecordFormTab(form, initialTab);

            form.querySelectorAll("[data-record-modal-tab]").forEach((tabButton) => {
                if (!(tabButton instanceof HTMLButtonElement)) {
                    return;
                }

                tabButton.addEventListener("click", () => {
                    const tabKey = tabButton.dataset.recordModalTab || "basic";
                    setRecordFormTab(form, tabKey);
                });
            });
        });
    }

    class ScheduleTimelineEngine {
        static computePlanAndActual(state, startDate) {
            const plan = [];
            const actual = [];
            let planCursor = new Date(startDate.getTime());
            let actualCursor = new Date(startDate.getTime());

            state.forEach((item) => {
                const planStart = new Date(planCursor.getTime());
                const planEnd = addCalendarDays(planStart, item.duration);
                plan.push({ start: planStart, end: planEnd });
                planCursor = new Date(planEnd.getTime());

                const actualStart = new Date(actualCursor.getTime());
                const actualEnd = addCalendarDays(actualStart, Math.max(0, item.duration + item.delay));
                actual.push({ start: actualStart, end: actualEnd });
                actualCursor = new Date(actualEnd.getTime());
            });

            return { plan, actual };
        }

        static buildScale(startDate, deadlineDate, actualEndDate) {
            const startStamp = toUtcDayStamp(startDate);
            const axisEndStamp = Math.max(
                startStamp,
                toUtcDayStamp(deadlineDate),
                toUtcDayStamp(actualEndDate));
            const totalDays = Math.max(1, Math.round((axisEndStamp - startStamp) / msPerDay));
            return {
                totalDays,
                axisEndDate: addCalendarDays(startDate, totalDays)
            };
        }

        static toPercent(valueDate, axisStart, totalDays) {
            const days = diffCalendarDays(valueDate, axisStart);
            return Math.max(0, Math.min(100, (days * 100) / totalDays));
        }

        static toWidthPercent(startDate, endDate, totalDays) {
            const days = Math.max(0, diffCalendarDays(endDate, startDate));
            return Math.max(0, Math.min(100, (days * 100) / totalDays));
        }
    }

    class RecordSchedulePlanner {
        constructor(form, editor) {
            this.form = form;
            this.editor = editor;
            this.startInput = form.querySelector('input[name="DatumZalozeni"]');
            this.deadlineInput = form.querySelector('input[name="TerminUkonceni"]');
            this.summaryDeadline = editor.querySelector("[data-schedule-summary-deadline]");
            this.summaryBaseline = editor.querySelector("[data-schedule-summary-baseline]");
            this.summaryShifted = editor.querySelector("[data-schedule-summary-shifted]");
            this.summaryDuration = editor.querySelector("[data-schedule-summary-duration]");
            this.summaryDelay = editor.querySelector("[data-schedule-summary-delay]");
            this.summaryState = editor.querySelector("[data-schedule-summary-state]");
            this.summaryOverrun = editor.querySelector("[data-schedule-summary-overrun]");
            this.statusLine = editor.querySelector(".schedule-status-line");
            this.timelineAxes = Array.from(editor.querySelectorAll("[data-schedule-axis]"))
                .filter((node) => node instanceof HTMLElement);
            this.ganttDeadlineMarkers = Array.from(editor.querySelectorAll("[data-schedule-gantt-deadline]"))
                .filter((node) => node instanceof HTMLElement);
            this.ganttPlannedSegments = Array.from(editor.querySelectorAll("[data-schedule-gantt-step-planned]"))
                .filter((node) => node instanceof HTMLElement);
            this.ganttActualSegments = Array.from(editor.querySelectorAll("[data-schedule-gantt-step-actual]"))
                .filter((node) => node instanceof HTMLElement);
            this.rows = Array.from(editor.querySelectorAll("[data-schedule-step-row]"))
                .filter((row) => row instanceof HTMLTableRowElement)
                .map((row, index) => ({
                    row,
                    index,
                    durationInput: row.querySelector("[data-schedule-duration]"),
                    delayInput: row.querySelector("[data-schedule-delay]"),
                    dateInput: row.querySelector("[data-schedule-date]"),
                    delayDateInput: row.querySelector("[data-schedule-delay-date]"),
                    baselineCell: row.querySelector("[data-schedule-baseline]"),
                    shiftedCell: row.querySelector("[data-schedule-shifted]"),
                    durationInc: row.querySelector("[data-schedule-duration-inc]"),
                    durationDec: row.querySelector("[data-schedule-duration-dec]"),
                    delayInc: row.querySelector("[data-schedule-delay-inc]"),
                    delayDec: row.querySelector("[data-schedule-delay-dec]")
                }));
        }

        isReady() {
            return this.form instanceof HTMLFormElement
                && this.editor instanceof HTMLElement
                && this.startInput instanceof HTMLInputElement
                && this.rows.length > 0;
        }

        readState() {
            return this.rows.map((entry) => {
                const duration = this.normalizeInt(entry.durationInput);
                const delay = this.normalizeSignedInt(entry.delayInput);
                return { duration, delay };
            });
        }

        writeState(state) {
            state.forEach((item, index) => {
                const entry = this.rows[index];
                if (!entry) {
                    return;
                }
                if (entry.durationInput instanceof HTMLInputElement) {
                    entry.durationInput.value = String(item.duration);
                }
                if (entry.delayInput instanceof HTMLInputElement) {
                    entry.delayInput.value = String(item.delay);
                }
            });
        }

        getStartDate() {
            if (!(this.startInput instanceof HTMLInputElement)) {
                return new Date();
            }

            return parseIsoDate(this.startInput.value) || new Date();
        }

        getDeadlineDate(startDate) {
            if (!(this.deadlineInput instanceof HTMLInputElement)) {
                return startDate;
            }

            return parseIsoDate(this.deadlineInput.value) || startDate;
        }

        normalizeInt(input) {
            return this.normalizeIntWithMinimum(input, 0);
        }

        normalizeSignedInt(input) {
            if (!(input instanceof HTMLInputElement)) {
                return 0;
            }
            const parsed = Number.parseInt((input.value || "").trim(), 10);
            if (!Number.isFinite(parsed)) {
                return 0;
            }
            return parsed;
        }

        normalizeIntWithMinimum(input, minimum) {
            if (!(input instanceof HTMLInputElement)) {
                return Math.max(minimum, 0);
            }
            const parsed = Number.parseInt((input.value || "").trim(), 10);
            if (!Number.isFinite(parsed)) {
                return Math.max(minimum, 0);
            }
            return Math.max(minimum, parsed);
        }

        setDateInputValue(input, value) {
            if (!(input instanceof HTMLInputElement)) {
                return;
            }

            const isoValue = formatIsoDate(value);
            input.value = isoValue;
            const dateField = input.closest("[data-app-date-field]");
            if (!(dateField instanceof HTMLElement)) {
                return;
            }

            const display = dateField.querySelector("[data-app-date-display]");
            if (display instanceof HTMLInputElement) {
                display.value = formatDisplayDate(value);
            }
        }

        computePlanAndActual(state, startDate) {
            return ScheduleTimelineEngine.computePlanAndActual(state, startDate);
        }

        recalcFromDuration(stepIndex) {
            if (!Number.isInteger(stepIndex) || stepIndex < 0) {
                this.recalcAll();
                return;
            }

            this.recalcAll();
        }

        recalcFromDelay(stepIndex) {
            if (!Number.isInteger(stepIndex) || stepIndex < 0) {
                this.recalcAll();
                return;
            }

            this.recalcAll();
        }

        recalcFromDate(stepIndex) {
            const entry = this.rows[stepIndex];
            if (!entry || !(entry.dateInput instanceof HTMLInputElement)) {
                this.recalcAll();
                return;
            }

            const state = this.readState();
            const startDate = this.getStartDate();
            const { plan } = this.computePlanAndActual(state, startDate);
            const previousPlanEnd = stepIndex === 0
                ? startDate
                : plan[stepIndex - 1]?.end || startDate;
            const selectedDate = parseIsoDate(entry.dateInput.value) || previousPlanEnd;
            const computedDuration = Math.max(0, diffCalendarDays(selectedDate, previousPlanEnd));
            state[stepIndex] = { ...state[stepIndex], duration: computedDuration };
            this.writeState(state);
            this.recalcAll();
        }

        recalcFromDelayDate(stepIndex) {
            const entry = this.rows[stepIndex];
            if (!entry || !(entry.delayDateInput instanceof HTMLInputElement)) {
                this.recalcAll();
                return;
            }

            const state = this.readState();
            const startDate = this.getStartDate();
            const { plan } = this.computePlanAndActual(state, startDate);
            const planEnd = plan[stepIndex]?.end || startDate;
            const selectedDate = parseIsoDate(entry.delayDateInput.value) || planEnd;
            const computedDelay = diffCalendarDays(selectedDate, planEnd);
            state[stepIndex] = { ...state[stepIndex], delay: computedDelay };
            this.writeState(state);
            this.recalcAll();
        }

        renderSummary(plan, actual, state, startDate, deadlineDate) {
            const baselineEnd = plan.length > 0 ? plan[plan.length - 1].end : startDate;
            const shiftedEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
            const totalDuration = state.reduce((sum, item) => sum + item.duration, 0);
            const totalDelay = state.reduce((sum, item) => sum + item.delay, 0);
            const stihame = toUtcDayStamp(shiftedEnd) <= toUtcDayStamp(deadlineDate);
            const overrunDays = stihame ? 0 : diffCalendarDays(shiftedEnd, deadlineDate);

            if (this.summaryDeadline instanceof HTMLElement) {
                this.summaryDeadline.textContent = formatDisplayDate(deadlineDate);
            }
            if (this.summaryBaseline instanceof HTMLElement) {
                this.summaryBaseline.textContent = formatDisplayDate(baselineEnd);
            }
            if (this.summaryShifted instanceof HTMLElement) {
                this.summaryShifted.textContent = formatDisplayDate(shiftedEnd);
            }
            if (this.summaryDuration instanceof HTMLElement) {
                this.summaryDuration.textContent = String(totalDuration);
            }
            if (this.summaryDelay instanceof HTMLElement) {
                this.summaryDelay.textContent = totalDelay > 0 ? `+${totalDelay}` : String(totalDelay);
            }
            if (this.summaryState instanceof HTMLElement) {
                this.summaryState.textContent = stihame ? "Stíháme" : "Nestíháme";
            }
            if (this.summaryOverrun instanceof HTMLElement) {
                this.summaryOverrun.textContent = stihame ? "" : `(+${overrunDays} dnů)`;
            }
            if (this.statusLine instanceof HTMLElement) {
                this.statusLine.classList.toggle("ok", stihame);
                this.statusLine.classList.toggle("late", !stihame);
            }

            this.renderMiniGantt(plan, actual, startDate, deadlineDate);
        }

        renderMiniGantt(plan, actual, startDate, deadlineDate) {
            const actualEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
            const { totalDays, axisEndDate } = ScheduleTimelineEngine.buildScale(startDate, deadlineDate, actualEnd);
            const deadlinePercent = ScheduleTimelineEngine.toPercent(deadlineDate, startDate, totalDays);
            const formatPercent = (value) => `${Number.isFinite(value) ? value.toFixed(4) : "0.0000"}%`;
            const formatSegmentWidth = (value) => {
                if (!Number.isFinite(value) || value <= 0) {
                    return "0%";
                }

                // Add a tiny overlap to avoid visible sub-pixel seams between adjacent segments.
                return `calc(${value.toFixed(4)}% + 1px)`;
            };
            const renderContinuousSegments = (segments, items) => {
                let previousRight = 0;
                segments.forEach((segment, index) => {
                    const item = items[index];
                    if (!item) {
                        segment.style.left = "0%";
                        segment.style.width = "0%";
                        return;
                    }

                    const rawLeft = ScheduleTimelineEngine.toPercent(item.start, startDate, totalDays);
                    const rawRight = ScheduleTimelineEngine.toPercent(item.end, startDate, totalDays);
                    const left = index === 0 ? rawLeft : Math.max(previousRight, rawLeft);
                    const right = Math.max(left, rawRight);
                    const width = Math.max(0, right - left);

                    segment.style.left = formatPercent(left);
                    segment.style.width = formatSegmentWidth(width);
                    previousRight = right;
                });
            };

            this.ganttDeadlineMarkers.forEach((marker) => {
                marker.style.left = formatPercent(deadlinePercent);
            });

            this.timelineAxes.forEach((axis) => {
                renderTimelineAxis(axis, startDate, axisEndDate);
            });

            renderContinuousSegments(this.ganttPlannedSegments, plan);
            renderContinuousSegments(this.ganttActualSegments, actual);

            queueRainbowSegmentRender(this.editor);
        }

        renderStepRows(plan, actual, state) {
            this.rows.forEach((entry, index) => {
                const planEnd = plan[index]?.end;
                const actualEnd = actual[index]?.end;
                if (entry.baselineCell instanceof HTMLElement && planEnd instanceof Date) {
                    entry.baselineCell.textContent = formatDisplayDate(planEnd);
                }
                if (entry.shiftedCell instanceof HTMLElement && actualEnd instanceof Date) {
                    entry.shiftedCell.textContent = formatDisplayDate(actualEnd);
                }
                if (entry.dateInput instanceof HTMLInputElement && planEnd instanceof Date) {
                    this.setDateInputValue(entry.dateInput, planEnd);
                }
                if (entry.delayDateInput instanceof HTMLInputElement && actualEnd instanceof Date) {
                    this.setDateInputValue(entry.delayDateInput, actualEnd);
                }

            });
        }

        recalcAll() {
            const state = this.readState();
            const startDate = this.getStartDate();
            const deadlineDate = this.getDeadlineDate(startDate);
            const { plan, actual } = this.computePlanAndActual(state, startDate);
            this.renderStepRows(plan, actual, state);
            this.renderSummary(plan, actual, state, startDate, deadlineDate);
        }

        bindNumericStepper(button, input, delta, onChange) {
            if (!(button instanceof HTMLButtonElement) || !(input instanceof HTMLInputElement)) {
                return;
            }

            let repeatDelayTimer = null;
            let repeatIntervalTimer = null;
            let suppressClickOnce = false;
            const repeatDelayMs = 350;
            const repeatIntervalMs = 70;

            const stopRepeat = () => {
                if (repeatDelayTimer !== null) {
                    window.clearTimeout(repeatDelayTimer);
                    repeatDelayTimer = null;
                }
                if (repeatIntervalTimer !== null) {
                    window.clearInterval(repeatIntervalTimer);
                    repeatIntervalTimer = null;
                }
            };

            const stepOnce = () => {
                if (button.disabled || input.disabled) {
                    return;
                }

                const isDelayInput = input.hasAttribute("data-schedule-delay");
                const currentValue = isDelayInput
                    ? this.normalizeSignedInt(input)
                    : this.normalizeInt(input);
                const nextValue = isDelayInput
                    ? currentValue + delta
                    : Math.max(0, currentValue + delta);
                input.value = String(nextValue);

                if (nextValue !== currentValue) {
                    onChange();
                }
            };

            button.addEventListener("mousedown", (event) => {
                if (!(event instanceof MouseEvent) || event.button !== 0) {
                    return;
                }

                event.preventDefault();
                suppressClickOnce = true;
                stepOnce();
                stopRepeat();
                repeatDelayTimer = window.setTimeout(() => {
                    repeatIntervalTimer = window.setInterval(() => {
                        stepOnce();
                    }, repeatIntervalMs);
                }, repeatDelayMs);
            });

            button.addEventListener("click", (event) => {
                if (suppressClickOnce) {
                    suppressClickOnce = false;
                    return;
                }

                stepOnce();
            });

            button.addEventListener("mouseup", stopRepeat);
            button.addEventListener("mouseleave", stopRepeat);
            button.addEventListener("blur", () => {
                stopRepeat();
                suppressClickOnce = false;
            });
            window.addEventListener("mouseup", (event) => {
                stopRepeat();
                if (!(event.target instanceof Element) || !button.contains(event.target)) {
                    suppressClickOnce = false;
                }
            });
        }

        bind() {
            this.rows.forEach((entry, index) => {
                if (entry.durationInput instanceof HTMLInputElement) {
                    entry.durationInput.addEventListener("input", () => this.recalcFromDuration(index));
                    entry.durationInput.addEventListener("change", () => {
                        entry.durationInput.value = String(this.normalizeInt(entry.durationInput));
                        this.recalcFromDuration(index);
                    });
                }

                if (entry.delayInput instanceof HTMLInputElement) {
                    entry.delayInput.addEventListener("input", () => this.recalcFromDelay(index));
                    entry.delayInput.addEventListener("change", () => {
                        entry.delayInput.value = String(this.normalizeSignedInt(entry.delayInput));
                        this.recalcFromDelay(index);
                    });
                }

                if (entry.dateInput instanceof HTMLInputElement) {
                    entry.dateInput.addEventListener("change", () => this.recalcFromDate(index));
                }
                if (entry.delayDateInput instanceof HTMLInputElement) {
                    entry.delayDateInput.addEventListener("change", () => this.recalcFromDelayDate(index));
                }

                this.bindNumericStepper(entry.durationInc, entry.durationInput, +1, () => this.recalcFromDuration(index));
                this.bindNumericStepper(entry.durationDec, entry.durationInput, -1, () => this.recalcFromDuration(index));
                this.bindNumericStepper(entry.delayInc, entry.delayInput, +1, () => this.recalcFromDelay(index));
                this.bindNumericStepper(entry.delayDec, entry.delayInput, -1, () => this.recalcFromDelay(index));
            });

            if (this.startInput instanceof HTMLInputElement) {
                this.startInput.addEventListener("change", () => this.recalcAll());
            }
            if (this.deadlineInput instanceof HTMLInputElement) {
                this.deadlineInput.addEventListener("change", () => this.recalcAll());
            }
        }
    }

    function initRecordSchedulePlanner(scope) {
        if (!(scope instanceof HTMLElement || scope instanceof Document)) {
            return;
        }

        const forms = [];
        if (scope instanceof HTMLFormElement && scope.matches('form[data-record-schedule-form="true"]')) {
            forms.push(scope);
        }

        scope.querySelectorAll('form[data-record-schedule-form="true"]').forEach((form) => {
            if (form instanceof HTMLFormElement) {
                forms.push(form);
            }
        });

        forms.forEach((form) => {
            if (!(form instanceof HTMLFormElement) || form.dataset.recordScheduleReady === "true") {
                return;
            }

            const editor = form.querySelector("[data-record-schedule-editor]");
            const schedulePanel = form.querySelector("[data-record-schedule-panel]");
            if (!(editor instanceof HTMLElement)
                || (schedulePanel instanceof HTMLElement && schedulePanel.dataset.scheduleDisabled === "true")) {
                return;
            }

            const planner = new RecordSchedulePlanner(form, editor);
            if (!planner.isReady()) {
                return;
            }

            planner.bind();
            planner.recalcAll();
            form._recordSchedulePlanner = planner;
            form.dataset.recordScheduleReady = "true";
            queueRecordSchedulePlannerRecalc(form, 0);
        });
    }

    function initRecordFormEnhancements(scope) {
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
        initRecordSchedulePlanner(scope);
        initRecordEditorDirtyTracking(scope);
    }

    function shouldIgnoreRecordEditorField(name) {
        if (!name) {
            return true;
        }

        const normalized = String(name).trim().toLowerCase();
        if (!normalized) {
            return true;
        }

        return normalized === "__requestverificationtoken"
            || normalized === "presentation"
            || normalized === "returnurl"
            || normalized === "editortab";
    }

    function buildRecordEditorFormSnapshot(form) {
        if (!(form instanceof HTMLFormElement)) {
            return "";
        }

        const entries = [];
        const formData = new FormData(form);
        formData.forEach((value, key) => {
            if (shouldIgnoreRecordEditorField(key)) {
                return;
            }

            const normalizedValue = value instanceof File
                ? value.name
                : String(value ?? "");
            entries.push(`${key}=${normalizedValue}`);
        });

        entries.sort();
        return entries.join("&");
    }

    function markRecordEditorFormClean(form) {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
    }

    function isRecordEditorFormDirty(form) {
        if (!(form instanceof HTMLFormElement)) {
            return false;
        }

        return buildRecordEditorFormSnapshot(form) !== (form.dataset.recordEditorSnapshot || "");
    }

    function prepareRecordEditorFormNavigation(form) {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        form.dataset.recordEditorNavigating = "true";
        markRecordEditorFormClean(form);
    }

    function closeRecordEditorCloseGuard(options) {
        const settings = options || {};
        const restoreFocus = Boolean(settings.restoreFocus);
        const trigger = recordEditorState.closeGuardTrigger;

        if (recordEditorState.closeGuard instanceof HTMLElement) {
            recordEditorState.closeGuard.remove();
        }

        recordEditorState.closeGuard = null;
        recordEditorState.closeGuardTrigger = null;

        if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
            trigger.focus({ preventScroll: true });
        }
    }

    function promptRecordEditorDiscard(form, trigger) {
        if (!(form instanceof HTMLFormElement) || !isRecordEditorFormDirty(form)) {
            return Promise.resolve(true);
        }

        closeRecordEditorCloseGuard({ restoreFocus: false });

        return new Promise((resolve) => {
            const isModalForm = modalRoot instanceof HTMLElement && modalRoot.contains(form);
            const host = isModalForm ? getActiveModalContainer() : document.body;
            if (!(host instanceof HTMLElement)) {
                resolve(window.confirm("Máte neuložené změny. Chcete je zahodit?"));
                return;
            }

            const overlay = document.createElement("div");
            overlay.className = `record-editor-close-guard${isModalForm ? " record-editor-close-guard-modal" : ""}`;
            overlay.setAttribute("data-record-editor-close-guard", "true");

            const dialog = document.createElement("div");
            dialog.className = "record-editor-close-guard-dialog";
            dialog.setAttribute("role", "alertdialog");
            dialog.setAttribute("aria-modal", "true");
            dialog.setAttribute("tabindex", "-1");

            const title = document.createElement("h3");
            title.className = "record-editor-close-guard-title";
            title.textContent = "Máte neuložené změny.";
            dialog.appendChild(title);

            const text = document.createElement("p");
            text.className = "record-editor-close-guard-text";
            text.textContent = "Chcete pokračovat v úpravách, nebo změny zahodit?";
            dialog.appendChild(text);

            const actions = document.createElement("div");
            actions.className = "record-editor-close-guard-actions";

            const keepEditingButton = document.createElement("button");
            keepEditingButton.type = "button";
            keepEditingButton.className = "btn";
            keepEditingButton.textContent = "Pokračovat v úpravách";
            actions.appendChild(keepEditingButton);

            const discardButton = document.createElement("button");
            discardButton.type = "button";
            discardButton.className = "btn danger";
            discardButton.textContent = "Zahodit změny";
            actions.appendChild(discardButton);

            dialog.appendChild(actions);
            overlay.appendChild(dialog);

            const finish = (shouldDiscard, restoreFocus) => {
                closeRecordEditorCloseGuard({ restoreFocus });
                if (shouldDiscard) {
                    prepareRecordEditorFormNavigation(form);
                }
                resolve(shouldDiscard);
            };

            overlay.addEventListener("click", (event) => {
                if (event.target === overlay) {
                    finish(false, true);
                }
            });

            keepEditingButton.addEventListener("click", () => finish(false, true));
            discardButton.addEventListener("click", () => finish(true, false));

            host.appendChild(overlay);
            recordEditorState.closeGuard = overlay;
            recordEditorState.closeGuardTrigger = trigger instanceof HTMLElement ? trigger : null;

            window.requestAnimationFrame(() => {
                keepEditingButton.focus({ preventScroll: true });
            });
        });
    }

    async function requestRecordEditorModalClose(trigger) {
        const editorForm = modalRoot?.querySelector('form[data-record-editor-form="true"]');
        if (!(editorForm instanceof HTMLFormElement)) {
            closeModal();
            return;
        }

        const canClose = await promptRecordEditorDiscard(editorForm, trigger);
        if (canClose) {
            closeModal();
        }
    }

    async function requestRecordEditorPageCancel(trigger) {
        const editorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
        if (!(editorForm instanceof HTMLFormElement)) {
            const fallbackUrl = trigger instanceof HTMLElement
                ? trigger.getAttribute("data-record-editor-back-url") || window.location.href
                : window.location.href;
            window.location.assign(fallbackUrl);
            return;
        }

        const canClose = await promptRecordEditorDiscard(editorForm, trigger);
        if (!canClose) {
            return;
        }

        const targetUrl = editorForm.dataset.recordEditorBackUrl
            || (trigger instanceof HTMLElement ? trigger.getAttribute("data-record-editor-back-url") : "")
            || window.location.href;
        window.location.assign(targetUrl);
    }

    function initRecordEditorDirtyTracking(scope) {
        if (!(scope instanceof HTMLElement || scope instanceof Document)) {
            return;
        }

        scope.querySelectorAll('form[data-record-editor-form="true"]').forEach((form) => {
            if (!(form instanceof HTMLFormElement) || form.dataset.recordEditorDirtyReady === "true") {
                return;
            }

            form.dataset.recordEditorDirtyReady = "true";
            form.dataset.recordEditorNavigating = "false";

            form.addEventListener("submit", () => {
                form.dataset.recordEditorNavigating = "true";
            });

            window.requestAnimationFrame(() => {
                if (form.isConnected) {
                    markRecordEditorFormClean(form);
                    form.dataset.recordEditorNavigating = "false";
                }
            });
        });
    }

    function resolveRecordEditorTabForFieldKey(rawKey) {
        const normalizedKey = normalizeServerFieldKey(rawKey).toLowerCase();
        if (!normalizedKey) {
            return "";
        }

        if (normalizedKey.startsWith("externivazby[")) {
            return "external";
        }

        if (normalizedKey.startsWith("vybranispolupracovniciids")) {
            return "collaboration";
        }

        if (normalizedKey.startsWith("harmonogramhodnoty[")
            || normalizedKey.startsWith("uiharmonogramdatumy[")
            || normalizedKey.startsWith("uiharmonogramposunutedatumy[")) {
            return "schedule";
        }

        const basicPrefixes = [
            "kategorie",
            "typukolu",
            "stav",
            "nazev",
            "popis",
            "vlastnikid",
            "datumzalozeni",
            "terminukonceni",
            "subsystem",
            "jednaniidprocislo"
        ];

        return basicPrefixes.some((prefix) => normalizedKey.startsWith(prefix)) ? "basic" : "";
    }

    function normalizeServerFieldKey(rawKey) {
        if (!rawKey) {
            return "";
        }

        const key = String(rawKey).trim();
        const dotIndex = key.indexOf(".");
        if (dotIndex > 0) {
            const prefix = key.slice(0, dotIndex);
            if (/^[a-zA-Z][a-zA-Z0-9]*$/.test(prefix)
                && (prefix.toLowerCase() === "command" || prefix.toLowerCase().endsWith("command"))) {
                return key.slice(dotIndex + 1);
            }
        }

        return key;
    }

    function resolveErrorTarget(field, form) {
        if (!(field instanceof HTMLElement)) {
            return null;
        }

        if (field instanceof HTMLInputElement && field.type === "hidden") {
            if (field.matches("[data-person-picker-hidden]")) {
                const pickerInput = field.closest('[data-person-picker="single"]')?.querySelector("[data-person-picker-input]");
                if (pickerInput instanceof HTMLElement) {
                    return pickerInput;
                }
            }

            if (field.matches("[data-ad-guid], [data-ad-query-hidden]")) {
                const adInput = form.querySelector("[data-ad-query-input]");
                if (adInput instanceof HTMLElement) {
                    return adInput;
                }
            }

            if (field.matches("[data-app-date-value]")) {
                const displayInput = field.closest("[data-app-date-field]")?.querySelector("[data-app-date-display]");
                if (displayInput instanceof HTMLElement) {
                    return displayInput;
                }
            }

            if (field.matches("[data-app-time-value]")) {
                const displayInput = field.closest("[data-app-time-field]")?.querySelector("[data-app-time-display]");
                if (displayInput instanceof HTMLElement) {
                    return displayInput;
                }
            }
        }

        return field;
    }

    function clearModalFormErrors(form) {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        form.querySelectorAll(".modal-submit-summary").forEach((node) => node.remove());
        form.querySelectorAll(".field-error-message").forEach((node) => node.remove());
        form.querySelectorAll(".field-invalid").forEach((node) => {
            if (node instanceof HTMLElement) {
                node.classList.remove("field-invalid");
                node.removeAttribute("aria-invalid");
            }
        });
    }

    function findFieldByName(form, rawKey) {
        if (!(form instanceof HTMLFormElement)) {
            return null;
        }

        const normalizedKey = normalizeServerFieldKey(rawKey);
        if (!normalizedKey) {
            return null;
        }

        const controls = Array.from(form.querySelectorAll("[name]"))
            .filter((candidate) => candidate instanceof HTMLInputElement
                || candidate instanceof HTMLSelectElement
                || candidate instanceof HTMLTextAreaElement);

        const normalizedLower = normalizedKey.toLowerCase();
        const exactMatch = controls.find((candidate) => {
            const fieldName = candidate.getAttribute("name");
            return fieldName && fieldName.toLowerCase() === normalizedLower;
        });
        if (exactMatch instanceof HTMLElement) {
            return exactMatch;
        }

        const suffixMatch = controls.find((candidate) => {
            const fieldName = (candidate.getAttribute("name") || "").toLowerCase();
            return fieldName.endsWith(`.${normalizedLower}`);
        });
        return suffixMatch instanceof HTMLElement ? suffixMatch : null;
    }

    function renderModalFormErrors(form, payload) {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        clearModalFormErrors(form);

        const fieldErrors = payload && typeof payload === "object" && payload.fieldErrors && typeof payload.fieldErrors === "object"
            ? payload.fieldErrors
            : {};

        const summaryMessages = [];
        const invalidTargets = [];
        let firstInvalidTab = "";

        Object.entries(fieldErrors).forEach(([rawKey, messages]) => {
            if (!Array.isArray(messages) || messages.length === 0) {
                return;
            }

            const normalizedMessages = messages
                .map((value) => String(value || "").trim())
                .filter((value) => value.length > 0);

            if (normalizedMessages.length === 0) {
                return;
            }

            const field = findFieldByName(form, rawKey);
            if (!(field instanceof HTMLElement)) {
                if (!firstInvalidTab) {
                    firstInvalidTab = resolveRecordEditorTabForFieldKey(rawKey);
                }
                summaryMessages.push(...normalizedMessages);
                return;
            }

            if (!firstInvalidTab) {
                firstInvalidTab = resolveRecordEditorTabForFieldKey(rawKey);
            }

            const target = resolveErrorTarget(field, form) || field;
            target.classList.add("field-invalid");
            target.setAttribute("aria-invalid", "true");
            invalidTargets.push(target);

            const errorHost = target.closest("label")
                || target.closest(".office-picker")
                || target.parentElement
                || form;
            if (!(errorHost instanceof HTMLElement)) {
                summaryMessages.push(...normalizedMessages);
                return;
            }

            const errorLine = document.createElement("div");
            errorLine.className = "field-error-message";
            errorLine.textContent = normalizedMessages.join(" ");
            errorHost.appendChild(errorLine);
        });

        const topMessage = payload && typeof payload.message === "string" ? payload.message.trim() : "";
        if (topMessage || summaryMessages.length > 0) {
            const summary = document.createElement("div");
            summary.className = "alert alert-error modal-submit-summary";
            summary.setAttribute("role", "alert");

            const merged = [];
            if (topMessage) {
                merged.push(topMessage);
            }
            merged.push(...summaryMessages);
            summary.textContent = merged.filter(Boolean).join(" | ");
            form.insertBefore(summary, form.firstElementChild);
        }

        if (firstInvalidTab) {
            setRecordFormTab(form, firstInvalidTab);
        }

        if (invalidTargets.length > 0 && invalidTargets[0] instanceof HTMLElement) {
            invalidTargets[0].focus();
        }
    }

    function syncSinglePersonPickerInForm(form, wrapper) {
        if (!(form instanceof HTMLFormElement) || !(wrapper instanceof HTMLElement)) {
            return;
        }

        const hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
        const queryInput = wrapper.querySelector("[data-person-picker-input]");
        const source = wrapper.querySelector("[data-person-picker-source]");
        if (!(hiddenInput instanceof HTMLInputElement)
            || !(queryInput instanceof HTMLInputElement)
            || !(source instanceof HTMLElement)) {
            return;
        }

        if (hiddenInput.value.trim()) {
            queryInput.setCustomValidity("");
            return;
        }

        const query = normalizeSearchText(queryInput.value || "");
        if (!query) {
            return;
        }

        const candidates = Array.from(source.querySelectorAll("[data-id]"))
            .filter((entry) => entry instanceof HTMLElement)
            .map((entry) => {
                const id = (entry.dataset.id || "").trim();
                const label = (entry.dataset.label || "").trim();
                const email = (entry.dataset.email || "").trim();
                if (!id || !label) {
                    return null;
                }

                const display = email ? `${label} <${email}>` : label;
                return { id, label, email, display };
            })
            .filter((entry) => entry !== null);

        const matches = candidates.filter((entry) => {
            const normalizedLabel = normalizeSearchText(entry.label);
            const normalizedEmail = normalizeSearchText(entry.email);
            const normalizedDisplay = normalizeSearchText(entry.display);
            return normalizedLabel === query || normalizedEmail === query || normalizedDisplay === query;
        });

        if (matches.length === 1) {
            const match = matches[0];
            hiddenInput.value = match.id;
            queryInput.value = match.display;
            queryInput.setCustomValidity("");
        }
    }

    function validateRequiredPersonPickers(form) {
        if (!(form instanceof HTMLFormElement)) {
            return true;
        }

        let firstInvalidInput = null;
        form.querySelectorAll('[data-person-picker="single"]').forEach((wrapper) => {
            if (!(wrapper instanceof HTMLElement)) {
                return;
            }

            syncSinglePersonPickerInForm(form, wrapper);

            const hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
            const queryInput = wrapper.querySelector("[data-person-picker-input]");
            if (!(hiddenInput instanceof HTMLInputElement) || !(queryInput instanceof HTMLInputElement)) {
                return;
            }

            const isRequired = hiddenInput.required || queryInput.required;
            if (!isRequired) {
                queryInput.setCustomValidity("");
                return;
            }

            if (!hiddenInput.value.trim()) {
                queryInput.setCustomValidity("Vyberte osobu ze seznamu výsledků.");
                if (!(firstInvalidInput instanceof HTMLInputElement)) {
                    firstInvalidInput = queryInput;
                }
            } else {
                queryInput.setCustomValidity("");
            }
        });

        if (firstInvalidInput instanceof HTMLInputElement) {
            firstInvalidInput.reportValidity();
            firstInvalidInput.focus();
            return false;
        }

        return true;
    }

    function initConfirmSubmitToggles(scope) {
        if (!(scope instanceof HTMLElement || scope instanceof Document)) {
            return;
        }

        scope.querySelectorAll('form[data-confirm-submit-toggle="true"]').forEach((form) => {
            if (!(form instanceof HTMLFormElement) || form.dataset.confirmSubmitReady === "true") {
                return;
            }

            const checkbox = form.querySelector("[data-confirm-submit-checkbox]");
            const submit = form.querySelector('[data-confirm-submit-button], button[type="submit"], input[type="submit"]');
            if (!(checkbox instanceof HTMLInputElement)
                || checkbox.type !== "checkbox"
                || !(submit instanceof HTMLButtonElement || submit instanceof HTMLInputElement)) {
                return;
            }

            const sync = () => {
                submit.disabled = !checkbox.checked;
            };

            checkbox.addEventListener("change", sync);
            sync();
            form.dataset.confirmSubmitReady = "true";
        });
    }

    function setFormSubmitting(form, submitting) {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const confirmationCheckbox = form.querySelector("[data-confirm-submit-checkbox]");
        const hasConfirmationGate = form.dataset.confirmSubmitToggle === "true"
            && confirmationCheckbox instanceof HTMLInputElement
            && confirmationCheckbox.type === "checkbox";

        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach((element) => {
            if (element instanceof HTMLButtonElement || element instanceof HTMLInputElement) {
                if (submitting) {
                    element.disabled = true;
                    return;
                }

                if (hasConfirmationGate && !confirmationCheckbox.checked) {
                    element.disabled = true;
                    return;
                }

                element.disabled = false;
            }
        });
    }

    async function fetchHtmlDocument(url) {
        const response = await fetch(url, {
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        const html = await response.text();
        return new DOMParser().parseFromString(html, "text/html");
    }

    async function fetchHtmlFragment(url) {
        const response = await fetch(url, {
            headers: { "X-Requested-With": "XMLHttpRequest" },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }

        return response.text();
    }

    function isElementInHiddenTree(element) {
        let current = element;
        while (current instanceof HTMLElement) {
            if (current.hidden) {
                return true;
            }

            current = current.parentElement;
        }

        return false;
    }

    function buildRecordUiState(scopeRoot) {
        const root = scopeRoot instanceof HTMLElement ? scopeRoot : document;
        const expandedRecordIds = Array.from(root.querySelectorAll('.record-card[data-record-id]'))
            .filter((card) => card instanceof HTMLElement && !isElementInHiddenTree(card) && !card.classList.contains("collapsed"))
            .map((card) => card.getAttribute("data-record-id") || "")
            .filter(Boolean);

        const commentSortDirectionByRecordId = {};
        root.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
            if (!(card instanceof HTMLElement)) {
                return;
            }
            if (isElementInHiddenTree(card)) {
                return;
            }

            const recordId = card.getAttribute("data-record-id");
            if (!recordId) {
                return;
            }

            const section = card.querySelector("[data-comment-sort-section]");
            if (!(section instanceof HTMLElement)) {
                return;
            }

            commentSortDirectionByRecordId[recordId] = section.getAttribute("data-comment-sort-direction") === "desc"
                ? "desc"
                : "asc";
        });

        return {
            activeTab: localStorage.getItem("pmtracker.tab.active") || "zaznamy",
            scrollY: window.scrollY,
            expandedRecordIds,
            commentSortDirectionByRecordId
        };
    }

    function restoreRecordUiState(state) {
        if (!state || typeof state !== "object") {
            return;
        }

        if (typeof state.activeTab === "string" && state.activeTab) {
            setActiveTab(state.activeTab);
            syncTabQuery(state.activeTab);
        }

        const expandedSet = new Set(Array.isArray(state.expandedRecordIds) ? state.expandedRecordIds : []);
        document.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
            if (!(card instanceof HTMLElement)) {
                return;
            }

            const recordId = card.getAttribute("data-record-id") || "";
            const shouldExpand = expandedSet.has(recordId);
            card.classList.toggle("collapsed", !shouldExpand);
            const header = card.querySelector("[data-record-toggle]");
            if (header instanceof HTMLElement) {
                header.setAttribute("aria-expanded", String(shouldExpand));
            }
        });

        const directionMap = state.commentSortDirectionByRecordId && typeof state.commentSortDirectionByRecordId === "object"
            ? state.commentSortDirectionByRecordId
            : {};
        Object.entries(directionMap).forEach(([recordId, direction]) => {
            document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"] [data-comment-sort-section]`)
                .forEach((section) => {
                    if (!(section instanceof HTMLElement)) {
                        return;
                    }
                    const normalizedDirection = direction === "desc" ? "desc" : "asc";
                    applyCommentSort(section, normalizedDirection);
                    const toggle = section.querySelector("[data-comment-sort-toggle]");
                    if (toggle instanceof HTMLButtonElement) {
                        setCommentSortButtonLabel(toggle, normalizedDirection);
                    }
                });
        });

        if (typeof state.scrollY === "number" && Number.isFinite(state.scrollY)) {
            window.scrollTo({ top: state.scrollY, behavior: "auto" });
        }
    }

    async function refreshRecordCard(payload) {
        const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
        const recordId = payload.recordId != null ? String(payload.recordId) : "";
        if (!refreshUrl || !recordId) {
            return;
        }

        const currentCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`))
            .filter((card) => card instanceof HTMLElement);
        if (currentCards.length === 0) {
            return;
        }

        const anchorCurrentCard = currentCards.find((card) => !isElementInHiddenTree(card)) ?? currentCards[0];
        const beforeTop = anchorCurrentCard.getBoundingClientRect().top;
        const wasCollapsed = anchorCurrentCard.classList.contains("collapsed");
        const previousSortDirection = anchorCurrentCard.querySelector("[data-comment-sort-section]")?.getAttribute("data-comment-sort-direction") === "desc"
            ? "desc"
            : "asc";

        const html = await fetchHtmlFragment(refreshUrl);
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const replacementCard = parsed.querySelector(".record-card[data-record-id]");
        if (!(replacementCard instanceof HTMLElement)) {
            throw new Error("Nepodařilo se načíst aktualizovanou kartu záznamu.");
        }

        currentCards.forEach((card, index) => {
            const nextCard = index === 0 ? replacementCard : replacementCard.cloneNode(true);
            card.replaceWith(nextCard);
        });

        const refreshedCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`))
            .filter((card) => card instanceof HTMLElement);
        refreshedCards.forEach((card) => {
            card.classList.toggle("collapsed", wasCollapsed);
            const header = card.querySelector("[data-record-toggle]");
            if (header instanceof HTMLElement) {
                header.setAttribute("aria-expanded", String(!wasCollapsed));
            }
            const section = card.querySelector("[data-comment-sort-section]");
            if (section instanceof HTMLElement) {
                applyCommentSort(section, previousSortDirection);
                const toggle = section.querySelector("[data-comment-sort-toggle]");
                if (toggle instanceof HTMLButtonElement) {
                    setCommentSortButtonLabel(toggle, previousSortDirection);
                }
            }
            initRecordFormEnhancements(card);
        });

        const anchorRefreshedCard = refreshedCards.find((card) => !isElementInHiddenTree(card)) ?? refreshedCards[0];
        if (anchorRefreshedCard instanceof HTMLElement) {
            const afterTop = anchorRefreshedCard.getBoundingClientRect().top;
            const delta = afterTop - beforeTop;
            if (Math.abs(delta) > 1) {
                window.scrollBy({ top: delta, behavior: "auto" });
            }
        }
    }

    async function refreshMeetingTaskItem(payload) {
        const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
        const recordId = payload.recordId != null ? String(payload.recordId) : "";
        if (!refreshUrl || !recordId) {
            return;
        }

        const currentTask = document.querySelector(`.task-item[data-task-record-id="${CSS.escape(recordId)}"]`);
        if (!(currentTask instanceof HTMLElement)) {
            return;
        }

        const beforeTop = currentTask.getBoundingClientRect().top;
        const previousSortDirection = currentTask.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";

        const html = await fetchHtmlFragment(refreshUrl);
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const replacementTask = parsed.querySelector(".task-item[data-task-record-id]");
        if (!(replacementTask instanceof HTMLElement)) {
            throw new Error("Nepodařilo se načíst aktualizovaný blok úkolu.");
        }

        currentTask.replaceWith(replacementTask);
        initRecordFormEnhancements(replacementTask);

        const refreshedTask = document.querySelector(`.task-item[data-task-record-id="${CSS.escape(recordId)}"]`);
        if (refreshedTask instanceof HTMLElement) {
            applyCommentSort(refreshedTask, previousSortDirection);
            const toggle = refreshedTask.querySelector("[data-comment-sort-toggle]");
            if (toggle instanceof HTMLButtonElement) {
                setCommentSortButtonLabel(toggle, previousSortDirection);
            }

            const afterTop = refreshedTask.getBoundingClientRect().top;
            const delta = afterTop - beforeTop;
            if (Math.abs(delta) > 1) {
                window.scrollBy({ top: delta, behavior: "auto" });
            }
        }
    }

    function replaceSelectorFromDocument(nextDoc, selector) {
        const current = document.querySelector(selector);
        const replacement = nextDoc.querySelector(selector);
        if (!(current instanceof HTMLElement) || !(replacement instanceof HTMLElement)) {
            return false;
        }
        current.replaceWith(replacement);
        return true;
    }

    async function refreshProjectSchedulePanels() {
        const hasSchedulePanel = document.querySelector('[data-tab-panel="harmonogram"]') instanceof HTMLElement;
        if (!hasSchedulePanel) {
            return;
        }

        const refreshUrl = new URL(window.location.href);
        refreshUrl.searchParams.set("tab", "harmonogram");
        const nextDoc = await fetchHtmlDocument(refreshUrl.toString());
        replaceSelectorFromDocument(nextDoc, '[data-tab-panel="harmonogram"]');
        queueRainbowSegmentRender(document);
    }

    async function refreshPageScope(payload) {
        if (!payload || typeof payload !== "object") {
            return;
        }

        closeAllFloatingPanels();

        const scope = typeof payload.refreshScope === "string" ? payload.refreshScope : "";
        const refreshUrl = typeof payload.refreshUrl === "string" && payload.refreshUrl
            ? payload.refreshUrl
            : window.location.href;

        if (!scope) {
            return;
        }

        if (scope === "page") {
            const pageEditorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
            if (pageEditorForm instanceof HTMLFormElement) {
                prepareRecordEditorFormNavigation(pageEditorForm);
            }

            window.location.assign(refreshUrl);
            return;
        }

        if (scope === "record-card") {
            await refreshRecordCard(payload);
            initProjectRecordsUi();
            initProjectScheduleUi();
            initCommentSortUi(document);
            return;
        }

        if (scope === "record-card-with-schedules") {
            const activeTab = localStorage.getItem("pmtracker.tab.active") || "zaznamy";
            const scrollY = window.scrollY;

            await refreshRecordCard(payload);
            await refreshProjectSchedulePanels();

            initProjectTabs();
            initProjectRecordsUi();
            initProjectScheduleUi();
            initCommentSortUi(document);
            setActiveTab(activeTab);
            syncTabQuery(activeTab);
            window.scrollTo({ top: scrollY, behavior: "auto" });
            return;
        }

        if (scope === "meeting-task-item") {
            await refreshMeetingTaskItem(payload);
            initCommentSortUi(document);
            return;
        }

        if (scope === "nastaveni-panel") {
            const shell = document.querySelector("[data-settings-shell]");
            const panel = shell?.querySelector("[data-settings-panel]");
            if (!(shell instanceof HTMLElement) || !(panel instanceof HTMLElement)) {
                return;
            }

            const endpointUrl = new URL(refreshUrl, window.location.origin);
            const section = endpointUrl.searchParams.get("section") || "role";
            const userId = endpointUrl.searchParams.get("userId");
            const projektId = endpointUrl.searchParams.get("projektId");

            panel.setAttribute("aria-busy", "true");
            try {
                const response = await fetch(`${endpointUrl.pathname}${endpointUrl.search}`, {
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    credentials: "same-origin"
                });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                panel.innerHTML = await response.text();
                initRecordFormEnhancements(panel);

                shell.querySelectorAll("[data-settings-link]").forEach((link) => {
                    if (!(link instanceof HTMLAnchorElement)) {
                        return;
                    }

                    const key = link.dataset.key || "role";
                    const linkUrl = new URL(link.href, window.location.origin);
                    linkUrl.searchParams.set("section", key);
                    if (userId) {
                        linkUrl.searchParams.set("userId", userId);
                    } else {
                        linkUrl.searchParams.delete("userId");
                    }
                    if (projektId) {
                        linkUrl.searchParams.set("projektId", projektId);
                    } else {
                        linkUrl.searchParams.delete("projektId");
                    }

                    link.href = `${linkUrl.pathname}${linkUrl.search}${linkUrl.hash}`;
                    const active = key === section;
                    link.classList.toggle("active", active);
                    if (active) {
                        link.setAttribute("aria-current", "page");
                    } else {
                        link.removeAttribute("aria-current");
                    }
                });

                const nextUrl = new URL(window.location.href);
                nextUrl.searchParams.set("section", section);
                if (userId) {
                    nextUrl.searchParams.set("userId", userId);
                } else {
                    nextUrl.searchParams.delete("userId");
                }
                if (projektId) {
                    nextUrl.searchParams.set("projektId", projektId);
                } else {
                    nextUrl.searchParams.delete("projektId");
                }

                history.replaceState(
                    {
                        ...(history.state || {}),
                        settingsSection: section,
                        settingsUserId: userId,
                        settingsProjektId: projektId
                    },
                    "",
                    `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`
                );
            } finally {
                panel.setAttribute("aria-busy", "false");
            }

            return;
        }

        if (scope === "ciselniky-detail") {
            const panel = document.querySelector("[data-ciselnik-panel]");
            if (!(panel instanceof HTMLElement)) {
                return;
            }

            const response = await fetch(refreshUrl, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            });
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            panel.innerHTML = await response.text();
            initRecordFormEnhancements(panel);
            return;
        }

        const nextDoc = await fetchHtmlDocument(refreshUrl);

        switch (scope) {
            case "projekty-index":
                replaceSelectorFromDocument(nextDoc, "[data-project-list-shell]");
                initProjectIndexStatusFilters(document);
                break;
            case "osoby-index":
                replaceSelectorFromDocument(nextDoc, "[data-osoby-table-card]");
                break;
            case "projekty-detail-zaznamy":
            case "projekty-detail-jednani":
            case "projekty-detail-tym":
            case "projekty-detail-zaznamy-preserve": {
                const preserveRecordUi = scope === "projekty-detail-zaznamy-preserve";
                const recordUiState = preserveRecordUi ? buildRecordUiState(document) : null;
                const tab = typeof payload.tab === "string" && payload.tab
                    ? payload.tab
                        : (scope === "projekty-detail-zaznamy"
                            ? "zaznamy"
                            : scope === "projekty-detail-jednani"
                                ? "jednani"
                                : "tym");

                if (tab === "harmonogram" || tab === "gant") {
                    replaceSelectorFromDocument(nextDoc, '[data-tab-panel="harmonogram"]');
                } else {
                    replaceSelectorFromDocument(nextDoc, `[data-tab-panel="${tab}"]`);
                }
                setActiveTab(tab);
                syncTabQuery(tab);
                initProjectTabs();
                initProjectRecordsUi();
                initProjectScheduleUi();
                initCommentSortUi(document);
                if (preserveRecordUi) {
                    restoreRecordUiState(recordUiState);
                }
                break;
            }
            default:
                break;
        }
    }

    function initModalAjaxSubmit() {
        if (!(document.body instanceof HTMLElement) || document.body.dataset.modalAjaxReady === "true") {
            return;
        }

        document.body.dataset.modalAjaxReady = "true";
        document.addEventListener("submit", async (event) => {
            const target = event.target;
            if (!(target instanceof HTMLFormElement) || target.dataset.ajaxSubmit !== "true") {
                return;
            }
            const isModalForm = modalRoot instanceof HTMLElement && modalRoot.contains(target);

            if (event.defaultPrevented) {
                return;
            }

            event.preventDefault();
            clearModalFormErrors(target);

            if (!validateRequiredPersonPickers(target)) {
                return;
            }

            if (!target.checkValidity()) {
                target.reportValidity();
                return;
            }

            setFormSubmitting(target, true);

            try {
                const action = target.getAttribute("action") || window.location.href;
                const method = (target.getAttribute("method") || "post").toUpperCase();
                const response = await fetch(action, {
                    method,
                    body: new FormData(target),
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    credentials: "same-origin"
                });

                const contentType = (response.headers.get("content-type") || "").toLowerCase();
                if (!contentType.includes("application/json")) {
                    throw new Error("Server nevrátil JSON odpověď.");
                }

                const payload = await response.json();
                if (!response.ok || !payload || payload.ok !== true) {
                    target.dataset.recordEditorNavigating = "false";
                    renderModalFormErrors(target, payload || { message: "Uložení se nezdařilo." });
                    return;
                }

                if (target.matches('[data-record-editor-form="true"]')) {
                    markRecordEditorFormClean(target);
                    target.dataset.recordEditorNavigating = "true";
                }

                if (isModalForm) {
                    closeModal();
                }
                await refreshPageScope(payload);
            } catch (error) {
                target.dataset.recordEditorNavigating = "false";
                renderModalFormErrors(target, {
                    message: error instanceof Error ? error.message : "Uložení se nezdařilo."
                });
            } finally {
                setFormSubmitting(target, false);
            }
        });
    }

    function initProjectTabs() {
        const tabs = document.querySelectorAll(".tab");
        if (tabs.length === 0) {
            return;
        }

        const availableTabs = new Set(Array.from(tabs).map((tab) => tab.getAttribute("data-tab")));
        const storedTab = localStorage.getItem("pmtracker.tab.active");
        const urlTab = new URL(window.location.href).searchParams.get("tab");
        const defaultTab = tabs[0].getAttribute("data-tab");
        const normalizedUrlTab = urlTab === "gant" ? "harmonogram" : urlTab;
        const normalizedStoredTab = storedTab === "gant" ? "harmonogram" : storedTab;
        const tabToActivate = normalizedUrlTab && availableTabs.has(normalizedUrlTab)
            ? normalizedUrlTab
            : normalizedStoredTab && availableTabs.has(normalizedStoredTab)
                ? normalizedStoredTab
                : defaultTab;
        setActiveTab(tabToActivate);

        tabs.forEach((tab) => {
            if (!(tab instanceof HTMLElement) || tab.dataset.tabReady === "true") {
                return;
            }

            tab.dataset.tabReady = "true";
            tab.addEventListener("click", () => {
                const requestedTab = tab.getAttribute("data-tab");
                setActiveTab(requestedTab);
                syncTabQuery(requestedTab);
            });
        });
    }

    function initProjectRecordsUi() {
        const filterPanel = document.querySelector("[data-filter-panel]");
        const storedOpen = localStorage.getItem("pmtracker.filters.open");
        if (filterPanel) {
            const shouldOpen = storedOpen === "true";
            setFilterPanelOpen(shouldOpen);
        }

        const state = restoreFilterState();
        setProjectFilterSaveStatus("records", "");
        applyRecordsView(Boolean(state.groupBySubsystem) ? "subsystem" : "flat");
        initSubsystemScrollIndicator();
    }

    function initProjectRecordPageshowSync() {
        if (!(document.body instanceof HTMLElement) || document.body.dataset.projectRecordPageshowSyncReady === "true") {
            return;
        }

        const hasProjectRecordsPanel = document.querySelector('[data-tab-panel="zaznamy"]') instanceof HTMLElement;
        if (!hasProjectRecordsPanel) {
            return;
        }

        document.body.dataset.projectRecordPageshowSyncReady = "true";
        window.addEventListener("pageshow", async (event) => {
            if (!event.persisted) {
                return;
            }

            try {
                const refreshUrl = new URL(window.location.href);
                refreshUrl.searchParams.set("tab", "zaznamy");
                await refreshPageScope({
                    refreshScope: "projekty-detail-zaznamy-preserve",
                    refreshUrl: refreshUrl.toString(),
                    tab: "zaznamy"
                });
            } catch (error) {
                console.error("Nepodařilo se synchronizovat panel záznamů po návratu na stránku.", error);
            }
        });
    }

    function setCommentSortButtonLabel(button, direction) {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        button.textContent = direction === "desc"
            ? "Řazení: jednání sestupně"
            : "Řazení: jednání vzestupně";
        button.setAttribute("aria-pressed", direction === "desc" ? "true" : "false");
    }

    function applyCommentSort(section, direction) {
        if (!(section instanceof HTMLElement)) {
            return;
        }

        const list = section.querySelector("[data-comment-list]");
        if (!(list instanceof HTMLElement)) {
            return;
        }

        const items = Array.from(list.querySelectorAll("[data-comment-item]"))
            .filter((item) => item instanceof HTMLElement);
        if (items.length <= 1) {
            return;
        }

        items.sort((aNode, bNode) => {
            const a = aNode;
            const b = bNode;
            const aMeeting = Number(a.getAttribute("data-comment-meeting") || "0");
            const bMeeting = Number(b.getAttribute("data-comment-meeting") || "0");
            const aId = Number(a.getAttribute("data-comment-id") || "0");
            const bId = Number(b.getAttribute("data-comment-id") || "0");
            if (direction === "desc") {
                return (bMeeting - aMeeting) || (bId - aId);
            }
            return (aMeeting - bMeeting) || (aId - bId);
        });

        items.forEach((item) => list.appendChild(item));
        section.setAttribute("data-comment-sort-direction", direction);
    }

    function initCommentSortUi(scope) {
        const root = scope instanceof Element ? scope : document;
        const sections = root.querySelectorAll("[data-comment-sort-section]");
        if (sections.length === 0) {
            return;
        }

        sections.forEach((section) => {
            if (!(section instanceof HTMLElement)) {
                return;
            }

            const defaultDirection = section.getAttribute("data-comment-sort-direction") === "desc"
                ? "desc"
                : "asc";
            applyCommentSort(section, defaultDirection);

            const toggle = section.querySelector("[data-comment-sort-toggle]");
            if (!(toggle instanceof HTMLButtonElement)) {
                return;
            }

            setCommentSortButtonLabel(toggle, defaultDirection);
            if (toggle.dataset.commentSortReady === "true") {
                return;
            }

            toggle.dataset.commentSortReady = "true";
            toggle.addEventListener("click", () => {
                const current = section.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";
                const next = current === "asc" ? "desc" : "asc";
                applyCommentSort(section, next);
                setCommentSortButtonLabel(toggle, next);
            });
        });
    }

    function toggleRecord(card) {
        const isCollapsed = card.classList.contains("collapsed");
        card.classList.toggle("collapsed", !isCollapsed);
        const header = card.querySelector("[data-record-toggle]");
        if (header) {
            header.setAttribute("aria-expanded", String(isCollapsed));
        }
    }

    document.addEventListener("click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        const resetPrintPreference = target.closest("[data-print-preference-reset]");
        if (resetPrintPreference instanceof HTMLButtonElement) {
            event.preventDefault();
            clearStoredPrintFormat();
            return;
        }

        const resetProjectFilterPreferences = target.closest("[data-project-filter-preferences-reset]");
        if (resetProjectFilterPreferences instanceof HTMLButtonElement) {
            event.preventDefault();
            clearProjectFilterPreferenceStorage();
            const status = document.querySelector("[data-project-filter-preferences-status]");
            if (status instanceof HTMLElement) {
                status.textContent = "Uložené projektové filtry byly odstraněny.";
            }
            return;
        }

        const resetRecordEditorPreference = target.closest("[data-record-editor-preference-reset]");
        if (resetRecordEditorPreference instanceof HTMLButtonElement) {
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
        if (saveDefaultsButton instanceof HTMLButtonElement) {
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

        if (target.classList.contains("modal-overlay")) {
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

        const recordToggle = target.closest("[data-record-toggle]");
        if (recordToggle) {
            if (target.closest("[data-stop-propagation]")) {
                return;
            }
            const card = recordToggle.closest(".record-card");
            if (card) {
                toggleRecord(card);
            }
            return;
        }

        if (target.closest("[data-stop-propagation]")) {
            return;
        }

        const navCard = target.closest("[data-href]");
        if (navCard && !target.closest("button") && !target.closest("a")) {
            const href = navCard.getAttribute("data-href");
            if (href) {
                window.location.href = href;
            }
        }
    });

    document.addEventListener("keydown", (event) => {
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
    });

    document.addEventListener("change", (event) => {
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

    });

    document.addEventListener("input", (event) => {
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

    });

    document.addEventListener("keydown", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        if ((event.key === "Enter" || event.key === " ") && target.matches("[data-href]")) {
            event.preventDefault();
            const href = target.getAttribute("data-href");
            if (href) {
                window.location.href = href;
            }
        }
    });

    window.addEventListener("beforeunload", (event) => {
        const pageEditorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
        if (!(pageEditorForm instanceof HTMLFormElement)) {
            return;
        }

        if (pageEditorForm.dataset.recordEditorNavigating === "true") {
            return;
        }

        if (!isRecordEditorFormDirty(pageEditorForm)) {
            return;
        }

        event.preventDefault();
        event.returnValue = "";
    });

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
    window.addEventListener("scroll", queueFloatingPanelReposition, true);
    window.addEventListener("resize", queueFloatingPanelReposition);
    window.addEventListener("resize", rerenderRainbowLabelsOnResize);
    window.addEventListener("resize", rerenderTimelineAxesOnResize);

    initProjectTabs();
    initProjectRecordsUi();
    initProjectScheduleUi();
    initProjectRecordPageshowSync();
    initCommentSortUi(document);
    restoreRecordEditorReturnStateFromUrl();

    initTheme();
    initUserMenu();
    initPrintFormatChooser();
    refreshRecordEditorPreferenceUi();
    initProfileRightsFilter();
    initCiselnikAjaxSwitch();
    initSettingsAjaxSwitch();
    initRecordFormEnhancements(document);
    initPermissionMetadataBindings(document);
    initProjectIndexStatusFilters(document);
    initModalAjaxSubmit();
})();
