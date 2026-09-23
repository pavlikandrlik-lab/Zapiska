# Import ze souboru JSON

**Návrh k odsouhlasení.**

## Účel

Většina číselníků dnes existuje jen v PDF směrnicích a na papíře. Zadavatel je hodlá
do aplikace dostat tak, že **nechá jazykový model přepsat PDF do předem daného tvaru JSON**
a ten pak naimportuje. Typicky číselník rozpočtových položek ministerstva financí a další.

Z toho plynou požadavky na formát:

- **musí být předem definovaný a stabilní** — jinak nelze zadat, do čeho se má přepisovat,
- **musí být soběstačný** — nese i definici struktury, ne jen hodnoty, aby šlo importem
  založit číselník, který v aplikaci ještě není,
- **musí být čitelný pro člověka i pro jazykový model** — žádné zkratky a zanořování navíc,
- **musí být striktně ověřitelný** — chyba v přepisu se musí najít při importu, ne až v provozu.

## Tvar

Záměrně zrcadlí tvar výdeje rozhraní. Co aplikace vydá, jde beze změny naimportovat zpátky.

```json
{
  "formatVerze": "1.0",
  "ciselnik": {
    "kod": "rozpoctove-polozky",
    "nazev": "Rozpočtová skladba — položky",
    "popis": "Položky rozpočtové skladby dle vyhlášky.",
    "zdrojUdaju": "Vyhláška MF č. …/…, příloha č. …",
    "hierarchicky": true,
    "atributy": [
      { "kod": "cisloPolozky", "nazev": "Číslo položky", "typ": "text",  "povinny": true },
      { "kod": "druh",         "nazev": "Druh",          "typ": "text",  "povinny": false },
      { "kod": "castka",       "nazev": "Částka",        "typ": "cislo", "povinny": false }
    ],
    "vazby": [
      { "kod": "trida", "nazev": "Třída", "cilovyCiselnik": "rozpoctove-tridy", "povinna": false }
    ]
  },
  "polozky": [
    {
      "kod": "5011",
      "nazev": "Platy zaměstnanců v pracovním poměru",
      "nadrazenyKod": "501",
      "platnostOd": "2026-01-01",
      "platnostDo": null,
      "atributy": { "cisloPolozky": "5011", "druh": "běžný výdaj" },
      "vazby": { "trida": "5" }
    }
  ]
}
```

### Pravidla

| Pravidlo | Důvod |
|---|---|
| `kod` položky je jedinečný v rámci číselníku | Je to klíč identity pro porovnání i pro vazby |
| `nadrazenyKod` odkazuje na `kod` v témž souboru nebo v číselníku | Hierarchie |
| Ve `vazby` se uvádí **kód cílové položky**, ne vnořený objekt | Soubor zůstane čitelný a přepisovatelný |
| Cílový číselník vazby musí existovat před importem | Jinak nelze ověřit, že odkaz někam vede |
| `platnostDo: null` znamená neomezeně | |
| Podporované typy atributů: `text`, `cislo`, `datum`, `ano_ne`, `vycet` | Malá uzavřená množina — víc typů znamená víc způsobů, jak se v přepisu splést |

## Import není samostatná funkce — je to druhý vstup do editace

Rozhodnutí E2. Nahraný soubor **nejde rovnou do databáze**. Projde tímtéž tokem
jako ruční hromadná editace:

```
vstup                          společný tok
──────────────────────    →    ──────────────────────────────────────────
ruční editace v tabulce        detekce změn proti publikovanému stavu
nahrání souboru JSON      →    promítnutí změn do tabulky, se zvýrazněním
                               volitelný seznam změn ke kontrole
                               Uložit změny  /  Publikovat novou verzi
```

Uživatel tedy po nahrání souboru **vidí svůj číselník v tabulce se zvýrazněnými změnami**
a může je ještě ručně upravit, než uloží. Seznam změn si může otevřít pro přehled,
ale **není podmínkou uložení**.

Přínos: import a hromadná editace nejsou dvě paralelní cesty v kódu ani dvě obrazovky
s vlastní logikou. Je to jeden tok se dvěma vstupy.

> **Import se týká jen ručně spravovaných číselníků.** Externí číselník bere data
> výhradně od svého konektoru (rozhodnutí E3).

## Režimy importu

| Režim | Co udělá |
|---|---|
| **Založení** | Číselník neexistuje. Vznikne včetně definice. |
| **Doplnění** | Přidá nové položky, existující nechá být. |
| **Sesouhlasení** | Porovná soubor s publikovaným stavem. Položky, které v souboru nejsou, se **vyřadí ukončením platnosti — nikdy nesmažou**. |

Ani jeden režim sám o sobě nic nepublikuje. Vždy vzniknou **rozpracované změny**
a o publikování rozhoduje člověk.

## Ověření před uložením

Import je **buď celý, nebo vůbec**. Částečně naimportovaný číselník je horší než žádný.

Aplikace nejprve celý soubor ověří a teprve pak ukládá. Ověřuje:

1. tvar souboru proti schématu importního formátu,
2. hodnoty proti definici číselníku — typy, povinnost, přípustné hodnoty výčtů,
3. jedinečnost kódů,
4. že každý `nadrazenyKod` někam vede a nevzniká cyklus,
5. že každá vazba míří na existující položku existujícího číselníku.

Výsledkem neúspěchu je **seznam všech nalezených chyb s uvedením položky**, ne první chyba
a konec. Kdo přepisuje 1000 řádků, potřebuje vidět všechny problémy najednou.

## Náhled před potvrzením

Před uložením aplikace ukáže, **co se stane**: kolik položek přibude, kolik se změní
a u kterých atributů, kolik se vyřadí. Teprve po potvrzení vzniká verze.

U číselníku o tisíci hodnotách je tohle jediná obrana proti tichému rozbití dat
překlepem v přepisu.
