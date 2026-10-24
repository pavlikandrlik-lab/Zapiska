import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
    formatPlanCompletion,
    formatScheduleState,
    formatTotalDuration,
    summarizeSchedule
} from "../../../PmTracker.Web/wwwroot/js/modules/schedule/scheduleStatus.js";

// Spec 2026-10-24 R11: tytéž případy čte ScheduleStatusGoldenVectorTests (xUnit) — parita se serverem.
const fixture = JSON.parse(readFileSync(fileURLToPath(new URL("./__fixtures__/status-cases.json", import.meta.url))));

/** Editor posílá vždy všech 10 kroků (řádky tabulky); případy uvádějí jen kroky s hodnotou. */
function expandSteps(steps) {
    const byPoradi = new Map(steps.map((step) => [step.poradi, step]));
    return fixture.kroky.map((krok) => ({
        poradi: krok.poradi,
        nazev: krok.nazev,
        planIso: byPoradi.get(krok.poradi)?.plan ?? null,
        skutecnostIso: byPoradi.get(krok.poradi)?.skutecnost ?? null
    }));
}

for (const c of fixture.cases) {
    test(`parita stavu: ${c.name}`, () => {
        const souhrn = summarizeSchedule({
            startIso: c.input.start,
            terminIso: c.input.termin,
            todayIso: c.input.today,
            steps: expandSteps(c.input.steps)
        });
        const e = c.expected;
        assert.equal(souhrn.vztah, e.vztah, "vztah");
        assert.equal(souhrn.aktualniKrokPoradi, e.aktualniKrokPoradi, "aktuální krok");
        assert.equal(souhrn.prekroceniDni, e.prekroceniDni, "překročení");
        assert.equal(souhrn.planoveDokonceniIso, e.planoveDokonceni, "plánové dokončení");
        assert.equal(souhrn.celkoveTrvaniDni, e.celkoveTrvaniDni, "trvání");
        assert.equal(souhrn.stihame, e.stihame, "štítek");
        assert.equal(formatScheduleState(souhrn), e.stavText, "text stavu");
        assert.equal(formatPlanCompletion(souhrn.planoveDokonceniIso), e.planDokonceniText, "plán dokončení");
        assert.equal(formatTotalDuration(souhrn.planoveDokonceniIso, souhrn.celkoveTrvaniDni), e.trvaniText, "trvání text");
    });
}
