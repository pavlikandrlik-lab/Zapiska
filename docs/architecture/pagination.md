# `pm-pagination`

Thin wrapper nad `<gov-pagination>`. Renderuje stránkovací lištu s odkazy na jednotlivé stránky.

## Použití

```razor
<pm-pagination current="@Model.CurrentPage"
               total-pages="@Model.TotalPages"
               url-template="?page={0}&q=@Model.Query" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Current` | `int` | Aktuální stránka (1-based). |
| `TotalPages` | `int` | Celkový počet stránek. |
| `UrlTemplate` | `string` | URL šablona s `{0}` pro číslo stránky. |
| `Size` | `PmComponentSize` | s/m/l. |

## Integrace se server-side pagingem

Server generuje `CurrentPage` a `TotalPages` (např. z EF Core `Skip/Take`). `UrlTemplate` obsahuje všechny ostatní parametry dotazu:

```csharp
// Controller
ViewBag.UrlTemplate = $"?page={{0}}&q={Uri.EscapeDataString(query ?? "")}";
```

```razor
<pm-pagination current="@currentPage" total-pages="@totalPages" url-template="@ViewBag.UrlTemplate" />
```

gov-pagination automaticky generuje `<a>` odkazy s nahrazeným `{0}` a označuje aktuální stránku.

## Mapování pm → gov

| pm atribut | gov atribut |
|---|---|
| `current` | `current` |
| `total-pages` | `pages` |
| `url-template` | `href-template` |
| `size` | `size` |

## Viz také
- [pm-link](./links.md)
