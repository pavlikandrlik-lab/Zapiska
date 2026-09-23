# P9 — Průřezové schopnosti: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Doplnit schopnosti, které nepatří k žádnému jednomu modulu, ale musí být v celé
aplikaci: auditní stopa, tisk, vyhledávání, profil, nápověda a verzování aplikace.
Po tomto plánu je **etapa 1 hotová**.

**Architektura:** Nic z toho nemění datový model ani kontrakty. Jsou to průřezové vrstvy
nad hotovým základem.

**Stack:** .NET 10, PuppeteerSharp, OpenSearch, Markdig, React + TypeScript, xUnit.

**Specifikace:** [../10-specifikace/03-schopnosti.md](../10-specifikace/03-schopnosti.md)
(průřezové schopnosti), [10-obrazovky.md](../10-specifikace/10-obrazovky.md) (O3, O4, O10–O12)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P8. **Uzavírá dluh D14 z P6.**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Každý zásah do dat má auditní stopu — uzavírá D14 |
| 2 | Obrazovka auditního logu s filtry |
| 3 | Tisk a PDF s číslováním stránek |
| 4 | Globální vyhledávání napříč číselníky |
| 5 | Uživatelský profil |
| 6 | Wiki knihovna v aplikaci |
| 7 | Living style guide — obrazovka O13 |
| 8 | Verze aplikace, changelog a povýšení na 1.0 |
| 9 | Závěrečné ověření celé etapy 1 |

---

## Blok 1: Auditní log

**Cíl bloku:** Každý zásah do dat je dohledatelný. **Uzavírá dluh D14 z P6.**

**Soubory:**
- Vytvoř: `db/db_upgrade_0_7_audit.sql`
- Vytvoř: `Ciselniky.Core/Domain/AuditZaznam.cs`
- Vytvoř: `Ciselniky.Core/Services/Audit/AuditSluzba.cs`
- Uprav: `ZamekSluzba` (D14), `PrirazeniSluzba`, `CiselnikSluzba`, `DefiniceSluzba`, `Publikovac`
- Test: `Ciselniky.Tests.Integration/Audit/AuditSluzbaTests.cs`

- [ ] **Krok 1: Migrační skript**

```sql
-- Auditní stopa. Jen k připsání: záznamy se nemění ani nemažou.

CREATE TABLE audit_zaznam (
    id          bigint IDENTITY(1,1) PRIMARY KEY,
    kdy         datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
    osoba_id    int     REFERENCES osoby(id),   -- NULL = systém (běh zdroje)
    akce        nvarchar(64) NOT NULL,               -- klíč oprávnění, nebo název systémové akce
    ciselnik_id int     REFERENCES ciselniky(id),
    predmet     nvarchar(128),                       -- kód položky, login osoby, číslo verze…
    podrobnosti nvarchar(max)                                -- lidsky čitelný doplněk
);

CREATE INDEX ix_audit_kdy      ON audit_zaznam (kdy DESC);
CREATE INDEX ix_audit_osoba    ON audit_zaznam (osoba_id, kdy DESC);
CREATE INDEX ix_audit_ciselnik ON audit_zaznam (ciselnik_id, kdy DESC);

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.7');
```

> **Auditní log není totéž co verzování a nesmí se s ním slučovat.**
> Verze říká *co* se v číselníku změnilo a je dopočitatelná. Audit říká *kdo, kdy
> a odkud zasáhl do aplikace* — včetně věcí, které do verzí nespadají vůbec:
> přidělení role, odebrání zámku, spuštění přeindexování.

> **Jen k připsání.** Aplikace nemá jedinou cestu, kterou by auditní záznam upravila
> nebo smazala. Vynucuje to architektonický test v kroku 4.

Entita a sada:

```csharp
namespace Ciselniky.Core.Domain;

public sealed class AuditZaznam
{
    public long Id { get; set; }
    public DateTimeOffset Kdy { get; set; }
    public int? OsobaId { get; set; }
    public required string Akce { get; set; }
    public int? CiselnikId { get; set; }
    public string? Predmet { get; set; }
    public string? Podrobnosti { get; set; }
}
```

```csharp
// CiselnikyDbContext
public DbSet<AuditZaznam> AuditZaznamy => Set<AuditZaznam>();
// v OnModelCreating
model.Entity<AuditZaznam>().ToTable("audit_zaznam");
```

- [ ] **Krok 2: Napiš padající testy**

```csharp
[Fact] public async Task Audit_ZaznamenaPridelenRole()
[Fact] public async Task Audit_ZaznamenaPublikovaniVerze()
[Fact] public async Task Audit_ZaznamenaSiloveOdebraniZamku()
[Fact] public async Task Audit_ZaznamenaZmenuStruktury()
[Fact] public async Task Audit_NezaznamenavaCteni()
```

Třetí test uzavírá dluh D14. Poslední drží rozhodnutí Z1: **kdo si číselník přečetl,
aplikace neeviduje a evidovat nemá.**

- [ ] **Krok 3: Služba a její zapojení**

```csharp
namespace Ciselniky.Core.Services.Audit;

public sealed class AuditSluzba(CiselnikyDbContext db)
{
    public void Zaznamenej(int? osobaId, string akce, int? ciselnikId = null,
                           string? predmet = null, string? podrobnosti = null)
        => db.AuditZaznamy.Add(new AuditZaznam
        {
            OsobaId = osobaId, Akce = akce, CiselnikId = ciselnikId,
            Predmet = predmet, Podrobnosti = podrobnosti
        });
}
```

Zapisuje se **do téže transakce jako sledovaná operace**. Kdyby šel audit zvlášť,
mohla by operace projít bez záznamu, nebo záznam vzniknout k operaci, která selhala.

Zapojení: `PrirazeniSluzba` (přidělení a odebrání role), `Publikovac` (vydání verze),
`ZamekSluzba.OdeberSiluAsync` (**D14**), `DefiniceSluzba` (změna struktury),
`CiselnikSluzba` (založení a vyřazení číselníku).

- [ ] **Krok 4: Architektonický test na neměnnost**

```csharp
[Fact]
public void Audit_SeNikdeNemazeAniNemeni()
{
    var prohresky = ZdrojoveSoubory()
        .Where(s => s.Text.Contains("AuditZaznamy.Remove")
                 || s.Text.Contains("AuditZaznamy.RemoveRange")
                 || s.Text.Contains("AuditZaznamy.Update"))
        .Select(s => s.Cesta).ToArray();

    Assert.True(prohresky.Length == 0,
        "Auditní log je jen k připsání. Zásah v: " + string.Join(", ", prohresky));
}

private static IEnumerable<(string Cesta, string Text)> ZdrojoveSoubory()
    => Directory
        .EnumerateFiles(KorenRepozitare(), "*.cs", SearchOption.AllDirectories)
        .Where(c => !c.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                 && !c.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                 && !c.Contains("Tests"))
        .Select(c => (c, File.ReadAllText(c)));
```

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter AuditSluzbaTests   # Passed: 5
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(audit): auditní stopa zásahů do aplikace"
```

---

## Blok 2: Obrazovka auditního logu

**Cíl bloku:** Správce dohledá, kdo co kdy změnil. Wireframe O10.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/AuditController.cs`
- Vytvoř: `ciselniky-web/src/stranky/AuditniLog.tsx`
- Test: `Ciselniky.Tests.Api/AuditControllerTests.cs`

- [ ] **Krok 1: Koncový bod**

```csharp
[HttpGet("internal/audit")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.NastaveniAuditView)]
public Task<IActionResult> Seznam(
    [FromQuery] int? osobaId, [FromQuery] string? ciselnik,
    [FromQuery] DateOnly? od, [FromQuery] DateOnly? doData,
    [FromQuery] int strana = 1, [FromQuery] int velikost = 100,
    CancellationToken ct = default);
```

- [ ] **Krok 2: Obrazovka**

Sloupce: kdy · kdo · akce · číselník · předmět. Filtry podle osoby, číselníku a období.
Řazení vždy od nejnovějšího — kdo sem přijde, hledá zpravidla to poslední.

Akce se zobrazuje **lidským názvem z katalogu oprávnění**, ne klíčem:
`hodnoty.edit` → *Upravit hodnotu*. Klíč je v popisku pro toho, kdo ho hledá.

- [ ] **Krok 3: Testy, ověř a commitni**

```csharp
[Fact] public async Task Audit_BezPrava_Odmitne()
[Fact] public async Task Audit_FiltrujePodleOsobyIObdobi()
[Fact] public async Task Audit_RadiOdNejnovejsiho()
```

```bash
dotnet test Ciselniky.Tests.Api --filter AuditControllerTests
git add -A && git commit -m "feat(web): obrazovka auditního logu"
```

---

## Blok 3: Tisk a PDF

**Cíl bloku:** Tlačítko `Tisk` vrátí **hotové PDF s patičkou „Strana X z Y"**.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Views/Tisk/Ciselnik.cshtml`, `RozdilVerzi.cshtml`, `_TiskLayout.cshtml`
- Vytvoř: `Ciselniky.Api/Services/Tisk/SablonaRenderer.cs`, `EdgePdfRenderer.cs`
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/TiskController.cs`
- Test: `Ciselniky.Tests.Unit/Architektura/RazorJenProTiskTests.cs`
- Test: `Ciselniky.Tests.Api/TiskControllerTests.cs`

### Proč serverové PDF, a ne tiskový dialog prohlížeče

> CSS umí číslovat stránky přes `@page { @bottom-center { content: counter(page) } }`,
> ale **Chromium — a tedy i Edge na cílových stanicích — margin boxy neimplementuje.**
> Zápiska to ověřila pokusem: vygenerované třístránkové PDF neobsahovalo slovo „Strana"
> vůbec. Ani `position: fixed` to nespraví; `counter(page)` je mimo `@page` neplatný.
>
> **Číslo stránky může vzniknout jen tam, kde se stránky skutečně lámou** — v generátoru
> PDF. Zápiska proto od tiskového dialogu prohlížeče ustoupila a Číselníky tou cestou
> nezačínají.

**Uživatel Razor stránku nikdy neuvidí.** Vyrenderuje se do řetězce uvnitř serveru
a rovnou putuje do sazby.

- [ ] **Krok 1: Razor výhradně pro tisk — a test, který to hlídá**

```csharp
/// <summary>
/// Aplikace má frontend v Reactu. Razor v ní zůstává výhradně jako zdroj HTML pro sazbu
/// PDF — tam se hodí, protože escapuje sám a tisková sestava je neinteraktivní dokument.
/// Mimo Views/Tisk/ nemá co dělat.
/// </summary>
[Fact]
public void Razor_SablonyJsouJenPodViewsTisk()
{
    var mimo = Directory
        .EnumerateFiles(KorenRepozitare(), "*.cshtml", SearchOption.AllDirectories)
        .Where(c => !c.Contains(Path.Combine("Views", "Tisk"))
                 && !c.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
        .ToArray();

    Assert.True(mimo.Length == 0,
        "Razor šablony mimo Views/Tisk/:\n  " + string.Join("\n  ", mimo));
}
```

> Ruční skládání HTML z uživatelských dat by znamenalo hlídat escapování na každém místě
> a jedno opomenutí je bezpečnostní chyba. Šablonovací vrstva to dělá sama — proto
> se pro tisk používá, a proto se testem drží tam, kde je užitečná.

- [ ] **Krok 2: Sazba PDF řídí už nainstalovaný Edge**

```csharp
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Ciselniky.Api.Services.Tisk;

/// <summary>
/// Vysází PDF z HTML. Řídí <b>Edge, který je na serveru už nainstalovaný</b> —
/// nestahuje žádnou binárku, což je v uzavřené síti podmínka.
/// </summary>
public sealed class EdgePdfRenderer(IOptions<CiselnikyNastaveni> nastaveni) : IPdfRenderer
{
    public async Task<byte[]> VysazejAsync(string html, CancellationToken ct = default)
    {
        await using var prohlizec = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = nastaveni.Value.CestaKProhlizeci   // např. msedge.exe
        });

        await using var stranka = await prohlizec.NewPageAsync();
        await stranka.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle0]
        });

        return await stranka.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate = """
                <div style="width:100%;text-align:center;font-size:9px;color:#555;">
                  Strana <span class="pageNumber"></span> z <span class="totalPages"></span>
                </div>
                """,
            MarginOptions = new MarginOptions
            {
                Top = "12mm", Bottom = "16mm", Left = "14mm", Right = "14mm"
            }
        });
    }
}
```

> **PuppeteerSharp, ne Microsoft.Playwright.** Zápiska obojí změřila: Playwright si nese
> node driver o **125 MB** v build outputu, PuppeteerSharp mluví s prohlížečem přímo
> z .NET a zabere **~7 MB**. Do balíku, který se nosí přes vzdálenou plochu, patří druhý.
> Playwright zůstává v koncových testech, kde velikost nevadí.

> Třídy `pageNumber` a `totalPages` v patičce plní **sám prohlížeč** při sazbě.
> Je to jediné místo, kde se dá k číslu stránky dostat.

- [ ] **Krok 3: Render šablony do řetězce**

Rozhraní obou kroků sazby:

```csharp
namespace Ciselniky.Api.Services.Tisk;

public interface IPdfRenderer
{
    Task<byte[]> VysazejAsync(string html, CancellationToken ct = default);
}

public interface ISablonaRenderer
{
    /// <summary>Vyrenderuje Razor šablonu do řetězce. Výsledek jde rovnou do sazby PDF —
    /// prohlížeči se neposílá.</summary>
    Task<string> VyrendrujAsync(string cestaSablony, object model, CancellationToken ct = default);
}
```

`SablonaRenderer` se přebírá ze Zápisky
(`Services/Export/RazorViewRenderer.cs`) — je to jediný soubor ve službách Zápisky,
který o Razoru ví, a právě tady se hodí.

> Rozdělení na dvě rozhraní není samoúčelné: **v testech se podvrhuje jen sazba**,
> aby se nespouštěl prohlížeč, kdežto render šablony se testuje doopravdy.

- [ ] **Krok 4: Šablony a koncový bod**

`Views/Tisk/Ciselnik.cshtml` — hlavička s **kódem, názvem, verzí a datem vydání**.
Bez nich je vytištěný číselník nedatovatelný papír. V patičce jen číslo stránky.

```csharp
[HttpGet("internal/ciselniky/{kod}/tisk")]
[AllowAnonymous]                                  // tisk je čtení (rozhodnutí N1)
public async Task<IActionResult> Tisk(string kod, [FromQuery] string? verze,
                                      CancellationToken ct)
{
    var html = await _sablony.VyrendrujAsync("Tisk/Ciselnik", model, ct);
    var pdf = await _pdf.VysazejAsync(html, ct);

    // Otevře se v prohlížečce PDF v nové kartě, nestahuje se do Stažených souborů.
    return File(pdf, "application/pdf", $"{kod}.pdf", enableRangeProcessing: false);
}
```

Odpověď nese `Content-Disposition: inline`.

- [ ] **Krok 5: Testy**

```csharp
[Fact] public async Task Tisk_VraciPdf()
[Fact] public async Task Tisk_JeDostupnyBezPrihlaseni()
[Fact] public async Task Tisk_StarsiVerze_VysadiTehdejsiStav()
[Fact] public void Razor_SablonyJsouJenPodViewsTisk()
```

V testech se sazba nahrazuje **podvrženým generátorem**, aby se nespouštěl prohlížeč.
Vzor: `PmTracker.Tests.Api/TestInfrastructure/FakePdfRenderer.cs`.

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter TiskControllerTests
dotnet test Ciselniky.Tests.Unit --filter RazorJenProTiskTests
git add -A && git commit -m "feat(tisk): serverové PDF s číslováním stránek"
```

---

## Blok 4: Globální vyhledávání

**Cíl bloku:** Uživatel najde hodnotu napříč všemi číselníky. Wireframe O3.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Hledani/IndexSluzba.cs`, `HledaciSluzba.cs`
- Vytvoř: `ciselniky-web/src/stranky/VysledkyHledani.tsx`
- Test: `Ciselniky.Tests.Integration/Hledani/HledaniTests.cs`

- [ ] **Krok 1: Přepínač schopnosti**

Vyhledávání je **volitelné**. Bez dostupného OpenSearch aplikace běží dál, jen se
vyhledávací pole nezobrazí.

Do `CiselnikyNastaveni` z P8 přibudou v tomto plánu tři vlastnosti:

```csharp
/// <summary>Cesta ke spustitelnému prohlížeči pro sazbu PDF, např. msedge.exe.</summary>
public required string CestaKProhlizeci { get; set; }

/// <summary>Vyhledávání je volitelné — bez něj aplikace běží dál.</summary>
public bool HledaniZapnuto { get; set; }
public string? OpenSearchUrl { get; set; }
```

Na rozdíl od `BazoveUri` **žádná z nich nesmí zastavit start aplikace**. Bázová adresa
je součástí identity dat a chybí-li, hrozí nevratný stav; tyhle tři jen vypnou schopnost.

> Vyhledávání je pohodlí, ne podmínka provozu. Kdyby jeho výpadek shodil celou aplikaci,
> byla by nedostupná i pro to, kvůli čemu existuje — čtení číselníků.

- [ ] **Krok 2: Co se indexuje**

Číselníky (kód, název, popis) a **publikované** hodnoty (kód, název, textové atributy).
Rozpracované změny se neindexují — nejsou to zveřejněná data.

Index se obnovuje **po publikování verze** a celý se dá přestavět akcí pod klíčem
`hledani.reindex`.

- [ ] **Krok 3: Zjednodušení proti Zápisce**

> Zápiska musí výsledky vyhledávání **filtrovat po dotazu podle oprávnění**, protože
> ne každý smí vidět každý projekt.
>
> **Číselníky tenhle krok nemají a mít nesmí.** Čtení není chráněná akce (rozhodnutí N1) —
> číselníky vidí každý. Filtr po dotazu by byl mrtvý kód a zároveň past: kdyby ho někdo
> později „opravil", zavedl by kontrolu čtení, která je proti smyslu aplikace.

- [ ] **Krok 4: Testy, obrazovka, commit**

```csharp
[Fact] public async Task Hledani_NajdeHodnotuNapricCiselniky()
[Fact] public async Task Hledani_VysledekVedeNaPolozkuVJejimCiselniku()
[Fact] public async Task Hledani_NeindexujeRozpracovaneZmeny()
[Fact] public async Task Hledani_PriNedostupnemOpenSearch_AplikaceBezi()
```

```bash
dotnet test Ciselniky.Tests.Integration --filter HledaniTests
git add -A && git commit -m "feat(hledani): globální vyhledávání přes OpenSearch"
```

---

## Blok 5: Uživatelský profil

**Cíl bloku:** Obrazovka O11.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/Profil.tsx`
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/ProfilController.cs`
- Test: `Ciselniky.Tests.Api/ProfilControllerTests.cs`

- [ ] **Krok 1: Co profil obsahuje**

Údaje osoby z Active Directory **jen ke čtení** — jméno, příjmení, e-mail, útvar.
Měnitelná je jediná věc: **režim vzhledu** (světlý / tmavý / podle systému).

> Údaje z domény se v aplikaci needitují. Kdyby šly, rozešly by se s doménou
> a nikdo by nevěděl, který zápis platí.

- [ ] **Krok 2: Uložení volby**

Volba vzhledu se ukládá **do souboru cookie** (už z P1), ne do databáze — je to
vlastnost prohlížeče, ne osoby, a musí platit i pro čtenáře bez záznamu v aplikaci.

- [ ] **Krok 3: Testy a commit**

```csharp
[Fact] public async Task Profil_ZobrazuejeUdajeZAd()
[Fact] public async Task Profil_UdajeZAdNejdouZmenit()
[Fact] public async Task Profil_NeznamyUzivatel_UvidiJenVolbuVzhledu()
```

```bash
dotnet test Ciselniky.Tests.Api --filter ProfilControllerTests
git add -A && git commit -m "feat(profil): uživatelský profil s volbou vzhledu"
```

---

## Blok 6: Wiki knihovna v aplikaci

**Cíl bloku:** Nápověda se čte v aplikaci, edituje se jako soubory v gitu. Obrazovka O12.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Dokumentace/MarkdownSluzba.cs`
- Vytvoř: `ciselniky-web/src/stranky/Dokumentace.tsx`
- Doplň: `docs/wiki/**` — texty k blokům, které už jsou hotové
- Test: `Ciselniky.Tests.Api/DokumentaceTests.cs`

- [ ] **Krok 1: Služba**

Přebírá se ze Zápisky (`Services/Documentation/MarkdownDocumentationService.cs`) —
render markdownu a sestavení navigačního stromu podle složek. **Bez vazby na UI**,
takže se mění jen obrazovka.

> **Jeden zdroj pravdy.** Nápověda žije jako soubory v repozitáři a v aplikaci se jen
> vykresluje. Druhá kopie v databázi by se rozešla s tou v gitu.

- [ ] **Krok 2: Doplň texty**

Struktura wiki vznikla už v P1; **texty se dopisují teď**, ke schopnostem, které
mezitím vznikly (rozhodnutí G3):

```
docs/wiki/
├── zacatek/           k čemu aplikace je, jak se v ní pohybovat
├── ciselniky/         prohlížení, struktura, verze, rozdíl verzí
├── editace/           hromadná editace, zámek, publikování, format-importu.md
├── rozhrani/          pro-konzumenty.md (odkazované z P8)
└── nastaveni/         role a datový rozsah, efektivní práva, audit
```

`editace/format-importu.md` a `rozhrani/pro-konzumenty.md` už existují z P7 a P8 —
tady se dopíše zbytek.

- [ ] **Krok 3: Testy a commit**

```csharp
[Fact] public async Task Dokumentace_VykresluejeMarkdown()
[Fact] public async Task Dokumentace_NavigaceOdpovidaSlozkam()
[Fact] public async Task Dokumentace_ZadnyMrtvyOdkazVeWiki()
```

Poslední test je levná pojistka: odkaz na neexistující stránku nápovědy je horší
než chybějící stránka, protože vypadá jako by nápověda byla.

```bash
dotnet test Ciselniky.Tests.Api --filter DokumentaceTests
git add -A && git commit -m "feat(wiki): nápověda renderovaná v aplikaci"
```

---

## Blok 7: Living style guide

**Cíl bloku:** Obrazovka O13 — každá komponenta `pm-*` ve všech variantách na jednom místě.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/StyleGuide.tsx`
- Test: `ciselniky-web/src/stranky/StyleGuide.test.tsx`

- [ ] **Krok 1: Stránka**

Pro každý wrapper `pm-*` sekce se všemi jeho variantami a stavy — včetně zakázaného,
načítajícího a chybového. Zdrojem je soupis z
[../20-architektura/03-frontend-react.md](../20-architektura/03-frontend-react.md).

> **K čemu to je.** Při upgradu gov design systemu je tohle jediné místo, kde se
> na jedné obrazovce pozná, co se rozbilo. Bez ní se to zjišťuje proklikáním celé
> aplikace — a najde se to, na co si někdo vzpomene.

- [ ] **Krok 2: Test, že nezůstane pozadu**

```tsx
test('style guide obsahuje sekci pro každý wrapper pm-*', () => {
  const wrappery = import.meta.glob('../komponenty/pm/Pm*.tsx', { eager: true })
  const nazvy = Object.keys(wrappery)
    .map((c) => c.match(/Pm(\w+)\.tsx$/)![1])

  render(<StyleGuide />)
  for (const nazev of nazvy)
    expect(screen.getByTestId(`sekce-${nazev.toLowerCase()}`)).toBeInTheDocument()
})
```

> Bez tohohle testu style guide zestárne během měsíce — nový wrapper se do ní
> nikdo nepřidat nevzpomene a ona přestane být tím jediným místem.

- [ ] **Krok 3: Ověř a commitni**

```bash
cd ciselniky-web && npx vitest run src/stranky/StyleGuide.test.tsx && cd ..
git add -A && git commit -m "feat(web): living style guide komponent pm-*"
```

---

## Blok 8: Verze aplikace a changelog

**Cíl bloku:** Aplikace má verzi **1.0** a doložený changelog.

- [ ] **Krok 1: Podklady changelogu**

`docs/changelog/releases/1.0.md` se sekcemi **Přidáno / Změněno / Opraveno**.
Kořenový `CHANGELOG.md` se **generuje skriptem**, needituje se ručně.

- [ ] **Krok 2: Povýšení verze**

V `Directory.Build.props`: `<Version>1.0.0</Version>`.
Test z P1 (`VerzeTests`) ověří, že podklad changelogu pro aktuální verzi existuje.

> Povýšení verze je vždy **samostatný závěrečný blok**, ne poznámka u jiné práce.
> Jinak se zapomene a vydá se verze bez doloženého obsahu.

- [ ] **Krok 3: Doplň technickou dokumentaci**

Do `docs/technical/` se dopíší dokumenty, které vznikly až provozní zkušeností
z implementace: instalace a nasazení, bootstrap databáze a migrace, bezpečnost
a autorizace, provozní postupy, testování, řešení potíží.

Každý má povinné sekce: Účel · Publikum a role · Závislosti a předpoklady ·
Vstupy a výstupy · Detailní postup · Verifikace · Rollback · Troubleshooting ·
Audit a traceability.

- [ ] **Krok 4: Commit**

```bash
dotnet test Ciselniky.sln
git add -A && git commit -m "chore(release): verze 1.0 a technická dokumentace"
```

---

## Blok 9: Závěrečné ověření etapy 1

**Cíl bloku:** Doložit, že aplikace umí to, co zadání slíbilo.

- [ ] **Krok 1: Projdi schopnosti proti kritériím**

Každá schopnost ze [specifikace](../10-specifikace/03-schopnosti.md) má kritérium
hotovosti. Projdi je a doplň k nim důkaz.

| Schopnost | Kritérium | Ověř |
|---|---|---|
| **S1** Udržet číselník včetně víceúrovňového | Hierarchie libovolné hloubky a vazby bez zásahu do schématu | ručně + `HierarchieTests` |
| **S2** Genericita | Nový číselník s dosud nepoužitou strukturou bez řádku kódu a bez migrace | **ruční zkouška níže** |
| **S5** Uživatelské prostředí | Neznámý doménový uživatel se dostane k číselníkům bez zakládání účtu | `V1AutentizaceTests`, ručně |
| **S6** Referenční zdroj | Jedno rozhraní obslouží plochý i víceúrovňový číselník; nový číselník ho nemění | `V1HodnotyTests` |
| Verzování | 1000 hodnot, změna jedné → jeden řádek | `ZmenaZapisovacTests` |

*(S3 a S4 — čtení z vnějších zdrojů — jsou etapa 2.)*

- [ ] **Krok 2: Zkouška genericity — nejdůležitější ruční ověření celé etapy**

> Založ **úplně nový číselník se strukturou, jaká v aplikaci ještě nebyla** — jiné
> atributy, jiné typy, jinou vazbu. Naplň ho, publikuj, přečti přes `/api/v1`.
>
> **Nesmí přitom vzniknout jediný řádek nového kódu, žádná migrace databáze
> a žádná změna rozhraní.**
>
> Když to projde, je splněný požadavek, kvůli kterému celá aplikace vznikla:
> **jedna webová služba pro všech ~50 číselníků**, ne padesát služeb.

- [ ] **Krok 3: Projdi průchody**

Všech sedm z [12-prochazeni.md](../10-specifikace/12-prochazeni.md) — W0 až W7 —
proklikej ručně a ověř, že sedí s tím, co je napsané.

- [ ] **Krok 4: Plná sada a uzavření**

```bash
dotnet test Ciselniky.sln
cd ciselniky-web && npx vitest run && cd ..
```

```bash
git add -A && git commit -m "chore: uzavření etapy 1"
```

---

## Po dokončení P9 ručně ověř

1. Přiděl kolegovi roli — v **auditním logu** je záznam se jménem, akcí a časem.
2. Odeber silou cizí zámek — v auditu je taky.
3. Zkus najít v auditu záznam o tom, že si někdo číselník **přečetl** — **žádný není**.
4. Klikni `Tisk` — otevře se **PDF v nové kartě** s patičkou *Strana 1 z N*,
   v hlavičce kód, název, verze a datum vydání.
5. Vyhledej hodnotu, kterou znáš z **jiného** číselníku, než ve kterém právě jsi —
   výsledek na ni vede.
6. Zastav OpenSearch — aplikace **běží dál**, jen zmizí vyhledávací pole.
7. V profilu přepni vzhled; volba přežije odhlášení.
8. Otevři nápovědu a projdi ji — žádný odkaz nevede nikam.
9. **Zkouška genericity z bloku 9.**
10. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D20 | Cesta k prohlížeči pro sazbu PDF je v nastavení, ale její platnost se při startu neověřuje. Chybná cesta se projeví až prvním tiskem. Zvážit kontrolu při startu. | *otevřeno* |
