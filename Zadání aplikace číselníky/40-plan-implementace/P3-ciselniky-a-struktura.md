# P3 — Číselníky a struktura: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Správce založí číselník a nadefinuje, jaké údaje ponesou jeho položky.
Struktura je **data, ne kód** — padesátý číselník vznikne bez jediného řádku nového kódu
a bez migrace schématu.

**Architektura:** Definice atributů a vazeb je jediný zdroj pravdy. V tomto plánu z ní
vzniká **formulář, validace a JSON Schema pro konzumenty**; v dalších plánech z ní poroste
editační tabulka a výdej rozhraní.

**Stack:** .NET 10, EF Core + SQL Server, React + TypeScript, xUnit.

**Specifikace:** [../10-specifikace/01-domenovy-model.md](../10-specifikace/01-domenovy-model.md),
[03-schopnosti.md](../10-specifikace/03-schopnosti.md) (S1, S2) ·
**Datový model:** [../20-architektura/04-datovy-model.md](../20-architektura/04-datovy-model.md) ·
**Wireframy:** [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) (rámec, O1, O7) ·
**Životní cyklus struktury:** [../10-specifikace/13-zivotni-cyklus-struktury.md](../10-specifikace/13-zivotni-cyklus-struktury.md) —
kdo strukturu určuje a co se s ní smí za provozu dělat (matici vynucuje P6) ·
**Postupy s počtem kliků:** [../10-specifikace/14-postupy-uzivatelu.md](../10-specifikace/14-postupy-uzivatelu.md)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P2 — `ciselniky` (minimální), katalog oprávnění, policy handler.
**Uzavírá dluh D1 z P2.**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Databáze unese definici struktury: atributy, vazby, režim správy, hierarchie |
| 2 | Číselník jde založit, přejmenovat a vyřadit; kód je po publikování neměnný |
| 3 | Atributy a vazby jdou definovat, s validací, která nepustí nesmysl |
| 4 | Z definice vzniká JSON Schema pro konzumenty |
| 5 | Koncové body jsou chráněné a odmítnou toho, kdo nemá rozsah |
| 6 | Rail a seznam číselníků nahradí prázdný rozcestník z P1 |
| 7 | Obrazovka O7 — správce definuje strukturu v prohlížeči |

---

## Klíčové pravidlo tohoto plánu

> **Kód číselníku je po prvním publikování verze neměnný.**

Kód je součástí stálého identifikátoru, který si konzumenti ukládají u sebe
(rozhodnutí Z4). Do prvního publikování se mění volně — číselník tehdy přes rozhraní
neexistuje. Po něm už ne.

Vynucuje se v kódu a hlídá testem v bloku 2. **Není to obtěžující omezení, je to celý
smysl slova „referenční".**

---

## Blok 1: Schéma definice struktury

**Cíl bloku:** Databáze unese strukturu libovolného číselníku bez další migrace.

**Soubory:**
- Vytvoř: `db/db_upgrade_0_3_definice.sql`
- Vytvoř: `Ciselniky.Core/Domain/AtributDefinice.cs`, `VazbaDefinice.cs`
- Uprav: `Ciselniky.Core/Domain/Ciselnik.cs`, `Data/CiselnikyDbContext.cs`, `Data/KontrolaSchematu.cs`
- Test: `Ciselniky.Tests.Integration/Data/DefiniceSchemaTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyDbContext.AtributDefinice`, `.VazbaDefinice`;
  `Ciselnik` s `Popis`, `Hierarchicky`, `RezimSpravy`, `SpravceId`;
  enumy `RezimSpravy`, `TypAtributu`.

- [ ] **Krok 1: Migrační skript**

`db/db_upgrade_0_3_definice.sql`:

```sql
-- Definice struktury číselníku. Struktura je data, ne kód.

ALTER TABLE ciselniky
    ADD COLUMN popis         nvarchar(1024),
    ADD COLUMN hierarchicky  bit     NOT NULL DEFAULT 0,
    ADD COLUMN rezim_spravy  nvarchar(16) NOT NULL DEFAULT 'RUCNI'
        CHECK (rezim_spravy IN ('RUCNI', 'EXTERNI')),
    ADD COLUMN spravce_id    int     REFERENCES osoby(id),
    ADD COLUMN zalozeno_kdy  datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME();

CREATE TABLE atribut_definice (
    id            int      IDENTITY(1,1) PRIMARY KEY,
    ciselnik_id   int      NOT NULL REFERENCES ciselniky(id),
    kod           nvarchar(64)  NOT NULL,
    nazev         nvarchar(256) NOT NULL,
    typ           nvarchar(16)  NOT NULL
        CHECK (typ IN ('TEXT', 'CISLO', 'DATUM', 'ANO_NE', 'VYCET')),
    povinny       bit      NOT NULL DEFAULT 0,
    poradi        int      NOT NULL DEFAULT 0,
    vycet_hodnot  nvarchar(max),                       -- hodnoty oddělené středníkem, jen u typu VYCET
    UNIQUE (ciselnik_id, kod)
);

CREATE TABLE vazba_definice (
    id                  int      IDENTITY(1,1) PRIMARY KEY,
    ciselnik_id         int      NOT NULL REFERENCES ciselniky(id),
    kod                 nvarchar(64)  NOT NULL,
    nazev               nvarchar(256) NOT NULL,
    cilovy_ciselnik_id  int      NOT NULL REFERENCES ciselniky(id),
    povinna             bit      NOT NULL DEFAULT 0,
    poradi              int      NOT NULL DEFAULT 0,
    UNIQUE (ciselnik_id, kod),
    -- Vazba na sebe sama neexistuje. Hierarchii uvnitř číselníku řídí
    -- příznak `hierarchicky`, ne vazba — jsou to dvě různé věci
    -- (viz 20-architektura/06-propojena-data-skos.md).
    CHECK (cilovy_ciselnik_id <> ciselnik_id)
);

CREATE INDEX ix_atribut_definice_ciselnik ON atribut_definice (ciselnik_id, poradi);
CREATE INDEX ix_vazba_definice_ciselnik   ON vazba_definice   (ciselnik_id, poradi);
CREATE INDEX ix_vazba_definice_cil        ON vazba_definice   (cilovy_ciselnik_id);

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.3');
```

> **Kód atributu a kód vazby sdílejí jmenný prostor.** Databáze to sama nevynutí (jsou to
> dvě tabulky) — hlídá se ve službě v bloku 3. Důvod: v odpovědi rozhraní i v importním
> souboru se objevují vedle sebe a stejný kód ve dvou významech je past.

> **Poslední index není zbytečný.** `ix_vazba_definice_cil` odpovídá na otázku
> „na který číselník se odkazuje odjinud" — potřebnou pokaždé, když se má číselník vyřadit.

- [ ] **Krok 2: Napiš padající testy schématu**

`Ciselniky.Tests.Integration/Data/DefiniceSchemaTests.cs`:

```csharp
using Ciselniky.Tests.Integration.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Tests.Integration.Data;

public sealed class DefiniceSchemaTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private static async Task<int> ZalozCiselnik(Ciselniky.Core.Data.CiselnikyDbContext db, string kod)
    {
        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO ciselniky (kod, nazev) VALUES ('{kod}', '{kod}')");
        return await db.Ciselniky.Where(c => c.Kod == kod).Select(c => c.Id).SingleAsync();
    }

    [Fact]
    public async Task Atribut_NemuzeMitDvakratTyzKodVJednomCiselniku()
    {
        await using var db = fixture.VytvorContext();
        var id = await ZalozCiselnik(db, "t-atr-dup");

        await db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO atribut_definice (ciselnik_id, kod, nazev, typ) VALUES ({id}, 'stav', 'Stav', 'TEXT')");

        var vyjimka = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync(
                $"INSERT INTO atribut_definice (ciselnik_id, kod, nazev, typ) VALUES ({id}, 'stav', 'Stav 2', 'TEXT')"));

        Assert.Contains(vyjimka.Number, new[] { 2627, 2601 });
    }

    [Fact]
    public async Task Atribut_PrijimaJenZnameTypy()
    {
        await using var db = fixture.VytvorContext();
        var id = await ZalozCiselnik(db, "t-atr-typ");

        var vyjimka = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync(
                $"INSERT INTO atribut_definice (ciselnik_id, kod, nazev, typ) VALUES ({id}, 'x', 'X', 'BARVA')"));

        Assert.Equal(547, vyjimka.Number);   // kontrolní podmínka
    }

    [Fact]
    public async Task Vazba_NemuzeUkazovatNaSveVlastniCiselnik()
    {
        await using var db = fixture.VytvorContext();
        var id = await ZalozCiselnik(db, "t-vaz-self");

        var vyjimka = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO vazba_definice (ciselnik_id, kod, nazev, cilovy_ciselnik_id)
                VALUES ({id}, 'rodic', 'Nadřazený', {id})
                """));

        Assert.Equal(547, vyjimka.Number);   // kontrolní podmínka
    }

    [Fact]
    public async Task RezimSpravy_PrijimaJenRucniNeboExterni()
    {
        await using var db = fixture.VytvorContext();

        var vyjimka = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync(
                "INSERT INTO ciselniky (kod, nazev, rezim_spravy) VALUES ('t-rezim', 'T', 'HYBRID')"));

        Assert.Equal(547, vyjimka.Number);   // kontrolní podmínka
    }
}
```

> Poslední test drží rozhodnutí E3 na úrovni databáze: **hybrid neexistuje**.
> Kdyby ho někdo chtěl přidat aplikačně, narazí na kontrolní podmínku.

- [ ] **Krok 3: Spusť testy a ověř, že padají**

```bash
dotnet test Ciselniky.Tests.Integration --filter DefiniceSchemaTests
```

Očekávej: FAIL — sloupce a tabulky neexistují.

- [ ] **Krok 4: Doplň entity**

`Ciselniky.Core/Domain/Ciselnik.cs` — rozšíření:

```csharp
namespace Ciselniky.Core.Domain;

public enum RezimSpravy { Rucni, Externi }

public sealed class Ciselnik
{
    public int Id { get; set; }
    public required string Kod { get; set; }
    public required string Nazev { get; set; }
    public string? Popis { get; set; }
    public bool Hierarchicky { get; set; }
    public RezimSpravy RezimSpravy { get; set; } = RezimSpravy.Rucni;
    public int? SpravceId { get; set; }
    public bool Aktivni { get; set; } = true;
    public DateTimeOffset ZalozenoKdy { get; set; }
}
```

`Ciselniky.Core/Domain/AtributDefinice.cs`:

```csharp
namespace Ciselniky.Core.Domain;

public enum TypAtributu { Text, Cislo, Datum, AnoNe, Vycet }

public sealed class AtributDefinice
{
    public int Id { get; set; }
    public int CiselnikId { get; set; }
    public required string Kod { get; set; }
    public required string Nazev { get; set; }
    public TypAtributu Typ { get; set; }
    public bool Povinny { get; set; }
    public int Poradi { get; set; }

    /// <summary>Hodnoty oddělené středníkem. Vyplněné jen u typu <see cref="TypAtributu.Vycet"/>.</summary>
    public string? VycetHodnot { get; set; }

    public IReadOnlyList<string> Vycet =>
        string.IsNullOrWhiteSpace(VycetHodnot)
            ? []
            : VycetHodnot.Split(';', StringSplitOptions.RemoveEmptyEntries
                                   | StringSplitOptions.TrimEntries);
}
```

`Ciselniky.Core/Domain/VazbaDefinice.cs`:

```csharp
namespace Ciselniky.Core.Domain;

public sealed class VazbaDefinice
{
    public int Id { get; set; }
    public int CiselnikId { get; set; }
    public required string Kod { get; set; }
    public required string Nazev { get; set; }
    public int CilovyCiselnikId { get; set; }
    public bool Povinna { get; set; }
    public int Poradi { get; set; }
}
```

V `OnModelCreating` se `TypAtributu.AnoNe` ukládá jako `ANO_NE`, ne `ANONE`:

```csharp
model.Entity<AtributDefinice>().ToTable("atribut_definice")
     .Property(a => a.Typ)
     .HasConversion(
         v => v == TypAtributu.AnoNe ? "ANO_NE" : v.ToString().ToUpperInvariant(),
         v => v == "ANO_NE" ? TypAtributu.AnoNe : Enum.Parse<TypAtributu>(v, true));
```

> Bez té výjimky by se enum uložil jako `ANONE` a neprošel kontrolní podmínkou.
> Je to jediné místo, kde se název v kódu a v databázi rozchází — proto výslovně.

- [ ] **Krok 5: Doplň kontrolu schématu, ověř a commitni**

```csharp
"ciselniky", "atribut_definice", "vazba_definice",   // přibude do OcekavaneTabulky
```

```bash
dotnet test Ciselniky.Tests.Integration --filter DefiniceSchemaTests   # Passed: 4
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(ciselniky): schéma definice struktury — atributy, vazby, režim správy"
```

---

## Blok 2: Životní cyklus číselníku

**Cíl bloku:** Číselník jde založit, přejmenovat a vyřadit. **Kód je po prvním publikování
neměnný.**

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Ciselniky/CiselnikSluzba.cs`
- Vytvoř: `Ciselniky.Core/Services/ValidacniVyjimka.cs`
- Test: `Ciselniky.Tests.Unit/Ciselniky/KodCiselnikuTests.cs`
- Test: `Ciselniky.Tests.Integration/Ciselniky/CiselnikSluzbaTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikSluzba.ZalozAsync`, `.UpravAsync`, `.VyradAsync`;
  `ValidacniVyjimka` s výčtem chyb.

- [ ] **Krok 1: Napiš padající testy tvaru kódu**

`Ciselniky.Tests.Unit/Ciselniky/KodCiselnikuTests.cs`:

```csharp
using Ciselniky.Core.Services.Ciselniky;

namespace Ciselniky.Tests.Unit.Ciselniky;

public sealed class KodCiselnikuTests
{
    [Theory]
    [InlineData("cile")]
    [InlineData("vydajove-oblasti")]
    [InlineData("rozpoctove-polozky-2026")]
    public void PlatnyKod_Projde(string kod)
        => Assert.True(CiselnikSluzba.JeKodPlatny(kod));

    [Theory]
    [InlineData("Cile")]              // velká písmena
    [InlineData("výdajové-oblasti")]  // diakritika
    [InlineData("vydajove oblasti")]  // mezera
    [InlineData("vydajove_oblasti")]  // podtržítko
    [InlineData("-cile")]             // začíná pomlčkou
    [InlineData("cile-")]             // končí pomlčkou
    [InlineData("cile--x")]           // dvě pomlčky za sebou
    [InlineData("")]
    public void NeplatnyKod_Neprojde(string kod)
        => Assert.False(CiselnikSluzba.JeKodPlatny(kod));
}
```

> Pravidla nejsou libovolná: **kód jde do adresy a do stálého identifikátoru.**
> Diakritika, mezery a velká písmena by v adrese musely být kódované a identifikátor
> by přestal být čitelný — a hlavně by dvě různá zakódování téhož kódu vedla na dvě
> různé adresy téže věci.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Unit --filter KodCiselnikuTests
```

- [ ] **Krok 3: Validační výjimka**

`Ciselniky.Core/Services/ValidacniVyjimka.cs`:

```csharp
namespace Ciselniky.Core.Services;

public sealed record ChybaOvereni(string? Polozka, string? Atribut, string Zprava);

/// <summary>
/// Nese <b>všechny</b> nalezené chyby, ne první. Kdo upravuje tisíc řádků,
/// potřebuje vidět všechno najednou — a rozhraní z toho staví seznam chyb.
/// </summary>
public sealed class ValidacniVyjimka(IReadOnlyList<ChybaOvereni> chyby)
    : Exception("Ověření dat selhalo.")
{
    public IReadOnlyList<ChybaOvereni> Chyby { get; } = chyby;
}
```

- [ ] **Krok 4: Služba číselníků**

`Ciselniky.Core/Services/Ciselniky/CiselnikSluzba.cs`:

```csharp
using System.Text.RegularExpressions;
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Ciselniky;

public sealed record ZalozeniPozadavek(
    string Kod, string Nazev, string? Popis, bool Hierarchicky, RezimSpravy RezimSpravy);

public sealed record UpravaPozadavek(
    string? Kod, string Nazev, string? Popis, bool Hierarchicky, RezimSpravy RezimSpravy);

public sealed partial class CiselnikSluzba(CiselnikyDbContext db)
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TvarKodu();

    public static bool JeKodPlatny(string? kod)
        => !string.IsNullOrEmpty(kod) && TvarKodu().IsMatch(kod);

    public async Task<int> ZalozAsync(ZalozeniPozadavek pozadavek, int kdoId,
                                      CancellationToken ct = default)
    {
        var chyby = new List<ChybaOvereni>();

        if (!JeKodPlatny(pozadavek.Kod))
            chyby.Add(new(null, "kod",
                "Kód smí obsahovat jen malá písmena bez diakritiky, číslice a pomlčky " +
                "mezi nimi. Například „vydajove-oblasti\"."));

        if (await db.Ciselniky.AnyAsync(c => c.Kod == pozadavek.Kod, ct))
            chyby.Add(new(null, "kod", $"Číselník s kódem „{pozadavek.Kod}\" už existuje."));

        if (string.IsNullOrWhiteSpace(pozadavek.Nazev))
            chyby.Add(new(null, "nazev", "Název je povinný."));

        if (chyby.Count > 0) throw new ValidacniVyjimka(chyby);

        var ciselnik = new Ciselnik
        {
            Kod = pozadavek.Kod,
            Nazev = pozadavek.Nazev.Trim(),
            Popis = pozadavek.Popis?.Trim(),
            Hierarchicky = pozadavek.Hierarchicky,
            RezimSpravy = pozadavek.RezimSpravy,
            SpravceId = kdoId
        };
        db.Ciselniky.Add(ciselnik);
        await db.SaveChangesAsync(ct);
        return ciselnik.Id;
    }

    public async Task UpravAsync(int ciselnikId, UpravaPozadavek pozadavek,
                                 CancellationToken ct = default)
    {
        var ciselnik = await db.Ciselniky.FindAsync([ciselnikId], ct)
            ?? throw new InvalidOperationException($"Číselník {ciselnikId} neexistuje.");

        var chyby = new List<ChybaOvereni>();

        if (pozadavek.Kod is not null && pozadavek.Kod != ciselnik.Kod)
        {
            // Kód je součástí stálého identifikátoru, který si konzumenti ukládají.
            // Po prvním publikování je proto neměnný (rozhodnutí Z4).
            if (await BylPublikovanAsync(ciselnikId, ct))
                chyby.Add(new(null, "kod",
                    "Kód už nejde změnit — číselník byl publikován a konzumenti podle " +
                    "něj drží odkazy. Založte nový číselník, nebo ponechte kód."));
            else if (!JeKodPlatny(pozadavek.Kod))
                chyby.Add(new(null, "kod", "Kód má nepovolený tvar."));
            else if (await db.Ciselniky.AnyAsync(c => c.Kod == pozadavek.Kod && c.Id != ciselnikId, ct))
                chyby.Add(new(null, "kod", $"Číselník s kódem „{pozadavek.Kod}\" už existuje."));
            else
                ciselnik.Kod = pozadavek.Kod;
        }

        if (string.IsNullOrWhiteSpace(pozadavek.Nazev))
            chyby.Add(new(null, "nazev", "Název je povinný."));

        if (chyby.Count > 0) throw new ValidacniVyjimka(chyby);

        ciselnik.Nazev = pozadavek.Nazev.Trim();
        ciselnik.Popis = pozadavek.Popis?.Trim();
        ciselnik.Hierarchicky = pozadavek.Hierarchicky;
        ciselnik.RezimSpravy = pozadavek.RezimSpravy;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Vyřadí číselník. <b>Nemaže</b> — a nepustí to, pokud na něj vede vazba.</summary>
    public async Task VyradAsync(int ciselnikId, CancellationToken ct = default)
    {
        var odkazujici = await db.VazbaDefinice
            .Where(v => v.CilovyCiselnikId == ciselnikId)
            .Join(db.Ciselniky, v => v.CiselnikId, c => c.Id, (v, c) => c.Nazev)
            .Distinct().ToListAsync(ct);

        if (odkazujici.Count > 0)
            throw new ValidacniVyjimka([new(null, null,
                "Číselník nejde vyřadit, protože na něj vedou vazby z: "
                + string.Join(", ", odkazujici)
                + ". Nejdřív odeberte tyto vazby.")]);

        var ciselnik = await db.Ciselniky.FindAsync([ciselnikId], ct)
            ?? throw new InvalidOperationException($"Číselník {ciselnikId} neexistuje.");
        ciselnik.Aktivni = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// V P3 vždy <c>false</c> — verze zavádí až P5. Metoda existuje už teď, aby pravidlo
    /// neměnnosti kódu bylo na jednom místě a P5 doplnil jen její tělo.
    /// </summary>
    private Task<bool> BylPublikovanAsync(int ciselnikId, CancellationToken ct)
        => Task.FromResult(false);
}
```

> **`BylPublikovanAsync` je záměrně napsaná už teď**, i když vždy vrací `false`.
> Kdyby se pravidlo neměnnosti kódu dopisovalo až v P5, muselo by se hledat mezi
> hotovými cestami. Takhle je jedno místo, kde P5 dopíše tělo — a dluh je zapsaný níže.

- [ ] **Krok 5: Integrační testy služby**

```csharp
[Fact] public async Task Zaloz_SNeplatnymKodem_VypiseSrozumitelnouChybu()
[Fact] public async Task Zaloz_SDuplicitnimKodem_Odmitne()
[Fact] public async Task Zaloz_VratiIdANastaviSpravce()
[Fact] public async Task Uprav_ZmenaKoduPredPublikovanim_Projde()
[Fact] public async Task Vyrad_KdyzNaNejVedeVazba_OdmitneAVyjmenujeOdkud()
[Fact] public async Task Vyrad_Deaktivuje_Nemaze()
```

Předposlední test je ten podstatný: hláška musí **vyjmenovat, odkud vazby vedou**.
„Nelze vyřadit" bez uvedení proč nutí uživatele hledat naslepo.

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Unit --filter KodCiselnikuTests            # Passed: 11
dotnet test Ciselniky.Tests.Integration --filter CiselnikSluzbaTests   # Passed: 6
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(ciselniky): životní cyklus číselníku s neměnným kódem po publikování"
```

---

## Blok 3: Definice atributů a vazeb

**Cíl bloku:** Struktura jde definovat a validace nepustí nesmysl.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Ciselniky/DefiniceSluzba.cs`
- Test: `Ciselniky.Tests.Integration/Ciselniky/DefiniceSluzbaTests.cs`

**Rozhraní:**
- Poskytuje: `DefiniceSluzba.UlozAtributAsync`, `.UlozVazbuAsync`,
  `.OdeberAtributAsync`, `.OdeberVazbuAsync`, `.NactiAsync`.

- [ ] **Krok 1: Napiš padající testy validace**

```csharp
[Fact] public async Task Atribut_TypuVycet_BezHodnot_Odmitne()
[Fact] public async Task Atribut_SKodemKolidujicimSVazbou_Odmitne()
[Fact] public async Task Vazba_NaNeexistujiciCiselnik_Odmitne()
[Fact] public async Task Vazba_NaVyrazenyCiselnik_Odmitne()
[Fact] public async Task Atribut_SNeplatnymKodem_Odmitne()
[Fact] public async Task Ulozeni_ZachovavaPoradiPolozek()
```

Druhý test drží pravidlo o sdíleném jmenném prostoru kódů. Databáze ho nevynutí
(jsou to dvě tabulky), takže bez tohoto testu by se pravidlo tiše rozpadlo.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter DefiniceSluzbaTests
```

- [ ] **Krok 3: Služba definice**

`Ciselniky.Core/Services/Ciselniky/DefiniceSluzba.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Ciselniky;

public sealed record AtributPozadavek(
    int? Id, string Kod, string Nazev, TypAtributu Typ,
    bool Povinny, int Poradi, string[] Vycet);

public sealed record VazbaPozadavek(
    int? Id, string Kod, string Nazev, int CilovyCiselnikId, bool Povinna, int Poradi);

public sealed record DefiniceCiselniku(
    IReadOnlyList<AtributDefinice> Atributy, IReadOnlyList<VazbaDefinice> Vazby);

public sealed class DefiniceSluzba(CiselnikyDbContext db)
{
    public async Task<DefiniceCiselniku> NactiAsync(int ciselnikId, CancellationToken ct = default)
        => new(
            await db.AtributDefinice.Where(a => a.CiselnikId == ciselnikId)
                    .OrderBy(a => a.Poradi).ThenBy(a => a.Kod).ToListAsync(ct),
            await db.VazbaDefinice.Where(v => v.CiselnikId == ciselnikId)
                    .OrderBy(v => v.Poradi).ThenBy(v => v.Kod).ToListAsync(ct));

    public async Task UlozAtributAsync(int ciselnikId, AtributPozadavek pozadavek,
                                       CancellationToken ct = default)
    {
        var chyby = new List<ChybaOvereni>();

        if (!CiselnikSluzba.JeKodPlatny(pozadavek.Kod))
            chyby.Add(new(null, "kod",
                "Kód atributu smí obsahovat jen malá písmena bez diakritiky, " +
                "číslice a pomlčky mezi nimi."));

        if (pozadavek.Typ == TypAtributu.Vycet && pozadavek.Vycet.Length == 0)
            chyby.Add(new(null, "vycet",
                "Atribut typu výčet potřebuje aspoň jednu přípustnou hodnotu."));

        if (pozadavek.Typ != TypAtributu.Vycet && pozadavek.Vycet.Length > 0)
            chyby.Add(new(null, "vycet",
                "Výčet hodnot má smysl jen u typu výčet. U ostatních typů se neuvádí."));

        await OverJmennyProstorAsync(ciselnikId, pozadavek.Kod,
            atributId: pozadavek.Id, vazbaId: null, chyby, ct);

        if (chyby.Count > 0) throw new ValidacniVyjimka(chyby);

        var atribut = pozadavek.Id is null
            ? PridejNovy(ciselnikId)
            : (await db.AtributDefinice.FindAsync([pozadavek.Id.Value], ct))
              ?? throw new InvalidOperationException($"Atribut {pozadavek.Id} neexistuje.");

        atribut.Kod = pozadavek.Kod;
        atribut.Nazev = pozadavek.Nazev.Trim();
        atribut.Typ = pozadavek.Typ;
        atribut.Povinny = pozadavek.Povinny;
        atribut.Poradi = pozadavek.Poradi;
        atribut.VycetHodnot = pozadavek.Typ == TypAtributu.Vycet
            ? string.Join(';', pozadavek.Vycet.Select(h => h.Trim()))
            : null;

        await db.SaveChangesAsync(ct);

        AtributDefinice PridejNovy(int id)
        {
            var novy = new AtributDefinice { CiselnikId = id, Kod = "", Nazev = "" };
            db.AtributDefinice.Add(novy);
            return novy;
        }
    }

    public async Task UlozVazbuAsync(int ciselnikId, VazbaPozadavek pozadavek,
                                     CancellationToken ct = default)
    {
        var chyby = new List<ChybaOvereni>();

        if (!CiselnikSluzba.JeKodPlatny(pozadavek.Kod))
            chyby.Add(new(null, "kod", "Kód vazby má nepovolený tvar."));

        var cil = await db.Ciselniky.FirstOrDefaultAsync(
            c => c.Id == pozadavek.CilovyCiselnikId, ct);

        if (cil is null)
            chyby.Add(new(null, "cilovyCiselnik", "Cílový číselník neexistuje."));
        else if (!cil.Aktivni)
            chyby.Add(new(null, "cilovyCiselnik",
                $"Číselník „{cil.Nazev}\" je vyřazený a nejde na něj navázat."));
        else if (cil.Id == ciselnikId)
            chyby.Add(new(null, "cilovyCiselnik",
                "Vazba nemůže mířit na týž číselník. Vztah mezi vlastními položkami " +
                "se zapíná příznakem hierarchie."));

        await OverJmennyProstorAsync(ciselnikId, pozadavek.Kod,
            atributId: null, vazbaId: pozadavek.Id, chyby, ct);

        if (chyby.Count > 0) throw new ValidacniVyjimka(chyby);

        var vazba = pozadavek.Id is null
            ? PridejNovou(ciselnikId)
            : (await db.VazbaDefinice.FindAsync([pozadavek.Id.Value], ct))
              ?? throw new InvalidOperationException($"Vazba {pozadavek.Id} neexistuje.");

        vazba.Kod = pozadavek.Kod;
        vazba.Nazev = pozadavek.Nazev.Trim();
        vazba.CilovyCiselnikId = pozadavek.CilovyCiselnikId;
        vazba.Povinna = pozadavek.Povinna;
        vazba.Poradi = pozadavek.Poradi;

        await db.SaveChangesAsync(ct);

        VazbaDefinice PridejNovou(int id)
        {
            var nova = new VazbaDefinice { CiselnikId = id, Kod = "", Nazev = "" };
            db.VazbaDefinice.Add(nova);
            return nova;
        }
    }

    /// <summary>
    /// Kódy atributů a vazeb sdílejí jmenný prostor. Databáze to nevynutí — jsou to
    /// dvě tabulky. V odpovědi rozhraní i v importním souboru ale stojí vedle sebe
    /// a stejný kód ve dvou významech je past.
    /// </summary>
    private async Task OverJmennyProstorAsync(
        int ciselnikId, string kod, int? atributId, int? vazbaId,
        List<ChybaOvereni> chyby, CancellationToken ct)
    {
        var koliduje =
            await db.AtributDefinice.AnyAsync(
                a => a.CiselnikId == ciselnikId && a.Kod == kod && a.Id != atributId, ct)
            || await db.VazbaDefinice.AnyAsync(
                v => v.CiselnikId == ciselnikId && v.Kod == kod && v.Id != vazbaId, ct);

        if (koliduje)
            chyby.Add(new(null, "kod",
                $"Kód „{kod}\" už v tomto číselníku používá jiný atribut nebo vazba."));
    }
}
```

- [ ] **Krok 4: Odebrání atributu a vazby**

```csharp
public async Task OdeberAtributAsync(int atributId, CancellationToken ct = default)
{
    var atribut = await db.AtributDefinice.FindAsync([atributId], ct)
        ?? throw new InvalidOperationException($"Atribut {atributId} neexistuje.");
    db.AtributDefinice.Remove(atribut);
    await db.SaveChangesAsync(ct);
}
```

> **V P3 se atribut maže bez okolků**, protože hodnoty ještě neexistují.
> Od P4 na něm budou viset hodnoty položek a mazání bude muset být buď zakázané,
> nebo kaskádové s výslovným potvrzením. **Zapsáno jako dluh D5.**

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter DefiniceSluzbaTests   # Passed: 6
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(ciselniky): definice atributů a vazeb s validací"
```

---

## Blok 4: JSON Schema z definice

**Cíl bloku:** Z definice vzniká strojový popis struktury pro konzumenty.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Ciselniky/SchemaGenerator.cs`
- Test: `Ciselniky.Tests.Unit/Ciselniky/SchemaGeneratorTests.cs`

**Rozhraní:**
- Poskytuje: `SchemaGenerator.Vytvor(Ciselnik, DefiniceCiselniku, string bazoveUri) → JsonObject`.

- [ ] **Krok 1: Napiš padající test**

`Ciselniky.Tests.Unit/Ciselniky/SchemaGeneratorTests.cs`:

```csharp
using System.Text.Json.Nodes;
using Ciselniky.Core.Domain;
using Ciselniky.Core.Services.Ciselniky;

namespace Ciselniky.Tests.Unit.Ciselniky;

public sealed class SchemaGeneratorTests
{
    private static (Ciselnik, DefiniceCiselniku) Vzorek() => (
        new Ciselnik { Id = 1, Kod = "cile", Nazev = "Rozpočtové cíle", Hierarchicky = true },
        new DefiniceCiselniku(
            [
                new AtributDefinice { Id = 1, CiselnikId = 1, Kod = "cislo-cile",
                                      Nazev = "Číslo cíle", Typ = TypAtributu.Text, Povinny = true },
                new AtributDefinice { Id = 2, CiselnikId = 1, Kod = "stav", Nazev = "Stav",
                                      Typ = TypAtributu.Vycet, VycetHodnot = "A;U;N" },
                new AtributDefinice { Id = 3, CiselnikId = 1, Kod = "castka", Nazev = "Částka",
                                      Typ = TypAtributu.Cislo },
            ],
            [
                new VazbaDefinice { Id = 1, CiselnikId = 1, Kod = "manazer",
                                    Nazev = "Manažer cíle", CilovyCiselnikId = 2, Povinna = true },
            ]));

    [Fact]
    public void Schema_UvadiPovinneAtributy()
    {
        var (ciselnik, definice) = Vzorek();

        var schema = SchemaGenerator.Vytvor(ciselnik, definice, "https://ciselniky.fis/id");

        var povinne = schema["properties"]!["atributy"]!["required"]!.AsArray()
            .Select(u => u!.GetValue<string>()).ToArray();
        Assert.Contains("cislo-cile", povinne);
        Assert.DoesNotContain("stav", povinne);
    }

    [Fact]
    public void Schema_PrevadiTypyNaJsonTypy()
    {
        var (ciselnik, definice) = Vzorek();

        var vlastnosti = SchemaGenerator.Vytvor(ciselnik, definice, "https://ciselniky.fis/id")
            ["properties"]!["atributy"]!["properties"]!;

        Assert.Equal("string", vlastnosti["cislo-cile"]!["type"]!.GetValue<string>());
        Assert.Equal("number", vlastnosti["castka"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Schema_VycetVydaJakoEnum()
    {
        var (ciselnik, definice) = Vzorek();

        var stav = SchemaGenerator.Vytvor(ciselnik, definice, "https://ciselniky.fis/id")
            ["properties"]!["atributy"]!["properties"]!["stav"]!;

        Assert.Equal(["A", "U", "N"],
            stav["enum"]!.AsArray().Select(u => u!.GetValue<string>()));
    }

    [Fact]
    public void Schema_VazbuVydaJakoOdkazNeJakoVnorenyObjekt()
    {
        var (ciselnik, definice) = Vzorek();

        var manazer = SchemaGenerator.Vytvor(ciselnik, definice, "https://ciselniky.fis/id")
            ["properties"]!["vazby"]!["properties"]!["manazer"]!;

        // Vazba je odkaz {ciselnik, kod, uri} — ne vnořený obsah cílové položky.
        // Kdyby se vnořovala, měl by každý číselník jiný tvar odpovědi a jedno
        // rozhraní by je neobsloužilo.
        var vlastnosti = manazer["properties"]!.AsObject().Select(p => p.Key).ToArray();
        Assert.Equal(["ciselnik", "kod", "uri"], vlastnosti);
    }

    [Fact]
    public void Schema_HierarchickyCiselnik_MaNadrazenyKod()
    {
        var (ciselnik, definice) = Vzorek();

        var schema = SchemaGenerator.Vytvor(ciselnik, definice, "https://ciselniky.fis/id");

        Assert.NotNull(schema["properties"]!["nadrazenyKod"]);
    }
}
```

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Unit --filter SchemaGeneratorTests
```

- [ ] **Krok 3: Generátor**

`Ciselniky.Core/Services/Ciselniky/SchemaGenerator.cs`:

```csharp
using System.Text.Json.Nodes;
using Ciselniky.Core.Domain;

namespace Ciselniky.Core.Services.Ciselniky;

/// <summary>
/// Sestaví JSON Schema položek číselníku z jeho definice. Je to funkční ekvivalent WSDL
/// pro číselník — popisuje konzumentovi, co v odpovědi najde.
/// </summary>
public static class SchemaGenerator
{
    public static JsonObject Vytvor(Ciselnik ciselnik, DefiniceCiselniku definice, string bazoveUri)
    {
        var atributy = new JsonObject();
        var povinneAtributy = new JsonArray();

        foreach (var atribut in definice.Atributy.OrderBy(a => a.Poradi))
        {
            atributy[atribut.Kod] = PopisAtributu(atribut);
            if (atribut.Povinny) povinneAtributy.Add(atribut.Kod);
        }

        var vazby = new JsonObject();
        var povinneVazby = new JsonArray();

        foreach (var vazba in definice.Vazby.OrderBy(v => v.Poradi))
        {
            vazby[vazba.Kod] = PopisVazby(vazba.Nazev);
            if (vazba.Povinna) povinneVazby.Add(vazba.Kod);
        }

        var vlastnosti = new JsonObject
        {
            ["kod"]         = new JsonObject { ["type"] = "string" },
            ["nazev"]       = new JsonObject { ["type"] = "string" },
            ["uri"]         = new JsonObject { ["type"] = "string", ["format"] = "uri" },
            ["platnostOd"]  = new JsonObject { ["type"] = "string", ["format"] = "date" },
            ["platnostDo"]  = new JsonObject { ["type"] = ArrayZ("string", "null"),
                                               ["format"] = "date" },
            ["atributy"]    = new JsonObject
            {
                ["type"] = "object", ["properties"] = atributy, ["required"] = povinneAtributy
            },
            ["vazby"]       = new JsonObject
            {
                ["type"] = "object", ["properties"] = vazby, ["required"] = povinneVazby
            },
        };

        if (ciselnik.Hierarchicky)
            vlastnosti["nadrazenyKod"] = new JsonObject
            {
                ["type"] = ArrayZ("string", "null"),
                ["description"] = "Kód nadřazené položky v témž číselníku."
            };

        return new JsonObject
        {
            ["$schema"]     = "https://json-schema.org/draft/2020-12/schema",
            ["$id"]         = $"{bazoveUri}/{ciselnik.Kod}/schema",
            ["title"]       = ciselnik.Nazev,
            ["description"] = ciselnik.Popis,
            ["type"]        = "object",
            ["properties"]  = vlastnosti,
            ["required"]    = new JsonArray("kod", "nazev"),
        };
    }

    private static JsonObject PopisAtributu(AtributDefinice atribut)
    {
        var popis = new JsonObject { ["title"] = atribut.Nazev };

        switch (atribut.Typ)
        {
            case TypAtributu.Text:   popis["type"] = "string"; break;
            case TypAtributu.Cislo:  popis["type"] = "number"; break;
            case TypAtributu.AnoNe:  popis["type"] = "boolean"; break;
            case TypAtributu.Datum:
                popis["type"] = "string";
                popis["format"] = "date";
                break;
            case TypAtributu.Vycet:
                popis["type"] = "string";
                popis["enum"] = new JsonArray([.. atribut.Vycet.Select(h => (JsonNode?)h)]);
                break;
        }

        return popis;
    }

    /// <summary>
    /// Vazba se popisuje jako <b>odkaz</b>, ne jako vnořený obsah cílové položky.
    /// Kdyby se vnořovala, měl by každý číselník jiný tvar odpovědi — a jedno generické
    /// rozhraní by je neobsloužilo. To je celý důvod, proč aplikace existuje.
    /// </summary>
    private static JsonObject PopisVazby(string nazev) => new()
    {
        ["title"] = nazev,
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["ciselnik"] = new JsonObject { ["type"] = "string" },
            ["kod"]      = new JsonObject { ["type"] = "string" },
            ["uri"]      = new JsonObject { ["type"] = "string", ["format"] = "uri" },
        },
        ["required"] = new JsonArray("ciselnik", "kod"),
    };

    private static JsonArray ArrayZ(params string[] typy)
        => new([.. typy.Select(t => (JsonNode?)t)]);
}
```

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Unit --filter SchemaGeneratorTests   # Passed: 5
git add -A && git commit -m "feat(ciselniky): JSON Schema generované z definice struktury"
```

---

## Blok 5: Koncové body

**Cíl bloku:** Chráněné koncové body pro číselníky a jejich strukturu. Uzavírá dluh D2 z P2.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/CiselnikyController.cs`, `StrukturaController.cs`
- Vytvoř: `Ciselniky.Api/Filters/ValidacniVyjimkaFilter.cs`
- Smaž: zkušební koncový bod `internal/ciselniky/{kod}/zkouska` z P2
- Test: `Ciselniky.Tests.Api/CiselnikyControllerTests.cs`

- [ ] **Krok 1: Převod validační výjimky na odpověď**

`Ciselniky.Api/Filters/ValidacniVyjimkaFilter.cs`:

```csharp
using Ciselniky.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ciselniky.Api.Filters;

/// <summary>Převádí <see cref="ValidacniVyjimka"/> na ProblemDetails se všemi chybami.</summary>
public sealed class ValidacniVyjimkaFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ValidacniVyjimka vyjimka) return;

        var problem = new ProblemDetails
        {
            Type = "https://ciselniky.fis/chyby/overeni-selhalo",
            Title = "Ověření dat selhalo",
            Status = StatusCodes.Status422UnprocessableEntity,
        };
        problem.Extensions["chyby"] = vyjimka.Chyby
            .Select(ch => new { polozka = ch.Polozka, atribut = ch.Atribut, zprava = ch.Zprava });

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        context.ExceptionHandled = true;
    }
}
```

- [ ] **Krok 2: Koncové body**

```csharp
[ApiController]
public sealed class CiselnikyController(...) : ControllerBase
{
    [HttpGet("internal/ciselniky")]
    [AllowAnonymous]                                   // čtení nemá klíč (rozhodnutí N1)
    public Task<IActionResult> Seznam(CancellationToken ct);

    [HttpGet("internal/ciselniky/{kod}")]
    [AllowAnonymous]
    public Task<IActionResult> Detail(string kod, CancellationToken ct);

    [HttpGet("internal/ciselniky/{kod}/schema")]
    [AllowAnonymous]
    public Task<IActionResult> Schema(string kod, CancellationToken ct);

    [HttpPost("internal/ciselniky")]
    [Authorize(Policy = "opravneni:" + KliceOpravneni.CiselnikyCreate)]
    public Task<IActionResult> Zaloz([FromBody] ZalozeniPozadavek p, CancellationToken ct);

    [HttpPut("internal/ciselniky/{kod}")]
    [Authorize(Policy = "opravneni:" + KliceOpravneni.CiselnikyEdit)]
    public Task<IActionResult> Uprav(string kod, [FromBody] UpravaPozadavek p, CancellationToken ct);

    [HttpDelete("internal/ciselniky/{kod}")]
    [Authorize(Policy = "opravneni:" + KliceOpravneni.CiselnikyDeactivate)]
    public Task<IActionResult> Vyrad(string kod, CancellationToken ct);
}
```

> `Zaloz` je jediná měnící akce **bez `{kod}` v adrese** — a je to správně:
> `ciselniky.create` je globální klíč, protože v okamžiku zakládání číselník ještě
> neexistuje a nemá na co se vázat rozsah. Architektonický test z P2 ho proto nehlídá.

`StrukturaController` obsluhuje `internal/ciselniky/{kod}/atributy`
a `.../vazby` (`PUT`, `DELETE`) pod klíčem `ciselniky.definice.edit`.

- [ ] **Krok 3: Testy**

```csharp
[Fact] public async Task Seznam_JeDostupny_IBezPrihlaseni()
[Fact] public async Task Zaloz_BezPrava_Odmitne()
[Fact] public async Task Uprav_SPravemNaJinyCiselnik_Odmitne()
[Fact] public async Task Zaloz_SNeplatnymKodem_Vrati422SeVsemiChybami()
[Fact] public async Task Schema_VraciJsonSchema()
```

Čtvrtý test ověřuje, že odpověď obsahuje **pole `chyby` se všemi nálezy**, ne jednu hlášku.

- [ ] **Krok 4: Smaž zkušební koncový bod z P2 a commitni**

```bash
grep -rn "zkouska" Ciselniky.Api/    # očekávej: 0 nálezů
dotnet test Ciselniky.Tests.Api --filter CiselnikyControllerTests
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(ciselniky): koncové body číselníků a struktury"
```

---

## Blok 6: Rail a seznam číselníků

**Cíl bloku:** Rámec aplikace podle wireframu — trvalý rail a seznam nahradí prázdný
rozcestník z P1.

**Soubory:**
- Vytvoř: `ciselniky-web/src/komponenty/pm/PmSearch.tsx`, `PmField.tsx`, `PmSelect.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/Ramec.tsx`, `RailCiselniku.tsx`
- Uprav: `ciselniky-web/src/stranky/SeznamCiselniku.tsx`, `src/App.tsx`
- Test: `ciselniky-web/src/komponenty/RailCiselniku.test.tsx`
- Test: `Ciselniky.Tests.E2E/Scenare/SeznamCiselnikuTests.cs`

- [ ] **Krok 1: Doplň chybějící wrappery `pm-*`**

P1 zavedl `PmButton`, `PmIcon` a `PmAlert`. Obrazovky tohoto plánu potřebují navíc
**`PmSearch`, `PmField` a `PmSelect`**. Vznikají stejným vzorem jako `PmButton`
a se stejným pravidlem: **jeden wrapper = jeden bod změny při upgradu gov design systemu.**

`ciselniky-web/src/komponenty/pm/PmSelect.tsx`:

```tsx
export function PmSelect<T extends string>(props: {
  popisek: string
  hodnota: T
  moznosti: { hodnota: T; popisek: string }[]
  onZmena: (hodnota: T) => void
  zakazano?: boolean
}) {
  return (
    <gov-form-control>
      <gov-form-label slot="top">{props.popisek}</gov-form-label>
      <gov-form-select
        slot="bottom"
        disabled={props.zakazano ? '' : undefined}
        onGov-change={(e: CustomEvent<{ value: T }>) => props.onZmena(e.detail.value)}
      >
        <select>
          {props.moznosti.map((m) => (
            <option key={m.hodnota} value={m.hodnota}
                    selected={m.hodnota === props.hodnota}>
              {m.popisek}
            </option>
          ))}
        </select>
      </gov-form-select>
    </gov-form-control>
  )
}
```

Test každého wrapperu ověřuje **dvě věci**: že se vykreslí správný gov element se správnými
atributy, a že **se popisek v DOM vyskytuje právě jednou**. Druhá kontrola není nadbytečná —
v Zápisce se popisek zdvojoval, když se na hostu gov komponenty sáhlo na `textContent`.

Typy pro nové elementy se doplní do `src/gov-elements.d.ts`:
`gov-form-control`, `gov-form-label`, `gov-form-select`, `gov-form-input`, `gov-form-search`.

- [ ] **Krok 2: Rail**

Podle wireframu: pevných 280 px, filtr nahoře, seznam s počty hodnot,
`+ Nový číselník` **pod čarou** — zakládání je vzácná akce správce, hledání častá akce
každého, a nahoru patří to častější.

```tsx
export function RailCiselniku(props: {
  ciselniky: { kod: string; nazev: string; pocetHodnot: number }[]
  aktivniKod?: string
  smimZakladat: boolean
}) {
  const [filtr, setFiltr] = useState('')
  const videt = props.ciselniky.filter((c) =>
    c.nazev.toLowerCase().includes(filtr.toLowerCase()) ||
    c.kod.includes(filtr.toLowerCase()))

  return (
    <nav className="rail" aria-label="Číselníky">
      <pm-search hodnota={filtr} onZmena={setFiltr} popisek="Filtrovat…" />
      <ul>
        {videt.map((c) => (
          <li key={c.kod} className={c.kod === props.aktivniKod ? 'rail-aktivni' : undefined}>
            <Link to={`/ciselnik/${c.kod}`}>
              <span className="rail-nazev">{c.nazev}</span>
              <span className="rail-pocet">{c.pocetHodnot}</span>
            </Link>
          </li>
        ))}
      </ul>
      {props.smimZakladat && (
        <>
          <hr />
          <Link to="/ciselnik/novy">+ Nový číselník</Link>
        </>
      )}
    </nav>
  )
}
```

- [ ] **Krok 3: Testy komponenty**

```tsx
test('filtr zúží seznam podle názvu i kódu')
test('bez práva zakládat se odkaz Nový číselník nevykreslí')
test('aktivní číselník má odlišující třídu')
```

Druhý test drží pravidlo: **prvek, na který uživatel nemá právo, chybí — není zašedlý.**
Zašedlý odkaz slibuje, že to jednou půjde.

- [ ] **Krok 4: Seznam číselníků s prázdným stavem**

Tabulka podle wireframu O1: kód, název, hodnot, verze, režim, publikován.
Prázdný stav je **výzva k akci**, ne oznámení o prázdnotě:

```tsx
{ciselniky.length === 0 && (
  <div className="prazdny-stav">
    <p>Zatím není založen žádný číselník.</p>
    {smimZakladat && (
      <>
        <p>Založte první a nadefinujte, jaké údaje ponese.</p>
        <PmButton varianta="primary" onClick={naNovy}>Založit číselník</PmButton>
      </>
    )}
  </div>
)}
```

Druhá věta a tlačítko se ukážou **jen tomu, kdo na to má právo**. Ostatní vidí první větu.

- [ ] **Krok 5: Koncový test**

`Ciselniky.Tests.E2E/Scenare/SeznamCiselnikuTests.cs`:

```csharp
[Fact] public async Task Rail_ZobrazuejeZalozeneCiselniky()
[Fact] public async Task CtenarBezRole_NevidiOdkazNovyCiselnik()
```

- [ ] **Krok 6: Ověř a commitni**

```bash
cd ciselniky-web && npx vitest run && cd ..
dotnet test Ciselniky.Tests.E2E --filter SeznamCiselnikuTests
git add -A && git commit -m "feat(web): rámec s railem a seznamem číselníků"
```

---

## Blok 7: Obrazovka definice struktury

**Cíl bloku:** Správce definuje strukturu v prohlížeči. Wireframe O7.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/StrukturaCiselniku.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/AtributRadek.tsx`, `VazbaRadek.tsx`
- Test: `ciselniky-web/src/stranky/StrukturaCiselniku.test.tsx`

- [ ] **Krok 1: Upozornění o dopadu nad tabulkou**

Podle wireframu je varování **nad tabulkou, ne v potvrzovacím okně**:

```tsx
<gov-message color="warning">
  Změna struktury zvýší hlavní číslo verze. Konzumující aplikace se jí mohou dotknout —
  po publikování si mají znovu načíst popis struktury.
</gov-message>
```

Kdo sem přišel, má vědět, do čeho jde, **dřív než něco změní**. Potvrzovací okno přijde
až ve chvíli, kdy je práce hotová a člověk ji nechce zahodit.

- [ ] **Krok 2: Tabulka atributů**

Sloupce podle wireframu: kód, název, typ, povinný, a u typu výčet přípustné hodnoty.
Výběr typu je `pm-select` s pěti možnostmi; pole pro výčet se objeví **jen u typu výčet**.

```tsx
{atribut.typ === 'VYCET' && (
  <pm-field
    popisek="Přípustné hodnoty"
    napoveda="Oddělte středníkem, například: A;U;N"
    hodnota={atribut.vycet.join(';')}
    onZmena={(v: string) => zmen({ ...atribut, vycet: rozdel(v) })}
  />
)}
```

- [ ] **Krok 3: Tabulka vazeb**

Cílový číselník se vybírá ze seznamu — **nikdy se nepíše kód z hlavy**.
Vlastní číselník v seznamu není; vztah mezi vlastními položkami se zapíná
příznakem hierarchie, ne vazbou.

- [ ] **Krok 4: Testy**

```tsx
test('pole pro výčet se objeví jen u typu výčet')
test('vlastní číselník není v nabídce cílových číselníků')
test('chyby ze serveru se vypíšou všechny, ne jen první')
```

- [ ] **Krok 5: Plná sada a commit**

```bash
cd ciselniky-web && npx vitest run && cd ..
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(web): obrazovka definice struktury číselníku"
```

---

## Po dokončení P3 ručně ověř

1. Správce založí číselník **Rozpočtové cíle** s kódem `cile`.
2. Pokusí se založit druhý se stejným kódem — dostane srozumitelnou hlášku.
3. Zkusí kód `Výdajové oblasti` — hláška vysvětlí, jaký tvar je povolený.
4. Nadefinuje atributy `cislo-cile` (text, povinný), `stav` (výčet A;U;N), `castka` (číslo).
5. Založí druhý číselník **Osoby** a v Cílech na něj přidá vazbu `manazer`.
6. Zkusí přidat vazbu s kódem `stav` — dostane hlášku o kolizi s atributem.
7. Zkusí vyřadit Osoby — hláška **vyjmenuje**, že na ně vede vazba z Cílů.
8. Otevře `/internal/ciselniky/cile/schema` a vidí JSON Schema s výčtem u `stav`
   a s vazbou jako odkazem.
9. Editor bez rozsahu na `cile` nevidí u tohoto číselníku editační prvky.
10. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D4 | `CiselnikSluzba.BylPublikovanAsync` vrací natvrdo `false` — verze zavádí až P5. Doplnit tělo. | **P5** |
| D5 | Odebrání atributu v P3 maže bez okolků, protože hodnoty neexistují. Od P4 na něm budou viset hodnoty — mazání musí být zakázané, nebo kaskádové s výslovným potvrzením. | **P4** |
| D6 | Bázová adresa identifikátorů je v generátoru schématu předávaná parametrem. Zatím ji volající předává natvrdo — přesunout do nastavení aplikace. | **P8** |
