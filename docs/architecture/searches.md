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
| `AriaLabel` | `string?` | `null` (při renderu fallback na `Placeholder`, pak `"Hledat"`) | Atribut `aria-label`. |
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

- Všechny user-supplied hodnoty (`Placeholder`, `Value`, `Name`, `AriaLabel`) jsou HTML-encodované pro ochranu před XSS při manuálním skládání atributů.
- **Nepoužívat pro person-picker** — pattern `data-person-picker-input` vyžaduje nativní `<input type="search">` referenci (JS čte `.value`, poslouchá `input` event). Gov-form-input by to rozbilo.
- **Nepoužívat pro table-tools filter** — pattern `data-table-tools-search-input` (Osoby/Index, Projekty/_ProjectTeamTab) kontroluje `instanceof HTMLInputElement` v `tableTools.js:199`. Migrace vyžaduje refaktor toho JS handleru, což je mimo scope 2D.

## Viz také
- [pm-button](./buttons.md)
- [pm-field](./fields.md)
