import { test } from "node:test";
import assert from "node:assert/strict";

import { sestavTema } from "../../../PmTracker.Web/wwwroot/js/modules/dashboard/echartsTheme.js";

const TOKENY = {
    "--app-chart-1": "rgb(55, 113, 184)",
    "--app-chart-2": "rgb(175, 122, 0)",
    "--app-chart-neutral": "rgb(136, 136, 136)",
    "--app-chart-neutral-strong": "rgb(79, 79, 79)",
    "--background-block-primary": "rgb(255, 255, 255)",
    "--text-primary": "rgb(38, 38, 38)",
    "--text-secondary": "rgb(79, 79, 79)",
    "--border-subtle": "rgb(209, 209, 209)",
    "--border-subtlest": "rgb(231, 231, 231)",
    "--background-status-success": "rgb(46, 125, 50)",
    "--background-status-error": "rgb(215, 12, 15)"
};
const ctecka = (name) => TOKENY[name] ?? "";

test("sestavTema_bere_barvy_z_app_chart_tokenu", () => {
    const tema = sestavTema(ctecka);

    assert.deepEqual(tema.color, ["rgb(55, 113, 184)", "rgb(175, 122, 0)", "rgb(136, 136, 136)", "rgb(79, 79, 79)"]);
    assert.equal(tema.surface, "rgb(255, 255, 255)");
    assert.equal(tema.textStyle.color, "rgb(38, 38, 38)");
    assert.equal(tema.axis.label, "rgb(79, 79, 79)");
    assert.equal(tema.axis.split, "rgb(231, 231, 231)");
    assert.equal(tema.tooltip.backgroundColor, "rgb(255, 255, 255)");
});

test("sestavTema_bez_hodnoty_tokenu_nepouzije_prazdnou_barvu", () => {
    const tema = sestavTema(() => "");

    for (const barva of [...tema.color, tema.surface, tema.textStyle.color, tema.axis.label, tema.axis.line,
        tema.axis.split, tema.tooltip.backgroundColor, tema.tooltip.borderColor]) {
        assert.match(barva, /^#[0-9a-f]{6}$/i, "chybějící token spadne na záložní barvu, ne na prázdný řetězec");
    }
});

test("barva tokenu série: známý token se vyřeší, neznámý vrací null", () => {
    const tema = sestavTema(ctecka);

    assert.equal(tema.barva("--background-status-error"), "rgb(215, 12, 15)");
    assert.equal(tema.barva("--neexistuje"), null);
    assert.equal(tema.barva(undefined), null);
    assert.equal(tema.barva("javascript:alert(1)"), null, "bere se jen název CSS proměnné");
});

test("čtečka, která vrací hodnotu s mezerami, se ořízne", () => {
    const tema = sestavTema((name) => (name === "--app-chart-1" ? "  rgb(1, 2, 3) " : ""));
    assert.equal(tema.color[0], "rgb(1, 2, 3)");
});
