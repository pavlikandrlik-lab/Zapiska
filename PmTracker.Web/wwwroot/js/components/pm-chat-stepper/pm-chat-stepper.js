// PmTracker.Web/wwwroot/js/components/pm-chat-stepper/pm-chat-stepper.js
// Plán 1 Feature B — <pm-chat-stepper> custom element.
// Wrapper nad gov-stepper (styly dědí přes CSS v pm-chat-stepper.css) + 5-slot buffer
// (viz buffer.js) + chronology drag&drop validace (viz chronology.js).
(function(global){
  'use strict';

  class PmChatStepperElement extends HTMLElement {
    constructor() {
      super();
      this._kroky = [];
      this._canAddAddon = false;
    }

    connectedCallback() {
      this._canAddAddon = this.hasAttribute('can-add-addon');
      this._render();
      this._bindDrops();
    }

    /** Setter volaný z chat modalu při initu — pole krok objektů (viz buffer.js). */
    setKroky(kroky) {
      this._kroky = Array.isArray(kroky) ? kroky : [];
      if (this.isConnected) this._render();
    }

    _render() {
      const buffer = global.pmChatStepperBuffer;
      if (!buffer || typeof buffer.computeStepperSlots !== 'function') {
        // Defensivní fallback — pokud se buffer.js nenahrál, nevypíš raw objekty.
        this.textContent = '';
        return;
      }
      const slots = buffer.computeStepperSlots(this._kroky, this._canAddAddon);
      this.innerHTML = slots.map(slot => `
        <div class="pm-chat-step${slot.isBufferSlot ? ' pm-chat-stepper__buffer-slot' : ''}"
             data-krok-key="${slot.poradi}"
             data-krok-poradi="${slot.poradi}"
             data-is-buffer="${!!slot.isBufferSlot}"
             data-can-add="${!!slot.canAdd}"
             aria-dropeffect="move">
          <span class="pm-chat-step__label">${escapeHtml(slot.label)}</span>
          ${slot.bindingDatum ? `<span class="pm-chat-step__datum">${formatDatum(slot.bindingDatum)}</span>` : ''}
        </div>
      `).join('');
    }

    _bindDrops() {
      this.addEventListener('dragover', this._onDragOver.bind(this));
      this.addEventListener('dragleave', this._onDragLeave.bind(this));
      this.addEventListener('drop', this._onDrop.bind(this));
    }

    _onDragOver(e) {
      const target = e.target.closest('[data-krok-key]');
      if (!target) return;
      e.preventDefault();

      const raw = e.dataTransfer && e.dataTransfer.types && e.dataTransfer.types.includes('application/x-bubble-datum')
        ? e.dataTransfer.getData('application/x-bubble-datum')
        : null;
      const bubbleDatum = raw ? new Date(raw) : new Date(NaN);
      if (isNaN(bubbleDatum.valueOf())) return;

      // Memory "Ticket bez id mimo scope": bubble musí nést ID; jinak ignorovat.
      const bubbleId = e.dataTransfer.getData('application/x-bubble-id');
      if (!bubbleId) return;

      // int.Parse (ne TryParse) per memory pravidlo — když atribut chybí, je to bug upstream.
      const poradi = parseInt(target.dataset.krokPoradi || target.dataset.krokKey, 10);
      const targetKrok = this._kroky.find(k => k.poradi === poradi) || { poradi, bindingDatum: null };
      const chrono = global.pmChatStepperChronology;
      if (!chrono || typeof chrono.validateDrop !== 'function') return;
      const v = chrono.validateDrop(bubbleDatum, targetKrok, this._kroky);

      this.querySelectorAll('[data-krok-key]').forEach(el => {
        el.classList.remove('pm-chat-stepper__drop-valid', 'pm-chat-stepper__drop-invalid');
      });
      target.classList.add(v.ok ? 'pm-chat-stepper__drop-valid' : 'pm-chat-stepper__drop-invalid');

      if (!v.ok) {
        target.title = v.reason || '';
        e.dataTransfer.dropEffect = 'none';
      } else {
        target.title = v.cascade ? `Cascade: posunou se kroky ${v.cascade.join(', ')}` : '';
        e.dataTransfer.dropEffect = 'move';
      }
    }

    _onDragLeave(e) {
      const target = e.target.closest('[data-krok-key]');
      if (target) target.classList.remove('pm-chat-stepper__drop-valid', 'pm-chat-stepper__drop-invalid');
    }

    _onDrop(e) {
      e.preventDefault();
      const target = e.target.closest('[data-krok-key]');
      if (!target) return;
      target.classList.remove('pm-chat-stepper__drop-valid', 'pm-chat-stepper__drop-invalid');

      const bubbleId = e.dataTransfer.getData('application/x-bubble-id');
      // Memory "Ticket bez id mimo scope": bez id drop ignoruj.
      if (!bubbleId) return;

      const detail = {
        krokPoradi: parseInt(target.dataset.krokPoradi || target.dataset.krokKey, 10),
        bubbleId,
        bubbleDatum: e.dataTransfer.getData('application/x-bubble-datum'),
        isBufferSlot: target.dataset.isBuffer === 'true',
        canAdd: target.dataset.canAdd === 'true'
      };
      this.dispatchEvent(new CustomEvent('pm-chat-stepper-drop', { detail, bubbles: true }));
    }
  }

  function escapeHtml(s) {
    if (s == null) return '';
    return String(s)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function formatDatum(d) {
    try {
      return (d instanceof Date ? d : new Date(d)).toISOString().slice(0, 10);
    } catch { return ''; }
  }

  if (!customElements.get('pm-chat-stepper')) {
    customElements.define('pm-chat-stepper', PmChatStepperElement);
  }

  global.PmChatStepperElement = PmChatStepperElement;
})(window);
