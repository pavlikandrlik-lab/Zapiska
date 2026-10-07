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
            PreferCSSPageSize = request.PreferCssPageSize,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = request.HeaderTemplate ?? PdfFooterTemplate.EmptyHeaderHtml,
            FooterTemplate = request.FooterTemplate ?? PdfFooterTemplate.Html,
            // Změřeno 2026-09-04: když šablona deklaruje @page margin (a pdf-export.css
            // deklaruje 5 mm), Chromium ji respektuje a tyhle hodnoty ignoruje — okraje
            // se NEsčítají. Zůstávají tu jako záloha pro případ šablony bez @page.
            // Pás pro patičku si Chromium rezervuje sám (změřená mezera k obsahu ~3,5 mm).
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
