import { test } from "node:test";
import assert from "node:assert/strict";

import { buildProjectFilterStorageKey } from "../../../PmTracker.Web/wwwroot/js/modules/filters/projectFilter.js";

test("detail projektu: klíč podle projektu pro stav i výchozí hodnoty", () => {
    assert.equal(buildProjectFilterStorageKey({ projectId: "12" }, "state"), "pmtracker.projectFilters.v1.project.12.state");
    assert.equal(buildProjectFilterStorageKey({ projectId: "12" }, "defaults"), "pmtracker.projectFilters.v1.project.12.defaults");
});

test("detail jednání: vlastní klíč stavu, oddělený od projektu", () => {
    assert.equal(
        buildProjectFilterStorageKey({ projectId: "12", projectFilterStorageId: "jednani-7" }, "state"),
        "pmtracker.meetingFilters.v1.jednani-7.state");
});

test("detail jednání: výchozí hodnoty se nečtou ani neukládají", () => {
    assert.equal(buildProjectFilterStorageKey({ projectId: "12", projectFilterStorageId: "jednani-7" }, "defaults"), "");
});

test("bez projektu i kontextu: žádný klíč", () => {
    assert.equal(buildProjectFilterStorageKey({}, "state"), "");
    assert.equal(buildProjectFilterStorageKey(undefined, "state"), "");
});
