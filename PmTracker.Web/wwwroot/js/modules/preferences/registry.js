/**
 * preferences/registry.js — registr osobních předvoleb (klientské, localStorage).
 * Každý deskriptor umí vyjmenovat své uložené položky (0..n) a každou samostatně smazat.
 * Rozšíření = přidání deskriptoru (Sub-projekt 2 sem přidá zámek rozbaleného menu).
 *
 * PreferenceItem = { descriptorId, itemKey, label, valueText?, remove() }
 */

import {
    getStoredPrintFormat,
    clearStoredPrintFormat,
    getPrintFormatLabel
} from "../ui/print.js";
import {
    listSavedProjectFilterPreferences,
    removeProjectFilterPreference
} from "../filters/projectFilter.js";
import { readCookie, deleteCookie } from "./cookie.js";

const menuLockCookie = "pmtracker.projectMenu.locked";

const printFormatDescriptor = {
    id: "printFormat",
    list() {
        const format = getStoredPrintFormat();
        if (!format) {
            return [];
        }
        return [{
            descriptorId: "printFormat",
            itemKey: "printFormat",
            label: "Preferovaný formát tisku",
            valueText: getPrintFormatLabel(format),
            remove: () => clearStoredPrintFormat()
        }];
    }
};

const projectFiltersDescriptor = {
    id: "projectFilters",
    list() {
        return listSavedProjectFilterPreferences().map((item) => ({
            descriptorId: "projectFilters",
            itemKey: item.projectId,
            label: item.label,
            valueText: null,
            remove: () => removeProjectFilterPreference(item.projectId)
        }));
    }
};

const menuLockDescriptor = {
    id: "menuLock",
    list() {
        if (readCookie(menuLockCookie) !== "1") {
            return [];
        }
        return [{
            descriptorId: "menuLock",
            itemKey: "menuLock",
            label: "Trvale rozbalené projektové menu",
            valueText: null,
            remove: () => deleteCookie(menuLockCookie)
        }];
    }
};

export const preferenceRegistry = [printFormatDescriptor, projectFiltersDescriptor, menuLockDescriptor];

/** Vrátí všechny aktuálně uložené položky napříč deskriptory (ploché pole). */
export function listAllPreferenceItems() {
    return preferenceRegistry.flatMap((descriptor) => descriptor.list());
}
