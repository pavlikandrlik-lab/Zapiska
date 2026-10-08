# Vyhledávání — přestavba na záznamocentrické hledání nad ostrými tabulkami

**Datum:** 2026-09-17
**Stav:** návrh k odsouhlasení
**Trigger:** zadání uživatele 2026-09-17 — „stávající verzi vyhledávání smazat a předělat to […] začal bych to stavět od znovu pořádně, hezky strukturovaně a ne moc složitě". Bezprostřední záminkou byl bug z 2026-09-08 (vyhledávání nefungovalo vůbec), jehož oprava odhalila, že problém je návrh, ne jedna vada.

---

## 1. Proč se přestavuje

### 1.1 Dnešní stav

Vyhledávání existuje **dvakrát**, s odlišným rozsahem i odlišnou autorizací:

| | Dropdown (`DbSuggestService`) | Stránka `/Search/Index` (`GlobalSearchService`) |
|---|---|---|
| Zdroj | EF Core `LIKE` přímo v tabulkách | tabulka `dbo.SearchIndex` plněná na pozadí |
| Rozsah | 2 entity (záznamy, vyjádření) | 7 entit |
| Autorizace | `WHERE` nad `VisibleProjectIds` | pre-filtr + post-filtr `SearchAcl` |
| Skupiny | ne | ano |

Dvě implementace znamenají dvě sady pravidel, které se rozcházejí, a dvojí místo, kde se opravuje totéž.

### 1.2 Vady, které se tím odstraňují

**V1 — autorizace pod-reportuje.** Pre-filtr používá výhradně `VisibleProjectIds` (plněno z `ObsazeniProjektu`), zatímco autoritativní pravidlo `CurrentUserContextViewModel.CanAccessProject` je širší:

```
CanAccessProject = IsSuperAdmin
                 ∪ VisibleProjectIds
                 ∪ globální klíč splňující PermissionKeys.GrantsProjectRead
                 ∪ per-projektový grant z AuthorizationSnapshot.PerProjectPermissions
```

Kdo má přístup přes **roli** a ne přes obsazení projektu, dostane v SQL ořezáno a vidí méně, než na co má právo. Stejná třída vady jako commit `55625b2 fix(authz): projects.read.all reálně grantuje čtení všech projektů`. `DbSuggestService` má tutéž vadu, dokonce bez fallbacku na snapshot.

**V2 — prázdná množina znamená žádný filtr.** Podmínka je `PreFilterProjectIds is { Count: > 0 }`. Uživateli bez projektů se do SQL nepošle žádné omezení; bezpečnost pak drží jen post-filtr.

**V3 — post-filtr běží až po `TOP N`.** Počty nalezených jsou nepřesné a „další výsledky" nespolehlivé.

**V4 — index může zestárnout.** `SearchIndex` se plní na pozadí z audit logu. Selhání reindexu se projeví jako „záznam nejde najít", tedy vada, která vypadá jako chyba v datech.

**V5 — fulltext na produkci neexistuje.** Na produkčním SQL Serveru 2022 vrací `SERVERPROPERTY('IsFullTextInstalled')` hodnotu `0` a `sys.fulltext_languages` je prázdná; komponenta Full-Text Search se instalovat nebude (rozhodnutí uživatele 2026-09-17, intranet bez internetu — instalační médium a restart instance by znamenaly change request). Indexová vrstva tak stojí na technologii, kterou cílové prostředí nemá a mít nebude.

---

## 2. Rozhodnutí

### 2.1 Jednotka výsledku = záznam

Jedna položka výsledku odpovídá jednomu **projektovému záznamu**. Vyjádření a externí odkazy jsou *prohledávaná pole* záznamu, ne samostatné výsledky. Kliknutí vede vždy na záznam.

Důvod: datový model to přímo podporuje — vyjádření i externí odkazy visí na záznamu, ne na projektu zvlášť:

```
projektove_zaznamy (projekt_id → autorizace)
   ├── vyjadreni             (zaznam_id) → text_vyjadreni
   └── zaznam_externi_odkazy (zaznam_id) → cislo
```

### 2.2 Rozsah prohledávaných polí

| Tabulka | Sloupce | Typ |
|---|---|---|
| `projektove_zaznamy` | `nazev` | `nvarchar(255)` |
| | `cil` | `nvarchar(500)` |
| | `popis` | **`text`** (legacy LOB) |
| | `cislo_viditelne`, `cislo_zaznamu` | `nvarchar(32)`, `int` |
| `vyjadreni` | `text_vyjadreni` | **`text`** (legacy LOB) |
| `zaznam_externi_odkazy` | `cislo` | `nvarchar(255)` |

**Nehledá se:** osoby, subsystémy, jednání, projekty, návrhy, `zaznam_externi_odkazy.pozadavek`, názvy z číselníků. (Vědomé rozhodnutí uživatele 2026-09-17 — číselníky a osoby produkují šum a patří spíš do filtrů.)

### 2.3 Autorizace

**Jediné pravidlo:** uživatel vidí záznam právě tehdy, když vidí projekt, ve kterém záznam leží — v plném rozsahu `CanAccessProject` včetně rolí (potvrzeno uživatelem 2026-09-17).

Vynucení:

1. Spočítat množinu viditelných projektů **jednou**, vzorem `JednaniController.BuildMeetingOverviewProjectFilter`:
   - superadmin nebo globální `GrantsProjectRead` → `null` = **bez omezení** (ne prázdný seznam)
   - jinak `VisibleProjectIds ∪ PerProjectPermissions` s klíčem `GrantsProjectRead`, distinct
2. Aplikovat jako `WHERE projekt_id IN (…)` **v témže dotazu**, nikoli post-filtrem.
3. Prázdná množina ≠ žádný filtr. Prázdná množina znamená **žádné výsledky** — tím padá V2.

Post-filtr se neimplementuje; nedostupný záznam se z databáze nevrátí. `SearchAcl` zaniká.

### 2.4 Úložiště: ostré tabulky, žádný index

Čte se přes EF Core rovnou z `projektove_zaznamy` s `EXISTS` podmínkami na vyjádření a externí odkazy.

Zvažovaná alternativa (denormalizovaná indexová tabulka) byla zamítnuta: při objemu **stovky až nízké tisíce záznamů** (potvrzeno uživatelem) je scan v jednotkách až desítkách milisekund, zatímco index by si vyžádal reindex službu, checkpoint, bootstrap a možnost zestárnutí — tedy vadu V4, kvůli které se přestavuje.

Hranice, při které se toto rozhodnutí musí revidovat: **~100 000 záznamů**.

### 2.5 Diakritika a zástupné znaky

Databáze má collation `Czech_CI_AS` — nerozlišuje velikost písmen, ale **rozlišuje diakritiku**. Bez zásahu by „zalohovani" nenašlo „Zálohování".

- Každý `LIKE` se vynucuje přes `COLLATE Latin1_General_CI_AI`. Ověřeno empiricky, že to funguje **i na sloupcích typu `text`** bez nutnosti `CAST`.
- **Ne `Czech_CI_AI`.** Čeština bere `č ř š ž` jako samostatná písmena abecedy, ne jako diakritické varianty — mají vlastní primární váhu, kterou akcent-necitlivost minout nemůže. Pod `Czech_CI_AI` by „rizeni" nenašlo „Řízení". Ověřeno na SQL Serveru 2026-09-21.
- V EF Core se zapisuje jako `EF.Functions.Like(EF.Functions.Collate(x, "Latin1_General_CI_AI"), pattern)`.
- Vstup se escapuje (`\` `%` `_` `[`) a `LIKE` dostane `ESCAPE '\'`, aby dotaz „50 %" hledal doslova „50 %".
- Víceslovný dotaz: všechna slova musí padnout (AND mezi slovy, OR mezi poli), max 6 slov.
  Slova kratší než 3 znaky se vedle delších vynechají z hledání, z odkazu `hl` i z podsvícení
  (`SearchQueryText.MinTermLength`, `searchHighlight.js`); dotaz jen z krátkých slov se hledá
  celý. Doplněno 2026-10-08 — spojka „a“ rozsvítila každé „a“ na kartě.
- Fráze (2026-10-08, jako Google): text v uvozovkách `"…"`/`„…“` je jeden výraz hledaný
  jako celek (LIKE `%fráze%`), vždy se hledá i krátký; slova mimo uvozovky beze změny.
  Neuzavřená fráze běží do konce dotazu. `hl` nese fráze v uvozovkách
  (`SearchQueryText.ToHighlightQuery`), podsvícení je rozkládá stejně (`highlightTerms`).
  Omezení: fráze přes HTML formátování v popisu/vyjádření se nenajde. Od 2026-10-08 se
  hledá nad sloupci čistého textu (`popis_prosty_text`, `text_vyjadreni_prosty_text`;
  `pozadavek_prosty_text` připravený pro výzvy), plněnými háčkem `RichTextSearchTextSync`
  při uložení a jednorázovým dopočtem při startu. Omezení „fráze přes formátování“ odpadá.
- **Minimální délka dotazu: 3 znaky.**

### 2.6 Řazení

Vzestupně, texty i čísla. Žádné skórování relevance — uživatel rozhodl 2026-09-17, že předvídatelné pořadí je přednější než odhad relevance.

---

## 3. Dropdown

### 3.1 Odchylka od Design systému

DS FIS předepisuje v hlavičce `<gov-form-autocomplete>`, která si seznam kreslí sama. Plní se ale plochými řetězci (`ac.options = [{ name: "…" }]`) a nemá API pro vlastní vykreslení položky. Požadované rozvržení (dva řádky, metadata vpravo, zvýraznění shody) se do toho nevejde.

Podle `DesignSystem-FIS-v1.0.0/README.md`, pravidla 4, se proto zavádí **dokumentovaná lokální odchylka**:

- Vyhledávací **pole** zůstává gov (`gov-form-search` + `gov-form-input`).
- **Seznam výsledků** kreslí aplikace sama v `.app-search-dropdown`, tedy mimo soubory Design systému, s prefixem `app-`.
- Soubory `assets/gov/**` ani `assets/ds-fis/*` se needitují — pravidlo 1 DS platí bez výjimky.

### 3.2 Rozvržení

Sedm výsledků, vždy **dva řádky**, dvousloupcově zhruba **80 % / 20 %**.

| Typ shody | 1. řádek (80 %) | 1. řádek (20 %) | 2. řádek (80 %) | 2. řádek (20 %) |
|---|---|---|---|---|
| název záznamu | název záznamu | zkratka subsystému | **týž název se žlutě zvýrazněnou shodou** | — |
| vyjádření | název záznamu | zkratka subsystému | 2 slova · **žlutá shoda** · 2 slova | číslo jednání |
| popis | název záznamu | zkratka subsystému | 2 slova · **žlutá shoda** · 2 slova | „popis" |
| externí odkaz | název záznamu | zkratka subsystému | číslo odkazu | „Ext. záz." |

Chování je u všech čtyř typů shodné a předvídatelné: první řádek vždy identifikuje záznam, druhý vždy ukazuje, **proč** se našel.

### 3.3 Chování

- Spouští se od 3 znaků, debounce **150–200 ms**, `AbortController` ruší předchozí dotaz.
  (Debounce se ponechává: bez něj by dotaz „zalohovani" poslal 10 požadavků místo jednoho a rychlý písař by narazil na rate limit 30 požadavků / 10 s — `Program.cs:47-50`.)
- Klávesnice zůstává: `Escape` zavře, `ArrowUp`/`ArrowDown` prochází, `Enter` naviguje.
- Bez výsledků: jednořádková hláška v dropdownu.

---

## 4. Stránka výsledků

Nová stránka na URL `/Search/Index`, postavená na gov šabloně „Výsledky vyhledávání":

- `<gov-container id="main">` → `gov-layout type="aside" variant="left"`
- `gov-page-heading` s dotazem a počtem nalezených
- `gov-card-grid` / `gov-card direction="horizontal"` na položku
- `gov-pagination` (mobil) + „Načíst dalších N" (desktop)

**Kategorie jsou objektový model.** Dnes existuje jediná kategorie — *Záznamy*. Výsledek se přesto modeluje jako kolekce kategorií, aby přidání další (osoby, jednání…) bylo doplněním implementace kategorie, ne přepisem stránky.

Zvýraznění shody: **žlutě**, shodně s dropdownem. Gov šablona používá `<b>`; jednotný vzhled napříč vyhledáváním má přednost a spadá pod tutéž lokální odchylku (§3.1).

Rozsah a autorizace jsou **totožné** s dropdownem — stránka není „bohatší hledání", jen jiné zobrazení téhož výsledku. Vyjádření se na ní vykreslují bohatěji než v dropdownu.

Filtry vlevo (které gov šablona předpokládá) se zatím nenaplňují — není podle čeho filtrovat, dokud je kategorie jediná. Místo v rozvržení zůstává připravené.

---

## 5. Co se ruší

| Vrstva | Objekty |
|---|---|
| Tabulky | `dbo.SearchIndex`, `dbo.search_reindex_checkpoint` |
| Migrace | `db_upgrade_1_4_4_search_index.sql` (nikdy nenasazená) + nová migrace rušící `search_reindex_checkpoint` |
| Služby | `SearchReindexHostedService`, `SearchIndexer`/`ISearchIndexer`, `EntityDocumentMapper`, `ISearchClient`, `SqlServerSearchClient`, `OpenSearchClient`, `OpenSearchIndexSettings`, `SearchDocument`, `SearchProvisioningRemedy`, `SearchAcl`, `IEmbeddingService` + obě implementace |
| Endpointy | `POST /Search/Reindex`, `GET /Search/Status` |
| UI | admin karta vyhledávání v `Views/Profil/Index.cshtml` |
| Oprávnění | klíč `search.reindex` — včetně úklidu v authz seedu, vzorem `db_upgrade_1_4_1_drop_schedule_preview.sql` |
| Konfigurace | `PmTracker:Search` — zůstává nanejvýš `Enabled` |
| Entity | `SearchReindexCheckpointEntity` + `DbSet` |

Složka `PmTracker.Web/Services/Search/` má dnes **23 souborů**. Po přestavbě zůstane jádro v řádu jednotek: služba sestavující dotaz, její rozhraní, kontrakty výsledku a DI registrace.

---

## 6. Testy

| Úroveň | Co se ověřuje |
|---|---|
| Unit | rozdělení dotazu na slova, escapování zástupných znaků, výpočet výřezu „2 slova + shoda + 2 slova", klasifikace typu shody |
| Integration (reálná DB) | diakritika (`zalohovani` → `Zálohování`) · všechna slova musí padnout · zástupné znaky doslova · **autorizace: člen projektu, držitel role, superadmin, uživatel bez projektů** · limit 7 · řazení |
| Api | dropdown endpoint vrací 200 a očekávaný tvar · stránka se vykreslí · práh 3 znaků |
| E2E | psaní otevře dropdown, Escape zavře, šipky procházejí, Enter naviguje |

Integrační test autorizace musí pokrýt zejména **držitele role bez obsazení v projektu** — to je vada V1, která dnes v testech chybí a proto nebyla odhalena.

Test diakritiky musí ověřit, že běží nad akcent-citlivou collation (`_AS`), jinak nic nedokazuje.

---

## 7. Mimo rozsah

- **Sjednocení hlavičky, navigace, page-headingu a patičky s DS FIS** a upgrade gov `4.2.9 → 4.7.0`. Samostatný úkol **po** vyhledávání (rozhodnuto 2026-09-17). Jde o výměnu markupu napříč layoutem, ~27 CSS pravidel a 8 testovacích souborů; upgrade gov v tomto projektu už jednou rozbil dialogy.
- Filtry na stránce výsledků.
- Fulltextové vyhledávání (komponenta na produkci není a nebude).

---

## 8. Otevřené body

Žádné blokující. Body k potvrzení při návrhu implementace:

1. Text hlášky při nulovém výsledku.
2. Jak se z dropdownu přejde na stránku výsledků (položka „zobrazit všechny", nebo `Enter` bez vybrané položky).
3. Kolik výsledků na stránku a velikost dávky pro „Načíst dalších N".
