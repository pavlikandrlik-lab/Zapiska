import { test } from "node:test";
import assert from "node:assert/strict";

import { resolveSouhrnCil } from "../../../PmTracker.Web/wwwroot/js/modules/dashboard/souhrnCil.js";

test("dlazdice_tickety_v_prodleni_vede_na_nes", () => {
    assert.deepEqual(resolveSouhrnCil({ souhrnTab: "nes", souhrnFilter: "v-prodleni" }), { tab: "nes", filter: "v-prodleni" });
});

test("dlazdice_ukoly_po_terminu_vede_na_zaznamy_s_filtrem_delayed", () => {
    assert.deepEqual(resolveSouhrnCil({ souhrnTab: "zaznamy", souhrnFilter: "delayed" }), { tab: "zaznamy", filter: "delayed" });
});

test("dlazdice bez filtru vede jen na záložku", () => {
    assert.deepEqual(resolveSouhrnCil({ souhrnTab: "nes", souhrnFilter: "" }), { tab: "nes", filter: "" });
});

test("neznámá záložka nebo chybějící data: žádný cíl", () => {
    assert.equal(resolveSouhrnCil({ souhrnTab: "jinam", souhrnFilter: "x" }), null);
    assert.equal(resolveSouhrnCil({}), null);
    assert.equal(resolveSouhrnCil(undefined), null);
});
