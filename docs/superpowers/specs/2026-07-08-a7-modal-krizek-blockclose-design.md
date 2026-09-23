# A7 — Křížek modalu nejde kliknout (block-close disabluje X v nové gov verzi)

**Datum:** 2026-07-08 · **Stav:** schváleno uživatelem (analýza 2026-07-08; Escape smí zavírat)

## Problém a kontext okolí (reprodukováno)

User report: modal „Přidat projektovou roli" (Osoby tým) nejde zavřít křížkem; Zrušit funguje. **Reprodukce ukázala, že X je mrtvý ve VŠECH modalech** (ověřeno i „Nové jednání") — user si všiml jen jednoho výskytu.

Řetěz příčin:
1. `_ModalLayout.cshtml:51` dává každému modalu `block-close="true"` + `block-backdrop-close="true"`. Původní záměr (komentáře v _ModalLayout i bootstrap.js:646): block-close jen zabrání self-close, `gov-close` event dál přijde a náš `handleGovCloseEvent` → `closeModal()`.
2. **Aktualizovaná gov-design-system** (dist v working tree) ale renderuje interní X jako `disabled: this.blockClose` (p-c99cc786.entry.js) → X má `disabled=""`, vnitřní button `aria-disabled="true"` a gov CSS `gov-button[disabled] .element { pointer-events:none }` → klik nikdy neprojde, `gov-close` se nevyemituje. Původní premisa už neplatí.
3. Nová gov sémantika při `blockClose=false`: X i Escape → `hideDialog()` (self-close, `open=false`) **+** `govClose.emit()`. Emit není cancelable-aware — `preventDefault` v našem handleru self-close nezruší (a nemusí — viz řešení).
4. Backdrop: `block-backdrop-close="true"` je samostatný atribut — **zůstává** (pravidlo „modal se zavírá jen křížkem, žádný backdrop-close" platí dál).
5. Escape dnes zavírá modal náš `handleDocumentOverlayKeydown` (bootstrap) → s `blockClose=false` se přidá i gov self-close → dvě cesty ke stejnému cíli; `closeModal()` musí být idempotentní vůči už-skrytému dialogu (ověřit — uklízí modal-root obsah + vrací floating-root, nezávisle na `open` stavu).

## Řešení

1. Odstranit `block-close="true"` z `_ModalLayout.cshtml` a z error dialogu v `modals.js:237`. `block-backdrop-close="true"` ponechat.
2. `handleGovCloseEvent` ponechat (preventDefault je proti nové verzi neškodný; `closeModal()` dělá úklid nad rámec gov `hideDialog` — odstranění obsahu modal-rootu, vrácení `#floating-panel-root`, focus).
3. Ověřit idempotenci `closeModal()` po gov self-close (dialog už `open=false`): musí doběhnout úklid bez chyby a bez vizuálního artefaktu.
4. Escape: potvrzeno userem — smí zavírat (chová se pak konzistentně X ≡ Escape). Dirty-check modalů se netýká (record editor je stránka; komentář v bootstrapu: non-record-editor modaly jsou fire-and-close).

## Dotčené soubory
- `PmTracker.Web/Views/Shared/_ModalLayout.cshtml` — odebrat `block-close`
- `PmTracker.Web/wwwroot/js/modules/modals.js` — error dialog šablona (odebrat `block-close`), případná idempotence `closeModal`
- Testy (Unit source-assertion + E2E)

## Akceptační kritéria
- X zavírá **všechny** modaly (Přidat projektovou roli, Přidat roli v subsystému, Nové jednání, Přidat osobu, AD search, …); po zavření je modal-root prázdný a floating-root vrácený.
- Klik na backdrop **nezavírá** (regrese memory pravidla — drag-select z inputu ven nesmí zavřít).
- Escape zavírá modal (jedno zavření, žádná chyba v konzoli z dvojité close cesty).
- Zrušit tlačítka fungují beze změny.

## Testy
- Unit source-assertion: `_ModalLayout` neobsahuje `block-close`; obsahuje `block-backdrop-close`.
- E2E: otevřít modal Přidat projektovou roli → klik na `.gov-dialog__close` → modal pryč; backdrop klik → modal zůstává.

## Mimo scope
Dirty-check pro modaly (neexistoval, nezavádí se); gov-design-system upgrade sám o sobě.
