# Datové konvence pro Microsoft SQL Server

Databáze je **Microsoft SQL Server** (rozhodnutí A5′). Přístup k datům EF Core
s poskytovatelem `Microsoft.EntityFrameworkCore.SqlServer`, migrace ručně psanými
očíslovanými skripty.

Tenhle dokument je **převodní tabulka a soupis pastí**. Datový model se nemění —
mění se dialekt.

---

## Převodní tabulka

| Pojem | PostgreSQL | **SQL Server** |
|---|---|---|
| Automatický klíč | `GENERATED ALWAYS AS IDENTITY` | `IDENTITY(1,1)` |
| Datum a čas s pásmem | `timestamptz` | `datetimeoffset` |
| Datum | `date` | `date` |
| Pravda/nepravda | `boolean` | `bit` |
| Krátký text | `varchar(n)` | **`nvarchar(n)`** |
| Dlouhý text | `text` | **`nvarchar(max)`** |
| Desetinné číslo | `numeric(18,4)` | `decimal(18,4)` |
| Identifikátor | `uuid` | `uniqueidentifier` |
| Nynější čas | `now()` | `SYSUTCDATETIME()` / `SYSDATETIMEOFFSET()` |
| Nový identifikátor | `gen_random_uuid()` | `NEWID()` |
| Posun času | `now() + interval '5 hours'` | `DATEADD(HOUR, 5, SYSUTCDATETIME())` |
| Vrácení klíče | `RETURNING id` | `OUTPUT INSERTED.id` / `SCOPE_IDENTITY()` |
| Hledání bez ohledu na velikost písmen | `ILIKE` | `LIKE` *(viz kolace níže)* |
| Skript s proměnnými | `DO $$ … $$`, `\set` | `DECLARE @x …; BEGIN … END` |

### Textové sloupce jsou vždy `nvarchar`, nikdy `varchar`

Aplikace je celá v češtině. `varchar` ukládá znaky podle kódové stránky a diakritika
se v něm rozsype nebo se ztratí při porovnávání. **Každý textový sloupec je `nvarchar`
a každý textový literál ve skriptu má předponu `N`:**

```sql
INSERT INTO ciselniky (kod, nazev) VALUES ('cile', N'Rozpočtové cíle');
```

Bez `N` se `Rozpočtové` uloží jako `Rozpoctove`, nebo hůř.

### Kolace

Databáze se zakládá s českou kolací **bez rozlišování velikosti písmen**
(`Czech_CI_AS`). Z toho plyne, že `LIKE` hledá bez ohledu na velikost písmen samo —
`ILIKE` z PostgreSQL nemá v SQL Serveru protějšek a nepotřebuje ho.

V EF Core:

```csharp
zaklad = zaklad.Where(p => EF.Functions.Like(p.Kod, vzorek)
                        || EF.Functions.Like(p.Nazev, vzorek));
```

---

## Tři místa, kde se sémantika liší

### 1. `NULL` se v jedinečném indexu rovná `NULL` — a nám to pomáhá

PostgreSQL považuje dva `NULL` za různé, takže jedinečnost přes sloupce, které mohou
být prázdné, vyžadovala obalení do `COALESCE`.

**SQL Server považuje dva `NULL` za shodné.** Jedinečný index nad rozpracovanými změnami
proto nepotřebuje žádné obalení:

```sql
CREATE UNIQUE INDEX ux_zmena_rozpracovana ON zmena
    (ciselnik_id, polozka_id, druh, atribut_definice_id, vazba_definice_id, pole)
    WHERE verze_id IS NULL;
```

Dva řádky se shodnými hodnotami včetně prázdných se považují za duplicitu a druhý
se odmítne — přesně to chceme. **Verze pro SQL Server je jednodušší než pro PostgreSQL.**

Částečné (filtrované) indexy SQL Server podporuje, takže podmínka `WHERE verze_id IS NULL`
platí beze změny.

### 2. Získání zámku nemá jednopříkazovou obdobu

PostgreSQL uměl `INSERT … ON CONFLICT DO UPDATE … WHERE podmínka`. SQL Server takovou
podmíněnou variantu nemá a `MERGE` má zdokumentované úskalí při souběhu.

Atomicita se proto drží **zámkem rozsahu uvnitř transakce**:

```sql
BEGIN TRANSACTION;

UPDATE zamek_ciselniku WITH (UPDLOCK, HOLDLOCK)
   SET osoba_id = @osoba, ziskan_kdy = SYSUTCDATETIME(),
       posledni_aktivita_kdy = SYSUTCDATETIME(),
       platnost_do = DATEADD(MINUTE, @minut, SYSUTCDATETIME())
 WHERE ciselnik_id = @ciselnik
   AND (osoba_id = @osoba OR platnost_do < SYSUTCDATETIME());

IF @@ROWCOUNT = 0
   AND NOT EXISTS (SELECT 1 FROM zamek_ciselniku WITH (UPDLOCK, HOLDLOCK)
                    WHERE ciselnik_id = @ciselnik)
BEGIN
    INSERT INTO zamek_ciselniku
        (ciselnik_id, osoba_id, ziskan_kdy, posledni_aktivita_kdy, platnost_do)
    VALUES (@ciselnik, @osoba, SYSUTCDATETIME(), SYSUTCDATETIME(),
            DATEADD(MINUTE, @minut, SYSUTCDATETIME()));
END

COMMIT TRANSACTION;
```

`HOLDLOCK` na neexistujícím řádku drží **zámek rozsahu**, takže druhý souběžný požadavek
nemůže vložit řádek mezi kontrolu a vložení. To je celý smysl konstrukce — bez něj
by dva lidé mohli získat zámek na tentýž číselník.

Vrací se počet dotčených řádků; nula znamená, že zámek drží někdo jiný.

### 3. Chybová čísla

Testy se kotví na čísla chyb, ne na text hlášky.

| Co | PostgreSQL | **SQL Server** |
|---|---|---|
| Porušení jedinečnosti | `23505` | **`2627`** (omezení), **`2601`** (index) |
| Porušení kontrolní podmínky | `23514` | **`547`** |
| Porušení cizího klíče | `23503` | **`547`** |
| Typ výjimky | `Npgsql.PostgresException` | `Microsoft.Data.SqlClient.SqlException` |

```csharp
var vyjimka = await Assert.ThrowsAsync<SqlException>(() => …);
Assert.Contains(vyjimka.Number, new[] { 2627, 2601 });   // jedinečnost
```

> **Pozor:** SQL Server hlásí porušení kontrolní podmínky i cizího klíče **týmž číslem 547**.
> Test, který chce odlišit obojí, se musí kotvit i na název omezení v textu hlášky —
> proto se všechna omezení pojmenovávají výslovně:
> `CONSTRAINT ck_polozka_platnost CHECK (…)`.

---

## Co se přebírá ze Zápisky navíc

Rozhodnutí A5′ odstranilo jedinou vrstvu, která se dosud měla překládat.
Zápiska běží na SQL Serveru, takže se **beze změny dialektu** přebírá:

- konfigurace entit a převody typů,
- vzor bootstrapu databáze a očíslovaných migračních skriptů
  (`PMTracker_insert_sql`, `db_upgrade_*.sql`, `db_check_applied_upgrades.sql`),
- kontrolní skript stavu instance,
- fixture integračních testů zakládající databázi na běh.

Zůstává jediné omezení: **schéma se přebírá jako vzor postupu, ne jako obsah.**
Doména Číselníků je jiná.

## Čemu se ze Zápisky vyhnout

Zápiska nese historickou zátěž: **sloupce s diakritikou a mezerami v názvech**
(`subsystemy.[kód]`). Vyžaduje to hranaté závorky a je to zdroj chyb.

**Číselníky to nedědí.** Názvy tabulek a sloupců jsou výhradně malá písmena bez
diakritiky, slova oddělená podtržítkem. Hranaté závorky nejsou v žádném skriptu potřeba.
