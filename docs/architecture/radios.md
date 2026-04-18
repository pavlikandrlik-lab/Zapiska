# `pm-radio` + `pm-radio-group`

Thin wrappery nad `<gov-form-radio>` a `<gov-form-radio-group>`.

## Použití

```razor
<pm-radio-group legend="Priorita" orientation="Horizontal">
    <pm-radio name="priorita" value="nizka" label="Nízká" />
    <pm-radio name="priorita" value="normalni" label="Normální" checked="true" />
    <pm-radio name="priorita" value="vysoka" label="Vysoká" />
</pm-radio-group>
```

## API

### pm-radio-group

| Property | Typ | Popis |
|---|---|---|
| `Legend` | `string` | Popisek skupiny (slot="top"). |
| `Orientation` | `PmRadioOrientation` | Vertical (výchozí) / Horizontal. |

### pm-radio

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | Společný name v rámci skupiny. |
| `Value` | `string` | Odesílaná hodnota. |
| `Label` | `string` | Popisek. |
| `Checked` | `bool` | Defaultní stav. |
| `Disabled` | `bool` | Zakázáno. |
| `Size` | `PmComponentSize` | s/m/l. |

## Viz také
- [`pm-checkbox`](./checkboxes.md)
