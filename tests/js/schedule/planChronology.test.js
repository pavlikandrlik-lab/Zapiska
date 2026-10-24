import { test } from "node:test";
import assert from "node:assert/strict";
import { planBounds } from "../../../PmTracker.Web/wwwroot/js/modules/harmonogram/planChronology.js";

// Spec 2026-10-24 R10: meze = nejbližší dřívější / pozdější krok s plánem (pravidlo serveru).
test("meze podle sousedů, když všechny kroky plán mají", () => {
    assert.deepEqual(planBounds(["2026-03-01", "2026-03-10", "2026-03-20"]), [
        { min: null, max: "2026-03-10" },
        { min: "2026-03-01", max: "2026-03-20" },
        { min: "2026-03-10", max: null }
    ]);
});

test("krok bez plánu se přeskakuje", () => {
    const bounds = planBounds(["2026-03-01", "", "2026-04-01", null, "2026-05-01"]);
    assert.deepEqual(bounds[2], { min: "2026-03-01", max: "2026-05-01" });
    assert.deepEqual(bounds[0], { min: null, max: "2026-04-01" });
    assert.deepEqual(bounds[1], { min: "2026-03-01", max: "2026-04-01" }, "prázdný krok má meze taky");
});

test("bez plánu žádné meze", () => {
    assert.deepEqual(planBounds(["", "", ""]), [
        { min: null, max: null }, { min: null, max: null }, { min: null, max: null }
    ]);
});
