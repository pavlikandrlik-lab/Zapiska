namespace PmTracker.Web.Services.Security.ViewAs;

public static class LocalReferer
{
    /// <summary>Path+query Refereru, jen když míří na stejný host; jinak null (open-redirect).</summary>
    public static string? Resolve(HttpRequest request)
    {
        var raw = request.Headers.Referer.ToString();
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var referer))
        {
            return null;
        }

        return string.Equals(referer.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
            ? referer.PathAndQuery
            : null;
    }
}
