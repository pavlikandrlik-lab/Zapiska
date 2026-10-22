/**
 * Spec 2026-10-22 R13: trvání kroku v editoru harmonogramu (datum-model). Čisté funkce bez DOM,
 * volá je duration-calendar-binding.js při změně data i po smazání (✕).
 * Zrcadlo serveru: ScheduleDurationText (text) a ScheduleDateCalculator (začátek kroku = konec
 * předchozího kroku s plánem, jinak datum založení záznamu).
 */

export const NO_PLAN_TEXT = "–";

/** Začátek kroku: konec nejbližšího předchozího kroku s plánem, jinak datum založení. */
export function planStart(plansByStep, stepIndex, recordStartIso) {
    for (let index = stepIndex - 1; index >= 1; index -= 1) {
        const iso = plansByStep.get(index);
        if (iso) {
            return { iso, fromPreviousStep: true };
        }
    }
    return { iso: recordStartIso, fromPreviousStep: false };
}

/** Počet kalendářních dnů mezi dvěma daty yyyy-MM-dd; null při neplatném vstupu. */
export function daysBetween(startIso, endIso) {
    const start = parseIso(startIso);
    const end = parseIso(endIso);
    if (!start || !end) {
        return null;
    }
    return Math.round((end.getTime() - start.getTime()) / 86400000);
}

/** „1 den“, „2–4 dny“, jinak „N dnů“; null = krok bez plánu („–“). */
export function formatDuration(days) {
    if (days === null || days === undefined) {
        return NO_PLAN_TEXT;
    }
    const unit = days === 1 ? "den" : days >= 2 && days <= 4 ? "dny" : "dnů";
    return `${days} ${unit}`;
}

function parseIso(value) {
    if (typeof value !== "string") {
        return null;
    }
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    if (!match) {
        return null;
    }
    const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
    return Number.isNaN(date.getTime()) ? null : date;
}
