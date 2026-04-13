const projectFilterStoragePrefix = "pmtracker.projectFilters.v1.project.";
const projectRecordFilterPanelStorageKey = "pmtracker.filters.open";
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
const recordMeetingCommentStateCache = new Map();
const recordMeetingCommentStateRequests = new Map();
const recordMeetingCommentStateLoadingMessage = "Načítání dat pro filtr jednání-vyjádření...";
const recordMeetingCommentStateErrorMessage = "Nepodařilo se načíst data pro filtr jednání-vyjádření.";
const projectFilterConfigs = {
    records: {
        rootSelector: '[data-project-filter-scope="records"]',
        inputSelector: "[data-filter-key]",
        keyAttribute: "data-filter-key",
        chipRowSelector: '[data-filter-chip-row="records"]',
        statusSelector: '[data-filter-save-status="records"]',
        fields: [
            { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
            { inputKey: "sortBy", stateKey: "sortBy", type: "select", skipChip: true },
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
            { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
            { inputKey: "sortBy", stateKey: "sortBy", type: "select", skipChip: true }
        ]
    }
};
const projectPrintRelevantRecordStateKeys = [
    "subsystem",
    "kategorie",
    "stav",
    "typ",
    "vlastnik",
    "aktivni",
    "mine",
    "jednaniVyjadreniStav"
];

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

function normalizeSubsystemSortMode(value) {
    const candidate = String(value || "").trim().toLowerCase();
    if (candidate === "alpha-asc" || candidate === "alpha-desc" || candidate === "project-desc") {
        return candidate;
    }

    return "project-asc";
}

function buildSubsystemSortMeta(source = {}) {
    return {
        name: String(source.name || "").trim() || "-",
        code: String(source.code || "").trim(),
        order: Number.isInteger(source.order) ? source.order : Number.parseInt(source.order || "0", 10) || 0,
        hasProjectOrder: source.hasProjectOrder === true || source.hasProjectOrder === "true"
    };
}

function readSubsystemGroupSortMeta(element) {
    if (!(element instanceof Element)) {
        return buildSubsystemSortMeta();
    }

    return buildSubsystemSortMeta({
        name: element.getAttribute("data-subsystem-name") || "",
        code: element.getAttribute("data-subsystem-kod") || "",
        order: element.getAttribute("data-subsystem-order") || "0",
        hasProjectOrder: element.getAttribute("data-subsystem-order-active") === "true"
    });
}

function compareSubsystemAlpha(left, right) {
    const byName = left.name.localeCompare(right.name, "cs");
    if (byName !== 0) {
        return byName;
    }

    return left.code.localeCompare(right.code, "cs");
}

function compareSubsystemSortMeta(leftSource, rightSource, sortMode) {
    const left = buildSubsystemSortMeta(leftSource);
    const right = buildSubsystemSortMeta(rightSource);
    const resolvedMode = normalizeSubsystemSortMode(sortMode);
    const alpha = compareSubsystemAlpha(left, right);

    if (resolvedMode === "alpha-asc") {
        return alpha;
    }

    if (resolvedMode === "alpha-desc") {
        return alpha * -1;
    }

    if (left.hasProjectOrder && right.hasProjectOrder) {
        if (left.order !== right.order) {
            return resolvedMode === "project-desc"
                ? right.order - left.order
                : left.order - right.order;
        }

        return alpha;
    }

    if (left.hasProjectOrder !== right.hasProjectOrder) {
        return left.hasProjectOrder ? -1 : 1;
    }

    return resolvedMode === "project-desc" ? alpha * -1 : alpha;
}

function sortSubsystemGroupsInContainer(container, sortMode) {
    if (!(container instanceof Element)) {
        return [];
    }

    const groups = Array.from(container.querySelectorAll("[data-subsystem-group]"))
        .filter((group) => group instanceof HTMLElement);

    groups
        .sort((left, right) => compareSubsystemSortMeta(
            readSubsystemGroupSortMeta(left),
            readSubsystemGroupSortMeta(right),
            sortMode))
        .forEach((group) => container.appendChild(group));

    return groups;
}

export function buildProjectPrintFilterSnapshot() {
    const state = buildProjectFilterStateFromInputs("records");
    const snapshot = {
        subsystem: typeof state.subsystem === "string" ? state.subsystem.trim() : "",
        kategorie: typeof state.kategorie === "string" ? state.kategorie.trim() : "",
        stav: typeof state.stav === "string" ? state.stav.trim() : "",
        typ: typeof state.typ === "string" ? state.typ.trim() : "",
        vlastnik: typeof state.vlastnik === "string" ? state.vlastnik.trim() : "",
        aktivni: Boolean(state.aktivni),
        mine: Boolean(state.mine),
        jednaniVyjadreniStav: typeof state.jednaniVyjadreniStav === "string" ? state.jednaniVyjadreniStav.trim() : ""
    };

    return {
        ...snapshot,
        hasRelevantFilters: projectPrintRelevantRecordStateKeys.some((key) => Boolean(snapshot[key]))
    };
}

export function buildProjectPrintFilterQueryParams(useCurrentFilters) {
    const snapshot = buildProjectPrintFilterSnapshot();
    const params = new URLSearchParams();

    if (!useCurrentFilters) {
        params.set("useCurrentFilters", "false");
        return { snapshot, params };
    }

    params.set("useCurrentFilters", "true");

    if (snapshot.subsystem) {
        params.set("subsystem", snapshot.subsystem);
    }

    if (snapshot.kategorie) {
        params.set("kategorie", snapshot.kategorie);
    }

    if (snapshot.stav) {
        params.set("stav", snapshot.stav);
    }

    if (snapshot.typ) {
        params.set("typ", snapshot.typ);
    }

    if (snapshot.vlastnik) {
        params.set("vlastnik", snapshot.vlastnik);
    }

    if (snapshot.aktivni) {
        params.set("aktivni", "true");
    }

    if (snapshot.mine) {
        params.set("mine", "true");
    }

    if (snapshot.jednaniVyjadreniStav) {
        params.set("jednaniVyjadreniStav", snapshot.jednaniVyjadreniStav);
    }

    return { snapshot, params };
}

function getProjectFilterRoot(scope) {
    const config = getProjectFilterConfig(scope);
    if (!config) {
        return null;
    }

    const root = document.querySelector(config.rootSelector);
    return root instanceof HTMLElement ? root : null;
}

function getProjectFilterProjectId(scope) {
    const root = getProjectFilterRoot(scope);
    const projectId = (root?.dataset.projectId || "").trim();
    return projectId || "0";
}

function getProjectDetailRoot() {
    const root = document.querySelector("[data-project-detail-root]");
    return root instanceof HTMLElement ? root : null;
}

function getProjectRecordsPanel() {
    const panel = document.querySelector('[data-tab-panel="zaznamy"]');
    return panel instanceof HTMLElement ? panel : null;
}

function normalizeRecordMeetingCommentStatesPayload(payload) {
    if (!payload || typeof payload !== "object") {
        return {};
    }

    const rawStates = payload.statesByRecordId && typeof payload.statesByRecordId === "object"
        ? payload.statesByRecordId
        : payload;
    const normalized = {};

    Object.entries(rawStates).forEach(([recordId, values]) => {
        const normalizedRecordId = String(recordId || "").trim();
        if (!normalizedRecordId) {
            return;
        }

        const normalizedValues = Array.isArray(values)
            ? values
                .map((value) => normalizeFilterToken(value))
                .filter(Boolean)
            : [];

        normalized[normalizedRecordId] = Array.from(new Set(normalizedValues));
    });

    return normalized;
}

function applyCachedRecordMeetingCommentStates(projectId) {
    const normalizedProjectId = String(projectId || "").trim();
    if (!normalizedProjectId || !recordMeetingCommentStateCache.has(normalizedProjectId)) {
        return false;
    }

    const statesByRecordId = recordMeetingCommentStateCache.get(normalizedProjectId) || {};
    const recordsPanel = getProjectRecordsPanel();
    if (!(recordsPanel instanceof HTMLElement)) {
        return false;
    }

    recordsPanel.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
        if (!(card instanceof HTMLElement)) {
            return;
        }

        const recordId = (card.dataset.recordId || "").trim();
        const values = Array.isArray(statesByRecordId[recordId]) ? statesByRecordId[recordId] : [];
        card.dataset.filterVyjadreniJednaniStavy = values.join("|");
    });

    return true;
}

async function ensureRecordMeetingCommentStatesLoaded() {
    const projectRoot = getProjectDetailRoot();
    const projectId = getProjectFilterProjectId("records");
    const loadUrl = (projectRoot?.dataset.recordMeetingCommentStatesUrl || "").trim();
    if (!projectId || projectId === "0" || !loadUrl) {
        return false;
    }

    if (applyCachedRecordMeetingCommentStates(projectId)) {
        return true;
    }

    const existingRequest = recordMeetingCommentStateRequests.get(projectId);
    if (existingRequest instanceof Promise) {
        return existingRequest;
    }

    setProjectFilterSaveStatus("records", recordMeetingCommentStateLoadingMessage);

    const request = (async () => {
        try {
            const response = await fetch(loadUrl, {
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }

            const payload = await response.json();
            recordMeetingCommentStateCache.set(projectId, normalizeRecordMeetingCommentStatesPayload(payload));
            applyCachedRecordMeetingCommentStates(projectId);
            setProjectFilterSaveStatus("records", "");
            applyProjectRecordFilters();
            return true;
        } catch (error) {
            setProjectFilterSaveStatus("records", recordMeetingCommentStateErrorMessage);
            return false;
        } finally {
            recordMeetingCommentStateRequests.delete(projectId);
        }
    })();

    recordMeetingCommentStateRequests.set(projectId, request);
    return request;
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

function applyProjectFilterScope(scope, options = {}) {
    if (scope === "records") {
        const state = buildProjectFilterStateFromInputs(scope);
        applyRecordsView(Boolean(state.groupBySubsystem) ? "subsystem" : "flat");
        return;
    }

    if (typeof options.applyScope === "function") {
        options.applyScope(scope, buildProjectFilterStateFromInputs(scope));
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

export function getProjectFilterConfig(scope) {
    return projectFilterConfigs[scope] || null;
}

export function getProjectFilterInput(scope, inputKey) {
    const config = getProjectFilterConfig(scope);
    const root = getProjectFilterRoot(scope);
    if (!config || !(root instanceof HTMLElement)) {
        return null;
    }

    const input = root.querySelector(`${config.inputSelector}[${config.keyAttribute}="${inputKey}"]`);
    return input instanceof HTMLInputElement || input instanceof HTMLSelectElement ? input : null;
}

export function getProjectFilterCurrentUserId(scope) {
    return normalizeFilterToken(getProjectFilterRoot(scope)?.dataset.currentUserId || "");
}

export function getProjectFilterStorageKey(scope, kind) {
    const projectId = getProjectFilterProjectId(scope);
    if (!projectId || projectId === "0") {
        return "";
    }

    return `${projectFilterStoragePrefix}${projectId}.${scope}.${kind}`;
}

export function normalizeProjectFilterState(scope, rawState, fallbackState) {
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

export function buildProjectFilterStateFromInputs(scope) {
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

export function setProjectFilterSaveStatus(scope, message) {
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

export function renderProjectFilterChips(scope) {
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

export function handleProjectFilterInputChange(scope, options = {}) {
    persistProjectFilterSessionState(scope);
    renderProjectFilterChips(scope);
    setProjectFilterSaveStatus(scope, "");
    applyProjectFilterScope(scope, options);
}

export function restoreProjectFilterScope(scope) {
    const fallbackState = buildProjectFilterStateFromInputs(scope);
    const restoredState = readStoredProjectFilterState(scope, "state", fallbackState)
        || readStoredProjectFilterState(scope, "defaults", fallbackState)
        || fallbackState;

    applyProjectFilterStateToInputs(scope, restoredState);
    persistProjectFilterSessionState(scope);
    renderProjectFilterChips(scope);
    return restoredState;
}

export function saveProjectFilterDefaults(scope) {
    const state = persistProjectFilterSessionState(scope);
    const key = getProjectFilterStorageKey(scope, "defaults");
    writeJsonStorage(localStorage, key, state);
    setProjectFilterSaveStatus(scope, "Výchozí filtry uloženy v tomto prohlížeči.");
}

export function clearProjectFilterInput(scope, inputKey) {
    const input = getProjectFilterInput(scope, inputKey);
    if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
        return;
    }

    if (input instanceof HTMLInputElement && input.type === "checkbox") {
        input.checked = false;
    }
    else {
        input.value = "";
    }
}

export function clearProjectFilterPreferenceStorage() {
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

export function setFilterPanelOpen(open) {
    const filterPanel = document.querySelector("[data-filter-panel]");
    const filterToggle = document.querySelector("[data-filter-toggle]");
    if (!(filterPanel instanceof HTMLElement)) {
        return;
    }

    filterPanel.classList.toggle("collapsed", !open);
    if (filterToggle instanceof HTMLElement) {
        filterToggle.setAttribute("aria-expanded", String(open));
    }

    localStorage.setItem(projectRecordFilterPanelStorageKey, String(open));
}

export function setRecordFilterVisibility(element, isVisible) {
    if (!(element instanceof HTMLElement)) {
        return;
    }

    element.classList.toggle("is-filter-hidden", !isVisible);
    element.hidden = !isVisible;
}

export function applyProjectRecordFilters() {
    const recordsPanel = getProjectRecordsPanel();
    if (!(recordsPanel instanceof HTMLElement)) {
        return;
    }

    const cards = recordsPanel.querySelectorAll(".record-card[data-record-id]");
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
    const projectId = getProjectFilterProjectId("records");
    const hasMeetingCommentStateCache = applyCachedRecordMeetingCommentStates(projectId);
    if (filters.meetingCommentState && !hasMeetingCommentStateCache) {
        void ensureRecordMeetingCommentStatesLoaded();
    }

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
            || !hasMeetingCommentStateCache
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

    recordsPanel.querySelectorAll(".subsystem-group").forEach((group) => {
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

export function scheduleSubsystemIndicatorSync() {
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

export function initSubsystemScrollIndicator() {
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

export function applyRecordsView(view) {
    const recordsPanel = getProjectRecordsPanel();
    if (!(recordsPanel instanceof HTMLElement)) {
        return;
    }

    const groupBySubsystemInput = getProjectFilterInput("records", "groupBySubsystem");
    const resolvedView = groupBySubsystemInput instanceof HTMLInputElement
        ? (groupBySubsystemInput.checked ? "subsystem" : "flat")
        : view;

    const shells = recordsPanel.querySelectorAll("[data-records-view]");
    if (shells.length === 0) {
        return;
    }

    const groupedList = recordsPanel.querySelector("[data-record-grouped-list]");
    const flatList = recordsPanel.querySelector("[data-record-flat-list]");
    const cards = Array.from(recordsPanel.querySelectorAll(".record-card[data-record-id]"))
        .filter((card) => card instanceof HTMLElement);
    const sortMode = normalizeSubsystemSortMode(buildProjectFilterStateFromInputs("records").sortBy);

    if (groupedList instanceof HTMLElement && flatList instanceof HTMLElement && cards.length > 0) {
        const groupsByKey = new Map();
        const orderedGroups = [];

        cards.forEach((card) => {
            const subsystemName = (card.getAttribute("data-filter-subsystem") || "").trim() || "-";
            const subsystemCode = (card.getAttribute("data-filter-subsystem-kod") || "").trim();
            const subsystemOrder = Number.parseInt(card.getAttribute("data-filter-subsystem-order") || "0", 10) || 0;
            const subsystemHasProjectOrder = card.getAttribute("data-filter-subsystem-order-active") === "true";
            const groupKey = `${subsystemCode}\u0000${subsystemName}`;
            let group = groupsByKey.get(groupKey);

            if (!group) {
                group = {
                    meta: {
                        name: subsystemName,
                        code: subsystemCode,
                        order: subsystemOrder,
                        hasProjectOrder: subsystemHasProjectOrder
                    },
                    cards: []
                };
                groupsByKey.set(groupKey, group);
                orderedGroups.push(group);
            }

            group.cards.push(card);
        });

        orderedGroups.sort((left, right) => compareSubsystemSortMeta(left.meta, right.meta, sortMode));

        if (resolvedView === "subsystem") {
            groupedList.innerHTML = "";

            orderedGroups.forEach((group) => {
                const currentGroup = document.createElement("div");
                currentGroup.className = "subsystem-group";
                currentGroup.setAttribute("data-subsystem-group", "");
                currentGroup.setAttribute("data-subsystem-name", group.meta.name);
                currentGroup.setAttribute("data-subsystem-kod", group.meta.code);
                currentGroup.setAttribute("data-subsystem-order", String(group.meta.order));
                currentGroup.setAttribute("data-subsystem-order-active", String(group.meta.hasProjectOrder));

                const heading = document.createElement("h3");
                heading.textContent = group.meta.name;
                currentGroup.appendChild(heading);

                const currentGroupCards = document.createElement("div");
                currentGroupCards.className = "card-list";
                group.cards.forEach((card) => currentGroupCards.appendChild(card));
                currentGroup.appendChild(currentGroupCards);
                groupedList.appendChild(currentGroup);
            });
        }
        else {
            orderedGroups.forEach((group) => {
                group.cards.forEach((card) => flatList.appendChild(card));
            });
            groupedList.innerHTML = "";
        }
    }

    shells.forEach((shell) => {
        const mode = shell.getAttribute("data-records-view");
        shell.toggleAttribute("hidden", mode !== resolvedView);
    });

    applyProjectRecordFilters();
    scheduleSubsystemIndicatorSync();
}

export function restoreFilterState() {
    return restoreProjectFilterScope("records");
}

export function persistFilterState(input, options = {}) {
    void input;
    handleProjectFilterInputChange("records", options);
}

export function initProjectRecordsUi(options = {}) {
    setFilterPanelOpen(localStorage.getItem(projectRecordFilterPanelStorageKey) === "true");
    restoreFilterState();
    const recordsPanel = getProjectRecordsPanel();
    const groupBySubsystemInput = getProjectFilterInput("records", "groupBySubsystem");
    const showGroupedView = groupBySubsystemInput instanceof HTMLInputElement
        ? groupBySubsystemInput.checked
        : true;
    const hasServerRenderedGroups = recordsPanel instanceof HTMLElement
        && recordsPanel.querySelector("[data-record-grouped-list] [data-subsystem-group]") instanceof HTMLElement;
    const setStatus = typeof options.setProjectFilterSaveStatus === "function"
        ? options.setProjectFilterSaveStatus
        : setProjectFilterSaveStatus;
    setStatus("records", "");
    if (showGroupedView && hasServerRenderedGroups && recordsPanel instanceof HTMLElement) {
        const groupedShell = recordsPanel.querySelector('[data-records-view="subsystem"]');
        const flatShell = recordsPanel.querySelector('[data-records-view="flat"]');
        if (groupedShell instanceof HTMLElement) {
            groupedShell.hidden = false;
        }

        if (flatShell instanceof HTMLElement) {
            flatShell.hidden = true;
        }

        applyProjectRecordFilters();
    }
    else {
        applyRecordsView(showGroupedView ? "subsystem" : "flat");
    }
    initSubsystemScrollIndicator();
}

export function invalidateRecordMeetingCommentStates(projectId) {
    const normalizedProjectId = String(projectId || "").trim();
    if (!normalizedProjectId) {
        return;
    }

    recordMeetingCommentStateCache.delete(normalizedProjectId);
    recordMeetingCommentStateRequests.delete(normalizedProjectId);
}

export function setActiveTab(tabName) {
    document.querySelectorAll("[data-tab-panel]").forEach((panel) => {
        if (!(panel instanceof HTMLElement)) {
            return;
        }
        panel.classList.toggle("active", panel.dataset.tabPanel === tabName);
    });
}

export function syncTabQuery(tabName) {
    if (!tabName) {
        return;
    }
    const url = new URL(window.location.href);
    url.searchParams.set("tab", tabName);
    window.history.replaceState({}, "", url);
}

export {
    compareSubsystemSortMeta,
    normalizeFilterText,
    normalizeSubsystemSortMode,
    readSubsystemGroupSortMeta,
    sortSubsystemGroupsInContainer
};
