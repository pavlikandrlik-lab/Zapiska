# P1 — Základ a identita: plán implementace

> **Pro vývojáře:** implementuj **inline v hlavní session**, blok po bloku.
> Kroky používají zaškrtávací syntaxi `- [ ]`. **Subagenti na programování se nepoužívají.**

**Cíl:** Aplikace běží, doménový uživatel se přihlásí sám a dostane se na prázdný rozcestník
číselníků v gov designu. Databáze má schéma osob a kontrolní skript. Všechny čtyři testovací
vrstvy mají infrastrukturu a zelenou sadu.

**Architektura:** Dva projekty — `Ciselniky.Api` (tenké controllery, hostování SPA)
a `Ciselniky.Core` (doména, služby, data). React SPA se sestaví zvlášť a její výstup se
kopíruje do `wwwroot`. Identita se řeší v middleware **před** autorizační fází, aby
autorizační vrstva měla koho kontrolovat.

**Stack:** .NET 10, ASP.NET Core, EF Core + SQL Server, SQL Server, React + Vite + TypeScript,
xUnit, Playwright v .NET, gov design system 4.2.9.

**Specifikace:** [../10-specifikace/](../10-specifikace/) ·
**Architektura:** [../20-architektura/02-backend-vrstveni.md](../20-architektura/02-backend-vrstveni.md),
[../20-architektura/03-frontend-react.md](../20-architektura/03-frontend-react.md)

**Global Constraints:** [README.md — Global Constraints](README.md#global-constraints).
Platí pro každý blok, neopakují se.

## Přehled bloků

| Blok | Co bude fungovat po něm |
|---|---|
| 1 | Řešení se sestaví, prázdná testovací sada běží zeleně |
| 2 | Aplikace se připojí k SQL Server, ověří schéma a databáze má prvního superadmina |
| 3 | Přihlášený doménový uživatel je rozpoznán a přeložen na osobu |
| 4 | HTTP vrstva jde testovat s podvrženou identitou |
| 5 | React SPA se sestaví, běží a používá gov komponenty |
| 6 | Backend servíruje SPA i rozhraní z jedné adresy |
| 7 | Playwright projde aplikaci a ověří, že se rozcestník zobrazí |
| 8 | Dokumentace má kostru, aplikace má verzi 0.1 |

---

## Blok 1: Řešení a projekty

**Cíl bloku:** `dotnet build` projde s 0 chybami a 0 varováními, `dotnet test` najde
a spustí prázdnou sadu.

**Soubory:**
- Vytvoř: `Ciselniky.sln`, `Directory.Build.props`, `.editorconfig`, `.gitignore`
- Vytvoř: `Ciselniky.Api/Ciselniky.Api.csproj`, `Ciselniky.Core/Ciselniky.Core.csproj`
- Vytvoř: `Ciselniky.Tests.Unit/`, `Ciselniky.Tests.Integration/`, `Ciselniky.Tests.Api/`, `Ciselniky.Tests.E2E/`

**Rozhraní:**
- Poskytuje: kořenový jmenný prostor `Ciselniky`, projekt `Ciselniky.Core` referencovaný
  z `Ciselniky.Api` a ze všech testovacích projektů.

- [ ] **Krok 1: Založ řešení a projekty**

```bash
dotnet new sln -n Ciselniky
dotnet new webapi   -n Ciselniky.Api   -f net10.0
dotnet new classlib -n Ciselniky.Core  -f net10.0
dotnet new xunit -n Ciselniky.Tests.Unit        -f net10.0
dotnet new xunit -n Ciselniky.Tests.Integration -f net10.0
dotnet new xunit -n Ciselniky.Tests.Api         -f net10.0
dotnet new xunit -n Ciselniky.Tests.E2E         -f net10.0

dotnet sln add Ciselniky.Api Ciselniky.Core Ciselniky.Tests.*
dotnet add Ciselniky.Api reference Ciselniky.Core
for t in Unit Integration Api E2E; do dotnet add Ciselniky.Tests.$t reference Ciselniky.Core; done
dotnet add Ciselniky.Tests.Api reference Ciselniky.Api
dotnet add Ciselniky.Tests.E2E reference Ciselniky.Api

# balíčky
dotnet add Ciselniky.Core package Microsoft.EntityFrameworkCore
dotnet add Ciselniky.Core package Microsoft.EntityFrameworkCore.SqlServer
dotnet add Ciselniky.Tests.Unit        package Microsoft.EntityFrameworkCore.InMemory
dotnet add Ciselniky.Tests.Api         package Microsoft.AspNetCore.Mvc.Testing
dotnet add Ciselniky.Tests.E2E         package Microsoft.AspNetCore.Mvc.Testing
dotnet add Ciselniky.Tests.E2E         package Microsoft.Playwright
```

> **Testovací sada je xUnit v3.** Plán používá `TestContext.Current.CancellationToken`
> a `IAsyncLifetime` s návratovým typem `ValueTask` — obojí je API verze 3.
> Ověř `dotnet list package | grep -i xunit`; je-li tam v2, přejdi na `xunit.v3`,
> jinak se testy nepřeloží.

- [ ] **Krok 2: Sdílené nastavení sestavení**

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <InvariantGlobalization>false</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

`TreatWarningsAsErrors` je záměrné — Global Constraints vyžadují 0 varování,
a pravidlo, které nevynucuje nástroj, se do měsíce přestane dodržovat.

- [ ] **Krok 3: Test hlídající velikost souborů**

`Ciselniky.Tests.Unit/Architektura/VelikostSouboruTests.cs`:

```csharp
namespace Ciselniky.Tests.Unit.Architektura;

public sealed class VelikostSouboruTests
{
    private const int MaxRadku = 400;

    [Fact]
    public void ZadnyZdrojovySoubor_NepresahujeLimitRadku()
    {
        var korenRepozitare = NajdiKorenRepozitare();
        var prekrocene = Directory
            .EnumerateFiles(korenRepozitare, "*.cs", SearchOption.AllDirectories)
            .Where(cesta => !cesta.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !cesta.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(cesta => (cesta, radku: File.ReadAllLines(cesta).Length))
            .Where(x => x.radku > MaxRadku)
            .ToList();

        Assert.True(prekrocene.Count == 0,
            "Soubory přes limit " + MaxRadku + " řádků:\n"
            + string.Join("\n", prekrocene.Select(x => $"  {x.radku,5}  {x.cesta}")));
    }

    private static string NajdiKorenRepozitare()
    {
        var adresar = new DirectoryInfo(AppContext.BaseDirectory);
        while (adresar is not null && !File.Exists(Path.Combine(adresar.FullName, "Ciselniky.sln")))
            adresar = adresar.Parent;
        return adresar?.FullName ?? throw new InvalidOperationException("Ciselniky.sln nenalezen.");
    }
}
```

- [ ] **Krok 4: Ověř, že sestavení i sada projdou**

```bash
dotnet build Ciselniky.sln
dotnet test  Ciselniky.Tests.Unit
```

Očekávej: build **0 Warning(s), 0 Error(s)**; testy **Passed: 1**.

- [ ] **Krok 5: Commit**

```bash
git add -A && git commit -m "chore(zaklad): založit řešení, projekty a sdílené nastavení sestavení"
```

---

## Blok 2: Databáze a schéma osob

**Cíl bloku:** Aplikace se připojí k SQL Server a při startu ověří, že schéma odpovídá
tomu, co kód očekává. Chybějící tabulka aplikaci zastaví se srozumitelnou hláškou,
ne až prvním dotazem za provozu.

**Soubory:**
- Vytvoř: `db/db_baseline_0_1.sql`, `db/db_check_applied_upgrades.sql`, `db/db_seed_prvni_superadmin.sql`
- Vytvoř: `Ciselniky.Core/Domain/Osoba.cs`, `Ciselniky.Core/Data/CiselnikyDbContext.cs`
- Vytvoř: `Ciselniky.Core/Data/KontrolaSchematu.cs`
- Vytvoř: `Ciselniky.Tests.Integration/TestInfrastructure/SqlFixture.cs`
- Uprav: `Ciselniky.Api/Program.cs`
- Test: `Ciselniky.Tests.Integration/Data/KontrolaSchematuTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyDbContext` s `DbSet<Osoba> Osoby`;
  `KontrolaSchematu.OverAsync(DbContext, CancellationToken) → Task` (vyhodí
  `InvalidOperationException` s výčtem chybějících objektů).

- [ ] **Krok 1: Baseline schéma**

`db/db_baseline_0_1.sql`:

```sql
-- Baseline schéma aplikace Číselníky, verze 0.1
-- Sloupce výhradně ASCII a snake_case. Diakritika v názvech se nepoužívá.

CREATE TABLE osoby (
    id                 int      IDENTITY(1,1) PRIMARY KEY,
    ad_guid            uniqueidentifier         NOT NULL UNIQUE,
    login              nvarchar(128) NOT NULL UNIQUE,
    jmeno              nvarchar(128) NOT NULL,
    prijmeni           nvarchar(128) NOT NULL,
    email              nvarchar(256),
    osobni_cislo       nvarchar(32),
    aktivni            bit      NOT NULL DEFAULT 1,
    zalozeno_kdy       datetimeoffset  NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX ix_osoby_login ON osoby (lower(login));

-- Nouzový klíč. Kdo je zde uveden, projde každou kontrolou oprávnění.
-- Stojí ZÁMĚRNĚ mimo tabulky rolí — kdyby na nich závisel, nefungoval by
-- právě tehdy, když je potřeba, tedy při porušených datech o oprávněních.
CREATE TABLE superadmini (
    osoba_id      int     PRIMARY KEY REFERENCES osoby(id),
    poznamka      nvarchar(256),
    zalozeno_kdy  datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
    zalozil_id    int     REFERENCES osoby(id)
);

CREATE TABLE aplikovane_upgrady (
    verze        nvarchar(32) PRIMARY KEY,
    aplikovano   datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME()
);

INSERT INTO aplikovane_upgrady (verze) VALUES ('0.1');
```

> `ad_guid` je technický identifikátor z Active Directory. `login` slouží jako záložní
> cesta pro prostředí, kde server zná přihlašovací jméno, ale ne identifikátor —
> proto index nad `lower(login)`.
>
> `osobni_cislo` je volitelný údaj z Active Directory. V etapě 1 se nevyplňuje;
> sloupec existuje, aby se nemusel doplňovat migrací, až bude potřeba.

- [ ] **Krok 2: Kontrolní skript stavu instance**

`db/db_check_applied_upgrades.sql`:

```sql
SELECT verze, aplikovano FROM aplikovane_upgrady ORDER BY verze;
```

- [ ] **Krok 3: Skript zakládající prvního superadmina**

Bez něj se do čerstvě nasazené aplikace nikdo nedostane: každý přihlášený je čtenář
a nikdo nemůže přidělit první roli. Nasazení je pouhé zkopírování souborů na IIS,
takže **jediné místo, kde se dá první správce vytvořit, je databáze** — a ta se stejně
zakládá ručním spuštěním skriptů.

`db/db_seed_prvni_superadmin.sql`:

```sql
-- Zakládá prvního správce čerstvě nasazené aplikace.
-- PŘED SPUŠTĚNÍM VYPLŇ tři hodnoty níže. Skript jde spustit opakovaně.

DECLARE @login    nvarchar(128) = N'FIS\novak';
DECLARE @jmeno    nvarchar(128) = N'Jan';
DECLARE @prijmeni nvarchar(128) = N'Novák';

IF NOT EXISTS (SELECT 1 FROM osoby WHERE login = @login)
    INSERT INTO osoby (ad_guid, login, jmeno, prijmeni)
    VALUES (NEWID(), @login, @jmeno, @prijmeni);

DECLARE @osoba int = (SELECT id FROM osoby WHERE login = @login);

IF NOT EXISTS (SELECT 1 FROM superadmini WHERE osoba_id = @osoba)
    INSERT INTO superadmini (osoba_id, poznamka, zalozil_id)
    VALUES (@osoba, N'První správce, založen při zakládání databáze.', @osoba);

SELECT o.id, o.login, o.jmeno, o.prijmeni
  FROM osoby o JOIN superadmini s ON s.osoba_id = o.id;
```

> `NEWID()` vyrobí náhodný identifikátor. Skutečný identifikátor z Active Directory
> se u ručně zakládaného správce nezná — doplní se, až se osoba spáruje s doménou.
>
> **Všechny textové literály mají předponu `N`.** Bez ní se „Novák" uloží zkomoleně.

> **Provozní pravidlo:** aspoň dva aktivní superadmini a nikdy sdílené účty.
> Superadmin je nouzový klíč, ne role k běžné práci — na to je role *Správce aplikace*.

- [ ] **Krok 4: Fixture pro SQL Server**

`Ciselniky.Tests.Integration/TestInfrastructure/SqlFixture.cs`:

```csharp
using Ciselniky.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Tests.Integration.TestInfrastructure;

/// <summary>
/// Lokální instance SQL Server (rozhodnutí T2 — rychlejší než kontejner na test).
/// Každý běh dostane vlastní databázi, aby na sebe testy nenavazovaly.
/// </summary>
public sealed class SqlFixture : IAsyncLifetime
{
    private readonly string _nazevDb = "ciselniky_test_" + Guid.NewGuid().ToString("N")[..12];
    private string _pripojeni = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var zaklad = Environment.GetEnvironmentVariable("CISELNIKY_TEST_DB")
                     ?? "Server=localhost;Integrated Security=true;TrustServerCertificate=true";

        await using (var sprava = new Microsoft.Data.SqlClient.SqlConnection($"{zaklad};Database=master"))
        {
            await sprava.OpenAsync();
            await using var prikaz = sprava.CreateCommand();
            prikaz.CommandText = $"CREATE DATABASE [{_nazevDb}]";
            await prikaz.ExecuteNonQueryAsync();
        }

        _pripojeni = $"{zaklad};Database={_nazevDb}";   // u SQL Serveru „Database", ne „Initial Catalog" — obojí platí

        await using var db = VytvorContext();
        var baseline = await File.ReadAllTextAsync(NajdiSkript("db_baseline_0_1.sql"));
        await db.Database.ExecuteSqlRawAsync(baseline);
    }

    public CiselnikyDbContext VytvorContext() => new(
        new DbContextOptionsBuilder<CiselnikyDbContext>().UseSqlServer(_pripojeni).Options);

    public async ValueTask DisposeAsync()
    {
        var zaklad = _pripojeni[.._pripojeni.LastIndexOf(";Database=", StringComparison.Ordinal)];
        await using var sprava = new Microsoft.Data.SqlClient.SqlConnection($"{zaklad};Database=master");
        await sprava.OpenAsync();
        await using var prikaz = sprava.CreateCommand();
        prikaz.CommandText = $"""
            IF DB_ID('{_nazevDb}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{_nazevDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{_nazevDb}];
            END
            """;
        await prikaz.ExecuteNonQueryAsync();
    }

    private static string NajdiSkript(string nazev)
    {
        var adresar = new DirectoryInfo(AppContext.BaseDirectory);
        while (adresar is not null && !Directory.Exists(Path.Combine(adresar.FullName, "db")))
            adresar = adresar.Parent;
        return Path.Combine(adresar!.FullName, "db", nazev);
    }
}
```

- [ ] **Krok 5: Napiš padající test kontroly schématu**

`Ciselniky.Tests.Integration/Data/KontrolaSchematuTests.cs`:

```csharp
using Ciselniky.Core.Data;
using Ciselniky.Tests.Integration.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Tests.Integration.Data;

public sealed class KontrolaSchematuTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Kontrola_ProjdeNaBaselineSchematu()
    {
        await using var db = fixture.VytvorContext();
        await KontrolaSchematu.OverAsync(db, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Kontrola_ChybejiciTabulku_NahlasiJmenem()
    {
        await using var db = fixture.VytvorContext();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS osoby CASCADE;");

        var vyjimka = await Assert.ThrowsAsync<InvalidOperationException>(
            () => KontrolaSchematu.OverAsync(db, TestContext.Current.CancellationToken));

        Assert.Contains("osoby", vyjimka.Message);
    }
}
```

- [ ] **Krok 6: Spusť test a ověř, že padá**

```bash
dotnet test Ciselniky.Tests.Integration --filter KontrolaSchematuTests
```

Očekávej: FAIL — `KontrolaSchematu` neexistuje.

- [ ] **Krok 7: Doplň entitu, kontext a kontrolu schématu**

`Ciselniky.Core/Domain/Osoba.cs`:

```csharp
namespace Ciselniky.Core.Domain;

public sealed class Osoba
{
    public int Id { get; set; }
    public Guid AdGuid { get; set; }
    public required string Login { get; set; }
    public required string Jmeno { get; set; }
    public required string Prijmeni { get; set; }
    public string? Email { get; set; }
    public string? OsobniCislo { get; set; }
    public bool Aktivni { get; set; } = true;
    public DateTimeOffset ZalozenoKdy { get; set; }
}
```

`Ciselniky.Core/Data/CiselnikyDbContext.cs`:

```csharp
using Ciselniky.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Data;

public sealed class CiselnikyDbContext(DbContextOptions<CiselnikyDbContext> options) : DbContext(options)
{
    public DbSet<Osoba> Osoby => Set<Osoba>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var osoba = model.Entity<Osoba>();
        osoba.ToTable("osoby");
        osoba.Property(o => o.Id).HasColumnName("id");
        osoba.Property(o => o.AdGuid).HasColumnName("ad_guid");
        osoba.Property(o => o.Login).HasColumnName("login");
        osoba.Property(o => o.Jmeno).HasColumnName("jmeno");
        osoba.Property(o => o.Prijmeni).HasColumnName("prijmeni");
        osoba.Property(o => o.Email).HasColumnName("email");
        osoba.Property(o => o.OsobniCislo).HasColumnName("osobni_cislo");
        osoba.Property(o => o.Aktivni).HasColumnName("aktivni");
        osoba.Property(o => o.ZalozenoKdy).HasColumnName("zalozeno_kdy");
    }
}
```

`Ciselniky.Core/Data/KontrolaSchematu.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Data;

/// <summary>
/// Ověří při startu, že databáze obsahuje objekty, které kód očekává.
/// Chybějící objekt zastaví aplikaci se srozumitelnou hláškou — lepší než pád
/// prvním dotazem za provozu.
/// </summary>
public static class KontrolaSchematu
{
    private static readonly string[] OcekavaneTabulky = ["osoby", "superadmini", "aplikovane_upgrady"];

    public static async Task OverAsync(DbContext db, CancellationToken ct = default)
    {
        var pripojeni = db.Database.GetDbConnection();
        await using var prikaz = pripojeni.CreateCommand();
        prikaz.CommandText = """
            SELECT table_name FROM information_schema.tables
             WHERE table_schema = 'public'
            """;

        if (pripojeni.State != System.Data.ConnectionState.Open)
            await pripojeni.OpenAsync(ct);

        var nalezene = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var ctecka = await prikaz.ExecuteReaderAsync(ct))
            while (await ctecka.ReadAsync(ct))
                nalezene.Add(ctecka.GetString(0));

        var chybejici = OcekavaneTabulky.Where(t => !nalezene.Contains(t)).ToArray();
        if (chybejici.Length > 0)
            throw new InvalidOperationException(
                "Databáze neodpovídá očekávanému schématu. Chybí: "
                + string.Join(", ", chybejici)
                + ". Aplikuj skripty z adresáře db/ v pořadí verzí.");
    }
}
```

- [ ] **Krok 8: Zaregistruj kontext a kontrolu při startu**

`Ciselniky.Api/Program.cs` — doplň před `app.Run()`:

```csharp
builder.Services.AddDbContext<CiselnikyDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("CiselnikyDb")));

// …

await using (var rozsah = app.Services.CreateAsyncScope())
{
    var db = rozsah.ServiceProvider.GetRequiredService<CiselnikyDbContext>();
    await KontrolaSchematu.OverAsync(db);
}
```

- [ ] **Krok 9: Spusť test a ověř, že prochází**

```bash
dotnet test Ciselniky.Tests.Integration --filter KontrolaSchematuTests
```

Očekávej: **Passed: 2**.

- [ ] **Krok 10: Plná sada a commit**

```bash
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(data): baseline schéma osob, DbContext a startovní kontrola schématu"
```

---

## Blok 3: Identita přihlášeného uživatele

**Cíl bloku:** Přihlášený doménový uživatel je rozpoznán a přeložen na osobu.
**Uživatel bez záznamu v databázi se nesmí odmítnout** — projde jako anonymní čtenář
(rozhodnutí N1).

**Soubory:**
- Vytvoř: `Ciselniky.Core/Security/ICurrentUserAccessor.cs`, `CurrentUserAccessor.cs`
- Vytvoř: `Ciselniky.Core/Security/IUserContextResolver.cs`, `UserContextResolver.cs`
- Vytvoř: `Ciselniky.Api/Middleware/UserContextMiddleware.cs`
- Uprav: `Ciselniky.Api/Program.cs`
- Test: `Ciselniky.Tests.Unit/Security/UserContextResolverTests.cs`

**Rozhraní:**
- Poskytuje: `ICurrentUserAccessor.OsobaId → int?` (scoped),
  `IUserContextResolver.ResolveAsync(HttpContext, CancellationToken) → Task<int?>`.
- Používá: `CiselnikyDbContext` z bloku 2.

> **Převzato ze Zápisky** (`PmTracker.Web/Middleware/UserContextMiddleware.cs`,
> `Services/Security/CurrentUserAccessor.cs`) a zjednodušeno.
> **Co bylo vypuštěno a proč:** Zápiskový `UserContextResolver` má 742 řádků, protože
> nese vývojový přepínač identity, rozlišování stavových kódů 401 a 403 a projektový
> kontext. Číselníky nic z toho nepotřebují — neznámý uživatel se neodmítá, jen nemá
> osobu. Zbývá překlad identity na osobu.

- [ ] **Krok 1: Napiš padající testy**

`Ciselniky.Tests.Unit/Security/UserContextResolverTests.cs`:

```csharp
using System.Security.Claims;
using Ciselniky.Core.Data;
using Ciselniky.Core.Domain;
using Ciselniky.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Tests.Unit.Security;

public sealed class UserContextResolverTests
{
    private static CiselnikyDbContext VytvorPametovouDb(params Osoba[] osoby)
    {
        var options = new DbContextOptionsBuilder<CiselnikyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new CiselnikyDbContext(options);
        db.Osoby.AddRange(osoby);
        db.SaveChanges();
        return db;
    }

    private static HttpContext KontextSLoginem(string? login)
    {
        var kontext = new DefaultHttpContext();
        if (login is not null)
            kontext.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.Name, login)], "Test"));
        return kontext;
    }

    [Fact]
    public async Task ZnamyLogin_VratiIdOsoby()
    {
        await using var db = VytvorPametovouDb(new Osoba
        {
            Id = 7, Login = @"FIS\novak", Jmeno = "Jan", Prijmeni = "Novák"
        });
        var resolver = new UserContextResolver(db);

        var id = await resolver.ResolveAsync(KontextSLoginem(@"FIS\novak"), TestContext.Current.CancellationToken);

        Assert.Equal(7, id);
    }

    [Fact]
    public async Task LoginJinouVelikostiPismen_VratiIdOsoby()
    {
        await using var db = VytvorPametovouDb(new Osoba
        {
            Id = 7, Login = @"FIS\novak", Jmeno = "Jan", Prijmeni = "Novák"
        });
        var resolver = new UserContextResolver(db);

        var id = await resolver.ResolveAsync(KontextSLoginem(@"fis\NOVAK"), TestContext.Current.CancellationToken);

        Assert.Equal(7, id);
    }

    [Fact]
    public async Task NeznamyLogin_VratiNull_ANeodmitne()
    {
        await using var db = VytvorPametovouDb();
        var resolver = new UserContextResolver(db);

        var id = await resolver.ResolveAsync(KontextSLoginem(@"FIS\cizi"), TestContext.Current.CancellationToken);

        Assert.Null(id);
    }

    [Fact]
    public async Task NeaktivniOsoba_VratiNull()
    {
        await using var db = VytvorPametovouDb(new Osoba
        {
            Id = 7, Login = @"FIS\novak", Jmeno = "Jan", Prijmeni = "Novák", Aktivni = false
        });
        var resolver = new UserContextResolver(db);

        var id = await resolver.ResolveAsync(KontextSLoginem(@"FIS\novak"), TestContext.Current.CancellationToken);

        Assert.Null(id);
    }
}
```

> Test `NeznamyLogin_VratiNull_ANeodmitne` je ten podstatný. Zápiska na tomto místě
> uživatele odmítá; Číselníky nesmí. Kdyby se sem někdy vrátila zápisková logika,
> tenhle test spadne.

- [ ] **Krok 2: Spusť testy a ověř, že padají**

```bash
dotnet test Ciselniky.Tests.Unit --filter UserContextResolverTests
```

Očekávej: FAIL — `UserContextResolver` neexistuje.

- [ ] **Krok 3: Doplň přístup k aktuálnímu uživateli**

`Ciselniky.Core/Security/ICurrentUserAccessor.cs`:

```csharp
namespace Ciselniky.Core.Security;

/// <summary>
/// Aktuálně přihlášená osoba. Vrací <c>null</c>, pokud přihlášený doménový uživatel
/// nemá v aplikaci záznam — což je platný stav, ne chyba: takový uživatel smí číst.
/// </summary>
public interface ICurrentUserAccessor
{
    int? OsobaId { get; }
}
```

`Ciselniky.Core/Security/CurrentUserAccessor.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace Ciselniky.Core.Security;

public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public const string KlicVKontextu = "ciselniky.identita.osobaId";

    public int? OsobaId =>
        httpContextAccessor.HttpContext?.Items.TryGetValue(KlicVKontextu, out var hodnota) == true
        && hodnota is int osobaId
            ? osobaId
            : null;
}
```

- [ ] **Krok 4: Doplň resolver**

`Ciselniky.Core/Security/IUserContextResolver.cs`:

```csharp
using Microsoft.AspNetCore.Http;

namespace Ciselniky.Core.Security;

public interface IUserContextResolver
{
    Task<int?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
}
```

`Ciselniky.Core/Security/UserContextResolver.cs`:

```csharp
using Ciselniky.Core.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ciselniky.Core.Security;

/// <summary>
/// Překládá přihlášenou doménovou identitu na osobu v aplikaci.
/// Neznámá identita <b>není chyba</b> — vrací <c>null</c> a uživatel projde jako čtenář.
/// </summary>
public sealed class UserContextResolver(CiselnikyDbContext db) : IUserContextResolver
{
    public async Task<int?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var login = httpContext.User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(login)) return null;

        var normalizovany = login.Trim().ToLowerInvariant();

        return await db.Osoby
            .Where(o => o.Aktivni && o.Login.ToLower() == normalizovany)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync(ct);
    }
}
```

- [ ] **Krok 5: Doplň middleware**

`Ciselniky.Api/Middleware/UserContextMiddleware.cs`:

```csharp
using Ciselniky.Core.Security;

namespace Ciselniky.Api.Middleware;

/// <summary>
/// Naplní identitu osoby do <c>HttpContext.Items</c> <b>před</b> autorizační fází.
/// </summary>
/// <remarks>
/// Pořadí je podstatné a v Zápisce to byla skutečná chyba: dokud se identita plnila
/// až v akčním filtru, běželo to <b>po</b> <c>UseAuthorization()</c> a každá chráněná
/// akce skončila odmítnutím, protože autorizace neměla koho kontrolovat.
/// </remarks>
public sealed class UserContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserContextResolver resolver)
    {
        if (!string.IsNullOrWhiteSpace(context.User.Identity?.Name))
        {
            var osobaId = await resolver.ResolveAsync(context, context.RequestAborted);
            if (osobaId is not null)
                context.Items[CurrentUserAccessor.KlicVKontextu] = osobaId.Value;
        }

        await next(context);
    }
}
```

- [ ] **Krok 6: Zaregistruj v Program.cs ve správném pořadí**

```csharp
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextResolver, UserContextResolver>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

// …

app.UseAuthentication();
app.UseMiddleware<UserContextMiddleware>();   // MUSÍ být před UseAuthorization
app.UseAuthorization();
```

- [ ] **Krok 7: Spusť testy a ověř, že prochází**

```bash
dotnet test Ciselniky.Tests.Unit --filter UserContextResolverTests
```

Očekávej: **Passed: 4**.

- [ ] **Krok 8: Plná sada a commit**

```bash
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "feat(identita): překlad doménové identity na osobu v middleware před autorizací"
```

---

## Blok 4: Testovací infrastruktura HTTP vrstvy

**Cíl bloku:** HTTP vrstva jde testovat s podvrženou identitou i jako anonymní uživatel.

**Soubory:**
- Vytvoř: `Ciselniky.Tests.Api/TestInfrastructure/CiselnikyWebAppFactory.cs`
- Vytvoř: `Ciselniky.Tests.Api/TestInfrastructure/TestAuthHandler.cs`
- Test: `Ciselniky.Tests.Api/ZdraviTests.cs`

**Rozhraní:**
- Poskytuje: `CiselnikyWebAppFactory.VytvorKlienta(string? login = null) → HttpClient`.
- Používá: `SqlFixture` z bloku 2.

> **Převzato ze Zápisky** (`PmTracker.Tests.Api/TestInfrastructure/TestAuthHandler.cs`).
> Podstata: bez úspěšné autentizace by autorizační vrstva vracela výzvu k přihlášení
> místo odmítnutí, a testy ověřující odmítnutí by selhaly na špatném stavovém kódu.

- [ ] **Krok 1: Podvržená autentizace**

`Ciselniky.Tests.Api/TestInfrastructure/TestAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ciselniky.Tests.Api.TestInfrastructure;

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CiselnikyTestAuth";
    public const string HlavickaIdentity = "X-Test-Login";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var login = Request.Headers[HlavickaIdentity].ToString();
        if (string.IsNullOrWhiteSpace(login))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identita = new ClaimsIdentity(SchemeName);
        identita.AddClaim(new Claim(ClaimTypes.Name, login));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identita), SchemeName)));
    }
}
```

- [ ] **Krok 2: Továrna na aplikaci**

`Ciselniky.Tests.Api/TestInfrastructure/CiselnikyWebAppFactory.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ciselniky.Tests.Api.TestInfrastructure;

public sealed class CiselnikyWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });
        });
    }

    /// <param name="login">Doménový login, nebo <c>null</c> pro anonymní požadavek.</param>
    public HttpClient VytvorKlienta(string? login = null)
    {
        var klient = CreateClient();
        if (login is not null)
            klient.DefaultRequestHeaders.Add(TestAuthHandler.HlavickaIdentity, login);
        return klient;
    }
}
```

`Program.cs` musí být pro továrnu viditelný — na jeho konec doplň:

```csharp
public partial class Program;
```

- [ ] **Krok 3: Napiš padající test zdravotní kontroly**

`Ciselniky.Tests.Api/ZdraviTests.cs`:

```csharp
using System.Net;
using Ciselniky.Tests.Api.TestInfrastructure;

namespace Ciselniky.Tests.Api;

public sealed class ZdraviTests(CiselnikyWebAppFactory factory) : IClassFixture<CiselnikyWebAppFactory>
{
    [Fact]
    public async Task Zdravi_JeDostupne_IBezPrihlaseni()
    {
        var klient = factory.VytvorKlienta();

        var odpoved = await klient.GetAsync("/zdravi", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, odpoved.StatusCode);
    }

    [Fact]
    public async Task Zdravi_ProPrihlasenehoUzivatele_TakeProjde()
    {
        var klient = factory.VytvorKlienta(@"FIS\novak");

        var odpoved = await klient.GetAsync("/zdravi", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, odpoved.StatusCode);
    }
}
```

- [ ] **Krok 4: Spusť test a ověř, že padá**

```bash
dotnet test Ciselniky.Tests.Api --filter ZdraviTests
```

Očekávej: FAIL — `/zdravi` vrací 404.

- [ ] **Krok 5: Doplň koncový bod**

V `Ciselniky.Api/Program.cs`:

```csharp
app.MapGet("/zdravi", () => Results.Ok(new { stav = "bezi" })).AllowAnonymous();
```

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api --filter ZdraviTests   # Passed: 2
dotnet test Ciselniky.Tests.Unit
git add -A && git commit -m "test(infra): továrna aplikace, podvržená identita a fixture SQL Server"
```

---

## Blok 5: React SPA a gov design system

**Cíl bloku:** `npm run dev` zobrazí prázdný rozcestník v gov designu s přepínačem
světlého a tmavého režimu. Žádný požadavek nejde ven ze sítě.

**Soubory:**
- Vytvoř: `ciselniky-web/` (Vite + React + TypeScript)
- Vytvoř: `ciselniky-web/public/lib/gov-design-system/**`, `public/assets/icons/components/**`
- Vytvoř: `ciselniky-web/src/komponenty/pm/PmButton.tsx`, `PmIcon.tsx`, `PmAlert.tsx`
- Vytvoř: `ciselniky-web/src/stranky/SeznamCiselniku.tsx`, `src/App.tsx`
- Test: `ciselniky-web/src/komponenty/pm/PmButton.test.tsx`

- [ ] **Krok 1: Založ projekt a stáhni knihovny na stroji s internetem**

```bash
npm create vite@latest ciselniky-web -- --template react-ts
cd ciselniky-web && npm ci
npm i -D vitest @testing-library/react @testing-library/jest-dom jsdom
npm i react-router-dom @tanstack/react-query

# gov design system — offline kopie do repozitáře
mkdir -p /tmp/govds && cd /tmp/govds
npm pack @gov-design-system-ce/components@4.2.9
npm pack @gov-design-system-ce/styles@4.2.7
npm pack bootstrap-icons@1.11.3
for f in *.tgz; do tar -xzf "$f" && mv package "${f%%-[0-9]*}"; done
```

Zkopíruj do repozitáře podle
[../30-prevzate-moduly/05-gov-design-system.md](../30-prevzate-moduly/05-gov-design-system.md):

```
ciselniky-web/public/lib/gov-design-system/dist/core/     (91 souborů)
ciselniky-web/public/lib/gov-design-system/styles/lib/
ciselniky-web/public/assets/icons/components/             (vybrané SVG)
```

- [ ] **Krok 2: Zapoj gov design system do stránky**

`ciselniky-web/index.html` — do `<head>`:

```html
<link rel="stylesheet" href="/lib/gov-design-system/styles/lib/tokens.min.css" />
<link rel="stylesheet" href="/lib/gov-design-system/dist/core/core.min.css" />
<script type="module" src="/lib/gov-design-system/dist/core/core.esm.min.js"></script>
```

Jen modulová varianta — non-module `core.js` verze 4.2.9 **neexistuje**, Stencil ji negeneruje.

- [ ] **Krok 3: Napiš padající test wrapperu**

`ciselniky-web/src/komponenty/pm/PmButton.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react'
import { PmButton } from './PmButton'

test('renderuje gov-button s popiskem a variantou', () => {
  render(<PmButton varianta="primary">Uložit změny</PmButton>)

  const tlacitko = screen.getByText('Uložit změny').closest('gov-button')
  expect(tlacitko).not.toBeNull()
  expect(tlacitko!.getAttribute('type')).toBe('solid')
  expect(tlacitko!.getAttribute('color')).toBe('primary')
})

test('popisek se v DOM vyskytuje právě jednou', () => {
  render(<PmButton varianta="primary">Uložit změny</PmButton>)

  expect(screen.getAllByText('Uložit změny')).toHaveLength(1)
})
```

> Druhý test není nadbytečný. V Zápisce se popisek zdvojoval, když se na hostu
> `gov-button` sahalo na `textContent` — přemístění slotu se tím rozbije.
> Test to zachytí dřív, než to uvidí uživatel.

- [ ] **Krok 4: Spusť a ověř, že padá**

```bash
cd ciselniky-web && npx vitest run src/komponenty/pm/PmButton.test.tsx
```

Očekávej: FAIL — modul `./PmButton` neexistuje.

- [ ] **Krok 5: Napiš wrapper**

`ciselniky-web/src/komponenty/pm/PmButton.tsx`:

```tsx
import type { ReactNode } from 'react'

type Varianta = 'primary' | 'secondary' | 'ghost'

const MAPOVANI: Record<Varianta, { type: string; color: string }> = {
  primary:   { type: 'solid',   color: 'primary' },
  secondary: { type: 'outlined', color: 'primary' },
  ghost:     { type: 'base',    color: 'secondary' },
}

export function PmButton(props: {
  varianta: Varianta
  onClick?: () => void
  zakazano?: boolean
  children: ReactNode
}) {
  const { type, color } = MAPOVANI[props.varianta]
  return (
    <gov-button type={type} color={color}
                disabled={props.zakazano ? '' : undefined}
                onClick={props.onClick}>
      {props.children}
    </gov-button>
  )
}
```

Typy pro gov elementy — `ciselniky-web/src/gov-elements.d.ts`:

```ts
import type { DetailedHTMLProps, HTMLAttributes } from 'react'

type GovProps = DetailedHTMLProps<HTMLAttributes<HTMLElement>, HTMLElement>
  & Record<string, unknown>

declare module 'react' {
  namespace JSX {
    interface IntrinsicElements {
      'gov-button': GovProps
      'gov-icon': GovProps
      'gov-message': GovProps
      'gov-tag': GovProps
    }
  }
}
```

- [ ] **Krok 6: Ověř, že testy procházejí**

```bash
npx vitest run
```

Očekávej: **2 passed**.

- [ ] **Krok 7: Rozcestník a režim vzhledu**

`ciselniky-web/src/stranky/SeznamCiselniku.tsx`:

```tsx
export function SeznamCiselniku() {
  return (
    <main className="rozcestnik">
      <h1>Číselníky</h1>
      <gov-message color="primary">Zatím není založen žádný číselník.</gov-message>
    </main>
  )
}
```

`ciselniky-web/src/lib/vzhled.ts` — třístavový přepínač:

```ts
export type Vzhled = 'svetly' | 'tmavy' | 'podle-systemu'

const KLIC = 'ciselniky.vzhled'

export function nactiVzhled(): Vzhled {
  const ulozeny = document.cookie.split('; ')
    .find(c => c.startsWith(KLIC + '='))?.split('=')[1]
  return (ulozeny as Vzhled) ?? 'podle-systemu'
}

export function nastavVzhled(vzhled: Vzhled): void {
  document.cookie = `${KLIC}=${vzhled}; path=/; max-age=31536000; SameSite=Lax`
  const tmavy = vzhled === 'tmavy'
    || (vzhled === 'podle-systemu'
        && window.matchMedia('(prefers-color-scheme: dark)').matches)
  document.documentElement.dataset.theme = tmavy ? 'dark' : 'light'
}
```

> Přepínač je **vlastní záměrně.** `gov-theme-switch` umí jen dva stavy (světlý a tmavý);
> potřebujeme tři, včetně „podle systému", a vlastní uložení volby.
> Tentýž důvod vedl ke stejnému rozhodnutí v Zápisce.

- [ ] **Krok 8: Ověř offline pravidlo, spusť a commitni**

```bash
grep -rn "https://" ciselniky-web/index.html ciselniky-web/src   # očekávej: 0 nálezů
npx vitest run
git add -A && git commit -m "feat(web): React SPA s offline gov design systemem a prvními wrappery pm-*"
```

---

## Blok 6: Backend servíruje SPA

**Cíl bloku:** Jedna adresa vrací rozhraní i prohlížečovou aplikaci. Čeština se nerozsype.

- [ ] **Krok 1: Napiš padající test kódování**

`Ciselniky.Tests.Api/StatickeSouboryTests.cs`:

```csharp
using Ciselniky.Tests.Api.TestInfrastructure;

namespace Ciselniky.Tests.Api;

public sealed class StatickeSouboryTests(CiselnikyWebAppFactory factory)
    : IClassFixture<CiselnikyWebAppFactory>
{
    [Fact]
    public async Task IndexSeServirujeSKodovanimUtf8()
    {
        var klient = factory.VytvorKlienta();

        var odpoved = await klient.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal("utf-8", odpoved.Content.Headers.ContentType?.CharSet);
    }
}
```

> Bez výslovného kódování se na cílových stanicích čeština rozsype na nečitelné znaky.
> V Zápisce to byla skutečná chyba, na kterou se přišlo až u uživatele.

- [ ] **Krok 2: Spusť a ověř, že padá**

```bash
dotnet test Ciselniky.Tests.Api --filter StatickeSouboryTests
```

- [ ] **Krok 3: Nastav servírování**

`Ciselniky.Api/Program.cs`:

```csharp
var typySouboru = new FileExtensionContentTypeProvider();
typySouboru.Mappings[".html"] = "text/html; charset=utf-8";
typySouboru.Mappings[".js"]   = "application/javascript; charset=utf-8";
typySouboru.Mappings[".css"]  = "text/css; charset=utf-8";
typySouboru.Mappings[".json"] = "application/json; charset=utf-8";
typySouboru.Mappings[".svg"]  = "image/svg+xml; charset=utf-8";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = typySouboru,
    OnPrepareResponse = kontext =>
    {
        if (app.Environment.IsDevelopment())
            kontext.Context.Response.Headers.CacheControl = "no-cache, no-store";
    }
});

// Cesty SPA, které nejsou souborem ani rozhraním, vrací index.html
app.MapFallbackToFile("index.html");
```

> Vypnutá mezipaměť ve vývoji řeší past ze Zápisky: verzování se přidává jen vstupnímu
> souboru, ne modulům, které si natahuje sám — změny v nich pak nedorazí do prohlížeče
> ani po tvrdém obnovení a vypadá to jako chyba v kódu.

- [ ] **Krok 4: Vývojová proxy — jediný původ i při vývoji**

Vývojový server Vite běží na vlastním portu. Kdyby prohlížeč volal backend přímo na jiný
port, byl by to **cizí původ** a prohlížeč by k němu vyjednávání o Windows přihlášení
sám neposlal. Vypadalo by to jako rozbitá autorizace, přitom by šlo o původ.

`ciselniky-web/vite.config.ts`:

```ts
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/internal': { target: 'http://localhost:5080', changeOrigin: false },
      '/api':      { target: 'http://localhost:5080', changeOrigin: false },
      '/zdravi':   { target: 'http://localhost:5080', changeOrigin: false },
    },
  },
})
```

`changeOrigin: false` je podstatné — hlavička `Host` musí zůstat původní, jinak se
vyjednávání o přihlášení rozejde s tím, na co je server nastavený.

Ověření: v běžícím `npm run dev` musí `fetch('/zdravi')` z konzole prohlížeče vrátit
`{ stav: "bezi" }`. Vrací-li chybu původu, proxy není zapojená.

- [ ] **Krok 5: Propoj sestavení SPA s publikováním**

`Ciselniky.Api/Ciselniky.Api.csproj`:

```xml
<Target Name="BuildSpa" BeforeTargets="Publish">
  <Exec Command="npm ci"        WorkingDirectory="../ciselniky-web" />
  <Exec Command="npm run build" WorkingDirectory="../ciselniky-web" />
  <ItemGroup>
    <SpaVystup Include="../ciselniky-web/dist/**/*" />
  </ItemGroup>
  <Copy SourceFiles="@(SpaVystup)"
        DestinationFolder="$(PublishDir)wwwroot/%(RecursiveDir)" />
</Target>
```

> **Publish patří na build stroj, nikdy na server.** Tenhle cíl spouští `npm ci`;
> na serveru by spadl na chybějícím npm.
>
> **Server ani klientské stanice Node nepotřebují.** Sestavením vzniknou statické
> soubory, které IIS servíruje stejně jako obrázky, a prohlížeč má vlastní běhový
> modul pro JavaScript. Nasazení zůstává kopírování publikované složky.

- [ ] **Krok 6: Ověř a commitni**

```bash
dotnet test Ciselniky.Tests.Api
dotnet publish Ciselniky.Api -c Release -o ./publish
ls publish/wwwroot/lib/gov-design-system/dist/core | wc -l   # očekávej: 91
git add -A && git commit -m "feat(api): servírování SPA z backendu s vynuceným kódováním UTF-8"
```

---

## Blok 7: Koncový test průchodu

**Cíl bloku:** Playwright otevře aplikaci a ověří, že se rozcestník zobrazí a gov komponenty
se probraly k životu.

**Soubory:**
- Vytvoř: `Ciselniky.Tests.E2E/TestInfrastructure/E2EFixture.cs`
- Test: `Ciselniky.Tests.E2E/Scenare/RozcestnikTests.cs`

**Rozhraní:**
- Poskytuje: `E2EFixture.OtevriAsync(string cesta) → Task<IPage>`.

- [ ] **Krok 1: Fixture spouštějící aplikaci a prohlížeč**

`Ciselniky.Tests.E2E/TestInfrastructure/E2EFixture.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;

namespace Ciselniky.Tests.E2E.TestInfrastructure;

public sealed class E2EFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _aplikace;
    private IPlaywright? _playwright;
    private IBrowser? _prohlizec;

    public async ValueTask InitializeAsync()
    {
        _aplikace = new WebApplicationFactory<Program>();
        _ = _aplikace.CreateClient();   // vynutí start hostitele

        Microsoft.Playwright.Program.Main(["install", "chromium"]);
        _playwright = await Playwright.CreateAsync();
        _prohlizec = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task<IPage> OtevriAsync(string cesta)
    {
        var kontext = await _prohlizec!.NewContextAsync();
        await kontext.RouteAsync("**/*", async trasa =>
        {
            var pozadavek = trasa.Request;
            var relativni = new Uri(pozadavek.Url).PathAndQuery;
            var odpoved = await _aplikace!.CreateClient().GetAsync(relativni);
            await trasa.FulfillAsync(new()
            {
                Status = (int)odpoved.StatusCode,
                ContentType = odpoved.Content.Headers.ContentType?.ToString(),
                BodyBytes = await odpoved.Content.ReadAsByteArrayAsync()
            });
        });

        var stranka = await kontext.NewPageAsync();
        await stranka.GotoAsync("http://ciselniky.test" + cesta);
        return stranka;
    }

    public async ValueTask DisposeAsync()
    {
        if (_prohlizec is not null) await _prohlizec.CloseAsync();
        _playwright?.Dispose();
        if (_aplikace is not null) await _aplikace.DisposeAsync();
    }
}
```

- [ ] **Krok 2: Napiš test**

`Ciselniky.Tests.E2E/Scenare/RozcestnikTests.cs`:

```csharp
using Ciselniky.Tests.E2E.TestInfrastructure;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Ciselniky.Tests.E2E.Scenare;

public sealed class RozcestnikTests(E2EFixture fixture) : IClassFixture<E2EFixture>
{
    [Fact]
    public async Task Rozcestnik_SeZobrazi_ADopadneHydratace()
    {
        var stranka = await fixture.OtevriAsync("/");

        await stranka.WaitForSelectorAsync("h1");
        Assert.Contains("Číselníky", await stranka.TitleAsync());

        // gov komponenta je připravená, až dostane třídu hydrated.
        // Bez čekání na ni test závodí s načtením modulu.
        await stranka.WaitForSelectorAsync("gov-message.hydrated");
        await Expect(stranka.Locator("gov-message")).ToHaveCountAsync(1);
    }
}
```

> Na host gov komponenty se v Playwrightu **nedá** použít `ToBeVisibleAsync` —
> host má nulovou výšku a test by hlásil, že prvek není vidět. Ověřuje se počtem prvků
> přes `ToHaveCountAsync`. Poučení ze Zápisky.

- [ ] **Krok 3: Spusť test a ověř, že padá**

```bash
dotnet test Ciselniky.Tests.E2E --filter RozcestnikTests
```

Očekávej: FAIL — rozcestník ještě není napojený na sestavený výstup SPA.

- [ ] **Krok 4: Zkopíruj výstup sestavení SPA a spusť znovu**

```bash
cd ciselniky-web && npm run build && cd ..
cp -R ciselniky-web/dist/. Ciselniky.Api/wwwroot/
dotnet test Ciselniky.Tests.E2E --filter RozcestnikTests
```

Očekávej: **Passed: 1**.

- [ ] **Krok 5: Plná sada a commit**

```bash
dotnet test Ciselniky.sln
git add -A && git commit -m "test(e2e): průchodový test rozcestníku s čekáním na hydrataci gov komponent"
```

---

## Blok 8: Dokumentační kostra a verze 0.1

**Cíl bloku:** Repozitář má dokumentaci od začátku, ne až na konci. Aplikace má verzi.

- [ ] **Krok 1: Založ strukturu dokumentace**

```
docs/
├── README.md                 rozcestník
├── technical/                00-documentation-tree … 10-troubleshooting-recovery
├── specs/                    sem se kopíruje 10-specifikace ze zadání
├── plans/                    sem se kopírují plány ze zadání
├── known-issues/             prázdné, formát YYYY-MM-DD-<popis>.md
├── architecture/             stavební pravidla komponent pm-*
├── changelog/releases/0.1.md
└── wiki/                     struktura předem, texty při implementaci bloků
    ├── index.md
    ├── zacatek/
    ├── ciselniky/
    ├── editace/
    ├── verzovani/
    ├── rozhrani/
    └── nastaveni/
```

Struktura wiki vzniká **teď**, texty až v blocích, které příslušnou funkci zavádějí
(rozhodnutí G3).

- [ ] **Krok 2: Podklad changelogu**

`docs/changelog/releases/0.1.md` se sekcemi **Přidáno / Změněno / Opraveno**.
Kořenový `CHANGELOG.md` se **generuje skriptem**, needituje se ručně.

- [ ] **Krok 3: Verze aplikace na jednom místě**

V `Directory.Build.props`:

```xml
<PropertyGroup>
  <Version>0.1.0</Version>
</PropertyGroup>
```

Zobrazí se v patičce aplikace a vrací ji `/zdravi`.

- [ ] **Krok 4: Test hlídající, že verze v changelogu odpovídá sestavení**

`Ciselniky.Tests.Unit/Common/VerzeTests.cs`:

```csharp
using System.Reflection;

namespace Ciselniky.Tests.Unit.Common;

public sealed class VerzeTests
{
    [Fact]
    public void PodkladChangelogu_ExistujeProAktualniVerzi()
    {
        var verze = typeof(Ciselniky.Core.Data.KontrolaSchematu).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];

        var kratka = string.Join('.', verze.Split('.').Take(2));   // 0.1.0 → 0.1
        var soubor = Path.Combine(KorenRepozitare(), "docs", "changelog", "releases", $"{kratka}.md");

        Assert.True(File.Exists(soubor), $"Chybí podklad changelogu {soubor}");
    }

    private static string KorenRepozitare()
    {
        var adresar = new DirectoryInfo(AppContext.BaseDirectory);
        while (adresar is not null && !File.Exists(Path.Combine(adresar.FullName, "Ciselniky.sln")))
            adresar = adresar.Parent;
        return adresar!.FullName;
    }
}
```

- [ ] **Krok 5: Plná sada a commit**

```bash
dotnet test Ciselniky.sln
git add -A && git commit -m "chore(docs): kostra dokumentace, wiki, changelog a verze 0.1"
```

---

## Po dokončení P1 ručně ověř

1. Aplikace se spustí a `/zdravi` vrátí `bezi`.
2. `db_seed_prvni_superadmin.sql` proběhne opakovaně bez chyby a vypíše založeného správce.
3. V prohlížeči se otevře rozcestník se svým nadpisem a hláškou o prázdném seznamu.
4. Přepínač vzhledu funguje ve všech třech stavech a volba přežije obnovení stránky.
5. Vývojářská konzole prohlížeče je bez chyb a **bez jediného požadavku mimo aplikaci**.
6. `dotnet test Ciselniky.sln` — všechny čtyři vrstvy zeleně.
