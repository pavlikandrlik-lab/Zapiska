# P4 — Hodnoty, hierarchie a vazby: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Číselník má hodnoty. Jdou prohlížet plochým seznamem i stromem, filtrovat platností
k datu a proklikávat po vazbách do jiných číselníků. Tohle je **horká cesta aplikace** —
čte ji neomezený počet lidí i konzumujících aplikací.

**Architektura:** Uložení podle varianty C — společné sloupce na položce, proměnné atributy
v samostatné tabulce, vazby jako řádky s cizím klíčem. Čtení skládá položku **třemi dotazy
na stránku**, nikdy jedním dotazem na položku.

**Stack:** .NET 10, EF Core + SQL Server, React + TypeScript, xUnit.

**Specifikace:** [../10-specifikace/01-domenovy-model.md](../10-specifikace/01-domenovy-model.md),
[03-schopnosti.md](../10-specifikace/03-schopnosti.md) (S1) ·
**Datový model:** [../20-architektura/04-datovy-model.md](../20-architektura/04-datovy-model.md) ·
**Wireframy:** [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) (O2 — záložka Hodnoty)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P3 — `ciselniky`, `atribut_definice`, `vazba_definice`.
**Uzavírá dluh D5 z P3.**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Databáze unese hodnoty, jejich atributy a vazby; ukázkový číselník je naplněný |
| 2 | Aplikace složí stránku hodnot třemi dotazy, ne dotazem na položku |
| 3 | Hierarchický číselník se vydá plochý i jako strom, bez rizika zacyklení |
| 4 | Filtr „platné k datu" a vyřazené hodnoty |
| 5 | Atribut s hodnotami nejde smazat — uzavírá dluh D5 |
| 6 | Koncové body pro čtení hodnot |
| 7 | Obrazovka O2, záložka Hodnoty — seznam, strom, filtr, proklik vazby |

---

## Rozsah: P4 je záměrně **jen čtecí**

Zápisová cesta v tomto plánu **není** a je to úmysl.

Podle datového modelu drží tabulka `polozka` **publikovaný** stav a každá úprava vzniká jako
rozpracovaná změna se zatím nepřiřazenou verzí. Kdyby P4 zavedl přímý zápis, publikoval by
bez verze — a P5 by ho musel přepsat, ne rozšířit.

Proto:

| | Kde |
|---|---|
| Uložení, čtení, strom, platnost, vazby | **P4** |
| Zápisová cesta přes rozpracované změny a publikování | **P5** |
| Hromadná editace v tabulce | **P6** |

Data pro ruční ověření a pro testy dodává **ukázkový seed skript** z bloku 1.
Není to berlička — hodí se i později při ladění a při ukázce aplikace.

---

## Blok 1: Schéma hodnot

**Cíl bloku:** Databáze unese položky, jejich atributy a vazby. Ukázkový číselník je naplněný.

**Soubory:**
- Vytvoř: `db/db_upgrade_0_4_hodnoty.sql`, `db/db_seed_ukazka.sql`
- Vytvoř: `Ciselniky.Core/Domain/Polozka.cs`, `PolozkaAtribut.cs`, `PolozkaVazba.cs`
- Uprav: `Data/CiselnikyDbContext.cs`, `Data/KontrolaSchematu.cs`
- Test: `Ciselniky.Tests.Integration/Data/HodnotySchemaTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyDbContext.Polozky`, `.PolozkaAtributy`, `.PolozkaVazby`.

- [ ] **Krok 1: Migrační skript**

`db/db_upgrade_0_4_hodnoty.sql`:

```sql
-- Hodnoty číselníku. Tabulka drží PUBLIKOVANÝ stav; rozpracované změny
-- zavádí až P5 v tabulce `zmena`.

CREATE TABLE polozka (
    id                    int          IDENTITY(1,1) PRIMARY KEY,
    ciselnik_id           int          NOT NULL REFERENCES ciselniky(id),
    kod                   nvarchar(64)  NOT NULL,
    nazev                 nvarchar(512) NOT NULL,
    nadrazena_polozka_id  int          REFERENCES polozka(id),
    platnost_od           date         NOT NULL
        CONSTRAINT df_polozka_platnost_od DEFAULT CAST(SYSUTCDATETIME() AS date),
    platnost_do           date,
    aktivni               bit          NOT NULL CONSTRAINT df_polozka_aktivni DEFAULT 1,
    poradi                int          NOT NULL CONSTRAINT df_polozka_poradi DEFAULT 0,
    UNIQUE (ciselnik_id, kod),
    CONSTRAINT ck_polozka_platnost
        CHECK (platnost_do IS NULL OR platnost_do >= platnost_od),
    CONSTRAINT ck_polozka_neni_svym_rodicem
        CHECK (nadrazena_polozka_id IS NULL OR nadrazena_polozka_id <> id)
);

CREATE TABLE polozka_atribut (
    polozka_id           int NOT NULL REFERENCES polozka(id) ON DELETE CASCADE,
    atribut_definice_id  int NOT NULL REFERENCES atribut_definice(id),
    hodnota_text         nvarchar(max),
    hodnota_cislo        decimal(18,4),
    hodnota_datum        date,
    hodnota_ano_ne       bit,
    PRIMARY KEY (polozka_id, atribut_definice_id),
    -- Právě jeden typovaný sloupec je vyplněný. Bez této podmínky by se dvě
    -- různé hodnoty téhož atributu tiše rozešly a nikdo by nevěděl, která platí.
    CONSTRAINT ck_polozka_atribut_jedina_hodnota CHECK (
        CASE WHEN hodnota_text   IS NOT NULL THEN 1 ELSE 0 END
      + CASE WHEN hodnota_cislo  IS NOT NULL THEN 1 ELSE 0 END
      + CASE WHEN hodnota_datum  IS NOT NULL THEN 1 ELSE 0 END
      + CASE WHEN hodnota_ano_ne IS NOT NULL THEN 1 ELSE 0 END = 1
    )
);

CREATE TABLE polozka_vazba (
    polozka_id        int NOT NULL REFERENCES polozka(id) ON DELETE CASCADE,
    vazba_definice_id int NOT NULL REFERENCES vazba_definice(id),
    cil_polozka_id    int NOT NULL REFERENCES polozka(id),
    PRIMARY KEY (polozka_id, vazba_definice_id, cil_polozka_id)
);

-- Stránka hodnot jednoho číselníku je nejčastější dotaz aplikace.
CREATE INDEX ix_polozka_ciselnik    ON polozka (ciselnik_id, poradi, kod);
CREATE INDEX ix_polozka_nadrazena   ON polozka (nadrazena_polozka_id)
       WHERE nadrazena_polozka_id IS NOT NULL;
CREATE INDEX ix_polozka_platnost    ON polozka (ciselnik_id, platnost_od, platnost_do);

-- „Na které položky se odkazuje tahle?" — potřeba při vyřazování i při prokliku zpět.
CREATE INDEX ix_polozka_vazba_cil   ON polozka_vazba (cil_polozka_id);

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.4');
```

> **Všechna omezení jsou pojmenovaná.** SQL Server hlásí porušení kontrolní podmínky
> i cizího klíče **týmž číslem 547**, takže test, který chce obojí odlišit, se musí
> kotvit na název omezení v textu hlášky.

> **`ON DELETE CASCADE` je jen u atributů a vazeb směrem od položky.**
> Cíl vazby (`cil_polozka_id`) kaskádu nemá záměrně — smazání položky, na kterou někdo
> odkazuje, musí selhat, ne tiše odpojit odkaz. Hodnoty se stejně nemažou, jen vyřazují;
> kdyby se ale někdy mazalo, tohle je poslední pojistka.

- [ ] **Krok 2: Ukázková data**

`db/db_seed_ukazka.sql` založí dva provázané číselníky, aby šlo aplikaci hned proklikat:
**Osoby** (plochý) a **Rozpočtové cíle** (hierarchický, s atributy `cislo-cile`, `stav`,
`castka` a vazbou `manazer` na Osoby).

```sql
-- Ukázková data pro ruční ověření a ladění. V produkci se nespouští.
-- Textové literály mají předponu N — bez ní se diakritika uloží zkomoleně.
SET NOCOUNT ON;

DECLARE @osoby int, @cile int;
DECLARE @cislo int, @stav int, @castka int, @manazer int;
DECLARE @novak int, @dvorakova int, @cil1 int;

INSERT INTO ciselniky (kod, nazev, popis)
VALUES ('osoby', N'Osoby', N'Osoby vystupující v číselnících.');
SET @osoby = SCOPE_IDENTITY();

INSERT INTO ciselniky (kod, nazev, popis, hierarchicky)
VALUES ('cile', N'Rozpočtové cíle', N'Cíle rozpočtu podle struktury.', 1);
SET @cile = SCOPE_IDENTITY();

INSERT INTO atribut_definice (ciselnik_id, kod, nazev, typ, povinny, poradi)
VALUES (@cile, 'cislo-cile', N'Číslo cíle', 'TEXT', 1, 1);
SET @cislo = SCOPE_IDENTITY();

INSERT INTO atribut_definice (ciselnik_id, kod, nazev, typ, poradi, vycet_hodnot)
VALUES (@cile, 'stav', N'Stav', 'VYCET', 2, N'A;U;N');
SET @stav = SCOPE_IDENTITY();

INSERT INTO atribut_definice (ciselnik_id, kod, nazev, typ, poradi)
VALUES (@cile, 'castka', N'Částka', 'CISLO', 3);
SET @castka = SCOPE_IDENTITY();

INSERT INTO vazba_definice (ciselnik_id, kod, nazev, cilovy_ciselnik_id, poradi)
VALUES (@cile, 'manazer', N'Manažer cíle', @osoby, 1);
SET @manazer = SCOPE_IDENTITY();

INSERT INTO polozka (ciselnik_id, kod, nazev, platnost_od)
VALUES (@osoby, 'O-001', N'Jan Novák', '2026-01-01');
SET @novak = SCOPE_IDENTITY();

INSERT INTO polozka (ciselnik_id, kod, nazev, platnost_od)
VALUES (@osoby, 'O-002', N'Eva Dvořáková', '2026-01-01');
SET @dvorakova = SCOPE_IDENTITY();

INSERT INTO polozka (ciselnik_id, kod, nazev, platnost_od)
VALUES (@cile, 'C-2026-001', N'Rozvoj infrastruktury', '2026-01-01');
SET @cil1 = SCOPE_IDENTITY();

INSERT INTO polozka (ciselnik_id, kod, nazev, nadrazena_polozka_id, platnost_od)
VALUES (@cile, 'C-2026-001-A', N'Silnice II. třídy', @cil1, '2026-01-01');

INSERT INTO polozka (ciselnik_id, kod, nazev, platnost_od, platnost_do)
VALUES (@cile, 'C-2025-014', N'Obnova vozového parku', '2025-01-01', '2025-12-31');

INSERT INTO polozka_atribut (polozka_id, atribut_definice_id, hodnota_text)
VALUES (@cil1, @cislo, N'001'), (@cil1, @stav, N'A');

INSERT INTO polozka_atribut (polozka_id, atribut_definice_id, hodnota_cislo)
VALUES (@cil1, @castka, 1500000);

INSERT INTO polozka_vazba (polozka_id, vazba_definice_id, cil_polozka_id)
VALUES (@cil1, @manazer, @novak);
```

- [ ] **Krok 3: Napiš padající testy schématu**

```csharp
[Fact] public async Task Atribut_SDvemaVyplnenymiTypy_Odmitne()      // 547
[Fact] public async Task Atribut_BezJedineHodnoty_Odmitne()          // 547
[Fact] public async Task Polozka_NemuzeBytVlastnimRodicem()          // 547
[Fact] public async Task Polozka_PlatnostDoPredPlatnostiOd_Odmitne() // 547
[Fact] public async Task Polozka_DvakratTyzKodVCiselniku_Odmitne()   // 2627
```

- [ ] **Krok 4: Spusť, ověř pád, doplň entity a kontrolu schématu**

`Ciselniky.Core/Domain/Polozka.cs`:

```csharp
namespace Ciselniky.Core.Domain;

public sealed class Polozka
{
    public int Id { get; set; }
    public int CiselnikId { get; set; }
    public required string Kod { get; set; }
    public required string Nazev { get; set; }
    public int? NadrazenaPolozkaId { get; set; }
    public DateOnly PlatnostOd { get; set; }
    public DateOnly? PlatnostDo { get; set; }
    public bool Aktivni { get; set; } = true;
    public int Poradi { get; set; }
}

public sealed class PolozkaAtribut
{
    public int PolozkaId { get; set; }
    public int AtributDefiniceId { get; set; }
    public string? HodnotaText { get; set; }
    public decimal? HodnotaCislo { get; set; }
    public DateOnly? HodnotaDatum { get; set; }
    public bool? HodnotaAnoNe { get; set; }
}

public sealed class PolozkaVazba
{
    public int PolozkaId { get; set; }
    public int VazbaDefiniceId { get; set; }
    public int CilPolozkaId { get; set; }
}
```

V `CiselnikyDbContext` přibudou sady a složené klíče:

```csharp
public DbSet<Polozka> Polozky => Set<Polozka>();
public DbSet<PolozkaAtribut> PolozkaAtributy => Set<PolozkaAtribut>();
public DbSet<PolozkaVazba> PolozkaVazby => Set<PolozkaVazba>();

// v OnModelCreating
model.Entity<Polozka>().ToTable("polozka");
model.Entity<PolozkaAtribut>().ToTable("polozka_atribut")
     .HasKey(h => new { h.PolozkaId, h.AtributDefiniceId });
model.Entity<PolozkaVazba>().ToTable("polozka_vazba")
     .HasKey(v => new { v.PolozkaId, v.VazbaDefiniceId, v.CilPolozkaId });
```

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter HodnotySchemaTests   # Passed: 5
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(hodnoty): schéma položek, atributů a vazeb"
```

---

## Blok 2: Čtení hodnot

**Cíl bloku:** Stránka hodnot se složí **třemi dotazy**, ne dotazem na položku.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Hodnoty/HodnotaModely.cs`, `HodnotaCtenar.cs`
- Test: `Ciselniky.Tests.Integration/Hodnoty/HodnotaCtenarTests.cs`

**Rozhraní:**
- Poskytuje: `HodnotaCtenar.NactiStrankuAsync(DotazHodnot, CancellationToken) → Task<StrankaHodnot>`.

- [ ] **Krok 1: Modely odpovědi**

`Ciselniky.Core/Services/Hodnoty/HodnotaModely.cs`:

```csharp
namespace Ciselniky.Core.Services.Hodnoty;

public sealed record OdkazNaPolozku(string Ciselnik, string Kod, string Nazev);

public sealed record HodnotaPolozky(
    int Id,
    string Kod,
    string Nazev,
    string? NadrazenyKod,
    DateOnly PlatnostOd,
    DateOnly? PlatnostDo,
    bool Aktivni,
    IReadOnlyDictionary<string, object?> Atributy,
    IReadOnlyDictionary<string, IReadOnlyList<OdkazNaPolozku>> Vazby);

public sealed record DotazHodnot(
    int CiselnikId,
    string? Hledat = null,
    DateOnly? PlatneK = null,
    bool VcetneVyrazenych = false,
    int Strana = 1,
    int Velikost = 50);

public sealed record StrankaHodnot(
    IReadOnlyList<HodnotaPolozky> Polozky, int Celkem, int Strana, int Velikost);
```

> **Atributy jsou slovník podle kódu, ne pojmenované vlastnosti.** Kdyby byly vlastnostmi,
> potřeboval by každý číselník vlastní typ — a tím i vlastní službu a vlastní rozhraní.
> To je přesně to, čemu se aplikace vyhýbá.

- [ ] **Krok 2: Napiš padající test počtu dotazů**

`Ciselniky.Tests.Integration/Hodnoty/HodnotaCtenarTests.cs`:

```csharp
using Ciselniky.Core.Services.Hodnoty;
using Ciselniky.Tests.Integration.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ciselniky.Tests.Integration.Hodnoty;

public sealed class HodnotaCtenarTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task NactiStranku_SlozíPolozkyTremiDotazy_BezOhleduNaPocet()
    {
        await using var db = fixture.VytvorContextSPocitadlemDotazu(out var pocitadlo);
        var ciselnikId = await fixture.ZalozCiselnikSPolozkamiAsync(db, pocet: 60);
        var ctenar = new HodnotaCtenar(db);
        pocitadlo.Vynuluj();

        var stranka = await ctenar.NactiStrankuAsync(
            new DotazHodnot(ciselnikId, Velikost: 50), TestContext.Current.CancellationToken);

        Assert.Equal(50, stranka.Polozky.Count);
        Assert.Equal(60, stranka.Celkem);

        // 1 = položky, 2 = atributy stránky, 3 = vazby stránky, 4 = počet celkem.
        // Naivní implementace by udělala 1 + 2×50 dotazů.
        Assert.True(pocitadlo.Pocet <= 4,
            $"Očekávány nejvýš 4 dotazy, provedeno {pocitadlo.Pocet}. "
            + "Pravděpodobně se atributy nebo vazby načítají po položkách.");
    }

    [Fact]
    public async Task NactiStranku_VydaAtributyPodleKoduDefinice()
    {
        await using var db = fixture.VytvorContext();
        await fixture.SpustSkriptAsync(db, "db_seed_ukazka.sql");
        var ciselnikId = await fixture.IdCiselnikuAsync(db, "cile");
        var ctenar = new HodnotaCtenar(db);

        var stranka = await ctenar.NactiStrankuAsync(
            new DotazHodnot(ciselnikId), TestContext.Current.CancellationToken);

        var cil = stranka.Polozky.Single(p => p.Kod == "C-2026-001");
        Assert.Equal("001", cil.Atributy["cislo-cile"]);
        Assert.Equal("A", cil.Atributy["stav"]);
        Assert.Equal(1500000m, cil.Atributy["castka"]);
    }

    [Fact]
    public async Task NactiStranku_VydaVazbuJakoOdkazSKodemACiselnikem()
    {
        await using var db = fixture.VytvorContext();
        await fixture.SpustSkriptAsync(db, "db_seed_ukazka.sql");
        var ciselnikId = await fixture.IdCiselnikuAsync(db, "cile");
        var ctenar = new HodnotaCtenar(db);

        var stranka = await ctenar.NactiStrankuAsync(
            new DotazHodnot(ciselnikId), TestContext.Current.CancellationToken);

        var manazer = stranka.Polozky.Single(p => p.Kod == "C-2026-001").Vazby["manazer"].Single();

        // Odkaz, ne vnořený obsah cílové položky. Kdyby se vnořovala, měl by
        // každý číselník jiný tvar odpovědi a jedno rozhraní by je neobsloužilo.
        Assert.Equal("osoby", manazer.Ciselnik);
        Assert.Equal("O-001", manazer.Kod);
        Assert.Equal("Jan Novák", manazer.Nazev);
    }

    [Fact]
    public async Task NactiStranku_Hledani_ProhledavaKodINazev()
    {
        await using var db = fixture.VytvorContext();
        await fixture.SpustSkriptAsync(db, "db_seed_ukazka.sql");
        var ciselnikId = await fixture.IdCiselnikuAsync(db, "cile");
        var ctenar = new HodnotaCtenar(db);
        var ct = TestContext.Current.CancellationToken;

        var podleKodu = await ctenar.NactiStrankuAsync(
            new DotazHodnot(ciselnikId, Hledat: "C-2026-001"), ct);
        var podleNazvu = await ctenar.NactiStrankuAsync(
            new DotazHodnot(ciselnikId, Hledat: "silnice"), ct);   // i jinou velikostí písmen

        Assert.Equal(2, podleKodu.Celkem);            // cíl i jeho potomek C-2026-001-A
        Assert.Single(podleNazvu.Polozky);
        Assert.Equal("C-2026-001-A", podleNazvu.Polozky[0].Kod);
    }
}
```

`SqlFixture` doplní tři pomocníky používané v testech tohoto plánu:

```csharp
public CiselnikyDbContext VytvorContextSPocitadlemDotazu(out PocitadloDotazu pocitadlo)
{
    var p = new PocitadloDotazu();
    pocitadlo = p;
    return new CiselnikyDbContext(new DbContextOptionsBuilder<CiselnikyDbContext>()
        .UseSqlServer(_pripojeni).AddInterceptors(p).Options);
}

public async Task SpustSkriptAsync(CiselnikyDbContext db, string nazev)
    => await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(NajdiSkript(nazev)));

public async Task<int> IdCiselnikuAsync(CiselnikyDbContext db, string kod)
    => await db.Ciselniky.Where(c => c.Kod == kod).Select(c => c.Id).SingleAsync();

/// <summary>Založí číselník s daným počtem položek, každou s vyplněným atributem.</summary>
public async Task<int> ZalozCiselnikSPolozkamiAsync(CiselnikyDbContext db, int pocet)
{
    var ciselnik = new Ciselnik
    {
        Kod = "vykon-" + Guid.NewGuid().ToString("N")[..8],
        Nazev = "Výkonnostní"
    };
    db.Ciselniky.Add(ciselnik);
    await db.SaveChangesAsync();

    var atribut = new AtributDefinice
    {
        CiselnikId = ciselnik.Id, Kod = "popis", Nazev = "Popis",
        Typ = TypAtributu.Text, Poradi = 1
    };
    db.AtributDefinice.Add(atribut);
    await db.SaveChangesAsync();

    var polozky = Enumerable.Range(1, pocet).Select(i => new Polozka
    {
        CiselnikId = ciselnik.Id,
        Kod = $"P-{i:D5}",
        Nazev = $"Položka {i}",
        Poradi = i
    }).ToList();
    db.Polozky.AddRange(polozky);
    await db.SaveChangesAsync();

    db.PolozkaAtributy.AddRange(polozky.Select(p => new PolozkaAtribut
    {
        PolozkaId = p.Id, AtributDefiniceId = atribut.Id, HodnotaText = "popis " + p.Kod
    }));
    await db.SaveChangesAsync();

    return ciselnik.Id;
}
```

a interceptor, který počítá provedené příkazy:

```csharp
public sealed class PocitadloDotazu : IDbCommandInterceptor
{
    private int _pocet;
    public int Pocet => _pocet;
    public void Vynuluj() => Interlocked.Exchange(ref _pocet, 0);

    public InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Interlocked.Increment(ref _pocet); return result; }

    public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    { Interlocked.Increment(ref _pocet); return ValueTask.FromResult(result); }
}
```

> **Tenhle test je nejcennější v celém P4.** Načítání atributů po položkách je nejsnadnější
> chyba v tomhle druhu uložení a projeví se až na tisícovce hodnot v produkci —
> na ukázkových třech položkách si jí nikdo nevšimne.

- [ ] **Krok 3: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter HodnotaCtenarTests
```

- [ ] **Krok 4: Čtenář**

`Ciselniky.Core/Services/Hodnoty/HodnotaCtenar.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Hodnoty;

/// <summary>
/// Skládá stránku hodnot. Tři dotazy na stránku: položky, jejich atributy, jejich vazby.
/// <b>Nikdy dotaz na položku</b> — počet dotazů nesmí růst s počtem řádků.
/// </summary>
public sealed class HodnotaCtenar(CiselnikyDbContext db)
{
    public async Task<StrankaHodnot> NactiStrankuAsync(
        DotazHodnot dotaz, CancellationToken ct = default)
    {
        var zaklad = db.Polozky.AsNoTracking().Where(p => p.CiselnikId == dotaz.CiselnikId);

        if (!dotaz.VcetneVyrazenych)
            zaklad = zaklad.Where(p => p.Aktivni);

        if (dotaz.PlatneK is { } kDatu)
            zaklad = zaklad.Where(p => p.PlatnostOd <= kDatu
                                    && (p.PlatnostDo == null || p.PlatnostDo >= kDatu));

        if (!string.IsNullOrWhiteSpace(dotaz.Hledat))
        {
            var vzorek = $"%{dotaz.Hledat.Trim()}%";
            zaklad = zaklad.Where(p => EF.Functions.Like(p.Kod, vzorek)
                                    || EF.Functions.Like(p.Nazev, vzorek));
        }

        var celkem = await zaklad.CountAsync(ct);

        var polozky = await zaklad
            .OrderBy(p => p.Poradi).ThenBy(p => p.Kod)
            .Skip((dotaz.Strana - 1) * dotaz.Velikost)
            .Take(dotaz.Velikost)
            .ToListAsync(ct);

        var ids = polozky.Select(p => p.Id).ToArray();

        var atributy = await (
            from hodnota in db.PolozkaAtributy.AsNoTracking()
            where ids.Contains(hodnota.PolozkaId)
            join definice in db.AtributDefinice on hodnota.AtributDefiniceId equals definice.Id
            select new { hodnota, definice.Kod, definice.Typ })
            .ToListAsync(ct);

        var vazby = await (
            from vazba in db.PolozkaVazby.AsNoTracking()
            where ids.Contains(vazba.PolozkaId)
            join definice in db.VazbaDefinice on vazba.VazbaDefiniceId equals definice.Id
            join cil in db.Polozky on vazba.CilPolozkaId equals cil.Id
            join cilovyCiselnik in db.Ciselniky on cil.CiselnikId equals cilovyCiselnik.Id
            select new { vazba.PolozkaId, DefiniceKod = definice.Kod,
                         CiselnikKod = cilovyCiselnik.Kod, cil.Kod, cil.Nazev })
            .ToListAsync(ct);

        // Kód nadřazené položky je potřeba i tehdy, když rodič na stránce není.
        var rodicovskeIds = polozky.Where(p => p.NadrazenaPolozkaId is not null)
                                   .Select(p => p.NadrazenaPolozkaId!.Value).Distinct().ToArray();
        var kodyRodicu = rodicovskeIds.Length == 0
            ? []
            : await db.Polozky.AsNoTracking().Where(p => rodicovskeIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Kod, ct);

        var atributyPodlePolozky = atributy
            .GroupBy(x => x.hodnota.PolozkaId)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyDictionary<string, object?>)g.ToDictionary(
                    x => x.Kod, x => Hodnota(x.hodnota, x.Typ)));

        var vazbyPodlePolozky = vazby
            .GroupBy(x => x.PolozkaId)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyDictionary<string, IReadOnlyList<OdkazNaPolozku>>)
                     g.GroupBy(x => x.DefiniceKod).ToDictionary(
                         vg => vg.Key,
                         vg => (IReadOnlyList<OdkazNaPolozku>)vg
                               .Select(x => new OdkazNaPolozku(x.CiselnikKod, x.Kod, x.Nazev))
                               .ToList()));

        var vysledek = polozky.Select(p => new HodnotaPolozky(
            p.Id, p.Kod, p.Nazev,
            p.NadrazenaPolozkaId is { } rodic && kodyRodicu.TryGetValue(rodic, out var kodRodice)
                ? kodRodice : null,
            p.PlatnostOd, p.PlatnostDo, p.Aktivni,
            atributyPodlePolozky.GetValueOrDefault(p.Id)
                ?? new Dictionary<string, object?>(),
            vazbyPodlePolozky.GetValueOrDefault(p.Id)
                ?? new Dictionary<string, IReadOnlyList<OdkazNaPolozku>>()))
            .ToList();

        return new StrankaHodnot(vysledek, celkem, dotaz.Strana, dotaz.Velikost);
    }

    private static object? Hodnota(PolozkaAtribut hodnota, TypAtributu typ) => typ switch
    {
        TypAtributu.Cislo  => hodnota.HodnotaCislo,
        TypAtributu.Datum  => hodnota.HodnotaDatum,
        TypAtributu.AnoNe  => hodnota.HodnotaAnoNe,
        _                  => hodnota.HodnotaText,
    };
}
```

> Dotaz na kódy rodičů je **čtvrtý**, ale jen když na stránce nějaký rodič je,
> a je to jeden dotaz na stránku — ne na položku. Proto má test limit čtyři.

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter HodnotaCtenarTests   # Passed: 4
git add -A && git commit -m "feat(hodnoty): čtení stránky hodnot třemi dotazy"
```

---

## Blok 3: Hierarchie a strom

**Cíl bloku:** Hierarchický číselník se vydá plochý i jako strom. Zacyklení nemůže vzniknout.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Hodnoty/StromSestavovac.cs`
- Test: `Ciselniky.Tests.Unit/Hodnoty/StromSestavovacTests.cs`
- Test: `Ciselniky.Tests.Integration/Hodnoty/HierarchieTests.cs`

**Rozhraní:**
- Poskytuje: `StromSestavovac.Sestav(IReadOnlyList<HodnotaPolozky>) → IReadOnlyList<UzelStromu>`;
  `HierarchieKontrola.OverBezCykluAsync(db, polozkaId, novyRodicId, ct)`.

- [ ] **Krok 1: Napiš padající testy sestavení stromu**

```csharp
[Fact] public void Sestav_VnoriDetiPodRodice()
[Fact] public void Sestav_PolozkuBezRodice_DaNaKorenovouUroven()
[Fact] public void Sestav_PolozkuSRodicemMimoVstup_DaNaKorenovouUroven()
[Fact] public void Sestav_ZachovavaPoradiSourozencu()
[Fact] public void Sestav_ZacyklenyVstup_NezacykliSe()
```

Třetí a pátý test jsou ty podstatné. Rodič mimo vstup nastane při stránkování —
a sestavovač nesmí položku zahodit. Zacyklení by nemělo vzniknout, ale sestavovač
běží nad daty, ne nad zárukami, a **zacyklit se nesmí ani na porušených datech**.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Unit --filter StromSestavovacTests
```

- [ ] **Krok 3: Sestavovač**

```csharp
namespace Ciselniky.Core.Services.Hodnoty;

public sealed record UzelStromu(HodnotaPolozky Polozka, IReadOnlyList<UzelStromu> Deti);

public static class StromSestavovac
{
    public static IReadOnlyList<UzelStromu> Sestav(IReadOnlyList<HodnotaPolozky> polozky)
    {
        var podleKodu = polozky.ToDictionary(p => p.Kod);
        var deti = polozky
            .Where(p => p.NadrazenyKod is not null && podleKodu.ContainsKey(p.NadrazenyKod))
            .GroupBy(p => p.NadrazenyKod!)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Kořen = položka bez rodiče NEBO s rodičem mimo vstup (nastane při stránkování).
        var koreny = polozky.Where(p => p.NadrazenyKod is null
                                     || !podleKodu.ContainsKey(p.NadrazenyKod));

        var navstivene = new HashSet<string>();
        return koreny.Select(k => Uzel(k, navstivene, deti)).ToList();
    }

    private static UzelStromu Uzel(HodnotaPolozky polozka, HashSet<string> navstivene,
                                   Dictionary<string, List<HodnotaPolozky>> deti)
    {
        // Pojistka proti zacyklení na porušených datech. Bez ní by cyklus
        // v datech shodil server přetečením zásobníku, ne vrácením chyby.
        if (!navstivene.Add(polozka.Kod))
            return new UzelStromu(polozka, []);

        var potomci = deti.TryGetValue(polozka.Kod, out var seznam)
            ? seznam.Select(d => Uzel(d, navstivene, deti)).ToList()
            : [];

        return new UzelStromu(polozka, potomci);
    }
}
```

- [ ] **Krok 4: Kontrola cyklu při určení rodiče**

Sestavovač se cyklu ubrání, ale vzniknout nesmí. Kontrola se volá při ukládání
(zápisová cesta přijde v P5, kontrola vzniká už tady, aby ji P5 jen zavolal):

`Ciselniky.Core/Services/Hodnoty/HierarchieKontrola.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Hodnoty;

public static class HierarchieKontrola
{
    /// <summary>
    /// Ověří, že určením rodiče nevznikne cyklus. Prochází nahoru od navrhovaného rodiče;
    /// narazí-li na samotnou položku, cyklus by vznikl.
    /// </summary>
    public static async Task OverBezCykluAsync(
        CiselnikyDbContext db, int polozkaId, int? novyRodicId, CancellationToken ct = default)
    {
        var kroky = 0;
        var uzel = novyRodicId;

        while (uzel is not null)
        {
            if (uzel == polozkaId)
                throw new ValidacniVyjimka([new(null, "nadrazenyKod",
                    "Položka nemůže být podřízená sama sobě ani svému potomkovi.")]);

            if (++kroky > 1000)
                throw new InvalidOperationException(
                    "Hierarchie je hlubší než 1000 úrovní nebo obsahuje cyklus.");

            uzel = await db.Polozky.Where(p => p.Id == uzel)
                           .Select(p => p.NadrazenaPolozkaId).FirstOrDefaultAsync(ct);
        }
    }
}
```

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Unit --filter StromSestavovacTests        # Passed: 5
dotnet test Ciselniky.Tests.Integration --filter HierarchieTests
git add -A && git commit -m "feat(hodnoty): sestavení stromu a ochrana proti zacyklení hierarchie"
```

---

## Blok 4: Platnost k datu a vyřazené hodnoty

**Cíl bloku:** Uživatel vidí, co platí k zadanému datu. Vyřazené hodnoty nezmizí — jen
se nezobrazují, dokud si je nevyžádá.

**Soubory:**
- Uprav: `HodnotaCtenar.cs` (filtr už je z bloku 2 — tady se doplní testy a hraniční případy)
- Test: `Ciselniky.Tests.Integration/Hodnoty/PlatnostTests.cs`

- [ ] **Krok 1: Napiš testy hraničních případů**

```csharp
[Fact] public async Task PlatneK_DenZacatkuPlatnosti_PolozkuVrati()      // od == datum
[Fact] public async Task PlatneK_DenKoncePlatnosti_PolozkuVrati()        // do == datum
[Fact] public async Task PlatneK_DenPoKonci_PolozkuNevrati()
[Fact] public async Task PlatneK_BezKonce_PolozkuVrati()                 // do IS NULL
[Fact] public async Task BezFiltru_VratiIPolozkyMimoPlatnost()
[Fact] public async Task VcetneVyrazenych_VratiINeaktivni()
```

> Hranice **jsou včetně**. Den konce platnosti je poslední den, kdy hodnota platí —
> ne první, kdy neplatí. Kdyby to bylo obráceně, každý převod z papírové směrnice
> by se o den rozešel a nikdo by nevěděl proč.

- [ ] **Krok 2: Ověř, doplň chybějící chování, commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter PlatnostTests   # Passed: 6
git add -A && git commit -m "test(hodnoty): hraniční případy filtru platnosti k datu"
```

---

## Blok 5: Ochrana definice před ztrátou dat

**Cíl bloku:** Atribut ani vazba, na kterých visí hodnoty, nejdou odebrat bez rozmyslu.
**Uzavírá dluh D5 z P3.**

**Soubory:**
- Uprav: `Ciselniky.Core/Services/Ciselniky/DefiniceSluzba.cs`
- Test: `Ciselniky.Tests.Integration/Ciselniky/OdebraniDefiniceTests.cs`

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task OdeberAtribut_BezHodnot_Projde()
[Fact] public async Task OdeberAtribut_SHodnotami_OdmitneAUvedePocet()
[Fact] public async Task OdeberAtribut_SHodnotami_SPotvrzenim_ProjdeAOdstraniHodnoty()
[Fact] public async Task OdeberVazbu_SVyplnenymiVazbami_OdmitneAUvedePocet()
```

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter OdebraniDefiniceTests
```

- [ ] **Krok 3: Doplň ochranu**

V `DefiniceSluzba` se `OdeberAtributAsync` z P3 nahradí:

```csharp
/// <summary>
/// Odebere atribut z definice. Visí-li na něm hodnoty, odmítne — pokud volající
/// výslovně nepotvrdí, že o ně smí přijít.
/// </summary>
public async Task OdeberAtributAsync(int atributId, bool potvrzenoOdstraneniHodnot = false,
                                     CancellationToken ct = default)
{
    var atribut = await db.AtributDefinice.FindAsync([atributId], ct)
        ?? throw new InvalidOperationException($"Atribut {atributId} neexistuje.");

    var pocetHodnot = await db.PolozkaAtributy
        .CountAsync(h => h.AtributDefiniceId == atributId, ct);

    if (pocetHodnot > 0 && !potvrzenoOdstraneniHodnot)
        throw new ValidacniVyjimka([new(null, "atribut",
            $"Atribut „{atribut.Nazev}\" má vyplněnou hodnotu u {pocetHodnot} položek. "
            + "Odebráním o ně nenávratně přijdete. Potvrďte, že to opravdu chcete.")]);

    if (pocetHodnot > 0)
        db.PolozkaAtributy.RemoveRange(
            db.PolozkaAtributy.Where(h => h.AtributDefiniceId == atributId));

    db.AtributDefinice.Remove(atribut);
    await db.SaveChangesAsync(ct);
}
```

> **Tohle je jediné místo v aplikaci, kde se data opravdu mažou** — a jde o to jediné
> místo, kde to dává smysl: atribut přestal existovat, takže jeho hodnoty nemají kam patřit.
> Vyřazení platnosti by tu neznamenalo nic, protože není co vyřadit.
>
> Právě proto to vyžaduje výslovné potvrzení a hláška uvádí **počet** dotčených položek.
> „Opravdu chcete pokračovat?" bez čísla je otázka, na kterou nikdo nemá jak odpovědět.

Totéž pro `OdeberVazbuAsync` nad `polozka_vazba`.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter OdebraniDefiniceTests   # Passed: 4
git add -A && git commit -m "fix(ciselniky): odebrání atributu s hodnotami vyžaduje potvrzení"
```

---

## Blok 6: Koncové body pro čtení hodnot

**Cíl bloku:** Hodnoty jdou přečíst přes rozhraní. Čtení je bez oprávnění.

**Soubory:**
- Uprav: `Ciselniky.Api/Controllers/Vnitrni/CiselnikyController.cs`
- Test: `Ciselniky.Tests.Api/HodnotyControllerTests.cs`

- [ ] **Krok 1: Koncové body**

```csharp
[HttpGet("internal/ciselniky/{kod}/polozky")]
[AllowAnonymous]                                    // čtení nemá klíč (rozhodnutí N1)
public async Task<IActionResult> Polozky(
    string kod,
    [FromQuery] string? hledat,
    [FromQuery] DateOnly? platneK,
    [FromQuery] string tvar = "plochy",             // plochy | strom
    [FromQuery] bool vcetneVyrazenych = false,
    [FromQuery] int strana = 1,
    [FromQuery] int velikost = 50,
    CancellationToken ct = default)
```

Při `tvar=strom` se stránkování **nepoužije** — strom rozdělený na stránky by rozpojil
větve. Vrací se celý číselník; u hierarchických číselníků je to nejvýš tisíce položek.

- [ ] **Krok 2: Testy**

```csharp
[Fact] public async Task Polozky_JsouDostupneBezPrihlaseni()
[Fact] public async Task Polozky_TvarStrom_VraciVnoreneUzly()
[Fact] public async Task Polozky_PlatneK_FiltrujePodleData()
[Fact] public async Task Polozky_NeznamyCiselnik_Vrati404()
[Fact] public async Task Polozky_VelikostNadLimit_Osekne()
```

Poslední test drží strop stránky (500). Bez něj by `velikost=1000000` byl způsob,
jak jedním požadavkem vytížit server.

- [ ] **Krok 3: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter HodnotyControllerTests   # Passed: 5
git add -A && git commit -m "feat(api): koncové body pro čtení hodnot"
```

---

## Blok 7: Obrazovka detailu — záložka Hodnoty

**Cíl bloku:** Uživatel vidí hodnoty podle wireframu O2. Uzavírá čtecí cestu aplikace.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/DetailCiselniku.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/TabulkaHodnot.tsx`, `StromHodnot.tsx`
- Test: `ciselniky-web/src/komponenty/TabulkaHodnot.test.tsx`
- Test: `Ciselniky.Tests.E2E/Scenare/DetailCiselnikuTests.cs`

- [ ] **Krok 1: Hlavička a záložky**

Podle wireframu: název, pod ním `kód · verze · publikováno · režim správy`, vpravo akce.
Záložky Hodnoty / Struktura / Verze / Rozdíl verzí.

> **Je-li číselník externí, tlačítko `Upravit hodnoty` tam není vůbec — ne zašedlé.**
> Zašedlé tlačítko slibuje, že to jednou půjde. U externího číselníku to nepůjde nikdy
> (rozhodnutí E3).

```tsx
{ciselnik.rezimSpravy === 'RUCNI' && smim('hodnoty.edit', ciselnik.kod) && (
  <PmButton varianta="primary" onClick={naEditaci}>Upravit hodnoty</PmButton>
)}
```

- [ ] **Krok 2: Nástrojová lišta**

Hledání, `Platné k` **předvyplněné dneškem**, přepínač Seznam / Strom.
Přepínač se u nehierarchického číselníku nezobrazuje — nemá co přepínat.

Nejčastější otázka je „co platí teď", proto je dnešek výchozí. Kdo se ptá na jiné datum,
ho přepíše.

- [ ] **Krok 3: Tabulka**

Sloupce se skládají **z definice**, ne natvrdo: kód, název, pak atributy podle pořadí,
pak vazby, nakonec platnost.

```tsx
const sloupce = [
  { klic: 'kod',   popisek: 'Kód'   },
  { klic: 'nazev', popisek: 'Název' },
  ...definice.atributy.map((a) => ({ klic: `atribut:${a.kod}`, popisek: a.nazev })),
  ...definice.vazby.map((v)   => ({ klic: `vazba:${v.kod}`,   popisek: v.nazev   })),
  { klic: 'platnost', popisek: 'Platnost' },
]
```

**Vazba se vykresluje jako odkaz s prokliknutím na cílovou položku** — je to hrana grafu
a chová se jako odkaz, protože to odkaz je:

```tsx
<Link to={`/ciselnik/${odkaz.ciselnik}?polozka=${odkaz.kod}`}>
  {odkaz.nazev} <gov-icon type="components" name="arrow-right" />
</Link>
```

- [ ] **Krok 4: Strom**

Rozbalování po úrovních, odsazení podle hloubky. Stav rozbalení **se drží v adrese**,
aby se po návratu z prokliknuté vazby strom neotevřel od začátku.

- [ ] **Krok 5: Testy komponent**

```tsx
test('sloupce vznikají z definice, ne natvrdo')
test('vazba se vykreslí jako odkaz na cílový číselník')
test('přepínač strom se u nehierarchického číselníku nezobrazí')
test('u externího číselníku chybí tlačítko Upravit hodnoty')
```

- [ ] **Krok 6: Koncový test**

`Ciselniky.Tests.E2E/Scenare/DetailCiselnikuTests.cs`:

```csharp
[Fact] public async Task Detail_ZobrazíHodnotyZUkazkovehoCiselniku()
[Fact] public async Task Detail_ProklikVazby_PrenesouNaOsobu()
[Fact] public async Task Detail_PrepnutiNaStrom_VnoriPodrizeneCile()
```

- [ ] **Krok 7: Plná sada a commit**

```bash
cd ciselniky-web && npx vitest run && cd ..
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(web): detail číselníku se záložkou Hodnoty, stromem a proklikem vazeb"
```

---

## Po dokončení P4 ručně ověř

1. Spusť `db/db_seed_ukazka.sql` na vývojové databázi.
2. Otevři **Rozpočtové cíle** — vidíš tři hodnoty se sloupci podle definice.
3. Přepni na **Strom** — *Silnice II. třídy* jsou vnořené pod *Rozvoj infrastruktury*.
4. Nastav `Platné k` na `1. 6. 2025` — objeví se *Obnova vozového parku*, ostatní zmizí.
5. Klikni na manažera *Jan Novák* — přeneseš se do číselníku **Osoby** na tu položku.
6. Vrať se zpět — strom je rozbalený tak, jak jsi ho nechal.
7. Zkus u číselníku v externím režimu najít tlačítko `Upravit hodnoty` — **není tam**.
8. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D7 | Zápisová cesta pro hodnoty v P4 není — jde jen o čtení. Ukládání přes rozpracované změny. | **P5** |
| D8 | `HierarchieKontrola.OverBezCykluAsync` je napsaná, ale nikdo ji nevolá — volající je zápisová cesta. | **P5** |
| D9 | Strop stránky 500 je natvrdo v controlleru — přesunout do nastavení aplikace spolu s bázovou adresou (D6). | **P8** |
