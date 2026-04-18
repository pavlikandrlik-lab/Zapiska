# `pm-tabs` + `pm-tabs-item`

Thin wrappery nad `<gov-tabs>` a `<gov-tabs-item>`. Přepínání aktivní záložky řídí gov Web Component JS interně.

## Použití

```razor
<pm-tabs>
    <pm-tabs-item title="Přehled" active="true">
        <p>Obsah přehledu</p>
    </pm-tabs-item>
    <pm-tabs-item title="Jednání">
        <p>Seznam jednání</p>
    </pm-tabs-item>
    <pm-tabs-item title="Dokumenty">
        <p>Dokumenty projektu</p>
    </pm-tabs-item>
</pm-tabs>
```

## Typy

```razor
<pm-tabs type="Chip">
    <pm-tabs-item title="Vše" active="true"></pm-tabs-item>
    <pm-tabs-item title="Aktivní"></pm-tabs-item>
</pm-tabs>

<pm-tabs orientation="Vertical">
    <pm-tabs-item title="Sekce 1"></pm-tabs-item>
    <pm-tabs-item title="Sekce 2"></pm-tabs-item>
</pm-tabs>
```

## API

### pm-tabs

| Property | Typ | Popis |
|---|---|---|
| `Orientation` | `PmTabsOrientation` | Horizontal (výchozí) / Vertical. |
| `Type` | `PmTabsType` | Default (podtržené) / Chip (pilulky). |
| `Size` | `PmComponentSize` | s/m/l. |

### pm-tabs-item

| Property | Typ | Popis |
|---|---|---|
| `Title` | `string` | Text v hlavičce záložky. |
| `Active` | `bool` | Defaultně vybraná záložka. |

## Integrace s JS

gov-tabs JS handler zachytává kliky na hlavičku a přepíná `active` atribut. Aplikační JS může poslouchat `gov-change` event z gov-tabs pro custom logiku:

```javascript
document.querySelector('gov-tabs').addEventListener('gov-change', (e) => {
    console.log('Vybrána záložka index:', e.detail.index);
});
```

## Viz také
- [pm-card](./cards.md)
