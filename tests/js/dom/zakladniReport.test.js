import test from "node:test";
import assert from "node:assert/strict";

import { obdobiUrl } from "../../../PmTracker.Web/wwwroot/js/modules/dashboard/zakladniReport.js";

test("rok_bez_kvartalu", () => {
    assert.equal(obdobiUrl("/projekty/1/dashboard/statistics-panel", "2025", ""), "/projekty/1/dashboard/statistics-panel?rok=2025");
});

test("rok_s_kvartalem", () => {
    assert.equal(obdobiUrl("/projekty/1/dashboard/statistics-panel", 2026, "3"), "/projekty/1/dashboard/statistics-panel?rok=2026&kvartal=3");
});
