/**
 * Spec 2026-10-24 R10: meze kalendáře u plánu kroku (datum-model). Zrcadlo serverového
 * ScheduleChronologyValidator: plán kroku ≥ plán nejbližšího dřívějšího kroku s plánem;
 * shora ho omezuje plán nejbližšího pozdějšího kroku s plánem. Kroky bez plánu se přeskakují,
 * stejný den je povolený (date.js vypíná jen dny ostře mimo meze).
 */

/**
 * @param {Array<string|null>} plans plán kroků v pořadí (yyyy-MM-dd; "" nebo null = bez plánu)
 * @returns {Array<{min: string|null, max: string|null}>}
 */
export function planBounds(plans) {
    return plans.map((_, index) => ({
        min: lastFilled(plans.slice(0, index)),
        max: firstFilled(plans.slice(index + 1))
    }));
}

function firstFilled(values) {
    return values.find((value) => Boolean(value)) || null;
}

function lastFilled(values) {
    return firstFilled([...values].reverse());
}
