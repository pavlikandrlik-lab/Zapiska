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
