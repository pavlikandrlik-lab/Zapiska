# Fáze 2E worklog — migrace modálního systému na gov-dialog

**Datum dokončení:** 2026-04-20
**Branch:** `codex/senior-refactor-fase-1`
**Commits v 2E:**

| SHA | Popis |
|---|---|
| `c996cbe` | Task 1: _ModalFormActions submit na pm-button |
| `24e7cf7` | Task 1 review fixes (using + DisableSubmit) |
| `a3c6229` | Task 2: _ModalLayout na gov-dialog |
| `217c786` | Task 2 review fix (slot) |
| `a3bba6b` | Task 3: JS adaptace (modals.js, ui.js, bootstrap.js, _Layout) |
| `ae2f0d7` | Task 3 follow-up: ajax.js modal detection |
| `fc0f3a1` | Task 3 review fixes (block-close, activateInsertedGovDialog) |
| `69e6e99` | Task 4: E2E smoke (Skip) + manual checklist |
| `63557d0` | Task 5: legacy CSS cleanup + gov-close event |
| `<this>` | Task 6: docs + close 2E |

## Metriky

- Unit testy: **345 → 346** (+1 new Bootstrap_ShouldListenForGovCloseEvent)
- E2E testy: +3 Skip testy (ModalGovDialogSmokeTests)
- Soubory změněné v 2E: **~12** (views + modules + bundle + CSS + tests + docs)
- CSS: **-90 řádků** (legacy `.modal-overlay`, `.modal--*`, `.modal-close*`, `.modal-header`, `.modal-floating-root`) + scope na `gov-dialog[data-modal-container]`
- 18 modal views: beze změny (používají `Layout = "_ModalLayout"`)

## Testing

### Provedeno v této session

- Unit tests: 346/346 pass
- Playwright harness (static HTML + gov-design-system JS/CSS): 5/5 pass
  - Variant attributes correct (default, wide, record-editor, overflow)
  - `block-close="true"` skutečně zabránil self-close na Esc (screenshot verifies)
  - Close via `[data-modal-close]` funguje (DOM clear)

### Deferred na Citrix manual smoke

18 modal views checklist v `docs/superpowers/logs/2026-04-19-faze-2e-manual-smoke.md`:
- Otevření, markup, backdrop, Esc, X close, focus trap, floating pickery
- EditZaznamModal (nejkomplexnější — 3 taby, schedule, quill, pickers)
- Destructive button color (DeleteRecordModal)

## Architektonická rozhodnutí

1. **Adapter approach (ne full rewrite):** Views se nedotýkají, celá 2E je transparentní migrace přes `_ModalLayout.cshtml` + JS adaptace.
2. **`block-close="true"` + `block-backdrop-close="true"`:** gov-dialog je purely presentational, app plně vlastní close flow (Esc, backdrop, X → dirty-check).
3. **Vestavěný X gov-dialogu:** emituje `gov-close` event, bootstrap.js ho catchuje a přesměruje do `requestRecordEditorModalClose`. Vlastní `<button class="modal-close">` odstraněn (Task 5).
4. **Floating pickery do globálního `#floating-panel-root`:** shadow DOM gov-dialog není vhodný host.
5. **`ModalFormActionsViewModel.SubmitVariant: PmButtonVariant`:** enum nahradil volný CSS string (2D leftover).

## Odloženo na další fáze

- Sdílený filter panel Záznamy + Harmonogram — user request; separátní mini-task před Fází 3
- Runtime smoke 18 views na Citrix — user provede po deployi
- Potenciální odstranění `pm-dialog` wrapperu (pokud se nikde nepoužívá kromě StyleGuide) — Fáze 3 cleanup

## Next up

**Fáze 3** — architektonický refactor god-files + přehledná struktura:
- Rozbití souborů nad 500 LOC (`site.bundle.js` 9500+, `recordEditor.js` 1800+, `schedule.js` 1600+, `bootstrap.js` 600+)
- Jasná odpovědnost per module/file
- God-controllery v backendu (Projekty, Zaznamy) rozdělit podle feature boundaries
- Odstranit "magic" DOM selectors + přejít na strukturované API typy
