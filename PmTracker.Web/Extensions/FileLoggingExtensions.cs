using PmTracker.Web.Logging;

namespace PmTracker.Web.Extensions;

public static class FileLoggingExtensions
{
    /// <summary>
    /// Zapne zápis logu do souboru. Bez něj aplikace logovala jen do konzole, kterou IIS
    /// v in-process režimu zahazuje — po chybě tak nezbyla žádná stopa (2026-09-08).
    /// </summary>
    /// <remarks>
    /// Konfigurace je v sekci <c>Logging:File</c>, ale je celá nepovinná: <c>appsettings.json</c>
    /// se na produkci doplňuje ručně a není ve verzování, takže výchozí hodnoty musí dávat
    /// funkční log i bez jediného řádku nastavení.
    /// </remarks>
    public static ILoggingBuilder AddPmTrackerFileLog(
        this ILoggingBuilder logging,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration.GetSection(FileLoggerOptions.SectionName).Get<FileLoggerOptions>()
            ?? new FileLoggerOptions();

        if (!options.Enabled) return logging;

        // Výchozí umístění vedle aplikace, ať to admin najde bez hledání. Když tam
        // identita app poolu psát nesmí, provider sám ustoupí do TEMP.
        if (string.IsNullOrWhiteSpace(options.Directory))
        {
            options.Directory = Path.Combine(environment.ContentRootPath, "logs");
        }

        logging.AddProvider(new FileLoggerProvider(options));
        return logging;
    }
}
