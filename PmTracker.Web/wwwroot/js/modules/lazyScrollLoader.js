// lazyScrollLoader.js — vertical lazy-load "po dvojnásobku stránky" helper.
//
// Pattern:
//   - Initial server render zobrazí prvních N items (≈ 2× viewport height).
//   - Na konci seznamu je sentinel element (prázdný div s data-lazy-scroll-sentinel).
//   - IntersectionObserver hlídá sentinel; když vstoupí do viewportu (s rootMargin
//     = 1 viewport), načte další batch přes fetchMore callback.
//   - Nové items se appendují před sentinel; sentinel se přesune na konec nebo se
//     odstraní, pokud server vrátí hasMore=false.
//
// Proč „po dvojnásobku stránky": uživatel scrolluje plynule bez viditelného loadingu,
// data se dotahují předem (prefetch efekt). Žádné klasické stránkování ("stránka 3 z 8").
//
// Usage:
//   initLazyScrollLoader(container, {
//     fetchMore: async (offset, take, ct) => ({ items: [...], hasMore: true }),
//     renderItem: (item) => "<li>...</li>",   // nebo DOM Element
//     itemsContainer: container.querySelector(".items"),
//     sentinel: container.querySelector("[data-lazy-scroll-sentinel]"),
//     pageSize: 50,                          // velikost batche
//     rootMargin: "200%",                    // preload 2× viewport napřed
//   });
//
// Cancellation: IntersectionObserver se disconnectne při unloadu container elementu.

/**
 * @typedef {Object} LazyScrollOptions
 * @property {(offset: number, take: number, signal: AbortSignal) => Promise<{items: any[], hasMore: boolean}>} fetchMore
 * @property {(item: any) => (string|Element)} renderItem
 * @property {HTMLElement} itemsContainer - kam se appendují nově načtené items
 * @property {HTMLElement} sentinel - div s data-lazy-scroll-sentinel pro IntersectionObserver
 * @property {number} [pageSize=50]
 * @property {string} [rootMargin="200%"] - jak brzy napřed začít loadovat
 */

/**
 * @param {HTMLElement} container - root element celé listy (pro disconnect na unload)
 * @param {LazyScrollOptions} options
 * @returns {() => void} disconnect — manual teardown (typicky netřeba, MutationObserver to zařídí)
 */
export function initLazyScrollLoader(container, options) {
    const {
        fetchMore,
        renderItem,
        itemsContainer,
        sentinel,
        pageSize = 50,
        rootMargin = "200%"
    } = options;

    if (!(container instanceof HTMLElement) || !(itemsContainer instanceof HTMLElement) || !(sentinel instanceof HTMLElement)) {
        return () => {};
    }
    if (typeof fetchMore !== "function" || typeof renderItem !== "function") {
        return () => {};
    }

    let offset = Number(container.dataset.lazyScrollInitialOffset || itemsContainer.childElementCount);
    let isLoading = false;
    let hasMore = (container.dataset.lazyScrollHasMore || "true") === "true";
    const abortController = new AbortController();

    async function loadNext() {
        if (isLoading || !hasMore) return;
        isLoading = true;
        sentinel.setAttribute("aria-busy", "true");
        try {
            const result = await fetchMore(offset, pageSize, abortController.signal);
            if (!result || !Array.isArray(result.items)) {
                hasMore = false;
                return;
            }
            const fragment = document.createDocumentFragment();
            for (const item of result.items) {
                const rendered = renderItem(item);
                if (typeof rendered === "string") {
                    const wrapper = document.createElement("template");
                    wrapper.innerHTML = rendered.trim();
                    if (wrapper.content.firstElementChild) {
                        fragment.appendChild(wrapper.content.firstElementChild);
                    }
                } else if (rendered instanceof Node) {
                    fragment.appendChild(rendered);
                }
            }
            itemsContainer.insertBefore(fragment, sentinel);
            offset += result.items.length;
            hasMore = Boolean(result.hasMore);
            if (!hasMore) {
                sentinel.remove();
                observer.disconnect();
            }
        }
        catch (err) {
            if (err?.name !== "AbortError") {
                sentinel.dataset.lazyScrollError = err?.message || "Chyba při načítání dalších položek.";
            }
        }
        finally {
            isLoading = false;
            sentinel.removeAttribute("aria-busy");
        }
    }

    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                loadNext();
            }
        }
    }, { rootMargin });

    observer.observe(sentinel);

    // Auto-teardown: pokud je container odstraněn z DOM, přeruš probíhající fetch.
    const mutationObserver = new MutationObserver(() => {
        if (!document.body.contains(container)) {
            abortController.abort();
            observer.disconnect();
            mutationObserver.disconnect();
        }
    });
    mutationObserver.observe(document.body, { childList: true, subtree: true });

    return () => {
        abortController.abort();
        observer.disconnect();
        mutationObserver.disconnect();
    };
}
