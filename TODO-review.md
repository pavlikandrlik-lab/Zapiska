# Review nálezů — UI audit commit `60e290d`

Zjištěno po code review. Seřazeno dle závažnosti.

---

## 🔴 Bugy (blokující)

### 1. `IsAddOnlyMode` se nekontroluje v `_ScheduleBlock.cshtml` — regrese

**Soubor**: `PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml`, sekce `canEditDuration`

Stará logika správně omezovala editaci na kroky s `TrvaniDni == 0`:
```razor
(Model.EditorCanEditScheduleAddOnly && krok.TrvaniDni <= 0)
```

Po refaktoru na `ScheduleEditorPermissionSet` se `IsAddOnlyMode` nikde v Razor souboru nepoužívá.
Výsledek: v add-only módu jsou editovatelné **všechny** kroky místo jen prázdných.

**Fix** — doplnit do podmínky `canEditDuration`:
```razor
var canEditDuration = !Model.Permissions.IsPlanLocked
    && !Model.Permissions.IsScheduleLocked
    && Model.Permissions.CanEditDuration
    && (!Model.Permissions.IsAddOnlyMode || krok.TrvaniDni <= 0);
```

---

### 2. `SchedulePreviewService.cs` — `ToDictionary` hází `ArgumentException` při duplicitních klíčích

**Soubor**: `PmTracker.Web/Services/Schedules/SchedulePreviewService.cs`

```csharp
var values = request.Steps
    .SelectMany(s => new[]
    {
        (s.DurationTypeId, s.DurationDays),
        (s.DelayTypeId, s.DelayDays)
    })
    .Where(x => x.Item1 > 0)
    .ToDictionary(x => x.Item1, x => x.Item2);  // ← crash při duplicate key
```

Pokud mají dva kroky stejný `DurationTypeId`, nebo `DurationTypeId` jednoho == `DelayTypeId` jiného →
`System.ArgumentException: An item with the same key has already been added`.

**Fix**:
```csharp
.GroupBy(x => x.Item1)
.ToDictionary(g => g.Key, g => g.First().Item2);
```

---

### 3. `recalcAll` — double debounce (300 ms celkem) + promise leak

**Soubor**: `PmTracker.Web/wwwroot/js/modules/schedule.js`

Problém 1 — double debounce: input event listener má 150 ms debounce → `recalcFromDuration` →
`recalcAll` má další interní 150 ms debounce. Celková prodleva ≈ **300 ms**.

Problém 2 — promise leak: při rychlém psaní `clearTimeout` zruší timer, ale předchozí
`await new Promise(...)` se nikdy nevyřeší → každé volání zanechá unresolved async invokaci
v paměti až do GC.

**Fix** — debounce patří pouze do input event listeneru, `recalcAll` nesmí obsahovat vlastní debounce:
```js
// V bindEditorInputs — zachovat stávající 150ms debounce
entry.durationInput.addEventListener("input", () => {
    clearTimeout(durationDebounceTimer);
    durationDebounceTimer = setTimeout(() => this.recalcFromDuration(index), 150);
});

// recalcAll — odstranit interní debounce (setTimeout + await Promise blok)
async recalcAll() {
    const state = this.readState();
    if (state.length === 0) return;
    const startDate = this.getStartDate();
    const deadlineDate = this.getDeadlineDate(startDate);
    // ... přímo token + fetch + fallback, bez _recalcDebounceTimer
}
```

---

## 🟡 Střední nálezy

### 4. `ScheduleVersion` se nepřenáší v `CloneScheduleBlock` (`RecordProposalService`)

**Soubor**: `PmTracker.Web/Services/RecordProposalService.cs`, metoda `CloneScheduleBlock`

`ScheduleVersion` není součástí klonování → při approve/reject návrhu verze zmizí a F-11
concurrency check nepomůže. Záměrné pouze pokud proposal flow záměrně vynechává tento check.

**Fix** (pokud má proposal flow concurrency check zachovat):
```csharp
ScheduleVersion = source.ScheduleVersion
```

---

### 5. `ForActivePlanProposal` je mrtvý kód

**Soubor**: `PmTracker.Web/Services/Schedules/ScheduleEditorPermissionSet.cs`

Factory metoda `ForActivePlanProposal` je definována, ale nikde se nevolá
(`ProjectService.RecordEditorComposition.cs` používá pouze `ForActiveScheduleProposal` / `ForFullEdit`).
Buď doplnit volání tam, kde existuje samostatný lock na plánovou část,
nebo metodu odstranit.

---

### 6. `_ZaznamDetailPartial.cshtml` — podmíněný label „Typ úkolu" chybí

**Plán sekce D** obsahoval: zobrazit label „Typ úkolu:" jen pokud `Kategorie.Kod == "UKOL"`.
Soubor `_ZaznamDetailPartial.cshtml` není v commitu. Terminologický audit je tím neúplný.

---

## 🟢 Drobnosti

### 7. TODO komentáře pro F-06 a F-19 bez implementace

**Soubor**: `PmTracker.Web/Services/Data/HarmonogramService.cs`

3× `// TODO: Add logger.LogWarning when ILogger is injected` — buď přidat
`ILogger<HarmonogramService>` do konstruktoru a logování implementovat,
nebo TODO odstranit (tiché fallbacky jsou pak záměrné).

---

### 8. DTOs smíchané se service třídou v jednom souboru

**Soubor**: `PmTracker.Web/Services/Schedules/SchedulePreviewService.cs`

6 DTO tříd + service třída v 98 řádcích. Při dalším rozšíření zvážit oddělení do
`SchedulePreviewModels.cs`.

---

*Review provedeno: 2026-04-16*
