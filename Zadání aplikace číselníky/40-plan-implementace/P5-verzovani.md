# P5 — Verzování: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Číselník má verze. Změny se ukládají jako rozpracované a teprve publikováním
vzniká verze, kterou uvidí konzumenti. Historická verze se **dopočítá**, neukládá se znovu.

**Architektura:** Tabulky hodnot drží **publikovaný** stav. Každá úprava je řádek v záznamu
změn; rozpracovaná změna má nepřiřazenou verzi. Publikování jí verzi přiřadí a promítne ji
do publikovaného stavu. Historická verze vzniká zpětnou aplikací změn.

**Stack:** .NET 10, EF Core + SQL Server, React + TypeScript, xUnit.

**Specifikace:** [../10-specifikace/01-domenovy-model.md](../10-specifikace/01-domenovy-model.md) ·
**Datový model:** [../20-architektura/04-datovy-model.md](../20-architektura/04-datovy-model.md) ·
**Wireframy:** [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) (O2 — Verze, Rozdíl verzí)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P4 — `polozka`, `polozka_atribut`, `polozka_vazba`, `HodnotaCtenar`.
**Uzavírá dluhy D4 (P3), D7 a D8 (P4).**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Databáze unese verze a záznam změn; rozpracovaná změna má nepřiřazenou verzi |
| 2 | Úprava hodnoty se uloží jako rozpracovaná změna, ne do publikovaného stavu |
| 3 | Editor vidí publikovaný stav překrytý svými rozpracovanými změnami |
| 4 | Publikování vydá verzi, promítne změny a zvýší správné číslo |
| 5 | Historická verze se dopočítá zpětnou aplikací změn |
| 6 | Rozdíl mezi dvěma verzemi |
| 7 | Koncové body pro změny, verze a rozdíl |
| 8 | Záložky Verze a Rozdíl verzí podle wireframu O2 |

---

## Dvě věty, ze kterých plyne celý plán

> **Tabulky hodnot drží publikovaný stav.**
> **Rozpracované změny jsou tytéž řádky záznamu změn, jen s nepřiřazenou verzí.**

Z toho plyne, kde je horká a kde studená cesta:

| Cesta | Kdo ji používá | Jak funguje |
|---|---|---|
| Čtení publikovaného stavu | neomezený počet lidí a aplikací | prostý dotaz do tabulky |
| Čtení s rozpracovanými změnami | editor, 5–20 lidí | dotaz + překryv v paměti |
| Dopočet historické verze | občas | dotaz + zpětná aplikace změn |

**Opačné uspořádání by dopočítávalo při každém dotazu konzumenta.** Dopočet patří tam,
kde je málo lidí.

---

## Blok 1: Schéma verzí a změn

**Cíl bloku:** Databáze unese verze a záznam změn.

**Soubory:**
- Vytvoř: `db/db_upgrade_0_5_verzovani.sql`
- Vytvoř: `Ciselniky.Core/Domain/CiselnikVerze.cs`, `Zmena.cs`
- Uprav: `Data/CiselnikyDbContext.cs`, `Data/KontrolaSchematu.cs`
- Test: `Ciselniky.Tests.Integration/Data/VerzovaniSchemaTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyDbContext.Verze`, `.Zmeny`; enumy `DruhZmeny`, `OperaceZmeny`.

- [ ] **Krok 1: Migrační skript**

`db/db_upgrade_0_5_verzovani.sql`:

```sql
-- Verzování. Uložený je publikovaný stav a záznamy změn; historická verze se dopočítá.

CREATE TABLE ciselnik_verze (
    id              int     IDENTITY(1,1) PRIMARY KEY,
    ciselnik_id     int     NOT NULL REFERENCES ciselniky(id),
    cislo_hlavni    int     NOT NULL,
    cislo_vedlejsi  int     NOT NULL,
    vydana_kdy      datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
    vydal_id        int     NOT NULL REFERENCES osoby(id),
    poznamka        nvarchar(512),
    UNIQUE (ciselnik_id, cislo_hlavni, cislo_vedlejsi)
);

CREATE TABLE zmena (
    id                   int     IDENTITY(1,1) PRIMARY KEY,
    ciselnik_id          int     NOT NULL REFERENCES ciselniky(id),

    -- NULL = rozpracovaná, dosud nepublikovaná změna.
    verze_id             int     REFERENCES ciselnik_verze(id),

    polozka_id           int     REFERENCES polozka(id),   -- NULL u druhu DEFINICE
    druh                 nvarchar(16) NOT NULL
        CHECK (druh IN ('POLOZKA', 'ATRIBUT', 'VAZBA', 'DEFINICE')),
    atribut_definice_id  int     REFERENCES atribut_definice(id),
    vazba_definice_id    int     REFERENCES vazba_definice(id),
    operace              nvarchar(16) NOT NULL
        CHECK (operace IN ('PRIDANO', 'ZMENENO', 'ODEBRANO')),

    -- U druhu POLOZKA určuje, který společný sloupec se změnil:
    -- kod | nazev | nadrazenyKod | platnostOd | platnostDo | aktivni | poradi
    pole                 nvarchar(32),

    hodnota_pred         nvarchar(max),
    hodnota_po           nvarchar(max),
    kdo_id               int     NOT NULL REFERENCES osoby(id),
    kdy                  datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),

    CHECK (druh <> 'ATRIBUT'  OR atribut_definice_id IS NOT NULL),
    CHECK (druh <> 'VAZBA'    OR vazba_definice_id   IS NOT NULL),
    CHECK (druh <> 'POLOZKA'  OR pole                IS NOT NULL)
);

-- Opakovaná úprava téže hodnoty před publikováním nezakládá druhý řádek.
-- Jinak by verze obsahovala mezikroky, které nikdy nikdo neviděl.
--
-- SQL Server považuje v jedinečném indexu dva NULL za SHODNÉ, takže se sloupce,
-- které mohou být prázdné, uvádějí přímo — žádné obalování není potřeba.
CREATE UNIQUE INDEX ux_zmena_rozpracovana ON zmena
    (ciselnik_id, polozka_id, druh, atribut_definice_id, vazba_definice_id, pole)
    WHERE verze_id IS NULL;

-- Dopočet historické verze čte změny odzadu po verzích.
CREATE INDEX ix_zmena_verze     ON zmena (ciselnik_id, verze_id);
CREATE INDEX ix_zmena_polozka   ON zmena (polozka_id) WHERE polozka_id IS NOT NULL;
CREATE INDEX ix_verze_ciselnik  ON ciselnik_verze (ciselnik_id, cislo_hlavni, cislo_vedlejsi);

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.5');
```

> **Hodnoty se ukládají jako text, i když jsou číslo nebo datum.** Záznam změn je
> **historie, ne pracovní data** — nikdy se podle něj nefiltruje ani neřadí. Typované
> sloupce by znamenaly čtyři další sloupce a kontrolní podmínku navíc, a přinesly by
> jedinou schopnost, kterou nikdo nepotřebuje. Převod na text je jednoznačný, protože
> typ je znám z definice atributu.

> **Částečný jedinečný index je to, co drží pravidlo o mezikrocích.** Vynucuje se
> v databázi, ne v aplikaci — dva souběžné požadavky by aplikační kontrolu obešly.

> **Chování `NULL` se tu liší od jiných databází.** SQL Server je v jedinečném indexu
> porovnává jako shodné, takže dvě rozpracované změny téhož atributu se odmítnou
> i tehdy, když jsou `polozka_id` a `vazba_definice_id` prázdné. Kdyby se plán někdy
> překládal zpátky na databázi, kde se `NULL` nerovná `NULL`, musela by se do klíče
> doplnit náhradní hodnota — jinak by pravidlo tiše přestalo platit.

- [ ] **Krok 2: Napiš padající testy schématu**

```csharp
[Fact] public async Task Zmena_TehozAtributuDvakrat_BezVerze_Odmitne()      // 2627
[Fact] public async Task Zmena_TehozAtributuDvakrat_RuznaVerze_Projde()
[Fact] public async Task Zmena_DruhuAtribut_BezDefinice_Odmitne()           // 547
[Fact] public async Task Zmena_DruhuPolozka_BezPole_Odmitne()               // 547
[Fact] public async Task Verze_TehozCislaDvakrat_Odmitne()                  // 2627
```

Druhý test je podstatný: **jedinečnost platí jen na rozpracovaných změnách.**
Publikovaná historie musí týž atribut obsahovat tolikrát, kolikrát se změnil.

- [ ] **Krok 3: Spusť, ověř pád, doplň entity**

```csharp
namespace Ciselniky.Core.Domain;

public enum DruhZmeny { Polozka, Atribut, Vazba, Definice }
public enum OperaceZmeny { Pridano, Zmeneno, Odebrano }

public sealed class CiselnikVerze
{
    public int Id { get; set; }
    public int CiselnikId { get; set; }
    public int CisloHlavni { get; set; }
    public int CisloVedlejsi { get; set; }
    public DateTimeOffset VydanaKdy { get; set; }
    public int VydalId { get; set; }
    public string? Poznamka { get; set; }

    public string Cislo => $"{CisloHlavni}.{CisloVedlejsi}";
}

public sealed class Zmena
{
    public int Id { get; set; }
    public int CiselnikId { get; set; }
    public int? VerzeId { get; set; }              // NULL = rozpracovaná
    public int? PolozkaId { get; set; }
    public DruhZmeny Druh { get; set; }
    public int? AtributDefiniceId { get; set; }
    public int? VazbaDefiniceId { get; set; }
    public OperaceZmeny Operace { get; set; }
    public string? Pole { get; set; }
    public string? HodnotaPred { get; set; }
    public string? HodnotaPo { get; set; }
    public int KdoId { get; set; }
    public DateTimeOffset Kdy { get; set; }
}```

V `CiselnikyDbContext` přibudou sady:

```csharp
public DbSet<CiselnikVerze> Verze => Set<CiselnikVerze>();
public DbSet<Zmena> Zmeny => Set<Zmena>();

// v OnModelCreating — enumy jako text velkými písmeny
model.Entity<CiselnikVerze>().ToTable("ciselnik_verze");
model.Entity<Zmena>().ToTable("zmena");
model.Entity<Zmena>().Property(z => z.Druh)
     .HasConversion(v => v.ToString().ToUpperInvariant(),
                    v => Enum.Parse<DruhZmeny>(v, ignoreCase: true));
model.Entity<Zmena>().Property(z => z.Operace)
     .HasConversion(v => v.ToString().ToUpperInvariant(),
                    v => Enum.Parse<OperaceZmeny>(v, ignoreCase: true));
```

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter VerzovaniSchemaTests   # Passed: 5
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(verze): schéma verzí a záznamu změn"
```

---

## Blok 2: Zápisová cesta přes rozpracované změny

**Cíl bloku:** Úprava hodnoty **nikdy nesahá na publikovaný stav**. Zapíše se jako
rozpracovaná změna. **Uzavírá dluhy D7 a D8 z P4.**

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Verze/ZmenaZapisovac.cs`
- Vytvoř: `Ciselniky.Core/Services/Verze/NavrhPolozky.cs`
- Test: `Ciselniky.Tests.Integration/Verze/ZmenaZapisovacTests.cs`

**Rozhraní:**
- Poskytuje: `ZmenaZapisovac.UlozAsync(int ciselnikId, IReadOnlyList<NavrhPolozky>, int kdoId, CancellationToken)`.
- Používá: `HierarchieKontrola.OverBezCykluAsync` z P4 (dluh D8).

- [ ] **Krok 1: Tvar návrhu**

`Ciselniky.Core/Services/Verze/NavrhPolozky.cs`:

```csharp
namespace Ciselniky.Core.Services.Verze;

/// <summary>
/// Stav položky, jak ho chce mít uživatel. Zapisovač si sám dopočítá,
/// co se proti publikovanému stavu změnilo — volající rozdíly nepočítá.
/// </summary>
public sealed record NavrhPolozky(
    int? Id,                       // null = nová položka
    string Kod,
    string Nazev,
    string? NadrazenyKod,
    DateOnly PlatnostOd,
    DateOnly? PlatnostDo,
    bool Aktivni,
    IReadOnlyDictionary<string, string?> Atributy,      // klíč = kód atributu
    IReadOnlyDictionary<string, IReadOnlyList<string>> Vazby);  // klíč = kód vazby, hodnota = kódy cílů
```

> **Volající posílá stav, ne rozdíl.** Kdyby posílal rozdíl, musel by ho počítat prohlížeč
> nebo importní modul — dvakrát tutéž logiku, dvě příležitosti k rozejití. Rozdíl počítá
> jedno místo: zapisovač.

- [ ] **Krok 2: Napiš padající testy**

```csharp
[Fact] public async Task Uloz_ZmenaJednohoAtributu_ZalozíJedinouZmenu()
[Fact] public async Task Uloz_NesahnaNaPublikovanyStav()
[Fact] public async Task Uloz_TutezHodnotuDvakrat_Prepise_NezalozíDruhou()
[Fact] public async Task Uloz_TutezHodnotuDvakrat_ZachovaPuvodniHodnotuPred()
[Fact] public async Task Uloz_BezeZmeny_NezalozíNic()
[Fact] public async Task Uloz_NovaPolozka_ZalozíZmenuPridano()
[Fact] public async Task Uloz_Cyklus_Odmitne()
```

První test je ten hlavní. Číselník o tisíci hodnotách, změna jedné → **jeden řádek**.
Přesně to zadání požaduje.

Čtvrtý test drží pravidlo o mezikrocích: po dvou úpravách `A → B → C` musí zůstat
jediná změna `A → C`. Kdyby zůstalo `B → C`, historie by tvrdila, že před publikováním
platilo `B`, což nikdy neplatilo.

- [ ] **Krok 3: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter ZmenaZapisovacTests
```

- [ ] **Krok 4: Zapisovač**

`Ciselniky.Core/Services/Verze/ZmenaZapisovac.cs`:

```csharp
using System.Globalization;
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Ciselniky.Core.Services.Hodnoty;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Verze;

/// <summary>
/// Porovná návrh s publikovaným stavem a rozdíl zapíše jako rozpracované změny.
/// <b>Publikovaného stavu se nedotýká</b> — ten mění až publikování verze.
/// </summary>
public sealed class ZmenaZapisovac(CiselnikyDbContext db)
{
    public async Task UlozAsync(int ciselnikId, IReadOnlyList<NavrhPolozky> navrhy,
                                int kdoId, CancellationToken ct = default)
    {
        var publikovane = await db.Polozky.AsNoTracking()
            .Where(p => p.CiselnikId == ciselnikId)
            .ToDictionaryAsync(p => p.Id, ct);

        var definiceAtributu = await db.AtributDefinice.AsNoTracking()
            .Where(a => a.CiselnikId == ciselnikId).ToDictionaryAsync(a => a.Kod, ct);
        var definiceVazeb = await db.VazbaDefinice.AsNoTracking()
            .Where(v => v.CiselnikId == ciselnikId).ToDictionaryAsync(v => v.Kod, ct);

        foreach (var navrh in navrhy)
        {
            if (navrh.Id is null)
            {
                await ZapisAsync(ciselnikId, null, DruhZmeny.Polozka, OperaceZmeny.Pridano,
                                 pole: "kod", pred: null, po: navrh.Kod, kdoId, ct);
                continue;
            }

            if (!publikovane.TryGetValue(navrh.Id.Value, out var puvodni))
                throw new InvalidOperationException(
                    $"Položka {navrh.Id} v číselníku {ciselnikId} neexistuje.");

            await OverBezCykluAsync(navrh, publikovane, ct);
            await ZapisSpolecneSloupceAsync(ciselnikId, puvodni, navrh, publikovane, kdoId, ct);
            await ZapisAtributyAsync(ciselnikId, puvodni.Id, navrh, definiceAtributu, kdoId, ct);
            await ZapisVazbyAsync(ciselnikId, puvodni.Id, navrh, definiceVazeb, kdoId, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ZapisSpolecneSloupceAsync(
        int ciselnikId, Polozka puvodni, NavrhPolozky navrh,
        IReadOnlyDictionary<int, Polozka> publikovane, int kdoId, CancellationToken ct)
    {
        var puvodniRodic = puvodni.NadrazenaPolozkaId is { } r
            && publikovane.TryGetValue(r, out var rodic) ? rodic.Kod : null;

        (string Pole, string? Pred, string? Po)[] sloupce =
        [
            ("kod",          puvodni.Kod,                       navrh.Kod),
            ("nazev",        puvodni.Nazev,                     navrh.Nazev),
            ("nadrazenyKod", puvodniRodic,                      navrh.NadrazenyKod),
            ("platnostOd",   Text(puvodni.PlatnostOd),          Text(navrh.PlatnostOd)),
            ("platnostDo",   Text(puvodni.PlatnostDo),          Text(navrh.PlatnostDo)),
            ("aktivni",      Text(puvodni.Aktivni),             Text(navrh.Aktivni)),
        ];

        foreach (var (pole, pred, po) in sloupce)
            if (pred != po)
                await ZapisAsync(ciselnikId, puvodni.Id, DruhZmeny.Polozka,
                                 OperaceZmeny.Zmeneno, pole, pred, po, kdoId, ct);
    }

    private async Task ZapisAtributyAsync(
        int ciselnikId, int polozkaId, NavrhPolozky navrh,
        IReadOnlyDictionary<string, AtributDefinice> definice, int kdoId, CancellationToken ct)
    {
        var publikovane = await db.PolozkaAtributy.AsNoTracking()
            .Where(h => h.PolozkaId == polozkaId).ToListAsync(ct);

        foreach (var (kodAtributu, novaHodnota) in navrh.Atributy)
        {
            if (!definice.TryGetValue(kodAtributu, out var def))
                throw new InvalidOperationException(
                    $"Číselník nemá atribut „{kodAtributu}\".");

            var stara = Text(publikovane.FirstOrDefault(h => h.AtributDefiniceId == def.Id), def.Typ);
            if (stara == novaHodnota) continue;

            var operace = novaHodnota is null ? OperaceZmeny.Odebrano
                        : stara is null       ? OperaceZmeny.Pridano
                                              : OperaceZmeny.Zmeneno;

            await ZapisAsync(ciselnikId, polozkaId, DruhZmeny.Atribut, operace,
                             pole: null, stara, novaHodnota, kdoId, ct,
                             atributDefiniceId: def.Id);
        }
    }

    private async Task ZapisVazbyAsync(
        int ciselnikId, int polozkaId, NavrhPolozky navrh,
        IReadOnlyDictionary<string, VazbaDefinice> definice, int kdoId, CancellationToken ct)
    {
        foreach (var (kodVazby, noveCile) in navrh.Vazby)
        {
            if (!definice.TryGetValue(kodVazby, out var def))
                throw new InvalidOperationException($"Číselník nemá vazbu „{kodVazby}\".");

            var stareCile = await (
                from vazba in db.PolozkaVazby.AsNoTracking()
                where vazba.PolozkaId == polozkaId && vazba.VazbaDefiniceId == def.Id
                join cil in db.Polozky on vazba.CilPolozkaId equals cil.Id
                select cil.Kod).ToListAsync(ct);

            var pred = Spoj(stareCile);
            var po = Spoj(noveCile);
            if (pred == po) continue;

            await ZapisAsync(ciselnikId, polozkaId, DruhZmeny.Vazba, OperaceZmeny.Zmeneno,
                             pole: null, pred, po, kdoId, ct, vazbaDefiniceId: def.Id);
        }
    }

    /// <summary>
    /// Zapíše rozpracovanou změnu. Existuje-li už pro tutéž věc,
    /// <b>přepíše jí cílovou hodnotu a původní ponechá</b> — jinak by verze
    /// obsahovala mezikrok, který nikdy nikdo neviděl.
    /// </summary>
    private async Task ZapisAsync(
        int ciselnikId, int? polozkaId, DruhZmeny druh, OperaceZmeny operace,
        string? pole, string? pred, string? po, int kdoId, CancellationToken ct,
        int? atributDefiniceId = null, int? vazbaDefiniceId = null)
    {
        var existujici = await db.Zmeny.FirstOrDefaultAsync(z =>
            z.VerzeId == null && z.CiselnikId == ciselnikId && z.PolozkaId == polozkaId
            && z.Druh == druh && z.AtributDefiniceId == atributDefiniceId
            && z.VazbaDefiniceId == vazbaDefiniceId && z.Pole == pole, ct);

        if (existujici is not null)
        {
            // Vrátil-li uživatel hodnotu na původní, změna zaniká.
            if (existujici.HodnotaPred == po) db.Zmeny.Remove(existujici);
            else { existujici.HodnotaPo = po; existujici.KdoId = kdoId;
                   existujici.Kdy = DateTimeOffset.UtcNow; }
            return;
        }

        db.Zmeny.Add(new Zmena
        {
            CiselnikId = ciselnikId, PolozkaId = polozkaId, Druh = druh, Operace = operace,
            AtributDefiniceId = atributDefiniceId, VazbaDefiniceId = vazbaDefiniceId,
            Pole = pole, HodnotaPred = pred, HodnotaPo = po, KdoId = kdoId
        });
    }

    private async Task OverBezCykluAsync(NavrhPolozky navrh,
        IReadOnlyDictionary<int, Polozka> publikovane, CancellationToken ct)
    {
        if (navrh.NadrazenyKod is null || navrh.Id is null) return;

        var rodic = publikovane.Values.FirstOrDefault(p => p.Kod == navrh.NadrazenyKod);
        if (rodic is not null)
            await HierarchieKontrola.OverBezCykluAsync(db, navrh.Id.Value, rodic.Id, ct);
    }

    private static string? Text(DateOnly? d) => d?.ToString("yyyy-MM-dd");
    private static string Text(DateOnly d)   => d.ToString("yyyy-MM-dd");
    private static string Text(bool b)       => b ? "true" : "false";
    private static string Spoj(IEnumerable<string> kody) => string.Join(';', kody.Order());

    private static string? Text(PolozkaAtribut? hodnota, TypAtributu typ) => typ switch
    {
        _ when hodnota is null => null,
        TypAtributu.Cislo => hodnota.HodnotaCislo?.ToString(CultureInfo.InvariantCulture),
        TypAtributu.Datum => hodnota.HodnotaDatum?.ToString("yyyy-MM-dd"),
        TypAtributu.AnoNe => hodnota.HodnotaAnoNe is { } b ? (b ? "true" : "false") : null,
        _ => hodnota.HodnotaText,
    };
}
```

> **Vrácení hodnoty na původní změnu ruší, nezaznamenává.** Kdo omylem přepsal a opravil se,
> nemá v publikované verzi figurovat vůbec. Bez toho by se verze plnily prázdnými změnami
> `A → A`.

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter ZmenaZapisovacTests   # Passed: 7
git add -A && git commit -m "feat(verze): zápisová cesta přes rozpracované změny"
```

---

## Blok 3: Překryv rozpracovaných změn

**Cíl bloku:** Editor vidí publikovaný stav překrytý svými rozpracovanými změnami.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Verze/PrekryvRozpracovanych.cs`
- Test: `Ciselniky.Tests.Integration/Verze/PrekryvTests.cs`

**Rozhraní:**
- Poskytuje: `PrekryvRozpracovanych.NactiAsync(int ciselnikId, CancellationToken) → Task<IReadOnlyList<HodnotaPolozky>>`.

> **Bez stránkování, záměrně.** Rozpracovaně přidaná položka v tabulce publikovaného stavu
> neexistuje, takže by se počet i pořadí musely dopočítávat napříč dvěma zdroji.
> Překryv se proto počítá nad celým číselníkem naráz. Je to studená cesta — editor si
> číselník stejně načítá celý do tabulky.
>
> Horká cesta, tedy stránkované čtení publikovaného stavu z P4, zůstává nedotčená.

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Prekryv_ZmenenyAtribut_UkazeNovouHodnotu()
[Fact] public async Task Prekryv_PridanaPolozka_JeVeVysledku_ByVTabulceNeni()
[Fact] public async Task Prekryv_VyrazenaPolozka_MaAktivniFalse()
[Fact] public async Task Prekryv_BezRozpracovanych_VratiTotezCoPublikovanyStav()
[Fact] public async Task Prekryv_OznaciZmenenePolozky()
```

Druhý test je ten, kvůli kterému překryv vůbec existuje.

- [ ] **Krok 2: Nestránkované čtení publikovaného stavu**

Překryv i dopočet historické verze potřebují **celý** publikovaný stav, ne stránku.
Do `HodnotaCtenar` z P4 přibude:

```csharp
/// <summary>
/// Načte celý publikovaný stav číselníku bez stránkování. Používá ho překryv
/// rozpracovaných změn a dopočet historické verze — obojí je studená cesta.
/// Horká cesta zůstává <see cref="NactiStrankuAsync"/>.
/// </summary>
public async Task<IReadOnlyList<HodnotaPolozky>> NactiVsePublikovaneAsync(
    int ciselnikId, CancellationToken ct = default)
{
    var celkem = await db.Polozky.CountAsync(p => p.CiselnikId == ciselnikId, ct);
    var stranka = await NactiStrankuAsync(
        new DotazHodnot(ciselnikId, VcetneVyrazenych: true, Strana: 1,
                        Velikost: Math.Max(celkem, 1)), ct);
    return stranka.Polozky;
}
```

- [ ] **Krok 3: Spusť, ověř pád, doplň překryv**

Výsledkem je `HodnotaPolozky` z P4 rozšířená o příznak stavu:

```csharp
public enum StavVePrekryvu { Beze_zmeny, Pridana, Zmenena, Vyrazena }

public sealed record PolozkaSPrekryvem(HodnotaPolozky Polozka, StavVePrekryvu Stav);
```

Postup: načti publikovaný stav celého číselníku, načti rozpracované změny,
aplikuj je na kopii v paměti a označ dotčené položky.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter PrekryvTests   # Passed: 5
git add -A && git commit -m "feat(verze): překryv rozpracovaných změn nad publikovaným stavem"
```

---

## Blok 4: Publikování verze

**Cíl bloku:** Publikování vydá verzi, promítne změny do publikovaného stavu a zvýší
**správné** číslo. **Uzavírá dluh D4 z P3.**

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Verze/Publikovac.cs`
- Uprav: `Ciselniky.Core/Services/Ciselniky/CiselnikSluzba.cs` (dluh D4)
- Test: `Ciselniky.Tests.Integration/Verze/PublikovacTests.cs`

**Rozhraní:**
- Poskytuje: `Publikovac.PublikujAsync(int ciselnikId, string? poznamka, int kdoId, CancellationToken) → Task<CiselnikVerze>`.

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Publikuj_PrvniVerze_Je_1_0()
[Fact] public async Task Publikuj_ZmenaHodnot_ZvysiVedlejsiCislo()
[Fact] public async Task Publikuj_ZmenaStruktury_ZvysiHlavniANulujeVedlejsi()
[Fact] public async Task Publikuj_PromitneZmenyDoPublikovanehoStavu()
[Fact] public async Task Publikuj_PrirazdiVerziVsemRozpracovanym()
[Fact] public async Task Publikuj_BezRozpracovanych_Odmitne()
[Fact] public async Task Publikuj_PoPublikovani_JizNejdeZmenitKodCiselniku()
```

Poslední test uzavírá dluh D4 a drží rozhodnutí Z4: **kód je po publikování neměnný**,
protože je součástí stálého identifikátoru, který si konzumenti ukládají.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter PublikovacTests
```

- [ ] **Krok 3: Publikovač**

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Ciselniky.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Verze;

public sealed class Publikovac(CiselnikyDbContext db)
{
    public async Task<CiselnikVerze> PublikujAsync(
        int ciselnikId, string? poznamka, int kdoId, CancellationToken ct = default)
    {
        await using var transakce = await db.Database.BeginTransactionAsync(ct);

        var rozpracovane = await db.Zmeny
            .Where(z => z.CiselnikId == ciselnikId && z.VerzeId == null)
            .ToListAsync(ct);

        if (rozpracovane.Count == 0)
            throw new ValidacniVyjimka([new(null, null,
                "Není co publikovat — číselník nemá žádné neuložené ani rozpracované změny.")]);

        var posledni = await db.Verze
            .Where(v => v.CiselnikId == ciselnikId)
            .OrderByDescending(v => v.CisloHlavni).ThenByDescending(v => v.CisloVedlejsi)
            .FirstOrDefaultAsync(ct);

        // Struktura se mění → hlavní číslo. Konzument z něj pozná, že se ho to může dotknout.
        var meniStrukturu = rozpracovane.Any(z => z.Druh == DruhZmeny.Definice);

        var (hlavni, vedlejsi) = posledni is null
            ? (1, 0)
            : meniStrukturu
                ? (posledni.CisloHlavni + 1, 0)
                : (posledni.CisloHlavni, posledni.CisloVedlejsi + 1);

        var verze = new CiselnikVerze
        {
            CiselnikId = ciselnikId, CisloHlavni = hlavni, CisloVedlejsi = vedlejsi,
            VydalId = kdoId, Poznamka = poznamka
        };
        db.Verze.Add(verze);
        await db.SaveChangesAsync(ct);

        await PromitniAsync(rozpracovane, ct);

        foreach (var zmena in rozpracovane) zmena.VerzeId = verze.Id;
        await db.SaveChangesAsync(ct);

        await transakce.CommitAsync(ct);
        return verze;
    }

private async Task PromitniAsync(IReadOnlyList<Zmena> zmeny, CancellationToken ct)
    {
        foreach (var zmena in zmeny.OrderBy(z => z.Id))
        {
            switch (zmena.Druh)
            {
                case DruhZmeny.Polozka when zmena.Operace == OperaceZmeny.Pridano:
                    db.Polozky.Add(new Polozka
                    {
                        CiselnikId = zmena.CiselnikId,
                        Kod = zmena.HodnotaPo!,
                        Nazev = zmena.HodnotaPo!,      // název dorovná změna pole "nazev"
                        PlatnostOd = DateOnly.FromDateTime(zmena.Kdy.UtcDateTime)
                    });
                    await db.SaveChangesAsync(ct);     // potřebujeme přidělené Id
                    break;

                case DruhZmeny.Polozka:
                    NastavSpolecnySloupec(
                        await NactiPolozkuAsync(zmena.PolozkaId!.Value, ct),
                        zmena.Pole!, zmena.HodnotaPo);
                    break;

                case DruhZmeny.Atribut:
                    await NastavAtributAsync(zmena, ct);
                    break;

                case DruhZmeny.Vazba:
                    await NastavVazbyAsync(zmena, ct);
                    break;

                case DruhZmeny.Definice:
                    // Definici mění DefiniceSluzba přímo; změna je zde jen jako záznam,
                    // aby publikování poznalo, že má zvýšit hlavní číslo. Viz dluh D11.
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
```

> **Přidaná položka se zakládá s názvem rovným kódu a hned poté ji dorovná změna
> pole `nazev`.** Zapisovač z bloku 2 obě změny vždy zapisuje spolu a `OrderBy(z => z.Id)`
> zaručí pořadí. Alternativou by bylo nést celý stav nové položky v jediné změně —
> tím by ale přestal platit vzor „jedna změna = jedna hodnota", na kterém stojí
> dopočet historické verze i rozdíl verzí.


> **Celé publikování je jedna transakce.** Kdyby se přerušilo mezi promítnutím a přiřazením
> verze, zůstal by publikovaný stav změněný, ale změny by se tvářily jako rozpracované —
> a příští publikování by je promítlo znovu.

- [ ] **Krok 4: Uzavři dluh D4**

V `CiselnikSluzba` se metoda z P3 doplní:

```csharp
private Task<bool> BylPublikovanAsync(int ciselnikId, CancellationToken ct)
    => db.Verze.AnyAsync(v => v.CiselnikId == ciselnikId, ct);
```

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter PublikovacTests   # Passed: 7
grep -n "Task.FromResult(false)" Ciselniky.Core/Services/Ciselniky/CiselnikSluzba.cs   # 0 nálezů
git add -A && git commit -m "feat(verze): publikování verze s dvojkovým číslováním"
```

---

## Blok 5: Dopočet historické verze

**Cíl bloku:** Číselník se vydá tak, jak vypadal ve zvolené verzi — sestavený jako celek,
přestože v databázi leží jen rozdíly.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Verze/HistorickyStav.cs`
- Test: `Ciselniky.Tests.Integration/Verze/HistorickyStavTests.cs`

**Rozhraní:**
- Poskytuje: `HistorickyStav.KVerziAsync(int ciselnikId, int hlavni, int vedlejsi, CancellationToken) → Task<IReadOnlyList<HodnotaPolozky>>`.

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task KVerzi_AktualniVerze_VratiTotezCoPublikovanyStav()
[Fact] public async Task KVerzi_PredchoziVerze_VratiPuvodniHodnotuAtributu()
[Fact] public async Task KVerzi_PredPridanimPolozky_PolozkuNevrati()
[Fact] public async Task KVerzi_PredVyrazenim_PolozkuVratiJakoAktivni()
[Fact] public async Task KVerzi_NeexistujiciVerze_Vyhodi()
[Fact] public async Task KVerzi_IgnorujeRozpracovaneZmeny()
```

Poslední test je důležitý: **dopočet se nesmí opřít o rozpracované změny**.
Historická verze je fakt, ne rozdělaná práce.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter HistorickyStavTests
```

- [ ] **Krok 3: Dopočet**

`Ciselniky.Core/Services/Verze/HistorickyStav.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Ciselniky.Core.Services;
using Ciselniky.Core.Services.Hodnoty;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Verze;

public sealed class HistorickyStav(CiselnikyDbContext db, HodnotaCtenar ctenar)
{
    /// <summary>
    /// Sestaví číselník tak, jak vypadal v dané verzi. Vychází z publikovaného stavu
    /// a jde po verzích zpět, přičemž změny aplikuje obráceně:
    /// PRIDANO → odeber · ODEBRANO → vrať · ZMENENO → nastav hodnotu před.
    /// </summary>
    public async Task<IReadOnlyList<HodnotaPolozky>> KVerziAsync(
        int ciselnikId, int hlavni, int vedlejsi, CancellationToken ct = default)
    {
        var cil = await db.Verze.AsNoTracking().FirstOrDefaultAsync(
        v => v.CiselnikId == ciselnikId && v.CisloHlavni == hlavni
          && v.CisloVedlejsi == vedlejsi, ct)
        ?? throw new ValidacniVyjimka([new(null, "verze",
            $"Verze {hlavni}.{vedlejsi} u tohoto číselníku neexistuje.")]);

        var stav = (await ctenar.NactiVsePublikovaneAsync(ciselnikId, ct)).ToList();

        // Změny novější než cílová verze, od nejnovější. Rozpracované (VerzeId == null)
        // se záměrně nezahrnují — historická verze je fakt, ne rozdělaná práce.
        var vratitZpet = await (
        from zmena in db.Zmeny.AsNoTracking()
        where zmena.CiselnikId == ciselnikId && zmena.VerzeId != null
        join verze in db.Verze on zmena.VerzeId equals verze.Id
        where verze.CisloHlavni > hlavni
           || (verze.CisloHlavni == hlavni && verze.CisloVedlejsi > vedlejsi)
        orderby verze.CisloHlavni descending, verze.CisloVedlejsi descending,
                zmena.Id descending
        select zmena).ToListAsync(ct);

        foreach (var zmena in vratitZpet) AplikujObracene(stav, zmena);

        return stav;
    }

    /// <summary>
    /// Vrátí jednu změnu zpět. Je to zrcadlo publikování:
    /// co publikování přidalo, dopočet odebere; co odebralo, vrátí.
    /// </summary>
    private static void AplikujObracene(List<HodnotaPolozky> stav, Zmena zmena)
    {
        var polozka = stav.FirstOrDefault(p => p.Id == zmena.PolozkaId);

        switch (zmena.Druh, zmena.Operace)
        {
            case (DruhZmeny.Polozka, OperaceZmeny.Pridano):
                if (polozka is not null) stav.Remove(polozka);
                break;

            case (DruhZmeny.Polozka, OperaceZmeny.Odebrano):
                // Položka se nemaže, jen vyřazuje — návrat znamená obnovit příznak.
                if (polozka is not null) Nahrad(stav, polozka with { Aktivni = true });
                break;

            case (DruhZmeny.Polozka, OperaceZmeny.Zmeneno) when polozka is not null:
                Nahrad(stav, VratSpolecnySloupec(polozka, zmena.Pole!, zmena.HodnotaPred));
                break;

            case (DruhZmeny.Atribut, _) when polozka is not null:
                var atributy = new Dictionary<string, object?>(polozka.Atributy);
                var kodAtributu = KodAtributu(zmena.AtributDefiniceId!.Value);
                if (zmena.HodnotaPred is null) atributy.Remove(kodAtributu);
                else atributy[kodAtributu] = zmena.HodnotaPred;
                Nahrad(stav, polozka with { Atributy = atributy });
                break;

            case (DruhZmeny.Vazba, _) when polozka is not null:
                var vazby = new Dictionary<string, IReadOnlyList<OdkazNaPolozku>>(polozka.Vazby);
                var kodVazby = KodVazby(zmena.VazbaDefiniceId!.Value);
                if (string.IsNullOrEmpty(zmena.HodnotaPred)) vazby.Remove(kodVazby);
                else vazby[kodVazby] = OdkazyZKodu(zmena.HodnotaPred);
                Nahrad(stav, polozka with { Vazby = vazby });
                break;

            case (DruhZmeny.Definice, _):
                // Změna struktury se do hodnot nepromítá — mění jen popis číselníku.
                // Historická verze proto vydává hodnoty se soudobou definicí.
                // Vědomé zjednodušení: jeho důsledek je zapsaný jako dluh D13.
                break;
        }

        static void Nahrad(List<HodnotaPolozky> stav, HodnotaPolozky nova)
        {
            var index = stav.FindIndex(p => p.Id == nova.Id);
            if (index >= 0) stav[index] = nova;
        }
    }
}
```

> **Kdyby počet verzí u jednoho číselníku narostl natolik, že dopočet zpomalí**, řeší se to
> občasným uloženým otiskem každých K verzí, od kterého se pak počítá.
> **Nestaví se teď** — přidání je čistě přírůstkové a nic z tohoto kódu nemění.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter HistorickyStavTests   # Passed: 6
git add -A && git commit -m "feat(verze): dopočet historické verze zpětnou aplikací změn"
```

---

## Blok 6: Rozdíl mezi verzemi

**Cíl bloku:** Uživatel vidí, co se mezi dvěma verzemi změnilo. Wireframe: záložka
Rozdíl verzí.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Verze/RozdilVerzi.cs`
- Test: `Ciselniky.Tests.Unit/Verze/RozdilVerziTests.cs`

- [ ] **Krok 1: Tvar výsledku**

```csharp
public enum DruhRozdilu { Pridana, Zmenena, Vyrazena }

public sealed record ZmenaAtributu(string Popisek, string? Pred, string? Po);

public sealed record RozdilPolozky(
    DruhRozdilu Druh, string Kod, string Nazev,
    string VeVerzi, IReadOnlyList<ZmenaAtributu> Zmeny);

public sealed record VysledekRozdilu(
    string OdVerze, string DoVerze,
    int Pridanych, int Zmenenych, int Vyrazenych,
    IReadOnlyList<RozdilPolozky> Polozky);
```

> **Seskupeno po položkách, ne po atributech.** Uživatel se ptá „co se stalo s tímhle cílem",
> ne „kde všude se změnil stav". Wireframe to tak ukazuje a datový tvar to musí umožnit.

- [ ] **Krok 2: Napiš padající testy**

```csharp
[Fact] public void Rozdil_SeskupujeZmenyPodlePolozek()
[Fact] public void Rozdil_PouzivaPopiskyAtributu_NeJejichKody()
[Fact] public void Rozdil_VyrazeniUkazeJakoZmenuPlatnosti()
[Fact] public void Rozdil_SouhrnSedíSPoctemPolozek()
[Fact] public void Rozdil_OpacnePoradiVerzi_ProhodíSmer()
```

Třetí test drží pravdu o datech: **vyřazení se ukazuje jako změna platnosti**, protože
přesně to se stalo. Předstírat mazání by lhalo o tom, co je v databázi.

- [ ] **Krok 3: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Unit --filter RozdilVerziTests
```

- [ ] **Krok 4: Výpočet**

Rozdíl se **nepočítá porovnáním dvou dopočítaných stavů**, ale přímo ze záznamu změn
mezi verzemi. Je to levnější a hlavně přesnější: záznam ví, *co* se změnilo, kdežto
porovnání dvou stavů to musí hádat.

```csharp
public sealed class RozdilVerzi(CiselnikyDbContext db)
{
    public async Task<VysledekRozdilu> SpocitejAsync(
        int ciselnikId, (int Hlavni, int Vedlejsi) od, (int Hlavni, int Vedlejsi) doVerze,
        CancellationToken ct = default)
    {
        // Opačné pořadí se prohodí, ať uživatel nemusí hlídat, co je dřív.
        var (a, b) = Drive(od, doVerze) ? (od, doVerze) : (doVerze, od);

        var zmeny = await (
            from zmena in db.Zmeny.AsNoTracking()
            where zmena.CiselnikId == ciselnikId && zmena.VerzeId != null
            join verze in db.Verze on zmena.VerzeId equals verze.Id
            where (verze.CisloHlavni > a.Hlavni
                   || (verze.CisloHlavni == a.Hlavni && verze.CisloVedlejsi > a.Vedlejsi))
               && (verze.CisloHlavni < b.Hlavni
                   || (verze.CisloHlavni == b.Hlavni && verze.CisloVedlejsi <= b.Vedlejsi))
            select new { zmena, verze.CisloHlavni, verze.CisloVedlejsi })
            .ToListAsync(ct);

        var popisky = await NactiPopiskyAsync(ciselnikId, ct);
        var kody = await NactiKodyPolozekAsync(ciselnikId, ct);

        // Seskupeno po POLOŽKÁCH, ne po atributech. Uživatel se ptá „co se stalo
        // s tímhle cílem", ne „kde všude se změnil stav".
        var polozky = zmeny
            .Where(x => x.zmena.PolozkaId is not null)
            .GroupBy(x => x.zmena.PolozkaId!.Value)
            .Select(skupina => SestavRozdil(skupina, popisky, kody))
            .OrderBy(r => r.Kod)
            .ToList();

        return new VysledekRozdilu(
            $"{a.Hlavni}.{a.Vedlejsi}", $"{b.Hlavni}.{b.Vedlejsi}",
            polozky.Count(r => r.Druh == DruhRozdilu.Pridana),
            polozky.Count(r => r.Druh == DruhRozdilu.Zmenena),
            polozky.Count(r => r.Druh == DruhRozdilu.Vyrazena),
            polozky);
    }

    private static bool Drive((int Hlavni, int Vedlejsi) x, (int Hlavni, int Vedlejsi) y)
        => x.Hlavni < y.Hlavni || (x.Hlavni == y.Hlavni && x.Vedlejsi <= y.Vedlejsi);
}
```

> **Vyřazení se ukazuje jako změna platnosti**, protože přesně to se v datech stalo.
> Předstírat mazání by lhalo o obsahu databáze.

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Unit --filter RozdilVerziTests   # Passed: 5
git add -A && git commit -m "feat(verze): rozdíl mezi dvěma verzemi seskupený po položkách"
```

---

## Blok 7: Koncové body

**Cíl bloku:** Změny, verze a rozdíl jsou dostupné přes rozhraní.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/VerzeController.cs`
- Uprav: `Ciselniky.Api/Controllers/Vnitrni/CiselnikyController.cs`
- Test: `Ciselniky.Tests.Api/VerzeControllerTests.cs`

- [ ] **Krok 1: Koncové body**

```csharp
[HttpGet("internal/ciselniky/{kod}/polozky/rozpracovane")]
[AllowAnonymous]
public Task<IActionResult> Rozpracovane(string kod, CancellationToken ct);

[HttpGet("internal/ciselniky/{kod}/zmeny")]
[AllowAnonymous]
public Task<IActionResult> Zmeny(string kod, CancellationToken ct);

[HttpPut("internal/ciselniky/{kod}/zmeny")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.HodnotyEdit)]
public Task<IActionResult> UlozZmeny(string kod, [FromBody] NavrhPolozky[] navrhy, CancellationToken ct);

// tvar těla požadavku
public sealed record PublikacePozadavek(string? Poznamka);

[HttpPost("internal/ciselniky/{kod}/verze")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.VerzePublish)]
public Task<IActionResult> Publikuj(string kod, [FromBody] PublikacePozadavek p, CancellationToken ct);

[HttpGet("internal/ciselniky/{kod}/verze")]
[AllowAnonymous]
public Task<IActionResult> SeznamVerzi(string kod, CancellationToken ct);

[HttpGet("internal/ciselniky/{kod}/verze/{a}/rozdil/{b}")]
[AllowAnonymous]
public Task<IActionResult> Rozdil(string kod, string a, string b, CancellationToken ct);
```

Verze se v adrese uvádí plným tvarem, například `.../verze/3.12/rozdil/3.14`.

- [ ] **Krok 2: Testy**

```csharp
[Fact] public async Task UlozZmeny_BezPrava_Odmitne()
[Fact] public async Task UlozZmeny_SPravemNaJinyCiselnik_Odmitne()
[Fact] public async Task Publikuj_BezRozpracovanych_Vrati422()
[Fact] public async Task Publikuj_VratiCisloNoveVerze()
[Fact] public async Task Rozdil_NeznamaVerze_Vrati422()
[Fact] public async Task Zmeny_JsouDostupneBezPrihlaseni()
```

- [ ] **Krok 3: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter VerzeControllerTests   # Passed: 6
git add -A && git commit -m "feat(api): koncové body pro změny, verze a rozdíl"
```

---

## Blok 8: Záložky Verze a Rozdíl verzí

**Cíl bloku:** Obrazovky podle wireframu O2.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/detail/ZalozkaVerze.tsx`, `ZalozkaRozdil.tsx`
- Test: `ciselniky-web/src/stranky/detail/ZalozkaVerze.test.tsx`

- [ ] **Krok 1: Záložka Verze**

Sloupce: verze, vydáno, vydal, změn, poznámka.

**Vodorovná čára odděluje změny hlavního čísla** — tam se měnila struktura a konzumující
aplikace se toho mohly dotknout. Je to jediná informace, kterou v tomhle seznamu někdo
opravdu hledá:

```tsx
{verze.map((v, i) => (
  <Fragment key={v.cislo}>
    {i > 0 && v.cisloHlavni !== verze[i - 1].cisloHlavni && (
      <tr className="verze-predel"><td colSpan={5} /></tr>
    )}
    <tr>
      <td>{v.cislo}</td>
      <td>{formatDatum(v.vydanaKdy)}</td>
      <td>{v.vydal}</td>
      <td>{v.pocetZmen}</td>
      <td>{v.poznamka ?? ''}</td>
    </tr>
  </Fragment>
))}
```

- [ ] **Krok 2: Záložka Rozdíl verzí**

Dva výběry verzí, souhrn, pak změny seskupené po položkách se znaménky `+ ~ −`.

**Znaménko je vedle barvy, ne místo ní.** Vytištěný rozdíl verzí je načerno a barva
v něm nic neřekne.

- [ ] **Krok 3: Upozornění na rozpracované změny v hlavičce detailu**

Má-li číselník rozpracované změny, hlavička to říká — a nabídne zobrazení s nimi:

```tsx
{ciselnik.maRozpracovaneZmeny && (
  <gov-message color="warning">
    Číselník obsahuje nepublikované změny. Konzumující aplikace zatím vidí verzi {ciselnik.verze}.
  </gov-message>
)}
```

- [ ] **Krok 4: Testy**

```tsx
test('předěl se vykreslí jen mezi různými hlavními čísly')
test('rozdíl seskupuje změny pod položku, ne pod atribut')
test('vyřazená položka se ukáže jako změna platnosti')
```

- [ ] **Krok 5: Plná sada a commit**

```bash
cd ciselniky-web && npx vitest run && cd ..
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(web): záložky Verze a Rozdíl verzí"
```

---

## Po dokončení P5 ručně ověř

1. Uprav u cíle `C-2026-001` atribut `stav` z `A` na `B` a **ulož**.
   V databázi je **jeden** řádek v `zmena` s nepřiřazenou verzí.
2. Publikovaný stav se **nezměnil** — `polozka_atribut` má pořád `A`.
3. Uprav týž atribut na `C` a ulož znovu. V `zmena` je pořád **jeden** řádek,
   `hodnota_pred` je `A` a `hodnota_po` je `C`.
4. Vrať atribut zpět na `A` a ulož. Řádek v `zmena` **zmizel**.
5. Uprav znovu na `B` a **publikuj**. Vznikla verze **1.1**, publikovaný stav má `B`.
6. Na záložce **Verze** jsou dvě verze, mezi 1.0 a 1.1 **není** vodorovný předěl.
7. Přidej do číselníku atribut a publikuj → vznikne **2.0** a předěl se objeví.
8. Na záložce **Rozdíl verzí** porovnej 1.0 a 1.1 — vidíš jeden cíl s jednou změnou.
9. Vyžádej si verzi 1.0 — atribut je zase `A`.
10. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D10 | Publikování zámek neuvolňuje — zámek zavádí až P6. Doplnit do `Publikovac`. | **P6** |
| D11 | Změny druhu `DEFINICE` zatím nikdo nezapisuje; `DefiniceSluzba` z P3 mění strukturu přímo. Napojit ji na záznam změn, aby změna struktury zvedla hlavní číslo. | **P6** |
| D12 | Uložený otisk každých K verzí se nestaví. Až dopočet historické verze zpomalí, přidat — je to přírůstková změna. | *otevřeno* |
| D13 | Dopočet historické verze vydává hodnoty se **soudobou** definicí struktury, ne s tou, která platila tehdy. U číselníku, kde atribut mezitím přibyl, se v historické verzi objeví prázdný. Rozhodnout, zda je to přijatelné, nebo verzovat i definici obsahově. | *otevřeno* |
