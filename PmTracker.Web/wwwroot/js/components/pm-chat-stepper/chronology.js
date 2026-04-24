// PmTracker.Web/wwwroot/js/components/pm-chat-stepper/chronology.js
// Plán 1 Feature B — chronology pure funkce pro drag&drop validaci na stepperu.
// Pravidla (memory project_servicedesk_infosystem_binding.md "Stepper chronologie rules"):
//   - Krok dříve v řadě (menší poradi) musí mít bublinu s ranějším datem nebo prázdný.
//   - Krok později v řadě (větší poradi) může obsahovat bublinu s pozdějším datem
//     (může se „posunout dolů" pokud nové drop ho předběhne — cascade).
//   - Spodní krok nemůže vytlačit horní; horní krok posouvá spodní.
(function(global){
  'use strict';

  /**
   * Validuje, zda bublina s datem `bubbleDatum` může být puštěna na krok `targetKrok`.
   * @param {Date} bubbleDatum - datum bubliny, která se přesouvá
   * @param {{poradi: number, bindingDatum: Date|null}} targetKrok - cílový krok
   * @param {Array<{poradi: number, bindingDatum: Date|null}>} allKroky - všechny kroky seřazené dle poradi asc
   * @returns {{ok: boolean, reason?: string, cascade?: Array<number>}}
   */
  function validateDrop(bubbleDatum, targetKrok, allKroky) {
    // 1) Zkontroluj kroky s menším poradi (musí být ≤ bubbleDatum)
    for (const krok of allKroky) {
      if (krok.poradi >= targetKrok.poradi) continue;
      if (krok.bindingDatum && krok.bindingDatum > bubbleDatum) {
        return {
          ok: false,
          reason: `Krok #${krok.poradi} má binding z ${krok.bindingDatum.toISOString().slice(0,10)} — nelze vložit dříve datovanou bublinu.`
        };
      }
    }
    // 2) Zkontroluj kroky s vyšším poradi (pokud existující binding < bubbleDatum, cascade)
    const cascade = [];
    for (const krok of allKroky) {
      if (krok.poradi <= targetKrok.poradi) continue;
      if (krok.bindingDatum && krok.bindingDatum < bubbleDatum) {
        cascade.push(krok.poradi);
      }
    }
    return { ok: true, cascade: cascade.length > 0 ? cascade : undefined };
  }

  global.pmChatStepperChronology = { validateDrop };
})(window);
