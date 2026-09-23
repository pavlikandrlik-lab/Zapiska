# Testovací strategie

Co se testuje, kde, čím a kdy. Doplňuje
[08-jak-se-hlida-kvalita.md](08-jak-se-hlida-kvalita.md), který popisuje, čím se hlídá
architektura.

---

## Pět vrstev

| Vrstva | Projekt | Co ověřuje | Potřebuje |
|---|---|---|---|
| **Jednotkové** | `Ciselniky.Tests.Unit` | Logika bez okolí — projekce práv, sestavení stromu, generátor schématu, rozdíl verzí | nic |
| **Architektonické** | tamtéž, `Architektura/` | Pravidla, ne chování — vrstvení, oprávnění, velikost souborů, veřejný kontrakt | nic |
| **Integrační** | `Ciselniky.Tests.Integration` | Skutečná databáze — schéma, omezení, dotazy, počet dotazů | SQL Server |
| **HTTP** | `Ciselniky.Tests.Api` | Rozhraní přes `WebApplicationFactory` — stavové kódy, oprávnění, tvar odpovědi | SQL Server |
| **Koncové** | `Ciselniky.Tests.E2E` | Průchod prohlížečem, Playwright v .NET | SQL Server + prohlížeč |
| **Komponenty** | `ciselniky-web`, Vitest | React komponenty a pomocné funkce | Node |

## Spuštění

```bash
dotnet test Ciselniky.sln            # všechny .NET vrstvy
cd ciselniky-web && npx vitest run   # komponenty
```

---

## Co se testuje na které vrstvě — a co ne

**Nejčastější chyba je testovat věc na příliš vysoké vrstvě.** Koncový test, který ověřuje
výpočet, je pomalý, křehký a při pádu neřekne, co je špatně.

| Otázka | Vrstva |
|---|---|
| Vrátí dopočet historické verze správné hodnoty? | jednotková |
| Odmítne databáze dvě rozpracované změny téhož atributu? | integrační |
| Dostane editor bez rozsahu odmítnutí? | HTTP |
| Zobrazí se rozcestník a ožijí gov komponenty? | koncová |

---

## Pravidla, která platí bez výjimky

**Regresní test musí před opravou selhat.** Test, který projde i na rozbité verzi,
nedrží nic. Ověřuje se to tak, že se spustí dřív, než vznikne oprava.

**Po každém bloku plná sada jednotkových testů a nahlášený výsledek.**
Blok není hotový, dokud sada neběží.

**Build končí s 0 chybami a 0 varováními.** Vynucuje `TreatWarningsAsErrors` —
pravidlo, které nevynucuje nástroj, se do měsíce přestane dodržovat.

---

## Pasti převzaté ze Zápisky

Tyhle chyby v Zápisce reálně nastaly. V zadání figurují jako **pravidla, ne doporučení**.

| Past | Pravidlo |
|---|---|
| Hostitelský prvek gov komponenty je pro Playwright „neviditelný", vysoká karta „nestabilní" | Klikat dispatchem události, čekat na třídu `hydrated`, viditelnost ověřovat **počtem prvků**, ne kontrolou viditelnosti |
| Šablonovací vrstva kóduje diakritiku na HTML entity | U Číselníků odpadá — rozhraní vrací JSON, porovnává se hodnota, ne vyrenderovaný dokument |
| Načítání souvisejících dat po položkách | Test **počítá provedené dotazy** a padne, když jich je víc než čtyři na stránku |
| Nabobtnalé soubory | Hlídač velikosti je **test**, ne skript — skript se v běhu testů sám nespustí |

---

## Testovací data

**Integrační a HTTP vrstva:** každý běh dostane **vlastní databázi**, aby na sebe testy
nenavazovaly. Zakládá ji fixture z baseline a upgrade skriptů — tím se **mimochodem
testuje i migrační cesta**, protože testy běží nad schématem, které vzniklo stejně
jako produkční.

**Ukázková data** (`db/db_seed_ukazka.sql`) slouží ručnímu ověření a ladění, ne testům.
Test, který stojí na ukázkových datech, se rozbije, jakmile je někdo upraví.

---

## Kdy testy nestačí

Verdikt o vzhledu a použitelnosti vynáší **uživatel na cílové stanici**, ne vývojářův
prohlížeč a ne koncový test. Každý plán proto končí seznamem **ručního ověření** —
a ten se odškrtává na skutečném počítači, pro který je aplikace určená.
