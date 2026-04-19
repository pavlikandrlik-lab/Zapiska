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

## ⚠️ Omezení inline použití

`<pm-loading>` renderuje `<gov-loading>` s interním markupem `position: fixed; z-index: 101`
zaměřeným na střed viewportu — je to **runtime blocking state**, ne inline prvek. Pokud
ho vložíte do statické stránky (např. StyleGuide), backdrop pokryje celou stránku
a zablokuje UI (user report 2026-04-19: "točí se tam Probíhá import").

**Použití pouze za runtime podmínkou** (např. `data-loading-target` přepínač ve
`loadRecordDetail`, modalní submit). Nevkládejte jako showcase / demo.

## Viz také
- [pm-skeleton](./skeletons.md)
