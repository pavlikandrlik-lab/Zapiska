/**
 * filters/printFilter.js — print filter snapshot + query params helpers.
 *
 * Fáze 3B Task 4: extrahováno z filters.js (1105 LOC → submodul ~65 LOC).
 *
 * Exports:
 *   buildProjectPrintFilterSnapshot, buildProjectPrintFilterQueryParams
 */

import { buildProjectFilterStateFromInputs } from "./projectFilter.js";

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const projectPrintRelevantRecordStateKeys = [
    "subsystem",
    "kategorie",
    "stav",
    "typ",
    "vlastnik",
    "aktivni",
    "mine",
    "jednaniVyjadreniStav"
];

// ---------------------------------------------------------------------------
// Public exports
// ---------------------------------------------------------------------------

export function buildProjectPrintFilterSnapshot() {
    const state = buildProjectFilterStateFromInputs("records");
    const snapshot = {
        subsystem: typeof state.subsystem === "string" ? state.subsystem.trim() : "",
        kategorie: typeof state.kategorie === "string" ? state.kategorie.trim() : "",
        stav: typeof state.stav === "string" ? state.stav.trim() : "",
        typ: typeof state.typ === "string" ? state.typ.trim() : "",
        vlastnik: typeof state.vlastnik === "string" ? state.vlastnik.trim() : "",
        aktivni: Boolean(state.aktivni),
        mine: Boolean(state.mine),
        jednaniVyjadreniStav: typeof state.jednaniVyjadreniStav === "string" ? state.jednaniVyjadreniStav.trim() : ""
    };

    return {
        ...snapshot,
        hasRelevantFilters: projectPrintRelevantRecordStateKeys.some((key) => Boolean(snapshot[key]))
    };
}

export function buildProjectPrintFilterQueryParams(useCurrentFilters) {
    const snapshot = buildProjectPrintFilterSnapshot();
    const params = new URLSearchParams();

    if (!useCurrentFilters) {
        params.set("useCurrentFilters", "false");
        return { snapshot, params };
    }

    params.set("useCurrentFilters", "true");

    if (snapshot.subsystem) {
        params.set("subsystem", snapshot.subsystem);
    }

    if (snapshot.kategorie) {
        params.set("kategorie", snapshot.kategorie);
    }

    if (snapshot.stav) {
        params.set("stav", snapshot.stav);
    }

    if (snapshot.typ) {
        params.set("typ", snapshot.typ);
    }

    if (snapshot.vlastnik) {
        params.set("vlastnik", snapshot.vlastnik);
    }

    if (snapshot.aktivni) {
        params.set("aktivni", "true");
    }

    if (snapshot.mine) {
        params.set("mine", "true");
    }

    if (snapshot.jednaniVyjadreniStav) {
        params.set("jednaniVyjadreniStav", snapshot.jednaniVyjadreniStav);
    }

    return { snapshot, params };
}
