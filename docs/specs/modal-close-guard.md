# Specifikace — close guard modalu úpravy záznamu

Dokumentuje chování ochrany proti nechtěnému zavření modálu editoru záznamu
(„Máte neuložené změny. Chcete je zahodit?").

---

## Kontext

Modal editoru záznamu (otevírá se tlačítkem „Upravit" u jednotlivého záznamu)
obsahuje formulář s kartami **Záznam**, **Externí vazby**, **Spolupráce** a
**Harmonogram**. Uživatel může kdykoli chtít modal zavřít:

- křížek vpravo nahoře (aria-label „Zavřít dialog", `.modal-close`)
- tlačítko **Zrušit** v action bar (`[data-modal-close]`)
- klávesa **Escape**

Pokud uživatel zavolal některou akci zavření a **formulář je „dirty"**
(změnil se oproti snapshotu pořízenému při otevření), místo zavření se
zobrazí overlay **close-guard** s dotazem „Pokračovat v úpravách" /
„Zahodit změny".

---

## Dirty tracking

Snapshot formuláře se tvoří v `initRecordEditorDirtyTracking`:

1. Jakmile je formulář připojený k DOM, vyčte `FormData(form)` a seřadí
   entries (`key=value&…`) → tento string je uložený do
   `form.dataset.recordEditorSnapshot`.
2. Dirty check (`isRecordEditorFormDirty`) porovná aktuální snapshot
   s uloženým — pokud se liší, form je dirty.
3. Pole jsou z porovnání **ignorovaná** (`shouldIgnoreRecordEditorField`),
   pokud jde o:
   - `__requestverificationtoken` (antiforgery, mění se při reload)
   - `presentation`, `returnurl` (navigační metadata)
   - `editortab` (aktuálně vybraná karta — mění se kliknutím na tab)
   - **všechny názvy začínající `uiharmonogramdatumy`** — tato pole jsou
     POUZE vypočítaná datumy ze schedule planneru, nikoli uživatelský vstup.

### Implementace

- [PmTracker.Web/wwwroot/js/modules/recordEditor.js](../../PmTracker.Web/wwwroot/js/modules/recordEditor.js) —
  `shouldIgnoreRecordEditorField`, `buildRecordEditorFormSnapshot`,
  `isRecordEditorFormDirty`, `promptRecordEditorDiscard`,
  `requestRecordEditorModalClose`, `initRecordFormTabs`.
- [PmTracker.Web/wwwroot/js/site.bundle.js](../../PmTracker.Web/wwwroot/js/site.bundle.js) —
  bundled verze, MUSÍ být synchronizovaná se zdrojem.

---

## Schedule tab — speciální chování

Po přepnutí na kartu **Harmonogram** `initRecordFormTabs`:

1. Nainicializuje `ScheduleRenderer` (pokud nebyl) a zavolá `recalcAll()`.
2. `recalcAll()` přepíše:
   - `HarmonogramHodnoty[N].Hodnota` — normalizovaná duration (`"3"` místo `"  3 "`).
   - `UiHarmonogramDatumy[N]` — vypočítaná datumy z duration + start date.
   - `data-app-date-display` hodnoty pickerů.
3. Tyto změny jsou **automatické**, nikoli uživatelské.

**Pravidlo**: po `recalcAll` v schedule tab switchingu se snapshot MUSÍ
přegenerovat (`requestAnimationFrame` → `buildRecordEditorFormSnapshot`).
Jinak by pouhé přepnutí na Harmonogram učinilo form dirty → close-guard
by nesmyslně blokoval zavření modálu.

**Kde**: [modules/recordEditor.js](../../PmTracker.Web/wwwroot/js/modules/recordEditor.js)
`initRecordFormTabs`, větev `if (normalizedTab === "schedule")`:

```js
window.requestAnimationFrame(() => {
    if (form.isConnected) {
        form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
    }
});
```

Stejný fix musí být i v `site.bundle.js`.

---

## Close flow

```
Uživatel klikne křížek / Zrušit / stiskne Escape
       ↓
handleDocumentClick / handleDocumentOverlayKeydown
       ↓
requestRecordEditorModalClose(trigger)
       ↓
promptRecordEditorDiscard(editorForm, trigger)
       ↓
  isRecordEditorFormDirty(form) → false → closeModal()       ← normální případ
                               → true  → overlay close-guard
                                         ↳ „Pokračovat" → zůstat
                                         ↳ „Zahodit"    → closeModal()
```

---

## Pokrytí testy

- E2E: [PmTracker.Tests.E2E/Scenarios/RecordEditModalCloseScenariosTests.cs](../../PmTracker.Tests.E2E/Scenarios/RecordEditModalCloseScenariosTests.cs)
  - Křížek, Escape, Zrušit — základní zavření
  - Přepnutí na Harmonogram + křížek / Escape — reprodukce bugu, který
    opravil commit [TBD]. Test ověřuje, že overlay `[data-record-editor-close-guard]`
    NENÍ zobrazen a modal se skutečně zavře.

---

## Pravidla pro úpravy

1. **Nepřidávej do formuláře nové hidden/vypočítané inputy** bez
   přidání jejich `name` do `shouldIgnoreRecordEditorField`.
2. **Nová karta editoru, která přepisuje hodnoty** (podobně jako schedule tab)
   musí přegenerovat snapshot po svých initech.
3. **Synchronizace bundle**: pokud měníš logiku `recordEditor.js`, uprav
   i `site.bundle.js` — aplikace v prohlížeči načítá bundle, ne moduly.
