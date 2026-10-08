import { test } from "node:test";
import assert from "node:assert/strict";

import { highlightTerms, buildHighlightPattern } from "../../../PmTracker.Web/wwwroot/js/modules/searchHighlight.js";

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

// 2026-10-08: text v uvozovkách se hledá i podsvítí jako celek (jako Google) — stejně jako
// SearchQueryText.SplitTerms na serveru.

test("fráze v uvozovkách je jeden celek, krátká slova v ní zůstávají", () => {
    assert.deepEqual(highlightTerms('"stav migrace a dat"'), ["stav migrace a dat"]);
    assert.deepEqual(highlightTerms("„stav migrace a dat“"), ["stav migrace a dat"]);
    assert.deepEqual(highlightTerms('"stav migrace" a dat'), ["stav migrace", "dat"]);
    assert.deepEqual(highlightTerms('dat "stav   migrace"'), ["stav migrace", "dat"]);
});

test("neuzavřená fráze běží do konce, prázdné uvozovky se ignorují", () => {
    assert.deepEqual(highlightTerms('"stav migrace'), ["stav migrace"]);
    assert.deepEqual(highlightTerms('"" migrace'), ["migrace"]);
});

test("krátké slovo v uvozovkách se hledá, krátké slovo vedle něj ne", () => {
    assert.deepEqual(highlightTerms('"IS" migrace a'), ["migrace", "IS"]);
});

test("fráze v textu stránky podsvítí i přes zalomení nebo pevnou mezeru", () => {
    const pattern = buildHighlightPattern(["stav migrace a dat"]);
    assert.equal("Stav migrace\u00a0a\ndat".match(pattern)?.[0], "Stav migrace\u00a0a\ndat");
    assert.equal("stav migrace dat".match(pattern), null, "bez „a“ to není ta fráze");
});
