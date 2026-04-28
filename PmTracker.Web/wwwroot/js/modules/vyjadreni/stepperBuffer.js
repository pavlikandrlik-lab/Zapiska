/**
 * stepperBuffer.js — handling bufferu nepřiřazených kroků.
 * Po úspěšném drop kroku z bufferu — krok zmizí z bufferu, objeví se v aligned stepperu.
 *
 * Spec: docs/superpowers/specs/2026-04-28-modal-vyjadreni-redesign-design.md §6
 */

export function moveStepFromBufferToAligned(modalRoot, krokKey) {
    const item = modalRoot.querySelector('gov-stepper-item[data-step][data-krok-key="' + krokKey + '"]');
    if (!item) return;

    const alignedStepper = modalRoot.querySelector('[data-aligned-stepper]');
    if (!alignedStepper) return;

    item.setAttribute('data-in-buffer', 'false');
    alignedStepper.appendChild(item);

    // Pokud buffer je prázdný, schovat ho
    const buffer = modalRoot.querySelector('[data-stepper-buffer]');
    if (buffer) {
        const remainingItems = buffer.querySelectorAll('gov-stepper-item[data-step]');
        if (remainingItems.length === 0) {
            buffer.style.display = 'none';
        }
    }
}

export function moveStepFromAlignedToBuffer(modalRoot, krokKey) {
    const item = modalRoot.querySelector('gov-stepper-item[data-step][data-krok-key="' + krokKey + '"]');
    if (!item) return;

    let buffer = modalRoot.querySelector('[data-stepper-buffer]');
    if (!buffer) {
        // Lazy-create buffer pokud neexistuje (např. první unbind kroku v session)
        buffer = document.createElement('div');
        buffer.className = 'pm-chat-modal__buffer';
        buffer.setAttribute('data-stepper-buffer', '');
        buffer.setAttribute('aria-label', 'Nepřiřazené kroky');
        const header = document.createElement('div');
        header.className = 'pm-chat-modal__buffer-header';
        header.textContent = 'Nepřiřazené kroky';
        buffer.appendChild(header);
        const stepper = document.createElement('gov-stepper');
        stepper.setAttribute('size', 'm');
        stepper.setAttribute('data-buffer-stepper', '');
        buffer.appendChild(stepper);
        const stepperSection = modalRoot.querySelector('.pm-chat-modal__stepper');
        if (stepperSection) stepperSection.insertBefore(buffer, stepperSection.firstChild);
    }
    buffer.style.display = '';

    const bufferStepper = buffer.querySelector('[data-buffer-stepper]');
    if (bufferStepper) {
        item.setAttribute('data-in-buffer', 'true');
        item.setAttribute('color', 'error');
        item.setAttribute('data-source', '');
        item.setAttribute('data-current-vyjadreni-id', '');
        item.setAttribute('data-current-datum', '');
        const contentSlot = item.querySelector('[slot="content"]');
        if (contentSlot) contentSlot.textContent = '—';
        // Reset position styly z drag/sticky
        item.style.position = '';
        item.style.top = '';
        item.style.left = '';
        item.style.right = '';
        item.style.width = '';
        item.style.zIndex = '';
        bufferStepper.appendChild(item);
    }
}
