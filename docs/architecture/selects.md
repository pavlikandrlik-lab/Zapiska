# `pm-select`

Thin wrapper nad `<gov-form-control>` + `<gov-form-select>` z gov-design-system.

## Použití

```razor
@{
    var options = new List<PmSelectOption>
    {
        new("nova", "Nová", false, false),
        new("rozpracovana", "Rozpracovaná", true, false),
        new("uzavrena", "Uzavřená", false, false)
    };
}

<pm-select name="stav" label="Stav záznamu" options="options" required="true" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | Název formulářového pole (HTML `name`). |
| `Label` | `string` | Popisek nad selectem. |
| `Options` | `IList<PmSelectOption>` | Seznam voleb. |
| `Required` | `bool` | Povinné pole (přidá hvězdičku a `required`). |
| `Disabled` | `bool` | Zakázaný select. |
| `Help` | `string?` | Nápovědný text pod selectem. |
| `Error` | `string?` | Chybové hlášení (přepíše Help, přidá `invalid`). |
| `Size` | `PmComponentSize` | Small / Medium (výchozí) / Large. |

`PmSelectOption` je record: `(Value, Text, selected, disabled)`.

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `name` | `<gov-form-select name>` |
| `label` | `<gov-form-label slot="top">` |
| `options` | `<option>` potomci `<gov-form-select>` |
| `required` | `required` + asterisk marker |
| `size` | `size` (s/m/l) |
| `help` | `<gov-form-message slot="bottom">` |
| `error` | `<gov-form-message slot="bottom" variant="error">` + `invalid` |

## Viz také
- [`pm-field`](./fields.md) — textové inputy
- [`pm-textarea`](./textareas.md) — víceřádkový text
