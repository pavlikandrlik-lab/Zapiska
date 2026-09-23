# P8 — Veřejné rozhraní a nastavení: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Aplikace je referenčním zdrojem číselníků. Jedno generické rozhraní obslouží
všech ~50 číselníků — plochých i víceúrovňových — a přidání nového číselníku ho nemění.

**Architektura:** `/api/v1` je **kontrakt**. Mění se jen povýšením verze rozhraní.
Vydává výhradně **publikovaný** stav; rozpracované změny se sem nikdy nedostanou.
Odpovědi jsou mezipaměťovatelné podle verze číselníku.

**Stack:** .NET 10, ASP.NET Core, `System.Text.Json`, xUnit.

**Specifikace:** [../10-specifikace/05-api-referencni-zdroj.md](../10-specifikace/05-api-referencni-zdroj.md) ·
**Propojená data:** [../20-architektura/06-propojena-data-skos.md](../20-architektura/06-propojena-data-skos.md)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P5 — `HistorickyStav`, `RozdilVerzi`. P4 — `HodnotaCtenar`. P3 — `SchemaGenerator`.
**Uzavírá dluhy D6 (P3), D9 (P4), D15 (P6) a D17 (P7).**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Meze a adresy jsou v nastavení, ne v kódu — uzavírá D6, D9, D15, D17 |
| 2 | Konzument získá seznam číselníků, metadata a popis struktury |
| 3 | Konzument získá hodnoty — v požadované verzi, k datu, plochý i strom |
| 4 | Vazby jde nechat rozbalit; kdo si drží kopii, stáhne si jen rozdíl |
| 5 | Opakovaný dotaz na nezměněný číselník nepřenáší data |
| 6 | Odpověď jde dostat jako propojená data se slovníkem SKOS |
| 7 | Rozhraní je popsané v OpenAPI a chráněné doménovou autentizací |
| 8 | Architektonické testy hlídají, že kontrakt zůstane kontraktem |

---

## Blok 1: Nastavení aplikace

**Cíl bloku:** Meze a adresy se dají změnit bez zásahu do kódu.
**Uzavírá dluhy D6, D9, D15 a D17.**

**Soubory:**
- Vytvoř: `Ciselniky.Core/Nastaveni/CiselnikyNastaveni.cs`
- Uprav: `SchemaGenerator` (D6), `CiselnikyController` (D9), `ZamekSluzba` (D15), `ImportController` (D17)
- Uprav: `Ciselniky.Api/appsettings.example.json`, `Program.cs`
- Test: `Ciselniky.Tests.Unit/Nastaveni/NastaveniTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyNastaveni` vkládané přes `IOptions<CiselnikyNastaveni>`.

- [ ] **Krok 1: Nastavení**

```csharp
namespace Ciselniky.Core.Nastaveni;

public sealed class CiselnikyNastaveni
{
    public const string Sekce = "Ciselniky";

    /// <summary>
    /// Bázová adresa stálých identifikátorů (rozhodnutí Z4, doména FIS).
    /// <b>Od prvního publikování verze je neměnná</b> — konzumenti podle ní drží odkazy.
    /// </summary>
    public required string BazoveUri { get; set; }

    /// <summary>Nejvyšší přípustná velikost stránky hodnot.</summary>
    public int MaxVelikostStranky { get; set; } = 500;

    /// <summary>Doba nečinnosti, po které vyprší zámek editace.</summary>
    public TimeSpan PlatnostZamku { get; set; } = TimeSpan.FromHours(5);

    /// <summary>Nejvyšší přípustná velikost nahrávaného souboru v bajtech.</summary>
    public long MaxVelikostImportu { get; set; } = 20 * 1024 * 1024;

    /// <summary>Kolik položek nejvýš vrátí našeptávač.</summary>
    public int MezNapovedace { get; set; } = 20;
}
```

- [ ] **Krok 2: Napiš padající test, který nepustí aplikaci bez bázové adresy**

```csharp
[Fact] public void Nastaveni_BezBazovehoUri_NespustiAplikaci()
[Fact] public void Nastaveni_BazoveUriSLomitkemNaKonci_Normalizuje()
[Fact] public void Nastaveni_ZapornaVelikostStranky_Odmitne()
```

> **Aplikace bez bázové adresy nesmí nastartovat.** Kdyby se rozeběhla s výchozí hodnotou
> a někdo publikoval verzi, vznikly by identifikátory s adresou, kterou nikdo nezvolil —
> a **ty už nejdou změnit** (rozhodnutí Z4). Chybějící nastavení musí zastavit start,
> ne vyrobit nevratný stav.

- [ ] **Krok 3: Registrace s ověřením při startu**

```csharp
builder.Services.AddOptions<CiselnikyNastaveni>()
    .Bind(builder.Configuration.GetSection(CiselnikyNastaveni.Sekce))
    .Validate(n => !string.IsNullOrWhiteSpace(n.BazoveUri),
              "Ciselniky:BazoveUri musí být vyplněné. Je součástí stálých identifikátorů "
              + "a po prvním publikování verze už nejde změnit.")
    .Validate(n => n.MaxVelikostStranky is > 0 and <= 5000, "Ciselniky:MaxVelikostStranky mimo rozsah.")
    .ValidateOnStart();
```

- [ ] **Krok 4: Splať čtyři dluhy**

| Dluh | Kde bylo natvrdo | Nahradit |
|---|---|---|
| D6 | `SchemaGenerator.Vytvor(..., bazoveUri)` — volající předával řetězec | `nastaveni.Value.BazoveUri` |
| D9 | strop stránky 500 v `CiselnikyController` | `nastaveni.Value.MaxVelikostStranky` |
| D15 | `ZamekSluzba.Platnost` jako statické pole | `nastaveni.Value.PlatnostZamku` |
| D17 | `[RequestSizeLimit(20 MB)]`, mez našeptávače 20 | `MaxVelikostImportu`, `MezNapovedace` |

- [ ] **Krok 5: Ukázkové nastavení a commit**

`appsettings.example.json`:

```json
{
  "Ciselniky": {
    "BazoveUri": "https://ciselniky.<doména-FIS>/id",
    "MaxVelikostStranky": 500,
    "PlatnostZamku": "05:00:00",
    "MaxVelikostImportu": 20971520,
    "MezNapovedace": 20
  }
}
```

`appsettings.json` a `appsettings.Development.json` jsou **gitignorovaná tajemství** —
nikdy se needitují ani nezakládají v repozitáři.

```bash
grep -rn "20 \* 1024 \* 1024\|TimeSpan.FromHours(5)\|500;" Ciselniky.Core Ciselniky.Api   # 0 nálezů
dotnet test Ciselniky.Tests.Unit --filter NastaveniTests
git add -A && git commit -m "refactor(nastaveni): meze a adresy do nastavení aplikace"
```

---

## Blok 2: Seznam číselníků, metadata a popis struktury

**Cíl bloku:** Konzument zjistí, co aplikace nabízí a jaký tvar to má.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Verejne/V1CiselnikyController.cs`
- Vytvoř: `Ciselniky.Api/Kontrakty/V1.cs`
- Test: `Ciselniky.Tests.Api/Verejne/V1MetadataTests.cs`

- [ ] **Krok 1: Tvary odpovědí jsou samostatné typy**

`Ciselniky.Api/Kontrakty/V1.cs` drží **vlastní** záznamy pro veřejné rozhraní.

> **Nesdílejí se s vnitřním rozhraním, ani když vypadají stejně.** Vnitřní tvary se mění
> spolu s frontendem; veřejné jsou kontrakt. Sdílený typ by znamenal, že úprava kvůli
> obrazovce tiše změní to, co dostávají konzumující aplikace.

```csharp
namespace Ciselniky.Api.Kontrakty;

public sealed record V1CiselnikSouhrn(
    string Ciselnik, string Nazev, string? Popis,
    string Verze, DateTimeOffset? VerzeVydana, int PocetHodnot, string Uri);

public sealed record V1Odkaz(string Ciselnik, string Kod, string Uri);

public sealed record V1Polozka(
    string Kod, string Nazev, string Uri,
    DateOnly PlatnostOd, DateOnly? PlatnostDo, string? NadrazenyKod,
    IReadOnlyDictionary<string, object?> Atributy,
    IReadOnlyDictionary<string, object> Vazby);

public sealed record V1StrankaHodnot(
    string Ciselnik, string Nazev, string Verze, DateTimeOffset? VerzeVydana,
    string Uri, int Pocet, IReadOnlyList<V1Polozka> Polozky);
```

- [ ] **Krok 2: Koncové body**

```csharp
[ApiController]
[Route("api/v1")]
[Authorize]                                  // doménová autentizace, viz blok 7
public sealed class V1CiselnikyController : ControllerBase
{
    [HttpGet("ciselniky")]        public Task<IActionResult> Seznam(CancellationToken ct);
    [HttpGet("ciselniky/{kod}")]  public Task<IActionResult> Detail(string kod, CancellationToken ct);
    [HttpGet("ciselniky/{kod}/schema")] public Task<IActionResult> Schema(string kod,
                                            [FromQuery] string? verze, CancellationToken ct);
}
```

> **Nepublikovaný číselník přes rozhraní neexistuje** a vrací `404`, ne prázdný seznam.
> Nedokončený číselník nemá co nabízet (průchod W5), a prázdná odpověď by konzumenta
> nechala v domnění, že číselník je prázdný, ne že ještě nevznikl.

- [ ] **Krok 3: Testy**

```csharp
[Fact] public async Task Seznam_VraciJenPublikovaneCiselniky()
[Fact] public async Task Detail_NepublikovanyCiselnik_Vrati404()
[Fact] public async Task Detail_VyrazenyCiselnik_JePoradDostupny()
[Fact] public async Task Schema_VraciJsonSchemaSBazovymUri()
[Fact] public async Task Schema_StarsiVerze_VraciTehdejsiStrukturu()
```

Třetí test drží pravidlo referenčního zdroje: **vyřazený číselník zůstává dostupný.**
Konzument, který si hodnotu uložil loni, ji musí dohledat.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter V1MetadataTests   # Passed: 5
git add -A && git commit -m "feat(api-v1): seznam číselníků, metadata a popis struktury"
```

---

## Blok 3: Výdej hodnot

**Cíl bloku:** Jedno rozhraní obslouží plochý i víceúrovňový číselník.

**Soubory:**
- Uprav: `V1CiselnikyController.cs`
- Vytvoř: `Ciselniky.Api/Kontrakty/V1Prevodnik.cs`
- Test: `Ciselniky.Tests.Api/Verejne/V1HodnotyTests.cs`

- [ ] **Krok 1: Parametry**

```csharp
[HttpGet("ciselniky/{kod}/polozky")]
public Task<IActionResult> Polozky(
    string kod,
    [FromQuery] string? verze,        // výchozí: aktuální
    [FromQuery] DateOnly? platneK,    // výchozí: bez filtru
    [FromQuery] string tvar = "plochy",   // plochy | strom
    [FromQuery] string? rozbalit,     // výčet vazeb k doplnění celých
    [FromQuery] string? zmenyOd,      // rozdíl proti uvedené verzi
    [FromQuery] int strana = 1,
    [FromQuery] int velikost = 200,
    CancellationToken ct = default);
```

- [ ] **Krok 2: Dva významy slova „víceúrovňový"**

Rozhraní je **rozlišuje**, a v tom je celá genericita:

| Co to je | Jak se vydává |
|---|---|
| **Hierarchie uvnitř číselníku** — cíl má nadřazený cíl | `tvar` — plochý seznam s kódem rodiče, nebo vnořený strom |
| **Vazba na jiný číselník** — cíl má manažera | vždy **odkaz**; parametrem `rozbalit` se nahradí obsahem |

> Kdyby se obojí řešilo vnořováním, měl by každý číselník jiný tvar odpovědi — a tím
> i vlastní službu. **Tohle je důvod, proč celá aplikace existuje.**

Při `tvar=strom` se **stránkování nepoužije**; strom rozdělený na stránky by rozpojil větve.

- [ ] **Krok 3: Napiš testy**

```csharp
[Fact] public async Task Polozky_VychoziTvar_VraciPlochySeznamSKodemRodice()
[Fact] public async Task Polozky_TvarStrom_VnoriPodrizene()
[Fact] public async Task Polozky_TvarStrom_IgnorujeStrankovani()
[Fact] public async Task Polozky_Verze_VraciTehdejsiStav()
[Fact] public async Task Polozky_PlatneK_FiltrujePodleData()
[Fact] public async Task Polozky_VazbaJeOdkaz_NeVnorenyObsah()
[Fact] public async Task Polozky_VelikostNadMez_Osekne()
[Fact] public async Task Polozky_NikdyNevydaRozpracovaneZmeny()
```

Poslední test je nejdůležitější v celém P8 a v bloku 8 se z něj stane
architektonické pravidlo.

- [ ] **Krok 4: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Api --filter V1HodnotyTests
```

- [ ] **Krok 5: Převodník na veřejný tvar**

`Ciselniky.Api/Kontrakty/V1Prevodnik.cs`:

```csharp
namespace Ciselniky.Api.Kontrakty;

public sealed class V1Prevodnik(IOptions<CiselnikyNastaveni> nastaveni)
{
    private string Baze => nastaveni.Value.BazoveUri;

    public V1Polozka Preved(string kodCiselniku, HodnotaPolozky polozka) => new(
        polozka.Kod,
        polozka.Nazev,
        $"{Baze}/{kodCiselniku}/{polozka.Kod}",
        polozka.PlatnostOd,
        polozka.PlatnostDo,
        polozka.NadrazenyKod,
        polozka.Atributy,
        polozka.Vazby.ToDictionary(
            v => v.Key,
            v => (object)(v.Value.Count == 1
                ? Odkaz(v.Value[0])
                : v.Value.Select(Odkaz).ToArray())));

    private V1Odkaz Odkaz(OdkazNaPolozku odkaz)
        => new(odkaz.Ciselnik, odkaz.Kod, $"{Baze}/{odkaz.Ciselnik}/{odkaz.Kod}");
}
```

> **Vazba s jedním cílem se vydává jako objekt, s více cíli jako pole.** Alternativa —
> vždy pole — by konzumenta nutila indexovat i tam, kde je násobnost z definice zaručeně
> jedna. Násobnost je v JSON Schema, takže konzument dopředu ví, co čekat.

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter V1HodnotyTests   # Passed: 8
git add -A && git commit -m "feat(api-v1): výdej hodnot s verzí, platností a tvarem"
```

---

## Blok 4: Rozbalení vazeb a přírůstkové čtení

**Cíl bloku:** Konzument si může nechat doplnit vazby a stáhnout jen rozdíl.

**Soubory:**
- Uprav: `V1Prevodnik.cs`
- Test: `Ciselniky.Tests.Api/Verejne/V1RozbaleniTests.cs`

- [ ] **Krok 1: Rozbalení**

```
GET …/polozky                      → "manazer": { "ciselnik": "osoby", "kod": "O-001", "uri": "…" }
GET …/polozky?rozbalit=manazer     → "manazer": { …celá položka z číselníku osob… }
```

Rozbalit jde jen vazby **o jednu úroveň**. Rekurzivní rozbalování by u provázaných
číselníků skončilo u odpovědi, která obsahuje polovinu databáze — a u cyklu by neskončilo
vůbec.

- [ ] **Krok 2: Přírůstkové čtení**

`zmenyOd=3.12` vrátí jen položky dotčené změnami po verzi 3.12, plus výčet kódů,
které mezitím **zmizely z platnosti**.

```csharp
public sealed record V1Prirustek(
    string Ciselnik, string OdVerze, string DoVerze,
    IReadOnlyList<V1Polozka> Zmenene, IReadOnlyList<string> Vyrazene);
```

> Je to pro konzumenty, kteří si drží vlastní kopii. Bez toho by museli stahovat
> celý číselník kvůli jedné změněné hodnotě — a přesně to je chování, kvůli kterému
> vznikají zastaralé kopie.

- [ ] **Krok 3: Testy**

```csharp
[Fact] public async Task Rozbalit_NahradiOdkazObsahem()
[Fact] public async Task Rozbalit_NeznamouVazbu_Ignoruje()
[Fact] public async Task Rozbalit_NerozbaluejeRekurzivne()
[Fact] public async Task ZmenyOd_VraciJenDotcenePolozky()
[Fact] public async Task ZmenyOd_UvadiVyrazeneKody()
[Fact] public async Task ZmenyOd_StejnaVerze_VraciPrazdnyPrirustek()
```

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter V1RozbaleniTests   # Passed: 6
git add -A && git commit -m "feat(api-v1): rozbalení vazeb a přírůstkové čtení"
```

---

## Blok 5: Mezipaměť

**Cíl bloku:** Opakovaný dotaz na nezměněný číselník nepřenáší data.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Filters/VerzeETagFilter.cs`
- Test: `Ciselniky.Tests.Api/Verejne/V1MezipametTests.cs`

- [ ] **Krok 1: Značka odvozená od verze**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ciselniky.Api.Filters;

/// <summary>
/// Doplní ETag odvozený od verze číselníku a odpoví 304, přišel-li týž v If-None-Match.
/// Verze je pro tenhle účel dokonalá značka: mění se právě tehdy, když se mění data.
/// </summary>
public sealed class VerzeETagFilter(CiselnikSluzba ciselniky) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext kontext, ActionExecutionDelegate dalsi)
    {
        if (kontext.RouteData.Values["kod"]?.ToString() is not { Length: > 0 } kod)
        {
            await dalsi();
            return;
        }

        var verze = await ciselniky.AktualniVerzeAsync(kod, kontext.HttpContext.RequestAborted);
        if (verze is null) { await dalsi(); return; }

        var znacka = Znacka(kod, verze, kontext.HttpContext.Request.QueryString.Value);
        kontext.HttpContext.Response.Headers.ETag = znacka;

        if (kontext.HttpContext.Request.Headers.IfNoneMatch.Contains(znacka))
        {
            kontext.Result = new StatusCodeResult(StatusCodes.Status304NotModified);
            return;
        }

        await dalsi();
    }

    /// <summary>
    /// Značka nese kód, verzi i dotazovací parametry — dva různé výřezy téže verze
    /// nesmějí sdílet jednu značku, jinak by konzument dostal 304 na dotaz,
    /// na který ještě odpověď neviděl.
    /// </summary>
    private static string Znacka(string kod, string verze, string? dotaz)
    {
        var otisk = SHA256.HashData(Encoding.UTF8.GetBytes($"{kod}|{verze}|{dotaz}"));
        return $"\"{Convert.ToHexString(otisk)[..16]}\"";
    }
}
```

Značka se skládá z kódu číselníku, verze a parametrů, které výdej ovlivňují
(`platneK`, `tvar`, `rozbalit`, `strana`, `velikost`) — dva různé výřezy téže verze
nesmějí sdílet jednu značku.

- [ ] **Krok 2: Testy**

```csharp
[Fact] public async Task Odpoved_NeseETag()
[Fact] public async Task Opakovany_DotazSTymzETag_Vrati304BezTela()
[Fact] public async Task PoPublikovaniNoveVerze_SeETagZmeni()
[Fact] public async Task RuzneParametry_MajiRuznyETag()
```

- [ ] **Krok 3: Spusť, ověř pád, doplň filtr, ověř, commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter V1MezipametTests   # Passed: 4
git add -A && git commit -m "feat(api-v1): mezipaměť přes ETag odvozený od verze"
```

---

## Blok 6: Propojená data se slovníkem SKOS

**Cíl bloku:** Odpověď jde dostat jako propojená data podle mezinárodního standardu.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Kontrakty/SkosKontext.cs`
- Test: `Ciselniky.Tests.Api/Verejne/V1JsonLdTests.cs`

- [ ] **Krok 1: Volba tvaru hlavičkou**

| `Accept` | Odpověď |
|---|---|
| `application/json` *(výchozí)* | Tvar z bloku 3. Konzument nemusí o propojených datech nic vědět. |
| `application/ld+json` | Táž data doplněná o `@context` postavený na **SKOS** |

- [ ] **Krok 2: Mapování na SKOS**

```csharp
public static JsonObject Kontext(Ciselnik ciselnik, DefiniceCiselniku definice, string bazoveUri)
{
    var kontext = new JsonObject
    {
        ["skos"]  = "http://www.w3.org/2004/02/skos/core#",
        ["fis"]   = $"{bazoveUri}/slovnik#",
        ["kod"]   = "skos:notation",
        ["nazev"] = "skos:prefLabel",
        ["popis"] = "skos:definition",
        // Hierarchie UVNITŘ číselníku. Nikdy pro vazbu na jiný číselník.
        ["nadrazenyKod"] = new JsonObject
        {
            ["@id"] = "skos:broader", ["@type"] = "@id"
        },
    };

    // Vazba na jiný číselník dostává VLASTNÍ predikát v našem jmenném prostoru.
    // Není to hierarchie ani ztotožnění — je to doménový vztah.
    foreach (var vazba in definice.Vazby)
        kontext[vazba.Kod] = new JsonObject
        {
            ["@id"] = $"fis:{ciselnik.Kod}/{vazba.Kod}", ["@type"] = "@id"
        };

    return kontext;
}
```

> **Závazné pravidlo:** `skos:broader` **výhradně pro hierarchii uvnitř jednoho číselníku.**
> Vazba na jiný číselník dostává vlastní doménový predikát, nikdy `skos:broader`.
>
> „Cíl má manažera" není tvrzení, že cíl je užší pojem než manažer. Je to častá chyba —
> objevila se i v externím doporučení, ze kterého jsme SKOS převzali — a v hotovém
> výstupu se špatně opravuje. Rozbor:
> [../20-architektura/06-propojena-data-skos.md](../20-architektura/06-propojena-data-skos.md).

- [ ] **Krok 3: Testy**

```csharp
[Fact] public async Task JsonLd_ObsahujeKontextSeSkos()
[Fact] public async Task JsonLd_HierarchiiMapujeNaSkosBroader()
[Fact] public async Task JsonLd_VazbuNaJinyCiselnik_NemapujeNaSkosBroader()
[Fact] public async Task JsonLd_VazbaMaVlastniPredikatVJmennemProstoruFis()
[Fact] public async Task BezHlavicky_VraciBeznyJsonBezKontextu()
```

Třetí test je pojistka proti té konkrétní chybě. Je psaný jako **zákaz**, protože
takhle se past nezavře sama.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter V1JsonLdTests   # Passed: 5
git add -A && git commit -m "feat(api-v1): propojená data se slovníkem SKOS"
```

---

## Blok 7: Popis rozhraní a autentizace konzumentů

**Cíl bloku:** Konzument si rozhraní přečte a přihlásí se doménovým účtem.

**Soubory:**
- Uprav: `Program.cs`
- Vytvoř: `docs/wiki/rozhrani/index.md`, `pro-konzumenty.md`
- Test: `Ciselniky.Tests.Api/Verejne/V1AutentizaceTests.cs`

- [ ] **Krok 1: OpenAPI**

Jeden dokument pro celé `/api/v1`. **Statický** — rozhraní je generické, takže se
novým číselníkem nemění. Popis struktury konkrétního číselníku dodává JSON Schema
z bloku 2; nezaměňovat.

- [ ] **Krok 2: Autentizace**

Výhradně **Active Directory** (rozhodnutí Z1). Žádné přístupové klíče ani vlastní
tokeny. Konzumující aplikace se hlásí doménovým servisním účtem.

> **Čtení se nesleduje.** Auditní log pokrývá zápis — kdo co změnil. Kdo si číselník
> přečetl, aplikace neeviduje a evidovat nemá.

- [ ] **Krok 3: Dokumentace pro konzumenty**

`docs/wiki/rozhrani/pro-konzumenty.md` — adresa, přihlášení, příklad volání,
vysvětlení dvojkové verze a doporučený postup: **číst přírůstkově a řídit se hlavním
číslem verze**. Odkaz na tuhle stránku je na záložce Struktura, aby ji vývojář
konzumující aplikace našel (průchod W2).

- [ ] **Krok 4: Testy a commit**

```csharp
[Fact] public async Task V1_BezAutentizace_Odmitne()
[Fact] public async Task V1_SDomenovouIdentitou_Projde()
[Fact] public async Task V1_NevyzadujeZaznamOsobyVAplikaci()
```

Třetí test drží rozhodnutí N1 i pro stroje: **servisní účet nepotřebuje záznam osoby.**
Kdyby ho potřeboval, musel by někdo zakládat osobu pro každou konzumující aplikaci.

```bash
dotnet test Ciselniky.Tests.Api --filter V1AutentizaceTests   # Passed: 3
git add -A && git commit -m "feat(api-v1): OpenAPI a doménová autentizace konzumentů"
```

---

## Blok 8: Architektonické testy kontraktu

**Cíl bloku:** Kontrakt zůstane kontraktem i za rok.

**Soubory:**
- Test: `Ciselniky.Tests.Unit/Architektura/VerejnyKontraktTests.cs`

- [ ] **Krok 1: Napiš testy**

```csharp
public sealed class VerejnyKontraktTests
{
    /// <summary>
    /// Veřejné rozhraní nesmí sáhnout na rozpracované změny. Konzument dostává
    /// výhradně publikovaný stav — jinak by referenční zdroj vydával rozdělanou práci.
    /// </summary>
    [Fact]
    public void VerejneControllery_NepouzivajiPrekryvRozpracovanych()
    {
        var zakazane = new[] { typeof(PrekryvRozpracovanych).FullName! };

        var prohresky = TypyVeJmennemProstoru("Ciselniky.Api.Controllers.Verejne")
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Where(p => zakazane.Contains(p.ParameterType.FullName))
            .Select(p => p.Member.DeclaringType!.Name)
            .Distinct().ToArray();

        Assert.True(prohresky.Length == 0,
            "Veřejné controllery sahající na rozpracovaný stav: " + string.Join(", ", prohresky));
    }

    /// <summary>
    /// Veřejné odpovědi mají vlastní typy. Sdílení s vnitřním rozhraním by znamenalo,
    /// že úprava kvůli obrazovce tiše změní kontrakt konzumentů.
    /// </summary>
    [Fact]
    public void VerejneControllery_VraciJenTypyZJmennehoProstoruKontrakty()
    {
        var prohresky = TypyVeJmennemProstoru("Ciselniky.Api.Controllers.Verejne")
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance
                                          | BindingFlags.DeclaredOnly))
            .SelectMany(NavraceneTypy)
            .Where(t => !t.Namespace?.StartsWith("Ciselniky.Api.Kontrakty") == true
                     && !t.Namespace!.StartsWith("System"))
            .Select(t => t.FullName!)
            .Distinct().ToArray();

        Assert.True(prohresky.Length == 0,
            "Veřejné rozhraní vrací vnitřní typy: " + string.Join(", ", prohresky));
    }

    /// <summary>
    /// Každá adresa veřejného rozhraní začíná /api/v1. Kontrakt se mění povýšením verze,
    /// ne úpravou na místě.
    /// </summary>
    [Fact]
    public void VerejneControllery_MajiVerziVAdrese()
    {
        var prohresky = TypyVeJmennemProstoru("Ciselniky.Api.Controllers.Verejne")
            .Where(t => t.GetCustomAttribute<RouteAttribute>()?.Template?
                         .StartsWith("api/v") != true)
            .Select(t => t.Name).ToArray();

        Assert.True(prohresky.Length == 0,
            "Veřejné controllery bez verze v adrese: " + string.Join(", ", prohresky));
    }
}
```

Pomocníci sdílení všemi třemi testy:

```csharp
    private static IEnumerable<Type> TypyVeJmennemProstoru(string jmennyProstor)
        => typeof(Ciselniky.Api.Kontrakty.V1Polozka).Assembly.GetTypes()
            .Where(t => t.Namespace == jmennyProstor
                     && typeof(ControllerBase).IsAssignableFrom(t));

    /// <summary>Rozbalí Task&lt;T&gt; i ActionResult&lt;T&gt; na skutečně vracený typ.</summary>
    private static IEnumerable<Type> NavraceneTypy(MethodInfo metoda)
    {
        var typ = metoda.ReturnType;
        while (typ.IsGenericType) typ = typ.GetGenericArguments()[0];
        return typ.IsGenericType ? typ.GetGenericArguments() : [typ];
    }
```

> Tyhle tři testy jsou levnější než dohled. Pravidlo, které lze vyjádřit testem,
> se vyjádří testem — jinak ho za rok někdo poruší v dobré víře.

- [ ] **Krok 2: Plná sada a commit**

```bash
dotnet test Ciselniky.sln
git add -A && git commit -m "test(api-v1): architektonické testy veřejného kontraktu"
```

---

## Po dokončení P8 ručně ověř

1. Spusť aplikaci **bez** `Ciselniky:BazoveUri` — **nenastartuje** a řekne proč.
2. Doplň nastavení, spusť, zavolej `GET /api/v1/ciselniky` — vidíš publikované číselníky.
3. `GET /api/v1/ciselniky/cile/polozky` — hodnoty s vazbou jako **odkazem**.
4. `…/polozky?rozbalit=manazer` — odkaz je nahrazený celou položkou z číselníku osob.
5. `…/polozky?tvar=strom` — podřízené cíle jsou vnořené.
6. `…/polozky?verze=1.0` — vidíš stav před poslední změnou.
7. Zopakuj dotaz s hlavičkou `If-None-Match` a značkou z minula — dostaneš **304**
   bez těla.
8. Publikuj novou verzi a zopakuj — značka je jiná a data přijdou.
9. Zavolej s `Accept: application/ld+json` — odpověď má `@context` se SKOS,
   hierarchie je `skos:broader`, vazba **není**.
10. Uprav hodnotu a **nepublikuj**. Veřejné rozhraní vydává pořád starou hodnotu.
11. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D19 | Rozbalení vazby dělá dotaz na cílový číselník pro každou rozbalovanou vazbu. U odpovědi o 200 položkách se dvěma rozbalenými vazbami jsou to dva dotazy navíc — přijatelné. Při více vazbách zvážit načtení najednou. | *otevřeno* |
