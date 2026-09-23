# Oprava pořadí mazání externí vazby (FK_zhvv_externi_odkaz) — implementační plán

> **Pro agentní workery:** REQUIRED SUB-SKILL: použij superpowers:executing-plans (uživatel jede inline v hlavní session). Kroky mají checkbox (`- [ ]`) syntaxi.

**Cíl:** Odebrání externí vazby ze záznamu projde i tehdy, když na ni harvest navěsil harmonogramové bingingy — místo dnešního pádu na SQL 547.

**Architektura:** Do EF modelu se doplní chybějící vztah `zaznam_harmonogram_vyjadreni_vazba.externi_odkaz_id → zaznam_externi_odkazy.id` s `DeleteBehavior.NoAction`, aby model odpovídal databázi. EF tím získá hranu závislosti a seřadí `DELETE` příkazy správně: nejdřív bingingy, pak vazba. Aplikační kód v `ReplaceRecordExternalLinksAsync` zůstává beze změny — jen jeho zavádějící komentáře se srovnají s realitou.

**Tech stack:** net8.0, EF Core 8 (SQL Server v provozu), xUnit + FluentAssertions, Testcontainers.MsSql.

**Spec:** `docs/known-issues/2026-09-06-fk-zhvv-externi-odkaz-delete-order.md`

## Globální omezení

- **Commity se DRŽÍ.** Uživatel commituje sám po ruční verifikaci. Každý task končí checkpointem, ne `git commit`. Navržená message je u tasku uvedená pro pozdější použití.
- `DeleteBehavior` musí být **`NoAction`** — databáze má `ON DELETE NO ACTION` a model jí má odpovídat. `Cascade` ani `ClientCascade` nepoužívat: měnily by chování a `CASCADE` na úrovni databáze je vyloučený kvůli SQL 1785 (multi-cascade-path).
- **Žádné navigační vlastnosti** do entit nepřidávat. Repozitář deklaruje vztahy stylem `HasOne<T>().WithMany()` bez navigací — vzor je `ScheduleKrokEntityConfiguration.cs:27-30`.
- Vztah patří **za** deklarace indexů, stejně jako v `ScheduleKrokEntityConfiguration.cs`.
- Reprodukční test `ExternalLinkDeleteWithBindingTests` už v pracovním stromě **existuje** (necommitnutý) a je červený. Task 1 ho nepíše znovu.
- Známá dřívější selhání, která se neopravují a jen hlásí: 4 gantt testy v Api (`schedule-layered-marker today`) a `ProposalRejectAndTakeOverE2ETests` v Integration (oprávnění `proposals.accept`).

## Struktura souborů

| Akce | Soubor | Odpovědnost |
|---|---|---|
| Změnit | `PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs` | deklarace chybějícího vztahu |
| Změnit | `PmTracker.Web/Services/RecordService.SaveRecord.cs:1091-1116` | srovnat komentář metody s realitou |
| Změnit | `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs:97-121` | srovnat komentář testu + doplnit, kde je skutečná záruka |
| Existuje | `PmTracker.Tests.Integration/DataStore/ExternalLinkDeleteWithBindingTests.cs` | reprodukce (necommitnutá, červená) |

---

### Task 1: Vztah do EF modelu

**Files:**
- Modify: `PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs`
- Test: `PmTracker.Tests.Integration/DataStore/ExternalLinkDeleteWithBindingTests.cs` (existuje, needitovat)

**Interfaces:**
- Consumes: entitu `ZaznamExterniOdkazEntity` z `PmTracker.Web.Models.Entities` (už je v `using` seznamu souboru) a `DeleteBehavior` z `Microsoft.EntityFrameworkCore` (rovněž už importováno).
- Produces: nic pro další tasky — Task 2 mění jen komentáře.

- [ ] **Krok 1: Ověř, že reprodukce je červená**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ExternalLinkDeleteWithBindingTests"`

Expected: FAIL s `Error Number:547` a textem `The DELETE statement conflicted with the REFERENCE constraint "FK_zhvv_externi_odkaz"`.

Integrační testy potřebují běžící Docker (Colima) kvůli Testcontainers.MsSql. Když kontejner neběží, test spadne už na startu fixture — to **není** hledaný červený běh; nejdřív rozjeď Docker.

- [ ] **Krok 2: Deklaruj vztah**

V `PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs` doplň za blok indexů (tedy za `b.HasIndex(x => x.ExterniOdkazId).HasDatabaseName("ix_zhvv_externi_odkaz");`) tento kód:

```csharp

        // FK_zhvv_externi_odkaz je v databázi ON DELETE NO ACTION (db_upgrade_1_3_6).
        // Vztah musí být v modelu, i když se přes něj nenaviguje: bez něj EF nezná
        // závislost mezi bindingem a externí vazbou a při jejich současném mazání
        // pošle DELETE v libovolném pořadí → SQL 547 (hlášení 2026-09-06).
        b.HasOne<ZaznamExterniOdkazEntity>()
            .WithMany()
            .HasForeignKey(x => x.ExterniOdkazId)
            .OnDelete(DeleteBehavior.NoAction);
```

Výsledné tělo metody `Configure` končí takto:

```csharp
        b.HasIndex(x => new { x.ZaznamId, x.Poradi, x.Stav })
            .HasDatabaseName("ix_zhvv_zaznam_poradi_stav");
        b.HasIndex(x => x.ExterniOdkazId)
            .HasDatabaseName("ix_zhvv_externi_odkaz");

        // FK_zhvv_externi_odkaz je v databázi ON DELETE NO ACTION (db_upgrade_1_3_6).
        // Vztah musí být v modelu, i když se přes něj nenaviguje: bez něj EF nezná
        // závislost mezi bindingem a externí vazbou a při jejich současném mazání
        // pošle DELETE v libovolném pořadí → SQL 547 (hlášení 2026-09-06).
        b.HasOne<ZaznamExterniOdkazEntity>()
            .WithMany()
            .HasForeignKey(x => x.ExterniOdkazId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
```

- [ ] **Krok 3: Ověř, že reprodukce je zelená**

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ExternalLinkDeleteWithBindingTests"`

Expected: PASS — 1 test zelený. Test kromě absence výjimky ověřuje i to, že po uložení nezůstala externí vazba ani navázané bingingy.

- [ ] **Krok 4: Ověř, že se nerozbilo mazání záznamu**

`DeleteRecordAsync` bingingy nemaže ručně a spoléhá na `ON DELETE CASCADE` v databázi. Nový vztah je `NoAction`, takže do toho nemá zasahovat — je to ale předpoklad, který se má ověřit, ne uhodnout.

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordDeleteCascadeFkTests"`

Expected: PASS.

- [ ] **Krok 5: Checkpoint (commit se DRŽÍ)**

Run: `dotnet build PmTracker.sln`
Expected: Build succeeded, 0 upozornění, 0 chyb.

Navržená message pro pozdější commit uživatelem:

```
fix(zaznamy): odebrání externí vazby s harvestovaným bindingem už nepadá na FK_zhvv_externi_odkaz
```

---

### Task 2: Srovnat zavádějící komentáře

**Files:**
- Modify: `PmTracker.Web/Services/RecordService.SaveRecord.cs:1091-1116`
- Modify: `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs:97-121`

**Interfaces:**
- Consumes: hotový Task 1 (vztah v modelu existuje).
- Produces: nic dalšího.

**Kontext:** Komentář u metody i doprovodný unit test dnes tvrdí *„EF Core SaveChanges respektuje FK ordering"*. Ta věta pozvala tuhle chybu — platí jen pro vztahy deklarované v modelu. Kdo ji nechá stát, pozve ji znovu. Chování se v tomto tasku nemění, mění se jen text.

- [ ] **Krok 1: Oprav komentář u metody**

V `PmTracker.Web/Services/RecordService.SaveRecord.cs` nahraď v XML dokumentaci metody `ReplaceRecordExternalLinksAsync` odstavec

```
    ///   cascade graph. Migration 1_3_13 byla odstraněna, FK_zhvv_externi_odkaz
    ///   zůstává <c>NO ACTION</c>. Aplikace explicitně cleanup-uje
    ///   <c>vyjadreni_vazby</c> rows PŘED smazáním externí vazby — pre-flight
    ///   harvest_locked check ODSTRANĚN, delete je nyní běžná operace.
```

textem

```
    ///   cascade graph. Migration 1_3_13 byla odstraněna, FK_zhvv_externi_odkaz
    ///   zůstává <c>NO ACTION</c>. Aplikace explicitně cleanup-uje
    ///   <c>vyjadreni_vazby</c> rows PŘED smazáním externí vazby — pre-flight
    ///   harvest_locked check ODSTRANĚN, delete je nyní běžná operace.
    /// - 2026-09-06: samotné pořadí <c>RemoveRange</c> volání v kódu na pořadí SQL
    ///   příkazů NEMÁ vliv. Do 2026-09-06 tu chyběl vztah v EF modelu, takže EF
    ///   neznalo závislost a mazalo v libovolném pořadí → SQL 547. Záruku dnes dává
    ///   deklarace vztahu v <c>ZaznamHarmonogramVyjadreniVazbaEntityConfiguration</c>.
```

a v bodě 3 logiky nahraď větu

```
    ///    navázaných vyjadreni_vazby rows, pak RemoveRange externí vazby
    ///    (EF Core SaveChanges respektuje FK ordering).
```

větou

```
    ///    navázaných vyjadreni_vazby rows, pak RemoveRange externí vazby. Pořadí
    ///    SQL příkazů plyne z deklarovaného vztahu v EF modelu, ne z pořadí těchto
    ///    dvou řádků.
```

- [ ] **Krok 2: Oprav komentář v testu**

V `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs` v metodě `ReplaceRecordExternalLinksAsync_MustCleanupVyjadreniVazbyBeforeExternalLinkDelete` nahraď řádek

```csharp
        // EF Core SaveChanges respektuje FK ordering.
```

řádky

```csharp
        // POZOR: tenhle test čte zdrojový kód jako text — o pořadí SQL příkazů neříká nic.
        // To zajišťuje deklarovaný vztah v ZaznamHarmonogramVyjadreniVazbaEntityConfiguration
        // a hlídá ho ExternalLinkDeleteWithBindingTests proti reálnému SQL Serveru.
```

- [ ] **Krok 3: Ověř, že testy pořád procházejí**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~RecordServiceExternalLinkUpsertTests"`

Expected: PASS — 4 testy zelené. Test hledá ve zdrojáku řetězce `VyjadreniVazby`, `bindingsToCleanup` a `VyjadreniVazby.RemoveRange(bindingsToCleanup)`; změna se týká jen komentářů, takže žádný z nich nezmizel.

- [ ] **Krok 4: Checkpoint (commit se DRŽÍ)**

Navržená message pro pozdější commit uživatelem:

```
docs(zaznamy): komentáře u mazání externích vazeb říkají, odkud pořadí SQL příkazů opravdu plyne
```

---

### Task 3: Kontrolní průchod

**Files:** žádné změny — když něco selže, oprava patří do tasku, který to způsobil.

**Interfaces:**
- Consumes: hotové Tasky 1 a 2.
- Produces: podklad pro ruční ověření uživatelem.

- [ ] **Krok 1: Build celého řešení**

Run: `dotnet build PmTracker.sln`
Expected: Build succeeded, 0 upozornění, 0 chyb.

- [ ] **Krok 2: Celá Unit sada**

Run: `dotnet test PmTracker.Tests.Unit`
Expected: PASS — 1644/1644.

- [ ] **Krok 3: Celá Api sada**

Run: `dotnet test PmTracker.Tests.Api`
Expected: PASS až na 4 známá dřívější selhání gantt testů (`RecordEditorControllerTests.Edit_ShouldRenderScheduleMiniGantt_…` a tři `ProjectHarmonogramRenderTests.…`). Nic dalšího selhat nesmí.

- [ ] **Krok 4: Celá Integration sada**

Run: `dotnet test PmTracker.Tests.Integration`
Expected: PASS až na známé dřívější selhání `ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`. Reprodukce `ExternalLinkDeleteWithBindingTests` musí být zelená.

- [ ] **Krok 5: Ověř, že reprodukce je poctivá**

Test, který prošel, ještě nedokazuje, že by chybu chytil. Dočasně zakomentuj v `ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs` blok `b.HasOne<ZaznamExterniOdkazEntity>()…` a spusť:

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ExternalLinkDeleteWithBindingTests"`
Expected: FAIL s `Error Number:547`.

Pak blok vrať a znovu ověř zelenou:

Run: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ExternalLinkDeleteWithBindingTests"`
Expected: PASS.

Nakonec zkontroluj, že po dočasné úpravě nezbyla stopa:

Run: `grep -c "HasOne<ZaznamExterniOdkazEntity>" PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs`
Expected: `1`

- [ ] **Krok 6: Kontrola diffu**

Run: `git status --short PmTracker.Web/Data/Configuration/ZaznamHarmonogramVyjadreniVazbaEntityConfiguration.cs PmTracker.Web/Services/RecordService.SaveRecord.cs PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs PmTracker.Tests.Integration/DataStore/ExternalLinkDeleteWithBindingTests.cs docs/known-issues/2026-09-06-fk-zhvv-externi-odkaz-delete-order.md`

Expected: změněné jsou právě tyhle soubory; žádný `appsettings*.json`, nic v `bin/` ani `obj/`.

- [ ] **Krok 7: Předání uživateli**

Shrň, co je hotové, které testy běžely a s jakým výsledkem, a která dřívější selhání zůstala. Commity zůstávají **nezacommitované** — uživatel je udělá po ruční verifikaci.

Uživateli řekni i to, že po nasazení jde jeho původní situace dořešit normálně přes formulář: PNF u chybného úkolu odebrat a uložit; u správného úkolu zůstane nedotčené.

---

## Co se záměrně nemění

- **Žádná migrace databáze.** Cizí klíč i jeho `NO ACTION` zůstávají, jak jsou. Mění se jen EF model, aby databázi odpovídal.
- **Aplikační logika `ReplaceRecordExternalLinksAsync`.** Úklid bingingů před mazáním vazby tam už je a je správně; chyběla mu jen záruka pořadí.
- **Chování při mazání záznamu.** `DeleteRecordAsync` dál spoléhá na `ON DELETE CASCADE` v databázi.
