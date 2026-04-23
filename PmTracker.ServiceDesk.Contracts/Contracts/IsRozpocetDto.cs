namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Rozpočtová metrika IS — čerpání vůči limitu.
/// Hodnoty se berou přímo z <c>HOT_IS.limit</c>/<c>cerpani</c>, bez součtu
/// přes subsystem-level tabulky.
/// </summary>
public sealed record IsRozpocetDto(
    int IsId,
    string IsZkratka,
    decimal? Limit,
    decimal? Cerpani)
{
    /// <summary>Procento čerpání 0–100 (nebo null pokud limit není známý).</summary>
    public decimal? ProcentoCerpani
        => Limit is > 0 && Cerpani is not null
            ? Math.Round(Cerpani.Value / Limit.Value * 100m, 1, MidpointRounding.AwayFromZero)
            : null;
}
