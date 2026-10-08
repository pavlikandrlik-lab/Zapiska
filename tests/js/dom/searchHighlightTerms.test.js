import { test } from "node:test";
import assert from "node:assert/strict";

import {
    highlightTerms,
    buildHighlightPattern,
    findHighlightRanges,
    groupHighlightItems
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

// groupHighlightItems (review M1 + M2): textové a oddělovací položky se spojují jen
// uvnitř jednoho .richtext-render containeru (karta popisu/vyjádření) — tam fráze smí jít
// přes formátování i přes <br>/odstavec/položku seznamu. Mimo něj (container null) je
// každý textový uzel vlastní skupina, přesně jako před touto větví — popisek a hodnota v
// plain <div> (_ZaznamDetailPartial.cshtml) nesmí vytvořit falešnou frázi. Duck-typed
// položky: { node: { data }, container } / { separator: true, container }, žádné DOM.

function textItem(data, container) {
    return { node: { data }, container };
}

function breakItem(container) {
    return { separator: true, container };
}

// Replikuje to, co s jednou skupinou dělá highlightSearchTerms: oddělovač dodá do
// spojeného textu "\n" (matchuje \s+, ale nejde ho obalit <mark>, nemá uzel), výsledné
// úseky se pak omezí jen na textové položky.
function wrappableRanges(group, pattern) {
    const texts = group.map((entry) => (entry.separator ? "\n" : entry.node.data));
    return findHighlightRanges(texts, pattern).filter((range) => !group[range.index].separator);
}

test("fráze přes <br> v jednom containeru — podsvítí jen textové položky, ne oddělovač", () => {
    const container = { name: "richtext" };
    const items = [textItem("pes a", container), breakItem(container), textItem("kočka", container)];

    const groups = groupHighlightItems(items);
    assert.deepEqual(groups, [items]);

    const ranges = wrappableRanges(groups[0], buildHighlightPattern(["pes a kočka"]));
    assert.deepEqual(ranges, [{ index: 0, start: 0, end: 5 }, { index: 2, start: 0, end: 5 }]);
});

test("fráze přes dva odstavce v jednom containeru se spojí přes oddělovač", () => {
    const container = { name: "richtext" };
    const items = [textItem("Rozhodnuto o", container), breakItem(container), textItem("řešení zálohy", container)];

    const groups = groupHighlightItems(items);
    assert.deepEqual(groups, [items]);

    const ranges = wrappableRanges(groups[0], buildHighlightPattern(["o řešení zálohy"]));
    assert.deepEqual(ranges, [{ index: 0, start: 11, end: 12 }, { index: 2, start: 0, end: 13 }]);
});

test("slova v různých odstavcích se nespojí do cizího slova přes oddělovač", () => {
    const container = { name: "richtext" };
    const items = [textItem("abc", container), breakItem(container), textItem("def", container)];

    const groups = groupHighlightItems(items);
    const ranges = wrappableRanges(groups[0], buildHighlightPattern(["cde"]));
    assert.deepEqual(ranges, []);
});

test("mimo .richtext-render (container null) je každý textový uzel vlastní skupina", () => {
    const items = [
        textItem("Jan Novák", null),
        textItem(" ", null),
        textItem("Pavel Dvořák", null)
    ];

    const groups = groupHighlightItems(items);
    assert.deepEqual(groups, [[items[0]], [items[1]], [items[2]]]);

    const pattern = buildHighlightPattern(["Novák Pavel"]);
    const ranges = groups.flatMap((group) => wrappableRanges(group, pattern));
    assert.deepEqual(ranges, [], "popisek a hodnota v plain <div> nesmí vytvořit falešnou frázi");
});

test("oddělovač mimo .richtext-render (container null) se zahodí", () => {
    const items = [textItem("a", null), breakItem(null), textItem("b", null)];

    assert.deepEqual(groupHighlightItems(items), [[items[0]], [items[2]]]);
});

test("dva různé containery — dvě skupiny", () => {
    const containerA = { name: "richtext-a" };
    const containerB = { name: "richtext-b" };
    const items = [textItem("první", containerA), textItem("druhý", containerB)];

    assert.deepEqual(groupHighlightItems(items), [[items[0]], [items[1]]]);
});
