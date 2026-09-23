# Propojená data: co přebíráme ze SKOS a co ne

Vzniklo z posouzení externího doporučení (Gemini) proti našemu návrhu. Cenné z něj je
**jedno konkrétní jméno: SKOS**. Přebíráme ho — ale jinak, než doporučení navrhovalo.

---

## Co je SKOS a proč ho chceme

**SKOS** (*Simple Knowledge Organization System*) je doporučení W3C — **mezinárodní standardní
slovník pro popis číselníků, tezaurů a klasifikací**. Jmenný prostor
`http://www.w3.org/2004/02/skos/core#`.

Naše pojmy mají v SKOS zavedené protějšky:

| Náš pojem | SKOS |
|---|---|
| Číselník | `skos:ConceptScheme` |
| Položka číselníku | `skos:Concept` |
| Položka patří do číselníku | `skos:inScheme` |
| Kód položky | `skos:notation` |
| Název položky | `skos:prefLabel` |
| Popis | `skos:definition`, případně `skos:scopeNote` |
| Nadřazená položka **uvnitř téhož číselníku** | `skos:broader` |

**Přínos:** výstup rozhraní přestane být „náš tvar" a stane se **rozpoznatelným standardem**.
Nástroje jiných úřadů ho přečtou bez domluvy. Ve veřejné správě je to argument, který obstojí.

**Cena:** napsat jeden `@context` dokument. To je celé.

> **Rozhodnutí: přebíráme SKOS jako slovník výstupu.** Vnitřní uložení se nemění —
> zůstává varianta C ([04-datovy-model.md](04-datovy-model.md)).

---

## Tři druhy odkazu a tři různá řešení

Tady bylo doporučení **věcně chybné** a je to past, do které by implementace snadno spadla.
Doporučení ukazuje jako hlavní příklad `skos:broader` mezi položkami **dvou různých číselníků**
(kraj → stát). Tak se `skos:broader` nepoužívá.

SKOS vede `broader` a `narrower` jako vztahy **uvnitř jednoho schématu**.
Pro vztahy mezi schématy má samostatné mapovací vlastnosti.
A náš druhý případ není ani jedno z toho.

| Druh odkazu | Příklad | Jak zapsat |
|---|---|---|
| **Hierarchie uvnitř číselníku** | Cíl → nadřazený cíl | `skos:broader` |
| **Doménová vazba na jiný číselník** | Cíl → manažer cíle | **Vlastní predikát v našem jmenném prostoru**, např. `fis:manazerCile` |
| **Ztotožnění s cizím číselníkem** | Naše položka = táž věc jinde | `skos:exactMatch` |

Prostřední případ je ten častý — a **není to mapování ani hierarchie**. „Cíl má manažera"
není tvrzení, že cíl je užší pojem než manažer, ani že jsou to tytéž věci.
Je to doménový vztah a patří mu doménový predikát.

Doporučení tuhle možnost zmiňuje až jako poznámku pod čarou. U nás je to hlavní případ.

> **Náš model tenhle rozdíl už nese.** `polozka.nadrazena_polozka_id` je hierarchie,
> `polozka_vazba` je doménový vztah. Jsou to dvě různé tabulky právě proto, že jsou to dvě
> různé věci. Posouzení to potvrdilo tím, že je splynulo dohromady a vyšla z toho chyba.

---

## Co SKOS nepokrývá

Doporučení řeší **pojmenování a hierarchii**. To je menší část našeho problému.

| Naše potřeba | Pokrývá SKOS |
|---|---|
| Pojmenování a identifikátory položek | **ano** |
| Hierarchie uvnitř číselníku | **ano** |
| **Typované atributy položky** — číslo cíle, stav, částka, platnost od–do | **ne** |
| **Verzování číselníku** | **ne** |
| **Rozpracovaný stav a publikování** | **ne** |
| **Úsporné ukládání změn** | **ne** |
| **Věcná platnost hodnoty v čase** | **ne** |
| **Datový rozsah oprávnění** | **ne** |
| **Výhradní zámek na editaci** | **ne** |

SKOS má sice `skos:historyNote`, ale to je poznámka pro člověka, ne strukturovaná historie.

Proto: **SKOS je slovník pro výstup, ne model pro uložení.** Návrh tabulek v doporučení
(`concept_schemes` / `concepts` / `relations`) drží jen identifikátor, název a vztah.
Kdyby se podle něj postavilo úložiště, chyběly by mu atributy — tedy vlastní obsah
číselníku — i celé verzování.

Naopak: v části, kterou pokrývá, se doporučení s naším návrhem shoduje.
Jeho tabulka `relations` je naše `polozka_vazba`. To je nezávislé potvrzení, že
oddělit vztahy do vlastní tabulky s cizími klíči je správně.

---

## Co odmítáme

### Grafovou databázi

Doporučení samo dochází k tomu, že relační databáze stačí. Souhlasíme —
a máme k tomu vlastní důvody: SQL Server je rozhodnutý, provoz ho umí,
a integritu vazeb hlídá databáze sama. V grafovém světě by ji musel hlídat SHACL
nebo aplikace.

### R2RML / Ontop a koncový bod SPARQL

Doporučení navrhuje posadit nad SQL překladovou vrstvu, která navenek mluví SPARQL.

**Odmítáme.** Znamenalo by to provozovat další službu v uzavřené síti, kterou musí někdo
udržovat a zálohovat — a to kvůli dotazovacímu jazyku, který **žádný náš konzument nechce**.
Konzumenti mluví REST (rozhodnutí Z3: ani SOAP nikdo nepotřebuje). Přidávat SPARQL
znamená přidat náklad bez odběratele.

Kdyby se jednou objevil konzument, který SPARQL potřebuje, dá se překladová vrstva
posadit nad hotovou databázi kdykoli později. Nic tím teď neztrácíme.

### Modelovat úložiště jako trojice

To byla naše zamítnutá varianta A. Zdůvodnění je v
[04-datovy-model.md](04-datovy-model.md).

---

## Co se tím konkrétně mění v zadání

1. **Rozhraní dostane `@context` postavený na SKOS.** Při `Accept: application/ld+json`
   se vrací tatáž data se standardním slovníkem.
2. **Doménové predikáty dostanou vlastní jmenný prostor** odvozený od bázové adresy
   z rozhodnutí Z4.
3. **`skos:broader` výhradně pro hierarchii uvnitř číselníku.** Nikdy mezi číselníky.
   Zapsáno jako závazné pravidlo, protože je to častá chyba.
4. **Vnitřní model se nemění.** Varianta C platí.
