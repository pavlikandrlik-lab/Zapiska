/**
 * Spec 2026-09-17 §4.2 — klientská část zámku karty záznamu.
 *
 * Heartbeat NEMÁ vlastní časovač: id záznamu se vystaví na <html> a keep-alive
 * koordinátor (session.js), který stejně běží každých 5 minut, ho přibalí ke svému
 * requestu. Zde zbývá jen uvolnění zámku při odchodu ze stránky.
 *
 * Uvolnění jde přes sendBeacon — fetch se při pagehide nedoručí, prohlížeč stránku
 * zahodí dřív. Když beacon selže, nic se neztratí: zámek vyprší TTL a cizí zápis
 * stejně zachytí record guard (§5).
 */
const releaseEndpoint = "/Zaznamy/ReleaseEditLock";

function resolveRecordId() {
    const form = document.querySelector('form[data-record-editor-form="true"]');
    if (!(form instanceof HTMLFormElement)) {
        return 0;
    }

    if (form.dataset.isCreate === "true") {
        return 0;
    }

    const idInput = form.querySelector('input[name="Id"]');
    if (!(idInput instanceof HTMLInputElement)) {
        return 0;
    }

    const value = Number.parseInt(idInput.value, 10);
    return Number.isFinite(value) && value > 0 ? value : 0;
}

export function initRecordEditLock() {
    if (!(document.body instanceof HTMLElement) || document.body.dataset.recordEditLockReady === "true") {
        return;
    }

    const zaznamId = resolveRecordId();
    if (!zaznamId) {
        // Stránka bez editoru (nebo zakládání nového záznamu) — zámek se neřeší.
        delete document.documentElement.dataset.recordEditLockId;
        return;
    }

    document.body.dataset.recordEditLockReady = "true";
    // Odsud si ho vyzvedne session.js a přibalí na keep-alive jako heartbeat.
    document.documentElement.dataset.recordEditLockId = String(zaznamId);

    window.addEventListener("pagehide", () => {
        const payload = JSON.stringify({ zaznamId });
        if (typeof navigator.sendBeacon === "function") {
            navigator.sendBeacon(releaseEndpoint, new Blob([payload], { type: "application/json" }));
        }
    });
}
