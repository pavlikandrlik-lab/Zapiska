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
        target.dispatchEvent(native);
    });
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

// Autoinstall při načtení, pokud je document ready
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => installGovClickAdapter());
} else {
    installGovClickAdapter();
}
