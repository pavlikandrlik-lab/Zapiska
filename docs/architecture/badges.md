# pm-badge

Wrapper nad `<gov-tag>`.

## Použití

```razor
<pm-badge variant="Success" size="Small">Hotovo</pm-badge>
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `variant` | Neutral/Primary/Success/Warning/Error | Neutral |
| `size` | Small/Medium/Large | Medium |

## Přechod

| Starý | Nový |
|---|---|
| `<span class="badge badge-success">OK</span>` | `<pm-badge variant="Success">OK</pm-badge>` |
| `<span class="badge badge-warning">Pozor</span>` | `<pm-badge variant="Warning">Pozor</pm-badge>` |
