# Harmonogram: přechod z "trvání + datum" na "jen datum" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nahradit dvojí zadání (trvání v dnech + datum kroku) za **pouze datum** u každého kroku harmonogramu (HS01–HS11 + DELAY varianty). Business pravidlo: datum kroku N nesmí být dřív než datum kroku N−1. Trvání je odvozené, ne zadávané.

**Architecture:** Datový model `zaznam_harmonogram_hodnoty(typ_id, hodnota_int)` zůstává — `hodnota_int` reinterpretováno jako **ordinální datum offset od DatumZalozeni** (resp. přímo datum). UI přejde na single-date pole per krok; výpočet "trvání" se odvozuje pro InfoBary (plán vs. skutečnost). Validace posloupnosti na serveru (FluentValidator) + klientu (inline chyba).

**Tech Stack:** ASP.NET Core 8 MVC Razor, EF Core, existující `harmonogramLogic.js` + `scheduleCalculator.cs`, Quill (nesouvisí), FluentValidation.

---

### Task 0: Audit současného stavu

**Files (read-only):**
- Read: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (ZaznamHarmonogramHodnota, CiselnikHarmonogramTypu, ProjektoveZaznamy)
- Read: `PmTracker.Web/Services/ScheduleCalculator.cs`
- Read: `PmTracker.Web/Services/Records/RecordScheduleService.cs`
- Read: `PmTracker.Web/wwwroot/js/modules/schedule.js` (planner recalc logic)
- Read: `PmTracker.Web/Views/Projekty/_EditZaznamSchedulePanel.cshtml`
- Read: `PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml` (schedule list + infoBar)
- Read: `PmTracker.Web/Views/Projekty/_ScheduleBreakdown*.cshtml` (rozpad)

- [ ] **Step 1: Zachytit stávající interpretaci `hodnota_int`**

Zdokumentovat v docs, jak se v současnosti `hodnota_int` používá:
- Je to **počet dní trvání** kroku (tj. step duration)?
- Anebo **ordinální offset ode dne zahájení** (tj. absolute date offset)?
- Nebo je to smíšené (DELAY = duration, plain = offset)?

Očekávaný výstup: krátký "state of the world" odstavec v `docs/superpowers/specs/2026-04-19-harmonogram-jen-datum-design.md` s ukázkami z DB (vybrat 2 reálné záznamy s naplněným harmonogramem a přečíst hodnoty).

- [ ] **Step 2: Navrhnout nové schéma interpretace**

Rozhodnutí, které uděláme (doplnit do specu):
- **A)** `hodnota_int` = počet dní od `DatumZalozeni` (daysFromStart). Datum kroku = `DatumZalozeni + hodnota_int dní`. Backward-compatible migrace.
- **B)** `hodnota_int` = počet dní od předchozího kroku (duration). Datum kroku = kumulativní součet. Blíží se současnému.
- **C)** Přidat `datum_kroku date NULL` sloupec. `hodnota_int` ponechat pro BC, nová logika primárně používá `datum_kroku`.

Doporučení: **A** — absolutní offset je jasnější pro posloupnost a UI. Migrace trivial: nová hodnota = CUMSUM(old). Validace "N ≥ N−1" je `offset[N] ≥ offset[N−1]`.

---

### Task 1: Spec document + rozhodnutí

**Files:**
- Create: `docs/superpowers/specs/2026-04-19-harmonogram-jen-datum-design.md`

- [ ] **Step 1: Sepsat spec**

Obsah:
1. Current state audit (Task 0)
2. Motivace (user report: "jak je tam trvání, tak to pořád blbne a nefunguje to správně, hlavně s ohledem na infobary zobrazující plán a skutečnost")
3. Rozhodnutí: schéma A/B/C (viz výše)
4. Validační pravidla: offset_krok_2 ≥ offset_krok_1 (ne striktně větší — stejný den OK)
5. Migrace dat: `UPDATE zaznam_harmonogram_hodnoty SET hodnota_int = SUM(...) OVER (PARTITION BY zaznam_id ORDER BY typ_id)` pokud volíme A s CUMSUM
6. Impact na InfoBars (plán/skutečnost): plán z `hodnota_int`, skutečnost z `zaznam_historie_terminu` nebo aktuálního stavu
7. UI změny: jedno datum pole per krok, chip pořadí, automatická validace posloupnosti
8. Test plán

- [ ] **Step 2: Commit spec**

```bash
git add docs/superpowers/specs/2026-04-19-harmonogram-jen-datum-design.md
git commit -m "docs(harmonogram): spec přechodu na jen-datum per krok"
```

---

### Task 2: DB migrace (rollback-safe)

**Files:**
- Create: `db_upgrade_1_1_8_harmonogram_date_offset.sql`

- [ ] **Step 1: Napsat SQL migraci**

```sql
-- Backup staré hodnoty
ALTER TABLE dbo.zaznam_harmonogram_hodnoty
    ADD hodnota_legacy_duration int NULL;

UPDATE dbo.zaznam_harmonogram_hodnoty
    SET hodnota_legacy_duration = hodnota_int;

-- Přepočet na absolutní offset (CUMSUM per zaznam_id podle krok_poradi)
;WITH cumsum AS (
    SELECT h.id,
           SUM(h.hodnota_int) OVER (
               PARTITION BY h.zaznam_id
               ORDER BY t.krok_poradi, t.je_zpozdeni
               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
           ) AS new_offset
    FROM dbo.zaznam_harmonogram_hodnoty h
    JOIN dbo.ciselnik_harmonogram_typu t ON t.id = h.typ_id
)
UPDATE h
    SET hodnota_int = c.new_offset
FROM dbo.zaznam_harmonogram_hodnoty h
JOIN cumsum c ON c.id = h.id;

-- CHECK constraint: offset >= 0
ALTER TABLE dbo.zaznam_harmonogram_hodnoty
    ADD CONSTRAINT CK_zaznam_harmonogram_hodnoty_offset_positive
        CHECK (hodnota_int >= 0);
```

- [ ] **Step 2: Test migrace proti e2e testcontaineru**

Run: `dotnet test PmTracker.Tests.E2E --filter "Harmonogram"` — musí projít.

- [ ] **Step 3: Commit migrace**

```bash
git add db_upgrade_1_1_8_harmonogram_date_offset.sql
git commit -m "db(harmonogram): migrace na absolutní offset (CUMSUM backfill)"
```

---

### Task 3: Backend — ScheduleCalculator reinterpretace

**Files:**
- Modify: `PmTracker.Web/Services/ScheduleCalculator.cs`
- Modify: `PmTracker.Web/Services/Records/RecordScheduleService.cs`

- [ ] **Step 1: Failing unit test**

```csharp
// PmTracker.Tests.Unit/Schedule/ScheduleCalculatorAbsoluteOffsetTests.cs
[Fact]
public void ComputeStepDate_AbsoluteOffset_ReturnsDatumZalozeniPlusOffsetDays()
{
    var calc = new ScheduleCalculator();
    var datumZalozeni = new DateTime(2026, 1, 1);
    var stepOffset = 14; // 14 dní od založení
    var result = calc.ComputeStepDate(datumZalozeni, stepOffset);
    result.Should().Be(new DateTime(2026, 1, 15));
}

[Fact]
public void ValidateStepSequence_LaterStepEarlierThanPrevious_ReturnsViolation()
{
    var calc = new ScheduleCalculator();
    var violations = calc.ValidateStepSequence(new[] { 10, 15, 12, 20 });
    violations.Should().ContainSingle(v => v.StepIndex == 2);
}
```

Run: `dotnet test PmTracker.Tests.Unit --filter "ScheduleCalculatorAbsoluteOffset"` → FAIL (metody neexistují).

- [ ] **Step 2: Implementovat ComputeStepDate + ValidateStepSequence**

```csharp
public DateTime ComputeStepDate(DateTime datumZalozeni, int offsetDays)
    => datumZalozeni.AddDays(offsetDays);

public IReadOnlyList<SequenceViolation> ValidateStepSequence(IReadOnlyList<int> offsetsInOrder)
{
    var violations = new List<SequenceViolation>();
    for (int i = 1; i < offsetsInOrder.Count; i++)
    {
        if (offsetsInOrder[i] < offsetsInOrder[i - 1])
        {
            violations.Add(new SequenceViolation(i, offsetsInOrder[i], offsetsInOrder[i - 1]));
        }
    }
    return violations;
}

public sealed record SequenceViolation(int StepIndex, int CurrentOffset, int PreviousOffset);
```

- [ ] **Step 3: Run test to verify pass**

Run: `dotnet test PmTracker.Tests.Unit --filter "ScheduleCalculatorAbsoluteOffset"` → PASS.

- [ ] **Step 4: Nahradit stará volání `CumulativeSum`/`ComputeDuration`**

Grep-and-replace: kdekoli se `hodnota_int` interpretuje jako duration, přepnout na offset.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Services/ScheduleCalculator.cs PmTracker.Tests.Unit/Schedule/ScheduleCalculatorAbsoluteOffsetTests.cs
git commit -m "feat(harmonogram): ScheduleCalculator absolute offset + sequence validation"
```

---

### Task 4: Server-side FluentValidation pro posloupnost

**Files:**
- Modify: `PmTracker.Web/Services/Validators/SaveRecordCommandValidator.cs` (nebo SchedulePlannerValidator, podle existující struktury)

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public async Task Validate_HarmonogramKrokEarlierThanPrevious_ReturnsError()
{
    var command = BuildSaveCommand(new[] { (Kod: "HS01_DURATION", Offset: 10), (Kod: "HS02_DURATION", Offset: 5) });
    var result = await _validator.ValidateAsync(command);
    result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("nesmí být dříve"));
}
```

- [ ] **Step 2: Implementace**

Přidat validation rule na posloupnost kroků, reuse `ScheduleCalculator.ValidateStepSequence`.

- [ ] **Step 3: Test pass + commit**

```bash
git commit -m "feat(harmonogram): FluentValidation posloupnost kroků"
```

---

### Task 5: UI — jeden datum input per krok

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_EditZaznamSchedulePanel.cshtml`
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule.js`

- [ ] **Step 1: Zjednodušit Razor markup**

Odstranit dvojité input (trvání + datum). Každý krok jen `<pm-date name="UiHarmonogramDatumy[{kod}]">`. Label ukazuje krok_poradi + nazev. Hidden offset input derived client-side (krok_datum - datumZalozeni → days).

- [ ] **Step 2: Client-side recalc (schedule.js)**

Při změně datumu kroku:
- Ověří offset ≥ offset předchozího kroku → show inline error "Musí být stejný nebo pozdější než krok N−1"
- Auto-posune navazující kroky pokud by byly dřív? Nebo nechat user opravit → **nechat user opravit** (business: "hlídat aby měly kroky správnou časovou posloupnost, jen jednoduše")
- Přepočítá hidden offset input

- [ ] **Step 3: Odstranit `double okénko kde je trvání a datum`**

Grep: `data-schedule-duration-input`, `data-schedule-date-input` — odstranit všude kromě migrace.

- [ ] **Step 4: Playwright test**

```js
// /tmp/playwright-test-harmonogram-date-only.js
// Otevři modal editace, změň datum kroku 3 na dřívější než datum kroku 2,
// assert inline error "Krok 3 nesmí být dříve než krok 2".
```

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(harmonogram): UI přechod na jen-datum per krok, bez dvojitého inputu"
```

---

### Task 6: InfoBars (plán/skutečnost) — přepočítat z offsetů

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamInfoBar*.cshtml` (pokud exist)
- Modify: `PmTracker.Web/wwwroot/js/modules/schedule.js` — infoBar render

- [ ] **Step 1: Přečíst současnou implementaci InfoBars**

Grep `infoBar`, `rainbowSegment` — zdokumentovat jak se renderuje plán vs. skutečnost.

- [ ] **Step 2: Plán = sekvence offsetů → dat. Skutečnost = historie termínů / aktuální hodnoty**

Update renderer aby používal DatumZalozeni + offset místo CUMSUM durations.

- [ ] **Step 3: Playwright smoke test**

Verify že infoBar ukazuje správné plán/skutečnost pro jeden seed-vytvořený záznam.

- [ ] **Step 4: Commit**

```bash
git commit -m "fix(harmonogram): InfoBars plán/skutečnost z absolutních offsetů"
```

---

### Task 7: E2E test kompletního flow

**Files:**
- Create: `PmTracker.Tests.E2E/Scenarios/HarmonogramDateOnlyScenariosTests.cs`

- [ ] **Step 1: Scénář**

1. Vytvoř záznam úkolového typu přes UI
2. Otevři záložku harmonogram
3. Vyplň kroky 1-5 s rostoucími daty
4. Ulož, verify že se uloží
5. Otevři znovu, změň krok 3 na datum dřív než 2 → assert validation error
6. Oprav → Save success

- [ ] **Step 2: Commit**

```bash
git commit -m "test(harmonogram): E2E happy path + sequence validation"
```

---

### Task 8: Dokumentace + cleanup

**Files:**
- Modify: `docs/architecture/schedule.md` (pokud existuje, jinak nezakládat)
- Modify: `PMTracker_insert_sql` nebo `db_seed_dev_admin.sql` pokud seed obsahuje ukázkový harmonogram s durations — přepsat na offsety

- [ ] **Step 1: Update seed data**

Seed má harmonogram_sablony záznamy (HS01_DURATION `hodnota=1`, HS02_DURATION `hodnota=2`, atd.). Tyto ZNAMENAJÍ TRVÁNÍ referenční šablony — **NEZMĚNIT** (`ciselnik_harmonogram_typu.hodnota` je zamknutá baseline duration). Pouze `zaznam_harmonogram_hodnoty.hodnota_int` se reinterpretuje jako offset.

Ověř že migrace (Task 2) se pouští i na test containeru.

- [ ] **Step 2: Changelog entry**

Přidat do `CHANGELOG.md` nebo `docs/technical/CHANGELOG.md` (pokud existuje):

```markdown
## [Unreleased]
### Changed
- Harmonogram: přechod z "trvání + datum" dvojitého inputu na jen datum per krok.
  Posloupnost kroků je validovaná (datum_N ≥ datum_N-1). InfoBars plán/skutečnost
  přepočítány z absolutních offsetů.
```

- [ ] **Step 3: Final commit + PR summary**

```bash
git commit -m "chore(harmonogram): dokumentace + seed sanity check"
```

---

## Self-review

- [ ] Všechny kroky mají konkrétní souborové cesty, žádné placeholder "TBD"
- [ ] FluentValidation rule + server test před UI změnou
- [ ] Migrace má rollback path (hodnota_legacy_duration sloupec)
- [ ] E2E test pokrývá happy path i validation failure
- [ ] Žádná změna `ciselnik_harmonogram_typu.hodnota` (baseline template stays)
- [ ] Spec má vysvětlení business pravidla (user: "jen jednoduše hlídat posloupnost")

## Execution Handoff

Plán kompletní. Dvě možnosti spuštění:

1. **Subagent-Driven (recommended)** — fresh subagent per task + two-stage review.
2. **Inline Execution** — batch execution v aktuální session.

Volba: při přechodu z této session na novou spustit `superpowers:subagent-driven-development`.
