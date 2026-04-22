# Integrace s návrhovou vrstvou + ruční kroky 2/5/8/9 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrovat harvest vyjádření + vazby na kroky s existující návrhovou vrstvou (`ZaznamNavrhEntity.payload_json`). Rozšířit `CreateRecordProposalPayload` a `SchedulePlanProposalPayload` o manuální datumy pro kroky 2/5/8/9 a o vazby vyjádření pro schéma 3 (nový záznam). Upravit záložku harmonogramu aby uživatel viděl skutečnost z přiřazených vyjádření a mohl ručně zadat datum pro kroky 2/5/8/9.

**Architecture:** Rozšíření existujícího `RecordProposalPayloadMapper` + `RecordProposalService.DecisionCommands` (při schválení aplikovat `ManualActualKroky[]` a `HarmonogramVazby[]` z payloadu). Úprava `_EditZaznamSchedulePanel.cshtml` — sloupec „Skutečnost" čte z nové tabulky `zaznam_harmonogram_vyjadreni_vazba`; pro ruční kroky input; pro automatické read-only ikona s odkazem na chat modal. Žádné DB změny — využíváme existující `payload_json` + `HS0X_DELAY.HodnotaInt` + nová tabulka vazeb z Plánu C.

**Tech Stack:** .NET 8 ASP.NET Core MVC, EF Core 8, Razor, existující návrhové workflow, Plán C tabulka `zaznam_harmonogram_vyjadreni_vazba`.

> **Authz (doplněno 2026-04-22):** Plán D pracuje se třemi typy uživatelů:
> 1. **Přímá editace harmonogramu** (schéma 1) — user s `records.edit` permission.
> 2. **Navrhování změn harmonogramu** (schéma 2 — `SCHEDULE_PLAN_CHANGE` submit) — user s `records.schedule.edit` permission ale bez `records.edit`.
> 3. **Navrhování nového záznamu** (schéma 3 — `CREATE_RECORD` submit) — subsystem lead role / `records.schedule.add` permission.
> 4. **Schvalování návrhů** (PM/ADM) — `proposals.approve` permission.
>
> **Finální podoba authz modelu se stále upravuje** (commity `98d97f4..e5eb2ed` a dál). Implementátor **MUSÍ** v momentu implementace:
> - Zkontrolovat aktuální stav `PmTracker.Web/Models/ViewModels/SecurityViewModels.cs` nebo kde jsou `PermissionKeys` definované.
> - Použít permission keys (nikdy `.HasRole("...")` nebo magic strings) — vzor z existujících controllerů po refaktoru.
> - Ověřit jména klíčů s autorem authz refaktoru, jména výše jsou orientační (mohou být `Records.Edit`, `RecordsEdit`, `records.edit` nebo jiný camelCase/PascalCase pattern podle current convention).
> - Pokud ACL attribute má jiný název než `[RequirePermission]` (např. `[AuthorizePermission]`, `[Permission]`), použít aktuální.
>
> **Žádné role-kódy v kódu**. Žádný `SUPERADMIN` fallback — `CurrentUserContext.IsSuperAdmin` čte z `AuthzSuperadmins` tabulky (commit `c7c3d88`).

**Předpoklady:**
- **Plán A, B, C, E hotovy** a nasazeny.
- **Authz refaktor dokončen** — viz notice výše.
- Spec 2026-04-21 §5 (tři workflow schémata) a §10.2 (rozšíření payloadů).

---

## File Structure

### Modifikované soubory — návrhová vrstva
- `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs` — přidat pole do `CreateRecordProposalPayload` a `SchedulePlanProposalPayload`.
- `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs` — mapping nových polí z/do commandů.
- `PmTracker.Web/Services/RecordProposalService.DecisionCommands.cs` — rozšířit `ApplyApprovedScheduleProposalAsync` a `ApproveCreateRecord...` o aplikaci nových polí při schválení.
- `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` — validace nových polí.

### Modifikované soubory — harmonogram UI
- `PmTracker.Web/Views/Projekty/_EditZaznamSchedulePanel.cshtml` — nový sloupec „Zdroj skutečnosti" + rozdělení input vs. read-only dle typu kroku.
- `PmTracker.Web/Services/Data/HarmonogramService.cs` — rozšířit výstup `BuildHarmonogramVypocetPublic` o informace o vazbě (zdrojové vyjádření per krok).
- `PmTracker.Web/Models/ViewModels/RecordScheduleViewModels.cs` — přidat `ZdrojSkutecnosti` property.

### Modifikované soubory — záznam UI
- `PmTracker.Web/Views/Projekty/_EditZaznamModal.cshtml` / `EditZaznamPage.cshtml` — propagace „hot vyjadreni" do JS (aby modal věděl, kam má poslat Save).

### Nové soubory — testy
- `PmTracker.Tests.Unit/Records/RecordProposalPayloadWithManualKrokyTests.cs`
- `PmTracker.Tests.Unit/Records/ApproveProposalAppliesManualKrokyTests.cs`
- `PmTracker.Tests.Unit/Records/ScheduleTabShowsActualFromBindingTests.cs`
- `PmTracker.Tests.Unit/Harmonogram/ManualKrokValidationTests.cs`

---

## Pořadí úkolů (12 tasků)

1. **Task 1** — Rozšířit `SchedulePlanProposalPayload` o `ManualActualKroky[]`.
2. **Task 2** — Rozšířit `CreateRecordProposalPayload` o `ManualActualKroky[]` + `HarmonogramVazby[]`.
3. **Task 3** — Rozšířit `RecordProposalPayloadMapper` o oba směry (VM ↔ payload).
4. **Task 4** — `RecordProposalService.SubmitCommands` — validace nových polí (chronologie, krok_key patří do schématu, datum není v budoucnosti).
5. **Task 5** — `ApplyApprovedScheduleProposalAsync` — aplikovat `ManualActualKroky[]` do `HS0X_DELAY.HodnotaInt` (převod datum → odchylka dnů).
6. **Task 6** — `ApproveCreateRecord...` — aplikovat `HarmonogramVazby[]` z payloadu do tabulky `zaznam_harmonogram_vyjadreni_vazba` + spustit harvest pro příchozí vazby.
7. **Task 7** — Rozšířit `HarmonogramService` výstup o zdroj skutečnosti (vazba vyjádření nebo ruční).
8. **Task 8** — `_EditZaznamSchedulePanel.cshtml` — sloupec „Skutečnost" rozlišuje automatický krok (read-only s ikonou chat) vs. ruční krok (input date).
9. **Task 9** — JS aktualizace — po editaci ručního datumu uložit přes existující SaveRecordCommand.
10. **Task 10** — Zámky harmonogramu (pending proposal) rozšířit o manuální kroky.
11. **Task 11** — Testy pro každý krok workflow (schéma 1, 2, 3 cover all).
12. **Task 12** — Full build + Playwright e2e + git clean.

---

## Task 1: Rozšířit `SchedulePlanProposalPayload`

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs`
- Test: `PmTracker.Tests.Unit/Records/RecordProposalPayloadWithManualKrokyTests.cs`

- [ ] **Step 1: Failující test**

`PmTracker.Tests.Unit/Records/RecordProposalPayloadWithManualKrokyTests.cs`:

```csharp
using FluentAssertions;
using System.Text.Json;
using PmTracker.Web.Models.ViewModels;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

public sealed class RecordProposalPayloadWithManualKrokyTests
{
    [Fact]
    public void SchedulePlanProposalPayload_ShouldSerializeManualActualKroky()
    {
        var payload = new SchedulePlanProposalPayload
        {
            ProjektId = 1,
            ZaznamId = 42,
            ManualActualKroky = new List<ManualActualKrokDto>
            {
                new() { KrokKey = Guid.NewGuid(), AbsolutniDatum = new DateOnly(2026, 3, 15) }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var roundtripped = JsonSerializer.Deserialize<SchedulePlanProposalPayload>(json);

        roundtripped!.ManualActualKroky.Should().HaveCount(1);
        roundtripped.ManualActualKroky[0].AbsolutniDatum.Should().Be(new DateOnly(2026, 3, 15));
    }

    [Fact]
    public void CreateRecordProposalPayload_ShouldSerializeHarmonogramVazby()
    {
        var payload = new CreateRecordProposalPayload
        {
            ProjektId = 1,
            HarmonogramVazby = new List<HarmonogramVazbaDto>
            {
                new() {
                    KrokKey = Guid.NewGuid(),
                    ExterniOdkazIndex = 0,
                    HotVyjadreniId = 99999,
                    DatumVyjadreni = new DateTimeOffset(2026, 3, 14, 9, 30, 0, TimeSpan.Zero)
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var rt = JsonSerializer.Deserialize<CreateRecordProposalPayload>(json);

        rt!.HarmonogramVazby.Should().HaveCount(1);
        rt.HarmonogramVazby[0].HotVyjadreniId.Should().Be(99999);
    }
}
```

- [ ] **Step 2: Spustit test (musí failnout)**

Run: `dotnet test PmTracker.Tests.Unit --filter "RecordProposalPayloadWithManualKrokyTests" --no-restore`

Expected: COMPILATION ERROR.

- [ ] **Step 3: Přidat DTO + property**

V `RecordProposalViewModels.cs`:

```csharp
public sealed class ManualActualKrokDto
{
    public Guid KrokKey { get; set; }

    /// <summary>
    /// Kalendářní datum skutečnosti zadaný uživatelem.
    /// DateOnly = bez časové zóny + bez času = žádný DST / Kind / ±1 den shift.
    /// Posílá se z JS jako "yyyy-MM-dd" ISO string; default ASP.NET Core binder
    /// DateOnly přijímá právě tento formát.
    /// </summary>
    public DateOnly AbsolutniDatum { get; set; }
}

public sealed class HarmonogramVazbaDto
{
    public Guid KrokKey { get; set; }
    public int ExterniOdkazIndex { get; set; }   // index do ExterniVazby[] v payloadu
    public long HotVyjadreniId { get; set; }

    /// <summary>
    /// Timestamp vyjádření z HOT DB (reálný okamžik, ne kalendářní datum).
    /// DateTimeOffset = absolutní okamžik včetně offsetu; server konverze
    /// do UTC pro ukládání přes TimeProvider.System.GetUtcNow().
    /// </summary>
    public DateTimeOffset DatumVyjadreni { get; set; }
}
```

A do `SchedulePlanProposalPayload` přidat:

```csharp
public List<ManualActualKrokDto> ManualActualKroky { get; set; } = new();
```

A do `CreateRecordProposalPayload` přidat:

```csharp
public List<ManualActualKrokDto> ManualActualKroky { get; set; } = new();
public List<HarmonogramVazbaDto> HarmonogramVazby { get; set; } = new();
```

- [ ] **Step 4: Build + test**

Run: `dotnet build --no-restore && dotnet test PmTracker.Tests.Unit --filter "RecordProposalPayloadWithManualKrokyTests" --no-restore`

Expected: 0 errors, 2/2 passed.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/RecordProposalViewModels.cs \
        PmTracker.Tests.Unit/Records/RecordProposalPayloadWithManualKrokyTests.cs
git commit -m "feat(navrhy): rozšířit SchedulePlan a CreateRecord payloady o ManualActualKroky + HarmonogramVazby"
```

---

## Task 2 — `CreateRecordProposalPayload` HarmonogramVazby

Přidání proběhlo již v Task 1 (oba payloady v jednom souboru). Tento task je **merge do Task 1**, přeskoč.

## Task 3 — `RecordProposalPayloadMapper` round-trip

Najít `BuildSaveCommand()` a `BuildSchedulePayload()` metody, přidat mapping:
- VM `SaveRecordCommand.ManualActualKroky` ↔ payload `ManualActualKroky[]`
- VM `SaveRecordCommand.HarmonogramVazby` ↔ payload `HarmonogramVazby[]`

Test: round-trip `VM → payload → VM` zachová všechny hodnoty.

Commit: `refactor(navrhy): round-trip mapping v RecordProposalPayloadMapper pro manuální kroky + vazby`

## Task 4 — Validace v `SubmitCommands`

Přidat do `ValidateCommonProposalInput()` / `ValidateScheduleProposalInput()`:
- Každý `ManualActualKrokDto.KrokKey` musí existovat ve schématu.
- `AbsolutniDatum` nesmí být v budoucnosti.
- Ruční kroky musí být v sadě `{2, 5, 8, 9}` dle poradi kroku (mapping KrokKey → Poradi z schématu).
- `HarmonogramVazby[].ExterniOdkazIndex` musí být valid index do `ExterniVazby[]` v payloadu.

Test: každé nevalidní pole → hlášená chyba ve validation result.

Commit: `feat(navrhy): validace ManualActualKroky a HarmonogramVazby v SubmitCommands`

## Task 5 — `ApplyApprovedScheduleProposalAsync` aplikuje ManualActualKroky

**Typový model (nutno zajistit):**

- Payload `ManualActualKrokDto` má property `AbsolutniDatum` typu **`DateOnly`** (nikoli `DateTime`). `DateOnly` nemá čas ani timezone komponentu, takže se neriskuje ±1 den shift mezi klientem (UTC serializace) a serverem (Local default).
- UI JS posílá datum ve formátu `"yyyy-MM-dd"` (ISO date bez času). Model binder ASP.NET Core defaultně `DateOnly` přijímá pouze tento formát.
- `BuildHarmonogramVypocetCore` vrací `planEndDate` taky jako `DateOnly` (pokud tak ještě není, upravit v Task 7 + fix volajících).

Rozšíření existující metody v `RecordProposalService.DecisionCommands.cs` (řádek ~137):
- Pro každý `ManualActualKrokDto`:
  1. Najít `HarmonogramTyp` s `KrokKey == krokKey && JeZpozdeni == true` (= DELAY řádek)
  2. Spočítat `planEndDate(krok)` z `BuildHarmonogramVypocetCore(datumZalozeni, typy, hodnoty)` — vrací `DateOnly`
  3. `odchylka = absolutniDatum.DayNumber - planEndDate.DayNumber` (diff v kalendářních dnech; `DayNumber` = počet dní od roku 1)
  4. UPSERT `ZaznamHarmonogramHodnotaEntity { TypId = delayTypId, HodnotaInt = odchylka }`

> **Proč `DateOnly`:** starší varianta používala `DateTime - DateTime` → podléhá DST / timezone / Kind nejasnostem (`DateTimeKind.Unspecified` default z binderu → C# tiše považuje za Local). Pro PMO data (plán datum = kalendářní den, ne okamžik) je `DateOnly` jediný správný typ. Jednotný server-side výpočet zamezí, aby ±1 h DST shift generoval ±1 den chybu u odchylky. Pattern „jak to řeší velká firma": všechna kalendářní data skrz `DateOnly`, všechny timestampy skrz `DateTimeOffset.UtcNow` (ze serverového `TimeProvider`), nikdy mix.

Test: předložit payload s manuálním datumem → schválit → ověřit `HS0X_DELAY.HodnotaInt` odpovídá odchylce.

Commit: `feat(navrhy): aplikovat ManualActualKroky při schválení návrhu`

## Task 6 — `ApproveCreateRecord...` aplikuje HarmonogramVazby

Po vytvoření záznamu + externích vazeb:
- Pro každý `HarmonogramVazbaDto`:
  1. Resolvnout `ExterniOdkazId` z `ExterniOdkazIndex` (index do nově vytvořených `ZaznamExterniOdkazEntity` kolekce).
  2. INSERT do `zaznam_harmonogram_vyjadreni_vazba` s `Source = Manual` (návrh = manuální).
  3. UPSERT `HS0X_DELAY.HodnotaInt` (odchylka dnů).
- Na konci spustit `IHarvestScheduler.ScheduleHarvestForRecordAsync(newRecordId)` — ověřit případná nová vyjádření, která mezitím přibyla.

Test: návrh s 2 vazbami → schválit → ověřit 2 řádky v `VyjadreniVazby` + 2 UPSERT do `ZaznamHarmonogramHodnoty`.

Commit: `feat(navrhy): aplikovat HarmonogramVazby při schválení CREATE_RECORD návrhu`

## Task 7 — `HarmonogramService.BuildHarmonogramVypocet...` rozšíření

Výstup `HarmonogramVypocetKroku` obohatit o:
- `ZdrojSkutecnosti` (`enum: None / FromVyjadreni / Manual`)
- `SourceVyjadreniId` (`long?`) — pokud FromVyjadreni
- `SourceVyjadreniDatum` (`DateTime?`)

Služba dohledá pro každý krok Active vazbu v `zaznam_harmonogram_vyjadreni_vazba` → pokud existuje, `ZdrojSkutecnosti = FromVyjadreni`; pokud v `HS0X_DELAY.HodnotaInt` je hodnota ale bez vazby, `ZdrojSkutecnosti = Manual`.

Test: 3 scénáře (none / from-vyjadreni / manual).

Commit: `feat(harmonogram): BuildHarmonogramVypocet rozšířit o ZdrojSkutecnosti per krok`

## Task 8 — `_EditZaznamSchedulePanel.cshtml` nová struktura

Sloupec „Skutečnost" rozlišuje:
- **Auto krok s vazbou** → read-only čísla odchylky + ikona 🔗 s tooltip „Z vyjádření {datum, autor}", klik otevře chat modal.
- **Auto krok bez vazby** → prázdný + ikona ⚠ „Automat zatím nenašel vhodné vyjádření".
- **Ruční krok** (2/5/8/9) → editovatelný date input (jen pro `records.edit`; jinak read-only).

Test: Razor snapshot testy pro každý scénář.

Commit: `feat(harmonogram-ui): sloupec Skutečnost rozlišuje auto/ruční kroky + ikona chat pro vazbu`

## Task 9 — JS aktualizace pro ruční editaci

Při změně data v inputu ručního kroku → POST přes existující `SaveRecordCommand` endpoint. Validace na klientské straně: datum nesmí být v budoucnosti + chronologie.

Commit: `feat(harmonogram-ui): JS ruční editace datumu pro kroky 2/5/8/9`

## Task 10 — Rozšíření pending lock evaluator

`PendingScheduleProposalLockEvaluator` musí brát v úvahu i ManualActualKroky — pokud existuje pending SCHEDULE_PLAN_CHANGE návrh, nelze manuálně editovat ty kroky v přímé úpravě (schéma 1).

Commit: `feat(navrhy): rozšířit pending lock o ManualActualKroky`

## Task 11 — Integrační testy all-three-schemas

`PmTracker.Tests.Unit/Records/ThreeSchemaIntegrationTests.cs` pokrývá:

1. **Schéma 1 (přímá):** records.edit user uloží `SaveRecordCommand` s ruční datum pro krok 2 → okamžitý zápis do `HS02_DELAY`, žádný návrh.
2. **Schéma 2 (úprava harmonogramu):** records.schedule.edit user bez records.edit submitnout `SCHEDULE_PLAN_CHANGE` s ManualActualKroky → návrh pending → PM/ADM schválí → ManualActualKroky aplikovány do DELAY hodnot, externí vazby NEDOTČENY.
3. **Schéma 3 (nový záznam):** subsystem lead submitne `CREATE_RECORD` s 2 externími vazbami + 3 vazbami vyjádření → návrh pending → PM/ADM schválí → nový záznam + 2 externí vazby + 3 řádky v VyjadreniVazby + 3 UPSERT DELAY + spustí se harvest pro každou vazbu.

Commit: `test(navrhy): integrační testy pro všechny 3 workflow schémata`

---

## Task 12: Full build + e2e + git clean

- [ ] **Step 1: `dotnet build` 0 errors**
- [ ] **Step 2: `dotnet test PmTracker.Tests.Unit` — všechny passed**
- [ ] **Step 3: Playwright e2e:**
  - Schéma 1: edit záznamu → tab harmonogram → manuálně zadat datum kroku 2 → save → reload → datum přetrvá
  - Schéma 2: login jako vedoucí subsystému (není records.edit) → navrhnout změnu harmonogramu s ručním datumem → odhlásit → login jako PM → schválit → ověřit aplikaci
  - Schéma 3: login jako vedoucí subsystému → navrhnout nový záznam s externí vazbou → otevřít chat modal v editoru návrhu → přiřadit bublinu ke kroku → uložit návrh → PM schválí → ověřit nový záznam s vazbou
- [ ] **Step 4: Git status clean**

---

## Hotovo — Plán D

Po dokončení máš plně uzavřený use-case A:
- ✅ Všechny 3 workflow schémata (přímá, úprava harmonogramu přes návrh, nový záznam přes návrh) plně integrované.
- ✅ ManualActualKroky pro ruční kroky 2/5/8/9 v payloadu.
- ✅ HarmonogramVazby v CREATE_RECORD payloadu pro předání vyjádření → krok vazeb.
- ✅ Záložka harmonogram rozlišuje auto (read-only s ikonou chat) vs. ruční (input).
- ✅ Pending lock evaluator respektuje nová pole.
- ✅ Integrační testy napříč schématy.

### Konec use-case A
Po Plánu D je celý scope „vytěžování vyjádření" dokončen. Další navazující fáze:
- Use-case B (Dashboard prodlení NES/PMP/PNF) — využívá `zaznam_harmonogram_vyjadreni_vazba` pro plán vs. skutečnost.
- Use-case C (Fáze 3 — Word export) — mimo scope.
