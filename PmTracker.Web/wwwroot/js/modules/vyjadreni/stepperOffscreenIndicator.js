/**
 * stepperOffscreenIndicator.js — sticky stack mini číselných kruhů s konkrétními
 * čísly off-screen kroků.
 *
 * Spec 2026-04-29 redesign: místo jednoho pill s count zobrazujeme stack malých
 * kruhů (replikuje gov-stepper-item__prefix vizuál) — každý kruh = konkrétní číslo
 * kroku který je mimo viewport. Klikací — scroll na bound bublinu (rychloposun).
 *
 * Layout:
 *   - Top stack: sticky top:0 v .pm-chat-modal__stepper, horizontální řada kruhů
 *     řazená od pravé strany. Zobrazí kroky jejichž bound bublina je nad viewportem.
 *   - Bottom stack: sticky bottom:0, totéž pro kroky pod viewportem.
 *
 * Color states (dle data-source na gov-stepper-item):
 *   - Auto / harvested      → success (zelená)
 *   - Manual                → warning (žlutá/oranžová)
 *   - bez vazby / neutral   → neutral (šedá)
 */

export function initStepperOffscreenIndicator(modalRoot) {
    if (!modalRoot) return null;

    const body = modalRoot.querySelector('.pm-chat-modal__body');
    const stepperSection = modalRoot.querySelector('.pm-chat-modal__stepper');
    if (!body || !stepperSection) return null;

    let topStack = stepperSection.querySelector('[data-offscreen-stack-top]');
    if (!topStack) {
        topStack = document.createElement('div');
        topStack.className = 'pm-chat-modal__offscreen-stack pm-chat-modal__offscreen-stack--top';
        topStack.setAttribute('data-offscreen-stack-top', '');
        topStack.setAttribute('hidden', 'hidden');
        topStack.setAttribute('aria-label', 'Kroky nad viditelnou oblastí');
        stepperSection.insertBefore(topStack, stepperSection.firstChild);
    }
    let bottomStack = stepperSection.querySelector('[data-offscreen-stack-bottom]');
    if (!bottomStack) {
        bottomStack = document.createElement('div');
        bottomStack.className = 'pm-chat-modal__offscreen-stack pm-chat-modal__offscreen-stack--bottom';
        bottomStack.setAttribute('data-offscreen-stack-bottom', '');
        bottomStack.setAttribute('hidden', 'hidden');
        bottomStack.setAttribute('aria-label', 'Kroky pod viditelnou oblastí');
        stepperSection.appendChild(bottomStack);
    }

    function scrollToBubble(vyjadreniId) {
        if (!vyjadreniId) return;
        const bubble = modalRoot.querySelector(
            '[data-bubble][data-vyjadreni-id="' + vyjadreniId + '"]'
        );
        if (bubble) {
            bubble.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    function createCircle(item) {
        const krokPoradi = item.getAttribute('data-krok-poradi') ||
                          item.getAttribute('data-krok-key') || '?';
        const vyjadreniId = item.getAttribute('data-current-vyjadreni-id') || '';
        const color = item.getAttribute('data-source') || 'neutral';

        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'pm-chat-modal__offscreen-step';
        btn.setAttribute('data-krok-poradi', krokPoradi);
        btn.setAttribute('data-vyjadreni-id', vyjadreniId);
        btn.setAttribute('data-color', color);
        btn.setAttribute('title', 'Krok ' + krokPoradi + ' — kliknutím přejít na bublinu');
        btn.setAttribute('aria-label', 'Krok ' + krokPoradi + ', kliknutím přejít na bublinu');
        btn.textContent = krokPoradi;
        btn.addEventListener('click', function () {
            scrollToBubble(vyjadreniId);
        });
        return btn;
    }

    function recompute() {
        const items = modalRoot.querySelectorAll(
            'gov-stepper-item[data-step][data-in-buffer="false"][data-current-vyjadreni-id]'
        );

        topStack.innerHTML = '';
        bottomStack.innerHTML = '';

        if (items.length === 0) {
            topStack.setAttribute('hidden', 'hidden');
            bottomStack.setAttribute('hidden', 'hidden');
            return;
        }

        const bodyRect = body.getBoundingClientRect();
        const visibleTop = bodyRect.top;
        const visibleBottom = bodyRect.bottom;

        const above = [];
        const below = [];
        items.forEach(function (item) {
            const vyjadreniId = item.getAttribute('data-current-vyjadreni-id');
            if (!vyjadreniId) return;
            const bubble = modalRoot.querySelector(
                '[data-bubble][data-vyjadreni-id="' + vyjadreniId + '"]'
            );
            if (!bubble) return;
            const bubbleRect = bubble.getBoundingClientRect();
            const bubbleCenter = bubbleRect.top + bubbleRect.height / 2;
            if (bubbleCenter < visibleTop) above.push(item);
            else if (bubbleCenter > visibleBottom) below.push(item);
        });

        // User logika 2026-04-29: closest-to-viewport step vlevo, furthest vpravo.
        //   - Top (above viewport): krok 1 scrolled out first (furthest) → vpravo;
        //     krok 2 scrolled out later (closer to viewport) → vlevo. Sort DESCENDING.
        //     Layout: [_][2][1] (right-aligned via justify-content: flex-end).
        //   - Bottom (below viewport): krok 3 closest below → vlevo; krok 5 furthest →
        //     vpravo. Sort ASCENDING. Layout: [_][3][4][5].
        const stepNum = function (item) {
            return parseInt(item.getAttribute('data-krok-poradi') || '0', 10);
        };
        above.sort(function (a, b) { return stepNum(b) - stepNum(a); }); // DESC
        below.sort(function (a, b) { return stepNum(a) - stepNum(b); }); // ASC

        if (above.length > 0) {
            topStack.removeAttribute('hidden');
            above.forEach(function (item) {
                topStack.appendChild(createCircle(item));
            });
        } else {
            topStack.setAttribute('hidden', 'hidden');
        }

        if (below.length > 0) {
            bottomStack.removeAttribute('hidden');
            below.forEach(function (item) {
                bottomStack.appendChild(createCircle(item));
            });
        } else {
            bottomStack.setAttribute('hidden', 'hidden');
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
