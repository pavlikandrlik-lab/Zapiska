import { test } from "node:test";
import assert from "node:assert/strict";
import { resolveScheduleProposalCheck, isPlainLeftClick, fetchScheduleProposalDecision } from "../../../PmTracker.Web/wwwroot/js/modules/navrhy/scheduleProposalGuard.js";

test("čekající návrh → dialog s textem ze serveru", () => {
    assert.deepEqual(
        resolveScheduleProposalCheck({ canCreate: false, title: "Návrh nelze založit", message: "Text", proposalUrl: "/Navrhy/ProposalDetail?projektId=1&proposalId=5" }),
        { action: "dialog", title: "Návrh nelze založit", message: "Text", proposalUrl: "/Navrhy/ProposalDetail?projektId=1&proposalId=5" });
});

test("bez odkazu na návrh → dialog bez odkazu", () => {
    assert.equal(resolveScheduleProposalCheck({ canCreate: false, title: "T", message: "M", proposalUrl: null }).proposalUrl, null);
});

test("lze založit nebo neznámá odpověď → pokračovat na editor (rozhodne server)", () => {
    assert.deepEqual(resolveScheduleProposalCheck({ canCreate: true }), { action: "navigate" });
    assert.deepEqual(resolveScheduleProposalCheck(null), { action: "navigate" });
    assert.deepEqual(resolveScheduleProposalCheck({}), { action: "navigate" });
});

test("klik s modifikátorem nebo jiným tlačítkem se nezachytává (nová karta)", () => {
    assert.equal(isPlainLeftClick({ button: 0 }), true);
    assert.equal(isPlainLeftClick({ button: 1 }), false);
    assert.equal(isPlainLeftClick({ button: 0, ctrlKey: true }), false);
    assert.equal(isPlainLeftClick({ button: 0, metaKey: true }), false);
    assert.equal(isPlainLeftClick({ button: 0, shiftKey: true }), false);
});

// Review fix M4 (2026-10-12): fetch bez timeoutu nechával tlačítko "mrtvé", když dotaz zůstal
// viset (např. dlouhotrvající serializable transakce na serveru). fetchScheduleProposalDecision
// musí po timeoutMs pokračovat na editor, i když fetchImpl signal nerespektuje.

test("fetch, který se nikdy nevyřeší, po timeoutu pokračuje na editor (server rozhodne)", async () => {
    const neverResolves = () => new Promise(() => {});
    const decision = await fetchScheduleProposalDecision("/Navrhy/ScheduleProposalAvailability", {
        fetchImpl: neverResolves,
        timeoutMs: 20
    });
    assert.deepEqual(decision, { action: "navigate" });
});

test("úspěšná odpověď s canCreate=false vrátí rozhodnutí dialog", async () => {
    const fetchImpl = async () => ({
        ok: true,
        json: async () => ({ canCreate: false, title: "T", message: "M", proposalUrl: null })
    });
    const decision = await fetchScheduleProposalDecision("/Navrhy/ScheduleProposalAvailability", {
        fetchImpl,
        timeoutMs: 1000
    });
    assert.deepEqual(decision, { action: "dialog", title: "T", message: "M", proposalUrl: null });
});

test("odpověď s ok=false pokračuje na editor (server rozhodne)", async () => {
    const fetchImpl = async () => ({ ok: false });
    const decision = await fetchScheduleProposalDecision("/Navrhy/ScheduleProposalAvailability", {
        fetchImpl,
        timeoutMs: 1000
    });
    assert.deepEqual(decision, { action: "navigate" });
});
