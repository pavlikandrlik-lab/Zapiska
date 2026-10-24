/**
 * schedule/scheduleStatus.js — JS dvojče serverového ScheduleDateCalculator.Summarize
 * a ScheduleStatusText (spec 2026-10-24). Editor harmonogramu (block.js) jím živě přepočítává
 * Stav, Plán dokončení a Trvání. Parita s C# je zamčená sdílenými případy
 * tests/js/schedule/__fixtures__/status-cases.json (node:test + xUnit nad týmiž daty).
 */

import { diffCalendarDays, formatDisplayDate, formatIsoDate, parseIsoDate } from "../utils.js";
import { formatDuration, NO_PLAN_TEXT } from "../harmonogram/planDuration.js";

/** Vůči čemu se stav počítá (C# HarmonogramStavVztah). */
export const VZTAH = Object.freeze({
    AKTUALNI_KROK: "AktualniKrok",
    CELKOVY_TERMIN: "CelkovyTermin",
    DOKONCENO: "Dokonceno"
});

export const PLAN_NENI_ZADAN = "není zadán";

/**
 * Souhrn harmonogramu z datumů kroků (mirror ScheduleDateCalculator.Compute + Summarize).
 * @param {{startIso: string, terminIso: string, todayIso: string,
 *          steps: Array<{poradi: number, nazev: string, planIso: string|null, skutecnostIso: string|null}>}} input
 */
export function summarizeSchedule({ startIso, terminIso, todayIso, steps }) {
    const start = parseIsoDate(startIso);
    const termin = parseIsoDate(terminIso);
    const today = parseIsoDate(todayIso);
    const ordered = [...steps].sort((left, right) => left.poradi - right.poradi);

    // Konec plánu kroku = jeho plán; bez plánu (nebo plán před koncem předchozího) = konec předchozího.
    let planCursor = start;
    const planEnds = ordered.map((step) => {
        const plan = parseIsoDate(step.planIso);
        if (plan && diffCalendarDays(plan, planCursor) > 0) {
            planCursor = plan;
        }
        return planCursor;
    });

    // Aktuální krok = první bez skutečnosti po posledním kroku se skutečností.
    let posledniVyplneny = -1;
    ordered.forEach((step, index) => {
        if (parseIsoDate(step.skutecnostIso)) {
            posledniVyplneny = index;
        }
    });
    const aktualniIndex = ordered.findIndex((step, index) => index > posledniVyplneny && !parseIsoDate(step.skutecnostIso));

    // R6: plánové dokončení jen tehdy, když aspoň jeden krok má vlastní plán.
    const maPlan = ordered.some((step) => parseIsoDate(step.planIso));
    const planoveDokonceni = maPlan && planEnds.length > 0 ? planEnds[planEnds.length - 1] : null;
    const zaklad = {
        planoveDokonceniIso: planoveDokonceni ? formatIsoDate(planoveDokonceni) : null,
        celkoveTrvaniDni: planoveDokonceni ? Math.max(0, diffCalendarDays(planoveDokonceni, start)) : 0
    };

    if (aktualniIndex < 0) {
        return {
            ...zaklad,
            vztah: VZTAH.DOKONCENO,
            aktualniKrokPoradi: null,
            aktualniKrokNazev: null,
            prekroceniDni: 0,
            dokonceno: true,
            stihame: true
        };
    }

    // R2: bez vlastního plánu aktuálního kroku se skluz počítá k celkovému termínu.
    const aktualni = ordered[aktualniIndex];
    const vlastniPlan = Boolean(parseIsoDate(aktualni.planIso));
    const prekroceniDni = vlastniPlan
        ? diffCalendarDays(today, planEnds[aktualniIndex])
        : diffCalendarDays(today, termin);
    return {
        ...zaklad,
        vztah: vlastniPlan ? VZTAH.AKTUALNI_KROK : VZTAH.CELKOVY_TERMIN,
        aktualniKrokPoradi: aktualni.poradi,
        aktualniKrokNazev: aktualni.nazev,
        prekroceniDni,
        dokonceno: false,
        stihame: prekroceniDni <= 0
    };
}

/** R4: „1 den“, „2–4 dny“, jinak „N dní“. */
export function formatDni(pocet) {
    const unit = pocet === 1 ? "den" : pocet >= 2 && pocet <= 4 ? "dny" : "dní";
    return `${pocet} ${unit}`;
}

function formatZbyva(pocet) {
    return pocet >= 2 && pocet <= 4 ? `zbývají ${formatDni(pocet)}` : `zbývá ${formatDni(pocet)}`;
}

/** R3: sjednocený stav (mirror ScheduleStatusText.State). */
export function formatScheduleState({ vztah, aktualniKrokNazev, prekroceniDni }) {
    if (vztah === VZTAH.DOKONCENO) {
        return "Dokončeno";
    }
    const krok = `Aktuální krok: ${aktualniKrokNazev}`;
    if (vztah === VZTAH.CELKOVY_TERMIN) {
        const termin = prekroceniDni > 0 ? `celkový termín: skluz +${formatDni(prekroceniDni)}`
            : prekroceniDni < 0 ? `celkový termín: ${formatZbyva(-prekroceniDni)}`
            : "celkový termín je dnes";
        return `${krok} · plán není zadán · ${termin}`;
    }
    return prekroceniDni > 0 ? `${krok} · skluz +${formatDni(prekroceniDni)}`
        : prekroceniDni < 0 ? `${krok} · v předstihu ${formatDni(-prekroceniDni)}`
        : `${krok} · dle plánu`;
}

/** R6: „Plán dokončení“ — datum, nebo „není zadán“ (mirror ScheduleStatusText.PlanCompletion). */
export function formatPlanCompletion(planoveDokonceniIso) {
    const date = parseIsoDate(planoveDokonceniIso);
    return date ? formatDisplayDate(date) : PLAN_NENI_ZADAN;
}

/** R6: celkové trvání — „–“ bez plánu (mirror ScheduleStatusText.TotalDuration). */
export function formatTotalDuration(planoveDokonceniIso, celkoveTrvaniDni) {
    return planoveDokonceniIso ? formatDuration(celkoveTrvaniDni) : NO_PLAN_TEXT;
}
