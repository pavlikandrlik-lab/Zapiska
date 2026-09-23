using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace PmTracker.Web.Logging;

/// <summary>
/// Zapisuje log do denního souboru. Vlastní implementace, ne knihovna — nasazení je
/// offline intranet bez přístupu na NuGet (viz docs), takže nová závislost by se na server
/// nedostala.
/// </summary>
/// <remarks>
/// Vzniklo 2026-09-08: aplikace neměla žádný trvalý cíl logů. Psala jen do konzole,
/// kterou IIS v in-process režimu zahazuje (stdout log je ve web.config vypnutý), takže
/// po chybě nezbyla žádná stopa a nešlo zjistit ani proč, ani kde nastala.
///
/// Zapisuje se otevřením a zavřením souboru na každý záznam. Je to dražší než držet
/// stream otevřený, ale nic se neztratí při pádu procesu ani při recyklaci app poolu —
/// a právě v takové chvíli je log potřeba nejvíc. Při výchozí úrovni Error je objem
/// zápisů tak nízký, že to nic nestojí.
/// </remarks>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _zamek = new();
    private string _directory;

    /// <summary>Den, pro který se právě píše. Změna znamená přelom dne a spustí úklid.</summary>
    private DateTime _aktualniDen;

    /// <summary>
    /// Když se nedá psát, log se vzdá a aplikace běží dál. Selhání logování nesmí být
    /// horší než chyba, kterou má zaznamenat.
    /// </summary>
    private bool _vypnuto;

    public FileLoggerProvider(FileLoggerOptions options, TimeProvider? timeProvider = null)
    {
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
        _aktualniDen = _time.GetLocalNow().Date;
        _directory = string.IsNullOrWhiteSpace(options.Directory)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : options.Directory!;

        if (!ZkusitAdresar(_directory))
        {
            // Typicky chybějící právo zápisu pro identitu app poolu. Ustupujeme do TEMP,
            // protože log bez stopy je přesně ten stav, kvůli kterému tohle vzniklo.
            var zaloha = string.IsNullOrWhiteSpace(options.FallbackDirectory)
                ? Path.Combine(Path.GetTempPath(), "pmtracker-logs")
                : options.FallbackDirectory!;

            if (ZkusitAdresar(zaloha)) { _directory = zaloha; }
            else { _vypnuto = true; }
        }
    }

    /// <summary>Adresář je použitelný, jen když do něj jde i zapsat — samotné CreateDirectory nestačí.</summary>
    private bool ZkusitAdresar(string cesta)
    {
        try
        {
            System.IO.Directory.CreateDirectory(cesta);
            var test = Path.Combine(cesta, ".write-test");
            File.WriteAllText(test, string.Empty);
            File.Delete(test);
            SmazatStareSoubory(cesta);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => _loggers.Clear();

    internal bool JeZapnuto(LogLevel level)
        => !_vypnuto && _options.Enabled && level >= _options.MinimumLevel && level != LogLevel.None;

    internal void Zapsat(LogLevel level, string kategorie, string zprava, Exception? vyjimka)
    {
        if (_vypnuto) return;

        var ted = _time.GetLocalNow();
        var radek = new StringBuilder()
            .Append(ted.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(Uroven(level)).Append("] ")
            .Append(kategorie)
            .Append(" - ")
            .Append(zprava);

        if (vyjimka is not null)
        {
            radek.AppendLine().Append(vyjimka);
        }

        radek.AppendLine();

        try
        {
            // Zámek drží pořadí řádků při souběžných požadavcích; bez něj by se
            // dlouhé stack trace navzájem proplétaly a log by nešel číst.
            lock (_zamek)
            {
                // Úklid nesmí běžet jen při startu: aplikace na IIS běží dlouho a mezi
                // restarty by se staré soubory nikdy nesmazaly.
                if (ted.Date != _aktualniDen)
                {
                    _aktualniDen = ted.Date;
                    SmazatStareSoubory(_directory);
                }

                File.AppendAllText(Soubor(ted.Date), radek.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            _vypnuto = true;
        }
    }

    private string Soubor(DateTime den)
        => Path.Combine(
            _directory,
            $"{_options.FileNamePrefix}-{den.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");

    /// <summary>
    /// Stáří se posuzuje podle data v názvu souboru, ne podle času posledního zápisu:
    /// ten umí přepsat kdejaká zálohovací nebo antivirová služba a mazalo by se špatně.
    /// Soubory, jejichž název se nepodaří přečíst, se nechávají být.
    /// </summary>
    private void SmazatStareSoubory(string adresar)
    {
        if (_options.RetainedDays <= 0) return;

        var prefix = _options.FileNamePrefix + "-";
        var hranice = _time.GetLocalNow().Date.AddDays(-_options.RetainedDays);

        foreach (var soubor in System.IO.Directory.GetFiles(adresar, prefix + "*.log"))
        {
            try
            {
                var jmeno = Path.GetFileNameWithoutExtension(soubor);
                if (jmeno.Length <= prefix.Length) continue;

                if (!DateTime.TryParseExact(
                        jmeno[prefix.Length..], "yyyyMMdd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var den))
                {
                    continue;
                }

                if (den.Date < hranice) File.Delete(soubor);
            }
            catch
            {
                // Zamčený nebo cizí soubor úklid nezastaví.
            }
        }
    }

    private static string Uroven(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "FATAL",
        _ => "?????",
    };

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _kategorie;

        public FileLogger(FileLoggerProvider provider, string kategorie)
        {
            _provider = provider;
            _kategorie = kategorie;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _provider.JeZapnuto(logLevel);

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            _provider.Zapsat(logLevel, _kategorie, formatter(state, exception), exception);
        }
    }
}
