// PmTracker.Web/wwwroot/js/components/pm-chat-stepper/buffer.js
// Plán 1 Feature B — 5 pevných slotů stepperu (3 fixní + 2 add-on).
// Fixní sloty reprezentují chronologické kroky dle typu ticketu (K3/K6/K4/K7/K10).
// Add-on sloty slouží pro user-added nepovinné kroky (permission-gated).
(function(global){
  'use strict';

  const FIXED_SLOTS = 3;
  const ADDON_SLOTS = 2;
  const TOTAL_SLOTS = FIXED_SLOTS + ADDON_SLOTS;

  /**
   * Vrátí renderable slots pro stepper: 3 fixní (dle typu ticketu) + 2 prázdné add-on sloty.
   * Pokud fixní je víc než 3, extend přirozeně; pokud méně, padding empty.
   * @param {Array<{poradi:number, label:string, bindingDatum:Date|null, fixni:boolean}>} kroky
   * @param {boolean} mozeAddonPridat - user má právo přidat nepovinný krok
   * @returns {Array} renderable slots
   */
  function computeStepperSlots(kroky, mozeAddonPridat) {
    const fixed = kroky.filter(k => k.fixni).sort((a,b) => a.poradi - b.poradi);
    const addon = kroky.filter(k => !k.fixni).sort((a,b) => a.poradi - b.poradi);
    const slots = [...fixed, ...addon];

    // Padding do TOTAL_SLOTS prázdnými add-on sloty
    while (slots.length < TOTAL_SLOTS) {
      slots.push({
        poradi: slots.length + 1,
        label: mozeAddonPridat ? '+ přidat nepovinný krok' : '—',
        bindingDatum: null,
        fixni: false,
        isBufferSlot: true,
        canAdd: mozeAddonPridat
      });
    }

    return slots;
  }

  global.pmChatStepperBuffer = { computeStepperSlots, FIXED_SLOTS, ADDON_SLOTS, TOTAL_SLOTS };
})(window);
