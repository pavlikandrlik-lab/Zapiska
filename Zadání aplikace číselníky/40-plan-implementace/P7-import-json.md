# P7 — Import ze souboru JSON: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Číselník jde naplnit ze souboru JSON. Soubor se **celý ověří dřív, než se
cokoli uloží**, a výsledek nekončí v databázi, ale v editační tabulce z P6.

**Architektura:** Import **není samostatná cesta**, je to **druhý vstup do téže editace**
(rozhodnutí E2). Nahraný soubor se převede na návrhy položek a předá se `ZmenaZapisovac`
z P5 — tedy přesně tomu, co obsluhuje ruční editaci.

**Stack:** .NET 10, `System.Text.Json`, React + TypeScript, xUnit.

**Specifikace:** [../10-specifikace/06-import-json.md](../10-specifikace/06-import-json.md) ·
**Wireframy:** [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) (O6) ·
**Průchod:** [../10-specifikace/12-prochazeni.md](../10-specifikace/12-prochazeni.md) (W4) ·
**Postupy:** [../10-specifikace/14-postupy-uzivatelu.md](../10-specifikace/14-postupy-uzivatelu.md) —
import je cesta, kterou se velký číselník založí **4 kliky** bez ohledu na počet hodnot

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P6 — editační mřížka, zámek. **Uzavírá dluh D16 z P6.**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Formát souboru je popsaný a strojově ověřitelný |
| 2 | Soubor se načte a vypíšou se **všechny** nalezené chyby najednou |
| 3 | Ověřený soubor se převede na návrhy položek a předá editaci |
| 4 | Tři režimy: založení, doplnění, sesouhlasení |
| 5 | Koncové body: ověřit a promítnout |
| 6 | Obrazovka O6 — tři kroky s náhledem dopadu |
| 7 | Našeptávač cílových položek vazby — uzavírá D16 |

---

## Proč import nekončí uložením

Většina číselníků dnes existuje jen v PDF směrnicích. Zadavatel je do aplikace dostane tak,
že **nechá jazykový model přepsat PDF do dohodnutého tvaru JSON**. Přepis tisícovky řádků
je z podstaty místo, kde vzniknou chyby.

Proto:

```
nahrání → ověření celého souboru → náhled dopadu → promítnutí do editační tabulky
                                                    ↓
                                          uživatel může ještě sáhnout do dat
                                                    ↓
                                          Uložit  /  Publikovat verzi
```

**Soubor se do databáze nikdy nedostane přímo.** Jediná obrana proti tichému rozbití dat
překlepem v přepisu je ukázat výsledek dřív, než se uloží.

---

## Blok 1: Formát souboru

**Cíl bloku:** Formát je popsaný, verzovaný a strojově ověřitelný.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Import/ImportModely.cs`
- Vytvoř: `docs/specs/format-importu-1.0.json` (JSON Schema samotného formátu)
- Vytvoř: `docs/wiki/editace/format-importu.md`
- Test: `Ciselniky.Tests.Unit/Import/ImportModelyTests.cs`

**Rozhraní:**
- Poskytuje: `ImportSoubor`, `ImportCiselnik`, `ImportAtribut`, `ImportVazba`, `ImportPolozka`.

- [ ] **Krok 1: Modely formátu**

`Ciselniky.Core/Services/Import/ImportModely.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Ciselniky.Core.Services.Import;

public sealed record ImportSoubor(
    [property: JsonPropertyName("formatVerze")] string FormatVerze,
    [property: JsonPropertyName("ciselnik")]    ImportCiselnik Ciselnik,
    [property: JsonPropertyName("polozky")]     ImportPolozka[] Polozky);

public sealed record ImportCiselnik(
    string Kod, string Nazev, string? Popis, string? ZdrojUdaju,
    bool Hierarchicky, ImportAtribut[] Atributy, ImportVazba[] Vazby);

public sealed record ImportAtribut(
    string Kod, string Nazev, string Typ, bool Povinny, string[]? Vycet);

public sealed record ImportVazba(
    string Kod, string Nazev, string CilovyCiselnik, bool Povinna);

public sealed record ImportPolozka(
    string Kod, string Nazev, string? NadrazenyKod,
    string? PlatnostOd, string? PlatnostDo,
    Dictionary<string, string?>? Atributy,
    Dictionary<string, string>? Vazby);
```

> **Ve `vazby` se uvádí kód cílové položky, ne vnořený objekt.** Soubor tak zůstane
> čitelný a přepsatelný — jazykový model, který přepisuje vyhlášku, nemá skládat
> zanořené struktury, ale opisovat hodnoty.

> **Datum je řetězec, ne datový typ.** Chybný tvar data má vypadnout jako **chyba ověření
> s uvedením položky**, ne jako pád rozbalování souboru bez kontextu. Rozbalování je
> záměrně shovívavé, ověřování přísné.

- [ ] **Krok 2: Číslo verze formátu**

`formatVerze` je `"1.0"`. Odmítne se všechno ostatní — s hláškou, která uvede,
jaké verze aplikace umí.

Formát je **veřejný závazek**: zadavatel podle něj zadává přepis PDF a přepis vzniká
dřív, než se soubor nahraje. Tichá změna tvaru by znehodnotila hotovou práci.

- [ ] **Krok 3: Popis formátu do wiki**

`docs/wiki/editace/format-importu.md` obsahuje **úplný komentovaný příklad** ze
[specifikace](../10-specifikace/06-import-json.md) a tabulku pravidel. Tenhle soubor
se dává jazykovému modelu jako zadání přepisu, takže musí být samonosný.

- [ ] **Krok 4: Testy rozbalení a commit**

```csharp
[Fact] public void Rozbal_UplnySoubor_NactePolozkyIDefinici()
[Fact] public void Rozbal_ChybejiciNepovinnaPole_NevadiI()
[Fact] public void Rozbal_NeznamaVerzeFormatu_Odmitne()
[Fact] public void Rozbal_PoskozenyJson_VratiSrozumitelnouChybu()
```

```bash
dotnet test Ciselniky.Tests.Unit --filter ImportModelyTests   # Passed: 4
git add -A && git commit -m "feat(import): formát souboru a jeho popis"
```

---

## Blok 2: Ověření souboru

**Cíl bloku:** Vypíšou se **všechny** nalezené chyby najednou, ne první a konec.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Import/ImportOverovac.cs`
- Test: `Ciselniky.Tests.Integration/Import/ImportOverovacTests.cs`

**Rozhraní:**
- Poskytuje: `ImportOverovac.OverAsync(ImportSoubor, int? ciselnikId, CancellationToken) → Task<VysledekOvereni>`.

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Overeni_VypiseVsechnyChybyNajednou_NeJenPrvni()
[Fact] public async Task Overeni_DuplicitniKod_UvedeKterý()
[Fact] public async Task Overeni_NeznamyTypAtributu_Odmitne()
[Fact] public async Task Overeni_ChybejiciPovinnyAtribut_UvedePolozkuIAtribut()
[Fact] public async Task Overeni_HodnotaMimoVycet_UvedePripustneHodnoty()
[Fact] public async Task Overeni_NecisloVCiselnemAtributu_Odmitne()
[Fact] public async Task Overeni_NadrazenyKodNikamNevede_Odmitne()
[Fact] public async Task Overeni_CyklusVHierarchii_Odmitne()
[Fact] public async Task Overeni_VazbaNaNeexistujiciPolozku_UvedeCiselnikIKod()
[Fact] public async Task Overeni_VazbaNaNeexistujiciCiselnik_Odmitne()
[Fact] public async Task Overeni_CistySoubor_VratiSouhrnDopadu()
```

První test je ten hlavní. Soubor se čtyřmi různými chybami musí vrátit **čtyři chyby**,
ne jednu. Kdo přepisuje tisíc řádků z vyhlášky, potřebuje vidět všechno naráz —
jinak opravuje jednu chybu za nahrání.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter ImportOverovacTests
```

- [ ] **Krok 3: Ověřovač**

`Ciselniky.Core/Services/Import/ImportOverovac.cs`:

```csharp
using System.Globalization;
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Ciselniky.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Import;

public sealed record SouhrnDopadu(int Pridanych, int BezeZmeny, int Zmenenych, int Vyrazenych);

public sealed record VysledekOvereni(
    IReadOnlyList<ChybaOvereni> Chyby, SouhrnDopadu? Dopad)
{
    public bool JeVPoradku => Chyby.Count == 0;
}

public sealed class ImportOverovac(CiselnikyDbContext db)
{
    public async Task<VysledekOvereni> OverAsync(
        ImportSoubor soubor, int? ciselnikId, CancellationToken ct = default)
    {
        var chyby = new List<ChybaOvereni>();

        OverJedinecnostKodu(soubor, chyby);
        OverHierarchii(soubor, chyby);
        await OverAtributyAsync(soubor, chyby, ct);
        await OverVazbyAsync(soubor, chyby, ct);

        // Dopad se počítá jen u čistého souboru. Nad daty, o kterých víme,
        // že jsou vadná, by to bylo číslo bez významu.
        var dopad = chyby.Count == 0 && ciselnikId is not null
            ? await SpocitejDopadAsync(soubor, ciselnikId.Value, ct)
            : null;

        return new VysledekOvereni(chyby, dopad);
    }

    private static void OverJedinecnostKodu(ImportSoubor soubor, List<ChybaOvereni> chyby)
    {
        foreach (var skupina in soubor.Polozky.GroupBy(p => p.Kod).Where(g => g.Count() > 1))
            chyby.Add(new(skupina.Key, "kod",
                $"Kód {skupina.Key} se v souboru opakuje {skupina.Count()}×."));
    }

    private static void OverHierarchii(ImportSoubor soubor, List<ChybaOvereni> chyby)
    {
        var kody = soubor.Polozky.Select(p => p.Kod).ToHashSet();
        var rodice = soubor.Polozky
            .Where(p => p.NadrazenyKod is not null)
            .ToDictionary(p => p.Kod, p => p.NadrazenyKod!);

        foreach (var (kod, rodic) in rodice)
            if (!kody.Contains(rodic))
                chyby.Add(new(kod, "nadrazenyKod",
                    $"Nadřazená položka {rodic} v souboru není."));

        foreach (var kod in rodice.Keys)
        {
            var navstivene = new HashSet<string> { kod };
            var uzel = rodice[kod];

            while (rodice.TryGetValue(uzel, out var vyse))
            {
                if (!navstivene.Add(uzel))
                {
                    chyby.Add(new(kod, "nadrazenyKod",
                        "Hierarchie se zacykluje — položka je nakonec podřízená sama sobě."));
                    break;
                }
                uzel = vyse;
            }
        }
    }

    private async Task OverAtributyAsync(
        ImportSoubor soubor, List<ChybaOvereni> chyby, CancellationToken ct)
    {
        var definice = soubor.Ciselnik.Atributy.ToDictionary(a => a.Kod);

        foreach (var atribut in soubor.Ciselnik.Atributy)
            if (!Enum.TryParse<TypAtributu>(atribut.Typ.Replace("_", ""), true, out _))
                chyby.Add(new(null, atribut.Kod,
                    $"Typ „{atribut.Typ}\" neznáme. Přípustné jsou: "
                    + "text, cislo, datum, ano_ne, vycet."));

        foreach (var polozka in soubor.Polozky)
        {
            var hodnoty = polozka.Atributy ?? new Dictionary<string, string?>();

            foreach (var atribut in soubor.Ciselnik.Atributy)
            {
                hodnoty.TryGetValue(atribut.Kod, out var hodnota);

                if (atribut.Povinny && string.IsNullOrWhiteSpace(hodnota))
                {
                    chyby.Add(new(polozka.Kod, atribut.Kod,
                        $"Atribut „{atribut.Nazev}\" je povinný a chybí."));
                    continue;
                }

                if (hodnota is null) continue;
                OverHodnotu(polozka.Kod, atribut, hodnota, chyby);
            }

            foreach (var neznamy in hodnoty.Keys.Where(k => !definice.ContainsKey(k)))
                chyby.Add(new(polozka.Kod, neznamy,
                    $"Číselník nemá atribut „{neznamy}\"."));
        }
    }

    private static void OverHodnotu(string kodPolozky, ImportAtribut atribut,
                                    string hodnota, List<ChybaOvereni> chyby)
    {
        switch (atribut.Typ.ToLowerInvariant())
        {
            case "cislo" when !decimal.TryParse(hodnota, NumberStyles.Any,
                                                CultureInfo.InvariantCulture, out _):
                chyby.Add(new(kodPolozky, atribut.Kod,
                    $"„{hodnota}\" není číslo."));
                break;

            case "datum" when !DateOnly.TryParseExact(hodnota, "yyyy-MM-dd", out _):
                chyby.Add(new(kodPolozky, atribut.Kod,
                    $"„{hodnota}\" není datum ve tvaru RRRR-MM-DD."));
                break;

            case "ano_ne" when hodnota is not ("true" or "false"):
                chyby.Add(new(kodPolozky, atribut.Kod,
                    $"„{hodnota}\" není ano ani ne. Použijte true nebo false."));
                break;

            case "vycet" when atribut.Vycet is { } vycet && !vycet.Contains(hodnota):
                chyby.Add(new(kodPolozky, atribut.Kod,
                    $"„{hodnota}\" není mezi přípustnými hodnotami: "
                    + string.Join(", ", vycet) + "."));
                break;
        }
    }
}
```

- [ ] **Krok 4: Výpočet dopadu**

Dopad se počítá porovnáním souboru s publikovaným stavem — **stejnou úvahou, jakou
pak provede zapisovač**, jen bez zápisu:

```csharp
private async Task<SouhrnDopadu> SpocitejDopadAsync(
    ImportSoubor soubor, int ciselnikId, CancellationToken ct)
{
    var publikovane = await db.Polozky.AsNoTracking()
        .Where(p => p.CiselnikId == ciselnikId)
        .ToDictionaryAsync(p => p.Kod, ct);

    var vSouboru = soubor.Polozky.Select(p => p.Kod).ToHashSet();

    var pridanych = soubor.Polozky.Count(p => !publikovane.ContainsKey(p.Kod));
    var vyrazenych = publikovane.Keys.Count(kod => !vSouboru.Contains(kod));

    var zmenenych = 0;
    foreach (var polozka in soubor.Polozky)
        if (publikovane.TryGetValue(polozka.Kod, out var puvodni)
            && await LisiSeAsync(puvodni, polozka, ct))
            zmenenych++;

    return new SouhrnDopadu(
        pridanych,
        BezeZmeny: soubor.Polozky.Length - pridanych - zmenenych,
        zmenenych,
        vyrazenych);
}
```

> **Číslo „beze změny" tam patří stejně jako ostatní.** Když vyjde nulové u souboru,
> který má být aktualizací, něco je špatně s klíčem identity — a uživatel to pozná
> dřív, než si vyrobí verzi, ve které se změnilo úplně všechno.

> **Každá hláška říká, co je špatně, a pokud možno i co je správně.**
> „Není mezi přípustnými hodnotami: A, U, N" je návod. „Ověření selhalo" není.
> Chyba, kterou uživatel nedokáže opravit bez hádání, je poloviční chyba navíc.

- [ ] **Krok 5: Ověření vazeb proti databázi**

Vazba se ověřuje **proti skutečným datům**, protože cílem je položka jiného číselníku:

```csharp
private async Task OverVazbyAsync(
    ImportSoubor soubor, List<ChybaOvereni> chyby, CancellationToken ct)
{
    foreach (var vazba in soubor.Ciselnik.Vazby)
    {
        var cilovy = await db.Ciselniky.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Kod == vazba.CilovyCiselnik && c.Aktivni, ct);

        if (cilovy is null)
        {
            chyby.Add(new(null, vazba.Kod,
                $"Cílový číselník „{vazba.CilovyCiselnik}\" neexistuje nebo je vyřazený."));
            continue;
        }

        var kodyCilu = (await db.Polozky.AsNoTracking()
            .Where(p => p.CiselnikId == cilovy.Id).Select(p => p.Kod).ToListAsync(ct))
            .ToHashSet();

        foreach (var polozka in soubor.Polozky)
        {
            if (polozka.Vazby?.TryGetValue(vazba.Kod, out var cilovyKod) != true) 
            {
                if (vazba.Povinna)
                    chyby.Add(new(polozka.Kod, vazba.Kod,
                        $"Vazba „{vazba.Nazev}\" je povinná a chybí."));
                continue;
            }

            if (!kodyCilu.Contains(cilovyKod!))
                chyby.Add(new(polozka.Kod, vazba.Kod,
                    $"Položka „{cilovyKod}\" v číselníku {cilovy.Nazev} neexistuje."));
        }
    }
}
```

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter ImportOverovacTests   # Passed: 11
git add -A && git commit -m "feat(import): ověření souboru se všemi chybami najednou"
```

---

## Blok 3: Převod na návrhy položek

**Cíl bloku:** Ověřený soubor se promítne do **rozpracovaných změn** týmž zapisovačem,
jaký obsluhuje ruční editaci.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Import/ImportPrevodnik.cs`
- Test: `Ciselniky.Tests.Integration/Import/ImportPrevodnikTests.cs`

**Rozhraní:**
- Poskytuje: `ImportPrevodnik.PromitniAsync(ImportSoubor, int ciselnikId, RezimImportu, int kdoId, CancellationToken)`.
- Používá: `ZmenaZapisovac.UlozAsync` z P5.

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Promitni_NezmenenaData_NezalozíZadnouZmenu()
[Fact] public async Task Promitni_JednaZmenenaHodnota_ZalozíJedinouZmenu()
[Fact] public async Task Promitni_NovaPolozka_ZalozíZmenuPridano()
[Fact] public async Task Promitni_NesahnaNaPublikovanyStav()
[Fact] public async Task Promitni_PouzijeTyzZapisovacJakoRucniEditace()
```

První test je podstatný: **soubor identický s obsahem číselníku nesmí založit nic.**
Bez toho by opakované nahrání téhož souboru vyrábělo prázdné verze.

- [ ] **Krok 2: Spusť, ověř pád, doplň převodník**

Převodník sestaví `NavrhPolozky[]` z P5 a zavolá `ZmenaZapisovac.UlozAsync`.
Rozdíly nepočítá — to dělá zapisovač.

> **Tohle je celý smysl rozhodnutí E2.** Import a ruční editace nejsou dvě cesty,
> ale jeden tok se dvěma vstupy. Kdyby import počítal rozdíly sám, byly by dvě
> implementace téhož a rozešly by se.

- [ ] **Krok 3: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter ImportPrevodnikTests   # Passed: 5
git add -A && git commit -m "feat(import): převod souboru na rozpracované změny"
```

---

## Blok 4: Režimy importu

**Cíl bloku:** Tři režimy podle specifikace.

**Soubory:**
- Uprav: `Ciselniky.Core/Services/Import/ImportPrevodnik.cs`
- Test: `Ciselniky.Tests.Integration/Import/RezimyImportuTests.cs`

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Zalozeni_NeexistujiciCiselnik_VytvoríHoIsDefinici()
[Fact] public async Task Zalozeni_ExistujiciCiselnik_Odmitne()
[Fact] public async Task Doplneni_ExistujiciPolozkyNechaByt()
[Fact] public async Task Sesouhlaseni_ChybejiciPolozky_VyradiUkoncenimPlatnosti()
[Fact] public async Task Sesouhlaseni_ChybejiciPolozky_Nesmaze()
[Fact] public async Task Import_ExterniCiselnik_Odmitne()
```

Předposlední test drží pravidlo, které platí v celé aplikaci: **nic se nemaže.**
Sesouhlasení, které by mazalo, by z referenčního zdroje udělalo nespolehlivý zdroj.

Poslední test drží rozhodnutí E3: **externí číselník bere data výhradně od svého
konektoru**, ne z nahraného souboru.

- [ ] **Krok 2: Spusť, ověř pád, doplň režimy**

```csharp
public enum RezimImportu
{
    /// <summary>Číselník neexistuje. Vznikne včetně definice.</summary>
    Zalozeni,
    /// <summary>Přidá nové položky, existující nechá být.</summary>
    Doplneni,
    /// <summary>Porovná soubor s publikovaným stavem. Co v souboru není, vyřadí ukončením platnosti.</summary>
    Sesouhlaseni
}
```

> **Ani jeden režim sám o sobě nic nepublikuje.** Vždy vzniknou rozpracované změny
> a o publikování rozhoduje člověk (rozhodnutí V1).

- [ ] **Krok 3: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter RezimyImportuTests   # Passed: 6
git add -A && git commit -m "feat(import): režimy založení, doplnění a sesouhlasení"
```

---

## Blok 5: Koncové body

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/ImportController.cs`
- Test: `Ciselniky.Tests.Api/ImportControllerTests.cs`

- [ ] **Krok 1: Dva kroky, dvě adresy**

```csharp
[HttpPost("internal/ciselniky/{kod}/import/overit")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.ImportJson)]
[RequestSizeLimit(20 * 1024 * 1024)]
public Task<IActionResult> Overit(string kod, IFormFile soubor, CancellationToken ct);

[HttpPost("internal/ciselniky/{kod}/import/promitnout")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.ImportJson)]
[RequestSizeLimit(20 * 1024 * 1024)]
public Task<IActionResult> Promitnout(string kod, IFormFile soubor,
                                      [FromForm] RezimImportu rezim, CancellationToken ct);
```

> **Dvoukrokovost je záměrná a soubor se posílá dvakrát.** Alternativa — uložit ověřený
> soubor na serveru mezi kroky — by znamenala dočasné úložiště, jeho úklid a otázku,
> co s ním při pádu aplikace. Soubor má nejvýš jednotky megabajtů; poslat ho podruhé
> je levnější než provozovat mezisklad.

`Promitnout` ověří soubor **znovu**. Mezi kroky se mohl změnit cílový číselník
a promítnutí neověřeného souboru je přesně to, čemu se celý blok vyhýbá.

- [ ] **Krok 2: Testy**

```csharp
[Fact] public async Task Overit_BezPrava_Odmitne()
[Fact] public async Task Overit_SPravemNaJinyCiselnik_Odmitne()
[Fact] public async Task Overit_VadnySoubor_Vrati422SeVsemiChybami()
[Fact] public async Task Overit_CistySoubor_VratiSouhrnDopadu()
[Fact] public async Task Promitnout_BezZamku_Vrati409()
[Fact] public async Task Promitnout_PrilisVelkySoubor_Vrati413()
```

- [ ] **Krok 3: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter ImportControllerTests   # Passed: 6
git add -A && git commit -m "feat(api): koncové body ověření a promítnutí importu"
```

---

## Blok 6: Obrazovka importu

**Cíl bloku:** Tři kroky podle wireframu O6.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/ImportJson.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/KrokyImportu.tsx`, `TabulkaChyb.tsx`
- Test: `ciselniky-web/src/komponenty/TabulkaChyb.test.tsx`

- [ ] **Krok 1: Ukazatel kroků**

```
①  Nahrát soubor   ──   ②  Zkontrolovat   ──   ③  Promítnout do tabulky
```

Číslování je tu na místě, protože **obsah opravdu je posloupnost** — nedá se zkontrolovat
soubor, který není nahraný.

- [ ] **Krok 2: Nahrání**

Přetažení souboru i výběr tlačítkem. Pod tím **odkaz na popis formátu** z wiki —
kdo neví, jaký tvar má soubor mít, se to má dozvědět tady, ne hledáním.

- [ ] **Krok 3: Tabulka chyb**

Sloupce **položka · atribut · co je špatně**. Všechny chyby najednou.

```tsx
<table className="chyby">
  <thead><tr><th>Položka</th><th>Atribut</th><th>Co je špatně</th></tr></thead>
  <tbody>
    {chyby.map((ch, i) => (
      <tr key={i}>
        <td>{ch.polozka ?? '—'}</td>
        <td>{ch.atribut ?? '—'}</td>
        <td>{ch.zprava}</td>
      </tr>
    ))}
  </tbody>
</table>
```

Nadpis říká počet a co dělat: *„Soubor obsahuje 4 chyby. Opravte je a nahrajte soubor znovu."*
Ne „Ověření selhalo".

- [ ] **Krok 4: Náhled dopadu**

```tsx
<div className="dopad">
  <span><strong>{d.pridanych}</strong> přidaných</span>
  <span><strong>{d.bezeZmeny}</strong> beze změny</span>
  <span><strong>{d.zmenenych}</strong> změněné</span>
  <span><strong>{d.vyrazenych}</strong> vyřazených</span>
</div>
```

U číselníku o tisíci hodnotách je tohle jediná obrana proti tichému rozbití dat.
Číslo **beze změny** tam patří taky — když je nulové, něco je špatně s klíčem identity.

- [ ] **Krok 5: Promítnutí do editace**

Krok 3 **nekončí uložením**, ale přechodem na editační obrazovku z P6
se zvýrazněnými změnami. Uživatel může ještě sáhnout do dat, než uloží.

- [ ] **Krok 6: Testy a commit**

```tsx
test('vypíše všechny chyby, ne jen první')
test('nadpis uvádí počet chyb')
test('náhled dopadu ukáže i počet beze změny')
```

```bash
cd ciselniky-web && npx vitest run && cd ..
git add -A && git commit -m "feat(web): obrazovka importu se třemi kroky a náhledem dopadu"
```

---

## Blok 7: Našeptávač cílových položek

**Cíl bloku:** Buňka vazby nenačítá celý cílový číselník. **Uzavírá dluh D16 z P6.**

**Soubory:**
- Uprav: `Ciselniky.Api/Controllers/Vnitrni/CiselnikyController.cs`
- Uprav: `ciselniky-web/src/komponenty/BunkaVazby.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/Napovedac.tsx`
- Test: `Ciselniky.Tests.Api/NapovedacTests.cs`

- [ ] **Krok 1: Koncový bod**

```csharp
[HttpGet("internal/ciselniky/{kod}/polozky/napoveda")]
[AllowAnonymous]
public async Task<IActionResult> Napoveda(
    string kod, [FromQuery] string hledat, [FromQuery] int limit = 20,
    CancellationToken ct = default)
```

Vrací nejvýš 20 položek, hledá v kódu i názvu, jen platné k dnešku.

- [ ] **Krok 2: Napovedac v prohlížeči**

Odesílá dotaz **až po 300 ms bez psaní** a **až od dvou znaků**:

```tsx
useEffect(() => {
  if (dotaz.length < 2) { setNavrhy([]); return }
  const casovac = setTimeout(() => void nactiNavrhy(dotaz), 300)
  return () => clearTimeout(casovac)
}, [dotaz])
```

> Bez prodlevy by každý stisk klávesy znamenal dotaz do databáze. Bez dolní meze dvou
> znaků by první písmeno vracelo skoro celý číselník — tedy přesně to, čemu se
> našeptávač vyhýbá.

- [ ] **Krok 3: Zachovat výběr ze seznamu**

Cíl vazby se **pořád vybírá, nepíše**. Našeptávač jen zúží nabídku;
volné psaní kódu zůstává vyloučené.

- [ ] **Krok 4: Testy a commit**

```csharp
[Fact] public async Task Napoveda_HledaVKoduINazvu()
[Fact] public async Task Napoveda_VraciNejvyseLimit()
[Fact] public async Task Napoveda_VraciJenPlatneKDnesku()
```

```bash
dotnet test Ciselniky.Tests.Api --filter NapovedacTests   # Passed: 3
cd ciselniky-web && npx vitest run && cd ..
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(web): našeptávač cílových položek vazby"
```

---

## Po dokončení P7 ručně ověř

1. Připrav soubor JSON se **čtyřmi různými chybami** — duplicitní kód, chybějící povinný
   atribut, hodnota mimo výčet a vazba na neexistující položku.
2. Nahraj ho. Krok 2 vypíše **všechny čtyři** s uvedením položky a atributu.
3. Oprav soubor a nahraj znovu — objeví se náhled dopadu se čtyřmi čísly.
4. Promítni do tabulky. Jsi na editační obrazovce se zvýrazněnými změnami,
   **nic není uloženo**.
5. Uprav ještě jednu hodnotu ručně, klikni `Uložit`, pak `Publikovat verzi`.
6. Nahraj **týž soubor znovu** a promítni. Náhled hlásí **0 změn** —
   opakovaný import nic nevyrábí.
7. Zkus import u číselníku v externím režimu — dostaneš odmítnutí.
8. V editaci napiš do buňky vazby dvě písmena — našeptávač nabídne nejvýš 20 položek.
9. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D17 | Strop velikosti souboru (20 MB) a mez našeptávače (20 položek) jsou natvrdo v kódu. Přesunout do nastavení spolu s D6, D9 a D15. | **P8** |
| D18 | Ověření velkého souboru běží celé v paměti. U souboru o statisících položek by to bylo znát — dokud takový nevznikne, neřešit. | *otevřeno* |
