# Záznam: hlavička, detaily v tabulce, procházení šipkami, mazání data plánu — plán implementace

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: superpowers:executing-plans (inline, po úkolech, bez subagentů — uživatel 2026-10-12). Kroky jsou checkboxy (`- [ ]`). Commity do větve `feat/zaznam-hlavicka-sipky-plan`, na konci merge do lokálního `main`, bez pushe.

**Cíl:** Čtyři nezávislé změny záznamu podle spec:
- **D:** datum plánu v harmonogramu jde smazat (✕) v editoru i v návrhu změny. Návrh nese odstranění a schválení plán smaže.
- **A:** hlavička karty je zarovnaná vlevo s tělem karty.
- **B:** detaily jsou v tabulce se dvěma dvojicemi na řádek.
- **C:** detail jednání ukáže jen záznamy jednání. Z menu karty „Otevřít na celou obrazovku“ vede na stránku záznamu, kde se mezi záznamy přechází tlačítky a klávesami ← →.

**Architektura:**
- **D:** `_ScheduleTable.cshtml` zapne `Clearable` podle oprávnění kroku. Text trvání sdílí server (`ScheduleDurationText`) a JS (`planDuration.js`). `duration-calendar-binding.js` se přepíše na datum-model, dnes tiše nedělá nic. `RecordProposalPayloadMapper` dostane množinu kroků, které plán v záznamu mají.
- **A, B:** jen CSS a sdílený partial `_ZaznamDetailPartial.cshtml` (karta i stránka záznamu).
- **C:**
  - Nová služba `IRecordSequenceService` je jediný zdroj „co patří do jednání“. Používá ji tisk zápisu, detail jednání i šipky.
  - Pořadí zápisu se vyčlení do `MinutesRecordOrdering` a sdílí ho tisk i služba.
  - Stránka záznamu dostane seznam z parametru `seznam` (`jednani-{id}` / `aktivni`).
  - Klávesy obsluhuje nový ES modul `recordSequenceKeys.js`.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, EF Core (SQL Server), CSS s gov DS tokeny, ES moduly, xUnit + FluentAssertions (Unit, Api `ApiSqlFixture`, Integration `SqlIntegrationFixture`), `node --test` (JS bez DOM), E2E Playwright .NET, ruční průchod Playwright Node proti lokální aplikaci.

**Spec:** `docs/superpowers/specs/2026-10-22-zaznam-hlavicka-detaily-sipky-plan-design.md` (R1–R14, schválená 2026-10-22).

## Global Constraints

- Pravidla, kdo co vidí a smí, se **nemění** (spec §5). Žádný nový klíč oprávnění.
- **Tisk zápisu: obsah i pořadí beze změny.** Charakterizační test z Task 6 musí zůstat zelený i po Task 7.
- **CSS:**
  - jen gov tokeny a `--app-*` (hlídá `AppCssColorTokenTests`);
  - žádné `:has`, `max-content` ani `[data-theme]`;
  - nosný layout jen `flex`, `table` a `position` (Edge na i15);
  - žádná duplicitní pravidla — před přidáním vyhledat selektor (grep).
- **Razor:**
  - žádné `@* *@` uvnitř tagu s TagHelpery;
  - `data-*` s hodnotou null se vypíše jako `""`, JS bere prázdný řetězec jako „nic“;
  - Api testy kotví na atributy, český text porovnávají až po `WebUtility.HtmlDecode`.
- **gov-button / pm-button:** žádné `textContent`; `aria-*` na hostu se neuplatní. Odkaz přes `pm-button href`.
- **JS:**
  - nový modul s vedlejšími efekty se importuje v `bootstrap.js`;
  - čisté funkce se testují `npm test` bez DOM, takže modul nesmí sahat na `document` při importu.
- **E2E:**
  - jednání ve fixture projektu má číslo < 9820 a datum jiné než dnešek;
  - kde záleží na přesné sadě karet, test použije vlastní projekt (`E2EOwnProject`);
  - identitu `asUser` doplní kontext prohlížeče (`RouteAsync`), protože odkazy z menu karty ani tlačítka šipek ji nenesou.
- Komentáře v kódu česky s odkazem „Spec 2026-10-22 Rx“.
- Po dokončení přibude sekce 6 v `docs/k-otestovani.md` (stále ten jeden soubor).

## Review Focus

1. **Karta v pohledu Harmonogram (rozbalená, přepínač zapnutý).** Tabulka detailů je tam skrytá, takže řádek „Termín · Založeno“ v hlavičce musí zůstat vidět. Pokrývá Task 5 (CSS test + E2E).
2. **Šipky, když je fokus v poli, výběru, editoru vyjádření (Quill) nebo v pruhu záložek.** Stránka se nesmí přepnout. Pokrývá Task 11 (unit čistých funkcí + E2E v editoru Quill).
3. **Návrat a seznam přežijí kroky i úpravu.** Odkazy na sousedy i „Upravit“ nesou `seznam` a `returnUrl`, drobečková lišta vede na původ. Pokrývá Task 10 (Api).
4. **Záznam mimo seznam (R12)** — uzavřený záznam otevřený se `seznam=aktivni`, cizí jednání, nesmyslná hodnota. Stránka je bez šipek a bez chyby. Pokrývá Task 10 (Api).
5. **Návrh změny harmonogramu.** Krok, který plán neměl a zůstal prázdný, **není** odstranění (jinak by každý návrh „měnil“ plán). Krok, který jen maže plán, **je** změna. Pokrývá Task 3 (unit).
6. **Zamčené pole plánu** (read-only tabulka na stránce záznamu, detail návrhu) ✕ nemá. Pokrývá Task 1 (Api).

---

## Struktura souborů

| Soubor | Změna |
|---|---|
| `PmTracker.Web/Services/Schedules/ScheduleDurationText.cs` | **nový:** text trvání („–“, 1 den / 2–4 dny / dnů) |
| `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml` | `Clearable = canEditDuration`, text trvání přes `ScheduleDurationText` |
| `PmTracker.Web/wwwroot/js/modules/harmonogram/planDuration.js` | **nový:** čisté funkce trvání (zrcadlo serveru) |
| `PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js` | přepis na datum-model |
| `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js` | ✕ se zamyká spolu s kalendářem |
| `PmTracker.Web/wwwroot/js/components/pm-date-field.js` | ✕ se zamyká se změnou atributu `locked` |
| `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs` | návrh nese odstranění plánu |
| `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs` | předá kroky s plánem |
| `PmTracker.Web/Services/RecordProposalService.Queries.cs` | text „Navržený: bez plánu“ |
| `docs/specs/harmonogram-plan-vs-skutecnost.md` | sekce o smazání plánu |
| `PmTracker.Web/wwwroot/css/site.css` | indikátor v okraji, `.record-facts`, skrytí meta řádku, `.record-sequence-nav`, `.records-meeting-empty`; smazat `.record-history` |
| `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` | `ZaznamCardDetailViewModel.DatumZalozeni` |
| `PmTracker.Web/Services/ProjectService.LazyQueries.cs` | založení do detailu; `BuildMeetingRecordsTabAsync` |
| `PmTracker.Web/Views/Projekty/_ZaznamDetailPartial.cshtml` | tabulka `.record-facts` |
| `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml` | bez samostatného „Založeno“; navigace šipkami |
| `PmTracker.Web/Services/Common/MinutesRecordOrdering.cs` | **nový:** pořadí zápisu (jeden zdroj) |
| `PmTracker.Web/Services/Export/ExportTemplateUseCase.cs` | skupiny zápisu přes `MinutesRecordOrdering` |
| `PmTracker.Web/Services/Records/RecordSequence.cs` | **nový:** `RecordSequenceEntry`, `RecordSequenceRequest`, `RecordSequencePosition` |
| `PmTracker.Web/Services/Records/RecordSequenceService.cs` | **nový:** `IRecordSequenceService` + implementace |
| `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` | registrace služby |
| `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` | tisk bere členy jednání ze služby |
| `PmTracker.Web/Services/IProjectService.cs`, `ProjectService.cs` | `BuildMeetingRecordsTabAsync`, závislost na službě |
| `PmTracker.Web/Controllers/JednaniController.cs`, `ProjektyController.TabPartials.cs` | detail jednání jen se záznamy jednání |
| `PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml` | prázdný stav v jednání |
| `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml` | menu: „Otevřít na celou obrazovku“; komentář indikátoru |
| `PmTracker.Web/Controllers/BaseController.RecordsTab.cs`, `ZaznamyController.Partials.cs` | odkazy na stránku se `seznam` |
| `PmTracker.Web/Controllers/ZaznamyController.cs` | `Detail(…, seznam)`, sekvence, odkazy se seznamem |
| `PmTracker.Web/Models/ViewModels/Projekty/ZaznamDetailPageViewModel.cs` | `Sequence` + `RecordSequenceNavigationViewModel` |
| `PmTracker.Web/wwwroot/js/modules/recordSequenceKeys.js` | **nový:** klávesy ← → |
| `PmTracker.Web/wwwroot/js/modules/bootstrap.js` | import modulu kláves |
| Testy — Unit | `ScheduleDurationTextTests`, `PlanDateClearSourceTests`, `RecordProposalPayloadMapperScheduleTests` (úprava), `RecordLayoutCssTests`, `MinutesRecordOrderingTests`, `RecordSequenceRequestTests`, `RecordSequencePositionTests`, `RecordCardOpenPageMenuMarkupTests`; fakes v `ProjektyControllerBehaviorTests`, `ProjektyControllerTeamAuthzTests`, `ZaznamyControllerEditAuthzTests` |
| Testy — Api | `PlanDateClearRenderTests`, `ScheduleProposalPlanRemovalRenderTests`, `RecordFactsTableRenderTests`, `RecordDetailPageRenderTests` (úprava), `MeetingRecordsMembershipRenderTests`, `RecordCardOpenPageLinksRenderTests`, `RecordSequencePageRenderTests` |
| Testy — Integration | `MeetingSequenceSeed`, `ExportTemplateUseCaseTests` (+1), `RecordSequenceServiceTests`, `SchedulePlanRemovalTests`, `IntegrationTestHelper` (+2 metody), `ScheduleProposalRezimTests` (helper přesunut) |
| Testy — JS | `tests/js/schedule/planDuration.test.js`, `tests/js/dom/recordSequenceKeys.test.js` |
| Testy — E2E | `E2EOwnProject`, `PlanDateClearScenariosTests`, `RecordCardLayoutScenariosTests`, `RecordSequenceScenariosTests` |
| `docs/k-otestovani.md` | sekce 6 |

---

## Příprava

- [ ] Z `main` (s commitem tohoto plánu) založit větev: `git checkout -b feat/zaznam-hlavicka-sipky-plan`.
- [ ] Ledger podle executing-plans (`sdd-workspace`), načíst spec a tento plán.
- [ ] Dev DB a aplikace se pro Task 1–11 nepotřebují (testy mají vlastní databáze); průchod v Task 12.

---

### Task 1: Pole plánu — ✕ a „–“ u kroku bez plánu (R13, server)

**Files:**
- Create: `PmTracker.Web/Services/Schedules/ScheduleDurationText.cs`
- Modify: `PmTracker.Web/Views/Shared/_ScheduleTable.cshtml:58-82`
- Test: `PmTracker.Tests.Unit/Schedules/ScheduleDurationTextTests.cs`
- Test: `PmTracker.Tests.Api/Controllers/PlanDateClearRenderTests.cs`

**Interfaces:**
- Produces: `ScheduleDurationText.NoPlan` (`"–"`), `ScheduleDurationText.Format(int days) : string`, `ScheduleDurationText.ForStep(DateTime? planDatum, int trvaniDni) : string`. JS zrcadlo v Task 2 musí dávat stejné texty.

- [ ] **Step 1: Unit test textu trvání**

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedules;

/// <summary>Spec 2026-10-22 R13: text trvání kroku. Zrcadlo v JS: harmonogram/planDuration.js.</summary>
public sealed class ScheduleDurationTextTests
{
    [Theory]
    [InlineData(0, "0 dnů")]
    [InlineData(1, "1 den")]
    [InlineData(2, "2 dny")]
    [InlineData(4, "4 dny")]
    [InlineData(5, "5 dnů")]
    [InlineData(21, "21 dnů")]
    public void Format_CzechPlural(int days, string expected)
        => ScheduleDurationText.Format(days).Should().Be(expected);

    [Fact]
    public void ForStep_WithoutPlan_IsDash()
        => ScheduleDurationText.ForStep(planDatum: null, trvaniDni: 0).Should().Be("–");

    [Fact]
    public void ForStep_WithPlan_FormatsDays()
        => ScheduleDurationText.ForStep(new DateTime(2026, 10, 22), 3).Should().Be("3 dny");
}
```

- [ ] **Step 2: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~ScheduleDurationTextTests"`
Expected: build FAIL — `ScheduleDurationText` neexistuje.

- [ ] **Step 3: Implementace**

```csharp
namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-22 R13: text trvání kroku v tabulce harmonogramu. Krok bez plánu má „–“
/// (dřív „0 dnů“, jako by plán byl nulový). Zrcadlo v JS: harmonogram/planDuration.js
/// (formatDuration) — editor text přepočítává při změně data i po ✕.
/// </summary>
public static class ScheduleDurationText
{
    public const string NoPlan = "–";

    public static string Format(int days)
        => $"{days} {(days == 1 ? "den" : days is >= 2 and <= 4 ? "dny" : "dnů")}";

    public static string ForStep(DateTime? planDatum, int trvaniDni)
        => planDatum.HasValue ? Format(trvaniDni) : NoPlan;
}
```

- [ ] **Step 4: Spustit — projde**

Run: stejný příkaz jako ve Step 2.
Expected: PASS 8/8.

- [ ] **Step 5: Api test — ✕ a text trvání**

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R13: pole plánu v harmonogramu má ✕ (smazat datum), když ho uživatel smí
/// upravit — v editoru záznamu i v návrhu změny harmonogramu. Read-only tabulka na stránce
/// záznamu ✕ nemá. Krok bez plánu má trvání „–“ (dřív „0 dnů“).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class PlanDateClearRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public PlanDateClearRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Úkol založený před 40 dny; plán kroku k = založení + 3·k, krok <paramref name="bezPlanu"/> bez plánu.</summary>
    private async Task<(int ProjectId, int RecordId)> SeedTaskAsync(string marker, int? bezPlanu = null)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", marker);
        var zalozeni = DateTime.Today.AddDays(-40);
        await _fixture.SetRecordFoundingDateAsync(recordId, zalozeni);

        await using var db = _fixture.CreateDbContext();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = poradi == bezPlanu ? null : zalozeni.AddDays(3 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (projectId, recordId);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static IReadOnlyList<string> PlanFields(string html)
        => Regex.Matches(html, "<pm-date-field\\b[^>]*data-schedule-plan-step=\"\\d+\"[^>]*>")
            .Select(m => m.Value)
            .ToList();

    private static string DurationText(string html, int poradi)
    {
        var row = Regex.Match(html, $"<tr data-schedule-step-row\\s+data-step-index=\"{poradi}\"[\\s\\S]*?</tr>").Value;
        return Regex.Match(row, "data-schedule-duration-readonly[^>]*>\\s*([^<]*?)\\s*</span>").Groups[1].Value;
    }

    [Fact]
    public async Task Editor_PlanFields_HaveClearButton()
    {
        var s = await SeedTaskAsync("APPLANX1");

        var html = await GetHtmlAsync($"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        var fields = PlanFields(html);
        fields.Should().HaveCount(10);
        fields.Should().OnlyContain(f => f.Contains("clearable=\"true\"") && f.Contains("locked=\"false\""),
            "kdo smí plán upravit, smí ho i smazat (R13)");
    }

    [Fact]
    public async Task ScheduleProposalEditor_PlanFields_HaveClearButton()
    {
        var s = await SeedTaskAsync("APPLANX2");

        var html = await GetHtmlAsync(
            $"/Navrhy/CreateScheduleProposal?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        PlanFields(html).Should().HaveCount(10)
            .And.OnlyContain(f => f.Contains("clearable=\"true\""), "odstranění plánu jde navrhnout (R13, R14)");
    }

    [Fact]
    public async Task RecordPageTable_PlanFields_HaveNoClearButton()
    {
        var s = await SeedTaskAsync("APPLANX3");

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        PlanFields(html).Should().HaveCount(10)
            .And.OnlyContain(f => f.Contains("clearable=\"false\"") && f.Contains("locked=\"true\""),
                "tabulka na stránce záznamu je jen pro čtení");
    }

    [Fact]
    public async Task Editor_DurationText_DashWithoutPlan_CzechPlural()
    {
        var s = await SeedTaskAsync("APPLANX4", bezPlanu: 3);

        var html = await GetHtmlAsync($"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}");

        DurationText(html, 1).Should().Be("3 dny");
        DurationText(html, 3).Should().Be("–", "krok bez plánu nemá trvání (dřív „0 dnů“)");
        DurationText(html, 4).Should().Be("6 dnů", "krok 4 začíná koncem kroku 2 — posledního kroku s plánem");
    }
}
```

- [ ] **Step 6: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~PlanDateClearRenderTests"`

Expected: 3 FAIL, 1 PASS.
- Selžou `Editor_PlanFields_HaveClearButton` a `ScheduleProposalEditor_PlanFields_HaveClearButton` (pole mají `clearable="false"`).
- Selže `Editor_DurationText_DashWithoutPlan_CzechPlural` (dnes „3 dnů“ a „0 dnů“).
- `RecordPageTable_PlanFields_HaveNoClearButton` projde (pojistka).

Pokud `ScheduleProposalEditor_…` selže na `locked`, nebo pole v návrhu nemá plán upravitelný, nejde o chybu testu. Návrh pak pole plánu zamyká a R13 v návrhu nejde splnit bez další změny. Postup podle systematic-debugging a ruling do ledgeru.

- [ ] **Step 7: Implementace v `_ScheduleTable.cshtml`**

V `_AppDateField` pole plánu (řádek s `Locked = dateDisabled,`) doplnit:

```cshtml
                                                Locked = dateDisabled,
                                                // Spec 2026-10-22 R13: plán smí smazat, kdo ho smí upravit. Kalendář datum
                                                // jen vybírá — bez ✕ prázdný plán nešel vrátit. Zámek při změně kategorie
                                                // ✕ sleduje spolu s kalendářem (form.js, pm-date-field.js).
                                                Clearable = canEditDuration,
```

Text trvání nahradit (místo `@krok.TrvaniDni dnů`):

```cshtml
                                            @(PmTracker.Web.Services.Schedules.ScheduleDurationText.ForStep(krok.PlanDatum, krok.TrvaniDni))
```

- [ ] **Step 8: Spustit — projde, a sousední sady**

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~PlanDateClearRenderTests|FullyQualifiedName~RecordDetailPageRenderTests|FullyQualifiedName~ScheduleProposal|FullyQualifiedName~RecordEditorControllerTests"`
Expected: vše PASS.

- [ ] **Step 9: Commit**

```bash
git add PmTracker.Web/Services/Schedules/ScheduleDurationText.cs PmTracker.Web/Views/Shared/_ScheduleTable.cshtml PmTracker.Tests.Unit/Schedules/ScheduleDurationTextTests.cs PmTracker.Tests.Api/Controllers/PlanDateClearRenderTests.cs
git commit -m "feat(harmonogram): pole plánu má ✕, krok bez plánu má trvání „–“ (spec 2026-10-22 R13)"
```

---

### Task 2: Editor — živý přepočet trvání a ✕ spolu s kalendářem (R13, JS)

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/harmonogram/planDuration.js`
- Modify (přepis): `PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/recordEditor/form.js` (`setScheduleDateFieldState`)
- Modify: `PmTracker.Web/wwwroot/js/components/pm-date-field.js` (`_applyAttributeChange`, `ATTR_LOCKED`)
- Test: `tests/js/schedule/planDuration.test.js`
- Test: `PmTracker.Tests.Unit/Architecture/PlanDateClearSourceTests.cs`
- Test: `PmTracker.Tests.E2E/Scenarios/PlanDateClearScenariosTests.cs`

**Interfaces:**
- Consumes: texty z Task 1 (`„–“`, `1 den`, `2–4 dny`, jinak `dnů`).
- Produces: `planDuration.js` exportuje `NO_PLAN_TEXT`, `planStart(plansByStep: Map<number,string>, stepIndex: number, recordStartIso: string) → { iso: string, fromPreviousStep: boolean }`, `daysBetween(startIso, endIso) → number|null`, `formatDuration(days: number|null) → string`.

Proč: dnešní `recalcRow` hledá `[data-schedule-duration-hidden]`, který datum-model zrušil, a hned skončí. Text trvání se proto po změně data ani po ✕ nemění. Kontrola pořadí (uložení zastaví plán dřív než plán předchozího kroku) dnes taky neběží. Oživí se se stejným pravidlem jako server (`ScheduleChronologyValidator`). Proti datu založení se nekontroluje, protože server to taky nedělá.

- [ ] **Step 1: JS test**

`tests/js/schedule/planDuration.test.js`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";

import {
    daysBetween,
    formatDuration,
    NO_PLAN_TEXT,
    planStart
} from "../../../PmTracker.Web/wwwroot/js/modules/harmonogram/planDuration.js";

test("krok bez plánu má trvání „–“", () => {
    assert.equal(formatDuration(null), "–");
    assert.equal(NO_PLAN_TEXT, "–");
});

test("české tvary jako server: 1 den, 2–4 dny, jinak dnů", () => {
    assert.equal(formatDuration(0), "0 dnů");
    assert.equal(formatDuration(1), "1 den");
    assert.equal(formatDuration(3), "3 dny");
    assert.equal(formatDuration(5), "5 dnů");
});

test("začátek kroku = konec nejbližšího předchozího kroku s plánem", () => {
    const plans = new Map([[1, "2026-10-01"], [2, "2026-10-08"], [4, "2026-10-22"]]);
    assert.deepEqual(planStart(plans, 4, "2026-09-01"), { iso: "2026-10-08", fromPreviousStep: true });
    assert.deepEqual(planStart(plans, 3, "2026-09-01"), { iso: "2026-10-08", fromPreviousStep: true });
});

test("bez předchozího kroku s plánem začíná krok datem založení", () => {
    assert.deepEqual(planStart(new Map([[3, "2026-10-15"]]), 3, "2026-09-01"), { iso: "2026-09-01", fromPreviousStep: false });
    assert.deepEqual(planStart(new Map(), 1, "2026-09-01"), { iso: "2026-09-01", fromPreviousStep: false });
});

test("počet dnů mezi daty; záporný při obráceném pořadí; null při neplatném vstupu", () => {
    assert.equal(daysBetween("2026-10-08", "2026-10-22"), 14);
    assert.equal(daysBetween("2026-10-22", "2026-10-08"), -14);
    assert.equal(daysBetween("2026-03-28", "2026-03-30"), 2, "UTC — přechod na letní čas den neztratí");
    assert.equal(daysBetween("", "2026-10-08"), null);
});
```

- [ ] **Step 2: Spustit — selže**

Run: `npm test`
Expected: FAIL — `Cannot find module …/planDuration.js` (ostatních 45 testů projde).

- [ ] **Step 3: `planDuration.js`**

```js
/**
 * Spec 2026-10-22 R13: trvání kroku v editoru harmonogramu (datum-model). Čisté funkce bez DOM,
 * volá je duration-calendar-binding.js při změně data i po smazání (✕).
 * Zrcadlo serveru: ScheduleDurationText (text) a ScheduleDateCalculator (začátek kroku = konec
 * předchozího kroku s plánem, jinak datum založení záznamu).
 */

export const NO_PLAN_TEXT = "–";

/** Začátek kroku: konec nejbližšího předchozího kroku s plánem, jinak datum založení. */
export function planStart(plansByStep, stepIndex, recordStartIso) {
    for (let index = stepIndex - 1; index >= 1; index -= 1) {
        const iso = plansByStep.get(index);
        if (iso) {
            return { iso, fromPreviousStep: true };
        }
    }
    return { iso: recordStartIso, fromPreviousStep: false };
}

/** Počet kalendářních dnů mezi dvěma daty yyyy-MM-dd; null při neplatném vstupu. */
export function daysBetween(startIso, endIso) {
    const start = parseIso(startIso);
    const end = parseIso(endIso);
    if (!start || !end) {
        return null;
    }
    return Math.round((end.getTime() - start.getTime()) / 86400000);
}

/** „1 den“, „2–4 dny“, jinak „N dnů“; null = krok bez plánu („–“). */
export function formatDuration(days) {
    if (days === null || days === undefined) {
        return NO_PLAN_TEXT;
    }
    const unit = days === 1 ? "den" : days >= 2 && days <= 4 ? "dny" : "dnů";
    return `${days} ${unit}`;
}

function parseIso(value) {
    if (typeof value !== "string") {
        return null;
    }
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
    if (!match) {
        return null;
    }
    const date = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3])));
    return Number.isNaN(date.getTime()) ? null : date;
}
```

- [ ] **Step 4: Spustit — projde**

Run: `npm test`
Expected: PASS 50/50.

- [ ] **Step 5: Zdrojová pojistka (Unit)**

`PmTracker.Tests.Unit/Architecture/PlanDateClearSourceTests.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-10-22 R13: ✕ u plánu se zamyká spolu s kalendářem (form.js při změně kategorie,
/// pm-date-field při změně atributu locked). Přepočet trvání běží v datum-modelu — dřív hledal
/// zrušený skrytý vstup data-schedule-duration-hidden a nedělal nic.
/// </summary>
public sealed class PlanDateClearSourceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepoRoot(), relative));

    [Fact]
    public void FormJs_ClearButtonFollowsCalendarLock()
        => Regex.IsMatch(
                Read("PmTracker.Web/wwwroot/js/modules/recordEditor/form.js"),
                @"setScheduleDateFieldState = \(\) => \{[\s\S]*?\[data-app-date-clear\][\s\S]*?clearButton\.disabled = shouldDisable")
            .Should().BeTrue("✕ se odemyká a zamyká s kalendářem (např. přepnutí kategorie na Úkol)");

    [Fact]
    public void PmDateField_LockedAttributeChange_LocksClearButton()
        => Regex.IsMatch(
                Read("PmTracker.Web/wwwroot/js/components/pm-date-field.js"),
                @"case ATTR_LOCKED: \{[\s\S]*?\[data-app-date-clear\][\s\S]*?break;")
            .Should().BeTrue();

    [Fact]
    public void DurationBinding_UsesDatumModel()
    {
        var src = Read("PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js");
        src.Should().Contain("from \"./planDuration.js\"");
        src.Should().NotContain("data-schedule-duration-hidden", "skrytý vstup trvání datum-model zrušil");
    }
}
```

- [ ] **Step 6: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~PlanDateClearSourceTests"`
Expected: 3 FAIL.

- [ ] **Step 7: `form.js` a `pm-date-field.js`**

`form.js`, ve `setScheduleDateFieldState` za blok `trigger.disabled = shouldDisable;`:

```js
                const trigger = dateField.querySelector("[data-app-date-open]");
                if (trigger instanceof HTMLButtonElement) {
                    trigger.disabled = shouldDisable;
                }

                // Spec 2026-10-22 R13: ✕ (smazat datum) se zamyká spolu s kalendářem.
                const clearButton = dateField.querySelector("[data-app-date-clear]");
                if (clearButton instanceof HTMLButtonElement) {
                    clearButton.disabled = shouldDisable;
                }
            });
        };
```

`pm-date-field.js`, `case ATTR_LOCKED`:

```js
            case ATTR_LOCKED: {
                const locked = this.getAttribute(ATTR_LOCKED) === "true";
                wrapper.dataset.appDateLocked = locked ? "true" : "false";
                if (trigger) {
                    if (locked) trigger.setAttribute("disabled", "disabled");
                    else trigger.removeAttribute("disabled");
                }
                // Spec 2026-10-22 R13: ✕ (smazat datum) se zamyká spolu s kalendářem.
                const clearButton = wrapper.querySelector("[data-app-date-clear]");
                if (clearButton) {
                    if (locked) clearButton.setAttribute("disabled", "disabled");
                    else clearButton.removeAttribute("disabled");
                }
                break;
            }
```

- [ ] **Step 8: E2E (napsat před přepisem binding modulu)**

`PmTracker.Tests.E2E/Scenarios/PlanDateClearScenariosTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22 R13: ✕ u plánu kroku v editoru vyprázdní datum a trvání kroku ukáže „–“.
/// Další krok se přepočte od posledního kroku s plánem a po uložení je plán v databázi prázdný.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class PlanDateClearScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public PlanDateClearScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private PmTrackerDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(_fixture.Database.ConnectionString).Options);

    /// <summary>Úkol ve fixture projektu založený před 40 dny, plán kroku k = založení + 7·k.</summary>
    private async Task<(int RecordId, DateTime Zalozeni)> SeedAsync()
    {
        await using var db = CreateDbContext();
        var subsystemId = await db.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == _fixture.ProjectId && x.DatumOdebrani == null)
            .OrderBy(x => x.SubsystemId).Select(x => x.SubsystemId).FirstAsync();
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var stateId = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var number = (await db.ProjektoveZaznamy.Where(x => x.ProjektId == _fixture.ProjectId)
            .Select(x => (int?)x.CisloZaznamu).MaxAsync() ?? 0) + 1;
        var zalozeni = DateTime.Today.AddDays(-40);
        var record = new ProjektovyZaznamEntity
        {
            ProjektId = _fixture.ProjectId, KategorieId = categoryId, StavUkoluId = stateId, CisloZaznamu = number,
            Nazev = "E2E smazani planu", Cil = "E2E smazani planu", Popis = "E2E smazani planu",
            VlastnikId = _fixture.AdminOsobaId, DatumZalozeni = zalozeni, DatumUkonceni = DateTime.Today.AddDays(60),
            SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        await db.SaveChangesAsync();
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = record.Id,
                Poradi = (byte)poradi,
                PlanDatum = zalozeni.AddDays(7 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        return (record.Id, zalozeni);
    }

    private async Task CleanupAsync(int recordId)
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.zaznam_edit_zamek WHERE zaznam_id = {recordId}");
        await db.ZaznamHarmonogramKroky.Where(x => x.ZaznamId == recordId).ExecuteDeleteAsync();
        await db.ProjektoveZaznamy.Where(x => x.Id == recordId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Editor_ClearPlan_ShowsDash_RecalculatesNextStep_AndSavesEmptyPlan()
    {
        var (recordId, zalozeni) = await SeedAsync();
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
            var row3 = page.Locator("tr[data-schedule-step-row][data-step-index='3']");
            var clear = row3.Locator("[data-app-date-clear]");
            await Expect(clear).ToHaveCountAsync(1);
            await Expect(clear).ToBeEnabledAsync();

            await clear.DispatchEventAsync("click");

            await Expect(row3.Locator("input[data-app-date-value]")).ToHaveValueAsync(string.Empty);
            await Expect(row3.Locator("[data-schedule-duration-readonly]")).ToHaveTextAsync("–");
            await Expect(page.Locator("tr[data-schedule-step-row][data-step-index='4'] [data-schedule-duration-readonly]"))
                .ToHaveTextAsync("14 dnů");

            var saveTask = page.WaitForResponseAsync(response =>
                response.Request.Method == "POST"
                && response.Url.Contains("/Zaznamy/Save", StringComparison.OrdinalIgnoreCase));
            await page.Locator("form[data-record-editor-form]").EvaluateAsync("form => form.requestSubmit()");
            var save = await saveTask;
            save.Ok.Should().BeTrue(await save.TextAsync());

            await using var db = CreateDbContext();
            var plans = await db.ZaznamHarmonogramKroky.AsNoTracking()
                .Where(x => x.ZaznamId == recordId)
                .ToDictionaryAsync(x => (int)x.Poradi, x => x.PlanDatum);
            plans[3].Should().BeNull("✕ plán kroku 3 smazal");
            plans[2].Should().Be(zalozeni.AddDays(14));
            plans[4].Should().Be(zalozeni.AddDays(28));
        }
        finally
        {
            await page.Context.CloseAsync();
            await CleanupAsync(recordId);
        }
    }
}
```

Run: `dotnet test PmTracker.Tests.E2E --nologo --filter "FullyQualifiedName~PlanDateClearScenariosTests"`
Expected: FAIL na `ToHaveTextAsync("–")` — binding dnešní text nepřepočítá. To dokazuje, že test chybu chytá.

- [ ] **Step 9: Přepis `duration-calendar-binding.js`**

Celý soubor nahradit:

```js
/**
 * DESIGN-9-F (2026-05-02) — primární editor plánu kroku je kalendář (datum konce kroku),
 * trvání ve dnech je text vedle něj (data-schedule-duration-readonly).
 *
 * Spec 2026-10-22 R13 — přepis na datum-model. Dřívější verze hledala skrytý vstup
 * data-schedule-duration-hidden, který datum-model zrušil, takže přepočet tiše nedělal nic
 * a text trvání zůstával z načtení stránky. Teď při každé změně data (i po ✕):
 *   - krok bez plánu má trvání „–“;
 *   - začátek kroku = konec nejbližšího předchozího kroku s plánem, jinak datum založení
 *     (data-schedule-start na .schedule-block) — jako server (ScheduleDateCalculator);
 *   - plán dřív než plán předchozího kroku s plánem se označí a uložení se zastaví —
 *     stejné pravidlo jako server (ScheduleChronologyValidator), proti založení se nekontroluje.
 * Změna kroku N přepočítá kroky N..10 (posune se začátek dalších kroků).
 *
 * Side-effect import v bootstrap.js (memory: project_bundle_sync).
 */
import { daysBetween, formatDuration, planStart } from "./planDuration.js";

const ROW_SELECTOR = "[data-schedule-step-row]";
const CALENDAR_SELECTOR = "[data-schedule-duration-calendar]";
const READONLY_SELECTOR = "[data-schedule-duration-readonly]";
const SCHEDULE_BLOCK_SELECTOR = "[data-schedule-start]";

function stepIndexOf(row) {
    return Number.parseInt(row.getAttribute("data-step-index") || "0", 10);
}

function readPlanIso(row) {
    const calendar = row.querySelector(CALENDAR_SELECTOR);
    const hidden = calendar?.querySelector('input[type="hidden"][data-app-date-value]');
    return hidden?.value || null;
}

function readPlansByStep(rows) {
    const plans = new Map();
    rows.forEach((row) => {
        const iso = readPlanIso(row);
        if (iso) {
            plans.set(stepIndexOf(row), iso);
        }
    });
    return plans;
}

function formatDisplayDate(iso) {
    const [year, month, day] = iso.slice(0, 10).split("-");
    return `${day}.${month}.${year}`;
}

function markRowInvalid(row, invalid, message) {
    if (invalid) {
        row.setAttribute("data-schedule-duration-invalid", "true");
    } else {
        row.removeAttribute("data-schedule-duration-invalid");
    }
    // Tooltip + aria — na pm-date-field i na vnitřní zobrazovací vstup.
    const calendar = row.querySelector(CALENDAR_SELECTOR);
    if (!(calendar instanceof HTMLElement)) {
        return;
    }
    if (invalid) {
        calendar.setAttribute("title", message);
        calendar.setAttribute("aria-invalid", "true");
    } else {
        calendar.removeAttribute("title");
        calendar.removeAttribute("aria-invalid");
    }
    const display = calendar.querySelector("input[data-app-date-display]");
    if (display instanceof HTMLInputElement) {
        if (invalid) {
            display.setAttribute("title", message);
        } else {
            display.removeAttribute("title");
        }
    }
}

function recalcRow(row, recordStartIso, plans) {
    const readonly = row.querySelector(READONLY_SELECTOR);
    if (!(readonly instanceof HTMLElement)) {
        return;
    }

    const endIso = readPlanIso(row);
    if (!endIso) {
        readonly.textContent = formatDuration(null);
        row.setAttribute("data-step-duration", "0");
        markRowInvalid(row, false, "");
        return;
    }

    const start = planStart(plans, stepIndexOf(row), recordStartIso);
    const rawDays = daysBetween(start.iso, endIso);
    const days = Math.max(0, rawDays ?? 0);
    readonly.textContent = formatDuration(days);
    row.setAttribute("data-step-duration", String(days));

    if (start.fromPreviousStep && rawDays !== null && rawDays < 0) {
        markRowInvalid(row, true,
            `Datum konce kroku nesmí být dříve než ${formatDisplayDate(start.iso)} (konec předchozího kroku s plánem).`);
    } else {
        markRowInvalid(row, false, "");
    }
}

function recalcChainFrom(stepIndex, scope) {
    const rows = Array.from(scope.querySelectorAll(ROW_SELECTOR))
        .sort((left, right) => stepIndexOf(left) - stepIndexOf(right));
    const plans = readPlansByStep(rows);
    const startAttr = scope instanceof HTMLElement ? scope.getAttribute("data-schedule-start") : null;
    const recordStartIso = startAttr || new Date().toISOString().slice(0, 10);
    rows.filter((row) => stepIndexOf(row) >= stepIndex)
        .forEach((row) => recalcRow(row, recordStartIso, plans));
}

function init() {
    // FIX 2026-05-02: capture fáze + setTimeout 0 — přepočet proběhne až po ostatních
    // posluchačích change (block.js), poslední slovo má tenhle modul.
    document.addEventListener("change", (event) => {
        const target = event.target;
        if (!(target instanceof HTMLInputElement)) {
            return;
        }
        const row = target.closest(CALENDAR_SELECTOR)?.closest(ROW_SELECTOR);
        if (!(row instanceof HTMLElement)) {
            return;
        }
        const scope = row.closest(SCHEDULE_BLOCK_SELECTOR) || document;
        const stepIndex = stepIndexOf(row);
        setTimeout(() => recalcChainFrom(stepIndex, scope), 0);
    }, true);

    // FIX 2026-05-05: uložení se zastaví, když je některý krok označený jako nechronologický —
    // uživatel hned vidí, které pole to je (server by uložení odmítl stejným pravidlem).
    document.addEventListener("submit", (event) => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) {
            return;
        }
        const invalidRow = form.querySelector(`${ROW_SELECTOR}[data-schedule-duration-invalid="true"]`);
        if (!invalidRow) {
            return;
        }
        event.preventDefault();
        const calendar = invalidRow.querySelector(CALENDAR_SELECTOR);
        const display = calendar?.querySelector("input[data-app-date-display]");
        if (display instanceof HTMLInputElement) {
            display.scrollIntoView({ behavior: "smooth", block: "center" });
            display.focus({ preventScroll: true });
        }
        window.alert(calendar?.getAttribute("title")
            || "Některý krok harmonogramu má datum konce dříve než předchozí krok.");
    }, true);
}

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
} else {
    init();
}
```

`window.pmDurationCalendarBinding` odpadá, nikdo ho nepoužívá (ověřeno vyhledáním ve `wwwroot/js`, `Views` a testech).

- [ ] **Step 10: Spustit — vše projde**

Run:
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~PlanDateClearSourceTests|FullyQualifiedName~HarmonogramPhantomUiFixesTests"`
- `npm test`
- `dotnet test PmTracker.Tests.E2E --nologo --filter "FullyQualifiedName~PlanDateClearScenariosTests"`

Expected: Unit PASS, JS 50/50, E2E 1/1.

- [ ] **Step 11: Ověřit graf v editoru (spec 4.D „ověřit dnešní chování a sjednotit“)**

Stačí jedno spuštění E2E s dočasným snímkem (`page.ScreenshotAsync` do scratchpadu, po ověření smazat), nebo krátký Node skript proti testovací aplikaci. Postup: editor záznamu s plánem → ✕ u kroku 3.

1. Zjistit, jestli graf v editoru reaguje na změnu data (překreslí se po výběru jiného data).
2. Když reaguje, krok bez plánu nesmí kreslit pruh. Jinak je to nález k opravě v tomto tasku, s testem.
3. Když nereaguje ani na výběr data (dnešní stav), zapsat ruling do ledgeru a známé omezení do `k-otestovani` (sekce 6). Serverové zobrazení (karta, stránka záznamu, záložka Harmonogram) pruh pro krok s nulovou délkou nekreslí už dnes (`_ScheduleBlock.cshtml`: `TrvaniDni <= 0` → `display:none`).

- [ ] **Step 12: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/harmonogram/planDuration.js PmTracker.Web/wwwroot/js/modules/harmonogram/duration-calendar-binding.js PmTracker.Web/wwwroot/js/modules/recordEditor/form.js PmTracker.Web/wwwroot/js/components/pm-date-field.js tests/js/schedule/planDuration.test.js PmTracker.Tests.Unit/Architecture/PlanDateClearSourceTests.cs PmTracker.Tests.E2E/Scenarios/PlanDateClearScenariosTests.cs
git commit -m "fix(harmonogram): trvání kroku se v editoru přepočítá i po ✕, ✕ se zamyká s kalendářem (spec 2026-10-22 R13)"
```

---

### Task 3: Návrh změny harmonogramu nese odstranění plánu (R14)

**Files:**
- Modify: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs:59-65`
- Modify: `PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs:139-149`
- Modify: `PmTracker.Web/Services/RecordProposalService.Queries.cs:540-557`
- Modify: `PmTracker.Tests.Unit/Records/RecordProposalPayloadMapperScheduleTests.cs`
- Modify: `PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestHelper.cs` (+ `BuildScheduleEditCommand`)
- Modify: `PmTracker.Tests.Integration/DataStore/ScheduleProposalRezimTests.cs` (helper přesunut)
- Create: `PmTracker.Tests.Integration/DataStore/SchedulePlanRemovalTests.cs`
- Create: `PmTracker.Tests.Api/Controllers/ScheduleProposalPlanRemovalRenderTests.cs`
- Modify: `docs/specs/harmonogram-plan-vs-skutecnost.md`

**Interfaces:**
- Produces: `RecordProposalPayloadMapper.BuildSchedulePayload(SaveRecordCommand command, DateTime originalDeadline, bool currentAutoFill, IReadOnlySet<int> currentPlanSteps)`; `IntegrationTestHelper.BuildScheduleEditCommand(ZaznamEditViewModel editor) : SaveRecordCommand`.

- [ ] **Step 1: Unit testy mapperu**

V `RecordProposalPayloadMapperScheduleTests`:
- přidat pole `private static readonly IReadOnlySet<int> BezPlanu = new HashSet<int>();`;
- ke čtyřem stávajícím voláním `BuildSchedulePayload(…)` doplnit poslední argument `currentPlanSteps: BezPlanu`;
- přidat:

```csharp
    /// <summary>
    /// Spec 2026-10-22 R14: návrh nese odstranění plánu — krok, který plán v záznamu má a ve
    /// formuláři přišel prázdný. Krok bez plánu, který zůstal prázdný, změnou není (jinak by
    /// každý návrh „měnil“ plán všech nevyplněných kroků).
    /// </summary>
    [Fact]
    public void BuildSchedulePayload_NeseOdstraneniPlanu_PrazdnyKrokBezPlanuNe()
    {
        var a = new DateTime(2026, 10, 5);
        var command = new SaveRecordCommand
        {
            TerminUkonceni = new DateTime(2026, 10, 31),
            HarmonogramHodnoty =
            [
                new SaveRecordHarmonogramValueCommand { Poradi = 1, PlanDatum = a },
                new SaveRecordHarmonogramValueCommand { Poradi = 2, PlanDatum = null },
                new SaveRecordHarmonogramValueCommand { Poradi = 3, PlanDatum = null }
            ]
        };

        var plan = new RecordProposalPayloadMapper()
            .BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: true, currentPlanSteps: new HashSet<int> { 1, 2 })
            .SchedulePlan!;

        plan.PlannedHarmonogramHodnoty.Select(x => (x.Poradi, x.PlanDatum)).Should().Equal(
            (1, (DateTime?)a),
            (2, (DateTime?)null));
        plan.ChangesSchedulePlan.Should().BeTrue();
    }

    [Fact]
    public void BuildSchedulePayload_JenOdstraneniPlanu_JeZmenaPlanu()
    {
        var command = new SaveRecordCommand
        {
            TerminUkonceni = new DateTime(2026, 10, 31),
            HarmonogramHodnoty = [new SaveRecordHarmonogramValueCommand { Poradi = 4, PlanDatum = null }]
        };

        var plan = new RecordProposalPayloadMapper()
            .BuildSchedulePayload(command, new DateTime(2026, 10, 31), currentAutoFill: true, currentPlanSteps: new HashSet<int> { 4 })
            .SchedulePlan!;

        plan.ChangesSchedulePlan.Should().BeTrue("návrh, který jen maže plán, není prázdný návrh");
        plan.PlannedHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 4 && x.PlanDatum == null);
    }
```

- [ ] **Step 2: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordProposalPayloadMapperScheduleTests"`
Expected: build FAIL — `BuildSchedulePayload` nemá parametr `currentPlanSteps`.

- [ ] **Step 3: Mapper**

```csharp
    /// <param name="currentPlanSteps">Kroky (pořadí 1–10), které mají v záznamu plán. Spec 2026-10-22
    /// R14: prázdné pole plánu u takového kroku je navržené odstranění plánu.</param>
    public RecordProposalPayload BuildSchedulePayload(
        SaveRecordCommand command, DateTime originalDeadline, bool currentAutoFill, IReadOnlySet<int> currentPlanSteps)
    {
        var changesTermDeadline = command.TerminUkonceni.Date != originalDeadline.Date;
        // Vyplněné plány jako dřív + odstranění (R14): krok, který plán v záznamu má a ve formuláři
        // přišel prázdný. Krok bez plánu, který zůstal prázdný, změnou není.
        var planned = command.HarmonogramHodnoty
            .Where(v => v.PlanDatum.HasValue || currentPlanSteps.Contains(v.Poradi))
            .Select(v => new SaveRecordHarmonogramValueCommand { Poradi = v.Poradi, PlanDatum = v.PlanDatum })
            .ToList();
```

Zbytek metody beze změny.

- [ ] **Step 4: Odeslání návrhu předá kroky s plánem**

V `SubmitCommands.cs`:

```csharp
        var currentKroky = await _dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(k => k.ZaznamId == record.Id)
            .Select(k => new { k.Poradi, k.SkutecnostRezim, k.PlanDatum })
            .ToListAsync(ct);
        var currentAutoFill = HarmonogramAutoFillRule.IsAutoFill(currentKroky.Select(k => ((int)k.Poradi, k.SkutecnostRezim)));
        // Spec 2026-10-22 R14: kroky s plánem — prázdné pole plánu u nich je navržené odstranění.
        var currentPlanSteps = currentKroky.Where(k => k.PlanDatum.HasValue).Select(k => (int)k.Poradi).ToHashSet();

        var payload = _payloadMapper.BuildSchedulePayload(command, record.DatumUkonceni, currentAutoFill, currentPlanSteps);
```

- [ ] **Step 5: Spustit unit — projde**

Run: příkaz ze Step 2.
Expected: PASS (stávajících 9 + 2 nové).

- [ ] **Step 6: Integrační test (odeslání → schválení)**

Do `IntegrationTestHelper` přidat (přesun z `ScheduleProposalRezimTests.BuildEditCommand`, tělo beze změny):

```csharp
    /// <summary>Příkaz uložení z editoru návrhu změny harmonogramu (základní pole beze změny, harmonogram prázdný).</summary>
    public static SaveRecordCommand BuildScheduleEditCommand(ZaznamEditViewModel editor)
    {
        var subsystemOption = editor.Subsystemy.First(candidate =>
            string.Equals(candidate.Kod, editor.Subsystem, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Nazev, editor.Subsystem, StringComparison.OrdinalIgnoreCase));
        var subsystem = string.IsNullOrWhiteSpace(subsystemOption.Kod) ? subsystemOption.Nazev : subsystemOption.Kod;

        return new SaveRecordCommand
        {
            Id = editor.Id,
            ProjektId = editor.ProjektId,
            Kategorie = editor.Kategorie,
            TypUkolu = editor.TypUkolu,
            Stav = editor.Stav,
            Nazev = editor.Nazev,
            Cil = editor.Cil,
            Popis = editor.Popis,
            VlastnikId = editor.VlastnikId,
            DatumZalozeni = editor.DatumZalozeni,
            TerminUkonceni = editor.TerminUkonceni ?? editor.DatumZalozeni,
            Subsystem = subsystem,
            VybraniSpolupracovniciIds = editor.VybraniSpolupracovniciIds.ToList(),
            ExterniVazby = [],
            HarmonogramHodnoty = [],
            EditorTab = "schedule"
        };
    }
```

V `ScheduleProposalRezimTests` nahradit celou privátní metodu `BuildEditCommand` řádkem:

```csharp
    private static SaveRecordCommand BuildEditCommand(ZaznamEditViewModel editor)
        => IntegrationTestHelper.BuildScheduleEditCommand(editor);
```

`PmTracker.Tests.Integration/DataStore/SchedulePlanRemovalTests.cs`:

```csharp
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-10-22 R13, R14: smazání plánu kroku.
/// - Uložení editoru zapíše prázdný plán (server to uměl už dřív — pojistka).
/// - Návrh změny harmonogramu nese i odstranění plánu (dřív ho mapper zahodil a návrh plán
///   „nezměnil“). Schválení plán kroku smaže a ostatní kroky nechá.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class SchedulePlanRemovalTests
{
    private readonly SqlIntegrationFixture _fixture;

    public SchedulePlanRemovalTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int RecordId, int ProposerId, int ManagerId, DateTime Start);

    /// <summary>Úkol s plánem kroku k = založení + 3·k; vedoucí subsystému a PM projektu.</summary>
    private static async Task<Seed> SeedAsync(PmTrackerDbContext dbContext, string marker)
    {
        var proposerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Lead");
        var managerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, $"{marker}Manager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, marker.ToUpperInvariant());
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, $"{marker.ToUpperInvariant()}_SYS", proposerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, proposerId, SubsystemRoleCodes.Lead);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, managerId, ProjectRoleCodes.ProjectManager);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, managerId, subsystemId, "U", marker);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var start = record.DatumZalozeni.Date;
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = start.AddDays(3 * poradi),
                SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                    ? (byte)SkutecnostRezimEnum.Manual
                    : (byte)SkutecnostRezimEnum.Auto,
                SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await dbContext.SaveChangesAsync();
        return new Seed(projectId, recordId, proposerId, managerId, start);
    }

    /// <summary>Plán všech 10 kroků jako v záznamu, jen krok <paramref name="bezPlanu"/> prázdný.</summary>
    private static List<SaveRecordHarmonogramValueCommand> PlansWithout(Seed seed, int bezPlanu)
        => Enumerable.Range(1, 10)
            .Select(poradi => new SaveRecordHarmonogramValueCommand
            {
                Poradi = poradi,
                PlanDatum = poradi == bezPlanu ? null : seed.Start.AddDays(3 * poradi)
            })
            .ToList();

    private static async Task<Dictionary<int, DateTime?>> PlansAsync(PmTrackerDbContext dbContext, int recordId)
        => await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToDictionaryAsync(x => (int)x.Poradi, x => x.PlanDatum);

    [Fact]
    public async Task UlozeniEditoru_PrazdnyPlan_SmazePlanKroku()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_plan_removal_editor");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var seed = await SeedAsync(dbContext, "PlanRemEdit");
        var admin = IntegrationTestHelper.BuildUser(seed.ManagerId, isSuperAdmin: true);

        var command = IntegrationTestHelper.BuildScheduleEditCommand(store.BuildZaznamEdit(seed.RecordId));
        command.HarmonogramHodnoty = PlansWithout(seed, bezPlanu: 3);
        store.SaveRecord(command, admin);

        var plans = await PlansAsync(dbContext, seed.RecordId);
        plans[3].Should().BeNull("✕ v editoru + uložení plán kroku 3 smaže (R13)");
        plans[4].Should().Be(seed.Start.AddDays(12));
    }

    [Fact]
    public async Task NavrhOdstraniPlanKroku_PoSchvaleniJePlanPrazdny()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_proposal_plan_removal");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var seed = await SeedAsync(dbContext, "PlanRemProp");
        var (projectId, recordId, managerId, start) = (seed.ProjectId, seed.RecordId, seed.ManagerId, seed.Start);

        var proposer = IntegrationTestHelper.BuildUser(seed.ProposerId, visibleProjectIds: [projectId]);
        var editor = store.BuildScheduleProposalEditor(projectId, recordId, proposer);
        var command = IntegrationTestHelper.BuildScheduleEditCommand(editor);
        command.HarmonogramHodnoty = PlansWithout(seed, bezPlanu: 3);
        store.SubmitScheduleProposal(command, proposer);

        var proposal = await dbContext.ZaznamNavrhy.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == recordId && x.TypNavrhu == RecordProposalTypeCodes.SchedulePlanChange);
        var payload = JsonSerializer.Deserialize<RecordProposalPayload>(proposal.PayloadJson)!.SchedulePlan!;
        payload.PlannedHarmonogramHodnoty.Should().ContainSingle(x => x.Poradi == 3)
            .Which.PlanDatum.Should().BeNull("odstranění plánu je v návrhu (R14)");

        var manager = IntegrationTestHelper.BuildUser(
            managerId,
            visibleProjectIds: [projectId],
            grants: [IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsAccept, projectId)]);
        store.ApproveProposal(new ProposalDecisionCommand { ProjektId = projectId, ProposalId = proposal.Id }, manager);

        var plans = await PlansAsync(dbContext, recordId);
        plans[3].Should().BeNull("schválení plán kroku 3 smazalo");
        plans[2].Should().Be(start.AddDays(6));
        plans[4].Should().Be(start.AddDays(12));
    }
}
```

Run: `dotnet test PmTracker.Tests.Integration --nologo --filter "FullyQualifiedName~SchedulePlanRemovalTests|FullyQualifiedName~ScheduleProposalRezimTests"`

Expected: PASS.
- `UlozeniEditoru_…` je pojistka dnešního chování serveru, projde hned.
- Mutační kontrola návrhu: dočasně vrátit v mapperu podmínku na `.Where(v => v.PlanDatum.HasValue)`. `NavrhOdstraniPlanKroku_…` pak selže na `ContainSingle(x => x.Poradi == 3)`. Pak změnu vrátit.

- [ ] **Step 7: Api test — přehled změn v detailu návrhu**

`PmTracker.Tests.Api/Controllers/ScheduleProposalPlanRemovalRenderTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R14: detail návrhu ukáže navržené odstranění plánu — v nápovědě
/// „Původní plán: X | Navržený: bez plánu“, pole plánu prázdné (tak, jak ho schválení zapíše).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleProposalPlanRemovalRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ScheduleProposalPlanRemovalRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DetailNavrhu_OdstraneniPlanu_UkazeBezPlanu_APrazdnePole()
    {
        var leadId = await _fixture.EnsurePersonAsync("APPLANRMLead");
        var projectId = await _fixture.EnsureProjectAsync("APPLANRM");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APPLANRM_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "APPLANRM");
        await _fixture.SetRecordFoundingDateAsync(recordId, DateTime.Today.AddDays(-40));
        var start = DateTime.Today.AddDays(-35);
        await _fixture.SeedDatumScheduleAsync(recordId, start, new HashSet<int>());
        var plan3 = start.AddDays(21);

        await using var db = _fixture.CreateDbContext();
        var proposal = new ZaznamNavrhEntity
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            SubsystemId = subsystemId,
            TypNavrhu = RecordProposalTypeCodes.SchedulePlanChange,
            Stav = RecordProposalStateCodes.Pending,
            PayloadJson = JsonSerializer.Serialize(new
            {
                ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
                SchedulePlan = new
                {
                    ProjektId = projectId,
                    ZaznamId = recordId,
                    TerminUkonceni = DateTime.Today.AddDays(30),
                    PlannedHarmonogramHodnoty = new[] { new { Poradi = 3, PlanDatum = (DateTime?)null } },
                    ActualHarmonogramHodnoty = Array.Empty<object>(),
                    ChangesSchedulePlan = true
                }
            }),
            CreatedByOsobaId = leadId,
            CreatedAt = DateTime.UtcNow
        };
        db.ZaznamNavrhy.Add(proposal);
        await db.SaveChangesAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposal.Id}&asUser={_fixture.AdminOsobaId}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var row = Regex.Match(html, "<tr data-schedule-step-row\\s+data-step-index=\"3\"[\\s\\S]*?</tr>").Value;
        row.Should().Contain($"Původní plán: {plan3:dd.MM.yyyy} | Navržený: bez plánu");
        Regex.Match(row, "<pm-date-field\\b[^>]*data-schedule-plan-step=\"3\"[^>]*>").Value
            .Should().Contain("iso-value=\"\"", "schválení plán smaže — detail ukazuje prázdné pole");
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~ScheduleProposalPlanRemovalRenderTests"`
Expected: FAIL — nápověda „bez plánu“ chybí (dnes `if (!planned.PlanDatum.HasValue) continue`).

- [ ] **Step 8: Přehled změn**

V `BuildScheduleProposalScheduleDiffTooltips`:

```csharp
        var result = new Dictionary<int, string>();
        var origPlanByPoradi = originalModel.HarmonogramBlok.Kroky.ToDictionary(s => s.KrokIndex, s => s.BaselineDatum);
        var origRawPlanByPoradi = originalModel.HarmonogramBlok.Kroky.ToDictionary(s => s.KrokIndex, s => s.PlanDatum);
        var origActualByPoradi = originalModel.HarmonogramBlok.Kroky.ToDictionary(s => s.KrokIndex, s => s.SkutecneDatum);

        foreach (var planned in payload.PlannedHarmonogramHodnoty)
        {
            if (!planned.PlanDatum.HasValue)
            {
                // Spec 2026-10-22 R14: navržené odstranění plánu (jen u kroku, který plán má).
                var origRaw = origRawPlanByPoradi.GetValueOrDefault(planned.Poradi);
                if (origRaw.HasValue)
                {
                    result[planned.Poradi] = $"Původní plán: {origRaw.Value:dd.MM.yyyy} | Navržený: bez plánu";
                }
                continue;
            }
```

Zbytek metody beze změny.

- [ ] **Step 9: Spustit — projde, a sousední sady**

Run:
- `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~ScheduleProposal"`
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~Proposal"`

Expected: vše PASS.

- [ ] **Step 10: Živá dokumentace**

Do `docs/specs/harmonogram-plan-vs-skutecnost.md` vložit před `## Hraniční případy`:

```markdown
## Smazání plánu kroku (spec 2026-10-22, R13 a R14)

- Pole plánu má **✕ (smazat datum)**, když ho uživatel smí upravit. Platí to v editoru záznamu
  i v návrhu změny harmonogramu (`_ScheduleTable.cshtml`: `Clearable = canEditDuration`).
  Zamčené pole ✕ nemá.
- Krok bez plánu má trvání **„–“**. Další krok začíná koncem posledního kroku s plánem.
  Text počítá server (`ScheduleDurationText`) a v editoru JS (`harmonogram/planDuration.js`).
- Návrh změny harmonogramu nese i **odstranění plánu**: krok, který plán v záznamu má a ve
  formuláři přišel prázdný (`RecordProposalPayloadMapper`, `currentPlanSteps`).
  - Detail návrhu ukáže „Původní plán: X | Navržený: bez plánu“.
  - Schválení zapíše `NULL`.
```

- [ ] **Step 11: Commit**

```bash
git add PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs PmTracker.Web/Services/RecordProposalService.SubmitCommands.cs PmTracker.Web/Services/RecordProposalService.Queries.cs PmTracker.Tests.Unit/Records/RecordProposalPayloadMapperScheduleTests.cs PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestHelper.cs PmTracker.Tests.Integration/DataStore/ScheduleProposalRezimTests.cs PmTracker.Tests.Integration/DataStore/SchedulePlanRemovalTests.cs PmTracker.Tests.Api/Controllers/ScheduleProposalPlanRemovalRenderTests.cs docs/specs/harmonogram-plan-vs-skutecnost.md
git commit -m "feat(navrhy): návrh změny harmonogramu nese odstranění plánu (spec 2026-10-22 R14)"
```

---

### Task 4: Indikátor rozbalení v levém okraji karty (R1)

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css` (`.record-content` ~řádek 3592, `.record-expand-indicator` ~řádek 3734)
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml:63` (jen komentář)
- Create: `PmTracker.Tests.Unit/Layout/RecordLayoutCssTests.cs`
- Create: `PmTracker.Tests.E2E/TestInfrastructure/E2EOwnProject.cs`
- Create: `PmTracker.Tests.E2E/Scenarios/RecordCardLayoutScenariosTests.cs`

**Interfaces:**
- Produces:
  - `E2EOwnProject.CreateAsync(string connectionString, int ownerOsobaId)`;
  - vlastnosti `ProjectId`, `FirstSubsystemId`, `SecondSubsystemId`;
  - `AddTaskAsync(int cisloZaznamu, int subsystemId, bool closed = false, bool withSchedule = false) : Task<int>`;
  - `AddOpenMeetingAsync(int cisloJednani, DateTime datum) : Task<int>`.
  - `RecordLayoutCssTests.Rule(string selector)` (pravidlo na začátku řádku). Task 5 a Task 10 do třídy přidávají testy.

- [ ] **Step 1: CSS test**

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>Spec 2026-10-22: pravidla v site.css pro kartu a stránku záznamu (A, B, C).</summary>
public sealed class RecordLayoutCssTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    /// <summary>Pravidlo, jehož selektor začíná na začátku řádku (ne jako součást složeného selektoru).</summary>
    private static string Rule(string selector)
    {
        var match = Regex.Match(Css, "(?m)^" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        match.Success.Should().BeTrue($"pravidlo {selector} existuje");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void ExpandIndicator_SitsInCardGutter_OutOfHeaderFlow()
    {
        var indicator = Rule(".record-expand-indicator");
        indicator.Should().Contain("position: absolute", "indikátor nesmí zabírat sloupec ve flexu hlavičky (R1)");
        indicator.Should().Contain("left: 0");
        indicator.Should().Contain("width: 16px", "vejde se do levého odsazení obsahu karty (16 px)");
        indicator.Should().Contain("height: 16px", "čtverec — po otočení o 90° nepřesáhne do textu");
        Rule(".record-content").Should().Contain("position: relative").And.Contain("padding: 14px 16px 16px");
    }
}
```

- [ ] **Step 2: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordLayoutCssTests"`
Expected: FAIL — `position: absolute` chybí.

- [ ] **Step 3: CSS**

`.record-content`:

```css
.record-content {
    flex: 1;
    padding: 14px 16px 16px;
    /* Spec 2026-10-22 R1: kotva indikátoru rozbalení v levém okraji. */
    position: relative;
}
```

`.record-expand-indicator` (pravidlo `.record-card.collapsed .record-expand-indicator` zůstává):

```css
/* Spec 2026-10-22 R1: indikátor rozbalení v levém okraji karty (mezi barevným pruhem a obsahem),
   mimo flex hlavičky — štítky, nadpis, detaily i vyjádření začínají na jedné svislé čáře.
   Dřív (R7 2026-07-13) zabíral ve flexu hlavičky vlastní sloupec → odskok hlavičky o 21 px. */
.record-expand-indicator {
    position: absolute;
    top: 18px;
    left: 0;
    width: 16px;
    height: 16px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 18px;
    line-height: 1;
    transform: rotate(90deg);
    transition: transform 0.2s ease;
}
```

`_ZaznamPartial.cshtml:63` — komentář (musí začínat `@* R7`, hlídá to `RecordCardScheduleToggleMarkupTests`):

```cshtml
            @* R7 (2026-07-13): indikátor rozbalení vlevo; spec 2026-10-22 R1: v levém okraji karty (CSS), mimo flex hlavičky. *@
```

- [ ] **Step 4: Spustit — projde**

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordLayoutCssTests|FullyQualifiedName~RecordScheduleViewCssTests|FullyQualifiedName~RecordCardScheduleToggleMarkupTests|FullyQualifiedName~AppCssColorTokenTests"`
Expected: PASS.

- [ ] **Step 5: E2E — vlastní projekt a měření odskoku**

`PmTracker.Tests.E2E/TestInfrastructure/E2EOwnProject.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.E2E.TestInfrastructure;

/// <summary>
/// Vlastní projekt pro E2E (spec 2026-10-22). Má unikátní kód, žádné záznamy jiných testů a žádné
/// uložené výchozí filtry, takže test zná přesnou sadu karet. Dva subsystémy jsou v pořadí projektu
/// proti abecedě: „… B“ má pořadí 1, „… A“ pořadí 2. Úklid není potřeba — E2E databáze vzniká pro
/// každý běh (stejně jako u MeetingOverviewYearGroupingScenariosTests).
/// </summary>
public sealed class E2EOwnProject
{
    private readonly string _connectionString;
    private readonly int _ownerOsobaId;

    private E2EOwnProject(string connectionString, int ownerOsobaId, int projectId, int firstSubsystemId, int secondSubsystemId)
    {
        _connectionString = connectionString;
        _ownerOsobaId = ownerOsobaId;
        ProjectId = projectId;
        FirstSubsystemId = firstSubsystemId;
        SecondSubsystemId = secondSubsystemId;
    }

    public int ProjectId { get; }

    /// <summary>Subsystém s pořadím 1 (název „… B“).</summary>
    public int FirstSubsystemId { get; }

    /// <summary>Subsystém s pořadím 2 (název „… A“).</summary>
    public int SecondSubsystemId { get; }

    private static PmTrackerDbContext CreateDbContext(string connectionString) => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>().UseSqlServer(connectionString).Options);

    public static async Task<E2EOwnProject> CreateAsync(string connectionString, int ownerOsobaId)
    {
        await using var db = CreateDbContext(connectionString);
        var code = $"E2E{Guid.NewGuid():N}"[..12];
        var project = new ProjektEntity
        {
            Zkratka = code,
            CelyNazev = $"E2E vlastni projekt {code}",
            StavId = await db.CiselnikStavuProjektu.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(),
            PouzivatIdentJednani = false
        };
        var first = new SubsystemEntity { Kod = $"{code}B", Nazev = $"{code} B" };
        var second = new SubsystemEntity { Kod = $"{code}A", Nazev = $"{code} A" };
        db.Projekty.Add(project);
        db.Subsystemy.AddRange(first, second);
        await db.SaveChangesAsync();
        db.ProjektSubsystemy.AddRange(
            new ProjektSubsystemEntity { ProjektId = project.Id, SubsystemId = first.Id, Poradi = 1, DatumPrirazeni = DateTime.Today.AddDays(-90) },
            new ProjektSubsystemEntity { ProjektId = project.Id, SubsystemId = second.Id, Poradi = 2, DatumPrirazeni = DateTime.Today.AddDays(-90) });
        await db.SaveChangesAsync();
        return new E2EOwnProject(connectionString, ownerOsobaId, project.Id, first.Id, second.Id);
    }

    /// <summary>Úkol založený před 30 dny. <paramref name="closed"/> = konečný stav, <paramref name="withSchedule"/> = plán 10 kroků po týdnu.</summary>
    public async Task<int> AddTaskAsync(int cisloZaznamu, int subsystemId, bool closed = false, bool withSchedule = false)
    {
        await using var db = CreateDbContext(_connectionString);
        var categoryId = await db.CiselnikKategoriiZaznamu.Where(x => x.Kod == "U").Select(x => x.Id).FirstAsync();
        var stateId = await db.CiselnikStavuUkolu.Where(x => x.IsFinal == closed).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var zalozeni = DateTime.Today.AddDays(-30);
        var record = new ProjektovyZaznamEntity
        {
            ProjektId = ProjectId,
            KategorieId = categoryId,
            StavUkoluId = stateId,
            CisloZaznamu = cisloZaznamu,
            Nazev = $"E2E zaznam {cisloZaznamu}",
            Cil = $"E2E cil {cisloZaznamu}",
            Popis = $"E2E popis {cisloZaznamu}",
            VlastnikId = _ownerOsobaId,
            DatumZalozeni = zalozeni,
            DatumUkonceni = DateTime.Today.AddDays(45),
            SubsystemId = subsystemId
        };
        db.ProjektoveZaznamy.Add(record);
        await db.SaveChangesAsync();

        if (withSchedule)
        {
            for (var poradi = 1; poradi <= 10; poradi++)
            {
                db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
                {
                    ZaznamId = record.Id,
                    Poradi = (byte)poradi,
                    PlanDatum = zalozeni.AddDays(7 * poradi),
                    SkutecnostRezim = HarmonogramManualSteps.IsManual(poradi)
                        ? (byte)SkutecnostRezimEnum.Manual
                        : (byte)SkutecnostRezimEnum.Auto,
                    SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        return record.Id;
    }

    /// <summary>Otevřené jednání; datum volit v minulosti (memory: jednání s dnešním datem odsouvá jiná).</summary>
    public async Task<int> AddOpenMeetingAsync(int cisloJednani, DateTime datum)
    {
        await using var db = CreateDbContext(_connectionString);
        var meeting = new JednaniEntity
        {
            ProjektId = ProjectId,
            CisloJednani = cisloJednani,
            DatumPlanovane = datum,
            CasZacatek = new TimeOnly(9, 0),
            Misto = "E2E vlastni projekt",
            StavJednaniId = await db.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync()
        };
        db.Jednani.Add(meeting);
        await db.SaveChangesAsync();
        return meeting.Id;
    }
}
```

`PmTracker.Tests.E2E/Scenarios/RecordCardLayoutScenariosTests.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22 A, B. Hlavička rozbalené karty začíná na stejné svislé čáře jako detaily
/// a vyjádření, protože indikátor rozbalení je v levém okraji (R1). Detaily v tabulce nahradí
/// řádek „Termín · Založeno“ v hlavičce; v pohledu Harmonogram ten řádek zůstane (R2, R3).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordCardLayoutScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordCardLayoutScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    /// <summary>Záložka Záznamy vlastního projektu s odkazem na záznam — karta se sama rozbalí.</summary>
    private async Task<ILocator> OpenExpandedCardAsync(IPage page, int projectId, int recordId)
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{projectId}?tab=zaznamy&recordId={recordId}&asUser={_fixture.AdminOsobaId}");
        var card = page.Locator($".record-card[data-record-id='{recordId}']");
        await Expect(card).ToHaveCountAsync(1);
        await Expect(card).Not.ToHaveClassAsync(new Regex(@"\bcollapsed\b"));
        return card;
    }

    private static Task<double> LeftAsync(ILocator locator)
        => locator.First.EvaluateAsync<double>("e => e.getBoundingClientRect().left");

    [Theory]
    [InlineData(1400)]
    [InlineData(375)]
    public async Task ExpandedCard_HeaderStartsOnSameLineAsBody(int width)
    {
        var project = await E2EOwnProject.CreateAsync(_fixture.Database.ConnectionString, _fixture.AdminOsobaId);
        var recordId = await project.AddTaskAsync(1, project.FirstSubsystemId);
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.SetViewportSizeAsync(width, 900);
            var card = await OpenExpandedCardAsync(page, project.ProjectId, recordId);

            var titleLeft = await LeftAsync(card.Locator(".record-title-wrap"));
            var bodyLeft = await LeftAsync(card.Locator("[data-record-comments-shell]"));
            Math.Abs(titleLeft - bodyLeft).Should().BeLessThanOrEqualTo(1,
                $"hlavička a tělo karty začínají na jedné svislé čáře (R1), šířka {width}");

            var indicator = await card.Locator(".record-expand-indicator").BoundingBoxAsync();
            indicator.Should().NotBeNull();
            (indicator!.X + indicator.Width).Should().BeLessThanOrEqualTo(titleLeft + 1,
                "indikátor je v levém okraji, ne přes text");
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }
}
```

Run: `dotnet test PmTracker.Tests.E2E --nologo --filter "FullyQualifiedName~RecordCardLayoutScenariosTests"`

Expected: PASS 2/2 (CSS už je hotové).

Mutační kontrola: dočasně v `.record-expand-indicator` vrátit `position: absolute` na `position: static`. Test pak selže na odskoku 20+ px. Pak změnu vrátit.

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml PmTracker.Tests.Unit/Layout/RecordLayoutCssTests.cs PmTracker.Tests.E2E/TestInfrastructure/E2EOwnProject.cs PmTracker.Tests.E2E/Scenarios/RecordCardLayoutScenariosTests.cs
git commit -m "feat(zaznamy): indikátor rozbalení v levém okraji karty, hlavička zarovnaná s tělem (spec 2026-10-22 R1)"
```

---

### Task 5: Detaily záznamu v tabulce (R2–R4)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs:105-118`
- Modify: `PmTracker.Web/Services/ProjectService.LazyQueries.cs` (`BuildRecordCardDetailAsync`, select + VM)
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamDetailPartial.cshtml:8-44`
- Modify: `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml:60-66`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (`.record-history` ~řádek 3884)
- Modify: `PmTracker.Tests.Api/Controllers/RecordDetailPageRenderTests.cs:39,105-108`
- Create: `PmTracker.Tests.Api/Controllers/RecordFactsTableRenderTests.cs`
- Modify: `PmTracker.Tests.Unit/Layout/RecordLayoutCssTests.cs` (+4 testy)
- Modify: `PmTracker.Tests.E2E/Scenarios/RecordCardLayoutScenariosTests.cs` (+1 test)

**Interfaces:**
- Consumes: `RecordLayoutCssTests.Rule`, `E2EOwnProject` (Task 4).
- Produces: `ZaznamCardDetailViewModel.DatumZalozeni : DateTime` (required); třída `.record-facts`.

- [ ] **Step 1: Api test tabulky**

`PmTracker.Tests.Api/Controllers/RecordFactsTableRenderTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R2–R4: detaily záznamu (karta i stránka záznamu) jsou v tabulce se dvěma
/// dvojicemi popisek | hodnota na řádek: Vlastník | Subsystém, Termín | Typ úkolu, Založeno.
/// Typ úkolu jen u záznamu s typem. Na stránce záznamu není samostatný řádek „Založeno“.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordFactsTableRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordFactsTableRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int RecordId)> SeedAsync(string marker, bool withTaskType)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker);
        await _fixture.SetRecordFoundingDateAsync(recordId, new DateTime(2026, 7, 8));
        if (withTaskType)
        {
            await using var db = _fixture.CreateDbContext();
            var record = await db.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
            record.AktualniTypUkoluId = await db.CiselnikTypuUkolu.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            await db.SaveChangesAsync();
        }
        return (projectId, recordId);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Facts(string html)
        => Regex.Match(html, "<table class=\"record-facts\">[\\s\\S]*?</table>").Value;

    private static IReadOnlyList<string> Labels(string row)
        => Regex.Matches(row, "<th scope=\"row\">([^<]+)</th>").Select(m => m.Groups[1].Value).ToList();

    [Fact]
    public async Task CardDetail_TwoPairsPerRow_WithFoundingDate()
    {
        var s = await SeedAsync("APFACTS1", withTaskType: true);

        var facts = Facts(await GetHtmlAsync(
            $"/Zaznamy/RecordDetailPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}"));

        facts.Should().NotBeEmpty("detaily jsou v tabulce (R2)");
        var rows = Regex.Matches(facts, "<tr>[\\s\\S]*?</tr>").Select(m => m.Value).ToList();
        rows.Should().HaveCount(3);
        Labels(rows[0]).Should().Equal("Vlastník", "Subsystém");
        Labels(rows[1]).Should().Equal("Termín", "Typ úkolu");
        Labels(rows[2]).Should().Equal("Založeno");
        rows[2].Should().Contain("08.07.2026");
    }

    [Fact]
    public async Task CardDetail_WithoutTaskType_HasNoTaskTypeCells()
    {
        var s = await SeedAsync("APFACTS2", withTaskType: false);

        var facts = Facts(await GetHtmlAsync(
            $"/Zaznamy/RecordDetailPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}"));

        facts.Should().NotBeEmpty();
        facts.Should().NotContain("Typ úkolu", "prázdné buňky se nevykreslí (spec 4.B)");
    }

    [Fact]
    public async Task RecordPage_FoundingDateOnlyInTable()
    {
        var s = await SeedAsync("APFACTS3", withTaskType: false);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        var header = Regex.Match(html, "<section class=\"card record-page-header\">[\\s\\S]*?</section>").Value;
        header.Should().Contain("<table class=\"record-facts\">");
        header.Should().NotContain("record-summary-meta", "samostatný řádek Založeno se přesunul do tabulky (R3)");
        Regex.Matches(header, "Založeno").Count.Should().Be(1);
    }
}
```

- [ ] **Step 2: Spustit — selže**

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~RecordFactsTableRenderTests"`
Expected: 3 FAIL (tabulka neexistuje).

- [ ] **Step 3: View model a dotaz**

`ZaznamCardDetailViewModel` (za `HistorieTypuUkolu`):

```csharp
    /// <summary>Spec 2026-10-22 R2: datum založení je v tabulce detailů (karta i stránka záznamu).</summary>
    public required DateTime DatumZalozeni { get; init; }
```

`BuildRecordCardDetailAsync`: do anonymního selectu přidat `x.DatumZalozeni,`. Do inicializace VM přidat `DatumZalozeni = record.DatumZalozeni,`.

- [ ] **Step 4: Partial `_ZaznamDetailPartial.cshtml`**

Blok `<div class="record-history">…</div>` (řádky 8–44) nahradit:

```cshtml
@{
    var hasTaskType = !string.IsNullOrWhiteSpace(Model.TypUkolu);
}
@* Spec 2026-10-22 R2–R4: detaily ve dvou dvojicích popisek | hodnota na řádek. Historie
   (přeškrtnuté staré hodnoty) zůstává v buňce hodnoty; bez typu úkolu se jeho buňky nevykreslí. *@
<table class="record-facts">
    <tbody>
        <tr>
            <th scope="row">Vlastník</th>
            <td>
                @foreach (var jmeno in Model.HistorieVlastniku)
                {
                    <span class="history"><s>@jmeno</s></span>
                }
                <strong>@Model.AktualniVlastnik</strong>
            </td>
            <th scope="row">Subsystém</th>
            <td>
                @foreach (var subsystem in Model.HistorieSubsystemu)
                {
                    <span class="history"><s>@subsystem</s></span>
                }
                <strong>@Model.AktualniSubsystem</strong>
            </td>
        </tr>
        <tr>
            <th scope="row">Termín</th>
            <td>
                @foreach (var termin in Model.HistorieTerminu)
                {
                    <span class="history"><s>@termin.ToString("dd.MM.yyyy")</s></span>
                }
                <strong>@(Model.AktualniTermin?.ToString("dd.MM.yyyy") ?? "-")</strong>
            </td>
            @if (hasTaskType)
            {
                <th scope="row">Typ úkolu</th>
                <td>
                    @foreach (var typ in Model.HistorieTypuUkolu)
                    {
                        <span class="history"><s>@typ</s></span>
                    }
                    <strong>@Model.TypUkolu</strong>
                </td>
            }
        </tr>
        <tr>
            <th scope="row">Založeno</th>
            <td><strong>@Model.DatumZalozeni.ToString("dd.MM.yyyy")</strong></td>
        </tr>
    </tbody>
</table>
```

Blok `@{ var safeDescriptionHtml … }` na začátku souboru zůstává. Nový `@{ var hasTaskType … }` přijde za něj.

- [ ] **Step 5: Stránka záznamu — bez samostatného „Založeno“**

V `ZaznamDetailPage.cshtml` nahradit komentář a `<div class="record-summary-meta">…</div>` (řádky 60–66):

```cshtml
    @* Detaily (vlastník, subsystém, termín, typ úkolu, založeno — s historií), popis, chips externích
       odkazů a spolupráce dodává _ZaznamDetailPartial (spec 2026-10-22 R2–R4). *@
    @await Html.PartialAsync("~/Views/Projekty/_ZaznamDetailPartial.cshtml", Model.Detail)
```

- [ ] **Step 6: Upravit `RecordDetailPageRenderTests`**

- Řádek 39: `html.Should().Contain("record-facts", "hlavička nese detaily s historií vlastníka/termínu/subsystému");`
- Řádky 105–108: komentář „Bohatá hlavička: kategorie, název, detaily v tabulce.“, `html.Should().Contain("record-facts");`. Řádek s `record-summary-meta` smazat — opak hlídá nový test.

- [ ] **Step 7: Spustit Api — projde**

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~RecordFactsTableRenderTests|FullyQualifiedName~RecordDetailPageRenderTests|FullyQualifiedName~RecordScheduleTogglePartialTests|FullyQualifiedName~RecordEditorControllerTests"`
Expected: PASS.

- [ ] **Step 8: CSS testy (do `RecordLayoutCssTests`)**

```csharp
    [Fact]
    public void FactsTable_HasNoBorders_AndUsesTokens()
    {
        var facts = Rule(".record-facts");
        facts.Should().Contain("border-collapse: collapse");
        facts.Should().Contain("color: var(--text-secondary)");
        Rule(".record-facts td strong").Should().Contain("color: var(--text-primary)");
    }

    [Fact]
    public void FactsTable_NarrowScreen_OnePairPerRow_WithoutModernLayout()
    {
        Regex.IsMatch(Css, @"@media \(max-width: 640px\) \{[\s\S]*?\.record-facts tr \{[^}]*display: flex;[^}]*flex-wrap: wrap;")
            .Should().BeTrue("na úzkém okně jedna dvojice na řádek (spec 4.B)");
        foreach (Match block in Regex.Matches(Css, @"[^{}]*\.record-facts[^{}]*\{[^}]*\}"))
        {
            block.Value.Should().NotContain("max-content").And.NotContain(":has(", "i15 — jen letité vlastnosti");
        }
    }

    [Fact]
    public void SummaryLine_HiddenOnExpandedCard_ExceptScheduleView()
        => Regex.IsMatch(Css,
                @"\.record-card:not\(\.collapsed\):not\(\.record-card--schedule-view\) \.record-header \.record-summary-meta \{\s*display: none;")
            .Should().BeTrue("R3: v tabulce už je; v pohledu Harmonogram je tabulka skrytá, řádek zůstává");

    [Fact]
    public void OldHistoryLayout_Removed()
        => Css.Should().NotContain(".record-history", "markup .record-history nahradila tabulka .record-facts");
```

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordLayoutCssTests"`
Expected: 4 nové FAIL.

- [ ] **Step 9: CSS**

Smazat pravidla `.record-history { … }` a `.record-history strong { … }` a na jejich místo vložit:

```css
/* Spec 2026-10-22 R2–R4: detaily záznamu v tabulce — dvě dvojice popisek | hodnota na řádek.
   Skutečná <table> drží sloupce zarovnané i v Edge na i15; bez rámečků a pozadí. Historie
   (přeškrtnuté staré hodnoty) zůstává v buňce hodnoty. */
.record-facts {
    border-collapse: collapse;
    margin: 12px 0 8px;
    font-size: 13px;
    color: var(--text-secondary);
}

.record-facts th,
.record-facts td {
    padding: 3px 0;
    border: 0;
    background: transparent;
    text-align: left;
    vertical-align: top;
}

.record-facts th {
    padding-right: 10px;
    font-weight: 600;
    white-space: nowrap;
}

.record-facts td {
    padding-right: 32px;
    overflow-wrap: anywhere;
}

.record-facts td strong {
    color: var(--text-primary);
}

.record-facts .history {
    margin-right: 6px;
}

/* Úzké okno: jedna dvojice na řádek (letitý flex, žádné :has / max-content — i15). */
@media (max-width: 640px) {
    .record-facts,
    .record-facts tbody {
        display: block;
    }

    .record-facts tr {
        display: flex;
        flex-wrap: wrap;
    }

    .record-facts th {
        flex: 0 0 6.5rem;
    }

    .record-facts td {
        flex: 1 1 calc(100% - 6.5rem);
        padding-right: 0;
    }
}
```

Za pravidlo `.record-summary-meta { … }` přidat:

```css
/* Spec 2026-10-22 R3: na rozbalené kartě je Termín i Založeno v tabulce detailů — řádek v hlavičce
   by je opakoval. Sbalená karta a pohled Harmonogram (detail je skrytý) ho ukazují dál. */
.record-card:not(.collapsed):not(.record-card--schedule-view) .record-header .record-summary-meta {
    display: none;
}
```

- [ ] **Step 10: E2E — tabulka místo meta řádku, pohled Harmonogram ho vrátí**

Do `RecordCardLayoutScenariosTests` přidat:

```csharp
    // gov-form-switch: input je pro Playwright „hidden" → DispatchEvent „click" po hydrataci
    // (vzor RecordScheduleToggleScenariosTests.ToggleViewAsync).
    private static async Task ToggleScheduleViewAsync(IPage page, int recordId)
    {
        var host = page.Locator($".record-card[data-record-id='{recordId}'] gov-form-switch[data-record-view-switch]");
        await Expect(host).ToHaveClassAsync(new Regex(@"\bhydrated\b"));
        var input = host.Locator("input").First;
        await input.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
        await input.DispatchEventAsync("click");
    }

    [Fact]
    public async Task ExpandedCard_FactsTableReplacesSummaryLine_ScheduleViewKeepsIt()
    {
        var project = await E2EOwnProject.CreateAsync(_fixture.Database.ConnectionString, _fixture.AdminOsobaId);
        var recordId = await project.AddTaskAsync(1, project.FirstSubsystemId, withSchedule: true);
        var page = await _fixture.NewPageAsync();
        try
        {
            var card = await OpenExpandedCardAsync(page, project.ProjectId, recordId);
            var summary = card.Locator(".record-header .record-summary-meta");
            var facts = card.Locator("[data-record-detail-shell] .record-facts");

            await Expect(facts).ToHaveCountAsync(1);
            await Expect(summary).ToBeHiddenAsync();
            Math.Abs(await LeftAsync(facts) - await LeftAsync(card.Locator(".record-title-wrap")))
                .Should().BeLessThanOrEqualTo(1, "tabulka začíná na stejné svislé čáře jako hlavička");

            await ToggleScheduleViewAsync(page, recordId);
            await Expect(card).ToHaveClassAsync(new Regex(@"record-card--schedule-view"));
            await Expect(summary).ToBeVisibleAsync();
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }
```

- [ ] **Step 11: Spustit — vše projde**

Run:
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordLayoutCssTests|FullyQualifiedName~AppCssColorTokenTests"`
- `dotnet test PmTracker.Tests.E2E --nologo --filter "FullyQualifiedName~RecordCardLayoutScenariosTests"`

Expected: Unit PASS, E2E 3/3.

- [ ] **Step 12: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs PmTracker.Web/Services/ProjectService.LazyQueries.cs PmTracker.Web/Views/Projekty/_ZaznamDetailPartial.cshtml PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Api/Controllers/RecordFactsTableRenderTests.cs PmTracker.Tests.Api/Controllers/RecordDetailPageRenderTests.cs PmTracker.Tests.Unit/Layout/RecordLayoutCssTests.cs PmTracker.Tests.E2E/Scenarios/RecordCardLayoutScenariosTests.cs
git commit -m "feat(zaznamy): detaily záznamu v tabulce se dvěma dvojicemi na řádek, bez duplicit (spec 2026-10-22 R2–R4)"
```

---

### Task 6: Pořadí zápisu jako jeden zdroj + charakterizace tisku (R5, R7)

**Files:**
- Create: `PmTracker.Tests.Integration/TestInfrastructure/MeetingSequenceSeed.cs`
- Modify: `PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs` (+1 test)
- Create: `PmTracker.Web/Services/Common/MinutesRecordOrdering.cs`
- Modify: `PmTracker.Web/Services/Export/ExportTemplateUseCase.cs:76-100`
- Test: `PmTracker.Tests.Unit/Common/MinutesRecordOrderingTests.cs`

**Interfaces:**
- Produces:
  - `MinutesOrderKey(string Subsystem, string SubsystemKod, int SubsystemPoradi, bool SubsystemHasProjectOrder, string Kategorie, int CisloViditelneA, int CisloViditelneB, int CisloZaznamu)`;
  - `MinutesSubsystemGroup<T>(string Subsystem, IReadOnlyList<T> Records)`;
  - `MinutesRecordOrdering.GroupBySubsystem<T>(IEnumerable<T>, Func<T, MinutesOrderKey>, bool useProjectOrder) : IReadOnlyList<MinutesSubsystemGroup<T>>`;
  - `MinutesRecordOrdering.InMinutesOrder<T>(IEnumerable<T>, Func<T, MinutesOrderKey>) : IReadOnlyList<T>`;
  - `MeetingSequenceSeed.CreateAsync(PmTrackerDbContext)` s vlastnostmi `ExpectedMeeting888`, `ExpectedMeeting887`, `ExpectedActive`.

- [ ] **Step 1: Sdílená data (Integration)**

`PmTracker.Tests.Integration/TestInfrastructure/MeetingSequenceSeed.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.TestInfrastructure;

/// <summary>
/// Spec 2026-10-22 R5–R8 — sdílená data pro tisk zápisu i službu sekvence.
///
/// Projekt má dva subsystémy v pořadí projektu proti abecedě (SEQZZ = 1, SEQAA = 2) a jednání
/// 887 (1. 10. 2026) a 888 (8. 10. 2026). Záznamy, číslo záznamu = pořadí založení:
///   r1 Úkol ZZ, běží, založen 1. 9.
///   r2 Informace ZZ, běží, založen 1. 9.
///   r3 Úkol AA, běží, založen 1. 9.
///   r4 Úkol ZZ, běží, založen 5. 10.
///   r5 Úkol ZZ, běží, založen 20. 10. (po 888)
///   r6 Úkol ZZ, uzavřen 20. 9. (před 887)
///   r7 Úkol AA, uzavřen 5. 10. (mezi 887 a 888)
///   r8 Úkol ZZ, uzavřen 12. 10. (po 888)
/// </summary>
public sealed record MeetingSequenceSeed(
    int AdminId, int ProjectId, int Meeting887, int Meeting888,
    int R1, int R2, int R3, int R4, int R5, int R6, int R7, int R8)
{
    /// <summary>888: ZZ (Informace r2; úkoly r1, r4, r8), pak AA (r3, r7). Mimo jsou r5 (založen po jednání) a r6 (uzavřen před minulým).</summary>
    public IReadOnlyList<int> ExpectedMeeting888 => [R2, R1, R4, R8, R3, R7];

    /// <summary>887 je první jednání (bez minulého). Navíc mimo je r4 (založen až po něm); r7 a r8 k 1. 10. běžely.</summary>
    public IReadOnlyList<int> ExpectedMeeting887 => [R2, R1, R8, R3, R7];

    /// <summary>Aktivní dnes (stav není konečný), bez ohledu na datum založení.</summary>
    public IReadOnlyList<int> ExpectedActive => [R2, R1, R4, R5, R3];

    public static async Task<MeetingSequenceSeed> CreateAsync(PmTrackerDbContext db)
    {
        var adminId = await IntegrationTestHelper.EnsurePersonAsync(db, "SeqAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(db, "SeqOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(db, "SEQPRJ");
        var zz = await IntegrationTestHelper.EnsureSubsystemAsync(db, "SEQZZ", adminId);
        var aa = await IntegrationTestHelper.EnsureSubsystemAsync(db, "SEQAA", adminId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(db, projectId, zz);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(db, projectId, aa);

        var running = await db.CiselnikStavuUkolu.Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var done = await db.CiselnikStavuUkolu.Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();

        var m887 = await MeetingAsync(db, projectId, 887, new DateTime(2026, 10, 1));
        var m888 = await MeetingAsync(db, projectId, 888, new DateTime(2026, 10, 8));

        var r1 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR1", new DateTime(2026, 9, 1));
        var r2 = await RecordAsync(db, projectId, ownerId, zz, "INFO", "SeqR2", new DateTime(2026, 9, 1));
        var r3 = await RecordAsync(db, projectId, ownerId, aa, "U", "SeqR3", new DateTime(2026, 9, 1));
        var r4 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR4", new DateTime(2026, 10, 5));
        var r5 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR5", new DateTime(2026, 10, 20));
        var r6 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR6", new DateTime(2026, 9, 1));
        var r7 = await RecordAsync(db, projectId, ownerId, aa, "U", "SeqR7", new DateTime(2026, 9, 1));
        var r8 = await RecordAsync(db, projectId, ownerId, zz, "U", "SeqR8", new DateTime(2026, 9, 1));
        await CloseAsync(db, r6, running, done, new DateTime(2026, 9, 20));
        await CloseAsync(db, r7, running, done, new DateTime(2026, 10, 5));
        await CloseAsync(db, r8, running, done, new DateTime(2026, 10, 12));

        return new MeetingSequenceSeed(adminId, projectId, m887, m888, r1, r2, r3, r4, r5, r6, r7, r8);
    }

    private static async Task<int> MeetingAsync(PmTrackerDbContext db, int projectId, int number, DateTime date)
    {
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(db, projectId, "OPEN", meetingNumber: number);
        var meeting = await db.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = date;
        await db.SaveChangesAsync();
        return meetingId;
    }

    private static async Task<int> RecordAsync(
        PmTrackerDbContext db, int projectId, int ownerId, int subsystemId, string category, string marker, DateTime zalozeni)
    {
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(db, projectId, ownerId, subsystemId, category, marker);
        var record = await db.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.DatumZalozeni = zalozeni;
        await db.SaveChangesAsync();
        return recordId;
    }

    private static async Task CloseAsync(PmTrackerDbContext db, int recordId, int fromStateId, int toStateId, DateTime platiOd)
    {
        var record = await db.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
        record.StavUkoluId = toStateId;
        db.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = fromStateId,
            NovyStav = toStateId,
            DatumZmeny = platiOd,
            PlatiOd = platiOd
        });
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Charakterizační test tisku (projde už teď)**

Do `ExportTemplateUseCaseTests` přidat:

```csharp
    /// <summary>
    /// Spec 2026-10-22 R5 — pojistka před přechodem tisku na sdílenou službu. Zápis jednání obsahuje
    /// přesně záznamy jednání v pořadí zápisu: subsystém v pořadí projektu → kategorie → číslo.
    /// </summary>
    [Fact]
    public async Task BuildMeetingTemplate_ListsMeetingRecordsInMinutesOrder()
    {
        var db = await _fixture.CreateDatabaseAsync("export_meeting_members_order");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(
            dbContext, new FixedTimeProvider(new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero)));
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(seed.AdminId, isSuperAdmin: true, visibleProjectIds: new[] { seed.ProjectId });

        var print888 = await useCase.BuildMeetingTemplateAsync(seed.Meeting888, currentUser, autoPrint: false);
        var print887 = await useCase.BuildMeetingTemplateAsync(seed.Meeting887, currentUser, autoPrint: false);

        print888.SubsystemGroups.Select(g => g.Subsystem).Should().Equal("SEQZZ Subsystem", "SEQAA Subsystem");
        print888.SubsystemGroups.SelectMany(g => g.Records).Select(r => r.ZaznamId).Should().Equal(seed.ExpectedMeeting888);
        print887.SubsystemGroups.SelectMany(g => g.Records).Select(r => r.ZaznamId).Should().Equal(seed.ExpectedMeeting887);
    }
```

Run: `dotnet test PmTracker.Tests.Integration --nologo --filter "FullyQualifiedName~ExportTemplateUseCaseTests"`
Expected: PASS (charakterizace dnešního chování).

Mutační kontrola, že test umí selhat: dočasně vložit `return true;` jako první řádek `ExportRecordVisibilityEvaluator.IsVisibleForMeetingPrint`. Nový test pak FAIL (objeví se r5, r6). Vrátit: `git checkout -- PmTracker.Web/Services/Export/ExportProjectionBuilders.cs`.

- [ ] **Step 3: Unit test pořadí**

`PmTracker.Tests.Unit/Common/MinutesRecordOrderingTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Common;
using Xunit;

namespace PmTracker.Tests.Unit.Common;

/// <summary>Spec 2026-10-22 R5, R7: pořadí zápisu — jediný zdroj pro tisk i procházení šipkami.</summary>
public sealed class MinutesRecordOrderingTests
{
    private sealed record Row(int Id, string Subsystem, int Poradi, bool HasOrder, string Kategorie, int A, int B = 0)
    {
        public MinutesOrderKey Key => new(Subsystem, Subsystem, Poradi, HasOrder, Kategorie, A, B, Id);
    }

    private static readonly Row[] Subsystems =
    [
        new(1, "Alfa", 0, false, "Úkol", 1),
        new(2, "Zeta", 1, true, "Úkol", 2),
        new(3, "Beta", 2, true, "Úkol", 3),
        new(4, "Gama", 0, false, "Úkol", 4)
    ];

    [Fact]
    public void Groups_ProjectOrderFirst_ThenUnorderedAlphabetically()
        => MinutesRecordOrdering.GroupBySubsystem(Subsystems, r => r.Key, useProjectOrder: true)
            .Select(g => g.Subsystem).Should().Equal("Zeta", "Beta", "Alfa", "Gama");

    [Fact]
    public void WithoutProjectOrder_GroupsAlphabetically()
        => MinutesRecordOrdering.GroupBySubsystem(Subsystems, r => r.Key, useProjectOrder: false)
            .Select(g => g.Subsystem).Should().Equal("Alfa", "Beta", "Gama", "Zeta");

    [Fact]
    public void WithinGroup_CategoryThenVisibleNumber()
    {
        var rows = new[]
        {
            new Row(1, "S", 1, true, "Úkol", 5),
            new Row(2, "S", 1, true, "Informace", 9),
            new Row(3, "S", 1, true, "Rozhodnutí", 7),
            new Row(4, "S", 1, true, "Úkol", 5, B: 1),
            new Row(5, "S", 1, true, "Úkol", 2)
        };

        MinutesRecordOrdering.InMinutesOrder(rows, r => r.Key).Select(r => r.Id)
            .Should().Equal(2, 3, 5, 1, 4);
    }
}
```

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~MinutesRecordOrderingTests"`
Expected: build FAIL (typ neexistuje).

- [ ] **Step 4: `MinutesRecordOrdering`**

`PmTracker.Web/Services/Common/MinutesRecordOrdering.cs`:

```csharp
namespace PmTracker.Web.Services.Common;

/// <summary>Klíč řazení záznamu v zápisu jednání (spec 2026-10-22 R5).</summary>
public readonly record struct MinutesOrderKey(
    string Subsystem,
    string SubsystemKod,
    int SubsystemPoradi,
    bool SubsystemHasProjectOrder,
    string Kategorie,
    int CisloViditelneA,
    int CisloViditelneB,
    int CisloZaznamu);

public sealed record MinutesSubsystemGroup<T>(string Subsystem, IReadOnlyList<T> Records);

/// <summary>
/// Pořadí záznamů zápisu (spec 2026-10-22 R5, R7):
/// - skupiny subsystémů: s pořadím v projektu napřed podle pořadí, ostatní za nimi abecedně;
/// - uvnitř skupiny: kategorie (Informace → Rozhodnutí → Úkol → ostatní) → název kategorie →
///   viditelné číslo A → B → číslo záznamu.
/// Jediný zdroj pro tisk (ExportTemplateUseCase) i procházení šipkami (RecordSequenceService).
/// </summary>
public static class MinutesRecordOrdering
{
    public static IReadOnlyList<MinutesSubsystemGroup<T>> GroupBySubsystem<T>(
        IEnumerable<T> records, Func<T, MinutesOrderKey> key, bool useProjectOrder)
        => records
            .Select(record => (Record: record, Key: key(record)))
            .GroupBy(x => new
            {
                x.Key.SubsystemKod,
                Subsystem = string.IsNullOrWhiteSpace(x.Key.Subsystem) ? "-" : x.Key.Subsystem,
                x.Key.SubsystemPoradi,
                x.Key.SubsystemHasProjectOrder
            })
            .OrderBy(group => useProjectOrder ? (group.Key.SubsystemHasProjectOrder ? 0 : 1) : 0)
            .ThenBy(group => useProjectOrder && group.Key.SubsystemHasProjectOrder ? group.Key.SubsystemPoradi : int.MaxValue)
            .ThenBy(group => group.Key.Subsystem, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new MinutesSubsystemGroup<T>(
                group.Key.Subsystem,
                group
                    .OrderBy(x => RecordDisplayOrdering.CategoryOrder(x.Key.Kategorie))
                    .ThenBy(x => x.Key.Kategorie, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(x => x.Key.CisloViditelneA)
                    .ThenBy(x => x.Key.CisloViditelneB)
                    .ThenBy(x => x.Key.CisloZaznamu)
                    .Select(x => x.Record)
                    .ToList()))
            .ToList();

    /// <summary>Záznamy v pořadí zápisu jako jeden seznam (procházení šipkami).</summary>
    public static IReadOnlyList<T> InMinutesOrder<T>(IEnumerable<T> records, Func<T, MinutesOrderKey> key)
        => GroupBySubsystem(records, key, useProjectOrder: true).SelectMany(group => group.Records).ToList();
}
```

- [ ] **Step 5: Tisk přes sdílené pořadí**

V `ExportTemplateUseCase.cs` nahradit `BuildSubsystemGroups`:

```csharp
    private static IReadOnlyList<PdfExportSubsystemGroupViewModel> BuildSubsystemGroups(IReadOnlyList<PdfExportRecordViewModel> records, bool useProjectOrder)
        => MinutesRecordOrdering.GroupBySubsystem(records, MinutesKey, useProjectOrder)
            .Select(group => new PdfExportSubsystemGroupViewModel
            {
                Subsystem = group.Subsystem,
                Records = group.Records
            })
            .ToList();

    private static MinutesOrderKey MinutesKey(PdfExportRecordViewModel record)
        => new(record.Subsystem, record.SubsystemKod, record.SubsystemPoradi, record.SubsystemHasProjectOrder,
            record.Kategorie, record.CisloViditelneA, record.CisloViditelneB, record.CisloZaznamu);
```

- [ ] **Step 6: Spustit — projde**

Run:
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~MinutesRecordOrderingTests|FullyQualifiedName~RecordDisplayOrderingTests|FullyQualifiedName~Export"`
- `dotnet test PmTracker.Tests.Integration --nologo --filter "FullyQualifiedName~Export"`

Expected: vše PASS, včetně charakterizace a `BuildProjectTemplate_OrdersRecordsWithinSubsystem_ByCategoryThenNumber`.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Tests.Integration/TestInfrastructure/MeetingSequenceSeed.cs PmTracker.Tests.Integration/Export/ExportTemplateUseCaseTests.cs PmTracker.Web/Services/Common/MinutesRecordOrdering.cs PmTracker.Web/Services/Export/ExportTemplateUseCase.cs PmTracker.Tests.Unit/Common/MinutesRecordOrderingTests.cs
git commit -m "refactor(export): pořadí zápisu jako jeden zdroj + charakterizace výběru záznamů tisku (spec 2026-10-22 R5)"
```

---

### Task 7: Služba sekvence záznamů; tisk zápisu na ni přejde (R5, R8, R11)

**Files:**
- Create: `PmTracker.Web/Services/Records/RecordSequence.cs`
- Create: `PmTracker.Web/Services/Records/RecordSequenceService.cs`
- Modify: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:206`
- Modify: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` (ctor `ExportRecordProjectionBuilder` + blok ~řádky 793–822)
- Modify: `PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestHelper.cs` (+ `CreateRecordSequenceService`)
- Test: `PmTracker.Tests.Unit/Records/RecordSequenceRequestTests.cs`, `PmTracker.Tests.Unit/Records/RecordSequencePositionTests.cs`
- Test: `PmTracker.Tests.Integration/DataStore/RecordSequenceServiceTests.cs`

**Interfaces:**
- Consumes: `MinutesRecordOrdering`, `MinutesOrderKey`, `MeetingSequenceSeed` (Task 6).
- Produces:
  - `RecordSequenceEntry(int ZaznamId, string CisloViditelne, string Nazev)` s `Label` = `"#{CisloViditelne} - {Nazev}"`.
  - `RecordSequenceRequest`: `JednaniId : int?`, `Active`, `ForMeeting(int)`, `Parse(string?) : RecordSequenceRequest?`, `ToQueryValue() : string`.
  - `RecordSequencePosition(int Index, int Count, RecordSequenceEntry? Previous, RecordSequenceEntry? Next)` + `Locate(IReadOnlyList<RecordSequenceEntry>, int zaznamId) : RecordSequencePosition?`.
  - `IRecordSequenceService.GetMeetingSequenceAsync(int jednaniId, CancellationToken)`, `GetActiveSequenceAsync(int projektId, CancellationToken)`, `ResolveAsync(RecordSequenceRequest, int projektId, CancellationToken) : Task<IReadOnlyList<RecordSequenceEntry>?>`.
  - `IntegrationTestHelper.CreateRecordSequenceService(PmTrackerDbContext)`.

- [ ] **Step 1: Unit testy parametru a polohy**

`PmTracker.Tests.Unit/Records/RecordSequenceRequestTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-22 R11: parametr adresy „seznam“ — aktivni / jednani-{id}.</summary>
public sealed class RecordSequenceRequestTests
{
    [Theory]
    [InlineData("aktivni")]
    [InlineData(" Aktivni ")]
    public void Parse_Active(string value)
    {
        var request = RecordSequenceRequest.Parse(value);
        request.Should().NotBeNull();
        request!.JednaniId.Should().BeNull();
        request.ToQueryValue().Should().Be("aktivni");
    }

    [Fact]
    public void Parse_Meeting_RoundTrips()
    {
        var request = RecordSequenceRequest.Parse("jednani-906");
        request!.JednaniId.Should().Be(906);
        request.ToQueryValue().Should().Be("jednani-906");
        RecordSequenceRequest.ForMeeting(906).Should().Be(request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jednani-")]
    [InlineData("jednani-0")]
    [InlineData("jednani--5")]
    [InlineData("jednani-12x")]
    [InlineData("vse")]
    public void Parse_Invalid_IsNull(string? value)
        => RecordSequenceRequest.Parse(value).Should().BeNull("neplatný parametr = stránka bez šipek");
}
```

`PmTracker.Tests.Unit/Records/RecordSequencePositionTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-22 R10, R12: poloha záznamu v seznamu, kraje bez přetáčení.</summary>
public sealed class RecordSequencePositionTests
{
    private static readonly RecordSequenceEntry A = new(10, "901-1", "Alfa");
    private static readonly RecordSequenceEntry B = new(20, "901-2", "Beta");
    private static readonly RecordSequenceEntry C = new(30, "7", "Gama");
    private static readonly IReadOnlyList<RecordSequenceEntry> Abc = [A, B, C];

    [Fact]
    public void Middle_HasBothNeighbours()
        => RecordSequencePosition.Locate(Abc, 20).Should().Be(new RecordSequencePosition(1, 3, A, C));

    [Fact]
    public void First_HasNoPrevious_LastHasNoNext()
    {
        RecordSequencePosition.Locate(Abc, 10)!.Previous.Should().BeNull();
        RecordSequencePosition.Locate(Abc, 30)!.Next.Should().BeNull();
    }

    [Fact]
    public void Single_HasNoNeighbours()
        => RecordSequencePosition.Locate([B], 20).Should().Be(new RecordSequencePosition(0, 1, null, null));

    [Fact]
    public void Missing_IsNull()
        => RecordSequencePosition.Locate(Abc, 99).Should().BeNull("záznam mimo seznam nemá šipky (R12)");

    [Fact]
    public void Label_LikeCardTitle()
        => A.Label.Should().Be("#901-1 - Alfa");
}
```

Run: `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordSequence"`
Expected: build FAIL.

- [ ] **Step 2: `RecordSequence.cs`**

```csharp
using System.Globalization;

namespace PmTracker.Web.Services.Records;

/// <summary>Záznam v seznamu pro procházení šipkami (spec 2026-10-22 R7, R8).</summary>
public sealed record RecordSequenceEntry(int ZaznamId, string CisloViditelne, string Nazev)
{
    /// <summary>Stejný tvar jako nadpis karty — do nápovědy tlačítek Předchozí / Další.</summary>
    public string Label => $"#{CisloViditelne} - {Nazev}";
}

/// <summary>
/// Spec 2026-10-22 R11: seznam pro procházení z parametru adresy „seznam“ — <c>aktivni</c>
/// (aktivní záznamy projektu) nebo <c>jednani-{id}</c> (záznamy jednání).
/// </summary>
public sealed record RecordSequenceRequest
{
    private const string ActiveValue = "aktivni";
    private const string MeetingPrefix = "jednani-";

    private RecordSequenceRequest(int? jednaniId) => JednaniId = jednaniId;

    /// <summary>Jednání, jehož záznamy se procházejí; null = aktivní záznamy projektu.</summary>
    public int? JednaniId { get; }

    public static RecordSequenceRequest Active { get; } = new((int?)null);

    public static RecordSequenceRequest ForMeeting(int jednaniId) => new(jednaniId);

    /// <summary>Null = parametr chybí nebo je neplatný — stránka bez šipek.</summary>
    public static RecordSequenceRequest? Parse(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, ActiveValue, StringComparison.OrdinalIgnoreCase))
        {
            return Active;
        }

        return normalized.StartsWith(MeetingPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized[MeetingPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var jednaniId)
            && jednaniId > 0
                ? ForMeeting(jednaniId)
                : null;
    }

    public string ToQueryValue()
        => JednaniId is int jednaniId ? MeetingPrefix + jednaniId.ToString(CultureInfo.InvariantCulture) : ActiveValue;
}

/// <summary>Poloha záznamu v seznamu (spec 2026-10-22 R10). Index od 0; kraje bez přetáčení.</summary>
public sealed record RecordSequencePosition(int Index, int Count, RecordSequenceEntry? Previous, RecordSequenceEntry? Next)
{
    /// <summary>Null = záznam v seznamu není (R12) — stránka bez šipek.</summary>
    public static RecordSequencePosition? Locate(IReadOnlyList<RecordSequenceEntry> sequence, int zaznamId)
    {
        for (var index = 0; index < sequence.Count; index++)
        {
            if (sequence[index].ZaznamId != zaznamId)
            {
                continue;
            }

            return new RecordSequencePosition(
                index,
                sequence.Count,
                index > 0 ? sequence[index - 1] : null,
                index < sequence.Count - 1 ? sequence[index + 1] : null);
        }

        return null;
    }
}
```

Run: příkaz ze Step 1. Expected: PASS.

- [ ] **Step 3: Integrační testy služby**

Do `IntegrationTestHelper` přidat `using PmTracker.Web.Services.Records;` a:

```csharp
    public static IRecordSequenceService CreateRecordSequenceService(PmTrackerDbContext dbContext)
        => BuildServiceProvider(dbContext).GetRequiredService<IRecordSequenceService>();
```

`PmTracker.Tests.Integration/DataStore/RecordSequenceServiceTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-10-22 R5–R8, R11: seznamy pro detail jednání a šipky. Záznamy jednání = záznamy
/// zápisu ve stejném pořadí; aktivní záznamy projektu ve stejném pořadí; cizí jednání se ignoruje.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordSequenceServiceTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordSequenceServiceTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MeetingSequence_IsMinutesSetInMinutesOrder()
    {
        var db = await _fixture.CreateDatabaseAsync("record_sequence_meeting");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var service = IntegrationTestHelper.CreateRecordSequenceService(dbContext);

        (await service.GetMeetingSequenceAsync(seed.Meeting888)).Select(x => x.ZaznamId)
            .Should().Equal(seed.ExpectedMeeting888);
        (await service.GetMeetingSequenceAsync(seed.Meeting887)).Select(x => x.ZaznamId)
            .Should().Equal(seed.ExpectedMeeting887, "první jednání nemá minulé; založený až po něm do něj nepatří");
        (await service.GetMeetingSequenceAsync(int.MaxValue)).Should().BeEmpty("neexistující jednání");
    }

    [Fact]
    public async Task ActiveSequence_IsActiveRecordsInMinutesOrder_WithLabels()
    {
        var db = await _fixture.CreateDatabaseAsync("record_sequence_active");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var service = IntegrationTestHelper.CreateRecordSequenceService(dbContext);

        var active = await service.GetActiveSequenceAsync(seed.ProjectId);

        active.Select(x => x.ZaznamId).Should().Equal(seed.ExpectedActive);
        active.Single(x => x.ZaznamId == seed.R1).Label.Should().Be("#1 - SeqR1 Record");
    }

    [Fact]
    public async Task Resolve_IgnoresMeetingOfAnotherProject()
    {
        var db = await _fixture.CreateDatabaseAsync("record_sequence_resolve");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var seed = await MeetingSequenceSeed.CreateAsync(dbContext);
        var otherProjectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SEQOTHER");
        var otherMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, otherProjectId, "OPEN", meetingNumber: 901);
        var service = IntegrationTestHelper.CreateRecordSequenceService(dbContext);

        (await service.ResolveAsync(RecordSequenceRequest.ForMeeting(otherMeetingId), seed.ProjectId))
            .Should().BeNull("jednání jiného projektu se ignoruje (spec §6)");
        (await service.ResolveAsync(RecordSequenceRequest.ForMeeting(seed.Meeting888), seed.ProjectId))!
            .Select(x => x.ZaznamId).Should().Equal(seed.ExpectedMeeting888);
        (await service.ResolveAsync(RecordSequenceRequest.Active, seed.ProjectId))!
            .Select(x => x.ZaznamId).Should().Equal(seed.ExpectedActive);
    }
}
```

Run: `dotnet test PmTracker.Tests.Integration --nologo --filter "FullyQualifiedName~RecordSequenceServiceTests"`
Expected: build FAIL (služba neexistuje).

- [ ] **Step 4: Služba + registrace**

`PmTracker.Web/Services/Records/RecordSequenceService.cs`:

```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Export;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Spec 2026-10-22 R5–R8, R11: seznamy záznamů pro detail jednání a pro procházení šipkami.
/// Jediný zdroj pravdy „co patří do jednání“ — tisk zápisu, detail jednání i šipky.
/// </summary>
public interface IRecordSequenceService
{
    /// <summary>
    /// Záznamy, které patří do jednání (= záznamy jeho zápisu), v pořadí zápisu. Pravidlo je
    /// <see cref="IExportRecordVisibilityEvaluator.IsVisibleForMeetingPrint"/> ke dni jednání
    /// a ke dni minulého jednání téhož projektu. Neexistující jednání = prázdný seznam.
    /// </summary>
    Task<IReadOnlyList<RecordSequenceEntry>> GetMeetingSequenceAsync(int jednaniId, CancellationToken ct = default);

    /// <summary>Aktivní záznamy projektu (bez stavu, nebo stav není konečný — jako filtr „Aktivní“) v pořadí zápisu.</summary>
    Task<IReadOnlyList<RecordSequenceEntry>> GetActiveSequenceAsync(int projektId, CancellationToken ct = default);

    /// <summary>
    /// Seznam podle parametru „seznam“ pro záznam projektu <paramref name="projektId"/>.
    /// Null = jednání jiného projektu nebo neexistující — stránka je pak bez šipek.
    /// </summary>
    Task<IReadOnlyList<RecordSequenceEntry>?> ResolveAsync(RecordSequenceRequest request, int projektId, CancellationToken ct = default);
}

public sealed class RecordSequenceService(
    PmTrackerDbContext dbContext,
    IExportRecordVisibilityEvaluator visibilityEvaluator) : IRecordSequenceService
{
    public async Task<IReadOnlyList<RecordSequenceEntry>> GetMeetingSequenceAsync(int jednaniId, CancellationToken ct = default)
    {
        var meeting = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == jednaniId)
            .Select(x => new { x.ProjektId, x.CisloJednani, x.DatumPlanovane })
            .FirstOrDefaultAsync(ct);
        if (meeting is null)
        {
            return [];
        }

        // Minulé jednání = nejbližší nižší číslo v projektu — stejně jako tisk zápisu.
        var previousMeetingDate = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == meeting.ProjektId && x.CisloJednani < meeting.CisloJednani)
            .OrderByDescending(x => x.CisloJednani)
            .Select(x => (DateTime?)x.DatumPlanovane)
            .FirstOrDefaultAsync(ct);

        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == meeting.ProjektId)
            .ToListAsync(ct);
        var recordIds = records.Select(x => x.Id).ToArray();
        var taskStates = await dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        // Pořadí historie jako v tisku (nejnovější platnost napřed) — výpočet stavu ke dni na něm závisí.
        var historyByRecord = (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderByDescending(x => x.PlatiOd)
                .ThenByDescending(x => x.Id)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ZaznamHistorieStavuZaznamuEntity>)group.ToList());

        var members = records
            .Where(record => visibilityEvaluator.IsVisibleForMeetingPrint(
                record,
                taskStates,
                historyByRecord.GetValueOrDefault(record.Id) ?? Array.Empty<ZaznamHistorieStavuZaznamuEntity>(),
                meeting.DatumPlanovane.Date,
                previousMeetingDate?.Date))
            .ToList();
        return await OrderAsync(meeting.ProjektId, members, ct);
    }

    public async Task<IReadOnlyList<RecordSequenceEntry>> GetActiveSequenceAsync(int projektId, CancellationToken ct = default)
    {
        var finalStateIds = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.IsFinal)
            .Select(x => x.Id)
            .ToArrayAsync(ct);
        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .Where(x => !x.StavUkoluId.HasValue || !finalStateIds.Contains(x.StavUkoluId.Value))
            .ToListAsync(ct);
        return await OrderAsync(projektId, records, ct);
    }

    public async Task<IReadOnlyList<RecordSequenceEntry>?> ResolveAsync(
        RecordSequenceRequest request, int projektId, CancellationToken ct = default)
    {
        if (request.JednaniId is not int jednaniId)
        {
            return await GetActiveSequenceAsync(projektId, ct);
        }

        // Jednání jiného projektu (nebo neexistující) se ignoruje — stránka bez šipek (spec §6).
        var meetingProjectId = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == jednaniId)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct);
        return meetingProjectId == projektId
            ? await GetMeetingSequenceAsync(jednaniId, ct)
            : null;
    }

    private async Task<IReadOnlyList<RecordSequenceEntry>> OrderAsync(
        int projektId, IReadOnlyList<ProjektovyZaznamEntity> records, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var subsystemIds = records.Select(x => x.SubsystemId).Distinct().ToArray();
        var subsystems = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => subsystemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var orderBySubsystemId = await ProjectSubsystemOrderingQuery.LoadActiveOrderBySubsystemIdAsync(dbContext, projektId, subsystemIds, ct);
        var categories = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);

        // Klíč řazení = stejné hodnoty jako řádek tisku (ExportRecordProjectionBuilder).
        MinutesOrderKey Key(ProjektovyZaznamEntity record)
        {
            var subsystem = subsystems.GetValueOrDefault(record.SubsystemId);
            var hasOrder = orderBySubsystemId.TryGetValue(record.SubsystemId, out var poradi);
            return new MinutesOrderKey(
                subsystem?.Nazev ?? "-",
                subsystem?.Kod ?? "-",
                hasOrder ? poradi : 0,
                hasOrder,
                categories.GetValueOrDefault(record.KategorieId)?.Nazev ?? "-",
                RecordDisplayOrdering.VisibleNumberPartA(record.CisloViditelneA, record.CisloZaznamu),
                RecordDisplayOrdering.VisibleNumberPartB(record.CisloViditelneTyp, record.CisloViditelneB),
                record.CisloZaznamu);
        }

        return MinutesRecordOrdering.InMinutesOrder(records, Key)
            .Select(record => new RecordSequenceEntry(
                record.Id,
                string.IsNullOrWhiteSpace(record.CisloViditelne)
                    ? record.CisloZaznamu.ToString(CultureInfo.InvariantCulture)
                    : record.CisloViditelne.Trim(),
                record.Nazev))
            .ToList();
    }
}
```

`DataStoreServiceCollectionExtensions.cs`, za `IExportRecordVisibilityEvaluator`:

```csharp
        services.AddScoped<IRecordSequenceService, RecordSequenceService>();
```

Run: příkaz ze Step 3. Expected: PASS 3/3.

- [ ] **Step 5: Tisk zápisu bere členy jednání ze služby**

`ExportProjectionBuilders.cs`:
- přidat `using PmTracker.Web.Services.Records;`;
- do primárního konstruktoru `ExportRecordProjectionBuilder` přidat poslední parametr `IRecordSequenceService recordSequenceService`;
- v bloku `if (applyMeetingSnapshotRules && anchorMeeting is not null)` smazat `var previousMeetingDate = …`;
- nahradit přiřazení `records = records.Where(record => exportRecordVisibilityEvaluator.IsVisibleForMeetingPrint(…)).ToList();`:

```csharp
            // Spec 2026-10-22 R5: které záznamy do jednání patří, rozhoduje jedna sdílená služba —
            // stejný seznam ukazuje detail jednání a procházení šipkami.
            var meetingRecordIds = (await recordSequenceService.GetMeetingSequenceAsync(anchorMeeting.Id, ct))
                .Select(entry => entry.ZaznamId)
                .ToHashSet();
            records = records.Where(record => meetingRecordIds.Contains(record.Id)).ToList();
```

`statusHistoryByRecord`, `anchorMeetingDate` a `statusAtAnchorByRecord` zůstávají — sloupec Stav ke dni jednání.

- [ ] **Step 6: Spustit — charakterizace i export drží**

Run:
- `dotnet test PmTracker.Tests.Integration --nologo --filter "FullyQualifiedName~Export|FullyQualifiedName~RecordSequenceServiceTests"`
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~Export|FullyQualifiedName~RecordSequence|FullyQualifiedName~ServiceCollection"`

Expected: vše PASS (`BuildMeetingTemplate_ListsMeetingRecordsInMinutesOrder` beze změny).

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/Records/RecordSequence.cs PmTracker.Web/Services/Records/RecordSequenceService.cs PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs PmTracker.Web/Services/Export/ExportProjectionBuilders.cs PmTracker.Tests.Integration/TestInfrastructure/IntegrationTestHelper.cs PmTracker.Tests.Integration/DataStore/RecordSequenceServiceTests.cs PmTracker.Tests.Unit/Records/RecordSequenceRequestTests.cs PmTracker.Tests.Unit/Records/RecordSequencePositionTests.cs
git commit -m "feat(zaznamy): služba sekvence záznamů — jeden zdroj „co patří do jednání“, tisk zápisu ji používá (spec 2026-10-22 R5, R8, R11)"
```

---

### Task 8: Detail jednání ukáže jen záznamy jednání (R6)

**Files:**
- Modify: `PmTracker.Web/Services/IProjectService.cs:11`
- Modify: `PmTracker.Web/Services/ProjectService.cs` (pole + ctor)
- Modify: `PmTracker.Web/Services/ProjectService.LazyQueries.cs:33-66`
- Modify: `PmTracker.Web/Controllers/JednaniController.cs:89-94`
- Modify: `PmTracker.Web/Controllers/ProjektyController.TabPartials.cs:24`
- Modify: `PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml:25`
- Modify: `PmTracker.Web/wwwroot/css/site.css` (`.records-meeting-empty`)
- Modify: `PmTracker.Tests.Unit/Projects/ProjektyControllerBehaviorTests.cs`, `PmTracker.Tests.Unit/Controllers/ProjektyControllerTeamAuthzTests.cs` (fakes)
- Test: `PmTracker.Tests.Api/Controllers/MeetingRecordsMembershipRenderTests.cs`

**Interfaces:**
- Consumes: `IRecordSequenceService.GetMeetingSequenceAsync` (Task 7).
- Produces: `IProjectService.BuildMeetingRecordsTabAsync(int projectId, int jednaniId, CancellationToken ct = default) : Task<ProjektZaznamyTabViewModel>`; prázdný stav `[data-records-meeting-empty]`.

- [ ] **Step 1: Api test**

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R6: detail otevřeného / připravovaného jednání ukáže jen záznamy jednání
/// (= záznamy jeho zápisu), při načtení stránky i při obnovení záložky. Záložka Záznamy projektu
/// ukazuje dál všechny záznamy.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingRecordsMembershipRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingRecordsMembershipRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int PmId, int ProjectId, int MeetingId, int Member, int FoundedLater, int Closed);

    /// <summary>První jednání projektu (dnes): patří běžící záznam; založený zítra a uzavřený ne.</summary>
    private async Task<Seed> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        var member = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} patri");
        var foundedLater = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} pozdeji");
        await _fixture.SetRecordFoundingDateAsync(foundedLater, DateTime.Today.AddDays(1));
        var closed = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} uzavreny");
        await _fixture.CloseRecordAsync(closed);
        return new Seed(pmId, projectId, meetingId, member, foundedLater, closed);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static void ShouldShowOnlyMember(string html, Seed s)
    {
        html.Should().Contain($"data-record-id=\"{s.Member}\"");
        html.Should().NotContain($"data-record-id=\"{s.FoundedLater}\"", "založený po dni jednání do zápisu nepatří");
        html.Should().NotContain($"data-record-id=\"{s.Closed}\"", "uzavřený k jednání (bez minulého jednání) do zápisu nepatří");
    }

    [Fact]
    public async Task MeetingDetail_ShowsOnlyMeetingRecords()
    {
        var s = await SeedAsync("ApiMtgMembers1", 9421);
        ShouldShowOnlyMember(await GetHtmlAsync($"/Jednani/Detail/{s.MeetingId}?asUser={s.PmId}"), s);
    }

    [Fact]
    public async Task RefreshInMeetingContext_ShowsOnlyMeetingRecords()
    {
        var s = await SeedAsync("ApiMtgMembers2", 9422);
        ShouldShowOnlyMember(
            await GetHtmlAsync($"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={s.PmId}&kontextJednaniId={s.MeetingId}"), s);
    }

    [Fact]
    public async Task ProjectRecordsTab_StillShowsAllRecords()
    {
        var s = await SeedAsync("ApiMtgMembers3", 9423);

        var html = await GetHtmlAsync($"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={s.PmId}");

        html.Should().Contain($"data-record-id=\"{s.Member}\"")
            .And.Contain($"data-record-id=\"{s.FoundedLater}\"")
            .And.Contain($"data-record-id=\"{s.Closed}\"");
        html.Should().NotContain("data-records-meeting-empty");
    }

    [Fact]
    public async Task MeetingWithoutRecords_ShowsEmptyState()
    {
        var s = await SeedAsync("ApiMtgMembers4", 9424);
        await _fixture.CloseRecordAsync(s.Member);

        var html = await GetHtmlAsync($"/Jednani/Detail/{s.MeetingId}?asUser={s.PmId}");

        html.Should().Contain("data-records-meeting-empty");
        html.Should().Contain("Do tohoto jednání zatím nepatří žádný záznam.");
        Regex.Matches(html, "<article class=\"record-card").Count.Should().Be(0);
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~MeetingRecordsMembershipRenderTests"`
Expected: 3 FAIL (detail ukazuje všechny záznamy), `ProjectRecordsTab_StillShowsAllRecords` PASS.

- [ ] **Step 2: ProjectService**

`IProjectService` (za `BuildProjectRecordsTabAsync`):

```csharp
    /// <summary>Spec 2026-10-22 R6: záložka Záznamy v detailu jednání — jen záznamy jednání (= zápisu).</summary>
    Task<ProjektZaznamyTabViewModel> BuildMeetingRecordsTabAsync(int projectId, int jednaniId, CancellationToken ct = default);
```

`ProjectService.cs`:
- pole `private readonly IRecordSequenceService recordSequenceService;`;
- poslední parametr konstruktoru `IRecordSequenceService recordSequenceService`;
- přiřazení `this.recordSequenceService = recordSequenceService;`.

`ProjectService.LazyQueries.cs`. Stávající `BuildProjectRecordsTabAsync` přejmenovat na privátní `BuildRecordsTabAsync(int id, IReadOnlySet<int>? onlyRecordIds, CancellationToken ct)`. Na začátek doplnit filtr a nad ni dát dvě veřejné metody:

```csharp
    public Task<ProjektZaznamyTabViewModel> BuildProjectRecordsTabAsync(int id, CancellationToken ct = default)
        => BuildRecordsTabAsync(id, onlyRecordIds: null, ct);

    /// <summary>
    /// Spec 2026-10-22 R6: záložka Záznamy v detailu jednání — jen záznamy, které do jednání patří
    /// (= záznamy jeho zápisu, <see cref="IRecordSequenceService.GetMeetingSequenceAsync"/>).
    /// </summary>
    public async Task<ProjektZaznamyTabViewModel> BuildMeetingRecordsTabAsync(int projectId, int jednaniId, CancellationToken ct = default)
    {
        var members = await recordSequenceService.GetMeetingSequenceAsync(jednaniId, ct);
        return await BuildRecordsTabAsync(projectId, members.Select(x => x.ZaznamId).ToHashSet(), ct);
    }

    private async Task<ProjektZaznamyTabViewModel> BuildRecordsTabAsync(int id, IReadOnlySet<int>? onlyRecordIds, CancellationToken ct)
    {
        var summaries = await BuildRecordCardSummariesForProjectAsync(id, ct);
        if (onlyRecordIds is not null)
        {
            summaries = summaries.Where(x => onlyRecordIds.Contains(x.Summary.Id)).ToList();
        }
        var filterShell = await BuildProjectFilterShellAsync(id, "records", ct);
        // … dál beze změny (return new ProjektZaznamyTabViewModel { … })
```

- [ ] **Step 3: Controllery**

`JednaniController.Detail`:

```csharp
        if (!model.IsReadOnly)
        {
            // Spec 2026-10-22 R6: jen záznamy jednání (= záznamy jeho zápisu); filtry fungují uvnitř nich.
            var zaznamyTab = await _projectService.BuildMeetingRecordsTabAsync(model.ProjektId, model.Jednani.Id, ct);
            await PrepareRecordsTabPresentationAsync(zaznamyTab, _recordProposalService, model.Jednani.Id, ct);
            model.ZaznamyTab = zaznamyTab;
        }
```

`ProjektyController.RecordsTabPartial`:

```csharp
        // Spec 2026-10-22 R6: obnovení záložky v detailu jednání drží jen záznamy jednání.
        var model = kontextJednaniId.HasValue
            ? await _projectService.BuildMeetingRecordsTabAsync(id, kontextJednaniId.Value, ct)
            : await _projectService.BuildProjectRecordsTabAsync(id, ct);
```

- [ ] **Step 4: Prázdný stav**

`_ProjectRecordsTab.cshtml`, za `@await Html.PartialAsync("~/Views/Projekty/_ProjectFilterShell.cshtml", Model.FilterShell)`:

```cshtml
    @if (Model.KontextJednaniId.HasValue && Model.SkupinyZaznamu.Count == 0)
    {
        @* Spec 2026-10-22 R6: detail jednání ukazuje jen záznamy jednání (= záznamy zápisu). *@
        <p class="muted records-meeting-empty" data-records-meeting-empty>Do tohoto jednání zatím nepatří žádný záznam.</p>
    }
```

`site.css` (za `.muted { … }`):

```css
/* Spec 2026-10-22 R6: prázdný seznam záznamů v detailu jednání. */
.records-meeting-empty {
    margin: 16px 0;
}
```

- [ ] **Step 5: Unit fakes**

`ProjektyControllerBehaviorTests.FakeProjectService` (za `BuildProjectRecordsTabAsync`):

```csharp
        public Task<ProjektZaznamyTabViewModel> BuildMeetingRecordsTabAsync(int projectId, int jednaniId, CancellationToken ct = default)
            => BuildProjectRecordsTabAsync(projectId, ct);
```

`ProjektyControllerTeamAuthzTests` (fake):

```csharp
        public Task<ProjektZaznamyTabViewModel> BuildMeetingRecordsTabAsync(int projectId, int jednaniId, CancellationToken ct = default)
            => throw new NotSupportedException();
```

- [ ] **Step 6: Spustit — projde**

Run:
- `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~MeetingRecordsMembershipRenderTests|FullyQualifiedName~MeetingDetailRecordsRenderTests|FullyQualifiedName~MeetingContextRecordsTabRenderTests|FullyQualifiedName~MeetingAttendanceRenderTests"`
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~ProjektyController|FullyQualifiedName~JednaniController|FullyQualifiedName~AppCssColorTokenTests"`

Expected: vše PASS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Services/IProjectService.cs PmTracker.Web/Services/ProjectService.cs PmTracker.Web/Services/ProjectService.LazyQueries.cs PmTracker.Web/Controllers/JednaniController.cs PmTracker.Web/Controllers/ProjektyController.TabPartials.cs PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Unit/Projects/ProjektyControllerBehaviorTests.cs PmTracker.Tests.Unit/Controllers/ProjektyControllerTeamAuthzTests.cs PmTracker.Tests.Api/Controllers/MeetingRecordsMembershipRenderTests.cs
git commit -m "feat(jednani): detail jednání ukáže jen záznamy jednání — stejné jako zápis (spec 2026-10-22 R6)"
```

---

### Task 9: Menu karty — „Otevřít na celou obrazovku“ a seznam v odkazech (R9)

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml:126-130`
- Modify: `PmTracker.Web/Controllers/BaseController.RecordsTab.cs:91-94`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.Partials.cs:152`
- Test: `PmTracker.Tests.Unit/Projects/RecordCardOpenPageMenuMarkupTests.cs`
- Test: `PmTracker.Tests.Api/Controllers/RecordCardOpenPageLinksRenderTests.cs`

**Interfaces:**
- Consumes: `RecordSequenceRequest.Active`, `RecordSequenceRequest.ForMeeting`, `ToQueryValue()` (Task 7).
- Produces: položky menu `[data-record-open-page="same-tab"]` (první) a `[data-record-open-page="new-tab"]`. Obě vedou na `/Zaznamy/Detail/{id}?seznam=…&returnUrl=…`.

- [ ] **Step 1: Testy**

`PmTracker.Tests.Unit/Projects/RecordCardOpenPageMenuMarkupTests.cs`:

```csharp
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

/// <summary>Spec 2026-10-22 R9: menu › na kartě — první položka otevře stránku záznamu ve stejné kartě.</summary>
public sealed class RecordCardOpenPageMenuMarkupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Menu => Regex.Match(
        File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml")),
        "<div class=\"record-actions-menu\"[\\s\\S]*?\\n                        </div>").Value;

    [Fact]
    public void FullScreenItem_IsFirst_AndStaysInSameTab()
    {
        var menu = Menu;
        var full = menu.IndexOf("Otevřít na celou obrazovku", StringComparison.Ordinal);
        full.Should().BeGreaterThan(0);
        full.Should().BeLessThan(menu.IndexOf("Otevřít na nové kartě", StringComparison.Ordinal));
        menu.IndexOf("Otevřít na celou obrazovku", StringComparison.Ordinal)
            .Should().BeLessThan(menu.IndexOf("data-goto-schedule", StringComparison.Ordinal));
        Regex.Match(menu, "<a[^>]*data-record-open-page=\"same-tab\"[^>]*>").Value
            .Should().NotContain("target=", "stejná karta prohlížeče");
    }
}
```

`PmTracker.Tests.Api/Controllers/RecordCardOpenPageLinksRenderTests.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R9, R11: obě položky „Otevřít…“ v menu karty vedou na stránku záznamu se
/// seznamem pro šipky a s návratem — z detailu jednání záznamy jednání, ze záložky Záznamy aktivní.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordCardOpenPageLinksRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordCardOpenPageLinksRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int RecordId, int MeetingId)> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        return (projectId, recordId, meetingId);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Item(string html, string kind)
        => Regex.Match(html, $"<a[^>]*data-record-open-page=\"{kind}\"[^>]*>").Value;

    [Fact]
    public async Task ProjectRecordsTab_ItemsCarryActiveList()
    {
        var s = await SeedAsync("ApiOpenPage1", 9431);

        var html = await GetHtmlAsync($"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={_fixture.AdminOsobaId}");

        var href = $"href=\"/Zaznamy/Detail/{s.RecordId}?seznam=aktivni&returnUrl=%2FProjekty%2FDetail%2F{s.ProjectId}%3Ftab%3Dzaznamy";
        Item(html, "same-tab").Should().Contain(href).And.NotContain("target=");
        Item(html, "new-tab").Should().Contain(href).And.Contain("target=\"_blank\"");
        html.IndexOf("Otevřít na celou obrazovku", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("Otevřít na nové kartě", StringComparison.Ordinal), "nová položka je v menu první");
    }

    [Fact]
    public async Task MeetingContext_ItemsCarryMeetingList()
    {
        var s = await SeedAsync("ApiOpenPage2", 9432);

        var html = await GetHtmlAsync(
            $"/Projekty/RecordsTabPartial/{s.ProjectId}?asUser={_fixture.AdminOsobaId}&kontextJednaniId={s.MeetingId}");

        var href = $"href=\"/Zaznamy/Detail/{s.RecordId}?seznam=jednani-{s.MeetingId}&returnUrl=%2FJednani%2FDetail%2F{s.MeetingId}\"";
        Item(html, "same-tab").Should().Contain(href);
        Item(html, "new-tab").Should().Contain(href);
    }
}
```

Run:
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordCardOpenPageMenuMarkupTests"`
- `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~RecordCardOpenPageLinksRenderTests"`

Expected: FAIL (položka a `seznam` chybí).

- [ ] **Step 2: Menu**

`_ZaznamPartial.cshtml`, blok `@if (!string.IsNullOrWhiteSpace(Model.RecordPageUrl))`:

```cshtml
                            @if (!string.IsNullOrWhiteSpace(Model.RecordPageUrl))
                            {
                                @* Spec 2026-10-22 R9: stránka záznamu ve stejné kartě prohlížeče — se seznamem
                                   pro šipky (záznamy jednání / aktivní záznamy) a návratem sem. *@
                                <a class="record-actions-menu-item" role="menuitem"
                                   href="@Model.RecordPageUrl" data-record-open-page="same-tab">Otevřít na celou obrazovku</a>
                                <a class="record-actions-menu-item" role="menuitem"
                                   href="@Model.RecordPageUrl" target="_blank" rel="noopener" data-record-open-page="new-tab">Otevřít na nové kartě</a>
                            }
```

- [ ] **Step 3: Odkazy se seznamem**

`BaseController.RecordsTab.cs`: přidat `using PmTracker.Web.Services.Records;` a nahradit přiřazení `record.RecordPageUrl`:

```csharp
        // Spec 2026-10-22 R9, R11: stránka záznamu nese seznam pro šipky — z detailu jednání
        // záznamy jednání, ze záložky Záznamy aktivní záznamy projektu.
        var recordPageSeznam = kontextJednaniId.HasValue
            ? RecordSequenceRequest.ForMeeting(kontextJednaniId.Value).ToQueryValue()
            : RecordSequenceRequest.Active.ToQueryValue();
        record.RecordPageUrl = Url.Action("Detail", "Zaznamy", new { id = summary.Id, seznam = recordPageSeznam, returnUrl = recordPageReturnUrl })
            ?? $"/Zaznamy/Detail/{summary.Id}";
```

`ZaznamyController.Partials.cs`: přidat `using PmTracker.Web.Services.Records;` a v `PrepareRecordCardShellPresentation`:

```csharp
        record.RecordPageUrl ??= Url.Action(nameof(Detail), new
        {
            id = summary.Id,
            seznam = RecordSequenceRequest.Active.ToQueryValue(),
            returnUrl = ProjektDetailTabUrl(projektId, "zaznamy")
        });
```

- [ ] **Step 4: Spustit — projde**

Run:
- příkazy ze Step 1;
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~RecordCardScheduleToggleMarkupTests"`;
- `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~RecordScheduleTogglePartialTests|FullyQualifiedName~MeetingContextRecordsTabRenderTests"`.

Expected: vše PASS.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml PmTracker.Web/Controllers/BaseController.RecordsTab.cs PmTracker.Web/Controllers/ZaznamyController.Partials.cs PmTracker.Tests.Unit/Projects/RecordCardOpenPageMenuMarkupTests.cs PmTracker.Tests.Api/Controllers/RecordCardOpenPageLinksRenderTests.cs
git commit -m "feat(zaznamy): menu karty „Otevřít na celou obrazovku“, odkazy na stránku nesou seznam pro šipky (spec 2026-10-22 R9)"
```

---

### Task 10: Stránka záznamu — Předchozí · N / M · Další (R10–R12)

**Files:**
- Modify: `PmTracker.Web/Models/ViewModels/Projekty/ZaznamDetailPageViewModel.cs`
- Modify: `PmTracker.Web/Controllers/ZaznamyController.cs` (ctor, `Detail`, `PrepareRecordDetailPagePresentationAsync`)
- Modify: `PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml` (navigace před `<section class="card record-page-header">`)
- Modify: `PmTracker.Web/wwwroot/css/site.css` (`.record-sequence-nav`)
- Modify: `PmTracker.Tests.Unit/Controllers/ZaznamyControllerEditAuthzTests.cs` (ctor)
- Modify: `PmTracker.Tests.Unit/Layout/RecordLayoutCssTests.cs` (+1)
- Test: `PmTracker.Tests.Api/Controllers/RecordSequencePageRenderTests.cs`

**Interfaces:**
- Consumes: `IRecordSequenceService.ResolveAsync`, `RecordSequenceRequest.Parse`, `RecordSequencePosition.Locate`, `RecordSequenceEntry.Label` (Task 7).
- Produces:
  - `ZaznamDetailPageViewModel.Sequence : RecordSequenceNavigationViewModel?` s vlastnostmi `Position`, `Count`, `PreviousUrl`, `PreviousLabel`, `NextUrl`, `NextLabel`;
  - markup `<nav class="record-sequence-nav" data-record-sequence-nav data-prev-url data-next-url>`, `[data-record-sequence-prev]`, `[data-record-sequence-next]`, `[data-record-sequence-position]`. Task 11 je čte.

- [ ] **Step 1: Api test**

```csharp
using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R10–R12: stránka záznamu se seznamem (seznam=jednani-{id} / aktivni) ukáže
/// pozici a odkazy na sousedy se stejným seznamem i návratem. Bez seznamu, s cizím jednáním,
/// s neplatnou hodnotou nebo pro záznam mimo seznam je stránka bez šipek.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordSequencePageRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordSequencePageRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int MeetingId, int R1, int R2, int R3, int Closed)
    {
        public string MeetingList => $"seznam=jednani-{MeetingId}&returnUrl=%2FJednani%2FDetail%2F{MeetingId}";
    }

    /// <summary>Jednání dnes; tři běžící úkoly (pořadí = číslo záznamu) a jeden uzavřený.</summary>
    private async Task<Seed> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        var r1 = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} prvni");
        var r2 = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} druhy");
        var r3 = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} treti");
        var closed = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} uzavreny");
        await _fixture.CloseRecordAsync(closed);
        return new Seed(projectId, meetingId, r1, r2, r3, closed);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Nav(string html)
        => Regex.Match(html, "<nav class=\"record-sequence-nav\"[\\s\\S]*?</nav>").Value;

    [Fact]
    public async Task MiddleRecord_ShowsPosition_AndNeighboursWithSameListAndReturn()
    {
        var s = await SeedAsync("ApiSeqPage1", 9441);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}");

        var nav = Nav(html);
        nav.Should().Contain("2 / 3");
        nav.Should().Contain($"data-prev-url=\"/Zaznamy/Detail/{s.R1}?{s.MeetingList}\"");
        nav.Should().Contain($"data-next-url=\"/Zaznamy/Detail/{s.R3}?{s.MeetingList}\"");
        nav.Should().Contain($"href=\"/Zaznamy/Detail/{s.R1}?{s.MeetingList}\"");
        nav.Should().Contain($"href=\"/Zaznamy/Detail/{s.R3}?{s.MeetingList}\"");
        nav.Should().Contain("ApiSeqPage1 prvni", "nápověda tlačítka nese číslo a název sousedního záznamu");
        Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"").Groups[1].Value
            .Should().StartWith($"/Jednani/Detail/{s.MeetingId}", "návrat vede tam, odkud jsem přišel (R11)");
    }

    [Fact]
    public async Task EdgesOfList_DisableButton_NoWrap()
    {
        var s = await SeedAsync("ApiSeqPage2", 9442);

        var first = Nav(await GetHtmlAsync($"/Zaznamy/Detail/{s.R1}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}"));
        var last = Nav(await GetHtmlAsync($"/Zaznamy/Detail/{s.R3}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}"));

        first.Should().Contain("1 / 3").And.Contain("data-prev-url=\"\"");
        Regex.Match(first, "<gov-button[^>]*data-record-sequence-prev[^>]*>").Value.Should().Contain("disabled");
        last.Should().Contain("3 / 3").And.Contain("data-next-url=\"\"");
        Regex.Match(last, "<gov-button[^>]*data-record-sequence-next[^>]*>").Value.Should().Contain("disabled");
    }

    [Fact]
    public async Task ActiveList_SkipsClosedRecords()
    {
        var s = await SeedAsync("ApiSeqPage3", 9443);

        var nav = Nav(await GetHtmlAsync($"/Zaznamy/Detail/{s.R1}?seznam=aktivni&asUser={_fixture.AdminOsobaId}"));

        nav.Should().Contain("1 / 3", "uzavřený záznam do aktivních nepatří (R8)");
    }

    [Fact]
    public async Task NoList_ForeignMeeting_InvalidValue_RecordOutsideList_HaveNoArrows()
    {
        var s = await SeedAsync("ApiSeqPage4", 9444);
        var other = await SeedAsync("ApiSeqPage4b", 9445);
        var asUser = $"asUser={_fixture.AdminOsobaId}";

        (await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "bez parametru seznam vypadá stránka jako dřív");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?seznam=jednani-{other.MeetingId}&{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "jednání jiného projektu se ignoruje");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?seznam=cokoli&{asUser}"))
            .Should().NotContain("data-record-sequence-nav");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.Closed}?seznam=aktivni&{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "záznam mimo seznam šipky nemá (R12)");
        (await GetHtmlAsync($"/Zaznamy/Detail/{s.Closed}?{s.MeetingList}&{asUser}"))
            .Should().NotContain("data-record-sequence-nav", "uzavřený k jednání do něj nepatří (R12)");
    }

    [Fact]
    public async Task EditLink_ReturnsToPageWithListAndOrigin()
    {
        var s = await SeedAsync("ApiSeqPage5", 9446);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.R2}?{s.MeetingList}&asUser={_fixture.AdminOsobaId}");

        html.Should().Contain(
            $"returnUrl=%2FZaznamy%2FDetail%2F{s.R2}%3Fseznam%3Djednani-{s.MeetingId}%26returnUrl%3D%252FJednani%252FDetail%252F{s.MeetingId}",
            "po úpravě se uživatel vrátí na stránku i se šipkami a s návratem (R11)");
    }
}
```

Run: `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~RecordSequencePageRenderTests"`
Expected: 4 FAIL, `NoList_…` PASS (dnes šipky nikde nejsou).

- [ ] **Step 2: View model**

V `ZaznamDetailPageViewModel` (za `BackUrl`):

```csharp
    /// <summary>
    /// Spec 2026-10-22 R10: procházení záznamů seznamu (jednání / aktivní). NULL = stránka bez šipek
    /// (bez parametru seznam, s neplatným nebo cizím seznamem, záznam v seznamu není — R12).
    /// </summary>
    public RecordSequenceNavigationViewModel? Sequence { get; set; }
```

Na konec souboru:

```csharp
/// <summary>Spec 2026-10-22 R10: „‹ Předchozí · N / M · Další ›“ na stránce záznamu.</summary>
public sealed class RecordSequenceNavigationViewModel
{
    /// <summary>Pozice od 1.</summary>
    public int Position { get; init; }
    public int Count { get; init; }
    public string? PreviousUrl { get; init; }
    public string? PreviousLabel { get; init; }
    public string? NextUrl { get; init; }
    public string? NextLabel { get; init; }
}
```

- [ ] **Step 3: Controller**

`ZaznamyController`:
- pole `private readonly IRecordSequenceService _recordSequenceService;`;
- poslední parametr konstruktoru `IRecordSequenceService recordSequenceService`;
- přiřazení `_recordSequenceService = recordSequenceService;`;
- `Detail(int id, string? returnUrl, string? seznam = null, CancellationToken ct = default)` a volání `await PrepareRecordDetailPagePresentationAsync(model, returnUrl, seznam, ct);`.

`PrepareRecordDetailPagePresentationAsync`:

```csharp
    private async Task PrepareRecordDetailPagePresentationAsync(ZaznamDetailPageViewModel model, string? returnUrl, string? seznam, CancellationToken ct)
    {
        var projektId = model.ProjektId;
        var recordId = model.Summary.Id;

        model.CanEditRecord = RecordEditorAffordancePolicy.CanOpenEditor(CurrentUserContext, projektId);
        model.CanCreateScheduleProposal = model.Summary.JeUkol
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projektId);

        // Spec 2026-10-22 R9–R12: seznam pro šipky počítá server z parametru „seznam“. Neplatný
        // parametr, jednání jiného projektu nebo záznam mimo seznam = stránka bez šipek.
        var normalizedReturnUrl = NormalizeLocalReturnUrl(returnUrl);
        var request = RecordSequenceRequest.Parse(seznam);
        var sequence = request is null ? null : await _recordSequenceService.ResolveAsync(request, projektId, ct);
        var seznamValue = sequence is null ? null : request!.ToQueryValue();
        string PageUrl(int zaznamId)
            => Url.Action(nameof(Detail), new { id = zaznamId, seznam = seznamValue, returnUrl = normalizedReturnUrl })
                ?? $"/Zaznamy/Detail/{zaznamId}";

        var position = sequence is null ? null : RecordSequencePosition.Locate(sequence, recordId);
        if (position is not null)
        {
            model.Sequence = new RecordSequenceNavigationViewModel
            {
                Position = position.Index + 1,
                Count = position.Count,
                PreviousUrl = position.Previous is null ? null : PageUrl(position.Previous.ZaznamId),
                PreviousLabel = position.Previous?.Label,
                NextUrl = position.Next is null ? null : PageUrl(position.Next.ZaznamId),
                NextLabel = position.Next?.Label
            };
        }

        // Úprava a návrh se vracejí na stránku i se seznamem a s návratem (R11).
        var pageUrl = PageUrl(recordId);
        model.EditUrl = Url.Action(nameof(Edit), new { id = recordId, projektId, returnUrl = pageUrl });
        model.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy",
            new { projektId, zaznamId = recordId, returnUrl = pageUrl });
        model.ScheduleProposalCheckUrl = Url.Action("ScheduleProposalAvailability", "Navrhy",
            new { projektId, zaznamId = recordId });
        model.PrintPdfUrl = Url.Action("UkolTisk", "Export", new { zaznamId = recordId, projektId, autoPrint = true });
        model.PrintWordUrl = Url.Action("UkolWord", "Export", new { zaznamId = recordId, projektId });
        model.BackUrl = normalizedReturnUrl ?? ProjektDetailTabUrl(projektId, "zaznamy");

        // Vyjádření: stejná presentation jako na kartě (oprávnění, filtr draft jednání).
        await PrepareRecordCommentsPresentationAsync(model.Comments, model.Summary, ct);
        model.Comments.CurrentUserOsobaId = CurrentUserContext.OsobaId;
    }
```

`ZaznamyControllerEditAuthzTests.BuildController`: do volání konstruktoru přidat `recordSequenceService: Mock.Of<IRecordSequenceService>()`.

- [ ] **Step 4: View**

`ZaznamDetailPage.cshtml`, těsně před `<section class="card record-page-header">`:

```cshtml
@if (Model.Sequence is { } sequence)
{
    @* Spec 2026-10-22 R10: procházení záznamů seznamu (jednání / aktivní). Klávesy ← → obsluhuje
       recordSequenceKeys.js podle data-prev-url / data-next-url (prázdné = kraj seznamu). *@
    <nav class="record-sequence-nav"
         aria-label="Procházení záznamů"
         data-record-sequence-nav
         data-prev-url="@(sequence.PreviousUrl ?? string.Empty)"
         data-next-url="@(sequence.NextUrl ?? string.Empty)">
        <pm-button variant="Secondary"
                   size="Small"
                   href="@sequence.PreviousUrl"
                   disabled="@(sequence.PreviousUrl is null)"
                   title="@(sequence.PreviousLabel is null ? "Začátek seznamu" : $"Předchozí: {sequence.PreviousLabel} (klávesa ←)")"
                   data-record-sequence-prev>‹ Předchozí</pm-button>
        <span class="record-sequence-position" data-record-sequence-position>@sequence.Position / @sequence.Count</span>
        <pm-button variant="Secondary"
                   size="Small"
                   href="@sequence.NextUrl"
                   disabled="@(sequence.NextUrl is null)"
                   title="@(sequence.NextLabel is null ? "Konec seznamu" : $"Další: {sequence.NextLabel} (klávesa →)")"
                   data-record-sequence-next>Další ›</pm-button>
    </nav>
}
```

`PmButtonTagHelper` vypíše `href` jen s hodnotou (`if (!string.IsNullOrWhiteSpace(Href))`, ověřeno při psaní plánu). Tlačítko na kraji seznamu je proto `gov-button disabled` bez odkazu.

- [ ] **Step 5: CSS + test**

`site.css` (za pravidla `.record-page-header…`):

```css
/* Spec 2026-10-22 R10: procházení záznamů na stránce záznamu (‹ Předchozí · N / M · Další ›). */
.record-sequence-nav {
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: 8px 12px;
    margin: 0 0 12px;
}

.record-sequence-position {
    font-size: 14px;
    color: var(--text-secondary);
    font-variant-numeric: tabular-nums;
}
```

Do `RecordLayoutCssTests`:

```csharp
    [Fact]
    public void SequenceNav_IsWrappingFlexRow()
    {
        var nav = Rule(".record-sequence-nav");
        nav.Should().Contain("display: flex").And.Contain("flex-wrap: wrap", "na úzkém okně se zalomí, nepřeteče");
        Rule(".record-sequence-position").Should().Contain("color: var(--text-secondary)");
    }
```

- [ ] **Step 6: Spustit — projde**

Run:
- `dotnet test PmTracker.Tests.Api --nologo --filter "FullyQualifiedName~RecordSequencePageRenderTests|FullyQualifiedName~RecordDetailPageRenderTests|FullyQualifiedName~Breadcrumb"`
- `dotnet test PmTracker.Tests.Unit --nologo --filter "FullyQualifiedName~ZaznamyController|FullyQualifiedName~RecordLayoutCssTests|FullyQualifiedName~AppCssColorTokenTests|FullyQualifiedName~ProjectScopedPolicy"`

Expected: vše PASS.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/Projekty/ZaznamDetailPageViewModel.cs PmTracker.Web/Controllers/ZaznamyController.cs PmTracker.Web/Views/Projekty/ZaznamDetailPage.cshtml PmTracker.Web/wwwroot/css/site.css PmTracker.Tests.Unit/Controllers/ZaznamyControllerEditAuthzTests.cs PmTracker.Tests.Unit/Layout/RecordLayoutCssTests.cs PmTracker.Tests.Api/Controllers/RecordSequencePageRenderTests.cs
git commit -m "feat(zaznamy): stránka záznamu — Předchozí · N / M · Další v seznamu jednání / aktivních (spec 2026-10-22 R10–R12)"
```

---

### Task 11: Klávesy ← → na stránce záznamu (R10)

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/recordSequenceKeys.js`
- Modify: `PmTracker.Web/wwwroot/js/modules/bootstrap.js` (import za `meetingMinutes.js`)
- Test: `tests/js/dom/recordSequenceKeys.test.js`
- Test: `PmTracker.Tests.E2E/Scenarios/RecordSequenceScenariosTests.cs`

**Interfaces:**
- Consumes: markup z Task 10 (`[data-record-sequence-nav]`, `data-prev-url`, `data-next-url`, `[data-record-sequence-position]`), `E2EOwnProject` (Task 4), menu z Task 9 (`[data-record-open-page="same-tab"]`).
- Produces: `resolveSequenceKey(event, { editable, dialogOpen }) → "prev" | "next" | null`, `isEditableTarget(target) → boolean`, `isDialogOpen(doc) → boolean`.

- [ ] **Step 1: JS test**

`tests/js/dom/recordSequenceKeys.test.js`:

```js
import { test } from "node:test";
import assert from "node:assert/strict";

import {
    isDialogOpen,
    isEditableTarget,
    resolveSequenceKey
} from "../../../PmTracker.Web/wwwroot/js/modules/recordSequenceKeys.js";

const free = { editable: false, dialogOpen: false };
const key = (name, extra = {}) => ({
    key: name, altKey: false, ctrlKey: false, metaKey: false, shiftKey: false,
    defaultPrevented: false, isComposing: false, ...extra
});

test("← vede na předchozí, → na další", () => {
    assert.equal(resolveSequenceKey(key("ArrowLeft"), free), "prev");
    assert.equal(resolveSequenceKey(key("ArrowRight"), free), "next");
});

test("jiné klávesy nic", () => {
    for (const name of ["ArrowUp", "ArrowDown", "Enter", "a"]) {
        assert.equal(resolveSequenceKey(key(name), free), null, name);
    }
});

test("s modifikátorem nic (Alt+← je v prohlížeči zpět)", () => {
    for (const modifier of ["altKey", "ctrlKey", "metaKey", "shiftKey"]) {
        assert.equal(resolveSequenceKey(key("ArrowRight", { [modifier]: true }), free), null, modifier);
    }
});

test("při psaní, v otevřeném okně a u zpracované události nic", () => {
    assert.equal(resolveSequenceKey(key("ArrowRight"), { editable: true, dialogOpen: false }), null);
    assert.equal(resolveSequenceKey(key("ArrowRight"), { editable: false, dialogOpen: true }), null);
    assert.equal(resolveSequenceKey(key("ArrowRight", { defaultPrevented: true }), free), null);
    assert.equal(resolveSequenceKey(key("ArrowRight", { isComposing: true }), free), null);
});

test("isEditableTarget: editor (contenteditable), pole přes closest; tělo stránky a null ne", () => {
    assert.equal(isEditableTarget({ isContentEditable: true, closest: () => null }), true);
    assert.equal(isEditableTarget({ isContentEditable: false, closest: (selector) => (selector.includes("textarea") ? {} : null) }), true);
    assert.equal(isEditableTarget({ isContentEditable: false, closest: () => null }), false);
    assert.equal(isEditableTarget(null), false);
});

test("isDialogOpen: neprázdný #modal-root nebo otevřený gov-dialog", () => {
    const doc = (modalRoot, dialogs = []) => ({
        getElementById: (id) => (id === "modal-root" ? modalRoot : null),
        querySelectorAll: (selector) => (selector === "gov-dialog" ? dialogs : [])
    });
    const root = (hidden, children) => ({ getAttribute: () => (hidden ? "true" : "false"), childElementCount: children });
    const dialog = (open, attr) => ({ open, getAttribute: () => attr });

    assert.equal(isDialogOpen(doc(root(false, 1))), true);
    assert.equal(isDialogOpen(doc(root(true, 1))), false);
    assert.equal(isDialogOpen(doc(root(false, 0))), false);
    assert.equal(isDialogOpen(doc(null, [dialog(true, null)])), true);
    assert.equal(isDialogOpen(doc(null, [dialog(false, "true")])), true);
    assert.equal(isDialogOpen(doc(null, [dialog(false, null)])), false);
});
```

Run: `npm test`
Expected: FAIL — modul neexistuje.

- [ ] **Step 2: Modul**

`PmTracker.Web/wwwroot/js/modules/recordSequenceKeys.js`:

```js
/**
 * Spec 2026-10-22 R10: procházení záznamů klávesami ← a → na stránce záznamu.
 *
 * Cíle čte z [data-record-sequence-nav] (data-prev-url / data-next-url). Jsou to stejné adresy
 * jako u tlačítek Předchozí / Další; prázdné = kraj seznamu, nepřetáčí se. Klávesy nereagují:
 *   - při psaní (pole, výběr, editor vyjádření);
 *   - v pruhu záložek;
 *   - v otevřeném okně;
 *   - s modifikátory (Alt+← je v prohlížeči „zpět“).
 * Side-effect import v bootstrap.js; při importu nesahá na DOM (testy bez DOM).
 */
import { appendCurrentAsUser } from "./navigationShared.js";

const NAV_SELECTOR = "[data-record-sequence-nav]";
const EDITABLE_SELECTOR = [
    "input", "textarea", "select", "[contenteditable='']", "[contenteditable='true']", "[role='tablist']",
    "gov-form-input", "gov-form-select", "gov-form-textarea", "gov-form-autocomplete"
].join(", ");

/** Kam klávesa vede: "prev", "next" nebo null. Čistá funkce. */
export function resolveSequenceKey(event, { editable, dialogOpen }) {
    if (!event || event.defaultPrevented || event.isComposing) {
        return null;
    }
    if (event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
        return null;
    }
    if (editable || dialogOpen) {
        return null;
    }
    if (event.key === "ArrowLeft") {
        return "prev";
    }
    if (event.key === "ArrowRight") {
        return "next";
    }
    return null;
}

/** Fokus je v prvku, kterému šipky patří (psaní, výběr, editor Quill, záložky). */
export function isEditableTarget(target) {
    if (!target || typeof target.closest !== "function") {
        return false;
    }
    if (target.isContentEditable === true) {
        return true;
    }
    return target.closest(EDITABLE_SELECTOR) !== null;
}

/** Otevřené okno: modal aplikace (#modal-root s obsahem) nebo otevřený gov-dialog. */
export function isDialogOpen(doc) {
    const modalRoot = doc.getElementById("modal-root");
    if (modalRoot && modalRoot.getAttribute("aria-hidden") !== "true" && modalRoot.childElementCount > 0) {
        return true;
    }
    return Array.from(doc.querySelectorAll("gov-dialog"))
        .some((dialog) => dialog.open === true || dialog.getAttribute("open") === "true");
}

function initRecordSequenceKeys() {
    const nav = document.querySelector(NAV_SELECTOR);
    if (!(nav instanceof HTMLElement)) {
        return;
    }

    document.addEventListener("keydown", (event) => {
        // composedPath: fokus uvnitř shadow DOM (gov komponenty) se jinak hlásí jako host.
        const path = typeof event.composedPath === "function" ? event.composedPath() : [];
        const target = path.length > 0 ? path[0] : event.target;
        const direction = resolveSequenceKey(event, {
            editable: isEditableTarget(target),
            dialogOpen: isDialogOpen(document)
        });
        if (!direction) {
            return;
        }
        const url = direction === "prev" ? nav.dataset.prevUrl : nav.dataset.nextUrl;
        if (!url) {
            return;
        }
        event.preventDefault();
        window.location.assign(appendCurrentAsUser(url));
    });
}

if (typeof document !== "undefined") {
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initRecordSequenceKeys);
    } else {
        initRecordSequenceKeys();
    }
}
```

`bootstrap.js`, za `import "./meetingMinutes.js"; …`:

```js
import "./recordSequenceKeys.js";                    // 2026-10-22 — klávesy ← → mezi záznamy na stránce záznamu (spec R10).
```

Run: `npm test`
Expected: PASS 56/56 (45 + 5 z Task 2 + 6).

- [ ] **Step 3: E2E**

`PmTracker.Tests.E2E/Scenarios/RecordSequenceScenariosTests.cs`:

```csharp
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22 C (R6–R12):
/// - detail jednání ukáže jen záznamy jednání;
/// - z menu karty „Otevřít na celou obrazovku“ vede na stránku záznamu se šipkami;
/// - klávesy ← → procházejí záznamy v pořadí zápisu a na krajích nepřetáčí;
/// - v editoru vyjádření klávesy nereagují;
/// - drobečková lišta vrátí do jednání.
/// Vlastní projekt (E2EOwnProject), takže sadu záznamů jednání má test pod kontrolou.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordSequenceScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordSequenceScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private sealed record Seed(int MeetingId, IReadOnlyList<int> MeetingRecordIds, int OutsideRecordId);

    /// <summary>
    /// Pořadí zápisu: subsystém s pořadím 1 („… B“) → záznamy 1, 3; subsystém s pořadím 2 („… A“) → záznam 2.
    /// Záznam 4 je uzavřený a minulé jednání není → do jednání nepatří.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        var project = await E2EOwnProject.CreateAsync(_fixture.Database.ConnectionString, _fixture.AdminOsobaId);
        var r1 = await project.AddTaskAsync(1, project.FirstSubsystemId);
        var r2 = await project.AddTaskAsync(2, project.SecondSubsystemId);
        var r3 = await project.AddTaskAsync(3, project.FirstSubsystemId);
        var r4 = await project.AddTaskAsync(4, project.FirstSubsystemId, closed: true);
        var meetingId = await project.AddOpenMeetingAsync(501, DateTime.Today.AddDays(-3));
        return new Seed(meetingId, [r1, r3, r2], r4);
    }

    /// <summary>
    /// Dev identita: asUser nesou jen drobečky (WithAsUser). Odkazy z menu karty a tlačítka šipek
    /// ho nenesou, proto ho kontext doplní ke každému požadavku na aplikaci (jako ruční průchod).
    /// </summary>
    private async Task<IPage> NewPageAsUserAsync()
    {
        var page = await _fixture.NewPageAsync();
        await page.Context.RouteAsync($"{_fixture.BaseUrl}/**", async route =>
        {
            var url = route.Request.Url;
            if (url.Contains("asUser=", StringComparison.Ordinal))
            {
                await route.ContinueAsync();
                return;
            }
            var separator = url.Contains('?') ? "&" : "?";
            await route.ContinueAsync(new RouteContinueOptions { Url = $"{url}{separator}asUser={_fixture.AdminOsobaId}" });
        });
        return page;
    }

    [Fact]
    public async Task MeetingDetail_FullScreen_ArrowKeysWalkMeetingRecordsInMinutesOrder_BreadcrumbReturns()
    {
        var seed = await SeedAsync();
        var page = await NewPageAsUserAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{seed.MeetingId}");
            await Expect(page.Locator(".record-card[data-record-id]")).ToHaveCountAsync(3);
            await Expect(page.Locator($".record-card[data-record-id='{seed.OutsideRecordId}']")).ToHaveCountAsync(0);

            var first = page.Locator($".record-card[data-record-id='{seed.MeetingRecordIds[0]}']");
            await first.Locator("[data-record-menu-trigger]").DispatchEventAsync("click");
            var openItem = page.Locator("[data-record-menu] [data-record-open-page='same-tab']");
            await Expect(openItem).ToBeVisibleAsync();
            await openItem.ClickAsync();

            await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[0]}\\?seznam=jednani-{seed.MeetingId}"));
            var position = page.Locator("[data-record-sequence-position]");
            await Expect(position).ToHaveTextAsync("1 / 3");

            // Kraj seznamu: ← na prvním nic neudělá (nepřetáčí se).
            await page.Keyboard.PressAsync("ArrowLeft");
            await page.WaitForTimeoutAsync(400);
            await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[0]}\\?"));

            for (var index = 1; index < 3; index++)
            {
                await page.Keyboard.PressAsync("ArrowRight");
                await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[index]}\\?"));
                await Expect(position).ToHaveTextAsync($"{index + 1} / 3");
            }

            await page.Keyboard.PressAsync("ArrowRight");
            await page.WaitForTimeoutAsync(400);
            await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[2]}\\?"));

            await page.Keyboard.PressAsync("ArrowLeft");
            await Expect(position).ToHaveTextAsync("2 / 3");

            await page.Locator(".app-breadcrumb-back").ClickAsync();
            await Expect(page).ToHaveURLAsync(new Regex($"/Jednani/Detail/{seed.MeetingId}"));
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task ArrowKeys_InCommentEditor_DoNotSwitchRecord()
    {
        var seed = await SeedAsync();
        var page = await NewPageAsUserAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{seed.MeetingRecordIds[1]}?seznam=jednani-{seed.MeetingId}");
            await Expect(page.Locator("[data-record-sequence-position]")).ToHaveTextAsync("2 / 3");
            var editor = page.Locator("[data-record-comments-shell] .ql-editor").First;
            await editor.ClickAsync();
            await page.Keyboard.TypeAsync("ab");

            await page.Keyboard.PressAsync("ArrowLeft");
            await page.Keyboard.PressAsync("ArrowRight");
            await page.WaitForTimeoutAsync(400);

            await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[1]}\\?"),
                new() { Timeout = 1000 });
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }
}
```

Run: `dotnet test PmTracker.Tests.E2E --nologo --filter "FullyQualifiedName~RecordSequenceScenariosTests"`

Expected: PASS 2/2.

Když selže, postupovat podle systematic-debugging; časy čekání nezvyšovat naslepo. Kontrolní otázky:
- Přepnula se stránka v editoru? Pak `isEditableTarget` nechytá cíl Quillu.
- Neodešla navigace? Pak chybí `asUser` (route).

Mutační kontrola: dočasně v `isEditableTarget` vracet vždy `false`. Druhý test pak FAIL. Pak změnu vrátit.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/recordSequenceKeys.js PmTracker.Web/wwwroot/js/modules/bootstrap.js tests/js/dom/recordSequenceKeys.test.js PmTracker.Tests.E2E/Scenarios/RecordSequenceScenariosTests.cs
git commit -m "feat(zaznamy): klávesy ← → mezi záznamy na stránce záznamu (spec 2026-10-22 R10)"
```

---

### Task 12: Plné sady, E2E proti baseline, průchod za všechny role, k-otestovani, revize, merge

**Files:**
- Modify: `docs/k-otestovani.md` (sekce 6)
- Modify: `docs/superpowers/specs/2026-10-22-zaznam-hlavicka-detaily-sipky-plan-design.md` (stav „implementováno“)

- [ ] **Step 1: Plné sady**

Run (výstupy do workspace, číst konec):
- `dotnet test PmTracker.Tests.Unit --nologo`
- `dotnet test PmTracker.Tests.Api --nologo`
- `dotnet test PmTracker.Tests.Integration --nologo`
- `npm test`

Expected:
- Unit 2234 + nové, vše PASS;
- Api 513 + nové, vše PASS;
- Integration 198 + 6, jediné selhání je známé `ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`;
- JS 56/56.

Přesné počty zapsat podle výstupu.

- [ ] **Step 2: E2E celá sada proti baseline `main`**

1. Dočasný worktree `main`: `git worktree add "$SP/wt-main" main`, kde `$SP` je scratchpad.
2. Obě sady pustit za sebou, ne souběžně: skript `$SP/e2e-chain.sh`, TRX do scratchpadu.
3. Porovnat jména selhání: `python3 "$SP/e2e-compare.py" "$SP"`.

Expected:
- žádné nové jméno selhání (baseline 32);
- 6 nových testů prošlo: `PlanDateClearScenariosTests` 1, `RecordCardLayoutScenariosTests` 3, `RecordSequenceScenariosTests` 2.

Po porovnání: `git worktree remove --force "$SP/wt-main"`.

- [ ] **Step 3: Průchod v prohlížeči nad spuštěnou aplikací za všechny role** (spec §7, stálé pravidlo)

1. **Příprava:**
   - záloha dev DB (`BACKUP DATABASE [PmTracker] TO DISK = N'/var/opt/mssql/data/pmtracker_pre_pruchod_sipky_20261022.bak' WITH INIT, COPY_ONLY`);
   - dočasné osoby „Průchod <ROLE>“ (11 rolí + PM s vedoucím + bez přístupu) podle memory `reference_all_roles_browser_walkthrough`;
   - `dotnet publish` do `$SP/app` a spuštění na portu 5071 (příkaz v memory `feedback_esm_module_cache_busting`).
2. **Data:**
   - projekt 1 (PMT): jednání 7 OPEN, 3 DRAFT;
   - záznam 99009 — pokud nemá plán harmonogramu, dočasně doplnit plán 10 kroků (zapsat původní stav pro úklid).
3. **Skript** (Node, bundled Playwright z `PmTracker.Tests.E2E/bin/Debug/net8.0/.playwright/package`, `context.route` doplní `asUser`) za každou personu podle matice spec §7:
   - **Detail jednání 7 a 3:**
     - Sada `data-record-id` karet = sada záznamů zápisu stejného jednání. Zápis: `/Export/Jednani/{id}/Tisk?projektId=1`, čísla z `.record-code-badge` porovnat s nadpisy karet.
     - U osoby bez přístupu 404.
   - **Menu › na kartě 99009:**
     - první položka je „Otevřít na celou obrazovku“;
     - klik otevře stránku ve stejné kartě;
     - „N / M“ s M = počet záznamů zápisu;
     - → / ← změní záznam;
     - drobečková lišta vrátí do jednání.
   - **Za PM-personu jednou celý seznam:** pořadí stránek přes → = pořadí zápisu.
   - **Záložka Záznamy:**
     - menu → stránka se `seznam=aktivni`, M = počet karet s `data-filter-aktivni="true"`;
     - uzavřený záznam otevřený z menu nemá šipky.
   - **✕ u plánu:**
     - editory (Superadmin, Admin aplikace, Vlastník, Admin projektu, PM, PM + vedoucí) mají ✕ v editoru;
     - Vedoucí a Zástupce ho mají v návrhu změny harmonogramu (`/Navrhy/CreateScheduleProposal?projektId=1&zaznamId=99009`);
     - ostatní editor ani návrh nemají.
   - **Hlavička karty:** odskok `.record-title-wrap` vs. tělo ≤ 1 px na 1400 i 375 px.
   - **Tabulka detailů:** 2 dvojice na řádek; na 375 px jedna dvojice na řádek bez vodorovného posuvníku.
   - **Konzole bez chyb** (kromě známé 404 u osoby bez přístupu).
4. **Akce:**
   - PM-persona smaže ✕ plán kroku 3 na 99009 a uloží → v DB `plan_datum` NULL;
   - vedoucí odešle návrh se smazaným plánem kroku 4 → PM v detailu návrhu vidí „Navržený: bez plánu“ a schválí → v DB NULL.
5. **Vzhled:**
   - tmavý motiv (tabulka, navigace, indikátor);
   - snímky karty rozbalené / sbalené / v pohledu Harmonogram a stránky záznamu se šipkami, 1400 a 375 px;
   - tisk zápisu jednání 7 má stejné záznamy jako před změnou — porovnat se snímkem zápisu z `main` buildu, pořízeným před přechodem na větev.
6. **Úklid:**
   - dočasné osoby, jejich audit a zámky;
   - návrh a plán vrátit na původní hodnoty;
   - kontrolní dotaz = 0 řádků.

Expected: matice sedí na 100 %, 0 JS chyb. Odchylku řešit podle superpowers:systematic-debugging, ne úpravou očekávání.

- [ ] **Step 4: `docs/k-otestovani.md` sekce 6**

Stejný formát jako sekce 5:
- co to dělá (A–D);
- ověřeno lokálně, s maticí rolí a čísly sad;
- checklist k ručnímu otestování:
  - zarovnání hlavičky;
  - tabulka detailů na kartě i stránce, pohled Harmonogram;
  - detail jednání = zápis;
  - menu → celá obrazovka → ← → → drobečková lišta;
  - klávesy v editoru vyjádření;
  - ✕ v editoru i v návrhu → schválení;
  - text „–“ u kroku bez plánu;
- známá omezení:
  - detail jednání ukazuje méně záznamů než dřív (záměr R6);
  - šipky nerespektují filtry na stránce (R7, R8);
  - výsledek kroku 11 v Task 2 (graf v editoru);
  - oživená kontrola pořadí plánů v editoru (Task 2).

Ve specifikaci změnit řádek **Stav** na „implementováno 2026-10-22 (větev feat/zaznam-hlavicka-sipky-plan)“.

- [ ] **Step 5: Revize celé větve, commit, merge**

Self-review podle `superpowers:requesting-code-review` → `code-reviewer.md`, bez subagenta (uživatelské pravidlo):
- `review-package` nad `git merge-base main HEAD..HEAD`;
- Review Focus tohoto plánu projít bod po bodu;
- rulingy z ledgeru vyhodnotit.

Nálezy Critical / Important opravit v jednom průchodu s testem RED→GREEN. Minor zapsat do ledgeru jako deferred.

```bash
git add docs/k-otestovani.md docs/superpowers/specs/2026-10-22-zaznam-hlavicka-detaily-sipky-plan-design.md
git commit -m "docs(k-otestovani,spec): záznam — hlavička, detaily v tabulce, šipky, mazání plánu"
git checkout main
git merge --no-ff feat/zaznam-hlavicka-sipky-plan -m "Merge feat/zaznam-hlavicka-sipky-plan into main"
git branch -d feat/zaznam-hlavicka-sipky-plan
```

Doplnit hash merge do sekce 6 a commitnout na `main`. Push ne. Aktualizovat memory `project_pre_existing_test_failures` (nové počty sad).

---

## Odhad (Opus inline)

| Task | Odhad |
|---|---|
| 1. ✕ a „–“ u plánu (server) | 0,5 h |
| 2. Editor: živé trvání, ✕ se zámkem, E2E | 0,75 h |
| 3. Návrh nese odstranění plánu | 0,75 h |
| 4. Indikátor v okraji (A) + E2E | 0,5 h |
| 5. Detaily v tabulce (B) | 1,25 h |
| 6. Pořadí zápisu + charakterizace tisku | 0,75 h |
| 7. Služba sekvence + tisk na ni | 1 h |
| 8. Detail jednání jen se záznamy jednání | 0,5 h |
| 9. Menu karty + odkazy se seznamem | 0,5 h |
| 10. Stránka záznamu se šipkami | 0,75 h |
| 11. Klávesy ← → + E2E | 0,75 h |
| 12. Sady, E2E baseline, průchod za všechny role, k-otestovani, revize, merge | 2 h |
| **Celkem** | **≈ 10 h** |

Proti spec (≈ 9,25 h) je o 0,75 h víc. Navíc přibyla oprava nefunkčního přepočtu trvání v editoru (Task 2) a sdílené pořadí zápisu (Task 6). Tvoje ruční ověření podle sekce 6 v `k-otestovani.md` je navíc.
