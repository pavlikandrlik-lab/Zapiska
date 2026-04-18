# pm-icon

Wrapper nad `<gov-icon>`. Automaticky dekorativní (aria-hidden); aria-label přepíná
na funkční.

## Použití

```razor
<pm-icon name="check" aria-label="Hotovo" />
<pm-icon name="arrow-right" />
<pm-icon name="x" slot="icon-end" aria-label="Zavřít" />
```

## Atributy

| Atribut | Typ | Default |
|---|---|---|
| `name` | string | — |
| `slot` | string | — (použij `icon-start`/`icon-end` pro sloty tlačítek) |
| `aria-label` | string | — (pokud nastaveno, aria-hidden se neaplikuje) |

## Dostupné ikony

Gov-design-system 4.2.9 poskytuje sadu `type="components"`. Názvy (výběr):
- `check`, `x`, `arrow-left`, `arrow-right`, `arrow-up`, `arrow-down`
- `search`, `pencil`, `trash`, `plus`, `minus`
- `info`, `warning`, `error`

Úplný seznam: https://designsystem.gov.cz/komponenty/ikony.html
