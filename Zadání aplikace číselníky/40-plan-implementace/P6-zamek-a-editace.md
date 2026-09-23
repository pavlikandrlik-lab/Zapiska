# P6 — Zámek a hromadná editace: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Editor upravuje hodnoty v tabulce. Jeden číselník upravuje v jeden okamžik
nejvýše jeden člověk. Změny se ukládají jako rozpracované a publikují se vědomým kliknutím.

**Architektura:** Zámek je řádek s primárním klíčem na číselníku — vynucuje ho **databáze**,
ne aplikace. Patří **osobě**, ne oknu prohlížeče. Editační mřížka si číselník načte celý
a pracuje nad překryvem z P5.

**Stack:** .NET 10, EF Core + SQL Server, React + TypeScript, xUnit, Playwright.

**Specifikace:** [../10-specifikace/08-zamek-editace.md](../10-specifikace/08-zamek-editace.md) ·
**Wireframy:** [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) (O5) ·
**Průchod:** [../10-specifikace/12-prochazeni.md](../10-specifikace/12-prochazeni.md) (W3, W6)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P5 — `ZmenaZapisovac`, `PrekryvRozpracovanych`, `Publikovac`.
**Uzavírá dluhy D10 a D11 z P5.**

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Databáze drží zámek; dva zámky na jeden číselník fyzicky nemohou vzniknout |
| 2 | Zámek jde získat, prodloužit, uvolnit a správce ho umí odebrat silou |
| 3 | Uložení i publikování zámek ověřují; publikování ho uvolní — uzavírá D10 |
| 4 | Změna struktury vydává vlastní verzi — uzavírá D11 |
| 5 | Koncové body zámku a editace |
| 6 | Editační mřížka: přilepená hlavička a spodní lišta, značky změn |
| 7 | Editace buněk, výběr cíle vazby, přidání a vyřazení hodnoty |
| 8 | Seznam změn a hláška o cizím zámku |

---

## Blok 1: Schéma zámku

**Cíl bloku:** Dva zámky na jeden číselník **fyzicky nemohou vzniknout**.

**Soubory:**
- Vytvoř: `db/db_upgrade_0_6_zamek.sql`
- Vytvoř: `Ciselniky.Core/Domain/ZamekCiselniku.cs`
- Uprav: `Data/CiselnikyDbContext.cs`, `Data/KontrolaSchematu.cs`
- Test: `Ciselniky.Tests.Integration/Data/ZamekSchemaTests.cs`

- [ ] **Krok 1: Migrační skript**

`db/db_upgrade_0_6_zamek.sql`:

```sql
-- Výhradní zámek na editaci číselníku.
-- Primární klíč na ciselnik_id je to, co pravidlo skutečně drží:
-- kdyby zámek hlídala jen aplikace, dva souběžné požadavky by ho obešly.

CREATE TABLE zamek_ciselniku (
    ciselnik_id            int     PRIMARY KEY REFERENCES ciselniky(id),
    osoba_id               int     NOT NULL REFERENCES osoby(id),
    ziskan_kdy             datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
    posledni_aktivita_kdy  datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
    platnost_do            datetimeoffset NOT NULL
);

CREATE INDEX ix_zamek_platnost ON zamek_ciselniku (platnost_do);

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.6');
```

> **`platnost_do` je uložený sloupec, ne dopočet z `posledni_aktivita_kdy`.**
> Podmínka „zámek už vypršel" se tak vyhodnocuje porovnáním dvou hodnot a jde přes index —
> a hlavně jde délka platnosti změnit v nastavení, aniž by se přepsala data.

- [ ] **Krok 2: Napiš padající test**

```csharp
[Fact] public async Task Zamek_DvaNaJedenCiselnik_Odmitne()   // 2627 na primárním klíči
```

- [ ] **Krok 3: Spusť, ověř pád, doplň entitu a kontrolu schématu, commitni**

```csharp
namespace Ciselniky.Core.Domain;

public sealed class ZamekCiselniku
{
    public int CiselnikId { get; set; }
    public int OsobaId { get; set; }
    public DateTimeOffset ZiskanKdy { get; set; }
    public DateTimeOffset PosledniAktivitaKdy { get; set; }
    public DateTimeOffset PlatnostDo { get; set; }
}
```

V `CiselnikyDbContext`:

```csharp
public DbSet<ZamekCiselniku> Zamky => Set<ZamekCiselniku>();

// v OnModelCreating
model.Entity<ZamekCiselniku>().ToTable("zamek_ciselniku").HasKey(z => z.CiselnikId);
```

```bash
dotnet test Ciselniky.Tests.Integration --filter ZamekSchemaTests
git add -A && git commit -m "feat(zamek): schéma výhradního zámku na číselník"
```

---

## Blok 2: Služba zámku

**Cíl bloku:** Zámek jde získat, prodloužit a uvolnit. **Načtení stránky znovu nikoho
neblokuje proti jeho vlastní práci.**

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Zamek/ZamekSluzba.cs`
- Test: `Ciselniky.Tests.Integration/Zamek/ZamekSluzbaTests.cs`

**Rozhraní:**
- Poskytuje: `ZamekSluzba.ZiskejAsync`, `.ProdluzAsync`, `.UvolniAsync`,
  `.OdeberSiluAsync`, `.StavAsync`.

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Ziskej_VolnyCiselnik_Uspeje()
[Fact] public async Task Ziskej_CiziPlatnyZamek_Neuspeje_AVratiDrzitele()
[Fact] public async Task Ziskej_VlastniZamek_Uspeje_ProtozeZamekPatriOsobe()
[Fact] public async Task Ziskej_CiziVyprselyZamek_Prevezme()
[Fact] public async Task Prodluz_PosuneKonecPlatnosti()
[Fact] public async Task Prodluz_CiziZamek_Neuspeje()
[Fact] public async Task Uvolni_ZamekZmizí()
[Fact] public async Task OdeberSilu_OdebereICiziPlatnyZamek()
```

Třetí test je ten, kvůli kterému návrh vypadá, jak vypadá:

> **Zámek patří osobě, ne oknu prohlížeče.** Načtení stránky znovu, druhá záložka,
> pád prohlížeče i opětovné přihlášení — pokaždé je to týž člověk, takže si zámek
> prostě bere zpátky. Kdyby byl vázaný na okno nebo na sezení, každý z těch případů
> by uživatele zablokoval proti jeho vlastní práci.

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter ZamekSluzbaTests
```

- [ ] **Krok 3: Služba**

`Ciselniky.Core/Services/Zamek/ZamekSluzba.cs`:

```csharp
using Ciselniky.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Zamek;

public sealed record StavZamku(bool Drzim, int? DrziOsobaId, string? DrziJmeno,
                               DateTimeOffset? Od, DateTimeOffset? PlatiDo);

public sealed class ZamekSluzba(CiselnikyDbContext db, IOptions<CiselnikyNastaveni> nastaveni)
{
    /// <summary>
    /// Získá zámek. SQL Server nemá podmíněné vložení jedním příkazem, proto se
    /// atomicita drží <b>zámkem rozsahu uvnitř transakce</b>: <c>HOLDLOCK</c> na
    /// neexistujícím řádku zabrání druhému požadavku vložit řádek mezi kontrolou
    /// a vložením. Bez něj by zámek na týž číselník mohli dostat dva lidé.
    /// </summary>
    public async Task<bool> ZiskejAsync(int ciselnikId, int osobaId,
                                        CancellationToken ct = default)
    {
        var minut = (int)nastaveni.Value.PlatnostZamku.TotalMinutes;

        var ovlivneno = await db.Database.ExecuteSqlInterpolatedAsync($"""
            BEGIN TRANSACTION;

            UPDATE zamek_ciselniku WITH (UPDLOCK, HOLDLOCK)
               SET osoba_id = {osobaId}, ziskan_kdy = SYSUTCDATETIME(),
                   posledni_aktivita_kdy = SYSUTCDATETIME(),
                   platnost_do = DATEADD(MINUTE, {minut}, SYSUTCDATETIME())
             WHERE ciselnik_id = {ciselnikId}
               AND (osoba_id = {osobaId} OR platnost_do < SYSUTCDATETIME());

            IF @@ROWCOUNT = 0
               AND NOT EXISTS (SELECT 1 FROM zamek_ciselniku WITH (UPDLOCK, HOLDLOCK)
                                WHERE ciselnik_id = {ciselnikId})
            BEGIN
                INSERT INTO zamek_ciselniku
                    (ciselnik_id, osoba_id, ziskan_kdy, posledni_aktivita_kdy, platnost_do)
                VALUES ({ciselnikId}, {osobaId}, SYSUTCDATETIME(), SYSUTCDATETIME(),
                        DATEADD(MINUTE, {minut}, SYSUTCDATETIME()));
            END

            COMMIT TRANSACTION;
            """, ct);

        return ovlivneno > 0;
    }

    /// <summary>
    /// Prodlouží zámek. Volá se <b>jen při skutečné aktivitě</b> uživatele, ne tikotem
    /// časovače — otevřená a zapomenutá záložka zámek prodlužovat nesmí.
    /// </summary>
    public async Task<bool> ProdluzAsync(int ciselnikId, int osobaId,
                                         CancellationToken ct = default)
    {
        var minut = (int)nastaveni.Value.PlatnostZamku.TotalMinutes;
        return await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE zamek_ciselniku
               SET posledni_aktivita_kdy = SYSUTCDATETIME(),
                   platnost_do = DATEADD(MINUTE, {minut}, SYSUTCDATETIME())
             WHERE ciselnik_id = {ciselnikId} AND osoba_id = {osobaId}
               AND platnost_do >= SYSUTCDATETIME()
            """, ct) > 0;
    }

    public async Task UvolniAsync(int ciselnikId, int osobaId, CancellationToken ct = default)
        => await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM zamek_ciselniku
             WHERE ciselnik_id = {ciselnikId} AND osoba_id = {osobaId}
            """, ct);

    /// <summary>Odebere i cizí platný zámek. Vyžaduje oprávnění <c>zamek.odebrat</c>.</summary>
    public async Task OdeberSiluAsync(int ciselnikId, CancellationToken ct = default)
        => await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM zamek_ciselniku WHERE ciselnik_id = {ciselnikId}
            """, ct);

    public async Task<StavZamku> StavAsync(int ciselnikId, int? osobaId,
                                           CancellationToken ct = default)
    {
        var zamek = await (
            from z in db.Zamky.AsNoTracking()
            where z.CiselnikId == ciselnikId && z.PlatnostDo >= DateTimeOffset.UtcNow
            join o in db.Osoby on z.OsobaId equals o.Id
            select new { z.OsobaId, o.Jmeno, o.Prijmeni, z.ZiskanKdy, z.PlatnostDo })
            .FirstOrDefaultAsync(ct);

        return zamek is null
            ? new StavZamku(false, null, null, null, null)
            : new StavZamku(zamek.OsobaId == osobaId, zamek.OsobaId,
                            $"{zamek.Jmeno} {zamek.Prijmeni}", zamek.ZiskanKdy, zamek.PlatnostDo);
    }
}
```

> **Vypršelý zámek se nemaže úklidovou úlohou.** Přebírá ho ten, kdo o něj příště požádá.
> Úklidová úloha by byla další pohyblivá součást, kterou by někdo musel hlídat, a řešila
> by problém, který nemáme: pár set osiřelých řádků nikomu nevadí.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter ZamekSluzbaTests   # Passed: 8
git add -A && git commit -m "feat(zamek): služba získání, prodloužení a uvolnění zámku"
```

---

## Blok 3: Zámek při ukládání a publikování

**Cíl bloku:** Zámek se ověřuje **i při zápisu**, ne jen při otevření editace.
Publikování ho uvolní. **Uzavírá dluh D10 z P5.**

**Soubory:**
- Uprav: `Ciselniky.Core/Services/Verze/ZmenaZapisovac.cs`, `Publikovac.cs`
- Vytvoř: `Ciselniky.Core/Services/Zamek/ZamekVyjimka.cs`
- Test: `Ciselniky.Tests.Integration/Zamek/ZamekPriZapisuTests.cs`

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task Uloz_BezZamku_Odmitne()
[Fact] public async Task Uloz_SCizimZamkem_Odmitne_AZmenyZustanouUlozene()
[Fact] public async Task Publikuj_BezZamku_Odmitne()
[Fact] public async Task Publikuj_UvolniZamek()
```

Druhý test drží slib z návrhu: kdo o zámek mezitím přišel, dostane odmítnutí
**s ujištěním, že jeho dříve uložené změny nezmizely**. Bez toho by uživatel
předpokládal nejhorší a začal práci znovu.

- [ ] **Krok 2: Spusť, ověř pád, doplň kontrolu**

```csharp
namespace Ciselniky.Core.Services.Zamek;

/// <summary>
/// Volající nedrží zámek. Nese jméno držitele, aby uživatel věděl, s kým se domluvit,
/// a ujištění o osudu dříve uložených změn.
/// </summary>
public sealed class ZamekVyjimka(string? drzitel)
    : Exception(drzitel is null
        ? "Nemáte otevřenou editaci tohoto číselníku. Otevřete ji znovu — "
        + "vaše dříve uložené změny zůstávají uložené."
        : $"Číselník mezitím převzal {drzitel}. Vaše dříve uložené změny "
        + "zůstávají uložené, ale teď do nich nemůžete zapisovat.")
{
    public string? Drzitel { get; } = drzitel;
}
```

Obě služby dostanou novou závislost — konstruktory z P5 se rozšíří:

```csharp
public sealed class ZmenaZapisovac(CiselnikyDbContext db, ZamekSluzba zamek)
public sealed class Publikovac(CiselnikyDbContext db, ZamekSluzba zamek)
```

V `ZmenaZapisovac.UlozAsync` a `Publikovac.PublikujAsync` se na začátku volá:

```csharp
var stav = await zamek.StavAsync(ciselnikId, kdoId, ct);
if (!stav.Drzim) throw new ZamekVyjimka(stav.DrziJmeno);
```

Na konci `PublikujAsync`, uvnitř téže transakce:

```csharp
await zamek.UvolniAsync(ciselnikId, kdoId, ct);   // dluh D10
```

- [ ] **Krok 3: Převod výjimky na odpověď**

`ZamekVyjimka` se mapuje na **409**, ne na 403. Není to chyba oprávnění —
uživatel právo má, jen ho v tuhle chvíli drží někdo jiný.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter ZamekPriZapisuTests   # Passed: 4
git add -A && git commit -m "feat(zamek): ověření zámku při zápisu a jeho uvolnění po publikování"
```

---

## Blok 4: Změna struktury vydává vlastní verzi

**Cíl bloku:** Změna struktury zvedne hlavní číslo verze. **Uzavírá dluh D11 z P5.**

**Soubory:**
- Uprav: `Ciselniky.Core/Services/Ciselniky/DefiniceSluzba.cs`
- Test: `Ciselniky.Tests.Integration/Ciselniky/StrukturaVerzeTests.cs`

- [ ] **Krok 1: Napiš padající testy**

```csharp
[Fact] public async Task ZmenaStruktury_VydaVerziSHlavnimCislemVyssim()
[Fact] public async Task ZmenaStruktury_PriNepublikovanychHodnotach_Odmitne()
[Fact] public async Task ZmenaStruktury_VydanaVerzeObsahujeJenZmenyDefinice()
[Fact] public async Task ZmenaStruktury_PrvniZmena_VydaVerzi2_0()
[Fact] public async Task ZmenaStruktury_PredPrvniVerzi_NevydaZadnouVerzi()
[Fact] public async Task ZmenaStruktury_PredPrvniVerzi_NevadiNepublikovanymHodnotam()
```

- [ ] **Krok 2: Spusť a ověř pád**

```bash
dotnet test Ciselniky.Tests.Integration --filter StrukturaVerzeTests
```

- [ ] **Krok 3: Doplň chování**

> **Struktura nemá rozpracovaný stav.** Kdyby se definice měnila hned a verze se zvýšila
> až příštím publikováním, viděli by konzumenti mezitím **novou strukturu se starým
> číslem verze**. Číslo verze je přitom jediné, podle čeho konzument pozná, že se ho změna
> může dotknout — nesmí lhát ani na chvíli.
>
> Proto změna struktury **vydává verzi okamžitě** a je **zakázaná, dokud má číselník
> nepublikované změny hodnot**. Vydaná verze pak obsahuje jen změny druhu `DEFINICE`
> a nic jiného.
>
> **Výjimka: dokud číselník nemá první verzi, pravidlo neplatí.** Tehdy se teprve skládá,
> přes rozhraní neexistuje a nemá tedy koho chránit. Správce může strukturu i hodnoty
> upravovat volně a **první publikování vydá verzi 1.0 se vším najednou** — tak, jak to
> popisuje průchod W5. Pravidlo se zapíná až po ní.

`DefiniceSluzba` z P3 dostane dvě nové závislosti:

```csharp
public sealed class DefiniceSluzba(CiselnikyDbContext db, Publikovac publikovac)
```

a každou měnící metodu obalí tentýž postup:

```csharp
/// <summary>
/// Vydá verzi změny struktury. Vrací <c>null</c>, byl-li číselník dosud nepublikovaný —
/// tehdy se jen změní definice a verze vznikne až prvním publikováním.
/// </summary>
private async Task<CiselnikVerze?> VydejVerziZmenyStrukturyAsync(
    int ciselnikId, int kdoId, Func<Task> zmena, CancellationToken ct)
{
    // Před první verzí se číselník teprve skládá — pravidlo se nepoužije.
    var jizPublikovan = await db.Verze.AnyAsync(v => v.CiselnikId == ciselnikId, ct);
    if (!jizPublikovan)
    {
        await zmena();                     // změna definice, žádná verze
        await db.SaveChangesAsync(ct);
        return null;                       // verze vznikne až prvním publikováním
    }

    var nepublikovaneHodnoty = await db.Zmeny.AnyAsync(
        z => z.CiselnikId == ciselnikId && z.VerzeId == null && z.Druh != DruhZmeny.Definice, ct);

    if (nepublikovaneHodnoty)
        throw new ValidacniVyjimka([new(null, null,
            "Číselník má nepublikované změny hodnot. Nejdřív je publikujte, "
            + "teprve potom měňte strukturu.")]);

    await using var transakce = await db.Database.BeginTransactionAsync(ct);

    await zmena();                                     // vlastní úprava definice

    db.Zmeny.Add(new Zmena
    {
        CiselnikId = ciselnikId, Druh = DruhZmeny.Definice,
        Operace = OperaceZmeny.Zmeneno, KdoId = kdoId,
        HodnotaPo = "struktura změněna"
    });
    await db.SaveChangesAsync(ct);

    var verze = await publikovac.PublikujAsync(ciselnikId, "Změna struktury", kdoId, ct);
    await transakce.CommitAsync(ct);
    return verze;
}
```

- [ ] **Krok 4: Matice povolených změn struktury**

Změna struktury **nesmí tiše znehodnotit data**. Když ji nejde provést beze ztráty,
odmítne se a **vyjmenuje, co jí brání** — nikdy nevrátí pouhé „nelze".

Úplná matice je v
[../10-specifikace/13-zivotni-cyklus-struktury.md](../10-specifikace/13-zivotni-cyklus-struktury.md).
Kontroly, které z ní plynou:

```csharp
private async Task OverZmenuStrukturyAsync(
    int ciselnikId, AtributDefinice pred, AtributPozadavek po, CancellationToken ct)
{
    var chyby = new List<ChybaOvereni>();

    // Kód je v popisu struktury a konzumenti podle něj čtou.
    if (po.Kod != pred.Kod && await BylPublikovanAsync(ciselnikId, ct))
        chyby.Add(new(null, "kod",
            $"Kód atributu už nejde změnit — číselník byl publikován a konzumenti "
            + $"podle „{pred.Kod}\" čtou data. Přidejte nový atribut a starý vyřaďte."));

    // Povinnost jde zavést, jen když ji všechny položky splňují.
    if (po.Povinny && !pred.Povinny)
    {
        var bezHodnoty = await NajdiPolozkyBezHodnotyAsync(ciselnikId, pred.Id, ct);
        if (bezHodnoty.Count > 0)
            chyby.Add(new(null, "povinny",
                $"Atribut nejde označit za povinný — chybí u {bezHodnoty.Count} položek: "
                + string.Join(", ", bezHodnoty.Take(10))
                + (bezHodnoty.Count > 10 ? " a dalších." : ".")
                + " Doplňte hodnoty a publikujte je, teprve potom povinnost zaveďte."));
    }

    // Typ jde změnit, jen když jsou všechny hodnoty převeditelné.
    if (po.Typ != pred.Typ)
    {
        var neprevoditelne = await NajdiNeprevoditelneAsync(ciselnikId, pred, po.Typ, ct);
        if (neprevoditelne.Count > 0)
            chyby.Add(new(null, "typ",
                $"Typ nejde změnit na {po.Typ} — u {neprevoditelne.Count} položek "
                + "hodnotu převést nelze: " + string.Join(", ", neprevoditelne.Take(10)) + "."));
    }

    // Výčet jde zúžit, jen když odebíranou hodnotu nikdo nepoužívá.
    var odebrane = pred.Vycet.Except(po.Vycet).ToArray();
    if (odebrane.Length > 0)
    {
        var pouzivane = await NajdiPouzivaneHodnotyAsync(ciselnikId, pred.Id, odebrane, ct);
        if (pouzivane.Count > 0)
            chyby.Add(new(null, "vycet",
                "Z výčtu nejde odebrat hodnotu, kterou položky používají: "
                + string.Join(", ", pouzivane) + "."));
    }

    if (chyby.Count > 0) throw new ValidacniVyjimka(chyby);
}
```

Přidání **povinného** atributu k číselníku s hodnotami se odmítá vždy — a hláška
**nabídne náhradní postup**, ne jen zákaz:

```
Povinný atribut nejde přidat k číselníku, který už má hodnoty.
Přidejte ho jako nepovinný, doplňte hodnoty u všech položek a teprve
potom ho povyšte na povinný.
```

Vypnutí hierarchie se odmítne, má-li některá položka nadřazenou — s jejich výčtem.

- [ ] **Krok 5: Testy matice**

```csharp
[Fact] public async Task Zmena_PridatNepovinnyAtribut_KCiselnikuSHodnotami_Projde()
[Fact] public async Task Zmena_PridatPovinnyAtribut_KCiselnikuSHodnotami_OdmitneANabidnePostup()
[Fact] public async Task Zmena_PovysitNaPovinny_KdyzVsechnyMajiHodnotu_Projde()
[Fact] public async Task Zmena_PovysitNaPovinny_KdyzNekteryChybi_VyjmenujePolozky()
[Fact] public async Task Zmena_KoduAtributu_PoPublikovani_Odmitne()
[Fact] public async Task Zmena_TypuTextNaCislo_KdyzVseCisla_Projde()
[Fact] public async Task Zmena_TypuTextNaCislo_KdyzNecoNeni_VyjmenujePolozky()
[Fact] public async Task Zmena_ZuzitVycet_KdyzHodnotaPouzivana_Odmitne()
[Fact] public async Task Zmena_ZapnoutHierarchii_KCiselnikuSHodnotami_Projde()
[Fact] public async Task Zmena_VypnoutHierarchii_KdyzNekdoMaRodice_Odmitne()
```

Čtvrtý a sedmý test drží vedoucí pravidlo: odmítnutí **jmenuje konkrétní položky**.
Hláška „nelze" bez výčtu nutí uživatele hledat naslepo v tisícovce řádků.

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter StrukturaVerzeTests   # Passed: 4
dotnet test Ciselniky.Tests.Integration --filter MaticeZmenTests       # Passed: 10
git add -A && git commit -m "feat(verze): změna struktury vydává verzi a nesmí znehodnotit data"
```

---

## Blok 5: Koncové body zámku a editace

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/ZamekController.cs`
- Test: `Ciselniky.Tests.Api/ZamekControllerTests.cs`

- [ ] **Krok 1: Koncové body**

```csharp
[HttpPost("internal/ciselniky/{kod}/zamek")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.HodnotyEdit)]
public Task<IActionResult> Ziskej(string kod, CancellationToken ct);

[HttpPost("internal/ciselniky/{kod}/zamek/prodlouzit")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.HodnotyEdit)]
public Task<IActionResult> Prodluz(string kod, CancellationToken ct);

[HttpDelete("internal/ciselniky/{kod}/zamek")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.HodnotyEdit)]
public Task<IActionResult> Uvolni(string kod, CancellationToken ct);

[HttpDelete("internal/ciselniky/{kod}/zamek/vynutit")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.ZamekOdebrat)]
public Task<IActionResult> OdeberSilu(string kod, CancellationToken ct);
```

Neúspěšné získání vrací **409** a v těle jméno držitele i čas, od kdy drží.

- [ ] **Krok 2: Testy**

```csharp
[Fact] public async Task Ziskej_BezPravaEditovat_Odmitne()
[Fact] public async Task Ziskej_ObsazenyCiselnik_Vrati409SeJmenem()
[Fact] public async Task Prodluz_BezZamku_Vrati409()
[Fact] public async Task OdeberSilu_BezPrava_Odmitne()
```

- [ ] **Krok 3: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter ZamekControllerTests   # Passed: 4
git add -A && git commit -m "feat(api): koncové body zámku"
```

---

## Blok 6: Editační mřížka — kostra

**Cíl bloku:** Tabulka podle wireframu O5 s **přilepenou hlavičkou i spodní lištou**.

**Soubory:**
- Vytvoř: `ciselniky-web/src/stranky/EditaceHodnot.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/MrizkaHodnot.tsx`, `SpodniLista.tsx`
- Vytvoř: `ciselniky-web/src/lib/zamek.ts`
- Test: `ciselniky-web/src/komponenty/MrizkaHodnot.test.tsx`

- [ ] **Krok 1: Získání zámku při vstupu**

```tsx
export function EditaceHodnot({ kod }: { kod: string }) {
  const [zamek, setZamek] = useState<StavZamku | 'nacita'>('nacita')

  useEffect(() => {
    let zruseno = false
    void (async () => {
      const odpoved = await fetch(`/internal/ciselniky/${kod}/zamek`, { method: 'POST' })
      if (zruseno) return
      setZamek(await odpoved.json())      // 200 → drzim:true, 409 → jméno držitele
    })()
    return () => { zruseno = true }
  }, [kod])

  useEffect(() => (zamek !== 'nacita' && zamek.drzim ? hlidejZamek(kod) : undefined),
            [kod, zamek])

  if (zamek === 'nacita') return <pm-loading />
  if (!zamek.drzim) return <PruhZamku stav={zamek} kod={kod} />   // režim čtení, blok 8

  return <MrizkaHodnot kod={kod} />
}
```

Nedostane-li obrazovka zámek, **editace se neotevře** — zobrazí se obsah v režimu čtení
a hláška z bloku 8. Uvolnění při odchodu z obrazovky obstará úklidová funkce
`hlidejZamek`, spolu s odhlášením posluchačů činnosti.

- [ ] **Krok 2: Prodlužování při skutečné aktivitě**

`ciselniky-web/src/lib/zamek.ts`:

```ts
/**
 * Prodlužuje zámek při skutečné činnosti uživatele — pohybu myší, kliknutí, psaní.
 * NE tikotem časovače: otevřená a zapomenutá záložka zámek prodlužovat nesmí.
 * Požadavek se posílá nejvýš jednou za minutu, ať se server nezahltí.
 */
export function hlidejZamek(kod: string): () => void {
  const INTERVAL = 60_000
  let posledni = 0

  const priCinnosti = () => {
    const ted = Date.now()
    if (ted - posledni < INTERVAL) return
    posledni = ted
    void fetch(`/internal/ciselniky/${kod}/zamek/prodlouzit`, { method: 'POST' })
  }

  const udalosti = ['mousemove', 'mousedown', 'keydown'] as const
  udalosti.forEach((u) => document.addEventListener(u, priCinnosti, { passive: true }))

  return () => udalosti.forEach((u) => document.removeEventListener(u, priCinnosti))
}
```

- [ ] **Krok 3: Přilepená hlavička a spodní lišta**

U tisícovky řádků musí být pořád vidět, co je který sloupec a kolik změn čeká:

```css
.mrizka thead th   { position: sticky; top: 0;    z-index: 2; background: var(--pm-surface); }
.editace-lista     { position: sticky; bottom: 0; z-index: 3; background: var(--pm-surface); }
```

> Nosné rozvržení stojí na letitých základech — `position: sticky` je bezpečná volba
> pro cílový prohlížeč. Novější vlastnosti stylů se používají jen jako nepovinné vylepšení.

- [ ] **Krok 4: Kolik řádků se vykresluje najednou**

Číselník o tisíci hodnotách a osmi sloupcích znamená **osm tisíc buněk**, a v editaci
je většina z nich formulářový prvek. To je na jednu stránku hodně — projeví se to
při načtení a při rolování, nejvíc na cílových kancelářských sestavách.

**Postup:** nejdřív změř, pak řeš.

```tsx
// Blok 5 doplní test, který drží mez:
test('mřížka o 1000 řádcích se vykreslí do 1,5 s', …)
```

Překročí-li se mez, řešením je **vykreslovat jen viditelné okno řádků** a zbytek držet
jako prázdné místo odpovídající výšky. Editační stav zůstává v paměti pro **všechny**
řádky — okénkuje se jen vykreslení, nikdy data. Kdyby se okénkovala data, ztratila by se
rozdělaná změna na řádku, který uživatel odroloval z dohledu.

> **Nestaví se dopředu.** Okénkování komplikuje přilepenou hlavičku, hledání v prohlížeči
> i tisk. Je to čistě přírůstková změna vykreslovací vrstvy — proto se přidá, teprve
> až měření ukáže, že je potřeba. Zapsáno jako dluh D21.

- [ ] **Krok 5: Sloupce z definice**

Stejně jako v P4: kód, název, atributy podle pořadí, vazby, platnost.
Vlevo úzký **sloupec značek** `+ ~ −`.

**U hierarchického číselníku přibývá sloupec `Nadřazená`** hned za názvem
(wireframe O5). U plochého se nezobrazuje — nemá co nabízet.

```tsx
const sloupce = [
  { klic: 'znacka',  popisek: ''      },
  { klic: 'kod',     popisek: 'Kód'   },
  { klic: 'nazev',   popisek: 'Název' },
  ...(ciselnik.hierarchicky
      ? [{ klic: 'nadrazena', popisek: 'Nadřazená' }]
      : []),
  ...definice.atributy.map((a) => ({ klic: `atribut:${a.kod}`, popisek: a.nazev })),
  ...definice.vazby.map((v)   => ({ klic: `vazba:${v.kod}`,   popisek: v.nazev   })),
  { klic: 'platnost', popisek: 'Platnost' },
]
```

- [ ] **Krok 6: Testy a commit**

```tsx
test('hlavička zůstává přilepená při odrolování')
test('sloupce vznikají z definice, ne natvrdo')
test('spodní lišta ukazuje počet neuložených změn')
test('mřížka o 1000 řádcích se vykreslí do 1,5 s')
```

```bash
cd ciselniky-web && npx vitest run && cd ..
git add -A && git commit -m "feat(web): kostra editační mřížky s přilepenou hlavičkou"
```

---

## Blok 7: Editace buněk

**Cíl bloku:** Hodnoty jdou měnit přímo v tabulce.

**Soubory:**
- Vytvoř: `ciselniky-web/src/komponenty/pm/PmDateField.tsx`, `PmSwitch.tsx`
- Vytvoř: `ciselniky-web/src/komponenty/BunkaAtributu.tsx`, `BunkaVazby.tsx`
- Test: `ciselniky-web/src/komponenty/BunkaAtributu.test.tsx`

- [ ] **Krok 1: Doplň chybějící wrappery**

Editační mřížka potřebuje navíc **`PmDateField`** a **`PmSwitch`**; P1 a P3 je nezavedly.
Vznikají stejným vzorem jako ostatní `pm-*` — jeden wrapper = jeden bod změny při upgradu
gov design systemu.

> **Past ze Zápisky:** CSS `:checked` **nematchuje** custom elementy. U `gov-form-switch`
> se stav v pravidlech stylu píše jako `[checked]`, nikdy `:checked`.

- [ ] **Krok 2: Buňka podle typu atributu**

Typ z definice určuje ovládací prvek:

```tsx
export function BunkaAtributu(props: {
  definice: { kod: string; nazev: string; typ: TypAtributu; vycet: string[] }
  hodnota: string | null
  onZmena: (hodnota: string | null) => void
}) {
  switch (props.definice.typ) {
    case 'TEXT':   return <PmField      hodnota={props.hodnota ?? ''} onZmena={props.onZmena} />
    case 'CISLO':  return <PmField      hodnota={props.hodnota ?? ''} onZmena={props.onZmena} cislo />
    case 'DATUM':  return <PmDateField  hodnota={props.hodnota}       onZmena={props.onZmena} />
    case 'ANO_NE': return <PmSwitch     zapnuto={props.hodnota === 'true'}
                                        onZmena={(z) => props.onZmena(z ? 'true' : 'false')} />
    case 'VYCET':  return <PmSelect     hodnota={props.hodnota ?? ''}
                                        moznosti={props.definice.vycet.map((h) => ({ hodnota: h, popisek: h }))}
                                        onZmena={props.onZmena} popisek={props.definice.nazev} />
  }
}
```

- [ ] **Krok 3: Buňka vazby**

Cílová položka se **vybírá ze seznamu, nikdy nepíše z hlavy**:

```tsx
<PmSelect
  popisek={vazba.nazev}
  hodnota={vybranyKod ?? ''}
  moznosti={cilovePolozky.map((p) => ({ hodnota: p.kod, popisek: `${p.kod} — ${p.nazev}` }))}
  onZmena={nastavCil}
/>
```

> Ručně psaný kód cizí položky je pozvánka k překlepu, který projde až do konzumujících
> aplikací. Server ho sice odmítne, ale uživatel se to dozví až při ukládání celé tabulky.

- [ ] **Krok 4: Přidání a vyřazení hodnoty**

Řádek jde přidat **třemi cestami** a každá má svůj důvod:

| Cesta | Kdy | Co udělá |
|---|---|---|
| `+ Přidat hodnotu` v liště | běžné přidání | prázdný řádek se značkou `+` |
| **Klávesa TAB na poslední buňce posledního řádku** | plnění číselníku od stolu | totéž, **bez sáhnutí na myš** |
| `↳ + Podřízená` na řádku | prohloubení hierarchie | řádek s **předvyplněnou nadřazenou položkou** |

```tsx
function naKlavesu(e: KeyboardEvent, radek: number, sloupec: number) {
  const poslednıBunka = radek === radky.length - 1 && sloupec === sloupce.length - 1
  if (e.key === 'Tab' && !e.shiftKey && poslednıBunka) {
    e.preventDefault()
    pridejRadek()          // ohnisko skočí na kód nového řádku
  }
}
```

> **TAB na konci je záměrný.** Kdo plní číselník o dvanácti hodnotách, nemá dvanáctkrát
> sahat na myš. Lidé to od tabulkového editoru čekají a jinde by to hledali marně.

Akce `↳ + Podřízená` se zobrazuje **jen u hierarchického číselníku** a je to hlavní cesta,
jak číselník prohloubit o další úroveň — **předvyplní nadřazenou položku**, takže odpadá
hledání rodiče i možnost splést se ([../10-specifikace/14-postupy-uzivatelu.md](../10-specifikace/14-postupy-uzivatelu.md), postup C).

Vyřazení nastaví konec platnosti a označí řádek `−`.

> **Vyřazení nemá koš.** Ikona koše slibuje mazání, které aplikace nedělá.
> Slovo „smazat" se v aplikaci nevyskytuje vůbec.

- [ ] **Krok 5: Buňka nadřazené položky**

Našeptávač nad položkami **téhož** číselníku. Nabídka **vylučuje položku samotnou
i všechny její potomky** — cyklus tak nejde vyrobit ani omylem, a uživatel se o něm
nedozví až z chybové hlášky při ukládání.

```tsx
function nabidkaRodicu(vsechny: Radek[], proRadek: Radek): Radek[] {
  const zakazane = new Set<string>([proRadek.kod])
  let pridano = true
  while (pridano) {                     // potomci, potomci potomků, …
    pridano = false
    for (const r of vsechny)
      if (r.nadrazenyKod && zakazane.has(r.nadrazenyKod) && !zakazane.has(r.kod)) {
        zakazane.add(r.kod); pridano = true
      }
  }
  return vsechny.filter((r) => !zakazane.has(r.kod))
}
```

> Kontrola cyklu na serveru (`HierarchieKontrola` z P4) **zůstává** — tohle je pohodlí
> pro uživatele, ne náhrada za ni. Prohlížeč nikdy nekontroluje, jen napovídá.

- [ ] **Krok 6: Značky a zvýraznění**

```tsx
const ZNACKA = { pridana: '+', zmenena: '~', vyrazena: '−' } as const
```

Značka **doprovází barvu, nenese ji sama** — vytištěná tabulka je načerno.

- [ ] **Krok 7: Testy a commit**

```tsx
test('typ atributu určuje ovládací prvek buňky')
test('výčet nabízí jen přípustné hodnoty')
test('cíl vazby se vybírá ze seznamu, nejde napsat')
test('vyřazení nastaví konec platnosti a značku −')
```

```bash
cd ciselniky-web && npx vitest run && cd ..
git add -A && git commit -m "feat(web): editace buněk podle typu atributu a výběr cíle vazby"
```

---

## Blok 8: Seznam změn a hláška o cizím zámku

**Cíl bloku:** Uzavření editační cesty podle průchodů W3 a W6.

**Soubory:**
- Vytvoř: `ciselniky-web/src/komponenty/SeznamZmen.tsx`, `PruhZamku.tsx`
- Test: `Ciselniky.Tests.E2E/Scenare/EditaceScenareTests.cs`

- [ ] **Krok 1: Spodní lišta**

```tsx
<div className="editace-lista">
  <span>{pocetZmen === 0 ? 'Žádné neuložené změny' : `${pocetZmen} ${sklonuj(pocetZmen)}`}</span>
  <PmButton varianta="ghost"     onClick={otevriSeznam} zakazano={pocetZmen === 0}>
    Zobrazit změny
  </PmButton>
  <PmButton varianta="secondary" onClick={uloz}         zakazano={pocetZmen === 0}>
    Uložit
  </PmButton>
  <PmButton varianta="primary"   onClick={publikuj}     zakazano={!maRozpracovane}>
    Publikovat verzi
  </PmButton>
</div>
```

Názvy akcí a jejich potvrzení se řídí **slovníkem akcí** z
[12-prochazeni.md](../10-specifikace/12-prochazeni.md): `Publikovat verzi`
→ `Verze 3.15 publikována`. Nikdy „operace proběhla úspěšně".

- [ ] **Krok 2: Seznam změn**

Otevře přehled rozpracovaných změn seskupený po položkách.
**Není podmínkou uložení**, jen nabídnutý.

Modál se zavírá **jen křížkem** — klik na pozadí zavírat nesmí. Tažení myší z pole ven
by jinak zavřelo rozdělanou práci.

- [ ] **Krok 3: Pruh cizího zámku**

```tsx
<gov-message color="warning">
  Číselník upravuje {drzitel} od {formatCas(od)}. Otevřít pro úpravy zatím nejde.
  Zámek se sám uvolní po pěti hodinách bez její činnosti.
</gov-message>
{smim('zamek.odebrat', kod) && (
  <PmButton varianta="secondary" onClick={odeberZamek}>Odebrat zámek</PmButton>
)}
```

Poslední věta hlášky není zdvořilost. Bez ní uživatel neví, jestli má čekat minutu,
nebo psát správci.

- [ ] **Krok 4: Koncové testy**

```csharp
[Fact] public async Task Editace_ZmenaAUlozeni_NezmeniPublikovanyStav()
[Fact] public async Task Editace_Publikovani_ZmeniPublikovanyStavAUvolniZamek()
[Fact] public async Task Editace_DruhyUzivatel_VidiPruhSeJmenem()
[Fact] public async Task Editace_NacteniStrankyZnovu_ZamekZustaneMuj()
```

Poslední test je ten, kvůli kterému zámek patří osobě. V Playwrightu se ověřuje
znovunačtením stránky a opětovným otevřením editace týmž uživatelem.

> Připomenutí pastí: host gov komponenty je pro Playwright „neviditelný" — klikat
> dispatchem události, čekat na třídu `hydrated`, viditelnost ověřovat počtem prvků.

- [ ] **Krok 5: Plná sada a commit**

```bash
cd ciselniky-web && npx vitest run && cd ..
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(web): seznam změn, spodní lišta a pruh cizího zámku"
```

---

## Po dokončení P6 ručně ověř

1. Otevři **Rozpočtové cíle** → `Upravit hodnoty`. Editace se otevře.
2. Ve druhém prohlížeči jako **jiný uživatel** zkus totéž — dostaneš pruh se jménem
   prvního uživatele a časem, od kdy drží.
3. V prvním prohlížeči **načti stránku znovu** a otevři editaci zase — **projde**,
   protože zámek patří tobě, ne oknu.
4. Změň stav u jednoho cíle, klikni `Uložit`. Publikovaný stav se nezměnil.
5. Otevři `Zobrazit změny` — vidíš jednu změnu seskupenou pod položkou.
6. Klikni `Publikovat verzi` — hláška řekne `Verze 1.1 publikována`, zámek se uvolnil
   a druhý uživatel teď editaci otevře.
7. Zkus změnit strukturu, když máš nepublikované změny hodnot — dostaneš hlášku,
   ať je nejdřív publikuješ.
8. Publikuj je, pak přidej atribut — vznikne verze **2.0**.
9. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D14 | Silové odebrání zámku se nikam nezapisuje — auditní log zavádí až P9. Doplnit zápis. | **P9** |
| D15 | Délka platnosti zámku je konstanta v `ZamekSluzba`. Přesunout do nastavení spolu s D6 a D9. | **P8** |
| D16 | Buňka vazby načítá **všechny** položky cílového číselníku. U číselníku o tisících hodnot je potřeba našeptávač, ne úplný seznam. | **P7** |
| D21 | Mřížka vykresluje všechny řádky najednou. Překročí-li měření mez 1,5 s, doplnit vykreslování jen viditelného okna — okénkuje se vykreslení, nikdy data. | *podle měření* |
