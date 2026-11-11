import test from "node:test";
import assert from "node:assert/strict";

import { jeRadekViditelny } from "../../../PmTracker.Web/wwwroot/js/modules/dashboard/nesPanel.js";

test("prazdny_filtr_ukaze_vse", () => {
    assert.equal(jeRadekViditelny("NES", ""), true);
    assert.equal(jeRadekViditelny("PMP", ""), true);
    assert.equal(jeRadekViditelny("PNF", null), true);
    assert.equal(jeRadekViditelny("", undefined), true);
});

test("filtr_nes_skryje_pmp_a_pnf", () => {
    assert.equal(jeRadekViditelny("NES", "NES"), true);
    assert.equal(jeRadekViditelny("PMP", "NES"), false);
    assert.equal(jeRadekViditelny("PNF", "NES"), false);
});

test("typ_se_porovnava_bez_mezer_a_velikosti_pismen", () => {
    // HOT_ZAZNAMY.typ_zaznamu je CHAR(5) — „PMP  “ musí sedět na filtr „PMP“.
    assert.equal(jeRadekViditelny("PMP  ", "pmp"), true);
    assert.equal(jeRadekViditelny(" nes", "NES"), true);
});
