/**
 * schedule/scheduleAxis.js — kanonický výpočet osy harmonogramu (JS dvojče C#
 * ScheduleBarLayoutCalculator). Osa je přichycená na celé měsíce; markery „dnes"/„termín"
 * jsou null mimo interval. Parita s C# je zamčená golden-vector fixtures
 * (tests/js/schedule/__fixtures__/axis-cases.json) — node:test + xUnit nad týmiž daty.
 *
 * Používá ho editor live-preview (block.js). Statická karta čte hotové pozice ze serveru.
 */

import { diffCalendarDays } from "../utils.js";

const firstDayOfMonth = (d) => new Date(d.getFullYear(), d.getMonth(), 1);
const clampPct = (v) => Math.max(0, Math.min(100, v));
const round4 = (v) => Math.round(v * 10000) / 10000;

/**
 * @param {{start: Date, deadline: Date, today: Date, steps: Array<{poradi:number, planStart:Date, planEnd:Date, maSkutecnost:boolean, skutecnostStart:Date, skutecnostEnd:Date}>}} input
 * @returns {{axisStart: Date, axisEnd: Date, totalDays: number, todayPct: number|null, deadlinePct: number|null, segments: Array, monthTicks: Array<{left:number,label:string}>}}
 */
export function computeAxisLayout({ start, deadline, today, steps }) {
    let contentEnd = deadline;
    for (const s of steps) {
        if (diffCalendarDays(s.planEnd, contentEnd) > 0) contentEnd = s.planEnd;
        if (s.maSkutecnost && diffCalendarDays(s.skutecnostEnd, contentEnd) > 0) contentEnd = s.skutecnostEnd;
    }

    const axisStart = firstDayOfMonth(start);
    // axisEnd = 1. den měsíce PO obsahu → pravý okraj je vždy popsaný měsíční předěl (tick na 100 %).
    // Mirror C# ScheduleBarLayoutCalculator.
    const axisEnd = new Date(contentEnd.getFullYear(), contentEnd.getMonth() + 1, 1);
    const totalDays = Math.max(1, diffCalendarDays(axisEnd, axisStart));

    const pct = (d) => round4(clampPct((diffCalendarDays(d, axisStart) * 100) / totalDays));
    const width = (a, b) => round4(clampPct((Math.max(0, diffCalendarDays(b, a)) * 100) / totalDays));
    const inAxis = (d) => diffCalendarDays(d, axisStart) >= 0 && diffCalendarDays(axisEnd, d) >= 0;

    const segments = steps
        .slice()
        .sort((a, b) => a.poradi - b.poradi)
        .map((s) => ({
            poradi: s.poradi,
            planLeft: pct(s.planStart),
            planWidth: width(s.planStart, s.planEnd),
            hasActual: s.maSkutecnost,
            actualLeft: s.maSkutecnost ? pct(s.skutecnostStart) : 0,
            actualWidth: s.maSkutecnost ? width(s.skutecnostStart, s.skutecnostEnd) : 0
        }));

    const monthTicks = [];
    for (let m = new Date(axisStart); m <= axisEnd; m = new Date(m.getFullYear(), m.getMonth() + 1, 1)) {
        monthTicks.push({
            left: round4((diffCalendarDays(m, axisStart) * 100) / totalDays),
            label: `${String(m.getMonth() + 1).padStart(2, "0")}/${m.getFullYear()}`
        });
    }

    return {
        axisStart,
        axisEnd,
        totalDays,
        todayPct: inAxis(today) ? pct(today) : null,
        deadlinePct: inAxis(deadline) ? pct(deadline) : null,
        segments,
        monthTicks
    };
}
