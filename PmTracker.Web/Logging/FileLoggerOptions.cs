using Microsoft.Extensions.Logging;

namespace PmTracker.Web.Logging;

/// <summary>
/// Nastavení souborového logu. Čte se ze sekce <c>Logging:File</c>, ale musí fungovat
/// i bez ní — <c>appsettings.json</c> se na produkci doplňuje ručně a není ve verzování,
/// takže výchozí hodnoty jsou nastavené tak, aby log vznikl i bez jediného řádku konfigurace.
/// </summary>
public sealed class FileLoggerOptions
{
    public const string SectionName = "Logging:File";

    /// <summary>Zapnutí/vypnutí. Vypínat je rozumné jen ve vývoji, kde stačí konzole.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Adresář se soubory logu. Prázdno = <c>logs</c> vedle aplikace.</summary>
    public string? Directory { get; set; }

    /// <summary>
    /// Kam ustoupit, když hlavní adresář nejde použít. Prázdno = podadresář v systémovém
    /// TEMP, do kterého identita app poolu zapsat smí. Tiché vypnutí logu by uživatele
    /// vrátilo přesně k chybě bez stopy, kvůli které tenhle provider vznikl.
    /// </summary>
    public string? FallbackDirectory { get; set; }

    /// <summary>
    /// Pod tuhle úroveň se nezapisuje. Error schválně: do souboru patří jen chyby,
    /// jinak adresář zbytečně roste. Na Warning padají i běžné provozní hlášky,
    /// na Information dokonce každý SQL příkaz EF Core — jeden běh testové sady z toho
    /// udělal 5,2 MB. Pro hlubší diagnostiku si admin úroveň dočasně sníží
    /// v Logging:File:MinimumLevel.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Error;

    /// <summary>Prefix názvu souboru; za něj se lepí datum.</summary>
    public string FileNamePrefix { get; set; } = "pmtracker";

    /// <summary>
    /// Po kolika dnech se starý soubor smaže. Nula = mazat se nebude.
    /// Týden je kompromis: chyba z pátku je v pondělí ještě po ruce, ale nic se
    /// v adresáři nedrží měsíce.
    /// </summary>
    public int RetainedDays { get; set; } = 7;
}
