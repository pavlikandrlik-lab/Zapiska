# `pm-textarea`

Thin wrapper nad `<gov-form-control>` + `<gov-form-input>` s vnitřním `<textarea slot="element">` — gov-design nemá samostatnou textarea komponentu.

## Použití

```razor
<pm-textarea name="poznamka" label="Poznámka" rows="4" placeholder="Zadejte poznámku…" help="Max. 500 znaků" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | HTML name. |
| `Label` | `string` | Popisek. |
| `Value` | `string?` | Předvyplněná hodnota. |
| `Placeholder` | `string?` | Placeholder text. |
| `Rows` | `int` | Počet viditelných řádků (default 3). |
| `Required` | `bool` | Povinné pole. |
| `Disabled` | `bool` | Zakázáno. |
| `Help` | `string?` | Nápověda. |
| `Error` | `string?` | Chybové hlášení. |
| `Size` | `PmComponentSize` | s/m/l. |

## Rozdíl oproti pm-field

`pm-field` má `input-type="text"` — jeden řádek. `pm-textarea` je víceřádkový. Jinak API a chování identické (label, help, error, size).

## Viz také
- [`pm-field`](./fields.md) — jednořádkový text input
