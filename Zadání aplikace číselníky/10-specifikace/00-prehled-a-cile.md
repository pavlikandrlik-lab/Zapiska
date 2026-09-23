# Přehled a cíle aplikace Číselníky

## Prostředí

Aplikace vzniká pro **státní správu — ministerstvo**, do **uzavřené sítě bez připojení
k internetu**. Jde o vyhrazenou síť pro informace, u kterých musí být jistota, že se
nedostanou ven. Z toho plyne offline-first pravidlo bez výjimek: nic za běhu nesmí
sáhnout mimo síť.

## Problém

V tomto prostředí existuje velké množství číselníků — ne ve smyslu aplikačních modulů,
ale ve smyslu **seznamů hodnot, podle kterých se následně zadávají data**. Tedy přesně
to, k čemu číselníky slouží.

Dnes tyto číselníky žijí **na papíře nebo rozeseté v PDF směrnicích**. Nejsou digitalizované.

Přitom souběžně vznikají aplikace, které digitalizují další procesy, a ty **potřebují
číselníky strojově číst**. Bez centrálního zdroje si každá aplikace nese vlastní kopii,
která se rozejde s ostatními.

Vedle stojí velký **ERP systém**, který číselníky nemá. V některých modulech drží seznamy,
které by se za číselníky v jistém ohledu daly vydávat — ale nejsou verzované a nejsou
poskytované jako referenční zdroj.

## Proč vlastní aplikace

Zadavatel posuzoval existující nástroje pro správu číselníků (uvádí je pod označeními
*VODSPNH3* a *C Kosmos*) a vyhodnotil je jako **výrazně předimenzované** vůči potřebě.
Aplikaci odpovídající rozsahu problému se nepodařilo najít.

## Cíl

Postavit aplikaci, která je v této síti **referenčním zdrojem číselníků**:

1. **udrží číselník** včetně víceúrovňového,
2. **je vůči struktuře číselníků generická** — nový číselník je datová operace, ne programování,
3. **čte číselníky z vnějších zdrojů** a verzuje si je po svém,
4. **čte i seznamy z vnějších zdrojů**, které číselníky nejsou, a dělá z nich vlastní
   verzované číselníky,
5. **poskytuje číselníky ostatním aplikacím** přes jedno generické rozhraní,
6. **verzuje všechno** a vede auditní stopu, kdo co kdy změnil.

> **Cíle 3 a 4 jsou až etapa 2.** První dodávka stojí na ručně spravovaných číselnících
> a importu; velký ERP se napojuje postupně později. Viz [09-etapy.md](09-etapy.md).

## Rozsah a nerozsah

**V rozsahu:** struktura, genericita, verzování, přebírání z vnějších zdrojů, poskytování
přes rozhraní, oprávnění s datovým rozsahem, uživatelské prostředí pro prohlížení a úpravu.

**Mimo rozsah tohoto zadání:** schémata konkrétních číselníků a jejich hodnoty. Konkrétní
číselníky se uvádějí výhradně jako **příklady** ověřující, že navržená obecná struktura
obstojí.

## Tvrdé omezení, ze kterého vychází celý návrh

Výhledově půjde o **řádově 50 číselníků**.

> **Každý číselník nesmí mít vlastní databázovou strukturu.**
> Vlastní struktura by znamenala vlastní webovou službu pro každý číselník.
> Cílem je **jedna webová služba pro všechny číselníky**.

Tohle omezení je zdrojem hlavního architektonického problému, který zadání musí vyřešit:
jak na relační databázi SQL Server uložit libovolně strukturované, víceúrovňové
a vzájemně provázané číselníky tak, aby nad nimi fungovalo jedno rozhraní.

## Uživatelé

| Skupina | Počet | Co dělá |
|---|---|---|
| Čtenáři | bez omezení, každý příchozí | Prohlížejí číselníky |
| Editoři | řádově 5–20 | Zakládají a mění hodnoty v přiděleném rozsahu číselníků |
| Konzumující aplikace | roste s digitalizací | Čtou číselníky strojově přes rozhraní |
| Správci | jednotky | Zakládají číselníky, definují jejich strukturu, spravují zdroje a oprávnění |
