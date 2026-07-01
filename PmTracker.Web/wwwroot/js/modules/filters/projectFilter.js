/**
 * filters/projectFilter.js — project filter config, normalization, chips, defaults.
 *
 * Fáze 3B Task 4: extrahováno z filters.js (1105 LOC → submodul ~420 LOC).
 *
 * Exports:
 *   getProjectFilterConfig, getProjectFilterInput, getProjectFilterCurrentUserId,
 *   getProjectFilterStorageKey, normalizeProjectFilterState, buildProjectFilterStateFromInputs,
 *   setProjectFilterSaveStatus, renderProjectFilterChips, handleProjectFilterInputChange,
 *   restoreProjectFilterScope, saveProjectFilterDefaults, clearProjectFilterInput,
 *   clearProjectFilterPreferenceStorage,
 *   compareSubsystemSortMeta, normalizeFilterText, normalizeSubsystemSortMode,
 *   readSubsystemGroupSortMeta, sortSubsystemGroupsInContainer
 */

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

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

/**
 * Sjednocený config pro oba scopes (records, schedule) — DRY refactor 2026-04-30.
 * Spec: docs/superpowers/specs/2026-04-30-project-filter-unification-design.md.
 *
 * Po sjednocení používají oba scopes stejných 10 fields (records data parity nastavena
 * v `_ProjectScheduleTab.cshtml` přes data-filter-* atributy na schedule cards).
 * Rootselector se liší jen pro DOM disambiguation (jeden filter shell aktivní per visible tab).
 */
const projectFilterFields = [
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
];

const proposalFilterFields = [
    { inputKey: "stavNavrhu", stateKey: "stavNavrhu", type: "select", chipLabel: "Stav návrhu" },
    { inputKey: "typNavrhu", stateKey: "typNavrhu", type: "select", chipLabel: "Typ návrhu" },
    { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
    { inputKey: "autor", stateKey: "autor", type: "select", chipLabel: "Autor" },
    { inputKey: "rozhodl", stateKey: "rozhodl", type: "select", chipLabel: "Rozhodl" },
];

function buildProjectFilterConfig(scope, fields) {
    return {
        rootSelector: `[data-project-filter-scope="${scope}"]`,
        inputSelector: "[data-filter-key]",
        keyAttribute: "data-filter-key",
        chipRowSelector: `[data-filter-chip-row="${scope}"]`,
        statusSelector: `[data-filter-save-status="${scope}"]`,
        fields: fields || projectFilterFields
    };
}

const projectFilterConfigs = {
    records: buildProjectFilterConfig("records"),
    schedule: buildProjectFilterConfig("schedule"),
    proposals: buildProjectFilterConfig("proposals", proposalFilterFields)
};

// ---------------------------------------------------------------------------
// Private helpers — text / token normalisation
// ---------------------------------------------------------------------------

export function normalizeFilterText(value) {
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

export function normalizeSubsystemSortMode(value) {
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

export function readSubsystemGroupSortMeta(element) {
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

export function compareSubsystemSortMeta(leftSource, rightSource, sortMode) {
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

export function sortSubsystemGroupsInContainer(container, sortMode) {
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

// ---------------------------------------------------------------------------
// Private helpers — DOM queries
// ---------------------------------------------------------------------------

export function getProjectFilterConfig(scope) {
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

function getProjectFilterProjectId(scope) {
    const root = getProjectFilterRoot(scope);
    const projectId = (root?.dataset.projectId || "").trim();
    return projectId || "0";
}

export function getProjectFilterInput(scope, inputKey) {
    const config = getProjectFilterConfig(scope);
    const root = getProjectFilterRoot(scope);
    if (!config || !(root instanceof HTMLElement)) {
        return null;
    }

    const input = root.querySelector(`${config.inputSelector}[${config.keyAttribute}="${inputKey}"]`);
    if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
        return input;
    }
    // <gov-form-switch> je HTMLElement s reflektovanou `checked` property — chovej se jako checkbox.
    if (input instanceof HTMLElement && input.tagName.toLowerCase() === "gov-form-switch") {
        return input;
    }
    return null;
}

function isGovFormSwitch(el) {
    return el instanceof HTMLElement && el.tagName && el.tagName.toLowerCase() === "gov-form-switch";
}

export function getProjectFilterCurrentUserId(scope) {
    return normalizeFilterToken(getProjectFilterRoot(scope)?.dataset.currentUserId || "");
}

/**
 * Storage klíč pro filter state — sjednocený 2026-04-30 (bez scope segmentu).
 * Klíč: ${prefix}${projectId}.${kind} (kind = "state" | "defaults").
 * Filter state je per-projekt, ne per-tab — Records a Schedule sdílejí stejný
 * objekt, propagace via pm-tab-change event.
 */
export function getProjectFilterStorageKey(scope, kind) {
    const projectId = getProjectFilterProjectId(scope);
    if (!projectId || projectId === "0") {
        return "";
    }

    return `${projectFilterStoragePrefix}${projectId}.${kind}`;
}

/**
 * Jednorázová migrace 2026-04-30: starý storage měl scope segment
 * (`${prefix}${projectId}.records.${kind}` / `.schedule.${kind}`).
 * Při prvním restore po deploy: pokud existuje legacy records klíč, jeho hodnotu
 * uložíme pod nový klíč (bez scope) a smažeme oba scope-suffixed (records i
 * schedule). Schedule legacy hodnoty se zahodí — records je primární zdroj.
 */
function migrateLegacyProjectFilterStorageKeys(projektId) {
    if (!projektId || projektId === "0") return;

    const newStateKey = `${projectFilterStoragePrefix}${projektId}.state`;
    const newDefaultsKey = `${projectFilterStoragePrefix}${projektId}.defaults`;
    const legacyStateRecords = `${projectFilterStoragePrefix}${projektId}.records.state`;
    const legacyStateSchedule = `${projectFilterStoragePrefix}${projektId}.schedule.state`;
    const legacyDefaultsRecords = `${projectFilterStoragePrefix}${projektId}.records.defaults`;
    const legacyDefaultsSchedule = `${projectFilterStoragePrefix}${projektId}.schedule.defaults`;

    try {
        // Defaults (localStorage): records preference je primární, schedule preference se zahodí.
        if (localStorage.getItem(newDefaultsKey) === null) {
            const legacyValue = localStorage.getItem(legacyDefaultsRecords);
            if (legacyValue !== null) {
                localStorage.setItem(newDefaultsKey, legacyValue);
            }
        }
        localStorage.removeItem(legacyDefaultsRecords);
        localStorage.removeItem(legacyDefaultsSchedule);

        // State (sessionStorage): stejný pattern.
        if (sessionStorage.getItem(newStateKey) === null) {
            const legacyValue = sessionStorage.getItem(legacyStateRecords);
            if (legacyValue !== null) {
                sessionStorage.setItem(newStateKey, legacyValue);
            }
        }
        sessionStorage.removeItem(legacyStateRecords);
        sessionStorage.removeItem(legacyStateSchedule);
    } catch {
        // localStorage / sessionStorage disabled or quota exceeded — silent (degrade gracefully).
    }
}

// ---------------------------------------------------------------------------
// Private helpers — storage I/O
// ---------------------------------------------------------------------------

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

// ---------------------------------------------------------------------------
// Private helpers — input value reading/writing
// ---------------------------------------------------------------------------

function hasSelectOptionValue(input, value) {
    if (!(input instanceof HTMLSelectElement)) {
        return false;
    }

    return Array.from(input.options).some((option) => option.value === value);
}

function readProjectFilterInputValue(input, field) {
    if (field.type === "checkbox") {
        if (input instanceof HTMLInputElement || isGovFormSwitch(input)) {
            return !!input.checked;
        }
        return false;
    }

    if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
        return input.value;
    }

    return "";
}

function applyProjectFilterStateToInputs(scope, state) {
    const config = getProjectFilterConfig(scope);
    if (!config) {
        return;
    }

    config.fields.forEach((field) => {
        const input = getProjectFilterInput(scope, field.inputKey);
        if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement || isGovFormSwitch(input))) {
            return;
        }

        const value = state[field.stateKey];
        if (field.type === "checkbox") {
            if (input instanceof HTMLInputElement) {
                input.checked = Boolean(value);
            } else if (isGovFormSwitch(input)) {
                if (Boolean(value)) {
                    input.setAttribute("checked", "");
                } else {
                    input.removeAttribute("checked");
                }
            }
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

// ---------------------------------------------------------------------------
// Private helpers — chips label
// ---------------------------------------------------------------------------

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


// ---------------------------------------------------------------------------
// Public exports — normalization + state read/write
// ---------------------------------------------------------------------------

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

export function persistProjectFilterSessionState(scope) {
    const currentState = buildProjectFilterStateFromInputs(scope);
    const normalizedState = normalizeProjectFilterState(scope, currentState, currentState);
    const key = getProjectFilterStorageKey(scope, "state");
    writeJsonStorage(sessionStorage, key, normalizedState);
    return normalizedState;
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

export function restoreProjectFilterScope(scope) {
    if (scope === "proposals") {
        const fallbackState = buildProjectFilterStateFromInputs(scope);
        applyProjectFilterStateToInputs(scope, fallbackState);
        renderProjectFilterChips(scope);
        return fallbackState;
    }

    // 2026-04-30: migrace ze starých scope-suffixed storage klíčů (records + schedule
    // měly oddělené state/defaults). Spec: project-filter-unification-design.
    migrateLegacyProjectFilterStorageKeys(getProjectFilterProjectId(scope));

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
    if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement || isGovFormSwitch(input))) {
        return;
    }

    if (input instanceof HTMLInputElement && input.type === "checkbox") {
        input.checked = false;
    }
    else if (isGovFormSwitch(input)) {
        input.removeAttribute("checked");
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
