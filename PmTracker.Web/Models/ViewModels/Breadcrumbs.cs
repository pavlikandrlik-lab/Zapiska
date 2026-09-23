namespace PmTracker.Web.Models.ViewModels;

/// <summary>Jeden drobeček. Url=null → aktuální (aria-current). IsClosable=false → kořen sekce (bez ✕).</summary>
public sealed record Breadcrumb(string Text, string? Url, string? MutedSuffix, bool IsClosable);

/// <summary>Uspořádaná drobečková cesta. Rodič libovolného drobečku = předchozí drobeček.</summary>
public sealed record BreadcrumbTrail(IReadOnlyList<Breadcrumb> Items, string? BackUrl = null)
{
    /// <summary>Cíl šipky ←: explicitní BackUrl (origin/kanonická záložka entity, C1 2026-07-10),
    /// jinak URL předposledního drobečku (rodič aktuálního); null když je jen kořen.</summary>
    public string? ParentUrl => BackUrl ?? (Items.Count >= 2 ? Items[Items.Count - 2].Url : null);
}
