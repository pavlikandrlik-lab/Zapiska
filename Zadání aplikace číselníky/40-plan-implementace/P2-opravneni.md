# P2 — Oprávnění: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Aplikace ví, kdo co smí. Role jsou definované v kódu a verzované v gitu, přidělují
se osobám **s datovým rozsahem** (výčet číselníků). Správce vidí, kdo má co a odkud to má.

**Architektura:** Seed-only RBAC převzatý ze Zápisky a **zjednodušený**. Katalog akcí, rolí
a jejich mapování je v kódu; databáze ho při startu jen zrcadlí. Za každý HTTP požadavek se
sestaví projekce efektivních práv a všechny kontroly čtou z ní.

**Stack:** .NET 10, EF Core + SQL Server, React + TypeScript, xUnit.

**Specifikace:** [../10-specifikace/02-role-a-opravneni.md](../10-specifikace/02-role-a-opravneni.md) ·
**Wireframy:** [../10-specifikace/11-wireframy.md](../10-specifikace/11-wireframy.md) (O8, O9) ·
**Architektura:** [../20-architektura/02-backend-vrstveni.md](../20-architektura/02-backend-vrstveni.md)

**Global Constraints:** [README.md](README.md#global-constraints). Platí, neopakují se.

**Navazuje na:** P1 — `osoby`, `superadmini`, `ICurrentUserAccessor`, testovací infrastruktura.

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Databáze má schéma oprávnění a minimální tabulku číselníků jako cíl rozsahu |
| 2 | Katalog akcí a rolí je v kódu, při startu se zrcadlí do databáze, architektonické testy ho hlídají |
| 3 | Aplikace umí sestavit efektivní práva osoby včetně datového rozsahu |
| 4 | Chráněný koncový bod odmítne toho, kdo na něj nemá právo |
| 5 | Prohlížeč zná svá práva a nenabízí, co server odmítne |
| 6 | Obrazovka O8 — přidělování rolí s rozsahem |
| 7 | Obrazovka O9 — efektivní práva se zdrojem grantu |

---

## Co se proti Zápisce zjednodušuje

Zápiska rozlišuje **tři** pojmy rozsahu, protože role se přidělují členstvím v projektu
nebo subsystému. Číselníky přidělují roli přímo, spolu s výčtem číselníků — a tím jeden
pojem odpadá.

| Zápiska | Číselníky |
|---|---|
| `RoleScope` — jak se role přiděluje (globálně / projektem / subsystémem) | **zaniká.** Role se přiděluje vždy přímo osobě. |
| `PermissionScopeLevel` — potřebuje klíč kontext? | zůstává: `GLOBAL` \| `CISELNIK` |
| `ScopeMode` — šířka grantu | zůstává, ale jen dvě hodnoty: `VSE` \| `VYBER` |

Zápiskovy hodnoty `Own` a `Subsystem` se nepřebírají — nemají v této doméně obsah.

> **Nepřenášej z Zápisky `RoleScope`.** Je to nejlákavější kus k opsání a nemá tu co dělat;
> jen by přidal třetí pojem, který nikdy nenabude jiné hodnoty.

---

## Blok 1: Schéma oprávnění

**Cíl bloku:** Databáze umí uložit katalog akcí a rolí, jejich mapování a přiřazení osobě
s datovým rozsahem.

**Soubory:**
- Vytvoř: `db/db_upgrade_0_2_authz.sql`
- Vytvoř: `Ciselniky.Core/Domain/Authz/AuthzAkce.cs`, `AuthzRole.cs`, `AuthzRoleAkce.cs`, `AuthzPrirazeni.cs`
- Vytvoř: `Ciselniky.Core/Domain/Ciselnik.cs`, `Ciselniky.Core/Domain/Superadmin.cs`
- Uprav: `Ciselniky.Core/Data/CiselnikyDbContext.cs`, `Ciselniky.Core/Data/KontrolaSchematu.cs`
- Test: `Ciselniky.Tests.Integration/Data/AuthzSchemaTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyDbContext` se sadami `.Ciselniky`, `.Superadmini`, `.AuthzAkce`,
  `.AuthzRole`, `.AuthzRoleAkce`, `.AuthzPrirazeni`, `.AuthzPrirazeniCiselnik`.
- Poskytuje entity `Ciselnik`, `Superadmin`, `AuthzAkce`, `AuthzRole`, `AuthzRoleAkce`,
  `AuthzPrirazeni`, `AuthzPrirazeniCiselnik` a enumy `UrovenAkce`, `RozsahPrirazeni`.

- [ ] **Krok 1: Migrační skript**

`db/db_upgrade_0_2_authz.sql`:

```sql
-- Oprávnění: katalog akcí a rolí + přiřazení osobě s datovým rozsahem.

-- Minimální číselník. Datový rozsah oprávnění na něj ukazuje, takže musí
-- existovat dřív než přiřazení. P3 tabulku rozšíří o definici struktury,
-- režim správy a hierarchii — sloupce se doplní dalším upgrade skriptem.
CREATE TABLE ciselniky (
    id       int      IDENTITY(1,1) PRIMARY KEY,
    kod      nvarchar(64)  NOT NULL UNIQUE,
    nazev    nvarchar(256) NOT NULL,
    aktivni  bit      NOT NULL DEFAULT 1
);

CREATE TABLE authz_akce (
    klic       nvarchar(64)  PRIMARY KEY,
    nazev      nvarchar(128) NOT NULL,
    kategorie  nvarchar(32)  NOT NULL,
    uroven     nvarchar(16)  NOT NULL CHECK (uroven IN ('GLOBAL', 'CISELNIK'))
);

CREATE TABLE authz_role (
    kod      nvarchar(32)  PRIMARY KEY,
    nazev    nvarchar(128) NOT NULL,
    popis    nvarchar(512),
    aktivni  bit      NOT NULL DEFAULT 1
);

CREATE TABLE authz_role_akce (
    role_kod   nvarchar(32) NOT NULL REFERENCES authz_role(kod) ON DELETE CASCADE,
    akce_klic  nvarchar(64) NOT NULL REFERENCES authz_akce(klic) ON DELETE CASCADE,
    PRIMARY KEY (role_kod, akce_klic)
);

CREATE TABLE authz_prirazeni (
    id             int     IDENTITY(1,1) PRIMARY KEY,
    osoba_id       int     NOT NULL REFERENCES osoby(id),
    role_kod       nvarchar(32) NOT NULL REFERENCES authz_role(kod),
    rozsah         nvarchar(16) NOT NULL CHECK (rozsah IN ('VSE', 'VYBER')),
    aktivni        bit     NOT NULL DEFAULT 1,
    prideleno_kdy  datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
    pridelil_id    int     REFERENCES osoby(id),
    UNIQUE (osoba_id, role_kod)
);

CREATE TABLE authz_prirazeni_ciselnik (
    prirazeni_id  int NOT NULL REFERENCES authz_prirazeni(id) ON DELETE CASCADE,
    ciselnik_id   int NOT NULL REFERENCES ciselniky(id),
    PRIMARY KEY (prirazeni_id, ciselnik_id)
);

-- Sestavení efektivních práv se ptá vždy „co má tahle osoba".
CREATE INDEX ix_authz_prirazeni_osoba ON authz_prirazeni (osoba_id) WHERE aktivni;

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.2');
```

> **Proč `UNIQUE (osoba_id, role_kod)`:** jedna osoba má danou roli buď na výběr číselníků,
> nebo na všechny — ne obojí. Dvě přiřazení téže role by znamenala dvě odpovědi na jednu
> otázku a někdo by musel rozhodovat, která platí.

> **Rozsah `VYBER` s prázdným výčtem neuděluje nic.** Není to chyba, je to platný stav —
> role přidělená a rozsah zatím nevyplněný. Kontrola v datech se proto nedělá; řeší se
> v obrazovce O8 upozorněním.

- [ ] **Krok 2: Napiš padající test schématu**

`Ciselniky.Tests.Integration/Data/AuthzSchemaTests.cs`:

```csharp
using Ciselniky.Tests.Integration.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Tests.Integration.Data;

public sealed class AuthzSchemaTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Osoba_NemuzeMitTutezRoliDvakrat()
    {
        await using var db = fixture.VytvorContext();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO osoby (ad_guid, login, jmeno, prijmeni)
            VALUES (NEWID(), N'FIS\test1', N'Test', N'Test');
            INSERT INTO authz_role (kod, nazev) VALUES ('EDITOR', 'Editor');
            INSERT INTO authz_prirazeni (osoba_id, role_kod, rozsah)
            SELECT id, 'EDITOR', 'VSE' FROM osoby WHERE login = 'FIS\test1';
            """);

        var vyjimka = await Assert.ThrowsAsync<SqlException>(() =
            db.Database.ExecuteSqlRawAsync("""
                INSERT INTO authz_prirazeni (osoba_id, role_kod, rozsah)
                SELECT id, 'EDITOR', 'VYBER' FROM osoby WHERE login = 'FIS\test1';
                """));

        Assert.Contains(vyjimka.Number, new[] { 2627, 2601 });   // porušení jedinečnosti
    }

    [Fact]
    public async Task Rozsah_PrijimaJenZnameHodnoty()
    {
        await using var db = fixture.VytvorContext();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO osoby (ad_guid, login, jmeno, prijmeni)
            VALUES (NEWID(), N'FIS\test2', N'Test', N'Test');
            INSERT INTO authz_role (kod, nazev) VALUES ('EDITOR2', 'Editor');
            """);

        var vyjimka = await Assert.ThrowsAsync<SqlException>(() =
            db.Database.ExecuteSqlRawAsync("""
                INSERT INTO authz_prirazeni (osoba_id, role_kod, rozsah)
                SELECT id, 'EDITOR2', 'NECO_JINEHO' FROM osoby WHERE login = 'FIS\test2';
                """));

        Assert.Equal(547, vyjimka.Number);   // porušení kontrolní podmínky
    }
}
```

- [ ] **Krok 3: Spusť test a ověř, že padá**

```bash
dotnet test Ciselniky.Tests.Integration --filter AuthzSchemaTests
```

Očekávej: FAIL — tabulky `authz_*` neexistují.

- [ ] **Krok 4: Zapoj skript do fixture**

V `SqlFixture.InitializeAsync` se po baseline spouštějí i upgrade skripty
v pořadí verzí:

```csharp
foreach (var skript in Directory
             .EnumerateFiles(AdresarDb(), "db_upgrade_*.sql")
             .OrderBy(c => c, StringComparer.Ordinal))
{
    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(skript));
}
```

> Řazení je **ordinální podle názvu souboru**, proto se čísla verzí v názvu píšou
> podtržítky (`0_2`, `0_10`). Nikdy `0.2` a `0.10` — ty by se seřadily obráceně.

- [ ] **Krok 5: Doplň entity a kontext**

`Ciselniky.Core/Domain/Ciselnik.cs`:

```csharp
namespace Ciselniky.Core.Domain;

public sealed class Ciselnik
{
    public int Id { get; set; }
    public required string Kod { get; set; }
    public required string Nazev { get; set; }
    public bool Aktivni { get; set; } = true;
}
```

`Ciselniky.Core/Domain/Authz/AuthzTypy.cs`:

```csharp
namespace Ciselniky.Core.Domain.Authz;

/// <summary>Potřebuje klíč kontext číselníku, nebo se vyhodnocuje globálně?</summary>
public enum UrovenAkce { Global, Ciselnik }

/// <summary>Šířka grantu v přiřazení role osobě.</summary>
public enum RozsahPrirazeni
{
    /// <summary>Všechny číselníky.</summary>
    Vse,
    /// <summary>Jen vyjmenované číselníky.</summary>
    Vyber
}

public sealed class AuthzAkce
{
    public required string Klic { get; set; }
    public required string Nazev { get; set; }
    public required string Kategorie { get; set; }
    public UrovenAkce Uroven { get; set; }
}

public sealed class AuthzRole
{
    public required string Kod { get; set; }
    public required string Nazev { get; set; }
    public string? Popis { get; set; }
    public bool Aktivni { get; set; } = true;
}

public sealed class AuthzRoleAkce
{
    public required string RoleKod { get; set; }
    public required string AkceKlic { get; set; }
}

public sealed class AuthzPrirazeni
{
    public int Id { get; set; }
    public int OsobaId { get; set; }
    public required string RoleKod { get; set; }
    public RozsahPrirazeni Rozsah { get; set; }
    public bool Aktivni { get; set; } = true;
    public DateTimeOffset PridelenoKdy { get; set; }
    public int? PridelilId { get; set; }
}

/// <summary>Výčet číselníků u přiřazení s rozsahem <see cref="RozsahPrirazeni.Vyber"/>.</summary>
public sealed class AuthzPrirazeniCiselnik
{
    public int PrirazeniId { get; set; }
    public int CiselnikId { get; set; }
}
```

`Ciselniky.Core/Domain/Superadmin.cs` — tabulku založil P1, entita chyběla:

```csharp
namespace Ciselniky.Core.Domain;

/// <summary>Nouzový klíč. Kdo je zde, projde každou kontrolou oprávnění.</summary>
public sealed class Superadmin
{
    public int OsobaId { get; set; }
    public string? Poznamka { get; set; }
    public DateTimeOffset ZalozenoKdy { get; set; }
    public int? ZalozilId { get; set; }
}
```

V `CiselnikyDbContext` přibudou sady:

```csharp
public DbSet<Ciselnik> Ciselniky => Set<Ciselnik>();
public DbSet<Superadmin> Superadmini => Set<Superadmin>();
public DbSet<AuthzAkce> AuthzAkce => Set<AuthzAkce>();
public DbSet<AuthzRole> AuthzRole => Set<AuthzRole>();
public DbSet<AuthzRoleAkce> AuthzRoleAkce => Set<AuthzRoleAkce>();
public DbSet<AuthzPrirazeni> AuthzPrirazeni => Set<AuthzPrirazeni>();
public DbSet<AuthzPrirazeniCiselnik> AuthzPrirazeniCiselnik => Set<AuthzPrirazeniCiselnik>();
```

V `OnModelCreating` se enumy ukládají jako text velkými písmeny (`Vse` → `VSE`),
aby byla data čitelná i mimo aplikaci, a složené klíče se určí výslovně:

```csharp
model.Entity<Superadmin>().ToTable("superadmini").HasKey(s => s.OsobaId);
model.Entity<AuthzRoleAkce>().ToTable("authz_role_akce")
     .HasKey(m => new { m.RoleKod, m.AkceKlic });
model.Entity<AuthzPrirazeniCiselnik>().ToTable("authz_prirazeni_ciselnik")
     .HasKey(v => new { v.PrirazeniId, v.CiselnikId });

model.Entity<AuthzAkce>().ToTable("authz_akce").HasKey(a => a.Klic);
model.Entity<AuthzAkce>().Property(a => a.Uroven)
     .HasConversion(v => v.ToString().ToUpperInvariant(),
                    v => Enum.Parse<UrovenAkce>(v, ignoreCase: true));

model.Entity<AuthzPrirazeni>().Property(p => p.Rozsah)
     .HasConversion(v => v.ToString().ToUpperInvariant(),
                    v => Enum.Parse<RozsahPrirazeni>(v, ignoreCase: true));
```

- [ ] **Krok 6: Doplň tabulky do kontroly schématu**

V `KontrolaSchematu.OcekavaneTabulky`:

```csharp
private static readonly string[] OcekavaneTabulky =
[
    "osoby", "superadmini", "aplikovane_upgrady",
    "ciselniky", "authz_akce", "authz_role", "authz_role_akce",
    "authz_prirazeni", "authz_prirazeni_ciselnik"
];
```

- [ ] **Krok 7: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Integration --filter AuthzSchemaTests   # Passed: 2
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(authz): schéma oprávnění s datovým rozsahem na číselník"
```

---

## Blok 2: Katalog akcí a rolí v kódu

**Cíl bloku:** Katalog je **jediný zdroj pravdy** a je v gitu. Databáze ho při startu zrcadlí.
Architektonické testy nedovolí, aby se rozešly.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Security/KliceOpravneni.cs`
- Vytvoř: `Ciselniky.Core/Security/SeedKonfigurace.cs`
- Vytvoř: `Ciselniky.Core/Security/Seeder.cs`
- Test: `Ciselniky.Tests.Unit/Architektura/SeedZdrojPravdyTests.cs`

**Rozhraní:**
- Poskytuje: `KliceOpravneni.VsechnyDefinice → IReadOnlyList<DefiniceAkce>`,
  konstanty `KliceOpravneni.HodnotyEdit` a spol.;
  `Seeder.AplikujAsync(CiselnikyDbContext, CancellationToken)`.

- [ ] **Krok 1: Katalog klíčů**

`Ciselniky.Core/Security/KliceOpravneni.cs`:

```csharp
using Ciselniky.Core.Domain.Authz;

namespace Ciselniky.Core.Security;

public sealed record DefiniceAkce(string Klic, string Nazev, string Kategorie, UrovenAkce Uroven);

/// <summary>
/// Katalog akcí. Jeden klíč = jedna měnící akce — ne hrubý klíč „editace".
/// <b>Čtení tu není a nikdy být nemá</b>: číselníky vidí každý, i doménový uživatel
/// bez záznamu v aplikaci (rozhodnutí N1).
/// </summary>
public static class KliceOpravneni
{
    public const string CiselnikyCreate     = "ciselniky.create";
    public const string CiselnikyEdit       = "ciselniky.edit";
    public const string CiselnikyDefiniceEdit = "ciselniky.definice.edit";
    public const string CiselnikyDeactivate = "ciselniky.deactivate";

    public const string HodnotyCreate       = "hodnoty.create";
    public const string HodnotyEdit         = "hodnoty.edit";
    public const string HodnotyDeactivate   = "hodnoty.deactivate";

    public const string VerzePublish        = "verze.publish";
    public const string ImportJson          = "import.json";
    public const string ZamekOdebrat        = "zamek.odebrat";

    public const string NastaveniRoleAssign = "nastaveni.role.assign";
    public const string NastaveniAuditView  = "nastaveni.audit.view";
    public const string HledaniReindex      = "hledani.reindex";

    private static readonly DefiniceAkce[] Definice =
    [
        new(CiselnikyCreate,       "Založit číselník",          "CISELNIKY", UrovenAkce.Global),
        new(CiselnikyEdit,         "Upravit číselník",          "CISELNIKY", UrovenAkce.Ciselnik),
        new(CiselnikyDefiniceEdit, "Změnit strukturu",          "CISELNIKY", UrovenAkce.Ciselnik),
        new(CiselnikyDeactivate,   "Vyřadit číselník",          "CISELNIKY", UrovenAkce.Ciselnik),

        new(HodnotyCreate,         "Přidat hodnotu",            "HODNOTY",   UrovenAkce.Ciselnik),
        new(HodnotyEdit,           "Upravit hodnotu",           "HODNOTY",   UrovenAkce.Ciselnik),
        new(HodnotyDeactivate,     "Vyřadit hodnotu",           "HODNOTY",   UrovenAkce.Ciselnik),

        new(VerzePublish,          "Publikovat verzi",          "VERZE",     UrovenAkce.Ciselnik),
        new(ImportJson,            "Nahrát soubor JSON",        "IMPORT",    UrovenAkce.Ciselnik),
        new(ZamekOdebrat,          "Odebrat zámek",             "EDITACE",   UrovenAkce.Ciselnik),

        new(NastaveniRoleAssign,   "Přidělovat role",           "NASTAVENI", UrovenAkce.Global),
        new(NastaveniAuditView,    "Zobrazit auditní log",      "NASTAVENI", UrovenAkce.Global),
        new(HledaniReindex,        "Přeindexovat vyhledávání",  "NASTAVENI", UrovenAkce.Global),
    ];

    public static IReadOnlyList<DefiniceAkce> VsechnyDefinice => Definice;
}
```

> Klíče pro **návrhy** (`navrhy.create`, `navrhy.approve`, `navrhy.reject`) se **nezavádějí**.
> Role *Navrhovatel* je v etapě 1 jen připravená, ne zavedená. Mrtvý klíč, který nic nechrání,
> je horší než chybějící — v Zápisce se takové musely uklízet migrací.

> Klíče pro **zdroje** (`zdroje.configure`, `zdroje.run`) patří etapě 2 a přibudou s ní.

- [ ] **Krok 2: Seed rolí a mapování**

`Ciselniky.Core/Security/SeedKonfigurace.cs`:

```csharp
namespace Ciselniky.Core.Security;

public sealed record DefiniceRole(string Kod, string Nazev, string Popis);
public sealed record Mapovani(string RoleKod, string AkceKlic);

/// <summary>
/// Jediný zdroj pravdy pro role a jejich oprávnění. Verzováno v gitu.
/// Runtime skládání rolí přes uživatelské rozhraní <b>neexistuje</b> — zabraňuje to
/// tichým rozdílům mezi prostředími a zajišťuje dohledatelnost změny v historii.
/// </summary>
public static class SeedKonfigurace
{
    public static readonly DefiniceRole[] Role =
    [
        new("EDITOR", "Editor",
            "Zakládá, mění a vyřazuje hodnoty v číselnících ve svém datovém rozsahu."),
        new("SPRAVCE_CISELNIKU", "Správce číselníků",
            "Zakládá číselníky a definuje jejich strukturu ve svém datovém rozsahu."),
        new("SPRAVCE_APLIKACE", "Správce aplikace",
            "Přiděluje role, vidí auditní log. Globální role bez datového rozsahu."),
    ];

    public static readonly Mapovani[] Mapovani =
    [
        new("EDITOR", KliceOpravneni.HodnotyCreate),
        new("EDITOR", KliceOpravneni.HodnotyEdit),
        new("EDITOR", KliceOpravneni.HodnotyDeactivate),
        new("EDITOR", KliceOpravneni.VerzePublish),
        new("EDITOR", KliceOpravneni.ImportJson),

        new("SPRAVCE_CISELNIKU", KliceOpravneni.CiselnikyCreate),
        new("SPRAVCE_CISELNIKU", KliceOpravneni.CiselnikyEdit),
        new("SPRAVCE_CISELNIKU", KliceOpravneni.CiselnikyDefiniceEdit),
        new("SPRAVCE_CISELNIKU", KliceOpravneni.CiselnikyDeactivate),
        new("SPRAVCE_CISELNIKU", KliceOpravneni.ZamekOdebrat),

        new("SPRAVCE_APLIKACE", KliceOpravneni.NastaveniRoleAssign),
        new("SPRAVCE_APLIKACE", KliceOpravneni.NastaveniAuditView),
        new("SPRAVCE_APLIKACE", KliceOpravneni.HledaniReindex),
    ];
}
```

> **Superadmin v seedu není.** Není to role a nemá klíče — je to řádek v tabulce
> `superadmini`, který zkratuje kontrolu. Kdyby byl rolí, závisel by na týchž datech,
> která má umět obejít.

- [ ] **Krok 3: Napiš padající architektonické testy**

`Ciselniky.Tests.Unit/Architektura/SeedZdrojPravdyTests.cs`:

```csharp
using System.Reflection;
using Ciselniky.Core.Security;

namespace Ciselniky.Tests.Unit.Architektura;

public sealed class SeedZdrojPravdyTests
{
    [Fact]
    public void KazdaKonstantaKlice_MaSvojiDefinici()
    {
        var konstanty = typeof(KliceOpravneni)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        var definovane = KliceOpravneni.VsechnyDefinice.Select(d => d.Klic).ToHashSet();

        Assert.Empty(konstanty.Except(definovane));
    }

    [Fact]
    public void KazdaDefinice_MaSvojiKonstantu()
    {
        var konstanty = typeof(KliceOpravneni)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        Assert.Empty(KliceOpravneni.VsechnyDefinice.Select(d => d.Klic).Except(konstanty));
    }

    [Fact]
    public void KazdaRole_MaAlesponJednoMapovani()
    {
        var namapovane = SeedKonfigurace.Mapovani.Select(m => m.RoleKod).ToHashSet();
        var bezMapovani = SeedKonfigurace.Role.Select(r => r.Kod).Except(namapovane).ToArray();

        Assert.True(bezMapovani.Length == 0,
            "Role bez jediného oprávnění: " + string.Join(", ", bezMapovani));
    }

    [Fact]
    public void KazdeMapovani_UkazujeNaExistujiciRoliAKlic()
    {
        var role = SeedKonfigurace.Role.Select(r => r.Kod).ToHashSet();
        var klice = KliceOpravneni.VsechnyDefinice.Select(d => d.Klic).ToHashSet();

        var neplatna = SeedKonfigurace.Mapovani
            .Where(m => !role.Contains(m.RoleKod) || !klice.Contains(m.AkceKlic))
            .Select(m => $"{m.RoleKod} → {m.AkceKlic}")
            .ToArray();

        Assert.True(neplatna.Length == 0,
            "Mapování do prázdna: " + string.Join(", ", neplatna));
    }

    [Fact]
    public void ZadnyKlic_NeniProCteni()
    {
        // Čtení není chráněná akce (rozhodnutí N1). Klíč se slovem read/view/list
        // by znamenal, že se někam vloudila kontrola na čtení.
        var podezrele = KliceOpravneni.VsechnyDefinice
            .Where(d => d.Klic.Contains(".read") || d.Klic.Contains(".list"))
            .Select(d => d.Klic)
            .ToArray();

        Assert.True(podezrele.Length == 0,
            "Klíče vypadající jako kontrola čtení: " + string.Join(", ", podezrele));
    }
}
```

> Poslední test není formalita. Kontrola na čtení je nejsnadnější věc, která se do
> aplikace vloudí omylem — a rozbila by celý smysl referenčního zdroje, ke kterému se
> každý dostane bez žádosti o přístup.

- [ ] **Krok 4: Spusť testy a ověř, že padají**

```bash
dotnet test Ciselniky.Tests.Unit --filter SeedZdrojPravdyTests
```

Očekávej: FAIL — `KliceOpravneni` a `SeedKonfigurace` neexistují (kroky 1 a 2 je zavedou;
padají-li testy i po nich, je chyba v katalogu, ne v testu).

- [ ] **Krok 5: Seeder**

`Ciselniky.Core/Security/Seeder.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain.Authz;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Security;

/// <summary>
/// Zrcadlí katalog z kódu do databáze při startu aplikace.
/// Databáze je projekce, ne zdroj — co v katalogu není, se z ní odebere.
/// </summary>
public static class Seeder
{
    public static async Task AplikujAsync(CiselnikyDbContext db, CancellationToken ct = default)
    {
        await SrovnejAkceAsync(db, ct);
        await SrovnejRoleAsync(db, ct);
        await SrovnejMapovaniAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SrovnejAkceAsync(CiselnikyDbContext db, CancellationToken ct)
    {
        var vDb = await db.AuthzAkce.ToDictionaryAsync(a => a.Klic, ct);

        foreach (var definice in KliceOpravneni.VsechnyDefinice)
        {
            if (vDb.TryGetValue(definice.Klic, out var akce))
            {
                akce.Nazev = definice.Nazev;
                akce.Kategorie = definice.Kategorie;
                akce.Uroven = definice.Uroven;
                vDb.Remove(definice.Klic);
            }
            else
            {
                db.AuthzAkce.Add(new AuthzAkce
                {
                    Klic = definice.Klic, Nazev = definice.Nazev,
                    Kategorie = definice.Kategorie, Uroven = definice.Uroven
                });
            }
        }

        // Co zbylo, v katalogu už není — mrtvý klíč se odebere, ať nechrání nic.
        db.AuthzAkce.RemoveRange(vDb.Values);
    }

    private static async Task SrovnejRoleAsync(CiselnikyDbContext db, CancellationToken ct)
    {
        var vDb = await db.AuthzRole.ToDictionaryAsync(r => r.Kod, ct);

        foreach (var definice in SeedKonfigurace.Role)
        {
            if (vDb.TryGetValue(definice.Kod, out var role))
            {
                role.Nazev = definice.Nazev;
                role.Popis = definice.Popis;
                role.Aktivni = true;
                vDb.Remove(definice.Kod);
            }
            else
            {
                db.AuthzRole.Add(new AuthzRole
                {
                    Kod = definice.Kod, Nazev = definice.Nazev, Popis = definice.Popis
                });
            }
        }

        // Role, která zmizela z katalogu, se deaktivuje — nemaže.
        // Visela by na ní přiřazení a mazání by o ně přišlo.
        foreach (var zbyla in vDb.Values) zbyla.Aktivni = false;
    }

    private static async Task SrovnejMapovaniAsync(CiselnikyDbContext db, CancellationToken ct)
    {
        var vDb = await db.AuthzRoleAkce.ToListAsync(ct);
        var chtena = SeedKonfigurace.Mapovani
            .Select(m => (m.RoleKod, m.AkceKlic)).ToHashSet();

        db.AuthzRoleAkce.RemoveRange(
            vDb.Where(m => !chtena.Contains((m.RoleKod, m.AkceKlic))));

        var existujici = vDb.Select(m => (m.RoleKod, m.AkceKlic)).ToHashSet();
        foreach (var (roleKod, akceKlic) in chtena.Except(existujici))
            db.AuthzRoleAkce.Add(new AuthzRoleAkce { RoleKod = roleKod, AkceKlic = akceKlic });
    }
}
```

> **Role se deaktivuje, nemaže** — na rozdíl od akcí a mapování. Visí na ní přiřazení
> osobám a smazání by o ně nenávratně přišlo. Deaktivovaná role neuděluje nic,
> ale je vidět, komu byla kdy přidělená.

- [ ] **Krok 6: Zavolej seeder při startu**

V `Program.cs`, hned za kontrolou schématu:

```csharp
await using (var rozsah = app.Services.CreateAsyncScope())
{
    var db = rozsah.ServiceProvider.GetRequiredService<CiselnikyDbContext>();
    await KontrolaSchematu.OverAsync(db);
    await Seeder.AplikujAsync(db);
}
```

- [ ] **Krok 7: Integrační test, že se seed skutečně promítne**

`Ciselniky.Tests.Integration/Security/SeederTests.cs`:

```csharp
using Ciselniky.Core.Security;
using Ciselniky.Tests.Integration.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Tests.Integration.Security;

public sealed class SeederTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Seed_ZrcadliKatalogDoDatabaze()
    {
        await using var db = fixture.VytvorContext();

        await Seeder.AplikujAsync(db, TestContext.Current.CancellationToken);

        var akci = await db.AuthzAkce.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(KliceOpravneni.VsechnyDefinice.Count, akci);
    }

    [Fact]
    public async Task Seed_JeIdempotentni()
    {
        await using var db = fixture.VytvorContext();

        await Seeder.AplikujAsync(db, TestContext.Current.CancellationToken);
        await Seeder.AplikujAsync(db, TestContext.Current.CancellationToken);

        Assert.Equal(KliceOpravneni.VsechnyDefinice.Count,
                     await db.AuthzAkce.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(SeedKonfigurace.Mapovani.Length,
                     await db.AuthzRoleAkce.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Seed_OdebereMrtvyKlicZDatabaze()
    {
        await using var db = fixture.VytvorContext();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO authz_akce (klic, nazev, kategorie, uroven)
            VALUES ('mrtvy.klic', 'Mrtvý', 'TEST', 'GLOBAL');
            """);

        await Seeder.AplikujAsync(db, TestContext.Current.CancellationToken);

        Assert.False(await db.AuthzAkce.AnyAsync(a => a.Klic == "mrtvy.klic",
            TestContext.Current.CancellationToken));
    }
}
```

- [ ] **Krok 8: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Unit --filter SeedZdrojPravdyTests    # Passed: 5
dotnet test Ciselniky.Tests.Integration --filter SeederTests      # Passed: 3
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(authz): katalog akcí a rolí v kódu se zrcadlením do databáze"
```

---

## Blok 3: Efektivní práva osoby

**Cíl bloku:** Aplikace umí za jeden dotaz sestavit, co osoba smí a na kterých číselnících.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Security/OpravneniSnapshot.cs`
- Vytvoř: `Ciselniky.Core/Security/IOpravneniSluzba.cs`, `OpravneniSluzba.cs`
- Test: `Ciselniky.Tests.Unit/Security/OpravneniSnapshotTests.cs`
- Test: `Ciselniky.Tests.Integration/Security/OpravneniSluzbaTests.cs`

**Rozhraní:**
- Poskytuje: `OpravneniSnapshot.Ma(string klic, string? ciselnikKod = null) → bool`;
  `IOpravneniSluzba.SnapshotAsync(int osobaId, CancellationToken) → Task<OpravneniSnapshot>`.
- Používá: `superadmini`, `authz_*`, `ciselniky` z bloků 1 a 2.

- [ ] **Krok 1: Napiš padající testy projekce**

`Ciselniky.Tests.Unit/Security/OpravneniSnapshotTests.cs`:

```csharp
using Ciselniky.Core.Security;

namespace Ciselniky.Tests.Unit.Security;

public sealed class OpravneniSnapshotTests
{
    private static OpravneniSnapshot Snapshot(
        bool superadmin = false,
        string[]? globalni = null,
        Dictionary<string, string[]>? naCiselnik = null)
        => new(superadmin,
               (globalni ?? []).ToHashSet(),
               (naCiselnik ?? []).ToDictionary(
                   x => x.Key,
                   x => (IReadOnlySet<string>)x.Value.ToHashSet()));

    [Fact]
    public void Superadmin_MaVsechno()
    {
        var s = Snapshot(superadmin: true);

        Assert.True(s.Ma(KliceOpravneni.HodnotyEdit, "cile"));
        Assert.True(s.Ma(KliceOpravneni.NastaveniRoleAssign));
    }

    [Fact]
    public void GlobalniOpravneni_PlatiBezKontextu()
    {
        var s = Snapshot(globalni: [KliceOpravneni.NastaveniRoleAssign]);

        Assert.True(s.Ma(KliceOpravneni.NastaveniRoleAssign));
    }

    [Fact]
    public void OpravneniNaCiselnik_PlatiJenNaSvemCiselniku()
    {
        var s = Snapshot(naCiselnik: new() { ["cile"] = [KliceOpravneni.HodnotyEdit] });

        Assert.True(s.Ma(KliceOpravneni.HodnotyEdit, "cile"));
        Assert.False(s.Ma(KliceOpravneni.HodnotyEdit, "osoby"));
    }

    [Fact]
    public void OpravneniNaCiselnik_BezUvedeniCiselniku_Neplati()
    {
        // Toto je past ze Zápisky: dotaz bez kontextu se vyhodnotí jako globální.
        // Kdo má právo jen na výběr číselníků, ho globálně nemá — a nesmí projít.
        var s = Snapshot(naCiselnik: new() { ["cile"] = [KliceOpravneni.HodnotyEdit] });

        Assert.False(s.Ma(KliceOpravneni.HodnotyEdit));
    }

    [Fact]
    public void NeznameOpravneni_Neplati()
    {
        var s = Snapshot(globalni: [KliceOpravneni.NastaveniAuditView]);

        Assert.False(s.Ma(KliceOpravneni.HodnotyEdit, "cile"));
    }
}
```

- [ ] **Krok 2: Spusť testy a ověř, že padají**

```bash
dotnet test Ciselniky.Tests.Unit --filter OpravneniSnapshotTests
```

Očekávej: FAIL — `OpravneniSnapshot` neexistuje.

- [ ] **Krok 3: Projekce**

`Ciselniky.Core/Security/OpravneniSnapshot.cs`:

```csharp
namespace Ciselniky.Core.Security;

/// <summary>
/// Projekce efektivních práv jedné osoby, sestavená jednou za HTTP požadavek.
/// Odpovídá na dotazy bez dalších dotazů do databáze. Neukládá se — zdrojem pravdy
/// je katalog v kódu a přiřazení v databázi.
/// </summary>
/// <param name="OpravneniNaCiselnik">Klíčem je <b>kód</b> číselníku, ne identifikátor —
/// kód přichází v adrese požadavku a nemusí se kvůli kontrole překládat.</param>
public sealed record OpravneniSnapshot(
    bool JeSuperadmin,
    IReadOnlySet<string> GlobalniOpravneni,
    IReadOnlyDictionary<string, IReadOnlySet<string>> OpravneniNaCiselnik)
{
    public bool Ma(string klic, string? ciselnikKod = null)
    {
        if (JeSuperadmin) return true;
        if (GlobalniOpravneni.Contains(klic)) return true;

        return ciselnikKod is not null
            && OpravneniNaCiselnik.TryGetValue(ciselnikKod, out var opravneni)
            && opravneni.Contains(klic);
    }

    /// <summary>Kódy číselníků, na kterých osoba drží daný klíč. Superadmin: <c>null</c> = všechny.</summary>
    public IReadOnlyCollection<string>? RozsahProKlic(string klic)
    {
        if (JeSuperadmin || GlobalniOpravneni.Contains(klic)) return null;

        return OpravneniNaCiselnik
            .Where(dvojice => dvojice.Value.Contains(klic))
            .Select(dvojice => dvojice.Key)
            .ToArray();
    }
}
```

- [ ] **Krok 4: Sestavení projekce z databáze**

`Ciselniky.Core/Security/OpravneniSluzba.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain.Authz;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Security;

public interface IOpravneniSluzba
{
    Task<OpravneniSnapshot> SnapshotAsync(int osobaId, CancellationToken ct = default);
}

/// <summary>
/// Sestaví projekci práv. Registruje se jako <c>Scoped</c> a projekci si pamatuje
/// po dobu požadavku — během jednoho požadavku se ptáme mnohokrát, databáze jednou.
/// </summary>
public sealed class OpravneniSluzba(CiselnikyDbContext db) : IOpravneniSluzba
{
    private readonly Dictionary<int, OpravneniSnapshot> _zaPozadavek = [];

    public async Task<OpravneniSnapshot> SnapshotAsync(int osobaId, CancellationToken ct = default)
    {
        if (_zaPozadavek.TryGetValue(osobaId, out var hotovy)) return hotovy;

        var jeSuperadmin = await db.Superadmini.AnyAsync(s => s.OsobaId == osobaId, ct);

        var granty = await (
            from prirazeni in db.AuthzPrirazeni
            where prirazeni.OsobaId == osobaId && prirazeni.Aktivni
            join role in db.AuthzRole on prirazeni.RoleKod equals role.Kod
            where role.Aktivni
            join mapovani in db.AuthzRoleAkce on role.Kod equals mapovani.RoleKod
            join akce in db.AuthzAkce on mapovani.AkceKlic equals akce.Klic
            select new { prirazeni.Id, prirazeni.Rozsah, akce.Klic, akce.Uroven })
            .ToListAsync(ct);

        var vyberyCiselniku = await (
            from vyber in db.AuthzPrirazeniCiselnik
            join ciselnik in db.Ciselniky on vyber.CiselnikId equals ciselnik.Id
            join prirazeni in db.AuthzPrirazeni on vyber.PrirazeniId equals prirazeni.Id
            where prirazeni.OsobaId == osobaId && prirazeni.Aktivni
            select new { vyber.PrirazeniId, ciselnik.Kod })
            .ToListAsync(ct);

        var vsechnyKody = await db.Ciselniky.Where(c => c.Aktivni)
            .Select(c => c.Kod).ToListAsync(ct);

        var globalni = new HashSet<string>();
        var naCiselnik = new Dictionary<string, HashSet<string>>();

        foreach (var grant in granty)
        {
            if (grant.Uroven == UrovenAkce.Global)
            {
                globalni.Add(grant.Klic);
                continue;
            }

            var kody = grant.Rozsah == RozsahPrirazeni.Vse
                ? vsechnyKody
                : vyberyCiselniku.Where(v => v.PrirazeniId == grant.Id)
                                 .Select(v => v.Kod).ToList();

            foreach (var kod in kody)
            {
                if (!naCiselnik.TryGetValue(kod, out var mnozina))
                    naCiselnik[kod] = mnozina = [];
                mnozina.Add(grant.Klic);
            }
        }

        var snapshot = new OpravneniSnapshot(
            jeSuperadmin,
            globalni,
            naCiselnik.ToDictionary(x => x.Key, x => (IReadOnlySet<string>)x.Value));

        _zaPozadavek[osobaId] = snapshot;
        return snapshot;
    }
}
```

> **Rozsah `VSE` se rozvine na skutečné kódy**, ne na hvězdičku. Číselníků je řádově
> padesát a rozvinutí je jeden dotaz navíc; zato je projekce dál jedna datová struktura
> a nikde se nemusí větvit na „a co když je to všechno".

- [ ] **Krok 5: Ověř jednotkové testy a doplň integrační**

```bash
dotnet test Ciselniky.Tests.Unit --filter OpravneniSnapshotTests   # Passed: 5
```

`Ciselniky.Tests.Integration/Security/OpravneniSluzbaTests.cs` ověří nad skutečnou
databází tři případy: přiřazení s rozsahem `VYBER` uděluje jen na vyjmenované číselníky,
rozsah `VSE` na všechny aktivní, a deaktivovaná role neuděluje nic.

- [ ] **Krok 6: Zaregistruj a commitni**

```csharp
builder.Services.AddScoped<IOpravneniSluzba, OpravneniSluzba>();
```

```bash
dotnet test Ciselniky.Tests.Integration --filter OpravneniSluzbaTests
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(authz): projekce efektivních práv s datovým rozsahem"
```

---

## Blok 4: Vynucení oprávnění na koncových bodech

**Cíl bloku:** Chráněný koncový bod odmítne toho, kdo na něj nemá právo. Architektonický test
nedovolí, aby vznikl chráněný bod bez kontextu číselníku v adrese.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Security/OpravneniRequirement.cs`, `OpravneniHandler.cs`
- Vytvoř: `Ciselniky.Api/Security/OpravneniPolicyExtensions.cs`
- Test: `Ciselniky.Tests.Api/OpravneniVynuceniTests.cs`
- Test: `Ciselniky.Tests.Unit/Architektura/PolicyKontextTests.cs`

**Rozhraní:**
- Poskytuje: policy pojmenované `opravneni:{klic}` pro každý klíč z katalogu.
- Používá: `IOpravneniSluzba`, `ICurrentUserAccessor`.

- [ ] **Krok 1: Requirement a registrace policy**

`Ciselniky.Api/Security/OpravneniRequirement.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Ciselniky.Api.Security;

public sealed class OpravneniRequirement(string klic) : IAuthorizationRequirement
{
    public string Klic { get; } = klic;
}
```

`Ciselniky.Api/Security/OpravneniPolicyExtensions.cs`:

```csharp
using Ciselniky.Core.Security;
using Microsoft.AspNetCore.Authorization;

namespace Ciselniky.Api.Security;

public static class OpravneniPolicyExtensions
{
    public const string Prefix = "opravneni:";

    public static void PridejOpravneniPolicies(this AuthorizationOptions options)
    {
        foreach (var definice in KliceOpravneni.VsechnyDefinice)
            options.AddPolicy($"{Prefix}{definice.Klic}",
                policy => policy.Requirements.Add(new OpravneniRequirement(definice.Klic)));
    }
}
```

- [ ] **Krok 2: Handler**

`Ciselniky.Api/Security/OpravneniHandler.cs`:

```csharp
using Ciselniky.Core.Security;
using Microsoft.AspNetCore.Authorization;

namespace Ciselniky.Api.Security;

/// <summary>
/// Vyhodnotí <see cref="OpravneniRequirement"/> proti projekci práv.
/// Kód číselníku čte z adresy požadavku — <b>Route → Query</b>, v tomto pořadí.
/// </summary>
/// <remarks>
/// Tělo požadavku se záměrně nečte. V Zápisce to byla skutečná chyba: kontrola podle
/// hodnoty z těla jde obejít podvržením, a navíc si vynucuje čtení těla dřív,
/// než ho zpracuje samotná akce.
/// </remarks>
public sealed class OpravneniHandler(
    IOpravneniSluzba opravneni,
    ICurrentUserAccessor uzivatel,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OpravneniRequirement>
{
    public const string KlicKontextu = "kod";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OpravneniRequirement requirement)
    {
        var osobaId = uzivatel.OsobaId;
        if (osobaId is null) return;      // bez identity se neuspěje

        var http = httpContextAccessor.HttpContext;
        var ciselnikKod = PrectiKodCiselniku(http);

        var snapshot = await opravneni.SnapshotAsync(
            osobaId.Value, http?.RequestAborted ?? CancellationToken.None);

        if (snapshot.Ma(requirement.Klic, ciselnikKod))
            context.Succeed(requirement);
    }

    private static string? PrectiKodCiselniku(HttpContext? http)
    {
        if (http is null) return null;

        if (http.Request.RouteValues.TryGetValue(KlicKontextu, out var zRoute)
            && zRoute?.ToString() is { Length: > 0 } kodZRoute)
            return kodZRoute;

        if (http.Request.Query.TryGetValue(KlicKontextu, out var zQuery)
            && zQuery.ToString() is { Length: > 0 } kodZQuery)
            return kodZQuery;

        return null;
    }
}
```

- [ ] **Krok 3: Napiš architektonický test — nejdůležitější test celého P2**

`Ciselniky.Tests.Unit/Architektura/PolicyKontextTests.cs`:

```csharp
using System.Reflection;
using Ciselniky.Core.Domain.Authz;
using Ciselniky.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ciselniky.Tests.Unit.Architektura;

/// <summary>
/// Klíč vázaný na číselník potřebuje kód číselníku v adrese. Chybí-li tam,
/// handler provede tichou globální kontrolu a odmítne každého, kdo má právo
/// jen na výběr číselníků. V Zápisce to byla skutečná chyba a stálo to čas —
/// tenhle test ji nedovolí zopakovat.
/// </summary>
public sealed class PolicyKontextTests
{
    [Fact]
    public void KazdaAkceSKlicemNaCiselnik_MaKodVAdrese()
    {
        var naCiselnik = KliceOpravneni.VsechnyDefinice
            .Where(d => d.Uroven == UrovenAkce.Ciselnik)
            .Select(d => "opravneni:" + d.Klic)
            .ToHashSet();

        var prohresky = new List<string>();

        foreach (var typ in typeof(Ciselniky.Api.Security.OpravneniHandler).Assembly
                     .GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t)))
        {
            var cestaTypu = typ.GetCustomAttribute<RouteAttribute>()?.Template ?? "";

            foreach (var metoda in typ.GetMethods(BindingFlags.Public | BindingFlags.Instance
                                                  | BindingFlags.DeclaredOnly))
            {
                var policy = metoda.GetCustomAttribute<AuthorizeAttribute>()?.Policy;
                if (policy is null || !naCiselnik.Contains(policy)) continue;

                var cestaMetody = metoda.GetCustomAttributes()
                    .OfType<IRouteTemplateProvider>().FirstOrDefault()?.Template ?? "";

                if (!(cestaTypu + "/" + cestaMetody).Contains("{kod}"))
                    prohresky.Add($"{typ.Name}.{metoda.Name} → {policy}");
            }
        }

        Assert.True(prohresky.Count == 0,
            "Akce s klíčem vázaným na číselník bez {kod} v adrese:\n  "
            + string.Join("\n  ", prohresky));
    }
}
```

- [ ] **Krok 4: Spusť testy a ověř, že padají**

```bash
dotnet test Ciselniky.Tests.Unit --filter PolicyKontextTests
```

Očekávej: FAIL — `OpravneniHandler` neexistuje (kroky 1 a 2 ho zavedou).

- [ ] **Krok 5: Zaregistruj v Program.cs**

```csharp
builder.Services.AddAuthorization(options => options.PridejOpravneniPolicies());
builder.Services.AddScoped<IAuthorizationHandler, OpravneniHandler>();
```

- [ ] **Krok 6: Zkušební chráněný koncový bod a test vynucení**

Dočasný koncový bod v `CiselnikyController`, který blok 3 P3 nahradí skutečným:

```csharp
[HttpPost("internal/ciselniky/{kod}/zkouska")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.HodnotyEdit)]
public IActionResult Zkouska(string kod) => Ok(new { kod });
```

`Ciselniky.Tests.Api/OpravneniVynuceniTests.cs` ověří čtyři případy:

```csharp
[Fact] public async Task BezIdentity_Odmitne()                      // 401/403
[Fact] public async Task SPravemNaJinyCiselnik_Odmitne()            // 403
[Fact] public async Task SPravemNaTentoCiselnik_Projde()            // 200
[Fact] public async Task Superadmin_ProjdeVzdy()                    // 200
```

Druhý případ je ten podstatný: osoba **má** roli Editor, ale s rozsahem na jiný číselník.
Bez správně čteného kontextu by prošla.

- [ ] **Krok 7: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter OpravneniVynuceniTests   # Passed: 4
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(authz): policy handler s kontextem číselníku z adresy"
```

---

## Blok 5: Práva do prohlížeče

**Cíl bloku:** Frontend zná svá práva a nenabízí, co server odmítne.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/JaController.cs`
- Vytvoř: `ciselniky-web/src/api/ja.ts`, `src/lib/prava.ts`
- Test: `Ciselniky.Tests.Api/JaTests.cs`, `ciselniky-web/src/lib/prava.test.ts`

**Rozhraní:**
- Poskytuje: `GET /internal/ja`; v prohlížeči `smim(klic, ciselnikKod?) → boolean`.

- [ ] **Krok 1: Koncový bod**

```csharp
[HttpGet("internal/ja")]
[AllowAnonymous]
public async Task<IActionResult> Ja(CancellationToken ct)
{
    var osobaId = _uzivatel.OsobaId;
    if (osobaId is null)
        return Ok(new { osoba = (object?)null, prava = new Dictionary<string, object>() });

    var osoba = await _db.Osoby.FindAsync([osobaId.Value], ct);
    var snapshot = await _opravneni.SnapshotAsync(osobaId.Value, ct);

    var prava = KliceOpravneni.VsechnyDefinice
        .Select(d => (d.Klic, Rozsah: snapshot.RozsahProKlic(d.Klic)))
        .Where(x => x.Rozsah is null || x.Rozsah.Count > 0)
        .ToDictionary(x => x.Klic, x => x.Rozsah is null ? (object)"*" : x.Rozsah);

    return Ok(new { osoba = new { osoba!.Id, osoba.Jmeno, osoba.Prijmeni }, prava });
}
```

> `AllowAnonymous` je správně: **čtenář bez záznamu v aplikaci se sem musí dostat** a dozvědět
> se, že nemá žádná práva. Kdyby se odmítal, prohlížeč by neuměl vykreslit ani rozcestník.

> `"*"` znamená všechny číselníky. Vzniká u globálních klíčů a u superadmina.

- [ ] **Krok 2: Test odpovědi pro tři různé uživatele**

```csharp
[Fact] public async Task NeznamyUzivatel_DostanePrazdnaPrava()
[Fact] public async Task Editor_DostaneVycetSvychCiselniku()
[Fact] public async Task Superadmin_DostaneHvezdicku()
```

- [ ] **Krok 3: Pomocná funkce v prohlížeči**

`ciselniky-web/src/lib/prava.ts`:

```ts
export type Prava = Record<string, '*' | string[]>

export function smim(prava: Prava, klic: string, ciselnikKod?: string): boolean {
  const rozsah = prava[klic]
  if (rozsah === undefined) return false
  if (rozsah === '*') return true
  return ciselnikKod !== undefined && rozsah.includes(ciselnikKod)
}
```

Test `prava.test.ts` ověří: chybějící klíč → ne; `'*'` → ano i bez kódu;
výčet → ano jen na uvedený kód; výčet bez uvedení kódu → ne.

> **Tohle není kontrola oprávnění, je to nápověda pro rozhraní.** Server kontroluje vždy
> znovu. Obojí ale čte z téhož katalogu, takže se nemůžou rozejít v tom, jaké klíče existují.

- [ ] **Krok 4: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter JaTests
cd ciselniky-web && npx vitest run src/lib/prava.test.ts && cd ..
git add -A && git commit -m "feat(authz): efektivní práva do prohlížeče"
```

---

## Blok 6: Obrazovka Role uživatelů

**Cíl bloku:** Správce přidělí osobě roli s datovým rozsahem. Wireframe: O8.

**Soubory:**
- Vytvoř: `Ciselniky.Api/Controllers/Vnitrni/RoleController.cs`
- Vytvoř: `Ciselniky.Core/Services/Security/PrirazeniSluzba.cs`
- Vytvoř: `ciselniky-web/src/stranky/RoleUzivatelu.tsx`
- Test: `Ciselniky.Tests.Api/RoleControllerTests.cs`

- [ ] **Krok 1: Napiš padající testy služby**

Ověřované chování:

```csharp
[Fact] public async Task Pridelit_ZalozíPrirazeniSVyberem()
[Fact] public async Task Pridelit_TutezRoliPodruhe_Aktualizuje_NezalozíDruhe()
[Fact] public async Task Pridelit_RozsahVse_NeulozíVycet()
[Fact] public async Task Odebrat_Deaktivuje_Nemaze()
```

Třetí test drží pravidlo, že rozsah `VSE` a výčet číselníků se vylučují — uložený výčet
vedle rozsahu `VSE` by byl tichý nesoulad, který nikdo nikdy neuvidí.

- [ ] **Krok 2: Spusť, ověř pád, doplň službu**

`Ciselniky.Core/Services/Security/PrirazeniSluzba.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain.Authz;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Security;

public sealed record PrideleniPozadavek(
    int OsobaId, string RoleKod, RozsahPrirazeni Rozsah, int[] CiselnikIds);

public sealed class PrirazeniSluzba(CiselnikyDbContext db)
{
    public async Task PridelitAsync(PrideleniPozadavek pozadavek, int kdoId,
                                    CancellationToken ct = default)
    {
        var prirazeni = await db.AuthzPrirazeni.FirstOrDefaultAsync(
            p => p.OsobaId == pozadavek.OsobaId && p.RoleKod == pozadavek.RoleKod, ct);

        if (prirazeni is null)
        {
            prirazeni = new AuthzPrirazeni
            {
                OsobaId = pozadavek.OsobaId, RoleKod = pozadavek.RoleKod,
                Rozsah = pozadavek.Rozsah, PridelilId = kdoId
            };
            db.AuthzPrirazeni.Add(prirazeni);
            await db.SaveChangesAsync(ct);          // potřebujeme přidělené Id
        }
        else
        {
            prirazeni.Rozsah = pozadavek.Rozsah;
            prirazeni.Aktivni = true;
            prirazeni.PridelilId = kdoId;
        }

        // Výčet se vždy přepíše celý — je to jediný stav, ne přírůstek.
        db.AuthzPrirazeniCiselnik.RemoveRange(
            db.AuthzPrirazeniCiselnik.Where(v => v.PrirazeniId == prirazeni.Id));

        // Rozsah VSE a výčet se vylučují. Uložený výčet vedle rozsahu VSE by byl
        // tichý nesoulad, který nikdo nikdy neuvidí.
        if (pozadavek.Rozsah == RozsahPrirazeni.Vyber)
            foreach (var ciselnikId in pozadavek.CiselnikIds.Distinct())
                db.AuthzPrirazeniCiselnik.Add(new AuthzPrirazeniCiselnik
                {
                    PrirazeniId = prirazeni.Id, CiselnikId = ciselnikId
                });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deaktivuje přiřazení. <b>Nemaže</b> — historie musí zůstat dohledatelná.</summary>
    public async Task OdebratAsync(int prirazeniId, CancellationToken ct = default)
    {
        var prirazeni = await db.AuthzPrirazeni.FindAsync([prirazeniId], ct)
            ?? throw new InvalidOperationException($"Přiřazení {prirazeniId} neexistuje.");
        prirazeni.Aktivni = false;
        await db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Krok 3: Koncové body**

```csharp
[HttpGet("internal/nastaveni/role")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.NastaveniRoleAssign)]
public Task<IActionResult> Seznam(CancellationToken ct);

[HttpPut("internal/nastaveni/role")]
[Authorize(Policy = "opravneni:" + KliceOpravneni.NastaveniRoleAssign)]
public Task<IActionResult> Pridelit([FromBody] PrideleniPozadavek pozadavek, CancellationToken ct);
```

`nastaveni.role.assign` je **globální klíč**, takže kód číselníku v adrese nepotřebuje —
architektonický test z bloku 4 ho proto správně nehlídá.

- [ ] **Krok 4: Obrazovka podle wireframu O8**

`ciselniky-web/src/stranky/RoleUzivatelu.tsx` — jedna tabulka: osoba, role, datový rozsah.
Rozsah se zadává přepínačem *všechny číselníky* / *vybrané*; při druhé volbě se objeví
výběr číselníků.

```tsx
type Rozsah = 'VSE' | 'VYBER'

function VyberRozsahu(props: {
  rozsah: Rozsah
  vybrane: string[]
  ciselniky: { kod: string; nazev: string }[]
  onZmena: (rozsah: Rozsah, vybrane: string[]) => void
}) {
  const prazdnyVyber = props.rozsah === 'VYBER' && props.vybrane.length === 0
  return (
    <>
      <pm-radio-group
        hodnota={props.rozsah}
        onZmena={(r: Rozsah) => props.onZmena(r, props.vybrane)}
        moznosti={[
          { hodnota: 'VSE',   popisek: 'Všechny číselníky' },
          { hodnota: 'VYBER', popisek: 'Vybrané číselníky' },
        ]}
      />
      {props.rozsah === 'VYBER' && (
        <SeznamZaskrtavatek
          polozky={props.ciselniky}
          vybrane={props.vybrane}
          onZmena={(v) => props.onZmena('VYBER', v)}
        />
      )}
      {prazdnyVyber && (
        <gov-message color="warning">
          Role bez vybraného číselníku neuděluje žádné oprávnění.
        </gov-message>
      )}
    </>
  )
}
```

**Upozornění, které obrazovka musí ukázat:** je-li zvolen výběr a nezaškrtnut žádný
číselník, role neuděluje nic. Není to chyba a nebrání uložení — ale uživatel to má vědět,
protože jinak přidělil roli, která nefunguje, a nedozví se proč.

- [ ] **Krok 5: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter RoleControllerTests
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(authz): obrazovka přidělování rolí s datovým rozsahem"
```

---

## Blok 7: Obrazovka Efektivní práva

**Cíl bloku:** Správce vidí, co konkrétní člověk smí **a odkud to má**. Wireframe: O9.

**Soubory:**
- Vytvoř: `Ciselniky.Core/Services/Security/EfektivniPravaSluzba.cs`
- Uprav: `Ciselniky.Api/Controllers/Vnitrni/RoleController.cs`
- Vytvoř: `ciselniky-web/src/stranky/EfektivniPrava.tsx`
- Test: `Ciselniky.Tests.Integration/Security/EfektivniPravaTests.cs`

- [ ] **Krok 1: Napiš padající test**

```csharp
[Fact]
public async Task Prava_UvadejiZdrojGrantu()
{
    // osoba má EDITOR s výběrem "cile"
    var radky = await sluzba.ProOsobuAsync(osobaId, ct);

    var radek = radky.Single(r => r.Klic == KliceOpravneni.HodnotyEdit);
    Assert.Equal(["cile"], radek.Rozsah);
    Assert.Equal("role Editor", radek.Zdroj);
}

[Fact]
public async Task Superadmin_MaZdrojNouzovyKlic()
{
    var radky = await sluzba.ProOsobuAsync(superadminId, ct);

    Assert.All(radky, r => Assert.Equal("nouzový klíč (superadmin)", r.Zdroj));
}
```

- [ ] **Krok 2: Spusť, ověř pád, doplň službu**

`Ciselniky.Core/Services/Security/EfektivniPravaSluzba.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Services.Security;

public sealed record RadekPrav(string Klic, string Nazev, string[] Rozsah, string Zdroj);

public sealed class EfektivniPravaSluzba(CiselnikyDbContext db, IOpravneniSluzba opravneni)
{
    public async Task<IReadOnlyList<RadekPrav>> ProOsobuAsync(
        int osobaId, CancellationToken ct = default)
    {
        var snapshot = await opravneni.SnapshotAsync(osobaId, ct);

        // Který klíč přišel z které role. Při shodě rozhoduje první nalezená role —
        // dvě role udělující týž klíč dávají totéž oprávnění, liší se jen popiskem.
        var zdrojKlice = await (
            from prirazeni in db.AuthzPrirazeni
            where prirazeni.OsobaId == osobaId && prirazeni.Aktivni
            join role in db.AuthzRole on prirazeni.RoleKod equals role.Kod
            where role.Aktivni
            join mapovani in db.AuthzRoleAkce on role.Kod equals mapovani.RoleKod
            select new { mapovani.AkceKlic, role.Nazev })
            .ToListAsync(ct);

        var podleKlice = zdrojKlice
            .GroupBy(x => x.AkceKlic)
            .ToDictionary(g => g.Key, g => g.First().Nazev);

        return KliceOpravneni.VsechnyDefinice.Select(definice =>
        {
            var rozsah = snapshot.RozsahProKlic(definice.Klic);

            var zdroj = snapshot.JeSuperadmin ? "nouzový klíč (superadmin)"
                : podleKlice.TryGetValue(definice.Klic, out var nazevRole) ? $"role {nazevRole}"
                : "nemá";

            return new RadekPrav(definice.Klic, definice.Nazev,
                                 rozsah?.ToArray() ?? ["*"], zdroj);
        }).ToList();
    }
}
```

Sloupec **Zdroj** je celý smysl obrazovky — odpovídá na otázku „proč tohle smí".

> **Zdroj je připravený na další hodnotu.** Až se práva začnou brát z centrálního systému
> řízení přístupů (rozhodnutí D1), přibude jen další hodnota v tomto sloupci
> a obrazovka se nemění.

- [ ] **Krok 3: Obrazovka podle wireframu O9**

`ciselniky-web/src/stranky/EfektivniPrava.tsx` — tabulka akce · rozsah · zdroj.

```tsx
export function EfektivniPrava({ radky }: { radky: RadekPrav[] }) {
  return (
    <table className="prava">
      <thead>
        <tr><th>Akce</th><th>Rozsah</th><th>Zdroj</th></tr>
      </thead>
      <tbody>
        {radky.map((r) => (
          <tr key={r.klic} className={r.zdroj === 'nemá' ? 'prava-nema' : undefined}>
            <td><code>{r.klic}</code> {r.nazev}</td>
            <td>{r.zdroj === 'nemá' ? '—'
                 : r.rozsah[0] === '*' ? 'všechny číselníky'
                 : r.rozsah.join(', ')}</td>
            <td>{r.zdroj}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
```

Akce, kterou osoba nemá, se **zobrazuje se zdrojem „nemá"** — vynechaný řádek by nutil
uživatele hádat, jestli se na klíč zapomnělo, nebo ho osoba opravdu nemá.

- [ ] **Krok 4: Plná sada a commit**

```bash
dotnet test Ciselniky.sln
git add -A && git commit -m "feat(authz): obrazovka efektivních práv se zdrojem grantu"
```

---

## Po dokončení P2 ručně ověř

1. Superadmin ze seed skriptu se přihlásí a vidí obrazovku **Role uživatelů**.
2. Přidělí kolegovi roli **Editor** s rozsahem na jeden číselník.
3. Kolega vidí editační prvky **jen u toho jednoho číselníku**, jinde ne.
4. Na **Efektivních právech** je u kolegy vidět klíč, rozsah i zdroj *role Editor*.
5. Odebrání role kolegu okamžitě omezí — bez restartu aplikace.
6. `dotnet test Ciselniky.sln` — všechny vrstvy zeleně.

## Dluhy předávané dál

| # | Dluh | Uzavře |
|---|---|---|
| D1 | Tabulka `ciselniky` má zatím jen kód, název a příznak aktivity. Chybí definice struktury, režim správy a hierarchie. | **P3** |
| D2 | Zkušební koncový bod `internal/ciselniky/{kod}/zkouska` z bloku 4 je dočasný — nahradit skutečnou editací hodnot. | **P4** |
| D3 | Klíče pro zdroje (`zdroje.configure`, `zdroje.run`) v katalogu nejsou. | **etapa 2** |
