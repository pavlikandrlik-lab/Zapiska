# `pm-tooltip`

Thin wrapper nad `<gov-tooltip>` + `<gov-tooltip-content>`. Child content = trigger, `Text` property = obsah bubliny.

## Použití

```razor
<pm-tooltip text="Klikněte pro uložení záznamu">
    <pm-button variant="Primary">Uložit</pm-button>
</pm-tooltip>

<pm-tooltip text="Stav projektu">
    <pm-badge variant="Success">Aktivní</pm-badge>
</pm-tooltip>
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Text` | `string` | Obsah bubliny. |

Dětský obsah je trigger (prvek, na který uživatel najede myší nebo fokusem).

## Mapování pm → gov

| pm atribut | gov prvek |
|---|---|
| child content | trigger (před `gov-tooltip-content`) |
| `text` | `<gov-tooltip-content>` |

## Viz také
- [pm-dialog](./dialogs.md) — pro delší obsah / modální
