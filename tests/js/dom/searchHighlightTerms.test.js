import { test } from "node:test";
import assert from "node:assert/strict";

import { highlightTerms } from "../../../PmTracker.Web/wwwroot/js/modules/searchHighlight.js";

// 2026-10-08: „stav migrace a dat“ podsvítilo na kartě každé písmeno „a“. Krátká slova vedle
// delších se nehledají (SearchQueryText.MinTermLength = 3), takže se ani nepodsvítí.

test("zahodí slova kratší než 3 znaky, když dotaz má delší slovo", () => {
    assert.deepEqual(highlightTerms("stav migrace a dat"), ["migrace", "stav", "dat"]);
    assert.deepEqual(highlightTerms("revize v systému"), ["systému", "revize"]);
});

test("delší slova první, ať „záloha“ nepřebije „zálohování“", () => {
    assert.deepEqual(highlightTerms("záloha zálohování"), ["zálohování", "záloha"]);
});

test("dotaz jen z krátkých slov se podsvítí celý, stejně jako se hledá („50 %“)", () => {
    assert.deepEqual(highlightTerms("50 %"), ["50", "%"]);
});

test("prázdný dotaz → nic k podsvícení", () => {
    assert.deepEqual(highlightTerms(""), []);
    assert.deepEqual(highlightTerms(null), []);
});

test("opakované slovo jen jednou", () => {
    assert.deepEqual(highlightTerms("dat dat"), ["dat"]);
});
