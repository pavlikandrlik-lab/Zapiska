/**
 * DESIGN-9-F (2026-05-02) — primární editor plánu kroku je kalendář (datum konce kroku),
 * trvání ve dnech je text vedle něj (data-schedule-duration-readonly).
 *
 * Spec 2026-10-22 R13 — přepis na datum-model. Dřívější verze hledala skrytý vstup trvání
 * (počet dnů), který datum-model zrušil, takže přepočet tiše nedělal nic
 * a text trvání zůstával z načtení stránky. Teď při každé změně data (i po ✕):
 *   - krok bez plánu má trvání „–“;
 *   - začátek kroku = konec nejbližšího předchozího kroku s plánem, jinak datum založení
 *     (data-schedule-start na .schedule-block) — jako server (ScheduleDateCalculator);
 *   - plán dřív než plán předchozího kroku s plánem se označí a uložení se zastaví —
 *     stejné pravidlo jako server (ScheduleChronologyValidator), proti založení se nekontroluje.
 * Změna kroku N přepočítá kroky N..10 (posune se začátek dalších kroků).
 *
 * Side-effect import v bootstrap.js (memory: project_bundle_sync).
 */
import { daysBetween, formatDuration, planStart } from "./planDuration.js";

const ROW_SELECTOR = "[data-schedule-step-row]";
const CALENDAR_SELECTOR = "[data-schedule-duration-calendar]";
const READONLY_SELECTOR = "[data-schedule-duration-readonly]";
const SCHEDULE_BLOCK_SELECTOR = "[data-schedule-start]";

function stepIndexOf(row) {
    return Number.parseInt(row.getAttribute("data-step-index") || "0", 10);
}

function readPlanIso(row) {
    const calendar = row.querySelector(CALENDAR_SELECTOR);
    const hidden = calendar?.querySelector('input[type="hidden"][data-app-date-value]');
    return hidden?.value || null;
}

function readPlansByStep(rows) {
    const plans = new Map();
    rows.forEach((row) => {
        const iso = readPlanIso(row);
        if (iso) {
            plans.set(stepIndexOf(row), iso);
        }
    });
    return plans;
}

function formatDisplayDate(iso) {
    const [year, month, day] = iso.slice(0, 10).split("-");
    return `${day}.${month}.${year}`;
}

function markRowInvalid(row, invalid, message) {
    if (invalid) {
        row.setAttribute("data-schedule-duration-invalid", "true");
    } else {
        row.removeAttribute("data-schedule-duration-invalid");
    }
    // Tooltip + aria — na pm-date-field i na vnitřní zobrazovací vstup.
    const calendar = row.querySelector(CALENDAR_SELECTOR);
    if (!(calendar instanceof HTMLElement)) {
        return;
    }
    if (invalid) {
        calendar.setAttribute("title", message);
        calendar.setAttribute("aria-invalid", "true");
    } else {
        calendar.removeAttribute("title");
        calendar.removeAttribute("aria-invalid");
    }
    const display = calendar.querySelector("input[data-app-date-display]");
    if (display instanceof HTMLInputElement) {
        if (invalid) {
            display.setAttribute("title", message);
        } else {
            display.removeAttribute("title");
        }
    }
}

function recalcRow(row, recordStartIso, plans) {
    const readonly = row.querySelector(READONLY_SELECTOR);
    if (!(readonly instanceof HTMLElement)) {
        return;
    }

    const endIso = readPlanIso(row);
    if (!endIso) {
        readonly.textContent = formatDuration(null);
        row.setAttribute("data-step-duration", "0");
        markRowInvalid(row, false, "");
        return;
    }

    const start = planStart(plans, stepIndexOf(row), recordStartIso);
    const rawDays = daysBetween(start.iso, endIso);
    const days = Math.max(0, rawDays ?? 0);
    readonly.textContent = formatDuration(days);
    row.setAttribute("data-step-duration", String(days));

    if (start.fromPreviousStep && rawDays !== null && rawDays < 0) {
        markRowInvalid(row, true,
            `Datum konce kroku nesmí být dříve než ${formatDisplayDate(start.iso)} (konec předchozího kroku s plánem).`);
    } else {
        markRowInvalid(row, false, "");
    }
}

function recalcChainFrom(stepIndex, scope) {
    const rows = Array.from(scope.querySelectorAll(ROW_SELECTOR))
        .sort((left, right) => stepIndexOf(left) - stepIndexOf(right));
    const plans = readPlansByStep(rows);
    const startAttr = scope instanceof HTMLElement ? scope.getAttribute("data-schedule-start") : null;
    const recordStartIso = startAttr || new Date().toISOString().slice(0, 10);
    rows.filter((row) => stepIndexOf(row) >= stepIndex)
        .forEach((row) => recalcRow(row, recordStartIso, plans));
}

function init() {
    // FIX 2026-05-02: capture fáze + setTimeout 0 — přepočet proběhne až po ostatních
    // posluchačích change (block.js), poslední slovo má tenhle modul.
    document.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement)) {
            return;
        }
        const row = target.closest(CALENDAR_SELECTOR)?.closest(ROW_SELECTOR);
        if (!(row instanceof HTMLElement)) {
            return;
        }
        const scope = row.closest(SCHEDULE_BLOCK_SELECTOR) || document;
        const stepIndex = stepIndexOf(row);
        setTimeout(() => recalcChainFrom(stepIndex, scope), 0);
    }, true);

    // FIX 2026-05-05: uložení se zastaví, když je některý krok označený jako nechronologický —
    // uživatel hned vidí, které pole to je (server by uložení odmítl stejným pravidlem).
    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }
        const invalidRow = form.querySelector(`${ROW_SELECTOR}[data-schedule-duration-invalid="true"]`);
        if (!invalidRow) {
            return;
        }
        event.preventDefault();
        const calendar = invalidRow.querySelector(CALENDAR_SELECTOR);
        const display = calendar?.querySelector("input[data-app-date-display]");
        if (display instanceof HTMLInputElement) {
            display.scrollIntoView({ behavior: "smooth", block: "center" });
            display.focus({ preventScroll: true });
        }
        window.alert(calendar?.getAttribute("title")
            || "Některý krok harmonogramu má datum konce dříve než předchozí krok.");
    }, true);
}

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
} else {
    init();
}
