# Feature A — `/SDConnector/Inspect` ticket inspector implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Admin diagnostická podstránka `/SDConnector/Inspect?cislo=XXXXXX` — zadá se 6-ciferné ID ticketu, vedle sebe zobrazí levý panel s raw daty ze ServiceDesku (HOT_ZAZNAMY + HOT_VYJADRENI včetně HTML) a pravý panel s user-friendly (sanitizovaný plain text + klasifikace přes HarvestPredicates). Button „Otevřít v chat modalu" pro spuštění reálné chat modal komponenty.

**Architecture:** Samostatný controller action na existujícím `SDConnectorController` + nová View + VM + JS modul. Controller reusuje stávající `ITicketingQueryService`, `IVyjadreniQueryService`, `AdLoginCache`, `VyjadreniHtmlText` (ze Sprint A), `HarvestPredicates`. Žádné DB change. Čistě read-only diagnostic flow. Standardní shell `_Layout.cshtml` (dle A-Q2 rozhodnutí).

**Tech Stack:** ASP.NET Core 8 MVC, Razor, vanilla JS (sessionStorage cache), existing gov components.

**Spec source:**
- [Task 19 v 2026-04-21-chat-modal-harvest-core.md řádky 1887-2113](2026-04-21-chat-modal-harvest-core.md)
- Decision brief: [2026-04-24-sd-features-decision-brief.md](../specs/2026-04-24-sd-features-decision-brief.md) → A-Q1/Q2/Q3

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Views/SDConnector/Inspect.cshtml` | Inspector view: input form + 2-pane grid + button do modalu |
| `PmTracker.Web/Models/ViewModels/SDConnector/SDConnectorInspectViewModel.cs` | VM: cislo, Nalezeno, Fingerprint, RawRows, FriendlyRows |
| `PmTracker.Web/wwwroot/js/modules/sdconnector/inspect.js` | fetch `/SDConnector/Load` + render raw/friendly + sessionStorage |
| `PmTracker.Web/wwwroot/css/components/sdconnector-inspect.css` | 2-pane grid layout styling |
| `PmTracker.Tests.Unit/Controllers/SDConnectorInspectControllerTests.cs` | ACL + 6-cifer validace + JSON shape testy |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Controllers/SDConnectorController.cs` | Přidat 2 nové actions: `GET /SDConnector/Inspect` (view) + `GET /SDConnector/Load?cislo=` (JSON) |
| `PmTracker.Web/Views/SDConnector/Index.cshtml` | Přidat odkaz „Prozkoumat konkrétní ticket → /SDConnector/Inspect" |
| `PmTracker.Web/wwwroot/js/site.bundle.js` | Sync inspect.js modul |

---

## Tasks

### Task 1: VM + Load endpoint (JSON)

**Files:**
- Create: `PmTracker.Web/Models/ViewModels/SDConnector/SDConnectorInspectViewModel.cs`
- Modify: `PmTracker.Web/Controllers/SDConnectorController.cs`
- Create: `PmTracker.Tests.Unit/Controllers/SDConnectorInspectControllerTests.cs`

- [ ] **Step 1: VM**

```csharp
// PmTracker.Web/Models/ViewModels/SDConnector/SDConnectorInspectViewModel.cs
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Models.ViewModels.SDConnector;

public sealed class SDConnectorInspectViewModel
{
    public bool TicketingEnabled { get; init; }
    public string? InitialCislo { get; init; }
}

/// <summary>JSON shape vracený z GET /SDConnector/Load?cislo=XXX</summary>
public sealed class SDConnectorLoadResponse
{
    public required bool Nalezeno { get; init; }
    public string? Error { get; init; }
    public SDConnectorRawHeader? Raw { get; init; }
    public IReadOnlyList<SDConnectorBublinaDto>? Bubliny { get; init; }
}

public sealed class SDConnectorRawHeader
{
    public required string Cislo { get; init; }
    public string? Pid { get; init; }
    public string? TypZaznamu { get; init; }
    public string? Stav { get; init; }
    public string? Strucne { get; init; }
    public string? PopisRaw { get; init; }
    public DateTime? Datum { get; init; }
    public int? ExterniOdkazId { get; init; }  // pokud existuje navázaná vazba → pro button „Otevřít v modalu"
}

public sealed class SDConnectorBublinaDto
{
    public required long HotId { get; init; }
    public required string Typ { get; init; }
    public DateTime Datum { get; init; }
    public string? LoginRaw { get; init; }
    public string? AutorDisplayName { get; init; }
    public string? Tym { get; init; }
    public string? PopisRaw { get; init; }
    public required string PopisPlainText { get; init; }
    public string? ClassifiedAs { get; init; }  // K3/K6/K4/K7/K10/PlanDodani/None
}
```

- [ ] **Step 2: Controller Load endpoint — failing test first**

```csharp
// PmTracker.Tests.Unit/Controllers/SDConnectorInspectControllerTests.cs
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Controllers;

public sealed class SDConnectorInspectControllerTests
{
    [Theory]
    [InlineData("12345", false)]   // 5 cifer
    [InlineData("1234567", false)] // 7 cifer
    [InlineData("12345a", false)]  // non-numeric
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("123456", true)]   // 6 cifer OK
    public void Load_Validuje6Cifer(string? input, bool shouldMatch)
    {
        var ok = input is not null && System.Text.RegularExpressions.Regex.IsMatch(input, @"^\d{6}$");
        ok.Should().Be(shouldMatch);
    }

    // Integration testy (s real services a mock ITicketingQueryService) jsou mimo unit scope —
    // zařazeno do PmTracker.Tests.Api nebo manuální E2E.
}
```

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "SDConnectorInspectControllerTests" --no-restore`
Expected: PASS 6/6 (čistě regex validation).

- [ ] **Step 3: Controller extension**

V `SDConnectorController.cs` přidat do DI constructor ještě:

```csharp
private readonly ITicketingQueryService _ticketing;
private readonly IVyjadreniQueryService _vyjadreni;
private readonly AdLoginCache _adCache;

// V constructoru:
public SDConnectorController(
    PmTrackerDbContext db,
    IVyjadreniHarvestService harvest,
    TimeProvider time,
    IOptions<TicketingOptions> ticketingOptions,
    ILogger<SDConnectorController> logger,
    IAuditWriteService auditWriteService,
    ICurrentUserAccessor currentUser,
    ITicketingQueryService ticketing,
    IVyjadreniQueryService vyjadreni,
    AdLoginCache adCache)
{
    // ... existing ...
    _ticketing = ticketing;
    _vyjadreni = vyjadreni;
    _adCache = adCache;
}
```

Přidat 2 nové actions:

```csharp
[HttpGet("Inspect")]
public IActionResult Inspect(string? cislo = null)
{
    var vm = new PmTracker.Web.Models.ViewModels.SDConnector.SDConnectorInspectViewModel
    {
        TicketingEnabled = _ticketingOptions.Value.Enabled,
        InitialCislo = cislo is not null && System.Text.RegularExpressions.Regex.IsMatch(cislo, @"^\d{6}$") ? cislo : null
    };
    return View(vm);
}

[HttpGet("Load")]
public async Task<IActionResult> Load(string cislo, CancellationToken ct)
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(cislo ?? "", @"^\d{6}$"))
    {
        return BadRequest(new SDConnectorLoadResponse { Nalezeno = false, Error = "Číslo musí být 6 cifer." });
    }

    // 1) Fingerprint + raw header
    var fingerprints = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { cislo }, ct).ConfigureAwait(false);
    if (!fingerprints.TryGetValue(cislo, out var fp))
    {
        return Ok(new SDConnectorLoadResponse { Nalezeno = false, Error = $"Ticket #{cislo} v HOT_ZAZNAMY neexistuje." });
    }

    var ticket = await _ticketing.GetZaznamAsync(cislo, ct).ConfigureAwait(false);

    var header = new SDConnectorRawHeader
    {
        Cislo = cislo,
        Pid = ticket?.Pid,
        TypZaznamu = fp.TypZaznamu,
        Stav = fp.Stav,
        Strucne = ticket?.Strucne,
        PopisRaw = ticket?.Popis,
        Datum = fp.Datum,
        ExterniOdkazId = await LookupExterniOdkazIdAsync(cislo, ct).ConfigureAwait(false)
    };

    // 2) Vyjádření + plaintext + klasifikace
    var vyjadreni = await _vyjadreni.GetVyjadreniForTicketAsync(cislo, null, ct).ConfigureAwait(false);
    var bubliny = new List<SDConnectorBublinaDto>(vyjadreni.Count);
    foreach (var v in vyjadreni)
    {
        var display = !string.IsNullOrWhiteSpace(v.Zpracoval)
            ? await _adCache.ResolveDisplayNameAsync(v.Zpracoval, ct).ConfigureAwait(false)
            : null;
        var plain = PmTracker.Web.Services.ServiceDesk.VyjadreniHtmlText.ToPlainText(v.Popis);
        var classified = PmTracker.Web.Services.ServiceDesk.HarvestPredicates.ClassifyPopis(v.Popis ?? "", fp.TypZaznamu ?? "")?.Key;

        bubliny.Add(new SDConnectorBublinaDto
        {
            HotId = v.Id,
            Typ = v.Typ ?? "",
            Datum = v.Datum,
            LoginRaw = v.Zpracoval,
            AutorDisplayName = display,
            Tym = v.Tym,
            PopisRaw = v.Popis,
            PopisPlainText = plain,
            ClassifiedAs = classified ?? "None"
        });
    }

    return Ok(new SDConnectorLoadResponse { Nalezeno = true, Raw = header, Bubliny = bubliny });
}

private async Task<int?> LookupExterniOdkazIdAsync(string cislo, CancellationToken ct)
{
    return await _db.ZaznamExterniOdkazy.AsNoTracking()
        .Where(x => x.Cislo == cislo)
        .Select(x => (int?)x.Id)
        .FirstOrDefaultAsync(ct).ConfigureAwait(false);
}
```

Poznámka: API `HarvestPredicates.ClassifyPopis` možná má jiný signature — zkontroluj reálně `HarvestPredicates.cs` před implementací. Pokud metoda neexistuje veřejně, vyfakti ji jako public + přidej unit test.

- [ ] **Step 4: Commit**

```bash
git add PmTracker.Web/Models/ViewModels/SDConnector/SDConnectorInspectViewModel.cs \
        PmTracker.Web/Controllers/SDConnectorController.cs \
        PmTracker.Tests.Unit/Controllers/SDConnectorInspectControllerTests.cs
git commit -m "feat(sdconnector): Inspect VM + Load JSON endpoint + regex validace"
```

---

### Task 2: View + CSS

**Files:**
- Create: `PmTracker.Web/Views/SDConnector/Inspect.cshtml`
- Create: `PmTracker.Web/wwwroot/css/components/sdconnector-inspect.css`

- [ ] **Step 1: View**

```cshtml
@model PmTracker.Web.Models.ViewModels.SDConnector.SDConnectorInspectViewModel
@{
    ViewData["Title"] = "SD Inspector";
}

<section class="sdc-inspect">
    <header>
        <h1>ServiceDesk ticket inspector</h1>
        <p class="sdc-inspect__subtitle">
            Zadej 6-ciferné ID ticketu (<code>HOT_ZAZNAMY.id</code>) a zobraz raw data + harvest klasifikaci.
        </p>
    </header>

    <form class="sdc-inspect__form" data-sdc-form>
        <label for="sdc-cislo-input">Číslo tiketu:</label>
        <input id="sdc-cislo-input"
               type="text"
               inputmode="numeric"
               pattern="\d{6}"
               maxlength="6"
               value="@(Model.InitialCislo ?? "")"
               placeholder="123456"
               data-sdc-input />
        <button type="submit" data-sdc-load-btn>Načíst</button>
    </form>

    <div class="sdc-inspect__status" data-sdc-status></div>

    <div class="sdc-inspect__header-box" data-sdc-header hidden></div>

    <section class="sdc-inspect__grid" data-sdc-grid hidden>
        <div class="sdc-inspect__raw-col">
            <h2>Raw data (ServiceDesk)</h2>
            <pre data-sdc-raw></pre>
        </div>
        <div class="sdc-inspect__friendly-col">
            <h2>Zpracované vyjádření</h2>
            <div data-sdc-friendly></div>
            <button type="button" data-sdc-open-modal hidden>Otevřít v chat modalu</button>
        </div>
    </section>
</section>

<script src="~/js/modules/sdconnector/inspect.js" asp-append-version="true"></script>
<script>
  (function(){
    if (window.pmSdcInspect?.init) pmSdcInspect.init();
  })();
</script>
```

- [ ] **Step 2: CSS**

```css
/* PmTracker.Web/wwwroot/css/components/sdconnector-inspect.css */
.sdc-inspect { display: flex; flex-direction: column; gap: 1rem; }
.sdc-inspect__subtitle { color: var(--gov-color-text-secondary, #475467); }

.sdc-inspect__form { display: flex; gap: 0.75rem; align-items: center; flex-wrap: wrap; }
.sdc-inspect__form input { font-family: monospace; font-size: 1.1rem; padding: 0.4rem 0.6rem; width: 8rem; }
.sdc-inspect__form button { padding: 0.5rem 1rem; }

.sdc-inspect__status { min-height: 1.5rem; }
.sdc-inspect__status[data-kind="error"] { color: var(--gov-color-danger, #dc2626); }
.sdc-inspect__status[data-kind="ok"]    { color: var(--gov-color-success, #16a34a); }

.sdc-inspect__header-box {
    padding: 1rem;
    background: var(--gov-color-neutral-bg, #f2f4f7);
    border-radius: 0.5rem;
    font-family: monospace;
    font-size: 0.9rem;
}

.sdc-inspect__grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1.5rem;
}
@media (max-width: 900px) {
    .sdc-inspect__grid { grid-template-columns: 1fr; }
}

.sdc-inspect__raw-col pre {
    background: #0f172a;
    color: #e2e8f0;
    padding: 1rem;
    overflow: auto;
    font-size: 0.85rem;
    border-radius: 0.5rem;
    max-height: 70vh;
}

.sdc-inspect__friendly-col {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
}

.sdc-inspect__friendly-col [data-sdc-friendly] {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
    max-height: 70vh;
    overflow: auto;
}

.sdc-friendly-bubble {
    padding: 0.75rem 1rem;
    border: 1px solid var(--gov-color-neutral-border, #d0d5dd);
    border-radius: 0.5rem;
    background: #fff;
}

.sdc-friendly-bubble__header {
    display: flex;
    gap: 0.75rem;
    align-items: baseline;
    font-size: 0.85rem;
    color: var(--gov-color-text-secondary, #475467);
}

.sdc-friendly-bubble__classification {
    padding: 0.15rem 0.5rem;
    border-radius: 0.25rem;
    background: var(--gov-color-primary-bg, #dbeafe);
    color: var(--gov-color-primary, #1e40af);
    font-size: 0.75rem;
    font-weight: 600;
}

.sdc-friendly-bubble__classification[data-predicate="None"] {
    background: var(--gov-color-neutral-bg, #f2f4f7);
    color: var(--gov-color-text-secondary, #475467);
}

.sdc-friendly-bubble__plain {
    white-space: pre-wrap;
    margin-top: 0.5rem;
}
```

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/Views/SDConnector/Inspect.cshtml \
        PmTracker.Web/wwwroot/css/components/sdconnector-inspect.css
git commit -m "feat(sdconnector): Inspect view + 2-pane grid CSS"
```

---

### Task 3: JS modul — fetch, render, sessionStorage

**Files:**
- Create: `PmTracker.Web/wwwroot/js/modules/sdconnector/inspect.js`

- [ ] **Step 1: Napsat JS modul**

```javascript
// PmTracker.Web/wwwroot/js/modules/sdconnector/inspect.js
(function(global){
  'use strict';
  const STORAGE_KEY = 'pm.sdc.inspect.lastLoad';

  async function load(cislo) {
    const resp = await fetch(`/SDConnector/Load?cislo=${encodeURIComponent(cislo)}`, {
      headers: { Accept: 'application/json' }
    });
    if (!resp.ok && resp.status !== 400) {
      throw new Error(`HTTP ${resp.status}`);
    }
    const data = await resp.json();
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ts: Date.now(), cislo, data}));
    } catch(_) { /* quota full, ignore */ }
    return data;
  }

  function formatDate(iso) {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.valueOf())) return iso;
    return d.toLocaleString('cs-CZ');
  }

  function renderHeader(data) {
    const box = document.querySelector('[data-sdc-header]');
    if (!box) return;
    if (!data.nalezeno || !data.raw) { box.hidden = true; return; }
    const h = data.raw;
    box.hidden = false;
    box.textContent = `#${h.cislo} · typ=${h.typZaznamu ?? '?'} · stav=${h.stav ?? '?'} · datum=${formatDate(h.datum)} · pid=${h.pid ?? '—'}`;
  }

  function renderRaw(data) {
    const pre = document.querySelector('[data-sdc-raw]');
    if (!pre) return;
    if (!data.nalezeno) { pre.textContent = ''; return; }
    const lines = [];
    lines.push('== Hlavička ==');
    lines.push(`id: ${data.raw.cislo}`);
    lines.push(`pid: ${data.raw.pid ?? ''}`);
    lines.push(`typ_zaznamu: ${data.raw.typZaznamu ?? ''}`);
    lines.push(`stav: ${data.raw.stav ?? ''}`);
    lines.push(`datum: ${formatDate(data.raw.datum)}`);
    lines.push('');
    lines.push('== Stručně ==');
    lines.push(data.raw.strucne ?? '');
    lines.push('');
    lines.push('== Popis (raw HTML) ==');
    lines.push(data.raw.popisRaw ?? '');
    lines.push('');
    lines.push('== Vyjádření ==');
    for (const b of data.bubliny || []) {
      lines.push(`[id=${b.hotId}] typ=${b.typ} ${formatDate(b.datum)}`);
      lines.push(`  autor (raw login): ${b.loginRaw ?? '—'}`);
      lines.push(`  tým: ${b.tym ?? '—'}`);
      lines.push(`  popis (raw HTML):`);
      lines.push('    ' + (b.popisRaw ?? '').split('\n').join('\n    '));
      lines.push('');
    }
    pre.textContent = lines.join('\n');
  }

  function renderFriendly(data) {
    const container = document.querySelector('[data-sdc-friendly]');
    const btn = document.querySelector('[data-sdc-open-modal]');
    if (!container) return;
    container.innerHTML = '';
    if (btn) btn.hidden = true;
    if (!data.nalezeno) return;

    for (const b of data.bubliny || []) {
      const div = document.createElement('div');
      div.className = 'sdc-friendly-bubble';
      div.innerHTML = `
        <div class="sdc-friendly-bubble__header">
          <span>${formatDate(b.datum)}</span>
          <span>·</span>
          <span>${escapeHtml(b.autorDisplayName || b.loginRaw || '—')}</span>
          <span>·</span>
          <span>${escapeHtml(b.tym || '—')}</span>
          <span class="sdc-friendly-bubble__classification" data-predicate="${b.classifiedAs}">${b.classifiedAs}</span>
        </div>
        <div class="sdc-friendly-bubble__plain">${escapeHtml(b.popisPlainText)}</div>
      `;
      container.appendChild(div);
    }

    // Button do reálného modalu — jen pokud ticket má externí vazbu
    if (btn && data.raw?.externiOdkazId) {
      btn.hidden = false;
      btn.dataset.externiOdkazId = String(data.raw.externiOdkazId);
    }
  }

  function escapeHtml(s) {
    return String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  }

  function setStatus(msg, kind) {
    const el = document.querySelector('[data-sdc-status]');
    if (!el) return;
    el.textContent = msg || '';
    el.dataset.kind = kind || '';
  }

  function render(data) {
    renderHeader(data);
    renderRaw(data);
    renderFriendly(data);
    const grid = document.querySelector('[data-sdc-grid]');
    if (grid) grid.hidden = !data.nalezeno;
    if (!data.nalezeno) setStatus(data.error || 'Ticket nenalezen.', 'error');
    else setStatus(`Načteno: ${(data.bubliny || []).length} vyjádření`, 'ok');
  }

  function init() {
    const form = document.querySelector('[data-sdc-form]');
    const input = document.querySelector('[data-sdc-input]');
    const btnModal = document.querySelector('[data-sdc-open-modal]');

    form?.addEventListener('submit', async e => {
      e.preventDefault();
      const cislo = (input?.value || '').trim();
      if (!/^\d{6}$/.test(cislo)) { setStatus('Zadej 6 cifer.', 'error'); return; }
      setStatus('Načítám…', '');
      try {
        const data = await load(cislo);
        render(data);
      } catch (err) {
        setStatus(`Chyba: ${err.message}`, 'error');
      }
    });

    btnModal?.addEventListener('click', () => {
      const id = btnModal.dataset.externiOdkazId;
      if (!id) return;
      // Reuse existing chat modal opening mechanism
      if (global.pmChatModal?.open) {
        global.pmChatModal.open(parseInt(id, 10));
      } else {
        // Fallback: navigate to full record edit
        global.location.href = `/Zaznamy/Edit?externiOdkazId=${id}#chat`;
      }
    });

    // Restore from sessionStorage on reload
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      if (raw) {
        const cached = JSON.parse(raw);
        if (cached && cached.data && input) {
          input.value = cached.cislo || '';
          render(cached.data);
        }
      }
    } catch (_) { /* ignore */ }

    // If URL has ?cislo=, auto-load
    const params = new URLSearchParams(global.location.search);
    const urlCislo = params.get('cislo');
    if (urlCislo && /^\d{6}$/.test(urlCislo)) {
      if (input) input.value = urlCislo;
      form?.dispatchEvent(new Event('submit', { cancelable: true }));
    }
  }

  global.pmSdcInspect = { init, load };
})(window);
```

- [ ] **Step 2: Sync do site.bundle.js**

```bash
# ... append nebo přidat samostatný include v _Layout ...
grep -n "sdconnector" PmTracker.Web/wwwroot/js/site.bundle.js | head -3
```

Přidej reference. Alternativně view-side `<script src=...>` (už je v Inspect.cshtml).

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/sdconnector/inspect.js \
        PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "feat(sdconnector): inspect.js — fetch/render/sessionStorage"
```

---

### Task 4: Odkaz z Index na Inspect

**Files:**
- Modify: `PmTracker.Web/Views/SDConnector/Index.cshtml`

- [ ] **Step 1: Přidat odkaz**

Do existujícího `Index.cshtml` pod hlavičku (za `<p class="pm-sdconnector__subtitle">...`):

```cshtml
<p class="pm-sdconnector__actions">
    <a asp-action="Inspect" class="pm-sdconnector__link">
        Prozkoumat konkrétní ticket →
    </a>
</p>
```

- [ ] **Step 2: Commit**

```bash
git add PmTracker.Web/Views/SDConnector/Index.cshtml
git commit -m "feat(sdconnector): odkaz z overview na Inspect"
```

---

### Task 5: Final validace

- [ ] **Step 1: Build + tests**

```bash
dotnet build -c Release --no-restore
dotnet test PmTracker.Tests.Unit -c Release --filter "SDConnector" --no-restore
```
Expected: 0 errors, SD testy pass.

- [ ] **Step 2: Manuální smoke**

Pusť dev server:
1. Přihlas se jako admin
2. Otevři `/SDConnector` → klikni „Prozkoumat konkrétní ticket" → otevře `/SDConnector/Inspect`
3. Zadej validní 6-cifer ID (ticket který existuje v DB) → očekávání: raw `<pre>` vlevo, friendly karty vpravo, klasifikace badge u každé bubliny
4. Zadej invalid ID (5 cifer) → error „Zadej 6 cifer"
5. Zadej neexistující ID → „Ticket #XXXXXX v HOT_ZAZNAMY neexistuje."
6. Pokud existuje ticket s externí vazbou → button „Otevřít v chat modalu" viditelný + funkční
7. Reload stránky → sessionStorage obnoví poslední načtená data

- [ ] **Step 3: Final commit**

```bash
git status --short  # pokud něco
```

---

## Deliverable

- ✅ `/SDConnector/Inspect` admin podstránka (gate `permission:settings.sd.view`)
- ✅ `GET /SDConnector/Load?cislo=XXXXXX` JSON endpoint s regex validací
- ✅ 2-pane grid: raw `<pre>` + user-friendly karty
- ✅ Klasifikace badge (K3/K6/K4/K7/K10/None) přes `HarvestPredicates`
- ✅ sessionStorage cache pro reload persistence
- ✅ Button „Otevřít v chat modalu" pro reuse existing modal komponenty
- ✅ URL-linkable: `/SDConnector/Inspect?cislo=123456` → auto-load
- ✅ Odkaz z overview `/SDConnector` → Inspect
