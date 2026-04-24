# Feature D — externí vazba hard constraint implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Vytvoření `ZaznamExterniOdkaz` (nová externí vazba na SD ticket) **vyžaduje platné 6-ciferné `HOT_ZAZNAMY.id` ověřené proti ServiceDesk DB**. Bez SD lookupu (`Ticketing:Enabled=false` nebo SD nedostupný nebo ID neexistuje v HOT_ZAZNAMY) nový externí záznam nelze uložit do DB. Stávající externí vazby zůstávají beze změny.

**Architecture:** Pre-save validace v controlleru / service vrstvě volá `IVyjadreniQueryService.GetHotZaznamFingerprintsAsync({cislo})`. Pokud dictionary neobsahuje klíč → reject s friendly error. Error se zobrazí v UI formuláře pro create externí vazby.

**Tech Stack:** ASP.NET Core 8 MVC + existing `IVyjadreniQueryService` (Sprint A), unit + integration testy.

**Spec source:**
- Memory: `project_servicedesk_infosystem_binding.md` → sekce „Externí vazba hard constraint (2026-04-24)"
- Memory: `feedback_sd_ticket_id_required.md` → rámec „ticket bez id mimo scope"
- Decision brief: U10

---

## File Structure

### Nové soubory
| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidator.cs` | Pure validator service — `Task<ExterniOdkazValidationResult> ValidateCreateAsync(string cislo6, CancellationToken)` |
| `PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidationResult.cs` | DTO: `bool IsValid`, `string? ErrorCode`, `string? ErrorMessage` |
| `PmTracker.Tests.Unit/Services/ServiceDesk/ExterniOdkazValidatorTests.cs` | Unit testy všech edge cases |

### Modifikované soubory
| Soubor | Změna |
|---|---|
| `PmTracker.Web/Controllers/???Controller.cs` | **Předtím jdi najít** — kde je endpoint pro create externí vazby? Grep `ZaznamExterniOdkaz` + `Create` + `POST` |
| `PmTracker.Web/Services/Security/DependencyInjection.cs` (nebo kde se DI registruje) | `AddScoped<ExterniOdkazValidator>()` |

---

## Tasks

### Task 1: Najít existující create flow externí vazby

**Files:**
- Investigate only

- [ ] **Step 1: Grep pro create flow**

```bash
cd "/Users/Pavel.Andrlik/Documents/PM Tracker"
grep -rn "ZaznamExterniOdkaz\|externi.*odkaz\|ExterniVazba" \
  --include="*.cs" --include="*.cshtml" 2>&1 \
  | grep -iE "Create|Add|Save|Post|Nov[áý]" \
  | head -20
```

Expected: odkryje controller action + view form + case service.

- [ ] **Step 2: Přečíst identified create action**

Typicky: `ZaznamyController.AddExterniOdkaz` nebo `ExterniOdkazyController.Create` nebo podobně. Prozkoumej:
- Metoda a binding ViewModel
- Kde se `cislo` nastavuje
- Zda existuje nějaký validation flow (`ModelState.IsValid`, FluentValidation)

- [ ] **Step 3: Zadokumentuj nalezené**

V komentu commit příštího tasku. Žádný code change v tomto kroku.

---

### Task 2: Validator service — TDD

**Files:**
- Create: `PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidationResult.cs`
- Create: `PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidator.cs`
- Create: `PmTracker.Tests.Unit/Services/ServiceDesk/ExterniOdkazValidatorTests.cs`

- [ ] **Step 1: DTO pro result**

```csharp
// PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidationResult.cs
namespace PmTracker.Web.Services.ServiceDesk;

public sealed record ExterniOdkazValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage)
{
    public static ExterniOdkazValidationResult Ok() => new(true, null, null);
    public static ExterniOdkazValidationResult Fail(string code, string msg) => new(false, code, msg);

    public const string CodeFormat = "format";
    public const string CodeDisabled = "ticketing_disabled";
    public const string CodeNotFound = "ticket_not_found";
    public const string CodeSdUnavailable = "sd_unavailable";
}
```

- [ ] **Step 2: Failing tests first (RED)**

```csharp
// PmTracker.Tests.Unit/Services/ServiceDesk/ExterniOdkazValidatorTests.cs
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.Services.ServiceDesk;

public sealed class ExterniOdkazValidatorTests
{
    private static ExterniOdkazValidator CreateSubject(
        bool ticketingEnabled = true,
        Dictionary<string, HotZaznamFingerprintDto>? fingerprints = null,
        bool queryThrows = false)
    {
        var options = Options.Create(new TicketingOptions
        {
            Enabled = ticketingEnabled,
            ConnectionStringName = "TicketingReadOnly"
        });

        var mock = new Mock<IVyjadreniQueryService>();
        mock.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<string> cisla, CancellationToken _) =>
            {
                if (queryThrows) throw new InvalidOperationException("SD unavailable");
                return Task.FromResult<IReadOnlyDictionary<string, HotZaznamFingerprintDto>>(
                    fingerprints ?? new Dictionary<string, HotZaznamFingerprintDto>());
            });

        return new ExterniOdkazValidator(mock.Object, options, NullLogger<ExterniOdkazValidator>.Instance);
    }

    [Theory]
    [InlineData("12345")]      // too short
    [InlineData("1234567")]    // too long
    [InlineData("abcdef")]     // non-numeric
    [InlineData("12a456")]     // mixed
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateCreateAsync_InvalidFormat_ReturnsFailWithFormatCode(string? input)
    {
        var validator = CreateSubject();
        var r = await validator.ValidateCreateAsync(input!, CancellationToken.None);
        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeFormat);
    }

    [Fact]
    public async Task ValidateCreateAsync_TicketingDisabled_ReturnsFailWithDisabledCode()
    {
        var validator = CreateSubject(ticketingEnabled: false);
        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);
        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeDisabled);
        r.ErrorMessage.Should().Contain("SD integrace");
    }

    [Fact]
    public async Task ValidateCreateAsync_SdUnavailable_ReturnsFailWithUnavailableCode()
    {
        var validator = CreateSubject(queryThrows: true);
        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);
        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeSdUnavailable);
    }

    [Fact]
    public async Task ValidateCreateAsync_TicketNotInHotZaznamy_ReturnsFailWithNotFoundCode()
    {
        var validator = CreateSubject(fingerprints: new Dictionary<string, HotZaznamFingerprintDto>());
        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);
        r.IsValid.Should().BeFalse();
        r.ErrorCode.Should().Be(ExterniOdkazValidationResult.CodeNotFound);
        r.ErrorMessage.Should().Contain("123456");
    }

    [Fact]
    public async Task ValidateCreateAsync_TicketExists_ReturnsOk()
    {
        var fp = new HotZaznamFingerprintDto("123456", new DateTime(2026, 4, 1), "otevřeno", "PNF");
        var validator = CreateSubject(fingerprints: new Dictionary<string, HotZaznamFingerprintDto> { ["123456"] = fp });
        var r = await validator.ValidateCreateAsync("123456", CancellationToken.None);
        r.IsValid.Should().BeTrue();
        r.ErrorCode.Should().BeNull();
    }
}
```

Run: `dotnet test PmTracker.Tests.Unit -c Release --filter "ExterniOdkazValidator" --no-restore`
Expected: FAIL 10/10 (class neexistuje).

- [ ] **Step 3: Implementovat service**

```csharp
// PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidator.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PmTracker.ServiceDesk.Contracts;
using System.Text.RegularExpressions;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Hard constraint validator pro vytvoření externí vazby na SD ticket.
/// Pravidlo: cislo musí být 6 cifer (HOT_ZAZNAMY.id je int, user-facing 6-digit),
/// SD integrace musí být zapnutá, a ticket musí existovat v HOT_ZAZNAMY.
/// </summary>
public sealed class ExterniOdkazValidator
{
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly IOptions<TicketingOptions> _options;
    private readonly ILogger<ExterniOdkazValidator> _logger;

    public ExterniOdkazValidator(
        IVyjadreniQueryService vyjadreni,
        IOptions<TicketingOptions> options,
        ILogger<ExterniOdkazValidator> logger)
    {
        _vyjadreni = vyjadreni;
        _options = options;
        _logger = logger;
    }

    public async Task<ExterniOdkazValidationResult> ValidateCreateAsync(string cislo6, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo6) || !SixDigits.IsMatch(cislo6))
        {
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeFormat,
                "Číslo ticketu musí být přesně 6 cifer (např. 123456).");
        }

        if (!_options.Value.Enabled)
        {
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeDisabled,
                "SD integrace je vypnutá — novou externí vazbu nelze založit. Kontaktuj administrátora.");
        }

        IReadOnlyDictionary<string, HotZaznamFingerprintDto> fingerprints;
        try
        {
            fingerprints = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { cislo6 }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SD lookup selhal při validaci externí vazby pro #{Cislo}", cislo6);
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeSdUnavailable,
                "ServiceDesk není dostupný — nelze ověřit existenci ticketu. Zkus to později.");
        }

        if (!fingerprints.ContainsKey(cislo6))
        {
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeNotFound,
                $"Ticket #{cislo6} v ServiceDesku neexistuje — externí vazbu nelze založit.");
        }

        return ExterniOdkazValidationResult.Ok();
    }
}
```

- [ ] **Step 4: Tests → GREEN**

```bash
dotnet test PmTracker.Tests.Unit -c Release --filter "ExterniOdkazValidator" --no-restore
```
Expected: PASS 10/10.

- [ ] **Step 5: DI registrace**

Najdi kde se registrují ServiceDesk services (pravděpodobně `PmTracker.Web/Program.cs` nebo `ServiceDeskServiceCollectionExtensions.cs`). Přidej:

```csharp
services.AddScoped<PmTracker.Web.Services.ServiceDesk.ExterniOdkazValidator>();
```

- [ ] **Step 6: Commit**

```bash
git add PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidationResult.cs \
        PmTracker.Web/Services/ServiceDesk/ExterniOdkazValidator.cs \
        PmTracker.Tests.Unit/Services/ServiceDesk/ExterniOdkazValidatorTests.cs \
        PmTracker.Web/Program.cs  # nebo wherever DI
git commit -m "feat(sd): ExterniOdkazValidator — hard constraint 6-cifer + SD lookup"
```

---

### Task 3: Integrace validátoru do create flow

**Files:**
- Modify: controller action nalezená v Task 1

- [ ] **Step 1: Inject validator do controlleru**

Přidat parameter `ExterniOdkazValidator externiOdkazValidator` do existujícího controlleru. Uložit jako field.

- [ ] **Step 2: Přidat validaci na začátek create action**

Před existing save/create logiku přidej:

```csharp
var validationResult = await _externiOdkazValidator.ValidateCreateAsync(cislo, ct).ConfigureAwait(false);
if (!validationResult.IsValid)
{
    ModelState.AddModelError(nameof(model.Cislo), validationResult.ErrorMessage ?? "Ticket nebyl ověřen.");
    // audit trail attempt
    _logger.LogInformation("Create externí vazby odmítnut: cislo={Cislo}, kód={Code}",
        cislo, validationResult.ErrorCode);
    return View(model); // nebo vrátit příslušný response
}
```

Konkrétní signature záleží na controllerovém patternu nalezeném v Task 1.

- [ ] **Step 3: UI — zobrazení chyby**

V View pro create externí vazby ověř, že `ModelState` errors se renderují (typicky `<span asp-validation-for="Cislo">` nebo gov-form error pattern). Pokud neexistuje, přidej.

- [ ] **Step 4: Integration test (doporučeno pokud máš Docker)**

```bash
# Spustí real API test proti docker MS SQL → ověří že endpoint reject 404-unknown cislo
dotnet test PmTracker.Tests.Api -c Release --filter "ExterniVazbaCreate" --no-restore
```

Pokud Docker není dostupný (viz commit d0c45b8 friendly error), test skipne nebo failne s čitelným důvodem. Nepovažuj to za regresi.

- [ ] **Step 5: Commit**

```bash
git add PmTracker.Web/Controllers/???Controller.cs \
        PmTracker.Web/Views/???/???.cshtml  # pokud UI change
git commit -m "feat(sd): validace 6-cifer ID při create externí vazby (hard constraint)"
```

---

### Task 4: Audit + dokumentace

- [ ] **Step 1: Memory entry** (už je v paměti z předchozího kroku, ale ověř)

```bash
grep "Externí vazba hard constraint" /Users/Pavel.Andrlik/.claude/projects/-Users-Pavel-Andrlik-Documents-PM-Tracker/memory/project_servicedesk_infosystem_binding.md | head -3
```

Pokud match → OK, nic měnit. Pokud ne → přidat entry (viz memory update z plánu 2026-04-24).

- [ ] **Step 2: Doplnit do docs/technical/13-servicedesk-connection-setup.md troubleshooting sekci**

Přidej řádek do tabulky Troubleshooting v §6:

```
| „Ticket #123456 v ServiceDesku neexistuje — externí vazbu nelze založit" | User zadává číslo, které v HOT_ZAZNAMY.id skutečně není (třeba typo) | Ověř v SD UI (https://servicedesk.fis.acr/Hotline/Ticket/Details/123456) že ticket existuje. Zkontroluj typo. |
| „SD integrace je vypnutá — novou externí vazbu nelze založit" | `Ticketing:Enabled=false` | Přepni v appsettings.json + restart |
| „ServiceDesk není dostupný — nelze ověřit existenci ticketu" | Network / SD SQL server down | Checkni connectivity (`telnet <host> 1433`) + SD SQL service status |
```

- [ ] **Step 3: Commit**

```bash
git add docs/technical/13-servicedesk-connection-setup.md
git commit -m "docs(sd): troubleshooting pro validation chyby při create externí vazby"
```

---

## Deliverable

- ✅ `ExterniOdkazValidator` service s 4 pure validations (format, enabled, SD reachable, ticket exists)
- ✅ 10 unit testů (invalid format, disabled, unavailable, not found, valid)
- ✅ Integrace do existujícího create flow (controller + ModelState)
- ✅ User-friendly chybové zprávy česky
- ✅ Audit log při rejection (server-side warning level)
- ✅ Troubleshooting docs rozšířené
