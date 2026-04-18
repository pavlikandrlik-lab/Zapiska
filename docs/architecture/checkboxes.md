# `pm-checkbox`

Thin wrapper nad `<gov-form-checkbox>`. Použití jako samostatné zaškrtávátko — pro skupinu souvisejících voleb použij `pm-radio-group`.

## Použití

```razor
<pm-checkbox name="souhlas" label="Souhlasím s podmínkami" value="1" />
<pm-checkbox name="aktivni" label="Aktivní" checked="true" />
<pm-checkbox name="zakazany" label="Zakázáno" disabled="true" />
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Name` | `string` | HTML name. |
| `Label` | `string` | Popisek vedle checkboxu. |
| `Value` | `string` | Hodnota odesílaná pokud checked (default "true"). |
| `Checked` | `bool` | Defaultní stav. |
| `Disabled` | `bool` | Zakázáno. |
| `Size` | `PmComponentSize` | s/m/l. |

## Viz také
- [`pm-radio`](./radios.md) — pro výběr 1-z-N
- [`pm-switch`](./switches.md) — alternativa pro on/off stavy
