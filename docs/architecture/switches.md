# `pm-switch`

Thin wrapper nad `<gov-form-switch>` — on/off přepínač (alternativa ke checkboxu pro stavy typu "Zapnuto/Vypnuto", "Aktivní/Neaktivní").

## Použití

```razor
<pm-switch name="notifikace" label="Zasílat notifikace" />
<pm-switch name="aktivni" label="Aktivní" checked="true" />
<pm-switch name="zamek" label="Uzamčeno" disabled="true" />
```

## Kdy použít switch vs. checkbox

- **Switch:** okamžitá změna stavu (toggle settings), binary on/off
- **Checkbox:** volba v rámci formuláře, která se odesílá dávkově

## Rozdíl oproti gov-theme-switch

`<gov-theme-switch>` je speciální komponenta pro přepínání light/dark/auto motivu. Má vlastní JS handler v `wwwroot/js/modules/theme.js`. **Není** obalena do `pm-*`, používá se přímo.

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | HTML name. |
| `Label` | `string` | Popisek. |
| `Value` | `string` | Hodnota pokud checked (default "true"). |
| `Checked` | `bool` | Defaultní stav. |
| `Disabled` | `bool` | Zakázáno. |
| `Size` | `PmComponentSize` | s/m/l. |

## Viz také
- [`pm-checkbox`](./checkboxes.md)
