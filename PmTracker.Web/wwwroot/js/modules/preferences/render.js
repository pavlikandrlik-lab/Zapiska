/**
 * preferences/render.js — vykreslení a správa seznamu osobních předvoleb (Profil).
 * Server vykreslí prázdný [data-preferences-list]; obsah plní JS z registru.
 */

import { listAllPreferenceItems, preferenceRegistry } from "./registry.js";

let boundContainer = null;

function findItem(descriptorId, itemKey) {
    const descriptor = preferenceRegistry.find((d) => d.id === descriptorId);
    if (!descriptor) {
        return null;
    }
    return descriptor.list().find((item) => item.itemKey === itemKey) || null;
}

function setStatus(text) {
    const status = document.querySelector("[data-preferences-status]");
    if (status instanceof HTMLElement) {
        status.textContent = text || "";
    }
}

export function renderPersonalPreferences() {
    const list = document.querySelector("[data-preferences-list]");
    const empty = document.querySelector("[data-preferences-empty]");
    if (!(list instanceof HTMLElement)) {
        return;
    }

    const items = listAllPreferenceItems();
    list.textContent = "";

    if (empty instanceof HTMLElement) {
        empty.hidden = items.length > 0;
    }

    items.forEach((item) => {
        const row = document.createElement("li");
        row.className = "preference-row";

        const label = document.createElement("span");
        label.className = "preference-row-label";
        label.textContent = item.valueText ? `${item.label}: ${item.valueText}` : item.label;

        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "preference-row-remove";
        remove.setAttribute("data-preference-remove", "");
        remove.setAttribute("data-preference-descriptor", item.descriptorId);
        remove.setAttribute("data-preference-item", item.itemKey);
        remove.setAttribute("aria-label", `Smazat předvolbu: ${item.label}`);
        remove.innerHTML = '<gov-icon size="s" name="trash" type="components" aria-hidden="true"></gov-icon>';

        row.append(label, remove);
        list.appendChild(row);
    });
}

export function initPersonalPreferences() {
    const list = document.querySelector("[data-preferences-list]");
    if (!(list instanceof HTMLElement)) {
        return;
    }

    renderPersonalPreferences();

    if (boundContainer === list) {
        return; // listener už navěšen (idempotence)
    }
    boundContainer = list;

    list.addEventListener("click", (event) => {
        const target = event.target instanceof Element
            ? event.target.closest("[data-preference-remove]")
            : null;
        if (!(target instanceof HTMLElement)) {
            return;
        }
        event.preventDefault();
        const descriptorId = target.getAttribute("data-preference-descriptor") || "";
        const itemKey = target.getAttribute("data-preference-item") || "";
        const item = findItem(descriptorId, itemKey);
        if (item) {
            item.remove();
            renderPersonalPreferences();
            setStatus("Předvolba byla odstraněna.");
        }
    });
}
