// Měří výšku sticky .app-header a vystavuje ji jako CSS proměnnou --app-header-h
// na :root. Dashboard přehled ji používá pro height: calc(100dvh - var(--app-header-h)),
// aby se panely vešly přesně na jednu obrazovku a site footer spadl těsně pod fold
// (stránka zůstává scrollovatelná, footer není zbytečně vidět).
//
// Header je dvouřádkový (.app-topbar + .app-nav) a může se na úzkém viewportu
// zalamovat → výšku držíme aktuální přes ResizeObserver + resize fallback.
// CSS má fallback (var(--app-header-h, 110px)) pro první paint / vypnutý JS.

function applyHeaderHeight(header) {
    const h = Math.round(header.getBoundingClientRect().height);
    if (h > 0) {
        document.documentElement.style.setProperty("--app-header-h", `${h}px`);
    }
}

export function initHeaderHeightVar() {
    const header = document.querySelector(".app-header");
    if (!header) {
        return;
    }

    applyHeaderHeight(header);

    if (typeof ResizeObserver !== "undefined") {
        const observer = new ResizeObserver(() => applyHeaderHeight(header));
        observer.observe(header);
    }

    window.addEventListener("resize", () => applyHeaderHeight(header), { passive: true });
}

initHeaderHeightVar();
