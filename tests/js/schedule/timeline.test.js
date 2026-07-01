import { test } from "node:test";
import assert from "node:assert/strict";
import { buildTicksFromServer } from "../../../PmTracker.Web/wwwroot/js/modules/schedule/timeline.js";

test("buildTicksFromServer parsuje data-schedule-ticks", () => {
    const ticks = buildTicksFromServer('[{"left":0,"label":"01/2026"},{"left":53.4483,"label":"02/2026"}]');
    assert.equal(ticks.length, 2);
    assert.equal(ticks[0].left, 0);
    assert.equal(ticks[1].label, "02/2026");
});

test("buildTicksFromServer vrací [] pro prázdné / nevalidní", () => {
    assert.deepEqual(buildTicksFromServer(""), []);
    assert.deepEqual(buildTicksFromServer(null), []);
    assert.deepEqual(buildTicksFromServer("{not json"), []);
    assert.deepEqual(buildTicksFromServer('{"left":1}'), []); // ne pole
});

test("buildTicksFromServer odfiltruje položky bez číselného left", () => {
    const ticks = buildTicksFromServer('[{"left":0,"label":"a"},{"label":"b"},{"left":"x","label":"c"}]');
    assert.equal(ticks.length, 1);
    assert.equal(ticks[0].label, "a");
});
