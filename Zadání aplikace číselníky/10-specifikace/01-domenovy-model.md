# Doménový model — pojmy

Věcné vymezení pojmů. Technické uložení řeší
[20-architektura/04-datovy-model.md](../20-architektura/04-datovy-model.md).

---

## Číselník

Pojmenovaná množina hodnot, podle které se zadávají data v jiných systémech.
Má kód, název, popis, správce a stav. Je **jednotkou verzování** i **jednotkou oprávnění**.

Číselník má **režim správy**: je buď **ručně spravovaný**, nebo **externí** — přebíraný
ze zdroje. Hybrid neexistuje. Číselník má právě jednoho vlastníka dat.

## Definice struktury číselníku

Popis toho, **jaké atributy mají položky daného číselníku** a **jaké vazby vedou ven**.
Je to data, ne kód — založení nového číselníku je vyplnění definice, ne programování.

Definice určuje pro každý atribut: kód, název, datový typ, povinnost, násobnost,
a u vazby cílový číselník.

Z definice se odvozuje:
- formulář pro editaci v uživatelském prostředí,
- validace při ukládání a při importu,
- popis struktury vystavený přes rozhraní pro konzumující aplikace.

## Položka číselníku

Jedna hodnota v číselníku. Má vlastní kód, název, hodnoty atributů podle definice
a časovou platnost.

Položka má **stabilní vnitřní identitu**, která přežije změnu jejího kódu i názvu.
Bez toho by se přejmenovaná položka jevila jako smazaná a nově založená a historie by se rozpadla.

## Hierarchie uvnitř číselníku

Položka může mít **nadřazenou položku ve stejném číselníku**. Tím vzniká víceúrovňový
číselník. Hloubka není omezená.

Příklad z praxe: cíl má nadřazený cíl.

> **Hloubka není nikde deklarovaná ani omezená.** Příznak `hierarchicky` je jen
> zapnuto/vypnuto. Prohloubení číselníku o další úroveň je proto **datová operace** —
> ne změna struktury, ne migrace, ne zásah do rozhraní.
> Viz [13-zivotni-cyklus-struktury.md](13-zivotni-cyklus-struktury.md).

## Vazba mezi číselníky

Položka jednoho číselníku může **odkazovat na položku jiného číselníku**.

Příklad z praxe: rozpočtový cíl odkazuje na manažera cíle (položka číselníku osob),
na ekonomický orgán a na rozpočtového kompetenta.

Vazby jsou to, co z množiny číselníků dělá **propojený celek**, ne hromadu nezávislých seznamů.
Jsou proto v modelu prvotřídním pojmem, ne textovým polem s cizím kódem.

## Verze číselníku

Očíslovaný stav **celého číselníku** v čase. Číslo je **dvojice hlavní.vedlejší** —
hlavní se zvyšuje při změně struktury, vedlejší při změně hodnot.

**Verzi vydává člověk.** Změny se ukládají jako rozpracované a teprve publikováním
vzniká verze, kterou uvidí konzumenti. U externího číselníku vydává verzi běh konektoru. Umožňuje odpovědět na otázku
„jak číselník vypadal, když se podle něj zadávalo".

Verze se skládá ze **změn** — elementárních úprav jednotlivých položek a atributů.
V databázi leží **publikovaný** stav a záznamy změn; historická verze se **dopočítá**,
neukládá se celá znovu.

## Platnost hodnoty

Časové rozmezí, kdy se hodnota **věcně používá** (od–do).

> **Verze a platnost jsou dvě nezávislé časové osy a nesmí se plést.**
>
> - *Verze* je technická: „jak vypadal číselník 15. 3."
> - *Platnost* je věcná: „tato hodnota se používá od 1. 1. 2026 do 31. 12. 2026."
>
> Ve verzi z 15. 3. může být uvedená hodnota, jejíž platnost začíná až v červenci.
> Vnější zdroje tuto dvojici běžně mají — příklad z praxe má u cíle vlastní `Platnost-od`
> a `Platnost-do` a přitom sám neverzuje.

## Vyřazení hodnoty

Hodnota se **nikdy nemaže**. Ukončí se jí platnost, případně se označí jako neaktivní.
Referenční zdroj, ze kterého mizí data, není referenční zdroj — konzument, který
si hodnotu uložil loni, musí být schopen ji dohledat.

## Zdroj

Vnější systém, ze kterého se číselník přebírá. Zdroj má konektor, adresu, přihlašovací
údaje, klíč identity položek a režim spouštění.

**Konektor je kód, jeden na zdroj** — obecný předpis mapování polí se nedělá. Konektor
vrací položky v kanonickém tvaru; porovnání, verzování a audit za ním jsou společné.

Zdroje se dělí podle toho, co poskytují:
- **Zdroj číselníku** — poskytuje něco, co je číselníkem už u něj.
- **Zdroj seznamu** — poskytuje seznam hodnot, který číselníkem není: neverzuje se,
  nemá stabilní identitu položek nebo nemá popis struktury. Aplikace z něj číselník **udělá**.

V obou případech platí: **pokud zdroj neverzuje, verzuje aplikace.**
To, že to jeden systém dělá špatně, není důvod, aby to dělal špatně i referenční zdroj.

## Datový rozsah

**Konkrétní číselník jako jednotka oprávnění.** Oprávnění se neuděluje globálně,
ale k výčtu číselníků. Viz [02-role-a-opravneni.md](02-role-a-opravneni.md).

---

## Ověřovací příklad

Model musí bez úprav schématu unést tuto strukturu z reálného ERP systému
(zjednodušeno z odpovědi rozhraní `export-mcdp`):

| Věc | Co to je v modelu |
|---|---|
| **Struktura cílů** — rok sběru, identifikátor, název | Číselník, nebo položka nadřazeného číselníku struktur |
| **Cíl** — identifikátor, název, číslo, platnost od–do, stav, popis | Položka číselníku cílů s atributy |
| Cíl → **nadřazený cíl** | Hierarchie uvnitř číselníku |
| Cíl → **manažer cíle** | Vazba na číselník osob |
| Cíl → **ekonomický orgán** | Vazba na jiný číselník |
| Cíl → **rozpočtový kompetent** | Vazba na jiný číselník |
| **Položka limitu** — cíl, výdajová oblast, rok, částka | Položka navázaná na cíl a na číselník výdajových oblastí |
| **Výdajová oblast** | Samostatný číselník |
| **Osoba** — jméno, příjmení, login, systémový identifikátor | Samostatný číselník |

Zdroj sám **neverzuje**. Aplikace si tedy vede vlastní verze podle zjištěných změn.

Úplný tvar odpovědi zdroje je v [04-zdroje-a-harvest.md](04-zdroje-a-harvest.md).
