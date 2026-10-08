import { test } from "node:test";
import assert from "node:assert/strict";

import {
    highlightTerms,
    buildHighlightPattern,
    findHighlightRanges,
    groupTextNodesByBlock
} from "../../../PmTracker.Web/wwwroot/js/modules/searchHighlight.js";

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

// 2026-10-08: fráze přes tučné slovo — text je rozdělený mezi víc textových uzlů jednoho
// bloku, nalezení shody musí jít přes spoj mezi uzly.

test("fráze přes tučné slovo — úseky po textových uzlech", () => {
    const texts = ["Rozhodnuto o ", "řešení", " zálohy"];
    assert.deepEqual(
        findHighlightRanges(texts, buildHighlightPattern(["o řešení zálohy"])),
        [{ index: 0, start: 11, end: 13 }, { index: 1, start: 0, end: 6 }, { index: 2, start: 0, end: 7 }]);
});

test("slova v různých uzlech zůstanou samostatné shody", () => {
    assert.deepEqual(
        findHighlightRanges(["Rozhodnuto o ", "řešení", " zálohy"], buildHighlightPattern(["řešení", "zálohy"])),
        [{ index: 1, start: 0, end: 6 }, { index: 2, start: 1, end: 7 }]);
});

// groupTextNodesByBlock: rozhoduje, jestli textové uzly spadají do stejného blokového
// elementu (fráze se v něm smí spojit přes formátování), nebo ne (popisek a hodnota v
// různých blocích nesmí vytvořit falešnou frázi). Duck-typed fake uzlu: jen parentElement
// s closest(), jak to používá reálný text node (pattern jako tests/js/dom/formSubmitterAttr.test.js).

function fakeTextNode(data, closestResult) {
    return {
        data,
        parentElement: { closest: () => closestResult }
    };
}

test("uzly ve dvou různých blokách — dvě skupiny, fráze mezi nimi se nespojí", () => {
    const blockLabel = { name: "dt" };
    const blockValue = { name: "dd" };
    const nodeLabel = fakeTextNode("Vlastník", blockLabel);
    const nodeValue = fakeTextNode(" Pavel", blockValue);
    const root = { name: "root" };

    const groups = groupTextNodesByBlock([nodeLabel, nodeValue], root);
    assert.deepEqual(groups, [[nodeLabel], [nodeValue]]);

    const pattern = buildHighlightPattern(["Vlastník Pavel"]);
    const ranges = groups.flatMap((group) => findHighlightRanges(group.map((node) => node.data), pattern));
    assert.deepEqual(ranges, []);
});

test("uzly ve stejné bloce — jedna skupina v pořadí dokumentu", () => {
    const block = { name: "p" };
    const first = fakeTextNode("Rozhodnuto o ", block);
    const second = fakeTextNode("řešení", block);
    const third = fakeTextNode(" zálohy", block);
    const root = { name: "root" };

    assert.deepEqual(groupTextNodesByBlock([first, second, third], root), [[first, second, third]]);
});

test("closest() bez shody — uzly se seskupí pod root", () => {
    const root = { name: "root" };
    const first = fakeTextNode("a", null);
    const second = fakeTextNode("b", null);

    assert.deepEqual(groupTextNodesByBlock([first, second], root), [[first, second]]);
});
