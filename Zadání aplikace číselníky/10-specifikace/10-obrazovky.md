# Obrazovky

Rozsah **etapy 1** ([09-etapy.md](09-etapy.md)). Obrazovky etapy 2 jsou uvedené na konci
jako výhled, nestaví se teď.

Rozvržení počítá se **širokou obrazovkou**, vzhled je gov design system.

**Rozvržení** jednotlivých obrazovek je v [11-wireframy.md](11-wireframy.md),
**průchod mezi nimi** v [12-prochazeni.md](12-prochazeni.md).

---

## Veřejná část — vidí každý, bez jakéhokoli přidělení role

### O1 — Seznam číselníků

Vstupní obrazovka aplikace.

| Prvek | |
|---|---|
| Vyhledávání | Podle kódu, názvu i popisu |
| Filtry | Režim správy, aktivní / vyřazené |
| Sloupce | Kód · Název · Počet hodnot · Aktuální verze · Režim správy · Naposledy publikován |
| Akce | Otevřít detail. Editační akce **jen u číselníků ve vlastním datovém rozsahu**. |

### O2 — Detail číselníku

Záložky. Všechny čitelné pro každého.

| Záložka | Obsah |
|---|---|
| **Hodnoty** | Tabulka hodnot. U hierarchického číselníku přepínač *plochý seznam / strom*. Filtr platnosti k datu. Hledání. Stránkování. Vazby zobrazené jako odkaz na položku cílového číselníku — proklik funguje. |
| **Struktura** | Definice atributů a vazeb. Odkaz na strojový popis struktury pro konzumenty. |
| **Verze** | Seznam vydaných verzí: číslo, datum, kdo vydal, poznámka, počet změn. |
| **Rozdíl verzí** | Výběr dvou verzí a přehled, co se mezi nimi změnilo — přidané, změněné, odebrané položky a u změněných konkrétní atributy. |

Je-li číselník rozpracovaný, hlavička nese upozornění *„obsahuje nepublikované změny"*
a u zamčeného *„upravuje X od HH:MM"*.

### O3 — Výsledky globálního vyhledávání

Napříč číselníky a hodnotami. Výsledek vede na položku v příslušném číselníku.

### O4 — Tisková a PDF podoba

Číselník a rozdíl verzí. Odvozené z týchž dat, ne samostatná obrazovka.

---

## Editace — pro roli s datovým rozsahem

### O5 — Hromadná editace hodnot

Jádro práce editora. Otevření si vyžádá **výhradní zámek** ([08-zamek-editace.md](08-zamek-editace.md));
pokud ho drží někdo jiný, obrazovka se neotevře a uživatel se dozví kdo a od kdy.

| Prvek | |
|---|---|
| Tabulka | Editace přímo v buňkách, sloupce podle definice číselníku |
| Zvýraznění | Přidané, změněné a vyřazené řádky odlišené na první pohled |
| Vazby | Výběr cílové položky z navázaného číselníku, ne psaní kódu z hlavy |
| Hierarchie | Určení nadřazené položky |
| Vyřazení | Ukončením platnosti. **Mazání neexistuje.** |
| Seznam změn | Otevře přehled všeho rozpracovaného. **Není podmínkou uložení.** |
| **Uložit změny** | Zapíše rozpracované změny. Konzumenti nic nepoznají. |
| **Publikovat novou verzi** | Vydá verzi z **celého** rozpracovaného balíku a uvolní zámek |

Rozpracované změny patří číselníku, ne osobě — kdo přijde po vypršení cizího zámku,
pokračuje v jejich práci.

### O6 — Import ze souboru JSON

Není samostatná cesta, je to **druhý vstup do O5**.

```
nahrání souboru → ověření → výsledek ověření → promítnutí do editační tabulky (O5)
```

Ověření vypíše **všechny nalezené chyby najednou** s uvedením položky, ne první a konec.
Po promítnutí uživatel vidí svůj číselník se zvýrazněnými změnami a může je ještě
ručně upravit, než uloží.

### O7 — Definice struktury číselníku

Pro roli **správce číselníků**. Zakládání číselníku, atributy, vazby na jiné číselníky,
příznak hierarchie, režim správy.

Změna struktury je změna hlavního čísla verze a dotýká se konzumujících aplikací —
obrazovka na to upozorňuje před publikováním.

---

## Správa aplikace

| # | Obrazovka | Obsah |
|---|---|---|
| O8 | **Role uživatelů** | Přiřazení rolí a **datových rozsahů** — výčet číselníků, nebo „všechny" |
| O9 | **Efektivní práva** | Co konkrétní člověk smí a **odkud grant pochází**. Připraveno na další zdroj práv z centrálního systému. |
| O10 | **Auditní log** | Kdo, kdy, co změnil. Filtrování podle osoby, číselníku a období. Jen zápisové operace — čtení se nesleduje (Z1). |

## Osobní a nápověda

| # | Obrazovka | Obsah |
|---|---|---|
| O11 | **Profil** | Vlastní předvolby, režim vzhledu |
| O12 | **Dokumentace / wiki** | Markdown z repozitáře renderovaný v aplikaci. Později se převede do XWiki. |
| O13 | **Style guide** | Živý přehled komponent. Vývojářská obrazovka. |

---

## Výhled — etapa 2

| Obrazovka | Obsah |
|---|---|
| **Zdroje** | Evidence zdrojů, nastavení, prahová pojistka |
| **Stav běhů** | Kdy zdroj běžel, s jakým výsledkem, kolik změn, proč se případně zastavil |

---

## Průřezová pravidla

- **Autorizace se vyhodnocuje na serveru.** Skrytí tlačítka není kontrola práva.
- **Editační prvky se zobrazují jen u číselníků ve vlastním datovém rozsahu** — a jen
  u ručně spravovaných (E3).
- **Modály se zavírají jen křížkem**, nikdy klikem mimo. Poučení ze Zápisky: tažení myší
  z pole ven jinak vede k nechtěnému zavření rozdělané práce.
- **Nic se nemaže.** Všude, kde by se čekalo mazání, je ukončení platnosti.
