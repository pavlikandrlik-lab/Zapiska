# Serverové PDF pro tisk — implementační plán

> **Pro agentní exekutory:** POVINNÝ SUB-SKILL: `superpowers:executing-plans` (inline exekuce, zvoleno uživatelem). Kroky používají `- [ ]` pro sledování postupu.

**Cíl:** Tisková tlačítka vrací skutečné PDF s patičkou „Strana X z Y" dole uprostřed; při selhání generátoru se tisk vrátí na dnešní HTML cestu.

**Architektura:** Stávající tisková Razor šablona se vyrenderuje do stringu, prožene headless Edgem přes CDP (PuppeteerSharp) a vrátí jako `application/pdf` s `inline` disposition. Patičku sází prohlížeč, takže čísla odpovídají skutečnému zalomení. Šablona ani tiskové CSS se nemění — zůstávají zdrojem PDF i záchrannou cestou.

**Tech stack:** .NET 8, ASP.NET Core MVC, PuppeteerSharp 25.8.0 (aplikace), UglyToad.PdfPig 0.1.16 (jen testy), xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-04-serverove-pdf-tisk-design.md`

## Globální omezení

- **Commity se NEDĚLAJÍ.** Uživatel commituje sám po ručním ověření na i15. Místo kroku „commit" je vždy **checkpoint**: shrnout, co je hotové, a pokračovat.
- Cílový framework `net8.0`; PuppeteerSharp **25.8.0**, PdfPig **0.1.16** (ověřeno, že se obojí přeloží na net8.0).
- `Microsoft.Playwright` **nesmí** přibýt do `PmTracker.Web` — jeho node driver má 125 MB. Patří jen do `PmTracker.Tests.E2E`.
- Texty v UI, komentářích i logu česky, bez anglicismů. Komentáře odkazují na spec datem `2026-09-04`.
- `Views/Export/*.cshtml` a `wwwroot/css/pdf-export.css` se v tomto plánu **nemění** (výjimka: krok 6.4 může vynulovat `@page` margin, pokud se okraje prokazatelně sčítají).
- Oprávnění tiskových akcí (`permission:export.pdf.projekt|jednani|ukol`) zůstávají beze změny.
- Api testy **nesmí** spouštět prohlížeč — používají podvržený `IPdfRenderer`.
- Testy nesmí tiše přeskakovat. Když integrační test nenajde prohlížeč, **selže s vysvětlením**; vakuově procházející test je v tomto repu známý problém (viz `RecordEditModalCloseScenariosTests`).

## Struktura souborů

| Soubor | Odpovědnost |
|---|---|
| `PmTracker.Web/Services/Export/PdfExportOptions.cs` | nastavení sekce `Export:Pdf` |
| `PmTracker.Web/Services/Export/BrowserExecutableResolver.cs` | najde spustitelný prohlížeč (nastavená cesta > autodetekce) |
| `PmTracker.Web/Services/Export/IPdfRenderer.cs` | kontrakt generátoru + `PdfRenderRequest` / `PdfRenderResult` |
| `PmTracker.Web/Services/Export/PdfFooterTemplate.cs` | HTML patičky a prázdné hlavičky |
| `PmTracker.Web/Services/Export/ChromiumPdfRenderer.cs` | spuštění Edge, sazba PDF, brána souběhu |
| `PmTracker.Web/Services/Export/RazorViewRenderer.cs` | Razor view → HTML string |
| `PmTracker.Web/Controllers/ExportController.cs` | tři tiskové akce vrací PDF, fallback na `View(...)` |

---

### Task 1: Konfigurace a nalezení prohlížeče

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Export/PdfExportOptions.cs`
- Vytvořit: `PmTracker.Web/Services/Export/BrowserExecutableResolver.cs`
- Test: `PmTracker.Tests.Unit/Export/BrowserExecutableResolverTests.cs`

**Rozhraní:**
- Poskytuje dál: `PdfExportOptions` (`Enabled`, `BrowserExecutablePath`, `TimeoutSeconds`, `MaxConcurrent`), `IBrowserExecutableResolver.Resolve() → string?`

- [ ] **Krok 1: Napsat padající test**

`PmTracker.Tests.Unit/Export/BrowserExecutableResolverTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>Serverové PDF (2026-09-04): hledání prohlížeče na serveru.</summary>
public sealed class BrowserExecutableResolverTests
{
    [Fact]
    public void Resolve_PrefersConfiguredPath_OverAutodetection()
    {
        var options = new PdfExportOptions { BrowserExecutablePath = @"D:\edge\msedge.exe" };
        var resolver = new BrowserExecutableResolver(options, _ => true);

        resolver.Resolve().Should().Be(@"D:\edge\msedge.exe",
            "nastavená cesta je rozhodnutí správce serveru a má přednost");
    }

    [Fact]
    public void Resolve_KeepsConfiguredPath_EvenWhenMissing()
    {
        var options = new PdfExportOptions { BrowserExecutablePath = @"D:\edge\msedge.exe" };
        var resolver = new BrowserExecutableResolver(options, _ => false);

        resolver.Resolve().Should().Be(@"D:\edge\msedge.exe",
            "tiché sklouznutí na jiný prohlížeč by správci zamlčelo překlep v konfiguraci");
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoBrowserExists()
    {
        var resolver = new BrowserExecutableResolver(new PdfExportOptions(), _ => false);

        resolver.Resolve().Should().BeNull(
            "bez prohlížeče se tisk vrací na HTML cestu, nesmí spadnout");
    }

    [Fact]
    public void Resolve_FindsFirstExistingCandidate()
    {
        var probed = new List<string>();
        var resolver = new BrowserExecutableResolver(new PdfExportOptions(), path =>
        {
            probed.Add(path);
            return path.Contains("Edge", StringComparison.OrdinalIgnoreCase);
        });

        resolver.Resolve().Should().NotBeNull();
        resolver.Resolve()!.Should().Contain("Edge");
        probed.Should().NotBeEmpty("autodetekce má projít známé cesty");
    }
}
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~BrowserExecutableResolverTests"`
Očekávat: chyba překladu — `PdfExportOptions` ani `BrowserExecutableResolver` neexistují.

- [ ] **Krok 3: Napsat minimální implementaci**

`PmTracker.Web/Services/Export/PdfExportOptions.cs`:

```csharp
namespace PmTracker.Web.Services.Export;

/// <summary>
/// Nastavení serverového generování PDF (sekce <c>Export:Pdf</c>).
/// Spec 2026-09-04-serverove-pdf-tisk-design.md, §7.
/// </summary>
public sealed class PdfExportOptions
{
    public const string SectionName = "Export:Pdf";

    /// <summary>Vypínač bez nasazení nové verze — false vrátí tisk na dnešní HTML cestu.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Cesta k msedge.exe. Prázdné = autodetekce ve známých cestách.</summary>
    public string? BrowserExecutablePath { get; set; }

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Strop souběžných tisků — jedna instance prohlížeče ≈ 200 MB.</summary>
    public int MaxConcurrent { get; set; } = 2;
}
```

`PmTracker.Web/Services/Export/BrowserExecutableResolver.cs`:

```csharp
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;

namespace PmTracker.Web.Services.Export;

public interface IBrowserExecutableResolver
{
    /// <summary>Cesta ke spustitelnému prohlížeči, nebo null když žádný není.</summary>
    string? Resolve();
}

/// <summary>
/// Hledá prohlížeč pro sazbu PDF. Serverové PDF (2026-09-04), spec §7.
/// </summary>
public sealed class BrowserExecutableResolver : IBrowserExecutableResolver
{
    private static readonly string[] WindowsCandidates =
    [
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
    ];

    private static readonly string[] UnixCandidates =
    [
        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
    ];

    private readonly PdfExportOptions _options;
    private readonly Func<string, bool> _fileExists;

    public BrowserExecutableResolver(IOptions<PdfExportOptions> options)
        : this(options.Value, File.Exists)
    {
    }

    /// <summary>Konstruktor pro testy — dovolí ověřit pořadí hledání bez skutečného disku.</summary>
    public BrowserExecutableResolver(PdfExportOptions options, Func<string, bool> fileExists)
    {
        _options = options;
        _fileExists = fileExists;
    }

    public string? Resolve()
    {
        var configured = _options.BrowserExecutablePath?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            // Vrací se i když soubor neexistuje — správce musí uvidět chybu s vlastní cestou,
            // ne tiché sklouznutí na jiný prohlížeč.
            return configured;
        }

        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsCandidates
            : UnixCandidates;

        return candidates.FirstOrDefault(path => _fileExists(path));
    }
}
```

- [ ] **Krok 4: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~BrowserExecutableResolverTests"`
Očekávat: 4/4 prošly.

- [ ] **Krok 5: Checkpoint**

Shrnout: konfigurace a hledání prohlížeče hotové, 4 testy zelené. **Necommitovat.**

---

### Task 2: Kontrakt generátoru a patička

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Export/IPdfRenderer.cs`
- Vytvořit: `PmTracker.Web/Services/Export/PdfFooterTemplate.cs`
- Test: `PmTracker.Tests.Unit/Export/PdfFooterTemplateTests.cs`

**Rozhraní:**
- Používá z Task 1: nic
- Poskytuje dál: `IPdfRenderer.RenderAsync(PdfRenderRequest, CancellationToken) → Task<PdfRenderResult>`; `PdfRenderRequest { Html, StylesheetPath }`; `PdfRenderResult.Success(byte[])` / `PdfRenderResult.Failure(string)` s `Succeeded`; `PdfFooterTemplate.Html`, `PdfFooterTemplate.EmptyHeaderHtml`

- [ ] **Krok 1: Napsat padající test**

`PmTracker.Tests.Unit/Export/PdfFooterTemplateTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Serverové PDF (2026-09-04): patička se sází v prohlížeči, čísla stránek se
/// nesmí vkládat z C# — v době skládání HTML ještě neznáme zalomení.
/// </summary>
public sealed class PdfFooterTemplateTests
{
    [Fact]
    public void Footer_UsesBrowserPageCounters()
    {
        PdfFooterTemplate.Html.Should().Contain("class=\"pageNumber\"");
        PdfFooterTemplate.Html.Should().Contain("class=\"totalPages\"");
    }

    [Fact]
    public void Footer_IsCentered()
    {
        PdfFooterTemplate.Html.Should().Contain("text-align:center",
            "zadání je číslování dole uprostřed");
    }

    [Fact]
    public void Footer_ReadsStranaXzY()
    {
        PdfFooterTemplate.Html.Should().MatchRegex(
            @"Strana\s*<span class=""pageNumber""></span>\s*z\s*<span class=""totalPages""></span>");
    }

    [Fact]
    public void Footer_HasExplicitFontSize()
    {
        // Bez vlastní velikosti sází Chromium patičku nečitelně malým písmem.
        PdfFooterTemplate.Html.Should().Contain("font-size:");
    }

    [Fact]
    public void Header_IsEmpty_SoBrowserDoesNotAddItsOwn()
    {
        PdfFooterTemplate.EmptyHeaderHtml.Should().Be("<div></div>",
            "prázdná hlavička potlačí výchozí název + URL od prohlížeče");
    }

    [Fact]
    public void RenderResult_DistinguishesSuccessFromFailure()
    {
        PdfRenderResult.Success([1, 2, 3]).Succeeded.Should().BeTrue();
        PdfRenderResult.Failure("bez prohlížeče").Succeeded.Should().BeFalse();
        PdfRenderResult.Failure("bez prohlížeče").FailureReason.Should().Be("bez prohlížeče");
    }
}
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PdfFooterTemplateTests"`
Očekávat: chyba překladu — `PdfFooterTemplate` a `PdfRenderResult` neexistují.

- [ ] **Krok 3: Napsat minimální implementaci**

`PmTracker.Web/Services/Export/IPdfRenderer.cs`:

```csharp
namespace PmTracker.Web.Services.Export;

public sealed record PdfRenderRequest
{
    public required string Html { get; init; }

    /// <summary>Absolutní cesta k tiskovému CSS; null = HTML si nese styly samo.</summary>
    public string? StylesheetPath { get; init; }
}

/// <summary>
/// Výsledek sazby. Selhání se nevyhazuje výjimkou — fallback na HTML tisk je
/// řízený tok, ne odchyt výjimky, a jde otestovat bez prohlížeče.
/// </summary>
public sealed record PdfRenderResult(byte[]? Bytes, string? FailureReason)
{
    public bool Succeeded => Bytes is not null;

    public static PdfRenderResult Success(byte[] bytes) => new(bytes, null);

    public static PdfRenderResult Failure(string reason) => new(null, reason);
}

public interface IPdfRenderer
{
    Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken ct);
}
```

`PmTracker.Web/Services/Export/PdfFooterTemplate.cs`:

```csharp
namespace PmTracker.Web.Services.Export;

/// <summary>
/// Patička tiskového PDF. Třídy <c>pageNumber</c> a <c>totalPages</c> plní sazeč
/// prohlížeče při lámání stránek. Spec 2026-09-04-serverove-pdf-tisk-design.md, §6.3.
/// </summary>
public static class PdfFooterTemplate
{
    public const string Html =
        "<div style=\"width:100%;font-family:Arial,sans-serif;font-size:9pt;color:#111;text-align:center;\">" +
        "Strana <span class=\"pageNumber\"></span> z <span class=\"totalPages\"></span>" +
        "</div>";

    /// <summary>Prázdná hlavička — bez ní vysází prohlížeč svou výchozí (název + URL).</summary>
    public const string EmptyHeaderHtml = "<div></div>";
}
```

- [ ] **Krok 4: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PdfFooterTemplateTests"`
Očekávat: 6/6 prošly.

- [ ] **Krok 5: Checkpoint**

Shrnout: kontrakt a patička hotové. **Necommitovat.**

---

### Task 3: Generátor PDF přes headless Edge

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Export/ChromiumPdfRenderer.cs`
- Změnit: `PmTracker.Web/PmTracker.Web.csproj` (přidat `PuppeteerSharp`)
- Změnit: `PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj` (přidat `PdfPig`)
- Test: `PmTracker.Tests.Integration/Export/ChromiumPdfRendererTests.cs`

**Rozhraní:**
- Používá z Task 1: `PdfExportOptions`, `IBrowserExecutableResolver.Resolve()`
- Používá z Task 2: `IPdfRenderer`, `PdfRenderRequest`, `PdfRenderResult`, `PdfFooterTemplate.Html`, `PdfFooterTemplate.EmptyHeaderHtml`
- Poskytuje dál: `ChromiumPdfRenderer : IPdfRenderer, IDisposable`

Tohle je nejdůležitější test celého plánu — bez něj číslování nikdo neuhlídá. Nepatří do `SqlIntegrationCollection`, databázi nepotřebuje.

- [ ] **Krok 1: Přidat balíčky**

Do `PmTracker.Web/PmTracker.Web.csproj`, do prvního `<ItemGroup>` s `PackageReference`, abecedně za `Markdig`:

```xml
    <PackageReference Include="PuppeteerSharp" Version="25.8.0" />
```

Do `PmTracker.Tests.Integration/PmTracker.Tests.Integration.csproj`, za `Microsoft.NET.Test.Sdk`:

```xml
    <PackageReference Include="PdfPig" Version="0.1.16" />
```

Spustit: `dotnet restore`
Očekávat: obnovení proběhne bez chyby.

- [ ] **Krok 2: Napsat padající test**

`PmTracker.Tests.Integration/Export/ChromiumPdfRendererTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Services.Export;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Xunit;

namespace PmTracker.Tests.Integration.Export;

/// <summary>
/// Serverové PDF (2026-09-04): skutečné vygenerování dokumentu headless prohlížečem.
/// Databázi nepotřebuje, proto stojí mimo SqlIntegrationCollection.
/// </summary>
public sealed class ChromiumPdfRendererTests
{
    private const string ThreePageHtml = """
        <html><head><meta charset="utf-8"></head><body>
        <div class="p">Strana jedna — příliš žluťoučký kůň úpěl ďábelské ódy</div>
        <div class="p">Strana dvě</div>
        <div>Strana tři</div>
        </body></html>
        """;

    private const string PageBreakCss =
        "body{font-family:Arial;font-size:12pt} .p{page-break-after:always;height:250mm}";

    private static string Squash(string value)
        => new(value.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

    [Fact]
    public async Task RenderAsync_NumbersEveryPage_InCenteredFooter()
    {
        var options = new PdfExportOptions();
        var resolver = new BrowserExecutableResolver(options, File.Exists);

        resolver.Resolve().Should().NotBeNull(
            "test skutečné sazby PDF potřebuje na stroji Edge nebo Chrome; " +
            "tiše přeskočený test by číslování nehlídal");

        var cssPath = Path.Combine(Path.GetTempPath(), $"pmtracker-pdf-test-{Guid.NewGuid():N}.css");
        await File.WriteAllTextAsync(cssPath, PageBreakCss);

        try
        {
            using var renderer = new ChromiumPdfRenderer(
                Options.Create(options), resolver, NullLogger<ChromiumPdfRenderer>.Instance);

            var result = await renderer.RenderAsync(
                new PdfRenderRequest { Html = ThreePageHtml, StylesheetPath = cssPath },
                CancellationToken.None);

            result.Succeeded.Should().BeTrue($"sazba selhala: {result.FailureReason}");

            using var pdf = PdfDocument.Open(result.Bytes!);
            pdf.NumberOfPages.Should().Be(3, "tři bloky s vynuceným zlomem = tři strany");

            for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
            {
                var text = ContentOrderTextExtractor.GetText(pdf.GetPage(pageNumber));
                Squash(text).Should().Contain($"Strana{pageNumber}z3",
                    $"na straně {pageNumber} musí být patička s vlastním číslem");
            }
        }
        finally
        {
            File.Delete(cssPath);
        }
    }

    [Fact]
    public async Task RenderAsync_ReportsFailure_WhenDisabled()
    {
        var options = new PdfExportOptions { Enabled = false };
        using var renderer = new ChromiumPdfRenderer(
            Options.Create(options),
            new BrowserExecutableResolver(options, File.Exists),
            NullLogger<ChromiumPdfRenderer>.Instance);

        var result = await renderer.RenderAsync(
            new PdfRenderRequest { Html = "<html><body>x</body></html>" }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("vypnut", "vypínač musí být v důvodu čitelně vidět");
    }

    [Fact]
    public async Task RenderAsync_ReportsFailure_WhenNoBrowserFound()
    {
        var options = new PdfExportOptions();
        using var renderer = new ChromiumPdfRenderer(
            Options.Create(options),
            new BrowserExecutableResolver(options, _ => false),
            NullLogger<ChromiumPdfRenderer>.Instance);

        var result = await renderer.RenderAsync(
            new PdfRenderRequest { Html = "<html><body>x</body></html>" }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("prohlížeč");
    }
}
```

- [ ] **Krok 3: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ChromiumPdfRendererTests"`
Očekávat: chyba překladu — `ChromiumPdfRenderer` neexistuje.

- [ ] **Krok 4: Napsat minimální implementaci**

`PmTracker.Web/Services/Export/ChromiumPdfRenderer.cs`:

```csharp
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Sází PDF headless prohlížečem (Edge nainstalovaný na serveru).
/// Prohlížeč se spouští na požadavek a po doběhnutí zaniká — vědomé rozhodnutí
/// (spec §9): stojí to ~1 s, ale odpadá zmrtvělá instance i únik paměti v poolu.
/// Spec 2026-09-04-serverove-pdf-tisk-design.md.
/// </summary>
public sealed class ChromiumPdfRenderer : IPdfRenderer, IDisposable
{
    private readonly PdfExportOptions _options;
    private readonly IBrowserExecutableResolver _resolver;
    private readonly ILogger<ChromiumPdfRenderer> _logger;
    private readonly SemaphoreSlim _gate;

    public ChromiumPdfRenderer(
        IOptions<PdfExportOptions> options,
        IBrowserExecutableResolver resolver,
        ILogger<ChromiumPdfRenderer> logger)
    {
        _options = options.Value;
        _resolver = resolver;
        _logger = logger;
        _gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrent));
    }

    public async Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            return PdfRenderResult.Failure("Serverové PDF je vypnuté (Export:Pdf:Enabled = false).");
        }

        var executablePath = _resolver.Resolve();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return PdfRenderResult.Failure("Na serveru nebyl nalezen prohlížeč pro sazbu PDF.");
        }

        var userDataDir = Path.Combine(Path.GetTempPath(), "pmtracker-pdf", Guid.NewGuid().ToString("N"));

        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(userDataDir);
            var bytes = await RenderCoreAsync(request, executablePath, userDataDir);
            return PdfRenderResult.Success(bytes);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sazba PDF selhala, tisk se vrací na HTML cestu.");
            return PdfRenderResult.Failure(ex.Message);
        }
        finally
        {
            _gate.Release();
            TryDeleteDirectory(userDataDir);
        }
    }

    private async Task<byte[]> RenderCoreAsync(
        PdfRenderRequest request, string executablePath, string userDataDir)
    {
        var launchOptions = new LaunchOptions
        {
            Browser = SupportedBrowser.Chrome,
            ExecutablePath = executablePath,
            Headless = true,
            UserDataDir = userDataDir,
            Timeout = _options.TimeoutSeconds * 1000,
            // Pod účtem aplikačního poolu nelze zakládat sandbox ani sdílenou paměť.
            Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
        };

        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();

        await page.SetContentAsync(request.Html, new SetContentOptions
        {
            WaitUntil = [WaitUntilNavigation.Load]
        });

        if (!string.IsNullOrWhiteSpace(request.StylesheetPath) && File.Exists(request.StylesheetPath))
        {
            // Šablona odkazuje CSS relativně; při sazbě z paměti se musí přilepit z disku.
            await page.AddStyleTagAsync(new AddTagOptions { Path = request.StylesheetPath });
        }

        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = PdfFooterTemplate.EmptyHeaderHtml,
            FooterTemplate = PdfFooterTemplate.Html,
            MarginOptions = new MarginOptions
            {
                Top = "5mm",
                Left = "5mm",
                Right = "5mm",
                Bottom = "14mm"
            }
        });
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Dočasný adresář prohlížeče {Path} se nepodařilo smazat.", path);
        }
    }

    public void Dispose() => _gate.Dispose();
}
```

- [ ] **Krok 5: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Integration --filter "FullyQualifiedName~ChromiumPdfRendererTests"`
Očekávat: 3/3 prošly, včetně „Strana 1 z 3", „Strana 2 z 3" a „Strana 3 z 3".

- [ ] **Krok 6: Checkpoint**

Shrnout: generátor umí vysázet očíslované PDF, ověřeno vytažením textu z reálného souboru. **Necommitovat.**

---

### Task 4: Tiskové akce vrací PDF

**Soubory:**
- Vytvořit: `PmTracker.Web/Services/Export/RazorViewRenderer.cs`
- Vytvořit: `PmTracker.Web/Services/Export/PdfExportFileName.cs`
- Změnit: `PmTracker.Web/Controllers/ExportController.cs`
- Změnit: `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs:195` (registrace vedle `IWordExportService`)
- Změnit: `PmTracker.Web/Program.cs` (navázání konfigurace)
- Vytvořit: `PmTracker.Tests.Api/TestInfrastructure/FakePdfRenderer.cs`
- Změnit: `PmTracker.Tests.Api/TestInfrastructure/PmTrackerWebAppFactory.cs`
- Test: `PmTracker.Tests.Unit/Export/PdfExportFileNameTests.cs`
- Test: `PmTracker.Tests.Api/Controllers/ExportPdfPrintTests.cs`

**Rozhraní:**
- Používá z Task 2: `IPdfRenderer`, `PdfRenderRequest`, `PdfRenderResult`
- Používá z Task 3: `ChromiumPdfRenderer`
- Poskytuje dál: `IViewRenderer.RenderToStringAsync(ControllerContext, string, object) → Task<string>`; `PdfExportFileName.Build(PdfExportTemplateViewModel, DateTime) → string`; `ExportController.BuildPrintResultAsync(...)`

- [ ] **Krok 1: Napsat padající test**

`PmTracker.Tests.Api/TestInfrastructure/FakePdfRenderer.cs`:

```csharp
using PmTracker.Web.Services.Export;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// Podvržený generátor — Api testy nesmí spouštět prohlížeč. Zachytí, co dostal,
/// a umí na povel selhat, aby šlo ověřit návrat na HTML tisk.
/// Testy sdílí factory (kolekce běží sériově); kdo přepne <see cref="ShouldFail"/>,
/// musí ho ve finally vrátit zpět.
/// </summary>
public sealed class FakePdfRenderer : IPdfRenderer
{
    public static readonly byte[] Payload = "%PDF-1.4 podvržený obsah"u8.ToArray();

    public string? LastHtml { get; private set; }
    public string? LastStylesheetPath { get; private set; }
    public bool ShouldFail { get; set; }

    public Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken ct)
    {
        LastHtml = request.Html;
        LastStylesheetPath = request.StylesheetPath;

        return Task.FromResult(ShouldFail
            ? PdfRenderResult.Failure("test: generátor záměrně selhal")
            : PdfRenderResult.Success(Payload));
    }
}
```

V `PmTracker.Tests.Api/TestInfrastructure/PmTrackerWebAppFactory.cs` do bloku `builder.ConfigureServices(services => { ... })`, hned za náhradu `IMemoryCache`:

```csharp
            // Serverové PDF (2026-09-04): Api testy nesmí spouštět prohlížeč.
            services.RemoveAll<IPdfRenderer>();
            services.AddSingleton<FakePdfRenderer>();
            services.AddSingleton<IPdfRenderer>(sp => sp.GetRequiredService<FakePdfRenderer>());
```

a doplnit `using PmTracker.Web.Services.Export;`.

`PmTracker.Tests.Api/Controllers/ExportPdfPrintTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Serverové PDF (2026-09-04): tiskové akce vrací dokument, ne HTML stránku.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportPdfPrintTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportPdfPrintTests(ApiSqlFixture fixture) => _fixture = fixture;

    private FakePdfRenderer Renderer => _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>();

    [Fact]
    public async Task ProjektTisk_ReturnsPdf_OpenedInline()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Export/Projekt/1/Tisk");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var disposition = response.Content.Headers.ContentDisposition!;
        disposition.DispositionType.Should().Be("inline",
            "rozhodnutí U4 — PDF se otevře v prohlížečce, nestahuje se");
        disposition.FileNameStar.Should().EndWith(".pdf");
        disposition.FileNameStar.Should().Contain("projekt");
    }

    [Fact]
    public async Task ProjektTisk_FeedsRenderedTemplate_AndPrintStylesheet()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        await client.GetAsync("/Export/Projekt/1/Tisk");

        Renderer.LastHtml.Should().NotBeNullOrWhiteSpace();
        Renderer.LastHtml.Should().Contain("records-table",
            "do generátoru musí jít vyrenderovaná tisková šablona, ne prázdný dokument");
        Renderer.LastStylesheetPath.Should().EndWith("pdf-export.css");
    }

    [Fact]
    public async Task UkolTisk_ReturnsPdf_WithRecordNumberInFileName()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Export/Ukol/1/Tisk?projektId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Contain("zaznam");
    }
}
```

- [ ] **Krok 2: Spustit test a ověřit, že padá**

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportPdfPrintTests"`
Očekávat: chyba překladu (`FakePdfRenderer` se nedá registrovat, `IPdfRenderer` není v DI) nebo `text/html` místo `application/pdf`.

- [ ] **Krok 3: Napsat renderer šablony**

`PmTracker.Web/Services/Export/RazorViewRenderer.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace PmTracker.Web.Services.Export;

public interface IViewRenderer
{
    Task<string> RenderToStringAsync(ControllerContext context, string viewPath, object model);
}

/// <summary>
/// Vyrenderuje Razor šablonu do řetězce, aby ji šlo předat sazbě PDF.
/// Serverové PDF (2026-09-04), spec §6.
/// </summary>
public sealed class RazorViewRenderer : IViewRenderer
{
    private readonly ICompositeViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;

    public RazorViewRenderer(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
    }

    public async Task<string> RenderToStringAsync(ControllerContext context, string viewPath, object model)
    {
        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath, isMainPage: true);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"Tiskovou šablonu {viewPath} se nepodařilo najít.");
        }

        await using var writer = new StringWriter();

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };
        var tempData = new TempDataDictionary(context.HttpContext, _tempDataProvider);
        var viewContext = new ViewContext(
            context, viewResult.View, viewData, tempData, writer, new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
```

- [ ] **Krok 4: Zaregistrovat služby**

Do `PmTracker.Web/Services/Data/DataStoreServiceCollectionExtensions.cs` hned za řádek `services.AddScoped<IWordExportService, OpenXmlWordExportService>();`:

```csharp
        // Serverové PDF (2026-09-04) — sazba tisku headless prohlížečem, spec §6.
        services.AddSingleton<PmTracker.Web.Services.Export.IBrowserExecutableResolver,
                              PmTracker.Web.Services.Export.BrowserExecutableResolver>();
        services.AddSingleton<PmTracker.Web.Services.Export.IPdfRenderer,
                              PmTracker.Web.Services.Export.ChromiumPdfRenderer>();
        services.AddScoped<PmTracker.Web.Services.Export.IViewRenderer,
                           PmTracker.Web.Services.Export.RazorViewRenderer>();
```

Do `PmTracker.Web/Program.cs` k ostatním `builder.Services` registracím (za řádek 51 s `IApplicationVersionProvider`):

```csharp
builder.Services.Configure<PmTracker.Web.Services.Export.PdfExportOptions>(
    builder.Configuration.GetSection(PmTracker.Web.Services.Export.PdfExportOptions.SectionName));
```

- [ ] **Krok 5: Napsat padající test na název souboru a doplnit stavitele**

`PmTracker.Tests.Unit/Export/PdfExportFileNameTests.cs`:

```csharp
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>Serverové PDF (2026-09-04), spec §6.5: názvy stahovaných dokumentů.</summary>
public sealed class PdfExportFileNameTests
{
    private static readonly DateTime Ctvrty = new(2026, 9, 4);

    private static PdfExportRecordViewModel Record(string cisloViditelne) => new()
    {
        CisloViditelne = cisloViditelne,
        Nazev = "Testovací záznam",
        KategorieKod = "U",
        Kategorie = "Úkol",
        Stav = "Rozpracováno",
        Vlastnik = "Pavel Admin",
        SubsystemKod = "INT",
        Subsystem = "Integrace",
        Spoluprace = Array.Empty<string>()
    };

    private static PdfExportTemplateViewModel Model(
        string? zkratka,
        string variant,
        int? jednaniCislo = null,
        string? cisloViditelne = null) => new()
    {
        ProjektNazev = "Ekonomický IS",
        ProjektZkratka = zkratka,
        JednaniStav = "Uzavřeno",
        JednaniCislo = jednaniCislo,
        Vytvoril = "Pavel Admin",
        SnapshotSummary = string.Empty,
        AppliedRuleSummary = Array.Empty<string>(),
        Legenda = Array.Empty<PdfLegendItemViewModel>(),
        Zaznamy = cisloViditelne is null
            ? Array.Empty<PdfExportRecordViewModel>()
            : [Record(cisloViditelne)],
        NormalizedVariant = variant
    };

    [Fact]
    public void Build_NamesProjectExport()
        => PdfExportFileName.Build(Model("EIS", "project_all"), Ctvrty)
            .Should().Be("Zapiska_EIS_projekt_2026-09-04.pdf");

    [Fact]
    public void Build_NamesMeetingByNumber()
        => PdfExportFileName.Build(Model("EIS", "meeting", jednaniCislo: 12), Ctvrty)
            .Should().Be("Zapiska_EIS_jednani-12_2026-09-04.pdf");

    [Fact]
    public void Build_NamesTaskByVisibleRecordNumber()
        => PdfExportFileName.Build(Model("EIS", "task_single", cisloViditelne: "901-1"), Ctvrty)
            .Should().Be("Zapiska_EIS_zaznam-901-1_2026-09-04.pdf");

    [Fact]
    public void Build_ReplacesSeparatorsAndSpaces()
        => PdfExportFileName.Build(Model("EIS/ACR 2", "project_all"), Ctvrty)
            .Should().Be("Zapiska_EIS_ACR_2_projekt_2026-09-04.pdf");

    [Fact]
    public void Build_KeepsCzechDiacritics()
        => PdfExportFileName.Build(Model("Zápiska", "project_all"), Ctvrty)
            .Should().Contain("Zápiska",
                "diakritika je v názvu souboru platná a přenese se hlavičkou filename*");

    [Fact]
    public void Build_FallsBackWhenProjectCodeMissing()
        => PdfExportFileName.Build(Model(null, "project_all"), Ctvrty)
            .Should().Be("Zapiska_Projekt_projekt_2026-09-04.pdf");

    [Fact]
    public void Build_FallsBackWhenTaskHasNoRecord()
        => PdfExportFileName.Build(Model("EIS", "task_single"), Ctvrty)
            .Should().Be("Zapiska_EIS_zaznam-0_2026-09-04.pdf",
                "prázdný export úkolu nesmí vyrobit rozbitý název");
}
```

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PdfExportFileNameTests"`
Očekávat: chyba překladu — `PdfExportFileName` neexistuje.

Pak vytvořit `PmTracker.Web/Services/Export/PdfExportFileName.cs`:

```csharp
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Export;

/// <summary>
/// Názvy stahovaných tiskových PDF. Spec 2026-09-04-serverove-pdf-tisk-design.md, §6.5.
/// </summary>
public static class PdfExportFileName
{
    // Vlastní seznam místo Path.GetInvalidFileNameChars() — ten vrací na Windows a na
    // Unixu různé sady, takže by se testy chovaly jinak na vývojovém Macu než na serveru.
    private static readonly char[] Invalid = ['/', '\\', ':', '*', '?', '"', '<', '>', '|', ' '];

    public static string Build(PdfExportTemplateViewModel model, DateTime localNow)
    {
        var scope = model.NormalizedVariant switch
        {
            "meeting" => $"jednani-{model.JednaniCislo}",
            "task_single" => $"zaznam-{Sanitize(model.Zaznamy.FirstOrDefault()?.CisloViditelne, "0")}",
            _ => "projekt"
        };

        return $"Zapiska_{Sanitize(model.ProjektZkratka, "Projekt")}_{scope}_{localNow:yyyy-MM-dd}.pdf";
    }

    private static string Sanitize(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return string.Concat(value.Trim().Select(ch => Invalid.Contains(ch) ? '_' : ch));
    }
}
```

Spustit: `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PdfExportFileNameTests"`
Očekávat: 7/7 prošly.

- [ ] **Krok 6: Přepnout tiskové akce na PDF**

V `PmTracker.Web/Controllers/ExportController.cs` doplnit `using` na začátek souboru:

```csharp
using Microsoft.Net.Http.Headers;
```

Do třídy přidat konstanty vedle `WordContentType`:

```csharp
    private const string PdfContentType = "application/pdf";
    private const string PdfViewPath = "~/Views/Export/PdfTemplate.cshtml";
```

Do pole a konstruktoru přidat tři závislosti (`BaseController` logger nenabízí, vytvoří se z továrny):

```csharp
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IViewRenderer _viewRenderer;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExportController> _logger;
```

V konstruktoru za `IWordExportService wordExportService` doplnit parametry
`IPdfRenderer pdfRenderer, IViewRenderer viewRenderer, IWebHostEnvironment environment`
a do těla:

```csharp
        _pdfRenderer = pdfRenderer;
        _viewRenderer = viewRenderer;
        _environment = environment;
        _logger = loggerFactory.CreateLogger<ExportController>();
```

Přidat společný helper vedle `BuildWordResult`:

```csharp
    /// <summary>
    /// Serverové PDF (2026-09-04): vyrenderuje tiskovou šablonu, nechá ji vysázet
    /// prohlížečem a vrátí dokument k otevření. Když sazba selže, vrátí se dnešní
    /// HTML tisk — tisk tak nikdy nepřestane fungovat (spec §8).
    /// </summary>
    private async Task<IActionResult> BuildPrintResultAsync(
        PdfExportTemplateViewModel model, CancellationToken ct)
    {
        var html = await _viewRenderer.RenderToStringAsync(ControllerContext, PdfViewPath, model);
        var stylesheetPath = Path.Combine(_environment.WebRootPath, "css", "pdf-export.css");

        var result = await _pdfRenderer.RenderAsync(
            new PdfRenderRequest { Html = html, StylesheetPath = stylesheetPath }, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "PDF se nevyrobilo ({Duvod}), tisk pokračuje HTML cestou.", result.FailureReason);
            return View(PdfViewPath, model);
        }

        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = PdfExportFileName.Build(model, GetLocalNow())
        }.ToString();

        return File(result.Bytes!, PdfContentType);
    }

```

Ve všech třech tiskových akcích nahradit
`return View("~/Views/Export/PdfTemplate.cshtml", model);`
za
`return await BuildPrintResultAsync(model, ct);`

Jsou to tři výskyty: `ProjektTisk` (dnes ř. 69), `JednaniTisk` (ř. 132), `UkolTisk` (ř. 171).
Ověřit, že po úpravě nezůstal žádný další: `grep -n 'PdfTemplate.cshtml' PmTracker.Web/Controllers/ExportController.cs`
— smí zůstat jen konstanta `PdfViewPath` a fallback ve `BuildPrintResultAsync`.

**Wordové akce se nemění.**

- [ ] **Krok 7: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportPdfPrintTests"`
Očekávat: 3/3 prošly.

- [ ] **Krok 8: Ověřit, že se nerozbily existující testy tisku**

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~Export"`
Očekávat: zelené. Testy, které dosud čekaly HTML z tiskové akce, teď dostanou PDF — pokud takový existuje, přepsat ho na kontrolu `application/pdf` a v komentáři uvést proč.

- [ ] **Krok 9: Checkpoint**

Shrnout: tři tisky vrací PDF, do generátoru jde vyrenderovaná šablona i tiskové CSS. **Necommitovat.**

---

### Task 5: Návrat na HTML tisk při selhání

**Soubory:**
- Test: `PmTracker.Tests.Api/Controllers/ExportPdfFallbackTests.cs`
- (implementace už vznikla v Task 4, krok 5 — tento task ji zamyká testem)

**Rozhraní:**
- Používá z Task 4: `FakePdfRenderer.ShouldFail`, `ExportController.BuildPrintResultAsync`

- [ ] **Krok 1: Napsat padající test**

`PmTracker.Tests.Api/Controllers/ExportPdfFallbackTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Tests.Api.TestInfrastructure;
using Xunit;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Serverové PDF (2026-09-04), spec §8: když sazba selže, tisk se vrátí na dnešní
/// HTML stránku. Bez tohoto pinu by výpadek prohlížeče na serveru shodil tisk úplně.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportPdfFallbackTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportPdfFallbackTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjektTisk_FallsBackToHtml_WhenRendererFails()
    {
        var renderer = _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        renderer.ShouldFail = true;
        try
        {
            var response = await client.GetAsync("/Export/Projekt/1/Tisk");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "výpadek sazby nesmí uživateli shodit tisk");
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

            var body = await response.Content.ReadAsStringAsync();
            body.Should().Contain("records-table", "vrací se dnešní tisková stránka");
            body.Should().Contain("window.print()",
                "na HTML cestě se má tiskový dialog stále vyvolat sám");
        }
        finally
        {
            renderer.ShouldFail = false;
        }
    }

    [Fact]
    public async Task JednaniTisk_FallsBackToHtml_WhenRendererFails()
    {
        var renderer = _fixture.Factory.Services.GetRequiredService<FakePdfRenderer>();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        renderer.ShouldFail = true;
        try
        {
            var response = await client.GetAsync("/Export/Jednani/1/Tisk?projektId=1");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        }
        finally
        {
            renderer.ShouldFail = false;
        }
    }
}
```

- [ ] **Krok 2: Spustit test a ověřit, že padá — a ověřit ho červeně**

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportPdfFallbackTests"`

Pokud test hned projde (implementace z Task 4 už fallback obsahuje), **je nutné ověřit, že test opravdu něco hlídá**: dočasně v `BuildPrintResultAsync` zakomentovat větev `if (!result.Succeeded)`, spustit test znovu a přesvědčit se, že **selže**. Pak větev vrátit. Bez tohoto kroku není jisté, že pin funguje.

- [ ] **Krok 3: Spustit test a ověřit, že prochází**

Spustit: `dotnet test PmTracker.Tests.Api --filter "FullyQualifiedName~ExportPdfFallbackTests"`
Očekávat: 2/2 prošly.

- [ ] **Krok 4: Checkpoint**

Shrnout: záchranná brzda ověřena červeno-zeleným cyklem. **Necommitovat.**

---

### Task 6: Konfigurace, dokumentace a ověření naživo

**Soubory:**
- Změnit: `PmTracker.Web/appsettings.example.json`
- Změnit: `docs/technical/04-installation-deployment-iis.md`
- Změnit (podmíněně): `PmTracker.Web/wwwroot/css/pdf-export.css:517-520`

**Rozhraní:**
- Používá z Task 1: `PdfExportOptions.SectionName` = `"Export:Pdf"`

- [ ] **Krok 1: Doplnit šablonu konfigurace**

Do `PmTracker.Web/appsettings.example.json` přidat na úroveň ostatních sekcí:

```json
  "Export": {
    "Pdf": {
      "Enabled": true,
      "BrowserExecutablePath": "",
      "TimeoutSeconds": 60,
      "MaxConcurrent": 2
    }
  }
```

Ověřit platnost: `python3 -c "import json;json.load(open('PmTracker.Web/appsettings.example.json'));print('JSON OK')"`

- [ ] **Krok 2: Doplnit deployment checklist**

Do `docs/technical/04-installation-deployment-iis.md` přidat oddíl:

```markdown
## Serverové PDF (tisk)

Tisk projektu, jednání i záznamu vzniká sazbou v headless Edgi na serveru.
Po nasazení ověřit:

1. Edge je nainstalovaný — `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`.
   Jiná cesta se zapíše do `Export:Pdf:BrowserExecutablePath` v `appsettings.json`.
2. Účet aplikačního poolu smí spustit proces prohlížeče (antivirus / politiky spouštění).
3. Dočasný adresář účtu poolu je zapisovatelný — vzniká v něm `%TEMP%\pmtracker-pdf\<guid>`,
   po každém tisku se maže.
4. Kontrola: otevřít tisk projektu. Přijde-li PDF s patičkou „Strana 1 z N", je vše v pořádku.
   Přijde-li stará HTML stránka s tiskovým dialogem, sazba selhala — důvod je
   v logu jako varování „PDF se nevyrobilo".

Vypnutí bez nasazení nové verze: `Export:Pdf:Enabled = false` (tisk se vrátí na HTML cestu).
```

- [ ] **Krok 3: Ověřit okraje na skutečném výstupu**

Spec §6.4 nechává otevřené, zda se okraj z CSS `@page { margin: 5mm }` sčítá s okrajem
zadaným generátoru. Rozhodne se měřením, ne odhadem.

Spustit aplikaci a stáhnout skutečný tisk:

```bash
dotnet run --project PmTracker.Web &
sleep 15
curl -s "http://localhost:5062/Export/Projekt/1/Tisk?asUser=1" -o /tmp/tisk.pdf
python3 -c "
import re
raw = open('/tmp/tisk.pdf','rb').read()
print('je to PDF:', raw[:5] == b'%PDF-')
box = re.search(rb'/MediaBox\s*\[([^\]]*)\]', raw)
print('MediaBox:', box.group(1).decode() if box else 'nenalezen')
"
```

Pak PDF otevřít a porovnat šířku textového bloku s dnešním tiskem přes prohlížeč.
- Sedí-li okraje → nic se nemění.
- Jsou-li viditelně širší (okraje se sečetly) → v `PmTracker.Web/wwwroot/css/pdf-export.css`
  změnit `@page { size: A4 portrait; margin: 5mm; }` na `@page { size: A4 portrait; margin: 0; }`
  a doplnit komentář `/* okraje řídí generátor PDF (2026-09-04), aby se nesčítaly */`.
  Pak znovu spustit `dotnet test PmTracker.Tests.Unit --filter "FullyQualifiedName~PdfTemplateSplitTests"`.

Aplikaci na konci zastavit.

- [ ] **Krok 4: Ověřit patičku na skutečném dokumentu projektu**

Pro vizuální kontrolu skutečného tisku vyrenderovat první stranu:

```bash
cd /tmp && rm -rf ql && mkdir ql && qlmanage -t -s 1400 -o ql tisk.pdf >/dev/null 2>&1 && ls ql/
```

Otevřít `/tmp/ql/tisk.pdf.png` a zkontrolovat, že dole uprostřed je „Strana 1 z N"
a že se patička nepřekrývá s obsahem tabulky.

- [ ] **Krok 5: Plná regrese**

```bash
dotnet build PmTracker.sln -c Debug
dotnet test PmTracker.Tests.Unit
dotnet test PmTracker.Tests.Api
dotnet test PmTracker.Tests.Integration
```

Očekávat: žádné nové selhání. Známé předchozí výpadky (gantt kvartet v Api,
orthogonální selhání dle `project_pre_existing_test_failures`) se nezapočítávají,
ale musí se **vyjmenovat**, ne odbýt.

- [ ] **Krok 6: Závěrečný checkpoint**

Shrnout uživateli: co je hotové, výsledky všech sad, co ověřit ručně na i15
(tisk projektu, jednání i záznamu — přijde PDF, dole uprostřed číslování).
**Necommitovat** — commit dělá uživatel po ručním ověření.
