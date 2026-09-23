# Výzvy: oprava bufferu, skutečná cena a tisk podle vzoru — implementační plán

> **Pro exekutora:** POVINNÁ SUB-DOVEDNOST `superpowers:executing-plans`. Uživatel chce
> **inline exekuci** v hlavní session, ne subagenty. Kroky mají checkboxy `- [ ]`.

**Cíl:** Opravit vracení PNF do bufferu, ukázat skutečnou cenu z kalkulace a sázet výzvu přesně
podle finálního vzoru.

**Architektura:** Uložení záznamu přestane sahat na zařazení do výzvy. Skutečná cena se ukládá
jako snímek u vazby, plní ho samostatná služba volaná z harvestu před rozhodnutím podle
fingerprintu. Výzva dostane novou projekci (římské pořadí, licence z `rozpad_licence`) a Word
se staví se záhlavím, zápatím, styly a skutečnými seznamy.

**Technologie:** ASP.NET Core MVC (net8.0), EF Core 8, Razor, DocumentFormat.OpenXml 3.2.0,
xUnit + FluentAssertions + Moq, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-10-pnf-skutecna-cena-design.md`

## Globální omezení

- **Commity drží uživatel.** Žádný krok nespustí `git commit`; místo „Commit" je „Ověření".
- **Build 0 chyb, 0 upozornění.**
- **Výchozí stav:** Unit 1721/1721, Api 362/366, Integration 86/87. Známá selhání, která se
  nikdy neopravují ani nevydávají za regresi: Api
  `RecordEditorControllerTests.Edit_ShouldRenderScheduleMiniGantt_WithAlignedAxis_WithoutPerStepDuplicateBars`
  a tři `ProjectHarmonogramRenderTests.*`; Integration
  `ProposalRejectAndTakeOverE2ETests.RejectAndTakeOver_ThenSaveWithModifiedData_CreatesRecord`.
- `appsettings.json` a `appsettings.Development.json` jsou gitignorované secrety — needitovat.
- Schéma jen ručním `db_upgrade_*.sql`, idempotentním. Zapsat do
  `docs/technical/06-database-bootstrap-migrations.md` (Integration sada z něj staví databázi)
  a do `db_check_applied_upgrades.sql`.
- Razor kóduje diakritiku na číselné entity — v Api HTML testech kotvit na atributy a ASCII.
- **Novou externí vazbu Integration sada přes `SaveRecord` nezaloží** — ServiceDesk je vypnutý
  a validace nový tiket odmítne. Vazby se seedují přímo do DB.
- Vzor výzvy `2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx` je gitignorovaný, nikdy ho necommitovat.
- `DphSazba = 0.21m` zůstává napevno v `VyzvaExportViewModel` (rozhodnutí uživatele).

## Mapa souborů

| Soubor | Odpovědnost | Blok |
|---|---|---|
| `PmTracker.Web/Services/RecordService.SaveRecord.cs` | UPSERT nesahá na zařazení; nová PNF do bufferu; bez pole `Vyzva` | 1, 2 |
| `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs` | příkaz bez `Vyzva` | 1 |
| `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs` | kopíruje `Pozadavek` a `ZaradidDoVyzvy` | 1, 2 |
| `PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js` | přepínač u nové vazby | 2 |
| `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` | snímek kalkulace u vazby | 3 |
| `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs` | mapování snímku | 3 |
| `db_upgrade_1_4_3_externi_odkaz_kalkulace.sql` (nový) | sloupce snímku | 3 |
| `PmTracker.Web/Services/ServiceDesk/KalkulaceSnapshotService.cs` (nový) | zápis snímku | 4 |
| `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs` | volání snímku před fingerprintem | 4 |
| `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` | DI | 4 |
| `PmTracker.Web/Views/Projekty/_ZaznamDetailPartial.cshtml` | chip se skutečnou cenou | 5 |
| `PmTracker.Web/Services/ProjectService.RecordCards.cs`, `ProjectService.LazyQueries.cs` | plnění chipu | 5 |
| `PmTracker.Web/Services/Vyzvy/*` + `_VyzvyPane.cshtml` | cena ve Výzvách | 5 |
| `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs` | export záznamů jen skutečná cena | 5 |
| `PmTracker.Web/Services/Export/RichTextHtmlParser.cs` | druh a identita seznamu | 6 |
| `PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs`, `VyzvaExportViewModels.cs` | projekce výzvy | 7 |
| `PmTracker.Web/Services/Vyzvy/RozpadLicenceParser.cs` (nový) | řádky licence z HTML | 7 |
| `PmTracker.Web/Services/Export/OpenXmlVyzvaExportService.cs` | Word podle vzoru | 8 |
| `PmTracker.Web/Views/Export/VyzvaTemplate.cshtml`, `wwwroot/css/pdf-export.css` | náhled podle vzoru | 9 |

---

## Blok 1: Uložení záznamu nesahá na zařazení do výzvy

Spec §0.1, R0.1, R0.4. Oprava hlášené vady.

**Soubory:**
- Upravit: `PmTracker.Web/Services/RecordService.SaveRecord.cs`
- Upravit: `PmTracker.Web/Models/ViewModels/Commands/RecordCommands.cs`
- Upravit: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs`
- Test: `PmTracker.Tests.Integration/DataStore/RecordSaveDataStoreTests.cs`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs`

- [ ] **Krok 1: Napsat padající Integration test**

Na konec třídy `RecordSaveDataStoreTests`:

```csharp
    /// <summary>
    /// Regrese z 2026-04-20: uložení záznamu přepisovalo VyzvaId podle textového pole Vyzva,
    /// které formulář od přechodu na přepínač neposílá. PNF po každém uložení vypadlo z výzvy
    /// do bufferu — i z odeslané, zamčené výzvy (spec 2026-09-10 §0).
    ///
    /// Scénář z hlášení uživatele: u PNF ve výzvě se změní jen předpokládaná cena.
    /// </summary>
    [Theory]
    [InlineData(VyzvaStav.Priprava)]
    [InlineData(VyzvaStav.Odeslano)]
    public async Task SaveRecord_ZmenaCenyPnfVeVyzve_ZachovaZarazeni(VyzvaStav stav)
    {
        var db = await _fixture.CreateDatabaseAsync($"record_save_keeps_vyzva_{(int)stav}");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordKeepVyzvaAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "RecordKeepVyzvaOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "RKEEPVYZVA");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "RKEEPVYZVA_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var recordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "Zaznam s PNF ve vyzve");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId).Select(x => x.Kod).FirstAsync();
        var statusCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId).Select(x => x.Kod).FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId).Select(x => x.Kod).FirstAsync();

        var vyzva = new VyzvaEntity
        {
            ProjektId = projectId,
            Kod = "3/2026",
            PoradoveVRoce = 3,
            Rok = 2026,
            Stav = stav,
            DatumZalozeni = new DateTime(2026, 9, 1),
            ZalozilOsobaId = ownerId,
            DatumOdeslani = stav == VyzvaStav.Odeslano ? new DateTime(2026, 9, 5) : null,
            OdeslalOsobaId = stav == VyzvaStav.Odeslano ? ownerId : null,
            MistoPlneniSnapshot = "FIS (EIS): VZ 8201",
            CisloRamcoveSmlouvySnapshot = "INT-SML-KEEP",
        };
        dbContext.Vyzvy.Add(vyzva);
        await dbContext.SaveChangesAsync();

        // Vazba vzniká přímo v DB — novou vazbu by validace bez ServiceDesku odmítla.
        var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var vazba = new ZaznamExterniOdkazEntity
        {
            ZaznamId = recordId,
            TypOdkazuId = pnfTypeId,
            Cislo = "336865",
            PredpokladanaCena = 1000m,
            ZaradidDoVyzvy = true,
            VyzvaId = vyzva.Id,
        };
        dbContext.ZaznamExterniOdkazy.Add(vazba);
        await dbContext.SaveChangesAsync();
        var vazbaId = vazba.Id;
        dbContext.ChangeTracker.Clear();

        // Přesně to, co posílá formulář: Id, typ, číslo, cenu a skryté VyzvaId + ZaradidDoVyzvy.
        store.SaveRecord(new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = record.ProjektId,
            Kategorie = categoryCode,
            Stav = statusCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu,
            ExterniVazby = new List<SaveRecordExterniVazbaCommand>
            {
                new()
                {
                    Id = vazbaId,
                    Typ = "PNF",
                    Cislo = "336865",
                    PredpokladanaCena = "2000",
                    VyzvaId = vyzva.Id,
                    ZaradidDoVyzvy = true,
                },
            },
        }, currentUser);

        var poUlozeni = await dbContext.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == vazbaId);
        poUlozeni.PredpokladanaCena.Should().Be(2000m, "změna ceny se uložit musí");
        poUlozeni.VyzvaId.Should().Be(vyzva.Id, "uložení záznamu nesmí PNF vytáhnout z výzvy");
        poUlozeni.ZaradidDoVyzvy.Should().BeTrue("přepínač zůstává zapnutý");
    }
```

- [ ] **Krok 2: Napsat padající pin**

Na konec třídy `RecordServiceExternalLinkUpsertTests`:

```csharp
    /// <summary>
    /// Zařazení do výzvy vlastní VyzvaService (spec 2026-09-10 R0.1). UPSERT u existující vazby
    /// nesmí přepsat VyzvaId ani ZaradidDoVyzvy — dřív to dělal podle pole, které formulář
    /// neposílá, a každé uložení vytáhlo PNF z výzvy.
    /// </summary>
    [Fact]
    public void ReplaceRecordExternalLinksAsync_NesahaNaZarazeniExistujiciVazby()
    {
        var source = LoadServiceSource();
        var start = source.IndexOf("ReplaceRecordExternalLinksAsync(int zaznamId,", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var end = source.IndexOf("\n    private ", start + 1, StringComparison.Ordinal);
        var metoda = end < 0 ? source[start..] : source[start..end];

        metoda.Should().NotContain("existingEntity.VyzvaId", "zařazení do výzvy vlastní VyzvaService");
        metoda.Should().NotContain("existingEntity.ZaradidDoVyzvy", "přepínač se ukládá vlastním endpointem");
        source.Should().NotContain("ResolveVyzvaIdAsync", "mrtvé pole Vyzva se už nikam nepřekládá");
    }
```

- [ ] **Krok 3: Spustit a ověřit selhání**

```bash
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~SaveRecord_ZmenaCenyPnfVeVyzve_ZachovaZarazeni"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~NesahaNaZarazeniExistujiciVazby"
```

Očekávej: oba Integration případy selžou na `VyzvaId` = null, pin selže na `existingEntity.VyzvaId`.

- [ ] **Krok 4: Opravit UPSERT**

V `RecordService.SaveRecord.cs`, metoda `ReplaceRecordExternalLinksAsync`:

Smaž řádek:

```csharp
            var vyzvaId = await ResolveVyzvaIdAsync(link.Vyzva, ct);
```

Ve větvi existující vazby nahraď:

```csharp
                existingEntity.PredpokladanaCena = price;
                existingEntity.VyzvaId = vyzvaId;
                existingEntity.Pozadavek = pozadavek;
```

za:

```csharp
                existingEntity.PredpokladanaCena = price;
                existingEntity.Pozadavek = pozadavek;
                // 2026-09-10: VyzvaId ani ZaradidDoVyzvy se tu nepřepisují — zařazení do výzvy
                // vlastní VyzvaService. Dřív se VyzvaId skládalo z pole Vyzva, které formulář
                // od 2026-04-20 neposílá, a každé uložení vytáhlo PNF z výzvy (spec 2026-09-10 §0).
```

V inicializátoru nové vazby smaž řádek `VyzvaId = vyzvaId,`.

Smaž celou metodu `ResolveVyzvaIdAsync`.

- [ ] **Krok 5: Vyhodit mrtvé pole z validace**

V `ValidateExternalLinksAsync` smaž načtení výzev:

```csharp
        var vyzvaRows = await dbContext.Vyzvy.AsNoTracking()
            .Select(x => new { x.Id, x.Kod })
            .ToListAsync(ct);
```

smaž řádek `var vyzvaValue = (link.Vyzva ?? string.Empty).Trim();`, podmínku prázdného řádku
zkrať na:

```csharp
            if (!hasType && !hasCislo && string.IsNullOrWhiteSpace(priceValue))
```

a smaž celý blok `if (!string.IsNullOrWhiteSpace(vyzvaValue)) { … external_vyzva_not_found … }`.

- [ ] **Krok 6: Smazat pole z příkazu a mapperu**

V `RecordCommands.cs` smaž ze `SaveRecordExterniVazbaCommand` řádek `public string? Vyzva { get; set; }`.

V `RecordProposalPayloadMapper.cs` smaž v `BuildSaveCommand` řádek `Vyzva = link.Vyzva,` a v mapování
pro zobrazení řádek `VyzvaKod = link.Vyzva,`. Mapper slouží návrhu **založení** — jeho vazby jsou
nové a ve výzvě být nemohou, takže `VyzvaKod` zůstane správně `null`.

Přelož řešení; pokud překladač najde další použití `.Vyzva` na příkazu, je to mrtvý kód se stejným
původem — smaž ho.

- [ ] **Krok 7: Spustit a ověřit průchod, pak červeno-zeleně**

Spusť příkazy z kroku 3 — očekávej zelenou. Pak dočasně vrať do větve existující vazby
`existingEntity.VyzvaId = null;`, spusť Integration test (musí selhat) a řádek smaž.

- [ ] **Krok 8: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj
```

Očekávej: build 0/0, Unit zelená, Integration jen se známým selháním.

---
## Blok 2: Nová PNF do bufferu a mapper návrhu

Spec §0.2, §0.3, R0.2, R0.3, R0.5.

**Soubory:**
- Upravit: `PmTracker.Web/Services/RecordService.SaveRecord.cs`
- Upravit: `PmTracker.Web/Services/Records/RecordProposalPayloadMapper.cs`
- Upravit: `PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js`
- Test: `PmTracker.Tests.Unit/Records/RecordProposalPayloadMapperVazbyTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs`
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvySwitchJsTests.cs` (nový)

- [ ] **Krok 1: Napsat padající test mapperu**

`PmTracker.Tests.Unit/Records/RecordProposalPayloadMapperVazbyTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Schválení návrhu založení staví příkaz uložení z payloadu. Text požadavku a zařazení do
/// bufferu se musí přenést — dřív se ztrácely (spec 2026-09-10 §0.3).
/// </summary>
public sealed class RecordProposalPayloadMapperVazbyTests
{
    [Fact]
    public void BuildSaveCommand_PrenasiTextPozadavkuAZarazeniDoBufferu()
    {
        var command = new RecordProposalPayloadMapper().BuildSaveCommand(new CreateRecordProposalPayload
        {
            ExterniVazby =
            [
                new SaveRecordExterniVazbaCommand
                {
                    Typ = "PNF",
                    Cislo = "336865",
                    Pozadavek = "<p>Chceme sestavu.</p>",
                    ZaradidDoVyzvy = true,
                },
            ],
        });

        var vazba = command.ExterniVazby.Should().ContainSingle().Which;
        vazba.Pozadavek.Should().Be("<p>Chceme sestavu.</p>", "text napsaný v návrhu se nesmí ztratit");
        vazba.ZaradidDoVyzvy.Should().BeTrue("zapnutý přepínač v návrhu platí i po schválení");
    }
}
```

- [ ] **Krok 2: Napsat padající pin UPSERTu**

Do `RecordServiceExternalLinkUpsertTests`:

```csharp
    /// <summary>
    /// Nová vazba nemá Id, takže přepínač nemohl zavolat set-zaradid — stav nese formulář.
    /// Do bufferu smí jen PNF a jen s oprávněním, které hlídá i endpoint přepínače (R0.2).
    /// Integration sada novou vazbu přes uložení nezaloží (ServiceDesk je vypnutý), proto pin.
    /// </summary>
    [Fact]
    public void ReplaceRecordExternalLinksAsync_NovaPnfJdeDoBufferuJenSOpravnenim()
    {
        var source = LoadServiceSource();

        source.Should().Contain("ZaradidDoVyzvy = link.ZaradidDoVyzvy && smiZaraditDoBufferu && JePnf(link.Typ)",
            "nová vazba bere stav přepínače z formuláře, ale jen PNF a jen s oprávněním");
        source.Should().Contain("currentUser.HasPermission(PermissionKeys.VyzvyPnfAssign, command.ProjektId)",
            "stejné oprávnění, jaké hlídá endpoint přepínače");
    }
```

- [ ] **Krok 3: Napsat padající JS pin**

`PmTracker.Tests.Unit/Vyzvy/VyzvySwitchJsTests.cs`:

```csharp
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// Přepínač „Zařadit" u nové vazby (spec 2026-09-10 R0.3). Aplikace nemá JS test runner,
/// ověřuje se zdroj modulu.
/// </summary>
public sealed class VyzvySwitchJsTests
{
    private static string Js()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        var root = dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
        return File.ReadAllText(Path.Combine(root, "PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js"));
    }

    [Fact]
    public void NovaVazba_PrepneJenSkrytePole_BezVolaniServeru()
    {
        var src = Js();

        var novaVetev = src.IndexOf("Po uložení půjde do bufferu", StringComparison.Ordinal);
        var volani = src.IndexOf("postForm(", StringComparison.Ordinal);

        novaVetev.Should().BeGreaterThan(-1, "nová vazba musí dostat vlastní větev");
        volani.Should().BeGreaterThan(novaVetev,
            "větev nové vazby skončí dřív, než se volá set-zaradid — vazba ještě nemá Id");
    }
}
```

- [ ] **Krok 4: Spustit a ověřit selhání**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RecordProposalPayloadMapperVazbyTests|FullyQualifiedName~NovaPnfJdeDoBufferuJenSOpravnenim|FullyQualifiedName~VyzvySwitchJsTests"
```

Očekávej: tři selhání.

- [ ] **Krok 5: Novou PNF zařadit do bufferu**

V `RecordService.SaveRecord.cs` změň signaturu:

```csharp
    private async Task<List<ZaznamExterniOdkazEntity>> ReplaceRecordExternalLinksAsync(
        int zaznamId,
        IReadOnlyList<SaveRecordExterniVazbaCommand> externalLinks,
        bool smiZaraditDoBufferu,
        CancellationToken ct)
```

Volání v `SaveRecordAsync` (dnes `var addedExternalLinks = await ReplaceRecordExternalLinksAsync(entity.Id, command.ExterniVazby, innerCt);`):

```csharp
            // Nová PNF do bufferu jen s oprávněním, které hlídá i endpoint přepínače (R0.2).
            var smiZaraditDoBufferu = currentUser.HasPermission(PermissionKeys.VyzvyPnfAssign, command.ProjektId);
            var addedExternalLinks = await ReplaceRecordExternalLinksAsync(
                entity.Id, command.ExterniVazby, smiZaraditDoBufferu, innerCt);
```

V inicializátoru nové vazby za `Pozadavek = pozadavek,`:

```csharp
                    // Nová vazba ještě nemá Id, takže přepínač nemohl zavolat set-zaradid —
                    // stav nese skryté pole formuláře. VyzvaId zůstává null: do konkrétní výzvy
                    // se PNF dostane jen vědomým přesunem (spec 2026-09-10 R0.2).
                    ZaradidDoVyzvy = link.ZaradidDoVyzvy && smiZaraditDoBufferu && JePnf(link.Typ),
```

A k ostatním statickým pomocníkům (vedle `SupportsEstimatedExternalLinkPrice`):

```csharp
    private static bool JePnf(string? typ)
        => string.Equals(typ?.Trim(), "PNF", StringComparison.OrdinalIgnoreCase);
```

`PermissionKeys` je v `PmTracker.Web.Models.ViewModels`, který soubor už importuje.

- [ ] **Krok 6: Mapper návrhu**

V `RecordProposalPayloadMapper.BuildSaveCommand` doplň do inicializátoru vazby za `PredpokladanaCena = link.PredpokladanaCena,`:

```csharp
                    ZaradidDoVyzvy = link.ZaradidDoVyzvy,
                    Pozadavek = link.Pozadavek,
```

- [ ] **Krok 7: Přepínač u nové vazby**

V `switchController.js` nahraď začátek `handleSwitch` až po `switchEl.setAttribute('disabled', '');`:

```javascript
  async function handleSwitch(switchEl, isChecked) {
    const externiOdkazId = switchEl.dataset.externiOdkazId;
    const wrap = switchEl.closest('[data-external-vyzvy-switch-wrap]');
    const statusEl = wrap ? wrap.querySelector('[data-vyzvy-switch-status]') : null;
    const hiddenState = wrap ? wrap.querySelector('[data-external-vyzvy-switch-state]') : null;

    // Nová vazba ještě nemá Id (spec 2026-09-10 R0.3), set-zaradid nemá co volat.
    // Stav nese skryté pole formuláře a PNF se do bufferu zařadí při uložení záznamu.
    if (!externiOdkazId || externiOdkazId === '0') {
      if (hiddenState) hiddenState.value = isChecked ? 'true' : 'false';
      if (statusEl) statusEl.textContent = isChecked ? 'Po uložení půjde do bufferu' : '';
      return;
    }

    switchEl.setAttribute('disabled', '');
```

- [ ] **Krok 8: Spustit a ověřit průchod**

Příkaz z kroku 4 — tři úspěšné. Plus `node --check PmTracker.Web/wwwroot/js/modules/vyzvy/switchController.js`.

- [ ] **Krok 9: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
```

Očekávej: build 0/0, Unit zelená, Api jen 4 známá selhání (`VyzvySwitchBufferTests` zelené —
cesta existující vazby se nemění).

---

## Blok 3: Sloupce snímku kalkulace

Spec A4.

**Soubory:**
- Upravit: `PmTracker.Web/Models/Entities/PmTrackerEntities.cs` (`ZaznamExterniOdkazEntity`)
- Upravit: `PmTracker.Web/Data/Configuration/RecordEntityConfiguration.cs`
- Vytvořit: `db_upgrade_1_4_3_externi_odkaz_kalkulace.sql`
- Upravit: `docs/technical/06-database-bootstrap-migrations.md`, `db_check_applied_upgrades.sql`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/ExterniOdkazKalkulaceMappingTests.cs` (nový)

- [ ] **Krok 1: Napsat padající test mapování**

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>Snímek skutečné ceny z kalkulace žije u vazby (spec 2026-09-10 A4).</summary>
public sealed class ExterniOdkazKalkulaceMappingTests
{
    private static PmTrackerDbContext CreateDb()
        => new(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Theory]
    [InlineData(nameof(ZaznamExterniOdkazEntity.KalkulaceCena), "kalkulace_cena")]
    [InlineData(nameof(ZaznamExterniOdkazEntity.KalkulaceId), "kalkulace_id")]
    [InlineData(nameof(ZaznamExterniOdkazEntity.KalkulaceNacteno), "kalkulace_nacteno")]
    public void Snimek_SeMapujeNaSvujSloupec(string vlastnost, string sloupec)
    {
        using var db = CreateDb();

        var property = db.Model.FindEntityType(typeof(ZaznamExterniOdkazEntity))!.FindProperty(vlastnost);

        property.Should().NotBeNull();
        property!.GetColumnName().Should().Be(sloupec);
        property.IsNullable.Should().BeTrue("stávající vazby zůstávají prázdné, doplní je harvest");
    }
}
```

- [ ] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~ExterniOdkazKalkulaceMappingTests"`
Očekávej: chybu překladu — vlastnosti neexistují.

- [ ] **Krok 3: Doplnit entitu a mapování**

Do `ZaznamExterniOdkazEntity` za `Pozadavek`:

```csharp
    /// <summary>
    /// Snímek skutečné ceny z akceptované kalkulace — HOT_KALKULACE.cena, celková včetně licence,
    /// bez DPH. Plní ho výhradně IKalkulaceSnapshotService při harvestu; uložení záznamu na něj
    /// nesahá (spec 2026-09-10 A3). NULL = kalkulace není známá.
    /// </summary>
    public decimal? KalkulaceCena { get; set; }

    /// <summary>HOT_KALKULACE.id kalkulace, ze které je KalkulaceCena.</summary>
    public long? KalkulaceId { get; set; }

    /// <summary>Kdy se snímek naposledy změnil (UTC).</summary>
    public DateTime? KalkulaceNacteno { get; set; }
```

Do `RecordEntityConfiguration.cs` za řádek s `pozadavek`:

```csharp
        builder.Property(x => x.KalkulaceCena).HasColumnName("kalkulace_cena").HasColumnType("decimal(18,2)");
        builder.Property(x => x.KalkulaceId).HasColumnName("kalkulace_id");
        builder.Property(x => x.KalkulaceNacteno).HasColumnName("kalkulace_nacteno");
```

- [ ] **Krok 4: Spustit a ověřit průchod**

Příkaz z kroku 2 — 3 úspěšné.

- [ ] **Krok 5: Migrační skript**

`db_upgrade_1_4_3_externi_odkaz_kalkulace.sql`:

```sql
-- =============================================================================
-- db_upgrade_1_4_3_externi_odkaz_kalkulace.sql
--
-- Snímek skutečné ceny PNF z akceptované kalkulace (spec 2026-09-10 část A).
--
-- KONTEXT: Aplikace ukazuje skutečnou cenu z HOT_KALKULACE místo předpokládané. Aby se
-- nečetla cizí databáze při každém vykreslení karet, harvest ji ukládá jako snímek u vazby.
--
-- ROZSAH MIGRACE:
--   * Přidá do dbo.zaznam_externi_odkazy sloupce kalkulace_cena, kalkulace_id,
--     kalkulace_nacteno — všechny NULL.
--   * Žádná data se nemigrují; doplní je první běh synchronizace se ServiceDeskem.
--   * Předpokládaná cena (predpokladana_cena) se nemění.
--
-- Idempotence: skript lze spustit opakovaně.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT N'[1.4.3] zaznam_externi_odkazy.kalkulace_* — start';

IF OBJECT_ID(N'dbo.zaznam_externi_odkazy', N'U') IS NULL
BEGIN
    RAISERROR(N'Tabulka dbo.zaznam_externi_odkazy neexistuje — spusť dřívější upgrade skripty.', 16, 1);
    RETURN;
END

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_cena') IS NULL
    ALTER TABLE dbo.zaznam_externi_odkazy ADD kalkulace_cena DECIMAL(18,2) NULL;

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_id') IS NULL
    ALTER TABLE dbo.zaznam_externi_odkazy ADD kalkulace_id BIGINT NULL;

IF COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_nacteno') IS NULL
    ALTER TABLE dbo.zaznam_externi_odkazy ADD kalkulace_nacteno DATETIME2 NULL;

PRINT N'[1.4.3] hotovo';
```

- [ ] **Krok 6: Zapsat skript tam, kde se podle něj staví a kontroluje databáze**

V `docs/technical/06-database-bootstrap-migrations.md` §5.1 za
`31. \`db_upgrade_1_4_2_externi_odkaz_pozadavek.sql\`` vlož
`32. \`db_upgrade_1_4_3_externi_odkaz_kalkulace.sql\`` a seed přečísluj na 33.

V `db_check_applied_upgrades.sql` za deklaraci `@pozadavek`:

```sql
DECLARE @kalkulace INT = CASE
    WHEN COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_cena') IS NOT NULL
     AND COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_id') IS NOT NULL
     AND COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'kalkulace_nacteno') IS NOT NULL
    THEN 1 ELSE 0 END;
    -- 1_4_3: snímek skutečné ceny z kalkulace u externí vazby
```

a za řádek přehledu `310`:

```sql
INSERT @r VALUES (320, N'db_upgrade_1_4_3_externi_odkaz_kalkulace',
    CASE WHEN @kalkulace = 1 THEN N'APLIKOVÁN' ELSE N'CHYBÍ' END,
    N'sloupce zaznam_externi_odkazy.kalkulace_cena/_id/_nacteno (snímek skutečné ceny)');
```

- [ ] **Krok 7: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~RecordSaveDataStoreTests"
```

Očekávej: build 0/0, Unit zelená, `RecordSaveDataStoreTests` zelené — potvrzuje, že bootstrap
nový skript opravdu pustil.

---
## Blok 4: Služba snímku a zapojení do harvestu

Spec A3 R4–R6, R8. Snímek běží **vedle** harvestu, ne za fingerprintem.

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/ServiceDesk/KalkulaceSnapshotService.cs`
- Upravit: `PmTracker.Web/Services/ServiceDesk/VyjadreniHarvestService.cs`
- Upravit: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs`
- Test: `PmTracker.Tests.Unit/ServiceDesk/KalkulaceSnapshotServiceTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/ServiceDesk/FingerprintDetectionTests.cs`
- Test: `PmTracker.Tests.Unit/ExterniOdkaz/RecordServiceExternalLinkUpsertTests.cs`

- [ ] **Krok 1: Napsat padající testy služby**

`PmTracker.Tests.Unit/ServiceDesk/KalkulaceSnapshotServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Unit.Vyzvy;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>Pravidla snímku skutečné ceny (spec 2026-09-10 A3 R6).</summary>
public sealed class KalkulaceSnapshotServiceTests
{
    private const string Cislo = "336865";
    private const string Pid = "A490P00E21MX";

    private sealed class FakeTicketing : ITicketingQueryService
    {
        public Dictionary<string, HotZaznamDto> Zaznamy { get; } = new();
        public Dictionary<string, HotKalkulaceDto> Kalkulace { get; } = new();

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult(Zaznamy.GetValueOrDefault(cislo));

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotZaznamDto>>(
                cisla.Where(Zaznamy.ContainsKey).ToDictionary(c => c, c => Zaznamy[c]));

        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string pid, CancellationToken ct)
            => Task.FromResult(Kalkulace.GetValueOrDefault(pid));

        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> pidy, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(
                pidy.Where(Kalkulace.ContainsKey).ToDictionary(p => p, p => Kalkulace[p]));
    }

    private static HotKalkulaceDto Kalkulace(long id, decimal cena) => new(
        id, Pid,
        PracnostAnalyza: 0m, SazbaAnalyza: 0m, CenaAnalyza: 0m,
        PracnostProgramovani: 0m, SazbaProgramovani: 0m, CenaProgramovani: 0m,
        PracnostTestovani: 0m, SazbaTestovani: 0m, CenaTestovani: 0m,
        PracnostImplementace: 0m, SazbaImplementace: 0m, CenaImplementace: 0m,
        CenaCelkem: cena,
        PocetLicenci: 1m, SazbaLicence: cena, CenaLicence: cena, RozpadLicence: null,
        TextTermin: null);

    private static async Task<(PmTrackerDbContext Db, int VazbaId)> SeedAsync(
        string typKod = "PNF", decimal? kalkulaceCena = null)
    {
        var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        if (typKod != "PNF")
        {
            db.CiselnikTypuExternichOdkazu.Add(new CiselnikTypuExternichOdkazuEntity { Id = 99, Kod = typKod, Nazev = typKod });
            await db.SaveChangesAsync();
        }

        var typId = await db.CiselnikTypuExternichOdkazu.Where(t => t.Kod == typKod).Select(t => t.Id).SingleAsync();
        var vazba = new ZaznamExterniOdkazEntity
        {
            Id = 700, ZaznamId = 100, TypOdkazuId = typId, Cislo = Cislo,
            PredpokladanaCena = 12000m, KalkulaceCena = kalkulaceCena,
        };
        db.ZaznamExterniOdkazy.Add(vazba);
        await db.SaveChangesAsync();
        return (db, vazba.Id);
    }

    private static KalkulaceSnapshotService Sut(PmTrackerDbContext db, ITicketingQueryService ticketing)
        => new(db, ticketing,
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero)),
            NullLogger<KalkulaceSnapshotService>.Instance);

    [Fact]
    public async Task Sync_AkceptovanaKalkulace_ZapiseCenuIdACas()
    {
        var (db, id) = await SeedAsync();
        using var _ = db;
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy[Cislo] = new HotZaznamDto(Cislo, "PNF", "s", "p", Pid: Pid);
        ticketing.Kalkulace[Pid] = Kalkulace(5414, 10340m);

        await Sut(db, ticketing).SyncAsync(new[] { id }, CancellationToken.None);

        var vazba = await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id);
        vazba.KalkulaceCena.Should().Be(10340m);
        vazba.KalkulaceId.Should().Be(5414);
        vazba.KalkulaceNacteno.Should().Be(new DateTime(2026, 9, 10, 8, 0, 0));
        vazba.PredpokladanaCena.Should().Be(12000m, "předpokládaná cena se nikdy nepřepisuje (R1)");
    }

    [Fact]
    public async Task Sync_TiketBezAkceptovaneKalkulace_SnimekVynuluje()
    {
        var (db, id) = await SeedAsync(kalkulaceCena: 500m);
        using var _ = db;
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy[Cislo] = new HotZaznamDto(Cislo, "PNF", "s", "p", Pid: Pid);

        await Sut(db, ticketing).SyncAsync(new[] { id }, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id))
            .KalkulaceCena.Should().BeNull("kalkulace mohla být odvolána");
    }

    [Fact]
    public async Task Sync_NenalezenyTiket_SnimekNemeni()
    {
        var (db, id) = await SeedAsync(kalkulaceCena: 500m);
        using var _ = db;

        await Sut(db, new FakeTicketing()).SyncAsync(new[] { id }, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id))
            .KalkulaceCena.Should().Be(500m, "vypnutý ServiceDesk nesmí smazat známou cenu");
    }

    [Fact]
    public async Task Sync_VazbaJinehoTypuNezPnf_SeNecte()
    {
        var (db, id) = await SeedAsync(typKod: "NES");
        using var _ = db;
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy[Cislo] = new HotZaznamDto(Cislo, "NES", "s", "p", Pid: Pid);
        ticketing.Kalkulace[Pid] = Kalkulace(1, 999m);

        await Sut(db, ticketing).SyncAsync(new[] { id }, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id))
            .KalkulaceCena.Should().BeNull("snímek se týká jen PNF");
    }
}
```

- [ ] **Krok 2: Napsat padající test zapojení do harvestu**

Do `FingerprintDetectionTests` (používá jeho `InMemoryDb()` a mocky):

```csharp
    /// <summary>
    /// Snímek skutečné ceny se obnoví i na rychlé cestě, kdy fingerprint tiketu sedí a drill
    /// se přeskočí. Akceptace kalkulace fingerprint nemění (spec 2026-09-10 A3 R4).
    /// </summary>
    [Fact]
    public async Task HarvestSingleTicketAsync_ShodnyFingerprint_PresToObnoviSnimekKalkulace()
    {
        using var db = InMemoryDb();
        var datum = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 10, Cislo = "100001",
            LastKnownHotZaznamDatum = datum,
            LastKnownMaxVyjadreniId = 500L,
            LastKnownVyjadreniCount = 3
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", datum, "otevreno")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>
          {
              ["100001"] = new VyjadreniSecondaryFingerprintDto("100001", MaxId: 500L, Count: 3)
          });
        var snimek = new Mock<IKalkulaceSnapshotService>();

        var sut = new VyjadreniHarvestService(
            db, vq.Object,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero)),
            new PerExterniOdkazLockRegistry(),
            NullLogger<VyjadreniHarvestService>.Instance,
            kalkulaceSnapshot: snimek.Object);

        var result = await sut.HarvestSingleTicketAsync(externiOdkazId: 1, CancellationToken.None);

        result.Message.Should().Contain("Fingerprint", "jde o rychlou cestu bez drillu");
        snimek.Verify(s => s.SyncAsync(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(1)), It.IsAny<CancellationToken>()), Times.Once);
    }
```

Do `RecordServiceExternalLinkUpsertTests` pin R8:

```csharp
    /// <summary>Snímek skutečné ceny vlastní harvest — uložení záznamu na něj nesahá (R8).</summary>
    [Fact]
    public void RecordService_NesahaNaSnimekKalkulace()
    {
        LoadServiceSource().Should().NotContain("Kalkulace",
            "KalkulaceCena/Id/Nacteno plní jen IKalkulaceSnapshotService");
    }
```

- [ ] **Krok 3: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~KalkulaceSnapshotServiceTests|FullyQualifiedName~PresToObnoviSnimekKalkulace|FullyQualifiedName~NesahaNaSnimekKalkulace"`
Očekávej: chybu překladu — `IKalkulaceSnapshotService` neexistuje. Pin R8 sám projde (UPSERT dnes na snímek nesahá) — ověř, že není prázdný: dočasně napiš do UPSERTu `existingEntity.KalkulaceCena = null;`, pin musí selhat, řádek smaž.

- [ ] **Krok 4: Vytvořit službu**

`PmTracker.Web/Services/ServiceDesk/KalkulaceSnapshotService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.ServiceDesk;

public interface IKalkulaceSnapshotService
{
    /// <summary>Obnoví snímek skutečné ceny u daných vazeb. Jiné vazby než PNF přeskočí.</summary>
    Task SyncAsync(IReadOnlyCollection<int> externiOdkazIds, CancellationToken ct);
}

/// <summary>
/// Snímek skutečné ceny PNF z akceptované kalkulace (spec 2026-09-10 část A).
///
/// Běží vedle harvestu vyjádření, ne v něm: harvest má rychlou cestu podle fingerprintu
/// tiketu, jenže akceptace kalkulace mění HOT_KALKULACE, ne fingerprint. Za rychlou cestou
/// by se snímek po akceptaci nikdy neobnovil.
///
/// Pravidla (R6): tiket nenalezen → beze změny (vypnutý ServiceDesk nebo smazaný tiket);
/// tiket nalezen bez akceptované kalkulace → vynulovat (mohla být odvolána); jinak zapsat.
/// Kalkulace se vybírá stejně jako pro tisk výzvy — GetAkceptovaneKalkulaceAsync.
/// Čas se zapisuje jen při změně, jinak by periodický běh přepisoval řádky všech PNF.
/// </summary>
public sealed class KalkulaceSnapshotService : IKalkulaceSnapshotService
{
    private const string PnfKod = "PNF";

    private readonly PmTrackerDbContext _db;
    private readonly ITicketingQueryService _ticketing;
    private readonly TimeProvider _time;
    private readonly ILogger<KalkulaceSnapshotService> _logger;

    public KalkulaceSnapshotService(
        PmTrackerDbContext db,
        ITicketingQueryService ticketing,
        TimeProvider time,
        ILogger<KalkulaceSnapshotService> logger)
    {
        _db = db;
        _ticketing = ticketing;
        _time = time;
        _logger = logger;
    }

    public async Task SyncAsync(IReadOnlyCollection<int> externiOdkazIds, CancellationToken ct)
    {
        if (externiOdkazIds.Count == 0) return;

        var pnfTypId = await _db.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(t => t.Kod == PnfKod)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (pnfTypId is null) return;

        var ids = externiOdkazIds.Distinct().ToArray();
        var vazby = await _db.ZaznamExterniOdkazy
            .Where(x => ids.Contains(x.Id) && x.TypOdkazuId == pnfTypId.Value && x.Cislo != "")
            .ToListAsync(ct).ConfigureAwait(false);
        if (vazby.Count == 0) return;

        var hot = await _ticketing
            .GetZaznamyAsync(vazby.Select(v => v.Cislo).Distinct().ToArray(), ct)
            .ConfigureAwait(false);
        var pidy = hot.Values
            .Select(h => h.Pid)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct()
            .ToArray();
        var kalkulace = await _ticketing.GetAkceptovaneKalkulaceAsync(pidy, ct).ConfigureAwait(false);

        var ted = _time.GetUtcNow().UtcDateTime;
        var zmeneno = 0;
        foreach (var vazba in vazby)
        {
            if (!hot.TryGetValue(vazba.Cislo, out var tiket)) continue;

            var k = string.IsNullOrWhiteSpace(tiket.Pid) ? null : kalkulace.GetValueOrDefault(tiket.Pid!);
            var cena = k?.CenaCelkem;
            long? kalkulaceId = k?.Id;
            if (vazba.KalkulaceCena == cena && vazba.KalkulaceId == kalkulaceId) continue;

            vazba.KalkulaceCena = cena;
            vazba.KalkulaceId = kalkulaceId;
            vazba.KalkulaceNacteno = ted;
            zmeneno++;
        }

        if (zmeneno > 0)
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("KalkulaceSnapshot: změněn snímek u {Pocet} PNF vazeb.", zmeneno);
        }
    }
}
```

- [ ] **Krok 5: Zapojit do harvestu**

Ve `VyjadreniHarvestService`:

- pole `private readonly IKalkulaceSnapshotService? _kalkulaceSnapshot;`
- konstruktor dostane jako **poslední** parametr `IKalkulaceSnapshotService? kalkulaceSnapshot = null`
  a přiřadí `_kalkulaceSnapshot = kalkulaceSnapshot;` — existující testy se nemění,
- v `HarvestSingleTicketAsync` hned za blok `if (!fingerprints.TryGetValue(...)) { return …; }`:

```csharp
        // Spec 2026-09-10 A3 R4: snímek skutečné ceny PŘED rozhodnutím podle fingerprintu.
        // Akceptace kalkulace mění HOT_KALKULACE, ne fingerprint tiketu — za rychlou cestou
        // by se po akceptaci nikdy neobnovil. Běží i po uložení záznamu: SaveRecord plánuje
        // harvest každé vazby (R5).
        await SyncKalkulaceBestEffortAsync(new[] { eo.Id }, ct).ConfigureAwait(false);
```

- v `HarvestScopeAsync` za smyčkou `foreach (var eo in scopedCandidates) { … }`:

```csharp
        // Snímek skutečné ceny dávkově pro celý rozsah, nezávisle na fingerprintu (A3 R4).
        await SyncKalkulaceBestEffortAsync(scopedCandidates.Select(x => x.Id).ToArray(), ct).ConfigureAwait(false);
```

- pomocník k ostatním privátním metodám:

```csharp
    private async Task SyncKalkulaceBestEffortAsync(IReadOnlyCollection<int> externiOdkazIds, CancellationToken ct)
    {
        if (_kalkulaceSnapshot is null) return;

        try
        {
            await _kalkulaceSnapshot.SyncAsync(externiOdkazIds, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort jako metadata sync: chyba snímku nesmí shodit harvest vyjádření.
            _logger.LogWarning(ex, "VyjadreniHarvestService: snímek kalkulace selhal pro {Pocet} vazeb.",
                externiOdkazIds.Count);
        }
    }
```

- [ ] **Krok 6: Registrace v DI**

V `DataStoreServiceCollectionExtensions.cs` za registraci `IPerTicketMetadataSyncService`:

```csharp
        // 2026-09-10: snímek skutečné ceny z kalkulace. Volaný z VyjadreniHarvestService
        // před rozhodnutím podle fingerprintu (spec 2026-09-10 A3 R4).
        services.AddScoped<IKalkulaceSnapshotService, KalkulaceSnapshotService>();
```

Volitelný parametr konstruktoru DI naplní, jakmile je služba registrovaná.

- [ ] **Krok 7: Spustit a ověřit průchod**

Příkaz z kroku 3 — všechny zelené.

- [ ] **Krok 8: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
```

Očekávej: build 0/0, Unit zelená, Api jen 4 známá selhání (Api běží s vypnutým ServiceDeskem,
snímek tam vždy skončí na „tiket nenalezen").

---
## Blok 5: Skutečná cena v aplikaci a v exportu záznamů

Spec A2, A3 R7. Chip (obě varianty a tooltip), karta PNF a patička ve Výzvách, export záznamů.

**Soubory:**
- Upravit: `PmTracker.Web/Models/ViewModels/Projekty/ProjektZaznamyTabViewModels.cs` (`ExterniOdkazViewModel`)
- Upravit: `PmTracker.Web/Services/ProjectService.RecordCards.cs`, `PmTracker.Web/Services/ProjectService.LazyQueries.cs`
- Upravit: `PmTracker.Web/Views/Projekty/_ZaznamDetailPartial.cshtml`
- Upravit: `PmTracker.Web/Services/Vyzvy/Contracts/VyzvaBufferItem.cs`, `VyzvaDetail.cs`
- Upravit: `PmTracker.Web/Services/Vyzvy/VyzvaService.Queries.cs`, `VyzvyPanelBuilder.cs`
- Upravit: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvyPanelViewModels.cs`
- Upravit: `PmTracker.Web/Views/Projekty/_VyzvyPane.cshtml`
- Upravit: `PmTracker.Web/Services/Export/ExportProjectionBuilders.cs`
- Test: `PmTracker.Tests.Api/Controllers/SkutecnaCenaRenderTests.cs` (nový)
- Test: `PmTracker.Tests.Integration/DataStore/ProjectMembershipDataStoreTests.cs`

- [ ] **Krok 1: Napsat padající Api testy**

`PmTracker.Tests.Api/Controllers/SkutecnaCenaRenderTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Skutečná cena z kalkulace má přednost před předpokládanou (spec 2026-09-10 A3 R7).
/// Kotví se na data-cena-zdroj a ASCII — Razor kóduje diakritiku i nezlomitelnou mezeru.
/// Snímek se seeduje přímo do DB: Api sada běží s vypnutým ServiceDeskem.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class SkutecnaCenaRenderTests
{
    private const string Marker = "APICENA";
    private const string Smlouva = "APICENA-SML";

    private readonly ApiSqlFixture _fixture;

    public SkutecnaCenaRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Seed(int ProjectId, int RecordId, int VyzvaId);

    /// <summary>Idempotentní — Api testy sdílí jednu databázi.</summary>
    private async Task<Seed> SeedAsync()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCenaOwner");
        var projectId = await _fixture.EnsureProjectAsync(Marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync(Marker + "_SUB", ownerId);

        await using var db = _fixture.CreateDbContext();

        var projekt = await db.Projekty.FirstAsync(p => p.Id == projectId);
        projekt.MistoPlneni = "FIS (EIS): VZ 8201";
        projekt.CisloRamcoveSmlouvy = Smlouva;
        await db.SaveChangesAsync();

        const string nazev = "API zaznam se skutecnou cenou";
        var recordId = await db.ProjektoveZaznamy.Where(z => z.Nazev == nazev)
            .Select(z => (int?)z.Id).FirstOrDefaultAsync()
            ?? await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", nazev);

        var vyzva = await db.Vyzvy.FirstOrDefaultAsync(v => v.ProjektId == projectId && v.Rok == 2026 && v.PoradoveVRoce == 901);
        if (vyzva is null)
        {
            vyzva = new VyzvaEntity
            {
                ProjektId = projectId, Kod = "901/2026", PoradoveVRoce = 901, Rok = 2026,
                Stav = VyzvaStav.Priprava, DatumZalozeni = new DateTime(2026, 9, 1), ZalozilOsobaId = ownerId,
                MistoPlneniSnapshot = "FIS (EIS): VZ 8201", CisloRamcoveSmlouvySnapshot = Smlouva,
            };
            db.Vyzvy.Add(vyzva);
            await db.SaveChangesAsync();
        }

        if (!await db.ZaznamExterniOdkazy.AnyAsync(x => x.Cislo == "941901"))
        {
            var pnf = await db.CiselnikTypuExternichOdkazu.Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
            db.ZaznamExterniOdkazy.AddRange(
                new ZaznamExterniOdkazEntity
                {
                    ZaznamId = recordId, TypOdkazuId = pnf, Cislo = "941901",
                    PredpokladanaCena = 5000m, KalkulaceCena = 1000m, KalkulaceId = 77,
                    ZaradidDoVyzvy = true, VyzvaId = vyzva.Id,
                },
                new ZaznamExterniOdkazEntity
                {
                    ZaznamId = recordId, TypOdkazuId = pnf, Cislo = "941902",
                    PredpokladanaCena = 200m,
                    ZaradidDoVyzvy = true, VyzvaId = vyzva.Id,
                });
            await db.SaveChangesAsync();
        }

        return new Seed(projectId, recordId, vyzva.Id);
    }

    [Fact]
    public async Task Chip_UkazeSkutecnouCenu_AJinakPredpokladanou()
    {
        var s = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Zaznamy/RecordDetailPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-cena-zdroj=\"kalkulace\"", "PNF se známou kalkulací ukáže skutečnou cenu");
        html.Should().Contain("data-cena-zdroj=\"predpokladana\"", "PNF bez kalkulace ukáže předpokládanou");
    }

    [Fact]
    public async Task VyzvyPanel_KartaASoucetBerouSkutecnouCenu()
    {
        var s = await SeedAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{s.ProjectId}?rok=2026&asUser={_fixture.AdminOsobaId}");
        var pane = VyzvyPanelHtml.VyzvaPane(html, s.VyzvaId);

        pane.Should().Contain("data-cena-zdroj=\"kalkulace\"");
        pane.Should().Contain("data-cena-zdroj=\"predpokladana\"");
        pane.Should().Contain("data-vyzvy-celkem=\"1200.00\"",
            "součet = skutečná 1000 + předpokládaná 200, ne 5000 + 200");
        pane.Should().Contain("data-vyzvy-soucet-predpokladane=\"1\"",
            "jedno PNF v součtu nese jen předpokládanou cenu");
    }
}
```

- [ ] **Krok 2: Napsat padající Integration test exportu záznamů**

Do `ProjectMembershipDataStoreTests` za test `TaskPrintTemplate_ShouldIncludePlannedDeliveryDateOnlyForExternalLinksThatHaveIt`:

```csharp
    /// <summary>
    /// Předpokládaná cena do PDF ani Wordu nesmí (uživatel 2026-09-10). Export ukáže jen
    /// skutečnou cenu z kalkulace; vazba bez ní je bez ceny.
    /// </summary>
    [Fact]
    public async Task TaskPrintTemplate_ExterniVazba_UkazeJenSkutecnouCenu()
    {
        var db = await _fixture.CreateDatabaseAsync("export_external_real_price");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportRealPriceAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportRealPriceOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPCENA");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPCENA_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportRealPriceRecord");
        await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9611);

        var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var pmpTypeId = await dbContext.CiselnikTypuExternichOdkazu.Where(x => x.Kod == "PMP").Select(x => x.Id).FirstAsync();

        dbContext.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId, TypOdkazuId = pnfTypeId, Cislo = "941903",
                PredpokladanaCena = 12000m, KalkulaceCena = 10340m,
            },
            new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId, TypOdkazuId = pmpTypeId, Cislo = "PMP-777",
                PredpokladanaCena = 500m,
            });
        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);
        var externalLinks = model.Zaznamy.Single().ExterniVazby;

        var skutecna = 10340m.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("cs-CZ"));
        externalLinks.Should().Contain($"PNF 941903 ({skutecna} Kč)", "export ukáže skutečnou cenu");
        externalLinks.Should().Contain("PMP PMP-777", "předpokládaná cena se do exportu nedostane");
        externalLinks.Should().NotContain(l => l.Contains("12", StringComparison.Ordinal) && l.Contains("000,00", StringComparison.Ordinal));
    }
```

- [ ] **Krok 3: Spustit a ověřit selhání**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~SkutecnaCenaRenderTests"
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj --filter "FullyQualifiedName~UkazeJenSkutecnouCenu"
```

Očekávej: oba Api testy selžou (atributy neexistují), Integration test selže — export dnes vypíše
předpokládané `12 000,00 Kč` a `500,00 Kč`.

- [ ] **Krok 4: Chip**

Do `ExterniOdkazViewModel` za `PredpokladanaCena`:

```csharp
    /// <summary>Skutečná cena z akceptované kalkulace (snímek z harvestu) — má přednost.</summary>
    public decimal? KalkulaceCena { get; init; }
```

V `ProjectService.RecordCards.cs` i `ProjectService.LazyQueries.cs` za `PredpokladanaCena = link.PredpokladanaCena,`
přidej `KalkulaceCena = link.KalkulaceCena,`. Obě místa plní týž view model — vynechání jednoho
by se projevilo jen na jedné ze tří cest ke chipu.

V `_ZaznamDetailPartial.cshtml` nahraď výpočet `estimatedPriceLabel`:

```cshtml
            // Skutečná cena z kalkulace má přednost (spec 2026-09-10 A3 R7). Předpokládaná jen
            // s příponou, aby ji nikdo nečetl jako konečnou.
            var csCz = CultureInfo.GetCultureInfo("cs-CZ");
            var realPriceLabel = odkaz.KalkulaceCena.HasValue
                ? $"{odkaz.KalkulaceCena.Value.ToString("N2", csCz)} Kč"
                : null;
            var estimatedPriceLabel = odkaz.PredpokladanaCena.HasValue
                ? $"{odkaz.PredpokladanaCena.Value.ToString("N2", csCz)} Kč"
                : null;
            var chipPriceLabel = realPriceLabel
                ?? (estimatedPriceLabel is null ? null : $"{estimatedPriceLabel} (předp.)");
            var chipPriceSource = realPriceLabel is not null ? "kalkulace" : "predpokladana";
```

V **obou** variantách chipu (`chip-link` i `span`) nahraď blok

```cshtml
                        @if (!string.IsNullOrWhiteSpace(estimatedPriceLabel))
                        {
                            <span class="chip-sub">@estimatedPriceLabel</span>
                        }
```

za

```cshtml
                        @if (!string.IsNullOrWhiteSpace(chipPriceLabel))
                        {
                            <span class="chip-sub" data-cena-zdroj="@chipPriceSource">@chipPriceLabel</span>
                        }
```

V tooltipu nahraď řádek „Předpokl. cena" dvěma řádky:

```cshtml
                    <span class="chip-tooltip-row">
                        <span>Cena z kalkulace:</span>
                        <strong>@(realPriceLabel ?? "-")</strong>
                    </span>
                    <span class="chip-tooltip-row">
                        <span>Předpokl. cena:</span>
                        <strong>@(estimatedPriceLabel ?? "-")</strong>
                    </span>
```

- [ ] **Krok 5: Výzvy — kontrakty a dotazy**

`VyzvaBufferItem` a `VyzvaDetailItem` dostanou poslední poziční parametr s výchozí hodnotou —
existující konstrukce v testech zůstanou platné:

```csharp
    decimal? PredpokladanaCena,
    decimal? KalkulaceCena = null);
```

Ve `VyzvaService.Queries.cs`:

- `GetBufferAsync`: projekce `.Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.PredpokladanaCena, ev.KalkulaceCena })`
  a konstrukce `new VyzvaBufferItem(r.Id, r.ZaznamId, r.Cislo, hot.GetValueOrDefault(r.Cislo)?.Strucne, r.PredpokladanaCena, r.KalkulaceCena)`,
- `MapToDetail`: `new VyzvaDetailItem(ev.Id, ev.ZaznamId, ev.Cislo, hot.GetValueOrDefault(ev.Cislo)?.Strucne, ev.PredpokladanaCena, ev.KalkulaceCena)`.

- [ ] **Krok 6: Výzvy — view model a builder**

Do `VyzvyPanelPolozkaViewModel` za `PredpokladanaCena`:

```csharp
    public decimal? KalkulaceCena { get; init; }
```

Do `VyzvyPanelVyzvaViewModel` za `CelkovaCena`:

```csharp
    /// <summary>Kolik PNF v součtu nese jen předpokládanou cenu (spec 2026-09-10 R7).</summary>
    public int PocetJenPredpokladanych { get; init; }
```

Ve `VyzvyPanelBuilder` v obou `ToPolozka` přidej `KalkulaceCena = item.KalkulaceCena,` a v `ToVyzva`
nahraď výpočet součtu:

```csharp
        // Součet ze skutečných cen; kde kalkulace chybí, dopočte se předpokládaná
        // a počítá se zvlášť, aby smíšený součet nikdo nečetl jako konečný (R7).
        decimal? celkova = null;
        var jenPredpokladane = 0;
        foreach (var p in detail.Polozky)
        {
            var cena = p.KalkulaceCena ?? p.PredpokladanaCena;
            if (!cena.HasValue) continue;

            celkova = (celkova ?? 0) + cena.Value;
            if (!p.KalkulaceCena.HasValue) jenPredpokladane++;
        }
```

a do inicializátoru výsledku doplň `PocetJenPredpokladanych = jenPredpokladane,`.

- [ ] **Krok 7: Výzvy — karta a patička**

V `_VyzvyPane.cshtml` nahraď `<span class="vyzvy-pnf-meta">…</span>`:

```cshtml
                                @* Skutečná cena z kalkulace má přednost (spec 2026-09-10 A3 R7). *@
                                <span class="vyzvy-pnf-meta">
                                    @if (p.KalkulaceCena is decimal skutecna)
                                    {
                                        <text>Cena:</text>
                                        <span class="vyzvy-pnf-cena" data-cena-zdroj="kalkulace">@skutecna.ToString("N2", css)</span>
                                        <text>Kč</text>
                                    }
                                    else
                                    {
                                        <text>Předpokládaná cena:</text>
                                        <span class="vyzvy-pnf-cena" data-cena-zdroj="predpokladana">@(p.PredpokladanaCena?.ToString("N2", css) ?? "—")</span>
                                        <text>Kč</text>
                                    }
                                </span>
```

a v patičce odstavec součtu:

```cshtml
                <p class="vyzvy-pane-soucet">
                    CELKEM:
                    <strong data-vyzvy-celkem="@celkem.ToString(CultureInfo.InvariantCulture)">@celkem.ToString("N2", css) Kč</strong>
                    @if (Model.Vyzva.PocetJenPredpokladanych > 0)
                    {
                        <span class="vyzvy-pane-soucet-poznamka"
                              data-vyzvy-soucet-predpokladane="@Model.Vyzva.PocetJenPredpokladanych">
                            (z toho @Model.Vyzva.PocetJenPredpokladanych PNF jen s předpokládanou cenou)
                        </span>
                    }
                </p>
```

Pokud partial nemá `@using System.Globalization`, doplň ho na začátek.

- [ ] **Krok 8: Export záznamů jen se skutečnou cenou**

V `ExportProjectionBuilders.cs` změň volání v `BuildExportRecordsAsync`:

```csharp
                    // Předpokládaná cena do PDF ani Wordu nesmí (uživatel 2026-09-10) — jen skutečná.
                    return FormatExternalLinkDisplay(typeCode, link.Cislo, link.KalkulaceCena, link.PlanDodani);
```

a přejmenuj parametr i pomocníka, ať název nelže:

```csharp
    private static string FormatExternalLinkDisplay(string typeCode, string number, decimal? realPrice, DateTime? plannedDelivery)
    {
        var header = $"{typeCode} {number}".Trim();
        var details = new List<string>();
        if (realPrice.HasValue)
        {
            details.Add(FormatPrice(realPrice.Value));
        }
```

a `FormatEstimatedPrice` → `FormatPrice` (tělo beze změny).

- [ ] **Krok 9: Spustit a ověřit průchod**

Příkazy z kroku 3 — zelené.

- [ ] **Krok 10: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj
```

Očekávej: build 0/0, Unit zelená, Api a Integration jen známá selhání.

---
## Blok 6: Parser nese druh a identitu seznamu

Spec B8. Word výzvy potřebuje skutečné seznamy; parser dnes vrací odrážku jen jako znak v prvním
tokenu. Doplní se druh a identita seznamu, **tokeny zůstávají beze změny** — export záznamu značku
dál sází z tokenu a musí zůstat chováním neutrální.

**Soubory:**
- Upravit: `PmTracker.Web/Services/Export/RichTextHtmlParser.cs`
- Test: `PmTracker.Tests.Unit/Export/RichTextHtmlParserTests.cs`

- [ ] **Krok 1: Napsat padající testy**

Do `RichTextHtmlParserTests`:

```csharp
    [Fact]
    public void Parse_Odstavec_NeniSeznam()
    {
        RichTextHtmlParser.Parse("<p>text</p>").Should().ContainSingle()
            .Which.ListKind.Should().Be(RichTextListKind.None);
    }

    /// <summary>
    /// U položky seznamu je první token vždy značka. Word výzvy ji přeskočí a kreslí seznam
    /// sám; export záznamu ji sází dál. Tahle smlouva drží oba konzumenty pohromadě.
    /// </summary>
    [Fact]
    public void Parse_Odrazky_MajiDruhBulletSpolecneIdAZnackuVPrvnimTokenu()
    {
        var odstavce = RichTextHtmlParser.Parse("<ul><li>prvni</li><li>druhy</li></ul>");

        odstavce.Should().HaveCount(2);
        odstavce.Should().OnlyContain(p => p.ListKind == RichTextListKind.Bullet);
        odstavce.Select(p => p.ListId).Distinct().Should().ContainSingle("obě položky jsou jeden seznam");
        odstavce[0].Tokens[0].Text.Should().Be("• ");
        odstavce[0].Tokens[1].Text.Should().Be("prvni");
    }

    [Fact]
    public void Parse_CislovanySeznam_MaDruhOrdered()
    {
        var odstavce = RichTextHtmlParser.Parse("<ol><li>a</li><li>b</li></ol>");

        odstavce.Should().OnlyContain(p => p.ListKind == RichTextListKind.Ordered);
        odstavce[1].Tokens[0].Text.Should().Be("2. ", "značka pro export záznamu se nemění");
    }

    [Fact]
    public void Parse_DvaSeznamyZaSebou_MajiRuzneId()
    {
        var odstavce = RichTextHtmlParser.Parse("<ol><li>a</li></ol><p>mezi</p><ol><li>b</li></ol>");

        var id = odstavce.Where(p => p.ListKind == RichTextListKind.Ordered).Select(p => p.ListId).ToArray();
        id.Should().HaveCount(2);
        id[0].Should().NotBe(id[1], "každý číslovaný seznam ve Wordu začíná od 1.");
    }

    /// <summary>
    /// Quill 2 dává odrážky i čísla do jednoho &lt;ol&gt; a liší je atributem data-list.
    /// Změna druhu uvnitř elementu je pro Word nový seznam.
    /// </summary>
    [Fact]
    public void Parse_Quill2SmisenySeznam_ZmenaDruhuZacinaNovySeznam()
    {
        var odstavce = RichTextHtmlParser.Parse(
            "<ol><li data-list=\"bullet\">a</li><li data-list=\"ordered\">b</li></ol>");

        odstavce.Select(p => p.ListKind).Should().Equal(RichTextListKind.Bullet, RichTextListKind.Ordered);
        odstavce[0].ListId.Should().NotBe(odstavce[1].ListId);
    }
```

- [ ] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RichTextHtmlParserTests"`
Očekávej: chybu překladu — `RichTextListKind` neexistuje.

- [ ] **Krok 3: Rozšířit typy**

V `RichTextHtmlParser.cs` nahraď `RichTextParagraph` a přidej výčet:

```csharp
/// <summary>Druh seznamu, ke kterému odstavec patří.</summary>
public enum RichTextListKind
{
    None,
    Bullet,
    Ordered,
}

/// <summary>
/// Jeden odstavec: úroveň odsazení a běhy textu. U položky seznamu nese i druh a identitu
/// seznamu (odstavce téhož seznamu mají stejné ListId) a první token je vždy značka.
/// </summary>
public readonly record struct RichTextParagraph(
    int IndentLevel,
    IReadOnlyList<RichTextToken> Tokens,
    RichTextListKind ListKind = RichTextListKind.None,
    int ListId = 0);
```

Výchozí hodnoty drží všechny dnešní konstrukce `new RichTextParagraph(level, tokens)` platné.

- [ ] **Krok 4: Plnit druh a identitu v parseru**

Do třídy `RichTextHtmlParser` přidej počítadlo:

```csharp
    /// <summary>Sdílené přes celé Parse — každý seznam dostane vlastní ListId.</summary>
    private sealed class ListCounter
    {
        public int Posledni { get; set; }
    }
```

V `Parse` před smyčku `foreach (var node in root.Nodes())` přidej `var listCounter = new ListCounter();`
a volání `AppendListParagraphs(listElement, paragraphs, baseIndentLevel: 0);` změň na
`AppendListParagraphs(listElement, paragraphs, baseIndentLevel: 0, listCounter);`.

V `AppendListParagraphs` změň signaturu na
`(XElement listElement, List<RichTextParagraph> paragraphs, int baseIndentLevel, ListCounter counter)`,
obě rekurzivní volání doplň o `counter` a za `var orderedIndex = 0;` přidej:

```csharp
        string? predchoziTag = null;
        var listId = 0;
```

Za řádek `var resolvedListTag = ResolveListTag(defaultListTag, listItemElement);` vlož:

```csharp
            // Quill 2 dává odrážky i čísla do jednoho <ol> a liší je data-list. Změna druhu
            // uvnitř elementu je pro Word nový seznam — číslování musí začít znovu od 1.
            if (resolvedListTag != predchoziTag)
            {
                listId = ++counter.Posledni;
                predchoziTag = resolvedListTag;
            }
```

a přidání odstavce `paragraphs.Add(new RichTextParagraph(itemIndentLevel, tokens));` nahraď:

```csharp
            paragraphs.Add(new RichTextParagraph(
                itemIndentLevel,
                tokens,
                resolvedListTag == "ol" ? RichTextListKind.Ordered : RichTextListKind.Bullet,
                listId));
```

Tvorba značky (`marker`, `orderedIndex`) zůstává beze změny.

- [ ] **Krok 5: Spustit a ověřit průchod i neutralitu**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RichTextHtmlParserTests"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlWordExportServiceTests|FullyQualifiedName~OpenXmlWordExportSplitTests"
```

Očekávej: nové testy zelené a **exportní sady záznamu zelené beze změny**. Kdyby bylo nutné upravit
test exportu záznamu, změna přestala být neutrální — vrať se a zjisti proč.

- [ ] **Krok 6: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
```

---
## Blok 7: Projekce výzvy podle vzoru

Spec B4, B5, B6. Římské pořadí, prefix „RU", příznaky činností a licence, řádky licence
z `rozpad_licence`, bez vazby na PMP. Obsah dokumentu se mění až v blocích 8 a 9.

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Vyzvy/RozpadLicenceParser.cs`
- Upravit: `PmTracker.Web/Models/ViewModels/Vyzvy/VyzvaExportViewModels.cs`
- Upravit (celý soubor): `PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs`
- Test: `PmTracker.Tests.Unit/Vyzvy/RozpadLicenceParserTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/Vyzvy/VyzvaExportBuilderTests.cs`

- [ ] **Krok 1: Napsat padající testy parseru licence**

`PmTracker.Tests.Unit/Vyzvy/RozpadLicenceParserTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// HOT_KALKULACE.rozpad_licence je HTML tabulka „název | cena", kterou píše ServiceDesk
/// (spec 2026-09-10 B5). Vzorek níž je doslova z produkce (id 5414).
/// </summary>
public sealed class RozpadLicenceParserTests
{
    private const string VzorekZProdukce =
        "<table style='width: 100%;'  cellspacing='0' cellpadding='0'><tr><td>RZA Registr zakázek – rozšíření – evidenční přenos VZ do NEN</td><td align='right'>10340,00</td></tr></table>";

    [Fact]
    public void Parse_VzorekZProdukce_JedenRadek()
    {
        var polozka = RozpadLicenceParser.Parse(VzorekZProdukce).Should().ContainSingle().Which;

        polozka.Nazev.Should().Be("RZA Registr zakázek – rozšíření – evidenční přenos VZ do NEN");
        polozka.Cena.Should().Be(10340m);
    }

    [Fact]
    public void Parse_ViceRadku_ZachovaPoradi()
    {
        var html = "<table><tr><td>XRG – RSS Rozhraní</td><td>94 470,00</td></tr>"
                 + "<tr><td>XRG - ESS Rozhraní</td><td>94&nbsp;470,00</td></tr></table>";

        var polozky = RozpadLicenceParser.Parse(html);

        polozky.Select(p => p.Nazev).Should().Equal("XRG – RSS Rozhraní", "XRG - ESS Rozhraní");
        polozky.Should().OnlyContain(p => p.Cena == 94470m, "mezera i &nbsp; jsou oddělovač tisíců");
    }

    [Fact]
    public void Parse_EntityVNazvu_SeDekoduji()
    {
        RozpadLicenceParser.Parse("<tr><td>A &amp; B &ndash; C</td><td>1,50</td></tr>")
            .Should().ContainSingle().Which.Nazev.Should().Be("A & B – C");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<table></table>")]
    [InlineData("<tr><td>bez ceny</td><td>neni cislo</td></tr>")]
    public void Parse_NecitelnyVstup_VraciPrazdno(string? html)
    {
        RozpadLicenceParser.Parse(html).Should().BeEmpty();
    }
}
```

- [ ] **Krok 2: Napsat padající testy builderu**

V `VyzvaExportBuilderTests`:

1. Test na řádku 97 přejmenuj na `Build_PozadavkyMajiRimskaPoradovaCislaAUdajeZeZaznamu`
   a assertaci změň na `.Should().Equal(new[] { "I.", "II." });`. Assertace `RU100` zůstává —
   harness seeduje `CisloViditelne = "RU100"` a pojistka proti zdvojení ho nechá být.
2. Test `Build_VazbaNaPmpZeZaznamu` **smaž celý** — vazba na PMP se do výzvy nedostane
   (spec B4, rozhodnutí uživatele). Hlídá to Word test v bloku 8.
3. Do `Build_PnfBezKalkulace_MaPrazdneRadkyNeNull` přidej:

```csharp
        k.MaCinnosti.Should().BeFalse("bez akceptované kalkulace se netiskne žádná tabulka");
        k.MaLicenci.Should().BeFalse();
```

4. Na konec třídy:

```csharp
    [Theory]
    [InlineData(0, "I.")]
    [InlineData(3, "IV.")]
    [InlineData(8, "IX.")]
    [InlineData(17, "XVIII.")]
    [InlineData(39, "XL.")]
    public void PoradoveOznaceni_JeRimskySTeckou(int index, string ocekavane)
    {
        VyzvaExportBuilder.PoradoveOznaceni(index).Should().Be(ocekavane);
    }

    [Theory]
    [InlineData("867-5", "RU867-5")]
    [InlineData("123", "RU123")]
    [InlineData("RU867-5", "RU867-5")]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void CisloUkoluVp_DostanePrefixRUBezZdvojeni(string? cisloViditelne, string? ocekavane)
    {
        VyzvaExportBuilder.CisloUkoluVp(cisloViditelne).Should().Be(ocekavane);
    }

    private static HotKalkulaceDto KalkulaceJenLicence(string pid, decimal cenaL, string? rozpad) => new(
        5414, pid,
        PracnostAnalyza: 0m, SazbaAnalyza: 0m, CenaAnalyza: 0m,
        PracnostProgramovani: 0m, SazbaProgramovani: 0m, CenaProgramovani: 0m,
        PracnostTestovani: 0m, SazbaTestovani: 0m, CenaTestovani: 0m,
        PracnostImplementace: 0m, SazbaImplementace: 0m, CenaImplementace: 0m,
        CenaCelkem: cenaL,
        PocetLicenci: 1m, SazbaLicence: cenaL, CenaLicence: cenaL, RozpadLicence: rozpad,
        TextTermin: null);

    /// <summary>Čistě licenční kalkulace: tabulka licencí ano, tabulka činností ne (spec B5).</summary>
    [Fact]
    public async Task Build_JenLicence_RadkyZRozpadu()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "s", "p", Pid: "P1");
        ticketing.Kalkulace["P1"] = KalkulaceJenLicence("P1", 188940m,
            "<table><tr><td>XRG – RSS</td><td>94470,00</td></tr><tr><td>XRG - ESS</td><td>94470,00</td></tr></table>");

        var k = (await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None))!
            .Pozadavky.Single().Kalkulace;

        k.MaCinnosti.Should().BeFalse();
        k.MaLicenci.Should().BeTrue();
        k.LicenceRadky.Select(r => r.Kod).Should().Equal(1, 2);
        k.LicenceRadky.Select(r => r.Nazev).Should().Equal("XRG – RSS", "XRG - ESS");
        k.LicenceRadky[0].CenaDph.Should().Be(19838.70m, "DPH 21 %");
        k.CenaLicence.Should().Be(188940m);
    }

    /// <summary>Bez čitelného rozpadu jeden řádek s cena_l (spec B5).</summary>
    [Fact]
    public async Task Build_LicenceBezRozpadu_JedenRadekZCenyL()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "s", "p", Pid: "P1");
        ticketing.Kalkulace["P1"] = KalkulaceJenLicence("P1", 10340m, rozpad: null);

        var radek = (await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None))!
            .Pozadavky.Single().Kalkulace.LicenceRadky.Should().ContainSingle().Which;

        radek.Nazev.Should().Be("Licenční rozšíření");
        radek.CenaBezDph.Should().Be(10340m);
    }

    [Fact]
    public async Task Build_CinnostiBezLicence_MaJenCinnosti()
    {
        using var db = await SeedAsync((100, "336865"));
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy["336865"] = new HotZaznamDto("336865", "PNF", "s", "p", Pid: "P1");
        ticketing.Kalkulace["P1"] = Kalkulace("P1");

        var k = (await Builder(db, ticketing).BuildAsync(VyzvaId, CancellationToken.None))!
            .Pozadavky.Single().Kalkulace;

        k.MaCinnosti.Should().BeTrue();
        k.MaLicenci.Should().BeFalse();
        k.LicenceRadky.Should().BeEmpty();
    }
```

`Kalkulace(pid)` a `FakeTicketing` už v souboru jsou. Pokud `HotZaznamDto` v testech dnes nemá
pojmenovaný argument `Pid:`, je to poziční parametr na konci s výchozí hodnotou — pojmenování platí.

- [ ] **Krok 3: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~RozpadLicenceParserTests|FullyQualifiedName~VyzvaExportBuilderTests"`
Očekávej: chybu překladu — `RozpadLicenceParser`, `MaCinnosti`, `LicenceRadky`, `CisloUkoluVp(...)` neexistují.

- [ ] **Krok 4: Parser licence**

`PmTracker.Web/Services/Vyzvy/RozpadLicenceParser.cs`:

```csharp
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace PmTracker.Web.Services.Vyzvy;

/// <summary>
/// Čte HOT_KALKULACE.rozpad_licence — HTML tabulku „název | cena", kterou píše ServiceDesk
/// (spec 2026-09-10 B5). Regulárním výrazem, ne XML parserem: stará aplikace píše atributy
/// v apostrofech a HTML entity (&amp;ndash;), na kterých XDocument padá.
/// Nečitelný vstup vrací prázdný seznam — builder pak tiskne jeden řádek s cena_l.
/// </summary>
public static partial class RozpadLicenceParser
{
    public sealed record Polozka(string Nazev, decimal Cena);

    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    [GeneratedRegex(@"<tr\b[^>]*>(?<radek>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RadekRegex();

    [GeneratedRegex(@"<td\b[^>]*>(?<bunka>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BunkaRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex ZnackaRegex();

    public static IReadOnlyList<Polozka> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Array.Empty<Polozka>();

        var polozky = new List<Polozka>();
        foreach (Match radek in RadekRegex().Matches(html))
        {
            var bunky = BunkaRegex().Matches(radek.Groups["radek"].Value)
                .Select(b => Text(b.Groups["bunka"].Value))
                .ToArray();
            if (bunky.Length < 2) continue;

            var nazev = bunky[0];
            if (nazev.Length == 0 || !TryCena(bunky[^1], out var cena)) continue;

            polozky.Add(new Polozka(nazev, cena));
        }

        return polozky;
    }

    private static string Text(string bunka)
    {
        var bezZnacek = WebUtility.HtmlDecode(ZnackaRegex().Replace(bunka, string.Empty));
        return string.Join(' ', bezZnacek.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool TryCena(string text, out decimal cena)
    {
        // char.IsWhiteSpace pokrývá i nezlomitelnou mezeru (oddělovač tisíců v cs-CZ).
        var cista = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return decimal.TryParse(cista, NumberStyles.Number, Cs, out cena)
            || decimal.TryParse(cista, NumberStyles.Number, CultureInfo.InvariantCulture, out cena);
    }
}
```

- [ ] **Krok 5: View model**

V `VyzvaExportViewModels.cs`:

- u `VyzvaExportPozadavekViewModel` změň komentář `PoradoveOznaceni` na
  `/// <summary>Poř. č. římsky s tečkou: „I.", „II.", … (komentář autora vzoru 2026-09-10).</summary>`,
  ke `CisloUkoluVp` přidej `/// <summary>Č. úkolu VP s prefixem „RU", např. „RU867-5". Null bez čísla.</summary>`
  a **smaž** `public string? VazbaPmp { get; init; }`;
- do `VyzvaExportKalkulaceViewModel` za `CelkemSDph`:

```csharp
    /// <summary>Součet cena_a až cena_i je kladný — tiskne se tabulka činností (spec B5).</summary>
    public bool MaCinnosti { get; init; }

    /// <summary>Kalkulace má licenci (cena_l &gt; 0) — tiskne se tabulka licencí (spec B5).</summary>
    public bool MaLicenci { get; init; }

    /// <summary>Řádky tabulky licencí z rozpad_licence; bez licence prázdné.</summary>
    public IReadOnlyList<VyzvaExportLicenceRadekViewModel> LicenceRadky { get; init; }
        = Array.Empty<VyzvaExportLicenceRadekViewModel>();
```

  a komentář `CenaLicence` doplň: `/// <summary>Součet řádků licence bez DPH; null bez licence.</summary>`;
- na konec souboru:

```csharp
/// <summary>Řádek tabulky licencí. POL a PPOL model nenese — tisknou se vždy prázdné (spec B5).</summary>
public sealed class VyzvaExportLicenceRadekViewModel
{
    public int Kod { get; init; }                  // 1, 2, …
    public required string Nazev { get; init; }
    public decimal CenaBezDph { get; init; }
    public decimal CenaDph { get; init; }
    public decimal CenaSDph { get; init; }
}
```

- [ ] **Krok 6: Builder — celý soubor**

`PmTracker.Web/Services/Vyzvy/VyzvaExportBuilder.cs` nahraď tímto obsahem. Oproti dnešku:
římské pořadí, prefix „RU", příznaky a řádky licence, volitelný logger; zmizela vazba na PMP
(`PmpKod`, `LoadPmpVazbyAsync`). `ZaznamInfo`, `SkupinoveRazeni` a `LoadZaznamyAsync` jsou beze změny.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Vyzvy;

/// <summary>
/// Skládá výzvu pro tisk. Jediná projekce pro Word i PDF (spec 2026-09-07 §9.2).
/// </summary>
public interface IVyzvaExportBuilder
{
    /// <summary>Null, když výzva neexistuje. Prázdná výzva vrací model bez požadavků (§9.7).</summary>
    Task<VyzvaExportViewModel?> BuildAsync(int vyzvaId, CancellationToken ct);
}

/// <summary>
/// Projekce výzvy podle finálního vzoru (spec 2026-09-10 část B): římské pořadí, číslo úkolu
/// s prefixem „RU", tabulky podle toho, co kalkulace obsahuje, licence z rozpad_licence.
/// Vazba na PMP se do výzvy nedává (rozhodnutí uživatele 2026-09-10).
/// </summary>
public sealed class VyzvaExportBuilder : IVyzvaExportBuilder
{
    private const string PrefixUkolu = "RU";

    /// <summary>
    /// Řádky kalkulační tabulky. Pořadí, kódy i názvy jsou dané formulářem a odpovídají
    /// sloupcům HOT_KALKULACE (pracnost_a/p/t/i) — nejsou to data, je to struktura.
    /// </summary>
    private static readonly (string Kod, string Nazev)[] KalkulaceRadky =
    {
        ("A", "Analýza"),
        ("B", "Programové úpravy"),
        ("C", "Testování"),
        ("D", "Implementace"),
    };

    private static readonly (int Hodnota, string Znak)[] RimskeCislice =
    {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    };

    private readonly PmTrackerDbContext _db;
    private readonly ITicketingQueryService _ticketing;
    private readonly IRichTextContentService _richText;
    private readonly ILogger<VyzvaExportBuilder> _logger;

    public VyzvaExportBuilder(
        PmTrackerDbContext db,
        ITicketingQueryService ticketing,
        IRichTextContentService richText,
        ILogger<VyzvaExportBuilder>? logger = null)
    {
        _db = db;
        _ticketing = ticketing;
        _richText = richText;
        _logger = logger ?? NullLogger<VyzvaExportBuilder>.Instance;
    }

    public async Task<VyzvaExportViewModel?> BuildAsync(int vyzvaId, CancellationToken ct)
    {
        var vyzva = await _db.Vyzvy.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vyzvaId, ct);
        if (vyzva == null) return null;

        var polozky = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(ev => ev.VyzvaId == vyzvaId)
            .Select(ev => new { ev.Id, ev.ZaznamId, ev.Cislo, ev.Pozadavek })
            .ToListAsync(ct);

        var zaznamIds = polozky.Select(x => x.ZaznamId).Distinct().ToArray();
        var zaznamy = await LoadZaznamyAsync(zaznamIds, ct);

        // HOT popisy nesou i PID, přes který teprve jdou dotáhnout kalkulace (§9.5).
        var hot = await _ticketing.GetZaznamyAsync(
            polozky.Select(x => x.Cislo).Distinct().ToArray(), ct);
        var pidy = hot.Values
            .Select(h => h.Pid)
            .Where(pid => !string.IsNullOrWhiteSpace(pid))
            .Select(pid => pid!)
            .Distinct()
            .ToArray();
        var kalkulace = await _ticketing.GetAkceptovaneKalkulaceAsync(pidy, ct);

        var poradi = 0;
        var pozadavky = polozky
            .OrderBy(x => SkupinoveRazeni(zaznamy.GetValueOrDefault(x.ZaznamId)))
            .ThenBy(x => x.Id)
            .Select(x =>
            {
                var z = zaznamy.GetValueOrDefault(x.ZaznamId);
                var h = hot.GetValueOrDefault(x.Cislo);
                var k = h?.Pid is string pid ? kalkulace.GetValueOrDefault(pid) : null;

                return new VyzvaExportPozadavekViewModel
                {
                    PoradoveOznaceni = PoradoveOznaceni(poradi++),
                    ZaznamId = x.ZaznamId,
                    CisloUkoluVp = CisloUkoluVp(z?.CisloViditelne),
                    Nazev = z?.Nazev,
                    CisloHtl = x.Cislo,
                    // Sanitizace i tady, ne jen při uložení: dokument je výstup ven
                    // a nesmí záviset na tom, že do DB nikdy nic nepřišlo jinudy.
                    PozadavekHtml = _richText.HasVisibleText(x.Pozadavek)
                        ? _richText.ToSafeHtml(x.Pozadavek)
                        : null,
                    Kalkulace = Kalkulace(k, x.Cislo),
                };
            })
            .ToArray();

        var celkemBez = pozadavky.Sum(p => p.Kalkulace.CelkemBezDph);
        var licenceBez = pozadavky.Sum(p => p.Kalkulace.CenaLicence ?? 0m);

        return new VyzvaExportViewModel
        {
            VyzvaId = vyzva.Id,
            ProjektId = vyzva.ProjektId,
            KodVyzvy = vyzva.Kod,
            PoradoveVRoce = vyzva.PoradoveVRoce,
            Rok = vyzva.Rok,
            CisloRamcoveSmlouvy = vyzva.CisloRamcoveSmlouvySnapshot,
            MistoPlneni = vyzva.MistoPlneniSnapshot,
            InformacniSystem = InformacniSystem(vyzva.MistoPlneniSnapshot),
            Pozadavky = pozadavky,
            CelkemBezDph = celkemBez,
            CelkemDph = Dph(celkemBez),
            CelkemSDph = celkemBez + Dph(celkemBez),
            LicenceBezDph = licenceBez,
            LicenceDph = Dph(licenceBez),
            LicenceSDph = licenceBez + Dph(licenceBez),
        };
    }

    private static decimal Dph(decimal bezDph)
        => decimal.Round(bezDph * VyzvaExportViewModel.DphSazba, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Zkratka IS z místa plnění: „FIS (EIS): VZ 8201" dá „FIS". Formulář ji používá
    /// v nadpisu výzvy.
    /// </summary>
    private static string InformacniSystem(string mistoPlneni)
    {
        var token = mistoPlneni.Split(new[] { ' ', '(', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(token) ? string.Empty : token;
    }

    /// <summary>Poř. č. římsky s tečkou: „I.", „II.", … (komentář autora vzoru 2026-09-10).</summary>
    internal static string PoradoveOznaceni(int index)
    {
        var cislo = index + 1;
        var vysledek = new System.Text.StringBuilder();
        foreach (var (hodnota, znak) in RimskeCislice)
        {
            while (cislo >= hodnota)
            {
                vysledek.Append(znak);
                cislo -= hodnota;
            }
        }

        return vysledek.Append('.').ToString();
    }

    /// <summary>
    /// Č. úkolu VP s prefixem „RU" (komentář autora vzoru). CisloViditelne ho v produkci
    /// nenese — skládá se jako {jednání}-{pořadí} nebo {číslo}. Pojistka proti zdvojení kryje
    /// importovaná data i testový harness, který „RU" seeduje.
    /// </summary>
    internal static string? CisloUkoluVp(string? cisloViditelne)
    {
        if (string.IsNullOrWhiteSpace(cisloViditelne)) return null;

        var cislo = cisloViditelne.Trim();
        return cislo.StartsWith(PrefixUkolu, StringComparison.OrdinalIgnoreCase)
            ? cislo
            : PrefixUkolu + cislo;
    }

    private VyzvaExportKalkulaceViewModel Kalkulace(HotKalkulaceDto? k, string cisloPnf)
    {
        var hodnoty = new (decimal? Rozsah, decimal? Sazba, decimal? Cena)[]
        {
            (k?.PracnostAnalyza, k?.SazbaAnalyza, k?.CenaAnalyza),
            (k?.PracnostProgramovani, k?.SazbaProgramovani, k?.CenaProgramovani),
            (k?.PracnostTestovani, k?.SazbaTestovani, k?.CenaTestovani),
            (k?.PracnostImplementace, k?.SazbaImplementace, k?.CenaImplementace),
        };

        var radky = KalkulaceRadky.Select((r, i) =>
        {
            var (rozsah, sazba, cena) = hodnoty[i];
            var dph = cena.HasValue ? Dph(cena.Value) : (decimal?)null;
            return new VyzvaExportKalkulaceRadekViewModel
            {
                Kod = r.Kod,
                Nazev = r.Nazev,
                Rozsah = rozsah,
                Sazba = sazba,
                CenaBezDph = cena,
                CenaDph = dph,
                CenaSDph = cena.HasValue ? cena.Value + dph!.Value : null,
            };
        }).ToArray();

        var celkemBez = radky.Sum(r => r.CenaBezDph ?? 0m);
        var licence = LicenceRadky(k, cisloPnf);

        return new VyzvaExportKalkulaceViewModel
        {
            Radky = radky,
            CelkemBezDph = celkemBez,
            CelkemDph = Dph(celkemBez),
            CelkemSDph = celkemBez + Dph(celkemBez),
            MaCinnosti = celkemBez > 0m,
            MaLicenci = licence.Count > 0,
            LicenceRadky = licence,
            PocetLicenci = k?.PocetLicenci,
            SazbaLicence = k?.SazbaLicence,
            CenaLicence = licence.Count > 0 ? licence.Sum(r => r.CenaBezDph) : null,
        };
    }

    /// <summary>
    /// Řádky tabulky licencí (spec B5). Zdroj je rozpad_licence, cena_l je kontrolní součet —
    /// když se rozejdou, tiskne se rozpad a rozdíl jde do logu. Bez čitelného rozpadu jeden
    /// řádek „Licenční rozšíření" s cena_l. Bez licence prázdný seznam.
    /// </summary>
    private IReadOnlyList<VyzvaExportLicenceRadekViewModel> LicenceRadky(HotKalkulaceDto? k, string cisloPnf)
    {
        if (k?.CenaLicence is not decimal cenaL || cenaL <= 0m)
        {
            return Array.Empty<VyzvaExportLicenceRadekViewModel>();
        }

        IReadOnlyList<RozpadLicenceParser.Polozka> polozky = RozpadLicenceParser.Parse(k.RozpadLicence);
        if (polozky.Count == 0)
        {
            polozky = new[] { new RozpadLicenceParser.Polozka("Licenční rozšíření", cenaL) };
        }
        else if (polozky.Sum(p => p.Cena) != cenaL)
        {
            _logger.LogWarning(
                "Výzva: rozpad licence PNF {Cislo} dává {Soucet}, ale cena_l je {CenaL}. Tiskne se rozpad.",
                cisloPnf, polozky.Sum(p => p.Cena), cenaL);
        }

        return polozky.Select((p, i) => new VyzvaExportLicenceRadekViewModel
        {
            Kod = i + 1,
            Nazev = p.Nazev,
            CenaBezDph = p.Cena,
            CenaDph = Dph(p.Cena),
            CenaSDph = p.Cena + Dph(p.Cena),
        }).ToArray();
    }

    private sealed record ZaznamInfo(
        string? CisloViditelne, string Nazev, string? KategorieNazev,
        int CisloViditelneA, byte CisloViditelneTyp, int CisloViditelneB, int CisloZaznamu);

    /// <summary>
    /// Řazení požadavků drží sdílené RecordDisplayOrdering — stejné pořadí jako záložka
    /// Záznamy a panel Výzev, aby uživatel nepotkal třetí pořadí (spec §9.4).
    /// </summary>
    private static (int, int, int, int) SkupinoveRazeni(ZaznamInfo? i)
        => (RecordDisplayOrdering.CategoryOrder(i?.KategorieNazev),
            RecordDisplayOrdering.VisibleNumberPartA(i?.CisloViditelneA ?? 0, i?.CisloZaznamu ?? 0),
            RecordDisplayOrdering.VisibleNumberPartB(i?.CisloViditelneTyp ?? 0, i?.CisloViditelneB ?? 0),
            i?.CisloZaznamu ?? 0);

    private async Task<IReadOnlyDictionary<int, ZaznamInfo>> LoadZaznamyAsync(
        int[] zaznamIds, CancellationToken ct)
    {
        if (zaznamIds.Length == 0) return new Dictionary<int, ZaznamInfo>();

        return await (from z in _db.ProjektoveZaznamy.AsNoTracking()
                      join kat in _db.CiselnikKategoriiZaznamu.AsNoTracking() on z.KategorieId equals kat.Id into kj
                      from kat in kj.DefaultIfEmpty()
                      where zaznamIds.Contains(z.Id)
                      select new
                      {
                          z.Id, z.CisloViditelne, z.Nazev,
                          KategorieNazev = kat != null ? kat.Nazev : null,
                          z.CisloViditelneA, z.CisloViditelneTyp, z.CisloViditelneB, z.CisloZaznamu,
                      })
            .ToDictionaryAsync(
                x => x.Id,
                x => new ZaznamInfo(x.CisloViditelne, x.Nazev, x.KategorieNazev,
                                    x.CisloViditelneA, x.CisloViditelneTyp, x.CisloViditelneB, x.CisloZaznamu),
                ct);
    }
}
```

Build teď selže v `OpenXmlVyzvaExportService` a `VyzvaTemplate.cshtml` na smazaném `VazbaPmp`.
Aby šel blok ověřit samostatně, v obou místech zatím smaž jen řádek s `VazbaPmp` — celé přestavby
přijdou v blocích 8 a 9.

- [ ] **Krok 7: Spustit a ověřit průchod**

Příkaz z kroku 3 — všechny zelené.

- [ ] **Krok 8: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTiskTests"
```

Očekávej: build 0/0, Unit zelená, `VyzvyTiskTests` zelené (obsah dokumentu se ještě nemění).

---
## Blok 8: Word výzvy podle vzoru

Spec B1–B9. Přestavba `OpenXmlVyzvaExportService`: stránka, písmo 12/10, záhlaví a zápatí,
část `numbering`, nové pořadí sekcí, tabulky podle kalkulace, opravené chyby vzoru.

**Soubory:**
- Upravit (celý soubor): `PmTracker.Web/Services/Export/OpenXmlVyzvaExportService.cs`
- Test: `PmTracker.Tests.Unit/Export/OpenXmlVyzvaExportStrukturaTests.cs` (nový)
- Test: `PmTracker.Tests.Unit/Export/OpenXmlVyzvaExportPozadavekTests.cs` (musí zůstat zelený beze změny)

- [ ] **Krok 1: Napsat padající testy struktury**

`PmTracker.Tests.Unit/Export/OpenXmlVyzvaExportStrukturaTests.cs`:

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels.Vyzvy;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Word výzvy podle finálního vzoru (spec 2026-09-10 část B). Kotví se na strukturu dokumentu —
/// pořadí, tabulky, buňky, číslování — ne na vzhled.
/// Čtyři požadavky pokrývají všechny tvary kalkulace: I. jen činnosti, II. jen licence,
/// III. obojí, IV. bez akceptované kalkulace.
/// </summary>
public sealed class OpenXmlVyzvaExportStrukturaTests
{
    private static VyzvaExportKalkulaceRadekViewModel Radek(string kod, string nazev, decimal? cena) => new()
    {
        Kod = kod, Nazev = nazev,
        Rozsah = cena.HasValue ? 1m : null, Sazba = cena,
        CenaBezDph = cena, CenaDph = cena * 0.21m, CenaSDph = cena * 1.21m,
    };

    private static VyzvaExportKalkulaceRadekViewModel[] Radky(decimal? cena) => new[]
    {
        Radek("A", "Analýza", cena), Radek("B", "Programové úpravy", cena),
        Radek("C", "Testování", cena), Radek("D", "Implementace", cena),
    };

    private static VyzvaExportLicenceRadekViewModel[] Licence() => new[]
    {
        new VyzvaExportLicenceRadekViewModel
        {
            Kod = 1, Nazev = "XRG – RSS Rozhraní", CenaBezDph = 94470m, CenaDph = 19838.70m, CenaSDph = 114308.70m,
        },
    };

    private static VyzvaExportPozadavekViewModel Pozadavek(
        string poradi, string htl, bool cinnosti, bool licence, string? html = null) => new()
    {
        PoradoveOznaceni = poradi,
        ZaznamId = 1,
        CisloUkoluVp = "RU867-5",
        Nazev = $"Pozadavek {htl}",
        CisloHtl = htl,
        PozadavekHtml = html,
        Kalkulace = new VyzvaExportKalkulaceViewModel
        {
            Radky = Radky(cinnosti ? 100m : null),
            CelkemBezDph = cinnosti ? 400m : 0m,
            CelkemDph = cinnosti ? 84m : 0m,
            CelkemSDph = cinnosti ? 484m : 0m,
            MaCinnosti = cinnosti,
            MaLicenci = licence,
            LicenceRadky = licence ? Licence() : Array.Empty<VyzvaExportLicenceRadekViewModel>(),
            CenaLicence = licence ? 94470m : null,
        },
    };

    private static VyzvaExportViewModel Model(params VyzvaExportPozadavekViewModel[] pozadavky) => new()
    {
        VyzvaId = 10,
        ProjektId = 1,
        KodVyzvy = "8/2026",
        PoradoveVRoce = 8,
        Rok = 2026,
        CisloRamcoveSmlouvy = "23106000271",
        MistoPlneni = "FIS (EIS): VZ 8201, Tychonova 1, 160 01 Praha 6",
        InformacniSystem = "FIS",
        Pozadavky = pozadavky,
        CelkemBezDph = pozadavky.Sum(p => p.Kalkulace.CelkemBezDph),
        LicenceBezDph = pozadavky.Sum(p => p.Kalkulace.CenaLicence ?? 0m),
    };

    private static VyzvaExportViewModel VsechnyTvary(string? html = null) => Model(
        Pozadavek("I.", "358333", cinnosti: true, licence: false, html),
        Pozadavek("II.", "358310", cinnosti: false, licence: true),
        Pozadavek("III.", "361652", cinnosti: true, licence: true),
        Pozadavek("IV.", "364451", cinnosti: false, licence: false));

    private static WordprocessingDocument Otevrit(VyzvaExportViewModel model)
        => WordprocessingDocument.Open(
            new MemoryStream(new OpenXmlVyzvaExportService().BuildDocument(model), writable: false), false);

    private static string Druh(Table t)
    {
        var hlavicka = t.Elements<TableRow>().First().InnerText;
        if (hlavicka.Contains("Rozsah [hod]")) return "cinnosti";
        if (hlavicka.Contains("PPOL")) return "licence";
        return "jina";
    }

    private static string[] Bunky(TableRow r) => r.Elements<TableCell>().Select(c => c.InnerText).ToArray();

    [Fact]
    public void Stranka_A4_ZahlaviSPrilohou_ZapatiSCislemStranky()
    {
        using var doc = Otevrit(VsechnyTvary());
        var main = doc.MainDocumentPart!;
        var body = main.Document.Body!;

        var sekce = body.Elements<SectionProperties>().Should().ContainSingle().Which;
        sekce.GetFirstChild<PageSize>()!.Width!.Value.Should().Be(11906U, "A4");
        sekce.GetFirstChild<PageMargin>()!.Left!.Value.Should().Be(1417U, "okraj 2,5 cm");

        main.HeaderParts.Should().ContainSingle().Which.Header.InnerText.Should().Contain("Příloha č.1 k Čj. MO");
        body.Elements<Paragraph>().First().InnerText.Should().NotContain("Příloha", "záhlaví patří do záhlaví stránky");
        main.FooterParts.Should().ContainSingle().Which.Footer.Descendants<SimpleField>()
            .Should().Contain(f => f.Instruction!.Value!.Contains("PAGE"), "zápatí nese číslo stránky");
    }

    [Fact]
    public void Pismo_Telo12Bodu_Tabulky10Bodu()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        body.Elements<Paragraph>().Single(p => p.InnerText.StartsWith("Podrobné návrhy požadavků"))
            .Descendants<Run>().Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "24", "tělo 12 b.");
        body.Elements<Table>().First().Descendants<Run>()
            .Should().OnlyContain(r => r.RunProperties!.FontSize!.Val == "20", "tabulky 10 b.");
    }

    [Fact]
    public void Uvod_OpravenyZakon_JmenoReditele_TucnyNazevZakazky()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        body.InnerText.Should().Contain("o zadávání veřejných zakázek,");
        body.InnerText.Should().NotContain("zakázkách", "chyba vzoru se opravuje (spec B9)");
        body.InnerText.Should().Contain("Ing. Petrem ZÁBORCEM");
        body.Descendants<Run>().Single(r => r.InnerText == "Technické zhodnocení APV a DZ")
            .RunProperties!.Bold.Should().NotBeNull();
        body.InnerText.Should().NotContain("XXXX", "prázdná pole zůstávají prázdná, ne zástupná");
    }

    [Fact]
    public void Sekce1_RimskaPoradi_PrefixRU_StrucnePopisyBezVazbyNaPmp()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        var prvni = Bunky(body.Elements<Table>().First().Elements<TableRow>().ElementAt(1));
        prvni[0].Should().Be("I.");
        prvni[1].Should().Be("RU867-5");

        var texty = body.Elements<Paragraph>().Select(p => p.InnerText).ToList();
        texty.IndexOf("Stručné popisy požadavků:").Should().BeLessThan(
            texty.IndexOf("Počet člověkohodin s rozčleněním dle sazeb a informačních systémů"),
            "stručné popisy patří do sekce 1");
        texty.Should().Contain("Číslo úkolu VP EIS: RU867-5.");
        texty.Should().Contain("Bližší podrobnosti jsou uvedeny v PNF 358333.");
        body.InnerText.Should().NotContain("Vazba na PMP", "rozhodnutí uživatele 2026-09-10");
    }

    [Fact]
    public void NadpisyPozadavku_JsouDveRimskeRadyWordu()
    {
        using var doc = Otevrit(VsechnyTvary());
        var main = doc.MainDocumentPart!;

        main.NumberingDefinitionsPart!.Numbering.Descendants<NumberingFormat>()
            .Should().Contain(f => f.Val! == NumberFormatValues.UpperRoman);

        var nadpisy = main.Document.Body!.Elements<Paragraph>()
            .Where(p => p.InnerText == "Pozadavek 358333").ToList();
        nadpisy.Should().HaveCount(2, "název požadavku je nadpisem v sekci 1 i v sekci 2");
        var cislovani = nadpisy.Select(p => p.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value).ToList();
        cislovani[0].Should().NotBe(cislovani[1], "každá sekce má vlastní řadu od I.");
    }

    [Fact]
    public void Sekce2_TabulkyPodleKalkulace_CinnostiPredLicenci()
    {
        using var doc = Otevrit(VsechnyTvary());
        var druhy = doc.MainDocumentPart!.Document.Body!.Elements<Table>().Select(Druh)
            .Where(d => d != "jina").ToList();

        druhy.Should().Equal("cinnosti", "licence", "cinnosti", "licence");
    }

    [Fact]
    public void TabulkaCinnosti_CelkemVeCtvrteBunce()
    {
        using var doc = Otevrit(VsechnyTvary());
        var tabulka = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First(t => Druh(t) == "cinnosti");

        var soucet = Bunky(tabulka.Elements<TableRow>().Last());
        soucet.Take(3).Should().OnlyContain(b => b.Length == 0);
        soucet[3].Should().Be("CELKEM");
    }

    [Fact]
    public void TabulkaLicenci_PolAPpolPrazdne_CelkemPresCtyriSloupce()
    {
        using var doc = Otevrit(VsechnyTvary());
        var tabulka = doc.MainDocumentPart!.Document.Body!.Elements<Table>().First(t => Druh(t) == "licence");

        var radek = Bunky(tabulka.Elements<TableRow>().ElementAt(1));
        radek[0].Should().Be("1");
        radek[1].Should().Be("XRG – RSS Rozhraní");
        radek[2].Should().BeEmpty("POL doplňuje uživatel ručně");
        radek[3].Should().BeEmpty("PPOL doplňuje uživatel ručně");

        var soucet = tabulka.Elements<TableRow>().Last().Elements<TableCell>().First();
        soucet.InnerText.Should().Be("CELKEM licenční rozšíření");
        soucet.TableCellProperties!.GridSpan!.Val!.Value.Should().Be(4);
    }

    [Fact]
    public void Sekce3_RekapitulaceJenSeSvymiPozadavky_PrazdnyRadekPredSoucty()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;
        var rekapitulace = body.Elements<Table>()
            .Where(t => Bunky(t.Elements<TableRow>().First()).SequenceEqual(new[] { "Poř. č.", "Název požadavku", "Cena [Kč]" }))
            .ToList();
        rekapitulace.Should().HaveCount(2, "individuální úpravy a licenční rozšíření");

        var cinnosti = rekapitulace[0].Elements<TableRow>().Select(Bunky).ToList();
        // Jen požadavky s činnostmi, pořadí převzaté ze sekce 1, prázdný řádek před součty.
        // (Equal nemá variantu s důvodem — řetězec by se bral jako další očekávaný prvek.)
        cinnosti.Select(r => r[0]).Should().Equal("Poř. č.", "I.", "III.", "", "Celkem", "", "");
        cinnosti[3].Should().OnlyContain(b => b.Length == 0);

        rekapitulace[1].Elements<TableRow>().Select(r => Bunky(r)[0])
            .Should().Equal("Poř. č.", "II.", "III.", "", "Celkem", "", "");

        body.Elements<Table>().Last(t => Druh(t) == "jina" && Bunky(t.Elements<TableRow>().First())[0] == "Název")
            .Elements<TableRow>().First().InnerText.Should().Contain("Položková cena DPH 21 % [Kč]");
    }

    [Fact]
    public void SeznamyVTextuPozadavku_JsouSeznamyWordu()
    {
        using var doc = Otevrit(VsechnyTvary("<ul><li>prvni</li></ul><ol><li>jedna</li></ol>"));
        var odstavce = doc.MainDocumentPart!.Document.Body!.Elements<Paragraph>().ToList();

        var odrazka = odstavce.Single(p => p.InnerText.Contains("prvni"));
        var cislo = odstavce.Single(p => p.InnerText.Contains("jedna"));

        odrazka.InnerText.Should().NotContain("•", "odrážku kreslí Word, ne znak v textu");
        cislo.InnerText.Should().NotStartWith("1.", "číslo kreslí Word");
        odrazka.ParagraphProperties!.NumberingProperties.Should().NotBeNull();
        cislo.ParagraphProperties!.NumberingProperties!.NumberingId!.Val!.Value
            .Should().NotBe(odrazka.ParagraphProperties.NumberingProperties!.NumberingId!.Val!.Value);
    }

    [Fact]
    public void Zaver_TerminPrazdny_PodpisySeJmeny_TriZalomeniStranky()
    {
        using var doc = Otevrit(VsechnyTvary());
        var body = doc.MainDocumentPart!.Document.Body!;

        body.Elements<Paragraph>().Single(p => p.InnerText.StartsWith("Termín pro splnění"))
            .InnerText.Trim().Should().Be("Termín pro splnění dílčí veřejné zakázky do:", "komentář autora: nechat volné");
        body.InnerText.Should().Contain("Ing. Petr ZÁBOREC").And.Contain("Ing. Břetislav MOC")
            .And.Contain("předseda správní rady");
        body.Descendants<Break>().Count(b => b.Type is not null && b.Type.Value == BreakValues.Page)
            .Should().Be(3, "zalomení před sekcemi 2, 3 a 4");
    }
}
```

- [ ] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlVyzvaExportStrukturaTests"`
Očekávej: všechny testy selžou proti dnešnímu exportu (záhlaví v těle, 10 b., „XXXX", žádné číslování…).

- [ ] **Krok 3: Přestavět export — celý soubor**

`PmTracker.Web/Services/Export/OpenXmlVyzvaExportService.cs` nahraď tímto obsahem.
Pozor na české uvozovky v textech: zavírací je `“` (U+201C), ne ASCII `"` — ta by ukončila řetězec.

```csharp
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PmTracker.Web.Models.ViewModels.Vyzvy;

namespace PmTracker.Web.Services.Export;

/// <summary>Word export výzvy k poskytnutí plnění (spec 2026-09-07 §9).</summary>
public interface IVyzvaWordExportService
{
    byte[] BuildDocument(VyzvaExportViewModel model);
}

/// <summary>
/// Staví .docx výzvy přesně podle finálního vzoru (spec 2026-09-10 část B): A4, Times New
/// Roman 12 v těle a 10 v tabulkách, záhlaví s přílohou, zápatí s číslem stránky, skutečné
/// seznamy Wordu. Čte tutéž projekci jako náhled <c>Views/Export/VyzvaTemplate.cshtml</c> —
/// pořadí a obsah částí musí zůstat shodné. Údaje, které aplikace nemá (č. j., datum, termín),
/// zůstávají prázdné k doplnění ve Wordu.
/// </summary>
public sealed class OpenXmlVyzvaExportService : IVyzvaWordExportService
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const int Telo = 24;            // 12 b. — styl Normal vzoru
    private const int Male = 20;            // 10 b. — tabulky a štítky v sekci 2
    private const int PrvniRadekUradu = 32; // 16 b.
    private const int Podpisy = 18;         // 9 b.
    private const int SirkaTextu = 9072;    // A4 11906 − 2 × okraj 1417 (twipy)

    private const int NumSekce = 1;
    private const int NumPozadavkySekce1 = 2;
    private const int NumPozadavkySekce2 = 3;

    private const string PodrobneNavrhy =
        "Podrobné návrhy požadavků jsou součástí příslušného protokolu (HotLine – uvedená v tabulce shora) "
        + "a specifikace. Byly analyzovány dodavatelem a jejich užitnost je posuzována zadavatelem, vedením "
        + "projektu EIS, Řídícím výborem FIS, případně dalšími odborníky. Požadavky jsou posuzovány jednotlivými "
        + "vedoucími subsystémů FIS/ISSP a příslušnými metodiky. Požadavky jsou schváleny vedením projektu "
        + "FIS/ISSP (VP EIS). Čísla úkolů jsou uvedena také v souhrnné tabulce shora. Dále je uvedena stručná "
        + "anotace požadavků.";

    private static readonly string[] HlavickaCinnosti =
    {
        "Kód činnosti", "Název činnosti", "Rozsah [hod]", "Jednotková sazba bez DPH [Kč]",
        "Položková cena bez DPH [Kč]", "Položková cena DPH [Kč]", "Položková cena s DPH [Kč]",
    };

    private static readonly string[] HlavickaLicenci =
    {
        "Kód činnosti", "Název činnosti", "POL", "PPOL", "Cena v Kč bez DPH", "DPH v Kč", "Cena v Kč s DPH",
    };

    public byte[] BuildDocument(VyzvaExportViewModel model)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body
                ?? throw new InvalidOperationException("Word body nebyl inicializován.");
            var cislovani = new Cislovani();

            AppendUvod(body, model);
            AppendPredmet(body, model, cislovani);
            ZalomitStranku(body);
            AppendClovekohodiny(body, model);
            ZalomitStranku(body);
            AppendCelkovaCena(body, model);
            ZalomitStranku(body);
            AppendZavery(body, model);

            cislovani.Zapsat(mainPart);
            // Vlastnosti oddílu musí být posledním potomkem těla.
            body.Append(Stranka(mainPart));
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string Castka(decimal? c) => (c ?? 0m).ToString("N2", Cs);

    private static string CastkaNeboPrazdno(decimal? c) => c.HasValue ? c.Value.ToString("N2", Cs) : string.Empty;

    private static string Hodiny(decimal? h) => h.HasValue ? h.Value.ToString("0.##", Cs) : string.Empty;

    private static string DphProcenta => (VyzvaExportViewModel.DphSazba * 100m).ToString("0.##", Cs);

    private static Run Beh(string text, bool tucne = false, int velikost = Telo)
        => new(
            OpenXmlWordElements.CreateRunProperties(velikost, tucne, italic: false, strike: false, underline: false),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });

    /// <summary>Vlastnosti odstavce v pořadí, které vyžaduje schéma (keepNext, numPr, spacing, jc).</summary>
    private static ParagraphProperties Vlastnosti(
        int po = 120, int pred = 0, JustificationValues? zarovnani = null, int? numId = null, bool drzetSDalsim = false)
    {
        var pPr = new ParagraphProperties();
        if (drzetSDalsim) pPr.Append(new KeepNext());
        if (numId.HasValue)
        {
            pPr.Append(new NumberingProperties(
                new NumberingLevelReference { Val = 0 },
                new NumberingId { Val = numId.Value }));
        }
        pPr.Append(new SpacingBetweenLines { Before = pred.ToString(Inv), After = po.ToString(Inv) });
        if (zarovnani.HasValue) pPr.Append(new Justification { Val = zarovnani.Value });
        return pPr;
    }

    private static Paragraph Odstavec(
        string text, bool tucne = false, int velikost = Telo, JustificationValues? zarovnani = null,
        int po = 120, int pred = 0, int? numId = null, bool drzetSDalsim = false)
        => new(Vlastnosti(po, pred, zarovnani, numId, drzetSDalsim), Beh(text, tucne, velikost));

    /// <summary>Nadpis sekce 1.–7. — číslovaný seznam Wordu arabsky.</summary>
    private static void Nadpis(Body body, string text)
        => body.Append(Odstavec(text, tucne: true, pred: 240, numId: NumSekce, drzetSDalsim: true));

    /// <summary>Nadpis požadavku — římská řada Wordu, v sekci 1 a 2 každá zvlášť od I.</summary>
    private static void NadpisPozadavku(Body body, string? nazev, int numId)
        => body.Append(Odstavec(nazev ?? string.Empty, tucne: true, pred: 180, numId: numId, drzetSDalsim: true));

    private static void ZalomitStranku(Body body)
        => body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

    private static string CisloUkolu(VyzvaExportPozadavekViewModel p)
        => string.IsNullOrWhiteSpace(p.CisloUkoluVp)
            ? "Číslo úkolu VP EIS:"
            : $"Číslo úkolu VP EIS: {p.CisloUkoluVp}.";

    private static Table Tabulka(int sloupcu, bool ramecek = true)
    {
        // Pořadí dle schématu: tblW před tblBorders.
        var vlastnosti = new TableProperties(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct });
        if (ramecek)
        {
            vlastnosti.Append(new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));
        }

        // Mřížka musí existovat, jinak Word sloučené buňky (gridSpan) rozloží špatně.
        var sirka = (SirkaTextu / sloupcu).ToString(Inv);
        return new Table(
            vlastnosti,
            new TableGrid(Enumerable.Range(0, sloupcu).Select(_ => new GridColumn { Width = sirka })));
    }

    /// <summary>Řádek se nedělí přes stránku (cantSplit) — náhrada ručních zalomení vzoru.</summary>
    private static TableRow Radek(params TableCell[] bunky)
    {
        var radek = new TableRow(new TableRowProperties(new CantSplit()));
        radek.Append(bunky);
        return radek;
    }

    private static TableCell Bunka(string text, bool tucne = false, string? podklad = null, int sloucit = 1, int velikost = Male)
    {
        var tcPr = new TableCellProperties();
        if (sloucit > 1) tcPr.Append(new GridSpan { Val = sloucit });
        if (podklad is not null) tcPr.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = podklad, Color = "auto" });
        return new TableCell(tcPr, Odstavec(text, tucne, velikost, po: 30, pred: 30));
    }

    private static TableRow HlavickaTabulky(params string[] texty)
        => Radek(texty.Select(t => Bunka(t, tucne: true, podklad: "EFEFEF")).ToArray());

    private static TableRow Data(bool tucne, params string[] texty)
        => Radek(texty.Select(t => Bunka(t, tucne)).ToArray());

    private static void AppendUvod(Body body, VyzvaExportViewModel model)
    {
        body.Append(Odstavec("Sekce vyzbrojování a akvizic Ministerstva obrany", tucne: true, velikost: PrvniRadekUradu, po: 0));
        body.Append(Odstavec("odbor komunikačních a informačních systémů", tucne: true, po: 0));
        body.Append(Odstavec("náměstí Svobody 471/4, Praha 6, PSČ 160 01, datová schránka hjyaavk", velikost: Male, po: 240));

        // Č. j. a datum doplní uživatel ve Wordu — vzor je má prázdné (spec B3).
        body.Append(new Paragraph(
            Vlastnosti(po: 240),
            new Run(
                OpenXmlWordElements.CreateRunProperties(Telo, bold: false, italic: false, strike: false, underline: false),
                new Text("Čj."), new TabChar(), new TabChar(), new TabChar(), new Text("V Praze dne"))));

        body.Append(Odstavec(
            $"Výzva k poskytnutí plnění č. {model.KodVyzvy} pro {model.InformacniSystem}",
            tucne: true, zarovnani: JustificationValues.Both, pred: 240, po: 240));

        // Název zákona opravený proti vzoru („zakázkách" → „zakázek", spec B9).
        body.Append(Odstavec(
            "Veřejný zadavatel Česká republika – Ministerstvo obrany, se sídlem Tychonova 1, Praha 6, "
            + "zastoupena ředitelem odboru vyzbrojování pozemních sil a KIS Sekce vyzbrojování a akvizic MO "
            + "Ing. Petrem ZÁBORCEM, se sídlem na adrese náměstí Svobody 471/4, 160 01 Praha 6 "
            + "(dále jen „nabyvatel“), Vás vyzývá podle ustanovení § 134 zákona č. 134/2016 Sb., "
            + "o zadávání veřejných zakázek, ve znění pozdějších předpisů, v souladu s čl. IV. rámcové dohody "
            + $"číslo {model.CisloRamcoveSmlouvy} (dále jen „rámcová dohoda“) a v souladu s podmínkami v ní uvedenými",
            zarovnani: JustificationValues.Both));

        body.Append(Odstavec("k poskytnutí plnění", tucne: true, zarovnani: JustificationValues.Center));

        body.Append(new Paragraph(
            Vlastnosti(zarovnani: JustificationValues.Both),
            Beh("veřejné zakázky „"),
            Beh("Technické zhodnocení APV a DZ", tucne: true),
            Beh("“ pořadové číslo "),
            Beh($"{model.PoradoveVRoce.ToString("00", Cs)}/{model.Rok}", tucne: true),
            Beh(" (dále jen „Výzva“) na zadání dílčí veřejné zakázky.")));
    }

    private static void AppendPredmet(Body body, VyzvaExportViewModel model, Cislovani cislovani)
    {
        Nadpis(body, "Popis předmětu dílčí VZ na základě rámcové dohody, příp. počet dodávaných "
                     + "souvisejících rozšíření licence APV a DZ");

        var table = Tabulka(4);
        table.Append(HlavickaTabulky("Poř. č.", "Č. úkolu VP", "Název požadavku", "Č. HTL"));
        foreach (var p in model.Pozadavky)
        {
            table.Append(Data(false, p.PoradoveOznaceni, p.CisloUkoluVp ?? string.Empty, p.Nazev ?? string.Empty, p.CisloHtl));
        }
        body.Append(table);

        body.Append(Odstavec(PodrobneNavrhy, zarovnani: JustificationValues.Both, pred: 120));
        body.Append(Odstavec("Stručné popisy požadavků:", tucne: true, drzetSDalsim: true));

        foreach (var p in model.Pozadavky)
        {
            NadpisPozadavku(body, p.Nazev, NumPozadavkySekce1);
            body.Append(Odstavec(CisloUkolu(p)));
            if (!string.IsNullOrWhiteSpace(p.PozadavekHtml))
            {
                PozadavekOdstavce(body, p.PozadavekHtml!, cislovani);
            }
            // Vždy „Bližší podrobnosti…", nikdy „Vazba na PMP" (rozhodnutí uživatele 2026-09-10).
            body.Append(Odstavec($"Bližší podrobnosti jsou uvedeny v PNF {p.CisloHtl}."));
        }
    }

    private static void AppendClovekohodiny(Body body, VyzvaExportViewModel model)
    {
        Nadpis(body, "Počet člověkohodin s rozčleněním dle sazeb a informačních systémů");

        foreach (var p in model.Pozadavky)
        {
            var k = p.Kalkulace;
            NadpisPozadavku(body, p.Nazev, NumPozadavkySekce2);
            body.Append(Odstavec(CisloUkolu(p), velikost: Male, drzetSDalsim: k.MaCinnosti || k.MaLicenci));

            // Nejdřív činnosti, pak licence; bez akceptované kalkulace žádná tabulka (spec B5).
            if (k.MaCinnosti)
            {
                body.Append(Odstavec("Individuální úpravy", velikost: Male, drzetSDalsim: true));
                var table = Tabulka(7);
                table.Append(HlavickaTabulky(HlavickaCinnosti));
                foreach (var r in k.Radky)
                {
                    table.Append(Data(false, r.Kod, r.Nazev, Hodiny(r.Rozsah), CastkaNeboPrazdno(r.Sazba),
                        CastkaNeboPrazdno(r.CenaBezDph), CastkaNeboPrazdno(r.CenaDph), CastkaNeboPrazdno(r.CenaSDph)));
                }
                // Vzor: tři prázdné buňky, „CELKEM" ve čtvrté, pak částky.
                table.Append(Data(true, string.Empty, string.Empty, string.Empty, "CELKEM",
                    Castka(k.CelkemBezDph), Castka(k.CelkemDph), Castka(k.CelkemSDph)));
                body.Append(table);
            }

            if (k.MaLicenci)
            {
                body.Append(Odstavec("Licenční rozšíření", velikost: Male, pred: k.MaCinnosti ? 120 : 0, drzetSDalsim: true));
                var table = Tabulka(7);
                table.Append(HlavickaTabulky(HlavickaLicenci));
                foreach (var r in k.LicenceRadky)
                {
                    // POL a PPOL vždy prázdné — v databázi pro ně sloupec není (spec B5).
                    table.Append(Data(false, r.Kod.ToString(Inv), r.Nazev, string.Empty, string.Empty,
                        Castka(r.CenaBezDph), Castka(r.CenaDph), Castka(r.CenaSDph)));
                }
                table.Append(Radek(
                    Bunka("CELKEM licenční rozšíření", tucne: true, sloucit: 4),
                    Bunka(Castka(k.CenaLicence), tucne: true),
                    Bunka(Castka(k.LicenceRadky.Sum(r => r.CenaDph)), tucne: true),
                    Bunka(Castka(k.LicenceRadky.Sum(r => r.CenaSDph)), tucne: true)));
                body.Append(table);
            }
        }
    }

    private static void AppendCelkovaCena(Body body, VyzvaExportViewModel model)
    {
        Nadpis(body, "Celková cena za člověkohodiny, příp. související rozšíření licence APV a DZ "
                     + "IS GINIS® DEFENCE");

        body.Append(Odstavec("Individuální úpravy:", drzetSDalsim: true));
        body.Append(Rekapitulace(
            model.Pozadavky.Where(p => p.Kalkulace.MaCinnosti)
                .Select(p => (p.PoradoveOznaceni, p.Nazev, p.Kalkulace.CelkemBezDph)),
            model.CelkemBezDph, model.CelkemDph, model.CelkemSDph));

        body.Append(Odstavec("Licenční rozšíření:", pred: 120, drzetSDalsim: true));
        body.Append(Rekapitulace(
            model.Pozadavky.Where(p => p.Kalkulace.MaLicenci)
                .Select(p => (p.PoradoveOznaceni, p.Nazev, p.Kalkulace.CenaLicence ?? 0m)),
            model.LicenceBezDph, model.LicenceDph, model.LicenceSDph));

        // Prázdný odstavec mezi tabulkami — sousední tabulky by Word slil do jedné.
        body.Append(new Paragraph(Vlastnosti()));

        var souhrn = Tabulka(4);
        souhrn.Append(HlavickaTabulky("Název", "Položková cena bez DPH [Kč]",
            $"Položková cena DPH {DphProcenta} % [Kč]", "Položková cena s DPH [Kč]"));
        souhrn.Append(Data(false, "Úprava APV a DZ celkem",
            Castka(model.CelkemBezDph), Castka(model.CelkemDph), Castka(model.CelkemSDph)));
        souhrn.Append(Data(false, "Licenční rozšíření APV a DZ celkem",
            Castka(model.LicenceBezDph), Castka(model.LicenceDph), Castka(model.LicenceSDph)));
        souhrn.Append(Data(true, "CELKEM",
            Castka(model.CelkemBezDph + model.LicenceBezDph),
            Castka(model.CelkemDph + model.LicenceDph),
            Castka(model.CelkemSDph + model.LicenceSDph)));
        body.Append(souhrn);
    }

    /// <summary>
    /// Rekapitulace sekce 3: řádky požadavků s pořadím ze sekce 1 (nepřečíslovává se),
    /// prázdný řádek, součty (spec B6). „Poř. č." velkým písmenem — chyba vzoru opravená.
    /// </summary>
    private static Table Rekapitulace(
        IEnumerable<(string Poradi, string? Nazev, decimal Cena)> radky, decimal bez, decimal dph, decimal sDph)
    {
        var table = Tabulka(3);
        table.Append(HlavickaTabulky("Poř. č.", "Název požadavku", "Cena [Kč]"));
        foreach (var (poradi, nazev, cena) in radky)
        {
            table.Append(Data(false, poradi, nazev ?? string.Empty, Castka(cena)));
        }
        table.Append(Data(false, string.Empty, string.Empty, string.Empty));
        table.Append(Data(true, "Celkem", "bez DPH", Castka(bez)));
        table.Append(Data(false, string.Empty, $"DPH {DphProcenta} %", Castka(dph)));
        table.Append(Data(false, string.Empty, "s DPH", Castka(sDph)));
        return table;
    }

    private static void AppendZavery(Body body, VyzvaExportViewModel model)
    {
        Nadpis(body, "Identifikační údaje nabyvatele");
        foreach (var radek in new[]
                 {
                     "Česká republika – Ministerstvo obrany", "Tychonova 1", "160 00 Praha 6",
                     "IČO: 60162694, DIČ: CZ60162694", "v zastoupení", "Sekce vyzbrojování a akvizic MO",
                     "odbor vyzbrojování pozemních sil a komunikačních a informačních systémů",
                     "náměstí Svobody 471/4", "160 01 Praha 6",
                 })
        {
            body.Append(Odstavec(radek, po: 0));
        }

        Nadpis(body, "Termín a místo plnění");
        // Datum „Nechat volné" (komentář autora vzoru) — doplní uživatel ve Wordu.
        body.Append(Odstavec("Termín pro splnění dílčí veřejné zakázky do: "));
        body.Append(Odstavec("Místem plnění je:", po: 0));
        body.Append(Odstavec(model.MistoPlneni, tucne: true));

        Nadpis(body, "Lhůta pro písemné potvrzení Výzvy");
        body.Append(Odstavec("Dodavatel dle čl. IV odst. 1 rámcové smlouvy potvrdí tuto Výzvu do 5 dnů "
                             + "od jejího doručení.", tucne: true));

        Nadpis(body, "Datum a místo potvrzení výzvy dodavatelem");
        // Obě jména ponechal uživatel 2026-09-10 — jsou ve vzoru a s výzvou se nemění.
        var podpisy = Tabulka(2, ramecek: false);
        podpisy.Append(Radek(Bunka("Za nabyvatele:", tucne: true, velikost: Podpisy), Bunka("Za dodavatele:", tucne: true, velikost: Podpisy)));
        podpisy.Append(Radek(Bunka("………………………", velikost: Podpisy), Bunka("………………………", velikost: Podpisy)));
        podpisy.Append(Radek(Bunka("Ing. Petr ZÁBOREC", velikost: Podpisy), Bunka("Ing. Břetislav MOC", velikost: Podpisy)));
        podpisy.Append(Radek(Bunka("ředitel", velikost: Podpisy), Bunka("předseda správní rady", velikost: Podpisy)));
        body.Append(podpisy);
    }

    /// <summary>
    /// Text požadavku jako formátovaný rich text ve 12 bodech. Seznamy jsou skutečné seznamy
    /// Wordu: parser u položky seznamu nese druh a identitu seznamu a první token je značka,
    /// kterou tu přeskakujeme — Word ji kreslí sám (spec B8). Odkazy jako prostý text.
    /// </summary>
    private static void PozadavekOdstavce(Body body, string safeHtml, Cislovani cislovani)
    {
        var seznamy = new Dictionary<int, int>(); // ListId parseru → numId Wordu

        foreach (var odstavec in RichTextHtmlParser.Parse(safeHtml))
        {
            var pPr = new ParagraphProperties();
            IEnumerable<RichTextToken> tokeny = odstavec.Tokens;

            if (odstavec.ListKind != RichTextListKind.None)
            {
                if (!seznamy.TryGetValue(odstavec.ListId, out var numId))
                {
                    numId = cislovani.NovySeznam(odstavec.ListKind == RichTextListKind.Ordered);
                    seznamy[odstavec.ListId] = numId;
                }

                pPr.Append(new NumberingProperties(
                    new NumberingLevelReference { Val = Math.Min(odstavec.IndentLevel, 2) },
                    new NumberingId { Val = numId }));
                tokeny = odstavec.Tokens.Skip(1);
            }

            pPr.Append(new SpacingBetweenLines { After = "120" });
            if (odstavec.ListKind == RichTextListKind.None && odstavec.IndentLevel > 0)
            {
                pPr.Append(new Indentation { Left = (odstavec.IndentLevel * 360).ToString(Inv) });
            }

            var paragraph = new Paragraph(pPr);
            foreach (var token in tokeny)
            {
                if (token.IsLineBreak)
                {
                    paragraph.Append(new Run(new Break()));
                    continue;
                }

                if (string.IsNullOrEmpty(token.Text)) continue;

                paragraph.Append(new Run(
                    OpenXmlWordElements.CreateRunProperties(Telo, token.Bold, token.Italic, strike: false, token.Underline),
                    new Text(token.Text) { Space = SpaceProcessingModeValues.Preserve }));
            }

            body.Append(paragraph);
        }
    }

    /// <summary>A4, okraje 2,5 cm, záhlaví „Příloha" vpravo, zápatí s číslem stránky na střed (spec B1, B2).</summary>
    private static SectionProperties Stranka(MainDocumentPart mainPart)
    {
        var zahlavi = mainPart.AddNewPart<HeaderPart>();
        zahlavi.Header = new Header(Odstavec("Příloha č.1 k Čj. MO ", zarovnani: JustificationValues.Right, po: 0));
        zahlavi.Header.Save();

        var zapati = mainPart.AddNewPart<FooterPart>();
        zapati.Footer = new Footer(new Paragraph(
            new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            new SimpleField(new Run(
                OpenXmlWordElements.CreateRunProperties(Male, bold: false, italic: false, strike: false, underline: false),
                new Text("1"))) { Instruction = " PAGE " }));
        zapati.Footer.Save();

        return new SectionProperties(
            new HeaderReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(zahlavi) },
            new FooterReference { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(zapati) },
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin
            {
                Top = 1417, Right = 1417U, Bottom = 1417, Left = 1417U,
                Header = 708U, Footer = 708U, Gutter = 0U,
            });
    }

    /// <summary>
    /// Část numbering: nadpisy sekcí arabsky, dvě řady nadpisů požadavků římsky (sekce 1 a 2)
    /// a seznamy z textu požadavku — každý seznam vlastní instance, aby začínal od 1.
    /// </summary>
    private sealed class Cislovani
    {
        private const int AbsSekce = 1;
        private const int AbsRimsky = 2;
        private const int AbsOdrazky = 3;
        private const int AbsCislovany = 4;

        private readonly List<NumberingInstance> _seznamy = new();
        private int _dalsiId = 100;

        public int NovySeznam(bool cislovany)
        {
            var id = _dalsiId++;
            _seznamy.Add(Instance(id, cislovany ? AbsCislovany : AbsOdrazky));
            return id;
        }

        public void Zapsat(MainDocumentPart mainPart)
        {
            // Schéma: všechny abstractNum před všemi num.
            var numbering = new Numbering(
                Jednourovnovy(AbsSekce, NumberFormatValues.Decimal),
                Jednourovnovy(AbsRimsky, NumberFormatValues.UpperRoman),
                Vicerovnovy(AbsOdrazky, cislovany: false),
                Vicerovnovy(AbsCislovany, cislovany: true),
                Instance(NumSekce, AbsSekce),
                Instance(NumPozadavkySekce1, AbsRimsky),
                Instance(NumPozadavkySekce2, AbsRimsky));
            numbering.Append(_seznamy);

            var part = mainPart.AddNewPart<NumberingDefinitionsPart>();
            part.Numbering = numbering;
            part.Numbering.Save();
        }

        private static AbstractNum Jednourovnovy(int id, NumberFormatValues format)
            => new(
                new MultiLevelType { Val = MultiLevelValues.SingleLevel },
                new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = format },
                    new LevelText { Val = "%1." },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation { Left = "360", Hanging = "360" }))
                { LevelIndex = 0 })
            { AbstractNumberId = id };

        private static AbstractNum Vicerovnovy(int id, bool cislovany)
        {
            var abs = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel })
            {
                AbstractNumberId = id,
            };

            for (var uroven = 0; uroven < 3; uroven++)
            {
                abs.Append(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = cislovany ? NumberFormatValues.Decimal : NumberFormatValues.Bullet },
                    new LevelText { Val = cislovany ? $"%{uroven + 1}." : "•" },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation
                    {
                        Left = (720 + uroven * 360).ToString(Inv),
                        Hanging = "360",
                    }))
                { LevelIndex = uroven });
            }

            return abs;
        }

        /// <summary>
        /// StartOverride = 1: instance téže abstraktní definice by ve Wordu pokračovaly
        /// v číslování — sekce 2 by začala na XIX. a každý číslovaný seznam v textu požadavku
        /// by navazoval na předchozí.
        /// </summary>
        private static NumberingInstance Instance(int numId, int abstractId)
            => new(
                new AbstractNumId { Val = abstractId },
                new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 })
            { NumberID = numId };
    }
}
```

- [ ] **Krok 4: Spustit a ověřit průchod**

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlVyzvaExportStrukturaTests|FullyQualifiedName~OpenXmlVyzvaExportPozadavekTests"
```

Očekávej: všechny zelené, `OpenXmlVyzvaExportPozadavekTests` beze změny.

- [ ] **Krok 5: Validace schématu a oprava sdíleného pomocníka**

Unit testy ověří strukturu, ne to, že dokument odpovídá schématu. Word leccos snese, ale špatné
pořadí prvků je skrytá vada. Přidej do `OpenXmlVyzvaExportStrukturaTests`:

```csharp
    /// <summary>Dokument musí projít validací schématu Office — Word toleruje víc, než by měl.</summary>
    [Fact]
    public void Dokument_ProjdeValidaciSchematu()
    {
        using var doc = Otevrit(VsechnyTvary("<ul><li>a</li></ul><ol><li>b</li></ol>"));

        var chyby = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc)
            .Select(c => $"{c.Path?.XPath}: {c.Description}")
            .ToList();

        chyby.Should().BeEmpty();
    }
```

Spusť ho: **selže** na vlastnostech běhu. Příčina není ve výzvě, ale ve sdíleném
`OpenXmlWordElements.CreateRunProperties`, který skládá `rFonts, sz, b, i, strike, u, color`.
Schéma chce `rFonts, b, i, strike, color, sz, u`. Oprav to u zdroje — je to čisté přeřazení,
vzhled se nemění a export záznamu, který pomocníka sdílí, zůstane chováním neutrální.

Nejdřív padající test pořadí do nového souboru `PmTracker.Tests.Unit/Export/OpenXmlWordElementsTests.cs`:

```csharp
using DocumentFormat.OpenXml.Wordprocessing;
using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Vlastnosti běhu v pořadí podle schématu Office (CT_RPr): rFonts, b, i, strike, color, sz, u.
/// Word špatné pořadí toleruje, validátor ne — a sdílí ho export záznamu i výzvy.
/// </summary>
public sealed class OpenXmlWordElementsTests
{
    [Fact]
    public void CreateRunProperties_PoradiPrvkuPodleSchematu()
    {
        var rPr = OpenXmlWordElements.CreateRunProperties(
            24, bold: true, italic: true, strike: true, underline: true, colorHex: "FF0000");

        rPr.ChildElements.Select(e => e.GetType()).Should().Equal(
            typeof(RunFonts), typeof(Bold), typeof(Italic), typeof(Strike),
            typeof(Color), typeof(FontSize), typeof(Underline));
    }
}
```

Pak v `OpenXmlWordElements.CreateRunProperties` nahraď tělo:

```csharp
        // Pořadí podle schématu (CT_RPr): rFonts, b, i, strike, color, sz, u. Word špatné pořadí
        // toleruje, validátor ne (2026-09-10, validace výzvy).
        var runProperties = new RunProperties(new RunFonts
        {
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = "Times New Roman",
            ComplexScript = "Times New Roman"
        });

        if (bold) runProperties.Append(new Bold());
        if (italic) runProperties.Append(new Italic());
        if (strike) runProperties.Append(new Strike());
        if (!string.IsNullOrWhiteSpace(colorHex)) runProperties.Append(new Color { Val = colorHex });

        runProperties.Append(new FontSize { Val = sizeHalfPoints.ToString(CultureInfo.InvariantCulture) });

        if (underline) runProperties.Append(new Underline { Val = UnderlineValues.Single });

        return runProperties;
```

```bash
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlWordElementsTests|FullyQualifiedName~Dokument_ProjdeValidaciSchematu"
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj --filter "FullyQualifiedName~OpenXmlWordExportServiceTests|FullyQualifiedName~OpenXmlWordExportSplitTests"
```

Očekávej: oba nové testy zelené a exportní sady záznamu zelené beze změny. Kdyby validátor našel
ještě něco jiného, je to pořadí prvků ve vlastnostech odstavce, tabulky nebo buňky v
`OpenXmlVyzvaExportService` — oprav pořadí podle hlášky, ne test.

- [ ] **Krok 6: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTiskTests"
```

Očekávej: build 0/0, Unit zelená, `VyzvyTiskTests.VyzvaWord_VratiPlatnyDocx` zelený.

---
## Blok 9: Náhled výzvy podle vzoru

Spec B2–B8 pro náhled. Stejná struktura jako Word; číslování sekcí a požadavků je v HTML
doslovné (Word ho kreslí seznamem). „Příloha" zůstává prvním řádkem — renderer PDF má záhlaví
sdílené pro všechny exporty a číslo stránky dává jeho sdílené zápatí (spec B2).

**Soubory:**
- Upravit (celý soubor): `PmTracker.Web/Views/Export/VyzvaTemplate.cshtml`
- Upravit: `PmTracker.Web/wwwroot/css/pdf-export.css`
- Test: `PmTracker.Tests.Api/Controllers/VyzvyTiskTests.cs`

- [ ] **Krok 1: Napsat padající Api test**

Do `VyzvyTiskTests`:

```csharp
    /// <summary>
    /// Náhled má strukturu finálního vzoru, stejnou jako Word (spec 2026-09-10 část B).
    /// Kotví se na data-* atributy — text je v HTML zakódovaný. ServiceDesk je v testech
    /// vypnutý, takže PNF nemá akceptovanou kalkulaci a v sekci 2 nesmí být tabulka.
    /// </summary>
    [Fact]
    public async Task VyzvaTisk_NahledMaStrukturuVzoru()
    {
        var (projectId, vyzvaId) = await SeedAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Vyzva/{vyzvaId}/Tisk?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = RenderedPrintHtml();

        foreach (var sekce in Enumerable.Range(1, 7))
        {
            html.Should().Contain($"data-vyzva-sekce=\"{sekce}\"");
        }
        html.Should().Contain("data-vyzva-poradi=\"I.\"", "pořadí požadavků římsky");
        html.Should().Contain("data-vyzva-strucne-popisy", "stručné popisy patří do sekce 1");
        html.Should().Contain("data-vyzva-tabulka=\"rekapitulace-cinnosti\"");
        html.Should().Contain("data-vyzva-tabulka=\"rekapitulace-licence\"");
        html.Should().NotContain("data-vyzva-tabulka=\"cinnosti\"", "bez akceptované kalkulace žádná tabulka");
        html.Should().NotContain("XXXX", "prázdná pole zůstávají prázdná, ne zástupná");
        html.Should().NotContain("Vazba na PMP");
    }
```

- [ ] **Krok 2: Spustit a ověřit selhání**

Spusť: `dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvaTisk_NahledMaStrukturuVzoru"`
Očekávej: selhání — atributy v dnešní šabloně nejsou a obsahuje „XXXX".

- [ ] **Krok 3: Šablona — celý soubor**

`PmTracker.Web/Views/Export/VyzvaTemplate.cshtml`:

```cshtml
@using System.Globalization
@using PmTracker.Web.Models.ViewModels.Vyzvy
@model VyzvaExportViewModel
@{
    Layout = null;
    var cs = CultureInfo.GetCultureInfo("cs-CZ");
    string Castka(decimal? c) => (c ?? 0m).ToString("N2", cs);
    string CastkaNeboPrazdno(decimal? c) => c.HasValue ? c.Value.ToString("N2", cs) : "";
    string Hodiny(decimal? h) => h.HasValue ? h.Value.ToString("0.##", cs) : "";
    string CisloUkolu(VyzvaExportPozadavekViewModel p) => string.IsNullOrWhiteSpace(p.CisloUkoluVp)
        ? "Číslo úkolu VP EIS:"
        : $"Číslo úkolu VP EIS: {p.CisloUkoluVp}.";
    var dphProcenta = (VyzvaExportViewModel.DphSazba * 100m).ToString("0.##", cs);
    var sCinnostmi = Model.Pozadavky.Where(p => p.Kalkulace.MaCinnosti).ToList();
    var sLicenci = Model.Pozadavky.Where(p => p.Kalkulace.MaLicenci).ToList();
}
@*
    Náhled výzvy podle finálního vzoru (spec 2026-09-10 část B) — stejná struktura jako Word
    (OpenXmlVyzvaExportService). Údaje, které aplikace nemá (č. j., datum, termín), zůstávají
    prázdné; uživatel je doplní ve Wordu.
*@
<!DOCTYPE html>
<html lang="cs">
<head>
    <meta charset="utf-8" />
    <title>Výzva č. @Model.KodVyzvy</title>
    <link rel="stylesheet" href="~/css/pdf-export.css" />
</head>
<body>
<div class="print-sheet vyzva-sheet">
    <p class="vyzva-priloha">Příloha č.1 k Čj. MO</p>

    <header class="vyzva-hlavicka">
        <p class="vyzva-hlavicka-urad">Sekce vyzbrojování a akvizic Ministerstva obrany</p>
        <p class="vyzva-hlavicka-odbor">odbor komunikačních a informačních systémů</p>
        <p class="vyzva-male">náměstí Svobody 471/4, Praha 6, PSČ 160 01, datová schránka hjyaavk</p>
    </header>

    <p class="vyzva-cj">Čj.<span class="vyzva-cj-datum">V Praze dne</span></p>

    <h1 class="vyzva-nadpis">Výzva k poskytnutí plnění č. @Model.KodVyzvy pro @Model.InformacniSystem</h1>

    <p class="vyzva-blok">
        Veřejný zadavatel Česká republika – Ministerstvo obrany, se sídlem Tychonova 1, Praha 6,
        zastoupena ředitelem odboru vyzbrojování pozemních sil a KIS Sekce vyzbrojování a akvizic MO
        Ing. Petrem ZÁBORCEM, se sídlem na adrese náměstí Svobody 471/4, 160 01 Praha 6 (dále jen
        „nabyvatel“), Vás vyzývá podle ustanovení § 134 zákona č. 134/2016 Sb., o zadávání veřejných
        zakázek, ve znění pozdějších předpisů, v souladu s čl. IV. rámcové dohody číslo
        @Model.CisloRamcoveSmlouvy (dále jen „rámcová dohoda“) a v souladu s podmínkami v ní uvedenými
    </p>

    <p class="vyzva-stred">k poskytnutí plnění</p>

    <p class="vyzva-blok">
        veřejné zakázky „<strong>Technické zhodnocení APV a DZ</strong>“ pořadové číslo
        <strong>@Model.PoradoveVRoce.ToString("00", cs)/@Model.Rok</strong> (dále jen „Výzva“) na zadání
        dílčí veřejné zakázky.
    </p>

    <section data-vyzva-sekce="1">
        <h2>1. Popis předmětu dílčí VZ na základě rámcové dohody, příp. počet dodávaných souvisejících rozšíření licence APV a DZ</h2>

        <table class="vyzva-tabulka" data-vyzva-tabulka="predmet">
            <thead>
                <tr><th>Poř. č.</th><th>Č. úkolu VP</th><th>Název požadavku</th><th>Č. HTL</th></tr>
            </thead>
            <tbody>
            @foreach (var p in Model.Pozadavky)
            {
                <tr><td>@p.PoradoveOznaceni</td><td>@p.CisloUkoluVp</td><td>@p.Nazev</td><td>@p.CisloHtl</td></tr>
            }
            </tbody>
        </table>

        <p class="vyzva-blok">
            Podrobné návrhy požadavků jsou součástí příslušného protokolu (HotLine – uvedená v tabulce shora)
            a specifikace. Byly analyzovány dodavatelem a jejich užitnost je posuzována zadavatelem, vedením
            projektu EIS, Řídícím výborem FIS, případně dalšími odborníky. Požadavky jsou posuzovány
            jednotlivými vedoucími subsystémů FIS/ISSP a příslušnými metodiky. Požadavky jsou schváleny
            vedením projektu FIS/ISSP (VP EIS). Čísla úkolů jsou uvedena také v souhrnné tabulce shora.
            Dále je uvedena stručná anotace požadavků.
        </p>

        <p class="vyzva-tucne" data-vyzva-strucne-popisy>Stručné popisy požadavků:</p>

        @foreach (var p in Model.Pozadavky)
        {
            <h3 class="vyzva-pozadavek-nadpis" data-vyzva-poradi="@p.PoradoveOznaceni">@p.PoradoveOznaceni @p.Nazev</h3>
            <p>@CisloUkolu(p)</p>
            @if (!string.IsNullOrWhiteSpace(p.PozadavekHtml))
            {
                @* Html.Raw je tu bezpečné: hodnotu sanitizoval builder přes ToSafeHtml.
                   Kódování by naopak vypsalo syrové značky místo formátovaného textu. *@
                <div class="vyzva-pozadavek">@Html.Raw(p.PozadavekHtml)</div>
            }
            <p>Bližší podrobnosti jsou uvedeny v PNF @p.CisloHtl.</p>
        }
    </section>

    <section class="vyzva-zalomit" data-vyzva-sekce="2">
        <h2>2. Počet člověkohodin s rozčleněním dle sazeb a informačních systémů</h2>

        @foreach (var p in Model.Pozadavky)
        {
            var k = p.Kalkulace;
            <h3 class="vyzva-pozadavek-nadpis">@p.PoradoveOznaceni @p.Nazev</h3>
            <p class="vyzva-male">@CisloUkolu(p)</p>

            @* Nejdřív činnosti, pak licence; bez akceptované kalkulace žádná tabulka (spec B5). *@
            @if (k.MaCinnosti)
            {
                <p class="vyzva-male">Individuální úpravy</p>
                <table class="vyzva-tabulka vyzva-kalkulace" data-vyzva-tabulka="cinnosti">
                    <thead>
                        <tr>
                            <th>Kód činnosti</th><th>Název činnosti</th><th>Rozsah [hod]</th>
                            <th>Jednotková sazba bez DPH [Kč]</th><th>Položková cena bez DPH [Kč]</th>
                            <th>Položková cena DPH [Kč]</th><th>Položková cena s DPH [Kč]</th>
                        </tr>
                    </thead>
                    <tbody>
                    @foreach (var r in k.Radky)
                    {
                        <tr>
                            <td>@r.Kod</td><td>@r.Nazev</td><td>@Hodiny(r.Rozsah)</td>
                            <td>@CastkaNeboPrazdno(r.Sazba)</td><td>@CastkaNeboPrazdno(r.CenaBezDph)</td>
                            <td>@CastkaNeboPrazdno(r.CenaDph)</td><td>@CastkaNeboPrazdno(r.CenaSDph)</td>
                        </tr>
                    }
                        <tr class="vyzva-soucet">
                            <td></td><td></td><td></td><td>CELKEM</td>
                            <td>@Castka(k.CelkemBezDph)</td><td>@Castka(k.CelkemDph)</td><td>@Castka(k.CelkemSDph)</td>
                        </tr>
                    </tbody>
                </table>
            }

            @if (k.MaLicenci)
            {
                <p class="vyzva-male">Licenční rozšíření</p>
                <table class="vyzva-tabulka vyzva-kalkulace" data-vyzva-tabulka="licence">
                    <thead>
                        <tr>
                            <th>Kód činnosti</th><th>Název činnosti</th><th>POL</th><th>PPOL</th>
                            <th>Cena v Kč bez DPH</th><th>DPH v Kč</th><th>Cena v Kč s DPH</th>
                        </tr>
                    </thead>
                    <tbody>
                    @foreach (var r in k.LicenceRadky)
                    {
                        @* POL a PPOL vždy prázdné — v databázi pro ně sloupec není (spec B5). *@
                        <tr>
                            <td>@r.Kod</td><td>@r.Nazev</td><td></td><td></td>
                            <td>@Castka(r.CenaBezDph)</td><td>@Castka(r.CenaDph)</td><td>@Castka(r.CenaSDph)</td>
                        </tr>
                    }
                        <tr class="vyzva-soucet">
                            <td colspan="4">CELKEM licenční rozšíření</td>
                            <td>@Castka(k.CenaLicence)</td>
                            <td>@Castka(k.LicenceRadky.Sum(r => r.CenaDph))</td>
                            <td>@Castka(k.LicenceRadky.Sum(r => r.CenaSDph))</td>
                        </tr>
                    </tbody>
                </table>
            }
        }
    </section>

    <section class="vyzva-zalomit" data-vyzva-sekce="3">
        <h2>3. Celková cena za člověkohodiny, příp. související rozšíření licence APV a DZ IS GINIS® DEFENCE</h2>

        <p>Individuální úpravy:</p>
        <table class="vyzva-tabulka" data-vyzva-tabulka="rekapitulace-cinnosti">
            <thead><tr><th>Poř. č.</th><th>Název požadavku</th><th>Cena [Kč]</th></tr></thead>
            <tbody>
            @foreach (var p in sCinnostmi)
            {
                <tr><td>@p.PoradoveOznaceni</td><td>@p.Nazev</td><td>@Castka(p.Kalkulace.CelkemBezDph)</td></tr>
            }
                <tr><td></td><td></td><td></td></tr>
                <tr class="vyzva-soucet"><td>Celkem</td><td>bez DPH</td><td>@Castka(Model.CelkemBezDph)</td></tr>
                <tr><td></td><td>DPH @dphProcenta %</td><td>@Castka(Model.CelkemDph)</td></tr>
                <tr><td></td><td>s DPH</td><td>@Castka(Model.CelkemSDph)</td></tr>
            </tbody>
        </table>

        <p>Licenční rozšíření:</p>
        <table class="vyzva-tabulka" data-vyzva-tabulka="rekapitulace-licence">
            <thead><tr><th>Poř. č.</th><th>Název požadavku</th><th>Cena [Kč]</th></tr></thead>
            <tbody>
            @foreach (var p in sLicenci)
            {
                <tr><td>@p.PoradoveOznaceni</td><td>@p.Nazev</td><td>@Castka(p.Kalkulace.CenaLicence)</td></tr>
            }
                <tr><td></td><td></td><td></td></tr>
                <tr class="vyzva-soucet"><td>Celkem</td><td>bez DPH</td><td>@Castka(Model.LicenceBezDph)</td></tr>
                <tr><td></td><td>DPH @dphProcenta %</td><td>@Castka(Model.LicenceDph)</td></tr>
                <tr><td></td><td>s DPH</td><td>@Castka(Model.LicenceSDph)</td></tr>
            </tbody>
        </table>

        <table class="vyzva-tabulka" data-vyzva-tabulka="souhrn">
            <thead>
                <tr>
                    <th>Název</th><th>Položková cena bez DPH [Kč]</th>
                    <th>Položková cena DPH @dphProcenta % [Kč]</th><th>Položková cena s DPH [Kč]</th>
                </tr>
            </thead>
            <tbody>
                <tr>
                    <td>Úprava APV a DZ celkem</td>
                    <td>@Castka(Model.CelkemBezDph)</td><td>@Castka(Model.CelkemDph)</td><td>@Castka(Model.CelkemSDph)</td>
                </tr>
                <tr>
                    <td>Licenční rozšíření APV a DZ celkem</td>
                    <td>@Castka(Model.LicenceBezDph)</td><td>@Castka(Model.LicenceDph)</td><td>@Castka(Model.LicenceSDph)</td>
                </tr>
                <tr class="vyzva-soucet">
                    <td>CELKEM</td>
                    <td>@Castka(Model.CelkemBezDph + Model.LicenceBezDph)</td>
                    <td>@Castka(Model.CelkemDph + Model.LicenceDph)</td>
                    <td>@Castka(Model.CelkemSDph + Model.LicenceSDph)</td>
                </tr>
            </tbody>
        </table>
    </section>

    <section class="vyzva-zalomit" data-vyzva-sekce="4">
        <h2>4. Identifikační údaje nabyvatele</h2>
        <p class="vyzva-bez-mezer">
            Česká republika – Ministerstvo obrany<br />
            Tychonova 1<br />
            160 00 Praha 6<br />
            IČO: 60162694, DIČ: CZ60162694<br />
            v zastoupení<br />
            Sekce vyzbrojování a akvizic MO<br />
            odbor vyzbrojování pozemních sil a komunikačních a informačních systémů<br />
            náměstí Svobody 471/4<br />
            160 01 Praha 6
        </p>
    </section>

    <section data-vyzva-sekce="5">
        <h2>5. Termín a místo plnění</h2>
        @* Datum „Nechat volné" (komentář autora vzoru). *@
        <p>Termín pro splnění dílčí veřejné zakázky do: </p>
        <p>Místem plnění je:</p>
        <p class="vyzva-tucne">@Model.MistoPlneni</p>
    </section>

    <section data-vyzva-sekce="6">
        <h2>6. Lhůta pro písemné potvrzení Výzvy</h2>
        <p class="vyzva-tucne">Dodavatel dle čl. IV odst. 1 rámcové smlouvy potvrdí tuto Výzvu do 5 dnů od jejího doručení.</p>
    </section>

    <section data-vyzva-sekce="7">
        <h2>7. Datum a místo potvrzení výzvy dodavatelem</h2>
        @* Obě jména ponechal uživatel 2026-09-10 — jsou ve vzoru a s výzvou se nemění. *@
        <table class="vyzva-podpisy">
            <tbody>
                <tr><td class="vyzva-tucne">Za nabyvatele:</td><td class="vyzva-tucne">Za dodavatele:</td></tr>
                <tr><td>………………………</td><td>………………………</td></tr>
                <tr><td>Ing. Petr ZÁBOREC</td><td>Ing. Břetislav MOC</td></tr>
                <tr><td>ředitel</td><td>předseda správní rady</td></tr>
            </tbody>
        </table>
    </section>
</div>
</body>
</html>
```

- [ ] **Krok 4: Styly náhledu**

V `pdf-export.css` nahraď blok od `.vyzva-sheet { font-size: 10.5pt; line-height: 1.35; }`
po `.vyzva-kalkulace { break-inside: avoid; page-break-inside: avoid; }` (včetně) tímto.
Pravidla `.vyzva-pozadavek*` pod ním zůstávají.

```css
/* Výzva podle finálního vzoru (spec 2026-09-10 B1): Times New Roman, tělo 12 b., tabulky 10 b. */
.vyzva-sheet { font-family: "Times New Roman", Times, serif; font-size: 12pt; line-height: 1.35; }
.vyzva-priloha { text-align: right; margin: 0 0 1.5rem; }
.vyzva-hlavicka { margin-bottom: 1.5rem; }
.vyzva-hlavicka p { margin: 0; }
.vyzva-hlavicka-urad { font-size: 16pt; font-weight: 700; }
.vyzva-hlavicka-odbor { font-weight: 700; }
.vyzva-male { font-size: 10pt; }
.vyzva-cj { display: flex; margin: 1.5rem 0; }
.vyzva-cj-datum { margin-left: 12rem; }
.vyzva-nadpis { font-size: 12pt; text-align: justify; margin: 1.5rem 0; }
.vyzva-blok { text-align: justify; }
.vyzva-stred { text-align: center; font-weight: 700; margin: 1rem 0; }
.vyzva-tucne { font-weight: 700; }
.vyzva-bez-mezer { margin: 0; }
.vyzva-sheet h2 { font-size: 12pt; margin: 1.4rem 0 0.6rem; }
.vyzva-sheet h3 { font-size: 12pt; margin: 1rem 0 0.4rem; }

.vyzva-tabulka { width: 100%; border-collapse: collapse; margin: 0.5rem 0 1rem; font-size: 10pt; }
.vyzva-tabulka th,
.vyzva-tabulka td { border: 1px solid #000; padding: 3px 5px; vertical-align: top; }
.vyzva-tabulka th { background: #efefef; text-align: left; }
.vyzva-tabulka tr.vyzva-soucet td { font-weight: 700; }
.vyzva-kalkulace td:nth-child(n+3),
.vyzva-kalkulace th:nth-child(n+3) { text-align: right; }

.vyzva-podpisy { width: 100%; margin-top: 2rem; font-size: 9pt; }
.vyzva-podpisy td { width: 50%; padding: 0.35rem 0; vertical-align: top; }

/* Zalomení před sekcemi 2, 3 a 4 jako ve vzoru; nadpis požadavku drží s tabulkou
   a řádek tabulky se nedělí — náhrada ručních zalomení konkrétní výzvy (spec B8). */
.vyzva-zalomit { break-before: page; page-break-before: always; }
.vyzva-sheet h3 { break-after: avoid; page-break-after: avoid; }
.vyzva-tabulka tr { break-inside: avoid; page-break-inside: avoid; }
```

- [ ] **Krok 5: Spustit a ověřit průchod**

```bash
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj --filter "FullyQualifiedName~VyzvyTiskTests"
```

Očekávej: všechny zelené — i dosavadní `VyzvaTisk_VrátiDokumentSObsahemVyzvy` (kód výzvy, číslo
PNF a číslo smlouvy v HTML zůstávají) a `VyzvaTisk_NahledVysaziTextPozadavku_BezSkriptu`
(třída `vyzva-pozadavek` zůstává).

- [ ] **Krok 6: Ověření bloku**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
```

Očekávej: build 0/0, Api jen 4 známá selhání.

---

## Závěrečné ověření

- [ ] **Krok 1: Plná sada**

```bash
dotnet build PmTracker.sln
dotnet test PmTracker.Tests.Unit/PmTracker.Tests.Unit.csproj
dotnet test PmTracker.Tests.Api/PmTracker.Tests.Api.csproj
dotnet test PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj
```

Očekávej: build 0/0, Unit zelená, Api jen 4 známá selhání, Integration jen 1 známé selhání.

- [ ] **Krok 2: Changelog**

Do `docs/changelog/releases/0.9.md` doplň. Do **Přidáno**:

```markdown
- Skutečná cena PNF z akceptované kalkulace: chip externí vazby, karta PNF i součet výzvy na záložce Výzvy ukazují cenu z kalkulace. Předpokládaná cena zůstává a do doby, než je kalkulace známá, se zobrazí s označením „(předp.)“.
```

Do **Změněno**:

```markdown
- Tisk výzvy podle finálního vzoru: stručné popisy požadavků v první sekci, římské číslování, číslo úkolu s prefixem RU, tabulky podle obsahu kalkulace včetně licenčního rozšíření z rozpisu kalkulace, ve Wordu záhlaví s přílohou, číslo stránky a písmo Times New Roman 12.
- Export záznamů do PDF a Wordu uvádí u externí vazby jen skutečnou cenu z kalkulace, předpokládanou už ne.
```

Do **Opraveno**:

```markdown
- Uložení záznamu vracelo PNF z výzvy do bufferu — a to i z odeslané, uzamčené výzvy.
- Nově přidanou PNF šlo zařadit do bufferu až po uložení a novém otevření záznamu.
- Schválení návrhu založení záznamu ztrácelo u PNF text požadavku.
```

Pak `bash scripts/generate-changelog.sh`. Kořenový `CHANGELOG.md` ručně needituj.

- [ ] **Krok 3: Balík k nasazení**

```bash
cd PmTracker.Web && dotnet publish -c Release -o ../publish && cd ..
cp db_upgrade_1_4_2_externi_odkaz_pozadavek.sql db_upgrade_1_4_3_externi_odkaz_kalkulace.sql publish/
rm -f publish.zip && (cd publish && zip -rq ../publish.zip .)
```

V balíku nesmí být `appsettings.json` ani vzor výzvy — zkontroluj `unzip -l publish.zip`.
**Pořadí nasazení: nejdřív oba SQL skripty, pak binárky** — nová verze čte sloupce, které skripty
přidávají, a bez nich spadne editor záznamu i záložka Výzvy.

- [ ] **Krok 4: Ruční průchod (nasazení uživatelem)**

1. PNF v rozpracované výzvě: změň předpokládanou cenu, ulož záznam — PNF zůstane ve výzvě.
   Totéž u odeslané výzvy.
2. Nová PNF: zadej číslo tiketu, zapni „Zařadit" (ukáže „Po uložení půjde do bufferu"), ulož —
   PNF je v bufferu.
3. Návrh založení záznamu s textem požadavku u PNF — po schválení text u vazby je.
4. PNF s akceptovanou kalkulací: po uložení záznamu (harvest proběhne na pozadí) chip ukáže
   skutečnou cenu, tooltip obě. PNF bez kalkulace ukáže předpokládanou „(předp.)".
5. Záložka Výzvy: karta PNF a CELKEM se skutečnou cenou, poznámka o PNF jen s předpokládanou.
6. Export záznamů do PDF i Wordu: u vazby jen skutečná cena.
7. Tisk výzvy: otevři Word **ve Wordu** a porovnej se vzorem `2027xxxx_N_8201_Vyzva_c_x_2027_EIS.docx` —
   záhlaví, číslo stránky, římská čísla, seznamy v textu požadavku, tabulky podle kalkulace,
   POL a PPOL prázdné, podpisy. Náhled otevři v prohlížeči.

- [ ] **Krok 5: Předání**

Commity drží uživatel. Nahlas výsledky sad, co je ověřené ručně a co ne, a nech commit na něm.
