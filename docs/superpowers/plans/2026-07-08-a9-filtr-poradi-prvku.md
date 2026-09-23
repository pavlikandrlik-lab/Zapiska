# A9 — Filtr: prohodit „Pouze aktivní záznamy" ↔ „Jednání-vyjádření" — Implementation Plan

> **For agentic workers:** Exekuce INLINE. **Commity DRŽET.**

**Goal:** V `.filter-grid` je select „Jednání-vyjádření" před přepínačem „Pouze aktivní záznamy".

**Architecture:** Čistá výměna pořadí dvou sousedních bloků markupu. JS váže přes `data-filter-key`, zarovnávací CSS přes třídu — pozice nehraje roli.

## Global Constraints
- Žádné jiné změny filtru; commity držené.

---

### Task 1: Test + swap

**Files:**
- Test: `PmTracker.Tests.Unit/Architecture/ProjectFilterShellTests.cs` (modify — přidat test; soubor existuje)
- Modify: `PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml` (bloky ~ř. 82–94)

- [ ] **Step 1: Failing test** — do existující třídy `ProjectFilterShellTests` přidat (helper `Read`/`RepoRoot` už tam je — použít stávající):

```csharp
    [Fact]
    public void FilterGrid_JednaniVyjadreni_PrecedesAktivniSwitch()
    {
        var src = Read("PmTracker.Web/Views/Projekty/_ProjectFilterShell.cshtml");
        var jednani = src.IndexOf("data-filter-key=\"jednani-vyjadreni-stav\"", StringComparison.Ordinal);
        var aktivni = src.IndexOf("data-filter-key=\"aktivni\"", StringComparison.Ordinal);
        jednani.Should().BeGreaterThan(-1);
        aktivni.Should().BeGreaterThan(-1);
        jednani.Should().BeLessThan(aktivni, "user 2026-07-08: select Jednání-vyjádření před přepínačem Pouze aktivní");
    }
```

(Pokud existující třída používá jiné jméno helperu než `Read`, převzít její konvenci.)

- [ ] **Step 2: Run — FAIL** (`--filter "FullyQualifiedName~ProjectFilterShellTests.FilterGrid_JednaniVyjadreni"`)

- [ ] **Step 3: Swap markupu** — v `_ProjectFilterShell.cshtml` přesunout blok:

```razor
            <label>
                Jednání-vyjádření
                <select data-filter-key="jednani-vyjadreni-stav">
                    <option value="">Vše</option>
                    @foreach (var item in Model.StavyJednaniVyjadreni)
                    {
                        <option value="@item.Value">@item.Label</option>
                    }
                </select>
            </label>
```
PŘED blok:
```razor
            <gov-form-switch size="m" data-filter-key="aktivni" checked>
                <span slot="label"><label>Pouze aktivní záznamy</label></span>
            </gov-form-switch>
```
(výsledné pořadí: … Vlastník → Jednání-vyjádření → Pouze aktivní záznamy).

- [ ] **Step 4: Run — PASS** + celé `ProjectFilterShellTests` zelené.

- [ ] **Step 5: Vizuální kontrola** — rebuild + Playwright screenshot otevřeného filter panelu (1440×900): pořadí správně, přepínač stále svisle zarovnaný se sousedem (translateY pravidlo). Screenshot do reportu.
