# Vnější zdroje a přebírání dat

Pokrývá schopnosti [S3 a S4](03-schopnosti.md).

## Princip

Aplikace si z vnějšího systému přečte data, **porovná je s aktuálním stavem svého číselníku**
a rozdíl uloží jako novou verzi. Zdroj se nikdy nepřepisuje do číselníku „nastvrdo" —
vždy přes porovnání, protože jinak by nešlo zjistit, co se změnilo.

```
zdroj → načtení → mapování na definici číselníku → porovnání s aktuálním stavem
      → žádný rozdíl → konec, nová verze nevzniká
      → rozdíl        → uložení změn + nová verze
```

## Konektor se programuje, mapování se nekonfiguruje

**Každé napojení na zdroj je vlastní kód.** Obecný předpis mapování polí zdroje na atributy
číselníku se nedělá.

Zdůvodnění: tvary odpovědí zdrojů se liší natolik, že obecný mapovací jazyk by byl větší
investice než napsat konektor — a byl by to vlastní malý produkt, který se použije jednou
za čas. Konektor je proti tomu pár desítek řádků čitelného kódu.

### Kde vede hranice

```
konektor zdroje (kód, jeden na zdroj)          sdílené (napsané jednou)
─────────────────────────────────────    →     ────────────────────────────────
přečti zdroj                                   porovnej s publikovaným stavem
rozbal jeho tvar odpovědi                      sestav změny
přihlas se jeho způsobem                       vydej verzi
vrať položky v kanonickém tvaru                zapiš audit
```

Konektor implementuje **jedno rozhraní** a vrací položky v **témž kanonickém tvaru,
jaký má importní soubor JSON** ([06-import-json.md](06-import-json.md)). Všechno za touto
hranicí je společné.

Přidání nového zdroje je tedy: napsat konektor, zaregistrovat ho, vyplnit nastavení.
Nic z toho se nedotýká porovnávání, verzování ani auditu.

## Co se o zdroji eviduje

| Údaj | K čemu |
|---|---|
| Typ konektoru | Který kód zdroj obsluhuje |
| Adresa a přihlašovací údaje | Zdroje mají vlastní autentizaci |
| Cílové číselníky | Kam se data ukládají — jeden běh může plnit více číselníků |
| Klíč identity položky | Podle čeho se pozná, že jde o tutéž položku jako minule |
| Režim spouštění | Ručně, periodicky, na událost |
| Stav posledního běhu | Kdy, s jakým výsledkem, kolik změn |

> **Klíč identity je kritický.** Bez něj se změna kódu nebo názvu položky projeví jako
> „stará zmizela, nová přibyla" a historie se rozpadne. Klíč je zdrojový identifikátor,
> ne název.

## Číselník má právě jednoho vlastníka

Číselník je buď **ručně spravovaný**, nebo **externí**. Hybrid neexistuje (rozhodnutí E3).

U externího číselníku nesmí editor měnit hodnoty ani při zjevné chybě — chyba se opravuje
**u zdroje**. Jinak by příští běh úpravu přepsal zpátky a nikdo by nevěděl proč.

Potřeba doplnit k přebíraným datům vlastní údaj se řeší **samostatným číselníkem navázaným
vazbou**, nikdy zápisem do přebíraných dat.

## Zdroj, který neverzuje

Případ S4 a zároveň případ hlavního ERP systému.

Zdroj vrátí **aktuální stav a nic víc** — nezná pojem verze. Aplikace tedy:

1. načte aktuální stav,
2. porovná s tím, co má uložené,
3. z rozdílu sestaví změny,
4. vydá novou verzi, pokud rozdíl není prázdný.

Tím vznikne verzovaná historie u zdroje, který ji sám nemá.

## Ověřovací zdroj — ERP, rozhraní `export-mcdp`

Reálná odpověď zdroje, na které se návrh ověřuje. Data **nejsou verzovaná** —
jde tedy o případ S4.

```json
{
  "GRestHeader": {
    "ResponseName": "Xrg",
    "ResponseNamespace": "http://www.gordic.cz/xrg/export-mcdp/response/v_1.0.0.0",
    "User": "...", "Password": "...", "PasswordText": "...",
    "Nonce": "...", "Created": "..."
  },
  "Xrg": {
    "Atribut_Xrg_ixsExt": "...",
    "Struktura-cilu": {
      "Rok-sberu": "...",
      "Id-struktury": "...",
      "Nazev-struktury": "..."
    },
    "Cil": {
      "Id-cile": "...",
      "Id-nadrazeneho-cile": "...",
      "Nazev": "...",
      "Cislo-cile": "...",
      "Cislo-nadrazeneho-cile": "...",
      "Platnost-od": "...",
      "Platnost-do": "...",
      "Rozpoctovy-cil": "...",
      "Id-manazera-cile": "...",
      "Id-ekonomickeho-organu": "...",
      "Id-rozpoctoveho-kompetenta": "...",
      "Stav": "...",
      "Stav-popis": "...",
      "Nks-zaukolovane": "...",
      "Popis": "..."
    },
    "Polozka-limitu": {
      "Id-cile": "...",
      "Cislo-cile": "...",
      "Vydajova-oblast": "...",
      "Rok": "...",
      "Nks": "...",
      "Castka-fin": "..."
    }
  }
}
```

### Co z toho pro návrh plyne

| Pozorování | Důsledek |
|---|---|
| Autentizace je v těle zprávy (`GRestHeader`), ne v hlavičkách HTTP | Konektor musí umět skládat autentizaci do těla, ne jen posílat hlavičku |
| Jedna odpověď nese **tři různé entity** — strukturu, cíle a položky limitu | Jeden běh konektoru plní **více číselníků najednou** |
| `Id-nadrazeneho-cile` | Hierarchie uvnitř číselníku |
| `Id-manazera-cile`, `Id-ekonomickeho-organu`, `Id-rozpoctoveho-kompetenta` | Vazby na jiné číselníky — cíl odkazuje na osobu a další subjekty |
| `Vydajova-oblast` u položky limitu | Vazba na číselník výdajových oblastí |
| `Platnost-od`, `Platnost-do` u cíle | Věcná platnost hodnoty — nezávislá na verzi číselníku |
| Odpověď nemá verzi ani časové razítko stavu | Verzování je plně na aplikaci |
| `Stav` a `Stav-popis` — kód a jeho popis v jedné odpovědi | Vnořený mini-číselník. Rozhodnuto (Z2): **vytáhne se jako samostatný číselník** a naváže vazbou. Jinak by se popis opakoval u každé položky. |

### Cílové číselníky z tohoto jediného zdroje

Struktury cílů · Cíle · Položky limitu · Výdajové oblasti · Osoby ·
Ekonomické orgány · Rozpočtoví kompetenti · Stavy cíle

Osm číselníků z jedné odpovědi. To je přesně ten důvod, proč nesmí mít každý číselník
vlastní tabulku a vlastní webovou službu.

## Co se přebírá ze Zápisky

Zápiska má odladěný běh synchronizace na pozadí: periodické spouštění, spouštění na událost,
nastavení uložené v databázi, administrace běhu z uživatelského prostředí a evidence
výsledku posledního běhu. Přebírá se celý mechanismus, mění se jen to, co se synchronizuje.

Zdroje k harvestu:
[30-prevzate-moduly/01-autentizace-ad.md](../30-prevzate-moduly/01-autentizace-ad.md)
— sekce o synchronizaci.
