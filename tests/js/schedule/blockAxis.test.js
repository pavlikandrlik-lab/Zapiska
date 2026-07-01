import { test } from "node:test";
import assert from "node:assert/strict";
import { resolveToday } from "../../../PmTracker.Web/wwwroot/js/modules/schedule/block.js";

test("resolveToday čte data-schedule-today (lokální dnešek ze serveru)", () => {
    const el = { dataset: { scheduleToday: "2026-06-23" } };
    const t = resolveToday(el);
    assert.equal(t.getFullYear(), 2026);
    assert.equal(t.getMonth(), 5); // červen = index 5
    assert.equal(t.getDate(), 23);
});

test("resolveToday fallback na new Date() když atribut chybí", () => {
    const t = resolveToday({ dataset: {} });
    assert.ok(t instanceof Date, "fallback je platné Date");
});
