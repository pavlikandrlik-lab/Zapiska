// Kopírování a ukládání diagnostiky na chybové stránce. Panel v modalech si staví
// ajax.js sám z JSON odpovědi; tady je HTML vyrenderované serverem, takže stačí
// obsluha tlačítek. Vlastní ukládání je sdílené s ajax.js (utils.saveTextAsFile),
// aby uživatel viděl u chyby totéž ať spadne modal, nebo obyčejný GET.
//
// Side-effect import v bootstrap.js (memory project_bundle_sync).
import { copyTextToClipboard, sanitizeFileName, saveTextAsFile } from "../utils.js";

function textLogu() {
    const el = document.querySelector("[data-error-log]");
    return el instanceof HTMLElement ? el.textContent || "" : "";
}

function docasnyPopisek(tlacitko, text, puvodni) {
    tlacitko.textContent = text;
    window.setTimeout(() => {
        tlacitko.textContent = puvodni;
    }, 1800);
}

document.addEventListener("click", async (event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) {
        return;
    }

    const copy = target.closest("[data-error-copy]");
    if (copy instanceof HTMLElement) {
        const copied = await copyTextToClipboard(textLogu());
        docasnyPopisek(copy, copied ? "Zkopírováno" : "Kopírování selhalo", "Kopírovat log");
        return;
    }

    const save = target.closest("[data-error-save]");
    if (save instanceof HTMLElement) {
        // Název souboru podle Request ID, stejně jako u modalů podle traceId.
        const trace = document.querySelector("[data-error-trace]");
        const stav = await saveTextAsFile(
            textLogu(),
            sanitizeFileName(trace ? trace.textContent : "", "diagnostic-log"));
        if (stav === "aborted") {
            return;
        }
        docasnyPopisek(save, stav === "saved" ? "Uloženo" : "Uložení selhalo", "Uložit log chyby");
    }
});
