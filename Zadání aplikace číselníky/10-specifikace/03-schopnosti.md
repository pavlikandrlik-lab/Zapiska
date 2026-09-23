# Schopnosti aplikace

Šest schopností, které aplikaci definují. Každá má kritérium, podle kterého se pozná,
že je hotová.

---

## S1 — Udržet číselník, včetně víceúrovňového

Aplikace eviduje číselníky, jejich položky, hierarchii uvnitř číselníku a vazby mezi číselníky.

**Hotovo, když:** lze založit číselník, naplnit ho hodnotami, postavit v něm hierarchii
libovolné hloubky a navázat jeho položky na položky jiného číselníku — a všechno to
projde bez zásahu do databázového schématu.

## S2 — Být generická vůči struktuře číselníků

Struktura číselníku je **data, ne kód**. Založení nového číselníku je vyplnění definice.

Východiskem návrhu jsou principy **propojených otevřených dat (Linked Open Data, 5★)** —
tedy nejen strojově čitelný formát, ale i **odkazy mezi věcmi a stabilní identifikátory**.
LOD se běžně staví nad grafovou databází; zde se tentýž princip realizuje **nad SQL Server**.

**Hotovo, když:** padesátý číselník s dosud nepoužitou strukturou vznikne bez jediného
řádku nového kódu, bez migrace schématu a bez nové webové služby.

> Tohle je hlavní architektonický problém zadání. Řešení:
> [20-architektura/04-datovy-model.md](../20-architektura/04-datovy-model.md).

## S3 — Číst číselníky z vnějších zdrojů a verzovat si je po svém

> **Etapa 2** — v první dodávce není. [09-etapy.md](09-etapy.md)

Aplikace umí přečíst číselník z vnějšího systému, uložit ho a **vést si nad ním vlastní verze**.

**Hotovo, když:** po opakovaném načtení nezměněného zdroje nevznikne nová verze;
po načtení zdroje s jedinou změněnou hodnotou vznikne nová verze obsahující **jednu změnu**.

## S4 — Číst seznamy z vnějších zdrojů a dělat z nich vlastní číselníky

> **Etapa 2** — v první dodávce není. [09-etapy.md](09-etapy.md)

Těžší případ. Zdroj poskytuje seznam hodnot, který číselníkem není — neverzuje,
nemá stabilní identitu položek, nemá popis struktury.

**Pravidlo:** pokud zdroj neverzuje, verzuje aplikace. To, že to jeden systém dělá špatně,
není důvod, aby to dělal špatně i referenční zdroj.

**Hotovo, když:** ze seznamu bez verzí vznikne plnohodnotný verzovaný číselník
a v historii je dohledatelné, kdy se která hodnota objevila, změnila a zmizela.

## S5 — Uživatelské prostředí

- Uživatel bez přidělené role prohlíží.
- Uživatel s rolí editora upravuje číselníky **ve svém datovém rozsahu**.
- Vzhled podle **gov designu**, jako Zápiska. Rozvržení počítá se širokou obrazovkou.

**Hotovo, když:** neznámý doménový uživatel se dostane k číselníkům bez jakéhokoli
zakládání účtu, a editor vidí ovládací prvky pro úpravu **jen u číselníků ve svém rozsahu**.

## S6 — Být referenčním zdrojem pro ostatní aplikace

Konzumující aplikace si přes rozhraní vyžádá číselník a dostane jeho hodnoty,
platnost, verzi a popis struktury.

**Hotovo, když:** jedno rozhraní obslouží plochý i víceúrovňový číselník,
konzument si umí vyžádat konkrétní historickou verzi, a **přidání nového číselníku
nevyžaduje žádnou změnu rozhraní**.

Podrobnosti: [05-api-referencni-zdroj.md](05-api-referencni-zdroj.md).

---

## Průřezové schopnosti

| Schopnost | Poznámka |
|---|---|
| **Verzování všeho** | Datově úsporné — uložený je publikovaný stav a rozdíly, historická verze se dopočítá |
| **Auditní log** | Kdo, kdy, co změnil. Standardní vedení auditu zásahů. |
| **Globální vyhledávání** | Přes OpenSearch, napříč číselníky a hodnotami |
| **Export do PDF a tisk** | |
| **Import ze souboru JSON** | Předem definovaná struktura, viz [06-import-json.md](06-import-json.md) |
| **Efektivní práva** | Obrazovka „kdo co smí a odkud to má" |
| **Uživatelský profil** | |
| **Světlý / tmavý / automatický vzhled** | |
