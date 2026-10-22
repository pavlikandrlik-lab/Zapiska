import { test } from "node:test";
import assert from "node:assert/strict";

import {
    daysBetween,
    formatDuration,
    NO_PLAN_TEXT,
    planStart
} from "../../../PmTracker.Web/wwwroot/js/modules/harmonogram/planDuration.js";

test("krok bez plánu má trvání „–“", () => {
    assert.equal(formatDuration(null), "–");
    assert.equal(NO_PLAN_TEXT, "–");
});

test("české tvary jako server: 1 den, 2–4 dny, jinak dnů", () => {
    assert.equal(formatDuration(0), "0 dnů");
    assert.equal(formatDuration(1), "1 den");
    assert.equal(formatDuration(3), "3 dny");
    assert.equal(formatDuration(5), "5 dnů");
});

test("začátek kroku = konec nejbližšího předchozího kroku s plánem", () => {
    const plans = new Map([[1, "2026-10-01"], [2, "2026-10-08"], [4, "2026-10-22"]]);
    assert.deepEqual(planStart(plans, 4, "2026-09-01"), { iso: "2026-10-08", fromPreviousStep: true });
    assert.deepEqual(planStart(plans, 3, "2026-09-01"), { iso: "2026-10-08", fromPreviousStep: true });
});

test("bez předchozího kroku s plánem začíná krok datem založení", () => {
    assert.deepEqual(planStart(new Map([[3, "2026-10-15"]]), 3, "2026-09-01"), { iso: "2026-09-01", fromPreviousStep: false });
    assert.deepEqual(planStart(new Map(), 1, "2026-09-01"), { iso: "2026-09-01", fromPreviousStep: false });
});

test("počet dnů mezi daty; záporný při obráceném pořadí; null při neplatném vstupu", () => {
    assert.equal(daysBetween("2026-10-08", "2026-10-22"), 14);
    assert.equal(daysBetween("2026-10-22", "2026-10-08"), -14);
    assert.equal(daysBetween("2026-03-28", "2026-03-30"), 2, "UTC — přechod na letní čas den neztratí");
    assert.equal(daysBetween("", "2026-10-08"), null);
});
