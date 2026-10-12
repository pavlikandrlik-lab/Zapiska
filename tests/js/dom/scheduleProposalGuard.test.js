import { test } from "node:test";
import assert from "node:assert/strict";
import { resolveScheduleProposalCheck, isPlainLeftClick } from "../../../PmTracker.Web/wwwroot/js/modules/navrhy/scheduleProposalGuard.js";

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
