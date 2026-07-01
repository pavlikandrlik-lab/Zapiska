import { test } from "node:test";
import assert from "node:assert/strict";

import { resolveFormSubmitterAttr } from "../../../PmTracker.Web/wwwroot/js/modules/utils.js";

// Minimální duck-typed fake elementu: jen getAttribute, který čte z mapy.
function fakeEl(attrs = {}) {
    return {
        getAttribute: (name) => (name in attrs ? attrs[name] : null),
    };
}

test("vrací formaction přímo ze submitteru, když ho má (nativní button s atributem)", () => {
    const submitter = fakeEl({ formaction: "/Navrhy/ApproveProposal" });
    const result = resolveFormSubmitterAttr(submitter, "formaction", null);
    assert.equal(result, "/Navrhy/ApproveProposal");
});

test("gov-button case: inner button nemá formaction → vystoupá na gov-button host", () => {
    // přesně náš scénář: event.submitter = inner <button class=element> bez formaction,
    // host <gov-button> formaction nese (pass-through z PmButtonTagHelper).
    const innerButton = fakeEl({});
    const govButtonHost = fakeEl({ formaction: "/Navrhy/RejectProposal", formmethod: "post" });

    assert.equal(
        resolveFormSubmitterAttr(innerButton, "formaction", govButtonHost),
        "/Navrhy/RejectProposal");
    assert.equal(
        resolveFormSubmitterAttr(innerButton, "formmethod", govButtonHost),
        "post");
});

test("vlastní atribut submitteru má přednost před hostem", () => {
    const submitter = fakeEl({ formaction: "/own" });
    const host = fakeEl({ formaction: "/host" });
    assert.equal(resolveFormSubmitterAttr(submitter, "formaction", host), "/own");
});

test("žádný submitter → prázdný řetězec (fallback na form action zůstává na volajícím)", () => {
    assert.equal(resolveFormSubmitterAttr(null, "formaction", null), "");
});

test("ani submitter ani host atribut nemají → prázdný řetězec", () => {
    assert.equal(resolveFormSubmitterAttr(fakeEl({}), "formaction", fakeEl({})), "");
});
