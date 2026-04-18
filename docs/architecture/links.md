# `pm-link`

Thin wrapper nad `<gov-link>`. Pro externí URL automaticky přidá `target="_blank"` a `rel="noopener noreferrer"`.

## Použití

```razor
<pm-link href="/projekty">Seznam projektů</pm-link>

<pm-link href="/detail/123" icon="chevron-right">Detail</pm-link>

<pm-link href="https://designsystem.gov.cz" external="true">Design systém</pm-link>
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Href` | `string` | Cíl odkazu. |
| `Icon` | `string?` | Volitelná ikona (`gov-icon name="…"`). |
| `IconPosition` | `string` | `start` nebo `end` (výchozí). |
| `External` | `bool` | Externí odkaz (nový tab + noopener). |
| `Size` | `PmComponentSize` | s/m/l. |

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `href` | `<gov-link href>` |
| `icon` + `icon-position` | `<gov-icon slot="icon-start\|icon-end">` |
| `external` | `target="_blank"` + `rel="noopener noreferrer"` |
| `size` | `size` (s/m/l) |

## Viz také
- [`pm-button`](./buttons.md) — pro odkazy stylované jako tlačítka
