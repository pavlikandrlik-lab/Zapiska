// PmTracker event bus a adaptér pro gov-design-system Web Components
//
// Problém: gov-button emituje CustomEvent "gov-click", ne nativní "click".
// Existující JS posluchače (modals.js, filters.js, bootstrap.js) nasazují
// document.addEventListener("click", ...). Bez adaptéru by přechod na
// gov-button rozbil všechny click handlery.
//
// Řešení: adaptér přeloží gov-click na nativní click na stejném targetu.
// Tím zůstávají existující JS handlery funkční beze změny.
//
// Dokumentace: docs/architecture/js-modules.md

const dispatched = new WeakSet();
// Review fix I1 (2026-10-12): syntetické "click" eventy, které adaptér sám vytvořil a
// vydispatchoval. gov-button při kliknutí s modifikátorem (Ctrl/Cmd/Shift) emituje "gov-click"
// bez ohledu na modifikátory, takže adaptér by vytvořil "plain" click i pro klik do nové karty.
// Konzumenti, kteří rozlišují modifikátory (např. scheduleProposalGuard.js), musí tento
// syntetický click ignorovat a spolehnout se na původní trusted click z vnitřního <a>/<button>,
// který modifikátory nese správně. isAdapterClick nepoužívá event.isTrusted, protože
// programová volání host.click() mají být dál hlídaná.
const adapterClicks = new WeakSet();

export function installGovClickAdapter(root = document) {
    root.addEventListener("gov-click", (event) => {
        const target = event.target;
        if (!(target instanceof Element)) return;
        // Zabránit rekurzi: pokud tento gov-click už byl přemapován, ignorovat
        if (dispatched.has(event)) return;
        dispatched.add(event);

        const native = new MouseEvent("click", {
            bubbles: true,
            cancelable: true,
            composed: true,
            detail: 1
        });
        adapterClicks.add(native);
        target.dispatchEvent(native);
    });
}

/** True pro syntetický click vytvořený adaptérem výše (ne trusted klik uživatele/klávesnice). */
export function isAdapterClick(event) {
    return adapterClicks.has(event);
}

export const appEventBus = {
    on(selector, eventName, handler) {
        document.addEventListener(eventName, (event) => {
            const target = event.target instanceof Element
                ? event.target.closest(selector)
                : null;
            if (target) handler(event, target);
        });
    }
};

// Autoinstall při načtení, pokud je document ready. typeof-guard kvůli node --test (bez DOM),
// stejný vzor jako na konci scheduleProposalGuard.js, které teď isAdapterClick importuje.
if (typeof document !== "undefined") {
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => installGovClickAdapter());
    } else {
        installGovClickAdapter();
    }
}
