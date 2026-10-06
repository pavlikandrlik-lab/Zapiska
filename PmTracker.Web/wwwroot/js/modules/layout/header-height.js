// Vystavuje --app-header-h = vzdálenost od vršku stránky k <main id="main"> (gov hlavička
// + hlavní navigace + případná drobečková lišta). Dashboard přehled ji používá pro
// height: calc(100dvh - var(--app-header-h)), aby se panely vešly na jednu obrazovku
// a patička spadla těsně pod fold. Hlavička není přilepená — jde o výšku nad obsahem
// při scrollu 0. Na úzkém viewportu se hlavička zalamuje → ResizeObserver + resize.
// CSS má fallback (var(--app-header-h, …)) pro první vykreslení / vypnutý JS.

function applyHeaderHeight(main) {
    const h = Math.round(main.getBoundingClientRect().top + window.scrollY);
    if (h > 0) {
        document.documentElement.style.setProperty("--app-header-h", `${h}px`);
    }
}

export function initHeaderHeightVar() {
    const main = document.getElementById("main");
    const header = document.querySelector(".gov-header");
    if (!main || !header) {
        return;
    }

    applyHeaderHeight(main);

    if (typeof ResizeObserver !== "undefined") {
        const observer = new ResizeObserver(() => applyHeaderHeight(main));
        observer.observe(header);
    }

    window.addEventListener("resize", () => applyHeaderHeight(main), { passive: true });
}

initHeaderHeightVar();
