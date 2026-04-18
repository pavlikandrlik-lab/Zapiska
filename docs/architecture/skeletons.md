# `pm-skeleton`

Thin wrapper nad `<gov-skeleton>`. Placeholder zobrazovaný během načítání dat.

## Použití

```razor
@if (Model.IsLoading)
{
    <pm-skeleton size="Medium" />
    <pm-skeleton shape="Circle" size="Large" />
}
else
{
    <div>@Model.Content</div>
}
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Shape` | `PmSkeletonShape` | Default (obdélník) / Circle. |
| `Size` | `PmComponentSize` | s/m/l. |

## Kdy použít skeleton vs. loading

- **Skeleton**: obsah načítá se z dat, chceme ukázat strukturu (karty, text, avatar)
- **Loading**: spinner pro neurčitou dobu čekání (submit formuláře, globální akce)

## Viz také
- [pm-loading](./loadings.md)
