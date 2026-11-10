/**
 * schedule/block.js — ScheduleBlockRenderer class.
 *
 * Fáze 3B Task 2: vyextrahováno ze schedule.js (1697 LOC).
 * Živý náhled editoru harmonogramu: souhrn, graf a meze kalendáře plánu. Pole trvání
 * a odchylky ve dnech, tlačítka +/− a Rozpad z offsetového modelu odstraněny 2026-11-10.
 */

import {
    diffCalendarDays,
    formatDisplayDate,
    formatIsoDate,
    parseIsoDate,
    toUtcDayStamp
} from "../utils.js";
import { queueRainbowSegmentRender } from "../ui.js";
import { renderTimelineAxis, renderTicksFromList } from "./timeline.js";
import { computeAxisLayout } from "./scheduleAxis.js";
import { formatPlanCompletion, formatScheduleState, formatTotalDuration, summarizeSchedule } from "./scheduleStatus.js";
import { planBounds } from "../harmonogram/planChronology.js";

/**
 * „Dnes" pro editor live-preview = lokální datum dodané serverem v data-schedule-today
 * (sjednocený zdroj s osou). Fallback new Date() jen kdyby atribut chyběl.
 */
export function resolveToday(root) {
    const iso = root && root.dataset ? root.dataset.scheduleToday : null;
    return parseIsoDate(iso) || new Date();
}

/**
 * Sestaví vstup kroků pro computeAxisLayout z editor modelu (plan/actual/state).
 * Aktuální (běžící) krok má maSkutecnost=true, aby contentEnd zahrnul „dnes" — shodně se serverem.
 */
function buildAxisSteps(plan, actual, state) {
    return state.map((s, i) => ({
        poradi: s.stepIndex,
        planStart: plan[i].start,
        planEnd: plan[i].end,
        maSkutecnost: s.hasActual || s.jeAktualni,
        skutecnostStart: actual[i].start,
        skutecnostEnd: actual[i].end
    }));
}

// Datum-model (Fáze 3b): /Schedule/Recalc endpoint i offsetový buildSchedulePlanAndActual
// zrušeny. Editor live-preview počítá čistě z datumů (ScheduleBlockRenderer.computeDateModel).

function toSchedulePercent(valueDate, axisStart, totalDays) {
    const days = diffCalendarDays(valueDate, axisStart);
    return Math.max(0, Math.min(100, (days * 100) / totalDays));
}

function formatSchedulePercent(value) {
    return `${Number.isFinite(value) ? value.toFixed(4) : "0.0000"}%`;
}

function formatScheduleSegmentWidth(value) {
    if (!Number.isFinite(value) || value <= 0) {
        return "0%";
    }

    // Sjednocení souřadnic s osou/markery (2026-06-23): bez +1px hacku, šířka = přesné %.
    return `${value.toFixed(4)}%`;
}

export class ScheduleBlockRenderer {
    constructor(root, options = {}) {
        this.root = root;
        this.mode = String(root.dataset.scheduleMode || "project-readonly").trim() || "project-readonly";
        this.form = options.form instanceof HTMLFormElement
            ? options.form
            : root.closest('form[data-record-editor-form="true"]');
        this.startInput = this.form instanceof HTMLFormElement
            ? this.form.querySelector('input[name="DatumZalozeni"]')
            : null;
        this.deadlineInput = this.form instanceof HTMLFormElement
            ? this.form.querySelector('input[name="TerminUkonceni"]')
            : null;
        this.summaryDeadline = root.querySelector("[data-schedule-summary-deadline]");
        this.summaryBaseline = root.querySelector("[data-schedule-summary-baseline]");
        this.summaryDuration = root.querySelector("[data-schedule-summary-duration]");
        this.summaryState = root.querySelector("[data-schedule-summary-state]");
        this.statusLine = root.querySelector(".schedule-status-line");
        this.overviewAxis = root.querySelector('[data-schedule-axis="overview"]');
        // „Dnes"/„Termín" jsou nově popisky DNES/TERMÍN na ose (renderTicksFromList z data-axis-*-pct),
        // ne svislé čáry v pruzích — žádné per-řádkové markery se v block.js nedrží.
        // Editor nemá Rozpad (ten kreslí jen server na kartě a v záložce), proto ho block.js neřeší.
        this.overviewPlannedSegments = this.collectSegmentMap('[data-schedule-segment-kind="planned"]');
        this.overviewActualSegments = this.collectSegmentMap('[data-schedule-segment-kind="actual"]');
        // TODO F-04: editorRows je pole řazené dle stepIndex (KrokIndex, 1-based).
        // Přístup přes this.editorRows[index] (applyPlanChronologyBounds) používá 0-based
        // forEach index — to funguje správně, pokud KrokIndex hodnoty nemají mezery. Pokud by
        // mezery nastaly (smazaný krok), je potřeba přepsat na Map<stepIndex, entry>.
        this.editorRows = Array.from(root.querySelectorAll("tr[data-schedule-step-row]"))
            .map((row) => {
                if (!(row instanceof HTMLTableRowElement)) {
                    return null;
                }

                const stepIndex = Number.parseInt(row.dataset.stepIndex || "", 10);
                if (!Number.isInteger(stepIndex)) {
                    return null;
                }

                return {
                    row,
                    stepIndex,
                    // Spec 2026-10-24: skryté pole plánu uvnitř <pm-date-field>. Selektor
                    // [data-schedule-duration-calendar] trefil obal (nese stejný atribut a je v DOM
                    // dřív), takže meze kalendáře ani přepočet po změně data nikdy nefungovaly.
                    planInput: row.querySelector('input[name$=".PlanDatum"]')
                };
            })
            .filter((entry) => entry && Number.isInteger(entry.stepIndex))
            .sort((left, right) => left.stepIndex - right.stepIndex);
    }

    collectSegmentMap(selector) {
        const segments = new Map();
        this.root.querySelectorAll(selector).forEach((node) => {
            if (!(node instanceof HTMLElement)) {
                return;
            }

            const stepIndex = Number.parseInt(node.dataset.stepIndex || "", 10);
            if (!Number.isInteger(stepIndex)) {
                return;
            }

            segments.set(stepIndex, node);
        });
        return segments;
    }

    isReady() {
        return this.root instanceof HTMLElement && this.editorRows.length > 0;
    }

    getStartDate() {
        if (this.startInput instanceof HTMLInputElement) {
            return parseIsoDate(this.startInput.value) || new Date();
        }

        return parseIsoDate(this.root.dataset.scheduleStart) || new Date();
    }

    getDeadlineDate(startDate) {
        if (this.deadlineInput instanceof HTMLInputElement) {
            return parseIsoDate(this.deadlineInput.value) || startDate;
        }

        return parseIsoDate(this.root.dataset.scheduleDeadline) || startDate;
    }

    setAxisRange(axis, startDate, endDate, monthTicks = null, todayPct = null, deadlinePct = null) {
        if (!(axis instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
            return;
        }

        axis.dataset.axisStart = formatIsoDate(startDate);
        axis.dataset.axisEnd = formatIsoDate(endDate);
        // „DNES"/„TERMÍN" popisky na ose: renderTicksFromList je čte z data-axis-today-pct /
        // data-axis-deadline-pct. null = datum mimo osu → popisek se nevykreslí.
        if (Number.isFinite(todayPct)) {
            axis.dataset.axisTodayPct = String(todayPct);
        } else {
            delete axis.dataset.axisTodayPct;
        }
        if (Number.isFinite(deadlinePct)) {
            axis.dataset.axisDeadlinePct = String(deadlinePct);
        } else {
            delete axis.dataset.axisDeadlinePct;
        }
        // Kanonická osa: máme měsíční ticky → kreslíme je (shodně se statickou kartou).
        if (Array.isArray(monthTicks)) {
            renderTicksFromList(axis, monthTicks.map((t) => ({ left: t.left, label: t.label })));
        } else {
            renderTimelineAxis(axis, startDate, endDate);
        }
    }

    applySegmentLayout(segment, leftPercent, widthPercent, title) {
        if (!(segment instanceof HTMLElement)) {
            return;
        }

        segment.style.left = formatSchedulePercent(leftPercent);
        segment.style.width = formatScheduleSegmentWidth(widthPercent);
        segment.style.display = widthPercent > 0 ? "" : "none";
        if (title) {
            segment.title = title;
        }
    }

    resetSegmentLayout(segment) {
        if (!(segment instanceof HTMLElement)) {
            return;
        }

        segment.style.left = "0%";
        segment.style.width = "0%";
        segment.style.display = "none";
    }

    renderSummary(state, startDate, deadlineDate) {
        // Spec 2026-10-24: Stav, Plán dokončení i Trvání ze sdíleného výpočtu scheduleStatus.js —
        // zrcadlo serverového ScheduleDateCalculator.Summarize + ScheduleStatusText (golden vektory).
        const souhrn = summarizeSchedule({
            startIso: formatIsoDate(startDate),
            terminIso: formatIsoDate(deadlineDate),
            todayIso: formatIsoDate(resolveToday(this.root)),
            steps: state.map((item) => ({
                poradi: item.stepIndex,
                nazev: item.name,
                planIso: item.planIso,
                skutecnostIso: item.actualIso
            }))
        });

        if (this.summaryDeadline instanceof HTMLElement) {
            this.summaryDeadline.textContent = formatDisplayDate(deadlineDate);
        }
        if (this.summaryBaseline instanceof HTMLElement) {
            this.summaryBaseline.textContent = formatPlanCompletion(souhrn.planoveDokonceniIso);
        }
        if (this.summaryDuration instanceof HTMLElement) {
            this.summaryDuration.textContent = formatTotalDuration(souhrn.planoveDokonceniIso, souhrn.celkoveTrvaniDni);
        }
        if (this.summaryState instanceof HTMLElement) {
            this.summaryState.textContent = formatScheduleState(souhrn);
        }
        if (this.statusLine instanceof HTMLElement) {
            this.statusLine.classList.toggle("ok", souhrn.stihame);
            this.statusLine.classList.toggle("late", !souhrn.stihame);
        }
    }

    renderOverview(plan, actual, state, startDate, deadlineDate) {
        // Kanonická osa (shodná se serverem / statickou kartou): přichycená na měsíce.
        const layout = computeAxisLayout({
            start: startDate,
            deadline: deadlineDate,
            today: resolveToday(this.root),
            steps: buildAxisSteps(plan, actual, state)
        });
        const axisStart = layout.axisStart;
        const totalDays = layout.totalDays;

        // „Dnes" i „Termín" už nejsou svislé čáry v pruhu — vykreslí se jako popisky DNES/TERMÍN na ose.
        this.setAxisRange(this.overviewAxis, axisStart, layout.axisEnd, layout.monthTicks, layout.todayPct, layout.deadlinePct);

        let previousPlanRight = 0;
        let previousActualRight = 0;
        let skippedCompactActualWidth = 0;

        state.forEach((item, index) => {
            const planItem = plan[index];
            const actualItem = actual[index];
            const plannedSegment = this.overviewPlannedSegments.get(item.stepIndex);
            const actualSegment = this.overviewActualSegments.get(item.stepIndex);
            if (!planItem || !actualItem) {
                this.resetSegmentLayout(plannedSegment);
                this.resetSegmentLayout(actualSegment);
                return;
            }

            const rawPlanLeft = toSchedulePercent(planItem.start, axisStart, totalDays);
            const rawPlanRight = toSchedulePercent(planItem.end, axisStart, totalDays);
            const rawActualLeft = toSchedulePercent(actualItem.start, axisStart, totalDays);
            const rawActualRight = toSchedulePercent(actualItem.end, axisStart, totalDays);

            if (item.duration > 0) {
                skippedCompactActualWidth = 0;  // Reset při každém viditelném kroku
                const planLeft = Math.max(previousPlanRight, rawPlanLeft);
                const planRight = Math.max(planLeft, rawPlanRight);
                const planWidth = Math.max(0, planRight - planLeft);
                this.applySegmentLayout(
                    plannedSegment,
                    planLeft,
                    planWidth,
                    `${item.name}: plán ${formatDisplayDate(planItem.start)}-${formatDisplayDate(planItem.end)}`);
                previousPlanRight = planRight;

                const adjustedActualLeft = Math.max(0, rawActualLeft - skippedCompactActualWidth);
                const adjustedActualRight = Math.max(adjustedActualLeft, rawActualRight - skippedCompactActualWidth);
                const actualLeft = Math.max(previousActualRight, adjustedActualLeft);
                const actualRight = Math.max(actualLeft, adjustedActualRight);
                const actualWidth = Math.max(0, actualRight - actualLeft);
                if (actualWidth <= 0) {
                    this.resetSegmentLayout(actualSegment);
                } else {
                    this.applySegmentLayout(
                        actualSegment,
                        actualLeft,
                        actualWidth,
                        `${item.name}: skutečnost ${formatDisplayDate(actualItem.start)}-${formatDisplayDate(actualItem.end)}`);
                }
                previousActualRight = actualRight;
            } else {
                // Akumuluj přeskočenou šířku jen pro bezprostředně sousední nulové kroky.
                // Reset se provede při dalším duration>0 kroku níže.
                skippedCompactActualWidth += Math.max(0, rawActualRight - rawActualLeft);
                this.resetSegmentLayout(plannedSegment);
                this.resetSegmentLayout(actualSegment);
            }
        });
    }

    // Datum-model (Fáze 3b): editor live-preview počítá POUZE z datumů na frontendu
    // (plán z [name$=".PlanDatum"], skutečnost z manuálního inputu nebo data-step-actual-iso).
    // Žádný server round-trip (/Schedule/Recalc zrušen), žádné offsety. Mirror serverového
    // ScheduleDateCalculator + ScheduleBarLayoutCalculator — render metody pozicují z {start,end}.
    recalcAll() {
        const { plan, actual, state, startDate } = this.computeDateModel();
        if (state.length === 0) {
            return;
        }
        const deadlineDate = this.getDeadlineDate(startDate);
        this.renderSummary(state, startDate, deadlineDate);
        this.renderOverview(plan, actual, state, startDate, deadlineDate);
        this.applyPlanChronologyBounds();
        queueRainbowSegmentRender(this.root);
    }

    // Spec 2026-10-24 R10: meze kalendáře plánu = nejbližší kroky s plánem (pravidlo serveru
    // ScheduleChronologyValidator; kroky bez plánu se přeskakují). Nastaví min/max na host
    // <pm-date-field>, kalendář dny mimo meze vypne (date.js).
    applyPlanChronologyBounds() {
        const plans = this.editorRows.map((entry) =>
            entry.planInput instanceof HTMLInputElement ? entry.planInput.value.trim() : "");
        const bounds = planBounds(plans);
        this.editorRows.forEach((entry, index) => {
            const host = entry.planInput instanceof HTMLInputElement ? entry.planInput.closest("pm-date-field") : null;
            if (!host) {
                return;
            }
            const { min, max } = bounds[index];
            if (min) host.setAttribute("min", min); else host.removeAttribute("min");
            if (max) host.setAttribute("max", max); else host.removeAttribute("max");
        });
    }

    rowDateIso(row, suffix) {
        if (!(row instanceof HTMLElement)) {
            return null;
        }
        const input = row.querySelector(`input[name$=".${suffix}"]`);
        if (input instanceof HTMLInputElement && input.value) {
            return input.value.trim();
        }
        return null;
    }

    // Plán: [name$=".PlanDatum"]. Skutečnost: HarmonogramHodnoty[*].SkutecnostDatum (spec 2026-10-14),
    // prázdné pole (vyčištěné přepínačem) → uložená hodnota z data-step-actual-iso.
    computeDateModel() {
        const startDate = this.getStartDate();
        let planCursor = new Date(startDate.getTime());
        let actualCursor = new Date(startDate.getTime());
        const plan = [];
        const actual = [];
        const state = [];

        this.editorRows.forEach((entry) => {
            const row = entry.row;
            const planIso = this.rowDateIso(row, "PlanDatum");
            const planEndRaw = parseIsoDate(planIso);
            const planStart = new Date(planCursor.getTime());
            let planEnd = planEndRaw || new Date(planStart.getTime());
            if (toUtcDayStamp(planEnd) < toUtcDayStamp(planStart)) {
                planEnd = new Date(planStart.getTime());
            }
            plan.push({ start: planStart, end: planEnd });
            planCursor = new Date(planEnd.getTime());

            const actualIso = this.rowDateIso(row, "SkutecnostDatum")
                || (String(row.dataset.stepActualIso || "").trim() || null);
            const actualDate = parseIsoDate(actualIso);
            const actualStart = new Date(actualCursor.getTime());
            let actualEnd = new Date(actualCursor.getTime());
            let hasActual = false;
            if (actualDate) {
                actualEnd = actualDate;
                if (toUtcDayStamp(actualEnd) < toUtcDayStamp(actualStart)) {
                    actualEnd = new Date(actualStart.getTime());
                }
                actualCursor = new Date(actualEnd.getTime());
                hasActual = true;
            }
            actual.push({ start: actualStart, end: actualEnd });

            state.push({
                stepIndex: entry.stepIndex,
                name: String(row.dataset.stepName || "").trim(),
                color: String(row.dataset.stepColor || "").trim(),
                duration: Math.max(0, diffCalendarDays(planEnd, planStart)),
                delay: hasActual ? diffCalendarDays(actualEnd, planEnd) : 0,
                hasActual,
                jeAktualni: false,
                planIso,
                actualIso: actualDate ? actualIso : null
            });
        });

        // Datum-model: aktuální krok = první nevyplněný PO posledním vyplněném. Kreslí skutečnost
        // do dneška (rozpracováno/prodlení) — mirror serverového ScheduleDateCalculator.Compute.
        let lastFilled = -1;
        for (let i = 0; i < state.length; i++) {
            if (state[i].hasActual) lastFilled = i;
        }
        let aktualniIndex = -1;
        for (let i = 0; i < state.length; i++) {
            if (!state[i].hasActual && i > lastFilled) { aktualniIndex = i; break; }
        }
        if (aktualniIndex >= 0) {
            const todayDate = resolveToday(this.root);
            const st = actual[aktualniIndex].start;
            actual[aktualniIndex].end = toUtcDayStamp(todayDate) > toUtcDayStamp(st) ? todayDate : st;
            state[aktualniIndex].jeAktualni = true;
        }

        return { plan, actual, state, startDate };
    }

    bind() {
        if (this.mode !== "record-editor") {
            return;
        }

        // Spec 2026-10-24 R9: výběr data i ✕ u plánu nebo skutečnosti přepočítá souhrn a graf hned.
        // date.js posílá „change“ ze skrytého pole uvnitř <pm-date-field>; delegace z bloku ho chytí
        // u všech řádků (posluchač na obalu pole se nikdy nespustil).
        this.root.addEventListener("change", (event) => {
            const target = event.target;
            if (target instanceof HTMLInputElement && /\.(PlanDatum|SkutecnostDatum)$/.test(target.name)) {
                this.recalcAll();
            }
        });

        if (this.startInput instanceof HTMLInputElement) {
            this.startInput.addEventListener("change", () => this.recalcAll());
        }
        if (this.deadlineInput instanceof HTMLInputElement) {
            this.deadlineInput.addEventListener("change", () => this.recalcAll());
        }
    }
}
