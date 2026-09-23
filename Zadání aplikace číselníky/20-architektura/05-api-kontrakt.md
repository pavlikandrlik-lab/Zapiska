# Vnitřní kontrakt mezi Reactem a backendem

> Tohle je rozhraní **pro vlastní frontend** (`/internal/…`). Nasazuje se s ním zároveň,
> takže se smí měnit spolu s ním.
>
> **Veřejné rozhraní pro konzumující aplikace** (`/api/v1/…`) je něco jiného — je to
> kontrakt a je popsané v
> [10-specifikace/05-api-referencni-zdroj.md](../10-specifikace/05-api-referencni-zdroj.md).

---

## Autentizace a ochrana proti podvržení požadavku

Přihlášení je **Windows Authentication**. Prohlížeč se autentizuje sám při každém požadavku —
a právě proto **nestačí spoléhat na to, že požadavek přišel od přihlášeného uživatele**.
Cizí stránka by mohla vyvolat měnící požadavek a prohlížeč by k němu identitu připojil.

Proto: **každá měnící akce nese token proti podvržení.** Čtení ho nepotřebuje.
Vzor ze Zápisky: `PmTracker.Web/Filters/AjaxAntiforgeryResultFilter.cs` — filtr, který
u požadavků z JavaScriptu vrací srozumitelnou chybu místo přesměrování na přihlášení.

## Tvar chyby

Jednotný pro celé rozhraní: **`ProblemDetails`** (RFC 9457).

```json
{
  "type": "https://…/chyby/overeni-selhalo",
  "title": "Ověření dat selhalo",
  "status": 422,
  "detail": "Číselník obsahuje položky s duplicitním kódem.",
  "chyby": [
    { "polozka": "5011", "atribut": "cisloPolozky", "zprava": "Kód se opakuje." }
  ]
}
```

Pole `chyby` je naše rozšíření. Import a hromadná editace **vracejí všechny nalezené chyby
najednou**, ne první a konec — kdo přepisuje tisíc řádků, potřebuje vidět všechno naráz.

## Oprávnění se posílají do prohlížeče

Aby frontend nenabízel, co server odmítne, dostane při načtení **seznam svých efektivních
práv** včetně datového rozsahu:

```
GET /internal/ja
→ { "osoba": {…},
    "prava": { "hodnoty.edit": ["cile", "vydajove-oblasti"],
               "verze.publish": ["cile"],
               "ciselniky.create": "*" } }
```

Hodnota je buď výčet kódů číselníků, nebo `"*"` pro všechny.

> **Není to kontrola oprávnění, je to nápověda pro rozhraní.** Kontrola je vždy znovu
> na serveru. Rozpad mezi tím, co UI nabízí, a tím, co server povolí, byl v Zápisce
> zdrojem chyb — proto oba čtou z téhož seedu.

---

## Skupiny koncových bodů

### Číselníky a hodnoty

| | |
|---|---|
| `GET /internal/ciselniky` | Seznam pro O1 — s filtry a stránkováním |
| `GET /internal/ciselniky/{kod}` | Metadata a definice struktury |
| `GET /internal/ciselniky/{kod}/polozky` | **Publikovaný stav**, stránkovaný. V odpovědi příznak `maRozpracovaneZmeny`. |
| `GET /internal/ciselniky/{kod}/polozky/rozpracovane` | Publikovaný stav **překrytý rozpracovanými změnami**. Bez stránkování — vrací celý číselník. |
| `POST /internal/ciselniky` | Založení *(správce)* |
| `PUT /internal/ciselniky/{kod}/struktura` | Změna definice *(správce)* |

> Rozdíl proti veřejnému rozhraní: **vnitřní umí ukázat rozpracovaný stav**, veřejné nikdy.

> **Proč dvě adresy místo jedné s přepínačem.** Rozpracovaně přidaná položka v tabulce
> publikovaného stavu vůbec neexistuje — je jen záznamem změny. Překryv nad **stránkou**
> by tedy musel dopočítávat i počet a pořadí napříč dvěma zdroji, a stránkování by
> přestalo dávat smysl.
>
> Překryv se proto počítá nad **celým číselníkem naráz**. Je to studená cesta: používá ji
> editor, který si číselník stejně načítá celý do tabulky, a čtenář, který si výslovně
> řekne o zobrazení s nepublikovanými změnami. Horká cesta — stránkované čtení
> publikovaného stavu — zůstává prostým dotazem do tabulky.

### Editace a zámek

| | |
|---|---|
| `POST /internal/ciselniky/{kod}/zamek` | Získání zámku. Vrací `409` a jméno držitele, drží-li ho někdo jiný. |
| `POST /internal/ciselniky/{kod}/zamek/prodlouzit` | Prodloužení. Volá se **jen při skutečné aktivitě**, nejvýš jednou za minutu. |
| `DELETE /internal/ciselniky/{kod}/zamek` | Uvolnění |
| `DELETE /internal/ciselniky/{kod}/zamek?vynutit=true` | Silové odebrání *(správce)*, zapisuje se do auditu |

### Rozpracované změny a verze

| | |
|---|---|
| `GET /internal/ciselniky/{kod}/zmeny` | Rozpracované změny — podklad pro seznam změn |
| `PUT /internal/ciselniky/{kod}/zmeny` | Uložení změn *(tlačítko **Uložit změny**)* |
| `POST /internal/ciselniky/{kod}/verze` | **Publikovat novou verzi** — vydá celý rozpracovaný balík a uvolní zámek |
| `GET /internal/ciselniky/{kod}/verze/{a}/rozdil/{b}` | Rozdíl dvou verzí pro O2 |

**Zámek se ověřuje i tady**, ne jen při jeho získání. Kdo o něj mezitím přišel, dostane
odmítnutí — s ujištěním, že jeho rozpracované změny jsou uložené.

### Import

| | |
|---|---|
| `POST /internal/ciselniky/{kod}/import/overit` | Nahraje soubor, ověří ho, **nic neuloží**. Vrací nalezené chyby, nebo změny k promítnutí. |
| `POST /internal/ciselniky/{kod}/import/promitnout` | Promítne ověřené změny mezi rozpracované |

Dvoukrokové záměrně: import je druhý vstup do editační tabulky, ne samostatná cesta
do databáze (rozhodnutí E2).

### Ostatní

`GET /internal/ja` · `GET /internal/hledani` · `GET /internal/audit` ·
`GET|PUT /internal/nastaveni/role` · `GET /internal/prava/{osobaId}` ·
`GET|PUT /internal/profil` · `GET /internal/dokumentace/*`

---

## Konvence

| Věc | Pravidlo |
|---|---|
| Stránkování | `strana` + `velikost`, odpověď nese `celkem` |
| Řazení | `razeni=nazev`, sestupně `razeni=-nazev` |
| Filtrování | Pojmenované parametry, ne obecný dotazovací jazyk |
| Datum a čas | ISO 8601, časová zóna vždy uvedená |
| Kódy | `200` čtení · `201` založení · `204` bez obsahu · `409` zámek drží jiný · `422` ověření selhalo · `403` chybí oprávnění |
| Identifikátor číselníku | **Vždy v cestě**, nikdy jen v těle — jinak nelze vyhodnotit datový rozsah oprávnění |
