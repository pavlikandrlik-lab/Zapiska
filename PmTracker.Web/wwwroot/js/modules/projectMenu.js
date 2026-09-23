/**
 * projectMenu.js — projektové menu (Option 2, 2026-07-07).
 * „+" jednosměrně rozbalí neaktivní sekundární záložky inline (do konce page-view; reload/navigace
 * ho přirozeně sbalí, pokud není trvale zamčeno). Aktivní tab je vždy inline (renderuje server),
 * takže podtržení je vidět i sbalené — persistence rozbalení netřeba.
 * Zámeček = trvalé rozbalení (cookie čtená serverově, jako téma) + reload.
 */

import { readCookie, writeCookie, deleteCookie } from "./preferences/cookie.js";

const menuLockCookie = "pmtracker.projectMenu.locked";
const oneYearSeconds = 60 * 60 * 24 * 365;

function initExpandToggle() {
    const group = document.querySelector("[data-project-menu-group]");
    if (!(group instanceof HTMLElement)) {
        return;
    }
    const toggle = group.querySelector("[data-project-menu-toggle]");
    if (!(toggle instanceof HTMLElement)) {
        return;
    }

    toggle.addEventListener("click", (event) => {
        event.preventDefault();
        // Jednosměrné rozbalení: ne klik-rozbal-klik-shrň, ne outside-close. Sbalí ho až
        // reload/navigace (aktivní tab zůstane inline vždy) nebo trvalé odemčení zámku.
        group.classList.add("is-expanded");
    });
}

function initLockToggle() {
    const lock = document.querySelector("[data-project-menu-lock-toggle]");
    if (!(lock instanceof HTMLElement)) {
        return;
    }
    lock.addEventListener("click", (event) => {
        event.preventDefault();
        if (readCookie(menuLockCookie) === "1") {
            deleteCookie(menuLockCookie);
        } else {
            writeCookie(menuLockCookie, "1", oneYearSeconds);
        }
        window.location.reload();
    });
}

export function initProjectMenuOverflow() {
    initExpandToggle();
    initLockToggle();
}
