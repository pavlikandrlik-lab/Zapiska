# Specifikace — modal layout rules (modal-overflow-policy)

**Datum:** 2026-04-20
**Autor:** Pavel Andrlík (user request 2026-04-20 ranní inbox, úprava #9/2)

## Pravidlo

**Modaly (`<gov-dialog data-modal-container>`) NEJSOU scrollovatelné v default stavu.** Content uvnitř modalu se musí vejít do viewportu.

Pokud je content delší, má dvě možnosti:
1. **Refactor layoutu** — některý interní region má vlastní `overflow-y: auto` (např. seznam výsledků uvnitř modalu s fixed search inputem).
2. **Opt-in flag** `data-modal-scrollable="true"` — modal jako celek scrollable. **Pouze pro legitimní výjimky.**

## Legitimní výjimky (whitelist)

| Modal | View | Důvod |
|---|---|---|
| Record-editor form | `_EditZaznamForm.cshtml`, variant `record-editor` | Form obsahuje metadata + harmonogram + vyjádření + externí vazby — může překročit výšku viewportu i na plném 1080p monitoru. |

Opt-in flag je nastavován automaticky přes `_ModalLayout.cshtml` když `ModalVariant == "record-editor"`.

## Zakázané vzory

- Přidávání `data-modal-scrollable="true"` na nové modaly bez diskuse / aktualizace tohoto whitelistu
- Použití `overflow-y: auto` na `.modal-content` v custom CSS uvnitř views
- Scroll jako quick-fix pro content overflow — místo toho refactor layoutu (sticky header/footer + internal scroll region)

## CSS implementace

```css
/* Default — modal NEscrollable */
gov-dialog[data-modal-container] .modal-content {
    padding: 16px;
    overflow: hidden;
}

/* Opt-in — pouze pro whitelisted výjimky */
gov-dialog[data-modal-container][data-modal-scrollable="true"] .modal-content {
    overflow-y: auto;
    max-height: calc(92vh - 32px);
}
```

## Razor implementace opt-in

V `_ModalLayout.cshtml` se flag nastavuje podmíněně:

```razor
@Html.Raw(string.Equals(normalizedVariant, "record-editor") ? "data-modal-scrollable=\"true\"" : "")
```

## Tests

Architecture test `ModalLayoutRulesTests` (`PmTracker.Tests.Unit/Layout/ModalLayoutRulesTests.cs`):
- `SiteCss_ShouldDefaultModalOverflowToHidden` — CSS default overflow:hidden
- `SiteCss_ShouldAllowOptInScrollable` — CSS opt-in scroll rule existuje
- `RecordEditorModal_ShouldBeOnlyOptInScrollable` — `_ModalLayout.cshtml` nastavuje flag pro record-editor variant
- `SpecDocument_ShouldDocumentOverflowRules` — tento dokument existuje s klíčovými termíny
