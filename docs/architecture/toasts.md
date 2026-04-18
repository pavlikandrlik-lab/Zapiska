# `pm-toast`

Thin wrapper nad `<gov-toast>`. Krátká notifikace u okraje obrazovky.

## Použití (deklarativně v Razoru)

```razor
<pm-toast variant="Success" gravity="Top" position="Right">
    Záznam byl uložen.
</pm-toast>
```

## Programové zobrazení (JS)

V praxi se toast vytváří za běhu aplikace:

```javascript
const toast = document.createElement('gov-toast');
toast.setAttribute('color', 'success');
toast.setAttribute('type', 'bold');
toast.setAttribute('gravity', 'top');
toast.setAttribute('position', 'right');
toast.textContent = 'Záznam byl uložen.';
document.body.appendChild(toast);
toast.show();
```

## API

| Property | Typ | Výchozí | Popis |
|---|---|---|---|
| `Variant` | `PmToastVariant` | `Info` | Info/Success/Warning/Error → color. |
| `Gravity` | `PmToastGravity` | `Top` | Svislá pozice. |
| `Position` | `PmToastPosition` | `Right` | Vodorovná pozice. |

> **Pozn.:** `gov-toast` v gov-design-system 4.2.9 nepodporuje atribut `size`.
> Pokud budoucí verze doplní size prop, lze jej přidat zpět.

## Mapování pm → gov

| pm | gov |
|---|---|
| `variant=Info` | `color="primary"` |
| `variant=Success` | `color="success"` |
| `variant=Warning` | `color="warning"` |
| `variant=Error` | `color="error"` |
| `gravity` | `gravity="top\|bottom"` |
| `position` | `position="left\|center\|right"` |

Vždy renderujeme `type="bold"` (filled barva pro kontrast).

## Viz také
- [pm-alert](./alerts.md) — inline oznámení
- [pm-dialog](./dialogs.md) — modální dialog
