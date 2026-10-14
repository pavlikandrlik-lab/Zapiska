# Změna stavu úkolu platná k jednání — design

**Datum:** 2026-10-13
**Stav:** schválen uživatelem 2026-10-14, implementační plán `docs/superpowers/plans/2026-10-14-zmena-stavu-k-jednani.md`
**Podnět:** zápis z jednání 8.10 se zpracovával až 13.10. Úkol ukončený při zpracování má v tisku jednání 8.10 ve sloupci Stav „Ukončeno", ale není podbarvený. V přípravě jednání na 22.10 podbarvený je.
**Souvisí:** `2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md`. Tento návrh **ruší jeho rozhodnutí U6** („sloupec Stav se neřeší") a jeho §9 („Mimo rozsah").

## 1. Cíl

1. Při změně stavu úkolu uživatel zvolí, **ke kterému datu změna platí**: dnešní datum, nebo datum jednání, ke kterému změna patří.
2. Tisk jednání ukazuje stav úkolu **ke dni jednání** v celém řádku: sloupec Stav, podbarvení ukončeného i pozastaveného úkolu. Text a barva si už nikdy neodporují.

## 2. Co platí dnes

| Zjištění | Místo |
|---|---|
| Historie stavů ukládá jen okamžik uložení, a to v **UTC** | `RecordService.SaveRecord.cs:178-187` (`DatumZmeny = timeProvider.GetUtcNow().UtcDateTime`) |
| Datum, ke kterému změna platí, nejde zvolit. Pozdě zapsaná změna tak „patří" ke dni zápisu | totéž |
| Tisk jednání odvozuje stav k datu jednání zpětným odvíjením historie od dnešního stavu. Odvinou se změny s datem **po** datu jednání | `ExportProjectionBuilders.cs:235-253` (`ResolveTaskStatusAtDate`) |
| Odvozený stav používá jen **viditelnost** řádku a **podbarvení ukončeného** | `ExportProjectionBuilders.cs:822-842` |
| **Sloupec Stav** a **pozastavení** se berou z dnešního stavu | `ExportProjectionBuilders.cs:939` a výpočet `IsPaused` |
| UTC čas změny se porovnává s místním datem jednání. Změna uložená mezi 0:00 a 2:00 místního času tak spadne do předchozího dne | `ExportProjectionBuilders.cs:243` |
| Dashboard bere „datum dokončení" jako nejpozdější `DatumZmeny` přechodu do koncového stavu | `ZakladniDatasetLoader.cs:51-62` |
| Uzavřené jednání se v celé aplikaci pozná jedním pravidlem: kód `CLOSED`, název obsahuje „uzav", nebo je jednání zamčené (`UzamklOsobaId`) | `Services/Data/MeetingStatePolicy.cs` |
| Stavy jednání: `DRAFT` = Příprava, `OPEN` = Otevřeno pro zápis, `CLOSED` = Uzavřeno | `db_seed_dev_admin.sql:106-110`, používané v kódu |
| Uzavřené jednání nejde upravit, a tedy ani přeplánovat. Smazání jednání smaže i jeho účast a vyjádření | `MeetingService.WriteCommands.cs:35-38`, `:80-117` |
| Stav úkolu se mění jen v editoru záznamu, obyčejným `<select name="Stav">` | `Views/Projekty/_EditZaznamBasicPanel.cshtml:71-86` |

## 3. Rozhodnutí uživatele (diskuse 2026-10-13)

| # | Rozhodnutí |
|---|---|
| Z1 | Volba „platí k" se týká **každé změny stavu**, nejen ukončení. Platí i pro znovuotevření a pozastavení. |
| Z2 | Nabízí se dnešní datum a jednání **od nejnovějšího po nejstarší**, jen ve stavu Příprava nebo Otevřeno pro zápis. **První uzavřené jednání nabídku ukončí.** |
| Z3 | Změna navázaná na jednání **jde s jednáním**: přeplánování jednání posune i datum změny. Data se musí hlídat tak, aby nevznikly dva rozporné údaje. |
| Z4 | Předvybrané je **nejnovější jednání ve stavu Otevřeno pro zápis**. |
| Z5 | Dashboard převezme nové datum jen **minimální změnou**. Celý dashboard se později předělá. |
| Z6 | U6 se řeší: v tisku jednání se sloupec Stav i pozastavení berou ke dni jednání. |

Doplněno v návrhu (k potvrzení při revizi):

| # | Doplnění | Proč |
|---|---|---|
| D1 | Změna stavu nesmí platit **dříve než předchozí změna stavu** téhož úkolu (§4.4) | Bez toho zpětný přepočet stavu vrací nesmysly |
| D2 | Změna stavu nesmí platit **před datem založení** záznamu | Stav úkolu před jeho založením nedává smysl |
| D3 | Skutečný okamžik uložení (`datum_zmeny`) **zůstává beze změny** | Audit: vždy je vidět, kdy změna opravdu proběhla |

## 4. Pravidla

### 4.1 Kdy se volba zobrazí

Volba „Změna stavu platí k" se v editoru záznamu zobrazí, **jakmile uživatel vybere jiný stav úkolu, než má záznam uložený**. Když vrátí původní stav, volba zmizí.

Server volbu použije jen tehdy, když se stav skutečně mění (vzniká řádek historie). Jinak ji ignoruje.

Při **zakládání** záznamu se volba nezobrazuje. Založení nezapisuje řádek historie stavů (§10).

### 4.2 Nabídka

```
nabídka = [ dnešní datum ]
        + jednání projektu seřazená podle čísla jednání sestupně:
            - je-li jednání uzavřené (MeetingStatePolicy.IsReadOnly) → KONEC, žádné další
            - je-li ve stavu DRAFT nebo OPEN                       → nabídnout
            - jinak (neznámý stav)                                 → přeskočit
        − volby s datem dřívějším než spodní mez (§4.4)
```

**Příklad ze zadání.** Dnes je 13.10.

| Jednání | Stav | Datum | Nabídne se? |
|---|---|---|---|
| — | — | dnes 13.10 | ano |
| 889 | Příprava | 22.10 | ano |
| 888 | Otevřeno pro zápis | 8.10 | ano (předvybráno, §4.3) |
| 887 | Otevřeno pro zápis | 1.10 | ano |
| 886 | Uzavřeno | 24.9 | **ne — tady nabídka končí** |
| 885 | Uzavřeno | 17.9 | ne |
| 884 | Otevřeno pro zápis | 10.9 | **ne**, i když je otevřené: leží za uzavřeným 886 |

Text voleb:
- `Dnes (13.10.2026)`
- `Jednání č. 889 – 22.10.2026 (příprava)`
- `Jednání č. 888 – 08.10.2026`

### 4.3 Výchozí volba

1. Nejnovější nabízené jednání ve stavu **Otevřeno pro zápis** (v příkladu 888).
2. Pokud žádné takové není, **dnešní datum**.
3. Pokud ani to nejde (vyřadila ho spodní mez), **první nabízená volba**.

### 4.4 Pořadí změn — spodní mez

```
spodní mez = MAX( plati_od poslední změny stavu tohoto záznamu,
                  datum založení záznamu z ukládaného formuláře )
```

Nabídnou se jen volby s datem **≥ spodní mez**. Stejné datum se nabídne: změny téhož dne se řadí podle pořadí uložení.

Při sestavení nabídky v editoru se použije uložené datum založení. Při ověření na serveru se použije datum založení z odeslaného formuláře, protože ho uživatel mohl v editoru změnit.

Bez spodní meze by zpětný přepočet (§4.7) dával nesmysly. Příklad: úkol ukončený k jednání 889 (22.10) a 14.10 znovuotevřený „dnes" by v tisku jednání 888 vyšel jako ukončený, přestože k 8.10 běžel.

Důsledek pro opravu omylu: úkol omylem ukončený k 889 jde znovuotevřít zase jen k 889 nebo později. Obě změny pak platí ke stejnému dni a v žádném tisku se úkol neukáže jako ukončený. To je žádoucí výsledek.

Když spodní mez vyřadí všechny volby, editor ukáže hlášku z §7 a stav nejde změnit. Nastane to jen výjimečně: poslední změna je navázaná na budoucí jednání, které mezitím někdo uzavřel.

### 4.5 Co se uloží

Každý nový řádek historie stavů dostane:

| Sloupec | Volba „dnes" | Volba „jednání X" |
|---|---|---|
| `datum_zmeny` | okamžik uložení (UTC), beze změny | okamžik uložení (UTC), beze změny |
| `plati_od` | dnešní **místní** datum | datum jednání X |
| `jednani_id` | `NULL` | id jednání X |

**Jediný zdroj pravdy je `plati_od`.** Tisk, dashboard i kontrola pořadí čtou jen tento sloupec a nikdy datum z jednání. `jednani_id` neslouží ke čtení. Je to jen vazba, přes kterou se `plati_od` udržuje aktuální (§4.6). Tím nevznikají dva údaje, které by si mohly odporovat.

Server nabídku při uložení **sestaví znovu** a zvolenou hodnotu ověří proti ní (ochrana proti podvržení a proti uzavření jednání mezi otevřením editoru a uložením).

### 4.6 Přeplánování a smazání jednání

**Přeplánování** (změna `DatumPlanovane` v `SaveMeetingAsync`) proběhne v jedné transakci takto:
1. Všem řádkům historie s `jednani_id` = toto jednání se nastaví `plati_od` = nové datum.
2. Pro každý dotčený záznam se ověří pořadí §4.4: seřazeno podle `id` nesmí `plati_od` klesat.
3. Pokud by pořadí porušilo, **uložení jednání se odmítne** hláškou z §7, která vyjmenuje dotčené záznamy. Nic se neuloží.

Uzavřené jednání přeplánovat nejde ani dnes, takže pravidlo se týká jen jednání v přípravě nebo otevřených.

**Smazání jednání:** dotčené řádky historie dostanou `jednani_id = NULL` a **`plati_od` si ponechají**. Změna stavu pak dál platí k datu, které mělo jednání v okamžiku smazání. Vazbu ruší `DeleteMeetingAsync` stejně, jako už dnes ruší účast a vyjádření. Cizí klíč v databázi je `NO ACTION` a slouží jen jako pojistka.

**Změna čísla jednání** na `plati_od` vliv nemá.

### 4.7 Stav k datu jednání

Stejný algoritmus jako dnes, jen nad `plati_od` místo `datum_zmeny`:

```
stav = dnešní stav záznamu
pro každou změnu seřazenou podle (plati_od sestupně, id sestupně):
    je-li plati_od > datum jednání a stav == nový stav změny:
        stav = původní stav změny
```

Změna s `plati_od` **rovným** datu jednání se počítá jako už platná, stejně jako dnes.

### 4.8 Tisk jednání — celý řádek ke dni jednání (U6)

| Údaj | Tisk jednání | Tisk projektu a záznamu |
|---|---|---|
| Viditelnost řádku | stav k datu jednání (beze změny) | — |
| **Sloupec Stav** | **stav k datu jednání** (nově) | dnešní stav (beze změny) |
| **Pozastavení** (krémové podbarvení) | **stav k datu jednání** (nově) | dnešní stav (beze změny) |
| Ukončení (modré podbarvení) | stav k datu jednání (beze změny) | dnešní stav (beze změny) |

Všechny tři údaje se v tisku jednání odvodí **z jednoho stavu** spočteného podle §4.7. Rozpor mezi textem a barvou tak vzniknout nemůže. Přednost pozastavení před ukončením (spec 2026-09-05, §4.3) platí dál.

**Příklad ze zadání.** Úkol se ukončuje 13.10 při zpracování zápisu z jednání 888.

| Zvoleno při ukončení | Tisk 887 (1.10) | Tisk 888 (8.10) | Tisk 889 (22.10) |
|---|---|---|---|
| jednání 888 | Rozpracováno | **Ukončeno, modře** | řádek se nevytiskne (ukončen už k minulému jednání) |
| dnes (13.10) | Rozpracováno | Rozpracováno | **Ukončeno, modře** |
| jednání 889 | Rozpracováno | Rozpracováno | **Ukončeno, modře** |

Dnešní aplikace (bez volby, změna platí ke dni uložení 13.10) ukáže v tisku 888 „Ukončeno" bez podbarvení. To je nahlášený rozpor.

### 4.9 Dashboard

`ZakladniDatasetLoader` vezme jako datum dokončení `MAX(plati_od)` místo `MAX(datum_zmeny)`. Jiné úpravy dashboardu se nedělají (Z5).

## 5. Databáze

Nový skript `db_upgrade_1_4_10_zmena_stavu_plati_od.sql`, idempotentní podle vzoru předchozích:

1. **Kontrola verze:** `AT TIME ZONE` vyžaduje SQL Server 2016 nebo novější (`SERVERPROPERTY('ProductMajorVersion') >= 13`). Na starší verzi skript skončí srozumitelnou chybou a nic nezmění.
2. `ALTER TABLE dbo.zaznam_historie_stavu_zaznamu ADD plati_od DATE NULL`.
3. Doplnění existujících řádků převodem z UTC na místní datum:
   `plati_od = CAST((datum_zmeny AT TIME ZONE 'UTC') AT TIME ZONE 'Central Europe Standard Time' AS date)`.
   Tím se zároveň opraví posun o den u změn uložených mezi 0:00 a 2:00 místního času.
4. `ALTER COLUMN plati_od DATE NOT NULL` + výchozí hodnota `DF_zaznam_historie_stavu_zaznamu_plati_od` = dnešní datum serveru. Skript běží před nasazením, takže stará verze aplikace (mezi skriptem a nasazením i při návratu na starou verzi) `plati_od` nezná. Bez výchozí hodnoty by jí každá změna stavu spadla na NOT NULL. Nová verze `plati_od` vždy posílá.
5. `ADD jednani_id INT NULL` + cizí klíč na `dbo.jednani(id)` s `ON DELETE NO ACTION` (§4.6).
6. Index `IX_zaznam_historie_stavu_zaznamu_jednani_id` pro přeplánování a mazání jednání. Obyčejný, ne filtrovaný: filtrovaný index vyžaduje `QUOTED_IDENTIFIER ON` a `sqlcmd` ho bez přepínače `-I` nezapíná.

Dále:
- doplnit skript do `db_check_applied_upgrades.sql`,
- doplnit oba sloupce do seznamu povinných sloupců ve `SqlStartupValidatorHostedService` (vzor `prosty_text`) s hláškou, která odkáže na skript.

Existující řádky mají `jednani_id = NULL`. Jejich `plati_od` odpovídá dnešnímu chování, jen bez chyby s časovým pásmem.

## 6. Rozhraní

V `_EditZaznamBasicPanel.cshtml` hned za pole „Stav úkolu":

```
Změna stavu platí k   [ Jednání č. 888 – 08.10.2026        ▾ ]
```

- Obyčejný `<select name="StavPlatiK">`, stejně jako sousední pole Stav. Hodnota `""` = dnes, jinak id jednání.
- Pole je skryté (`hidden`), dokud se vybraný stav neliší od uloženého (§4.1). Přepínání řeší nový modul `recordEditor/statusEffectiveDate.js` funkcí `initStatusEffectiveDate(scope)`. Volá ji `initRecordFormEnhancements(scope)` při každém vložení editoru do stránky, stejně jako ostatní `init…` funkce editoru. Pole se neklonuje, takže delegace z `document` není potřeba.
- `.record-form-row` má `display: grid`, který atribut `hidden` přebije. Proto se do `site.css` doplní pravidlo `.record-form-row[hidden] { display: none; }`.
- Je-li stav zamčený (`metadataLocked`), stav se změnit nedá, a volba se tedy nezobrazí.
- Nabídku a výchozí volbu připraví server do view modelu editoru (§4.2, §4.3). JS ji jen zobrazuje a skrývá.

## 7. Chybové hlášky

| Situace | Hláška |
|---|---|
| Zvolené jednání je uzavřené nebo leží za uzavřeným (mezitím ho někdo uzavřel) | „Jednání č. {číslo} teď nejde zvolit — je uzavřené nebo leží za uzavřeným jednáním. Vyberte znovu, k čemu změna stavu platí." |
| Zvolené jednání neexistuje nebo nepatří do projektu (smazané, podvržené) | „Vybrané jednání neexistuje nebo bylo smazáno. Vyberte znovu, k čemu změna stavu platí." |
| Zvolené datum je před spodní mezí (podvržený požadavek, nebo se v témže uložení posunulo datum založení) | „Změna stavu nemůže platit dříve než {datum} (předchozí změna stavu nebo založení záznamu)." |
| Spodní mez vyřadila všechny volby | „Stav teď nejde změnit: změna nemůže platit dříve než {datum} (předchozí změna stavu nebo založení záznamu) a žádné nabízené datum není pozdější." |
| Přeplánování jednání by porušilo pořadí změn | „Jednání nelze přesunout na {datum}: změna stavu záznamů č. {seznam} navázaná na toto jednání by platila mimo pořadí ostatních změn stavu těchto záznamů. Ponechte původní datum, nebo zvolte datum mezi sousedními změnami." |

## 8. Architektura a soubory

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Vytvořit | `db_upgrade_1_4_10_zmena_stavu_plati_od.sql` | §5 |
| Změnit | `db_check_applied_upgrades.sql` | registrace skriptu |
| Změnit | `Services/Data/SqlStartupValidatorHostedService.cs` | povinné sloupce |
| Změnit | `Models/Entities/PmTrackerEntities.cs` | `PlatiOd`, `JednaniId` na `ZaznamHistorieStavuZaznamuEntity` |
| Změnit | `Data/Configuration/RecordEntityConfiguration.cs` | mapování (`plati_od` jako `date`), index |
| Vytvořit | `Services/Records/StatusChangeEffectiveDatePolicy.cs` | **čistá funkce**: nabídka (§4.2), výchozí volba (§4.3), spodní mez (§4.4), ověření volby. Používá `MeetingStatePolicy.IsReadOnly`. |
| Vytvořit | `Services/Records/StatusChangeEffectiveDateQuery.cs` | načtení jednání projektu a poslední `plati_od` záznamu, sestavení nabídky přes politiku (vzor `RecordVersionQuery`) |
| Změnit | `Services/RecordService.SaveRecord.cs` | ověření volby ve `ValidateRecordSaveCommandAsync` (stejně jako `JednaniIdProCislo`, chyba u pole `StavPlatiK`), zápis `plati_od` a `jednani_id`, hlášky §7 |
| Změnit | `Models/ViewModels/Projekty/ZaznamEditViewModels.cs`, `ProjectService.RecordEditorComposition.cs` | nabídka a výchozí volba do editoru |
| Změnit | `Models/ViewModels/Commands/RecordCommands.cs` | pole `StavPlatiK` v `SaveRecordCommand` |
| Změnit | `Views/Projekty/_EditZaznamBasicPanel.cshtml`, `wwwroot/css/site.css` | §6 |
| Vytvořit | `wwwroot/js/modules/recordEditor/statusEffectiveDate.js` (+ volání v `recordEditor/index.js`) | §6 |
| Změnit | `Services/MeetingService.WriteCommands.cs` | přeplánování (§4.6), zrušení vazby při smazání jednání |
| Změnit | `Services/Export/ExportProjectionBuilders.cs` | přepočet nad `plati_od`, řazení `(plati_od, id)`, sloupec Stav a pozastavení ke dni jednání (§4.8) |
| Změnit | `Services/ProjectDashboard/Zakladni/ZakladniDatasetLoader.cs` | `MAX(plati_od)` (§4.9) |
| Změnit | `docs/wiki/export/tisk-jednani.md` | jak tisk ukazuje stav úkolu |
| Změnit | `docs/superpowers/specs/2026-09-05-ukoncene-ukoly-podbarveni-tisk-design.md` | poznámka u U6 a §9, že je řeší tento návrh |

**Evaluátor tisku** dostane jednu veřejnou metodu `ResolveStatusForMeetingPrint(...) → int?`, tedy stav k datu jednání. Z ní se v `BuildExportRecordsAsync` odvodí název stavu, `IsPaused` i `IsCompleted`. Odpadne tím možnost, že by se tři údaje počítaly každý jinak. `IsCompletedForMeetingPrint` se nahradí touto metodou.

## 9. Testy

| Vrstva | Test |
|---|---|
| Unit | `StatusChangeEffectiveDatePolicy`, **příklad ze zadání**: 889 příprava, 888 a 887 otevřené, 886 a 885 uzavřené, 884 otevřené → nabídka `[dnes, 889, 888, 887]`, výchozí 888 |
| Unit | zamčené jednání (`UzamklOsobaId`) nabídku ukončí stejně jako `CLOSED` |
| Unit | spodní mez vyřadí starší volby; volba se stejným datem zůstane; datum založení funguje jako mez |
| Unit | výchozí volba: bez otevřeného jednání → dnes; dnes vyřazené → první volba |
| Unit | evaluátor nad `plati_od`: změna zapsaná 13.10 s `plati_od` 8.10 → stav k 8.10 je ukončený |
| Unit | evaluátor: stejné `plati_od` u dvou změn → rozhoduje `id` (ukončení a hned znovuotevření = neukončený) |
| Integrace | uložení záznamu s volbou jednání → řádek historie má `jednani_id`, `plati_od` = datum jednání, `datum_zmeny` = okamžik uložení |
| Integrace | podvržené id uzavřeného jednání → odmítnuto, nic se neuloží |
| Integrace | přeplánování jednání posune `plati_od`; přeplánování porušující pořadí se odmítne a nic se neuloží |
| Integrace | smazání jednání → `jednani_id = NULL`, `plati_od` zůstane |
| Integrace | **tabulka z §4.8**: tisk 887, 888 a 889 pro všechny tři volby, kontroluje se `Stav`, `IsCompleted` i viditelnost |
| Integrace | úkol pozastavený až po jednání → v tisku toho jednání není krémový a sloupec Stav ukazuje stav k datu jednání |
| Api | editor záznamu vykreslí `name="StavPlatiK"` s volbami a předvybraným jednáním. Kontrola jen přes atributy, ne český text (Razor kóduje diakritiku). |
| Prohlížeč | Playwright proti běžící aplikaci: volba se ukáže po změně stavu, zmizí po vrácení stavu, uložení projde. Snímky obrazovky. |

Startovní validátor a databázový skript se ověří spuštěním na lokální DB (se zálohou).

## 10. Mimo rozsah

- **Pravidla otevírání a uzavírání jednání** (otevřít jde jen poslední uzavřené, uzavřít nejde, když jsou starší neuzavřená). Uživatel 2026-10-14 rozhodl, že se **dělat nebudou**. Návrh na nich nezávisí: nabídka se řídí jen tím, které jednání je uzavřené, a otevřená jednání za uzavřeným (v příkladu 884) se nenabídnou.
- **Oprava už uložených změn**, například ukončení z 13.10, které patří k jednání 888. Editor mění jen budoucí změny. Jednorázově jde opravit ručním `UPDATE` sloupce `plati_od` a `jednani_id`. Na vyžádání připravím hotový skript.
- **Stav při založení záznamu.** Založení nezapisuje řádek historie, a tak úkol založený rovnou jako ukončený je ukončený od data založení. Platí to beze změny.
- **Zobrazení historie stavů v rozhraní**, například „Ukončeno k jednání č. 888". Nikde se dnes nezobrazuje a návrh ho nepřidává.
- **Další úpravy dashboardu** (Z5).

## 11. Co se nemění

Tisk projektu a záznamu (dnešní stav), pravidla viditelnosti řádků v tisku jednání (jen čtou `plati_od`), barvy podbarvení a jejich přednost, zvýraznění vyjádření, oprávnění k úpravě záznamu a jednání, `datum_zmeny` a všechny ostatní historie záznamu.
