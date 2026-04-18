# PM Tracker — architektonická dokumentace

Stavební pravidla aplikace. Závazné pro všechny vývojáře interní i externí.

## Komponentní vrstva (pm-*)

Aplikace používá thin-wrapper TagHelpery nad gov-design-system Web Components.
Jeden wrapper = jeden bod změny při upgradu gov DS verze.

### Fáze 1 (hotovo)
- [pm-button — tlačítka](buttons.md)
- [pm-alert — alerty](alerts.md)
- [pm-badge — štítky](badges.md)
- [pm-field — formulářová pole (text, email, …)](fields.md)
- [pm-icon — ikony](icons.md)

### Fáze 2A — formulářové primitivy (hotovo 2026-04-19)
- [pm-select — výběr z číselníku](selects.md)
- [pm-textarea — víceřádkový text](textareas.md)
- [pm-checkbox — zaškrtávátko](checkboxes.md)
- [pm-radio + pm-radio-group — výběr 1-z-N](radios.md)
- [pm-switch — on/off přepínač](switches.md)

## Infrastruktura

- [Design tokens (CSS proměnné)](tokens.md)
- [JS moduly a event bus](js-modules.md)
- [Backend layering — thin controller, services, repositories](backend-layering.md)
- [Upgrade gov-design-system verze](upgrade-gov-ds.md)

## Living style guide

`/StyleGuide` — běží v aplikaci. Ukazuje každou pm-* komponentu ve všech variantách.
