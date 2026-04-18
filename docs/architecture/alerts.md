# pm-alert

Wrapper nad `<gov-message>`.

## Použití

```razor
<pm-alert variant="Error">Něco se pokazilo.</pm-alert>
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `variant` | Info/Success/Warning/Error | Info |

## Mapování

| variant | gov color |
|---|---|
| Info | primary |
| Success | success |
| Warning | warning |
| Error | error |

## Přechod

| Starý | Nový |
|---|---|
| `<div class="alert alert-error">X</div>` | `<pm-alert variant="Error">X</pm-alert>` |
| `<div class="alert warning">X</div>` | `<pm-alert variant="Warning">X</pm-alert>` |
