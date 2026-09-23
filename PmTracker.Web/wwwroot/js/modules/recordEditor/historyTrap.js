/**
 * historyTrap.js — A8 (2026-07-08): aplikační dialog při šipce zpět na STRÁNKOVÉM editoru.
 *
 * Mechanismus: při loadu editoru pushState sentinel ({pmEditorTrap}) → šipka zpět popne
 * sentinel = popstate VE STEJNÉM dokumentu (žádný nativní browser dialog — unload handler
 * aplikace je záměrně prázdný). Dirty form → promptRecordEditorDiscard: „Pokračovat v úpravách" =
 * sentinel se obnoví, „Zahodit změny" = history.back() na skutečnou předchozí stránku.
 * Čistý form → projde rovnou (žádný dotaz).
 *
 * Limity (záměr): kryje 1 krok zpět (delší skok v historii trap obejde — best effort);
 * zavření tabu/okna nekryjeme (nativní dialog nechceme).
 */
import { isRecordEditorFormDirty, promptRecordEditorDiscard } from "./draft.js";

const trapState = { armed: false, prompting: false };

function findPageEditorForm() {
    const form = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
    if (!(form instanceof HTMLFormElement)) {
        return null;
    }
    // B3 (2026-07-09): rozhodovací stránka návrhu (guard="off") nemá koncept rozpracovanosti.
    if (form.dataset.recordEditorGuard === "off") {
        return null;
    }
    return form;
}

// Propuštění dál MIMO popstate dispatch: history.back() volané synchronně uvnitř
// popstate handleru Chromium (v některých verzích) tiše ignoruje/koalescuje.
function passThroughBack() {
    window.setTimeout(() => window.history.back(), 0);
}

async function handlePopState() {
    // Testovací marker — kolikrát trap zpracoval popstate (viz armed marker níže).
    const popCount = Number(document.documentElement.getAttribute("data-pm-editor-trap-pop") || "0") + 1;
    document.documentElement.setAttribute("data-pm-editor-trap-pop", String(popCount));
    const form = findPageEditorForm();
    if (!form || form.dataset.recordEditorNavigating === "true") {
        // Editor pryč / navigace už schválená — propustit dál.
        passThroughBack();
        return;
    }

    if (!isRecordEditorFormDirty(form)) {
        passThroughBack();
        return;
    }

    if (trapState.prompting) {
        // Reentrance (další back během otevřeného dialogu) — obnovit sentinel, dialog nechat být.
        window.history.pushState({ pmEditorTrap: true }, "", window.location.href);
        return;
    }

    trapState.prompting = true;
    try {
        // promptRecordEditorDiscard při „Zahodit změny" interně nastaví
        // recordEditorNavigating=true (prepareRecordEditorFormNavigation).
        const shouldLeave = await promptRecordEditorDiscard(form, null);
        if (shouldLeave) {
            window.history.back();
        } else {
            window.history.pushState({ pmEditorTrap: true }, "", window.location.href);
        }
    } finally {
        trapState.prompting = false;
    }
}

export function initRecordEditorHistoryTrap() {
    if (trapState.armed || !findPageEditorForm()) {
        return;
    }
    trapState.armed = true;
    window.history.pushState({ pmEditorTrap: true }, "", window.location.href);
    window.addEventListener("popstate", handlePopState);
    // Testovací/diagnostický marker: sentinel STATE později přepíše pm-tabs
    // (pmTabs.js replaceState(null) při hash sync) — mechanika trapu tím netrpí
    // (rozhoduje extra history ENTRY + popstate listener), ale history.state
    // NELZE použít jako signál „armed". E2E čeká na tento atribut.
    document.documentElement.setAttribute("data-pm-editor-trap-armed", "true");
}
