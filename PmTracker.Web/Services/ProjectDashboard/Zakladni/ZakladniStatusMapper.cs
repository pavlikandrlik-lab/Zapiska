namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>Kýble stavového rozpadu záznamů (pořadí = pořadí segmentů grafu).</summary>
public enum StatusBucket
{
    Nezahajeno,
    Rozpracovano,
    Hotovo,
    Zruseno
}

/// <summary>
/// Mapuje stav záznamu na <see cref="StatusBucket"/> dle spec pravidel. Hotovo/Zrušeno
/// se odvozuje z <see cref="DatasetState.IsFinal"/> (robustní vůči jiným kódům); jen
/// NEW/CANCEL jsou pojmenované konstanty.
/// </summary>
public static class ZakladniStatusMapper
{
    public const string NotStartedStateCode = "NEW";
    public const string CancelStateCode = "CANCEL";

    /// <summary>Kýble v pořadí segmentů (koláč / skládaný sloupec / legenda).</summary>
    public static readonly IReadOnlyList<StatusBucket> Buckets =
        new[] { StatusBucket.Nezahajeno, StatusBucket.Rozpracovano, StatusBucket.Hotovo, StatusBucket.Zruseno };

    /// <summary>Kýbl záznamu — stav se dohledá v katalogu; neznámý/chybějící stav = Nezahájeno.</summary>
    public static StatusBucket Bucket(DatasetRecord record, IReadOnlyDictionary<int, DatasetState> statesById)
        => Bucket(record.StavId is int id && statesById.TryGetValue(id, out var s) ? s : null);

    public static StatusBucket Bucket(DatasetState? s)
    {
        if (s is null)
        {
            return StatusBucket.Nezahajeno;
        }

        if (s.IsFinal)
        {
            return string.Equals(s.Kod, CancelStateCode, StringComparison.OrdinalIgnoreCase)
                ? StatusBucket.Zruseno
                : StatusBucket.Hotovo;
        }

        return string.Equals(s.Kod, NotStartedStateCode, StringComparison.OrdinalIgnoreCase)
            ? StatusBucket.Nezahajeno
            : StatusBucket.Rozpracovano;
    }

    /// <summary>Lidský popisek kýble (osy/legendy/koláč).</summary>
    public static string Label(StatusBucket bucket) => bucket switch
    {
        StatusBucket.Nezahajeno => "Nezahájeno",
        StatusBucket.Rozpracovano => "Rozpracováno",
        StatusBucket.Hotovo => "Hotovo",
        StatusBucket.Zruseno => "Zrušeno",
        _ => bucket.ToString()
    };
}
