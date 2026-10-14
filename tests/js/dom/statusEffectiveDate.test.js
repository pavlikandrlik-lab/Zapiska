import { test } from "node:test";
import assert from "node:assert/strict";

import { syncStatusEffectiveDateRow } from "../../../PmTracker.Web/wwwroot/js/modules/recordEditor/statusEffectiveDate.js";

// Duck-typed řádek: jen hidden a dataset, víc funkce nečte.
function fakeRow(originalStav) {
    return { hidden: true, dataset: { originalStav } };
}

test("změněný stav volbu „Změna stavu platí k“ ukáže", () => {
    const row = fakeRow("Rozpracováno");
    syncStatusEffectiveDateRow(row, "Hotovo");
    assert.equal(row.hidden, false);
});

test("návrat na uložený stav volbu znovu skryje", () => {
    const row = fakeRow("Rozpracováno");
    syncStatusEffectiveDateRow(row, "Hotovo");
    syncStatusEffectiveDateRow(row, "Rozpracováno");
    assert.equal(row.hidden, true);
});

test("záznam bez uloženého stavu: jakýkoli vybraný stav je změna", () => {
    const row = fakeRow("");
    syncStatusEffectiveDateRow(row, "Rozpracováno");
    assert.equal(row.hidden, false);
});
