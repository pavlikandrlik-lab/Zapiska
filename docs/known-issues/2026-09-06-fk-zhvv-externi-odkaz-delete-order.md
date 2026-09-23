# Mazání externí vazby padá na FK_zhvv_externi_odkaz — diagnóza

**Datum:** 2026-09-06
**Stav:** příčina nalezena a reprodukována, oprava ověřena spikem, čeká na implementaci
**Podnět:** uživatel nemohl odebrat PNF ze záznamu — `UNEXPECTED_SERVER_ERROR`, SQL 547

## 1. Příznak

`POST /Zaznamy/Save` skončí chybou:

```
The DELETE statement conflicted with the REFERENCE constraint "FK_zhvv_externi_odkaz".
The conflict occurred in database "PM_Tracker", table "dbo.zaznam_harmonogram_vyjadreni_vazba",
column 'externi_odkaz_id'.
Error Number:547

Entry[0] ZaznamExterniOdkazEntity State=Deleted PK=Id=164
```

Pád je v `RecordService.SaveRecord.cs:254` — ve `SaveChangesAsync` uvnitř serializovatelné transakce.

## 2. Příčina

`ReplaceRecordExternalLinksAsync` (`RecordService.SaveRecord.cs:1117`) na problém **myslí**: než smaže externí vazbu, načte a odstraní navázané harmonogramové bindingy (`:1141-1147`), teprve pak maže samotnou vazbu (`:1149`). Komentář u metody tvrdí *„EF Core SaveChanges respektuje FK ordering"*.

Ta věta platí jen pro vztahy, které jsou **v EF modelu**. Tenhle v něm není.

`ZaznamHarmonogramVyjadreniVazbaEntityConfiguration` deklaruje jen sloupce a dva indexy. `ExterniOdkazId` je obyčejný `int` — žádné `HasOne`/`HasForeignKey`. EF proto nemá mezi oběma mazáními hranu závislosti a pořadí příkazů si volí libovolně: pošle nejdřív `DELETE` z `zaznam_externi_odkazy`, až potom z tabulky bingingů. Cizí klíč je v databázi `ON DELETE NO ACTION`, takže SQL Server první příkaz odmítne.

Pořadí `RemoveRange` volání ve zdrojovém kódu na pořadí SQL příkazů nemá vliv.

### 2.1 Proč to testy nechytily

`RecordServiceExternalLinkUpsertTests.ReplaceRecordExternalLinksAsync_MustCleanupVyjadreniVazbyBeforeExternalLinkDelete` **čte zdrojový kód jako text** a kontroluje, že `VyjadreniVazby.RemoveRange(bindingsToCleanup)` je v souboru napsané nad `ZaznamExterniOdkazy.RemoveRange(toDelete)`. O tom, co EF pošle do databáze, netvrdí nic.

V integračních testech se tabulka `zaznam_harmonogram_vyjadreni_vazba` nevyskytovala vůbec — tahle cesta proti reálnému cizímu klíči nikdy neběžela.

### 2.2 Co s tím nesouvisí

- **Dva úkoly se stejným PNF.** Reprodukce má jediný záznam. Stačí smazat libovolnou externí vazbu, na kterou harvest navěsil binding.
- **Necommitnutá změna v `SaveRecord.cs`** (přesun chronologie do `ScheduleChronologyValidator`) — jiná část souboru, s mazáním vazeb nesouvisí.
- **Data uživatele.** Transakce je serializovatelná a odrolovala se; nic se neuložilo ani nepoškodilo.
- **Druhý záznam.** Každý záznam má vlastní řádek `zaznam_externi_odkazy` a harvest věší bindingy vždy na vazbu téhož záznamu (`VyjadreniHarvestService.cs:407` bere záznam z `eo.ZaznamId`). Mazání u jednoho záznamu se druhého nedotkne.

## 3. Reprodukce

`PmTracker.Tests.Integration/DataStore/ExternalLinkDeleteWithBindingTests.cs` — záznam s externí vazbou PNF, na ni jeden binding, pak uložení záznamu s prázdným seznamem vazeb. Proti reálnému SQL Serveru padá se stejnou chybou 547 jako provoz.

Vazba se v testu zakládá přímo do databáze: integrační fixture má ServiceDesk vypnutý (`DisabledTicketingQueryService`), takže by validace nový tiket odmítla jako nenalezený.

## 4. Oprava

Doplnit vztah do EF modelu s `DeleteBehavior.NoAction`, aby seděl s databází:

```csharp
b.HasOne<ZaznamExterniOdkazEntity>()
    .WithMany()
    .HasForeignKey(x => x.ExterniOdkazId)
    .OnDelete(DeleteBehavior.NoAction);
```

EF tím získá hranu závislosti a seřadí mazání správně — bindingy první, vazba druhá.

**Ověřeno spikem 2026-09-06:** reprodukce prošla, Unit 1644/1644, Integration 85/86 (jediné selhání je dřívější známé `ProposalRejectAndTakeOverE2ETests` s oprávněním `proposals.accept`).

### 4.1 Proč ne jiné varianty

| Varianta | Proč ne |
|---|---|
| `ON DELETE CASCADE` v databázi | Zkoušeno 2026-04-28, revertováno. SQL 1785 — `vyjadreni_vazby` má dvě cesty na `projektove_zaznamy` (přímo přes `zaznam_id` i přes `zaznam_externi_odkazy.zaznam_id`), SQL Server víc kaskádových cest nepovolí. |
| Mezikrok `SaveChangesAsync` mezi mazáním bingingů a vazby | Zalepí jedno místo. Příčina — chybějící vztah v modelu — zůstane a příště spadne jiná cesta. |
| `DeleteBehavior.Cascade` / `ClientCascade` | Změnilo by chování: EF by potomky mazal sám. Model má odpovídat databázi, kde je `NO ACTION`. |

### 4.2 Rozsah

Oprava je bodová, sourozence nemá:

- `FK_zhvv_externi_odkaz` je **jediný** cizí klíč mířící na `zaznam_externi_odkazy`.
- Na `zaznam_harmonogram_krok` nemíří žádný cizí klíč.
- `zaznam_harmonogram_krok.preferred_externi_odkaz_id` sice na externí vazbu odkazuje, ale **cizí klíč v databázi nemá** — není co porušit.
- `ReplaceRecordExternalLinksAsync` je jediné místo v celé aplikaci, kde se v jednom `SaveChanges` maže rodič i jeho potomek. Všechna ostatní `Remove`/`RemoveRange` volání ve službách sahají na jedinou tabulku.
- `DeleteRecordAsync` bindingy nemaže ručně — spoléhá na `ON DELETE CASCADE` v databázi a tato změna se ho nedotkne.

## 5. Co zůstane nedořešené

Komentář u `ReplaceRecordExternalLinksAsync` a doprovodný textový test dnes tvrdí, že pořadí zajišťuje samo EF. To je nepravda, která pozvala tuhle chybu — po opravě musí obojí říkat, že záruku dává deklarovaný vztah v modelu.
