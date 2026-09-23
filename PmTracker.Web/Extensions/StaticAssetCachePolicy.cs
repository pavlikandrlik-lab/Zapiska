namespace PmTracker.Web.Extensions;

/// <summary>
/// Cache-Control pro statické soubory.
/// <para>
/// DS gov v <c>/assets/gov/</c> se mění jen výměnou celé složky při upgradu DS. Vstupní
/// soubory layout verzuje přes asp-append-version, chunky mají hash v názvu a fonty se
/// nemění → smí se cachovat natrvalo (MANUAL kitu, Část B).
/// </para>
/// <para>
/// Ve vývoji dostanou ostatní .js/.css no-cache: ESM moduly se importují relativním URL
/// bez verze (asp-append-version verzuje jen entry site.js), takže by změny v modulech
/// nedorazily do prohlížeče bez ručního vymazání cache. V produkci necháváme standardní
/// caching (deploy = plná výměna souborů + ohlášený hard-refresh).
/// </para>
/// </summary>
public static class StaticAssetCachePolicy
{
    public const string Immutable = "public, max-age=31536000, immutable";
    public const string NoCache = "no-cache, no-store, must-revalidate";

    /// <returns>Hodnota hlavičky Cache-Control, nebo null = hlavičku neměnit.</returns>
    public static string? Resolve(string requestPath, bool isDevelopment)
    {
        if (requestPath.StartsWith("/assets/gov/", StringComparison.OrdinalIgnoreCase))
        {
            return Immutable;
        }

        if (isDevelopment
            && (requestPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || requestPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
        {
            return NoCache;
        }

        return null;
    }
}
