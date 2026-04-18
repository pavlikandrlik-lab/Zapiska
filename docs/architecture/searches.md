# `pm-search`

Thin wrapper nad `<gov-form-search>` + vnořený `<gov-form-input type="search" slot="input">`. Volitelně přidá submit tlačítko "Hledat" do slotu `button`.

## Použití

### Jednoduchý search (bez submit tlačítka, JS-driven)

```razor
<pm-search name="q" placeholder="Jméno…" aria-label="Hledaný výraz" />
```

### Search s submit tlačítkem (GET form)

```razor
<form asp-controller="Search" asp-action="Index" method="get">
    <pm-search name="q" value="@Model.Query" placeholder="Hledat…" submit="true" autofocus="true" />
</form>
```

## API

| Property | Typ | Default | Popis |
|---|---|---|---|
| `Name` | `string` | `"q"` | Atribut `name` inputu. |
| `Placeholder` | `string?` | `null` | Atribut `placeholder`. |
| `Value` | `string?` | `null` | Aktuální hodnota. |
| `AriaLabel` | `string?` | `Placeholder` nebo `"Hledat"` | Atribut `aria-label`. |
| `Size` | `PmComponentSize` | `Medium` | Velikost (s/m/l). |
| `Submit` | `bool` | `false` | Přidá `<gov-button slot="button">Hledat</gov-button>`. |
| `Autofocus` | `bool` | `false` | Přidá `autofocus` na input. |

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `name` / `placeholder` / `value` | na `<gov-form-input slot="input">` |
| `size` | `size` na `gov-form-search` i `gov-form-input` |
| `submit="true"` | přidá `<gov-button slot="button">` |
| `autofocus="true"` | atribut na `<gov-form-input>` |

## Poznámky

- Všechny user-supplied hodnoty (`Placeholder`, `Value`, `Name`, `AriaLabel`) jsou encodované přes `HtmlEncoder.Create(UnicodeRanges.All)` — plná XSS ochrana (escapuje `<`, `>`, `"`, `&`, `'`) při zachování diakritiky (é, í, á, ě, …).
- Pro person-picker pattern (`data-person-picker-input`) zachovejte `<input type="search">` beze změny — pm-search se tam nehodí (gov-form-input by rozbil existující JS handler).

## Viz také
- [pm-button](./buttons.md)
- [pm-field](./fields.md)
