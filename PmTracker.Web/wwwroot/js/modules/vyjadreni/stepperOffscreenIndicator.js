/**
 * stepperOffscreenIndicator.js — sticky indikátor počtu kroků mimo viewport.
 *
 * Spec 2026-04-29: když user scrolluje v modalu vyjadreni a některé bound kroky
 * (vedle vyjadreni) jsou mimo viditelnou plochu, zobrazí se na horní/dolní hraně
 * stepperu indikátor "↑ N" / "↓ N" — uchytí se na okraj místo aby se vytrácel ven.
 *
 * Strategy:
 *   - Track all gov-stepper-item[data-step][data-in-buffer="false"] s navázaným vyjadřením.
 *   - On scroll: zjisti, kolik z nich má y-pozici nad / pod viditelnou částí body containeru.
 *   - Update top/bottom indicator s počtem.
 */

export function initStepperOffscreenIndicator(modalRoot) {
    if (!modalRoot) return null;

    const body = modalRoot.querySelector('.pm-chat-modal__body');
    const stepperSection = modalRoot.querySelector('.pm-chat-modal__stepper');
    if (!body || !stepperSection) return null;

    // Vytvořit indikátory (pokud ještě neexistují)
    let topIndicator = stepperSection.querySelector('[data-offscreen-top]');
    if (!topIndicator) {
        topIndicator = document.createElement('div');
        topIndicator.className = 'pm-chat-modal__offscreen-indicator pm-chat-modal__offscreen-indicator--top';
        topIndicator.setAttribute('data-offscreen-top', '');
        topIndicator.setAttribute('hidden', 'hidden');
        topIndicator.setAttribute('aria-hidden', 'true');
        topIndicator.innerHTML =
            '<gov-icon size="s" name="chevron-up" type="components" aria-hidden="true"></gov-icon>' +
            '<span data-offscreen-count>0</span>';
        stepperSection.insertBefore(topIndicator, stepperSection.firstChild);
    }

    let bottomIndicator = stepperSection.querySelector('[data-offscreen-bottom]');
    if (!bottomIndicator) {
        bottomIndicator = document.createElement('div');
        bottomIndicator.className = 'pm-chat-modal__offscreen-indicator pm-chat-modal__offscreen-indicator--bottom';
        bottomIndicator.setAttribute('data-offscreen-bottom', '');
        bottomIndicator.setAttribute('hidden', 'hidden');
        bottomIndicator.setAttribute('aria-hidden', 'true');
        bottomIndicator.innerHTML =
            '<gov-icon size="s" name="chevron-down" type="components" aria-hidden="true"></gov-icon>' +
            '<span data-offscreen-count>0</span>';
        stepperSection.appendChild(bottomIndicator);
    }

    function recompute() {
        const items = modalRoot.querySelectorAll(
            'gov-stepper-item[data-step][data-in-buffer="false"][data-current-vyjadreni-id]'
        );
        if (items.length === 0) {
            topIndicator.setAttribute('hidden', 'hidden');
            bottomIndicator.setAttribute('hidden', 'hidden');
            return;
        }

        const bodyRect = body.getBoundingClientRect();
        const visibleTop = bodyRect.top;
        const visibleBottom = bodyRect.bottom;

        let aboveCount = 0;
        let belowCount = 0;
        items.forEach(function (item) {
            // Najdi bound bublinu — sticky alignment ukazuje krok vedle ní.
            const vyjadreniId = item.getAttribute('data-current-vyjadreni-id');
            if (!vyjadreniId) return;
            const bubble = modalRoot.querySelector(
                '[data-bubble][data-vyjadreni-id="' + vyjadreniId + '"]'
            );
            if (!bubble) return;
            const bubbleRect = bubble.getBoundingClientRect();
            const bubbleCenter = bubbleRect.top + bubbleRect.height / 2;
            if (bubbleCenter < visibleTop) aboveCount++;
            else if (bubbleCenter > visibleBottom) belowCount++;
        });

        if (aboveCount > 0) {
            topIndicator.removeAttribute('hidden');
            const c = topIndicator.querySelector('[data-offscreen-count]');
            if (c) c.textContent = String(aboveCount);
        } else {
            topIndicator.setAttribute('hidden', 'hidden');
        }
        if (belowCount > 0) {
            bottomIndicator.removeAttribute('hidden');
            const c = bottomIndicator.querySelector('[data-offscreen-count]');
            if (c) c.textContent = String(belowCount);
        } else {
            bottomIndicator.setAttribute('hidden', 'hidden');
        }
    }

    // Initial render po DOM hydrace
    requestAnimationFrame(function () { recompute(); });

    // Recompute on scroll (rAF debounced)
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

    return {
        recompute: recompute,
        destroy: function () {
            body.removeEventListener('scroll', onScroll);
            if (resizeObserver) resizeObserver.disconnect();
        }
    };
}
