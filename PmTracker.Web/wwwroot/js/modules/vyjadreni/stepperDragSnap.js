/**
 * stepperDragSnap.js — cursor-tracked drag krok elementu s magnetic snap
 * na nejbližší vyjádření u kurzoru. Inverze drag flow z 2026-04-21 specu —
 * táhne se KROK, ne bublina.
 *
 * Spec: docs/superpowers/specs/2026-04-28-modal-vyjadreni-redesign-design.md §4
 *
 * Behavior:
 * 1. mouse-down na gov-stepper-item[data-step] → drag start (pokud není pinned)
 * 2. mousemove → krok plynule sleduje kurzor po Y ose, snap na Y-center nejbližší bubliny
 * 3. mouse-up → validace (chronologie + 1:1) → commit nebo revert
 */

export function initStepperDragSnap(modalRoot, options) {
    if (!modalRoot) return null;
    const opts = options || {};
    const onCommit = opts.onCommit || function () { return Promise.resolve(); };

    let active = null;

    function findNearestBubble(cursorY) {
        const bubbles = modalRoot.querySelectorAll('[data-bubble]');
        let nearest = null;
        let nearestDistance = Infinity;
        bubbles.forEach(function (b) {
            const rect = b.getBoundingClientRect();
            const center = rect.top + rect.height / 2;
            const distance = Math.abs(center - cursorY);
            if (distance < nearestDistance) {
                nearestDistance = distance;
                nearest = b;
            }
        });
        return nearest;
    }

    function clearDropTargets() {
        modalRoot.querySelectorAll('[data-bubble].drop-target').forEach(function (b) {
            b.classList.remove('drop-target');
        });
    }

    function showToast(message, state) {
        const status = modalRoot.querySelector('[data-chat-status]');
        if (!status) return;
        status.textContent = message;
        status.setAttribute('data-status-state', state || 'ok');
        setTimeout(function () {
            if (status.textContent === message) {
                status.textContent = '';
                status.removeAttribute('data-status-state');
            }
        }, 4000);
    }

    function revertItem(item, state) {
        item.removeAttribute('data-dragging');
        item.style.position = state.originalPosition || '';
        item.style.top = state.originalTop || '';
        item.style.left = state.originalLeft || '';
        item.style.right = state.originalRight || '';
        item.style.width = state.originalWidth || '';
        item.style.zIndex = state.originalZIndex || '';
    }

    function onMouseDown(ev) {
        const item = ev.target.closest('gov-stepper-item[data-step]');
        if (!item) return;
        if (item.getAttribute('data-pinned') === 'true') return;
        if (ev.button !== 0) return; // jen left button

        const rect = item.getBoundingClientRect();
        active = {
            item: item,
            startCursorY: ev.clientY,
            originalPosition: item.style.position || '',
            originalTop: item.style.top || '',
            originalLeft: item.style.left || '',
            originalRight: item.style.right || '',
            originalWidth: item.style.width || '',
            originalZIndex: item.style.zIndex || '',
            currentSnapBubble: null
        };
        item.setAttribute('data-dragging', 'true');
        item.style.position = 'fixed';
        item.style.zIndex = '1000';
        item.style.top = rect.top + 'px';
        item.style.left = rect.left + 'px';
        item.style.right = '';
        item.style.width = rect.width + 'px';

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
        ev.preventDefault();
    }

    function onMouseMove(ev) {
        if (!active) return;

        const target = findNearestBubble(ev.clientY);
        clearDropTargets();
        if (!target) {
            // Žádný target v dosahu — krok zůstává u kurzoru
            const itemHeight = active.item.offsetHeight;
            active.item.style.top = (ev.clientY - itemHeight / 2) + 'px';
            return;
        }

        target.classList.add('drop-target');
        active.currentSnapBubble = target;

        const bubbleRect = target.getBoundingClientRect();
        const bubbleCenterY = bubbleRect.top + bubbleRect.height / 2;
        const itemHeight = active.item.offsetHeight;
        active.item.style.top = (bubbleCenterY - itemHeight / 2) + 'px';
    }

    function onMouseUp() {
        if (!active) return;

        document.removeEventListener('mousemove', onMouseMove);
        document.removeEventListener('mouseup', onMouseUp);

        const item = active.item;
        const target = active.currentSnapBubble;
        const state = active;

        if (!target) {
            revertItem(item, state);
            active = null;
            clearDropTargets();
            return;
        }

        const targetVyjadreniId = target.getAttribute('data-vyjadreni-id');
        const targetDatum = target.getAttribute('data-datum');
        const krokKey = item.getAttribute('data-krok-key');
        const krokPoradi = parseInt(item.getAttribute('data-krok-poradi'), 10);
        const vazbaId = item.getAttribute('data-vazba-id');

        // 1:1 — bublina už má jiný krok?
        const allItems = modalRoot.querySelectorAll('gov-stepper-item[data-step][data-current-vyjadreni-id]');
        let conflictingStep = null;
        allItems.forEach(function (other) {
            if (other === item) return;
            const otherVyId = other.getAttribute('data-current-vyjadreni-id');
            if (otherVyId === targetVyjadreniId) {
                conflictingStep = other;
            }
        });
        if (conflictingStep) {
            const otherPoradi = conflictingStep.getAttribute('data-krok-poradi');
            showToast('Bublina již má přiřazený krok ' + otherPoradi + '. Odpojte ho nejdřív.', 'error');
            revertItem(item, state);
            active = null;
            clearDropTargets();
            return;
        }

        // Chronologie validace
        const allActiveItems = Array.from(modalRoot.querySelectorAll('gov-stepper-item[data-step][data-in-buffer="false"]'));
        if (targetDatum) {
            const targetD = new Date(targetDatum).getTime();
            for (let i = 0; i < allActiveItems.length; i++) {
                const other = allActiveItems[i];
                if (other === item) continue;
                const otherPoradi = parseInt(other.getAttribute('data-krok-poradi'), 10);
                const otherDatumStr = other.getAttribute('data-current-datum');
                if (!otherDatumStr) continue;
                const otherD = new Date(otherDatumStr).getTime();

                if (otherPoradi < krokPoradi && otherD > targetD) {
                    showToast('Krok ' + krokPoradi + ' nelze přiřadit zde — porušila by se chronologie kroku ' + otherPoradi + '.', 'error');
                    revertItem(item, state);
                    active = null;
                    clearDropTargets();
                    return;
                }
                if (otherPoradi > krokPoradi && otherD < targetD) {
                    showToast('Krok ' + krokPoradi + ' nelze přiřadit zde — porušila by se chronologie kroku ' + otherPoradi + '.', 'error');
                    revertItem(item, state);
                    active = null;
                    clearDropTargets();
                    return;
                }
            }
        }

        // Commit (async)
        onCommit({
            krokKey: krokKey,
            vazbaId: vazbaId,
            vyjadreniId: targetVyjadreniId,
            datum: targetDatum
        }).then(function () {
            item.setAttribute('data-current-vyjadreni-id', targetVyjadreniId);
            item.setAttribute('data-current-datum', targetDatum || '');
            item.setAttribute('data-source', 'warning');
            item.setAttribute('color', 'warning');
            item.setAttribute('data-in-buffer', 'false');

            item.removeAttribute('data-dragging');
            item.style.position = '';
            item.style.zIndex = '';
            item.style.top = '';
            item.style.left = '';
            item.style.right = '';
            item.style.width = '';

            showToast('Krok přiřazen.', 'ok');
            clearDropTargets();
            active = null;
        }).catch(function (err) {
            console.error('Stepper commit failed', err);
            showToast('Uložení selhalo. Zkuste znovu.', 'error');
            revertItem(item, state);
            active = null;
            clearDropTargets();
        });
    }

    modalRoot.addEventListener('mousedown', onMouseDown);

    return {
        destroy: function () {
            modalRoot.removeEventListener('mousedown', onMouseDown);
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
        }
    };
}
