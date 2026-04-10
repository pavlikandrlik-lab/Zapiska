import { normalizeFilterText } from "./filters.js";

function parseCzechDateTime(value) {
    const text = (value || "").trim();
    if (!text || text === "-") {
        return null;
    }

    const match = text.match(/^(\d{2})\.(\d{2})\.(\d{4})(?:\s+(\d{2}):(\d{2}))?$/);
    if (!match) {
        return null;
    }

    const [, dayText, monthText, yearText, hourText, minuteText] = match;
    const day = Number.parseInt(dayText, 10);
    const month = Number.parseInt(monthText, 10) - 1;
    const year = Number.parseInt(yearText, 10);
    const hour = Number.parseInt(hourText || "0", 10);
    const minute = Number.parseInt(minuteText || "0", 10);
    const valueDate = new Date(year, month, day, hour, minute, 0, 0);
    return Number.isNaN(valueDate.getTime()) ? null : valueDate.getTime();
}

function parseBooleanValue(value) {
    const normalized = normalizeFilterText(value);
    if (!normalized || normalized === "-") {
        return null;
    }

    if (["ano", "true", "1", "yes"].includes(normalized)) {
        return 1;
    }

    if (["ne", "false", "0", "no"].includes(normalized)) {
        return 0;
    }

    return null;
}

function parseComparableValue(rawValue, type) {
    if (type === "datetime") {
        return parseCzechDateTime(rawValue);
    }

    if (type === "boolean") {
        return parseBooleanValue(rawValue);
    }

    if (type === "number") {
        const normalized = (rawValue || "").trim().replace(/\s+/g, "").replace(",", ".");
        if (!normalized || normalized === "-") {
            return null;
        }

        const parsed = Number.parseFloat(normalized);
        return Number.isFinite(parsed) ? parsed : null;
    }

    return normalizeFilterText(rawValue);
}

function resolveCellValue(row, columnIndex) {
    const cells = Array.from(row.children).filter((cell) => cell instanceof HTMLTableCellElement);
    const cell = cells[columnIndex];
    if (!(cell instanceof HTMLTableCellElement)) {
        return "";
    }

    return (cell.dataset.tableSortValue || cell.textContent || "").trim();
}

function buildSearchText(row) {
    const explicit = (row.dataset.tableSearchText || "").trim();
    if (explicit) {
        return normalizeFilterText(explicit);
    }

    const text = Array.from(row.children)
        .filter((cell) => cell instanceof HTMLTableCellElement && !cell.classList.contains("table-actions"))
        .map((cell) => cell.textContent || "")
        .join(" ");

    return normalizeFilterText(text);
}

function ensureEmptyRow(table) {
    const tbody = table.tBodies[0];
    if (!(tbody instanceof HTMLTableSectionElement)) {
        return null;
    }

    const existing = tbody.querySelector("[data-table-empty-row]");
    if (existing instanceof HTMLTableRowElement) {
        return existing;
    }

    const headerCells = table.tHead?.rows[0]?.cells;
    const colspan = headerCells?.length || 1;
    const message = (table.dataset.tableEmptyMessage || "Žádné výsledky.").trim();

    const row = document.createElement("tr");
    row.setAttribute("data-table-empty-row", "");
    row.hidden = true;

    const cell = document.createElement("td");
    cell.colSpan = colspan;
    cell.className = "table-empty-row";
    cell.textContent = message;
    row.appendChild(cell);
    tbody.appendChild(row);
    return row;
}

function getDataRows(table) {
    return Array.from(table.tBodies[0]?.querySelectorAll("tr[data-table-row]") || [])
        .filter((row) => row instanceof HTMLTableRowElement);
}

function updateSortIndicators(table) {
    const activeIndex = table.dataset.tableSortIndex || "";
    const direction = table.dataset.tableSortDirection === "desc" ? "desc" : "asc";

    table.querySelectorAll("[data-table-sort-button]").forEach((button) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        const sortIndex = button.dataset.tableSortIndex || "";
        const isActive = sortIndex === activeIndex;
        button.dataset.tableSortDirection = isActive ? direction : "";
        button.classList.toggle("is-active", isActive);
        button.setAttribute("aria-sort", isActive ? (direction === "desc" ? "descending" : "ascending") : "none");
    });
}

function compareRows(left, right, columnIndex, type, direction) {
    const leftValue = parseComparableValue(resolveCellValue(left, columnIndex), type);
    const rightValue = parseComparableValue(resolveCellValue(right, columnIndex), type);

    if (leftValue === null && rightValue === null) {
        return 0;
    }

    if (leftValue === null) {
        return 1;
    }

    if (rightValue === null) {
        return -1;
    }

    if (leftValue < rightValue) {
        return direction === "desc" ? 1 : -1;
    }

    if (leftValue > rightValue) {
        return direction === "desc" ? -1 : 1;
    }

    return 0;
}

function sortTableRows(table) {
    const sortIndexText = table.dataset.tableSortIndex || "";
    if (!sortIndexText) {
        return;
    }

    const sortIndex = Number.parseInt(sortIndexText, 10);
    if (!Number.isInteger(sortIndex) || sortIndex < 0) {
        return;
    }

    const button = table.querySelector(`[data-table-sort-button][data-table-sort-index="${sortIndex}"]`);
    const sortType = button instanceof HTMLButtonElement ? (button.dataset.tableSortType || "text") : "text";
    const direction = table.dataset.tableSortDirection === "desc" ? "desc" : "asc";
    const rows = getDataRows(table);
    const sorted = rows
        .map((row, index) => ({ row, index }))
        .sort((left, right) => {
            const result = compareRows(left.row, right.row, sortIndex, sortType, direction);
            return result !== 0 ? result : left.index - right.index;
        });

    const tbody = table.tBodies[0];
    if (!(tbody instanceof HTMLTableSectionElement)) {
        return;
    }

    const emptyRow = ensureEmptyRow(table);
    sorted.forEach((entry) => tbody.appendChild(entry.row));
    if (emptyRow instanceof HTMLTableRowElement) {
        tbody.appendChild(emptyRow);
    }
}

function filterTableRows(root, table) {
    const query = normalizeFilterText(root.querySelector("[data-table-tools-search-input]") instanceof HTMLInputElement
        ? root.querySelector("[data-table-tools-search-input]").value
        : "");
    const rows = getDataRows(table);
    let visibleCount = 0;

    rows.forEach((row) => {
        const matches = !query || buildSearchText(row).includes(query);
        row.hidden = !matches;
        if (matches) {
            visibleCount += 1;
        }
    });

    const emptyRow = ensureEmptyRow(table);
    if (emptyRow instanceof HTMLTableRowElement) {
        emptyRow.hidden = visibleCount > 0;
    }
}

function applyTableState(root, table) {
    sortTableRows(table);
    filterTableRows(root, table);
    updateSortIndicators(table);
}

function handleSortButtonClick(root, button) {
    const table = button.closest("[data-table-tools-table]");
    if (!(table instanceof HTMLTableElement)) {
        return;
    }

    const requestedIndex = button.dataset.tableSortIndex || "";
    const currentIndex = table.dataset.tableSortIndex || "";
    const currentDirection = table.dataset.tableSortDirection === "desc" ? "desc" : "asc";
    const nextDirection = currentIndex === requestedIndex && currentDirection === "asc" ? "desc" : "asc";

    table.dataset.tableSortIndex = requestedIndex;
    table.dataset.tableSortDirection = nextDirection;
    applyTableState(root, table);
}

function initTable(root) {
    if (!(root instanceof HTMLElement) || root.dataset.tableToolsReady === "true") {
        return;
    }

    root.dataset.tableToolsReady = "true";
    const tables = Array.from(root.querySelectorAll("[data-table-tools-table]"))
        .filter((table) => table instanceof HTMLTableElement);

    if (tables.length === 0) {
        return;
    }

    const searchInput = root.querySelector("[data-table-tools-search-input]");
    if (searchInput instanceof HTMLInputElement) {
        searchInput.addEventListener("input", () => {
            tables.forEach((table) => applyTableState(root, table));
        });
    }

    root.addEventListener("click", (event) => {
        const target = event.target instanceof Element ? event.target.closest("[data-table-sort-button]") : null;
        if (!(target instanceof HTMLButtonElement)) {
            return;
        }

        event.preventDefault();
        handleSortButtonClick(root, target);
    });

    tables.forEach((table) => {
        const defaultButton = table.querySelector("[data-table-sort-button][data-table-sort-default='true']");
        if (defaultButton instanceof HTMLButtonElement) {
            table.dataset.tableSortIndex = defaultButton.dataset.tableSortIndex || "";
            table.dataset.tableSortDirection = defaultButton.dataset.tableSortDefaultDirection === "desc" ? "desc" : "asc";
        }

        applyTableState(root, table);
    });
}

export function initTableTools(scope = document) {
    const roots = scope instanceof HTMLElement
        ? (scope.matches("[data-table-tools-root]") ? [scope] : Array.from(scope.querySelectorAll("[data-table-tools-root]")))
        : Array.from(document.querySelectorAll("[data-table-tools-root]"));

    roots.forEach((root) => {
        if (root instanceof HTMLElement) {
            initTable(root);
        }
    });
}
