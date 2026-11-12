# PM Tracker – Technická dokumentace 12: ServiceDesk integrace — referenční schéma

## 1. Účel

Tento dokument je **jediný reference** pro strukturální znalost ticketovací DB, ze které PM Tracker čte přes read-only integraci. Obsahuje schema-level informace (tabulky, datové typy, enum kategorie, join řetězce, datové pasti), které nejsou zdrojově v aplikačním kódu a které by si jinak každý vývojář musel vyhledávat v legacy ASP aplikaci nebo živé DB.

**Tento dokument neobsahuje reálná data** (jména osob, konkrétní tickety, částky, počty řádků). Obsahuje jen strukturální znalost získanou z `INFORMATION_SCHEMA` dotazů a small-sample validace.

## 2. Publikum a role

- **Backend vývojář:** mapuje EF entity, píše dotazy nad `TicketingReadOnlyDbContext`, potřebuje znát reálné datové typy a join cesty.
- **Architekt:** rozhoduje o cache strategii, izolaci přes DTO, out-of-scope hranicích.
- **QA:** seed data pro in-memory testy — musí respektovat reálné typy (jinak testy projdou, ale produkce selže).
- **DB admin (informativní):** pochopení, jak PM Tracker čte z cizí DB.

## 3. Závislosti a předpoklady

- Ticketing DB = `intranetNEW` na MS SQL Serveru.
- PM Tracker používá **servisní účet s rolí `db_datareader`** a samostatný connection string `Ticketing:ConnectionStringName` (default `TicketingReadOnly`).
- V DB **neexistují formální FK** mezi `HOT_*` tabulkami (legacy ASP). Vazby jsou čistě aplikační.
- Referenční BusinessLayer implementace z původního intranetu je v [`SD_servicedesk/Hotline.cs`](../../SD_servicedesk/Hotline.cs) (gitignored, lokální). Schema inventář v [`SD_servicedesk/hotline.txt`](../../SD_servicedesk/hotline.txt).

## 4. Vstupy a výstupy

### Vstupy
- Read-only čtení `intranetNEW.dbo.HOT_*` přes EF Core 8 SqlServer provider.
- Sekundární zdroj: souborový systém `./vyjadreni_prilohy/*` (mimo scope — viz §7).

### Výstupy
- DTOs v `PmTracker.ServiceDesk.Contracts`.
- Dashboardové agregace (počty v prodlení, čerpání rozpočtu).
- Harvest vyjádření pro chat modal v PM Trackeru.

## 5. Detailní postup

### 5.1 Klíčové tabulky a jejich role

| Tabulka | Role v PM Trackeru |
|---|---|
| `HOT_ZAZNAMY` | Tickety (NES/PMP/PNF) — zdroj dashboardu, externích vazeb |
| `HOT_VYJADRENI` | Vyjádření/zprávy k ticketům — zdroj chat modalu a harvestu |
| `HOT_KALKULACE` | Kalkulace ceny k ticketům — zdroj Výzev a sum čerpání |
| `HOT_KALKULACE_PMP` | Per-zaměstnanec rozpis kalkulace (specificky pro PMP tickety) |
| `HOT_IS` | Informační systémy — dropdown pro nastavení IS na projektu + rozpočet |
| `HOT_MODULY` | Moduly pod subsystémy — most mezi ticketem a IS |
| `HOT_SUBSYSTEM` | Subsystémy — sekundární join cesta |
| `HOT_PRILOHY` | Přílohy v DB (1 řádek per ticket, storage jako `{pid}.docx`) |
| `HOT_VYJADRENI_TEXT` | Texty šablon vyjádření (samostatný číselník) |
| `HOT_DODAVATEL` | Číselník dodavatelů |
| `HOT_TYMY` | Řešitelské týmy |
| `HOT_SEMAFOR` | "Kdo naposled sáhl na ticket" (jiný koncept než `HOT_IS.semafor`) |
| `HOT_PID` | Pool přidělovaných PIDů |

**Ignorovat:**
- `HOT_BT_*` — paralelní Bug Tracker modul, mimo scope
- `HOT_old_*` — legacy zálohy
- `HOT_SECURE_old`, `HOT_SECURE_NEW` — user tabulky mimo scope
- `HOT_LOG_EMAIL`, `HOT_LOG_GDPR`, `HOT_LOG_UPDATE` — audit logy cizí aplikace
- `HOT_UPOZORNENI`, `HOT_EXPORT`, `HOT_IMPORT` — vnitřní mechanismy ticketingu

### 5.2 Identifikátory a join řetězec

**Dva klíče pro tickety:**

| Sloupec | Typ | Role |
|---|---|---|
| `HOT_ZAZNAMY.radek` | `int IDENTITY` | Surrogate PK, interní |
| `HOT_ZAZNAMY.id` | `int NULL` | **User-facing 6-ciferné číslo ticketu** (zadává uživatel) |
| `HOT_ZAZNAMY.pid` | `nvarchar(50) NOT NULL` | **Business key napříč HOT_* tabulkami** — 12-znakový alfanumerický kód |

**Pravidlo joinu:** Uživatel zadá `id` → z `HOT_ZAZNAMY` se dohledá `pid` → **všechny ostatní tabulky** (vyjádření, kalkulace, přílohy, upozornění, semafor) joinují **přes `pid`**, nikoli přes `id`.

**Join řetězec ticket → informační systém:**

```
HOT_ZAZNAMY.modul (nvarchar 50)
       ↓ join přes zkratku
HOT_MODULY.zkratka (nvarchar 10 NOT NULL)
       ↓ FK v aplikaci (ne v DB!)
HOT_MODULY.id_IS (int NULL)
       ↓
HOT_IS.ID (int IDENTITY PK)
```

Join `HOT_ZAZNAMY.modul ↔ HOT_MODULY.zkratka` má **>99 % match rate** v reálných datech. Alternativní cesta přes `HOT_ZAZNAMY.subsystem ↔ HOT_SUBSYSTEM.zkratka` existuje, ale je sekundární.

**Primární klíče s kvirkem:**
- `HOT_SUBSYSTEM.zkratka` je **skutečné PK** (nikoli sloupec `id`!)
- `HOT_PID.PID` je **PK** (nikoli sloupec `id`)
- `HOT_SECURE.login` je **PK**
- Ostatní tabulky mají `id` nebo `ID` jako PK

### 5.3 Enumové kategorie

#### `HOT_ZAZNAMY.typ_zaznamu` — 3 hodnoty (string kódy)
- `NES` — nesrovnalost
- `PMP` — požadavek metodické podpory
- `PNF` — požadavek nové funkcionality

#### `HOT_ZAZNAMY.stav` — 4 hodnoty
- `archiv` — finální (uzavřený, vyřešený)
- `otevřeno` — aktivní v PM Trackeru
- `dodavatel` — předaný dodavateli
- `od dodavatele` — převzatý zpět od dodavatele

**"Aktivní" = `stav != 'archiv'`**. Drtivá většina všech ticketů je v archivu; aktivních je zlomek.

#### `HOT_ZAZNAMY.dulezitost` — není čistý enum
Mix hodnot podle typu záznamu:
- Společné: `Neurčeno`
- Pro NES: `NES I`, `NES II`, `NES III`, `Vada A`, `Vada B`, `Vada C`, `Vada D`
- Pro PMP/PNF: `PMP`, `PNF I`, `PNF II`, `Pozadavek`

Rozlišuje severity **uvnitř** typu ticketu.

#### `HOT_ZAZNAMY.zavaznost` — 1-znakové kódy
`D`, `N`, `Z`, `P`, `T`, `M`, `U`, `V`, `-`, `1`, `3`, `4`

Sémantika jednotlivých kódů není v dostupné dokumentaci; pro PM Tracker zůstává opaque string k zobrazení.

#### `HOT_VYJADRENI.typ` — 4 hodnoty
- `Z` = **Zpráva** / workflow událost — popis obsahuje čitelný text včetně HTML
- `P` = **Odkaz na přílohu** — popis obsahuje jen název souboru, fyzicky umístěný v `./vyjadreni_prilohy/{filename}`
- `K` = **Back-reference na kalkulaci** — popis obsahuje `id_kalk` (14-znakový kód)
- `O` = vzácné, sémantika neznámá

#### `HOT_KALKULACE.akceptace` — 8 stavů
Akceptační rodina: `Akceptováno`, `Akceptováno s výhradou k ceně`, `Akceptovat ?`
Fakturační rodina: `Fakturovat`, `Fakturováno`, `Vyfakturováno`
Ostatní: `Neakceptováno`, `Návrh`

**Pro PM Tracker platí byznys rozhodnutí (2026-04-23):** jako "akceptovaná" kalkulace se počítá **jen stav `Akceptováno`**. Fakturované stavy se **nepočítají** jako akceptované (jsou v jiné fázi životního cyklu). Viz [`SqlTicketingQueryService.AkceptovanoStav`](../../PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs).

### 5.4 Datové typy — pasti a nuance

| Sloupec | DB typ | Past |
|---|---|---|
| `HOT_ZAZNAMY.sla_deadline` | `datetime` | **Ne `datetime2`** — starší typ s milisekundovou přesností |
| `HOT_ZAZNAMY.splneno` | `smalldatetime` | **Je to datum**, nikoli bool ani int. V EF entitě musí být `DateTime?` |
| `HOT_ZAZNAMY.term_pl` | `smalldatetime` | Obvykle placeholder v budoucnosti (typicky ~2050-12-30) — **nepoužívat pro detekci prodlení** |
| `HOT_ZAZNAMY.dat_res_t` | `smalldatetime` | "Termín pro řešitele" — používá se jako primární termín pro PMP/PNF |
| `HOT_ZAZNAMY.dat_dod` | `smalldatetime` | Datum dodání dodavatelem (rozdílné od `splneno`) |
| `HOT_ZAZNAMY.priznak_zamceni` | `tinyint` | **Ne bool** — jsou tři hodnoty (0/1/2) |
| `HOT_ZAZNAMY.priznak_gdpr` | `bit` | Nullable |
| `HOT_ZAZNAMY.schvaleno` | `bit NOT NULL` | V praxi obvykle 0 i u archivovaných — sémantika nejasná |
| `HOT_IS.aktivita` | `char(10)` | **Má trailing spaces!** V dotazech použít `LIKE 'Aktivní%'` nebo `LTRIM/RTRIM`. `HOT_IS_LIMIT.aktivita` / `HOT_MODULY.aktivita` jsou `nvarchar(10)` — bez trailing spaces |
| `HOT_IS.limit`, `cerpani` | `numeric(18,2)` | Finanční rozpočet a čerpání per IS |
| `HOT_ZAZNAMY.id` | `int NULL` | V EF entitě je mapováno jako `string` pro snadné srovnání s uživatelským vstupem; přejmenování by rozbilo existující API |
| `HOT_ZAZNAMY.pid` | `nvarchar(50) NOT NULL` | 12-znakový alfanumerický kód |
| `HOT_ZAZNAMY.faxvfu` | `nvarchar(50)` | **Matoucí název — obsahuje telefonní číslo**, nikoli fax ani "vfu" |
| `HOT_ZAZNAMY.zal_HFU` | `nvarchar(50)` | Windows login ve formátu `DOMAIN\username` — **most na `Osoba.AdLogin`** |

### 5.5 Definice prodlení per typ ticketu

**Platí od 2026-11 (spec [2026-10-15](../superpowers/specs/2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md) §5, §10).**
Prodlení se počítá stejně jako ve starém ServiceDesku: port třídy `PenaltyCalculation.TicketPenaltyCalculator`
(`PmTracker.Web/Services/ServiceDesk/Prodleni/PenaltyCalculator.cs`) nad předávkami z databáze ServiceDesku (§5.13).

| Typ | Termín obrátky | Dny penále |
|---|---|---|
| `NES` | SLA hodiny podle důležitosti (`ticket_external_sla`), počítané v pracovních dnech po 24 h od předání (U1/U2) | pracovní dny po vyčerpání SLA |
| `PMP` | `dt_param` předávky U1/U2 (termín plnění), případně schválené prodloužení U11 | kalendářní dny po termínu |
| `PNF` | jako PMP | pracovní dny po termínu |

- **V prodlení** je ticket, který je **právě u dodavatele** (poslední z předávek U1/U2/U3 je U1 nebo U2) a má penalizované dny > 0.
  Ticket vrácený zpět (U3) v prodlení není, i když mu z minulých obrátek penále zůstalo (to se počítá jen do Statistik).
- Ticket u více dodavatelů = řádek na dodavatele (odůvodnění T00); v seznamu jen dodavatel, u kterého ticket právě je.
- Pracovní dny = bez víkendů a českých svátků (včetně Velkého pátku od 2016).
- Termín končí v 23:59:59 dne, prodloužení U11 promíjí dny do schváleného data včetně (T14).
- Důležitost bez řádku v `ticket_external_sla` → **Nelze vyhodnotit** (originál by dosadil 0 h, spec R6).
- Odůvodnění dnů skládají kódy T00–T14 (`PenaltyReasonFormatter` dává české věty).

~~Dřívější definice (2026-04-23): NES `sla_deadline < dnes`, PMP/PNF `dat_res_t < dnes`, metrika `DATEDIFF(DAY, termín, dnes)`.~~
Tato zjednodušená definice ukazovala jiná čísla než ServiceDesk a byla zrušena; dotaz `GetProdleneAsync` je smazaný.
Navíc se na SQL Serveru nikdy nevyhodnotil (porovnání `id` s prázdným textem se přes převodník přeložilo na `[id] <> NULL`).

### 5.6 `res_tym` se přepisuje při archivaci

Sloupec `HOT_ZAZNAMY.res_tym` obsahuje řešitelský tým, **ale při přechodu do stavu `archiv` se hodnota přepisuje na literál `'archiv'`** (originální tým se ztratí).

Pro mapování na DTO používat:

```csharp
ResTym = stav == "archiv" ? null : z.ResTym
```

Nebo v SQL:

```sql
CASE WHEN stav = 'archiv' THEN NULL ELSE res_tym END AS res_tym
```

### 5.7 Sémantika HTML v popisech vyjádření

`HOT_VYJADRENI.popis` (pro typ `Z`) a `HOT_ZAZNAMY.popis` obsahují **HTML fragmenty** z původní ASP aplikace.

**Značky, které se reálně vyskytují:**
- Blokové: `<li>`, `<br>`, `<p>`
- Zvýraznění: `<b>`, `<i>`, `<strong>`
- Odkazy: `<a href="..." target="...">` (case mixed — i `<A HREF>` varianty)

**Stabilní patterny pro parser / sanitizér:**

| Vzor | Význam |
|---|---|
| `<A HREF='./zobraz_zaznam.asp?pid={PID}' target='_VAZBA_HOTLINE'>{id}</A>` | Cross-reference na jiný ticket přes PID |
| `<a href='./vyjadreni_prilohy/{filename}' target='_HOTLINE'>{label}</a>` | Inline odkaz na přílohu (**soubor je mimo DB**) |

**Při zobrazení v PM Trackeru:**
- Vždy HTML sanitizovat (povolit jen whitelisted značky)
- URL rewriting: `./zobraz_zaznam.asp?pid=...` → interní detail PM Trackeru nebo zpětný odkaz do ServiceDesku (`Ticketing:ServicedeskBaseUrl`)
- Přílohy se v PM Trackeru **neotevírají** — uživatel je přesměrován do ServiceDesku

### 5.8 Workflow události ve vyjádřeních — stabilní fráze

PM Tracker v současnosti **neparsuje** workflow (penalizační logika U1–U11 je trvale out-of-scope). Následující seznam je **informativní** pro budoucí rozvoj a pro pochopení, co text vyjádření obvykle obsahuje:

| Fráze (substring) | Význam (TransferType v původní app) |
|---|---|
| "Záznam byl založen a předán dodavateli" | U1 — založení ticketu u dodavatele |
| "předal záznam dodavateli" | U2 — předání |
| "Záznam byl převzat k řešení dne" | U3 — převzetí od dodavatele |
| "Záznam byl převeden do archivu" | U5 — archivace |
| "Dodavatel vytvořil kalkulaci" | vedlejší událost ke kalkulaci |
| "Kalkulace byla akceptována" | vedlejší událost ke kalkulaci |
| "Dodavatel přidal řešení" | U3 — řešení předáno |
| "Řešení bylo vráceno k přepracování" | vrácení řešení |

**Autoři a zdroje:**
- `HOT_VYJADRENI.zpracoval` může obsahovat jméno osoby (viz §5.11), nebo placeholder `'Automat'` (systémová akce) či `'Nedohledáno'` (neznámý autor z dodavatelské strany).
- `HOT_VYJADRENI.tym` obsahuje organizační/dodavatelské označení (mix: role kódy, názvy dodavatelů, systémové zdroje).

### 5.9 Kalkulace — per typ ticketu

| Typ | `HOT_KALKULACE` | `HOT_KALKULACE_PMP` |
|---|---|---|
| `NES` | Typicky žádná kalkulace (servis v rámci smlouvy) | — |
| `PMP` | 1 řádek — souhrn | N řádků — per-zaměstnanec rozpis |
| `PNF` | 1 řádek — souhrn (licenční model přes `cena_i` a `cena_l`) | Žádný řádek |

Klíčové sloupce `HOT_KALKULACE`:
- `pid` — join na ticket
- `id_kalk` — 14-znakový kód kalkulace
- `verze` — verze kalkulace (při víc verzích se bere nejvyšší)
- `akceptace` — viz §5.3, pro PM Tracker jen `"Akceptováno"`
- Finance: `cena`, `pracnost_a/p/t/i`, `sazba_a/p/t/i`, `cena_a/p/t/i`, `pocet_l`, `sazba_l`, `cena_l`

**Nejvyšší akceptovaná kalkulace** (pattern v [`SqlTicketingQueryService`](../../PmTracker.ServiceDesk.Sql/SqlTicketingQueryService.cs)):

```csharp
_db.HotKalkulace
   .Where(x => x.Pid == cislo && x.Akceptace == "Akceptováno")
   .OrderByDescending(x => x.Verze)
   .FirstOrDefaultAsync(ct);
```

### 5.10 Přílohy — dvojí systém

V realitě existují **dvě paralelní cesty** k přílohám:

| Zdroj | Jak uložené | Používá PM Tracker |
|---|---|---|
| `HOT_PRILOHY` tabulka | 1 řádek per ticket, storage jako `{pid}.docx` + `puvodni_nazev` | **NE** (out of scope) |
| Inline v `HOT_VYJADRENI` typ `P` | Popis = název souboru, fyzický soubor v `./vyjadreni_prilohy/{filename}` | **NE** (out of scope) |

Pro detaily si uživatel otevře ticket v ServiceDesku přes zpětný URL. Viz §6.

### 5.11 Reprezentace osob — dvě paralelní cesty

Ticket obsahuje **dvě nezávislé reprezentace** toho samého autora/zpracovatele:

| Sloupec | Formát | Strategie mapování na `Osoba` |
|---|---|---|
| `HOT_ZAZNAMY.zal_HFU` | `FIS\username` nebo `ACR\username` (Windows login) | **Primární:** přes `AdLoginCache` → `Osoba.AdLogin` (exact match) |
| `HOT_ZAZNAMY.zpracoval`, `uzivatel`, `kontakt`, `schvalil`, `HOT_VYJADRENI.zpracoval` | Free-text, různé formáty (titul + jméno + příjmení v různém pořadí) | **Fallback:** fuzzy match na `Osoba.Jmeno`+`Osoba.Prijmeni` s tolerancí titulů a pořadí |

**Domény v `zal_HFU`:** pouze **`FIS\`** a **`ACR\`** (potvrzeno). Jiné domény se nevyskytují.

Pro Sprint A a B dashboardu není mapování na `Osoba` potřeba — stačí zobrazit raw string. Harvest vyjádření (chat modal) má vlastní resolver.

### 5.12 Architektura PM Tracker strany

**Projekt `PmTracker.ServiceDesk.Sql`:**
- [`TicketingReadOnlyDbContext`](../../PmTracker.ServiceDesk.Sql/TicketingReadOnlyDbContext.cs) — read-only DbContext s hard guardem v `SaveChanges*` (hází `InvalidOperationException`), `ChangeTracker.QueryTrackingBehavior = NoTracking`
- Entity v `Entities/` — `internal sealed class` s `HasColumnName` mappings
- `InternalsVisibleTo("PmTracker.Tests.Unit")` — testy mohou seedovat InMemory provider přes `SaveChangesForTests()`
- Query services:
  - `SqlTicketingQueryService` — tickety + kalkulace
  - `SqlVyjadreniQueryService` — vyjádření (pro harvest)
  - `SqlInformacniSystemQueryService` — IS hierarchie + prodlení + rozpočet (od Sprintu A)
- Dekorátory: `CachingTicketingQueryService`, `CachingInformacniSystemQueryService`
- Fallbacky: `DisabledTicketingQueryService`, `DisabledVyjadreniQueryService`, `DisabledInformacniSystemQueryService` (aktivní když `Ticketing:Enabled = false`)

**Projekt `PmTracker.ServiceDesk.Contracts`:**
- DTO ve formě `sealed record` s primary constructor
- Interfaces pro query services
- `TicketingOptions` (settings + validace)

**DI registrace:**
[`AddServiceDeskIntegration(IConfiguration)`](../../PmTracker.ServiceDesk.Sql/ServiceDeskServiceCollectionExtensions.cs) čte `Ticketing:Enabled` z configu a přepíná mezi Sql+Caching vs. Disabled variantami.

### 5.13 Databáze ServiceDesku: `ticket_external*`

Předávky a SLA hodiny nejsou v `intranetNEW`, ale ve **vlastní databázi starého ServiceDesku**
(v jeho `Web.config` connection string `ServiceDeskEntities`, EF6 model `ServiceDesk.edmx`). PM Tracker ji čte
read-only přes `ServiceDeskExternalReadOnlyDbContext` (connection string `ServiceDeskExternalReadOnly`, viz `install.md` §7).

| Tabulka | Sloupec | Typ | NULL | Pozn. |
|---|---|---|---|---|
| `ticket_external` | `id` | int identity | ne | PK |
| | `external_id` | varchar(50) | ne | `HOT_ZAZNAMY.id` jako text, páruje se po ořezu mezer; nečíselné hodnoty se ignorují |
| | `supplier_tag`, `sla_name` | nvarchar(50) | ano | |
| `ticket_external_workflow` | `id` | int identity | ne | PK, druhý klíč řazení při shodném čase |
| | `id_ticket_external` | int | ne | FK → `ticket_external.id` |
| | `date` | datetime | ne | čas předávky |
| | `transfertype` | int | ano | 1 = U1 založení u dodavatele, 2 = U2 předání, 3 = U3 převzetí zpět, 5 = U5 archivace, 10 = U10 žádost o prodloužení, 11 = U11 schválené prodloužení |
| | `supplier` | nvarchar(50) | ano | dodavatel předávky |
| | `dt_param` | datetime | ano | termín plnění (U1/U2 u PMP/PNF) nebo schválený termín (U11) |
| `ticket_external_sla` | `id` | int identity | ne | PK |
| | `sla_name` | nvarchar(8) | ne | hodnota `HOT_ZAZNAMY.dulezitost` (NES I … Vada D, Neurčeno); páruje se po ořezu mezer na obou stranách |
| | `sla_hours` | float | ne | SLA v hodinách |

- **Hodnotí se jen synchronizované tickety** (mají řádek v `ticket_external`). Ostatní se nezobrazují.
- Ticket patří do IS přes `HOT_ZAZNAMY.modul` → `HOT_MODULY.id_IS`.
- Spojení obou databází běží v paměti, seznamy id jdou po kouscích ≤ 1 000 (limit parametrů SQL Serveru 2 100).
- Kód: `PmTracker.ServiceDesk.Sql/SqlProdleniQueryService.cs`, `ServiceDeskExternalReadOnlyDbContext.cs`;
  lokální replika pro vývoj: `scripts/dev/servicedesk_external_schema.sql` + `scripts/dev/intranetnew_prodleni_seed.sql`.

## 6. Zpětné linky do ServiceDesku

Detaily ticketu (plný popis, všechna vyjádření, přílohy, historie) se **nepřekopírovávají** do PM Trackeru. Uživatel se z PM Trackeru prokliká zpět do ServiceDesku.

**URL pattern:**

```
{Ticketing:ServicedeskBaseUrl}/zobraz_zaznam.asp?pid={PID}
```

Base URL je příslušný hostname původní ASP aplikace (varianta `servicedesk.fis.acr.*`); přesná hodnota se nastaví v `appsettings.json` sekci `Ticketing:ServicedeskBaseUrl` při deploymentu.

**Pravidlo:** do linku jde **PID**, nikoli `id`. V PM Trackeru je obvykle znám `id`; pro generování URL je nutné znát i `pid`, proto se v tabulce `zaznam_externi_odkazy` doporučuje **ukládat oboje** (`id` + `pid`) — ušetří se lookup dotaz při každém renderingu odkazu.

## 7. Out of scope — trvale

Následující oblasti zůstávají **mimo scope PM Trackeru** (byznys rozhodnutí 2026-04-23):

- **Přílohy** — fyzický přístup k `./vyjadreni_prilohy/*` ani k `HOT_PRILOHY`. Uživatel jde do ServiceDesku.
- **Penále v Kč** — PM Tracker počítá penalizované **dny** (§5.5), ne částky v korunách. Předávky U1–U11 čte strukturovaně z databáze ServiceDesku (§5.13), parser textu vyjádření se na ně nepoužívá.
- **Workflow events jako strukturovaná data** — fráze v §5.8 jsou informativní pro porozumění, ne pro implementaci.
- **Notifikace** (`HOT_UPOZORNENI`) — PM Tracker má vlastní notifikační pipeline.
- **Audit logy** (`HOT_LOG_*`) — ticketing systém si vede sám.
- **Export/Import** (`HOT_EXPORT`, `HOT_IMPORT`) — interní mechanismy ticketingu.
- **Legacy a paralelní tabulky** (`HOT_BT_*`, `HOT_old_*`, `HOT_SECURE_old`) — ignorovat.
- **Uživatelský seznam** (`HOT_SECURE`) — PM Tracker má vlastní AD autentifikaci.

## 8. Kvirky datového modelu (referenční seznam)

- **Žádné formální FK** — legacy ASP, vazby jen v aplikační vrstvě. EF `HasOne/WithMany` nesmí předpokládat DB constraint.
- **`res_tym = 'archiv'`** se přepisuje při archivaci (§5.6).
- **`rok = 0`** ve všech moderních ticketech — sloupec se nepoužívá.
- **`schvaleno = 0`** i u archivovaných ticketů — sémantika nejasná.
- **`zpracoval`** může být `'Automat'` (systémová akce) nebo `'Nedohledáno'` (chybějící mapování autora z dodavatelské strany).
- **`faxvfu`** obsahuje telefon, ne fax (matoucí název) — §5.4.
- **`HOT_IS.aktivita`** má trailing spaces (char(10)) — §5.4.
- **`term_pl`** je v praxi placeholder v budoucnosti — nepoužívat pro prodlení.
- **`HOT_VYJADRENI` typ `P` vs `HOT_PRILOHY`** — dvě paralelní cesty k přílohám (§5.10), PM Tracker nepoužívá ani jednu.
- **`HOT_ZAZNAMY.id` je `int NULL`** — v aplikační vrstvě se pracuje jako se stringem pro srovnání s uživatelským vstupem (6-ciferné číslo).

## 9. Odkazy

### Kód a lokální soubory
- [`PmTracker.ServiceDesk.Sql/`](../../PmTracker.ServiceDesk.Sql/) — EF Core implementace
- [`PmTracker.ServiceDesk.Contracts/`](../../PmTracker.ServiceDesk.Contracts/) — DTOs, interfaces, options
- [`SD_servicedesk/hotline.txt`](../../SD_servicedesk/hotline.txt) — inventář tabulek (public, bez dat)
- [`SD_servicedesk/Hotline.cs`](../../SD_servicedesk/Hotline.cs) — referenční BusinessLayer z původní ASP aplikace (gitignored)
- [`SD_servicedesk/01_discovery.sql`](../../SD_servicedesk/01_discovery.sql) — skript pro schema discovery
- [`SD_servicedesk/02_ticket_detail.sql`](../../SD_servicedesk/02_ticket_detail.sql) — skript pro per-ticket validaci
- **Reálná data**: `SD_servicedesk/results_hot/`, `SD_servicedesk/htl_res/` (gitignored, nesdílet)

### Specy a plány
- [`docs/specs/ticketing-integration.md`](../specs/ticketing-integration.md) — původní návrh (s banner o zastaralosti)
- [`docs/specs/dashboard-prodleni.md`](../specs/dashboard-prodleni.md) — dashboard prodlení NES/PMP/PNF (nahrazeno)
- [`docs/superpowers/specs/2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md`](../superpowers/specs/2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md) — přestavba dashboardu, penalizační algoritmus, Statistiky
- [`docs/superpowers/specs/2026-04-16-projektovy-dashboard-design.md`](../superpowers/specs/2026-04-16-projektovy-dashboard-design.md) — projektový dashboard design
- [`docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md`](../superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md) — vytěžování vyjádření
- [`docs/superpowers/specs/2026-04-22-sync-infra-and-ad-design.md`](../superpowers/specs/2026-04-22-sync-infra-and-ad-design.md) — sync infrastruktura (nahrazuje Hangfire z ticketing-integration.md)
- [`docs/superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md`](../superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md) — aktuální implementační plán pro rozšíření entit a query service

## 10. Změnová historie

| Datum | Změna | Zdroj |
|---|---|---|
| 2026-04-23 | První verze — konsolidace znalostí získaných při discovery SQL + 3 ukázkových ticketů | Brainstorming session, [plán Sprint A](../superpowers/plans/2026-04-23-sd-integrace-sprint-a-backend.md) |
| 2026-11-11 | §5.5 prodlení podle penalizačního algoritmu ServiceDesku, nová §5.13 databáze ServiceDesku (`ticket_external*`), §7 „Penále v Kč" | [spec 2026-10-15](../superpowers/specs/2026-10-15-dashboard-prestavba-prodleni-statistiky-design.md), [plán](../superpowers/plans/2026-10-15-dashboard-prestavba-prodleni-statistiky.md) |
