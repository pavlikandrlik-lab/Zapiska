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

`gov-icon type="components"` bere SVG z `wwwroot/assets/icons/components/{name}.svg`
(`window.GOV_DS_CONFIG.iconsPath` v `_Layout.cshtml`). Strom je aplikační: Bootstrap Icons
1.11.3, které stahujeme sami, + ikony sady DS gov 4.7.0, které jsme neměli (odchylka č. 3
v `docs/known-issues/ds-fis-odchylky.md`). Novou ikonu přidej jako SVG do této složky —
`GovAssets470Tests.KazdaIkonaPouzitaVAplikaci_Existuje` selže, když chybí.
