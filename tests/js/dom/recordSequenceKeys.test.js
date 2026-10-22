import { test } from "node:test";
import assert from "node:assert/strict";

import {
    isDialogOpen,
    isEditableTarget,
    resolveSequenceKey
} from "../../../PmTracker.Web/wwwroot/js/modules/recordSequenceKeys.js";

const free = { editable: false, dialogOpen: false };
const key = (name, extra = {}) => ({
    key: name, altKey: false, ctrlKey: false, metaKey: false, shiftKey: false,
    defaultPrevented: false, isComposing: false, ...extra
});

test("← vede na předchozí, → na další", () => {
    assert.equal(resolveSequenceKey(key("ArrowLeft"), free), "prev");
    assert.equal(resolveSequenceKey(key("ArrowRight"), free), "next");
});

test("jiné klávesy nic", () => {
    for (const name of ["ArrowUp", "ArrowDown", "Enter", "a"]) {
        assert.equal(resolveSequenceKey(key(name), free), null, name);
    }
});

test("s modifikátorem nic (Alt+← je v prohlížeči zpět)", () => {
    for (const modifier of ["altKey", "ctrlKey", "metaKey", "shiftKey"]) {
        assert.equal(resolveSequenceKey(key("ArrowRight", { [modifier]: true }), free), null, modifier);
    }
});

test("při psaní, v otevřeném okně a u zpracované události nic", () => {
    assert.equal(resolveSequenceKey(key("ArrowRight"), { editable: true, dialogOpen: false }), null);
    assert.equal(resolveSequenceKey(key("ArrowRight"), { editable: false, dialogOpen: true }), null);
    assert.equal(resolveSequenceKey(key("ArrowRight", { defaultPrevented: true }), free), null);
    assert.equal(resolveSequenceKey(key("ArrowRight", { isComposing: true }), free), null);
});

test("isEditableTarget: editor (contenteditable), pole přes closest; tělo stránky a null ne", () => {
    assert.equal(isEditableTarget({ isContentEditable: true, closest: () => null }), true);
    assert.equal(isEditableTarget({ isContentEditable: false, closest: (selector) => (selector.includes("textarea") ? {} : null) }), true);
    assert.equal(isEditableTarget({ isContentEditable: false, closest: () => null }), false);
    assert.equal(isEditableTarget(null), false);
});

test("isDialogOpen: neprázdný #modal-root nebo otevřený gov-dialog", () => {
    const doc = (modalRoot, dialogs = []) => ({
        getElementById: (id) => (id === "modal-root" ? modalRoot : null),
        querySelectorAll: (selector) => (selector === "gov-dialog" ? dialogs : [])
    });
    const root = (hidden, children) => ({ getAttribute: () => (hidden ? "true" : "false"), childElementCount: children });
    const dialog = (open, attr) => ({ open, getAttribute: () => attr });

    assert.equal(isDialogOpen(doc(root(false, 1))), true);
    assert.equal(isDialogOpen(doc(root(true, 1))), false);
    assert.equal(isDialogOpen(doc(root(false, 0))), false);
    assert.equal(isDialogOpen(doc(null, [dialog(true, null)])), true);
    assert.equal(isDialogOpen(doc(null, [dialog(false, "true")])), true);
    assert.equal(isDialogOpen(doc(null, [dialog(false, null)])), false);
});
