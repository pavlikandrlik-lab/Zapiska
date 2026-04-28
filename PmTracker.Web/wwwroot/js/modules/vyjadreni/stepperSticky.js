/**
 * stepperSticky.js — propočítává absolutní Y-offset pro každý aligned krok
 * tak, aby Y-center kroku odpovídal Y-center bound bubliny.
 *
 * Spec: docs/superpowers/specs/2026-04-28-modal-vyjadreni-redesign-design.md §6, §7
 *
 * Recompute trigger:
 *   - body container scroll (rAF debounced)
 *   - body resize (ResizeObserver)
 *   - timeline mutation (MutationObserver na bublin list)
 */

export function initStepperSticky(modalRoot) {
    if (!modalRoot) return null;

    const alignedStepper = modalRoot.querySelector('[data-aligned-stepper]');
    if (!alignedStepper) return null;

    const body = modalRoot.querySelector('.pm-chat-modal__body');
    if (!body) return null;

    function recompute() {
        const items = alignedStepper.querySelectorAll('gov-stepper-item[data-step][data-in-buffer="false"]');
        items.forEach((item) => {
            // Skip pokud probíhá drag — drag handler řídí pozici sám
            if (item.getAttribute('data-dragging') === 'true') return;

            const vyjadreniId = item.getAttribute('data-current-vyjadreni-id');
            if (!vyjadreniId) return;

            const bubble = modalRoot.querySelector('[data-bubble][data-vyjadreni-id="' + vyjadreniId + '"]');
            if (!bubble) return;

            const bubbleRect = bubble.getBoundingClientRect();
            const stepperRect = alignedStepper.getBoundingClientRect();
            const itemHeight = item.offsetHeight;
            const offsetTop = bubbleRect.top - stepperRect.top + (bubbleRect.height / 2) - (itemHeight / 2);

            item.style.position = 'absolute';
            item.style.top = Math.max(0, offsetTop) + 'px';
            item.style.left = '0';
            item.style.right = '0';
        });
    }

    // Initial render po načtení DOM
    requestAnimationFrame(() => recompute());

    let scrollRaf = null;
    function onScroll() {
        if (scrollRaf) return;
        scrollRaf = requestAnimationFrame(function () {
            recompute();
            scrollRaf = null;
        });
    }
    body.addEventListener('scroll', onScroll, { passive: true });

    let resizeObserver = null;
    if (typeof ResizeObserver !== 'undefined') {
        resizeObserver = new ResizeObserver(function () { recompute(); });
        resizeObserver.observe(body);
    }

    let mutationObserver = null;
    if (typeof MutationObserver !== 'undefined') {
        mutationObserver = new MutationObserver(function () { recompute(); });
        const timeline = modalRoot.querySelector('.pm-chat-modal__timeline');
        if (timeline) mutationObserver.observe(timeline, { childList: true, subtree: true });
    }

    return {
        recompute: recompute,
        destroy: function () {
            body.removeEventListener('scroll', onScroll);
            if (resizeObserver) resizeObserver.disconnect();
            if (mutationObserver) mutationObserver.disconnect();
        }
    };
}
