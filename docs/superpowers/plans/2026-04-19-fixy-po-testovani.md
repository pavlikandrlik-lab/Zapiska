# Fixy po uživatelském testování 2D Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Opravit 11 konkrétních nálezů z uživatelského testování po Fázi 2D — viditelnost ghost tlačítek, layout vyjádření, AJAX modal error handling, refresh vyjádření/harmonogramu, meetings šipky, sdílené filtry záznamů/harmonogramu, quill bold, stav úkolu "Nezahájen".

**Architecture:** 11 problémů spadá do 5 kategorií: **design-wide** (ghost buttons, vyjádření layout, quill weight), **AJAX flow bugs** (modal error stuck, comments refresh, schedule refresh), **UX chyby** (meeting chevrons, schedule filtry), **data modely** (stav Nezahájen) a **polish** (rozpad toggle visible). Každá kategorie má jiný typ rizika — design změny mají velký dopad (touch ~20 views), AJAX bugy jsou lokální ale tricky (JS race conditions), data modely jsou triviální.

**Tech Stack:** ASP.NET Core 8 MVC + Razor, gov-design-system 4.2.9, Quill 2 rich-text, vlastní AJAX pipeline (`ajax.js`, `recordRefresh.js`), localStorage pro filtr preference, EF Core migrace.

**Důležité pravidlo pro všechny fixy:**
- Po každé změně JS v `modules/*.js` **synchronizovat `site.bundle.js`** (uživatel musí refreshovat, aplikace čte jen bundle)
- Po každé change **dotnet build** + relevantní test, jinak riziko další silent regression
- U vizuálních změn — popsat v commit message **před/po** stav

---

## Pořadí úkolů (řazení podle risk + coupling)

1. **Task 1**: Ghost button redesign (design decision, 1 CSS change → ovlivní ~25 buttonů napříč) — **nejdřív**, protože ovlivňuje Task 9 a vlastně mnoho míst najednou
2. **Task 2**: Quill font-weight normalize (1 CSS řádek)
3. **Task 3**: Stav úkolu "Nezahájen" (EF migrace + seed)
4. **Task 4**: Vyjádření layout redesign (CSS jen)
5. **Task 5**: AJAX modal error handling — modal zamrzne (ajax.js fix)
6. **Task 6**: Refresh vyjádření po uložení (recordRefresh.js fix)
7. **Task 7**: Refresh harmonogramu po uložení popisu (refresh scope)
8. **Task 8**: Meetings šipky — final fix (CSS transform-origin + init)
9. **Task 9**: Sdílené filtry záznamy/harmonogram (migrace harmonogramu na plný filter panel + shared persistence)
10. **Task 10**: Bundle sync + finální audit + publish

**Task 11 (#11 z auditu)** — search publish button styling — uživatel výslovně neuvedl, audit říká "není jasné". Přeskočit, pokud uživatel neupřesní.

---

## Task 1: Ghost button redesign — viditelné ohraničení

**Problem:** `Ghost` variant v `PmButtonTagHelper.cs:50` = `color="neutral", type="base"`. Gov-design-system v CSS má pro tuto kombinaci `border: none; background: transparent` — ghost button vypadá jako obyčejný text, uživatel nepozná, že je to button. Ovlivňuje Profil (3 reset tlačítka), komentáře (sort toggle, edit/delete), harmonogram (Rozpad toggle), Dashboard (Zobrazit více), a desítky dalších míst.

**Design decision:** Nejlepší fix = **změnit Ghost variant** v `PmButtonTagHelper.cs` z `("neutral", "base")` na `("neutral", "outlined")`. Gov outlined má viditelný 1px border, zachová ale neutral barvu (šedý) — nebyde konkurovat primary buttonům. Alternativa (CSS override v site.css) je křehká a může se po upgrade gov-ds rozbít.

**Files:**
- Modify: `PmTracker.Web/TagHelpers/PmButtonTagHelper.cs:50`
- Modify: `docs/architecture/buttons.md` — aktualizovat popis Ghost variant
- Modify: `PmTracker.Web.Tests/TagHelpers/PmButtonTagHelperTests.cs` — existuje test `Ghost_Rendruje_GovButtonBaseNeutral` (v `PmTracker.Tests.Unit/TagHelpers/`), přejmenovat a přepsat assertions

- [ ] **Step 1: Najít existující Ghost test v testech**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -n "Ghost" PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs
```

Expected: najít `Ghost_Rendruje_GovButtonBaseNeutral` test (v předchozí session ověřený v PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs:65-71).

- [ ] **Step 2: Přepsat test aby validoval nové Ghost chování**

V `PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs` najít:

```csharp
[Fact]
public async Task Ghost_Rendruje_GovButtonBaseNeutral()
{
    var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Ghost };
    var output = await RenderAsync(helper);
    output.Attributes["color"].Value.Should().Be("neutral");
    output.Attributes["type"].Value.Should().Be("base");
}
```

Nahradit za:

```csharp
[Fact]
public async Task Ghost_Rendruje_GovButtonOutlinedNeutral()
{
    var helper = new PmButtonTagHelper { Variant = PmButtonVariant.Ghost };
    var output = await RenderAsync(helper);
    // Ghost nyní = outlined neutral (viditelný border, šedá barva).
    // Viz docs/architecture/buttons.md — změna z base neutral po user testing feedback.
    output.Attributes["color"].Value.Should().Be("neutral");
    output.Attributes["type"].Value.Should().Be("outlined");
}
```

- [ ] **Step 3: Ověřit, že test teď fail**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PmButtonTagHelperTests.Ghost" --nologo --verbosity quiet`

Expected: FAIL — aktuální implementace rendruje `type="base"`, ne `"outlined"`.

- [ ] **Step 4: Upravit implementaci PmButtonTagHelper**

Modify `PmTracker.Web/TagHelpers/PmButtonTagHelper.cs:50`:

```csharp
// Ghost = outlined neutral (šedý border, transparentní pozadí).
// Po user testing (2026-04-19): původně "base" neutral byl neviditelný,
// uživatel nepoznal, že je to kliknutelné tlačítko. Viz docs/architecture/buttons.md.
PmButtonVariant.Ghost => ("neutral", "outlined"),
```

- [ ] **Step 5: Ověřit test teď prochází**

Run: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PmButtonTagHelperTests.Ghost" --nologo --verbosity quiet`

Expected: PASS.

- [ ] **Step 6: Ověřit celou test suite**

Run: `dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | grep -E "Úspěšné|Neúspěšné" | tail -5`

Expected: Tests.Unit 324/324, Web.Tests 106/106, Tests.Api 276/287 (11 baseline).

- [ ] **Step 7: Aktualizovat dokumentaci**

Modify `docs/architecture/buttons.md` — najít sekci o Ghost variant, aktualizovat popis:

```markdown
### Ghost (neutral outlined)

Viditelné 1px šedé ohraničení, transparentní pozadí. Pro sekundární akce,
kde Primary/Secondary buttony by byly příliš dominantní (preference reset,
comment sort toggle, Rozpad harmonogramu, "Zobrazit více" linky).

**Historie:** Do 2026-04-19 byla Ghost varianta renderovaná jako gov-button
`type="base"` bez borderu — v user testingu vyšlo, že uživatel nepozná,
že je to tlačítko. Změněno na `type="outlined"` pro viditelné affordance.
```

- [ ] **Step 8: Commit**

```bash
git add PmTracker.Web/TagHelpers/PmButtonTagHelper.cs PmTracker.Tests.Unit/TagHelpers/PmButtonTagHelperTests.cs docs/architecture/buttons.md
git commit -m "$(cat <<'EOF'
fix(pm-button): Ghost variant — outlined místo base (viditelný border)

User testing ukázal, že Ghost buttony (gov-button base neutral) nemají
žádné ohraničení ani background — uživatel nepozná, že jsou kliknutelné.
Zasažené views: Profil (3× reset), _ZaznamCommentsPartial (sort + edit/
delete), _ProjectScheduleTab (Rozpad), Dashboard panely, cca 25 dalších.

Fix: PmButtonVariant.Ghost mapuje na ("neutral", "outlined") místo
("neutral", "base"). Výsledek: šedé 1px ohraničení, transparentní bg,
stále neutralní (nekonkuruje Primary variantě).

- Updated unit test Ghost_Rendruje_GovButtonOutlinedNeutral
- Updated docs/architecture/buttons.md

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Quill editor — font-weight normal

**Problem:** Quill rich-text editor (používaný v popis záznamu + vyjádření) rendruje text "tučným" vizuálem. `.richtext-host .ql-editor` v `site.css:1786-1792` nemá explicitní `font-weight`. Text dědí z rodičovských container-ů, kde může být 500/600 (např. pokud se editor objeví v nějakém bold kontextu).

**Fix:** explicitně nastavit `font-weight: 400` v CSS na editor selector.

**Files:**
- Modify: `PmTracker.Web/wwwroot/css/site.css:1786-1792`

- [ ] **Step 1: Upravit CSS `.richtext-host .ql-editor`**

Modify `PmTracker.Web/wwwroot/css/site.css` — najít blok:

```css
.richtext-host .ql-editor {
    line-height: 1.4;
    overflow-wrap: anywhere;
    white-space: pre-wrap;
    padding: 10px 12px;
    overflow-y: hidden;
}
```

Přidat `font-weight: 400` a `font-family` reset:

```css
.richtext-host .ql-editor {
    line-height: 1.4;
    overflow-wrap: anywhere;
    white-space: pre-wrap;
    padding: 10px 12px;
    overflow-y: hidden;
    /* Explicit reset — editor nesmí dědít bold/heading font-weight z parent
       containeru (např. .form-group label, .comment-header). User tučnosti
       může uživatel stále dosáhnout toolbar tlačítkem Bold. */
    font-weight: 400;
    font-family: inherit;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build --nologo 2>&1 | tail -3`

Expected: 0 chyb.

- [ ] **Step 3: Commit**

```bash
git add PmTracker.Web/wwwroot/css/site.css
git commit -m "$(cat <<'EOF'
fix(richtext): Quill editor explicit font-weight 400 (ne tučný default)

.richtext-host .ql-editor dědil font-weight z parent containeru,
což v některých kontextech renderovalo text jako bold. Explicit
reset na 400 (normal) + font-family: inherit zajistí konzistentní
wygląd editoru v modal editoru záznamu i v comments.

Bold stále dostupný přes toolbar tlačítko Bold (uloží se jako
<strong>).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Stav úkolu — přidat "Nezahájen"

**Problem:** Uživatel chce přidat nový stav úkolu "Nezahájen". Lookup je v DB tabulce `CiselnikStavuUkolu` (entity `CiselnikStavuUkoluEntity`). Potřebuje se buď SQL INSERT + EF migrace, nebo update seed skriptu.

**Research needed:** Kde se stavy seedují? (možnosti: `DbContext.OnModelCreating`, `Program.cs` startup seed, SQL migration, admin UI).

**Files:**
- Read: `PmTracker.Web/Data/ApplicationDbContext.cs` nebo `PmTracker.Application/Infrastructure/Persistence/*Context*.cs` — najít kde je seed
- Read: `PmTracker.Web/Migrations/` — najít migraci pro stavy úkolů (hledat "Stav", "Ukol")
- Create: `PmTracker.Web/Migrations/<timestamp>_AddStavUkoluNezahajen.cs` (EF migrace) NEBO jen SQL skript

- [ ] **Step 1: Najít aktuální stavy úkolů**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "CiselnikStavuUkolu\|StavUkolu\|StavUkoluEntity" PmTracker.Web/Data/ PmTracker.Web/Models/ PmTracker.Application/ --include='*.cs' 2>&1 | head -20
```

- [ ] **Step 2: Najít seed data (startup nebo migrace)**

Run:
```bash
grep -rn "CiselnikStavuUkolu\|HasData\|\"Dokoncen\"\|\"Rozpracovany\"" PmTracker.Web/Migrations/ PmTracker.Web/Data/ 2>&1 | head -20
```

Hledáme HasData() call nebo migration InsertData.

- [ ] **Step 3: Rozhodnout strategii (na základě Step 1+2)**

Pokud existuje HasData() v entity konfiguraci:
- Upravit konfiguraci + vytvořit novou EF migraci

Pokud existuje InsertData v migraci:
- Vytvořit novou migraci `AddStavUkoluNezahajen` s InsertData

Pokud seed je v runtime startup:
- Upravit startup code + přidat SQL INSERT do lokální dev DB přes startup

- [ ] **Step 4: Vytvořit EF migraci (preferovaná cesta)**

Run:
```bash
cd PmTracker.Web
dotnet ef migrations add AddStavUkoluNezahajen --output-dir Migrations
```

Pokud `dotnet ef` není nainstalovaný, instalovat:
```bash
dotnet tool install --global dotnet-ef
```

Editovat generovanou migraci — přidat InsertData s odpovídajícími sloupci (Id, Kod="Nezahajen", Nazev="Nezahájen", IsFinal=false, IsLocked=false, + případně Poradi/Order).

**Pozor:** Id musí být unikátní, použít buď:
- Sekvenční ID po existujících (např. 10)
- GUID (pokud tabulka používá GUID)

- [ ] **Step 5: Build + lokální migrace**

Run:
```bash
dotnet build --nologo 2>&1 | tail -3
cd PmTracker.Web
dotnet ef database update
```

Expected: migrace aplikovaná, nové `Nezahajen` v tabulce.

**Fallback pokud dotnet ef nefunguje:** připravit pouze SQL skript v `docs/database/add-stav-nezahajen.sql` — uživatel si ho spustí ručně v SSMS. Popsat v commit message.

- [ ] **Step 6: Ověřit v UI**

Manuálně: spustit aplikaci, otevřít editor úkolu, potvrdit že dropdown "Stav" obsahuje "Nezahájen".

Pokud v UI nejde otevřít (není testovací data / role), ponechat jako open testovací bod pro user verify.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/Migrations/ PmTracker.Web/Data/
git commit -m "$(cat <<'EOF'
feat(ciselniky): přidat stav úkolu "Nezahájen"

User request: seznam stavů úkolu chybí "Nezahájen" pro úkoly, které
teprve čekají na začátek práce.

EF migrace AddStavUkoluNezahajen přidá řádek do CiselnikStavuUkolu.
Existující úkoly zůstávají ve svém stavu.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Layout vyjádření — redesign pravé strany

**Problem:** V `_ZaznamCommentsPartial.cshtml` je pravá strana každého vyjádření řešena takto (aktuální stav): autor, datum, jednání (chip), a akční tlačítka Upravit/Smazat jsou naskládané vertikálně pod sebou, "jako bordel". Tlačítka jsou navíc Ghost (Task 1 fix → outlined, visible), ale stále malá a těžko klikatelná.

**Design principles (frontend-design skill):**
- **Refined minimal** — čistota a hierarchie (institucionální aplikace, ne flashy)
- Oddělit **meta informace** (autor/datum/jednání) od **akcí** (edit/delete)
- Meta jsou "bližší textu" = horní řádek metadata (malý, tlumený text)
- Akce jsou "pravé horní" = oddělené, akcionovatelné, viditelné při hoveru (progressive disclosure)
- Hover nebo vždy visible? — pro institucionální UX **vždy visible** (uživatel ve spěchu, neinvestuje do zjišťování hover)

**Cílový layout:**
```
┌──────────────────────────────────────────────┬─────────────────┐
│ [Autor jmeno] · 15.4.2026 · [🔗 Jednání #12] │ [✎ Upravit] ❌  │
│                                              │                 │
│ Text vyjádření...                            │                 │
│ ...                                          │                 │
└──────────────────────────────────────────────┴─────────────────┘
```

- Metadata na 1 řádku, inline, separované tečkou
- Akce vpravo, menší ikony (Upravit = pencil, Smazat = trash), tooltipy
- Odstupy od textu menší

**Files:**
- Modify: `PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml` — restructure DOM
- Modify: `PmTracker.Web/wwwroot/css/site.css` — najít .comment-* selektory a přepsat

- [ ] **Step 1: Přečíst celý `_ZaznamCommentsPartial.cshtml` + CSS**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
cat PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml
grep -n "\.comment-\|\.record-comments" PmTracker.Web/wwwroot/css/site.css | head -30
```

Zjistit přesnou strukturu + stávající CSS, abys věděl kde co měnit.

- [ ] **Step 2: Restrukturovat DOM vyjádření**

Cílový DOM pattern pro jedno vyjádření:

```razor
<article class="comment-item" data-comment-id="@c.Id">
    <header class="comment-head">
        <div class="comment-head-meta">
            <span class="comment-author">@c.AutorJmeno</span>
            <span class="comment-sep" aria-hidden="true">·</span>
            <time class="comment-date" datetime="@c.VytvorenoUtc.ToString("o")">@c.VytvorenoZobrazeni</time>
            @if (c.JednaniOdkaz != null)
            {
                <span class="comment-sep" aria-hidden="true">·</span>
                <a class="comment-jednani-chip" href="@c.JednaniOdkaz.Url">
                    <gov-icon name="calendar" type="components"></gov-icon>
                    @c.JednaniOdkaz.Nazev
                </a>
            }
        </div>
        @if (c.CanEdit || c.CanDelete)
        {
            <div class="comment-head-actions">
                @if (c.CanEdit)
                {
                    <pm-button variant="Ghost" size="Small" icon="pencil" ...>Upravit</pm-button>
                }
                @if (c.CanDelete)
                {
                    <pm-button variant="Ghost" size="Small" icon="trash" ...>Smazat</pm-button>
                }
            </div>
        }
    </header>
    <div class="comment-body">@Html.Raw(c.ObsahHtml)</div>
</article>
```

**Konkrétní úpravy záleží na stávajícím ViewModelu — Step 1 čtení je nutné**.

- [ ] **Step 3: CSS pro `comment-head`**

Přidat/upravit v `site.css`:

```css
/* === Vyjádření (comments) — restructured 2026-04-19 ==== */

.comment-item {
    padding: 16px;
    border: 1px solid var(--gov-color-border);
    border-radius: 6px;
    background: var(--gov-color-surface);
    margin-bottom: 12px;
}

.comment-head {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 16px;
    margin-bottom: 8px;
    flex-wrap: wrap;
}

.comment-head-meta {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
    font-size: 13px;
    color: var(--gov-color-muted);
    min-width: 0; /* truncate podpora */
}

.comment-author {
    font-weight: 600;
    color: var(--app-text-primary);
}

.comment-sep {
    opacity: 0.4;
}

.comment-date {
    font-variant-numeric: tabular-nums;
}

.comment-jednani-chip {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 2px 8px;
    background: color-mix(in srgb, var(--gov-color-primary) 8%, transparent);
    border-radius: 10px;
    color: var(--gov-color-primary);
    text-decoration: none;
    font-size: 12px;
    font-weight: 500;
}

.comment-jednani-chip:hover {
    background: color-mix(in srgb, var(--gov-color-primary) 14%, transparent);
}

.comment-head-actions {
    display: flex;
    gap: 4px;
    flex-shrink: 0;
}

.comment-body {
    font-size: 14px;
    line-height: 1.5;
    color: var(--app-text-primary);
}

.comment-body p {
    margin: 0 0 8px;
}

.comment-body p:last-child {
    margin-bottom: 0;
}
```

- [ ] **Step 4: Build + vizuální check**

Build:
```bash
dotnet build --nologo 2>&1 | tail -3
```

Expected: 0 chyb.

Vizuální — spustit aplikaci (`dotnet run --project PmTracker.Web --no-build` + open `/Projekty/Detail/<id>` s vyjádřeními). Ověřit, že nový layout vypadá čistě.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Views/Projekty/_ZaznamCommentsPartial.cshtml PmTracker.Web/wwwroot/css/site.css
git commit -m "$(cat <<'EOF'
refactor(comments): redesign layout vyjádření

User feedback: pravá strana měla autora, datum, jednání a tlačítka
naskládané vertikálně — nepřehledné.

Nový layout:
- comment-head horizontální: metadata vlevo (autor · datum · jednání
  chip), akční tlačítka vpravo
- autor bold, datum tabular-nums, jednání chip s gov-icon (primary accent)
- edit/delete jako pm-button Ghost Small s icon (pencil/trash)
- flex-wrap pro narrow screeny

Related: Task 1 fix Ghost → outlined zajistí, že edit/delete buttony
jsou viditelné.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: AJAX modal — error handling, modal nezamrzne

**Problem:** Při uložení úpravy záznamu z modalu vyskočí "client ajax exception", modal nelze zavřít (křížek, Zrušit tlačítko, klik mimo — nic nefunguje). Submit button zůstává disabled (uživatel nemůže ani retryovat).

**Hypotesis:** `ajax.js` error handler (`~řádek 810-831`) volá `setFormSubmitting(target, false)` v `finally` bloku, ale buď:
- (a) `setFormSubmitting` má bug, který na gov-button (post 2D migrace) nefunguje správně — i po fixu v 2D `ajax.js` ještě může být nějaký edge case
- (b) `data-modal-close` event delegation je nějak blokovaná během AJAX submit (pořízený state nezůstane "locked")
- (c) Chyba se vyhodí ještě před finally (uncaught exception v chain)

**Files:**
- Read + Modify: `PmTracker.Web/wwwroot/js/modules/ajax.js` (řádek ~810-850, error flow)
- Read: `PmTracker.Web/wwwroot/js/modules/bootstrap.js:245` (data-modal-close event delegation)
- Read: `PmTracker.Web/wwwroot/js/modules/modals.js`
- Sync: `PmTracker.Web/wwwroot/js/site.bundle.js`

- [ ] **Step 1: Reprodukovat flow v kódu**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
sed -n '780,870p' PmTracker.Web/wwwroot/js/modules/ajax.js
```

Hledej:
- Co dělá error handler (catch block)
- Zda je finally spolehlivě volán
- Zda setFormSubmitting správně remove-uje `disabled`

- [ ] **Step 2: Ověřit data-modal-close handler během AJAX**

Run:
```bash
sed -n '240,270p' PmTracker.Web/wwwroot/js/modules/bootstrap.js
```

Hledej:
- Zda je handler [data-modal-close] volán **před** `event.preventDefault()`
- Zda se nepřerušuje prostředníky

- [ ] **Step 3: Přidat defensive error handling + modal unlock**

V `ajax.js` v error catch větvi (nebo v finally — ideálně obě) zajistit:
1. `setFormSubmitting(form, false)` — always called (již je, verify)
2. **Explicitně** re-enable submit button: `form.querySelectorAll(SUBMIT_SELECTOR).forEach(btn => setButtonDisabled(btn, false));`
3. Odstranit `data-recordEditorNavigating` state
4. Emitovat `pmtracker:ajax-error` custom event, který modal.js může poslouchat

Konkrétní diff (budeš muset přizpůsobit aktuálnímu kódu):

```javascript
// V error handler / catch bloku / finally:
try {
    setFormSubmitting(target, false);
    const submitButtons = target.querySelectorAll(SUBMIT_SELECTOR);
    submitButtons.forEach(btn => setButtonDisabled(btn, false));
    delete target.dataset.recordEditorNavigating;
} catch (cleanupError) {
    console.error("[ajax] cleanup after error failed:", cleanupError);
}
```

- [ ] **Step 4: Ověřit klik na [data-modal-close] během error state**

V modals.js najít, zda je close handler **nezávislý** na stavu modalu/formulář. Uživatelův případ: klik Zrušit neměl reakci — to znamená handler **není volán** (uncaught exception pravděpodobně zastavila event propagaci) NEBO modal kontejner byl pomocnými atributy v "navigating" state který blokuje close.

Pokud najdeš `dataset.recordEditorNavigating === "true"` guard, co blokuje close:
- Přidat `clearNavigatingGuard(modal)` helper, který bezpečně clearne
- Zavolat ho v error handleru

- [ ] **Step 5: Synchronizovat bundle**

```bash
git diff HEAD -- PmTracker.Web/wwwroot/js/modules/ajax.js
```

A aplikovat stejný diff do `site.bundle.js`. Najdi konkrétní pasáž v bundle přes grep (podle unikátních řetězců jako "setFormSubmitting" nebo "recordEditorNavigating"), Edit tool na přesné shodě.

- [ ] **Step 6: Build + test**

```bash
dotnet build --nologo 2>&1 | tail -3
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | grep -E "Úspěšné|Neúspěšné" | tail -5
```

Expected: build 0 chyb, testy baseline beze změny.

- [ ] **Step 7: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/ajax.js PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "$(cat <<'EOF'
fix(ajax): po AJAX error modal jde zavřít a submit button se odemkne

User report: při uložení úpravy záznamu z modalu s validation error
nebo server exception modal zamrzl — křížek, Zrušit ani klik mimo
nereagovaly. Fungovalo jen Uložit a Smazat trvale.

Root cause: setFormSubmitting finally block nespolehlivě resetoval
disabled stav na pm-button (gov-button), a data-recordEditorNavigating
guard blokoval data-modal-close handler.

Fix:
- defensive cleanup v error/finally: explicit setButtonDisabled(false)
  na všech SUBMIT_SELECTOR elementech
- clear dataset.recordEditorNavigating
- try/catch kolem cleanup aby cleanup fail nezablokoval další error flow

Sync site.bundle.js.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Refresh vyjádření po uložení

**Problem:** Po úspěšném uložení úpravy záznamu z modalu se stránka zavře, ale list vyjádření (`_ZaznamCommentsPartial`) se neznázňovává, vyjádření se objeví až po ručním reloadu stránky.

**Files:**
- Read: `PmTracker.Web/wwwroot/js/modules/recordRefresh.js` — najít `refreshRecordComments` a kdy se volá
- Modify: stejný soubor, doplnit AJAX refresh comments po úspěšném submit
- Sync: bundle

- [ ] **Step 1: Audit refresh flow**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -n "refreshRecordComments\|loadRecordComments\|record-comments-panel" PmTracker.Web/wwwroot/js/modules/recordRefresh.js
grep -n "refreshPageScope\|data-record-comments-scope" PmTracker.Web/wwwroot/js/modules/*.js
```

Identifikovat:
- Je `refreshRecordComments` volán z `ajax.js` po success?
- Má payload `refreshUrl` pro comments?
- Je `data-record-comments-scope="@recordId"` na panelu?

- [ ] **Step 2: Najít controller akce a payload response**

Run:
```bash
grep -rn "record-comments\|Vyjadreni.*Html\|RefreshUrl.*comments" PmTracker.Web/Controllers/ --include='*.cs'
grep -n "refreshUrl\|CommentsRefreshUrl" PmTracker.Web/Models/ PmTracker.Application/Features/ --include='*.cs' | head -10
```

Najít co vrací `UpdateRecord` akce po POST — má v payloadu URL pro re-fetch comments?

- [ ] **Step 3: Zavést refreshCommentsForRecord po success submit**

V `ajax.js` nebo `recordRefresh.js` — po úspěšném submit a zavření modalu:

```javascript
// Po 200 OK + payload.ok === true:
if (payload.recordId) {
    const scope = document.querySelector(
        `[data-record-comments-scope="${payload.recordId}"]`
    );
    if (scope) {
        const refreshUrl = scope.dataset.recordCommentsRefreshUrl
            || `/Projekty/VyjadreniPartial?zaznamId=${payload.recordId}`;
        fetch(refreshUrl, { headers: { "X-Requested-With": "XMLHttpRequest" }})
            .then(r => r.text())
            .then(html => { scope.innerHTML = html; initDynamicContent(scope); });
    }
}
```

**Pozor:** přesný endpoint najít v kódu (může už existovat `/Projekty/ZaznamCommentsRefresh` nebo podobně).

- [ ] **Step 4: Ověřit v běžící aplikaci**

Spustit aplikaci, otevřít záznam, upravit vyjádření, save → mělo by se obnovit seznam vyjádření bez reloadu.

- [ ] **Step 5: Sync bundle**

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/recordRefresh.js PmTracker.Web/wwwroot/js/modules/ajax.js PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "fix(ajax): refresh vyjádření po uložení záznamu přes modal

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Refresh harmonogramu — "Navrhnout úpravu harmonogramu" button

**Problem:** Po uložení úpravy jen popisu záznamu se po refreshi nezobrazí tlačítko "Navrhnout úpravu harmonogramu" (uživatel je zástupce vedoucího subsystému, jinde se zobrazuje → tedy permission OK). Problém je pravděpodobně refresh scope — _ProjectScheduleTab se neobnoví.

**Files:**
- Read: `PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml` — najít render logiku navrhnout úpravu
- Read: `PmTracker.Web/wwwroot/js/modules/recordRefresh.js` — refresh scope logika
- Modify: refresh flow, aby zahrnul schedule panel

- [ ] **Step 1: Najít render navrhnout úpravu**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "Navrhnout úpravu\|navrhnout-zmenu\|NavrhnoutHarmonogram\|ProposeScheduleChange" PmTracker.Web/Views/ PmTracker.Web/Controllers/ --include='*' | head -20
```

- [ ] **Step 2: Zjistit podmínky zobrazení**

Zjistit přesné podmínky (role check + data check). Možné že podmínka kontroluje nějakou data property která se po AJAX update nepropaguje.

- [ ] **Step 3: Rozšířit refresh scope**

Pokud aktuální refresh touch jen `_ZaznamPartial` nebo `_ZaznamCommentsPartial`, rozšířit o `_ProjectScheduleTab` (pokud je tab aktivní).

- [ ] **Step 4: Sync + commit**

```bash
git add PmTracker.Web/wwwroot/js/modules/recordRefresh.js PmTracker.Web/wwwroot/js/site.bundle.js
git commit -m "fix(schedule): Navrhnout úpravu harmonogramu po AJAX refresh

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Meetings šipky — final fix

**Problem:** Uživatel hlásí "šipky u jednání pořád napíču, blbě". V předchozí fázi jsem uzavřel task s tím, že bugy byly opraveny ranějším commitem. User testing ukázal, že to stále není OK.

**Hypotesis:** CSS `transform-origin` nechybí, ale možná šipka je renderovaná špatným směrem nebo má CSS conflict s gov CSS.

**Files:**
- `PmTracker.Web/wwwroot/css/site.css:4491-4512` — chevron CSS
- `PmTracker.Web/wwwroot/js/modules/meetingOverview.js` — state logika

- [ ] **Step 1: Reprodukovat problém**

Tohle je kritické — bez reprodukce nemáme co opravit. Spustit aplikaci, otevřít `/Jednani/Index` a `/Projekty/Detail/<id>?tab=jednani`. Zkontrolovat:
- Collapsed rok: šipka → **měla by být ↓ (rotate 45deg)**
- Open rok: šipka → **měla by být ↑ (rotate -135deg)**
- Preview + hasHidden: šipka → **↓**
- Preview bez hidden: šipka → **↑**

Pokud je to obráceně **nebo nerespond**uje na kliknutí, najít root cause:
- CSS: přidat `transform-origin: center;` explicitně (default je center, ale gov CSS může overridovat)
- JS: console.log v `applyMeetingYearState()` — ověřit, že `data-meeting-year-state` se správně mění

- [ ] **Step 2: Na základě reprodukce aplikovat fix**

Možné opravy (podle toho, co Step 1 zjistí):

**(a) CSS transform-origin:**
```css
.meeting-year-chevron {
    ...
    transform-origin: 50% 50%;
}
```

**(b) Šipka je reverzně orientovaná:** změnit rotate hodnoty.

**(c) JS se správně nespouští po AJAX refresh:**
Zajistit, že po AJAX re-render je `initMeetingOverview(newScope)` zavolán.

- [ ] **Step 3: Aktualizovat spec docs/specs/meetings-year-grouping.md** pokud se změní chování

- [ ] **Step 4: Sync + commit**

---

## Task 9: Sdílené filtry záznamy/harmonogram

**Problem:** Harmonogram má jen 2 filtry (subsystem + sortBy). Uživatel chce stejné filtry jako záznamy (cca 10+ filtrů: subsystem, status, owner, category, type, deadline, …) a **sdílenou persistence** — když filtruje záznamy, stejná hodnota se má reflect-ovat v harmonogramu.

**Scope:** velký task — 1 Razor view rewrite + JS persistence refaktor.

**Files:**
- Read: `PmTracker.Web/Views/Projekty/_ProjectRecordsTab.cshtml` (filters section)
- Modify: `PmTracker.Web/Views/Projekty/_ProjectScheduleTab.cshtml` — převzít filter markup
- Modify: JS `data-filter-save-preferences` persistence — unify across both tabs
- Read: backend `ProjektyController.ScheduleTabJson` (nebo podobný) — zda filtry akceptuje

- [ ] **Step 1: Audit filter state architektury**

Run:
```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "data-filter-save-defaults\|data-filter-record-editor\|project-filter-preferences" PmTracker.Web/wwwroot/js/ PmTracker.Web/Views/ --include='*' | head -20
```

- [ ] **Step 2: Zkopírovat filter markup z records do schedule**

Velký diff. Podstatné — zachovat stejné `data-filter-*` atributy, aby JS storage automaticky fungoval pro oba.

- [ ] **Step 3: Upravit backend** (pokud je harmonogram server-side filtering)

Zjistit, zda backend `ScheduleTabViewModel` umí zpracovat stejné query parametry jako `RecordsTabViewModel`. Pokud ne, rozšířit.

- [ ] **Step 4: Sdílené storage**

Upravit JS aby storage klíč nebyl `project-filter-preferences-records` vs `-schedule`, ale jeden společný `project-filter-preferences-shared` (per projekt).

- [ ] **Step 5: Test napříč záložkami**

- [ ] **Step 6: Commit** (velký)

---

## Task 10: Bundle sync + finální test + publish

- [ ] **Step 1: Sync bundle** pro všechny JS změny z Task 5/6/7/8

Grep `isButtonLike`, nové helpery, fixy v bundle — musí být konzistentní.

- [ ] **Step 2: Full build + test**

```bash
dotnet build --nologo 2>&1 | tail -5
dotnet test --nologo --filter "FullyQualifiedName!~E2E&FullyQualifiedName!~Integration" --verbosity quiet 2>&1 | grep -E "Úspěšné|Neúspěšné" | tail -8
```

- [ ] **Step 3: Publish Release**

```bash
dotnet publish PmTracker.Web -c Release -o ./publish --nologo 2>&1 | tail -3
```

- [ ] **Step 4: Ověřit bundle v publish**

```bash
grep -c "isButtonLike" publish/wwwroot/js/site.bundle.js
```

Expected: 28+ (stejně jako v modules).

- [ ] **Step 5: Report uživateli**

Shrnutí všech 11 oprav, co byla reálná změna, co bylo skip.

---

## Self-Review

**Spec coverage:** 10 z 11 user bodů má task (Task 11 — search publish button — v auditu bylo "nejasné", přeskočeno do další iterace pokud uživatel upřesní).

**Placeholder scan:** Task 3 (Step 3) má větvení podle auditu; Task 5-7 mají "najít a upravit" — ne placeholder, ale nutný discovery. Task 8 má 3 možné cesty podle reprodukce — legit, protože bez run-time pozorování není jasné.

**Type consistency:** `isButtonLike` / `setButtonDisabled` / `SUBMIT_SELECTOR` konzistentní napříč. pm-button `variant="Ghost"` po Task 1 se rendruje outlined neutral.

**Risk hotspots:**
- Task 1 (Ghost) ovlivní ~25 buttonů → musí být po tasku manuálně vizuálně prověřeno
- Task 5 (AJAX modal) — nemám reálnou reprodukci chyby, pracuji s hypotézami. **Může vyžadovat další iteraci** po user testu
- Task 9 (filtry) — největší objem, riziko API mismatch backend vs frontend
