# `pm-loading`

Thin wrapper nad `<gov-loading>`. Animovaný spinner, volitelně s popiskem.

## Použití

```razor
<pm-loading label="Načítám data…" size="Medium" />

<!-- jen spinner -->
<pm-loading size="Small" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Label` | `string?` | Volitelný text pod/vedle spinneru. |
| `Size` | `PmComponentSize` | s/m/l. |

## Kdy použít loading vs. skeleton

- **Loading**: neurčitá čekací doba (submit, dlouhá operace) — spinner
- **Skeleton**: zobrazujeme strukturu budoucího obsahu — placeholder tvary

## Viz také
- [pm-skeleton](./skeletons.md)
