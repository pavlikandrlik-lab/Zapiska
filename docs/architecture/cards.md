# `pm-card`

Thin wrapper nad `<gov-card>`. Obsahuje `headline` slot a default body slot.

## Použití

```razor
<pm-card headline="Projekt Alfa">
    <p>Popis projektu…</p>
    <pm-badge variant="Success">Aktivní</pm-badge>
</pm-card>
```

## Klikací karta (celá karta jako odkaz)

```razor
<pm-card headline="Detail projektu" href="/projekty/123">
    <p>Klikněte pro detail</p>
</pm-card>
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Headline` | `string` | Titulek karty (renderuje se jako `<h3 slot="headline">`). |
| `Href` | `string?` | Pokud nastaveno, celá karta je klikací. |

Dětský obsah jde do default slotu (tělo karty).

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `headline` | `<h3 slot="headline">` |
| `href` | `<gov-card href>` |
| child content | default slot (tělo) |

## Viz také
- [`pm-tabs`](./tabs.md)
