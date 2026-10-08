# pm-button

Thin-wrapper nad `<gov-button>`. Jeden bod změny mapování při upgrade gov DS.

## Použití

```razor
<pm-button variant="Primary" size="Medium" icon="save">Uložit</pm-button>
<pm-button variant="Destructive" native-type="submit">Smazat</pm-button>
<pm-button variant="Ghost" href="/Projekty">Zpět na seznam</pm-button>
```

## Atributy

| Atribut | Typ | Default | Popis |
|---|---|---|---|
| `variant` | Primary/Secondary/Destructive/Ghost/GhostOutlined | Secondary | Semantická varianta |
| `size` | Small/Medium/Large | Medium | Velikost |
| `icon` | string | — | Jméno ikony ze sady gov |
| `icon-position` | start/end | start | Pozice ikony |
| `native-type` | button/submit/reset | button | HTML type atribut |
| `disabled` | bool | false | Zakázané |
| `href` | string | — | Pokud je, renderuje jako odkaz |

## Mapování na gov-button

| variant | gov color | gov type |
|---|---|---|
| Primary | primary | solid |
| Secondary | primary | outlined |
| Destructive | error | solid |
| Ghost | neutral | base |
| GhostOutlined | neutral | outlined |

**Historie Ghost variant:**
- do 2026-04-19 odpoledne: Ghost = `(neutral, base)` — tiché pozadí bez borderu.
- 2026-04-19 odpoledne: dočasně přepnuto na `(neutral, outlined)` kvůli reportované nízké viditelnosti.
- 2026-04-19 večer: **vráceno zpět na `(neutral, base)`**. Outlined Ghost kolidoval vizuálně se Secondary a rozmazával hierarchii. Kde je potřeba výraznosti, view přešly na `variant="Secondary"` (Profil reset volby, Rozpad harmonogramu, Navrhnout termín a harmonogram, Zamítnout návrh). Ghost zůstává vyhrazený pro decorative/tichou akci: Zpět (PageHeader), Zobrazit více (dashboard panely), filter chips, admin sekce.

**GhostOutlined (2026-10-08):** uživatel chtěl stránkování vyjádření (Další / Předchozí /
Zobrazit vše) výraznější, ale dál neutrální — ne modré jako Secondary. Globální Ghost se
kvůli zkušenosti z 2026-04-19 nemění; `GhostOutlined` je samostatná varianta s kombinací DS
gov `neutral` + `outlined` (tokeny `--button-outlined-neutral*`), použitá jen tam, kde je
potřeba.

## Kdy použít kterou variantu

- **Primary** — hlavní akce formu (Uložit, Potvrdit, Odeslat). Max jeden primary per obrazovka.
- **Secondary** — běžné akce (Zrušit, Zpět, Přidat). Několik na stránce je OK.
- **Destructive** — smazání, ztráta dat. Vždy s potvrzením.
- **Ghost** — tiché akce v tabulkách, inline (Rozbalit, Více).
- **GhostOutlined** — tichá akce, která má být vidět: načítání dalších položek v seznamu
  (stránkování vyjádření).

## Přechod z .btn

| Starý markup | Nový markup |
|---|---|
| `<button class="btn primary">Uložit</button>` | `<pm-button variant="Primary" native-type="submit">Uložit</pm-button>` |
| `<button class="btn">Zrušit</button>` | `<pm-button variant="Secondary">Zrušit</pm-button>` |
| `<button class="btn danger">Smazat</button>` | `<pm-button variant="Destructive">Smazat</pm-button>` |
| `<button class="btn ghost small">Více</button>` | `<pm-button variant="Ghost" size="Small">Více</pm-button>` |
| `<a class="btn" href="...">Zpět</a>` | `<pm-button variant="Secondary" href="...">Zpět</pm-button>` |

## JS a eventy

`<gov-button>` emituje custom event `gov-click`. Aplikace má v `eventBus.js` adaptér,
který jej přemapuje na nativní `click`. Existující `addEventListener("click", ...)`
posluchače fungují beze změny.
