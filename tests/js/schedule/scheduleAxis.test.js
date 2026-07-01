import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { computeAxisLayout } from "../../../PmTracker.Web/wwwroot/js/modules/schedule/scheduleAxis.js";

const cases = JSON.parse(readFileSync(fileURLToPath(new URL("./__fixtures__/axis-cases.json", import.meta.url))));

const d = (s) => { const [y, m, day] = s.split("-").map(Number); return new Date(y, m - 1, day); };
const iso = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
const approx = (a, b) => Math.abs(a - b) < 0.01;

for (const c of cases) {
    test(`parity: ${c.name}`, () => {
        const layout = computeAxisLayout({
            start: d(c.input.start),
            deadline: d(c.input.deadline),
            today: d(c.input.today),
            steps: c.input.steps.map((s) => ({
                poradi: s.poradi,
                planStart: d(s.planStart),
                planEnd: d(s.planEnd),
                maSkutecnost: s.maSkutecnost,
                skutecnostStart: d(s.skutecnostStart),
                skutecnostEnd: d(s.skutecnostEnd)
            }))
        });

        assert.equal(iso(layout.axisStart), c.expected.axisStart, "axisStart");
        assert.equal(iso(layout.axisEnd), c.expected.axisEnd, "axisEnd");
        assert.equal(layout.totalDays, c.expected.totalDays, "totalDays");

        if (c.expected.todayPct === null) {
            assert.equal(layout.todayPct, null, "todayPct null");
        } else {
            assert.ok(approx(layout.todayPct, c.expected.todayPct), `todayPct ${layout.todayPct} != ${c.expected.todayPct}`);
        }
        assert.ok(approx(layout.deadlinePct, c.expected.deadlinePct), `deadlinePct ${layout.deadlinePct} != ${c.expected.deadlinePct}`);

        assert.equal(layout.monthTicks.length, c.expected.monthTicks.length, "monthTicks count");
        c.expected.monthTicks.forEach((t, i) => {
            assert.ok(approx(layout.monthTicks[i].left, t.left), `tick ${i} left ${layout.monthTicks[i].left} != ${t.left}`);
            assert.equal(layout.monthTicks[i].label, t.label, `tick ${i} label`);
        });

        c.expected.segments.forEach((es) => {
            const seg = layout.segments.find((s) => s.poradi === es.poradi);
            assert.ok(seg, `segment ${es.poradi} exists`);
            assert.ok(approx(seg.planLeft, es.planLeft), `seg ${es.poradi} planLeft`);
            assert.ok(approx(seg.planWidth, es.planWidth), `seg ${es.poradi} planWidth`);
            assert.equal(seg.hasActual, es.hasActual, `seg ${es.poradi} hasActual`);
            assert.ok(approx(seg.actualWidth, es.actualWidth), `seg ${es.poradi} actualWidth`);
        });
    });
}
