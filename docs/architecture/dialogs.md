# `pm-dialog`

Thin wrapper nad `<gov-dialog>`. Modální dialog s titulkem, tělem a volitelnými akcemi.

## Použití

```razor
<pm-button variant="Primary" onclick="document.getElementById('confirm-delete').show()">Smazat</pm-button>

<pm-dialog id="confirm-delete" title="Potvrdit smazání">
    <p>Opravdu chcete smazat tento záznam?</p>
    <pm-button variant="Destructive" onclick="document.getElementById('confirm-delete').close()">Smazat</pm-button>
    <pm-button variant="Secondary" onclick="document.getElementById('confirm-delete').close()">Zrušit</pm-button>
</pm-dialog>
```

## API

| Property | Typ | Popis |
|---|---|---|
| `Id` | `string` | HTML id (nutné pro otevření/zavření z jiných elementů). |
| `Title` | `string` | Titulek (renderuje se jako `<h3 slot="title">`). |
| `Open` | `bool` | Defaultně otevřený (přidá `open="true"`). |

Dětský obsah jde do default slotu (tělo dialogu).

## Otevření/zavření z JS

gov-dialog web komponenta má metody `.show()` a `.close()`:

```javascript
const dlg = document.getElementById('confirm-delete');
dlg.show();    // otevřít
dlg.close();   // zavřít
```

## Mapování pm → gov

| pm atribut | gov atribut / slot |
|---|---|
| `id` | HTML id |
| `title` | `<h3 slot="title">` |
| `open` | `open="true"` |
| child content | default slot |

## Viz také
- [pm-button](./buttons.md)
