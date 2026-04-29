/**
 * chatModalDragDrop.js — orchestrace bindings v modalu Vyjádření a termíny.
 *
 * 2026-04-29 redesign: drag&drop nahrazen dropdown selectorem v každé bublině
 * (bubbleStepSelector.js). Stepper vpravo zachován jako informational dashboard
 * se sticky alignmentem (stepperSticky.js) a click-to-scroll na bound bublinu.
 *
 * Spec: docs/superpowers/specs/2026-04-29-modal-vyjadreni-dropdown-design.md
 *
 * Backward-compat API: window.pmChatModalDragDrop.attach(root) — voláno z chatModal.js.
 * Název modulu zachován pro backward-compat (i když drag už není), aby chatModal.js
 * nemusel měnit referenci na window.pmChatModalDragDrop.
 */

import { initStepperSticky } from './stepperSticky.js';
import { attachBubbleStepSelectors } from './bubbleStepSelector.js';
import { initStepperOffscreenIndicator } from './stepperOffscreenIndicator.js';

function attach(root) {
    if (!root) return null;
    if (root.getAttribute('data-chat-init') === 'true') return null;
    root.setAttribute('data-chat-init', 'true');

    // Sticky aligned kroky vedle bound bublin (informational dashboard).
    const sticky = initStepperSticky(root);

    // Spec 2026-04-29: indikátor počtu off-screen kroků (↑ N / ↓ N)
    // se uchytí na okraj místo aby kroky vyjely ven mimo modal.
    const offscreen = initStepperOffscreenIndicator(root);

    // Dropdown handlers + ✕ clear na badge + click-to-scroll na stepper items
    // + paralelní „Odpojit" tlačítko v stepper item content slot.
    attachBubbleStepSelectors(root);

    return {
        destroy: function () {
            if (sticky) sticky.destroy();
            if (offscreen) offscreen.destroy();
        }
    };
}

if (typeof window !== 'undefined') {
    window.pmChatModalDragDrop = { attach: attach };
}

export { attach };
