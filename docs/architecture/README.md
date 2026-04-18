# PM Tracker — architektonická dokumentace

Stavební pravidla aplikace. Závazné pro všechny vývojáře interní i externí.

## Komponentní vrstva (pm-*)

Aplikace používá thin-wrapper TagHelpery nad gov-design-system Web Components.
Jeden wrapper = jeden bod změny při upgradu gov DS verze.

- [pm-button — tlačítka](buttons.md)
- [pm-alert — alerty](alerts.md)
- [pm-badge — štítky](badges.md)
- [pm-field — formulářová pole](fields.md)
- [pm-icon — ikony](icons.md)

## Infrastruktura

- [Design tokens (CSS proměnné)](tokens.md)
- [JS moduly a event bus](js-modules.md)
- [Backend layering — thin controller, services, repositories](backend-layering.md)
- [Upgrade gov-design-system verze](upgrade-gov-ds.md)

## Living style guide

`/StyleGuide` — běží v aplikaci. Ukazuje každou pm-* komponentu ve všech variantách.
